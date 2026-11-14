using System.Collections.Generic;
using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Map
{
    /// <summary>
    /// 플레이어 시야 계산의 한 곳. 보이는 칸 = 시야 거리 안에서 사선(Sight)이 막히지 않은 칸
    /// + 서 있는 칸이 방 안이면 그 방 전체와 둘레 벽 (결정 2026-11-10, 사용자 "방에 입장하면 그 방은 전부 보이게").
    /// 전투 시작 · 적 표시 · 안개가 모두 이 결과를 쓴다.
    /// </summary>
    public static class Vision
    {
        /// <summary>보이는 칸 배열 (인덱스 = x + y * Width).</summary>
        public static bool[] Compute(FloorMap map, GridPos origin, int range)
        {
            var visible = new bool[map.Width * map.Height];
            if (!map.InBounds(origin)) return visible;

            for (int y = origin.Y - range; y <= origin.Y + range; y++)
            {
                for (int x = origin.X - range; x <= origin.X + range; x++)
                {
                    var cell = new GridPos(x, y);
                    if (map.InBounds(cell) && Sight.CanSee(map, origin, cell, range))
                        visible[x + y * map.Width] = true;
                }
            }

            foreach (var room in map.Rooms)
            {
                if (!room.Contains(origin)) continue;
                for (int y = room.Y - 1; y <= room.Y + room.Height; y++)
                {
                    for (int x = room.X - 1; x <= room.X + room.Width; x++)
                    {
                        if (map.InBounds(new GridPos(x, y))) visible[x + y * map.Width] = true;
                    }
                }
            }

            visible[origin.X + origin.Y * map.Width] = true;
            RevealWallsBesideVisibleFloor(map, visible);
            return visible;
        }

        /// <summary>
        /// 보이는 바닥(설 수 있는 칸) 바로 옆(8방향)의 시야를 막는 칸(벽 · 기둥 · 잠긴 문)도 보인다 (2026-11-14 사용자 "골목에서 8칸이 보이지 않아").
        /// 사선만 보면 1칸 폭 복도 옆 벽은 플레이어 바로 근처만 보여, 부드러운 안개(FogView)가 보이는 복도 바닥까지 어둡게 번졌다.
        /// 벽에는 유닛 · 물건이 없으므로 누구를 보느냐(전투 시작 · 적 표시)는 바뀌지 않고, 지형이 보이는 범위만 바뀐다.
        /// </summary>
        private static void RevealWallsBesideVisibleFloor(FloorMap map, bool[] visible)
        {
            var reveal = new List<int>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int index = x + y * map.Width;
                    if (!visible[index] || !map.IsWalkable(new GridPos(x, y))) continue;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            var near = new GridPos(x + dx, y + dy);
                            if ((dx != 0 || dy != 0) && map.InBounds(near) && map.BlocksSight(near)) reveal.Add(near.X + near.Y * map.Width);
                        }
                    }
                }
            }
            foreach (int index in reveal) visible[index] = true;
        }

        public static bool IsSet(bool[] cells, FloorMap map, GridPos cell)
        {
            return map.InBounds(cell) && cells[cell.X + cell.Y * map.Width];
        }
    }
}
