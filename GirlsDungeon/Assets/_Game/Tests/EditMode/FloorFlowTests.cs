using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>BALANCE 11번 층 진행: 탐험 중 계단 → 다음 층, 5층 계단 = 승리, 층마다 적 수 +1 · 돌격병 +5%p · 체력 · 공격 +10% · 경계 −10%p.</summary>
    public class FloorFlowTests
    {
        /// <summary>적 없는 판에서 계단까지 걷는다. 걷는 동안 나온 사건을 돌려준다.</summary>
        private static List<GameEvent> WalkToStairs(Simulation simulation)
        {
            var world = simulation.World;
            var path = GridNavigation.FindPath(world.Map, world.Player.Position, world.Map.StairsCell);
            Assert.That(path.Count, Is.GreaterThan(0), "stairs are reachable");
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);
            foreach (var step in path)
                Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, step, 1)), Is.True, $"step {step}");
            return events;
        }

        [Test]
        public void SteppingOnTheStairs_GoesToTheNextFloor_KeepingThePlayer()
        {
            var world = TestData.NewRun(11);
            var player = world.Player;
            player.Hp = 7;
            var oldMap = world.Map;
            var simulation = new Simulation(world, TestData.Combat(), TestData.Map());

            var events = WalkToStairs(simulation);

            Assert.That(world.FloorIndex, Is.EqualTo(1));
            Assert.That(world.Player, Is.SameAs(player), "same unit, same id");
            Assert.That(player.Hp, Is.EqualTo(7), "no healing between floors (user decision)");
            Assert.That(player.Position, Is.EqualTo(world.Map.StartCell));
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(events.OfType<FloorChanged>().Single().FloorIndex, Is.EqualTo(1));
            Assert.That(world.Explored[world.Map.StartCell.X + world.Map.StartCell.Y * world.Map.Width], Is.True, "vision recomputed on the new map");
            Assert.That(world.Map, Is.Not.SameAs(oldMap), "a new map");
        }

        [Test]
        public void TheLastFloorsStairs_WinTheRun()
        {
            var world = TestData.NewRun(12);
            world.FloorIndex = TestData.Combat().FloorCount - 1;
            var simulation = new Simulation(world, TestData.Combat(), TestData.Map());

            var events = WalkToStairs(simulation);

            Assert.That(world.Mode, Is.EqualTo(WorldMode.Victory));
            Assert.That(events.OfType<RunWon>().Count(), Is.EqualTo(1));
            var anyStep = new GridPos(world.Player.Position.X + 1, world.Player.Position.Y);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, anyStep, 1)), Is.False, "no more moves after winning");
        }

        [Test]
        public void StandingOnTheStairs_WithoutSteppingThisCommand_DoesNothing()
        {
            var world = TestData.NewRun(13);
            world.Player.Position = world.Map.StairsCell;
            FloorFlow.CheckStairs(world, TestData.Map(), TestData.Combat(), new List<GameEvent>());
            Assert.That(world.FloorIndex, Is.EqualTo(0));
        }

        [Test]
        public void InCombat_TheStairsDoNotWork()
        {
            var world = TestData.NewRun(14);
            var stairs = world.Map.StairsCell;
            world.Mode = WorldMode.Combat;
            world.Player.Position = stairs;
            var events = new List<GameEvent> { new UnitMoved(world.PlayerId, stairs, stairs) };
            FloorFlow.CheckStairs(world, TestData.Map(), TestData.Combat(), events);
            Assert.That(world.FloorIndex, Is.EqualTo(0));
        }

        [Test]
        public void NextFloor_IsTheSameForTheSameSeed()
        {
            var a = TestData.NewRun(21, 4);
            var b = TestData.NewRun(21, 4);
            RunFactory.NextFloor(a, TestData.Map(), TestData.Combat(4), null);
            RunFactory.NextFloor(b, TestData.Map(), TestData.Combat(4), null);
            CollectionAssert.AreEqual(WorldSnapshot.Encode(a), WorldSnapshot.Encode(b));
        }

        [Test]
        public void Scale_RoundsHalfUp()
        {
            Assert.That(EnemySpawner.Scale(10, 140), Is.EqualTo(14));
            Assert.That(EnemySpawner.Scale(6, 140), Is.EqualTo(8), "8.4 → 8");
            Assert.That(EnemySpawner.Scale(8, 140), Is.EqualTo(11), "11.2 → 11");
            Assert.That(EnemySpawner.Scale(5, 130), Is.EqualTo(7), "6.5 → 7");
        }

        [Test]
        public void Floor5_HasMoreTougherAssaultHeavyAwakeEnemies()
        {
            var combat = TestData.Combat(5);
            int assaults = 0, guards = 0, total = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                var world = RunFactory.NewRun(seed, TestData.Map(), combat);
                for (int i = 0; i < 4; i++) RunFactory.NextFloor(world, TestData.Map(), combat, null);
                Assert.That(world.FloorIndex, Is.EqualTo(4));

                var enemies = world.Units.Where(u => !u.IsPlayer).ToList();
                Assert.That(enemies.Count, Is.EqualTo(5 + 4), $"seed {seed}: +1 enemy per floor");
                foreach (var enemy in enemies)
                {
                    var stats = combat.StatsFor(enemy.Kind);
                    Assert.That(enemy.MaxHp, Is.EqualTo(EnemySpawner.Scale(stats.MaxHp, 140)), $"seed {seed} {enemy.Kind} hp");
                    Assert.That(enemy.Hp, Is.EqualTo(enemy.MaxHp));
                    Assert.That(enemy.Attack, Is.EqualTo(EnemySpawner.Scale(stats.Attack, 140)), $"seed {seed} {enemy.Kind} attack");
                    Assert.That(enemy.Defense, Is.EqualTo(stats.Defense), "defense does not scale");
                    total++;
                    if (enemy.Kind == UnitKind.Assault) assaults++;
                    if (enemy.Guarding) guards++;
                }
            }
            Assert.That(assaults * 100.0 / total, Is.InRange(38.0, 62.0), "about 50% assaults on floor 5");
            Assert.That(guards * 100.0 / total, Is.LessThan(20.0), "about 10% guarding on floor 5");
        }

        [Test]
        public void Floor1_IsUnchanged()
        {
            var combat = TestData.Combat(5);
            var world = RunFactory.NewRun(5, TestData.Map(), combat);
            foreach (var enemy in world.Units.Where(u => !u.IsPlayer))
            {
                Assert.That(enemy.MaxHp, Is.EqualTo(combat.StatsFor(enemy.Kind).MaxHp));
                Assert.That(enemy.Attack, Is.EqualTo(combat.StatsFor(enemy.Kind).Attack));
            }
        }

        [Test]
        public void Kills_CountDefeatedEnemies_AndSurviveTheSnapshot()
        {
            var world = TestData.NewRun(31, 4);
            var simulation = new Simulation(world, TestData.Combat(4));
            var enemy = world.Units.First(u => !u.IsPlayer);
            Assert.That(simulation.Execute(new DebugDefeatCommand(enemy.Id)), Is.True);
            Assert.That(world.Kills, Is.EqualTo(1));

            world.FloorIndex = 3;
            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.Kills, Is.EqualTo(1));
            Assert.That(restored.FloorIndex, Is.EqualTo(3));
        }

        [Test]
        public void DebugDescend_GoesDownOrWins()
        {
            var world = TestData.NewRun(41);
            var simulation = new Simulation(world, TestData.Combat(), TestData.Map());
            for (int i = 0; i < 4; i++) Assert.That(simulation.Execute(new DebugDescendCommand(TestData.Map(), TestData.Combat())), Is.True);
            Assert.That(world.FloorIndex, Is.EqualTo(4));
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(simulation.Execute(new DebugDescendCommand(TestData.Map(), TestData.Combat())), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Victory));
        }
    }
}
