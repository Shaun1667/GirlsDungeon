using System.Collections.Generic;

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
    }

    /// <summary>
    /// 사르디스 골드의 한 곳 (3-15a, 결정 2026-11-14 사용자 — 상점 화폐, 정식 이름 "사르디스 골드"). 값은 World.Gold, 층을 넘어 유지.
    /// 얻는 곳: 적을 쓰러뜨림(GoldPerKill — 커맨드마다 Simulation이 CheckKills를 부름) · 보급 상자(CrateGold) · 보물 상자(TreasureGold) · 상인에게 팔기.
    /// 쓰는 곳: 상점 (Shop.Buy). 난수를 쓰지 않는다 (BALANCE 31번).
    /// </summary>
    public static class Gold
    {
        /// <summary>이번 커맨드에서 쓰러진 적마다 골드를 준다 (어떤 공격이든 — 사격 · 스킬 · 낙하 · 디버그 처치).</summary>
        public static void CheckKills(World world, CombatSettings settings, List<GameEvent> events)
        {
            int kills = 0;
            foreach (var gameEvent in events)
                if (gameEvent is UnitDied died && died.Kind != UnitKind.Player) kills++;
            if (kills > 0) Gain(world, kills * settings.GoldPerKill, GoldSource.Kill, events);
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
