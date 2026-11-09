using GirlsDungeon.Core.Grid;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>칸 좌표 ↔ 3D 위치 변환의 한 곳. 칸의 가운데가 정수 좌표이고 바닥 높이는 0이다.</summary>
    public static class GridSpace
    {
        public static Vector3 ToWorld(GridPos cell) => new Vector3(cell.X, 0f, cell.Y);

        public static GridPos ToCell(Vector3 world) => new GridPos(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
    }
}
