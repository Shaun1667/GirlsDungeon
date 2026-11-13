using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 잠긴 문의 한 곳 (3-4d, 결정 2026-11-13 사용자: 특수 방 입구는 열쇠가 있어야 열림 · 열쇠는 층마다 1개 보장 + 가끔 더 · 층을 넘어가도 남음 ·
    /// 캐릭터 A는 힘으로 열 수 있음 (70%, 문당 1번, 실패해도 벌칙 없고 열쇠로는 열 수 있음)).
    /// - 문은 FloorDecor.LockRoom이 특수 방 입구마다 단다. 하나를 열면 그 방의 문이 모두 열린다 (입구가 여럿이어도 열쇠 1개).
    /// - 바로 옆(8방향)에서, 탐험 중에만. 굴림은 문 난수(RandomPurpose.Door).
    /// 수치: 캐릭터 표 DoorForcePercent · CombatSettings.CombatDropKeyPercent (BALANCE 20번).
    /// </summary>
    public static class Doors
    {
        /// <summary>그 캐릭터가 힘으로 열 확률 (%, 0 = 못 함). 고르지 않은 판은 0.</summary>
        public static int ForcePercent(World world, CombatSettings settings)
            => settings.TryCharacter(world.PlayerCharacter)?.DoorForcePercent ?? 0;

        /// <summary>문을 다룰 수 있는 자리인가 (탐험 중 · 잠긴 문 바로 옆).</summary>
        public static bool CanReach(World world, GridPos door)
        {
            var player = world.Player;
            return player != null && world.Mode == WorldMode.Exploration && world.Map.IsLockedDoor(door)
                && Loadout.CellDistance(player.Position, door) == 1;
        }

        /// <summary>이 문에 힘으로 열기를 시도할 수 있는가 (캐릭터가 할 수 있고, 이 문에 아직 안 해 봤다).</summary>
        public static bool CanForce(World world, CombatSettings settings, GridPos door)
            => ForcePercent(world, settings) > 0 && world.Map.InBounds(door) && world.Map.Get(door) == TileType.DoorLocked;

        /// <summary>열쇠 1개로 연다. 열쇠가 없거나 다룰 수 없으면 false.</summary>
        public static bool OpenWithKey(World world, GridPos door, List<GameEvent> events)
        {
            if (!CanReach(world, door) || world.Keys <= 0) return false;
            world.Keys--;
            int opened = OpenRoomOf(world.Map, door);
            events.Add(new DoorOpened(door, DoorOpenMethod.Key, opened, world.Keys));
            return true;
        }

        /// <summary>
        /// 힘으로 열어 본다 (문당 1번). 성공하면 그 방 문이 모두 열리고, 실패하면 이 문만 "시도함"이 된다 (열쇠로는 열 수 있다).
        /// 시도할 수 없으면 아무것도 바꾸지 않고 false.
        /// </summary>
        public static bool TryForce(World world, CombatSettings settings, GridPos door, List<GameEvent> events)
        {
            if (!CanReach(world, door) || !CanForce(world, settings, door)) return false;
            bool success = world.Random.Get(RandomPurpose.Door).Range(0, 100) < ForcePercent(world, settings);
            if (success)
            {
                int opened = OpenRoomOf(world.Map, door);
                events.Add(new DoorOpened(door, DoorOpenMethod.Force, opened, world.Keys));
            }
            else
            {
                world.Map.SetTile(door, TileType.DoorLockedForced);
                events.Add(new DoorForceFailed(door));
            }
            return true;
        }

        /// <summary>그 문이 달린 방(들)의 잠긴 문을 모두 연다. 연 문 수.</summary>
        public static int OpenRoomOf(FloorMap map, GridPos door)
        {
            int opened = 0;
            foreach (var room in map.Rooms)
            {
                if (!Touches(room, door)) continue;
                foreach (var cell in FloorDecor.Entrances(map, room))
                {
                    if (!map.IsLockedDoor(cell)) continue;
                    map.SetTile(cell, TileType.DoorOpen);
                    opened++;
                }
            }
            if (map.IsLockedDoor(door))
            {
                map.SetTile(door, TileType.DoorOpen);
                opened++;
            }
            return opened;
        }

        private static bool Touches(RoomRect room, GridPos cell)
        {
            if (room.Contains(cell)) return false;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (room.Contains(new GridPos(cell.X + dx, cell.Y + dy))) return true;
            return false;
        }
    }

    public enum DoorOpenMethod : byte
    {
        Key = 0,
        Force = 1,
    }

    /// <summary>잠긴 문을 연다 — 열쇠(useForce = false) 또는 힘(true). 탐험 중 바로 옆에서. 행동 · 틱을 쓰지 않는다.</summary>
    public sealed class OpenDoorCommand : ICommand
    {
        private readonly GridPos door;
        private readonly bool useForce;
        private readonly CombatSettings settings;

        public int CostTicks => 0;

        public OpenDoorCommand(GridPos door, bool useForce, CombatSettings settings)
        {
            this.door = door;
            this.useForce = useForce;
            this.settings = settings;
        }

        public bool TryApply(World world, List<GameEvent> events)
            => useForce ? Doors.TryForce(world, settings, door, events) : Doors.OpenWithKey(world, door, events);
    }

    /// <summary>디버그 전용: 열쇠를 더한다.</summary>
    public sealed class DebugAddKeysCommand : ICommand
    {
        private readonly int amount;

        public int CostTicks => 0;

        public DebugAddKeysCommand(int amount)
        {
            this.amount = amount;
        }

        public bool TryApply(World world, List<GameEvent> events)
        {
            world.Keys = System.Math.Max(0, world.Keys + amount);
            events.Add(new UnitStatsChanged(world.PlayerId));
            return true;
        }
    }

    /// <summary>문이 열렸다 (그 방 문 Count개). 화면은 지형을 다시 그린다.</summary>
    public sealed class DoorOpened : GameEvent
    {
        public GridPos Door { get; }
        public DoorOpenMethod Method { get; }
        public int Count { get; }
        public int KeysLeft { get; }

        public DoorOpened(GridPos door, DoorOpenMethod method, int count, int keysLeft)
        {
            Door = door;
            Method = method;
            Count = count;
            KeysLeft = keysLeft;
        }
    }

    /// <summary>힘으로 열기에 실패했다 (그 문은 다시 힘으로 열 수 없다).</summary>
    public sealed class DoorForceFailed : GameEvent
    {
        public GridPos Door { get; }

        public DoorForceFailed(GridPos door)
        {
            Door = door;
        }
    }

    /// <summary>열쇠를 주웠다.</summary>
    public sealed class KeyPickedUp : GameEvent
    {
        public int ItemId { get; }
        public int Keys { get; }

        public KeyPickedUp(int itemId, int keys)
        {
            ItemId = itemId;
            Keys = keys;
        }
    }
}
