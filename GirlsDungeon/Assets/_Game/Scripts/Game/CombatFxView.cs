using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 공격 연출: 쏜 유닛 → 맞은 유닛 탄도 선, 맞은 유닛 위 피해 숫자, 맞은 유닛 흔들림.
    /// 사건 재생기(EventPlayer)의 UnitAttacked를 받아 그린다. 숫자 · 글은 사건과 텍스트 표에서만 온다.
    /// </summary>
    public sealed class CombatFxView : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Material tracerMaterial;
        [Tooltip("Hidden label copied for each floating damage number.")]
        [SerializeField] private UnityEngine.UI.Text popupTemplate;
        [SerializeField] private float tracerWidth = 0.06f;
        [SerializeField] private float tracerHeight = 1.1f;
        [SerializeField] private float popupHeight = 2.3f;
        [SerializeField] private float popupRise = 60f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color critColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color playerHurtColor = new Color(1f, 0.4f, 0.35f);

        private sealed class Popup
        {
            public UnityEngine.UI.Text Label;
            public Vector3 Anchor;
            public float Age;
            /// <summary>화면에서 아래로 내리는 픽셀 (같은 유닛의 두 번째 글줄).</summary>
            public float Offset;
        }

        private sealed class Tracer
        {
            public LineRenderer Line;
            public float Age;
        }

        private readonly List<Popup> popups = new List<Popup>();
        private readonly List<Tracer> tracers = new List<Tracer>();
        private IEventSource source;
        private GameBalance balance;
        private UnitsView unitsView;
        private PlayerView playerView;
        private bool skipTracers;

        public int ActivePopupCount => popups.Count;
        public int ActiveTracerCount => tracers.Count;
        public string LastPopupText { get; private set; }

        public void Bind(IEventSource newSource, GameBalance newBalance, UnitsView newUnitsView, PlayerView newPlayerView)
        {
            Unbind();
            source = newSource;
            balance = newBalance;
            unitsView = newUnitsView;
            playerView = newPlayerView;
            source.EventsPublished += HandleEvents;
            if (popupTemplate != null) popupTemplate.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (source != null) source.EventsPublished -= HandleEvents;
            source = null;
            foreach (var popup in popups) if (popup.Label != null) Destroy(popup.Label.gameObject);
            foreach (var tracer in tracers) if (tracer.Line != null) Destroy(tracer.Line.gameObject);
            popups.Clear();
            tracers.Clear();
        }

        private void HandleEvents(IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events)
            {
                // 수류탄 · 밀치기로 맞은 것은 탄도 선을 긋지 않는다 — 사격 · 근접만 선을 긋는다. 쓰면 차례가 끝나므로 다음 차례 표시에서 풀린다.
                if (gameEvent is AreaExploded || gameEvent is PushUsed) skipTracers = true;
                if (gameEvent is TurnStarted || gameEvent is CombatEnded || gameEvent is OverwatchTriggered) skipTracers = false;
                if (!(gameEvent is UnitAttacked attacked)) continue;
                var from = ViewOf(attacked.AttackerId, attacked.AttackerKind);
                var to = ViewOf(attacked.TargetId, attacked.TargetKind);
                if (to == null) continue;

                if (!skipTracers && from != null && from != to) AddTracer(Ground(from.position), Ground(to.position));
                bool hurtPlayer = attacked.TargetKind == UnitKind.Player;
                string text = attacked.IsCrit ? Text.Get("ui.fx.crit", attacked.Damage) : Text.Get("ui.fx.damage", attacked.Damage);
                AddPopup(Ground(to.position), text, attacked.IsCrit ? critColor : hurtPlayer ? playerHurtColor : normalColor, 0f);
                if (attacked.StabilityBroken) AddPopup(Ground(to.position), Text.Get("ui.fx.stability_break"), normalColor, -40f);

                if (hurtPlayer) playerView.Shake();
                else unitsView.Shake(attacked.TargetId);
            }
        }

        private Transform ViewOf(int unitId, UnitKind kind) => kind == UnitKind.Player ? playerView.transform : unitsView.ViewOf(unitId);

        /// <summary>그림의 바닥 칸 위치 (적 그림은 몸 가운데 높이라 바닥으로 내린다).</summary>
        private static Vector3 Ground(Vector3 position) => new Vector3(position.x, 0f, position.z);

        private void AddTracer(Vector3 from, Vector3 to)
        {
            var line = new GameObject("Tracer", typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(transform, false);
            line.sharedMaterial = tracerMaterial;
            line.positionCount = 2;
            line.SetPosition(0, from + Vector3.up * tracerHeight);
            line.SetPosition(1, to + Vector3.up * tracerHeight);
            line.startWidth = line.endWidth = tracerWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracers.Add(new Tracer { Line = line });
        }

        private void AddPopup(Vector3 anchor, string text, Color color, float offset)
        {
            if (popupTemplate == null) return;
            var label = Instantiate(popupTemplate, popupTemplate.transform.parent);
            label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            LastPopupText = text;
            var popup = new Popup { Label = label, Anchor = anchor + Vector3.up * popupHeight, Offset = offset };
            popups.Add(popup);
            Place(popup);
        }

        private void Update()
        {
            if (balance == null) return;
            for (int i = tracers.Count - 1; i >= 0; i--)
            {
                var tracer = tracers[i];
                tracer.Age += Time.deltaTime;
                if (tracer.Age < balance.TracerSeconds) continue;
                Destroy(tracer.Line.gameObject);
                tracers.RemoveAt(i);
            }
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var popup = popups[i];
                popup.Age += Time.deltaTime;
                if (popup.Age >= balance.DamagePopupSeconds)
                {
                    Destroy(popup.Label.gameObject);
                    popups.RemoveAt(i);
                    continue;
                }
                Place(popup);
            }
        }

        /// <summary>월드 위치를 화면 좌표로 바꿔 숫자를 붙이고, 시간이 지날수록 위로 올리며 흐리게 한다.</summary>
        private void Place(Popup popup)
        {
            float extraOffset = popup.Offset;
            var camera = viewCamera != null ? viewCamera : Camera.main;
            if (camera == null) return;
            var screen = camera.WorldToScreenPoint(popup.Anchor);
            bool behind = screen.z < 0f;
            popup.Label.enabled = !behind;
            float t = Mathf.Clamp01(popup.Age / balance.DamagePopupSeconds);
            var canvasRect = (RectTransform)popup.Label.canvas.transform;
            float scale = canvasRect.lossyScale.x > 0f ? canvasRect.lossyScale.x : 1f;
            popup.Label.rectTransform.position = new Vector3(screen.x, screen.y + (popupRise * t + extraOffset) * scale, 0f);
            var color = popup.Label.color;
            color.a = 1f - t * t;
            popup.Label.color = color;
        }
    }
}
