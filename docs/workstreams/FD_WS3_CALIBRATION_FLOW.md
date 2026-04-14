# FD Workstream 3: Field Calibration Flow

**Field Demo Milestone**
**Priority:** HIGH — markers won't appear without calibration
**Dependencies:** WS1 (GPS coordinate bridge — needs `CalibrationLat/Lng/UnityPosition` properties)
**Estimated effort:** ~30 minutes
**Component:** Unity (`unity/IRIS-AR/`)

---

## Context

In Cesium sim mode, calibration creates a spatial anchor and maps it to a Cesium globe position using ECEF conversion. In passthrough mode, there is no Cesium globe — calibration needs to record "I'm standing at GPS (lat, lng) and my Quest tracking position is (x, y, z)". This becomes the origin for all `GeoUtils` conversions.

`CalibrationManager.Calibrate()` currently uses `CesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed()` to get the camera's GPS coordinates. In passthrough mode, this won't work because we've disabled Cesium (WS2) and the camera's position is relative to Quest's tracking origin, not a globe.

**Solution:** In passthrough mode, use hardcoded GPS coordinates for the calibration point (you know where you're standing on the GT green) and record the Quest's current tracking position. All future markers are offset from this anchor.

---

## Tasks

### 3A. Add Passthrough Calibration Path

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs`

**Change 1:** Add serialized fields for the field calibration GPS point. These get set in the Inspector before building (or at runtime via a future UI):

```csharp
[Header("Field Calibration (Passthrough Mode)")]
[Tooltip("GPS latitude of the calibration point on the GT green")]
[SerializeField] private double fieldCalibrationLat = 33.7756;
[Tooltip("GPS longitude of the calibration point on the GT green")]
[SerializeField] private double fieldCalibrationLng = -84.3963;
```

**Change 2:** Modify `Calibrate()` to branch based on mode:

```csharp
public async void Calibrate()
{
    var cam = Camera.main;
    if (cam == null)
    {
        Debug.LogWarning("[CalibrationManager] No main camera found");
        return;
    }

    try
    {
        var camTransform = cam.transform;
        var pose = new Pose(camTransform.position, camTransform.rotation);

        double lat, lng, alt;

        if (IRISManager.IsPassthroughMode)
        {
            // Passthrough: use hardcoded GPS + Quest tracking position
            lat = fieldCalibrationLat;
            lng = fieldCalibrationLng;
            alt = 0; // Ground level in passthrough

            // Store calibration data for GeoUtils conversion (WS1 properties)
            CalibrationLat = lat;
            CalibrationLng = lng;
            CalibrationUnityPosition = camTransform.position;

            Debug.Log($"[CalibrationManager] Field calibration at GPS ({lat:F6}, {lng:F6}), " +
                      $"Unity pos ({camTransform.position.x:F2}, {camTransform.position.y:F2}, {camTransform.position.z:F2})");
        }
        else
        {
            // Cesium sim: existing ECEF conversion
            if (georeference == null)
            {
                Debug.LogWarning("[CalibrationManager] No CesiumGeoreference assigned");
                return;
            }

            double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(
                new double3(pose.position.x, pose.position.y, pose.position.z));
            double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);
            lat = llh.y;
            lng = llh.x;
            alt = llh.z;
        }

        Debug.Log($"[CalibrationManager] Calibrating at lat/lng/alt ({lat:F6}, {lng:F6}, {alt:F2})");

        // Create and share calibration anchor via provider
        var anchorId = await spatialAnchorManager.CreateAndShareCalibrationAnchor(
            pose, _calibrationGroupUuid);

        // Emit with GPS data via C2Client
        c2Client.EmitAnchorShare(
            _currentSessionId, anchorId, _calibrationGroupUuid.ToString(),
            pose, lat, lng, alt);

        IsCalibrated = true;
        OnCalibrationChanged?.Invoke(true);
        Debug.Log($"[CalibrationManager] Calibration complete — anchor {anchorId}");
    }
    catch (Exception ex)
    {
        Debug.LogError($"[CalibrationManager] Calibration failed: {ex.Message}");
    }
}
```

**Note:** The `try/catch` wrapping from the Demo Milestone D4 plan is included here. If D4 was already implemented, this replaces it. If not, you get it for free.

---

### 3B. Add Using for IRISManager

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs`

**Change:** Add at top of file:
```csharp
using IRIS.Core;
```

This is needed for the `IRISManager.IsPassthroughMode` reference.

