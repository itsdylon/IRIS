# IRIS — Demo Milestone

**Goal:** Demonstrate the full IRIS pipeline in a live walkthrough — marker placement, spatial calibration, multi-component sync, and session awareness — all running in the Unity Editor without Quest 3 hardware.

**Baseline:** WS1 (Cesium 3D scene), WS2 (spatial anchor system), and WS3 (session server) are implemented. WS4 (integration/polish) is not started.

---

## What the Demo Should Show

1. **3D geospatial scene** — Cesium terrain + buildings rendering at GT campus
2. **Marker pipeline** — place markers from dashboard, see them in 3D; place from Unity, see on dashboard
3. **Calibration flow** — press C to calibrate, server receives anchor data, confirms round-trip
4. **Session awareness** — dashboard shows active session, connected devices, calibration status
5. **Status HUD** — Unity overlay showing connection, calibration, marker count
6. **Resilience** — server restart → clients reconnect and recover state

---

## Task Dependency Order

```
D1 SessionStatus ──┐
D2 HUD ────────────┤
D3 Auto-session ───┤──→ Demo ready
D4 Error handling ─┤
D5 Reconnect ──────┤
D6 Marker count ───┘
```

All tasks are independent and can be done in any order/parallel. D1 + D2 are highest visual impact. D3 is critical for smooth demo flow. D4/D5 prevent demo failures. D6 is nice-to-have.

### Status

| Task | Status | Notes |
|------|--------|-------|
| D1 SessionStatus | DONE | SessionStatus component, useSession hook, App wiring, CSS |
| D2 HUD | NOT STARTED | |
| D3 Auto-session | DONE | OnDeviceRegistered event + auto-create session after registration |
| D4 Error handling | NOT STARTED | |
| D5 Reconnect | NOT STARTED | |
| D6 Marker count | NOT STARTED | |

### Additional fixes applied

- **Marker placement status**: AnchorManager now calls `EmitMarkerPlace()` after spawning a geo-positioned marker, so the dashboard shows "placed" instead of "Pending placement"
- **C2Client OnDeviceRegistered event**: New event fires after server confirms device registration, used by D3 to guarantee device is registered before creating a session

---

## D1. Dashboard SessionStatus Component

**Priority:** HIGH
**Effort:** ~2 hours
**Component:** Dashboard (React)

### Context

The dashboard currently shows device count (`DeviceStatus.jsx`) in the header but has no session or calibration awareness. The server already broadcasts `session:created`, `session:joined`, `session:state`, and `anchor:shared` events. This task adds a React hook and component to display that data.

### Files to Create

#### `dashboard/src/components/SessionStatus.jsx` (new)

```jsx
export default function SessionStatus({ session }) {
  if (!session.sessionId) {
    return (
      <div className="session-status">
        <span className="status-dot" />
        <span>No Session</span>
      </div>
    )
  }

  return (
    <div className="session-status">
      <span className={`status-dot ${session.isCalibrated ? 'calibrated' : 'awaiting'}`} />
      <div className="session-info">
        <span className="session-id">Session: {session.sessionId.slice(0, 8)}</span>
        <span className="session-detail">
          {session.isCalibrated ? 'Calibrated' : 'Awaiting Calibration'}
        </span>
      </div>
    </div>
  )
}
```

### Files to Edit

#### `dashboard/src/hooks/useSocket.js` — add `useSession()` hook

Append this function after the existing `useDevices()` function:

```js
export function useSession() {
  const [session, setSession] = useState({
    sessionId: null,
    hostDeviceId: null,
    devices: [],
    isCalibrated: false,
  })

  useEffect(() => {
    socket.on('session:created', (data) => {
      setSession((prev) => ({
        ...prev,
        sessionId: data.sessionId,
        hostDeviceId: data.hostDeviceId,
      }))
    })

    socket.on('session:joined', (data) => {
      setSession((prev) => ({
        ...prev,
        devices: [...prev.devices, data.deviceId],
      }))
    })

    socket.on('session:state', (data) => {
      setSession((prev) => ({
        ...prev,
        sessionId: data.sessionId,
        hostDeviceId: data.hostDeviceId,
        devices: data.devices || [],
        isCalibrated: data.calibration != null,
      }))
    })

    socket.on('anchor:shared', () => {
      setSession((prev) => ({ ...prev, isCalibrated: true }))
    })

    return () => {
      socket.off('session:created')
      socket.off('session:joined')
      socket.off('session:state')
      socket.off('anchor:shared')
    }
  }, [])

  return { session }
}
```

#### `dashboard/src/App.jsx` — import and render SessionStatus

