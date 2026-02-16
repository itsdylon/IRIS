using UnityEngine;
using TMPro;

namespace IRIS.Anchors
{
    public class AnchorVisualizer : MonoBehaviour
    {
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
