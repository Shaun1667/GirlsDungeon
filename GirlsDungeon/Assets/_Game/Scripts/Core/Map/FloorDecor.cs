using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Map
{
    /// <summary>
    /// 맵 모양이 끝난 뒤 꾸미기 난수로 엄폐물을 놓는다. 별도 난수라 꾸미기를 바꿔도 같은 시드의 맵 모양은 그대로다.
    /// 놓는 순서: 칸을 막는 엄폐물(상자 · 기둥) → 낮은 벽. 막는 엄폐물은 놓을 때마다 연결을 검사한다.
    /// </summary>
    public static class FloorDecor
    {
        private static readonly GridPos[] Steps =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
            new GridPos(1, 1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(-1, -1),
        };

        private static readonly GridPos[] CardinalSteps =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
        };

        public static void Apply(FloorMap map, MapSettings settings, IRandom random)
        {
            PlaceChasms(map, settings, random);
            foreach (var room in map.Rooms)
            {
                PlaceCoverObjects(map, settings, room, random);
                PlaceLowWalls(map, settings, room, random);
            }
        }

        /// <summary>
        /// 전체 방의 ChasmRoomPercent만큼(반올림) 방을 골라 낭떠러지를 만든다 (결정 2026-11-10, 사용자).
        /// 시작 방 · 계단 방은 고르지 않는다. 방 테두리 줄은 비워 복도 입구가 막히지 않게 하고,
        /// 한 칸씩 붙여 덩어리로 키우며 놓을 때마다 연결을 검사한다.
        /// </summary>
        private static void PlaceChasms(FloorMap map, MapSettings settings, IRandom random)
        {
            var candidates = new List<RoomRect>();
            foreach (var room in map.Rooms)
                if (!room.Contains(map.StartCell) && !room.Contains(map.StairsCell)) candidates.Add(room);

            int roomCount = (map.Rooms.Count * settings.ChasmRoomPercent + 50) / 100;
            for (int i = 0; i < roomCount && candidates.Count > 0; i++)
            {
                int pick = random.Range(0, candidates.Count);
                var room = candidates[pick];
                candidates.RemoveAt(pick);
                int percent = random.Range(settings.MinChasmPercent, settings.MaxChasmPercent + 1);
                GrowChasm(map, room, room.Width * room.Height * percent / 100, random);
            }
        }

        /// <summary>방 안쪽(테두리 줄 제외)에 낭떠러지 덩어리를 target칸까지 키운다. skip이 참인 칸은 쓰지 않는다 (함정 방 — 상자 · 입구 옆).</summary>
        private static int GrowChasm(FloorMap map, RoomRect room, int target, IRandom random, System.Func<GridPos, bool> skip = null)
        {
            var open = new List<GridPos>();
            for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
                for (int x = room.X + 1; x < room.X + room.Width - 1; x++)
                {
                    var cell = new GridPos(x, y);
                    if (map.Get(cell) == TileType.Floor && (skip == null || !skip(cell))) open.Add(cell);
                }

            var edge = new List<GridPos>();
            int placed = 0;
            while (placed < target && open.Count > 0)
            {
                // 이미 놓인 낭떠러지 옆 칸을 먼저 — 덩어리가 되게. 없으면 새 덩어리를 시작한다.
                GridPos cell;
                if (edge.Count > 0)
                {
                    int e = random.Range(0, edge.Count);
                    cell = edge[e];
                    edge.RemoveAt(e);
                    if (!open.Remove(cell)) continue;
                }
                else
                {
                    int o = random.Range(0, open.Count);
                    cell = open[o];
                    open.RemoveAt(o);
                }

                map.SetTile(cell, TileType.Chasm);
                if (!IsFullyConnected(map))
                {
                    map.SetTile(cell, TileType.Floor);
                    continue;
                }
                placed++;
                foreach (var step in CardinalSteps)
                {
                    var near = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    if (open.Contains(near) && !edge.Contains(near)) edge.Add(near);
                }
            }
            return placed;
        }

        /// <summary>
        /// 보급 상자를 놓는다 (결정 2026-11-10, 사용자: 층마다 2~3개, 시작 방 밖). 꾸미기가 끝난 뒤 따로 부른다 —
        /// 난수도 따로(아이템 난수) 받으므로 상자를 더해도 같은 시드의 지형 · 엄폐물은 그대로다.
        /// 한 방에 하나, 시작 방 · 계단 방 제외, 방 테두리 줄 제외, 시작 · 계단 칸과 붙지 않게, 낮은 벽이 붙은 칸 제외, 바로 옆에 설 칸이 있게, 놓을 때마다 연결 검사.
        /// </summary>
        public static void PlaceSupplyCrates(FloorMap map, MapSettings settings, IRandom random)
        {
            var candidates = new List<RoomRect>();
            foreach (var room in map.Rooms)
                if (!room.Contains(map.StartCell) && !room.Contains(map.StairsCell)) candidates.Add(room);

            int target = random.Range(settings.MinSupplyCrates, settings.MaxSupplyCrates + 1);
            int placed = 0;
            while (placed < target && candidates.Count > 0)
            {
                int pick = random.Range(0, candidates.Count);
                var room = candidates[pick];
                candidates.RemoveAt(pick);
                if (TryPlaceSupplyCrate(map, settings, room, random)) placed++;
            }
        }

        /// <summary>
        /// 특수 방의 모든 입구에 잠긴 문을 단다 (3-4d) — 입구 = 방 가장자리 줄의 칸 중 방 밖의 설 수 있는 칸과 8방향으로 붙은 칸.
        /// 문은 방 안쪽 가장자리에 달아서 방 옆을 지나는 복도는 그대로 둔다. 문을 단 뒤 방 밖은 모두 시작 칸에서 이어지고, 방 안은 닿지 않으며,
        /// 방 안에 설 칸이 남아야 한다. 아니면(또는 입구가 시작 · 계단 칸이거나 없으면) 되돌리고 false. 다른 특수 방도 이 함수를 쓴다.
        /// </summary>
        public static bool LockRoom(FloorMap map, RoomRect room)
        {
            var doors = Entrances(map, room);
            if (doors.Count == 0) return false;
            foreach (var cell in doors)
                if (cell == map.StartCell || cell == map.StairsCell) return false;
            foreach (var cell in doors) map.SetTile(cell, TileType.DoorLocked);
            if (OutsideConnectedAndRoomSealed(map, room) && HasWalkableInside(map, room)) return true;
            foreach (var cell in doors) map.SetTile(cell, TileType.Floor);
            return false;
        }

        /// <summary>방의 입구 칸 — 방 가장자리 줄에서, 이미 문이거나 방 밖의 설 수 있는 칸과 8방향으로 붙은 설 수 있는 칸. 행 순서.</summary>
        public static List<GridPos> Entrances(FloorMap map, RoomRect room)
        {
            var result = new List<GridPos>();
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    bool edge = x == room.X || y == room.Y || x == room.X + room.Width - 1 || y == room.Y + room.Height - 1;
                    if (!edge) continue;
                    var cell = new GridPos(x, y);
                    var tile = map.Get(cell);
                    if (tile == TileType.DoorLocked || tile == TileType.DoorLockedForced || tile == TileType.DoorOpen)
                    {
                        result.Add(cell);
                        continue;
                    }
                    if (map.IsWalkable(cell) && TouchesOutsideWalkable(map, room, cell)) result.Add(cell);
                }
            return result;
        }

        private static bool TouchesOutsideWalkable(FloorMap map, RoomRect room, GridPos cell)
        {
            foreach (var step in Steps)
            {
                var next = new GridPos(cell.X + step.X, cell.Y + step.Y);
                if (!room.Contains(next) && map.IsWalkable(next)) return true;
            }
            return false;
        }

        private static bool HasWalkableInside(FloorMap map, RoomRect room)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    if (map.IsWalkable(new GridPos(x, y))) return true;
            return false;
        }

        /// <summary>문을 단 뒤에도 작업대 바로 옆에 설 칸이 남았는가.</summary>
        internal static bool BenchHasRoom(FloorMap map, RoomRect room) => TileHasRoom(map, room, TileType.Workbench);

        /// <summary>방 안의 그 칸 종류마다 바로 옆에 설 칸이 남았는가 (문을 단 뒤 확인).</summary>
        internal static bool TileHasRoom(FloorMap map, RoomRect room, TileType tile)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (map.Get(cell) == tile && !HasWalkableNeighbor(map, cell)) return false;
                }
            return true;
        }

        /// <summary>
        /// 이 방의 문을 잠근 채로, 문을 하나도 열지 않고: 시작 칸에서 잠긴 방(이 방과 이미 잠긴 다른 특수 방) 밖의 설 수 있는 칸에 모두 닿고, 이 방 안에는 닿지 않는가.
        /// 계단 · 일반 방이 잠긴 방 뒤에 갇히지 않게 한다 (3-14b 테스트에서 찾음).
        /// </summary>
        private static bool OutsideConnectedAndRoomSealed(FloorMap map, RoomRect room)
        {
            int reached = CountReachable(map, map.IsWalkable, out bool enteredRoom, room);
            if (enteredRoom) return false;
            var locked = LockedRoomCells(map);
            int walkableOutside = 0;
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (map.IsWalkable(cell) && !room.Contains(cell) && !locked[x + y * map.Width]) walkableOutside++;
                }
            return reached == walkableOutside;
        }

        /// <summary>잠긴 특수 방(가장자리에 잠긴 문이 있는 방) 안의 칸.</summary>
        private static bool[] LockedRoomCells(FloorMap map)
        {
            var cells = new bool[map.Width * map.Height];
            foreach (var room in map.Rooms)
            {
                bool hasLockedDoor = false;
                foreach (var entrance in Entrances(map, room))
                    if (map.IsLockedDoor(entrance)) hasLockedDoor = true;
                if (!hasLockedDoor) continue;
                for (int y = room.Y; y < room.Y + room.Height; y++)
                    for (int x = room.X; x < room.X + room.Width; x++)
                        cells[x + y * map.Width] = true;
            }
            return cells;
        }

        /// <summary>
        /// 시작 칸에서 passable 칸으로만 걸어 닿는 "설 수 있는 칸" 수 (8방향, 벽 모서리를 대각선으로 뚫지 못함 — GridNavigation.CanStep과 같은 규칙).
        /// watch 방 안의 칸에 닿으면 enteredRoom = true.
        /// </summary>
        private static int CountReachable(FloorMap map, System.Func<GridPos, bool> passable, out bool enteredRoom, RoomRect? watch = null, bool[] uncounted = null)
        {
            enteredRoom = false;
            var seen = new bool[map.Width * map.Height];
            var frontier = new Queue<GridPos>();
            seen[map.StartCell.X + map.StartCell.Y * map.Width] = true;
            frontier.Enqueue(map.StartCell);
            int reached = 0;
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                if (watch.HasValue && watch.Value.Contains(cell)) enteredRoom = true;
                if (map.IsWalkable(cell) && (uncounted == null || !uncounted[cell.X + cell.Y * map.Width])) reached++;
                foreach (var step in Steps)
                {
                    var next = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    if (!map.InBounds(next) || !passable(next)) continue;
                    if (step.X != 0 && step.Y != 0
                        && (!passable(new GridPos(cell.X + step.X, cell.Y)) || !passable(new GridPos(cell.X, cell.Y + step.Y)))) continue;
                    int index = next.X + next.Y * map.Width;
                    if (seen[index]) continue;
                    seen[index] = true;
                    frontier.Enqueue(next);
                }
            }
            return reached;
        }

        internal static void RemoveTile(FloorMap map, RoomRect room, TileType tile)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    if (map.Get(new GridPos(x, y)) == tile) map.SetTile(new GridPos(x, y), TileType.Floor);
        }

        /// <summary>그 칸이 작업방 안인가.</summary>
        public static bool InWorkshopRoom(FloorMap map, GridPos cell) => map.SpecialRoomAt(cell) == SpecialRoomKind.Workshop;

        /// <summary>그 칸이 특수 방 안인가 (일반 적 배치에서 뺀다, 3-14).</summary>
        public static bool InSpecialRoom(FloorMap map, GridPos cell) => map.SpecialRoomAt(cell) != SpecialRoomKind.None;

        internal static bool HasTile(FloorMap map, RoomRect room, TileType tile)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    if (map.Get(new GridPos(x, y)) == tile) return true;
            return false;
        }

        internal static bool TryPlaceWorkbench(FloorMap map, RoomRect room) => TryPlaceAtCenter(map, room, TileType.Workbench);

        /// <summary>방 가운데에 가까운 칸부터 그 칸 종류(작업대 · 보물 상자 등)를 하나 놓는다. 놓을 수 없으면 false.</summary>
        internal static bool TryPlaceAtCenter(FloorMap map, RoomRect room, TileType tile)
        {
            // 가운데에서 가까운 칸 순서 (거리 같으면 행 순서) — 난수 없이 정해진다.
            var cells = new List<GridPos>();
            for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
                for (int x = room.X + 1; x < room.X + room.Width - 1; x++)
                    cells.Add(new GridPos(x, y));
            var center = room.Center;
            cells.Sort((a, b) =>
            {
                int da = System.Math.Max(System.Math.Abs(a.X - center.X), System.Math.Abs(a.Y - center.Y));
                int db = System.Math.Max(System.Math.Abs(b.X - center.X), System.Math.Abs(b.Y - center.Y));
                if (da != db) return da.CompareTo(db);
                return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });
            foreach (var cell in cells)
            {
                if (map.Get(cell) != TileType.Floor || !HasWalkableNeighbor(map, cell) || TouchesLowWall(map, cell)) continue;
                bool nearKeyCell = false;
                foreach (var step in Steps)
                {
                    var near = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    // 보급 상자 바로 옆에도 놓지 않는다 — 상자 옆 설 칸을 막을 수 있다.
                    if (near == map.StartCell || near == map.StairsCell || map.IsSupplyCrate(near) || map.IsRoomChest(near)) nearKeyCell = true;
                }
                if (nearKeyCell) continue;

                map.SetTile(cell, tile);
                if (IsFullyConnected(map)) return true;
                map.SetTile(cell, TileType.Floor);
            }
            return false;
        }

        /// <summary>
        /// 함정 방 (3-14j): 방 가운데 보물 상자 → 낭떠러지가 방 넓이의 TrapChasmPercent가 될 때까지(원래 있던 것 포함) → 낮은 엄폐(상자) TrapCrates개.
        /// 보물 상자 · 입구 바로 옆 칸은 비워 둔다 (상자에 닿을 칸 · 들어올 칸). 놓을 때마다 연결 검사 — 복도가 지나가도 끊기지 않는다.
        /// 굴림은 작업 난수(특수 방 배치와 같은 흐름). 보물 상자를 놓을 수 없으면 false (낭떠러지 · 엄폐는 상자를 놓은 뒤에만).
        /// </summary>
        internal static bool TryMakeTrapRoom(FloorMap map, RoomRect room, MapSettings settings, IRandom random)
        {
            if (!TryPlaceAtCenter(map, room, TileType.TreasureChest)) return false;
            var keep = new List<GridPos>(Entrances(map, room));
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    if (map.Get(new GridPos(x, y)) == TileType.TreasureChest) keep.Add(new GridPos(x, y));
            System.Func<GridPos, bool> nearKeep = cell =>
            {
                foreach (var other in keep)
                    if (System.Math.Max(System.Math.Abs(cell.X - other.X), System.Math.Abs(cell.Y - other.Y)) <= 1) return true;
                return false;
            };

            // 꾸미기에서 이미 낭떠러지가 생긴 방이면 모자란 만큼만 더한다 (방 넓이 기준 합계 TrapChasmPercent).
            int existing = 0;
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    if (map.Get(new GridPos(x, y)) == TileType.Chasm) existing++;
            int target = room.Width * room.Height * settings.TrapChasmPercent / 100 - existing;
            // 낮은 벽에 붙은 칸은 쓰지 않는다 — 낮은 벽은 설 수 있는 두 칸 사이에만 선다.
            if (target > 0) GrowChasm(map, room, target, random, cell => nearKeep(cell) || TouchesLowWall(map, cell));

            var spots = new List<GridPos>();
            for (int y = room.Y + 1; y < room.Y + room.Height - 1; y++)
                for (int x = room.X + 1; x < room.X + room.Width - 1; x++)
                {
                    var cell = new GridPos(x, y);
                    if (!nearKeep(cell)) spots.Add(cell);
                }
            int placed = 0;
            while (placed < settings.TrapCrates && spots.Count > 0)
            {
                int pick = random.Range(0, spots.Count);
                var cell = spots[pick];
                spots.RemoveAt(pick);
                if (!CanHoldObject(map, cell) || TouchesLowWall(map, cell)) continue;
                map.SetTile(cell, TileType.Crate);
                if (!IsFullyConnected(map))
                {
                    map.SetTile(cell, TileType.Floor);
                    continue;
                }
                placed++;
            }
            return true;
        }

        private static bool TryPlaceSupplyCrate(FloorMap map, MapSettings settings, RoomRect room, IRandom random)
        {
            for (int attempt = 0; attempt < settings.DecorAttempts; attempt++)
            {
                var cell = new GridPos(
                    random.Range(room.X + 1, room.X + room.Width - 1),
                    random.Range(room.Y + 1, room.Y + room.Height - 1));
                // 낮은 벽은 걸을 수 있는 두 칸 사이에만 선다 — 낮은 벽이 붙은 칸에는 놓지 않는다.
                if (map.Get(cell) != TileType.Floor || !HasWalkableNeighbor(map, cell) || TouchesLowWall(map, cell)) continue;
                bool nearKeyCell = false;
                foreach (var step in Steps)
                {
                    var near = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    if (near == map.StartCell || near == map.StairsCell) nearKeyCell = true;
                }
                if (nearKeyCell) continue;

                map.SetTile(cell, TileType.SupplyCrate);
                if (IsFullyConnected(map)) return true;
                map.SetTile(cell, TileType.Floor);
            }
            return false;
        }

        private static bool TouchesLowWall(FloorMap map, GridPos cell)
        {
            foreach (var step in CardinalSteps)
                if (map.HasLowWallBetween(cell, new GridPos(cell.X + step.X, cell.Y + step.Y))) return true;
            return false;
        }

        private static bool HasWalkableNeighbor(FloorMap map, GridPos cell)
        {
            foreach (var step in Steps)
                if (map.IsWalkable(new GridPos(cell.X + step.X, cell.Y + step.Y))) return true;
            return false;
        }

        private static void PlaceCoverObjects(FloorMap map, MapSettings settings, RoomRect room, IRandom random)
        {
            int target = random.Range(settings.MinCoverObjectsPerRoom, settings.MaxCoverObjectsPerRoom + 1);
            int placed = 0;
            for (int attempt = 0; attempt < settings.DecorAttempts && placed < target; attempt++)
            {
                // 방 테두리 줄은 비워 둔다 — 복도 입구가 막히지 않게.
                var cell = new GridPos(
                    random.Range(room.X + 1, room.X + room.Width - 1),
                    random.Range(room.Y + 1, room.Y + room.Height - 1));
                var type = random.Range(0, 100) < settings.PillarPercent ? TileType.Pillar : TileType.Crate;

                if (!CanHoldObject(map, cell)) continue;

                map.SetTile(cell, type);
                if (IsFullyConnected(map))
                {
                    placed++;
                    continue;
                }
                map.SetTile(cell, TileType.Floor);
            }
        }

        private static void PlaceLowWalls(FloorMap map, MapSettings settings, RoomRect room, IRandom random)
        {
            int target = random.Range(settings.MinLowWallsPerRoom, settings.MaxLowWallsPerRoom + 1);
            int placed = 0;
            for (int attempt = 0; attempt < settings.DecorAttempts && placed < target; attempt++)
            {
                bool runsAlongX = random.Range(0, 2) == 0;
                int length = random.Range(settings.MinLowWallLength, settings.MaxLowWallLength + 1);
                var start = new GridPos(
                    random.Range(room.X + 1, room.X + room.Width - 1),
                    random.Range(room.Y + 1, room.Y + room.Height - 1));

                // 낮은 벽은 칸 사이 변에 놓인다. X 방향으로 늘어선 벽은 각 칸과 위쪽 칸 사이에 있다.
                var across = runsAlongX ? new GridPos(0, 1) : new GridPos(1, 0);
                var along = runsAlongX ? new GridPos(1, 0) : new GridPos(0, 1);

                var edges = new List<GridPos>();
                bool fits = true;
                for (int i = 0; i < length && fits; i++)
                {
                    var cell = new GridPos(start.X + along.X * i, start.Y + along.Y * i);
                    var other = new GridPos(cell.X + across.X, cell.Y + across.Y);
                    fits = IsInsideRoomInterior(room, cell) && IsInsideRoomInterior(room, other)
                        && map.IsWalkable(cell) && map.IsWalkable(other) && !map.HasLowWallBetween(cell, other);
                    edges.Add(cell);
                }
                if (!fits) continue;

                foreach (var cell in edges)
                    map.SetLowWallBetween(cell, new GridPos(cell.X + across.X, cell.Y + across.Y), true);
                placed++;
            }
        }

        private static bool IsInsideRoomInterior(RoomRect room, GridPos cell)
        {
            return cell.X > room.X && cell.Y > room.Y && cell.X < room.X + room.Width - 1 && cell.Y < room.Y + room.Height - 1;
        }

        /// <summary>바닥이고, 시작 · 계단이 아니고, 주변 8칸에 다른 엄폐물이 없어야 한다.</summary>
        private static bool CanHoldObject(FloorMap map, GridPos cell)
        {
            if (map.Get(cell) != TileType.Floor || cell == map.StartCell || cell == map.StairsCell) return false;
            foreach (var step in Steps)
            {
                var near = new GridPos(cell.X + step.X, cell.Y + step.Y);
                if (near == map.StartCell || near == map.StairsCell) return false;
                var tile = map.Get(near);
                if (tile == TileType.Crate || tile == TileType.Pillar) return false;
            }
            return true;
        }

        /// <summary>
        /// 지도가 하나로 이어지는가 (3-14): ① 잠긴 방 밖의 설 수 있는 칸은 문을 하나도 열지 않고 모두 닿고,
        /// ② 잠긴 문을 열 수 있는 길로 치면 모든 설 수 있는 칸에 닿는다 (잠긴 방 안도 문을 열면 닿는다).
        /// </summary>
        private static bool IsFullyConnected(FloorMap map)
        {
            if (CountReachable(map, cell => map.IsWalkable(cell) || map.IsLockedDoor(cell), out _) != map.CountWalkable()) return false;
            var locked = LockedRoomCells(map);
            int outside = 0;
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.IsWalkable(new GridPos(x, y)) && !locked[x + y * map.Width]) outside++;
            int reachedWithoutDoors = CountReachable(map, map.IsWalkable, out _, null, locked);
            return reachedWithoutDoors == outside;
        }
    }
}
