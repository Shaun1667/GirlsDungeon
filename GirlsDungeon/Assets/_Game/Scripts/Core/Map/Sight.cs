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

        private static readonly GridPos[] Sides =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
        };

        /// <summary>
        /// 사선 (사격 · 감시용, 시야는 HasLine). 곧은 선이 막혀도, 쏘는 쪽이 벽 · 기둥 바로 옆(동서남북)에 서 있으면
        /// 옆 칸(동서남북 중 설 수 있는 칸)으로 몸을 내민 자리에서의 선도 인정한다 — 모서리 너머로 쏘기 (결정 2026-11-11, 사용자 "이 상황에서 쏠 수 있어야").
        /// 엄폐는 여전히 쏘는 칸 기준으로 본다 (Cover.Against). 지도만 보므로 내민 칸의 유닛은 따지지 않는다.
        /// </summary>
        public static bool HasLineOfFire(FloorMap map, GridPos from, GridPos to)
        {
            if (HasLine(map, from, to)) return true;
            if (!NextToBlocker(map, from)) return false;
            foreach (var side in Sides)
            {
                var lean = new GridPos(from.X + side.X, from.Y + side.Y);
                if (lean == to || !map.IsWalkable(lean)) continue;
                if (HasLine(map, lean, to)) return true;
            }
            return false;
        }

        /// <summary>동서남북 옆 칸 중 시야를 막는 칸(벽 · 기둥)이 있다 — 그 모서리에서 몸을 내밀 수 있다.</summary>
        public static bool NextToBlocker(FloorMap map, GridPos cell)
        {
            foreach (var side in Sides)
                if (map.BlocksSight(new GridPos(cell.X + side.X, cell.Y + side.Y))) return true;
            return false;
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
