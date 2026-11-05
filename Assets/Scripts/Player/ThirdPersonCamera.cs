using UnityEngine;

namespace Zombio
{
    [RequireComponent(typeof(Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public float FollowSharpness = 18f;
        public float RotationSensitivity = 2f;
        public float Distance = 5f;
        public float ShoulderOffset = .6f;
        public float MinimumPitch = -25f;
        public float MaximumPitch = 65f;
        public float Yaw { get; private set; }
        public Camera Camera { get; private set; }
        PlayerController player;
        float pitch = 14f, shake, recoil, currentDistance;
        Vector3 followedTarget;
        bool positioned;

        public void Configure(PlayerController target)
        {
            player = target;
            Camera = GetComponent<Camera>();
            Camera.nearClipPlane = .08f;
            Camera.farClipPlane = 180f;
            Camera.fieldOfView = 67f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(.075f, .12f, .17f);
            Yaw = player != null ? player.transform.eulerAngles.y : 0f;
            currentDistance = Distance;
            pitch = 14f;
            recoil = shake = 0f;
            positioned = false;
        }

        void LateUpdate()
        {
            if (player == null || Camera == null) return;
            GameSession session = GameSession.Instance;
            bool active = session != null && session.IsPlaying && !player.IsDead;
            float dt = Time.deltaTime;
            if (active)
            {
                float sensitivity = session.Sensitivity > 0f ? session.Sensitivity : RotationSensitivity;
                Yaw += Input.GetAxisRaw("Mouse X") * sensitivity;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, MinimumPitch, MaximumPitch);
                Distance = Mathf.Clamp(Distance - Input.mouseScrollDelta.y * .45f, 3.5f, 7.5f);
            }
            float blend = 1f - Mathf.Exp(-FollowSharpness * Mathf.Max(dt, .0001f));
            Vector3 target = player.transform.position + Vector3.up * (player.IsCrouching ? 1.05f : 1.48f);
            if (!positioned) { followedTarget = target; positioned = true; }
            else followedTarget = Vector3.Lerp(followedTarget, target, blend);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 12f);
            Quaternion rotation = Quaternion.Euler(pitch - recoil, Yaw, 0f);
            float wantedDistance = player.IsAiming ? 2.65f : Distance;
            currentDistance = Mathf.Lerp(currentDistance, wantedDistance, blend);
            float shoulder = player.IsAiming ? .8f : ShoulderOffset;
            Vector3 offset = rotation * new Vector3(shoulder, .12f, -currentDistance);
            Vector3 wantedPosition = followedTarget + offset;
            // Sweep the whole offset from the target, including its shoulder component.
            RaycastHit[] hits = Physics.SphereCastAll(followedTarget, .23f, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float closest = offset.magnitude;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(player.transform) || hit.collider.GetComponentInParent<EnemyActor>() != null) continue;
                if (hit.distance < closest) closest = Mathf.Max(.16f, hit.distance - .08f);
            }
            if (closest < offset.magnitude) wantedPosition = followedTarget + offset.normalized * closest;
            shake = Mathf.MoveTowards(shake, 0f, dt * .65f);
            Vector3 shakeOffset = active ? Random.insideUnitSphere * shake * .18f : Vector3.zero;
            transform.SetPositionAndRotation(wantedPosition + shakeOffset, rotation);
            Camera.fieldOfView = Mathf.Lerp(Camera.fieldOfView, player.IsAiming ? 54f : (player.IsSprinting ? 73f : 67f), blend);
        }

        public void Shake(float amount) { shake = Mathf.Clamp(shake + amount, 0f, .8f); }
        public void Kick(float amount) { recoil = Mathf.Clamp(recoil + amount, 0f, 7f); Shake(amount * .025f); }
    }
}
