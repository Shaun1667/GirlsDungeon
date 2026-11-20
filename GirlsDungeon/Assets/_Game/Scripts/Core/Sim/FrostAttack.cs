using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>빙결 사격 미리보기. 실제 공격도 같은 값을 쓴다 (설계 규칙 6). 쓸 수 없는 이유는 음파와 같은 SonicBlock.</summary>
    public readonly struct FrostPreview
    {
        public readonly SonicBlock Block;
        public readonly Unit Target;
        /// <summary>주는 피해 합 = 빙결 피해(MainDamage) + 고정 피해(FixedDamage).</summary>
        public readonly int Damage;
        public readonly int MainDamage;
        public readonly int FixedDamage;
        public readonly int StabilityDamage;
        public readonly CoverLevel Cover;
        public readonly bool StabilityGuard;

        public bool CanUse => Block == SonicBlock.None;

        public FrostPreview(SonicBlock block, Unit target, int mainDamage, int fixedDamage, int stabilityDamage, CoverLevel cover, bool stabilityGuard)
        {
            Block = block;
            Target = target;
            MainDamage = mainDamage;
            FixedDamage = fixedDamage;
            Damage = mainDamage + fixedDamage;
            StabilityDamage = stabilityDamage;
            Cover = cover;
            StabilityGuard = stabilityGuard;
        }
    }

    /// <summary>
    /// 빙결 사격 스킬의 한 곳 (BALANCE 35번, 3-16z — 결정 2026-11-19 사용자, 대원 B 스킬1 "주위 반경 6칸 안의 적 하나에게 공격력 100% 빙결 피해 + 방어력 50% 고정 피해, 안정 피해 4").
    /// - 대상: 음파와 같다 — 전투 중인 적(탐험 중이면 보이는 아무 적, 중립 상점 주인) 하나, 반경(직선 거리) 안 · 지금 보이는 칸.
    /// - 빙결 피해: 공격력 공식 × FrostDamagePercent. 엄폐 감소 · 안정지수 감소를 받는다 (사격과 같은 판정 — 사용자 결정 "엄폐 적용"). 치명 없음. 속성은 빙결 (SetEffects.ElementOf).
    /// - 고정 피해: 쓰는 사람 방어력 × FrostFixedDefensePercent (반올림 — 사용자 결정 "수오미 방어력"). 방어 · 엄폐 · 세트로 바뀌지 않는다.
    /// - 엄폐물은 깎지 않는다. 쓰면 재사용 대기가 걸리고 차례가 끝난다 (FrostAttackCommand).
    /// </summary>
    public static class FrostAttack
    {
        public static FrostPreview Preview(World world, CombatSettings settings, Unit attacker, Unit target)
        {
            if (attacker.IsPlayer && !Remold.IsReady(world, settings, SkillKind.Frost)) return new FrostPreview(SonicBlock.NotReady, target, 0, 0, 0, CoverLevel.None, false);
            bool validTarget = target != null && !target.IsPlayer
                && (world.Combatants.Contains(target.Id) || world.Mode == WorldMode.Exploration || Shop.IsNeutral(world, target));
            if (!validTarget) return new FrostPreview(SonicBlock.NoTarget, target, 0, 0, 0, CoverLevel.None, false);

            int dx = target.Position.X - attacker.Position.X;
            int dy = target.Position.Y - attacker.Position.Y;
            var block = SonicBlock.None;
            if (dx * dx + dy * dy > settings.FrostRange * settings.FrostRange) block = SonicBlock.OutOfRange;
            else if (!Vision.IsSet(Vision.Compute(world.Map, attacker.Position, attacker.VisionRange), world.Map, target.Position)) block = SonicBlock.NotVisible;

            // 엄폐 · 안정지수 감소는 사격(CombatRules.Preview)과 같은 수치.
            var cover = Cover.Against(world.Map, target.Position, attacker.Position);
            bool guard = cover != CoverLevel.None && target.Stability > 0;
            int reduction = cover == CoverLevel.Full ? settings.FullCoverReductionPercent
                : cover == CoverLevel.Half ? settings.HalfCoverReductionPercent : 0;
            if (guard) reduction += settings.StabilityGuardPercent;

            int setPercent = SetEffects.DamagePercent(world, settings, attacker, target, DamageSource.Frost);
            int main = CombatRules.Damage(attacker.Attack, target.Defense, settings.SkillDamagePercentFor(attacker.IsPlayer ? world.PlayerCharacter : CharacterKind.None, SkillKind.Frost),
                reduction, settings.MinDamage, 100, setPercent);
            int fixedDamage = FixedDamage(attacker.Defense, settings.FrostFixedDefensePercent);
            return new FrostPreview(block, target, main, fixedDamage, settings.FrostStabilityDamage + Weakness.StabilityBonus(world, settings, attacker, target, DamageSource.Frost), cover, guard);
        }

        /// <summary>방어력 × % (반올림, 0.5는 올림). 음수 방어는 0.</summary>
        public static int FixedDamage(int defense, int percent)
        {
            if (defense <= 0 || percent <= 0) return 0;
            return (defense * percent * 2 + 100) / 200;
        }

        /// <summary>빙결 사격을 처리한다. 쓸 수 없으면 아무것도 바꾸지 않고 false.</summary>
        public static bool Resolve(World world, CombatSettings settings, Unit attacker, Unit target, List<GameEvent> events)
        {
            var preview = Preview(world, settings, attacker, target);
            if (!preview.CanUse) return false;

            Remold.Pay(world, settings, SkillKind.Frost);
            Remold.BeforeAttack(world, settings, attacker, target, events);
            int stabilityBefore = target.Stability;
            int dealt = KeeperSkills.AfterShield(world, target, preview.Damage, events);
            target.Hp -= dealt;
            Remold.OnDamageDealt(world, settings, attacker, target, dealt);
            target.Stability = Math.Max(0, target.Stability - preview.StabilityDamage);
            events.Add(new UnitAttacked(attacker, target, dealt, false, preview.Cover, preview.StabilityGuard, stabilityBefore > 0 && target.Stability == 0));
            events.Add(new UnitStatsChanged(target.Id));
            Alert.Wake(world, target, events);
            if (target.Hp <= 0) CombatFlow.Kill(world, target, events);
            return true;
        }
    }

    /// <summary>플레이어 빙결 사격 스킬. 캐릭터가 이 스킬을 가지고 있어야 한다. 쓰면 차례가 끝나고 적 차례가 이어진다 (전투가 끝났으면 그대로).</summary>
    public sealed class FrostAttackCommand : ICommand
    {
        private readonly int targetId;
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public FrostAttackCommand(int targetId, CombatSettings settings)
        {
            this.targetId = targetId;
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!CombatFlow.CanUseSkill(world)) return false;
            if (!settings.HasSkill(world.PlayerCharacter, SkillKind.Frost)) return false;
            if (!FrostAttack.Resolve(world, settings, world.Player, world.FindUnit(targetId), events)) return false;
            ActionSupport.Trigger(world, settings, targetId, events);
            SetEffects.OnSkillUsed(world, settings, SkillKind.Frost);
            CombatFlow.AfterSkill(world, settings, events);
            return true;
        }
    }
}
