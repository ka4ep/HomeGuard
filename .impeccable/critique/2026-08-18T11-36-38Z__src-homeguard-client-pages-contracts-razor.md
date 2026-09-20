---
target: contracts surface (Contracts.razor, ContractDetail.razor, ContractSection.razor, ContractDialog.razor, Home.razor attention strip)
total_score: 26
max_score: 40
na_heuristics: 
p0_count: 2
p1_count: 2
timestamp: 2026-08-18T11-36-38Z
slug: src-homeguard-client-pages-contracts-razor
---
Method: dual-agent (A: design-review subagent · B: detector/evidence subagent)

## Design Health Score

| # | Heuristic | Score | Key Issue |
|---|-----------|-------|-----------|
| 1 | Visibility of System Status | 2 | Confirming a payment gets a snackbar; committing an early payoff — the brief's own declared "best screen in the product" — closes silently (`RevisionDialog.razor` never injects `ISnackbar`) |
| 2 | Match System / Real World | 4 | Household-register language throughout; no finance-app jargon |
| 3 | User Control and Freedom | 2 | `PaymentDialog.razor` deletes a payment record with zero confirmation — no undo, no "are you sure," on money data |
| 4 | Consistency and Standards | 1 | Status is dot-only in list/cards, an unstyled bordered chip with no dot in the detail header, and nothing at all in the equipment section — the brief's own "dot plus chip" rule (§6) appears nowhere combined |
| 5 | Error Prevention | 3 | Disabled-save guards and server-side overlap/too-early checks surface in plain language |
| 6 | Recognition Rather Than Recall | 3 | Captions-above-values throughout compensate for the missing field-well containers |
| 7 | Flexibility and Efficiency | 2 | No batch-confirm for multiple overdue rows; two selects (`RevisionReason`, `ContractKind`) exceed the brief's own ≤4-options guidance |
| 8 | Aesthetic and Minimalist Design | 2 | `BudgetLoadChart` is out of the brief's scoped surface list and risks its own named anti-goal, "charts that answer no question" |
| 9 | Error Recovery | 4 | Error strings name the actual constraint (overlap, too-early, missing rate) instead of a generic failure |
| 10 | Help and Documentation | 3 | Contextual helper text substitutes reasonably for a help system |
| **Total** | | **26/40** | **Acceptable** |

Both heuristics 7 and 10 were scored, not marked n/a — this is an Operate-mode surface (task completion, not persuasion), so both genuinely apply.

## Design Specificity Verdict

