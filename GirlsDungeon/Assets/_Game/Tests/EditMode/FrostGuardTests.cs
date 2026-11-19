using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-17b 서리 수호 (대원 B 스킬2, 결정 2026-11-19 사용자): 리몰딩 1, 자신에게 서리 보호막(기초 공격력 130%, 2차례) · 안정 +2 ·
    /// 걸음 수 5 안 적 하나마다 방위 지원 1스택(방어 + 기초 방어 30%, 최대 4, 2차례). 보호막을 깨뜨린 적은 결빙(1행동 이동 불가) (BALANCE 36번).
    /// </summary>
    public class FrostGuardTests
    {
        /// <summary>열린 15 × 9 방: 대원 B (2,4) 전투 중 (체력 90 · 안정 10 · 방어 3), 사격병 (7,4) — 걸음 수 5.</summary>
        private static (Simulation simulation, World world, Unit enemy, CombatSettings combat) StartCombat()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1))
            {
                Map = TestData.Parse(
                    "###############",
                    "#.............#",
                    "#.............#",
                    "#.............#",
                    "#.............#",
                    "#.............#",
                    "#.............#",
                    "#.............#",
                    "###############"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 4)).Id;
            world.PlayerCharacter = CharacterKind.HeroB;
            world.PlayerWeapon = WeaponKind.Smg;
            var player = world.Player;
            player.MaxHp = player.Hp = 90;
            player.MaxStability = player.Stability = 10;
            player.Defense = 3;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(7, 4));
            enemy.MaxHp = enemy.Hp = 100;
            Loadout.Apply(world, combat);
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 4), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, enemy, combat);
        }

        [Test]
        public void Preview_Shield130_StabilityPlus2_OneStackPerEnemyInRange()
        {
            var (_, world, enemy, combat) = StartCombat();
            var player = world.Player;
            player.Stability = 5;
            world.PlayerRemold = 1;

            var preview = FrostGuard.Preview(world, combat, player);
            Assert.That(preview.CanUse, Is.True);
            int baseAttack = combat.WeaponFor(WeaponKind.Smg).Attack;
            Assert.That(preview.Shield, Is.EqualTo(Loadout.Percent(baseAttack, 130)), "130% of the weapon attack (before parts)");
            Assert.That(preview.StabilityRestored, Is.EqualTo(2));
            Assert.That(preview.EnemiesInRange, Is.EqualTo(1), "the rifleman is exactly 5 steps away");
            Assert.That(preview.StacksAfter, Is.EqualTo(1));
            Assert.That(preview.DefenseBonusAfter, Is.EqualTo(1), "3 x 30% = 0.9 -> 1");

            enemy.Position = new GridPos(7, 5); // 걸음 수 6 — 범위 밖
            Assert.That(FrostGuard.Preview(world, combat, player).EnemiesInRange, Is.EqualTo(0));

            player.Stability = 9;
            Assert.That(FrostGuard.Preview(world, combat, player).StabilityRestored, Is.EqualTo(1), "never above the maximum");
        }

        [Test]
        public void NeedsOneRemold()
        {
            var (simulation, world, _, combat) = StartCombat();
            Assert.That(world.PlayerRemold, Is.EqualTo(0));
            Assert.That(FrostGuard.Preview(world, combat, world.Player).CanUse, Is.False);
            Assert.That(simulation.Execute(new FrostGuardCommand(combat)), Is.False);

            world.PlayerRemold = 1;
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);
            Assert.That(simulation.Execute(new FrostGuardCommand(combat)), Is.True);
            Assert.That(events.OfType<FrostGuardUsed>().Single().Stacks, Is.EqualTo(1));
            // 1 − 1, 차례를 마칠 때 특성 +2 (3-17a).
            Assert.That(world.PlayerRemold, Is.EqualTo(2));
        }

        [Test]
        public void Resolve_SetsShield_Stability_AndDefenseSupport_ThenTheyRunOut()
        {
            var (_, world, _, combat) = StartCombat();
            var player = world.Player;
            player.Stability = 5;
            world.PlayerRemold = 1;
            int defense = player.Defense;
            var events = new List<GameEvent>();

            Assert.That(FrostGuard.Resolve(world, combat, player, events), Is.True);
            int shield = Loadout.Percent(combat.WeaponFor(WeaponKind.Smg).Attack, 130);
            Assert.That(world.PlayerShield, Is.EqualTo(shield));
            Assert.That(world.PlayerShieldTurns, Is.EqualTo(2));
            Assert.That(player.Stability, Is.EqualTo(7));
            Assert.That(world.DefenseSupportStacks, Is.EqualTo(1));
            Assert.That(player.Defense, Is.EqualTo(defense + 1));
            Assert.That(world.PlayerRemold, Is.EqualTo(0));

            // 내 차례 시작마다 1씩 — 두 번째에 사라지고 방어가 돌아온다.
            FrostGuard.OnPlayerTurnStarted(world, combat, events);
            Assert.That(world.PlayerShield, Is.EqualTo(shield));
            Assert.That(player.Defense, Is.EqualTo(defense + 1));
            FrostGuard.OnPlayerTurnStarted(world, combat, events);
            Assert.That(world.PlayerShield, Is.EqualTo(0));
            Assert.That(world.DefenseSupportStacks, Is.EqualTo(0));
            Assert.That(player.Defense, Is.EqualTo(defense));
        }

        [Test]
        public void DefenseSupport_StacksUpToFour()
        {
            var (_, world, _, combat) = StartCombat();
            for (int i = 0; i < 4; i++) world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(3 + i, 3)); // 걸음 수 2 ~ 5
            world.PlayerRemold = 6;
            int defense = world.Player.Defense;
            Assert.That(FrostGuard.Resolve(world, combat, world.Player, new List<GameEvent>()), Is.True);
            Assert.That(world.DefenseSupportStacks, Is.EqualTo(4), "five enemies, capped at four");
            Assert.That(world.Player.Defense, Is.EqualTo(defense + 4), "3 x 120% = 3.6 -> 4");

            // 다시 쓰면 스택은 4에서 멈추고 차례가 새로 2.
            world.DefenseSupportTurns = 1;
            Assert.That(FrostGuard.Resolve(world, combat, world.Player, new List<GameEvent>()), Is.True);
            Assert.That(world.DefenseSupportStacks, Is.EqualTo(4));
            Assert.That(world.DefenseSupportTurns, Is.EqualTo(2));
            Assert.That(world.Player.Defense, Is.EqualTo(defense + 4), "applied once, not twice");

            FrostGuard.Clear(world);
            Assert.That(world.Player.Defense, Is.EqualTo(defense));
        }

        [Test]
        public void Shield_AbsorbsFirst_AndFreezesTheEnemyThatBreaksIt()
        {
            var (_, world, enemy, combat) = StartCombat();
            world.PlayerShield = 10;
            world.PlayerShieldTurns = 2;
            world.PlayerShieldFreezeTurns = combat.FreezeTurns;
            var events = new List<GameEvent>();

            Assert.That(KeeperSkills.AfterShield(world, world.Player, 4, events, enemy), Is.EqualTo(0));
            Assert.That(world.PlayerShield, Is.EqualTo(6));
            Assert.That(enemy.FrozenTurns, Is.EqualTo(0), "not broken yet");

            Assert.That(KeeperSkills.AfterShield(world, world.Player, 9, events, enemy), Is.EqualTo(3), "6 blocked, 3 goes through");
            Assert.That(world.PlayerShield, Is.EqualTo(0));
            Assert.That(world.PlayerShieldTurns, Is.EqualTo(0));
            Assert.That(enemy.FrozenTurns, Is.EqualTo(1));
            Assert.That(events.OfType<UnitFrozen>().Single().UnitId, Is.EqualTo(enemy.Id));

            // 결빙: 다음 행동 한 번 이동 0, 그 뒤 원래대로.
            Assert.That(Remold.MoveRangeOf(enemy), Is.EqualTo(0));
            Remold.OnEnemyActed(enemy);
            Assert.That(enemy.FrozenTurns, Is.EqualTo(0));
            Assert.That(Remold.MoveRangeOf(enemy), Is.EqualTo(enemy.MoveRange));
        }

        [Test]
        public void EnemyShot_HitsTheShieldBeforeHp()
        {
            var (_, world, enemy, combat) = StartCombat();
            world.PlayerShield = 1000;
            world.PlayerShieldTurns = 2;
            int hp = world.Player.Hp;
            Assert.That(CombatRules.Resolve(world, combat, enemy, world.Player, new List<GameEvent>()), Is.True);
            Assert.That(world.Player.Hp, Is.EqualTo(hp), "the whole shot went into the shield");
            Assert.That(world.PlayerShield, Is.LessThan(1000));
        }

        [Test]
        public void Snapshot_KeepsShieldSupportAndFreeze()
        {
            var (_, world, enemy, combat) = StartCombat();
            world.PlayerRemold = 1;
            Assert.That(FrostGuard.Resolve(world, combat, world.Player, new List<GameEvent>()), Is.True);
            enemy.FrozenTurns = 1;
            var bytes = WorldSnapshot.Encode(world);
            var copy = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(copy), Is.EqualTo(bytes));
            Assert.That(copy.PlayerShield, Is.EqualTo(world.PlayerShield));
            Assert.That(copy.PlayerShieldFreezeTurns, Is.EqualTo(1));
            Assert.That(copy.DefenseSupportStacks, Is.EqualTo(1));
            Assert.That(copy.DefenseSupportApplied, Is.EqualTo(1));
            Assert.That(copy.FindUnit(enemy.Id).FrozenTurns, Is.EqualTo(1));
        }
    }
}
