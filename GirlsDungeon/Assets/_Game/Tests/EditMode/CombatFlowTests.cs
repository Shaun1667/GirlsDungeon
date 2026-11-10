using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    public class EnemySpawnerTests
    {
        private const int SeedCount = 200;

        [Test]
        public void EveryFloor_GetsTheRangeOfEnemies_FarAndHiddenFromStart()
        {
            var settings = TestData.Combat();
            settings.MinEnemiesPerFloor = 4;
            settings.MaxEnemiesPerFloor = 6;
            var counts = new HashSet<int>();

            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var world = RunFactory.NewRun(seed, TestData.Map(), settings);
                var map = world.Map;
                var enemies = world.Units.Where(u => !u.IsPlayer).ToList();
                counts.Add(enemies.Count);
                Assert.That(enemies.Count, Is.InRange(4, 6), $"seed {seed}");
                Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration), $"seed {seed}");

                foreach (var enemy in enemies)
                {
                    var cell = enemy.Position;
                    Assert.That(map.IsWalkable(cell), Is.True, $"seed {seed} {cell}");
                    Assert.That(cell, Is.Not.EqualTo(map.StairsCell), $"seed {seed}");
                    Assert.That(System.Math.Max(System.Math.Abs(cell.X - map.StartCell.X), System.Math.Abs(cell.Y - map.StartCell.Y)),
                        Is.GreaterThanOrEqualTo(8), $"seed {seed} {cell}");
                    Assert.That(Sight.CanSee(map, map.StartCell, cell, 9), Is.False, $"seed {seed} {cell}");
                    // 사격병 · 돌격병이 섞여 나온다 (2-8) — 각자 종류의 능력치로 가득 찬 채 시작한다.
                    var stats = settings.StatsFor(enemy.Kind);
                    Assert.That(enemy.Hp, Is.EqualTo(stats.MaxHp), $"seed {seed} {enemy.Kind}");
                    Assert.That(enemy.Stability, Is.EqualTo(stats.MaxStability), $"seed {seed} {enemy.Kind}");
                }
                Assert.That(enemies.Select(e => e.Position).Distinct().Count(), Is.EqualTo(enemies.Count), $"seed {seed}");
            }
            Assert.That(counts.Count, Is.GreaterThan(1), "enemy count never varies");
        }

        [Test]
        public void Spawning_DoesNotChangeTheMap()
        {
            for (int seed = 1; seed <= 30; seed++)
                Assert.That(Encode(TestData.NewRun(seed, 6).Map), Is.EqualTo(Encode(TestData.NewRun(seed).Map)), $"seed {seed}");
        }

        [Test]
        public void SameSeed_GivesSameEnemies()
        {
            var a = TestData.NewRun(77, 5);
            var b = TestData.NewRun(77, 5);
            CollectionAssert.AreEqual(a.Units.Select(u => u.Position), b.Units.Select(u => u.Position));
        }

        private static byte[] Encode(FloorMap map)
        {
            var world = new World(0, new GirlsDungeon.Core.Random.RandomStreams(0)) { Map = map };
            return WorldSnapshot.Encode(world);
        }
    }

    public class CombatFlowTests
    {
        /// <summary>열린 방에서 플레이어와 적을 마주 보게 놓는다.</summary>
        private static (Simulation simulation, World world, Unit enemy, List<GameEvent> events) OpenRoomWithEnemy(int enemyX)
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1))
            {
                Map = TestData.Parse(
                    "############",
                    "#..........#",
                    "#..........#",
                    "#..........#",
                    "############"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 2)).Id;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(enemyX, 2));
            var simulation = new Simulation(world);
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);
            return (simulation, world, enemy, events);
        }

        [Test]
        public void SeeingAnEnemy_StartsCombat_OnThePlayersTurn()
        {
            var (simulation, world, enemy, events) = OpenRoomWithEnemy(10);
            world.Map.SetTile(new GridPos(5, 1), TileType.Wall);
            world.Map.SetTile(new GridPos(5, 2), TileType.Wall);
            world.Map.SetTile(new GridPos(5, 3), TileType.Wall);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration), "wall should hide the enemy");

            world.Map.SetTile(new GridPos(5, 2), TileType.Floor);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(3, 2), 1)), Is.True);

            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            Assert.That(world.Turn, Is.EqualTo(TurnSide.Player));
            Assert.That(world.Round, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { enemy.Id }, world.Combatants);
            Assert.That(events.OfType<CombatStarted>().Single().EnemyIds, Is.EqualTo(new[] { enemy.Id }));
        }

        [Test]
        public void EnemyOutOfVisionRange_DoesNotStartCombat()
        {
            var far = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(new string('.', 15)) };
            var combat = TestData.Combat();
            far.PlayerId = far.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(0, 0)).Id;
            far.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(11, 0));
            var farSimulation = new Simulation(far);

            Assert.That(farSimulation.Execute(new MoveCommand(far.PlayerId, new GridPos(1, 0), 1)), Is.True);
            Assert.That(far.Mode, Is.EqualTo(WorldMode.Exploration), "distance 10 > vision 9");
            Assert.That(farSimulation.Execute(new MoveCommand(far.PlayerId, new GridPos(2, 0), 1)), Is.True);
            Assert.That(far.Mode, Is.EqualTo(WorldMode.Combat), "distance 9 = vision 9");
        }

        [Test]
        public void InCombat_ExplorationMoveIsRejected()
        {
            var (simulation, world, _, _) = OpenRoomWithEnemy(6);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));

            var before = WorldSnapshot.Encode(world);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 1), 1)), Is.False);
            Assert.That(WorldSnapshot.Encode(world), Is.EqualTo(before));
        }

        [Test]
        public void EndTurn_RunsTheEnemyTurn_AndStartsTheNextRound()
        {
            var (simulation, world, _, events) = OpenRoomWithEnemy(6);
            simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1));
            events.Clear();

            Assert.That(simulation.Execute(new EndTurnCommand(TestData.Combat())), Is.True);

            Assert.That(world.Round, Is.EqualTo(2));
            Assert.That(world.Turn, Is.EqualTo(TurnSide.Player));
            var turns = events.OfType<TurnStarted>().ToList();
            Assert.That(turns.Select(t => t.Side), Is.EqualTo(new[] { TurnSide.Enemies, TurnSide.Player }));
        }

        [Test]
        public void EndTurn_IsRejectedOutsideCombat()
        {
            var (simulation, world, _, _) = OpenRoomWithEnemy(10);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(simulation.Execute(new EndTurnCommand(TestData.Combat())), Is.False);
        }

        [Test]
        public void DefeatingTheLastCombatant_EndsCombat_AndRefillsStability()
        {
            var (simulation, world, enemy, events) = OpenRoomWithEnemy(6);
            simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1));
            world.Player.Stability = 2;

            Assert.That(simulation.Execute(new DebugDefeatCommand(enemy.Id)), Is.True);

            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(world.Combatants, Is.Empty);
            Assert.That(world.FindUnit(enemy.Id), Is.Null);
            Assert.That(world.Player.Stability, Is.EqualTo(world.Player.MaxStability));
            Assert.That(events.OfType<UnitDied>().Single().UnitId, Is.EqualTo(enemy.Id));
            Assert.That(events.OfType<CombatEnded>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void EnemySeenDuringCombat_JoinsIt()
        {
            var (simulation, world, first, events) = OpenRoomWithEnemy(6);
            simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1));
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));

            var second = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Rifleman), new GridPos(7, 1));
            simulation.Execute(new EndTurnCommand(TestData.Combat()));

            CollectionAssert.AreEqual(new[] { first.Id, second.Id }, world.Combatants);
            Assert.That(events.OfType<EnemiesJoinedCombat>().Single().EnemyIds, Is.EqualTo(new[] { second.Id }));
        }

        [Test]
        public void DebugDefeat_CannotTargetThePlayer()
        {
            var (simulation, world, _, _) = OpenRoomWithEnemy(10);
            Assert.That(simulation.Execute(new DebugDefeatCommand(world.PlayerId)), Is.False);
        }

        [Test]
        public void Snapshot_KeepsCombatStateAndUnitStats()
        {
            var (simulation, world, _, _) = OpenRoomWithEnemy(6);
            simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1));
            simulation.Execute(new EndTurnCommand(TestData.Combat()));
            world.Player.Hp = 13;
            world.Player.Stability = 4;

            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);

            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.Mode, Is.EqualTo(WorldMode.Combat));
            Assert.That(restored.Round, Is.EqualTo(2));
            Assert.That(restored.Player.Hp, Is.EqualTo(13));
            Assert.That(restored.Player.Stability, Is.EqualTo(4));
            CollectionAssert.AreEqual(world.Combatants, restored.Combatants);
        }
    }
}
