using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14g 세트 진열실: 잠긴 특수 방 · 진열장 · 같은 세트 군용 파츠 3개(2 · 3 · 4번 칸) 중 1개 · 끼운 세트를 먼저 (BALANCE 26번).</summary>
    public class SetGalleryTests
    {
        private static World CaseWorld(int seed = 1)
        {
            var combat = TestData.Combat();
            var world = new World(seed, new RandomStreams(seed)) { Map = TestData.Parse("......", "......", "......") };
            world.Map.SetTile(new GridPos(2, 1), TileType.SetGalleryCase);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            Loadout.Apply(world, combat);
            return world;
        }

        [Test]
        public void Open_ShowsThreeSameSetPartsForTheHeldWeapon_OnePerSlot()
        {
            var combat = TestData.Combat();
            for (int seed = 1; seed <= 30; seed++)
            {
                var world = CaseWorld(seed);
                Assert.That(SetGallery.Open(world, combat, new List<GameEvent>()), Is.True);
                var offers = world.GalleryOffers;
                Assert.That(offers.Count, Is.EqualTo(3));
                Assert.That(offers.Select(p => p.Slot), Is.EqualTo(new[] { 1, 2, 3 }));
                Assert.That(offers.All(p => p.Weapon == world.PlayerWeapon), Is.True);
                Assert.That(offers.All(p => p.Grade == PartGrade.Military), Is.True);
                Assert.That(offers.Select(p => p.Set).Distinct().Count(), Is.EqualTo(1), "same set");
                Assert.That(offers[0].Set, Is.Not.EqualTo(PartSet.None));
                // 두 번 열어도 같은 파츠 (다시 굴리지 않는다).
                var first = offers[0];
                SetGallery.Open(world, combat, new List<GameEvent>());
                Assert.That(world.GalleryOffers[0], Is.SameAs(first));
            }
        }

        [Test]
        public void Open_PrefersTheSetYouAlreadyWear()
        {
            var combat = TestData.Combat();
            var world = CaseWorld();
            world.PlayerParts[1] = new WeaponPart(world.PlayerWeapon, 1, PartGrade.Military, new List<PartBonus>(), PartSet.Tactical);
            world.PlayerParts[2] = new WeaponPart(world.PlayerWeapon, 2, PartGrade.Military, new List<PartBonus>(), PartSet.Tactical);
            world.PlayerParts[3] = new WeaponPart(world.PlayerWeapon, 3, PartGrade.Military, new List<PartBonus>(), PartSet.Decisive);
            SetGallery.Open(world, combat, new List<GameEvent>());
            Assert.That(world.GalleryOffers.All(p => p.Set == PartSet.Tactical), Is.True);
        }

        [Test]
        public void Pick_DropsOnePart_EmptiesTheCase_AndOnlyOnce()
        {
            var combat = TestData.Combat();
            var world = CaseWorld();
            SetGallery.Open(world, combat, new List<GameEvent>());
            var chosen = world.GalleryOffers[1];
            var events = new List<GameEvent>();
            Assert.That(SetGallery.Pick(world, 1, events), Is.True);
            Assert.That(world.Items.Single().Part, Is.SameAs(chosen));
            Assert.That(world.GalleryOffers, Is.Empty);
            Assert.That(world.Map.Get(new GridPos(2, 1)), Is.EqualTo(TileType.SetGalleryCaseUsed));
            Assert.That(events.OfType<GalleryPicked>().Single().Slot, Is.EqualTo(chosen.Slot));
            Assert.That(SetGallery.Open(world, combat, new List<GameEvent>()), Is.False, "the case is empty");
            Assert.That(SetGallery.Pick(world, 0, new List<GameEvent>()), Is.False);
        }

        [Test]
        public void OnlyNextToTheCase_AndOnlyWhileExploring()
        {
            var combat = TestData.Combat();
            var world = CaseWorld();
            world.Player.Position = new GridPos(5, 2);
            Assert.That(SetGallery.Open(world, combat, new List<GameEvent>()), Is.False);
            world.Player.Position = new GridPos(1, 1);
            world.Mode = WorldMode.Combat;
            Assert.That(SetGallery.Open(world, combat, new List<GameEvent>()), Is.False);
            Assert.That(world.GalleryOffers, Is.Empty);
        }

        [Test]
        public void Offers_ResetOnTheNextFloor_AndTheSnapshotKeepsThem()
        {
            var combat = TestData.Combat();
            var world = RunFactory.NewRun(8, TestData.Map(), combat);
            world.GalleryOffers.Add(new WeaponPart(world.PlayerWeapon, 2, PartGrade.Military, new List<PartBonus> { new PartBonus(PartStat.Attack, 1) }, PartSet.DualCounter));
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.GalleryOffers.Single().Set, Is.EqualTo(PartSet.DualCounter));
            RunFactory.NextFloor(world, TestData.Map(), combat, new List<GameEvent>());
            Assert.That(world.GalleryOffers, Is.Empty);
        }

        [Test]
        public void TheGame_PutsMostlyLockedGalleriesWithOneCase()
        {
            int floors = 0, locked = 0;
            for (int seed = 1; seed <= 200 && floors < 8; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                foreach (var index in Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.SetGallery))
                {
                    floors++;
                    var room = map.Rooms[index];
                    if (FloorDecor.Entrances(map, room).Any(map.IsLockedDoor)) locked++;
                    int cases = 0;
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        for (int x = room.X; x < room.X + room.Width; x++)
                            if (map.Get(new GridPos(x, y)) == TileType.SetGalleryCase) cases++;
                    Assert.That(cases, Is.EqualTo(1), $"seed {seed}");
                }
            }
            // 무작위 후보가 늘면 40개 층의 개수는 흔들린다 — 8개 층을 찾을 때까지 본다 (3-14j).
            Assert.That(floors, Is.GreaterThanOrEqualTo(8));
            Assert.That(locked * 2, Is.GreaterThanOrEqualTo(floors), "locked room — usually locked");
        }
    }
}
