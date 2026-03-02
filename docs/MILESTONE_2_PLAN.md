# IRIS — Milestone 2: GPS-to-AR Marker Positioning

**Week of March 2–8, 2026**
**Team size: 4**

---

## Decision Record: Geo-Positioning Approach

### Evaluated: Google ARCore Geospatial API

A team member proposed using Google's ARCore Geospatial API with the Geospatial Creator plugin. This would leverage Google Street View / Earth data to geo-anchor AR content at lat/lng coordinates and provide photorealistic 3D tile previews in the Unity Editor.

**We are not using this approach.** The reasons:

1. **ARCore Geospatial does not support Meta Quest 3.** The supported device list is limited to Android phones/tablets and iOS. Quest 3 runs Meta Horizon OS without Google Play Services.
2. **Quest 3 lacks GPS hardware.** ARCore Geospatial requires GPS as a bootstrap for its Visual Positioning System pipeline.
3. **Google's upcoming Android XR headsets** (Samsung Galaxy XR, expected 2026) will support it, but that is a different ecosystem from Meta Quest entirely.

### Chosen: Flat-Earth Coordinate Mapping + Intent-Based Placement

We will implement a lightweight GPS-to-Unity coordinate conversion using a flat-earth approximation. This:

- **Works on Quest 3 today** with zero external dependencies
- **Fits the existing architecture** — the server already stores both `(lat, lng)` and `(x, y, z)` with a two-stage `pending → placed` lifecycle
- **Is simple** — the core conversion is ~20 lines of C#
- **Supports the C2 model** — operators place markers with geographic intent, the headset grounds them in local AR space

The reference anchor point is **Georgia Tech campus center (33.7756, -84.3963)**, which is already hard-coded as `GT_CENTER` in the dashboard's `MapView.jsx`.

### Future Consideration: Niantic Spatial SDK

Niantic's Spatial SDK v3.15 has beta Quest 3 support with centimeter-level VPS localization. Coverage is limited to ~1M Niantic-scanned locations (urban/gaming hotspots). Worth revisiting if IRIS needs outdoor VPS localization at scale, but overkill for our current milestone.

---

## Current State (Starting Point)

| Component | What Works | What's Missing |
|-----------|-----------|----------------|
| **C2 Server** | Marker CRUD (`create`, `list`, `delete`, `place`), device presence, `marker:updated` broadcast | Markers don't carry `lat`/`lng` through to Unity positioning; no geo conversion |
| **Dashboard** | Leaflet map, click-to-place with `lat`/`lng`, real-time sync, marker panel shows AR position or "Pending" | No marker type selector, no status badge, no visual distinction by type |
| **Unity AR** | C2Client connected via Socket.IO, AnchorManager spawns markers, `marker:place` round-trip works | Pending markers spawn 2m in front of camera (ignores lat/lng), no geo conversion, no type-based visuals, no AR HUD |

### The Core Gap

When the dashboard creates a marker at `(33.7760, -84.3950)`, the Unity app receives it but **ignores the lat/lng entirely** — it just spawns a yellow cube 2m in front of the camera. There is no function that converts geographic coordinates to Unity world-space positions.

---

## Unified Plan

### Goal

By end of week: a marker placed on the dashboard at a specific lat/lng appears in the Quest 3 headset at the correct relative position in AR space, with type-appropriate visuals and a status indicator visible on both dashboard and headset.

### Architecture

```
Dashboard (Leaflet)                    Unity (Quest 3)
  Click map at lat/lng                   GeoUtils converts lat/lng → (x, y, z)
       │                                      │
       ▼                                      ▼
  socket.emit('marker:create',           AnchorManager spawns at
    { lat, lng, label, type })           computed position
       │                                      │
       ▼                                      ▼
  ┌─────────────────┐                   Marker appears in AR space
  │   C2 Server     │                   at geographically correct
  │   (Node.js)     │◄──────────────── relative location
  └─────────────────┘
       │
       ▼
  io.emit('marker:created',
    { id, lat, lng, label, type,
      status, position, createdAt })
```

**Reference origin:** `GT_CENTER (33.7756, -84.3963)` → Unity `(0, 0, 0)`

**Conversion (flat-earth):**
```
x = (lng - refLng) × 111,320 × cos(refLat)   // east-west → Unity X
z = (lat - refLat) × 110,540                   // north-south → Unity Z
y = 1.5                                         // fixed eye-level height
```

