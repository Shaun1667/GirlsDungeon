using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-15f 상점 주인 스킬 (BALANCE 33번): 밑천(스택) · 일반 공격 80% · 강공격 · 화염탄(화상) · 결전기(방벽) · 추가 공격.
    /// 검산 값: 상점 주인 공격 9 · 체력 80, 플레이어 방어 4 → 화염탄 9 × 9 ÷ 13 = 6, 강공격 × 1.2 = 7, 일반 × 0.8 = 5 (치명 7),
    /// 추가 공격 밑천 3 × 4% × 80 = 9.6 → 10.
    /// </summary>
    public class ShopkeeperSkillTests
    {
        /// <summary>열린 방에서 이미 적대한 상점 주인과 전투 중 (플레이어 차례). 엄폐 없음, 거리 3.</summary>
        private static (World world, Unit keeper, Unit player, CombatSettings combat) Fight()
        {
            var combat = TestData.Combat();
            var world = new World(1, new RandomStreams(1))
            {
                Map = TestData.ParseWithRooms(new[] { new RoomRect(1, 1, 10, 5) },
                    "############",
                    "#..........#",
                    "#..........#",
                    "#..........#",
                    "#..........#",
                    "#..........#",
                    "############"),
            };
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(3, 3)).Id;
            var keeper = world.AddUnit(combat.StatsFor(UnitKind.Shopkeeper), new GridPos(6, 3));
            Loadout.Apply(world, combat);
            world.ShopHostile = true;
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Enemies;
            world.Round = 1;
            world.Combatants.Add(keeper.Id);
            return (world, keeper, world.Player, combat);
        }

        [Test]
        public void FirstAction_ThrowsTheFireBomb_PaysHp_GainsStacks_BurnsAndAttacksAgain()
        {
            var (world, keeper, player, combat) = Fight();
            int hp = player.Hp;
            var events = new List<GameEvent>();

            Assert.That(KeeperSkills.Act(world, combat, keeper, player, events), Is.True);

            Assert.That(events.OfType<KeeperSkillUsed>().Single().Skill, Is.EqualTo(KeeperSkill.Order));
            Assert.That(keeper.Hp, Is.EqualTo(120 - 48), "40% of max hp");
            Assert.That(world.KeeperStacks, Is.EqualTo(3));
            Assert.That(world.KeeperOrderCooldown, Is.EqualTo(2));
            Assert.That(world.PlayerBurnTurns, Is.EqualTo(2));
            Assert.That(world.KeeperRemold, Is.EqualTo(3), "two hits, then the passive at the end of the action");
            Assert.That(events.OfType<AreaExploded>().Count(), Is.EqualTo(1));
            var hits = events.OfType<UnitAttacked>().Select(hit => hit.Damage).ToArray();
            Assert.That(hits, Is.EqualTo(new[] { 6, 14 }), "fire bomb, then one extra attack for 3 stacks (3 × 4% × max hp 120 = 14.4)");
            Assert.That(player.Hp, Is.EqualTo(hp - 20));
        }

        [Test]
        public void SecondAction_UsesTheSingleSkill_WhileTheFireBombCoolsDown()
        {
            var (world, keeper, player, combat) = Fight();
            KeeperSkills.Act(world, combat, keeper, player, new List<GameEvent>());
            KeeperSkills.StartAction(world, combat, keeper);
            Assert.That(world.KeeperStacks, Is.EqualTo(1), "3 stacks lose half, rounded up");
            Assert.That(world.KeeperOrderCooldown, Is.EqualTo(1));

            int keeperHp = keeper.Hp;
            var events = new List<GameEvent>();
            Assert.That(KeeperSkills.Act(world, combat, keeper, player, events), Is.True);

            Assert.That(events.OfType<KeeperSkillUsed>().Single().Skill, Is.EqualTo(KeeperSkill.Heart));
            Assert.That(keeper.Hp, Is.EqualTo(keeperHp - 36), "30% of max hp");
            Assert.That(world.KeeperStacks, Is.EqualTo(3));
            Assert.That(world.KeeperHeartCooldown, Is.EqualTo(2));
            var hits = events.OfType<UnitAttacked>().Select(hit => hit.Damage).ToArray();
            Assert.That(hits, Is.EqualTo(new[] { 7, 10 }), "120% shot, then one extra attack for 3 stacks");
        }

        [Test]
        public void WithBothSkillsCoolingDown_TheBasicShotUsesEightyPercent()
        {
            var (world, keeper, player, combat) = Fight();
            world.KeeperOrderCooldown = 1;
            world.KeeperHeartCooldown = 1;
            var preview = CombatRules.Preview(world, combat, keeper, keeper.Position, player);
            Assert.That(preview.Damage, Is.EqualTo(5));
            Assert.That(preview.CritDamage, Is.EqualTo(7));

            var events = new List<GameEvent>();
            Assert.That(KeeperSkills.Act(world, combat, keeper, player, events), Is.True);
            Assert.That(events.OfType<KeeperSkillUsed>().Any(), Is.False);
            Assert.That(events.OfType<UnitAttacked>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Remold_FollowsThePlayerRules_ActionStart_EachHit_AndTheCap()
        {
            var (world, keeper, player, combat) = Fight();
            KeeperSkills.StartAction(world, combat, keeper);
            Assert.That(world.KeeperRemold, Is.EqualTo(1), "+1 at the start of an action");

            world.KeeperOrderCooldown = 1;
            world.KeeperHeartCooldown = 1;
            Assert.That(CombatRules.Resolve(world, combat, keeper, player, new List<GameEvent>()), Is.True);
            Assert.That(world.KeeperRemold, Is.EqualTo(2), "+1 for a basic shot that deals damage (overwatch shots too)");

            Assert.That(CombatRules.Resolve(world, combat, player, keeper, new List<GameEvent>()), Is.True);
            Assert.That(world.KeeperRemold, Is.EqualTo(2), "nothing for being hit");

            world.KeeperRemold = 5;
            world.KeeperOrderCooldown = 0;
            KeeperSkills.Act(world, combat, keeper, player, new List<GameEvent>());
            Assert.That(world.KeeperRemold, Is.EqualTo(6), "never above the maximum");
        }

        [Test]
        public void SkillCost_NeverGoesBelowTheHpFloor()
        {
            var (world, keeper, player, combat) = Fight();
            keeper.Hp = 15;
            KeeperSkills.Act(world, combat, keeper, player, new List<GameEvent>());
            Assert.That(keeper.Hp, Is.EqualTo(12), "10% of max hp is kept");
            Assert.That(world.KeeperStacks, Is.EqualTo(3), "the skill is still used");
        }

        [Test]
        public void Stacks_ReduceDamageTaken_AndHalveAtTheStartOfEachAction()
        {
            var (world, keeper, player, combat) = Fight();
            int plain = CombatRules.Preview(world, combat, player, player.Position, keeper).Damage;
            world.KeeperStacks = 6;
            Assert.That(KeeperSkills.DamageTakenReduction(world, combat, keeper), Is.EqualTo(60));
            Assert.That(SetEffects.DamagePercent(world, combat, player, keeper, DamageSource.Shot), Is.EqualTo(-60));
            Assert.That(CombatRules.Preview(world, combat, player, player.Position, keeper).Damage, Is.LessThan(plain));
            Assert.That(KeeperSkills.DamageTakenReduction(world, combat, player), Is.EqualTo(0), "only the shopkeeper");

            KeeperSkills.StartAction(world, combat, keeper);
            Assert.That(world.KeeperStacks, Is.EqualTo(3));
            KeeperSkills.StartAction(world, combat, keeper);
            Assert.That(world.KeeperStacks, Is.EqualTo(1));
            KeeperSkills.StartAction(world, combat, keeper);
            Assert.That(world.KeeperStacks, Is.EqualTo(0));
        }

        [Test]
        public void FullMomentum_UsesTheFinalSkill_AndTheBarrierAbsorbsDamage()
        {
            var (world, keeper, player, combat) = Fight();
            world.KeeperRemold = 6;
            var events = new List<GameEvent>();
            Assert.That(KeeperSkills.Act(world, combat, keeper, player, events), Is.True);

            Assert.That(events.OfType<KeeperSkillUsed>().Single().Skill, Is.EqualTo(KeeperSkill.Final));
            Assert.That(world.KeeperStacks, Is.EqualTo(6));
            Assert.That(world.KeeperShield, Is.EqualTo(60), "50% of max hp");
            Assert.That(world.KeeperShieldTurns, Is.EqualTo(3));
            Assert.That(world.KeeperRemold, Is.EqualTo(1), "spent 6, then +1 for the action");
            Assert.That(events.OfType<UnitAttacked>().Any(), Is.False, "the final skill is the whole action");

            Assert.That(KeeperSkills.AfterShield(world, keeper, 10, events), Is.EqualTo(0));
            Assert.That(world.KeeperShield, Is.EqualTo(50));
            Assert.That(KeeperSkills.AfterShield(world, keeper, 70, events), Is.EqualTo(20));
            Assert.That(world.KeeperShield, Is.EqualTo(0));
            Assert.That(KeeperSkills.AfterShield(world, player, 10, events), Is.EqualTo(10), "only the shopkeeper");
        }

        [Test]
        public void Barrier_ExpiresAfterThreeActions()
        {
            var (world, keeper, _, combat) = Fight();
            world.KeeperShield = 40;
            world.KeeperShieldTurns = 3;
            KeeperSkills.StartAction(world, combat, keeper);
            KeeperSkills.StartAction(world, combat, keeper);
            Assert.That(world.KeeperShield, Is.EqualTo(40));
            KeeperSkills.StartAction(world, combat, keeper);
            Assert.That(world.KeeperShield, Is.EqualTo(0));
        }

        [Test]
        public void Burn_HurtsAtTheStartOfThePlayerTurn_AndRunsOut()
        {
            var (world, _, player, combat) = Fight();
            world.PlayerBurnTurns = 2;
            int hp = player.Hp;
            var events = new List<GameEvent>();
            KeeperSkills.OnPlayerTurnStarted(world, combat, events);
            KeeperSkills.OnPlayerTurnStarted(world, combat, events);
            KeeperSkills.OnPlayerTurnStarted(world, combat, events);
            Assert.That(player.Hp, Is.EqualTo(hp - 8));
            Assert.That(events.OfType<BurnTicked>().Count(), Is.EqualTo(2));
            Assert.That(world.PlayerBurnTurns, Is.EqualTo(0));
        }

        [Test]
        public void Burn_CanDefeatThePlayer()
        {
            var (world, _, player, combat) = Fight();
            world.PlayerBurnTurns = 1;
            player.Hp = 3;
            var events = new List<GameEvent>();
            KeeperSkills.OnPlayerTurnStarted(world, combat, events);
            Assert.That(player.Hp, Is.EqualTo(0));
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Defeat));
            Assert.That(events.OfType<PlayerDefeated>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void EndingThePlayerTurn_RunsTheShopkeeperSkills_ThroughTheSimulation()
        {
            var (world, keeper, player, combat) = Fight();
            world.Turn = TurnSide.Player;
            var simulation = new Simulation(world, combat);
            int hp = player.Hp;

            Assert.That(simulation.Execute(new EndTurnCommand(combat)), Is.True);

            Assert.That(world.KeeperStacks, Is.EqualTo(3));
            Assert.That(keeper.Hp, Is.LessThan(keeper.MaxHp));
            Assert.That(world.Round, Is.EqualTo(2));
            Assert.That(world.PlayerBurnTurns, Is.EqualTo(1), "the first burn already ticked at the start of the turn");
            Assert.That(player.Hp, Is.LessThan(hp - 4));
        }

        [Test]
        public void WithoutKeeperSettings_TheShopkeeperFightsLikeARifleman()
        {
            var (world, keeper, player, combat) = Fight();
            combat.Keeper = null;
            Assert.That(KeeperSkills.Uses(combat, keeper), Is.False);
            Assert.That(KeeperSkills.BasicPercentFor(combat, keeper), Is.EqualTo(100));
            world.KeeperStacks = 3;
            Assert.That(KeeperSkills.DamageTakenReduction(world, combat, keeper), Is.EqualTo(0));
            Assert.That(CombatRules.Preview(world, combat, keeper, keeper.Position, player).Damage, Is.EqualTo(6));
        }

        [Test]
        public void CombatEnd_ClearsSkillState_AndTheSnapshotKeepsIt()
        {
            var (world, keeper, _, combat) = Fight();
            world.KeeperStacks = 4;
            world.KeeperHeartCooldown = 1;
            world.KeeperOrderCooldown = 2;
            world.KeeperRemold = 5;
            world.KeeperShield = 17;
            world.KeeperShieldTurns = 2;
            world.PlayerBurnTurns = 1;

            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.KeeperStacks, Is.EqualTo(4));
            Assert.That(restored.KeeperHeartCooldown, Is.EqualTo(1));
            Assert.That(restored.KeeperOrderCooldown, Is.EqualTo(2));
            Assert.That(restored.KeeperRemold, Is.EqualTo(5));
            Assert.That(restored.KeeperShield, Is.EqualTo(17));
            Assert.That(restored.KeeperShieldTurns, Is.EqualTo(2));
            Assert.That(restored.PlayerBurnTurns, Is.EqualTo(1));

            world.Turn = TurnSide.Player;
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new DebugDefeatCommand(keeper.Id)), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
            Assert.That(world.KeeperStacks + world.KeeperRemold + world.KeeperShield + world.PlayerBurnTurns, Is.EqualTo(0));
        }
    }
}
