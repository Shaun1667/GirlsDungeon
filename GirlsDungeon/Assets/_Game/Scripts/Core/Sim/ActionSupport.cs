using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 행동 지원 (대원 B 특성, 3-17g — 결정 2026-11-20 사용자 "자신의 기본 사격이랑 빙결 사격에도 발동, 사격으로 때린 적을 자동으로 한 번 더, 추가 사격에는 발동하지 않게"):
    /// - 발동: 그 차례의 첫 기본 사격(AttackCommand — 기관단총의 두 번째 발 · 스킬 뒤 남은 한 발은 아님) · 빙결 사격(FrostAttackCommand)이 적을 맞힌 뒤.
    ///   감시 사격 · 지원 공격 자신으로는 발동하지 않는다.
    /// - 지원 공격: 같은 적에게 사격과 똑같은 계산(엄폐 · 안정지수 감소 · 치명 · 거리별 피해 · 엄폐물 깎기 — 사용자 결정 "사격과 완전히 같게")에
    ///   배율만 캐릭터 줄 ActionSupportPercent, 안정 피해는 ActionSupportStabilityDamage. 사거리 · 사선이 안 되면 발동하지 않는다. 피해 출처는 지원 공격(턴 외).
    /// - 그 뒤 자신에게 치명피해 상승Ⅰ: 치명 피해 +CritBoostPercent%p, CritBoostTurns 차례 (Loadout.Apply가 더함, 내 차례 시작마다 −1, 다시 걸리면 차례만 새로).
    /// 수치는 캐릭터 줄 (0 = 이 캐릭터는 없음, BALANCE 34-9).
    /// </summary>
    public static class ActionSupport
    {
        private static CharacterStats Trait(World world, CombatSettings settings) => settings?.TryCharacter(world.PlayerCharacter);

        /// <summary>이 캐릭터가 행동 지원을 가졌는가.</summary>
        public static bool Has(World world, CombatSettings settings) => (Trait(world, settings)?.ActionSupportPercent ?? 0) > 0;

        /// <summary>
        /// 사격 · 빙결 사격이 대상을 맞힌 뒤 부른다. 대상이 살아 있고 지금 자리에서 쏠 수 있으면 지원 공격 한 번 + 치명피해 상승Ⅰ.
        /// 발동했으면 true.
        /// </summary>
        public static bool Trigger(World world, CombatSettings settings, int targetId, List<GameEvent> events)
        {
            var trait = Trait(world, settings);
            var player = world.Player;
            var target = world.FindUnit(targetId);
            if (trait == null || trait.ActionSupportPercent <= 0 || player == null || target == null || target.IsPlayer) return false;
            if (world.Mode != WorldMode.Combat && world.Mode != WorldMode.Exploration) return false;

            var preview = CombatRules.Preview(world, settings, player, player.Position, target);
            if (!preview.CanAttack) return false;

            bool crit = world.Random.Get(RandomPurpose.Combat).Range(0, 100) < preview.CritPercent;
            int multiplier = (crit ? player.CritMultiplierPercent : 100) * trait.ActionSupportPercent / 100;
            int setPercent = SetEffects.DamagePercent(world, settings, player, target, DamageSource.Support);
            int damage = CombatRules.Damage(player.Attack, target.Defense, multiplier, preview.ReductionPercent, settings.MinDamage, 100 - preview.FalloffPercent, setPercent);

            events.Add(new ActionSupportTriggered(player.Id, target.Id));
            Remold.BeforeAttack(world, settings, player, target, events);
            int dealt = KeeperSkills.AfterShield(world, target, damage, events, player, settings);
            int stabilityBefore = target.Stability;
            target.Hp -= dealt;
            Remold.OnDamageDealt(world, settings, player, target, dealt);
            target.Stability = Math.Max(0, target.Stability - trait.ActionSupportStabilityDamage);
            events.Add(new UnitAttacked(player, target, dealt, crit, preview.Cover, preview.StabilityGuard, stabilityBefore > 0 && target.Stability == 0));
            events.Add(new UnitStatsChanged(target.Id));
            if (preview.Cover != CoverLevel.None)
                CoverBreak.Hit(world, settings, CoverBreak.Facing(world.Map, target.Position, player.Position), events);
            Alert.Wake(world, target, events);
            if (target.Hp <= 0) CombatFlow.Kill(world, target, events);

            if (trait.CritBoostPercent > 0 && trait.CritBoostTurns > 0)
            {
                world.PlayerCritBoostTurns = trait.CritBoostTurns;
                Loadout.Apply(world, settings);
                events.Add(new CritBoosted(player.Id, trait.CritBoostPercent, world.PlayerCritBoostTurns));
                events.Add(new UnitStatsChanged(player.Id));
            }
            return true;
        }

        /// <summary>치명피해 상승Ⅰ로 더하는 치명 피해 (%p). Loadout.Apply가 부른다.</summary>
        public static int CritBoost(World world, CombatSettings settings)
            => world.PlayerCritBoostTurns > 0 ? Trait(world, settings)?.CritBoostPercent ?? 0 : 0;

        /// <summary>내 차례 시작 (CombatFlow): 치명피해 상승Ⅰ 남은 차례 −1, 0이면 사라진다.</summary>
        public static void OnPlayerTurnStarted(World world, CombatSettings settings, List<GameEvent> events)
        {
            if (world.PlayerCritBoostTurns <= 0 || --world.PlayerCritBoostTurns > 0) return;
            if (settings != null) Loadout.Apply(world, settings);
            events.Add(new UnitStatsChanged(world.PlayerId));
        }

        /// <summary>전투가 끝나거나 층을 옮기면 지운다 (치명 피해는 다음 Loadout.Apply에서 돌아온다).</summary>
        public static void Clear(World world, CombatSettings settings)
        {
            if (world.PlayerCritBoostTurns <= 0) return;
            world.PlayerCritBoostTurns = 0;
            if (settings != null) Loadout.Apply(world, settings);
        }
    }

    /// <summary>행동 지원이 발동했다 (뒤따르는 UnitAttacked가 그 공격).</summary>
    public sealed class ActionSupportTriggered : GameEvent
    {
        public int UnitId { get; }
        public int TargetId { get; }

        public ActionSupportTriggered(int unitId, int targetId)
        {
            UnitId = unitId;
            TargetId = targetId;
        }
    }

    /// <summary>치명피해 상승Ⅰ가 걸렸다.</summary>
    public sealed class CritBoosted : GameEvent
    {
        public int UnitId { get; }
        public int Percent { get; }
        public int Turns { get; }

        public CritBoosted(int unitId, int percent, int turns)
        {
            UnitId = unitId;
            Percent = percent;
            Turns = turns;
        }
    }
}
