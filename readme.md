# IRIS — Immersive Real-time Interlinked Systems

A tactical augmented reality system built as a Georgia Tech capstone project. IRIS connects a command-and-control server, a web-based dashboard, and a Meta Quest 3 AR application to enable real-time spatial marker placement and team coordination.

## Architecture

```
┌─────────────────┐     Socket.IO     ┌─────────────────┐
│  React Dashboard │◄────────────────►│   C2 Server     │
│  (Vite + Leaflet)│                  │ (Express+Socket) │
└─────────────────┘                   └────────┬────────┘
                                               │ Socket.IO
                                      ┌────────▼────────┐
                                      │  Quest 3 AR App │
                                      │  (Unity + Meta  │
                                      │   XR SDK)       │
                                      └─────────────────┘
```

## Components

| Component | Directory | Stack |
|-----------|-----------|-------|
| C2 Server | `server/` | Node.js 20, Express, Socket.IO v4 |
| Command Dashboard | `dashboard/` | Vite, React 18, Leaflet, socket.io-client |
| AR Application | `unity/IRIS-AR/` | Unity 2022.3 LTS (URP), Meta XR All-in-One SDK |

## Prerequisites

- **Node.js 20 LTS** — [download](https://nodejs.org/)
- **Unity 2022.3 LTS** — install via [Unity Hub](https://unity.com/download) with **Android Build Support** (including Android SDK & NDK)
- **Meta Quest 3** or **Meta XR Simulator** for AR development/testing

---

## Getting Started

### 1. C2 Server

```bash
cd server
cp .env.example .env
npm install
npm run dev
```

Server starts on `http://localhost:3000`. Verify with `GET /health`.

### 2. Command Dashboard

```bash
cd dashboard
npm install
npm run dev
```

Opens at `http://localhost:5173`. Click the map to place markers. Start the server first for full functionality.

### 3. End-to-End Test

1. Start the server (`cd server && npm run dev`)
2. Start the dashboard (`cd dashboard && npm run dev`)
3. Click the Leaflet map to place a marker — enter a label when prompted
4. The marker appears on the map and in the sidebar panel
5. Check the server terminal for socket event logs

### 4. Unity AR App — First-Time Setup

If the Unity project has already been set up (the scene `Assets/IRIS/Scenes/MainAR.unity` exists with OVRCameraRig and IRISManager), you can skip to [Running the Unity App](#running-the-unity-app).

#### 4a. Open the Project

1. Open **Unity Hub** → **Open** → navigate to `unity/IRIS-AR/` → select the folder
2. Make sure it opens with **Unity 2022.3 LTS**
3. Wait for the project to import (first time may take several minutes)

#### 4b. Install Meta XR SDK (if not already imported)

1. **Window** → **Package Manager**
2. In the top-left dropdown, switch to **My Assets** or search the **Unity Asset Store**
3. Find **Meta XR All-in-One SDK** → **Import**
4. When the Meta XR Project Setup Tool appears, click **Fix All** to apply recommended settings
5. Go to **File** → **Build Settings** → switch platform to **Android**
6. In **Player Settings** → **Other Settings**:
   - Scripting Backend → **IL2CPP**
   - Target Architectures → check **ARM64** only
   - Minimum API Level → **Android 10.0 (API level 29)**

#### 4c. Enable Meta XR Simulator

1. Top menu → **Oculus** → **Meta XR Simulator** → check **Enabled**
2. This lets you test in the editor without a headset

#### 4d. Verify Cesium for Unity Package

The Cesium package is declared in `Packages/manifest.json` and resolves automatically on project open.

1. Confirm import succeeded: **Window** → **Package Manager** → search for `Cesium for Unity` — it should show **v1.13.0** installed
2. Verify the top menu now has a **Cesium** entry
3. If the package failed to resolve, check the **Console** for errors. The most common issue is a conflict with Meta XR Simulator Synthetic Environment Builder (`com.meta.xr.simulator.synthenvbuilder`). If so, remove that package from `Packages/manifest.json` and re-open the project

#### 4e. Set Up Cesium ion Account

1. Top menu → **Cesium** → **Cesium** (opens the Cesium panel)
2. Click **Connect to Cesium ion** — a browser window opens
3. Sign in or create a free Cesium ion account
4. Authorize the Unity plugin when prompted
5. Back in Unity, the Cesium panel should now show **Connected**
6. Click **Token** at the top of the Cesium panel → create a new token or paste an existing access token

#### 4f. Add Cesium 3D Tilesets (GT Campus)

1. In the Cesium panel, click **Quick Add** → **Cesium World Terrain + Bing Maps Aerial imagery** → a `Cesium World Terrain` GameObject appears in the Hierarchy
2. Click **Quick Add** → **Cesium OSM Buildings** → adds a 3D building tileset
3. Select the **CesiumGeoreference** GameObject in the Hierarchy (auto-created by Cesium) → in Inspector, set:
   - **Latitude** → `33.7756`
   - **Longitude** → `-84.3963`
   - **Height** → `0`
4. This centers the 3D world on Georgia Tech campus

#### 4g. Add Fly Camera

The Cesium DynamicCamera is designed for globe-scale navigation and doesn't work well at ground level. Use a simple fly camera instead.

1. In the Hierarchy, right-click empty space → **Create Empty** → rename to `FlyCamera`
2. **Drag** `FlyCamera` onto the **CesiumGeoreference** GameObject to make it a child (this keeps it positioned correctly on the globe)
3. With `FlyCamera` selected → **Add Component** → search `Camera` → add it
4. **Add Component** → search `FlyCameraController` → add it
5. **Add Component** → search `CesiumGlobeAnchor` → add it
6. In the **CesiumGlobeAnchor** component, set:
   - **Longitude** → `-84.3963`
   - **Latitude** → `33.7756`
   - **Height** → `250` (meters — gives an aerial view of campus)
7. If a `DynamicCamera` exists in the Hierarchy from a previous step, **delete** it
8. Delete the default **Main Camera** if still present
9. Press **Play** to verify: you should see 3D terrain + buildings at GT campus
   - **Right-click + WASD** to fly, **Q/E** for down/up, **Shift** for speed boost

#### 4h. Create the AR Scene

1. **File** → **New Scene** → select **Basic (Built-in)** → **Create**
2. **File** → **Save As** → navigate to `Assets/IRIS/Scenes/` → name it `MainAR` → **Save**
3. In the **Hierarchy** panel (left side), right-click on **Main Camera** → **Delete**

#### 4i. Add OVRCameraRig

1. In the **Project** panel (bottom), use the search bar to search for `OVRCameraRig`
2. Find the **OVRCameraRig** prefab (blue cube icon) — it's from the Oculus package
3. **Drag** it from the Project panel into the **Hierarchy** panel
4. With **OVRCameraRig** selected, find the **OVR Manager** component in the Inspector (right side):
   - Under **Quest Features > General**, set:
     - Passthrough Support → **Supported**
     - Anchor Support → **Enabled**
     - Shared Spatial Anchor Support → **Enabled**

#### 4j. Add Passthrough Layer

1. Select **OVRCameraRig** in the Hierarchy
2. In the Inspector, scroll to the bottom → click **Add Component**
3. Search for `OVRPassthroughLayer` → click to add
4. On the new component, set **Placement** → **Underlay**

#### 4k. Configure the Camera for Passthrough

1. In the Hierarchy, expand **OVRCameraRig** → expand **TrackingSpace**
2. Click on **CenterEyeAnchor**
3. In the Inspector, find the **Camera** component:
   - **Clear Flags** → change to **Solid Color**
   - Click the **Background** color swatch → set the **A** (alpha) slider to **0** → close the color picker

#### 4l. Create the AnchorPrefab

1. In the Hierarchy, right-click empty space → **Create Empty**
2. Rename it to `AnchorPrefab` (click the name field in the Inspector)
3. With it selected → **Add Component** → search `OVRSpatialAnchor` → add it
4. **Add Component** → search `CesiumGlobeAnchor` → add it (this is what positions markers on the 3D globe)
5. Right-click `AnchorPrefab` in the Hierarchy → **3D Object** → **Cube**
6. Select the **Cube** child → in Inspector, set **Transform > Scale** to `0.1, 0.1, 0.1`
7. Select **AnchorPrefab** (the parent) → **Add Component** → search `AnchorVisualizer` → add it
8. In the **Project** panel, navigate to `Assets > IRIS > Prefabs`
9. **Drag** `AnchorPrefab` from the **Hierarchy** into the `Prefabs` folder in the Project panel — it turns blue in the Hierarchy (it's now a prefab)
10. Right-click `AnchorPrefab` in the Hierarchy → **Delete**

#### 4m. Create IRISManager

1. In the Hierarchy, right-click empty space → **Create Empty**
2. Rename it to `IRISManager`
3. **Add Component** → search `IRISManager` → add it
4. **Add Component** → search `AnchorManager` → add it
5. **Add Component** → search `C2Client` → add it
6. **Add Component** → search `DesktopInputManager` → add it
7. In the Inspector, find the **Anchor Manager** component:
   - **Anchor Prefab** field → drag `AnchorPrefab` from `Assets > IRIS > Prefabs`
   - **C2 Client** field → drag the `IRISManager` GameObject from the Hierarchy
   - **Georeference** field → drag the `CesiumGeoreference` GameObject from the Hierarchy
   - **Marker Height Offset** → `2` (meters above terrain / ellipsoid fallback)
   - **Ellipsoid Height Fallback Meters** → `255` (match **CesiumGeoreference** height when terrain sampling is off)
8. In the Inspector, find the **IRIS Manager** component:
   - **Auto Lift Rig Above Terrain On Start** → enabled for **Editor / XR Simulator** only if you spawn underground
   - **Disable Terrain Lift On Android** → enabled on **Quest builds** (avoids fighting Meta floor height and Cesium LOD “creep”)
   - **Eye Height Above Ground** → `1.6` (only used when terrain lift runs)
   - **Raycast Start Height** → `200`
   - **Enable Thumbstick Locomotion** → enabled
   - **Thumbstick Move Speed** → `2`
9. In the Inspector, find the **Desktop Input Manager** component:
   - **Anchor Manager** field → drag the `IRISManager` GameObject from the Hierarchy (resolves to AnchorManager)
10. In the Inspector, find the **C2 Client** component:
   - **Server Url** → `http://localhost:3000` for **Editor / Simulator** on the same Mac
   - For **Quest on Wi‑Fi**, set **Server Url** to your Mac’s LAN IP, e.g. `http://192.168.x.x:3000` (not `localhost`)
   - **Device Name** → `Quest3`
   - **Heartbeat Interval** → `10`

#### 4n. Add FieldStatusHUD

1. Select **IRISManager** in the Hierarchy
2. **Add Component** → search `FieldStatusHUD` → add it
3. In the Inspector, find the **Field Status HUD** component:
   - **C2 Client** field → drag the `IRISManager` GameObject from the Hierarchy (resolves to C2Client)
   - **Calibration Manager** field → drag the `IRISManager` GameObject from the Hierarchy (resolves to CalibrationManager)
4. The HUD will display connection status, calibration status, and a contextual hint ("Press A to calibrate" / "Press A to place marker") as a head-locked overlay in the bottom-left of the user's view

#### 4o. AR Marker Visuals (Optional Polish)

These steps improve marker visibility in passthrough AR. They are cosmetic — skip them if you just need functional markers.

**Create Unlit AR Material:**

1. In the Project panel, navigate to `Assets/IRIS/Materials/` (create the folder if needed)
2. Right-click → **Create** → **Material** → name it `MarkerAR_Unlit`
3. Change the shader to **Universal Render Pipeline → Unlit**
4. Set **Base Color** to bright cyan `#00FFFF`
5. Set **Render Face** to **Both**
6. Set **Surface Type** to **Opaque**

**Adjust Marker Prefab for AR:**

1. Open `Assets/IRIS/Prefabs/AnchorPrefab.prefab`
2. On the **MeshRenderer**, swap the material to `MarkerAR_Unlit`
3. Change **Transform Scale** to `(0.3, 1.0, 0.3)` — a 1m tall, 30cm wide pillar suitable for human-scale AR
4. The ground pin line and billboard label rotation are handled automatically by `AnchorVisualizer` in passthrough mode

#### 4p. Save the Scene

1. Press **Ctrl+S** to save

#### 4q. Testing dashboard markers on Quest

1. Put **Quest** and **Mac** on the **same Wi‑Fi**.
2. On the Mac: `cd server && npm run dev` and `cd dashboard && npm run dev -- --host`.
3. Note your Mac **LAN IP** (Vite prints **Network:** `http://<ip>:5173/`).
4. In Unity **C2 Client** on the scene you build: **Server Url** = `http://<ip>:3000` (not `localhost`). Build and run to the headset.
5. On the **Mac**, open the dashboard at `http://localhost:5173` and place a marker on the map. The **Quest** app should receive it (server log: `Quest3`, `marker:create`). You do **not** need to open the dashboard inside the headset browser unless you want to.

### Running the Unity App

1. Open the project in Unity Hub
2. Open the scene at `Assets/MainAR.unity`
3. Make sure Meta XR Simulator is enabled: **Oculus** → **Meta XR Simulator** → **Enabled**
4. Start the C2 server first: `cd server && npm run dev`
5. Press **Play** (triangle button at the top center)
6. Check the **Console** panel for:
   - `[C2Client] Connected to C2 server`
   - `[C2Client] Registered as device: <id>`
7. Markers created from the dashboard will appear as yellow cubes (pending), turning cyan once placed

---

## Project Structure

```
IRIS/
├── docs/                          # Project documentation
│   └── IRIS Project Proposal.docx
├── server/                        # C2 Server (Node.js)
│   ├── .env.example               # Environment template
│   └── src/
│       ├── index.js               # Express + Socket.IO entry point
│       ├── config.js              # Environment config
│       ├── socket/                # Socket.IO event handlers
│       │   ├── markerHandlers.js  # Marker CRUD events
│       │   └── deviceHandlers.js  # Device registration + heartbeat
│       └── models/                # In-memory data models
│           ├── Marker.js
│           └── Device.js
├── dashboard/                     # Command Dashboard (React)
│   └── src/
│       ├── App.jsx                # Main layout
│       ├── components/
│       │   ├── MapView.jsx        # Leaflet map with marker placement
│       │   ├── MarkerPanel.jsx    # Marker list sidebar
│       │   └── DeviceStatus.jsx   # Connected device indicators
│       ├── hooks/
│       │   └── useSocket.js       # useMarkers() and useDevices() hooks
│       └── services/
│           └── socketService.js   # Socket.IO client singleton
└── unity/IRIS-AR/                 # Unity AR project
    └── Assets/IRIS/
        ├── Scenes/MainAR.unity
        ├── Scripts/
        │   ├── Anchors/           # Spatial anchor management
        │   ├── Core/              # App manager
        │   ├── Geo/               # GPS ↔ Unity coordinate conversion
        │   ├── Markers/           # Marker data + rendering
        │   ├── Networking/        # Socket.IO client + event DTOs
        │   └── UI/                # HUD overlays (FieldStatusHUD)
        ├── Prefabs/               # AnchorPrefab, MarkerPrefab
        └── Materials/
```

## Team

Georgia Tech Ubi Comp Team IRIS— Spring 2026
