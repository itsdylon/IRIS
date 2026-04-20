using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using CesiumForUnity;
using IRIS.Anchors;
using IRIS.Networking;
using IRIS.UI;

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

        [Tooltip(
            "Off (default): VR builds show the Cesium globe and use globe anchors for markers (Quest 2/3 geo testing). " +
            "On: hide Cesium tiles, disable terrain sampler, use field calibration + passthrough-style marker placement.")]
        [SerializeField] private bool passthroughFieldMode = false;

        [Tooltip(
            "Quest passthrough field mode: ask the runtime to suppress Guardian boundary redraws while passthrough is active " +
            "(Meta Boundary Visibility API). Requires Oculus Project Config → Boundary visibility support, and passthrough running.")]
        [SerializeField] private bool suppressBoundaryWhilePassthrough = true;

        /// <summary>True when using real-world / field calibration flow (not the Cesium virtual globe).</summary>
        public static bool IsPassthroughMode { get; private set; }

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

            InitializeFieldStatusHUD();
        }

        private void InitializeFieldStatusHUD()
        {
            try
            {
                // Find dependencies in the scene
                var calibrationManager = FindObjectOfType<CalibrationManager>();
                var anchorManager = FindObjectOfType<AnchorManager>();

                if (calibrationManager == null)
                {
                    Debug.LogWarning("[IRISManager] CalibrationManager not found; HUD cannot initialize");
                    return;
                }

                // Create HUD GameObject and attach component
                var hudGo = new GameObject("FieldStatusHUD_Instance");
                var hud = hudGo.AddComponent<FieldStatusHUD>();

                // Wire up references via reflection (private [SerializeField] fields)
                var c2Field = typeof(FieldStatusHUD).GetField("c2Client", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var calibField = typeof(FieldStatusHUD).GetField("calibrationManager", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var anchorField = typeof(FieldStatusHUD).GetField("anchorManager", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (c2Field != null)
                    c2Field.SetValue(hud, c2Client);
                if (calibField != null)
                    calibField.SetValue(hud, calibrationManager);
                if (anchorField != null)
                    anchorField.SetValue(hud, anchorManager);

                Debug.Log("[IRISManager] FieldStatusHUD initialized successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[IRISManager] Failed to initialize FieldStatusHUD: {ex.Message}");
            }
        }

        private void Start()
        {
            ConfigureRuntimeCameraRig();

            if (passthroughFieldMode && _isVrRuntime)
            {
                StartCoroutine(EnableInsightPassthroughNextFrame());
            }

            var onQuest = Application.platform == RuntimePlatform.Android;
            if (autoLiftRigAboveTerrainOnStart && !(onQuest && disableTerrainLiftOnAndroid))
            {
                StartCoroutine(AlignRigToTerrainWhenReady());
            }
        }

        /// <summary>
        /// OVRManager may not assign <see cref="OVRManager.instance"/> until OVRCameraRig awakens; wait one frame.
        /// </summary>
        private IEnumerator EnableInsightPassthroughNextFrame()
        {
            yield return null;
            var ovr = OVRManager.instance != null ? OVRManager.instance : Object.FindObjectOfType<OVRManager>();
            if (ovr == null)
            {
                Debug.LogWarning(
                    "[IRISManager] Passthrough field mode: no OVRManager found. Add OVRCameraRig (with OVRManager) to the scene.");
                yield break;
            }

            ovr.isInsightPassthroughEnabled = true;
            Debug.Log("[IRISManager] Enabled OVRManager.isInsightPassthroughEnabled for camera passthrough.");

            yield return null;
            if (suppressBoundaryWhilePassthrough)
            {
                ovr.shouldBoundaryVisibilityBeSuppressed = ovr.isInsightPassthroughEnabled;
                Debug.Log(
                    "[IRISManager] Requested Guardian boundary visibility suppression while passthrough is active " +
                    $"(isBoundaryVisibilitySuppressed={ovr.isBoundaryVisibilitySuppressed}).");
            }
        }

        private void Update()
        {
            if (enableThumbstickLocomotion && _isVrRuntime)
            {
                ApplyThumbstickLocomotion();
            }

            // Keep aligned with passthrough state every frame (Meta OVRManager requirement).
            if (_isVrRuntime && IsPassthroughMode && suppressBoundaryWhilePassthrough)
            {
                var ovr = OVRManager.instance;
                if (ovr != null)
                    ovr.shouldBoundaryVisibilityBeSuppressed = ovr.isInsightPassthroughEnabled;
            }
        }

        private void ConfigureRuntimeCameraRig()
        {
            _isVrRuntime = Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive;
            IsPassthroughMode = _isVrRuntime && passthroughFieldMode;
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

            if (!passthroughFieldMode)
            {
                Debug.Log("[IRISManager] Cesium globe mode on VR — tilesets stay enabled.");
                return;
            }

            // Passthrough / field mode: hide globe tiles (they would cover the real world)
            var tilesets = FindObjectsOfType<Cesium3DTileset>();
            foreach (var tileset in tilesets)
            {
                tileset.gameObject.SetActive(false);
                Debug.Log($"[IRISManager] Disabled Cesium tileset: {tileset.gameObject.name}");
            }

            var heightSampler = FindObjectOfType<IRIS.Geo.TerrainHeightSampler>();
            if (heightSampler != null)
            {
                heightSampler.enabled = false;
                Debug.Log("[IRISManager] Disabled TerrainHeightSampler for passthrough field mode");
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
