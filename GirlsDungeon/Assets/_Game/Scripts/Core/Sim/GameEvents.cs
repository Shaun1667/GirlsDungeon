using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>규칙이 화면에 알리는 사건. 값만 담고 글은 만들지 않는다.</summary>
    public abstract class GameEvent { }

    public sealed class UnitMoved : GameEvent
    {
        public int UnitId { get; }
        public GridPos From { get; }
        public GridPos To { get; }

        public UnitMoved(int unitId, GridPos from, GridPos to)
        {
            UnitId = unitId;
            From = from;
            To = to;
        }
    }

    public sealed class TickAdvanced : GameEvent
    {
        public long Tick { get; }

        public TickAdvanced(long tick)
        {
            Tick = tick;
        }
    }
}
