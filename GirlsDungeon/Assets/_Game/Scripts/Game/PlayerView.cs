using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 플레이어 유닛을 그린다. 규칙 사건을 받아 칸 사이를 차례로 옮길 뿐, 위치를 정하지 않는다.
    /// 전투 이동처럼 한 번에 여러 걸음이 오면 줄을 세워 한 칸씩 걷는다.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private readonly Queue<Vector3> pendingSteps = new Queue<Vector3>();
        private Simulation simulation;
        private Vector3 fromPosition;
        private Vector3 toPosition;
        private float stepSeconds;
        private float elapsed;
        private bool isStepping;

        /// <summary>걷는 중이거나 걸을 칸이 남아 있다.</summary>
        public bool IsMoving => isStepping || pendingSteps.Count > 0;

        public void Bind(Simulation newSimulation, float newStepSeconds)
        {
            Unbind();
            simulation = newSimulation;
            stepSeconds = newStepSeconds;
            simulation.EventsPublished += HandleEvents;

            pendingSteps.Clear();
            toPosition = fromPosition = GridSpace.ToWorld(simulation.World.Player.Position);
            transform.position = toPosition;
            isStepping = false;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (simulation != null) simulation.EventsPublished -= HandleEvents;
            simulation = null;
        }

        private void HandleEvents(IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events)
            {
                if (gameEvent is UnitMoved moved && moved.UnitId == simulation.World.PlayerId)
                    pendingSteps.Enqueue(GridSpace.ToWorld(moved.To));
                // 낭떠러지에 떨어졌다가 다시 일어남: 떨어진 칸까지 걸어간 뒤 일어난 칸으로 바로 옮긴다.
                if (gameEvent is PlayerFell fell && fell.To != fell.From)
                    pendingSteps.Enqueue(GridSpace.ToWorld(fell.To) + SnapMarker);
                // 새 층: 계단까지 걸어간 뒤 새 시작 칸으로 바로 옮긴다.
                if (gameEvent is FloorChanged floor)
                    pendingSteps.Enqueue(GridSpace.ToWorld(floor.StartCell) + SnapMarker);
            }
            if (!isStepping) BeginNextStep();
        }

        /// <summary>큐에서 "걷지 않고 바로 옮김"을 표시하는 높이 (칸 위치는 늘 y = 0).</summary>
        private static readonly Vector3 SnapMarker = Vector3.up * 1000f;

        private void BeginNextStep()
        {
            if (pendingSteps.Count == 0) return;
            fromPosition = transform.position;
            toPosition = pendingSteps.Dequeue();
            if (toPosition.y > SnapMarker.y * 0.5f)
            {
                toPosition -= SnapMarker;
                fromPosition = toPosition;
            }
            elapsed = 0f;
            isStepping = true;
        }

        private void Update()
        {
            if (!isStepping) return;

            elapsed += Time.deltaTime;
            float progress = stepSeconds > 0f ? Mathf.Clamp01(elapsed / stepSeconds) : 1f;
            transform.position = Vector3.Lerp(fromPosition, toPosition, progress);
            if (progress < 1f) return;

            isStepping = false;
            BeginNextStep();
        }
    }
}
