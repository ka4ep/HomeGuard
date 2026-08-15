# HomeGuard Contracts & Payments — Design Specification

Covers insurance policies, subscriptions, loans and leases, and how they surface
on the home screen. Companion to `timeline-spec.md`.

Status: **design agreed 2026-08-10** (§13). Phases 0–2 built as of 2026-08-12: domain,
schema, API, and the screens. Phase 3 (interest, amortisation, and — pulled forward
from Phase 4 — the monthly cash-flow rollup) built as of 2026-08-15: see §14 for what
shipped, the fallback ladder that replaces the original "stay silent without a rate"
stance, and what Phase 3 still leaves for later. Phases 4–6 built the same day: see
§15 for what shipped and, importantly, what did not — background badge updates, the
client half of the offline outbox, and phases 7–8 (`critique`/`polish`/`harden`) all
need a live, viewable app to do honestly and were left rather than shipped unverified.

---

## 1. The unifying insight

Insurance, subscriptions, leasing and credit look like four features. They are one:

> A **contract** with a counterparty, running over a period, paid in **N instalments**,
> whose terms **change over time**, and which is usually **already running** when
> the user first enters it.

Everything the user asked for maps onto that sentence:

| Ask | Where it lands |
|---|---|
| Insurance: description, price, 1-year period, 1/2/4/12 payments per year | `Contract(Kind=Insurance)` + `PaymentPlanRevision(IntervalMonths=12/6/3/1)` |
| Rules PDF + optional markdown summary card | `BlobEntry(OwnerEntityType="Contract")` + `Contract.SummaryMarkdown` |
| Open-ended coverage (bank-card insurance) | `Contract.EndDate == null` |
| Leasing / credit, 60–360 instalments | `Contract(Kind=Loan\|Lease)`, same plan, `InstallmentCount=360` |
| Early partial payoff → new count / new amount | `PaymentPlanRevision` + `Payment(Kind=Extra)` |
| "How much paid, how many months left, balance" | computed `ContractSummaryDto` |
| Backfill of a contract that started years ago | `Contract.Opening` (owned value object) |
| Subscriptions with corrections and add-ons | same plan revisions + `PlanAdjustment` lines |

One aggregate, one set of endpoints, one detail page with kind-specific sections.
Four parallel implementations would be four times the code and four inconsistent
summaries on the dashboard.

**Guardrail:** this is a family app, not an accounting system. No double-entry
ledger, no tax handling, no bank integration. The target is "I open the app and
know what I owe, when, and how much is left".

---

## 2. Domain model

New files under `HomeGuard.Domain/Entities/`. Style follows the existing entities:
`sealed`, private ctor for EF, `InitNew()`/`Touch()`, static `Create`, private setters.

### 2.1 Contract (aggregate root)

```csharp
public sealed class Contract : Entity
{
    public Guid?          EquipmentId  { get; private set; }  // null = household-level (Netflix, card insurance)
    public ContractKind   Kind         { get; private set; }
    public string         Name         { get; private set; }  // "KASKO Cupra Born", "Netflix Premium"
    public string?        Provider     { get; private set; }  // insurer / bank / vendor
    public string?        ContractNumber { get; private set; }// policy no., agreement no.

    public DateOnly       StartDate    { get; private set; }
    public DateOnly?      EndDate      { get; private set; }  // null = open-ended
    public RenewalMode    Renewal      { get; private set; }  // None | Auto | Manual
    public int?           CancellationNoticeDays { get; private set; } // notify before the cancel window closes

    public string         Currency     { get; private set; }  // ISO-4217, default from settings
    public ContractStatus Status       { get; private set; }  // Active | Ended | Cancelled | Suspended

    public string?        SummaryMarkdown { get; private set; } // the dedicated card
    public string?        Notes        { get; private set; }    // markdown, same as everywhere else

    // Insurance extras (nullable, only shown for Kind=Insurance)
    public decimal?       CoverageAmount { get; private set; }
    public decimal?       Deductible     { get; private set; }

    public OpeningPosition? Opening    { get; private set; }  // owned, see §5

    public IReadOnlyList<string>              Tags { get; }
    public IReadOnlyList<PaymentPlanRevision> Revisions { get; }
    public IReadOnlyList<Payment>             Payments  { get; }
    public IReadOnlyList<NotificationRule>    NotificationRules { get; }  // reuse the existing owned type
    public IReadOnlyList<BlobEntry>           Attachments { get; }        // OwnerEntityType = "Contract"
}
```

`EquipmentId` nullable is deliberate: a Netflix subscription belongs to the
household, KASKO belongs to a car. When it is set, the contract shows up in
`EquipmentDetail` as a section next to warranties and services, and its cost
feeds "cost of ownership" for that item.

### 2.2 PaymentPlanRevision — the versioned schedule

The single most important decision in this spec: **the payment plan is immutable and
versioned, never edited in place.** Every correction (early payoff, price hike,
term change, pause) appends a revision. The active schedule is the last revision;
history is the whole list; nothing is ever lost.

```csharp
public sealed class PaymentPlanRevision : Entity
{
    public Guid           ContractId     { get; private set; }
    public int            Version        { get; private set; }   // 1, 2, 3…
    public DateOnly       EffectiveFrom  { get; private set; }   // first due date this revision governs
    public RevisionReason Reason         { get; private set; }

    public DateOnly       FirstDueDate   { get; private set; }   // anchor for date generation
    public int            IntervalMonths { get; private set; }   // 1 | 3 | 6 | 12 (or any n)
    public int?           InstallmentCount { get; private set; } // null = open-ended (subscriptions)
    public decimal        InstallmentAmount { get; private set; }

    // Loans / leases only
    public decimal?       RemainingPrincipal  { get; private set; } // balance at EffectiveFrom
    public decimal?       AnnualInterestRate  { get; private set; } // e.g. 0.079m
    public decimal?       ResidualAmount      { get; private set; } // balloon / buy-out
    public DateOnly?      ResidualDueDate     { get; private set; }

    public string?        Note           { get; private set; }
    public IReadOnlyList<PlanAdjustment> Adjustments { get; }      // owned collection
}

/// One line of "добавка": an add-on, rider, or surcharge folded into the instalment.
public sealed class PlanAdjustment            // owned by the revision
{
    public string  Name   { get; private set; }   // "Roadside assistance", "4K plan"
    public decimal Amount { get; private set; }   // signed: +4.99 / −2.00 discount
}
```

