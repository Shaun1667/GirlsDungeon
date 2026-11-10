using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 층 진행의 한 곳 (결정 2026-11-10, 사용자): 탐험 중 이번 커맨드로 계단 칸에 들어서면 다음 층으로 내려간다.
    /// 마지막 층(FloorCount)의 계단이면 판 승리. 전투 중에는 내려가지 않는다 — 계단 위에서 전투가 끝나면 한 번 내려섰다 다시 밟는다.
    /// Simulation이 커맨드마다 CombatFlow.AfterAction 뒤에 부른다.
    /// </summary>
    public static class FloorFlow
    {
        public static void CheckStairs(World world, MapSettings mapSettings, CombatSettings settings, List<GameEvent> events)
        {
            var player = world.Player;
            if (player == null || world.Mode != WorldMode.Exploration) return;
            if (player.Position != world.Map.StairsCell || !SteppedOnThisCommand(world, events)) return;
            Descend(world, mapSettings, settings, events);
        }

        /// <summary>계단을 내려간다: 마지막 층이면 승리, 아니면 다음 층.</summary>
        public static void Descend(World world, MapSettings mapSettings, CombatSettings settings, List<GameEvent> events)
        {
            if (world.FloorIndex + 1 >= settings.FloorCount)
            {
                world.Mode = WorldMode.Victory;
                events.Add(new RunWon(world.FloorIndex));
                return;
            }
            RunFactory.NextFloor(world, mapSettings, settings, events);
        }

        private static bool SteppedOnThisCommand(World world, List<GameEvent> events)
        {
            foreach (var gameEvent in events)
                if (gameEvent is UnitMoved moved && moved.UnitId == world.PlayerId && moved.To == world.Map.StairsCell) return true;
            return false;
        }
    }

    /// <summary>새 층에 내려왔다. 지형 · 적을 모두 다시 그려야 한다. 플레이어는 StartCell에 있다.</summary>
    public sealed class FloorChanged : GameEvent
    {
        /// <summary>0 = 1층.</summary>
        public int FloorIndex { get; }
        public GridPos StartCell { get; }

        public FloorChanged(int floorIndex, GridPos startCell)
        {
            FloorIndex = floorIndex;
            StartCell = startCell;
        }
    }

    /// <summary>마지막 층의 계단을 밟아 판을 이겼다.</summary>
    public sealed class RunWon : GameEvent
    {
        public int FloorIndex { get; }

        public RunWon(int floorIndex)
        {
            FloorIndex = floorIndex;
        }
    }

    /// <summary>디버그 전용: 탐험 중 바로 계단을 내려간다 (마지막 층이면 승리).</summary>
    public sealed class DebugDescendCommand : ICommand
    {
        private readonly MapSettings mapSettings;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public DebugDescendCommand(MapSettings mapSettings, CombatSettings settings)
        {
            this.mapSettings = mapSettings;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (world.Mode != WorldMode.Exploration && world.Mode != WorldMode.Combat) return false;
            FloorFlow.Descend(world, mapSettings, settings, events);
            return true;
        }
    }
}
