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
        [Tooltip("Result title: defeat or victory.")]
        [SerializeField] private UnityEngine.UI.Text defeatLabel;
        [SerializeField] private UnityEngine.UI.Text resultLabel;
        [SerializeField] private Button newRunButton;
        [SerializeField] private UnityEngine.UI.Text floorLabel;
        [SerializeField] private UnityEngine.UI.Text weaponLabel;
        [Tooltip("Weapon compare window shown when the player steps on a weapon.")]
        [SerializeField] private GameObject weaponPanel;
        [SerializeField] private UnityEngine.UI.Text weaponPanelLabel;
        [SerializeField] private Button swapButton;
        [SerializeField] private Button keepButton;
        [Tooltip("Confirms the shot at the selected target.")]
        [SerializeField] private Button attackConfirmButton;
        [SerializeField] private int logLines = 4;

        private readonly List<string> log = new List<string>();
        private EventPlayer simulation;
        private CombatSettings settings;
        private GameBalance balance;
        private AimMode aim;
        private int shownRound;
        private bool wasPlaying;

        public string PreviewText => previewLabel.text;
        public string LogText => logLabel.text;
        public string TurnText => turnLabel.text;
        public string HpText => hpLabel.text;

        /// <summary>결과 화면의 "새 판" 버튼을 눌렀다 (GameRoot가 새 시드로 판을 다시 시작한다).</summary>
        public event System.Action NewRunPressed;

        public string ResultText => resultLabel.text;

        /// <summary>사격 확인 버튼을 눌렀다 (GameRoot가 MoveController.ConfirmAttack으로 잇는다).</summary>
        public event System.Action AttackConfirmPressed;

        public bool AttackConfirmShown => attackConfirmButton.gameObject.activeSelf;
        public bool AttackConfirmEnabled => attackConfirmButton.interactable;

        /// <summary>사격 확인 버튼: 대상을 골랐을 때만 보이고, 쏠 수 있을 때만 누를 수 있다.</summary>
        public void ShowAttackConfirm(bool shown, bool enabled)
        {
            if (attackConfirmButton == null) return;
            attackConfirmButton.gameObject.SetActive(shown);
            attackConfirmButton.interactable = enabled;
        }

        /// <summary>비교 창의 "바꾸기"를 눌렀다 (발밑 무기 ID). GameRoot가 SwapWeaponCommand를 낸다.</summary>
        public event System.Action<int> SwapPressed;

        /// <summary>비교 창이 떠 있으면 그 무기 ID, 아니면 -1.</summary>
        public int OfferedItemId { get; private set; } = -1;

        public bool IsWeaponOfferOpen => OfferedItemId >= 0;
        public string WeaponText => weaponLabel.text;
        public string WeaponPanelText => weaponPanelLabel.text;

        private UnitKind? killerKind;

        /// <summary>조준 모드가 바뀌었다 (스킬 바 카드 강조는 SkillBarView가 MoveController.Aim을 직접 읽는다).</summary>
        public void SetAiming(AimMode value)
        {
            aim = value;
            Refresh();
        }

        public void ShowPushPreview(PushPreview preview)
        {
            var lines = new List<string>();
            switch (preview.Block)
            {
                case PushBlock.NotReady:
                    lines.Add(Text.Get("ui.combat.preview_push_not_ready", simulation.World.PushCooldown));
                    break;
                case PushBlock.NoTarget:
                    lines.Add(Text.Get("ui.combat.preview_push_no_target"));
                    break;
                default:
                    lines.Add(Text.Get("ui.combat.preview_push_header", preview.Hits.Count));
                    foreach (var hit in preview.Hits)
                    {
                        string name = UnitName(hit.Target.Kind);
                        string result = hit.DiesFromHit ? Text.Get("ui.combat.push_result_down")
                            : hit.Slide.Falls ? Text.Get("ui.combat.push_result_fall")
                            : hit.Slide.Collides ? Text.Get("ui.combat.push_result_collide", hit.CollisionDamage, hit.Slide.Path.Count)
                            : Text.Get("ui.combat.push_result_slide", hit.Slide.Path.Count);
                        lines.Add(Text.Get("ui.combat.preview_push_hit", name, hit.Damage, result));
                    }
                    break;
            }
            previewLabel.text = string.Join("\n", lines);
            previewLabel.gameObject.SetActive(true);
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

        /// <summary>사건 재생기에 묶는다 — 기록 · 차례 표시가 화면 재생 순서를 따른다.</summary>
        public void Bind(EventPlayer newEventPlayer, CombatSettings newSettings, GameBalance newBalance)
        {
            Unbind();
            simulation = newEventPlayer;
            settings = newSettings;
            balance = newBalance;
            log.Clear();
            logLabel.text = string.Empty;
            killerKind = null;
            shownRound = simulation.World.Round;
            wasPlaying = false;
            simulation.EventsPublished += HandleEvents;
            CloseWeaponOffer();
            ShowAttackConfirm(false, false);
            ClearPreview();
            Refresh();
        }

        /// <summary>비교 창의 "바꾸기"와 같다. 창이 없으면 false.</summary>
        public bool PressSwap()
        {
            if (!IsWeaponOfferOpen) return false;
            int id = OfferedItemId;
            CloseWeaponOffer();
            SwapPressed?.Invoke(id);
            return true;
        }

        /// <summary>비교 창의 "두기"와 같다 — 무기는 바닥에 그대로 남는다.</summary>
        public void CloseWeaponOffer()
        {
            OfferedItemId = -1;
            if (weaponPanel != null) weaponPanel.SetActive(false);
        }

        private void OpenWeaponOffer(int itemId, WeaponKind found)
        {
            var current = settings.WeaponFor(simulation.World.PlayerWeapon);
            var next = settings.WeaponFor(found);
            weaponPanelLabel.text = Text.Get("ui.weapon.compare",
                WeaponName(current.Kind), WeaponName(next.Kind),
                current.Attack, next.Attack, current.AttackRange, next.AttackRange,
                current.StabilityDamage, next.StabilityDamage, current.CritPercent, next.CritPercent,
                WeaponRule(next.Kind));
            OfferedItemId = itemId;
            weaponPanel.SetActive(true);
        }

        /// <summary>전투 중 보급 상자에 마우스를 올렸을 때.</summary>
        public void ShowCratePreview(bool canOpen)
        {
            previewLabel.text = canOpen ? Text.Get("ui.crate.preview_open") : Text.Get("ui.crate.preview_too_far");
            previewLabel.gameObject.SetActive(true);
        }

        public void ShowAttackPreview(Unit target, AttackPreview preview, bool alreadyActed)
        {
            string name = UnitName(target.Kind);
            if (alreadyActed) previewLabel.text = Text.Get("ui.combat.preview_already_acted");
            else if (preview.Block == AttackBlock.OutOfRange) previewLabel.text = Text.Get("ui.combat.preview_out_of_range", name);
            else if (preview.Block == AttackBlock.NoLineOfFire) previewLabel.text = Text.Get("ui.combat.preview_no_line", name);
            else if (preview.Block == AttackBlock.MovedThisTurn) previewLabel.text = Text.Get("ui.combat.preview_moved", name);
            else
            {
                string cover = CoverName(preview.Cover);
                if (preview.ReductionPercent > 0) cover = cover + " · " + Text.Get("ui.combat.reduction", preview.ReductionPercent);
                if (preview.FalloffPercent > 0) cover = cover + " · " + Text.Get("ui.combat.falloff", preview.FalloffPercent);
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
            if (newRunButton != null) newRunButton.onClick.AddListener(() => NewRunPressed?.Invoke());
            if (swapButton != null) swapButton.onClick.AddListener(() => PressSwap());
            if (keepButton != null) keepButton.onClick.AddListener(CloseWeaponOffer);
            if (attackConfirmButton != null) attackConfirmButton.onClick.AddListener(() => AttackConfirmPressed?.Invoke());
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
                    case TurnStarted started:
                        shownRound = started.Round;
                        break;
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
                    case PushUsed pushed:
                        AddLog(Text.Get("ui.combat.log_push", UnitName(pushed.AttackerKind)));
                        break;
                    case UnitPushed slid:
                        // 플레이어가 떨어진 것은 PlayerFell 한 줄로 알린다.
                        if (slid.Fell && slid.Kind != UnitKind.Player) AddLog(Text.Get("ui.combat.log_fell", UnitName(slid.Kind)));
                        else if (slid.CollisionDamage > 0) AddLog(Text.Get("ui.combat.log_collide", UnitName(slid.Kind), slid.CollisionDamage));
                        break;
                    case PlayerFell fell:
                        AddLog(fell.To != fell.From
                            ? Text.Get("ui.combat.log_player_recovered", fell.Damage)
                            : Text.Get("ui.combat.log_player_fell_down", fell.Damage));
                        break;
                    case AreaExploded exploded:
                        AddLog(Text.Get("ui.combat.log_area", UnitName(exploded.AttackerKind)));
                        break;
                    case UnitDied died:
                        AddLog(Text.Get("ui.combat.log_down", UnitName(died.Kind)));
                        break;
                    case FloorChanged floor:
                        CloseWeaponOffer();
                        AddLog(Text.Get("ui.combat.log_floor", floor.FloorIndex + 1));
                        break;
                    case UnitMoved moved when moved.UnitId == simulation.World.PlayerId:
                        // 발밑을 떠나면 비교 창을 닫는다 (무기는 바닥에 남는다).
                        CloseWeaponOffer();
                        break;
                    case SupplyCrateOpened _:
                        AddLog(Text.Get("ui.crate.log_open"));
                        break;
                    case ItemDropped dropped when dropped.Source == DropSource.Combat || dropped.Source == DropSource.SupplyCrate:
                        AddLog(dropped.Kind == ItemKind.Weapon
                            ? Text.Get("ui.loot.log_weapon_dropped", WeaponName(dropped.Weapon))
                            : Text.Get("ui.loot.log_medkit_dropped"));
                        break;
                    case MedkitUsed medkit:
                        AddLog(Text.Get("ui.loot.log_medkit_used", medkit.Healed));
                        break;
                    case WeaponFound found:
                        OpenWeaponOffer(found.ItemId, found.Weapon);
                        break;
                    case WeaponSwapped swapped:
                        AddLog(Text.Get("ui.loot.log_swapped", WeaponName(swapped.To)));
                        break;
                    case PlayerDefeated defeated:
                        // 결과 화면에 쓰러뜨린 상대를 보여 준다 (자기 수류탄이면 나).
                        killerKind = simulation.World.FindUnit(defeated.KillerId)?.Kind;
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

        private string WeaponName(WeaponKind kind)
        {
            string key = balance.WeaponNameKey(kind);
            return key != null ? Text.Get(key) : kind.ToString();
        }

        private string WeaponRule(WeaponKind kind)
        {
            string key = balance.WeaponRuleKey(kind);
            return key != null ? Text.Get(key) : string.Empty;
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
            // 상태는 이미 끝난 결과다. 적 차례를 재생하는 동안에는 체력 · 결과 화면을 바꾸지 않고, 다 보여 준 뒤에 맞춘다 (Update).
            bool playing = simulation.IsPlaying;
            if (player != null && !playing)
            {
                hpLabel.text = Text.Get("ui.hud.hp", player.Hp, player.MaxHp);
                stabilityLabel.text = Text.Get("ui.hud.stability", player.Stability, player.MaxStability);
                // 표시 값 = 실제 값: 무기 표가 아니라 유닛에 적용된 값(Loadout.Apply 결과)을 읽는다.
                weaponLabel.text = Text.Get("ui.hud.weapon", WeaponName(world.PlayerWeapon), player.Attack, player.AttackRange);
            }

            floorLabel.text = Text.Get("ui.hud.floor", world.FloorIndex + 1, settings.FloorCount);

            // 결과 화면: 패배 · 승리 제목, 도달한 층 · 쓰러뜨린 적 · 시드, 쓰러뜨린 상대, "새 판" 버튼.
            bool defeated = world.Mode == WorldMode.Defeat && !playing;
            bool won = world.Mode == WorldMode.Victory && !playing;
            defeatLabel.gameObject.SetActive(defeated || won);
            resultLabel.gameObject.SetActive(defeated || won);
            newRunButton.gameObject.SetActive(defeated || won);
            if (defeated || won)
            {
                defeatLabel.text = won ? Text.Get("ui.result.victory") : Text.Get("ui.combat.defeat");
                string detail = Text.Get("ui.result.detail", world.FloorIndex + 1, settings.FloorCount, world.Kills, world.Seed);
                if (defeated && killerKind.HasValue) detail += "\n" + Text.Get("ui.result.killer", UnitName(killerKind.Value));
                resultLabel.text = detail;
            }

            bool inCombat = world.Mode == WorldMode.Combat;
            // 차례 표시는 화면에 지금 재생 중인 차례 (EventPlayer.ShownTurn) — 적 차례 동안 "적 차례"가 보인다.
            bool playerTurn = simulation.ShownTurn == TurnSide.Player;
            roundLabel.gameObject.SetActive(inCombat);
            turnLabel.gameObject.SetActive(inCombat);
            if (!inCombat)
            {
                ClearPreview();
                return;
            }
            if (!playerTurn) ClearPreview();

            roundLabel.text = Text.Get("ui.combat.round", playing ? shownRound : world.Round);
            turnLabel.text = playerTurn ? Text.Get("ui.combat.player_turn") : Text.Get("ui.combat.enemy_turn");
            turnLabel.color = playerTurn ? Color.white : enemyTurnColor;
        }

        [SerializeField] private Color enemyTurnColor = new Color(1f, 0.45f, 0.4f);

        private void Update()
        {
            // 재생이 끝나는 순간 체력 · 결과 화면을 실제 상태에 맞춘다.
            if (simulation == null) return;
            bool playing = simulation.IsPlaying;
            if (wasPlaying && !playing) Refresh();
            wasPlaying = playing;
        }
    }
}
