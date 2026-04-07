using System.Collections;
using UnityEngine;
using IRIS.Anchors;
using IRIS.Networking;

namespace IRIS.Core
{
    public class IRISManager : MonoBehaviour
    {
        [SerializeField] private AnchorManager anchorManager;
        [SerializeField] private C2Client c2Client;
        [SerializeField] private bool autoLiftRigAboveTerrainOnStart = true;
        [SerializeField] private float eyeHeightAboveGround = 1.6f;
        [SerializeField] private float raycastStartHeight = 200f;
        [SerializeField] private int maxGroundCheckAttempts = 120;
        [SerializeField] private float groundCheckIntervalSeconds = 0.25f;

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

            Application.runInBackground = true;
            Debug.Log("[IRISManager] IRIS system initialized");
        }

        private void Start()
        {
            if (autoLiftRigAboveTerrainOnStart)
            {
                StartCoroutine(AlignRigToTerrainWhenReady());
            }
        }

        private IEnumerator AlignRigToTerrainWhenReady()
        {
            for (var attempt = 0; attempt < maxGroundCheckAttempts; attempt++)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    var origin = mainCamera.transform.position + Vector3.up * raycastStartHeight;
                    if (Physics.Raycast(origin, Vector3.down, out var hit, raycastStartHeight * 2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var targetEyeY = hit.point.y + eyeHeightAboveGround;
                        var currentEyeY = mainCamera.transform.position.y;
                        var deltaY = targetEyeY - currentEyeY;

                        if (Mathf.Abs(deltaY) > 0.01f)
                        {
                            var rigRoot = mainCamera.transform.root;
                            rigRoot.position += Vector3.up * deltaY;
                            Debug.Log($"[IRISManager] Adjusted XR rig by {deltaY:F2}m to start above terrain");
                        }

                        yield break;
                    }
                }

                yield return new WaitForSeconds(groundCheckIntervalSeconds);
            }

            Debug.LogWarning("[IRISManager] Could not find terrain collider at startup; leaving rig position unchanged");
        }
    }
}
