using UnityEngine;
using IRIS.Anchors;

namespace IRIS.Core
{
    public class IRISManager : MonoBehaviour
    {
        [SerializeField] private AnchorManager anchorManager;

        private void Awake()
        {
            if (anchorManager == null)
            {
                anchorManager = GetComponent<AnchorManager>();
            }

            Debug.Log("[IRISManager] IRIS system initialized");
        }
    }
}
