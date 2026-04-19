using UnityEngine;
using TMPro;
using IRIS.Anchors;
using IRIS.Networking;

namespace IRIS.UI
{
    public class FieldStatusHUD : MonoBehaviour
    {
        [SerializeField] private C2Client c2Client;
        [SerializeField] private CalibrationManager calibrationManager;

        [Header("Layout")]
        [SerializeField] private float distanceFromCamera = 2.5f;
        [SerializeField] private float downOffset = -0.6f;
        [SerializeField] private float leftOffset = -0.8f;
        [SerializeField] private float fontSize = 0.3f;

        private Canvas _canvas;
        private TextMeshProUGUI _connectionText;
        private TextMeshProUGUI _calibrationText;
        private TextMeshProUGUI _hintText;

        private bool _lastConnected;
        private bool _lastCalibrated;

        private void Start()
        {
            CreateHUD();

            if (c2Client != null)
            {
                c2Client.OnConnectedEvent += OnConnectionChanged;
                c2Client.OnDisconnectedEvent += OnConnectionChanged;
            }

            if (calibrationManager != null)
            {
                calibrationManager.OnCalibrationChanged += OnCalibrationChanged;
            }

            RefreshAll();
        }

        private void CreateHUD()
        {
            var canvasGo = new GameObject("FieldStatusHUD_Canvas");
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var rt = _canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1f, 0.5f);
            rt.localScale = Vector3.one * 0.5f;

            _connectionText = CreateLabel(rt, "ConnectionLabel", new Vector2(0f, 0.15f));
            _calibrationText = CreateLabel(rt, "CalibrationLabel", new Vector2(0f, 0f));
            _hintText = CreateLabel(rt, "HintLabel", new Vector2(0f, -0.15f));

            if (_hintText != null)
            {
                _hintText.fontStyle = FontStyles.Italic;
                _hintText.fontSize = fontSize * 0.8f;
                _hintText.color = new Color(0.85f, 0.85f, 0.85f);
            }
        }

        private TextMeshProUGUI CreateLabel(RectTransform parent, string name, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1f, 0.15f);
            rect.anchoredPosition = anchoredPos;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.text = "";

            return tmp;
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || _canvas == null) return;

            var camT = cam.transform;
            var forward = camT.forward;
            var right = camT.right;
            var up = camT.up;

            var targetPos = camT.position
                + forward * distanceFromCamera
                + right * leftOffset
                + up * downOffset;

            _canvas.transform.position = targetPos;
            _canvas.transform.rotation = Quaternion.LookRotation(forward, up);
        }

        private void OnConnectionChanged()
        {
            RefreshConnection();
            RefreshHint();
        }

        private void OnCalibrationChanged(bool calibrated)
        {
            RefreshCalibration();
            RefreshHint();
        }

        private void RefreshAll()
        {
            RefreshConnection();
            RefreshCalibration();
            RefreshHint();
        }

        private void RefreshConnection()
        {
            if (_connectionText == null) return;

            bool connected = c2Client != null && c2Client.IsConnected;
            _lastConnected = connected;

            if (connected)
            {
                _connectionText.text = "Connected";
                _connectionText.color = new Color(0.2f, 0.9f, 0.3f); // green
            }
            else
            {
                _connectionText.text = "Disconnected";
                _connectionText.color = new Color(0.95f, 0.25f, 0.25f); // red
            }
        }

        private void RefreshCalibration()
        {
            if (_calibrationText == null) return;

            bool calibrated = calibrationManager != null && calibrationManager.IsCalibrated;
            _lastCalibrated = calibrated;

            if (calibrated)
            {
                _calibrationText.text = "Calibrated";
                _calibrationText.color = new Color(0.2f, 0.9f, 0.3f); // green
            }
            else
            {
                _calibrationText.text = "Not Calibrated";
                _calibrationText.color = new Color(0.95f, 0.85f, 0.15f); // yellow
            }
        }

        private void RefreshHint()
        {
            if (_hintText == null) return;

            if (!_lastConnected)
            {
                _hintText.text = "Waiting for server...";
            }
            else if (!_lastCalibrated)
            {
                _hintText.text = "Press A to calibrate";
            }
            else
            {
                _hintText.text = "Press A to place marker";
            }
        }

        private void OnDestroy()
        {
            if (c2Client != null)
            {
                c2Client.OnConnectedEvent -= OnConnectionChanged;
                c2Client.OnDisconnectedEvent -= OnConnectionChanged;
            }

            if (calibrationManager != null)
            {
                calibrationManager.OnCalibrationChanged -= OnCalibrationChanged;
            }
        }
    }
}
