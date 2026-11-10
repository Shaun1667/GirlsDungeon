using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    public class FloorDecorTests
    {
        private const int SeedCount = 300;

        [Test]
        public void Decor_DoesNotChangeTheMapShape()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var bare = DungeonGenerator.Generate(TestData.Map(), new RandomStreams(seed).Get(RandomPurpose.Map));
                var decorated = RunFactory.NewRun(seed, TestData.Map()).Map;

                for (int y = 0; y < bare.Height; y++)
                    for (int x = 0; x < bare.Width; x++)
                    {
                        var cell = new GridPos(x, y);
                        Assert.That(decorated.Get(cell) == TileType.Wall, Is.EqualTo(bare.Get(cell) == TileType.Wall), $"seed {seed} {cell}");
                    }
            }
        }

        [Test]
        public void CoverObjects_StayInsideRooms_AwayFromStartAndStairs()
        {
            var settings = TestData.Map();
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = RunFactory.NewRun(seed, settings).Map;
                int objects = map.CountTiles(TileType.Crate) + map.CountTiles(TileType.Pillar);
                Assert.That(objects, Is.LessThanOrEqualTo(map.Rooms.Count * settings.MaxCoverObjectsPerRoom), $"seed {seed}");
                Assert.That(objects, Is.GreaterThan(0), $"seed {seed}");

                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        var cell = new GridPos(x, y);
                        var tile = map.Get(cell);
                        if (tile != TileType.Crate && tile != TileType.Pillar) continue;

                        Assert.That(System.Math.Max(System.Math.Abs(cell.X - map.StartCell.X), System.Math.Abs(cell.Y - map.StartCell.Y)),
                            Is.GreaterThan(1), $"seed {seed} {cell}");
                        Assert.That(System.Math.Max(System.Math.Abs(cell.X - map.StairsCell.X), System.Math.Abs(cell.Y - map.StairsCell.Y)),
                            Is.GreaterThan(1), $"seed {seed} {cell}");
                    }
                }
            }
        }

        [Test]
        public void LowWalls_OnlyStandBetweenWalkableCells()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = RunFactory.NewRun(seed, TestData.Map()).Map;
                Assert.That(map.CountLowWalls(), Is.GreaterThan(0), $"seed {seed}");

                for (int y = 0; y < map.Height; y++)
                {
                    for (int x = 0; x < map.Width; x++)
                    {
                        var cell = new GridPos(x, y);
                        if (map.HasLowWallEast(cell))
                            Assert.That(map.IsWalkable(cell) && map.IsWalkable(new GridPos(x + 1, y)), Is.True, $"seed {seed} {cell} east");
                        if (map.HasLowWallNorth(cell))
                            Assert.That(map.IsWalkable(cell) && map.IsWalkable(new GridPos(x, y + 1)), Is.True, $"seed {seed} {cell} north");
                    }
                }
            }
        }

        [Test]
        public void LowWall_IsReadFromBothSides_AndCanBeCrossed()
        {
            var map = TestData.Parse("...", "...");
            var a = new GridPos(0, 0);
            var b = new GridPos(1, 0);
            Assert.That(map.SetLowWallBetween(b, a, true), Is.True);

            Assert.That(map.HasLowWallBetween(a, b), Is.True);
            Assert.That(map.HasLowWallBetween(b, a), Is.True);
            Assert.That(map.HasLowWallEast(a), Is.True);
            Assert.That(GridNavigation.CanStep(map, a, b), Is.True);
            Assert.That(map.SetLowWallBetween(a, new GridPos(1, 1), true), Is.False);
        }
    }

    public class SightTests
    {
        [Test]
        public void OpenRoom_HasLine()
        {
            var map = TestData.Parse(".....", ".....", ".....");
            Assert.That(Sight.HasLine(map, new GridPos(0, 0), new GridPos(4, 2)), Is.True);
        }

        [Test]
        public void WallAndPillar_BlockTheLine_CrateDoesNot()
        {
            Assert.That(Sight.HasLine(TestData.Parse("..#.."), new GridPos(0, 0), new GridPos(4, 0)), Is.False);
            Assert.That(Sight.HasLine(TestData.Parse("..P.."), new GridPos(0, 0), new GridPos(4, 0)), Is.False);
            Assert.That(Sight.HasLine(TestData.Parse("..C.."), new GridPos(0, 0), new GridPos(4, 0)), Is.True);
        }

        [Test]
        public void LowWall_DoesNotBlockTheLine()
        {
            var map = TestData.Parse(".....");
            map.SetLowWallBetween(new GridPos(1, 0), new GridPos(2, 0), true);
            Assert.That(Sight.HasLine(map, new GridPos(0, 0), new GridPos(4, 0)), Is.True);
        }

        [Test]
        public void ExactCorner_IsBlockedOnlyWhenBothSidesBlock()
        {
            var oneSide = TestData.Parse(".#", "..");
            Assert.That(Sight.HasLine(oneSide, new GridPos(0, 0), new GridPos(1, 1)), Is.True);

            var bothSides = TestData.Parse(".#", "#.");
            Assert.That(Sight.HasLine(bothSides, new GridPos(0, 0), new GridPos(1, 1)), Is.False);
        }

        [Test]
        public void Line_IsTheSameInBothDirections()
        {
            var map = TestData.Parse(
                "......",
                "..P...",
                "....#.",
                "......");
            for (int y1 = 0; y1 < 4; y1++)
                for (int x1 = 0; x1 < 6; x1++)
                    for (int y2 = 0; y2 < 4; y2++)
                        for (int x2 = 0; x2 < 6; x2++)
                        {
                            var a = new GridPos(x1, y1);
                            var b = new GridPos(x2, y2);
                            Assert.That(Sight.HasLine(map, a, b), Is.EqualTo(Sight.HasLine(map, b, a)), $"{a} {b}");
                        }
        }

        [Test]
        public void CanSee_RespectsRange()
        {
            var map = TestData.Parse("..........");
            Assert.That(Sight.CanSee(map, new GridPos(0, 0), new GridPos(5, 0), 5), Is.True);
            Assert.That(Sight.CanSee(map, new GridPos(0, 0), new GridPos(6, 0), 5), Is.False);
        }
    }

    public class CoverTests
    {
        [Test]
        public void CrateInFront_GivesHalfCover()
        {
            var map = TestData.Parse(".C...");
            Assert.That(Cover.Against(map, new GridPos(0, 0), new GridPos(4, 0)), Is.EqualTo(CoverLevel.Half));
        }

        [Test]
        public void PillarOrWallInFront_GivesFullCover()
        {
            Assert.That(Cover.Against(TestData.Parse(".P...", "....."), new GridPos(0, 1), new GridPos(4, 0)), Is.EqualTo(CoverLevel.None));
            Assert.That(Cover.Against(TestData.Parse(".....", ".P..."), new GridPos(0, 1), new GridPos(4, 0)), Is.EqualTo(CoverLevel.Full));
            Assert.That(Cover.Against(TestData.Parse("#...."), new GridPos(1, 0), new GridPos(0, 0)), Is.EqualTo(CoverLevel.Full));
        }

        [Test]
        public void LowWallInFront_GivesHalfCover()
        {
            var map = TestData.Parse(".....");
            map.SetLowWallBetween(new GridPos(1, 0), new GridPos(2, 0), true);
            Assert.That(Cover.Against(map, new GridPos(1, 0), new GridPos(4, 0)), Is.EqualTo(CoverLevel.Half));
            Assert.That(Cover.Against(map, new GridPos(2, 0), new GridPos(0, 0)), Is.EqualTo(CoverLevel.Half));
        }

        [Test]
        public void AttackerBehindOrBesideTheCover_FlanksIt()
        {
            var map = TestData.Parse(
                ".....",
                ".C...",
                ".....");
            var target = new GridPos(0, 1);
            Assert.That(Cover.Against(map, target, new GridPos(4, 1)), Is.EqualTo(CoverLevel.Half));
            Assert.That(Cover.Against(map, target, new GridPos(0, 2)), Is.EqualTo(CoverLevel.None));
        }

        [Test]
        public void BestCoverFacingTheAttacker_Wins()
        {
            var map = TestData.Parse(
                ".P...",
                "C....");
            var target = new GridPos(0, 0);
            map.SetTile(new GridPos(0, 1), TileType.Crate);
            Assert.That(Cover.Against(map, target, new GridPos(3, 1)), Is.EqualTo(CoverLevel.Full));
        }
    }

    public class SnapshotLowWallTests
    {
        [Test]
        public void RoundTrip_KeepsLowWalls()
        {
            var world = RunFactory.NewRun(21, TestData.Map());
            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.Map.CountLowWalls(), Is.EqualTo(world.Map.CountLowWalls()));
            Assert.That(restored.Map.CountTiles(TileType.Pillar), Is.EqualTo(world.Map.CountTiles(TileType.Pillar)));
        }
    }
}
