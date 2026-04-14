# FD Workstream 5: Quest Passthrough Field Test

**Field Demo Milestone**
**Priority:** HIGH — integration test, validates all other workstreams
**Dependencies:** WS1 (GPS bridge) + WS2 (disable Cesium) + WS3 (calibration) + WS4 (network)
**Estimated effort:** ~1 hour
**Component:** Cross-cutting (Unity build + server + dashboard)

---

## Context

This workstream is the integration test. All prior workstreams (WS1-WS4) are code/config changes. This workstream validates that everything works together on real Quest 3 hardware with passthrough, outdoors on the GT green.

---

## Pre-Flight Checklist (Indoor, Before Going Outside)

### Build the APK

1. Open Unity Editor with `MainAR.unity`
2. Verify Inspector settings:
   - **C2Client.serverUrl:** `http://<laptop-hotspot-ip>:3000`
   - **CalibrationManager.fieldCalibrationLat:** Your calibration point latitude
   - **CalibrationManager.fieldCalibrationLng:** Your calibration point longitude
3. File → Build Settings:
   - Platform: Android
   - Texture Compression: ASTC
   - Run Device: (select your Quest if connected via USB)
4. Build and deploy APK to all Quest devices
5. Verify APK launches on Quest — should show passthrough (real world), not Cesium tiles

### Test Connectivity (Indoor)

1. Start phone hotspot
2. Connect laptop + all Quests to hotspot
3. Start server: `cd server && npm run dev`
4. Start dashboard: `cd dashboard && npm run dev`
5. Launch IRIS on each Quest
6. Verify server console shows `[device:register]` for each device
7. Verify dashboard shows devices in the device list

---

## Field Test Protocol

### Setup (5 min)

1. Go to the GT green
2. Start hotspot, connect all devices
3. Start server + dashboard on laptop
4. Identify your calibration point — the physical spot matching the GPS coordinates you hardcoded in WS3

### Test 1: Single Device Calibration (5 min)

1. Put on Quest A
2. Walk to the calibration point
3. Stand still, trigger calibration (Button.One on right controller)
4. Verify console/HUD shows "Calibrated"
5. Verify dashboard shows session + calibration status

**Pass criteria:** Calibration completes without errors.

### Test 2: Dashboard-to-AR Markers (10 min)

1. On the dashboard, click the map to place a marker on the GT green
   - Place it at a recognizable landmark ~20-50m from calibration point (e.g., a bench, tree, building corner)
2. Put on Quest A
3. Look toward the landmark — a marker should be floating in that direction

**Pass criteria:** Marker appears in approximately the correct direction and distance from calibration point. Accuracy within ~5m is acceptable for the demo.

**If marker is wildly wrong:**
- Check that `fieldCalibrationLat/Lng` matches where you actually stood during calibration
- Check that the dashboard marker's lat/lng is correct (hover over it on the map)
- Try recalibrating — stand precisely on the calibration point

### Test 3: AR-to-Dashboard Markers (10 min)

1. While wearing Quest A, point at a recognizable object ~10m away
2. Press Button.One to place a marker
3. Check the dashboard — the marker should appear on the map near that object's real-world position

**Pass criteria:** Marker appears on the dashboard map within ~5-10m of the object's actual GPS position.

### Test 4: Multi-Device Alignment (10 min)

1. Calibrate Quest B from the **same calibration point** as Quest A
2. On the dashboard, place a marker
3. Both Quest A and Quest B should see the marker in approximately the same real-world position

**Pass criteria:** Both devices show the marker in roughly the same direction. Perfect alignment is not expected due to GPS and tracking differences.

### Test 5: Marker Lifecycle (5 min)

1. Place a marker from the dashboard
2. Verify it appears on both Quests
3. Delete the marker from the dashboard
4. Verify it disappears from both Quests

**Pass criteria:** Create and delete propagate to all devices in real-time.

### Test 6: Server Resilience (5 min)

1. Stop the server on the laptop (Ctrl+C)
2. Verify Quest HUD shows "Disconnected"
3. Restart the server
4. Verify Quests reconnect, session auto-creates, markers reload

**Pass criteria:** Recovery is automatic without restarting the Quest app.

---

## Known Limitations for Demo

| Limitation | Impact | Workaround |
|-----------|--------|------------|
| GPS accuracy is 3-5m | Markers won't be pixel-perfect on real objects | Acceptable for demo — explain this is expected |
| Quest has no GPS | Can't auto-detect position | Hardcode calibration point in Inspector |
| Markers float at fixed height | No ground plane detection | Set `markerHeightOffset` to a reasonable value (1-2m) |
| Tracking drift over long sessions | Markers may slowly shift | Recalibrate if drift is noticeable |
| Sun/bright conditions | Passthrough quality degrades | Demo in shade or overcast conditions |

---

## Debugging in the Field

### ADB over Wi-Fi (no cable needed)

Before leaving indoors, while Quest is still connected via USB:
```bash
adb tcpip 5555
```

Then in the field, connect wirelessly:
```bash
adb connect <quest-wifi-ip>:5555
adb logcat -s Unity
```

### Key Log Tags to Watch

```
[IRISManager]           — Camera rig setup, passthrough mode detection
[C2Client]              — Connection status, marker events
[AnchorManager]         — Marker spawning, GeoUtils placement
[CalibrationManager]    — Calibration GPS + Unity position
```

### Quick Diagnostic Commands

```bash
# Check server is running
curl http://<laptop-ip>:3000/health

# Check connected devices
curl http://<laptop-ip>:3000/api/config

# List current markers
curl http://<laptop-ip>:3000/api/markers
```

---

## Demo Script (If Presenting)

### Beat 1: The Setup (30s)
- Show laptop: server running, dashboard open with map centered on GT green
- "IRIS is a tactical AR system — command post sees the map, operators see markers in the real world"

### Beat 2: Calibration (30s)
- Put on Quest, stand at calibration point
- Press calibration button
- "I'm telling the system: my position right now equals these GPS coordinates"
- Show dashboard: session created, calibrated status

### Beat 3: Command Post Places a Marker (1 min)
- Someone clicks the dashboard map to place a marker at a nearby landmark
- Put on Quest — look toward the landmark
- "The command post placed a waypoint, and I can see it in my AR view anchored to the real world"

### Beat 4: Operator Places a Marker (1 min)
- While wearing Quest, point at something and press button to place marker
- Show dashboard — marker appears on map at correct location
- "I can report what I see back to the command post in real-time"

### Beat 5: Multi-Device (30s)
- Second operator calibrates and sees the same markers
- "Multiple operators share the same spatial awareness"

---

## Post-Demo Cleanup

1. Change `C2Client.serverUrl` back to `http://localhost:3000` in the Inspector
2. Commit the field demo code changes (WS1-WS3) to a feature branch
3. Note any issues encountered for future improvement
