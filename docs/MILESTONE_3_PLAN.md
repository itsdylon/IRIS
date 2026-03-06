# Milestone 3: Shared Spatial Anchors in a Cesium 3D Environment

**Sprint: March 6–20, 2026 (2 weeks / 10 working days)**
**Goal:** A rendered Cesium 3D geospatial scene where multiple simulated users share spatial anchors and see markers at the same world positions — fully developed and testable in the Unity Editor without Quest 3 hardware.

---

## Workstreams

| WS | Name | Component | Can Start | Depends On | Effort |
|---|---|---|---|---|---|
| **WS1** | [Cesium 3D Scene + Marker Pipeline](workstreams/M3_WS1_CESIUM_3D_SCENE.md) | Unity | **Day 1** | Nothing | 3–4 days |
| **WS2** | [Spatial Anchor System](workstreams/M3_WS2_SPATIAL_ANCHOR_SYSTEM.md) | Unity | **Day 1** (partially) | WS1 + WS3 for later tasks | 3–4 days |
| **WS3** | [Session & Anchor Server + Dashboard](workstreams/M3_WS3_SESSION_SERVER.md) | Server + Dashboard | **Day 1** | Nothing | 2–3 days |
| **WS4** | [Integration, Testing & Polish](workstreams/M3_WS4_INTEGRATION_AND_POLISH.md) | Cross-cutting | **Week 2** | WS1 + WS2 + WS3 | 3–4 days |

---

## Dependency Graph

```
DAY 1                        DAY 3              DAY 5              WEEK 2
─────                        ─────              ─────              ──────

WS1 ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■░░░░░░░░░░░░░░░
  1A Install Cesium ─┬─→ 1B Scene + Tiles ──→ 1E Rewrite          │
                     │   1C DynamicCamera      AnchorManager ──→ 1F Controller
                     └─→ 1D Prefab GlobeAnchor─┘                  markers ──→ 1G Input

WS3 ■■■■■■■■■■■■■■■■■■■■■■■■■■■■■░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
  3A Session Model ──→ 3B Handlers ──→ 3C Register ──→ 3E Tests
                                   └──→ 3D Dashboard SessionStatus

WS2 ░░░░░■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■░░░░░░░░░░░░░
  2A Interface ──→ 2C OVR stub                        │
              └──→ 2B Simulated ──────────────────→ 2D Manager ──→ 2E Calibration ──→ 2F Verify
                         │                                               │
                         └── needs WS3 (server events) ─────────┘        └── needs WS1 (Cesium)

WS4 ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░■■■■■■■■■■■■■■■
                                                   4A XR Simulator
                                                   4B Multi-instance test ← needs WS1+WS2+WS3
                                                   4C Error handling
                                                   4D Status HUD
                                                   4E Documentation
                                                   4F Quest 3 build prep

■ = active work    ░ = blocked / waiting
```

---

## Task-Level Dependency Table

Every task, its dependencies, and the earliest day it can start:

| Task | Description | Depends On | Earliest Start |
|---|---|---|---|
| **WS1-1A** | Install Cesium for Unity | — | Day 1 |
| **WS1-1B** | CesiumGeoreference + 3D Tilesets | 1A | Day 1 |
| **WS1-1C** | DynamicCamera | 1B | Day 1 |
| **WS1-1D** | CesiumGlobeAnchor on marker prefab | 1A | Day 1 |
| **WS1-1E** | Rewrite AnchorManager for Cesium | 1A, 1D | Day 2 |
| **WS1-1F** | Controller markers through server | 1E | Day 2 |
| **WS1-1G** | Desktop input manager (M/C keys) | 1E, 1F | Day 3 |
| | | | |
| **WS2-2A** | ISpatialAnchorProvider interface | — | Day 1 |
| **WS2-2B** | SimulatedSpatialAnchorProvider | 2A, **WS3-3B** | Day 2–3 |
| **WS2-2C** | OVRSpatialAnchorProvider stub | 2A | Day 1 |
| **WS2-2D** | SpatialAnchorManager | 2A, 2B, 2C | Day 3–4 |
| **WS2-2E** | CalibrationManager | 2D, **WS1-1E** | Day 4–5 |
| **WS2-2F** | Verify calibration flow | 2E, **WS3-3B** | Day 5 |
| | | | |
| **WS3-3A** | Session model | — | Day 1 |
| **WS3-3B** | Session + anchor handlers | 3A | Day 1–2 |
| **WS3-3C** | Register handlers in socket router | 3B | Day 2 |
| **WS3-3D** | Dashboard SessionStatus component | 3B | Day 2–3 |
| **WS3-3E** | Integration tests | 3B | Day 3 |
| | | | |
| **WS4-4A** | XR Simulator setup | **WS1** (scene) | Day 6 |
| **WS4-4B** | Multi-instance testing | **WS1 + WS2 + WS3** | Day 7 |
| **WS4-4C** | Error handling + resilience | **WS2** (anchor scripts) | Day 8 |
| **WS4-4D** | Status HUD | **WS1** (scene), **WS2** (calibration) | Day 8 |
| **WS4-4E** | Documentation | All workstreams | Day 9 |
| **WS4-4F** | Quest 3 build preparation | **WS1** (Cesium installed) | Day 6+ |

