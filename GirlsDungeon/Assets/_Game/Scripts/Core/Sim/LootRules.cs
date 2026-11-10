using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>바닥 물건이 어디서 생겼는지 (화면 기록용).</summary>
    public enum DropSource
    {
        Combat = 0,
        SupplyCrate = 1,
        /// <summary>무기를 바꿔 원래 무기를 내려놓았다.</summary>
        Swap = 2,
        Debug = 3,
    }

    /// <summary>
    /// 보상 · 바닥 물건의 한 곳 (BALANCE 12번, 결정 2026-11-10 사용자).
    /// 전투에서 이기면 마지막으로 쓰러뜨린 적 자리에 가끔 무기나 구급상자가 떨어지고, 보급 상자를 열면 항상 하나가 나온다.
    /// 플레이어가 물건 칸에 들어서면: 구급상자는 바로 회복, 무기는 WeaponFound → 화면이 비교 창을 띄우고 SwapWeaponCommand를 낸다.
    /// 굴림은 모두 아이템 난수 하나로 한다.
    /// </summary>
    public static class Loot
    {
        private static readonly GridPos[] Steps =
        {
            new GridPos(1, 0), new GridPos(-1, 0), new GridPos(0, 1), new GridPos(0, -1),
            new GridPos(1, 1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(-1, -1),
        };

        /// <summary>전투가 끝날 때(CombatFlow) 부른다: 무기 · 구급상자 · 없음을 굴려 마지막으로 쓰러뜨린 적 자리 근처에 놓는다.</summary>
        public static void DropAfterCombat(World world, CombatSettings settings, GridPos lastKillCell, List<GameEvent> events)
        {
            var random = world.Random.Get(RandomPurpose.Item);
            int roll = random.Range(0, 100);
            if (roll < settings.CombatDropWeaponPercent) DropWeapon(world, settings, random, lastKillCell, DropSource.Combat, events);
            else if (roll < settings.CombatDropWeaponPercent + settings.CombatDropMedkitPercent)
                Drop(world, ItemKind.Medkit, default, lastKillCell, DropSource.Combat, events);
        }

        /// <summary>
        /// 닫힌 보급 상자를 연다: 열린 상자로 바꾸고, 무기(CrateWeaponPercent) 또는 구급상자를 상자 바로 옆 빈 칸에 놓는다.
        /// 닫힌 보급 상자가 아니면 아무것도 바꾸지 않고 false. 거리 · 차례 확인은 OpenSupplyCrateCommand가 한다.
        /// </summary>
        public static bool OpenCrate(World world, CombatSettings settings, GridPos cell, List<GameEvent> events)
        {
            var map = world.Map;
            if (!map.InBounds(cell) || map.Get(cell) != TileType.SupplyCrate) return false;

            map.SetTile(cell, TileType.SupplyCrateOpened);
            events.Add(new SupplyCrateOpened(cell));
            var random = world.Random.Get(RandomPurpose.Item);
            if (random.Range(0, 100) < settings.CrateWeaponPercent) DropWeapon(world, settings, random, cell, DropSource.SupplyCrate, events);
            else Drop(world, ItemKind.Medkit, default, cell, DropSource.SupplyCrate, events);
            return true;
        }

        /// <summary>
        /// 이번 커맨드로 플레이어가 물건 칸에 들어섰으면 줍는다. Simulation이 커맨드마다 CombatFlow 뒤에 부른다.
        /// 구급상자: 체력이 모자라면 바로 회복하고 사라진다 (가득이면 그대로 둔다). 무기: WeaponFound만 내고 고르는 것은 화면에 맡긴다.
        /// </summary>
        public static void CheckPickup(World world, CombatSettings settings, List<GameEvent> events)
        {
            var player = world.Player;
            if (player == null || (world.Mode != WorldMode.Exploration && world.Mode != WorldMode.Combat)) return;
            if (!SteppedOnThisCommand(world, player, events)) return;

            bool offered = false;
            foreach (var item in world.Items.ToArray())
            {
                if (item.Position != player.Position) continue;
                if (item.Kind == ItemKind.Medkit)
                {
                    if (player.Hp >= player.MaxHp) continue;
                    int healed = System.Math.Min(MedkitHeal(player.MaxHp, settings.MedkitHealPercent), player.MaxHp - player.Hp);
                    player.Hp += healed;
                    world.Items.Remove(item);
                    events.Add(new MedkitUsed(item.Id, healed));
                    events.Add(new UnitStatsChanged(player.Id));
                }
                else if (item.Kind == ItemKind.Weapon && !offered)
                {
                    offered = true;
                    events.Add(new WeaponFound(item.Id, item.Weapon));
                }
            }
        }

        /// <summary>
        /// 발밑 무기와 든 무기를 바꾼다: 새 무기를 들고(Loadout.Apply), 원래 무기는 같은 칸에 내려놓는다 (새 ID).
        /// 무기가 아니거나 발밑이 아니면 아무것도 바꾸지 않고 false.
        /// </summary>
        public static bool Swap(World world, CombatSettings settings, int itemId, List<GameEvent> events)
        {
            var player = world.Player;
            var item = world.FindItem(itemId);
            if (player == null || item == null || item.Kind != ItemKind.Weapon || item.Position != player.Position) return false;
            if (settings.TryWeapon(item.Weapon) == null) return false;

            var old = world.PlayerWeapon;
            world.Items.Remove(item);
            world.PlayerWeapon = item.Weapon;
            Loadout.Apply(world, settings);
            var dropped = world.AddItem(ItemKind.Weapon, old, player.Position);
            events.Add(new WeaponSwapped(old, item.Weapon, item.Id));
            events.Add(new ItemDropped(dropped.Id, dropped.Kind, dropped.Weapon, dropped.Position, DropSource.Swap));
            events.Add(new UnitStatsChanged(player.Id));
            return true;
        }

        /// <summary>최대 체력 × 비율, 반올림 (0.5 올림).</summary>
        public static int MedkitHeal(int maxHp, int percent) => (maxHp * percent * 2 + 100) / 200;

        /// <summary>
        /// from에서 8방향으로 넓혀 가며(바닥 · 낭떠러지로만 지나감) 처음 만나는, 설 수 있고 유닛 · 물건이 없는 칸.
        /// from이 보급 상자처럼 설 수 없는 칸이면 그 바로 옆부터 본다. 이웃 순서가 고정이라 같은 상태면 같은 칸. 없으면 null.
        /// </summary>
        public static GridPos? FreeCellNear(World world, GridPos from)
        {
            var map = world.Map;
            var seen = new HashSet<GridPos> { from };
            var frontier = new Queue<GridPos>();
            frontier.Enqueue(from);
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                if (map.IsWalkable(cell) && !world.IsOccupied(cell) && world.ItemAt(cell) == null) return cell;
                foreach (var step in Steps)
                {
                    var next = new GridPos(cell.X + step.X, cell.Y + step.Y);
                    if (!map.InBounds(next) || (!map.IsWalkable(next) && !map.IsChasm(next)) || !seen.Add(next)) continue;
                    frontier.Enqueue(next);
                }
            }
            return null;
        }

        /// <summary>지금 든 것과 다른 무기 중 하나 (표 순서에서 아이템 난수로). 다른 무기가 없으면 null.</summary>
        public static WeaponKind? RandomOtherWeapon(World world, CombatSettings settings, IRandom random)
        {
            var others = new List<WeaponKind>();
            foreach (var weapon in settings.Weapons) if (weapon.Kind != world.PlayerWeapon) others.Add(weapon.Kind);
            if (others.Count == 0) return null;
            return others[random.Range(0, others.Count)];
        }

        private static void DropWeapon(World world, CombatSettings settings, IRandom random, GridPos near, DropSource source, List<GameEvent> events)
        {
            var weapon = RandomOtherWeapon(world, settings, random);
            if (weapon.HasValue) Drop(world, ItemKind.Weapon, weapon.Value, near, source, events);
            else Drop(world, ItemKind.Medkit, default, near, source, events);
        }

        private static void Drop(World world, ItemKind kind, WeaponKind weapon, GridPos near, DropSource source, List<GameEvent> events)
        {
            // 빈 칸이 없으면(아주 드묾) 플레이어 발밑에 둔다 — 물건이 사라지지 않게.
            var cell = FreeCellNear(world, near) ?? world.Player.Position;
            var item = world.AddItem(kind, weapon, cell);
            events.Add(new ItemDropped(item.Id, item.Kind, item.Weapon, item.Position, source));
        }

        private static bool SteppedOnThisCommand(World world, Unit player, List<GameEvent> events)
        {
            for (int i = events.Count - 1; i >= 0; i--)
                if (events[i] is UnitMoved moved && moved.UnitId == player.Id) return moved.To == player.Position;
            return false;
        }
    }

    /// <summary>
    /// 바로 옆(8방향) 보급 상자를 연다. 탐험 중에는 언제나, 전투 중에는 내 차례에 아직 행동하지 않았을 때 — 열면 차례가 끝난다.
    /// </summary>
    public sealed class OpenSupplyCrateCommand : ICommand
    {
        private readonly GridPos cell;
        private readonly CombatSettings settings;

        public int CostTicks { get; }

        public OpenSupplyCrateCommand(GridPos cell, CombatSettings settings)
        {
            this.cell = cell;
            this.settings = settings;
            CostTicks = settings.EndTurnCostTicks;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            var player = world.Player;
            if (player == null) return false;
            bool inCombat = world.Mode == WorldMode.Combat;
            if (inCombat ? !TurnFlow.IsPlayerTurn(world) || world.PlayerActed : world.Mode != WorldMode.Exploration) return false;
            if (Loadout.CellDistance(player.Position, cell) != 1) return false;
            if (!Loot.OpenCrate(world, settings, cell, events)) return false;

            if (!inCombat) return true;
            world.PlayerActed = true;
            if (world.Combatants.Count > 0) TurnFlow.EndPlayerTurn(world, settings, events);
            return true;
        }
    }

    /// <summary>발밑 무기로 바꾼다 (비교 창의 "바꾸기"). 탐험 중이나 전투 중 내 차례에. 행동을 쓰지 않는다.</summary>
    public sealed class SwapWeaponCommand : ICommand
    {
        private readonly int itemId;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public SwapWeaponCommand(int itemId, CombatSettings settings)
        {
            this.itemId = itemId;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (world.Mode != WorldMode.Exploration && !TurnFlow.IsPlayerTurn(world)) return false;
            return Loot.Swap(world, settings, itemId, events);
        }
    }

    /// <summary>디버그 전용: 설 수 있는 칸에 물건을 놓는다.</summary>
    public sealed class DebugDropItemCommand : ICommand
    {
        private readonly ItemKind kind;
        private readonly WeaponKind weapon;
        private readonly GridPos cell;

        public int CostTicks => 0;

        public DebugDropItemCommand(ItemKind kind, WeaponKind weapon, GridPos cell)
        {
            this.kind = kind;
            this.weapon = weapon;
            this.cell = cell;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!world.Map.IsWalkable(cell)) return false;
            var item = world.AddItem(kind, weapon, cell);
            events.Add(new ItemDropped(item.Id, item.Kind, item.Weapon, item.Position, DropSource.Debug));
            return true;
        }
    }

    /// <summary>바닥에 물건이 놓였다.</summary>
    public sealed class ItemDropped : GameEvent
    {
        public int ItemId { get; }
        public ItemKind Kind { get; }
        public WeaponKind Weapon { get; }
        public GridPos Cell { get; }
        public DropSource Source { get; }

        public ItemDropped(int itemId, ItemKind kind, WeaponKind weapon, GridPos cell, DropSource source)
        {
            ItemId = itemId;
            Kind = kind;
            Weapon = weapon;
            Cell = cell;
            Source = source;
        }
    }

    /// <summary>보급 상자를 열었다. 지형을 다시 그려야 한다. 나온 물건은 ItemDropped가 뒤따른다.</summary>
    public sealed class SupplyCrateOpened : GameEvent
    {
        public GridPos Cell { get; }

        public SupplyCrateOpened(GridPos cell)
        {
            Cell = cell;
        }
    }

    /// <summary>구급상자를 주워 회복했다.</summary>
    public sealed class MedkitUsed : GameEvent
    {
        public int ItemId { get; }
        public int Healed { get; }

        public MedkitUsed(int itemId, int healed)
        {
            ItemId = itemId;
            Healed = healed;
        }
    }

    /// <summary>발밑에 무기가 있다. 화면이 비교 창을 띄운다 (바꾸기 = SwapWeaponCommand, 두기 = 아무것도 안 함).</summary>
    public sealed class WeaponFound : GameEvent
    {
        public int ItemId { get; }
        public WeaponKind Weapon { get; }

        public WeaponFound(int itemId, WeaponKind weapon)
        {
            ItemId = itemId;
            Weapon = weapon;
        }
    }

    /// <summary>무기를 바꿨다. 원래 무기는 ItemDropped(Swap)로 같은 칸에 놓인다.</summary>
    public sealed class WeaponSwapped : GameEvent
    {
        public WeaponKind From { get; }
        public WeaponKind To { get; }
        public int PickedItemId { get; }

        public WeaponSwapped(WeaponKind from, WeaponKind to, int pickedItemId)
        {
            From = from;
            To = to;
            PickedItemId = pickedItemId;
        }
    }
}