---

### 3C. Update JoinCalibration for Passthrough

When a second Quest device joins calibration, it needs to use the same GeoUtils-based approach instead of Cesium ECEF conversion.

**File:** `unity/IRIS-AR/Assets/IRIS/Scripts/Anchors/CalibrationManager.cs`

**Change:** Update `JoinCalibration()`:

```csharp
public async Task JoinCalibration(Guid groupUuid, double lat, double lng, double alt)
{
    _calibrationGroupUuid = groupUuid;

    // Load the shared anchor
    var anchorPose = await spatialAnchorManager.LoadCalibrationAnchor(groupUuid);
    if (anchorPose == null)
    {
        Debug.LogWarning("[CalibrationManager] Failed to load calibration anchor");
        return;
    }

    if (IRISManager.IsPassthroughMode)
    {
        // Store the shared GPS calibration point
        CalibrationLat = lat;
        CalibrationLng = lng;
        CalibrationUnityPosition = anchorPose.Value.position;

        Debug.Log($"[CalibrationManager] Joined field calibration at GPS ({lat:F6}, {lng:F6})");
    }
    else
    {
        // Existing Cesium path
        if (georeference == null)
        {
            Debug.LogWarning("[CalibrationManager] No CesiumGeoreference for join calibration");
            return;
        }

        double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
            new double3(lng, lat, alt));
        double3 expectedUnity = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
        var expectedPos = new Vector3(
            (float)expectedUnity.x, (float)expectedUnity.y, (float)expectedUnity.z);

        var offset = expectedPos - anchorPose.Value.position;
        ApplyCalibrationOffset(offset);
    }

    IsCalibrated = true;
    OnCalibrationChanged?.Invoke(true);
    Debug.Log($"[CalibrationManager] Join calibration complete");
}
```

---

## Unity Inspector Steps (Manual)

After applying code changes:

1. Select the **IRISManager** GameObject in the Hierarchy
2. On the **CalibrationManager** component, find the new fields:
   - **Field Calibration Lat**: Set to the latitude of your calibration point on the GT green (default: `33.7756`)
   - **Field Calibration Lng**: Set to the longitude of your calibration point on the GT green (default: `-84.3963`)
3. Before building the APK for field use, update these to the **exact GPS coordinates** of where you plan to stand during calibration. Use a phone GPS app (e.g., Google Maps, GPS Coordinates) to read the coordinates.

---

## Field Calibration Procedure

1. Open a GPS app on your phone
2. Stand at your chosen calibration point on the GT green
3. Note the GPS coordinates (e.g., `33.77542, -84.39618`)
4. Set these coordinates in `CalibrationManager.fieldCalibrationLat` and `fieldCalibrationLng` in the Unity Inspector
5. Build the APK
6. On-site: put on the Quest, stand at the calibration point, press the calibration button (currently Button.One or 'C' key)
7. The system records: "My Quest position right now = this GPS coordinate"
8. All markers are now positioned relative to this anchor

**For multi-device calibration:**
- Have all operators stand at the **same physical point** when calibrating
- Or use the existing `JoinCalibration` flow — second device loads the first device's anchor

---

## Verification

### In Editor (no regression)
1. Enter Play mode with server running
2. Press C to calibrate — should use Cesium ECEF path as before
3. Console shows `Calibrating at lat/lng/alt` with Cesium-derived coordinates

### Simulated Passthrough Test
1. Temporarily add `IsPassthroughMode = true;` at the start of `IRISManager.Awake()` (remove after testing)
2. Enter Play mode, press C
3. Console should show: `[CalibrationManager] Field calibration at GPS (33.775600, -84.396300), Unity pos (...)`
4. Place a marker from the dashboard — it should appear in the scene using GeoUtils positioning (requires WS1)
5. Remove the temporary `IsPassthroughMode = true` line

---

## Notes

- **Why hardcode GPS instead of reading it from the Quest?** Quest 3 has no GPS radio. You'd need an external Bluetooth GPS puck or phone companion app. For a demo, hardcoding the coordinates of a known point is simpler and more reliable.
- **Future improvement:** Add a runtime UI where the operator types in GPS coordinates or receives them from a phone companion app via the server. This would remove the need to rebuild the APK for different calibration points.
- **Accuracy:** The hardcoded point only needs to match the physical standing position within ~3-5m. The GeoUtils projection error at GT-green scale (<200m) is well under 1m, so the dominant error source is how accurately the operator stands on the calibration point.
