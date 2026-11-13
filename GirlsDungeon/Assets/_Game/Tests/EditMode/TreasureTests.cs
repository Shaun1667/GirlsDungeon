using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14b 보물방: 층마다 1개 (잠김) · 가운데 보물 상자 · 정밀 파츠 확정 + 무기 50% (BALANCE 21번).</summary>
    public class TreasureTests
    {
        private static World ChestWorld(int seed)
        {
            var combat = TestData.Combat();
            var world = new World(seed, new RandomStreams(seed)) { Map = TestData.Parse("......", "......") };
            world.Map.SetTile(new GridPos(2, 0), TileType.TreasureChest);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 0)).Id;
            world.FloorKeyGiven = true;
            Loadout.Apply(world, combat);
            return world;
        }

        [Test]
        public void EveryFloor_HasALockedTreasureRoom_WithAChestInside()
        {
            int unlocked = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                var index = Enumerable.Range(0, map.Rooms.Count).Single(i => map.RoomKind(i) == SpecialRoomKind.Treasure);
                var room = map.Rooms[index];
                int chests = 0;
                for (int y = room.Y; y < room.Y + room.Height; y++)
                    for (int x = room.X; x < room.X + room.Width; x++)
                        if (map.Get(new GridPos(x, y)) == TileType.TreasureChest) chests++;
                Assert.That(chests, Is.EqualTo(1), $"seed {seed}");
                Assert.That(map.CountTiles(TileType.TreasureChest), Is.EqualTo(1), $"seed {seed}: only in the treasure room");
                if (!FloorDecor.Entrances(map, room).Any(map.IsLockedDoor)) unlocked++;
            }
            // 복도가 지나가는 방은 잠글 수 없어 문 없이 둔다 — 잠글 수 있는 방은 작업방이 먼저 쓴다 (2026-11-13 확인: 약 70% 잠김).
            Assert.That(unlocked, Is.LessThanOrEqualTo(20), "usually locked");
        }

        [Test]
        public void Chest_GivesAPrecisionPartForTheWeaponInHand_AndSometimesAWeapon()
        {
            var combat = TestData.Combat();
            int weapons = 0;
            for (int seed = 1; seed <= 300; seed++)
            {
                var world = ChestWorld(seed);
                var events = new List<GameEvent>();
                Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 0), events), Is.True);
                Assert.That(world.Map.Get(new GridPos(2, 0)), Is.EqualTo(TileType.TreasureChestOpened));
                Assert.That(events.OfType<TreasureOpened>().Count(), Is.EqualTo(1));
                var part = world.Items.Single(i => i.Kind == ItemKind.Part).Part;
                Assert.That(part.Grade, Is.EqualTo(PartGrade.Precision));
                Assert.That(part.Weapon, Is.EqualTo(world.PlayerWeapon));
                Assert.That(world.Items.Any(i => i.Kind == ItemKind.Key), Is.False, "the floor key comes from supply crates");
                var weapon = world.Items.Where(i => i.Kind == ItemKind.Weapon).ToList();
                Assert.That(weapon.Count, Is.LessThanOrEqualTo(1));
                if (weapon.Count == 1)
                {
                    weapons++;
                    Assert.That(weapon[0].Weapon, Is.Not.EqualTo(world.PlayerWeapon));
                }
                Assert.That(Loot.OpenCrate(world, combat, new GridPos(2, 0), new List<GameEvent>()), Is.False, "opens once");
            }
            Assert.That(weapons, Is.InRange(120, 180), "50% of 300");
        }

        [Test]
        public void Chest_BlocksTheCell_GivesLowCover_AndOpensFromTheNextCellOnly()
        {
            var combat = TestData.Combat();
            var world = ChestWorld(1);
            var chest = new GridPos(2, 0);
            Assert.That(world.Map.IsWalkable(chest), Is.False);
            Assert.That(world.Map.BlocksSight(chest), Is.False);
            Assert.That(Cover.InDirection(world.Map, new GridPos(1, 0), new GridPos(1, 0)), Is.EqualTo(CoverLevel.Half));
            Assert.That(world.Map.IsClosedChest(chest), Is.True);

            var simulation = new Simulation(world, combat);
            world.Player.Position = new GridPos(5, 1);
            Assert.That(simulation.Execute(new OpenSupplyCrateCommand(chest, combat)), Is.False, "too far");
            world.Player.Position = new GridPos(3, 1);
            Assert.That(simulation.Execute(new OpenSupplyCrateCommand(chest, combat)), Is.True);
            Assert.That(world.Map.IsClosedChest(chest), Is.False);
        }
    }
}
