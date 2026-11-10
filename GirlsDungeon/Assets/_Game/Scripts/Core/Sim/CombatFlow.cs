using System.Collections.Generic;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 커맨드가 끝날 때마다 Simulation이 부르는 마무리 단계 (틱 순서 ⑥):
    /// 시야 갱신(VisionSystem) → 보이는 적 전투 합류 → 경계 칸 확인(Alert) → 전투 종료 판정.
    /// </summary>
    public static class CombatFlow
    {
        public static void AfterAction(World world, CombatSettings settings, List<GameEvent> events)
        {
            var player = world.Player;
            if (player == null || world.Mode == WorldMode.Defeat) return;

            var visible = VisionSystem.Update(world, events);
            JoinVisibleEnemies(world, visible, events);
            Alert.CheckZones(world, settings, events);

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
                world.PlayerMoved = false;
                world.PlayerActed = false;
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
            world.PlayerMoved = false;
            world.PlayerActed = false;
            world.GrenadeCooldown = 0;
            player.Overwatching = false;
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

    /// <summary>차례 넘기기의 한 곳.</summary>
    public static class TurnFlow
    {
        public static bool IsPlayerTurn(World world) => world.Mode == WorldMode.Combat && world.Turn == TurnSide.Player;

        /// <summary>플레이어 차례 끝 → 적 차례 → (패배가 아니면) 다음 라운드 플레이어 차례.</summary>
        public static void EndPlayerTurn(World world, CombatSettings settings, List<GameEvent> events)
        {
            world.Turn = TurnSide.Enemies;
            events.Add(new TurnStarted(TurnSide.Enemies, world.Round));
            EnemyTurn.Run(world, settings, events);
            if (world.Mode != WorldMode.Combat) return;

            world.Round++;
            world.Turn = TurnSide.Player;
            world.PlayerMoved = false;
            world.PlayerActed = false;
            if (world.GrenadeCooldown > 0) world.GrenadeCooldown--;
            world.Player.Overwatching = false;
            events.Add(new TurnStarted(TurnSide.Player, world.Round));
        }
    }

    /// <summary>
    /// 적 차례 (전투에 들어온 적, ID 순서). 경계 중인 적은 행동하지 않는다.
    /// 사격병: 이동 범위 안에서 플레이어를 쏠 수 있는 칸 중 플레이어 쪽 엄폐가 가장 좋은 칸(같으면 먼 칸)으로 간 뒤 사격한다.
    /// 쏠 수 있는 칸이 없으면 플레이어에게 가장 가까워지는 칸으로 가고 감시를 건다. 걷는 동안 플레이어의 감시에 맞을 수 있다.
    /// 칸 고르기 · 사선 · 피해는 플레이어와 같은 규칙(CombatMovement · CombatRules · Cover · Alert)을 쓴다.
    /// </summary>
    public static class EnemyTurn
    {
        public static void Run(World world, CombatSettings settings, List<GameEvent> events)
        {
            foreach (int id in world.Combatants.ToArray())
            {
                if (world.Mode != WorldMode.Combat) return;
                var enemy = world.FindUnit(id);
                var player = world.Player;
                if (enemy == null || player == null || enemy.Guarding) continue;

                enemy.Overwatching = false;
                var destination = ChooseCell(world, settings, enemy, player);
                if (destination != enemy.Position)
                {
                    var path = CombatMovement.Path(world, enemy, destination, enemy.MoveRange);
                    if (!CombatMovement.Walk(world, settings, enemy, path, events)) continue;
                }

                if (CombatRules.Resolve(world, settings, enemy, player, events)) continue;
                enemy.Overwatching = true;
                events.Add(new OverwatchSet(enemy.Id, enemy.Kind));
            }
        }

        private static Grid.GridPos ChooseCell(World world, CombatSettings settings, Unit enemy, Unit player)
        {
            var reachable = CombatMovement.Reachable(world, enemy, enemy.MoveRange);
            var best = enemy.Position;
            int bestScore = int.MinValue;

            foreach (var pair in Ordered(reachable))
            {
                var cell = pair.Key;
                int steps = pair.Value;
                bool canShoot = CombatRules.Preview(world, settings, enemy, cell, player).CanAttack;
                int score;
                if (canShoot)
                {
                    // 엄폐가 가장 중요하고, 같으면 플레이어에게서 먼 칸 (결정 2026-11-10, 사용자 — 바로 옆까지 붙지 않게).
                    var cover = Cover.Against(world.Map, cell, player.Position);
                    int dx = cell.X - player.Position.X;
                    int dy = cell.Y - player.Position.Y;
                    score = 100000 + (int)cover * 1000 + (dx * dx + dy * dy) * 2 - steps;
                }
                else
                {
                    int dx = cell.X - player.Position.X;
                    int dy = cell.Y - player.Position.Y;
                    score = -(dx * dx + dy * dy) * 10 - steps;
                }

                if (score <= bestScore) continue;
                bestScore = score;
                best = cell;
            }
            return best;
        }

        /// <summary>사전 순회 순서에 기대지 않도록 걸음 수 → y → x 순으로 정렬한다.</summary>
        private static List<KeyValuePair<Grid.GridPos, int>> Ordered(Dictionary<Grid.GridPos, int> cells)
        {
            var list = new List<KeyValuePair<Grid.GridPos, int>>(cells);
            list.Sort((a, b) => a.Value != b.Value ? a.Value.CompareTo(b.Value)
                : a.Key.Y != b.Key.Y ? a.Key.Y.CompareTo(b.Key.Y) : a.Key.X.CompareTo(b.Key.X));
            return list;
        }
    }
}
