# IRIS — Developer Conventions

## Project Overview
IRIS (Immersive Real-time Interlinked Systems) is a tactical AR system with three components:
- **C2 Server** (`server/`) — Node.js + Express + Socket.IO
- **Command Dashboard** (`dashboard/`) — Vite + React + Leaflet
- **AR App** (`unity/IRIS-AR/`) — Unity 2022.3 LTS + Meta XR SDK (Quest 3)

## Monorepo Layout
- `server/` — C2 server (Node.js 20)
- `dashboard/` — React command dashboard (Vite)
- `unity/IRIS-AR/` — Unity project; custom code lives under `Assets/IRIS/`
- `docs/` — Project documentation

## Coding Conventions

### JavaScript/React (server + dashboard)
- ES Modules (`import`/`export`)
- No semicolons (Prettier default)
- Single quotes for strings
- Use `const` by default, `let` when reassignment is needed
- Socket.IO event names: `namespace:action` (e.g., `marker:create`, `device:register`)

### C# (Unity)
- PascalCase for classes, methods, properties
- camelCase for local variables and parameters
- `[SerializeField]` for Inspector-exposed private fields
- All custom scripts go under `Assets/IRIS/Scripts/`
- Namespace: `IRIS`

### Socket.IO Events
| Event | Direction | Payload |
|-------|-----------|---------|
| `marker:create` | Client → Server | `{ lat, lng, label, type }` |
| `marker:created` | Server → All | `{ id, lat, lng, label, type, createdAt }` |
| `marker:list` | Client → Server | — |
| `marker:list:response` | Server → Client | `[markers]` |
| `marker:delete` | Client → Server | `{ id }` |
| `marker:deleted` | Server → All | `{ id }` |
| `device:register` | Client → Server | `{ name, type }` |
| `device:registered` | Server → Client | `{ id, name, type }` |
| `device:heartbeat` | Client → Server | `{ id }` |
| `device:list` | Server → All | `[devices]` |

## Running Locally
```bash
# Server
cd server && npm install && npm run dev

# Dashboard
cd dashboard && npm install && npm run dev
```

## Unity Inspector Steps
After any code change that adds or modifies `[SerializeField]` fields, new components, or prefab wiring:
1. Document the required Unity Inspector steps in the README under the appropriate setup section
2. These are manual steps that agents cannot perform — the README is the only way teammates learn about them
3. Keep instructions explicit: name the GameObject, the component, the field, and exactly what to drag where

## Git
- Branch naming: `feature/short-description`, `fix/short-description`
- Commit messages: imperative mood, concise
- Do not commit `.env`, `node_modules/`, or Unity `Library/` folders
