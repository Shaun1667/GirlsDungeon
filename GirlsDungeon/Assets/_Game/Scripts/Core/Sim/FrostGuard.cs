using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>서리 수호 미리보기. 실제도 같은 값을 쓴다 (설계 규칙 6).</summary>
    public readonly struct FrostGuardPreview
    {
        public readonly bool Ready;
        /// <summary>쓰고 난 뒤 보호막 양 (지금 보호막과 새 보호막 중 큰 쪽).</summary>
        public readonly int Shield;
        public readonly int StabilityRestored;
        /// <summary>범위 안 적 수 = 더하는 방위 지원 스택 (최대 스택까지).</summary>
        public readonly int EnemiesInRange;
        public readonly int StacksAfter;
        public readonly int DefenseBonusAfter;
        /// <summary>방위 지원을 세는 범위 칸 (화면 표시용).</summary>
        public readonly List<GridPos> Cells;

        public bool CanUse => Ready;

        public FrostGuardPreview(bool ready, int shield, int stabilityRestored, int enemiesInRange, int stacksAfter, int defenseBonusAfter, List<GridPos> cells)
        {
            Ready = ready;
            Shield = shield;
            StabilityRestored = stabilityRestored;
            EnemiesInRange = enemiesInRange;
            StacksAfter = stacksAfter;
            DefenseBonusAfter = defenseBonusAfter;
            Cells = cells;
        }
    }

    /// <summary>
    /// 서리 수호 스킬 · 서리 보호막 · 방위 지원 · 결빙의 한 곳 (BALANCE 36번, 3-17b — 결정 2026-11-19 사용자, 대원 B 스킬2):
    /// - 쓰면: 자신에게 서리 보호막(기초 공격력 × FrostGuardShieldPercent, FrostGuardShieldTurns 차례) · 안정지수 +FrostGuardStabilityRestore ·
    ///   범위(걸음 수 FrostGuardRange) 안 적 하나마다 방위 지원 1스택(최대 DefenseSupportMaxStacks, DefenseSupportTurns 차례). 리몰딩을 쓴다 (캐릭터 줄 소모).
    /// - 서리 보호막: 플레이어가 받는 공격 피해를 먼저 막는다 (KeeperSkills.AfterShield가 부름). 깨지면 깨뜨린 공격자에게 결빙(FreezeTurns 행동 동안 이동 불가).
    /// - 방위 지원: 방어 + 기초 방어(캐릭터 방어, 파츠 전) × DefenseSupportPercent × 스택 (Loadout.Apply가 더함 — 표시 값 = 실제 값).
    /// - 남은 차례는 내 차례가 시작될 때 1씩 준다. 전투가 끝나거나 층을 옮기면 모두 사라진다 (Clear).
    /// </summary>
    public static class FrostGuard
    {
        /// <summary>기초 공격력 = 든 무기 공격력 (파츠 전).</summary>
        public static int BaseAttack(World world, CombatSettings settings) => settings.TryWeapon(world.PlayerWeapon)?.Attack ?? world.Player.Attack;

        public static int ShieldAmount(World world, CombatSettings settings) => Loadout.Percent(BaseAttack(world, settings), settings.FrostGuardShieldPercent);

        /// <summary>범위 칸: 쓰는 자리에서 걸음 수(가로 + 세로) range 이내, 맵 안 (자기 칸 포함).</summary>
        public static List<GridPos> RangeCells(World world, GridPos center, int range)
        {
            var cells = new List<GridPos>();
            for (int dy = -range; dy <= range; dy++)
                for (int dx = -range; dx <= range; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > range) continue;
                    var cell = new GridPos(center.X + dx, center.Y + dy);
                    if (world.Map.InBounds(cell)) cells.Add(cell);
                }
            return cells;
        }

        /// <summary>범위 안의 적 수 (중립 상점 주인은 빼고).</summary>
        public static int EnemiesInRange(World world, GridPos center, int range)
        {
            int count = 0;
            foreach (var unit in world.Units)
            {
                if (unit.IsPlayer || unit.Hp <= 0 || Shop.IsNeutral(world, unit)) continue;
                if (Math.Abs(unit.Position.X - center.X) + Math.Abs(unit.Position.Y - center.Y) <= range) count++;
            }
            return count;
        }

        /// <summary>방위 지원으로 더하는 방어 (Loadout.Apply가 부른다). baseDefense = 파츠 · 방위 지원을 뺀 몸 방어.</summary>
        public static int DefenseSupportBonus(World world, CombatSettings settings, int baseDefense)
        {
            if (world.DefenseSupportStacks <= 0 || settings == null || baseDefense <= 0) return 0;
            return Loadout.Percent(baseDefense, settings.DefenseSupportPercent * world.DefenseSupportStacks);
        }

        public static FrostGuardPreview Preview(World world, CombatSettings settings, Unit user)
        {
            bool ready = !user.IsPlayer || Remold.IsReady(world, settings, SkillKind.FrostGuard);
            int shield = Math.Max(world.PlayerShield, ShieldAmount(world, settings));
            int restored = Math.Min(settings.FrostGuardStabilityRestore, Math.Max(0, user.MaxStability - user.Stability));
            int enemies = EnemiesInRange(world, user.Position, settings.FrostGuardRange);
            int stacks = Math.Min(settings.DefenseSupportMaxStacks, world.DefenseSupportStacks + enemies);
            int baseDefense = user.Defense - world.PartAppliedDefense - world.DefenseSupportApplied;
            int bonus = baseDefense > 0 ? Loadout.Percent(baseDefense, settings.DefenseSupportPercent * stacks) : 0;
            return new FrostGuardPreview(ready, shield, restored, enemies, stacks, bonus, RangeCells(world, user.Position, settings.FrostGuardRange));
        }

        /// <summary>서리 수호를 쓴다. 쓸 수 없으면 아무것도 바꾸지 않고 false.</summary>
        public static bool Resolve(World world, CombatSettings settings, Unit user, List<GameEvent> events)
        {
            var preview = Preview(world, settings, user);
            if (!preview.CanUse) return false;

            Remold.Pay(world, settings, SkillKind.FrostGuard);
            world.PlayerShield = preview.Shield;
            world.PlayerShieldTurns = preview.Shield > 0 ? settings.FrostGuardShieldTurns : 0;
            world.PlayerShieldFreezeTurns = settings.FreezeTurns;
            user.Stability += preview.StabilityRestored;
            int added = preview.StacksAfter - world.DefenseSupportStacks;
            if (preview.EnemiesInRange > 0)
            {
                world.DefenseSupportStacks = preview.StacksAfter;
                world.DefenseSupportTurns = settings.DefenseSupportTurns;
            }
            Loadout.Apply(world, settings);
            events.Add(new FrostGuardUsed(user.Id, world.PlayerShield, preview.StabilityRestored, added, world.DefenseSupportStacks, world.DefenseSupportApplied));
            events.Add(new UnitStatsChanged(user.Id));
            return true;
        }

        /// <summary>
        /// 플레이어가 받는 피해를 서리 보호막이 먼저 막는다. 남은 피해를 돌려준다.
        /// 보호막이 이번에 깨졌고 공격자가 있으면(플레이어가 아닌 유닛) 결빙을 건다.
        /// </summary>
        public static int AbsorbPlayerShield(World world, Unit attacker, int damage, List<GameEvent> events, CombatSettings settings = null)
        {
            if (world.PlayerShield <= 0 || damage <= 0) return damage;
            int absorbed = Math.Min(world.PlayerShield, damage);
            world.PlayerShield -= absorbed;
            events?.Add(new PlayerShieldAbsorbed(absorbed, world.PlayerShield));
            if (world.PlayerShield > 0) return damage - absorbed;

            world.PlayerShieldTurns = 0;
            if (attacker != null && !attacker.IsPlayer)
            {
                // 결빙은 빙결계 디버프 — 눈사태가 쌓인다 (FrostStatus, 3-17c).
                FrostStatus.Freeze(world, settings, attacker, world.PlayerShieldFreezeTurns, events);
            }
            return damage - absorbed;
        }

        /// <summary>내 차례 시작 (CombatFlow): 보호막 · 방위 지원 남은 차례가 1 준다. 0이 되면 사라진다.</summary>
        public static void OnPlayerTurnStarted(World world, CombatSettings settings, List<GameEvent> events)
        {
            FrostStatus.OnPlayerTurnStarted(world, events);
            if (world.PlayerShieldTurns > 0 && --world.PlayerShieldTurns == 0) world.PlayerShield = 0;
            if (world.DefenseSupportTurns > 0 && --world.DefenseSupportTurns == 0)
            {
                world.DefenseSupportStacks = 0;
                if (settings != null) Loadout.Apply(world, settings);
                events.Add(new UnitStatsChanged(world.PlayerId));
            }
        }

        /// <summary>전투가 끝나거나 층을 옮기면 모두 지운다 (수치 없이 — 방어에 더해 둔 양을 그대로 뺀다).</summary>
        public static void Clear(World world)
        {
            world.PlayerShield = 0;
            world.PlayerShieldTurns = 0;
            world.PlayerShieldFreezeTurns = 0;
            world.DefenseSupportStacks = 0;
            world.DefenseSupportTurns = 0;
            if (world.Player != null) world.Player.Defense -= world.DefenseSupportApplied;
            world.DefenseSupportApplied = 0;
            FrostStatus.Clear(world);
        }
    }

    /// <summary>플레이어 서리 수호 스킬. 캐릭터가 이 스킬을 가지고 있어야 한다. 쓰면 차례가 끝나고 적 차례가 이어진다 (전투가 끝났으면 그대로).</summary>
    public sealed class FrostGuardCommand : ICommand
    {
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public FrostGuardCommand(CombatSettings settings)
        {
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!CombatFlow.CanUseSkill(world)) return false;
            if (!settings.HasSkill(world.PlayerCharacter, SkillKind.FrostGuard)) return false;
            if (!FrostGuard.Resolve(world, settings, world.Player, events)) return false;
            SetEffects.OnSkillUsed(world, settings, SkillKind.FrostGuard);
            CombatFlow.AfterSkill(world, settings, events);
            return true;
        }
    }

    /// <summary>서리 수호를 썼다. Shield = 지금 보호막, StacksAdded · Stacks = 방위 지원, DefenseBonus = 더해진 방어.</summary>
    public sealed class FrostGuardUsed : GameEvent
    {
        public int UnitId { get; }
        public int Shield { get; }
        public int StabilityRestored { get; }
        public int StacksAdded { get; }
        public int Stacks { get; }
        public int DefenseBonus { get; }

        public FrostGuardUsed(int unitId, int shield, int stabilityRestored, int stacksAdded, int stacks, int defenseBonus)
        {
            UnitId = unitId;
            Shield = shield;
            StabilityRestored = stabilityRestored;
            StacksAdded = stacksAdded;
            Stacks = stacks;
            DefenseBonus = defenseBonus;
        }
    }

    /// <summary>서리 보호막이 피해를 막았다. Remaining = 남은 보호막 (0 = 깨짐).</summary>
    public sealed class PlayerShieldAbsorbed : GameEvent
    {
        public int Absorbed { get; }
        public int Remaining { get; }

        public PlayerShieldAbsorbed(int absorbed, int remaining)
        {
            Absorbed = absorbed;
            Remaining = remaining;
        }
    }

    /// <summary>결빙에 걸렸다 (Turns 행동 동안 이동 불가).</summary>
    public sealed class UnitFrozen : GameEvent
    {
        public int UnitId { get; }
        public UnitKind Kind { get; }
        public int Turns { get; }

        public UnitFrozen(int unitId, UnitKind kind, int turns)
        {
            UnitId = unitId;
            Kind = kind;
            Turns = turns;
        }
    }
}
