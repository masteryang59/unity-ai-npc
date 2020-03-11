using UnityEngine;

namespace UnityAiNpc.Gameplay
{
    /// <summary>WASD / arrow-key movement relative to the camera, with gravity. Disabled while talking.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float turnSpeed = 12f;
        [SerializeField] private float gravity = -20f;

        private CharacterController _controller;
        private float _verticalVelocity;

        /// <summary>When false the player stands still (e.g. while the dialogue box has focus).</summary>
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            Vector3 input = InputEnabled
                ? new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"))
                : Vector3.zero;
            input = Vector3.ClampMagnitude(input, 1f);

            Vector3 move = input;
            if (cameraTransform != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                move = forward * input.z + right * input.x;
            }

            if (move.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            _verticalVelocity = _controller.isGrounded ? -1f : _verticalVelocity + gravity * Time.deltaTime;
            _controller.Move((move * moveSpeed + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        /// <summary>Moves the player without the CharacterController fighting back.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
        }
    }
}
