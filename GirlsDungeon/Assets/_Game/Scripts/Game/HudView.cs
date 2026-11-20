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

        /// <summary>스킬을 못 쓰는 이유: 리몰딩을 쓰는 스킬이면 모자란 지수, 아니면 재사용 대기 (Remold 한 곳에서 읽는다, 3-16).</summary>
        private string NotReadyText(SkillKind skill, string cooldownKey)
        {
            var world = simulation.World;
            int cost = Remold.Cost(world, settings, skill);
            return cost > 0 ? Text.Get("ui.remold.not_enough", cost, world.PlayerRemold) : Text.Get(cooldownKey, Remold.Cooldown(world, settings, skill));
        }

        public void ShowPushPreview(PushPreview preview)
        {
            var lines = new List<string>();
            switch (preview.Block)
            {
                case PushBlock.NotReady:
                    lines.Add(NotReadyText(SkillKind.Push, "ui.combat.preview_push_not_ready"));
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
                    lines.Add(NotReadyText(SkillKind.Grenade, "ui.combat.preview_area_not_ready"));
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
                case PartSet.Decisive: return Text.Get("part.set.decisive.name");
                case PartSet.DualCounter: return Text.Get("part.set.dual_counter.name");
                case PartSet.PhaseStrike: return Text.Get("part.set.phase_strike.name");
                case PartSet.PhaseAmp: return Text.Get("part.set.phase_amp.name");
                case PartSet.Kinetic: return Text.Get("part.set.kinetic.name");
                case PartSet.Blaze: return Text.Get("part.set.blaze.name");
                case PartSet.Frost: return Text.Get("part.set.frost.name");
                case PartSet.Acid: return Text.Get("part.set.acid.name");
                case PartSet.Magnetic: return Text.Get("part.set.magnetic.name");
                case PartSet.Torrent: return Text.Get("part.set.torrent.name");
                case PartSet.Tactical: return Text.Get("part.set.tactical.name");
                case PartSet.Assault: return Text.Get("part.set.assault.name");
                case PartSet.Apex: return Text.Get("part.set.apex.name");
                default: return string.Empty;
            }
        }

        /// <summary>세트 규칙 글 — 숫자는 수치 표(CombatSettings.Set…)에서 넣는다 (표시 값 = 실제 값).</summary>
        private string SetRule(PartSet set)
        {
            switch (set)
            {
                case PartSet.Decisive: return Text.Get("part.set.decisive.rule", settings.SetDecisivePercentPerStack, settings.SetDecisiveMaxStacks);
                case PartSet.DualCounter: return Text.Get("part.set.dual_counter.rule", settings.SetDualCounterPercent);
                case PartSet.PhaseStrike: return Text.Get("part.set.phase_strike.rule", settings.SetPhaseStrikePercent);
                case PartSet.PhaseAmp: return Text.Get("part.set.phase_amp.rule", settings.SetPhaseAmpTurns, settings.SetPhaseAmpPercent, settings.SetPhaseAmpDoublePercent);
                case PartSet.Kinetic: return Text.Get("part.set.kinetic.rule", settings.SetElementPercent);
                case PartSet.Blaze: return Text.Get("part.set.blaze.rule", settings.SetElementPercent);
                case PartSet.Frost: return Text.Get("part.set.frost.rule", settings.SetElementPercent);
                case PartSet.Acid: return Text.Get("part.set.acid.rule", settings.SetElementPercent);
                case PartSet.Magnetic: return Text.Get("part.set.magnetic.rule", settings.SetElementPercent);
                case PartSet.Torrent: return Text.Get("part.set.torrent.rule", settings.SetElementPercent);
                case PartSet.Tactical: return Text.Get("part.set.tactical.rule", settings.SetTacticalPercent);
                case PartSet.Assault: return Text.Get("part.set.assault.rule", settings.SetAssaultPercent, settings.SetAssaultMeleePercent);
                case PartSet.Apex: return Text.Get("part.set.apex.rule", settings.SetApexPercent);
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
                case PartStat.AttackPercent: return "part.stat_name.attack_percent";
                case PartStat.MaxHpPercent: return "part.stat_name.hp_percent";
                case PartStat.DefensePercent: return "part.stat_name.defense_percent";
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
                case PartStat.AttackPercent: return "part.stat.attack_percent";
                case PartStat.MaxHpPercent: return "part.stat.hp_percent";
                case PartStat.DefensePercent: return "part.stat.defense_percent";
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
        private bool showingShopItem;

        /// <summary>탐험 중 상점 진열 물건 위에 마우스: 이름 · 값 (3-15).</summary>
        public void ShowShopItemPreview(FloorItem item)
        {
            previewLabel.text = Text.Get("ui.shop.preview", ItemName(item), item.Price, simulation.World.Gold);
            previewLabel.gameObject.SetActive(true);
            showingShopItem = true;
        }

        public void ClearShopItemPreview()
        {
            if (!showingShopItem) return;
            showingShopItem = false;
            ClearPreview();
        }

        /// <summary>바닥 물건 이름 (상점 창 · 미리보기): 무기 이름 · "{무기} {등급} 파츠" · 구급상자 · 열쇠.</summary>
        internal string ItemName(FloorItem item)
        {
            switch (item.Kind)
            {
                case ItemKind.Weapon: return WeaponName(item.Weapon);
                case ItemKind.Part: return item.Part != null ? Text.Get("ui.shop.item_part", WeaponName(item.Part.Weapon), GradeName(item.Part.Grade)) : WeaponName(item.Weapon);
                case ItemKind.Key: return Text.Get("ui.shop.item_key");
                case ItemKind.Gold: return Text.Get("ui.loot.item_gold", item.Amount);
                default: return Text.Get("ui.shop.item_medkit");
            }
        }

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
                else if (preview.FalloffPercent < 0) cover = cover + " · " + Text.Get("ui.combat.range_bonus", -preview.FalloffPercent);
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
                    previewLabel.text = NotReadyText(SkillKind.Sonic, "ui.combat.preview_sonic_not_ready");
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

        /// <summary>서리 결전 미리보기 (규칙 FrostDecisive.Preview의 값 그대로, 3-17c).</summary>
        public void ShowFrostDecisivePreview(FrostDecisivePreview preview)
        {
            switch (preview.Block)
            {
                case FrostDecisiveBlock.NotReady:
                    previewLabel.text = NotReadyText(SkillKind.FrostDecisive, "ui.combat.preview_frost_guard_not_ready");
                    break;
                case FrostDecisiveBlock.OutOfRange:
                    previewLabel.text = Text.Get("ui.combat.preview_decisive_out_of_range");
                    break;
                case FrostDecisiveBlock.NotEmpty:
                    previewLabel.text = Text.Get("ui.combat.preview_decisive_not_empty");
                    break;
                default:
                    previewLabel.text = Text.Get("ui.combat.preview_decisive", preview.Targets.Count, preview.FixedDamage, preview.AvalancheStacks, preview.Shield, preview.StabilityRestored);
                    break;
            }
            previewLabel.gameObject.SetActive(true);
        }

        /// <summary>서리 수호 미리보기 (규칙 FrostGuard.Preview의 값 그대로, 3-17b).</summary>
        public void ShowFrostGuardPreview(FrostGuardPreview preview)
        {
            previewLabel.text = !preview.CanUse
                ? NotReadyText(SkillKind.FrostGuard, "ui.combat.preview_frost_guard_not_ready")
                : Text.Get("ui.combat.preview_frost_guard", preview.Shield, preview.StabilityRestored, preview.EnemiesInRange, preview.StacksAfter, preview.DefenseBonusAfter);
            previewLabel.gameObject.SetActive(true);
        }

        /// <summary>빙결 사격 조준 중, 마우스가 적 위에 있지 않을 때 (3-16z).</summary>
        public void ShowFrostHint()
        {
            previewLabel.text = Text.Get("ui.combat.preview_frost_hint");
            previewLabel.gameObject.SetActive(true);
        }

        /// <summary>빙결 사격 미리보기 (규칙 FrostAttack.Preview의 값 그대로).</summary>
        public void ShowFrostPreview(FrostPreview preview)
        {
            string name = preview.Target != null ? UnitName(preview.Target.Kind) : string.Empty;
            switch (preview.Block)
            {
                case SonicBlock.NotReady:
                    previewLabel.text = NotReadyText(SkillKind.Frost, "ui.combat.preview_frost_not_ready");
                    break;
                case SonicBlock.OutOfRange:
                    previewLabel.text = Text.Get("ui.combat.preview_out_of_range", name);
                    break;
                case SonicBlock.NotVisible:
                case SonicBlock.NoTarget:
                    previewLabel.text = Text.Get("ui.combat.preview_frost_not_visible", name);
                    break;
                default:
                    previewLabel.text = Text.Get("ui.combat.preview_frost", name, preview.Target.Hp, preview.Target.MaxHp,
                        preview.Target.Stability, preview.Target.MaxStability, preview.Damage, preview.MainDamage, preview.FixedDamage, CoverName(preview.Cover), preview.StabilityDamage);
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
                            : dropped.Kind == ItemKind.Gold ? Text.Get("ui.loot.log_gold_dropped")
                            : Text.Get("ui.loot.log_medkit_dropped"));
                        break;
                    case PartFound partFound:
                        OpenPartOffer(partFound.ItemId);
                        break;
                    case PartEquipped equipped:
                        AddLog(Text.Get("ui.loot.log_part_equipped", PartSlotName(equipped.Weapon, equipped.Slot), GradeName(equipped.Grade)));
                        break;
                    case GoldGained gold when gold.Source != GoldSource.Kill:
                        AddLog(Text.Get("ui.gold.log_gain", gold.Amount, gold.Total));
                        break;
                    case ShopkeeperAngered _:
                        AddLog(Text.Get("ui.shop.log_angered"));
                        break;
                    case ActionSupportTriggered _:
                        AddLog(Text.Get("ui.support.log_triggered"));
                        break;
                    case CritBoosted boosted:
                        AddLog(Text.Get("ui.support.log_crit_boost", boosted.Percent, boosted.Turns));
                        break;
                    case FrostDecisiveUsed decisiveUsed:
                        AddLog(Text.Get("ui.decisive.log_used", decisiveUsed.Hits));
                        break;
                    case FrostDecisiveSelf decisiveSelf:
                        AddLog(Text.Get(decisiveSelf.Cleansed ? "ui.decisive.log_self_cleansed" : "ui.decisive.log_self", decisiveSelf.Shield, decisiveSelf.StabilityRestored, decisiveSelf.BulwarkStacks));
                        break;
                    case AvalancheStacked avalanche:
                        AddLog(Text.Get("ui.decisive.log_avalanche", UnitName(avalanche.Kind), avalanche.Stacks));
                        break;
                    case AvalancheBurst burst:
                        AddLog(Text.Get("ui.decisive.log_avalanche_burst", UnitName(burst.Kind), burst.StabilityDamage));
                        break;
                    case UnitCoagulated coag:
                        AddLog(Text.Get("ui.decisive.log_coag", UnitName(coag.Kind), coag.Stacks));
                        break;
                    case FrostGuardUsed guard:
                        AddLog(Text.Get("ui.frost_guard.log_used", guard.Shield, guard.StabilityRestored, guard.StacksAdded, guard.Stacks, guard.DefenseBonus));
                        break;
                    case PlayerShieldAbsorbed absorbed:
                        AddLog(absorbed.Remaining > 0 ? Text.Get("ui.frost_guard.log_absorbed", absorbed.Absorbed, absorbed.Remaining) : Text.Get("ui.frost_guard.log_broken", absorbed.Absorbed));
                        break;
                    case UnitFrozen frozen:
                        AddLog(Text.Get("ui.frost_guard.log_frozen", UnitName(frozen.Kind), frozen.Turns));
                        break;
                    case TraitHealed traitHealed:
                        AddLog(Text.Get("ui.remold.log_heal", traitHealed.Amount));
                        break;
                    case UnitSlowed slowed:
                        AddLog(Text.Get("ui.remold.log_slowed", UnitName(slowed.Kind), slowed.Amount, slowed.Turns));
                        break;
                    case KeeperSkillUsed skill:
                        // 키는 통째로 쓴다 (이어 붙이면 키 검사가 못 찾는다).
                        if (skill.Skill == KeeperSkill.Heart) AddLog(Text.Get("ui.keeper.log_heart", skill.HpCost, skill.Stacks));
                        else if (skill.Skill == KeeperSkill.Order) AddLog(Text.Get("ui.keeper.log_order", skill.HpCost, skill.Stacks));
                        else AddLog(Text.Get("ui.keeper.log_final", skill.Stacks, skill.Shield));
                        break;
                    case KeeperShieldAbsorbed absorbed:
                        AddLog(Text.Get("ui.keeper.log_shield", absorbed.Absorbed, absorbed.Left));
                        break;
                    case BurnTicked burn:
                        AddLog(Text.Get("ui.keeper.log_burn", burn.Damage, burn.TurnsLeft));
                        break;
                    case ShopkeeperDefeated defeated:
                        AddLog(Text.Get("ui.shop.log_defeated", defeated.Freed));
                        break;
                    case ShopItemBought bought:
                        AddLog(Text.Get("ui.shop.log_bought", bought.Price, bought.GoldNow));
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
                // 서리 보호막 · 방위 지원 (3-17b)이 있으면 같은 줄에 붙여 보인다 (키를 통째로).
                hpLabel.text = world.BulwarkStacks > 0
                    ? Text.Get("ui.hud.hp_shield_bulwark", player.Hp, player.MaxHp, world.PlayerShield, world.BulwarkStacks)
                    : world.PlayerShield > 0
                    ? Text.Get("ui.hud.hp_shield", player.Hp, player.MaxHp, world.PlayerShield)
                    : Text.Get("ui.hud.hp", player.Hp, player.MaxHp);
                stabilityLabel.text = world.DefenseSupportStacks > 0
                    ? Text.Get("ui.hud.stability_support", player.Stability, player.MaxStability, world.DefenseSupportStacks, world.DefenseSupportApplied)
                    : Text.Get("ui.hud.stability", player.Stability, player.MaxStability);
                // 표시 값 = 실제 값: 무기 표가 아니라 유닛에 적용된 값(Loadout.Apply 결과)을 읽는다.
                weaponLabel.text = Text.Get("ui.hud.weapon", WeaponName(world.PlayerWeapon), player.Attack, player.AttackRange);
                // 사르디스 골드 (3-15): 늘 보인다.
                weaponLabel.text += "\n" + Text.Get("ui.hud.gold", world.Gold);
                // 열쇠 (3-4d): 하나라도 있을 때만.
                if (world.Keys > 0) weaponLabel.text += "\n" + Text.Get("ui.hud.keys", world.Keys);
                // 재료 · 보관함 (3-4c): 하나라도 있을 때만.
                if (world.PartMaterials > 0 || world.SpareParts.Count > 0)
                    weaponLabel.text += "\n" + Text.Get("ui.hud.materials", world.PartMaterials, world.SpareParts.Count, settings.SpareSlots);
                // 켜진 세트 (3-4b · 3-16e): 이름 · 결전 추진 중첩.
                var activeSet = SetEffects.Active(world, settings);
                if (activeSet == PartSet.Decisive) weaponLabel.text += "\n" + Text.Get("ui.hud.set_stacks", SetName(activeSet), world.SetDecisiveStacks, settings.SetDecisiveMaxStacks);
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
