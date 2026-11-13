using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14d 수리실: 열린 특수 방 · 가운데 수리대 · 체력 100% + 안정지수 가득 · 한 번만 · 가득이면 쓰지 않음 (BALANCE 23번).</summary>
    public class RepairTests
    {
        private static World StationWorld()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse("......", "......", "......") };
            world.Map.SetTile(new GridPos(2, 1), TileType.RepairStation);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            Loadout.Apply(world, combat);
            return world;
        }

        [Test]
        public void Station_FillsHpAndStability_Once()
        {
            var combat = TestData.Combat();
            var world = StationWorld();
            var player = world.Player;
            player.Hp = 10;
            player.Stability = 0;
            var events = new List<GameEvent>();
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 1), events), Is.True);
            Assert.That(player.Hp, Is.EqualTo(player.MaxHp));
            Assert.That(player.Stability, Is.EqualTo(player.MaxStability));
            var used = events.OfType<RepairUsed>().Single();
            Assert.That(used.Healed, Is.EqualTo(player.MaxHp - 10));
            Assert.That(world.Map.Get(new GridPos(2, 1)), Is.EqualTo(TileType.RepairStationUsed));
            Assert.That(world.Items, Is.Empty, "drops nothing");

            player.Hp = 1;
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 1), new List<GameEvent>()), Is.False, "once per station");
            Assert.That(player.Hp, Is.EqualTo(1));
        }

        [Test]
        public void Station_IsNotWasted_WhenHpAndStabilityAreFull()
        {
            var combat = TestData.Combat();
            var world = StationWorld();
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 1), new List<GameEvent>()), Is.False);
            Assert.That(world.Map.Get(new GridPos(2, 1)), Is.EqualTo(TileType.RepairStation), "still ready");
            world.Player.Stability = 0;
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 1), new List<GameEvent>()), Is.True, "stability alone is enough");
        }

        [Test]
        public void Station_BlocksTheCell_GivesLowCover_AndIsClickable()
        {
            var world = StationWorld();
            var station = new GridPos(2, 1);
            Assert.That(world.Map.IsWalkable(station), Is.False);
            Assert.That(world.Map.BlocksSight(station), Is.False);
            Assert.That(world.Map.IsClosedChest(station), Is.True);
            Assert.That(Cover.InDirection(world.Map, new GridPos(1, 1), new GridPos(1, 0)), Is.EqualTo(CoverLevel.Half));
        }

        [Test]
        public void TheGame_PutsOpenRepairBaysWithOneStation()
        {
            int floors = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                foreach (var index in Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.Repair))
                {
                    floors++;
                    var room = map.Rooms[index];
                    Assert.That(FloorDecor.Entrances(map, room).Any(map.IsLockedDoor), Is.False, $"seed {seed}: repair bay is open");
                    int stations = 0;
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        for (int x = room.X; x < room.X + room.Width; x++)
                            if (map.Get(new GridPos(x, y)) == TileType.RepairStation) stations++;
                    Assert.That(stations, Is.EqualTo(1), $"seed {seed}");
                }
            }
            Assert.That(floors, Is.GreaterThan(10), "two of the random pool on each floor");
        }
    }
}
