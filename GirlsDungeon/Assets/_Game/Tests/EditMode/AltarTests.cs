using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14h 제단: 잠긴 특수 방 · 거래 3개(표에서 서로 다르게) 중 하나 · 대가와 보상이 영구 효과로 · 거절 가능 (BALANCE 27번).</summary>
    public class AltarTests
    {
        private static World AltarWorld(int seed = 1)
        {
            var combat = TestData.Combat();
            var world = new World(seed, new RandomStreams(seed)) { Map = TestData.Parse("......", "......", "......") };
            world.Map.SetTile(new GridPos(2, 1), TileType.Altar);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            Loadout.Apply(world, combat);
            return world;
        }

        /// <summary>거래 표에서 그 줄(0부터)이 진열된 자리. 없으면 -1.</summary>
        private static int OfferOf(World world, int row) => world.AltarOffers.IndexOf(row);

        [Test]
        public void Open_OffersThreeDifferentTrades_AndKeepsThemUntilTaken()
        {
            var combat = TestData.Combat();
            for (int seed = 1; seed <= 30; seed++)
            {
                var world = AltarWorld(seed);
                Assert.That(Altar.Open(world, combat, new List<GameEvent>()), Is.True);
                Assert.That(world.AltarOffers.Count, Is.EqualTo(3));
                Assert.That(world.AltarOffers.Distinct().Count(), Is.EqualTo(3));
                Assert.That(world.AltarOffers.All(row => row >= 0 && row < combat.AltarTrades.Count), Is.True);
                var first = world.AltarOffers.ToArray();
                Altar.Open(world, combat, new List<GameEvent>());
                Assert.That(world.AltarOffers, Is.EqualTo(first), "opening again does not reroll");
            }
        }

        [Test]
        public void Accept_PaysThePrice_GainsTheBoon_AndTheAltarGoesOut()
        {
            var combat = TestData.Combat();
            // 체력 −10 → 공격 +2 (표 0번)가 나오는 시드를 찾는다.
            for (int seed = 1; seed <= 60; seed++)
            {
                var world = AltarWorld(seed);
                Altar.Open(world, combat, new List<GameEvent>());
                int index = OfferOf(world, 0);
                if (index < 0) continue;
                var player = world.Player;
                int maxHp = player.MaxHp, attack = player.Attack;
                var events = new List<GameEvent>();
                Assert.That(Altar.Accept(world, combat, index, events), Is.True);
                Assert.That(player.MaxHp, Is.EqualTo(maxHp - 10));
                Assert.That(player.Hp, Is.LessThanOrEqualTo(player.MaxHp));
                Assert.That(player.Attack, Is.EqualTo(attack + 2));
                Assert.That(world.Map.Get(new GridPos(2, 1)), Is.EqualTo(TileType.AltarUsed));
                Assert.That(world.AltarOffers, Is.Empty);
                var traded = events.OfType<AltarTraded>().Single();
                Assert.That((traded.CostStat, traded.CostAmount, traded.BoonStat, traded.BoonAmount), Is.EqualTo((PartStat.MaxHp, 10, PartStat.Attack, 2)));
                Assert.That(Altar.Open(world, combat, new List<GameEvent>()), Is.False, "the altar is spent");

                // 영구 효과: 장비를 다시 맞춰도(무기 교체 · 파츠) 그대로 남는다.
                Loadout.Apply(world, combat);
                Assert.That(player.Attack, Is.EqualTo(attack + 2));
                Assert.That(player.MaxHp, Is.EqualTo(maxHp - 10));
                return;
            }
            Assert.Fail("no seed offered trade 0");
        }

        [Test]
        public void CannotPayAPriceThatWouldDropAStatBelowItsMinimum()
        {
            var combat = TestData.Combat();
            for (int seed = 1; seed <= 60; seed++)
            {
                var world = AltarWorld(seed);
                Altar.Open(world, combat, new List<GameEvent>());
                int index = OfferOf(world, 1); // 방어 −1 → 치명 +15
                if (index < 0) continue;
                world.Player.Defense = 0;
                Assert.That(Altar.CanAccept(world, combat, index), Is.False);
                Assert.That(Altar.Accept(world, combat, index, new List<GameEvent>()), Is.False);
                Assert.That(world.AltarBonuses, Is.Empty);
                Assert.That(world.Map.Get(new GridPos(2, 1)), Is.EqualTo(TileType.Altar));
                return;
            }
            Assert.Fail("no seed offered trade 1");
        }

        [Test]
        public void OnlyNextToTheAltar_AndOnlyWhileExploring()
        {
            var combat = TestData.Combat();
            var world = AltarWorld();
            world.Player.Position = new GridPos(5, 2);
            Assert.That(Altar.Open(world, combat, new List<GameEvent>()), Is.False);
            world.Player.Position = new GridPos(1, 1);
            Assert.That(Altar.Open(world, combat, new List<GameEvent>()), Is.True);
            world.Mode = WorldMode.Combat;
            Assert.That(Altar.Accept(world, combat, 0, new List<GameEvent>()), Is.False);
            Assert.That(world.AltarBonuses, Is.Empty);
        }

        [Test]
        public void Bonuses_LastAcrossFloors_OffersReset_AndTheSnapshotKeepsBoth()
        {
            var combat = TestData.Combat();
            var world = RunFactory.NewRun(8, TestData.Map(), combat);
            world.AltarOffers.Add(2);
            world.AltarBonuses.Add(new PartBonus(PartStat.MoveRange, -1));
            world.AltarBonuses.Add(new PartBonus(PartStat.AttackRange, 2));
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.AltarOffers, Is.EqualTo(new[] { 2 }));
            Assert.That(restored.AltarBonuses.Select(b => (b.Stat, b.Value)), Is.EqualTo(new[] { (PartStat.MoveRange, -1), (PartStat.AttackRange, 2) }));

            RunFactory.NextFloor(world, TestData.Map(), combat, new List<GameEvent>());
            Assert.That(world.AltarOffers, Is.Empty);
            Assert.That(world.AltarBonuses.Count, Is.EqualTo(2), "permanent for the run");
        }

        [Test]
        public void TheGame_PutsMostlyLockedAltarsWithOneAltarTile()
        {
            int floors = 0, locked = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                foreach (var index in Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.Altar))
                {
                    floors++;
                    var room = map.Rooms[index];
                    if (FloorDecor.Entrances(map, room).Any(map.IsLockedDoor)) locked++;
                    int altars = 0;
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        for (int x = room.X; x < room.X + room.Width; x++)
                            if (map.Get(new GridPos(x, y)) == TileType.Altar) altars++;
                    Assert.That(altars, Is.EqualTo(1), $"seed {seed}");
                }
            }
            Assert.That(floors, Is.GreaterThan(5));
            Assert.That(locked * 2, Is.GreaterThanOrEqualTo(floors), "locked room — usually locked");
        }
    }
}
