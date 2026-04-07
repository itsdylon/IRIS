using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using CesiumForUnity;
using Unity.Mathematics;
using IRIS.Geo;
using IRIS.Markers;
using IRIS.Networking;

namespace IRIS.Anchors
{
    public class AnchorManager : MonoBehaviour
    {
        [SerializeField] private GameObject anchorPrefab;
        [SerializeField] private C2Client c2Client;
        [SerializeField] private CesiumGeoreference georeference;
        [SerializeField] private TerrainHeightSampler terrainHeightSampler;
        [FormerlySerializedAs("markerAltitude")]
        [SerializeField] private float markerHeightOffset = 2f;
        /// <summary>WGS84 ellipsoid height (m) when terrain sampling is unavailable — match CesiumGeoreference height (~255 at GT).</summary>
        [SerializeField] private double ellipsoidHeightFallbackMeters = 255.0;
        [SerializeField] private bool spawnTestMarkerOnStart = false;

        private readonly Dictionary<string, GameObject> _activeAnchors = new Dictionary<string, GameObject>();
        private readonly ConcurrentQueue<MarkerData> _pendingCreated = new ConcurrentQueue<MarkerData>();
        private readonly ConcurrentQueue<MarkerData> _pendingUpdated = new ConcurrentQueue<MarkerData>();
        private readonly ConcurrentQueue<string> _pendingDeleted = new ConcurrentQueue<string>();

        private void Start()
        {
            ClearSpawnedAnchorsInScene();

            if (georeference == null)
            {
                georeference = FindObjectOfType<CesiumGeoreference>();
                if (georeference != null)
                    Debug.Log("[AnchorManager] Auto-found CesiumGeoreference in scene");
                else
                    Debug.LogWarning("[AnchorManager] No CesiumGeoreference found — markers won't be geo-positioned");
            }

            if (spawnTestMarkerOnStart)
            {
                SpawnTestMarker();
            }

            if (c2Client != null)
            {
                c2Client.OnMarkerCreated += (m) => _pendingCreated.Enqueue(m);
                c2Client.OnMarkerUpdated += (m) => _pendingUpdated.Enqueue(m);
                c2Client.OnMarkerDeleted += (id) => _pendingDeleted.Enqueue(id);
            }
        }

        private void ClearSpawnedAnchorsInScene()
        {
            var stale = FindObjectsOfType<MarkerRenderer>();
            foreach (var markerRenderer in stale)
            {
                if (markerRenderer != null)
                {
                    Destroy(markerRenderer.gameObject);
                }
            }
            _activeAnchors.Clear();
        }

        private void Update()
        {
            while (_pendingCreated.TryDequeue(out var marker))
                HandleMarkerCreated(marker);

            while (_pendingUpdated.TryDequeue(out var marker))
                HandleMarkerUpdated(marker);

            while (_pendingDeleted.TryDequeue(out var id))
                HandleMarkerDeleted(id);

            if (OVRInput.GetDown(OVRInput.Button.One))
            {
                SpawnMarkerAtController();
            }
        }

        private async void HandleMarkerCreated(MarkerData marker)
        {
            if (_activeAnchors.ContainsKey(marker.id)) return;

            if (marker.lat != 0 && marker.lng != 0)
            {
                var anchor = SpawnAnchor(Vector3.zero, marker);
                _activeAnchors[marker.id] = anchor;

                double height;
                if (terrainHeightSampler != null && terrainHeightSampler.IsAvailable)
                    height = await terrainHeightSampler.SampleHeightAsync(marker.lng, marker.lat, markerHeightOffset);
                else
                    height = ellipsoidHeightFallbackMeters + markerHeightOffset;

                if (anchor == null)
                {
                    Debug.LogWarning($"[AnchorManager] Marker '{marker.id}' destroyed during height sampling — skipping");
                    _activeAnchors.Remove(marker.id);
                    return;
                }

                var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
                if (globeAnchor == null)
                    globeAnchor = anchor.AddComponent<CesiumGlobeAnchor>();
                globeAnchor.longitudeLatitudeHeight = new double3(marker.lng, marker.lat, height);

                SetAnchorType(anchor, marker.type);
                var cam = Camera.main;
                var dist = cam != null ? Vector3.Distance(cam.transform.position, anchor.transform.position) : -1f;
                Debug.Log($"[AnchorManager] Spawned geo marker '{marker.label}' at lat/lng ({marker.lat:F6}, {marker.lng:F6}), height {height:F1}m — {dist:F0}m from camera");

                if (c2Client != null && marker.status != "placed")
                {
                    c2Client.EmitMarkerPlace(marker.id, anchor.transform.position);
                }
            }
            else
            {
                var basePos = georeference != null
                    ? georeference.transform.position + Vector3.up * markerHeightOffset
                    : new Vector3(0f, markerHeightOffset, 0f);

                var anchor = SpawnAnchor(basePos, marker);
                SetAnchorType(anchor, marker.type, isPending: true);
                _activeAnchors[marker.id] = anchor;
                Debug.Log($"[AnchorManager] Spawned pending marker '{marker.label}' at georeference origin (no lat/lng)");
            }
        }

