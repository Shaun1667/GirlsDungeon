using System;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>유닛 종류 하나의 능력치 (표 한 줄).</summary>
    [Serializable]
    public sealed class UnitStatsEntry
    {
        public UnitKind kind;
        [Tooltip("Text table key for the unit name.")]
        public string nameKey;
        public int maxHp;
        public int maxStability;
        public int visionRange;
        public int attack;
        public int defense;
        public int moveRange;
        public int attackRange;
        public int stabilityDamage;
        public int critPercent;
        public int critMultiplierPercent;
    }

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

        [Header("Units (BALANCE 4)")]
        [SerializeField] private UnitStatsEntry[] units =
        {
            new UnitStatsEntry
            {
                kind = UnitKind.Player, nameKey = "unit.player.name", maxHp = 20, maxStability = 6, visionRange = 9,
                attack = 10, defense = 4, moveRange = 6, attackRange = 8, stabilityDamage = 2, critPercent = 15, critMultiplierPercent = 150,
            },
            new UnitStatsEntry
            {
                kind = UnitKind.Rifleman, nameKey = "unit.rifleman.name", maxHp = 10, maxStability = 3, visionRange = 9,
                attack = 6, defense = 2, moveRange = 6, attackRange = 7, stabilityDamage = 1, critPercent = 15, critMultiplierPercent = 150,
            },
        };

        [Header("Combat (BALANCE 5)")]
        [SerializeField] private int minEnemiesPerFloor = 4;
        [SerializeField] private int maxEnemiesPerFloor = 6;
        [SerializeField] private int enemyMinDistanceFromStart = 8;
        [SerializeField] private int endTurnCostTicks = 1;
        [SerializeField] private int halfCoverReductionPercent = 25;
        [SerializeField] private int fullCoverReductionPercent = 35;
        [SerializeField] private int stabilityGuardPercent = 60;
        [SerializeField] private int minDamage = 1;

        [Header("Grenade skill (BALANCE 6)")]
        [SerializeField] private int grenadeThrowRange = 6;
        [SerializeField] private int grenadeRadius = 1;
        [SerializeField] private int grenadeDamagePercent = 70;
        [SerializeField] private int grenadeStabilityDamage = 2;
        [SerializeField] private int grenadeCooldownRounds = 3;

        [Header("Movement (BALANCE 2)")]
        [SerializeField] private int moveCostTicks = 1;
        [SerializeField] private float stepSeconds = 0.14f;

        [Header("View (BALANCE 3)")]
        [SerializeField] private float wallHeight = 1.2f;
        [SerializeField] private float lowCoverHeight = 0.55f;
        [SerializeField] private float crateSize = 0.8f;
        [SerializeField] private float pillarSize = 0.7f;
        [SerializeField] private float lowWallThickness = 0.14f;
        [Tooltip("Darkness of cells seen before but not in sight now (0 clear - 1 black).")]
        [SerializeField] private float exploredFogDarkness = 0.6f;
        [SerializeField] private int fogTexelsPerCell = 4;
        [Tooltip("How many cells the fog edge fades over.")]
        [SerializeField] private float fogSoftCells = 2f;
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
        public float ExploredFogDarkness => exploredFogDarkness;
        public int FogTexelsPerCell => fogTexelsPerCell;
        public float FogSoftCells => fogSoftCells;
        public float CameraPitch => cameraPitch;
        public float CameraStartYaw => cameraStartYaw;
        public float CameraYawStep => cameraYawStep;
        public float CameraDistance => cameraDistance;
        public float CameraFollowSharpness => cameraFollowSharpness;
        public float CameraRotateSharpness => cameraRotateSharpness;

        public CombatSettings CreateCombatSettings()
        {
            var settings = new CombatSettings
            {
                MinEnemiesPerFloor = minEnemiesPerFloor,
                MaxEnemiesPerFloor = maxEnemiesPerFloor,
                EnemyMinDistanceFromStart = enemyMinDistanceFromStart,
                EndTurnCostTicks = endTurnCostTicks,
                HalfCoverReductionPercent = halfCoverReductionPercent,
                FullCoverReductionPercent = fullCoverReductionPercent,
                StabilityGuardPercent = stabilityGuardPercent,
                MinDamage = minDamage,
                GrenadeThrowRange = grenadeThrowRange,
                GrenadeRadius = grenadeRadius,
                GrenadeDamagePercent = grenadeDamagePercent,
                GrenadeStabilityDamage = grenadeStabilityDamage,
                GrenadeCooldownRounds = grenadeCooldownRounds,
            };
            foreach (var entry in units)
            {
                settings.Units.Add(new UnitStats
                {
                    Kind = entry.kind,
                    MaxHp = entry.maxHp,
                    MaxStability = entry.maxStability,
                    VisionRange = entry.visionRange,
                    Attack = entry.attack,
                    Defense = entry.defense,
                    MoveRange = entry.moveRange,
                    AttackRange = entry.attackRange,
                    StabilityDamage = entry.stabilityDamage,
                    CritPercent = entry.critPercent,
                    CritMultiplierPercent = entry.critMultiplierPercent,
                });
            }
            return settings;
        }

        /// <summary>유닛 이름의 텍스트 표 키. 표에 없는 종류면 null.</summary>
        public string NameKey(UnitKind kind)
        {
            foreach (var entry in units) if (entry.kind == kind) return entry.nameKey;
            return null;
        }

        public System.Collections.Generic.IEnumerable<string> UnitNameKeys()
        {
            foreach (var entry in units) yield return entry.nameKey;
        }

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
