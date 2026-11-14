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
            if (settings != null) Loot.CheckPickup(World, settings, pending);
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
        /// 다음 층으로 내려간다: 적 · 바닥 물건을 모두 치우고 새 지형 · 새 적을 만든다. 플레이어는 같은 유닛(체력 · 능력치 · 무기 그대로)으로 새 시작 칸에 선다.
        /// 지형 · 배치 난수는 판의 난수를 이어 쓰므로 같은 시드 · 같은 진행이면 같은 층이 나온다.
        /// </summary>
        public static void NextFloor(World world, MapSettings mapSettings, CombatSettings combatSettings, List<GameEvent> events)
        {
            var player = world.Player;
            world.Units.RemoveAll(unit => !unit.IsPlayer);
            world.Combatants.Clear();
            world.Items.Clear();
            world.FloorIndex++;
            BuildFloor(world, mapSettings);

            player.Position = world.Map.StartCell;
            player.Overwatching = false;
            world.Mode = WorldMode.Exploration;
            world.Turn = TurnSide.Player;
            world.Round = 0;
            world.PlayerMoveUsed = 0;
            world.PlayerActed = false;
            world.PlayerShots = 0;
            world.GrenadeCooldown = 0;
            world.PushCooldown = 0;
            world.SonicCooldown = 0;
            world.Explored = null;

            EnemySpawner.Spawn(world, combatSettings, world.Random.Get(RandomPurpose.Spawn));
            Shop.Stock(world, combatSettings);
            events?.Add(new FloorChanged(world.FloorIndex, world.Map.StartCell));
            VisionSystem.Update(world, events);
        }

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
