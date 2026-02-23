# IRIS Milestone: Dashboard Markers → Quest 3 Spatial Anchors

## Context: Why We're Changing the Coordinate System

The original architecture assumed GPS-based positioning: the dashboard places markers at `(lat, lng)` on a Leaflet map, and a future transform maps them to Unity world space. **This doesn't work for our development environment:**

1. **Meta XR Simulator has no GPS.** The Quest runs in a predefined virtual room with Unity world-space coordinates. Latitude/longitude has no meaning here.
2. **GPS-to-local transforms require physical calibration.** Even on real hardware, mapping `(lat, lng)` to `(x, y, z)` requires a known GPS fix aligned to a known world-space origin. That's future work.
3. **The AR headset is the spatial authority.** It has 6DoF tracking with sub-centimeter accuracy. The dashboard cannot know where "here" is in 3D space — only the headset can.

### The Decided Architecture

Unity is the spatial authority. The dashboard sends **intent** (label + type), and Unity decides **where** the marker goes in 3D space:

```
Dashboard creates marker (label + type, no coordinates)
  → Server stores as status:"pending", assigns ID, broadcasts
    → Unity receives, places in local 3D space (2m in front of camera)
      → Unity reports position {x, y, z} back to server
        → Server updates to status:"placed" with position, broadcasts
          → Dashboard renders marker on 2D top-down XZ grid
```

This works identically in the Meta XR Simulator and on real Quest 3 hardware. GPS mapping can be layered on later without changing the core flow.

---

## Shared Contract (All Teams Read This First)

### New Marker Schema

```json
{
  "id": "uuid-string",
  "label": "string",
  "type": "generic | waypoint | threat | objective",
  "status": "pending | placed",
  "position": null | { "x": 0.0, "y": 1.5, "z": 2.0 },
  "createdAt": "ISO-8601",
  "placedAt": null | "ISO-8601"
}
```

**Removed** from current schema: `lat`, `lng`
**Added**: `status`, `position` (object or null), `placedAt`

### Socket Events

| Event | Direction | Payload |
|-------|-----------|---------|
| `marker:create` | Dashboard → Server | `{ label, type }` |
| `marker:created` | Server → All | Full marker (status: `"pending"`, position: `null`) |
| **`marker:place`** | **Unity → Server** | **`{ id, position: { x, y, z } }`** |
| **`marker:updated`** | **Server → All** | **Full marker (status: `"placed"`, position filled)** |
| `marker:list` | Client → Server | — |
| `marker:list:response` | Server → Client | `[markers]` |
| `marker:delete` | Client → Server | `{ id }` |
| `marker:deleted` | Server → All | `{ id }` |

**Bold = new events.** All device events (`device:register`, `device:heartbeat`, `device:list`) are unchanged.

### Flow Diagram

```
 DASHBOARD                    SERVER                      UNITY
     |                          |                           |
     |-- marker:create -------->|                           |
     |   { label, type }        |                           |
     |                          |-- marker:created -------->|
     |<-- marker:created -------|   (status:"pending")      |
     |   (status:"pending")     |                           |
     |                          |                  [places in 3D space]
     |                          |<-- marker:place --------- |
     |                          |   { id, position:{x,y,z}} |
     |<-- marker:updated -------|-- marker:updated -------->|
     |   (status:"placed",      |   (status:"placed",      |
     |    position:{x,y,z})     |    position:{x,y,z})     |
```

---

## Workstream A: Unity Socket.IO Integration

**Branch**: `feature/unity-socketio-integration`
**Goal**: Replace the C2Client stub with a working Socket.IO connection, wire it to AnchorManager so server markers spawn as spatial anchors, and report placement positions back.

### Files to Change

