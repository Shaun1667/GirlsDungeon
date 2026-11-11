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
            Assert.That(map.MinSupplyCrates, Is.EqualTo(expected.MinSupplyCrates));
            Assert.That(map.MaxSupplyCrates, Is.EqualTo(expected.MaxSupplyCrates));
        }

        [Test]
        public void Weapons_MatchBalanceTable12()
        {
            var combat = Load().CreateCombatSettings();
            var expected = TestData.Combat();

            Assert.That(combat.StartWeapon, Is.EqualTo(expected.StartWeapon));
            Assert.That(combat.CombatDropWeaponPercent, Is.EqualTo(expected.CombatDropWeaponPercent));
            Assert.That(combat.CombatDropMedkitPercent, Is.EqualTo(expected.CombatDropMedkitPercent));
            Assert.That(combat.CrateWeaponPercent, Is.EqualTo(expected.CrateWeaponPercent));
            Assert.That(combat.MedkitHealPercent, Is.EqualTo(expected.MedkitHealPercent));
            Assert.That(combat.Weapons.Count, Is.EqualTo(expected.Weapons.Count));
            foreach (var weapon in expected.Weapons)
            {
                var actual = combat.WeaponFor(weapon.Kind);
                Assert.That(actual.Attack, Is.EqualTo(weapon.Attack), weapon.Kind + " attack");
                Assert.That(actual.AttackRange, Is.EqualTo(weapon.AttackRange), weapon.Kind + " range");
                Assert.That(actual.StabilityDamage, Is.EqualTo(weapon.StabilityDamage), weapon.Kind + " stability damage");
                Assert.That(actual.CritPercent, Is.EqualTo(weapon.CritPercent), weapon.Kind + " crit");
                Assert.That(actual.CritMultiplierPercent, Is.EqualTo(weapon.CritMultiplierPercent), weapon.Kind + " crit multiplier");
                Assert.That(actual.MoveAfterShot, Is.EqualTo(weapon.MoveAfterShot), weapon.Kind + " move after shot");
                Assert.That(actual.NoShotAfterMove, Is.EqualTo(weapon.NoShotAfterMove), weapon.Kind + " no shot after move");
                Assert.That(actual.FalloffStartCells, Is.EqualTo(weapon.FalloffStartCells), weapon.Kind + " falloff start");
                Assert.That(actual.FalloffPercentPerCell, Is.EqualTo(weapon.FalloffPercentPerCell), weapon.Kind + " falloff");
                Assert.That(actual.ExtraShotsPerTurn, Is.EqualTo(weapon.ExtraShotsPerTurn), weapon.Kind + " extra shots");
                Assert.That(actual.MoveRangeBonus, Is.EqualTo(weapon.MoveRangeBonus), weapon.Kind + " move bonus");
                Assert.That(actual.Melee, Is.EqualTo(weapon.Melee), weapon.Kind + " melee");
                Assert.That(actual.IgnoresCover, Is.EqualTo(weapon.IgnoresCover), weapon.Kind + " ignores cover");
            }

            // 처음 무기 = 지금까지의 플레이어 능력치 (4번 표) — 무기를 넣어도 첫 전투 검산(5-10)이 그대로다.
            var rifle = combat.WeaponFor(combat.StartWeapon);
            var player = combat.StatsFor(GirlsDungeon.Core.Sim.UnitKind.Player);
            Assert.That(new[] { rifle.Attack, rifle.AttackRange, rifle.StabilityDamage, rifle.CritPercent, rifle.CritMultiplierPercent },
                Is.EqualTo(new[] { player.Attack, player.AttackRange, player.StabilityDamage, player.CritPercent, player.CritMultiplierPercent }));
        }

        [Test]
        public void WeaponTextKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            foreach (var key in Load().WeaponTextKeys())
                Assert.That(table.Contains(key), Is.True, key);
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
            Assert.That(combat.AssaultPercent, Is.EqualTo(expected.AssaultPercent));
            Assert.That(combat.FallDamagePercent, Is.EqualTo(expected.FallDamagePercent));
            Assert.That(combat.FloorCount, Is.EqualTo(expected.FloorCount));
            Assert.That(combat.EnemiesPerFloorStep, Is.EqualTo(expected.EnemiesPerFloorStep));
            Assert.That(combat.AssaultPercentPerFloor, Is.EqualTo(expected.AssaultPercentPerFloor));
            Assert.That(combat.GuardPercentDropPerFloor, Is.EqualTo(expected.GuardPercentDropPerFloor));
            Assert.That(combat.EnemyStatPercentPerFloor, Is.EqualTo(expected.EnemyStatPercentPerFloor));
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
                Assert.That(actual.PushOnHit, Is.EqualTo(stats.PushOnHit), stats.Kind + " push on hit");
            }
        }

        [Test]
        public void Characters_MatchBalanceTable13()
        {
            var balance = Load();
            var combat = balance.CreateCombatSettings();
            var expected = TestData.Combat();

            Assert.That(combat.Characters.Count, Is.EqualTo(expected.Characters.Count));
            foreach (var character in expected.Characters)
            {
                var actual = combat.TryCharacter(character.Kind);
                Assert.That(actual, Is.Not.Null, character.Kind.ToString());
                Assert.That(actual.MaxHp, Is.EqualTo(character.MaxHp), character.Kind + " hp");
                Assert.That(actual.MaxStability, Is.EqualTo(character.MaxStability), character.Kind + " stability");
                Assert.That(actual.VisionRange, Is.EqualTo(character.VisionRange), character.Kind + " vision");
                Assert.That(actual.Defense, Is.EqualTo(character.Defense), character.Kind + " defense");
                Assert.That(actual.MoveRange, Is.EqualTo(character.MoveRange), character.Kind + " move");
                Assert.That(actual.StartWeapon, Is.EqualTo(character.StartWeapon), character.Kind + " start weapon");
            }
            // 사용자 결정 (2026-11-11): 첫 캐릭터는 산탄총, 두 번째는 기관단총으로 시작.
            Assert.That(combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroA).StartWeapon, Is.EqualTo(GirlsDungeon.Core.Sim.WeaponKind.Shotgun));
            Assert.That(combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroB).StartWeapon, Is.EqualTo(GirlsDungeon.Core.Sim.WeaponKind.Smg));
        }

        [Test]
        public void CharacterTextKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            foreach (var key in Load().CharacterNameKeys())
                Assert.That(table.Contains(key), Is.True, key);
            Assert.That(table.Contains("ui.select.title"), Is.True);
            Assert.That(table.Contains("ui.select.card"), Is.True);
        }

        [Test]
        public void UnitNameKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            foreach (var key in Load().UnitNameKeys())
                Assert.That(table.Contains(key), Is.True, key);
        }

        [Test]
        public void TurnPlayback_MatchesBalanceTable3()
        {
            var balance = Load();
            Assert.That(balance.EnemyTurnBannerSeconds, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(balance.AttackPauseSeconds, Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(balance.ActionPauseSeconds, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(balance.TracerSeconds, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(balance.DamagePopupSeconds, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(balance.ShakeSeconds, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(balance.ShakeDistance, Is.EqualTo(0.08f).Within(0.0001f));
        }

        [Test]
        public void FixedSeed_IsOff()
        {
            Assert.That(Load().FixedSeed, Is.EqualTo(0));
        }
    }
}
