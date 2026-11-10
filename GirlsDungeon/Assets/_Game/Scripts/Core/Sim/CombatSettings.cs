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

        // 밀치기 스킬 (BALANCE 9번)
        public int PushLength;
        public int PushWidth;
        public int PushDamagePercent;
        public int PushCollisionDamage;
        public int PushCooldownRounds;

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

        public UnitStats StatsFor(UnitKind kind)
        {
            foreach (var stats in Units) if (stats.Kind == kind) return stats;
            throw new ArgumentException($"No stats for unit kind {kind}.");
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
            if (AssaultPercent < 0 || AssaultPercent > 100 || FallDamagePercent < 0)
                throw new ArgumentException("Assault / fall settings are invalid.");
            if (FloorCount < 1 || EnemiesPerFloorStep < 0 || AssaultPercentPerFloor < 0 || GuardPercentDropPerFloor < 0 || EnemyStatPercentPerFloor < 0)
                throw new ArgumentException("Floor progression settings are invalid.");
        }
    }
}
