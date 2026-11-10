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
        /// <summary>2: 낮은 벽 (2026-11-10). 3: 체력 · 안정지수 · 시야 · 전투 상태 · 배치 난수 (2026-11-10).</summary>
        public const int Version = 3;

        public static byte[] Encode(World world)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(world.Seed);
                writer.Write(world.Tick);
                writer.Write(world.FloorIndex);
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
                }

                writer.Write((byte)world.Mode);
                writer.Write((byte)world.Turn);
                writer.Write(world.Round);
                writer.Write(world.Combatants.Count);
                foreach (var id in world.Combatants) writer.Write(id);

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
                    int maxHp = reader.ReadInt32();
                    int hp = reader.ReadInt32();
                    int maxStability = reader.ReadInt32();
                    int stability = reader.ReadInt32();
                    int visionRange = reader.ReadInt32();
                    world.Units.Add(new Unit(id, kind, position, maxHp, hp, maxStability, stability, visionRange));
                }

                world.Mode = (WorldMode)reader.ReadByte();
                world.Turn = (TurnSide)reader.ReadByte();
                world.Round = reader.ReadInt32();
                int combatantCount = reader.ReadInt32();
                for (int i = 0; i < combatantCount; i++) world.Combatants.Add(reader.ReadInt32());
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

            int roomCount = reader.ReadInt32();
            var rooms = new List<RoomRect>(roomCount);
            for (int i = 0; i < roomCount; i++)
                rooms.Add(new RoomRect(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));

            var start = ReadCell(reader);
            var stairs = ReadCell(reader);
            return new FloorMap(width, height, tiles, lowWallEast, lowWallNorth, rooms, start, stairs);
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
