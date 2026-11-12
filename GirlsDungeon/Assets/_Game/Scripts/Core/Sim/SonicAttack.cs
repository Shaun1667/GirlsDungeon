using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>음파 공격을 쓸 수 있는지와 그 이유.</summary>
    public enum SonicBlock
    {
        None = 0,
        /// <summary>재사용 대기 중.</summary>
        NotReady = 1,
        /// <summary>대상이 전투 중인 적이 아니다.</summary>
        NoTarget = 2,
        OutOfRange = 3,
        /// <summary>지금 시야(Vision)에 보이지 않는다.</summary>
        NotVisible = 4,
    }

    /// <summary>음파 공격 미리보기. 실제 공격도 같은 값을 쓴다 (설계 규칙 6).</summary>
    public readonly struct SonicPreview
    {
        public readonly SonicBlock Block;
        public readonly Unit Target;
        public readonly int Damage;
        public readonly int StabilityDamage;

        public bool CanUse => Block == SonicBlock.None;

        public SonicPreview(SonicBlock block, Unit target, int damage, int stabilityDamage)
        {
            Block = block;
            Target = target;
            Damage = damage;
            StabilityDamage = stabilityDamage;
        }
    }

    /// <summary>
    /// 음파 공격 스킬의 한 곳 (BALANCE 15번, 3-12 — 결정 2026-11-12 사용자 "음파공격(신규스킬, 단일 타겟팅 데미지)").
    /// - 대상: 전투 중인 적 하나. 사거리(직선 거리) 안이고 지금 보이는 칸 (Vision — 수류탄과 같은 판정).
    /// - 피해: 든 무기 공격력 공식 × 피해 배율, 엄폐 감소 · 안정지수 감소(StabilityGuard)를 받지 않는다. 치명타 없음. 안정지수를 따로 깎는다.
    /// - 엄폐물은 깎지 않는다. 쓰면 재사용 대기가 걸리고 차례가 끝난다 (SonicAttackCommand).
    /// </summary>
    public static class SonicAttack
    {
        public static SonicPreview Preview(World world, CombatSettings settings, Unit attacker, Unit target)
        {
            if (world.SonicCooldown > 0) return new SonicPreview(SonicBlock.NotReady, target, 0, 0);
            if (target == null || target.IsPlayer || !world.Combatants.Contains(target.Id)) return new SonicPreview(SonicBlock.NoTarget, target, 0, 0);

            int dx = target.Position.X - attacker.Position.X;
            int dy = target.Position.Y - attacker.Position.Y;
            var block = SonicBlock.None;
            if (dx * dx + dy * dy > settings.SonicRange * settings.SonicRange) block = SonicBlock.OutOfRange;
            else if (!Vision.IsSet(Vision.Compute(world.Map, attacker.Position, attacker.VisionRange), world.Map, target.Position)) block = SonicBlock.NotVisible;

            int damage = CombatRules.Damage(attacker.Attack, target.Defense, settings.SonicDamagePercent, 0, settings.MinDamage);
            return new SonicPreview(block, target, damage, settings.SonicStabilityDamage);
        }

        /// <summary>음파 공격을 처리한다. 쓸 수 없으면 아무것도 바꾸지 않고 false.</summary>
        public static bool Resolve(World world, CombatSettings settings, Unit attacker, Unit target, List<GameEvent> events)
        {
            var preview = Preview(world, settings, attacker, target);
            if (!preview.CanUse) return false;

            int stabilityBefore = target.Stability;
            target.Hp -= preview.Damage;
            target.Stability = Math.Max(0, target.Stability - preview.StabilityDamage);
            events.Add(new UnitAttacked(attacker, target, preview.Damage, false, CoverLevel.None, false, stabilityBefore > 0 && target.Stability == 0));
            events.Add(new UnitStatsChanged(target.Id));
            Alert.Wake(world, target, events);
            if (target.Hp <= 0) CombatFlow.Kill(world, target, events);

            world.SonicCooldown = settings.SonicCooldownRounds;
            return true;
        }
    }

    /// <summary>플레이어 음파 공격 스킬. 캐릭터가 이 스킬을 가지고 있어야 한다. 쓰면 차례가 끝나고 적 차례가 이어진다 (전투가 끝났으면 그대로).</summary>
    public sealed class SonicAttackCommand : ICommand
    {
        private readonly int targetId;
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public SonicAttackCommand(int targetId, CombatSettings settings)
        {
            this.targetId = targetId;
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!TurnFlow.IsPlayerTurn(world) || world.PlayerActed) return false;
            if (!settings.HasSkill(world.PlayerCharacter, SkillKind.Sonic)) return false;
            if (!SonicAttack.Resolve(world, settings, world.Player, world.FindUnit(targetId), events)) return false;

            world.PlayerActed = true;
            if (world.Mode == WorldMode.Combat && world.Combatants.Count > 0) TurnFlow.EndPlayerTurn(world, settings, events);
            return true;
        }
    }
}
