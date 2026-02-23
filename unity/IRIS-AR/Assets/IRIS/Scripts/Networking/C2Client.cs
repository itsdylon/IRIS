using System;
using System.Collections;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Transport;
using IRIS.Markers;
using Newtonsoft.Json.Linq;

namespace IRIS.Networking
{
    public class C2Client : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "http://localhost:3000";
        [SerializeField] private string deviceName = "Quest3";
        [SerializeField] private string deviceType = "ar-headset";
        [SerializeField] private float heartbeatInterval = 10f;

        public bool IsConnected { get; private set; }

        public event Action<MarkerData> OnMarkerCreated;
        public event Action<MarkerData> OnMarkerUpdated;
        public event Action<string> OnMarkerDeleted;
        public event Action OnConnectedEvent;
        public event Action OnDisconnectedEvent;

        private SocketIOUnity _socket;
        private string _deviceId;
        private Coroutine _heartbeatCoroutine;

        private void Start()
        {
            var uri = new Uri(serverUrl);
            var options = new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
                Reconnection = true,
                ReconnectionAttempts = int.MaxValue,
                ReconnectionDelay = 1000,
            };

            _socket = new SocketIOUnity(uri, options);
            _socket.unityThreadScope = SocketIOUnity.UnityThreadScope.Update;

            _socket.OnConnected += OnConnected;
            _socket.OnDisconnected += OnDisconnected;

            RegisterSocketEvents();

            Debug.Log($"[C2Client] Connecting to {serverUrl}...");
            _socket.Connect();
        }

        private void RegisterSocketEvents()
        {
            _socket.On("device:registered", (response) =>
            {
                var data = response.GetValue<JObject>();
                _deviceId = data["id"]?.ToString();
                Debug.Log($"[C2Client] Registered as device: {_deviceId}");
            });

            _socket.On("marker:created", (response) =>
            {
                var marker = ParseMarker(response.GetValue<JObject>());
                if (marker != null)
                {
                    Debug.Log($"[C2Client] marker:created — {marker.label} (status: {marker.status})");
                    OnMarkerCreated?.Invoke(marker);
                }
            });

            _socket.On("marker:updated", (response) =>
            {
                var marker = ParseMarker(response.GetValue<JObject>());
                if (marker != null)
                {
                    Debug.Log($"[C2Client] marker:updated — {marker.label} (status: {marker.status})");
                    OnMarkerUpdated?.Invoke(marker);
                }
            });

            _socket.On("marker:deleted", (response) =>
            {
                var data = response.GetValue<JObject>();
                var id = data["id"]?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    Debug.Log($"[C2Client] marker:deleted — {id}");
                    OnMarkerDeleted?.Invoke(id);
                }
            });

            _socket.On("marker:list:response", (response) =>
            {
                var markers = response.GetValue<JArray>();
                Debug.Log($"[C2Client] marker:list:response — {markers.Count} markers");
                foreach (var token in markers)
                {
                    var marker = ParseMarker((JObject)token);
                    if (marker != null)
                    {
                        OnMarkerCreated?.Invoke(marker);
                    }
                }
            });
        }

        private void OnConnected(object sender, EventArgs e)
        {
            IsConnected = true;
            Debug.Log("[C2Client] Connected to C2 server");

            _socket.Emit("device:register", new DeviceRegisterPayload
            {
                name = deviceName,
                type = deviceType
            });

            RequestMarkerList();

            _heartbeatCoroutine = StartCoroutine(HeartbeatLoop());

            OnConnectedEvent?.Invoke();
        }

        private void OnDisconnected(object sender, string reason)
        {
            IsConnected = false;
            _deviceId = null;

            if (_heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
                _heartbeatCoroutine = null;
            }

            Debug.Log($"[C2Client] Disconnected: {reason}");
            OnDisconnectedEvent?.Invoke();
        }

        private IEnumerator HeartbeatLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(heartbeatInterval);

                if (IsConnected && !string.IsNullOrEmpty(_deviceId))
                {
                    _socket.Emit("device:heartbeat", new DeviceHeartbeatPayload
                    {
                        id = _deviceId
                    });
                }
            }
        }

        public void EmitMarkerPlace(string markerId, Vector3 position)
        {
            if (!IsConnected) return;

            var payload = new MarkerPlacePayload
            {
                id = markerId,
                position = new PositionPayload(position.x, position.y, position.z)
            };

            _socket.Emit("marker:place", payload);
            Debug.Log($"[C2Client] marker:place — {markerId} at ({position.x:F2}, {position.y:F2}, {position.z:F2})");
        }

        public void RequestMarkerList()
        {
            if (!IsConnected) return;
            _socket.Emit("marker:list");
        }

        private MarkerData ParseMarker(JObject obj)
        {
            if (obj == null) return null;

            var marker = new MarkerData(
                obj["id"]?.ToString() ?? "",
                obj["label"]?.ToString() ?? "",
                obj["type"]?.ToString() ?? "generic"
            );

            marker.status = obj["status"]?.ToString() ?? "pending";
            marker.createdAt = obj["createdAt"]?.ToString();
            marker.placedAt = obj["placedAt"]?.ToString();

            var pos = obj["position"] as JObject;
            if (pos != null)
            {
                marker.position = new MarkerPosition(new Vector3(
                    pos["x"]?.Value<float>() ?? 0f,
                    pos["y"]?.Value<float>() ?? 0f,
                    pos["z"]?.Value<float>() ?? 0f
                ));
            }

            return marker;
        }

        private void OnDestroy()
        {
            if (_heartbeatCoroutine != null)
            {
                StopCoroutine(_heartbeatCoroutine);
            }

            if (_socket != null)
            {
                _socket.OnConnected -= OnConnected;
                _socket.OnDisconnected -= OnDisconnected;
                _socket.Disconnect();
                _socket.Dispose();
                _socket = null;
            }
        }
    }
}
