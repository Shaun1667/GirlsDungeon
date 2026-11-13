using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14c 보급고: 열린 특수 방 · 가운데 보급 선반 · 재료 6 + 구급상자 2 (BALANCE 22번).</summary>
    public class StorehouseTests
    {
        private static World RackWorld()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse("......", "......", "......") };
            world.Map.SetTile(new GridPos(2, 1), TileType.StorehouseRack);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            world.FloorKeyGiven = true;
            Loadout.Apply(world, combat);
            return world;
        }

        [Test]
        public void Rack_GivesMaterialsAndMedkits_Once()
        {
            var combat = TestData.Combat();
            var world = RackWorld();
            world.PartMaterials = 1;
            var events = new List<GameEvent>();
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 1), events), Is.True);
            Assert.That(world.PartMaterials, Is.EqualTo(1 + combat.StorehouseMaterials));
            Assert.That(world.Items.Count(i => i.Kind == ItemKind.Medkit), Is.EqualTo(combat.StorehouseMedkits));
            Assert.That(world.Items.Count, Is.EqualTo(combat.StorehouseMedkits), "nothing else");
            Assert.That(events.OfType<StorehouseOpened>().Single().Gained, Is.EqualTo(combat.StorehouseMaterials));
            Assert.That(world.Map.Get(new GridPos(2, 1)), Is.EqualTo(TileType.StorehouseRackOpened));
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 1), new List<GameEvent>()), Is.False, "opens once");
        }

        [Test]
        public void Rack_BlocksTheCell_GivesLowCover_AndIsClickableLikeACrate()
        {
            var world = RackWorld();
            var rack = new GridPos(2, 1);
            Assert.That(world.Map.IsWalkable(rack), Is.False);
            Assert.That(world.Map.BlocksSight(rack), Is.False);
            Assert.That(world.Map.IsClosedChest(rack), Is.True);
            Assert.That(Cover.InDirection(world.Map, new GridPos(1, 1), new GridPos(1, 0)), Is.EqualTo(CoverLevel.Half));
        }

        [Test]
        public void TheGame_PutsAnOpenStorehouseWithARack_WhenThereIsRoom()
        {
            int floorsWithStorehouse = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                var indices = Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.Storehouse).ToList();
                Assert.That(indices.Count, Is.LessThanOrEqualTo(1), $"seed {seed}");
                if (indices.Count == 0) continue;
                floorsWithStorehouse++;
                var room = map.Rooms[indices[0]];
                Assert.That(FloorDecor.Entrances(map, room).Any(map.IsLockedDoor), Is.False, $"seed {seed}: storehouse is open");
                int racks = 0;
                for (int y = room.Y; y < room.Y + room.Height; y++)
                    for (int x = room.X; x < room.X + room.Width; x++)
                        if (map.Get(new GridPos(x, y)) == TileType.StorehouseRack) racks++;
                Assert.That(racks, Is.EqualTo(1), $"seed {seed}");
            }
            // 방이 6 ~ 8개라 작업방 · 보물방 · 일반 방 2개를 빼고 남을 때만 나온다.
            Assert.That(floorsWithStorehouse, Is.GreaterThan(0));
        }
    }
}
