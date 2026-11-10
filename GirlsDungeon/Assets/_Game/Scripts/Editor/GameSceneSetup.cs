using System.IO;
using GirlsDungeon.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GirlsDungeon.EditorTools
{
    /// <summary>게임 씬과 데이터 에셋을 만든다. 몇 번 실행해도 같은 구성이 나온다. 씬 구성을 바꾸려면 이 코드를 고친다.</summary>
    public static class GameSceneSetup
    {
        private const string BalancePath = "Assets/_Game/Data/GameBalance.asset";
        private const string MaterialFolder = "Assets/_Game/Data/Materials";
        private const string ScenePath = "Assets/_Game/Scenes/Game.unity";
        private const string LitShaderName = "Universal Render Pipeline/Lit";

        [MenuItem("GirlsDungeon/Setup/Build Game Scene")]
        public static void BuildGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 새 씬은 참조 없는 에셋을 메모리에서 내린다. 에셋은 반드시 NewScene 뒤에 불러온다.
            var balance = LoadOrCreateBalance();
            var floorMaterial = LoadOrCreateMaterial("GreyboxFloor", new Color(0.42f, 0.44f, 0.47f));
            var wallMaterial = LoadOrCreateMaterial("GreyboxWall", new Color(0.2f, 0.22f, 0.27f));
            var stairsMaterial = LoadOrCreateMaterial("GreyboxStairs", new Color(0.95f, 0.72f, 0.2f));
            var playerMaterial = LoadOrCreateMaterial("GreyboxPlayer", new Color(0.92f, 0.36f, 0.5f));
            var hoverMaterial = LoadOrCreateMaterial("GreyboxHover", new Color(0.35f, 0.85f, 0.95f));
            var coverMaterial = LoadOrCreateMaterial("GreyboxCover", new Color(0.55f, 0.47f, 0.36f));
            var enemyMaterial = LoadOrCreateMaterial("GreyboxEnemy", new Color(0.85f, 0.25f, 0.2f));
            var fogMaterial = LoadOrCreateFogMaterial();
            var rangeMaterial = LoadOrCreateMaterial("GreyboxRange", new Color(0.3f, 0.6f, 0.95f));
            var areaMaterial = LoadOrCreateMaterial("GreyboxArea", new Color(0.95f, 0.45f, 0.15f));
            var guardMaterial = LoadOrCreateMaterial("GreyboxGuard", new Color(0.95f, 0.85f, 0.2f));
            var watchMaterial = LoadOrCreateMaterial("GreyboxWatch", new Color(0.85f, 0.15f, 0.2f));
            var chasmMaterial = LoadOrCreateMaterial("GreyboxChasm", new Color(0.05f, 0.05f, 0.08f));
            var pushMaterial = LoadOrCreateMaterial("GreyboxPush", new Color(0.7f, 0.35f, 0.95f));

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = CreateCanvas();
            var seedLabel = CreateLabel(canvas.transform, "SeedLabel", font, 28,
                new Vector2(0f, 1f), new Vector2(24f, -20f), TextAnchor.UpperLeft);
            var hpLabel = CreateLabel(canvas.transform, "HpLabel", font, 30,
                new Vector2(0f, 1f), new Vector2(24f, -64f), TextAnchor.UpperLeft);
            var stabilityLabel = CreateLabel(canvas.transform, "StabilityLabel", font, 30,
                new Vector2(0f, 1f), new Vector2(24f, -104f), TextAnchor.UpperLeft);
            var roundLabel = CreateLabel(canvas.transform, "RoundLabel", font, 34,
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), TextAnchor.UpperCenter);
            var turnLabel = CreateLabel(canvas.transform, "TurnLabel", font, 30,
                new Vector2(0.5f, 1f), new Vector2(0f, -64f), TextAnchor.UpperCenter);
            var controlsLabel = CreateLabel(canvas.transform, "ControlsLabel", font, 26,
                new Vector2(0f, 0f), new Vector2(24f, 20f), TextAnchor.LowerLeft);
            controlsLabel.gameObject.AddComponent<TextLabel>().TextKey = "ui.hud.controls";
            var previewLabel = CreateLabel(canvas.transform, "PreviewLabel", font, 28,
                new Vector2(0.5f, 0f), new Vector2(0f, 120f), TextAnchor.LowerCenter);
            previewLabel.rectTransform.sizeDelta = new Vector2(1200f, 90f);
            var logLabel = CreateLabel(canvas.transform, "LogLabel", font, 24,
                new Vector2(0f, 0f), new Vector2(24f, 64f), TextAnchor.LowerLeft);
            logLabel.rectTransform.sizeDelta = new Vector2(700f, 140f);
            var defeatLabel = CreateLabel(canvas.transform, "DefeatLabel", font, 64,
                new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);
            defeatLabel.color = new Color(1f, 0.35f, 0.3f);
            var endTurnButton = CreateButton(canvas.transform, "EndTurnButton", font, "ui.combat.end_turn",
                new Vector2(1f, 0f), new Vector2(-32f, 32f));
            var grenadeButton = CreateButton(canvas.transform, "GrenadeButton", font, "ui.skill.grenade",
                new Vector2(1f, 0f), new Vector2(-32f, 132f));
            grenadeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 84f);
            var pushButton = CreateButton(canvas.transform, "PushButton", font, "ui.skill.push",
                new Vector2(1f, 0f), new Vector2(-32f, 232f));
            pushButton.GetComponent<RectTransform>().sizeDelta = new Vector2(420f, 84f);
            var overwatchButton = CreateButton(canvas.transform, "OverwatchButton", font, "ui.skill.overwatch",
                new Vector2(1f, 0f), new Vector2(-32f, 332f));
            CreateEventSystem();

            var hudView = canvas.gameObject.AddComponent<HudView>();
            SetReferences(hudView, ("hpLabel", hpLabel), ("stabilityLabel", stabilityLabel), ("roundLabel", roundLabel),
                ("turnLabel", turnLabel), ("previewLabel", previewLabel), ("logLabel", logLabel), ("defeatLabel", defeatLabel),
                ("endTurnButton", endTurnButton), ("grenadeButton", grenadeButton), ("pushButton", pushButton), ("overwatchButton", overwatchButton));

            var rangeObject = new GameObject("RangeView");
            var rangeView = rangeObject.AddComponent<RangeView>();
            SetReferences(rangeView, ("rangeMaterial", rangeMaterial));
            var areaObject = new GameObject("AreaView");
            var areaView = areaObject.AddComponent<RangeView>();
            SetReferences(areaView, ("rangeMaterial", areaMaterial));
            var pushAreaView = new GameObject("PushAreaView").AddComponent<RangeView>();
            SetReferences(pushAreaView, ("rangeMaterial", pushMaterial));

            var threatObject = new GameObject("ThreatView");
            var guardView = new GameObject("GuardCells").AddComponent<RangeView>();
            guardView.transform.SetParent(threatObject.transform, false);
            SetReferences(guardView, ("rangeMaterial", guardMaterial));
            // 위협 칸은 이동 범위 칸(0.015)보다 아래에 깔아 겹쳐도 깜빡이지 않게 한다.
            SetFloat(guardView, "height", 0.008f);
            var watchView = new GameObject("WatchCells").AddComponent<RangeView>();
            watchView.transform.SetParent(threatObject.transform, false);
            SetReferences(watchView, ("rangeMaterial", watchMaterial));
            SetFloat(watchView, "height", 0.011f);
            SetFloat(areaView, "height", 0.02f);
            SetFloat(pushAreaView, "height", 0.02f);
            var threatView = threatObject.AddComponent<ThreatView>();
            SetReferences(threatView, ("guardView", guardView), ("watchView", watchView));

            var unitsObject = new GameObject("UnitsView");
            var unitsView = unitsObject.AddComponent<UnitsView>();
            SetReferences(unitsView, ("enemyMaterial", enemyMaterial));

            var floorObject = new GameObject("FloorView");
            var floorView = floorObject.AddComponent<FloorView>();
            SetReferences(floorView, ("floorMaterial", floorMaterial), ("wallMaterial", wallMaterial), ("stairsMaterial", stairsMaterial), ("coverMaterial", coverMaterial), ("chasmMaterial", chasmMaterial), ("fogMaterial", fogMaterial));
            var fogView = floorObject.AddComponent<FogView>();

            var playerObject = new GameObject("PlayerView");
            var playerView = playerObject.AddComponent<PlayerView>();
            var capsule = CreateVisual(PrimitiveType.Capsule, "Capsule", playerObject.transform, playerMaterial);
            capsule.transform.localPosition = Vector3.up;

            var hover = CreateVisual(PrimitiveType.Quad, "HoverMarker", null, hoverMaterial);
            hover.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            hover.SetActive(false);

            var cameraObject = Camera.main.gameObject;
            // 안 본 칸(검정)과 맵 밖이 이어지게 배경을 검정으로.
            var viewCameraComponent = cameraObject.GetComponent<Camera>();
            viewCameraComponent.clearFlags = CameraClearFlags.SolidColor;
            viewCameraComponent.backgroundColor = Color.black;
            var cameraRig = cameraObject.AddComponent<CameraRig>();

            var rootObject = new GameObject("GameRoot");
            var moveController = rootObject.AddComponent<MoveController>();
            SetReferences(moveController, ("viewCamera", cameraObject.GetComponent<Camera>()), ("hoverMarker", hover.transform),
                ("rangeView", rangeView), ("areaView", areaView), ("pushAreaView", pushAreaView));

            var root = rootObject.AddComponent<GameRoot>();
            SetReferences(root,
                ("balance", balance),
                ("seedLabel", seedLabel),
                ("floorView", floorView),
                ("playerView", playerView),
                ("cameraRig", cameraRig),
                ("moveController", moveController),
                ("unitsView", unitsView),
                ("hudView", hudView),
                ("fogView", fogView),
                ("threatView", threatView));

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"Game scene built: {ScenePath}");
        }

        private static Button CreateButton(Transform parent, string objectName, Font font, string textKey, Vector2 corner, Vector2 offset)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.sizeDelta = new Vector2(260f, 84f);
            rect.anchoredPosition = offset;

            var image = buttonObject.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = new Color(0.16f, 0.18f, 0.24f, 0.95f);

            var label = CreateLabel(buttonObject.transform, "Label", font, 32, new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = Vector2.zero;
            label.gameObject.AddComponent<TextLabel>().TextKey = textKey;
            return buttonObject.GetComponent<Button>();
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReferences(Object target, params (string field, Object value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in references)
                serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameBalance LoadOrCreateBalance()
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(BalancePath);
            if (balance == null)
            {
                EnsureFolder(Path.GetDirectoryName(BalancePath).Replace('\\', '/'));
                balance = ScriptableObject.CreateInstance<GameBalance>();
                AssetDatabase.CreateAsset(balance, BalancePath);
            }

            // 새로 생긴 필드의 값도 에셋 파일에 기록한다.
            EditorUtility.SetDirty(balance);
            return balance;
        }

        private static Material LoadOrCreateMaterial(string materialName, Color color)
        {
            var path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder(MaterialFolder);
                material = new Material(Shader.Find(LitShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateFogMaterial()
        {
            var path = $"{MaterialFolder}/FogOverlay.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            EnsureFolder(MaterialFolder);
            material = new Material(Shader.Find("GirlsDungeon/FogOverlay"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            Directory.CreateDirectory(folder);
            AssetDatabase.ImportAsset(folder);
        }

        private static GameObject CreateVisual(PrimitiveType type, string objectName, Transform parent, Material material)
        {
            var visual = GameObject.CreatePrimitive(type);
            visual.name = objectName;
            if (parent != null) visual.transform.SetParent(parent, false);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;
            return visual;
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

        private static UnityEngine.UI.Text CreateLabel(Transform parent, string objectName, Font font, int size,
            Vector2 corner, Vector2 offset, TextAnchor alignment)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            labelObject.transform.SetParent(parent, false);

            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.sizeDelta = new Vector2(1400f, 60f);
            rect.anchoredPosition = offset;

            var label = labelObject.GetComponent<UnityEngine.UI.Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }
    }
}
