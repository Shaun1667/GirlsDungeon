using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>사격을 할 수 있는지와 그 이유.</summary>
    public enum AttackBlock
    {
        None = 0,
        OutOfRange = 1,
        NoLineOfFire = 2,
    }

    /// <summary>사격 미리보기. 실제 사격도 같은 값을 쓴다 (설계 규칙 6).</summary>
    public readonly struct AttackPreview
    {
        public readonly AttackBlock Block;
        public readonly CoverLevel Cover;
        public readonly bool StabilityGuard;
        public readonly int ReductionPercent;
        public readonly int Damage;
        public readonly int CritDamage;
        public readonly int CritPercent;
        public readonly int StabilityDamage;

        /// <summary>이 사격으로 대상의 상자 · 낮은 벽이 부서진다.</summary>
        public readonly bool BreaksCover;

        public bool CanAttack => Block == AttackBlock.None;

        public AttackPreview(AttackBlock block, CoverLevel cover, bool stabilityGuard, int reductionPercent,
            int damage, int critDamage, int critPercent, int stabilityDamage, bool breaksCover)
        {
            BreaksCover = breaksCover;
            Block = block;
            Cover = cover;
            StabilityGuard = stabilityGuard;
            ReductionPercent = reductionPercent;
            Damage = damage;
            CritDamage = critDamage;
            CritPercent = critPercent;
            StabilityDamage = stabilityDamage;
        }
    }

    /// <summary>
    /// 사격 · 피해의 한 곳 (BALANCE 5번). 공격은 항상 맞는다.
    /// 피해 = 공격력 × 공격력 ÷ (공격력 + 방어력) × 치명 배율 × (1 − 엄폐 감소), 반올림, 최소 MinDamage.
    /// 엄폐 감소 = 엄폐물(낮음 · 높음) + 안정지수가 남아 있으면 StabilityGuard. 엄폐는 공격자 쪽을 향한 것만 (Cover).
    /// 안정지수는 엄폐와 무관하게 공격의 안정 피해만큼 깎인다. 감소 판정은 깎이기 전 안정지수로 한다.
    /// </summary>
    public static class CombatRules
    {
        public static AttackPreview Preview(World world, CombatSettings settings, Unit attacker, GridPos from, Unit target)
        {
            var map = world.Map;
            var block = AttackBlock.None;
            int dx = target.Position.X - from.X;
            int dy = target.Position.Y - from.Y;
            if (dx * dx + dy * dy > attacker.AttackRange * attacker.AttackRange) block = AttackBlock.OutOfRange;
            else if (!Sight.HasLine(map, from, target.Position)) block = AttackBlock.NoLineOfFire;

            var cover = Cover.Against(map, target.Position, from);
            bool guard = cover != CoverLevel.None && target.Stability > 0;
            int reduction = cover == CoverLevel.Full ? settings.FullCoverReductionPercent
                : cover == CoverLevel.Half ? settings.HalfCoverReductionPercent : 0;
            if (guard) reduction += settings.StabilityGuardPercent;

            int damage = Damage(attacker.Attack, target.Defense, 100, reduction, settings.MinDamage);
            int critDamage = Damage(attacker.Attack, target.Defense, attacker.CritMultiplierPercent, reduction, settings.MinDamage);
            bool breaksCover = cover != CoverLevel.None
                && CoverBreak.AnyBreaksOnNextHit(map, settings, CoverBreak.Facing(map, target.Position, from));
            return new AttackPreview(block, cover, guard, reduction, damage, critDamage, attacker.CritPercent, attacker.StabilityDamage, breaksCover);
        }

        /// <summary>정수 계산 · 반올림(0.5는 올림). 같은 입력이면 어디서나 같은 값.</summary>
        public static int Damage(int attack, int defense, int multiplierPercent, int reductionPercent, int minDamage)
        {
            long numerator = (long)attack * attack * multiplierPercent * (100 - reductionPercent);
            long denominator = (long)(attack + defense) * 100 * 100;
            int value = (int)((2 * numerator + denominator) / (2 * denominator));
            return value < minDamage ? minDamage : value;
        }

        /// <summary>
        /// 사격을 처리한다. 치명타는 전투 난수로 굴린다. 쓰러지면 적은 맵에서 빠지고, 플레이어는 패배 상태가 된다.
        /// 사격할 수 없으면 아무것도 바꾸지 않고 false.
        /// </summary>
        public static bool Resolve(World world, CombatSettings settings, Unit attacker, Unit target, List<GameEvent> events)
        {
            var preview = Preview(world, settings, attacker, attacker.Position, target);
            if (!preview.CanAttack) return false;

            bool crit = world.Random.Get(RandomPurpose.Combat).Range(0, 100) < preview.CritPercent;
            int damage = crit ? preview.CritDamage : preview.Damage;
            int stabilityBefore = target.Stability;

            target.Hp -= damage;
            target.Stability = System.Math.Max(0, target.Stability - preview.StabilityDamage);
            bool stabilityBroken = stabilityBefore > 0 && target.Stability == 0;

            events.Add(new UnitAttacked(attacker, target, damage, crit, preview.Cover, preview.StabilityGuard, stabilityBroken));
            events.Add(new UnitStatsChanged(target.Id));
            // 피해는 이미 깎이기 전 엄폐로 계산했다. 이제 쏘는 쪽을 향한 상자 · 낮은 벽을 깎는다.
            if (preview.Cover != CoverLevel.None)
                CoverBreak.Hit(world, settings, CoverBreak.Facing(world.Map, target.Position, attacker.Position), events);
            Alert.Wake(world, target, events);

            if (target.Hp > 0)
            {
                // 밀기 능력이 있는 공격자(돌격병)는 맞은 상대를 민다 — 낭떠러지 · 충돌 처리는 Push 한 곳.
                Push.Knockback(world, settings, attacker, target, events);
                return true;
            }
            if (target.IsPlayer)
            {
                target.Hp = 0;
                world.Mode = WorldMode.Defeat;
                events.Add(new PlayerDefeated(attacker.Id));
            }
            else
            {
                CombatFlow.Kill(world, target, events);
            }
            return true;
        }
    }

    /// <summary>
    /// 전투 이동의 한 곳. 한 걸음 = 1칸(대각선 포함), 벽 모서리를 대각선으로 뚫지 못하고, 다른 유닛 칸은 지나가지 못한다.
    /// 범위 표시 · 실제 이동 · 적 AI가 같은 계산을 쓴다.
    /// </summary>
    public static class CombatMovement
    {
        private static readonly GridPos[] Steps =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
            new GridPos(1, 1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(-1, -1),
        };

        /// <summary>
        /// 갈 수 있는 칸 → 걸음 수. 시작 칸(0걸음)도 들어 있다. allowed가 있으면 그 칸만 지난다 (예: 플레이어는 본 칸만).
        /// </summary>
        public static Dictionary<GridPos, int> Reachable(World world, Unit unit, int maxSteps, bool[] allowed = null)
        {
            var distance = new Dictionary<GridPos, int> { [unit.Position] = 0 };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(unit.Position);
            var map = world.Map;

            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                int steps = distance[cell];
                if (steps >= maxSteps) continue;

                foreach (var step in Steps)
                {
                    var next = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    if (distance.ContainsKey(next)) continue;
                    if (!GridNavigation.CanStep(map, cell, next) || world.IsOccupied(next)) continue;
                    if (allowed != null && !Vision.IsSet(allowed, map, next)) continue;
                    distance[next] = steps + 1;
                    frontier.Enqueue(next);
                }
            }
            return distance;
        }

        /// <summary>목표 칸까지 걸음 목록 (시작 칸 제외). maxSteps 안에 갈 수 없으면 빈 목록.</summary>
        public static List<GridPos> Path(World world, Unit unit, GridPos target, int maxSteps, bool[] allowed = null)
        {
            var path = new List<GridPos>();
            var reachable = Reachable(world, unit, maxSteps, allowed);
            if (target == unit.Position || !reachable.TryGetValue(target, out int remaining)) return path;

            // 목표에서 거꾸로 한 걸음씩 줄여 가며 시작 칸까지 잇는다 (이웃 순서가 고정이라 결과가 항상 같다).
            var cell = target;
            path.Add(cell);
            while (remaining > 1)
            {
                bool found = false;
                foreach (var step in Steps)
                {
                    var previous = new GridPos(cell.X - step.X, cell.Y - step.Y);
                    if (!reachable.TryGetValue(previous, out int steps) || steps != remaining - 1) continue;
                    if (!GridNavigation.CanStep(world.Map, previous, cell)) continue;
                    cell = previous;
                    remaining = steps;
                    path.Add(cell);
                    found = true;
                    break;
                }
                if (!found) return new List<GridPos>();
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// 유닛을 걸음마다 옮기며 UnitMoved를 낸다 (화면이 칸 사이를 차례로 그린다).
        /// settings가 있으면 걸음마다 상대 감시(Alert.React)를 확인하고, 쓰러지면 멈추고 false.
        /// </summary>
        public static bool Walk(World world, CombatSettings settings, Unit unit, List<GridPos> path, List<GameEvent> events)
        {
            foreach (var cell in path)
            {
                var from = unit.Position;
                unit.Position = cell;
                events.Add(new UnitMoved(unit.Id, from, cell));
                if (settings != null && !Alert.React(world, settings, unit, events)) return false;
            }
            return true;
        }
    }
}
