using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using IRIS.Networking;

namespace IRIS.Anchors
{
    public class SimulatedSpatialAnchorProvider : ISpatialAnchorProvider
    {
        private readonly C2Client _c2Client;
        private readonly Dictionary<string, Pose> _anchors = new Dictionary<string, Pose>();

        public SimulatedSpatialAnchorProvider(C2Client c2Client)
        {
            _c2Client = c2Client;
        }

        public Task<string> CreateAnchorAsync(Pose pose)
        {
            var id = Guid.NewGuid().ToString();
            _anchors[id] = pose;
            Debug.Log($"[SimulatedAnchor] Created anchor {id}");
            return Task.FromResult(id);
        }

        public Task<bool> SaveAnchorAsync(string anchorId)
        {
            Debug.Log($"[SimulatedAnchor] SaveAnchor {anchorId} (no-op)");
            return Task.FromResult(true);
        }

        public Task<bool> ShareAnchorAsync(string anchorId, Guid groupUuid)
        {
            if (!_anchors.TryGetValue(anchorId, out var pose))
            {
                Debug.LogWarning($"[SimulatedAnchor] Anchor {anchorId} not found for sharing");
                return Task.FromResult(false);
            }

            var tcs = new TaskCompletionSource<bool>();

            void OnShared(AnchorSharedPayload payload)
            {
                if (payload.anchorId == anchorId)
                {
                    _c2Client.OnAnchorShared -= OnShared;
                    Debug.Log($"[SimulatedAnchor] Anchor {anchorId} shared confirmed");
                    tcs.TrySetResult(true);
                }
            }

            _c2Client.OnAnchorShared += OnShared;
            _c2Client.EmitAnchorShare(
                null, anchorId, groupUuid.ToString(),
                pose, 0, 0, 0
            );

            return tcs.Task;
        }

        public Task<List<(string id, Pose pose)>> LoadSharedAnchorsAsync(Guid groupUuid)
        {
            var tcs = new TaskCompletionSource<List<(string id, Pose pose)>>();

            void OnLoadResponse(List<AnchorSharedPayload> anchors)
            {
                _c2Client.OnAnchorLoadResponse -= OnLoadResponse;
                var result = new List<(string id, Pose pose)>();
                foreach (var a in anchors)
                {
                    if (a.pose != null)
                    {
                        var pos = new Vector3(a.pose.px, a.pose.py, a.pose.pz);
                        var rot = new Quaternion(a.pose.rx, a.pose.ry, a.pose.rz, a.pose.rw);
                        result.Add((a.anchorId, new Pose(pos, rot)));
                    }
                }
                Debug.Log($"[SimulatedAnchor] Loaded {result.Count} shared anchors");
                tcs.TrySetResult(result);
            }

            _c2Client.OnAnchorLoadResponse += OnLoadResponse;
            _c2Client.EmitAnchorLoad(groupUuid.ToString());

            return tcs.Task;
        }

        public Task<bool> EraseAnchorAsync(string anchorId)
        {
            _anchors.Remove(anchorId);
            _c2Client.EmitAnchorErase(anchorId);
            Debug.Log($"[SimulatedAnchor] Erased anchor {anchorId}");
            return Task.FromResult(true);
        }
    }
}
