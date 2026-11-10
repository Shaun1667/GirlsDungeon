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
        /// <summary>돌격병: 바로 옆에서 근접 공격하고 맞은 상대를 1칸 민다.</summary>
        Assault = 2,
    }

    /// <summary>탐험과 턴 전투. 저장에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum WorldMode : byte
    {
        Exploration = 0,
        Combat = 1,
        /// <summary>플레이어가 쓰러졌다. 더 이상 행동할 수 없다.</summary>
        Defeat = 2,
        /// <summary>마지막 층의 계단을 밟았다 — 판 승리. 더 이상 행동할 수 없다.</summary>
        Victory = 3,
    }

    /// <summary>전투 중 지금 움직이는 쪽.</summary>
    public enum TurnSide : byte
    {
        Player = 0,
        Enemies = 1,
    }

    /// <summary>
    /// 맵 위의 유닛. 다른 곳에서는 고유 ID로 참조한다.
    /// 능력치는 만들 때 CombatSettings 표에서 복사한다 — 판 안 성장으로 유닛마다 바뀔 수 있다 (예: 이동 범위).
    /// </summary>
    public sealed class Unit
    {
        public int Id { get; }
        public UnitKind Kind { get; }
        public GridPos Position { get; set; }
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int MaxStability { get; set; }
        public int Stability { get; set; }
        public int VisionRange { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int MoveRange { get; set; }
        public int AttackRange { get; set; }
        public int StabilityDamage { get; set; }
        public int CritPercent { get; set; }
        public int CritMultiplierPercent { get; set; }

        /// <summary>공격이 맞으면 상대를 공격자 반대쪽으로 미는 칸 수 (0 = 밀지 않음, 돌격병 1).</summary>
        public int PushOnHit { get; set; }

        /// <summary>경계 상태 (적). 공격받거나 플레이어가 경계 칸에 들어오기 전까지 행동하지 않는다.</summary>
        public bool Guarding { get; set; }

        /// <summary>감시 중. 상대가 사거리 · 사선 안에서 움직이면 한 번 반응 사격한다.</summary>
        public bool Overwatching { get; set; }

        public bool IsPlayer => Kind == UnitKind.Player;

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
        /// <summary>지금 층 (0 = 1층).</summary>
        public int FloorIndex { get; set; }

        /// <summary>이번 판에 쓰러뜨린 적 수 (결과 화면).</summary>
        public int Kills { get; set; }
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

        /// <summary>이번 플레이어 차례에 이동을 썼다 (전투 중).</summary>
        public bool PlayerMoved { get; set; }

        /// <summary>이번 플레이어 차례에 행동(사격)을 썼다 (전투 중).</summary>
        public bool PlayerActed { get; set; }

        /// <summary>수류탄 스킬을 다시 쓸 수 있을 때까지 남은 라운드 (0 = 지금 사용 가능). 전투가 끝나면 0.</summary>
        public int GrenadeCooldown { get; set; }

        /// <summary>밀치기 스킬을 다시 쓸 수 있을 때까지 남은 라운드 (0 = 지금 사용 가능). 전투가 끝나면 0.</summary>
        public int PushCooldown { get; set; }

        /// <summary>한 번이라도 본 칸 (인덱스 = x + y * Width). 지금 보이는 칸은 저장하지 않고 Vision으로 다시 계산한다.</summary>
        public bool[] Explored { get; set; }

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
            var unit = new Unit(NextEntityId++, stats.Kind, position)
            {
                MaxHp = stats.MaxHp,
                Hp = stats.MaxHp,
                MaxStability = stats.MaxStability,
                Stability = stats.MaxStability,
                VisionRange = stats.VisionRange,
                Attack = stats.Attack,
                Defense = stats.Defense,
                MoveRange = stats.MoveRange,
                AttackRange = stats.AttackRange,
                StabilityDamage = stats.StabilityDamage,
                CritPercent = stats.CritPercent,
                CritMultiplierPercent = stats.CritMultiplierPercent,
                PushOnHit = stats.PushOnHit,
            };
            Units.Add(unit);
            return unit;
        }
    }
}
