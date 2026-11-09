using System;
using GirlsDungeon.Core.Grid;
using GirlsDungeon.Core.Sim;
using UnityEngine;

namespace GirlsDungeon.Game
{
    /// <summary>씬 연결의 한 곳(컴포지션 루트). 새 판 · 이어 하기는 모두 여기서 시작한다.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] private GameBalance balance;
        [SerializeField] private UnityEngine.UI.Text seedLabel;
        [SerializeField] private FloorView floorView;
        [SerializeField] private PlayerView playerView;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private MoveController moveController;

        private Simulation simulation;

        public World World => simulation?.World;

        /// <summary>디버그 기능을 쓴 판은 기록에 남기지 않는다.</summary>
        public bool DebugUsed { get; private set; }

        private void Start()
        {
            BeginRun(PickSeed());
        }

        public void BeginRun(int seed)
        {
            simulation = new Simulation(RunFactory.NewRun(seed, balance.CreateMapSettings()));

            floorView.Build(simulation.World.Map, balance.WallHeight);
            playerView.Bind(simulation, balance.StepSeconds);
            moveController.Bind(simulation, playerView, balance.MoveCostTicks);
            cameraRig.Bind(balance, playerView.transform);

            if (seedLabel != null) seedLabel.text = Text.Get("ui.hud.seed", seed);
        }

        /// <summary>[상태 변경] 새 시드로 판을 다시 시작한다.</summary>
        public void DebugRestart()
        {
            DebugUsed = true;
            BeginRun(PickSeed());
        }

        /// <summary>[상태 변경] 정한 시드로 판을 다시 시작한다.</summary>
        public void DebugRestartWithSeed(int seed)
        {
            DebugUsed = true;
            BeginRun(seed);
        }

        /// <summary>[상태 변경] 칸을 클릭한 것과 같게 걷기 시작한다. 갈 수 없으면 false.</summary>
        public bool DebugMoveTo(GridPos cell)
        {
            DebugUsed = true;
            return moveController.RequestMove(cell);
        }

        /// <summary>[상태 변경] 카메라를 돌린다. Q/E와 같다.</summary>
        public void DebugRotateCamera(int direction)
        {
            cameraRig.Rotate(direction);
        }

        public string DebugStateSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            return $"Seed={world.Seed}, Tick={world.Tick}, Floor={world.FloorIndex}, Player={world.Player.Position}, "
                + $"Pending={moveController.PendingStepCount}, DebugUsed={DebugUsed}, Language={Text.Language}";
        }

        public string DebugMapSummary()
        {
            var world = World;
            if (world == null) return "Run not started";
            var map = world.Map;
            return $"Size={map.Width}x{map.Height}, Rooms={map.Rooms.Count}, Walkable={map.CountWalkable()}, "
                + $"Start={map.StartCell}, Stairs={map.StairsCell}";
        }

        private int PickSeed()
        {
            if (balance.FixedSeed != 0) return balance.FixedSeed;
            int seed = Guid.NewGuid().GetHashCode() & int.MaxValue;
            return seed == 0 ? 1 : seed;
        }
    }
}
