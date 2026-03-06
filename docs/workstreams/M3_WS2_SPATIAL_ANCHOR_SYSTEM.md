# M3 Workstream 2: Spatial Anchor System

**Milestone 3 — March 6–20, 2026**
**Priority:** HIGH — core milestone feature
**Dependencies:** Partial — see task-level dependency notes
**Estimated effort:** 3–4 days
**Component:** Unity (`unity/IRIS-AR/`), depends on WS3 for server events

---

## Context

We need multiple users to share a coordinate frame so markers appear at the same position for everyone. On Quest 3 hardware, Meta's `OVRSpatialAnchor` handles this. But we have no hardware — so we build an abstraction with a simulated implementation that works entirely through Socket.IO, and a hardware implementation that compiles now and runs later.

The `CalibrationManager` bridges spatial anchors to Cesium: one anchor at a known GPS coordinate aligns the two coordinate systems.

---

## Dependency Map

```
WS2 Task          Depends On
─────────         ──────────
2A Interface      Nothing — start Day 1
2B Simulated      2A + WS3 server events (anchor:share, anchor:load)
2C OVR stub       2A
2D Manager        2A, 2B, 2C
2E Calibration    2D + WS1 (CesiumGeoreference must exist)
2F Verify         2E + WS3 (server running with session events)
```

**Tasks 2A and 2C can start Day 1 in parallel with WS1 and WS3.**
**Task 2B needs WS3 server events to be implemented (or stubbed locally).**
**Task 2E is the convergence point — needs both WS1 (Cesium) and WS3 (server).**

---

## Tasks

### 2A. Define ISpatialAnchorProvider interface

**No dependencies. Start Day 1.**

```csharp
// Assets/IRIS/Scripts/Anchors/ISpatialAnchorProvider.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace IRIS.Anchors
{
    public interface ISpatialAnchorProvider
    {
        /// Create an anchor at the given pose. Returns an anchor ID (UUID string).
        Task<string> CreateAnchorAsync(Pose pose);

        /// Persist the anchor to cloud/storage so it survives app restarts.
        Task<bool> SaveAnchorAsync(string anchorId);

        /// Share the anchor with a group UUID. Other devices in the group can load it.
        Task<bool> ShareAnchorAsync(string anchorId, Guid groupUuid);

        /// Load all shared anchors for a group. Returns list of (anchorId, pose) pairs.
        Task<List<(string id, Pose pose)>> LoadSharedAnchorsAsync(Guid groupUuid);

        /// Delete/erase an anchor.
        Task<bool> EraseAnchorAsync(string anchorId);
    }
}
```

This interface is the key architectural decision. Everything downstream — SpatialAnchorManager, CalibrationManager, the host/client flows — programs against this interface. Swapping simulated ↔ OVR is a one-line toggle.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/ISpatialAnchorProvider.cs` (new)

---

### 2B. Implement SimulatedSpatialAnchorProvider

**Depends on 2A. Also needs WS3 server events (anchor:share, anchor:load, anchor:load:response).**

This provider works entirely through the C2 server with no hardware dependencies.

**Behavior:**
- `CreateAnchorAsync(pose)` — generates a `Guid`, stores `{id, pose}` in a local `Dictionary`. Returns the ID immediately.
- `SaveAnchorAsync(id)` — returns `true` (no-op — anchors are already in memory).
- `ShareAnchorAsync(id, groupUuid)` — emits `anchor:share` to the C2 server with `{ anchorId, groupUuid, pose: {position, rotation} }`. Returns `true` when the server acknowledges.
- `LoadSharedAnchorsAsync(groupUuid)` — emits `anchor:load { groupUuid }` to the server. Waits for `anchor:load:response { anchors: [...] }`. Returns the list of (id, pose) pairs.
- `EraseAnchorAsync(id)` — removes from local dictionary, emits `anchor:erase { anchorId }`.

**Constructor takes a `C2Client` reference** (or a dedicated socket reference) to emit/listen for events.

**Key detail:** The simulated provider needs to serialize `Pose` (position + rotation) as JSON over Socket.IO. Use a simple DTO:
```csharp
[Serializable]
public class PosePayload
{
    public float px, py, pz; // position
    public float rx, ry, rz, rw; // rotation quaternion
}
```

Add `PosePayload` to `MarkerEventData.cs` (or a new `AnchorEventData.cs`).

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/SimulatedSpatialAnchorProvider.cs` (new)