        private void HandleMarkerUpdated(MarkerData marker)
        {
            if (!_activeAnchors.TryGetValue(marker.id, out var anchor)) return;
            if (anchor == null) return;

            // Dashboard markers: keep lat/lng + Cesium so beacons stay fixed to the globe, not the camera.
            if (marker.lat != 0 && marker.lng != 0 && georeference != null)
            {
                if (anchor.transform.parent != georeference.transform)
                    anchor.transform.SetParent(georeference.transform, true);

                var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
                if (globeAnchor == null)
                {
                    globeAnchor = anchor.AddComponent<CesiumGlobeAnchor>();
                    globeAnchor.longitudeLatitudeHeight = new double3(marker.lng, marker.lat, ellipsoidHeightFallbackMeters + markerHeightOffset);
                }
                else
                {
                    var h = globeAnchor.longitudeLatitudeHeight.z;
                    globeAnchor.longitudeLatitudeHeight = new double3(marker.lng, marker.lat, h);
                }
            }
            else if (marker.position != null)
            {
                var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
                if (globeAnchor != null)
                    Destroy(globeAnchor);

                anchor.transform.position = marker.GetPositionVector();
                anchor.transform.SetParent(null, true);
            }

            SetAnchorType(anchor, marker.type);
            Debug.Log($"[AnchorManager] Marker '{marker.label}' updated");
        }

        private void HandleMarkerDeleted(string markerId)
        {
            if (!_activeAnchors.TryGetValue(markerId, out var anchor)) return;

            if (anchor != null)
            {
                Destroy(anchor);
            }

            _activeAnchors.Remove(markerId);
            Debug.Log($"[AnchorManager] Destroyed marker {markerId}");
        }

        private void SetAnchorType(GameObject anchor, string type, bool isPending = false)
        {
            if (anchor == null) return;

            var visualizer = anchor.GetComponent<AnchorVisualizer>();
            if (visualizer == null) return;

            if (isPending)
            {
                visualizer.SetTypePending(type);
                return;
            }

            visualizer.SetType(type);
        }

        public async void SpawnTestMarker()
        {
            var data = new MarkerData("test-001", "Test Marker", "hardcoded");
            var anchor = SpawnAnchor(Vector3.zero, data);

            double height;
            if (terrainHeightSampler != null && terrainHeightSampler.IsAvailable)
                height = await terrainHeightSampler.SampleHeightAsync(-84.3963, 33.7756, markerHeightOffset);
            else
                height = ellipsoidHeightFallbackMeters + markerHeightOffset;

            if (anchor == null)
            {
                Debug.LogWarning("[AnchorManager] Test marker destroyed during height sampling — skipping");
                return;
            }

            var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
            if (globeAnchor == null)
                globeAnchor = anchor.AddComponent<CesiumGlobeAnchor>();
            globeAnchor.longitudeLatitudeHeight = new double3(-84.3963, 33.7756, height);
            Debug.Log($"[AnchorManager] Spawned test marker at GT campus origin, height {height:F1}m");
        }

        public GameObject SpawnAnchor(Vector3 position, MarkerData data)
        {
            if (anchorPrefab == null)
            {
                Debug.LogError("[AnchorManager] anchorPrefab is not assigned!");
                return null;
            }

            var parent = georeference != null ? georeference.transform : null;
            var anchor = Instantiate(anchorPrefab, position, Quaternion.identity, parent);

            var visualizer = anchor.GetComponent<AnchorVisualizer>();
            if (visualizer != null)
            {
                visualizer.SetLabel(data.label);
            }

            var renderer = anchor.GetComponent<MarkerRenderer>();
            if (renderer != null)
            {
                renderer.Initialize(data);
            }

            return anchor;
        }

        private void SpawnMarkerAtController()
        {
            var controllerPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
            var controllerRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);
            var spawnPos = controllerPos + controllerRot * Vector3.forward * 0.5f;

            EmitMarkerCreateFromWorldPosition(spawnPos, "Placed Marker", "waypoint");
        }

        public void PlaceMarkerAtCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var spawnPos = cam.transform.position + cam.transform.forward * 2f;
            EmitMarkerCreateFromWorldPosition(spawnPos, "Placed Marker", "waypoint");
        }

        private void EmitMarkerCreateFromWorldPosition(Vector3 worldPos, string label, string type)
        {
            if (c2Client == null || georeference == null) return;

            double3 ecef = georeference.TransformUnityPositionToEarthCenteredEarthFixed(new double3(worldPos.x, worldPos.y, worldPos.z));
            double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecef);
            c2Client.EmitMarkerCreate(llh.y, llh.x, label, type);
            Debug.Log($"[AnchorManager] Emitting marker:create at lat/lng ({llh.y:F6}, {llh.x:F6})");
        }

        private void OnDestroy()
        {
            // Lambdas are used for subscriptions so explicit unsubscribe isn't possible,
            // but the queues will simply stop being drained once this object is destroyed.
        }
    }
}
