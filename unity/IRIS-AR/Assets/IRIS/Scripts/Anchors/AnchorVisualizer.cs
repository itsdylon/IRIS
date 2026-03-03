using UnityEngine;
using TMPro;

namespace IRIS.Anchors
{
    public class AnchorVisualizer : MonoBehaviour
    {
        private const float PendingColorDimFactor = 0.6f;
        private const float PendingAlpha = 0.65f;

        public static Color GetColorForType(string type) => type switch
        {
            "waypoint" => new Color(0.23f, 0.51f, 0.96f), // blue
            "threat" => new Color(0.94f, 0.27f, 0.27f), // red
            "objective" => new Color(0.13f, 0.77f, 0.37f), // green
            "info" => new Color(0.92f, 0.70f, 0.03f), // yellow
            _ => Color.white, // generic
        };

        [SerializeField] private Color anchorColor = Color.cyan;
        [SerializeField] private string label = "";

        private Renderer _renderer;
        private TextMeshPro _labelText;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
            _labelText = GetComponentInChildren<TextMeshPro>();
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
            SetColor(GetColorForType(type));
        }

        public void SetTypePending(string type)
        {
            var baseColor = GetColorForType(type);
            var pendingColor = new Color(
                baseColor.r * PendingColorDimFactor,
                baseColor.g * PendingColorDimFactor,
                baseColor.b * PendingColorDimFactor,
                PendingAlpha
            );

            SetColor(pendingColor);
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