Effective instalment = `InstallmentAmount + Σ Adjustments.Amount`. Storing them
separately means the detail page can show *why* the monthly figure is what it is,
and a price change is a new revision with a diffable list of lines.

**Interval covers every case asked for:**

| Ask | IntervalMonths | InstallmentCount |
|---|---|---|
| Insurance, one payment/year | 12 | 1 per term (or n for n years) |
| Insurance, 4 payments/year | 3 | 4 |
| Insurance, 12 payments/year | 1 | 12 |
| Subscription, monthly, forever | 1 | `null` |
| Credit, 84 months | 1 | 84 |
| Mortgage, 30 years | 1 | 360 |

### 2.3 Payment — the actual money event

```csharp
public sealed class Payment : Entity
{
    public Guid          ContractId    { get; private set; }
    public Guid?         PlanRevisionId{ get; private set; }  // which revision produced it
    public int?          InstallmentNo { get; private set; }  // sequence within the plan
    public PaymentKind   Kind          { get; private set; }  // Scheduled | Extra | DownPayment | Residual | Fee | Refund
    public PaymentStatus Status        { get; private set; }  // Planned | Paid | Skipped | Failed

    public DateOnly      DueDate       { get; private set; }
    public decimal       AmountDue     { get; private set; }
    public DateOnly?     PaidDate      { get; private set; }
    public decimal?      AmountPaid    { get; private set; }  // may differ from AmountDue

    public decimal?      PrincipalPart { get; private set; }  // loans: computed split
    public decimal?      InterestPart  { get; private set; }
    public string?       Note          { get; private set; }
    public IReadOnlyList<BlobEntry> Attachments { get; }      // receipt photo
}
```

### 2.4 Enums

```csharp
public enum ContractKind   { Insurance = 1, Subscription = 2, Loan = 3, Lease = 4, Other = 99 }
public enum ContractStatus { Active = 1, Ended = 2, Cancelled = 3, Suspended = 4 }
public enum RenewalMode    { None = 0, Auto = 1, Manual = 2 }
public enum PaymentKind    { Scheduled = 0, Extra = 1, DownPayment = 2, Residual = 3, Fee = 4, Refund = 5 }
public enum PaymentStatus  { Planned = 0, Paid = 1, Skipped = 2, Failed = 3 }
public enum RevisionReason { Initial = 0, PriceChange = 1, EarlyPayment = 2, TermChange = 3,
                             RateChange = 4, Pause = 5, AddOn = 6, Correction = 99 }
public enum EarlyPaymentEffect { ReduceTerm = 0, ReducePayment = 1 }   // command-level, not stored
```

---

## 3. Lifecycle — reuse the service-record state machine

The app already has a three-state model for services. Payments get the identical one,
which means the timeline, the calendar feed and the notification scheduler need no
new concepts:

```
Projected ──materialize──► Planned ──confirm──► Paid
(computed, no DB)          (in DB)              (in DB)
```

- **Projected** — generated at runtime by `ContractScheduleService` from the latest
  revision. Never stored. A 30-year mortgage is 360 projected rows computed on demand,
  not 360 DB rows. Shown greyed on the schedule table and the timeline.
- **Planned** — materialized `MaterializeDaysAhead` days before due (default **14** for
  payments, vs 30 for services). Real row → feeds iCal, feeds push, can be edited.
  Done by a background service modelled on `RecurringRuleMaterializationService`.
- **Paid** — user confirms: date, actual amount, optional receipt photo. For loans the
  principal/interest split is computed and stored at confirmation time.

Date generation uses `FirstDueDate.AddMonths(k * IntervalMonths)` — anchored, not
iterative, so a 31st-of-month contract clamps to Feb 28 without drifting to the 28th
forever. Always generate from the anchor.

---

## 4. Corrections — the hard requirement

Everything below is one operation: `POST /api/contracts/{id}/revisions`. It creates
a new `PaymentPlanRevision` and, when money moved, a `Payment(Kind=Extra)`.

### 4.1 Early partial payoff (the case in the brief)

Input: amount paid, date, effect (`ReduceTerm` | `ReducePayment`).

Interest-free split (insurance, 0 % instalments, subscriptions):

```
B' = B − E
ReduceTerm    → n' = ceil(B' / A),   A unchanged (last instalment = remainder)
ReducePayment → A' = B' / n_left,    n unchanged
```

Amortized loan (annuity), `i = AnnualInterestRate / 12`:

```
A   = P·i / (1 − (1+i)^−n)                       instalment
B_k = P(1+i)^k − A·((1+i)^k − 1)/i               balance after k payments
B'  = B_k − E                                    after the lump sum
ReduceTerm    → n' = −ln(1 − B'·i/A) / ln(1+i),  rounded up
ReducePayment → A' = B'·i / (1 − (1+i)^−n_left)
```

The dialog **previews the result before committing** — this is the whole point of
the feature:

```
Досрочный платёж 5 000 €  ·  12.08.2026

  Было:   48 × 320,00 €     остаток 15 360 €   до 03.2030
  Станет: 32 × 320,00 €     остаток 10 360 €   до 11.2028
          сэкономлено процентов ≈ 1 240 €      ← only when rate is known

  ( ) уменьшить платёж    (•) сократить срок
```

On confirm: `Payment(Kind=Extra, AmountPaid=5000)` + `Revision(v+1, Reason=EarlyPayment,
EffectiveFrom=next due date, RemainingPrincipal=10360, InstallmentCount=32)`.
Nothing already Paid is touched. Ever.

