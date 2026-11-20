using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 파츠 세트 (3-16e, 결정 2026-11-16 사용자: 새 목록 13종으로 전부 교체). 군용 · 정밀 등급의 2 · 3 · 4번 칸 파츠에만 붙는다.
    /// 저장 · 데이터에 정수로 들어가므로 끝에만 추가한다. 이름 글은 텍스트 표 `part.set.{소문자 이름}.name` · `.rule`.
    /// </summary>
    public enum PartSet : byte
    {
        None = 0,
        /// <summary>결전 추진: 결전기(세 번째 스킬 = 스킬 바 4번 카드) 피해 +n% × 중첩, 결전기를 쓴 뒤마다 1중첩, 최대 m.</summary>
        Decisive = 1,
        /// <summary>이중 대책: 대상 옆에 엄폐물이 없으면 단일 피해 +n%, 있으면 범위 피해 +n%.</summary>
        DualCounter = 2,
        /// <summary>위상 타격: 위상 속성 디버프를 가진 적에게 주는 피해 +n% (위상 디버프가 생기면 켜짐).</summary>
        PhaseStrike = 3,
        /// <summary>위상 증폭: 액티브 스킬로 약점 공격 시 위상 강화I, 두 약점 동시 공격 +n% (약점이 생기면 켜짐).</summary>
        PhaseAmp = 4,
        /// <summary>실탄 증폭: 물리 피해 +n%.</summary>
        Kinetic = 5,
        /// <summary>작열 증폭: 연소 피해 +n%.</summary>
        Blaze = 6,
        /// <summary>빙점 증폭: 빙결 피해 +n%.</summary>
        Frost = 7,
        /// <summary>강산 증폭: 산성 피해 +n%.</summary>
        Acid = 8,
        /// <summary>자력 증폭: 전도 피해 +n%.</summary>
        Magnetic = 9,
        /// <summary>격류 증폭: 탁류 피해 +n%.</summary>
        Torrent = 10,
        /// <summary>전술 연산: 턴 외 피해(지금은 감시 사격) +n%.</summary>
        Tactical = 11,
        /// <summary>육박 강공: 주는 피해 +n%, 근접 무기(도검) 공격이면 +m% 더.</summary>
        Assault = 12,
        /// <summary>정점 소환: 내 실체 소환물이 있으면 나와 소환물이 주는 피해 +n% (소환물이 생기면 켜짐).</summary>
        Apex = 13,
    }

    /// <summary>피해가 어디서 나왔는지 — 세트 효과가 고르는 기준.</summary>
    public enum DamageSource
    {
        /// <summary>내 차례 사격 (단일).</summary>
        Shot = 0,
        /// <summary>적 차례 감시 사격 (단일, 턴 외).</summary>
        Overwatch = 1,
        /// <summary>수류탄 (범위, 스킬).</summary>
        Grenade = 2,
        /// <summary>넉백 공격의 직접 피해 (범위, 스킬). 부딪힘 피해는 고정값이라 세트를 받지 않는다.</summary>
        Push = 3,
        /// <summary>음파 공격 (단일, 스킬).</summary>
        Sonic = 4,
        /// <summary>빙결 사격 (단일, 스킬, 빙결 속성 — 3-16z).</summary>
        Frost = 5,
        /// <summary>지원 공격 — 행동 지원 (단일, 턴 외, 물리 — 3-17g).</summary>
        Support = 6,
    }

    /// <summary>피해 속성 (3-16e). 지금은 모든 피해가 물리 — 속성을 주는 무기 · 스킬이 생기면 ElementOf에서 고른다.</summary>
    public enum DamageElement
    {
        Physical = 0,
        Burn = 1,
        Freeze = 2,
        Acid = 3,
        Conduct = 4,
        Torrent = 5,
        Phase = 6,
    }

    /// <summary>
    /// 세트 효과의 한 곳 (3-4b → 3-16e 새 목록).
    /// - 켜짐 `Active`: 든 무기의 2 · 3 · 4번 칸이 모두 같은 세트일 때 그 세트 (아니면 None).
    /// - 피해 `DamagePercent`: 주는 피해 증가 − 받는 피해 감소를 더한 값(%). 사격 · 수류탄 · 넉백 · 음파의 미리보기와 실제가 모두 이것을 쓴다 (규칙 6 · 7).
    /// - 상태: 결전 추진 중첩 `World.SetDecisiveStacks` — 전투가 끝나면 0.
    /// - 아직 게임에 없는 조건(위상 디버프 · 약점 · 소환물 · 물리 밖 속성)을 쓰는 세트는 `HasSource`가 false — 드롭 · 작업방 · 진열실 후보(`Pool`)에서 빠진다.
    ///   그 시스템이 생기면 아래 조건 함수(ElementOf · HasPhaseDebuff · IsWeaknessHit · HasSummon)만 고치면 자동으로 켜진다.
    /// 수치는 CombatSettings.Set… (BALANCE 18번).
    /// </summary>
    public static class SetEffects
    {
        /// <summary>세트가 붙는 첫 칸 (0 = 1번 칸은 세트가 없다).</summary>
        public const int FirstSetSlot = 1;

        public const int SetCount = 13;

        /// <summary>결전기 = 세 번째 스킬 (스킬 바 4번 카드, 스킬 칸 목록 2 · 3 · 4번 중 세 번째 — 결정 2026-11-16 사용자, 대원 A = 수류탄).</summary>
        public const int DecisiveSkillIndex = 2;

        /// <summary>그 등급 · 칸의 파츠에 세트가 붙는가 (군용 · 정밀의 2 · 3 · 4번 칸).</summary>
        public static bool CanHaveSet(PartGrade grade, int slot) => slot >= FirstSetSlot && grade != PartGrade.Industrial;

        // ---- 아직 없는 시스템의 조건 (생기면 여기만 고친다) ----

        /// <summary>그 피해의 속성. 빙결 사격만 빙결 (3-16z), 나머지는 물리.</summary>
        public static DamageElement ElementOf(World world, CombatSettings settings, DamageSource source)
            => source == DamageSource.Frost ? DamageElement.Freeze : DamageElement.Physical;

        /// <summary>대상이 위상 속성 디버프를 가졌는가 (위상 디버프가 아직 없다).</summary>
        public static bool HasPhaseDebuff(Unit target) => false;

        /// <summary>이 공격이 약점 공격인가 (약점이 아직 없다).</summary>
        public static bool IsWeaknessHit(World world, Unit target, DamageSource source) => Weakness.Hits(world, null, world.Player, target, source) > 0;

        /// <summary>플레이어의 실체 소환물이 필드에 있는가 (소환물이 아직 없다).</summary>
        public static bool HasSummon(World world) => false;

        /// <summary>속성 증폭 세트가 고르는 속성 (속성 세트가 아니면 null).</summary>
        public static DamageElement? ElementOfSet(PartSet set)
        {
            switch (set)
            {
                case PartSet.Kinetic: return DamageElement.Physical;
                case PartSet.Blaze: return DamageElement.Burn;
                case PartSet.Frost: return DamageElement.Freeze;
                case PartSet.Acid: return DamageElement.Acid;
                case PartSet.Magnetic: return DamageElement.Conduct;
                case PartSet.Torrent: return DamageElement.Torrent;
                default: return null;
            }
        }

        private static readonly DamageSource[] AllSources = { DamageSource.Shot, DamageSource.Overwatch, DamageSource.Grenade, DamageSource.Push, DamageSource.Sonic, DamageSource.Frost, DamageSource.Support };

        /// <summary>지금 게임에서 이 세트의 조건이 생길 수 있는가 — 없으면 드롭 후보에서 뺀다.</summary>
        public static bool HasSource(PartSet set)
        {
            var element = ElementOfSet(set);
            if (element.HasValue)
            {
                foreach (var source in AllSources)
                    if (ElementOf(null, null, source) == element.Value) return true;
                return false;
            }
            switch (set)
            {
                case PartSet.PhaseStrike:
                case PartSet.PhaseAmp:
                case PartSet.Apex:
                    return false;
                case PartSet.None:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>파츠에 붙을 수 있는 세트 후보 (번호 순).</summary>
        public static List<PartSet> Pool()
        {
            var pool = new List<PartSet>();
            for (int i = 1; i <= SetCount; i++)
                if (HasSource((PartSet)i)) pool.Add((PartSet)i);
            return pool;
        }

        /// <summary>후보에서 무작위 세트 하나 (드롭 · 작업방 실패 · 진열실).</summary>
        public static PartSet RandomSet(IRandom random)
        {
            var pool = Pool();
            return pool[random.Range(0, pool.Count)];
        }

        /// <summary>든 무기에서 켜진 세트 (없으면 None).</summary>
        public static PartSet Active(World world, CombatSettings settings)
        {
            var counts = new int[SetCount + 1];
            foreach (var part in world.PlayerParts)
                if (part != null && part.Set != PartSet.None && (int)part.Set <= SetCount) counts[(int)part.Set]++;
            for (int i = 1; i <= SetCount; i++)
                if (counts[i] >= settings.SetPiecesRequired) return (PartSet)i;
            return PartSet.None;
        }

        /// <summary>그 세트 파츠를 몇 개 끼웠는가 (비교 창 "n / 3").</summary>
        public static int Count(World world, PartSet set)
        {
            if (set == PartSet.None) return 0;
            int count = 0;
            foreach (var part in world.PlayerParts) if (part != null && part.Set == set) count++;
            return count;
        }

        public static bool IsSkill(DamageSource source) => source == DamageSource.Grenade || source == DamageSource.Push || source == DamageSource.Sonic || source == DamageSource.Frost;
        public static bool IsArea(DamageSource source) => source == DamageSource.Grenade || source == DamageSource.Push;
        /// <summary>턴 외 피해 (지원 공격 · 매복 · 반격 · 패시브 — 지금은 감시 사격뿐).</summary>
        public static bool IsOffTurn(DamageSource source) => source == DamageSource.Overwatch || source == DamageSource.Support;

        /// <summary>그 피해를 낸 스킬 (스킬이 아니면 None).</summary>
        public static SkillKind SkillOf(DamageSource source)
        {
            switch (source)
            {
                case DamageSource.Grenade: return SkillKind.Grenade;
                case DamageSource.Push: return SkillKind.Push;
                case DamageSource.Sonic: return SkillKind.Sonic;
                case DamageSource.Frost: return SkillKind.Frost;
                default: return SkillKind.None;
            }
        }

        /// <summary>그 스킬이 지금 캐릭터의 결전기(세 번째 스킬, 스킬 바 4번 카드)인가. 그 칸이 비어 있는 캐릭터는 결전기가 없다.</summary>
        public static bool IsDecisive(World world, CombatSettings settings, SkillKind skill)
        {
            if (skill == SkillKind.None || settings == null) return false;
            var slots = settings.SkillSlots(world.PlayerCharacter);
            return slots.Count > DecisiveSkillIndex && slots[DecisiveSkillIndex] == skill;
        }

        /// <summary>
        /// 세트 효과로 바뀌는 피해 (%p). 100에 더해 곱한다 (+12 → ×1.12, −20 → ×0.8).
        /// 주는 쪽 효과는 플레이어가 때릴 때만.
        /// </summary>
        public static int DamagePercent(World world, CombatSettings settings, Unit attacker, Unit target, DamageSource source, GridPos? from = null)
        {
            // 상점 주인의 밑천(스택)은 받는 피해를 줄인다 (KeeperSkills, 3-15f) — 모든 피해 미리보기가 이 함수를 거친다.
            // 이동 감소를 가진 적에게 주는 피해 증가 (Remold, 3-16 — from = 이동 예약 칸에서 쏘는 미리보기).
            // 빙설 보루 (3-17c): 플레이어가 받는 피해 감소. 약점 (3-17h): 맞힌 약점 1개당 +.
            return Weakness.DamagePercent(world, settings, attacker, target, source) + SetPercent(world, settings, attacker, target, source) - KeeperSkills.DamageTakenReduction(world, settings, target) - FrostStatus.BulwarkReduction(world, settings, target)
                + Remold.SlowedDamagePercent(world, settings, attacker, target, from);
        }

        private static int SetPercent(World world, CombatSettings settings, Unit attacker, Unit target, DamageSource source)
        {
            if (world.Player == null || attacker == null || !attacker.IsPlayer || target == null) return 0;
            var set = Active(world, settings);
            if (set == PartSet.None) return 0;

            var element = ElementOfSet(set);
            if (element.HasValue)
                return ElementOf(world, settings, source) == element.Value ? settings.SetElementPercent : 0;

            switch (set)
            {
                case PartSet.Decisive:
                    return IsDecisive(world, settings, SkillOf(source)) ? settings.SetDecisivePercentPerStack * world.SetDecisiveStacks : 0;
                case PartSet.DualCounter:
                    bool nearCover = NearCover(world.Map, target.Position);
                    if (!nearCover && !IsArea(source)) return settings.SetDualCounterPercent;
                    if (nearCover && IsArea(source)) return settings.SetDualCounterPercent;
                    return 0;
                case PartSet.PhaseStrike:
                    return !target.IsPlayer && HasPhaseDebuff(target) ? settings.SetPhaseStrikePercent : 0;
                case PartSet.PhaseAmp:
                    // 위상 강화I(주는 위상 피해 +n%)는 약점 공격 전에 얻는다 — 약점이 생기면 버프 상태와 두 약점 동시 공격을 여기에 더한다.
                    return IsSkill(source) && IsWeaknessHit(world, target, source) && ElementOf(world, settings, source) == DamageElement.Phase
                        ? settings.SetPhaseAmpPercent : 0;
                case PartSet.Tactical:
                    return IsOffTurn(source) ? settings.SetTacticalPercent : 0;
                case PartSet.Assault:
                    int percent = settings.SetAssaultPercent;
                    var weapon = settings.TryWeapon(world.PlayerWeapon);
                    if ((source == DamageSource.Shot || source == DamageSource.Overwatch) && weapon != null && weapon.Melee)
                        percent += settings.SetAssaultMeleePercent;
                    return percent;
                case PartSet.Apex:
                    return HasSummon(world) ? settings.SetApexPercent : 0;
                default:
                    return 0;
            }
        }

        /// <summary>대상 칸의 동서남북 어느 쪽이든 엄폐물(낮음 · 높음)이 있는가 — 방향은 따지지 않는다 (이중 대책).</summary>
        public static bool NearCover(FloorMap map, GridPos cell)
        {
            return Cover.InDirection(map, cell, new GridPos(1, 0)) != CoverLevel.None
                || Cover.InDirection(map, cell, new GridPos(-1, 0)) != CoverLevel.None
                || Cover.InDirection(map, cell, new GridPos(0, 1)) != CoverLevel.None
                || Cover.InDirection(map, cell, new GridPos(0, -1)) != CoverLevel.None;
        }

        /// <summary>스킬을 쓴 뒤: 결전 추진이고 그 스킬이 결전기면 중첩 +1 (최대까지). 쓴 스킬의 피해에는 아직 붙지 않는다.</summary>
        public static void OnSkillUsed(World world, CombatSettings settings, SkillKind skill)
        {
            if (Active(world, settings) != PartSet.Decisive || !IsDecisive(world, settings, skill)) return;
            if (world.SetDecisiveStacks < settings.SetDecisiveMaxStacks) world.SetDecisiveStacks++;
        }

        /// <summary>전투가 끝날 때 (CombatFlow): 중첩을 지운다.</summary>
        public static void OnCombatEnded(World world)
        {
            world.SetDecisiveStacks = 0;
        }
    }
}
