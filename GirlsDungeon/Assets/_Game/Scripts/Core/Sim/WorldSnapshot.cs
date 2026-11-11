using System.Collections.Generic;
using System.IO;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using GirlsDungeon.Core.Random;

namespace GirlsDungeon.Core.Sim
{
    /// <summary>
    /// 세계 ↔ 바이트. 저장 · 복원 · 회귀 테스트가 함께 쓴다.
    /// 새 상태 = World 필드 + 버전 증가 + 여기 인코더 + 왕복 테스트.
    /// </summary>
    public static class WorldSnapshot
    {
        /// <summary>
        /// 2: 낮은 벽 (2026-11-10). 3: 체력 · 안정지수 · 시야 · 전투 상태 · 배치 난수 (2026-11-10). 4: 본 칸 기록 (2026-11-10).
        /// 5: 유닛 전투 능력치 · 이번 차례 이동 / 행동 여부 (2026-11-10). 6: 수류탄 재사용 대기 (2026-11-10).
        /// 7: 유닛 경계 · 감시 (2026-11-10). 8: 상자 · 낮은 벽 손상 (2026-11-10). 9: 밀치기 재사용 대기 (2026-11-10, 낭떠러지 칸은 TileType 값만 추가). 10: 유닛 밀기 칸 수 (2026-11-10). 11: 쓰러뜨린 적 수 (2026-11-10).
        /// 12: 든 무기 · 바닥 물건 (2026-11-10, 보급 상자 칸은 TileType 값만 추가). 13: 이번 차례 사격 수 (2026-11-11).
        /// </summary>
        public const int Version = 13;

        public static byte[] Encode(World world)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(world.Seed);
                writer.Write(world.Tick);
                writer.Write(world.FloorIndex);
                writer.Write(world.Kills);
                writer.Write(world.NextEntityId);
                writer.Write(world.PlayerId);

                var states = world.Random.GetStates();
                writer.Write(states.Length);
                foreach (var state in states) writer.Write(state);

                WriteMap(writer, world.Map);

                writer.Write(world.Units.Count);
                foreach (var unit in world.Units)
                {
                    writer.Write(unit.Id);
                    writer.Write((byte)unit.Kind);
                    WriteCell(writer, unit.Position);
                    writer.Write(unit.MaxHp);
                    writer.Write(unit.Hp);
                    writer.Write(unit.MaxStability);
                    writer.Write(unit.Stability);
                    writer.Write(unit.VisionRange);
                    writer.Write(unit.Attack);
                    writer.Write(unit.Defense);
                    writer.Write(unit.MoveRange);
                    writer.Write(unit.AttackRange);
                    writer.Write(unit.StabilityDamage);
                    writer.Write(unit.CritPercent);
                    writer.Write(unit.CritMultiplierPercent);
                    writer.Write(unit.PushOnHit);
                    writer.Write(unit.Guarding);
                    writer.Write(unit.Overwatching);
                }

                writer.Write((byte)world.Mode);
                writer.Write((byte)world.Turn);
                writer.Write(world.Round);
                writer.Write(world.PlayerMoved);
                writer.Write(world.PlayerActed);
                writer.Write(world.GrenadeCooldown);
                writer.Write(world.PushCooldown);
                writer.Write(world.Combatants.Count);
                foreach (var id in world.Combatants) writer.Write(id);

                int cellCount = world.Map.Width * world.Map.Height;
                for (int i = 0; i < cellCount; i++)
                    writer.Write(world.Explored != null && i < world.Explored.Length && world.Explored[i]);

