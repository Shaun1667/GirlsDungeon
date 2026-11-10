using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>BALANCE 6번 수류탄 스킬: 사거리 6 · 반경 1 · 피해 70% · 안정 피해 2 · 재사용 3라운드 · 자기 피해 있음.</summary>
    public class AreaAttackTests
    {
        /// <summary>열린 방 (1,1) ~ (12,3). 플레이어 (1,2) → 전투 시작 때 (2,2), 사격병 둘 (6,2) · (7,2).</summary>
        private static (Simulation simulation, World world, Unit first, Unit second) StartCombat()
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
            var first = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(6, 2));
            var second = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(7, 2));
            var simulation = new Simulation(world);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, first, second);
        }

        [Test]
        public void Preview_HitsEveryUnitInThe3x3_WithTheFormula()
        {
            var (_, world, first, second) = StartCombat();
            var preview = AreaAttack.Preview(world, TestData.Combat(), world.Player, new GridPos(6, 2));

            Assert.That(preview.CanThrow, Is.True);
            Assert.That(preview.Cells.Count, Is.EqualTo(9));
            CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, preview.Hits.Select(h => h.Target.Id));
            Assert.That(preview.HitsPlayer, Is.False);
            Assert.That(preview.Hits.All(h => h.Damage == CombatRules.Damage(10, 2, 70, 0, 1)), Is.True, "5.83 → 6");
            Assert.That(preview.Hits.First().Damage, Is.EqualTo(6));
        }

        [Test]
        public void Cover_IsJudgedFromTheBlast_NotFromTheThrower()
        {
            // 상자 (4,0) · 적 (5,0). 플레이어 (0,0) 쪽에서 보면 상자 뒤지만, 폭심지 (6,0)은 반대편이라 엄폐가 없다.
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(".........") };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(0, 0)).Id;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(5, 0));
            world.Map.SetTile(new GridPos(4, 0), TileType.Crate);

            var hit = AreaAttack.Preview(world, combat, world.Player, new GridPos(6, 0)).Hits.Single(h => h.Target.Id == enemy.Id);
            Assert.That(hit.Cover, Is.EqualTo(CoverLevel.None));
            Assert.That(hit.ReductionPercent, Is.EqualTo(0));
            Assert.That(enemy.Stability, Is.GreaterThan(0), "stability is up, but a blast never gets the 60% guard");
        }

        [Test]
        public void LowCover_FacingTheBlast_Reduces25_NotThe85OfAShot()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(".........") };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(0, 0)).Id;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(5, 0));
            world.Map.SetTile(new GridPos(4, 0), TileType.Crate);
            world.Mode = WorldMode.Combat;

            var hit = AreaAttack.Preview(world, combat, world.Player, new GridPos(4, 0)).Hits.Single(h => h.Target.Id == enemy.Id);
            Assert.That(hit.Cover, Is.EqualTo(CoverLevel.Half));
            Assert.That(hit.ReductionPercent, Is.EqualTo(25));
            Assert.That(hit.Damage, Is.EqualTo(CombatRules.Damage(10, 2, 70, 25, 1)));
        }

        [Test]
        public void Throw_IsBlockedOutOfRange_OrWhenNotVisible()
        {
            var (_, world, _, _) = StartCombat();
            Assert.That(AreaAttack.Preview(world, TestData.Combat(), world.Player, new GridPos(9, 2)).Block, Is.EqualTo(AreaBlock.OutOfRange));

            world.Map.SetTile(new GridPos(4, 1), TileType.Wall);
            world.Map.SetTile(new GridPos(4, 2), TileType.Wall);
            world.Map.SetTile(new GridPos(4, 3), TileType.Wall);
            Assert.That(AreaAttack.Preview(world, TestData.Combat(), world.Player, new GridPos(6, 2)).Block, Is.EqualTo(AreaBlock.NotVisible));
        }

        [Test]
        public void Throw_DamagesAll_TakesStability_EndsTheTurn_AndStartsCooldown()
        {
            var (simulation, world, first, second) = StartCombat();
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);

            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(6, 2), TestData.Combat())), Is.True);

            Assert.That(first.Hp, Is.EqualTo(10 - 6));
            Assert.That(second.Hp, Is.EqualTo(10 - 6));
            Assert.That(first.Stability, Is.EqualTo(3 - 2));
            Assert.That(events.OfType<AreaExploded>().Count(), Is.EqualTo(1));
            Assert.That(world.Round, Is.EqualTo(2), "the enemy turn ran");
            Assert.That(world.GrenadeCooldown, Is.EqualTo(2), "3 set on throw, minus 1 at the next player turn");
        }

        [Test]
        public void Cooldown_BlocksUntilReady()
        {
            var (simulation, world, first, second) = StartCombat();
            first.Hp = first.MaxHp = 100;
            second.Hp = second.MaxHp = 100;
            world.Player.Hp = world.Player.MaxHp = 100;
            var settings = TestData.Combat();

            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(6, 2), settings)), Is.True);
            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(6, 2), settings)), Is.False, "round 2: cooldown 2");
            simulation.Execute(new EndTurnCommand(settings));
            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(6, 2), settings)), Is.False, "round 3: cooldown 1");
            simulation.Execute(new EndTurnCommand(settings));
            Assert.That(world.GrenadeCooldown, Is.EqualTo(0));
            Assert.That(world.Round, Is.EqualTo(4));
        }

        [Test]
        public void PlayerInsideTheBlast_IsHit_AndCanFall()
        {
            var (simulation, world, _, _) = StartCombat();
            var preview = AreaAttack.Preview(world, TestData.Combat(), world.Player, new GridPos(3, 2));
            Assert.That(preview.HitsPlayer, Is.True);

            world.Player.Hp = 1;
            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(3, 2), TestData.Combat())), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Defeat));
        }

        [Test]
        public void KillingTheLastEnemies_EndsCombat_AndResetsCooldown()
        {
            var (simulation, world, first, second) = StartCombat();
            first.Hp = 1;
            second.Hp = 1;

            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(6, 2), TestData.Combat())), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(world.GrenadeCooldown, Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_KeepsTheCooldown()
        {
            var (simulation, world, first, second) = StartCombat();
            first.Hp = first.MaxHp = 100;
            second.Hp = second.MaxHp = 100;
            simulation.Execute(new AreaAttackCommand(new GridPos(6, 2), TestData.Combat()));

            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.GrenadeCooldown, Is.EqualTo(world.GrenadeCooldown));
        }
    }
}
