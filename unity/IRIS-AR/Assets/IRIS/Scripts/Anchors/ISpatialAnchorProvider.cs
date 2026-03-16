using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace IRIS.Anchors
{
    public interface ISpatialAnchorProvider
    {
        Task<string> CreateAnchorAsync(Pose pose);
        Task<bool> SaveAnchorAsync(string anchorId);
        Task<bool> ShareAnchorAsync(string anchorId, Guid groupUuid);
        Task<List<(string id, Pose pose)>> LoadSharedAnchorsAsync(Guid groupUuid);
        Task<bool> EraseAnchorAsync(string anchorId);
    }
}