---

### 2C. Implement OVRSpatialAnchorProvider (hardware-ready stub)

**Depends on 2A. No other dependencies — can start Day 1 alongside 2A.**

Wraps Meta's `OVRSpatialAnchor` API behind `ISpatialAnchorProvider`. This compiles now but only runs on Quest 3.

```csharp
public class OVRSpatialAnchorProvider : ISpatialAnchorProvider
{
    private readonly GameObject _anchorPrefab; // must have OVRSpatialAnchor component
    private readonly Dictionary<string, OVRSpatialAnchor> _anchors = new();

    public OVRSpatialAnchorProvider(GameObject anchorPrefab)
    {
        _anchorPrefab = anchorPrefab;
    }

    public async Task<string> CreateAnchorAsync(Pose pose)
    {
        var go = Object.Instantiate(_anchorPrefab, pose.position, pose.rotation);
        var anchor = go.GetComponent<OVRSpatialAnchor>();
        while (!anchor.Created) await Task.Yield();
        var id = anchor.Uuid.ToString();
        _anchors[id] = anchor;
        return id;
    }

    public async Task<bool> SaveAnchorAsync(string anchorId)
    {
        if (!_anchors.TryGetValue(anchorId, out var anchor)) return false;
        var result = await anchor.SaveAnchorAsync();
        return result.Success;
    }

    public async Task<bool> ShareAnchorAsync(string anchorId, Guid groupUuid)
    {
        if (!_anchors.TryGetValue(anchorId, out var anchor)) return false;
        var result = await anchor.ShareAsync(groupUuid);
        return result == OVRSpatialAnchor.OperationResult.Success;
    }

    public async Task<List<(string id, Pose pose)>> LoadSharedAnchorsAsync(Guid groupUuid)
    {
        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
        await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(groupUuid, unboundAnchors);

        var results = new List<(string, Pose)>();
        foreach (var unbound in unboundAnchors)
        {
            if (await unbound.LocalizeAsync())
            {
                var go = Object.Instantiate(_anchorPrefab);
                unbound.BindTo(go.GetComponent<OVRSpatialAnchor>());
                var anchor = go.GetComponent<OVRSpatialAnchor>();
                _anchors[anchor.Uuid.ToString()] = anchor;
                results.Add((anchor.Uuid.ToString(), new Pose(go.transform.position, go.transform.rotation)));
            }
        }
        return results;
    }

    public async Task<bool> EraseAnchorAsync(string anchorId)
    {
        if (!_anchors.TryGetValue(anchorId, out var anchor)) return false;
        var result = await anchor.EraseAnchorAsync();
        _anchors.Remove(anchorId);
        if (anchor.gameObject != null) Object.Destroy(anchor.gameObject);
        return result.Success;
    }
}
```

**Note:** This code uses Meta SDK v71+ group-based sharing (no Oculus user ID needed). Verify the exact API signatures against v85 — they may have changed since the docs.

**Also create** a minimal `SpatialAnchorPrefab`:

