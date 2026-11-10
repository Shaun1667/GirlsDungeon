using GirlsDungeon.Game;
using NUnit.Framework;
using UnityEditor;

namespace GirlsDungeon.Tests
{
    /// <summary>실제 에셋 값이 BALANCE.md 표와 같은지 검산한다. 수치를 바꾸면 표와 이 테스트를 함께 고친다.</summary>
    public class BalanceTests
    {
        private static GameBalance Load()
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>("Assets/_Game/Data/GameBalance.asset");
            Assert.That(balance, Is.Not.Null);
            return balance;
        }

        [Test]
        public void MapSettings_MatchBalanceTable1()
        {
            var map = Load().CreateMapSettings();
            var expected = TestData.Map();

            Assert.That(map.Width, Is.EqualTo(expected.Width));
            Assert.That(map.Height, Is.EqualTo(expected.Height));
            Assert.That(map.MinRooms, Is.EqualTo(expected.MinRooms));
            Assert.That(map.MaxRooms, Is.EqualTo(expected.MaxRooms));
            Assert.That(map.MinRoomSize, Is.EqualTo(expected.MinRoomSize));
            Assert.That(map.MaxRoomSize, Is.EqualTo(expected.MaxRoomSize));
            Assert.That(map.RoomMargin, Is.EqualTo(expected.RoomMargin));
            Assert.That(map.RoomAttempts, Is.EqualTo(expected.RoomAttempts));
            Assert.That(map.LayoutAttempts, Is.EqualTo(expected.LayoutAttempts));
            Assert.That(map.CorridorWallCost, Is.EqualTo(expected.CorridorWallCost));
            Assert.That(map.ExtraConnections, Is.EqualTo(expected.ExtraConnections));
            Assert.That(map.MinCoverObjectsPerRoom, Is.EqualTo(expected.MinCoverObjectsPerRoom));
            Assert.That(map.MaxCoverObjectsPerRoom, Is.EqualTo(expected.MaxCoverObjectsPerRoom));
            Assert.That(map.PillarPercent, Is.EqualTo(expected.PillarPercent));
            Assert.That(map.MinLowWallsPerRoom, Is.EqualTo(expected.MinLowWallsPerRoom));
            Assert.That(map.MaxLowWallsPerRoom, Is.EqualTo(expected.MaxLowWallsPerRoom));
            Assert.That(map.MinLowWallLength, Is.EqualTo(expected.MinLowWallLength));
            Assert.That(map.MaxLowWallLength, Is.EqualTo(expected.MaxLowWallLength));
            Assert.That(map.DecorAttempts, Is.EqualTo(expected.DecorAttempts));
            Assert.That(map.ChasmRoomPercent, Is.EqualTo(expected.ChasmRoomPercent));
            Assert.That(map.MinChasmPercent, Is.EqualTo(expected.MinChasmPercent));
            Assert.That(map.MaxChasmPercent, Is.EqualTo(expected.MaxChasmPercent));
        }

        [Test]
        public void Movement_MatchesBalanceTable2()
        {
            var balance = Load();
            Assert.That(balance.MoveCostTicks, Is.EqualTo(1));
            Assert.That(balance.StepSeconds, Is.EqualTo(0.14f).Within(0.0001f));
        }

        [Test]
        public void Combat_MatchesBalanceTables4And5()
        {
            var combat = Load().CreateCombatSettings();
            var expected = TestData.Combat();

            foreach (var stats in expected.Units)
            {
                var actual = combat.StatsFor(stats.Kind);
                Assert.That(actual.MaxHp, Is.EqualTo(stats.MaxHp), stats.Kind + " hp");
                Assert.That(actual.MaxStability, Is.EqualTo(stats.MaxStability), stats.Kind + " stability");
                Assert.That(actual.VisionRange, Is.EqualTo(stats.VisionRange), stats.Kind + " vision");
            }
            Assert.That(combat.MinEnemiesPerFloor, Is.EqualTo(4));
            Assert.That(combat.MaxEnemiesPerFloor, Is.EqualTo(6));
            Assert.That(combat.EnemyMinDistanceFromStart, Is.EqualTo(expected.EnemyMinDistanceFromStart));
            Assert.That(combat.HalfCoverReductionPercent, Is.EqualTo(expected.HalfCoverReductionPercent));
            Assert.That(combat.FullCoverReductionPercent, Is.EqualTo(expected.FullCoverReductionPercent));
            Assert.That(combat.StabilityGuardPercent, Is.EqualTo(expected.StabilityGuardPercent));
            Assert.That(combat.MinDamage, Is.EqualTo(expected.MinDamage));
            Assert.That(combat.EndTurnCostTicks, Is.EqualTo(expected.EndTurnCostTicks));
            Assert.That(combat.GrenadeThrowRange, Is.EqualTo(expected.GrenadeThrowRange));
            Assert.That(combat.GrenadeRadius, Is.EqualTo(expected.GrenadeRadius));
            Assert.That(combat.GrenadeDamagePercent, Is.EqualTo(expected.GrenadeDamagePercent));
            Assert.That(combat.GrenadeStabilityDamage, Is.EqualTo(expected.GrenadeStabilityDamage));
            Assert.That(combat.GrenadeCooldownRounds, Is.EqualTo(expected.GrenadeCooldownRounds));
            Assert.That(combat.GuardPercent, Is.EqualTo(expected.GuardPercent));
            Assert.That(combat.GuardRadius, Is.EqualTo(expected.GuardRadius));
            Assert.That(combat.CrateDurability, Is.EqualTo(expected.CrateDurability));
            Assert.That(combat.LowWallDurability, Is.EqualTo(expected.LowWallDurability));
            Assert.That(combat.PushLength, Is.EqualTo(expected.PushLength));
            Assert.That(combat.PushWidth, Is.EqualTo(expected.PushWidth));
            Assert.That(combat.PushDamagePercent, Is.EqualTo(expected.PushDamagePercent));
            Assert.That(combat.PushCollisionDamage, Is.EqualTo(expected.PushCollisionDamage));
            Assert.That(combat.PushCooldownRounds, Is.EqualTo(expected.PushCooldownRounds));
            foreach (var stats in expected.Units)
            {
                var actual = combat.StatsFor(stats.Kind);
                Assert.That(actual.Attack, Is.EqualTo(stats.Attack), stats.Kind + " attack");
                Assert.That(actual.Defense, Is.EqualTo(stats.Defense), stats.Kind + " defense");
                Assert.That(actual.MoveRange, Is.EqualTo(stats.MoveRange), stats.Kind + " move");
                Assert.That(actual.AttackRange, Is.EqualTo(stats.AttackRange), stats.Kind + " range");
                Assert.That(actual.StabilityDamage, Is.EqualTo(stats.StabilityDamage), stats.Kind + " stability damage");
                Assert.That(actual.CritPercent, Is.EqualTo(stats.CritPercent), stats.Kind + " crit");
                Assert.That(actual.CritMultiplierPercent, Is.EqualTo(stats.CritMultiplierPercent), stats.Kind + " crit multiplier");
            }
        }

        [Test]
        public void UnitNameKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            foreach (var key in Load().UnitNameKeys())
                Assert.That(table.Contains(key), Is.True, key);
        }

        [Test]
        public void FixedSeed_IsOff()
        {
            Assert.That(Load().FixedSeed, Is.EqualTo(0));
        }
    }
}
