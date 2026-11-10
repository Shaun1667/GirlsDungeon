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
    }

    /// <summary>전투 수치 (BALANCE 4번 · 5번).</summary>
    public sealed class CombatSettings
    {
        public List<UnitStats> Units = new List<UnitStats>();
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

        public UnitStats StatsFor(UnitKind kind)
        {
            foreach (var stats in Units) if (stats.Kind == kind) return stats;
            throw new ArgumentException($"No stats for unit kind {kind}.");
        }

        public void Validate()
        {
            StatsFor(UnitKind.Player);
            StatsFor(UnitKind.Rifleman);
            foreach (var stats in Units)
            {
                if (stats.MaxHp < 1 || stats.MaxStability < 0 || stats.VisionRange < 1 || stats.Attack < 1 || stats.Defense < 0
                    || stats.MoveRange < 0 || stats.AttackRange < 1 || stats.StabilityDamage < 0
                    || stats.CritPercent < 0 || stats.CritPercent > 100 || stats.CritMultiplierPercent < 100)
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
        }
    }
}
