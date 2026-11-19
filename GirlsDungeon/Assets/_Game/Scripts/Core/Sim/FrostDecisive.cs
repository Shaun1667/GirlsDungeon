using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>서리 결전을 쓸 수 있는지와 그 이유.</summary>
    public enum FrostDecisiveBlock
    {
        None = 0,
        /// <summary>리몰딩이 모자람.</summary>
        NotReady = 1,
        /// <summary>쓰는 자리에서 걸음 수 범위 밖.</summary>
        OutOfRange = 2,
        /// <summary>빈 칸이 아님 (걸을 수 없는 칸 · 유닛이 있는 칸).</summary>
        NotEmpty = 3,
    }

    /// <summary>서리 결전 미리보기. 실제도 같은 값을 쓴다 (설계 규칙 6).</summary>
    public readonly struct FrostDecisivePreview
    {
        public readonly FrostDecisiveBlock Block;
        public readonly GridPos Center;
        /// <summary>눈꽃 범위 칸 (피해 · 서리밭 공용).</summary>
        public readonly List<GridPos> Cells;
        /// <summary>맞는 적과 고정 피해 (모두 같은 값).</summary>
        public readonly List<Unit> Targets;
        public readonly int FixedDamage;
        public readonly int AvalancheStacks;
        public readonly int Shield;
        public readonly int StabilityRestored;
        /// <summary>정화되는 디버프가 있는가 (지금은 화상).</summary>
        public readonly bool Cleanses;

        public bool CanUse => Block == FrostDecisiveBlock.None;

        public FrostDecisivePreview(FrostDecisiveBlock block, GridPos center, List<GridPos> cells, List<Unit> targets, int fixedDamage, int avalancheStacks,
            int shield, int stabilityRestored, bool cleanses)
        {
            Block = block;
            Center = center;
            Cells = cells;
            Targets = targets;
            FixedDamage = fixedDamage;
            AvalancheStacks = avalancheStacks;
            Shield = shield;
            StabilityRestored = stabilityRestored;
            Cleanses = cleanses;
        }
    }

    /// <summary>
    /// 서리 결전의 한 곳 (BALANCE 37번, 3-17c — 결정 2026-11-19 사용자, 대원 B 결전기):
    /// - 걸음 수 DecisiveRange 안의 빈 칸을 고른다. 그 칸에서 걸음 수 DecisiveRadius 안(눈꽃 모양 = 마름모, 사용자 그림)의 모든 적에게
    ///   쓰는 사람 방어력 × DecisiveFixedDefensePercent 고정 피해 + 눈사태 DecisiveAvalancheStacks스택.
    /// - 자신(아군): 안정 +DecisiveStabilityRestore · 디버프 1개 정화(지금은 화상) · 도발 해제(도발이 아직 없음) · 서리 보호막(기초 공격력 × DecisiveShieldPercent, DecisiveBuffTurns 차례 — 깨지면 결빙)
    ///   · 빙설 보루 BulwarkStacksPerUse스택(DecisiveBuffTurns 차례).
    /// - 쓴 뒤 그 범위에 서리밭 DecisiveFieldTurns 차례.
    /// 리몰딩을 쓴다 (캐릭터 줄 소모). 쓰면 차례가 끝난다.
    /// </summary>
    public static class FrostDecisive
    {
        public static FrostDecisivePreview Preview(World world, CombatSettings settings, Unit user, GridPos center)
        {
            var cells = FrostGuard.RangeCells(world, center, settings.DecisiveRadius);
            var targets = new List<Unit>();
            foreach (var unit in world.Units)
            {
                if (unit.IsPlayer || unit.Hp <= 0 || Shop.IsNeutral(world, unit)) continue;
                if (Math.Abs(unit.Position.X - center.X) + Math.Abs(unit.Position.Y - center.Y) <= settings.DecisiveRadius) targets.Add(unit);
            }
            int fixedDamage = FrostAttack.FixedDamage(user.Defense, settings.DecisiveFixedDefensePercent);
            int shield = Math.Max(world.PlayerShield, Loadout.Percent(FrostGuard.BaseAttack(world, settings), settings.DecisiveShieldPercent));
            int restored = Math.Min(settings.DecisiveStabilityRestore, Math.Max(0, user.MaxStability - user.Stability));
            bool cleanses = world.PlayerBurnTurns > 0;

            var block = FrostDecisiveBlock.None;
            if (user.IsPlayer && !Remold.IsReady(world, settings, SkillKind.FrostDecisive)) block = FrostDecisiveBlock.NotReady;
            else if (Math.Abs(center.X - user.Position.X) + Math.Abs(center.Y - user.Position.Y) > settings.DecisiveRange) block = FrostDecisiveBlock.OutOfRange;
            else if (!world.Map.InBounds(center) || !world.Map.IsWalkable(center) || world.IsOccupied(center)) block = FrostDecisiveBlock.NotEmpty;
            return new FrostDecisivePreview(block, center, cells, targets, fixedDamage, settings.DecisiveAvalancheStacks, shield, restored, cleanses);
        }

        /// <summary>서리 결전을 쓴다. 쓸 수 없으면 아무것도 바꾸지 않고 false.</summary>
        public static bool Resolve(World world, CombatSettings settings, Unit user, GridPos center, List<GameEvent> events)
        {
            var preview = Preview(world, settings, user, center);
            if (!preview.CanUse) return false;

            Remold.Pay(world, settings, SkillKind.FrostDecisive);
            events.Add(new FrostDecisiveUsed(user.Id, center, preview.Targets.Count));

            // 적: 고정 피해 + 눈사태.
            foreach (var target in preview.Targets)
            {
                if (world.FindUnit(target.Id) == null) continue;
                int dealt = KeeperSkills.AfterShield(world, target, preview.FixedDamage, events, user, settings);
                target.Hp -= dealt;
                Remold.OnDamageDealt(world, settings, user, target, dealt);
                events.Add(new UnitAttacked(user, target, dealt, false, CoverLevel.None, false, false));
                events.Add(new UnitStatsChanged(target.Id));
                Alert.Wake(world, target, events);
                if (target.Hp <= 0)
                {
                    CombatFlow.Kill(world, target, events);
                    continue;
                }
                FrostStatus.AddAvalanche(world, settings, target, preview.AvalancheStacks, events);
            }

            // 자신: 안정 · 정화 · 보호막 · 빙설 보루.
            user.Stability += preview.StabilityRestored;
            if (preview.Cleanses) world.PlayerBurnTurns = 0;
            world.PlayerShield = preview.Shield;
            world.PlayerShieldTurns = preview.Shield > 0 ? settings.DecisiveBuffTurns : 0;
            world.PlayerShieldFreezeTurns = settings.FreezeTurns;
            world.BulwarkStacks = Math.Min(settings.BulwarkMaxStacks, world.BulwarkStacks + settings.BulwarkStacksPerUse);
            world.BulwarkTurns = settings.DecisiveBuffTurns;
            events.Add(new FrostDecisiveSelf(user.Id, preview.StabilityRestored, preview.Cleanses, world.PlayerShield, world.BulwarkStacks));
            events.Add(new UnitStatsChanged(user.Id));

            // 서리밭.
            if (settings.DecisiveFieldTurns > 0)
            {
                world.FrostFields.Add(new FrostField { Center = center, Radius = settings.DecisiveRadius, Turns = settings.DecisiveFieldTurns });
                events.Add(new FrostFieldChanged());
            }
            return true;
        }
    }

    /// <summary>플레이어 서리 결전 스킬. 캐릭터가 이 스킬을 가지고 있어야 한다. 쓰면 차례가 끝나고 적 차례가 이어진다 (전투가 끝났으면 그대로).</summary>
    public sealed class FrostDecisiveCommand : ICommand
    {
        private readonly GridPos center;
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public FrostDecisiveCommand(GridPos center, CombatSettings settings)
        {
            this.center = center;
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!CombatFlow.CanUseSkill(world)) return false;
            if (!settings.HasSkill(world.PlayerCharacter, SkillKind.FrostDecisive)) return false;
            if (!FrostDecisive.Resolve(world, settings, world.Player, center, events)) return false;
            SetEffects.OnSkillUsed(world, settings, SkillKind.FrostDecisive);
            CombatFlow.AfterSkill(world, settings, events);
            return true;
        }
    }

    /// <summary>서리 결전을 썼다 (Hits = 범위 안 적 수).</summary>
    public sealed class FrostDecisiveUsed : GameEvent
    {
        public int UnitId { get; }
        public GridPos Center { get; }
        public int Hits { get; }

        public FrostDecisiveUsed(int unitId, GridPos center, int hits)
        {
            UnitId = unitId;
            Center = center;
            Hits = hits;
        }
    }

    /// <summary>서리 결전의 자신 효과 (안정 · 정화 · 보호막 · 빙설 보루).</summary>
    public sealed class FrostDecisiveSelf : GameEvent
    {
        public int UnitId { get; }
        public int StabilityRestored { get; }
        public bool Cleansed { get; }
        public int Shield { get; }
        public int BulwarkStacks { get; }

        public FrostDecisiveSelf(int unitId, int stabilityRestored, bool cleansed, int shield, int bulwarkStacks)
        {
            UnitId = unitId;
            StabilityRestored = stabilityRestored;
            Cleansed = cleansed;
            Shield = shield;
            BulwarkStacks = bulwarkStacks;
        }
    }

    /// <summary>서리밭이 생기거나 사라졌다 (화면이 다시 그린다).</summary>
    public sealed class FrostFieldChanged : GameEvent
    {
    }
}
