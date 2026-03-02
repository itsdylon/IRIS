using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using IRIS.Markers;
using IRIS.Networking;
using IRIS.Geo;

namespace IRIS.Anchors
{
    public class AnchorManager : MonoBehaviour
    {
        [SerializeField] private GameObject anchorPrefab;
        [SerializeField] private C2Client c2Client;
        [SerializeField] private Vector3 testMarkerPosition = new Vector3(0f, 1.5f, 2f);
        [SerializeField] private bool spawnTestMarkerOnStart = false;

        [Header("Geo Reference (Origin Point)")]
        [SerializeField] private double referenceLat = 33.7756;
        [SerializeField] private double referenceLng = -84.3963;

        private readonly Dictionary<string, GameObject> _activeAnchors = new Dictionary<string, GameObject>();
        private readonly ConcurrentQueue<MarkerData> _pendingCreated = new ConcurrentQueue<MarkerData>();
        private readonly ConcurrentQueue<MarkerData> _pendingUpdated = new ConcurrentQueue<MarkerData>();
        private readonly ConcurrentQueue<string> _pendingDeleted = new ConcurrentQueue<string>();

        private void Start()
        {
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

            if (marker.status == "placed" && marker.position != null)
            {
                var anchor = SpawnAnchor(marker.GetPositionVector(), marker);
                SetAnchorColor(anchor, Color.cyan);
                _activeAnchors[marker.id] = anchor;
                Debug.Log($"[AnchorManager] Spawned placed marker '{marker.label}' at known position");
            }
            else if (marker.lat != 0 && marker.lng != 0)
            {
                var spawnPos = GeoUtils.LatLngToUnityPosition(marker.lat, marker.lng, referenceLat, referenceLng);
                var anchor = SpawnAnchor(spawnPos, marker);
                SetAnchorColor(anchor, Color.green);
                _activeAnchors[marker.id] = anchor;

                c2Client.EmitMarkerPlace(marker.id, spawnPos);
                Debug.Log($"[AnchorManager] Spawned geo marker '{marker.label}' at ({spawnPos.x:F2}, {spawnPos.y:F2}, {spawnPos.z:F2}) from lat/lng ({marker.lat}, {marker.lng})");
            }
            else
            {
                var cam = Camera.main;
                var spawnPos = cam != null
                    ? cam.transform.position + cam.transform.forward * 2f
                    : new Vector3(0f, 1.5f, 2f);

                var anchor = SpawnAnchor(spawnPos, marker);
                SetAnchorColor(anchor, Color.yellow);
                _activeAnchors[marker.id] = anchor;

                c2Client.EmitMarkerPlace(marker.id, spawnPos);
                Debug.Log($"[AnchorManager] Spawned pending marker '{marker.label}' 2m in front, reporting position");
            }
        }

        private void HandleMarkerUpdated(MarkerData marker)
        {
            if (!_activeAnchors.TryGetValue(marker.id, out var anchor)) return;
            if (anchor == null) return;

            SetAnchorColor(anchor, Color.cyan);
            Debug.Log($"[AnchorManager] Marker '{marker.label}' updated to placed");
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

        private void SetAnchorColor(GameObject anchor, Color color)
        {
            if (anchor == null) return;

            var visualizer = anchor.GetComponent<AnchorVisualizer>();
            if (visualizer != null)
            {
                visualizer.SetColor(color);
            }
        }

        public void SpawnTestMarker()
        {
            var data = new MarkerData("test-001", "Test Marker", "hardcoded");
            SpawnAnchor(testMarkerPosition, data);
            Debug.Log($"[AnchorManager] Spawned test marker at {testMarkerPosition}");
        }

        public GameObject SpawnAnchor(Vector3 position, MarkerData data)
        {
            if (anchorPrefab == null)
            {
                Debug.LogError("[AnchorManager] anchorPrefab is not assigned!");
                return null;
            }

            var anchor = Instantiate(anchorPrefab, position, Quaternion.identity);

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

            var data = new MarkerData(
                System.Guid.NewGuid().ToString(),
                "Placed Marker",
                "manual"
            );

            SpawnAnchor(spawnPos, data);
            Debug.Log($"[AnchorManager] Placed marker at {spawnPos}");
        }

        private void OnDestroy()
        {
            // Lambdas are used for subscriptions so explicit unsubscribe isn't possible,
            // but the queues will simply stop being drained once this object is destroyed.
        }
    }
}
