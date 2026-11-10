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

    /// <summary>탐험 중 붙어 있는 칸으로 한 걸음.</summary>
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

    /// <summary>
    /// 전투 중 플레이어 이동 한 번. 차례마다 한 번, 사격 전에만, 이동 범위 안의 본 칸으로.
    /// </summary>
    public sealed class CombatMoveCommand : ICommand
    {
        private readonly GridPos destination;

        public int CostTicks => 0;

        public CombatMoveCommand(GridPos destination)
        {
            this.destination = destination;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!TurnFlow.IsPlayerTurn(world) || world.PlayerMoved || world.PlayerActed) return false;

            var player = world.Player;
            var path = CombatMovement.Path(world, player, destination, player.MoveRange, world.Explored);
            if (path.Count == 0) return false;

            CombatMovement.Walk(player, path, events);
            world.PlayerMoved = true;
            return true;
        }
    }

    /// <summary>플레이어 사격. 사격하면 차례가 끝나고 적 차례가 이어진다 (전투가 끝났으면 바로 탐험).</summary>
    public sealed class AttackCommand : ICommand
    {
        private readonly int targetId;
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public AttackCommand(int targetId, CombatSettings settings)
        {
            this.targetId = targetId;
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!TurnFlow.IsPlayerTurn(world) || world.PlayerActed) return false;

            var target = world.FindUnit(targetId);
            if (target == null || target.IsPlayer || !world.Combatants.Contains(target.Id)) return false;
            if (!CombatRules.Resolve(world, settings, world.Player, target, events)) return false;

            world.PlayerActed = true;
            if (world.Combatants.Count > 0) TurnFlow.EndPlayerTurn(world, settings, events);
            return true;
        }
    }

    /// <summary>플레이어 차례를 끝낸다. 적이 차례대로 움직이고 다음 라운드의 플레이어 차례가 된다.</summary>
    public sealed class EndTurnCommand : ICommand
    {
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public EndTurnCommand(CombatSettings settings)
        {
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!TurnFlow.IsPlayerTurn(world)) return false;
            TurnFlow.EndPlayerTurn(world, settings, events);
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
