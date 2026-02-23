using System.Collections.Generic;
using UnityEngine;
using IRIS.Markers;
using IRIS.Networking;

namespace IRIS.Anchors
{
    public class AnchorManager : MonoBehaviour
    {
        [SerializeField] private GameObject anchorPrefab;
        [SerializeField] private C2Client c2Client;
        [SerializeField] private Vector3 testMarkerPosition = new Vector3(0f, 1.5f, 2f);
        [SerializeField] private bool spawnTestMarkerOnStart = false;

        private readonly Dictionary<string, GameObject> _activeAnchors = new Dictionary<string, GameObject>();

        private void Start()
        {
            if (spawnTestMarkerOnStart)
            {
                SpawnTestMarker();
            }

            if (c2Client != null)
            {
                c2Client.OnMarkerCreated += HandleMarkerCreated;
                c2Client.OnMarkerUpdated += HandleMarkerUpdated;
                c2Client.OnMarkerDeleted += HandleMarkerDeleted;
            }
        }

        private void Update()
        {
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
            if (c2Client != null)
            {
                c2Client.OnMarkerCreated -= HandleMarkerCreated;
                c2Client.OnMarkerUpdated -= HandleMarkerUpdated;
                c2Client.OnMarkerDeleted -= HandleMarkerDeleted;
            }
        }
    }
}
