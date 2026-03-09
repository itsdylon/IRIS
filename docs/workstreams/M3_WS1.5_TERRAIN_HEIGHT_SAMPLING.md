# M3 Workstream 1.5: Terrain Height Sampling for Markers

**Milestone 3 — March 2026**
**Priority:** Enhancement — markers work without this but float at fixed altitude
**Dependencies:** M3 WS1 (Cesium 3D scene)
**Estimated effort:** 1–2 days
**Component:** Unity (`unity/IRIS-AR/`), Dashboard (`dashboard/`)

---

## Context

Markers currently spawn at a hardcoded `markerAltitude = 2f` meters above the WGS84 ellipsoid. On flat terrain near sea level this looks fine, but on hilly terrain or near tall buildings, markers may float in the air or clip through the ground.

Cesium provides `CesiumTerrainSampler` (or raycasting against loaded 3D tiles) to query the actual terrain height at a given lat/lng. Using this, markers can be placed at terrain surface + an optional offset.

---

## Tasks

### 1.5A. Sample terrain height at marker position (Unity)

**Depends on M3 WS1.**

In `AnchorManager.HandleMarkerCreated()`, after determining lat/lng, query the terrain height before setting the globe anchor position.

**Approach — Raycast against Cesium tiles:**
```csharp
// Cast a ray downward from high altitude at the marker's Unity position
// to find where it hits the Cesium terrain mesh
var origin = /* Unity position at marker lat/lng, high altitude */;
if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, Mathf.Infinity))
{
    // hit.point.y = terrain surface height in Unity coords
    // Convert back to LLH for the globe anchor
}
```

**Alternative — CesiumSampleHeightMostDetailedAsync:**
Cesium for Unity may expose an async height query API. Check `Cesium3DTileset` for height sampling methods. This is more accurate than raycasting but requires tiles to be loaded at that location.

**Key decisions:**
- Raycast requires Cesium tiles to have mesh colliders enabled (`Generate Colliders` on `Cesium3DTileset`)
- Async API may not be available in Cesium for Unity 1.13.0 — verify before choosing approach
- Fallback to hardcoded altitude if terrain height query fails

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

---

### 1.5B. Add height offset to marker create payload

**Depends on 1.5A.**

Allow the command dashboard to specify an optional height offset when placing markers. This lets operators place markers at specific floors of a building (e.g., +3m for 1st floor, +6m for 2nd floor).

**Server changes:**
- `marker:create` payload already accepts arbitrary fields — add `heightOffset` (number, optional, default 0)
- Store in markers table: add `height_offset REAL` column
- Return in `marker:created` broadcast

**Dashboard changes:**
- Add optional height offset input to marker placement dialog (number field, default empty/0)
- Send `heightOffset` in `marker:create` emit

**Unity changes:**
- Parse `heightOffset` from `MarkerData` (add field)
- Final marker height = terrain height + heightOffset

**Files:**
- `server/src/socket/markerHandlers.js`
- `server/src/models/Marker.js`
- `server/src/db.js` (migration)
- `dashboard/src/components/MapView.jsx`
- `unity/IRIS-AR/Assets/IRIS/Scripts/Markers/MarkerData.cs`
- `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

---

## Verification

1. Place marker from dashboard on flat terrain → marker sits on ground surface, not floating
2. Place marker on hilly area → marker follows terrain contour
3. Set height offset to 5m from dashboard → marker floats 5m above terrain
4. Height offset of 0 (default) → same as no offset, marker at terrain surface
5. If terrain tiles not loaded at marker position → falls back to ellipsoid height + offset

---

## Files Modified / Created

| File | Action |
|---|---|
| `Assets/IRIS/Scripts/Anchors/AnchorManager.cs` | Edit — add terrain height query |
| `Assets/IRIS/Scripts/Markers/MarkerData.cs` | Edit — add heightOffset field |
| `server/src/socket/markerHandlers.js` | Edit — pass through heightOffset |
| `server/src/models/Marker.js` | Edit — add heightOffset field |
| `server/src/db.js` | Edit — add height_offset column |
| `dashboard/src/components/MapView.jsx` | Edit — add height offset input |
