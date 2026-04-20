using UnityEngine;
using IRIS.Anchors;
using IRIS.Networking;
using IRIS.UI;

namespace IRIS.Core
{
    /// <summary>
    /// Automatically instantiates and configures the FieldStatusHUD at runtime.
    /// Attach this to any active GameObject in the scene (e.g., a manager).
    /// </summary>
    public class HUDSetup : MonoBehaviour
    {
        [SerializeField] private C2Client c2Client;
        [SerializeField] private CalibrationManager calibrationManager;

        private void Awake()
        {
            // Auto-find references if not assigned
            if (c2Client == null)
            {
                c2Client = FindObjectOfType<C2Client>();
            }
            if (calibrationManager == null)
            {
                calibrationManager = FindObjectOfType<CalibrationManager>();
            }

            // Create HUD GameObject and attach component
            var hudGo = new GameObject("FieldStatusHUD_Instance");
            var hud = hudGo.AddComponent<FieldStatusHUD>();

            // Wire up references via reflection (since fields are private [SerializeField])
            var c2Field = typeof(FieldStatusHUD).GetField("c2Client", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var calibField = typeof(FieldStatusHUD).GetField("calibrationManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (c2Field != null)
                c2Field.SetValue(hud, c2Client);
            if (calibField != null)
                calibField.SetValue(hud, calibrationManager);

            Debug.Log("[HUDSetup] FieldStatusHUD instantiated and configured");
        }
    }
}
