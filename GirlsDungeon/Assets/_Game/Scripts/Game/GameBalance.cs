using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>게임 수치의 한 곳 (BALANCE.md와 함께 관리). 코드에 숫자를 직접 넣지 않는다.</summary>
    [CreateAssetMenu(fileName = "GameBalance", menuName = "GirlsDungeon/Game Balance")]
    public sealed class GameBalance : ScriptableObject
    {
        [Tooltip("0이면 판마다 새 시드를 뽑고, 0이 아니면 이 시드로 고정한다.")]
        [SerializeField] private int fixedSeed;

        public int FixedSeed => fixedSeed;
    }
}
