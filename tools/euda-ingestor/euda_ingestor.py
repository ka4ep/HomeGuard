#!/usr/bin/env python3
"""HomeGuard odometer ingestor: VW Group EU Data Act portal -> POST /api/meter-readings.

Logs into the portal (eu-data-act.drivesomethinggreater.com), downloads the newest
data-delivery ZIP for the vehicle, extracts the odometer reading and posts it to
HomeGuard with Source="Auto". HomeGuard upserts one Auto reading per equipment per
day, so reposting the same day is safe.

Prerequisite (manual, once): on the portal, create a *continuous* data request
("All Data", 15-minute interval) for the vehicle. Without it there are no ZIPs
to download. The subscription is valid for one year and must then be renewed.

Portal protocol (OIDC login scrape, proxy_api paths, dataset format) adapted from
TommiG1/HA_VAG-EU-Data-Act (MIT).
"""
from __future__ import annotations

import io
import json
import logging
import os
import re
import sys
import time
import zipfile
from datetime import date, datetime, timezone
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlencode, urljoin, urlparse

import requests

log = logging.getLogger("euda-ingestor")

# ── Portal constants ──────────────────────────────────────────────────────────

BASE_URL = "https://eu-data-act.drivesomethinggreater.com"
OIDC_AUTHORIZE_URL = "https://identity.vwgroup.io/oidc/v1/authorize"
OIDC_SCOPE = "openid cars profile"
OIDC_REDIRECT_URI = BASE_URL + "/login"

VEHICLES_PATH = "/proxy_api/consent/me/vehicles"
METADATA_PATH = "/proxy_api/euda-apim/datarequest/vehicles/{vin}/metadata/partial"
LIST_PATH = "/proxy_api/euda-apim/datadelivery/vehicles/{vin}/{identifier}/list"
DOWNLOAD_PATH = "/proxy_api/euda-apim/datadelivery/vehicles/{vin}/{identifier}/download"

NO_CONTENT_SUFFIX = "_no_content_found.zip"

USER_AGENT = (
    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36"
)

# OIDC client ids per brand (same portal, different identity client).
BRAND_CLIENTS = {
    "volkswagen": ("VOLKSWAGEN_PASSENGER_CARS", "9b58543e-1c15-4193-91d5-8a14145bebb0@apps_vw-dilab_com"),
    "audi":       ("AUDI",  "cc29b87a-5e9a-4362-aecf-5adea6b01bbb@apps_vw-dilab_com"),
    "skoda":      ("SKODA", "3ea88bf9-1d4e-4a68-b3ad-4098c1f1d246@apps_vw-dilab_com"),
    "seat":       ("SEAT",  "f85e5b69-e3b2-43aa-9c0d-1b7d0e0b576f@apps_vw-dilab_com"),
    "cupra":      ("CUPRA", "f85e5b69-e3b2-43aa-9c0d-1b7d0e0b576f@apps_vw-dilab_com"),
}

# ── Dataset field knowledge ───────────────────────────────────────────────────

# Data items may carry dataFieldName, but older payloads only have the key UUID;
# these UUIDs come from the portal's data dictionary (V5.0).
MILEAGE_VALUE_KEYS = {
    "30cc36fd-71ca-3c09-9296-e94ebd47bd2b",  # mileage.value (MEB)
    "69d437d3-7baa-38bc-b842-f5baf99ddade",  # mileage.value
    "75d65f00-5fa8-334a-826d-e73e91fe5c8d",  # mileage.value
    "41c0805c-43e5-313e-9dfb-356cb8d20f7c",  # mileage (flat, pre-MEB)
}
MILEAGE_UNIT_KEYS = {
    "37fcab93-1d16-329f-b13c-844d244faa04",
    "aefcb497-c0eb-39d0-8a91-2b728fcb2b47",
    "bafabdff-671e-331f-825d-d193cd397c5d",
}
MILEAGE_FIELD_NAMES = {"mileage.value", "mileage"}
MILEAGE_UNIT_FIELD_NAMES = {"mileage.unit"}

