---
name: HomeGuard
description: A household's equipment, warranties, services and contracts, kept like a well-ordered paper archive.
colors:
  greige: "#BAB2AC"
  greige-deep: "#55514c"
  greige-raised: "#b0aca7"
  paper: "#eeeae4"
  paper-top: "#faf8f5"
  ink: "#1e1c1a"
  ink-muted: "#6b6560"
  ink-subtle: "#78716c"
  ink-on-greige: "#5E5B58"
  chrome-text: "#ffffff"
  slate: "#4f6fa0"
  slate-deep: "#3a5278"
  slate-on-greige: "#a8c4e8"
  slate-wash: "#dce6f2"
  warranty: "#6E6BA8"
  service: "#4E8A6B"
  meter: "#9C752E"
  money: "#8A5F72"
  today: "#D85A30"
  today-text: "#ab421f"
  success-wash: "#d8eee0"
  success-ink: "#2d6040"
  warning-wash: "#f5ede0"
  warning-ink: "#7a4f10"
  danger-wash: "#f5e8e8"
  danger-ink: "#7a2a2a"
  hairline: "rgba(60, 50, 40, 0.12)"
  hairline-strong: "rgba(60, 50, 40, 0.25)"
  hairline-on-greige: "rgba(255, 255, 255, 0.18)"
typography:
  display:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "27px"
    fontWeight: 600
    lineHeight: 1
    letterSpacing: "-0.03em"
  headline:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "1.22rem"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "-0.01em"
  title:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "17px"
    fontWeight: 600
    lineHeight: 1.25
    letterSpacing: "-0.015em"
  body:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "15px"
    fontWeight: 400
    lineHeight: 1.6
    letterSpacing: "normal"
  label:
    fontFamily: "Plus Jakarta Sans, system-ui, sans-serif"
    fontSize: "9.5px"
    fontWeight: 600
    lineHeight: 1.4
    letterSpacing: "0.06em"
rounded:
  sm: "6px"
  md: "10px"
  lg: "16px"
  pill: "999px"
spacing:
  hair: "2px"
  xs: "6px"
  sm: "8px"
  md: "12px"
  lg: "16px"
  xl: "24px"
components:
  button-primary:
    backgroundColor: "{colors.slate}"
    textColor: "#ffffff"
    rounded: "{rounded.sm}"
    padding: "8px 16px"
    typography: "{typography.label}"
  button-primary-hover:
    backgroundColor: "{colors.slate-deep}"
  button-ghost-on-card:
    backgroundColor: "transparent"
    textColor: "{colors.slate}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"
  button-ghost-on-greige:
    backgroundColor: "transparent"
    textColor: "{colors.ink-on-greige}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"
  card:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    padding: "14px"
  card-elevated:
    backgroundColor: "{colors.paper-top}"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    padding: "16px"
  field-well:
    backgroundColor: "{colors.paper-top}"
    textColor: "{colors.ink}"
    rounded: "{rounded.sm}"
    padding: "7px 9px"
  chip-neutral:
    backgroundColor: "{colors.slate-wash}"
    textColor: "{colors.slate-deep}"
    rounded: "{rounded.pill}"
    padding: "2px 9px"
  chip-success:
    backgroundColor: "{colors.success-wash}"
    textColor: "{colors.success-ink}"
    rounded: "{rounded.pill}"
    padding: "2px 9px"
  chip-warning:
    backgroundColor: "{colors.warning-wash}"
    textColor: "{colors.warning-ink}"
    rounded: "{rounded.pill}"
    padding: "2px 9px"
  chip-danger:
    backgroundColor: "{colors.danger-wash}"
    textColor: "{colors.danger-ink}"
    rounded: "{rounded.pill}"
    padding: "2px 9px"
  nav-link:
    backgroundColor: "transparent"
    textColor: "{colors.chrome-text}"
    rounded: "{rounded.sm}"
    padding: "8px 12px"
  nav-link-active:
    backgroundColor: "rgba(255, 255, 255, 0.15)"
    textColor: "{colors.chrome-text}"
  density-toggle-off:
    backgroundColor: "transparent"
    textColor: "rgba(255, 255, 255, 0.62)"
    rounded: "{rounded.sm}"
    padding: "5px 13px"
  density-toggle-on:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.sm}"
    padding: "5px 13px"
---

# Design System: HomeGuard

