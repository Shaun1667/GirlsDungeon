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

        private Simulation simulation;

        public World World => simulation?.World;

        /// <summary>디버그 기능을 쓴 판은 기록에 남기지 않는다.</summary>
        public bool DebugUsed { get; private set; }

        private void Start()
        {
            BeginRun(PickSeed());
        }

        public void BeginRun(int seed)
        {
            var combatSettings = balance.CreateCombatSettings();
            simulation = new Simulation(RunFactory.NewRun(seed, balance.CreateMapSettings(), combatSettings), combatSettings);
            simulation.EventsPublished += RebuildFloorWhenCoverBreaks;

            floorView.Build(simulation.World.Map, balance);
            fogView.Bind(simulation, balance);
            playerView.Bind(simulation, balance.StepSeconds);
            unitsView.Bind(simulation);
            hudView.Bind(simulation, combatSettings, balance);
            moveController.Bind(simulation, playerView, hudView, combatSettings, balance.MoveCostTicks);
            hudView.GrenadeButtonPressed -= moveController.ToggleAim;
            hudView.GrenadeButtonPressed += moveController.ToggleAim;
            hudView.OverwatchButtonPressed -= OnOverwatchButton;
            hudView.OverwatchButtonPressed += OnOverwatchButton;
            threatView.Bind(simulation, combatSettings);
            cameraRig.Bind(balance, playerView.transform);

            if (seedLabel != null) seedLabel.text = Text.Get("ui.hud.seed", seed);
        }

        /// <summary>엄폐물이 부서지면 지형 메시를 다시 만든다 (부서진 칸은 빈 바닥).</summary>
        private void RebuildFloorWhenCoverBreaks(System.Collections.Generic.IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events)
            {
                if (!(gameEvent is CoverDestroyed)) continue;
                floorView.Build(simulation.World.Map, balance);
                return;
            }
        }

        private void OnOverwatchButton()
        {
            moveController.RequestOverwatch();
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
            return hudView.EndTurn();
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
                + $"GrenadeCooldown={world.GrenadeCooldown}, Aiming={moveController.IsAiming}, "
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
            return $"Seed={world.Seed}, Tick={world.Tick}, Floor={world.FloorIndex}, Player={world.Player.Position}, "
                + $"Pending={moveController.PendingStepCount}, DebugUsed={DebugUsed}, Language={Text.Language}";
        }

        public string DebugMapSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            var map = world.Map;
            return $"Size={map.Width}x{map.Height}, Rooms={map.Rooms.Count}, Walkable={map.CountWalkable()}, "
                + $"Start={map.StartCell}, Stairs={map.StairsCell}, Crates={map.CountTiles(TileType.Crate)}, "
                + $"Pillars={map.CountTiles(TileType.Pillar)}, LowWalls={map.CountLowWalls()}";
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
