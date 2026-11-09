using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>유닛 종류. 저장에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum UnitKind : byte
    {
        Player = 0,
    }

    /// <summary>맵 위의 유닛. 다른 곳에서는 고유 ID로 참조한다.</summary>
    public sealed class Unit
    {
        public int Id { get; }
        public UnitKind Kind { get; }
        public GridPos Position { get; set; }

        public Unit(int id, UnitKind kind, GridPos position)
        {
            Id = id;
            Kind = kind;
            Position = position;
        }
    }

    /// <summary>판의 모든 상태. 저장 · 복원은 이것 하나로 끝나고, 시스템은 상태를 들지 않는다.</summary>
    public sealed class World
    {
        public int Seed { get; }
        public RandomStreams Random { get; }
        public FloorMap Map { get; set; }
        public int FloorIndex { get; set; }
        public long Tick { get; set; }
        public int NextEntityId { get; set; }
        public int PlayerId { get; set; }
        public List<Unit> Units { get; } = new List<Unit>();

        public World(int seed, RandomStreams random)
        {
            Seed = seed;
            Random = random;
        }

        public Unit Player => FindUnit(PlayerId);

        public Unit FindUnit(int id)
        {
            foreach (var unit in Units) if (unit.Id == id) return unit;
            return null;
        }

        public bool IsOccupied(GridPos cell)
        {
            foreach (var unit in Units) if (unit.Position == cell) return true;
            return false;
        }

        public Unit AddUnit(UnitKind kind, GridPos position)
        {
            var unit = new Unit(NextEntityId++, kind, position);
            Units.Add(unit);
            return unit;
        }
    }
}
