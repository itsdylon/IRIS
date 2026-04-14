# FD Workstream 1: GPS-to-Local Coordinate Bridge

**Field Demo Milestone**
**Priority:** CRITICAL — everything else depends on this
**Dependencies:** None — starts immediately
**Estimated effort:** ~2 hours
**Component:** Unity (`unity/IRIS-AR/`)

---

## Context

Today, `AnchorManager.HandleMarkerCreated()` places markers using `CesiumGlobeAnchor.longitudeLatitudeHeight`, which pins objects to Cesium's virtual globe. In passthrough AR, there is no globe — the Quest only knows its local tracking space (meters from where it powered on).

The project already has `GeoUtils.cs` with simple cartographic projection math (`x = (lng - refLng) * 111320 * cos(refLat)`, `z = (lat - refLat) * 110540`). This produces local meter offsets from a reference GPS point. The error is <1m within 500m of the reference — more than adequate for the GT green (~200m across).

This workstream adds a platform toggle so `AnchorManager` uses `GeoUtils` instead of `CesiumGlobeAnchor` when running in passthrough AR mode.

---

## Tasks

### 1A. Add `IsPassthroughMode` to IRISManager

`IRISManager` already detects VR runtime in `ConfigureRuntimeCameraRig()`. Expose this as a static property so other scripts can query it.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Core/IRISManager.cs`

**Change:** Add a public static property after the `_isVrRuntime` field (line 25):

```csharp
/// <summary>True when running on Quest hardware with passthrough (not Cesium sim).</summary>
public static bool IsPassthroughMode { get; private set; }
```

Set it in `ConfigureRuntimeCameraRig()` after line 65:
```csharp
_isVrRuntime = Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive;
IsPassthroughMode = _isVrRuntime;
```

**Why static?** Multiple scripts (`AnchorManager`, `CalibrationManager`) need to check this without requiring a serialized reference to `IRISManager`.

---

### 1B. Add Calibration Reference Point Storage

When the user calibrates in the field, we need to store the GPS coordinates of their standing position. This becomes the origin for all `GeoUtils` conversions.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs`

**Change:** Add public properties to expose the calibration GPS point:

```csharp
/// <summary>GPS latitude of the calibration point (set during field calibration).</summary>
public double CalibrationLat { get; private set; }

/// <summary>GPS longitude of the calibration point (set during field calibration).</summary>
public double CalibrationLng { get; private set; }

/// <summary>Unity world position at the moment of calibration.</summary>
public Vector3 CalibrationUnityPosition { get; private set; }

/// <summary>True if field calibration data (GPS + Unity position) is available.</summary>
public bool HasFieldCalibration => IsCalibrated && CalibrationLat != 0;
```

These get set during the calibration flow (WS3).

---

### 1C. Add GeoUtils Marker Placement Path to AnchorManager

This is the core change. `AnchorManager.HandleMarkerCreated()` currently has one path: Cesium. Add a second path using `GeoUtils` when in passthrough mode.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

**Change 1:** Add a serialized reference to `CalibrationManager`:

```csharp
[SerializeField] private CalibrationManager calibrationManager;
```

**Change 2:** Replace `HandleMarkerCreated()` to add the GeoUtils path. The new method:

```csharp
private async void HandleMarkerCreated(MarkerData marker)
{
    if (_activeAnchors.ContainsKey(marker.id)) return;

    if (marker.lat != 0 && marker.lng != 0)
    {
        if (IRISManager.IsPassthroughMode)
        {
            HandleMarkerCreatedPassthrough(marker);
        }
        else
        {
            await HandleMarkerCreatedCesium(marker);
        }
    }
    else
    {
        // No lat/lng — spawn at origin as pending
        var basePos = georeference != null
            ? georeference.transform.position + Vector3.up * markerHeightOffset
            : new Vector3(0f, markerHeightOffset, 0f);

        var anchor = SpawnAnchor(basePos, marker);
        SetAnchorType(anchor, marker.type, isPending: true);
        _activeAnchors[marker.id] = anchor;
        Debug.Log($"[AnchorManager] Spawned pending marker '{marker.label}' at origin (no lat/lng)");
    }
}
```

**Change 3:** Extract the existing Cesium path into its own method:

