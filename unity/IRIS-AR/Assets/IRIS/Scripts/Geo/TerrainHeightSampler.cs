using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

namespace IRIS.Geo
{
    public class TerrainHeightSampler : MonoBehaviour
    {
        [SerializeField] private Cesium3DTileset terrainTileset;
        [SerializeField] private double fallbackHeight = 2.0;
        [SerializeField] private float samplingTimeoutSeconds = 10f;

        [Header("Camera Ground Placement")]
        [SerializeField] private CesiumGlobeAnchor cameraGlobeAnchor;
        [SerializeField] private double cameraEyeHeight = 1.7;

        public bool IsAvailable => terrainTileset != null;

        private void Awake()
        {
            if (terrainTileset == null)
            {
                terrainTileset = FindObjectOfType<Cesium3DTileset>();
                if (terrainTileset != null)
                    Debug.Log("[TerrainHeightSampler] Auto-found Cesium3DTileset in scene");
                else
                    Debug.LogWarning("[TerrainHeightSampler] No Cesium3DTileset found — terrain sampling unavailable");
            }
        }

        private void Start()
        {
            if (cameraGlobeAnchor != null)
                StartCoroutine(PlaceCameraOnGround());
        }

        private IEnumerator PlaceCameraOnGround()
        {
            var cameraTransform = cameraGlobeAnchor.transform;

            const int maxAttempts = 30;
            const float retryInterval = 0.5f;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                yield return new WaitForSeconds(retryInterval);

                var origin = cameraTransform.position + Vector3.up * 1000f;

                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2000f))
                {
                    var surfacePos = hit.point + Vector3.up * (float)cameraEyeHeight;
                    cameraTransform.position = surfacePos;
                    Debug.Log($"[TerrainHeightSampler] Placed camera on terrain at Unity Y={surfacePos.y:F1} (attempt {attempt})");
                    yield break;
                }

                if (attempt % 5 == 0)
                    Debug.LogWarning($"[TerrainHeightSampler] Camera raycast attempt {attempt}/{maxAttempts} — no terrain hit yet");
            }

            Debug.LogWarning("[TerrainHeightSampler] Could not find terrain surface — camera position unchanged");
        }

        public async Task<double> SampleHeightAsync(double longitude, double latitude, double heightOffset = 0.0)
        {
            var height = await SampleHeightRawAsync(longitude, latitude);

            if (height.HasValue)
            {
                Debug.Log($"[TerrainHeightSampler] Sampled terrain height {height.Value:F1}m at ({latitude:F4}, {longitude:F4})");
                return height.Value + heightOffset;
            }

            Debug.LogWarning($"[TerrainHeightSampler] Sample failed at ({latitude:F4}, {longitude:F4}) — using fallback");
            return fallbackHeight + heightOffset;
        }

        private async Task<double?> SampleHeightRawAsync(double longitude, double latitude)
        {
            if (terrainTileset == null) return null;

            var inputPosition = new double3(longitude, latitude, 0.0);

            try
            {
                using var cts = new CancellationTokenSource(
                    (int)(samplingTimeoutSeconds * 1000));

                var sampleTask = terrainTileset.SampleHeightMostDetailed(inputPosition);
                var completedTask = await Task.WhenAny(sampleTask, Task.Delay(-1, cts.Token));

                if (completedTask != sampleTask) return null;

                var result = await sampleTask;

                if (result.sampleSuccess != null
                    && result.sampleSuccess.Length > 0
                    && result.sampleSuccess[0])
                {
                    return result.longitudeLatitudeHeightPositions[0].z;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
