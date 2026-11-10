using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 경계 상태와 감시의 한 곳 (BALANCE 7번).
    /// 경계: 경계 중인 적은 행동하지 않는다. 공격받거나 플레이어가 경계 칸(반경 GuardRadius)에 들어오면 깨어나고,
    /// 같은 방의 경계 적도 함께 깨어난다.
    /// 감시: 감시 중인 유닛은 상대가 자기 사거리 · 사선 안에서 한 걸음 움직일 때마다 확인해 처음 한 번 반응 사격한다 (일반 사격과 같은 계산).
    /// </summary>
    public static class Alert
    {
        /// <summary>경계 칸 — 적 칸에서 직선 거리 GuardRadius 안.</summary>
        public static bool InGuardZone(CombatSettings settings, Unit guard, GridPos cell)
        {
            int dx = cell.X - guard.Position.X;
            int dy = cell.Y - guard.Position.Y;
            return dx * dx + dy * dy <= settings.GuardRadius * settings.GuardRadius;
        }

        /// <summary>플레이어가 경계 칸 안에 있으면 그 적(과 같은 방의 경계 적)을 깨운다.</summary>
        public static void CheckZones(World world, CombatSettings settings, List<GameEvent> events)
        {
            var player = world.Player;
            if (player == null || settings == null) return;

            foreach (var unit in world.Units.ToArray())
            {
                if (!unit.Guarding || unit.IsPlayer) continue;
                if (InGuardZone(settings, unit, player.Position)) Wake(world, unit, events);
            }
        }

        /// <summary>경계 중인 적을 깨운다. 같은 방에 있는 경계 적도 함께 깨운다.</summary>
        public static void Wake(World world, Unit unit, List<GameEvent> events)
        {
            if (!unit.Guarding) return;

            var ids = new List<int>();
            var kinds = new List<UnitKind>();
            Add(unit, ids, kinds);

            foreach (var room in world.Map.Rooms)
            {
                if (!room.Contains(unit.Position)) continue;
                foreach (var other in world.Units)
                    if (other.Guarding && room.Contains(other.Position)) Add(other, ids, kinds);
            }
            events.Add(new EnemiesAlerted(ids, kinds));
        }

        private static void Add(Unit unit, List<int> ids, List<UnitKind> kinds)
        {
            if (!unit.Guarding) return;
            unit.Guarding = false;
            ids.Add(unit.Id);
            kinds.Add(unit.Kind);
        }

        /// <summary>감시 칸 — 감시자가 그 칸에 있는 상대를 쏠 수 있는 칸 (사거리 · 사선).</summary>
        public static bool InWatch(World world, Unit watcher, GridPos cell)
        {
            int dx = cell.X - watcher.Position.X;
            int dy = cell.Y - watcher.Position.Y;
            if (dx * dx + dy * dy > watcher.AttackRange * watcher.AttackRange) return false;
            return Sight.HasLine(world.Map, watcher.Position, cell);
        }

        /// <summary>
        /// mover가 한 걸음 옮긴 직후 부른다. 상대편 감시자(ID 순)가 쏠 수 있으면 반응 사격한다.
        /// mover가 쓰러지거나 패배하면 false (이동을 멈춘다).
        /// </summary>
        public static bool React(World world, CombatSettings settings, Unit mover, List<GameEvent> events)
        {
            foreach (var watcher in world.Units.ToArray())
            {
                if (!watcher.Overwatching || watcher.IsPlayer == mover.IsPlayer || watcher.Guarding) continue;
                if (!CombatRules.Preview(world, settings, watcher, watcher.Position, mover).CanAttack) continue;

                watcher.Overwatching = false;
                events.Add(new OverwatchTriggered(watcher, mover));
                CombatRules.Resolve(world, settings, watcher, mover, events);
                if (world.FindUnit(mover.Id) == null || world.Mode == WorldMode.Defeat) return false;
            }
            return true;
        }
    }

    /// <summary>플레이어 감시. 사격 대신 걸고, 차례가 끝난다. 다음 플레이어 차례가 시작되면 풀린다.</summary>
    public sealed class OverwatchCommand : ICommand
    {
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public OverwatchCommand(CombatSettings settings)
        {
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!TurnFlow.IsPlayerTurn(world) || world.PlayerActed) return false;

            var player = world.Player;
            player.Overwatching = true;
            world.PlayerActed = true;
            events.Add(new OverwatchSet(player.Id, player.Kind));
            TurnFlow.EndPlayerTurn(world, settings, events);
            return true;
        }
    }
}
