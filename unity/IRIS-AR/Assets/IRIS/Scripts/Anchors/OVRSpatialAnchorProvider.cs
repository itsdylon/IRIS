using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace IRIS.Anchors
{
    public class OVRSpatialAnchorProvider : ISpatialAnchorProvider
    {
        private readonly GameObject _anchorPrefab;
        private readonly Dictionary<string, OVRSpatialAnchor> _activeAnchors = new Dictionary<string, OVRSpatialAnchor>();

        public OVRSpatialAnchorProvider(GameObject anchorPrefab)
        {
            _anchorPrefab = anchorPrefab;
        }

        public async Task<string> CreateAnchorAsync(Pose pose)
        {
            var go = UnityEngine.Object.Instantiate(_anchorPrefab, pose.position, pose.rotation);
            var spatialAnchor = go.GetComponent<OVRSpatialAnchor>();
            if (spatialAnchor == null)
                spatialAnchor = go.AddComponent<OVRSpatialAnchor>();

            while (!spatialAnchor.Created)
                await Task.Yield();

            var uuid = spatialAnchor.Uuid.ToString();
            _activeAnchors[uuid] = spatialAnchor;
            Debug.Log($"[OVRAnchor] Created anchor {uuid}");
            return uuid;
        }

        public async Task<bool> SaveAnchorAsync(string anchorId)
        {
            if (!_activeAnchors.TryGetValue(anchorId, out var anchor))
            {
                Debug.LogWarning($"[OVRAnchor] Anchor {anchorId} not found for save");
                return false;
            }

            var tcs = new TaskCompletionSource<bool>();
            anchor.Save((a, success) => tcs.TrySetResult(success));
            var result = await tcs.Task;
            Debug.Log($"[OVRAnchor] Save {anchorId}: {result}");
            return result;
        }

        public async Task<bool> ShareAnchorAsync(string anchorId, Guid groupUuid)
        {
            if (!_activeAnchors.TryGetValue(anchorId, out var anchor))
            {
                Debug.LogWarning($"[OVRAnchor] Anchor {anchorId} not found for share");
                return false;
            }

            var anchors = new OVRSpatialAnchor[] { anchor };
            var users = new OVRSpaceUser[] { };
            var tcs = new TaskCompletionSource<bool>();
            OVRSpatialAnchor.ShareAsync(anchors, users)
                .ContinueWith(result => tcs.TrySetResult(result.IsSuccess()));
            var success = await tcs.Task;
            Debug.Log($"[OVRAnchor] Share {anchorId}: {success}");
            return success;
        }

        public async Task<List<(string id, Pose pose)>> LoadSharedAnchorsAsync(Guid groupUuid)
        {
            var result = new List<(string id, Pose pose)>();
            var uuids = new Guid[] { groupUuid };

            var tcs = new TaskCompletionSource<bool>();
            var options = new OVRSpatialAnchor.LoadOptions
            {
                Uuids = uuids,
                MaxAnchorCount = 10,
                StorageLocation = OVRSpace.StorageLocation.Cloud,
            };

            OVRSpatialAnchor.LoadUnboundAnchors(options, (unboundAnchors) =>
            {
                if (unboundAnchors != null)
                {
                    foreach (var unbound in unboundAnchors)
                    {
                        var go = UnityEngine.Object.Instantiate(_anchorPrefab);
                        unbound.BindTo(go.GetComponent<OVRSpatialAnchor>());
                        var pose = new Pose(go.transform.position, go.transform.rotation);
                        result.Add((unbound.Uuid.ToString(), pose));
                    }
                }
                tcs.TrySetResult(true);
            });

            await tcs.Task;
            Debug.Log($"[OVRAnchor] Loaded {result.Count} anchors for group {groupUuid}");
            return result;
        }

        public async Task<bool> EraseAnchorAsync(string anchorId)
        {
            if (!_activeAnchors.TryGetValue(anchorId, out var anchor))
            {
                Debug.LogWarning($"[OVRAnchor] Anchor {anchorId} not found for erase");
                return false;
            }

            var tcs = new TaskCompletionSource<bool>();
            anchor.Erase((a, success) => tcs.TrySetResult(success));
            var result = await tcs.Task;

            if (result)
            {
                _activeAnchors.Remove(anchorId);
                UnityEngine.Object.Destroy(anchor.gameObject);
            }

            Debug.Log($"[OVRAnchor] Erase {anchorId}: {result}");
            return result;
        }
    }
}
