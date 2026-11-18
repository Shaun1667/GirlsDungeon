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

        /// <summary>
        /// 규칙: 거리별 피해 (0 = 안 씀, 산탄총 — 2026-11-17 사용자). 1칸 CloseRangePercent → FarRangeCells칸 FarRangePercent로 고르게,
        /// 더 멀면 FarRangePercent. 쓰면 FalloffStartCells · FalloffPercentPerCell 대신 이것.
        /// </summary>
        public int CloseRangePercent;
        public int FarRangePercent;
        public int FarRangeCells;

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
            // 무기 값 + 든 무기에 끼운 파츠 합 (3-4).
            int attackBase = weapon.Attack + Parts.Sum(world, PartStat.Attack);
            player.Attack = attackBase + Percent(attackBase, Parts.Sum(world, PartStat.AttackPercent));
            player.AttackRange = weapon.AttackRange + Parts.Sum(world, PartStat.AttackRange);
            player.StabilityDamage = weapon.StabilityDamage + Parts.Sum(world, PartStat.StabilityDamage);
            player.CritPercent = System.Math.Min(100, weapon.CritPercent + Parts.Sum(world, PartStat.CritPercent));
            player.CritMultiplierPercent = weapon.CritMultiplierPercent + Parts.Sum(world, PartStat.CritMultiplier);

            // 몸 능력치는 파츠 합이 바뀐 만큼만 더하고 뺀다 (캐릭터 · 유닛 표에서 정한 값은 그대로 두고). 최대 체력이 늘면 지금 체력도 같이 는다.
            // 퍼센트(3-16n)는 파츠를 빼고 본 기본값에 곱한다 — 모든 파츠의 %를 더한 뒤 한 번 반올림.
            int hpBonus = Parts.Sum(world, PartStat.MaxHp) + Percent(player.MaxHp - world.PartAppliedMaxHp, Parts.Sum(world, PartStat.MaxHpPercent));
            int hpDelta = hpBonus - world.PartAppliedMaxHp;
            player.MaxHp += hpDelta;
            if (hpDelta > 0) player.Hp += hpDelta;
            if (player.Hp > player.MaxHp) player.Hp = player.MaxHp;
            world.PartAppliedMaxHp = hpBonus;

            int defenseBonus = Parts.Sum(world, PartStat.Defense) + Percent(player.Defense - world.PartAppliedDefense, Parts.Sum(world, PartStat.DefensePercent));
            player.Defense += defenseBonus - world.PartAppliedDefense;
            world.PartAppliedDefense = defenseBonus;

            int stabilityBonus = Parts.Sum(world, PartStat.MaxStability);
            int stabilityDelta = stabilityBonus - world.PartAppliedMaxStability;
            player.MaxStability += stabilityDelta;
            if (stabilityDelta > 0) player.Stability += stabilityDelta;
            if (player.Stability > player.MaxStability) player.Stability = player.MaxStability;
            world.PartAppliedMaxStability = stabilityBonus;
            // 이동 범위 = 캐릭터 값(없으면 유닛 표 값) + 무기 보정 (기관총 −2).
            player.MoveRange = System.Math.Max(0, settings.PlayerBaseMoveRange(world.PlayerCharacter) + weapon.MoveRangeBonus + Parts.Sum(world, PartStat.MoveRange));
        }

        /// <summary>value의 percent% (반올림, 3-16n 파츠 퍼센트 능력치).</summary>
        public static int Percent(int value, int percent)
        {
            if (percent == 0) return 0;
            long product = (long)value * percent;
            return (int)(product >= 0 ? (product + 50) / 100 : -((-product + 50) / 100));
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

        /// <summary>이번 차례에 플레이어가 더 걸을 수 있는 칸 수: 이동 범위 − 이미 걸은 칸 (0 이상). 범위 표시 · 이동 커맨드가 같이 쓴다.</summary>
        public static int MoveLeft(World world)
        {
            var player = world.Player;
            return player == null ? 0 : System.Math.Max(0, player.MoveRange - world.PlayerMoveUsed);
        }

        /// <summary>
        /// 전투 중 플레이어가 지금 이동할 수 있는지: 내 차례이고 이동 칸이 남았고,
        /// 행동(사격)을 아직 안 했거나, 든 무기가 쏜 뒤 이동을 허락하거나(권총), 아직 더 쏠 수 있을 때(기관단총).
        /// 행동 전에는 남은 칸만큼 여러 번 나눠 걸을 수 있다 (3-13).
        /// </summary>
        public static bool CanCombatMove(World world, CombatSettings settings)
        {
            if (!TurnFlow.IsPlayerTurn(world) || MoveLeft(world) <= 0) return false;
            if (!world.PlayerActed) return true;
            var weapon = WeaponOf(world, settings, world.Player);
            if (weapon == null || world.PlayerShots == 0) return false;
            return weapon.MoveAfterShot || world.PlayerShots < weapon.ShotsPerTurn;
        }

        /// <summary>사격 뒤 차례가 이어지는지: 아직 더 쏠 수 있거나(기관단총), 권총이고 이동 칸이 남았을 때 (3-13).</summary>
        public static bool KeepsTurnAfterShot(World world, CombatSettings settings)
        {
            var weapon = WeaponOf(world, settings, world.Player);
            if (weapon == null) return false;
            return world.PlayerShots < weapon.ShotsPerTurn || (weapon.MoveAfterShot && MoveLeft(world) > 0);
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

        /// <summary>
        /// 산탄총 규칙: 거리에 따라 줄어드는 피해 (%). 피해 = 100 − 이 값 (%). 거리별 피해(FarRangeCells)를 쓰면 가까울 때 음수 = 늘어남
        /// (1칸 150% → −50). 미리보기와 실제가 모두 이것을 쓴다.
        /// </summary>
        public static int FalloffPercent(WeaponStats weapon, GridPos from, GridPos target)
        {
            if (weapon == null) return 0;
            if (weapon.FarRangeCells > 1) return 100 - DistancePercent(weapon, CellDistance(from, target));
            if (weapon.FalloffPercentPerCell <= 0) return 0;
            int over = CellDistance(from, target) - weapon.FalloffStartCells;
            if (over <= 0) return 0;
            return System.Math.Min(100, over * weapon.FalloffPercentPerCell);
        }

        /// <summary>거리별 피해 (%): 1칸 CloseRangePercent → FarRangeCells칸 FarRangePercent 사이를 고르게 (반올림), 그 밖은 끝 값.</summary>
        public static int DistancePercent(WeaponStats weapon, int distance)
        {
            int far = weapon.FarRangeCells;
            int d = System.Math.Max(1, System.Math.Min(far, distance));
            long numerator = (long)weapon.CloseRangePercent * (far - d) + (long)weapon.FarRangePercent * (d - 1);
            long denominator = far - 1;
            return (int)((2 * numerator + denominator) / (2 * denominator));
        }
    }
}
