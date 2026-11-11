using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 시작 화면: 캐릭터 카드 중 하나를 고른다. 판을 처음 시작할 때와 결과 화면의 "새 판"에서 뜬다.
    /// 글과 숫자는 캐릭터 표(GameBalance · CombatSettings)에서 읽는다 — 여기서는 보여 주기만 하고, 고르면 GameRoot가 새 판을 만든다.
    /// 화면 전체를 덮어 뒤의 맵 클릭을 막는다 (MoveController는 UI 위 클릭을 무시한다).
    /// </summary>
    public sealed class CharacterSelectView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [Tooltip("One button per character, in the balance table order. Extra buttons stay hidden.")]
        [SerializeField] private Button[] cards = Array.Empty<Button>();

        private readonly List<CharacterKind> shownKinds = new List<CharacterKind>();

        /// <summary>카드를 눌렀다 (GameRoot가 그 캐릭터로 새 판을 만든다).</summary>
        public event Action<CharacterKind> Picked;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>보이는 카드 글 (디버그 · 확인용).</summary>
        public string CardText(int index)
        {
            if (index < 0 || index >= cards.Length) return null;
            var label = cards[index].GetComponentInChildren<UnityEngine.UI.Text>(true);
            return label != null ? label.text : null;
        }

        public void Show(GameBalance balance, CombatSettings settings)
        {
            shownKinds.Clear();
            foreach (var kind in balance.CharacterKinds()) shownKinds.Add(kind);

            for (int i = 0; i < cards.Length; i++)
            {
                bool used = i < shownKinds.Count;
                cards[i].gameObject.SetActive(used);
                if (!used) continue;
                var stats = settings.TryCharacter(shownKinds[i]);
                var weapon = stats != null ? settings.WeaponFor(stats.StartWeapon) : null;
                var label = cards[i].GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (label == null || stats == null || weapon == null) continue;
                string nameKey = balance.CharacterNameKey(shownKinds[i]);
                string weaponKey = balance.WeaponNameKey(stats.StartWeapon);
                label.text = Text.Get("ui.select.card",
                    nameKey != null ? Text.Get(nameKey) : shownKinds[i].ToString(),
                    stats.MaxHp, stats.MaxStability, stats.Defense, stats.MoveRange, stats.VisionRange,
                    weaponKey != null ? Text.Get(weaponKey) : stats.StartWeapon.ToString(),
                    weapon.Attack, weapon.AttackRange);
            }
            panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>카드 누르기와 같다. 그 자리에 카드가 없거나 창이 닫혀 있으면 false.</summary>
        public bool Press(int index)
        {
            if (!IsOpen || index < 0 || index >= shownKinds.Count) return false;
            var kind = shownKinds[index];
            Hide();
            Picked?.Invoke(kind);
            return true;
        }

        private void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].onClick.AddListener(() => Press(index));
            }
        }
    }
}
