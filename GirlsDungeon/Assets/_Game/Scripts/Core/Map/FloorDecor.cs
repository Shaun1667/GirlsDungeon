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

        public static void Apply(FloorMap map, MapSettings settings, IRandom random)
        {
            foreach (var room in map.Rooms)
            {
                PlaceCoverObjects(map, settings, room, random);
                PlaceLowWalls(map, settings, room, random);
            }
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

        private static bool IsFullyConnected(FloorMap map)
        {
            var seen = new bool[map.Width * map.Height];
            var frontier = new Queue<GridPos>();
            seen[map.StartCell.X + map.StartCell.Y * map.Width] = true;
            frontier.Enqueue(map.StartCell);
            int reached = 0;

            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                reached++;
                foreach (var step in Steps)
                {
                    var next = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    if (!GridNavigation.CanStep(map, cell, next)) continue;
                    int index = next.X + next.Y * map.Width;
                    if (seen[index]) continue;
                    seen[index] = true;
                    frontier.Enqueue(next);
                }
            }
            return reached == map.CountWalkable();
        }
    }
}
