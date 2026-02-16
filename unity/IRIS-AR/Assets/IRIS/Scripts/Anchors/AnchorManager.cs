using UnityEngine;
using IRIS.Markers;

namespace IRIS.Anchors
{
    public class AnchorManager : MonoBehaviour
    {
        [SerializeField] private GameObject anchorPrefab;
        [SerializeField] private Vector3 testMarkerPosition = new Vector3(0f, 1.5f, 2f);
        [SerializeField] private bool spawnTestMarkerOnStart = true;

        private void Start()
        {
            if (spawnTestMarkerOnStart)
            {
                SpawnTestMarker();
            }
        }

        private void Update()
        {
            // Spawn marker on right trigger press (A button on Quest controller)
            if (OVRInput.GetDown(OVRInput.Button.One))
            {
                SpawnMarkerAtController();
            }
        }

        public void SpawnTestMarker()
        {
            var data = new MarkerData("test-001", 33.7756f, -84.3963f, "Test Marker", "hardcoded");
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
                visualizer.SetColor(Color.cyan);
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
                0f, 0f,
                "Placed Marker",
                "manual"
            );

            SpawnAnchor(spawnPos, data);
            Debug.Log($"[AnchorManager] Placed marker at {spawnPos}");
        }
    }
}
