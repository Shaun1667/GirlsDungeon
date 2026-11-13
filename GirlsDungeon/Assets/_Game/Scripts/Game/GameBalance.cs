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
        [Tooltip("Visual id looked up in the UnitVisuals catalog (no prefab there = greybox shape).")]
        public string visualId;
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

    /// <summary>고를 수 있는 캐릭터 하나 (표 한 줄, BALANCE 13번). 유닛 표 플레이어 줄의 몸 능력치와 시작 무기를 덮어쓴다.</summary>
    [Serializable]
    public sealed class CharacterEntry
    {
        public CharacterKind kind;
        [Tooltip("Text table key for the character name.")]
        public string nameKey;
        [Tooltip("Visual id looked up in the UnitVisuals catalog (no prefab there = greybox shape).")]
        public string visualId;
        public int maxHp;
        public int maxStability;
        public int visionRange;
        public int defense;
        public int moveRange;
        public WeaponKind startWeapon;
        [Tooltip("Skill bar slots 2, 3 and 4 in order (None = empty). Shoot (1) and overwatch (5) are shared.")]
        public SkillKind[] skills = { SkillKind.Grenade, SkillKind.Push, SkillKind.None };
        [Tooltip("Part workshop success chance (%): change option, change set.")]
        public int workshopOptionPercent = 70;
        public int workshopSetPercent = 50;
        [Tooltip("Chance (%) to force a locked door open, one try per door. 0 = cannot force.")]
        public int doorForcePercent;
    }

    /// <summary>파츠 능력치 하나의 값 범위 (표 한 줄, BALANCE 17번).</summary>
    [Serializable]
    public sealed class PartStatEntry
    {
        public PartStat stat;
        public int min;
        public int max;
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
        [Tooltip("Text table keys for the four part slot names (slot 1 = muzzle or blade tip).")]
        public string[] partSlotKeys = { "part.slot.muzzle", "part.slot.sight", "part.slot.grip", "part.slot.handguard" };
    }

    /// <summary>게임 수치의 한 곳 (BALANCE.md와 함께 관리). 코드에 숫자를 직접 넣지 않는다.</summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "GirlsDungeon/Game Balance")]
    public sealed class GameBalance : ScriptableObject
    {
        [Header("Run")]
        [Tooltip("0 picks a new seed every run. Any other value fixes the seed.")]
        [SerializeField] private int fixedSeed;

        [Header("Map (BALANCE 1)")]
        [SerializeField] private int mapWidth = 40;
        [SerializeField] private int mapHeight = 40;
        [SerializeField] private int minRooms = 9;
        [SerializeField] private int maxRooms = 11;
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

        [Header("Part workshop room (BALANCE 1-19)")]
        [Tooltip("Workshop rooms per floor (one workbench each, no enemies).")]
        [SerializeField] private int workshopsPerFloor = 1;

        [Header("Special rooms (BALANCE 1-21 ~ 1-24)")]
        [Tooltip("Special rooms on every floor, after the workshop.")]
        [SerializeField] private SpecialRoomKind[] alwaysSpecialRooms = { SpecialRoomKind.Treasure };
        [Tooltip("Pool of special rooms picked at random each floor (no repeats).")]
        [SerializeField] private SpecialRoomKind[] randomSpecialRooms = { SpecialRoomKind.Storehouse, SpecialRoomKind.Repair, SpecialRoomKind.Observatory };
        [SerializeField] private int randomSpecialRoomsPerFloor = 2;
        [Tooltip("Normal rooms kept on each floor besides the start and stairs rooms.")]
        [SerializeField] private int minNormalRooms = 2;
        [Tooltip("Special room kinds whose entrances are locked doors.")]
        [SerializeField] private SpecialRoomKind[] lockedSpecialRooms =
        {
            SpecialRoomKind.Workshop, SpecialRoomKind.Treasure, SpecialRoomKind.SetGallery, SpecialRoomKind.Altar,
        };

        [Header("Units (BALANCE 4)")]
        [SerializeField] private UnitStatsEntry[] units =
        {
            new UnitStatsEntry
            {
                kind = UnitKind.Player, nameKey = "unit.player.name", visualId = "player", maxHp = 100, maxStability = 6, visionRange = 9,
                attack = 10, defense = 4, moveRange = 6, attackRange = 8, stabilityDamage = 2, critPercent = 15, critMultiplierPercent = 150,
            },
            new UnitStatsEntry
            {
                kind = UnitKind.Rifleman, nameKey = "unit.rifleman.name", visualId = "rifleman", maxHp = 10, maxStability = 3, visionRange = 9,
                attack = 6, defense = 2, moveRange = 6, attackRange = 7, stabilityDamage = 1, critPercent = 15, critMultiplierPercent = 150,
            },
            new UnitStatsEntry
            {
                kind = UnitKind.Assault, nameKey = "unit.assault.name", visualId = "assault", maxHp = 8, maxStability = 2, visionRange = 9,
                attack = 6, defense = 1, moveRange = 8, attackRange = 1, stabilityDamage = 1, critPercent = 15, critMultiplierPercent = 150,
                pushOnHit = 1,
            },
        };

        [Header("Characters (BALANCE 13)")]
        [Tooltip("Characters picked on the start screen. Each row replaces the player row's body stats and start weapon.")]
        [SerializeField] private CharacterEntry[] characters =
        {
            new CharacterEntry
            {
                kind = CharacterKind.HeroA, nameKey = "character.hero_a.name", visualId = "hero_a",
                maxHp = 110, maxStability = 7, visionRange = 8, defense = 5, moveRange = 8, startWeapon = WeaponKind.Shotgun,
                skills = new[] { SkillKind.Push, SkillKind.Sonic, SkillKind.Grenade },
                workshopOptionPercent = 55, workshopSetPercent = 35,
                doorForcePercent = 70,
            },
            new CharacterEntry
            {
                kind = CharacterKind.HeroB, nameKey = "character.hero_b.name", visualId = "hero_b",
                maxHp = 90, maxStability = 5, visionRange = 10, defense = 3, moveRange = 6, startWeapon = WeaponKind.Smg,
            },
            new CharacterEntry
            {
                kind = CharacterKind.HeroC, nameKey = "character.hero_c.name", visualId = "hero_c",
                maxHp = 100, maxStability = 6, visionRange = 9, defense = 4, moveRange = 6, startWeapon = WeaponKind.AssaultRifle,
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

        [Header("Sonic skill (BALANCE 15)")]
        [SerializeField] private int sonicRange = 6;
        [Tooltip("Damage multiplier (%) on the held weapon attack. Ignores cover and the stability guard.")]
        [SerializeField] private int sonicDamagePercent = 60;
        [SerializeField] private int sonicStabilityDamage = 3;
        [SerializeField] private int sonicCooldownRounds = 2;

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
                partSlotKeys = new[] { "part.slot.muzzle", "part.slot.sight", "part.slot.coupler", "part.slot.latch" },
            },
            new WeaponEntry
            {
                kind = WeaponKind.Shotgun, nameKey = "weapon.shotgun.name", ruleKey = "weapon.shotgun.rule",
                attack = 14, attackRange = 4, stabilityDamage = 3, critPercent = 15, critMultiplierPercent = 150,
                falloffStartCells = 2, falloffPercentPerCell = 15,
                partSlotKeys = new[] { "part.slot.muzzle", "part.slot.sight", "part.slot.coupler", "part.slot.latch" },
            },
            new WeaponEntry
            {
                kind = WeaponKind.Sniper, nameKey = "weapon.sniper.name", ruleKey = "weapon.sniper.rule",
                attack = 13, attackRange = 12, stabilityDamage = 1, critPercent = 30, critMultiplierPercent = 150, noShotAfterMove = true,
                partSlotKeys = new[] { "part.slot.muzzle", "part.slot.sight", "part.slot.bipod", "part.slot.handguard" },
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
                partSlotKeys = new[] { "part.slot.muzzle", "part.slot.sight", "part.slot.bipod", "part.slot.handguard" },
            },
            new WeaponEntry
            {
                kind = WeaponKind.Blade, nameKey = "weapon.blade.name", ruleKey = "weapon.blade.rule",
                attack = 16, attackRange = 1, stabilityDamage = 3, critPercent = 20, critMultiplierPercent = 150, melee = true, ignoresCover = true,
                partSlotKeys = new[] { "part.slot.blade_tip", "part.slot.tuning", "part.slot.coupler", "part.slot.latch" },
            },
        };
        [Tooltip("Chance (%) that winning a combat drops a weapon.")]
        [SerializeField] private int combatDropWeaponPercent = 10;
        [Tooltip("Chance (%) that winning a combat drops a medkit. The rest drops nothing.")]
        [SerializeField] private int combatDropMedkitPercent = 20;
        [Tooltip("Chance (%) that a supply crate holds a weapon. Then the part range, the rest holds a medkit.")]
        [SerializeField] private int crateWeaponPercent = 10;
        [Tooltip("Medkit heal (% of max HP).")]
        [SerializeField] private int medkitHealPercent = 40;

        [Header("Weapon parts (BALANCE 17)")]
        [Tooltip("Chance (%) that winning a combat drops a part (after the weapon and medkit ranges).")]
        [SerializeField] private int combatDropPartPercent = 60;
        [Tooltip("Chance (%) that a supply crate holds a part (after the weapon range). The rest holds a medkit.")]
        [SerializeField] private int cratePartPercent = 60;
        [Tooltip("Value range rolled for each part stat.")]
        [SerializeField] private PartStatEntry[] partStats =
        {
            new PartStatEntry { stat = PartStat.Attack, min = 1, max = 2 },
            new PartStatEntry { stat = PartStat.AttackRange, min = 1, max = 1 },
            new PartStatEntry { stat = PartStat.CritPercent, min = 3, max = 6 },
            new PartStatEntry { stat = PartStat.CritMultiplier, min = 10, max = 20 },
            new PartStatEntry { stat = PartStat.StabilityDamage, min = 1, max = 1 },
            new PartStatEntry { stat = PartStat.MaxHp, min = 5, max = 10 },
            new PartStatEntry { stat = PartStat.Defense, min = 1, max = 1 },
            new PartStatEntry { stat = PartStat.MoveRange, min = 1, max = 1 },
            new PartStatEntry { stat = PartStat.MaxStability, min = 1, max = 1 },
        };
        [Tooltip("Stats that slot 1 (muzzle or blade tip) can roll.")]
        [SerializeField] private PartStat[] partPrimaryStats = { PartStat.Attack, PartStat.AttackRange, PartStat.CritPercent, PartStat.CritMultiplier, PartStat.StabilityDamage };
        [Tooltip("Stats that slots 2 to 4 can roll (body stats included).")]
        [SerializeField] private PartStat[] partSecondaryStats =
        {
            PartStat.Attack, PartStat.AttackRange, PartStat.CritPercent, PartStat.CritMultiplier, PartStat.StabilityDamage,
            PartStat.MaxHp, PartStat.Defense, PartStat.MoveRange, PartStat.MaxStability,
        };
        [Tooltip("Grade chance (%) in order: industrial, military, precision. Sum 100.")]
        [SerializeField] private int[] partGradePercents = { 60, 30, 10 };
        [Tooltip("Stat count on slot 1 by grade: industrial, military, precision.")]
        [SerializeField] private int[] partPrimaryStatCounts = { 2, 3, 4 };
        [Tooltip("Stat count on slots 2 to 4 by grade: industrial, military, precision.")]
        [SerializeField] private int[] partSecondaryStatCounts = { 1, 2, 3 };

        [Header("Part sets (BALANCE 18)")]
        [Tooltip("How many parts of one set (slots 2 to 4) turn the set on.")]
        [SerializeField] private int setPiecesRequired = 3;
        [Tooltip("Chain: skill damage per stack (%), stacks gained per skill used, max stacks.")]
        [SerializeField] private int setChainPercentPerStack = 5;
        [SerializeField] private int setChainMaxStacks = 4;
        [Tooltip("Two-sided: single damage when no cover near the target, area damage when cover is near (%).")]
        [SerializeField] private int setTwoSidedPercent = 10;
        [Tooltip("Fracture: damage to enemies whose stability is below max (%).")]
        [SerializeField] private int setFracturePercent = 15;
        [Tooltip("Emergency: stability gained when a medkit fills HP.")]
        [SerializeField] private int setEmergencyStability = 2;
        [Tooltip("Bulwark: area damage taken reduction (%) and player turns it lasts after setting overwatch.")]
        [SerializeField] private int setBulwarkPercent = 20;
        [SerializeField] private int setBulwarkTurns = 2;
        [Tooltip("Ambush: overwatch shot damage (%).")]
        [SerializeField] private int setAmbushPercent = 25;
        [Tooltip("Assault: all damage (%) and extra for melee weapon attacks (%).")]
        [SerializeField] private int setAssaultPercent = 12;
        [SerializeField] private int setAssaultMeleePercent = 24;

        [Header("Part workshop (BALANCE 19)")]
        [Tooltip("Success chance (%) when no character is picked: change option, change set. Characters have their own.")]
        [SerializeField] private int workshopOptionPercent = 70;
        [SerializeField] private int workshopSetPercent = 50;
        [Tooltip("Materials spent per try (success or fail): change option, change set.")]
        [SerializeField] private int workshopOptionCost = 2;
        [SerializeField] private int workshopSetCost = 3;
        [Tooltip("Materials from disassembling a part by grade: industrial, military, precision.")]
        [SerializeField] private int[] disassembleMaterials = { 1, 2, 3 };
        [Tooltip("Spare part storage slots.")]
        [SerializeField] private int spareSlots = 4;

        [Header("Repair bay (BALANCE 23)")]
        [Tooltip("HP restored by a repair station (% of max HP). Stability is filled to max.")]
        [SerializeField] private int repairHealPercent = 100;

        [Header("Storehouse (BALANCE 22)")]
        [Tooltip("Part materials gained when opening a supply rack.")]
        [SerializeField] private int storehouseMaterials = 6;
        [Tooltip("Medkits dropped next to a supply rack.")]
        [SerializeField] private int storehouseMedkits = 2;

        [Header("Treasure room (BALANCE 21)")]
        [Tooltip("Grade of the part a treasure chest always holds (for the weapon in hand).")]
        [SerializeField] private PartGrade treasurePartGrade = PartGrade.Precision;
        [Tooltip("Chance (%) that a treasure chest also holds a weapon.")]
        [SerializeField] private int treasureWeaponPercent = 50;

        [Header("Locked doors and keys (BALANCE 20)")]
        [Tooltip("Chance (%) that winning a combat also drops a key (rolled apart from other rewards).")]
        [SerializeField] private int combatDropKeyPercent = 10;

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
        [Tooltip("Closest and farthest camera distance for mouse-wheel zoom (BALANCE 3-21, 3-22).")]
        [SerializeField] private float cameraMinDistance = 8f;
        [SerializeField] private float cameraMaxDistance = 28f;
        [Tooltip("Distance change per wheel notch (BALANCE 3-23).")]
        [SerializeField] private float cameraZoomStep = 2f;
        [SerializeField] private float cameraZoomSharpness = 10f;
        [Tooltip("Pixels the mouse must move while held before a click becomes a camera drag (BALANCE 3-25).")]
        [SerializeField] private float cameraDragThresholdPixels = 8f;
        [Tooltip("How far the camera can be dragged away from its target (BALANCE 3-26).")]
        [SerializeField] private float cameraMaxPanDistance = 20f;

        [Header("Turn playback (BALANCE 3-14 ~ 3-20, view only)")]
        [Tooltip("Pause after the enemy-turn banner appears.")]
        [SerializeField] private float enemyTurnBannerSeconds = 0.6f;
        [Tooltip("Pause after each attack (tracer and damage number).")]
        [SerializeField] private float attackPauseSeconds = 0.55f;
        [Tooltip("Pause after overwatch, explosions, pushes and deaths.")]
        [SerializeField] private float actionPauseSeconds = 0.3f;
        [SerializeField] private float tracerSeconds = 0.2f;
        [SerializeField] private float damagePopupSeconds = 1f;
        [SerializeField] private float shakeSeconds = 0.2f;
        [SerializeField] private float shakeDistance = 0.08f;

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
        public float CameraMinDistance => cameraMinDistance;
        public float CameraMaxDistance => cameraMaxDistance;
        public float CameraZoomStep => cameraZoomStep;
        public float CameraZoomSharpness => cameraZoomSharpness;
        public float CameraDragThresholdPixels => cameraDragThresholdPixels;
        public float CameraMaxPanDistance => cameraMaxPanDistance;
        public float EnemyTurnBannerSeconds => enemyTurnBannerSeconds;
        public float AttackPauseSeconds => attackPauseSeconds;
        public float ActionPauseSeconds => actionPauseSeconds;
        public float TracerSeconds => tracerSeconds;
        public float DamagePopupSeconds => damagePopupSeconds;
        public float ShakeSeconds => shakeSeconds;
        public float ShakeDistance => shakeDistance;

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
                SonicRange = sonicRange,
                SonicDamagePercent = sonicDamagePercent,
                SonicStabilityDamage = sonicStabilityDamage,
                SonicCooldownRounds = sonicCooldownRounds,
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
                CombatDropPartPercent = combatDropPartPercent,
                CratePartPercent = cratePartPercent,
                PartGradePercents = (int[])partGradePercents.Clone(),
                PartPrimaryStatCounts = (int[])partPrimaryStatCounts.Clone(),
                PartSecondaryStatCounts = (int[])partSecondaryStatCounts.Clone(),
                SetPiecesRequired = setPiecesRequired,
                SetChainPercentPerStack = setChainPercentPerStack,
                SetChainMaxStacks = setChainMaxStacks,
                SetTwoSidedPercent = setTwoSidedPercent,
                SetFracturePercent = setFracturePercent,
                SetEmergencyStability = setEmergencyStability,
                SetBulwarkPercent = setBulwarkPercent,
                SetBulwarkTurns = setBulwarkTurns,
                SetAmbushPercent = setAmbushPercent,
                SetAssaultPercent = setAssaultPercent,
                SetAssaultMeleePercent = setAssaultMeleePercent,
                WorkshopOptionPercent = workshopOptionPercent,
                WorkshopSetPercent = workshopSetPercent,
                WorkshopOptionCost = workshopOptionCost,
                WorkshopSetCost = workshopSetCost,
                DisassembleMaterials = (int[])disassembleMaterials.Clone(),
                SpareSlots = spareSlots,
                CombatDropKeyPercent = combatDropKeyPercent,
                TreasurePartGrade = treasurePartGrade,
                TreasureWeaponPercent = treasureWeaponPercent,
                StorehouseMaterials = storehouseMaterials,
                RepairHealPercent = repairHealPercent,
                StorehouseMedkits = storehouseMedkits,
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
            foreach (var entry in characters)
            {
                settings.Characters.Add(new CharacterStats
                {
                    Kind = entry.kind,
                    MaxHp = entry.maxHp,
                    MaxStability = entry.maxStability,
                    VisionRange = entry.visionRange,
                    Defense = entry.defense,
                    MoveRange = entry.moveRange,
                    StartWeapon = entry.startWeapon,
                    Skills = new System.Collections.Generic.List<SkillKind>(entry.skills ?? System.Array.Empty<SkillKind>()),
                    WorkshopOptionPercent = entry.workshopOptionPercent,
                    WorkshopSetPercent = entry.workshopSetPercent,
                    DoorForcePercent = entry.doorForcePercent,
                });
            }
            foreach (var entry in partStats) settings.PartStatRanges.Add(new PartStatRange { Stat = entry.stat, Min = entry.min, Max = entry.max });
            settings.PartPrimaryStats.AddRange(partPrimaryStats);
            settings.PartSecondaryStats.AddRange(partSecondaryStats);
            return settings;
        }

        /// <summary>시작 화면에 보이는 캐릭터 (표 순서).</summary>
        public System.Collections.Generic.IEnumerable<CharacterKind> CharacterKinds()
        {
            foreach (var entry in characters) yield return entry.kind;
        }

        public int CharacterCount => characters.Length;

        /// <summary>캐릭터 이름의 텍스트 표 키. 표에 없으면 null.</summary>
        public string CharacterNameKey(CharacterKind kind)
        {
            foreach (var entry in characters) if (entry.kind == kind) return entry.nameKey;
            return null;
        }

        /// <summary>캐릭터 그림 ID. 표에 없으면(None 포함) 유닛 표 플레이어 줄의 그림 ID.</summary>
        public string PlayerVisualId(CharacterKind kind)
        {
            foreach (var entry in characters) if (entry.kind == kind) return entry.visualId;
            return VisualId(UnitKind.Player);
        }

        public System.Collections.Generic.IEnumerable<string> CharacterNameKeys()
        {
            foreach (var entry in characters) yield return entry.nameKey;
        }

        public System.Collections.Generic.IEnumerable<string> CharacterVisualIds()
        {
            foreach (var entry in characters) yield return entry.visualId;
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

        /// <summary>유닛 그림 ID (그림 목록 UnitVisualCatalog에서 찾는다). 표에 없는 종류면 null.</summary>
        public string VisualId(UnitKind kind)
        {
            foreach (var entry in units) if (entry.kind == kind) return entry.visualId;
            return null;
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

        /// <summary>그 무기의 파츠 칸 이름 키 (0 = 1번 칸). 표에 없으면 null.</summary>
        public string PartSlotKey(WeaponKind kind, int slot)
        {
            foreach (var entry in weapons)
                if (entry.kind == kind && entry.partSlotKeys != null && slot >= 0 && slot < entry.partSlotKeys.Length) return entry.partSlotKeys[slot];
            return null;
        }

        /// <summary>무기 표의 파츠 칸 이름 키 전부 (텍스트 표 검사용).</summary>
        public System.Collections.Generic.IEnumerable<string> PartSlotKeys()
        {
            foreach (var entry in weapons)
                if (entry.partSlotKeys != null)
                    foreach (var key in entry.partSlotKeys) yield return key;
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
                WorkshopsPerFloor = workshopsPerFloor,
                AlwaysSpecialRooms = new System.Collections.Generic.List<SpecialRoomKind>(alwaysSpecialRooms),
                RandomSpecialRooms = new System.Collections.Generic.List<SpecialRoomKind>(randomSpecialRooms),
                RandomSpecialRoomsPerFloor = randomSpecialRoomsPerFloor,
                MinNormalRooms = minNormalRooms,
                LockedSpecialRooms = new System.Collections.Generic.List<SpecialRoomKind>(lockedSpecialRooms),
            };
        }
    }
}
