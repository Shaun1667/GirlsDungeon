using System.Collections.Generic;
using GirlsDungeon.Core.Grid;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>전투 이동 범위 칸 표시. 칸 목록은 규칙(CombatMovement.Reachable)에서 받는다. 사각형을 재사용한다.</summary>
    public sealed class RangeView : MonoBehaviour
    {
        [SerializeField] private Material rangeMaterial;
        [SerializeField] private float height = 0.015f;
        [SerializeField] private float tileSize = 0.9f;

        private readonly List<GameObject> tiles = new List<GameObject>();

        public int ShownCount { get; private set; }

        public void Show(IEnumerable<GridPos> cells)
        {
            int index = 0;
            foreach (var cell in cells)
            {
                if (index == tiles.Count) tiles.Add(CreateTile());
                var tile = tiles[index++];
                tile.transform.position = GridSpace.ToWorld(cell) + Vector3.up * height;
                tile.SetActive(true);
            }
            for (int i = index; i < tiles.Count; i++) tiles[i].SetActive(false);
            ShownCount = index;
        }

        public void Hide()
        {
            foreach (var tile in tiles) tile.SetActive(false);
            ShownCount = 0;
        }

        private GameObject CreateTile()
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tile.name = "RangeTile";
            Destroy(tile.GetComponent<Collider>());
            tile.GetComponent<MeshRenderer>().sharedMaterial = rangeMaterial;
            tile.transform.SetParent(transform, false);
            tile.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            tile.transform.localScale = new Vector3(tileSize, tileSize, 1f);
            return tile;
        }
    }
}
