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
            var assaultMaterial = LoadOrCreateMaterial("GreyboxAssault", new Color(0.95f, 0.6f, 0.1f));
            var fogMaterial = LoadOrCreateFogMaterial();
            var rangeMaterial = LoadOrCreateMaterial("GreyboxRange", new Color(0.3f, 0.6f, 0.95f));
            var areaMaterial = LoadOrCreateMaterial("GreyboxArea", new Color(0.95f, 0.45f, 0.15f));
            var guardMaterial = LoadOrCreateMaterial("GreyboxGuard", new Color(0.95f, 0.85f, 0.2f));
            var watchMaterial = LoadOrCreateMaterial("GreyboxWatch", new Color(0.85f, 0.15f, 0.2f));
            var chasmMaterial = LoadOrCreateMaterial("GreyboxChasm", new Color(0.05f, 0.05f, 0.08f));
            var pushMaterial = LoadOrCreateMaterial("GreyboxPush", new Color(0.7f, 0.35f, 0.95f));
            var supplyMaterial = LoadOrCreateMaterial("GreyboxSupply", new Color(0.25f, 0.65f, 0.35f));
            var supplyOpenedMaterial = LoadOrCreateMaterial("GreyboxSupplyOpened", new Color(0.12f, 0.3f, 0.17f));
            var weaponItemMaterial = LoadOrCreateMaterial("GreyboxWeaponItem", new Color(0.15f, 0.85f, 0.95f));
            var medkitMaterial = LoadOrCreateMaterial("GreyboxMedkit", new Color(0.95f, 0.95f, 0.95f));
            var tracerMaterial = LoadOrCreateMaterial("GreyboxTracer", new Color(1f, 0.9f, 0.4f));
            tracerMaterial.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.3f));
            tracerMaterial.EnableKeyword("_EMISSION");
            var targetLineMaterial = LoadOrCreateMaterial("GreyboxTargetLine", new Color(1f, 0.15f, 0.15f));
            targetLineMaterial.SetColor("_EmissionColor", new Color(0.9f, 0.05f, 0.05f));
            targetLineMaterial.EnableKeyword("_EMISSION");

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
            // 조작 안내는 오른쪽 위 (층 표시 아래) — 아래 가운데 스킬 바와 겹치지 않게.
            var controlsLabel = CreateLabel(canvas.transform, "ControlsLabel", font, 22,
                new Vector2(1f, 1f), new Vector2(-24f, -130f), TextAnchor.UpperRight);
            controlsLabel.gameObject.AddComponent<TextLabel>().TextKey = "ui.hud.controls";
            // 미리보기는 스킬 바(아래 가운데) · 설명 창 위에 둔다.
            var previewLabel = CreateLabel(canvas.transform, "PreviewLabel", font, 28,
                new Vector2(0.5f, 0f), new Vector2(0f, 300f), TextAnchor.LowerCenter);
            previewLabel.rectTransform.sizeDelta = new Vector2(1200f, 90f);
            // 기록은 왼쪽 아래 — 스킬 바와 겹치지 않게 폭을 줄였다.
            var logLabel = CreateLabel(canvas.transform, "LogLabel", font, 24,
                new Vector2(0f, 0f), new Vector2(24f, 64f), TextAnchor.LowerLeft);
            logLabel.rectTransform.sizeDelta = new Vector2(470f, 140f);
            var defeatLabel = CreateLabel(canvas.transform, "DefeatLabel", font, 64,
                new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);
            defeatLabel.color = new Color(1f, 0.35f, 0.3f);
            // 글자 64가 기본 높이 60을 넘으면 uGUI가 글을 통째로 숨긴다 (3-1에서 발견).
            defeatLabel.rectTransform.sizeDelta = new Vector2(1400f, 110f);
            var resultLabel = CreateLabel(canvas.transform, "ResultLabel", font, 32,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), TextAnchor.UpperCenter);
            resultLabel.rectTransform.sizeDelta = new Vector2(1400f, 120f);
            var newRunButton = CreateButton(canvas.transform, "NewRunButton", font, "ui.result.new_run",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -220f));
            var floorLabel = CreateLabel(canvas.transform, "FloorLabel", font, 30,
                new Vector2(1f, 1f), new Vector2(-24f, -20f), TextAnchor.UpperRight);
            // 스킬 바: 아래 가운데에 카드 5장 (사격 · 수류탄 · 밀치기 · 감시 · 턴 종료). 전투 중에만 보인다.
            var skillBarObject = new GameObject("SkillBar", typeof(RectTransform));
            skillBarObject.transform.SetParent(canvas.transform, false);
            // 화면 전체로 늘려야 자식(카드 · 설명 창)이 화면 아래 가운데에 붙는다.
            var skillBarRect = skillBarObject.GetComponent<RectTransform>();
            skillBarRect.anchorMin = Vector2.zero;
            skillBarRect.anchorMax = Vector2.one;
            skillBarRect.sizeDelta = Vector2.zero;
            var barRoot = new GameObject("Cards", typeof(RectTransform));
            barRoot.transform.SetParent(skillBarObject.transform, false);
            var barRect = barRoot.GetComponent<RectTransform>();
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 24f);
            barRect.sizeDelta = new Vector2(5 * CardWidth + 4 * CardGap, CardHeight);
            var shootCard = CreateCard(barRoot.transform, "ShootCard", font, 0);
            var grenadeCard = CreateCard(barRoot.transform, "GrenadeCard", font, 1);
            var pushCard = CreateCard(barRoot.transform, "PushCard", font, 2);
            var overwatchCard = CreateCard(barRoot.transform, "OverwatchCard", font, 3);
            var endTurnCard = CreateCard(barRoot.transform, "EndTurnCard", font, 4);
            var tooltipPanel = CreatePanel(skillBarObject.transform, "Tooltip", new Vector2(0.5f, 0f), new Vector2(0f, CardHeight + 40f),
                new Vector2(900f, 140f), new Color(0.06f, 0.07f, 0.1f, 0.95f));
            tooltipPanel.GetComponent<Image>().raycastTarget = false;
            var tooltipLabel = CreateLabel(tooltipPanel.transform, "Label", font, 24, new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);
            tooltipLabel.rectTransform.sizeDelta = new Vector2(860f, 130f);
            tooltipPanel.SetActive(false);
            var skillBar = skillBarObject.AddComponent<SkillBarView>();
            SetReferences(skillBar, ("barRoot", barRoot), ("shootCard", shootCard), ("grenadeCard", grenadeCard), ("pushCard", pushCard),
                ("overwatchCard", overwatchCard), ("endTurnCard", endTurnCard), ("tooltipPanel", tooltipPanel), ("tooltipLabel", tooltipLabel));

            // 사격 확인 버튼: 적을 골랐을 때만 스킬 바 오른쪽(오른쪽 아래)에 뜬다 — 화면 가운데의 플레이어를 가리지 않게.
            var attackConfirmButton = CreateButton(canvas.transform, "AttackConfirmButton", font, "ui.combat.confirm_attack",
                new Vector2(1f, 0f), new Vector2(-40f, 24f));
            attackConfirmButton.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, CardHeight);
            attackConfirmButton.GetComponent<Image>().color = new Color(0.75f, 0.18f, 0.16f, 0.95f);
            attackConfirmButton.gameObject.SetActive(false);

            // 떠오르는 피해 숫자의 틀 (숨겨 두고 복사해 쓴다).
            var popupTemplate = CreateLabel(canvas.transform, "DamagePopupTemplate", font, 40,
                new Vector2(0f, 0f), Vector2.zero, TextAnchor.MiddleCenter);
            popupTemplate.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            popupTemplate.rectTransform.sizeDelta = new Vector2(320f, 60f);
            popupTemplate.fontStyle = FontStyle.Bold;
            popupTemplate.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            popupTemplate.gameObject.SetActive(false);

            var weaponLabel = CreateLabel(canvas.transform, "WeaponLabel", font, 28,
                new Vector2(0f, 1f), new Vector2(24f, -144f), TextAnchor.UpperLeft);

            // 무기 비교 창: 무기를 밟으면 뜬다 (바꾸기 / 두기).
            var weaponPanel = new GameObject("WeaponPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            weaponPanel.transform.SetParent(canvas.transform, false);
            var panelRect = weaponPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1100f, 330f);
            panelRect.anchoredPosition = new Vector2(0f, 120f);
            var panelImage = weaponPanel.GetComponent<Image>();
            panelImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0.08f, 0.09f, 0.13f, 0.95f);
            var weaponPanelLabel = CreateLabel(weaponPanel.transform, "Label", font, 28,
                new Vector2(0.5f, 1f), new Vector2(0f, -24f), TextAnchor.UpperCenter);
            weaponPanelLabel.rectTransform.sizeDelta = new Vector2(1040f, 180f);
            var swapButton = CreateButton(weaponPanel.transform, "SwapButton", font, "ui.weapon.swap",
                new Vector2(0.5f, 0f), new Vector2(-150f, 24f));
            var keepButton = CreateButton(weaponPanel.transform, "KeepButton", font, "ui.weapon.keep",
                new Vector2(0.5f, 0f), new Vector2(150f, 24f));
            weaponPanel.SetActive(false);
            CreateEventSystem();

            var hudView = canvas.gameObject.AddComponent<HudView>();
            SetReferences(hudView, ("hpLabel", hpLabel), ("stabilityLabel", stabilityLabel), ("roundLabel", roundLabel),
                ("turnLabel", turnLabel), ("previewLabel", previewLabel), ("logLabel", logLabel), ("defeatLabel", defeatLabel), ("resultLabel", resultLabel), ("newRunButton", newRunButton), ("floorLabel", floorLabel),
                ("weaponLabel", weaponLabel), ("weaponPanel", weaponPanel), ("weaponPanelLabel", weaponPanelLabel), ("swapButton", swapButton), ("keepButton", keepButton), ("attackConfirmButton", attackConfirmButton));

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
            SetReferences(unitsView, ("enemyMaterial", enemyMaterial), ("assaultMaterial", assaultMaterial));

            var itemsView = new GameObject("ItemsView").AddComponent<ItemsView>();
            SetReferences(itemsView, ("weaponMaterial", weaponItemMaterial), ("medkitMaterial", medkitMaterial));

            var floorObject = new GameObject("FloorView");
            var floorView = floorObject.AddComponent<FloorView>();
            SetReferences(floorView, ("floorMaterial", floorMaterial), ("wallMaterial", wallMaterial), ("stairsMaterial", stairsMaterial), ("coverMaterial", coverMaterial), ("chasmMaterial", chasmMaterial), ("supplyMaterial", supplyMaterial), ("supplyOpenedMaterial", supplyOpenedMaterial), ("fogMaterial", fogMaterial));
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
            // 고른 사격 대상까지 빨간 선.
            var targetLine = new GameObject("TargetLine", typeof(LineRenderer)).GetComponent<LineRenderer>();
            targetLine.sharedMaterial = targetLineMaterial;
            targetLine.positionCount = 2;
            targetLine.startWidth = targetLine.endWidth = 0.08f;
            targetLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            targetLine.enabled = false;
            SetReferences(moveController, ("viewCamera", cameraObject.GetComponent<Camera>()), ("hoverMarker", hover.transform),
                ("rangeView", rangeView), ("areaView", areaView), ("pushAreaView", pushAreaView), ("targetLine", targetLine));

            var eventPlayer = rootObject.AddComponent<EventPlayer>();
            var combatFx = new GameObject("CombatFx").AddComponent<CombatFxView>();
            SetReferences(combatFx, ("viewCamera", cameraObject.GetComponent<Camera>()), ("tracerMaterial", tracerMaterial), ("popupTemplate", popupTemplate));

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
                ("threatView", threatView),
                ("itemsView", itemsView),
                ("eventPlayer", eventPlayer),
                ("skillBar", skillBar),
                ("combatFx", combatFx));

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

        private const float CardWidth = 170f;
        private const float CardHeight = 110f;
        private const float CardGap = 12f;

        /// <summary>스킬 바 카드: 배경 · 이름(가운데) · 단축키(왼쪽 위) · 재사용 대기 덮개와 숫자. 글은 SkillBarView가 채운다.</summary>
        private static SkillCardView CreateCard(Transform parent, string objectName, Font font, int index)
        {
            var cardObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cardObject.transform.SetParent(parent, false);
            var rect = cardObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);
            rect.anchoredPosition = new Vector2(index * (CardWidth + CardGap), 0f);
            var image = cardObject.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;

            var nameLabel = CreateLabel(cardObject.transform, "Name", font, 28, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), TextAnchor.MiddleCenter);
            nameLabel.rectTransform.sizeDelta = new Vector2(CardWidth - 10f, 50f);
            var keyLabel = CreateLabel(cardObject.transform, "Key", font, 20, new Vector2(0f, 1f), new Vector2(10f, -6f), TextAnchor.UpperLeft);
            keyLabel.rectTransform.sizeDelta = new Vector2(CardWidth - 20f, 30f);
            keyLabel.color = new Color(1f, 1f, 1f, 0.7f);

            var overlay = CreatePanel(cardObject.transform, "Cooldown", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(CardWidth, CardHeight), new Color(0f, 0f, 0f, 0.65f));
            overlay.GetComponent<Image>().raycastTarget = false;
            var cooldownLabel = CreateLabel(overlay.transform, "Rounds", font, 44, new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);
            cooldownLabel.rectTransform.sizeDelta = new Vector2(CardWidth, CardHeight);
            overlay.SetActive(false);

            var card = cardObject.AddComponent<SkillCardView>();
            SetReferences(card, ("button", cardObject.GetComponent<Button>()), ("background", image), ("nameLabel", nameLabel),
                ("keyLabel", keyLabel), ("cooldownOverlay", overlay), ("cooldownLabel", cooldownLabel));
            return card;
        }

        private static GameObject CreatePanel(Transform parent, string objectName, Vector2 corner, Vector2 offset, Vector2 size, Color color)
        {
            var panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            var image = panel.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            return panel;
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
