using UnityEngine;
using IRIS.Anchors;
using IRIS.Networking;

namespace IRIS.Core
{
    public class IRISManager : MonoBehaviour
    {
        [SerializeField] private AnchorManager anchorManager;
        [SerializeField] private C2Client c2Client;

        private void Awake()
        {
            if (anchorManager == null)
            {
                anchorManager = GetComponent<AnchorManager>();
            }

            if (c2Client == null)
            {
                c2Client = GetComponent<C2Client>();
            }

            Debug.Log("[IRISManager] IRIS system initialized");
        }
    }
}
