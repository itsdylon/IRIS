# M3 Workstream 3: Session & Anchor Server + Dashboard

**Milestone 3 — March 6–20, 2026**
**Priority:** HIGH — WS2 depends on these server events
**Dependencies:** None — fully async, starts Day 1
**Estimated effort:** 2–3 days
**Components:** Server (`server/`), Dashboard (`dashboard/`)

---

## Context

The C2 server already handles markers and devices. This workstream adds session management (grouping devices into a colocation session) and anchor relay (storing and broadcasting shared spatial anchor data). It also adds a dashboard component showing session/calibration status.

This workstream has **zero Unity dependencies** — it can be built and tested entirely with Socket.IO client scripts or the dashboard, starting on Day 1 in parallel with everything else.

---

## Dependency Map

```
WS3 Task               Depends On
─────────               ──────────
3A Session model        Nothing — start Day 1
3B Session handlers     3A
3C Register handlers    3B
3D Dashboard session    3B (needs to know event names/shapes)
3E Integration tests    3B
```

**All of WS3 is independent of WS1 and WS2.** It runs fully in parallel.

**WS2 depends on WS3:** The `SimulatedSpatialAnchorProvider` (WS2 task 2B) needs the `anchor:share`, `anchor:load`, `anchor:load:response` events to exist on the server.

---

## Tasks

### 3A. Create Session model

**No dependencies. Start Day 1.**

In-memory session store, same pattern as `Marker.js` and `Device.js`.

```javascript
// server/src/models/Session.js
import { v4 as uuid } from 'uuid'

const sessions = new Map()

export const SessionStore = {
  create(hostDeviceId) {
    const session = {
      id: uuid(),
      hostDeviceId,
      devices: [hostDeviceId],
      calibration: null, // { anchorId, groupUuid, lat, lng, alt, pose }
      anchors: [],       // shared anchors for this session
      createdAt: new Date().toISOString(),
    }
    sessions.set(session.id, session)
    return session
  },

  join(sessionId, deviceId) {
    const session = sessions.get(sessionId)
    if (!session) return null
    if (!session.devices.includes(deviceId)) {
      session.devices.push(deviceId)
    }
    return session
  },

  setCalibration(sessionId, calibration) {
    const session = sessions.get(sessionId)
    if (!session) return null
    session.calibration = calibration
    return session
  },

  addAnchor(sessionId, anchor) {
    const session = sessions.get(sessionId)
    if (!session) return null
    session.anchors.push(anchor)
    return session
  },

  getAnchorsForGroup(groupUuid) {
    for (const session of sessions.values()) {
      const matches = session.anchors.filter(a => a.groupUuid === groupUuid)
      if (matches.length > 0) return matches
    }
    return []
  },

  get(sessionId) { return sessions.get(sessionId) || null },
  list() { return [...sessions.values()] },
  delete(sessionId) { return sessions.delete(sessionId) },
}
```

**File:** `server/src/models/Session.js` (new)

---

### 3B. Create session and anchor Socket.IO handlers

**Depends on 3A.**

```javascript
// server/src/socket/sessionHandlers.js
import { SessionStore } from '../models/Session.js'

export default function sessionHandlers(io, socket) {

  // ─── Session lifecycle ───

  socket.on('session:create', (data) => {
    const deviceId = socket.deviceId // set during device:register
    if (!deviceId) {
      return socket.emit('session:error', { message: 'Register device first' })
    }
    const session = SessionStore.create(deviceId)
    io.emit('session:created', {
      sessionId: session.id,
      hostDeviceId: session.hostDeviceId,
    })
    console.log(`[Session] Created session ${session.id} by ${deviceId}`)
  })

  socket.on('session:join', ({ sessionId }) => {
    const deviceId = socket.deviceId
    if (!deviceId) {
      return socket.emit('session:error', { message: 'Register device first' })
    }
    const session = SessionStore.join(sessionId, deviceId)
    if (!session) {
      return socket.emit('session:error', { message: 'Session not found' })
    }

    // Send current session state to the joining device
    socket.emit('session:state', {
      sessionId: session.id,
      hostDeviceId: session.hostDeviceId,
      devices: session.devices,
      calibration: session.calibration,
      anchors: session.anchors,
    })

    // Broadcast to all that a device joined
    io.emit('session:joined', { sessionId, deviceId })
    console.log(`[Session] Device ${deviceId} joined session ${sessionId}`)
  })

  // ─── Anchor sharing ───

  socket.on('anchor:share', (data) => {
    // data: { sessionId, anchorId, groupUuid, pose, calibrationLat, calibrationLng, calibrationAlt }
    const anchor = {
      anchorId: data.anchorId,
      groupUuid: data.groupUuid,
      pose: data.pose, // { px, py, pz, rx, ry, rz, rw }
      calibrationLat: data.calibrationLat,
      calibrationLng: data.calibrationLng,
      calibrationAlt: data.calibrationAlt,
      sharedBy: socket.deviceId,
      sharedAt: new Date().toISOString(),
    }

    if (data.sessionId) {
      SessionStore.addAnchor(data.sessionId, anchor)
      SessionStore.setCalibration(data.sessionId, anchor)
    }

    // Broadcast to all clients
    io.emit('anchor:shared', anchor)
    console.log(`[Session] Anchor ${data.anchorId} shared in group ${data.groupUuid}`)
  })

  socket.on('anchor:load', ({ groupUuid }) => {
    const anchors = SessionStore.getAnchorsForGroup(groupUuid)
    socket.emit('anchor:load:response', { anchors })
    console.log(`[Session] Anchor load request for group ${groupUuid} — ${anchors.length} found`)
  })

  socket.on('anchor:erase', ({ anchorId }) => {
    // For simulation cleanup — just broadcast
    io.emit('anchor:erased', { anchorId })
    console.log(`[Session] Anchor ${anchorId} erased`)
  })
}
```

