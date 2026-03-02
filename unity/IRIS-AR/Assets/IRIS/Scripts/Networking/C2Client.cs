using System;
using System.Collections;
using System.Text.Json;
using UnityEngine;
using SocketIOClient;
using SocketIOClient.Transport;
using IRIS.Markers;

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
                var data = response.GetValue<JsonElement>();
                _deviceId = data.GetProperty("id").GetString();
                Debug.Log($"[C2Client] Registered as device: {_deviceId}");
            });

            _socket.On("marker:created", (response) =>
            {
                var marker = ParseMarker(response.GetValue<JsonElement>());
                if (marker != null)
                {
                    Debug.Log($"[C2Client] marker:created — {marker.label} (status: {marker.status})");
                    OnMarkerCreated?.Invoke(marker);
                }
            });

            _socket.On("marker:updated", (response) =>
            {
                var marker = ParseMarker(response.GetValue<JsonElement>());
                if (marker != null)
                {
                    Debug.Log($"[C2Client] marker:updated — {marker.label} (status: {marker.status})");
                    OnMarkerUpdated?.Invoke(marker);
                }
            });

            _socket.On("marker:deleted", (response) =>
            {
                var data = response.GetValue<JsonElement>();
                var id = data.GetProperty("id").GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    Debug.Log($"[C2Client] marker:deleted — {id}");
                    OnMarkerDeleted?.Invoke(id);
                }
            });

            _socket.On("marker:list:response", (response) =>
            {
                var arr = response.GetValue<JsonElement>();
                Debug.Log($"[C2Client] marker:list:response — {arr.GetArrayLength()} markers");
                foreach (var token in arr.EnumerateArray())
                {
                    var marker = ParseMarker(token);
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

        private MarkerData ParseMarker(JsonElement obj)
        {
            var marker = new MarkerData(
                obj.GetProperty("id").GetString() ?? "",
                obj.TryGetProperty("label", out var labelEl) ? labelEl.GetString() ?? "" : "",
                obj.TryGetProperty("type", out var typeEl) ? typeEl.GetString() ?? "generic" : "generic"
            );

            marker.status = obj.TryGetProperty("status", out var statusEl) ? statusEl.GetString() ?? "pending" : "pending";
            marker.createdAt = obj.TryGetProperty("createdAt", out var createdEl) && createdEl.ValueKind != JsonValueKind.Null ? createdEl.GetString() : null;
            marker.placedAt = obj.TryGetProperty("placedAt", out var placedEl) && placedEl.ValueKind != JsonValueKind.Null ? placedEl.GetString() : null;
            marker.lat = obj.TryGetProperty("lat", out var latEl) && latEl.ValueKind == JsonValueKind.Number ? latEl.GetDouble() : 0;
            marker.lng = obj.TryGetProperty("lng", out var lngEl) && lngEl.ValueKind == JsonValueKind.Number ? lngEl.GetDouble() : 0;

            if (obj.TryGetProperty("position", out var posEl) && posEl.ValueKind == JsonValueKind.Object)
            {
                marker.position = new MarkerPosition(new Vector3(
                    posEl.TryGetProperty("x", out var xEl) ? (float)xEl.GetDouble() : 0f,
                    posEl.TryGetProperty("y", out var yEl) ? (float)yEl.GetDouble() : 0f,
                    posEl.TryGetProperty("z", out var zEl) ? (float)zEl.GetDouble() : 0f
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
