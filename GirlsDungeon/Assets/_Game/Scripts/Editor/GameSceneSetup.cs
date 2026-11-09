using System.IO;
using GirlsDungeon.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.EditorTools
{
    /// <summary>게임 씬과 데이터 에셋을 만든다. 몇 번 실행해도 같은 결과가 나온다.</summary>
    public static class GameSceneSetup
    {
        private const string BalancePath = "Assets/_Game/Data/GameBalance.asset";
        private const string ScenePath = "Assets/_Game/Scenes/Game.unity";

        [MenuItem("GirlsDungeon/Setup/Build Game Scene")]
        public static void BuildGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var balance = LoadOrCreateBalance();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = CreateCanvas();
            var title = CreateLabel(canvas.transform, "ReadyLabel", font, 48, new Vector2(0f, 40f));
            title.gameObject.AddComponent<TextLabel>().TextKey = "ui.dev.ready";
            var seed = CreateLabel(canvas.transform, "SeedLabel", font, 32, new Vector2(0f, -40f));

            var rootObject = new GameObject("GameRoot");
            var root = rootObject.AddComponent<GameRoot>();
            var serialized = new SerializedObject(root);
            serialized.FindProperty("balance").objectReferenceValue = balance;
            serialized.FindProperty("seedLabel").objectReferenceValue = seed;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Game scene built: {ScenePath}");
        }

        private static GameBalance LoadOrCreateBalance()
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(BalancePath);
            if (balance != null) return balance;

            Directory.CreateDirectory(Path.GetDirectoryName(BalancePath));
            AssetDatabase.Refresh();
            balance = ScriptableObject.CreateInstance<GameBalance>();
            AssetDatabase.CreateAsset(balance, BalancePath);
            return balance;
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static UnityEngine.UI.Text CreateLabel(Transform parent, string name, Font font, int size, Vector2 position)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            labelObject.transform.SetParent(parent, false);

            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1400f, 80f);
            rect.anchoredPosition = position;

            var label = labelObject.GetComponent<UnityEngine.UI.Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }
    }
}
