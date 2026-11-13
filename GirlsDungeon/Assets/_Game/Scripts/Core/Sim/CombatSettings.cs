using System;
using System.Collections.Generic;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>유닛 종류 하나의 능력치 (표 한 줄). 값은 GameBalance 에셋에서 받는다 (BALANCE 4번).</summary>
    public sealed class UnitStats
    {
        public UnitKind Kind;
        public int MaxHp;
        public int MaxStability;
        public int VisionRange;
        public int Attack;
        public int Defense;
        public int MoveRange;
        public int AttackRange;
        public int StabilityDamage;
        public int CritPercent;
        public int CritMultiplierPercent;
        /// <summary>공격이 맞으면 상대를 미는 칸 수 (0 = 밀지 않음).</summary>
        public int PushOnHit;
    }

    /// <summary>
    /// 고를 수 있는 플레이어 캐릭터. 저장에 정수로 들어가므로 끝에만 추가한다. None = 캐릭터 표 없이 유닛 표의 플레이어 줄 그대로 (테스트 · 기본).
    /// 이름 · 그림은 화면 쪽 표(GameBalance.characters)에서 ID로 찾는다.
    /// </summary>
    public enum CharacterKind : byte
    {
        None = 0,
        HeroA = 1,
        HeroB = 2,
        HeroC = 3,
    }

    /// <summary>
    /// 스킬 바 2 · 3 · 4번 칸에 들어가는 스킬 (사격 1번 · 감시 5번은 모든 캐릭터 공통). 저장 · 데이터에 정수로 들어가므로 끝에만 추가한다.
    /// </summary>
    public enum SkillKind : byte
    {
        None = 0,
        /// <summary>수류탄 (AreaAttack).</summary>
        Grenade = 1,
        /// <summary>넉백 공격 = 밀치기 (Push).</summary>
        Push = 2,
        /// <summary>음파 공격: 보이는 적 하나에게 엄폐를 무시하는 피해 · 안정 피해 (SonicAttack, 3-12).</summary>
        Sonic = 3,
    }

    /// <summary>
    /// 캐릭터 하나의 몸 능력치와 시작 무기 (표 한 줄, BALANCE 13번). 유닛 표의 플레이어 줄 위에 덮어쓴다.
    /// 공격 · 사거리 · 치명은 무기에서 오므로(Loadout.Apply) 여기에는 없다.
    /// </summary>
    public sealed class CharacterStats
    {
        public CharacterKind Kind;
        public int MaxHp;
        public int MaxStability;
        public int VisionRange;
        public int Defense;
        public int MoveRange;
        public WeaponKind StartWeapon;

        /// <summary>스킬 바 2 · 3 · 4번 칸 (순서대로, None = 빈 칸). BALANCE 13번.</summary>
        public List<SkillKind> Skills = new List<SkillKind>();

        /// <summary>파츠 작업방 옵션 · 세트 바꾸기 성공 확률 (%, BALANCE 19번, 3-4c).</summary>
        public int WorkshopOptionPercent;
        public int WorkshopSetPercent;

        /// <summary>잠긴 문을 힘으로 열 확률 (%, 문당 1번, 0 = 못 함 — BALANCE 20번, 3-4d).</summary>
        public int DoorForcePercent;
    }

    /// <summary>전투 수치 (BALANCE 4번 · 5번).</summary>
    public sealed class CombatSettings
    {
        public List<UnitStats> Units = new List<UnitStats>();

        /// <summary>고를 수 있는 캐릭터 (BALANCE 13번). 비어 있으면 고르기 없이 유닛 표의 플레이어 줄로 시작한다.</summary>
        public List<CharacterStats> Characters = new List<CharacterStats>();
        public int MinEnemiesPerFloor;
        public int MaxEnemiesPerFloor;
        public int EnemyMinDistanceFromStart;
        public int EndTurnCostTicks;

        /// <summary>엄폐물 감소 — 낮은 엄폐(상자 · 낮은 벽).</summary>
        public int HalfCoverReductionPercent;

        /// <summary>엄폐물 감소 — 높은 엄폐(벽 · 기둥).</summary>
        public int FullCoverReductionPercent;

        /// <summary>엄폐 중이고 안정지수가 남아 있으면 더하는 감소.</summary>
        public int StabilityGuardPercent;

        public int MinDamage;

        // 범위 공격 — 수류탄 스킬 (BALANCE 6번)
        public int GrenadeThrowRange;
        public int GrenadeRadius;
        public int GrenadeDamagePercent;
        public int GrenadeStabilityDamage;
        public int GrenadeCooldownRounds;

        // 경계 상태 (BALANCE 7번)
        public int GuardPercent;
        public int GuardRadius;

        // 부서지는 엄폐물 (BALANCE 8번)
        public int CrateDurability;
        public int LowWallDurability;

        // 밀치기 스킬 (BALANCE 9번)
        public int PushLength;
        public int PushWidth;
        public int PushDamagePercent;
        public int PushCollisionDamage;
        public int PushCooldownRounds;

        // 음파 공격 스킬 (BALANCE 15번, 3-12)
        public int SonicRange;
        /// <summary>든 무기 공격력에 곱하는 피해 배율 (%). 엄폐 감소 · 안정지수 감소를 받지 않는다.</summary>
        public int SonicDamagePercent;
        public int SonicStabilityDamage;
        public int SonicCooldownRounds;

        // 돌격병 · 낙하 (BALANCE 10번)
        /// <summary>적마다 돌격병으로 나올 확률 (나머지는 사격병).</summary>
        public int AssaultPercent;
        /// <summary>플레이어가 낭떠러지에 떨어지면 받는 피해 (최대 체력의 %).</summary>
        public int FallDamagePercent;

        // 층 진행 (BALANCE 11번)
        /// <summary>판의 층 수. 마지막 층의 계단 = 승리.</summary>
        public int FloorCount;
        /// <summary>층이 하나 깊어질 때마다 더하는 적 수 (최소 · 최대 모두).</summary>
        public int EnemiesPerFloorStep;
        /// <summary>층마다 더하는 돌격병 확률 (%p).</summary>
        public int AssaultPercentPerFloor;
        /// <summary>층마다 빼는 경계 시작 확률 (%p, 0 아래로는 안 내려감).</summary>
        public int GuardPercentDropPerFloor;
        /// <summary>층마다 더하는 적 최대 체력 · 공격력 (%).</summary>
        public int EnemyStatPercentPerFloor;

        // 무기 · 보상 (BALANCE 12번)
        public List<WeaponStats> Weapons = new List<WeaponStats>();
        /// <summary>판을 시작할 때 드는 무기.</summary>
        public WeaponKind StartWeapon;
        /// <summary>전투에서 이기면 무기가 떨어질 확률 (%).</summary>
        public int CombatDropWeaponPercent;
        /// <summary>전투에서 이기면 구급상자가 떨어질 확률 (%). 나머지는 아무것도 없음.</summary>
        public int CombatDropMedkitPercent;
        /// <summary>보급 상자에서 무기가 나올 확률 (%). 나머지는 구급상자 (강화는 3-4에서).</summary>
        public int CrateWeaponPercent;
        /// <summary>구급상자 회복량 (최대 체력의 %).</summary>
        public int MedkitHealPercent;

        // 무기 파츠 (BALANCE 17번, 3-4)
        /// <summary>전투에서 이기면 파츠가 떨어질 확률 (%) — 무기 · 구급상자 다음 구간. 나머지는 아무것도 없음.</summary>
        public int CombatDropPartPercent;
        /// <summary>보급 상자에서 파츠가 나올 확률 (%) — 무기 다음 구간. 나머지는 구급상자.</summary>
        public int CratePartPercent;
        /// <summary>파츠 능력치 값 범위 (능력치마다 한 줄).</summary>
        public List<PartStatRange> PartStatRanges = new List<PartStatRange>();
        /// <summary>1번 칸(총구 · 칼끝)에 나올 수 있는 능력치.</summary>
        public List<PartStat> PartPrimaryStats = new List<PartStat>();
        /// <summary>2 · 3 · 4번 칸에 나올 수 있는 능력치.</summary>
        public List<PartStat> PartSecondaryStats = new List<PartStat>();
        /// <summary>등급이 나올 확률 (%), PartGrade 순서 (공업 · 군용 · 정밀). 합 100.</summary>
        public int[] PartGradePercents = new int[3];
        /// <summary>1번 칸 능력치 개수, PartGrade 순서 (공업 2 · 군용 3 · 정밀 4).</summary>
        public int[] PartPrimaryStatCounts = new int[3];
        /// <summary>2 · 3 · 4번 칸 능력치 개수, PartGrade 순서 (공업 1 · 군용 2 · 정밀 3).</summary>
        public int[] PartSecondaryStatCounts = new int[3];

        // 파츠 세트 효과 (BALANCE 18번, 3-4b — SetEffects)
        /// <summary>같은 세트 파츠를 몇 개 끼우면 켜지는가 (2 · 3 · 4번 칸 = 3).</summary>
        public int SetPiecesRequired;
        public int SetChainPercentPerStack;
        public int SetChainMaxStacks;
        public int SetTwoSidedPercent;
        public int SetFracturePercent;
        public int SetEmergencyStability;
        public int SetBulwarkPercent;
        /// <summary>방호 태세가 걸려 있는 플레이어 차례 수 (감시를 건 뒤).</summary>
        public int SetBulwarkTurns;
        public int SetAmbushPercent;
        public int SetAssaultPercent;
        /// <summary>백병 강습: 근접 무기(도검) 공격이면 SetAssaultPercent 위에 더하는 값.</summary>
        public int SetAssaultMeleePercent;

        // 파츠 작업방 (BALANCE 19번, 3-4c — Workshop)
        /// <summary>캐릭터를 고르지 않은 판의 옵션 · 세트 바꾸기 성공 확률 (%).</summary>
        public int WorkshopOptionPercent;
        public int WorkshopSetPercent;
        /// <summary>옵션 · 세트 바꾸기에 드는 재료 (성공 · 실패 모두).</summary>
        public int WorkshopOptionCost;
        public int WorkshopSetCost;
        /// <summary>분해로 얻는 재료, PartGrade 순서 (공업 · 군용 · 정밀).</summary>
        public int[] DisassembleMaterials = new int[3];
        /// <summary>예비 파츠 보관함 칸 수.</summary>
        public int SpareSlots;

        // 보물방 (BALANCE 21번, 3-14b — Loot.OpenTreasure)
        /// <summary>보물 상자 파츠의 등급 (든 무기용, 확정 1개).</summary>
        public PartGrade TreasurePartGrade;
        /// <summary>보물 상자에서 무기가 더 나올 확률 (%).</summary>
        public int TreasureWeaponPercent;

        // 잠긴 문 · 열쇠 (BALANCE 20번, 3-4d — Doors)
        /// <summary>전투에서 이기면 열쇠가 더 떨어질 확률 (%) — 다른 보상과 따로 굴린다 (문 난수).</summary>
        public int CombatDropKeyPercent;

        public PartStatRange PartRange(PartStat stat)
        {
            foreach (var range in PartStatRanges) if (range.Stat == stat) return range;
            throw new ArgumentException($"No part range for {stat}.");
        }

        public WeaponStats WeaponFor(WeaponKind kind)
        {
            var weapon = TryWeapon(kind);
            if (weapon == null) throw new ArgumentException($"No stats for weapon {kind}.");
            return weapon;
        }

        public WeaponStats TryWeapon(WeaponKind kind)
        {
            foreach (var weapon in Weapons) if (weapon.Kind == kind) return weapon;
            return null;
        }

        public UnitStats StatsFor(UnitKind kind)
        {
            foreach (var stats in Units) if (stats.Kind == kind) return stats;
            throw new ArgumentException($"No stats for unit kind {kind}.");
        }

        /// <summary>캐릭터 표의 줄. None이거나 표에 없으면 null.</summary>
        public CharacterStats TryCharacter(CharacterKind kind)
        {
            if (kind == CharacterKind.None) return null;
            foreach (var character in Characters) if (character.Kind == kind) return character;
            return null;
        }

        /// <summary>캐릭터를 고르지 않은 판(None)의 스킬 칸 = 3-11까지의 스킬 바 (수류탄 · 밀치기).</summary>
        public static readonly IReadOnlyList<SkillKind> DefaultSkills = new[] { SkillKind.Grenade, SkillKind.Push, SkillKind.None };

        /// <summary>스킬 바 2 · 3 · 4번 칸. 캐릭터 줄이 있으면 그 칸, 없으면 DefaultSkills.</summary>
        public IReadOnlyList<SkillKind> SkillSlots(CharacterKind kind)
        {
            var character = TryCharacter(kind);
            return character != null ? character.Skills : DefaultSkills;
        }

        /// <summary>그 캐릭터가 이 스킬을 칸에 가지고 있는지 (스킬 커맨드가 확인한다).</summary>
        public bool HasSkill(CharacterKind kind, SkillKind skill)
        {
            if (skill == SkillKind.None) return false;
            foreach (var slot in SkillSlots(kind)) if (slot == skill) return true;
            return false;
        }

        /// <summary>무기 보정 전 플레이어 이동 범위: 캐릭터 줄이 있으면 그 값, 없으면 유닛 표의 플레이어 줄.</summary>
        public int PlayerBaseMoveRange(CharacterKind kind)
        {
            var character = TryCharacter(kind);
            return character != null ? character.MoveRange : StatsFor(UnitKind.Player).MoveRange;
        }

        public void Validate()
        {
            StatsFor(UnitKind.Player);
            StatsFor(UnitKind.Rifleman);
            if (AssaultPercent > 0) StatsFor(UnitKind.Assault);
            foreach (var stats in Units)
            {
                if (stats.MaxHp < 1 || stats.MaxStability < 0 || stats.VisionRange < 1 || stats.Attack < 1 || stats.Defense < 0
                    || stats.MoveRange < 0 || stats.AttackRange < 1 || stats.StabilityDamage < 0
                    || stats.CritPercent < 0 || stats.CritPercent > 100 || stats.CritMultiplierPercent < 100 || stats.PushOnHit < 0)
                    throw new ArgumentException($"Stats for {stats.Kind} are invalid.");
            }
            if (MinEnemiesPerFloor < 0 || MaxEnemiesPerFloor < MinEnemiesPerFloor || EnemyMinDistanceFromStart < 0 || EndTurnCostTicks < 0
                || HalfCoverReductionPercent < 0 || FullCoverReductionPercent < 0 || StabilityGuardPercent < 0
                || HalfCoverReductionPercent + StabilityGuardPercent > 100 || FullCoverReductionPercent + StabilityGuardPercent > 100
                || MinDamage < 0)
                throw new ArgumentException("Combat settings are invalid.");
            if (GrenadeThrowRange < 1 || GrenadeRadius < 0 || GrenadeDamagePercent < 0 || GrenadeStabilityDamage < 0 || GrenadeCooldownRounds < 0)
                throw new ArgumentException("Grenade settings are invalid.");
            if (GuardPercent < 0 || GuardPercent > 100 || GuardRadius < 0)
                throw new ArgumentException("Guard settings are invalid.");
            if (CrateDurability < 1 || LowWallDurability < 1)
                throw new ArgumentException("Cover durability must be at least 1.");
            if (PushLength < 1 || PushWidth < 1 || PushDamagePercent < 0 || PushCollisionDamage < 0 || PushCooldownRounds < 0)
                throw new ArgumentException("Push settings are invalid.");
            if (SonicRange < 1 || SonicDamagePercent < 0 || SonicStabilityDamage < 0 || SonicCooldownRounds < 0)
                throw new ArgumentException("Sonic settings are invalid.");
            if (AssaultPercent < 0 || AssaultPercent > 100 || FallDamagePercent < 0)
                throw new ArgumentException("Assault / fall settings are invalid.");
            if (FloorCount < 1 || EnemiesPerFloorStep < 0 || AssaultPercentPerFloor < 0 || GuardPercentDropPerFloor < 0 || EnemyStatPercentPerFloor < 0)
                throw new ArgumentException("Floor progression settings are invalid.");
            WeaponFor(StartWeapon);
            foreach (var weapon in Weapons)
            {
                if (weapon.Attack < 1 || weapon.AttackRange < 1 || weapon.StabilityDamage < 0 || weapon.CritPercent < 0 || weapon.CritPercent > 100
                    || weapon.CritMultiplierPercent < 100 || weapon.FalloffStartCells < 0 || weapon.FalloffPercentPerCell < 0
                    || weapon.ExtraShotsPerTurn < 0)
                    throw new ArgumentException($"Stats for weapon {weapon.Kind} are invalid.");
            }
            var seen = new HashSet<CharacterKind>();
            foreach (var character in Characters)
            {
                if (character.Kind == CharacterKind.None || !seen.Add(character.Kind)
                    || character.MaxHp < 1 || character.MaxStability < 0 || character.VisionRange < 1 || character.Defense < 0 || character.MoveRange < 0)
                    throw new ArgumentException($"Stats for character {character.Kind} are invalid.");
                WeaponFor(character.StartWeapon);
                if (character.Skills.Count > 3) throw new ArgumentException($"Character {character.Kind} has more than three skill slots.");
            }
            if (CombatDropPartPercent < 0 || CratePartPercent < 0 || CrateWeaponPercent + CratePartPercent > 100
                || CombatDropWeaponPercent + CombatDropMedkitPercent + CombatDropPartPercent > 100)
                throw new ArgumentException("Part drop settings are invalid.");
            if (PartGradePercents.Length != 3 || PartPrimaryStatCounts.Length != 3 || PartSecondaryStatCounts.Length != 3
                || PartGradePercents[0] + PartGradePercents[1] + PartGradePercents[2] != 100)
                throw new ArgumentException("Part grade settings are invalid.");
            if (WorkshopOptionPercent < 0 || WorkshopOptionPercent > 100 || WorkshopSetPercent < 0 || WorkshopSetPercent > 100
                || WorkshopOptionCost < 0 || WorkshopSetCost < 0 || DisassembleMaterials.Length != 3 || SpareSlots < 0)
                throw new ArgumentException("Workshop settings are invalid.");
            if (TreasureWeaponPercent < 0 || TreasureWeaponPercent > 100)
                throw new ArgumentException("Treasure settings are invalid.");
            if (CombatDropKeyPercent < 0 || CombatDropKeyPercent > 100)
                throw new ArgumentException("Key settings are invalid.");
            foreach (var character in Characters)
                if (character.DoorForcePercent < 0 || character.DoorForcePercent > 100)
                    throw new ArgumentException($"Door force chance for {character.Kind} is invalid.");
            foreach (var character in Characters)
                if (character.WorkshopOptionPercent < 0 || character.WorkshopOptionPercent > 100 || character.WorkshopSetPercent < 0 || character.WorkshopSetPercent > 100)
                    throw new ArgumentException($"Workshop chances for {character.Kind} are invalid.");
            if (SetPiecesRequired < 1 || SetPiecesRequired > WeaponPart.SlotCount - SetEffects.FirstSetSlot
                || SetChainPercentPerStack < 0 || SetChainMaxStacks < 0 || SetTwoSidedPercent < 0 || SetFracturePercent < 0
                || SetEmergencyStability < 0 || SetBulwarkPercent < 0 || SetBulwarkPercent > 100 || SetBulwarkTurns < 0
                || SetAmbushPercent < 0 || SetAssaultPercent < 0 || SetAssaultMeleePercent < 0)
                throw new ArgumentException("Part set settings are invalid.");
            foreach (var stat in PartPrimaryStats) PartRange(stat);
            foreach (var stat in PartSecondaryStats) PartRange(stat);
            foreach (var range in PartStatRanges)
                if (range.Min < 1 || range.Max < range.Min) throw new ArgumentException($"Part range for {range.Stat} is invalid.");
            if (CombatDropWeaponPercent < 0 || CombatDropMedkitPercent < 0 || CombatDropWeaponPercent + CombatDropMedkitPercent > 100
                || CrateWeaponPercent < 0 || CrateWeaponPercent > 100 || MedkitHealPercent < 0)
                throw new ArgumentException("Loot settings are invalid.");
        }
    }
}