### 4.2 Price change / add-on (subscriptions, insurance riders)

New revision with `Reason=PriceChange|AddOn`, new `InstallmentAmount` and/or new
`Adjustments` list, `EffectiveFrom` = the first billing date at the new price.
The detail page then shows a price history sparkline — "12,99 → 15,49 → 17,99" —
which is exactly the thing people want when deciding whether to cancel.

### 4.3 Pause / suspend

`Reason=Pause`, `Contract.Status=Suspended`, no projections generated while suspended.
Resume = another revision with a new `FirstDueDate`.

### 4.4 Correction of a mistake

`Reason=Correction` — same mechanism. Deliberately no "edit the plan in place":
if the audit trail is optional, people will end up with a summary they cannot explain.

---

## 5. Backfill — contracts that are already running

> "часто договора будут уже активными"

Two levels, both needed:

**Level 1 — opening position (the fast path).** One owned value object on the contract:

```csharp
public sealed class OpeningPosition            // owned, nullable
{
    public DateOnly AsOfDate         { get; }  // tracking starts here
    public int      InstallmentsPaid { get; }  // how many were paid before AsOfDate
    public decimal  AmountPaid       { get; }  // total paid before AsOfDate
    public decimal? RemainingBalance { get; }  // what the bank/insurer says is left
}
```

Entering a 5-year-old mortgage becomes: start date, current balance, current
instalment, remaining months, "уже выплачено 47 платежей на 15 040 €". Done in
one dialog, no back-filling of 47 rows.

**Invariant that prevents double counting:** `Opening` covers everything strictly
before `AsOfDate`; every stored `Payment` must have `DueDate >= AsOfDate`. Enforced
in the domain, checked in the service, surfaced as a validation error in the UI.

**Level 2 — retroactive individual payments.** The user can still add past payments
(`Kind=Scheduled, Status=Paid`) with dates ≥ `AsOfDate`, e.g. after digging out old
receipts. If they want to go further back, they lower `AsOfDate` and reduce the
opening counters — the UI offers "перенести N платежей из свёртки в историю" which
does both sides atomically.

The same applies to insurance renewals: a policy renewed for the 4th year is a new
contract linked to the previous one (`PreviousContractId`, optional, phase 2) or
simply an extended `EndDate` + new revision — recommend the former for insurance
(each year has its own PDF and its own price) and the latter for subscriptions.

---

## 6. Kind-specific behaviour

### Insurance
- Period defaults to 1 year from start; `EndDate = null` allowed for card-linked
  policies that run as long as the card does.
- **Documents:** the rules PDF is a `BlobEntry` with `OwnerEntityType="Contract"`.
  The existing polymorphic blob mechanism and `DocumentCapture.razor` work unchanged.
- **Summary card:** `SummaryMarkdown`, rendered in a dedicated card above the payment
  schedule. This is the "what does this policy actually cover" digest — deductible,
  hotline, claim procedure, exclusions. Kept separate from `Notes` so it can be styled
  as a reference card rather than a scratchpad.
- Key facts strip: coverage amount, deductible, policy number, hotline — a 4-cell grid
  above the summary, always visible.
- Renewal reminder uses `CancellationNoticeDays`: notify at `EndDate − notice − 7d`,
  because the useful moment is *before the cancellation window closes*, not on expiry.

### Subscriptions
- Usually `EndDate = null`, `Renewal = Auto`, `InstallmentCount = null`.
- Add-ons via `PlanAdjustment`; price history via revisions.
- Trial period: `Revision(v1)` with `InstallmentAmount = 0` and `InstallmentCount = 1`,
  then `Revision(v2)` at full price. Trial-ending notification falls out of the
  ordinary payment reminder.
- Cancellation: `Status = Cancelled` + `EndDate` — projections stop, history stays.

### Loans and leases
- `DownPayment` as `Payment(Kind=DownPayment)` before instalment #1.
- Balloon / buy-out: `ResidualAmount` + `ResidualDueDate` → a `Payment(Kind=Residual)`
  that is *not* part of `InstallmentCount`. Essential for car leasing.
- Interest optional: without a rate, the app works in "simple" mode (balance decreases
  by the paid amount) — plenty for interest-free retail instalments and honest about
  what it doesn't know.

---

## 7. Computed summary

`ContractSummaryDto`, produced by `ContractSummaryService`, never stored:

```csharp
public sealed record ContractSummaryDto(
    Guid     ContractId,
    string   Currency,
    int      InstallmentsPaid,      // Opening.InstallmentsPaid + paid Payments
    int?     InstallmentsRemaining, // null for open-ended
    decimal  AmountPaid,            // Opening.AmountPaid + Σ AmountPaid
    decimal? AmountRemaining,
    decimal? RemainingBalance,
    decimal? TotalContractCost,     // paid + remaining (+ residual)
    decimal? InterestPaidToDate,
    DateOnly? NextDueDate,
    decimal?  NextAmount,
    DateOnly? PayoffDate,
    decimal   MonthlyEquivalent,    // effective instalment / IntervalMonths
    double?   ProgressPercent,
    bool      IsOverdue);
```

`MonthlyEquivalent` is what makes cross-contract rollups possible: an annual policy
at 480 € and a 40 €/month subscription both normalise to a monthly figure, so
`/api/finance/monthly` can answer "сколько уходит в месяц" and `EquipmentDetail` can
show the true cost of owning that car.

---

## 8. API surface

Follows the existing `MapGroup` + private static handlers style.

