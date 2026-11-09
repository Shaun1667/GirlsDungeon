using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 규칙 진행의 한 곳. 모든 변화는 커맨드와 틱으로만 일어나고, 사건은 끝에 한꺼번에 나간다.
    /// 틱 순서 (시스템이 생기면 이 순서에 끼운다):
    /// ① 행동 ② 반응 ③ 지형 ④ 상태 ⑤ 재생 · 기타 ⑥ 사망 · 승패 · 시야 ⑦ 사건 발행
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
        public static World NewRun(int seed, MapSettings mapSettings)
        {
            var world = new World(seed, new RandomStreams(seed));
            world.Map = DungeonGenerator.Generate(mapSettings, world.Random.Get(RandomPurpose.Map));
            world.PlayerId = world.AddUnit(UnitKind.Player, world.Map.StartCell).Id;
            return world;
        }
    }
}
