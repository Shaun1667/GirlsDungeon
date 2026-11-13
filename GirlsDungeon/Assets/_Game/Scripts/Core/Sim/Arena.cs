using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 결투장의 한 곳 (3-14i — 열린 특수 방). 방 안에 강한 적 무리를 놓고, 그 무리를 모두 쓰러뜨리면 정밀 파츠 1개가 확정으로 나온다 (BALANCE 28번).
    /// - 배치 `Spawn`: 층의 일반 적 다음(EnemySpawner가 부름), 배치 난수 · 적 종류 난수. 경계하지 않는 적이라 방 안이 보이면 바로 전투에 들어온다.
    ///   강함 = 층 강화(EnemyStatPercentPerFloor) + ArenaStatPercent. 입구 칸 · 방 가운데 물건 옆은 비운다.
    /// - 보상 `CheckCleared`: 전투가 끝날 때(CombatFlow) 무리가 모두 쓰러졌으면 든 무기용 ArenaPartGrade 파츠를 마지막으로 쓰러진 자리 근처에 놓는다 (아이템 난수, 일반 전투 보상 다음). 층마다 한 번.
    /// 무리의 ID는 World.ArenaEnemies (층을 새로 만들 때 비운다).
    /// </summary>
    public static class Arena
    {
        public static void Spawn(World world, CombatSettings settings, IRandom random)
        {
            var map = world.Map;
            int floor = world.FloorIndex;
            int assaultPercent = settings.AssaultPercent + floor * settings.AssaultPercentPerFloor;
            int statPercent = 100 + floor * settings.EnemyStatPercentPerFloor + settings.ArenaStatPercent;
            var kindRandom = world.Random.Get(RandomPurpose.EnemyKind);

            for (int index = 0; index < map.Rooms.Count; index++)
            {
                if (map.RoomKind(index) != SpecialRoomKind.Arena) continue;
                var room = map.Rooms[index];
                var cells = Cells(world, room);
                for (int n = 0; n < settings.ArenaEnemyCount && cells.Count > 0; n++)
                {
                    int pick = random.Range(0, cells.Count);
                    var kind = kindRandom.Range(0, 100) < assaultPercent ? UnitKind.Assault : UnitKind.Rifleman;
                    var enemy = world.AddUnit(settings.StatsFor(kind), cells[pick]);
                    enemy.Guarding = false;
                    enemy.MaxHp = EnemySpawner.Scale(enemy.MaxHp, statPercent);
                    enemy.Hp = enemy.MaxHp;
                    enemy.Attack = EnemySpawner.Scale(enemy.Attack, statPercent);
                    world.ArenaEnemies.Add(enemy.Id);
                    cells.RemoveAt(pick);
                }
            }
        }

        /// <summary>결투장 무리가 남아 있는가 (아직 이기지 않음).</summary>
        public static bool Pending(World world) => world.ArenaEnemies.Count > 0;

        /// <summary>전투가 끝날 때: 결투장 무리가 모두 쓰러졌으면 보상을 놓고 무리 기록을 비운다.</summary>
        public static void CheckCleared(World world, CombatSettings settings, GridPos near, List<GameEvent> events)
        {
            if (world.ArenaEnemies.Count == 0) return;
            foreach (int id in world.ArenaEnemies)
                if (world.FindUnit(id) != null) return;

            world.ArenaEnemies.Clear();
            var random = world.Random.Get(RandomPurpose.Item);
            var part = Parts.GenerateWithGrade(settings, world.PlayerWeapon, settings.ArenaPartGrade, random);
            var cell = Loot.FreeCellNear(world, near) ?? world.Player.Position;
            var item = world.AddPartItem(part, cell);
            events.Add(new ArenaCleared(item.Id, part.Grade));
            events.Add(new ItemDropped(item.Id, item.Kind, item.Weapon, item.Position, DropSource.Combat));
        }

        /// <summary>무리를 놓을 수 있는 방 안 칸: 걸을 수 있고 비어 있으며, 입구 칸 · 그 바로 안쪽 · 방 가운데 물건 옆이 아닌 칸 (칸 순서 고정).</summary>
        private static List<GridPos> Cells(World world, RoomRect room)
        {
            var map = world.Map;
            var entrances = FloorDecor.Entrances(map, room);
            var result = new List<GridPos>();
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (!map.IsWalkable(cell) || world.IsOccupied(cell) || cell == map.StairsCell) continue;
                    if (NearAny(cell, entrances)) continue;
                    result.Add(cell);
                }
            return result;
        }

        private static bool NearAny(GridPos cell, List<GridPos> cells)
        {
            foreach (var other in cells)
                if (Math.Max(Math.Abs(cell.X - other.X), Math.Abs(cell.Y - other.Y)) <= 1) return true;
            return false;
        }
    }

    /// <summary>결투장 무리를 모두 쓰러뜨렸다 — 보상 파츠(ItemId, Grade)를 놓았다.</summary>
    public sealed class ArenaCleared : GameEvent
    {
        public int ItemId { get; }
        public PartGrade Grade { get; }

        public ArenaCleared(int itemId, PartGrade grade)
        {
            ItemId = itemId;
            Grade = grade;
        }
    }
}
