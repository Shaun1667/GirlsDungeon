using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>마우스 클릭을 경로로 바꾸고 한 걸음씩 규칙에 넘긴다. 칸의 이동 가능 여부는 규칙이 정한다.</summary>
    public sealed class MoveController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform hoverMarker;
        [SerializeField] private float hoverMarkerHeight = 0.02f;

        private readonly Queue<GridPos> pendingSteps = new Queue<GridPos>();
        private Simulation simulation;
        private PlayerView playerView;
        private int moveCostTicks;

        public int PendingStepCount => pendingSteps.Count;

        public void Bind(Simulation newSimulation, PlayerView newPlayerView, int newMoveCostTicks)
        {
            simulation = newSimulation;
            playerView = newPlayerView;
            moveCostTicks = newMoveCostTicks;
            pendingSteps.Clear();
        }

        /// <summary>목표 칸까지 걷기 시작한다. 걷는 중이면 지금 칸에서 새 경로로 바꾼다. 갈 수 없으면 false.</summary>
        public bool RequestMove(GridPos target)
        {
            if (simulation == null) return false;

            var world = simulation.World;
            var player = world.Player;
            var path = GridNavigation.FindPath(world.Map, player.Position, target,
                cell => cell != player.Position && world.IsOccupied(cell));
            if (path.Count == 0) return false;

            pendingSteps.Clear();
            foreach (var step in path) pendingSteps.Enqueue(step);
            return true;
        }

        private void Update()
        {
            if (simulation == null) return;

            HandleMouse();
            AdvanceSteps();
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null || viewCamera == null)
            {
                SetHover(null);
                return;
            }

            var cell = PointerCell(mouse.position.ReadValue());
            bool isWalkable = cell.HasValue && simulation.World.Map.IsWalkable(cell.Value);
            SetHover(isWalkable ? cell : null);

            if (isWalkable && mouse.leftButton.wasPressedThisFrame) RequestMove(cell.Value);
        }

        private void AdvanceSteps()
        {
            if (pendingSteps.Count == 0 || playerView.IsMoving) return;

            var command = new MoveCommand(simulation.World.PlayerId, pendingSteps.Dequeue(), moveCostTicks);
            if (!simulation.Execute(command)) pendingSteps.Clear();
        }

        private GridPos? PointerCell(Vector2 screenPosition)
        {
            if (screenPosition.x < 0f || screenPosition.y < 0f
                || screenPosition.x > Screen.width || screenPosition.y > Screen.height) return null;

            var ray = viewCamera.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return null;
            return GridSpace.ToCell(ray.GetPoint(distance));
        }

        private void SetHover(GridPos? cell)
        {
            if (hoverMarker == null) return;

            hoverMarker.gameObject.SetActive(cell.HasValue);
            if (cell.HasValue) hoverMarker.position = GridSpace.ToWorld(cell.Value) + Vector3.up * hoverMarkerHeight;
        }
    }
}