Change the imports to:
```jsx
import MapView from './components/MapView'
import MarkerPanel from './components/MarkerPanel'
import DeviceStatus from './components/DeviceStatus'
import SessionStatus from './components/SessionStatus'
import { useMarkers, useDevices, useSession } from './hooks/useSocket'
import './App.css'
```

Add inside `App()`:
```jsx
const { session } = useSession()
```

Add `<SessionStatus session={session} />` inside the header, next to `<DeviceStatus>`:
```jsx
<header className="app-header">
  <h1>IRIS Command Dashboard</h1>
  <div className="header-status">
    <SessionStatus session={session} />
    <DeviceStatus devices={devices} />
  </div>
</header>
```

#### `dashboard/src/App.css` — add session status styles

Append these rules:

```css
.header-status {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.session-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
}

.session-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.session-id {
  font-family: monospace;
  font-size: 0.8rem;
}

.session-detail {
  font-size: 0.7rem;
  color: #8899aa;
}

.status-dot.calibrated {
  background: #44ff44;
}

.status-dot.awaiting {
  background: #ffaa00;
}
```

### Verification

1. Start server + dashboard
2. Open Unity, enter Play mode, press C
3. Dashboard header should show: `Session: xxxxxxxx` with green dot and "Calibrated"
4. Before pressing C: gray dot, "No Session"

---

## D2. Unity Status HUD

**Priority:** HIGH
**Effort:** ~3 hours
**Component:** Unity

### Context

Currently all feedback is in the Unity console. This task adds a screen-space overlay showing connection, calibration, and marker status. The `HUDManager` script reads from `C2Client.IsConnected`, `CalibrationManager.IsCalibrated`, and `AnchorManager.ActiveMarkerCount` (added in D6).

### Files to Create

#### `unity/IRIS-AR/Assets/IRIS/Scripts/UI/HUDManager.cs` (new)

```csharp
using UnityEngine;
using TMPro;
using IRIS.Networking;
using IRIS.Anchors;

namespace IRIS.UI
{
    public class HUDManager : MonoBehaviour
    {
        [SerializeField] private C2Client c2Client;
        [SerializeField] private CalibrationManager calibrationManager;
        [SerializeField] private AnchorManager anchorManager;
        [SerializeField] private TMP_Text statusText;

        private void Update()
        {
            if (statusText == null) return;

            var connColor = c2Client != null && c2Client.IsConnected ? "#44FF44" : "#FF4444";
            var connLabel = c2Client != null && c2Client.IsConnected ? "Connected" : "Disconnected";

            var calColor = calibrationManager != null && calibrationManager.IsCalibrated ? "#44FF44" : "#FFAA00";
            var calLabel = calibrationManager != null && calibrationManager.IsCalibrated ? "Calibrated" : "Not Calibrated";

            var markerCount = anchorManager != null ? anchorManager.ActiveMarkerCount : 0;

            statusText.text = $"<color={connColor}>\u25CF</color> {connLabel}\n" +
                              $"<color={calColor}>\u25CF</color> {calLabel}\n" +
                              $"Markers: {markerCount}";
        }
    }
}
```

**Note:** `\u25CF` is a filled circle character (●) used as a status dot.

### Files to Edit

None — D6 adds `ActiveMarkerCount` to `AnchorManager` separately. If D6 is not done yet, `HUDManager` handles `anchorManager == null` gracefully (shows 0).

### Unity Inspector Steps (Manual)

1. In the Hierarchy, right-click → UI → Canvas
   - Set Canvas **Render Mode** to "Screen Space - Overlay"
   - Set **Sort Order** to 10 (renders above everything)
2. Right-click Canvas → UI → Panel
   - Anchor: top-left (hold Alt and click top-left preset in Rect Transform)
   - Width: 260, Height: 80
   - Set Image color to `(0, 0, 0, 0.6)` (semi-transparent black)
3. Right-click Panel → UI → Text - TextMeshPro
   - If prompted to import TMP Essentials, click "Import"
   - Anchor: stretch-stretch (fill parent)
   - Margins: 10 on all sides
   - Font Size: 16
   - Alignment: top-left
   - Rich Text: enabled (should be default)
   - Set text to placeholder: `● Connected\n● Not Calibrated\nMarkers: 0`
4. Add `HUDManager` component to the **Canvas** GameObject
5. Wire fields:
   - **C2 Client**: drag IRISManager
   - **Calibration Manager**: drag IRISManager
   - **Anchor Manager**: drag IRISManager
   - **Status Text**: drag the TextMeshPro text child

