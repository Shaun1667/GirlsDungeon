using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-12 캐릭터별 스킬 칸 · 음파 공격 (결정 2026-11-12 사용자: 캐릭터 A = 2 넉백 공격(밀치기) · 3 음파 · 4 수류탄, 감시는 모두 5번, B · C는 그대로).
    /// </summary>
    public class SkillLoadoutTests
    {
        /// <summary>열린 방: 플레이어 (2,2) 전투 중, 사격병 (6,2), 플레이어 쪽 낮은 엄폐 상자 (5,2) — 사격병은 낮은 엄폐를 받는다.</summary>
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
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(6, 2));
            world.Map.SetTile(new GridPos(5, 2), TileType.Crate);
            Loadout.Apply(world, combat);
            var simulation = new Simulation(world);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, enemy, combat);
        }

        [Test]
        public void HeroA_SlotsAreKnockbackSonicGrenade_OthersKeepTheOldBar()
        {
            var combat = TestData.Combat();
            Assert.That(combat.SkillSlots(CharacterKind.HeroA), Is.EqualTo(new[] { SkillKind.Push, SkillKind.Sonic, SkillKind.Grenade }));
            Assert.That(combat.SkillSlots(CharacterKind.HeroB), Is.EqualTo(new[] { SkillKind.Grenade, SkillKind.Push, SkillKind.None }));
            Assert.That(combat.SkillSlots(CharacterKind.HeroC), Is.EqualTo(new[] { SkillKind.Grenade, SkillKind.Push, SkillKind.None }));
            Assert.That(combat.SkillSlots(CharacterKind.None), Is.EqualTo(new[] { SkillKind.Grenade, SkillKind.Push, SkillKind.None }));
            Assert.That(combat.HasSkill(CharacterKind.HeroA, SkillKind.Sonic), Is.True);
            Assert.That(combat.HasSkill(CharacterKind.HeroB, SkillKind.Sonic), Is.False);
        }

        [Test]
        public void Sonic_IgnoresCover_AndPreviewEqualsResult()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            Assert.That(Cover.Against(world.Map, enemy.Position, world.Player.Position), Is.Not.EqualTo(CoverLevel.None), "the enemy is in cover");

            var preview = SonicAttack.Preview(world, combat, world.Player, enemy);
            Assert.That(preview.CanUse, Is.True);
            Assert.That(preview.Damage, Is.EqualTo(CombatRules.Damage(world.Player.Attack, enemy.Defense, combat.SonicDamagePercent, 0, combat.MinDamage)));
            Assert.That(preview.StabilityDamage, Is.EqualTo(combat.SonicStabilityDamage));

            int hp = enemy.Hp;
            int stability = enemy.Stability;
            Assert.That(simulation.Execute(new SonicAttackCommand(enemy.Id, combat)), Is.True);
            if (world.FindUnit(enemy.Id) != null)
            {
                Assert.That(hp - enemy.Hp, Is.EqualTo(preview.Damage));
                Assert.That(enemy.Stability, Is.EqualTo(System.Math.Max(0, stability - preview.StabilityDamage)));
            }
            // 쓰면 차례가 끝나고 적 차례 뒤 내 차례가 오며 한 라운드 줄어든다 (적은 한 방에 쓰러지지 않는다: 10 × 10 ÷ 12 × 60% = 5).
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            Assert.That(world.SonicCooldown, Is.EqualTo(combat.SonicCooldownRounds - 1));
        }

        [Test]
        public void Sonic_NotOwned_IsRejected()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroB);
            Assert.That(simulation.Execute(new SonicAttackCommand(enemy.Id, combat)), Is.False);
            Assert.That(world.PlayerActed, Is.False);
        }

        [Test]
        public void Sonic_OutOfRange_IsBlocked()
        {
            var (_, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            combat.SonicRange = 3;
            Assert.That(SonicAttack.Preview(world, combat, world.Player, enemy).Block, Is.EqualTo(SonicBlock.OutOfRange));
        }

        [Test]
        public void Sonic_OnCooldown_IsBlocked()
        {
            var (_, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            world.SonicCooldown = 1;
            Assert.That(SonicAttack.Preview(world, combat, world.Player, enemy).Block, Is.EqualTo(SonicBlock.NotReady));
        }

        [Test]
        public void HeroA_StillThrowsTheGrenadeFromSlot4()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            Assert.That(simulation.Execute(new AreaAttackCommand(enemy.Position, combat)), Is.True);
        }

        [Test]
        public void SkillMissingFromTheSlots_IsRejectedByTheRules()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroB);
            combat.TryCharacter(CharacterKind.HeroB).Skills.Remove(SkillKind.Grenade);
            Assert.That(simulation.Execute(new AreaAttackCommand(enemy.Position, combat)), Is.False);
            Assert.That(world.PlayerActed, Is.False);
        }

        [Test]
        public void Snapshot_KeepsSonicCooldown()
        {
            var (_, world, _, _) = StartCombat(CharacterKind.HeroA);
            world.SonicCooldown = 2;
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(restored.SonicCooldown, Is.EqualTo(2));
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
        }
    }
}
