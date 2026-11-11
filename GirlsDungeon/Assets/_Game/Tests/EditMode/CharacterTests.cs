using System;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>3-7 캐릭터 선택: 캐릭터 표(BALANCE 13번)가 새 판의 플레이어 능력치 · 시작 무기를 정한다.</summary>
    public class CharacterTests
    {
        private static World NewRun(int seed, CharacterKind character, int enemies = 0)
            => RunFactory.NewRun(seed, TestData.Map(), TestData.Combat(enemies), character);

        [Test]
        public void HeroA_StartsWithShotgunAndItsBodyStats()
        {
            var world = NewRun(7, CharacterKind.HeroA);
            var player = world.Player;
            var shotgun = TestData.Combat().WeaponFor(WeaponKind.Shotgun);

            Assert.That(world.PlayerCharacter, Is.EqualTo(CharacterKind.HeroA));
            Assert.That(world.PlayerWeapon, Is.EqualTo(WeaponKind.Shotgun));
            Assert.That(new[] { player.MaxHp, player.Hp, player.MaxStability, player.Stability, player.VisionRange, player.Defense, player.MoveRange },
                Is.EqualTo(new[] { 110, 110, 7, 7, 8, 5, 6 }));
            // 공격 · 사거리는 무기에서 온다 (Loadout.Apply).
            Assert.That(player.Attack, Is.EqualTo(shotgun.Attack));
            Assert.That(player.AttackRange, Is.EqualTo(shotgun.AttackRange));
        }

        [Test]
        public void HeroB_StartsWithSmgAndItsBodyStats()
        {
            var world = NewRun(7, CharacterKind.HeroB);
            var player = world.Player;

            Assert.That(world.PlayerCharacter, Is.EqualTo(CharacterKind.HeroB));
            Assert.That(world.PlayerWeapon, Is.EqualTo(WeaponKind.Smg));
            Assert.That(new[] { player.MaxHp, player.MaxStability, player.VisionRange, player.Defense, player.MoveRange },
                Is.EqualTo(new[] { 90, 5, 10, 3, 7 }));
            Assert.That(Loadout.ShotsPerTurn(world, TestData.Combat()), Is.EqualTo(2));
        }

        [Test]
        public void NoCharacter_KeepsThePlayerRowAndDefaultWeapon()
        {
            var world = TestData.NewRun(7);
            var row = TestData.Combat().StatsFor(UnitKind.Player);

            Assert.That(world.PlayerCharacter, Is.EqualTo(CharacterKind.None));
            Assert.That(world.PlayerWeapon, Is.EqualTo(TestData.Combat().StartWeapon));
            Assert.That(world.Player.MaxHp, Is.EqualTo(row.MaxHp));
            Assert.That(world.Player.MoveRange, Is.EqualTo(row.MoveRange));
        }

        [Test]
        public void WeaponMoveBonus_AppliesToTheCharacterMoveRange()
        {
            var combat = TestData.Combat();
            var world = NewRun(7, CharacterKind.HeroB);
            world.PlayerWeapon = WeaponKind.MachineGun;
            Loadout.Apply(world, combat);

            // HeroB 이동 7 + 기관총 −2 = 5 (유닛 표 6이 아니라 캐릭터 값이 기준).
            Assert.That(world.Player.MoveRange, Is.EqualTo(7 + combat.WeaponFor(WeaponKind.MachineGun).MoveRangeBonus));
        }

        [Test]
        public void SameSeed_GivesTheSameMapForEitherCharacter()
        {
            var a = NewRun(4242, CharacterKind.HeroA);
            var b = NewRun(4242, CharacterKind.HeroB);

            Assert.That(b.Map.StartCell, Is.EqualTo(a.Map.StartCell));
            Assert.That(b.Map.StairsCell, Is.EqualTo(a.Map.StairsCell));
            Assert.That(b.Map.CountWalkable(), Is.EqualTo(a.Map.CountWalkable()));
        }

        [Test]
        public void SameSeedAndCharacter_IsDeterministic()
        {
            var first = WorldSnapshot.Encode(NewRun(99, CharacterKind.HeroB, enemies: 4));
            var second = WorldSnapshot.Encode(NewRun(99, CharacterKind.HeroB, enemies: 4));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Snapshot_KeepsTheCharacter()
        {
            var world = NewRun(11, CharacterKind.HeroA, enemies: 3);
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);

            Assert.That(restored.PlayerCharacter, Is.EqualTo(CharacterKind.HeroA));
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
        }

        [Test]
        public void NextFloor_KeepsCharacterStatsAndWeapon()
        {
            var world = NewRun(5, CharacterKind.HeroA);
            RunFactory.NextFloor(world, TestData.Map(), TestData.Combat(), null);

            Assert.That(world.PlayerCharacter, Is.EqualTo(CharacterKind.HeroA));
            Assert.That(world.PlayerWeapon, Is.EqualTo(WeaponKind.Shotgun));
            Assert.That(world.Player.MaxHp, Is.EqualTo(110));
        }

        [Test]
        public void UnknownCharacter_IsRejected()
        {
            var combat = TestData.Combat();
            combat.Characters.RemoveAll(character => character.Kind == CharacterKind.HeroB);
            Assert.Throws<ArgumentException>(() => RunFactory.NewRun(1, TestData.Map(), combat, CharacterKind.HeroB));
        }

        [Test]
        public void CharacterWithMissingWeapon_FailsValidation()
        {
            var combat = TestData.Combat();
            combat.Characters[0].StartWeapon = (WeaponKind)200;
            Assert.Throws<ArgumentException>(() => combat.Validate());
        }
    }
}