| File | Action |
|------|--------|
| `unity/IRIS-AR/Packages/manifest.json` | **Modify** — add SocketIOUnity package |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/C2Client.cs` | **Rewrite** — full Socket.IO client |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/MarkerEventData.cs` | **Create** — JSON DTO classes |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Markers/MarkerData.cs` | **Modify** — new schema (remove lat/lng, add status/position) |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs` | **Modify** — subscribe to C2Client events, track anchors by ID |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Core/IRISManager.cs` | **Modify** — add C2Client reference |

### Step A.1: Install SocketIOUnity

Add to `Packages/manifest.json`:
```json
"com.itisnajim.socketiounity": "https://github.com/itisnajim/SocketIOUnity.git"
```

This pulls in SocketIOClient + Newtonsoft.Json + Unity main-thread dispatcher.

### Step A.2: Update MarkerData.cs

Remove `lat`/`lng`, add `status`, `position`, `placedAt`. Add `MarkerPosition` class with `x, y, z` and a `Vector3` conversion helper.

```csharp
namespace IRIS.Markers
{
    [Serializable]
    public class MarkerData
    {
        public string id;
        public string label;
        public string type;
        public string status;           // "pending" or "placed"
        public MarkerPosition position;  // null when pending
        public string createdAt;
        public string placedAt;          // null when pending

        public MarkerData(string id, string label, string type) { ... }
        public Vector3 GetPositionVector() { ... }
    }

    [Serializable]
    public class MarkerPosition
    {
        public float x, y, z;
        public MarkerPosition(Vector3 v) { ... }
    }
}
```

### Step A.3: Create MarkerEventData.cs

Lightweight DTOs for socket JSON payloads: `MarkerCreatePayload`, `MarkerPlacePayload`, `PositionPayload`, `MarkerDeletePayload`, `DeviceRegisterPayload`.

### Step A.4: Rewrite C2Client.cs

The core of this workstream. The new C2Client must:

1. **Connect** on `Start()` via SocketIOUnity to `http://localhost:3000`
   - Force WebSocket transport (skip HTTP polling)
   - Set `unityThreadScope = UnityThreadScope.Update` (all callbacks on main thread)
2. **Register** as device: emit `device:register` with `{ name: "Quest3", type: "ar-headset" }`
3. **Request existing markers** on connect: emit `marker:list`
4. **Heartbeat** every 10s: emit `device:heartbeat` with assigned device ID
5. **Expose C# events** for AnchorManager to subscribe to:
   - `event Action<MarkerData> OnMarkerCreated`
   - `event Action<MarkerData> OnMarkerUpdated`
   - `event Action<string> OnMarkerDeleted`
   - `event Action OnConnectedEvent` / `OnDisconnectedEvent`
6. **Provide outbound methods**:
   - `EmitMarkerPlace(string markerId, Vector3 position)` — sends `marker:place`
   - `RequestMarkerList()` — sends `marker:list`
7. **Parse JSON** using Newtonsoft.Json `JObject` for incoming marker payloads
8. **Clean up** on `OnDestroy()`: cancel heartbeat, disconnect, dispose socket

### Step A.5: Update AnchorManager.cs

- Add `[SerializeField] private C2Client c2Client`
- Subscribe to `OnMarkerCreated`, `OnMarkerUpdated`, `OnMarkerDeleted` in `Start()`
- Track spawned anchors in `Dictionary<string, GameObject> _activeAnchors`
- **On `marker:created` (pending)**: spawn anchor 2m in front of camera, then call `c2Client.EmitMarkerPlace(id, position)` to report placement back
- **On `marker:created` (placed, from list response)**: spawn at the known position
- **On `marker:updated`**: update visual color (yellow→cyan for pending→placed)
- **On `marker:deleted`**: destroy the GameObject, remove from dictionary
- Color scheme: **yellow** = pending, **cyan** = placed
- Keep A-button controller placement as a local-only feature
- Default `spawnTestMarkerOnStart` to `false`

### Step A.6: Update IRISManager.cs

Add `[SerializeField] private C2Client c2Client` with auto-GetComponent fallback.

### Step A.7: Unity Inspector Wiring

- Add C2Client component to IRISManager GameObject
- Drag C2Client into AnchorManager's serialized field
- Set `serverUrl` to `http://localhost:3000`
- Set `spawnTestMarkerOnStart` to false

