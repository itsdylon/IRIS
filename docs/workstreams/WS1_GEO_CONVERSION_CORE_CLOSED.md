# Workstream 1: Geo Conversion Core (Unity) — CLOSED

**Milestone 2 — Week of March 2–8, 2026**
**Priority:** Critical path — Workstreams 2 and 3 depend on this
**Estimated effort:** 1–2 days
**Status:** CLOSED — Completed March 2, 2026
**Commit:** `3c50ddf` on `main`

---

## Context

When the dashboard creates a marker at `(33.7760, -84.3950)`, the Unity app receives the event but **ignores the lat/lng entirely**. The `C2Client.ParseMarker()` method (C2Client.cs:177) never reads the `lat`/`lng` fields from the server JSON, and `MarkerData.cs` has no fields to store them. Instead, `AnchorManager.HandleMarkerCreated()` (AnchorManager.cs:40) spawns a yellow cube 2m in front of the camera for any pending marker.

This workstream builds the bridge: a coordinate conversion utility and the plumbing to carry lat/lng from the server JSON all the way through to a Unity world-space position.

**Reference origin:** `GT_CENTER (33.7756, -84.3963)` → Unity `(0, 0, 0)` — matches the dashboard's hard-coded center in `MapView.jsx:14`.

---

## Tasks

### 1A. Create `Assets/IRIS/Scripts/Geo/GeoUtils.cs`

New static utility class in the `IRIS.Geo` namespace.

```csharp
// Flat-earth approximation — valid within ~5km of reference point
// At GT campus latitude, error is <1m across the ~500m working radius

public static Vector3 LatLngToUnityPosition(double lat, double lng, double refLat, double refLng)
{
    float metersPerDegreeLat = 110540f;
    float metersPerDegreeLng = 111320f * Mathf.Cos((float)refLat * Mathf.Deg2Rad);
    float x = (float)(lng - refLng) * metersPerDegreeLng;  // east-west → Unity X
    float z = (float)(lat - refLat) * metersPerDegreeLat;   // north-south → Unity Z
    float y = 1.5f;                                          // default eye-level height
    return new Vector3(x, y, z);
}

public static (double lat, double lng) UnityPositionToLatLng(Vector3 pos, double refLat, double refLng)
{
    float metersPerDegreeLat = 110540f;
    float metersPerDegreeLng = 111320f * Mathf.Cos((float)refLat * Mathf.Deg2Rad);
    double lat = refLat + pos.z / metersPerDegreeLat;
    double lng = refLng + pos.x / metersPerDegreeLng;
    return (lat, lng);
}
```

Both directions are needed: `LatLngToUnity` for dashboard→AR, `UnityToLatLng` for AR→dashboard (task 3D in WS3).

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Geo/GeoUtils.cs` (new)

---

### 1B. Add `lat`/`lng` fields to `MarkerData.cs`

The server already sends `lat` and `lng` on every marker object (confirmed in `Marker.js:9-10`), but `MarkerData.cs` has no fields to receive them.

Add two fields:

```csharp
public double lat;
public double lng;
```

Update the constructor to accept them (default to `0`), or let them be set post-construction by `ParseMarker()`.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Markers/MarkerData.cs`

---

### 1C. Update `C2Client.ParseMarker()` to extract `lat`/`lng`

Currently `ParseMarker()` (C2Client.cs:177-202) reads `id`, `label`, `type`, `status`, `createdAt`, `placedAt`, and `position` — but skips `lat` and `lng`.

Add after line 188:

```csharp
marker.lat = obj["lat"]?.Value<double>() ?? 0;
marker.lng = obj["lng"]?.Value<double>() ?? 0;
```

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/C2Client.cs`

---

### 1D. Update `AnchorManager.HandleMarkerCreated()`

Current logic (AnchorManager.cs:40-64):
- If `status == "placed"` and `position != null` → spawn at known `(x, y, z)` ✓
- Otherwise → spawn 2m in front of camera ✗ (ignores lat/lng)

New logic:
```
if (status == "placed" && position != null):
    spawn at position (x, y, z)          — already works
else if (lat != 0 && lng != 0):
    compute position via GeoUtils         — NEW
    spawn at computed position
    emit marker:place with computed (x, y, z)
else:
    spawn 2m in front of camera           — fallback for markers with no geo data
    emit marker:place
