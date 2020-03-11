using UnityEngine;

namespace UnityAiNpc.Gameplay
{
    /// <summary>
    /// Third-person camera that trails the player. During a conversation it swings to an
    /// over-the-shoulder shot framing the NPC.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 4.5f, -7f);
        [SerializeField] private float lookHeight = 1.2f;
        [SerializeField] private float smoothing = 5f;

        [Header("Conversation framing")]
        [SerializeField] private float shoulderDistance = 2.6f;
        [SerializeField] private float shoulderHeight = 2.1f;
        [SerializeField] private float shoulderSide = 1.1f;
        [SerializeField] private float focusLookHeight = 1.45f;

        private Transform _focus;

        public Transform Target { get => target; set => target = value; }

        /// <summary>Frame <paramref name="npc"/> over the player's shoulder; null returns to follow mode.</summary>
        public void SetFocus(Transform npc) => _focus = npc;

        private void LateUpdate()
        {
            if (target == null) return;
            ComputePose(out var position, out var lookAt);

            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, position, k);
            var rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, k);
        }

        /// <summary>Jumps straight to the current desired pose (scene load, teleports).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            ComputePose(out var position, out var lookAt);
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
        }

        private void ComputePose(out Vector3 position, out Vector3 lookAt)
        {
            if (_focus != null)
            {
                Vector3 toNpc = Vector3.ProjectOnPlane(_focus.position - target.position, Vector3.up);
                if (toNpc.sqrMagnitude < 0.01f) toNpc = target.forward;
                toNpc.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, toNpc);

                position = target.position - toNpc * shoulderDistance + side * shoulderSide + Vector3.up * shoulderHeight;
                lookAt = Vector3.Lerp(target.position, _focus.position, 0.75f) + Vector3.up * focusLookHeight;
            }
            else
            {
                position = target.position + followOffset;
                lookAt = target.position + Vector3.up * lookHeight;
            }
        }
    }
}
