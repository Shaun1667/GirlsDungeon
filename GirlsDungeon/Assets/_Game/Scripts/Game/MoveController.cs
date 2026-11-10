using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 마우스 입력을 커맨드로 바꾼다. 칸의 이동 · 사격 가능 여부와 숫자는 모두 규칙이 정한다.
    /// 탐험: 클릭한 본 칸까지 경로를 만들어 한 걸음씩 MoveCommand.
    /// 전투(내 차례): 이동 범위 표시 · 범위 안 칸 클릭 = CombatMoveCommand, 적 클릭 = AttackCommand, 마우스를 올리면 미리보기.
    /// </summary>
    public sealed class MoveController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform hoverMarker;
        [SerializeField] private float hoverMarkerHeight = 0.02f;
        [SerializeField] private RangeView rangeView;
        [Tooltip("Shows the blast cells while aiming the grenade.")]
        [SerializeField] private RangeView areaView;

        private readonly Queue<GridPos> pendingSteps = new Queue<GridPos>();
        private bool aiming;
        private Simulation simulation;
        private PlayerView playerView;
        private HudView hudView;
        private CombatSettings settings;
        private int moveCostTicks;
        private Dictionary<GridPos, int> reachable;
        private string reachableKey;

        public int PendingStepCount => pendingSteps.Count;
        public int ReachableCount => reachable?.Count ?? 0;
        public bool IsAiming => aiming;

        /// <summary>수류탄 조준 모드를 켜고 끈다 (수류탄 버튼). 내 차례에 아직 행동하지 않았을 때만 켤 수 있다.</summary>
        public void ToggleAim()
        {
            var world = simulation?.World;
            bool canAim = world != null && TurnFlow.IsPlayerTurn(world) && !world.PlayerActed;
            SetAiming(!aiming && canAim);
        }

        /// <summary>수류탄을 그 칸에 던진다. 못 던지면 false (조준 모드는 그대로).</summary>
        public bool RequestAreaAttack(GridPos center)
        {
            if (simulation == null || !TurnFlow.IsPlayerTurn(simulation.World) || playerView.IsMoving) return false;
            if (!simulation.Execute(new AreaAttackCommand(center, settings))) return false;
            SetAiming(false);
            return true;
        }

        private void SetAiming(bool value)
        {
            aiming = value;
            if (!aiming) areaView.Hide();
            hudView.SetAiming(aiming);
            reachableKey = null;
        }

        public void Bind(Simulation newSimulation, PlayerView newPlayerView, HudView newHudView, CombatSettings newSettings, int newMoveCostTicks)
        {
            simulation = newSimulation;
            playerView = newPlayerView;
            hudView = newHudView;
            settings = newSettings;
            moveCostTicks = newMoveCostTicks;
            pendingSteps.Clear();
            reachable = null;
            reachableKey = null;
            rangeView.Hide();
            aiming = false;
            areaView.Hide();
        }

        /// <summary>탐험 중 목표 칸까지 걷기 시작한다. 걷는 중이면 지금 칸에서 새 경로로 바꾼다. 갈 수 없으면 false.</summary>
        public bool RequestMove(GridPos target)
        {
            if (simulation == null || simulation.World.Mode != WorldMode.Exploration) return false;

            var world = simulation.World;
            var player = world.Player;
            if (!IsKnown(target)) return false;

            // 본 적 없는 칸은 지나가지 않는다 — 경로로 안 본 지형이 드러나지 않게.
            var path = GridNavigation.FindPath(world.Map, player.Position, target,
                cell => cell != player.Position && (world.IsOccupied(cell) || !IsKnown(cell)));
            if (path.Count == 0) return false;

            pendingSteps.Clear();
            foreach (var step in path) pendingSteps.Enqueue(step);
            return true;
        }

        /// <summary>전투 중 칸 클릭과 같다: 적이 있으면 사격, 아니면 이동. 아무것도 못 하면 false.</summary>
        public bool RequestCombatAction(GridPos cell)
        {
            if (simulation == null || !TurnFlow.IsPlayerTurn(simulation.World) || playerView.IsMoving) return false;

            var world = simulation.World;
            var target = world.UnitAt(cell);
            if (target != null && !target.IsPlayer) return simulation.Execute(new AttackCommand(target.Id, settings));
            return simulation.Execute(new CombatMoveCommand(cell));
        }

        private void Update()
        {
            if (simulation == null) return;

            var mode = simulation.World.Mode;
            if (mode != WorldMode.Exploration) pendingSteps.Clear();
            if (aiming && (!TurnFlow.IsPlayerTurn(simulation.World) || simulation.World.PlayerActed)) SetAiming(false);

            if (mode == WorldMode.Combat) HandleCombat();
            else
            {
                rangeView.Hide();
                reachableKey = null;
                if (mode == WorldMode.Exploration) HandleExploration();
                else SetHover(null);
            }
            AdvanceSteps();
        }

        private void HandleExploration()
        {
            var mouse = Mouse.current;
            var cell = mouse != null ? PointerCell(mouse.position.ReadValue()) : null;
            bool isWalkable = cell.HasValue && simulation.World.Map.IsWalkable(cell.Value) && IsKnown(cell.Value);
            SetHover(isWalkable ? cell : null);

            if (isWalkable && mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi()) RequestMove(cell.Value);
        }

        private void HandleCombat()
        {
            var world = simulation.World;
            bool canMove = TurnFlow.IsPlayerTurn(world) && !world.PlayerMoved && !world.PlayerActed && !playerView.IsMoving && !aiming;
            RefreshRange(canMove);

            var mouse = Mouse.current;
            var cell = mouse != null && TurnFlow.IsPlayerTurn(world) && !playerView.IsMoving ? PointerCell(mouse.position.ReadValue()) : null;
            if (aiming && mouse != null && mouse.rightButton.wasPressedThisFrame) SetAiming(false);
            if (!cell.HasValue)
            {
                SetHover(null);
                areaView.Hide();
                hudView.ClearPreview();
                return;
            }

            var target = world.UnitAt(cell.Value);
            bool clicked = mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi();
            if (aiming)
            {
                var area = AreaAttack.Preview(world, settings, world.Player, cell.Value);
                areaView.Show(area.Cells);
                hudView.ShowAreaPreview(area);
                SetHover(cell);
                if (clicked) RequestAreaAttack(cell.Value);
                return;
            }
            if (target != null && !target.IsPlayer && world.Combatants.Contains(target.Id))
            {
                var preview = CombatRules.Preview(world, settings, world.Player, world.Player.Position, target);
                hudView.ShowAttackPreview(target, preview, world.PlayerActed);
                SetHover(cell);
                if (clicked) RequestCombatAction(cell.Value);
                return;
            }

            if (canMove && reachable != null && reachable.ContainsKey(cell.Value) && cell.Value != world.Player.Position)
            {
                hudView.ShowMovePreview(WeakestCoverAgainstCombatants(cell.Value));
                SetHover(cell);
                if (clicked) RequestCombatAction(cell.Value);
                return;
            }

            SetHover(null);
            hudView.ClearPreview();
        }

        /// <summary>범위 칸은 상태가 바뀔 때만 다시 계산한다.</summary>
        private void RefreshRange(bool canMove)
        {
            var world = simulation.World;
            string key = canMove ? $"{world.Round}:{world.Player.Position}:{world.Units.Count}" : "none";
            if (key == reachableKey) return;
            reachableKey = key;

            if (!canMove)
            {
                reachable = null;
                rangeView.Hide();
                return;
            }

            var player = world.Player;
            reachable = CombatMovement.Reachable(world, player, player.MoveRange, world.Explored);
            var cells = new List<GridPos>();
            foreach (var cell in reachable.Keys) if (cell != player.Position) cells.Add(cell);
            rangeView.Show(cells);
        }

        /// <summary>그 칸에 섰을 때, 전투 중인 적 가운데 가장 약한 엄폐 (측면이 열린 적이 있으면 그 값).</summary>
        private CoverLevel WeakestCoverAgainstCombatants(GridPos cell)
        {
            var world = simulation.World;
            var weakest = CoverLevel.Full;
            bool any = false;
            foreach (int id in world.Combatants)
            {
                var enemy = world.FindUnit(id);
                if (enemy == null) continue;
                any = true;
                var cover = Cover.Against(world.Map, cell, enemy.Position);
                if (cover < weakest) weakest = cover;
            }
            return any ? weakest : CoverLevel.None;
        }

        /// <summary>한 번이라도 본 칸인지 (World.Explored).</summary>
        private bool IsKnown(GridPos cell)
        {
            var world = simulation.World;
            return world.Explored != null && Vision.IsSet(world.Explored, world.Map, cell);
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void AdvanceSteps()
        {
            if (pendingSteps.Count == 0 || playerView.IsMoving) return;

            var command = new MoveCommand(simulation.World.PlayerId, pendingSteps.Dequeue(), moveCostTicks);
            if (!simulation.Execute(command)) pendingSteps.Clear();
        }

        private GridPos? PointerCell(Vector2 screenPosition)
        {
            if (viewCamera == null || screenPosition.x < 0f || screenPosition.y < 0f
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
