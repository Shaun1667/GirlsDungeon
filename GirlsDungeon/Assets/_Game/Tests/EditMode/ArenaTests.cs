using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14i 결투장: 열린 특수 방 · 강한 적 무리(경계 안 함) · 무리를 모두 쓰러뜨리면 정밀 파츠 확정 (BALANCE 28번).</summary>
    public class ArenaTests
    {
        /// <summary>플레이어 하나 + 결투장 무리로 기록된 적 enemies개가 전투 중인 세계.</summary>
        private static World FightWorld(int enemies, int arenaMembers)
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse("........", "........", "........") };
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(0, 1)).Id;
            Loadout.Apply(world, combat);
            for (int i = 0; i < enemies; i++)
            {
                var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(4 + i, 1));
                world.Combatants.Add(enemy.Id);
                if (i < arenaMembers) world.ArenaEnemies.Add(enemy.Id);
            }
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            world.Round = 1;
            return world;
        }

        private static void KillAndFinish(World world, Unit unit, List<GameEvent> events)
        {
            CombatFlow.Kill(world, unit, events);
            CombatFlow.AfterAction(world, TestData.Combat(), events);
        }

        [Test]
        public void TheGame_PutsAnOpenArenaWithAStrongAwakeGroup()
        {
            var combat = TestData.Combat();
            int floors = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = TestData.NewRun(seed);
                var map = world.Map;
                foreach (var index in Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) == SpecialRoomKind.Arena))
                {
                    floors++;
                    var room = map.Rooms[index];
                    Assert.That(FloorDecor.Entrances(map, room).Any(map.IsLockedDoor), Is.False, $"seed {seed}: arena is open");
                    var group = world.Units.Where(u => !u.IsPlayer && room.Contains(u.Position)).ToList();
                    Assert.That(group.Count, Is.EqualTo(combat.ArenaEnemyCount), $"seed {seed}");
                    Assert.That(group.Select(u => u.Id), Is.EquivalentTo(world.ArenaEnemies), $"seed {seed}");
                    foreach (var enemy in group)
                    {
                        Assert.That(enemy.Guarding, Is.False);
                        int baseHp = combat.StatsFor(enemy.Kind).MaxHp;
                        Assert.That(enemy.MaxHp, Is.EqualTo(EnemySpawner.Scale(baseHp, 100 + combat.ArenaStatPercent)), "floor 1: only the arena bonus");
                        Assert.That(enemy.Hp, Is.EqualTo(enemy.MaxHp));
                    }
                }
                if (!map.Rooms.Where((r, i) => map.RoomKind(i) == SpecialRoomKind.Arena).Any())
                    Assert.That(world.ArenaEnemies, Is.Empty, $"seed {seed}");
            }
            Assert.That(floors, Is.GreaterThan(5));
        }

        [Test]
        public void DefeatingTheWholeGroup_DropsAPrecisionPart_Once()
        {
            var world = FightWorld(2, 2);
            var events = new List<GameEvent>();
            KillAndFinish(world, world.FindUnit(world.ArenaEnemies[0]), events);
            Assert.That(events.OfType<ArenaCleared>(), Is.Empty, "one left");
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));

            events.Clear();
            KillAndFinish(world, world.FindUnit(world.ArenaEnemies[1]), events);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            var cleared = events.OfType<ArenaCleared>().Single();
            Assert.That(cleared.Grade, Is.EqualTo(PartGrade.Precision));
            var item = world.FindItem(cleared.ItemId);
            Assert.That(item, Is.Not.Null);
            Assert.That(item.Kind, Is.EqualTo(ItemKind.Part));
            Assert.That(item.Part.Grade, Is.EqualTo(PartGrade.Precision));
            Assert.That(item.Part.Weapon, Is.EqualTo(world.PlayerWeapon), "for the weapon in hand");
            Assert.That(world.ArenaEnemies, Is.Empty);

            // 다음 전투가 끝나도 다시 나오지 않는다.
            var other = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Rifleman), new GridPos(6, 2));
            world.Combatants.Add(other.Id);
            world.Mode = WorldMode.Combat;
            events.Clear();
            KillAndFinish(world, other, events);
            Assert.That(events.OfType<ArenaCleared>(), Is.Empty);
        }

        [Test]
        public void ACombatWithoutTheArenaGroup_GivesNoArenaReward()
        {
            // 결투장 무리 1명은 전투 밖(아직 안 보임)에 남아 있다.
            var world = FightWorld(1, 0);
            var waiting = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Rifleman), new GridPos(7, 0));
            world.ArenaEnemies.Add(waiting.Id);
            var blocker = TestData.Parse(".....#..", ".....#..", ".....#..");
            world.Map = blocker;
            var events = new List<GameEvent>();
            KillAndFinish(world, world.FindUnit(world.Combatants[0]), events);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(events.OfType<ArenaCleared>(), Is.Empty);
            Assert.That(world.ArenaEnemies, Is.EqualTo(new[] { waiting.Id }));
        }

        [Test]
        public void TheSnapshotKeepsTheGroup_AndANewFloorReplacesIt()
        {
            var combat = TestData.Combat();
            var world = RunFactory.NewRun(8, TestData.Map(), combat);
            world.ArenaEnemies.Clear();
            world.ArenaEnemies.AddRange(new[] { 41, 7 });
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.ArenaEnemies, Is.EqualTo(new[] { 41, 7 }));

            RunFactory.NextFloor(world, TestData.Map(), combat, new List<GameEvent>());
            Assert.That(world.ArenaEnemies.All(id => world.FindUnit(id) != null), Is.True, "only this floor's group");
            Assert.That(world.ArenaEnemies, Has.No.Member(41));
        }
    }
}
