using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 플레이어가 아닌 유닛을 그린다. 지금 보이는 칸(Vision)에 있거나 전투에 들어온 적만 화면에 나타난다.
    /// 사건 재생기(EventPlayer)에서 사건을 하나씩 받아 칸 사이를 걸어서 옮긴다 (한 칸 = 걸음 시간).
    /// 모습: 그림 목록(UnitVisualCatalog)에서 그 유닛 그림 ID의 프리팹이 있으면 모델을, 없으면 그레이박스 캡슐을 그린다 (3-15c).
    /// 모델은 걷는 동안 걷는 방향을, 서 있을 때는 플레이어를 바라본다. 쏘는 사건(UnitAttacked)이 오면 대상을 바라보고 사격 트리거를 켠다.
    /// </summary>
    public sealed class UnitsView : MonoBehaviour
    {
        [SerializeField] private Material enemyMaterial;
        [Tooltip("Material for assault units (melee pushers).")]
        [SerializeField] private Material assaultMaterial;
        [Tooltip("Shopkeeper (3-15b) — neutral until hit, then a boss-level enemy.")]
        [SerializeField] private Material shopkeeperMaterial;
        [SerializeField] private float bodyHeight = 1f;
        [Tooltip("How fast a model turns to its facing (higher = snappier).")]
        [SerializeField] private float turnSharpness = 12f;
        [Tooltip("Seconds a model keeps its walking pose and facing after a step, so steps in a row do not flicker to idle.")]
        [SerializeField] private float walkHoldSeconds = 0.2f;

        private sealed class Body
        {
            public GameObject Object;
            public Vector3 From;
            public Vector3 To;
            public float Elapsed;
            /// <summary>밀려서 미끄러지는 중 — 걷기 동작 · 걷는 방향 바라보기를 하지 않는다.</summary>
            public bool Sliding;
            public float ShakeLeft;
            // 모델을 쓰는 유닛만 (캡슐이면 null).
            public Transform Model;
            public float YawOffset;
            public Animator Animator;
            public int SpeedHash;
            public bool HasSpeed;
            public int FireHash;
            public bool HasFire;
            // 마지막 걸음이 끝난 뒤 지난 시간 — 걸음 사이의 짧은 틈에는 걷는 자세 · 방향을 유지한다.
            public float StillFor = 999f;
        }

        private readonly Dictionary<int, Body> views = new Dictionary<int, Body>();
        private IEventSource source;
        private GameBalance balance;
        private UnitVisualCatalog visuals;
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

        /// <summary>그 유닛이 모델로 그려지는지 (캡슐이면 false). 디버그 · 확인용.</summary>
        public bool UsesModel(int unitId) => views.TryGetValue(unitId, out var body) && body.Model != null;

        public void Bind(IEventSource newSource, GameBalance newBalance, UnitVisualCatalog newVisuals)
        {
            Unbind();
            source = newSource;
            balance = newBalance;
            visuals = newVisuals;
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
            var visual = visuals != null ? visuals.Find(balance.VisualId(unit.Kind)) : null;
            var view = new Body { Elapsed = 1f };
            GameObject body;
            if (visual != null && visual.prefab != null)
            {
                // 뿌리는 캡슐과 같은 높이(몸 가운데)에 두고, 모델은 그 아래 바닥에 발을 맞춘다 — 카메라 · 탄도 · 흔들림이 같은 점을 쓴다.
                body = new GameObject();
                var model = Instantiate(visual.prefab, body.transform, false);
                model.transform.localPosition = new Vector3(0f, visual.heightOffset - bodyHeight, 0f);
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * visual.scale;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
                view.Model = body.transform;
                view.YawOffset = visual.yawOffset;
                view.Animator = model.GetComponentInChildren<Animator>();
                if (view.Animator != null && view.Animator.runtimeAnimatorController != null)
                {
                    foreach (var parameter in view.Animator.parameters)
                    {
                        if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == visual.speedParameter)
                        {
                            view.SpeedHash = parameter.nameHash;
                            view.HasSpeed = true;
                        }
                        if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == visual.fireTrigger)
                        {
                            view.FireHash = parameter.nameHash;
                            view.HasFire = true;
                        }
                    }
                }
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(body.GetComponent<Collider>());
                body.GetComponent<MeshRenderer>().sharedMaterial = unit.Kind == UnitKind.Assault && assaultMaterial != null ? assaultMaterial
                    : unit.Kind == UnitKind.Shopkeeper && shopkeeperMaterial != null ? shopkeeperMaterial : enemyMaterial;
            }
            body.name = $"Unit {unit.Id} {unit.Kind}";
            body.transform.SetParent(transform, false);
            var position = CellPosition(unit.Position);
            body.transform.position = position;
            view.Object = body;
            view.From = position;
            view.To = position;
            if (view.Model != null && TryFacing(view, out var facing)) view.Model.rotation = facing;
            views.Add(unit.Id, view);
        }

        /// <summary>모델이 바라볼 방향: 걷는 중이면 걷는 방향, 서 있으면 플레이어 쪽.</summary>
        private bool TryFacing(Body view, out Quaternion facing)
        {
            facing = Quaternion.identity;
            Vector3 direction;
            if (view.Elapsed < 1f && view.Sliding) return false;
            if (view.Elapsed < 1f) direction = view.To - view.From;
            else if (view.StillFor < walkHoldSeconds) return false;
            else
            {
                var player = source != null ? source.World.Player : null;
                if (player == null) return false;
                direction = GridSpace.ToWorld(player.Position) - view.To;
            }
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return false;
            facing = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, view.YawOffset, 0f);
            return true;
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
                        view.Sliding = moved.Pushed;
                        break;
                    case UnitAttacked attacked when views.TryGetValue(attacked.AttackerId, out var shooter) && shooter.Model != null:
                        // 쏘는 순간 대상을 바로 바라보고 사격 동작을 켠다.
                        var target = attacked.TargetId == source.World.PlayerId ? source.World.Player : source.World.FindUnit(attacked.TargetId);
                        if (target != null)
                        {
                            var toTarget = GridSpace.ToWorld(target.Position) - shooter.To;
                            toTarget.y = 0f;
                            if (toTarget.sqrMagnitude > 0.0001f)
                                shooter.Model.rotation = Quaternion.LookRotation(toTarget) * Quaternion.Euler(0f, shooter.YawOffset, 0f);
                        }
                        if (shooter.HasFire) shooter.Animator.SetTrigger(shooter.FireHash);
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
                if (view.Model == null) continue;
                if (view.Sliding && view.Elapsed >= 1f)
                {
                    // 다 밀렸다: 걷기 여운 없이 바로 선 모습.
                    view.Sliding = false;
                    view.StillFor = walkHoldSeconds;
                }
                if (view.Sliding) view.StillFor = walkHoldSeconds;
                else view.StillFor = view.Elapsed < 1f ? 0f : view.StillFor + Time.deltaTime;
                if (TryFacing(view, out var facing))
                    view.Model.rotation = Quaternion.Slerp(view.Model.rotation, facing, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
                if (view.HasSpeed) view.Animator.SetFloat(view.SpeedHash, view.StillFor < walkHoldSeconds ? 1f : 0f);
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
                // 관측실(3-14e)을 쓴 층은 적이 모두 보인다.
                bool shown = world.Combatants.Contains(unit.Id) || world.EnemiesRevealed || Vision.IsSet(visible, world.Map, CellOf(pair.Value));
                pair.Value.Object.SetActive(shown);
            }
        }

        /// <summary>화면에 보이는 칸 (걷는 중이면 도착할 칸). 규칙 위치는 재생보다 앞서 있을 수 있다.</summary>
        private static Core.Grid.GridPos CellOf(Body body) => GridSpace.ToCell(body.To);
    }
}