At Georgia Tech's latitude, 1 degree ≈ 111km. For the ~500m campus radius we work in, flat-earth error is negligible (<1m).

---

### Deliverables

| # | Deliverable | Component | Files Touched |
|---|-------------|-----------|---------------|
| 1 | `GeoUtils.cs` — lat/lng ↔ Unity position converter | Unity | New file |
| 2 | `MarkerData` carries `lat`/`lng` from server | Unity | `MarkerData.cs`, `C2Client.cs` |
| 3 | `AnchorManager` uses geo conversion for pending markers | Unity | `AnchorManager.cs` |
| 4 | Server adds `lat`/`lng` to `marker:create` payload passed to Unity | Server | `markerHandlers.js` (already works — lat/lng included) |
| 5 | Marker type system — color-coded visuals in AR | Unity | `AnchorVisualizer.cs`, `AnchorManager.cs` |
| 6 | Marker type selector on dashboard | Dashboard | `MapView.jsx`, `MarkerPanel.jsx` |
| 7 | Status badges on dashboard (pending/placed) | Dashboard | `MarkerPanel.jsx` |
| 8 | AR HUD — connection status, marker count | Unity | New `HUDManager.cs` |
| 9 | Reference origin configurable on server | Server | `Marker.js` or new config |
| 10 | End-to-end integration test | All | Manual test script |

---

## Workstreams

Detailed task breakdowns are in individual files — one per workstream, ready to hand to each team member:

| # | Workstream | File | Start |
|---|---|---|---|
| 1 | **Geo Conversion Core** (Unity) | [`workstreams/WS1_GEO_CONVERSION_CORE.md`](workstreams/WS1_GEO_CONVERSION_CORE.md) | Day 1 — critical path |
| 2 | **Marker Type System** (Unity + Dashboard) | [`workstreams/WS2_MARKER_TYPE_SYSTEM.md`](workstreams/WS2_MARKER_TYPE_SYSTEM.md) | Dashboard Day 1, Unity after WS1 |
| 3 | **AR HUD & UX** (Unity) | [`workstreams/WS3_AR_HUD_AND_UX.md`](workstreams/WS3_AR_HUD_AND_UX.md) | HUD layout Day 1, geo tasks after WS1 |
| 4 | **Server & Integration Testing** | [`workstreams/WS4_SERVER_AND_INTEGRATION.md`](workstreams/WS4_SERVER_AND_INTEGRATION.md) | Day 1 — no blockers |

---

## Workstream Dependencies

```
WS4 (Server)  ────────────────────────────────────────► fully unblocked, start Day 1
WS1 (Geo Core) ──────────────────────────────────────► fully unblocked, start Day 1
                │
                ├──► WS2 (Types) ─────────────────────► dashboard tasks Day 1, Unity tasks after WS1
                │
                └──► WS3 (AR HUD) ────────────────────► HUD layout Day 1, geo tasks after WS1
```

**Suggested team assignment:**
- **Person 1** → WS1 (Geo Core) — this is the critical path, unblocks WS2 + WS3 Unity tasks
- **Person 2** → WS4 (Server) — fully unblocked, can start and finish independently
- **Person 3** → WS2 (Types) — start with dashboard tasks (2C, 2D, 2E) on Day 1, pick up Unity tasks (2A, 2B) once WS1 lands
- **Person 4** → WS3 (AR HUD) — start with HUD layout (3A, 3C) on Day 1, pick up geo-dependent tasks (3B, 3D) once WS1 lands

### Known Bug (fix included in WS2)

`dashboard/src/hooks/useSocket.js` registers a `marker:updated` listener (line 21) but never cleans it up in the useEffect return (lines 24–28). This causes listener accumulation. Fix is included as part of WS2 task 2E.

---

## Definition of Done

The milestone is complete when:

- [ ] A marker placed on the dashboard at a GT campus lat/lng spawns in Unity at the correct relative position
- [ ] A marker placed via Quest 3 controller appears on the dashboard map at the correct lat/lng
- [ ] Markers are color-coded by type in both dashboard and AR
- [ ] The AR HUD shows connection status and marker count
- [ ] The integration test script passes
- [ ] The manual test protocol has been executed successfully at least once
