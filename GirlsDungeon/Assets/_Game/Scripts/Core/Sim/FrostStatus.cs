using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>서리밭 한 곳 (3-17c): 가운데 칸 · 걸음 수 반경 · 남은 차례.</summary>
    public sealed class FrostField
    {
        public GridPos Center;
        public int Radius;
        public int Turns;

        public bool Contains(GridPos cell) => Math.Abs(cell.X - Center.X) + Math.Abs(cell.Y - Center.Y) <= Radius;
    }

    /// <summary>
    /// 빙결계 상태의 한 곳 (BALANCE 37번, 3-17c — 결정 2026-11-19 사용자 설명):
    /// - 눈사태 (적, Unit.AvalancheStacks): 다른 빙결계 디버프(응고 · 결빙)를 받을 때 +1스택 (이미 눈사태가 있을 때). AvalancheBurstStacks가 되면 고정 안정 피해 AvalancheStabilityDamage, 스택은 0으로.
    /// - 응고 (적, Unit.CoagStacks · CoagTurns): 스택마다 이동 −CoagMovePerStack, 최대 CoagMaxStacks, CoagTurns 행동 동안. 행동을 마칠 때 최대 스택이면 결빙으로 바뀐다.
    /// - 결빙 (적, Unit.FrozenTurns): 이동할 수 없다 (Remold.MoveRangeOf).
    /// - 서리밭 (World.FrostFields): 그 안에서 행동을 마친 적에게 응고 1스택. 내 차례 시작마다 남은 차례 −1. 빙결 약점은 약점 시스템이 생기면 (사용자 결정).
    /// - 빙설 보루 (플레이어, World.BulwarkStacks · BulwarkTurns): 받는 피해 −BulwarkDamagePercent% (SetEffects.DamagePercent — 미리보기 공통), 공격을 받을 때마다 −1스택, 최대 BulwarkMaxStacks.
    /// 전투가 끝나거나 층을 옮기면 모두 지운다.
    /// </summary>
    public static class FrostStatus
    {
        /// <summary>적에게 빙결계 디버프가 걸렸다: 눈사태가 있으면 +1스택 (그리고 터지는지 확인).</summary>
        private static void OnFrostDebuff(World world, CombatSettings settings, Unit unit, List<GameEvent> events)
        {
            if (unit.AvalancheStacks > 0) AddAvalanche(world, settings, unit, 1, events);
        }

        /// <summary>눈사태 스택을 더한다. 터질 수만큼 쌓이면 고정 안정 피해를 주고 0으로.</summary>
        public static void AddAvalanche(World world, CombatSettings settings, Unit unit, int stacks, List<GameEvent> events)
        {
            if (unit == null || unit.IsPlayer || stacks <= 0) return;
            unit.AvalancheStacks += stacks;
            events?.Add(new AvalancheStacked(unit.Id, unit.Kind, unit.AvalancheStacks));
            if (settings.AvalancheBurstStacks <= 0 || unit.AvalancheStacks < settings.AvalancheBurstStacks) return;
            unit.AvalancheStacks = 0;
            int before = unit.Stability;
            unit.Stability = Math.Max(0, unit.Stability - settings.AvalancheStabilityDamage);
            events?.Add(new AvalancheBurst(unit.Id, unit.Kind, before - unit.Stability, before > 0 && unit.Stability == 0));
            events?.Add(new UnitStatsChanged(unit.Id));
        }

        /// <summary>결빙을 건다 (서리 보호막이 깨질 때 · 응고 2스택). 빙결계 디버프라 눈사태가 쌓인다.</summary>
        public static void Freeze(World world, CombatSettings settings, Unit unit, int turns, List<GameEvent> events)
        {
            if (unit == null || unit.IsPlayer || turns <= 0) return;
            unit.FrozenTurns = Math.Max(unit.FrozenTurns, turns);
            events?.Add(new UnitFrozen(unit.Id, unit.Kind, unit.FrozenTurns));
            if (settings != null) OnFrostDebuff(world, settings, unit, events);
        }

        /// <summary>응고 1스택 (최대까지), 지속은 새로.</summary>
        public static void Coagulate(World world, CombatSettings settings, Unit unit, List<GameEvent> events)
        {
            if (unit == null || unit.IsPlayer) return;
            unit.CoagStacks = Math.Min(settings.CoagMaxStacks, unit.CoagStacks + 1);
            unit.CoagTurns = settings.CoagTurns;
            unit.CoagMove = unit.CoagStacks * settings.CoagMovePerStack;
            events?.Add(new UnitCoagulated(unit.Id, unit.Kind, unit.CoagStacks));
            OnFrostDebuff(world, settings, unit, events);
        }

        /// <summary>적이 행동을 마쳤다 (EnemyTurn): 서리밭 안이면 응고, 응고가 최대 스택이면 결빙으로 바뀐다.</summary>
        public static void OnEnemyActionEnded(World world, CombatSettings settings, Unit enemy, List<GameEvent> events)
        {
            if (settings == null || enemy == null || enemy.IsPlayer) return;
            foreach (var field in world.FrostFields)
            {
                if (!field.Contains(enemy.Position)) continue;
                Coagulate(world, settings, enemy, events);
                break;
            }
            if (settings.CoagMaxStacks > 0 && enemy.CoagStacks >= settings.CoagMaxStacks)
            {
                enemy.CoagStacks = 0;
                enemy.CoagTurns = 0;
                enemy.CoagMove = 0;
                Freeze(world, settings, enemy, settings.CoagFreezeTurns, events);
            }
        }

        /// <summary>적이 행동을 시작했다 (Remold.OnEnemyActed가 부름 — 이동 칸을 정한 뒤): 응고 남은 행동 −1.</summary>
        public static void OnEnemyActed(Unit unit)
        {
            if (unit.CoagTurns > 0 && --unit.CoagTurns == 0)
            {
                unit.CoagStacks = 0;
                unit.CoagMove = 0;
            }
        }

        /// <summary>내 차례 시작 (FrostGuard.OnPlayerTurnStarted가 부름): 서리밭 · 빙설 보루 남은 차례 −1.</summary>
        public static void OnPlayerTurnStarted(World world, List<GameEvent> events)
        {
            bool removed = false;
            for (int i = world.FrostFields.Count - 1; i >= 0; i--)
            {
                if (--world.FrostFields[i].Turns > 0) continue;
                world.FrostFields.RemoveAt(i);
                removed = true;
            }
            if (removed) events?.Add(new FrostFieldChanged());
            if (world.BulwarkTurns > 0 && --world.BulwarkTurns == 0) world.BulwarkStacks = 0;
        }

        /// <summary>빙설 보루가 받는 피해를 줄이는 양 (%p, 0 = 없음). SetEffects.DamagePercent가 뺀다.</summary>
        public static int BulwarkReduction(World world, CombatSettings settings, Unit target)
            => target != null && target.IsPlayer && world.BulwarkStacks > 0 && settings != null ? settings.BulwarkDamagePercent : 0;

        /// <summary>플레이어가 공격을 받았다 (KeeperSkills.AfterShield): 빙설 보루 −1스택.</summary>
        public static void OnPlayerAttacked(World world)
        {
            if (world.BulwarkStacks <= 0) return;
            if (--world.BulwarkStacks == 0) world.BulwarkTurns = 0;
        }

        /// <summary>전투가 끝나거나 층을 옮기면 모두 지운다.</summary>
        public static void Clear(World world)
        {
            world.FrostFields.Clear();
            world.BulwarkStacks = 0;
            world.BulwarkTurns = 0;
            foreach (var unit in world.Units)
            {
                unit.AvalancheStacks = 0;
                unit.CoagStacks = 0;
                unit.CoagTurns = 0;
                unit.CoagMove = 0;
            }
        }
    }

    /// <summary>눈사태 스택이 쌓였다 (Stacks = 지금).</summary>
    public sealed class AvalancheStacked : GameEvent
    {
        public int UnitId { get; }
        public UnitKind Kind { get; }
        public int Stacks { get; }

        public AvalancheStacked(int unitId, UnitKind kind, int stacks)
        {
            UnitId = unitId;
            Kind = kind;
            Stacks = stacks;
        }
    }

    /// <summary>눈사태가 터졌다: 고정 안정 피해 StabilityDamage.</summary>
    public sealed class AvalancheBurst : GameEvent
    {
        public int UnitId { get; }
        public UnitKind Kind { get; }
        public int StabilityDamage { get; }
        public bool StabilityBroken { get; }

        public AvalancheBurst(int unitId, UnitKind kind, int stabilityDamage, bool stabilityBroken)
        {
            UnitId = unitId;
            Kind = kind;
            StabilityDamage = stabilityDamage;
            StabilityBroken = stabilityBroken;
        }
    }

    /// <summary>응고가 걸렸다 (Stacks = 지금).</summary>
    public sealed class UnitCoagulated : GameEvent
    {
        public int UnitId { get; }
        public UnitKind Kind { get; }
        public int Stacks { get; }

        public UnitCoagulated(int unitId, UnitKind kind, int stacks)
        {
            UnitId = unitId;
            Kind = kind;
            Stacks = stacks;
        }
    }
}
