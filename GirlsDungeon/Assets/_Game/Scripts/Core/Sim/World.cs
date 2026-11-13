using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>유닛 종류. 저장에 정수로 들어가므로 끝에만 추가한다. 능력치는 CombatSettings 표에서 온다.</summary>
    public enum UnitKind : byte
    {
        Player = 0,
        Rifleman = 1,
        /// <summary>돌격병: 바로 옆에서 근접 공격하고 맞은 상대를 1칸 민다.</summary>
        Assault = 2,
    }

    /// <summary>탐험과 턴 전투. 저장에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum WorldMode : byte
    {
        Exploration = 0,
        Combat = 1,
        /// <summary>플레이어가 쓰러졌다. 더 이상 행동할 수 없다.</summary>
        Defeat = 2,
        /// <summary>마지막 층의 계단을 밟았다 — 판 승리. 더 이상 행동할 수 없다.</summary>
        Victory = 3,
    }

    /// <summary>전투 중 지금 움직이는 쪽.</summary>
    public enum TurnSide : byte
    {
        Player = 0,
        Enemies = 1,
    }

    /// <summary>
    /// 맵 위의 유닛. 다른 곳에서는 고유 ID로 참조한다.
    /// 능력치는 만들 때 CombatSettings 표에서 복사한다 — 판 안 성장으로 유닛마다 바뀔 수 있다 (예: 이동 범위).
    /// </summary>
    public sealed class Unit
    {
        public int Id { get; }
        public UnitKind Kind { get; }
        public GridPos Position { get; set; }
        public int MaxHp { get; set; }
        public int Hp { get; set; }
        public int MaxStability { get; set; }
        public int Stability { get; set; }
        public int VisionRange { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int MoveRange { get; set; }
        public int AttackRange { get; set; }
        public int StabilityDamage { get; set; }
        public int CritPercent { get; set; }
        public int CritMultiplierPercent { get; set; }

        /// <summary>공격이 맞으면 상대를 공격자 반대쪽으로 미는 칸 수 (0 = 밀지 않음, 돌격병 1).</summary>
        public int PushOnHit { get; set; }

        /// <summary>경계 상태 (적). 공격받거나 플레이어가 경계 칸에 들어오기 전까지 행동하지 않는다.</summary>
        public bool Guarding { get; set; }

        /// <summary>감시 중. 상대가 사거리 · 사선 안에서 움직이면 한 번 반응 사격한다.</summary>
        public bool Overwatching { get; set; }

        public bool IsPlayer => Kind == UnitKind.Player;

        public Unit(int id, UnitKind kind, GridPos position)
        {
            Id = id;
            Kind = kind;
            Position = position;
        }
    }

    /// <summary>바닥 물건 종류. 저장에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum ItemKind : byte
    {
        /// <summary>무기. 밟으면 비교 창 → 바꾸기 / 두기.</summary>
        Weapon = 0,
        /// <summary>구급상자. 밟으면 바로 회복 (체력이 가득이면 그대로 남는다).</summary>
        Medkit = 1,
        /// <summary>무기 파츠 (3-4). 밟으면 비교 창 → 끼우기 / 두기. Weapon = 맞는 무기 종류.</summary>
        Part = 2,
        /// <summary>열쇠 (3-4d). 밟으면 바로 줍는다 (World.Keys).</summary>
        Key = 3,
    }

    /// <summary>바닥에 놓인 물건. 다른 곳에서는 고유 ID로 참조한다.</summary>
    public sealed class FloorItem
    {
        public int Id { get; }
        public ItemKind Kind { get; }
        /// <summary>무기일 때 무기 종류.</summary>
        public WeaponKind Weapon { get; }
        public GridPos Position { get; }

        /// <summary>파츠일 때 그 파츠.</summary>
        public WeaponPart Part { get; set; }

        /// <summary>무기일 때 그 무기에 끼워져 있는 파츠 4칸 (null = 빈 칸). 무기를 주우면 같이 들린다.</summary>
        public WeaponPart[] Parts { get; } = new WeaponPart[WeaponPart.SlotCount];

        public FloorItem(int id, ItemKind kind, WeaponKind weapon, GridPos position)
        {
            Id = id;
            Kind = kind;
            Weapon = weapon;
            Position = position;
        }
    }

    /// <summary>판의 모든 상태. 저장 · 복원은 이것 하나로 끝나고, 시스템은 상태를 들지 않는다.</summary>
    public sealed class World
    {
        public int Seed { get; }
        public RandomStreams Random { get; }
        public FloorMap Map { get; set; }
        /// <summary>지금 층 (0 = 1층).</summary>
        public int FloorIndex { get; set; }

        /// <summary>이번 판에 쓰러뜨린 적 수 (결과 화면).</summary>
        public int Kills { get; set; }
        public long Tick { get; set; }
        public int NextEntityId { get; set; }
        public int PlayerId { get; set; }

        /// <summary>살아 있는 유닛. ID 오름차순을 유지한다 (추가는 언제나 끝에, 새 ID가 가장 크다).</summary>
        public List<Unit> Units { get; } = new List<Unit>();

        public WorldMode Mode { get; set; }
        public TurnSide Turn { get; set; }
        public int Round { get; set; }

        /// <summary>지금 전투에 들어와 있는 적 ID (오름차순).</summary>
        public List<int> Combatants { get; } = new List<int>();

        /// <summary>이번 플레이어 차례에 걸은 칸 수 (전투 중). 남은 이동 = 이동 범위 − 이 값 (Loadout.MoveLeft).</summary>
        public int PlayerMoveUsed { get; set; }

        /// <summary>이번 플레이어 차례에 한 칸이라도 걸었다 (저격총 규칙이 본다).</summary>
        public bool PlayerMoved => PlayerMoveUsed > 0;

        /// <summary>이번 플레이어 차례에 행동(사격)을 썼다 (전투 중).</summary>
        public bool PlayerActed { get; set; }

        /// <summary>이번 플레이어 차례에 쏜 횟수 (기관단총은 두 번까지). PlayerMoveUsed와 같은 때 0이 된다.</summary>
        public int PlayerShots { get; set; }

        /// <summary>수류탄 스킬을 다시 쓸 수 있을 때까지 남은 라운드 (0 = 지금 사용 가능). 전투가 끝나면 0.</summary>
        public int GrenadeCooldown { get; set; }

        /// <summary>밀치기 스킬을 다시 쓸 수 있을 때까지 남은 라운드 (0 = 지금 사용 가능). 전투가 끝나면 0.</summary>
        public int PushCooldown { get; set; }

        /// <summary>음파 공격 스킬을 다시 쓸 수 있을 때까지 남은 라운드 (0 = 지금 사용 가능). 전투가 끝나면 0.</summary>
        public int SonicCooldown { get; set; }

        /// <summary>한 번이라도 본 칸 (인덱스 = x + y * Width). 지금 보이는 칸은 저장하지 않고 Vision으로 다시 계산한다.</summary>
        public bool[] Explored { get; set; }

        /// <summary>플레이어가 든 무기 (하나만 든다). 능력치는 Loadout.Apply가 유닛에 맞춘다.</summary>
        public WeaponKind PlayerWeapon { get; set; }

        /// <summary>든 무기에 끼운 파츠 4칸 (null = 빈 칸, 3-4). 무기를 바꾸면 내려놓는 무기에 붙어 바닥에 남는다.</summary>
        public WeaponPart[] PlayerParts { get; } = new WeaponPart[WeaponPart.SlotCount];

        /// <summary>
        /// 파츠가 지금 플레이어 몸 능력치에 더해 둔 값 (최대 체력 · 방어 · 최대 안정지수). Loadout.Apply가 파츠 합이 바뀐 만큼만 더하고 빼려고 기억한다.
        /// </summary>
        public int PartAppliedMaxHp { get; set; }
        public int PartAppliedDefense { get; set; }
        public int PartAppliedMaxStability { get; set; }

        /// <summary>예비 파츠 보관함 (어느 무기용이든, 최대 CombatSettings.SpareSlots, 3-4c). 작업대에서 분해 · 작업 · 끼우기.</summary>
        public List<WeaponPart> SpareParts { get; } = new List<WeaponPart>();

        /// <summary>파츠를 분해해 모은 재료 (판 안에서만, 3-4c).</summary>
        public int PartMaterials { get; set; }

        /// <summary>가진 열쇠 (층을 넘어가도 남는다, 3-4d). 잠긴 문 하나(그 방 전체)에 1개.</summary>
        public int Keys { get; set; }

        /// <summary>이 층 교환소에서 이미 판 물건 (ExchangeOffer 비트, 3-14f). 층을 새로 만들 때 0.</summary>
        public byte ExchangeSold { get; set; }

        /// <summary>이 층에서 관측 콘솔을 써서 적 위치가 모두 보인다 (화면 표시만, 3-14e). 층을 새로 만들 때 false.</summary>
        public bool EnemiesRevealed { get; set; }

        /// <summary>이 층에서 보장 열쇠가 이미 나왔다 (첫 보급 상자). 층을 새로 만들 때 false.</summary>
        public bool FloorKeyGiven { get; set; }

        /// <summary>연쇄 가속 세트의 중첩 (스킬을 쓸 때마다 +1, 최대 CombatSettings.SetChainMaxStacks, 3-4b). 전투가 끝나면 0.</summary>
        public int SetChainStacks { get; set; }

        /// <summary>방호 태세 세트로 받는 범위 피해가 주는 남은 차례 (감시를 걸면 채움, 플레이어 차례 시작마다 -1, 3-4b). 전투가 끝나면 0.</summary>
        public int SetBulwarkTurns { get; set; }

        /// <summary>판을 시작할 때 고른 캐릭터 (None = 유닛 표의 플레이어 줄 그대로). 이동 범위 기준값 · 화면 그림 · 이름을 여기서 찾는다.</summary>
        public CharacterKind PlayerCharacter { get; set; }

        /// <summary>이 층 바닥에 놓인 물건. ID 오름차순 (추가는 언제나 끝에). 층을 내려가면 비운다.</summary>
        public List<FloorItem> Items { get; } = new List<FloorItem>();

        public World(int seed, RandomStreams random)
        {
            Seed = seed;
            Random = random;
        }

        public Unit Player => FindUnit(PlayerId);

        public Unit FindUnit(int id)
        {
            foreach (var unit in Units) if (unit.Id == id) return unit;
            return null;
        }

        public Unit UnitAt(GridPos cell)
        {
            foreach (var unit in Units) if (unit.Position == cell) return unit;
            return null;
        }

        public bool IsOccupied(GridPos cell) => UnitAt(cell) != null;

        public FloorItem FindItem(int id)
        {
            foreach (var item in Items) if (item.Id == id) return item;
            return null;
        }

        /// <summary>그 칸에 놓인 첫 물건 (ID가 가장 작은 것). 없으면 null.</summary>
        public FloorItem ItemAt(GridPos cell)
        {
            foreach (var item in Items) if (item.Position == cell) return item;
            return null;
        }

        public FloorItem AddItem(ItemKind kind, WeaponKind weapon, GridPos position)
        {
            var item = new FloorItem(NextEntityId++, kind, weapon, position);
            Items.Add(item);
            return item;
        }

        /// <summary>바닥에 파츠를 놓는다 (새 ID).</summary>
        public FloorItem AddPartItem(WeaponPart part, GridPos position)
        {
            var item = AddItem(ItemKind.Part, part.Weapon, position);
            item.Part = part;
            return item;
        }

        public Unit AddUnit(UnitStats stats, GridPos position)
        {
            var unit = new Unit(NextEntityId++, stats.Kind, position)
            {
                MaxHp = stats.MaxHp,
                Hp = stats.MaxHp,
                MaxStability = stats.MaxStability,
                Stability = stats.MaxStability,
                VisionRange = stats.VisionRange,
                Attack = stats.Attack,
                Defense = stats.Defense,
                MoveRange = stats.MoveRange,
                AttackRange = stats.AttackRange,
                StabilityDamage = stats.StabilityDamage,
                CritPercent = stats.CritPercent,
                CritMultiplierPercent = stats.CritMultiplierPercent,
                PushOnHit = stats.PushOnHit,
            };
            Units.Add(unit);
            return unit;
        }
    }
}
