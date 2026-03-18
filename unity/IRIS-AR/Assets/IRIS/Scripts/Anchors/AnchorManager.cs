using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;
using IRIS.Markers;
using IRIS.Networking;

namespace IRIS.Anchors
{
    public class AnchorManager : MonoBehaviour
    {
        [SerializeField] private GameObject anchorPrefab;
        [SerializeField] private C2Client c2Client;
        [SerializeField] private CesiumGeoreference georeference;
        [SerializeField] private float markerAltitude = 2f;
        [SerializeField] private bool spawnTestMarkerOnStart = false;

        private readonly Dictionary<string, GameObject> _activeAnchors = new Dictionary<string, GameObject>();
        private readonly ConcurrentQueue<MarkerData> _pendingCreated = new ConcurrentQueue<MarkerData>();
        private readonly ConcurrentQueue<MarkerData> _pendingUpdated = new ConcurrentQueue<MarkerData>();
        private readonly ConcurrentQueue<string> _pendingDeleted = new ConcurrentQueue<string>();

        private void Start()
        {
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

        private void HandleMarkerCreated(MarkerData marker)
        {
            if (_activeAnchors.ContainsKey(marker.id)) return;

            if (marker.lat != 0 && marker.lng != 0)
            {
                var anchor = SpawnAnchor(Vector3.zero, marker);
                var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
                if (globeAnchor == null)
                    globeAnchor = anchor.AddComponent<CesiumGlobeAnchor>();
                globeAnchor.longitudeLatitudeHeight = new double3(marker.lng, marker.lat, markerAltitude);

                SetAnchorType(anchor, marker.type);
                _activeAnchors[marker.id] = anchor;
                Debug.Log($"[AnchorManager] Spawned geo marker '{marker.label}' at lat/lng ({marker.lat:F6}, {marker.lng:F6})");

                if (c2Client != null && marker.status != "placed")
                {
                    c2Client.EmitMarkerPlace(marker.id, anchor.transform.position);
                }
            }
            else
            {
                var cam = Camera.main;
                var spawnPos = cam != null
                    ? cam.transform.position + cam.transform.forward * 2f
                    : new Vector3(0f, 1.5f, 2f);

                var anchor = SpawnAnchor(spawnPos, marker);
                SetAnchorType(anchor, marker.type, isPending: true);
                _activeAnchors[marker.id] = anchor;
                Debug.Log($"[AnchorManager] Spawned pending marker '{marker.label}' near camera (no lat/lng)");
            }
        }

        private void HandleMarkerUpdated(MarkerData marker)
        {
            if (!_activeAnchors.TryGetValue(marker.id, out var anchor)) return;
            if (anchor == null) return;

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

        public void SpawnTestMarker()
        {
            var data = new MarkerData("test-001", "Test Marker", "hardcoded");
            var anchor = SpawnAnchor(Vector3.zero, data);
            var globeAnchor = anchor.GetComponent<CesiumGlobeAnchor>();
            if (globeAnchor == null)
                globeAnchor = anchor.AddComponent<CesiumGlobeAnchor>();
            globeAnchor.longitudeLatitudeHeight = new double3(-84.3963, 33.7756, markerAltitude);
            Debug.Log("[AnchorManager] Spawned test marker at GT campus origin");
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
