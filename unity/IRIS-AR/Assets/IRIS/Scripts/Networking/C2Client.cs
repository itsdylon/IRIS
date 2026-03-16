using System;
using System.Collections;
using System.Collections.Generic;
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

        public event Action<AnchorSharedPayload> OnAnchorShared;
        public event Action<List<AnchorSharedPayload>> OnAnchorLoadResponse;
        public event Action<string> OnAnchorErased;
        public event Action<SessionCreatedPayload> OnSessionCreated;
        public event Action<SessionJoinedPayload> OnSessionJoined;
        public event Action<SessionStatePayload> OnSessionState;

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

            _socket.On("anchor:shared", (response) =>
            {
                var data = response.GetValue<JsonElement>();
                var payload = new AnchorSharedPayload
                {
                    anchorId = data.GetProperty("anchorId").GetString(),
                    groupUuid = data.GetProperty("groupUuid").GetString(),
                    sharedBy = data.TryGetProperty("sharedBy", out var sb) ? sb.GetString() : null,
                    sharedAt = data.TryGetProperty("sharedAt", out var sa) ? sa.GetString() : null,
                    calibrationLat = data.TryGetProperty("calibrationLat", out var cl) ? cl.GetDouble() : 0,
                    calibrationLng = data.TryGetProperty("calibrationLng", out var cln) ? cln.GetDouble() : 0,
                    calibrationAlt = data.TryGetProperty("calibrationAlt", out var ca) ? ca.GetDouble() : 0,
                };
                if (data.TryGetProperty("pose", out var poseEl) && poseEl.ValueKind == JsonValueKind.Object)
                {
                    payload.pose = ParsePose(poseEl);
                }
                Debug.Log($"[C2Client] anchor:shared — {payload.anchorId}");
                OnAnchorShared?.Invoke(payload);
            });

            _socket.On("anchor:load:response", (response) =>
            {
                var data = response.GetValue<JsonElement>();
                var anchors = new List<AnchorSharedPayload>();
                if (data.TryGetProperty("anchors", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        var a = new AnchorSharedPayload
                        {
                            anchorId = item.GetProperty("anchorId").GetString(),
                            groupUuid = item.GetProperty("groupUuid").GetString(),
                            sharedBy = item.TryGetProperty("sharedBy", out var sb) ? sb.GetString() : null,
                            sharedAt = item.TryGetProperty("sharedAt", out var sa) ? sa.GetString() : null,
                            calibrationLat = item.TryGetProperty("calibrationLat", out var cl) ? cl.GetDouble() : 0,
                            calibrationLng = item.TryGetProperty("calibrationLng", out var cln) ? cln.GetDouble() : 0,
                            calibrationAlt = item.TryGetProperty("calibrationAlt", out var ca) ? ca.GetDouble() : 0,
                        };
                        if (item.TryGetProperty("pose", out var poseEl) && poseEl.ValueKind == JsonValueKind.Object)
                        {
                            a.pose = ParsePose(poseEl);
                        }
                        anchors.Add(a);
                    }
                }
                Debug.Log($"[C2Client] anchor:load:response — {anchors.Count} anchors");
                OnAnchorLoadResponse?.Invoke(anchors);
            });

            _socket.On("anchor:erased", (response) =>
            {
                var data = response.GetValue<JsonElement>();
                var anchorId = data.GetProperty("anchorId").GetString();
                Debug.Log($"[C2Client] anchor:erased — {anchorId}");
                OnAnchorErased?.Invoke(anchorId);
            });

            _socket.On("session:created", (response) =>
            {
                var data = response.GetValue<JsonElement>();
                var payload = new SessionCreatedPayload
                {
                    sessionId = data.GetProperty("sessionId").GetString(),
                    hostDeviceId = data.GetProperty("hostDeviceId").GetString(),
                };
                Debug.Log($"[C2Client] session:created — {payload.sessionId}");
                OnSessionCreated?.Invoke(payload);
            });

            _socket.On("session:joined", (response) =>
            {
                var data = response.GetValue<JsonElement>();
                var payload = new SessionJoinedPayload
                {
                    sessionId = data.GetProperty("sessionId").GetString(),
                    deviceId = data.GetProperty("deviceId").GetString(),
                };
                Debug.Log($"[C2Client] session:joined — {payload.deviceId} → {payload.sessionId}");
                OnSessionJoined?.Invoke(payload);
            });

            _socket.On("session:state", (response) =>
            {
                var data = response.GetValue<JsonElement>();
                var payload = new SessionStatePayload
                {
                    sessionId = data.GetProperty("sessionId").GetString(),
                    hostDeviceId = data.GetProperty("hostDeviceId").GetString(),
                };
                Debug.Log($"[C2Client] session:state — {payload.sessionId}");
                OnSessionState?.Invoke(payload);
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

        public void EmitMarkerCreate(double lat, double lng, string label, string type)
        {
            if (!IsConnected) return;

            var payload = new MarkerCreatePayload
            {
                lat = lat,
                lng = lng,
                label = label,
                type = type
            };

            _socket.Emit("marker:create", payload);
            Debug.Log($"[C2Client] marker:create — '{label}' at ({lat:F6}, {lng:F6})");
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

        public void EmitSessionCreate()
        {
            if (!IsConnected) return;
            _socket.Emit("session:create", new SessionCreatePayload());
            Debug.Log("[C2Client] session:create");
        }

        public void EmitSessionJoin(string sessionId)
        {
            if (!IsConnected) return;
            _socket.Emit("session:join", new SessionJoinPayload { sessionId = sessionId });
            Debug.Log($"[C2Client] session:join — {sessionId}");
        }

        public void EmitAnchorShare(string sessionId, string anchorId, string groupUuid, UnityEngine.Pose pose, double lat, double lng, double alt)
        {
            if (!IsConnected) return;
            var payload = new AnchorSharePayload
            {
                sessionId = sessionId,
                anchorId = anchorId,
                groupUuid = groupUuid,
                pose = new PosePayload
                {
                    px = pose.position.x,
                    py = pose.position.y,
                    pz = pose.position.z,
                    rx = pose.rotation.x,
                    ry = pose.rotation.y,
                    rz = pose.rotation.z,
                    rw = pose.rotation.w,
                },
                calibrationLat = lat,
                calibrationLng = lng,
                calibrationAlt = alt,
            };
            _socket.Emit("anchor:share", payload);
            Debug.Log($"[C2Client] anchor:share — {anchorId} in group {groupUuid}");
        }

        public void EmitAnchorLoad(string groupUuid)
        {
            if (!IsConnected) return;
            _socket.Emit("anchor:load", new AnchorLoadPayload { groupUuid = groupUuid });
            Debug.Log($"[C2Client] anchor:load — {groupUuid}");
        }

        public void EmitAnchorErase(string anchorId)
        {
            if (!IsConnected) return;
            _socket.Emit("anchor:erase", new AnchorErasePayload { anchorId = anchorId });
            Debug.Log($"[C2Client] anchor:erase — {anchorId}");
        }

        private PosePayload ParsePose(JsonElement el)
        {
            return new PosePayload
            {
                px = el.TryGetProperty("px", out var px) ? (float)px.GetDouble() : 0f,
                py = el.TryGetProperty("py", out var py) ? (float)py.GetDouble() : 0f,
                pz = el.TryGetProperty("pz", out var pz) ? (float)pz.GetDouble() : 0f,
                rx = el.TryGetProperty("rx", out var rx) ? (float)rx.GetDouble() : 0f,
                ry = el.TryGetProperty("ry", out var ry) ? (float)ry.GetDouble() : 0f,
                rz = el.TryGetProperty("rz", out var rz) ? (float)rz.GetDouble() : 0f,
                rw = el.TryGetProperty("rw", out var rw) ? (float)rw.GetDouble() : 1f,
            };
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