**File:** `server/src/socket/sessionHandlers.js` (new)

**Also:** In `deviceHandlers.js`, store the `deviceId` on the socket object after registration so `sessionHandlers` can access it:

```javascript
// In device:register handler, add:
socket.deviceId = device.id
```

**File:** `server/src/socket/deviceHandlers.js` (edit)

---

### 3C. Register session handlers

**Depends on 3B.**

Update the socket router to include the new handlers.

```javascript
// server/src/socket/index.js
import sessionHandlers from './sessionHandlers.js'

export default function registerSocketHandlers(io) {
  io.on('connection', (socket) => {
    markerHandlers(io, socket)
    deviceHandlers(io, socket)
    sessionHandlers(io, socket) // ← add this
  })
}
```

**Optionally** add a REST endpoint for debugging:
```javascript
// In server/src/index.js
app.get('/api/session', (req, res) => {
  const sessions = SessionStore.list()
  res.json(sessions)
})
```

**Files:**
- `server/src/socket/index.js` (edit)
- `server/src/index.js` (edit — optional REST endpoint)

---

### 3D. Dashboard SessionStatus component

**Depends on 3B (needs to know event names). Can run parallel with 3C.**

New component showing colocation session state in the dashboard header or sidebar.

**Displays:**
- Session active/inactive indicator
- Session ID (truncated)
- Host device name
- Joined device count + list
- Calibration status: "Calibrated" (green) / "Awaiting Calibration" (amber) / "No Session" (gray)

**Hook updates** — add session event listeners to `useSocket.js`:
```javascript
export function useSession() {
  const [session, setSession] = useState(null)

  useEffect(() => {
    socket.on('session:created', (data) => {
      setSession({ ...data, devices: [data.hostDeviceId], calibration: null })
    })

    socket.on('session:joined', ({ sessionId, deviceId }) => {
      setSession(prev => prev ? {
        ...prev,
        devices: [...new Set([...prev.devices, deviceId])]
      } : prev)
    })

    socket.on('session:state', (data) => {
      setSession(data)
    })

    socket.on('anchor:shared', (anchor) => {
      setSession(prev => prev ? { ...prev, calibration: anchor } : prev)
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

**Files:**
- `dashboard/src/hooks/useSocket.js` (edit — add `useSession` hook)
- `dashboard/src/components/SessionStatus.jsx` (new)
- `dashboard/src/App.jsx` (edit — render SessionStatus)

---

### 3E. Integration tests for session/anchor events

**Depends on 3B.**

Extend the existing `server/tests/marker-roundtrip.test.js` (which currently has only helper functions) with actual test cases.

**Test cases to add:**

1. **Marker roundtrip** — Client A creates marker, Client B receives `marker:created` with correct fields
2. **Marker place + update** — Client B emits `marker:place`, Client A receives `marker:updated` with position
3. **Marker delete** — Client A deletes, Client B receives `marker:deleted`
4. **Session create + join** — Client A creates session, Client B joins, both receive correct events
5. **Anchor share + load** — Client A shares anchor with groupUuid, Client B loads anchors for same groupUuid, receives correct pose data
6. **Validation** — invalid lat/lng returns `marker:error`

Add `"test": "node tests/marker-roundtrip.test.js"` to `server/package.json` scripts.

**File:** `server/tests/marker-roundtrip.test.js` (edit — add test cases)
**File:** `server/package.json` (edit — add test script)

---

## Socket.IO Events (complete reference)

| Event | Direction | Payload |
|---|---|---|
| `session:create` | Client → Server | `{}` (server generates sessionId) |
| `session:created` | Server → All | `{ sessionId, hostDeviceId }` |
| `session:join` | Client → Server | `{ sessionId }` |
| `session:joined` | Server → All | `{ sessionId, deviceId }` |
| `session:state` | Server → Client | `{ sessionId, hostDeviceId, devices, calibration, anchors }` |
| `session:error` | Server → Client | `{ message }` |
| `anchor:share` | Client → Server | `{ sessionId, anchorId, groupUuid, pose, calibrationLat, calibrationLng, calibrationAlt }` |
| `anchor:shared` | Server → All | (same + sharedBy, sharedAt) |
| `anchor:load` | Client → Server | `{ groupUuid }` |
| `anchor:load:response` | Server → Client | `{ anchors: [{ anchorId, groupUuid, pose, calibrationLat, calibrationLng, calibrationAlt }] }` |
| `anchor:erase` | Client → Server | `{ anchorId }` |
| `anchor:erased` | Server → All | `{ anchorId }` |

---

## Files Modified / Created

| File | Action |
|---|---|
| `server/src/models/Session.js` | **Create** — session store |
| `server/src/socket/sessionHandlers.js` | **Create** — session + anchor events |
| `server/src/socket/index.js` | Edit — register session handlers |
| `server/src/socket/deviceHandlers.js` | Edit — store deviceId on socket |
| `server/src/index.js` | Edit — optional `/api/session` REST endpoint |
| `server/tests/marker-roundtrip.test.js` | Edit — add test cases |
| `server/package.json` | Edit — add test script |
| `dashboard/src/hooks/useSocket.js` | Edit — add `useSession` hook |
| `dashboard/src/components/SessionStatus.jsx` | **Create** — session UI |
| `dashboard/src/App.jsx` | Edit — render SessionStatus |

---

## Unblocks

Completing this workstream unblocks:
- **WS2 task 2B** (SimulatedSpatialAnchorProvider needs anchor:share/load events on the server)
- **WS2 task 2F** (verification needs server running with session events)
- **WS4 task 4B** (multi-instance testing needs session flow)
