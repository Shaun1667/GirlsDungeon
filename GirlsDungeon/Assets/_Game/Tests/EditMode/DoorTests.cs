using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-4d 잠긴 문 · 열쇠 (결정 2026-11-13 사용자: 특수 방 입구는 열쇠로만 · 열쇠는 층마다 1개 보장 + 가끔 더 · 층을 넘어가도 남음 ·
    /// 캐릭터 A는 힘으로 열기 70% · 문당 1번 · 실패해도 열쇠로는 열림, BALANCE 20번).
    /// </summary>
    public class DoorTests
    {
        /// <summary>왼쪽 복도 칸 (1..4, 1..3), 오른쪽 특수 방 (6..8, 1..3), 문 (5,1) · (5,2). 플레이어 (4,2) — 문 바로 옆.</summary>
        private static (World world, CombatSettings combat) Room(CharacterKind character = CharacterKind.None, int seed = 1)
        {
            var world = new World(seed, new RandomStreams(seed))
            {
                Map = TestData.ParseWithRooms(new[] { new RoomRect(6, 1, 3, 3) },
                    "##########",
                    "#....D...#",
                    "#....D.W.#",
                    "#....#...#",
                    "##########"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(4, 2)).Id;
            world.PlayerCharacter = character;
            Loadout.Apply(world, combat);
            return (world, combat);
        }

        private static readonly GridPos Door = new GridPos(5, 2);

        [Test]
        public void EveryFloor_TheWorkshopRoomIsSealedByLockedDoors_AndTheRestStaysConnected()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = RunFactory.NewRun(seed, TestData.Map(), TestData.Combat(6));
                var map = world.Map;
                var room = map.Rooms.First(r => Enumerable.Range(r.X, r.Width).Any(x => Enumerable.Range(r.Y, r.Height).Any(y => map.Get(new GridPos(x, y)) == TileType.Workbench)));
                var entrances = FloorDecor.Entrances(map, room);
                Assert.That(entrances, Is.Not.Empty, $"seed {seed}");
                Assert.That(entrances.All(map.IsLockedDoor), Is.True, $"seed {seed}: every entrance is a locked door");
                foreach (var door in entrances)
                {
                    Assert.That(map.IsWalkable(door), Is.False);
                    Assert.That(map.BlocksSight(door), Is.True);
                }
                // 시작 칸에서 방 안으로는 갈 수 없다.
                var inside = Enumerable.Range(room.X, room.Width).SelectMany(x => Enumerable.Range(room.Y, room.Height).Select(y => new GridPos(x, y)))
                    .First(c => map.IsWalkable(c));
                Assert.That(GridNavigation.FindPath(map, map.StartCell, inside, c => false), Is.Empty, $"seed {seed}: room sealed");
                Assert.That(GridNavigation.FindPath(map, map.StartCell, map.StairsCell, c => false), Is.Not.Empty, $"seed {seed}: stairs reachable");

                // 열면 방에 들어갈 수 있다.
                Doors.OpenRoomOf(map, entrances[0]);
                Assert.That(entrances.Any(map.IsLockedDoor), Is.False);
                Assert.That(GridNavigation.FindPath(map, map.StartCell, inside, c => false), Is.Not.Empty, $"seed {seed}: room open");
            }
        }

        [Test]
        public void Key_OpensEveryDoorOfTheRoom_AndIsSpent()
        {
            var (world, _) = Room();
            Assert.That(Doors.OpenWithKey(world, Door, new List<GameEvent>()), Is.False, "no key");
            world.Keys = 2;
            var events = new List<GameEvent>();
            Assert.That(Doors.OpenWithKey(world, Door, events), Is.True);
            Assert.That(world.Keys, Is.EqualTo(1));
            Assert.That(world.Map.Get(new GridPos(5, 1)), Is.EqualTo(TileType.DoorOpen), "the other door of the same room");
            Assert.That(world.Map.Get(Door), Is.EqualTo(TileType.DoorOpen));
            Assert.That(world.Map.IsWalkable(Door), Is.True);
            Assert.That(events.OfType<DoorOpened>().Single().Count, Is.EqualTo(2));
        }

        [Test]
        public void OnlyNextToTheDoor_AndOnlyWhileExploring()
        {
            var (world, combat) = Room(CharacterKind.HeroA);
            world.Keys = 1;
            world.Player.Position = new GridPos(1, 2);
            Assert.That(Doors.OpenWithKey(world, Door, new List<GameEvent>()), Is.False);
            Assert.That(Doors.TryForce(world, combat, Door, new List<GameEvent>()), Is.False);
            world.Player.Position = new GridPos(4, 2);
            world.Mode = WorldMode.Combat;
            Assert.That(Doors.OpenWithKey(world, Door, new List<GameEvent>()), Is.False);
            Assert.That(world.Keys, Is.EqualTo(1));
        }

        [Test]
        public void HeroA_ForcesAbout70Percent_OncePerDoor_AndAKeyStillOpensAFailedDoor()
        {
            var combat = TestData.Combat();
            int successes = 0;
            for (int seed = 1; seed <= 300; seed++)
            {
                var (world, _) = Room(CharacterKind.HeroA, seed);
                var events = new List<GameEvent>();
                Assert.That(Doors.TryForce(world, combat, Door, events), Is.True);
                if (events.OfType<DoorOpened>().Any())
                {
                    successes++;
                    Assert.That(world.Map.IsLockedDoor(new GridPos(5, 1)), Is.False, "the whole room opens");
                    continue;
                }
                Assert.That(world.Map.Get(Door), Is.EqualTo(TileType.DoorLockedForced));
                Assert.That(Doors.TryForce(world, combat, Door, new List<GameEvent>()), Is.False, "one try per door");
                Assert.That(Doors.CanForce(world, combat, new GridPos(5, 1)), Is.True, "the other door can still be tried");
                world.Keys = 1;
                Assert.That(Doors.OpenWithKey(world, Door, new List<GameEvent>()), Is.True, "a key still opens it");
                Assert.That(world.Map.IsLockedDoor(Door), Is.False);
            }
            Assert.That(successes, Is.InRange(180, 240), "70% of 300");
        }

        [Test]
        public void OnlyHeroA_CanForce()
        {
            var combat = TestData.Combat();
            Assert.That(Doors.ForcePercent(Room(CharacterKind.HeroA).world, combat), Is.EqualTo(70));
            foreach (var character in new[] { CharacterKind.HeroB, CharacterKind.HeroC, CharacterKind.None })
            {
                var (world, _) = Room(character);
                Assert.That(Doors.ForcePercent(world, combat), Is.EqualTo(0), character.ToString());
                Assert.That(Doors.TryForce(world, combat, Door, new List<GameEvent>()), Is.False, character.ToString());
                Assert.That(world.Map.Get(Door), Is.EqualTo(TileType.DoorLocked));
            }
        }

        [Test]
        public void FirstCrateOfTheFloor_HoldsAKey_KeysCarryToTheNextFloor()
        {
            var combat = TestData.Combat();
            var world = new World(3, new RandomStreams(3))
            {
                Map = TestData.Parse(
                    "#########",
                    "#S.....S#",
                    "#.......#",
                    "#########"),
            };
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(2, 1)).Id;
            Loadout.Apply(world, combat);

            var events = new List<GameEvent>();
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(1, 1), events), Is.True);
            Assert.That(world.Items.Count(i => i.Kind == ItemKind.Key), Is.EqualTo(1), "guaranteed key");
            Assert.That(world.Items.Count(i => i.Kind != ItemKind.Key), Is.EqualTo(1), "plus the normal content");
            Assert.That(Loot.OpenCrate(world, combat, new GridPos(7, 1), events), Is.True);
            Assert.That(world.Items.Count(i => i.Kind == ItemKind.Key), Is.EqualTo(1), "only the first crate");

            var run = RunFactory.NewRun(5, TestData.Map(), combat);
            run.Keys = 2;
            run.FloorKeyGiven = true;
            RunFactory.NextFloor(run, TestData.Map(), combat, new List<GameEvent>());
            Assert.That(run.Keys, Is.EqualTo(2), "keys carry over");
            Assert.That(run.FloorKeyGiven, Is.False, "a new floor has a new guaranteed key");
        }

        [Test]
        public void SteppingOnAKey_PicksItUp()
        {
            var (world, combat) = Room();
            var simulation = new Simulation(world, combat);
            world.AddItem(ItemKind.Key, default, new GridPos(3, 2));
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(3, 2), 1)), Is.True);
            Assert.That(world.Keys, Is.EqualTo(1));
            Assert.That(world.Items, Is.Empty);
        }

        [Test]
        public void WinningACombat_SometimesDropsAKey_About10Percent()
        {
            var combat = TestData.Combat();
            int keys = 0;
            for (int seed = 1; seed <= 600; seed++)
            {
                var (world, _) = Room(CharacterKind.None, seed);
                Loot.DropAfterCombat(world, combat, new GridPos(2, 2), new List<GameEvent>());
                keys += world.Items.Count(i => i.Kind == ItemKind.Key);
            }
            Assert.That(keys, Is.InRange(30, 95), "10% of 600");
        }

        [Test]
        public void Snapshot_KeepsKeysAndDoors()
        {
            var (world, _) = Room();
            world.Keys = 3;
            world.FloorKeyGiven = true;
            world.Map.SetTile(new GridPos(5, 1), TileType.DoorLockedForced);
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.Keys, Is.EqualTo(3));
            Assert.That(restored.FloorKeyGiven, Is.True);
            Assert.That(restored.Map.Get(new GridPos(5, 1)), Is.EqualTo(TileType.DoorLockedForced));
        }
    }
}