### Verification

1. Enter Play mode → HUD shows red "Disconnected" (if server not running) or green "Connected"
2. Press C → HUD updates to green "Calibrated"
3. Place marker from dashboard → marker count increments

---

## D3. Auto-Create Session on Connect

**Priority:** HIGH
**Effort:** ~1 hour
**Component:** Unity

### Context

Currently, `CalibrationManager.Calibrate()` emits `anchor:share` with `_currentSessionId` which is `null` unless a session was manually created. The server stores anchors under sessions, so `sessionId` should be set. This task makes the Unity client auto-create a session on connect so the demo flow is seamless.

### Files to Edit

#### `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs`

**Change 1:** Subscribe to `OnConnectedEvent` and auto-create session in `Start()`.

Replace the current `Start()` method:
```csharp
private void Start()
{
    _calibrationGroupUuid = Guid.NewGuid();

    if (c2Client != null)
    {
        c2Client.OnSessionCreated += OnSessionCreated;
    }
}
```

With:
```csharp
private void Start()
{
    _calibrationGroupUuid = Guid.NewGuid();

    if (c2Client != null)
    {
        c2Client.OnSessionCreated += OnSessionCreated;
        c2Client.OnConnectedEvent += OnClientConnected;
    }
}

private void OnClientConnected()
{
    if (string.IsNullOrEmpty(_currentSessionId))
    {
        c2Client.EmitSessionCreate();
        Debug.Log("[CalibrationManager] Auto-creating session on connect");
    }
}
```

**Change 2:** Unsubscribe in `OnDestroy()`.

Replace:
```csharp
private void OnDestroy()
{
    if (c2Client != null)
    {
        c2Client.OnSessionCreated -= OnSessionCreated;
    }
}
```

With:
```csharp
private void OnDestroy()
{
    if (c2Client != null)
    {
        c2Client.OnSessionCreated -= OnSessionCreated;
        c2Client.OnConnectedEvent -= OnClientConnected;
    }
}
```

### Verification

1. Start server, enter Unity Play mode
2. Unity console should show: `[CalibrationManager] Auto-creating session on connect` then `[CalibrationManager] Session created: <id>`
3. Server console shows `[session:create]`
4. Press C → `anchor:share` now includes a valid `sessionId`
5. Dashboard SessionStatus (D1) shows the session

---

## D4. Error Handling — Timeouts and Try/Catch

**Priority:** MEDIUM
**Effort:** ~2 hours
**Component:** Unity

### Context

`SimulatedSpatialAnchorProvider` uses `TaskCompletionSource` for `ShareAnchorAsync` and `LoadSharedAnchorsAsync`. If the server never responds (e.g., disconnected), these tasks hang forever, freezing the calibration flow. `CalibrationManager.Calibrate()` is `async void` with no try/catch, so any exception crashes silently. This task adds timeouts and defensive wrapping.

### Files to Edit

#### `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/SimulatedSpatialAnchorProvider.cs`

**Change 1:** Add timeout to `ShareAnchorAsync`. Replace the current `ShareAnchorAsync` method:

```csharp
public async Task<bool> ShareAnchorAsync(string anchorId, Guid groupUuid)
{
    if (!_anchors.TryGetValue(anchorId, out var pose))
    {
        Debug.LogWarning($"[SimulatedAnchor] Anchor {anchorId} not found for sharing");
        return false;
    }

    var tcs = new TaskCompletionSource<bool>();

    void OnShared(AnchorSharedPayload payload)
    {
        if (payload.anchorId == anchorId)
        {
            _c2Client.OnAnchorShared -= OnShared;
            Debug.Log($"[SimulatedAnchor] Anchor {anchorId} shared confirmed");
            tcs.TrySetResult(true);
        }
    }

    _c2Client.OnAnchorShared += OnShared;
    _c2Client.EmitAnchorShare(
        null, anchorId, groupUuid.ToString(),
        pose, 0, 0, 0
    );

    var timeoutTask = Task.Delay(10000);
    var completed = await Task.WhenAny(tcs.Task, timeoutTask);
    if (completed == timeoutTask)
    {
        _c2Client.OnAnchorShared -= OnShared;
        Debug.LogWarning($"[SimulatedAnchor] ShareAnchor {anchorId} timed out after 10s");
        return false;
    }

    return await tcs.Task;
}
```

**Change 2:** Add timeout to `LoadSharedAnchorsAsync`. Replace the current method:

```csharp
public async Task<List<(string id, Pose pose)>> LoadSharedAnchorsAsync(Guid groupUuid)
{
    var tcs = new TaskCompletionSource<List<(string id, Pose pose)>>();

    void OnLoadResponse(List<AnchorSharedPayload> anchors)
    {
        _c2Client.OnAnchorLoadResponse -= OnLoadResponse;
        var result = new List<(string id, Pose pose)>();
        foreach (var a in anchors)
        {
            if (a.pose != null)
            {
                var pos = new Vector3(a.pose.px, a.pose.py, a.pose.pz);
                var rot = new Quaternion(a.pose.rx, a.pose.ry, a.pose.rz, a.pose.rw);
                result.Add((a.anchorId, new Pose(pos, rot)));
            }
        }
        Debug.Log($"[SimulatedAnchor] Loaded {result.Count} shared anchors");
        tcs.TrySetResult(result);
    }

    _c2Client.OnAnchorLoadResponse += OnLoadResponse;
    _c2Client.EmitAnchorLoad(groupUuid.ToString());

    var timeoutTask = Task.Delay(10000);
    var completed = await Task.WhenAny(tcs.Task, timeoutTask);
    if (completed == timeoutTask)
    {
        _c2Client.OnAnchorLoadResponse -= OnLoadResponse;
        Debug.LogWarning($"[SimulatedAnchor] LoadSharedAnchors timed out after 10s");
        return new List<(string id, Pose pose)>();
    }

    return await tcs.Task;
}
```

**Note:** Add `using System.Threading.Tasks;` is already present. No new usings needed.

#### `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs`

Wrap the body of `Calibrate()` in a try/catch. Replace the async body (everything after the null checks) with:

```csharp
public async void Calibrate()
{
    var cam = Camera.main;
    if (cam == null)
    {
        Debug.LogWarning("[CalibrationManager] No main camera found");
        return;
    }

    if (georeference == null)
    {
        Debug.LogWarning("[CalibrationManager] No CesiumGeoreference assigned");
        return;
    }

    try
    {
        var camTransform = cam.transform;
        var pose = new Pose(camTransform.position, camTransform.rotation);

        double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(
            new double3(pose.position.x, pose.position.y, pose.position.z));
        double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);
        double lat = llh.y;
        double lng = llh.x;
        double alt = llh.z;

        Debug.Log($"[CalibrationManager] Calibrating at lat/lng/alt ({lat:F6}, {lng:F6}, {alt:F2})");

        var anchorId = await spatialAnchorManager.CreateAndShareCalibrationAnchor(pose, _calibrationGroupUuid);

        c2Client.EmitAnchorShare(
            _currentSessionId, anchorId, _calibrationGroupUuid.ToString(),
            pose, lat, lng, alt);

        IsCalibrated = true;
        OnCalibrationChanged?.Invoke(true);
        Debug.Log($"[CalibrationManager] Calibration complete — anchor {anchorId}");
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[CalibrationManager] Calibration failed: {ex.Message}");
    }
}
```

#### `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/SpatialAnchorManager.cs`

Add try/catch wrappers and `IsReady` property:

```csharp
public bool IsReady => Provider != null;

public async Task<string> CreateAndShareCalibrationAnchor(Pose pose, Guid groupUuid)
{
    try
    {
        var anchorId = await Provider.CreateAnchorAsync(pose);
        await Provider.SaveAnchorAsync(anchorId);
        await Provider.ShareAnchorAsync(anchorId, groupUuid);
        Debug.Log($"[SpatialAnchorManager] Calibration anchor {anchorId} created and shared");
        return anchorId;
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[SpatialAnchorManager] CreateAndShareCalibrationAnchor failed: {ex.Message}");
        return null;
    }
}

public async Task<Pose?> LoadCalibrationAnchor(Guid groupUuid)
{
    try
    {
        var anchors = await Provider.LoadSharedAnchorsAsync(groupUuid);
        if (anchors.Count > 0)
        {
            Debug.Log($"[SpatialAnchorManager] Loaded calibration anchor {anchors[0].id}");
            return anchors[0].pose;
        }

        Debug.LogWarning("[SpatialAnchorManager] No calibration anchors found");
        return null;
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"[SpatialAnchorManager] LoadCalibrationAnchor failed: {ex.Message}");
        return null;
    }
}
```

### Verification

1. Start Unity without server running → press C → should log "timed out" or "failed" instead of hanging
2. Start server, enter Play mode, press C → normal flow works as before
3. No Unity exceptions or freezes in any failure case

---

## D5. Reconnect Session Recovery

**Priority:** MEDIUM
**Effort:** ~1 hour
**Component:** Unity

### Context

