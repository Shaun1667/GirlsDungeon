using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>플레이어 유닛을 그린다. 규칙 사건을 받아 칸 사이를 부드럽게 옮길 뿐, 위치를 정하지 않는다.</summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private Simulation simulation;
        private Vector3 fromPosition;
        private Vector3 toPosition;
        private float stepSeconds;
        private float elapsed;
        private bool isMoving;

        public bool IsMoving => isMoving;

        public void Bind(Simulation newSimulation, float newStepSeconds)
        {
            Unbind();
            simulation = newSimulation;
            stepSeconds = newStepSeconds;
            simulation.EventsPublished += HandleEvents;

            toPosition = fromPosition = GridSpace.ToWorld(simulation.World.Player.Position);
            transform.position = toPosition;
            isMoving = false;
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
                    BeginStep(moved.To);
            }
        }

        private void BeginStep(GridPos to)
        {
            fromPosition = transform.position;
            toPosition = GridSpace.ToWorld(to);
            elapsed = 0f;
            isMoving = true;
        }

        private void Update()
        {
            if (!isMoving) return;

            elapsed += Time.deltaTime;
            float progress = stepSeconds > 0f ? Mathf.Clamp01(elapsed / stepSeconds) : 1f;
            transform.position = Vector3.Lerp(fromPosition, toPosition, progress);
            if (progress >= 1f) isMoving = false;
        }
    }
}
