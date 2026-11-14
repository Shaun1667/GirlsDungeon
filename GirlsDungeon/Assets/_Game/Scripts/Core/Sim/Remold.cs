using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 리몰딩 지수와 대원 A의 특성 한 곳 (BALANCE 34번, 3-16 — 결정 2026-11-14 사용자):
    /// - 리몰딩 지수 World.PlayerRemold: 최대 RemoldMax(캐릭터 줄, 0 = 이 캐릭터는 없음). 행동(내 차례) 시작마다 RemoldPerAction, 피해를 1회 줄 때마다 RemoldPerHit. 판 내내 유지.
    /// - 스킬 소모: 캐릭터 줄의 스킬 소모가 0보다 크면 그 스킬은 재사용 대기 대신 리몰딩을 쓴다 (IsReady · Pay — 스킬 미리보기 · 실제 · 화면 공용).
    /// - 이동 감소를 가진 적에게 주는 피해 +SlowedDamagePercent (SetEffects.DamagePercent가 더함 — 모든 피해 미리보기 공통).
    /// - 이번 차례에 SlowAfterMoveCells칸 이상 걸었으면 공격 전에 대상에게 이동 감소(SlowAmount칸, SlowTurns차례)를 건다 — 그 공격부터 위 피해 증가를 받는다.
    /// - 상점 주인의 리몰딩(World.KeeperRemold)도 같은 규칙으로 얻는다 — 수치는 CombatSettings.Keeper, 처리는 KeeperSkills (3-16b).
    /// - 이동 감소: 적 이동 칸은 MoveRangeOf 한 곳. 적이 행동할 때마다 남은 차례가 1 준다. 전투가 끝나면 지운다.
    /// </summary>
    public static class Remold
    {
        private static CharacterStats Trait(World world, CombatSettings settings)
            => settings?.TryCharacter(world.PlayerCharacter);

        /// <summary>지금 캐릭터의 리몰딩 최대치 (0 = 리몰딩 없음).</summary>
        public static int Max(World world, CombatSettings settings) => Trait(world, settings)?.RemoldMax ?? 0;

        /// <summary>그 스킬의 리몰딩 소모 (0 = 재사용 대기를 쓰는 스킬).</summary>
        public static int Cost(World world, CombatSettings settings, SkillKind skill)
        {
            var trait = Trait(world, settings);
            if (trait == null || trait.RemoldMax <= 0) return 0;
            switch (skill)
            {
                case SkillKind.Grenade: return trait.GrenadeRemoldCost;
                case SkillKind.Push: return trait.PushRemoldCost;
                case SkillKind.Sonic: return trait.SonicRemoldCost;
                default: return 0;
            }
        }

        /// <summary>스킬의 남은 재사용 대기 (리몰딩을 쓰는 스킬은 늘 0).</summary>
        public static int Cooldown(World world, CombatSettings settings, SkillKind skill)
        {
            if (Cost(world, settings, skill) > 0) return 0;
            switch (skill)
            {
                case SkillKind.Grenade: return world.GrenadeCooldown;
                case SkillKind.Push: return world.PushCooldown;
                case SkillKind.Sonic: return world.SonicCooldown;
                default: return 0;
            }
        }

        /// <summary>지금 그 스킬을 낼 수 있는가 (리몰딩이 소모만큼 있거나, 재사용 대기가 0).</summary>
        public static bool IsReady(World world, CombatSettings settings, SkillKind skill)
        {
            int cost = Cost(world, settings, skill);
            return cost > 0 ? world.PlayerRemold >= cost : Cooldown(world, settings, skill) == 0;
        }

        /// <summary>스킬을 쓴 값을 치른다: 리몰딩을 쓰거나 재사용 대기를 건다. 스킬 Resolve가 피해보다 먼저 부른다.</summary>
        public static void Pay(World world, CombatSettings settings, SkillKind skill)
        {
            int cost = Cost(world, settings, skill);
            if (cost > 0)
            {
                world.PlayerRemold = Math.Max(0, world.PlayerRemold - cost);
                return;
            }
            switch (skill)
            {
                case SkillKind.Grenade: world.GrenadeCooldown = settings.GrenadeCooldownRounds; break;
                case SkillKind.Push: world.PushCooldown = settings.PushCooldownRounds; break;
                case SkillKind.Sonic: world.SonicCooldown = settings.SonicCooldownRounds; break;
            }
        }

        private static void Gain(World world, CombatSettings settings, int amount)
        {
            int max = Max(world, settings);
            if (max <= 0 || amount <= 0) return;
            world.PlayerRemold = Math.Min(max, world.PlayerRemold + amount);
        }

        /// <summary>플레이어 행동(차례) 시작 — 전투가 시작될 때와 내 차례가 올 때 (CombatFlow · TurnFlow).</summary>
        public static void OnPlayerTurnStarted(World world, CombatSettings settings)
        {
            var trait = Trait(world, settings);
            if (trait != null) Gain(world, settings, trait.RemoldPerAction);
        }

        /// <summary>피해를 준 뒤 (체력을 깎는 곳마다): 플레이어가 다른 유닛에게 1 이상 주었으면 1회로 센다.</summary>
        public static void OnDamageDealt(World world, CombatSettings settings, Unit attacker, Unit target, int dealt)
        {
            // 상점 주인도 같은 규칙으로 리몰딩을 얻는다 (KeeperSkills, 3-16b).
            KeeperSkills.OnDamageDealt(world, settings, attacker, target, dealt);
            if (attacker == null || !attacker.IsPlayer || target == null || target == attacker || dealt <= 0) return;
            var trait = Trait(world, settings);
            if (trait != null) Gain(world, settings, trait.RemoldPerHit);
        }

        // ---- 이동 감소 ----

        public static bool IsSlowed(Unit unit) => unit != null && unit.SlowTurns > 0 && unit.SlowAmount > 0;

        /// <summary>이번 행동에서 쓸 이동 칸 (이동 감소를 뺀 값, 0 이상). 적 이동 범위 · 경로가 쓴다.</summary>
        public static int MoveRangeOf(Unit unit) => IsSlowed(unit) ? Math.Max(0, unit.MoveRange - unit.SlowAmount) : unit.MoveRange;

        /// <summary>적이 행동을 시작했다 (EnemyTurn): 이동 감소 남은 차례가 1 준다.</summary>
        public static void OnEnemyActed(Unit unit)
        {
            if (unit.SlowTurns <= 0) return;
            unit.SlowTurns--;
            if (unit.SlowTurns == 0) unit.SlowAmount = 0;
        }

        /// <summary>전투가 끝나면 남은 이동 감소를 지운다.</summary>
        public static void OnCombatEnded(World world)
        {
            foreach (var unit in world.Units)
            {
                unit.SlowAmount = 0;
                unit.SlowTurns = 0;
            }
        }

        /// <summary>
        /// 이 공격 전에 대상에게 이동 감소를 거는가: 전투 중 내 차례이고, 이번 차례에 걸은 칸(from이 지금 칸과 다르면 그 칸까지 걸음을 더함 — 이동 예약 미리보기)이 기준 이상.
        /// </summary>
        public static bool SlowsBeforeAttack(World world, CombatSettings settings, Unit attacker, GridPos? from = null)
        {
            if (attacker == null || !attacker.IsPlayer || !TurnFlow.IsPlayerTurn(world)) return false;
            var trait = Trait(world, settings);
            if (trait == null || trait.SlowAfterMoveCells <= 0 || trait.SlowAmount <= 0 || trait.SlowTurns <= 0) return false;
            int moved = world.PlayerMoveUsed;
            if (from.HasValue && from.Value != attacker.Position
                && CombatMovement.Reachable(world, attacker, Loadout.MoveLeft(world)).TryGetValue(from.Value, out int steps))
                moved += steps;
            return moved >= trait.SlowAfterMoveCells;
        }

        /// <summary>이동 감소를 가진(또는 이 공격 전에 걸릴) 적에게 주는 피해 증가 (%p). SetEffects.DamagePercent가 더한다.</summary>
        public static int SlowedDamagePercent(World world, CombatSettings settings, Unit attacker, Unit target, GridPos? from = null)
        {
            if (attacker == null || !attacker.IsPlayer || target == null || target.IsPlayer) return 0;
            var trait = Trait(world, settings);
            if (trait == null || trait.SlowedDamagePercent <= 0) return 0;
            return IsSlowed(target) || SlowsBeforeAttack(world, settings, attacker, from) ? trait.SlowedDamagePercent : 0;
        }

        /// <summary>공격 전 (피해 미리보기를 계산한 뒤, 체력을 깎기 전): 조건이 맞으면 대상에게 이동 감소를 건다.</summary>
        public static void BeforeAttack(World world, CombatSettings settings, Unit attacker, Unit target, List<GameEvent> events)
        {
            if (target == null || target.IsPlayer || !SlowsBeforeAttack(world, settings, attacker)) return;
            var trait = Trait(world, settings);
            target.SlowAmount = Math.Max(target.SlowAmount, trait.SlowAmount);
            target.SlowTurns = Math.Max(target.SlowTurns, trait.SlowTurns);
            events.Add(new UnitSlowed(target.Id, target.Kind, target.SlowAmount, target.SlowTurns));
        }
    }

    /// <summary>유닛에게 이동 감소가 걸렸다 (Remold.BeforeAttack).</summary>
    public sealed class UnitSlowed : GameEvent
    {
        public int UnitId { get; }
        public UnitKind Kind { get; }
        public int Amount { get; }
        public int Turns { get; }

        public UnitSlowed(int unitId, UnitKind kind, int amount, int turns)
        {
            UnitId = unitId;
            Kind = kind;
            Amount = amount;
            Turns = turns;
        }
    }

    /// <summary>디버그: 리몰딩 지수를 최대로 채운다.</summary>
    public sealed class DebugFillRemoldCommand : ICommand
    {
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public DebugFillRemoldCommand(CombatSettings settings) => this.settings = settings;

        public bool TryApply(World world, List<GameEvent> events)
        {
            int max = Remold.Max(world, settings);
            if (max <= 0) return false;
            world.PlayerRemold = max;
            return true;
        }
    }
}
