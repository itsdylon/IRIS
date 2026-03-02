# Workstream 3: AR HUD & UX (Unity)

**Milestone 2 — Week of March 2–8, 2026**
**Priority:** Medium — HUD layout can start Day 1, geo-dependent tasks need WS1
**Estimated effort:** 1–2 days

---

## Context

The Unity AR app currently has no user-facing status information. There is no way to tell if the headset is connected to the server, how many markers exist, or what reference location is active. Markers are identical cubes with no distance indication. The controller-based marker creation (AnchorManager.cs:132-146) only spawns locally and never syncs to the server or dashboard.

This workstream builds the heads-up display, improves marker readability, and wires controller-placed markers through the C2 server.

---

## Tasks

### 3A. Create `Assets/IRIS/Scripts/UI/HUDManager.cs`

**Can start Day 1 — no dependency on WS1.**

A world-space Canvas (or OVR overlay) anchored to the user's head that shows operational status.

**Display fields:**
- **Connection indicator** — green dot + "Connected" / red dot + "Disconnected"
- **Device ID** — the ID returned by `device:registered` (from `C2Client`)
- **Marker count** — number of active markers (from `AnchorManager._activeAnchors.Count`)
- **Reference location** — label like "GT Campus" (informational, from Inspector field)

**Implementation notes:**
- Subscribe to `C2Client.OnConnectedEvent` and `OnDisconnectedEvent` for connection status
- For marker count, either expose a public `MarkerCount` property on `AnchorManager` or subscribe to a new `OnMarkerCountChanged` event
- Use TextMeshPro for text rendering
- Position the canvas as a small panel in the upper-left of the user's view, or pinned to the left wrist (wrist-mounted HUD is a common Quest 3 pattern)

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/UI/HUDManager.cs` (new)

**Unity Inspector steps (manual):**
> 1. Create a new GameObject named `HUD` in the IRIS scene
> 2. Add a `Canvas` component set to **World Space**, size ~0.3m x 0.15m
> 3. Add `HUDManager` component to the HUD GameObject
> 4. Drag the `C2Client` and `AnchorManager` references into the Inspector fields
> 5. Add TextMeshPro children for each display field
> 6. Parent the HUD to the `CenterEyeAnchor` (or use a follow script) so it tracks the user's head

---

### 3B. Add distance/bearing labels to markers

**Depends on WS1** (needs markers at real positions, not 2m away).

Update `AnchorVisualizer` to display distance from the camera in meters.

**Implementation:**
- Add a TextMeshPro field below/beside the label showing `"12.4m"` (distance)
- Billboard the entire label group to always face `Camera.main`
- Update distance on a 0.5s timer (not every frame) to save performance:
  ```csharp
  private float _distanceUpdateTimer;
  private void Update()
  {
      _distanceUpdateTimer += Time.deltaTime;
      if (_distanceUpdateTimer >= 0.5f)
      {
          _distanceUpdateTimer = 0f;
          UpdateDistance();
      }
      // Billboard: always face camera
      transform.LookAt(Camera.main.transform);
      transform.Rotate(0, 180, 0); // flip so text isn't mirrored
  }
  ```

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorVisualizer.cs`

---

### 3C. Improve marker prefab visuals

**Can start Day 1 — no dependency on WS1.**

The current marker prefab is a plain cube. Replace with a more readable tactical marker:

**Suggested approach:**
- **Shape:** Cylinder base (0.1m diameter, 0.02m tall) + vertical line (thin cylinder, 0.5m tall) + billboard label at top. This reads as a "pin" in 3D space.
- **Pending state:** Add a pulsing ring or transparency oscillation to visually distinguish from placed markers. Simple approach: animate alpha between 0.5 and 1.0 on a sine wave.
- **Placed state:** Solid color, no pulse.
- **Scale:** Should be readable at 5–20m distance. Test in editor — the label text should be ~0.05m high minimum.

