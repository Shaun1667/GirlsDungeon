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
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button grenadeButton;
        [SerializeField] private Button pushButton;
        [SerializeField] private Button overwatchButton;
        [SerializeField] private UnityEngine.UI.Text weaponLabel;
        [Tooltip("Weapon compare window shown when the player steps on a weapon.")]
        [SerializeField] private GameObject weaponPanel;
        [SerializeField] private UnityEngine.UI.Text weaponPanelLabel;
        [SerializeField] private Button swapButton;
        [SerializeField] private Button keepButton;
        [SerializeField] private int logLines = 4;

        private readonly List<string> log = new List<string>();
        private Simulation simulation;
        private CombatSettings settings;
        private GameBalance balance;
        private AimMode aim;

        public string PreviewText => previewLabel.text;
        public string LogText => logLabel.text;
        public string GrenadeButtonText => GrenadeLabel.text;
        public string PushButtonText => PushLabel.text;

        /// <summary>밀치기 버튼을 눌렀다. MoveController가 밀치기 조준 모드를 켜고 끈다 (GameRoot가 잇는다).</summary>
        public event System.Action PushButtonPressed;

        /// <summary>수류탄 버튼을 눌렀다. MoveController가 조준 모드를 켜고 끈다 (GameRoot가 잇는다).</summary>
        public event System.Action GrenadeButtonPressed;

        /// <summary>결과 화면의 "새 판" 버튼을 눌렀다 (GameRoot가 새 시드로 판을 다시 시작한다).</summary>
        public event System.Action NewRunPressed;

        public string ResultText => resultLabel.text;

        /// <summary>비교 창의 "바꾸기"를 눌렀다 (발밑 무기 ID). GameRoot가 SwapWeaponCommand를 낸다.</summary>
        public event System.Action<int> SwapPressed;

        /// <summary>비교 창이 떠 있으면 그 무기 ID, 아니면 -1.</summary>
        public int OfferedItemId { get; private set; } = -1;

        public bool IsWeaponOfferOpen => OfferedItemId >= 0;
        public string WeaponText => weaponLabel.text;
        public string WeaponPanelText => weaponPanelLabel.text;

        private UnitKind? killerKind;

        /// <summary>감시 버튼을 눌렀다 (GameRoot가 MoveController.RequestOverwatch로 잇는다).</summary>
        public event System.Action OverwatchButtonPressed;

        private UnityEngine.UI.Text GrenadeLabel => grenadeButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
        private UnityEngine.UI.Text PushLabel => pushButton.GetComponentInChildren<UnityEngine.UI.Text>(true);

        /// <summary>조준 모드 표시. 그 스킬 버튼 글이 "조준 취소"로 바뀐다.</summary>
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

        public void Bind(Simulation newSimulation, CombatSettings newSettings, GameBalance newBalance)
        {
            Unbind();
            simulation = newSimulation;
            settings = newSettings;
            balance = newBalance;
            log.Clear();
            logLabel.text = string.Empty;
            killerKind = null;
            simulation.EventsPublished += HandleEvents;
            CloseWeaponOffer();
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
            if (endTurnButton != null) endTurnButton.onClick.AddListener(() => EndTurn());
            if (grenadeButton != null) grenadeButton.onClick.AddListener(() => GrenadeButtonPressed?.Invoke());
            if (pushButton != null) pushButton.onClick.AddListener(() => PushButtonPressed?.Invoke());
            if (overwatchButton != null) overwatchButton.onClick.AddListener(() => OverwatchButtonPressed?.Invoke());
            if (newRunButton != null) newRunButton.onClick.AddListener(() => NewRunPressed?.Invoke());
            if (swapButton != null) swapButton.onClick.AddListener(() => PressSwap());
            if (keepButton != null) keepButton.onClick.AddListener(CloseWeaponOffer);
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
            if (player != null)
            {
                hpLabel.text = Text.Get("ui.hud.hp", player.Hp, player.MaxHp);
                stabilityLabel.text = Text.Get("ui.hud.stability", player.Stability, player.MaxStability);
                // 표시 값 = 실제 값: 무기 표가 아니라 유닛에 적용된 값(Loadout.Apply 결과)을 읽는다.
                weaponLabel.text = Text.Get("ui.hud.weapon", WeaponName(world.PlayerWeapon), player.Attack, player.AttackRange);
            }

            floorLabel.text = Text.Get("ui.hud.floor", world.FloorIndex + 1, settings.FloorCount);

            // 결과 화면: 패배 · 승리 제목, 도달한 층 · 쓰러뜨린 적 · 시드, 쓰러뜨린 상대, "새 판" 버튼.
            bool defeated = world.Mode == WorldMode.Defeat;
            bool won = world.Mode == WorldMode.Victory;
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
            bool playerTurn = TurnFlow.IsPlayerTurn(world);
            roundLabel.gameObject.SetActive(inCombat);
            turnLabel.gameObject.SetActive(inCombat);
            endTurnButton.gameObject.SetActive(playerTurn);
            grenadeButton.gameObject.SetActive(playerTurn && !world.PlayerActed && aim != AimMode.Push);
            pushButton.gameObject.SetActive(playerTurn && !world.PlayerActed && aim != AimMode.Grenade);
            overwatchButton.gameObject.SetActive(playerTurn && !world.PlayerActed && aim == AimMode.None);
            if (playerTurn)
            {
                GrenadeLabel.text = aim == AimMode.Grenade ? Text.Get("ui.skill.grenade_aiming")
                    : world.GrenadeCooldown > 0 ? Text.Get("ui.skill.grenade_cooldown", world.GrenadeCooldown)
                    : Text.Get("ui.skill.grenade");
                PushLabel.text = aim == AimMode.Push ? Text.Get("ui.skill.push_aiming")
                    : world.PushCooldown > 0 ? Text.Get("ui.skill.push_cooldown", world.PushCooldown)
                    : Text.Get("ui.skill.push");
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
