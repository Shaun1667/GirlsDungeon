using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>기록실 예고 (다음 층). LastFloor면 다음 층이 없다 (이 층 계단 = 판 승리).</summary>
    public sealed class ArchiveForecast
    {
        public bool LastFloor;
        /// <summary>다음 층의 특수 방 종류 (방 번호 순서).</summary>
        public readonly List<SpecialRoomKind> Rooms = new List<SpecialRoomKind>();
        /// <summary>다음 층의 적 수 (결투장 · 함정 방 적 포함).</summary>
        public int Enemies;
    }

    /// <summary>
    /// 기록실의 한 곳 (3-14l — 열린 특수 방). 기록 단말(TileType.ArchiveTerminal)을 쓰면(보급 상자와 같은 길 — Loot.OpenCrate → Use)
    /// 이 층에서 기록을 읽은 것으로 표시하고(World.ArchiveRead), 화면은 Forecast로 다음 층의 특수 방 · 적 수를 보여 준다.
    /// 예고 = 실제: 세계를 스냅샷으로 복제해 다음 층을 실제로 만들어 본 결과다. 층을 만드는 난수(Map · Decor · Floor · Spawn · EnemyKind)는
    /// 층을 진행하는 동안 쓰지 않으므로(3-14l에서 나눔), 그 사이에 무엇을 해도 실제로 내려가면 같은 층이 나온다.
    /// </summary>
    public static class Archive
    {
        public static bool Use(World world, GridPos cell, List<GameEvent> events)
        {
            var map = world.Map;
            if (!map.InBounds(cell) || map.Get(cell) != TileType.ArchiveTerminal) return false;
            map.SetTile(cell, TileType.ArchiveTerminalUsed);
            world.ArchiveRead = true;
            events.Add(new ArchiveUsed(cell));
            return true;
        }

        /// <summary>다음 층 예고. 세계는 바꾸지 않는다 (복제본에서 NextFloor).</summary>
        public static ArchiveForecast Forecast(World world, MapSettings mapSettings, CombatSettings combatSettings)
        {
            var forecast = new ArchiveForecast();
            if (world.FloorIndex + 1 >= combatSettings.FloorCount)
            {
                forecast.LastFloor = true;
                return forecast;
            }
            var copy = WorldSnapshot.Decode(WorldSnapshot.Encode(world));
            RunFactory.NextFloor(copy, mapSettings, combatSettings, null);
            for (int i = 0; i < copy.Map.Rooms.Count; i++)
            {
                var kind = copy.Map.RoomKind(i);
                if (kind != SpecialRoomKind.None) forecast.Rooms.Add(kind);
            }
            foreach (var unit in copy.Units)
                if (!unit.IsPlayer) forecast.Enemies++;
            return forecast;
        }
    }

    /// <summary>기록 단말을 썼다. 화면은 지형을 다시 그리고 예고를 보여 준다.</summary>
    public sealed class ArchiveUsed : GameEvent
    {
        public GridPos Cell { get; }

        public ArchiveUsed(GridPos cell)
        {
            Cell = cell;
        }
    }
}
