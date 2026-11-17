using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-16 리몰딩 지수 · 대원 A 특성 (결정 2026-11-14 사용자): 최대 6, 행동 시작 +1, 피해 1회마다 +1, 음파 1 · 수류탄 4 소모(재사용 대기 없음),
    /// 넉백은 재사용 대기 그대로, 판 내내 유지. 이동 감소를 가진 적에게 피해 +20%, 5칸 이상 걸으면 공격 전에 대상에게 이동 감소Ⅱ(2칸 · 2차례).
    /// </summary>
    public class RemoldTests
    {
        /// <summary>
        /// 열린 15 × 9 방: 플레이어 (1,4) → (2,4) 걸어 전투 시작, 사격병 (7,4) (체력 100 — 한 방에 쓰러지지 않게).
        /// </summary>
        private static (Simulation simulation, World world, Unit enemy, CombatSettings combat) StartCombat(CharacterKind character)
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
            world.PlayerCharacter = character;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(7, 4));
            enemy.MaxHp = enemy.Hp = 100;
            Loadout.Apply(world, combat);
            var simulation = new Simulation(world, combat); // 전투 수치를 넘겨야 전투 시작 리몰딩이 들어온다 (게임과 같음)
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 4), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, enemy, combat);
        }

        [Test]
        public void HeroA_GainsOnePerTurnStart_UpToSix_OthersHaveNone()
        {
            var (simulation, world, _, combat) = StartCombat(CharacterKind.HeroA);
            Assert.That(world.PlayerRemold, Is.EqualTo(1), "combat start is the first action");
            for (int i = 0; i < 8 && world.Mode == WorldMode.Combat; i++)
            {
                world.Player.Hp = world.Player.MaxHp;
                simulation.Execute(new EndTurnCommand(combat));
            }
            Assert.That(world.PlayerRemold, Is.EqualTo(6), "capped at the maximum");

            var (_, other, _, otherCombat) = StartCombat(CharacterKind.HeroB);
            Assert.That(Remold.Max(other, otherCombat), Is.EqualTo(0));
            Assert.That(other.PlayerRemold, Is.EqualTo(0));
        }

        [Test]
        public void Sonic_CostsOne_NoCooldown_BlockedWithoutRemold()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            world.PlayerRemold = 0;
            Assert.That(SonicAttack.Preview(world, combat, world.Player, enemy).Block, Is.EqualTo(SonicBlock.NotReady));
            Assert.That(simulation.Execute(new SonicAttackCommand(enemy.Id, combat)), Is.False);

            world.PlayerRemold = 1;
            Assert.That(Remold.Cost(world, combat, SkillKind.Sonic), Is.EqualTo(1));
            Assert.That(simulation.Execute(new SonicAttackCommand(enemy.Id, combat)), Is.True);
            Assert.That(world.SonicCooldown, Is.EqualTo(0));
            // 1 − 1 + 피해 1 + 다음 차례 시작 1.
            Assert.That(world.PlayerRemold, Is.EqualTo(2));
            Assert.That(Remold.IsReady(world, combat, SkillKind.Sonic), Is.True, "usable again right away");
        }

        [Test]
        public void Grenade_CostsFour_AndEachHitGivesOne()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            var second = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(7, 5));
            second.MaxHp = second.Hp = 100;
            world.Combatants.Add(second.Id);
            world.Combatants.Sort();

            world.PlayerRemold = 3;
            Assert.That(AreaAttack.Preview(world, combat, world.Player, new GridPos(7, 4)).Block, Is.EqualTo(AreaBlock.NotReady));

            world.PlayerRemold = 4;
            var preview = AreaAttack.Preview(world, combat, world.Player, new GridPos(7, 4));
            Assert.That(preview.CanThrow, Is.True);
            Assert.That(preview.Hits.Count, Is.EqualTo(2));
            var round = world.Round;
            Assert.That(simulation.Execute(new AreaAttackCommand(new GridPos(7, 4), combat)), Is.True);
            Assert.That(world.GrenadeCooldown, Is.EqualTo(0));
            Assert.That(world.Round, Is.EqualTo(round + 1));
            // 4 − 4 + 맞힌 적 2 + 다음 차례 시작 1.
            Assert.That(world.PlayerRemold, Is.EqualTo(3));
        }

        [Test]
        public void Push_KeepsItsCooldown()
        {
            var (_, world, _, combat) = StartCombat(CharacterKind.HeroA);
            Assert.That(Remold.Cost(world, combat, SkillKind.Push), Is.EqualTo(0));
            world.PushCooldown = 2;
            Assert.That(Remold.IsReady(world, combat, SkillKind.Push), Is.False);
            Assert.That(Remold.Cooldown(world, combat, SkillKind.Push), Is.EqualTo(2));
        }

        [Test]
        public void WalkingFiveCells_SlowsTheTargetBeforeTheShot_AndThePreviewShowsTheBonus()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            var player = world.Player;
            var cell = new GridPos(7, 1);
            Assert.That(Loadout.MoveLeft(world), Is.GreaterThanOrEqualTo(5));

            // 이동 예약 미리보기 (아직 걷지 않음): (2,4) → (7,1)은 5걸음이라 +20%가 이미 보인다.
            var planned = CombatRules.Preview(world, combat, player, cell, enemy);
            Assert.That(planned.SetPercent, Is.EqualTo(20));
            var near = CombatRules.Preview(world, combat, player, new GridPos(3, 4), enemy);
            Assert.That(near.SetPercent, Is.EqualTo(0), "one step is not enough");

            Assert.That(simulation.Execute(new CombatMoveCommand(cell, combat)), Is.True);
            Assert.That(world.PlayerMoveUsed, Is.EqualTo(5));
            var preview = CombatRules.Preview(world, combat, player, player.Position, enemy);
            Assert.That(preview.CanAttack, Is.True);
            Assert.That(preview.Damage, Is.EqualTo(planned.Damage), "the planned preview equals the real one");
            Assert.That(preview.Damage, Is.EqualTo(CombatRules.Damage(player.Attack, enemy.Defense, 100, preview.ReductionPercent, combat.MinDamage,
                100 - preview.FalloffPercent, 20)));

            int hp = enemy.Hp;
            var events = new List<GameEvent>();
            simulation.EventsPublished += published => events.AddRange(published);
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.True);
            var slowed = events.OfType<UnitSlowed>().Single();
            Assert.That(slowed.UnitId, Is.EqualTo(enemy.Id));
            Assert.That(slowed.Amount, Is.EqualTo(2));
            int dealt = events.OfType<UnitAttacked>().First(e => e.TargetId == enemy.Id).Damage;
            Assert.That(dealt, Is.EqualTo(preview.Damage).Or.EqualTo(preview.CritDamage));
            Assert.That(enemy.Hp, Is.LessThanOrEqualTo(hp - dealt));
        }

        [Test]
        public void SlowedEnemy_TakesTwentyPercentMore_EvenWithoutWalking()
        {
            var (_, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            var player = world.Player;
            var plain = CombatRules.Preview(world, combat, player, player.Position, enemy);
            enemy.SlowAmount = 2;
            enemy.SlowTurns = 1;
            var slowed = CombatRules.Preview(world, combat, player, player.Position, enemy);
            Assert.That(plain.SetPercent, Is.EqualTo(0));
            Assert.That(slowed.SetPercent, Is.EqualTo(20));
            Assert.That(SonicAttack.Preview(world, combat, player, enemy).Damage,
                Is.EqualTo(CombatRules.Damage(player.Attack, enemy.Defense, combat.SkillDamagePercentFor(CharacterKind.HeroA, SkillKind.Sonic), 0, combat.MinDamage, 100, 20)));

            // 다른 캐릭터에게는 이 특성이 없다.
            world.PlayerCharacter = CharacterKind.HeroB;
            Assert.That(CombatRules.Preview(world, combat, player, player.Position, enemy).SetPercent, Is.EqualTo(0));
        }

        [Test]
        public void Slow_ShortensEnemyMoves_ForTwoTurns_AndClearsAtCombatEnd()
        {
            var unit = new Unit(9, UnitKind.Rifleman, new GridPos(0, 0)) { MoveRange = 6, SlowAmount = 2, SlowTurns = 2 };
            Assert.That(Remold.MoveRangeOf(unit), Is.EqualTo(4));
            Remold.OnEnemyActed(unit);
            Assert.That(Remold.MoveRangeOf(unit), Is.EqualTo(4));
            Remold.OnEnemyActed(unit);
            Assert.That(Remold.MoveRangeOf(unit), Is.EqualTo(6));
            Assert.That(Remold.IsSlowed(unit), Is.False);

            // 적 차례: 이동 감소가 걸린 사격병은 줄어든 칸 안에서만 걷는다.
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            enemy.Position = new GridPos(13, 7);
            world.Player.Position = new GridPos(1, 1);
            enemy.SlowAmount = 4;
            enemy.SlowTurns = 1;
            var start = enemy.Position;
            Assert.That(simulation.Execute(new EndTurnCommand(combat)), Is.True);
            int moved = System.Math.Max(System.Math.Abs(enemy.Position.X - start.X), System.Math.Abs(enemy.Position.Y - start.Y));
            Assert.That(moved, Is.LessThanOrEqualTo(2), "6 - 4 = 2 cells");
            Assert.That(enemy.SlowTurns, Is.EqualTo(0));

            enemy.SlowAmount = 2;
            enemy.SlowTurns = 2;
            Remold.OnCombatEnded(world);
            Assert.That(Remold.IsSlowed(enemy), Is.False);
        }

        [Test]
        public void Remold_StaysAfterCombat()
        {
            var (simulation, world, enemy, combat) = StartCombat(CharacterKind.HeroA);
            world.PlayerRemold = 5;
            enemy.Hp = 1;
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(world.PlayerRemold, Is.EqualTo(6), "5 + the hit, kept after the fight");
        }

        [Test]
        public void Snapshot_KeepsRemoldAndSlow()
        {
            var (_, world, enemy, _) = StartCombat(CharacterKind.HeroA);
            world.PlayerRemold = 4;
            enemy.SlowAmount = 2;
            enemy.SlowTurns = 1;
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(restored.PlayerRemold, Is.EqualTo(4));
            Assert.That(restored.FindUnit(enemy.Id).SlowAmount, Is.EqualTo(2));
            Assert.That(restored.FindUnit(enemy.Id).SlowTurns, Is.EqualTo(1));
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
        }
    }
}
