using System.Collections.Generic;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 체력 · 안정지수 · 전투 상태 · 사격 미리보기 · 전투 기록 · 턴 종료 버튼. 글은 모두 텍스트 표에서 가져온다.
    /// 미리보기 숫자는 MoveController가 규칙(CombatRules.Preview)에서 받아 넘긴다 — 여기서는 글로 바꾸기만 한다.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text hpLabel;
        [SerializeField] private UnityEngine.UI.Text stabilityLabel;
        [SerializeField] private UnityEngine.UI.Text roundLabel;
        [SerializeField] private UnityEngine.UI.Text turnLabel;
        [SerializeField] private UnityEngine.UI.Text previewLabel;
        [SerializeField] private UnityEngine.UI.Text logLabel;
        [SerializeField] private UnityEngine.UI.Text defeatLabel;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button grenadeButton;
        [SerializeField] private Button overwatchButton;
        [SerializeField] private int logLines = 4;

        private readonly List<string> log = new List<string>();
        private Simulation simulation;
        private CombatSettings settings;
        private GameBalance balance;
        private bool aiming;

        public string PreviewText => previewLabel.text;
        public string LogText => logLabel.text;
        public string GrenadeButtonText => GrenadeLabel.text;

        /// <summary>수류탄 버튼을 눌렀다. MoveController가 조준 모드를 켜고 끈다 (GameRoot가 잇는다).</summary>
        public event System.Action GrenadeButtonPressed;

        /// <summary>감시 버튼을 눌렀다 (GameRoot가 MoveController.RequestOverwatch로 잇는다).</summary>
        public event System.Action OverwatchButtonPressed;

        private UnityEngine.UI.Text GrenadeLabel => grenadeButton.GetComponentInChildren<UnityEngine.UI.Text>(true);

        /// <summary>조준 모드 표시. 버튼 글이 "조준 중"으로 바뀐다.</summary>
        public void SetAiming(bool value)
        {
            aiming = value;
            Refresh();
        }

        public void ShowAreaPreview(AreaPreview preview)
        {
            var lines = new List<string>();
            switch (preview.Block)
            {
                case AreaBlock.NotReady:
                    lines.Add(Text.Get("ui.combat.preview_area_not_ready", simulation.World.GrenadeCooldown));
                    break;
                case AreaBlock.OutOfRange:
                    lines.Add(Text.Get("ui.combat.preview_area_out_of_range"));
                    break;
                case AreaBlock.NotVisible:
                    lines.Add(Text.Get("ui.combat.preview_area_not_visible"));
                    break;
                default:
                    lines.Add(Text.Get("ui.combat.preview_area_header", preview.Hits.Count));
                    foreach (var hit in preview.Hits)
                    {
                        string cover = CoverName(hit.Cover);
                        if (hit.ReductionPercent > 0) cover = cover + " · " + Text.Get("ui.combat.reduction", hit.ReductionPercent);
                        lines.Add(Text.Get("ui.combat.preview_area_hit", UnitName(hit.Target.Kind), hit.Damage, hit.StabilityDamage, cover));
                    }
                    if (preview.CoverBreaks > 0) lines.Add(Text.Get("ui.combat.preview_area_breaks_cover", preview.CoverBreaks));
                    if (preview.HitsPlayer) lines.Add(Text.Get("ui.combat.preview_area_self"));
                    break;
            }
            previewLabel.text = string.Join("\n", lines);
            previewLabel.gameObject.SetActive(true);
        }

        public void Bind(Simulation newSimulation, CombatSettings newSettings, GameBalance newBalance)
        {
            Unbind();
            simulation = newSimulation;
            settings = newSettings;
            balance = newBalance;
            log.Clear();
            logLabel.text = string.Empty;
            simulation.EventsPublished += HandleEvents;
            ClearPreview();
            Refresh();
        }

        /// <summary>턴 종료 버튼과 같다. 지금 플레이어 차례가 아니면 false.</summary>
        public bool EndTurn()
        {
            return simulation != null && simulation.Execute(new EndTurnCommand(settings));
        }

        public void ShowAttackPreview(Unit target, AttackPreview preview, bool alreadyActed)
        {
            string name = UnitName(target.Kind);
            if (alreadyActed) previewLabel.text = Text.Get("ui.combat.preview_already_acted");
            else if (preview.Block == AttackBlock.OutOfRange) previewLabel.text = Text.Get("ui.combat.preview_out_of_range", name);
            else if (preview.Block == AttackBlock.NoLineOfFire) previewLabel.text = Text.Get("ui.combat.preview_no_line", name);
            else
            {
                string cover = CoverName(preview.Cover);
                if (preview.ReductionPercent > 0) cover = cover + " · " + Text.Get("ui.combat.reduction", preview.ReductionPercent);
                previewLabel.text = Text.Get("ui.combat.preview_attack", name, target.Hp, target.MaxHp, target.Stability, target.MaxStability,
                    preview.Damage, preview.CritPercent, preview.CritDamage, preview.StabilityDamage, cover);
                if (preview.BreaksCover) previewLabel.text += "\n" + Text.Get("ui.combat.preview_breaks_cover");
            }
            previewLabel.gameObject.SetActive(true);
        }

        public void ShowMovePreview(CoverLevel cover)
        {
            previewLabel.text = Text.Get("ui.combat.preview_move", CoverName(cover));
            previewLabel.gameObject.SetActive(true);
        }

        public void ClearPreview()
        {
            previewLabel.text = string.Empty;
            previewLabel.gameObject.SetActive(false);
        }

        private void Awake()
        {
            if (endTurnButton != null) endTurnButton.onClick.AddListener(() => EndTurn());
            if (grenadeButton != null) grenadeButton.onClick.AddListener(() => GrenadeButtonPressed?.Invoke());
            if (overwatchButton != null) overwatchButton.onClick.AddListener(() => OverwatchButtonPressed?.Invoke());
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
            foreach (var gameEvent in events)
            {
                switch (gameEvent)
                {
                    case UnitAttacked attacked:
                        string attacker = UnitName(attacked.AttackerKind);
                        string target = UnitName(attacked.TargetKind);
                        AddLog(attacked.IsCrit
                            ? Text.Get("ui.combat.log_crit", attacker, target, attacked.Damage)
                            : Text.Get("ui.combat.log_hit", attacker, target, attacked.Damage));
                        if (attacked.StabilityBroken) AddLog(Text.Get("ui.combat.log_break", target));
                        break;
                    case EnemiesAlerted alerted:
                        foreach (var kind in alerted.Kinds) AddLog(Text.Get("ui.combat.log_alerted", UnitName(kind)));
                        break;
                    case OverwatchSet overwatch:
                        AddLog(Text.Get("ui.combat.log_overwatch", UnitName(overwatch.Kind)));
                        break;
                    case OverwatchTriggered reaction:
                        AddLog(Text.Get("ui.combat.log_reaction", UnitName(reaction.WatcherKind)));
                        break;
                    case CoverDestroyed broken:
                        AddLog(Text.Get("ui.combat.log_cover_broken", broken.Cells.Count));
                        break;
                    case AreaExploded exploded:
                        AddLog(Text.Get("ui.combat.log_area", UnitName(exploded.AttackerKind)));
                        break;
                    case UnitDied died:
                        AddLog(Text.Get("ui.combat.log_down", UnitName(died.Kind)));
                        break;
                    case PlayerDefeated _:
                        AddLog(Text.Get("ui.combat.log_down", UnitName(UnitKind.Player)));
                        break;
                }
            }
            Refresh();
        }

        private void AddLog(string line)
        {
            log.Add(line);
            while (log.Count > logLines) log.RemoveAt(0);
            logLabel.text = string.Join("\n", log);
        }

        private string UnitName(UnitKind kind)
        {
            string key = balance.NameKey(kind);
            return key != null ? Text.Get(key) : kind.ToString();
        }

        private static string CoverName(CoverLevel cover)
        {
            switch (cover)
            {
                case CoverLevel.Full: return Text.Get("ui.cover.full");
                case CoverLevel.Half: return Text.Get("ui.cover.half");
                default: return Text.Get("ui.cover.none");
            }
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

            bool defeated = world.Mode == WorldMode.Defeat;
            defeatLabel.gameObject.SetActive(defeated);
            if (defeated) defeatLabel.text = Text.Get("ui.combat.defeat");

            bool inCombat = world.Mode == WorldMode.Combat;
            bool playerTurn = TurnFlow.IsPlayerTurn(world);
            roundLabel.gameObject.SetActive(inCombat);
            turnLabel.gameObject.SetActive(inCombat);
            endTurnButton.gameObject.SetActive(playerTurn);
            grenadeButton.gameObject.SetActive(playerTurn && !world.PlayerActed);
            overwatchButton.gameObject.SetActive(playerTurn && !world.PlayerActed && !aiming);
            if (playerTurn)
            {
                GrenadeLabel.text = aiming ? Text.Get("ui.skill.grenade_aiming")
                    : world.GrenadeCooldown > 0 ? Text.Get("ui.skill.grenade_cooldown", world.GrenadeCooldown)
                    : Text.Get("ui.skill.grenade");
            }
            if (!inCombat)
            {
                ClearPreview();
                return;
            }

            roundLabel.text = Text.Get("ui.combat.round", world.Round);
            turnLabel.text = playerTurn ? Text.Get("ui.combat.player_turn") : Text.Get("ui.combat.enemy_turn");
        }
    }
}