```
GET    /api/contracts                        ?kind=&status=&equipmentId=
POST   /api/contracts
GET    /api/contracts/{id}                   full aggregate + summary
PUT    /api/contracts/{id}
DELETE /api/contracts/{id}
GET    /api/contracts/by-equipment/{equipId}
GET    /api/contracts/expiring?days=60       renewals + cancellation windows

GET    /api/contracts/{id}/summary
GET    /api/contracts/{id}/schedule?from=&to=   projected + planned + paid, merged
PUT    /api/contracts/{id}/opening              backfill
PATCH  /api/contracts/{id}/summary-markdown
PATCH  /api/contracts/{id}/notifications        reuse SetNotificationRulesRequest

GET    /api/contracts/{id}/revisions
POST   /api/contracts/{id}/revisions            price change / term change / pause
POST   /api/contracts/{id}/revisions/preview    ← dry run, returns the "было / станет" diff
POST   /api/contracts/{id}/early-payment        sugar over revisions + Payment(Extra)

GET    /api/contracts/{id}/payments
POST   /api/contracts/{id}/payments
POST   /api/payments/{id}/confirm
PUT    /api/payments/{id}
DELETE /api/payments/{id}

GET    /api/finance/upcoming?days=30            cross-contract, for Home + widget
GET    /api/finance/monthly?months=12           rollup by month and by kind
```

`preview` returning the same shape as the real call, without persisting, is what
lets the early-payoff dialog show consequences before the user commits.

---

## 9. Persistence notes

- Decimals: `HasColumnType("TEXT")`, matching `Equipment.PurchasePrice`.
- `DateOnly`: the existing `dateOnlyConverter` / `nullableDateOnlyConverter`.
- `Tags`: same JSON-serialized `_tags` backing field pattern.
- `PlanAdjustment` and `OpeningPosition`: `OwnsMany` / `OwnsOne` with a shadow PK,
  exactly as `NotificationRule` / `DateRange` are configured today.
- Indexes: `Payment(ContractId, DueDate)`, `Payment(Status, DueDate)` (materializer
  and the "upcoming" query), `Contract(Status, EndDate)` (renewal sweep).
- Writes go through `HomeGuardUnitOfWork` — the SQLite write semaphore already covers it.
- One migration adds four tables: `Contracts`, `PaymentPlanRevisions`,
  `PlanAdjustments`, `Payments`. No changes to existing tables.

### Offline / outbox

New operation types in the outbox vocabulary (`OutboxEntry.OperationType`):
`CreateContract`, `UpdateContract`, `DeleteContract`, `AddPlanRevision`,
`CreatePayment`, `ConfirmPayment`, `SetOpeningPosition`.

`ConfirmPayment` is naturally idempotent on `ClientOperationId`; `AddPlanRevision`
is **not** commutative — two revisions created offline on two devices must be applied
in `CreatedAt` order, and the server must reject a revision whose `EffectiveFrom`
precedes an already-applied one (`SyncAckStatus.Rejected` with a message).
This is the one genuinely tricky offline case in this feature — worth handling
explicitly rather than discovering it later.

### Notifications & calendar

- Payment reminders: default offsets 1 week / 1 day / same day. `NotificationSchedulerService`
  extends to `Payment` rows with `Status=Planned` — same shape as Planned services.
- Renewal reminders: on the contract, offsets 1 month / 1 week, plus the
  cancellation-window rule from §6.
- iCal: payments and renewals join `/api/calendar/feed.ics`.
  **This is also the zero-effort Android answer** — see §10.4.
- Timeline: contracts render as bars (like warranties), payments as markers
  (like services). No new timeline concepts needed.

---

## 10. Ambient signals: icon badge, notifications, calendar

**Decision (2026-08-10):** no native Android app, no home-screen widgets. Attention is
surfaced by the app icon itself, plus the calendar feed. Rationale: a widget needs an
APK, a signing pipeline, its own auth and its own refresh story, to show a number that
the icon can already show.

### 10.1 What actually works, per platform

Verified facts, not assumptions:

- **iOS / iPadOS 16.4+** — `navigator.setAppBadge(n)` works for web apps added to the
  Home Screen. Not exposed to Safari tabs or `WKWebView`. The badge only appears once
  notification permission is granted (which Web Push needs anyway).
- **Chrome on Android** — the Badging API is **not implemented**. Instead Android's
  launcher shows a **notification dot** on the installed PWA icon whenever the app has
  an unread notification, exactly as for native apps. Some launchers render a count
  from the notification.
- **Chrome / Edge on Windows and macOS** — `setAppBadge` works for installed PWAs.
  On Linux the call succeeds but nothing is drawn.
- **Firefox** — unsupported everywhere.

| Platform | Numeric badge | Dot | Mechanism |
|---|---|---|---|
| iOS 16.4+, home screen | yes | yes | `setAppBadge` from the service worker |
| Android, installed WebAPK | launcher-dependent | **yes** | one tag-replaced summary notification |
| Windows / macOS, Chrome or Edge | yes | yes | `setAppBadge` |
| Linux, Firefox | no | no | in-app strip only |

So: compute one number on the server, and let each platform render it however it can.
`setAppBadge` is called unconditionally behind a feature check — it is a no-op where
unsupported, and the notification path carries Android.

### 10.2 The attention number

```
GET /api/attention          →  ETag, cheap, cacheable
{
  "count": 3, "urgent": 1, "soon": 2,
  "items": [
    { "kind": "payment", "severity": "urgent",
      "title": "Лизинг Cupra — просрочен 2 дня", "url": "/contracts/{id}" },
    { "kind": "service", "severity": "soon",  "title": "ТО через 5 дней", "url": "…" },
    { "kind": "contract", "severity": "soon", "title": "KASKO: отказаться до 28.08", "url": "…" }
  ]
}
```

- **urgent** — overdue payments, overdue Planned services, a contract whose
  cancellation window closes within a week.
- **soon** — anything due within `AttentionHorizonDays` (default 7, editable in Settings).
- **count = urgent + soon** — that is the badge.

A badge cannot express "срочно" versus "скоро": the dot is binary and the number is
just a number. So urgency lives in the **notification text** ("1 срочно · 2 на неделе")
and in the colour of the Home-screen attention strip. The icon's only job is
"загляни сюда". That is enough, and it is honest about what the platform can do.

### 10.3 Implementation

