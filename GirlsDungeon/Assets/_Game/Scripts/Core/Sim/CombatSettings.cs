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
    }

    /// <summary>전투 수치 (BALANCE 4번 · 5번).</summary>
    public sealed class CombatSettings
    {
        public List<UnitStats> Units = new List<UnitStats>();
        public int MinEnemiesPerFloor;
        public int MaxEnemiesPerFloor;
        public int EnemyMinDistanceFromStart;
        public int StabilityLossPerHit;
        public int EndTurnCostTicks;

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
                if (stats.MaxHp < 1 || stats.MaxStability < 0 || stats.VisionRange < 1)
                    throw new ArgumentException($"Stats for {stats.Kind} are invalid.");
            if (MinEnemiesPerFloor < 0 || MaxEnemiesPerFloor < MinEnemiesPerFloor || EnemyMinDistanceFromStart < 0
                || StabilityLossPerHit < 0 || EndTurnCostTicks < 0)
                throw new ArgumentException("Combat settings are invalid.");
        }
    }
}
