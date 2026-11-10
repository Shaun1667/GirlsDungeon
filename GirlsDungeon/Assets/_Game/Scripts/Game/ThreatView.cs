using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 적의 위협 칸 표시: 경계 중인 적의 경계 칸(노랑)과 감시 중인 적의 감시 칸(빨강).
    /// 지금 보이는 적만, 본 칸 중 걸을 수 있는 칸만 표시한다. 칸 판정은 규칙(Alert)과 같은 함수를 쓴다.
    /// </summary>
    public sealed class ThreatView : MonoBehaviour
    {
        [SerializeField] private RangeView guardView;
        [SerializeField] private RangeView watchView;

        private Simulation simulation;
        private CombatSettings settings;

        public int GuardCellCount => guardView.ShownCount;
        public int WatchCellCount => watchView.ShownCount;

        public void Bind(Simulation newSimulation, CombatSettings newSettings)
        {
            Unbind();
            simulation = newSimulation;
            settings = newSettings;
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
            var world = simulation.World;
            var player = world.Player;
            var map = world.Map;
            if (player == null || world.Mode == WorldMode.Defeat)
            {
                guardView.Hide();
                watchView.Hide();
                return;
            }

            var visible = Vision.Compute(map, player.Position, player.VisionRange);
            var guardCells = new HashSet<GridPos>();
            var watchCells = new HashSet<GridPos>();
            foreach (var unit in world.Units)
            {
                if (unit.IsPlayer || !Vision.IsSet(visible, map, unit.Position)) continue;
                if (!unit.Guarding && !unit.Overwatching) continue;

                int reach = unit.Guarding ? settings.GuardRadius : unit.AttackRange;
                for (int y = unit.Position.Y - reach; y <= unit.Position.Y + reach; y++)
                {
                    for (int x = unit.Position.X - reach; x <= unit.Position.X + reach; x++)
                    {
                        var cell = new GridPos(x, y);
                        if (!map.IsWalkable(cell) || !Vision.IsSet(world.Explored, map, cell)) continue;
                        if (unit.Guarding && Alert.InGuardZone(settings, unit, cell)) guardCells.Add(cell);
                        else if (unit.Overwatching && Alert.InWatch(world, unit, cell)) watchCells.Add(cell);
                    }
                }
            }

            guardView.Show(Sorted(guardCells));
            watchView.Show(Sorted(watchCells));
        }

        private static List<GridPos> Sorted(HashSet<GridPos> cells)
        {
            var list = new List<GridPos>(cells);
            list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return list;
        }
    }
}
