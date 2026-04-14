# Field Demo — Validation Guide

How to verify each workstream's code changes are working correctly.

---

## WS1: GPS-to-Local Coordinate Bridge

### What Changed
- `IRISManager.cs` — added `IsPassthroughMode` static property (true on Quest, false in Editor)
- `CalibrationManager.cs` — added `CalibrationLat`, `CalibrationLng`, `CalibrationUnityPosition`, `HasFieldCalibration` properties
- `AnchorManager.cs` — added passthrough code paths for marker create/update/delete using `GeoUtils` instead of Cesium

### Unity Inspector Setup (Manual)
1. Select the **IRISManager** GameObject in the Hierarchy
2. On the **AnchorManager** component, find the new **Calibration Manager** field
3. Drag the **IRISManager** GameObject (which has `CalibrationManager`) into this field

### Test 1: Editor Regression (Cesium Mode Still Works)
**Goal:** Confirm existing Cesium marker placement is not broken.

1. Open the Unity project and enter Play mode in the Editor
2. Open the Console window (Window > General > Console)
3. Start the server (`cd server && npm run dev`) and dashboard (`cd dashboard && npm run dev`)
4. Place a marker from the dashboard at any GT campus lat/lng
5. **Expected:**
   - Console logs: `[AnchorManager] Spawned geo marker '...' at lat/lng ...` (the Cesium path)
   - No logs mentioning "passthrough" or "GeoUtils"
   - `IRISManager.IsPassthroughMode` is `false` (check via Console: add a temporary `Debug.Log` in `Start()` or use the debugger)
   - Markers appear at the correct Cesium globe position, same as before

### Test 2: GeoUtils Accuracy (Simulated Passthrough in Editor)
**Goal:** Verify the GeoUtils math produces correct local positions.

1. Temporarily force passthrough mode in the Editor. In `IRISManager.ConfigureRuntimeCameraRig()`, add this line at the top:
   ```csharp
   IsPassthroughMode = true; // TEMP: remove after testing
   ```
2. Temporarily hardcode calibration data in `CalibrationManager.Start()`:
   ```csharp
   // TEMP: simulate field calibration at GT campus center
   CalibrationLat = 33.7756;
   CalibrationLng = -84.3963;
   CalibrationUnityPosition = Vector3.zero;
   IsCalibrated = true;
   ```
   (You'll need to temporarily make the setters `internal` or add a test method, since they're `private set`.)
3. Enter Play mode
4. From the dashboard, place a marker at (33.7761, -84.3963) — approximately 55m north of the calibration point
5. **Expected:**
   - Console log: `[AnchorManager] Spawned passthrough marker '...' at (0.0, 2.0, 55.3) — offset from calibration`
   - The Z value should be approximately 55 (meters north)
   - The X value should be approximately 0 (same longitude)
   - The Y value should equal `markerHeightOffset` (default 2.0)
6. Place another marker at (33.7756, -84.3958) — approximately 46m east
7. **Expected:**
   - X value approximately 46, Z approximately 0

> **Reminder:** Remove the temporary test code before committing or building.

### Test 3: Reverse GPS Conversion (Controller Marker in Passthrough)
**Goal:** Verify markers placed from the Quest controller convert back to correct GPS.

1. With the same temporary passthrough setup from Test 2
2. In Play mode, press the A button (or call `PlaceMarkerAtCamera()`)
3. **Expected:**
   - Console log: `[AnchorManager] Emitting passthrough marker:create at lat/lng (...)`
   - The lat/lng should be close to the calibration point (33.7756, -84.3963) plus the camera's offset in meters converted back to degrees
   - The marker should appear on the dashboard's Leaflet map at the corresponding GPS location

### Test 4: Quest Build Smoke Test
**Goal:** Confirm the APK builds and the mode detection works on hardware.

1. Remove all temporary test code
2. Build an APK and deploy to Quest 3
3. Launch IRIS
4. **Expected:**
   - `IRISManager.IsPassthroughMode` is `true`
   - ADB logcat (`adb logcat -s Unity`) shows no errors related to the new code
   - Placing markers from the dashboard logs: `[AnchorManager] Cannot place marker '...' — no field calibration. Calibrate first.`
   - This is correct behavior — markers will only appear after calibration is implemented in WS3

