using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>제단 거래 하나 (BALANCE 27번): 능력치 CostStat을 CostAmount만큼 내고 BoonStat을 BoonAmount만큼 받는다 (둘 다 양수).</summary>
    public sealed class AltarTrade
    {
        public PartStat CostStat;
        public int CostAmount;
        public PartStat BoonStat;
        public int BoonAmount;
    }

    /// <summary>
    /// 제단의 한 곳 (3-14h — 잠긴 특수 방). 제단(TileType.Altar) 바로 옆에서, 탐험 중에만.
    /// - 열기 `Open`: 처음 열 때 거래 표(CombatSettings.AltarTrades)에서 서로 다른 거래 AltarOfferCount개를 골라 World.AltarOffers에 둔다 (아이템 난수).
    /// - 받기 `Accept`: 하나를 받으면 대가 · 보상이 World.AltarBonuses에 영구로 쌓이고(판이 끝날 때까지), 제단은 꺼진다. 거절은 창을 닫으면 된다.
    /// 효과는 파츠 합(Parts.Sum)에 함께 더해진다 — 능력치의 한 조회 지점(Loadout.Apply)을 지난다 (설계 규칙 7).
    /// </summary>
    public static class Altar
    {
        /// <summary>% 능력치 대가를 낸 뒤 남아야 하는 % 합의 아래 끝 (BALANCE 27-7).</summary>
        public const int MinimumPercent = -50;

        /// <summary>플레이어가 켜진 제단 바로 옆에 있고 탐험 중이면 그 칸.</summary>
        public static GridPos? AltarNextTo(World world)
        {
            var player = world.Player;
            if (player == null || world.Mode != WorldMode.Exploration) return null;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new GridPos(player.Position.X + dx, player.Position.Y + dy);
                    if ((dx != 0 || dy != 0) && world.Map.InBounds(cell) && world.Map.Get(cell) == TileType.Altar) return cell;
                }
            return null;
        }

        public static bool AtAltar(World world) => AltarNextTo(world).HasValue;

        /// <summary>제단을 연다 (처음이면 거래를 고른다). 제단 옆이 아니면 false.</summary>
        public static bool Open(World world, CombatSettings settings, List<GameEvent> events)
        {
            if (!AtAltar(world)) return false;
            if (world.AltarOffers.Count == 0)
            {
                var random = world.Random.Get(RandomPurpose.Item);
                var pool = new List<int>();
                for (int i = 0; i < settings.AltarTrades.Count; i++) pool.Add(i);
                int count = System.Math.Min(settings.AltarOfferCount, pool.Count);
                for (int n = 0; n < count; n++)
                {
                    int pick = random.Range(0, pool.Count);
                    world.AltarOffers.Add(pool[pick]);
                    pool.RemoveAt(pick);
                }
            }
            events.Add(new AltarOpened(world.AltarOffers.Count));
            return true;
        }

        /// <summary>진열된 거래(0부터)의 표 줄. 없으면 null.</summary>
        public static AltarTrade OfferAt(World world, CombatSettings settings, int index)
        {
            if (index < 0 || index >= world.AltarOffers.Count) return null;
            int row = world.AltarOffers[index];
            return row >= 0 && row < settings.AltarTrades.Count ? settings.AltarTrades[row] : null;
        }

        /// <summary>대가를 낸 뒤에도 능력치가 최소값 이상인지 (체력 · 공격 · 사거리 · 안정지수 · 이동은 1, % 능력치는 파츠 · 제단 % 합이 −50%, 나머지는 0).</summary>
        public static bool CanAccept(World world, CombatSettings settings, int index)
        {
            var trade = OfferAt(world, settings, index);
            var player = world.Player;
            if (trade == null || player == null) return false;
            return Current(world, player, trade.CostStat) - trade.CostAmount >= Minimum(trade.CostStat);
        }

        /// <summary>거래 하나를 받는다: 대가와 보상을 영구 효과로 쌓고 능력치를 다시 맞춘다. 제단은 꺼진다.</summary>
        public static bool Accept(World world, CombatSettings settings, int index, List<GameEvent> events)
        {
            var at = AltarNextTo(world);
            if (!at.HasValue || !CanAccept(world, settings, index)) return false;
            var trade = OfferAt(world, settings, index);
            world.AltarBonuses.Add(new PartBonus(trade.CostStat, -trade.CostAmount));
            world.AltarBonuses.Add(new PartBonus(trade.BoonStat, trade.BoonAmount));
            world.AltarOffers.Clear();
            world.Map.SetTile(at.Value, TileType.AltarUsed);
            Loadout.Apply(world, settings);
            events.Add(new AltarTraded(trade.CostStat, trade.CostAmount, trade.BoonStat, trade.BoonAmount));
            return true;
        }

        /// <summary>영구 효과 중 그 능력치의 합 (Parts.Sum이 더한다).</summary>
        public static int Sum(World world, PartStat stat)
        {
            int total = 0;
            foreach (var bonus in world.AltarBonuses) if (bonus.Stat == stat) total += bonus.Value;
            return total;
        }

        private static int Current(World world, Unit player, PartStat stat)
        {
            switch (stat)
            {
                // % 능력치(3-16n)는 지금 붙은 % 합 — 파츠 · 제단 모두.
                case PartStat.AttackPercent:
                case PartStat.MaxHpPercent:
                case PartStat.DefensePercent:
                    return Parts.Sum(world, stat);
                case PartStat.Attack: return player.Attack;
                case PartStat.AttackRange: return player.AttackRange;
                case PartStat.CritPercent: return player.CritPercent;
                case PartStat.CritMultiplier: return player.CritMultiplierPercent;
                case PartStat.StabilityDamage: return player.StabilityDamage;
                case PartStat.MaxHp: return player.MaxHp;
                case PartStat.Defense: return player.Defense;
                case PartStat.MoveRange: return player.MoveRange;
                default: return player.MaxStability;
            }
        }

        private static int Minimum(PartStat stat)
        {
            switch (stat)
            {
                case PartStat.Attack:
                case PartStat.AttackRange:
                case PartStat.MaxHp:
                case PartStat.MoveRange:
                case PartStat.MaxStability:
                    return 1;
                case PartStat.AttackPercent:
                case PartStat.MaxHpPercent:
                case PartStat.DefensePercent:
                    return MinimumPercent;
                default:
                    return 0;
            }
        }
    }

    /// <summary>제단을 연다 (제단 창을 띄우기 전에 거래를 고른다). 행동 · 틱을 쓰지 않는다.</summary>
    public sealed class OpenAltarCommand : ICommand
    {
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public OpenAltarCommand(CombatSettings settings)
        {
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => Altar.Open(world, settings, events);
    }

    /// <summary>제단 거래 하나를 받는다.</summary>
    public sealed class AltarTradeCommand : ICommand
    {
        private readonly CombatSettings settings;
        private readonly int index;

        public int CostTicks => 0;

        public AltarTradeCommand(CombatSettings settings, int index)
        {
            this.settings = settings;
            this.index = index;
        }

        public bool TryApply(World world, List<GameEvent> events) => Altar.Accept(world, settings, index, events);
    }

    /// <summary>제단을 열었다 (거래 Count개).</summary>
    public sealed class AltarOpened : GameEvent
    {
        public int Count { get; }

        public AltarOpened(int count)
        {
            Count = count;
        }
    }

    /// <summary>제단 거래를 받았다. 화면은 지형을 다시 그린다.</summary>
    public sealed class AltarTraded : GameEvent
    {
        public PartStat CostStat { get; }
        public int CostAmount { get; }
        public PartStat BoonStat { get; }
        public int BoonAmount { get; }

        public AltarTraded(PartStat costStat, int costAmount, PartStat boonStat, int boonAmount)
        {
            CostStat = costStat;
            CostAmount = costAmount;
            BoonStat = boonStat;
            BoonAmount = boonAmount;
        }
    }
}
