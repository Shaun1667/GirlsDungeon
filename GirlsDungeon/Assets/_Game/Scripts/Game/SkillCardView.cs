using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>스킬 바의 카드 하나: 이름 · 단축키 · 재사용 대기 덮개. 마우스를 올리면 SkillBarView가 설명 창을 띄운다.</summary>
    public sealed class SkillCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private UnityEngine.UI.Text nameLabel;
        [SerializeField] private UnityEngine.UI.Text keyLabel;
        [SerializeField] private GameObject cooldownOverlay;
        [SerializeField] private UnityEngine.UI.Text cooldownLabel;

        public event Action<SkillCardView> PointerEntered;
        public event Action<SkillCardView> PointerExited;

        public Button Button => button;
        public string NameText => nameLabel.text;
        public bool Interactable => button.interactable;
        public bool CooldownShown => cooldownOverlay.activeSelf;

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

        public void OnPointerEnter(PointerEventData eventData) => PointerEntered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => PointerExited?.Invoke(this);
    }
}
