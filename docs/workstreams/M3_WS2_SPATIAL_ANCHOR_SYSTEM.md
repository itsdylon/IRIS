# M3 Workstream 2: Spatial Anchor System

**Milestone 3 — March 6–20, 2026**
**Priority:** HIGH — core milestone feature
**Status:** IMPLEMENTED — calibration flow verified in editor
**Component:** Unity (`unity/IRIS-AR/`), depends on WS3 for server events

---

## Context

We need multiple users to share a coordinate frame so markers appear at the same position for everyone. On Quest 3 hardware, Meta's `OVRSpatialAnchor` handles this. But we have no hardware — so we build an abstraction with a simulated implementation that works entirely through Socket.IO, and a hardware implementation that compiles now and runs later.

The `CalibrationManager` bridges spatial anchors to Cesium: one anchor at a known GPS coordinate aligns the two coordinate systems.

---

## Implementation Summary

All tasks (2A–2E) are implemented. The calibration flow has been verified end-to-end:
- Press C in editor → SimulatedSpatialAnchorProvider creates anchor → emits `anchor:share` → server receives and broadcasts → `anchor:shared` round-trips back → CalibrationManager emits GPS-tagged `anchor:share`.

### Files Created

| File | Purpose |
|---|---|
| `Assets/IRIS/Scripts/Networking/AnchorEventData.cs` | Payload DTOs (`PosePayload`, `AnchorSharePayload`, `AnchorSharedPayload`, `AnchorLoadPayload`, `AnchorLoadResponsePayload`, `SessionCreatePayload`, `SessionCreatedPayload`, `SessionJoinPayload`, `SessionStatePayload`, `SessionJoinedPayload`) |
| `Assets/IRIS/Scripts/Anchors/ISpatialAnchorProvider.cs` | Interface with 5 async methods |
| `Assets/IRIS/Scripts/Anchors/SimulatedSpatialAnchorProvider.cs` | Socket.IO-based simulation using `TaskCompletionSource` for request/response flows |
| `Assets/IRIS/Scripts/Anchors/OVRSpatialAnchorProvider.cs` | Quest 3 hardware wrapper (compiles, only runs on device) |
| `Assets/IRIS/Scripts/Anchors/SpatialAnchorManager.cs` | Provider orchestrator with `useSimulation` toggle |
| `Assets/IRIS/Scripts/Anchors/CalibrationManager.cs` | SSA↔Cesium bridge using ECEF conversion and parent-transform offset |

### Files Modified

| File | Changes |
|---|---|
| `Assets/IRIS/Scripts/Networking/C2Client.cs` | Added 6 C# events, 5 emit methods, 6 socket listeners, `ParsePose` helper |
| `Assets/IRIS/Scripts/Core/DesktopInputManager.cs` | Added `CalibrationManager` field and C key → `Calibrate()` binding |

---

## Unity Inspector Setup

> **IMPORTANT:** All `[SerializeField]` references point to the **IRISManager** GameObject. When Unity shows the object picker, it filters to GameObjects that have the required component — so "IRISManager" is the correct (and only) choice for `C2Client`, `SpatialAnchorManager`, etc.

### Step 1: SpatialAnchorManager
1. Select **IRISManager** GameObject in the Hierarchy
2. Add Component → `SpatialAnchorManager`
3. **Use Simulation**: check (default, leave enabled for editor testing)
4. **C2 Client**: drag **IRISManager** → the field (it has the `C2Client` component)
5. **Spatial Anchor Prefab**: set to **None** for simulation mode (see gotcha below)

### Step 2: CalibrationManager
1. On the same **IRISManager** GameObject, Add Component → `CalibrationManager`
2. **Spatial Anchor Manager**: drag **IRISManager** → the field
3. **Georeference**: drag the **CesiumGeoreference** GameObject → the field
4. **C2 Client**: drag **IRISManager** → the field

### Step 3: DesktopInputManager
1. On **IRISManager**, find the existing **DesktopInputManager** component
2. **Calibration Manager**: drag **IRISManager** → the new field

---

## Known Gotchas

