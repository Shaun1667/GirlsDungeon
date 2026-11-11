using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 비스듬한 위에서 대상을 따라가는 카메라. Q/E로 일정 각도씩 돌리고, 마우스 휠로 확대 · 축소,
    /// 왼쪽(또는 가운데) 버튼 드래그로 바닥을 잡아끌듯 화면을 옮긴다. 옮긴 만큼은 대상이 걷기 시작하거나
    /// 따라갈 대상이 바뀌면 부드럽게 제자리로 돌아온다. 화면만 바뀌고 규칙에는 영향이 없다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        private GameBalance balance;
        private Camera cam;
        private Transform target;
        /// <summary>따라가던 대상이 사라지면(쓰러진 적) 돌아갈 대상 = 플레이어.</summary>
        private Transform fallback;
        private Vector3 focus;
        private float targetYaw;
        private float yaw;
        private float targetDistance;
        private float distance;
        /// <summary>드래그로 옮긴 만큼 (바닥 평면 위).</summary>
        private Vector3 panOffset;
        private bool recentering;
        private Vector3 lastTargetPosition;
        private PointerDrag leftDrag;
        private PointerDrag middleDrag;
        private Vector2 lastPointer;

        public float TargetYaw => targetYaw;
        public float TargetDistance => targetDistance;
        public Vector3 PanOffset => panOffset;

        /// <summary>드래그로 화면을 옮기는 중인가 (그동안 클릭 이동을 받지 않는다).</summary>
        public bool IsDragging => leftDrag.Dragging || middleDrag.Dragging;

        public void Bind(GameBalance newBalance, Transform newTarget)
        {
            balance = newBalance;
            cam = GetComponent<Camera>();
            target = fallback = newTarget;
            focus = lastTargetPosition = target.position;
            targetYaw = yaw = balance.CameraStartYaw;
            targetDistance = distance = balance.CameraDistance;
            panOffset = Vector3.zero;
            ApplyTransform();
        }

        /// <summary>따라갈 대상을 바꾼다 (적 차례에 움직이는 적 → 끝나면 플레이어). 부드럽게 옮겨 간다.</summary>
        public void SetTarget(Transform newTarget)
        {
            if (newTarget == null || newTarget == target) return;
            target = newTarget;
            lastTargetPosition = target.position;
            recentering = true;
        }

        public Transform Target => target;

        /// <summary>카메라를 돌린다. direction이 1이면 오른쪽, -1이면 왼쪽.</summary>
        public void Rotate(int direction)
        {
            targetYaw += direction * balance.CameraYawStep;
        }

        /// <summary>확대(steps &gt; 0) · 축소(steps &lt; 0). 거리는 BALANCE 3-21 ~ 3-22 사이.</summary>
        public void Zoom(int steps)
        {
            targetDistance = Mathf.Clamp(targetDistance - steps * balance.CameraZoomStep, balance.CameraMinDistance, balance.CameraMaxDistance);
        }

        /// <summary>드래그로 옮긴 화면을 대상 쪽으로 되돌린다.</summary>
        public void Recenter()
        {
            recentering = true;
        }

        private void LateUpdate()
        {
            if (balance == null) return;
            if (target == null) target = fallback;
            if (target == null) return;

            HandleInput();

            float dt = Time.unscaledDeltaTime;
            // 대상이 걷기 시작하면 옮긴 화면을 되돌린다 (걸어서 화면 밖으로 나가지 않게).
            if ((target.position - lastTargetPosition).sqrMagnitude > 0.0001f && !IsDragging) recentering = true;
            lastTargetPosition = target.position;
            if (recentering && !IsDragging)
            {
                panOffset = Vector3.Lerp(panOffset, Vector3.zero, 1f - Mathf.Exp(-balance.CameraFollowSharpness * dt));
                if (panOffset.sqrMagnitude < 0.0004f)
                {
                    panOffset = Vector3.zero;
                    recentering = false;
                }
            }

            focus = Vector3.Lerp(focus, target.position + panOffset, IsDragging ? 1f : 1f - Mathf.Exp(-balance.CameraFollowSharpness * dt));
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-balance.CameraRotateSharpness * dt));
            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-balance.CameraZoomSharpness * dt));
            ApplyTransform();
        }

        private void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.qKey.wasPressedThisFrame) Rotate(-1);
                if (keyboard.eKey.wasPressedThisFrame) Rotate(1);
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            var pointer = mouse.position.ReadValue();
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // 휠: 한 칸 = 한 단계 (기기마다 값의 크기가 달라 부호만 쓴다).
            float wheel = mouse.scroll.ReadValue().y;
            if (!overUi && Mathf.Abs(wheel) > 0.01f) Zoom(wheel > 0f ? 1 : -1);

            bool wasDragging = IsDragging;
            // UI 위에서 누른 것은 드래그로 치지 않는다 (버튼 누르다 화면이 밀리지 않게).
            if (!(overUi && mouse.leftButton.wasPressedThisFrame)) leftDrag.Update(mouse.leftButton, pointer, balance.CameraDragThresholdPixels);
            if (!(overUi && mouse.middleButton.wasPressedThisFrame)) middleDrag.Update(mouse.middleButton, pointer, balance.CameraDragThresholdPixels);

            if (IsDragging && wasDragging && GroundPoint(lastPointer, out var from) && GroundPoint(pointer, out var to))
            {
                // 바닥을 잡아끈다: 커서 아래 바닥 점이 커서를 따라오게 카메라를 반대로 옮긴다.
                panOffset += from - to;
                panOffset.y = 0f;
                panOffset = Vector3.ClampMagnitude(panOffset, balance.CameraMaxPanDistance);
                recentering = false;
            }
            lastPointer = pointer;
        }

        private bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, target.position.y, 0f));
            var ray = cam.ScreenPointToRay(screen);
            if (plane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        private void ApplyTransform()
        {
            var rotation = Quaternion.Euler(balance.CameraPitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