# The portal reports "no reading" as out-of-band integer sentinels, not omission.
NUMERIC_SENTINELS = {2**16 - 1, 2**31 - 1, 2**32 - 1}

DISTANCE_UNITS = {
    "KM": "km", "KILOMETER": "km", "KILOMETERS": "km",
    "KILOMETRE": "km", "KILOMETRES": "km",
    "MI": "mi", "MILE": "mi", "MILES": "mi",
}
MI_PER_KM = 1.609344

CAPTURED_TIME_SUFFIXES = ("car_captured_time", "car_captured_utc_timestamp")

MAX_DATASETS_PER_CYCLE = 12


class PortalError(Exception):
    pass


class AuthError(PortalError):
    pass


# ── VW identity page scraping (adapted from HA_VAG-EU-Data-Act, MIT) ──────────


class _FormParser(HTMLParser):
    """Extract the first <form> action and all input fields."""

    def __init__(self) -> None:
        super().__init__()
        self.action: str | None = None
        self.fields: dict[str, str] = {}
        self._in_form = False
        self._done = False

    def handle_starttag(self, tag, attrs):
        if self._done:
            return
        a = dict(attrs)
        if tag == "form" and self.action is None:
            self.action = a.get("action")
            self._in_form = True
        elif tag == "input" and self._in_form:
            if name := a.get("name"):
                self.fields[name] = a.get("value") or ""

    def handle_endtag(self, tag):
        if tag == "form" and self._in_form:
            self._in_form = False
            self._done = True


