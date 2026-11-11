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
        [Tooltip("Cells a hit pushes the target away from the attacker (0 = no push).")]
        public int pushOnHit;
    }

    /// <summary>무기 하나의 능력치와 규칙 (표 한 줄, BALANCE 12번).</summary>
    [Serializable]
    public sealed class WeaponEntry
    {
        public WeaponKind kind;
        [Tooltip("Text table key for the weapon name.")]
        public string nameKey;
        [Tooltip("Text table key for the weapon rule description.")]
        public string ruleKey;
        public int attack;
        public int attackRange;
        public int stabilityDamage;
        public int critPercent;
        public int critMultiplierPercent;
        [Tooltip("Rule: may still move after shooting (then the turn ends).")]
        public bool moveAfterShot;
        [Tooltip("Rule: cannot shoot in a turn after moving.")]
        public bool noShotAfterMove;
        [Tooltip("Rule: damage drops beyond this many cells (0 = no drop).")]
        public int falloffStartCells;
        [Tooltip("Damage lost (%) per cell beyond the falloff start.")]
        public int falloffPercentPerCell;
        [Tooltip("Rule: extra shots per turn (1 = two shots). Moving between shots is allowed.")]
        public int extraShotsPerTurn;
        [Tooltip("Rule: added to the player's move range (negative = heavier).")]
        public int moveRangeBonus;
        [Tooltip("Rule: range counts diagonals as one cell (range 1 = all eight neighbors).")]
        public bool melee;
        [Tooltip("Rule: ignores the target's cover.")]
        public bool ignoresCover;
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

        [Header("Chasm (BALANCE 1-15 ~ 1-17)")]
        [Tooltip("Share (%) of all rooms that get chasm cells. Start and stairs rooms are never picked.")]
        [SerializeField] private int chasmRoomPercent = 50;
        [Tooltip("Chasm area (%) of a chasm room, lower bound.")]
        [SerializeField] private int minChasmPercent = 30;
        [Tooltip("Chasm area (%) of a chasm room, upper bound.")]
        [SerializeField] private int maxChasmPercent = 50;

        [Header("Supply crates (BALANCE 1-18)")]
        [SerializeField] private int minSupplyCrates = 2;
        [SerializeField] private int maxSupplyCrates = 3;

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
            new UnitStatsEntry
            {
                kind = UnitKind.Assault, nameKey = "unit.assault.name", maxHp = 8, maxStability = 2, visionRange = 9,
                attack = 6, defense = 1, moveRange = 8, attackRange = 1, stabilityDamage = 1, critPercent = 15, critMultiplierPercent = 150,
                pushOnHit = 1,
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

        [Header("Guard (BALANCE 7)")]
        [Tooltip("Chance (%) that each spawned enemy starts guarding.")]
        [SerializeField] private int guardPercent = 50;
        [SerializeField] private int guardRadius = 3;

        [Header("Destructible cover (BALANCE 8)")]
        [Tooltip("Hits a crate takes before it breaks.")]
        [SerializeField] private int crateDurability = 2;
        [Tooltip("Hits a low wall takes before it breaks.")]
        [SerializeField] private int lowWallDurability = 3;

        [Header("Push skill (BALANCE 9)")]
        [Tooltip("Cells the push area reaches in front of the player.")]
        [SerializeField] private int pushLength = 3;
        [Tooltip("Lanes the push area covers (the player's lane plus the aimed side).")]
        [SerializeField] private int pushWidth = 2;
        [SerializeField] private int pushDamagePercent = 30;
        [Tooltip("Flat damage when a pushed unit hits a wall, cover or unit.")]
        [SerializeField] private int pushCollisionDamage = 3;
        [SerializeField] private int pushCooldownRounds = 2;

        [Header("Assault and falling (BALANCE 10)")]
        [Tooltip("Chance (%) that each spawned enemy is an assault unit instead of a rifleman.")]
        [SerializeField] private int assaultPercent = 30;
        [Tooltip("Damage (% of max HP) the player takes on falling into a chasm.")]
        [SerializeField] private int fallDamagePercent = 50;

        [Header("Floor progression (BALANCE 11)")]
        [Tooltip("Floors in a run. Taking the stairs on the last floor wins the run.")]
        [SerializeField] private int floorCount = 5;
        [Tooltip("Enemies added per floor deeper (both min and max).")]
        [SerializeField] private int enemiesPerFloorStep = 1;
        [SerializeField] private int assaultPercentPerFloor = 5;
        [SerializeField] private int guardPercentDropPerFloor = 10;
        [Tooltip("Enemy max HP and attack added per floor deeper (%).")]
        [SerializeField] private int enemyStatPercentPerFloor = 10;

        [Header("Weapons and loot (BALANCE 12)")]
        [SerializeField] private WeaponKind startWeapon = WeaponKind.AssaultRifle;
        [SerializeField] private WeaponEntry[] weapons =
        {
            new WeaponEntry
            {
                kind = WeaponKind.AssaultRifle, nameKey = "weapon.assault_rifle.name", ruleKey = "weapon.assault_rifle.rule",
                attack = 10, attackRange = 8, stabilityDamage = 2, critPercent = 15, critMultiplierPercent = 150,
            },
            new WeaponEntry
            {
                kind = WeaponKind.Pistol, nameKey = "weapon.pistol.name", ruleKey = "weapon.pistol.rule",
                attack = 7, attackRange = 6, stabilityDamage = 1, critPercent = 15, critMultiplierPercent = 150, moveAfterShot = true,
            },
            new WeaponEntry
            {
                kind = WeaponKind.Shotgun, nameKey = "weapon.shotgun.name", ruleKey = "weapon.shotgun.rule",
                attack = 14, attackRange = 4, stabilityDamage = 3, critPercent = 15, critMultiplierPercent = 150,
                falloffStartCells = 2, falloffPercentPerCell = 15,
            },
            new WeaponEntry
            {
                kind = WeaponKind.Sniper, nameKey = "weapon.sniper.name", ruleKey = "weapon.sniper.rule",
                attack = 13, attackRange = 12, stabilityDamage = 1, critPercent = 30, critMultiplierPercent = 150, noShotAfterMove = true,
            },
            new WeaponEntry
            {
                kind = WeaponKind.Smg, nameKey = "weapon.smg.name", ruleKey = "weapon.smg.rule",
                attack = 7, attackRange = 6, stabilityDamage = 2, critPercent = 15, critMultiplierPercent = 150, extraShotsPerTurn = 1,
            },
            new WeaponEntry
            {
                kind = WeaponKind.MachineGun, nameKey = "weapon.machine_gun.name", ruleKey = "weapon.machine_gun.rule",
                attack = 9, attackRange = 8, stabilityDamage = 4, critPercent = 15, critMultiplierPercent = 150, moveRangeBonus = -2,
            },
            new WeaponEntry
            {
                kind = WeaponKind.Blade, nameKey = "weapon.blade.name", ruleKey = "weapon.blade.rule",
                attack = 16, attackRange = 1, stabilityDamage = 3, critPercent = 20, critMultiplierPercent = 150, melee = true, ignoresCover = true,
            },
        };
        [Tooltip("Chance (%) that winning a combat drops a weapon.")]
        [SerializeField] private int combatDropWeaponPercent = 30;
        [Tooltip("Chance (%) that winning a combat drops a medkit. The rest drops nothing.")]
        [SerializeField] private int combatDropMedkitPercent = 40;
        [Tooltip("Chance (%) that a supply crate holds a weapon. The rest holds a medkit.")]
        [SerializeField] private int crateWeaponPercent = 50;
        [Tooltip("Medkit heal (% of max HP).")]
        [SerializeField] private int medkitHealPercent = 40;

        [Header("Movement (BALANCE 2)")]
        [SerializeField] private int moveCostTicks = 1;
        [SerializeField] private float stepSeconds = 0.14f;

        [Header("View (BALANCE 3)")]
        [SerializeField] private float wallHeight = 1.2f;
        [SerializeField] private float lowCoverHeight = 0.55f;
        [SerializeField] private float crateSize = 0.8f;
        [SerializeField] private float pillarSize = 0.7f;
        [SerializeField] private float lowWallThickness = 0.14f;
        [Tooltip("How deep the chasm pit is drawn (view only).")]
        [SerializeField] private float chasmDepth = 2.5f;
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
        public float ChasmDepth => chasmDepth;
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
                GuardPercent = guardPercent,
                GuardRadius = guardRadius,
                CrateDurability = crateDurability,
                LowWallDurability = lowWallDurability,
                PushLength = pushLength,
                PushWidth = pushWidth,
                PushDamagePercent = pushDamagePercent,
                PushCollisionDamage = pushCollisionDamage,
                PushCooldownRounds = pushCooldownRounds,
                AssaultPercent = assaultPercent,
                FallDamagePercent = fallDamagePercent,
                FloorCount = floorCount,
                EnemiesPerFloorStep = enemiesPerFloorStep,
                AssaultPercentPerFloor = assaultPercentPerFloor,
                GuardPercentDropPerFloor = guardPercentDropPerFloor,
                EnemyStatPercentPerFloor = enemyStatPercentPerFloor,
                StartWeapon = startWeapon,
                CombatDropWeaponPercent = combatDropWeaponPercent,
                CombatDropMedkitPercent = combatDropMedkitPercent,
                CrateWeaponPercent = crateWeaponPercent,
                MedkitHealPercent = medkitHealPercent,
            };
            foreach (var entry in weapons)
            {
                settings.Weapons.Add(new WeaponStats
                {
                    Kind = entry.kind,
                    Attack = entry.attack,
                    AttackRange = entry.attackRange,
                    StabilityDamage = entry.stabilityDamage,
                    CritPercent = entry.critPercent,
                    CritMultiplierPercent = entry.critMultiplierPercent,
                    MoveAfterShot = entry.moveAfterShot,
                    NoShotAfterMove = entry.noShotAfterMove,
                    FalloffStartCells = entry.falloffStartCells,
                    FalloffPercentPerCell = entry.falloffPercentPerCell,
                    ExtraShotsPerTurn = entry.extraShotsPerTurn,
                    MoveRangeBonus = entry.moveRangeBonus,
                    Melee = entry.melee,
                    IgnoresCover = entry.ignoresCover,
                });
            }
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
                    PushOnHit = entry.pushOnHit,
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

        /// <summary>무기 이름의 텍스트 표 키. 표에 없는 무기면 null.</summary>
        public string WeaponNameKey(WeaponKind kind)
        {
            foreach (var entry in weapons) if (entry.kind == kind) return entry.nameKey;
            return null;
        }

        /// <summary>무기 규칙 설명의 텍스트 표 키. 표에 없는 무기면 null.</summary>
        public string WeaponRuleKey(WeaponKind kind)
        {
            foreach (var entry in weapons) if (entry.kind == kind) return entry.ruleKey;
            return null;
        }

        public System.Collections.Generic.IEnumerable<string> WeaponTextKeys()
        {
            foreach (var entry in weapons)
            {
                yield return entry.nameKey;
                yield return entry.ruleKey;
            }
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
                ChasmRoomPercent = chasmRoomPercent,
                MinChasmPercent = minChasmPercent,
                MaxChasmPercent = maxChasmPercent,
                MinSupplyCrates = minSupplyCrates,
                MaxSupplyCrates = maxSupplyCrates,
            };
        }
    }
}