```csharp
private async void HandleMarkerCreatedCesium(MarkerData marker)
{
    var anchor = SpawnAnchor(Vector3.zero, marker);
    _activeAnchors[marker.id] = anchor;

    double height;
    if (terrainHeightSampler != null && terrainHeightSampler.IsAvailable)
        height = await terrainHeightSampler.SampleHeightAsync(marker.lng, marker.lat, markerHeightOffset);
    else
        height = ellipsoidHeightFallbackMeters + markerHeightOffset;

    if (anchor == null)
    {
        Debug.LogWarning($"[AnchorManager] Marker '{marker.id}' destroyed during height sampling — skipping");
        _activeAnchors.Remove(marker.id);
        return;
    }

    var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
    if (globeAnchor == null)
        globeAnchor = anchor.AddComponent<CesiumGlobeAnchor>();
    globeAnchor.longitudeLatitudeHeight = new double3(marker.lng, marker.lat, height);

    SetAnchorType(anchor, marker.type);
    var cam = Camera.main;
    var dist = cam != null ? Vector3.Distance(cam.transform.position, anchor.transform.position) : -1f;
    Debug.Log($"[AnchorManager] Spawned geo marker '{marker.label}' at lat/lng ({marker.lat:F6}, {marker.lng:F6}), height {height:F1}m — {dist:F0}m from camera");

    if (c2Client != null && marker.status != "placed")
    {
        c2Client.EmitMarkerPlace(marker.id, anchor.transform.position);
    }
}
```

**Change 4:** Add the new GeoUtils passthrough path:

```csharp
private void HandleMarkerCreatedPassthrough(MarkerData marker)
{
    if (calibrationManager == null || !calibrationManager.HasFieldCalibration)
    {
        Debug.LogWarning($"[AnchorManager] Cannot place marker '{marker.label}' — no field calibration. Calibrate first.");
        return;
    }

    // Convert marker GPS to local Unity position relative to calibration point
    var localOffset = GeoUtils.LatLngToUnityPosition(
        marker.lat, marker.lng,
        calibrationManager.CalibrationLat, calibrationManager.CalibrationLng);

    // Offset is relative to calibration point — add calibration Unity position
    var worldPos = calibrationManager.CalibrationUnityPosition + localOffset;

    // Override Y to eye-level height offset (ground is real in passthrough)
    worldPos.y = markerHeightOffset;

    var anchor = SpawnAnchorUnparented(worldPos, marker);
    _activeAnchors[marker.id] = anchor;

    SetAnchorType(anchor, marker.type);
    Debug.Log($"[AnchorManager] Spawned passthrough marker '{marker.label}' at ({worldPos.x:F1}, {worldPos.y:F1}, {worldPos.z:F1}) — offset from calibration");

    if (c2Client != null && marker.status != "placed")
    {
        c2Client.EmitMarkerPlace(marker.id, anchor.transform.position);
    }
}
```

