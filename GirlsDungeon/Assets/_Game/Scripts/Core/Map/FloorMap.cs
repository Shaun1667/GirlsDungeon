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
        /// <summary>낮은 엄폐물. 칸을 막고, 옆 칸에 반엄폐를 주며, 시야는 막지 않는다.</summary>
        Crate = 3,
        /// <summary>높은 엄폐물. 칸을 막고, 옆 칸에 완전엄폐를 주며, 시야를 막는다.</summary>
        Pillar = 4,
        /// <summary>낭떠러지. 걸어서 들어갈 수 없고, 시야 · 사선을 막지 않으며 엄폐도 없다. 밀려 들어가면 떨어진다 (Push).</summary>
        Chasm = 5,
        /// <summary>닫힌 보급 상자. 칸을 막고, 옆 칸에 반엄폐를 주며, 시야는 막지 않는다. 바로 옆에서 열면 SupplyCrateOpened가 된다. 부서지지 않는다.</summary>
        SupplyCrate = 6,
        /// <summary>연 보급 상자. 닫힌 상자와 같지만 다시 열리지 않는다.</summary>
        SupplyCrateOpened = 7,
        /// <summary>파츠 작업대 (3-4c). 칸을 막고, 옆 칸에 반엄폐를 주며, 시야는 막지 않는다. 바로 옆에서 작업 화면을 연다. 부서지지 않는다.</summary>
        Workbench = 8,
        /// <summary>잠긴 문 (특수 방 입구, 3-4d). 걸을 수 없고 시야를 막는다 (벽처럼 높은 엄폐). 열쇠 · 힘으로 열면 DoorOpen.</summary>
        DoorLocked = 9,
        /// <summary>열린 문. 바닥과 같다.</summary>
        DoorOpen = 10,
        /// <summary>잠긴 문인데 힘으로 열기를 이미 한 번 실패했다 (문당 1번). 열쇠로는 열 수 있다.</summary>
        DoorLockedForced = 11,
        /// <summary>닫힌 보물 상자 (보물방, 3-14b). 보급 상자처럼 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 열면 TreasureChestOpened.</summary>
        TreasureChest = 12,
        /// <summary>연 보물 상자.</summary>
        TreasureChestOpened = 13,
        /// <summary>닫힌 보급 선반 (보급고, 3-14c). 보급 상자처럼 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 열면 StorehouseRackOpened.</summary>
        StorehouseRack = 14,
        /// <summary>연 보급 선반.</summary>
        StorehouseRackOpened = 15,
        /// <summary>수리대 (수리실, 3-14d). 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 쓰면 RepairStationUsed.</summary>
        RepairStation = 16,
        /// <summary>쓴 수리대.</summary>
        RepairStationUsed = 17,
        /// <summary>관측 콘솔 (관측실, 3-14e). 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 쓰면 ObservatoryConsoleUsed.</summary>
        ObservatoryConsole = 18,
        /// <summary>쓴 관측 콘솔.</summary>
        ObservatoryConsoleUsed = 19,
        /// <summary>교환대 (교환소, 3-14f). 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 교환 창을 연다 (쓰고 나도 그대로).</summary>
        ExchangeCounter = 20,
        /// <summary>진열장 (세트 진열실, 3-14g). 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 진열 창을 연다.</summary>
        SetGalleryCase = 21,
        /// <summary>빈 진열장 (하나를 고른 뒤).</summary>
        SetGalleryCaseUsed = 22,
        /// <summary>제단 (3-14h). 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 제단 창을 연다.</summary>
        Altar = 23,
        /// <summary>꺼진 제단 (거래를 받은 뒤).</summary>
        AltarUsed = 24,
        /// <summary>도박 상자 (도박실, 3-14k). 보급 상자처럼 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 방에서 하나만 열 수 있다.</summary>
        GambleChest = 25,
        /// <summary>연 · 닫혀 버린 도박 상자 (하나를 열면 그 방의 상자가 모두 이것이 된다).</summary>
        GambleChestOpened = 26,
        /// <summary>기록 단말 (기록실, 3-14l). 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 쓰면 다음 층 예고를 보여 주고 ArchiveTerminalUsed.</summary>
        ArchiveTerminal = 27,
        /// <summary>쓴 기록 단말.</summary>
        ArchiveTerminalUsed = 28,
        /// <summary>상인 계산대 (상점, 3-15b). 칸을 막고 낮은 엄폐, 시야는 막지 않는다. 바로 옆에서 팔기 창을 연다 (쓰고 나도 그대로).</summary>
        ShopCounter = 29,
        /// <summary>올라가는 계단 (3-16u). 2층부터 층 시작 칸에 놓인다. 설 수 있고 시야를 막지 않는다. 밟으면 위층(떠날 때 그대로)으로 올라간다.</summary>
        StairsUp = 30,
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

        public bool Contains(GridPos cell) => cell.X >= X && cell.Y >= Y && cell.X < X + Width && cell.Y < Y + Height;

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
        private readonly bool[] lowWallEast;
        private readonly bool[] lowWallNorth;
        private readonly int[] crateDamage;
        private readonly int[] lowWallEastDamage;
        private readonly int[] lowWallNorthDamage;
        private readonly List<RoomRect> rooms;
        /// <summary>방마다 특수 방 종류 (Rooms와 같은 순서, 3-14).</summary>
        private readonly SpecialRoomKind[] roomKinds;

        public int Width { get; }
        public int Height { get; }
        public GridPos StartCell { get; }
        public GridPos StairsCell { get; }
        public IReadOnlyList<RoomRect> Rooms => rooms;

        public FloorMap(int width, int height, TileType[] tiles, IEnumerable<RoomRect> rooms, GridPos startCell, GridPos stairsCell)
            : this(width, height, tiles, new bool[width * height], new bool[width * height], rooms, startCell, stairsCell)
        {
        }

        /// <param name="lowWallEast">칸과 오른쪽(+X) 칸 사이의 낮은 벽.</param>
        /// <param name="lowWallNorth">칸과 위쪽(+Y) 칸 사이의 낮은 벽.</param>
        public FloorMap(int width, int height, TileType[] tiles, bool[] lowWallEast, bool[] lowWallNorth,
            IEnumerable<RoomRect> rooms, GridPos startCell, GridPos stairsCell)
        {
            int count = width * height;
            if (tiles == null || tiles.Length != count)
                throw new ArgumentException("Tile count does not match the map size.", nameof(tiles));
            if (lowWallEast == null || lowWallEast.Length != count || lowWallNorth == null || lowWallNorth.Length != count)
                throw new ArgumentException("Low wall count does not match the map size.");

            Width = width;
            Height = height;
            this.tiles = tiles;
            this.lowWallEast = lowWallEast;
            this.lowWallNorth = lowWallNorth;
            crateDamage = new int[count];
            lowWallEastDamage = new int[count];
            lowWallNorthDamage = new int[count];
            this.rooms = new List<RoomRect>(rooms);
            roomKinds = new SpecialRoomKind[this.rooms.Count];
            StartCell = startCell;
            StairsCell = stairsCell;
        }

        /// <summary>그 방의 특수 방 종류 (None = 일반 방).</summary>
        public SpecialRoomKind RoomKind(int roomIndex) => roomKinds[roomIndex];

        public void SetRoomKind(int roomIndex, SpecialRoomKind kind) => roomKinds[roomIndex] = kind;

        /// <summary>그 칸이 든 방의 번호 (방 밖이면 -1).</summary>
        public int RoomIndexAt(GridPos cell)
        {
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Contains(cell)) return i;
            return -1;
        }

        /// <summary>그 칸이 든 특수 방의 종류 (일반 방 · 복도는 None).</summary>
        public SpecialRoomKind SpecialRoomAt(GridPos cell)
        {
            int index = RoomIndexAt(cell);
            return index < 0 ? SpecialRoomKind.None : roomKinds[index];
        }

        public bool InBounds(GridPos cell) => cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

        public TileType Get(GridPos cell) => tiles[Index(cell)];

        /// <summary>칸 종류를 바꾼다. 상자가 아니게 되면 그 칸의 상자 손상도 지운다.</summary>
        public void SetTile(GridPos cell, TileType tile)
        {
            tiles[Index(cell)] = tile;
            if (tile != TileType.Crate) crateDamage[Index(cell)] = 0;
        }

        /// <summary>상자가 받은 타격 수 (내구도와 비교해 부서짐을 정한다 — CoverBreak).</summary>
        public int CrateDamage(GridPos cell) => InBounds(cell) ? crateDamage[Index(cell)] : 0;

        public void SetCrateDamage(GridPos cell, int hits) => crateDamage[Index(cell)] = hits;

        /// <summary>두 칸 사이 낮은 벽이 받은 타격 수.</summary>
        public int LowWallDamage(GridPos a, GridPos b)
        {
            if (!TryEdge(a, b, out var owner, out bool isEast) || !InBounds(owner)) return 0;
            return isEast ? lowWallEastDamage[Index(owner)] : lowWallNorthDamage[Index(owner)];
        }

        public void SetLowWallDamage(GridPos a, GridPos b, int hits)
        {
            if (!TryEdge(a, b, out var owner, out bool isEast) || !InBounds(owner)) return;
            if (isEast) lowWallEastDamage[Index(owner)] = hits;
            else lowWallNorthDamage[Index(owner)] = hits;
        }

        /// <summary>칸 (x, y)의 오른쪽 · 위쪽 낮은 벽 손상. 저장에서 쓴다.</summary>
        public int LowWallEastDamage(GridPos cell) => InBounds(cell) ? lowWallEastDamage[Index(cell)] : 0;
        public int LowWallNorthDamage(GridPos cell) => InBounds(cell) ? lowWallNorthDamage[Index(cell)] : 0;
        public void SetLowWallEastDamage(GridPos cell, int hits) => lowWallEastDamage[Index(cell)] = hits;
        public void SetLowWallNorthDamage(GridPos cell, int hits) => lowWallNorthDamage[Index(cell)] = hits;

        /// <summary>유닛이 설 수 있는 칸 (바닥 · 계단).</summary>
        public bool IsWalkable(GridPos cell)
        {
            if (!InBounds(cell)) return false;
            var tile = Get(cell);
            return tile == TileType.Floor || tile == TileType.Stairs || tile == TileType.DoorOpen || tile == TileType.StairsUp;
        }

        /// <summary>바로 옆에서 열 수 있는 닫힌 상자 (보급 상자 · 보물 상자).</summary>
        public bool IsClosedChest(GridPos cell)
        {
            if (!InBounds(cell)) return false;
            var tile = Get(cell);
            return tile == TileType.SupplyCrate || tile == TileType.TreasureChest || tile == TileType.StorehouseRack || tile == TileType.RepairStation
                || tile == TileType.ObservatoryConsole || tile == TileType.GambleChest || tile == TileType.ArchiveTerminal;
        }

        /// <summary>특수 방의 상자류 칸 (보물 상자 · 보급 선반, 닫힘 · 열림) — 낮은 엄폐, 옆에 다른 물건을 놓지 않는다.</summary>
        public bool IsRoomChest(GridPos cell)
        {
            if (!InBounds(cell)) return false;
            var tile = Get(cell);
            return tile == TileType.TreasureChest || tile == TileType.TreasureChestOpened
                || tile == TileType.StorehouseRack || tile == TileType.StorehouseRackOpened
                || tile == TileType.RepairStation || tile == TileType.RepairStationUsed
                || tile == TileType.ObservatoryConsole || tile == TileType.ObservatoryConsoleUsed
                || tile == TileType.ExchangeCounter
                || tile == TileType.SetGalleryCase || tile == TileType.SetGalleryCaseUsed
                || tile == TileType.Altar || tile == TileType.AltarUsed
                || tile == TileType.GambleChest || tile == TileType.GambleChestOpened
                || tile == TileType.ArchiveTerminal || tile == TileType.ArchiveTerminalUsed
                || tile == TileType.ShopCounter;
        }

        /// <summary>보물 상자 칸 (닫힘 · 열림).</summary>
        public bool IsTreasureChest(GridPos cell)
        {
            if (!InBounds(cell)) return false;
            var tile = Get(cell);
            return tile == TileType.TreasureChest || tile == TileType.TreasureChestOpened;
        }

        /// <summary>잠긴 문 칸 (힘으로 열기를 실패한 문 포함).</summary>
        public bool IsLockedDoor(GridPos cell)
        {
            if (!InBounds(cell)) return false;
            var tile = Get(cell);
            return tile == TileType.DoorLocked || tile == TileType.DoorLockedForced;
        }

        /// <summary>낭떠러지 칸.</summary>
        public bool IsChasm(GridPos cell) => InBounds(cell) && Get(cell) == TileType.Chasm;

        /// <summary>보급 상자 칸 (닫힘 · 열림 모두).</summary>
        public bool IsSupplyCrate(GridPos cell)
        {
            if (!InBounds(cell)) return false;
            var tile = Get(cell);
            return tile == TileType.SupplyCrate || tile == TileType.SupplyCrateOpened;
        }

        /// <summary>시야와 사선을 막는 칸 (벽 · 기둥 · 맵 밖). 낭떠러지는 막지 않는다.</summary>
        public bool BlocksSight(GridPos cell)
        {
            if (!InBounds(cell)) return true;
            var tile = Get(cell);
            return tile == TileType.Wall || tile == TileType.Pillar || tile == TileType.DoorLocked || tile == TileType.DoorLockedForced;
        }

        /// <summary>동서남북으로 붙은 두 칸 사이에 낮은 벽이 있으면 true.</summary>
        public bool HasLowWallBetween(GridPos a, GridPos b)
        {
            if (!TryEdge(a, b, out var owner, out bool isEast) || !InBounds(owner)) return false;
            return isEast ? lowWallEast[Index(owner)] : lowWallNorth[Index(owner)];
        }

        /// <summary>동서남북으로 붙은 두 칸 사이에 낮은 벽을 놓거나 치운다. 붙어 있지 않으면 false.</summary>
        public bool SetLowWallBetween(GridPos a, GridPos b, bool present)
        {
            if (!TryEdge(a, b, out var owner, out bool isEast) || !InBounds(owner)) return false;
            if (isEast) lowWallEast[Index(owner)] = present;
            else lowWallNorth[Index(owner)] = present;
            if (!present) SetLowWallDamage(a, b, 0);
            return true;
        }

        /// <summary>칸 (x, y)의 오른쪽 · 위쪽 낮은 벽. 저장과 그리기에서 쓴다.</summary>
        public bool HasLowWallEast(GridPos cell) => InBounds(cell) && lowWallEast[Index(cell)];
        public bool HasLowWallNorth(GridPos cell) => InBounds(cell) && lowWallNorth[Index(cell)];

        public int CountWalkable()
        {
            int count = 0;
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i] == TileType.Floor || tiles[i] == TileType.Stairs || tiles[i] == TileType.DoorOpen || tiles[i] == TileType.StairsUp) count++;
            return count;
        }

        public int CountTiles(TileType type)
        {
            int count = 0;
            foreach (var tile in tiles) if (tile == type) count++;
            return count;
        }

        public int CountLowWalls()
        {
            int count = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (lowWallEast[i]) count++;
                if (lowWallNorth[i]) count++;
            }
            return count;
        }

        private int Index(GridPos cell) => cell.X + cell.Y * Width;

        private static bool TryEdge(GridPos a, GridPos b, out GridPos owner, out bool isEast)
        {
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;
            owner = a;
            isEast = false;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return false;

            isEast = dx != 0;
            owner = dx < 0 || dy < 0 ? b : a;
            return true;
        }
    }
}