def _extract_template_model(html: str) -> dict:
    """Extract the identity page's JS ``templateModel`` object (hmac, relayState, error)."""
    idx = html.find("templateModel")
    if idx == -1:
        return {}
    brace = html.find("{", idx)
    if brace == -1:
        return {}
    depth = 0
    for i in range(brace, len(html)):
        c = html[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                try:
                    return json.loads(html[brace : i + 1])
                except ValueError:
                    return {}
    return {}


def _login_fields(html: str) -> tuple[dict[str, str], str | None]:
    """Merge HTML hidden inputs with the JS templateModel/csrf for a login POST."""
    parser = _FormParser()
    parser.feed(html)
    fields = dict(parser.fields)
    model = _extract_template_model(html)
    for key in ("hmac", "relayState"):
        if model.get(key):
            fields[key] = model[key]
    if email := (model.get("emailPasswordForm") or {}).get("email"):
        fields.setdefault("email", email)
    if m := re.search(r"csrf_token\s*[:=]\s*['\"]([^'\"]+)['\"]", html):
        fields.setdefault("_csrf", m.group(1))
    return fields, parser.action


def _login_error(html: str) -> str | None:
    model = _extract_template_model(html)
    err = model.get("error") or model.get("errorCode")
    if isinstance(err, dict):
        return err.get("text") or err.get("errorCode") or str(err)
    return str(err) if err else None


# ── Portal client ─────────────────────────────────────────────────────────────


class PortalClient:
    def __init__(self, email: str, password: str, brand: str) -> None:
        if brand not in BRAND_CLIENTS:
            raise PortalError(f"Unknown brand {brand!r}; one of {sorted(BRAND_CLIENTS)}")
        self._email = email
        self._password = password
        self._brand = brand
        self._session = requests.Session()
        self._session.headers["User-Agent"] = USER_AGENT
        self._logged_in = False

    def login(self) -> None:
        # 0. Prime portal session cookies (AEM load balancer).
        try:
            self._session.get(f"{BASE_URL}/", timeout=30)
        except requests.RequestException:
            pass

        # 1. Start the OIDC flow at the identity provider; lands on the signin page.
        state_key, client_id = BRAND_CLIENTS[self._brand]
        authorize_url = OIDC_AUTHORIZE_URL + "?" + urlencode({
            "client_id": client_id,
            "response_type": "code",
            "scope": OIDC_SCOPE,
            "state": f"de__de__{state_key}",
            "redirect_uri": OIDC_REDIRECT_URI,
            "prompt": "login",
        })
        resp = self._session.get(authorize_url, timeout=30)
        signin_url, signin_html = resp.url, resp.text

        # 2. POST the email (identifier step).
        fields, action = _login_fields(signin_html)
        if "hmac" not in fields or "_csrf" not in fields:
            raise AuthError(f"Could not parse the sign-in form (fields: {sorted(fields)})")
        fields["email"] = self._email
        resp = self._session.post(
            urljoin(signin_url, action or ""), data=fields,
            headers={"Referer": signin_url}, timeout=30)
        authenticate_url, authenticate_html = resp.url, resp.text

        # 3. POST the password. Its hidden fields live in the JS templateModel;
        #    the action URL must not carry ?relayState= (server rejects the duplicate).
        fields2, action2 = _login_fields(authenticate_html)
        if "hmac" not in fields2 or "_csrf" not in fields2:
            raise AuthError(_login_error(authenticate_html)
                            or "Identity portal did not return the password form")
        fields2["email"] = self._email
        fields2["password"] = self._password
        authenticate_action = (urljoin(authenticate_url, action2) if action2
                               else authenticate_url.split("?", 1)[0])
        resp = self._session.post(
            authenticate_action, data=fields2,
            headers={"Referer": authenticate_url}, timeout=30)
        if resp.status_code >= 400:
            raise AuthError(_login_error(resp.text) or f"Login rejected (HTTP {resp.status_code})")

        # 4. A completed flow lands back on the portal host; bad credentials
        #    re-render the identity sign-in page.
        landing = resp.url
        if "signin-service" in landing or "/error" in landing:
            raise AuthError("Login failed - check email and password")
        if urlparse(landing).netloc != urlparse(BASE_URL).netloc:
            raise AuthError(f"Login did not complete (ended at {landing})")
        self._logged_in = True
        log.info("Portal login ok (%s)", self._brand)

    def _get(self, url: str, *, headers: dict | None = None, retry: bool = True) -> requests.Response:
        if not self._logged_in:
            self.login()
        resp = self._session.get(url, headers=headers, timeout=60)
        if resp.status_code in (401, 403) and retry:
            log.info("Session expired (HTTP %s), re-authenticating", resp.status_code)
            self._logged_in = False
            return self._get(url, headers=headers, retry=False)
        return resp

    def _get_json(self, url: str, *, headers: dict | None = None):
        resp = self._get(url, headers=headers)
        if resp.status_code >= 400:
            raise PortalError(f"GET {url} -> HTTP {resp.status_code}")
        try:
            return resp.json()
        except ValueError as err:
            raise PortalError(f"Invalid JSON from {url}: {err}") from err

    def list_vins(self) -> list[str]:
        payload = self._get_json(f"{BASE_URL}{VEHICLES_PATH}?viewPosition=FRONT_LEFT")
        vins: list[str] = []

        def walk(node):
            if isinstance(node, dict):
                vin = node.get("vin") or node.get("vehicleIdentificationNumber")
                if isinstance(vin, str) and len(vin) == 17 and vin not in vins:
                    vins.append(vin)
                for v in node.values():
                    walk(v)
            elif isinstance(node, list):
                for v in node:
                    walk(v)

        walk(payload)
        return vins

    def get_identifier(self, vin: str) -> str:
        """Data-request identifier of the active continuous subscription."""
        meta = self._get_json(f"{BASE_URL}{METADATA_PATH.format(vin=vin)}")
        identifier = meta.get("Identifier") or meta.get("identifier")
        if not identifier:
            raise PortalError(
                "No data-request identifier — create a continuous 'All Data' "
                "request on the portal first")
        return identifier

    def list_datasets(self, vin: str, identifier: str) -> list[dict]:
        """Available ZIPs, newest first: [{name, createdOn, size}]."""
        url = f"{BASE_URL}{LIST_PATH.format(vin=vin, identifier=identifier)}"
        resp = self._get(url, headers={"type": "partial"})
        if resp.status_code == 404:
            return []  # nothing delivered yet
        if resp.status_code >= 400:
            raise PortalError(f"GET {url} -> HTTP {resp.status_code}")
        data = resp.json()
        files = data if isinstance(data, list) else data.get("files", [])
        files = [f for f in files if not f.get("name", "").endswith(NO_CONTENT_SUFFIX)]
        return sorted(files, key=lambda f: (f.get("createdOn") or "", f.get("name") or ""),
                      reverse=True)

    def download_dataset(self, vin: str, identifier: str, name: str) -> dict:
        """Download one ZIP and return the JSON payload inside it."""
        url = f"{BASE_URL}{DOWNLOAD_PATH.format(vin=vin, identifier=identifier)}"
        resp = self._get(url, headers={"filename": name, "type": "partial"})
        if resp.status_code >= 400:
            raise PortalError(f"Download {name} -> HTTP {resp.status_code}")
        try:
            with zipfile.ZipFile(io.BytesIO(resp.content)) as zf:
                members = [n for n in zf.namelist() if n.lower().endswith(".json")]
                if not members:
                    raise PortalError(f"No JSON inside {name}")
                with zf.open(members[0]) as fh:
                    return json.loads(fh.read().decode("utf-8"))
        except (zipfile.BadZipFile, ValueError) as err:
            raise PortalError(f"Could not read {name}: {err}") from err


# ── Mileage extraction ────────────────────────────────────────────────────────


def extract_mileage(payload: dict, target_unit: str) -> tuple[float, date] | None:
    """Return (odometer in target_unit, reading date) or None if the dataset has no usable reading.

    One dataset can carry several mileage slots from report snapshots that lag
    each other; the odometer is monotonic, so the highest non-sentinel value wins.
    """
    values: list[float] = []
    unit_raw: str | None = None
    captured: list[datetime] = []

    for item in payload.get("Data", []):
        key = item.get("key") or ""
        name = item.get("dataFieldName") or ""
        raw = item.get("value")

        if name in MILEAGE_UNIT_FIELD_NAMES or key in MILEAGE_UNIT_KEYS:
            if isinstance(raw, str) and raw.strip():
                unit_raw = raw.strip()
        elif name in MILEAGE_FIELD_NAMES or key in MILEAGE_VALUE_KEYS:
            try:
                v = float(str(raw).strip())
            except (TypeError, ValueError):
                continue
            if v >= 0 and v not in NUMERIC_SENTINELS:
                values.append(v)
        elif name.rsplit(".", 1)[-1] in CAPTURED_TIME_SUFFIXES and isinstance(raw, str):
            try:
                captured.append(datetime.fromisoformat(raw.replace("Z", "+00:00")))
            except ValueError:
                pass

    if not values:
        return None

    value = max(values)
    unit = DISTANCE_UNITS.get((unit_raw or "").upper(), "km")
    if unit == "mi" and target_unit == "km":
        value *= MI_PER_KM
    elif unit == "km" and target_unit == "mi":
        value /= MI_PER_KM

    reading_date = max(captured).date() if captured else datetime.now(timezone.utc).date()
    return round(value, 1), reading_date


# ── HomeGuard client ──────────────────────────────────────────────────────────


def post_reading(cfg: dict, value: float, reading_date: date, note: str) -> None:
    resp = requests.post(
        f"{cfg['homeguard_url'].rstrip('/')}/api/meter-readings",
        json={
            "equipmentId": cfg["equipment_id"],
            "readingDate": reading_date.isoformat(),
            "value": value,
            "source": "Auto",
            "note": note,
        },
        headers={"X-Api-Key": cfg["api_key"]},
        timeout=30,
    )
    if resp.status_code >= 400:
        raise RuntimeError(f"HomeGuard rejected the reading: HTTP {resp.status_code} {resp.text[:300]}")


# ── State (skip already-processed datasets across cycles) ─────────────────────


def load_state(path: Path) -> dict:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def save_state(path: Path, state: dict) -> None:
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(state), encoding="utf-8")
    except OSError as err:
        log.warning("Could not persist state to %s: %s", path, err)


