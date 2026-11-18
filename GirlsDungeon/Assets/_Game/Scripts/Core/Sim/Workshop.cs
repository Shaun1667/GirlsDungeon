using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>파츠가 있는 곳 — 든 무기의 칸(0 ~ 3) 또는 예비 파츠 보관함(0 ~ SpareSlots-1).</summary>
    public enum PartPlace : byte
    {
        Equipped = 0,
        Spare = 1,
    }

    /// <summary>작업으로 바꾼 것.</summary>
    public enum ReworkKind : byte
    {
        Option = 0,
        Set = 1,
        /// <summary>강화 (3-16p): 한 줄의 값을 처음 나오는 범위만큼 한 번 더 굴려 더한다 — 한계(PartStatRange.Cap)까지, 항상 성공.</summary>
        Upgrade = 2,
    }

    /// <summary>
    /// 파츠 작업방의 한 곳 (3-4c, 결정 2026-11-13 사용자: 층마다 작업방 1개 · 적 없음 · 끼운 파츠와 예비 파츠를 다룸 ·
    /// 옵션 바꾸기와 세트 바꾸기 중 하나를 골라 원하는 것을 고르면 확률로 성공, 실패하면 무작위 · 확률은 캐릭터마다 다르고 A는 낮음).
    /// - 작업대: TileType.Workbench 바로 옆(8방향)에서, 탐험 중에만.
    /// - 분해 `Disassemble`: 파츠를 없애고 등급만큼 재료(World.PartMaterials)를 얻는다. 항상 성공.
    /// - 옵션 바꾸기 `ChangeOption`: 능력치 한 줄을 골라 원하는 능력치로 — 성공하면 그 능력치, 실패하면 후보에서 무작위. 값은 둘 다 범위 안에서 굴림.
    /// - 세트 바꾸기 `ChangeSet`: 세트가 붙는 파츠에서 원하는 세트로 — 성공하면 그 세트, 실패하면 7개 중 무작위.
    /// - 재료는 성공 · 실패 모두 든다. 굴림은 작업 난수(RandomPurpose.Workshop).
    /// - 예비 파츠: 바닥 파츠를 보관함에 넣기 `Store`(어디서나), 작업대에서 보관함 파츠를 든 무기에 끼우기 `EquipSpare`(원래 파츠는 보관함으로).
    /// 수치는 CombatSettings.Workshop… · 캐릭터 표 WorkshopOptionPercent · WorkshopSetPercent (BALANCE 19번).
    /// </summary>
    public static class Workshop
    {
        /// <summary>플레이어가 작업대 바로 옆에 있고 탐험 중인가.</summary>
        public static bool AtBench(World world)
        {
            var player = world.Player;
            if (player == null || world.Mode != WorldMode.Exploration) return false;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new GridPos(player.Position.X + dx, player.Position.Y + dy);
                    if ((dx != 0 || dy != 0) && world.Map.InBounds(cell) && world.Map.Get(cell) == TileType.Workbench) return true;
                }
            return false;
        }

        public static WeaponPart Get(World world, PartPlace place, int index)
        {
            if (place == PartPlace.Equipped) return index >= 0 && index < WeaponPart.SlotCount ? world.PlayerParts[index] : null;
            return index >= 0 && index < world.SpareParts.Count ? world.SpareParts[index] : null;
        }

        /// <summary>옵션 바꾸기 성공 확률 (%) — 고른 캐릭터 줄, 없으면 기본값.</summary>
        public static int OptionPercent(World world, CombatSettings settings)
            => settings.TryCharacter(world.PlayerCharacter)?.WorkshopOptionPercent ?? settings.WorkshopOptionPercent;

        /// <summary>세트 바꾸기 성공 확률 (%) — 고른 캐릭터 줄, 없으면 기본값.</summary>
        public static int SetPercent(World world, CombatSettings settings)
            => settings.TryCharacter(world.PlayerCharacter)?.WorkshopSetPercent ?? settings.WorkshopSetPercent;

        /// <summary>그 등급 파츠를 분해하면 얻는 재료.</summary>
        public static int MaterialsFrom(CombatSettings settings, PartGrade grade) => settings.DisassembleMaterials[(int)grade];

        /// <summary>그 칸에 나올 수 있는 능력치 후보.</summary>
        public static IReadOnlyList<PartStat> Pool(CombatSettings settings, int slot)
            => slot == 0 ? settings.PartPrimaryStats : settings.PartSecondaryStats;

        /// <summary>그 줄을 target으로 바꿀 수 있는가 (그 칸 후보이고, 다른 줄에 같은 능력치가 없다 — 같은 줄의 능력치는 다시 굴리기).</summary>
        public static bool CanTarget(CombatSettings settings, WeaponPart part, int line, PartStat target)
        {
            if (part == null || line < 0 || line >= part.Bonuses.Count) return false;
            bool inPool = false;
            foreach (var stat in Pool(settings, part.Slot)) if (stat == target) inPool = true;
            if (!inPool) return false;
            for (int i = 0; i < part.Bonuses.Count; i++)
                if (i != line && part.Bonuses[i].Stat == target) return false;
            return true;
        }

        public static bool Disassemble(World world, CombatSettings settings, PartPlace place, int index, List<GameEvent> events)
        {
            if (!AtBench(world)) return false;
            var part = Get(world, place, index);
            if (part == null) return false;
            Remove(world, settings, place, index);
            int gained = MaterialsFrom(settings, part.Grade);
            world.PartMaterials += gained;
            events.Add(new PartDisassembled(part.Weapon, part.Slot, part.Grade, gained, world.PartMaterials));
            if (place == PartPlace.Equipped) events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }

        public static bool ChangeOption(World world, CombatSettings settings, PartPlace place, int index, int line, PartStat target, List<GameEvent> events)
        {
            if (!AtBench(world) || world.PartMaterials < settings.WorkshopOptionCost) return false;
            var part = Get(world, place, index);
            if (!CanTarget(settings, part, line, target)) return false;

            var random = world.Random.Get(RandomPurpose.Workshop);
            bool success = random.Range(0, 100) < OptionPercent(world, settings);
            var stat = target;
            if (!success)
            {
                // 실패: 그 칸 후보 중 다른 줄과 겹치지 않는 것에서 무작위 (원하던 것이 다시 나올 수도 있다).
                var candidates = new List<PartStat>();
                foreach (var candidate in Pool(settings, part.Slot))
                    if (CanTarget(settings, part, line, candidate)) candidates.Add(candidate);
                stat = candidates[random.Range(0, candidates.Count)];
            }
            var range = settings.PartRange(stat);
            var bonuses = new List<PartBonus>(part.Bonuses);
            bonuses[line] = new PartBonus(stat, random.Range(range.Min, range.Max + 1));

            world.PartMaterials -= settings.WorkshopOptionCost;
            Replace(world, settings, place, index, new WeaponPart(part.Weapon, part.Slot, part.Grade, bonuses, part.Set));
            events.Add(new PartReworked(ReworkKind.Option, success, place, index, world.PartMaterials));
            if (place == PartPlace.Equipped) events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }

        /// <summary>그 줄을 강화할 수 있는가 (한계가 있고 아직 한계 아래).</summary>
        public static bool CanUpgrade(CombatSettings settings, WeaponPart part, int line)
        {
            if (part == null || line < 0 || line >= part.Bonuses.Count) return false;
            var range = settings.PartRange(part.Bonuses[line].Stat);
            return range != null && range.Cap > 0 && part.Bonuses[line].Value < range.Cap;
        }

        /// <summary>
        /// 강화 (3-16p, 결정 2026-11-18 사용자): 재료를 내고 그 줄의 값에 처음 나오는 범위(Min ~ Max)를 한 번 더 굴려 더한다.
        /// 한계(Cap)를 넘으면 한계에서 멈춘다. 항상 성공. 작업 난수.
        /// </summary>
        public static bool Upgrade(World world, CombatSettings settings, PartPlace place, int index, int line, List<GameEvent> events)
        {
            if (!AtBench(world) || world.PartMaterials < settings.WorkshopUpgradeCost) return false;
            var part = Get(world, place, index);
            if (!CanUpgrade(settings, part, line)) return false;

            var bonus = part.Bonuses[line];
            var range = settings.PartRange(bonus.Stat);
            int roll = world.Random.Get(RandomPurpose.Workshop).Range(range.Min, range.Max + 1);
            var bonuses = new List<PartBonus>(part.Bonuses);
            bonuses[line] = new PartBonus(bonus.Stat, System.Math.Min(range.Cap, bonus.Value + roll));

            world.PartMaterials -= settings.WorkshopUpgradeCost;
            Replace(world, settings, place, index, new WeaponPart(part.Weapon, part.Slot, part.Grade, bonuses, part.Set));
            events.Add(new PartReworked(ReworkKind.Upgrade, true, place, index, world.PartMaterials));
            if (place == PartPlace.Equipped) events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }

        public static bool ChangeSet(World world, CombatSettings settings, PartPlace place, int index, PartSet target, List<GameEvent> events)
        {
            if (!AtBench(world) || world.PartMaterials < settings.WorkshopSetCost || target == PartSet.None) return false;
            var part = Get(world, place, index);
            if (part == null || part.Set == PartSet.None) return false;

            var random = world.Random.Get(RandomPurpose.Workshop);
            bool success = random.Range(0, 100) < SetPercent(world, settings);
            var set = success ? target : SetEffects.RandomSet(random);

            world.PartMaterials -= settings.WorkshopSetCost;
            Replace(world, settings, place, index, new WeaponPart(part.Weapon, part.Slot, part.Grade, part.Bonuses, set));
            events.Add(new PartReworked(ReworkKind.Set, success, place, index, world.PartMaterials));
            if (place == PartPlace.Equipped) events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }

        /// <summary>발밑 파츠를 예비 보관함에 넣는다 (보관함이 차면 false). 탐험 중이나 전투 중 내 차례에.</summary>
        public static bool Store(World world, CombatSettings settings, int itemId, List<GameEvent> events)
        {
            var player = world.Player;
            var item = world.FindItem(itemId);
            if (player == null || item == null || item.Kind != ItemKind.Part || item.Position != player.Position) return false;
            if (world.SpareParts.Count >= settings.SpareSlots) return false;
            world.Items.Remove(item);
            world.SpareParts.Add(item.Part);
            events.Add(new PartStored(item.Id, item.Part.Weapon, world.SpareParts.Count));
            return true;
        }

        /// <summary>작업대에서 보관함 파츠를 든 무기의 그 칸에 끼운다. 원래 파츠는 같은 자리(보관함)로 간다. 든 무기용이 아니면 false.</summary>
        public static bool EquipSpare(World world, CombatSettings settings, int spareIndex, List<GameEvent> events)
        {
            if (!AtBench(world)) return false;
            var part = Get(world, PartPlace.Spare, spareIndex);
            if (part == null || !Parts.Fits(world, part)) return false;
            var old = world.PlayerParts[part.Slot];
            world.PlayerParts[part.Slot] = part;
            if (old != null) world.SpareParts[spareIndex] = old;
            else world.SpareParts.RemoveAt(spareIndex);
            Loadout.Apply(world, settings);
            events.Add(new PartEquipped(-1, part.Weapon, part.Slot, part.Grade));
            events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }

        private static void Remove(World world, CombatSettings settings, PartPlace place, int index)
        {
            if (place == PartPlace.Spare)
            {
                world.SpareParts.RemoveAt(index);
                return;
            }
            world.PlayerParts[index] = null;
            Loadout.Apply(world, settings);
        }

        private static void Replace(World world, CombatSettings settings, PartPlace place, int index, WeaponPart part)
        {
            if (place == PartPlace.Spare)
            {
                world.SpareParts[index] = part;
                return;
            }
            world.PlayerParts[index] = part;
            Loadout.Apply(world, settings);
        }
    }

    /// <summary>작업대에서 파츠를 분해한다 (재료 얻기). 행동 · 틱을 쓰지 않는다.</summary>
    public sealed class DisassemblePartCommand : ICommand
    {
        private readonly PartPlace place;
        private readonly int index;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public DisassemblePartCommand(PartPlace place, int index, CombatSettings settings)
        {
            this.place = place;
            this.index = index;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => Workshop.Disassemble(world, settings, place, index, events);
    }

    /// <summary>작업대에서 파츠 능력치 한 줄을 원하는 능력치로 바꿔 본다 (확률, 실패하면 무작위).</summary>
    public sealed class ChangePartOptionCommand : ICommand
    {
        private readonly PartPlace place;
        private readonly int index;
        private readonly int line;
        private readonly PartStat target;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public ChangePartOptionCommand(PartPlace place, int index, int line, PartStat target, CombatSettings settings)
        {
            this.place = place;
            this.index = index;
            this.line = line;
            this.target = target;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => Workshop.ChangeOption(world, settings, place, index, line, target, events);
    }

    /// <summary>작업대에서 파츠 한 줄을 강화한다 (3-16p, 항상 성공).</summary>
    public sealed class UpgradePartCommand : ICommand
    {
        private readonly PartPlace place;
        private readonly int index;
        private readonly int line;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public UpgradePartCommand(PartPlace place, int index, int line, CombatSettings settings)
        {
            this.place = place;
            this.index = index;
            this.line = line;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => Workshop.Upgrade(world, settings, place, index, line, events);
    }

    /// <summary>작업대에서 파츠 세트를 원하는 세트로 바꿔 본다 (확률, 실패하면 무작위).</summary>
    public sealed class ChangePartSetCommand : ICommand
    {
        private readonly PartPlace place;
        private readonly int index;
        private readonly PartSet target;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public ChangePartSetCommand(PartPlace place, int index, PartSet target, CombatSettings settings)
        {
            this.place = place;
            this.index = index;
            this.target = target;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => Workshop.ChangeSet(world, settings, place, index, target, events);
    }

    /// <summary>발밑 파츠를 예비 보관함에 넣는다 (비교 창의 "보관"). 행동을 쓰지 않는다.</summary>
    public sealed class StorePartCommand : ICommand
    {
        private readonly int itemId;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public StorePartCommand(int itemId, CombatSettings settings)
        {
            this.itemId = itemId;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (world.Mode != WorldMode.Exploration && !TurnFlow.IsPlayerTurn(world)) return false;
            return Workshop.Store(world, settings, itemId, events);
        }
    }

    /// <summary>작업대에서 보관함 파츠를 든 무기에 끼운다.</summary>
    public sealed class EquipSparePartCommand : ICommand
    {
        private readonly int spareIndex;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public EquipSparePartCommand(int spareIndex, CombatSettings settings)
        {
            this.spareIndex = spareIndex;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events) => Workshop.EquipSpare(world, settings, spareIndex, events);
    }

    /// <summary>디버그 전용: 플레이어를 빈 칸으로 바로 옮긴다 (작업방 시험용, 감시 반응 없음).</summary>
    public sealed class DebugTeleportCommand : ICommand
    {
        private readonly int unitId;
        private readonly GridPos cell;

        public int CostTicks => 0;

        public DebugTeleportCommand(int unitId, GridPos cell)
        {
            this.unitId = unitId;
            this.cell = cell;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            var unit = world.FindUnit(unitId);
            if (unit == null || !world.Map.IsWalkable(cell) || world.IsOccupied(cell)) return false;
            var from = unit.Position;
            unit.Position = cell;
            events.Add(new UnitMoved(unit.Id, from, cell));
            return true;
        }
    }

    /// <summary>디버그 전용: 파츠 재료를 더한다.</summary>
    public sealed class DebugAddMaterialsCommand : ICommand
    {
        private readonly int amount;

        public int CostTicks => 0;

        public DebugAddMaterialsCommand(int amount)
        {
            this.amount = amount;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            world.PartMaterials = System.Math.Max(0, world.PartMaterials + amount);
            events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }
    }

    /// <summary>파츠를 분해했다.</summary>
    public sealed class PartDisassembled : GameEvent
    {
        public WeaponKind Weapon { get; }
        public int Slot { get; }
        public PartGrade Grade { get; }
        public int Gained { get; }
        public int MaterialsNow { get; }

        public PartDisassembled(WeaponKind weapon, int slot, PartGrade grade, int gained, int materialsNow)
        {
            Weapon = weapon;
            Slot = slot;
            Grade = grade;
            Gained = gained;
            MaterialsNow = materialsNow;
        }
    }

    /// <summary>파츠 옵션 · 세트를 바꿨다 (Success = 원하던 것이 나옴).</summary>
    public sealed class PartReworked : GameEvent
    {
        public ReworkKind Kind { get; }
        public bool Success { get; }
        public PartPlace Place { get; }
        public int Index { get; }
        public int MaterialsNow { get; }

        public PartReworked(ReworkKind kind, bool success, PartPlace place, int index, int materialsNow)
        {
            Kind = kind;
            Success = success;
            Place = place;
            Index = index;
            MaterialsNow = materialsNow;
        }
    }

    /// <summary>파츠를 예비 보관함에 넣었다.</summary>
    public sealed class PartStored : GameEvent
    {
        public int ItemId { get; }
        public WeaponKind Weapon { get; }
        public int SpareCount { get; }

        public PartStored(int itemId, WeaponKind weapon, int spareCount)
        {
            ItemId = itemId;
            Weapon = weapon;
            SpareCount = spareCount;
        }
    }
}
