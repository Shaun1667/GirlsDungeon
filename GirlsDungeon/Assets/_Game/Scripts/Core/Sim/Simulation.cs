using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 화면이 사건을 받는 곳. Simulation은 커맨드마다 한꺼번에 내보내고,
    /// 화면 쪽 재생기(Game.EventPlayer)는 같은 사건을 시간을 두고 하나씩 다시 내보낸다.
    /// </summary>
    public interface IEventSource
    {
        World World { get; }
        event Action<IReadOnlyList<GameEvent>> EventsPublished;
    }

    /// <summary>
    /// 규칙 진행의 한 곳. 모든 변화는 커맨드와 틱으로만 일어나고, 사건은 끝에 한꺼번에 나간다.
    /// 틱 순서 (시스템이 생기면 이 순서에 끼운다):
    /// ① 행동 ② 반응 ③ 지형 ④ 상태 ⑤ 재생 · 기타 ⑥ 사망 · 승패 · 시야(전투 전환 CombatFlow) ⑦ 사건 발행
    /// </summary>
    public sealed class Simulation : IEventSource
    {
        private readonly List<GameEvent> pending = new List<GameEvent>();
        private readonly CombatSettings settings;
        private readonly MapSettings mapSettings;

        public World World { get; }

        /// <summary>한 커맨드가 끝날 때 쌓인 사건을 내보낸다.</summary>
        public event Action<IReadOnlyList<GameEvent>> EventsPublished;

        /// <param name="settings">전투 수치. 없으면 경계 칸 확인(Alert.CheckZones)을 건너뛴다 (맵 · 이동만 보는 테스트용).</param>
        /// <param name="mapSettings">맵 수치. 있으면(전투 수치도 있을 때) 계단을 밟을 때 다음 층으로 내려간다 (FloorFlow).</param>
        public Simulation(World world, CombatSettings settings = null, MapSettings mapSettings = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            this.settings = settings;
            this.mapSettings = mapSettings;
        }

        /// <summary>커맨드를 적용한다. 규칙에 맞지 않으면 틱이 흐르지 않고 false.</summary>
        public bool Execute(ICommand command)
        {
            if (!command.TryApply(World, pending)) return false;

            for (int i = 0; i < command.CostTicks; i++) RunTick();
            Shop.CheckHostility(World, pending);
            CombatFlow.AfterAction(World, settings, pending);
            if (settings != null) Gold.CheckKills(World, settings, pending);
            if (settings != null) Loot.CheckPickup(World, settings, pending, command is MoveCommand step && step.PassingThrough);
            if (settings != null && mapSettings != null) FloorFlow.CheckStairs(World, mapSettings, settings, pending);
            Publish();
            return true;
        }

        private void RunTick()
        {
            World.Tick++;
            pending.Add(new TickAdvanced(World.Tick));
        }

        private void Publish()
        {
            var events = pending.ToArray();
            pending.Clear();
            EventsPublished?.Invoke(events);
        }
    }

    /// <summary>새 판 만들기의 한 곳. 같은 시드와 수치면 언제나 같은 세계가 나온다.</summary>
    public static class RunFactory
    {
        /// <summary>캐릭터를 고르지 않은 판: 유닛 표의 플레이어 줄과 기본 시작 무기 (테스트 · 기본).</summary>
        public static World NewRun(int seed, MapSettings mapSettings, CombatSettings combatSettings)
        {
            return NewRun(seed, mapSettings, combatSettings, CharacterKind.None);
        }

        /// <summary>
        /// 고른 캐릭터로 새 판을 만든다. 캐릭터 줄(BALANCE 13번)이 유닛 표 플레이어 줄의 체력 · 안정지수 · 시야 · 방어 · 이동과 시작 무기를 덮어쓴다.
        /// 캐릭터는 지형 · 배치 난수를 쓰지 않으므로 같은 시드면 어느 캐릭터든 같은 맵이 나온다 (적 배치는 시작 칸 시야에 따라 달라질 수 있다).
        /// </summary>
        public static World NewRun(int seed, MapSettings mapSettings, CombatSettings combatSettings, CharacterKind character)
        {
            return Create(seed, mapSettings, combatSettings, character, false);
        }

        /// <summary>
        /// 특수 방 모음 판 (테스트 씬 — SpecialRoomShowcase): 첫 층이 모든 특수 방 종류를 하나씩 모은 층이다. 일반 적은 없고(결투장 · 함정 방은 자기 적),
        /// 잠긴 방 수만큼 열쇠를 들고 시작한다. 계단을 밟으면 다음 층부터는 보통 층.
        /// </summary>
        public static World NewShowcaseRun(int seed, MapSettings mapSettings, CombatSettings combatSettings, CharacterKind character)
        {
            return Create(seed, mapSettings, combatSettings, character, true);
        }

        private static World Create(int seed, MapSettings mapSettings, CombatSettings combatSettings, CharacterKind character, bool showcase)
        {
            combatSettings.Validate();
            var characterStats = combatSettings.TryCharacter(character);
            if (character != CharacterKind.None && characterStats == null)
                throw new ArgumentException($"No stats for character {character}.");

            var world = new World(seed, new RandomStreams(seed));
            BuildFloor(world, mapSettings, showcase);
            var playerStats = combatSettings.StatsFor(UnitKind.Player);
            var player = world.AddUnit(playerStats, world.Map.StartCell);
            world.PlayerId = player.Id;
            world.PlayerCharacter = characterStats != null ? character : CharacterKind.None;
            world.PlayerWeapon = combatSettings.StartWeapon;
            if (characterStats != null)
            {
                player.MaxHp = player.Hp = characterStats.MaxHp;
                player.MaxStability = player.Stability = characterStats.MaxStability;
                player.VisionRange = characterStats.VisionRange;
                player.Defense = characterStats.Defense;
                world.PlayerWeapon = characterStats.StartWeapon;
            }
            Loadout.Apply(world, combatSettings);
            if (showcase)
            {
                var spawn = world.Random.Get(RandomPurpose.Spawn);
                Arena.Spawn(world, combatSettings, spawn);
                Trap.Spawn(world, combatSettings, spawn);
                world.Keys = CountLockedRooms(world.Map);
            }
            else EnemySpawner.Spawn(world, combatSettings, world.Random.Get(RandomPurpose.Spawn));
            Shop.Stock(world, combatSettings);
            VisionSystem.Update(world, null);
            return world;
        }

        /// <summary>
        /// 다음 층으로 내려간다. 지금 층은 떠날 때 그대로 보관하고(SaveFloor — 3-16u), 다음 층에 가 본 적이 있으면 그대로 되살린다.
        /// 처음 가는 층이면 새 지형 · 새 적을 만든다 — 지형 · 배치 난수는 판의 난수를 이어 쓰므로 같은 시드 · 같은 진행이면 같은 층이 나온다
        /// (되살리는 층은 난수를 쓰지 않는다). 플레이어는 같은 유닛(체력 · 능력치 · 무기 그대로)으로 시작 칸(2층부터 올라가는 계단)에 선다.
        /// </summary>
        public static void NextFloor(World world, MapSettings mapSettings, CombatSettings combatSettings, List<GameEvent> events)
        {
            var player = world.Player;
            SaveFloor(world);
            world.FloorIndex++;
            var stored = StoredAt(world, world.FloorIndex);
            if (stored != null)
            {
                RestoreFloor(world, stored);
                player.Position = OpenCellAt(world, world.Map.StartCell);
            }
            else
            {
                BuildFloor(world, mapSettings);
                // 2층부터 시작 칸이 올라가는 계단 (꾸미기 · 배치 뒤 — 지형 모양과 난수는 그대로).
                world.Map.SetTile(world.Map.StartCell, TileType.StairsUp);
                world.Explored = null;
                player.Position = world.Map.StartCell;
            }
            ArriveOnFloor(world);
            if (stored == null)
            {
                EnemySpawner.Spawn(world, combatSettings, world.Random.Get(RandomPurpose.Spawn));
                Shop.Stock(world, combatSettings);
            }
            events?.Add(new FloorChanged(world.FloorIndex, player.Position));
            VisionSystem.Update(world, events);
        }

        /// <summary>
        /// 위층으로 올라간다 (3-16u, 올라가는 계단): 지금 층을 보관하고 위층을 떠날 때 그대로 되살린다. 플레이어는 위층 내려가는 계단 옆 빈 칸에 선다.
        /// 1층이거나 위층 기록이 없으면 false (아무것도 바꾸지 않음).
        /// </summary>
        public static bool PreviousFloor(World world, List<GameEvent> events)
        {
            if (world.FloorIndex <= 0) return false;
            var stored = StoredAt(world, world.FloorIndex - 1);
            if (stored == null) return false;
            var player = world.Player;
            SaveFloor(world);
            world.FloorIndex--;
            RestoreFloor(world, stored);
            player.Position = CellBeside(world, world.Map.StairsCell);
            ArriveOnFloor(world);
            events?.Add(new FloorChanged(world.FloorIndex, player.Position));
            VisionSystem.Update(world, events);
            return true;
        }

        /// <summary>그 층의 보관 상태, 없으면 null.</summary>
        public static StoredFloor StoredAt(World world, int floorIndex)
            => floorIndex >= 0 && floorIndex < world.StoredFloors.Count ? world.StoredFloors[floorIndex] : null;

        /// <summary>지금 층(지형 · 플레이어가 아닌 유닛 · 바닥 물건 · 본 칸 · 층별 기록)을 StoredFloors[FloorIndex]로 옮긴다.</summary>
        private static void SaveFloor(World world)
        {
            var stored = new StoredFloor
            {
                Map = world.Map,
                Explored = world.Explored,
                FloorKeyGiven = world.FloorKeyGiven,
                EnemiesRevealed = world.EnemiesRevealed,
                ExchangeSold = world.ExchangeSold,
                ArchiveRead = world.ArchiveRead,
                ShopHostile = world.ShopHostile,
            };
            foreach (var unit in world.Units) if (!unit.IsPlayer) stored.Units.Add(unit);
            world.Units.RemoveAll(unit => !unit.IsPlayer);
            stored.Items.AddRange(world.Items);
            world.Items.Clear();
            stored.GalleryOffers.AddRange(world.GalleryOffers);
            stored.AltarOffers.AddRange(world.AltarOffers);
            stored.ArenaEnemies.AddRange(world.ArenaEnemies);
            while (world.StoredFloors.Count <= world.FloorIndex) world.StoredFloors.Add(null);
            world.StoredFloors[world.FloorIndex] = stored;
        }

        /// <summary>보관한 층을 지금 층으로 되살린다 (FloorIndex는 부르는 쪽이 맞춘다). 보관 자리는 비운다.</summary>
        private static void RestoreFloor(World world, StoredFloor stored)
        {
            world.StoredFloors[world.FloorIndex] = null;
            world.Map = stored.Map;
            world.Explored = stored.Explored;
            world.Units.AddRange(stored.Units);
            world.Units.Sort((a, b) => a.Id.CompareTo(b.Id));
            world.Items.Clear();
            world.Items.AddRange(stored.Items);
            world.FloorKeyGiven = stored.FloorKeyGiven;
            world.EnemiesRevealed = stored.EnemiesRevealed;
            world.ExchangeSold = stored.ExchangeSold;
            world.GalleryOffers.Clear();
            world.GalleryOffers.AddRange(stored.GalleryOffers);
            world.AltarOffers.Clear();
            world.AltarOffers.AddRange(stored.AltarOffers);
            world.ArenaEnemies.Clear();
            world.ArenaEnemies.AddRange(stored.ArenaEnemies);
            world.ArchiveRead = stored.ArchiveRead;
            world.ShopHostile = stored.ShopHostile;
        }

        /// <summary>층에 들어설 때 늘 새로 시작하는 탐험 상태 (전투 · 차례 · 재사용 대기 · 상점 주인 스킬).</summary>
        private static void ArriveOnFloor(World world)
        {
            world.Combatants.Clear();
            world.Player.Overwatching = false;
            world.Mode = WorldMode.Exploration;
            world.Turn = TurnSide.Player;
            world.Round = 0;
            world.PlayerMoveUsed = 0;
            world.PlayerActed = false;
            world.PlayerShots = 0;
            world.GrenadeCooldown = 0;
            world.PushCooldown = 0;
            world.SonicCooldown = 0;
            world.FrostCooldown = 0;
            KeeperSkills.Reset(world);
            FrostGuard.Clear(world);
            Remold.OnCombatEnded(world);
        }

        /// <summary>그 칸이 비었으면 그 칸, 아니면 가장 가까운 빈 바닥.</summary>
        private static GridPos OpenCellAt(World world, GridPos cell)
            => world.Map.IsWalkable(cell) && !world.IsOccupied(cell) ? cell : Push.NearestOpenFloor(world, cell);

        /// <summary>계단 옆 빈 칸 (이웃 순서 고정 — 같은 상태면 같은 칸). 옆이 모두 막혔으면 가장 가까운 빈 바닥.</summary>
        private static GridPos CellBeside(World world, GridPos stairs)
        {
            var map = world.Map;
            foreach (var step in BesideSteps)
            {
                var cell = new GridPos(stairs.X + step.X, stairs.Y + step.Y);
                if (map.InBounds(cell) && map.Get(cell) == TileType.Floor && !world.IsOccupied(cell) && world.ItemAt(cell) == null) return cell;
            }
            return OpenCellAt(world, stairs);
        }

        private static readonly GridPos[] BesideSteps =
        {
            new GridPos(0, -1), new GridPos(0, 1), new GridPos(-1, 0), new GridPos(1, 0),
            new GridPos(-1, -1), new GridPos(1, -1), new GridPos(-1, 1), new GridPos(1, 1),
        };

        private static int CountLockedRooms(FloorMap map)
        {
            int count = 0;
            foreach (var room in map.Rooms)
            {
                foreach (var cell in FloorDecor.Entrances(map, room))
                    if (map.IsLockedDoor(cell)) { count++; break; }
            }
            return count;
        }

        private static void BuildFloor(World world, MapSettings mapSettings, bool showcase = false)
        {
            if (showcase)
            {
                world.Map = SpecialRoomShowcase.Build(mapSettings, world.Random.Get(RandomPurpose.Decor), world.Random.Get(RandomPurpose.Floor));
                ResetFloorState(world);
                return;
            }
            world.Map = DungeonGenerator.Generate(mapSettings, world.Random.Get(RandomPurpose.Map));
            ResetFloorState(world);
            FloorDecor.Apply(world.Map, mapSettings, world.Random.Get(RandomPurpose.Decor));
            // 보급 상자 · 특수 방(작업방 포함)은 층 만들기 난수로 (3-14l — 전에는 아이템 · 작업 난수). 층을 진행하는 동안 쓰지 않는 난수라
            // 기록실 예고가 실제 다음 층과 같다 (Map · Decor · Floor · Spawn · EnemyKind는 층을 만들 때만 쓴다).
            var floorRandom = world.Random.Get(RandomPurpose.Floor);
            FloorDecor.PlaceSupplyCrates(world.Map, mapSettings, floorRandom);
            SpecialRoomLayout.Place(world.Map, mapSettings, floorRandom, world.FloorIndex);
        }

        /// <summary>층마다 새로 시작하는 상태 (새 층을 만들 때).</summary>
        private static void ResetFloorState(World world)
        {
            world.FloorKeyGiven = false;
            world.EnemiesRevealed = false;
            world.ExchangeSold = 0;
            world.GalleryOffers.Clear();
            world.AltarOffers.Clear();
            world.ArenaEnemies.Clear();
            world.ArchiveRead = false;
            world.ShopHostile = false;
            KeeperSkills.Reset(world);
        }
    }

    /// <summary>
    /// 층에 적을 놓는 한 곳. 적마다 AssaultPercent 확률로 돌격병, 나머지는 사격병. 시작 칸에서 일정 거리 밖이고, 시작 칸에서 보이지 않는(Vision — 시작 방 전체 포함) 걸을 수 있는 칸에만 놓는다.
    /// 후보 칸은 행 순서로 모은 뒤 배치 난수로 고르므로 같은 시드면 같은 자리가 나온다.
    /// </summary>
    public static class EnemySpawner
    {
        public static void Spawn(World world, CombatSettings settings, IRandom random)
        {
            var map = world.Map;
            var start = map.StartCell;
            // 플레이어의 실제 시야 (캐릭터마다 다르다). 플레이어가 아직 없으면 유닛 표 값.
            int playerVision = world.Player?.VisionRange ?? settings.StatsFor(UnitKind.Player).VisionRange;
            var visibleFromStart = Vision.Compute(map, start, playerVision);

            var candidates = new List<GridPos>();
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    if (!map.IsWalkable(cell) || cell == map.StairsCell || world.IsOccupied(cell)) continue;
                    if (Math.Max(Math.Abs(cell.X - start.X), Math.Abs(cell.Y - start.Y)) < settings.EnemyMinDistanceFromStart) continue;
                    if (Vision.IsSet(visibleFromStart, map, cell)) continue;
                    // 특수 방에는 일반 적이 나오지 않는다 (3-4c · 3-14).
                    if (FloorDecor.InSpecialRoom(map, cell)) continue;
                    candidates.Add(cell);
                }
            }

            // 층이 깊을수록 적이 많고, 돌격병이 늘고, 강해지고, 바로 덤빈다 (결정 2026-11-10, 사용자 · BALANCE 11번).
            int floor = world.FloorIndex;
            int extra = floor * settings.EnemiesPerFloorStep;
            int assaultPercent = settings.AssaultPercent + floor * settings.AssaultPercentPerFloor;
            int guardPercent = Math.Max(0, settings.GuardPercent - floor * settings.GuardPercentDropPerFloor);
            int statPercent = 100 + floor * settings.EnemyStatPercentPerFloor;

            int count = random.Range(settings.MinEnemiesPerFloor + extra, settings.MaxEnemiesPerFloor + extra + 1);
            // 종류는 적 종류 난수로 따로 굴린다 — 종류 비율을 바꿔도 배치 자리 · 경계 굴림은 그대로.
            var kindRandom = world.Random.Get(RandomPurpose.EnemyKind);
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = random.Range(0, candidates.Count);
                var kind = kindRandom.Range(0, 100) < assaultPercent ? UnitKind.Assault : UnitKind.Rifleman;
                var enemy = world.AddUnit(settings.StatsFor(kind), candidates[index]);
                enemy.Guarding = random.Range(0, 100) < guardPercent;
                if (statPercent != 100)
                {
                    enemy.MaxHp = Scale(enemy.MaxHp, statPercent);
                    enemy.Hp = enemy.MaxHp;
                    enemy.Attack = Scale(enemy.Attack, statPercent);
                }
                candidates.RemoveAt(index);
            }

            // 결투장 무리는 일반 적 다음에 (3-14i — 일반 적 배치 굴림은 그대로).
            Arena.Spawn(world, settings, random);
            // 함정 방의 감시병도 따로 (3-14j).
            Trap.Spawn(world, settings, random);
        }

        /// <summary>값 × 비율%, 반올림 (0.5 올림).</summary>
        public static int Scale(int value, int percent) => (value * percent * 2 + 100) / 200;
    }
}
