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
    /// BALANCE 12번 무기 · 보상 · 1-18 보급 상자 (결정 2026-11-10, 사용자):
    /// 무기 하나만 들고 능력치 + 규칙이 다르다, 전투에서 이기면 가끔 떨어짐(무기 30 · 구급상자 40 · 없음 30),
    /// 보급 상자는 층마다 2~3개 · 바로 옆에서 열기 · 항상 무기 50 / 구급상자 50, 구급상자 = 최대 체력 40% 회복.
    /// </summary>
    public class WeaponAndLootTests
    {
        private static World MakeWorld(GridPos player, params string[] rows)
        {
            var world = new World(1, new RandomStreams(1)) { Map = TestData.Parse(rows) };
            world.PlayerId = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Player), player).Id;
            world.Explored = Enumerable.Repeat(true, world.Map.Width * world.Map.Height).ToArray();
            return world;
        }

        private static Unit AddEnemy(World world, GridPos cell, UnitKind kind = UnitKind.Rifleman)
        {
            var enemy = world.AddUnit(TestData.Combat().StatsFor(kind), cell);
            world.Combatants.Add(enemy.Id);
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            world.Round = 1;
            return enemy;
        }

        private static void Equip(World world, WeaponKind weapon)
        {
            world.PlayerWeapon = weapon;
            Loadout.Apply(world, TestData.Combat());
        }

        // ---------- 무기 ----------

        [Test]
        public void NewRun_StartsWithTheAssaultRifle()
        {
            var world = TestData.NewRun(3);
            Assert.That(world.PlayerWeapon, Is.EqualTo(WeaponKind.AssaultRifle));
            Assert.That(world.Player.Attack, Is.EqualTo(10));
            Assert.That(world.Player.AttackRange, Is.EqualTo(8));
            Assert.That(world.Player.StabilityDamage, Is.EqualTo(2));
            Assert.That(world.Player.CritPercent, Is.EqualTo(15));
        }

        [Test]
        public void Loadout_CopiesTheWeaponStatsOntoThePlayer()
        {
            var world = MakeWorld(new GridPos(0, 0), "....");
            Equip(world, WeaponKind.Sniper);
            var player = world.Player;
            Assert.That(new[] { player.Attack, player.AttackRange, player.StabilityDamage, player.CritPercent, player.CritMultiplierPercent },
                Is.EqualTo(new[] { 13, 12, 1, 30, 150 }));
            Assert.That(player.MaxHp, Is.EqualTo(100), "the weapon does not touch hp");
            Assert.That(player.MoveRange, Is.EqualTo(6), "the weapon does not touch movement");
        }

        [Test]
        public void Swap_EquipsTheNewWeapon_AndLeavesTheOldOneOnTheSameCell()
        {
            var world = MakeWorld(new GridPos(1, 0), "....");
            var item = world.AddItem(ItemKind.Weapon, WeaponKind.Shotgun, new GridPos(1, 0));
            var simulation = new Simulation(world, TestData.Combat());

            Assert.That(simulation.Execute(new SwapWeaponCommand(item.Id, TestData.Combat())), Is.True);
            Assert.That(world.PlayerWeapon, Is.EqualTo(WeaponKind.Shotgun));
            Assert.That(world.Player.Attack, Is.EqualTo(14));
            Assert.That(world.FindItem(item.Id), Is.Null);
            var left = world.Items.Single();
            Assert.That(left.Weapon, Is.EqualTo(WeaponKind.AssaultRifle));
            Assert.That(left.Position, Is.EqualTo(new GridPos(1, 0)));
            Assert.That(left.Id, Is.GreaterThan(item.Id), "a new id");
        }

        [Test]
        public void Swap_NeedsTheWeaponUnderThePlayer()
        {
            var world = MakeWorld(new GridPos(0, 0), "....");
            var item = world.AddItem(ItemKind.Weapon, WeaponKind.Shotgun, new GridPos(2, 0));
            Assert.That(new Simulation(world, TestData.Combat()).Execute(new SwapWeaponCommand(item.Id, TestData.Combat())), Is.False);
            Assert.That(world.PlayerWeapon, Is.EqualTo(WeaponKind.AssaultRifle));
        }

        [Test]
        public void Shotgun_LosesFifteenPercentPerCellBeyondTwo()
        {
            var combat = TestData.Combat();
            foreach (var (distance, falloff) in new[] { (1, 0), (2, 0), (3, 15), (4, 30) })
            {
                var world = MakeWorld(new GridPos(0, 0), "........");
                Equip(world, WeaponKind.Shotgun);
                var enemy = AddEnemy(world, new GridPos(distance, 0));
                var preview = CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy);
                Assert.That(preview.CanAttack, Is.True, $"distance {distance}");
                Assert.That(preview.FalloffPercent, Is.EqualTo(falloff), $"distance {distance}");
                Assert.That(preview.Damage, Is.EqualTo(CombatRules.Damage(14, 2, 100, 0, 1, 100 - falloff)), $"distance {distance}");
            }
            Assert.That(CombatRules.Damage(14, 2, 100, 0, 1, 100), Is.EqualTo(12), "12.25");
            Assert.That(CombatRules.Damage(14, 2, 100, 0, 1, 85), Is.EqualTo(10), "10.41");
            Assert.That(CombatRules.Damage(14, 2, 100, 0, 1, 70), Is.EqualTo(9), "8.58");
        }

        [Test]
        public void Shotgun_FalloffCountsDiagonalsAsOneCell()
        {
            var world = MakeWorld(new GridPos(0, 0), "....", "....", "....", "....");
            Equip(world, WeaponKind.Shotgun);
            var enemy = AddEnemy(world, new GridPos(2, 2));
            Assert.That(CombatRules.Preview(world, TestData.Combat(), world.Player, world.Player.Position, enemy).FalloffPercent, Is.EqualTo(0));
        }

        [Test]
        public void Sniper_CannotShootAfterMoving()
        {
            var combat = TestData.Combat();
            var world = MakeWorld(new GridPos(0, 0), "..............");
            Equip(world, WeaponKind.Sniper);
            var enemy = AddEnemy(world, new GridPos(12, 0));
            var simulation = new Simulation(world, combat);

            Assert.That(CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy).CanAttack, Is.True, "before moving");
            Assert.That(CombatRules.Preview(world, combat, world.Player, new GridPos(1, 0), enemy).Block, Is.EqualTo(AttackBlock.MovedThisTurn),
                "previewing from another cell means moving first");

            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(1, 0), combat)), Is.True);
            Assert.That(CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy).Block, Is.EqualTo(AttackBlock.MovedThisTurn));
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.False);
            Assert.That(enemy.Hp, Is.EqualTo(enemy.MaxHp));
        }

        [Test]
        public void Sniper_OverwatchStillFiresInTheEnemyTurn()
        {
            var combat = TestData.Combat();
            var world = MakeWorld(new GridPos(0, 0), "..............");
            Equip(world, WeaponKind.Sniper);
            var enemy = AddEnemy(world, new GridPos(12, 0));
            world.PlayerMoveUsed = 1;
            world.Turn = TurnSide.Enemies;
            Assert.That(CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy).CanAttack, Is.True);
        }

        [Test]
        public void OtherWeapons_CanShootAfterMoving()
        {
            var combat = TestData.Combat();
            var world = MakeWorld(new GridPos(0, 0), "..........");
            var enemy = AddEnemy(world, new GridPos(8, 0));
            var simulation = new Simulation(world, combat);
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(1, 0), combat)), Is.True);
            Assert.That(CombatRules.Preview(world, combat, world.Player, world.Player.Position, enemy).CanAttack, Is.True);
        }

        [Test]
        public void Pistol_KeepsTheTurnAfterShooting_UntilTheCellsRunOut()
        {
            var combat = TestData.Combat();
            var world = MakeWorld(new GridPos(0, 0), "..........");
            Equip(world, WeaponKind.Pistol);
            var enemy = AddEnemy(world, new GridPos(5, 0));
            enemy.Guarding = true; // 맞으면 깨어나지만 상관없다 — 차례 넘김만 본다.
            var simulation = new Simulation(world, combat);

            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.True);
            Assert.That(enemy.Hp, Is.LessThan(enemy.MaxHp));
            Assert.That(world.Turn, Is.EqualTo(TurnSide.Player), "the pistol keeps the turn");
            Assert.That(world.Round, Is.EqualTo(1));
            Assert.That(Loadout.CanCombatMove(world, combat), Is.True);
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.False, "still one shot per turn");

            // 3-13: 쏜 뒤에도 남은 칸만큼 나눠 걷는다. 6칸 = 1 + 3 + 2, 다 쓰면 차례가 끝난다.
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(1, 0), combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(1), "5 cells are left");
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(4, 0), combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(1), "2 cells are left");
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.False, "still one shot per turn");
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(2, 0), combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(2), "the last cell after the shot ends the turn");
            Assert.That(world.Turn, Is.EqualTo(TurnSide.Player));
        }

        [Test]
        public void Pistol_MoveShootMove_AndAShotWithNoCellsLeftEndsTheTurn()
        {
            var combat = TestData.Combat();
            var world = MakeWorld(new GridPos(0, 0), "..........");
            Equip(world, WeaponKind.Pistol);
            var enemy = AddEnemy(world, new GridPos(6, 0));
            enemy.Guarding = true;
            var simulation = new Simulation(world, combat);

            // 걷고 → 쏘고 → 남은 칸을 또 걷는다 (3-13, 결정 2026-11-12 사용자).
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(1, 0), combat)), Is.True);
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(1), "5 cells are left, so the turn goes on");
            Assert.That(Loadout.CanCombatMove(world, combat), Is.True);
            Assert.That(simulation.Execute(new CombatMoveCommand(new GridPos(3, 0), combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(1));
            Assert.That(world.PlayerMoveUsed, Is.EqualTo(3));

            // 다음 상황: 이동 칸을 다 쓴 뒤에 쏘면 그 사격으로 차례가 끝난다. (두 발째에 쓰러져 전투가 끝나지 않게 체력을 채운다.)
            enemy.MaxHp = enemy.Hp = 60;
            world.PlayerActed = false;
            world.PlayerShots = 0;
            world.PlayerMoveUsed = world.Player.MoveRange;
            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(2));
        }

        [Test]
        public void AssaultRifle_ShotEndsTheTurn_AndBlocksMoving()
        {
            var combat = TestData.Combat();
            var world = MakeWorld(new GridPos(0, 0), "..........");
            var enemy = AddEnemy(world, new GridPos(5, 0));
            enemy.Guarding = true;
            var simulation = new Simulation(world, combat);

            Assert.That(simulation.Execute(new AttackCommand(enemy.Id, combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(2));
            world.PlayerActed = true;
            Assert.That(Loadout.CanCombatMove(world, combat), Is.False);
        }

        // ---------- 보급 상자 ----------

        [Test]
        public void SupplyCrates_TwoOrThreePerFloor_OnePerRoom_OutsideTheStartAndStairsRooms()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var world = TestData.NewRun(seed);
                var map = world.Map;
                var crates = new List<GridPos>();
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                        if (map.Get(new GridPos(x, y)) == TileType.SupplyCrate) crates.Add(new GridPos(x, y));

                Assert.That(crates.Count, Is.InRange(2, 3), $"seed {seed}");
                foreach (var crate in crates)
                {
                    var rooms = map.Rooms.Where(r => r.Contains(crate)).ToList();
                    Assert.That(rooms.Count, Is.EqualTo(1), $"seed {seed} {crate} is in a room");
                    Assert.That(rooms[0].Contains(map.StartCell) || rooms[0].Contains(map.StairsCell), Is.False, $"seed {seed} {crate}");
                    Assert.That(crates.Count(c => rooms[0].Contains(c)), Is.EqualTo(1), $"seed {seed}: one per room");
                    Assert.That(map.IsWalkable(crate), Is.False);
                    bool standable = false;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (map.IsWalkable(new GridPos(crate.X + dx, crate.Y + dy))
                                && GridNavigation.FindPath(map, map.StartCell, new GridPos(crate.X + dx, crate.Y + dy)).Count > 0) standable = true;
                    Assert.That(standable, Is.True, $"seed {seed} {crate}: a reachable cell next to it");
                }
            }
        }

        [Test]
        public void SupplyCrate_GivesLowCover_AndDoesNotBlockSight()
        {
            var map = TestData.Parse(".S.");
            Assert.That(Cover.InDirection(map, new GridPos(0, 0), new GridPos(1, 0)), Is.EqualTo(CoverLevel.Half));
            Assert.That(map.BlocksSight(new GridPos(1, 0)), Is.False);
            map.SetTile(new GridPos(1, 0), TileType.SupplyCrateOpened);
            Assert.That(Cover.InDirection(map, new GridPos(0, 0), new GridPos(1, 0)), Is.EqualTo(CoverLevel.Half), "opened crates still cover");
            Assert.That(map.IsWalkable(new GridPos(1, 0)), Is.False);
        }

        [Test]
        public void OpenCrate_OnlyFromAnAdjacentCell_AndOnlyOnce()
        {
            var combat = TestData.Combat();
            var world = MakeWorld(new GridPos(0, 0), "...S...", ".......");
            var simulation = new Simulation(world, combat);
            var crate = new GridPos(3, 0);

            Assert.That(simulation.Execute(new OpenSupplyCrateCommand(crate, combat)), Is.False, "two cells away");
            world.Player.Position = new GridPos(2, 1);
            Assert.That(simulation.Execute(new OpenSupplyCrateCommand(crate, combat)), Is.True, "diagonal neighbor");
            Assert.That(world.Map.Get(crate), Is.EqualTo(TileType.SupplyCrateOpened));

            var item = world.Items.Single();
            Assert.That(Loadout.CellDistance(item.Position, crate), Is.EqualTo(1), "next to the crate");
            Assert.That(item.Position, Is.Not.EqualTo(world.Player.Position), "not under the player");
            Assert.That(world.Map.IsWalkable(item.Position), Is.True);
            Assert.That(simulation.Execute(new OpenSupplyCrateCommand(crate, combat)), Is.False, "already open");
            Assert.That(world.Items.Count, Is.EqualTo(1));
        }

        [Test]
        public void OpenCrate_InCombat_UsesTheAction_AndEndsTheTurn()
        {
            var combat = TestData.Combat();
            var world = MakeWorld(new GridPos(2, 0), "...S......");
            var enemy = AddEnemy(world, new GridPos(9, 0));
            enemy.Guarding = true;
            var simulation = new Simulation(world, combat);

            Assert.That(simulation.Execute(new OpenSupplyCrateCommand(new GridPos(3, 0), combat)), Is.True);
            Assert.That(world.Round, Is.EqualTo(2));
        }

        [Test]
        public void CrateContents_AreAboutHalfWeapons_NeverTheWeaponInHand()
        {
            var combat = TestData.Combat();
            int weapons = 0, total = 0;
            for (int seed = 1; seed <= 400; seed++)
            {
                var world = new World(seed, new RandomStreams(seed)) { Map = TestData.Parse("S.") };
                world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 0)).Id;
                world.Map.SetTile(new GridPos(0, 0), TileType.SupplyCrate);
                // 바로 옆 빈 칸이 플레이어 칸뿐이면 발밑에 놓인다.
                Assert.That(Loot.OpenCrate(world, combat, new GridPos(0, 0), new List<GameEvent>()), Is.True);
                var item = world.Items.Single();
                total++;
                if (item.Kind == ItemKind.Weapon)
                {
                    weapons++;
                    Assert.That(item.Weapon, Is.Not.EqualTo(world.PlayerWeapon));
                }
            }
            Assert.That(weapons * 100.0 / total, Is.InRange(42.0, 58.0));
        }

        // ---------- 전투 보상 ----------

        [Test]
        public void WinningACombat_DropsWeapon30_Medkit40_Nothing30_AtTheLastKill()
        {
            var combat = TestData.Combat();
            int weapons = 0, medkits = 0, nothing = 0;
            for (int seed = 1; seed <= 600; seed++)
            {
                var world = new World(seed, new RandomStreams(seed)) { Map = TestData.Parse("........") };
                world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(0, 0)).Id;
                world.Explored = Enumerable.Repeat(true, 8).ToArray();
                var enemy = AddEnemy(world, new GridPos(5, 0));
                var simulation = new Simulation(world, combat);

                Assert.That(simulation.Execute(new DebugDefeatCommand(enemy.Id)), Is.True);
                Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));
                if (world.Items.Count == 0) { nothing++; continue; }
                var item = world.Items.Single();
                Assert.That(item.Position, Is.EqualTo(new GridPos(5, 0)), $"seed {seed}: on the enemy's cell");
                if (item.Kind == ItemKind.Weapon) weapons++;
                else medkits++;
            }
            Assert.That(weapons / 6.0, Is.InRange(24.0, 36.0));
            Assert.That(medkits / 6.0, Is.InRange(34.0, 46.0));
            Assert.That(nothing / 6.0, Is.InRange(24.0, 36.0));
        }

        [Test]
        public void EnemyFallingIntoAChasm_DropsOnTheNearestFreeFloor()
        {
            var world = MakeWorld(new GridPos(0, 0), "...X..");
            var cell = Loot.FreeCellNear(world, new GridPos(3, 0));
            Assert.That(cell.HasValue, Is.True);
            Assert.That(world.Map.IsWalkable(cell.Value), Is.True);
            Assert.That(Loadout.CellDistance(cell.Value, new GridPos(3, 0)), Is.EqualTo(1), "right next to where it fell");

            world.AddItem(ItemKind.Medkit, default, cell.Value);
            var next = Loot.FreeCellNear(world, new GridPos(3, 0));
            Assert.That(next, Is.Not.EqualTo(cell), "items do not stack on one cell");
        }

        [Test]
        public void NoCombat_NoDrop()
        {
            var world = MakeWorld(new GridPos(0, 0), "........");
            var enemy = world.AddUnit(TestData.Combat().StatsFor(UnitKind.Rifleman), new GridPos(6, 0));
            new Simulation(world, TestData.Combat()).Execute(new DebugDefeatCommand(enemy.Id));
            Assert.That(world.Items, Is.Empty, "killing outside combat is not a combat win");
        }

        // ---------- 줍기 ----------

        [Test]
        public void MedkitHeal_IsFortyPercent_RoundedHalfUp()
        {
            Assert.That(Loot.MedkitHeal(20, 40), Is.EqualTo(8));
            Assert.That(Loot.MedkitHeal(15, 40), Is.EqualTo(6));
            Assert.That(Loot.MedkitHeal(21, 40), Is.EqualTo(8), "8.4 → 8");
            Assert.That(Loot.MedkitHeal(24, 40), Is.EqualTo(10), "9.6 → 10");
        }

        [Test]
        public void SteppingOnAMedkit_Heals_UpToMax()
        {
            var world = MakeWorld(new GridPos(0, 0), "....");
            world.Player.Hp = 97;
            var medkit = world.AddItem(ItemKind.Medkit, default, new GridPos(1, 0));
            var events = new List<GameEvent>();
            var simulation = new Simulation(world, TestData.Combat());
            simulation.EventsPublished += published => events.AddRange(published);

            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(1, 0), 1)), Is.True);
            Assert.That(world.Player.Hp, Is.EqualTo(100), "capped at max");
            Assert.That(events.OfType<MedkitUsed>().Single().Healed, Is.EqualTo(3));
            Assert.That(world.FindItem(medkit.Id), Is.Null);
        }

        [Test]
        public void AtFullHp_TheMedkitStaysOnTheFloor()
        {
            var world = MakeWorld(new GridPos(0, 0), "....");
            var medkit = world.AddItem(ItemKind.Medkit, default, new GridPos(1, 0));
            new Simulation(world, TestData.Combat()).Execute(new MoveCommand(world.PlayerId, new GridPos(1, 0), 1));
            Assert.That(world.FindItem(medkit.Id), Is.Not.Null);
        }

        [Test]
        public void SteppingOnAWeapon_OffersIt_WithoutSwapping()
        {
            var world = MakeWorld(new GridPos(0, 0), "....");
            var weapon = world.AddItem(ItemKind.Weapon, WeaponKind.Pistol, new GridPos(1, 0));
            var events = new List<GameEvent>();
            var simulation = new Simulation(world, TestData.Combat());
            simulation.EventsPublished += published => events.AddRange(published);

            simulation.Execute(new MoveCommand(world.PlayerId, new GridPos(1, 0), 1));
            var found = events.OfType<WeaponFound>().Single();
            Assert.That(found.ItemId, Is.EqualTo(weapon.Id));
            Assert.That(found.Weapon, Is.EqualTo(WeaponKind.Pistol));
            Assert.That(world.PlayerWeapon, Is.EqualTo(WeaponKind.AssaultRifle), "the player chooses on the compare window");
            Assert.That(world.FindItem(weapon.Id), Is.Not.Null);
        }

        // ---------- 저장 · 층 ----------

        [Test]
        public void WeaponAndItems_SurviveTheSnapshot()
        {
            var world = TestData.NewRun(51);
            world.PlayerWeapon = WeaponKind.Sniper;
            var a = world.AddItem(ItemKind.Weapon, WeaponKind.Shotgun, world.Map.StartCell);
            var b = world.AddItem(ItemKind.Medkit, default, world.Map.StairsCell);

            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.PlayerWeapon, Is.EqualTo(WeaponKind.Sniper));
            Assert.That(restored.Items.Select(i => (i.Id, i.Kind, i.Weapon, i.Position)),
                Is.EqualTo(new[] { (a.Id, a.Kind, a.Weapon, a.Position), (b.Id, b.Kind, b.Weapon, b.Position) }));
            CollectionAssert.AreEqual(WorldSnapshot.Encode(world), WorldSnapshot.Encode(restored));
        }

        [Test]
        public void NextFloor_ClearsTheItems_AndKeepsTheWeapon()
        {
            var combat = TestData.Combat();
            var world = TestData.NewRun(52);
            world.PlayerWeapon = WeaponKind.Shotgun;
            Loadout.Apply(world, combat);
            world.AddItem(ItemKind.Medkit, default, world.Map.StartCell);

            RunFactory.NextFloor(world, TestData.Map(), combat, null);
            Assert.That(world.Items, Is.Empty);
            Assert.That(world.PlayerWeapon, Is.EqualTo(WeaponKind.Shotgun));
            Assert.That(world.Player.Attack, Is.EqualTo(14));
        }
    }
}
