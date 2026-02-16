# IRIS — What's Next

## Current State (Checkpoint)

| Component | Status |
|-----------|--------|
| **C2 Server** | Working — Express + Socket.IO, in-memory marker CRUD, device presence, health endpoint |
| **Dashboard** | Working — Leaflet map, click-to-place markers, marker list, device status, real-time sync |
| **Unity AR App** | Partial — local marker spawning works (controller + test marker), but **networking is a stub** |

The server and dashboard are a functional end-to-end pair today. The Quest 3 app can render AR anchors locally but has **zero connection to the server**.

---

## Phase 1 — Connect Unity to the Server (Critical Path)

This is the single biggest gap. Without it, the AR headset is isolated.

- [ ] **1.1 Integrate a Socket.IO library into Unity**
  - Evaluate `SocketIOUnity` (NuGet/OpenUPM) vs raw `NativeWebSocket` + manual event framing
  - Add the chosen package to `unity/IRIS-AR/Packages/manifest.json`

- [ ] **1.2 Implement `C2Client.cs`**
  - Connect to the server on `Start()`, handle reconnection
  - Emit `device:register` with `{ name: "Quest3", type: "ar-headset" }`
  - Emit `device:heartbeat` on a timer (e.g. every 10 s)
  - Listen for `marker:created` and `marker:deleted` events

- [ ] **1.3 Wire C2Client into AnchorManager**
  - When the server sends `marker:created` → spawn an AR anchor at the GPS-mapped position
  - When a user places a marker in AR → emit `marker:create` to the server
  - When the server sends `marker:deleted` → destroy the local anchor

- [ ] **1.4 GPS-to-local coordinate mapping**
  - Define a reference anchor (e.g. a known GPS point on GT campus)
  - Convert `(lat, lng)` ↔ Unity world-space `(x, y, z)` relative to that anchor
  - This is the core spatial problem — start simple (flat-earth approximation) and refine later

---

## Phase 2 — Make the AR Experience Usable

- [ ] **2.1 Marker type system**
  - Add a UI or gesture to pick marker type (`waypoint`, `threat`, `objective`, etc.)
  - Dashboard: add a type selector dropdown when placing markers
  - Render different colors/icons per type in both dashboard and AR

- [ ] **2.2 Spatial anchor persistence (OVR)**
  - Use `OVRSpatialAnchor` save/load so anchors survive app restarts
  - Map persisted anchors back to server marker IDs

- [ ] **2.3 AR UI overlay**
  - HUD showing connection status, device count, marker count
  - Distance/bearing labels on markers
  - Hand-menu or controller-menu for marker actions (place, delete, change type)

---

## Phase 3 — Harden the Server

- [ ] **3.1 Persistent storage**
  - Replace in-memory `Map` stores with SQLite or MongoDB
  - Markers and device history survive server restarts

- [ ] **3.2 REST API for markers**
  - `GET /api/markers`, `POST /api/markers`, `DELETE /api/markers/:id`
  - Useful for non-realtime clients, debugging, and data export

- [ ] **3.3 Authentication**
  - Token-based auth for Socket.IO connections (middleware)
  - Prevent unauthorized devices from joining the network

- [ ] **3.4 Multi-room / mission support**
  - Socket.IO rooms per mission/operation
  - Server tracks which markers belong to which mission

---

## Phase 4 — Dashboard Enhancements

- [ ] **4.1 Marker filtering & search**
  - Filter by type, date, creator
  - Search by label text

- [ ] **4.2 Device tracking on map**
  - Show AR headset positions on the Leaflet map in real-time
  - Requires the Unity app to emit its GPS position periodically

- [ ] **4.3 Mission timeline / activity log**
  - Scrollable log of marker create/delete events with timestamps
  - Replay capability (stretch)

- [ ] **4.4 Map layer controls**
  - Satellite vs street view toggle
  - Custom overlay layers (building floorplans, threat zones)

---

## Phase 5 — Polish & Demo Prep

- [ ] **5.1 Error handling & edge cases**
  - Graceful reconnection in all clients
  - Offline buffering (queue events while disconnected, flush on reconnect)

- [ ] **5.2 Performance testing**
  - Stress test with many simultaneous markers and devices
  - Profile Unity rendering with dozens of anchors

- [ ] **5.3 Documentation**
  - API reference (auto-generate from server routes + socket events)
  - User guide for the AR app
  - Demo script / video walkthrough

- [ ] **5.4 Deployment**
  - Server → cloud host (Railway, Render, AWS)
  - Dashboard → static hosting (Vercel, Netlify)
  - Unity → Quest 3 APK sideload via SideQuest or Meta developer portal

---

## Immediate Next Steps (Start Here)

1. **Pick a Unity Socket.IO library** and get a basic connection working (Phase 1.1)
2. **Implement `C2Client.cs`** end-to-end (Phase 1.2)
3. **Test the full loop**: place a marker on the dashboard → see it appear in the headset (Phase 1.3)

Everything else builds on top of that connection.
