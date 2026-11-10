using System;
using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Map
{
    /// <summary>
    /// 시야와 사선의 한 곳. 칸 가운데끼리 잇는 선이 지나는 모든 칸을 검사하고, 벽 · 기둥이 있으면 막힌다.
    /// 선이 칸 모서리를 정확히 지날 때는 양쪽 칸이 모두 막혀야 막힌다.
    /// </summary>
    public static class Sight
    {
        /// <summary>두 칸 사이 선이 벽 · 기둥에 막히지 않으면 true (두 끝 칸은 검사하지 않는다).</summary>
        public static bool HasLine(FloorMap map, GridPos from, GridPos to)
        {
            int dx = Math.Abs(to.X - from.X);
            int dy = Math.Abs(to.Y - from.Y);
            int stepX = to.X > from.X ? 1 : -1;
            int stepY = to.Y > from.Y ? 1 : -1;
            int x = from.X;
            int y = from.Y;

            for (int ix = 0, iy = 0; ix < dx || iy < dy;)
            {
                int decision = (1 + 2 * ix) * dy - (1 + 2 * iy) * dx;
                if (decision == 0)
                {
                    if (map.BlocksSight(new GridPos(x + stepX, y)) && map.BlocksSight(new GridPos(x, y + stepY))) return false;
                    x += stepX;
                    y += stepY;
                    ix++;
                    iy++;
                }
                else if (decision < 0)
                {
                    x += stepX;
                    ix++;
                }
                else
                {
                    y += stepY;
                    iy++;
                }

                var cell = new GridPos(x, y);
                if (cell != to && map.BlocksSight(cell)) return false;
            }
            return true;
        }

        /// <summary>거리 안에 있고 선이 막히지 않으면 보인다. 거리는 칸 가운데 사이 직선 거리.</summary>
        public static bool CanSee(FloorMap map, GridPos from, GridPos to, int range)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dx * dx + dy * dy > range * range) return false;
            return HasLine(map, from, to);
        }
    }
}
