using System.Collections.Generic;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>체력 · 안정지수 · 전투 상태 표시와 턴 종료 버튼. 글은 모두 텍스트 표에서 가져온다.</summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text hpLabel;
        [SerializeField] private UnityEngine.UI.Text stabilityLabel;
        [SerializeField] private UnityEngine.UI.Text roundLabel;
        [SerializeField] private UnityEngine.UI.Text turnLabel;
        [SerializeField] private Button endTurnButton;

        private Simulation simulation;
        private int endTurnCostTicks;

        public void Bind(Simulation newSimulation, int newEndTurnCostTicks)
        {
            Unbind();
            simulation = newSimulation;
            endTurnCostTicks = newEndTurnCostTicks;
            simulation.EventsPublished += HandleEvents;
            Refresh();
        }

        /// <summary>턴 종료 버튼과 같다. 지금 플레이어 차례가 아니면 false.</summary>
        public bool EndTurn()
        {
            return simulation != null && simulation.Execute(new EndTurnCommand(endTurnCostTicks));
        }

        private void Awake()
        {
            if (endTurnButton != null) endTurnButton.onClick.AddListener(() => EndTurn());
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            if (simulation != null) simulation.EventsPublished -= HandleEvents;
            simulation = null;
        }

        private void HandleEvents(IReadOnlyList<GameEvent> events)
        {
            Refresh();
        }

        private void Refresh()
        {
            var world = simulation.World;
            var player = world.Player;
            if (player != null)
            {
                hpLabel.text = Text.Get("ui.hud.hp", player.Hp, player.MaxHp);
                stabilityLabel.text = Text.Get("ui.hud.stability", player.Stability, player.MaxStability);
            }

            bool inCombat = world.Mode == WorldMode.Combat;
            bool playerTurn = inCombat && world.Turn == TurnSide.Player;
            roundLabel.gameObject.SetActive(inCombat);
            turnLabel.gameObject.SetActive(inCombat);
            endTurnButton.gameObject.SetActive(playerTurn);
            if (!inCombat) return;

            roundLabel.text = Text.Get("ui.combat.round", world.Round);
            turnLabel.text = playerTurn ? Text.Get("ui.combat.player_turn") : Text.Get("ui.combat.enemy_turn");
        }
    }
}
