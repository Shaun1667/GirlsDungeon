using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 플레이어가 아닌 유닛을 그린다. 지금 보이는 칸(Vision)에 있거나 전투에 들어온 적만 화면에 나타난다.
    /// 사건 재생기(EventPlayer)에서 사건을 하나씩 받아 칸 사이를 걸어서 옮긴다 (한 칸 = 걸음 시간).
    /// </summary>
    public sealed class UnitsView : MonoBehaviour
    {
        [SerializeField] private Material enemyMaterial;
        [Tooltip("Material for assault units (melee pushers).")]
        [SerializeField] private Material assaultMaterial;
        [SerializeField] private float bodyHeight = 1f;

        private sealed class Body
        {
            public GameObject Object;
            public Vector3 From;
            public Vector3 To;
            public float Elapsed;
            public float ShakeLeft;
        }

        private readonly Dictionary<int, Body> views = new Dictionary<int, Body>();
        private IEventSource source;
        private GameBalance balance;
        private bool[] visible;

        public int VisibleCount
        {
            get
            {
                int count = 0;
                foreach (var view in views.Values) if (view.Object.activeSelf) count++;
                return count;
            }
        }

        /// <summary>그 유닛의 그림 (없으면 null). 카메라 · 탄도 연출이 쓴다.</summary>
        public Transform ViewOf(int unitId) => views.TryGetValue(unitId, out var body) ? body.Object.transform : null;

        /// <summary>맞은 유닛을 잠깐 흔든다.</summary>
        public void Shake(int unitId)
        {
            if (views.TryGetValue(unitId, out var body)) body.ShakeLeft = balance.ShakeSeconds;
        }

        public void Bind(IEventSource newSource, GameBalance newBalance)
        {
            Unbind();
            source = newSource;
            balance = newBalance;
            source.EventsPublished += HandleEvents;

            var world = source.World;
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
            if (source != null) source.EventsPublished -= HandleEvents;
            source = null;
            foreach (var view in views.Values) if (view.Object != null) Destroy(view.Object);
            views.Clear();
        }

        private void CreateView(Unit unit)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = $"Unit {unit.Id} {unit.Kind}";
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = unit.Kind == UnitKind.Assault && assaultMaterial != null ? assaultMaterial : enemyMaterial;
            body.transform.SetParent(transform, false);
            var position = CellPosition(unit.Position);
            body.transform.position = position;
            views.Add(unit.Id, new Body { Object = body, From = position, To = position, Elapsed = 1f });
        }

        private Vector3 CellPosition(Core.Grid.GridPos cell) => GridSpace.ToWorld(cell) + Vector3.up * bodyHeight;

        private void HandleEvents(IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events)
            {
                switch (gameEvent)
                {
                    case UnitMoved moved when views.TryGetValue(moved.UnitId, out var view):
                        // 이전 걸음이 덜 끝났으면 거기서 이어 걷는다.
                        view.From = view.Object.transform.position;
                        view.To = CellPosition(moved.To);
                        view.Elapsed = 0f;
                        break;
                    case UnitDied died when views.TryGetValue(died.UnitId, out var deadView):
                        Destroy(deadView.Object);
                        views.Remove(died.UnitId);
                        break;
                    case VisibilityChanged changed:
                        visible = changed.Visible;
                        break;
                    case FloorChanged _:
                        // 새 층: 이전 층의 적 모습을 모두 지운다. 새 적은 아래 RefreshVisibility가 만든다.
                        foreach (var oldView in views.Values) if (oldView.Object != null) Destroy(oldView.Object);
                        views.Clear();
                        break;
                }
            }
            RefreshVisibility();
        }

        private void Update()
        {
            if (balance == null) return;
            float step = balance.StepSeconds;
            foreach (var view in views.Values)
            {
                if (view.Elapsed < 1f)
                    view.Elapsed = step > 0f ? Mathf.Min(1f, view.Elapsed + Time.deltaTime / step) : 1f;
                var position = Vector3.Lerp(view.From, view.To, view.Elapsed);
                if (view.ShakeLeft > 0f)
                {
                    view.ShakeLeft -= Time.deltaTime;
                    float side = Mathf.Sin(Time.time * 90f) * balance.ShakeDistance;
                    position += new Vector3(side, 0f, -side);
                }
                view.Object.transform.position = position;
            }
        }

        private void RefreshVisibility()
        {
            var world = source.World;
            // 판 중간에 생긴 유닛(디버그 소환 등)도 그린다.
            foreach (var unit in world.Units)
                if (!unit.IsPlayer && !views.ContainsKey(unit.Id)) CreateView(unit);
            foreach (var pair in views)
            {
                var unit = world.FindUnit(pair.Key);
                // 규칙에서는 이미 쓰러졌지만 쓰러지는 사건이 아직 재생되지 않은 유닛은 지금 모습을 유지한다.
                if (unit == null) continue;
                bool shown = world.Combatants.Contains(unit.Id) || Vision.IsSet(visible, world.Map, CellOf(pair.Value));
                pair.Value.Object.SetActive(shown);
            }
        }

        /// <summary>화면에 보이는 칸 (걷는 중이면 도착할 칸). 규칙 위치는 재생보다 앞서 있을 수 있다.</summary>
        private static Core.Grid.GridPos CellOf(Body body) => GridSpace.ToCell(body.To);
    }
}
