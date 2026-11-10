using GirlsDungeon.Core.Map;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>게임 수치의 한 곳 (BALANCE.md와 함께 관리). 코드에 숫자를 직접 넣지 않는다.</summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "GirlsDungeon/Game Balance")]
    public sealed class GameBalance : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("0 picks a new seed every run. Any other value fixes the seed.")]
        [SerializeField] private int fixedSeed;

        [Header("Map (BALANCE 1)")]
        [SerializeField] private int mapWidth = 32;
        [SerializeField] private int mapHeight = 32;
        [SerializeField] private int minRooms = 6;
        [SerializeField] private int maxRooms = 8;
        [SerializeField] private int minRoomSize = 7;
        [SerializeField] private int maxRoomSize = 13;
        [SerializeField] private int roomMargin = 1;
        [SerializeField] private int roomAttempts = 300;
        [SerializeField] private int layoutAttempts = 50;
        [SerializeField] private int corridorWallCost = 5;
        [SerializeField] private int extraConnections = 2;

        [Header("Cover (BALANCE 1-9 ~ 1-16)")]
        [SerializeField] private int minCoverObjectsPerRoom = 2;
        [SerializeField] private int maxCoverObjectsPerRoom = 4;
        [SerializeField] private int pillarPercent = 30;
        [SerializeField] private int minLowWallsPerRoom = 1;
        [SerializeField] private int maxLowWallsPerRoom = 2;
        [SerializeField] private int minLowWallLength = 2;
        [SerializeField] private int maxLowWallLength = 3;
        [SerializeField] private int decorAttempts = 30;

        [Header("Movement (BALANCE 2)")]
        [SerializeField] private int moveCostTicks = 1;
        [SerializeField] private float stepSeconds = 0.14f;

        [Header("View (BALANCE 3)")]
        [SerializeField] private float wallHeight = 1.2f;
        [SerializeField] private float lowCoverHeight = 0.55f;
        [SerializeField] private float crateSize = 0.8f;
        [SerializeField] private float pillarSize = 0.7f;
        [SerializeField] private float lowWallThickness = 0.14f;
        [SerializeField] private float cameraPitch = 52f;
        [SerializeField] private float cameraStartYaw = 45f;
        [SerializeField] private float cameraYawStep = 30f;
        [SerializeField] private float cameraDistance = 17f;
        [SerializeField] private float cameraFollowSharpness = 8f;
        [SerializeField] private float cameraRotateSharpness = 10f;

        public int FixedSeed => fixedSeed;
        public int MoveCostTicks => moveCostTicks;
        public float StepSeconds => stepSeconds;
        public float WallHeight => wallHeight;
        public float LowCoverHeight => lowCoverHeight;
        public float CrateSize => crateSize;
        public float PillarSize => pillarSize;
        public float LowWallThickness => lowWallThickness;
        public float CameraPitch => cameraPitch;
        public float CameraStartYaw => cameraStartYaw;
        public float CameraYawStep => cameraYawStep;
        public float CameraDistance => cameraDistance;
        public float CameraFollowSharpness => cameraFollowSharpness;
        public float CameraRotateSharpness => cameraRotateSharpness;

        public MapSettings CreateMapSettings()
        {
            return new MapSettings
            {
                Width = mapWidth,
                Height = mapHeight,
                MinRooms = minRooms,
                MaxRooms = maxRooms,
                MinRoomSize = minRoomSize,
                MaxRoomSize = maxRoomSize,
                RoomMargin = roomMargin,
                RoomAttempts = roomAttempts,
                LayoutAttempts = layoutAttempts,
                CorridorWallCost = corridorWallCost,
                ExtraConnections = extraConnections,
                MinCoverObjectsPerRoom = minCoverObjectsPerRoom,
                MaxCoverObjectsPerRoom = maxCoverObjectsPerRoom,
                PillarPercent = pillarPercent,
                MinLowWallsPerRoom = minLowWallsPerRoom,
                MaxLowWallsPerRoom = maxLowWallsPerRoom,
                MinLowWallLength = minLowWallLength,
                MaxLowWallLength = maxLowWallLength,
                DecorAttempts = decorAttempts,
            };
        }
    }
}
