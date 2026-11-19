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
        /// <summary>음파 공격: 적을 클릭해 쓴다 (3-12).</summary>
        Sonic = 3,
        /// <summary>빙결 사격: 음파처럼 적을 클릭해 쓴다 (3-16z).</summary>
        Frost = 4,
        /// <summary>서리 수호: 자신에게 쓰는 스킬 — 카드를 누르면 범위 · 미리보기, 확인으로 쓴다 (3-17b).</summary>
        FrostGuard = 5,
        /// <summary>서리 결전: 수류탄처럼 빈 칸을 골라 눈꽃 범위를 옮긴다 (3-17c).</summary>
        FrostDecisive = 6,
    }

    /// <summary>
    /// 마우스 입력을 커맨드로 바꾼다. 칸의 이동 · 사격 가능 여부와 숫자는 모두 규칙이 정한다.
    /// 탐험: 클릭한 본 칸까지 경로를 만들어 한 걸음씩 MoveCommand.
    /// 전투(내 차례): 남은 이동 범위 표시(이동만 했으면 남은 칸만큼 또 걸을 수 있다 — 3-13) · 범위 안 칸 클릭 = 이동 예약(홀로그램, 바로 걷지 않음 — 3-10), 적 클릭 = 사격 대상 고르기(노란 선 · 미리보기 · 확인 버튼).
    /// 예약이 있으면 선 · 미리보기는 예약 칸에서 계산한다. 확인(버튼 · Space · 사격 카드) = 예약 칸으로 CombatMoveCommand 뒤 AttackCommand를 한 번에,
    /// 대상 없이 Space 또는 예약 칸을 다시 클릭 = 이동만. 오른쪽 클릭 · Esc = 대상 → 예약 순으로 취소. 마우스를 올리면 미리보기.
    /// 조준 모드 (2026-11-14 사용자 "스킬도 기본 사격처럼"): 이동 범위는 계속 보이고, 클릭은 바로 쓰지 않고 대상만 고른다 — 확인(버튼 · Space · 그 스킬 카드)에 쓴다.
    /// 수류탄 · 밀치기(범위 스킬) = 클릭한 칸으로 범위가 옮겨짐 → AreaAttackCommand · PushCommand. 음파 · 빙결 사격(대상 스킬) = 사격과 같이 적 클릭 = 대상, 파란 칸 = 이동 예약 → SonicAttackCommand · FrostAttackCommand.
    /// 오른쪽 클릭 · Esc = 스킬 대상 → 조준 → 예약 순 취소.
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

        /// <summary>고른 사격 대상 (없으면 -1). 적을 클릭하면 고르기만 하고, 확인(버튼 · Space · 사격 카드)을 눌러야 쏜다.</summary>
        private int selectedTargetId = -1;

        /// <summary>전투 이동 예약 칸 (홀로그램 자리). 화면의 입력 상태일 뿐 규칙 상태가 아니다 — 확인할 때 커맨드로 바뀐다.</summary>
        private GridPos? plannedCell;

        private readonly Queue<GridPos> pendingSteps = new Queue<GridPos>();
        private int pendingFloor;
        /// <summary>걸음이 끝나면 열 보급 상자 (탐험 중 먼 상자를 클릭했을 때).</summary>
        private GridPos? pendingCrate;
        /// <summary>걸음이 끝나면 작업 화면을 연다 (탐험 중 먼 작업대를 클릭했을 때, 3-4c).</summary>
        private bool pendingBench;

        /// <summary>작업대 바로 옆에 섰다 — GameRoot가 작업 화면(WorkshopView)을 연다.</summary>
        public event System.Action WorkbenchReached;

        /// <summary>걸음이 끝나면 교환 창을 연다 (탐험 중 먼 교환대를 클릭했을 때, 3-14f).</summary>
        private TileType? pendingCounter;

        /// <summary>교환대 바로 옆에 섰다 — GameRoot가 교환 창(ExchangeView)을 연다.</summary>
        public event System.Action CounterReached;

        /// <summary>세트 진열실 진열장 바로 옆에 섰다 — GameRoot가 진열 창(GalleryView)을 연다 (3-14g).</summary>
        public event System.Action GalleryReached;

        /// <summary>제단 바로 옆에 섰다 — GameRoot가 제단 창(AltarView)을 연다 (3-14h).</summary>
        public event System.Action AltarReached;

        /// <summary>상인 계산대 바로 옆에 섰다 — GameRoot가 팔기 창(ShopView)을 연다 (3-15).</summary>
        public event System.Action ShopReached;

        /// <summary>걸음이 끝나면 문 창을 연다 (탐험 중 먼 잠긴 문을 클릭했을 때, 3-4d).</summary>
        private GridPos? pendingDoor;

        /// <summary>잠긴 문 바로 옆에 섰다 — GameRoot가 문 창(DoorView)을 연다.</summary>
        public event System.Action<GridPos> DoorReached;
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

        /// <summary>
        /// 고른 대상을 쏜다 (확인 버튼 · Space · 사격 카드). 이동 예약이 있으면 먼저 예약 칸으로 걷고 이어서 쏜다 (한 번에).
        /// 걷는 동안 감시 사격 등으로 상황이 바뀌어 더 쏠 수 없으면 이동만 하고 끝난다. 고른 대상이 없거나 아무것도 못 하면 false — 고른 상태는 남는다.
        /// </summary>
        public bool ConfirmAttack()
        {
            if (simulation == null || Busy || selectedTargetId < 0) return false;
            int id = selectedTargetId;
            var world = simulation.World;
            var target = world.FindUnit(id);
            if (target == null || !CombatRules.Preview(world, settings, world.Player, Origin(world), target).CanAttack) return false;

            if (plannedCell.HasValue)
            {
                if (!simulation.Execute(new CombatMoveCommand(plannedCell.Value, settings))) return false;
                ClearPlan();
                // 걷는 동안 반응 사격 · 전투 종료 등이 있었으면 사격은 건너뛴다 (판정은 AttackCommand가 다시 한다).
                bool stillValid = world.FindUnit(id) != null && TurnFlow.IsPlayerTurn(world) && world.Combatants.Contains(id) && Loadout.CanShoot(world, settings);
                if (stillValid) simulation.Execute(new AttackCommand(id, settings));
                ClearTarget();
                return true;
            }

            if (!simulation.Execute(new AttackCommand(id, settings))) return false;
            ClearTarget();
            return true;
        }

        /// <summary>지금 예약된 이동 칸 (없으면 null).</summary>
        public GridPos? PlannedCell => plannedCell;

        /// <summary>선 · 미리보기를 계산하는 자리: 이동 예약이 있으면 그 칸, 없으면 플레이어 칸.</summary>
        private GridPos Origin(World world) => plannedCell ?? world.Player.Position;

        /// <summary>전투 이동 칸을 예약한다 (홀로그램). 지금 갈 수 있는 칸이 아니면 false.</summary>
        public bool PlanMove(GridPos cell)
        {
            // 범위 스킬 조준 중의 클릭은 범위 옮기기라 예약하지 않는다. 음파(대상 스킬)는 사격처럼 예약할 수 있다.
            if (simulation == null || Busy || (aim != AimMode.None && !AllowsPlanning)) return false;
            var world = simulation.World;
            if (world.Mode != WorldMode.Combat || !Loadout.CanCombatMove(world, settings)) return false;
            RefreshRange(true);
            if (reachable == null || !reachable.ContainsKey(cell) || cell == world.Player.Position) return false;
            plannedCell = cell;
            // 고른 대상이 새 자리에서 쏠 수 없으면 고르기를 푼다 (선 · 확인 버튼이 새 자리 기준으로 다시 그려진다).
            var target = selectedTargetId >= 0 ? world.FindUnit(selectedTargetId) : null;
            if (target != null && !CombatRules.Preview(world, settings, world.Player, cell, target).CanAttack) ClearTarget();
            return true;
        }

        /// <summary>예약한 칸으로 이동만 한다 (예약 칸 다시 클릭 · 대상 없이 Space). 예약이 없거나 못 가면 false.</summary>
        public bool ConfirmMove()
        {
            if (simulation == null || Busy || !plannedCell.HasValue) return false;
            var cell = plannedCell.Value;
            ClearPlan();
            ClearTarget();
            return simulation.Execute(new CombatMoveCommand(cell, settings));
        }

        public void ClearPlan()
        {
            plannedCell = null;
            if (playerView != null) playerView.HideGhost();
        }

        public void ClearTarget()
        {
            selectedTargetId = -1;
            if (hudView != null) hudView.ShowAttackConfirm(false, false);
        }

        /// <summary>
        /// 지금 쏠 수 있는 적(사거리 · 사선 안, CombatRules.Preview가 쏠 수 있다고 한 적)마다 플레이어에서 선을 긋는다
        /// (결정 2026-11-11, 사용자): 엄폐를 받는 적 = 흰 선, 엄폐가 없는 적 = 노란 선, 고른 적 = 밝은 노란 선.
        /// 쏠 수 없는 때(적 차례 · 재생 중 · 스킬 조준 · 이미 쐈음)에는 모두 숨긴다. 음파 조준 중에는 RefreshSonicLines가 다시 긋는다.
        /// </summary>
        private void RefreshTargetLines(World world, bool show)
        {
            lineKinds.Clear();
            int used = 0;
            if (show)
            {
                var player = world.Player;
                var origin = Origin(world);
                foreach (int id in world.Combatants)
                {
                    var enemy = world.FindUnit(id);
                    if (enemy == null) continue;
                    var preview = CombatRules.Preview(world, settings, player, origin, enemy);
                    if (!preview.CanAttack) continue;
                    string kind = id == selectedTargetId ? "selected" : preview.Cover != CoverLevel.None ? "cover" : "open";
                    DrawTargetLine(used++, origin, enemy.Position, id, kind);
                }
            }
            for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
        }

        /// <summary>
        /// 음파처럼 적 하나를 고르는 스킬의 조준 중 (2026-11-17 사용자 "사격과 같은 노란 줄"): 그 스킬을 쓸 수 있는 적마다
        /// 사격과 같은 선을 긋는다. 음파는 엄폐를 무시하므로 흰 선 없이 노란 선, 고른 대상은 밝은 노란 선.
        /// 출발점과 판정은 스킬 미리보기와 같은 복제 세계(예약 칸으로 걸어 본 결과)에서.
        /// </summary>
        private void RefreshSonicLines(World skillWorld)
        {
            lineKinds.Clear();
            int used = 0;
            if (skillWorld != null && !Busy)
            {
                var player = skillWorld.Player;
                foreach (int id in skillWorld.Combatants)
                {
                    var enemy = skillWorld.FindUnit(id);
                    if (enemy == null) continue;
                    if (!TargetSkillUsable(skillWorld, enemy)) continue;
                    // 빙결 사격은 엄폐를 받는다 — 사격처럼 엄폐 뒤 적은 흰 선 (3-16z).
                    bool covered = aim == AimMode.Frost && FrostAttack.Preview(skillWorld, settings, player, enemy).Cover != CoverLevel.None;
                    DrawTargetLine(used++, player.Position, enemy.Position, id, id == skillTargetId ? "selected" : covered ? "cover" : "open");
                }
            }
            for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
        }

        private void DrawTargetLine(int index, GridPos origin, GridPos enemyCell, int id, string kind)
        {
            var line = LineAt(index);
            line.sharedMaterial = kind == "selected" ? selectedLineMaterial : kind == "cover" ? coverLineMaterial : openLineMaterial;
            line.SetPosition(0, GridSpace.ToWorld(origin) + Vector3.up * targetLineHeight);
            line.SetPosition(1, GridSpace.ToWorld(enemyCell) + Vector3.up * targetLineHeight);
            line.enabled = true;
            lineKinds.Add(id + ":" + kind);
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

        /// <summary>음파 공격 조준 모드를 켜고 끈다 (음파 버튼).</summary>
        public void ToggleSonicAim() => ToggleAim(AimMode.Sonic);

        /// <summary>빙결 사격 조준 모드를 켜고 끈다 (빙결 사격 카드, 3-16z).</summary>
        public void ToggleFrostAim() => ToggleAim(AimMode.Frost);

        /// <summary>그 적에게 음파 공격을 쓴다 (예약이 있으면 예약 칸으로 걸은 뒤). 못 쓰면 false (조준 모드는 그대로).</summary>
        public bool RequestSonic(int targetId) => RequestTargetSkill(AimMode.Sonic, targetId);

        /// <summary>그 적에게 빙결 사격을 쓴다 (예약이 있으면 예약 칸으로 걸은 뒤). 못 쓰면 false.</summary>
        public bool RequestFrost(int targetId) => RequestTargetSkill(AimMode.Frost, targetId);

        private bool RequestTargetSkill(AimMode mode, int targetId)
        {
            if (simulation == null || !CombatFlow.CanUseSkill(simulation.World) || Busy) return false;
            var preview = PreviewWorld();
            if (preview == null || !TargetSkillUsable(preview, preview.FindUnit(targetId), mode)) return false;
            ICommand command = mode == AimMode.Frost ? new FrostAttackCommand(targetId, settings) : (ICommand)new SonicAttackCommand(targetId, settings);
            if (!RunAfterPlannedMove(command)) return false;
            SetAiming(AimMode.None);
            return true;
        }

        /// <summary>적 하나를 고르는 스킬(음파 · 빙결 사격)을 조준 중인가.</summary>
        private bool IsTargetAim => aim == AimMode.Sonic || aim == AimMode.Frost;

        /// <summary>조준 중에도 파란 칸 클릭으로 이동 예약을 할 수 있는가 (대상 스킬 · 자신에게 쓰는 스킬).</summary>
        private bool AllowsPlanning => IsTargetAim || aim == AimMode.FrostGuard;

        /// <summary>서리 수호 조준 모드를 켜고 끈다 (서리 수호 카드, 3-17b).</summary>
        public void ToggleFrostGuardAim() => ToggleAim(AimMode.FrostGuard);

        /// <summary>서리 결전 조준 모드를 켜고 끈다 (서리 결전 카드, 3-17c).</summary>
        public void ToggleFrostDecisiveAim() => ToggleAim(AimMode.FrostDecisive);

        /// <summary>그 칸에 서리 결전을 쓴다 (예약이 있으면 예약 칸으로 걸은 뒤). 못 쓰면 false.</summary>
        public bool RequestFrostDecisive(GridPos center)
        {
            if (simulation == null || !CombatFlow.CanUseSkill(simulation.World) || Busy) return false;
            var preview = PreviewWorld();
            if (preview == null || !FrostDecisive.Preview(preview, settings, preview.Player, center).CanUse) return false;
            if (!RunAfterPlannedMove(new FrostDecisiveCommand(center, settings))) return false;
            SetAiming(AimMode.None);
            return true;
        }

        /// <summary>서리 수호를 쓴다 (예약이 있으면 예약 칸으로 걸은 뒤). 못 쓰면 false.</summary>
        public bool RequestFrostGuard()
        {
            if (simulation == null || !CombatFlow.CanUseSkill(simulation.World) || Busy) return false;
            var preview = PreviewWorld();
            if (preview == null || !FrostGuard.Preview(preview, settings, preview.Player).CanUse) return false;
            if (!RunAfterPlannedMove(new FrostGuardCommand(settings))) return false;
            SetAiming(AimMode.None);
            return true;
        }

        /// <summary>그 적에게 대상 스킬을 쓸 수 있는가 (판정은 규칙 Preview 한 곳). mode를 주지 않으면 지금 조준 중인 스킬.</summary>
        private bool TargetSkillUsable(World skillWorld, Unit target, AimMode? mode = null)
        {
            if (skillWorld == null || target == null) return false;
            return (mode ?? aim) == AimMode.Frost
                ? FrostAttack.Preview(skillWorld, settings, skillWorld.Player, target).CanUse
                : SonicAttack.Preview(skillWorld, settings, skillWorld.Player, target).CanUse;
        }

        /// <summary>지금 조준 중인 대상 스킬의 미리보기를 HUD에 띄운다. 쓸 수 있으면 true.</summary>
        private bool ShowTargetPreview(World skillWorld, Unit target)
        {
            if (aim == AimMode.Frost)
            {
                var frost = FrostAttack.Preview(skillWorld, settings, skillWorld.Player, target);
                hudView.ShowFrostPreview(frost);
                return frost.CanUse;
            }
            var sonic = SonicAttack.Preview(skillWorld, settings, skillWorld.Player, target);
            hudView.ShowSonicPreview(sonic);
            return sonic.CanUse;
        }

        private void ShowTargetHint()
        {
            if (aim == AimMode.Frost) hudView.ShowFrostHint();
            else hudView.ShowSonicHint();
        }

        private void ToggleAim(AimMode mode)
        {
            // 대상을 골라 둔 스킬 카드를 다시 누르면 확인 (사격 카드와 같다).
            if (aim == mode && HasSkillTarget)
            {
                ConfirmSkill();
                return;
            }
            var world = simulation?.World;
            // 그 캐릭터의 스킬 칸에 있는 스킬만 조준할 수 있다 (판정은 규칙 CombatSettings.HasSkill).
            // 탐험 중에도 쓸 수 있다 (2026-11-14 사용자 — 판정은 규칙 CombatFlow.CanUseSkill).
            bool canAim = world != null && CombatFlow.CanUseSkill(world) && settings.HasSkill(world.PlayerCharacter, SkillOf(mode));
            SetAiming(aim != mode && canAim ? mode : AimMode.None);
        }

        private static SkillKind SkillOf(AimMode mode)
        {
            switch (mode)
            {
                case AimMode.Grenade: return SkillKind.Grenade;
                case AimMode.Push: return SkillKind.Push;
                case AimMode.Sonic: return SkillKind.Sonic;
                case AimMode.Frost: return SkillKind.Frost;
                case AimMode.FrostGuard: return SkillKind.FrostGuard;
                case AimMode.FrostDecisive: return SkillKind.FrostDecisive;
                default: return SkillKind.None;
            }
        }

        /// <summary>마우스 칸 쪽으로 밀치기를 쓴다 (방향 · 옆 줄은 Push.Aim). 못 쓰면 false.</summary>
        public bool RequestPush(GridPos pointer)
        {
            if (simulation == null || !CombatFlow.CanUseSkill(simulation.World) || Busy) return false;
            // 방향 · 옆 줄은 쓰는 자리(예약이 있으면 예약 칸) 기준.
            Push.Aim(Origin(simulation.World), pointer, out var direction, out int side);
            var preview = PreviewWorld();
            if (preview == null || !Push.Preview(preview, settings, preview.Player, direction, side).CanPush) return false;
            if (!RunAfterPlannedMove(new PushCommand(direction, side, settings))) return false;
            SetAiming(AimMode.None);
            return true;
        }

        /// <summary>수류탄을 그 칸에 던진다 (예약이 있으면 예약 칸으로 걸은 뒤). 못 던지면 false (조준 모드는 그대로).</summary>
        public bool RequestAreaAttack(GridPos center)
        {
            if (simulation == null || !CombatFlow.CanUseSkill(simulation.World) || Busy) return false;
            var preview = PreviewWorld();
            if (preview == null || !AreaAttack.Preview(preview, settings, preview.Player, center).CanThrow) return false;
            if (!RunAfterPlannedMove(new AreaAttackCommand(center, settings))) return false;
            SetAiming(AimMode.None);
            return true;
        }

        /// <summary>감시 버튼: 사격 대신 감시를 걸고 차례를 끝낸다 (예약이 있으면 예약 칸으로 걸은 뒤). 못 하면 false.</summary>
        public bool RequestOverwatch()
        {
            if (simulation == null || !TurnFlow.IsPlayerTurn(simulation.World) || Busy) return false;
            SetAiming(AimMode.None);
            return RunAfterPlannedMove(new OverwatchCommand(settings));
        }

        /// <summary>
        /// 이동 예약이 있으면 먼저 예약 칸으로 걷고(CombatMoveCommand) 이어서 그 행동을 낸다. 예약이 없으면 행동만.
        /// 걷기는 됐는데 그 사이 상황이 바뀌어(감시 사격 · 전투 끝) 행동이 안 되면 이동만 하고 true (걸었으므로).
        /// </summary>
        private bool RunAfterPlannedMove(ICommand action)
        {
            if (!plannedCell.HasValue) return simulation.Execute(action);
            var cell = plannedCell.Value;
            if (!simulation.Execute(new CombatMoveCommand(cell, settings))) return false;
            ClearPlan();
            ClearTarget();
            if (TurnFlow.IsPlayerTurn(simulation.World)) simulation.Execute(action);
            return true;
        }

        /// <summary>
        /// 스킬 미리보기를 계산할 세계: 예약이 없으면 지금 세계, 있으면 지금 세계를 복제해 예약 칸으로 실제로 걸어 본 결과
        /// (걸음마다 감시 사격까지 같은 난수로 — 확인 때와 똑같다, 핵심 규칙 6). 걸을 수 없으면 null. 상태가 바뀔 때만 다시 만든다.
        /// </summary>
        private World PreviewWorld()
        {
            var world = simulation.World;
            if (!plannedCell.HasValue) return world;
            if (previewWorldCell == plannedCell && previewWorld != null) return previewWorldValid ? previewWorld : null;

            previewWorldCell = plannedCell;
            previewWorld = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            previewWorldValid = new Simulation(previewWorld, settings).Execute(new CombatMoveCommand(plannedCell.Value, settings))
                && TurnFlow.IsPlayerTurn(previewWorld);
            return previewWorldValid ? previewWorld : null;
        }

        private World previewWorld;
        private GridPos? previewWorldCell;
        private bool previewWorldValid;

        /// <summary>규칙 상태가 바뀌면(커맨드 실행) 복제 미리보기를 버린다.</summary>
        private void DropPreviewWorld(IReadOnlyList<GameEvent> events)
        {
            previewWorld = null;
            previewWorldCell = null;
        }

        /// <summary>조준 중인 스킬을 끈다 (스킬 바 "사격" · 턴 종료).</summary>
        public void CancelAim() => SetAiming(AimMode.None);

        /// <summary>
        /// 스킬 대상 (2026-11-14 사용자 "스킬도 기본 사격처럼"): 조준 중 클릭은 바로 쓰지 않고 대상만 고른다 — 확인(버튼 · Space · 그 스킬 카드)에 쓴다.
        /// 범위 스킬(수류탄 · 넉백) = 누른 칸으로 범위가 옮겨진다 (skillCell). 대상 스킬(음파) = 사격처럼 적 클릭 = 대상, 파란 칸 클릭 = 이동 예약 (skillTargetId).
        /// </summary>
        private GridPos? skillCell;
        private int skillTargetId = -1;

        public bool HasSkillTarget => aim == AimMode.FrostGuard || (IsTargetAim ? skillTargetId >= 0 : aim != AimMode.None && skillCell.HasValue);
        public GridPos? SkillCell => skillCell;
        public int SkillTargetId => skillTargetId;

        private void ClearSkillTarget()
        {
            skillCell = null;
            skillTargetId = -1;
        }

        /// <summary>고른 스킬 대상에 스킬을 쓴다 (예약이 있으면 예약 칸으로 걸은 뒤). 대상이 없거나 못 쓰면 false.</summary>
        public bool ConfirmSkill()
        {
            if (aim == AimMode.Grenade && skillCell.HasValue) return RequestAreaAttack(skillCell.Value);
            if (aim == AimMode.Push && skillCell.HasValue) return RequestPush(skillCell.Value);
            if (aim == AimMode.FrostDecisive && skillCell.HasValue) return RequestFrostDecisive(skillCell.Value);
            if (IsTargetAim && skillTargetId >= 0) return RequestTargetSkill(aim, skillTargetId);
            if (aim == AimMode.FrostGuard) return RequestFrostGuard();
            return false;
        }

        /// <summary>확인 버튼 · Space: 스킬 조준 중이면 스킬, 아니면 사격.</summary>
        public bool Confirm() => aim != AimMode.None ? ConfirmSkill() : ConfirmAttack();

        /// <summary>[상태 변경 없음] 스킬 대상 고르기 (조준 중 클릭과 같다 — 디버그 · 테스트용). 고를 수 없으면 false.</summary>
        public bool SelectSkillTarget(GridPos cell)
        {
            if (simulation == null || Busy || aim == AimMode.None) return false;
            var world = simulation.World;
            var skillWorld = PreviewWorld();
            if (skillWorld == null) return false;
            if (IsTargetAim)
            {
                var under = world.UnitAt(cell);
                var target = under != null && !under.IsPlayer ? skillWorld.FindUnit(under.Id) : null;
                if (target == null || !TargetSkillUsable(skillWorld, target)) return false;
                skillTargetId = under.Id;
                return true;
            }
            if (!world.Map.InBounds(cell)) return false;
            skillCell = cell;
            return true;
        }

        /// <summary>화면이 사건을 재생하는 중(적 차례 연출)이거나 플레이어가 걷는 중이면 입력을 받지 않는다.</summary>
        private bool Busy => playerView.IsMoving || (eventPlayer != null && eventPlayer.IsPlaying);

        private void SetAiming(AimMode mode)
        {
            if (aim != mode) ClearSkillTarget();
            aim = mode;
            // 스킬을 조준하면 고른 사격 대상은 푼다. 이동 예약은 남는다 — 스킬도 예약 칸으로 걸은 뒤 쓴다.
            if (mode != AimMode.None) ClearTarget();
            if (aim != AimMode.Grenade) areaView.Hide();
            if (aim != AimMode.Push) pushAreaView.Hide();
            hudView.SetAiming(aim);
            reachableKey = null;
        }

        public void Bind(Simulation newSimulation, EventPlayer newEventPlayer, PlayerView newPlayerView, HudView newHudView, CombatSettings newSettings, int newMoveCostTicks, float newDragThresholdPixels)
        {
            dragThresholdPixels = newDragThresholdPixels;
            if (simulation != null) simulation.EventsPublished -= DropPreviewWorld;
            simulation = newSimulation;
            simulation.EventsPublished -= DropPreviewWorld;
            simulation.EventsPublished += DropPreviewWorld;
            previewWorld = null;
            previewWorldCell = null;
            eventPlayer = newEventPlayer;
            playerView = newPlayerView;
            hudView = newHudView;
            settings = newSettings;
            moveCostTicks = newMoveCostTicks;
            pendingSteps.Clear();
            pendingCrate = null;
            pendingBench = false;
            pendingCounter = null;
            pendingDoor = null;
            reachable = null;
            reachableKey = null;
            ClearTarget();
            ClearPlan();
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
            pendingBench = false;
            pendingCounter = null;
            pendingDoor = null;
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
            if (!world.Map.IsClosedChest(crate) || !IsKnown(crate)) return false;

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
            pendingBench = false;
            pendingCounter = null;
            pendingDoor = null;
            return true;
        }

        /// <summary>
        /// 잠긴 문을 연다 (3-4d): 바로 옆이면 문 창을 열고, 탐험 중 멀면 가장 가까운 옆 칸까지 걸어간 뒤 연다. 전투 중이거나 못 가면 false.
        /// </summary>
        public bool RequestOpenDoor(GridPos door)
        {
            if (simulation == null || Busy) return false;
            var world = simulation.World;
            if (world.Mode != WorldMode.Exploration || !world.Map.IsLockedDoor(door) || !IsKnown(door)) return false;
            if (Loadout.CellDistance(world.Player.Position, door) == 1)
            {
                DoorReached?.Invoke(door);
                return true;
            }
            var best = PathNextTo(world, door);
            if (best == null) return false;
            pendingSteps.Clear();
            pendingCrate = null;
            pendingBench = false;
            pendingCounter = null;
            foreach (var step in best) pendingSteps.Enqueue(step);
            pendingFloor = world.FloorIndex;
            pendingDoor = door;
            return true;
        }

        /// <summary>target 옆(8방향) 설 수 있는 칸 중 가장 짧은 경로 (이웃 순서 고정). 없으면 null.</summary>
        private List<GridPos> PathNextTo(World world, GridPos target)
        {
            var player = world.Player;
            List<GridPos> best = null;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var side = new GridPos(target.X + dx, target.Y + dy);
                    if ((dx == 0 && dy == 0) || !world.Map.IsWalkable(side) || !IsKnown(side)) continue;
                    var path = GridNavigation.FindPath(world.Map, player.Position, side,
                        cell => cell != player.Position && (world.IsOccupied(cell) || !IsKnown(cell)));
                    if (path.Count > 0 && (best == null || path.Count < best.Count)) best = path;
                }
            }
            return best;
        }

        /// <summary>
        /// 교환대를 쓴다 (3-14f): 바로 옆이면 교환 창을 열고, 탐험 중 멀면 가장 가까운 옆 칸까지 걸어간 뒤 연다. 전투 중이거나 못 가면 false.
        /// </summary>
        public bool RequestUseCounter(GridPos counter)
        {
            if (simulation == null || Busy) return false;
            var world = simulation.World;
            if (world.Mode != WorldMode.Exploration || !world.Map.InBounds(counter) || !IsKnown(counter)) return false;
            var tile = world.Map.Get(counter);
            if (tile != TileType.ExchangeCounter && tile != TileType.SetGalleryCase && tile != TileType.Altar && tile != TileType.ShopCounter) return false;
            if (Loadout.CellDistance(world.Player.Position, counter) == 1)
            {
                ReachStation(tile);
                return true;
            }
            var best = PathNextTo(world, counter);
            if (best == null) return false;
            pendingSteps.Clear();
            pendingCrate = null;
            pendingBench = false;
            pendingDoor = null;
            foreach (var step in best) pendingSteps.Enqueue(step);
            pendingFloor = world.FloorIndex;
            pendingCounter = tile;
            return true;
        }

        /// <summary>교환대 · 진열장 옆에 섰다: 그 창을 여는 사건을 낸다.</summary>
        private void ReachStation(TileType tile)
        {
            var world = simulation.World;
            if (tile == TileType.ExchangeCounter && Exchange.AtCounter(world)) CounterReached?.Invoke();
            if (tile == TileType.SetGalleryCase && SetGallery.AtCase(world)) GalleryReached?.Invoke();
            if (tile == TileType.Altar && Altar.AtAltar(world)) AltarReached?.Invoke();
            if (tile == TileType.ShopCounter && Shop.AtCounter(world)) ShopReached?.Invoke();
        }

        /// <summary>
        /// 작업대를 쓴다 (3-4c): 바로 옆이면 작업 화면을 열고, 탐험 중 멀면 가장 가까운 옆 칸까지 걸어간 뒤 연다. 전투 중이거나 못 가면 false.
        /// </summary>
        public bool RequestUseWorkbench(GridPos bench)
        {
            if (simulation == null || Busy) return false;
            var world = simulation.World;
            if (world.Mode != WorldMode.Exploration || !world.Map.InBounds(bench) || world.Map.Get(bench) != TileType.Workbench || !IsKnown(bench)) return false;

            var player = world.Player;
            if (Loadout.CellDistance(player.Position, bench) == 1)
            {
                WorkbenchReached?.Invoke();
                return true;
            }

            List<GridPos> best = null;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var side = new GridPos(bench.X + dx, bench.Y + dy);
                    if ((dx == 0 && dy == 0) || !world.Map.IsWalkable(side) || !IsKnown(side)) continue;
                    var path = GridNavigation.FindPath(world.Map, player.Position, side,
                        cell => cell != player.Position && (world.IsOccupied(cell) || !IsKnown(cell)));
                    if (path.Count > 0 && (best == null || path.Count < best.Count)) best = path;
                }
            }
            if (best == null) return false;

            pendingSteps.Clear();
            pendingCrate = null;
            foreach (var step in best) pendingSteps.Enqueue(step);
            pendingFloor = world.FloorIndex;
            pendingBench = true;
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

        /// <summary>작업 화면 같은 창이 떠 있는 동안 지도 클릭 · 단축키를 받지 않는다 (GameRoot가 켠다).</summary>
        public bool Blocked { get; set; }

        private void Update()
        {
            if (simulation == null || Blocked) return;
            var pointerMouse = Mouse.current;
            if (pointerMouse != null) leftClick.Update(pointerMouse.leftButton, pointerMouse.position.ReadValue(), dragThresholdPixels);

            var mode = simulation.World.Mode;
            // 전투가 시작되거나 무기 비교 창이 뜨면 걷던 길을 멈춘다.
            if (mode != WorldMode.Exploration || hudView.IsWeaponOfferOpen)
            {
                pendingSteps.Clear();
                pendingCrate = null;
                pendingBench = false;
                pendingCounter = null;
                pendingDoor = null;
            }
            if (aim != AimMode.None && !CombatFlow.CanUseSkill(simulation.World)) SetAiming(AimMode.None);

            if (mode == WorldMode.Combat) HandleCombat();
            else
            {
                if (selectedTargetId >= 0) ClearTarget();
                if (plannedCell.HasValue) ClearPlan();
                RefreshTargetLines(simulation.World, false);
                rangeView.Hide();
                reachableKey = null;
                if (mode == WorldMode.Exploration && aim != AimMode.None) HandleExplorationAim();
                else if (mode == WorldMode.Exploration) HandleExploration();
                else SetHover(null);
            }
            AdvanceSteps();
        }

        /// <summary>
        /// 탐험 중 스킬 조준 (2026-11-14 사용자 "어디서나 탐험 중 스킬"): 전투 때와 같은 조준 · 대상 고르기 · 확인(Space · 공격 버튼 · 같은 카드), 이동 예약은 없다.
        /// 쓰면 맞은 적 · 보이는 적과 전투가 시작되고 그 스킬이 첫 행동이 된다 (규칙 CombatFlow.AfterSkill). 조준하는 동안은 걷지 않는다.
        /// </summary>
        private void HandleExplorationAim()
        {
            var world = simulation.World;
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            pendingSteps.Clear();
            if (!Busy)
            {
                bool cancel = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
                bool confirm = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
                if (cancel)
                {
                    if (HasSkillTarget) ClearSkillTarget();
                    else SetAiming(AimMode.None);
                    if (aim == AimMode.None)
                    {
                        hudView.ShowAttackConfirm(false, false);
                        hudView.ClearPreview();
                        return;
                    }
                }
                else if (confirm && HasSkillTarget && ConfirmSkill()) return;
            }
            var cell = mouse != null && !Busy ? PointerCell(mouse.position.ReadValue()) : null;
            HandleSkillAim(world, cell, cell.HasValue && leftClick.Clicked && !IsPointerOverUi(), false);
        }

        private void HandleExploration()
        {
            var mouse = Mouse.current;
            var cell = mouse != null ? PointerCell(mouse.position.ReadValue()) : null;
            var map = simulation.World.Map;
            bool isWalkable = cell.HasValue && map.IsWalkable(cell.Value) && IsKnown(cell.Value);
            bool isCrate = cell.HasValue && map.IsClosedChest(cell.Value) && IsKnown(cell.Value);
            bool isBench = cell.HasValue && map.InBounds(cell.Value) && map.Get(cell.Value) == TileType.Workbench && IsKnown(cell.Value);
            bool isDoor = cell.HasValue && map.IsLockedDoor(cell.Value) && IsKnown(cell.Value);
            bool isCounter = cell.HasValue && map.InBounds(cell.Value) && IsKnown(cell.Value)
                && (map.Get(cell.Value) == TileType.ExchangeCounter || map.Get(cell.Value) == TileType.SetGalleryCase || map.Get(cell.Value) == TileType.Altar
                    || map.Get(cell.Value) == TileType.ShopCounter);
            SetHover(isWalkable || isCrate || isBench || isDoor || isCounter ? cell : null);
            // 상점 진열 물건 위에 마우스를 올리면 이름 · 값 (3-15).
            var shopItem = isWalkable ? simulation.World.ItemAt(cell.Value) : null;
            if (shopItem != null && shopItem.Price > 0) hudView.ShowShopItemPreview(shopItem);
            else hudView.ClearShopItemPreview();

            bool clicked = mouse != null && leftClick.Clicked && !IsPointerOverUi();
            if (isWalkable && clicked) RequestMove(cell.Value);
            if (isCrate && clicked) RequestOpenCrate(cell.Value);
            if (isBench && clicked) RequestUseWorkbench(cell.Value);
            if (isDoor && clicked) RequestOpenDoor(cell.Value);
            if (isCounter && clicked) RequestUseCounter(cell.Value);
        }

        private void HandleCombat()
        {
            var world = simulation.World;
            // 권총은 쏜 뒤에도 이동할 수 있다 — 판정은 규칙(Loadout.CanCombatMove) 한 곳.
            // 이동 범위는 스킬 조준 중에도 보인다 (2026-11-14 사용자). 예약 클릭은 조준 없음 · 음파 조준 때만.
            bool canMove = Loadout.CanCombatMove(world, settings) && !Busy && (aim == AimMode.None || AllowsPlanning);
            RefreshRange(Loadout.CanCombatMove(world, settings) && !Busy);

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            var cell = mouse != null && TurnFlow.IsPlayerTurn(world) && !Busy ? PointerCell(mouse.position.ReadValue()) : null;
            // 오른쪽 클릭 · Esc 한 번은 한 단계만 취소한다: 스킬 대상 → 스킬 조준 → 고른 사격 대상 → 이동 예약.
            bool cancelPressed = (mouse != null && mouse.rightButton.wasPressedThisFrame) || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
            // 확인 키 = Space (2026-11-14 사용자 — 전에는 Enter, 턴 종료는 Z로 옮김).
            bool confirmPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            bool aimCancelled = false;
            if (aim != AimMode.None && !Busy)
            {
                if (cancelPressed)
                {
                    if (HasSkillTarget) ClearSkillTarget();
                    else SetAiming(AimMode.None);
                    aimCancelled = true;
                }
                else if (confirmPressed && HasSkillTarget && ConfirmSkill()) return;
            }

            // 이동 예약은 지금 이동할 수 있고 그 칸이 아직 범위 안일 때만 남는다 (적 차례 · 이미 이동함 등이면 푼다).
            // 스킬 조준 중에는 범위 칸을 숨기지만 예약은 남는다 (스킬도 예약 칸에서 쓴다).
            if (plannedCell.HasValue && !Busy && (!Loadout.CanCombatMove(world, settings)
                || (reachable != null && !reachable.ContainsKey(plannedCell.Value)))) ClearPlan();

            // 고른 사격 대상: 노란 선 · 확인 버튼. 오른쪽 클릭 · Esc = 대상 → 이동 예약 순으로 취소, Space = 확인 (대상이 없으면 예약 칸으로 이동만).
            var selected = RefreshSelection(world);
            if (!Busy && !aimCancelled && aim == AimMode.None && (selected != null || plannedCell.HasValue))
            {
                bool cancel = cancelPressed;
                bool confirm = confirmPressed;
                if (cancel)
                {
                    if (selected != null) ClearTarget();
                    else ClearPlan();
                }
                else if (confirm && selected != null && ConfirmAttack()) return;
                else if (confirm && selected == null && ConfirmMove()) return;
                selected = RefreshSelection(world);
            }
            var origin = Origin(world);
            RefreshTargetLines(world, TurnFlow.IsPlayerTurn(world) && !Busy && aim == AimMode.None && Loadout.CanShoot(world, settings));
            var selectedPreview = selected != null ? CombatRules.Preview(world, settings, world.Player, origin, selected) : default;
            if (aim != AimMode.None)
            {
                HandleSkillAim(world, cell, cell.HasValue && leftClick.Clicked && !IsPointerOverUi(), canMove);
                return;
            }
            hudView.ShowAttackConfirm(selected != null, selected != null && selectedPreview.CanAttack && !Busy);
            RefreshGhost(world, selected != null ? GridSpace.ToWorld(selected.Position) : (Vector3?)null);

            if (!cell.HasValue)
            {
                SetHover(null);
                areaView.Hide();
                pushAreaView.Hide();
                ShowIdlePreview(selected, selectedPreview);
                return;
            }

            var target = world.UnitAt(cell.Value);
            bool clicked = leftClick.Clicked && !IsPointerOverUi();
            if (world.Map.IsClosedChest(cell.Value) && IsKnown(cell.Value))
            {
                bool canOpen = !world.PlayerActed && Loadout.CellDistance(world.Player.Position, cell.Value) == 1;
                hudView.ShowCratePreview(canOpen);
                SetHover(cell);
                if (clicked && canOpen) RequestOpenCrate(cell.Value);
                return;
            }
            if (target != null && !target.IsPlayer && world.Combatants.Contains(target.Id))
            {
                var preview = CombatRules.Preview(world, settings, world.Player, origin, target);
                // 기관단총은 첫 사격 뒤에도 한 번 더 쏠 수 있다 — 판정은 규칙(Loadout.CanShoot) 한 곳.
                hudView.ShowAttackPreview(target, preview, !Loadout.CanShoot(world, settings));
                SetHover(cell);
                // 클릭 = 대상 고르기 (바로 쏘지 않는다). 확인은 버튼 · Space · 사격 카드.
                if (clicked) SelectTarget(target.Id);
                return;
            }

            if (canMove && reachable != null && reachable.ContainsKey(cell.Value) && cell.Value != world.Player.Position)
            {
                bool isPlanned = plannedCell.HasValue && plannedCell.Value == cell.Value;
                if (isPlanned) hudView.ShowPlannedMovePreview(WeakestCoverAgainstCombatants(cell.Value));
                else hudView.ShowMovePreview(WeakestCoverAgainstCombatants(cell.Value));
                SetHover(cell);
                // 클릭 = 이동 예약 (홀로그램). 예약 칸을 다시 클릭하면 이동만 한다.
                if (clicked)
                {
                    if (isPlanned) ConfirmMove();
                    else PlanMove(cell.Value);
                }
                return;
            }

            // 플레이어 자기 칸 클릭 = 예약 취소.
            if (clicked && cell.Value == world.Player.Position && plannedCell.HasValue) ClearPlan();
            SetHover(null);
            ShowIdlePreview(selected, selectedPreview);
        }

        /// <summary>마우스가 아무 칸에도 없을 때: 고른 대상 미리보기 → 이동 예약 안내 → 비움.</summary>
        private void ShowIdlePreview(Unit selected, AttackPreview selectedPreview)
        {
            if (selected != null) hudView.ShowAttackPreview(selected, selectedPreview, false);
            else if (plannedCell.HasValue) hudView.ShowPlannedMovePreview(WeakestCoverAgainstCombatants(plannedCell.Value));
            else hudView.ClearPreview();
        }

        /// <summary>
        /// 스킬 조준 중 입력 (2026-11-14 사용자). 미리보기는 예약이 있으면 예약 칸으로 걸어 본 복제 세계에서 계산한다 (확인 때와 같은 결과).
        /// 범위 스킬(수류탄 · 넉백): 클릭 = 그 칸으로 범위를 옮긴다 (고르기 전에는 마우스를 따라다님). 이동 범위는 보이기만 한다.
        /// 대상 스킬(음파): 사격과 같다 — 적 클릭 = 대상 고르기, 파란 칸 클릭 = 이동 예약 (예약 칸 다시 클릭 = 이동만).
        /// 쓰기는 확인(버튼 · Space · 그 스킬 카드)으로.
        /// </summary>
        private void HandleSkillAim(World world, GridPos? cell, bool clicked, bool canMove)
        {
            var skillWorld = PreviewWorld();
            if (IsTargetAim)
            {
                HandleSonicAim(world, skillWorld, cell, clicked, canMove);
                return;
            }
            if (aim == AimMode.FrostGuard)
            {
                HandleFrostGuardAim(world, cell, clicked, canMove);
                return;
            }

            if (clicked && skillWorld != null && world.Map.InBounds(cell.Value)) skillCell = cell.Value;
            SetHover(cell);
            var at = skillCell ?? cell;
            bool usable = false;
            if (skillWorld == null || !at.HasValue)
            {
                areaView.Hide();
                pushAreaView.Hide();
                if (skillWorld == null) hudView.ShowPlannedMovePreview(WeakestCoverAgainstCombatants(plannedCell ?? world.Player.Position));
                else ShowIdlePreview(null, default);
            }
            else if (aim == AimMode.FrostDecisive)
            {
                var decisive = FrostDecisive.Preview(skillWorld, settings, skillWorld.Player, at.Value);
                areaView.Show(decisive.Cells);
                hudView.ShowFrostDecisivePreview(decisive);
                usable = decisive.CanUse;
            }
            else if (aim == AimMode.Push)
            {
                Push.Aim(skillWorld.Player.Position, at.Value, out var direction, out int side);
                var push = Push.Preview(skillWorld, settings, skillWorld.Player, direction, side);
                pushAreaView.Show(push.Cells);
                hudView.ShowPushPreview(push);
                usable = push.CanPush;
            }
            else
            {
                var area = AreaAttack.Preview(skillWorld, settings, skillWorld.Player, at.Value);
                areaView.Show(area.Cells);
                hudView.ShowAreaPreview(area);
                usable = area.CanThrow;
            }
            hudView.ShowAttackConfirm(skillCell.HasValue, skillCell.HasValue && usable && !Busy);
            RefreshGhost(world, at.HasValue ? GridSpace.ToWorld(at.Value) : (Vector3?)null);
        }

        /// <summary>
        /// 서리 수호 조준 중 (3-17b): 대상이 없는 자신 스킬 — 범위(방위 지원을 세는 칸)와 미리보기를 보이고, 파란 칸 클릭 = 이동 예약 (예약 칸 다시 클릭 = 이동만).
        /// 쓰기는 확인(버튼 · Space · 같은 카드)으로. 범위 · 미리보기는 예약 칸으로 걸어 본 복제 세계에서.
        /// </summary>
        private void HandleFrostGuardAim(World world, GridPos? cell, bool clicked, bool canMove)
        {
            pushAreaView.Hide();
            if (cell.HasValue && canMove && reachable != null && reachable.ContainsKey(cell.Value) && cell.Value != world.Player.Position)
            {
                SetHover(cell);
                if (clicked)
                {
                    if (plannedCell.HasValue && plannedCell.Value == cell.Value) ConfirmMove();
                    else PlanMove(cell.Value);
                }
            }
            else
            {
                SetHover(null);
                if (clicked && cell.HasValue && cell.Value == world.Player.Position && plannedCell.HasValue) ClearPlan();
            }

            var skillWorld = PreviewWorld();
            if (skillWorld == null)
            {
                areaView.Hide();
                hudView.ShowPlannedMovePreview(WeakestCoverAgainstCombatants(plannedCell ?? world.Player.Position));
                hudView.ShowAttackConfirm(true, false);
            }
            else
            {
                var preview = FrostGuard.Preview(skillWorld, settings, skillWorld.Player);
                areaView.Show(preview.Cells);
                hudView.ShowFrostGuardPreview(preview);
                hudView.ShowAttackConfirm(true, preview.CanUse && !Busy);
            }
            RefreshSonicLines(null);
            RefreshGhost(world, null);
        }

        private void HandleSonicAim(World world, World skillWorld, GridPos? cell, bool clicked, bool canMove)
        {
            areaView.Hide();
            pushAreaView.Hide();
            bool handled = false;
            if (cell.HasValue)
            {
                var under = world.UnitAt(cell.Value);
                if (under != null && !under.IsPlayer)
                {
                    // 마우스 아래 적 (복제 세계에서는 같은 ID로 찾는다).
                    var target = skillWorld?.FindUnit(under.Id);
                    SetHover(cell);
                    if (target == null) ShowTargetHint();
                    else
                    {
                        bool usable = ShowTargetPreview(skillWorld, target);
                        if (clicked && usable) skillTargetId = under.Id;
                    }
                    handled = true;
                }
                else if (canMove && reachable != null && reachable.ContainsKey(cell.Value) && cell.Value != world.Player.Position)
                {
                    bool isPlanned = plannedCell.HasValue && plannedCell.Value == cell.Value;
                    if (isPlanned) hudView.ShowPlannedMovePreview(WeakestCoverAgainstCombatants(cell.Value));
                    else hudView.ShowMovePreview(WeakestCoverAgainstCombatants(cell.Value));
                    SetHover(cell);
                    if (clicked)
                    {
                        if (isPlanned) ConfirmMove();
                        else PlanMove(cell.Value);
                    }
                    handled = true;
                }
                else if (clicked && cell.Value == world.Player.Position && plannedCell.HasValue) ClearPlan();
            }

            // 예약을 바꿨으면 복제 세계도 바뀐다.
            skillWorld = PreviewWorld();
            var locked = skillTargetId >= 0 && skillWorld != null ? skillWorld.FindUnit(skillTargetId) : null;
            bool lockedUsable = locked != null && TargetSkillUsable(skillWorld, locked);
            if (!handled)
            {
                SetHover(null);
                if (locked != null) ShowTargetPreview(skillWorld, locked);
                else if (plannedCell.HasValue) hudView.ShowPlannedMovePreview(WeakestCoverAgainstCombatants(plannedCell.Value));
                else ShowTargetHint();
            }
            hudView.ShowAttackConfirm(skillTargetId >= 0, lockedUsable && !Busy);
            RefreshSonicLines(skillWorld);
            var lockedUnit = skillTargetId >= 0 ? world.FindUnit(skillTargetId) : null;
            RefreshGhost(world, lockedUnit != null ? GridSpace.ToWorld(lockedUnit.Position) : (Vector3?)null);
        }

        /// <summary>이동 예약 칸에 홀로그램을 세운다. 바라볼 곳(고른 대상)이 있으면 그쪽을, 없으면 걸어갈 방향을 바라본다.</summary>
        private void RefreshGhost(World world, Vector3? lookAt)
        {
            if (!plannedCell.HasValue || Busy)
            {
                playerView.HideGhost();
                return;
            }
            var at = GridSpace.ToWorld(plannedCell.Value);
            var look = lookAt ?? at + (at - GridSpace.ToWorld(world.Player.Position));
            var direction = look - at;
            float yaw = direction.sqrMagnitude > 0.0001f ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : playerView.FacingYaw;
            playerView.ShowGhost(at, yaw);
        }

        /// <summary>범위 칸은 상태가 바뀔 때만 다시 계산한다.</summary>
        private void RefreshRange(bool canMove)
        {
            var world = simulation.World;
            string key = canMove ? $"{world.Round}:{world.Player.Position}:{world.Units.Count}:{Loadout.MoveLeft(world)}" : "none";
            if (key == reachableKey) return;
            reachableKey = key;

            if (!canMove)
            {
                reachable = null;
                rangeView.Hide();
                return;
            }

            var player = world.Player;
            // 남은 이동 칸만큼만 (이번 차례에 이미 걸은 칸은 뺀다 — 판정은 규칙 Loadout.MoveLeft 한 곳).
            reachable = CombatMovement.Reachable(world, player, Loadout.MoveLeft(world), world.Explored);
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
                pendingBench = false;
                pendingCounter = null;
                pendingDoor = null;
            }
            if (Busy) return;
            if (pendingSteps.Count == 0)
            {
                // 잠긴 문 옆에 다 왔으면 문 창을 연다.
                if (pendingDoor.HasValue)
                {
                    var door = pendingDoor.Value;
                    pendingDoor = null;
                    if (Doors.CanReach(simulation.World, door)) DoorReached?.Invoke(door);
                    return;
                }
                // 교환대 옆에 다 왔으면 교환 창을 연다.
                if (pendingCounter.HasValue)
                {
                    var station = pendingCounter.Value;
                    pendingCounter = null;
                    ReachStation(station);
                    return;
                }
                // 작업대 옆에 다 왔으면 작업 화면을 연다.
                if (pendingBench)
                {
                    pendingBench = false;
                    pendingCounter = null;
                    pendingDoor = null;
                    if (Workshop.AtBench(simulation.World)) WorkbenchReached?.Invoke();
                    return;
                }
                // 상자 옆에 다 왔으면 연다.
                if (!pendingCrate.HasValue) return;
                var crate = pendingCrate.Value;
                pendingCrate = null;
                pendingBench = false;
                pendingCounter = null;
                pendingDoor = null;
                if (simulation.World.Mode == WorldMode.Exploration) simulation.Execute(new OpenSupplyCrateCommand(crate, settings));
                return;
            }

            // 길 중간 칸이면 지나가는 걸음 — 바닥 무기 · 파츠 · 진열 물건에 걸려 멈추지 않는다 (3-16v).
            var next = pendingSteps.Dequeue();
            var command = new MoveCommand(simulation.World.PlayerId, next, moveCostTicks, pendingSteps.Count > 0);
            if (!simulation.Execute(command))
            {
                pendingSteps.Clear();
                pendingCrate = null;
                pendingBench = false;
                pendingCounter = null;
                pendingDoor = null;
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