### What "Broken" Looks Like
- Markers placed from the dashboard don't appear in the Editor at all → check Console for errors
- Markers appear but at wrong positions in the Cesium view → regression in `HandleMarkerCreatedCesium`
- `NullReferenceException` mentioning `calibrationManager` → Inspector wiring step was missed (see Manual Setup above)
- `GeoUtils` positions are wildly off (100s of meters wrong) → check that `CalibrationLat`/`CalibrationLng` are set correctly and not swapped

---

## WS2: Disable Cesium in Passthrough Mode

### What Changed
- `IRISManager.cs` — extended `ConfigureRuntimeCameraRig()` to disable all `Cesium3DTileset` GameObjects and the `TerrainHeightSampler` when running on Quest

### Unity Inspector Setup (Manual)
None — all changes are code-only. Cesium objects remain in the scene for Editor/sim use and are only disabled at runtime on Quest.

### Pre-check: Inspector Default
Before testing, confirm in the Inspector that **IRISManager > Disable Terrain Lift On Android** is checked (true). This is the default and ensures the terrain alignment coroutine is skipped on Quest.

### Test 1: Editor Regression (Cesium Still Renders)
**Goal:** Confirm Cesium tilesets are NOT disabled in Editor play mode.

1. Enter Play mode in the Editor
2. **Expected:**
   - Cesium OSM Buildings and Terrain render normally (3D buildings visible)
   - Console does NOT log any `Disabled Cesium tileset` messages
   - Console does NOT log `Disabled TerrainHeightSampler`
   - `IRISManager.IsPassthroughMode` is `false`
   - Marker placement still works via Cesium path (same as WS1 Test 1)

### Test 2: Quest Build (Cesium Disabled, Passthrough Visible)
**Goal:** Confirm Cesium is disabled and passthrough is the only background.

1. Build APK and deploy to Quest 3
2. Launch IRIS
3. Check ADB logcat: `adb logcat -s Unity`
4. **Expected log lines (in order):**
   ```
   [IRISManager] Enabled OVRCameraRig for VR runtime
   [IRISManager] Disabled FlyCamera for VR runtime
   [IRISManager] Disabled Cesium tileset: Cesium OSM Buildings
   [IRISManager] Disabled Cesium tileset: Terrain
   [IRISManager] Disabled TerrainHeightSampler for passthrough mode
   ```
5. **Expected visuals:**
   - You see the real world through passthrough — no 3D buildings or terrain overlay
   - No opaque geometry blocking the camera feed
   - The CesiumGeoreference object is still active (harmless, no visual impact)

### Test 3: Combined WS1+WS2 Smoke Test on Quest
**Goal:** Verify both workstreams work together.

1. With WS1+WS2 code on the Quest
2. Start server and dashboard on laptop (same Wi-Fi)
3. Place a marker from the dashboard
4. **Expected:**
   - Logcat shows: `Cannot place marker '...' — no field calibration. Calibrate first.`
   - No crashes, no NullReferenceExceptions
   - Passthrough is visible, no Cesium geometry

### What "Broken" Looks Like
- 3D buildings or terrain visible on Quest → `FindObjectsOfType<Cesium3DTileset>()` didn't find them; check that the tilesets are active in the scene before `ConfigureRuntimeCameraRig()` runs
- Cesium disappears in Editor play mode → `IsPassthroughMode` is incorrectly `true` in Editor; check `_isVrRuntime` logic
- `NullReferenceException` in `TerrainHeightSampler` → the sampler wasn't disabled; confirm the `FindObjectOfType` call matches the class name
- Black screen on Quest (no passthrough) → OVRCameraRig passthrough settings issue, not related to this workstream

---

## WS3: Field Calibration Flow

### What Changed
- `CalibrationManager.cs` — added `fieldCalibrationLat`/`fieldCalibrationLng` serialized fields (default GT campus), `Calibrate()` now branches on `IsPassthroughMode` to use hardcoded GPS + Quest tracking position instead of Cesium ECEF, `JoinCalibration()` updated similarly

