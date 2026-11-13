using System;
using System.Collections.Generic;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>파츠 등급 (사용자 제공 표, TASKS 3-4). 저장 · 데이터에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum PartGrade : byte
    {
        /// <summary>공업: 1번 칸 능력치 2개, 2 · 3 · 4번 칸 1개.</summary>
        Industrial = 0,
        /// <summary>군용: 1번 칸 3개, 2 · 3 · 4번 칸 2개 + 세트 (SetEffects).</summary>
        Military = 1,
        /// <summary>정밀: 1번 칸 4개, 2 · 3 · 4번 칸 3개 + 세트 (SetEffects).</summary>
        Precision = 2,
    }

    /// <summary>파츠가 올려 주는 능력치. 저장 · 데이터에 정수로 들어가므로 끝에만 추가한다.</summary>
    public enum PartStat : byte
    {
        Attack = 0,
        AttackRange = 1,
        CritPercent = 2,
        /// <summary>치명 배율 (%p, 150 → 160).</summary>
        CritMultiplier = 3,
        StabilityDamage = 4,
        MaxHp = 5,
        Defense = 6,
        MoveRange = 7,
        MaxStability = 8,
    }

    /// <summary>파츠 능력치 하나 (예: 공격 +2).</summary>
    public readonly struct PartBonus
    {
        public readonly PartStat Stat;
        public readonly int Value;

        public PartBonus(PartStat stat, int value)
        {
            Stat = stat;
            Value = value;
        }
    }

    /// <summary>
    /// 무기 파츠 하나. 무기 종류마다 4칸(0 = 1번 칸 총구 · 칼끝, 1 ~ 3 = 2 · 3 · 4번 칸)이고 그 무기 종류에만 맞는다.
    /// 칸 이름(총구 · 조준기 …)은 화면 쪽 무기 표(GameBalance.weapons[].partSlotKeys)에서 찾는다.
    /// </summary>
    public sealed class WeaponPart
    {
        public const int SlotCount = 4;

        public WeaponKind Weapon { get; }
        public int Slot { get; }
        public PartGrade Grade { get; }
        public IReadOnlyList<PartBonus> Bonuses { get; }

        /// <summary>세트 (군용 · 정밀의 2 · 3 · 4번 칸만, 그 밖은 None). 같은 세트 3개를 끼우면 켜진다 (SetEffects).</summary>
        public PartSet Set { get; }

        public WeaponPart(WeaponKind weapon, int slot, PartGrade grade, IReadOnlyList<PartBonus> bonuses, PartSet set = PartSet.None)
        {
            Set = set;
            Weapon = weapon;
            Slot = slot;
            Grade = grade;
            Bonuses = bonuses;
        }

        public int Sum(PartStat stat)
        {
            int total = 0;
            foreach (var bonus in Bonuses) if (bonus.Stat == stat) total += bonus.Value;
            return total;
        }
    }

    /// <summary>파츠 능력치 하나가 나올 때의 값 범위 (표 한 줄, BALANCE 17번).</summary>
    public sealed class PartStatRange
    {
        public PartStat Stat;
        public int Min;
        public int Max;
    }

    /// <summary>
    /// 파츠의 한 곳 (TASKS 3-4 파츠, 결정 2026-11-12 사용자: 첫 단계는 파츠 끼우기만 — 세트 효과 · 가방 · 분해 · 제작은 다음 항목,
    /// 능력치는 몸 능력치 포함, 파츠는 무기에 붙어 다님).
    /// - 만들기 `Generate`: 칸 · 등급 · 능력치(칸마다 후보 표에서 겹치지 않게) · 값을 아이템 난수로 굴린다.
    /// - 끼우기 `Equip`: 발밑 파츠를 든 무기의 그 칸에 끼우고, 원래 파츠는 같은 칸 바닥에 내려놓는다.
    /// - 합산 `Sum`: 든 무기 4칸의 능력치 합 — 적용은 Loadout.Apply 한 곳 (핵심 규칙 7).
    /// </summary>
    public static class Parts
    {
        /// <summary>그 무기 종류의 새 파츠를 굴린다 (칸 · 등급 · 능력치 · 값 모두 random에서).</summary>
        public static WeaponPart Generate(CombatSettings settings, WeaponKind weapon, IRandom random)
        {
            int slot = random.Range(0, WeaponPart.SlotCount);
            var grade = RollGrade(settings, random);
            var pool = new List<PartStat>(slot == 0 ? settings.PartPrimaryStats : settings.PartSecondaryStats);
            int count = Math.Min(pool.Count, (slot == 0 ? settings.PartPrimaryStatCounts : settings.PartSecondaryStatCounts)[(int)grade]);

            var bonuses = new List<PartBonus>();
            for (int i = 0; i < count; i++)
            {
                int index = random.Range(0, pool.Count);
                var stat = pool[index];
                pool.RemoveAt(index);
                var range = settings.PartRange(stat);
                bonuses.Add(new PartBonus(stat, random.Range(range.Min, range.Max + 1)));
            }
            // 세트는 능력치 다음에 굴린다 (세트가 없는 파츠는 난수를 더 쓰지 않는다).
            var set = SetEffects.CanHaveSet(grade, slot) ? (PartSet)random.Range(1, SetEffects.SetCount + 1) : PartSet.None;
            return new WeaponPart(weapon, slot, grade, bonuses, set);
        }

        private static PartGrade RollGrade(CombatSettings settings, IRandom random)
        {
            int roll = random.Range(0, 100);
            if (roll < settings.PartGradePercents[(int)PartGrade.Precision]) return PartGrade.Precision;
            roll -= settings.PartGradePercents[(int)PartGrade.Precision];
            if (roll < settings.PartGradePercents[(int)PartGrade.Military]) return PartGrade.Military;
            return PartGrade.Industrial;
        }

        /// <summary>든 무기에 끼운 파츠의 그 능력치 합.</summary>
        public static int Sum(World world, PartStat stat)
        {
            int total = 0;
            foreach (var part in world.PlayerParts) if (part != null) total += part.Sum(stat);
            return total;
        }

        /// <summary>발밑 파츠를 지금 든 무기에 끼울 수 있는지 (그 무기 종류의 파츠여야 한다).</summary>
        public static bool Fits(World world, WeaponPart part) => part != null && part.Weapon == world.PlayerWeapon;

        /// <summary>
        /// 발밑 파츠를 끼운다: 그 칸의 원래 파츠는 같은 칸 바닥에 내려놓고(새 ID), 능력치를 다시 맞춘다(Loadout.Apply).
        /// 파츠가 아니거나 발밑이 아니거나 든 무기에 맞지 않으면 아무것도 바꾸지 않고 false.
        /// </summary>
        public static bool Equip(World world, CombatSettings settings, int itemId, List<GameEvent> events)
        {
            var player = world.Player;
            var item = world.FindItem(itemId);
            if (player == null || item == null || item.Kind != ItemKind.Part || item.Position != player.Position || !Fits(world, item.Part)) return false;

            var part = item.Part;
            var old = world.PlayerParts[part.Slot];
            world.Items.Remove(item);
            world.PlayerParts[part.Slot] = part;
            Loadout.Apply(world, settings);
            events.Add(new PartEquipped(item.Id, part.Weapon, part.Slot, part.Grade));
            if (old != null)
            {
                var dropped = world.AddPartItem(old, player.Position);
                events.Add(new ItemDropped(dropped.Id, dropped.Kind, dropped.Weapon, dropped.Position, DropSource.Swap));
            }
            events.Add(new UnitStatsChanged(player.Id));
            return true;
        }
    }

    /// <summary>발밑 파츠를 든 무기에 끼운다 (비교 창의 "바꾸기"). 탐험 중이나 전투 중 내 차례에. 행동을 쓰지 않는다.</summary>
    public sealed class EquipPartCommand : ICommand
    {
        private readonly int itemId;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public EquipPartCommand(int itemId, CombatSettings settings)
        {
            this.itemId = itemId;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (world.Mode != WorldMode.Exploration && !TurnFlow.IsPlayerTurn(world)) return false;
            return Parts.Equip(world, settings, itemId, events);
        }
    }

    /// <summary>디버그 전용: 설 수 있는 칸에 지금 든 무기용 새 파츠를 놓는다 (아이템 난수로 굴림).</summary>
    public sealed class DebugDropPartCommand : ICommand
    {
        private readonly Grid.GridPos cell;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public DebugDropPartCommand(Grid.GridPos cell, CombatSettings settings)
        {
            this.cell = cell;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (!world.Map.IsWalkable(cell)) return false;
            var part = Parts.Generate(settings, world.PlayerWeapon, world.Random.Get(RandomPurpose.Item));
            var item = world.AddPartItem(part, cell);
            events.Add(new ItemDropped(item.Id, item.Kind, item.Weapon, item.Position, DropSource.Debug));
            return true;
        }
    }

    /// <summary>
    /// 디버그 전용: 든 무기의 2 · 3 · 4번 칸에 그 세트의 군용 파츠(능력치 없음)를 끼운다 — 세트 효과 시험용 (3-4b).
    /// 원래 파츠는 지운다. set = None이면 세 칸을 비운다.
    /// </summary>
    public sealed class DebugEquipSetCommand : ICommand
    {
        private readonly PartSet set;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public DebugEquipSetCommand(PartSet set, CombatSettings settings)
        {
            this.set = set;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            if (world.Player == null) return false;
            for (int slot = SetEffects.FirstSetSlot; slot < WeaponPart.SlotCount; slot++)
                world.PlayerParts[slot] = set == PartSet.None ? null : new WeaponPart(world.PlayerWeapon, slot, PartGrade.Military, new List<PartBonus>(), set);
            Loadout.Apply(world, settings);
            events.Add(new UnitStatsChanged(world.Player.Id));
            return true;
        }
    }

    /// <summary>발밑에 파츠가 있다. 화면이 비교 창을 띄운다 (끼우기 = EquipPartCommand, 두기 = 아무것도 안 함).</summary>
    public sealed class PartFound : GameEvent
    {
        public int ItemId { get; }

        public PartFound(int itemId)
        {
            ItemId = itemId;
        }
    }

    /// <summary>파츠를 끼웠다. 원래 파츠가 있었으면 ItemDropped(Swap)로 같은 칸에 놓인다.</summary>
    public sealed class PartEquipped : GameEvent
    {
        public int ItemId { get; }
        public WeaponKind Weapon { get; }
        public int Slot { get; }
        public PartGrade Grade { get; }

        public PartEquipped(int itemId, WeaponKind weapon, int slot, PartGrade grade)
        {
            ItemId = itemId;
            Weapon = weapon;
            Slot = slot;
            Grade = grade;
        }
    }
}
