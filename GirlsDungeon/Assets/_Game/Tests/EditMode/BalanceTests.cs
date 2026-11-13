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
            Assert.That(combat.SonicRange, Is.EqualTo(expected.SonicRange));
            Assert.That(combat.SonicDamagePercent, Is.EqualTo(expected.SonicDamagePercent));
            Assert.That(combat.SonicStabilityDamage, Is.EqualTo(expected.SonicStabilityDamage));
            Assert.That(combat.SonicCooldownRounds, Is.EqualTo(expected.SonicCooldownRounds));
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
                Assert.That(actual.Skills, Is.EqualTo(character.Skills), character.Kind + " skill slots");
            }
            // 사용자 결정 (2026-11-11): 첫 캐릭터는 산탄총, 두 번째는 기관단총으로 시작.
            Assert.That(combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroA).StartWeapon, Is.EqualTo(GirlsDungeon.Core.Sim.WeaponKind.Shotgun));
            Assert.That(combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroB).StartWeapon, Is.EqualTo(GirlsDungeon.Core.Sim.WeaponKind.Smg));
            // 사용자 결정 (2026-11-11): 세 번째 캐릭터는 돌격소총으로 시작.
            Assert.That(combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroC).StartWeapon, Is.EqualTo(GirlsDungeon.Core.Sim.WeaponKind.AssaultRifle));
        }

        [Test]
        public void Parts_MatchBalanceTable17()
        {
            var combat = Load().CreateCombatSettings();
            var expected = TestData.Combat();

            Assert.That(combat.CrateWeaponPercent, Is.EqualTo(expected.CrateWeaponPercent));
            Assert.That(combat.CratePartPercent, Is.EqualTo(expected.CratePartPercent));
            Assert.That(combat.CombatDropPartPercent, Is.EqualTo(expected.CombatDropPartPercent));
            Assert.That(combat.PartGradePercents, Is.EqualTo(expected.PartGradePercents));
            Assert.That(combat.PartPrimaryStatCounts, Is.EqualTo(expected.PartPrimaryStatCounts));
            Assert.That(combat.PartSecondaryStatCounts, Is.EqualTo(expected.PartSecondaryStatCounts));
            Assert.That(combat.PartPrimaryStats, Is.EqualTo(expected.PartPrimaryStats));
            Assert.That(combat.PartSecondaryStats, Is.EqualTo(expected.PartSecondaryStats));
            Assert.That(combat.PartStatRanges.Count, Is.EqualTo(expected.PartStatRanges.Count));
            foreach (var range in expected.PartStatRanges)
            {
                var actual = combat.PartRange(range.Stat);
                Assert.That(new[] { actual.Min, actual.Max }, Is.EqualTo(new[] { range.Min, range.Max }), range.Stat.ToString());
            }
            // 사용자 결정 등급 표 (TASKS 3-4): 1번 칸 2 · 3 · 4개, 2 · 3 · 4번 칸 1 · 2 · 3개.
            Assert.That(combat.PartPrimaryStatCounts, Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(combat.PartSecondaryStatCounts, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void PartSets_MatchBalanceTable18()
        {
            var combat = Load().CreateCombatSettings();
            // 결정 2026-11-13 사용자: 같은 세트 3개 · 받은 퍼센트 그대로.
            Assert.That(combat.SetPiecesRequired, Is.EqualTo(3));
            Assert.That(combat.SetChainPercentPerStack, Is.EqualTo(5));
            Assert.That(combat.SetChainMaxStacks, Is.EqualTo(4));
            Assert.That(combat.SetTwoSidedPercent, Is.EqualTo(10));
            Assert.That(combat.SetFracturePercent, Is.EqualTo(15));
            Assert.That(combat.SetEmergencyStability, Is.EqualTo(2));
            Assert.That(combat.SetBulwarkPercent, Is.EqualTo(20));
            Assert.That(combat.SetBulwarkTurns, Is.EqualTo(2));
            Assert.That(combat.SetAmbushPercent, Is.EqualTo(25));
            Assert.That(combat.SetAssaultPercent, Is.EqualTo(12));
            Assert.That(combat.SetAssaultMeleePercent, Is.EqualTo(24));
            // 검산: 백병 강습 + 도검 = 1.36배, 연쇄 가속 최대 = +20%.
            Assert.That(combat.SetAssaultPercent + combat.SetAssaultMeleePercent, Is.EqualTo(36));
            Assert.That(combat.SetChainPercentPerStack * combat.SetChainMaxStacks, Is.EqualTo(20));
        }

        [Test]
        public void Workshop_MatchesBalanceTable19()
        {
            var balance = Load();
            var combat = balance.CreateCombatSettings();
            Assert.That(balance.CreateMapSettings().WorkshopsPerFloor, Is.EqualTo(1), "1-19");
            Assert.That(combat.WorkshopOptionPercent, Is.EqualTo(70));
            Assert.That(combat.WorkshopSetPercent, Is.EqualTo(50));
            Assert.That(combat.WorkshopOptionCost, Is.EqualTo(2));
            Assert.That(combat.WorkshopSetCost, Is.EqualTo(3));
            Assert.That(combat.DisassembleMaterials, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(combat.SpareSlots, Is.EqualTo(4));
            var heroA = combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroA);
            Assert.That(new[] { heroA.WorkshopOptionPercent, heroA.WorkshopSetPercent }, Is.EqualTo(new[] { 55, 35 }));
            foreach (var kind in new[] { GirlsDungeon.Core.Sim.CharacterKind.HeroB, GirlsDungeon.Core.Sim.CharacterKind.HeroC })
            {
                var character = combat.TryCharacter(kind);
                Assert.That(new[] { character.WorkshopOptionPercent, character.WorkshopSetPercent }, Is.EqualTo(new[] { 70, 50 }), kind.ToString());
                // 사용자 결정: A는 성공 확률이 낮다.
                Assert.That(heroA.WorkshopOptionPercent, Is.LessThan(character.WorkshopOptionPercent));
                Assert.That(heroA.WorkshopSetPercent, Is.LessThan(character.WorkshopSetPercent));
            }
            // 검산: 정밀 파츠 하나 분해(3)로 세트 바꾸기 1번(3) 또는 옵션 바꾸기 1번(2).
            Assert.That(combat.DisassembleMaterials[2], Is.EqualTo(combat.WorkshopSetCost));
        }

        [Test]
        public void Doors_MatchBalanceTable20()
        {
            var combat = Load().CreateCombatSettings();
            Assert.That(combat.CombatDropKeyPercent, Is.EqualTo(10));
            // 사용자 결정: 캐릭터 A만 힘으로 열기 70%.
            Assert.That(combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroA).DoorForcePercent, Is.EqualTo(70));
            Assert.That(combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroB).DoorForcePercent, Is.EqualTo(0));
            Assert.That(combat.TryCharacter(GirlsDungeon.Core.Sim.CharacterKind.HeroC).DoorForcePercent, Is.EqualTo(0));
        }

        [Test]
        public void SpecialRooms_MatchBalanceTable1_21To1_24()
        {
            var map = Load().CreateMapSettings();
            Assert.That(map.RandomSpecialRoomsPerFloor, Is.EqualTo(2), "1-21");
            Assert.That(map.MinNormalRooms, Is.EqualTo(2), "1-22");
            // 1-23: 보상이 큰 방만 잠근다 (사용자 결정, 종류는 Claude 정함).
            Assert.That(map.LockedSpecialRooms, Is.EquivalentTo(new[]
            {
                GirlsDungeon.Core.Map.SpecialRoomKind.Workshop, GirlsDungeon.Core.Map.SpecialRoomKind.Treasure,
                GirlsDungeon.Core.Map.SpecialRoomKind.SetGallery, GirlsDungeon.Core.Map.SpecialRoomKind.Altar,
            }));
            // 1-24: 방 내용이 생기면 하나씩 넣는다 — 보물방은 늘 (3-14b).
            Assert.That(map.AlwaysSpecialRooms, Is.EqualTo(new[] { GirlsDungeon.Core.Map.SpecialRoomKind.Treasure }));
            Assert.That(map.RandomSpecialRooms, Is.EqualTo(new[] { GirlsDungeon.Core.Map.SpecialRoomKind.Storehouse, GirlsDungeon.Core.Map.SpecialRoomKind.Repair, GirlsDungeon.Core.Map.SpecialRoomKind.Observatory }), "3-14c ~ 3-14e");
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            Assert.That(table.Contains("ui.observatory.log_used"), Is.True);
        }

        [Test]
        public void Treasure_MatchesBalanceTable21()
        {
            var combat = Load().CreateCombatSettings();
            Assert.That(combat.TreasurePartGrade, Is.EqualTo(GirlsDungeon.Core.Sim.PartGrade.Precision));
            Assert.That(combat.TreasureWeaponPercent, Is.EqualTo(50));
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            Assert.That(table.Contains("ui.treasure.log_open"), Is.True);
        }

        [Test]
        public void Storehouse_MatchesBalanceTable22()
        {
            var combat = Load().CreateCombatSettings();
            Assert.That(combat.StorehouseMaterials, Is.EqualTo(6));
            Assert.That(combat.StorehouseMedkits, Is.EqualTo(2));
            // 검산: 재료 6 = 정밀 파츠 2개를 분해한 만큼 (19-4).
            Assert.That(combat.StorehouseMaterials, Is.EqualTo(2 * combat.DisassembleMaterials[2]));
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            Assert.That(table.Contains("ui.storehouse.log_open"), Is.True);
        }

        [Test]
        public void Repair_MatchesBalanceTable23()
        {
            var combat = Load().CreateCombatSettings();
            Assert.That(combat.RepairHealPercent, Is.EqualTo(100));
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            Assert.That(table.Contains("ui.repair.log_used"), Is.True);
        }

        [Test]
        public void RoomNameKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            foreach (GirlsDungeon.Core.Map.SpecialRoomKind kind in System.Enum.GetValues(typeof(GirlsDungeon.Core.Map.SpecialRoomKind)))
            {
                if (kind == GirlsDungeon.Core.Map.SpecialRoomKind.None) continue;
                string key = GirlsDungeon.Game.RoomLabelsView.NameKey(kind);
                Assert.That(table.Contains(key), Is.True, key);
            }
        }

        [Test]
        public void DoorTextKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            foreach (var key in new[]
            {
                "ui.hud.keys", "ui.door.title", "ui.door.force_ready", "ui.door.force_used", "ui.door.no_key", "ui.door.force_failed",
                "ui.door.use_key", "ui.door.use_force", "ui.door.close", "ui.door.log_key", "ui.door.log_key_dropped",
                "ui.door.log_opened", "ui.door.log_forced", "ui.door.log_force_failed",
            })
                Assert.That(table.Contains(key), Is.True, key);
        }

        [Test]
        public void WorkshopTextKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            foreach (var key in new[]
            {
                "ui.part.store", "ui.loot.log_part_stored", "ui.hud.materials", "ui.workshop.header", "ui.workshop.no_parts",
                "ui.workshop.part_equipped", "ui.workshop.part_spare", "ui.workshop.line", "ui.workshop.line_selected", "ui.workshop.set_line",
                "ui.workshop.no_set", "ui.workshop.disassemble_hint", "ui.workshop.mode_option", "ui.workshop.mode_set", "ui.workshop.mode_set_none",
                "ui.workshop.result_option_success", "ui.workshop.result_option_fail", "ui.workshop.result_set_success", "ui.workshop.result_set_fail",
                "ui.workshop.result_disassembled", "ui.workshop.result_equipped", "ui.workshop.result_not_enough", "ui.workshop.result_not_fit",
                "ui.workshop.log_disassembled", "ui.workshop.log_success", "ui.workshop.log_fail",
            })
                Assert.That(table.Contains(key), Is.True, key);
            foreach (var name in new[] { "attack", "range", "crit", "crit_multiplier", "stability_damage", "hp", "defense", "move", "stability" })
                Assert.That(table.Contains("part.stat_name." + name), Is.True, name);
        }

        [Test]
        public void PartSetTextKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            foreach (var name in new[] { "chain", "two_sided", "fracture", "emergency", "bulwark", "ambush", "assault" })
            {
                Assert.That(table.Contains("part.set." + name + ".name"), Is.True, name);
                Assert.That(table.Contains("part.set." + name + ".rule"), Is.True, name);
            }
            foreach (var key in new[] { "ui.part.set", "ui.part.summary_set", "ui.hud.set", "ui.hud.set_chain", "ui.hud.set_bulwark" })
                Assert.That(table.Contains(key), Is.True, key);
        }

        [Test]
        public void PartTextKeys_ExistInTheTextTable()
        {
            var table = GirlsDungeon.Core.Localization.TextTable.Parse(System.IO.File.ReadAllText("Assets/_Game/Resources/Text/text_table.csv"));
            var balance = Load();
            foreach (var key in balance.PartSlotKeys()) Assert.That(table.Contains(key), Is.True, key);
            foreach (var weapon in new[] { GirlsDungeon.Core.Sim.WeaponKind.AssaultRifle, GirlsDungeon.Core.Sim.WeaponKind.Blade })
                for (int slot = 0; slot < 4; slot++) Assert.That(balance.PartSlotKey(weapon, slot), Is.Not.Null, weapon + " slot " + slot);
            foreach (var key in new[] { "part.grade.industrial", "part.grade.military", "part.grade.precision", "ui.part.compare", "ui.part.summary",
                "ui.part.empty", "ui.part.not_fit", "ui.part.on_weapon", "ui.loot.log_part_dropped", "ui.loot.log_part_equipped",
                "part.stat.attack", "part.stat.range", "part.stat.crit", "part.stat.crit_multiplier", "part.stat.stability_damage",
                "part.stat.hp", "part.stat.defense", "part.stat.move", "part.stat.stability" })
                Assert.That(table.Contains(key), Is.True, key);
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
        public void CameraZoomAndDrag_MatchesBalanceTable3()
        {
            var balance = Load();
            Assert.That(balance.CameraMinDistance, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(balance.CameraMaxDistance, Is.EqualTo(28f).Within(0.0001f));
            Assert.That(balance.CameraZoomStep, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(balance.CameraZoomSharpness, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(balance.CameraDragThresholdPixels, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(balance.CameraMaxPanDistance, Is.EqualTo(20f).Within(0.0001f));
            // 시작 거리는 확대 · 축소 범위 안에 있어야 한다.
            Assert.That(balance.CameraDistance, Is.InRange(balance.CameraMinDistance, balance.CameraMaxDistance));
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
