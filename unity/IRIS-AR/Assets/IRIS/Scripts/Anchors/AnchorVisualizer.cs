using UnityEngine;
using TMPro;
using IRIS.Core;

namespace IRIS.Anchors
{
    public class AnchorVisualizer : MonoBehaviour
    {
        private const float PendingColorDimFactor = 0.6f;
        private const float PendingAlpha = 0.65f;
        private const float GroundPinWidth = 0.02f;
        private static readonly Vector3 GlyphScale = Vector3.one * 2.35f;
        private static readonly Vector3 GlyphLocalPos = new Vector3(0f, 1.15f, 0f);

        /// <summary>Tactical scheme: threat=red ▲, friendly=blue ●, waypoint/objective=yellow ◆, extraction=green +, POI=orange ⯄, generic=white ▼.</summary>
        public static Color GetColorForType(string type) => type switch
        {
            "threat" => new Color(0.86f, 0.15f, 0.15f),
            "friendly" => new Color(0.15f, 0.41f, 0.92f),
            "waypoint" => new Color(0.98f, 0.82f, 0.09f),
            "objective" => new Color(0.98f, 0.82f, 0.09f),
            "extraction" => new Color(0.09f, 0.64f, 0.29f),
            "info" => new Color(0.92f, 0.35f, 0.09f),
            _ => new Color(0.94f, 0.96f, 0.98f),
        };

        [Header("Distance LOD")]
        [Tooltip("Beyond this horizontal distance from the camera, the distant beacon is fully visible and the tactical glyph is hidden.")]
        [SerializeField] private float farBeaconMeters = 24f;
        [Tooltip("Inside this distance, only the tactical glyph is shown (beacon faded out).")]
        [SerializeField] private float nearGlyphMeters = 7f;
        [Tooltip("Glyph scale multiplier when the marker is at or farther than this horizontal distance (smaller silhouette mid-range).")]
        [SerializeField] private float glyphScaleFarMeters = 11f;
        [Tooltip("Glyph scale multiplier peaks when you are this close or closer.")]
        [SerializeField] private float glyphScaleCloseMeters = 1.25f;
        [SerializeField] private float minGlyphScaleMul = 0.55f;
        [SerializeField] private float maxGlyphScaleMul = 2.65f;

        [Header("Beacon (far)")]
        [SerializeField] private float beaconColumnHeightMeters = 11f;
        [SerializeField] private float beaconHelixRadius = 0.38f;
        [SerializeField] private int beaconHelixSegments = 42;
        [SerializeField] private float beaconHelixTurns = 2.25f;
        [SerializeField] private float beaconFlowSpeed = 0.42f;
        [SerializeField] private float beaconCoreMaxAlpha = 0.14f;
        [SerializeField] private float beaconHelixMaxAlpha = 0.11f;
        [SerializeField] private float beaconRingMaxAlpha = 0.07f;
        [SerializeField] private float beaconPulseHz = 0.55f;
        [SerializeField] private float beaconPulseAmplitude = 0.04f;

        [SerializeField] private Color anchorColor = Color.cyan;
        [SerializeField] private string label = "";

        private Renderer _renderer;
        private TextMeshPro _labelText;
        private MeshFilter _glyphMeshFilter;
        private Transform _glyphTransform;
        private LineRenderer _groundPin;

        private Transform _beaconRoot;
        private LineRenderer _beaconCore;
        private LineRenderer _beaconHelix;
        private LineRenderer _beaconRing;
        private Material _beaconCoreMat;
        private Material _beaconHelixMat;
        private Material _beaconRingMat;
        private Vector3[] _helixScratch;

        private void Awake()
        {
            _glyphTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;
            _glyphMeshFilter = _glyphTransform.GetComponent<MeshFilter>();
            _renderer = _glyphMeshFilter != null
                ? _glyphMeshFilter.GetComponent<Renderer>()
                : GetComponentInChildren<Renderer>();
            _labelText = GetComponentInChildren<TextMeshPro>();
            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                _renderer.material = new Material(_renderer.sharedMaterial);
            }
        }

        public void SetColor(Color color)
        {
            anchorColor = color;
            ApplyColorToGlyphRenderer(anchorColor);
        }

        public void SetType(string type)
        {
            ApplyGlyphMesh(type);
            SetColor(GetColorForType(type));
            EnsureBeaconFx();

            if (IRISManager.IsPassthroughMode && _groundPin == null)
            {
                AddGroundPin();
            }
        }

        public void SetTypePending(string type)
        {
            ApplyGlyphMesh(type);
            var baseColor = GetColorForType(type);
            var pendingColor = new Color(
                baseColor.r * PendingColorDimFactor,
                baseColor.g * PendingColorDimFactor,
                baseColor.b * PendingColorDimFactor,
                PendingAlpha
            );

            SetColor(pendingColor);
            EnsureBeaconFx();
        }