---

## Workstream B: Server Bidirectional Flow

**Branch**: `feature/server-bidirectional-markers`
**Goal**: Evolve the marker model and socket handlers to support the pending→placed lifecycle.

### Files to Change

| File | Action |
|------|--------|
| `server/src/models/Marker.js` | **Modify** — new schema, add `place()` method |
| `server/src/socket/markerHandlers.js` | **Modify** — add `marker:place` handler, emit `marker:updated` |

No changes needed to `index.js`, `config.js`, `deviceHandlers.js`, or `Device.js`.

### Step B.1: Update Marker.js

**`create()`** — Accept `{ label, type }` only (no lat/lng). Return marker with `status: 'pending'`, `position: null`, `placedAt: null`.

**`place(id, position)`** — New method. Finds marker by ID, sets `status: 'placed'`, stores `position: { x, y, z }` (with `parseFloat` guards), sets `placedAt` timestamp. Returns updated marker or null.

```javascript
export const MarkerStore = {
  create({ label = '', type = 'generic' }) {
    const marker = {
      id: uuidv4(),
      label,
      type,
      status: 'pending',
      position: null,
      createdAt: new Date().toISOString(),
      placedAt: null,
    }
    markers.set(marker.id, marker)
    return marker
  },

  place(id, position) {
    const marker = markers.get(id)
    if (!marker) return null
    marker.status = 'placed'
    marker.position = {
      x: parseFloat(position.x) || 0,
      y: parseFloat(position.y) || 0,
      z: parseFloat(position.z) || 0,
    }
    marker.placedAt = new Date().toISOString()
    return marker
  },

  // list(), get(), delete(), clear() — unchanged
}
```

### Step B.2: Update markerHandlers.js

- **`marker:create`** — Update log line (no more lat/lng to print). Behavior unchanged.
- **`marker:place`** — New handler. Validates `id` and `position`, calls `MarkerStore.place()`, broadcasts `marker:updated` to all clients.
- **`marker:list`** and **`marker:delete`** — Unchanged.

```javascript
socket.on('marker:place', ({ id, position }) => {
  if (!id || !position) return
  const marker = MarkerStore.place(id, position)
  if (marker) {
    console.log(`[marker:place] ${marker.label} at (${marker.position.x}, ${marker.position.y}, ${marker.position.z})`)
    io.emit('marker:updated', marker)
  }
})
```

### CORS Note

Unity's SocketIOUnity with WebSocket transport bypasses browser CORS. No server CORS changes should be needed. If issues arise, expand the origin array in `index.js` as a fallback.

---

## Workstream C: Dashboard Grid View

**Branch**: `feature/dashboard-spatial-grid`
**Goal**: Replace the Leaflet map with a 2D top-down XZ spatial grid, add a marker creation form, and handle the new marker schema.

### Files to Change

| File | Action |
|------|--------|
| `dashboard/src/hooks/useSocket.js` | **Modify** — add `marker:updated` listener, update createMarker signature |
| `dashboard/src/components/SpatialGrid.jsx` | **Create** — SVG-based XZ grid view |
| `dashboard/src/components/MarkerCreateForm.jsx` | **Create** — label + type input form |
| `dashboard/src/components/MarkerPanel.jsx` | **Modify** — show position/status instead of lat/lng |
| `dashboard/src/App.jsx` | **Modify** — swap MapView for SpatialGrid, add MarkerCreateForm |
| `dashboard/src/App.css` | **Modify** — add grid, form, and pending styles |

`MapView.jsx` is kept in the codebase but not rendered. Leaflet dependencies stay in `package.json` for future GPS use.

### Step C.1: Update useSocket.js

- Add `marker:updated` listener that replaces the matching marker in state by ID
- `createMarker` now sends `{ label, type }` only (no lat/lng)

```javascript
socket.on('marker:updated', (marker) => {
  setMarkers((prev) => prev.map((m) => (m.id === marker.id ? marker : m)))
})
```