# ── Main cycle ────────────────────────────────────────────────────────────────


def run_cycle(portal: PortalClient, cfg: dict, state: dict, dry_run: bool) -> None:
    vin = cfg.get("vin")
    if not vin:
        vins = portal.list_vins()
        if len(vins) != 1:
            raise PortalError(f"EUDA_VIN not set and portal reports {len(vins)} vehicles: {vins}")
        vin = vins[0]
        cfg["vin"] = vin
        log.info("Auto-detected VIN %s", vin)

    # Fetched every cycle: the identifier changes when the yearly subscription
    # is renewed, so caching it would silently break the poll after renewal.
    identifier = portal.get_identifier(vin)

    datasets = portal.list_datasets(vin, identifier)
    if not datasets:
        log.info("No datasets delivered yet")
        return

    last_seen = state.get("last_dataset")
    for entry in datasets[:MAX_DATASETS_PER_CYCLE]:
        name = entry.get("name", "")
        if name == last_seen:
            log.info("Newest usable dataset %s already processed", name)
            return
        payload = portal.download_dataset(vin, identifier, name)
        extracted = extract_mileage(payload, cfg["meter_unit"])
        if extracted is None:
            log.debug("%s carries no usable mileage, trying older dataset", name)
            continue
        value, reading_date = extracted
        if dry_run:
            log.info("[dry-run] would post %s %s @ %s (from %s)",
                     value, cfg["meter_unit"], reading_date, name)
            return
        post_reading(cfg, value, reading_date,
                     note=f"EU Data Act, VIN {vin}, {name}")
        log.info("Posted %s %s @ %s (from %s)", value, cfg["meter_unit"], reading_date, name)
        state["last_dataset"] = name
        return

    log.warning("No usable mileage in the %d newest datasets", MAX_DATASETS_PER_CYCLE)


