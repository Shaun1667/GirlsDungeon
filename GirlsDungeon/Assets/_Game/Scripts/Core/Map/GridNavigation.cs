using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Map
{
    /// <summary>칸 이동 규칙과 경로 찾기의 한 곳. 8방향이고, 벽 모서리를 대각선으로 뚫고 지나가지 못한다.</summary>
    public static class GridNavigation
    {
        private const int StraightCost = 10;
        private const int DiagonalCost = 14;

        private static readonly GridPos[] Steps =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
            new GridPos(1, 1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(-1, -1),
        };

        /// <summary>붙어 있는 칸으로 한 걸음 옮길 수 있는지 판정한다 (다른 유닛은 보지 않는다).</summary>
        public static bool CanStep(FloorMap map, GridPos from, GridPos to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if ((dx == 0 && dy == 0) || Math.Abs(dx) > 1 || Math.Abs(dy) > 1) return false;
            if (!map.IsWalkable(from) || !map.IsWalkable(to)) return false;
            if (dx == 0 || dy == 0) return true;
            return map.IsWalkable(new GridPos(from.X + dx, from.Y)) && map.IsWalkable(new GridPos(from.X, from.Y + dy));
        }

        /// <summary>
        /// 시작 칸을 뺀 걸음 목록을 돌려준다. 갈 수 없으면 빈 목록.
        /// isBlocked는 다른 유닛처럼 지나갈 수 없는 칸을 알려 준다 (없으면 null).
        /// </summary>
        public static List<GridPos> FindPath(FloorMap map, GridPos from, GridPos to, Func<GridPos, bool> isBlocked = null)
        {
            var path = new List<GridPos>();
            if (from == to || !map.IsWalkable(from) || !map.IsWalkable(to)) return path;
            if (isBlocked != null && isBlocked(to)) return path;

            int cellCount = map.Width * map.Height;
            var cost = new int[cellCount];
            var previous = new int[cellCount];
            for (int i = 0; i < cellCount; i++)
            {
                cost[i] = int.MaxValue;
                previous[i] = -1;
            }

            int fromIndex = from.X + from.Y * map.Width;
            int toIndex = to.X + to.Y * map.Width;
            cost[fromIndex] = 0;

            var queue = new MinQueue();
            queue.Enqueue(fromIndex, 0);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == toIndex) break;

                var cell = new GridPos(current % map.Width, current / map.Width);
                foreach (var step in Steps)
                {
                    var next = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    if (!CanStep(map, cell, next)) continue;
                    if (isBlocked != null && isBlocked(next)) continue;

                    bool isDiagonal = step.X != 0 && step.Y != 0;
                    int nextIndex = next.X + next.Y * map.Width;
                    int nextCost = cost[current] + (isDiagonal ? DiagonalCost : StraightCost);
                    if (nextCost >= cost[nextIndex]) continue;
                    cost[nextIndex] = nextCost;
                    previous[nextIndex] = current;
                    queue.Enqueue(nextIndex, nextCost);
                }
            }

            if (previous[toIndex] == -1) return path;
            for (int index = toIndex; index != fromIndex; index = previous[index])
                path.Add(new GridPos(index % map.Width, index / map.Width));
            path.Reverse();
            return path;
        }
    }
}
