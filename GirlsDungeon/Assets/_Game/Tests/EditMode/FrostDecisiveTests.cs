using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-17c 서리 결전 (대원 B 결전기, 결정 2026-11-19 사용자) · 눈사태 · 응고 · 결빙 · 서리밭 · 빙설 보루 (BALANCE 37번).
    /// 범위: 쓰는 자리에서 걸음 수 6 안 빈 칸, 눈꽃 = 그 칸에서 걸음 수 3 안 마름모 (사용자 그림).
    /// </summary>
    public class FrostDecisiveTests
    {
        /// <summary>열린 19 × 11 방: 대원 B (2,5) 전투 중 (방어 3), 사격병 (7,5) — 걸음 수 5.</summary>
        private static (Simulation simulation, World world, Unit enemy, CombatSettings combat) StartCombat()
        {
            var rows = new List<string> { "###################" };
            for (int i = 0; i < 9; i++) rows.Add("#.................#");
            rows.Add("###################");
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(rows.ToArray()) };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 5)).Id;
            world.PlayerCharacter = CharacterKind.HeroB;
            world.PlayerWeapon = WeaponKind.Smg;
            var player = world.Player;
            player.MaxHp = player.Hp = 90;
            player.MaxStability = player.Stability = 10;
            player.Defense = 3;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(7, 5));
            enemy.MaxHp = enemy.Hp = 100;
            Loadout.Apply(world, combat);
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 5), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, enemy, combat);
        }

        [Test]
        public void Preview_PicksAnEmptyCellInRange_SnowflakeIsADiamondOfThree()
        {
            var (_, world, enemy, combat) = StartCombat();
            world.PlayerRemold = 5;
            var player = world.Player;

            var preview = FrostDecisive.Preview(world, combat, player, new GridPos(6, 5));
            Assert.That(preview.CanUse, Is.True);
            Assert.That(preview.Cells.Count, Is.EqualTo(25), "diamond of radius 3");
            Assert.That(preview.Targets.Select(u => u.Id), Is.EqualTo(new[] { enemy.Id }));
            Assert.That(preview.FixedDamage, Is.EqualTo(2), "defense 3 x 50% = 1.5 -> 2");
            Assert.That(preview.AvalancheStacks, Is.EqualTo(2));
            Assert.That(preview.Shield, Is.EqualTo(Loadout.Percent(combat.WeaponFor(WeaponKind.Smg).Attack, 100)));

            Assert.That(FrostDecisive.Preview(world, combat, player, new GridPos(9, 5)).Block, Is.EqualTo(FrostDecisiveBlock.OutOfRange), "7 steps");
            Assert.That(FrostDecisive.Preview(world, combat, player, new GridPos(8, 5)).CanUse, Is.True, "6 steps");
            Assert.That(FrostDecisive.Preview(world, combat, player, enemy.Position).Block, Is.EqualTo(FrostDecisiveBlock.NotEmpty));
            Assert.That(FrostDecisive.Preview(world, combat, player, new GridPos(0, 5)).Block, Is.EqualTo(FrostDecisiveBlock.NotEmpty), "a wall");
            Assert.That(FrostDecisive.Preview(world, combat, player, new GridPos(6, 6)).Targets.Count, Is.EqualTo(1));
            Assert.That(FrostDecisive.Preview(world, combat, player, new GridPos(5, 7)).Targets, Is.Empty, "the enemy is 4 steps from there");

            world.PlayerRemold = 4;
            Assert.That(FrostDecisive.Preview(world, combat, player, new GridPos(6, 5)).Block, Is.EqualTo(FrostDecisiveBlock.NotReady), "costs 5");
        }

        [Test]
        public void Resolve_HitsAvalanches_BuffsSelf_AndLeavesAFrostField()
        {
            var (_, world, enemy, combat) = StartCombat();
            world.PlayerRemold = 6;
            var player = world.Player;
            player.Stability = 5;
            world.PlayerBurnTurns = 2;
            var events = new List<GameEvent>();

            Assert.That(FrostDecisive.Resolve(world, combat, player, new GridPos(6, 5), events), Is.True);
            Assert.That(enemy.Hp, Is.EqualTo(98));
            Assert.That(enemy.AvalancheStacks, Is.EqualTo(2));
            Assert.That(world.PlayerRemold, Is.EqualTo(1));
            Assert.That(player.Stability, Is.EqualTo(7));
            Assert.That(world.PlayerBurnTurns, Is.EqualTo(0), "one debuff cleansed");
            Assert.That(world.PlayerShield, Is.EqualTo(Loadout.Percent(combat.WeaponFor(WeaponKind.Smg).Attack, 100)));
            Assert.That(world.PlayerShieldTurns, Is.EqualTo(3));
            Assert.That(world.BulwarkStacks, Is.EqualTo(1));
            Assert.That(world.BulwarkTurns, Is.EqualTo(3));
            Assert.That(world.FrostFields.Single().Center, Is.EqualTo(new GridPos(6, 5)));
            Assert.That(world.FrostFields.Single().Turns, Is.EqualTo(3));
            Assert.That(events.OfType<FrostFieldChanged>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Avalanche_BurstsAtFive_ForEightFixedStability_FromOtherFrostDebuffs()
        {
            var (_, world, enemy, combat) = StartCombat();
            var events = new List<GameEvent>();
            enemy.MaxStability = enemy.Stability = 20;

            FrostStatus.Coagulate(world, combat, enemy, events);
            Assert.That(enemy.AvalancheStacks, Is.EqualTo(0), "no avalanche yet, nothing to add to");
            FrostStatus.AddAvalanche(world, combat, enemy, 2, events);
            FrostStatus.Coagulate(world, combat, enemy, events);
            Assert.That(enemy.AvalancheStacks, Is.EqualTo(3), "a frost debuff adds one");
            FrostStatus.Freeze(world, combat, enemy, 1, events);
            Assert.That(enemy.AvalancheStacks, Is.EqualTo(4));
            FrostStatus.AddAvalanche(world, combat, enemy, 1, events);
            Assert.That(enemy.AvalancheStacks, Is.EqualTo(0), "burst at five");
            Assert.That(enemy.Stability, Is.EqualTo(12));
            Assert.That(events.OfType<AvalancheBurst>().Single().StabilityDamage, Is.EqualTo(8));
        }

        [Test]
        public void Coagulation_SlowsOnePerStack_AndTwoStacksTurnIntoFreeze()
        {
            var (_, world, enemy, combat) = StartCombat();
            var events = new List<GameEvent>();
            world.FrostFields.Add(new FrostField { Center = enemy.Position, Radius = 3, Turns = 3 });

            FrostStatus.OnEnemyActionEnded(world, combat, enemy, events);
            Assert.That(enemy.CoagStacks, Is.EqualTo(1));
            Assert.That(Remold.MoveRangeOf(enemy), Is.EqualTo(enemy.MoveRange - 1));
            Assert.That(enemy.FrozenTurns, Is.EqualTo(0));

            // 다음 행동: 시작에 응고 남은 행동 −1, 끝에 서리밭에서 또 응고 → 2스택 = 결빙.
            Remold.OnEnemyActed(enemy);
            FrostStatus.OnEnemyActionEnded(world, combat, enemy, events);
            Assert.That(enemy.CoagStacks, Is.EqualTo(0));
            Assert.That(enemy.FrozenTurns, Is.EqualTo(1));
            Assert.That(Remold.MoveRangeOf(enemy), Is.EqualTo(0));

            // 서리밭 밖: 응고가 생기지 않고, 남은 응고는 두 행동 뒤 사라진다.
            world.FrostFields.Clear();
            FrostStatus.Coagulate(world, combat, enemy, events);
            Remold.OnEnemyActed(enemy);
            Assert.That(enemy.CoagStacks, Is.EqualTo(1));
            Remold.OnEnemyActed(enemy);
            Assert.That(enemy.CoagStacks, Is.EqualTo(0));
        }

        [Test]
        public void FrostField_AndBulwark_RunOutOnPlayerTurns()
        {
            var (_, world, _, combat) = StartCombat();
            world.FrostFields.Add(new FrostField { Center = new GridPos(6, 5), Radius = 3, Turns = 3 });
            world.BulwarkStacks = 1;
            world.BulwarkTurns = 3;
            var events = new List<GameEvent>();
            for (int i = 0; i < 2; i++) FrostStatus.OnPlayerTurnStarted(world, events);
            Assert.That(world.FrostFields.Count, Is.EqualTo(1));
            Assert.That(world.BulwarkStacks, Is.EqualTo(1));
            FrostStatus.OnPlayerTurnStarted(world, events);
            Assert.That(world.FrostFields, Is.Empty);
            Assert.That(world.BulwarkStacks, Is.EqualTo(0));
            Assert.That(events.OfType<FrostFieldChanged>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Bulwark_CutsDamageTaken30Percent_AndLosesAStackPerAttack()
        {
            var (_, world, enemy, combat) = StartCombat();
            var player = world.Player;
            int plain = CombatRules.Preview(world, combat, enemy, enemy.Position, player).Damage;
            world.BulwarkStacks = 2;
            world.BulwarkTurns = 3;
            Assert.That(SetEffects.DamagePercent(world, combat, enemy, player, DamageSource.Shot), Is.EqualTo(-30));
            int reduced = CombatRules.Preview(world, combat, enemy, enemy.Position, player).Damage;
            Assert.That(reduced, Is.LessThanOrEqualTo(plain));

            int hp = player.Hp;
            Assert.That(CombatRules.Resolve(world, combat, enemy, player, new List<GameEvent>()), Is.True);
            Assert.That(world.BulwarkStacks, Is.EqualTo(1));
            Assert.That(hp - player.Hp, Is.EqualTo(reduced).Or.EqualTo(CombatRules.Preview(world, combat, enemy, enemy.Position, player).CritDamage), "the reduced preview is what lands (or its crit)");
            Assert.That(CombatRules.Resolve(world, combat, enemy, player, new List<GameEvent>()), Is.True);
            Assert.That(world.BulwarkStacks, Is.EqualTo(0));
            Assert.That(world.BulwarkTurns, Is.EqualTo(0));
        }

        [Test]
        public void EnemyEndingItsTurnInTheField_IsCoagulated_ThroughTheRealEnemyTurn()
        {
            var (simulation, world, enemy, combat) = StartCombat();
            world.PlayerRemold = 5;
            // 적 칸 바로 옆(빈 칸)에 쓴다 — 적은 사거리 안이라 움직이지 않고 쏜다.
            Assert.That(simulation.Execute(new FrostDecisiveCommand(new GridPos(7, 4), combat)), Is.True);
            // 기관단총은 스킬 뒤에도 한 발이 남는다 (3-17f) — 차례를 끝내 적이 행동하게 한다.
            Assert.That(world.PlayerShots, Is.EqualTo(1));
            Assert.That(simulation.Execute(new EndTurnCommand(combat)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            var after = world.FindUnit(enemy.Id);
            Assert.That(after, Is.Not.Null);
            Assert.That(after.AvalancheStacks, Is.GreaterThanOrEqualTo(2), "the decisive gives two");
            if (world.FrostFields.Single().Contains(after.Position))
            {
                Assert.That(after.CoagStacks + after.FrozenTurns, Is.GreaterThan(0), "ended its action inside the field");
                // 결전기 2스택 + 응고(빙결계 디버프) 1 이상.
                Assert.That(after.AvalancheStacks, Is.GreaterThanOrEqualTo(3));
            }
        }

        [Test]
        public void Snapshot_KeepsFieldsBulwarkAndUnitStatuses()
        {
            var (_, world, enemy, combat) = StartCombat();
            world.PlayerRemold = 5;
            Assert.That(FrostDecisive.Resolve(world, combat, world.Player, new GridPos(6, 5), new List<GameEvent>()), Is.True);
            FrostStatus.Coagulate(world, combat, enemy, new List<GameEvent>());
            var bytes = WorldSnapshot.Encode(world);
            var copy = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(copy), Is.EqualTo(bytes));
            Assert.That(copy.FrostFields.Single().Center, Is.EqualTo(new GridPos(6, 5)));
            Assert.That(copy.BulwarkStacks, Is.EqualTo(1));
            var copied = copy.FindUnit(enemy.Id);
            Assert.That(copied.AvalancheStacks, Is.EqualTo(3));
            Assert.That(copied.CoagStacks, Is.EqualTo(1));
            Assert.That(copied.CoagMove, Is.EqualTo(1));

            FrostGuard.Clear(world);
            Assert.That(world.FrostFields, Is.Empty);
            Assert.That(enemy.AvalancheStacks, Is.EqualTo(0));
            Assert.That(enemy.CoagStacks, Is.EqualTo(0));
        }
    }
}
