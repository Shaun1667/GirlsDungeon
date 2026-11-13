using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14e 관측실: 열린 특수 방 · 가운데 관측 콘솔 · 쓰면 층 지도 전체가 "본 칸" + 적 위치 표시 · 한 번만 · 다음 층에서 초기화.</summary>
    public class ObservatoryTests
    {
        [Test]
        public void Console_RevealsTheWholeFloor_AndTheEnemies_Once()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse("..........", "..........", "..........") };
            world.Map.SetTile(new GridPos(2, 1), TileType.ObservatoryConsole);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(9, 2));
            world.Explored = new bool[world.Map.Width * world.Map.Height];

            var events = new List<GameEvent>();
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 1), events), Is.True);
            Assert.That(world.Explored.All(seen => seen), Is.True);
            Assert.That(world.EnemiesRevealed, Is.True);
            Assert.That(events.OfType<MapRevealed>().Single().Enemies, Is.EqualTo(1));
            Assert.That(world.Map.Get(new GridPos(2, 1)), Is.EqualTo(TileType.ObservatoryConsoleUsed));
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 1), new List<GameEvent>()), Is.False, "once");
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration), "revealing does not start a combat");
        }

        [Test]
        public void Reveal_IsForgottenOnTheNextFloor_AndKeptBySnapshots()
        {
            var combat = TestData.Combat();
            var world = RunFactory.NewRun(4, TestData.Map(), combat);
            world.EnemiesRevealed = true;
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.EnemiesRevealed, Is.True);

            RunFactory.NextFloor(world, TestData.Map(), combat, new List<GameEvent>());
            Assert.That(world.EnemiesRevealed, Is.False);
            Assert.That(world.Explored.All(seen => seen), Is.False, "the new floor is unexplored again");
        }

        [Test]
        public void TheGame_PutsOpenObservatoriesWithOneConsole()
        {
            int floors = 0;
            for (int seed = 1; seed <= 200 && floors < 8; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                foreach (var index in Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.Observatory))
                {
                    floors++;
                    var room = map.Rooms[index];
                    Assert.That(FloorDecor.Entrances(map, room).Any(map.IsLockedDoor), Is.False, $"seed {seed}: observatory is open");
                    int consoles = 0;
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        for (int x = room.X; x < room.X + room.Width; x++)
                            if (map.Get(new GridPos(x, y)) == TileType.ObservatoryConsole) consoles++;
                    Assert.That(consoles, Is.EqualTo(1), $"seed {seed}");
                }
            }
            // 무작위 후보가 늘면 40개 층의 개수는 흔들린다 — 8개 층을 찾을 때까지 본다 (3-14j).
            Assert.That(floors, Is.GreaterThanOrEqualTo(8));
        }
    }
}
