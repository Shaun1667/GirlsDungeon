using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 상점 창 (3-15). 두 가지:
    /// - 사기 창: 진열 물건을 밟으면(ShopItemOffered — 사건 재생기를 거쳐) 이름 · 값 · 가진 골드를 보이고 "사기" = BuyItemCommand, "두기" = 닫기.
    ///   산 무기 · 파츠는 이어서 보통 비교 창(HudView)이 뜬다.
    /// - 팔기 창: 상인 계산대 옆에 서면 뜬다 (MoveController.ShopReached → GameRoot). 예비 파츠(◀ ▶로 고름) · 열쇠 · 재료 하나씩 = ShopSellCommand.
    /// 값 · 팔 수 있는지는 규칙(Core.Sim.Shop)에서 읽는다 (표시 값 = 실제 값).
    /// </summary>
    public sealed class ShopView : MonoBehaviour
    {
        [SerializeField] private GameObject buyPanel;
        [SerializeField] private UnityEngine.UI.Text buyLabel;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button leaveButton;

        [SerializeField] private GameObject sellPanel;
        [SerializeField] private UnityEngine.UI.Text sellLabel;
        [SerializeField] private Button previousPartButton;
        [SerializeField] private Button nextPartButton;
        [SerializeField] private Button sellPartButton;
        [SerializeField] private Button sellKeyButton;
        [SerializeField] private Button sellMaterialButton;
        [SerializeField] private Button closeButton;

        private Simulation simulation;
        private IEventSource events;
        private CombatSettings settings;
        private HudView hudView;
        private int offeredItemId = -1;
        private int partIndex;
        private string lastResult = string.Empty;

        public bool IsBuyOpen => buyPanel != null && buyPanel.activeSelf;
        public bool IsSellOpen => sellPanel != null && sellPanel.activeSelf;
        public bool IsOpen => IsBuyOpen || IsSellOpen;
        public string BuyText => buyLabel != null ? buyLabel.text : string.Empty;
        public string SellText => sellLabel != null ? sellLabel.text : string.Empty;
        public int OfferedItemId => offeredItemId;

        public void Bind(Simulation newSimulation, IEventSource newEvents, CombatSettings newSettings, HudView newHudView)
        {
            if (events != null) events.EventsPublished -= HandleEvents;
            simulation = newSimulation;
            events = newEvents;
            settings = newSettings;
            hudView = newHudView;
            events.EventsPublished += HandleEvents;
            CloseBuy();
            CloseSell();
        }

        private void OnDestroy()
        {
            if (events != null) events.EventsPublished -= HandleEvents;
        }

        private void HandleEvents(IReadOnlyList<GameEvent> published)
        {
            foreach (var gameEvent in published)
                if (gameEvent is ShopItemOffered offered) OpenBuy(offered.ItemId);
        }

        // ---- 사기 ----

        public void OpenBuy(int itemId)
        {
            var item = simulation?.World.FindItem(itemId);
            if (item == null || item.Price <= 0) return;
            offeredItemId = itemId;
            buyPanel.SetActive(true);
            RefreshBuy();
        }

        public void CloseBuy()
        {
            offeredItemId = -1;
            if (buyPanel != null) buyPanel.SetActive(false);
        }

        /// <summary>사기 (버튼 · 디버그 공용). 못 사면(골드 모자람) 창은 그대로.</summary>
        public bool Buy()
        {
            if (!IsBuyOpen || offeredItemId < 0) return false;
            int id = offeredItemId;
            CloseBuy();
            if (simulation.Execute(new BuyItemCommand(id, settings))) return true;
            OpenBuy(id);
            return false;
        }

        private void RefreshBuy()
        {
            var world = simulation.World;
            var item = world.FindItem(offeredItemId);
            if (item == null || item.Price <= 0)
            {
                CloseBuy();
                return;
            }
            bool enough = world.Gold >= item.Price;
            buyLabel.text = Text.Get("ui.shop.buy_title", hudView.ItemName(item), item.Price, world.Gold)
                + (enough ? string.Empty : "\n" + Text.Get("ui.shop.not_enough"));
            if (buyButton != null) buyButton.interactable = enough;
        }

        // ---- 팔기 ----

        public void OpenSell()
        {
            // 상점 주인이 적대했거나 쓰러졌으면 팔기 창을 열지 않는다 (3-15b).
            if (simulation == null || !Shop.AtCounter(simulation.World) || simulation.World.ShopHostile || Shop.Keeper(simulation.World) == null) return;
            lastResult = string.Empty;
            partIndex = 0;
            sellPanel.SetActive(true);
            RefreshSell();
        }

        public void CloseSell()
        {
            if (sellPanel != null) sellPanel.SetActive(false);
        }

        /// <summary>팔기 (버튼 · 디버그 공용). 예비 파츠는 지금 고른 것.</summary>
        public bool Sell(ShopSale sale)
        {
            if (!IsSellOpen) return false;
            int value = Shop.SaleValue(simulation.World, settings, sale, partIndex);
            bool done = simulation.Execute(new ShopSellCommand(sale, partIndex, settings));
            lastResult = done ? Text.Get("ui.shop.result_sold", value) : Text.Get("ui.shop.result_nothing");
            int spare = simulation.World.SpareParts.Count;
            if (partIndex >= spare) partIndex = Mathf.Max(0, spare - 1);
            RefreshSell();
            return done;
        }

        public void ChangePart(int step)
        {
            int count = simulation.World.SpareParts.Count;
            if (count == 0) return;
            partIndex = ((partIndex + step) % count + count) % count;
            RefreshSell();
        }

        private void RefreshSell()
        {
            var world = simulation.World;
            var lines = new List<string> { Text.Get("ui.shop.sell_title", world.Gold) };
            if (world.SpareParts.Count > 0)
            {
                var part = world.SpareParts[partIndex];
                lines.Add(Text.Get("ui.shop.sell_part", partIndex + 1, world.SpareParts.Count, hudView.WeaponName(part.Weapon),
                    HudView.GradeName(part.Grade), Shop.SaleValue(world, settings, ShopSale.SparePart, partIndex)));
            }
            else lines.Add(Text.Get("ui.shop.sell_part_none"));
            lines.Add(Text.Get("ui.shop.sell_key", world.Keys, Shop.SellValue(settings, settings.ShopKeyPrice)));
            lines.Add(Text.Get("ui.shop.sell_material", world.PartMaterials, settings.ShopMaterialSellPrice));
            if (!string.IsNullOrEmpty(lastResult)) lines.Add(lastResult);
            sellLabel.text = string.Join("\n", lines);

            bool many = world.SpareParts.Count > 1;
            if (previousPartButton != null) previousPartButton.interactable = many;
            if (nextPartButton != null) nextPartButton.interactable = many;
            if (sellPartButton != null) sellPartButton.interactable = Shop.CanSell(world, settings, ShopSale.SparePart, partIndex);
            if (sellKeyButton != null) sellKeyButton.interactable = Shop.CanSell(world, settings, ShopSale.Key);
            if (sellMaterialButton != null) sellMaterialButton.interactable = Shop.CanSell(world, settings, ShopSale.Material);
        }

        private void Update()
        {
            if (simulation == null) return;
            if (IsSellOpen && !Shop.AtCounter(simulation.World)) CloseSell();
            if (IsBuyOpen)
            {
                // 물건 칸을 떠났거나 이미 샀으면 닫는다.
                var item = simulation.World.FindItem(offeredItemId);
                var player = simulation.World.Player;
                if (item == null || item.Price <= 0 || player == null || player.Position != item.Position) CloseBuy();
                else RefreshBuy();
            }
        }

        private void Awake()
        {
            if (buyButton != null) buyButton.onClick.AddListener(() => Buy());
            if (leaveButton != null) leaveButton.onClick.AddListener(CloseBuy);
            if (previousPartButton != null) previousPartButton.onClick.AddListener(() => ChangePart(-1));
            if (nextPartButton != null) nextPartButton.onClick.AddListener(() => ChangePart(1));
            if (sellPartButton != null) sellPartButton.onClick.AddListener(() => Sell(ShopSale.SparePart));
            if (sellKeyButton != null) sellKeyButton.onClick.AddListener(() => Sell(ShopSale.Key));
            if (sellMaterialButton != null) sellMaterialButton.onClick.AddListener(() => Sell(ShopSale.Material));
            if (closeButton != null) closeButton.onClick.AddListener(CloseSell);
        }

        /// <summary>이 창 · 기록 줄이 쓰는 텍스트 표 키 (키 검사 테스트용).</summary>
        public static IEnumerable<string> TextKeys()
        {
            return new[]
            {
                "ui.shop.buy_title", "ui.shop.not_enough", "ui.shop.buy", "ui.shop.leave",
                "ui.shop.sell_title", "ui.shop.sell_part", "ui.shop.sell_part_none", "ui.shop.sell_key", "ui.shop.sell_material",
                "ui.shop.result_sold", "ui.shop.result_nothing",
                "ui.shop.previous_part", "ui.shop.next_part", "ui.shop.sell_part_button", "ui.shop.sell_key_button", "ui.shop.sell_material_button", "ui.shop.close",
                "ui.shop.preview", "ui.shop.item_part", "ui.shop.item_key", "ui.shop.item_medkit", "ui.shop.log_bought",
                "ui.gold.log_gain", "ui.hud.gold", "room.kind.shop.name",
                "ui.shop.log_angered", "ui.shop.log_defeated", "unit.shopkeeper.name",
            };
        }
    }
}
