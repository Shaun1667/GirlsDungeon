using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 비스듬한 위에서 대상을 따라가는 카메라. Q/E를 누르면 일정 각도씩 돌리고, 꾹 누르고 있으면 부드럽게 계속 돌며 뗀 곳에서 멈춘다.
    /// 마우스 휠로 확대 · 축소,
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
        /// <summary>Q/E를 누르고 있는 시간 (꾹 누르면 계속 돌기 — BALANCE 3-27 · 3-28).</summary>
        private float rotateHeld;

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

        /// <summary>확대(steps &gt; 0) · 축소(steps &lt; 0). 거리는 BALANCE 3-21 ~ 3-22 사이, 가까이선 칸 간격이 작다 (3-29 · 3-30).</summary>
        public void Zoom(int steps)
        {
            // 가까운 거리(3-29)보다 안쪽은 한 칸에 3-30만큼, 바깥은 3-23만큼. 경계는 넘지 않고 한 번 멈춘다.
            var from = balance.CameraCloseZoomFrom;
            for (var i = 0; i < Mathf.Abs(steps); i++)
            {
                if (steps > 0)
                    targetDistance = targetDistance > from + 0.0001f
                        ? Mathf.Max(targetDistance - balance.CameraZoomStep, from)
                        : targetDistance - balance.CameraCloseZoomStep;
                else
                    targetDistance = targetDistance < from - 0.0001f
                        ? Mathf.Min(targetDistance + balance.CameraCloseZoomStep, from)
                        : targetDistance + balance.CameraZoomStep;
                targetDistance = Mathf.Clamp(targetDistance, balance.CameraMinDistance, balance.CameraMaxDistance);
            }
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
                HandleHeldRotation(keyboard);
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

        /// <summary>
        /// Q/E를 꾹 누르면 (CameraHoldDelaySeconds 뒤부터) 초당 CameraHoldYawSpeed도씩 계속 돈다.
        /// 뗀 곳의 각도를 그대로 둔다 (회전 단위로 맞추지 않음). 둘 다 누르면 서로 상쇄.
        /// </summary>
        private void HandleHeldRotation(Keyboard keyboard)
        {
            int direction = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
            if (!keyboard.qKey.isPressed && !keyboard.eKey.isPressed)
            {
                rotateHeld = 0f;
                return;
            }
            rotateHeld += Time.unscaledDeltaTime;
            if (direction == 0 || rotateHeld < balance.CameraHoldDelaySeconds) return;
            // 목표 각도를 일정하게 밀면 LateUpdate의 부드러운 따라가기가 그대로 일정 속도 회전이 된다.
            targetYaw += direction * balance.CameraHoldYawSpeed * Time.unscaledDeltaTime;
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
            // 가까이 확대할수록 발 대신 몸 쪽을 본다 (3-31) — 가장 가까우면 캐릭터가 화면에 꽉 찬다.
            var from = balance.CameraCloseZoomFrom;
            var closeness = from > balance.CameraMinDistance ? Mathf.Clamp01((from - distance) / (from - balance.CameraMinDistance)) : 0f;
            var look = focus + Vector3.up * (balance.CameraCloseLookHeight * closeness);
            transform.SetPositionAndRotation(look - rotation * Vector3.forward * distance, rotation);
        }
    }
}
