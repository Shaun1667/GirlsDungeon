using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-17h 적 약점 (결정 2026-11-20 사용자): 적이 생길 때 연소 · 전도 · 탁류 · 산성 · 빙결 · 물리 중 하나, 상점 주인은 없음.
    /// 약점 1개당 피해 +10% · 안정 피해 +2. 서리밭 안의 적은 빙결 약점 (BALANCE 38번).
    /// </summary>
    public class WeaknessTests
    {
        private static (World world, Unit enemy, CombatSettings combat) Room()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1))
            {
                Map = TestData.Parse(
                    "##########",
                    "#........#",
                    "#........#",
                    "##########"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 1)).Id;
            Loadout.Apply(world, combat);
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(5, 1));
            enemy.MaxHp = enemy.Hp = 100;
            world.Combatants.Add(enemy.Id);
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            return (world, enemy, combat);
        }

        [Test]
        public void SpawnedEnemies_GetOneWeaknessFromTheSix_TheShopkeeperNone()
        {
            var seen = new HashSet<DamageElement>();
            for (int seed = 1; seed <= 20; seed++)
            {
                var world = TestData.NewRun(seed, 6);
                foreach (var unit in world.Units.Where(u => !u.IsPlayer))
                {
                    if (unit.Kind == UnitKind.Shopkeeper)
                    {
                        Assert.That(unit.WeaknessMask, Is.EqualTo(0), "the shopkeeper has none");
                        continue;
                    }
                    var list = Weakness.Of(world, unit);
                    Assert.That(list.Count, Is.EqualTo(1), $"seed {seed} unit {unit.Id}");
                    Assert.That(Weakness.Pool, Does.Contain(list[0]));
                    seen.Add(list[0]);
                }
            }
            Assert.That(seen.Count, Is.GreaterThanOrEqualTo(4), "the roll spreads over the pool");
            Assert.That(Weakness.Pool, Is.EquivalentTo(new[] { DamageElement.Burn, DamageElement.Conduct, DamageElement.Torrent, DamageElement.Acid, DamageElement.Freeze, DamageElement.Physical }));

            // 같은 시드 = 같은 약점.
            var a = TestData.NewRun(7, 6).Units.Select(u => u.WeaknessMask).ToArray();
            var b = TestData.NewRun(7, 6).Units.Select(u => u.WeaknessMask).ToArray();
            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void PhysicalWeakness_ShotDealsTenPercentMore_AndTwoMoreStability()
        {
            var (world, enemy, combat) = Room();
            var player = world.Player;
            var plain = CombatRules.Preview(world, combat, player, player.Position, enemy);

            enemy.WeaknessMask = Weakness.Bit(DamageElement.Physical);
            var weak = CombatRules.Preview(world, combat, player, player.Position, enemy);
            Assert.That(weak.SetPercent, Is.EqualTo(plain.SetPercent + 10));
            Assert.That(weak.StabilityDamage, Is.EqualTo(plain.StabilityDamage + 2));
            Assert.That(weak.Damage, Is.EqualTo(CombatRules.Damage(player.Attack, enemy.Defense, 100, weak.ReductionPercent, combat.MinDamage, 100 - weak.FalloffPercent, plain.SetPercent + 10)));
            Assert.That(SetEffects.IsWeaknessHit(world, enemy, DamageSource.Shot), Is.True);

            enemy.WeaknessMask = Weakness.Bit(DamageElement.Burn);
            var other = CombatRules.Preview(world, combat, player, player.Position, enemy);
            Assert.That(other.SetPercent, Is.EqualTo(plain.SetPercent), "a burn weakness does not care about bullets");
            Assert.That(other.StabilityDamage, Is.EqualTo(plain.StabilityDamage));
        }

        [Test]
        public void TheRealShot_UsesThePreviewWithTheWeakness()
        {
            var (world, enemy, combat) = Room();
            enemy.WeaknessMask = Weakness.Bit(DamageElement.Physical);
            enemy.MaxStability = enemy.Stability = 20;
            var preview = CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy);
            var events = new List<GameEvent>();
            Assert.That(CombatRules.Resolve(world, combat, world.Player, enemy, events), Is.True);
            var hit = events.OfType<UnitAttacked>().Single();
            Assert.That(hit.Damage, Is.EqualTo(hit.IsCrit ? preview.CritDamage : preview.Damage));
            Assert.That(enemy.Stability, Is.EqualTo(20 - preview.StabilityDamage));
        }

        [Test]
        public void FrostField_GivesAFreezeWeakness_ToFrostDamageOnly()
        {
            var (world, enemy, combat) = Room();
            world.PlayerCharacter = CharacterKind.HeroB;
            var player = world.Player;
            var plain = FrostAttack.Preview(world, combat, player, enemy);
            world.FrostFields.Add(new FrostField { Center = enemy.Position, Radius = 3, Turns = 3 });
            var inField = FrostAttack.Preview(world, combat, player, enemy);
            Assert.That(inField.StabilityDamage, Is.EqualTo(plain.StabilityDamage + 2));
            Assert.That(inField.MainDamage, Is.GreaterThanOrEqualTo(plain.MainDamage));
            Assert.That(inField.FixedDamage, Is.EqualTo(plain.FixedDamage), "fixed damage is not changed by weaknesses");
            Assert.That(Weakness.Of(world, enemy), Does.Contain(DamageElement.Freeze));
            // 사격(물리)은 서리밭 빙결 약점을 맞히지 않는다.
            Assert.That(CombatRules.Preview(world, combat, player, player.Position, enemy).StabilityDamage, Is.EqualTo(player.StabilityDamage));

            // 타고난 빙결 약점 + 서리밭 = 하나로 센다.
            enemy.WeaknessMask = Weakness.Bit(DamageElement.Freeze);
            Assert.That(Weakness.Hits(world, combat, player, enemy, DamageSource.Frost), Is.EqualTo(1));
        }

        [Test]
        public void EnemiesShootingThePlayer_IgnoreWeaknesses()
        {
            var (world, enemy, combat) = Room();
            enemy.WeaknessMask = Weakness.Bit(DamageElement.Physical);
            world.Player.WeaknessMask = Weakness.Bit(DamageElement.Physical);
            Assert.That(Weakness.Hits(world, combat, enemy, world.Player, DamageSource.Shot), Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_KeepsWeaknesses()
        {
            var world = TestData.NewRun(3, 6);
            var bytes = WorldSnapshot.Encode(world);
            var copy = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(copy), Is.EqualTo(bytes));
            Assert.That(copy.Units.Select(u => u.WeaknessMask), Is.EqualTo(world.Units.Select(u => u.WeaknessMask)));
        }
    }
}