**Unity Inspector steps (manual):**
> 1. Create empty GameObject
> 2. Add `OVRSpatialAnchor` component
> 3. Optionally add a small debug visual (wireframe sphere or crosshair)
> 4. Save as `Assets/IRIS/Prefabs/SpatialAnchorPrefab.prefab`

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/OVRSpatialAnchorProvider.cs` (new)

---

### 2D. Create SpatialAnchorManager

**Depends on 2A, 2B, 2C.**

MonoBehaviour that selects the active provider and exposes high-level operations:

```csharp
namespace IRIS.Anchors
{
    public class SpatialAnchorManager : MonoBehaviour
    {
        [SerializeField] private bool useSimulation = true;
        [SerializeField] private C2Client c2Client;
        [SerializeField] private GameObject spatialAnchorPrefab;

        private ISpatialAnchorProvider _provider;

        public ISpatialAnchorProvider Provider => _provider;

        private void Awake()
        {
            _provider = useSimulation
                ? new SimulatedSpatialAnchorProvider(c2Client)
                : new OVRSpatialAnchorProvider(spatialAnchorPrefab);
        }

        /// Host: create, save, and share a calibration anchor in one call.
        public async Task<string> CreateAndShareCalibrationAnchor(Pose pose, Guid groupUuid)
        {
            var id = await _provider.CreateAnchorAsync(pose);
            await _provider.SaveAnchorAsync(id);
            await _provider.ShareAnchorAsync(id, groupUuid);
            return id;
        }

        /// Client: load shared anchors for a group UUID, return the first pose.
        public async Task<Pose?> LoadCalibrationAnchor(Guid groupUuid)
        {
            var anchors = await _provider.LoadSharedAnchorsAsync(groupUuid);
            if (anchors.Count == 0) return null;
            return anchors[0].pose;
        }
    }
}
```

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/SpatialAnchorManager.cs` (new)

**Unity Inspector steps (manual):**
> 1. Add SpatialAnchorManager to the IRIS manager GameObject
> 2. Check "Use Simulation" (default true)
> 3. Drag C2Client into the c2Client field
> 4. Drag SpatialAnchorPrefab into the spatialAnchorPrefab field

---

### 2E. Create CalibrationManager

**Depends on 2D + WS1 (CesiumGeoreference must exist in the scene).**

This is the bridge between spatial anchors and Cesium. It answers: "where does the shared tracking space align with the geospatial world?"

```csharp
namespace IRIS.Anchors
{
    public class CalibrationManager : MonoBehaviour
    {
        [SerializeField] private SpatialAnchorManager spatialAnchorManager;
        [SerializeField] private CesiumGeoreference georeference;
        [SerializeField] private C2Client c2Client;

        public bool IsCalibrated { get; private set; }
        public event Action<bool> OnCalibrationChanged;

        private Guid _sessionGroupUuid;
        private string _calibrationAnchorId;
        private double _calibrationLat, _calibrationLng, _calibrationAlt;

        /// Host calls this: create a calibration anchor at the current position.
        public async void Calibrate()
        {
            var camTransform = Camera.main.transform;
            var pose = new Pose(camTransform.position, camTransform.rotation);

            // Get the GPS coordinate at this Unity position
            var llh = georeference.TransformUnityPositionToLongitudeLatitudeHeight(
                new double3(pose.position.x, pose.position.y, pose.position.z));
            _calibrationLng = llh.x;
            _calibrationLat = llh.y;
            _calibrationAlt = llh.z;

            // Create and share the spatial anchor
            _sessionGroupUuid = Guid.NewGuid();
            _calibrationAnchorId = await spatialAnchorManager
                .CreateAndShareCalibrationAnchor(pose, _sessionGroupUuid);

            // Tell the server about the calibration
            c2Client.EmitAnchorShare(
                _calibrationAnchorId, _sessionGroupUuid,
                pose, _calibrationLat, _calibrationLng, _calibrationAlt);

            IsCalibrated = true;
            OnCalibrationChanged?.Invoke(true);
        }

        /// Client calls this: load the shared calibration anchor and align.
        public async void JoinCalibration(Guid groupUuid, double lat, double lng, double alt)
        {
            var anchorPose = await spatialAnchorManager.LoadCalibrationAnchor(groupUuid);
            if (anchorPose == null) return;

            // Where Cesium thinks this GPS coordinate is in Unity space
            var cesiumPos = georeference.TransformLongitudeLatitudeHeightToUnity(
                new double3(lng, lat, alt));

            // The offset between the spatial anchor pose and the Cesium position
            var offset = anchorPose.Value.position - (Vector3)cesiumPos;

            // Apply the offset to align Cesium's world to the shared tracking space
            // (In simulation mode this offset is ~zero; on real hardware it's non-trivial)
            ApplyCalibrationOffset(offset);

            IsCalibrated = true;
            OnCalibrationChanged?.Invoke(true);
        }

        private void ApplyCalibrationOffset(Vector3 offset)
        {
            // Shift the CesiumGeoreference origin to absorb the offset.
            // This aligns the Cesium world with the shared spatial anchor frame.
            // Implementation depends on how CesiumGeoreference exposes origin mutation.
            // Option A: adjust the georeference lat/lng/height
            // Option B: parent the georeference under an offset transform
            // Test both approaches — Option B is simpler and less likely to break tile loading.
        }
    }
}
```

