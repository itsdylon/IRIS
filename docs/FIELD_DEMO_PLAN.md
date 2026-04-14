# IRIS — Field Demo Milestone: GT Green Live XR

**Goal:** Take IRIS out of the Cesium simulation and run it as a live AR system on Quest 3 hardware at the Georgia Tech green, with passthrough video and GPS-anchored markers visible in the real world.

**Baseline:** Milestones 1-3 and the Demo Milestone are complete. The Cesium sim works on multiple Quest devices. Server, dashboard, and networking are field-ready. The work is entirely in Unity — creating a non-Cesium rendering path for passthrough AR.

---

## What Changes vs. Sim Mode

| Concern | Cesium Sim (today) | Field XR (needed) |
|---------|--------------------|--------------------|
| What user sees | Cesium 3D tiles (buildings, terrain) | Real world via Quest passthrough + overlays |
| Coordinate origin | CesiumGeoreference (virtual globe) | Quest tracking origin + GPS calibration anchor |
| Marker placement | `CesiumGlobeAnchor` on Cesium globe | `GeoUtils` offset from calibration GPS point |
| Terrain height | `Cesium3DTileset.SampleHeightMostDetailed()` | Real ground = user floor level (eye height offset) |
| Camera | FlyCamera or OVRCameraRig inside Cesium world | OVRCameraRig with passthrough, no Cesium rendering |
| Server/Dashboard | No changes | No changes (already field-ready) |

---

## Workstreams

| WS | Name | Effort | Depends On |
|----|------|--------|------------|
| **WS1** | [GPS-to-Local Coordinate Bridge](workstreams/FD_WS1_GPS_COORDINATE_BRIDGE.md) | ~2 hrs | Nothing |
| **WS2** | [Disable Cesium in Passthrough Mode](workstreams/FD_WS2_DISABLE_CESIUM_PASSTHROUGH.md) | ~30 min | Nothing |
| **WS3** | [Field Calibration Flow](workstreams/FD_WS3_CALIBRATION_FLOW.md) | ~30 min | WS1 |
| **WS4** | [Network Configuration for Field](workstreams/FD_WS4_NETWORK_CONFIG.md) | ~15 min | Nothing |
| **WS5** | [Quest Passthrough Field Test](workstreams/FD_WS5_FIELD_TESTING.md) | ~1 hr | WS1 + WS2 + WS3 + WS4 |
| **WS6** | [AR Marker Visuals (Optional)](workstreams/FD_WS6_AR_MARKER_VISUALS.md) | ~1 hr | WS1 |

---

## Dependency Graph

```
                WS1 GPS Bridge ──────────┐
                                         ├──→ WS3 Calibration ──┐
                WS2 Disable Cesium ──────┤                      ├──→ WS5 Field Test
                                         │                      │
                WS4 Network Config ──────┘                      │
                                                                │
                WS6 AR Visuals (optional) ──────────────────────┘
```

WS1, WS2, and WS4 can all start in parallel. WS3 depends on WS1. WS5 is the integration test requiring everything. WS6 is polish and can happen anytime after WS1.

---

## What Does NOT Need to Change

- **Server** (`server/`) — Already binds to `0.0.0.0`, all GPS marker logic is generic
- **Dashboard** (`dashboard/`) — Leaflet maps work identically, session/device status unchanged
- **C2Client.cs** — Pure networking, no Cesium dependency
- **Socket.IO events** — All event payloads use lat/lng, no sim-specific data
- **MarkerData.cs, MarkerRenderer.cs** — Data containers, no Cesium dependency

---

## Architecture: Field Mode vs. Sim Mode

```
                         C2 Server (laptop on hotspot)
                         ┌────────────────────────────────┐
                         │  Socket.IO + REST (port 3000)  │
                         └──┬──────────┬──────────┬───────┘
                            │          │          │
                    Dashboard      Quest A      Quest B
                    (laptop)     (passthrough)  (passthrough)
                    ┌────────┐   ┌──────────┐  ┌──────────┐
                    │Leaflet │   │OVRCamera │  │OVRCamera │
                    │Map     │   │Passthrough│  │Passthrough│
                    │Markers │   │          │  │          │
                    └────────┘   │GeoUtils  │  │GeoUtils  │
                                 │(GPS→local)│  │(GPS→local)│
                                 │          │  │          │
                                 │Markers at│  │Markers at│
                                 │local pos │  │local pos │
                                 └──────────┘  └──────────┘

    Calibration: All devices calibrate from same GPS point on GT green
    Markers: Dashboard lat/lng → GeoUtils → local Unity position
    Network: All devices on same Wi-Fi hotspot
```

---

## Field Day Checklist

### Equipment
- [ ] 2+ Quest 3 headsets (charged, dev mode enabled)
- [ ] Laptop for server + dashboard
- [ ] Mobile hotspot (or phone tethering)
- [ ] Phone with GPS app (for reading calibration coordinates)
- [ ] USB-C cable (for sideloading APK if needed)

### Pre-Field Setup
- [ ] Build APK with `serverUrl` set to laptop's hotspot IP
- [ ] Verify APK installs and launches on Quest
- [ ] Test server + dashboard on laptop over hotspot
- [ ] Note the exact GPS coordinates of your calibration point on the GT green

### On-Site
- [ ] Start hotspot, connect all devices
- [ ] Start server on laptop (`cd server && npm run dev`)
- [ ] Start dashboard on laptop (`cd dashboard && npm run dev`)
- [ ] Launch IRIS on each Quest
- [ ] Stand at calibration point, trigger calibration on each device
- [ ] Place markers from dashboard, verify they appear in AR at correct locations
- [ ] Place markers from Quest, verify they appear on dashboard map

---

## Success Criteria

**Must-haves (demo fails without these):**
1. Quest 3 shows passthrough video (real world), not Cesium tiles
2. Markers placed from dashboard appear in the AR view at approximately correct real-world positions
3. Markers placed from Quest appear on the dashboard map at approximately correct GPS coordinates
4. Two Quest devices see markers at the same real-world positions after calibrating from the same point

**Expected (nice to have):**
5. Markers are visually distinct against the real-world background (high contrast, unlit)
6. HUD shows connection + calibration status in passthrough mode
7. Reconnection works if server restarts in the field

---

## Risk Assessment

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| GPS accuracy (3-5m) causes marker offset | Medium | High | Calibrate all devices from same physical point |
| Quest tracking drift outdoors | Medium | Medium | Stay near calibration point; Quest 3 inside-out tracking works outdoors |
| Wi-Fi hotspot range on open field | High | Low | Stay within ~30m of hotspot; test range beforehand |
| Sunlight makes passthrough hard to see | Low | Medium | Demo in shade or later afternoon |
| `GeoUtils` approximation error at scale | Low | Low | Error is <1m within 500m of reference point — fine for GT green |

---

## Files Modified This Milestone

| File | WS | Change |
|------|-----|--------|
| `Scripts/Anchors/AnchorManager.cs` | WS1 | Add `useGeoUtils` mode, bypass Cesium for marker placement |
| `Scripts/Core/IRISManager.cs` | WS2 | Disable Cesium GameObjects in passthrough mode |
| `Scripts/Anchors/CalibrationManager.cs` | WS3 | Support GeoUtils-based calibration without Cesium |
| `Scripts/Geo/GeoUtils.cs` | WS1 | Add height parameter, improve API |
| `Scripts/Networking/C2Client.cs` | WS4 | No code changes, Inspector field `serverUrl` updated per-build |
| `Prefabs/AnchorPrefab.prefab` | WS6 | AR-friendly material/shader |