        private void ApplyGlyphMesh(string type)
        {
            if (_glyphMeshFilter == null) return;
            _glyphMeshFilter.sharedMesh = MarkerGlyphMeshes.ForMarkerType(string.IsNullOrEmpty(type) ? "generic" : type);
            _glyphTransform.localScale = GlyphScale;
            _glyphTransform.localPosition = GlyphLocalPos;
        }

        public void SetLabel(string text)
        {
            label = text;
            if (_labelText != null)
            {
                _labelText.text = text;
            }
        }

        private void Start()
        {
            SetColor(anchorColor);
            if (!string.IsNullOrEmpty(label))
            {
                SetLabel(label);
            }
        }

        private void LateUpdate()
        {
            var cam = Camera.main;

            if (_labelText != null && cam != null)
            {
                _labelText.transform.rotation = Quaternion.LookRotation(
                    _labelText.transform.position - cam.transform.position);
            }

            UpdateDistanceLod(cam);
        }

        private void UpdateDistanceLod(Camera cam)
        {
            if (_glyphTransform == null || _renderer == null) return;

            if (cam == null)
            {
                if (_beaconRoot != null)
                    _beaconRoot.gameObject.SetActive(false);
                ApplyColorToGlyphRenderer(anchorColor);
                _glyphTransform.localScale = GlyphScale * Mathf.Lerp(minGlyphScaleMul, maxGlyphScaleMul, 0.5f);
                return;
            }

            Vector3 delta = transform.position - cam.transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;

            float beaconWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(nearGlyphMeters, farBeaconMeters, dist));
            float glyphWeight = 1f - beaconWeight;

            float scaleT = Mathf.InverseLerp(glyphScaleFarMeters, glyphScaleCloseMeters, dist);
            scaleT = Mathf.SmoothStep(0f, 1f, scaleT);
            float scaleMul = Mathf.Lerp(minGlyphScaleMul, maxGlyphScaleMul, scaleT);
            _glyphTransform.localScale = GlyphScale * scaleMul;

            var glyphCol = anchorColor;
            glyphCol.a *= Mathf.Clamp01(glyphWeight + 0.1f);
            ApplyColorToGlyphRenderer(glyphCol);

            if (_beaconRoot == null)
                return;

            bool showBeacon = beaconWeight > 0.02f;
            _beaconRoot.gameObject.SetActive(showBeacon);
            if (!showBeacon)
                return;

            float pulse = 1f + beaconPulseAmplitude * Mathf.Sin(Time.time * (Mathf.PI * 2f * beaconPulseHz));
            float helixAlpha = beaconHelixMaxAlpha * beaconWeight * pulse;
            float coreAlpha = beaconCoreMaxAlpha * beaconWeight * pulse;
            float ringAlpha = beaconRingMaxAlpha * beaconWeight;

