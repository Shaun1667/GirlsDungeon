namespace GirlsDungeon.Core.Random
{
    /// <summary>시드 난수 인터페이스. 규칙 코드는 전역 난수를 쓰지 않고 이것만 받는다.</summary>
    public interface IRandom
    {
        /// <summary>[minInclusive, maxExclusive) 범위의 정수.</summary>
        int Range(int minInclusive, int maxExclusive);
    }

    /// <summary>xorshift64* 난수. 상태 하나로 저장 · 복원할 수 있고 플랫폼이 달라도 결과가 같다.</summary>
    public sealed class SeededRandom : IRandom
    {
        public ulong State { get; private set; }

        public SeededRandom(ulong state)
        {
            State = state != 0UL ? state : 0x9E3779B97F4A7C15UL;
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong span = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(((NextULong() >> 32) * span) >> 32);
        }

        private ulong NextULong()
        {
            ulong x = State;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            State = x;
            return x * 0x2545F4914F6CDD1DUL;
        }
    }
}
