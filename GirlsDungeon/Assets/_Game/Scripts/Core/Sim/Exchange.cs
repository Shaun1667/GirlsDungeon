using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>교환소 물건 (3-14f). 저장에 비트로 들어가므로 끝에만 추가한다 (최대 8개).</summary>
    public enum ExchangeOffer : byte
    {
        /// <summary>구급상자 1개를 재료로 산다.</summary>
        Medkit = 0,
        /// <summary>든 무기용 파츠 1개(등급 ExchangePartGrade)를 재료로 산다.</summary>
        Part = 1,
        /// <summary>열쇠 1개를 재료로 산다.</summary>
        Key = 2,
        /// <summary>열쇠 1개를 재료로 판다.</summary>
        SellKey = 3,
    }

    /// <summary>
    /// 교환소의 한 곳 (3-14f — 특수 방). 교환대(TileType.ExchangeCounter) 바로 옆에서, 탐험 중에만.
    /// 물건마다 이 층에서 한 번씩 (World.ExchangeSold 비트, 층을 새로 만들 때 0). 값은 CombatSettings.Exchange… (BALANCE 25번).
    /// 구급상자 · 파츠는 교환대 옆 빈 칸에 놓이고(밟아서 줍기), 열쇠 · 재료는 바로 들어온다. 파츠 굴림은 아이템 난수.
    /// </summary>
    public static class Exchange
    {
        public const int OfferCount = 4;

        /// <summary>플레이어가 교환대 바로 옆에 있고 탐험 중이면 그 교환대 칸.</summary>
        public static GridPos? CounterNextTo(World world)
        {
            var player = world.Player;
            if (player == null || world.Mode != WorldMode.Exploration) return null;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new GridPos(player.Position.X + dx, player.Position.Y + dy);
                    if ((dx != 0 || dy != 0) && world.Map.InBounds(cell) && world.Map.Get(cell) == TileType.ExchangeCounter) return cell;
                }
            return null;
        }

        public static bool AtCounter(World world) => CounterNextTo(world).HasValue;

        public static bool IsSold(World world, ExchangeOffer offer) => (world.ExchangeSold & (1 << (int)offer)) != 0;

        /// <summary>그 물건의 값 (재료, 열쇠 팔기는 받는 재료).</summary>
        public static int Price(CombatSettings settings, ExchangeOffer offer)
        {
            switch (offer)
            {
                case ExchangeOffer.Medkit: return settings.ExchangeMedkitCost;
                case ExchangeOffer.Part: return settings.ExchangePartCost;
                case ExchangeOffer.Key: return settings.ExchangeKeyCost;
                default: return settings.ExchangeKeySellMaterials;
            }
        }

        /// <summary>지금 살(팔) 수 있는가 — 교환대 옆 · 아직 안 팔림 · 재료(또는 열쇠)가 있음.</summary>
        public static bool CanTrade(World world, CombatSettings settings, ExchangeOffer offer)
        {
            if (!AtCounter(world) || IsSold(world, offer)) return false;
            if (offer == ExchangeOffer.SellKey) return world.Keys > 0;
            return world.PartMaterials >= Price(settings, offer);
        }

        public static bool Trade(World world, CombatSettings settings, ExchangeOffer offer, List<GameEvent> events)
        {
            if (!CanTrade(world, settings, offer)) return false;
            var counter = CounterNextTo(world).Value;
            switch (offer)
            {
                case ExchangeOffer.Medkit:
                    world.PartMaterials -= settings.ExchangeMedkitCost;
                    DropNear(world, ItemKind.Medkit, counter, events, null);
                    break;
                case ExchangeOffer.Part:
                    world.PartMaterials -= settings.ExchangePartCost;
                    var part = Parts.GenerateWithGrade(settings, world.PlayerWeapon, settings.ExchangePartGrade, world.Random.Get(RandomPurpose.Item));
                    DropNear(world, ItemKind.Part, counter, events, part);
                    break;
                case ExchangeOffer.Key:
                    world.PartMaterials -= settings.ExchangeKeyCost;
                    world.Keys++;
                    break;
                case ExchangeOffer.SellKey:
                    world.Keys--;
                    world.PartMaterials += settings.ExchangeKeySellMaterials;
                    break;
            }
            world.ExchangeSold |= (byte)(1 << (int)offer);
            events.Add(new ExchangeTraded(offer, world.PartMaterials, world.Keys));
            events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }

        private static void DropNear(World world, ItemKind kind, GridPos near, List<GameEvent> events, WeaponPart part)
        {
            var cell = Loot.FreeCellNear(world, near) ?? world.Player.Position;
            var item = part != null ? world.AddPartItem(part, cell) : world.AddItem(kind, default, cell);
            events.Add(new ItemDropped(item.Id, item.Kind, item.Weapon, item.Position, DropSource.SupplyCrate));
        }
    }

    /// <summary>교환소에서 사고판다. 행동 · 틱을 쓰지 않는다.</summary>
    public sealed class ExchangeCommand : ICommand
    {
        private readonly ExchangeOffer offer;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public ExchangeCommand(ExchangeOffer offer, CombatSettings settings)
        {
            this.offer = offer;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => Exchange.Trade(world, settings, offer, events);
    }

    /// <summary>교환소에서 사고팔았다.</summary>
    public sealed class ExchangeTraded : GameEvent
    {
        public ExchangeOffer Offer { get; }
        public int MaterialsNow { get; }
        public int KeysNow { get; }

        public ExchangeTraded(ExchangeOffer offer, int materialsNow, int keysNow)
        {
            Offer = offer;
            MaterialsNow = materialsNow;
            KeysNow = keysNow;
        }
    }
}
