using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>유닛 종류. 저장에 정수로 들어가므로 끝에만 추가한다. 능력치는 CombatSettings 표에서 온다.</summary>
    public enum UnitKind : byte
    {
        Player = 0,
        Rifleman = 1,
    }

    /// <summary>탐험과 턴 전투. 저장에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum WorldMode : byte
    {
        Exploration = 0,
        Combat = 1,
    }

    /// <summary>전투 중 지금 움직이는 쪽.</summary>
    public enum TurnSide : byte
    {
        Player = 0,
        Enemies = 1,
    }

    /// <summary>맵 위의 유닛. 다른 곳에서는 고유 ID로 참조한다.</summary>
    public sealed class Unit
    {
        public int Id { get; }
        public UnitKind Kind { get; }
        public GridPos Position { get; set; }
        public int MaxHp { get; }
        public int Hp { get; set; }
        public int MaxStability { get; }
        public int Stability { get; set; }
        public int VisionRange { get; }

        public bool IsPlayer => Kind == UnitKind.Player;

        public Unit(int id, UnitKind kind, GridPos position, int maxHp, int hp, int maxStability, int stability, int visionRange)
        {
            Id = id;
            Kind = kind;
            Position = position;
            MaxHp = maxHp;
            Hp = hp;
            MaxStability = maxStability;
            Stability = stability;
            VisionRange = visionRange;
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

        /// <summary>살아 있는 유닛. ID 오름차순을 유지한다 (추가는 언제나 끝에, 새 ID가 가장 크다).</summary>
        public List<Unit> Units { get; } = new List<Unit>();

        public WorldMode Mode { get; set; }
        public TurnSide Turn { get; set; }
        public int Round { get; set; }

        /// <summary>지금 전투에 들어와 있는 적 ID (오름차순).</summary>
        public List<int> Combatants { get; } = new List<int>();

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

        public Unit UnitAt(GridPos cell)
        {
            foreach (var unit in Units) if (unit.Position == cell) return unit;
            return null;
        }

        public bool IsOccupied(GridPos cell) => UnitAt(cell) != null;

        public Unit AddUnit(UnitStats stats, GridPos position)
        {
            var unit = new Unit(NextEntityId++, stats.Kind, position, stats.MaxHp, stats.MaxHp,
                stats.MaxStability, stats.MaxStability, stats.VisionRange);
            Units.Add(unit);
            return unit;
        }
    }
}
