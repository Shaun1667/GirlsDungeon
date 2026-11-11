using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// BALANCE 12-11 ~ 12-13 (결정 2026-11-11, 사용자 — 3-3): 기관단총 = 한 차례에 두 번 쏨 (사이에 이동 가능),
    /// 기관총 = 이동 −2, 도검 = 주변 8칸 근접 · 엄폐 무시.
    /// </summary>
    public class MoreWeaponsTests
    {
        private static World MakeCombat(WeaponKind weapon, GridPos player, GridPos enemyCell, params string[] rows)
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse(rows) };
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), player).Id;
            world.Explored = Enumerable.Repeat(true, world.Map.Width * world.Map.Height).ToArray();
            world.PlayerWeapon = weapon;
            Loadout.Apply(world, combat);

            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), enemyCell);
            enemy.MaxHp = enemy.Hp = 60; // 몇 발 맞아도 쓰러지지 않게 — 차례 넘김만 본다.
            world.Combatants.Add(enemy.Id);
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            world.Round = 1;
            return world;
        }

        private static Unit Enemy(World world) => world.Units.Single(u => !u.IsPlayer);

        // ---------- 기관단총 ----------

        [Test]
        public void Smg_ShootsTwice_ThenTheTurnEnds()
        {
            var combat = TestData.Combat();
            var world = MakeCombat(WeaponKind.Smg, new GridPos(0, 0), new GridPos(4, 0), "..........");
            var simulation = new Simulation(world, combat);
            int id = Enemy(world).Id;

            Assert.That(simulation.Execute(new AttackCommand(id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(1), "one shot left");
            Assert.That(world.PlayerShots, Is.EqualTo(1));
            Assert.That(Loadout.CanShoot(world, combat), Is.True);

            Assert.That(simulation.Execute(new AttackCommand(id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(2), "the second shot ends the turn");
            Assert.That(world.PlayerShots, Is.EqualTo(0), "reset for the new turn");
        }

        [Test]
        public void Smg_CanMoveBetweenTheShots()
        {
            var combat = TestData.Combat();
            var world = MakeCombat(WeaponKind.Smg, new GridPos(0, 0), new GridPos(4, 0), "..........");
            var simulation = new Simulation(world, combat);
            int id = Enemy(world).Id;

            Assert.That(simulation.Execute(new AttackCommand(id, combat)), Is.True);
            Assert.That(Loadout.CanCombatMove(world, combat), Is.True);
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(1, 0), combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(1), "moving does not end the turn while a shot is left");
            Assert.That(simulation.Execute(new AttackCommand(id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(2));
        }

        [Test]
        public void Smg_MoveThenTwoShots()
        {
            var combat = TestData.Combat();
            var world = MakeCombat(WeaponKind.Smg, new GridPos(0, 0), new GridPos(5, 0), "..........");
            var simulation = new Simulation(world, combat);
            int id = Enemy(world).Id;

            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(1, 0), combat)), Is.True);
            Assert.That(simulation.Execute(new AttackCommand(id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(1));
            Assert.That(Loadout.CanCombatMove(world, combat), Is.False, "only one move per turn");
            Assert.That(simulation.Execute(new AttackCommand(id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(2));
        }

        [Test]
        public void Smg_AfterTheFirstShot_SkillsAndOverwatchAreClosed()
        {
            var combat = TestData.Combat();
            var world = MakeCombat(WeaponKind.Smg, new GridPos(0, 0), new GridPos(4, 0), "..........");
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new AttackCommand(Enemy(world).Id, combat)), Is.True);
            Assert.That(simulation.Execute(new OverwatchCommand(combat)), Is.False);
            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(4, 0), combat)), Is.False);
        }

        [Test]
        public void OtherWeapons_ShootOncePerTurn()
        {
            var combat = TestData.Combat();
            var world = MakeCombat(WeaponKind.AssaultRifle, new GridPos(0, 0), new GridPos(4, 0), "..........");
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new AttackCommand(Enemy(world).Id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(2));
        }

        // ---------- 기관총 ----------

        [Test]
        public void MachineGun_MovesTwoCellsLess_AndSwappingBackRestoresIt()
        {
            var combat = TestData.Combat();
            var world = MakeCombat(WeaponKind.MachineGun, new GridPos(0, 0), new GridPos(9, 0), "..........");
            Assert.That(world.Player.MoveRange, Is.EqualTo(4));
            Assert.That(world.Player.Attack, Is.EqualTo(9));
            Assert.That(world.Player.StabilityDamage, Is.EqualTo(4));

            world.PlayerWeapon = WeaponKind.AssaultRifle;
            Loadout.Apply(world, combat);
            Assert.That(world.Player.MoveRange, Is.EqualTo(6));
        }

        [Test]
        public void MachineGun_CannotMoveFive()
        {
            var combat = TestData.Combat();
            var world = MakeCombat(WeaponKind.MachineGun, new GridPos(0, 0), new GridPos(9, 1), "..........", "..........");
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(5, 0), combat)), Is.False);
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(4, 0), combat)), Is.True);
        }

        // ---------- 도검 ----------

        [Test]
        public void Blade_ReachesAllEightNeighbors_AndNothingFarther()
        {
            var combat = TestData.Combat();
            foreach (var (cell, reaches) in new[]
            {
                (new GridPos(3, 2), true), (new GridPos(3, 3), true), (new GridPos(2, 3), true), (new GridPos(1, 1), true),
                (new GridPos(4, 2), false), (new GridPos(4, 4), false), (new GridPos(2, 0), false),
            })
            {
                var world = MakeCombat(WeaponKind.Blade, new GridPos(2, 2), cell, ".....", ".....", ".....", ".....", ".....");
                var preview = CombatRules.Preview(world, combat, world.Player, world.Player.Position, Enemy(world));
                Assert.That(preview.CanAttack, Is.EqualTo(reaches), $"{cell}");
                if (!reaches) Assert.That(preview.Block, Is.EqualTo(AttackBlock.OutOfRange), $"{cell}");
            }
        }

        [Test]
        public void Blade_IgnoresCover()
        {
            var combat = TestData.Combat();
            // 사격병 (1,1) 아래 (1,0)에 상자 — (0,0)에서 보면 낮은 엄폐.
            var rows = new[] { ".C.", "..." };
            var rifle = MakeCombat(WeaponKind.AssaultRifle, new GridPos(0, 0), new GridPos(1, 1), rows);
            var withRifle = CombatRules.Preview(rifle, combat, rifle.Player, rifle.Player.Position, Enemy(rifle));
            Assert.That(withRifle.Cover, Is.EqualTo(CoverLevel.Half), "the setup really has cover");

            var blade = MakeCombat(WeaponKind.Blade, new GridPos(0, 0), new GridPos(1, 1), rows);
            var preview = CombatRules.Preview(blade, combat, blade.Player, blade.Player.Position, Enemy(blade));
            Assert.That(preview.CanAttack, Is.True);
            Assert.That(preview.Cover, Is.EqualTo(CoverLevel.None));
            Assert.That(preview.ReductionPercent, Is.EqualTo(0));
            Assert.That(preview.BreaksCover, Is.False);
            Assert.That(preview.Damage, Is.EqualTo(14), "16 x 16 / 18 = 14.2");
            Assert.That(preview.CritDamage, Is.EqualTo(21), "14.2 x 1.5 = 21.3");
        }

        [Test]
        public void Blade_HitDoesNotDamageTheCrate()
        {
            var combat = TestData.Combat();
            var world = MakeCombat(WeaponKind.Blade, new GridPos(0, 0), new GridPos(1, 1), ".C.", "...");
            // 공격만 본다 — AttackCommand는 차례를 넘기고, 적 차례의 사격이 플레이어 옆 같은 상자를 깎을 수 있다.
            var events = new System.Collections.Generic.List<GameEvent>();
            Assert.That(CombatRules.Resolve(world, combat, world.Player, Enemy(world), events), Is.True);
            Assert.That(world.Map.CrateDamage(new GridPos(1, 0)), Is.EqualTo(0));
            Assert.That(events.OfType<CoverDestroyed>(), Is.Empty);
        }

        // ---------- 저장 ----------

        [Test]
        public void PlayerShots_SurviveTheSnapshot()
        {
            var world = TestData.NewRun(61);
            world.PlayerShots = 1;
            Assert.That(WorldSnapshot.Decode(WorldSnapshot.Encode(world)).PlayerShots, Is.EqualTo(1));
        }
    }
}
