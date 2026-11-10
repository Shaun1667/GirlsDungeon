using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 플레이어가 아닌 유닛을 그린다. 지금 보이는 칸(Vision)에 있거나 전투에 들어온 적만 화면에 나타난다.
    /// </summary>
    public sealed class UnitsView : MonoBehaviour
    {
        [SerializeField] private Material enemyMaterial;
        [Tooltip("Material for assault units (melee pushers).")]
        [SerializeField] private Material assaultMaterial;
        [SerializeField] private float bodyHeight = 1f;

        private readonly Dictionary<int, GameObject> views = new Dictionary<int, GameObject>();
        private Simulation simulation;
        private bool[] visible;

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

            var world = simulation.World;
            var player = world.Player;
            visible = player != null ? Vision.Compute(world.Map, player.Position, player.VisionRange) : new bool[world.Map.Width * world.Map.Height];
            foreach (var unit in world.Units)
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
            body.GetComponent<MeshRenderer>().sharedMaterial = unit.Kind == UnitKind.Assault && assaultMaterial != null ? assaultMaterial : enemyMaterial;
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
                    case VisibilityChanged changed:
                        visible = changed.Visible;
                        break;
                }
            }
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            var world = simulation.World;
            // 판 중간에 생긴 유닛(디버그 소환 등)도 그린다.
            foreach (var unit in world.Units)
                if (!unit.IsPlayer && !views.ContainsKey(unit.Id)) CreateView(unit);
            foreach (var pair in views)
            {
                var unit = world.FindUnit(pair.Key);
                bool shown = unit != null && (world.Combatants.Contains(unit.Id) || Vision.IsSet(visible, world.Map, unit.Position));
                pair.Value.SetActive(shown);
            }
        }
    }
}
