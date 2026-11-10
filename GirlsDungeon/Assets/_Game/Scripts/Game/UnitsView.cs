using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 플레이어가 아닌 유닛을 그린다. 지금 보이거나 전투에 들어온 적만 화면에 나타난다.
    /// 시야 판정은 규칙의 Sight를 그대로 쓴다.
    /// </summary>
    public sealed class UnitsView : MonoBehaviour
    {
        [SerializeField] private Material enemyMaterial;
        [SerializeField] private float bodyHeight = 1f;

        private readonly Dictionary<int, GameObject> views = new Dictionary<int, GameObject>();
        private Simulation simulation;

        public int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (var view in views.Values) if (view.activeSelf) count++;
                return count;
            }
        }

        public void Bind(Simulation newSimulation)
        {
            Unbind();
            simulation = newSimulation;
            simulation.EventsPublished += HandleEvents;

            foreach (var unit in simulation.World.Units)
                if (!unit.IsPlayer) CreateView(unit);
            RefreshVisibility();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (simulation != null) simulation.EventsPublished -= HandleEvents;
            simulation = null;
            foreach (var view in views.Values) if (view != null) Destroy(view);
            views.Clear();
        }

        private void CreateView(Unit unit)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = $"Unit {unit.Id} {unit.Kind}";
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = enemyMaterial;
            body.transform.SetParent(transform, false);
            body.transform.position = GridSpace.ToWorld(unit.Position) + Vector3.up * bodyHeight;
            views.Add(unit.Id, body);
        }

        private void HandleEvents(IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events)
            {
                switch (gameEvent)
                {
                    case UnitMoved moved when views.TryGetValue(moved.UnitId, out var view):
                        view.transform.position = GridSpace.ToWorld(moved.To) + Vector3.up * bodyHeight;
                        break;
                    case UnitDied died when views.TryGetValue(died.UnitId, out var deadView):
                        Destroy(deadView);
                        views.Remove(died.UnitId);
                        break;
                }
            }
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            var world = simulation.World;
            var player = world.Player;
            foreach (var pair in views)
            {
                var unit = world.FindUnit(pair.Key);
                bool visible = unit != null && player != null
                    && (world.Combatants.Contains(unit.Id) || Sight.CanSee(world.Map, player.Position, unit.Position, player.VisionRange));
                pair.Value.SetActive(visible);
            }
        }
    }
}
