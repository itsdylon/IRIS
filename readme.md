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

#### 4d. Create the AR Scene

1. **File** → **New Scene** → select **Basic (Built-in)** → **Create**
2. **File** → **Save As** → navigate to `Assets/IRIS/Scenes/` → name it `MainAR` → **Save**
3. In the **Hierarchy** panel (left side), right-click on **Main Camera** → **Delete**

#### 4e. Add OVRCameraRig

1. In the **Project** panel (bottom), use the search bar to search for `OVRCameraRig`
2. Find the **OVRCameraRig** prefab (blue cube icon) — it's from the Oculus package
3. **Drag** it from the Project panel into the **Hierarchy** panel
4. With **OVRCameraRig** selected, find the **OVR Manager** component in the Inspector (right side):
   - Under **Quest Features > General**, set:
     - Passthrough Support → **Supported**
     - Anchor Support → **Enabled**
     - Shared Spatial Anchor Support → **Enabled**

#### 4f. Add Passthrough Layer

1. Select **OVRCameraRig** in the Hierarchy
2. In the Inspector, scroll to the bottom → click **Add Component**
3. Search for `OVRPassthroughLayer` → click to add
4. On the new component, set **Placement** → **Underlay**

#### 4g. Configure the Camera for Passthrough

1. In the Hierarchy, expand **OVRCameraRig** → expand **TrackingSpace**
2. Click on **CenterEyeAnchor**
3. In the Inspector, find the **Camera** component:
   - **Clear Flags** → change to **Solid Color**
   - Click the **Background** color swatch → set the **A** (alpha) slider to **0** → close the color picker

#### 4h. Create the AnchorPrefab

1. In the Hierarchy, right-click empty space → **Create Empty**
2. Rename it to `AnchorPrefab` (click the name field in the Inspector)
3. With it selected → **Add Component** → search `OVRSpatialAnchor` → add it
4. Right-click `AnchorPrefab` in the Hierarchy → **3D Object** → **Cube**
5. Select the **Cube** child → in Inspector, set **Transform > Scale** to `0.1, 0.1, 0.1`
6. Select **AnchorPrefab** (the parent) → **Add Component** → search `AnchorVisualizer` → add it
7. In the **Project** panel, navigate to `Assets > IRIS > Prefabs`
8. **Drag** `AnchorPrefab` from the **Hierarchy** into the `Prefabs` folder in the Project panel — it turns blue in the Hierarchy (it's now a prefab)
9. Right-click `AnchorPrefab` in the Hierarchy → **Delete**

#### 4i. Create IRISManager

1. In the Hierarchy, right-click empty space → **Create Empty**
2. Rename it to `IRISManager`
3. **Add Component** → search `IRISManager` → add it
4. **Add Component** → search `AnchorManager` → add it
5. In the Inspector, find the **Anchor Manager** component
6. The **Anchor Prefab** field shows "None (Game Object)"
7. In the Project panel, navigate to `Assets > IRIS > Prefabs`
8. **Drag** the `AnchorPrefab` from the Project panel into the **Anchor Prefab** field — it should now show "AnchorPrefab"

#### 4j. Save the Scene

1. Press **Ctrl+S** to save

### Running the Unity App

1. Open the project in Unity Hub
2. Open the scene at `Assets/IRIS/Scenes/MainAR`
3. Make sure Meta XR Simulator is enabled: **Oculus** → **Meta XR Simulator** → **Enabled**
4. Press **Play** (triangle button at the top center)
5. A cyan cube should appear at position (0, 1.5, 2) — 2 meters in front, at head height
6. Check the **Console** panel (tab next to Project at the bottom) for: `[AnchorManager] Spawned test marker at (0.0, 1.5, 2.0)`

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
        │   ├── Markers/           # Marker data + rendering
        │   ├── Networking/        # C2 server connection (stub)
        │   └── Core/              # App manager
        ├── Prefabs/               # AnchorPrefab, MarkerPrefab
        └── Materials/
```

## Team

Georgia Tech Ubi Comp Team IRIS— Spring 2026
