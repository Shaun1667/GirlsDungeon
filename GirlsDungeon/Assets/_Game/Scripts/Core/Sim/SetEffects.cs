using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 파츠 세트 (3-4b, 사용자 제공 표를 우리 규칙으로 바꿈 — TASKS 3-4). 군용 · 정밀 등급의 2 · 3 · 4번 칸 파츠에만 붙는다.
    /// 저장 · 데이터에 정수로 들어가므로 끝에만 추가한다. 이름 글은 텍스트 표 `part.set.{소문자 이름}.name` · `.rule`.
    /// </summary>
    public enum PartSet : byte
    {
        None = 0,
        /// <summary>연쇄 가속: 스킬을 쓸 때마다 이번 전투 스킬 피해 +n%, 최대 m중첩.</summary>
        Chain = 1,
        /// <summary>양면 전술: 대상 옆에 엄폐물이 없으면 단일 피해 +n%, 있으면 범위 피해 +n%.</summary>
        TwoSided = 2,
        /// <summary>균열 추격: 안정지수가 깎인 적에게 주는 피해 +n%.</summary>
        Fracture = 3,
        /// <summary>응급 정비: 구급상자로 체력이 가득 차면 안정지수 +n.</summary>
        Emergency = 4,
        /// <summary>방호 태세: 감시를 걸면 n턴 동안 받는 범위 피해 -m%.</summary>
        Bulwark = 5,
        /// <summary>매복 사수: 감시 사격(적 차례에 쏘는 것) 피해 +n%.</summary>
        Ambush = 6,
        /// <summary>백병 강습: 주는 피해 +n%, 근접 무기(도검) 공격이면 +m% 더.</summary>
        Assault = 7,
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
    }

    /// <summary>
    /// 세트 효과의 한 곳 (3-4b, 결정 2026-11-13 사용자: 7개 모두 · 같은 세트 3개를 끼우면 켜짐 · 받은 퍼센트 그대로).
    /// - 켜짐 `Active`: 든 무기의 2 · 3 · 4번 칸이 모두 같은 세트일 때 그 세트 (아니면 None).
    /// - 피해 `DamagePercent`: 주는 피해 증가 − 받는 피해 감소를 더한 값(%). 사격 · 수류탄 · 넉백 · 음파의 미리보기와 실제가 모두 이것을 쓴다 (규칙 6 · 7).
    /// - 상태: 연쇄 가속 중첩 `World.SetChainStacks`, 방호 태세 남은 차례 `World.SetBulwarkTurns` — 전투가 끝나면 0.
    /// 수치는 CombatSettings.Set… (BALANCE 18번).
    /// </summary>
    public static class SetEffects
    {
        /// <summary>세트가 붙는 첫 칸 (0 = 1번 칸은 세트가 없다).</summary>
        public const int FirstSetSlot = 1;

        public const int SetCount = 7;

        /// <summary>그 등급 · 칸의 파츠에 세트가 붙는가 (군용 · 정밀의 2 · 3 · 4번 칸).</summary>
        public static bool CanHaveSet(PartGrade grade, int slot) => slot >= FirstSetSlot && grade != PartGrade.Industrial;

        /// <summary>든 무기에서 켜진 세트 (없으면 None).</summary>
        public static PartSet Active(World world, CombatSettings settings)
        {
            var counts = new int[SetCount + 1];
            foreach (var part in world.PlayerParts)
                if (part != null && part.Set != PartSet.None) counts[(int)part.Set]++;
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

        public static bool IsSkill(DamageSource source) => source == DamageSource.Grenade || source == DamageSource.Push || source == DamageSource.Sonic;
        public static bool IsArea(DamageSource source) => source == DamageSource.Grenade || source == DamageSource.Push;

        /// <summary>
        /// 세트 효과로 바뀌는 피해 (%p). 100에 더해 곱한다 (+12 → ×1.12, −20 → ×0.8).
        /// 주는 쪽 효과는 플레이어가 때릴 때만, 받는 쪽 효과(방호 태세)는 플레이어가 맞을 때만.
        /// </summary>
        public static int DamagePercent(World world, CombatSettings settings, Unit attacker, Unit target, DamageSource source)
        {
            // 상점 주인의 밑천(스택)은 받는 피해를 줄인다 (KeeperSkills, 3-15f) — 모든 피해 미리보기가 이 함수를 거친다.
            return SetPercent(world, settings, attacker, target, source) - KeeperSkills.DamageTakenReduction(world, settings, target);
        }

        private static int SetPercent(World world, CombatSettings settings, Unit attacker, Unit target, DamageSource source)
        {
            if (world.Player == null) return 0;
            var set = Active(world, settings);
            if (set == PartSet.None) return 0;

            int percent = 0;
            if (attacker != null && attacker.IsPlayer)
            {
                switch (set)
                {
                    case PartSet.Chain:
                        if (IsSkill(source)) percent += settings.SetChainPercentPerStack * world.SetChainStacks;
                        break;
                    case PartSet.TwoSided:
                        bool nearCover = NearCover(world.Map, target.Position);
                        if (!nearCover && !IsArea(source)) percent += settings.SetTwoSidedPercent;
                        if (nearCover && IsArea(source)) percent += settings.SetTwoSidedPercent;
                        break;
                    case PartSet.Fracture:
                        if (!target.IsPlayer && target.Stability < target.MaxStability) percent += settings.SetFracturePercent;
                        break;
                    case PartSet.Ambush:
                        if (source == DamageSource.Overwatch) percent += settings.SetAmbushPercent;
                        break;
                    case PartSet.Assault:
                        percent += settings.SetAssaultPercent;
                        var weapon = settings.TryWeapon(world.PlayerWeapon);
                        if ((source == DamageSource.Shot || source == DamageSource.Overwatch) && weapon != null && weapon.Melee)
                            percent += settings.SetAssaultMeleePercent;
                        break;
                }
            }
            if (target != null && target.IsPlayer && set == PartSet.Bulwark && world.SetBulwarkTurns > 0 && IsArea(source))
                percent -= settings.SetBulwarkPercent;
            return percent;
        }

        /// <summary>대상 칸의 동서남북 어느 쪽이든 엄폐물(낮음 · 높음)이 있는가 — 방향은 따지지 않는다 (양면 전술).</summary>
        public static bool NearCover(FloorMap map, GridPos cell)
        {
            return Cover.InDirection(map, cell, new GridPos(1, 0)) != CoverLevel.None
                || Cover.InDirection(map, cell, new GridPos(-1, 0)) != CoverLevel.None
                || Cover.InDirection(map, cell, new GridPos(0, 1)) != CoverLevel.None
                || Cover.InDirection(map, cell, new GridPos(0, -1)) != CoverLevel.None;
        }

        /// <summary>스킬(수류탄 · 넉백 · 음파)을 쓴 뒤: 연쇄 가속이면 중첩 +1 (최대까지). 쓴 스킬의 피해에는 아직 붙지 않는다.</summary>
        public static void OnSkillUsed(World world, CombatSettings settings)
        {
            if (Active(world, settings) != PartSet.Chain) return;
            if (world.SetChainStacks < settings.SetChainMaxStacks) world.SetChainStacks++;
        }

        /// <summary>감시를 건 뒤: 방호 태세면 받는 범위 피해 감소를 n차례 건다 (플레이어 차례가 시작될 때마다 1씩 준다).</summary>
        public static void OnOverwatchSet(World world, CombatSettings settings)
        {
            if (Active(world, settings) == PartSet.Bulwark) world.SetBulwarkTurns = settings.SetBulwarkTurns;
        }

        /// <summary>플레이어 차례가 시작될 때 (TurnFlow).</summary>
        public static void OnPlayerTurnStarted(World world)
        {
            if (world.SetBulwarkTurns > 0) world.SetBulwarkTurns--;
        }

        /// <summary>전투가 끝날 때 (CombatFlow): 중첩 · 남은 차례를 지운다.</summary>
        public static void OnCombatEnded(World world)
        {
            world.SetChainStacks = 0;
            world.SetBulwarkTurns = 0;
        }

        /// <summary>
        /// 구급상자로 체력이 가득 찼을 때: 응급 정비면 안정지수 +n (최대까지). 올린 만큼을 돌려준다 (0이면 그대로).
        /// </summary>
        public static int OnMedkitFilledHp(World world, CombatSettings settings, Unit player)
        {
            if (Active(world, settings) != PartSet.Emergency || player.Stability >= player.MaxStability) return 0;
            int gained = System.Math.Min(settings.SetEmergencyStability, player.MaxStability - player.Stability);
            player.Stability += gained;
            return gained;
        }
    }
}
