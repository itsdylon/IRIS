using UnityEngine;

namespace IRIS.Networking
{
    /// <summary>
    /// Stub for Socket.IO client connection to the C2 server.
    /// Will be implemented in a future sprint with NativeWebSocket or SocketIOUnity.
    /// </summary>
    public class C2Client : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "http://localhost:3000";

        public bool IsConnected { get; private set; }

        private void Start()
        {
            Debug.Log($"[C2Client] Server URL configured: {serverUrl} (not yet connected — stub)");
        }
    }
}
