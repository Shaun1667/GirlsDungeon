using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 잠긴 문 창 (3-4d). 잠긴 문 옆에 서면 뜬다 (MoveController.DoorReached → GameRoot).
    /// 열쇠로 열기 / 힘으로 열기 (캐릭터가 할 수 있고 이 문에 아직 안 해 봤을 때만) / 닫기. 규칙은 Core.Sim.Doors 한 곳 — 이 창은 커맨드만 낸다.
    /// </summary>
    public sealed class DoorView : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private UnityEngine.UI.Text label;
        [SerializeField] private Button keyButton;
        [SerializeField] private Button forceButton;
        [SerializeField] private Button closeButton;

        private Simulation simulation;
        private CombatSettings settings;
        private GridPos door;
        private string lastResult = string.Empty;

        public bool IsOpen => panel != null && panel.activeSelf;
        public string LabelText => label != null ? label.text : string.Empty;
        public GridPos Door => door;

        public void Bind(Simulation newSimulation, CombatSettings newSettings)
        {
            simulation = newSimulation;
            settings = newSettings;
            Close();
        }

        public void Open(GridPos cell)
        {
            if (simulation == null || !Doors.CanReach(simulation.World, cell)) return;
            door = cell;
            lastResult = string.Empty;
            panel.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
        }

        public bool UseKey()
        {
            if (!IsOpen) return false;
            bool done = simulation.Execute(new OpenDoorCommand(door, false, settings));
            if (done) Close();
            else
            {
                lastResult = Text.Get("ui.door.no_key");
                Refresh();
            }
            return done;
        }

        public bool UseForce()
        {
            if (!IsOpen || !Doors.CanForce(simulation.World, settings, door)) return false;
            bool done = simulation.Execute(new OpenDoorCommand(door, true, settings));
            if (done && !simulation.World.Map.IsLockedDoor(door)) Close();
            else
            {
                lastResult = Text.Get("ui.door.force_failed");
                Refresh();
            }
            return done;
        }

        private void Refresh()
        {
            var world = simulation.World;
            int percent = Doors.ForcePercent(world, settings);
            string text = Text.Get("ui.door.title", world.Keys);
            if (percent > 0)
                text += "\n" + (Doors.CanForce(world, settings, door) ? Text.Get("ui.door.force_ready", percent) : Text.Get("ui.door.force_used"));
            if (!string.IsNullOrEmpty(lastResult)) text += "\n" + lastResult;
            label.text = text;
            if (keyButton != null) keyButton.interactable = world.Keys > 0;
            if (forceButton != null)
            {
                forceButton.gameObject.SetActive(percent > 0);
                forceButton.interactable = Doors.CanForce(world, settings, door);
            }
        }

        private void Update()
        {
            // 문 옆을 떠나거나 문이 열리면(전투 · 층 이동 등) 닫는다.
            if (IsOpen && (simulation == null || !Doors.CanReach(simulation.World, door))) Close();
        }

        private void Awake()
        {
            if (keyButton != null) keyButton.onClick.AddListener(() => UseKey());
            if (forceButton != null) forceButton.onClick.AddListener(() => UseForce());
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }
    }
}
