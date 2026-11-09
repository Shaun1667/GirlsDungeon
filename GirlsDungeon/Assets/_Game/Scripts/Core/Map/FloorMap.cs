using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Map
{
    /// <summary>칸 종류. 저장에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum TileType : byte
    {
        Wall = 0,
        Floor = 1,
        Stairs = 2,
    }

    /// <summary>방 하나의 사각형.</summary>
    public readonly struct RoomRect
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public RoomRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public GridPos Center => new GridPos(X + Width / 2, Y + Height / 2);

        /// <summary>margin칸 안쪽까지 겹치면 true.</summary>
        public bool Overlaps(RoomRect other, int margin)
        {
            return X - margin < other.X + other.Width && other.X < X + Width + margin
                && Y - margin < other.Y + other.Height && other.Y < Y + Height + margin;
        }
    }

    /// <summary>한 층의 지형. 세계 스냅샷에 그대로 저장된다.</summary>
    public sealed class FloorMap
    {
        private readonly TileType[] tiles;
        private readonly List<RoomRect> rooms;

        public int Width { get; }
        public int Height { get; }
        public GridPos StartCell { get; }
        public GridPos StairsCell { get; }
        public IReadOnlyList<RoomRect> Rooms => rooms;

        public FloorMap(int width, int height, TileType[] tiles, IEnumerable<RoomRect> rooms, GridPos startCell, GridPos stairsCell)
        {
            if (tiles == null || tiles.Length != width * height)
                throw new ArgumentException("Tile count does not match the map size.", nameof(tiles));

            Width = width;
            Height = height;
            this.tiles = tiles;
            this.rooms = new List<RoomRect>(rooms);
            StartCell = startCell;
            StairsCell = stairsCell;
        }

        public bool InBounds(GridPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

        public TileType Get(GridPos cell) => tiles[cell.X + cell.Y * Width];

        public bool IsWalkable(GridPos cell) => InBounds(cell) && Get(cell) != TileType.Wall;

        public int CountWalkable()
        {
            int count = 0;
            foreach (var tile in tiles) if (tile != TileType.Wall) count++;
            return count;
        }
    }
}
