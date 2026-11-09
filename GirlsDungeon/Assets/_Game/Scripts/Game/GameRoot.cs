using System;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>씬 연결의 한 곳(컴포지션 루트). 새 판 · 이어 하기는 모두 여기서 시작한다.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private GameBalance balance;
        [SerializeField] private UnityEngine.UI.Text seedLabel;

        public int Seed { get; private set; }
        public bool RunStarted { get; private set; }

        /// <summary>디버그 기능을 쓴 판은 기록에 남기지 않는다.</summary>
        public bool DebugUsed { get; private set; }

        private void Start()
        {
            BeginRun(PickSeed());
        }

        public void BeginRun(int seed)
        {
            Seed = seed;
            RunStarted = true;
            if (seedLabel != null) seedLabel.text = Text.Get("ui.hud.seed", Seed);
        }

        /// <summary>[상태 변경] 새 시드로 판을 다시 시작한다.</summary>
        public void DebugRestart()
        {
            DebugUsed = true;
            BeginRun(PickSeed());
        }

        public string DebugStateSummary()
        {
            return $"RunStarted={RunStarted}, Seed={Seed}, DebugUsed={DebugUsed}, Language={Text.Language}";
        }

        private int PickSeed()
        {
            if (balance != null && balance.FixedSeed != 0) return balance.FixedSeed;
            int seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            return seed == 0 ? 1 : seed;
        }
    }
}
