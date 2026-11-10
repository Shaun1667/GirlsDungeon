using System.Collections.Generic;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 커맨드가 끝날 때마다 Simulation이 부르는 마무리 단계 (틱 순서 ⑥):
    /// 시야 갱신(VisionSystem) → 보이는 적 전투 합류 → 전투 종료 판정.
    /// </summary>
    public static class CombatFlow
    {
        public static void AfterAction(World world, List<GameEvent> events)
        {
            var player = world.Player;
            if (player == null) return;

            var visible = VisionSystem.Update(world, events);
            JoinVisibleEnemies(world, visible, events);

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

        private static void JoinVisibleEnemies(World world, bool[] visible, List<GameEvent> events)
        {
            List<int> joined = null;
            foreach (var unit in world.Units)
            {
                if (unit.IsPlayer || world.Combatants.Contains(unit.Id)) continue;
                if (!Vision.IsSet(visible, world.Map, unit.Position)) continue;

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

    /// <summary>플레이어 시야를 다시 계산해 본 칸 기록(World.Explored)에 더하고 VisibilityChanged를 낸다.</summary>
    public static class VisionSystem
    {
        /// <summary>지금 보이는 칸을 돌려준다. 플레이어가 없으면 빈 배열.</summary>
        public static bool[] Update(World world, List<GameEvent> events)
        {
            var map = world.Map;
            var player = world.Player;
            if (world.Explored == null || world.Explored.Length != map.Width * map.Height)
                world.Explored = new bool[map.Width * map.Height];
            if (player == null) return new bool[map.Width * map.Height];

            var visible = Vision.Compute(map, player.Position, player.VisionRange);
            for (int i = 0; i < visible.Length; i++)
                if (visible[i]) world.Explored[i] = true;

            events?.Add(new VisibilityChanged((bool[])visible.Clone(), (bool[])world.Explored.Clone()));
            return visible;
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
