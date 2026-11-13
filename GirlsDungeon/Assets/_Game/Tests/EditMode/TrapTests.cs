using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-14j 함정 방: 열린 방 · 낭떠러지와 낮은 엄폐 · 가운데 보물 상자 · 입구에서 먼 감시 중인 사격병 (BALANCE 29번).</summary>
    public class TrapTests
    {
        private static IEnumerable<(int seed, World world, RoomRect room)> TrapRooms()
        {
            // 함정 방이 있는 층을 8개 찾을 때까지 (무작위 후보 8종 중 2개).
            int found = 0;
            for (int seed = 1; seed <= 200 && found < 8; seed++)
            {
                var world = TestData.NewRun(seed);
                var map = world.Map;
                for (int i = 0; i < map.Rooms.Count; i++)
                    if (map.RoomKind(i) == SpecialRoomKind.Trap)
                    {
                        found++;
                        yield return (seed, world, map.Rooms[i]);
                    }
            }
        }

        private static IEnumerable<GridPos> CellsOf(RoomRect room)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    yield return new GridPos(x, y);
        }

        [Test]
        public void TheGame_BuildsOpenTrapRooms_WithChasms_Cover_AndAReachableChest()
        {
            var settings = TestData.Map();
            int rooms = 0;
            foreach (var (seed, world, room) in TrapRooms())
            {
                rooms++;
                var map = world.Map;
                Assert.That(FloorDecor.Entrances(map, room).Any(map.IsLockedDoor), Is.False, $"seed {seed}: open room");
                var chests = CellsOf(room).Where(c => map.Get(c) == TileType.TreasureChest).ToList();
                Assert.That(chests.Count, Is.EqualTo(1), $"seed {seed}");
                int chasms = CellsOf(room).Count(c => map.Get(c) == TileType.Chasm);
                Assert.That(chasms, Is.GreaterThanOrEqualTo(room.Width * room.Height * settings.TrapChasmPercent / 100 / 2), $"seed {seed}: chasm-grown (total share, decor chasms count)");
                Assert.That(CellsOf(room).Any(c => map.Get(c) == TileType.Crate), Is.True, $"seed {seed}: low cover");

                // 상자 옆 설 칸까지 시작 칸에서 걸어갈 수 있고, 계단도 막히지 않는다.
                var chest = chests[0];
                bool reachable = false;
                for (int dy = -1; dy <= 1 && !reachable; dy++)
                    for (int dx = -1; dx <= 1 && !reachable; dx++)
                    {
                        var near = new GridPos(chest.X + dx, chest.Y + dy);
                        if (map.IsWalkable(near) && GridNavigation.FindPath(map, map.StartCell, near) != null) reachable = true;
                    }
                Assert.That(reachable, Is.True, $"seed {seed}: chest reachable");
                Assert.That(GridNavigation.FindPath(map, map.StartCell, map.StairsCell), Is.Not.Null, $"seed {seed}: stairs reachable");
            }
            Assert.That(rooms, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void TrapGuards_AreOverwatchingRiflemen_FarFromTheEntrance()
        {
            var combat = TestData.Combat();
            foreach (var (seed, world, room) in TrapRooms())
            {
                var map = world.Map;
                var guards = world.Units.Where(u => !u.IsPlayer && room.Contains(u.Position)).ToList();
                Assert.That(guards.Count, Is.EqualTo(combat.TrapEnemyCount), $"seed {seed}");
                var entrances = FloorDecor.Entrances(map, room);
                foreach (var guard in guards)
                {
                    Assert.That(guard.Kind, Is.EqualTo(UnitKind.Rifleman));
                    Assert.That(guard.Overwatching, Is.True);
                    Assert.That(guard.Guarding, Is.False);
                    Assert.That(guard.MaxHp, Is.EqualTo(combat.StatsFor(UnitKind.Rifleman).MaxHp), "floor 1: normal strength");
                    if (entrances.Count == 0) continue;
                    int distance = entrances.Min(e => System.Math.Max(System.Math.Abs(e.X - guard.Position.X), System.Math.Abs(e.Y - guard.Position.Y)));
                    Assert.That(distance, Is.GreaterThanOrEqualTo(2), $"seed {seed}: not at the door");
                }
            }
        }

        [Test]
        public void SeeingTheGuards_StartsCombat_AndTheyKeepWatching()
        {
            var combat = TestData.Combat();
            foreach (var (seed, world, room) in TrapRooms())
            {
                var guards = world.Units.Where(u => !u.IsPlayer && room.Contains(u.Position)).ToList();
                // 방 안 아무 빈 칸으로 옮겨 방 전체가 보이게 한다.
                var inside = CellsOf(room).First(c => world.Map.IsWalkable(c) && !world.IsOccupied(c));
                world.Player.Position = inside;
                CombatFlow.AfterAction(world, combat, new List<GameEvent>());
                Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat), $"seed {seed}");
                Assert.That(guards.All(g => world.Combatants.Contains(g.Id)), Is.True);
                Assert.That(guards.All(g => g.Overwatching), Is.True, "still on overwatch for the player's first turn");
                return;
            }
            Assert.Fail("no trap room");
        }
    }
}
