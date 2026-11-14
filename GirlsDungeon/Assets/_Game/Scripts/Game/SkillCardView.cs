using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>스킬 바의 카드 하나: 이름 · 단축키 · 재사용 대기 덮개 · 아래 리몰딩 소모 칸(3-16). 마우스를 올리면 SkillBarView가 설명 창을 띄운다.</summary>
    public sealed class SkillCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private UnityEngine.UI.Text nameLabel;
        [SerializeField] private UnityEngine.UI.Text keyLabel;
        [SerializeField] private GameObject cooldownOverlay;
        [SerializeField] private UnityEngine.UI.Text cooldownLabel;
        [Tooltip("Remold cost pips under the card (hidden when the skill has no Remold cost).")]
        [SerializeField] private GameObject costRoot;
        [SerializeField] private Image[] costPips = new Image[0];

        public event Action<SkillCardView> PointerEntered;
        public event Action<SkillCardView> PointerExited;

        public Button Button => button;
        public string NameText => nameLabel.text;
        public bool Interactable => button.interactable;
        public bool CooldownShown => cooldownOverlay.activeSelf;

        /// <summary>보이는 소모 칸 수 (0 = 숨김).</summary>
        public int CostShown { get; private set; }

        public void SetName(string text) => nameLabel.text = text;
        public void SetKey(string text) => keyLabel.text = text;

        /// <summary>쓸 수 있는지 · 고른 상태인지 · 재사용 대기(0 = 없음)를 그린다.</summary>
        public void SetState(bool usable, bool selected, int cooldown, Color normal, Color highlight, Color disabled)
        {
            button.interactable = usable;
            background.color = selected ? highlight : usable ? normal : disabled;
            nameLabel.color = usable || selected ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            cooldownOverlay.SetActive(cooldown > 0);
            if (cooldown > 0) cooldownLabel.text = cooldown.ToString();
        }

        /// <summary>리몰딩 소모 칸 (0 = 숨김). 모자라면 흐리게.</summary>
        public void SetCost(int cost, bool affordable, Color filled, Color lacking)
        {
            CostShown = Mathf.Min(cost, costPips.Length);
            if (costRoot != null) costRoot.SetActive(CostShown > 0);
            for (int i = 0; i < costPips.Length; i++)
            {
                costPips[i].gameObject.SetActive(i < CostShown);
                costPips[i].color = affordable ? filled : lacking;
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => PointerEntered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => PointerExited?.Invoke(this);
    }
}
