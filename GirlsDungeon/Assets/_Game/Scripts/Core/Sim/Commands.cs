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
    /// 전투 중 플레이어 이동 한 번. 차례마다 한 번, 사격 전에만(권총은 쏜 뒤에도 — 그때는 이동 뒤 차례가 끝난다), 이동 범위 안의 본 칸으로.
    /// </summary>
    public sealed class CombatMoveCommand : ICommand
    {
        private readonly GridPos destination;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        /// <param name="settings">있으면 걸음마다 적 감시에 반응 사격을 받는다 (게임에서는 항상 넘긴다).</param>
        public CombatMoveCommand(GridPos destination, CombatSettings settings = null)
        {
            this.destination = destination;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!Loadout.CanCombatMove(world, settings)) return false;

            var player = world.Player;
            var path = CombatMovement.Path(world, player, destination, player.MoveRange, world.Explored);
            if (path.Count == 0) return false;

            world.PlayerMoved = true;
            CombatMovement.Walk(world, settings, player, path, events);
            // 권총: 쏜 뒤 남은 이동까지 썼으면 차례가 끝난다. 기관단총은 아직 더 쏠 수 있으면 이어진다.
            if (world.PlayerActed && settings != null && TurnFlow.IsPlayerTurn(world) && world.Combatants.Count > 0
                && world.PlayerShots >= Loadout.ShotsPerTurn(world, settings))
                TurnFlow.EndPlayerTurn(world, settings, events);
            return true;
        }
    }

    /// <summary>
    /// 플레이어 사격. 사격하면 차례가 끝나고 적 차례가 이어진다 (전투가 끝났으면 바로 탐험).
    /// 권총으로 아직 이동하지 않고 쐈으면 차례가 이어진다 — 남은 이동을 쓰거나 턴 종료.
    /// 기관단총은 두 번째 사격까지 차례가 이어진다 (사이에 이동 가능).
    /// </summary>
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
            if (!Loadout.CanShoot(world, settings)) return false;

            var target = world.FindUnit(targetId);
            if (target == null || target.IsPlayer || !world.Combatants.Contains(target.Id)) return false;
            if (!CombatRules.Resolve(world, settings, world.Player, target, events)) return false;

            world.PlayerActed = true;
            world.PlayerShots++;
            bool keepsTurn = world.Mode == WorldMode.Combat && Loadout.KeepsTurnAfterShot(world, settings);
            if (world.Combatants.Count > 0 && !keepsTurn) TurnFlow.EndPlayerTurn(world, settings, events);
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

    /// <summary>디버그 전용: 유닛을 빈 칸으로 바로 옮긴다 (감시 반응 없음). 플레이어에는 쓸 수 없다.</summary>
    public sealed class DebugPlaceCommand : ICommand
    {
        private readonly int unitId;
        private readonly GridPos cell;

        public int CostTicks => 0;

        public DebugPlaceCommand(int unitId, GridPos cell)
        {
            this.unitId = unitId;
            this.cell = cell;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            var unit = world.FindUnit(unitId);
            if (unit == null || unit.IsPlayer || !world.Map.IsWalkable(cell) || world.IsOccupied(cell)) return false;
            var from = unit.Position;
            unit.Position = cell;
            events.Add(new UnitMoved(unit.Id, from, cell));
            return true;
        }
    }

    /// <summary>디버그 전용: 빈 칸에 적을 하나 만든다 (경계 상태 아님). 플레이어 종류는 만들 수 없다.</summary>
    public sealed class DebugSpawnCommand : ICommand
    {
        private readonly UnitKind kind;
        private readonly GridPos cell;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public DebugSpawnCommand(UnitKind kind, GridPos cell, CombatSettings settings)
        {
            this.kind = kind;
            this.cell = cell;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (kind == UnitKind.Player || !world.Map.IsWalkable(cell) || world.IsOccupied(cell)) return false;
            world.AddUnit(settings.StatsFor(kind), cell);
            return true;
        }
    }
}