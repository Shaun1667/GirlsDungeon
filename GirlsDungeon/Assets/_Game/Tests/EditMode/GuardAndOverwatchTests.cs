using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>BALANCE 7번 경계 상태 (적 50% · 경계 칸 반경 3)와 감시(반응 사격).</summary>
    public class GuardTests
    {
        [Test]
        public void AboutHalfOfTheEnemies_StartGuarding()
        {
            int guarding = 0, total = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                foreach (var enemy in TestData.NewRun(seed, 5).Units.Where(u => !u.IsPlayer))
                {
                    total++;
                    if (enemy.Guarding) guarding++;
                }
            }
            Assert.That(guarding / (double)total, Is.InRange(0.4, 0.6));
        }

        /// <summary>열린 방 (1,1) ~ (12,3)을 방 하나로 둔다. 플레이어 (1,2), 경계 사격병 (9,2).</summary>
        private static (Simulation simulation, World world, Unit enemy) GuardRoom()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1))
            {
                Map = TestData.ParseWithRooms(new[] { new RoomRect(1, 1, 12, 3) },
                    "##############",
                    "#............#",
                    "#............#",
                    "#............#",
                    "##############"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 2)).Id;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(9, 2));
            enemy.Guarding = true;
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat), "a guarding enemy still joins combat on sight");
            return (simulation, world, enemy);
        }

        [Test]
        public void GuardingEnemy_DoesNothingOnItsTurn()
        {
            var (simulation, world, enemy) = GuardRoom();
            simulation.Execute(new EndTurnCommand(TestData.Combat()));

            Assert.That(enemy.Position, Is.EqualTo(new GridPos(9, 2)));
            Assert.That(world.Player.Hp, Is.EqualTo(world.Player.MaxHp));
            Assert.That(enemy.Guarding, Is.True);
        }

        [Test]
        public void EnteringTheGuardZone_WakesTheEnemy()
        {
            var (simulation, world, enemy) = GuardRoom();
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(6, 2), TestData.Combat())), Is.True);
            Assert.That(enemy.Guarding, Is.False, "distance 3 is inside the zone");
            Assert.That(events.OfType<EnemiesAlerted>().Single().UnitIds, Is.EqualTo(new[] { enemy.Id }));
        }

        [Test]
        public void BeingShot_WakesTheEnemy_AndItsRoommates()
        {
            var (simulation, world, enemy) = GuardRoom();
            var roommate = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Rifleman), new GridPos(12, 3));
            roommate.Guarding = true;
            enemy.Hp = enemy.MaxHp = 100;
            world.Player.CritPercent = 0;

            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, TestData.Combat())), Is.True);
            Assert.That(enemy.Guarding, Is.False);
            Assert.That(roommate.Guarding, Is.False, "same room wakes together");
        }

        [Test]
        public void WakingUp_LetsTheEnemyActNextTurn()
        {
            var (simulation, world, enemy) = GuardRoom();
            enemy.Hp = enemy.MaxHp = 100;
            world.Player.CritPercent = 0;
            simulation.Execute(new AttackCommand(enemy.Id, TestData.Combat()));

            Assert.That(world.Player.Hp, Is.LessThan(world.Player.MaxHp), "the woken rifleman shot back in the same enemy turn");
        }
    }

    public class OverwatchTests
    {
        /// <summary>한 줄 복도 (0,0) ~ (24,0). 전투 상태를 직접 만든다.</summary>
        private static (Simulation simulation, World world, Unit enemy) Corridor(int playerX, int enemyX)
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(new string('.', 25)) };
            var combat = TestData.Combat();
            var player = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(playerX, 0));
            world.PlayerId = player.Id;
            player.CritPercent = 0;
            player.VisionRange = 30;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(enemyX, 0));
            enemy.CritPercent = 0;
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            world.Round = 1;
            world.Combatants.Add(enemy.Id);
            world.Explored = Enumerable.Repeat(true, 25).ToArray();
            return (new Simulation(world, combat), world, enemy);
        }

        [Test]
        public void EnemyThatCannotShoot_SetsOverwatch_AndFiresWhenThePlayerMovesIn()
        {
            var (simulation, world, enemy) = Corridor(0, 15);
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            simulation.Execute(new EndTurnCommand(TestData.Combat()));
            Assert.That(enemy.Position.X, Is.EqualTo(9), "moved 6 closer, still out of range 7");
            Assert.That(enemy.Overwatching, Is.True);
            Assert.That(world.Player.Hp, Is.EqualTo(world.Player.MaxHp));

            events.Clear();
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(4, 0), TestData.Combat())), Is.True);
            var reaction = events.OfType<OverwatchTriggered>().Single();
            Assert.That(reaction.WatcherId, Is.EqualTo(enemy.Id));
            Assert.That(world.Player.Hp, Is.EqualTo(world.Player.MaxHp - 4));
            Assert.That(enemy.Overwatching, Is.False, "overwatch fires once");
            Assert.That(world.Player.Position, Is.EqualTo(new GridPos(4, 0)), "the move still finishes when not downed");
        }

        [Test]
        public void PlayerOverwatch_HitsAnEnemyWalkingIntoRange()
        {
            var (simulation, world, enemy) = Corridor(0, 13);
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            Assert.That(simulation.Execute(new OverwatchCommand(TestData.Combat())), Is.True);

            var reaction = events.OfType<OverwatchTriggered>().Single();
            Assert.That(reaction.WatcherId, Is.EqualTo(world.PlayerId));
            Assert.That(enemy.Hp, Is.EqualTo(enemy.MaxHp - 8), "reaction at distance 8, no cover");
            Assert.That(world.Player.Overwatching, Is.False, "cleared when fired, and at the next player turn anyway");
            Assert.That(world.Round, Is.EqualTo(2));
        }

        [Test]
        public void PlayerOverwatch_KillingTheMover_StopsItsTurn()
        {
            var (simulation, world, enemy) = Corridor(0, 13);
            enemy.Hp = 1;

            Assert.That(simulation.Execute(new OverwatchCommand(TestData.Combat())), Is.True);
            Assert.That(world.FindUnit(enemy.Id), Is.Null);
            Assert.That(world.Player.Hp, Is.EqualTo(world.Player.MaxHp));
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
        }

        [Test]
        public void Overwatch_IsAnAction_SoItCannotFollowAShot()
        {
            var (simulation, world, _) = Corridor(0, 13);
            world.PlayerActed = true;
            Assert.That(simulation.Execute(new OverwatchCommand(TestData.Combat())), Is.False);
        }

        [Test]
        public void Snapshot_KeepsGuardAndOverwatchFlags()
        {
            var (_, world, enemy) = Corridor(0, 13);
            enemy.Guarding = true;
            world.Player.Overwatching = true;

            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.FindUnit(enemy.Id).Guarding, Is.True);
            Assert.That(restored.Player.Overwatching, Is.True);
        }
    }

    public class EnemyPositioningTests
    {
        [Test]
        public void WithEqualCover_TheRiflemanKeepsItsDistance()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1))
            {
                Map = TestData.Parse(
                    "#################",
                    "#...............#",
                    "#...............#",
                    "#...............#",
                    "#################"),
            };
            var combat = TestData.Combat();
            var player = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(2, 2));
            world.PlayerId = player.Id;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(4, 2));
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            world.Round = 1;
            world.Combatants.Add(enemy.Id);
            var simulation = new Simulation(world, combat);

            simulation.Execute(new EndTurnCommand(combat));

            int dx = enemy.Position.X - player.Position.X;
            int dy = enemy.Position.Y - player.Position.Y;
            Assert.That(dx * dx + dy * dy, Is.GreaterThan(4 * 4), $"rifleman stood at {enemy.Position}");
            Assert.That(dx * dx + dy * dy, Is.LessThanOrEqualTo(7 * 7), "still within its range");
        }
    }
}
