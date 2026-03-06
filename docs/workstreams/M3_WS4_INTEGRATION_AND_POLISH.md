# M3 Workstream 4: Integration, Testing & Polish

**Milestone 3 — March 6–20, 2026**
**Priority:** MEDIUM — runs in Week 2 after WS1–WS3 converge
**Dependencies:** Depends on WS1, WS2, WS3 — see task-level notes
**Estimated effort:** 3–4 days
**Components:** Unity, Server, Dashboard, Docs

---

## Context

This workstream is where everything comes together. WS1 builds the 3D world, WS2 builds the spatial anchor system, WS3 builds the server events. WS4 validates that they all work as a unified system, adds polish (HUD, error handling), and produces documentation.

**This workstream cannot start until WS1, WS2, and WS3 are substantially complete** (at minimum: Cesium scene renders, SimulatedProvider compiles, server session events work). Individual tasks have specific dependency notes.

---

## Dependency Map

```
WS4 Task               Depends On
─────────               ──────────
4A XR Simulator         WS1 (scene must exist)
4B Multi-instance test  WS1 + WS2 + WS3 (full pipeline)
4C Error handling       WS2 (CalibrationManager, SpatialAnchorManager exist)
4D Status HUD           WS1 (scene), WS2 (CalibrationManager for status)
4E Documentation        All workstreams substantially complete
4F Quest 3 build prep   WS1 (Cesium installed)
```

---

## Tasks

### 4A. Set up Meta XR Simulator

**Depends on WS1 (scene must exist to run in simulator).**

1. Verify `com.meta.xr.simulator` is installed (included in `com.meta.xr.sdk.all` v85)
2. Open the Meta XR Simulator panel in Unity
3. Select a synthetic environment (e.g., "Game Room")
4. Enter Play mode — the simulator activates as the OpenXR runtime
5. Test: WASD + mouse navigation as a simulated headset
6. Test: simulated controller interaction (if needed for marker placement)

**Camera toggle:** The scene needs to switch between DynamicCamera (desktop mode) and OVRCameraRig (XR Simulator mode). Options:
- **Option A:** Two scene profiles (separate GameObjects, toggle active state)
- **Option B:** Runtime detection — check if XR is initialized, activate the appropriate camera
- Recommended: Option B, using `XRGeneralSettings.Instance.Manager.isInitializationComplete`

**File:** No new code files — configuration + possible camera toggle script

---

### 4B. Multi-instance testing

**Depends on WS1 + WS2 + WS3. This is the milestone's integration test.**

**Setup:**
1. Build a standalone player (File → Build Settings → Build)
2. Run the C2 server (`cd server && npm run dev`)
3. Run the dashboard (`cd dashboard && npm run dev`)
4. Open Instance A in Unity Editor → enter Play mode
5. Launch the standalone build as Instance B

**Test protocol:**

| # | Action | Expected |
|---|---|---|
| 1 | Instance A connects | Dashboard shows device in device list |
| 2 | Instance B connects | Dashboard shows second device |
| 3 | Instance A presses C (calibrate) | Server receives `session:create` + `anchor:share`. Dashboard shows "Calibrated" |
| 4 | Instance B auto-joins session | Server receives `session:join`. Instance B loads calibration anchor |
| 5 | Dashboard: click map to place marker | Both instances show marker at same position in 3D scene |
| 6 | Instance A presses M (place marker) | Instance B and dashboard show the marker |
| 7 | Dashboard: delete a marker | Marker disappears from both instances |
| 8 | Stop server, restart | Both instances reconnect, re-request marker list |

**If tests fail:** Debug by checking:
- Server console for event logs
- Unity console for C2Client connection/event logs
- Dashboard for marker/session state
- Network tab for Socket.IO traffic

---

### 4C. Error handling and resilience

**Depends on WS2 (CalibrationManager, SpatialAnchorManager must exist).**

Add graceful handling for failure cases:

**CalibrationManager:**
- If `CreateAndShareCalibrationAnchor` fails, log error and show "Calibration Failed" status
- If `LoadCalibrationAnchor` returns null (no anchors found), retry with backoff (3 attempts, 2s apart)
- If anchor loading times out (>10s), fall back to Cesium-only positioning (no offset, markers still appear but may not be aligned between devices)

**SpatialAnchorManager:**
- If provider throws, catch and log — don't crash the app
- Expose `IsReady` property that other systems can check

**C2Client:**
- On reconnect, re-request session state: `socket.emit('session:join', { sessionId })`
- If host disconnects, server broadcasts `session:host-disconnected` — clients show warning in HUD

**Files:**
- `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs` (edit)
- `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/SpatialAnchorManager.cs` (edit)
- `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/C2Client.cs` (edit)

---

### 4D. Status HUD

**Depends on WS1 (scene), WS2 (CalibrationManager for calibration status).**

Minimal screen-space overlay showing operational status. Use a Unity `Canvas` set to Screen Space — Overlay (simpler than world-space for development).