### OVRSpatialAnchor error in Editor
If the `SpatialAnchorPrefab` has an `OVRSpatialAnchor` component, Unity will log:
```
[OVRSpatialAnchor] OVRPlugin.CreateSpatialAnchor failed. Destroying OVRSpatialAnchor component.
```
This happens because `OVRSpatialAnchor.Start()` tries to create a hardware anchor, which fails without Quest 3 runtime. **Fix:** Set the `Spatial Anchor Prefab` field to **None** on `SpatialAnchorManager` when running in simulation mode. The `OVRSpatialAnchorProvider` adds the component at runtime — it doesn't need to be on the prefab in advance.

**Do NOT add `OVRSpatialAnchor` to the marker prefab** used by `AnchorManager`. That prefab is for rendering markers, not spatial anchors. They are separate prefabs. Mixing them up will cause the OVR error on every marker spawn.

### Marker altitude
The `AnchorManager` component has a **Marker Altitude** field (default 2m). If markers appear underground or invisible, increase this value. At the GT campus reference point, ~254m works correctly. This is terrain-dependent.

### anchor:share emitted twice
When pressing C, the server logs two `[anchor:share]` events. This is by design:
1. First emit: from `SimulatedSpatialAnchorProvider.ShareAnchorAsync()` — shares the pose through the provider abstraction (no GPS data)
2. Second emit: from `CalibrationManager.Calibrate()` — shares with GPS calibration coordinates (`calibrationLat`, `calibrationLng`, `calibrationAlt`)

The second emit is the one that matters for calibration — it includes the GPS tie-point.

---

## Tasks (Reference)

### 2A. ISpatialAnchorProvider interface

Five async methods:
- `CreateAnchorAsync(Pose)` → `Task<string>`
- `SaveAnchorAsync(string)` → `Task<bool>`
- `ShareAnchorAsync(string, Guid)` → `Task<bool>`
- `LoadSharedAnchorsAsync(Guid)` → `Task<List<(string id, Pose pose)>>`
- `EraseAnchorAsync(string)` → `Task<bool>`

### 2B. SimulatedSpatialAnchorProvider

Constructor takes `C2Client`. Uses `TaskCompletionSource<T>` for request/response flows:
- `ShareAnchorAsync` subscribes to `C2Client.OnAnchorShared`, emits `anchor:share`, waits for callback
- `LoadSharedAnchorsAsync` subscribes to `C2Client.OnAnchorLoadResponse`, emits `anchor:load`, waits for response

### 2C. OVRSpatialAnchorProvider

Wraps Meta's `OVRSpatialAnchor` API. Uses callback-based `TaskCompletionSource` pattern for `Save`, `Share`, `LoadUnboundAnchors`, and `Erase`. Only runs on Quest 3 hardware.

### 2D. SpatialAnchorManager

MonoBehaviour with `[SerializeField] bool useSimulation = true`. Selects provider in `Awake()`. Exposes:
- `CreateAndShareCalibrationAnchor(pose, groupUuid)` — chains create → save → share
- `LoadCalibrationAnchor(groupUuid)` — loads shared anchors, returns first pose

### 2E. CalibrationManager

Uses the correct Cesium ECEF conversion pattern (from `AnchorManager.cs`):
```csharp
// Unity → GPS (for host calibration)
double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(...);
double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);

// GPS → Unity (for client join)
double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(lng, lat, alt));
double3 unity = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
```

**Note:** The WS2 design doc referenced `TransformUnityPositionToLongitudeLatitudeHeight` which does NOT exist in CesiumForUnity. Always use the two-step ECEF conversion above.

`ApplyCalibrationOffset` uses the parent-transform approach: inserts a `CalibrationOffset` GameObject above `CesiumGeoreference` to avoid breaking tile loading.

### 2F. Verification

Verified flow:
1. Start server: `cd server && npm run dev`
2. Open Unity Editor, enter Play mode
3. Press C → console shows full calibration chain
4. Server console shows `[anchor:share]` logs
5. No compile errors

**Not yet tested:** Multi-instance join flow (`JoinCalibration`). This requires WS4 integration work to wire up automatic session discovery and join triggers.

---

## What's Left for WS4

- Multi-instance testing (4B): build standalone player, test two-instance calibration
- Wire up `JoinCalibration` trigger: currently no UI/automation for client-side join
- Session discovery: clients need a way to find and join existing sessions
- Error handling (4C): timeout on `TaskCompletionSource`, retry logic, disconnection recovery

---

## Unblocks

Completing this workstream unblocks:
- **WS4 task 4B** (multi-instance testing needs the calibration flow)
- **WS4 task 4C** (error handling targets CalibrationManager + SpatialAnchorManager)
