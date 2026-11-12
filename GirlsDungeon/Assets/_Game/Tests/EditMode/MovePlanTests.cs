using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-10 이동 예약(홀로그램): 화면은 예약 칸에서 CombatRules.Preview를 계산하고, 확인하면 CombatMoveCommand → AttackCommand를 차례로 낸다.
    /// 미리보기(예약 칸) = 실제(걸은 뒤) 인지 검산한다 (핵심 규칙 6).
    /// </summary>
    public class MovePlanTests
    {
        /// <summary>열린 방: 플레이어 (2,2) 전투 중, 사격병 (8,2), 상자 (7,3).</summary>
        private static (Simulation simulation, World world, Unit enemy, CombatSettings combat) StartCombat()
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
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(8, 2));
            world.Map.SetTile(new GridPos(7, 3), TileType.Crate);
            var simulation = new Simulation(world);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, enemy, combat);
        }

        [Test]
        public void PreviewFromPlannedCell_EqualsPreviewAfterMoving()
        {
            var (simulation, world, enemy, combat) = StartCombat();
            var planned = new GridPos(5, 1);
            var before = CombatRules.Preview(world, combat, world.Player, planned, enemy);

            Assert.That(world.Player.Position, Is.Not.EqualTo(planned), "booking does not move the player");
            Assert.That(simulation.Execute(new CombatMoveCommand(planned)), Is.True);
            var after = CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy);

            Assert.That(after.CanAttack, Is.EqualTo(before.CanAttack));
            Assert.That(after.Cover, Is.EqualTo(before.Cover));
            Assert.That(after.ReductionPercent, Is.EqualTo(before.ReductionPercent));
            Assert.That(after.Damage, Is.EqualTo(before.Damage));
            Assert.That(after.CritDamage, Is.EqualTo(before.CritDamage));
        }

        [Test]
        public void MoveThenAttack_DealsThePreviewedDamage()
        {
            var (simulation, world, enemy, combat) = StartCombat();
            var planned = new GridPos(4, 3);
            var preview = CombatRules.Preview(world, combat, world.Player, planned, enemy);
            Assert.That(preview.CanAttack, Is.True);
            int hp = enemy.Hp;

            Assert.That(simulation.Execute(new CombatMoveCommand(planned, combat)), Is.True);
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.True);

            int dealt = hp - enemy.Hp;
            Assert.That(dealt == preview.Damage || dealt == preview.CritDamage, Is.True, $"dealt {dealt}, preview {preview.Damage} / crit {preview.CritDamage}");
            Assert.That(world.Player.Position, Is.EqualTo(planned));
        }

        [Test]
        public void Sniper_PreviewFromPlannedCell_ShowsItCannotShootAfterMoving()
        {
            var (_, world, enemy, combat) = StartCombat();
            world.PlayerWeapon = WeaponKind.Sniper;
            Loadout.Apply(world, combat);

            Assert.That(CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy).CanAttack, Is.True);
            Assert.That(CombatRules.Preview(world, combat, world.Player, new GridPos(4, 2), enemy).Block, Is.EqualTo(AttackBlock.MovedThisTurn));
        }
    }
}