**Service worker** — on push, and on `attention` payloads specifically:

```js
async function applyAttention(a) {
  if (navigator.setAppBadge) {
    a.count ? navigator.setAppBadge(a.count) : navigator.clearAppBadge();
  }
  const existing = await self.registration.getNotifications({ tag: 'homeguard-attention' });
  if (a.count === 0) { existing.forEach(n => n.close()); return; }

  await self.registration.showNotification(
    a.urgent > 0 ? `${a.urgent} срочно` : 'HomeGuard',
    {
      body: summarise(a),                 // "Лизинг Cupra просрочен · ещё 2 на неделе"
      icon: 'icon-192.png',
      badge: 'badge-72.png',              // monochrome status-bar glyph — NOT the count
      tag: 'homeguard-attention',         // one tag → replaces in place, never stacks
      renotify: a.urgent > 0,             // re-alert only when something is urgent
      silent:   a.urgent === 0,           // otherwise: dot appears without a buzz
      data: { url: '/attention' },
    });
}
```

One tag means exactly one dot on Android that updates in place instead of piling up —
without this, a family of contracts turns the notification tray into a wall.

**Foreground** — recompute and re-badge on app start, on `visibilitychange`, and after
any mutation that could change the count (confirm payment, complete service). The page
calls `setAppBadge` directly; no round trip through the SW.

**Server** — push an `attention` payload from the daily sweep that materializes Planned
rows and rolls dates over, plus on demand. Never on every write: the badge is a
low-frequency signal.

**Offline** — the SW stores the last attention payload via the existing
`homeGuardDb.cacheSet`, so a cold start shows the last known number rather than nothing.

### 10.4 Hard prerequisite: the PWA icons — **done 2026-08-10**

The problem was that `manifest.json` declared `icon-192.png` and `icon-512.png` and
`service-worker.published.js` passed `icon-192.png` to both `icon:` and `badge:`, while
**neither file existed** — there was only `icon.svg` and `favicon.svg`. So Android
WebAPK installation was unreliable, notifications rendered with a default glyph, and
`badge:` was wrong by type (on Android it is the small monochrome status-bar icon).

What now sits in `src/HomeGuard.Client/wwwroot/`:

| File | Source | Purpose |
|---|---|---|
| `icon.svg` | — | `purpose: any`, rounded plate on transparent |
| `icon-192.png`, `icon-512.png` | `icon.svg` | `purpose: any` raster fallbacks |
| `icon-maskable.svg` | — | full-bleed, opaque, mark inside the 80% safe circle |
| `icon-maskable-512.png` | `icon-maskable.svg` | `purpose: maskable` — Android crops up to 20% per edge |
| `apple-touch-icon-180.png` | `icon-maskable.svg` | iOS composites on an opaque ground, so no transparency |
| `badge.svg` → `badge-72.png` | — | alpha-only silhouette, house knocked out with `fill-rule="evenodd"` |

Notes for whoever regenerates them: every PNG comes from an SVG next to it, so the SVG
is the source of truth — re-render with any rasterizer (`rsvg-convert -w N -h N`,
`magick -background none`, `npx sharp-cli`). The `feDropShadow` filter was removed from
`icon.svg`: several rasterizers drop the filtered shape entirely, and the platform
applies its own shadow anyway.

Also corrected while the files were open: `theme_color` was `#7F77DD` (the retired
warranty purple) and is now `#7a7672`, the actual `.mud-appbar` background, with
`background_color` `#BAB2AC` so the splash matches the page it becomes. `manifest.json`
gained `id`, `scope`, `lang` and three `shortcuts` (Сроки / Обслуживание / Техника —
routes that exist today; add a payments shortcut when the screen lands).

One limitation to know about: **manifest strings are fixed at install time.** The
name, description and shortcut labels cannot follow the in-app language switcher
(§13.8), so they stay Russian. If that ever matters, serve `manifest.json` from the API
and vary it by `Accept-Language` — it does not have to be a static file.

Still to verify on a real device: DevTools → Application → Manifest showing the app as
installable, and one push notification checked for the badge glyph.

### 10.5 Calendar as the second channel

Payments, renewals and services all land in `/api/calendar/feed.ics` as all-day
`VEVENT`s with `VALARM` triggers, `CATEGORIES` per kind, and stable `UID`s so edits
update rather than duplicate. `VTODO` is tempting for "задания", but Google Calendar
ignores it — use events.

Caveat worth designing around: subscribed-calendar refresh on Google's side is
unpredictable (hours, sometimes a day). So the division of labour is:

- **calendar** = ambient, "what does this month look like", tolerant of lag;
- **push + badge** = timely, "act now";
- separate feed URLs per kind (`?kinds=payments,services`) so the user can subscribe
  the family calendar to payments only.

---

## 11. Where the `impeccable` skill fits

Short answer: **not now, not once at the end either — at three specific moments.**

The skill's value is highest where a screen's *shape* is still undecided, and lowest
where the work is CRUD plumbing behind an already-decided layout. Concretely:

| When | Command | Why then |
|---|---|---|
| **Now, once** (30 min) | `/impeccable document` | Generates `DESIGN.md` from the current code — captures the existing palette, Plus Jakarta Sans, MudBlazor conventions, `mud-overrides.css` as the source of truth. Every later pass then *extends* the existing look instead of inventing a competing one. This is the single most valuable thing to run before writing any new UI. |
| **Before writing the contract screens** | `/impeccable shape contracts` | Plans UX/UI before code. The contract detail page is genuinely new information architecture — key-facts strip, markdown summary card, payment schedule with three visual states, price history, early-payoff dialog. Designing it after implementing it means implementing it twice. |
| **After §1–§7 work end-to-end** | `/impeccable critique` then `polish` | Reviews and refines against real data and real edge cases. Empty states, long provider names, 360-row schedules, overdue styling. |
| **With the attention strip and icons (§10)** | `/impeccable delight` scoped to the app icon + attention strip | The icon is now a status surface, and the attention strip is the first thing seen on open. Both are small, visual, and high-traffic — the best possible ratio of design effort to payoff. |
| **Before shipping to the family** | `/impeccable harden` | Error states, empty states, long text, i18n. The moment other people use it, "it broke and said nothing" becomes the top complaint. |

