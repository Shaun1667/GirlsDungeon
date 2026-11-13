using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14f 교환소: 교환대 옆에서 · 물건마다 층에서 한 번 · 구급상자 2 · 군용 파츠 4 · 열쇠 5 · 열쇠 팔기 3 (BALANCE 25번).</summary>
    public class ExchangeTests
    {
        private static World CounterWorld()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse("......", "......", "......") };
            world.Map.SetTile(new GridPos(2, 1), TileType.ExchangeCounter);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            Loadout.Apply(world, combat);
            return world;
        }

        [Test]
        public void EachOffer_CostsItsPrice_AndSellsOnce()
        {
            var combat = TestData.Combat();
            var world = CounterWorld();
            world.PartMaterials = 20;
            world.Keys = 0;

            Assert.That(Exchange.Trade(world, combat, ExchangeOffer.Medkit, new List<GameEvent>()), Is.True);
            Assert.That(world.PartMaterials, Is.EqualTo(18));
            Assert.That(world.Items.Count(i => i.Kind == ItemKind.Medkit), Is.EqualTo(1));

            Assert.That(Exchange.Trade(world, combat, ExchangeOffer.Part, new List<GameEvent>()), Is.True);
            Assert.That(world.PartMaterials, Is.EqualTo(14));
            var part = world.Items.Single(i => i.Kind == ItemKind.Part).Part;
            Assert.That(part.Grade, Is.EqualTo(PartGrade.Military));
            Assert.That(part.Weapon, Is.EqualTo(world.PlayerWeapon));

            Assert.That(Exchange.Trade(world, combat, ExchangeOffer.SellKey, new List<GameEvent>()), Is.False, "no key to sell");
            Assert.That(Exchange.Trade(world, combat, ExchangeOffer.Key, new List<GameEvent>()), Is.True);
            Assert.That(world.PartMaterials, Is.EqualTo(9));
            Assert.That(world.Keys, Is.EqualTo(1));
            Assert.That(Exchange.Trade(world, combat, ExchangeOffer.SellKey, new List<GameEvent>()), Is.True);
            Assert.That(world.Keys, Is.EqualTo(0));
            Assert.That(world.PartMaterials, Is.EqualTo(12));

            foreach (ExchangeOffer offer in System.Enum.GetValues(typeof(ExchangeOffer)))
            {
                Assert.That(Exchange.IsSold(world, offer), Is.True, offer.ToString());
                Assert.That(Exchange.Trade(world, combat, offer, new List<GameEvent>()), Is.False, $"{offer} once per floor");
            }
        }

        [Test]
        public void NeedsMaterials_TheCounter_AndExploring()
        {
            var combat = TestData.Combat();
            var world = CounterWorld();
            world.PartMaterials = 1;
            Assert.That(Exchange.Trade(world, combat, ExchangeOffer.Medkit, new List<GameEvent>()), Is.False, "not enough");
            world.PartMaterials = 10;
            world.Player.Position = new GridPos(5, 2);
            Assert.That(Exchange.AtCounter(world), Is.False);
            Assert.That(Exchange.Trade(world, combat, ExchangeOffer.Medkit, new List<GameEvent>()), Is.False, "too far");
            world.Player.Position = new GridPos(1, 1);
            world.Mode = WorldMode.Combat;
            Assert.That(Exchange.Trade(world, combat, ExchangeOffer.Medkit, new List<GameEvent>()), Is.False, "not in combat");
            Assert.That(world.PartMaterials, Is.EqualTo(10));
            Assert.That(Exchange.IsSold(world, ExchangeOffer.Medkit), Is.False);
        }

        [Test]
        public void Stock_ResetsOnTheNextFloor_AndTheSnapshotKeepsIt()
        {
            var combat = TestData.Combat();
            var world = RunFactory.NewRun(6, TestData.Map(), combat);
            world.ExchangeSold = 0b0101;
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.ExchangeSold, Is.EqualTo((byte)0b0101));
            RunFactory.NextFloor(world, TestData.Map(), combat, new List<GameEvent>());
            Assert.That(world.ExchangeSold, Is.EqualTo((byte)0));
        }

        [Test]
        public void TheGame_PutsOpenExchangesWithOneCounter()
        {
            int floors = 0;
            for (int seed = 1; seed <= 200 && floors < 8; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                foreach (var index in Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.Exchange))
                {
                    floors++;
                    var room = map.Rooms[index];
                    Assert.That(FloorDecor.Entrances(map, room).Any(map.IsLockedDoor), Is.False, $"seed {seed}: exchange is open");
                    int counters = 0;
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        for (int x = room.X; x < room.X + room.Width; x++)
                            if (map.Get(new GridPos(x, y)) == TileType.ExchangeCounter) counters++;
                    Assert.That(counters, Is.EqualTo(1), $"seed {seed}");
                }
            }
            // 무작위 후보가 늘면 40개 층의 개수는 흔들린다 — 8개 층을 찾을 때까지 본다 (3-14j).
            Assert.That(floors, Is.GreaterThanOrEqualTo(8));
        }
    }
}
