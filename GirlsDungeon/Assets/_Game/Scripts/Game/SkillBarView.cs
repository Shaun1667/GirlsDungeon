using GirlsDungeon.Core.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GirlsDungeon.Game
{
    /// <summary>
    /// 전투 중 화면 아래 가운데의 스킬 바: 사격(1) · 수류탄(2) · 밀치기(3) · 감시(4) · 턴 종료(Space).
    /// 쓸 수 없으면 어둡게, 재사용 대기는 덮개와 남은 라운드, 마우스를 올리면 설명 창 (수치는 규칙 · 무기 값에서 읽는다).
    /// 적 차례 재생 중(EventPlayer.IsPlaying)에는 누를 수 없다. 누르면 MoveController · 커맨드로만 잇는다.
    /// </summary>
    public sealed class SkillBarView : MonoBehaviour
    {
        [Tooltip("Shown only in combat.")]
        [SerializeField] private GameObject barRoot;
        [SerializeField] private SkillCardView shootCard;
        [SerializeField] private SkillCardView grenadeCard;
        [SerializeField] private SkillCardView pushCard;
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
        public SkillCardView GrenadeCard => grenadeCard;
        public SkillCardView PushCard => pushCard;
        public SkillCardView OverwatchCard => overwatchCard;
        public SkillCardView EndTurnCard => endTurnCard;

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
            grenadeCard.Button.onClick.AddListener(() => PressGrenade());
            pushCard.Button.onClick.AddListener(() => PressPush());
            overwatchCard.Button.onClick.AddListener(() => PressOverwatch());
            endTurnCard.Button.onClick.AddListener(() => EndTurn());
            foreach (var card in new[] { shootCard, grenadeCard, pushCard, overwatchCard, endTurnCard })
            {
                card.PointerEntered += c => hovered = c;
                card.PointerExited += c => { if (hovered == c) hovered = null; };
            }
            shootCard.SetKey("1");
            grenadeCard.SetKey("2");
            pushCard.SetKey("3");
            overwatchCard.SetKey("4");
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

        public bool PressGrenade()
        {
            if (!Ready || simulation.World.GrenadeCooldown > 0) return false;
            moveController.ToggleAim();
            return true;
        }

        public bool PressPush()
        {
            if (!Ready || simulation.World.PushCooldown > 0) return false;
            moveController.TogglePushAim();
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
            grenadeCard.SetName(Text.Get("ui.skill.grenade"));
            pushCard.SetName(Text.Get("ui.skill.push"));
            overwatchCard.SetName(Text.Get("ui.skill.overwatch"));
            endTurnCard.SetName(Text.Get("ui.combat.end_turn"));
            endTurnCard.SetKey(Text.Get("ui.key.space"));

            shootCard.SetState(ready && Loadout.CanShoot(world, settings), ready && aim == AimMode.None && Loadout.CanShoot(world, settings), 0,
                normalColor, selectedColor, disabledColor);
            grenadeCard.SetState(ready && !acted && world.GrenadeCooldown == 0, aim == AimMode.Grenade, world.GrenadeCooldown,
                normalColor, selectedColor, disabledColor);
            pushCard.SetState(ready && !acted && world.PushCooldown == 0, aim == AimMode.Push, world.PushCooldown,
                normalColor, selectedColor, disabledColor);
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
            else if (keyboard.digit2Key.wasPressedThisFrame) PressGrenade();
            else if (keyboard.digit3Key.wasPressedThisFrame) PressPush();
            else if (keyboard.digit4Key.wasPressedThisFrame) PressOverwatch();
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
            if (card == grenadeCard)
                return Text.Get("ui.skill.grenade_tip", settings.GrenadeThrowRange, settings.GrenadeRadius, settings.GrenadeDamagePercent,
                    settings.GrenadeStabilityDamage, settings.GrenadeCooldownRounds);
            if (card == pushCard)
                return Text.Get("ui.skill.push_tip", settings.PushWidth, settings.PushLength, settings.PushDamagePercent,
                    settings.PushCollisionDamage, settings.PushCooldownRounds);
            if (card == overwatchCard)
                return Text.Get("ui.skill.overwatch_tip", player.AttackRange);
            return Text.Get("ui.skill.end_turn_tip");
        }
    }
}
