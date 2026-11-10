using System;

namespace GirlsDungeon.Core.Map
{
    /// <summary>맵 생성 수치. 값은 GameBalance 에셋에서 받는다 (BALANCE 1번).</summary>
    public sealed class MapSettings
    {
        public int Width;
        public int Height;
        public int MinRooms;
        public int MaxRooms;
        public int MinRoomSize;
        public int MaxRoomSize;
        public int RoomMargin;
        public int RoomAttempts;
        public int LayoutAttempts;
        public int CorridorWallCost;
        public int ExtraConnections;

        // 엄폐물 (BALANCE 1-9 ~ 1-15)
        public int MinCoverObjectsPerRoom;
        public int MaxCoverObjectsPerRoom;
        public int PillarPercent;
        public int MinLowWallsPerRoom;
        public int MaxLowWallsPerRoom;
        public int MinLowWallLength;
        public int MaxLowWallLength;
        public int DecorAttempts;

        public void Validate()
        {
            if (Width < MaxRoomSize + 2 || Height < MaxRoomSize + 2)
                throw new ArgumentException("Map is smaller than the largest room.");
            if (MinRooms < 2 || MaxRooms < MinRooms)
                throw new ArgumentException("Room count range is invalid.");
            if (MinRoomSize < 3 || MaxRoomSize < MinRoomSize)
                throw new ArgumentException("Room size range is invalid.");
            if (RoomMargin < 0 || RoomAttempts < 1 || LayoutAttempts < 1 || CorridorWallCost < 1 || ExtraConnections < 0)
                throw new ArgumentException("Map generator settings are invalid.");
            if (MinCoverObjectsPerRoom < 0 || MaxCoverObjectsPerRoom < MinCoverObjectsPerRoom
                || PillarPercent < 0 || PillarPercent > 100
                || MinLowWallsPerRoom < 0 || MaxLowWallsPerRoom < MinLowWallsPerRoom
                || MinLowWallLength < 1 || MaxLowWallLength < MinLowWallLength || DecorAttempts < 1)
                throw new ArgumentException("Cover settings are invalid.");
        }
    }
}
