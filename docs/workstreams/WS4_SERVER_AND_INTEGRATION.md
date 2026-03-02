# Workstream 4: Server Hardening & Integration Testing

**Milestone 2 — Week of March 2–8, 2026**
**Priority:** Parallel — fully unblocked, can start Day 1
**Estimated effort:** 1–2 days

---

## Context

The server is functional but minimal: in-memory marker storage, Socket.IO events only, a single `/health` endpoint (server/src/index.js:21), no input validation, no REST API, and no tests. The reference point (`GT_CENTER`) is hard-coded separately in the dashboard (`MapView.jsx:14`) and will be hard-coded again in Unity's `AnchorManager` — there is no single source of truth.

This workstream hardens the server, adds a config endpoint so all clients share the same reference origin, and builds the integration tests that validate the full event loop.

---

## Tasks

### 4A. Add server-side geo config endpoint

**Can start Day 1.**

Create `server/src/config.js`:
```javascript
export const config = {
  referencePoint: {
    lat: 33.7756,
    lng: -84.3963,
    label: 'Georgia Tech Campus',
  },
  markerTypes: ['waypoint', 'threat', 'objective', 'info', 'generic'],
}
```

Add `GET /api/config` to `server/src/index.js`:
```javascript
app.get('/api/config', (req, res) => {
  res.json(config)
})
```

This gives both dashboard and Unity a single source of truth for the reference point. The dashboard can fetch it on startup instead of hard-coding `GT_CENTER`, and Unity's `C2Client` can request it on connect.

**Optionally** emit the config on Socket.IO connect as well, so Unity doesn't need an HTTP request:
```javascript
socket.on('config:request', () => {
  socket.emit('config:response', config)
})
```

**Files:** `server/src/config.js` (new), `server/src/index.js`

---

### 4B. Add `marker:create` validation

**Can start Day 1.**

Currently `markerHandlers.js` passes the raw `data` object straight to `MarkerStore.create()` with no validation. Add checks:

```javascript
socket.on('marker:create', (data) => {
  // Validate lat/lng if provided
  if (data.lat != null && (typeof data.lat !== 'number' || data.lat < -90 || data.lat > 90)) {
    return socket.emit('marker:error', { message: 'Invalid latitude' })
  }
  if (data.lng != null && (typeof data.lng !== 'number' || data.lng < -180 || data.lng > 180)) {
    return socket.emit('marker:error', { message: 'Invalid longitude' })
  }
  // Validate type
  if (data.type && !config.markerTypes.includes(data.type)) {
    return socket.emit('marker:error', { message: `Invalid type: ${data.type}` })
  }
  // ... proceed with create
})
```

Import `config` from the new config file (task 4A).

Also add a `marker:error` listener in the dashboard's `useSocket.js` to surface validation errors (e.g., `console.warn` or a toast).

**Files:** `server/src/socket/markerHandlers.js`, `dashboard/src/hooks/useSocket.js`

---

### 4C. Add REST endpoints for markers

**Can start Day 1.**

Add to `server/src/index.js` (or create a `server/src/routes/markers.js` router):

```javascript
// List all markers
app.get('/api/markers', (req, res) => {
  res.json(MarkerStore.list())
})

// Get single marker
app.get('/api/markers/:id', (req, res) => {
  const marker = MarkerStore.get(req.params.id)
  if (!marker) return res.status(404).json({ error: 'Not found' })
  res.json(marker)
})

// Delete marker (also emits socket event)
app.delete('/api/markers/:id', (req, res) => {
  const deleted = MarkerStore.delete(req.params.id)
  if (!deleted) return res.status(404).json({ error: 'Not found' })
  io.emit('marker:deleted', { id: req.params.id })
  res.json({ ok: true })
})
```

These are for debugging, tooling, and data export — the primary interface remains Socket.IO. Having REST endpoints makes it easy to inspect state with `curl` or a browser during development.

**Files:** `server/src/index.js` (or new `server/src/routes/markers.js`)