**One warning specific to this repo.** CLAUDE.md records a deliberate decision:
`mud-overrides.css` with `!important` is the single source of truth for colour, and
`HomeGuardTheme.cs` palette settings are inert. A design skill will naturally want to
introduce tokens and may produce CSS that fights those overrides. So:

- run `document` **first** so the constraint is written down where the skill reads it;
- scope each pass to **one screen at a time** and review the diff for new global CSS;
- if it proposes replacing the CSS-override approach with a token system, that is a
  real decision worth making deliberately — not a side effect of a polish pass.

**Do not** run it on backend/domain work, and do not run it on a screen that is still
changing shape weekly — the passes cost real tokens and get overwritten.

---

## 12. Phased plan

| Phase | Content | Rough size |
|---|---|---|
| **0** | ✅ decisions (§13) · ✅ **`/impeccable document`** → `DESIGN.md` + `.impeccable/design.json` · ✅ PWA icons (§10.4) · migration scaffold | small |
| **1** | ✅ **`/impeccable shape`** → `.impeccable/surfaces/contracts.md`, before any Razor is written | small |
| **1.5** | Groundwork both later phases depend on, and neither can bolt on afterwards: i18n plumbing (resx + `IStringLocalizer`, culture bootstrap, per-user language, existing screens migrated off literals) and the shared Cards / List density switch (§13.8, §13.9) | medium |
| **2** | ✅ `Contract` + `PaymentPlanRevision` + `Payment` + `Opening`; migration; CRUD endpoints; `MarkdownCard`; list + detail pages, four dialogs, equipment section and the Home strip | large |
| **3** | ✅ Loans & leases: amortization, early-payoff preview, principal/interest split · ✅ the monthly cash-flow rollup pulled forward from Phase 4 · residual and a price-history sparkline still open (§14) | medium |
| **4** | ✅ Payment materialization background service · ✅ push notifications (contract renewal, cancellation window, payment due) · ✅ iCal feed gains contract/payment events · ✅ timeline integration · ~~Home rollups (`/api/finance/*`)~~ done in Phase 3 | medium |
| **5** | ✅ `/api/attention` · ✅ foreground icon badge (app open) · background badge-on-push and the tag-replaced summary notification not built — needs a live device, see §15 | small–medium |
| **6** | ✅ Outbox operation types + server dispatch, revision-ordering conflict rule (free — the domain already throws) · client dialogs still call the API directly, not the outbox — see §15 | small–medium |
| **7** | **`/impeccable critique`** → fix → **`polish`**, then **`delight`** on icon + attention strip — blocked on a viewable app, see §15 | small |
| **8** | **`/impeccable harden`** before the rest of the family gets the link — blocked on a viewable app, see §15 | small |

Design passes are phases, not afterthoughts: `document` before anything is drawn,
`shape` before the new screens exist, and the refine passes only once the screens hold
real data. Phase 2 alone already delivers the brief's core — insurance with documents,
a summary card, a payment split, subscriptions, and correct summaries for contracts
that were already running.

---

## 13. Decisions taken — 2026-08-10

1. **Warranty stays separate from Contract.** No migration, no UI rewrite. `Contract`
   uses identical field names (`Provider`, `ContractNumber`, period, notification rules,
   attachments) so a later merge is a pure data migration if it ever earns its keep.
2. **Currency is per contract, and never converted.** ISO-4217 string on the contract,
   one household default in Settings. Foreign-currency contracts display in their own
   currency and are excluded from rollup totals with a footnote. No FX rates, ever.
3. **Insurance renewal creates a new contract** with `PreviousContractId` — each year
   has its own PDF, price and summary. Subscriptions extend in place via revisions.
4. **Markdown is rendered client-side** with Markdig + sanitisation before
   `MarkupString`, via a shared `MarkdownCard`. It immediately upgrades `Notes` on
   Equipment, Warranty and ServiceRecord, which are declared markdown today and
   rendered as plain text.
5. **One "Договоры" page with kind filter chips**, not three nav entries. The monthly
   total only makes sense when insurance, subscriptions and loans are counted together.
6. **No native Android app and no home-screen widgets.** Attention surfaces through the
   icon badge / notification dot (§10) and the calendar feed. Revisit only if the badge
   turns out to be too coarse in daily use.

7. **Visual system recorded in `DESIGN.md`** (see it for the normative version). Three
   decisions bind this feature's screens: entity colours move to the muted family
   (money = Muted Plum `#8A5F72`); a **density switch — Cards / List — appears on every
   list surface**, defaults to List on a phone and Cards on a desktop, and is
   remembered per screen and per device; the
   card lift state is pointer-only. The contracts list and detail pages are built to
   that switch from the start, not retrofitted — on desktop, Cards mode is the
   rail-plus-detail workspace, List mode the wide table with totals.

8. **Bilingual from day one — RU and EN — everywhere except what the user typed.**
   Not a later "i18n pass": every string added from now on goes through resources, and
   a screen that ships with a hard-coded literal is unfinished. The dividing line is
   authorship — the app's own words are translated, the household's words are not.

   | Translated | Never translated |
   |---|---|
   | labels, buttons, headings, empty states, validation and error text | contract `Name`, `Provider`, `ContractNumber` |
   | enum display names (`ContractKind`, `PaymentStatus`, `RevisionReason`) | `SummaryMarkdown`, `Notes`, attachment file names |
   | notification titles and bodies | tag names, equipment names, meter units as entered |
   | iCal `SUMMARY` / `DESCRIPTION` prefixes | currency codes (ISO, language-neutral) |

   Three consequences that have to be designed in, not retrofitted:

   - **The server needs to know the language too.** Push notifications and the iCal
     feed are generated with no browser and no `Accept-Language` — so the chosen
     language is stored per user, not only in `localStorage`, and the notification and
     calendar builders take it as a parameter.
   - **Formatting follows the culture, money does not follow the locale.** Dates,
     numbers and meter readings format per culture; an amount always renders in the
     contract's own currency (decision 2), never converted, never re-symbolised.
   - **Layout is sized for the longer language.** Russian runs 20–30% longer than
     English; column headers, chips and buttons are built to the Russian string and
     must not depend on a fixed width.