**File:** Update the existing anchor prefab in `unity/IRIS-AR/Assets/IRIS/Prefabs/` (or wherever it lives — check the `anchorPrefab` reference on `AnchorManager` in the Inspector)

**Unity Inspector steps (manual):**
> 1. Open the anchor prefab
> 2. Replace the Cube mesh with the new pin geometry (cylinder + line + billboard label)
> 3. Ensure `AnchorVisualizer` and `MarkerRenderer` components are still attached
> 4. Add a TextMeshPro child for the distance label (used by task 3B)
> 5. Save the prefab

---

### 3D. Wire controller marker creation through the server

**Depends on WS1** (needs `GeoUtils.UnityPositionToLatLng()`).

Currently `SpawnMarkerAtController()` (AnchorManager.cs:132-146) creates a marker locally with a random GUID but never tells the server. This means controller-placed markers:
- Don't appear on the dashboard
- Don't sync to other headsets
- Use a local GUID that the server doesn't know about

**Fix:**
1. Compute `(lat, lng)` from the spawn position using `GeoUtils.UnityPositionToLatLng(spawnPos, referenceLat, referenceLng)`
2. Emit `marker:create` with `{ lat, lng, label: "Placed Marker", type: "waypoint" }` to the server
3. Do NOT spawn the anchor locally — let the `marker:created` event from the server trigger `HandleMarkerCreated()`, which will then spawn it at the geo-converted position
4. This ensures the marker gets a server-assigned ID, appears on the dashboard, and syncs to all clients

**Also requires:** Adding `lat` and `lng` fields to `MarkerCreatePayload` in `MarkerEventData.cs`:
```csharp
public class MarkerCreatePayload
{
    public double lat;
    public double lng;
    public string label;
    public string type;
}
```

**Files:**
- `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs` — rewrite `SpawnMarkerAtController()`
- `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/MarkerEventData.cs` — add `lat`/`lng` to `MarkerCreatePayload`
- `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/C2Client.cs` — add `EmitMarkerCreate()` method

---

### 3E. Document all Unity Inspector wiring

**Do after other tasks are complete.**

Update the project README (or create a setup section in `docs/`) documenting every manual Inspector step introduced by this milestone:

- `AnchorManager`: new `referenceLat`/`referenceLng` fields (from WS1)
- `HUDManager`: new component, Canvas setup, reference wiring
- Updated anchor prefab: new geometry, distance label
- Any new GameObjects added to the scene

This is critical — these are steps that code changes alone do not accomplish. If they aren't documented, the next person who pulls the repo won't have a working scene.

**File:** Project README or `docs/UNITY_SETUP.md`

---

## Verification

1. Enter Play mode in Unity editor — HUD should display "Connected" (green), device ID, and marker count
2. Disconnect the server — HUD should update to "Disconnected" (red)
3. Place a marker from the dashboard — it should appear with distance label (e.g., "15.2m") that updates as you move
4. Markers should display as pins, not cubes, with type-appropriate colors
5. Pending markers should pulse; placed markers should be solid
6. Press A button on controller to place a marker — it should:
   - Appear in AR at the controller position
   - Show up on the dashboard map at the correct lat/lng
   - Have a server-assigned ID (not a local GUID)

---

## Files Modified

| File | Action |
|------|--------|
| `unity/IRIS-AR/Assets/IRIS/Scripts/UI/HUDManager.cs` | **Create** |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorVisualizer.cs` | Edit — distance label, billboard, pulse animation |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs` | Edit — rewrite `SpawnMarkerAtController()` |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/MarkerEventData.cs` | Edit — add `lat`/`lng` to `MarkerCreatePayload` |
| `unity/IRIS-AR/Assets/IRIS/Scripts/Networking/C2Client.cs` | Edit — add `EmitMarkerCreate()` method |
| Anchor prefab | Edit — new geometry, distance label child |
| `docs/UNITY_SETUP.md` or README | Create/edit — Inspector wiring docs |