**LLM assessment:** Mixed, and the split is informative. The *content* decisions are unmistakably bespoke to this product: the opening-position dialog's framing ("record where it stood on one date"), the before/after diff on early payoff, the two-tap payment confirm paired with a soft-undo reopen switch, and the refusal to show a total when a rate or balance is unknown rather than guessing — none of that reads as a generic CRUD scaffold. But large parts of the *execution* regress to MudBlazor stock behavior: the density switch has no backing CSS anywhere in the project despite being DESIGN.md's most emphasized signature pattern, the field-well pattern (also DESIGN.md's most emphasized pattern) is not implemented once on this surface even in the one place structurally built for it, and there is no confirmed evidence Slate is wired to filled buttons, chips, or switches at all. A generic MudBlazor app and this app would currently render the Add button, the filter chips, and the density switch identically.

**Deterministic scan:** Clean — 0 findings, exit code 0, across all 5 in-scope files (`Contracts.razor`, `ContractDetail.razor`, `ContractSection.razor`, `ContractDialog.razor`, `Home.razor`). This does not contradict the LLM assessment above; the two operate at different scopes. The detector's ruleset catches mechanical red flags (magic values, forbidden literal patterns); it has no way to notice that `mud-overrides.css` never defines `.mud-button-filled-primary` or that a field grid renders as bare `Typo.caption` stacks instead of the token-defined well container. This is a case of the LLM review catching something the detector's ruleset doesn't cover, not a case of either assessment being wrong.

**Visual overlays:** Not available this run — no browser automation tool is exposed in this session, so neither assessment could load the live app. Both reviews are code-only; treat conclusions about actual rendered spacing, real contrast ratios, and live responsive behavior as lower-confidence inference from token values and markup, not confirmed visual fact.

## Overall Impression

The feature's hard problems — the ones that needed real design thinking about money, trust, and a shared household record — are solved well: the opening-position flow, the payoff diff, the safe two-tap confirm. The feature's easy problems — the ones DESIGN.md already answered in exact CSS terms — are unevenly finished: the density switch and field-well pattern the whole system is built around are absent from the surface that was supposed to prove them out, and status, the single fact every list row exists to communicate, is shown three different ways in three different places. The single biggest opportunity is closing that gap between the bespoke interaction design and the token system that was supposed to carry it — most of what's flagged below is "finish wiring what DESIGN.md already specified," not "invent something new."

## What's Working

- **The early-payoff diff is the real thing the brief asked for.** `RevisionDialog.razor` computes before/after for installment, interval, remaining count, last-due date, and total, and it deliberately withholds the total when either input factor is unknown rather than fabricating a number — a precise, principled read of PRODUCT.md's "predict from history, and show the working."
- **Confirming a payment is genuinely safe.** The two-tap confirm with no intermediate dialog is paired with an explicit reopen path that says the totals return to what they were — this is exactly the low-friction-but-reversible pattern the brief's task 2 called for.
- **Progressive disclosure inside the contract dialog is well judged.** Insurance-only fields appear only for Insurance contracts, the plan block only appears once a plan exists, the early-payment assist panel only appears once early payment is chosen — each reveal is gated on something the user just told the form, not shown speculatively.

## Priority Issues

**[P0] The policy-PDF half of task 5 has no UI.** Brief task 5 is "find the clause at the garage — summary card and policy PDF, offline included." The summary-markdown half exists; there is no `DocumentCapture` usage, attachment list, or PDF viewer anywhere in `Contracts.razor`, `ContractDetail.razor`, `ContractSection.razor`, or `ContractDialog.razor`, even though `DocumentCapture` already exists as a reusable component elsewhere in the app.
**Why it matters:** this is one of five tasks the surface brief says the whole feature exists for, and at the garage — the scene it's named for — a user standing next to the car cannot open the policy PDF from this screen at all.
**Fix:** embed `<DocumentCapture>` (or a read-only viewer variant of it) in `ContractDetail.razor`'s "why" section, next to the summary card.
**Suggested command:** `/impeccable harden` (this is a missing production capability against the brief, not a polish pass).

**[P0] The Second Object Rule — the brief's own named structural thesis for this screen — isn't built.** Brief §4 scopes "contract detail (rail plus pane on desktop, stacked on phone)"; DESIGN.md's Second Object Rule requires a rail beside the open item at ≥900px. `ContractDetail.razor` is currently a single full-width page reached by full navigation from the list, with no sibling rail.
**Why it matters:** on desktop — the "long weekly session" scene the app is explicitly designed around — opening a contract loses the list context every time, which is the exact cost-of-opening problem the Second Object Rule exists to eliminate.
**Fix:** build the two-pane rail-plus-pane workspace the brief scoped, or if it's genuinely out of this pass, descope it explicitly in `contracts.md` rather than leaving the brief overstating what shipped.
**Suggested command:** `/impeccable layout`.

**[P1] No confirmed evidence Slate reaches filled buttons, chips, or switches.** `HomeGuardTheme.cs` sets no `Palette`, and `mud-overrides.css` overrides `.mud-button-outlined-primary` but not `.mud-button-filled-primary`, chip fills, or `.mud-switch`/`.mud-toggle`. That leaves the Add button, the selected filter chips, the plan switch, and every filled-primary save button with no verified path to `#4f6fa0`.
**Why it matters:** this is DESIGN.md's One Ink Rule — "if something is slate-blue, it can be pressed; if it can be pressed, it is slate-blue" — and the primary action across this entire surface may currently be rendering MudBlazor's stock palette instead of the app's one actionable color. (Lower confidence on the exact rendered hex without a browser, but the absence of the CSS wiring is a verified fact, not a guess.)
**Fix:** either set `Palette` in `HomeGuardTheme.cs` for these variants or add the missing selectors to `mud-overrides.css`, then confirm once in a browser.
**Suggested command:** `/impeccable audit` (verify), then `/impeccable colorize` if gaps are confirmed.

**[P1] Status is never shown as "dot plus chip," and the three places it appears disagree with each other.** The brief (§6) specifies status as a 7px dot plus a chip. List rows and cards show a dot with no chip; the detail header shows an unstyled outlined chip (itself against DESIGN.md's "a chip never carries a border") with no dot; the equipment section shows neither.
**Why it matters:** status is the one fact a list row exists to communicate, and right now a reader who can't rely on color alone — including the "other adult" persona PRODUCT.md names as a first-class user — gets no reliable status signal from any of the three views.
**Fix:** standardize on dot + semantic-wash chip (success/warning/danger per DESIGN.md) in all three locations.
**Suggested command:** `/impeccable layout`.

**[P2] The field-well pattern — DESIGN.md's single most emphasized rule — is unbuilt at the one place structurally designed for it.** The "what's next" grid in `ContractDetail.razor` is exactly the four-cell field grid DESIGN.md describes wells for, but renders as bare caption/value text stacks with no Paper Top background or 6px radius container.
**Why it matters:** this is the system's own stated signature pattern ("never a bare value in a paragraph"), unmet at the screen built to showcase it.
**Fix:** wrap each field-grid cell in the field-well container already defined in DESIGN.md's component tokens.
**Suggested command:** `/impeccable typeset`.

## Persona Red Flags

**Sam (Accessibility-Dependent).** Confirming a payment by keyboard: the confirm icon in `PaymentSchedule.razor` has a tooltip, but the adjacent Edit button doesn't — inconsistent accessible naming on two controls sitting side by side. Opening a contract from Cards view, from the equipment section, or from Home's upcoming list all rely on `@onclick` attached to a `MudCard`/`MudPaper`/`MudStack` with no `tabindex`, `role="button"`, or keydown handler — a keyboard-only user can reach a contract only through List/table mode, not Cards. And because status is color-only with no chip (see Priority Issues above), Sam gets no status information from the list at all.

**Riley (Stress-Tester).** A 360-row schedule defaults to a 12-row collapse, but "Show all" then dumps all 360 rows into one ungrouped table with no pagination or virtualization — a likely real scroll/perf problem at the brief's own stated maximum. Multi-currency contracts correctly refuse to blend totals, but the resx string written for exactly this moment (`Contract_OtherCurrencies`, "amounts are never converted") is never referenced by any `.razor` file — Riley just sees two silent, unexplained total lines and has to infer the policy, which reads as a bug rather than a deliberate rule.

**The other adult (PRODUCT.md's secondary user — reads records they didn't create).** A suspended contract with no `SummaryMarkdown` written has no structured "why suspended" field; that context lives only in optional free-text Notes, so PRODUCT.md's own Principle 1 ("understand it without asking the person who typed it") goes unmet whenever the record-keeper skipped the summary. Separately, the plan-history card only appears once a contract has more than one revision — a contract with exactly one plan shows no history card at all, so this reader can't tell "there's only ever been one plan" apart from "history didn't load."

## Minor Observations

- `ContractStatus.Ended` falls into the same generic grey branch as an undefined/unmatched status — it has no dot color of its own.
- Currency-symbol and interval-label formatting are copy-pasted across multiple files (six and three respectively) rather than a shared helper; adding a currency means editing every switch statement correctly.
- The Kind field is disabled on edit with no tooltip explaining why it's locked.
- `BudgetLoadChart` reuses the Ember token for "peak month" — DESIGN.md's Ember Reserve restricts that color to the "today" line only; a spending peak isn't "today."
- Home's "money coming up" strip hand-rolls the same row shape `PaymentSchedule` already owns, rather than reusing the shared component the brief explicitly asked to be built once and shared.

## Questions to Consider

- The brief calls for building the schedule component once and sharing it across detail, equipment, and Home — why does Home reimplement its own row markup instead of reusing it?
- `DocumentCapture` already exists and looks trivially embeddable — was the policy-PDF half of task 5 cut on purpose, or just missed? Either way, how does a user satisfy task 5 in the app today?
- If `mud-overrides.css` is meant to be the single source of truth for color, why does it cover outlined-primary buttons but not filled-primary, chips, or switches — was this ever checked in a browser, or only assumed to cascade?
