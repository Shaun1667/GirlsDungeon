using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>조준 모드 (스킬 버튼).</summary>
    public enum AimMode
    {
        None = 0,
        Grenade = 1,
        Push = 2,
    }

    /// <summary>
    /// 마우스 입력을 커맨드로 바꾼다. 칸의 이동 · 사격 가능 여부와 숫자는 모두 규칙이 정한다.
    /// 탐험: 클릭한 본 칸까지 경로를 만들어 한 걸음씩 MoveCommand.
    /// 전투(내 차례): 이동 범위 표시 · 범위 안 칸 클릭 = CombatMoveCommand, 적 클릭 = 사격 대상 고르기(노란 선 · 미리보기 · 확인 버튼),
    /// 확인(버튼 · Enter · 사격 카드) = AttackCommand, 오른쪽 클릭 · Esc = 취소. 마우스를 올리면 미리보기.
    /// 조준 모드: 수류탄 = 칸 클릭으로 AreaAttackCommand, 밀치기 = 마우스 쪽 방향 · 옆 줄로 PushCommand.
    /// </summary>
    public sealed class MoveController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform hoverMarker;
        [SerializeField] private float hoverMarkerHeight = 0.02f;
        [SerializeField] private RangeView rangeView;
        [Tooltip("Shows the blast cells while aiming the grenade.")]
        [SerializeField] private RangeView areaView;
        [Tooltip("Shows the push cells while aiming the push skill.")]
        [SerializeField] private RangeView pushAreaView;
        [Tooltip("Hidden template copied for each line from the player to an enemy in range.")]
        [SerializeField] private LineRenderer targetLine;
        [SerializeField] private float targetLineHeight = 1.1f;
        [Tooltip("Line to an enemy in range that has no cover against the player.")]
        [SerializeField] private Material openLineMaterial;
        [Tooltip("Line to an enemy in range that gets cover.")]
        [SerializeField] private Material coverLineMaterial;
        [Tooltip("Line to the selected target.")]
        [SerializeField] private Material selectedLineMaterial;

        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<string> lineKinds = new List<string>();

        /// <summary>고른 사격 대상 (없으면 -1). 적을 클릭하면 고르기만 하고, 확인(버튼 · Enter · 사격 카드)을 눌러야 쏜다.</summary>
        private int selectedTargetId = -1;

        private readonly Queue<GridPos> pendingSteps = new Queue<GridPos>();
        private int pendingFloor;
        /// <summary>걸음이 끝나면 열 보급 상자 (탐험 중 먼 상자를 클릭했을 때).</summary>
        private GridPos? pendingCrate;
        private AimMode aim;
        /// <summary>왼쪽 버튼: 움직이지 않고 떼면 클릭, 기준(BALANCE 3-25)보다 움직이면 카메라 드래그.</summary>
        private PointerDrag leftClick;
        private float dragThresholdPixels;
        private Simulation simulation;
        private EventPlayer eventPlayer;
        private PlayerView playerView;
        private HudView hudView;
        private CombatSettings settings;
        private int moveCostTicks;
        private Dictionary<GridPos, int> reachable;
        private string reachableKey;

        public int PendingStepCount => pendingSteps.Count;
        public int ReachableCount => reachable?.Count ?? 0;
        public bool IsAiming => aim != AimMode.None;
        public AimMode Aim => aim;
        public int SelectedTargetId => selectedTargetId;
        /// <summary>고른 대상까지 선이 그려져 있다.</summary>
        public bool TargetLineShown => lineKinds.Contains("selected");

        /// <summary>지금 그린 선들 (디버그): "적ID:open|cover|selected".</summary>
        public string TargetLinesSummary => string.Join(", ", lineKinds);

        /// <summary>사격 대상을 고른다 (노란 선 · 미리보기 · 확인 버튼). 전투 중인 적이 아니거나 지금 쏠 수 없으면 false.</summary>
        public bool SelectTarget(int unitId)
        {
            if (simulation == null || Busy) return false;
            var world = simulation.World;
            var target = world.FindUnit(unitId);
            if (target == null || target.IsPlayer || !world.Combatants.Contains(unitId) || !Loadout.CanShoot(world, settings)) return false;
            SetAiming(AimMode.None);
            selectedTargetId = unitId;
            return true;
        }

        /// <summary>고른 대상을 쏜다 (확인 버튼 · Enter · 사격 카드). 고른 대상이 없거나 쏠 수 없으면 false — 고른 상태는 남는다.</summary>
        public bool ConfirmAttack()
        {
            if (simulation == null || Busy || selectedTargetId < 0) return false;
            int id = selectedTargetId;
            if (!simulation.Execute(new AttackCommand(id, settings))) return false;
            ClearTarget();
            return true;
        }

        public void ClearTarget()
        {
            selectedTargetId = -1;
            if (hudView != null) hudView.ShowAttackConfirm(false, false);
        }

        /// <summary>
        /// 지금 쏠 수 있는 적(사거리 · 사선 안, CombatRules.Preview가 쏠 수 있다고 한 적)마다 플레이어에서 선을 긋는다
        /// (결정 2026-11-11, 사용자): 엄폐를 받는 적 = 흰 선, 엄폐가 없는 적 = 노란 선, 고른 적 = 밝은 노란 선.
        /// 쏠 수 없는 때(적 차례 · 재생 중 · 스킬 조준 · 이미 쐈음)에는 모두 숨긴다.
        /// </summary>
        private void RefreshTargetLines(World world, bool show)
        {
            lineKinds.Clear();
            int used = 0;
            if (show)
            {
                var player = world.Player;
                var from = GridSpace.ToWorld(player.Position) + Vector3.up * targetLineHeight;
                foreach (int id in world.Combatants)
                {
                    var enemy = world.FindUnit(id);
                    if (enemy == null) continue;
                    var preview = CombatRules.Preview(world, settings, player, player.Position, enemy);
                    if (!preview.CanAttack) continue;

                    string kind = id == selectedTargetId ? "selected" : preview.Cover != CoverLevel.None ? "cover" : "open";
                    var line = LineAt(used++);
                    line.sharedMaterial = kind == "selected" ? selectedLineMaterial : kind == "cover" ? coverLineMaterial : openLineMaterial;
                    line.SetPosition(0, from);
                    line.SetPosition(1, GridSpace.ToWorld(enemy.Position) + Vector3.up * targetLineHeight);
                    line.enabled = true;
                    lineKinds.Add(id + ":" + kind);
                }
            }
            for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
        }

        private LineRenderer LineAt(int index)
        {
            while (lines.Count <= index)
            {
                var line = Instantiate(targetLine, targetLine.transform.parent);
                line.name = "TargetLine " + lines.Count;
                lines.Add(line);
            }
            return lines[index];
        }

        /// <summary>수류탄 조준 모드를 켜고 끈다 (수류탄 버튼). 내 차례에 아직 행동하지 않았을 때만 켤 수 있다.</summary>
        public void ToggleAim() => ToggleAim(AimMode.Grenade);

        /// <summary>밀치기 조준 모드를 켜고 끈다 (밀치기 버튼).</summary>
        public void TogglePushAim() => ToggleAim(AimMode.Push);

        private void ToggleAim(AimMode mode)
        {
            var world = simulation?.World;
            bool canAim = world != null && TurnFlow.IsPlayerTurn(world) && !world.PlayerActed;
            SetAiming(aim != mode && canAim ? mode : AimMode.None);
        }

        /// <summary>마우스 칸 쪽으로 밀치기를 쓴다 (방향 · 옆 줄은 Push.Aim). 못 쓰면 false.</summary>
        public bool RequestPush(GridPos pointer)
        {
            if (simulation == null || !TurnFlow.IsPlayerTurn(simulation.World) || Busy) return false;
            Push.Aim(simulation.World.Player.Position, pointer, out var direction, out int side);
            if (!simulation.Execute(new PushCommand(direction, side, settings))) return false;
            SetAiming(AimMode.None);
            return true;
        }

        /// <summary>수류탄을 그 칸에 던진다. 못 던지면 false (조준 모드는 그대로).</summary>
        public bool RequestAreaAttack(GridPos center)
        {
            if (simulation == null || !TurnFlow.IsPlayerTurn(simulation.World) || Busy) return false;
            if (!simulation.Execute(new AreaAttackCommand(center, settings))) return false;
            SetAiming(AimMode.None);
            return true;
        }

        /// <summary>감시 버튼: 사격 대신 감시를 걸고 차례를 끝낸다. 못 하면 false.</summary>
        public bool RequestOverwatch()
        {
            if (simulation == null || !TurnFlow.IsPlayerTurn(simulation.World) || Busy) return false;
            SetAiming(AimMode.None);
            return simulation.Execute(new OverwatchCommand(settings));
        }

        /// <summary>조준 중인 스킬을 끈다 (스킬 바 "사격" · 턴 종료).</summary>
        public void CancelAim() => SetAiming(AimMode.None);

        /// <summary>화면이 사건을 재생하는 중(적 차례 연출)이거나 플레이어가 걷는 중이면 입력을 받지 않는다.</summary>
        private bool Busy => playerView.IsMoving || (eventPlayer != null && eventPlayer.IsPlaying);

        private void SetAiming(AimMode mode)
        {
            aim = mode;
            if (mode != AimMode.None) ClearTarget();
            if (aim != AimMode.Grenade) areaView.Hide();
            if (aim != AimMode.Push) pushAreaView.Hide();
            hudView.SetAiming(aim);
            reachableKey = null;
        }

        public void Bind(Simulation newSimulation, EventPlayer newEventPlayer, PlayerView newPlayerView, HudView newHudView, CombatSettings newSettings, int newMoveCostTicks, float newDragThresholdPixels)
        {
            dragThresholdPixels = newDragThresholdPixels;
            simulation = newSimulation;
            eventPlayer = newEventPlayer;
            playerView = newPlayerView;
            hudView = newHudView;
            settings = newSettings;
            moveCostTicks = newMoveCostTicks;
            pendingSteps.Clear();
            pendingCrate = null;
            reachable = null;
            reachableKey = null;
            ClearTarget();
            rangeView.Hide();
            aim = AimMode.None;
            areaView.Hide();
            pushAreaView.Hide();
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
            pendingCrate = null;
            foreach (var step in path) pendingSteps.Enqueue(step);
            pendingFloor = world.FloorIndex;
            return true;
        }

        /// <summary>
        /// 보급 상자를 연다. 바로 옆이면 바로 열고, 탐험 중 멀면 가장 가까운 옆 칸까지 걸어간 뒤 연다. 못 하면 false.
        /// </summary>
        public bool RequestOpenCrate(GridPos crate)
        {
            if (simulation == null || Busy) return false;
            var world = simulation.World;
            if (!world.Map.InBounds(crate) || world.Map.Get(crate) != TileType.SupplyCrate || !IsKnown(crate)) return false;

            var player = world.Player;
            if (Loadout.CellDistance(player.Position, crate) == 1)
                return simulation.Execute(new OpenSupplyCrateCommand(crate, settings));
            if (world.Mode != WorldMode.Exploration) return false;

            // 상자 옆 칸 중 가장 짧은 경로 (이웃 순서 고정).
            List<GridPos> best = null;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var side = new GridPos(crate.X + dx, crate.Y + dy);
                    if ((dx == 0 && dy == 0) || !world.Map.IsWalkable(side) || !IsKnown(side)) continue;
                    var path = GridNavigation.FindPath(world.Map, player.Position, side,
                        cell => cell != player.Position && (world.IsOccupied(cell) || !IsKnown(cell)));
                    if (path.Count > 0 && (best == null || path.Count < best.Count)) best = path;
                }
            }
            if (best == null) return false;

            pendingSteps.Clear();
            foreach (var step in best) pendingSteps.Enqueue(step);
            pendingFloor = world.FloorIndex;
            pendingCrate = crate;
            return true;
        }

        /// <summary>
        /// 적이 있으면 바로 사격, 아니면 이동 (디버그 · 확인용 지름길). 마우스 클릭은 적을 고르기만 하고 확인을 받는다 (HandleCombat).
        /// 아무것도 못 하면 false.
        /// </summary>
        public bool RequestCombatAction(GridPos cell)
        {
            if (simulation == null || !TurnFlow.IsPlayerTurn(simulation.World) || Busy) return false;

            var world = simulation.World;
            var target = world.UnitAt(cell);
            if (target != null && !target.IsPlayer) return simulation.Execute(new AttackCommand(target.Id, settings));
            ClearTarget();
            return simulation.Execute(new CombatMoveCommand(cell, settings));
        }

        /// <summary>고른 대상이 아직 쏠 수 있는 대상인지 보고, 아니면 고른 것을 푼다.</summary>
        private Unit RefreshSelection(World world)
        {
            if (selectedTargetId < 0) return null;
            var target = world.FindUnit(selectedTargetId);
            bool valid = target != null && world.Mode == WorldMode.Combat && TurnFlow.IsPlayerTurn(world)
                && world.Combatants.Contains(selectedTargetId) && Loadout.CanShoot(world, settings) && aim == AimMode.None;
            if (!valid)
            {
                ClearTarget();
                return null;
            }
            return target;
        }

        private void Update()
        {
            if (simulation == null) return;
            var pointerMouse = Mouse.current;
            if (pointerMouse != null) leftClick.Update(pointerMouse.leftButton, pointerMouse.position.ReadValue(), dragThresholdPixels);

            var mode = simulation.World.Mode;
            // 전투가 시작되거나 무기 비교 창이 뜨면 걷던 길을 멈춘다.
            if (mode != WorldMode.Exploration || hudView.IsWeaponOfferOpen)
            {
                pendingSteps.Clear();
                pendingCrate = null;
            }
            if (aim != AimMode.None && (!TurnFlow.IsPlayerTurn(simulation.World) || simulation.World.PlayerActed)) SetAiming(AimMode.None);

            if (mode == WorldMode.Combat) HandleCombat();
            else
            {
                if (selectedTargetId >= 0) ClearTarget();
                RefreshTargetLines(simulation.World, false);
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
            var map = simulation.World.Map;
            bool isWalkable = cell.HasValue && map.IsWalkable(cell.Value) && IsKnown(cell.Value);
            bool isCrate = cell.HasValue && map.InBounds(cell.Value) && map.Get(cell.Value) == TileType.SupplyCrate && IsKnown(cell.Value);
            SetHover(isWalkable || isCrate ? cell : null);

            bool clicked = mouse != null && leftClick.Clicked && !IsPointerOverUi();
            if (isWalkable && clicked) RequestMove(cell.Value);
            if (isCrate && clicked) RequestOpenCrate(cell.Value);
        }

        private void HandleCombat()
        {
            var world = simulation.World;
            // 권총은 쏜 뒤에도 이동할 수 있다 — 판정은 규칙(Loadout.CanCombatMove) 한 곳.
            bool canMove = Loadout.CanCombatMove(world, settings) && !Busy && aim == AimMode.None;
            RefreshRange(canMove);

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            var cell = mouse != null && TurnFlow.IsPlayerTurn(world) && !Busy ? PointerCell(mouse.position.ReadValue()) : null;
            if (aim != AimMode.None && mouse != null && mouse.rightButton.wasPressedThisFrame) SetAiming(AimMode.None);

            // 고른 사격 대상: 노란 선 · 확인 버튼. 오른쪽 클릭 · Esc = 취소, Enter = 확인.
            var selected = RefreshSelection(world);
            if (selected != null && !Busy)
            {
                bool cancel = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
                bool confirm = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
                if (cancel) ClearTarget();
                else if (confirm && ConfirmAttack()) return;
                selected = RefreshSelection(world);
            }
            RefreshTargetLines(world, TurnFlow.IsPlayerTurn(world) && !Busy && aim == AimMode.None && Loadout.CanShoot(world, settings));
            var selectedPreview = selected != null ? CombatRules.Preview(world, settings, world.Player, world.Player.Position, selected) : default;
            hudView.ShowAttackConfirm(selected != null, selected != null && selectedPreview.CanAttack && !Busy);

            if (!cell.HasValue)
            {
                SetHover(null);
                areaView.Hide();
                pushAreaView.Hide();
                if (selected != null) hudView.ShowAttackPreview(selected, selectedPreview, false);
                else hudView.ClearPreview();
                return;
            }

            var target = world.UnitAt(cell.Value);
            bool clicked = leftClick.Clicked && !IsPointerOverUi();
            if (aim == AimMode.Push)
            {
                Push.Aim(world.Player.Position, cell.Value, out var direction, out int side);
                var push = Push.Preview(world, settings, world.Player, direction, side);
                pushAreaView.Show(push.Cells);
                hudView.ShowPushPreview(push);
                SetHover(cell);
                if (clicked) RequestPush(cell.Value);
                return;
            }
            if (aim == AimMode.Grenade)
            {
                var area = AreaAttack.Preview(world, settings, world.Player, cell.Value);
                areaView.Show(area.Cells);
                hudView.ShowAreaPreview(area);
                SetHover(cell);
                if (clicked) RequestAreaAttack(cell.Value);
                return;
            }
            if (aim == AimMode.None && world.Map.InBounds(cell.Value) && world.Map.Get(cell.Value) == TileType.SupplyCrate && IsKnown(cell.Value))
            {
                bool canOpen = !world.PlayerActed && Loadout.CellDistance(world.Player.Position, cell.Value) == 1;
                hudView.ShowCratePreview(canOpen);
                SetHover(cell);
                if (clicked && canOpen) RequestOpenCrate(cell.Value);
                return;
            }
            if (target != null && !target.IsPlayer && world.Combatants.Contains(target.Id))
            {
                var preview = CombatRules.Preview(world, settings, world.Player, world.Player.Position, target);
                // 기관단총은 첫 사격 뒤에도 한 번 더 쏠 수 있다 — 판정은 규칙(Loadout.CanShoot) 한 곳.
                hudView.ShowAttackPreview(target, preview, !Loadout.CanShoot(world, settings));
                SetHover(cell);
                // 클릭 = 대상 고르기 (바로 쏘지 않는다). 확인은 버튼 · Enter · 사격 카드.
                if (clicked) SelectTarget(target.Id);
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
            if (selected != null) hudView.ShowAttackPreview(selected, selectedPreview, false);
            else hudView.ClearPreview();
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
            // 계단을 지나 층이 바뀌면 남은 걸음은 이전 층 것이라 버린다.
            if (pendingSteps.Count > 0 && pendingFloor != simulation.World.FloorIndex)
            {
                pendingSteps.Clear();
                pendingCrate = null;
            }
            if (Busy) return;
            if (pendingSteps.Count == 0)
            {
                // 상자 옆에 다 왔으면 연다.
                if (!pendingCrate.HasValue) return;
                var crate = pendingCrate.Value;
                pendingCrate = null;
                if (simulation.World.Mode == WorldMode.Exploration) simulation.Execute(new OpenSupplyCrateCommand(crate, settings));
                return;
            }

            var command = new MoveCommand(simulation.World.PlayerId, pendingSteps.Dequeue(), moveCostTicks);
            if (!simulation.Execute(command))
            {
                pendingSteps.Clear();
                pendingCrate = null;
            }
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
