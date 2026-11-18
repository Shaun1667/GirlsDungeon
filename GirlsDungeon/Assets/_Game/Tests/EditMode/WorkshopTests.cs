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
    /// 3-4c 파츠 작업방 (결정 2026-11-13 사용자: 층마다 1개 · 적 없음 · 끼운 파츠와 예비 파츠 · 원하는 옵션 / 세트를 골라 확률로 성공, 실패하면 무작위 ·
    /// 확률은 캐릭터마다, A는 낮음, BALANCE 19번). 배치 · 분해 · 옵션 · 세트 · 보관 · 끼우기 · 저장을 검사한다.
    /// </summary>
    public class WorkshopTests
    {
        /// <summary>작업대 (5,2) 옆 (4,2)에 플레이어. 탐험 중.</summary>
        private static (World world, CombatSettings combat) AtBench(CharacterKind character = CharacterKind.None, int seed = 1)
        {
            var world = new World(seed, new RandomStreams(seed))
            {
                Map = TestData.Parse(
                    "##########",
                    "#........#",
                    "#....W...#",
                    "#........#",
                    "##########"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(4, 2)).Id;
            world.PlayerCharacter = character;
            Loadout.Apply(world, combat);
            return (world, combat);
        }

        private static WeaponPart Part(WeaponKind weapon, int slot, PartGrade grade, PartSet set, params (PartStat stat, int value)[] bonuses)
            => new WeaponPart(weapon, slot, grade, bonuses.Select(b => new PartBonus(b.stat, b.value)).ToList(), set);

        [Test]
        public void EveryFloor_HasOneWorkbench_OutsideStartAndStairsRooms_WithNoEnemiesInThatRoom()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = RunFactory.NewRun(seed, TestData.Map(), TestData.Combat(6));
                var map = world.Map;
                Assert.That(map.CountTiles(TileType.Workbench), Is.EqualTo(1), $"seed {seed}");
                var bench = Enumerable.Range(0, map.Width * map.Height).Select(i => new GridPos(i % map.Width, i / map.Width))
                    .First(c => map.Get(c) == TileType.Workbench);
                var room = map.Rooms.First(r => r.Contains(bench));
                Assert.That(room.Contains(map.StartCell) || room.Contains(map.StairsCell), Is.False, $"seed {seed}");
                foreach (var unit in world.Units)
                    if (!unit.IsPlayer) Assert.That(room.Contains(unit.Position), Is.False, $"seed {seed}: enemy in the workshop room");
                Assert.That(map.IsWalkable(bench), Is.False);
                Assert.That(map.BlocksSight(bench), Is.False);
            }
        }

        [Test]
        public void Chances_ComeFromTheCharacter_HeroAIsLower()
        {
            var combat = TestData.Combat();
            var (a, _) = AtBench(CharacterKind.HeroA);
            var (b, _) = AtBench(CharacterKind.HeroB);
            var (none, _) = AtBench();
            Assert.That(Workshop.OptionPercent(a, combat), Is.EqualTo(55));
            Assert.That(Workshop.SetPercent(a, combat), Is.EqualTo(35));
            Assert.That(Workshop.OptionPercent(b, combat), Is.EqualTo(70));
            Assert.That(Workshop.SetPercent(b, combat), Is.EqualTo(50));
            Assert.That(Workshop.OptionPercent(none, combat), Is.EqualTo(combat.WorkshopOptionPercent));
            Assert.That(Workshop.OptionPercent(a, combat), Is.LessThan(Workshop.OptionPercent(b, combat)));
            Assert.That(Workshop.SetPercent(a, combat), Is.LessThan(Workshop.SetPercent(b, combat)));
        }

        [Test]
        public void OnlyNextToTheBench_AndOnlyWhileExploring()
        {
            var (world, combat) = AtBench();
            world.PartMaterials = 10;
            world.SpareParts.Add(Part(world.PlayerWeapon, 1, PartGrade.Military, PartSet.Decisive, (PartStat.Attack, 1)));
            Assert.That(Workshop.AtBench(world), Is.True);

            world.Player.Position = new GridPos(1, 1);
            Assert.That(Workshop.AtBench(world), Is.False);
            Assert.That(Workshop.Disassemble(world, combat, PartPlace.Spare, 0, new List<GameEvent>()), Is.False);

            world.Player.Position = new GridPos(4, 2);
            world.Mode = WorldMode.Combat;
            Assert.That(Workshop.ChangeSet(world, combat, PartPlace.Spare, 0, PartSet.Tactical, new List<GameEvent>()), Is.False);
            Assert.That(world.PartMaterials, Is.EqualTo(10), "nothing spent");
        }

        [Test]
        public void Disassemble_GivesMaterialsByGrade_AndUpdatesEquippedStats()
        {
            var (world, combat) = AtBench();
            int baseAttack = world.Player.Attack;
            world.PlayerParts[0] = Part(world.PlayerWeapon, 0, PartGrade.Precision, PartSet.None, (PartStat.Attack, 2));
            Loadout.Apply(world, combat);
            Assert.That(world.Player.Attack, Is.EqualTo(baseAttack + 2));
            world.SpareParts.Add(Part(WeaponKind.Blade, 2, PartGrade.Industrial, PartSet.None, (PartStat.MaxHp, 5)));

            var events = new List<GameEvent>();
            Assert.That(Workshop.Disassemble(world, combat, PartPlace.Equipped, 0, events), Is.True);
            Assert.That(world.PartMaterials, Is.EqualTo(3), "precision = 3");
            Assert.That(world.PlayerParts[0], Is.Null);
            Assert.That(world.Player.Attack, Is.EqualTo(baseAttack), "stats follow the removed part");
            Assert.That(Workshop.Disassemble(world, combat, PartPlace.Spare, 0, events), Is.True);
            Assert.That(world.PartMaterials, Is.EqualTo(4), "industrial = 1");
            Assert.That(world.SpareParts, Is.Empty);
            Assert.That(events.OfType<PartDisassembled>().Count(), Is.EqualTo(2));
        }

        [Test]
        public void ChangeOption_SuccessGivesTheTarget_FailureGivesARandomValidStat_CostsEitherWay()
        {
            var combat = TestData.Combat();
            int successes = 0, failures = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                var (world, _) = AtBench(CharacterKind.HeroB, seed);
                world.PartMaterials = combat.WorkshopOptionCost;
                world.SpareParts.Add(Part(WeaponKind.Blade, 2, PartGrade.Military, PartSet.Decisive, (PartStat.MaxHp, 5), (PartStat.Defense, 1)));
                var events = new List<GameEvent>();
                Assert.That(Workshop.ChangeOption(world, combat, PartPlace.Spare, 0, 0, PartStat.AttackPercent, events), Is.True);
                Assert.That(world.PartMaterials, Is.EqualTo(0));

                var part = world.SpareParts[0];
                var reworked = events.OfType<PartReworked>().Single();
                Assert.That(part.Bonuses[1].Stat, Is.EqualTo(PartStat.Defense), "other line untouched");
                Assert.That(part.Bonuses[0].Stat, Is.Not.EqualTo(PartStat.Defense), "no stat twice");
                Assert.That(part.Set, Is.EqualTo(PartSet.Decisive), "set untouched");
                var range = combat.PartRange(part.Bonuses[0].Stat);
                Assert.That(part.Bonuses[0].Value, Is.InRange(range.Min, range.Max));
                if (reworked.Success)
                {
                    successes++;
                    Assert.That(part.Bonuses[0].Stat, Is.EqualTo(PartStat.AttackPercent));
                }
                else failures++;
            }
            // 70% — 200번 중 대략 140번 (넉넉한 범위).
            Assert.That(successes, Is.InRange(110, 170));
            Assert.That(failures, Is.GreaterThan(0));
        }

        [Test]
        public void ChangeOption_RejectsStatsOutsideThePoolOrOnAnotherLine_AndNeedsMaterials()
        {
            var (world, combat) = AtBench();
            world.SpareParts.Add(Part(world.PlayerWeapon, 0, PartGrade.Military, PartSet.None, (PartStat.Attack, 1), (PartStat.CritPercent, 3)));
            world.PartMaterials = 1;
            Assert.That(Workshop.ChangeOption(world, combat, PartPlace.Spare, 0, 0, PartStat.AttackPercent, new List<GameEvent>()), Is.False, "not enough");
            world.PartMaterials = 10;
            Assert.That(Workshop.ChangeOption(world, combat, PartPlace.Spare, 0, 0, PartStat.MaxHp, new List<GameEvent>()), Is.False, "flat HP is not in the pool any more (3-16n)");
            world.SpareParts.Add(Part(world.PlayerWeapon, 1, PartGrade.Military, PartSet.None, (PartStat.AttackPercent, 5)));
            Assert.That(Workshop.ChangeOption(world, combat, PartPlace.Spare, 1, 0, PartStat.CritMultiplier, new List<GameEvent>()), Is.False, "crit damage is slot 1 only");
            Assert.That(Workshop.ChangeOption(world, combat, PartPlace.Spare, 0, 0, PartStat.CritPercent, new List<GameEvent>()), Is.False, "already on line 2");
            Assert.That(world.PartMaterials, Is.EqualTo(10));
        }

        [Test]
        public void ChangeSet_SuccessGivesTheTarget_OnlyOnPartsWithASet_HeroASucceedsLessOften()
        {
            var combat = TestData.Combat();
            int Successes(CharacterKind character)
            {
                int count = 0;
                for (int seed = 1; seed <= 300; seed++)
                {
                    var (world, _) = AtBench(character, seed);
                    world.PartMaterials = combat.WorkshopSetCost;
                    world.SpareParts.Add(Part(WeaponKind.Pistol, 3, PartGrade.Precision, PartSet.Decisive, (PartStat.Attack, 1)));
                    var events = new List<GameEvent>();
                    Assert.That(Workshop.ChangeSet(world, combat, PartPlace.Spare, 0, PartSet.Assault, events), Is.True);
                    Assert.That(world.PartMaterials, Is.EqualTo(0));
                    var reworked = events.OfType<PartReworked>().Single();
                    Assert.That(world.SpareParts[0].Set, Is.Not.EqualTo(PartSet.None));
                    Assert.That(world.SpareParts[0].Bonuses[0].Stat, Is.EqualTo(PartStat.Attack), "options untouched");
                    if (reworked.Success)
                    {
                        count++;
                        Assert.That(world.SpareParts[0].Set, Is.EqualTo(PartSet.Assault));
                    }
                }
                return count;
            }
            int heroA = Successes(CharacterKind.HeroA);
            int heroB = Successes(CharacterKind.HeroB);
            Assert.That(heroA, Is.InRange(70, 140), "35% of 300");
            Assert.That(heroB, Is.InRange(115, 185), "50% of 300");
            Assert.That(heroA, Is.LessThan(heroB));

            var (plain, _) = AtBench();
            plain.PartMaterials = 10;
            plain.SpareParts.Add(Part(WeaponKind.Pistol, 0, PartGrade.Precision, PartSet.None, (PartStat.Attack, 1)));
            Assert.That(Workshop.ChangeSet(plain, combat, PartPlace.Spare, 0, PartSet.Assault, new List<GameEvent>()), Is.False, "no set on slot 1");
        }

        [Test]
        public void Store_PutsTheFloorPartInSpare_UpToTheLimit()
        {
            var (world, combat) = AtBench();
            world.Player.Position = new GridPos(2, 1);
            for (int i = 0; i < combat.SpareSlots; i++)
            {
                var item = world.AddPartItem(Part(WeaponKind.Sniper, 1, PartGrade.Industrial, PartSet.None, (PartStat.Attack, 1)), world.Player.Position);
                Assert.That(Workshop.Store(world, combat, item.Id, new List<GameEvent>()), Is.True);
            }
            var extra = world.AddPartItem(Part(WeaponKind.Sniper, 1, PartGrade.Industrial, PartSet.None, (PartStat.Attack, 1)), world.Player.Position);
            Assert.That(Workshop.Store(world, combat, extra.Id, new List<GameEvent>()), Is.False, "full");
            Assert.That(world.SpareParts.Count, Is.EqualTo(combat.SpareSlots));
            Assert.That(world.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void EquipSpare_SwapsWithTheEquippedPart_AndRefusesOtherWeapons()
        {
            var (world, combat) = AtBench();
            int baseAttack = world.Player.Attack;
            var old = Part(world.PlayerWeapon, 2, PartGrade.Industrial, PartSet.None, (PartStat.Defense, 1));
            world.PlayerParts[2] = old;
            world.SpareParts.Add(Part(world.PlayerWeapon, 2, PartGrade.Military, PartSet.Tactical, (PartStat.Attack, 2)));
            world.SpareParts.Add(Part(WeaponKind.Blade, 2, PartGrade.Military, PartSet.Tactical, (PartStat.Attack, 2)));
            Loadout.Apply(world, combat);

            Assert.That(Workshop.EquipSpare(world, combat, 1, new List<GameEvent>()), Is.False, "other weapon");
            Assert.That(Workshop.EquipSpare(world, combat, 0, new List<GameEvent>()), Is.True);
            Assert.That(world.PlayerParts[2].Set, Is.EqualTo(PartSet.Tactical));
            Assert.That(world.SpareParts[0], Is.SameAs(old), "old part goes to the same spare slot");
            Assert.That(world.Player.Attack, Is.EqualTo(baseAttack + 2));
        }

        [Test]
        public void SameSeed_SameWorkshopResults()
        {
            var combat = TestData.Combat();
            string Run()
            {
                var (world, _) = AtBench(CharacterKind.HeroA, 7);
                world.PartMaterials = 20;
                world.SpareParts.Add(Part(WeaponKind.Smg, 1, PartGrade.Military, PartSet.Decisive, (PartStat.Attack, 1), (PartStat.MaxHp, 5)));
                var events = new List<GameEvent>();
                Workshop.ChangeOption(world, combat, PartPlace.Spare, 0, 0, PartStat.CritPercent, events);
                Workshop.ChangeSet(world, combat, PartPlace.Spare, 0, PartSet.DualCounter, events);
                var part = world.SpareParts[0];
                return string.Join(",", part.Bonuses.Select(b => $"{b.Stat}{b.Value}")) + part.Set;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        [Test]
        public void Snapshot_KeepsMaterialsAndSpareParts()
        {
            var (world, combat) = AtBench();
            world.PartMaterials = 9;
            world.SpareParts.Add(Part(WeaponKind.Smg, 1, PartGrade.Military, PartSet.Decisive, (PartStat.Attack, 1)));
            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
            Assert.That(restored.PartMaterials, Is.EqualTo(9));
            Assert.That(restored.SpareParts.Count, Is.EqualTo(1));
            Assert.That(restored.SpareParts[0].Set, Is.EqualTo(PartSet.Decisive));
            Assert.That(restored.Map.Get(new GridPos(5, 2)), Is.EqualTo(TileType.Workbench));
        }
    }
}
