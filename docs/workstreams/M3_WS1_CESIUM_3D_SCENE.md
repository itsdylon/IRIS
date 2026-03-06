# M3 Workstream 1: Cesium 3D Scene + Marker Pipeline

**Milestone 3 — March 6–20, 2026**
**Priority:** CRITICAL PATH — everything else depends on this
**Dependencies:** None — starts Day 1
**Estimated effort:** 3–4 days
**Component:** Unity (`unity/IRIS-AR/`)

---

## Context

Markers currently exist in a blank Unity void, positioned by a flat-earth formula in `GeoUtils.cs`. This workstream replaces that with a rendered 3D digital twin (Cesium terrain + buildings) and WGS84-accurate marker positioning via `CesiumGlobeAnchor`.

After this workstream, you can fly around a 3D Georgia Tech campus and see colored markers pinned to real buildings.

---

## Tasks

### 1A. Install Cesium for Unity

**Can start Day 1. No dependencies.**

1. Edit → Project Settings → Package Manager → Add Scoped Registry:
   - Name: `Cesium`
   - URL: `https://unity.pkg.cesium.com`
   - Scope: `com.cesium.unity`
2. Window → Package Manager → My Registries → Install **Cesium for Unity**
3. Window → Cesium → Connect to Cesium ion → sign in (free account at cesium.com/ion)
4. Create a default project access token

**Verify:** Project compiles with zero errors alongside `com.meta.xr.sdk.all` v85.

**Risk:** This is the highest-risk task in the entire milestone. If Cesium and Meta XR SDK have native plugin conflicts, nothing else moves. Budget the full day for this if needed. If conflicts arise, try removing `com.meta.xr.simulator.synthenvbuilder` (v60) first — it's the oldest Meta package and least likely to be needed immediately.

**File:** `unity/IRIS-AR/Packages/manifest.json`

---

### 1B. Set up CesiumGeoreference + 3D Tilesets

**Depends on 1A.**

In the MainAR scene:

1. From the Cesium panel, click **Quick Add → Cesium World Terrain + Bing Maps Aerial imagery**
   - Auto-creates a `CesiumGeoreference` parent with a `Cesium3DTileset` child
2. Set the `CesiumGeoreference` origin:
   - Latitude: `33.7756`, Longitude: `-84.3963`, Height: `0`
3. From the Cesium panel, click **Quick Add → Cesium OSM Buildings**
   - Adds a second `Cesium3DTileset` child
4. On each `Cesium3DTileset`, set:
   - Maximum Screen Space Error: `16`
   - Maximum Simultaneous Tile Loads: `10`
   - Maximum Cached Bytes: `512 MB`
   - Enable Frustum Culling: `true`
   - Enable Fog Culling: `true`

**Alternative tileset:** Google Photorealistic 3D Tiles (more realistic photogrammetry mesh, heavier). If used, enable "Show Credits On Screen" on the tileset component for ToS compliance.

**Unity Inspector steps (manual):**
> 1. Open Cesium panel (Window → Cesium)
> 2. Connect to Cesium ion, sign in
> 3. Quick Add → Cesium World Terrain + Bing Maps Aerial imagery
> 4. Quick Add → Cesium OSM Buildings
> 5. Select CesiumGeoreference → Latitude: 33.7756, Longitude: -84.3963, Height: 0
> 6. Main Camera → Near Clip: 1, Far Clip: 100000

**File:** `unity/IRIS-AR/Assets/IRIS/Scenes/MainAR.unity`

---

### 1C. Add DynamicCamera for desktop navigation

**Depends on 1B.**

From the Cesium panel, click **Quick Add → DynamicCamera**. Controls:
- **WASD** — move, **Q/E** — up/down, **Mouse** — look, **Scroll** — speed
- Speed auto-scales with altitude

Position near ground level at the GT campus origin (~2m altitude).

**Note:** When using XR Simulator, disable DynamicCamera and use OVRCameraRig. A runtime toggle or separate scene profile handles this (see WS4 task 4D).

**Unity Inspector steps (manual):**
> 1. Quick Add → DynamicCamera (from Cesium panel)
> 2. Position at origin, altitude ~2m
> 3. Play mode → verify WASD + mouse navigation over 3D terrain

---

### 1D. Add CesiumGlobeAnchor to marker prefab

**Depends on 1A (Cesium installed). Can run parallel with 1B/1C.**

Add `CesiumGlobeAnchor` component to `AnchorPrefab.prefab`. This locks markers to WGS84 coordinates — they stay at the correct geographic position even if the CesiumGeoreference origin changes.

**Unity Inspector steps (manual):**
> 1. Open `Assets/IRIS/Prefabs/AnchorPrefab.prefab`
> 2. Add Component → CesiumGlobeAnchor
> 3. Save prefab

---

### 1E. Rewrite AnchorManager to use Cesium

**Depends on 1A, 1D.**

Update `AnchorManager.cs`:

**Replace** the geo conversion in `HandleMarkerCreated()`:
```csharp
// OLD (flat-earth):
var spawnPos = GeoUtils.LatLngToUnityPosition(marker.lat, marker.lng, referenceLat, referenceLng);
var anchor = SpawnAnchor(spawnPos, marker);

// NEW (Cesium):
var anchor = SpawnAnchor(Vector3.zero, marker);
var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
globeAnchor.SetPositionLongitudeLatitudeHeight(marker.lng, marker.lat, markerAltitude);
```

**Remove:**
- `referenceLat` / `referenceLng` SerializeFields
- `using IRIS.Geo;` import
- All `GeoUtils` calls

**Add:**
- `using CesiumForUnity;`
- `[SerializeField] private CesiumGeoreference georeference;` — for reverse conversions (1F)
- `[SerializeField] private float markerAltitude = 2f;` — default height above terrain

Keep `GeoUtils.cs` in the codebase but unused — cleanup is not a blocker.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

**Unity Inspector steps (manual):**
> 1. On the AnchorManager component, drag the scene's CesiumGeoreference into the new `georeference` field

---

### 1F. Wire controller-placed markers through the server

**Depends on 1E.**

`SpawnMarkerAtController()` currently creates local-only markers with a random GUID. Fix it to go through the server so markers sync to all clients.

**Step 1 — Add lat/lng to MarkerCreatePayload:**
```csharp
public class MarkerCreatePayload
{
    public double lat { get; set; }
    public double lng { get; set; }
    public string label { get; set; }
    public string type { get; set; }
}
```

**Step 2 — Add EmitMarkerCreate to C2Client:**
```csharp
public void EmitMarkerCreate(double lat, double lng, string label, string type)
{
    if (!IsConnected) return;
    _socket.Emit("marker:create", new MarkerCreatePayload
    {
        lat = lat, lng = lng, label = label, type = type
    });
}
```

**Step 3 — Rewrite SpawnMarkerAtController:**
```csharp
private void SpawnMarkerAtController()
{
    var pos = /* controller or camera position */;
    var llh = georeference.TransformUnityPositionToLongitudeLatitudeHeight(
        new double3(pos.x, pos.y, pos.z));
    // llh = (longitude, latitude, height)
    c2Client.EmitMarkerCreate(llh.y, llh.x, "Placed Marker", "waypoint");
    // Do NOT spawn locally — let marker:created from server trigger HandleMarkerCreated()
}
```

**Files:**
- `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/MarkerEventData.cs` — add lat/lng fields
- `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/C2Client.cs` — add EmitMarkerCreate()
- `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs` — rewrite SpawnMarkerAtController()

---

### 1G. Desktop input for marker placement

**Depends on 1E, 1F.**

Add keyboard bindings for desktop mode (no controller available):
- **M key** — place a marker at the current camera position
- **C key** — trigger calibration at the current camera position (used by WS2 CalibrationManager)

```csharp
// Assets/IRIS/Scripts/Core/DesktopInputManager.cs
namespace IRIS.Core
{
    public class DesktopInputManager : MonoBehaviour
    {
        [SerializeField] private AnchorManager anchorManager;
        [SerializeField] private CalibrationManager calibrationManager; // wired after WS2

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
                anchorManager.PlaceMarkerAtCamera();

            if (Input.GetKeyDown(KeyCode.C))
                calibrationManager?.Calibrate();
        }
    }
}
```

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Core/DesktopInputManager.cs` (new)

---

## Verification

1. Play mode → 3D terrain + buildings render at GT campus
2. DynamicCamera → WASD navigation works
3. Dashboard → click map → marker appears at correct building in 3D scene
4. Press M in 3D scene → marker appears on dashboard at correct lat/lng
5. Marker colors match type (blue/red/green/yellow/white)

---

## Files Modified / Created

| File | Action |
|---|---|
| `Packages/manifest.json` | Edit — add Cesium scoped registry |
| `Assets/IRIS/Scenes/MainAR.unity` | Edit — add CesiumGeoreference, tilesets, DynamicCamera |
| `Assets/IRIS/Prefabs/AnchorPrefab.prefab` | Edit — add CesiumGlobeAnchor component |
| `Assets/IRIS/Scripts/Anchors/AnchorManager.cs` | Edit — rewrite for Cesium, controller markers through server |
| `Assets/IRIS/Scripts/Networking/MarkerEventData.cs` | Edit — add lat/lng to MarkerCreatePayload |
| `Assets/IRIS/Scripts/Networking/C2Client.cs` | Edit — add EmitMarkerCreate() |
| `Assets/IRIS/Scripts/Core/DesktopInputManager.cs` | **Create** — keyboard input for desktop mode |

---

## Unblocks

Completing this workstream unblocks:
- **WS2 tasks 2E–2F** (CalibrationManager needs CesiumGeoreference)
- **WS4 tasks 4A, 4B, 4D** (XR Simulator, multi-instance testing, HUD all need the scene to exist)
