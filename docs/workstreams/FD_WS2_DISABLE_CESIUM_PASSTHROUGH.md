# FD Workstream 2: Disable Cesium in Passthrough Mode

**Field Demo Milestone**
**Priority:** HIGH — Cesium tiles will block passthrough if left active
**Dependencies:** None — can start in parallel with WS1
**Estimated effort:** ~30 minutes
**Component:** Unity (`unity/IRIS-AR/`)

---

## Context

When Quest 3 runs in passthrough mode, the user sees the real world through the headset's cameras. But Cesium's 3D tilesets (OSM buildings + terrain) will render as opaque geometry on top of the passthrough feed, completely blocking the view of the real world.

`IRISManager.ConfigureRuntimeCameraRig()` already detects Quest hardware and swaps to `OVRCameraRig`. This workstream extends that method to also disable Cesium rendering components so only the passthrough feed and IRIS overlays (markers, HUD) are visible.

---

## Tasks

### 2A. Disable Cesium Tilesets and Terrain on Quest

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Core/IRISManager.cs`

**Change:** Extend `ConfigureRuntimeCameraRig()` to find and disable Cesium rendering. Add the following block after the existing FlyCamera disable (after line 81):

```csharp
// Disable Cesium 3D tilesets — they'd render over passthrough
var tilesets = FindObjectsOfType<CesiumForUnity.Cesium3DTileset>();
foreach (var tileset in tilesets)
{
    tileset.gameObject.SetActive(false);
    Debug.Log($"[IRISManager] Disabled Cesium tileset: {tileset.gameObject.name}");
}
```

**Required using:** Add at the top of the file:
```csharp
using CesiumForUnity;
```

**What this disables in the current scene:**
- `Cesium OSM Buildings` — the 3D building tileset (Ion Asset 96188)
- `Terrain` — the Cesium terrain tileset

**What stays active:**
- `CesiumGeoreference` — the parent transform stays active. It's needed if we ever want to use Cesium coordinate conversion in passthrough mode. But in the WS1 GeoUtils path, it's simply unused and harmless.

---

### 2B. Disable TerrainHeightSampler on Quest

`TerrainHeightSampler` depends on `Cesium3DTileset.SampleHeightMostDetailed()`. With tilesets disabled, sampling would fail or return garbage. Disable it explicitly.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Core/IRISManager.cs`

**Change:** Add after the tileset disable block:

```csharp
// Disable terrain height sampler — no Cesium terrain in passthrough
var heightSampler = FindObjectOfType<IRIS.Geo.TerrainHeightSampler>();
if (heightSampler != null)
{
    heightSampler.enabled = false;
    Debug.Log("[IRISManager] Disabled TerrainHeightSampler for passthrough mode");
}
```

---

### 2C. Skip Terrain Alignment on Quest

`IRISManager.Start()` already conditionally skips `AlignRigToTerrainWhenReady()` on Android when `disableTerrainLiftOnAndroid` is true (which is the default). This means no code change is needed here — just verify the default is `true`.

**Verification:** In the Inspector, confirm `IRISManager.disableTerrainLiftOnAndroid` is checked (true). It is by default.

---

## Full Method After Changes

For reference, `ConfigureRuntimeCameraRig()` should look like this after all changes:

```csharp
private void ConfigureRuntimeCameraRig()
{
    _isVrRuntime = Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive;
    IsPassthroughMode = _isVrRuntime; // From WS1

    if (!_isVrRuntime) return;

    // Enable OVRCameraRig
    var ovrRig = Resources.FindObjectsOfTypeAll<GameObject>()
        .FirstOrDefault(go => go.name == "OVRCameraRig" && go.scene.isLoaded);
    if (ovrRig != null && !ovrRig.activeSelf)
    {
        ovrRig.SetActive(true);
        Debug.Log("[IRISManager] Enabled OVRCameraRig for VR runtime");
    }

    // Disable FlyCamera
    var flyCamera = GameObject.Find("FlyCamera");
    if (flyCamera != null && flyCamera.activeSelf)
    {
        flyCamera.SetActive(false);
        Debug.Log("[IRISManager] Disabled FlyCamera for VR runtime");
    }

    // Disable Cesium 3D tilesets — they'd render over passthrough
    var tilesets = FindObjectsOfType<Cesium3DTileset>();
    foreach (var tileset in tilesets)
    {
        tileset.gameObject.SetActive(false);
        Debug.Log($"[IRISManager] Disabled Cesium tileset: {tileset.gameObject.name}");
    }

    // Disable terrain height sampler — no Cesium terrain in passthrough
    var heightSampler = FindObjectOfType<IRIS.Geo.TerrainHeightSampler>();
    if (heightSampler != null)
    {
        heightSampler.enabled = false;
        Debug.Log("[IRISManager] Disabled TerrainHeightSampler for passthrough mode");
    }
}
```

---

## Unity Inspector Steps (Manual)

None — all changes are code-only. Cesium objects remain in the scene for Editor/sim use but are disabled at runtime on Quest.

---

## Verification

### In Editor (no regression)
1. Enter Play mode — Cesium terrain + buildings should still render normally
2. `IRISManager.IsPassthroughMode` should be `false`
3. All Cesium tilesets remain active

### On Quest (passthrough)
1. Build APK, deploy to Quest
2. On launch, console should log:
   - `[IRISManager] Enabled OVRCameraRig for VR runtime`
   - `[IRISManager] Disabled FlyCamera for VR runtime`
   - `[IRISManager] Disabled Cesium tileset: Cesium OSM Buildings`
   - `[IRISManager] Disabled Cesium tileset: Terrain`
   - `[IRISManager] Disabled TerrainHeightSampler for passthrough mode`
3. User should see the real world through passthrough — no 3D buildings or terrain overlay
4. IRIS markers (once WS1/WS3 are done) should render as overlays on top of passthrough

---

## Notes

- The `CesiumGeoreference` GameObject is deliberately **not** disabled. Even though passthrough mode uses `GeoUtils` instead of Cesium for marker placement, keeping the georeference alive avoids null reference errors in any code that still holds a serialized reference to it. It just won't do anything meaningful.
- If you later want a "hybrid" mode (Cesium buildings overlaid on passthrough), you can toggle individual tilesets back on. The passthrough layer renders behind scene geometry by default, so Cesium buildings would occlude correctly.
