using UnityEngine;
using IRIS.Anchors;

namespace IRIS.Core
{
    public class DesktopInputManager : MonoBehaviour
    {
        [SerializeField] private AnchorManager anchorManager;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                anchorManager.PlaceMarkerAtCamera();
            }

            // C key reserved for WS2 CalibrationManager
        }
    }
}
