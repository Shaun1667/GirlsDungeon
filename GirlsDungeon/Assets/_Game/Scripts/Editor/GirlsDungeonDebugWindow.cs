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
            DrawRunSection();
            EditorGUILayout.EndScrollView();
        }

        private static void DrawRunSection()
        {
            EditorGUILayout.LabelField("판", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서 쓸 수 있습니다. 씬: Assets/_Game/Scenes/Game.unity", MessageType.Info);
                return;
            }

            var root = FindAnyObjectByType<GameRoot>();
            if (root == null)
            {
                EditorGUILayout.HelpBox("지금 씬에 GameRoot가 없습니다.", MessageType.Warning);
                return;
            }

            EditorGUILayout.SelectableLabel($"시드: {root.Seed}", GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.LabelField("상태", root.DebugStateSummary(), EditorStyles.wordWrappedLabel);

            if (GUILayout.Button("재시작 [상태 변경]")) root.DebugRestart();
        }
    }
}
