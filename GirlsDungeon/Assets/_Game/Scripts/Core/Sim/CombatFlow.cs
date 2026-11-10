using System.Collections.Generic;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 탐험 ↔ 턴 전투 전환과 유닛 사망의 한 곳. 커맨드가 끝날 때마다 Simulation이 AfterAction을 부른다 (틱 순서 ⑥).
    /// </summary>
    public static class CombatFlow
    {
        public static void AfterAction(World world, List<GameEvent> events)
        {
            var player = world.Player;
            if (player == null) return;

            JoinVisibleEnemies(world, player, events);

            if (world.Mode == WorldMode.Combat && world.Combatants.Count == 0)
                EndCombat(world, player, events);
        }

        /// <summary>유닛을 쓰러뜨린다. 맵에서 빠지고 전투 명단에서도 빠진다.</summary>
        public static void Kill(World world, Unit unit, List<GameEvent> events)
        {
            world.Units.Remove(unit);
            world.Combatants.Remove(unit.Id);
            events.Add(new UnitDied(unit.Id, unit.Kind));
        }

        private static void JoinVisibleEnemies(World world, Unit player, List<GameEvent> events)
        {
            List<int> joined = null;
            foreach (var unit in world.Units)
            {
                if (unit.IsPlayer || world.Combatants.Contains(unit.Id)) continue;
                if (!Sight.CanSee(world.Map, player.Position, unit.Position, player.VisionRange)) continue;

                if (joined == null) joined = new List<int>();
                joined.Add(unit.Id);
                world.Combatants.Add(unit.Id);
            }
            if (joined == null) return;

            world.Combatants.Sort();
            if (world.Mode == WorldMode.Exploration)
            {
                world.Mode = WorldMode.Combat;
                world.Turn = TurnSide.Player;
                world.Round = 1;
                events.Add(new CombatStarted(joined));
                events.Add(new TurnStarted(TurnSide.Player, world.Round));
            }
            else
            {
                events.Add(new EnemiesJoinedCombat(joined));
            }
        }

        private static void EndCombat(World world, Unit player, List<GameEvent> events)
        {
            world.Mode = WorldMode.Exploration;
            world.Turn = TurnSide.Player;
            world.Round = 0;
            if (player.Stability != player.MaxStability)
            {
                player.Stability = player.MaxStability;
                events.Add(new UnitStatsChanged(player.Id));
            }
            events.Add(new CombatEnded());
        }
    }

    /// <summary>적의 차례. 2-3에서 이동 · 사격 AI가 여기에 들어온다. 지금은 아무것도 하지 않는다.</summary>
    public static class EnemyTurn
    {
        public static void Run(World world, List<GameEvent> events)
        {
        }
    }
}