## Overview

**Creative North Star: "The Warm Archive"**

HomeGuard is the drawer where a household keeps proof of what it owns: the boiler's
warranty card, the car's service book, the insurance policy with the clause about
winter tyres. The interface is that drawer made legible. Cards are documents on a
clay-coloured table — they lift off the surface, they carry small uppercase catalogue
labels, and they hold facts rather than illustrations. The mood is calm, warm and
literate: a family archive kept by someone precise, not a finance dashboard.

Density is the system's signature. Values live in small tinted wells with a
9.5px uppercase label above them, so a screen can carry a dozen facts without
shouting. Warmth comes from the ground, not from decoration: a greige page, cream
paper, a single slate-blue ink for every action. Colour is scarce and always
carries meaning — an entity's identity or a status — never mood.

The interface answers to two scenes with one language. On a phone it is a list you
check while standing at the car; on a desktop it is a two-pane workspace where a
contract opens beside its neighbours. These are **densities of one world**, not two
looks: the same tokens, the same labels, the same ink — only how many objects fit
on screen changes, under a switch the user controls.

**Key Characteristics:**
- Warm greige ground with cream documents floating on it
- One slate-blue ink for every action, everywhere
- Small uppercase catalogue labels over tabular values
- Facts in tinted wells, never in free-floating text
- Exactly two elevation levels; the third is a modal
- Colour identifies a thing or a state, never decorates

## Colors

A warm greige ground under cream paper, with one cool ink for action and a muted
family of hues for identity — every accent sits at the same temperature as the room.

