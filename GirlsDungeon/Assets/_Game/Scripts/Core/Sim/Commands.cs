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

    /// <summary>붙어 있는 칸으로 한 걸음.</summary>
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
}