**Key design note:** `ApplyCalibrationOffset` is the trickiest implementation detail. Moving the `CesiumGeoreference` origin might cause 3D tiles to reload. A safer approach may be to insert a parent transform above the georeference and offset that instead. This needs experimentation.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs` (new)

**Also requires** new methods on `C2Client.cs`:
- `EmitAnchorShare(anchorId, groupUuid, pose, lat, lng, alt)`
- `EmitSessionCreate(sessionId)`
- Listeners for `anchor:shared`, `session:created`, `session:joined`

And new payloads in `MarkerEventData.cs` (or a new `SessionEventData.cs`):
- `AnchorSharePayload`, `SessionCreatePayload`, `PosePayload`

**Files:**
- `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs` (new)
- `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/C2Client.cs` (extend)
- `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/MarkerEventData.cs` (extend, or new SessionEventData.cs)

---

### 2F. Verify calibration flow

**Depends on 2E + WS1 (scene) + WS3 (server events).**

1. Run server + dashboard
2. Open Unity Editor, enter Play mode (Instance A)
3. Press C → calibration anchor created, shared via server
4. Open a standalone build (Instance B), connect to same server
5. Instance B receives calibration data, loads shared anchor, aligns
6. Place a marker from dashboard → both instances show it at the same position
7. Place a marker from Instance A → Instance B and dashboard show it

In simulation mode, the offset is ~zero, so "same position" means the markers are at identical Unity coordinates. The real test is that the full flow executes without errors and the data round-trips correctly.

---

## Files Modified / Created

| File | Action |
|---|---|
| `Assets/IRIS/Scripts/Anchors/ISpatialAnchorProvider.cs` | **Create** — interface |
| `Assets/IRIS/Scripts/Anchors/SimulatedSpatialAnchorProvider.cs` | **Create** — simulation impl |
| `Assets/IRIS/Scripts/Anchors/OVRSpatialAnchorProvider.cs` | **Create** — hardware impl |
| `Assets/IRIS/Scripts/Anchors/SpatialAnchorManager.cs` | **Create** — orchestrator |
| `Assets/IRIS/Scripts/Anchors/CalibrationManager.cs` | **Create** — SSA↔Cesium bridge |
| `Assets/IRIS/Prefabs/SpatialAnchorPrefab.prefab` | **Create** — OVR anchor prefab |
| `Assets/IRIS/Scripts/Networking/C2Client.cs` | Edit — add session/anchor emit methods |
| `Assets/IRIS/Scripts/Networking/MarkerEventData.cs` | Edit — add session/anchor payloads |

---

## Unblocks

Completing this workstream unblocks:
- **WS4 task 4B** (multi-instance testing needs the calibration flow)
- **WS4 task 4C** (error handling targets CalibrationManager + SpatialAnchorManager)
