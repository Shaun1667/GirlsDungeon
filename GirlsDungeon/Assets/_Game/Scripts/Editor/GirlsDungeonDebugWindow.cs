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
        private int lastDefeated = -1;

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
                    EditorGUILayout.Space();
                    DrawLootSection(root);
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

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("계단으로 걷기 [상태 변경]")) root.DebugWalkToStairs();
            if (GUILayout.Button("바로 다음 층 [상태 변경]")) root.DebugDescend();
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

        private void DrawCombatSection(GameRoot root)
        {
            EditorGUILayout.LabelField("전투", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("상태", root.DebugCombatSummary(), EditorStyles.wordWrappedLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("턴 종료 [상태 변경]")) root.DebugEndTurn();
            if (GUILayout.Button("적 하나 처치 [상태 변경]")) root.DebugDefeatEnemy();
            if (GUILayout.Button("첫 적 사격 [상태 변경]")) root.DebugShootFirstCombatant();
            EditorGUILayout.EndHorizontal();
            // 서 있는 방의 적 모두 (복도면 전투 중인 적 모두) — 2026-11-14.
            if (GUILayout.Button("현재 방의 적 모두 처치 [상태 변경]")) lastDefeated = root.DebugDefeatEnemiesInRoom();
            if (lastDefeated >= 0) EditorGUILayout.LabelField("방금 처치", lastDefeated + "명");
            if (GUILayout.Button("위 '칸'으로 전투 이동 · 사격 [상태 변경]")) root.DebugCombatAction(new GridPos(moveTarget.x, moveTarget.y));
            if (GUILayout.Button("위 '칸'에 수류탄 [상태 변경]")) root.DebugThrowGrenade(new GridPos(moveTarget.x, moveTarget.y));
            if (GUILayout.Button("감시 걸기 [상태 변경]")) root.DebugOverwatch();
            EditorGUILayout.LabelField("경계 · 감시", root.DebugThreatSummary(), EditorStyles.wordWrappedLabel);

            EditorGUILayout.LabelField("유닛", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(root.DebugUnitsSummary(), EditorStyles.wordWrappedLabel);
        }

        private void DrawLootSection(GameRoot root)
        {
            EditorGUILayout.LabelField("무기 · 보상", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("상태", root.DebugLootSummary(), EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("가장 가까운 보급 상자 열기 [상태 변경]")) root.DebugOpenNearestCrate();

            EditorGUILayout.BeginHorizontal();
            weaponToDrop = (GirlsDungeon.Core.Sim.WeaponKind)EditorGUILayout.EnumPopup("무기", weaponToDrop);
            if (GUILayout.Button("옆에 놓기 [상태 변경]")) root.DebugDropItem(GirlsDungeon.Core.Sim.ItemKind.Weapon, weaponToDrop);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("옆에 구급상자 [상태 변경]")) root.DebugDropItem(GirlsDungeon.Core.Sim.ItemKind.Medkit, default);
            if (GUILayout.Button("비교 창 바꾸기 [상태 변경]")) root.DebugPressSwap();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("파츠", root.DebugPartsSummary(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("세트", root.DebugSetSummary(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            setToEquip = (GirlsDungeon.Core.Sim.PartSet)EditorGUILayout.EnumPopup("세트", setToEquip);
            if (GUILayout.Button("2 · 3 · 4번 칸에 끼우기 [상태 변경]")) root.DebugEquipSet(setToEquip);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("옆에 파츠 놓기 [상태 변경]")) root.DebugDropPart();

            EditorGUILayout.LabelField("특수 방", root.DebugRoomsSummary(), EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            roomToVisit = (GirlsDungeon.Core.Map.SpecialRoomKind)EditorGUILayout.EnumPopup("방", roomToVisit);
            if (GUILayout.Button("방 안으로 이동 [상태 변경]")) root.DebugGoToSpecialRoom(roomToVisit);
            EditorGUILayout.EndHorizontal();
            // 상점 · 사르디스 골드 (3-15).
            EditorGUILayout.LabelField("상점", root.DebugShopSummary(), EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("사르디스 골드 +100 [상태 변경]")) root.DebugAddGold(100);
            // 리몰딩 지수 · 이동 감소 (3-16).
            EditorGUILayout.LabelField("리몰딩", root.DebugRemoldSummary(), EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("리몰딩 지수 최대 [상태 변경]")) root.DebugFillRemold();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("열쇠 +1 [상태 변경]")) root.DebugAddKeys(1);
            if (GUILayout.Button("잠긴 문 앞으로 [상태 변경]")) root.DebugGoToLockedDoor();
            EditorGUILayout.EndHorizontal();
        }

        private GirlsDungeon.Core.Map.SpecialRoomKind roomToVisit = GirlsDungeon.Core.Map.SpecialRoomKind.Treasure;

        private GirlsDungeon.Core.Sim.PartSet setToEquip = GirlsDungeon.Core.Sim.PartSet.Assault;

        private GirlsDungeon.Core.Sim.WeaponKind weaponToDrop = GirlsDungeon.Core.Sim.WeaponKind.Shotgun;

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
