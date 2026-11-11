using UnityEngine;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>비스듬한 위에서 대상을 따라가는 카메라. Q/E로 일정 각도씩 돌린다.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        private GameBalance balance;
        private Transform target;
        /// <summary>따라가던 대상이 사라지면(쓰러진 적) 돌아갈 대상 = 플레이어.</summary>
        private Transform fallback;
        private Vector3 focus;
        private float targetYaw;
        private float yaw;

        public float TargetYaw => targetYaw;

        public void Bind(GameBalance newBalance, Transform newTarget)
        {
            balance = newBalance;
            target = fallback = newTarget;
            focus = target.position;
            targetYaw = yaw = balance.CameraStartYaw;
            ApplyTransform();
        }

        /// <summary>따라갈 대상을 바꾼다 (적 차례에 움직이는 적 → 끝나면 플레이어). 부드럽게 옮겨 간다.</summary>
        public void SetTarget(Transform newTarget)
        {
            if (newTarget != null) target = newTarget;
        }

        public Transform Target => target;

        /// <summary>카메라를 돌린다. direction이 1이면 오른쪽, -1이면 왼쪽.</summary>
        public void Rotate(int direction)
        {
            targetYaw += direction * balance.CameraYawStep;
        }

        private void LateUpdate()
        {
            if (target == null) target = fallback;
            if (target == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) Rotate(-1);
                if (keyboard.eKey.wasPressedThisFrame) Rotate(1);
            }

            float dt = Time.unscaledDeltaTime;
            focus = Vector3.Lerp(focus, target.position, 1f - Mathf.Exp(-balance.CameraFollowSharpness * dt));
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-balance.CameraRotateSharpness * dt));
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            var rotation = Quaternion.Euler(balance.CameraPitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * balance.CameraDistance, rotation);
        }
    }
}