### Step C.2: Create MarkerCreateForm.jsx

Sidebar form replacing the map-click `prompt()` workflow:
- Text input for label
- Dropdown for type (`generic`, `waypoint`, `threat`, `objective`)
- Create button (disabled when label is empty)
- Resets form after creation

### Step C.3: Create SpatialGrid.jsx

SVG-based 2D top-down view:
- **Auto-scaling viewport**: computes bounding box of all placed markers, adds 2m padding, minimum 6m visible range
- **1m grid lines** for spatial reference, with origin crosshair
- **Placed markers**: cyan circles with labels at their `(x, z)` position
- **Pending markers**: listed below the grid with pulsing yellow dots and "Awaiting Placement by AR Device" header
- Uses `viewBox="0 0 100 100"` with `preserveAspectRatio` for responsive scaling

### Step C.4: Update MarkerPanel.jsx

- Show `x, y, z` coordinates for placed markers (instead of `lat.toFixed(4), lng.toFixed(4)`)
- Show "Pending placement..." in yellow italic for pending markers
- Update empty state text to "Create a marker to get started"

### Step C.5: Update App.jsx

- Replace `MapView` import with `SpatialGrid`
- Add `MarkerCreateForm` to sidebar (above MarkerPanel)
- Pass `createMarker` to `MarkerCreateForm` instead of `MapView`

### Step C.6: Add CSS

- `.marker-create-form` — dark inputs, cyan create button, vertical layout
- `.spatial-grid` — full-height flex container, dark background
- `.grid-svg` — fills available space
- `.pending-list` — below grid, yellow pulsing dots
- `.marker-pending` — dimmed opacity for pending items in sidebar

---

## Integration & Testing

### Independent Testing

**Workstream B (Server)** — test with a quick Node script:
```javascript
import { io } from 'socket.io-client'
const socket = io('http://localhost:3000')
socket.on('connect', () => {
  socket.emit('marker:create', { label: 'Test', type: 'waypoint' })
})
socket.on('marker:created', (m) => {
  console.log('Created:', m) // status:"pending", position:null
  socket.emit('marker:place', { id: m.id, position: { x: 1.5, y: 1.0, z: 3.0 } })
})
socket.on('marker:updated', (m) => {
  console.log('Placed:', m) // status:"placed", position filled
})
```

**Workstream C (Dashboard)** — run against updated server, create markers via form, simulate placement with the test script above.

**Workstream A (Unity)** — run against updated server, verify device registration in server console, create markers from dashboard and watch Unity console for `marker:created` logs and anchor spawning.

### End-to-End Test Sequence

| Step | Action | Expected |
|------|--------|----------|
| 1 | Start server, dashboard, Unity (Meta XR Sim) | Dashboard + "Quest3" in device list |
| 2 | Create marker "Alpha" (waypoint) in dashboard | Appears as "Pending placement..." in sidebar |
| 3 | Observe Unity | Anchor spawns 2m in front of camera, yellow cube, "Alpha" label |
| 4 | Observe dashboard | "Alpha" updates to show x/y/z coords, appears on spatial grid as cyan dot |
| 5 | Delete "Alpha" from dashboard | Disappears from sidebar + grid, destroyed in Unity |
| 6 | Create multiple markers | All appear in sequence, grid auto-scales |

### Edge Cases

- **Unity offline when marker created**: stays "pending" in dashboard. On Unity connect, received via `marker:list`, placed and reported.
- **Server restart**: all in-memory data lost. Both clients reconnect to empty state. (Persistent storage is a future phase.)
- **Multiple Unity clients**: both receive `marker:created`, both place. Last `marker:place` wins. Acceptable for now.

### Merge Order

1. **Workstream B (Server) first** — changes the data model everything depends on
2. **Workstream C (Dashboard) second** — depends on new server schema
3. **Workstream A (Unity) last** — completes the full loop

Branch naming per CLAUDE.md:
- `feature/server-bidirectional-markers`
- `feature/dashboard-spatial-grid`
- `feature/unity-socketio-integration`
