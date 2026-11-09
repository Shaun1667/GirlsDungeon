using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Map;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>층 지형을 그레이박스 메시로 그린다. 규칙은 모르고 FloorMap만 읽는다.</summary>
    public sealed class FloorView : MonoBehaviour
    {
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material wallMaterial;
        [SerializeField] private Material stairsMaterial;

        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<GameObject> parts = new List<GameObject>();

        public int WallCellCount { get; private set; }

        public void Build(FloorMap map, float wallHeight)
        {
            Clear();

            var floor = new MeshData();
            var walls = new MeshData();
            var stairs = new MeshData();
            WallCellCount = 0;

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    switch (map.Get(cell))
                    {
                        case TileType.Floor:
                            floor.AddTop(x, y, 0f);
                            break;
                        case TileType.Stairs:
                            stairs.AddTop(x, y, 0f);
                            break;
                        default:
                            if (!TouchesWalkable(map, cell)) break;
                            AddWall(walls, map, cell, wallHeight);
                            WallCellCount++;
                            break;
                    }
                }
            }

            AddPart("Floor", floor, floorMaterial);
            AddPart("Walls", walls, wallMaterial);
            AddPart("Stairs", stairs, stairsMaterial);
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
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            parts.Add(part);
        }

        private static bool TouchesWalkable(FloorMap map, GridPos cell)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (map.IsWalkable(new GridPos(cell.X + dx, cell.Y + dy))) return true;
            return false;
        }

        private static void AddWall(MeshData walls, FloorMap map, GridPos cell, float height)
        {
            walls.AddTop(cell.X, cell.Y, height);
            if (map.IsWalkable(new GridPos(cell.X + 1, cell.Y))) walls.AddSide(cell.X, cell.Y, height, Vector3.right);
            if (map.IsWalkable(new GridPos(cell.X - 1, cell.Y))) walls.AddSide(cell.X, cell.Y, height, Vector3.left);
            if (map.IsWalkable(new GridPos(cell.X, cell.Y + 1))) walls.AddSide(cell.X, cell.Y, height, Vector3.forward);
            if (map.IsWalkable(new GridPos(cell.X, cell.Y - 1))) walls.AddSide(cell.X, cell.Y, height, Vector3.back);
        }

        private sealed class MeshData
        {
            private const float Half = 0.5f;

            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();

            public void AddTop(int x, int y, float height)
            {
                AddQuad(
                    new Vector3(x - Half, height, y - Half),
                    new Vector3(x - Half, height, y + Half),
                    new Vector3(x + Half, height, y + Half),
                    new Vector3(x + Half, height, y - Half),
                    Vector3.up);
            }

            public void AddSide(int x, int y, float height, Vector3 facing)
            {
                var center = new Vector3(x, 0f, y) + facing * Half;
                // 바깥에서 볼 때 시계 방향이어야 면이 그려진다.
                var along = Vector3.Cross(facing, Vector3.up) * Half;
                var up = Vector3.up * height;
                AddQuad(center - along, center - along + up, center + along + up, center + along, facing);
            }

            public Mesh ToMesh(string meshName)
            {
                var mesh = new Mesh { name = meshName };
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
