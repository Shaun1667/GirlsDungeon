using System;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>씬 연결의 한 곳(컴포지션 루트). 새 판 · 이어 하기는 모두 여기서 시작한다.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private GameBalance balance;
        [SerializeField] private UnityEngine.UI.Text seedLabel;
        [SerializeField] private FloorView floorView;
        [SerializeField] private PlayerView playerView;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private MoveController moveController;
        [SerializeField] private UnitsView unitsView;
        [SerializeField] private HudView hudView;
        [SerializeField] private FogView fogView;
        [SerializeField] private ThreatView threatView;
        [SerializeField] private ItemsView itemsView;
        [SerializeField] private EventPlayer eventPlayer;
        [SerializeField] private SkillBarView skillBar;
        [SerializeField] private CombatFxView combatFx;
        [Tooltip("Visual catalog: visual id -> model prefab (Docs/ART_ASSETS.md).")]
        [SerializeField] private UnitVisualCatalog visuals;
        [Tooltip("Start screen: pick a character before each run.")]
        [SerializeField] private CharacterSelectView characterSelect;

        private Simulation simulation;
        private CombatSettings combatSettings;
        private MapSettings mapSettings;
        /// <summary>지금(또는 다음) 판의 캐릭터. 시작 화면에서 고른 값이고, 디버그 재시작도 이 캐릭터를 쓴다.</summary>
        private CharacterKind character;

        public World World => simulation?.World;

        /// <summary>디버그 기능을 쓴 판은 기록에 남기지 않는다.</summary>
        public bool DebugUsed { get; private set; }

        private void Start()
        {
            // 시작 화면 뒤에 첫 캐릭터로 판을 미리 깔아 둔다 (화면이 비지 않게). 고르면 새 시드로 다시 만든다.
            foreach (var kind in balance.CharacterKinds())
            {
                character = kind;
                break;
            }
            if (characterSelect != null)
            {
                characterSelect.Picked -= OnCharacterPicked;
                characterSelect.Picked += OnCharacterPicked;
            }
            BeginRun(PickSeed());
            ShowCharacterSelect();
        }

        private void ShowCharacterSelect()
        {
            if (characterSelect == null || balance.CharacterCount == 0) return;
            characterSelect.Show(balance, combatSettings);
        }

        /// <summary>시작 화면에서 캐릭터를 골랐다: 새 시드로 그 캐릭터의 판을 시작한다. 디버그 표시는 새 판에 넘기지 않는다.</summary>
        private void OnCharacterPicked(CharacterKind picked)
        {
            character = picked;
            DebugUsed = false;
            BeginRun(PickSeed());
        }

        public void BeginRun(int seed)
        {
            combatSettings = balance.CreateCombatSettings();
            mapSettings = balance.CreateMapSettings();
            simulation = new Simulation(RunFactory.NewRun(seed, mapSettings, combatSettings, character), combatSettings, mapSettings);

            // 화면은 규칙 결과를 사건 재생기로 하나씩 받는다 (적 차례가 한 동작씩 보이게). 안개 · 위협 칸만 바로 받는다.
            eventPlayer.Bind(simulation, balance);
            eventPlayer.EventsPublished -= RebuildFloorWhenCoverBreaks;
            eventPlayer.EventsPublished += RebuildFloorWhenCoverBreaks;
            eventPlayer.FocusChanged -= OnFocusChanged;
            eventPlayer.FocusChanged += OnFocusChanged;

            floorView.Build(simulation.World.Map, balance);
            fogView.Bind(simulation, balance);
            // 플레이어 모습: 고른 캐릭터의 그림 ID — 그림 목록에 그 ID의 프리팹이 있으면 모델, 없으면 캡슐.
            playerView.Bind(eventPlayer, balance, visuals != null ? visuals.Find(balance.PlayerVisualId(simulation.World.PlayerCharacter)) : null);
            unitsView.Bind(eventPlayer, balance);
            itemsView.Bind(eventPlayer);
            hudView.Bind(eventPlayer, combatSettings, balance);
            moveController.Bind(simulation, eventPlayer, playerView, hudView, combatSettings, balance.MoveCostTicks, balance.CameraDragThresholdPixels);
            skillBar.Bind(simulation, eventPlayer, moveController, playerView, combatSettings, balance);
            combatFx.Bind(eventPlayer, balance, unitsView, playerView);
            hudView.NewRunPressed -= OnNewRunButton;
            hudView.NewRunPressed += OnNewRunButton;
            hudView.AttackConfirmPressed -= OnAttackConfirmPressed;
            hudView.AttackConfirmPressed += OnAttackConfirmPressed;
            hudView.SwapPressed -= OnSwapPressed;
            hudView.SwapPressed += OnSwapPressed;
            threatView.Bind(simulation, combatSettings);
            cameraRig.Bind(balance, playerView.transform);

            if (seedLabel != null) seedLabel.text = Text.Get("ui.hud.seed", seed);
        }

        /// <summary>엄폐물이 부서지거나, 보급 상자가 열리거나, 새 층에 내려오면 지형 메시를 다시 만든다.</summary>
        private void RebuildFloorWhenCoverBreaks(System.Collections.Generic.IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events)
            {
                if (!(gameEvent is CoverDestroyed) && !(gameEvent is FloorChanged) && !(gameEvent is SupplyCrateOpened)) continue;
                floorView.Build(simulation.World.Map, balance);
                return;
            }
        }

        /// <summary>결과 화면의 "새 판": 시작 화면을 다시 띄운다 (캐릭터를 고르면 새 시드로 시작). 캐릭터 표가 비었으면 바로 새 판.</summary>
        private void OnNewRunButton()
        {
            if (characterSelect != null && balance.CharacterCount > 0)
            {
                ShowCharacterSelect();
                return;
            }
            DebugUsed = false;
            BeginRun(PickSeed());
        }

        /// <summary>결과 화면의 "새 판" 버튼과 같다.</summary>
        public void DebugPressNewRun()
        {
            OnNewRunButton();
        }

        /// <summary>시작 화면의 카드 누르기와 같다 (0 = 첫 카드). 창이 닫혀 있거나 카드가 없으면 false.</summary>
        public bool DebugPickCharacter(int index)
        {
            return characterSelect != null && characterSelect.Press(index);
        }

        public string DebugCharacterSummary()
        {
            var world = World;
            string open = characterSelect != null && characterSelect.IsOpen ? "open" : "closed";
            if (world == null) return $"Select={open}, Run not started";
            var player = world.Player;
            return $"Select={open}, Character={world.PlayerCharacter}, Visual={balance.PlayerVisualId(world.PlayerCharacter)}, Model={playerView.ModelName ?? "capsule"}, "
                + $"HP {player.MaxHp}, ST {player.MaxStability}, Vision {player.VisionRange}, Def {player.Defense}, Move {player.MoveRange}, Weapon={world.PlayerWeapon}";
        }

        /// <summary>[상태 변경] 바로 계단을 내려간다 (마지막 층이면 승리). 못 하면 false.</summary>
        public bool DebugDescend()
        {
            if (simulation == null) return false;
            DebugUsed = true;
            return simulation.Execute(new DebugDescendCommand(mapSettings, combatSettings));
        }

        /// <summary>[상태 변경] 계단까지 걸어가기 시작한다 (본 칸이 아니면 못 감 — 그때는 false).</summary>
        public bool DebugWalkToStairs()
        {
            if (World == null) return false;
            DebugUsed = true;
            return moveController.RequestMove(World.Map.StairsCell);
        }

        /// <summary>적 차례: 카메라가 지금 움직이는 적을 따라가고, 끝나면 플레이어로 돌아온다.</summary>
        private void OnFocusChanged(int unitId)
        {
            var world = World;
            if (world == null) return;
            var target = unitId == world.PlayerId ? playerView.transform : unitsView.ViewOf(unitId);
            cameraRig.SetTarget(target);
        }

        /// <summary>사건 재생 상태 (디버그).</summary>
        public string DebugPlaybackSummary()
        {
            if (eventPlayer == null) return "No player";
            var target = cameraRig.Target;
            return $"Playing={eventPlayer.IsPlaying}, Pending={eventPlayer.PendingCount}, ShownTurn={eventPlayer.ShownTurn}, "
                + $"CameraTarget={(target != null ? target.name : "none")}, Popups={combatFx.ActivePopupCount}, Tracers={combatFx.ActiveTracerCount}, "
                + $"LastPopup={combatFx.LastPopupText}, SkillBar={skillBar.IsShown}";
        }

        /// <summary>[상태 변경] 재생 중인 사건을 기다리지 않고 모두 내보낸다.</summary>
        public void DebugFlushPlayback()
        {
            DebugUsed = true;
            eventPlayer.Flush();
        }

        public SkillBarView DebugSkillBar => skillBar;

        private void OnAttackConfirmPressed()
        {
            moveController.ConfirmAttack();
        }

        /// <summary>[상태 변경] 적 클릭과 같다: 그 적을 사격 대상으로 고른다 (노란 선 · 확인 버튼). 못 고르면 false.</summary>
        public bool DebugSelectTarget(int unitId)
        {
            DebugUsed = true;
            return moveController.SelectTarget(unitId);
        }

        /// <summary>전투 중 이동 칸 클릭과 같다: 그 칸을 이동 예약한다 (홀로그램, 아직 걷지 않음). 못 하면 false.</summary>
        public bool DebugPlanMove(GridPos cell)
        {
            return moveController.PlanMove(cell);
        }

        /// <summary>[상태 변경] 예약 칸 다시 클릭 · 대상 없이 Enter와 같다: 예약 칸으로 이동만 한다. 못 하면 false.</summary>
        public bool DebugConfirmMove()
        {
            DebugUsed = true;
            return moveController.ConfirmMove();
        }

        public string DebugPlanSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            return $"Planned={(moveController.PlannedCell.HasValue ? moveController.PlannedCell.Value.ToString() : "none")}, Ghost={playerView.GhostShown}, "
                + $"Player={world.Player.Position}, Moved={world.PlayerMoved}, Shots={world.PlayerShots}, Selected={moveController.SelectedTargetId}, Lines=[{moveController.TargetLinesSummary}]";
        }

        /// <summary>[상태 변경] 음파 공격을 그 적에게 쓴다 (조준 모드에서 적을 클릭한 것과 같다). 못 쓰면 false.</summary>
        public bool DebugSonic(int targetId)
        {
            DebugUsed = true;
            return moveController.RequestSonic(targetId);
        }

        /// <summary>스킬 바 칸 (2 · 3 · 4)에 든 스킬과 카드 이름 · 쓸 수 있는지.</summary>
        public string DebugSkillSlotsSummary()
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 3; i++)
            {
                var card = skillBar.SlotCard(i);
                parts.Add($"{i + 2}:{skillBar.SlotSkill(i)}({(card.gameObject.activeSelf ? card.NameText + (card.Interactable ? "" : " off") : "hidden")})");
            }
            return string.Join(" ", parts) + $", Aim={moveController.Aim}, SonicCooldown={World?.SonicCooldown}";
        }

        /// <summary>[상태 변경] 사격 확인 버튼과 같다. 못 쏘면 false.</summary>
        public bool DebugConfirmAttack()
        {
            DebugUsed = true;
            return moveController.ConfirmAttack();
        }

        public string DebugTargetSummary()
        {
            return $"Selected={moveController.SelectedTargetId}, Lines=[{moveController.TargetLinesSummary}], "
                + $"Confirm shown={hudView.AttackConfirmShown} enabled={hudView.AttackConfirmEnabled}, Preview={hudView.PreviewText.Replace('\n', ' ')}";
        }

        /// <summary>비교 창의 "바꾸기": 발밑 무기로 바꾼다.</summary>
        private void OnSwapPressed(int itemId)
        {
            simulation.Execute(new SwapWeaponCommand(itemId, combatSettings));
        }

        /// <summary>[상태 변경] 보급 상자를 클릭한 것과 같다 (멀면 걸어가서 연다). 가장 가까운 닫힌 상자, 없거나 못 가면 false.</summary>
        public bool DebugOpenNearestCrate()
        {
            var world = World;
            if (world == null) return false;
            GridPos? best = null;
            int bestDistance = int.MaxValue;
            var map = world.Map;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (map.Get(cell) != TileType.SupplyCrate) continue;
                    int distance = Loadout.CellDistance(cell, world.Player.Position);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = cell;
                }
            }
            if (!best.HasValue) return false;
            DebugUsed = true;
            return moveController.RequestOpenCrate(best.Value);
        }

        /// <summary>[상태 변경] 플레이어 옆 빈 칸에 무기(또는 구급상자)를 놓는다. 놓은 칸, 못 놓으면 null.</summary>
        public GridPos? DebugDropItem(ItemKind kind, WeaponKind weapon)
        {
            var world = World;
            if (world == null) return null;
            var player = world.Player.Position;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new GridPos(player.X + dx, player.Y + dy);
                    if ((dx == 0 && dy == 0) || !world.Map.IsWalkable(cell) || world.IsOccupied(cell) || world.ItemAt(cell) != null) continue;
                    DebugUsed = true;
                    return simulation.Execute(new DebugDropItemCommand(kind, weapon, cell)) ? cell : (GridPos?)null;
                }
            }
            return null;
        }

        /// <summary>[상태 변경] 비교 창의 "바꾸기"와 같다. 창이 없으면 false.</summary>
        public bool DebugPressSwap()
        {
            DebugUsed = true;
            return hudView.PressSwap();
        }

        public string DebugLootSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var item in world.Items) parts.Add($"#{item.Id} {item.Kind}{(item.Kind == ItemKind.Weapon ? " " + item.Weapon : "")} {item.Position}");
            var player = world.Player;
            return $"Weapon={world.PlayerWeapon} (Atk {player.Attack}, Range {player.AttackRange}, Stab {player.StabilityDamage}, Crit {player.CritPercent}%), "
                + $"Crates={world.Map.CountTiles(TileType.SupplyCrate)} closed / {world.Map.CountTiles(TileType.SupplyCrateOpened)} open, "
                + $"Offer={hudView.OfferedItemId}, Items=[{string.Join(", ", parts)}], ItemViews={itemsView.VisibleCount}";
        }

        /// <summary>[상태 변경] 감시 버튼과 같다. 못 하면 false.</summary>
        public bool DebugOverwatch()
        {
            DebugUsed = true;
            return moveController.RequestOverwatch();
        }

        public string DebugThreatSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var unit in world.Units)
                if (unit.Guarding || unit.Overwatching) parts.Add($"#{unit.Id}{(unit.Guarding ? " guard" : "")}{(unit.Overwatching ? " watch" : "")}");
            return $"[{string.Join(", ", parts)}] GuardCells={threatView.GuardCellCount}, WatchCells={threatView.WatchCellCount}";
        }

        /// <summary>[상태 변경] 새 시드로 판을 다시 시작한다.</summary>
        public void DebugRestart()
        {
            DebugUsed = true;
            BeginRun(PickSeed());
        }

        /// <summary>[상태 변경] 정한 시드로 판을 다시 시작한다.</summary>
        public void DebugRestartWithSeed(int seed)
        {
            DebugUsed = true;
            BeginRun(seed);
        }

        /// <summary>[상태 변경] 칸을 클릭한 것과 같게 걷기 시작한다. 갈 수 없으면 false.</summary>
        public bool DebugMoveTo(GridPos cell)
        {
            DebugUsed = true;
            return moveController.RequestMove(cell);
        }

        /// <summary>[상태 변경] 전투 중 칸 클릭과 같다: 적이 있으면 사격, 아니면 이동. 못 하면 false.</summary>
        public bool DebugCombatAction(GridPos cell)
        {
            DebugUsed = true;
            return moveController.RequestCombatAction(cell);
        }

        /// <summary>[상태 변경] 수류탄을 그 칸에 던진다. 못 던지면 false.</summary>
        public bool DebugThrowGrenade(GridPos center)
        {
            DebugUsed = true;
            return moveController.RequestAreaAttack(center);
        }

        /// <summary>[상태 변경] 그 칸 쪽으로 밀치기를 쓴다 (마우스를 그 칸에 두고 클릭한 것과 같다). 못 쓰면 false.</summary>
        public bool DebugPush(GridPos pointer)
        {
            DebugUsed = true;
            return moveController.RequestPush(pointer);
        }

        /// <summary>밀치기 버튼과 같다 (조준 모드 켜기 · 끄기).</summary>
        public void DebugTogglePushAim()
        {
            moveController.TogglePushAim();
        }

        /// <summary>[상태 변경] 칸 종류를 바꾸고 지형을 다시 그린다 (낭떠러지 시험용).</summary>
        public void DebugSetTile(GridPos cell, TileType tile)
        {
            if (World == null || !World.Map.InBounds(cell)) return;
            DebugUsed = true;
            World.Map.SetTile(cell, tile);
            floorView.Build(World.Map, balance);
        }

        /// <summary>[상태 변경] 유닛을 그 칸으로 옮긴다 (밀치기 시험용). 설 수 없거나 차 있으면 false.</summary>
        public bool DebugPlaceUnit(int unitId, GridPos cell)
        {
            var world = World;
            var unit = world?.FindUnit(unitId);
            if (unit == null || !world.Map.IsWalkable(cell) || world.IsOccupied(cell)) return false;
            DebugUsed = true;
            return simulation.Execute(new DebugPlaceCommand(unitId, cell));
        }

        /// <summary>[상태 변경] 그 빈 칸에 적을 하나 만든다 (돌격병 시험용). 만든 적 ID, 못 만들면 -1.</summary>
        public int DebugSpawnEnemy(UnitKind kind, GridPos cell)
        {
            var world = World;
            if (world == null) return -1;
            DebugUsed = true;
            int id = world.NextEntityId;
            return simulation.Execute(new DebugSpawnCommand(kind, cell, combatSettings)) ? id : -1;
        }

        /// <summary>수류탄 버튼과 같다 (조준 모드 켜기 · 끄기).</summary>
        public void DebugToggleAim()
        {
            moveController.ToggleAim();
        }

        /// <summary>[상태 변경] 전투 중 첫 번째 적을 쏜다. 못 쏘면 false.</summary>
        public bool DebugShootFirstCombatant()
        {
            var world = World;
            if (world == null || world.Combatants.Count == 0) return false;
            return DebugCombatAction(world.FindUnit(world.Combatants[0]).Position);
        }

        /// <summary>[상태 변경] 카메라를 돌린다. Q/E와 같다.</summary>
        public void DebugRotateCamera(int direction)
        {
            cameraRig.Rotate(direction);
        }

        /// <summary>안개를 숨겨 맵 전체를 밝힌다. 표시만 바꾸며 본 칸 기록 · 규칙은 그대로다.</summary>
        public bool DebugRevealAll
        {
            get => fogView.RevealAll;
            set => fogView.RevealAll = value;
        }

        public string DebugVisionSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            var player = world.Player;
            var visible = Vision.Compute(world.Map, player.Position, player.VisionRange);
            int seen = 0, explored = 0;
            foreach (var cell in visible) if (cell) seen++;
            foreach (var cell in world.Explored) if (cell) explored++;
            string room = "corridor";
            for (int i = 0; i < world.Map.Rooms.Count; i++)
                if (world.Map.Rooms[i].Contains(player.Position)) room = "room " + i;
            return $"Visible={seen}, Explored={explored}/{visible.Length}, Player in {room}";
        }

        /// <summary>[상태 변경] 턴 종료 버튼과 같다. 지금 플레이어 차례가 아니면 false.</summary>
        public bool DebugEndTurn()
        {
            DebugUsed = true;
            return skillBar.EndTurn();
        }

        /// <summary>[상태 변경] 전투 중인 적(없으면 가장 가까운 적) 하나를 쓰러뜨린다. 쓰러뜨린 적이 없으면 false.</summary>
        public bool DebugDefeatEnemy()
        {
            var world = World;
            if (world == null) return false;

            Unit target = null;
            if (world.Combatants.Count > 0) target = world.FindUnit(world.Combatants[0]);
            else
            {
                int best = int.MaxValue;
                foreach (var unit in world.Units)
                {
                    if (unit.IsPlayer) continue;
                    int distance = Math.Max(Math.Abs(unit.Position.X - world.Player.Position.X), Math.Abs(unit.Position.Y - world.Player.Position.Y));
                    if (distance >= best) continue;
                    best = distance;
                    target = unit;
                }
            }
            if (target == null) return false;

            DebugUsed = true;
            return simulation.Execute(new DebugDefeatCommand(target.Id));
        }

        public string DebugCombatSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            var player = world.Player;
            return $"Mode={world.Mode}, Turn={world.Turn}, Round={world.Round}, Moved={world.PlayerMoved}, Acted={world.PlayerActed}, "
                + $"GrenadeCooldown={world.GrenadeCooldown}, PushCooldown={world.PushCooldown}, Aim={moveController.Aim}, "
                + $"Reachable={moveController.ReachableCount}, Combatants=[{string.Join(",", world.Combatants)}], "
                + $"PlayerHp={player.Hp}/{player.MaxHp}, Stability={player.Stability}/{player.MaxStability}, VisibleEnemies={unitsView.VisibleCount}";
        }

        public string DebugUnitsSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var unit in world.Units) parts.Add($"#{unit.Id} {unit.Kind} {unit.Position} HP {unit.Hp}/{unit.MaxHp} ST {unit.Stability}/{unit.MaxStability}");
            return string.Join("\n", parts);
        }

        public string DebugStateSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            return $"Seed={world.Seed}, Tick={world.Tick}, Floor={world.FloorIndex + 1}/{combatSettings.FloorCount}, Kills={world.Kills}, Mode={world.Mode}, Player={world.Player.Position}, "
                + $"Pending={moveController.PendingStepCount}, DebugUsed={DebugUsed}, Language={Text.Language}";
        }

        public string DebugMapSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            var map = world.Map;
            return $"Size={map.Width}x{map.Height}, Rooms={map.Rooms.Count}, Walkable={map.CountWalkable()}, "
                + $"Start={map.StartCell}, Stairs={map.StairsCell}, Crates={map.CountTiles(TileType.Crate)}, "
                + $"Pillars={map.CountTiles(TileType.Pillar)}, LowWalls={map.CountLowWalls()}, Chasm={map.CountTiles(TileType.Chasm)}, "
                + $"SupplyCrates={map.CountTiles(TileType.SupplyCrate) + map.CountTiles(TileType.SupplyCrateOpened)}";
        }

        /// <summary>칸이 네 방향으로 받는 엄폐. 예: "E:Half N:Full".</summary>
        public string DebugCoverAt(GridPos cell)
        {
            var map = World?.Map;
            if (map == null || !map.InBounds(cell)) return "Out of map";
            return $"Tile={map.Get(cell)}, E:{Cover.InDirection(map, cell, new GridPos(1, 0))} "
                + $"W:{Cover.InDirection(map, cell, new GridPos(-1, 0))} N:{Cover.InDirection(map, cell, new GridPos(0, 1))} "
                + $"S:{Cover.InDirection(map, cell, new GridPos(0, -1))}";
        }

        private int PickSeed()
        {
            if (balance.FixedSeed != 0) return balance.FixedSeed;
            int seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            return seed == 0 ? 1 : seed;
        }
    }
}
