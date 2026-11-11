using UnityEngine;
using UnityEngine.InputSystem.Controls;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 왼쪽 버튼 한 번 누름이 "클릭"인지 "드래그"인지 가른다. 누른 자리에서 기준 픽셀(BALANCE 3-25)보다
    /// 멀리 움직이면 드래그(카메라 이동)이고, 그 전에 떼면 클릭이다. 카메라와 클릭 이동이 같은 규칙을 쓴다.
    /// </summary>
    public struct PointerDrag
    {
        private Vector2 pressPosition;
        private bool pressed;

        /// <summary>지금 누른 채로 기준보다 멀리 움직였는가.</summary>
        public bool Dragging { get; private set; }

        /// <summary>이번 프레임에 클릭(움직이지 않고 뗌)이 끝났는가.</summary>
        public bool Clicked { get; private set; }

        /// <summary>프레임마다 한 번 부른다.</summary>
        public void Update(ButtonControl button, Vector2 position, float thresholdPixels)
        {
            Clicked = false;
            if (button == null) return;
            if (button.wasPressedThisFrame)
            {
                pressed = true;
                Dragging = false;
                pressPosition = position;
            }
            if (pressed && !Dragging && (position - pressPosition).sqrMagnitude > thresholdPixels * thresholdPixels) Dragging = true;
            if (button.wasReleasedThisFrame)
            {
                Clicked = pressed && !Dragging;
                pressed = false;
                Dragging = false;
            }
        }
    }
}
