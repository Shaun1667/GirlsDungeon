using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-14a 특수 방 틀 (결정 2026-11-13 사용자: 층마다 작업방 1 · 보물방 1 + 무작위 2 · 보상이 큰 방만 잠금 · 문에 방 이름).
    /// 방 내용은 3-14b부터 — 여기서는 종류 정하기 · 개수 · 잠금 · 적 제외 · 저장을 검사한다.
    /// </summary>
    public class SpecialRoomTests
    {
        /// <summary>틀 시험용: 늘 보물방, 무작위 후보 셋 중 둘.</summary>
        private static MapSettings Settings()
        {
            var map = TestData.Map();
            if (!map.AlwaysSpecialRooms.Contains(SpecialRoomKind.Treasure)) map.AlwaysSpecialRooms.Add(SpecialRoomKind.Treasure);
            map.RandomSpecialRooms.Clear();
            map.RandomSpecialRooms.AddRange(new[] { SpecialRoomKind.Storehouse, SpecialRoomKind.Repair, SpecialRoomKind.Arena });
            return map;
        }

        private static int[] SpecialIndices(FloorMap map) => Enumerable.Range(0, map.Rooms.Count).Where(i => map.RoomKind(i) != SpecialRoomKind.None).ToArray();

        [Test]
        public void EveryFloor_WorkshopFirst_ThenAlwaysRooms_ThenDistinctRandomRooms_KeepingNormalRooms()
        {
            var settings = Settings();
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = RunFactory.NewRun(seed, settings, TestData.Combat(6));
                var map = world.Map;
                var kinds = SpecialIndices(map).Select(map.RoomKind).ToList();
                int candidates = map.Rooms.Count(r => !r.Contains(map.StartCell) && !r.Contains(map.StairsCell));
                int budget = candidates - settings.MinNormalRooms;

                // 1층은 구역의 첫 층이라 작업방 다음에 상점이 하나 더 (3-15).
                int shops = SpecialRoomLayout.IsShopFloor(settings, world.FloorIndex) ? settings.ShopsPerZone : 0;
                Assert.That(kinds.Count, Is.EqualTo(System.Math.Max(0, System.Math.Min(budget, 1 + shops + 1 + settings.RandomSpecialRoomsPerFloor))), $"seed {seed}");
                Assert.That(kinds.Count(k => k == SpecialRoomKind.Workshop), Is.EqualTo(budget >= 1 ? 1 : 0), $"seed {seed}");
                if (budget >= 1 + shops) Assert.That(kinds.Count(k => k == SpecialRoomKind.Shop), Is.EqualTo(shops), $"seed {seed}");
                if (budget >= 2 + shops) Assert.That(kinds.Count(k => k == SpecialRoomKind.Treasure), Is.EqualTo(1), $"seed {seed}");
                Assert.That(kinds.Distinct().Count(), Is.EqualTo(kinds.Count), $"seed {seed}: no repeats");
                foreach (var index in SpecialIndices(map))
                {
                    var room = map.Rooms[index];
                    Assert.That(room.Contains(map.StartCell) || room.Contains(map.StairsCell), Is.False, $"seed {seed}");
                }
            }
        }

        [Test]
        public void LockedKindsAreLocked_OthersAreOpen_AndNoEnemiesInSpecialRooms()
        {
            var settings = Settings();
            int lockedFallbacks = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = RunFactory.NewRun(seed, settings, TestData.Combat(6));
                var map = world.Map;
                foreach (var index in SpecialIndices(map))
                {
                    var kind = map.RoomKind(index);
                    var room = map.Rooms[index];
                    bool locked = FloorDecor.Entrances(map, room).Any(map.IsLockedDoor);
                    if (settings.LockedSpecialRooms.Contains(kind)) { if (!locked) lockedFallbacks++; }
                    else Assert.That(locked, Is.False, $"seed {seed} {kind} is open");
                    // 결투장 · 함정 방은 자기 적을 따로 놓는다 (ArenaTests · TrapTests).
                    if (kind == SpecialRoomKind.Arena || kind == SpecialRoomKind.Trap) continue;
                    foreach (var unit in world.Units)
                        if (!unit.IsPlayer) Assert.That(room.Contains(unit.Position), Is.False, $"seed {seed}: enemy in {kind}");
                }
                // 문을 모두 열면 지도는 하나로 이어진다.
                TestData.OpenAllDoors(map);
                Assert.That(GridNavigation.FindPath(map, map.StartCell, map.StairsCell), Is.Not.Empty, $"seed {seed}");
            }
            // 잠그는 방 80개 중 — 복도가 지나가는 방은 문 없이 둔다 (작업방 · 보물방 모두 잠기는 층은 약 70%).
            Assert.That(lockedFallbacks, Is.LessThanOrEqualTo(30), "locked rooms are usually locked");
        }

        [Test]
        public void WithoutOpeningAnyDoor_TheStairsAndEveryCellOutsideLockedRoomsAreReachable()
        {
            // 3-14b 테스트에서 찾음 (seed 7): 계단으로 가는 길이 잠긴 특수 방을 지나가면 열쇠 없이는 내려갈 수 없다.
            var settings = Settings();
            for (int seed = 1; seed <= 40; seed++)
            {
                var map = RunFactory.NewRun(seed, settings, TestData.Combat()).Map;
                Assert.That(GridNavigation.FindPath(map, map.StartCell, map.StairsCell), Is.Not.Empty, $"seed {seed}: stairs without keys");
                var lockedRooms = map.Rooms.Where(r => FloorDecor.Entrances(map, r).Any(map.IsLockedDoor)).ToList();
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        var cell = new GridPos(x, y);
                        if (!map.IsWalkable(cell) || cell == map.StartCell || lockedRooms.Any(r => r.Contains(cell))) continue;
                        Assert.That(GridNavigation.FindPath(map, map.StartCell, cell), Is.Not.Empty, $"seed {seed}: {cell} behind a locked room");
                    }
            }
        }

        [Test]
        public void SpecialRoomAt_FindsTheKindOfACell()
        {
            var world = RunFactory.NewRun(3, Settings(), TestData.Combat());
            var map = world.Map;
            int index = SpecialIndices(map).First();
            var room = map.Rooms[index];
            var inside = new GridPos(room.X + 1, room.Y + 1);
            Assert.That(map.SpecialRoomAt(inside), Is.EqualTo(map.RoomKind(index)));
            Assert.That(FloorDecor.InSpecialRoom(map, inside), Is.True);
            Assert.That(map.SpecialRoomAt(map.StartCell), Is.EqualTo(SpecialRoomKind.None));
        }

        [Test]
        public void SameSeed_SameSpecialRooms_AndTheSnapshotKeepsThem()
        {
            var a = RunFactory.NewRun(9, Settings(), TestData.Combat());
            var b = RunFactory.NewRun(9, Settings(), TestData.Combat());
            Assert.That(Enumerable.Range(0, a.Map.Rooms.Count).Select(a.Map.RoomKind), Is.EqualTo(Enumerable.Range(0, b.Map.Rooms.Count).Select(b.Map.RoomKind)));

            var bytes = WorldSnapshot.Encode(a);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(Enumerable.Range(0, restored.Map.Rooms.Count).Select(restored.Map.RoomKind), Is.EqualTo(Enumerable.Range(0, a.Map.Rooms.Count).Select(a.Map.RoomKind)));
        }

        [Test]
        public void TheGame_HasTheWorkshopOnEveryFloor_WithTheRoomsBuiltSoFar()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                Assert.That(SpecialIndices(map).Count(i => map.RoomKind(i) == SpecialRoomKind.Workshop), Is.EqualTo(1), $"seed {seed}");
            }
        }
    }
}
