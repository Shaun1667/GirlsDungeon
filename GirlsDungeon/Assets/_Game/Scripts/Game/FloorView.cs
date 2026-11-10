using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>층 지형과 엄폐물을 그레이박스 메시로 그린다. 규칙은 모르고 FloorMap만 읽는다.</summary>
    public sealed class FloorView : MonoBehaviour
    {
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material stairsMaterial;
        [SerializeField] private Material coverMaterial;
        [Tooltip("Dark pit drawn for chasm cells.")]
        [SerializeField] private Material chasmMaterial;
        [Tooltip("Closed supply crates.")]
        [SerializeField] private Material supplyMaterial;
        [Tooltip("Opened supply crates (drawn lower and darker so they do not look like breakable crates).")]
        [SerializeField] private Material supplyOpenedMaterial;
        [Tooltip("Second pass that darkens unexplored / out-of-sight cells (GirlsDungeon/FogOverlay).")]
        [SerializeField] private Material fogMaterial;

        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<GameObject> parts = new List<GameObject>();

        public int WallCellCount { get; private set; }

        public void Build(FloorMap map, GameBalance balance)
        {
            Clear();

            var floor = new MeshData();
            var walls = new MeshData();
            var stairs = new MeshData();
            var cover = new MeshData();
            var chasm = new MeshData();
            var supply = new MeshData();
            var supplyOpened = new MeshData();
            WallCellCount = 0;

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    var position = GridSpace.ToWorld(cell);
                    switch (map.Get(cell))
                    {
                        case TileType.Floor:
                            floor.AddTop(position, 0.5f, 0.5f, 0f);
                            break;
                        case TileType.Stairs:
                            stairs.AddTop(position, 0.5f, 0.5f, 0f);
                            break;
                        case TileType.Crate:
                            floor.AddTop(position, 0.5f, 0.5f, 0f);
                            cover.AddBox(position, balance.CrateSize * 0.5f, balance.CrateSize * 0.5f, balance.LowCoverHeight);
                            break;
                        case TileType.Pillar:
                            floor.AddTop(position, 0.5f, 0.5f, 0f);
                            walls.AddBox(position, balance.PillarSize * 0.5f, balance.PillarSize * 0.5f, balance.WallHeight);
                            break;
                        case TileType.Chasm:
                            AddPit(chasm, map, cell, balance.ChasmDepth);
                            break;
                        case TileType.SupplyCrate:
                            floor.AddTop(position, 0.5f, 0.5f, 0f);
                            supply.AddBox(position, balance.CrateSize * 0.5f, balance.CrateSize * 0.35f, balance.LowCoverHeight);
                            break;
                        case TileType.SupplyCrateOpened:
                            floor.AddTop(position, 0.5f, 0.5f, 0f);
                            supplyOpened.AddBox(position, balance.CrateSize * 0.5f, balance.CrateSize * 0.35f, balance.LowCoverHeight * 0.6f);
                            break;
                        default:
                            if (!TouchesOpenCell(map, cell)) break;
                            AddWall(walls, map, cell, balance.WallHeight);
                            WallCellCount++;
                            break;
                    }

                    AddLowWalls(cover, map, cell, balance);
                }
            }

            AddPart("Floor", floor, floorMaterial);
            AddPart("Walls", walls, wallMaterial);
            AddPart("Stairs", stairs, stairsMaterial);
            AddPart("Cover", cover, coverMaterial);
            AddPart("Chasm", chasm, chasmMaterial != null ? chasmMaterial : wallMaterial);
            AddPart("Supply", supply, supplyMaterial != null ? supplyMaterial : coverMaterial);
            AddPart("SupplyOpened", supplyOpened, supplyOpenedMaterial != null ? supplyOpenedMaterial : coverMaterial);
        }

        /// <summary>낭떠러지: 바닥면 없이 깊이 depth 아래의 바닥과, 낭떠러지가 아닌 이웃 쪽 안벽.</summary>
        private static void AddPit(MeshData chasm, FloorMap map, GridPos cell, float depth)
        {
            var position = GridSpace.ToWorld(cell);
            chasm.AddTop(position, 0.5f, 0.5f, -depth);
            var below = position + Vector3.down * depth;
            if (!map.IsChasm(new GridPos(cell.X + 1, cell.Y))) chasm.AddFace(below, Vector3.left, -0.5f, 0.5f, depth);
            if (!map.IsChasm(new GridPos(cell.X - 1, cell.Y))) chasm.AddFace(below, Vector3.right, -0.5f, 0.5f, depth);
            if (!map.IsChasm(new GridPos(cell.X, cell.Y + 1))) chasm.AddFace(below, Vector3.back, 0.5f, -0.5f, depth);
            if (!map.IsChasm(new GridPos(cell.X, cell.Y - 1))) chasm.AddFace(below, Vector3.forward, 0.5f, -0.5f, depth);
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Clear()
        {
            foreach (var part in parts) if (part != null) Destroy(part);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            parts.Clear();
            meshes.Clear();
        }

        private void AddPart(string partName, MeshData data, Material material)
        {
            var mesh = data.ToMesh(partName);
            meshes.Add(mesh);

            var part = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(transform, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            // 재질 두 개 = 같은 메시를 두 번 그린다: 지형 재질 → 안개 덧그리기.
            part.GetComponent<MeshRenderer>().sharedMaterials = fogMaterial != null ? new[] { material, fogMaterial } : new[] { material };
            parts.Add(part);
        }

        /// <summary>벽이 아닌 칸(바닥 · 계단 · 엄폐물)이 주변에 있으면 그 벽을 그린다.</summary>
        private static bool TouchesOpenCell(FloorMap map, GridPos cell)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (IsOpen(map, new GridPos(cell.X + dx, cell.Y + dy))) return true;
            return false;
        }

        private static bool IsOpen(FloorMap map, GridPos cell) => map.InBounds(cell) && map.Get(cell) != TileType.Wall;

        private static void AddWall(MeshData walls, FloorMap map, GridPos cell, float height)
        {
            var position = GridSpace.ToWorld(cell);
            walls.AddTop(position, 0.5f, 0.5f, height);
            if (IsOpen(map, new GridPos(cell.X + 1, cell.Y))) walls.AddFace(position, Vector3.right, 0.5f, 0.5f, height);
            if (IsOpen(map, new GridPos(cell.X - 1, cell.Y))) walls.AddFace(position, Vector3.left, 0.5f, 0.5f, height);
            if (IsOpen(map, new GridPos(cell.X, cell.Y + 1))) walls.AddFace(position, Vector3.forward, 0.5f, 0.5f, height);
            if (IsOpen(map, new GridPos(cell.X, cell.Y - 1))) walls.AddFace(position, Vector3.back, 0.5f, 0.5f, height);
        }

        /// <summary>칸의 오른쪽 · 위쪽 변에 놓인 낮은 벽을 얇은 상자로 그린다.</summary>
        private static void AddLowWalls(MeshData cover, FloorMap map, GridPos cell, GameBalance balance)
        {
            float halfThickness = balance.LowWallThickness * 0.5f;
            var position = GridSpace.ToWorld(cell);
            if (map.HasLowWallEast(cell))
                cover.AddBox(position + new Vector3(0.5f, 0f, 0f), halfThickness, 0.5f, balance.LowCoverHeight);
            if (map.HasLowWallNorth(cell))
                cover.AddBox(position + new Vector3(0f, 0f, 0.5f), 0.5f, halfThickness, balance.LowCoverHeight);
        }

        private sealed class MeshData
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();

            /// <summary>가운데가 center이고 반폭이 halfX · halfZ인 윗면.</summary>
            public void AddTop(Vector3 center, float halfX, float halfZ, float height)
            {
                AddQuad(
                    new Vector3(center.x - halfX, height, center.z - halfZ),
                    new Vector3(center.x - halfX, height, center.z + halfZ),
                    new Vector3(center.x + halfX, height, center.z + halfZ),
                    new Vector3(center.x + halfX, height, center.z - halfZ),
                    Vector3.up);
            }

            /// <summary>상자의 facing 쪽 옆면. halfX · halfZ는 상자 반폭.</summary>
            public void AddFace(Vector3 center, Vector3 facing, float halfX, float halfZ, float height)
            {
                float halfDepth = Mathf.Abs(facing.x) > 0f ? halfX : halfZ;
                float halfWidth = Mathf.Abs(facing.x) > 0f ? halfZ : halfX;
                var faceCenter = new Vector3(center.x, center.y, center.z) + facing * halfDepth;
                // 바깥에서 볼 때 시계 방향이어야 면이 그려진다.
                var along = Vector3.Cross(facing, Vector3.up) * Mathf.Abs(halfWidth);
                var up = Vector3.up * height;
                AddQuad(faceCenter - along, faceCenter - along + up, faceCenter + along + up, faceCenter + along, facing);
            }

            public void AddBox(Vector3 center, float halfX, float halfZ, float height)
            {
                AddTop(center, halfX, halfZ, height);
                AddFace(center, Vector3.right, halfX, halfZ, height);
                AddFace(center, Vector3.left, halfX, halfZ, height);
                AddFace(center, Vector3.forward, halfX, halfZ, height);
                AddFace(center, Vector3.back, halfX, halfZ, height);
            }

            public Mesh ToMesh(string meshName)
            {
                var mesh = new Mesh { name = meshName };
                if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }

            private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int start = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                vertices.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
        }
    }
}
