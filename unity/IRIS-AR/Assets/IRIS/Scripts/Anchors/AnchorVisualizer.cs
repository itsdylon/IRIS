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
        private LineRenderer _groundPin;

        private void Awake()
        {
            _renderer = GetComponentInChildren<Renderer>();
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
            SetColor(GetColorForType(type));

            if (IRISManager.IsPassthroughMode && _groundPin == null)
            {
                AddGroundPin();
            }
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

        private void LateUpdate()
        {
            if (_labelText == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            _labelText.transform.rotation = Quaternion.LookRotation(
                _labelText.transform.position - cam.transform.position);
        }

        private void AddGroundPin()
        {
            _groundPin = gameObject.AddComponent<LineRenderer>();
            _groundPin.positionCount = 2;
            _groundPin.startWidth = GroundPinWidth;
            _groundPin.endWidth = GroundPinWidth;
            _groundPin.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _groundPin.material.color = Color.white;
            _groundPin.useWorldSpace = false;
            _groundPin.SetPosition(0, Vector3.zero);
            _groundPin.SetPosition(1, Vector3.down * transform.position.y);
        }
    }
}
