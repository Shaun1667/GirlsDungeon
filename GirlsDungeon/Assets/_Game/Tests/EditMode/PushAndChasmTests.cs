using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// BALANCE 1-15 ~ 1-17 낭떠러지, 9번 밀치기: 앞 3 × 너비 2 · 범위 끝 바로 바깥까지 밀림 · 피해 30% · 충돌 3 · 재사용 2라운드.
    /// </summary>
    public class PushAndChasmTests
    {
        private static readonly GridPos East = new GridPos(1, 0);

        /// <summary>글자 지도 맵 + 플레이어 · 사격병들. 전투 상태는 직접 켠다.</summary>
        private static World MakeWorld(GridPos player, IEnumerable<GridPos> enemies, params string[] rows)
        {
            var world = new World(1, new GirlsDungeon.Core.Random.RandomStreams(1)) { Map = TestData.Parse(rows) };
            var combat = TestData.Combat();
            world.PlayerId = world.AddUnit(combat.StatsFor(UnitKind.Player), player).Id;
            foreach (var cell in enemies)
            {
                var enemy = world.AddUnit(combat.StatsFor(UnitKind.Rifleman), cell);
                world.Combatants.Add(enemy.Id);
            }
            world.Mode = WorldMode.Combat;
            world.Turn = TurnSide.Player;
            world.Round = 1;
            return world;
        }

        // ---- 낭떠러지 칸 ----

        [Test]
        public void Chasm_IsNotWalkable_DoesNotBlockSight_AndGivesNoCover()
        {
            var map = TestData.Parse(".X.");
            var chasm = new GridPos(1, 0);
            Assert.That(map.IsWalkable(chasm), Is.False);
            Assert.That(map.BlocksSight(chasm), Is.False);
            Assert.That(Sight.HasLine(map, new GridPos(0, 0), new GridPos(2, 0)), Is.True);
            Assert.That(Cover.InDirection(map, new GridPos(0, 0), East), Is.EqualTo(CoverLevel.None));
            Assert.That(GridNavigation.CanStep(map, new GridPos(0, 0), chasm), Is.False);
        }

        [Test]
        public void Generation_HalfTheRooms_GetChasm_NeverStartOrStairsRoom_AndTheFloorStaysConnected()
        {
            var settings = TestData.Map();
            for (int seed = 1; seed <= 200; seed++)
            {
                var map = TestData.NewRun(seed).Map;
                int chasmRooms = 0;
                int inRooms = 0;
                foreach (var room in map.Rooms)
                {
                    int count = 0;
                    for (int y = room.Y; y < room.Y + room.Height; y++)
                        for (int x = room.X; x < room.X + room.Width; x++)
                            if (map.IsChasm(new GridPos(x, y))) count++;
                    inRooms += count;
                    if (count == 0) continue;

                    chasmRooms++;
                    int area = room.Width * room.Height;
                    Assert.That(room.Contains(map.StartCell) || room.Contains(map.StairsCell), Is.False, $"seed {seed}: start / stairs room");
                    Assert.That(count * 100, Is.LessThanOrEqualTo(area * settings.MaxChasmPercent), $"seed {seed}: too much chasm");
                    Assert.That(count * 100, Is.GreaterThanOrEqualTo(area * (settings.MinChasmPercent - 5)), $"seed {seed}: too little chasm");
                    for (int x = room.X; x < room.X + room.Width; x++)
                    {
                        Assert.That(map.IsChasm(new GridPos(x, room.Y)) || map.IsChasm(new GridPos(x, room.Y + room.Height - 1)), Is.False, "edge row stays open");
                    }
                }

                Assert.That(chasmRooms, Is.EqualTo((map.Rooms.Count * settings.ChasmRoomPercent + 50) / 100), $"seed {seed}: room count");
                Assert.That(map.CountTiles(TileType.Chasm), Is.EqualTo(inRooms), $"seed {seed}: chasm outside rooms");
                Assert.That(ReachableFromStart(map), Is.EqualTo(map.CountWalkable()), $"seed {seed}: floor split");
            }
        }

        [Test]
        public void Generation_IsTheSameForTheSameSeed()
        {
            var a = TestData.NewRun(42).Map;
            var b = TestData.NewRun(42).Map;
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    Assert.That(b.Get(new GridPos(x, y)), Is.EqualTo(a.Get(new GridPos(x, y))));
        }

        // ---- 조준 · 범위 ----

        [Test]
        public void Aim_PicksTheLongerAxis_AndTheSideOfThePointer()
        {
            var origin = new GridPos(5, 5);
            Push.Aim(origin, new GridPos(8, 6), out var direction, out int side);
            Assert.That(direction, Is.EqualTo(East));
            Assert.That(side, Is.EqualTo(1), "north of an east push is the +1 side");

            Push.Aim(origin, new GridPos(8, 4), out direction, out side);
            Assert.That(direction, Is.EqualTo(East));
            Assert.That(side, Is.EqualTo(-1));

            Push.Aim(origin, new GridPos(4, 1), out direction, out side);
            Assert.That(direction, Is.EqualTo(new GridPos(0, -1)));
        }

        [Test]
        public void Area_IsThreeLongAndTwoWide_InFrontOfThePlayer()
        {
            var map = TestData.Parse("........", "........", "........");
            var cells = Push.AreaCells(map, TestData.Combat(), new GridPos(0, 1), East, 1);
            CollectionAssert.AreEquivalent(new[]
            {
                new GridPos(1, 1), new GridPos(1, 2), new GridPos(2, 1), new GridPos(2, 2), new GridPos(3, 1), new GridPos(3, 2),
            }, cells);
        }

        // ---- 밀림 ----

        [Test]
        public void Units_ArePushedToTheCellJustPastTheArea()
        {
            var world = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0), new GridPos(3, 0) }, "........");
            var near = world.UnitAt(new GridPos(1, 0));
            var far = world.UnitAt(new GridPos(3, 0));

            Assert.That(Push.Resolve(world, TestData.Combat(), world.Player, East, 1, new List<GameEvent>()), Is.True);
            Assert.That(far.Position, Is.EqualTo(new GridPos(4, 0)), "depth 3 → 1 cell");
            Assert.That(near.Position, Is.EqualTo(new GridPos(3, 0)), "depth 1 → stops behind the far one");
        }

        [Test]
        public void DirectDamage_Is30PercentOfTheFormula_WithNoCover()
        {
            var world = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0) }, "........");
            var preview = Push.Preview(world, TestData.Combat(), world.Player, East, 1);
            var hit = preview.Hits.Single();
            Assert.That(hit.Damage, Is.EqualTo(CombatRules.Damage(10, 2, 30, 0, 1)));
            Assert.That(hit.Damage, Is.EqualTo(3), "10 × 10 ÷ 12 × 30% = 2.5 → 3");
            Assert.That(hit.Slide.End, Is.EqualTo(new GridPos(4, 0)));
            Assert.That(hit.Slide.Collides, Is.False);
        }

        [Test]
        public void HittingAWall_StopsTheUnit_AndDealsCollisionDamage()
        {
            var world = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0) }, "...#....");
            var enemy = world.UnitAt(new GridPos(1, 0));
            var events = new List<GameEvent>();

            Assert.That(Push.Resolve(world, TestData.Combat(), world.Player, East, 1, events), Is.True);
            Assert.That(enemy.Position, Is.EqualTo(new GridPos(2, 0)));
            Assert.That(enemy.Hp, Is.EqualTo(10 - 3 - 3), "push 3 + collision 3");
            var pushed = events.OfType<UnitPushed>().Single();
            Assert.That(pushed.CollisionDamage, Is.EqualTo(3));
            Assert.That(pushed.Fell, Is.False);
        }

        [Test]
        public void CratesAndLowWalls_StopThePushToo()
        {
            var crate = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0) }, "..C.....");
            Assert.That(Push.Preview(crate, TestData.Combat(), crate.Player, East, 1).Hits.Single().Slide.End, Is.EqualTo(new GridPos(1, 0)));

            var lowWall = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0) }, "........");
            lowWall.Map.SetLowWallBetween(new GridPos(2, 0), new GridPos(3, 0), true);
            var slide = Push.Preview(lowWall, TestData.Combat(), lowWall.Player, East, 1).Hits.Single().Slide;
            Assert.That(slide.End, Is.EqualTo(new GridPos(2, 0)));
            Assert.That(slide.Collides, Is.True);
        }

        [Test]
        public void AnEnemyPushedOverAChasm_FallsAndDies()
        {
            var world = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0) }, "...X....");
            var enemy = world.UnitAt(new GridPos(1, 0));
            var events = new List<GameEvent>();

            var hit = Push.Preview(world, TestData.Combat(), world.Player, East, 1).Hits.Single();
            Assert.That(hit.Slide.Falls, Is.True);
            Assert.That(hit.Dies, Is.True);

            Assert.That(Push.Resolve(world, TestData.Combat(), world.Player, East, 1, events), Is.True);
            Assert.That(world.FindUnit(enemy.Id), Is.Null);
            Assert.That(world.Combatants.Contains(enemy.Id), Is.False);
            Assert.That(events.OfType<UnitPushed>().Single().Fell, Is.True);
            Assert.That(events.OfType<UnitDied>().Single().UnitId, Is.EqualTo(enemy.Id));
        }

        [Test]
        public void AnEnemyKilledByTheHit_IsNotPushed()
        {
            var world = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0) }, "........");
            var enemy = world.UnitAt(new GridPos(1, 0));
            enemy.Hp = 2;
            var hit = Push.Preview(world, TestData.Combat(), world.Player, East, 1).Hits.Single();
            Assert.That(hit.DiesFromHit, Is.True);
            Assert.That(hit.Slide.Path.Count, Is.EqualTo(0));
        }

        [Test]
        public void Preview_MatchesTheRealResult()
        {
            var world = MakeWorld(new GridPos(0, 1), new[] { new GridPos(1, 1), new GridPos(2, 2), new GridPos(3, 1) },
                "........",
                ".....X..",
                "....#...");
            var preview = Push.Preview(world, TestData.Combat(), world.Player, East, 1);
            var expected = preview.Hits.ToDictionary(h => h.Target.Id, h => (h.Slide.End, h.Dies, hp: h.Target.Hp - h.Damage - h.CollisionDamage));

            Assert.That(Push.Resolve(world, TestData.Combat(), world.Player, East, 1, new List<GameEvent>()), Is.True);
            foreach (var pair in expected)
            {
                var unit = world.FindUnit(pair.Key);
                if (pair.Value.Dies)
                {
                    Assert.That(unit, Is.Null, $"#{pair.Key} should be gone");
                    continue;
                }
                Assert.That(unit.Position, Is.EqualTo(pair.Value.End), $"#{pair.Key} end");
                Assert.That(unit.Hp, Is.EqualTo(pair.Value.hp), $"#{pair.Key} hp");
            }
        }

        // ---- 커맨드 · 재사용 대기 ----

        [Test]
        public void NoTarget_BlocksThePush()
        {
            var world = MakeWorld(new GridPos(0, 0), new[] { new GridPos(6, 0) }, "........");
            Assert.That(Push.Preview(world, TestData.Combat(), world.Player, East, 1).Block, Is.EqualTo(PushBlock.NoTarget));
            Assert.That(new Simulation(world).Execute(new PushCommand(East, 1, TestData.Combat())), Is.False);
        }

        [Test]
        public void PushCommand_EndsTheTurn_AndStartsTheCooldown()
        {
            var world = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0), new GridPos(7, 0) }, "........");
            var simulation = new Simulation(world, TestData.Combat());

            Assert.That(simulation.Execute(new PushCommand(East, 1, TestData.Combat())), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Combat));
            Assert.That(world.Round, Is.EqualTo(2), "enemy turn ran");
            Assert.That(world.PushCooldown, Is.EqualTo(1), "2 set on use, minus 1 at the next player turn");
            Assert.That(Push.Preview(world, TestData.Combat(), world.Player, East, 1).Block, Is.EqualTo(PushBlock.NotReady));
        }

        [Test]
        public void CombatEnd_ResetsTheCooldown()
        {
            var world = MakeWorld(new GridPos(0, 0), new[] { new GridPos(1, 0) }, "...X....");
            var simulation = new Simulation(world, TestData.Combat());
            Assert.That(simulation.Execute(new PushCommand(East, 1, TestData.Combat())), Is.True);
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration), "the only enemy fell");
            Assert.That(world.PushCooldown, Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_KeepsPushCooldownAndChasm()
        {
            var world = TestData.NewRun(7);
            world.PushCooldown = 2;
            world.Map.SetTile(new GridPos(1, 1), TileType.Chasm);
            var restored = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            Assert.That(restored.PushCooldown, Is.EqualTo(2));
            Assert.That(restored.Map.Get(new GridPos(1, 1)), Is.EqualTo(TileType.Chasm));
            Assert.That(WorldSnapshot.Encode(restored), Is.EqualTo(WorldSnapshot.Encode(world)));
        }

        private static int ReachableFromStart(FloorMap map)
        {
            var seen = new HashSet<GridPos> { map.StartCell };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(map.StartCell);
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var next = new GridPos(cell.X + dx, cell.Y + dy);
                        if (!GridNavigation.CanStep(map, cell, next) || !seen.Add(next)) continue;
                        frontier.Enqueue(next);
                    }
            }
            return seen.Count;
        }
    }
}
