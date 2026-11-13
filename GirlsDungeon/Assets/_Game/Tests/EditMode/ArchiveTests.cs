using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14l 기록실: 열린 방 · 기록 단말 1개 · 다음 층의 특수 방 · 적 수 예고 = 실제 다음 층.</summary>
    public class ArchiveTests
    {
        private static List<SpecialRoomKind> RoomsOf(FloorMap map) =>
            Enumerable.Range(0, map.Rooms.Count).Select(map.RoomKind).Where(k => k != SpecialRoomKind.None).ToList();

        [Test]
        public void UsingTheTerminal_MarksTheFloorRead_Once()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse("....", "....") };
            world.Map.SetTile(new GridPos(2, 0), TileType.ArchiveTerminal);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 0)).Id;
            var events = new List<GameEvent>();
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 0), events), Is.True);
            Assert.That(world.ArchiveRead, Is.True);
            Assert.That(world.Map.Get(new GridPos(2, 0)), Is.EqualTo(TileType.ArchiveTerminalUsed));
            Assert.That(events.OfType<ArchiveUsed>().Count(), Is.EqualTo(1));
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 0), new List<GameEvent>()), Is.False);
        }

        [Test]
        public void Forecast_MatchesTheRealNextFloor_EvenAfterPlayingThisFloor()
        {
            var combat = TestData.Combat(4);
            var map = TestData.Map();
            for (int seed = 1; seed <= 20; seed++)
            {
                var world = RunFactory.NewRun(seed, map, combat);
                var forecast = Archive.Forecast(world, map, combat);
                Assert.That(forecast.LastFloor, Is.False);

                // 이 층에서 다른 난수를 쓰는 일을 한다: 보급 상자 열기(아이템 · 문), 명중 굴림(전투).
                for (int y = 0; y < world.Map.Height; y++)
                    for (int x = 0; x < world.Map.Width; x++)
                        if (world.Map.Get(new GridPos(x, y)) == TileType.SupplyCrate)
                            Loot.OpenCrate(world, combat, new GridPos(x, y), new List<GameEvent>());
                world.Random.Get(RandomPurpose.Combat).Range(0, 100);
                world.Random.Get(RandomPurpose.Workshop).Range(0, 100);

                RunFactory.NextFloor(world, map, combat, null);
                Assert.That(RoomsOf(world.Map), Is.EqualTo(forecast.Rooms), $"seed {seed}: rooms");
                Assert.That(world.Units.Count(u => !u.IsPlayer), Is.EqualTo(forecast.Enemies), $"seed {seed}: enemies");
                Assert.That(forecast.Rooms, Has.Member(SpecialRoomKind.Workshop).And.Member(SpecialRoomKind.Treasure));
            }
        }

        [Test]
        public void Forecast_DoesNotChangeTheWorld_AndKnowsTheLastFloor()
        {
            var combat = TestData.Combat(4);
            var map = TestData.Map();
            var world = RunFactory.NewRun(3, map, combat);
            var before = WorldSnapshot.Encode(world);
            Archive.Forecast(world, map, combat);
            Assert.That(WorldSnapshot.Encode(world), Is.EqualTo(before));

            while (world.FloorIndex + 1 < combat.FloorCount) RunFactory.NextFloor(world, map, combat, null);
            Assert.That(Archive.Forecast(world, map, combat).LastFloor, Is.True);
        }

        [Test]
        public void ArchiveRead_IsSaved_AndResetOnANewFloor()
        {
            var combat = TestData.Combat();
            var world = RunFactory.NewRun(5, TestData.Map(), combat);
            world.ArchiveRead = true;
            var bytes = WorldSnapshot.Encode(world);
            Assert.That(WorldSnapshot.Decode(bytes).ArchiveRead, Is.True);
            Assert.That(WorldSnapshot.Encode(WorldSnapshot.Decode(bytes)), Is.EqualTo(bytes));
            RunFactory.NextFloor(world, TestData.Map(), combat, null);
            Assert.That(world.ArchiveRead, Is.False);
        }

        [Test]
        public void TheGame_PutsOpenArchivesWithOneReachableTerminal()
        {
            int floors = 0;
            for (int seed = 1; seed <= 200 && floors < 8; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                foreach (var index in Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.Archive))
                {
                    floors++;
                    var room = map.Rooms[index];
                    Assert.That(FloorDecor.Entrances(map, room).Any(map.IsLockedDoor), Is.False, $"seed {seed}: open room");
                    var terminals = new List<GridPos>();
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        for (int x = room.X; x < room.X + room.Width; x++)
                            if (map.Get(new GridPos(x, y)) == TileType.ArchiveTerminal) terminals.Add(new GridPos(x, y));
                    Assert.That(terminals.Count, Is.EqualTo(1), $"seed {seed}");
                    var t = terminals[0];
                    bool reachable = false;
                    for (int dy = -1; dy <= 1 && !reachable; dy++)
                        for (int dx = -1; dx <= 1 && !reachable; dx++)
                        {
                            var near = new GridPos(t.X + dx, t.Y + dy);
                            if (map.IsWalkable(near) && GridNavigation.FindPath(map, map.StartCell, near) != null) reachable = true;
                        }
                    Assert.That(reachable, Is.True, $"seed {seed}");
                }
            }
            Assert.That(floors, Is.GreaterThanOrEqualTo(8));
        }
    }
}
