using System;
using System.Threading.Tasks;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;
using IRIS.Networking;

namespace IRIS.Anchors
{
    public class CalibrationManager : MonoBehaviour
    {
        [SerializeField] private SpatialAnchorManager spatialAnchorManager;
        [SerializeField] private CesiumGeoreference georeference;
        [SerializeField] private C2Client c2Client;

        public bool IsCalibrated { get; private set; }
        public event Action<bool> OnCalibrationChanged;

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

            if (georeference == null)
            {
                Debug.LogWarning("[CalibrationManager] No CesiumGeoreference assigned");
                return;
            }

            var camTransform = cam.transform;
            var pose = new Pose(camTransform.position, camTransform.rotation);

            // Convert camera position to GPS
            double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(
                new double3(pose.position.x, pose.position.y, pose.position.z));
            double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);
            double lat = llh.y;
            double lng = llh.x;
            double alt = llh.z;

            Debug.Log($"[CalibrationManager] Calibrating at lat/lng/alt ({lat:F6}, {lng:F6}, {alt:F2})");

            // Create and share calibration anchor via provider
            var anchorId = await spatialAnchorManager.CreateAndShareCalibrationAnchor(pose, _calibrationGroupUuid);

            // Also emit with GPS data via C2Client
            c2Client.EmitAnchorShare(
                _currentSessionId, anchorId, _calibrationGroupUuid.ToString(),
                pose, lat, lng, alt);

            IsCalibrated = true;
            OnCalibrationChanged?.Invoke(true);
            Debug.Log($"[CalibrationManager] Calibration complete — anchor {anchorId}");
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

            // Compute where the GPS calibration point should be in Unity space
            double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
                new double3(lng, lat, alt));
            double3 expectedUnity = georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
            var expectedPos = new Vector3((float)expectedUnity.x, (float)expectedUnity.y, (float)expectedUnity.z);

            // Calculate offset between anchor pose and expected Cesium position
            var offset = expectedPos - anchorPose.Value.position;
            ApplyCalibrationOffset(offset);

            IsCalibrated = true;
            OnCalibrationChanged?.Invoke(true);
            Debug.Log($"[CalibrationManager] Joined calibration — offset ({offset.x:F3}, {offset.y:F3}, {offset.z:F3})");
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
