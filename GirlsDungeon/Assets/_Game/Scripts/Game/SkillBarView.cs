using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 화면 오른쪽 아래의 스킬 바 (3-16 배치): 사격(1) · 스킬 칸 2 · 3 · 4(캐릭터마다 다름, 3-12) · 감시(5, 지금은 숨김 — showOverwatch), 그 위 오른쪽에 턴 종료(Z), 왼쪽에 리몰딩 게이지.
    /// 리몰딩을 쓰는 스킬은 카드 아래에 소모 칸을 그린다 (값 · 쓸 수 있는지는 Core.Sim.Remold 한 곳).
    /// 스킬 칸에 무엇이 들어가는지는 규칙의 캐릭터 표(CombatSettings.SkillSlots)에서 읽는다 — 빈 칸은 숨긴다.
    /// 쓸 수 없으면 어둡게, 재사용 대기는 덮개와 남은 라운드, 마우스를 올리면 설명 창 (수치는 규칙 · 무기 값에서 읽는다).
    /// 적 차례 재생 중(EventPlayer.IsPlaying)에는 누를 수 없다. 누르면 MoveController · 커맨드로만 잇는다.
    /// </summary>
    public sealed class SkillBarView : MonoBehaviour
    {
        [Tooltip("Shown only in combat.")]
        [SerializeField] private GameObject barRoot;
        [SerializeField] private SkillCardView shootCard;
        [Tooltip("Skill slot 2 (what it holds depends on the character).")]
        [SerializeField] private SkillCardView slotCard2;
        [Tooltip("Skill slot 3.")]
        [SerializeField] private SkillCardView slotCard3;
        [Tooltip("Skill slot 4.")]
        [SerializeField] private SkillCardView slotCard4;
        [SerializeField] private SkillCardView overwatchCard;
        [Tooltip("Show the overwatch card and its 5 key. Off for now (2026-11-14 user: take overwatch out of the bar).")]
        [SerializeField] private bool showOverwatch;
        [SerializeField] private SkillCardView endTurnCard;
        [Tooltip("Remold gauge (shown only for characters with Remold).")]
        [SerializeField] private GameObject remoldGauge;
        [SerializeField] private UnityEngine.UI.Text remoldLabel;
        [SerializeField] private UnityEngine.UI.Image[] remoldPips = new UnityEngine.UI.Image[0];
        [SerializeField] private Color remoldFilledColor = new Color(0.98f, 0.55f, 0.2f, 1f);
        [SerializeField] private Color remoldEmptyColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private UnityEngine.UI.Text tooltipLabel;
        [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        [SerializeField] private Color selectedColor = new Color(0.2f, 0.45f, 0.8f, 0.95f);
        [SerializeField] private Color disabledColor = new Color(0.1f, 0.1f, 0.12f, 0.8f);

        private Simulation simulation;
        private EventPlayer eventPlayer;
        private MoveController moveController;
        private PlayerView playerView;
        private CombatSettings settings;
        private GameBalance balance;
        private SkillCardView hovered;

        public bool IsShown => barRoot.activeSelf;
        public bool TooltipShown => tooltipPanel.activeSelf;
        public string TooltipText => tooltipLabel.text;
        public SkillCardView ShootCard => shootCard;
        public SkillCardView OverwatchCard => overwatchCard;
        public bool OverwatchShown => showOverwatch;
        public SkillCardView EndTurnCard => endTurnCard;
        public bool RemoldGaugeShown => remoldGauge != null && remoldGauge.activeSelf;
        public string RemoldText => remoldLabel != null ? remoldLabel.text : string.Empty;

        /// <summary>게이지에서 켜진(채워진) 칸 수 — 확인용.</summary>
        public int RemoldPipsFilled
        {
            get
            {
                int filled = 0;
                foreach (var pip in remoldPips) if (pip.gameObject.activeSelf && pip.color == remoldFilledColor) filled++;
                return filled;
            }
        }

        /// <summary>스킬 칸 카드 (0 = 2번 칸).</summary>
        public SkillCardView SlotCard(int index) => index == 0 ? slotCard2 : index == 1 ? slotCard3 : index == 2 ? slotCard4 : null;

        /// <summary>그 스킬 칸에 든 스킬 (지금 캐릭터 기준, 0 = 2번 칸). 없으면 None.</summary>
        public SkillKind SlotSkill(int index)
        {
            if (simulation == null || settings == null) return SkillKind.None;
            var slots = settings.SkillSlots(simulation.World.PlayerCharacter);
            return index >= 0 && index < slots.Count ? slots[index] : SkillKind.None;
        }

        public void Bind(Simulation newSimulation, EventPlayer newEventPlayer, MoveController newMoveController, PlayerView newPlayerView,
            CombatSettings newSettings, GameBalance newBalance)
        {
            simulation = newSimulation;
            eventPlayer = newEventPlayer;
            moveController = newMoveController;
            playerView = newPlayerView;
            settings = newSettings;
            balance = newBalance;
            hovered = null;
            tooltipPanel.SetActive(false);
        }

        private void Awake()
        {
            shootCard.Button.onClick.AddListener(() => PressShoot());
            slotCard2.Button.onClick.AddListener(() => PressSlot(0));
            slotCard3.Button.onClick.AddListener(() => PressSlot(1));
            slotCard4.Button.onClick.AddListener(() => PressSlot(2));
            overwatchCard.Button.onClick.AddListener(() => PressOverwatch());
            endTurnCard.Button.onClick.AddListener(() => EndTurn());
            foreach (var card in new[] { shootCard, slotCard2, slotCard3, slotCard4, overwatchCard, endTurnCard })
            {
                card.PointerEntered += c => hovered = c;
                card.PointerExited += c => { if (hovered == c) hovered = null; };
            }
            shootCard.SetKey("1");
            slotCard2.SetKey("2");
            slotCard3.SetKey("3");
            slotCard4.SetKey("4");
            overwatchCard.SetKey("5");
        }

        /// <summary>내 차례이고 화면 재생이 끝나 입력을 받을 수 있다.</summary>
        public bool Ready => simulation != null && (TurnFlow.IsPlayerTurn(simulation.World) || simulation.World.Mode == WorldMode.Exploration)
            && !eventPlayer.IsPlaying && !playerView.IsMoving;

        /// <summary>
        /// 사격 카드 (1): 대상을 골라 두었으면 그 대상을 쏜다 (확인). 아니면 조준 중인 스킬을 끄고 적 클릭 = 대상 고르기 상태로 돌아간다.
        /// </summary>
        public bool PressShoot()
        {
            if (!Ready) return false;
            if (moveController.SelectedTargetId >= 0) return moveController.ConfirmAttack();
            moveController.CancelAim();
            return true;
        }

        /// <summary>스킬 칸 카드 (2 · 3 · 4): 그 칸 스킬의 조준 모드를 켜고 끈다. 빈 칸 · 재사용 대기면 false.</summary>
        public bool PressSlot(int index)
        {
            var skill = SlotSkill(index);
            if (!Ready || skill == SkillKind.None || !Remold.IsReady(simulation.World, settings, skill)) return false;
            switch (skill)
            {
                case SkillKind.Grenade: moveController.ToggleAim(); break;
                case SkillKind.Push: moveController.TogglePushAim(); break;
                case SkillKind.Sonic: moveController.ToggleSonicAim(); break;
                case SkillKind.Frost: moveController.ToggleFrostAim(); break;
                case SkillKind.FrostGuard: moveController.ToggleFrostGuardAim(); break;
            }
            return true;
        }

        public bool PressOverwatch() => showOverwatch && Ready && moveController.RequestOverwatch();

        /// <summary>턴 종료 카드 (Z). 지금 내 차례가 아니면 false.</summary>
        public bool EndTurn()
        {
            if (!Ready) return false;
            moveController.CancelAim();
            return simulation.Execute(new EndTurnCommand(settings));
        }

        private static AimMode AimOf(SkillKind skill)
        {
            switch (skill)
            {
                case SkillKind.Grenade: return AimMode.Grenade;
                case SkillKind.Push: return AimMode.Push;
                case SkillKind.Sonic: return AimMode.Sonic;
                case SkillKind.Frost: return AimMode.Frost;
                case SkillKind.FrostGuard: return AimMode.FrostGuard;
                default: return AimMode.None;
            }
        }

        private static string NameKey(SkillKind skill)
        {
            switch (skill)
            {
                case SkillKind.Grenade: return "ui.skill.grenade";
                case SkillKind.Push: return "ui.skill.push";
                case SkillKind.Sonic: return "ui.skill.sonic";
                case SkillKind.Frost: return "ui.skill.frost";
                case SkillKind.FrostGuard: return "ui.skill.frost_guard";
                default: return null;
            }
        }

        private void Update()
        {
            if (simulation == null) return;
            var world = simulation.World;
            // 탐험 중에도 보인다 — 스킬을 탐험 중에도 쓸 수 있다 (2026-11-14 사용자). 사격 · 감시 · 턴 종료는 전투 중에만 켜진다.
            bool inCombat = world.Mode == WorldMode.Combat;
            bool shown = inCombat || world.Mode == WorldMode.Exploration;
            barRoot.SetActive(shown);
            if (!shown)
            {
                tooltipPanel.SetActive(false);
                hovered = null;
                return;
            }

            bool ready = Ready;
            var aim = moveController.Aim;
            bool acted = world.PlayerActed;
            shootCard.SetName(Text.Get("ui.skill.shoot"));
            overwatchCard.SetName(Text.Get("ui.skill.overwatch"));
            endTurnCard.SetName(Text.Get("ui.combat.end_turn"));
            endTurnCard.SetKey(Text.Get("ui.key.z"));

            shootCard.SetState(ready && Loadout.CanShoot(world, settings), ready && aim == AimMode.None && Loadout.CanShoot(world, settings), 0,
                normalColor, selectedColor, disabledColor);
            for (int i = 0; i < 3; i++)
            {
                var card = SlotCard(i);
                var skill = SlotSkill(i);
                card.gameObject.SetActive(skill != SkillKind.None);
                if (skill == SkillKind.None)
                {
                    if (hovered == card) hovered = null;
                    continue;
                }
                int cooldown = Remold.Cooldown(world, settings, skill);
                bool skillReady = Remold.IsReady(world, settings, skill);
                card.SetName(Text.Get(NameKey(skill)));
                card.SetState(ready && !acted && skillReady, aim == AimOf(skill), cooldown, normalColor, selectedColor, disabledColor);
                card.SetCost(Remold.Cost(world, settings, skill), skillReady, remoldFilledColor, remoldEmptyColor);
            }
            RefreshRemoldGauge(world);
            // 감시 카드는 지금 바에서 뺐다 (2026-11-14 사용자 "감시 스킬 일단 빼줘") — 규칙(OverwatchCommand)은 그대로.
            overwatchCard.gameObject.SetActive(showOverwatch);
            if (!showOverwatch && hovered == overwatchCard) hovered = null;
            overwatchCard.SetState(ready && inCombat && !acted, false, 0, normalColor, selectedColor, disabledColor);
            endTurnCard.SetState(ready && inCombat, false, 0, normalColor, selectedColor, disabledColor);

            ReadHotkeys();
            RefreshTooltip();
        }

        /// <summary>리몰딩 게이지: 최대치만큼 칸, 지금 지수만큼 채움. 리몰딩이 없는 캐릭터는 숨긴다.</summary>
        private void RefreshRemoldGauge(World world)
        {
            if (remoldGauge == null) return;
            int max = Remold.Max(world, settings);
            remoldGauge.SetActive(max > 0);
            if (max <= 0) return;
            remoldLabel.text = Text.Get("ui.remold.gauge", world.PlayerRemold, max);
            for (int i = 0; i < remoldPips.Length; i++)
            {
                remoldPips[i].gameObject.SetActive(i < max);
                remoldPips[i].color = i < world.PlayerRemold ? remoldFilledColor : remoldEmptyColor;
            }
        }

        private void ReadHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Ready) return;
            if (keyboard.digit1Key.wasPressedThisFrame) PressShoot();
            else if (keyboard.digit2Key.wasPressedThisFrame) PressSlot(0);
            else if (keyboard.digit3Key.wasPressedThisFrame) PressSlot(1);
            else if (keyboard.digit4Key.wasPressedThisFrame) PressSlot(2);
            else if (showOverwatch && keyboard.digit5Key.wasPressedThisFrame) PressOverwatch();
            else if (keyboard.zKey.wasPressedThisFrame) EndTurn();
        }

        /// <summary>디버그 · 확인용: 카드에 마우스를 올린 것과 같다 (null이면 내림).</summary>
        public void Hover(SkillCardView card)
        {
            hovered = card;
            RefreshTooltip();
        }

        private void RefreshTooltip()
        {
            tooltipPanel.SetActive(hovered != null);
            if (hovered == null) return;
            tooltipLabel.text = TooltipFor(hovered);
        }

        /// <summary>설명 창의 값 줄: 리몰딩 소모 또는 재사용 대기.</summary>
        private string CostText(SkillKind skill, int cooldownRounds)
        {
            int cost = Remold.Cost(simulation.World, settings, skill);
            return cost > 0 ? Text.Get("ui.skill.cost_remold", cost) : Text.Get("ui.skill.cost_cooldown", cooldownRounds);
        }

        /// <summary>설명 글. 수치는 지금 유닛 · 무기 · 수치 표에서 읽는다 (표시 값 = 실제 값).</summary>
        private string TooltipFor(SkillCardView card)
        {
            var world = simulation.World;
            var player = world.Player;
            if (card == shootCard)
            {
                string weaponKey = balance.WeaponNameKey(world.PlayerWeapon);
                string ruleKey = balance.WeaponRuleKey(world.PlayerWeapon);
                return Text.Get("ui.skill.shoot_tip", weaponKey != null ? Text.Get(weaponKey) : world.PlayerWeapon.ToString(),
                    player.Attack, player.AttackRange, ruleKey != null ? Text.Get(ruleKey) : string.Empty, Loadout.ShotsPerTurn(world, settings));
            }
            if (card == overwatchCard)
                return Text.Get("ui.skill.overwatch_tip", player.AttackRange);
            if (card == endTurnCard)
                return Text.Get("ui.skill.end_turn_tip");

            var skill = card == slotCard2 ? SlotSkill(0) : card == slotCard3 ? SlotSkill(1) : SlotSkill(2);
            switch (skill)
            {
                case SkillKind.Grenade:
                    return Text.Get("ui.skill.grenade_tip", settings.GrenadeThrowRange, settings.GrenadeRadiusFor(simulation.World.PlayerCharacter), settings.SkillDamagePercentFor(simulation.World.PlayerCharacter, SkillKind.Grenade),
                        settings.GrenadeStabilityDamage, CostText(skill, settings.GrenadeCooldownRounds));
                case SkillKind.Push:
                    return Text.Get("ui.skill.push_tip", settings.PushWidthFor(simulation.World.PlayerCharacter), settings.PushLengthFor(simulation.World.PlayerCharacter), settings.SkillDamagePercentFor(simulation.World.PlayerCharacter, SkillKind.Push),
                        settings.PushCollisionMaxHpPercent > 0 ? settings.PushCollisionMaxHpPercent : settings.PushCollisionDamage, CostText(skill, settings.PushCooldownRounds));
                case SkillKind.Sonic:
                    return Text.Get("ui.skill.sonic_tip", settings.SonicRange, settings.SkillDamagePercentFor(simulation.World.PlayerCharacter, SkillKind.Sonic),
                        settings.SonicStabilityDamage, CostText(skill, settings.SonicCooldownRounds));
                case SkillKind.Frost:
                    return Text.Get("ui.skill.frost_tip", settings.FrostRange, settings.SkillDamagePercentFor(simulation.World.PlayerCharacter, SkillKind.Frost),
                        settings.FrostFixedDefensePercent, FrostAttack.FixedDamage(simulation.World.Player.Defense, settings.FrostFixedDefensePercent),
                        settings.FrostStabilityDamage, CostText(skill, settings.FrostCooldownRounds));
                case SkillKind.FrostGuard:
                    return Text.Get("ui.skill.frost_guard_tip", settings.FrostGuardShieldPercent, FrostGuard.ShieldAmount(simulation.World, settings), settings.FrostGuardShieldTurns,
                        settings.FrostGuardStabilityRestore, settings.FrostGuardRange, settings.DefenseSupportPercent, settings.DefenseSupportMaxStacks, settings.DefenseSupportTurns,
                        settings.FreezeTurns, CostText(skill, 0));
                default:
                    return string.Empty;
            }
        }
    }
}
