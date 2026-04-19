using System;
using System.Threading.Tasks;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;
using IRIS.Core;
using IRIS.Networking;

namespace IRIS.Anchors
{
    public class CalibrationManager : MonoBehaviour
    {
        [SerializeField] private SpatialAnchorManager spatialAnchorManager;
        [SerializeField] private CesiumGeoreference georeference;
        [SerializeField] private C2Client c2Client;

        [Header("Field Calibration (Passthrough Mode)")]
        [Tooltip(
            "Reference latitude for field mode — set in Inspector. Quest has no GPS; this must match where you " +
            "physically calibrate (Y), and dashboard markers must be placed near this lat/lng on the map.")]
        [SerializeField] private double fieldCalibrationLat = 33.769254;
        [Tooltip("Reference longitude for field mode (same rules as Field Calibration Lat).")]
        [SerializeField] private double fieldCalibrationLng = -84.391748;

        public bool IsCalibrated { get; private set; }
        public event Action<bool> OnCalibrationChanged;

        /// <summary>GPS latitude of the calibration point (set during field calibration).</summary>
        public double CalibrationLat { get; private set; }

        /// <summary>GPS longitude of the calibration point (set during field calibration).</summary>
        public double CalibrationLng { get; private set; }

        /// <summary>Unity world position at the moment of calibration.</summary>
        public Vector3 CalibrationUnityPosition { get; private set; }

        /// <summary>True if field calibration data (GPS + Unity position) is available.</summary>
        public bool HasFieldCalibration => IsCalibrated && CalibrationLat != 0;

        private Guid _calibrationGroupUuid;
        private string _currentSessionId;

        private void Start()
        {
            _calibrationGroupUuid = Guid.NewGuid();

            if (c2Client != null)
            {
                c2Client.OnSessionCreated += OnSessionCreated;
                c2Client.OnDeviceRegistered += OnDeviceRegistered;
            }
        }

        private void OnDeviceRegistered(string deviceId)
        {
            if (string.IsNullOrEmpty(_currentSessionId))
            {
                c2Client.EmitSessionCreate();
                Debug.Log("[CalibrationManager] Auto-creating session after device registered");
            }
        }

        private void OnSessionCreated(SessionCreatedPayload payload)
        {
            _currentSessionId = payload.sessionId;
            Debug.Log($"[CalibrationManager] Session created: {_currentSessionId}");
        }

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

        private void ApplyCalibrationOffset(Vector3 offset)
        {
            if (georeference == null) return;

            // Insert an offset parent above the CesiumGeoreference
            var geoTransform = georeference.transform;
            var offsetParent = geoTransform.parent;

            if (offsetParent == null || offsetParent.name != "CalibrationOffset")
            {
                var offsetGo = new GameObject("CalibrationOffset");
                offsetGo.transform.SetParent(geoTransform.parent, false);
                offsetGo.transform.position = geoTransform.position;
                offsetGo.transform.rotation = geoTransform.rotation;
                geoTransform.SetParent(offsetGo.transform, true);
                offsetParent = offsetGo.transform;
            }

            offsetParent.position += offset;
            Debug.Log($"[CalibrationManager] Applied calibration offset: {offset}");
        }

        private void OnDestroy()
        {
            if (c2Client != null)
            {
                c2Client.OnSessionCreated -= OnSessionCreated;
                c2Client.OnDeviceRegistered -= OnDeviceRegistered;
            }
        }
    }
}
