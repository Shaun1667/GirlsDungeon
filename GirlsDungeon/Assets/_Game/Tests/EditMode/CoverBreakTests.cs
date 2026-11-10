using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>BALANCE 8번 부서지는 엄폐물: 상자 2 · 낮은 벽 3, 부서지면 빈 바닥. 기둥 · 벽은 부서지지 않는다.</summary>
    public class CoverBreakTests
    {
        /// <summary>한 줄 방. 플레이어 (0,0), 사격병 (5,0), 사격병 왼쪽 (4,0)에 상자 → 플레이어 쪽 낮은 엄폐.</summary>
        private static (World world, Unit player, Unit enemy) Line()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse("..........") };
            var combat = TestData.Combat();
            var player = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(0, 0));
            world.PlayerId = player.Id;
            player.CritPercent = 0;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(5, 0));
            enemy.Hp = enemy.MaxHp = 100;
            world.Map.SetTile(new GridPos(4, 0), TileType.Crate);
            return (world, player, enemy);
        }

        [Test]
        public void Crate_BreaksOnTheSecondShot_DamageUsesCoverBeforeBreaking()
        {
            var (world, player, enemy) = Line();
            var settings = TestData.Combat();
            var events = new List<GameEvent>();

            var first = CombatRules.Preview(world, settings, player, player.Position, enemy);
            Assert.That(first.BreaksCover, Is.False);
            CombatRules.Resolve(world, settings, player, enemy, events);
            Assert.That(world.Map.Get(new GridPos(4, 0)), Is.EqualTo(TileType.Crate));
            Assert.That(world.Map.CrateDamage(new GridPos(4, 0)), Is.EqualTo(1));

            var second = CombatRules.Preview(world, settings, player, player.Position, enemy);
            Assert.That(second.BreaksCover, Is.True);
            events.Clear();
            CombatRules.Resolve(world, settings, player, enemy, events);

            Assert.That(events.OfType<UnitAttacked>().Single().Cover, Is.EqualTo(CoverLevel.Half), "the breaking shot still used the cover");
            Assert.That(world.Map.Get(new GridPos(4, 0)), Is.EqualTo(TileType.Floor));
            Assert.That(events.OfType<CoverDestroyed>().Single().Cells, Is.EqualTo(new[] { new GridPos(4, 0) }));
            Assert.That(Cover.Against(world.Map, enemy.Position, player.Position), Is.EqualTo(CoverLevel.None));
        }

        [Test]
        public void LowWall_TakesThreeHits()
        {
            var (world, player, enemy) = Line();
            world.Map.SetTile(new GridPos(4, 0), TileType.Floor);
            world.Map.SetLowWallBetween(new GridPos(4, 0), new GridPos(5, 0), true);
            var settings = TestData.Combat();
            var events = new List<GameEvent>();

            CombatRules.Resolve(world, settings, player, enemy, events);
            CombatRules.Resolve(world, settings, player, enemy, events);
            Assert.That(world.Map.HasLowWallBetween(new GridPos(4, 0), new GridPos(5, 0)), Is.True);
            Assert.That(world.Map.LowWallDamage(new GridPos(5, 0), new GridPos(4, 0)), Is.EqualTo(2));

            CombatRules.Resolve(world, settings, player, enemy, events);
            Assert.That(world.Map.HasLowWallBetween(new GridPos(4, 0), new GridPos(5, 0)), Is.False);
        }

        [Test]
        public void FlankingShot_DoesNotDamageTheCover()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse("......", "......") };
            var combat = TestData.Combat();
            var player = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(5, 1));
            world.PlayerId = player.Id;
            var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), new GridPos(5, 0));
            enemy.Hp = enemy.MaxHp = 100;
            world.Map.SetTile(new GridPos(4, 0), TileType.Crate);

            CombatRules.Resolve(world, combat, player, enemy, new List<GameEvent>());
            Assert.That(world.Map.CrateDamage(new GridPos(4, 0)), Is.EqualTo(0), "the crate faces west, the shot came from the north");
        }

        [Test]
        public void PillarsAndWalls_NeverBreak()
        {
            var (world, player, enemy) = Line();
            world.Map.SetTile(new GridPos(4, 0), TileType.Floor);
            world.Map.SetTile(new GridPos(5, 0), TileType.Floor);
            enemy.Position = new GridPos(6, 0);
            var settings = TestData.Combat();
            var events = new List<GameEvent>();

            Assert.That(CoverBreak.Facing(world.Map, enemy.Position, player.Position), Is.Empty);
            world.Map.SetTile(new GridPos(5, 0), TileType.Pillar);
            Assert.That(CoverBreak.Facing(world.Map, enemy.Position, player.Position), Is.Empty);
        }

        [Test]
        public void Grenade_HitsEveryCrateAndLowWallInTheBlast()
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1))
            {
                Map = TestData.Parse(
                    "##########",
                    "#........#",
                    "#........#",
                    "#........#",
                    "##########"),
            };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), new GridPos(1, 2)).Id;
            world.Mode = WorldMode.Combat;
            world.Map.SetTile(new GridPos(5, 1), TileType.Crate);
            world.Map.SetTile(new GridPos(7, 3), TileType.Crate);
            world.Map.SetLowWallBetween(new GridPos(6, 2), new GridPos(7, 2), true);
            world.Map.SetCrateDamage(new GridPos(5, 1), 1);

            var preview = AreaAttack.Preview(world, combat, world.Player, new GridPos(6, 2));
            Assert.That(preview.CoverBreaks, Is.EqualTo(1), "only the already-hit crate breaks");

            var events = new List<GameEvent>();
            Assert.That(AreaAttack.Resolve(world, combat, world.Player, new GridPos(6, 2), events), Is.True);
            Assert.That(world.Map.Get(new GridPos(5, 1)), Is.EqualTo(TileType.Floor));
            Assert.That(world.Map.CrateDamage(new GridPos(7, 3)), Is.EqualTo(1));
            Assert.That(world.Map.LowWallDamage(new GridPos(6, 2), new GridPos(7, 2)), Is.EqualTo(1));
        }

        [Test]
        public void Snapshot_KeepsCoverDamage()
        {
            var (world, _, _) = Line();
            world.Map.SetCrateDamage(new GridPos(4, 0), 1);
            world.Map.SetLowWallBetween(new GridPos(1, 0), new GridPos(2, 0), true);
            world.Map.SetLowWallDamage(new GridPos(1, 0), new GridPos(2, 0), 2);
            world.Explored = new bool[world.Map.Width * world.Map.Height];

            var bytes = WorldSnapshot.Encode(world);
            var restored = WorldSnapshot.Decode(bytes);
            Assert.That(restored.Map.CrateDamage(new GridPos(4, 0)), Is.EqualTo(1));
            Assert.That(restored.Map.LowWallDamage(new GridPos(2, 0), new GridPos(1, 0)), Is.EqualTo(2));
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(bytes));
        }
    }
}