            UpdateBeaconGeometry();
            ApplyBeaconLineColors(_beaconCore, _beaconCoreMat, coreAlpha);
            ApplyBeaconLineColors(_beaconHelix, _beaconHelixMat, helixAlpha);
            ApplyBeaconLineColors(_beaconRing, _beaconRingMat, ringAlpha);
        }

        private void ApplyColorToGlyphRenderer(Color c)
        {
            if (_renderer == null) return;
            var m = _renderer.material;
            if (m == null) return;
            if (m.HasProperty("_BaseColor"))
                m.SetColor("_BaseColor", c);
            else
                m.color = c;
        }

        private void EnsureBeaconFx()
        {
            if (_beaconRoot != null)
                return;

            var beamMatBase = CreateBeaconLineMaterial();
            if (beamMatBase == null)
            {
                Debug.LogWarning("[AnchorVisualizer] Beacon FX disabled — no compatible line material.");
                return;
            }

            var rootGo = new GameObject("BeaconFX");
            _beaconRoot = rootGo.transform;
            _beaconRoot.SetParent(transform, false);
            _beaconRoot.localPosition = Vector3.zero;
            _beaconRoot.localRotation = Quaternion.identity;

            _beaconCore = CreateBeaconLine(_beaconRoot, "BeaconCore", sortingOrder: -3, width: 0.028f, beamMatBase);
            _beaconHelix = CreateBeaconLine(_beaconRoot, "BeaconHelix", sortingOrder: -2, width: 0.034f, beamMatBase);
            _beaconRing = CreateBeaconLine(_beaconRoot, "BeaconRing", sortingOrder: -1, width: 0.022f, beamMatBase);
            _beaconRing.loop = true;

            _beaconCoreMat = _beaconCore.material;
            _beaconHelixMat = _beaconHelix.material;
            _beaconRingMat = _beaconRing.material;
            Destroy(beamMatBase);

            _helixScratch = new Vector3[Mathf.Max(8, beaconHelixSegments)];
        }

        private static LineRenderer CreateBeaconLine(Transform parent, string name, int sortingOrder, float width, Material template)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = sortingOrder;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.startWidth = width;
            lr.endWidth = width * 0.65f;
            if (template != null)
                lr.material = new Material(template);
            return lr;
        }

        private void UpdateBeaconGeometry()
        {
            float h = beaconColumnHeightMeters;
            float phase = Time.time * beaconFlowSpeed;

            if (_beaconCore != null)
            {
                _beaconCore.positionCount = 2;
                _beaconCore.SetPosition(0, Vector3.zero);
                _beaconCore.SetPosition(1, new Vector3(0f, h, 0f));
            }

            if (_beaconHelix != null)
            {
                int n = Mathf.Max(4, beaconHelixSegments);
                if (_helixScratch == null || _helixScratch.Length < n)
                    _helixScratch = new Vector3[n];

                _beaconHelix.positionCount = n;
                float turns = beaconHelixTurns * Mathf.PI * 2f;
                for (int i = 0; i < n; i++)
                {
                    float t = n == 1 ? 0f : i / (float)(n - 1);
                    float y = t * h;
                    float taper = 0.35f + 0.65f * t;
                    float ang = t * turns + phase;
                    float r = beaconHelixRadius * taper;
                    _helixScratch[i] = new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);
                    _beaconHelix.SetPosition(i, _helixScratch[i]);
                }
            }

            if (_beaconRing != null)
            {
                const int ringPts = 20;
                _beaconRing.positionCount = ringPts;
                float ringY = h * 0.94f;
                float rRing = beaconHelixRadius * 2.1f;
                float rot = Time.time * 0.38f;
                for (int i = 0; i < ringPts; i++)
                {
                    float ang = rot + (i / (float)ringPts) * Mathf.PI * 2f;
                    _beaconRing.SetPosition(i, new Vector3(Mathf.Cos(ang) * rRing, ringY, Mathf.Sin(ang) * rRing));
                }
            }
        }

        private void ApplyBeaconLineColors(LineRenderer lr, Material mat, float alpha)
        {
            if (lr == null) return;
            var c = new Color(anchorColor.r, anchorColor.g, anchorColor.b, alpha);
            lr.startColor = c;
            lr.endColor = new Color(anchorColor.r, anchorColor.g, anchorColor.b, alpha * 0.35f);

            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", c);
                else
                    mat.color = c;
            }
        }

        private void OnDestroy()
        {
            if (_beaconCoreMat != null) Destroy(_beaconCoreMat);
            if (_beaconHelixMat != null) Destroy(_beaconHelixMat);
            if (_beaconRingMat != null) Destroy(_beaconRingMat);
        }

        private void AddGroundPin()
        {
            _groundPin = gameObject.AddComponent<LineRenderer>();
            _groundPin.positionCount = 2;
            _groundPin.startWidth = GroundPinWidth;
            _groundPin.endWidth = GroundPinWidth;
            _groundPin.startColor = Color.white;
            _groundPin.endColor = new Color(1f, 1f, 1f, 0.35f);
            var pinMat = CreateGroundPinLineMaterial();
            if (pinMat != null)
                _groundPin.material = pinMat;
            _groundPin.useWorldSpace = false;
            _groundPin.SetPosition(0, Vector3.zero);
            _groundPin.SetPosition(1, Vector3.down * transform.position.y);
        }

        /// <summary>URP shader name differs by version; missing shader shows as magenta on device.</summary>
        private static Material CreateGroundPinLineMaterial()
        {
            string[] candidates =
            {
                "Universal Render Pipeline/Unlit",
                "Unlit/Color",
                "Sprites/Default",
                "Legacy Shaders/Particles/Alpha Blended",
            };

            foreach (var name in candidates)
            {
                var s = Shader.Find(name);
                if (s == null) continue;
                var m = new Material(s);
                if (m.HasProperty("_BaseColor"))
                    m.SetColor("_BaseColor", Color.white);
                else
                    m.color = Color.white;
                return m;
            }

            Debug.LogWarning("[AnchorVisualizer] No shader for ground pin — assign a URP-compatible Unlit in the project.");
            return null;
        }

        private static Material CreateBeaconLineMaterial()
        {
            var m = CreateGroundPinLineMaterial();
            if (m == null) return null;
            return new Material(m) { renderQueue = 3000 };
        }
    }
}
