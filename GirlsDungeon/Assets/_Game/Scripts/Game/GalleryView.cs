using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 세트 진열 창 (3-14g). 진열장 옆에 서면 뜬다 (MoveController.GalleryReached → GameRoot).
    /// 열 때 OpenGalleryCommand로 파츠 3개를 만들고(처음 한 번), 고르기 버튼은 PickGalleryPartCommand만 낸다.
    /// 파츠 글은 규칙의 파츠 값을 그대로 보인다 (표시 값 = 실제 값).
    /// </summary>
    public sealed class GalleryView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text label;
        [SerializeField] private Button pickButton1;
        [SerializeField] private Button pickButton2;
        [SerializeField] private Button pickButton3;
        [SerializeField] private Button closeButton;

        private Simulation simulation;
        private CombatSettings settings;
        private GameBalance balance;

        public bool IsOpen => panel != null && panel.activeSelf;
        public string LabelText => label != null ? label.text : string.Empty;

        public void Bind(Simulation newSimulation, CombatSettings newSettings, GameBalance newBalance)
        {
            simulation = newSimulation;
            settings = newSettings;
            balance = newBalance;
            Close();
        }

        public void Open()
        {
            if (simulation == null || !SetGallery.AtCase(simulation.World)) return;
            if (!simulation.Execute(new OpenGalleryCommand(settings))) return;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>진열된 파츠 하나를 고른다 (0부터). 고르면 창을 닫는다.</summary>
        public bool Pick(int index)
        {
            if (!IsOpen) return false;
            bool done = simulation.Execute(new PickGalleryPartCommand(index));
            if (done) Close();
            return done;
        }

        private void Refresh()
        {
            var offers = simulation.World.GalleryOffers;
            var lines = new List<string>();
            if (offers.Count == 0) lines.Add(Text.Get("ui.gallery.empty"));
            else
            {
                lines.Add(Text.Get("ui.gallery.title", HudView.SetName(offers[0].Set)));
                lines.Add(Text.Get("ui.gallery.rule"));
                for (int i = 0; i < offers.Count; i++)
                {
                    var part = offers[i];
                    lines.Add(Text.Get("ui.gallery.offer", i + 1, WeaponName(part.Weapon), SlotName(part), HudView.GradeName(part.Grade), HudView.PartStats(part)));
                }
            }
            label.text = string.Join("\n", lines);
            var buttons = new[] { pickButton1, pickButton2, pickButton3 };
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null) buttons[i].interactable = i < offers.Count;
        }

        private string WeaponName(WeaponKind kind)
        {
            string key = balance != null ? balance.WeaponNameKey(kind) : null;
            return key != null ? Text.Get(key) : kind.ToString();
        }

        private string SlotName(WeaponPart part)
        {
            string key = balance != null ? balance.PartSlotKey(part.Weapon, part.Slot) : null;
            return key != null ? Text.Get(key) : (part.Slot + 1).ToString();
        }

        private void Update()
        {
            if (IsOpen && (simulation == null || !SetGallery.AtCase(simulation.World))) Close();
        }

        private void Awake()
        {
            if (pickButton1 != null) pickButton1.onClick.AddListener(() => Pick(0));
            if (pickButton2 != null) pickButton2.onClick.AddListener(() => Pick(1));
            if (pickButton3 != null) pickButton3.onClick.AddListener(() => Pick(2));
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        /// <summary>이 창 · 기록 줄이 쓰는 텍스트 표 키 (키 검사 테스트용).</summary>
        public static IEnumerable<string> TextKeys()
        {
            return new[]
            {
                "ui.gallery.title", "ui.gallery.rule", "ui.gallery.offer", "ui.gallery.empty",
                "ui.gallery.pick_1", "ui.gallery.pick_2", "ui.gallery.pick_3", "ui.gallery.close", "ui.gallery.log_picked",
            };
        }
    }
}
