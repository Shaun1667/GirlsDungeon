using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-16z 빙결 사격 (대원 B 스킬1, 결정 2026-11-19 사용자): 반경 6칸 안 보이는 적 하나, 공격력 100% 빙결 피해(엄폐 적용)
    /// + 쓰는 사람 방어력 50% 고정 피해, 안정 피해 4, 재사용 대기 (BALANCE 35번).
    /// </summary>
    public class FrostAttackTests
    {
        /// <summary>열린 방: 플레이어 (2,2) 전투 중, 사격병 (6,2), 사격병 앞(플레이어 쪽) 낮은 엄폐 상자 (5,2).</summary>
        private static (Simulation simulation, World world, Unit enemy, CombatSettings combat) StartCombat(CharacterKind character)
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1))
            {
                Map = TestData.Parse(
                    "##############",
                    "#............#",
                    "#............#",
                    "#............#",
                    "##############"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 2)).Id;
            world.PlayerCharacter = character;
            var stats = combat.TryCharacter(character);
            if (stats != null)
            {
                world.Player.MaxHp = world.Player.Hp = stats.MaxHp;
                world.Player.Defense = stats.Defense;
            }
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(6, 2));
            world.Map.SetTile(new GridPos(5, 2), TileType.Crate);
            Loadout.Apply(world, combat);
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, enemy, combat);
        }

        [Test]
        public void FixedDamage_IsHalfTheDefense_Rounded()
        {
            Assert.That(FrostAttack.FixedDamage(3, 50), Is.EqualTo(2), "1.5 -> 2");
            Assert.That(FrostAttack.FixedDamage(4, 50), Is.EqualTo(2));
            Assert.That(FrostAttack.FixedDamage(5, 50), Is.EqualTo(3), "2.5 -> 3");
            Assert.That(FrostAttack.FixedDamage(0, 50), Is.EqualTo(0));
            Assert.That(FrostAttack.FixedDamage(-2, 50), Is.EqualTo(0));
        }

        [Test]
        public void Frost_TakesCover_AddsFixedDamage_AndPreviewEqualsResult()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroB);
            enemy.MaxHp = enemy.Hp = 100;
            var player = world.Player;
            var cover = Cover.Against(world.Map, enemy.Position, player.Position);
            Assert.That(cover, Is.EqualTo(CoverLevel.Half), "the enemy is behind the crate");
            Assert.That(enemy.Stability, Is.GreaterThan(0));

            var preview = FrostAttack.Preview(world, combat, player, enemy);
            Assert.That(preview.CanUse, Is.True);
            Assert.That(preview.Cover, Is.EqualTo(CoverLevel.Half));
            Assert.That(preview.StabilityGuard, Is.True, "cover + stability left = the same guard as a shot");
            int reduction = combat.HalfCoverReductionPercent + combat.StabilityGuardPercent;
            Assert.That(preview.MainDamage, Is.EqualTo(CombatRules.Damage(player.Attack, enemy.Defense, combat.FrostDamagePercent, reduction, combat.MinDamage)));
            Assert.That(preview.FixedDamage, Is.EqualTo(FrostAttack.FixedDamage(player.Defense, combat.FrostFixedDefensePercent)));
            Assert.That(preview.FixedDamage, Is.EqualTo(2), "B defense 3 x 50% = 1.5 -> 2");
            Assert.That(preview.Damage, Is.EqualTo(preview.MainDamage + preview.FixedDamage));
            Assert.That(preview.StabilityDamage, Is.EqualTo(4));

            int hp = enemy.Hp;
            int stability = enemy.Stability;
            Assert.That(simulation.Execute(new FrostAttackCommand(enemy.Id, combat)), Is.True);
            Assert.That(hp - enemy.Hp, Is.EqualTo(preview.Damage));
            Assert.That(enemy.Stability, Is.EqualTo(System.Math.Max(0, stability - 4)));
            Assert.That(world.Map.Get(new GridPos(5, 2)), Is.EqualTo(TileType.Crate), "the skill does not chip the cover");
            // 쓰면 차례가 끝나고 적 차례 뒤 내 차례: 재사용 대기 2 → 1.
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            Assert.That(world.Turn, Is.EqualTo(TurnSide.Player));
            Assert.That(world.FrostCooldown, Is.EqualTo(1));
        }

        [Test]
        public void Frost_SkipsOneTurn_ThenIsReadyAgain()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroB);
            enemy.MaxHp = enemy.Hp = 200;
            Assert.That(simulation.Execute(new FrostAttackCommand(enemy.Id, combat)), Is.True);
            Assert.That(FrostAttack.Preview(world, combat, world.Player, enemy).Block, Is.EqualTo(SonicBlock.NotReady));
            Assert.That(simulation.Execute(new FrostAttackCommand(enemy.Id, combat)), Is.False);
            Assert.That(simulation.Execute(new EndTurnCommand(combat)), Is.True);
            Assert.That(world.FrostCooldown, Is.EqualTo(0));
            // 적이 움직였을 수 있으니 재사용 대기만 본다.
            Assert.That(FrostAttack.Preview(world, combat, world.Player, enemy).Block, Is.Not.EqualTo(SonicBlock.NotReady));
        }

        [Test]
        public void Frost_NeedsRange_AndTheSkill()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroB);
            world.Map.SetTile(new GridPos(5, 2), TileType.Floor);
            enemy.Position = new GridPos(9, 2); // 거리 7 > 반경 6
            Assert.That(FrostAttack.Preview(world, combat, world.Player, enemy).Block, Is.EqualTo(SonicBlock.OutOfRange));
            enemy.Position = new GridPos(8, 2); // 거리 6
            var open = FrostAttack.Preview(world, combat, world.Player, enemy);
            Assert.That(open.CanUse, Is.True);
            Assert.That(open.Cover, Is.EqualTo(CoverLevel.None));
            Assert.That(open.StabilityGuard, Is.False);

            var (otherSimulation, other, otherEnemy, otherCombat) = StartCombat(CharacterKind.HeroA);
            Assert.That(otherCombat.HasSkill(CharacterKind.HeroA, SkillKind.Frost), Is.False);
            Assert.That(otherSimulation.Execute(new FrostAttackCommand(otherEnemy.Id, otherCombat)), Is.False);
            Assert.That(other.PlayerActed, Is.False);
        }

        [Test]
        public void Snapshot_KeepsFrostCooldown()
        {
            var (_, world, _, _) = StartCombat(CharacterKind.HeroB);
            world.FrostCooldown = 2;
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(restored.FrostCooldown, Is.EqualTo(2));
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
        }
    }
}
