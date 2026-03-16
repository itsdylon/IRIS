using UnityEngine;
using IRIS.Anchors;

namespace IRIS.Core
{
    public class DesktopInputManager : MonoBehaviour
    {
        [SerializeField] private AnchorManager anchorManager;
        [SerializeField] private CalibrationManager calibrationManager;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                anchorManager.PlaceMarkerAtCamera();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                calibrationManager.Calibrate();
            }
        }
    }
}