9. **The density switch is a system component, not a contracts feature.** Cards / List
   lives in `Shared/`, is used by Equipment, Warranties, Service, Contracts and the
   payment schedule alike, and is built **before** the contracts screens so those are
   its second consumer rather than its origin. Default List on a phone, Cards on a
   desktop; the choice is remembered per surface and per device.

Still genuinely open, decide when the code gets there:

- Whether `PreviousContractId` needs a UI (a "renewal chain" view) or stays a pure link.
- `AttentionHorizonDays` default — 7 is a guess; adjust after a month of real use.

---

## 14. Decisions taken — 2026-08-15 (Phase 3)

1. **The "stay silent without a rate" stance from §1's original Phase 3 note is
   replaced by a fallback ladder.** Interest math is genuinely optional data, and a
   loan with none of it recorded yet is the common case for a contract entered from an
   old paper file, not the exception. So every loan/lease figure now reports one of
   three states rather than one:

   | `LoanEstimateGap` | What is missing | What still shows |
   |---|---|---|
   | `None` | nothing | exact term, balance, and interest figures |
   | `MissingRate` | `AnnualInterestRate` | exact term and balance (simple/interest-free math); no interest-saved figure |
   | `MissingBalance` | `RemainingPrincipal` | nothing new — inventing a balance is exactly the case §1 warned against |

   The UI never blocks on a gap — it shows what it can and names what is missing,
   with the field to fill sitting in the same dialog rather than behind a link to
   somewhere else. This is a widening of the original guardrail, not a reversal of it:
   the app still never invents a number it was not given.

