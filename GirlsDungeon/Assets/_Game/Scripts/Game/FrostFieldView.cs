using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 서리밭 칸 표시 (3-17c): World.FrostFields의 칸을 하늘색 판으로. 본 칸 중 걸을 수 있는 칸만.
    /// 규칙 상태를 그대로 그린다 — 사건이 올 때마다 다시 맞춘다 (위협 칸처럼 Simulation에서 바로).
    /// </summary>
    public sealed class FrostFieldView : MonoBehaviour
    {
        [SerializeField] private RangeView fieldCells;

        private Simulation simulation;

        public int ShownCount => fieldCells != null ? fieldCells.ShownCount : 0;

        public void Bind(Simulation newSimulation)
        {
            Unbind();
            simulation = newSimulation;
            simulation.EventsPublished += HandleEvents;
            Refresh();
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
            Refresh();
        }

        private void Refresh()
        {
            if (fieldCells == null || simulation == null) return;
            var world = simulation.World;
            if (world.FrostFields.Count == 0)
            {
                fieldCells.Hide();
                return;
            }
            var cells = new HashSet<GridPos>();
            foreach (var field in world.FrostFields)
                foreach (var cell in FrostGuard.RangeCells(world, field.Center, field.Radius))
                    if (world.Map.IsWalkable(cell) && Core.Map.Vision.IsSet(world.Explored, world.Map, cell)) cells.Add(cell);
            fieldCells.Show(cells);
        }
    }
}
