using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    public class VisionTests
    {
        // 방 (1,1) ~ (6,4), 가운데 기둥 줄 x = 4, 오른쪽에 복도 (7,2) ~ (10,2).
        private static readonly string[] RoomRows =
        {
            "############",
            "#....P######",
            "#....P......",
            "#....P######",
            "#....P######",
            "############",
        };

        private static FloorMap RoomMap() => TestData.ParseWithRooms(new[] { new RoomRect(1, 1, 6, 4) }, RoomRows);

        [Test]
        public void InsideARoom_TheWholeRoomAndItsWallsAreVisible()
        {
            var map = RoomMap();
            var visible = Vision.Compute(map, new GridPos(1, 1), 3);

            for (int y = 0; y <= 5; y++)
                for (int x = 0; x <= 7; x++)
                    Assert.That(Vision.IsSet(visible, map, new GridPos(x, y)), Is.True, $"({x},{y})");
        }

        [Test]
        public void InACorridor_OnlyLineOfSightWithinRangeIsVisible()
        {
            var map = RoomMap();
            var visible = Vision.Compute(map, new GridPos(9, 2), 3);

            Assert.That(Vision.IsSet(visible, map, new GridPos(7, 2)), Is.True);
            Assert.That(Vision.IsSet(visible, map, new GridPos(6, 2)), Is.True, "distance 3 is in range");
            Assert.That(Vision.IsSet(visible, map, new GridPos(5, 2)), Is.False, "distance 4 is out of range");
            Assert.That(Vision.IsSet(visible, map, new GridPos(1, 1)), Is.False);
        }

        [Test]
        public void StandingCell_IsAlwaysVisible()
        {
            var map = RoomMap();
            Assert.That(Vision.IsSet(Vision.Compute(map, new GridPos(9, 2), 0), map, new GridPos(9, 2)), Is.True);
        }

        [Test]
        public void EnteringARoom_ShowsAnEnemyBehindAPillar_AndStartsCombat()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = RoomMap() };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(7, 2)).Id;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(1, 4));
            var simulation = new Simulation(world);

            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(8, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration), "pillars hide the enemy from the corridor");

            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(7, 2), 1)), Is.True);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(6, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat), "the whole room is visible once inside");
            CollectionAssert.AreEqual(new[] { enemy.Id }, world.Combatants);
        }

        [Test]
        public void Explored_GrowsAndIsNeverForgotten()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = RoomMap() };
            world.PlayerId = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Player), new GridPos(2, 2)).Id;
            var simulation = new Simulation(world);
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(3, 2), 1));
            int roomCells = world.Explored.Count(cell => cell);
            var latest = events.OfType<VisibilityChanged>().Last();
            Assert.That(latest.Explored.Count(cell => cell), Is.EqualTo(roomCells));

            for (int x = 4; x <= 10; x++)
            {
                if (!world.Map.IsWalkable(new GridPos(x, 2))) continue;
                simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(x, 2), 1));
            }
            Assert.That(world.Explored.Count(cell => cell), Is.GreaterThanOrEqualTo(roomCells));
            Assert.That(Vision.IsSet(world.Explored, world.Map, new GridPos(1, 1)), Is.True, "room stays explored after leaving");
        }

        [Test]
        public void NewRun_StartsWithTheStartRoomExplored_AndNoEnemyInIt()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var world = TestData.NewRun(seed, 6);
                var start = world.Map.StartCell;
                Assert.That(Vision.IsSet(world.Explored, world.Map, start), Is.True, $"seed {seed}");
                Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration), $"seed {seed}");

                foreach (var room in world.Map.Rooms.Where(r => r.Contains(start)))
                    foreach (var enemy in world.Units.Where(u => !u.IsPlayer))
                        Assert.That(room.Contains(enemy.Position), Is.False, $"seed {seed} enemy in start room");
            }
        }

        [Test]
        public void Snapshot_KeepsExploredCells()
        {
            var world = TestData.NewRun(5);
            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            CollectionAssert.AreEqual(world.Explored, restored.Explored);
        }
    }
}