                writer.Write((byte)world.PlayerWeapon);
                writer.Write(world.PlayerShots);
                writer.Write(world.Items.Count);
                foreach (var item in world.Items)
                {
                    writer.Write(item.Id);
                    writer.Write((byte)item.Kind);
                    writer.Write((byte)item.Weapon);
                    WriteCell(writer, item.Position);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        public static World Decode(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                int version = reader.ReadInt32();
                if (version != Version)
                    throw new InvalidDataException($"Unsupported snapshot version {version}.");

                int seed = reader.ReadInt32();
                long tick = reader.ReadInt64();
                int floorIndex = reader.ReadInt32();
                int kills = reader.ReadInt32();
                int nextEntityId = reader.ReadInt32();
                int playerId = reader.ReadInt32();

                int stateCount = reader.ReadInt32();
                if (stateCount != RandomStreams.Count)
                    throw new InvalidDataException($"Snapshot has {stateCount} random streams, expected {RandomStreams.Count}.");
                var states = new ulong[stateCount];
                for (int i = 0; i < states.Length; i++) states[i] = reader.ReadUInt64();

                var world = new World(seed, new RandomStreams(states))
                {
                    Tick = tick,
                    FloorIndex = floorIndex,
                    Kills = kills,
                    NextEntityId = nextEntityId,
                    PlayerId = playerId,
                    Map = ReadMap(reader),
                };

                int unitCount = reader.ReadInt32();
                for (int i = 0; i < unitCount; i++)
                {
                    int id = reader.ReadInt32();
                    var kind = (UnitKind)reader.ReadByte();
                    var position = ReadCell(reader);
                    world.Units.Add(new Unit(id, kind, position)
                    {
                        MaxHp = reader.ReadInt32(),
                        Hp = reader.ReadInt32(),
                        MaxStability = reader.ReadInt32(),
                        Stability = reader.ReadInt32(),
                        VisionRange = reader.ReadInt32(),
                        Attack = reader.ReadInt32(),
                        Defense = reader.ReadInt32(),
                        MoveRange = reader.ReadInt32(),
                        AttackRange = reader.ReadInt32(),
                        StabilityDamage = reader.ReadInt32(),
                        CritPercent = reader.ReadInt32(),
                        CritMultiplierPercent = reader.ReadInt32(),
                        PushOnHit = reader.ReadInt32(),
                        Guarding = reader.ReadBoolean(),
                        Overwatching = reader.ReadBoolean(),
                    });
                }

                world.Mode = (WorldMode)reader.ReadByte();
                world.Turn = (TurnSide)reader.ReadByte();
                world.Round = reader.ReadInt32();
                world.PlayerMoved = reader.ReadBoolean();
                world.PlayerActed = reader.ReadBoolean();
                world.GrenadeCooldown = reader.ReadInt32();
                world.PushCooldown = reader.ReadInt32();
                int combatantCount = reader.ReadInt32();
                for (int i = 0; i < combatantCount; i++) world.Combatants.Add(reader.ReadInt32());

                var explored = new bool[world.Map.Width * world.Map.Height];
                for (int i = 0; i < explored.Length; i++) explored[i] = reader.ReadBoolean();
                world.Explored = explored;

                world.PlayerWeapon = (WeaponKind)reader.ReadByte();
                world.PlayerShots = reader.ReadInt32();
                int itemCount = reader.ReadInt32();
                for (int i = 0; i < itemCount; i++)
                {
                    int id = reader.ReadInt32();
                    var kind = (ItemKind)reader.ReadByte();
                    var weapon = (WeaponKind)reader.ReadByte();
                    world.Items.Add(new FloorItem(id, kind, weapon, ReadCell(reader)));
                }
                return world;
            }
        }

        private static void WriteMap(BinaryWriter writer, FloorMap map)
        {
            writer.Write(map.Width);
            writer.Write(map.Height);
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    writer.Write((byte)map.Get(new GridPos(x, y)));

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    writer.Write((byte)((map.HasLowWallEast(cell) ? 1 : 0) | (map.HasLowWallNorth(cell) ? 2 : 0)));
                }
            }

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    writer.Write((byte)map.CrateDamage(cell));
                    writer.Write((byte)map.LowWallEastDamage(cell));
                    writer.Write((byte)map.LowWallNorthDamage(cell));
                }
            }

            writer.Write(map.Rooms.Count);
            foreach (var room in map.Rooms)
            {
                writer.Write(room.X);
                writer.Write(room.Y);
                writer.Write(room.Width);
                writer.Write(room.Height);
            }

            WriteCell(writer, map.StartCell);
            WriteCell(writer, map.StairsCell);
        }

        private static FloorMap ReadMap(BinaryReader reader)
        {
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            var tiles = new TileType[width * height];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = (TileType)reader.ReadByte();

            var lowWallEast = new bool[tiles.Length];
            var lowWallNorth = new bool[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                byte edges = reader.ReadByte();
                lowWallEast[i] = (edges & 1) != 0;
                lowWallNorth[i] = (edges & 2) != 0;
            }

            var crateDamage = new byte[tiles.Length];
            var eastDamage = new byte[tiles.Length];
            var northDamage = new byte[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                crateDamage[i] = reader.ReadByte();
                eastDamage[i] = reader.ReadByte();
                northDamage[i] = reader.ReadByte();
            }

            int roomCount = reader.ReadInt32();
            var rooms = new List<RoomRect>(roomCount);
            for (int i = 0; i < roomCount; i++)
                rooms.Add(new RoomRect(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));

            var start = ReadCell(reader);
            var stairs = ReadCell(reader);
            var map = new FloorMap(width, height, tiles, lowWallEast, lowWallNorth, rooms, start, stairs);
            for (int i = 0; i < tiles.Length; i++)
            {
                var cell = new GridPos(i % width, i / width);
                map.SetCrateDamage(cell, crateDamage[i]);
                map.SetLowWallEastDamage(cell, eastDamage[i]);
                map.SetLowWallNorthDamage(cell, northDamage[i]);
            }
            return map;
        }

        private static void WriteCell(BinaryWriter writer, GridPos cell)
        {
            writer.Write(cell.X);
            writer.Write(cell.Y);
        }

        private static GridPos ReadCell(BinaryReader reader)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            return new GridPos(x, y);
        }
    }
}
