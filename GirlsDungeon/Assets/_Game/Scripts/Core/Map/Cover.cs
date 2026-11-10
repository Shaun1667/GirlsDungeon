using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Map
{
    /// <summary>엄폐 정도. 저장에 쓰지 않지만 순서는 강한 쪽이 크다.</summary>
    public enum CoverLevel
    {
        None = 0,
        Half = 1,
        Full = 2,
    }

    /// <summary>
    /// 엄폐 판정의 한 곳. 대상 칸의 동서남북 중 공격자 쪽을 향한 방향만 본다.
    /// 그 방향의 옆 칸이 벽 · 기둥이면 완전엄폐, 상자거나 사이에 낮은 벽이 있으면 반엄폐.
    /// 공격자가 엄폐의 바깥쪽(측면)에 있으면 그 방향은 적용되지 않는다.
    /// </summary>
    public static class Cover
    {
        private static readonly GridPos[] Directions =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
        };

        public static CoverLevel Against(FloorMap map, GridPos target, GridPos attacker)
        {
            int offsetX = attacker.X - target.X;
            int offsetY = attacker.Y - target.Y;
            var best = CoverLevel.None;

            foreach (var direction in Directions)
            {
                if (direction.X * offsetX + direction.Y * offsetY <= 0) continue;
                var level = InDirection(map, target, direction);
                if (level > best) best = level;
            }
            return best;
        }

        /// <summary>대상 칸이 한 방향으로 받는 엄폐 (공격자와 무관).</summary>
        public static CoverLevel InDirection(FloorMap map, GridPos cell, GridPos direction)
        {
            var neighbor = new GridPos(cell.X + direction.X, cell.Y + direction.Y);
            if (map.BlocksSight(neighbor)) return CoverLevel.Full;
            if (map.InBounds(neighbor) && map.Get(neighbor) == TileType.Crate) return CoverLevel.Half;
            if (map.HasLowWallBetween(cell, neighbor)) return CoverLevel.Half;
            return CoverLevel.None;
        }
    }
}