### Unity Inspector Setup (Manual)
1. Select the **IRISManager** GameObject in the Hierarchy
2. On the **CalibrationManager** component, find the new **Field Calibration** section:
   - **Field Calibration Lat**: default `33.7756` (GT campus)
   - **Field Calibration Lng**: default `-84.3963` (GT campus)
3. Before building an APK for the field, update these to the **exact GPS coordinates** of the physical calibration point (use a phone GPS app to read them on-site)

### Test 1: Editor Regression (Cesium Calibration Still Works)
**Goal:** Confirm existing Cesium calibration path is unchanged.

1. Start server + dashboard
2. Enter Play mode in the Editor
3. Press C to trigger calibration
4. **Expected:**
   - Console log: `[CalibrationManager] Calibrating at lat/lng/alt (...)` with Cesium-derived coordinates (lat ~33.77, lng ~-84.39, alt ~255)
   - No log mentioning "Field calibration"
   - `IsCalibrated` becomes true
   - Markers placed from dashboard still appear via Cesium path

### Test 2: Simulated Passthrough Calibration in Editor
**Goal:** Verify the passthrough calibration path stores GPS + Unity position correctly.

1. Temporarily force passthrough mode — add at the top of `IRISManager.ConfigureRuntimeCameraRig()`:
   ```csharp
   IsPassthroughMode = true; // TEMP: remove after testing
   ```
2. Enter Play mode
3. Press C to calibrate
4. **Expected:**
   - Console log: `[CalibrationManager] Field calibration at GPS (33.775600, -84.396300), Unity pos (...)`
   - Console log: `[CalibrationManager] Calibrating at lat/lng/alt (33.775600, -84.396300, 0.00)`
   - `CalibrationManager.HasFieldCalibration` is now true
5. Place a marker from the dashboard at a GPS point near the calibration coords
6. **Expected:**
   - Console log: `[AnchorManager] Spawned passthrough marker '...'` (NOT "Cannot place marker" — calibration is now done)
   - Marker appears in the scene at a GeoUtils-derived position

### Test 3: End-to-End WS1+WS2+WS3 in Editor
**Goal:** Full passthrough pipeline works from calibration through marker placement.

1. Keep the temporary `IsPassthroughMode = true` from Test 2
2. Enter Play mode, press C to calibrate
3. Place a marker from the dashboard at (33.7761, -84.3963) — ~55m north
4. **Expected:**
   - Marker spawns at approximately (0, 2, 55) in Unity coordinates
5. Press A (or use `PlaceMarkerAtCamera()`) to place a marker from the Quest
6. **Expected:**
   - Console log: `[AnchorManager] Emitting passthrough marker:create at lat/lng (...)` — lat/lng close to 33.7756, -84.3963 plus camera offset
   - Marker appears on dashboard map at the corresponding GPS position

> **Reminder:** Remove the temporary `IsPassthroughMode = true` line before committing or building.

### Test 4: Quest Build
**Goal:** Full calibration + marker pipeline on Quest hardware.

1. Remove temporary test code
2. Verify Inspector values: `fieldCalibrationLat = 33.7756`, `fieldCalibrationLng = -84.3963`
3. Build APK and deploy to Quest
4. Start server + dashboard on laptop (same Wi-Fi)
5. Launch IRIS on Quest
6. Before calibrating — place a marker from the dashboard
7. **Expected:** Logcat shows `Cannot place marker '...' — no field calibration. Calibrate first.`
8. Press calibration button on Quest
9. **Expected:** Logcat shows `Field calibration at GPS (33.775600, -84.396300)...`
10. Place another marker from the dashboard
11. **Expected:** Logcat shows `Spawned passthrough marker '...'` — marker appears in AR view

### What "Broken" Looks Like
- `[CalibrationManager] Calibration failed: ...` in Console → exception in `Calibrate()`; check the error message
- Calibration works but markers still say "no field calibration" → `CalibrationLat`/`CalibrationLng` not being set; check that the passthrough branch in `Calibrate()` runs
- GPS coordinates in log are (0, 0) → `fieldCalibrationLat`/`fieldCalibrationLng` Inspector fields reverted to 0; check the Inspector
- Editor calibration uses wrong path (field instead of Cesium) → `IsPassthroughMode` is stuck true; check for leftover temp code

