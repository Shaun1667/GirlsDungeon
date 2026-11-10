using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>부서지는 엄폐물 한 개: 상자 칸, 또는 두 칸 사이 낮은 벽.</summary>
    public readonly struct CoverPiece
    {
        public readonly bool IsLowWall;
        public readonly GridPos Cell;
        public readonly GridPos Other;

        private CoverPiece(bool isLowWall, GridPos cell, GridPos other)
        {
            IsLowWall = isLowWall;
            Cell = cell;
            Other = other;
        }

        public static CoverPiece Crate(GridPos cell) => new CoverPiece(false, cell, cell);

        /// <summary>낮은 벽. 두 칸 순서와 상관없이 같은 벽이 되도록 작은 쪽을 먼저 둔다.</summary>
        public static CoverPiece LowWall(GridPos a, GridPos b)
        {
            bool aFirst = a.Y < b.Y || (a.Y == b.Y && a.X < b.X);
            return aFirst ? new CoverPiece(true, a, b) : new CoverPiece(true, b, a);
        }
    }

    /// <summary>
    /// 부서지는 엄폐물의 한 곳 (BALANCE 8번). 상자 · 낮은 벽만 부서진다 (기둥 · 벽은 아님).
    /// 사격: 대상의 엄폐 중 공격자 쪽을 향한 상자 · 낮은 벽이 1씩 깎인다 (엄폐가 적용될 때만).
    /// 범위 공격: 폭발 칸 안의 상자와, 한쪽이라도 폭발 칸에 닿은 낮은 벽이 1씩 깎인다.
    /// 타격 수가 내구도에 닿으면 빈 바닥이 된다. 피해 계산은 깎이기 전 엄폐로 한다.
    /// </summary>
    public static class CoverBreak
    {
        private static readonly GridPos[] Directions =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
        };

        /// <summary>target이 attacker 쪽으로 받는 부서지는 엄폐물 (Cover.Against와 같은 방향 규칙).</summary>
        public static List<CoverPiece> Facing(FloorMap map, GridPos target, GridPos attacker)
        {
            var pieces = new List<CoverPiece>();
            int offsetX = attacker.X - target.X;
            int offsetY = attacker.Y - target.Y;
            foreach (var direction in Directions)
            {
                if (direction.X * offsetX + direction.Y * offsetY <= 0) continue;
                var neighbor = new GridPos(target.X + direction.X, target.Y + direction.Y);
                if (map.InBounds(neighbor) && map.Get(neighbor) == TileType.Crate) pieces.Add(CoverPiece.Crate(neighbor));
                if (map.HasLowWallBetween(target, neighbor)) pieces.Add(CoverPiece.LowWall(target, neighbor));
            }
            return pieces;
        }

        /// <summary>폭발 칸에 걸린 부서지는 엄폐물. 같은 낮은 벽은 한 번만 센다.</summary>
        public static List<CoverPiece> InArea(FloorMap map, IReadOnlyList<GridPos> cells)
        {
            var pieces = new List<CoverPiece>();
            var seen = new HashSet<CoverPiece>();
            foreach (var cell in cells)
            {
                if (map.Get(cell) == TileType.Crate && seen.Add(CoverPiece.Crate(cell))) pieces.Add(CoverPiece.Crate(cell));
                foreach (var direction in Directions)
                {
                    var neighbor = new GridPos(cell.X + direction.X, cell.Y + direction.Y);
                    if (!map.HasLowWallBetween(cell, neighbor)) continue;
                    var wall = CoverPiece.LowWall(cell, neighbor);
                    if (seen.Add(wall)) pieces.Add(wall);
                }
            }
            return pieces;
        }

        /// <summary>이 엄폐물이 한 번 더 맞으면 부서지는지 (미리보기용).</summary>
        public static bool BreaksOnNextHit(FloorMap map, CombatSettings settings, CoverPiece piece)
        {
            return Hits(map, piece) + 1 >= Durability(settings, piece);
        }

        public static bool AnyBreaksOnNextHit(FloorMap map, CombatSettings settings, List<CoverPiece> pieces)
        {
            foreach (var piece in pieces) if (BreaksOnNextHit(map, settings, piece)) return true;
            return false;
        }

        /// <summary>엄폐물들을 한 번씩 때린다. 부서진 것은 빈 바닥이 되고 CoverDestroyed를 낸다.</summary>
        public static void Hit(World world, CombatSettings settings, List<CoverPiece> pieces, List<GameEvent> events)
        {
            var map = world.Map;
            var destroyed = new List<GridPos>();
            foreach (var piece in pieces)
            {
                int hits = Hits(map, piece) + 1;
                bool breaks = hits >= Durability(settings, piece);
                if (piece.IsLowWall)
                {
                    if (breaks) map.SetLowWallBetween(piece.Cell, piece.Other, false);
                    else map.SetLowWallDamage(piece.Cell, piece.Other, hits);
                }
                else
                {
                    if (breaks) map.SetTile(piece.Cell, TileType.Floor);
                    else map.SetCrateDamage(piece.Cell, hits);
                }
                if (breaks) destroyed.Add(piece.Cell);
            }
            if (destroyed.Count > 0) events.Add(new CoverDestroyed(destroyed));
        }

        private static int Hits(FloorMap map, CoverPiece piece)
        {
            return piece.IsLowWall ? map.LowWallDamage(piece.Cell, piece.Other) : map.CrateDamage(piece.Cell);
        }

        private static int Durability(CombatSettings settings, CoverPiece piece)
        {
            return piece.IsLowWall ? settings.LowWallDurability : settings.CrateDurability;
        }
    }
}
