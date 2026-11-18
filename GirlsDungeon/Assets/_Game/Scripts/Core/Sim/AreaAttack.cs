using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>범위 공격을 던질 수 있는지와 그 이유.</summary>
    public enum AreaBlock
    {
        None = 0,
        NotReady = 1,
        OutOfRange = 2,
        NotVisible = 3,
    }

    /// <summary>수류탄 범위 모양 (캐릭터 줄 GrenadeShape, 2026-11-14). 끝에만 추가.</summary>
    public enum AreaShape
    {
        /// <summary>정사각형: 가로 · 세로 · 대각 반경 칸 (반경 1 = 3 × 3).</summary>
        Square = 0,
        /// <summary>마름모: 가로 + 세로 거리 합이 반경 이하 (반경 2 = 13칸).</summary>
        Diamond = 1,
    }

    /// <summary>범위 공격 한 대상의 미리보기.</summary>
    public readonly struct AreaHit
    {
        public readonly Unit Target;
        public readonly CoverLevel Cover;
        public readonly int ReductionPercent;
        public readonly int Damage;
        public readonly int StabilityDamage;

        /// <summary>세트 효과로 바뀐 피해 (%p, SetEffects).</summary>
        public readonly int SetPercent;

        public AreaHit(Unit target, CoverLevel cover, int reductionPercent, int damage, int stabilityDamage, int setPercent = 0)
        {
            SetPercent = setPercent;
            Target = target;
            Cover = cover;
            ReductionPercent = reductionPercent;
            Damage = damage;
            StabilityDamage = stabilityDamage;
        }
    }

    /// <summary>범위 공격 미리보기. 실제 폭발도 같은 값을 쓴다 (설계 규칙 6).</summary>
    public sealed class AreaPreview
    {
        public AreaBlock Block { get; }
        public GridPos Center { get; }
        public IReadOnlyList<GridPos> Cells { get; }
        public IReadOnlyList<AreaHit> Hits { get; }
        public bool HitsPlayer { get; }

        /// <summary>이 폭발로 부서지는 상자 · 낮은 벽 수.</summary>
        public int CoverBreaks { get; }

        public bool CanThrow => Block == AreaBlock.None;

        public AreaPreview(AreaBlock block, GridPos center, IReadOnlyList<GridPos> cells, IReadOnlyList<AreaHit> hits, bool hitsPlayer, int coverBreaks)
        {
            CoverBreaks = coverBreaks;
            Block = block;
            Center = center;
            Cells = cells;
            Hits = hits;
            HitsPlayer = hitsPlayer;
        }
    }

    /// <summary>
    /// 범위 공격(수류탄 스킬)의 한 곳 (BALANCE 6번).
    /// - 던질 수 있는 칸: 사거리 안이고 지금 보이는 칸 (Vision). 높은 엄폐 너머로도 던질 수 있다.
    /// - 범위: 폭심지에서 반경 · 모양(CombatSettings.GrenadeRadiusFor · GrenadeShapeFor — 캐릭터 줄, 없으면 기본 정사각형 GrenadeRadius). 플레이어도 맞는다.
    /// - 피해: 공격력 공식 × 피해 배율, 엄폐는 폭심지 기준이고 안정지수의 감소(StabilityGuard)는 무시한다. 치명타 없음.
    /// </summary>
    public static class AreaAttack
    {
        public static AreaPreview Preview(World world, CombatSettings settings, Unit attacker, GridPos center)
        {
            var map = world.Map;
            var block = AreaBlock.None;
            if (attacker.IsPlayer && !Remold.IsReady(world, settings, SkillKind.Grenade)) block = AreaBlock.NotReady;
            else
            {
                int dx = center.X - attacker.Position.X;
                int dy = center.Y - attacker.Position.Y;
                if (!map.InBounds(center) || dx * dx + dy * dy > settings.GrenadeThrowRange * settings.GrenadeThrowRange)
                    block = AreaBlock.OutOfRange;
                else if (!Vision.IsSet(Vision.Compute(map, attacker.Position, attacker.VisionRange), map, center) || map.BlocksSight(center))
                    block = AreaBlock.NotVisible;
            }

            var character = attacker.IsPlayer ? world.PlayerCharacter : CharacterKind.None;
            int radius = settings.GrenadeRadiusFor(character);
            var shape = settings.GrenadeShapeFor(character);
            var cells = new List<GridPos>();
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
                for (int x = center.X - radius; x <= center.X + radius; x++)
                    if (map.InBounds(new GridPos(x, y)) && InArea(x - center.X, y - center.Y, radius, shape)) cells.Add(new GridPos(x, y));

            var hits = new List<AreaHit>();
            bool hitsPlayer = false;
            foreach (var unit in world.Units)
            {
                // 던진 사람은 자기 수류탄에 맞지 않는다 (2026-11-18 사용자, 3-16t).
                if (unit == attacker) continue;
                var position = unit.Position;
                if (!InArea(position.X - center.X, position.Y - center.Y, radius, shape)) continue;

                var cover = Cover.Against(map, position, center);
                int reduction = cover == CoverLevel.Full ? settings.FullCoverReductionPercent
                    : cover == CoverLevel.Half ? settings.HalfCoverReductionPercent : 0;
                int setPercent = SetEffects.DamagePercent(world, settings, attacker, unit, DamageSource.Grenade);
                int damage = CombatRules.Damage(attacker.Attack, unit.Defense, settings.SkillDamagePercentFor(character, SkillKind.Grenade), reduction, settings.MinDamage, 100, setPercent);
                hits.Add(new AreaHit(unit, cover, reduction, damage, settings.GrenadeStabilityDamage, setPercent));
                if (unit.IsPlayer) hitsPlayer = true;
            }

            int coverBreaks = 0;
            foreach (var piece in CoverBreak.InArea(map, cells))
                if (CoverBreak.BreaksOnNextHit(map, settings, piece)) coverBreaks++;

            return new AreaPreview(block, center, cells, hits, hitsPlayer, coverBreaks);
        }

        /// <summary>폭심지에서 (dx, dy) 떨어진 칸이 범위 안인지.</summary>
        public static bool InArea(int dx, int dy, int radius, AreaShape shape)
        {
            dx = Math.Abs(dx);
            dy = Math.Abs(dy);
            return shape == AreaShape.Diamond ? dx + dy <= radius : Math.Max(dx, dy) <= radius;
        }

        /// <summary>폭발을 처리한다. 던질 수 없으면 아무것도 바꾸지 않고 false.</summary>
        public static bool Resolve(World world, CombatSettings settings, Unit attacker, GridPos center, List<GameEvent> events)
        {
            var preview = Preview(world, settings, attacker, center);
            if (!preview.CanThrow) return false;

            if (attacker.IsPlayer) Remold.Pay(world, settings, SkillKind.Grenade);
            events.Add(new AreaExploded(attacker.Id, attacker.Kind, center, preview.Cells));
            var downed = new List<Unit>();
            foreach (var hit in preview.Hits)
            {
                var target = hit.Target;
                Remold.BeforeAttack(world, settings, attacker, target, events);
                int stabilityBefore = target.Stability;
                int dealt = KeeperSkills.AfterShield(world, target, hit.Damage, events);
                target.Hp -= dealt;
                Remold.OnDamageDealt(world, settings, attacker, target, dealt);
                target.Stability = Math.Max(0, target.Stability - hit.StabilityDamage);
                events.Add(new UnitAttacked(attacker, target, dealt, false, hit.Cover, false, stabilityBefore > 0 && target.Stability == 0));
                events.Add(new UnitStatsChanged(target.Id));
                Alert.Wake(world, target, events);
                if (target.Hp <= 0) downed.Add(target);
            }

            // 피해는 이미 깎이기 전 엄폐로 계산했다. 폭발 칸에 걸린 상자 · 낮은 벽을 깎는다.
            CoverBreak.Hit(world, settings, CoverBreak.InArea(world.Map, preview.Cells), events);

            foreach (var target in downed)
            {
                if (target.IsPlayer)
                {
                    target.Hp = 0;
                    world.Mode = WorldMode.Defeat;
                    events.Add(new PlayerDefeated(attacker.Id));
                }
                else
                {
                    CombatFlow.Kill(world, target, events);
                }
            }
            return true;
        }
    }

    /// <summary>플레이어 수류탄 스킬. 쓰면 차례가 끝나고 적 차례가 이어진다 (전투가 끝났거나 쓰러졌으면 그대로).</summary>
    public sealed class AreaAttackCommand : ICommand
    {
        private readonly GridPos center;
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public AreaAttackCommand(GridPos center, CombatSettings settings)
        {
            this.center = center;
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!CombatFlow.CanUseSkill(world)) return false;
            if (!settings.HasSkill(world.PlayerCharacter, SkillKind.Grenade)) return false;
            if (!AreaAttack.Resolve(world, settings, world.Player, center, events)) return false;
            SetEffects.OnSkillUsed(world, settings, SkillKind.Grenade);
            CombatFlow.AfterSkill(world, settings, events);
            return true;
        }
    }
}