**Change 5:** Add `SpawnAnchorUnparented()` — same as `SpawnAnchor()` but without parenting to CesiumGeoreference (which doesn't exist in passthrough mode):

```csharp
private GameObject SpawnAnchorUnparented(Vector3 position, MarkerData data)
{
    if (anchorPrefab == null)
    {
        Debug.LogError("[AnchorManager] anchorPrefab is not assigned!");
        return null;
    }

    var anchor = Instantiate(anchorPrefab, position, Quaternion.identity);

    var visualizer = anchor.GetComponent<AnchorVisualizer>();
    if (visualizer != null)
    {
        visualizer.SetLabel(data.label);
    }

    var renderer = anchor.GetComponent<MarkerRenderer>();
    if (renderer != null)
    {
        renderer.Initialize(data);
    }

    return anchor;
}
```

---

### 1D. Add GeoUtils Reverse Path for Passthrough Marker Creation

When the user places a marker from the Quest controller in passthrough mode, we need to convert the local Unity position back to GPS coordinates using `GeoUtils` instead of Cesium ECEF conversion.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

**Change:** Update `EmitMarkerCreateFromWorldPosition()`:

```csharp
private void EmitMarkerCreateFromWorldPosition(Vector3 worldPos, string label, string type)
{
    if (c2Client == null) return;

    if (IRISManager.IsPassthroughMode)
    {
        if (calibrationManager == null || !calibrationManager.HasFieldCalibration)
        {
            Debug.LogWarning("[AnchorManager] Cannot create marker — no field calibration");
            return;
        }

        // Reverse: local Unity position → GPS via GeoUtils
        var relativePos = worldPos - calibrationManager.CalibrationUnityPosition;
        var (lat, lng) = GeoUtils.UnityPositionToLatLng(
            relativePos,
            calibrationManager.CalibrationLat,
            calibrationManager.CalibrationLng);

        c2Client.EmitMarkerCreate(lat, lng, label, type);
        Debug.Log($"[AnchorManager] Emitting passthrough marker:create at lat/lng ({lat:F6}, {lng:F6})");
    }
    else
    {
        if (georeference == null) return;

        double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(
            new double3(worldPos.x, worldPos.y, worldPos.z));
        double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);
        c2Client.EmitMarkerCreate(llh.y, llh.x, label, type);
        Debug.Log($"[AnchorManager] Emitting marker:create at lat/lng ({llh.y:F6}, {llh.x:F6})");
    }
}
```

---

### 1E. Update HandleMarkerUpdated for Passthrough

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/AnchorManager.cs`

**Change:** Add passthrough branch in `HandleMarkerUpdated()`:

```csharp
private void HandleMarkerUpdated(MarkerData marker)
{
    if (!_activeAnchors.TryGetValue(marker.id, out var anchor)) return;
    if (anchor == null) return;

    if (IRISManager.IsPassthroughMode)
    {
        // In passthrough, update position via GeoUtils
        if (marker.lat != 0 && marker.lng != 0
            && calibrationManager != null && calibrationManager.HasFieldCalibration)
        {
            var localOffset = GeoUtils.LatLngToUnityPosition(
                marker.lat, marker.lng,
                calibrationManager.CalibrationLat, calibrationManager.CalibrationLng);
            var worldPos = calibrationManager.CalibrationUnityPosition + localOffset;
            worldPos.y = markerHeightOffset;
            anchor.transform.position = worldPos;
        }
    }
    else
    {
        // Existing Cesium path (unchanged)
        if (marker.lat != 0 && marker.lng != 0 && georeference != null)
        {
            if (anchor.transform.parent != georeference.transform)
                anchor.transform.SetParent(georeference.transform, true);

            var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
            if (globeAnchor == null)
            {
                globeAnchor = anchor.AddComponent<CesiumGlobeAnchor>();
                globeAnchor.longitudeLatitudeHeight = new double3(marker.lng, marker.lat, ellipsoidHeightFallbackMeters + markerHeightOffset);
            }
            else
            {
                var h = globeAnchor.longitudeLatitudeHeight.z;
                globeAnchor.longitudeLatitudeHeight = new double3(marker.lng, marker.lat, h);
            }
        }
        else if (marker.position != null)
        {
            var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
            if (globeAnchor != null)
                Destroy(globeAnchor);

            anchor.transform.position = marker.GetPositionVector();
            anchor.transform.SetParent(null, true);
        }
    }

    SetAnchorType(anchor, marker.type);
    Debug.Log($"[AnchorManager] Marker '{marker.label}' updated");
}
```

---

## Unity Inspector Steps (Manual)

After applying code changes:

1. Select the **IRISManager** GameObject in the Hierarchy
2. On the **AnchorManager** component, find the new **Calibration Manager** field
3. Drag the **IRISManager** GameObject (which has CalibrationManager) into this field

---

## Verification

1. **Editor (Cesium mode):** Enter Play mode in the Editor — `IRISManager.IsPassthroughMode` should be `false`, markers should use Cesium path as before. No regression.
2. **Quest build:** Build APK, deploy to Quest — `IsPassthroughMode` should be `true`, markers should use GeoUtils path. Markers will not appear until calibration is done (WS3).
3. **GeoUtils accuracy test:** In Editor, temporarily set `IsPassthroughMode = true` and hardcode a calibration point at GT campus (33.7756, -84.3963). Place a marker from the dashboard 50m north. Verify the marker appears ~50m along the Z axis.
