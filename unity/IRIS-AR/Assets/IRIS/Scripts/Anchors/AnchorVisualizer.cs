using UnityEngine;
using TMPro;

namespace IRIS.Anchors
{
    public class AnchorVisualizer : MonoBehaviour
    {
        private const float PendingColorDimFactor = 0.6f;
        private const float PendingAlpha = 0.65f;
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

        [SerializeField] private Color anchorColor = Color.cyan;
        [SerializeField] private string label = "";

        private Renderer _renderer;
        private TextMeshPro _labelText;
        private MeshFilter _glyphMeshFilter;
        private Transform _glyphTransform;

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
            if (_renderer != null)
            {
                _renderer.material.color = anchorColor;
            }
        }

        public void SetType(string type)
        {
            ApplyGlyphMesh(type);
            SetColor(GetColorForType(type));
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
    }
}
