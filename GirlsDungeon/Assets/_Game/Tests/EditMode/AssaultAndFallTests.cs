using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// BALANCE 10번 돌격병 · 낙하: 바로 옆(동서남북) 근접 공격 = 보통 피해 + 1칸 밀기, 막히면 충돌 3,
    /// 플레이어가 떨어지면 최대 체력 50% 피해 후 가장 가까운 빈 바닥에서 다시 일어남.
    /// </summary>
    public class AssaultAndFallTests
    {
        private static World MakeWorld(GridPos player, GridPos assault, params string[] rows)
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(rows) };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), player).Id;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Assault), assault);
            world.Combatants.Add(enemy.Id);
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            world.Round = 1;
            return world;
        }

        private static Unit Assault(World world) => world.Units.Single(u => u.Kind == UnitKind.Assault);

        [Test]
        public void FallDamage_IsHalfOfMaxHp_RoundedHalfUp()
        {
            Assert.That(Push.FallDamage(20, 50), Is.EqualTo(10));
            Assert.That(Push.FallDamage(15, 50), Is.EqualTo(8), "7.5 → 8");
        }

        [Test]
        public void MeleeHit_DealsTheFormulaDamage_AndPushesThePlayerOneCellAway()
        {
            var world = MakeWorld(new GridPos(2, 0), new GridPos(1, 0), "........");
            var events = new List<GameEvent>();

            Assert.That(CombatRules.Resolve(world, TestData.Combat(), Assault(world), world.Player, events), Is.True);
            int damage = events.OfType<UnitAttacked>().Single().Damage;
            Assert.That(damage, Is.EqualTo(CombatRules.Damage(6, 4, 100, 0, 1)).Or.EqualTo(CombatRules.Damage(6, 4, 150, 0, 1)), "3.6 → 4, crit 5");
            Assert.That(world.Player.Position, Is.EqualTo(new GridPos(3, 0)));
            Assert.That(world.Player.Hp, Is.EqualTo(100 - damage));
            Assert.That(events.OfType<UnitPushed>().Single().CollisionDamage, Is.EqualTo(0));
        }

        [Test]
        public void Melee_ReachesOnlyTheFourSides()
        {
            var world = MakeWorld(new GridPos(2, 0), new GridPos(1, 1), "........", "........");
            var preview = CombatRules.Preview(world, TestData.Combat(), Assault(world), Assault(world).Position, world.Player);
            Assert.That(preview.Block, Is.EqualTo(AttackBlock.OutOfRange));
        }

        [Test]
        public void PushedIntoAWall_ThePlayerStays_AndTakesCollisionDamage()
        {
            var world = MakeWorld(new GridPos(1, 0), new GridPos(0, 0), "..#.....");
            var events = new List<GameEvent>();

            CombatRules.Resolve(world, TestData.Combat(), Assault(world), world.Player, events);
            int damage = events.OfType<UnitAttacked>().Single().Damage;
            Assert.That(world.Player.Position, Is.EqualTo(new GridPos(1, 0)));
            Assert.That(world.Player.Hp, Is.EqualTo(100 - damage - 3));
        }

        [Test]
        public void PlayerPushedIntoAChasm_LosesHalfMaxHp_AndGetsUpOnTheNearestOpenFloor()
        {
            var world = MakeWorld(new GridPos(2, 0), new GridPos(1, 0), "...X....");
            var events = new List<GameEvent>();

            CombatRules.Resolve(world, TestData.Combat(), Assault(world), world.Player, events);
            int damage = events.OfType<UnitAttacked>().Single().Damage;
            var fell = events.OfType<PlayerFell>().Single();
            Assert.That(fell.From, Is.EqualTo(new GridPos(3, 0)));
            Assert.That(fell.Damage, Is.EqualTo(50), "half of max hp 100");
            Assert.That(fell.To, Is.EqualTo(new GridPos(4, 0)), "first open floor next to the chasm cell");
            Assert.That(world.Player.Position, Is.EqualTo(new GridPos(4, 0)));
            Assert.That(world.Player.Hp, Is.EqualTo(100 - damage - 50));
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
        }

        [Test]
        public void TheFall_CanStillKnockThePlayerDown()
        {
            var world = MakeWorld(new GridPos(2, 0), new GridPos(1, 0), "...X....");
            world.Player.Hp = 9;
            var events = new List<GameEvent>();

            CombatRules.Resolve(world, TestData.Combat(), Assault(world), world.Player, events);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Defeat));
            Assert.That(world.Player.Hp, Is.EqualTo(0));
            Assert.That(events.OfType<PlayerDefeated>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void NearestOpenFloor_SkipsOccupiedCells()
        {
            var world = MakeWorld(new GridPos(4, 0), new GridPos(6, 0), "...X....");
            Assert.That(Push.NearestOpenFloor(world, new GridPos(3, 0)), Is.EqualTo(new GridPos(2, 0)), "(4,0) holds the player");
        }

        [Test]
        public void AssaultTurn_PicksTheCellThatThrowsThePlayerIntoTheChasm()
        {
            var world = MakeWorld(new GridPos(3, 3), new GridPos(0, 0),
                ".......",
                ".......",
                ".......",
                "....X..",
                ".......",
                ".......",
                ".......");
            var simulation = new Simulation(world, TestData.Combat());
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            Assert.That(simulation.Execute(new EndTurnCommand(TestData.Combat())), Is.True);
            Assert.That(Assault(world).Position, Is.EqualTo(new GridPos(2, 3)), "the only side that pushes east into the chasm");
            Assert.That(events.OfType<PlayerFell>().Count(), Is.EqualTo(1));
            Assert.That(world.Player.Position, Is.EqualTo(new GridPos(5, 3)));
        }

        [Test]
        public void AssaultThatCannotReach_DoesNotSetOverwatch()
        {
            var world = MakeWorld(new GridPos(0, 0), new GridPos(7, 0), "........");
            Assault(world).MoveRange = 1;
            var simulation = new Simulation(world, TestData.Combat());
            Assert.That(simulation.Execute(new EndTurnCommand(TestData.Combat())), Is.True);
            Assert.That(Assault(world).Position, Is.EqualTo(new GridPos(6, 0)));
            Assert.That(Assault(world).Overwatching, Is.False);
        }

        [Test]
        public void Spawner_MixesAssaultsAndRiflemen()
        {
            int assaults = 0, total = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                foreach (var unit in TestData.NewRun(seed, 5).Units)
                {
                    if (unit.IsPlayer) continue;
                    total++;
                    if (unit.Kind == UnitKind.Assault)
                    {
                        assaults++;
                        Assert.That(unit.PushOnHit, Is.EqualTo(1));
                        Assert.That(unit.AttackRange, Is.EqualTo(1));
                    }
                }
            }
            double share = assaults * 100.0 / total;
            Assert.That(share, Is.InRange(20.0, 40.0), $"assault share {share:F1}% (expected about 30%)");
        }

        [Test]
        public void Spawner_KeepsTheSamePlacesWhenOnlyTheKindMixChanges()
        {
            var withMix = TestData.NewRun(9, 5).Units.Where(u => !u.IsPlayer).Select(u => u.Position).ToList();
            var combat = TestData.Combat(5);
            combat.AssaultPercent = 0;
            var noMix = RunFactory.NewRun(9, TestData.Map(), combat).Units.Where(u => !u.IsPlayer).Select(u => u.Position).ToList();
            CollectionAssert.AreEqual(withMix, noMix);
        }

        [Test]
        public void Snapshot_KeepsPushOnHit()
        {
            var world = TestData.NewRun(3, 5);
            var assault = world.Units.First(u => !u.IsPlayer);
            assault.PushOnHit = 2;
            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.FindUnit(assault.Id).PushOnHit, Is.EqualTo(2));
        }
    }
}