2. **Amortization math lives in `AmortizationMath`** (`HomeGuard.Application/Services`),
   a pure static class: the standard fixed-rate annuity formulas (instalment,
   balance-after-k, principal/interest split, term-for-a-balance), each falling back to
   straight-line arithmetic at a 0% rate. `ContractService.BalanceBeforeInstallment` /
   `SplitInstallmentAt` adapt it to a `PaymentPlanRevision`, walking forward from
   `RemainingPrincipal` at that revision's `EffectiveFrom` — never from `Opening`,
   which is a separate, coarser backfill number and stays exactly as inert as it was
   in Phase 2 (`ContractSummary.RemainingBalance`'s existing precedence is untouched).
   `ContractService.ConfirmPaymentAsync` now calls `SetLoanSplit` on a loan/lease
   instalment when its revision carries a rate; `BuildSchedule` shows the same split as
   an estimate on projected and not-yet-paid rows, and the real number once paid —
   `PaymentSchedule.razor` renders it as a small caption under the amount, never
   competing with it.

3. **Early payoff is a preview, not a new commit endpoint.**
   `ContractService.PreviewEarlyPayment` (pure, `POST /api/contracts/{id}/early-payment/preview`)
   answers "what would this lump sum change" — before/after term, instalment, payoff
   date, and interest saved (`null` under a gap, not zero — zero would claim there is
   provably nothing to save). `RevisionDialog`'s "Calculate" button folds the result
   straight into the existing instalment/count fields, so the plain "было → станет"
   table built in Phase 2 shows the consequence without a second diff view. Committing
   still goes through the existing `POST /revisions`; the lump sum itself is recorded
   as a `Payment(Kind=Extra)` via the existing payment endpoints, composed client-side
   rather than through a new atomic server call — §4.1's "sugar" endpoint was judged
   not worth a second code path for what two existing calls already do correctly.
   Not built this pass: `ResidualAmount`/`ResidualDueDate` have no dialog fields yet
   (domain and API already carry them, from Phase 2), and the price-history sparkline
   from §4.2 is still just revisions in a list, not a chart.

4. **The monthly cash-flow rollup was pulled forward from Phase 4**, on the household's
   own request: not just "how much per month" (Phase 2 already answers that from the
   *current* instalment) but "which future month do several obligations land on at
   once." `ContractService.BuildMonthlyLoad` (pure) merges every active contract's
   `BuildSchedule` output into month × currency buckets — cheap, because it is the same
   engine the detail page's schedule already uses, not new machinery. `GET
   /api/finance/monthly` and a `BudgetLoadChart` component (on the contracts list page)
   render it: one bar per month, `--hg-money` for the fill — the app's one established
   "this is about payments" colour — and the existing `--hg-today` ember for a month
   running ≥25% above the average, the same token overdue rows already use for
   "needs attention." Deliberately not a stacked-by-contract-kind chart: HomeGuard has
   no categorical palette for that yet (§11's warning about a design pass introducing
   colours that fight `mud-overrides.css` applies here too), so which contracts make up
   a month lives in a tap-to-expand breakdown instead of five new hues. A real
   categorical treatment, if it is ever worth it, is `/impeccable shape` work, not a
   byproduct of this pass.

5. **What §11 flagged in advance held up.** `/impeccable document` and `shape
   contracts` were both already run before Phase 2 (2026-08-10), so this pass extended
   an already-recorded look rather than inventing one. The next natural checkpoint is
   still `critique` → `polish` (§11's table) — better timed now than before, since
   Phase 3 is what finally produces the long schedules (a 360-row mortgage) and the
   real interest figures that `polish` needs real data to react to. The budget-load
   chart is new information architecture in the sense §11 means it — a short `shape`
   pass on it specifically, before it grows a second chart type or a categorical
   palette, is worth doing before it is not a one-screen change anymore.

---

## 15. Decisions taken — 2026-08-15 (Phases 4–6)

Most of what these phases described as new infrastructure already existed — built
generically for warranties and service records in earlier, unrelated work. The actual
job was plugging contracts and payments into it, not building it:

1. **Materialization, notifications, and the iCal feed all reused existing patterns
   line-for-line.** `PaymentMaterializationService` mirrors `RecurringRuleMaterializationService`
   (and gets idempotency for free from `BuildSchedule` already excluding covered dates —
   no separate "already pending" check needed, unlike the service it mirrors).
   `NotificationSchedulerService` gained `ScheduleContractNotificationsAsync` (the
   household's own `NotificationRules`, already on `Contract` since Phase 2) and
   `SchedulePaymentNotificationsAsync` (fixed 1w/1d/same-day offsets — nobody wants to
   configure reminders per instalment). `ICalFeedGenerator` gained contract and payment
   events the same way. All three ride the pre-existing `ScheduledJob` /
   `JobRunnerService` / `WebPushNotificationSender` pipeline; none of it is new.
2. **Timeline: payments are marks on the contract's bar, not a second row.** The literal
   reading of "payments as markers (like services)" would have meant a full second row
   per contract using the anchor/interval-bar machinery — but Paid payments becoming
   "anchors" would fragment the contract's one continuous bar into spurious sub-bars
   between consecutive payment dates. Reusing the *existing* `TimelineMark` tick
   mechanism (already built for standalone meter readings) instead needed zero new JS
   rendering code, keeps one row per contract, and is arguably the more literal reading
   of "no new timeline concepts needed." Two small, adjacent bugs fixed while in this
   code: the timeline's `WarrantyColor`/`ServiceColor` constants had drifted from
   `DESIGN.md` (a stale purple pre-dating the current palette) — now match
   `--hg-warranty`/`--hg-service`; and the detail card's cost line hard-coded `€` — now
   takes the event's own currency, defaulting to `€` so existing warranty/service cards
   are unaffected.
3. **`GET /api/attention` is genuinely new** — no aggregate endpoint existed to extend.
   It merges `WarrantyService.GetExpiringAsync`, `ServiceRecordService.GetOverdueAsync`/
   `GetDueSoonAsync`, and `ContractService.GetUpcomingAsync`/`GetExpiringAsync`
   (cancellation-window items only) into one `{count, urgent, soon, items}` shape. Only
   the **foreground** half of §10.3 is built: `MainLayout.razor` calls it once per app
   open and sets the icon badge (`navigator.setAppBadge`, feature-detected, a no-op
   where unsupported) plus caches it via the IndexedDB `cache` store that already
   existed (`HomeGuardDb.CacheSetAsync` — nothing new needed there). The **background**
   half — a push arriving while the app is closed re-badging the icon, and the
   tag-replaced summary notification — is not built. Piggybacking it onto the existing
   per-reminder push (having the service worker re-fetch `/api/attention` on any push
   delivery) would have been a plausible shortcut, but it conflates two different
   concerns for a mechanism nobody can watch fire on a real device from here — better
   built deliberately in the pass that can.
4. **Offline outbox: server-side dispatch only, not client wiring.** `OutboxSyncService`
   (client) and `SyncProcessorService` (server) already existed — but adopted by
   *nothing*: every existing entity dialog (Equipment, Warranty, ServiceRecord,
   MeterReading) still calls its `*ApiClient` directly, and the server dispatcher itself
   had gaps predating this work (`DeleteWarranty` and all `ServiceRecord` operation
   types are declared but unhandled — not touched here, not this feature's regression to
   fix). `SyncOperationTypes` gained `CreateContract`/`UpdateContract`/`DeleteContract`/
   `AddPlanRevision`/`CreatePayment`/`ConfirmPayment`/`SetOpeningPosition`, each handled
   in `SyncProcessorService.DispatchAsync` the same way the existing ones are — bringing
   contracts to full declared-and-handled parity, ahead of where the entities it mirrors
   currently sit. The non-commutative `AddPlanRevision` ordering rule from §9 needed no
   new hook: `Contract.AddRevision` already throws when `EffectiveFrom` precedes the
   active revision's, and that propagates through the dispatcher's existing generic
   `catch` into a `Rejected` ack for free. What's *not* done: no Contract dialog
   actually calls `OutboxSyncService.EnqueueAsync` yet. Doing that for real needs
   client-generated entity IDs (so a create can navigate immediately without waiting for
   a flush) — a change that would be the first of its kind in this codebase, worth doing
   deliberately with a real precedent to follow, not as a side effect of this pass.
5. **Phases 7–8 were not started.** `/impeccable critique`, `polish`, `delight` and
   `harden` all work by looking at the rendered app — screenshots, real data, real
   devices for the PWA-specific pieces. None of that is available mid-session here;
   running them now would mean guessing at what they'd find, which is worse than
   waiting.

### Live-verification checklist (once there's a way to look)

- **Materialization** — confirm a Planned payment actually appears 14 days before its
  due date, and that a second run the next day does not duplicate it.
- **Notifications** — a payment reminder and a contract renewal/cancellation reminder
  both actually arrive as push notifications on a subscribed device.
- **iCal** — subscribe a real calendar app to `/api/calendar/feed.ics`; contract and
  payment events show up, with the description text and the right all-day date.
- **Timeline** — contract bars render in the corrected `--hg-money` plum; hovering a
  payment mark shows the right amount, currency, and status word (paid/planned/
  projected); clicking a contract's start/expiry button opens the "Open contract" action
  and lands on the right page; a household-level (no-equipment) contract's row still
  looks right with no equipment name.
- **Badge** — `navigator.setAppBadge` actually changes the icon on each platform's own
  terms (§10.1): a number on iOS 16.4+/Windows/macOS Chrome, a dot on Android, nothing
  visible on Linux/Firefox (expected, not a bug).
- **Offline outbox** — not wired to any dialog yet; nothing to verify here until it is.
