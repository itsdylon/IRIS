using UnityEngine;

namespace IRIS.Core
{
    public class FlyCameraController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float fastMultiplier = 3f;
        [SerializeField] private float lookSpeed = 2f;

        private float _yaw;
        private float _pitch;

        private void Start()
        {
            var angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void Update()
        {
            // Right-click to look around
            if (Input.GetMouseButton(1))
            {
                _yaw += Input.GetAxis("Mouse X") * lookSpeed;
                _pitch -= Input.GetAxis("Mouse Y") * lookSpeed;
                _pitch = Mathf.Clamp(_pitch, -90f, 90f);
                transform.eulerAngles = new Vector3(_pitch, _yaw, 0f);

                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
            }

            // WASD + QE movement
            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? fastMultiplier : 1f);
            var move = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

            transform.position += move.normalized * speed * Time.deltaTime;
        }
    }
}