---

### 4D. Write integration test script

**Can start Day 1.** Does not require Unity.

Create `server/tests/marker-roundtrip.test.js` — a Node.js script that:

1. Starts or connects to the server
2. Connects **two** Socket.IO clients (simulating dashboard + headset)
3. Client A (dashboard) emits `marker:create` with `{ lat: 33.7760, lng: -84.3950, label: 'Test Alpha', type: 'waypoint' }`
4. Client B (headset) receives `marker:created` — asserts it has `lat`, `lng`, `label`, `type`, `status: 'pending'`, `position: null`
5. Client B emits `marker:place` with `{ id, position: { x: 14.4, y: 1.5, z: 4.4 } }`
6. Client A receives `marker:updated` — asserts `status: 'placed'`, `position` matches
7. Client A emits `marker:delete` with `{ id }`
8. Client B receives `marker:deleted` — asserts `id` matches
9. Both clients disconnect cleanly

Use whatever test runner the team prefers (plain Node assert, or add vitest/jest as a dev dependency). A simple `node server/tests/marker-roundtrip.test.js` that exits 0 on success and 1 on failure is sufficient.

**Add a test script to `server/package.json`:**
```json
"scripts": {
  "test": "node tests/marker-roundtrip.test.js"
}
```

**Files:** `server/tests/marker-roundtrip.test.js` (new), `server/package.json`

---

### 4E. Write end-to-end manual test protocol

**Do after tasks 4A–4D are complete, and ideally after WS1 has landed.**

Create `docs/TEST_PROTOCOL.md` with a step-by-step manual test script. This is what the team runs to validate the full pipeline before demo.

**Contents:**

```markdown
# End-to-End Test Protocol

## Prerequisites
- Server running (`cd server && npm run dev`)
- Dashboard running (`cd dashboard && npm run dev`)
- Unity project open with IRIS scene loaded

## Test Cases

### T1: Dashboard → AR marker placement
1. Open dashboard at localhost:5173
2. Click the map at the Student Center (~33.7742, -84.3983)
3. Enter label "Student Center", type "objective"
4. Verify: marker appears on map as green dot
5. Enter Unity Play mode
6. Verify: green marker appears at approx (-22m, 1.5m, -155m) from origin
7. Verify: marker label reads "Student Center" with distance

### T2: AR → Dashboard marker placement
1. In Unity, press A button on right controller
2. Verify: marker appears in AR at controller position
3. Verify: marker appears on dashboard map at correct lat/lng
4. Verify: sidebar shows marker with status "placed"

### T3: Marker deletion sync
1. Delete a marker from the dashboard sidebar
2. Verify: marker disappears from map AND from Unity

### T4: Connection resilience
1. Stop the server (Ctrl+C)
2. Verify: Unity HUD shows "Disconnected" (red)
3. Restart the server
4. Verify: Unity HUD shows "Connected" (green)
5. Verify: existing markers resync (marker:list on reconnect)
```

**File:** `docs/TEST_PROTOCOL.md` (new)

---

## Verification

1. `npm test` in `server/` — integration test passes (exit code 0)
2. `curl localhost:3000/api/config` returns the reference point JSON
3. `curl localhost:3000/api/markers` returns the current marker list
4. Creating a marker with invalid lat (e.g., `999`) returns a `marker:error` event
5. Manual test protocol can be executed end-to-end by a team member

---

## Files Modified

| File | Action |
|------|--------|
| `server/src/config.js` | **Create** — reference point, allowed types |
| `server/src/index.js` | Edit — add `/api/config`, `/api/markers` routes |
| `server/src/socket/markerHandlers.js` | Edit — add validation |
| `server/tests/marker-roundtrip.test.js` | **Create** — integration test |
| `server/package.json` | Edit — add `test` script |
| `dashboard/src/hooks/useSocket.js` | Edit — listen for `marker:error` |
| `docs/TEST_PROTOCOL.md` | **Create** — manual test protocol |
