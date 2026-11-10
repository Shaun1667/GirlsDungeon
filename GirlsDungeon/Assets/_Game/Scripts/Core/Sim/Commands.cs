using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>행동 하나. 즉시 적용되고, 비용만큼 틱이 흐른다.</summary>
    public interface ICommand
    {
        int CostTicks { get; }

        /// <summary>규칙에 맞으면 세계를 바꾸고 사건을 쌓은 뒤 true. 맞지 않으면 아무것도 바꾸지 않고 false.</summary>
        bool TryApply(World world, List<GameEvent> events);
    }

    /// <summary>탐험 중 붙어 있는 칸으로 한 걸음. 전투 중 이동은 2-3에서 따로 만든다.</summary>
    public sealed class MoveCommand : ICommand
    {
        private readonly int unitId;
        private readonly GridPos to;

        public int CostTicks { get; }

        public MoveCommand(int unitId, GridPos to, int costTicks)
        {
            this.unitId = unitId;
            this.to = to;
            CostTicks = costTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (world.Mode != WorldMode.Exploration) return false;

            var unit = world.FindUnit(unitId);
            if (unit == null) return false;
            if (!GridNavigation.CanStep(world.Map, unit.Position, to)) return false;
            if (world.IsOccupied(to)) return false;

            var from = unit.Position;
            unit.Position = to;
            events.Add(new UnitMoved(unit.Id, from, to));
            return true;
        }
    }

    /// <summary>플레이어 차례를 끝낸다. 적이 차례대로 움직이고 다음 라운드의 플레이어 차례가 된다.</summary>
    public sealed class EndTurnCommand : ICommand
    {
        public int CostTicks { get; }

        public EndTurnCommand(int costTicks)
        {
            CostTicks = costTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (world.Mode != WorldMode.Combat || world.Turn != TurnSide.Player) return false;

            world.Turn = TurnSide.Enemies;
            events.Add(new TurnStarted(TurnSide.Enemies, world.Round));
            EnemyTurn.Run(world, events);

            world.Round++;
            world.Turn = TurnSide.Player;
            events.Add(new TurnStarted(TurnSide.Player, world.Round));
            return true;
        }
    }

    /// <summary>디버그 전용: 유닛 하나를 바로 쓰러뜨린다. 플레이어에는 쓸 수 없다.</summary>
    public sealed class DebugDefeatCommand : ICommand
    {
        private readonly int unitId;

        public int CostTicks => 0;

        public DebugDefeatCommand(int unitId)
        {
            this.unitId = unitId;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            var unit = world.FindUnit(unitId);
            if (unit == null || unit.IsPlayer) return false;
            CombatFlow.Kill(world, unit, events);
            return true;
        }
    }
}
