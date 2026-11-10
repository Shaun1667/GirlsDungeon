using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>BALANCE 5번 피해 공식 검산 (첫 수치: 플레이어 공격 10 · 방어 4 / 사격병 공격 6 · 방어 2).</summary>
    public class DamageFormulaTests
    {
        [Test]
        public void PlayerOnRifleman_NoCover_Is8()
        {
            Assert.That(CombatRules.Damage(10, 2, 100, 0, 1), Is.EqualTo(8));
        }

        [Test]
        public void PlayerOnRifleman_LowCoverWithStability_Is1_AfterBreak_Is6()
        {
            Assert.That(CombatRules.Damage(10, 2, 100, 25 + 60, 1), Is.EqualTo(1));
            Assert.That(CombatRules.Damage(10, 2, 100, 25, 1), Is.EqualTo(6));
        }

        [Test]
        public void RiflemanOnPlayer_NoCover_Is4_Crit_Is5()
        {
            Assert.That(CombatRules.Damage(6, 4, 100, 0, 1), Is.EqualTo(4));
            Assert.That(CombatRules.Damage(6, 4, 150, 0, 1), Is.EqualTo(5));
        }

        [Test]
        public void HighCoverWithStability_StillDealsTheMinimum()
        {
            Assert.That(CombatRules.Damage(6, 4, 100, 35 + 60, 1), Is.EqualTo(1));
        }
    }

    public class CombatRulesTests
    {
        private static World Room(params string[] rows)
        {
            return new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(rows) };
        }

        private static Unit Add(World world, UnitKind kind, int x, int y)
        {
            var unit = world.AddUnit(TestData.Combat().StatsFor(kind), new GridPos(x, y));
            if (kind == UnitKind.Player) world.PlayerId = unit.Id;
            return unit;
        }

        [Test]
        public void Preview_UsesCoverFacingTheAttacker_AndStability()
        {
            var world = Room("..........");
            var player = Add(world, UnitKind.Player, 0, 0);
            var enemy = Add(world, UnitKind.Rifleman, 6, 0);
            world.Map.SetTile(new GridPos(5, 0), TileType.Crate);

            var guarded = CombatRules.Preview(world, TestData.Combat(), player, player.Position, enemy);
            Assert.That(guarded.CanAttack, Is.True);
            Assert.That(guarded.Cover, Is.EqualTo(CoverLevel.Half));
            Assert.That(guarded.StabilityGuard, Is.True);
            Assert.That(guarded.ReductionPercent, Is.EqualTo(85));
            Assert.That(guarded.Damage, Is.EqualTo(1));

            enemy.Stability = 0;
            var broken = CombatRules.Preview(world, TestData.Combat(), player, player.Position, enemy);
            Assert.That(broken.ReductionPercent, Is.EqualTo(25));
            Assert.That(broken.Damage, Is.EqualTo(6));
        }

        [Test]
        public void Preview_BlocksOutOfRangeAndBlockedLines()
        {
            var world = Room("..........", "..........");
            var player = Add(world, UnitKind.Player, 0, 0);
            var far = Add(world, UnitKind.Rifleman, 9, 0);
            Assert.That(CombatRules.Preview(world, TestData.Combat(), player, player.Position, far).Block, Is.EqualTo(AttackBlock.OutOfRange));

            var wallWorld = Room("..#...");
            var shooter = Add(wallWorld, UnitKind.Player, 0, 0);
            var hidden = Add(wallWorld, UnitKind.Rifleman, 4, 0);
            Assert.That(CombatRules.Preview(wallWorld, TestData.Combat(), shooter, shooter.Position, hidden).Block, Is.EqualTo(AttackBlock.NoLineOfFire));
        }

        [Test]
        public void Resolve_DealsPreviewDamage_AndTakesStability()
        {
            var world = Room("..........");
            var player = Add(world, UnitKind.Player, 0, 0);
            var enemy = Add(world, UnitKind.Rifleman, 5, 0);
            var settings = TestData.Combat();
            settings.StatsFor(UnitKind.Player).CritPercent = 0;
            player.CritPercent = 0;
            var events = new List<GameEvent>();

            Assert.That(CombatRules.Resolve(world, settings, player, enemy, events), Is.True);

            Assert.That(enemy.Hp, Is.EqualTo(10 - 8));
            Assert.That(enemy.Stability, Is.EqualTo(3 - 2));
            var attacked = events.OfType<UnitAttacked>().Single();
            Assert.That(attacked.Damage, Is.EqualTo(8));
            Assert.That(attacked.IsCrit, Is.False);
        }

        [Test]
        public void Resolve_AlwaysCrit_UsesCritDamage()
        {
            var world = Room("..........");
            var player = Add(world, UnitKind.Player, 0, 0);
            var enemy = Add(world, UnitKind.Rifleman, 5, 0);
            player.CritPercent = 100;
            var events = new List<GameEvent>();

            CombatRules.Resolve(world, TestData.Combat(), player, enemy, events);
            Assert.That(events.OfType<UnitAttacked>().Single().IsCrit, Is.True);
            Assert.That(events.OfType<UnitDied>().Count(), Is.EqualTo(1), "crit 13 > 10 hp");
        }

        [Test]
        public void StabilityBreak_IsReported_WhenItReachesZero()
        {
            var world = Room("..........");
            var player = Add(world, UnitKind.Player, 0, 0);
            var enemy = Add(world, UnitKind.Rifleman, 5, 0);
            player.CritPercent = 0;
            enemy.Hp = 100;
            enemy.MaxHp = 100;
            var events = new List<GameEvent>();

            CombatRules.Resolve(world, TestData.Combat(), player, enemy, events);
            CombatRules.Resolve(world, TestData.Combat(), player, enemy, events);
            var hits = events.OfType<UnitAttacked>().ToList();
            Assert.That(hits[0].StabilityBroken, Is.False);
            Assert.That(hits[1].StabilityBroken, Is.True);
            Assert.That(enemy.Stability, Is.EqualTo(0));
        }

        [Test]
        public void PlayerAtZeroHp_IsDefeated_NotRemoved()
        {
            var world = Room("..........");
            var player = Add(world, UnitKind.Player, 0, 0);
            var enemy = Add(world, UnitKind.Rifleman, 5, 0);
            world.Mode = WorldMode.Combat;
            player.Hp = 1;
            var events = new List<GameEvent>();

            CombatRules.Resolve(world, TestData.Combat(), enemy, player, events);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Defeat));
            Assert.That(world.Player, Is.Not.Null);
            Assert.That(player.Hp, Is.EqualTo(0));
            Assert.That(events.OfType<PlayerDefeated>().Count(), Is.EqualTo(1));
        }
    }

    public class CombatMovementTests
    {
        [Test]
        public void Reachable_CountsDiagonalsAsOneStep_AndStopsAtRange()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(new string('.', 20), new string('.', 20), new string('.', 20)) };
            var unit = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Player), new GridPos(0, 0));
            var reachable = CombatMovement.Reachable(world, unit, 6);

            Assert.That(reachable[new GridPos(2, 2)], Is.EqualTo(2));
            Assert.That(reachable.ContainsKey(new GridPos(6, 0)), Is.True);
            Assert.That(reachable.ContainsKey(new GridPos(7, 0)), Is.False);
        }

        [Test]
        public void Path_GoesAroundUnits_AndMatchesReachableSteps()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1))
            {
                Map = TestData.Parse("......", "......", "......"),
            };
            var unit = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Player), new GridPos(0, 1));
            world.AddUnit(TestData.Combat().StatsFor(UnitKind.Rifleman), new GridPos(1, 1));

            var path = CombatMovement.Path(world, unit, new GridPos(3, 1), 6);
            Assert.That(path.Count, Is.EqualTo(3));
            Assert.That(path, Has.No.Member(new GridPos(1, 1)));
            Assert.That(path.Last(), Is.EqualTo(new GridPos(3, 1)));
            for (int i = 1; i < path.Count; i++)
                Assert.That(GridNavigation.CanStep(world.Map, path[i - 1], path[i]), Is.True);
        }
    }

    public class CombatTurnTests
    {
        /// <summary>
        /// 열린 방: 플레이어 (1,2) → 전투 시작 때 (2,2), 사격병 (6,2), 상자 (5,3).
        /// 사격병이 한 걸음 옮긴 (6,3)에서는 상자가 플레이어 쪽 낮은 엄폐가 되고, 플레이어가 사거리(7칸) 안이다.
        /// </summary>
        private static (Simulation simulation, World world, Unit enemy) StartCombat()
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
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(6, 2));
            world.Map.SetTile(new GridPos(5, 3), TileType.Crate);
            var simulation = new Simulation(world);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, enemy);
        }

        [Test]
        public void CombatMove_OnceAndWithinRange()
        {
            var (simulation, world, _) = StartCombat();
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(9, 3))), Is.False, "7 steps > 6");
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(4, 3))), Is.True);
            Assert.That(world.Player.Position, Is.EqualTo(new GridPos(4, 3)));
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(3, 3))), Is.False, "only one move per turn");
        }

        [Test]
        public void Attack_EndsTheTurn_EnemyActs_NextRoundBegins()
        {
            var (simulation, world, enemy) = StartCombat();
            world.Player.CritPercent = 0;
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, TestData.Combat())), Is.True);

            Assert.That(world.Round, Is.EqualTo(2));
            Assert.That(world.Turn, Is.EqualTo(TurnSide.Player));
            Assert.That(world.PlayerActed, Is.False, "reset for the new round");
            var attacks = events.OfType<UnitAttacked>().ToList();
            Assert.That(attacks.Count, Is.EqualTo(2), "player shot, then the rifleman shot back");
            Assert.That(attacks[1].TargetId, Is.EqualTo(world.PlayerId));
            Assert.That(world.Player.Hp, Is.LessThan(world.Player.MaxHp));
        }

        [Test]
        public void Rifleman_MovesToCoverBeforeShooting()
        {
            var (simulation, world, enemy) = StartCombat();
            simulation.Execute(new EndTurnCommand(TestData.Combat()));

            Assert.That(Cover.Against(world.Map, enemy.Position, world.Player.Position), Is.Not.EqualTo(CoverLevel.None),
                $"rifleman stood at {enemy.Position}");
            Assert.That(world.Player.Hp, Is.LessThan(world.Player.MaxHp), "rifleman shot after moving");
        }

        [Test]
        public void KillingTheLastEnemy_EndsCombat_WithoutAnEnemyTurn()
        {
            var (simulation, world, enemy) = StartCombat();
            enemy.Hp = 1;
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, TestData.Combat())), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(events.OfType<TurnStarted>(), Is.Empty);
            Assert.That(events.OfType<CombatEnded>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void CannotMoveAfterShooting_InTheSameTurn()
        {
            var (simulation, world, enemy) = StartCombat();
            enemy.Hp = 100;
            enemy.MaxHp = 100;
            world.PlayerActed = true;
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(3, 3))), Is.False);
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, TestData.Combat())), Is.False);
        }

        [Test]
        public void SameSeed_SameFight()
        {
            byte[] Fight()
            {
                var (simulation, world, enemy) = StartCombat();
                enemy.Hp = 100;
                enemy.MaxHp = 100;
                for (int i = 0; i < 4; i++) simulation.Execute(new AttackCommand(enemy.Id, TestData.Combat()));
                return WorldSnapshot.Encode(world);
            }

            Assert.That(Fight(), Is.EqualTo(Fight()));
        }
    }
}
