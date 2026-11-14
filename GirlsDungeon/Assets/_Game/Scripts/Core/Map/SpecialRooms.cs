using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Map
{
    /// <summary>
    /// 특수 방 종류 (3-14, 결정 2026-11-13 사용자). 저장 · 데이터에 정수로 들어가므로 끝에만 추가한다.
    /// 이름 글은 텍스트 표 `room.kind.{이름}.name`. 방 안 내용은 종류마다 따로 만든다 (3-14b ~).
    /// </summary>
    public enum SpecialRoomKind : byte
    {
        None = 0,
        Workshop = 1,
        Treasure = 2,
        Storehouse = 3,
        SetGallery = 4,
        Exchange = 5,
        Altar = 6,
        Repair = 7,
        Arena = 8,
        Trap = 9,
        Gamble = 10,
        Observatory = 11,
        Archive = 12,
        /// <summary>상점 (3-15b) — 구역의 첫 층에만 (MapSettings.FloorsPerZone · ShopsPerZone).</summary>
        Shop = 13,
    }

    /// <summary>
    /// 특수 방 배치의 한 곳 (3-14a, 결정 2026-11-13 사용자: 층마다 작업방 1 · 보물방 1 + 무작위 2 · 보상이 큰 방만 잠금 · 문에 방 이름이 보임).
    /// - 순서: 작업방(WorkshopsPerFloor) → 늘 나오는 방(AlwaysSpecialRooms) → 무작위 방(RandomSpecialRooms에서 겹치지 않게 RandomSpecialRoomsPerFloor개).
    /// - 시작 · 계단 방은 쓰지 않고, 일반 방이 MinNormalRooms개 남도록 개수를 줄인다 (작업방 · 늘 나오는 방이 먼저).
    /// - 잠그는 종류(LockedSpecialRooms)는 FloorDecor.LockRoom으로 입구를 잠근다. 잠글 수 있는 방이 없으면 문 없이 둔다.
    /// - 보급 상자가 없는 방을 먼저 고른다. 굴림은 층 만들기 난수(RandomPurpose.Floor, 3-14l) — 보급 상자 다음에 부른다.
    /// 특수 방에는 일반 적을 놓지 않는다 (EnemySpawner — 결투장은 Arena.Spawn, 함정 방은 Trap.Spawn이 따로 놓는다).
    /// </summary>
    public static class SpecialRoomLayout
    {
        /// <param name="floorIndex">지금 층 (0 = 1층). 구역의 첫 층(floorIndex % FloorsPerZone == 0)이면 상점을 놓는다 (3-15b).</param>
        public static void Place(FloorMap map, MapSettings settings, IRandom random, int floorIndex = 0)
        {
            var candidates = new List<int>();
            for (int i = 0; i < map.Rooms.Count; i++)
            {
                var room = map.Rooms[i];
                if (!room.Contains(map.StartCell) && !room.Contains(map.StairsCell)) candidates.Add(i);
            }
            int budget = candidates.Count - settings.MinNormalRooms;

            var kinds = new List<SpecialRoomKind>();
            for (int i = 0; i < settings.WorkshopsPerFloor; i++) kinds.Add(SpecialRoomKind.Workshop);
            if (IsShopFloor(settings, floorIndex))
                for (int i = 0; i < settings.ShopsPerZone; i++) kinds.Add(SpecialRoomKind.Shop);
            kinds.AddRange(settings.AlwaysSpecialRooms);
            var pool = new List<SpecialRoomKind>(settings.RandomSpecialRooms);
            for (int i = 0; i < settings.RandomSpecialRoomsPerFloor && pool.Count > 0; i++)
            {
                int pick = random.Range(0, pool.Count);
                kinds.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            foreach (var kind in kinds)
            {
                if (budget <= 0) break;
                if (PlaceOne(map, settings, kind, random)) budget--;
            }
        }

        /// <summary>상점이 나오는 층인가: 구역의 첫 층 (결정 2026-11-14 사용자 "구역 하나당 초기에 상점 방").</summary>
        public static bool IsShopFloor(MapSettings settings, int floorIndex)
        {
            return settings.FloorsPerZone > 0 && floorIndex >= 0 && floorIndex % settings.FloorsPerZone == 0;
        }

        /// <summary>
        /// 정해진 방 하나를 그 종류의 특수 방으로 만든다 (특수 방 모음 층 — SpecialRoomShowcase). 실제 배치와 같은 순서:
        /// 내용(Prepare) → 잠그는 종류면 잠금 · 닿는지 확인, 못 잠그면 문 없이. 내용을 놓을 수 없으면 false.
        /// </summary>
        public static bool PlaceInRoom(FloorMap map, MapSettings settings, int roomIndex, SpecialRoomKind kind, IRandom random)
        {
            bool locked = settings.LockedSpecialRooms.Contains(kind);
            if (TryRoom(map, settings, roomIndex, kind, random, locked)) return true;
            return locked && TryRoom(map, settings, roomIndex, kind, random, false);
        }

        /// <summary>방 하나에 내용을 놓고(잠그는 종류면 잠그고) 종류를 정한다. 안 되면 되돌리고 false.</summary>
        private static bool TryRoom(FloorMap map, MapSettings settings, int index, SpecialRoomKind kind, IRandom random, bool locked)
        {
            var room = map.Rooms[index];
            if (!Prepare(map, settings, room, kind, random)) return false;
            if (!locked)
            {
                map.SetRoomKind(index, kind);
                return true;
            }
            if (FloorDecor.LockRoom(map, room))
            {
                if (ContentReachable(map, room, kind))
                {
                    map.SetRoomKind(index, kind);
                    return true;
                }
                foreach (var door in FloorDecor.Entrances(map, room)) map.SetTile(door, TileType.Floor);
            }
            Undo(map, room, kind);
            return false;
        }

        /// <summary>그 칸이 어느 특수 방 안인지 (None = 일반 방 · 복도).</summary>
        public static SpecialRoomKind KindAt(FloorMap map, GridPos cell) => map.SpecialRoomAt(cell);

        private static bool PlaceOne(FloorMap map, MapSettings settings, SpecialRoomKind kind, IRandom random)
        {
            bool locked = settings.LockedSpecialRooms.Contains(kind);
            var order = Order(map, random);
            foreach (int index in order)
                if (TryRoom(map, settings, index, kind, random, locked)) return true;
            if (!locked) return false;

            // 잠글 수 있는 방이 없으면(방마다 복도가 지나가거나 낭떠러지뿐인 방 등, 드묾) 문 없이 둔다 — 그 방은 층마다 있게.
            foreach (int index in order)
            {
                if (!Prepare(map, settings, map.Rooms[index], kind, random)) continue;
                map.SetRoomKind(index, kind);
                return true;
            }
            return false;
        }

        /// <summary>아직 특수 방이 아닌 후보 방 — 보급 상자가 없는 방 먼저, 각 무리 안에서 무작위 순서.</summary>
        private static List<int> Order(FloorMap map, IRandom random)
        {
            var plain = new List<int>();
            var withCrate = new List<int>();
            for (int i = 0; i < map.Rooms.Count; i++)
            {
                var room = map.Rooms[i];
                if (room.Contains(map.StartCell) || room.Contains(map.StairsCell) || map.RoomKind(i) != SpecialRoomKind.None) continue;
                (FloorDecor.HasTile(map, room, TileType.SupplyCrate) ? withCrate : plain).Add(i);
            }
            var result = new List<int>();
            foreach (var group in new[] { plain, withCrate })
            {
                while (group.Count > 0)
                {
                    int pick = random.Range(0, group.Count);
                    result.Add(group[pick]);
                    group.RemoveAt(pick);
                }
            }
            return result;
        }

        /// <summary>방 안 내용을 놓는다 (작업방 = 작업대). 다른 종류의 내용은 그 방 항목에서 더한다. 놓을 수 없으면 false.</summary>
        private static bool Prepare(FloorMap map, MapSettings settings, RoomRect room, SpecialRoomKind kind, IRandom random)
        {
            switch (kind)
            {
                case SpecialRoomKind.Workshop:
                    return FloorDecor.TryPlaceWorkbench(map, room);
                case SpecialRoomKind.Treasure:
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.TreasureChest);
                case SpecialRoomKind.Storehouse:
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.StorehouseRack);
                case SpecialRoomKind.Repair:
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.RepairStation);
                case SpecialRoomKind.Observatory:
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.ObservatoryConsole);
                case SpecialRoomKind.Exchange:
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.ExchangeCounter);
                case SpecialRoomKind.SetGallery:
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.SetGalleryCase);
                case SpecialRoomKind.Altar:
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.Altar);
                case SpecialRoomKind.Archive:
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.ArchiveTerminal);
                case SpecialRoomKind.Shop:
                    // 상인 계산대만 놓는다. 진열 물건은 세계 상태라 층을 만든 뒤 Shop.Stock이 놓는다.
                    return FloorDecor.TryPlaceAtCenter(map, room, TileType.ShopCounter);
                case SpecialRoomKind.Gamble:
                    return FloorDecor.TryPlaceGambleChests(map, room, settings.GambleChests);
                case SpecialRoomKind.Trap:
                    // 함정 방: 보물 상자 + 낭떠러지 · 낮은 엄폐 (열린 방이라 Undo로 되돌릴 일이 없다 — 잠그게 바꾸면 Undo에 지형 복구를 더한다).
                    return FloorDecor.TryMakeTrapRoom(map, room, settings, random);
                default:
                    return true;
            }
        }

        private static void Undo(FloorMap map, RoomRect room, SpecialRoomKind kind)
        {
            if (kind == SpecialRoomKind.Workshop) FloorDecor.RemoveTile(map, room, TileType.Workbench);
            if (kind == SpecialRoomKind.Treasure) FloorDecor.RemoveTile(map, room, TileType.TreasureChest);
            if (kind == SpecialRoomKind.Storehouse) FloorDecor.RemoveTile(map, room, TileType.StorehouseRack);
            if (kind == SpecialRoomKind.Repair) FloorDecor.RemoveTile(map, room, TileType.RepairStation);
            if (kind == SpecialRoomKind.Observatory) FloorDecor.RemoveTile(map, room, TileType.ObservatoryConsole);
            if (kind == SpecialRoomKind.Exchange) FloorDecor.RemoveTile(map, room, TileType.ExchangeCounter);
            if (kind == SpecialRoomKind.SetGallery) FloorDecor.RemoveTile(map, room, TileType.SetGalleryCase);
            if (kind == SpecialRoomKind.Altar) FloorDecor.RemoveTile(map, room, TileType.Altar);
            if (kind == SpecialRoomKind.Trap) FloorDecor.RemoveTile(map, room, TileType.TreasureChest);
            if (kind == SpecialRoomKind.Gamble) FloorDecor.RemoveTile(map, room, TileType.GambleChest);
            if (kind == SpecialRoomKind.Archive) FloorDecor.RemoveTile(map, room, TileType.ArchiveTerminal);
            if (kind == SpecialRoomKind.Shop) FloorDecor.RemoveTile(map, room, TileType.ShopCounter);
        }

        /// <summary>문을 단 뒤에도 방 안 내용에 닿을 수 있는가 (작업대 옆 설 칸).</summary>
        private static bool ContentReachable(FloorMap map, RoomRect room, SpecialRoomKind kind)
        {
            if (kind == SpecialRoomKind.Workshop) return FloorDecor.BenchHasRoom(map, room);
            if (kind == SpecialRoomKind.Treasure) return FloorDecor.TileHasRoom(map, room, TileType.TreasureChest);
            if (kind == SpecialRoomKind.Storehouse) return FloorDecor.TileHasRoom(map, room, TileType.StorehouseRack);
            if (kind == SpecialRoomKind.Repair) return FloorDecor.TileHasRoom(map, room, TileType.RepairStation);
            if (kind == SpecialRoomKind.Observatory) return FloorDecor.TileHasRoom(map, room, TileType.ObservatoryConsole);
            if (kind == SpecialRoomKind.Exchange) return FloorDecor.TileHasRoom(map, room, TileType.ExchangeCounter);
            if (kind == SpecialRoomKind.SetGallery) return FloorDecor.TileHasRoom(map, room, TileType.SetGalleryCase);
            if (kind == SpecialRoomKind.Altar) return FloorDecor.TileHasRoom(map, room, TileType.Altar);
            if (kind == SpecialRoomKind.Trap) return FloorDecor.TileHasRoom(map, room, TileType.TreasureChest);
            if (kind == SpecialRoomKind.Gamble) return FloorDecor.TileHasRoom(map, room, TileType.GambleChest);
            if (kind == SpecialRoomKind.Archive) return FloorDecor.TileHasRoom(map, room, TileType.ArchiveTerminal);
            if (kind == SpecialRoomKind.Shop) return FloorDecor.TileHasRoom(map, room, TileType.ShopCounter);
            return true;
        }
    }
}
