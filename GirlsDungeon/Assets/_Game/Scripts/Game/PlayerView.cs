using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 플레이어 유닛을 그린다. 규칙 사건을 받아 칸 사이를 차례로 옮길 뿐, 위치를 정하지 않는다.
    /// 전투 이동처럼 한 번에 여러 걸음이 오면 줄을 세워 한 칸씩 걷는다.
    /// 모습: 그림 목록(UnitVisualCatalog)에서 플레이어 그림 ID의 프리팹이 있으면 그 모델을, 없으면 그레이박스 캡슐을 그린다.
    /// 모델은 걷는 방향을 바라보고, Animator에 속도 값(예: "Speed")이 있으면 걷는 동안 1 · 서 있으면 0을 넣는다.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        [Tooltip("Greybox shape drawn when the visual id has no prefab.")]
        [SerializeField] private Transform fallbackBody;
        [SerializeField] private float turnSharpness = 14f;

        private readonly Queue<Vector3> pendingSteps = new Queue<Vector3>();
        private IEventSource simulation;
        private GameBalance balance;
        private Vector3 fromPosition;
        private Vector3 toPosition;
        private float stepSeconds;
        private float elapsed;
        private bool isStepping;
        private float shakeLeft;

        private Transform modelPivot;
        private Animator modelAnimator;
        private int speedHash;
        private bool hasSpeedParameter;
        private float facingYaw;
        private float yawOffset;

        /// <summary>걷는 중이거나 걸을 칸이 남아 있다.</summary>
        public bool IsMoving => isStepping || pendingSteps.Count > 0;

        /// <summary>그림 목록의 모델을 쓰고 있다 (아니면 캡슐).</summary>
        public bool UsesModel => modelPivot != null;

        /// <summary>지금 그리는 모델 이름 (캡슐이면 null).</summary>
        public string ModelName { get; private set; }

        public float FacingYaw => facingYaw;

        /// <summary>맞았을 때 잠깐 흔든다 (모습만).</summary>
        public void Shake()
        {
            shakeLeft = balance.ShakeSeconds;
        }

        public void Bind(IEventSource newSource, GameBalance newBalance, UnitVisualEntry visual)
        {
            Unbind();
            simulation = newSource;
            balance = newBalance;
            stepSeconds = balance.StepSeconds;
            simulation.EventsPublished += HandleEvents;

            pendingSteps.Clear();
            toPosition = fromPosition = GridSpace.ToWorld(simulation.World.Player.Position);
            transform.position = toPosition;
            isStepping = false;
            ApplyVisual(visual);
        }

        /// <summary>모델을 붙이거나(프리팹이 있으면) 캡슐로 돌아간다. 새 판마다 다시 만든다.</summary>
        private void ApplyVisual(UnitVisualEntry visual)
        {
            if (modelPivot != null) Destroy(modelPivot.gameObject);
            modelPivot = null;
            modelAnimator = null;
            hasSpeedParameter = false;
            ModelName = null;

            bool useModel = visual != null && visual.prefab != null;
            if (fallbackBody != null) fallbackBody.gameObject.SetActive(!useModel);
            if (!useModel) return;

            modelPivot = new GameObject("Model").transform;
            modelPivot.SetParent(transform, false);
            var model = Instantiate(visual.prefab, modelPivot, false);
            model.transform.localPosition = new Vector3(0f, visual.heightOffset, 0f);
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * visual.scale;
            // 모델에 콜라이더가 있으면 클릭 판정(바닥 평면)과 상관없지만 물리 비용만 들므로 끈다.
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            ModelName = visual.prefab.name;
            yawOffset = visual.yawOffset;
            facingYaw = 0f;
            modelPivot.localRotation = Quaternion.Euler(0f, facingYaw + yawOffset, 0f);

            modelAnimator = model.GetComponentInChildren<Animator>();
            // 컨트롤러가 없는 Animator(애니메이션 없는 모델)는 parameters를 읽으면 경고가 나므로 건너뛴다.
            if (modelAnimator != null && modelAnimator.runtimeAnimatorController != null && !string.IsNullOrEmpty(visual.speedParameter))
            {
                foreach (var parameter in modelAnimator.parameters)
                {
                    if (parameter.type != AnimatorControllerParameterType.Float || parameter.name != visual.speedParameter) continue;
                    speedHash = parameter.nameHash;
                    hasSpeedParameter = true;
                }
            }
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
            // 걷는 방향을 바라본다 (순간 이동이면 그대로).
            var direction = toPosition - fromPosition;
            if (direction.sqrMagnitude > 0.0001f) facingYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            elapsed = 0f;
            isStepping = true;
        }

        private void Update()
        {
            UpdateModel();
            UpdateShake();
            if (!isStepping) return;

            elapsed += Time.deltaTime;
            float progress = stepSeconds > 0f ? Mathf.Clamp01(elapsed / stepSeconds) : 1f;
            transform.position = Vector3.Lerp(fromPosition, toPosition, progress);
            if (progress < 1f) return;

            isStepping = false;
            BeginNextStep();
        }

        private void UpdateModel()
        {
            if (modelPivot == null) return;
            var target = Quaternion.Euler(0f, facingYaw + yawOffset, 0f);
            modelPivot.localRotation = Quaternion.Slerp(modelPivot.localRotation, target, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
            if (hasSpeedParameter) modelAnimator.SetFloat(speedHash, IsMoving ? 1f : 0f);
        }

        /// <summary>흔들림은 보이는 모습(모델 받침 또는 캡슐)만 옆으로 움직인다.</summary>
        private void UpdateShake()
        {
            var body = modelPivot != null ? modelPivot : fallbackBody;
            if (body == null) return;
            if (shakeLeft <= 0f)
            {
                body.localPosition = new Vector3(0f, body.localPosition.y, 0f);
                return;
            }
            shakeLeft -= Time.deltaTime;
            float side = Mathf.Sin(Time.time * 90f) * balance.ShakeDistance;
            body.localPosition = new Vector3(side, body.localPosition.y, -side);
        }
    }
}
