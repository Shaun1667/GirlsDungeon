using System.Collections.Generic;
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

        // 낭떠러지 (BALANCE 1-15 ~ 1-17)
        /// <summary>낭떠러지가 있는 방의 비율 (전체 방 수 기준, 시작 · 계단 방 제외하고 고른다).</summary>
        public int ChasmRoomPercent;
        /// <summary>낭떠러지 방에서 낭떠러지가 차지하는 방 넓이의 비율.</summary>
        public int MinChasmPercent;
        public int MaxChasmPercent;

        // 보급 상자 (BALANCE 1-18)
        /// <summary>층마다 놓는 보급 상자 수 (한 방에 하나, 시작 · 계단 방 제외).</summary>
        public int MinSupplyCrates;
        public int MaxSupplyCrates;

        // 파츠 작업방 (BALANCE 1-19, 3-4c)
        /// <summary>층마다 작업방 수 (작업대 하나씩, 시작 · 계단 방 제외, 적이 나오지 않는다). 0이면 없음.</summary>
        public int WorkshopsPerFloor;

        // 특수 방 (BALANCE 1-21 ~ 1-24, 3-14)
        /// <summary>층마다 늘 나오는 특수 방 (작업방 다음 순서).</summary>
        public List<SpecialRoomKind> AlwaysSpecialRooms = new List<SpecialRoomKind>();
        /// <summary>층마다 무작위로 고르는 특수 방 후보 (겹치지 않게).</summary>
        public List<SpecialRoomKind> RandomSpecialRooms = new List<SpecialRoomKind>();
        public int RandomSpecialRoomsPerFloor;
        /// <summary>특수 방이 아닌 일반 방을 최소 몇 개 남기는가 (시작 · 계단 방 제외).</summary>
        public int MinNormalRooms;
        /// <summary>입구를 잠그는 특수 방 종류.</summary>
        public List<SpecialRoomKind> LockedSpecialRooms = new List<SpecialRoomKind>();

        // 함정 방 (BALANCE 29번, 3-14j — FloorDecor.TryMakeTrapRoom)
        /// <summary>함정 방에서 낭떠러지가 차지하는 방 넓이의 비율 (상자 · 입구 옆은 비운다).</summary>
        public int TrapChasmPercent;
        /// <summary>함정 방에 놓는 낮은 엄폐(상자) 수.</summary>
        public int TrapCrates;

        // 도박실 (BALANCE 30번, 3-14k — FloorDecor.TryPlaceGambleChests)
        /// <summary>도박실의 도박 상자 수 (그중 하나만 열 수 있다).</summary>
        public int GambleChests;

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
            if (ChasmRoomPercent < 0 || ChasmRoomPercent > 100 || MinChasmPercent < 0 || MaxChasmPercent < MinChasmPercent || MaxChasmPercent > 100)
                throw new ArgumentException("Chasm settings are invalid.");
            if (MinSupplyCrates < 0 || MaxSupplyCrates < MinSupplyCrates)
                throw new ArgumentException("Supply crate settings are invalid.");
            if (WorkshopsPerFloor < 0)
                throw new ArgumentException("Workshop settings are invalid.");
            if (RandomSpecialRoomsPerFloor < 0 || MinNormalRooms < 0 || AlwaysSpecialRooms.Contains(SpecialRoomKind.None) || RandomSpecialRooms.Contains(SpecialRoomKind.None))
                throw new ArgumentException("Special room settings are invalid.");
            if (GambleChests < 1)
                throw new ArgumentException("Gamble room settings are invalid.");
            if (TrapChasmPercent < 0 || TrapChasmPercent > 100 || TrapCrates < 0)
                throw new ArgumentException("Trap room settings are invalid.");
        }
    }
}
