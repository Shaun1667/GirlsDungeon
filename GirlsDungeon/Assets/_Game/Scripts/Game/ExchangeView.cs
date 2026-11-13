using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 교환 창 (3-14f). 교환대 옆에 서면 뜬다 (MoveController.CounterReached → GameRoot).
    /// 물건 4가지(구급상자 · 파츠 · 열쇠 · 열쇠 팔기)를 값과 함께 보이고, 버튼은 ExchangeCommand만 낸다.
    /// 값 · 판매 여부 · 살 수 있는지는 규칙(Core.Sim.Exchange)에서 읽는다 (표시 값 = 실제 값).
    /// </summary>
    public sealed class ExchangeView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text label;
        [SerializeField] private Button medkitButton;
        [SerializeField] private Button partButton;
        [SerializeField] private Button keyButton;
        [SerializeField] private Button sellKeyButton;
        [SerializeField] private Button closeButton;

        private Simulation simulation;
        private CombatSettings settings;
        private string lastResult = string.Empty;

        public bool IsOpen => panel != null && panel.activeSelf;
        public string LabelText => label != null ? label.text : string.Empty;

        public void Bind(Simulation newSimulation, CombatSettings newSettings)
        {
            simulation = newSimulation;
            settings = newSettings;
            Close();
        }

        public void Open()
        {
            if (simulation == null || !Exchange.AtCounter(simulation.World)) return;
            lastResult = string.Empty;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>그 물건을 사고판다 (버튼 · 디버그 공용).</summary>
        public bool Trade(ExchangeOffer offer)
        {
            if (!IsOpen) return false;
            bool done = simulation.Execute(new ExchangeCommand(offer, settings));
            if (done) lastResult = Text.Get("ui.exchange.result_done");
            else if (Exchange.IsSold(simulation.World, offer)) lastResult = Text.Get("ui.exchange.result_sold");
            else lastResult = Text.Get(offer == ExchangeOffer.SellKey ? "ui.exchange.result_no_key" : "ui.exchange.result_not_enough");
            Refresh();
            return done;
        }

        private void Refresh()
        {
            if (simulation == null || label == null) return;
            var world = simulation.World;
            var lines = new List<string>
            {
                Text.Get("ui.exchange.title", world.PartMaterials, world.Keys),
                OfferLine(ExchangeOffer.Medkit, "ui.exchange.offer_medkit", "ui.exchange.offer_medkit_sold"),
                OfferLine(ExchangeOffer.Part, "ui.exchange.offer_part", "ui.exchange.offer_part_sold"),
                OfferLine(ExchangeOffer.Key, "ui.exchange.offer_key", "ui.exchange.offer_key_sold"),
                OfferLine(ExchangeOffer.SellKey, "ui.exchange.offer_sell_key", "ui.exchange.offer_sell_key_sold"),
            };
            if (!string.IsNullOrEmpty(lastResult)) lines.Add(lastResult);
            label.text = string.Join("\n", lines);
            SetButton(medkitButton, ExchangeOffer.Medkit);
            SetButton(partButton, ExchangeOffer.Part);
            SetButton(keyButton, ExchangeOffer.Key);
            SetButton(sellKeyButton, ExchangeOffer.SellKey);
        }

        /// <summary>물건 한 줄 — 값은 수치 표에서, 이미 팔렸으면 "다 팔림" 글 (키는 통째로 받는다).</summary>
        private string OfferLine(ExchangeOffer offer, string key, string soldKey)
        {
            if (Exchange.IsSold(simulation.World, offer)) return Text.Get(soldKey);
            return Text.Get(key, Exchange.Price(settings, offer));
        }

        private void SetButton(Button button, ExchangeOffer offer)
        {
            if (button != null) button.interactable = Exchange.CanTrade(simulation.World, settings, offer);
        }

        private void Update()
        {
            if (IsOpen && (simulation == null || !Exchange.AtCounter(simulation.World))) Close();
        }

        private void Awake()
        {
            if (medkitButton != null) medkitButton.onClick.AddListener(() => Trade(ExchangeOffer.Medkit));
            if (partButton != null) partButton.onClick.AddListener(() => Trade(ExchangeOffer.Part));
            if (keyButton != null) keyButton.onClick.AddListener(() => Trade(ExchangeOffer.Key));
            if (sellKeyButton != null) sellKeyButton.onClick.AddListener(() => Trade(ExchangeOffer.SellKey));
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        /// <summary>이 창 · 기록 줄이 쓰는 텍스트 표 키 (키 검사 테스트용).</summary>
        public static IEnumerable<string> TextKeys()
        {
            return new[]
            {
                "ui.exchange.title",
                "ui.exchange.offer_medkit", "ui.exchange.offer_medkit_sold",
                "ui.exchange.offer_part", "ui.exchange.offer_part_sold",
                "ui.exchange.offer_key", "ui.exchange.offer_key_sold",
                "ui.exchange.offer_sell_key", "ui.exchange.offer_sell_key_sold",
                "ui.exchange.result_done", "ui.exchange.result_sold", "ui.exchange.result_not_enough", "ui.exchange.result_no_key",
                "ui.exchange.buy_medkit", "ui.exchange.buy_part", "ui.exchange.buy_key", "ui.exchange.sell_key", "ui.exchange.close",
                "ui.exchange.log_medkit", "ui.exchange.log_part", "ui.exchange.log_key", "ui.exchange.log_sell_key",
            };
        }
    }
}
