using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14k 도박실: 열린 방 · 도박 상자 3개 중 하나만 · 대박 / 작은 상 / 빈 상자 / 매복 (BALANCE 30번).</summary>
    public class GambleTests
    {
        /// <summary>방 하나(0,0 ~ 7,4)에 도박 상자 3개, 플레이어는 가운데 상자 옆.</summary>
        private static World GambleWorld(int seed)
        {
            var combat = TestData.Combat();
            var rooms = new[] { new RoomRect(0, 0, 8, 5) };
            var world = new World(seed, new RandomStreams(seed))
            {
                Map = TestData.ParseWithRooms(rooms, "........", "........", "........", "........", "........"),
            };
            foreach (var x in new[] { 1, 3, 5 }) world.Map.SetTile(new GridPos(x, 2), TileType.GambleChest);
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(3, 3)).Id;
            Loadout.Apply(world, combat);
            return world;
        }

        [Test]
        public void Roll_FollowsTheChanceTable()
        {
            var combat = TestData.Combat();
            Assert.That(Gamble.Roll(combat, 0), Is.EqualTo(GambleOutcome.Jackpot));
            Assert.That(Gamble.Roll(combat, 29), Is.EqualTo(GambleOutcome.Jackpot));
            Assert.That(Gamble.Roll(combat, 30), Is.EqualTo(GambleOutcome.Small));
            Assert.That(Gamble.Roll(combat, 59), Is.EqualTo(GambleOutcome.Small));
            Assert.That(Gamble.Roll(combat, 60), Is.EqualTo(GambleOutcome.Empty));
            Assert.That(Gamble.Roll(combat, 79), Is.EqualTo(GambleOutcome.Empty));
            Assert.That(Gamble.Roll(combat, 80), Is.EqualTo(GambleOutcome.Ambush));
            Assert.That(Gamble.Roll(combat, 99), Is.EqualTo(GambleOutcome.Ambush));
        }

        [Test]
        public void OpeningOneChest_SealsTheOthers_AndGivesItsOutcome()
        {
            var combat = TestData.Combat();
            var seen = new HashSet<GambleOutcome>();
            for (int seed = 1; seed <= 80; seed++)
            {
                var world = GambleWorld(seed);
                var events = new List<GameEvent>();
                Assert.That(Loot.OpenCrate(world, combat, new GridPos(3, 2), events), Is.True);
                Assert.That(world.Map.CountTiles(TileType.GambleChest), Is.Zero, "the other chests are sealed");
                Assert.That(world.Map.CountTiles(TileType.GambleChestOpened), Is.EqualTo(3));
                Assert.That(Loot.OpenCrate(world, combat, new GridPos(1, 2), new List<GameEvent>()), Is.False, "only one per room");

                var outcome = events.OfType<GambleOpened>().Single().Outcome;
                seen.Add(outcome);
                var enemies = world.Units.Where(u => !u.IsPlayer).ToList();
                switch (outcome)
                {
                    case GambleOutcome.Jackpot:
                        var part = world.Items.Single();
                        Assert.That(part.Kind, Is.EqualTo(ItemKind.Part));
                        Assert.That(part.Part.Grade, Is.EqualTo(PartGrade.Precision));
                        Assert.That(part.Part.Weapon, Is.EqualTo(world.PlayerWeapon));
                        Assert.That(enemies, Is.Empty);
                        break;
                    case GambleOutcome.Small:
                        Assert.That(world.Items.Single().Kind, Is.EqualTo(ItemKind.Medkit));
                        Assert.That(enemies, Is.Empty);
                        break;
                    case GambleOutcome.Empty:
                        Assert.That(world.Items, Is.Empty);
                        Assert.That(enemies, Is.Empty);
                        break;
                    case GambleOutcome.Ambush:
                        Assert.That(world.Items, Is.Empty);
                        Assert.That(enemies.Count, Is.EqualTo(combat.GambleAmbushEnemies));
                        foreach (var enemy in enemies)
                        {
                            Assert.That(enemy.Guarding, Is.False);
                            int d = System.Math.Max(System.Math.Abs(enemy.Position.X - world.Player.Position.X), System.Math.Abs(enemy.Position.Y - world.Player.Position.Y));
                            Assert.That(d, Is.GreaterThan(1), "not right next to the player");
                        }
                        break;
                }
            }
            Assert.That(seen, Is.EquivalentTo(new[] { GambleOutcome.Jackpot, GambleOutcome.Small, GambleOutcome.Empty, GambleOutcome.Ambush }));
        }

        [Test]
        public void AnAmbush_StartsCombat()
        {
            var combat = TestData.Combat();
            for (int seed = 1; seed <= 80; seed++)
            {
                var world = GambleWorld(seed);
                var events = new List<GameEvent>();
                Loot.OpenCrate(world, combat, new GridPos(3, 2), events);
                if (events.OfType<GambleOpened>().Single().Outcome != GambleOutcome.Ambush) continue;
                CombatFlow.AfterAction(world, combat, events);
                Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
                Assert.That(world.Combatants.Count, Is.EqualTo(combat.GambleAmbushEnemies));
                return;
            }
            Assert.Fail("no ambush in 80 seeds");
        }

        [Test]
        public void TheGame_PutsOpenGambleRoomsWithThreeReachableChests()
        {
            var settings = TestData.Map();
            int floors = 0;
            for (int seed = 1; seed <= 200 && floors < 8; seed++)
            {
                var world = TestData.NewRun(seed);
                var map = world.Map;
                foreach (var index in Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.Gamble))
                {
                    floors++;
                    var room = map.Rooms[index];
                    Assert.That(FloorDecor.Entrances(map, room).Any(map.IsLockedDoor), Is.False, $"seed {seed}: open room");
                    var chests = new List<GridPos>();
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        for (int x = room.X; x < room.X + room.Width; x++)
                            if (map.Get(new GridPos(x, y)) == TileType.GambleChest) chests.Add(new GridPos(x, y));
                    Assert.That(chests.Count, Is.EqualTo(settings.GambleChests), $"seed {seed}");
                    foreach (var chest in chests)
                    {
                        bool reachable = false;
                        for (int dy = -1; dy <= 1 && !reachable; dy++)
                            for (int dx = -1; dx <= 1 && !reachable; dx++)
                            {
                                var near = new GridPos(chest.X + dx, chest.Y + dy);
                                if (map.IsWalkable(near) && GridNavigation.FindPath(map, map.StartCell, near) != null) reachable = true;
                            }
                        Assert.That(reachable, Is.True, $"seed {seed} {chest}");
                    }
                    Assert.That(world.Units.Any(u => !u.IsPlayer && room.Contains(u.Position)), Is.False, "no enemies until an ambush");
                }
            }
            Assert.That(floors, Is.GreaterThanOrEqualTo(8));
        }
    }
}
