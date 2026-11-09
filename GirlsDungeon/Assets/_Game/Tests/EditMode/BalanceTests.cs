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
        }

        [Test]
        public void Movement_MatchesBalanceTable2()
        {
            var balance = Load();
            Assert.That(balance.MoveCostTicks, Is.EqualTo(1));
            Assert.That(balance.StepSeconds, Is.EqualTo(0.14f).Within(0.0001f));
        }

        [Test]
        public void FixedSeed_IsOff()
        {
            Assert.That(Load().FixedSeed, Is.EqualTo(0));
        }
    }
}