def main() -> int:
    logging.basicConfig(
        level=os.environ.get("LOG_LEVEL", "INFO"),
        format="%(asctime)s %(levelname)s %(message)s",
        stream=sys.stdout,
    )

    required = {
        "EUDA_EMAIL": "portal login email",
        "EUDA_PASSWORD": "portal login password",
        "HOMEGUARD_EQUIPMENT_ID": "HomeGuard equipment GUID for the vehicle",
        "HOMEGUARD_API_KEY": "HomeGuard machine API key (Auth__ApiKey on the server)",
    }
    missing = [k for k in required if not os.environ.get(k)]
    if missing:
        for k in missing:
            log.error("Missing env %s (%s)", k, required[k])
        return 2

    cfg = {
        "vin": os.environ.get("EUDA_VIN") or None,
        "homeguard_url": os.environ.get("HOMEGUARD_URL", "http://homeguard:8080"),
        "api_key": os.environ["HOMEGUARD_API_KEY"],
        "equipment_id": os.environ["HOMEGUARD_EQUIPMENT_ID"],
        "meter_unit": os.environ.get("HOMEGUARD_METER_UNIT", "km"),
    }
    dry_run = "--dry-run" in sys.argv
    run_once = dry_run or "--once" in sys.argv
    interval_min = int(os.environ.get("POLL_INTERVAL_MINUTES", "360"))
    state_path = Path(os.environ.get("STATE_FILE", "/data/state.json"))

    portal = PortalClient(
        os.environ["EUDA_EMAIL"], os.environ["EUDA_PASSWORD"],
        os.environ.get("EUDA_BRAND", "cupra").lower())
    state = load_state(state_path)

    while True:
        try:
            run_cycle(portal, cfg, state, dry_run)
            save_state(state_path, state)
        except AuthError as err:
            log.error("Portal authentication failed: %s", err)
            if run_once:
                return 1
        except Exception as err:  # noqa: BLE001 — a cycle must never kill the loop
            log.error("Cycle failed: %s", err)
            if run_once:
                return 1
        if run_once:
            return 0
        time.sleep(interval_min * 60)


if __name__ == "__main__":
    sys.exit(main())
