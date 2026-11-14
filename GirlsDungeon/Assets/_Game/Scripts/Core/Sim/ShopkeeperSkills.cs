using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>상점 주인 스킬 수치 (BALANCE 33번, 에셋 keeperSkills). CombatSettings.Keeper가 null이면 스킬 없이 사격병처럼 싸운다.</summary>
    public sealed class KeeperSkillSettings
    {
        /// <summary>일반 공격의 공격력 배율 (%).</summary>
        public int BasicPercent;

        /// <summary>밑천(스택) 최대 개수.</summary>
        public int StackMax;
        /// <summary>밑천 1개마다 줄어드는 받는 피해 (%p).</summary>
        public int StackDamageTakenPercent;
        /// <summary>스킬로 체력을 써도 남기는 최소 체력 (최대 체력의 %).</summary>
        public int HpFloorPercent;

        // 단일 강공격 (액티브 1)
        public int HeartHpCostPercent;
        public int HeartStacks;
        public int HeartDamagePercent;
        public int HeartStabilityDamage;
        public int HeartCooldown;

        // 화염탄 범위 공격 (액티브 2)
        public int OrderHpCostPercent;
        public int OrderStacks;
        public int OrderDamagePercent;
        public int OrderStabilityDamage;
        public int OrderCooldown;
        public int OrderRange;
        public int OrderRadius;
        /// <summary>화상: 맞은 플레이어가 자기 차례를 시작할 때마다 받는 고정 피해와 지속 차례 수.</summary>
        public int BurnDamage;
        public int BurnTurns;

        // 리몰딩 지수 (3-16 시스템을 상점 주인에게 — 3-16b): 플레이어와 같은 규칙으로 얻는다.
        /// <summary>리몰딩 지수 최대.</summary>
        public int RemoldMax;
        /// <summary>행동을 시작할 때마다 얻는 리몰딩.</summary>
        public int RemoldPerAction;
        /// <summary>피해를 1회 줄 때마다 얻는 리몰딩.</summary>
        public int RemoldPerHit;
        /// <summary>패시브: 행동을 끝낼 때마다 얻는 리몰딩.</summary>
        public int RemoldPerActionEnd;

        // 결전기
        /// <summary>결전기에 드는 리몰딩 지수.</summary>
        public int FinalCost;
        public int FinalStacks;
        /// <summary>방벽이 막는 양 (최대 체력의 %)과 지속 차례 수.</summary>
        public int ShieldPercent;
        public int ShieldTurns;

        // 패시브: 액티브 뒤 추가 공격
        /// <summary>밑천 이 개수마다 추가 공격 1번.</summary>
        public int StacksPerExtraAttack;
        /// <summary>추가 공격 피해 = 밑천 수 × 이 값(%) × 최대 체력. 방어력을 무시하고 엄폐 감소만 받는다.</summary>
        public int ExtraDamagePercentPerStack;

        public void Validate()
        {
            if (BasicPercent < 1 || StackMax < 1 || StackDamageTakenPercent < 0 || StackMax * StackDamageTakenPercent > 100)
                throw new ArgumentException("Keeper stack settings are invalid.");
            if (HpFloorPercent < 1 || HpFloorPercent > 100 || HeartHpCostPercent < 0 || OrderHpCostPercent < 0)
                throw new ArgumentException("Keeper hp cost settings are invalid.");
            if (HeartStacks < 0 || HeartDamagePercent < 0 || HeartStabilityDamage < 0 || HeartCooldown < 0)
                throw new ArgumentException("Keeper single skill settings are invalid.");
            if (OrderStacks < 0 || OrderDamagePercent < 0 || OrderStabilityDamage < 0 || OrderCooldown < 0 || OrderRange < 1 || OrderRadius < 0
                || BurnDamage < 0 || BurnTurns < 0)
                throw new ArgumentException("Keeper area skill settings are invalid.");
            if (RemoldMax < 1 || RemoldPerAction < 0 || RemoldPerHit < 0 || RemoldPerActionEnd < 0 || FinalCost > RemoldMax)
                throw new ArgumentException("Keeper remold settings are invalid.");
            if (FinalCost < 1 || FinalStacks < 0 || ShieldPercent < 0 || ShieldTurns < 0)
                throw new ArgumentException("Keeper final skill settings are invalid.");
            if (StacksPerExtraAttack < 1 || ExtraDamagePercentPerStack < 0)
                throw new ArgumentException("Keeper extra attack settings are invalid.");
        }
    }

    /// <summary>상점 주인이 쓴 스킬. 저장에는 들어가지 않는다 (사건 전용).</summary>
    public enum KeeperSkill : byte
    {
        Heart = 0,
        Order = 1,
        Final = 2,
    }

    /// <summary>상점 주인이 스킬을 썼다. 피해는 뒤따르는 UnitAttacked · AreaExploded가 알린다.</summary>
    public sealed class KeeperSkillUsed : GameEvent
    {
        public int UnitId { get; }
        public KeeperSkill Skill { get; }
        public int HpCost { get; }
        public int Stacks { get; }
        public int Shield { get; }

        public KeeperSkillUsed(int unitId, KeeperSkill skill, int hpCost, int stacks, int shield)
        {
            UnitId = unitId;
            Skill = skill;
            HpCost = hpCost;
            Stacks = stacks;
            Shield = shield;
        }
    }

    /// <summary>방벽이 피해를 막았다.</summary>
    public sealed class KeeperShieldAbsorbed : GameEvent
    {
        public int UnitId { get; }
        public int Absorbed { get; }
        public int Left { get; }

        public KeeperShieldAbsorbed(int unitId, int absorbed, int left)
        {
            UnitId = unitId;
            Absorbed = absorbed;
            Left = left;
        }
    }

    /// <summary>화상 피해를 받았다 (플레이어 차례 시작).</summary>
    public sealed class BurnTicked : GameEvent
    {
        public int UnitId { get; }
        public int Damage { get; }
        public int TurnsLeft { get; }

        public BurnTicked(int unitId, int damage, int turnsLeft)
        {
            UnitId = unitId;
            Damage = damage;
            TurnsLeft = turnsLeft;
        }
    }

    /// <summary>
    /// 상점 주인(적대하면 보스 급) 스킬의 한 곳 (BALANCE 33번, 3-15f — 결정 2026-11-14 사용자).
    /// - 밑천(스택, World.KeeperStacks): 1개마다 받는 피해 −StackDamageTakenPercent%p (DamageTakenReduction — SetEffects.DamagePercent가 더한다),
    ///   상점 주인이 행동을 시작할 때 절반(올림)이 줄어든다, 최대 StackMax.
    /// - 일반 공격: 공격력 × BasicPercent (CombatRules.Preview가 BasicPercentFor로 읽는다 — 감시 사격도 같다).
    /// - 단일 강공격: 체력을 쓰고 밑천을 얻은 뒤 쏠 수 있는 플레이어에게 강한 사격 (엄폐 적용, 치명타 없음).
    /// - 화염탄: 체력을 쓰고 밑천을 얻은 뒤 사거리 · 사선 안의 플레이어 칸에 던진다 — 범위 피해(엄폐 무시) + 화상.
    /// - 리몰딩 지수(World.KeeperRemold, 3-16b): 플레이어의 리몰딩(Remold)과 같은 규칙 — 행동 시작마다 RemoldPerAction, 피해를 1회 줄 때마다 RemoldPerHit
    ///   (Remold.OnDamageDealt가 OnDamageDealt를 부른다), 최대 RemoldMax. 패시브로 행동을 끝낼 때마다 RemoldPerActionEnd.
    /// - 결전기: 리몰딩 지수가 FinalCost만큼 있으면 쓰고 밑천을 얻고 방벽(World.KeeperShield)을 두른다.
    /// - 액티브(강공격 · 화염탄) 뒤 밑천 StacksPerExtraAttack개마다 추가 공격 1번: 피해 = 밑천 수 × ExtraDamagePercentPerStack% × 최대 체력.
    /// 적 차례는 EnemyTurn.Run이 StartAction → 이동 → Act 순으로 부른다. 받는 피해는 AfterShield를 거친다.
    /// </summary>
    public static class KeeperSkills
    {
        /// <summary>이 유닛이 상점 주인 스킬을 쓰는가.</summary>
        public static bool Uses(CombatSettings settings, Unit unit) => settings != null && settings.Keeper != null && unit != null && unit.Kind == UnitKind.Shopkeeper;

        /// <summary>일반 공격의 공격력 배율 (%). 상점 주인이 아니면 100.</summary>
        public static int BasicPercentFor(CombatSettings settings, Unit attacker) => Uses(settings, attacker) ? settings.Keeper.BasicPercent : 100;

        /// <summary>밑천 때문에 줄어드는 받는 피해 (%p). 상점 주인이 아니면 0.</summary>
        public static int DamageTakenReduction(World world, CombatSettings settings, Unit target)
            => Uses(settings, target) ? world.KeeperStacks * settings.Keeper.StackDamageTakenPercent : 0;

        /// <summary>방벽이 막고 남은 피해를 돌려준다. 체력을 깎는 곳은 모두 이것을 거친다.</summary>
        public static int AfterShield(World world, Unit target, int damage, List<GameEvent> events)
        {
            if (target == null || target.Kind != UnitKind.Shopkeeper || world.KeeperShield <= 0 || damage <= 0) return damage;
            int absorbed = Math.Min(world.KeeperShield, damage);
            world.KeeperShield -= absorbed;
            if (world.KeeperShield == 0) world.KeeperShieldTurns = 0;
            events?.Add(new KeeperShieldAbsorbed(target.Id, absorbed, world.KeeperShield));
            return damage - absorbed;
        }

        /// <summary>전투가 끝나거나 층이 바뀌면 스킬 상태를 비운다.</summary>
        public static void Reset(World world)
        {
            world.KeeperStacks = 0;
            world.KeeperHeartCooldown = 0;
            world.KeeperOrderCooldown = 0;
            world.KeeperRemold = 0;
            world.KeeperShield = 0;
            world.KeeperShieldTurns = 0;
            world.PlayerBurnTurns = 0;
        }

        private static void GainRemold(World world, KeeperSkillSettings keeperSettings, int amount)
        {
            if (amount > 0) world.KeeperRemold = Math.Min(keeperSettings.RemoldMax, world.KeeperRemold + amount);
        }

        /// <summary>피해를 준 뒤 (Remold.OnDamageDealt가 부른다): 상점 주인이 다른 유닛의 체력을 1 이상 깎았으면 리몰딩을 얻는다.</summary>
        public static void OnDamageDealt(World world, CombatSettings settings, Unit attacker, Unit target, int dealt)
        {
            if (!Uses(settings, attacker) || target == null || target == attacker || dealt <= 0) return;
            GainRemold(world, settings.Keeper, settings.Keeper.RemoldPerHit);
        }

        /// <summary>상점 주인이 행동을 시작한다: 리몰딩 · 밑천 절반(올림) 감소 · 재사용 대기 · 방벽 지속 차례 감소.</summary>
        public static void StartAction(World world, CombatSettings settings, Unit keeper)
        {
            if (!Uses(settings, keeper)) return;
            GainRemold(world, settings.Keeper, settings.Keeper.RemoldPerAction);
            world.KeeperStacks -= (world.KeeperStacks + 1) / 2;
            if (world.KeeperHeartCooldown > 0) world.KeeperHeartCooldown--;
            if (world.KeeperOrderCooldown > 0) world.KeeperOrderCooldown--;
            if (world.KeeperShieldTurns > 0 && --world.KeeperShieldTurns == 0) world.KeeperShield = 0;
        }

        /// <summary>
        /// 상점 주인의 행동 (이동은 끝난 뒤). 결전기 → 화염탄 → 강공격 → 일반 공격 순으로 쓸 수 있는 것을 쓴다.
        /// 아무것도 못 했으면 false (EnemyTurn이 감시를 건다). 행동을 끝내면 패시브로 리몰딩을 얻는다.
        /// </summary>
        public static bool Act(World world, CombatSettings settings, Unit keeper, Unit player, List<GameEvent> events)
        {
            var keeperSettings = settings.Keeper;
            bool acted;
            if (world.KeeperRemold >= keeperSettings.FinalCost)
            {
                UseFinal(world, keeperSettings, keeper, events);
                acted = true;
            }
            else if (world.KeeperOrderCooldown == 0 && CanThrow(world, keeperSettings, keeper, player))
            {
                UseOrder(world, settings, keeper, player, events);
                acted = true;
            }
            else if (world.KeeperHeartCooldown == 0 && CombatRules.Preview(world, settings, keeper, keeper.Position, player).CanAttack)
            {
                UseHeart(world, settings, keeper, player, events);
                acted = true;
            }
            else
            {
                acted = CombatRules.Resolve(world, settings, keeper, player, events);
            }
            GainRemold(world, keeperSettings, keeperSettings.RemoldPerActionEnd);
            return acted;
        }

        /// <summary>플레이어 차례가 시작될 때: 화상 피해. 쓰러지면 패배.</summary>
        public static void OnPlayerTurnStarted(World world, CombatSettings settings, List<GameEvent> events)
        {
            var player = world.Player;
            if (settings == null || settings.Keeper == null || player == null || world.PlayerBurnTurns <= 0) return;
            world.PlayerBurnTurns--;
            int damage = settings.Keeper.BurnDamage;
            if (damage <= 0) return;
            player.Hp -= damage;
            events.Add(new BurnTicked(player.Id, damage, world.PlayerBurnTurns));
            events.Add(new UnitStatsChanged(player.Id));
            if (player.Hp > 0) return;
            player.Hp = 0;
            world.Mode = WorldMode.Defeat;
            var keeper = Shop.Keeper(world);
            events.Add(new PlayerDefeated(keeper != null ? keeper.Id : player.Id));
        }

        /// <summary>화염탄을 던질 수 있는가: 사거리(직선 거리) 안이고 사선이 통한다.</summary>
        public static bool CanThrow(World world, KeeperSkillSettings keeperSettings, Unit keeper, Unit player)
        {
            int dx = player.Position.X - keeper.Position.X;
            int dy = player.Position.Y - keeper.Position.Y;
            return dx * dx + dy * dy <= keeperSettings.OrderRange * keeperSettings.OrderRange
                && Sight.HasLineOfFire(world.Map, keeper.Position, player.Position);
        }

        /// <summary>강공격 피해 (엄폐 감소는 사격과 같은 계산).</summary>
        public static int HeartDamage(World world, CombatSettings settings, Unit keeper, Unit player)
        {
            var preview = CombatRules.Preview(world, settings, keeper, keeper.Position, player);
            return CombatRules.Damage(keeper.Attack, player.Defense, settings.Keeper.HeartDamagePercent, preview.ReductionPercent, settings.MinDamage);
        }

        /// <summary>화염탄 피해 (엄폐 무시).</summary>
        public static int OrderDamage(CombatSettings settings, Unit keeper, Unit player)
            => CombatRules.Damage(keeper.Attack, player.Defense, settings.Keeper.OrderDamagePercent, 0, settings.MinDamage);

        /// <summary>추가 공격 한 번의 피해: 밑천 수 × 비율 × 최대 체력, 엄폐 감소만 받는다.</summary>
        public static int ExtraDamage(CombatSettings settings, Unit keeper, int stacks, int reductionPercent)
        {
            long numerator = (long)stacks * settings.Keeper.ExtraDamagePercentPerStack * keeper.MaxHp * (100 - reductionPercent);
            int value = (int)((2 * numerator + 10000) / 20000);
            return value < settings.MinDamage ? settings.MinDamage : value;
        }

        /// <summary>스킬 값으로 체력을 쓴다 (최소 체력은 남긴다). 쓴 양을 돌려준다.</summary>
        private static int PayHp(KeeperSkillSettings keeperSettings, Unit keeper, int costPercent, List<GameEvent> events)
        {
            int floor = (keeper.MaxHp * keeperSettings.HpFloorPercent + 99) / 100;
            int cost = Math.Max(0, Math.Min(keeper.MaxHp * costPercent / 100, keeper.Hp - floor));
            if (cost == 0) return 0;
            keeper.Hp -= cost;
            events.Add(new UnitStatsChanged(keeper.Id));
            return cost;
        }

        private static void GainStacks(World world, KeeperSkillSettings keeperSettings, int amount)
        {
            world.KeeperStacks = Math.Min(keeperSettings.StackMax, world.KeeperStacks + amount);
        }

        private static void UseFinal(World world, KeeperSkillSettings keeperSettings, Unit keeper, List<GameEvent> events)
        {
            world.KeeperRemold -= keeperSettings.FinalCost;
            GainStacks(world, keeperSettings, keeperSettings.FinalStacks);
            world.KeeperShield = keeper.MaxHp * keeperSettings.ShieldPercent / 100;
            world.KeeperShieldTurns = world.KeeperShield > 0 ? keeperSettings.ShieldTurns : 0;
            events.Add(new KeeperSkillUsed(keeper.Id, KeeperSkill.Final, 0, world.KeeperStacks, world.KeeperShield));
        }

        private static void UseHeart(World world, CombatSettings settings, Unit keeper, Unit player, List<GameEvent> events)
        {
            var keeperSettings = settings.Keeper;
            int cost = PayHp(keeperSettings, keeper, keeperSettings.HeartHpCostPercent, events);
            GainStacks(world, keeperSettings, keeperSettings.HeartStacks);
            world.KeeperHeartCooldown = keeperSettings.HeartCooldown;
            events.Add(new KeeperSkillUsed(keeper.Id, KeeperSkill.Heart, cost, world.KeeperStacks, world.KeeperShield));

            var preview = CombatRules.Preview(world, settings, keeper, keeper.Position, player);
            int damage = HeartDamage(world, settings, keeper, player);
            if (Hit(world, settings, keeper, player, damage, keeperSettings.HeartStabilityDamage, preview.Cover, preview.StabilityGuard, events))
                ExtraAttacks(world, settings, keeper, player, events);
        }

        private static void UseOrder(World world, CombatSettings settings, Unit keeper, Unit player, List<GameEvent> events)
        {
            var keeperSettings = settings.Keeper;
            int cost = PayHp(keeperSettings, keeper, keeperSettings.OrderHpCostPercent, events);
            GainStacks(world, keeperSettings, keeperSettings.OrderStacks);
            world.KeeperOrderCooldown = keeperSettings.OrderCooldown;
            events.Add(new KeeperSkillUsed(keeper.Id, KeeperSkill.Order, cost, world.KeeperStacks, world.KeeperShield));

            var map = world.Map;
            var center = player.Position;
            int radius = keeperSettings.OrderRadius;
            var cells = new List<GridPos>();
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
                for (int x = center.X - radius; x <= center.X + radius; x++)
                    if (map.InBounds(new GridPos(x, y))) cells.Add(new GridPos(x, y));
            events.Add(new AreaExploded(keeper.Id, keeper.Kind, center, cells));

            world.PlayerBurnTurns = keeperSettings.BurnTurns;
            bool alive = Hit(world, settings, keeper, player, OrderDamage(settings, keeper, player), keeperSettings.OrderStabilityDamage, CoverLevel.None, false, events);
            CoverBreak.Hit(world, settings, CoverBreak.InArea(map, cells), events);
            if (alive) ExtraAttacks(world, settings, keeper, player, events);
        }

        /// <summary>액티브 뒤 추가 공격: 밑천 StacksPerExtraAttack개마다 1번. 쏠 수 없으면(사거리 · 사선) 하지 않는다.</summary>
        private static void ExtraAttacks(World world, CombatSettings settings, Unit keeper, Unit player, List<GameEvent> events)
        {
            int count = world.KeeperStacks / settings.Keeper.StacksPerExtraAttack;
            for (int i = 0; i < count; i++)
            {
                var preview = CombatRules.Preview(world, settings, keeper, keeper.Position, player);
                if (!preview.CanAttack) return;
                int damage = ExtraDamage(settings, keeper, world.KeeperStacks, preview.ReductionPercent);
                if (!Hit(world, settings, keeper, player, damage, keeper.StabilityDamage, preview.Cover, preview.StabilityGuard, events)) return;
            }
        }

        /// <summary>플레이어에게 피해를 넣는다. 살아 있으면 true, 쓰러지면 패배 처리 후 false.</summary>
        private static bool Hit(World world, CombatSettings settings, Unit keeper, Unit player, int damage, int stabilityDamage,
            CoverLevel cover, bool guard, List<GameEvent> events)
        {
            int stabilityBefore = player.Stability;
            player.Hp -= damage;
            player.Stability = Math.Max(0, player.Stability - stabilityDamage);
            events.Add(new UnitAttacked(keeper, player, damage, false, cover, guard, stabilityBefore > 0 && player.Stability == 0));
            events.Add(new UnitStatsChanged(player.Id));
            Remold.OnDamageDealt(world, settings, keeper, player, damage);
            if (cover != CoverLevel.None)
                CoverBreak.Hit(world, settings, CoverBreak.Facing(world.Map, player.Position, keeper.Position), events);
            if (player.Hp > 0) return true;
            player.Hp = 0;
            world.Mode = WorldMode.Defeat;
            events.Add(new PlayerDefeated(keeper.Id));
            return false;
        }
    }
}
