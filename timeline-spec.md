# HomeGuard Timeline — Component Specification

The custom timeline component replaces vis-timeline with a fully self-contained
Blazor/JS implementation. The interactive HTML prototype is in `docs/timeline-prototype.html`.

---

## Visual Structure

```
┌────────────────────────────────────────────────────────────────┐
│  [Today] [Fit all]                    [🛡 Warranties] [🔧 Service] │  toolbar
├──────────────┬─────────────────────────────────────────────────┤
│              │  Jan 23    Jul 23    Jan 24    Jan 25    Jan 26  │  time axis
├──────────────┼─────────────────────────────────────────────────┤
│ TOYOTA CAMRY │                                                  │  group header row
│  Engine oil  │  [●]──9mo·+7199km──[●]──10mo·+7209km──[●] ···[○]│  service row
│  Air filters │       [●]──────1y5mo·+15385km──────[●]  ···[○]  │  service row
├──────────────┼─────────────────────────────────────────────────┤
│ DISHWASHER   │                                                  │
│  Warranty    │  [■]═══════════════3y══════════════[✕]          │  warranty row
└──────────────┴─────────────────────────────────────────────────┘
     fixed           pannable + zoomable (wheel)
     170px
```

- **Label column** (170px): fixed, never scrolls
- **Timeline area**: pan (drag) + zoom (wheel), overflow hidden
- **Group header rows**: UPPERCASE, `letter-spacing: 0.06em`, `font-weight: 600`
- **Service rows**: one row per `(EquipmentId, RecurringRule.Title)` pair
- **Warranty rows**: one row per warranty record

---

## Data Model for Timeline

### Timeline row sources

```csharp
// One row per completed/planned cluster
record ServiceRow(
    Guid         EquipmentId,
    string       EquipmentName,
    string       GroupLabel,      // null if same equipment as row above
    string       CanonicalTitle,  // RecurringRule.Title or fuzzy-matched title
    string       Color,           // "#5B6EC2" for service
    List<ServiceEventPoint> Events
);

// One row per warranty
record WarrantyRow(
    Guid     EquipmentId,
    string   EquipmentName,
    string?  GroupLabel,
    Guid     WarrantyId,
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool     IsExpired
);
```

### Event point on the timeline

```csharp
record ServiceEventPoint(
    Guid?         Id,             // null = Predicted (not in DB)
    DateOnly      Date,
    decimal?      MeterReading,
    string?       MeterUnit,      // from Equipment
    decimal?      Cost,
    string?       ServiceProvider,
    ServiceStatus Status,         // Completed | Planned | Predicted
    bool          IsPredicted     // Status == Predicted (computed)
);
```

### Status → visual style

| Status | Button style | Color |
|---|---|---|
| `Completed` | Solid fill, `color + cc` opacity | Row color |
| `Planned` | Solid fill, slightly lighter | Row color |
| `Predicted` | Gray, **dashed border**, no fill | `#aaa` |
| Warranty start | Solid | `#7F77DD` |
| Warranty expiry (future) | Solid | `#7F77DD` |
| Warranty expiry (past) | Solid | `#ef4444` with ✕ |
| Warranty expired (entire row) | Grayed | `#aaa` |

---

## Layout Constants

```javascript
const LW  = 170;   // label column width px
const RH  = 50;    // row height px
const BTN = 28;    // event button size px (square)
const AH  = 44;    // axis height px
```

---

## Interaction Behaviors

### Zoom (mouse wheel)
- Anchor point: date under cursor stays fixed
- Min zoom: 6 hours/px (`MS / 8`)
- Max zoom: 400 days/px
- Factor per step: `1.2`

### Pan (drag)
- Drag timeline area left/right
- Cursor: `grab` → `grabbing`
- Label column is NOT draggable

### Click on event button
- Centers the time axis on that event's date
- Highlights the button (white ring + `box-shadow`)
- Opens detail card below the timeline
- Second click on same button → deselect and close card
- Only one event selected at a time

