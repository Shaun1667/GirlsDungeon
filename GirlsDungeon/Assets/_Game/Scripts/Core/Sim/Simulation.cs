using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 규칙 진행의 한 곳. 모든 변화는 커맨드와 틱으로만 일어나고, 사건은 끝에 한꺼번에 나간다.
    /// 틱 순서 (시스템이 생기면 이 순서에 끼운다):
    /// ① 행동 ② 반응 ③ 지형 ④ 상태 ⑤ 재생 · 기타 ⑥ 사망 · 승패 · 시야(전투 전환 CombatFlow) ⑦ 사건 발행
    /// </summary>
    public sealed class Simulation
    {
        private readonly List<GameEvent> pending = new List<GameEvent>();

        public World World { get; }

        /// <summary>한 커맨드가 끝날 때 쌓인 사건을 내보낸다.</summary>
        public event Action<IReadOnlyList<GameEvent>> EventsPublished;

        public Simulation(World world)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>커맨드를 적용한다. 규칙에 맞지 않으면 틱이 흐르지 않고 false.</summary>
        public bool Execute(ICommand command)
        {
            if (!command.TryApply(World, pending)) return false;

            for (int i = 0; i < command.CostTicks; i++) RunTick();
            CombatFlow.AfterAction(World, pending);
            Publish();
            return true;
        }

        private void RunTick()
        {
            World.Tick++;
            pending.Add(new TickAdvanced(World.Tick));
        }

        private void Publish()
        {
            var events = pending.ToArray();
            pending.Clear();
            EventsPublished?.Invoke(events);
        }
    }

    /// <summary>새 판 만들기의 한 곳. 같은 시드와 수치면 언제나 같은 세계가 나온다.</summary>
    public static class RunFactory
    {
        public static World NewRun(int seed, MapSettings mapSettings, CombatSettings combatSettings)
        {
            combatSettings.Validate();

            var world = new World(seed, new RandomStreams(seed));
            world.Map = DungeonGenerator.Generate(mapSettings, world.Random.Get(RandomPurpose.Map));
            FloorDecor.Apply(world.Map, mapSettings, world.Random.Get(RandomPurpose.Decor));

            var playerStats = combatSettings.StatsFor(UnitKind.Player);
            world.PlayerId = world.AddUnit(playerStats, world.Map.StartCell).Id;
            EnemySpawner.Spawn(world, combatSettings, world.Random.Get(RandomPurpose.Spawn));
            return world;
        }
    }

    /// <summary>
    /// 층에 적을 놓는 한 곳. 시작 칸에서 일정 거리 밖이고, 시작 칸에서 보이지 않는 걸을 수 있는 칸에만 놓는다.
    /// 후보 칸은 행 순서로 모은 뒤 배치 난수로 고르므로 같은 시드면 같은 자리가 나온다.
    /// </summary>
    public static class EnemySpawner
    {
        public static void Spawn(World world, CombatSettings settings, IRandom random)
        {
            var map = world.Map;
            var start = map.StartCell;
            int playerVision = settings.StatsFor(UnitKind.Player).VisionRange;

            var candidates = new List<GridPos>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (!map.IsWalkable(cell) || cell == map.StairsCell || world.IsOccupied(cell)) continue;
                    if (Math.Max(Math.Abs(cell.X - start.X), Math.Abs(cell.Y - start.Y)) < settings.EnemyMinDistanceFromStart) continue;
                    if (Sight.CanSee(map, start, cell, playerVision)) continue;
                    candidates.Add(cell);
                }
            }

            int count = random.Range(settings.MinEnemiesPerFloor, settings.MaxEnemiesPerFloor + 1);
            var riflemanStats = settings.StatsFor(UnitKind.Rifleman);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = random.Range(0, candidates.Count);
                world.AddUnit(riflemanStats, candidates[index]);
                candidates.RemoveAt(index);
            }
        }
    }
}