**Display fields:**
- **Connection:** green dot + "Connected" / red dot + "Disconnected" (from `C2Client.IsConnected`)
- **Session:** session ID (truncated) or "No Session"
- **Calibration:** "Calibrated" (green) / "Not Calibrated" (amber) (from `CalibrationManager.IsCalibrated`)
- **Devices:** count of devices in session
- **Markers:** count of active markers
- **Mode:** "Desktop" / "XR Simulator"

**Layout:** Small semi-transparent panel in the top-left corner. Use TextMeshPro.

```csharp
// Assets/IRIS/Scripts/UI/HUDManager.cs
namespace IRIS.UI
{
    public class HUDManager : MonoBehaviour
    {
        [SerializeField] private C2Client c2Client;
        [SerializeField] private CalibrationManager calibrationManager;
        [SerializeField] private TMP_Text statusText;

        private void Update()
        {
            var conn = c2Client.IsConnected ? "<color=green>Connected</color>" : "<color=red>Disconnected</color>";
            var cal = calibrationManager.IsCalibrated ? "<color=green>Calibrated</color>" : "<color=yellow>Not Calibrated</color>";
            statusText.text = $"{conn}\n{cal}\nMarkers: {markerCount}";
        }
    }
}
```

**Unity Inspector steps (manual):**
> 1. Create a Canvas (Screen Space — Overlay) in the scene
> 2. Add a Panel child (semi-transparent dark background, top-left anchor)
> 3. Add a TextMeshPro text child
> 4. Add HUDManager component to the Canvas
> 5. Wire C2Client, CalibrationManager, and TMP_Text references

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/UI/HUDManager.cs` (new)

---

### 4E. Documentation

**Depends on all workstreams being substantially complete.**

**`docs/UNITY_SETUP.md`** — complete Inspector wiring guide:
- Cesium setup: scoped registry, ion token, CesiumGeoreference origin, tilesets, DynamicCamera
- Prefab configuration: AnchorPrefab (CesiumGlobeAnchor), SpatialAnchorPrefab (OVRSpatialAnchor)
- Component wiring: AnchorManager fields, SpatialAnchorManager fields, CalibrationManager fields, HUDManager fields, DesktopInputManager fields
- OVRManager settings: Shared Spatial Anchor Support, Passthrough, Scene
- XR Simulator setup: how to activate, synthetic environment selection
- Camera toggle: how to switch between Desktop and XR Simulator mode
- Build settings for Quest 3 (when hardware arrives)

**`docs/TEST_PROTOCOL.md`** — step-by-step manual test script:
- Prerequisites (server, dashboard, Unity scene)
- Test Case 1: Desktop mode — single-instance marker pipeline
- Test Case 2: Multi-instance — two instances sharing a session
- Test Case 3: XR Simulator — spatial anchor flow
- Test Case 4: Dashboard — session status and calibration indicator
- Test Case 5: Resilience — server restart, reconnection

**Files:** `docs/UNITY_SETUP.md` (new), `docs/TEST_PROTOCOL.md` (new)

---

### 4F. Quest 3 build preparation

**Depends on WS1 (Cesium installed).**

Set up Android build config so the project is ready when hardware arrives. **Do not flip `useSimulation = false` yet.**

**Player Settings:**
- Target: Android ARM64
- Graphics API: **OpenGLES3** (Vulkan has Cesium left-eye bug, issue #388)
- Internet Access: **Require** (Cesium streams tiles from cloud — silent fail without this)
- Scripting Backend: IL2CPP
- Minimum API Level: Android 10 (API 29)

**XR Plugin Management:**
- Enable OpenXR + Meta Quest Feature

**OVRManager (on OVRCameraRig):**
- Shared Spatial Anchor Support: **Required**
- Passthrough Support: **Required**
- Scene Support: **Required**

**Cesium 3D Tileset performance overrides for Quest:**
- `maximumScreenSpaceError`: **32–64** (lower detail)
- `maximumSimultaneousTileLoads`: **3–5** (prevent frame spikes)
- `maximumCachedBytes`: **128–256 MB** (prevent OOM crashes)
- `preloadAncestors`: **false**
- `preloadSiblings`: **false**

Document all of this in `docs/UNITY_SETUP.md` (task 4E).

---

## Files Modified / Created

| File | Action |
|---|---|
| `Assets/IRIS/Scripts/UI/HUDManager.cs` | **Create** — status HUD |
| `Assets/IRIS/Scripts/Anchors/CalibrationManager.cs` | Edit — error handling |
| `Assets/IRIS/Scripts/Anchors/SpatialAnchorManager.cs` | Edit — error handling |
| `Assets/IRIS/Scripts/Networking/C2Client.cs` | Edit — reconnect session logic |
| `docs/UNITY_SETUP.md` | **Create** — Inspector setup guide |
| `docs/TEST_PROTOCOL.md` | **Create** — manual test script |

---

## This Workstream Produces

- Validated end-to-end system (multi-instance test passes)
- User-facing status HUD
- Resilient error handling
- Complete documentation for onboarding
- Quest 3 build configuration (ready for hardware)
