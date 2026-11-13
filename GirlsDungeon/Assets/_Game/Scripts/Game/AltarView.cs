using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 제단 창 (3-14h). 제단 옆에 서면 뜬다 (MoveController.AltarReached → GameRoot).
    /// 열 때 OpenAltarCommand로 거래를 고르고(처음 한 번), 받기 버튼은 AltarTradeCommand만 낸다. 닫기 = 거절 (나중에 다시 와도 된다).
    /// 거래 글은 규칙의 거래 값을 그대로 보인다 (표시 값 = 실제 값).
    /// </summary>
    public sealed class AltarView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text label;
        [SerializeField] private Button acceptButton1;
        [SerializeField] private Button acceptButton2;
        [SerializeField] private Button acceptButton3;
        [SerializeField] private Button closeButton;

        private Simulation simulation;
        private CombatSettings settings;

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
            if (simulation == null || !Altar.AtAltar(simulation.World)) return;
            if (!simulation.Execute(new OpenAltarCommand(settings))) return;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>거래 하나를 받는다 (0부터). 받으면 창을 닫는다.</summary>
        public bool Accept(int index)
        {
            if (!IsOpen) return false;
            bool done = simulation.Execute(new AltarTradeCommand(settings, index));
            if (done) Close();
            else Refresh();
            return done;
        }

        private void Refresh()
        {
            var world = simulation.World;
            var lines = new List<string> { Text.Get("ui.altar.title"), Text.Get("ui.altar.rule") };
            int count = world.AltarOffers.Count;
            for (int i = 0; i < count; i++)
            {
                var trade = Altar.OfferAt(world, settings, i);
                if (trade == null) continue;
                string line = TradeLine(i + 1, trade);
                lines.Add(Altar.CanAccept(world, settings, i) ? line : Text.Get("ui.altar.offer_blocked", line));
            }
            label.text = string.Join("\n", lines);
            var buttons = new[] { acceptButton1, acceptButton2, acceptButton3 };
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null)
                {
                    buttons[i].gameObject.SetActive(i < count);
                    buttons[i].interactable = i < count && Altar.CanAccept(world, settings, i);
                }
        }

        internal static string TradeLine(int number, AltarTrade trade)
        {
            return Text.Get("ui.altar.offer", number, Text.Get(HudView.StatNameKey(trade.CostStat)), trade.CostAmount,
                Text.Get(HudView.StatNameKey(trade.BoonStat)), trade.BoonAmount);
        }

        private void Update()
        {
            if (IsOpen && (simulation == null || !Altar.AtAltar(simulation.World))) Close();
        }

        private void Awake()
        {
            if (acceptButton1 != null) acceptButton1.onClick.AddListener(() => Accept(0));
            if (acceptButton2 != null) acceptButton2.onClick.AddListener(() => Accept(1));
            if (acceptButton3 != null) acceptButton3.onClick.AddListener(() => Accept(2));
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        /// <summary>이 창 · 기록 줄이 쓰는 텍스트 표 키 (키 검사 테스트용).</summary>
        public static IEnumerable<string> TextKeys()
        {
            return new[]
            {
                "ui.altar.title", "ui.altar.rule", "ui.altar.offer", "ui.altar.offer_blocked",
                "ui.altar.accept_1", "ui.altar.accept_2", "ui.altar.accept_3", "ui.altar.close", "ui.altar.log_traded",
            };
        }
    }
}
