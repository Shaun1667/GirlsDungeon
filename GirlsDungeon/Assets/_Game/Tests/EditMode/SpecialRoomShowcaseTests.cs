using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>특수 방 모음 층 (테스트 씬 SpecialRoomsTest, 2026-11-14): 모든 종류가 방 하나씩, 새 종류는 자동으로 더해진다.</summary>
    public class SpecialRoomShowcaseTests
    {
        [Test]
        public void Kinds_AreEveryKindButNone_SoNewKindsJoinAutomatically()
        {
            var all = System.Enum.GetValues(typeof(SpecialRoomKind)).Cast<SpecialRoomKind>().Where(k => k != SpecialRoomKind.None);
            Assert.That(SpecialRoomShowcase.Kinds(), Is.EqualTo(all.OrderBy(k => k)));
        }

        [Test]
        public void TheShowcaseFloor_HasEveryKindOnce_AllReachable_WithKeysForTheLockedOnes()
        {
            var kinds = SpecialRoomShowcase.Kinds();
            for (int seed = 1; seed <= 10; seed++)
            {
                var world = RunFactory.NewShowcaseRun(seed, TestData.Map(), TestData.Combat(5), CharacterKind.None);
                var map = world.Map;
                var placed = Enumerable.Range(0, map.Rooms.Count).Select(map.RoomKind).Where(k => k != SpecialRoomKind.None).OrderBy(k => k).ToList();
                Assert.That(placed, Is.EqualTo(kinds), $"seed {seed}: one room per kind");
                Assert.That(map.Rooms[0].Contains(map.StartCell) && map.Rooms[0].Contains(map.StairsCell), Is.True, "start room holds the stairs");

                int locked = Enumerable.Range(0, map.Rooms.Count).Count(i => FloorDecor.Entrances(map, map.Rooms[i]).Any(map.IsLockedDoor));
                Assert.That(world.Keys, Is.EqualTo(locked), $"seed {seed}: a key per locked room");

                // 일반 적은 없다 — 결투장 · 함정 방의 자기 적만.
                foreach (var unit in world.Units.Where(u => !u.IsPlayer))
                {
                    var kind = map.SpecialRoomAt(unit.Position);
                    Assert.That(kind == SpecialRoomKind.Arena || kind == SpecialRoomKind.Trap, Is.True, $"seed {seed}: enemy in {kind}");
                }

                // 문을 모두 열면 모든 특수 방 안에 걸어 들어갈 수 있다.
                TestData.OpenAllDoors(map);
                for (int i = 1; i < map.Rooms.Count; i++)
                {
                    var room = map.Rooms[i];
                    var entrance = FloorDecor.Entrances(map, room).FirstOrDefault(map.IsWalkable);
                    Assert.That(GridNavigation.FindPath(map, map.StartCell, entrance), Is.Not.Null, $"seed {seed}: room {map.RoomKind(i)}");
                }
            }
        }

        [Test]
        public void TheStairs_LeadToANormalFloor()
        {
            var combat = TestData.Combat(5);
            var world = RunFactory.NewShowcaseRun(3, TestData.Map(), combat, CharacterKind.None);
            RunFactory.NextFloor(world, TestData.Map(), combat, new List<GameEvent>());
            var kinds = Enumerable.Range(0, world.Map.Rooms.Count).Select(world.Map.RoomKind).Where(k => k != SpecialRoomKind.None).ToList();
            Assert.That(kinds.Count, Is.LessThan(SpecialRoomShowcase.Kinds().Count), "a normal floor again");
            Assert.That(kinds, Has.Member(SpecialRoomKind.Workshop).And.Member(SpecialRoomKind.Treasure));
        }
    }
}
