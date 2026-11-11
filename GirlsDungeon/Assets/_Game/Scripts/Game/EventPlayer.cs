using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 사건 재생기. 규칙(Simulation)은 커맨드 하나의 결과를 한꺼번에 내지만, 화면은 이것을 거쳐 사건을 하나씩 시간을 두고 받는다.
    /// 적 차례: "적 차례" 표시 → 적마다 걸음 한 칸씩 → 사격(탄도 · 피해 숫자) → … → 내 차례.
    /// 사건마다 기다리는 시간은 GameBalance 3-14 ~ 3-19 (규칙에는 영향 없음). 재생 중에는 입력을 받지 않는다 (MoveController · SkillBarView가 IsPlaying을 본다).
    /// 상태(World)는 이미 끝난 결과라서, 화면은 위치 · 피해를 사건에서 읽어야 한다.
    /// </summary>
    public sealed class EventPlayer : MonoBehaviour, IEventSource
    {
        private readonly Queue<GameEvent> queue = new Queue<GameEvent>();
        private readonly GameEvent[] single = new GameEvent[1];
        private Simulation simulation;
        private GameBalance balance;
        private float wait;

        public World World => simulation?.World;

        /// <summary>사건을 하나씩 내보낸다 (목록에는 늘 사건 하나).</summary>
        public event Action<IReadOnlyList<GameEvent>> EventsPublished;

        /// <summary>카메라가 따라갈 유닛이 바뀌었다 (적 차례에 움직이는 적, 끝나면 플레이어).</summary>
        public event Action<int> FocusChanged;

        /// <summary>내보낼 사건이 남았거나 앞 사건의 연출 시간이 남았다.</summary>
        public bool IsPlaying => queue.Count > 0 || wait > 0f;

        public int PendingCount => queue.Count;

        /// <summary>
        /// 화면에 지금 보이는 차례. World.Turn은 이미 끝난 결과(적 차례가 끝나 다시 내 차례)라서,
        /// 재생 중 "적 차례" 표시 · 카메라는 이 값을 쓴다.
        /// </summary>
        public TurnSide ShownTurn { get; private set; }

        public void Bind(Simulation newSimulation, GameBalance newBalance)
        {
            Unbind();
            simulation = newSimulation;
            balance = newBalance;
            queue.Clear();
            wait = 0f;
            ShownTurn = TurnSide.Player;
            simulation.EventsPublished += Enqueue;
        }

        /// <summary>남은 사건을 기다리지 않고 모두 내보낸다 (새 판 · 디버그용).</summary>
        public void Flush()
        {
            while (queue.Count > 0) Publish(queue.Dequeue());
            wait = 0f;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (simulation != null) simulation.EventsPublished -= Enqueue;
            simulation = null;
        }

        private void Enqueue(IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events) queue.Enqueue(gameEvent);
            // 기다릴 것이 없으면 바로 시작한다 — 같은 프레임 안에서 화면이 첫 사건을 받는다.
            // 사건을 내보내는 도중(화면이 그 안에서 커맨드를 낸 경우)에는 지금 돌고 있는 반복이 이어서 처리한다.
            if (wait <= 0f && !publishing) PlayUntilWaiting();
        }

        private bool publishing;

        private void Update()
        {
            if (wait > 0f)
            {
                wait -= Time.deltaTime;
                if (wait > 0f) return;
                wait = 0f;
            }
            PlayUntilWaiting();
        }

        /// <summary>기다리는 시간이 생길 때까지 사건을 내보낸다.</summary>
        private void PlayUntilWaiting()
        {
            publishing = true;
            try
            {
                while (wait <= 0f && queue.Count > 0)
                {
                    var gameEvent = queue.Dequeue();
                    Publish(gameEvent);
                    wait = DurationOf(gameEvent);
                }
            }
            finally
            {
                publishing = false;
            }
        }

        private void Publish(GameEvent gameEvent)
        {
            if (gameEvent is TurnStarted started) ShownTurn = started.Side;
            if (gameEvent is CombatEnded || gameEvent is FloorChanged) ShownTurn = TurnSide.Player;
            int focus = FocusOf(gameEvent);
            if (focus >= 0) FocusChanged?.Invoke(focus);
            single[0] = gameEvent;
            EventsPublished?.Invoke(single);
        }

        /// <summary>적 차례에 무엇을 따라갈지. -1이면 그대로.</summary>
        private int FocusOf(GameEvent gameEvent)
        {
            var world = simulation.World;
            switch (gameEvent)
            {
                case UnitMoved moved when moved.UnitId != world.PlayerId && ShownTurn == TurnSide.Enemies:
                    return moved.UnitId;
                case UnitAttacked attacked when attacked.AttackerKind != UnitKind.Player:
                    return attacked.AttackerId;
                case TurnStarted started when started.Side == TurnSide.Player:
                case CombatEnded _:
                case FloorChanged _:
                    return world.PlayerId;
                default:
                    return -1;
            }
        }

        /// <summary>사건 뒤에 기다릴 시간 (초). 걸음은 화면이 칸 사이를 걷는 시간과 같다.</summary>
        private float DurationOf(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case TurnStarted started:
                    return started.Side == TurnSide.Enemies ? balance.EnemyTurnBannerSeconds : 0f;
                case UnitMoved _:
                    return balance.StepSeconds;
                case UnitAttacked _:
                    return balance.AttackPauseSeconds;
                case OverwatchTriggered _:
                case OverwatchSet _:
                case AreaExploded _:
                case PushUsed _:
                    return balance.ActionPauseSeconds;
                case UnitDied _:
                    return balance.ActionPauseSeconds;
                default:
                    return 0f;
            }
        }
    }
}
