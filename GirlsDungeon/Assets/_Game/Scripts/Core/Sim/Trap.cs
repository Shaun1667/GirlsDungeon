using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 함정 방의 적 (3-14j — 열린 특수 방, BALANCE 29번). 방 모양(낭떠러지 · 낮은 엄폐 · 가운데 보물 상자)은 FloorDecor.TryMakeTrapRoom.
    /// - 배치 `Spawn`: 층의 일반 적 · 결투장 다음(EnemySpawner가 부름), 배치 난수. 사격병 TrapEnemyCount명을 입구에서 먼 칸에 놓는다.
    ///   처음부터 감시 중(Overwatching)이고 경계하지 않는다 — 방 안이 보이면 전투가 시작되고, 첫 차례에 감시 칸으로 걸으면 반응 사격을 받는다.
    ///   능력치는 일반 적과 같은 층 강화. 보상은 보물 상자 그대로(Loot.OpenTreasure).
    /// </summary>
    public static class Trap
    {
        /// <summary>입구에서 가장 먼 칸들 중에서 고르는 후보 수 (적 1명당).</summary>
        private const int FarChoicesPerEnemy = 3;

        public static void Spawn(World world, CombatSettings settings, IRandom random)
        {
            var map = world.Map;
            int statPercent = 100 + world.FloorIndex * settings.EnemyStatPercentPerFloor;
            for (int index = 0; index < map.Rooms.Count; index++)
            {
                if (map.RoomKind(index) != SpecialRoomKind.Trap) continue;
                var room = map.Rooms[index];
                var cells = FarCells(world, room);
                int choices = Math.Min(cells.Count, settings.TrapEnemyCount * FarChoicesPerEnemy);
                cells.RemoveRange(choices, cells.Count - choices);
                for (int n = 0; n < settings.TrapEnemyCount && cells.Count > 0; n++)
                {
                    int pick = random.Range(0, cells.Count);
                    var enemy = world.AddUnit(settings.StatsFor(UnitKind.Rifleman), cells[pick]);
                    Weakness.Assign(world, enemy);
                    enemy.Guarding = false;
                    enemy.Overwatching = true;
                    if (statPercent != 100)
                    {
                        enemy.MaxHp = EnemySpawner.Scale(enemy.MaxHp, statPercent);
                        enemy.Hp = enemy.MaxHp;
                        enemy.Attack = EnemySpawner.Scale(enemy.Attack, statPercent);
                    }
                    cells.RemoveAt(pick);
                }
            }
        }

        /// <summary>방 안 설 수 있는 빈 칸을 입구에서 먼 순서로 (같으면 행 · 열 순서). 입구가 없으면 방 가운데에서 먼 순서.</summary>
        private static List<GridPos> FarCells(World world, RoomRect room)
        {
            var map = world.Map;
            var entrances = FloorDecor.Entrances(map, room);
            if (entrances.Count == 0) entrances.Add(room.Center);
            var cells = new List<GridPos>();
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (map.IsWalkable(cell) && !world.IsOccupied(cell) && cell != map.StairsCell) cells.Add(cell);
                }
            cells.Sort((a, b) =>
            {
                int da = Distance(a, entrances), db = Distance(b, entrances);
                if (da != db) return db.CompareTo(da);
                return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });
            return cells;
        }

        private static int Distance(GridPos cell, List<GridPos> targets)
        {
            int best = int.MaxValue;
            foreach (var t in targets) best = Math.Min(best, Math.Max(Math.Abs(cell.X - t.X), Math.Abs(cell.Y - t.Y)));
            return best;
        }
    }
}
