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
        private const float GlyphBounceHz = 1.4f;
        private static readonly Vector3 GlyphScale = Vector3.one * 4.5f;
        /// <summary>Higher default Y so markers hover well above head level. When you walk under them they only need to "come down" a small amount.</summary>
        private static readonly Vector3 GlyphLocalPos = new Vector3(0f, 3.5f, 0f);

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
        [Tooltip("Glyph scale multiplier peaks when you are this close or closer. (Kept low to avoid 'in your face' when walking directly under.)")]
        [SerializeField] private float glyphScaleCloseMeters = 1.25f;
        [SerializeField] private float minGlyphScaleMul = 1.0f;
        [SerializeField] private float maxGlyphScaleMul = 1.0f;
        [Tooltip("Max up/down bob amount (meters) when close. Marker no longer shifts down with proximity.")]
        [SerializeField] private float glyphProximityVerticalLiftMax = 0.02f;

        [Header("Waypoint / objective (yellow diamond)")]
        [Tooltip("Uniform scale vs other types — octahedron is bulky at the same scale as the white tetra.")]
        [SerializeField] private float waypointDiamondMeshScale = 0.82f;
        [Tooltip("Extra local Y above the default glyph anchor so the diamond reads higher like other icons.")]
        [SerializeField] private float waypointDiamondExtraHeight = 0.22f;

        [Header("Beacon (far) — very tall column")]
        [Tooltip("How tall the spinning beacon column/pole is (meters). Bumped very high so it's visible from practically anywhere.")]
        [SerializeField] private float beaconColumnHeightMeters = 150f;
        [Tooltip("Beam footprint = glyph localScale.x × this (matches icon width as it LOD-scales).")]
        [SerializeField] private float beamWidthPerGlyphScale = 1f;
        [SerializeField] private float beaconSpinDegreesPerSecond = 22f;
        [SerializeField] private float beaconMaxAlpha = 0.4f;
        [SerializeField] private float beaconPulseHz = 0.45f;
        [SerializeField] private float beaconPulseAmplitude = 0.05f;

        [SerializeField] private Color anchorColor = Color.cyan;
        [SerializeField] private string label = "";
        private Renderer _renderer;
        private TextMeshPro _labelText;
        private MeshFilter _glyphMeshFilter;
        private Transform _glyphTransform;
        private LineRenderer _groundPin;

        private Transform _beaconRoot;
        private Transform _beaconSpinRoot;
        private Renderer _beamFin0;
        private Renderer _beamFin1;
        private Material _beamFin0Mat;
        private Material _beamFin1Mat;

        private Vector3 _glyphScaleUniformBase = GlyphScale;
        private Vector3 _glyphBaseLocalPos = GlyphLocalPos;

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
            var t = string.IsNullOrEmpty(type) ? "generic" : type;
            _glyphMeshFilter.sharedMesh = MarkerGlyphMeshes.ForMarkerType(t);

            _glyphScaleUniformBase = GlyphScale;
            _glyphBaseLocalPos = GlyphLocalPos;
            if (t == "waypoint" || t == "objective")
            {
                _glyphScaleUniformBase = GlyphScale * waypointDiamondMeshScale;
                _glyphBaseLocalPos = GlyphLocalPos + Vector3.up * waypointDiamondExtraHeight;
            }
            else
            {
                _glyphBaseLocalPos = GlyphLocalPos;
            }

            _glyphTransform.localScale = _glyphScaleUniformBase;
            _glyphTransform.localPosition = _glyphBaseLocalPos;
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
                float midMul = Mathf.Lerp(minGlyphScaleMul, maxGlyphScaleMul, 0.5f);
                _glyphTransform.localScale = _glyphScaleUniformBase * midMul;
                _glyphTransform.localPosition = _glyphBaseLocalPos;
                return;
            }

            Vector3 delta = transform.position - cam.transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;

            float beaconWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(nearGlyphMeters, farBeaconMeters, dist));
            float glyphWeight = 1f - beaconWeight;

            float scaleMul = minGlyphScaleMul;
            _glyphTransform.localScale = _glyphScaleUniformBase * scaleMul;

            float closeWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(nearGlyphMeters, farBeaconMeters, dist));
            float bounce = Mathf.Sin(Time.time * (Mathf.PI * 2f * GlyphBounceHz))
                * glyphProximityVerticalLiftMax
                * closeWeight;
            _glyphTransform.localPosition = _glyphBaseLocalPos + Vector3.up * bounce;

            if (_beaconSpinRoot != null)
                _beaconSpinRoot.localPosition = _glyphTransform.localPosition;

            var glyphCol = anchorColor;
            glyphCol.a *= Mathf.Clamp01(glyphWeight + 0.1f);
            ApplyColorToGlyphRenderer(glyphCol);

            if (_beaconRoot == null)
                return;

            bool showBeacon = beaconWeight > 0.02f;
            _beaconRoot.gameObject.SetActive(showBeacon);
            if (!showBeacon)
                return;

            if (_beaconSpinRoot != null)
                _beaconSpinRoot.Rotate(0f, beaconSpinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);

            UpdateBeamFootprint();
            float pulse = 1f + beaconPulseAmplitude * Mathf.Sin(Time.time * (Mathf.PI * 2f * beaconPulseHz));
            float alpha = beaconMaxAlpha * beaconWeight * pulse;
            ApplyBeamFinColor(_beamFin0Mat, alpha);
            ApplyBeamFinColor(_beamFin1Mat, alpha);
        }

        private void UpdateBeamFootprint()
        {
            if (_beamFin0 == null || _beamFin1 == null) return;
            float w = Mathf.Max(0.35f, _glyphTransform.localScale.x * beamWidthPerGlyphScale);
            float h = beaconColumnHeightMeters;
            _beamFin0.transform.localScale = new Vector3(w, h, 1f);
            _beamFin1.transform.localScale = new Vector3(w, h, 1f);
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

        private void ApplyBeamFinColor(Material mat, float alpha)
        {
            if (mat == null) return;
            var c = new Color(anchorColor.r, anchorColor.g, anchorColor.b, alpha);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", c);
            else
                mat.color = c;
        }

        private void EnsureBeaconFx()
        {
            if (_beaconRoot != null)
                return;

            var template = CreateBeaconSurfaceMaterial();
            if (template == null)
            {
                Debug.LogWarning("[AnchorVisualizer] Beacon FX disabled — no compatible transparent material.");
                return;
            }

            var rootGo = new GameObject("BeaconFX");
            _beaconRoot = rootGo.transform;
            _beaconRoot.SetParent(transform, false);
            _beaconRoot.localPosition = Vector3.zero;
            _beaconRoot.localRotation = Quaternion.identity;

            var spinGo = new GameObject("BeaconSpin");
            _beaconSpinRoot = spinGo.transform;
            _beaconSpinRoot.SetParent(_beaconRoot, false);
            _beaconSpinRoot.localPosition = GlyphLocalPos;
            _beaconSpinRoot.localRotation = Quaternion.identity;

            float h = beaconColumnHeightMeters;
            _beamFin0 = CreateVerticalBeamFin(_beaconSpinRoot, "BeamFin0", 0f, h, template);
            _beamFin1 = CreateVerticalBeamFin(_beaconSpinRoot, "BeamFin1", 90f, h, template);

            _beamFin0Mat = _beamFin0.material;
            _beamFin1Mat = _beamFin1.material;
            Destroy(template);

            UpdateBeamFootprint();
        }

        /// <summary>
        /// Two crossed vertical quads in the XY plane (Unity Quad default); Y is world-up column height.
        /// Spinning the parent rotates the whole cross as one volume.
        /// </summary>
        private static Renderer CreateVerticalBeamFin(Transform parent, string name, float yDegrees, float columnHeight, Material template)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            Object.Destroy(go.GetComponent<Collider>());

            go.transform.localPosition = new Vector3(0f, columnHeight * 0.5f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, yDegrees, 0f);
            go.transform.localScale = Vector3.one;

            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (template != null)
                r.material = new Material(template);
            return r;
        }

        private void OnDestroy()
        {
            if (_beamFin0Mat != null) Destroy(_beamFin0Mat);
            if (_beamFin1Mat != null) Destroy(_beamFin1Mat);
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

        private static Material CreateBeaconSurfaceMaterial()
        {
            var m = CreateGroundPinLineMaterial();
            if (m == null) return null;
            var inst = new Material(m) { renderQueue = 3000 };
            if (inst.HasProperty("_Cull"))
                inst.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            return inst;
        }
    }
}
