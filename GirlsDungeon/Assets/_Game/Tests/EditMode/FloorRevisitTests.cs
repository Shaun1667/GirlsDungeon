using System.Collections.Generic;
using System.Linq;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using NUnit.Framework;

namespace GirlsDungeon.Tests
{
    /// <summary>
    /// 3-16u 올라가는 계단 (결정 2026-11-18 사용자): 2층부터 시작 칸이 올라가는 계단, 밟으면 위층을 떠날 때 그대로 되살린다
    /// (쓰러뜨린 적 · 연 상자 · 바닥 물건 · 본 칸 · 층별 기록). 다시 내려가도 그 층 그대로 — 지형 난수를 다시 쓰지 않는다.
    /// </summary>
    public class FloorRevisitTests
    {
        [Test]
        public void TheFirstFloorHasNoUpStairs_TheNextFloorStartsOnThem()
        {
            var world = TestData.NewRun(21);
            Assert.That(world.Map.CountTiles(TileType.StairsUp), Is.EqualTo(0));
            Assert.That(FloorFlow.Ascend(world, new List<GameEvent>()), Is.False, "nowhere to go up from floor 1");

            RunFactory.NextFloor(world, TestData.Map(), TestData.Combat(), new List<GameEvent>());
            Assert.That(world.Map.Get(world.Map.StartCell), Is.EqualTo(TileType.StairsUp));
            Assert.That(world.Map.CountTiles(TileType.StairsUp), Is.EqualTo(1));
            Assert.That(world.Map.IsWalkable(world.Map.StartCell), Is.True);
            Assert.That(world.Map.BlocksSight(world.Map.StartCell), Is.False);
        }

        [Test]
        public void GoingUp_RestoresTheFloorAsItWasLeft()
        {
            var world = TestData.NewRun(22, 3);
            var combat = TestData.Combat(3);
            var firstMap = world.Map;
            var enemies = world.Units.Where(unit => !unit.IsPlayer).ToList();
            Assert.That(enemies.Count, Is.GreaterThan(1));
            var defeated = enemies[0];
            world.Units.Remove(defeated);
            var hurt = enemies[1];
            hurt.Hp = 1;
            var medkit = world.AddItem(ItemKind.Medkit, default, world.Player.Position);
            world.FloorKeyGiven = true;
            var explored = (bool[])world.Explored.Clone();

            RunFactory.NextFloor(world, TestData.Map(), combat, new List<GameEvent>());
            Assert.That(world.FloorIndex, Is.EqualTo(1));
            Assert.That(world.Items.Any(item => item.Id == medkit.Id), Is.False);
            Assert.That(world.FindUnit(hurt.Id), Is.Null);

            var events = new List<GameEvent>();
            Assert.That(FloorFlow.Ascend(world, events), Is.True);

            Assert.That(world.FloorIndex, Is.EqualTo(0));
            Assert.That(world.Map, Is.SameAs(firstMap));
            Assert.That(world.FindUnit(defeated.Id), Is.Null, "a defeated enemy stays down");
            Assert.That(world.FindUnit(hurt.Id).Hp, Is.EqualTo(1), "a hurt enemy stays hurt");
            Assert.That(world.Items.Any(item => item.Id == medkit.Id), Is.True, "items stay on the floor");
            Assert.That(world.FloorKeyGiven, Is.True);
            for (int i = 0; i < explored.Length; i++)
                if (explored[i]) Assert.That(world.Explored[i], Is.True, "cells seen before stay seen");
            Assert.That(world.Mode, Is.EqualTo(WorldMode.Exploration));

            var stairs = world.Map.StairsCell;
            var at = world.Player.Position;
            Assert.That(System.Math.Max(System.Math.Abs(at.X - stairs.X), System.Math.Abs(at.Y - stairs.Y)), Is.EqualTo(1), "beside the down stairs");
            Assert.That(events.OfType<FloorChanged>().Single().StartCell, Is.EqualTo(at));
        }

        [Test]
        public void GoingDownAgain_RestoresTheSameFloor_WithoutUsingTheFloorRandom()
        {
            var world = TestData.NewRun(23, 2);
            var combat = TestData.Combat(2);
            RunFactory.NextFloor(world, TestData.Map(), combat, null);
            var secondMap = world.Map;
            var secondUnits = world.Units.Where(unit => !unit.IsPlayer).Select(unit => unit.Id).ToArray();
            secondMap.SetTile(new GridPos(0, 0), TileType.Wall);

            Assert.That(FloorFlow.Ascend(world, null), Is.True);
            var randomBefore = world.Random.GetStates();
            RunFactory.NextFloor(world, TestData.Map(), combat, null);

            Assert.That(world.FloorIndex, Is.EqualTo(1));
            Assert.That(world.Map, Is.SameAs(secondMap));
            Assert.That(world.Units.Where(unit => !unit.IsPlayer).Select(unit => unit.Id).ToArray(), Is.EqualTo(secondUnits));
            Assert.That(world.Random.GetStates(), Is.EqualTo(randomBefore), "no floor is generated");
            Assert.That(world.Map.Get(world.Map.StartCell), Is.EqualTo(TileType.StairsUp));
        }

        [Test]
        public void SteppingOntoTheUpStairs_GoesUp()
        {
            var world = TestData.NewRun(24);
            var simulation = new Simulation(world, TestData.Combat(), TestData.Map());
            RunFactory.NextFloor(world, TestData.Map(), TestData.Combat(), null);
            world.Units.RemoveAll(unit => !unit.IsPlayer);
            world.ArenaEnemies.Clear();
            var start = world.Map.StartCell;

            GridPos? off = null;
            foreach (var step in new[] { new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1) })
            {
                var cell = new GridPos(start.X + step.X, start.Y + step.Y);
                if (world.Map.IsWalkable(cell)) { off = cell; break; }
            }
            Assert.That(off.HasValue, Is.True);
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, off.Value, 1)), Is.True);
            Assert.That(world.FloorIndex, Is.EqualTo(1), "standing next to the up stairs does nothing");
            Assert.That(simulation.Execute(new MoveCommand(world.PlayerId, start, 1)), Is.True);
            Assert.That(world.FloorIndex, Is.EqualTo(0));
        }

        [Test]
        public void Snapshot_KeepsTheStoredFloors()
        {
            var world = TestData.NewRun(25, 2);
            var combat = TestData.Combat(2);
            world.AddItem(ItemKind.Medkit, default, world.Player.Position);
            RunFactory.NextFloor(world, TestData.Map(), combat, null);

            var bytes = WorldSnapshot.Encode(world);
            var copy = WorldSnapshot.Decode(bytes);
            Assert.That(WorldSnapshot.Encode(copy), Is.EqualTo(bytes), "round trip");
            Assert.That(copy.StoredFloors.Count, Is.EqualTo(1));

            Assert.That(FloorFlow.Ascend(world, null), Is.True);
            Assert.That(FloorFlow.Ascend(copy, null), Is.True);
            Assert.That(WorldSnapshot.Encode(copy), Is.EqualTo(WorldSnapshot.Encode(world)), "the decoded run goes up to the same floor");
        }
    }
}