---

## Schedule

```
Week 1 (March 6–12)
═══════════════════════════════════════════════════════════════════════
Day 1 (Thu 3/6)   │ WS1: 1A Install Cesium, 1B Tilesets, 1C Camera, 1D Prefab
                   │ WS2: 2A Interface, 2C OVR stub
                   │ WS3: 3A Session model, 3B Handlers (start)
                   │
Day 2 (Fri 3/7)   │ WS1: 1E Rewrite AnchorManager, 1F Controller markers
                   │ WS2: 2B Simulated provider (needs WS3-3B)
                   │ WS3: 3B Handlers (finish), 3C Register, 3D Dashboard (start)
                   │
Day 3 (Mon 3/10)  │ WS1: 1G Desktop input, verify marker pipeline
                   │ WS2: 2D SpatialAnchorManager
                   │ WS3: 3D Dashboard (finish), 3E Integration tests
                   │
Day 4 (Tue 3/11)  │ WS1: Buffer / fixes from verification
                   │ WS2: 2E CalibrationManager
                   │ WS3: Buffer / test fixes
                   │
Day 5 (Wed 3/12)  │ WS2: 2E CalibrationManager (finish), 2F Verify calibration
                   │      → End of day: calibration flow works in editor
═══════════════════════════════════════════════════════════════════════

Week 2 (March 13–19)
═══════════════════════════════════════════════════════════════════════
Day 6 (Thu 3/13)  │ WS4: 4A XR Simulator setup, 4F Quest 3 build prep
                   │ WS4: 4D Status HUD (start)
                   │
Day 7 (Fri 3/14)  │ WS4: 4B Multi-instance testing (the big integration test)
                   │      Debug + fix issues
                   │
Day 8 (Mon 3/17)  │ WS4: 4C Error handling + resilience
                   │ WS4: 4D Status HUD (finish)
                   │      Continue fixing multi-instance issues
                   │
Day 9 (Tue 3/18)  │ WS4: 4E Documentation (UNITY_SETUP.md, TEST_PROTOCOL.md)
                   │
Day 10 (Wed 3/19) │ BUFFER — bug fixes, polish, demo prep
═══════════════════════════════════════════════════════════════════════
```

---

## What Can Run in Parallel (for multiple people)

**Three independent tracks on Day 1:**

| Person A (Unity — Cesium) | Person B (Unity — Anchors) | Person C (Server + Dashboard) |
|---|---|---|
| Install Cesium (1A) | ISpatialAnchorProvider interface (2A) | Session model (3A) |
| Scene + tilesets (1B) | OVR provider stub (2C) | Session handlers (3B) |
| DynamicCamera (1C) | Study Meta SSA docs/samples | Register handlers (3C) |
| Prefab globe anchor (1D) | | |

**Convergence points (require multiple workstreams):**

| Day | What converges | Why |
|---|---|---|
| Day 2–3 | WS2-2B needs WS3-3B | Simulated provider needs server anchor events |
| Day 4–5 | WS2-2E needs WS1-1E | CalibrationManager needs CesiumGeoreference |
| Day 7 | WS4-4B needs WS1+WS2+WS3 | Multi-instance test is the full-stack integration point |

---

## Critical Path

The longest dependency chain determines the minimum time:

```
Day 1: WS1-1A (Install Cesium)
  → Day 1-2: WS1-1B/1D (Scene + prefab)
    → Day 2: WS1-1E (Rewrite AnchorManager)
      → Day 3: WS1-1F/1G (Controller markers + input)
        → Day 4-5: WS2-2E (CalibrationManager — needs Cesium + SpatialAnchorManager)
          → Day 5: WS2-2F (Verify calibration)
            → Day 7: WS4-4B (Multi-instance test)
```

**Minimum: 7 working days to the integration test.** The remaining 3 days are for error handling, HUD, docs, and buffer.

**The single biggest risk to the schedule is Day 1:** if Cesium + Meta XR SDK fail to coexist, the entire critical path stalls. Budget the full day for this.

---

## Success Criteria

Must-haves (sprint fails without these):
1. Cesium 3D scene renders terrain + buildings at GT campus
2. Dashboard-placed markers appear at correct positions in the 3D scene
3. Markers placed in the 3D scene appear on the dashboard at correct lat/lng
4. Two Unity instances share a calibration anchor and see markers at the same positions

Expected (should have, sprint is still viable without):
5. Dashboard shows session/calibration status
6. Markers are type-colored across all clients
7. ISpatialAnchorProvider compiles with both Simulated and OVR providers
8. Status HUD shows connection + calibration state
9. Documentation for Inspector setup and testing

---

## Architecture Reference

```
┌──────────────────────────────────────────────────────────────────┐
│                       C2 Server (Node.js)                        │
│  Socket.IO: marker:*, device:*, session:*, anchor:*              │
│  REST: /api/config, /api/markers, /api/session                   │
└────────┬──────────────────────┬───────────────────┬──────────────┘
         │                      │                   │
    Dashboard              Unity Instance A     Unity Instance B
    (Leaflet 2D)           (Editor)             (Standalone Build)
    ┌──────────┐           ┌─────────────────┐  ┌─────────────────┐
    │ Map      │           │CesiumGeoreference│  │CesiumGeoreference│
    │ Markers  │           │ ├ World Terrain  │  │ ├ World Terrain  │
    │ Session  │           │ ├ OSM Buildings  │  │ ├ OSM Buildings  │
    │ status   │           │ └ DynamicCamera  │  │ └ DynamicCamera  │
    │          │           │                  │  │                  │
    │          │           │ Markers with     │  │ Markers with     │
    │          │           │ CesiumGlobeAnchor│  │ CesiumGlobeAnchor│
    │          │           │                  │  │                  │
    │          │           │ ISpatialAnchor   │  │ ISpatialAnchor   │
    │          │           │ Provider         │  │ Provider         │
    │          │           │ (Simulated)      │  │ (Simulated)      │
    │          │           │       │          │  │       │          │
    │          │           │ CalibrationMgr   │  │ CalibrationMgr   │
    │          │           │ (bridges SSA↔Geo)│  │ (bridges SSA↔Geo)│
    └──────────┘           └─────────────────┘  └─────────────────┘
```

---

## Files Created This Milestone (complete list)

| File | WS | Purpose |
|---|---|---|
| `Scripts/Anchors/ISpatialAnchorProvider.cs` | WS2 | Spatial anchor interface |
| `Scripts/Anchors/SimulatedSpatialAnchorProvider.cs` | WS2 | Socket.IO-based simulation |
| `Scripts/Anchors/OVRSpatialAnchorProvider.cs` | WS2 | Quest 3 hardware wrapper |
| `Scripts/Anchors/SpatialAnchorManager.cs` | WS2 | Provider orchestrator |
| `Scripts/Anchors/CalibrationManager.cs` | WS2 | SSA↔Cesium bridge |
| `Scripts/Core/DesktopInputManager.cs` | WS1 | Keyboard controls |
| `Scripts/UI/HUDManager.cs` | WS4 | Status display |
| `Prefabs/SpatialAnchorPrefab.prefab` | WS2 | OVR anchor prefab |
| `server/src/models/Session.js` | WS3 | Session store |
| `server/src/socket/sessionHandlers.js` | WS3 | Session/anchor events |
| `dashboard/src/components/SessionStatus.jsx` | WS3 | Session UI |
| `docs/UNITY_SETUP.md` | WS4 | Inspector guide |
| `docs/TEST_PROTOCOL.md` | WS4 | Test script |
