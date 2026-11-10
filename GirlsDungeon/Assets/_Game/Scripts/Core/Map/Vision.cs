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
            return visible;
        }

        public static bool IsSet(bool[] cells, FloorMap map, GridPos cell)
        {
            return map.InBounds(cell) && cells[cell.X + cell.Y * map.Width];
        }
    }
}