### Primary
- **Slate Ink** (#4f6fa0): every action. Buttons, links, active timeline dots, the
  focused control. It is the only colour a user is invited to press.
- **Slate Ink Deep** (#3a5278): the pressed and hovered state of Slate Ink, and the
  colour of accent text sitting on cream paper.
- **Slate on Greige** (#a8c4e8): the same voice raised to survive the greige ground —
  used for accents that sit outside a card, never inside one.
- **Slate Wash** (#dce6f2): the quiet chip background inside a card.

### Secondary — entity identity
Four hues that answer "what kind of thing is this", tuned to the ground's warmth so
none of them jumps out of the page. Each clears 3:1 against cream paper.

- **Muted Iris** (#6E6BA8): warranties and guarantees.
- **Muted Moss** (#4E8A6B): service and maintenance.
- **Muted Brass** (#9C752E): meter and odometer readings.
- **Muted Plum** (#8A5F72): money — contracts, insurance, subscriptions, payments.

### Tertiary
- **Ember** (#D85A30): time-urgency — the one thing that needs the eye first because
  of *when* it is, not *what* it is. Dots, bars and the timeline's "today" line —
  graphical marks, never letters. Never a stand-in for the semantic pairs below: a
  validation error or a status chip is never Ember, whatever its severity — that
  distinction is what keeps Ember legible as "this is about time" rather than
  "something is wrong."
- **Ember Text** (#ab421f): the same warm hue, darkened, for the rare case Ember
  labels an actual sentence — a cancellation-deadline note, a rate-unknown warning —
  rather than a dot or a bar. #D85A30 itself is a graphic-only value (3.2:1, the
  non-text threshold); letters need Ember Text's 5.0:1 against Paper instead.

### Neutral
- **Clay** (#BAB2AC): the page ground. Everything else floats on it.
- **Clay Deep** (#55514c): app bar and drawer — the chrome that frames the archive.
- **Paper** (#eeeae4): the standard card. The colour of a document.
- **Paper Top** (#faf8f5): fields, wells, table headers, dialogs — the layer above paper.
- **Ink** (#1e1c1a): body text on paper.
- **Ink Muted** (#6b6560): secondary text and table headings.
- **Ink Subtle** (#78716c): catalogue labels, axis ticks, timestamps — darkened from an
  earlier #9c9590 (2.5:1 on Paper) after an audit found the system's own signature
  label pattern failing contrast on itself.
- **Ink on Greige** (#5E5B58): text sitting directly on the light Clay ground (5.6:1).
  Never on Clay Deep — see Chrome Text below.
- **Chrome Text** (#ffffff): text on Clay Deep — the app bar, the drawer, every nav
  link. An earlier Clay Deep (#7a7672) gave white exactly 4.50:1 — technically AA, but
  zero headroom and it read as borderline live; Clay Deep darkened to #55514c gives
  white real margin (7.87:1). Ink on Greige on this surface measured 1.5–2.0:1 before
  an audit caught it. There is no muted variant of this token — Clay Deep has no room
  for one and stay compliant, so hierarchy here comes from background lift, not text
  weight.
- **Hairlines** (rgba(60,50,40,.12) / .25): card edges and dividers; on the clay
  ground the hairline flips to rgba(255,255,255,.18).

### Semantic
Pale wash plus dark ink, always as a pair, always as a pill: success #d8eee0/#2d6040,
warning #f5ede0/#7a4f10, danger #f5e8e8/#7a2a2a.

### Named Rules
**The One Ink Rule.** Slate is the only actionable colour. If something is
slate-blue, it can be pressed; if it can be pressed, it is slate-blue. A decorative
slate element is a bug.

**The Meaning-Only Rule.** Every non-neutral colour answers one of two questions:
*what kind of thing is this* (entity hues) or *what state is it in* (semantic pairs).
No colour exists to make a screen livelier.

**The Ember Reserve.** #D85A30 marks time-urgency, not severity: the "today" line,
an overdue payment, a cancellation deadline, a budget month spiking well above the
rest — the handful of places something demands attention because of when it falls,
not because it is wrong. It never substitutes for the semantic danger/warning pairs
— a form error or a status chip is never Ember, however severe.

## Typography

**Display / Body / Label Font:** Plus Jakarta Sans, self-hosted (400, 500, 600), with
`system-ui, sans-serif` as fallback. One family, three weights, no second face.

**Character:** humanist, slightly rounded, wide apertures — it stays legible at 9.5px
uppercase and still looks friendly at 27px. The whole hierarchy is built from three
weights and letter-spacing, never from a contrasting display face.

### Hierarchy
- **Display** (600, 27px, 1.0, -0.03em): the money figure — the one number a screen
  is about. Tabular numerals. At most one per screen.
- **Headline** (600, 1.22rem, 1.3, -0.01em): page titles.
- **Title** (600, 17px, 1.25, -0.015em): the name of the open object — a contract, a
  piece of equipment, a service record.
- **Body** (400, 15px, 1.6): all prose, including the markdown summary card. Measure
  capped at 65–75ch; on a desktop pane that means the text column stops before the pane does.
- **Label** (600, 9.5–10px, uppercase, 0.06em): catalogue labels above values, timeline
  group names, table headers (0.05em at 0.75rem). This is the system's most recognisable
  typographic gesture.
- **Button** (500, 0.8125rem, no text-transform): MudBlazor's ALL CAPS default is
  overridden on purpose.

### Named Rules
**The Label-Over-Value Rule.** A fact is a 9.5px uppercase label with a 13.5px/500
value beneath it, inside a tinted well. Not "Label: value" on one line, not a bare
number hoping to be understood.

**The Tabular Rule.** Every number that can be compared down a column —
money, meter readings, dates, counts — carries `font-variant-numeric: tabular-nums`.
Numbers that jitter between rows read as an unfinished product.

## Layout

The page is a greige ground; content is a stack of cream cards with 6–8px between
siblings and 12–16px of internal padding. Base rhythm is 2 / 6 / 8 / 12 / 16 / 24.

**Two densities, one switch.** Every list surface — contracts, equipment, service,
warranties — offers a segmented control with **Cards** and **List**, and remembers the
choice per screen and per device. The default follows the scene, not the screen's
name: **List on a phone**, where the question is "what needs me" and one column can
only answer it as a list; **Cards on a desktop**, where the rail and the detail pane
fit side by side and opening an object costs nothing.

- **Phone (< 600px):** single column, full-bleed cards, the switch pinned above the
  list. Cards mode = one roomy card per object (17px title, 27px figure, progress bar,
  one primary action). List mode = 38px rows, name left, amount right, due date last.
- **Desktop (≥ 900px):** Cards mode becomes a two-pane workspace — a 286px rail of
  objects beside an open detail pane carrying a four-column field grid, the summary
  card, and the full schedule without scrolling. List mode becomes a seven-column
  table with totals along the bottom edge, not in a separate card.
- Between those widths the rail collapses and the table drops its least-load-bearing
  columns (Item, Progress) before it ever scrolls horizontally.

Field grids are two columns on a phone, four on a desktop pane. Tables never scroll
the page sideways: they scroll inside their own container.

### Named Rules
**The Second Object Rule.** Extra width buys a second object, never wider rows.
A desktop screen shows the list *and* the open item; it does not stretch one card to
1400px.

## Elevation & Depth

Two levels, and the modal. A card lifts off the clay ground with a soft shadow and a
half-pixel hairline; inside a card, separation is tonal only — Paper Top on Paper,
never a nested shadow. Dialogs are the single exception and use a deeper, wider shadow.

### Shadow Vocabulary
- **Card** (`box-shadow: 0 2px 8px rgba(30,28,26,.10), 0 0 0 .5px rgba(60,50,40,.10)`):
  every card, panel and timeline grid at rest.
- **Modal** (`box-shadow: 0 8px 32px rgba(30,28,26,.18), 0 0 0 .5px rgba(60,50,40,.12)`):
  dialogs only.
- **Lift** (`box-shadow: 0 8px 22px rgba(30,28,26,.18)` + `translateY(-3px)`): the
  pointer-only response state.

### Named Rules
**The Two-Level Rule.** Ground → card → tonal field. Anything that needs a third
elevation is a dialog, and should be built as one.

**The Pointer-Only Lift Rule.** The lift state lives inside
`@media (hover:hover) and (pointer:fine)`. On touch, cards stay at rest — a hover
shadow on a phone is a state no finger can produce, and it costs scroll performance.

## Shapes

Corners are gently curved and consistent: 6px for the small parts inside a card
(wells, chips-that-aren't-pills, buttons, icon buttons), 10px for cards, panels,
tables and dialogs, 16px for the largest containers such as drop zones. Status
chips are full pills (999px). The timeline is the one place with tighter geometry:
2–4px on event marks, so a 13px dot still reads as a mark rather than a blob.

Borders are hairlines, never structure: 0.5px translucent brown on cream, 1px white
at 18% on the clay ground. Nothing in the system uses a thick or coloured border to
carry meaning — a colour bar wider than 1px on the edge of a card is not part of this
world; use a 7px status dot instead.

## Components

### Buttons
- **Shape:** softly curved (6px), never pill, never square.
- **Primary:** Slate Ink fill, white text, 8px/16px padding, 0.8125rem/500, sentence case.
- **Hover / Focus:** background shifts to Slate Ink Deep over 160ms; focus rings are
  drawn from Slate, never the browser default.
- **Ghost:** transparent with a hairline. Two variants that must not be swapped —
  on cream paper the border is the brown hairline and the label is Slate; on the clay
  ground the border is white-18% and the label is Ink on Greige.

### Chips
- **Style:** full pill, 2px/9px, 10.5px/600, wash background with matching dark ink.
- **Kinds:** entity chips use Slate Wash; state chips use the semantic pairs. A chip
  never carries a border.

### Cards / Containers
- **Corner Style:** 10px.
- **Background:** Paper (#eeeae4); dialogs and inner wells use Paper Top (#faf8f5).
- **Shadow Strategy:** the Card shadow at rest, the Lift response on pointer devices only.
- **Border:** 0.5px hairline, always paired with the shadow.
- **Internal Padding:** 14px on a phone, 15–16px on a desktop pane.

### Field wells
The system's signature: a Paper Top block at 6px radius with a 9.5px uppercase label
and a 13.5px/500 tabular value. Two per row on a phone, four in a desktop pane, 6–7px
between them. This is how every fact is displayed — never a bare value in a paragraph.

### Inputs / Fields
- **Style:** MudBlazor underline inputs, label 0.8125rem in Ink Muted, value in Ink.
- **Focus:** the underline takes Slate; the label does not change size.
- **Error:** danger ink on the label and underline, with the message directly beneath.

### Navigation
App bar and drawer are Clay Deep with a white-18% hairline instead of a shadow. Nav
link text is Chrome Text (#ffffff) at every state — Clay Deep leaves no contrast
headroom for a dimmer resting state, so which item is current is carried entirely by
a 6px-radius background lift: transparent at rest, white-10% on hover, white-15% when
active. The active row never uses Slate — inside the chrome, contrast comes from
lightness, not hue.

### Density switch
A segmented control of two icon-only options, **Cards** and **List**, in the surface's
header bar: a transparent track with a hairline border (0.5px, Hairline on Greige) at
34px total height — matched to the neighbouring "Add" button's own height so both sit
on the same line. The selected icon lifts off the track with a Paper fill and the Card
shadow; the resting icon sits in Ink on Greige, since the track carries no fill of its
own. An earlier filled-black track read as heavier than the plain header buttons around
it; transparent-with-hairline reads as one control instead of two loose icons. It
appears on every list surface, is remembered per screen and per device, and defaults to
List on a phone, Cards on a desktop.

### Timeline
The one custom-drawn surface. Rows are 50px on a Paper grid at 10px radius; the label
column is fixed and the track pans. Warranties are 13px bars, services are 13px
circles on a 2px interval line at 34% opacity, payments are 11px diamonds, meter
readings are 4×9px ticks along the baseline. Completed marks are filled with the
entity hue; predicted marks are hollow with a dashed edge in Ink Subtle. The Ember
"today" line crosses every row at 2px.

## Do's and Don'ts

### Do:
- **Do** state every fact as a label-over-value well: 9.5px uppercase label,
  13.5px/500 tabular value, Paper Top background, 6px radius.
- **Do** give numbers `font-variant-numeric: tabular-nums` wherever they can be
  compared down a column.
- **Do** spend extra width on a second object — a rail plus a detail pane — rather
  than on wider rows.
- **Do** keep the density switch on every list surface — List by default on a phone,
  Cards on a desktop — and remember the choice per screen and per device.
- **Do** gate the lift state behind `@media (hover:hover) and (pointer:fine)`.
- **Do** theme the browser's own surfaces from the palette: selection, caret,
  scrollbar (6px, Clay Raised thumb), and focus rings.
- **Do** check any new entity hue at 3:1 against Paper (#eeeae4) before adopting it —
  this is how #B08A3C became #9C752E.
- **Do** treat Clay Deep as its own contrast case. Text there needs Chrome Text
  (#ffffff) — Ink on Greige (#5E5B58) is tuned for the lighter Clay page and measures
  1.5–2.0:1 on Clay Deep. The two greys don't share a text colour, however similar
  their names look.
- **Do** give a clickable card or row a keyboard path — `tabindex="0"`, `role="button"`,
  Enter/Space — whenever the click target isn't already a native button or link.

### Don't:
- **Don't** use Slate for anything that cannot be pressed, and don't build an action
  in any other colour.
- **Don't** nest a shadow inside a card; separate inner regions with Paper Top instead.
- **Don't** put a coloured bar wider than 1px on the edge of a card or row — a 7px
  status dot carries the same information without the costume.
- **Don't** use Ember for a form error or a status chip, however severe — Ember marks
  *when* something needs attention, the semantic pairs mark *what's wrong*. Don't put
  Ember itself (#D85A30) on text either — it's a graphic-only value; use Ember Text
  (#ab421f) for an actual sentence.
- **Don't** let MudBlazor's ALL CAPS buttons back in, and don't reintroduce Roboto —
  `app.css` still carries a Blazor-template `font-family: 'Roboto'` on `html, body`
  and a Google Fonts link in `index.html`; both are leftovers, and Plus Jakarta Sans
  is the only face in this system.
- **Don't** introduce a second visual language for a roomier layout. Roominess is a
  density of this world — bigger type and more air on the same tokens, not 20px radii
  and sentence-case labels borrowed from somewhere else.
- **Don't** set colour in `HomeGuardTheme.cs`; `mud-overrides.css` is the single
  source of truth and its `!important` rules will win silently.
- **Don't** override `.mud-table-container`'s overflow. MudBlazor's own default is
  already `overflow-x:auto` — this system's "tables scroll inside their own
  container" rule, for free. A stray `overflow:hidden` there clips content instead
- **Don't** give a dark-chrome surface (AppBar, Drawer) a background override scoped
  only to its own class. MudBlazor renders these with `.mud-elevation-0` on the same
  element, and `mud-overrides.css`'s own `.mud-elevation-0 { background-color:
  var(--hg-card) ... }` — a later, equally-specific `!important` rule — silently won
  the tie by file position alone, painting Clay Deep chrome back to Paper. Pair the
  selector with `.mud-elevation-0` (`.mud-appbar.mud-elevation-0`,
  `.mud-drawer.mud-elevation-0`) so the override wins on specificity instead of
  source order
  of scrolling it.
