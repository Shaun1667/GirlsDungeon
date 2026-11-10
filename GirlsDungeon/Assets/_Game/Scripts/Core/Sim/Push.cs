using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>밀치기를 쓸 수 있는지와 그 이유.</summary>
    public enum PushBlock
    {
        None = 0,
        NotReady = 1,
        NoTarget = 2,
    }

    /// <summary>밀려서 움직인 결과 한 줄.</summary>
    public readonly struct SlideResult
    {
        /// <summary>지나간 칸 (출발 칸 제외, 떨어지면 마지막이 낭떠러지 칸).</summary>
        public readonly IReadOnlyList<GridPos> Path;
        public readonly GridPos End;
        public readonly bool Falls;
        public readonly bool Collides;

        public SlideResult(IReadOnlyList<GridPos> path, GridPos end, bool falls, bool collides)
        {
            Path = path;
            End = end;
            Falls = falls;
            Collides = collides;
        }
    }

    /// <summary>밀치기 한 대상의 미리보기.</summary>
    public readonly struct PushHit
    {
        public readonly Unit Target;
        public readonly int Damage;
        /// <summary>직접 피해로 쓰러지면 밀리지 않는다.</summary>
        public readonly bool DiesFromHit;
        public readonly SlideResult Slide;
        /// <summary>부딪히면 받는 피해 (안 부딪히면 0).</summary>
        public readonly int CollisionDamage;

        public PushHit(Unit target, int damage, bool diesFromHit, SlideResult slide, int collisionDamage)
        {
            Target = target;
            Damage = damage;
            DiesFromHit = diesFromHit;
            Slide = slide;
            CollisionDamage = collisionDamage;
        }

        /// <summary>떨어지거나 피해로 쓰러지는지.</summary>
        /// <summary>미리보기 시점의 체력 기준 (실제 처리 전에만 쓴다).</summary>
        public bool Dies => DiesFromHit || (!Target.IsPlayer && Slide.Falls) || Target.Hp - Damage - CollisionDamage <= 0;
    }

    /// <summary>밀치기 미리보기. 실제 밀치기도 같은 값을 쓴다 (설계 규칙 6).</summary>
    public sealed class PushPreview
    {
        public PushBlock Block { get; }
        public GridPos Direction { get; }
        public IReadOnlyList<GridPos> Cells { get; }
        public IReadOnlyList<PushHit> Hits { get; }

        public bool CanPush => Block == PushBlock.None;

        public PushPreview(PushBlock block, GridPos direction, IReadOnlyList<GridPos> cells, IReadOnlyList<PushHit> hits)
        {
            Block = block;
            Direction = direction;
            Cells = cells;
            Hits = hits;
        }
    }

    /// <summary>
    /// 밀치기의 한 곳 (BALANCE 9번 · 10번, 결정 2026-11-10 사용자). 플레이어 밀치기 스킬과 돌격병 근접 밀기(Knockback)가 같은 Slide · 낙하 처리를 쓴다.
    /// - 범위: 플레이어 앞 동서남북 한 방향으로 길이 PushLength × 너비 PushWidth. 너비의 옆 줄은 side(+1 / −1) 쪽.
    /// - 범위 안 유닛은 직접 피해(공격력 공식 × PushDamagePercent, 엄폐 · 치명 없음)를 받고, 범위 끝 바로 바깥 칸까지 밀린다.
    /// - 미는 중 낭떠러지에 들어가면 떨어진다 (적은 즉사). 벽 · 기둥 · 상자 · 낮은 벽 · 다른 유닛 · 맵 끝에 부딪히면 그 앞에서 멈추고 충돌 피해.
    /// - 먼 유닛부터 민다 — 앞 유닛이 비켜 준 칸으로 뒤 유닛이 밀린다. 밀려 움직일 때는 감시 반응이 없다.
    /// </summary>
    public static class Push
    {
        /// <summary>마우스 칸으로 방향(동서남북)과 옆 줄(+1 / −1)을 고른다. 가로 · 세로 중 큰 쪽이 방향, 같으면 가로.</summary>
        public static void Aim(GridPos origin, GridPos pointer, out GridPos direction, out int side)
        {
            int dx = pointer.X - origin.X;
            int dy = pointer.Y - origin.Y;
            if (dx == 0 && dy == 0) dx = 1;
            direction = Math.Abs(dx) >= Math.Abs(dy) ? new GridPos(Math.Sign(dx), 0) : new GridPos(0, Math.Sign(dy));
            var perpendicular = Perpendicular(direction);
            int across = dx * perpendicular.X + dy * perpendicular.Y;
            side = across < 0 ? -1 : 1;
        }

        public static GridPos Perpendicular(GridPos direction) => new GridPos(-direction.Y, direction.X);

        /// <summary>범위 칸 (맵 밖은 뺀다). 앞쪽 깊이 1 → PushLength, 각 깊이에서 가운데 줄 → 옆 줄.</summary>
        public static List<GridPos> AreaCells(FloorMap map, CombatSettings settings, GridPos origin, GridPos direction, int side)
        {
            var perpendicular = Perpendicular(direction);
            var cells = new List<GridPos>();
            for (int depth = 1; depth <= settings.PushLength; depth++)
            {
                for (int lane = 0; lane < settings.PushWidth; lane++)
                {
                    var cell = new GridPos(origin.X + direction.X * depth + perpendicular.X * lane * side,
                        origin.Y + direction.Y * depth + perpendicular.Y * lane * side);
                    if (map.InBounds(cell)) cells.Add(cell);
                }
            }
            return cells;
        }

        public static PushPreview Preview(World world, CombatSettings settings, Unit attacker, GridPos direction, int side)
        {
            var map = world.Map;
            var cells = AreaCells(map, settings, attacker.Position, direction, side);

            // 먼 유닛부터 (깊이 큰 순 → 가운데 줄 먼저 → ID).
            var targets = new List<Unit>();
            foreach (var unit in world.Units)
                if (unit != attacker && cells.Contains(unit.Position)) targets.Add(unit);
            targets.Sort((a, b) =>
            {
                int depthA = Depth(attacker.Position, a.Position, direction);
                int depthB = Depth(attacker.Position, b.Position, direction);
                if (depthA != depthB) return depthB.CompareTo(depthA);
                int laneA = Math.Abs(Depth(attacker.Position, a.Position, Perpendicular(direction)));
                int laneB = Math.Abs(Depth(attacker.Position, b.Position, Perpendicular(direction)));
                return laneA != laneB ? laneA.CompareTo(laneB) : a.Id.CompareTo(b.Id);
            });

            // 미는 동안 칸 차지를 따라간다 — 앞 유닛이 비켜 준 칸, 떨어지거나 쓰러진 유닛의 칸은 빈다.
            var occupied = new HashSet<GridPos>();
            foreach (var unit in world.Units) occupied.Add(unit.Position);

            var hits = new List<PushHit>();
            foreach (var target in targets)
            {
                int damage = CombatRules.Damage(attacker.Attack, target.Defense, settings.PushDamagePercent, 0, settings.MinDamage);
                bool diesFromHit = target.Hp - damage <= 0;
                var start = target.Position;
                occupied.Remove(start);
                if (diesFromHit)
                {
                    hits.Add(new PushHit(target, damage, true, new SlideResult(new GridPos[0], start, false, false), 0));
                    continue;
                }

                int distance = settings.PushLength + 1 - Depth(attacker.Position, start, direction);
                var slide = Slide(map, occupied, start, direction, distance);
                int collision = slide.Collides ? settings.PushCollisionDamage : 0;
                var hit = new PushHit(target, damage, false, slide, collision);
                if (!hit.Dies && !slide.Falls) occupied.Add(slide.End);
                hits.Add(hit);
            }

            var block = world.PushCooldown > 0 ? PushBlock.NotReady : hits.Count == 0 ? PushBlock.NoTarget : PushBlock.None;
            return new PushPreview(block, direction, cells, hits);
        }

        /// <summary>
        /// 한 칸씩 distance만큼 민다. 다음 칸이 낭떠러지면 그 칸으로 떨어지고,
        /// 막히면(설 수 없는 칸 · 낮은 벽 · occupied) 지금 칸에서 멈추고 부딪힌다. 근접 밀치기(2-8)도 이것을 쓴다.
        /// </summary>
        public static SlideResult Slide(FloorMap map, ICollection<GridPos> occupied, GridPos start, GridPos direction, int distance)
        {
            var path = new List<GridPos>();
            var current = start;
            for (int i = 0; i < distance; i++)
            {
                var next = new GridPos(current.X + direction.X, current.Y + direction.Y);
                if (map.IsChasm(next) && !map.HasLowWallBetween(current, next))
                {
                    path.Add(next);
                    return new SlideResult(path, next, true, false);
                }
                if (!map.IsWalkable(next) || map.HasLowWallBetween(current, next) || occupied.Contains(next))
                    return new SlideResult(path, current, false, true);
                path.Add(next);
                current = next;
            }
            return new SlideResult(path, current, false, false);
        }

        /// <summary>밀치기를 처리한다. 쓸 수 없으면 아무것도 바꾸지 않고 false.</summary>
        public static bool Resolve(World world, CombatSettings settings, Unit attacker, GridPos direction, int side, List<GameEvent> events)
        {
            var preview = Preview(world, settings, attacker, direction, side);
            if (!preview.CanPush) return false;

            events.Add(new PushUsed(attacker.Id, attacker.Kind, direction, preview.Cells));
            foreach (var hit in preview.Hits)
            {
                var target = hit.Target;
                target.Hp -= hit.Damage;
                events.Add(new UnitAttacked(attacker, target, hit.Damage, false, CoverLevel.None, false, false));
                Alert.Wake(world, target, events);

                ApplySlide(world, settings, attacker, target, hit.Slide, hit.CollisionDamage, events);
            }

            world.PushCooldown = settings.PushCooldownRounds;
            return true;
        }

        /// <summary>
        /// 공격이 맞은 뒤 상대를 공격자 반대쪽으로 attacker.PushOnHit칸 민다 (돌격병 근접 공격 — CombatRules.Resolve가 부른다).
        /// 상대가 이미 쓰러졌거나 밀기 능력이 없으면 아무것도 하지 않는다. 막히면 충돌 피해, 낭떠러지면 떨어진다.
        /// </summary>
        public static void Knockback(World world, CombatSettings settings, Unit attacker, Unit target, List<GameEvent> events)
        {
            if (attacker.PushOnHit <= 0 || target.Hp <= 0) return;
            var direction = new GridPos(Math.Sign(target.Position.X - attacker.Position.X), Math.Sign(target.Position.Y - attacker.Position.Y));
            if (direction.X == 0 && direction.Y == 0) return;

            var occupied = new HashSet<GridPos>();
            foreach (var unit in world.Units) if (unit != target) occupied.Add(unit.Position);
            var slide = Slide(world.Map, occupied, target.Position, direction, attacker.PushOnHit);
            ApplySlide(world, settings, attacker, target, slide, slide.Collides ? settings.PushCollisionDamage : 0, events);
        }

        /// <summary>
        /// 밀린 결과를 세계에 적용한다: 칸마다 UnitMoved → UnitPushed → 충돌 피해 →
        /// 떨어지면 적은 즉사, 플레이어는 최대 체력의 FallDamagePercent 피해 후 가장 가까운 빈 바닥에서 다시 일어난다 (결정 2026-11-10, 사용자).
        /// </summary>
        private static void ApplySlide(World world, CombatSettings settings, Unit attacker, Unit target, SlideResult slide, int collisionDamage, List<GameEvent> events)
        {
            var from = target.Position;
            foreach (var cell in slide.Path)
            {
                events.Add(new UnitMoved(target.Id, target.Position, cell));
                target.Position = cell;
            }
            if (slide.Path.Count > 0 || slide.Collides)
                events.Add(new UnitPushed(target.Id, target.Kind, from, slide.End, slide.Falls, collisionDamage));

            target.Hp -= collisionDamage;
            events.Add(new UnitStatsChanged(target.Id));

            if (target.IsPlayer)
            {
                if (slide.Falls && target.Hp > 0) RecoverFromFall(world, settings, target, slide.End, events);
                if (target.Hp > 0) return;
                target.Hp = 0;
                world.Mode = WorldMode.Defeat;
                events.Add(new PlayerDefeated(attacker.Id));
            }
            else if (slide.Falls || target.Hp <= 0)
            {
                CombatFlow.Kill(world, target, events);
            }
        }

        /// <summary>떨어진 플레이어: 피해를 받고, 살아 있으면 떨어진 칸에서 가장 가까운 빈 바닥으로 옮긴다.</summary>
        private static void RecoverFromFall(World world, CombatSettings settings, Unit player, GridPos fellAt, List<GameEvent> events)
        {
            int damage = FallDamage(player.MaxHp, settings.FallDamagePercent);
            player.Hp -= damage;
            events.Add(new UnitStatsChanged(player.Id));
            if (player.Hp <= 0)
            {
                events.Add(new PlayerFell(player.Id, fellAt, fellAt, damage));
                return;
            }

            var cell = NearestOpenFloor(world, fellAt);
            player.Position = cell;
            events.Add(new PlayerFell(player.Id, fellAt, cell, damage));
        }

        /// <summary>최대 체력 × 비율, 반올림 (0.5 올림).</summary>
        public static int FallDamage(int maxHp, int percent) => (maxHp * percent * 2 + 100) / 200;

        /// <summary>
        /// from에서 8방향으로 넓혀 가며(벽 · 기둥 · 상자는 지나지 않음) 처음 만나는 설 수 있고 빈 칸.
        /// 이웃 순서가 고정이라 같은 상태면 같은 칸이 나온다. 없으면 시작 칸.
        /// </summary>
        public static GridPos NearestOpenFloor(World world, GridPos from)
        {
            var map = world.Map;
            var seen = new HashSet<GridPos> { from };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(from);
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                if (map.IsWalkable(cell) && !world.IsOccupied(cell)) return cell;
                foreach (var step in NeighborSteps)
                {
                    var next = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    if (!map.InBounds(next) || (!map.IsWalkable(next) && !map.IsChasm(next)) || !seen.Add(next)) continue;
                    frontier.Enqueue(next);
                }
            }
            return map.StartCell;
        }

        private static readonly GridPos[] NeighborSteps =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
            new GridPos(1, 1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(-1, -1),
        };

        private static int Depth(GridPos origin, GridPos cell, GridPos direction)
        {
            return (cell.X - origin.X) * direction.X + (cell.Y - origin.Y) * direction.Y;
        }
    }

    /// <summary>플레이어 밀치기 스킬. 쓰면 차례가 끝나고 적 차례가 이어진다 (전투가 끝났거나 쓰러졌으면 그대로).</summary>
    public sealed class PushCommand : ICommand
    {
        private readonly GridPos direction;
        private readonly int side;
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public PushCommand(GridPos direction, int side, CombatSettings settings)
        {
            this.direction = direction;
            this.side = side;
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!TurnFlow.IsPlayerTurn(world) || world.PlayerActed) return false;
            if (Math.Abs(direction.X) + Math.Abs(direction.Y) != 1 || (side != 1 && side != -1)) return false;
            if (!Push.Resolve(world, settings, world.Player, direction, side, events)) return false;

            world.PlayerActed = true;
            if (world.Mode == WorldMode.Combat && world.Combatants.Count > 0) TurnFlow.EndPlayerTurn(world, settings, events);
            return true;
        }
    }
}
