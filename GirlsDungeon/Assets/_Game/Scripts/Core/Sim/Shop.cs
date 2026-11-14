using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>상인에게 파는 것 (3-15c). 저장되지 않는 커맨드 값이지만 끝에만 추가한다.</summary>
    public enum ShopSale : byte
    {
        /// <summary>예비 파츠 보관함의 파츠 하나 (값 = 그 등급의 사는 값 × ShopSellPercent).</summary>
        SparePart = 0,
        /// <summary>열쇠 하나.</summary>
        Key = 1,
        /// <summary>파츠 재료 하나.</summary>
        Material = 2,
    }

    /// <summary>
    /// 상점의 한 곳 (3-15b · 3-15c, 결정 2026-11-14 사용자: 구역의 첫 층 · 사르디스 골드 · 바닥 진열 + 상인 · 팔기 있음).
    /// 오픈소스 게임 Shattered Pixel Dungeon의 상점 구조를 참고했다 — 물건은 방 가장자리 바닥에 값과 함께 놓이고, 밟으면 사기 창, 가운데 상인에게 판다.
    /// - 진열(Stock): 층을 만든 뒤 RunFactory가 부른다. 상점 방 가장자리 줄의 칸에 고르게(입구 · 계산대 옆 제외), 물건마다 FloorItem.Price. 굴림은 상점 난수.
    /// - 사기(Buy): 그 물건 칸에 서서 골드가 값 이상이면 → 값을 내고 Price = 0, 그 자리에서 보통 줍기(Loot.PickUp — 무기 · 파츠는 비교 창).
    /// - 팔기(Sell): 계산대(TileType.ShopCounter) 바로 옆, 탐험 중. 예비 파츠 · 열쇠 · 재료를 골드로.
    /// 값은 CombatSettings.Shop… (BALANCE 32번).
    /// </summary>
    public static class Shop
    {
        /// <summary>층에 상점 방이 있으면 진열 물건을 놓는다 (이미 값이 있는 물건이 있으면 다시 놓지 않는다).</summary>
        public static void Stock(World world, CombatSettings settings)
        {
            var map = world.Map;
            int roomIndex = -1;
            for (int i = 0; i < map.Rooms.Count; i++)
                if (map.RoomKind(i) == SpecialRoomKind.Shop) { roomIndex = i; break; }
            if (roomIndex < 0) return;
            foreach (var item in world.Items) if (item.Price > 0) return;

            var room = map.Rooms[roomIndex];
            PlaceKeeper(world, settings, room);
            var random = world.Random.Get(RandomPurpose.Shop);
            var stock = new List<(ItemKind kind, PartGrade grade)>();
            for (int i = 0; i < settings.ShopMedkits; i++) stock.Add((ItemKind.Medkit, default));
            for (int i = 0; i < settings.ShopKeys; i++) stock.Add((ItemKind.Key, default));
            foreach (var grade in settings.ShopPartGrades) stock.Add((ItemKind.Part, grade));
            for (int i = 0; i < settings.ShopWeapons; i++) stock.Add((ItemKind.Weapon, default));

            var cells = DisplayCells(world, room, stock.Count);
            for (int i = 0; i < stock.Count && i < cells.Count; i++)
            {
                var (kind, grade) = stock[i];
                FloorItem item;
                if (kind == ItemKind.Part)
                {
                    item = world.AddPartItem(Parts.GenerateWithGrade(settings, world.PlayerWeapon, grade, random), cells[i]);
                    item.Price = settings.ShopPartPrices[(int)grade];
                }
                else if (kind == ItemKind.Weapon)
                {
                    var weapon = Loot.RandomOtherWeapon(world, settings, random);
                    if (!weapon.HasValue) continue;
                    item = world.AddItem(ItemKind.Weapon, weapon.Value, cells[i]);
                    item.Price = settings.ShopWeaponPrice;
                }
                else
                {
                    item = world.AddItem(kind, default, cells[i]);
                    item.Price = kind == ItemKind.Key ? settings.ShopKeyPrice : settings.ShopMedkitPrice;
                }
            }
        }

        /// <summary>상점 주인을 계산대 바로 옆(위 → 아래 → 왼쪽 → 오른쪽 → 대각) 첫 빈 칸에 세운다. 유닛 표에 줄이 없으면(옛 테스트 수치) 두지 않는다.</summary>
        private static void PlaceKeeper(World world, CombatSettings settings, RoomRect room)
        {
            if (!settings.Units.Exists(stats => stats.Kind == UnitKind.Shopkeeper)) return;
            var map = world.Map;
            GridPos? counter = null;
            for (int y = room.Y; y < room.Y + room.Height && !counter.HasValue; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                    if (map.Get(new GridPos(x, y)) == TileType.ShopCounter) { counter = new GridPos(x, y); break; }
            if (!counter.HasValue) return;
            var steps = new[] { new GridPos(0, -1), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(1, 0), new GridPos(-1, -1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(1, 1) };
            foreach (var step in steps)
            {
                var cell = new GridPos(counter.Value.X + step.X, counter.Value.Y + step.Y);
                if (!room.Contains(cell) || !map.IsWalkable(cell) || map.Get(cell) == TileType.Stairs || world.IsOccupied(cell)) continue;
                var keeper = world.AddUnit(settings.StatsFor(UnitKind.Shopkeeper), cell);
                keeper.Guarding = false;
                return;
            }
        }

        /// <summary>이 층의 상점 주인 (없거나 쓰러졌으면 null).</summary>
        public static Unit Keeper(World world)
        {
            foreach (var unit in world.Units) if (unit.Kind == UnitKind.Shopkeeper) return unit;
            return null;
        }

        /// <summary>아직 적대하지 않은 상점 주인 — 전투에 들어오지 않고 행동하지 않는다.</summary>
        public static bool IsNeutral(World world, Unit unit) => unit != null && unit.Kind == UnitKind.Shopkeeper && !world.ShopHostile;

        /// <summary>
        /// 이번 커맨드의 사건을 보고 (Simulation · 스킬 커맨드가 부른다): 상점 주인이 공격 · 밀치기에 맞았으면 적대한다 (어떤 피해든 — 결정 2026-11-14 사용자).
        /// 상점 주인이 쓰러졌으면 진열 물건이 모두 공짜가 된다 (값 0 — 밟으면 보통 줍기). 몇 번 불러도 같다.
        /// </summary>
        public static void CheckHostility(World world, List<GameEvent> events)
        {
            foreach (var gameEvent in events)
            {
                if (!(gameEvent is UnitDied died) || died.Kind != UnitKind.Shopkeeper) continue;
                int freed = 0;
                foreach (var item in world.Items)
                    if (item.Price > 0) { item.Price = 0; freed++; }
                if (freed > 0 || !world.ShopHostile) events.Add(new ShopkeeperDefeated(freed));
                world.ShopHostile = true;
                return;
            }

            var keeper = Keeper(world);
            if (keeper == null || world.ShopHostile) return;
            foreach (var gameEvent in events)
            {
                bool hit = (gameEvent is UnitAttacked attacked && attacked.TargetId == keeper.Id)
                    || (gameEvent is UnitPushed pushed && pushed.UnitId == keeper.Id);
                if (!hit) continue;
                world.ShopHostile = true;
                keeper.Guarding = false;
                events.Add(new ShopkeeperAngered(keeper.Id));
                return;
            }
        }

        /// <summary>
        /// 진열 칸: 방 가장자리 줄을 한 바퀴(위 → 오른쪽 → 아래 → 왼쪽) 돌며 설 수 있고 빈 칸 중 입구 · 계산대 바로 옆이 아닌 칸을 모아,
        /// 그중 count개를 고르게 고른다. 모자라면 안쪽 칸으로 채운다. 물건은 길을 막지 않는다.
        /// </summary>
        public static List<GridPos> DisplayCells(World world, RoomRect room, int count)
        {
            var map = world.Map;
            var ring = new List<GridPos>();
            int right = room.X + room.Width - 1;
            int bottom = room.Y + room.Height - 1;
            for (int x = room.X; x <= right; x++) ring.Add(new GridPos(x, room.Y));
            for (int y = room.Y + 1; y <= bottom; y++) ring.Add(new GridPos(right, y));
            for (int x = right - 1; x >= room.X; x--) ring.Add(new GridPos(x, bottom));
            for (int y = bottom - 1; y > room.Y; y--) ring.Add(new GridPos(room.X, y));

            var open = new List<GridPos>();
            foreach (var cell in ring) if (CanDisplay(world, room, cell) && !open.Contains(cell)) open.Add(cell);
            var result = new List<GridPos>();
            if (open.Count <= count) result.AddRange(open);
            else for (int i = 0; i < count; i++) result.Add(open[i * open.Count / count]);

            for (int y = room.Y + 1; y < bottom && result.Count < count; y++)
                for (int x = room.X + 1; x < right && result.Count < count; x++)
                {
                    var cell = new GridPos(x, y);
                    if (CanDisplay(world, room, cell) && !result.Contains(cell)) result.Add(cell);
                }
            return result;
        }

        private static bool CanDisplay(World world, RoomRect room, GridPos cell)
        {
            var map = world.Map;
            if (!map.IsWalkable(cell) || map.Get(cell) == TileType.Stairs || world.IsOccupied(cell) || world.ItemAt(cell) != null) return false;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var near = new GridPos(cell.X + dx, cell.Y + dy);
                    if (!map.InBounds(near)) continue;
                    if (map.Get(near) == TileType.ShopCounter) return false;
                    // 입구 바로 옆은 비운다 (방 밖의 설 수 있는 칸 · 문과 붙은 칸).
                    if (!room.Contains(near) && (map.IsWalkable(near) || map.IsLockedDoor(near))) return false;
                }
            return true;
        }

        /// <summary>발밑 진열 물건을 산다. 값이 없거나 골드가 모자라면 아무것도 바꾸지 않고 false.</summary>
        public static bool Buy(World world, CombatSettings settings, int itemId, List<GameEvent> events)
        {
            var player = world.Player;
            var item = world.FindItem(itemId);
            if (player == null || item == null || item.Price <= 0 || item.Position != player.Position || world.Gold < item.Price) return false;
            if (world.ShopHostile) return false;

            int price = item.Price;
            world.Gold -= price;
            item.Price = 0;
            events.Add(new ShopItemBought(item.Id, item.Kind, item.Weapon, price, world.Gold));
            bool offered = false;
            Loot.PickUp(world, settings, player, item, ref offered, events);
            return true;
        }

        /// <summary>플레이어가 계산대 바로 옆에 있고 탐험 중이면 그 계산대 칸.</summary>
        public static GridPos? CounterNextTo(World world)
        {
            var player = world.Player;
            if (player == null || world.Mode != WorldMode.Exploration) return null;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new GridPos(player.Position.X + dx, player.Position.Y + dy);
                    if ((dx != 0 || dy != 0) && world.Map.InBounds(cell) && world.Map.Get(cell) == TileType.ShopCounter) return cell;
                }
            return null;
        }

        public static bool AtCounter(World world) => CounterNextTo(world).HasValue;

        /// <summary>그 등급 파츠의 사는 값.</summary>
        public static int PartPrice(CombatSettings settings, PartGrade grade) => settings.ShopPartPrices[(int)grade];

        /// <summary>파는 값 = 사는 값 × ShopSellPercent%, 반올림 (0.5 올림).</summary>
        public static int SellValue(CombatSettings settings, int buyPrice) => (buyPrice * settings.ShopSellPercent * 2 + 100) / 200;

        /// <summary>그것을 팔면 받는 골드 (예비 파츠는 index번째). 팔 것이 없으면 0.</summary>
        public static int SaleValue(World world, CombatSettings settings, ShopSale sale, int index = 0)
        {
            switch (sale)
            {
                case ShopSale.SparePart:
                    return index >= 0 && index < world.SpareParts.Count ? SellValue(settings, PartPrice(settings, world.SpareParts[index].Grade)) : 0;
                case ShopSale.Key:
                    return world.Keys > 0 ? SellValue(settings, settings.ShopKeyPrice) : 0;
                default:
                    return world.PartMaterials > 0 ? settings.ShopMaterialSellPrice : 0;
            }
        }

        public static bool CanSell(World world, CombatSettings settings, ShopSale sale, int index = 0)
        {
            // 상점 주인이 적대했거나 쓰러졌으면 팔 수 없다 (3-15b).
            return AtCounter(world) && !world.ShopHostile && Keeper(world) != null && SaleValue(world, settings, sale, index) > 0;
        }

        /// <summary>상인에게 판다 (계산대 옆 · 탐험 중). 못 팔면 아무것도 바꾸지 않고 false.</summary>
        public static bool Sell(World world, CombatSettings settings, ShopSale sale, int index, List<GameEvent> events)
        {
            if (!CanSell(world, settings, sale, index)) return false;
            int value = SaleValue(world, settings, sale, index);
            if (sale == ShopSale.SparePart) world.SpareParts.RemoveAt(index);
            else if (sale == ShopSale.Key) world.Keys--;
            else world.PartMaterials--;
            events.Add(new ShopItemSold(sale, value));
            Gold.Gain(world, value, GoldSource.Sale, events);
            events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }
    }

    /// <summary>발밑 진열 물건을 산다 (사기 창의 "사기"). 탐험 중이나 전투 중 내 차례에. 행동을 쓰지 않는다.</summary>
    public sealed class BuyItemCommand : ICommand
    {
        private readonly int itemId;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public BuyItemCommand(int itemId, CombatSettings settings)
        {
            this.itemId = itemId;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (world.Mode != WorldMode.Exploration && !TurnFlow.IsPlayerTurn(world)) return false;
            return Shop.Buy(world, settings, itemId, events);
        }
    }

    /// <summary>상인에게 판다 (팔기 창). 계산대 옆, 탐험 중에만. 행동을 쓰지 않는다.</summary>
    public sealed class ShopSellCommand : ICommand
    {
        private readonly ShopSale sale;
        private readonly int index;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public ShopSellCommand(ShopSale sale, int index, CombatSettings settings)
        {
            this.sale = sale;
            this.index = index;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => Shop.Sell(world, settings, sale, index, events);
    }

    /// <summary>발밑에 상점 물건이 있다 — 화면이 사기 창을 띄운다 (사기 = BuyItemCommand, 두기 = 아무것도 안 함).</summary>
    public sealed class ShopItemOffered : GameEvent
    {
        public int ItemId { get; }
        public int Price { get; }

        public ShopItemOffered(int itemId, int price)
        {
            ItemId = itemId;
            Price = price;
        }
    }

    /// <summary>상점 주인이 적대했다 (3-15b). 사기 · 팔기가 닫히고, 보이면 전투에 들어온다.</summary>
    public sealed class ShopkeeperAngered : GameEvent
    {
        public int UnitId { get; }

        public ShopkeeperAngered(int unitId)
        {
            UnitId = unitId;
        }
    }

    /// <summary>상점 주인을 쓰러뜨렸다 (3-15b). 진열 물건 Freed개가 공짜가 됐다.</summary>
    public sealed class ShopkeeperDefeated : GameEvent
    {
        public int Freed { get; }

        public ShopkeeperDefeated(int freed)
        {
            Freed = freed;
        }
    }

    /// <summary>상점 물건을 샀다. GoldNow = 남은 골드. 이어서 보통 줍기 사건(KeyPickedUp · MedkitUsed · WeaponFound · PartFound)이 올 수 있다.</summary>
    public sealed class ShopItemBought : GameEvent
    {
        public int ItemId { get; }
        public ItemKind Kind { get; }
        public WeaponKind Weapon { get; }
        public int Price { get; }
        public int GoldNow { get; }

        public ShopItemBought(int itemId, ItemKind kind, WeaponKind weapon, int price, int goldNow)
        {
            ItemId = itemId;
            Kind = kind;
            Weapon = weapon;
            Price = price;
            GoldNow = goldNow;
        }
    }

    /// <summary>상인에게 팔았다. 받은 골드는 GoldGained(Sale)가 뒤따른다.</summary>
    public sealed class ShopItemSold : GameEvent
    {
        public ShopSale Sale { get; }
        public int Value { get; }

        public ShopItemSold(ShopSale sale, int value)
        {
            Sale = sale;
            Value = value;
        }
    }
}