### Hover on event button
- Scale `1.15`, `box-shadow`
- **Date badge** (top-right of timeline area) shows: `15 Jan 2024 · 59,598 km`
- On hover-off: badge reverts to cursor date

### Hover on interval block
- Background opacity increases
- Label text → `font-weight: 600`

### Vertical cursor line
- Follows mouse across full height of rows area
- Separate overlay div (never triggers re-render)
- **Date badge** shows current cursor date when no button is hovered

### Predicted event click
- Does NOT open a record card (no DB record exists)
- Opens a "Materialize?" prompt: confirm date → creates `Planned` ServiceRecord

---

## Train Collision (Button Layout)

When events are close together at zoom-out, buttons must never overlap.
Apply left-to-right push — "паровозик":

```javascript
function adjPos(events) {
    const pos = events.map(ev => toX(ev.date));
    for (let i = 1; i < pos.length; i++)
        if (pos[i] < pos[i-1] + BTN + 1)
            pos[i] = pos[i-1] + BTN + 1;
    return pos;
}
```

Button pixel boundaries are absolute — never violate them.

---

## Interval Blocks

Blocks sit **strictly between** consecutive buttons:

```javascript
const x1 = pos[i] + BTN;      // right edge of button i
const x2 = pos[i + 1];        // left edge of button i+1
const cx = Math.max(0, x1);   // clip left
const ce = Math.min(TW, x2);  // clip right
const cw = ce - cx;
if (cw < 1) return;            // skip if no space (train collision or off-screen)
```

**Label inside block** (adaptive):
- `cw > 44px`: show label
- Label content: `{timeSpan} · +{meterDelta} {unit}` e.g. `9mo · +7,199 km`
- If no meter data: just `9mo`
- If `cw` shrinks below 44px: hide label silently (block remains visible as colored strip)

**Block colors:**
```
Completed interval:  row.color + '28' opacity  →  hover: row.color + '88'
Predicted interval:  #e0e0e0                   →  hover: #bbb
```

---

## Time Axis

### Tick interval selection

```javascript
const dv = mpp * TW / MS;  // days visible
const iv =
    dv < 30   ? 7   :
    dv < 90   ? 14  :
    dv < 200  ? 30  :
    dv < 400  ? 60  :
    dv < 800  ? 90  :
    dv < 1500 ? 180 : 365;
```

### Tick label filtering
Minimum **64px** between rendered ticks (Plus Jakarta Sans is wider than Roboto):

```javascript
const filtered = [];
let lastX = -Infinity;
raw.forEach(t => {
    const x = toX(t);
    if (x >= -20 && x <= TW + 20 && x - lastX >= 64) {
        filtered.push(t);
        lastX = x;
    }
});
```

### Tick label format (adaptive)

| Days visible | Format | Example |
|---|---|---|
| > 1200 | `'YY` | `'25` |
| > 400 | `MM/YY` | `01/25` |
| > 100 | `Mon YY` | `Jan 25` |
| ≤ 100 | `D Mon` | `15 Jan` |

---

## Toggle Buttons

```
🛡 Warranties   — shows/hides all warranty rows
🔧 Service      — shows/hides all service rows
☑ Show expired  — (within Warranties) shows historically expired warranties
```

Active toggle: filled background (`row.color`), white text.
Inactive: transparent, colored border and text, `opacity: 0.4`.

---

## Detail Card (below timeline)

Appears when an event button is clicked. Fields:

| Field | Source |
|---|---|
| Title | `ServiceRecord.Title` |
| Equipment | `Equipment.Name` |
| Date | `ServiceRecord.ServiceDate` |
| Status | `Completed` / `Planned` |
| Meter reading | `ServiceRecord.MeterReading + Equipment.MeterUnit` |
| Cost | `ServiceRecord.Cost` (if set) |
| Provider | `ServiceRecord.ServiceProvider` (if set) |
| Notes | `ServiceRecord.Notes` (if set) |

Card has a close button (✕). Clicking same event button again also closes it.

---

