# FD Workstream 6: AR Marker Visuals (Optional)

**Field Demo Milestone**
**Priority:** LOW — polish, not required for functionality
**Dependencies:** WS1 (GPS bridge — markers must appear first)
**Estimated effort:** ~1 hour
**Component:** Unity (`unity/IRIS-AR/`)

---

## Context

The current `AnchorPrefab` is a cyan cube (scale 1.2 x 6 x 1.2) with a TextMeshPro label — designed for visibility in the Cesium 3D sim. In passthrough AR, this cube will:
- Look odd floating in the real world (no ground reference)
- Be hard to see against bright outdoor backgrounds (lit material darkens against sky)
- Lack depth cues that help the user gauge distance

This workstream adjusts marker visuals for outdoor AR use. These are cosmetic changes — the system works without them.

---

## Tasks

### 6A. Create Unlit AR Material

Passthrough scenes have unpredictable lighting. An unlit material ensures markers are always visible regardless of sun position, shadows, or passthrough exposure.

**Steps (Unity Inspector — manual):**

1. In the Project panel, right-click `Assets/IRIS/Materials/` (create folder if needed) → Create → Material
2. Name it `MarkerAR_Unlit`
3. Change the shader:
   - If using URP: Shader → Universal Render Pipeline → Unlit
   - Set Base Color to a high-visibility color: bright cyan `#00FFFF` or orange `#FF8800`
4. Set **Render Face** to Both (visible from any angle)
5. Set **Surface Type** to Opaque

### 6B. Adjust Marker Prefab Scale

The current 1.2 x 6 x 1.2 scale was designed for Cesium globe-scale viewing. In passthrough AR at human scale, this is a 6-meter tall pillar. Reduce to something reasonable.

**Steps (Unity Inspector — manual):**

1. Open `Assets/IRIS/Prefabs/AnchorPrefab.prefab`
2. Change Transform Scale to `(0.3, 1.0, 0.3)` — a 1m tall, 30cm wide pillar
3. Or consider `(0.5, 0.5, 0.5)` for a more compact cube

**Alternative:** Use a different shape entirely:
- **Diamond/rhombus** — more distinctive than a cube, reads as a "marker" at a glance
- **Vertical line + billboard** — a thin vertical line from ground to label height, with the label always facing the camera

### 6C. Add Ground Pin Line (Optional Enhancement)

A thin line from the marker down to the ground helps users judge distance and position in 3D space.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Markers/AnchorVisualizer.cs` (or wherever marker rendering lives)

**Change:** Add a `LineRenderer` component programmatically:

```csharp
private void AddGroundPin()
{
    var lr = gameObject.AddComponent<LineRenderer>();
    lr.positionCount = 2;
    lr.startWidth = 0.02f;
    lr.endWidth = 0.02f;
    lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
    lr.material.color = Color.white;
    lr.useWorldSpace = false;
    lr.SetPosition(0, Vector3.zero);           // Marker position
    lr.SetPosition(1, Vector3.down * transform.position.y); // Down to ground
}
```

Call `AddGroundPin()` from the visualizer's initialization.

**Note:** This is a rough implementation. The line length assumes `transform.position.y` is the height above ground, which is true in passthrough mode where ground = y=0.

### 6D. Add Billboard Label Rotation

TextMeshPro labels on the marker should always face the camera so they're readable from any angle.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Markers/AnchorVisualizer.cs`

**Change:** Add in `Update()` or `LateUpdate()`:

```csharp
private void LateUpdate()
{
    if (labelText == null) return;

    var cam = Camera.main;
    if (cam == null) return;

    // Billboard: face the camera
    labelText.transform.rotation = Quaternion.LookRotation(
        labelText.transform.position - cam.transform.position);
}
```

This may already be happening if the label is a world-space TextMeshPro with auto-rotation. Check in the Editor first before adding.

### 6E. Type-Based Color Coding for AR

The existing `AnchorVisualizer.SetType()` sets colors per marker type. Verify these colors are high-contrast against an outdoor background:

| Type | Current Color | Suggested AR Color |
|------|--------------|-------------------|
| waypoint | Cyan | Keep — high visibility |
| threat | Red | Keep — instinctively alarming |
| objective | Yellow/Gold | Keep — stands out outdoors |
| info | Blue | Make brighter — dark blue is hard to see against sky |
| generic | White | Add thin black outline or glow for contrast |

These are Inspector/material tweaks, not code changes. Adjust in the Editor based on outdoor testing.

---

## Unity Inspector Steps (Manual)

1. Open `Assets/IRIS/Prefabs/AnchorPrefab.prefab`
2. On the MeshRenderer, swap the material to `MarkerAR_Unlit`
3. Adjust Transform Scale to `(0.3, 1.0, 0.3)` or preferred size
4. Test in Editor Play mode — markers should appear as bright, unlit objects

---

## Verification

### In Editor
1. Temporarily set `IRISManager.IsPassthroughMode = true`
2. Place markers from the dashboard
3. Verify markers are visible, correctly scaled, and labeled
4. Move the camera around — labels should be readable from all angles

### On Quest
1. Build APK with the new prefab settings
2. In passthrough mode, place a marker
3. Verify:
   - Marker is visible against the outdoor background
   - Size is reasonable (not towering or tiny)
   - Label is readable
   - Color is distinguishable by type

---

## Notes

- **Don't over-polish:** This is a demo. Functional markers that are visible are more important than beautiful markers that took hours to design.
- **Test outdoors first:** What looks good in the Editor may be invisible outdoors. The most important thing is contrast against grass, sky, and buildings.
- **Passthrough depth:** Quest 3's passthrough renders behind all scene geometry by default (the OVRPassthroughLayer overlay type). This means markers will always render on top of the real world, which is the desired behavior. No depth compositing changes needed.
- **Performance:** Unlit materials are cheaper to render than lit ones. This is a minor benefit for Quest's mobile GPU.
