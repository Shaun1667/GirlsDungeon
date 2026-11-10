using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 바닥 물건(무기 · 구급상자)을 도형으로 그린다. 한 번이라도 본 칸(World.Explored)에 있는 것만 보인다.
    /// 사건이 올 때마다 World.Items와 맞춘다 — 물건 수가 적어 통째로 맞춰도 가볍다.
    /// </summary>
    public sealed class ItemsView : MonoBehaviour
    {
        [SerializeField] private Material weaponMaterial;
        [SerializeField] private Material medkitMaterial;

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
            Sync();
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

        private void HandleEvents(IReadOnlyList<GameEvent> events)
        {
            Sync();
        }

        private void Sync()
        {
            var world = simulation.World;
            var alive = new HashSet<int>();
            foreach (var item in world.Items)
            {
                alive.Add(item.Id);
                if (!views.TryGetValue(item.Id, out var view))
                {
                    view = CreateView(item);
                    views.Add(item.Id, view);
                }
                bool known = world.Explored != null && Vision.IsSet(world.Explored, world.Map, item.Position);
                view.SetActive(known);
            }

            var gone = new List<int>();
            foreach (var pair in views) if (!alive.Contains(pair.Key)) gone.Add(pair.Key);
            foreach (int id in gone)
            {
                Destroy(views[id]);
                views.Remove(id);
            }
        }

        private GameObject CreateView(FloorItem item)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = $"Item {item.Id} {item.Kind}";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            bool isWeapon = item.Kind == ItemKind.Weapon;
            body.GetComponent<MeshRenderer>().sharedMaterial = isWeapon ? weaponMaterial : medkitMaterial;
            // 무기 = 납작하고 긴 막대, 구급상자 = 작은 정육면체.
            var size = isWeapon ? new Vector3(0.7f, 0.12f, 0.2f) : new Vector3(0.35f, 0.35f, 0.35f);
            body.transform.localScale = size;
            body.transform.position = GridSpace.ToWorld(item.Position) + Vector3.up * (size.y * 0.5f);
            if (isWeapon) body.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            return body;
        }
    }
}