```

This means dashboard-placed markers with lat/lng will immediately appear at the correct geographic position in AR space.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

---

### 1E. Wire `GeoReference` as `[SerializeField]` on `AnchorManager`

Add Inspector-configurable fields:

```csharp
[Header("Geo Reference (Origin Point)")]
[SerializeField] private double referenceLat = 33.7756;
[SerializeField] private double referenceLng = -84.3963;
```

Pass these to all `GeoUtils` calls.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

**Unity Inspector step (manual, cannot be automated):**
> On the GameObject with `AnchorManager`, verify the **Reference Lat** and **Reference Lng** fields are set to `33.7756` and `-84.3963` (GT campus). These are the defaults but should be confirmed after import.

---

## Verification

After completing all tasks, this test should work:

1. Start the server and dashboard
2. Click the dashboard map at a point ~100m north of GT_CENTER
3. In Unity editor Play mode, the marker should spawn at approximately `(0, 1.5, 11)` — about 11 meters on the Z axis (north) from origin
4. The server should receive `marker:place` with the computed `(x, y, z)` and update all clients

---

## Files Modified

| File | Action |
|------|--------|
| `unity/IRIS-AR/Assets/IRIS/Scripts/Geo/GeoUtils.cs` | **Create** |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Markers/MarkerData.cs` | Edit — add `lat`, `lng` fields |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/C2Client.cs` | Edit — parse `lat`/`lng` in `ParseMarker()` |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs` | Edit — geo conversion in `HandleMarkerCreated()`, add reference fields |

---

## Implementation Notes

### Additional files changed (beyond original plan)

| File | Action | Reason |
|------|--------|--------|
| `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/MarkerEventData.cs` | Edit — fields to properties | `System.Text.Json` only serializes properties, not fields |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Core/IRISManager.cs` | Edit — add `Application.runInBackground = true` | Meta XR SDK pauses app on focus loss, blocking socket event dispatch |
| `server/src/socket/markerHandlers.js` | Edit — import config, fix lng validation typo | Pre-existing bugs: missing `config` import, `data.lng != data` instead of `data.lng != null` |
| `dashboard/src/components/MapView.jsx` | Edit — `'generic'` to `'general'` | Dashboard sent a marker type not in the server's allowed list, causing silent rejection |

### Bugs discovered and fixed during implementation

1. **Newtonsoft vs System.Text.Json mismatch** — SocketIOUnity uses `System.Text.Json` internally but `C2Client` was deserializing into Newtonsoft `JObject`/`JArray`. This silently failed for any non-empty payload. Rewrote all JSON handling in `C2Client.cs` to use `System.Text.Json.JsonElement`.

2. **Payload serialization failure** — All emit payload classes (`DeviceRegisterPayload`, `MarkerPlacePayload`, etc.) used public fields. `System.Text.Json` only serializes properties by default, so every emit sent `{}`. Converted all fields to `{ get; set; }` properties in `MarkerEventData.cs`.

3. **Background thread crashes** — Socket callbacks run on `_ThreadPoolWaitCallback` (not the Unity main thread despite `unityThreadScope = Update`). `AnchorManager` called `Instantiate` from these callbacks, which requires the main thread. Exceptions were silently swallowed by SocketIOUnity's `TryInvoke`. Fixed by adding `ConcurrentQueue<T>` buffers drained in `Update()`.

4. **App pausing on alt-tab** — Meta XR SDK calls `OnApplicationPause(true)` when Unity Editor loses focus, halting `Update()` and preventing socket event dispatch. Fixed with `Application.runInBackground = true` in `IRISManager.Awake()`.

5. **Server crash on marker create** — `markerHandlers.js` referenced `config` without importing it. Also had a typo: `data.lng != data` instead of `data.lng != null`.

6. **Silent marker rejection** — Dashboard sent `type: 'generic'` but server only accepts `['waypoint', 'threat', 'objective', 'info', 'general', 'other']`. Server emitted `marker:error` but dashboard had no listener. Changed to `'general'`.

### Key architectural decisions

- **`System.Text.Json` is the standard for this project.** SocketIOUnity uses it internally. All Unity-side JSON parsing and payload serialization must use `System.Text.Json`, not Newtonsoft. Future workstreams must follow this convention.
- **Main-thread dispatch via ConcurrentQueue.** Socket callbacks arrive on background threads. Any code that touches Unity APIs (Instantiate, GetComponent, Camera.main, etc.) must be queued and drained in `Update()`. The pattern established in `AnchorManager` should be reused.
- **Marker color coding:** Cyan = already placed with known position, Green = geo-converted from lat/lng, Yellow = fallback (no geo data, spawned in front of camera).
