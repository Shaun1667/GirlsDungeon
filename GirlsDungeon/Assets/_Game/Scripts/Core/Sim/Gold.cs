using System.Collections.Generic;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>골드를 얻은 곳 (화면 기록용).</summary>
    public enum GoldSource
    {
        Kill = 0,
        SupplyCrate = 1,
        Treasure = 2,
        /// <summary>상인에게 물건을 팔았다 (3-15c).</summary>
        Sale = 3,
        Debug = 4,
        /// <summary>바닥에 떨어진 골드를 주웠다 (3-16w).</summary>
        Pickup = 5,
    }

    /// <summary>
    /// 사르디스 골드의 한 곳 (3-15a, 결정 2026-11-14 사용자 — 상점 화폐, 정식 이름 "사르디스 골드"). 값은 World.Gold, 층을 넘어 유지.
    /// 얻는 곳: 적을 쓰러뜨리면 확률로 바닥 골드(GoldDropPercent · GoldDropMin ~ Max — 커맨드마다 Simulation이 CheckKills를 부름, 주우면 Loot.PickUp,
    /// 결정 2026-11-19 사용자: 바로 주던 5를 바닥 골드로 바꿈) · 보급 상자(CrateGold) · 보물 상자(TreasureGold) · 상인에게 팔기.
    /// 쓰는 곳: 상점 (Shop.Buy). 난수는 바닥 골드만 RandomPurpose.GoldDrop (BALANCE 31번).
    /// </summary>
    public static class Gold
    {
        /// <summary>
        /// 이번 커맨드에서 쓰러진 적마다 굴려 쓰러진 자리 근처 빈 칸에 골드를 떨어뜨린다 (어떤 공격이든 — 사격 · 스킬 · 낙하 · 디버그 처치).
        /// 낭떠러지에 떨어진 적은 가장 가까운 바닥에 (Loot.FreeCellNear).
        /// </summary>
        public static void CheckKills(World world, CombatSettings settings, List<GameEvent> events)
        {
            if (world.Player == null) return;
            var deaths = new List<UnitDied>();
            foreach (var gameEvent in events)
                if (gameEvent is UnitDied died && died.Kind != UnitKind.Player) deaths.Add(died);
            if (deaths.Count == 0) return;

            var random = world.Random.Get(RandomPurpose.GoldDrop);
            foreach (var died in deaths)
            {
                if (random.Range(0, 100) >= settings.GoldDropPercent) continue;
                int amount = random.Range(settings.GoldDropMin, settings.GoldDropMax + 1);
                Loot.DropGold(world, amount, died.Cell, events);
            }
        }

        public static void Gain(World world, int amount, GoldSource source, List<GameEvent> events)
        {
            if (amount <= 0) return;
            world.Gold += amount;
            events.Add(new GoldGained(amount, world.Gold, source));
        }
    }

    /// <summary>골드를 얻었다. Total = 지금 가진 골드.</summary>
    public sealed class GoldGained : GameEvent
    {
        public int Amount { get; }
        public int Total { get; }
        public GoldSource Source { get; }

        public GoldGained(int amount, int total, GoldSource source)
        {
            Amount = amount;
            Total = total;
            Source = source;
        }
    }

    /// <summary>디버그 전용: 골드를 더한다.</summary>
    public sealed class DebugAddGoldCommand : ICommand
    {
        private readonly int amount;

        public int CostTicks => 0;

        public DebugAddGoldCommand(int amount)
        {
            this.amount = amount;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (amount <= 0) return false;
            Gold.Gain(world, amount, GoldSource.Debug, events);
            return true;
        }
    }
}
