using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>도박 상자를 연 결과 (BALANCE 30번). 저장 · 데이터에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum GambleOutcome : byte
    {
        /// <summary>대박: 든 무기용 GambleJackpotGrade 파츠.</summary>
        Jackpot = 0,
        /// <summary>작은 상: 구급상자.</summary>
        Small = 1,
        /// <summary>빈 상자.</summary>
        Empty = 2,
        /// <summary>매복: 적 GambleAmbushEnemies명이 상자 근처에 나타난다 (경계하지 않음 — 보이면 바로 전투).</summary>
        Ambush = 3,
    }

    /// <summary>
    /// 도박실의 한 곳 (3-14k — 열린 특수 방). 방에 도박 상자(TileType.GambleChest) 여러 개, 그중 하나만 열 수 있다.
    /// 여는 법은 보급 상자와 같다 (Loot.OpenCrate → Open). 열면 그 방의 다른 도박 상자는 모두 닫혀 버린다(GambleChestOpened).
    /// 결과는 열 때 아이템 난수로 굴린다 (대박 · 작은 상 · 빈 상자 · 매복 확률, 나머지가 매복). 매복 적의 종류는 적 종류 난수, 능력치는 층 강화 그대로.
    /// </summary>
    public static class Gamble
    {
        public static bool Open(World world, CombatSettings settings, GridPos cell, List<GameEvent> events)
        {
            var map = world.Map;
            if (!map.InBounds(cell) || map.Get(cell) != TileType.GambleChest) return false;

            SealRoom(map, cell);
            var random = world.Random.Get(RandomPurpose.Item);
            var outcome = Roll(settings, random.Range(0, 100));
            events.Add(new GambleOpened(cell, outcome));
            switch (outcome)
            {
                case GambleOutcome.Jackpot:
                {
                    var part = Parts.GenerateWithGrade(settings, world.PlayerWeapon, settings.GambleJackpotGrade, random);
                    var partCell = Loot.FreeCellNear(world, cell) ?? world.Player.Position;
                    var item = world.AddPartItem(part, partCell);
                    events.Add(new ItemDropped(item.Id, item.Kind, item.Weapon, item.Position, DropSource.SupplyCrate));
                    break;
                }
                case GambleOutcome.Small:
                {
                    var itemCell = Loot.FreeCellNear(world, cell) ?? world.Player.Position;
                    var item = world.AddItem(ItemKind.Medkit, default, itemCell);
                    events.Add(new ItemDropped(item.Id, item.Kind, item.Weapon, item.Position, DropSource.SupplyCrate));
                    break;
                }
                case GambleOutcome.Ambush:
                    SpawnAmbush(world, settings, cell);
                    break;
            }
            return true;
        }

        /// <summary>굴림 값(0 ~ 99) → 결과. 대박 · 작은 상 · 빈 상자 확률을 차례로 쌓고, 나머지가 매복.</summary>
        public static GambleOutcome Roll(CombatSettings settings, int roll)
        {
            if (roll < settings.GambleJackpotPercent) return GambleOutcome.Jackpot;
            roll -= settings.GambleJackpotPercent;
            if (roll < settings.GambleSmallPercent) return GambleOutcome.Small;
            roll -= settings.GambleSmallPercent;
            if (roll < settings.GambleEmptyPercent) return GambleOutcome.Empty;
            return GambleOutcome.Ambush;
        }

        /// <summary>열린 상자가 있는 방의 모든 도박 상자를 닫힌(못 여는) 상태로 바꾼다. 방 밖이면 그 상자만.</summary>
        private static void SealRoom(FloorMap map, GridPos cell)
        {
            int index = map.RoomIndexAt(cell);
            if (index < 0)
            {
                map.SetTile(cell, TileType.GambleChestOpened);
                return;
            }
            var room = map.Rooms[index];
            for (int y = room.Y; y < room.Y + room.Height; y++)
                for (int x = room.X; x < room.X + room.Width; x++)
                {
                    var c = new GridPos(x, y);
                    if (map.Get(c) == TileType.GambleChest) map.SetTile(c, TileType.GambleChestOpened);
                }
        }

        /// <summary>매복 적을 상자에서 가까운 빈 칸(플레이어 바로 옆은 빼고, 칸 순서 고정)에 놓는다.</summary>
        private static void SpawnAmbush(World world, CombatSettings settings, GridPos chest)
        {
            var map = world.Map;
            var player = world.Player;
            var cells = new List<GridPos>();
            for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                {
                    var c = new GridPos(chest.X + dx, chest.Y + dy);
                    if (!map.IsWalkable(c) || world.IsOccupied(c) || c == map.StairsCell) continue;
                    if (player != null && Math.Max(Math.Abs(c.X - player.Position.X), Math.Abs(c.Y - player.Position.Y)) <= 1) continue;
                    cells.Add(c);
                }
            cells.Sort((a, b) =>
            {
                int da = Math.Max(Math.Abs(a.X - chest.X), Math.Abs(a.Y - chest.Y));
                int db = Math.Max(Math.Abs(b.X - chest.X), Math.Abs(b.Y - chest.Y));
                if (da != db) return da.CompareTo(db);
                return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
            });

            int floor = world.FloorIndex;
            int assaultPercent = settings.AssaultPercent + floor * settings.AssaultPercentPerFloor;
            int statPercent = 100 + floor * settings.EnemyStatPercentPerFloor;
            var kindRandom = world.Random.Get(RandomPurpose.EnemyKind);
            for (int n = 0; n < settings.GambleAmbushEnemies && n < cells.Count; n++)
            {
                var kind = kindRandom.Range(0, 100) < assaultPercent ? UnitKind.Assault : UnitKind.Rifleman;
                var enemy = world.AddUnit(settings.StatsFor(kind), cells[n]);
                enemy.Guarding = false;
                if (statPercent != 100)
                {
                    enemy.MaxHp = EnemySpawner.Scale(enemy.MaxHp, statPercent);
                    enemy.Hp = enemy.MaxHp;
                    enemy.Attack = EnemySpawner.Scale(enemy.Attack, statPercent);
                }
            }
        }
    }

    /// <summary>도박 상자를 열었다 (Cell, 결과). 화면은 지형을 다시 그리고 기록 줄을 남긴다.</summary>
    public sealed class GambleOpened : GameEvent
    {
        public GridPos Cell { get; }
        public GambleOutcome Outcome { get; }

        public GambleOpened(GridPos cell, GambleOutcome outcome)
        {
            Cell = cell;
            Outcome = outcome;
        }
    }
}