---

## WS4: Network Configuration for Field

### What Changed
No code changes. This workstream is purely configuration: setting the server URL on the Quest build, connecting devices to the hotspot, and verifying connectivity.

### Pre-Field Checklist

Run through this checklist indoors before going to the GT green.

#### Step 1: Start Hotspot and Find Laptop IP
1. Enable hotspot on your phone (or dedicated hotspot device)
2. Connect laptop to hotspot Wi-Fi
3. Find the laptop's hotspot IP:
   - **Windows:** `ipconfig` in terminal — look for the Wi-Fi adapter's IPv4 (e.g., `192.168.43.100`)
   - **macOS:** `ifconfig en0` or System Preferences > Network
4. Write down this IP

#### Step 2: Set Server URL in Unity Inspector
1. Open `MainAR.unity` in the Unity Editor
2. Select the **IRISManager** GameObject
3. On the **C2Client** component, change **Server Url** to `http://<laptop-ip>:3000`
   - Example: `http://192.168.43.100:3000`
   - No HTTPS, no trailing slash
4. **Remember to change this back** to `http://localhost:3000` after the field demo

#### Step 3: Build APK
1. Build the APK with the updated server URL
2. Deploy to all Quest devices

### Connectivity Validation

#### Test 1: Server Reachable from Hotspot
1. Start the server: `cd server && npm run dev`
2. From the laptop, verify:
   ```bash
   curl http://<laptop-ip>:3000/health
   ```
3. **Expected:** `{"status":"ok","uptime":...}`
4. **If it fails:** Check Windows Firewall — add an inbound rule allowing port 3000, or temporarily disable the firewall

#### Test 2: Quest Connects to Server
1. Connect Quest to the same hotspot Wi-Fi (Settings > Wi-Fi on Quest)
2. Launch IRIS on the Quest
3. Check server terminal for: `[device:register]` log entry
4. Check ADB logcat: `adb logcat -s Unity | findstr C2Client`
5. **Expected:** `[C2Client] Connected to C2 server`

#### Test 3: Dashboard Accessible
1. Start dashboard: `cd dashboard && npm run dev`
2. Open `http://localhost:5173` in laptop browser
3. **Expected:** Dashboard loads, shows connected device(s) in the device list
4. **(Optional)** To access dashboard from a phone/tablet on the hotspot:
   - Run `cd dashboard && npm run dev -- --host` instead
   - Open `http://<laptop-ip>:5173` from the other device

#### Test 4: End-to-End Marker Round-Trip
1. With server, dashboard, and Quest all connected
2. Place a marker from the dashboard
3. **Expected:** Quest logcat shows the marker event (either placed or "no calibration" if WS3 not yet calibrated)
4. Press A button on Quest controller to place a marker
5. **Expected:** Marker appears on the dashboard's Leaflet map

### What "Broken" Looks Like
- Quest logcat shows `[C2Client] Connection failed` or no C2Client logs at all → server URL is wrong in the Inspector; check the IP matches the laptop's hotspot IP
- Server shows no device:register → Quest isn't on the same Wi-Fi network, or firewall is blocking port 3000
- `curl` to laptop IP fails → firewall issue; add inbound rule for port 3000
- Dashboard shows "0 devices" but server logs show connection → dashboard and server may be on different ports; verify both are running
- IP changed after hotspot restart → DHCP assigned a new IP; re-check with `ipconfig` and rebuild APK if needed

### Tips
- **ADB over Wi-Fi** (for debugging without a cable in the field): `adb tcpip 5555` then `adb connect <quest-ip>:5555`, then `adb logcat -s Unity`
- **Static IP** if your hotspot supports it: assign a fixed IP to the laptop to avoid IP changes between sessions
- Prefer **5GHz Wi-Fi** on the Quest for lower latency

---

## WS5: Quest Passthrough Field Test

_Validation instructions will be added when WS5 is implemented._

---

## WS6: AR Marker Visuals (Optional)

_Validation instructions will be added when WS6 is implemented._