When the server restarts, `C2Client` auto-reconnects and re-requests the marker list (existing behavior). But the session is lost — `_currentSessionId` in `CalibrationManager` still holds the old ID, and the server no longer has that session. This task makes the client re-create a session on reconnect.

### Files to Edit

#### `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs`

The `OnClientConnected` handler added in D3 already handles this correctly: it checks `if (string.IsNullOrEmpty(_currentSessionId))` and creates a session. We just need to **reset the session ID on disconnect** so it gets re-created on reconnect.

Add a disconnect handler. In `Start()`, add:
```csharp
c2Client.OnDisconnectedEvent += OnClientDisconnected;
```

Add the method:
```csharp
private void OnClientDisconnected()
{
    _currentSessionId = null;
    IsCalibrated = false;
    OnCalibrationChanged?.Invoke(false);
    Debug.Log("[CalibrationManager] Disconnected — session and calibration reset");
}
```

In `OnDestroy()`, add:
```csharp
c2Client.OnDisconnectedEvent -= OnClientDisconnected;
```

### Verification

1. Start server + Unity Play mode → session auto-created, press C to calibrate
2. Stop server (Ctrl+C) → Unity console: "Disconnected — session and calibration reset"
3. HUD (D2) shows red "Disconnected", amber "Not Calibrated"
4. Restart server → Unity reconnects, auto-creates new session
5. Press C → calibration works again

---

## D6. Marker Count Tracking

**Priority:** LOW
**Effort:** ~30 min
**Component:** Unity

### Context

`HUDManager` (D2) needs a marker count to display. `AnchorManager` already tracks active markers in `_activeAnchors` dictionary but doesn't expose the count publicly.

### Files to Edit

#### `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

Add a single public property after the field declarations (after line 23 in the current file, after `_pendingDeleted`):

```csharp
public int ActiveMarkerCount => _activeAnchors.Count;
```

That's the entire change. One line.

### Verification

1. Enter Play mode → HUD shows "Markers: 0"
2. Place marker from dashboard → HUD shows "Markers: 1"
3. Delete marker → HUD shows "Markers: 0"

---

## Demo Script

Once all tasks are done, this is the walkthrough:

### Setup
1. Terminal 1: `cd server && npm run dev`
2. Terminal 2: `cd dashboard && npm run dev`
3. Open Unity Editor with MainAR scene, enter Play mode

### Beat 1: Connection (30s)
- Show Unity HUD: green "Connected"
- Show dashboard: device appears in device list, session status shows session ID
- Show server console: `[device:register] Quest3`, `[session:create]`

### Beat 2: Calibration (30s)
- Press **C** in Unity
- Unity HUD updates: green "Calibrated"
- Dashboard session status: green dot, "Calibrated"
- Server console shows `[anchor:share]` logs

### Beat 3: Marker Pipeline — Dashboard to 3D (1 min)
- Click on dashboard map to place a marker at GT campus
- Marker appears on the Leaflet map
- Switch to Unity — marker appears in the 3D Cesium scene at the correct building/location
- HUD marker count increments

### Beat 4: Marker Pipeline — 3D to Dashboard (1 min)
- Press **M** in Unity to place a marker from the camera position
- Switch to dashboard — marker appears on the map at the correct lat/lng
- Show bidirectional real-time sync

### Beat 5: Marker Management (30s)
- Delete a marker from the dashboard
- Marker disappears from Unity in real-time
- HUD marker count decrements

### Beat 6: Resilience (30s)
- Stop the server (Ctrl+C)
- Unity HUD shows red "Disconnected", amber "Not Calibrated"
- Restart the server
- Unity auto-reconnects, HUD returns to green "Connected"
- Auto-creates new session, dashboard shows new session ID

### Talking Points
- Cesium 3D Tiles for geospatial accuracy without custom map data
- ISpatialAnchorProvider abstraction — swap simulated ↔ OVR with one toggle
- Socket.IO for real-time bidirectional sync
- Ready for Quest 3 hardware — just flip `useSimulation = false`

---

## Not in Scope for Demo

These are real features but not needed for the demo walkthrough:

- **Multi-instance testing** (WS4-4B) — requires standalone build, complex setup
- **XR Simulator** (WS4-4A) — adds complexity without visual payoff unless demoing on headset
- **Quest 3 build** (WS4-4F) — no hardware available
- **Terrain height sampling** (WS1.5) — markers at hardcoded altitude works fine
- **Integration tests** (WS3-3E) — behind-the-scenes quality
- **UNITY_SETUP.md / TEST_PROTOCOL.md** (WS4-4E) — documentation
