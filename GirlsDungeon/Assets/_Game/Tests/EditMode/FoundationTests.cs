using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    public class RandomStreamsTests
    {
        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new RandomStreams(42).Get(RandomPurpose.Combat);
            var b = new RandomStreams(42).Get(RandomPurpose.Combat);
            for (int i = 0; i < 100; i++) Assert.That(a.Range(0, 1000), Is.EqualTo(b.Range(0, 1000)));
        }

        [Test]
        public void Purposes_AreIndependent()
        {
            var plain = new RandomStreams(42);
            var drawn = new RandomStreams(42);
            for (int i = 0; i < 50; i++) drawn.Get(RandomPurpose.Decor).Range(0, 100);

            for (int i = 0; i < 100; i++)
                Assert.That(drawn.Get(RandomPurpose.Map).Range(0, 1000), Is.EqualTo(plain.Get(RandomPurpose.Map).Range(0, 1000)));
        }

        [Test]
        public void Range_StaysInBounds()
        {
            var random = new SeededRandom(7UL);
            for (int i = 0; i < 10000; i++) Assert.That(random.Range(-3, 4), Is.InRange(-3, 3));
        }
    }

    public class DungeonGeneratorTests
    {
        private const int SeedCount = 300;

        private static FloorMap Generate(int seed) => RunFactory.NewRun(seed, TestData.Map()).Map;

        [Test]
        public void SameSeed_GivesSameMap()
        {
            Assert.That(WorldSnapshot.Encode(RunFactory.NewRun(5, TestData.Map())),
                Is.EqualTo(WorldSnapshot.Encode(RunFactory.NewRun(5, TestData.Map()))));
        }

        [Test]
        public void EveryMap_MeetsTheRoomAndBorderRules()
        {
            var settings = TestData.Map();
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                Assert.That(map.Rooms.Count, Is.InRange(settings.MinRooms, settings.MaxRooms), $"seed {seed}");
                for (int i = 0; i < map.Width; i++)
                {
                    Assert.That(map.IsWalkable(new GridPos(i, 0)), Is.False, $"seed {seed}");
                    Assert.That(map.IsWalkable(new GridPos(i, map.Height - 1)), Is.False, $"seed {seed}");
                    Assert.That(map.IsWalkable(new GridPos(0, i)), Is.False, $"seed {seed}");
                    Assert.That(map.IsWalkable(new GridPos(map.Width - 1, i)), Is.False, $"seed {seed}");
                }
            }
        }

        [Test]
        public void EveryWalkableCell_IsReachableFromStart()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                Assert.That(CountReachable(map), Is.EqualTo(map.CountWalkable()), $"seed {seed}");
            }
        }

        [Test]
        public void Stairs_AreSeparateFromStart_AndHaveAPath()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                Assert.That(map.StairsCell, Is.Not.EqualTo(map.StartCell), $"seed {seed}");
                Assert.That(map.Get(map.StairsCell), Is.EqualTo(TileType.Stairs), $"seed {seed}");
                Assert.That(GridNavigation.FindPath(map, map.StartCell, map.StairsCell), Is.Not.Empty, $"seed {seed}");
            }
        }

        private static int CountReachable(FloorMap map)
        {
            var seen = new HashSet<GridPos> { map.StartCell };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(map.StartCell);
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var next = new GridPos(cell.X + dx, cell.Y + dy);
                        if (GridNavigation.CanStep(map, cell, next) && seen.Add(next)) frontier.Enqueue(next);
                    }
                }
            }
            return seen.Count;
        }
    }

    public class GridNavigationTests
    {
        private static FloorMap Parse(params string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var tiles = new TileType[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    tiles[x + y * width] = rows[y][x] == '#' ? TileType.Wall : TileType.Floor;
            return new FloorMap(width, height, tiles, new RoomRect[0], new GridPos(0, 0), new GridPos(0, 0));
        }

        [Test]
        public void Diagonal_IsAllowedInTheOpen()
        {
            var map = Parse("..", "..");
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), new GridPos(1, 1)), Is.True);
        }

        [Test]
        public void Diagonal_CannotCutAWallCorner()
        {
            var map = Parse(".#", "..");
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), new GridPos(1, 1)), Is.False);
        }

        [Test]
        public void CannotStepFarOrIntoWallOrInPlace()
        {
            var map = Parse("..#");
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), new GridPos(2, 0)), Is.False);
            Assert.That(GridNavigation.CanStep(map, new GridPos(1, 0), new GridPos(2, 0)), Is.False);
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), new GridPos(0, 0)), Is.False);
        }

        [Test]
        public void FindPath_PrefersDiagonalsAndExcludesStart()
        {
            var map = Parse("...", "...", "...");
            var path = GridNavigation.FindPath(map, new GridPos(0, 0), new GridPos(2, 2));
            CollectionAssert.AreEqual(new[] { new GridPos(1, 1), new GridPos(2, 2) }, path);
        }

        [Test]
        public void FindPath_ReturnsEmpty_WhenUnreachable()
        {
            var map = Parse(".#.");
            Assert.That(GridNavigation.FindPath(map, new GridPos(0, 0), new GridPos(2, 0)), Is.Empty);
        }

        [Test]
        public void FindPath_AvoidsBlockedCells()
        {
            var map = Parse("...", "...");
            var blocked = new GridPos(1, 0);
            var path = GridNavigation.FindPath(map, new GridPos(0, 0), new GridPos(2, 0), cell => cell == blocked);
            Assert.That(path, Has.No.Member(blocked));
            Assert.That(path.Last(), Is.EqualTo(new GridPos(2, 0)));
        }
    }

    public class SimulationTests
    {
        [Test]
        public void ValidMove_ChangesPositionAdvancesTickAndPublishesEvents()
        {
            var world = RunFactory.NewRun(9, TestData.Map());
            var simulation = new Simulation(world);
            var published = new List<GameEvent>();
            simulation.EventsPublished += events => published.AddRange(events);

            var start = world.Player.Position;
            var target = FindOpenNeighbor(world.Map, start);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, target, 1)), Is.True);

            Assert.That(world.Player.Position, Is.EqualTo(target));
            Assert.That(world.Tick, Is.EqualTo(1));
            Assert.That(published.OfType<UnitMoved>().Single().From, Is.EqualTo(start));
            Assert.That(published.OfType<TickAdvanced>().Single().Tick, Is.EqualTo(1));
        }

        [Test]
        public void InvalidMove_ChangesNothing()
        {
            var world = RunFactory.NewRun(9, TestData.Map());
            var simulation = new Simulation(world);
            var before = WorldSnapshot.Encode(world);
            var published = 0;
            simulation.EventsPublished += _ => published++;

            var far = new GridPos(world.Player.Position.X + 5, world.Player.Position.Y);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, far, 1)), Is.False);

            Assert.That(WorldSnapshot.Encode(world), Is.EqualTo(before));
            Assert.That(published, Is.EqualTo(0));
        }

        private static GridPos FindOpenNeighbor(FloorMap map, GridPos cell)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var next = new GridPos(cell.X + dx, cell.Y + dy);
                    if (GridNavigation.CanStep(map, cell, next)) return next;
                }
            }
            throw new AssertionException("Start cell has no open neighbor.");
        }
    }

    public class WorldSnapshotTests
    {
        [Test]
        public void RoundTrip_GivesSameBytes()
        {
            var world = RunFactory.NewRun(11, TestData.Map());
            var simulation = new Simulation(world);
            var path = GridNavigation.FindPath(world.Map, world.Player.Position, world.Map.StairsCell);
            foreach (var step in path.Take(5)) Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, step, 1)), Is.True);
            world.Random.Get(RandomPurpose.Combat).Range(0, 100);

            var bytes = WorldSnapshot.Encode(world);
            Assert.That(WorldSnapshot.Encode(WorldSnapshot.Decode(bytes)), Is.EqualTo(bytes));
        }

        [Test]
        public void RestoredRandom_ContinuesTheSameSequence()
        {
            var world = RunFactory.NewRun(11, TestData.Map());
            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));

            for (int i = 0; i < 100; i++)
                Assert.That(restored.Random.Get(RandomPurpose.Combat).Range(0, 1000),
                    Is.EqualTo(world.Random.Get(RandomPurpose.Combat).Range(0, 1000)));
        }

        [Test]
        public void Decode_RejectsUnknownVersion()
        {
            var bytes = WorldSnapshot.Encode(RunFactory.NewRun(1, TestData.Map()));
            bytes[0] = 0xFF;
            Assert.Throws<System.IO.InvalidDataException>(() => WorldSnapshot.Decode(bytes));
        }
    }

    internal static class TestData
    {
        /// <summary>BALANCE 1번과 같은 값. 실제 에셋 값은 BalanceTests가 검사한다.</summary>
        public static MapSettings Map()
        {
            return new MapSettings
            {
                Width = 32,
                Height = 32,
                MinRooms = 6,
                MaxRooms = 8,
                MinRoomSize = 7,
                MaxRoomSize = 13,
                RoomMargin = 1,
                RoomAttempts = 300,
                LayoutAttempts = 50,
                CorridorWallCost = 5,
                ExtraConnections = 2,
            };
        }
    }
}
