---
version: 1
slug: "contracts"
primary_target: "contracts"
related_targets: []
---

# Surface brief: Contracts & Payments

Confirmed 2026-08-10 from `/impeccable shape`. Visual authority is `DESIGN.md` —
the world is established and this brief does not create one. Product truth is
`PRODUCT.md`; the domain design is `contracts-spec.md`.

## 1. Job and audience

Mode: **Operate.**

The record keeper arrives by four routes, all real (`PRODUCT.md` → Operating Context):
from the icon badge asking "what is burning", to the desk for a long weekly session,
to the car or garage with a phone in one hand, and straight after a purchase or
renewal with the document still in hand.

A second adult reads the same screens without knowing the keeper's conventions.
Anything legible only to the person who typed it is a defect, not a shortcut.

## 2. Outcome and proof

Five tasks the surface exists for:

1. **"What do I owe and when"** — answered in ten seconds, without opening a contract.
2. **Confirm a payment** — two taps from the notification.
3. **Enter a contract that has been running for five years** — one dialog
   (opening position), never 47 back-filled rows.
4. **Pay off early** — with the consequence visible *before* confirming.
5. **Find the clause at the garage** — summary card and policy PDF, offline included.

Success: scenes 2 and 3 answered in under ten seconds; scenes 1 and 4 let a whole
contract be entered in one sitting without leaving the screen.

## 3. Selected direction

- **Visual authority:** `DESIGN.md`, unchanged. No new visual world, no second language.
- **Structural thesis:** one object (`Contract`) rendered at two densities. The
  **payment schedule is the spine** of the detail screen, not an appendix to it.
- **Sequence on the detail screen:** who this is → what is next → why
  (the markdown summary) → history and schedule → actions.
- **Focal moment:** the "было → станет" preview in the early-payoff dialog. It is the
  only place where the app shows its working, and it should be the best screen in the
  product.
- **Implementation consequence:** the schedule component is shared by the detail pane,
  the equipment section, and the Home attention strip — build it once, as a component
  that takes a range and a density.

## 4. Scope and boundaries

**In scope:** contracts list (2 densities × 2 breakpoints); contract detail (rail plus
pane on desktop, stacked on phone); four dialogs — new contract, opening position,
confirm payment, revision / early payoff; the contracts section inside
`EquipmentDetail`; the attention strip on Home.

**Untouched:** timeline component internals, warranty screens, auth and device
management, the equipment form.

**Anti-goals:** a double-entry ledger; currency conversion; charts that answer no
question; a third density; a separate visual treatment for insurance versus loans.

## 5. States and ranges

| Axis | Minimum | Typical | Maximum |
|---|---|---|---|
| Contracts | 0 (first run) | 6–10 | ~30 |
| Schedule rows | 1 | 12–48 | 360 (far tail collapses behind "show all") |
| Instalments | 1 per year | 12 per year | 360 consecutive |
| Amount | 0,99 € | 17–320 € | 300 000 € |
| Provider name | 2 chars | ~20 | ~60 |
| Summary markdown | empty | ~400 chars | ~2000 chars |

Material states: predicted / planned / paid / overdue / skipped; contract active,
suspended, cancelled, ended; opening position present or absent; foreign currency
(shown in its own currency, excluded from totals with a footnote); a row queued in the
offline outbox; interest known versus unknown; Russian strings running 20–30% longer
than English.

## 6. Interaction and layout

- **Density switch** in the surface header: List by default on a phone, Cards on a
  desktop; remembered per screen and per device.
- **List row:** name left, amount right in tabular figures, due date last, status as a
  7px dot plus a chip. No coloured edge bars.
- **Detail:** field grid of four wells on a desktop pane, two on a phone; summary card
  above the schedule; schedule shows the three lifecycle states as distinct visual
  weights, with the far future collapsed.
- **Early payoff dialog:** the diff renders before confirmation, with the two effects
  (shorten term / lower payment) as a single choice.
- **Receipts:** captured with the existing `DocumentCapture` component, no new pattern.
- Extra desktop width buys a second object, never wider rows (The Second Object Rule).

## 7. Constraints and open decisions

**Binding:**
- MudBlazor components over `mud-overrides.css`; no hand-rolled equivalents.
- **All user-facing strings go through resources from day one** — the app is bilingual
  RU/EN with a switcher, and layouts must survive Russian at +20–30%.
- Tabular figures wherever numbers are compared down a column.
- Card lift state only inside `@media (hover:hover) and (pointer:fine)`.
- Money is Muted Plum `#8A5F72`; no new entity hue without a 3:1 check against Paper.

**A builder must not invent:** interest figures where no rate was entered; a converted
amount; a total that mixes currencies; a prediction presented without its evidence.

**Open:** whether renewal chains between successive policies need their own view;
how many days ahead "needs attention" reaches (`AttentionHorizonDays`, 7 is a guess).
