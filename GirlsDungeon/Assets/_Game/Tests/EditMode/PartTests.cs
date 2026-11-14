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
    /// 3-4 무기 파츠 첫 단계 (결정 2026-11-12 사용자: 파츠 끼우기만 · 몸 능력치 포함 · 파츠는 무기에 붙어 다님).
    /// 만들기 · 끼우기 · 능력치 합산(Loadout.Apply) · 무기 교체 · 드롭 · 저장을 검사한다.
    /// </summary>
    public class PartTests
    {
        private static WeaponPart MakePart(WeaponKind weapon, int slot, params (PartStat stat, int value)[] bonuses)
            => new WeaponPart(weapon, slot, PartGrade.Military, bonuses.Select(b => new PartBonus(b.stat, b.value)).ToList());

        [Test]
        public void Generate_FollowsGradeCountsPoolsAndRanges()
        {
            var combat = TestData.Combat();
            var random = new RandomStreams(3).Get(RandomPurpose.Item);
            var seenGrades = new HashSet<PartGrade>();
            for (int i = 0; i < 300; i++)
            {
                var part = Parts.Generate(combat, WeaponKind.Shotgun, random);
                seenGrades.Add(part.Grade);
                Assert.That(part.Weapon, Is.EqualTo(WeaponKind.Shotgun));
                Assert.That(part.Slot, Is.InRange(0, 3));
                var counts = part.Slot == 0 ? combat.PartPrimaryStatCounts : combat.PartSecondaryStatCounts;
                var pool = part.Slot == 0 ? combat.PartPrimaryStats : combat.PartSecondaryStats;
                Assert.That(part.Bonuses.Count, Is.EqualTo(counts[(int)part.Grade]));
                Assert.That(part.Bonuses.Select(b => b.Stat).Distinct().Count(), Is.EqualTo(part.Bonuses.Count), "no stat twice");
                foreach (var bonus in part.Bonuses)
                {
                    Assert.That(pool, Does.Contain(bonus.Stat));
                    var range = combat.PartRange(bonus.Stat);
                    Assert.That(bonus.Value, Is.InRange(range.Min, range.Max));
                }
            }
            Assert.That(seenGrades.Count, Is.EqualTo(3), "all three grades appear");
        }

        [Test]
        public void Generate_SameSeed_SameParts()
        {
            var combat = TestData.Combat();
            var a = Parts.Generate(combat, WeaponKind.Smg, new RandomStreams(9).Get(RandomPurpose.Item));
            var b = Parts.Generate(combat, WeaponKind.Smg, new RandomStreams(9).Get(RandomPurpose.Item));
            Assert.That(b.Slot, Is.EqualTo(a.Slot));
            Assert.That(b.Grade, Is.EqualTo(a.Grade));
            Assert.That(b.Bonuses.Select(x => (x.Stat, x.Value)), Is.EqualTo(a.Bonuses.Select(x => (x.Stat, x.Value))));
        }

        [Test]
        public void Equip_AddsWeaponStatsThroughLoadout()
        {
            var combat = TestData.Combat();
            var world = TestData.NewRun(1);
            world.Items.Clear(); // 1층 상점 진열(3-15)은 이 테스트와 상관없다.
            var rifle = combat.WeaponFor(world.PlayerWeapon);
            var item = world.AddPartItem(MakePart(world.PlayerWeapon, 0, (PartStat.Attack, 2), (PartStat.CritPercent, 5), (PartStat.AttackRange, 1)), world.Player.Position);

            var events = new List<GameEvent>();
            Assert.That(Parts.Equip(world, combat, item.Id, events), Is.True);
            Assert.That(world.Player.Attack, Is.EqualTo(rifle.Attack + 2));
            Assert.That(world.Player.CritPercent, Is.EqualTo(rifle.CritPercent + 5));
            Assert.That(world.Player.AttackRange, Is.EqualTo(rifle.AttackRange + 1));
            Assert.That(world.Items, Is.Empty);
            Assert.That(events.OfType<PartEquipped>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void Equip_BodyStats_RaiseAndLowerOnlyByTheChange()
        {
            var combat = TestData.Combat();
            var world = TestData.NewRun(1);
            world.Items.Clear(); // 1층 상점 진열(3-15)은 이 테스트와 상관없다.
            var player = world.Player;
            int baseHp = player.MaxHp, baseDefense = player.Defense, baseMove = player.MoveRange;
            player.Hp = baseHp - 20;

            var first = world.AddPartItem(MakePart(world.PlayerWeapon, 2, (PartStat.MaxHp, 8), (PartStat.Defense, 1), (PartStat.MoveRange, 1)), player.Position);
            Assert.That(Parts.Equip(world, combat, first.Id, new List<GameEvent>()), Is.True);
            Assert.That(player.MaxHp, Is.EqualTo(baseHp + 8));
            Assert.That(player.Hp, Is.EqualTo(baseHp - 20 + 8), "raising max HP also raises current HP by the same amount");
            Assert.That(player.Defense, Is.EqualTo(baseDefense + 1));
            Assert.That(player.MoveRange, Is.EqualTo(baseMove + 1));

            // 같은 칸에 몸 능력치 없는 파츠로 바꾸면 원래대로 (원래 파츠는 바닥에).
            var second = world.AddPartItem(MakePart(world.PlayerWeapon, 2, (PartStat.Attack, 1)), player.Position);
            Assert.That(Parts.Equip(world, combat, second.Id, new List<GameEvent>()), Is.True);
            Assert.That(player.MaxHp, Is.EqualTo(baseHp));
            Assert.That(player.Defense, Is.EqualTo(baseDefense));
            Assert.That(player.MoveRange, Is.EqualTo(baseMove));
            Assert.That(world.Items.Count, Is.EqualTo(1), "the replaced part lies on the floor");
            Assert.That(world.Items[0].Part.Sum(PartStat.MaxHp), Is.EqualTo(8));
        }

        [Test]
        public void Equip_PartForAnotherWeapon_IsRefused()
        {
            var combat = TestData.Combat();
            var world = TestData.NewRun(1);
            world.Items.Clear(); // 1층 상점 진열(3-15)은 이 테스트와 상관없다.
            var item = world.AddPartItem(MakePart(WeaponKind.Sniper, 1, (PartStat.Attack, 2)), world.Player.Position);
            Assert.That(world.PlayerWeapon, Is.Not.EqualTo(WeaponKind.Sniper));
            Assert.That(Parts.Equip(world, combat, item.Id, new List<GameEvent>()), Is.False);
            Assert.That(world.PlayerParts.All(p => p == null), Is.True);
        }

        [Test]
        public void SwapWeapon_PartsTravelWithTheWeapon()
        {
            var combat = TestData.Combat();
            var world = TestData.NewRun(1);
            world.Items.Clear(); // 1층 상점 진열(3-15)은 이 테스트와 상관없다.
            var start = world.PlayerWeapon;
            var partItem = world.AddPartItem(MakePart(start, 0, (PartStat.Attack, 2)), world.Player.Position);
            Assert.That(Parts.Equip(world, combat, partItem.Id, new List<GameEvent>()), Is.True);

            var pistol = world.AddItem(ItemKind.Weapon, WeaponKind.Pistol, world.Player.Position);
            Assert.That(Loot.Swap(world, combat, pistol.Id, new List<GameEvent>()), Is.True);
            Assert.That(world.PlayerParts.All(p => p == null), Is.True, "the pistol had no parts");
            Assert.That(world.Player.Attack, Is.EqualTo(combat.WeaponFor(WeaponKind.Pistol).Attack));
            var dropped = world.Items.Single(i => i.Kind == ItemKind.Weapon);
            Assert.That(dropped.Weapon, Is.EqualTo(start));
            Assert.That(dropped.Parts[0].Sum(PartStat.Attack), Is.EqualTo(2), "the dropped weapon keeps its part");

            Assert.That(Loot.Swap(world, combat, dropped.Id, new List<GameEvent>()), Is.True);
            Assert.That(world.PlayerParts[0], Is.Not.Null);
            Assert.That(world.Player.Attack, Is.EqualTo(combat.WeaponFor(start).Attack + 2));
        }

        [Test]
        public void CrateAndCombat_CanDropPartsForTheHeldWeapon()
        {
            var combat = TestData.Combat();
            int parts = 0;
            for (int seed = 1; seed <= 40 && parts == 0; seed++)
            {
                var world = TestData.NewRun(seed);
                var crate = new GridPos(1, 1);
                world.Map.SetTile(crate, TileType.SupplyCrate);
                var events = new List<GameEvent>();
                Assert.That(Loot.OpenCrate(world, combat, crate, events), Is.True);
                foreach (var item in world.Items.Where(i => i.Kind == ItemKind.Part))
                {
                    parts++;
                    Assert.That(item.Part.Weapon, Is.EqualTo(world.PlayerWeapon));
                }
            }
            Assert.That(parts, Is.GreaterThan(0), "a 60% crate part shows up within 40 crates");
        }

        [Test]
        public void Snapshot_KeepsPartsEverywhere()
        {
            var combat = TestData.Combat();
            var world = TestData.NewRun(2);
            world.Items.Clear(); // 1층 상점 진열(3-15)은 이 테스트와 상관없다.
            var equipped = world.AddPartItem(MakePart(world.PlayerWeapon, 3, (PartStat.MaxHp, 6), (PartStat.MaxStability, 1)), world.Player.Position);
            Assert.That(Parts.Equip(world, combat, equipped.Id, new List<GameEvent>()), Is.True);
            world.AddPartItem(MakePart(WeaponKind.Blade, 0, (PartStat.CritMultiplier, 15)), world.Player.Position);
            var weapon = world.AddItem(ItemKind.Weapon, WeaponKind.Sniper, world.Player.Position);
            weapon.Parts[1] = MakePart(WeaponKind.Sniper, 1, (PartStat.AttackRange, 1));

            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.PlayerParts[3].Sum(PartStat.MaxHp), Is.EqualTo(6));
            Assert.That(restored.PartAppliedMaxHp, Is.EqualTo(6));
            Assert.That(restored.Items.Single(i => i.Kind == ItemKind.Part).Part.Weapon, Is.EqualTo(WeaponKind.Blade));
            Assert.That(restored.Items.Single(i => i.Kind == ItemKind.Weapon).Parts[1].Sum(PartStat.AttackRange), Is.EqualTo(1));
        }
    }
}
