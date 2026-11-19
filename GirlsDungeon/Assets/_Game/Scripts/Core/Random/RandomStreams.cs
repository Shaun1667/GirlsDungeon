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
        Spawn = 6,
        /// <summary>파츠 작업방 — 작업방 고르기 · 옵션 · 세트 바꾸기 굴림 (3-4c).</summary>
        Workshop = 7,
        /// <summary>잠긴 문 — 힘으로 열기 굴림 · 전투 보상 열쇠 굴림 (3-4d).</summary>
        Door = 8,
        /// <summary>층 만들기 — 보급 상자 · 특수 방 배치 (3-14l). 층을 진행하는 동안에는 쓰지 않는다 — 기록실 예고(Archive.Forecast)가 실제 다음 층과 같도록.</summary>
        Floor = 9,
        /// <summary>상점 진열 물건 (파츠 굴림 · 무기 고르기, 3-15b). 층을 만들 때만 쓴다.</summary>
        Shop = 10,
        /// <summary>적을 쓰러뜨렸을 때 바닥 골드 굴림 (3-16w — 떨어질지 · 양).</summary>
        GoldDrop = 11,
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
