using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    public class RandomStreamsTests
    {
        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new RandomStreams(42).Get(RandomPurpose.Combat);
            var b = new RandomStreams(42).Get(RandomPurpose.Combat);
            for (int i = 0; i < 100; i++) Assert.That(a.Range(0, 1000), Is.EqualTo(b.Range(0, 1000)));
        }

        [Test]
        public void Purposes_AreIndependent()
        {
            var plain = new RandomStreams(42);
            var drawn = new RandomStreams(42);
            for (int i = 0; i < 50; i++) drawn.Get(RandomPurpose.Decor).Range(0, 100);

            for (int i = 0; i < 100; i++)
                Assert.That(drawn.Get(RandomPurpose.Map).Range(0, 1000), Is.EqualTo(plain.Get(RandomPurpose.Map).Range(0, 1000)));
        }

        [Test]
        public void Range_StaysInBounds()
        {
            var random = new SeededRandom(7UL);
            for (int i = 0; i < 10000; i++) Assert.That(random.Range(-3, 4), Is.InRange(-3, 3));
        }
    }

    public class DungeonGeneratorTests
    {
        private const int SeedCount = 300;

        private static FloorMap Generate(int seed) => TestData.NewRun(seed).Map;

        [Test]
        public void SameSeed_GivesSameMap()
        {
            Assert.That(WorldSnapshot.Encode(TestData.NewRun(5)),
                Is.EqualTo(WorldSnapshot.Encode(TestData.NewRun(5))));
        }

        [Test]
        public void EveryMap_MeetsTheRoomAndBorderRules()
        {
            var settings = TestData.Map();
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                Assert.That(map.Rooms.Count, Is.InRange(settings.MinRooms, settings.MaxRooms), $"seed {seed}");
                for (int i = 0; i < map.Width; i++)
                {
                    Assert.That(map.IsWalkable(new GridPos(i, 0)), Is.False, $"seed {seed}");
                    Assert.That(map.IsWalkable(new GridPos(i, map.Height - 1)), Is.False, $"seed {seed}");
                    Assert.That(map.IsWalkable(new GridPos(0, i)), Is.False, $"seed {seed}");
                    Assert.That(map.IsWalkable(new GridPos(map.Width - 1, i)), Is.False, $"seed {seed}");
                }
            }
        }

        [Test]
        public void EveryWalkableCell_IsReachableFromStart()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                // 특수 방은 잠긴 문 뒤에 있다 (3-4d) — 문을 모두 연 지도가 하나로 이어져 있어야 한다.
                TestData.OpenAllDoors(map);
                Assert.That(CountReachable(map), Is.EqualTo(map.CountWalkable()), $"seed {seed}");
            }
        }

        [Test]
        public void Stairs_AreSeparateFromStart_AndHaveAPath()
        {
            for (int seed = 1; seed <= SeedCount; seed++)
            {
                var map = Generate(seed);
                Assert.That(map.StairsCell, Is.Not.EqualTo(map.StartCell), $"seed {seed}");
                Assert.That(map.Get(map.StairsCell), Is.EqualTo(TileType.Stairs), $"seed {seed}");
                Assert.That(GridNavigation.FindPath(map, map.StartCell, map.StairsCell), Is.Not.Empty, $"seed {seed}");
            }
        }

        private static int CountReachable(FloorMap map)
        {
            var seen = new HashSet<GridPos> { map.StartCell };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(map.StartCell);
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var next = new GridPos(cell.X + dx, cell.Y + dy);
                        if (GridNavigation.CanStep(map, cell, next) && seen.Add(next)) frontier.Enqueue(next);
                    }
                }
            }
            return seen.Count;
        }
    }

    public class GridNavigationTests
    {
        private static FloorMap Parse(params string[] rows) => TestData.Parse(rows);

        [Test]
        public void Diagonal_IsAllowedInTheOpen()
        {
            var map = Parse("..", "..");
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), new GridPos(1, 1)), Is.True);
        }

        [Test]
        public void Diagonal_CannotCutAWallCorner()
        {
            var map = Parse(".#", "..");
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), new GridPos(1, 1)), Is.False);
        }

        [Test]
        public void CannotStepFarOrIntoWallOrInPlace()
        {
            var map = Parse("..#");
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), new GridPos(2, 0)), Is.False);
            Assert.That(GridNavigation.CanStep(map, new GridPos(1, 0), new GridPos(2, 0)), Is.False);
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), new GridPos(0, 0)), Is.False);
        }

        [Test]
        public void FindPath_PrefersDiagonalsAndExcludesStart()
        {
            var map = Parse("...", "...", "...");
            var path = GridNavigation.FindPath(map, new GridPos(0, 0), new GridPos(2, 2));
            CollectionAssert.AreEqual(new[] { new GridPos(1, 1), new GridPos(2, 2) }, path);
        }

        [Test]
        public void FindPath_ReturnsEmpty_WhenUnreachable()
        {
            var map = Parse(".#.");
            Assert.That(GridNavigation.FindPath(map, new GridPos(0, 0), new GridPos(2, 0)), Is.Empty);
        }

        [Test]
        public void FindPath_AvoidsBlockedCells()
        {
            var map = Parse("...", "...");
            var blocked = new GridPos(1, 0);
            var path = GridNavigation.FindPath(map, new GridPos(0, 0), new GridPos(2, 0), cell => cell == blocked);
            Assert.That(path, Has.No.Member(blocked));
            Assert.That(path.Last(), Is.EqualTo(new GridPos(2, 0)));
        }
    }

    public class SimulationTests
    {
        [Test]
        public void ValidMove_ChangesPositionAdvancesTickAndPublishesEvents()
        {
            var world = TestData.NewRun(9);
            var simulation = new Simulation(world);
            var published = new List<GameEvent>();
            simulation.EventsPublished += events => published.AddRange(events);

            var start = world.Player.Position;
            var target = FindOpenNeighbor(world.Map, start);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, target, 1)), Is.True);

            Assert.That(world.Player.Position, Is.EqualTo(target));
            Assert.That(world.Tick, Is.EqualTo(1));
            Assert.That(published.OfType<UnitMoved>().Single().From, Is.EqualTo(start));
            Assert.That(published.OfType<TickAdvanced>().Single().Tick, Is.EqualTo(1));
        }

        [Test]
        public void InvalidMove_ChangesNothing()
        {
            var world = TestData.NewRun(9);
            var simulation = new Simulation(world);
            var before = WorldSnapshot.Encode(world);
            var published = 0;
            simulation.EventsPublished += _ => published++;

            var far = new GridPos(world.Player.Position.X + 5, world.Player.Position.Y);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, far, 1)), Is.False);

            Assert.That(WorldSnapshot.Encode(world), Is.EqualTo(before));
            Assert.That(published, Is.EqualTo(0));
        }

        private static GridPos FindOpenNeighbor(FloorMap map, GridPos cell)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var next = new GridPos(cell.X + dx, cell.Y + dy);
                    if (GridNavigation.CanStep(map, cell, next)) return next;
                }
            }
            throw new AssertionException("Start cell has no open neighbor.");
        }
    }

    public class WorldSnapshotTests
    {
        [Test]
        public void RoundTrip_GivesSameBytes()
        {
            var world = TestData.NewRun(11);
            var simulation = new Simulation(world);
            var path = GridNavigation.FindPath(world.Map, world.Player.Position, world.Map.StairsCell);
            foreach (var step in path.Take(5)) Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, step, 1)), Is.True);
            world.Random.Get(RandomPurpose.Combat).Range(0, 100);

            var bytes = WorldSnapshot.Encode(world);
            Assert.That(WorldSnapshot.Encode(WorldSnapshot.Decode(bytes)), Is.EqualTo(bytes));
        }

        [Test]
        public void RestoredRandom_ContinuesTheSameSequence()
        {
            var world = TestData.NewRun(11);
            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));

            for (int i = 0; i < 100; i++)
                Assert.That(restored.Random.Get(RandomPurpose.Combat).Range(0, 1000),
                    Is.EqualTo(world.Random.Get(RandomPurpose.Combat).Range(0, 1000)));
        }

        [Test]
        public void Decode_RejectsUnknownVersion()
        {
            var bytes = WorldSnapshot.Encode(TestData.NewRun(1));
            bytes[0] = 0xFF;
            Assert.Throws<System.IO.InvalidDataException>(() => WorldSnapshot.Decode(bytes));
        }
    }

    internal static class TestData
    {
        /// <summary>BALANCE 1번과 같은 값. 실제 에셋 값은 BalanceTests가 검사한다.</summary>
        public static MapSettings Map()
        {
            return new MapSettings
            {
                Width = 40,
                Height = 40,
                MinRooms = 9,
                MaxRooms = 11,
                MinRoomSize = 7,
                MaxRoomSize = 13,
                RoomMargin = 1,
                RoomAttempts = 300,
                LayoutAttempts = 50,
                CorridorWallCost = 5,
                ExtraConnections = 2,
                MinCoverObjectsPerRoom = 2,
                MaxCoverObjectsPerRoom = 4,
                PillarPercent = 30,
                MinLowWallsPerRoom = 1,
                MaxLowWallsPerRoom = 2,
                MinLowWallLength = 2,
                MaxLowWallLength = 3,
                DecorAttempts = 30,
                ChasmRoomPercent = 50,
                MinChasmPercent = 30,
                MaxChasmPercent = 50,
                MinSupplyCrates = 2,
                MaxSupplyCrates = 3,
                WorkshopsPerFloor = 1,
                // BALANCE 1-21 ~ 1-24 특수 방 (방 내용은 3-14b부터 하나씩).
                AlwaysSpecialRooms = { SpecialRoomKind.Treasure },
                RandomSpecialRooms = { SpecialRoomKind.Storehouse, SpecialRoomKind.Repair, SpecialRoomKind.Observatory, SpecialRoomKind.Exchange, SpecialRoomKind.SetGallery, SpecialRoomKind.Altar, SpecialRoomKind.Arena, SpecialRoomKind.Trap, SpecialRoomKind.Gamble, SpecialRoomKind.Archive },
                RandomSpecialRoomsPerFloor = 2,
                MinNormalRooms = 2,
                LockedSpecialRooms = { SpecialRoomKind.Workshop, SpecialRoomKind.Treasure, SpecialRoomKind.SetGallery, SpecialRoomKind.Altar },
                // BALANCE 29번 함정 방.
                TrapChasmPercent = 30,
                TrapCrates = 3,
                // BALANCE 30번 도박실.
                GambleChests = 3,
                // BALANCE 32번 상점: 구역 5층, 구역의 첫 층에 상점 1.
                FloorsPerZone = 5,
                ShopsPerZone = 1,
            };
        }

        /// <summary>BALANCE 4번 · 5번과 같은 값. enemies로 층당 적 수를 고정한다 (기본 0 = 적 없는 판).</summary>
        public static CombatSettings Combat(int enemies = 0)
        {
            var settings = new CombatSettings
            {
                MinEnemiesPerFloor = enemies,
                MaxEnemiesPerFloor = enemies,
                EnemyMinDistanceFromStart = 8,
                EndTurnCostTicks = 1,
                HalfCoverReductionPercent = 25,
                FullCoverReductionPercent = 35,
                StabilityGuardPercent = 60,
                MinDamage = 1,
                GrenadeThrowRange = 6,
                GrenadeRadius = 1,
                GrenadeDamagePercent = 70,
                GrenadeStabilityDamage = 2,
                GrenadeCooldownRounds = 3,
                GuardPercent = 50,
                GuardRadius = 3,
                CrateDurability = 2,
                LowWallDurability = 3,
                PushLength = 3,
                PushWidth = 2,
                PushDamagePercent = 30,
                PushCollisionDamage = 3,
                PushCollisionMaxHpPercent = 30, // BALANCE 9-4 (2026-11-17 사용자): 넉백 공격 부딪힘 = 최대 체력 30%
                PushCooldownRounds = 2,
                SonicRange = 6,
                SonicDamagePercent = 60,
                SonicStabilityDamage = 3,
                SonicCooldownRounds = 2,
                // BALANCE 35번 (3-16z): 빙결 사격 — 반경 6 · 100% · 방어 50% 고정 · 안정 4 · 재사용 대기 2 (한 차례 쉼).
                FrostRange = 6,
                FrostDamagePercent = 100,
                FrostFixedDefensePercent = 50,
                FrostStabilityDamage = 4,
                FrostCooldownRounds = 2,
                Keeper = new KeeperSkillSettings
                {
                    BasicPercent = 80,
                    StackMax = 6,
                    StackDamageTakenPercent = 10,
                    HpFloorPercent = 10,
                    HeartHpCostPercent = 30,
                    HeartStacks = 2,
                    HeartDamagePercent = 120,
                    HeartStabilityDamage = 4,
                    HeartCooldown = 2,
                    OrderHpCostPercent = 40,
                    OrderStacks = 3,
                    OrderDamagePercent = 100,
                    OrderStabilityDamage = 2,
                    OrderCooldown = 2,
                    OrderRange = 6,
                    OrderRadius = 2,
                    BurnDamage = 4,
                    BurnTurns = 2,
                    RemoldMax = 6,
                    RemoldPerAction = 1,
                    RemoldPerHit = 1,
                    RemoldPerActionEnd = 1,
                    FinalCost = 6,
                    FinalStacks = 6,
                    ShieldPercent = 50,
                    ShieldTurns = 3,
                    StacksPerExtraAttack = 3,
                    ExtraDamagePercentPerStack = 4,
                },
                AssaultPercent = 30,
                FallDamagePercent = 50,
                FloorCount = 5,
                EnemiesPerFloorStep = 1,
                AssaultPercentPerFloor = 5,
                GuardPercentDropPerFloor = 10,
                EnemyStatPercentPerFloor = 10,
                StartWeapon = WeaponKind.AssaultRifle,
                CombatDropWeaponPercent = 10,
                CombatDropMedkitPercent = 20,
                CrateWeaponPercent = 10,
                MedkitHealPercent = 40,
                CombatDropPartPercent = 60,
                CratePartPercent = 60,
                PartGradePercents = new[] { 60, 30, 10 },
                PartPrimaryStatCounts = new[] { 2, 3, 4 },
                PartSecondaryStatCounts = new[] { 1, 2, 3 },
                // BALANCE 18번 세트 효과.
                SetPiecesRequired = 3,
                SetDecisivePercentPerStack = 5,
                SetDecisiveMaxStacks = 4,
                SetDualCounterPercent = 10,
                SetPhaseStrikePercent = 15,
                SetPhaseAmpPercent = 10,
                SetPhaseAmpTurns = 1,
                SetPhaseAmpDoublePercent = 10,
                SetElementPercent = 20,
                SetTacticalPercent = 25,
                SetAssaultPercent = 12,
                SetAssaultMeleePercent = 24,
                SetApexPercent = 20,
                // BALANCE 19번 작업방.
                WorkshopOptionPercent = 70,
                WorkshopSetPercent = 50,
                WorkshopOptionCost = 2,
                WorkshopSetCost = 3,
                WorkshopUpgradeCost = 2,
                DisassembleMaterials = new[] { 1, 2, 3 },
                SpareSlots = 4,
                // BALANCE 20번 잠긴 문 · 열쇠.
                CombatDropKeyPercent = 10,
                // BALANCE 21번 보물방.
                TreasurePartGrade = PartGrade.Precision,
                TreasureWeaponPercent = 50,
                // BALANCE 22번 보급고.
                StorehouseMaterials = 6,
                StorehouseMedkits = 2,
                // BALANCE 23번 수리실.
                RepairHealPercent = 100,
                // BALANCE 25번 교환소.
                // BALANCE 26번 세트 진열실.
                SetGalleryPartGrade = PartGrade.Military,
                // BALANCE 27번 제단.
                AltarTrades =
                {
                    new AltarTrade { CostStat = PartStat.MaxHpPercent, CostAmount = 10, BoonStat = PartStat.AttackPercent, BoonAmount = 10 },
                    new AltarTrade { CostStat = PartStat.DefensePercent, CostAmount = 15, BoonStat = PartStat.CritPercent, BoonAmount = 15 },
                    new AltarTrade { CostStat = PartStat.AttackPercent, CostAmount = 10, BoonStat = PartStat.MaxHpPercent, BoonAmount = 15 },
                    new AltarTrade { CostStat = PartStat.MaxHpPercent, CostAmount = 10, BoonStat = PartStat.CritMultiplier, BoonAmount = 30 },
                    new AltarTrade { CostStat = PartStat.CritPercent, CostAmount = 5, BoonStat = PartStat.DefensePercent, BoonAmount = 25 },
                },
                AltarOfferCount = 3,
                // BALANCE 29번 함정 방.
                TrapEnemyCount = 2,
                // BALANCE 30번 도박실.
                GambleJackpotPercent = 30,
                GambleSmallPercent = 30,
                GambleEmptyPercent = 20,
                GambleJackpotGrade = PartGrade.Precision,
                GambleAmbushEnemies = 2,
                // BALANCE 28번 결투장.
                ArenaEnemyCount = 3,
                ArenaStatPercent = 30,
                ArenaPartGrade = PartGrade.Precision,
                ExchangeMedkitCost = 2,
                ExchangePartCost = 4,
                ExchangePartGrade = PartGrade.Military,
                ExchangeKeyCost = 5,
                ExchangeKeySellMaterials = 3,
                // BALANCE 31번 사르디스 골드 · 32번 상점.
                GoldDropPercent = 40,
                GoldDropMin = 5,
                GoldDropMax = 15,
                CrateGold = 15,
                TreasureGold = 40,
                ShopMedkits = 2,
                ShopKeys = 1,
                ShopWeapons = 1,
                ShopPartGrades = { PartGrade.Military, PartGrade.Precision },
                ShopMedkitPrice = 25,
                ShopKeyPrice = 35,
                ShopWeaponPrice = 60,
                ShopPartPrices = { 20, 45, 80 },
                ShopSellPercent = 35,
                ShopMaterialSellPrice = 3,
            };
            // BALANCE 12번 무기 표.
            settings.Weapons.Add(new WeaponStats
            {
                Kind = WeaponKind.AssaultRifle, Attack = 10, AttackRange = 8, StabilityDamage = 2, CritPercent = 15, CritMultiplierPercent = 150,
            });
            settings.Weapons.Add(new WeaponStats
            {
                Kind = WeaponKind.Pistol, Attack = 7, AttackRange = 6, StabilityDamage = 1, CritPercent = 15, CritMultiplierPercent = 150,
                MoveAfterShot = true,
            });
            settings.Weapons.Add(new WeaponStats
            {
                Kind = WeaponKind.Shotgun, Attack = 14, AttackRange = 5, StabilityDamage = 3, CritPercent = 15, CritMultiplierPercent = 150,
                // BALANCE 12-3 (2026-11-17 사용자): 1칸 150% → 5칸 20%.
                CloseRangePercent = 150, FarRangePercent = 20, FarRangeCells = 5,
            });
            settings.Weapons.Add(new WeaponStats
            {
                Kind = WeaponKind.Sniper, Attack = 13, AttackRange = 12, StabilityDamage = 1, CritPercent = 30, CritMultiplierPercent = 150,
                NoShotAfterMove = true,
            });
            settings.Weapons.Add(new WeaponStats
            {
                Kind = WeaponKind.Smg, Attack = 7, AttackRange = 6, StabilityDamage = 2, CritPercent = 15, CritMultiplierPercent = 150,
                ExtraShotsPerTurn = 1,
            });
            settings.Weapons.Add(new WeaponStats
            {
                Kind = WeaponKind.MachineGun, Attack = 9, AttackRange = 8, StabilityDamage = 4, CritPercent = 15, CritMultiplierPercent = 150,
                MoveRangeBonus = -2,
            });
            settings.Weapons.Add(new WeaponStats
            {
                Kind = WeaponKind.Blade, Attack = 16, AttackRange = 1, StabilityDamage = 3, CritPercent = 20, CritMultiplierPercent = 150,
                Melee = true, IgnoresCover = true,
            });
            settings.Units.Add(new UnitStats
            {
                Kind = UnitKind.Player, MaxHp = 100, MaxStability = 6, VisionRange = 9,
                Attack = 10, Defense = 4, MoveRange = 6, AttackRange = 8, StabilityDamage = 2, CritPercent = 15, CritMultiplierPercent = 150,
            });
            settings.Units.Add(new UnitStats
            {
                Kind = UnitKind.Rifleman, MaxHp = 26, MaxStability = 3, VisionRange = 9,
                Attack = 8, Defense = 2, MoveRange = 6, AttackRange = 7, StabilityDamage = 1, CritPercent = 15, CritMultiplierPercent = 150,
            });
            settings.Units.Add(new UnitStats
            {
                Kind = UnitKind.Assault, MaxHp = 21, MaxStability = 2, VisionRange = 9,
                Attack = 8, Defense = 1, MoveRange = 8, AttackRange = 1, StabilityDamage = 1, CritPercent = 15, CritMultiplierPercent = 150,
                PushOnHit = 1,
            });
            // BALANCE 32-6 상점 주인 (보스 급).
            settings.Units.Add(new UnitStats
            {
                Kind = UnitKind.Shopkeeper, MaxHp = 120, MaxStability = 8, VisionRange = 10,
                Attack = 9, Defense = 4, MoveRange = 6, AttackRange = 6, StabilityDamage = 3, CritPercent = 20, CritMultiplierPercent = 150,
            });
            // BALANCE 13번 캐릭터 표. 캐릭터를 고르지 않은 판(NewRun 기본)은 위 플레이어 줄을 그대로 쓴다.
            settings.Characters.Add(new CharacterStats
            {
                Kind = CharacterKind.HeroA, MaxHp = 110, MaxStability = 8, VisionRange = 8, Defense = 5, MoveRange = 8, StartWeapon = WeaponKind.Shotgun,
                Skills = { SkillKind.Push, SkillKind.Sonic, SkillKind.Grenade },
                WorkshopOptionPercent = 55, WorkshopSetPercent = 35, DoorForcePercent = 70,
                // BALANCE 6-7: A의 수류탄은 마름모 반경 2.
                GrenadeRadius = 2, GrenadeShape = AreaShape.Diamond,
                // BALANCE 9-1: A의 넉백 공격은 너비 3(가운데) × 길이 5.
                PushLength = 5, PushWidth = 3,
                // BALANCE 6-3 · 9-3 · 15-2 (2026-11-17 사용자): A의 스킬 계수 수류탄 120 · 넉백 100 · 음파 150.
                GrenadeDamagePercent = 120, PushDamagePercent = 100, SonicDamagePercent = 150,
                // BALANCE 34번: 리몰딩 (A만) — 최대 6 · 행동 +1 · 피해 +1 · 음파 1 · 수류탄 4, 이동 감소 적 +20%, 5칸 이상 걸으면 이동 감소 2칸 2차례.
                RemoldMax = 6, RemoldPerAction = 1, RemoldPerHit = 1, GrenadeRemoldCost = 4, SonicRemoldCost = 1,
                SlowedDamagePercent = 20, SlowAfterMoveCells = 5, SlowAmount = 2, SlowTurns = 2,
            });
            settings.Characters.Add(new CharacterStats
            {
                Kind = CharacterKind.HeroB, MaxHp = 90, MaxStability = 10, VisionRange = 10, Defense = 3, MoveRange = 6, StartWeapon = WeaponKind.Smg,
                // 3-16z: B 스킬1 = 빙결 사격 (2 · 3번 칸은 B 스킬2 · 결전기가 생길 때까지 넉백 · 수류탄).
                Skills = { SkillKind.Frost, SkillKind.Push, SkillKind.Grenade },
                WorkshopOptionPercent = 70, WorkshopSetPercent = 50,
            });
            settings.Characters.Add(new CharacterStats
            {
                Kind = CharacterKind.HeroC, MaxHp = 100, MaxStability = 6, VisionRange = 9, Defense = 4, MoveRange = 6, StartWeapon = WeaponKind.AssaultRifle,
                Skills = { SkillKind.Grenade, SkillKind.Push, SkillKind.None },
                WorkshopOptionPercent = 70, WorkshopSetPercent = 50,
            });
            // BALANCE 17번 파츠 표 (3-4).
            settings.PartStatRanges.AddRange(new[]
            {
                new PartStatRange { Stat = PartStat.Attack, Min = 1, Max = 2 },
                new PartStatRange { Stat = PartStat.AttackRange, Min = 1, Max = 1 },
                new PartStatRange { Stat = PartStat.CritPercent, Min = 3, Max = 6, Cap = 30 },
                new PartStatRange { Stat = PartStat.CritMultiplier, Min = 10, Max = 20, Cap = 30 },
                new PartStatRange { Stat = PartStat.StabilityDamage, Min = 1, Max = 1 },
                new PartStatRange { Stat = PartStat.MaxHp, Min = 5, Max = 10 },
                new PartStatRange { Stat = PartStat.Defense, Min = 1, Max = 1 },
                new PartStatRange { Stat = PartStat.MoveRange, Min = 1, Max = 1 },
                new PartStatRange { Stat = PartStat.MaxStability, Min = 1, Max = 1 },
                new PartStatRange { Stat = PartStat.AttackPercent, Min = 5, Max = 10, Cap = 30 },
                new PartStatRange { Stat = PartStat.MaxHpPercent, Min = 5, Max = 10, Cap = 30 },
                new PartStatRange { Stat = PartStat.DefensePercent, Min = 5, Max = 10, Cap = 30 },
            });
            settings.PartPrimaryStats.AddRange(new[] { PartStat.AttackPercent, PartStat.CritPercent, PartStat.CritMultiplier, PartStat.MaxHpPercent, PartStat.DefensePercent });
            settings.PartSecondaryStats.AddRange(new[] { PartStat.AttackPercent, PartStat.CritPercent, PartStat.MaxHpPercent, PartStat.DefensePercent });
            return settings;
        }

        /// <summary>BALANCE 값으로 새 판을 만든다. 맵 · 이동 테스트는 적 없는 판을 쓴다.</summary>
        public static World NewRun(int seed, int enemies = 0) => RunFactory.NewRun(seed, Map(), Combat(enemies));

        /// <summary>잠긴 문을 모두 연다 (지도 연결 검사용, 3-4d).</summary>
        public static void OpenAllDoors(FloorMap map)
        {
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (map.IsLockedDoor(new GridPos(x, y))) map.SetTile(new GridPos(x, y), TileType.DoorOpen);
        }

        /// <summary>글자 지도로 맵을 만든다. # 벽, . 바닥, C 상자, P 기둥, X 낭떠러지, S 보급 상자, W 작업대, D 잠긴 문. 첫 줄이 y = 0.</summary>
        public static FloorMap Parse(params string[] rows) => ParseWithRooms(new RoomRect[0], rows);

        /// <summary>Parse와 같고, 방 목록을 함께 넣는다 (방 시야 테스트용).</summary>
        public static FloorMap ParseWithRooms(RoomRect[] rooms, params string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var tiles = new TileType[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char ch = rows[y][x];
                    tiles[x + y * width] = ch == '#' ? TileType.Wall
                        : ch == 'C' ? TileType.Crate
                        : ch == 'P' ? TileType.Pillar
                        : ch == 'X' ? TileType.Chasm
                        : ch == 'S' ? TileType.SupplyCrate
                        : ch == 'W' ? TileType.Workbench
                        : ch == 'D' ? TileType.DoorLocked
                        : TileType.Floor;
                }
            }
            return new FloorMap(width, height, tiles, rooms, new GridPos(0, 0), new GridPos(0, 0));
        }
    }
}
