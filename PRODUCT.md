# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

**Primary — the record keeper.** One adult who owns the archive: enters equipment,
photographs documents, confirms services and payments, and is the one who notices
when something is overdue. Uses both a phone and a desktop.

**Secondary — the other adults in the household.** They have their own passkey
accounts and read the same records: what the policy covers, when the car is due,
how much is left on the lease. They did not create the data and do not know its
conventions, so anything obvious only to the record keeper is a defect for them.

No child accounts today. Roles beyond "owner" are not modelled; every adult sees
everything, including contract amounts and outstanding balances.

## Product Purpose

HomeGuard keeps a household's proof of ownership in one place: appliances, vehicles,
warranties, service history, meter readings, and — next — insurance policies,
subscriptions, leases and loans. It answers three questions: *what do we own*,
*what needs attention now*, and *what does keeping it cost*.

Success is that nobody in the house has to search a drawer, an email archive, or a
bank statement to answer those questions, and that nothing expires unnoticed.

## Positioning

Three things a neighbouring app cannot truthfully copy:

- **It runs on the family's own hardware.** Self-hosted on a home Linux server via
  Podman, passkey-only, no vendor account, no subscription, no third party holding
  the household's documents.
- **It predicts from what actually happened.** Next-service dates come from the
  average interval of completed records for that specific item, not from a
  manufacturer's schedule — with the same machinery about to serve payment plans.
- **It is one archive, not several apps.** Warranty, service, meter, and money for
  the same object live on one timeline and roll up to one cost of ownership.

## Operating Context

The app is opened in four distinct scenes, and all four are real:

1. **At the desk, weekly to monthly.** A long session: sorting bills, entering
   receipts, looking at what is ahead. Desktop, keyboard, lots of input.
2. **On the move, at the thing itself.** Standing at the car or the meter, or in a
   shop: enter a reading, check whether the warranty still holds, find a policy
   number for the garage. One hand, thirty seconds, possibly no signal.
3. **Driven by a notification.** A push or an icon badge brought the user here, and
   the only question is *what exactly is burning*.
4. **On a purchase or an event.** Bought an appliance, came back from a service,
   renewed a policy — enter the record and photograph the document while it is in hand.

Documents arrive as paper or PDF and are captured with the phone camera. Dates flow
outward to the family's calendar through an iCal feed. Blob storage on NextCloud and
Web Push are the planned edges of the system.

## Capabilities and Constraints

**Built:** equipment CRUD, warranties, service records, recurring maintenance rules
with runtime prediction, standalone meter readings, a custom pan/zoom timeline, an
iCal feed, FIDO2 passkey auth with device management, Podman deployment.

**Designed, not built** (`contracts-spec.md`): contracts covering insurance,
subscriptions, leases and loans; versioned payment plans with corrections and early
payoff; an opening position for contracts that were already running; an attention
number surfaced as an icon badge.

**Binding constraints:**
- Offline-first is non-negotiable: IndexedDB outbox, idempotent operations. Scene 2
  regularly has no signal.
- SQLite allows one writer at a time; writes are serialised behind a semaphore.
- Money is never converted between currencies — no FX rates, ever.
- iOS requires "Add to Home Screen" for push and badges; Chrome on Android has no
  Badging API and shows a notification dot instead.
- The UI is Blazor WebAssembly with MudBlazor; new screens use those components
  rather than hand-rolled equivalents.

**Undecided:** whether renewal chains between successive insurance policies need
their own view; how many days ahead "needs attention" should reach.

## Brand Commitments

- The name is **HomeGuard**.
- **Family-friendly, not corporate** — a standing instruction in `CLAUDE.md`.
- **Plus Jakarta Sans**, self-hosted, is the confirmed typeface.

## Evidence on Hand

Real household data only: the family's own vehicles, appliances and meters, their
actual policies and receipts. There are no customers, no testimonials, no pricing,
no benchmarks, and no deployment claims — future work must not invent any.

Repository records: `contracts-spec.md` (contracts and payments design),
`timeline-spec.md` (timeline component), `DESIGN.md` (visual system),
`design-directions.html` (the comparison sheet the visual decisions came from).

## Product Principles

1. **The record must outlive the moment it was written.** A second adult reading it a
   year later has to understand it without asking the person who typed it.
2. **Capture beats completeness.** Scene 2 and scene 4 are one-handed and interrupted;
   a partial record entered now is worth more than a perfect one entered never.
3. **Predict from history, and show the working.** Where the app guesses a date or an
   amount, the evidence behind the guess stays visible and correctable.
4. **The house owns its data.** No feature may require a third-party account or move
   documents off the family's own server.
5. **Attention is scarce.** The app may interrupt only for what is actually due; every
   badge, push and highlight competes with the family's real life.

## Accessibility & Inclusion

- **Bilingual: Russian and English, with a switcher in settings.** All user-facing
  strings live in resources from now on, and layouts must survive Russian running
  20–30% longer than English.
- Scene 2 is outdoors and one-handed: touch targets and contrast must hold up in
  daylight, at arm's length, in motion.
- Amounts, dates and meter readings are compared down columns and use tabular figures.
