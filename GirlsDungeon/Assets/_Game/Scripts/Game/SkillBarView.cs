using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 전투 중 화면 아래 가운데의 스킬 바: 사격(1) · 스킬 칸 2 · 3 · 4(캐릭터마다 다름, 3-12) · 감시(5) · 턴 종료(Space).
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
        [SerializeField] private SkillCardView endTurnCard;
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
        public SkillCardView EndTurnCard => endTurnCard;

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
        public bool Ready => simulation != null && TurnFlow.IsPlayerTurn(simulation.World) && !eventPlayer.IsPlaying && !playerView.IsMoving;

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
            if (!Ready || skill == SkillKind.None || Cooldown(skill) > 0) return false;
            switch (skill)
            {
                case SkillKind.Grenade: moveController.ToggleAim(); break;
                case SkillKind.Push: moveController.TogglePushAim(); break;
                case SkillKind.Sonic: moveController.ToggleSonicAim(); break;
            }
            return true;
        }

        public bool PressOverwatch() => Ready && moveController.RequestOverwatch();

        /// <summary>턴 종료 카드 (Space). 지금 내 차례가 아니면 false.</summary>
        public bool EndTurn()
        {
            if (!Ready) return false;
            moveController.CancelAim();
            return simulation.Execute(new EndTurnCommand(settings));
        }

        private int Cooldown(SkillKind skill)
        {
            var world = simulation.World;
            switch (skill)
            {
                case SkillKind.Grenade: return world.GrenadeCooldown;
                case SkillKind.Push: return world.PushCooldown;
                case SkillKind.Sonic: return world.SonicCooldown;
                default: return 0;
            }
        }

        private static AimMode AimOf(SkillKind skill)
        {
            switch (skill)
            {
                case SkillKind.Grenade: return AimMode.Grenade;
                case SkillKind.Push: return AimMode.Push;
                case SkillKind.Sonic: return AimMode.Sonic;
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
                default: return null;
            }
        }

        private void Update()
        {
            if (simulation == null) return;
            var world = simulation.World;
            bool inCombat = world.Mode == WorldMode.Combat;
            barRoot.SetActive(inCombat);
            if (!inCombat)
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
            endTurnCard.SetKey(Text.Get("ui.key.space"));

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
                int cooldown = Cooldown(skill);
                card.SetName(Text.Get(NameKey(skill)));
                card.SetState(ready && !acted && cooldown == 0, aim == AimOf(skill), cooldown, normalColor, selectedColor, disabledColor);
            }
            overwatchCard.SetState(ready && !acted, false, 0, normalColor, selectedColor, disabledColor);
            endTurnCard.SetState(ready, false, 0, normalColor, selectedColor, disabledColor);

            ReadHotkeys();
            RefreshTooltip();
        }

        private void ReadHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Ready) return;
            if (keyboard.digit1Key.wasPressedThisFrame) PressShoot();
            else if (keyboard.digit2Key.wasPressedThisFrame) PressSlot(0);
            else if (keyboard.digit3Key.wasPressedThisFrame) PressSlot(1);
            else if (keyboard.digit4Key.wasPressedThisFrame) PressSlot(2);
            else if (keyboard.digit5Key.wasPressedThisFrame) PressOverwatch();
            else if (keyboard.spaceKey.wasPressedThisFrame) EndTurn();
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
                    return Text.Get("ui.skill.grenade_tip", settings.GrenadeThrowRange, settings.GrenadeRadius, settings.GrenadeDamagePercent,
                        settings.GrenadeStabilityDamage, settings.GrenadeCooldownRounds);
                case SkillKind.Push:
                    return Text.Get("ui.skill.push_tip", settings.PushWidth, settings.PushLength, settings.PushDamagePercent,
                        settings.PushCollisionDamage, settings.PushCooldownRounds);
                case SkillKind.Sonic:
                    return Text.Get("ui.skill.sonic_tip", settings.SonicRange, settings.SonicDamagePercent,
                        settings.SonicStabilityDamage, settings.SonicCooldownRounds);
                default:
                    return string.Empty;
            }
        }
    }
}
