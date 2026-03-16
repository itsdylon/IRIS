using System;
using System.Threading.Tasks;
using UnityEngine;
using IRIS.Networking;

namespace IRIS.Anchors
{
    public class SpatialAnchorManager : MonoBehaviour
    {
        [SerializeField] private bool useSimulation = true;
        [SerializeField] private C2Client c2Client;
        [SerializeField] private GameObject spatialAnchorPrefab;

        public ISpatialAnchorProvider Provider { get; private set; }

        private void Awake()
        {
            if (useSimulation)
            {
                Provider = new SimulatedSpatialAnchorProvider(c2Client);
                Debug.Log("[SpatialAnchorManager] Using SimulatedSpatialAnchorProvider");
            }
            else
            {
                Provider = new OVRSpatialAnchorProvider(spatialAnchorPrefab);
                Debug.Log("[SpatialAnchorManager] Using OVRSpatialAnchorProvider");
            }
        }

        public async Task<string> CreateAndShareCalibrationAnchor(Pose pose, Guid groupUuid)
        {
            var anchorId = await Provider.CreateAnchorAsync(pose);
            await Provider.SaveAnchorAsync(anchorId);
            await Provider.ShareAnchorAsync(anchorId, groupUuid);
            Debug.Log($"[SpatialAnchorManager] Calibration anchor {anchorId} created and shared");
            return anchorId;
        }

        public async Task<Pose?> LoadCalibrationAnchor(Guid groupUuid)
        {
            var anchors = await Provider.LoadSharedAnchorsAsync(groupUuid);
            if (anchors.Count > 0)
            {
                Debug.Log($"[SpatialAnchorManager] Loaded calibration anchor {anchors[0].id}");
                return anchors[0].pose;
            }

            Debug.LogWarning("[SpatialAnchorManager] No calibration anchors found");
            return null;
        }
    }
}
