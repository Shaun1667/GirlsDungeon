using System;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>그림 ID 하나에 붙는 모델 (표 한 줄).</summary>
    [Serializable]
    public sealed class UnitVisualEntry
    {
        [Tooltip("Visual id referenced by data (units[].visualId).")]
        public string id;
        [Tooltip("Model prefab. Empty = draw the greybox shape instead.")]
        public GameObject prefab;
        [Tooltip("Uniform scale applied to the model.")]
        public float scale = 1f;
        [Tooltip("Raise or lower the model so its feet touch the floor.")]
        public float heightOffset;
        [Tooltip("Extra yaw (degrees) when the model's front is not +Z.")]
        public float yawOffset;
        [Tooltip("Animator float parameter set to the walking speed (0 idle, 1 walking). Empty = none.")]
        public string speedParameter = "Speed";
    }

    /// <summary>
    /// 그림 목록의 한 곳 (아트 규칙: 데이터는 그림을 ID로만 참조하고, 프리팹이 없으면 도형으로 그린다).
    /// 모델을 바꾸려면 여기 그 ID 줄에 프리팹만 넣는다 — 코드 · 규칙은 그대로. 목록과 출처 · 라이선스는 Docs/ART_ASSETS.md.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitVisuals", menuName = "GirlsDungeon/Unit Visuals")]
    public sealed class UnitVisualCatalog : ScriptableObject
    {
        [SerializeField] private UnitVisualEntry[] entries = Array.Empty<UnitVisualEntry>();

        public UnitVisualEntry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var entry in entries) if (entry != null && entry.id == id) return entry;
            return null;
        }
    }
}
