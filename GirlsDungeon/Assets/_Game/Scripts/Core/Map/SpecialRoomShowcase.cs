using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Map
{
    /// <summary>
    /// 특수 방 모음 층 (테스트용, 2026-11-14 사용자 "씬 하나 새로 만들어서 특수방들만 다 모아줘").
    /// SpecialRoomKind의 모든 종류(None 제외)를 방 하나씩 — 새 종류를 enum에 더하면 자동으로 방이 하나 늘어난다.
    /// 모양: 위 · 아래 두 줄의 같은 크기 방, 가운데 가로 복도, 방마다 복도로 난 입구 1칸. 맨 왼쪽 위 방이 시작 방(계단 포함), 그 오른쪽이 상점.
    /// 방 안 내용 · 잠금은 실제 게임과 같은 길(SpecialRoomLayout.PlaceInRoom — Prepare · LockRoom · ContentReachable). 엄폐물은 FloorDecor.ApplyCover (꾸미기 낭떠러지는 없음 — 함정 방만 자기 낭떠러지).
    /// 방 크기는 (MinRoomSize + MaxRoomSize) / 2.
    /// </summary>
    public static class SpecialRoomShowcase
    {
        /// <summary>
        /// 모음 층에 놓는 종류 (None 제외). 상점을 맨 앞(시작 방 바로 옆)에 두고 나머지는 enum 순서 (2026-11-14 사용자 "상점 방을 제일 앞으로").
        /// </summary>
        public static List<SpecialRoomKind> Kinds()
        {
            var kinds = new List<SpecialRoomKind>();
            foreach (SpecialRoomKind kind in Enum.GetValues(typeof(SpecialRoomKind)))
                if (kind != SpecialRoomKind.None) kinds.Add(kind);
            kinds.Sort();
            if (kinds.Remove(SpecialRoomKind.Shop)) kinds.Insert(0, SpecialRoomKind.Shop);
            return kinds;
        }

        public static FloorMap Build(MapSettings settings, IRandom decorRandom, IRandom roomRandom)
        {
            var kinds = Kinds();
            int size = (settings.MinRoomSize + settings.MaxRoomSize) / 2;
            int slots = kinds.Count + 1;
            int perRow = (slots + 1) / 2;
            int width = 1 + perRow * (size + 1);
            int height = 2 * size + 5;
            int corridorY = size + 2;

            var tiles = new TileType[width * height];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = TileType.Wall;
            void Set(int x, int y, TileType tile) => tiles[x + y * width] = tile;

            var rooms = new List<RoomRect>();
            for (int slot = 0; slot < slots; slot++)
            {
                int column = slot % perRow;
                bool top = slot < perRow;
                int x = 1 + column * (size + 1);
                int y = top ? 1 : corridorY + 2;
                rooms.Add(new RoomRect(x, y, size, size));
                for (int yy = y; yy < y + size; yy++)
                    for (int xx = x; xx < x + size; xx++)
                        Set(xx, yy, TileType.Floor);
                // 입구: 방 가운데 열에서 복도로 한 칸.
                Set(x + size / 2, top ? corridorY - 1 : corridorY + 1, TileType.Floor);
            }
            for (int x = 1; x < width - 1; x++) Set(x, corridorY, TileType.Floor);

            var startRoom = rooms[0];
            var start = startRoom.Center;
            var stairs = new GridPos(startRoom.X + 1, startRoom.Y + 1);
            Set(stairs.X, stairs.Y, TileType.Stairs);

            var map = new FloorMap(width, height, tiles, rooms, start, stairs);
            FloorDecor.ApplyCover(map, settings, decorRandom);
            for (int i = 0; i < kinds.Count; i++)
                SpecialRoomLayout.PlaceInRoom(map, settings, i + 1, kinds[i], roomRandom);
            return map;
        }
    }
}
