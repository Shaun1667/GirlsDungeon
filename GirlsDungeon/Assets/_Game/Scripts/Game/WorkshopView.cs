using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 파츠 작업 화면 (3-4c). 작업대 옆에 서면 뜬다 (MoveController.WorkbenchReached → GameRoot).
    /// 끼운 파츠 · 보관함 파츠 중 하나를 고르고: 분해 / 옵션 바꾸기(줄 · 원하는 능력치) / 세트 바꾸기(원하는 세트) / 강화(줄, 3-16p) / 보관함 파츠 끼우기.
    /// 규칙은 Core.Sim.Workshop 한 곳 — 이 화면은 커맨드만 내고, 확률 · 비용 · 결과는 규칙과 수치 표에서 읽는다 (표시 값 = 실제 값).
    /// </summary>
    public sealed class WorkshopView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text label;
        [SerializeField] private Button prevPartButton;
        [SerializeField] private Button nextPartButton;
        [SerializeField] private Button optionModeButton;
        [SerializeField] private Button setModeButton;
        [SerializeField] private Button upgradeModeButton;
        [SerializeField] private Button prevLineButton;
        [SerializeField] private Button nextLineButton;
        [SerializeField] private Button prevTargetButton;
        [SerializeField] private Button nextTargetButton;
        [SerializeField] private Button runButton;
        [SerializeField] private Button disassembleButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button closeButton;

        private Simulation simulation;
        private CombatSettings settings;
        private HudView hud;

        private readonly List<(PartPlace place, int index)> entries = new List<(PartPlace, int)>();
        private int selected;
        private ReworkKind mode = ReworkKind.Option;
        private int line;
        private int targetIndex;
        private string lastResult = string.Empty;

        public bool IsOpen => panel != null && panel.activeSelf;
        public ReworkKind Mode => mode;
        public string LabelText => label != null ? label.text : string.Empty;

        public void Bind(Simulation newSimulation, CombatSettings newSettings, HudView newHud)
        {
            simulation = newSimulation;
            settings = newSettings;
            hud = newHud;
            Close();
        }

        public void Open()
        {
            if (simulation == null || !Workshop.AtBench(simulation.World)) return;
            selected = 0;
            line = 0;
            targetIndex = 0;
            mode = ReworkKind.Option;
            lastResult = string.Empty;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        // ── 버튼 (디버그에서도 부른다) ──
        public void StepPart(int direction)
        {
            if (entries.Count == 0) return;
            selected = (selected + direction + entries.Count) % entries.Count;
            line = 0;
            targetIndex = 0;
            lastResult = string.Empty;
            Refresh();
        }

        public void SetMode(ReworkKind newMode)
        {
            mode = newMode;
            targetIndex = 0;
            lastResult = string.Empty;
            Refresh();
        }

        public void StepLine(int direction)
        {
            var part = SelectedPart();
            if (part == null || part.Bonuses.Count == 0) return;
            line = (line + direction + part.Bonuses.Count) % part.Bonuses.Count;
            targetIndex = 0;
            Refresh();
        }

        /// <summary>세트 바꾸기에서 고른 세트 (후보 SetEffects.Pool — 아직 조건이 없는 세트는 빠짐).</summary>
        private PartSet SetTarget()
        {
            var pool = SetEffects.Pool();
            return pool[targetIndex % pool.Count];
        }

        public void StepTarget(int direction)
        {
            if (mode == ReworkKind.Upgrade) return;
            int count = mode == ReworkKind.Option ? OptionTargets().Count : SetEffects.Pool().Count;
            if (count == 0) return;
            targetIndex = (targetIndex + direction + count) % count;
            Refresh();
        }

        /// <summary>고른 방식으로 바꾸기를 시도한다. 결과(성공 · 실패)는 사건에서 읽어 글로 남긴다.</summary>
        public bool Run()
        {
            var part = SelectedPart();
            if (part == null) return false;
            var (place, index) = entries[selected];
            int cost = mode == ReworkKind.Option ? settings.WorkshopOptionCost : mode == ReworkKind.Upgrade ? settings.WorkshopUpgradeCost : settings.WorkshopSetCost;
            if (simulation.World.PartMaterials < cost)
            {
                lastResult = Text.Get("ui.workshop.result_not_enough", cost);
                Refresh();
                return false;
            }

            bool done;
            if (mode == ReworkKind.Option)
            {
                var targets = OptionTargets();
                if (targets.Count == 0) return false;
                done = Execute(new ChangePartOptionCommand(place, index, line, targets[targetIndex % targets.Count], settings));
            }
            else if (mode == ReworkKind.Upgrade)
            {
                if (!Workshop.CanUpgrade(settings, part, line)) return false;
                done = Execute(new UpgradePartCommand(place, index, line, settings));
            }
            else
            {
                done = Execute(new ChangePartSetCommand(place, index, SetTarget(), settings));
            }
            Refresh();
            return done;
        }

        public bool Disassemble()
        {
            if (SelectedPart() == null) return false;
            var (place, index) = entries[selected];
            bool done = Execute(new DisassemblePartCommand(place, index, settings));
            if (done && selected >= entries.Count - 1) selected = Mathf.Max(0, entries.Count - 2);
            line = 0;
            Refresh();
            return done;
        }

        public bool EquipSelected()
        {
            if (SelectedPart() == null) return false;
            var (place, index) = entries[selected];
            if (place != PartPlace.Spare) return false;
            if (!Parts.Fits(simulation.World, SelectedPart()))
            {
                lastResult = Text.Get("ui.workshop.result_not_fit");
                Refresh();
                return false;
            }
            bool done = Execute(new EquipSparePartCommand(index, settings));
            Refresh();
            return done;
        }

        private bool Execute(ICommand command)
        {
            simulation.EventsPublished += CaptureResult;
            try
            {
                return simulation.Execute(command);
            }
            finally
            {
                simulation.EventsPublished -= CaptureResult;
            }
        }

        private void CaptureResult(IReadOnlyList<GameEvent> events)
        {
            foreach (var gameEvent in events)
            {
                switch (gameEvent)
                {
                    case PartReworked reworked when reworked.Kind == ReworkKind.Upgrade:
                    {
                        var upgraded = Workshop.Get(simulation.World, reworked.Place, reworked.Index);
                        if (upgraded != null && line < upgraded.Bonuses.Count)
                            lastResult = Text.Get("ui.workshop.result_upgraded", Text.Get(HudView.StatKey(upgraded.Bonuses[line].Stat), upgraded.Bonuses[line].Value));
                        break;
                    }
                    case PartReworked reworked when reworked.Kind == ReworkKind.Option:
                        lastResult = Text.Get(reworked.Success ? "ui.workshop.result_option_success" : "ui.workshop.result_option_fail");
                        break;
                    case PartReworked reworked:
                        lastResult = Text.Get(reworked.Success ? "ui.workshop.result_set_success" : "ui.workshop.result_set_fail");
                        break;
                    case PartDisassembled disassembled:
                        lastResult = Text.Get("ui.workshop.result_disassembled", disassembled.Gained);
                        break;
                    case PartEquipped _:
                        lastResult = Text.Get("ui.workshop.result_equipped");
                        break;
                }
            }
        }

        private WeaponPart SelectedPart()
        {
            if (entries.Count == 0) return null;
            selected = Mathf.Clamp(selected, 0, entries.Count - 1);
            var (place, index) = entries[selected];
            return Workshop.Get(simulation.World, place, index);
        }

        /// <summary>지금 줄을 바꿀 수 있는 능력치 후보 (그 칸 후보 중 다른 줄과 겹치지 않는 것).</summary>
        private List<PartStat> OptionTargets()
        {
            var targets = new List<PartStat>();
            var part = SelectedPart();
            if (part == null) return targets;
            foreach (var stat in Workshop.Pool(settings, part.Slot))
                if (Workshop.CanTarget(settings, part, line, stat)) targets.Add(stat);
            return targets;
        }

        private void CollectEntries()
        {
            entries.Clear();
            var world = simulation.World;
            for (int slot = 0; slot < WeaponPart.SlotCount; slot++)
                if (world.PlayerParts[slot] != null) entries.Add((PartPlace.Equipped, slot));
            for (int i = 0; i < world.SpareParts.Count; i++) entries.Add((PartPlace.Spare, i));
        }

        private void Refresh()
        {
            if (simulation == null || label == null) return;
            CollectEntries();
            var world = simulation.World;
            var header = Text.Get("ui.workshop.header", world.PartMaterials, world.SpareParts.Count, settings.SpareSlots);
            var part = SelectedPart();
            var lines = new List<string> { header };

            if (part == null)
            {
                lines.Add(Text.Get("ui.workshop.no_parts"));
            }
            else
            {
                var (place, _) = entries[selected];
                lines.Add(Text.Get(place == PartPlace.Equipped ? "ui.workshop.part_equipped" : "ui.workshop.part_spare",
                    selected + 1, entries.Count, hud.WeaponName(part.Weapon), hud.PartSlotName(part.Weapon, part.Slot), HudView.GradeName(part.Grade)));
                line = Mathf.Clamp(line, 0, Mathf.Max(0, part.Bonuses.Count - 1));
                for (int i = 0; i < part.Bonuses.Count; i++)
                {
                    string stat = Text.Get(HudView.StatKey(part.Bonuses[i].Stat), part.Bonuses[i].Value);
                    bool marked = mode != ReworkKind.Set && i == line;
                    lines.Add(Text.Get(marked ? "ui.workshop.line_selected" : "ui.workshop.line", i + 1, stat));
                }
                lines.Add(part.Set != PartSet.None ? Text.Get("ui.workshop.set_line", HudView.SetName(part.Set)) : Text.Get("ui.workshop.no_set"));
                lines.Add(Text.Get("ui.workshop.disassemble_hint", Workshop.MaterialsFrom(settings, part.Grade)));

                if (mode == ReworkKind.Option)
                {
                    var targets = OptionTargets();
                    string target = targets.Count > 0 ? Text.Get(HudView.StatNameKey(targets[targetIndex % targets.Count])) : "-";
                    lines.Add(Text.Get("ui.workshop.mode_option", line + 1, target, Workshop.OptionPercent(world, settings), settings.WorkshopOptionCost));
                }
                else if (mode == ReworkKind.Upgrade)
                {
                    var bonus = part.Bonuses.Count > 0 ? part.Bonuses[line] : default;
                    var range = part.Bonuses.Count > 0 ? settings.PartRange(bonus.Stat) : null;
                    if (range == null || range.Cap <= 0)
                        lines.Add(Text.Get("ui.workshop.mode_upgrade_none", line + 1));
                    else if (bonus.Value >= range.Cap)
                        lines.Add(Text.Get("ui.workshop.mode_upgrade_max", line + 1, range.Cap));
                    else
                        lines.Add(Text.Get("ui.workshop.mode_upgrade", line + 1, range.Min, range.Max, range.Cap, settings.WorkshopUpgradeCost));
                }
                else if (part.Set == PartSet.None)
                {
                    lines.Add(Text.Get("ui.workshop.mode_set_none"));
                }
                else
                {
                    var target = SetTarget();
                    lines.Add(Text.Get("ui.workshop.mode_set", HudView.SetName(target), Workshop.SetPercent(world, settings), settings.WorkshopSetCost));
                }
            }
            if (!string.IsNullOrEmpty(lastResult)) lines.Add(lastResult);
            label.text = string.Join("\n", lines);

            bool hasPart = part != null;
            bool canRun = hasPart && (mode == ReworkKind.Option ? part.Bonuses.Count > 0
                : mode == ReworkKind.Upgrade ? Workshop.CanUpgrade(settings, part, line) : part.Set != PartSet.None);
            if (runButton != null) runButton.interactable = canRun;
            if (disassembleButton != null) disassembleButton.interactable = hasPart;
            if (equipButton != null) equipButton.interactable = hasPart && entries[selected].place == PartPlace.Spare;
            if (prevLineButton != null) prevLineButton.interactable = hasPart && mode != ReworkKind.Set;
            if (nextLineButton != null) nextLineButton.interactable = hasPart && mode != ReworkKind.Set;
            if (prevTargetButton != null) prevTargetButton.interactable = mode != ReworkKind.Upgrade;
            if (nextTargetButton != null) nextTargetButton.interactable = mode != ReworkKind.Upgrade;
        }

        private void Update()
        {
            // 작업대에서 멀어지면(전투 · 층 이동 등) 닫는다.
            if (IsOpen && (simulation == null || !Workshop.AtBench(simulation.World))) Close();
        }

        private void Awake()
        {
            Hook(prevPartButton, () => StepPart(-1));
            Hook(nextPartButton, () => StepPart(1));
            Hook(optionModeButton, () => SetMode(ReworkKind.Option));
            Hook(setModeButton, () => SetMode(ReworkKind.Set));
            Hook(upgradeModeButton, () => SetMode(ReworkKind.Upgrade));
            Hook(prevLineButton, () => StepLine(-1));
            Hook(nextLineButton, () => StepLine(1));
            Hook(prevTargetButton, () => StepTarget(-1));
            Hook(nextTargetButton, () => StepTarget(1));
            Hook(runButton, () => Run());
            Hook(disassembleButton, () => Disassemble());
            Hook(equipButton, () => EquipSelected());
            Hook(closeButton, Close);
        }

        private static void Hook(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.AddListener(action);
        }
    }
}
