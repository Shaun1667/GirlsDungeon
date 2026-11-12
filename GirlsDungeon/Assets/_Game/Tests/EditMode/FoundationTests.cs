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
                Width = 32,
                Height = 32,
                MinRooms = 6,
                MaxRooms = 8,
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
                PushCooldownRounds = 2,
                SonicRange = 6,
                SonicDamagePercent = 60,
                SonicStabilityDamage = 3,
                SonicCooldownRounds = 2,
                AssaultPercent = 30,
                FallDamagePercent = 50,
                FloorCount = 5,
                EnemiesPerFloorStep = 1,
                AssaultPercentPerFloor = 5,
                GuardPercentDropPerFloor = 10,
                EnemyStatPercentPerFloor = 10,
                StartWeapon = WeaponKind.AssaultRifle,
                CombatDropWeaponPercent = 30,
                CombatDropMedkitPercent = 40,
                CrateWeaponPercent = 50,
                MedkitHealPercent = 40,
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
                Kind = WeaponKind.Shotgun, Attack = 14, AttackRange = 4, StabilityDamage = 3, CritPercent = 15, CritMultiplierPercent = 150,
                FalloffStartCells = 2, FalloffPercentPerCell = 15,
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
                Kind = UnitKind.Rifleman, MaxHp = 10, MaxStability = 3, VisionRange = 9,
                Attack = 6, Defense = 2, MoveRange = 6, AttackRange = 7, StabilityDamage = 1, CritPercent = 15, CritMultiplierPercent = 150,
            });
            settings.Units.Add(new UnitStats
            {
                Kind = UnitKind.Assault, MaxHp = 8, MaxStability = 2, VisionRange = 9,
                Attack = 6, Defense = 1, MoveRange = 8, AttackRange = 1, StabilityDamage = 1, CritPercent = 15, CritMultiplierPercent = 150,
                PushOnHit = 1,
            });
            // BALANCE 13번 캐릭터 표. 캐릭터를 고르지 않은 판(NewRun 기본)은 위 플레이어 줄을 그대로 쓴다.
            settings.Characters.Add(new CharacterStats
            {
                Kind = CharacterKind.HeroA, MaxHp = 110, MaxStability = 7, VisionRange = 8, Defense = 5, MoveRange = 8, StartWeapon = WeaponKind.Shotgun,
                Skills = { SkillKind.Push, SkillKind.Sonic, SkillKind.Grenade },
            });
            settings.Characters.Add(new CharacterStats
            {
                Kind = CharacterKind.HeroB, MaxHp = 90, MaxStability = 5, VisionRange = 10, Defense = 3, MoveRange = 6, StartWeapon = WeaponKind.Smg,
                Skills = { SkillKind.Grenade, SkillKind.Push, SkillKind.None },
            });
            settings.Characters.Add(new CharacterStats
            {
                Kind = CharacterKind.HeroC, MaxHp = 100, MaxStability = 6, VisionRange = 9, Defense = 4, MoveRange = 6, StartWeapon = WeaponKind.AssaultRifle,
                Skills = { SkillKind.Grenade, SkillKind.Push, SkillKind.None },
            });
            return settings;
        }

        /// <summary>BALANCE 값으로 새 판을 만든다. 맵 · 이동 테스트는 적 없는 판을 쓴다.</summary>
        public static World NewRun(int seed, int enemies = 0) => RunFactory.NewRun(seed, Map(), Combat(enemies));

        /// <summary>글자 지도로 맵을 만든다. # 벽, . 바닥, C 상자, P 기둥, X 낭떠러지, S 보급 상자. 첫 줄이 y = 0.</summary>
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
                        : TileType.Floor;
                }
            }
            return new FloorMap(width, height, tiles, rooms, new GridPos(0, 0), new GridPos(0, 0));
        }
    }
}
