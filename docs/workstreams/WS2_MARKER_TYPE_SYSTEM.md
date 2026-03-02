# Workstream 2: Marker Type System (Unity + Dashboard)

**Milestone 2 — Week of March 2–8, 2026**
**Priority:** Medium — dashboard tasks are unblocked, Unity tasks depend on WS1
**Estimated effort:** 1–2 days

---

## Context

The marker type field exists end-to-end (`type` is stored on the server, transmitted via Socket.IO, parsed in Unity) but is never used visually. The dashboard hard-codes `type: 'generic'` on every marker (MapView.jsx:25) and the only user input is a `prompt()` for the label. In Unity, all markers render as identical cubes colored cyan (placed) or yellow (pending).

This workstream adds visual differentiation by type across both clients and gives the dashboard operator a way to select the type when placing markers.

---

## Type Palette

| Type | Color (hex) | Use Case |
|------|-------------|----------|
| `waypoint` | Blue (`#3B82F6`) | Navigation/rally points |
| `threat` | Red (`#EF4444`) | Hostile positions, hazards |
| `objective` | Green (`#22C55E`) | Mission objectives, targets |
| `info` | Yellow (`#EAB308`) | General information, notes |
| `generic` | White (`#FFFFFF`) | Default/unclassified |

---

## Tasks

### 2A. Define the type palette as constants

Create a shared reference for both Unity and dashboard.

**Unity** — add a static dictionary or switch in `AnchorVisualizer.cs` (or a new `MarkerTypes.cs` if preferred):
```csharp
public static Color GetColorForType(string type) => type switch
{
    "waypoint"  => new Color(0.23f, 0.51f, 0.96f), // blue
    "threat"    => new Color(0.94f, 0.27f, 0.27f),  // red
    "objective" => new Color(0.13f, 0.77f, 0.37f),  // green
    "info"      => new Color(0.92f, 0.70f, 0.03f),  // yellow
    _           => Color.white,                       // generic
};
```

**Dashboard** — add a `MARKER_TYPES` constant object (can live at top of `MapView.jsx` or in a shared `constants.js`).

**Files:** `AnchorVisualizer.cs`, `MapView.jsx` (or new shared files)

---

### 2B. Update `AnchorVisualizer.cs` with type-based coloring

Add a `public void SetType(string type)` method that calls the color lookup from 2A and applies it.

Then update `AnchorManager.HandleMarkerCreated()`:
- Replace `SetAnchorColor(anchor, Color.cyan)` and `SetAnchorColor(anchor, Color.yellow)` with `SetType(marker.type)` calls
- Pending markers can use a dimmed/transparent version of the type color to distinguish from placed markers

**Files:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorVisualizer.cs`, `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

**Note:** These AnchorManager edits touch the same method as WS1 task 1D. Coordinate with WS1 owner — either merge the changes or have WS1 land first, then modify.

---

### 2C. Add marker type selector to dashboard `MapView.jsx`

**Can start Day 1 — no dependency on WS1.**

Replace the current `prompt('Marker label:')` flow (MapView.jsx:19) with a form or modal. Options:

**Option A (simple — inline popup):** Use a small absolutely-positioned form that appears at the click location with:
- Text input for label
- `<select>` dropdown with the 5 types
- Submit / Cancel buttons

**Option B (simpler — sequential prompts):** Keep prompt for label, add a second prompt or `confirm` for type. (Not ideal UX but fastest to ship.)

Recommended: Option A. The form should:
1. Appear on map click at the cursor position
2. Default type to `waypoint` (most common use case)
3. On submit, call `onMapClick({ lat, lng, label, type })`
4. On cancel or click-away, dismiss without creating a marker

**File:** `dashboard/src/components/MapView.jsx` — may also want a new `MarkerCreateForm.jsx` component

**Note:** `MarkerCreateForm.jsx` already exists in the repo but is an **empty stub** (0 lines). Build the form there and import it into `MapView.jsx`.

---

### 2D. Color-code markers on the Leaflet map

**Can start Day 1 — no dependency on WS1.**

Replace the default blue Leaflet markers with type-colored icons. Approach:

```jsx
const markerIcon = (type) => L.divIcon({
  className: `marker-icon marker-${type}`,
  html: `<div style="background:${MARKER_TYPES[type]?.color || '#fff'};
         width:12px; height:12px; border-radius:50%; border:2px solid #333;"></div>`,
  iconSize: [16, 16],
  iconAnchor: [8, 8],
})
```

Add a small legend overlay in the bottom-right corner of the map showing all 5 types with their color dots.

**File:** `dashboard/src/components/MapView.jsx`

---

### 2E. Update `MarkerPanel.jsx` with type badges and status

**Can start Day 1 — no dependency on WS1.**

Current state (MarkerPanel.jsx:12-16): shows either `(x, y, z)` position or "Pending placement". The type is shown as plain text.

Update to show:
- A small colored dot matching the type color, next to the type name
- Status as a badge: `pending` (orange) / `placed` (green)
- Both lat/lng AND AR position when available (lat/lng is always available for dashboard-created markers; AR position is filled after placement)
- Note: currently the lat/lng display is commented out (line 12) — uncomment and show both

**Bug fix to include:** In `useSocket.js`, the `marker:updated` listener (line 21) is registered but **never cleaned up** in the useEffect return (lines 24-28). Add `socket.off('marker:updated')` to the cleanup function. This prevents listener accumulation on re-renders.

**Files:** `dashboard/src/components/MarkerPanel.jsx`, `dashboard/src/hooks/useSocket.js`

---

## Verification

1. Open the dashboard, click the map — a form should appear with label input + type dropdown
2. Create one marker of each type — they should appear as different colored dots on the map
3. The sidebar should show colored badges and "pending" status
4. In Unity (after WS1 lands), markers should render in their type color
5. After a marker is placed by the headset, dashboard sidebar should update to "placed" with both lat/lng and AR position

---

## Files Modified

| File | Action |
|------|--------|
| `dashboard/src/components/MapView.jsx` | Edit — type selector form, colored map markers, legend |
| `dashboard/src/components/MarkerPanel.jsx` | Edit — type badges, status indicators, lat/lng display |
| `dashboard/src/hooks/useSocket.js` | **Bug fix** — add `socket.off('marker:updated')` to cleanup |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorVisualizer.cs` | Edit — add `SetType()`, type-to-color mapping |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs` | Edit — use `SetType()` instead of `SetColor()` |
| `dashboard/src/components/MarkerCreateForm.jsx` | Possibly edit or create — check if existing file is a stub |
