# IRIS — What's Next

## Current State (March 6, 2026)

| Component | Status |
|-----------|--------|
| **C2 Server** | Working — Express + Socket.IO, marker CRUD, device presence, REST API, config, validation |
| **Dashboard** | Working — Leaflet map, type-colored markers, create form, marker panel, device status |
| **Unity AR** | Working — Socket.IO networking, markers from server, flat-earth geo, type coloring |
| **Hardware** | No Quest 3 devices yet — all development in Unity Editor + Meta XR Simulator |

### Milestone History
- **Milestone 1** (Complete): Server + dashboard + Unity app with Socket.IO networking
- **Milestone 2** (Closed): GPS-to-AR marker positioning — WS1 (geo core) + WS2 (types) complete, WS3/WS4 partial, folded into M3

---

## Active: Milestone 3 — Shared Spatial Anchors in a Cesium 3D Environment

**Sprint: March 6–20, 2026 (2 weeks)**
**Full plan: `docs/MILESTONE_3_PLAN.md`**

| Phase | Days | Goal |
|-------|------|------|
| 1: Cesium 3D World | 1–2 | Rendered terrain + buildings at GT campus, DynamicCamera navigation |
| 2: Markers in the 3D World | 3–5 | CesiumGlobeAnchor replaces GeoUtils, markers appear in the 3D scene |
| 3: Spatial Anchor System | 5–8 | ISpatialAnchorProvider abstraction, simulated + OVR implementations, calibration bridge |
| 4: Multi-Instance Testing | 8–10 | XR Simulator, two-instance colocation testing, dashboard session status |
| 5: Hardening + Docs | 11–14 | Error handling, HUD, integration tests, documentation, Quest 3 build prep |

**Key design decisions:**
- Cesium 3D Tiles render a visible digital twin (not just coordinate math)
- Spatial anchors abstracted behind `ISpatialAnchorProvider` — simulated for dev, OVR for hardware
- Everything testable in editor without Quest 3 devices
- Meta XR Simulator for XR interaction testing

---

## Future (Post-Milestone 3)

- Quest 3 on-device testing (when hardware arrives — swap `useSimulation` flag)
- Persistent storage (database instead of in-memory)
- Authentication
- Dashboard enhancements (filtering, headset position tracking, activity log)
- Improved marker visuals (pins, distance labels)
- Deployment (cloud server, static dashboard hosting, Quest APK)
