using GirlsDungeon.Core.Grid;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>무기 종류. 저장에 정수로 들어가므로 끝에만 추가한다. 능력치 · 규칙은 CombatSettings.Weapons 표에서 온다.</summary>
    public enum WeaponKind : byte
    {
        /// <summary>돌격소총 — 처음 드는 무기, 특수 규칙 없음.</summary>
        AssaultRifle = 0,
        /// <summary>권총 — 쏜 뒤에도 남은 이동을 쓸 수 있다.</summary>
        Pistol = 1,
        /// <summary>산탄총 — 멀수록 피해가 준다.</summary>
        Shotgun = 2,
        /// <summary>저격총 — 이동한 차례에는 쏘지 못한다.</summary>
        Sniper = 3,
        /// <summary>기관단총 — 한 차례에 두 번 쏜다 (사이에 이동 가능).</summary>
        Smg = 4,
        /// <summary>기관총 — 무거워 이동이 줄어든다.</summary>
        MachineGun = 5,
        /// <summary>도검 — 주변 8칸 근접, 엄폐를 무시한다.</summary>
        Blade = 6,
    }

    /// <summary>
    /// 무기 하나의 능력치와 규칙 (표 한 줄, BALANCE 12번). 값은 GameBalance 에셋에서 받는다.
    /// 플레이어의 공격 · 사거리 · 안정 피해 · 치명은 든 무기의 값이다 (Loadout.Apply 한 곳에서 덮어쓴다).
    /// </summary>
    public sealed class WeaponStats
    {
        public WeaponKind Kind;
        public int Attack;
        public int AttackRange;
        public int StabilityDamage;
        public int CritPercent;
        public int CritMultiplierPercent;

        /// <summary>규칙: 쏜 뒤에도 (아직 이동하지 않았으면) 이동할 수 있다. 이동하면 차례가 끝난다 (권총).</summary>
        public bool MoveAfterShot;

        /// <summary>규칙: 이번 차례에 이동했으면 쏘지 못한다 (저격총). 적 차례의 감시 사격은 막지 않는다.</summary>
        public bool NoShotAfterMove;

        /// <summary>규칙: 이 칸 수보다 멀면 피해가 준다 (0 = 줄지 않음, 산탄총 2). 거리는 가로 · 세로 · 대각 중 큰 값.</summary>
        public int FalloffStartCells;

        /// <summary>FalloffStartCells를 넘는 한 칸마다 줄어드는 피해 (%).</summary>
        public int FalloffPercentPerCell;

        /// <summary>규칙: 한 차례에 더 쏠 수 있는 횟수 (0 = 한 번, 기관단총 1 = 두 번). 사이에 이동할 수 있다.</summary>
        public int ExtraShotsPerTurn;

        /// <summary>규칙: 플레이어 이동 범위에 더하는 값 (기관총 −2).</summary>
        public int MoveRangeBonus;

        /// <summary>규칙: 사거리를 가로 · 세로 · 대각 중 큰 값으로 잰다 — 사거리 1이면 주변 8칸 (도검).</summary>
        public bool Melee;

        /// <summary>규칙: 대상의 엄폐(와 엄폐에 붙는 안정지수 감소)를 무시한다 (도검).</summary>
        public bool IgnoresCover;

        /// <summary>한 차례에 쏠 수 있는 횟수.</summary>
        public int ShotsPerTurn => 1 + ExtraShotsPerTurn;
    }

    /// <summary>
    /// 플레이어 장비 → 능력치의 한 조회 지점 (설계 규칙 7). 무기를 바꾸면 여기서 유닛 능력치를 다시 맞춘다.
    /// 미리보기 · 실제 · HUD가 모두 유닛 값을 읽으므로 표시 값 = 실제 값이 된다. 3-4의 파츠 효과도 여기서 더한다.
    /// </summary>
    public static class Loadout
    {
        public static void Apply(World world, CombatSettings settings)
        {
            var player = world.Player;
            if (player == null) return;
            var weapon = settings.WeaponFor(world.PlayerWeapon);
            player.Attack = weapon.Attack;
            player.AttackRange = weapon.AttackRange;
            player.StabilityDamage = weapon.StabilityDamage;
            player.CritPercent = weapon.CritPercent;
            player.CritMultiplierPercent = weapon.CritMultiplierPercent;
            // 이동 범위 = 캐릭터 값(없으면 유닛 표 값) + 무기 보정 (기관총 −2).
            player.MoveRange = System.Math.Max(0, settings.PlayerBaseMoveRange(world.PlayerCharacter) + weapon.MoveRangeBonus);
        }

        /// <summary>이번 차례에 플레이어가 쏠 수 있는 횟수 (무기 규칙, 수치가 없으면 1).</summary>
        public static int ShotsPerTurn(World world, CombatSettings settings)
        {
            var weapon = WeaponOf(world, settings, world.Player);
            return weapon != null ? weapon.ShotsPerTurn : 1;
        }

        /// <summary>
        /// 전투 중 플레이어가 지금 쏠 수 있는지: 내 차례이고, 이번 차례 사격 수가 무기 한도보다 적고,
        /// 사격이 아닌 행동(스킬 · 감시)을 쓰지 않았을 때. 사거리 · 사선은 CombatRules.Preview가 본다.
        /// </summary>
        public static bool CanShoot(World world, CombatSettings settings)
        {
            if (!TurnFlow.IsPlayerTurn(world)) return false;
            if (world.PlayerActed && world.PlayerShots == 0) return false;
            return world.PlayerShots < ShotsPerTurn(world, settings);
        }

        /// <summary>attacker가 든 무기의 규칙. 적이거나 수치가 없으면 null (규칙 없음).</summary>
        public static WeaponStats WeaponOf(World world, CombatSettings settings, Unit attacker)
        {
            if (settings == null || attacker == null || !attacker.IsPlayer) return null;
            return settings.TryWeapon(world.PlayerWeapon);
        }

        /// <summary>
        /// 전투 중 플레이어가 지금 이동할 수 있는지: 내 차례이고 아직 이동하지 않았고,
        /// 행동(사격)을 아직 안 했거나, 든 무기가 쏜 뒤 이동을 허락하거나(권총), 아직 더 쏠 수 있을 때(기관단총).
        /// </summary>
        public static bool CanCombatMove(World world, CombatSettings settings)
        {
            if (!TurnFlow.IsPlayerTurn(world) || world.PlayerMoved) return false;
            if (!world.PlayerActed) return true;
            var weapon = WeaponOf(world, settings, world.Player);
            if (weapon == null || world.PlayerShots == 0) return false;
            return weapon.MoveAfterShot || world.PlayerShots < weapon.ShotsPerTurn;
        }

        /// <summary>사격 뒤 차례가 이어지는지: 아직 더 쏠 수 있거나(기관단총), 권총이고 아직 이동하지 않았을 때.</summary>
        public static bool KeepsTurnAfterShot(World world, CombatSettings settings)
        {
            var weapon = WeaponOf(world, settings, world.Player);
            if (weapon == null) return false;
            return world.PlayerShots < weapon.ShotsPerTurn || (weapon.MoveAfterShot && !world.PlayerMoved);
        }

        /// <summary>사거리 안인지. 근접 무기(도검)는 가로 · 세로 · 대각 중 큰 값, 나머지는 직선 거리.</summary>
        public static bool InRange(WeaponStats weapon, int range, GridPos from, GridPos target)
        {
            if (weapon != null && weapon.Melee) return CellDistance(from, target) <= range;
            int dx = target.X - from.X;
            int dy = target.Y - from.Y;
            return dx * dx + dy * dy <= range * range;
        }

        /// <summary>두 칸의 거리 (가로 · 세로 · 대각 중 큰 값, 대각선도 1칸).</summary>
        public static int CellDistance(GridPos a, GridPos b)
        {
            return System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Y - b.Y));
        }

        /// <summary>산탄총 규칙: 거리에 따라 줄어드는 피해 (%). 0 ~ 100.</summary>
        public static int FalloffPercent(WeaponStats weapon, GridPos from, GridPos target)
        {
            if (weapon == null || weapon.FalloffPercentPerCell <= 0) return 0;
            int over = CellDistance(from, target) - weapon.FalloffStartCells;
            if (over <= 0) return 0;
            return System.Math.Min(100, over * weapon.FalloffPercentPerCell);
        }
    }
}
