using System;

namespace GirlsDungeon.Core.Random
{
    /// <summary>난수 용도. 기능을 더해도 다른 용도의 결과가 바뀌지 않게 나눈다. 끝에만 추가한다.</summary>
    public enum RandomPurpose
    {
        Map = 0,
        Decor = 1,
        Combat = 2,
        Item = 3,
        EnemyKind = 4,
        Effects = 5,
    }

    /// <summary>시드 하나에서 용도별 난수를 만든다. 상태는 세계 스냅샷에 저장된다.</summary>
    public sealed class RandomStreams
    {
        public static readonly int Count = Enum.GetValues(typeof(RandomPurpose)).Length;

        private readonly SeededRandom[] streams;

        public RandomStreams(int seed)
        {
            streams = new SeededRandom[Count];
            for (int i = 0; i < Count; i++) streams[i] = new SeededRandom(DeriveState(seed, i));
        }

        public RandomStreams(ulong[] states)
        {
            if (states == null || states.Length != Count)
                throw new ArgumentException("Random stream state count mismatch.", nameof(states));

            streams = new SeededRandom[Count];
            for (int i = 0; i < Count; i++) streams[i] = new SeededRandom(states[i]);
        }

        public IRandom Get(RandomPurpose purpose) => streams[(int)purpose];

        public ulong[] GetStates()
        {
            var states = new ulong[Count];
            for (int i = 0; i < Count; i++) states[i] = streams[i].State;
            return states;
        }

        private static ulong DeriveState(int seed, int purposeIndex)
        {
            // splitmix64: 시드와 용도가 조금만 달라도 전혀 다른 상태가 된다.
            ulong z = (uint)seed + (ulong)(purposeIndex + 1) * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
