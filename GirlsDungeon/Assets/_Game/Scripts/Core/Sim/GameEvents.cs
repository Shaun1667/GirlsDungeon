using System.Collections.Generic;
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

    /// <summary>탐험이 턴 전투로 바뀌었다. 처음 들어온 적 ID.</summary>
    public sealed class CombatStarted : GameEvent
    {
        public IReadOnlyList<int> EnemyIds { get; }

        public CombatStarted(IReadOnlyList<int> enemyIds)
        {
            EnemyIds = enemyIds;
        }
    }

    /// <summary>전투 중 새로 보인 적이 합류했다.</summary>
    public sealed class EnemiesJoinedCombat : GameEvent
    {
        public IReadOnlyList<int> EnemyIds { get; }

        public EnemiesJoinedCombat(IReadOnlyList<int> enemyIds)
        {
            EnemyIds = enemyIds;
        }
    }

    public sealed class CombatEnded : GameEvent { }

    public sealed class TurnStarted : GameEvent
    {
        public TurnSide Side { get; }
        public int Round { get; }

        public TurnStarted(TurnSide side, int round)
        {
            Side = side;
            Round = round;
        }
    }

    /// <summary>체력 · 안정지수가 바뀌었다.</summary>
    public sealed class UnitStatsChanged : GameEvent
    {
        public int UnitId { get; }

        public UnitStatsChanged(int unitId)
        {
            UnitId = unitId;
        }
    }

    /// <summary>플레이어 시야를 다시 계산했다. 배열은 복사본 (인덱스 = x + y * Width).</summary>
    public sealed class VisibilityChanged : GameEvent
    {
        public bool[] Visible { get; }
        public bool[] Explored { get; }

        public VisibilityChanged(bool[] visible, bool[] explored)
        {
            Visible = visible;
            Explored = explored;
        }
    }

    /// <summary>사격이 맞았다 (항상 맞는다). 화면 기록 · 연출용 값.</summary>
    public sealed class UnitAttacked : GameEvent
    {
        public int AttackerId { get; }
        public UnitKind AttackerKind { get; }
        public int TargetId { get; }
        public UnitKind TargetKind { get; }
        public int Damage { get; }
        public bool IsCrit { get; }
        public Map.CoverLevel Cover { get; }
        public bool StabilityGuard { get; }
        public bool StabilityBroken { get; }

        public UnitAttacked(Unit attacker, Unit target, int damage, bool isCrit, Map.CoverLevel cover, bool stabilityGuard, bool stabilityBroken)
        {
            AttackerId = attacker.Id;
            AttackerKind = attacker.Kind;
            TargetId = target.Id;
            TargetKind = target.Kind;
            Damage = damage;
            IsCrit = isCrit;
            Cover = cover;
            StabilityGuard = stabilityGuard;
            StabilityBroken = stabilityBroken;
        }
    }

    /// <summary>범위 공격이 터졌다. 맞은 유닛마다 UnitAttacked가 뒤따른다.</summary>
    public sealed class AreaExploded : GameEvent
    {
        public int AttackerId { get; }
        public UnitKind AttackerKind { get; }
        public GridPos Center { get; }
        public IReadOnlyList<GridPos> Cells { get; }

        public AreaExploded(int attackerId, UnitKind attackerKind, GridPos center, IReadOnlyList<GridPos> cells)
        {
            AttackerId = attackerId;
            AttackerKind = attackerKind;
            Center = center;
            Cells = cells;
        }
    }

    /// <summary>경계 중이던 적이 깨어났다.</summary>
    public sealed class EnemiesAlerted : GameEvent
    {
        public IReadOnlyList<int> UnitIds { get; }
        public IReadOnlyList<UnitKind> Kinds { get; }

        public EnemiesAlerted(IReadOnlyList<int> unitIds, IReadOnlyList<UnitKind> kinds)
        {
            UnitIds = unitIds;
            Kinds = kinds;
        }
    }

    /// <summary>유닛이 감시를 걸었다.</summary>
    public sealed class OverwatchSet : GameEvent
    {
        public int UnitId { get; }
        public UnitKind Kind { get; }

        public OverwatchSet(int unitId, UnitKind kind)
        {
            UnitId = unitId;
            Kind = kind;
        }
    }

    /// <summary>감시가 발동했다. 바로 뒤에 UnitAttacked가 온다.</summary>
    public sealed class OverwatchTriggered : GameEvent
    {
        public int WatcherId { get; }
        public UnitKind WatcherKind { get; }
        public int TargetId { get; }

        public OverwatchTriggered(Unit watcher, Unit target)
        {
            WatcherId = watcher.Id;
            WatcherKind = watcher.Kind;
            TargetId = target.Id;
        }
    }

    /// <summary>플레이어가 쓰러졌다.</summary>
    public sealed class PlayerDefeated : GameEvent
    {
        public int KillerId { get; }

        public PlayerDefeated(int killerId)
        {
            KillerId = killerId;
        }
    }

    public sealed class UnitDied : GameEvent
    {
        public int UnitId { get; }
        public UnitKind Kind { get; }

        public UnitDied(int unitId, UnitKind kind)
        {
            UnitId = unitId;
            Kind = kind;
        }
    }
}
