using GirlsDungeon.Core.Grid;
using GirlsDungeon.Game;
using UnityEditor;
using UnityEngine;

namespace GirlsDungeon.EditorTools
{
    /// <summary>
    /// 개발자 디버그 창. 시스템이 생길 때마다 섹션을 하나 더한다.
    /// 게임 쪽 진입점은 GameRoot.DebugXxx, 상태를 바꾸는 기능에는 [상태 변경]을 붙인다.
    /// </summary>
    public sealed class GirlsDungeonDebugWindow : EditorWindow
    {
        private Vector2 scroll;
        private int seedInput;
        private Vector2Int moveTarget;

        [MenuItem("GirlsDungeon/Debug/Debug Window")]
        private static void Open()
        {
            GetWindow<GirlsDungeonDebugWindow>("GirlsDungeon Debug");
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 쓸 수 있습니다. 씬: Assets/_Game/Scenes/Game.unity", MessageType.Info);
            }
            else
            {
                var root = FindAnyObjectByType<GameRoot>();
                if (root == null)
                {
                    EditorGUILayout.HelpBox("지금 씬에 GameRoot가 없습니다.", MessageType.Warning);
                }
                else
                {
                    DrawRunSection(root);
                    EditorGUILayout.Space();
                    DrawMapSection(root);
                    EditorGUILayout.Space();
                    DrawMoveSection(root);
                    EditorGUILayout.Space();
                    DrawCombatSection(root);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawRunSection(GameRoot root)
        {
            EditorGUILayout.LabelField("판", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("상태", root.DebugStateSummary(), EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("새 시드로 재시작 [상태 변경]")) root.DebugRestart();

            EditorGUILayout.BeginHorizontal();
            seedInput = EditorGUILayout.IntField("시드", seedInput);
            if (GUILayout.Button("이 시드로 재시작 [상태 변경]")) root.DebugRestartWithSeed(seedInput);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMapSection(GameRoot root)
        {
            EditorGUILayout.LabelField("맵", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("요약", root.DebugMapSummary(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("시야", root.DebugVisionSummary(), EditorStyles.wordWrappedLabel);
            root.DebugRevealAll = EditorGUILayout.Toggle("맵 전체 밝히기 (표시만)", root.DebugRevealAll);
            EditorGUILayout.LabelField("칸 엄폐 (아래 '칸' 기준)", root.DebugCoverAt(new GridPos(moveTarget.x, moveTarget.y)), EditorStyles.wordWrappedLabel);
        }

        private static void DrawCombatSection(GameRoot root)
        {
            EditorGUILayout.LabelField("전투", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("상태", root.DebugCombatSummary(), EditorStyles.wordWrappedLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("턴 종료 [상태 변경]")) root.DebugEndTurn();
            if (GUILayout.Button("적 하나 처치 [상태 변경]")) root.DebugDefeatEnemy();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("유닛", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(root.DebugUnitsSummary(), EditorStyles.wordWrappedLabel);
        }

        private void DrawMoveSection(GameRoot root)
        {
            EditorGUILayout.LabelField("이동", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            moveTarget = EditorGUILayout.Vector2IntField("칸", moveTarget);
            if (GUILayout.Button("이 칸으로 걷기 [상태 변경]")) root.DebugMoveTo(new GridPos(moveTarget.x, moveTarget.y));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("카메라 왼쪽 [상태 변경]")) root.DebugRotateCamera(-1);
            if (GUILayout.Button("카메라 오른쪽 [상태 변경]")) root.DebugRotateCamera(1);
            EditorGUILayout.EndHorizontal();
        }
    }
}
