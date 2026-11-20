using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-17g 행동 지원 (대원 B, 결정 2026-11-20 사용자): 그 차례의 첫 기본 사격 · 빙결 사격이 맞힌 적을 한 번 더 — 사격과 똑같은 계산에 80% · 안정 3,
    /// 그 뒤 치명피해 상승Ⅰ(+10%p, 2차례). 기관단총의 두 번째 발 · 스킬 뒤 남은 한 발에는 발동하지 않는다 (BALANCE 34-9).
    /// </summary>
    public class ActionSupportTests
    {
        /// <summary>열린 방: 대원 B (2,2) 기관단총 전투 중, 사격병 (5,2) 엄폐 없음 · 체력 300.</summary>
        private static (Simulation simulation, World world, Unit enemy, CombatSettings combat) StartCombat(CharacterKind character = CharacterKind.HeroB)
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
            world.PlayerWeapon = WeaponKind.Smg;
            world.Player.MaxHp = world.Player.Hp = 300;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(5, 2));
            enemy.MaxHp = enemy.Hp = 300;
            enemy.MaxStability = enemy.Stability = 30;
            Loadout.Apply(world, combat);
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(2, 2), 1)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            return (simulation, world, enemy, combat);
        }

        private static List<GameEvent> Run(Simulation simulation, ICommand command)
        {
            var events = new List<GameEvent>();
            System.Action<IReadOnlyList<GameEvent>> handler = published => events.AddRange(published);
            simulation.EventsPublished += handler;
            Assert.That(simulation.Execute(command), Is.True);
            simulation.EventsPublished -= handler;
            return events;
        }

        [Test]
        public void FirstShot_HitsAgainAt80Percent_SecondShotDoesNot()
        {
            var (simulation, world, enemy, combat) = StartCombat();
            var player = world.Player;
            var shot = CombatRules.Preview(world, combat, player, player.Position, enemy);
            int critMultiplier = player.CritMultiplierPercent;
            int stability = enemy.Stability;

            var first = Run(simulation, new AttackCommand(enemy.Id, combat));
            Assert.That(first.OfType<ActionSupportTriggered>().Count(), Is.EqualTo(1));
            var hits = first.OfType<UnitAttacked>().Where(hit => hit.AttackerId == player.Id).ToList();
            Assert.That(hits.Count, Is.EqualTo(2), "the shot and the follow-up");
            int plain = CombatRules.Damage(player.Attack, enemy.Defense, 80, shot.ReductionPercent, combat.MinDamage, 100 - shot.FalloffPercent, shot.SetPercent);
            int crit = CombatRules.Damage(player.Attack, enemy.Defense, critMultiplier * 80 / 100, shot.ReductionPercent, combat.MinDamage, 100 - shot.FalloffPercent, shot.SetPercent);
            Assert.That(hits[1].Damage, Is.EqualTo(hits[1].IsCrit ? crit : plain), "same math as a shot, 80%");
            Assert.That(enemy.Stability, Is.EqualTo(stability - shot.StabilityDamage - 3));

            // 치명피해 상승Ⅰ: +10%p, 2차례.
            Assert.That(world.PlayerCritBoostTurns, Is.EqualTo(2));
            Assert.That(player.CritMultiplierPercent, Is.EqualTo(critMultiplier + 10));

            // 기관단총 두 번째 발 = 추가 사격 → 발동하지 않는다.
            var second = Run(simulation, new AttackCommand(enemy.Id, combat));
            Assert.That(second.OfType<ActionSupportTriggered>(), Is.Empty);
        }

        [Test]
        public void FrostShot_Triggers_ButTheShotLeftAfterIt_DoesNot()
        {
            var (simulation, world, enemy, combat) = StartCombat();
            var frost = Run(simulation, new FrostAttackCommand(enemy.Id, combat));
            Assert.That(frost.OfType<ActionSupportTriggered>().Count(), Is.EqualTo(1));
            Assert.That(world.PlayerShots, Is.EqualTo(1), "SMG: the skill took the first shot (3-17f)");

            var extra = Run(simulation, new AttackCommand(enemy.Id, combat));
            Assert.That(extra.OfType<ActionSupportTriggered>(), Is.Empty);
        }

        [Test]
        public void OtherCharacters_HaveNoActionSupport()
        {
            var (simulation, _, enemy, combat) = StartCombat(CharacterKind.HeroC);
            var events = Run(simulation, new AttackCommand(enemy.Id, combat));
            Assert.That(events.OfType<ActionSupportTriggered>(), Is.Empty);
        }

        [Test]
        public void CritBoost_RunsOutAfterTwoPlayerTurns()
        {
            var (_, world, enemy, combat) = StartCombat();
            int baseCrit = world.Player.CritMultiplierPercent;
            Assert.That(ActionSupport.Trigger(world, combat, enemy.Id, new List<GameEvent>()), Is.True);
            Assert.That(world.Player.CritMultiplierPercent, Is.EqualTo(baseCrit + 10));
            ActionSupport.OnPlayerTurnStarted(world, combat, new List<GameEvent>());
            Assert.That(world.Player.CritMultiplierPercent, Is.EqualTo(baseCrit + 10));
            ActionSupport.OnPlayerTurnStarted(world, combat, new List<GameEvent>());
            Assert.That(world.PlayerCritBoostTurns, Is.EqualTo(0));
            Assert.That(world.Player.CritMultiplierPercent, Is.EqualTo(baseCrit));

            Assert.That(SetEffects.IsOffTurn(DamageSource.Support), Is.True, "a support attack is off-turn damage");
            Assert.That(SetEffects.ElementOf(world, combat, DamageSource.Support), Is.EqualTo(DamageElement.Physical));
        }

        [Test]
        public void OutOfRange_DoesNotTrigger()
        {
            var (_, world, enemy, combat) = StartCombat();
            enemy.Position = new GridPos(12, 2); // 거리 10 > 기관단총 사거리 6
            Assert.That(ActionSupport.Trigger(world, combat, enemy.Id, new List<GameEvent>()), Is.False);
            Assert.That(world.PlayerCritBoostTurns, Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_KeepsTheCritBoost()
        {
            var (_, world, enemy, combat) = StartCombat();
            Assert.That(ActionSupport.Trigger(world, combat, enemy.Id, new List<GameEvent>()), Is.True);
            var bytes = WorldSnapshot.Encode(world);
            var copy = WorldSnapshot.Decode(bytes);
            Assert.That(copy.PlayerCritBoostTurns, Is.EqualTo(2));
            Assert.That(WorldSnapshot.Encode(copy), Is.EqualTo(bytes));
        }
    }
}
