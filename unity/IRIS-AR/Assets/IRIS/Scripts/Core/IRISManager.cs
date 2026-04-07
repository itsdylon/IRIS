using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using IRIS.Anchors;
using IRIS.Networking;

namespace IRIS.Core
{
    public class IRISManager : MonoBehaviour
    {
        [SerializeField] private AnchorManager anchorManager;
        [SerializeField] private C2Client c2Client;
        [SerializeField] private bool autoLiftRigAboveTerrainOnStart = true;
        [Tooltip("Quest: leave on so Cesium raycasts do not fight Meta floor height (often caused starting too high / drifting up).")]
        [SerializeField] private bool disableTerrainLiftOnAndroid = true;
        [SerializeField] private float eyeHeightAboveGround = 1.6f;
        [SerializeField] private float raycastStartHeight = 200f;
        [SerializeField] private int maxGroundCheckAttempts = 120;
        [SerializeField] private float groundCheckIntervalSeconds = 0.25f;
        [SerializeField] private bool enableThumbstickLocomotion = true;
        [SerializeField] private float thumbstickMoveSpeed = 2f;
        [SerializeField] private float thumbstickDeadzone = 0.15f;

        private bool _isVrRuntime;
        private bool _terrainAlignFinished;

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
            ConfigureRuntimeCameraRig();

            var onQuest = Application.platform == RuntimePlatform.Android;
            if (autoLiftRigAboveTerrainOnStart && !(onQuest && disableTerrainLiftOnAndroid))
            {
                StartCoroutine(AlignRigToTerrainWhenReady());
            }
        }

        private void Update()
        {
            if (enableThumbstickLocomotion && _isVrRuntime)
            {
                ApplyThumbstickLocomotion();
            }
        }

        private void ConfigureRuntimeCameraRig()
        {
            _isVrRuntime = Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive;
            if (!_isVrRuntime) return;

            var ovrRig = Resources.FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go => go.name == "OVRCameraRig" && go.scene.isLoaded);
            if (ovrRig != null && !ovrRig.activeSelf)
            {
                ovrRig.SetActive(true);
                Debug.Log("[IRISManager] Enabled OVRCameraRig for VR runtime");
            }

            var flyCamera = GameObject.Find("FlyCamera");
            if (flyCamera != null && flyCamera.activeSelf)
            {
                flyCamera.SetActive(false);
                Debug.Log("[IRISManager] Disabled FlyCamera for VR runtime");
            }
        }

        private IEnumerator AlignRigToTerrainWhenReady()
        {
            // One-shot only: Cesium terrain LOD refines over time; repeating lifts caused the rig to
            // creep upward every guard tick as the raycast hit moved up with higher-res tiles.
            for (var attempt = 0; attempt < maxGroundCheckAttempts; attempt++)
            {
                if (TryLiftCameraAboveTerrainOnce())
                    yield break;

                yield return new WaitForSeconds(groundCheckIntervalSeconds);
            }
        }

        private bool TryLiftCameraAboveTerrainOnce()
        {
            if (_terrainAlignFinished)
                return true;

            var mainCamera = Camera.main;
            if (mainCamera == null)
                return false;

            var origin = mainCamera.transform.position + Vector3.up * raycastStartHeight;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, raycastStartHeight * 2f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            var minimumEyeY = hit.point.y + eyeHeightAboveGround;
            var currentEyeY = mainCamera.transform.position.y;
            if (currentEyeY + 0.01f < minimumEyeY)
            {
                var deltaY = minimumEyeY - currentEyeY;
                var rigRoot = mainCamera.transform.root;
                rigRoot.position += Vector3.up * deltaY;
                Debug.Log($"[IRISManager] One-shot terrain lift: {deltaY:F2}m (eye above hit)");
            }

            _terrainAlignFinished = true;
            return true;
        }

        private void ApplyThumbstickLocomotion()
        {
            var stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            if (stick.magnitude < thumbstickDeadzone) return;

            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            var forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up).normalized;
            var movement = (forward * stick.y + right * stick.x) * (thumbstickMoveSpeed * Time.deltaTime);
            var rigRoot = mainCamera.transform.root;
            rigRoot.position += movement;
        }
    }
}
