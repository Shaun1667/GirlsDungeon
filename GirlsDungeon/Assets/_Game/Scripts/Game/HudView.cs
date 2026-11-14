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
        [Tooltip("Puts the part under the player into spare storage (part offers only).")]
        [SerializeField] private Button storeButton;
        [Tooltip("Confirms the shot at the selected target.")]
        [SerializeField] private Button attackConfirmButton;
        [SerializeField] private int logLines = 4;

        private readonly List<string> log = new List<string>();
        private EventPlayer simulation;
        private CombatSettings settings;
        private GameBalance balance;
        private MapSettings mapSettings;
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

        /// <summary>파츠 비교 창의 "보관"을 눌렀다 (발밑 파츠 ID). GameRoot가 StorePartCommand를 낸다 (3-4c).</summary>
        public event System.Action<int> StorePressed;

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
            mapSettings = newBalance != null ? newBalance.CreateMapSettings() : null;
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

        /// <summary>
        /// 발밑 파츠 비교 창 (3-4): 새 파츠의 칸 · 등급 · 능력치와 지금 그 칸에 끼운 파츠. 든 무기에 맞지 않으면 "바꾸기"를 누를 수 없다.
        /// "바꾸기" = SwapPressed(파츠 ID) → GameRoot가 EquipPartCommand를 낸다.
        /// </summary>
        private void OpenPartOffer(int itemId)
        {
            var world = simulation.World;
            var item = world.FindItem(itemId);
            if (item == null || item.Part == null) return;
            var part = item.Part;
            bool fits = Parts.Fits(world, part);
            var current = fits ? world.PlayerParts[part.Slot] : null;
            weaponPanelLabel.text = Text.Get("ui.part.compare", WeaponName(part.Weapon), PartSlotName(part.Weapon, part.Slot), GradeName(part.Grade),
                PartStats(part), current != null ? PartSummary(current) : Text.Get("ui.part.empty"));
            if (part.Set != PartSet.None)
            {
                // 끼우면 그 세트가 몇 개가 되는지 (같은 칸의 원래 파츠가 같은 세트면 빠지는 것까지).
                int after = SetEffects.Count(world, part.Set) - (current != null && current.Set == part.Set ? 1 : 0) + (fits ? 1 : 0);
                weaponPanelLabel.text += "\n" + Text.Get("ui.part.set", SetName(part.Set), after, settings.SetPiecesRequired, SetRule(part.Set));
            }
            if (!fits) weaponPanelLabel.text += "\n" + Text.Get("ui.part.not_fit", WeaponName(part.Weapon));
            OfferedItemId = itemId;
            weaponPanel.SetActive(true);
            if (swapButton != null) swapButton.interactable = fits;
            // 보관함 (3-4c): 어느 무기용이든 넣을 수 있다 — 차면 누를 수 없다.
            if (storeButton != null)
            {
                storeButton.gameObject.SetActive(true);
                storeButton.interactable = world.SpareParts.Count < settings.SpareSlots;
            }
        }

        internal string PartSlotName(WeaponKind weapon, int slot)
        {
            string key = balance.PartSlotKey(weapon, slot);
            return key != null ? Text.Get(key) : (slot + 1).ToString();
        }

        /// <summary>지금 끼운 파츠 한 줄: 등급 · 능력치 (세트가 있으면 세트 이름까지).</summary>
        private static string PartSummary(WeaponPart part)
        {
            return part.Set != PartSet.None
                ? Text.Get("ui.part.summary_set", GradeName(part.Grade), PartStats(part), SetName(part.Set))
                : Text.Get("ui.part.summary", GradeName(part.Grade), PartStats(part));
        }

        /// <summary>세트 이름 (텍스트 표 part.set.{이름}.name).</summary>
        internal static string SetName(PartSet set)
        {
            switch (set)
            {
                case PartSet.Chain: return Text.Get("part.set.chain.name");
                case PartSet.TwoSided: return Text.Get("part.set.two_sided.name");
                case PartSet.Fracture: return Text.Get("part.set.fracture.name");
                case PartSet.Emergency: return Text.Get("part.set.emergency.name");
                case PartSet.Bulwark: return Text.Get("part.set.bulwark.name");
                case PartSet.Ambush: return Text.Get("part.set.ambush.name");
                case PartSet.Assault: return Text.Get("part.set.assault.name");
                default: return string.Empty;
            }
        }

        /// <summary>세트 규칙 글 — 숫자는 수치 표(CombatSettings.Set…)에서 넣는다 (표시 값 = 실제 값).</summary>
        private string SetRule(PartSet set)
        {
            switch (set)
            {
                case PartSet.Chain: return Text.Get("part.set.chain.rule", settings.SetChainPercentPerStack, settings.SetChainMaxStacks);
                case PartSet.TwoSided: return Text.Get("part.set.two_sided.rule", settings.SetTwoSidedPercent);
                case PartSet.Fracture: return Text.Get("part.set.fracture.rule", settings.SetFracturePercent);
                case PartSet.Emergency: return Text.Get("part.set.emergency.rule", settings.SetEmergencyStability);
                case PartSet.Bulwark: return Text.Get("part.set.bulwark.rule", settings.SetBulwarkTurns, settings.SetBulwarkPercent);
                case PartSet.Ambush: return Text.Get("part.set.ambush.rule", settings.SetAmbushPercent);
                case PartSet.Assault: return Text.Get("part.set.assault.rule", settings.SetAssaultPercent, settings.SetAssaultMeleePercent);
                default: return string.Empty;
            }
        }

        internal static string GradeName(PartGrade grade)
        {
            switch (grade)
            {
                case PartGrade.Precision: return Text.Get("part.grade.precision");
                case PartGrade.Military: return Text.Get("part.grade.military");
                default: return Text.Get("part.grade.industrial");
            }
        }

        /// <summary>파츠 능력치 목록 (예: "공격 +2 · 체력 +8"). 항목마다 표의 글 하나, 사이는 구분점.</summary>
        internal static string PartStats(WeaponPart part)
        {
            var lines = new List<string>();
            foreach (var bonus in part.Bonuses) lines.Add(Text.Get(StatKey(bonus.Stat), bonus.Value));
            return string.Join(" · ", lines);
        }

        /// <summary>능력치 이름만 (값 없이, 작업 화면의 "원하는 능력치").</summary>
        internal static string StatNameKey(PartStat stat)
        {
            switch (stat)
            {
                case PartStat.Attack: return "part.stat_name.attack";
                case PartStat.AttackRange: return "part.stat_name.range";
                case PartStat.CritPercent: return "part.stat_name.crit";
                case PartStat.CritMultiplier: return "part.stat_name.crit_multiplier";
                case PartStat.StabilityDamage: return "part.stat_name.stability_damage";
                case PartStat.MaxHp: return "part.stat_name.hp";
                case PartStat.Defense: return "part.stat_name.defense";
                case PartStat.MoveRange: return "part.stat_name.move";
                default: return "part.stat_name.stability";
            }
        }

        internal static string StatKey(PartStat stat)
        {
            switch (stat)
            {
                case PartStat.Attack: return "part.stat.attack";
                case PartStat.AttackRange: return "part.stat.range";
                case PartStat.CritPercent: return "part.stat.crit";
                case PartStat.CritMultiplier: return "part.stat.crit_multiplier";
                case PartStat.StabilityDamage: return "part.stat.stability_damage";
                case PartStat.MaxHp: return "part.stat.hp";
                case PartStat.Defense: return "part.stat.defense";
                case PartStat.MoveRange: return "part.stat.move";
                default: return "part.stat.stability";
            }
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

        /// <summary>파츠 비교 창의 "보관"과 같다. 창이 없으면 false.</summary>
        public bool PressStore()
        {
            if (!IsWeaponOfferOpen) return false;
            int id = OfferedItemId;
            CloseWeaponOffer();
            StorePressed?.Invoke(id);
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
            // 파츠는 무기에 붙어 다닌다 — 바닥 무기에 끼워진 파츠 수를 알려 준다.
            var item = simulation.World.FindItem(itemId);
            int partCount = 0;
            if (item != null) foreach (var part in item.Parts) if (part != null) partCount++;
            if (partCount > 0) weaponPanelLabel.text += "\n" + Text.Get("ui.part.on_weapon", partCount);
            OfferedItemId = itemId;
            weaponPanel.SetActive(true);
            if (swapButton != null) swapButton.interactable = true;
            if (storeButton != null) storeButton.gameObject.SetActive(false);
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

        /// <summary>음파 공격 조준 중, 마우스가 적 위에 있지 않을 때.</summary>
        public void ShowSonicHint()
        {
            previewLabel.text = Text.Get("ui.combat.preview_sonic_hint");
            previewLabel.gameObject.SetActive(true);
        }

        /// <summary>음파 공격 미리보기 (규칙 SonicAttack.Preview의 값 그대로).</summary>
        public void ShowSonicPreview(SonicPreview preview)
        {
            string name = preview.Target != null ? UnitName(preview.Target.Kind) : string.Empty;
            switch (preview.Block)
            {
                case SonicBlock.NotReady:
                    previewLabel.text = Text.Get("ui.combat.preview_sonic_not_ready", simulation.World.SonicCooldown);
                    break;
                case SonicBlock.OutOfRange:
                    previewLabel.text = Text.Get("ui.combat.preview_out_of_range", name);
                    break;
                case SonicBlock.NotVisible:
                case SonicBlock.NoTarget:
                    previewLabel.text = Text.Get("ui.combat.preview_sonic_not_visible", name);
                    break;
                default:
                    previewLabel.text = Text.Get("ui.combat.preview_sonic", name, preview.Target.Hp, preview.Target.MaxHp,
                        preview.Target.Stability, preview.Target.MaxStability, preview.Damage, preview.StabilityDamage);
                    break;
            }
            previewLabel.gameObject.SetActive(true);
        }

        /// <summary>이동 예약(홀로그램) 칸: 엄폐와 조작 안내 (다시 클릭 · Space = 이동, 적 클릭 = 이동 후 공격, 오른쪽 클릭 = 취소).</summary>
        public void ShowPlannedMovePreview(CoverLevel cover)
        {
            previewLabel.text = Text.Get("ui.combat.preview_planned_move", CoverName(cover));
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
            if (storeButton != null) storeButton.onClick.AddListener(() => PressStore());
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
                    case AltarTraded traded:
                        AddLog(Text.Get("ui.altar.log_traded", Text.Get(StatNameKey(traded.CostStat)), traded.CostAmount, Text.Get(StatNameKey(traded.BoonStat)), traded.BoonAmount));
                        break;
                    case ArchiveUsed _:
                        ShowArchiveForecast();
                        break;
                    case GambleOpened gamble:
                        AddLog(Text.Get(gamble.Outcome == GambleOutcome.Jackpot ? "ui.gamble.log_jackpot"
                            : gamble.Outcome == GambleOutcome.Small ? "ui.gamble.log_small"
                            : gamble.Outcome == GambleOutcome.Empty ? "ui.gamble.log_empty" : "ui.gamble.log_ambush"));
                        break;
                    case ArenaCleared cleared:
                        AddLog(Text.Get("ui.arena.log_cleared", GradeName(cleared.Grade)));
                        break;
                    case GalleryPicked picked:
                        AddLog(Text.Get("ui.gallery.log_picked", SetName(picked.Set)));
                        break;
                    case ExchangeTraded traded:
                        AddLog(Text.Get(traded.Offer == ExchangeOffer.Medkit ? "ui.exchange.log_medkit"
                            : traded.Offer == ExchangeOffer.Part ? "ui.exchange.log_part"
                            : traded.Offer == ExchangeOffer.Key ? "ui.exchange.log_key" : "ui.exchange.log_sell_key"));
                        break;
                    case MapRevealed revealed:
                        AddLog(Text.Get("ui.observatory.log_used", revealed.Enemies));
                        break;
                    case RepairUsed repair:
                        AddLog(Text.Get("ui.repair.log_used", repair.Healed));
                        break;
                    case StorehouseOpened storehouse:
                        AddLog(Text.Get("ui.storehouse.log_open", storehouse.Gained));
                        break;
                    case TreasureOpened _:
                        AddLog(Text.Get("ui.treasure.log_open"));
                        break;
                    case SupplyCrateOpened _:
                        AddLog(Text.Get("ui.crate.log_open"));
                        break;
                    case ItemDropped dropped when dropped.Source == DropSource.Combat || dropped.Source == DropSource.SupplyCrate:
                        AddLog(dropped.Kind == ItemKind.Weapon ? Text.Get("ui.loot.log_weapon_dropped", WeaponName(dropped.Weapon))
                            : dropped.Kind == ItemKind.Part ? Text.Get("ui.loot.log_part_dropped", WeaponName(dropped.Weapon))
                            : dropped.Kind == ItemKind.Key ? Text.Get("ui.door.log_key_dropped")
                            : Text.Get("ui.loot.log_medkit_dropped"));
                        break;
                    case PartFound partFound:
                        OpenPartOffer(partFound.ItemId);
                        break;
                    case PartEquipped equipped:
                        AddLog(Text.Get("ui.loot.log_part_equipped", PartSlotName(equipped.Weapon, equipped.Slot), GradeName(equipped.Grade)));
                        break;
                    case KeyPickedUp key:
                        AddLog(Text.Get("ui.door.log_key", key.Keys));
                        break;
                    case DoorOpened opened:
                        AddLog(Text.Get(opened.Method == DoorOpenMethod.Force ? "ui.door.log_forced" : "ui.door.log_opened"));
                        break;
                    case DoorForceFailed _:
                        AddLog(Text.Get("ui.door.log_force_failed"));
                        break;
                    case PartStored stored:
                        AddLog(Text.Get("ui.loot.log_part_stored", WeaponName(stored.Weapon), stored.SpareCount, settings.SpareSlots));
                        break;
                    case PartDisassembled disassembled:
                        AddLog(Text.Get("ui.workshop.log_disassembled", PartSlotName(disassembled.Weapon, disassembled.Slot), disassembled.Gained));
                        break;
                    case PartReworked reworked:
                        AddLog(Text.Get(reworked.Success ? "ui.workshop.log_success" : "ui.workshop.log_fail"));
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

        /// <summary>기록실 예고 (3-14l): 다음 층의 특수 방 · 적 수 — Archive.Forecast (복제본에서 다음 층을 실제로 만들어 본 값).</summary>
        private void ShowArchiveForecast()
        {
            if (mapSettings == null || settings == null) return;
            var forecast = Archive.Forecast(simulation.World, mapSettings, settings);
            if (forecast.LastFloor)
            {
                AddLog(Text.Get("ui.archive.log_last"));
                return;
            }
            var names = new List<string>();
            foreach (var kind in forecast.Rooms) names.Add(Text.Get(RoomLabelsView.NameKey(kind)));
            AddLog(Text.Get("ui.archive.log_rooms", string.Join(" · ", names)));
            AddLog(Text.Get("ui.archive.log_enemies", forecast.Enemies));
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

        internal string WeaponName(WeaponKind kind)
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
                // 열쇠 (3-4d): 하나라도 있을 때만.
                if (world.Keys > 0) weaponLabel.text += "\n" + Text.Get("ui.hud.keys", world.Keys);
                // 재료 · 보관함 (3-4c): 하나라도 있을 때만.
                if (world.PartMaterials > 0 || world.SpareParts.Count > 0)
                    weaponLabel.text += "\n" + Text.Get("ui.hud.materials", world.PartMaterials, world.SpareParts.Count, settings.SpareSlots);
                // 켜진 세트 (3-4b): 이름 · 연쇄 가속 중첩 · 방호 태세 남은 차례.
                var activeSet = SetEffects.Active(world, settings);
                if (activeSet == PartSet.Chain) weaponLabel.text += "\n" + Text.Get("ui.hud.set_chain", SetName(activeSet), world.SetChainStacks, settings.SetChainMaxStacks);
                else if (activeSet == PartSet.Bulwark && world.SetBulwarkTurns > 0) weaponLabel.text += "\n" + Text.Get("ui.hud.set_bulwark", SetName(activeSet), world.SetBulwarkTurns);
                else if (activeSet != PartSet.None) weaponLabel.text += "\n" + Text.Get("ui.hud.set", SetName(activeSet));
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
