using System;
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
        public const int Version = 1;

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

                foreach (var state in world.Random.GetStates()) writer.Write(state);

                WriteMap(writer, world.Map);

                writer.Write(world.Units.Count);
                foreach (var unit in world.Units)
                {
                    writer.Write(unit.Id);
                    writer.Write((byte)unit.Kind);
                    WriteCell(writer, unit.Position);
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
                int nextEntityId = reader.ReadInt32();
                int playerId = reader.ReadInt32();

                var states = new ulong[RandomStreams.Count];
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
                    world.Units.Add(new Unit(id, kind, ReadCell(reader)));
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

            int roomCount = reader.ReadInt32();
            var rooms = new List<RoomRect>(roomCount);
            for (int i = 0; i < roomCount; i++)
                rooms.Add(new RoomRect(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()));

            var start = ReadCell(reader);
            var stairs = ReadCell(reader);
            return new FloorMap(width, height, tiles, rooms, start, stairs);
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
