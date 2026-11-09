using GirlsDungeon.Core.Map;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>게임 수치의 한 곳 (BALANCE.md와 함께 관리). 코드에 숫자를 직접 넣지 않는다.</summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "GirlsDungeon/Game Balance")]
    public sealed class GameBalance : ScriptableObject
    {
        [Header("판")]
        [Tooltip("0이면 판마다 새 시드를 뽑고, 0이 아니면 이 시드로 고정한다.")]
        [SerializeField] private int fixedSeed;

        [Header("맵 (BALANCE 1번)")]
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

        [Header("이동 (BALANCE 2번)")]
        [SerializeField] private int moveCostTicks = 1;
        [SerializeField] private float stepSeconds = 0.14f;

        [Header("화면 (BALANCE 3번)")]
        [SerializeField] private float wallHeight = 1.2f;
        [SerializeField] private float cameraPitch = 52f;
        [SerializeField] private float cameraStartYaw = 45f;
        [SerializeField] private float cameraYawStep = 90f;
        [SerializeField] private float cameraDistance = 17f;
        [SerializeField] private float cameraFollowSharpness = 8f;
        [SerializeField] private float cameraRotateSharpness = 10f;

        public int FixedSeed => fixedSeed;
        public int MoveCostTicks => moveCostTicks;
        public float StepSeconds => stepSeconds;
        public float WallHeight => wallHeight;
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
            };
        }
    }
}