## Blazor Implementation Plan

### Files to create/modify

```
HomeGuard.Client/
  Pages/Timeline.razor                  ← full rewrite
  Services/TimelineInterop.cs           ← update records + JS calls
  wwwroot/js/homeguard-timeline.js      ← replace (see prototype)
  wwwroot/css/timeline.css              ← add new class rules
HomeGuard.Application/
  Services/TimelinePredictionService.cs ← NEW: computes Predicted events
HomeGuard.Domain/
  Entities/RecurringRule.cs             ← NEW entity
  Enums/ServiceStatus.cs                ← NEW enum
  Entities/ServiceRecord.cs             ← add Status, RecurringRuleId, MeterReading
  Entities/Equipment.cs                 ← add MeterUnit
HomeGuard.Common/
  DTOs/TimelineDto.cs                   ← NEW: ServiceEventPoint, row DTOs
HomeGuard.Infrastructure/
  Persistence/AppDbContext.cs           ← add RecurringRule DbSet
  Migrations/                           ← new migration
```

### Key C# records (TimelineInterop)

```csharp
public sealed record TimelineItem(
    string    Id,
    string    Content,
    DateOnly  Start,
    DateOnly? End       = null,
    string?   Group     = null,
    string?   ClassName = null,
    string?   Tooltip   = null
    // NOTE: no Subgroup — use nestedGroups mechanism instead
);

public sealed record TimelineGroup(
    string  Id,
    string  Content,
    int     Order         = 0,
    string? NestedInGroup = null
);
```

### Prediction algorithm (TimelinePredictionService)

```csharp
// For a given (EquipmentId, RecurringRule), compute next N predicted dates
public IEnumerable<ServiceEventPoint> GetPredictions(
    RecurringRule rule,
    IReadOnlyList<ServiceRecord> completedRecords,  // ordered by date
    int count)
{
    if (completedRecords.Count < 2) yield break;

    // Average interval from last N completed records (N = min(5, count))
    var recent = completedRecords.TakeLast(5).ToList();
    var avgDays = (recent.Last().ServiceDate.DayNumber
                - recent.First().ServiceDate.DayNumber)
                / (double)(recent.Count - 1);

    var lastDate = completedRecords.Last().ServiceDate;
    for (int i = 0; i < count; i++)
    {
        lastDate = lastDate.AddDays((int)Math.Round(avgDays));
        yield return new ServiceEventPoint(
            Id:           null,
            Date:         lastDate,
            MeterReading: null,
            Status:       ServiceStatus.Predicted,
            IsPredicted:  true
        );
    }
}
```

---

## CSS Classes (timeline.css)

```css
/* Event button states */
.hg-service          { /* blue: #5B6EC2 */ }
.hg-service.planned  { /* lighter blue */ }
.hg-warranty         { /* purple: #7F77DD */ }
.hg-warranty.expired { /* gray: #aaa */ }
.hg-expiry           { /* red: #ef4444, shows ✕ */ }
.hg-predicted        { /* dashed border, gray */ }

/* Selected state */
.hg-selected {
    outline: 2px solid white;
    box-shadow: 0 0 0 3px {color}55;
    transform: scale(1.1);
}
```

---

## Reference

- **Interactive prototype:** `docs/timeline-prototype.html`
  Open in browser — fully functional zoom/pan/click/hover demo.
  Data in prototype matches approximate real HomeGuard data.

- **Font:** Plus Jakarta Sans loaded from Google Fonts (400/500/600)
  ```html
  <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600&display=swap" rel="stylesheet">
  ```

- **vis-timeline CDN (current, keep until replaced):**
  ```html
  <link href="https://cdnjs.cloudflare.com/ajax/libs/vis-timeline/7.7.3/vis-timeline-graph2d.min.css" rel="stylesheet" />
  <script src="https://cdnjs.cloudflare.com/ajax/libs/vis-timeline/7.7.3/vis-timeline-graph2d.min.js"></script>
  ```
  After replacing, these lines can be removed from `index.html`.
