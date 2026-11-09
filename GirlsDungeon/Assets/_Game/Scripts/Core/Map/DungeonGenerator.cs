using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Map
{
    /// <summary>
    /// 층 지형 생성. 방 놓기 → 복도 연결 → 고리 추가 → 시작 · 계단 지정.
    /// 재시도 횟수에 상한이 있고, 복도가 방을 잇기 때문에 모든 걸을 수 있는 칸이 이어진다.
    /// </summary>
    public static class DungeonGenerator
    {
        private static readonly GridPos[] CardinalSteps =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
        };

        public static FloorMap Generate(MapSettings settings, IRandom random)
        {
            settings.Validate();

            for (int layout = 0; layout < settings.LayoutAttempts; layout++)
            {
                var rooms = PlaceRooms(settings, random);
                if (rooms.Count < settings.MinRooms) continue;

                var tiles = new TileType[settings.Width * settings.Height];
                foreach (var room in rooms) CarveRoom(settings, tiles, room);
                ConnectRooms(settings, tiles, rooms, random);

                var start = rooms[0].Center;
                var stairs = FindFarthestRoomCenter(settings, tiles, rooms, start);
                tiles[Index(settings, stairs)] = TileType.Stairs;
                return new FloorMap(settings.Width, settings.Height, tiles, rooms, start, stairs);
            }

            throw new InvalidOperationException($"Could not place {settings.MinRooms} rooms in {settings.LayoutAttempts} layouts.");
        }

        private static List<RoomRect> PlaceRooms(MapSettings settings, IRandom random)
        {
            int target = random.Range(settings.MinRooms, settings.MaxRooms + 1);
            var rooms = new List<RoomRect>();

            for (int attempt = 0; attempt < settings.RoomAttempts && rooms.Count < target; attempt++)
            {
                int width = random.Range(settings.MinRoomSize, settings.MaxRoomSize + 1);
                int height = random.Range(settings.MinRoomSize, settings.MaxRoomSize + 1);
                int x = random.Range(1, settings.Width - width);
                int y = random.Range(1, settings.Height - height);
                var candidate = new RoomRect(x, y, width, height);

                bool overlaps = false;
                foreach (var room in rooms)
                {
                    if (!room.Overlaps(candidate, settings.RoomMargin)) continue;
                    overlaps = true;
                    break;
                }
                if (!overlaps) rooms.Add(candidate);
            }
            return rooms;
        }

        private static void CarveRoom(MapSettings settings, TileType[] tiles, RoomRect room)
        {
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    tiles[x + y * settings.Width] = TileType.Floor;
        }

        private static void ConnectRooms(MapSettings settings, TileType[] tiles, List<RoomRect> rooms, IRandom random)
        {
            // 방마다 이미 연결된 가장 가까운 방에 잇는다.
            for (int i = 1; i < rooms.Count; i++)
            {
                int nearest = 0;
                int nearestDistance = int.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    int distance = Math.Abs(rooms[i].Center.X - rooms[j].Center.X) + Math.Abs(rooms[i].Center.Y - rooms[j].Center.Y);
                    if (distance >= nearestDistance) continue;
                    nearestDistance = distance;
                    nearest = j;
                }
                CarveCorridor(settings, tiles, rooms[i].Center, rooms[nearest].Center);
            }

            // 쫓길 때 돌아갈 길이 생기도록 고리를 더한다.
            for (int i = 0; i < settings.ExtraConnections; i++)
            {
                int a = random.Range(0, rooms.Count);
                int b = random.Range(0, rooms.Count - 1);
                if (b >= a) b++;
                CarveCorridor(settings, tiles, rooms[a].Center, rooms[b].Center);
            }
        }

        /// <summary>벽을 뚫는 비용을 비싸게 해 기존 길을 재활용하는 최단 경로를 판다.</summary>
        private static void CarveCorridor(MapSettings settings, TileType[] tiles, GridPos from, GridPos to)
        {
            int cellCount = tiles.Length;
            var cost = new int[cellCount];
            var previous = new int[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                cost[i] = int.MaxValue;
                previous[i] = -1;
            }

            int fromIndex = Index(settings, from);
            int toIndex = Index(settings, to);
            cost[fromIndex] = 0;

            var queue = new MinQueue();
            queue.Enqueue(fromIndex, 0);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == toIndex) break;

                int cx = current % settings.Width;
                int cy = current / settings.Width;
                foreach (var step in CardinalSteps)
                {
                    int nx = cx + step.X;
                    int ny = cy + step.Y;
                    if (nx < 1 || ny < 1 || nx >= settings.Width - 1 || ny >= settings.Height - 1) continue;

                    int next = nx + ny * settings.Width;
                    int nextCost = cost[current] + (tiles[next] == TileType.Wall ? settings.CorridorWallCost : 1);
                    if (nextCost >= cost[next]) continue;
                    cost[next] = nextCost;
                    previous[next] = current;
                    queue.Enqueue(next, nextCost);
                }
            }

            for (int index = toIndex; index != -1; index = previous[index])
                if (tiles[index] == TileType.Wall) tiles[index] = TileType.Floor;
        }

        /// <summary>시작 칸에서 걸어서 가장 먼 방의 가운데. 같으면 먼저 놓은 방.</summary>
        private static GridPos FindFarthestRoomCenter(MapSettings settings, TileType[] tiles, List<RoomRect> rooms, GridPos start)
        {
            var distance = new int[tiles.Length];
            for (int i = 0; i < distance.Length; i++) distance[i] = -1;

            var frontier = new Queue<int>();
            int startIndex = Index(settings, start);
            distance[startIndex] = 0;
            frontier.Enqueue(startIndex);
            while (frontier.Count > 0)
            {
                int current = frontier.Dequeue();
                int cx = current % settings.Width;
                int cy = current / settings.Width;
                foreach (var step in CardinalSteps)
                {
                    int nx = cx + step.X;
                    int ny = cy + step.Y;
                    if (nx < 0 || ny < 0 || nx >= settings.Width || ny >= settings.Height) continue;
                    int next = nx + ny * settings.Width;
                    if (tiles[next] == TileType.Wall || distance[next] >= 0) continue;
                    distance[next] = distance[current] + 1;
                    frontier.Enqueue(next);
                }
            }

            var farthest = rooms[rooms.Count - 1].Center;
            int farthestDistance = -1;
            for (int i = 1; i < rooms.Count; i++)
            {
                int roomDistance = distance[Index(settings, rooms[i].Center)];
                if (roomDistance <= farthestDistance) continue;
                farthestDistance = roomDistance;
                farthest = rooms[i].Center;
            }
            return farthest;
        }

        private static int Index(MapSettings settings, GridPos cell) => cell.X + cell.Y * settings.Width;
    }
}
