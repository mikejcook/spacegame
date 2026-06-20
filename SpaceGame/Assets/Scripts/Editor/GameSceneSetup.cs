using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the Game Scene (System View) with one click.
/// Menu item: Star Captain → Setup Game Scene
///
/// ── How to use ────────────────────────────────────────────────────────────
///
///   1. Open a fresh empty scene  (File → New Scene → Empty).
///   2. Run  Star Captain → Setup Game Scene.
///   3. Save As → Assets/Scenes/GameScene.unity  (overwrite if it exists).
///   4. Add it to Build Settings (File → Build Settings → Add Open Scenes).
///
/// ── Notes ─────────────────────────────────────────────────────────────────
///
///   The GameManager is NOT created here — it persists from the MainMenu scene
///   via DontDestroyOnLoad. SystemViewController.Start() handles the case where
///   no GameManager is present so the scene can be opened in isolation for
///   layout inspection (placeholder text is shown instead of live data).
///
/// ── Scene hierarchy produced ──────────────────────────────────────────────
///
///   Main Camera
///   EventSystem
///   UI Audio
///   Canvas  (ScreenSpaceOverlay, 1920×1080, blend 0.5)
///     Background              (Image, full-screen, dark)
///     SystemViewController    (SystemViewController MonoBehaviour, full-screen)
///       Header                (Image, top bar 90 px)
///         AccentLine          (Image, 2 px cyan rule at bottom of header)
///         SystemNameText      (TMP_Text, centred, proper case)
///       Body                  (full-screen minus header and nav bar)
///         SystemMap           (Image, full width — POI nodes spawned here)
///           StarNode          (Image, star sprite centred in map)
///       NavBar                (Image, bottom bar 80 px)
///         AccentLine          (Image, 2 px cyan rule at top of nav bar)
///         ButtonContainer     (HorizontalLayoutGroup, centred)
///           SystemButton      (Shift MainButton — "System")
///           GalaxyButton      (Shift MainButton — "Galaxy")
///           ShipButton        (Shift MainButton — "Ship")
///           CrewButton        (Shift MainButton — "Crew")
///       POIDetailPanel        (full-screen scrim + centred card, hidden at start)
///         Card                (Image, dark panel)
///           TopBar            (Image, thin cyan strip)
///           POIDetailNameText
///           POIDetailTypeText
///           Rule              (Image, horizontal divider)
///           POIDetailDescText
///           POIDetailScannerText   (amber hint, hidden when scanner is sufficient)
///           POIDetailNavigateButton (Shift MainButton — "NAVIGATE")
///           POIDetailCloseButton   (Shift MainButton — "CLOSE")
///
///   Notes: GalaxyView also contains SystemInfoPanel (CanvasGroup-toggled popup
///   shown when a star system node is tapped, before the ship travels).
/// </summary>
public static class GameSceneSetup
{
    // ── Colour palette — matches MainMenuSceneSetup ──────────────────────
    static readonly Color BgColor      = new Color(0.04f, 0.06f, 0.10f, 1.00f);
    static readonly Color PanelBg      = new Color(0.06f, 0.09f, 0.14f, 0.97f);
    static readonly Color HeaderBar    = new Color(0.05f, 0.07f, 0.12f, 1.00f);
    static readonly Color MapBg        = new Color(0.02f, 0.03f, 0.06f, 1.00f);
    static readonly Color AccentCyan   = new Color(0.30f, 0.85f, 1.00f, 1.00f);
    static readonly Color TextWhite    = new Color(0.92f, 0.95f, 1.00f, 1.00f);
    static readonly Color TextSubtle   = new Color(0.60f, 0.72f, 0.85f, 1.00f);
    static readonly Color DividerColor = new Color(0.20f, 0.35f, 0.50f, 0.55f);
    static readonly Color BtnNormal    = new Color(0.08f, 0.25f, 0.42f, 1.00f);
    static readonly Color Scrim        = new Color(0.00f, 0.00f, 0.00f, 0.55f);

    const string MainBtnPrefabPath =
        "Assets/Shift - Complete Sci-Fi UI/Prefabs/Button/Main Button.prefab";
    const string PanelBtnPrefabPath =
        "Assets/Shift - Complete Sci-Fi UI/Prefabs/Button/Panel Button.prefab";
    const string SquadMemberBtnPrefabPath =
        "Assets/Shift - Complete Sci-Fi UI/Prefabs/Button/Squad Member Button.prefab";
    const string PlusIconPath  = "Assets/UI/Icons/icon_plus.png";
    const string MinusIconPath = "Assets/UI/Icons/icon_minus.png";

    // -----------------------------------------------------------------------
    // Entry point
    // -----------------------------------------------------------------------

    [MenuItem("Star Captain/Debug DGB Sprites")]
    public static void DebugDGBSprites()
    {
        const string folder = "Assets/DGB Spaceships/Spaceship Sprites";

        // 1 — what does FindAssets("t:Texture2D") see?
        var guidsT = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        Debug.Log($"[DGB Debug] FindAssets t:Texture2D found {guidsT.Length} assets");
        foreach (var g in guidsT)
            Debug.Log($"  Texture2D guid → path: {AssetDatabase.GUIDToAssetPath(g)}");

        // 2 — what does FindAssets("t:Sprite") see?
        var guidsS = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        Debug.Log($"[DGB Debug] FindAssets t:Sprite found {guidsS.Length} assets");
        foreach (var g in guidsS)
            Debug.Log($"  Sprite guid → path: {AssetDatabase.GUIDToAssetPath(g)}");

        // 3 — try loading one sprite by direct path
        var testPath   = $"{folder}/red_dreadnaught_1.png";
        var testSprite = AssetDatabase.LoadAssetAtPath<Sprite>(testPath);
        var testTex    = AssetDatabase.LoadAssetAtPath<Texture2D>(testPath);
        Debug.Log($"[DGB Debug] Direct load '{testPath}' → Sprite: {(testSprite != null ? testSprite.name : "NULL")}, Texture2D: {(testTex != null ? testTex.name : "NULL")}");

        // 4 — textureType of that file
        var imp = AssetImporter.GetAtPath(testPath) as TextureImporter;
        if (imp != null)
            Debug.Log($"[DGB Debug] textureType={imp.textureType}  spriteImportMode={imp.spriteImportMode}");
        else
            Debug.Log("[DGB Debug] No TextureImporter found for test path");
    }

    [MenuItem("Star Captain/Setup Game Scene")]
    public static void Setup()
    {
        if (!EditorUtility.DisplayDialog(
                "Setup Game Scene",
                "This will populate the current scene with the System View UI.\n\n" +
                "Run in a fresh empty scene for best results.\n\n" +
                "NOTE: The GameManager is NOT created here — it persists from MainMenu via " +
                "DontDestroyOnLoad.",
                "Build It", "Cancel"))
            return;

        // ── Camera ────────────────────────────────────────────────────────
        var camGO = MakeGO("Main Camera", null);
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = BgColor;
        camGO.AddComponent<AudioListener>();
        camGO.tag = "MainCamera";
        Undo.RegisterCreatedObjectUndo(camGO, "Create Main Camera");

        // ── EventSystem ───────────────────────────────────────────────────
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = MakeGO("EventSystem", null);
            es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // ── UI Audio (needed if any buttons use UIElementSound) ───────────
        var uiAudioGO = MakeGO("UI Audio", null);
        uiAudioGO.AddComponent<AudioSource>();
        Undo.RegisterCreatedObjectUndo(uiAudioGO, "Create UI Audio");

        // ── Canvas ────────────────────────────────────────────────────────
        var canvasGO = MakeGO("Canvas", null);
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Background — RawImage so a Texture2D starfield can be set at runtime ──
        var bg = MakeRawImage(canvasGO.transform, "Background", Color.white);
        Stretch(bg);

        // ── SystemViewController root ─────────────────────────────────────
        var svcGO      = MakeUIGO("SystemViewController", canvasGO.transform);
        var controller = svcGO.AddComponent<SystemViewController>();
        Stretch(svcGO);

        // ── Build child hierarchy ─────────────────────────────────────────
        var header     = BuildHeader(svcGO.transform);
        var body       = BuildBody(svcGO.transform);
        var navBar     = BuildNavBar(svcGO.transform);
        var poiDetail  = BuildPOIDetailPanel(svcGO.transform);
        // CombatView is a sibling of Body/NavBar so it covers the full area
        // below the header (including the NavBar strip) when active.
        var combatView      = BuildCombatView(svcGO.transform);
        var levelUpPanel     = BuildLevelUpPanel(svcGO.transform);
        // Sibling of Body/NavBar so it's visible regardless of which sub-view
        // (System/Galaxy/Ship/Crew/Research) is currently shown.
        var researchCompletePopup = BuildResearchCompletePopup(svcGO.transform);

        // ── Wire UIElementSound audio source on all Shift buttons ─────────
        var uiAudio = uiAudioGO.GetComponent<AudioSource>();
        foreach (var ues in canvasGO.GetComponentsInChildren<Michsky.UI.Shift.UIElementSound>(true))
        {
            var so = new SerializedObject(ues);
            so.FindProperty("audioObject").objectReferenceValue = uiAudio;
            so.ApplyModifiedProperties();
        }

        // ── Wire CombatViewController.torpedoCountText ────────────────────
        // torpedoCountText lives in the Header hierarchy (built by BuildHeader)
        // but is a serialised field on CombatViewController (built by BuildCombatView).
        // Neither builder has visibility into the other, so we wire it here.
        {
            var cvc   = combatView.GetComponent<CombatViewController>();
            var cvcSo = new SerializedObject(cvc);
            var torpTMP = header.transform
                              .Find("CombatHeaderStats/TorpedoIconContainer/TorpedoCountText")
                              ?.GetComponent<TMP_Text>();
            if (torpTMP == null)
                Debug.LogError("[GameSceneSetup] TorpedoCountText not found — check BuildHeader hierarchy.");
            else
                cvcSo.FindProperty("torpedoCountText").objectReferenceValue = torpTMP;
            cvcSo.ApplyModifiedProperties();
        }

        // ── Wire serialised fields ────────────────────────────────────────
        var researchView = body.transform.Find("ResearchView")?.gameObject;

        WireController(controller, bg, header, body, navBar, poiDetail, combatView,
                       levelUpPanel, researchView, researchCompletePopup);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Done!",
            "Game Scene built.\n\n" +
            "Save it now as:\n  Assets/Scenes/GameScene.unity\n  (File → Save As…)\n\n" +
            "Then add it to Build Settings:\n  File → Build Settings → Add Open Scenes",
            "OK");

        Debug.Log("[Star Captain] Game scene setup complete.");
    }

    // -----------------------------------------------------------------------
    // Header — top bar (90 px), system name centred
    // -----------------------------------------------------------------------

    static GameObject BuildHeader(Transform parent)
    {
        var header = MakeImage(parent, "Header", HeaderBar);
        PlaceRect(header, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -45f), v2(0f, 90f));

        // Cyan accent line along the bottom of the header
        var rule = MakeImage(header.transform, "AccentLine", AccentCyan);
        PlaceRect(rule, anchor(0f, 0f), anchor(1f, 0f), v2(0f, 1f), v2(0f, 2f));

        // System name — centred across full header width
        var sysName = MakeTMP(header.transform, "SystemNameText", "Sol System", 42, TextWhite);
        PlaceRect(sysName, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(-40f, 0f));
        sysName.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        sysName.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Days widget — left side, left-anchored ────────────────────────────────
        var daysWidget = MakeUIGO("DaysWidget", header.transform);
        {
            var rt       = daysWidget.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(156f, 50f);
            rt.anchoredPosition = new Vector2(16f, 0f);
        }
        // Safe-area: shift right on devices with left notch/rounded corner
        {
            var inset   = daysWidget.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_left").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }

        var daysHLG = daysWidget.AddComponent<HorizontalLayoutGroup>();
        daysHLG.padding                = new RectOffset(0, 0, 0, 0);
        daysHLG.spacing                = 8f;
        daysHLG.childControlWidth      = false;
        daysHLG.childControlHeight     = false;
        daysHLG.childForceExpandWidth  = false;
        daysHLG.childForceExpandHeight = false;
        daysHLG.childAlignment         = TextAnchor.MiddleLeft;

        var daysIconGO  = MakeImage(daysWidget.transform, "DaysIcon", Color.white);
        var daysIconImg = daysIconGO.GetComponent<Image>();
        daysIconImg.preserveAspect = true;
        {
            const string DaysIconPath = "Assets/Art/UI/Other/days.png";
            var imp = AssetImporter.GetAtPath(DaysIconPath) as TextureImporter;
            if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(DaysIconPath, ImportAssetOptions.ForceUpdate);
            }
            var daysSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DaysIconPath);
            if (daysSprite != null) { daysIconImg.sprite = daysSprite; SizeIconToHeight(daysIconImg, 48f); }
            else Debug.LogWarning("[GameSceneSetup] days.png not found at " + DaysIconPath);
        }

        var daysCountGO = MakeTMP(daysWidget.transform, "DaysText", "0", 32, TextWhite);
        {
            var rt       = daysCountGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 40f);
        }
        var daysTMP = daysCountGO.GetComponent<TextMeshProUGUI>();
        daysTMP.alignment        = TextAlignmentOptions.Left;
        daysTMP.fontStyle        = FontStyles.Bold;
        daysTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // Salvage widget — right side, rightmost ─────────────────────────────────
        var salvageWidget = MakeUIGO("SalvageWidget", header.transform);
        {
            var rt       = salvageWidget.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(156f, 50f);
            rt.anchoredPosition = new Vector2(-16f, 0f);
        }
        var hlg = salvageWidget.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(0, 0, 0, 0);
        hlg.spacing                = 8f;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment         = TextAnchor.MiddleRight;

        // Icon
        const string SalvageIconPath = "Assets/Art/UI/Other/salvage.png";
        var salvageIconGO = MakeImage(salvageWidget.transform, "SalvageIcon", Color.white);
        {
            var rt       = salvageIconGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(36f, 36f);
        }
        var salvageIconImg = salvageIconGO.GetComponent<Image>();
        salvageIconImg.preserveAspect = true;
        {
            var importer = AssetImporter.GetAtPath(SalvageIconPath) as TextureImporter;
            if (importer != null && importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(SalvageIconPath, ImportAssetOptions.ForceUpdate);
            }
            var salvageSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SalvageIconPath);
            if (salvageSprite != null) { salvageIconImg.sprite = salvageSprite; SizeIconToHeight(salvageIconImg, 48f); }
            else Debug.LogWarning("[GameSceneSetup] salvage.png not found at " + SalvageIconPath);
        }

        // Count label
        var salvageText = MakeTMP(salvageWidget.transform, "SalvageText", "0", 32, TextWhite);
        {
            var rt       = salvageText.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 40f);
        }
        var salvageTMP = salvageText.GetComponent<TextMeshProUGUI>();
        salvageTMP.alignment          = TextAlignmentOptions.Left;
        salvageTMP.fontStyle          = FontStyles.Bold;
        salvageTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // Fuel widget — right side, leftmost of right group ──────────────────────
        var fuelWidget = MakeUIGO("FuelWidget", header.transform);
        {
            var rt       = fuelWidget.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(156f, 50f);
            rt.anchoredPosition = new Vector2(-360f, 0f);
        }
        var fuelHLG = fuelWidget.AddComponent<HorizontalLayoutGroup>();
        fuelHLG.padding                = new RectOffset(0, 0, 0, 0);
        fuelHLG.spacing                = 8f;
        fuelHLG.childControlWidth      = false;
        fuelHLG.childControlHeight     = false;
        fuelHLG.childForceExpandWidth  = false;
        fuelHLG.childForceExpandHeight = false;
        fuelHLG.childAlignment         = TextAnchor.MiddleRight;

        var fuelIconGO  = MakeImage(fuelWidget.transform, "FuelIcon", Color.white);
        var fuelIconImg = fuelIconGO.GetComponent<Image>();
        fuelIconImg.preserveAspect = true;
        {
            const string FuelIconPath = "Assets/Art/UI/Other/fuel.png";
            var imp = AssetImporter.GetAtPath(FuelIconPath) as TextureImporter;
            if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(FuelIconPath, ImportAssetOptions.ForceUpdate);
            }
            var fuelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FuelIconPath);
            if (fuelSprite != null) { fuelIconImg.sprite = fuelSprite; SizeIconToHeight(fuelIconImg, 48f); }
            else Debug.LogWarning("[GameSceneSetup] fuel.png not found at " + FuelIconPath);
        }

        var fuelTextGO = MakeTMP(fuelWidget.transform, "FuelText", "100", 32, TextWhite);
        {
            var rt       = fuelTextGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 40f);
        }
        var fuelTMP = fuelTextGO.GetComponent<TextMeshProUGUI>();
        fuelTMP.alignment        = TextAlignmentOptions.Left;
        fuelTMP.fontStyle        = FontStyles.Bold;
        fuelTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // Loyalty widget — left side, right of Days ──────────────────────────────
        var loyaltyWidget = MakeUIGO("LoyaltyWidget", header.transform);
        {
            var rt       = loyaltyWidget.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(156f, 50f);
            rt.anchoredPosition = new Vector2(188f, 0f);
        }
        // Safe-area: shift right to match DaysWidget so the gap between them stays constant
        {
            var inset   = loyaltyWidget.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_left").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }

        var loyaltyHLG = loyaltyWidget.AddComponent<HorizontalLayoutGroup>();
        loyaltyHLG.padding                = new RectOffset(0, 0, 0, 0);
        loyaltyHLG.spacing                = 8f;
        loyaltyHLG.childControlWidth      = false;
        loyaltyHLG.childControlHeight     = false;
        loyaltyHLG.childForceExpandWidth  = false;
        loyaltyHLG.childForceExpandHeight = false;
        loyaltyHLG.childAlignment         = TextAnchor.MiddleLeft;

        var loyaltyIconGO  = MakeImage(loyaltyWidget.transform, "LoyaltyIcon", Color.white);
        var loyaltyIconImg = loyaltyIconGO.GetComponent<Image>();
        loyaltyIconImg.preserveAspect = true;
        {
            const string LoyaltyIconPath = "Assets/Art/UI/Other/loyalty.png";
            var imp = AssetImporter.GetAtPath(LoyaltyIconPath) as TextureImporter;
            if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(LoyaltyIconPath, ImportAssetOptions.ForceUpdate);
            }
            var loyaltySprite = AssetDatabase.LoadAssetAtPath<Sprite>(LoyaltyIconPath);
            if (loyaltySprite != null) { loyaltyIconImg.sprite = loyaltySprite; SizeIconToHeight(loyaltyIconImg, 48f); }
            else Debug.LogWarning("[GameSceneSetup] loyalty.png not found at " + LoyaltyIconPath);
        }

        var loyaltyTextGO = MakeTMP(loyaltyWidget.transform, "LoyaltyText", "75", 32, TextWhite);
        {
            var rt       = loyaltyTextGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 40f);
        }
        var loyaltyTMP = loyaltyTextGO.GetComponent<TextMeshProUGUI>();
        loyaltyTMP.alignment        = TextAlignmentOptions.Left;
        loyaltyTMP.fontStyle        = FontStyles.Bold;
        loyaltyTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // Crew widget — left side, right of Loyalty (loyalty spans 188..344, +16 gap) ──
        var crewWidget = MakeUIGO("CrewWidget", header.transform);
        {
            var rt       = crewWidget.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(156f, 50f);
            rt.anchoredPosition = new Vector2(360f, 0f);
        }
        // Safe-area: shift right to match the other left-side widgets
        {
            var inset   = crewWidget.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_left").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }

        var crewHLG = crewWidget.AddComponent<HorizontalLayoutGroup>();
        crewHLG.padding                = new RectOffset(0, 0, 0, 0);
        crewHLG.spacing                = 8f;
        crewHLG.childControlWidth      = false;
        crewHLG.childControlHeight     = false;
        crewHLG.childForceExpandWidth  = false;
        crewHLG.childForceExpandHeight = false;
        crewHLG.childAlignment         = TextAnchor.MiddleLeft;

        var crewIconGO  = MakeImage(crewWidget.transform, "CrewIcon", Color.white);
        var crewIconImg = crewIconGO.GetComponent<Image>();
        crewIconImg.preserveAspect = true;
        {
            const string CrewIconPath = "Assets/Art/UI/Other/character.png";
            var imp = AssetImporter.GetAtPath(CrewIconPath) as TextureImporter;
            if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(CrewIconPath, ImportAssetOptions.ForceUpdate);
            }
            var crewSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CrewIconPath);
            if (crewSprite != null) { crewIconImg.sprite = crewSprite; SizeIconToHeight(crewIconImg, 48f); }
            else Debug.LogWarning("[GameSceneSetup] character.png not found at " + CrewIconPath);
        }

        var crewTextGO = MakeTMP(crewWidget.transform, "CrewText", "2/2", 32, TextWhite);
        {
            var rt       = crewTextGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 40f);
        }
        var crewTMP = crewTextGO.GetComponent<TextMeshProUGUI>();
        crewTMP.alignment        = TextAlignmentOptions.Left;
        crewTMP.fontStyle        = FontStyles.Bold;
        crewTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // Iridium widget — right side, centre (x=-188) ─────────────────────────
        var iridiumWidget = MakeUIGO("IridiumWidget", header.transform);
        {
            var rt       = iridiumWidget.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(156f, 50f);
            rt.anchoredPosition = new Vector2(-188f, 0f);
        }
        var iridiumHLG = iridiumWidget.AddComponent<HorizontalLayoutGroup>();
        iridiumHLG.padding                = new RectOffset(0, 0, 0, 0);
        iridiumHLG.spacing                = 8f;
        iridiumHLG.childControlWidth      = false;
        iridiumHLG.childControlHeight     = false;
        iridiumHLG.childForceExpandWidth  = false;
        iridiumHLG.childForceExpandHeight = false;
        iridiumHLG.childAlignment         = TextAnchor.MiddleRight;

        var iridiumIconGO  = MakeImage(iridiumWidget.transform, "IridiumIcon", Color.white);
        var iridiumIconImg = iridiumIconGO.GetComponent<Image>();
        iridiumIconImg.preserveAspect = true;
        {
            const string IridiumIconPath = "Assets/Art/UI/Other/iridium.png";
            var imp = AssetImporter.GetAtPath(IridiumIconPath) as TextureImporter;
            if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(IridiumIconPath, ImportAssetOptions.ForceUpdate);
            }
            var iridiumSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IridiumIconPath);
            if (iridiumSprite != null) { iridiumIconImg.sprite = iridiumSprite; SizeIconToHeight(iridiumIconImg, 48f); }
            else Debug.LogWarning("[GameSceneSetup] iridium.png not found at " + IridiumIconPath);
        }

        var iridiumTextGO = MakeTMP(iridiumWidget.transform, "IridiumText", "0", 32, TextWhite);
        {
            var rt       = iridiumTextGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(100f, 40f);
        }
        var iridiumTMP = iridiumTextGO.GetComponent<TextMeshProUGUI>();
        iridiumTMP.alignment        = TextAlignmentOptions.Left;
        iridiumTMP.fontStyle        = FontStyles.Bold;
        iridiumTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // ── Combat header stats — hidden by default, shown during combat ─────
        // Displays player's own Shield% and Hull% on the right side of the
        // header, replacing the salvage widget. CanvasGroup controls visibility
        // so no GameObjects are disabled (avoids Shift animator binding issues).
        //
        //   CombatHeaderStats  (CanvasGroup, right-anchored)
        //     PlayerShieldText ("SHIELDS  100%")
        //     DividerText      ("|")
        //     PlayerHullText   ("HULL  100%")
        var combatHeaderStats = MakeUIGO("CombatHeaderStats", header.transform);
        {
            var rt       = combatHeaderStats.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 56f);  // torpedo icon + count + dividers + shields + hull
            rt.anchoredPosition = new Vector2(-48f, 0f);   // moved inward from edge
        }
        var chsCG = combatHeaderStats.AddComponent<CanvasGroup>();
        chsCG.alpha          = 0f;
        chsCG.blocksRaycasts = false;
        chsCG.interactable   = false;

        // Shift inward on devices with right-side safe area (notch / rounded corners)
        {
            var inset   = combatHeaderStats.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_right").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }

        var chsHLG = combatHeaderStats.AddComponent<HorizontalLayoutGroup>();
        chsHLG.padding                = new RectOffset(0, 0, 0, 0);
        chsHLG.spacing                = 12f;
        chsHLG.childControlWidth      = false;
        chsHLG.childControlHeight     = false;
        chsHLG.childForceExpandWidth  = false;
        chsHLG.childForceExpandHeight = false;
        chsHLG.childAlignment         = TextAnchor.MiddleRight;

        // ── Torpedo icon + count ──────────────────────────────────────────
        var torpIconContainer = MakeUIGO("TorpedoIconContainer", combatHeaderStats.transform);
        {
            var rt       = torpIconContainer.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(72f, 40f);
        }
        var torpHLG = torpIconContainer.AddComponent<HorizontalLayoutGroup>();
        torpHLG.padding                = new RectOffset(0, 0, 0, 0);
        torpHLG.spacing                = 6f;
        torpHLG.childControlWidth      = false;
        torpHLG.childControlHeight     = false;
        torpHLG.childForceExpandWidth  = false;
        torpHLG.childForceExpandHeight = false;
        torpHLG.childAlignment         = TextAnchor.MiddleCenter;

        var torpIconGO = MakeImage(torpIconContainer.transform, "TorpedoIcon", Color.white);
        {
            var rt       = torpIconGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(28f, 28f);
            torpIconGO.GetComponent<Image>().preserveAspect = true;
        }

        var torpedoCountText = MakeTMP(torpIconContainer.transform, "TorpedoCountText", "×8", 26, TextWhite);
        {
            var rt       = torpedoCountText.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(38f, 40f);
        }
        torpedoCountText.GetComponent<TextMeshProUGUI>().alignment          = TextAlignmentOptions.Left;
        torpedoCountText.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        var chsDivider0 = MakeTMP(combatHeaderStats.transform, "DividerText0", "|", 26, TextSubtle);
        {
            var rt       = chsDivider0.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(16f, 40f);
        }
        chsDivider0.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        var playerShieldText = MakeTMP(combatHeaderStats.transform, "PlayerShieldText", "Shields  100%", 28, TextWhite);
        {
            var rt       = playerShieldText.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(180f, 40f);
        }
        playerShieldText.GetComponent<TextMeshProUGUI>().alignment          = TextAlignmentOptions.Right;
        playerShieldText.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        var chsDivider = MakeTMP(combatHeaderStats.transform, "DividerText", "|", 28, TextSubtle);
        {
            var rt       = chsDivider.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(16f, 40f);
        }
        chsDivider.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        var playerHullText = MakeTMP(combatHeaderStats.transform, "PlayerHullText", "Hull  100%", 28, TextWhite);
        {
            var rt       = playerHullText.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(170f, 40f);
        }
        playerHullText.GetComponent<TextMeshProUGUI>().alignment          = TextAlignmentOptions.Left;
        playerHullText.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        return header;
    }

    // -----------------------------------------------------------------------
    // Body — between header (90 px) and nav bar (80 px)
    // -----------------------------------------------------------------------

    static GameObject BuildBody(Transform parent)
    {
        // Fills canvas minus 90 px header at top and 96 px nav bar at bottom.
        // The 96 MUST match BuildNavBar's base height or the two won't be flush
        // once the safe-area expand/shrink run on a home-indicator device.
        // anchoredPosition.y = (96 - 90) / 2 = 3   (centre shifts slightly upward)
        // sizeDelta.y = -(90 + 96) = -186
        var body = MakeUIGO("Body", parent);
        PlaceRect(body, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 3f), v2(0f, -186f));
        // Clip system map and galaxy map so panning can't bleed over the header or nav bar
        body.AddComponent<RectMask2D>();

        // On devices with a home indicator the nav bar expands upward; the body
        // must shrink from the bottom by the same amount to stay flush with it.
        // _shrinkTowardSafeArea only moves the bottom edge — the top (header) is untouched.
        var bodySafeInset   = body.AddComponent<SafeAreaInset>();
        var bodySafeInsetSo = new SerializedObject(bodySafeInset);
        bodySafeInsetSo.FindProperty("_bottom").boolValue               = true;
        bodySafeInsetSo.FindProperty("_shrinkTowardSafeArea").boolValue = true;
        bodySafeInsetSo.ApplyModifiedProperties();

        BuildSystemMap(body.transform);
        BuildGalaxyView(body.transform);
        BuildShipView(body.transform);
        BuildCrewView(body.transform);
        BuildResearchView(body.transform);

        return body;
    }

    // ── System map — full width ──────────────────────────────────────────────

    static void BuildSystemMap(Transform parent)
    {
        // No Image component — the full-screen Background RawImage (starfield) shows through.
        var map = MakeUIGO("SystemMap", parent);
        PlaceRect(map, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(0f, 0f));

        // Pinch-to-zoom + drag-pan controller
        map.AddComponent<SystemMapZoomController>();

        // Star node — sprite set at runtime by SystemViewController from StarType
        var star = MakeImage(map.transform, "StarNode", Color.white);
        PlaceRect(star, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f), v2(0f, 0f), v2(130f, 130f));
    }

    // ── Galaxy view — full width, starts hidden ──────────────────────────────

    static GameObject BuildGalaxyView(Transform parent)
    {
        // Root panel — toggled by nav button; starts inactive
        var galaxyView = MakeUIGO("GalaxyView", parent);
        PlaceRect(galaxyView, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(0f, 0f));
        galaxyView.AddComponent<GalaxyViewController>();
        galaxyView.SetActive(false);

        // GalaxyMap — zoom/pan container (mirrors the role of SystemMap)
        var galaxyMap = MakeUIGO("GalaxyMap", galaxyView.transform);
        PlaceRect(galaxyMap, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(0f, 0f));
        galaxyMap.AddComponent<SystemMapZoomController>();

        // Background — galaxy texture applied at runtime
        var bg = MakeRawImage(galaxyMap.transform, "GalaxyBackground", Color.white);
        Stretch(bg);

        // Nodes container — system dots anchored here
        var nodesContainer = MakeUIGO("SystemNodesContainer", galaxyMap.transform);
        PlaceRect(nodesContainer, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(0f, 0f));

        // System info panel — shown when a node is tapped; hidden via CanvasGroup
        BuildGalaxySystemInfoPanel(galaxyView.transform);

        return galaxyView;
    }

    // ── Galaxy system info panel — displayed on node tap ────────────────────
    //
    //   SystemInfoPanel  (CanvasGroup — alpha toggled for show/hide)
    //   └─ Scrim         (full-screen dark overlay, catches taps outside card)
    //   └─ Card          (centred card, 680×420)
    //      ├─ TopBar
    //      ├─ SystemInfoNameText
    //      ├─ SystemInfoSubtitleText
    //      ├─ DistanceRow
    //      │  ├─ DistanceLabel
    //      │  └─ SystemInfoDistanceText
    //      ├─ Rule
    //      ├─ SystemInfoPOIText
    //      └─ ButtonRow
    //         ├─ SystemInfoTravelButton  ("SET COURSE")
    //         └─ SystemInfoCloseButton   ("CLOSE")

    static GameObject BuildGalaxySystemInfoPanel(Transform parent)
    {
        // Root: CanvasGroup so Shift buttons stay active (no SetActive toggling)
        var panel = MakeUIGO("SystemInfoPanel", parent);
        Stretch(panel);
        panel.AddComponent<CanvasGroup>();

        // Semi-transparent scrim behind the card
        var scrimImg = panel.AddComponent<Image>();
        scrimImg.color = Scrim;

        // Card — centred, fixed size
        var card = MakeImage(panel.transform, "Card", PanelBg);
        PlaceRect(card, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f), v2(0f, 0f), v2(680f, 420f));

        // Cyan top bar
        var topBar = MakeImage(card.transform, "TopBar", AccentCyan);
        PlaceRect(topBar, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -2f), v2(0f, 4f));

        // System name — bold, large
        var nameText = MakeTMP(card.transform, "SystemInfoNameText", "System Name", 52, TextWhite);
        PlaceRect(nameText, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -60f), v2(-48f, 66f));
        nameText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        nameText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        nameText.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        nameText.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;

        // Subtitle — star type
        var subtitleText = MakeTMP(card.transform, "SystemInfoSubtitleText", "Yellow Dwarf", 26, TextSubtle);
        PlaceRect(subtitleText, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -122f), v2(-48f, 36f));
        subtitleText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        subtitleText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        // Distance row — label + value side by side
        var distLabel = MakeTMP(card.transform, "DistanceLabel", "DISTANCE", 22, TextSubtle);
        PlaceRect(distLabel, anchor(0f, 1f), anchor(0f, 1f), v2(24f, -170f), v2(170f, 28f));
        distLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        distLabel.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);

        var distValue = MakeTMP(card.transform, "SystemInfoDistanceText", "6,380 ly away", 32, AccentCyan);
        PlaceRect(distValue, anchor(0f, 1f), anchor(1f, 1f), v2(200f, -162f), v2(-48f, 40f));
        distValue.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        distValue.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        distValue.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);

        // Horizontal rule
        var rule = MakeImage(card.transform, "Rule", DividerColor);
        PlaceRect(rule, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -214f), v2(-40f, 2f));

        // POI summary text
        var poiText = MakeTMP(card.transform, "SystemInfoPOIText", "8 planets  ·  1 station", 28, TextSubtle);
        PlaceRect(poiText, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -250f), v2(-48f, 36f));
        poiText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        // Buttons — bottom-right of the card
        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);

        GameObject travelGO;
        if (btnPrefab != null)
        {
            travelGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            travelGO.name = "SystemInfoTravelButton";
            var mb = travelGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Set Course";
        }
        else
        {
            travelGO = MakeImage(card.transform, "SystemInfoTravelButton", BtnNormal);
            travelGO.AddComponent<Button>();
            var lbl = MakeTMP(travelGO.transform, "Label", "Set Course", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        // SET COURSE — anchored bottom-left
        {
            var rt       = travelGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(200f, 52f);
            rt.anchoredPosition = new Vector2(16f, 16f);
        }

        GameObject closeGO;
        if (btnPrefab != null)
        {
            closeGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            closeGO.name = "SystemInfoCloseButton";
            var mb = closeGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Close";
        }
        else
        {
            closeGO = MakeImage(card.transform, "SystemInfoCloseButton", BtnNormal);
            closeGO.AddComponent<Button>();
            var lbl = MakeTMP(closeGO.transform, "Label", "Close", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        // CLOSE — anchored bottom-right
        {
            var rt       = closeGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(180f, 52f);
            rt.anchoredPosition = new Vector2(-16f, 16f);
        }

        // HE-3 + DAYS labels — LabelControl prefabs, side-by-side above the Set Course button
        // Button bottom edge is at y=16, top at y=68. Labels sit just above at y=76.
        const string LabelControlPrefabPath = "Assets/Prefabs/UI/LabelControl.prefab";
        var labelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabelControlPrefabPath);
        if (labelPrefab != null)
        {
            // He-3 fuel cost
            var fuelLabel = (GameObject)Object.Instantiate(labelPrefab, card.transform);
            fuelLabel.name = "SystemInfoFuelCostLabel";
            var fuelRT     = fuelLabel.GetComponent<RectTransform>();
            fuelRT.anchorMin = fuelRT.anchorMax = new Vector2(0f, 0f);
            fuelRT.pivot     = new Vector2(0f, 0f);
            fuelRT.anchoredPosition = new Vector2(16f, 76f);
            var fuelTitle = fuelLabel.transform.Find("Normal/Border/Title")?.GetComponent<TMP_Text>();
            if (fuelTitle != null) fuelTitle.text = "He-3";

            // Days travel time
            var daysLabel = (GameObject)Object.Instantiate(labelPrefab, card.transform);
            daysLabel.name = "SystemInfoDaysLabel";
            var daysRT     = daysLabel.GetComponent<RectTransform>();
            daysRT.anchorMin = daysRT.anchorMax = new Vector2(0f, 0f);
            daysRT.pivot     = new Vector2(0f, 0f);
            daysRT.anchoredPosition = new Vector2(164f, 76f);
            var daysTitle = daysLabel.transform.Find("Normal/Border/Title")?.GetComponent<TMP_Text>();
            if (daysTitle != null) daysTitle.text = "Days";
        }
        else
        {
            Debug.LogWarning("[GameSceneSetup] LabelControl prefab not found at " + LabelControlPrefabPath);
        }

        return panel;
    }

    // ── Ship view — full body area, starts hidden ────────────────────────────

    static GameObject BuildShipView(Transform parent)
    {
        var shipView = MakeUIGO("ShipView", parent);
        PlaceRect(shipView, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(0f, 0f));
        shipView.SetActive(false);

        // ShipViewController drives slot refresh and the equipment detail popup.
        shipView.AddComponent<ShipViewController>();

        // ── Layer 1: solid dark blue background ───────────────────────────
        var bgGO = MakeImage(shipView.transform, "ShipViewBackground", new Color(0.03f, 0.05f, 0.12f, 1f));
        Stretch(bgGO);

        // ── Layer 2: component columns — left (power/defense) and right (offense/utility)
        // Each column is 150 canvas-units wide, full height of the view.
        BuildComponentColumn(shipView.transform, "LeftComponentColumn", isLeft: true,
            "Reactor", "FTL Drive", "Engines", "Shields", "Armor");
        BuildComponentColumn(shipView.transform, "RightComponentColumn", isLeft: false,
            "Particle Cannons", "Torpedoes", "Scanner", "Cargo Hold", "Crew Quarters");

        // ── Layer 3: ship sprite — native pixel size (56×72), centred.
        // The DGB corvette sprite is 56×72 px pixel art. Any upscaling blurs it
        // regardless of filter mode, so we display 1:1 (no scaling). If a higher-res
        // ship sprite is swapped in later, increase the sizeDelta to match.
        // DGB sprites face UP (+Y) by default — no rotation correction needed
        // (the old 180° was for the Excalibur sprite which faced left/−X).
        var shipImg = MakeImage(shipView.transform, "ShipImage", Color.white);
        // Canvas scale on the target device is ~0.5 (960-wide effective resolution vs
        // 1920 reference), so 56 canvas units → ~28 screen pixels (half native).
        // Double the sizeDelta so the sprite renders at ~1× native pixels on device.
        PlaceRect(shipImg, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f), v2(0f, 0f), v2(112f, 144f));
        // No rotation — DGB sprites already face up.
        var img = shipImg.GetComponent<Image>();
        img.preserveAspect = true;
        img.type           = Image.Type.Simple;
        // Decorative only — must not intercept taps.
        img.raycastTarget  = false;

        // ── Layer 4: equipment detail overlay (CanvasGroup-controlled popup) ─
        BuildEquipmentDetailOverlay(shipView.transform);

        return shipView;
    }

    // ── Equipment detail popup ────────────────────────────────────────────────
    //
    // Hierarchy:
    //   EquipmentDetailOverlay   (CanvasGroup scrim — shown/hidden via alpha)
    //     DetailCard             (dark panel, 680 × 480, centred)
    //       TopBar               (4 px cyan accent)
    //       IconArea             (96 × 96 icon, top-left of card body)
    //       DetailName           (bold TMP, right of icon)
    //       Rule                 (2 px divider)
    //       DetailDesc           (multi-line TMP)
    //       CostLabel            ("UPGRADE COST")
    //       CostValue            (placeholder resource text)
    //       UpgradeButton        (Shift MainButton)
    //       CloseButton          (Shift MainButton)
    //
    // Per CLAUDE.md: uses CanvasGroup not SetActive because Shift MainButtons are
    // inside and their Animators must remain continuously bound.

    static GameObject BuildEquipmentDetailOverlay(Transform parent)
    {
        // Scrim — fills entire ShipView, blocks clicks when popup is open.
        var overlay = MakeImage(parent, "EquipmentDetailOverlay", Scrim);
        Stretch(overlay);
        var cg = overlay.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        // ── Card ──────────────────────────────────────────────────────────
        const float CardW = 680f;
        const float CardH = 480f;
        var card = MakeImage(overlay.transform, "DetailCard", PanelBg);
        PlaceRect(card, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f),
                  v2(0f, 0f), v2(CardW, CardH));

        // Cyan accent bar at top of card
        var topBar = MakeImage(card.transform, "TopBar", AccentCyan);
        PlaceRect(topBar, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -4f), v2(0f, 4f));

        // ── Icon (96 × 96), anchored top-left ─────────────────────────────
        const float IconSize = 96f;
        const float Pad      = 20f;
        var iconGO  = MakeImage(card.transform, "DetailIcon", Color.white);
        PlaceRect(iconGO, anchor(0f, 1f), anchor(0f, 1f),
                  v2(Pad + IconSize * 0.5f, -(Pad + IconSize * 0.5f + 4f)),
                  v2(IconSize, IconSize));
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.type           = Image.Type.Simple;
        iconImg.preserveAspect = true;

        // ── Equipment name, right of icon ─────────────────────────────────
        var nameGO  = MakeTMP(card.transform, "DetailName", "—", 32, TextWhite);
        PlaceRect(nameGO, anchor(0f, 1f), anchor(1f, 1f),
                  v2((Pad + IconSize + 12f + (CardW - Pad - IconSize - 12f) * 0.5f), -(Pad + IconSize * 0.5f + 4f)),
                  v2(CardW - Pad - IconSize - 12f - Pad, IconSize));
        var nameTMP = nameGO.GetComponent<TextMeshProUGUI>();
        nameTMP.alignment = TextAlignmentOptions.Left;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // ── Current tier label, right-aligned on the name row ─────────────
        var tierGO = MakeTMP(card.transform, "DetailTier", "Mk I", 24, AccentCyan);
        PlaceRect(tierGO, anchor(0f, 1f), anchor(1f, 1f),
                  v2(0f, -(Pad + IconSize * 0.5f + 4f)),
                  v2(-Pad * 2f, 36f));
        var tierTMP = tierGO.GetComponent<TextMeshProUGUI>();
        tierTMP.alignment = TextAlignmentOptions.Right;
        tierTMP.fontStyle = FontStyles.Bold;
        tierTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // ── Horizontal rule ───────────────────────────────────────────────
        float ruleY = -(Pad + IconSize + 4f + 12f);
        var rule = MakeImage(card.transform, "Rule", DividerColor);
        PlaceRect(rule, anchor(0f, 1f), anchor(1f, 1f),
                  v2(0f, ruleY), v2(-Pad * 2f, 2f));

        // ── Description text ──────────────────────────────────────────────
        float descTop = ruleY - 14f;
        const float DescH = 120f;
        var descGO  = MakeTMP(card.transform, "DetailDesc",
                              "No description available.", 22, TextSubtle);
        PlaceRect(descGO, anchor(0f, 1f), anchor(1f, 1f),
                  v2(0f, descTop - DescH * 0.5f), v2(-Pad * 2f, DescH));
        var descTMP = descGO.GetComponent<TextMeshProUGUI>();
        descTMP.alignment          = TextAlignmentOptions.TopLeft;
        descTMP.textWrappingMode = TMPro.TextWrappingModes.Normal;

        // ── Upgrade cost ──────────────────────────────────────────────────
        float costY = descTop - DescH - 18f;
        var costLabel = MakeTMP(card.transform, "CostLabel", "UPGRADE COST", 20, TextSubtle);
        PlaceRect(costLabel, anchor(0f, 1f), anchor(0f, 1f),
                  v2(Pad + 120f, costY - 14f), v2(240f, 28f));
        costLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

        var costValue = MakeTMP(card.transform, "CostValue", "[RESOURCES TBD]", 20, AccentCyan);
        PlaceRect(costValue, anchor(0f, 1f), anchor(1f, 1f),
                  v2(0f, costY - 14f), v2(-Pad * 2f - 260f, 28f));
        var costTMP = costValue.GetComponent<TextMeshProUGUI>();
        costTMP.alignment = TextAlignmentOptions.Right;

        // ── Buttons — bottom of card ──────────────────────────────────────
        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);

        // UPGRADE — anchored bottom-left
        GameObject upgradeGO;
        if (btnPrefab != null)
        {
            upgradeGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            upgradeGO.name = "UpgradeButton";
            var mb = upgradeGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Upgrade";
        }
        else
        {
            upgradeGO = MakeImage(card.transform, "UpgradeButton", BtnNormal);
            upgradeGO.AddComponent<Button>();
            var lbl = MakeTMP(upgradeGO.transform, "Label", "Upgrade", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt = upgradeGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(200f, 52f);
            rt.anchoredPosition = new Vector2(Pad, Pad);
        }

        // CLOSE — anchored bottom-right
        GameObject closeGO;
        if (btnPrefab != null)
        {
            closeGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            closeGO.name = "DetailCloseButton";
            var mb = closeGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Close";
        }
        else
        {
            closeGO = MakeImage(card.transform, "DetailCloseButton", BtnNormal);
            closeGO.AddComponent<Button>();
            var lbl = MakeTMP(closeGO.transform, "Label", "Close", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt = closeGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(180f, 52f);
            rt.anchoredPosition = new Vector2(-Pad, Pad);
        }

        // ── Upgrade confirmation dialog (sits above the detail card) ──────
        BuildUpgradeConfirmOverlay(overlay.transform);

        return overlay;
    }

    // ── Upgrade confirmation dialog ───────────────────────────────────────────
    //
    // Hierarchy:
    //   ConfirmOverlay        (CanvasGroup scrim — shown/hidden via alpha)
    //     ConfirmCard         (dark panel, centred)
    //       TopBar            (cyan accent)
    //       ConfirmText       (multi-line prompt)
    //       ConfirmYesButton  (Shift MainButton — "YES")
    //       ConfirmNoButton   (Shift MainButton — "NO")
    //
    // CanvasGroup (not SetActive) because it contains Shift MainButtons whose
    // Animators must stay continuously bound (see CLAUDE.md).

    static GameObject BuildUpgradeConfirmOverlay(Transform parent)
    {
        var confirm = MakeImage(parent, "ConfirmOverlay", Scrim);
        Stretch(confirm);
        var cg = confirm.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        const float CardW = 620f;
        const float CardH = 300f;
        const float Pad   = 24f;
        var card = MakeImage(confirm.transform, "ConfirmCard", PanelBg);
        PlaceRect(card, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f),
                  v2(0f, 0f), v2(CardW, CardH));

        var topBar = MakeImage(card.transform, "TopBar", AccentCyan);
        PlaceRect(topBar, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -4f), v2(0f, 4f));

        // Prompt text — fills the top portion of the card.
        var textGO = MakeTMP(card.transform, "ConfirmText",
                             "Do you wish to upgrade this component?", 26, TextWhite);
        PlaceRect(textGO, anchor(0f, 1f), anchor(1f, 1f),
                  v2(0f, -(Pad + 80f)), v2(-Pad * 2f, 140f));
        var textTMP = textGO.GetComponent<TextMeshProUGUI>();
        textTMP.alignment          = TextAlignmentOptions.Center;
        textTMP.textWrappingMode = TMPro.TextWrappingModes.Normal;

        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);

        // YES — bottom-left
        GameObject yesGO;
        if (btnPrefab != null)
        {
            yesGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            yesGO.name = "ConfirmYesButton";
            var mb = yesGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Yes";
        }
        else
        {
            yesGO = MakeImage(card.transform, "ConfirmYesButton", BtnNormal);
            yesGO.AddComponent<Button>();
            var lbl = MakeTMP(yesGO.transform, "Label", "Yes", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt = yesGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(200f, 52f);
            rt.anchoredPosition = new Vector2(Pad, Pad);
        }

        // NO — bottom-right
        GameObject noGO;
        if (btnPrefab != null)
        {
            noGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            noGO.name = "ConfirmNoButton";
            var mb = noGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "No";
        }
        else
        {
            noGO = MakeImage(card.transform, "ConfirmNoButton", BtnNormal);
            noGO.AddComponent<Button>();
            var lbl = MakeTMP(noGO.transform, "Label", "No", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt = noGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(200f, 52f);
            rt.anchoredPosition = new Vector2(-Pad, Pad);
        }

        return confirm;
    }

    // ── Component column — stacks icon+label buttons vertically ─────────────

    static void BuildComponentColumn(Transform parent, string name, bool isLeft,
                                     params string[] componentNames)
    {
        // 150 px wide strip pinned to the left or right edge, full height.
        var col = MakeUIGO(name, parent);
        if (isLeft)
            PlaceRect(col, anchor(0f, 0f), anchor(0f, 1f), v2(75f, 0f), v2(150f, 0f));
        else
            PlaceRect(col, anchor(1f, 0f), anchor(1f, 1f), v2(-75f, 0f), v2(150f, 0f));

        // VerticalLayoutGroup distributes slots evenly across the full height.
        var vlg = col.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(8, 8, 16, 16);
        vlg.spacing                = 6f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = true;
        vlg.childAlignment         = TextAnchor.MiddleCenter;

        // Shift the column away from notches / home indicators at runtime.
        var safeInset   = col.AddComponent<SafeAreaInset>();
        var safeInsetSo = new SerializedObject(safeInset);
        safeInsetSo.FindProperty(isLeft ? "_left" : "_right").boolValue = true;
        safeInsetSo.ApplyModifiedProperties();

        foreach (var componentName in componentNames)
            BuildComponentSlot(col.transform, componentName);
    }

    // ── Single component slot — icon fills the slot with a thin tier border ──
    //
    // Hierarchy:
    //   Slot_<Name>   dark bg + Button
    //     TierBorder  Image — filled with tier color (the visible border ring)
    //       SlotInner Image — dark bg, inset 3 px → makes the 3 px border visible
    //         ComponentIcon Image — icon sprite, stretch-fill, preserveAspect=true
    //
    // preserveAspect on a Simple Image is the reliable way to avoid distortion in
    // a non-square rect. AspectRatioFitter was tried but fails here: FitInParent
    // with a point anchor queries the parent rect before layout resolves, producing
    // a zero or wrong size. preserveAspect has no such timing dependency.

    static void BuildComponentSlot(Transform parent, string componentName)
    {
        var slotName = "Slot_" + componentName.Replace(" ", "");

        // Slot root — dark background, acts as the Button target.
        var slotGO = MakeImage(parent, slotName, new Color(0.06f, 0.10f, 0.18f, 0.90f));
        var btn    = slotGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(0.55f, 0.90f, 1.00f, 1f);
        colors.pressedColor     = new Color(0.30f, 0.55f, 0.70f, 1f);
        btn.colors        = colors;
        btn.targetGraphic = slotGO.GetComponent<Image>();

        var slotUI   = slotGO.AddComponent<EquipmentSlotUI>();
        var slotUISo = new SerializedObject(slotUI);

        // TierBorder — fills the slot; its color becomes the visible border ring.
        // raycastTarget=false: the Button on slotGO handles the full rect hit,
        // so child images must not intercept raycasts (they cause the "only the
        // icon sprite area responds" problem with preserveAspect letterboxing).
        var borderGO = MakeImage(slotGO.transform, "TierBorder",
                                 Constants.Ship.EmptySlotBorderColor);
        borderGO.GetComponent<Image>().raycastTarget = false;
        var borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = Vector2.zero;
        borderRT.offsetMax = Vector2.zero;

        // SlotInner — dark bg inset 3 px; the 3 px gap around it IS the border.
        var innerGO = MakeImage(borderGO.transform, "SlotInner",
                                new Color(0.06f, 0.10f, 0.18f, 1f));
        innerGO.GetComponent<Image>().raycastTarget = false;
        var innerRT = innerGO.GetComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(3f, 3f);
        innerRT.offsetMax = new Vector2(-3f, -3f);

        // ComponentIcon — stretch-fills SlotInner.  preserveAspect=true on a
        // Simple Image is the most reliable way to prevent distortion: Unity
        // scales the sprite uniformly to fit the rect and centres it, with no
        // dependency on layout timing (unlike AspectRatioFitter).
        var iconGO  = MakeImage(innerGO.transform, "ComponentIcon", Color.white);
        var iconRT  = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin = Vector2.zero;
        iconRT.anchorMax = Vector2.one;
        iconRT.offsetMin = Vector2.zero;
        iconRT.offsetMax = Vector2.zero;
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.type           = Image.Type.Simple;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;  // slotGO's Image handles the full-rect hit

        // Load the icon sprite from the EquipmentIcons art folder.
        Sprite iconSprite = null;
        if (Constants.Ship.EquipmentSlots.IconNames.TryGetValue(componentName, out var iconFile))
        {
            var iconPath = $"Assets/Art/UI/EquipmentIcons/{iconFile}.png";

            // Ensure the texture is imported as a Single sprite.
            // The icons ship with spriteMode=Multiple (sliced), so LoadAssetAtPath
            // returns only the first sub-sprite (a quarter of the image).
            // Reimporting as Single gives us one sprite covering the full texture.
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer != null && importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[GameSceneSetup] Re-imported {iconFile}.png as Single sprite.");
            }

            iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (iconSprite == null)
                Debug.LogWarning($"[GameSceneSetup] Icon not found at {iconPath}");
        }

        iconImg.sprite = iconSprite;
        iconImg.color  = Color.white;

        // Wire EquipmentSlotUI references.
        slotUISo.FindProperty("iconImage").objectReferenceValue   = iconImg;
        slotUISo.FindProperty("borderImage").objectReferenceValue = borderGO.GetComponent<Image>();
        slotUISo.FindProperty("defaultIcon").objectReferenceValue = iconSprite;
        slotUISo.ApplyModifiedProperties();

        // ClickBlocker — transparent Image on top of everything, fills the full
        // slot rect, raycastTarget=true.  SlotClickForwarder is added directly
        // here with a wired reference to slotUI, so there is no parent-traversal
        // at all — the click goes: ClickBlocker Image hit → SlotClickForwarder.
        // OnPointerClick → ShipViewController.OnSlotClicked.  Direct, no ambiguity.
        var blocker    = MakeImage(slotGO.transform, "ClickBlocker", new Color(0f, 0f, 0f, 0.004f));
        var blockerImg = blocker.GetComponent<Image>();
        blockerImg.raycastTarget = true;
        Stretch(blocker);

        var forwarder   = blocker.AddComponent<SlotClickForwarder>();
        var forwarderSo = new SerializedObject(forwarder);
        forwarderSo.FindProperty("target").objectReferenceValue = slotUI;
        forwarderSo.ApplyModifiedProperties();
    }

    // ── Crew view — full body area, starts hidden ────────────────────────────
    //
    //   CrewView
    //   ├─ CrewViewBackground
    //   ├─ CrewTabBar              (52 px top strip — two tab buttons)
    //   ├─ CrewContent             (crew list + detail; hidden when Assignments tab active)
    //   │  ├─ CrewListPanel
    //   │  ├─ Divider
    //   │  └─ CrewDetailPanel
    //   └─ CrewAssignmentPanel     (hidden by default; CrewAssignmentViewController)
    //      ├─ AssignmentBackground
    //      ├─ LeftSlotColumn       (Pilot / Engineer / Scientist)
    //      ├─ CaptainSlot          (centre — always filled)
    //      ├─ RightSlotColumn      (Gunner / Doctor / Soldier)
    //      └─ AssignmentPickerPanel (CanvasGroup overlay; lists crew for a chosen slot)

    const float TabBarHeight   = 52f;
    const float SlotWidth      = 140f;
    const float SlotHeight     = 180f;
    const float SlotSpacing    = 16f;
    const float ColX           = 0.12f;   // left column centre (normalised)
    const float ColXRight      = 0.88f;   // right column centre (normalised)

    static GameObject BuildCrewView(Transform parent)
    {
        var crewView = MakeUIGO("CrewView", parent);
        PlaceRect(crewView, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(0f, 0f));
        crewView.AddComponent<CrewViewController>();
        crewView.SetActive(false);

        // ── Background ───────────────────────────────────────────────────
        var bg = MakeImage(crewView.transform, "CrewViewBackground", new Color(0.03f, 0.05f, 0.12f, 1f));
        Stretch(bg);

        // ── Tab bar — 52 px at the top ───────────────────────────────────
        var tabBar = MakeImage(crewView.transform, "CrewTabBar", new Color(0.04f, 0.08f, 0.16f, 1f));
        {
            var rt = tabBar.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, TabBarHeight);
        }

        var tabBarHLG = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabBarHLG.childControlWidth      = true;
        tabBarHLG.childControlHeight     = true;
        tabBarHLG.childForceExpandWidth  = true;
        tabBarHLG.childForceExpandHeight = true;
        tabBarHLG.spacing                = 0f;
        tabBarHLG.padding                = new RectOffset(0, 0, 0, 0);

        var listTabBtn  = BuildTabButton(tabBar.transform, "CrewListTabButton",  "CREW LIST");
        var assignTabBtn = BuildTabButton(tabBar.transform, "AssignmentsTabButton", "ASSIGNMENTS");

        // Tab divider line at bottom — ignoreLayout so it doesn't consume a slot in the HLG
        var tabLine = MakeImage(tabBar.transform, "TabLine", new Color(0.20f, 0.35f, 0.50f, 0.55f));
        {
            var rt = tabLine.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 2f);
            rt.anchoredPosition = Vector2.zero;
        }
        tabLine.AddComponent<LayoutElement>().ignoreLayout = true;

        // ── CrewContent — list + detail, sits below tab bar ──────────────
        var crewContent = MakeUIGO("CrewContent", crewView.transform);
        {
            var rt = crewContent.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, -TabBarHeight);
        }

        // Left panel — crew list (38 % width)
        var listPanel = MakeImage(crewContent.transform, "CrewListPanel", new Color(0.04f, 0.07f, 0.13f, 1f));
        PlaceRect(listPanel, anchor(0f, 0f), anchor(0.38f, 1f), v2(0f, 0f), v2(0f, 0f));

        var listSafeInset   = listPanel.AddComponent<SafeAreaInset>();
        var listSafeInsetSo = new SerializedObject(listSafeInset);
        listSafeInsetSo.FindProperty("_left").boolValue               = true;
        listSafeInsetSo.FindProperty("_shrinkTowardSafeArea").boolValue = true;
        listSafeInsetSo.ApplyModifiedProperties();

        var scrollGO = MakeUIGO("Scroll View", listPanel.transform);
        Stretch(scrollGO);
        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;
        sr.scrollSensitivity = 30f;
        sr.movementType = ScrollRect.MovementType.Clamped;

        var viewport = MakeUIGO("Viewport", scrollGO.transform);
        Stretch(viewport);
        viewport.AddComponent<RectMask2D>();
        sr.viewport = viewport.GetComponent<RectTransform>();

        var content   = MakeUIGO("Content", viewport.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f); contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero; contentRT.sizeDelta = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8); vlg.spacing = 6f;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contentRT;

        var divider = MakeImage(crewContent.transform, "Divider", new Color(0.20f, 0.35f, 0.50f, 0.55f));
        PlaceRect(divider, anchor(0.38f, 0f), anchor(0.38f, 1f), v2(0f, 0f), v2(2f, 0f));

        var detailPanel = MakeImage(crewContent.transform, "CrewDetailPanel", new Color(0.04f, 0.07f, 0.13f, 0.60f));
        PlaceRect(detailPanel, anchor(0.38f, 0f), anchor(1f, 1f), v2(4f, 0f), v2(0f, 0f));

        // Detail panel internals (identical to before)
        var portrait = MakeImage(detailPanel.transform, "PortraitImage", AccentCyan);
        PlaceRect(portrait, anchor(0f, 1f), anchor(0f, 1f), v2(24f, -24f), v2(160f, 160f));
        portrait.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        portrait.GetComponent<Image>().preserveAspect = true;

        var nameText = MakeTMP(detailPanel.transform, "CrewNameText", "Crew Member", 42, TextWhite);
        TopStretch(nameText, 204f, 24f, 210f, 52f);
        nameText.GetComponent<TextMeshProUGUI>().alignment        = TextAlignmentOptions.Left;
        nameText.GetComponent<TextMeshProUGUI>().fontStyle        = FontStyles.Bold;
        nameText.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        nameText.GetComponent<TextMeshProUGUI>().overflowMode     = TextOverflowModes.Ellipsis;

        var levelText = MakeTMP(detailPanel.transform, "LevelText", "Level 1", 28, AccentCyan);
        { var rt = levelText.GetComponent<RectTransform>();
          rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
          rt.anchoredPosition = new Vector2(-24f, -24f); rt.sizeDelta = new Vector2(186f, 52f); }
        levelText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;
        levelText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        levelText.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        var roleText = MakeTMP(detailPanel.transform, "CrewRoleText", "Role", 30, AccentCyan);
        TopStretch(roleText, 204f, 82f, 24f, 36f);
        roleText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        roleText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        var xpLabel = MakeTMP(detailPanel.transform, "XPLabel", "EXPERIENCE", 20, TextSubtle);
        TopStretch(xpLabel, 204f, 124f, 24f, 24f);
        xpLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        var xpBarGO = MakeUIGO("XPBar", detailPanel.transform);
        TopStretch(xpBarGO, 204f, 152f, 24f, 28f);
        var slider = xpBarGO.AddComponent<Slider>();
        slider.interactable = false; slider.minValue = 0f; slider.maxValue = 100f;
        slider.value = 0f; slider.direction = Slider.Direction.LeftToRight;
        var xpBg = MakeImage(xpBarGO.transform, "Background", new Color(0.12f, 0.20f, 0.32f, 1f));
        Stretch(xpBg); slider.targetGraphic = xpBg.GetComponent<Image>();
        var fillArea = MakeUIGO("Fill Area", xpBarGO.transform);
        Stretch(fillArea);
        fillArea.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        fillArea.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        var fill = MakeImage(fillArea.transform, "Fill", AccentCyan);
        Stretch(fill); slider.fillRect = fill.GetComponent<RectTransform>();

        var xpText = MakeTMP(detailPanel.transform, "XPText", "0 / 100 XP", 18, TextSubtle);
        TopStretch(xpText, 204f, 152f, 24f, 28f);
        xpText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

        var skillDivider = MakeImage(detailPanel.transform, "SkillsDivider", new Color(0.20f, 0.35f, 0.50f, 0.55f));
        TopStretch(skillDivider, 24f, 196f, 24f, 2f);

        var skillsHeader = MakeTMP(detailPanel.transform, "SkillsHeader", "SKILLS", 22, TextSubtle);
        TopStretch(skillsHeader, 24f, 202f, 24f, 28f);
        skillsHeader.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        var skillsContainer = MakeUIGO("SkillsContainer", detailPanel.transform);
        { var rt = skillsContainer.GetComponent<RectTransform>();
          rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
          rt.offsetMin = new Vector2(24f, 16f); rt.offsetMax = new Vector2(-24f, -234f); }
        var skillsVLG = skillsContainer.AddComponent<VerticalLayoutGroup>();
        skillsVLG.spacing = 6f; skillsVLG.childControlWidth = skillsVLG.childControlHeight = true;
        skillsVLG.childForceExpandWidth = true; skillsVLG.childForceExpandHeight = false;
        skillsVLG.childAlignment = TextAnchor.UpperLeft;

        // ── CrewAssignmentPanel — sits below tab bar, starts hidden ───────
        var assignPanel = BuildCrewAssignmentPanel(crewView.transform);

        // ── Wire CrewViewController serialised fields ──────────────────────
        var cvc   = crewView.GetComponent<CrewViewController>();
        var cvcSo = new SerializedObject(cvc);
        cvcSo.FindProperty("crewListTabButton").objectReferenceValue   = listTabBtn.GetComponent<Button>();
        cvcSo.FindProperty("assignmentsTabButton").objectReferenceValue = assignTabBtn.GetComponent<Button>();
        cvcSo.FindProperty("crewContent").objectReferenceValue         = crewContent;
        var cavc = assignPanel.GetComponent<CrewAssignmentViewController>();
        cvcSo.FindProperty("assignmentViewController").objectReferenceValue = cavc;
        cvcSo.ApplyModifiedProperties();

        return crewView;
    }

    // ── Tab button helper ────────────────────────────────────────────────────

    static GameObject BuildTabButton(Transform parent, string goName, string label)
    {
        var go  = new GameObject(goName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.04f, 0.08f, 0.16f, 1f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        cols.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = cols;

        var lblGO  = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(go.transform, false);
        var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
        lblTMP.text      = label;
        lblTMP.fontSize  = 24f;
        lblTMP.color     = new Color(0.60f, 0.72f, 0.85f, 1f);  // TextSubtle
        lblTMP.alignment = TextAlignmentOptions.Center;
        lblTMP.fontStyle = FontStyles.Bold;
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;

        go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        return go;
    }

    // ── Crew assignment panel ────────────────────────────────────────────────
    //
    //  Layout mirrors ShipView: 3 slots left, captain centre, 3 slots right.
    //  Slot positions (normalised x): left col 0.12, centre 0.50, right col 0.88.
    //  Three slots per column, evenly spaced vertically.

    static GameObject BuildCrewAssignmentPanel(Transform parent)
    {
        const float TabH  = TabBarHeight;
        const float SW    = SlotWidth;
        const float SH    = SlotHeight;
        const float CapSz = 200f;  // captain portrait diameter

        var panel = MakeUIGO("CrewAssignmentPanel", parent);
        {
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, -TabH);
        }
        var cavc = panel.AddComponent<CrewAssignmentViewController>();
        panel.SetActive(false);  // hidden by default; shown when Assignments tab tapped

        var panelSo = new SerializedObject(cavc);

        // Background
        var panelBg = MakeImage(panel.transform, "AssignmentBackground", new Color(0.03f, 0.05f, 0.12f, 1f));
        Stretch(panelBg);

        // ── Captain slot — centre ─────────────────────────────────────────
        var captainSlotGO = BuildAssignmentSlot(panel.transform, "CaptainSlot",
            Constants.Crew.Roles.Captain, CapSz, CapSz, isInteractable: false);
        {
            var rt = captainSlotGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(CapSz, CapSz + 60f);   // +60 for label + name
        }
        WireAssignmentSlot(panelSo, "captainSlot", captainSlotGO, Constants.Crew.Roles.Captain);

        // ── Left column — Pilot, Engineer, Scientist ──────────────────────
        // Panel height ≈ 842 cu (no safe area) or ~686 cu (iPhone w/ home indicator,
        // because Body shrinks via SafeAreaInset). SlotHeight = 180.
        // With {0.84, 0.50, 0.16}: spacing = 0.34 × 686 = 233 cu — 53 cu clearance.
        // Top slot top-edge: 0.84×686+90 = 667 < 686 ✓. Bottom slot bottom: 16 cu > 0 ✓.
        string[] leftRoles  = { Constants.Crew.Roles.Pilot, Constants.Crew.Roles.Engineer, Constants.Crew.Roles.Scientist };
        float[]  leftYNorm  = { 0.84f, 0.50f, 0.16f };

        for (int i = 0; i < leftRoles.Length; i++)
        {
            var slotGO = BuildAssignmentSlot(panel.transform, $"Slot_{leftRoles[i]}",
                leftRoles[i], SW, SH, isInteractable: true);
            {
                var rt = slotGO.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(ColX, leftYNorm[i]);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(SW, SH);
            }
            WireAssignmentSlotArray(panelSo, "assignmentSlots", i, slotGO, leftRoles[i]);
        }

        // ── Right column — Gunner, Doctor, Soldier ────────────────────────
        string[] rightRoles = { Constants.Crew.Roles.Gunner, Constants.Crew.Roles.Doctor, Constants.Crew.Roles.Soldier };
        float[]  rightYNorm = { 0.84f, 0.50f, 0.16f };

        for (int i = 0; i < rightRoles.Length; i++)
        {
            var slotGO = BuildAssignmentSlot(panel.transform, $"Slot_{rightRoles[i]}",
                rightRoles[i], SW, SH, isInteractable: true);
            {
                var rt = slotGO.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(ColXRight, rightYNorm[i]);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(SW, SH);
            }
            WireAssignmentSlotArray(panelSo, "assignmentSlots", 3 + i, slotGO, rightRoles[i]);
        }

        // ── Assignment picker panel (modal card overlay) ──────────────────
        var picker = BuildAssignmentPickerPanel(panel.transform);
        panelSo.FindProperty("pickerPanel").objectReferenceValue        = picker;
        var pickerContent = picker.transform.Find("Card/PickerScrollView/Viewport/Content");
        if (pickerContent != null)
            panelSo.FindProperty("pickerContent").objectReferenceValue  = pickerContent;
        var pickerLabel = picker.transform.Find("Card/RoleLabel")?.GetComponent<TextMeshProUGUI>();
        if (pickerLabel != null)
            panelSo.FindProperty("pickerRoleLabel").objectReferenceValue = pickerLabel;
        var pickerClose = picker.transform.Find("Card/CloseButton")?.GetComponentInChildren<Button>(true);
        if (pickerClose != null)
            panelSo.FindProperty("pickerCloseButton").objectReferenceValue = pickerClose;

        panelSo.ApplyModifiedProperties();
        return panel;
    }

    // Builds a single assignment slot: role label at top, portrait circle, name at bottom.
    static GameObject BuildAssignmentSlot(Transform parent, string goName, string role,
                                          float w, float h, bool isInteractable)
    {
        var slotGO = MakeUIGO(goName, parent);

        // Slot background
        var bgImg  = slotGO.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.10f, 0.18f, 0.85f);

        if (isInteractable)
        {
            var btn = slotGO.AddComponent<Button>();
            btn.targetGraphic = bgImg;
            var cols = btn.colors;
            cols.normalColor      = Color.white;
            cols.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cols.pressedColor     = new Color(0.80f, 0.80f, 0.80f, 1f);
            btn.colors = cols;
        }

        // Role label — top, centred
        const float LabelH = 24f;
        const float PortraitInset = 8f;
        const float NameH = 26f;
        // Cap portrait so label + portrait + name always fits inside the slot height.
        float portraitSize = Mathf.Min(w - PortraitInset * 2f, h - LabelH - NameH - 12f);

        var roleLabelGO  = MakeTMP(slotGO.transform, "RoleLabel",
            CrewViewController.FormatRole(role).ToUpper(), 18, new Color(0.60f, 0.72f, 0.85f, 1f));
        {
            var rt = roleLabelGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -4f);
            rt.sizeDelta        = new Vector2(0f, LabelH);
        }
        roleLabelGO.GetComponent<TextMeshProUGUI>().alignment        = TextAlignmentOptions.Center;
        roleLabelGO.GetComponent<TextMeshProUGUI>().fontStyle        = FontStyles.Bold;
        roleLabelGO.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;

        // Portrait — centred in remaining space
        var portraitGO  = MakeImage(slotGO.transform, "PortraitImage", new Color(0.12f, 0.18f, 0.28f, 1f));
        portraitGO.GetComponent<Image>().preserveAspect = true;
        {
            var rt = portraitGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, (NameH - LabelH) * 0.5f);
            rt.sizeDelta        = new Vector2(portraitSize, portraitSize);
        }

        // Name — bottom, centred
        var nameGO  = MakeTMP(slotGO.transform, "NameText", "— VACANT —", 18,
            new Color(0.35f, 0.40f, 0.50f, 1f));
        {
            var rt = nameGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(1f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            rt.sizeDelta        = new Vector2(0f, NameH);
        }
        nameGO.GetComponent<TextMeshProUGUI>().alignment        = TextAlignmentOptions.Center;
        nameGO.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        nameGO.GetComponent<TextMeshProUGUI>().overflowMode     = TextOverflowModes.Ellipsis;

        return slotGO;
    }

    // Wires the captainSlot SerializedObject field.
    static void WireAssignmentSlot(SerializedObject so, string fieldName, GameObject slotGO, string role)
    {
        var prop = so.FindProperty(fieldName);
        prop.FindPropertyRelative("role").stringValue                          = role;
        prop.FindPropertyRelative("root").objectReferenceValue                 = slotGO;
        prop.FindPropertyRelative("portrait").objectReferenceValue             = slotGO.transform.Find("PortraitImage")?.GetComponent<Image>();
        prop.FindPropertyRelative("nameText").objectReferenceValue             = slotGO.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        prop.FindPropertyRelative("button").objectReferenceValue               = slotGO.GetComponent<Button>();
    }

    // Wires one element of the assignmentSlots array.
    static void WireAssignmentSlotArray(SerializedObject so, string arrayField, int index,
                                        GameObject slotGO, string role)
    {
        var arrayProp = so.FindProperty(arrayField);
        while (arrayProp.arraySize <= index) arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
        var elem = arrayProp.GetArrayElementAtIndex(index);
        elem.FindPropertyRelative("role").stringValue          = role;
        elem.FindPropertyRelative("root").objectReferenceValue = slotGO;
        elem.FindPropertyRelative("portrait").objectReferenceValue =
            slotGO.transform.Find("PortraitImage")?.GetComponent<Image>();
        elem.FindPropertyRelative("nameText").objectReferenceValue =
            slotGO.transform.Find("NameText")?.GetComponent<TextMeshProUGUI>();
        elem.FindPropertyRelative("button").objectReferenceValue =
            slotGO.GetComponent<Button>();
    }

    // Builds the crew picker overlay that appears when a slot is tapped.
    static GameObject BuildAssignmentPickerPanel(Transform parent)
    {
        // Root: CanvasGroup so Shift MainButton animators stay continuously bound.
        // Never call SetActive on this — show/hide via CanvasGroup alpha only.
        var picker = MakeUIGO("AssignmentPickerPanel", parent);
        Stretch(picker);
        var pickerCG = picker.AddComponent<CanvasGroup>();
        pickerCG.alpha          = 0f;
        pickerCG.blocksRaycasts = false;
        pickerCG.interactable   = false;

        // Scrim — dark backdrop behind the card
        var scrim = MakeImage(picker.transform, "Scrim", Scrim);
        Stretch(scrim);

        // Card — centred modal panel (matches POI Detail / Level Up style)
        var card = MakeImage(picker.transform, "Card", PanelBg);
        PlaceRect(card, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f), v2(0f, 0f), v2(700f, 580f));

        // Top cyan accent bar
        var topBar = MakeImage(card.transform, "TopBar", AccentCyan);
        PlaceRect(topBar, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -2f), v2(0f, 4f));

        // "ASSIGN" title
        var titleGO = MakeTMP(card.transform, "TitleText", "ASSIGN", 38, TextWhite);
        PlaceRect(titleGO, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -32f), v2(-60f, 52f));
        titleGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        titleGO.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Role label — updated at runtime to show the slot's role name
        var roleLabel = MakeTMP(card.transform, "RoleLabel", "CREW MEMBER", 26, AccentCyan);
        PlaceRect(roleLabel, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -82f), v2(-60f, 34f));
        roleLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        roleLabel.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        // Divider rule
        var rule = MakeImage(card.transform, "DividerRule", DividerColor);
        PlaceRect(rule, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -118f), v2(-40f, 2f));

        // ScrollRect for crew list — fills the middle of the card
        var scrollGO = MakeUIGO("PickerScrollView", card.transform);
        {
            var rt       = scrollGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 80f);    // 80 px above close button
            rt.offsetMax = new Vector2(-20f, -122f); // 122 px below top (below rule)
        }
        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal        = false;
        sr.vertical          = true;
        sr.scrollSensitivity = 30f;
        sr.movementType      = ScrollRect.MovementType.Clamped;

        var viewport = MakeUIGO("Viewport", scrollGO.transform);
        Stretch(viewport);
        viewport.AddComponent<RectMask2D>();
        sr.viewport = viewport.GetComponent<RectTransform>();

        var content   = MakeUIGO("Content", viewport.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin        = new Vector2(0f, 1f);
        contentRT.anchorMax        = new Vector2(1f, 1f);
        contentRT.pivot            = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta        = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding               = new RectOffset(0, 0, 8, 8);
        vlg.spacing               = 6f;
        vlg.childControlWidth     = vlg.childControlHeight     = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = contentRT;

        // CLOSE button — Shift MainButton, centred at bottom of card
        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);
        GameObject closeGO;
        if (btnPrefab != null)
        {
            closeGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            closeGO.name = "CloseButton";
            var mb = closeGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Close";
        }
        else
        {
            Debug.LogWarning("[GameSceneSetup] Shift MainButton prefab not found — using plain close button.");
            closeGO = MakeImage(card.transform, "CloseButton", BtnNormal);
            closeGO.AddComponent<Button>();
            var lbl = MakeTMP(closeGO.transform, "Label", "Close", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt              = closeGO.GetComponent<RectTransform>();
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 16f);
            rt.sizeDelta        = new Vector2(220f, 52f);
        }

        return picker;
    }

    // ── Research view — full body area, starts hidden ────────────────────────
    //
    // Hierarchy:
    //   ResearchView                   (CanvasGroup for show/hide; ResearchViewController)
    //     ResearchBackground           (Image, same dark blue as ShipView)
    //     TreeScrollRect               (ScrollRect — pan to navigate)
    //       Viewport                   (RectMask2D)
    //         TreeContent              (2400×1800 fixed canvas; nodes + lines)
    //           ConnectionLayer        (UILineRenderer children)
    //           NodeLayer              (node GameObjects)
    //     DetailPanel                  (CanvasGroup, right-anchored card)

    // TreeContent canvas size — must match ResearchViewController.TreeCanvasW/H
    const float ResearchTreeW   = 2400f;
    const float ResearchTreeH   = 1800f;
    // Extra space added around all four edges so the player can pan past the
    // outermost nodes without hitting a hard wall.
    const float ResearchPadding = 300f;

    static GameObject BuildResearchView(Transform parent)
    {
        var researchView = MakeUIGO("ResearchView", parent);
        Stretch(researchView);
        researchView.SetActive(false);

        var cg = researchView.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        researchView.AddComponent<ResearchViewController>();

        // ── Solid background — same dark blue as ShipView / CrewView ─────────
        var bg = MakeImage(researchView.transform, "ResearchBackground",
                           new Color(0.03f, 0.05f, 0.12f, 1f));
        Stretch(bg);

        // ── Tree scroll area (full area, panning) ─────────────────────────────
        var scrollGO = MakeUIGO("TreeScrollRect", researchView.transform);
        Stretch(scrollGO);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal        = true;
        scrollRect.vertical          = true;
        scrollRect.movementType      = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity        = 0.1f;
        scrollRect.scrollSensitivity = 40f;
        scrollRect.inertia           = true;
        scrollRect.decelerationRate  = 0.135f;

        var viewport = MakeUIGO("Viewport", scrollGO.transform);
        Stretch(viewport);
        // Use stencil Mask (not RectMask2D) so UILineRenderer children — which don't
        // implement IClippable — are properly clipped and never bleed over the header.
        var viewportImg              = viewport.AddComponent<Image>();
        viewportImg.color            = Color.white;          // alpha must be > 0 or mask is empty
        var viewportMask             = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = false;                // image shapes the mask but doesn't draw
        scrollRect.viewport = viewport.GetComponent<RectTransform>();

        // TreeContent: fixed large canvas — nodes placed by normalised (0–1) coords
        var content   = MakeUIGO("TreeContent", viewport.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin        = new Vector2(0f, 0f);
        contentRT.anchorMax        = new Vector2(0f, 0f);
        contentRT.pivot            = new Vector2(0f, 0f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta        = new Vector2(ResearchTreeW + ResearchPadding * 2f,
                                                  ResearchTreeH + ResearchPadding * 2f);
        scrollRect.content         = contentRT;

        // ConnectionLayer — lines drawn behind nodes
        var connLayer = MakeUIGO("ConnectionLayer", content.transform);
        Stretch(connLayer);

        // NodeLayer — nodes drawn on top of lines
        var nodeLayer = MakeUIGO("NodeLayer", content.transform);
        Stretch(nodeLayer);

        // ── Detail overlay root — CanvasGroup controls scrim + card together ──
        // Full-screen so the scrim fills the view; the card is a centred child.
        var detailRoot = MakeUIGO("DetailRoot", researchView.transform);
        Stretch(detailRoot);

        // Scrim — dims the tree behind the popup
        var detailScrim = MakeImage(detailRoot.transform, "DetailScrim",
                                    new Color(0f, 0f, 0f, 0.55f));
        Stretch(detailScrim);

        // Centred card
        var detailPanel = MakeImage(detailRoot.transform, "DetailPanel",
                                    new Color(0.06f, 0.09f, 0.14f, 0.97f));
        {
            var rt       = detailPanel.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(720f, 500f);
        }

        var topAccent = MakeImage(detailPanel.transform, "TopAccent", AccentCyan);
        PlaceRect(topAccent, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -2f), v2(0f, 4f));

        var detailNameTMP = MakeTMP(detailPanel.transform, "DetailNameText", "Node Name", 34, TextWhite);
        PlaceRect(detailNameTMP, anchor(0f, 1f), anchor(1f, 1f), v2(16f, -52f), v2(-32f, 44f));
        detailNameTMP.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        detailNameTMP.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var detailCatTMP = MakeTMP(detailPanel.transform, "DetailCategoryText", "Category", 22, AccentCyan);
        PlaceRect(detailCatTMP, anchor(0f, 1f), anchor(1f, 1f), v2(16f, -90f), v2(-32f, 30f));
        detailCatTMP.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        var rule = MakeImage(detailPanel.transform, "Rule", DividerColor);
        PlaceRect(rule, anchor(0f, 1f), anchor(1f, 1f), v2(16f, -112f), v2(-32f, 2f));

        var detailDescTMP = MakeTMP(detailPanel.transform, "DetailDescText", "Description text.", 24, TextSubtle);
        PlaceRect(detailDescTMP, anchor(0f, 1f), anchor(1f, 1f), v2(16f, -260f), v2(-32f, 140f));
        {
            var tmp = detailDescTMP.GetComponent<TextMeshProUGUI>();
            tmp.alignment        = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
        }

        var detailCostTMP = MakeTMP(detailPanel.transform, "DetailCostText", "Cost: 0 salvage", 26,
                                    new Color(1.00f, 0.78f, 0.20f, 1.00f));
        PlaceRect(detailCostTMP, anchor(0f, 1f), anchor(1f, 1f), v2(16f, -298f), v2(-32f, 30f));
        detailCostTMP.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        // Unlock button
        GameObject unlockGO;
        var unlockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);
        if (unlockPrefab != null)
        {
            unlockGO      = (GameObject)Object.Instantiate(unlockPrefab, detailPanel.transform);
            unlockGO.name = "UnlockButton";
            var mb = unlockGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Unlock";
        }
        else
        {
            unlockGO = MakeImage(detailPanel.transform, "UnlockButton", BtnNormal);
            unlockGO.AddComponent<Button>();
            var lbl = MakeTMP(unlockGO.transform, "Label", "Unlock", 24, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        // UNLOCK — anchored bottom-left
        {
            var rt       = unlockGO.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(16f, 16f);
            rt.sizeDelta = new Vector2(220f, 52f);
        }

        // Debug: Research Now button — centred between Unlock and Close
        GameObject debugResearchGO;
        if (unlockPrefab != null)
        {
            debugResearchGO      = (GameObject)Object.Instantiate(unlockPrefab, detailPanel.transform);
            debugResearchGO.name = "DebugResearchNowButton";
            var mb = debugResearchGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Research Now";
        }
        else
        {
            debugResearchGO = MakeImage(detailPanel.transform, "DebugResearchNowButton", BtnNormal);
            debugResearchGO.AddComponent<Button>();
            var lbl = MakeTMP(debugResearchGO.transform, "Label", "Research Now", 24, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        // DEBUG RESEARCH NOW — anchored bottom-centre
        {
            var rt       = debugResearchGO.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 16f);
            rt.sizeDelta = new Vector2(220f, 52f);
        }

        // Close detail button
        GameObject closeDetailGO;
        if (unlockPrefab != null)
        {
            closeDetailGO      = (GameObject)Object.Instantiate(unlockPrefab, detailPanel.transform);
            closeDetailGO.name = "CloseDetailButton";
            var mb = closeDetailGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Close";
        }
        else
        {
            closeDetailGO = MakeImage(detailPanel.transform, "CloseDetailButton", BtnNormal);
            closeDetailGO.AddComponent<Button>();
            var lbl = MakeTMP(closeDetailGO.transform, "Label", "Close", 24, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        // CLOSE — anchored bottom-right
        {
            var rt       = closeDetailGO.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(1f, 0f);
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-16f, 16f);
            rt.sizeDelta = new Vector2(180f, 52f);
        }

        // CanvasGroup on the root — show/hide controls both scrim and card
        var detailCG = detailRoot.AddComponent<CanvasGroup>();
        detailCG.alpha          = 0f;
        detailCG.blocksRaycasts = false;
        detailCG.interactable   = false;

        return researchView;
    }

    // ── Global "Research Complete" popup — shown from any view (System/Galaxy/etc.) ──
    static GameObject BuildResearchCompletePopup(Transform parent)
    {
        var root = MakeUIGO("ResearchCompletePopup", parent);
        Stretch(root);

        var scrim = MakeImage(root.transform, "Scrim", new Color(0f, 0f, 0f, 0.55f));
        Stretch(scrim);

        var panel = MakeImage(root.transform, "Panel", new Color(0.06f, 0.09f, 0.14f, 0.97f));
        {
            var rt       = panel.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(560f, 460f);
        }

        var accent = MakeImage(panel.transform, "TopAccent", AccentCyan);
        PlaceRect(accent, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -2f), v2(0f, 4f));

        var headerTMP = MakeTMP(panel.transform, "HeaderText", "Research Complete", 28, AccentCyan);
        PlaceRect(headerTMP, anchor(0f, 1f), anchor(1f, 1f), v2(16f, -36f), v2(-32f, 36f));
        headerTMP.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        headerTMP.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var iconGO = MakeUIGO("Icon", panel.transform);
        {
            var rt       = iconGO.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -78f);
            rt.sizeDelta = new Vector2(80f, 80f);
        }
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;

        var nameTMP = MakeTMP(panel.transform, "NameText", "Research Name", 30, TextWhite);
        PlaceRect(nameTMP, anchor(0f, 1f), anchor(1f, 1f), v2(16f, -190f), v2(-32f, 40f));
        nameTMP.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        nameTMP.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var descTMP = MakeTMP(panel.transform, "DescText", "Description text.", 22, TextSubtle);
        PlaceRect(descTMP, anchor(0f, 1f), anchor(1f, 1f), v2(16f, -290f), v2(-32f, 130f));
        {
            var tmp = descTMP.GetComponent<TextMeshProUGUI>();
            tmp.alignment        = TextAlignmentOptions.Top;
            tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
        }

        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);
        GameObject okGO;
        if (btnPrefab != null)
        {
            okGO      = (GameObject)Object.Instantiate(btnPrefab, panel.transform);
            okGO.name = "OkButton";
            var mb = okGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "OK";
        }
        else
        {
            okGO = MakeImage(panel.transform, "OkButton", BtnNormal);
            okGO.AddComponent<Button>();
            var lbl = MakeTMP(okGO.transform, "Label", "OK", 24, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt       = okGO.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 16f);
            rt.sizeDelta = new Vector2(200f, 52f);
        }

        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        return root;
    }

    // ── Combat view — full body area, starts hidden ──────────────────────────
    //
    // Hierarchy:
    //   CombatView                     (CombatViewController MonoBehaviour)
    //   ├─ EnemyLeftSlot / CenterSlot / RightSlot
    //   ├─ PlayerShipImage             (bottom-centre, nose up)
    //   ├─ CombatActionBar             (bottom 96 px strip)
    //   ├─ CombatLogPanel              (right-anchored, above action bar; rolling attack/repair/enemy log)
    //   │  └─ CombatLogText            (TMP_Text, updated by CombatViewController.AppendLog)
    //   ├─ ProjectileLayer             (full-stretch, above ships; projectiles spawned here at runtime)
    //   ├─ CrosshairRoot               (animated targeting crosshair, drawn last / on top)
    //   └─ ExpandedLogPanel            (CanvasGroup, hidden; tap CombatLogPanel to open)
    //      ├─ ExpandedBackground       (semi-transparent Image)
    //      ├─ ExpandedLogHeader        (52 px top strip — title + close button)
    //      └─ ExpandedLogScrollRect    (ScrollRect, fills below header)
    //         └─ Viewport              (RectMask2D)
    //            └─ ExpandedLogText    (TMP_Text + ContentSizeFitter; full-detail rolling log)
    //      ├─ AccentLine               (2 px cyan rule at top)
    //      ├─ FireTorpedesContainer      (left-anchored, 300 px)
    //      │  └─ FireTorpedesButton      (Shift MainButton — "FIRE TORPEDOES")
    //      ├─ EmergencyThrustContainer   (left-anchored, 220 px — right of torpedo button; hidden unless prop_3a researched)
    //      │  ├─ EmergencyThrustLabel    (TMP_Text — "EMERGENCY THRUST")
    //      │  ├─ EmergencyThrustButton   (Shift MainButton — "ACTIVATE")
    //      │  └─ EmergencyThrustStatusText (TMP_Text — "" / "Active: N" / "Cooldown: N")
    //      ├─ TargetInfoPanel            (centre, flexible — enemy Shield/Hull %)
    //      │  ├─ TargetInfoTitle         ("TARGET", 16 px)
    //      │  └─ TargetStatsRow          (HLG)
    //      │     ├─ TargetShieldText     ("SHIELDS  100%")
    //      │     ├─ DividerText          ("|")
    //      │     └─ TargetHullText       ("HULL  100%")
    //      ├─ PilotManeuverContainer     (right-anchored, 220 px — left of cannon button)
    //      │  ├─ ManeuverLabel           (TMP_Text — "PILOT MANEUVER")
    //      │  └─ PilotManeuverDropdown   (Shift Dropdown — Standard / Evasive / Attack Pattern)
    //      └─ FireParticleCannonContainer  (right-anchored, 300 px)
    //         └─ FireParticleCannonButton  (Shift MainButton — "FIRE PARTICLE CANNONS")

    static GameObject BuildCombatView(Transform parent)
    {
        // CombatView is a sibling of Body and NavBar (child of SystemViewController).
        // Fills all space below the 90 px header — covers the NavBar area too, so
        // the action bar at the bottom sits at the true screen edge.
        var combatView = MakeUIGO("CombatView", parent);
        {
            var rt       = combatView.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -90f);
        }
        combatView.AddComponent<CombatViewController>();

        // CanvasGroup — show/hide without disabling Shift button Animators (CLAUDE.md)
        var cg = combatView.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        // ── Enemy ship slots (upper area) ────────────────────────────────
        // Each slot is a CanvasGroup point-anchored at the centre of its screen
        // third. sizeDelta is set at runtime by CombatViewController to match
        // the sprite's natural pixel dimensions. The inner Image is pre-rotated
        // to aim at the player ship
        // (bottom-centre). Angles derived from canvas positions:
        //   Left (cx 0.18)  → atan2(-dx, dy) ≈ 235 ° (down-right)
        //   Centre           → 180 °           (straight down)
        //   Right (cx 0.82) → ≈ 125 °          (down-left)
        BuildEnemySlot(combatView.transform, "EnemyLeftSlot",   "EnemyLeftImage",
                       new Vector2(0.18f, 0.68f), 235f);
        BuildEnemySlot(combatView.transform, "EnemyCenterSlot", "EnemyCenterImage",
                       new Vector2(0.50f, 0.68f), 180f);
        BuildEnemySlot(combatView.transform, "EnemyRightSlot",  "EnemyRightImage",
                       new Vector2(0.82f, 0.68f), 125f);

        // ── Player ship — bottom-centre, ~1/3 smaller, nose up ───────────
        // Previous anchors: (0.425, 0.13)→(0.575, 0.31) = 15%×18%.
        // Reduce each span by 1/3: 10%×12%. Keep vertical centre same.
        var playerShip = MakeImage(combatView.transform, "PlayerShipImage", Color.white);
        PlaceRect(playerShip, anchor(0.45f, 0.22f), anchor(0.55f, 0.34f), v2(0f, 0f), v2(0f, 0f));
        {
            var rt = playerShip.GetComponent<RectTransform>();
            rt.localEulerAngles = Vector3.zero;  // DGB sprites face up (+Y) by default
            var img = playerShip.GetComponent<Image>();
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;
            img.color          = Color.white;
        }

        // ── Turn banner — centred overlay, hidden by default ─────────────
        // Faded in/out by CombatViewController.ShowTurnBanner() to announce
        // "Player's Turn" at combat start and after each enemy turn.
        var turnBannerGO = MakeUIGO("TurnBanner", combatView.transform);
        {
            var rt       = turnBannerGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.42f);
            rt.anchorMax = new Vector2(0.8f, 0.58f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        var turnBannerCG = turnBannerGO.AddComponent<CanvasGroup>();
        turnBannerCG.alpha          = 0f;
        turnBannerCG.blocksRaycasts = false;
        turnBannerCG.interactable   = false;

        var turnBannerTMP = MakeTMP(turnBannerGO.transform, "TurnBannerText",
                                    "Player's Turn", 54, AccentCyan);
        Stretch(turnBannerTMP);
        var tbTMP = turnBannerTMP.GetComponent<TextMeshProUGUI>();
        tbTMP.alignment  = TextAlignmentOptions.Center;
        tbTMP.fontStyle  = FontStyles.Bold;

        // Action bar — 96 px strip at the bottom ───────────────────────
        //
        // Layout (left → right):
        //   FireTorpedesContainer   (300 px, left-anchored)
        //     FireTorpedesButton    (Shift MainButton — "FIRE TORPEDOES")
        //   TargetInfoPanel         (centre, flexible)
        //     TargetInfoTitle       ("TARGET", 18 px)
        //     TargetStatsRow        (HLG)
        //       TargetShieldText    ("SHIELDS  100%")
        //       DividerText         ("|")
        //       TargetHullText      ("HULL  100%")
        //   FireParticleCannonContainer (300 px, right-anchored)
        //     FireParticleCannonButton  (Shift MainButton — "FIRE PARTICLE CANNONS")
        var actionBar = MakeImage(combatView.transform, "CombatActionBar", HeaderBar);
        PlaceRect(actionBar, anchor(0f, 0f), anchor(1f, 0f), v2(0f, 48f), v2(0f, 96f));

        // Mirror the NavBar safe-area pattern: bar background expands downward to
        // cover the home-indicator strip; buttons/containers shift inward from edges.
        var abSafeInset   = actionBar.AddComponent<SafeAreaInset>();
        var abSafeInsetSo = new SerializedObject(abSafeInset);
        abSafeInsetSo.FindProperty("_bottom").boolValue               = true;
        abSafeInsetSo.FindProperty("_expandInsteadOfShift").boolValue = true;
        abSafeInsetSo.ApplyModifiedProperties();

        var actionRule = MakeImage(actionBar.transform, "AccentLine", AccentCyan);
        PlaceRect(actionRule, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -1f), v2(0f, 2f));

        const float WeaponBtnWidth  = 300f;
        const float ActionBarPad    = 40f;   // padding from each edge of the action bar
        const float DropdownWidth   = 220f;  // pilot maneuver dropdown, to the left of cannon button
        const float DropdownGap     = 12f;   // gap between dropdown container and cannon container
        const string DropdownPrefabPath = "Assets/Shift - Complete Sci-Fi UI/Prefabs/Dropdown/Dropdown.prefab";

        // ── Left weapon button ────────────────────────────────────────────
        var torpContainer = MakeUIGO("FireTorpedesContainer", actionBar.transform);
        {
            var rt              = torpContainer.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(0f, 1f);
            rt.pivot            = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(ActionBarPad, 0f);
            rt.sizeDelta        = new Vector2(WeaponBtnWidth, -20f);
        }
        // Shift the left button inward by the device's left safe-area inset so
        // it clears the rounded display corners on iPhone (same pattern as
        // LeftComponentColumn / CrewListPanel elsewhere in GameSceneSetup).
        {
            var inset   = torpContainer.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_left").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }
        var weaponBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);
        if (weaponBtnPrefab != null)
        {
            var go  = (GameObject)Object.Instantiate(weaponBtnPrefab, torpContainer.transform);
            go.name = "FireTorpedesButton";
            var mb  = go.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Fire Torpedoes";
            var rt   = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            var go  = MakeImage(torpContainer.transform, "FireTorpedesButton", BtnNormal);
            go.AddComponent<Button>();
            var lbl = MakeTMP(go.transform, "Label", "Fire Torpedoes", 20, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── Emergency Thrust container — mirrors PilotManeuverContainer from the left ──
        // Positioned just right of the torpedo button; hidden by default (shown only
        // when prop_3a research is unlocked). Layout: label (26px) + button (40px) +
        // status text (18px) stacked, total ~90px.
        const float ThrustLabelH  = 26f;
        const float ThrustButtonH = 40f;
        const float ThrustStatusH = 18f;
        const float ThrustTotalH  = ThrustLabelH + 4f + ThrustButtonH + 2f + ThrustStatusH; // 90px
        var thrustContainer = MakeUIGO("EmergencyThrustContainer", actionBar.transform);
        {
            var rt              = thrustContainer.GetComponent<RectTransform>();
            rt.pivot            = new Vector2(0f, 0.5f);
            rt.anchorMin        = new Vector2(0f, 0.5f);
            rt.anchorMax        = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(ActionBarPad + WeaponBtnWidth + DropdownGap, 0f);
            rt.sizeDelta        = new Vector2(DropdownWidth, ThrustTotalH);
        }
        {
            var inset   = thrustContainer.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_left").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }
        // Label — top
        {
            var lbl = MakeTMP(thrustContainer.transform, "EmergencyThrustLabel", "Emergency Thrust", 20, TextSubtle);
            var rt  = lbl.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta        = new Vector2(0f, ThrustLabelH);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        // Activate button — below the label
        if (weaponBtnPrefab != null)
        {
            var go = (GameObject)Object.Instantiate(weaponBtnPrefab, thrustContainer.transform);
            go.name = "EmergencyThrustButton";
            var mb  = go.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Activate";
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(ThrustLabelH + 4f));
            rt.sizeDelta        = new Vector2(0f, ThrustButtonH);
        }
        else
        {
            var go  = MakeImage(thrustContainer.transform, "EmergencyThrustButton", BtnNormal);
            go.AddComponent<Button>();
            var lbl = MakeTMP(go.transform, "Label", "Activate", 20, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -(ThrustLabelH + 4f));
            rt.sizeDelta        = new Vector2(0f, ThrustButtonH);
        }
        // Status text — below the button (shows "Active: N" or "Cooldown: N")
        {
            var statusPos = -(ThrustLabelH + 4f + ThrustButtonH + 2f);
            var lbl = MakeTMP(thrustContainer.transform, "EmergencyThrustStatusText", "", 18, AccentCyan);
            var rt  = lbl.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, statusPos);
            rt.sizeDelta        = new Vector2(0f, ThrustStatusH);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        thrustContainer.SetActive(false);  // hidden until research unlocks it

        // ── Target info panel — centre ────────────────────────────────────
        var targetInfoPanel = MakeUIGO("TargetInfoPanel", actionBar.transform);
        {
            var rt       = targetInfoPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(ActionBarPad + WeaponBtnWidth + 8f, 6f);
            rt.offsetMax = new Vector2(-(ActionBarPad + WeaponBtnWidth + 8f), -6f);
        }
        var tipVLG = targetInfoPanel.AddComponent<VerticalLayoutGroup>();
        tipVLG.padding                = new RectOffset(0, 0, 4, 4);
        tipVLG.spacing                = 2f;
        tipVLG.childControlWidth      = true;
        tipVLG.childControlHeight     = true;
        tipVLG.childForceExpandWidth  = true;
        tipVLG.childForceExpandHeight = true;
        tipVLG.childAlignment         = TextAnchor.MiddleCenter;

        var tipTitle = MakeTMP(targetInfoPanel.transform, "TargetInfoTitle", "TARGET", 22, TextSubtle);
        tipTitle.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        tipTitle.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        {
            var le           = tipTitle.AddComponent<LayoutElement>();
            le.preferredHeight = 26f;
            le.flexibleHeight  = 0f;
        }

        var statsRow = MakeUIGO("TargetStatsRow", targetInfoPanel.transform);
        {
            var le           = statsRow.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
        }
        // childForceExpandWidth OFF so labels keep their preferred widths and pack
        // together around the centre divider instead of stretching to fill the panel.
        var statsHLG = statsRow.AddComponent<HorizontalLayoutGroup>();
        statsHLG.padding                = new RectOffset(0, 0, 0, 0);
        statsHLG.spacing                = 8f;
        statsHLG.childControlWidth      = true;
        statsHLG.childControlHeight     = true;
        statsHLG.childForceExpandWidth  = false;
        statsHLG.childForceExpandHeight = true;
        statsHLG.childAlignment         = TextAnchor.MiddleCenter;

        var targetShieldText = MakeTMP(statsRow.transform, "TargetShieldText", "Shields  100%", 24, TextWhite);
        targetShieldText.GetComponent<TextMeshProUGUI>().alignment          = TextAlignmentOptions.Right;
        targetShieldText.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        {
            var le          = targetShieldText.AddComponent<LayoutElement>();
            le.preferredWidth = 210f;
            le.flexibleWidth  = 0f;
        }

        var tipDivider = MakeTMP(statsRow.transform, "DividerText", "|", 24, TextSubtle);
        tipDivider.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        {
            var le          = tipDivider.AddComponent<LayoutElement>();
            le.preferredWidth = 20f;
            le.flexibleWidth  = 0f;
        }

        var targetHullText = MakeTMP(statsRow.transform, "TargetHullText", "Hull  100%", 24, TextWhite);
        targetHullText.GetComponent<TextMeshProUGUI>().alignment          = TextAlignmentOptions.Left;
        targetHullText.GetComponent<TextMeshProUGUI>().textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        {
            var le          = targetHullText.AddComponent<LayoutElement>();
            le.preferredWidth = 190f;
            le.flexibleWidth  = 0f;
        }

        // ── Pilot maneuver dropdown — to the left of the cannon button ─────
        // Uses the Shift Dropdown prefab (styled TMP_Dropdown, 300×40 default).
        // Anchored from the right edge: position = -(ActionBarPad + WeaponBtnWidth + DropdownGap),
        // width = DropdownWidth. A label above names the control.
        // Maneuver container: fixed height, vertically centered in the action bar
        // Label (26px) + 4px gap + dropdown (40px) = 70px total
        const float ManeuverLabelH   = 26f;
        const float ManeuverDropH    = 40f;
        const float ManeuverTotalH   = ManeuverLabelH + 4f + ManeuverDropH; // 70px
        var maneuverContainer = MakeUIGO("PilotManeuverContainer", actionBar.transform);
        {
            var rt              = maneuverContainer.GetComponent<RectTransform>();
            rt.pivot            = new Vector2(1f, 0.5f);
            rt.anchorMin        = new Vector2(1f, 0.5f);
            rt.anchorMax        = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-(ActionBarPad + WeaponBtnWidth + DropdownGap), 0f);
            rt.sizeDelta        = new Vector2(DropdownWidth, ManeuverTotalH);
        }
        {
            var inset   = maneuverContainer.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_right").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }
        // Label — top of the container
        {
            var lbl = MakeTMP(maneuverContainer.transform, "ManeuverLabel", "Pilot Maneuver", 24, TextSubtle);
            var rt  = lbl.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta        = new Vector2(0f, ManeuverLabelH);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        // Dropdown — bottom of the container, fixed height
        var dropdownPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DropdownPrefabPath);
        if (dropdownPrefab != null)
        {
            var go = (GameObject)Object.Instantiate(dropdownPrefab, maneuverContainer.transform);
            go.name = "PilotManeuverDropdown";
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(1f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 0f);
            rt.sizeDelta        = new Vector2(0f, ManeuverDropH);
        }
        else
        {
            Debug.LogWarning("[GameSceneSetup] Shift Dropdown prefab not found at " + DropdownPrefabPath +
                             " — PilotManeuverDropdown will be missing.");
        }

        // ── Right weapon button ───────────────────────────────────────────
        var cannonContainer = MakeUIGO("FireParticleCannonContainer", actionBar.transform);
        {
            var rt              = cannonContainer.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 0f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-ActionBarPad, 0f);
            rt.sizeDelta        = new Vector2(WeaponBtnWidth, -20f);
        }
        // Shift the right button inward by the device's right safe-area inset
        // (mirrors the _left inset on FireTorpedesContainer).
        {
            var inset   = cannonContainer.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_right").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }
        if (weaponBtnPrefab != null)
        {
            var go  = (GameObject)Object.Instantiate(weaponBtnPrefab, cannonContainer.transform);
            go.name = "FireParticleCannonButton";
            var mb  = go.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Fire Particle Cannons";
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            var go  = MakeImage(cannonContainer.transform, "FireParticleCannonButton", BtnNormal);
            go.AddComponent<Button>();
            var lbl = MakeTMP(go.transform, "Label", "Fire Particle Cannons", 20, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            Stretch(go);
        }

        // ── Combat log panel — right side, between right enemy slot and action bar ──
        //
        // Anchored to the right edge, bottom-aligned with a gap above the action bar.
        // SafeAreaInset(_right) mirrors the cannon button inset.
        // CombatViewController.AppendLog() refreshes the TMP_Text child at runtime.
        var logPanel = MakeImage(combatView.transform, "CombatLogPanel",
            new Color(HeaderBar.r, HeaderBar.g, HeaderBar.b, 0.82f));
        {
            var rt = logPanel.GetComponent<RectTransform>();
            rt.pivot            = new Vector2(1f, 0f);        // right-bottom — set BEFORE anchoredPosition (CLAUDE.md)
            rt.anchorMin        = new Vector2(1f, 0f);
            rt.anchorMax        = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(0f, 170f);              // right edge flush with safe-area border (SafeAreaInset handles the inset); above action bar
            rt.sizeDelta        = new Vector2(800f, 170f);            // ~380 displayed px at current scale; right edge at safe-area boundary
        }
        {
            var inset   = logPanel.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_right").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }
        {
            // Tap the compact log to open the expanded full-detail view.
            var btn = logPanel.AddComponent<Button>();
            var cb  = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            cb.pressedColor     = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.selectedColor    = Color.white;
            btn.colors          = cb;
        }
        {
            var logTextGO = MakeTMP(logPanel.transform, "CombatLogText", "", 30, TextSubtle);
            var rt        = logTextGO.GetComponent<RectTransform>();
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.offsetMin  = new Vector2(10f, 6f);
            rt.offsetMax  = new Vector2(-10f, -6f);
            var tmp                  = logTextGO.GetComponent<TextMeshProUGUI>();
            tmp.alignment            = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.richText             = true;
            tmp.overflowMode         = TextOverflowModes.Truncate;
        }

        // ── Projectile layer — full-stretch, above ships/crosshair ───────
        // CombatViewController.LaunchProjectile() instantiates transient Image
        // children into this layer at runtime. It never blocks raycasts so
        // enemy tap-buttons remain interactive during a shot animation.
        var projectileLayer = MakeUIGO("ProjectileLayer", combatView.transform);
        {
            var rt       = projectileLayer.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        var projLayerCG          = projectileLayer.AddComponent<CanvasGroup>();
        projLayerCG.alpha        = 1f;
        projLayerCG.blocksRaycasts = false;
        projLayerCG.interactable   = false;

        // ── Targeting crosshair — centred in the enemy arena ─────────────
        // CrosshairRoot sits at the vertical midpoint of the enemy area
        // (y ≈ 0.65 in CombatView space).
        //   crosshairOuter — xArrowheadInwards128Glow (expands on firing)
        // CombatCrosshair tints it blue in Start().
        var crosshairRoot = MakeUIGO("CrosshairRoot", combatView.transform);
        {
            var rt              = crosshairRoot.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.68f);
            rt.anchorMax        = new Vector2(0.5f, 0.68f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(200f, 200f);
        }
        // CanvasGroup — Hide() sets alpha=0; Start() hides immediately until targeted.
        var chCG       = crosshairRoot.AddComponent<CanvasGroup>();
        chCG.alpha     = 0f;
        crosshairRoot.AddComponent<CombatCrosshair>();

        // outer — inward arrowhead ring (expands on Pulse)
        var crosshairOuter = MakeUIGO("CrosshairOuter", crosshairRoot.transform);
        {
            var ri       = crosshairOuter.AddComponent<RawImage>();
            ri.color     = Color.white;  // tinted to blue by CombatCrosshair.Start()
            var rt       = crosshairOuter.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ── Expanded combat log overlay ──────────────────────────────────────
        // Tapping CombatLogPanel toggles this panel.  It covers the full combat
        // arena (above the action bar) and contains a scrollable full-detail log.
        // Hidden by default via CanvasGroup — no Shift buttons inside so
        // SetActive(false) would also be safe, but CanvasGroup is consistent.
        var expandedLogPanel = MakeUIGO("ExpandedLogPanel", combatView.transform);
        {
            var rt       = expandedLogPanel.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16f, 180f);  // 96 px action bar + compact-log height (170) + gap; safe-area adds more at runtime
            rt.offsetMax = new Vector2(-16f, -8f);
        }
        var expandedLogCG          = expandedLogPanel.AddComponent<CanvasGroup>();
        expandedLogCG.alpha        = 0f;
        expandedLogCG.blocksRaycasts = false;
        expandedLogCG.interactable   = false;

        // Safe-area inset — shrink each edge independently so notches/rounded
        // corners and the home indicator don't clip content.
        // _shrinkTowardSafeArea: oMin.x += leftPx, oMax.x -= rightPx, oMin.y += bottomPx
        // — each edge moves inward on its own, with no cross-axis coupling.
        {
            var inset   = expandedLogPanel.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_left").boolValue              = true;
            insetSo.FindProperty("_right").boolValue             = true;
            insetSo.FindProperty("_bottom").boolValue            = true;
            insetSo.FindProperty("_shrinkTowardSafeArea").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }

        // Dark semi-transparent background — same colour as the compact log panel.
        var expandedBg = MakeImage(expandedLogPanel.transform, "ExpandedBackground",
            new Color(HeaderBar.r, HeaderBar.g, HeaderBar.b, 0.95f));
        {
            var rt       = expandedBg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Header strip — title on the left, close button on the right.
        var expandedHeader = MakeUIGO("ExpandedLogHeader", expandedLogPanel.transform);
        {
            var rt       = expandedHeader.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0.5f, 1f);          // set pivot BEFORE anchoredPosition
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, -52f);
            rt.offsetMax = Vector2.zero;
        }

        var expandedTitleGO = MakeTMP(expandedHeader.transform, "ExpandedLogTitle",
                                      "COMBAT LOG", 22, TextWhite);
        {
            var rt = expandedTitleGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(14f, 0f);
            rt.offsetMax = new Vector2(-60f, 0f);
            var tmp = expandedTitleGO.GetComponent<TextMeshProUGUI>();
            tmp.alignment  = TextAlignmentOptions.Left;
            tmp.fontStyle  = FontStyles.Bold;
            tmp.richText   = false;
        }

        // Close button — plain Button + "×" label (no Shift prefab needed).
        var closeGO = MakeUIGO("ExpandedLogCloseButton", expandedHeader.transform);
        {
            var rt       = closeGO.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(1f, 0.5f);          // set pivot BEFORE anchoredPosition
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-52f, 4f);
            rt.offsetMax = new Vector2(-4f, -4f);
        }
        closeGO.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 0.9f);
        closeGO.AddComponent<Button>();
        var closeLabelGO = MakeTMP(closeGO.transform, "CloseLabel", "X", 22, TextWhite);
        {
            var rt       = closeLabelGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            closeLabelGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }

        // ScrollRect — fills everything below the header.
        var scrollGO = MakeUIGO("ExpandedLogScrollRect", expandedLogPanel.transform);
        {
            var rt       = scrollGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, -52f);   // below header
        }
        var scrollRect              = scrollGO.AddComponent<ScrollRect>();
        scrollRect.horizontal       = false;
        scrollRect.vertical         = true;
        scrollRect.scrollSensitivity = 40f;
        scrollRect.movementType     = ScrollRect.MovementType.Clamped;

        // Viewport — RectMask2D (avoids the Mask+Image.color.clear gotcha from CLAUDE.md).
        var viewportGO = MakeUIGO("Viewport", scrollGO.transform);
        {
            var rt       = viewportGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12f, 6f);
            rt.offsetMax = new Vector2(-12f, -6f);
        }
        viewportGO.AddComponent<RectMask2D>();

        // ExpandedLogText — the scroll content; auto-sizes height to fit all lines.
        // anchorMin/Max = top-stretch so it spans full width; ContentSizeFitter
        // grows the height as text is appended at runtime.
        var expandedLogTextGO = MakeTMP(viewportGO.transform, "ExpandedLogText", "", 26, TextSubtle);
        {
            var rt       = expandedLogTextGO.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0.5f, 1f);          // top-centre — set pivot BEFORE anchoredPosition
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);
            var tmp               = expandedLogTextGO.GetComponent<TextMeshProUGUI>();
            tmp.alignment         = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            tmp.richText          = true;
            tmp.overflowMode      = TextOverflowModes.Overflow;
        }
        var expandedCsf         = expandedLogTextGO.AddComponent<ContentSizeFitter>();
        expandedCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        expandedCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Wire ScrollRect references.
        scrollRect.viewport = viewportGO.GetComponent<RectTransform>();
        scrollRect.content  = expandedLogTextGO.GetComponent<RectTransform>();

        return combatView;
    }

    // ── Enemy slot — point-anchored CanvasGroup + Image child ───────────────
    //
    //   slotCenter  — normalised position of the slot's pivot in CombatView space.
    //   imageRotZ   — pre-baked z-rotation of the ship image so it aims at the
    //                 player ship (bottom-centre of the arena).

    static void BuildEnemySlot(Transform parent, string slotName, string imageName,
                                Vector2 slotCenter, float imageRotZ)
    {
        // Slot root — point-anchored at slotCenter; CombatViewController sets
        // sizeDelta at runtime to match the sprite's natural pixel dimensions.
        var slot = MakeUIGO(slotName, parent);
        {
            var rt              = slot.GetComponent<RectTransform>();
            rt.anchorMin        = slotCenter;
            rt.anchorMax        = slotCenter;
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(240f, 213f); // Large — runtime overrides this
        }
        var slotCG = slot.AddComponent<CanvasGroup>();
        slotCG.alpha          = 0f;
        slotCG.blocksRaycasts = false;
        slotCG.interactable   = false;

        // Ship image — stretch-fills the slot; pre-rotated to face the player.
        var img = MakeImage(slot.transform, imageName, Color.white);
        {
            var rt              = img.GetComponent<RectTransform>();
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.offsetMin        = Vector2.zero;
            rt.offsetMax        = Vector2.zero;
            rt.localEulerAngles = new Vector3(0f, 0f, imageRotZ);
        }
        var imgComp = img.GetComponent<Image>();
        imgComp.preserveAspect = true;
        imgComp.type           = Image.Type.Simple;
        imgComp.color          = Color.white;

        // Button — lets the player tap the ship to target it.
        // Transition.None so the Image sprite/color is not touched by the Button.
        // onClick listener is added at runtime in CombatViewController.Start().
        var btn = img.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
    }

    // -----------------------------------------------------------------------
    // Nav bar — bottom 80 px with four centred navigation buttons
    // -----------------------------------------------------------------------

    static GameObject BuildNavBar(Transform parent)
    {
        // Base height 96 (not 80): the button container is top-anchored with an
        // 8 px gap, so buttons clear the home-indicator strip by (baseHeight - 72)
        // canvas units after the bar expands by the inset. 80 gave only 8 px of
        // clearance (buttons sat on the indicator); 96 gives a comfortable 24 px.
        // NOTE: BuildBody's bottom reserve MUST match this (96) or the body and
        // nav bar will not be flush on home-indicator devices.
        var bar = MakeImage(parent, "NavBar", HeaderBar);
        PlaceRect(bar, anchor(0f, 0f), anchor(1f, 0f), v2(0f, 48f), v2(0f, 96f));

        // CanvasGroup — used by SystemViewController to hide the bar during
        // combat (alpha=0, blocksRaycasts=false) while keeping Shift button
        // Animators continuously bound (SetActive(false) would break them).
        bar.AddComponent<CanvasGroup>();

        // On devices with a home indicator (iPhone X+) the bar expands downward
        // so its background covers the indicator strip. The button container is
        // top-anchored inside the bar, so buttons stay in the safe zone above it.
        var navSafeInset   = bar.AddComponent<SafeAreaInset>();
        var navSafeInsetSo = new SerializedObject(navSafeInset);
        navSafeInsetSo.FindProperty("_bottom").boolValue             = true;
        navSafeInsetSo.FindProperty("_expandInsteadOfShift").boolValue = true;
        navSafeInsetSo.ApplyModifiedProperties();

        var rule = MakeImage(bar.transform, "AccentLine", AccentCyan);
        PlaceRect(rule, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -1f), v2(0f, 2f));

        // Button container — bottom-anchored with a 16 px base gap above the bar
        // bottom (= screen bottom), then lifted by a plain SHIFT SafeAreaInset on
        // the BOTTOM edge. This is the exact pattern the ShipView component columns
        // use for left/right notches: put a shift inset directly on the content
        // that must move, rather than relying on the parent bar's expand to carry
        // it. On a home-indicator device the buttons rise by the inset amount and
        // keep a constant 16 px clearance above the indicator, independent of the
        // inset size. The bar still expands (so it grows tall enough to contain the
        // lifted buttons and its background covers the indicator strip).
        var container = MakeUIGO("ButtonContainer", bar.transform);
        PlaceRect(container, anchor(0.5f, 0f), anchor(0.5f, 0f), v2(0f, 16f), v2(860f, 64f));
        container.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);

        var btnSafeInset   = container.AddComponent<SafeAreaInset>();
        var btnSafeInsetSo = new SerializedObject(btnSafeInset);
        btnSafeInsetSo.FindProperty("_bottom").boolValue = true;   // shift mode (default)
        btnSafeInsetSo.ApplyModifiedProperties();

        var hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(0, 0, 0, 0);
        hlg.spacing                = 16f;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment         = TextAnchor.MiddleCenter;

        BuildNavButton(container.transform, "SystemButton",   "System");
        BuildNavButton(container.transform, "GalaxyButton",   "Galaxy");
        BuildNavButton(container.transform, "ShipButton",     "Ship");
        BuildNavButton(container.transform, "CrewButton",     "Crew");
        BuildNavButton(container.transform, "ResearchButton", "Research");

        // ── Debug button container — bottom-left ─────────────────────────────────
        // Battle + Random buttons sit outside the centred nav area.
        // Container shifts right/up via SafeAreaInset so buttons clear the
        // left notch and bottom home indicator on iPhone X+ devices.
        var debugContainer = MakeUIGO("DebugButtonContainer", bar.transform);
        {
            var rt = debugContainer.GetComponent<RectTransform>();
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(8f, 16f);
            rt.sizeDelta = new Vector2(376f, 56f);
        }
        {
            var inset   = debugContainer.AddComponent<SafeAreaInset>();
            var insetSo = new SerializedObject(inset);
            insetSo.FindProperty("_left").boolValue   = true;
            insetSo.FindProperty("_bottom").boolValue = true;
            insetSo.ApplyModifiedProperties();
        }
        var dbgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);
        if (dbgPrefab != null)
        {
            var battleGO = (GameObject)Object.Instantiate(dbgPrefab, debugContainer.transform);
            battleGO.name = "CombatDebugButton";
            var bMB = battleGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (bMB != null) bMB.buttonText = "Battle";
            var bRT = battleGO.GetComponent<RectTransform>();
            bRT.pivot     = new Vector2(0f, 0.5f);
            bRT.anchorMin = bRT.anchorMax = new Vector2(0f, 0.5f);
            bRT.anchoredPosition = new Vector2(0f, 0f);
            bRT.sizeDelta = new Vector2(180f, 56f);

            var randomGO = (GameObject)Object.Instantiate(dbgPrefab, debugContainer.transform);
            randomGO.name = "RandomCombatButton";
            var rMB = randomGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (rMB != null) rMB.buttonText = "Random";
            var rRT = randomGO.GetComponent<RectTransform>();
            rRT.pivot     = new Vector2(0f, 0.5f);
            rRT.anchorMin = rRT.anchorMax = new Vector2(0f, 0.5f);
            rRT.anchoredPosition = new Vector2(196f, 0f);
            rRT.sizeDelta = new Vector2(180f, 56f);
        }
        else
        {
            // Fallback — plain buttons without the Shift prefab
            string[] dbgNames   = { "CombatDebugButton", "RandomCombatButton" };
            string[] dbgLabels  = { "Battle",            "Random" };
            float[]  dbgOffsets = { 0f,                   196f    };
            for (int di = 0; di < dbgNames.Length; di++)
            {
                var go  = MakeImage(debugContainer.transform, dbgNames[di], BtnNormal);
                go.AddComponent<Button>();
                var lbl = MakeTMP(go.transform, "Label", dbgLabels[di], 20, TextWhite);
                Stretch(lbl);
                lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
                var rt  = go.GetComponent<RectTransform>();
                rt.pivot     = new Vector2(0f, 0.5f);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(dbgOffsets[di], 0f);
                rt.sizeDelta = new Vector2(180f, 56f);
            }
        }

        return bar;
    }

    static void BuildNavButton(Transform parent, string name, string label)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);
        if (prefab != null)
        {
            var go = (GameObject)Object.Instantiate(prefab, parent);
            go.name = name;
            var mb = go.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = label;
        }
        else
        {
            Debug.LogWarning($"[GameSceneSetup] Shift MainButton prefab not found for '{name}'.");
            var go  = MakeImage(parent, name, BtnNormal);
            var btn = go.AddComponent<Button>();
            var col = btn.colors;
            col.highlightedColor = new Color(0.12f, 0.38f, 0.60f, 1f);
            col.pressedColor     = new Color(0.04f, 0.16f, 0.28f, 1f);
            btn.colors           = col;
            btn.targetGraphic    = go.GetComponent<Image>();
            var lbl = MakeTMP(go.transform, "Label", label.ToUpper(), 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
    }

    // -----------------------------------------------------------------------
    // POI Detail Panel
    // -----------------------------------------------------------------------

    static GameObject BuildPOIDetailPanel(Transform parent)
    {
        var panel = MakeImage(parent, "POIDetailPanel", Scrim);
        Stretch(panel);
        panel.SetActive(false);

        var card = MakeImage(panel.transform, "Card", PanelBg);
        PlaceRect(card, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f), v2(0f, 0f), v2(700f, 480f));

        var topBar = MakeImage(card.transform, "TopBar", AccentCyan);
        PlaceRect(topBar, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -2f), v2(0f, 4f));

        var poiName = MakeTMP(card.transform, "POIDetailNameText", "POI Name", 52, TextWhite);
        PlaceRect(poiName, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -50f), v2(-60f, 68f));
        poiName.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        poiName.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var poiType = MakeTMP(card.transform, "POIDetailTypeText", "Planet", 32, AccentCyan);
        PlaceRect(poiType, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -116f), v2(-60f, 44f));
        poiType.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        poiType.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        var ruleGO = MakeImage(card.transform, "Rule", DividerColor);
        PlaceRect(ruleGO, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -150f), v2(-40f, 2f));

        var desc = MakeTMP(card.transform, "POIDetailDescText", "A rocky world with varied terrain.", 30, TextSubtle);
        PlaceRect(desc, anchor(0f, 0f), anchor(1f, 1f), v2(0f, -45f), v2(-60f, -234f));
        var descTMP               = desc.GetComponent<TextMeshProUGUI>();
        descTMP.alignment         = TextAlignmentOptions.TopLeft;
        descTMP.textWrappingMode = TMPro.TextWrappingModes.Normal;
        descTMP.overflowMode      = TextOverflowModes.ScrollRect;

        // Scanner upgrade hint — shown when scanner level is too low to display all info
        var scannerHint = MakeTMP(card.transform, "POIDetailScannerText",
                                  "Upgrade Scanner to reveal more information", 22,
                                  new Color(1.00f, 0.80f, 0.20f, 1f)); // amber
        PlaceRect(scannerHint, anchor(0f, 0f), anchor(1f, 0f), v2(0f, 124f), v2(-60f, 32f));
        var scannerHintTMP = scannerHint.GetComponent<TextMeshProUGUI>();
        scannerHintTMP.alignment          = TextAlignmentOptions.Center;
        scannerHintTMP.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        scannerHintTMP.fontStyle          = FontStyles.Italic;
        scannerHint.SetActive(false); // hidden by default; shown at runtime when needed

        // ── Resource icon strip — top-right corner ────────────────────────────
        // Three 36×36 icons (He-3, Iridium, Salvage) side by side, shown/tinted
        // at runtime by SystemViewController based on scanner tier and orbit state.
        {
            var iconPanel = MakeUIGO("POIDetailResourcePanel", card.transform);
            var panelRT   = iconPanel.GetComponent<RectTransform>();
            panelRT.anchorMin        = panelRT.anchorMax = new Vector2(1f, 1f);
            panelRT.pivot            = new Vector2(1f, 1f);
            panelRT.anchoredPosition = new Vector2(-20f, -10f);
            panelRT.sizeDelta        = new Vector2(124f, 36f);
            var hlg               = iconPanel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing           = 8f;
            hlg.childAlignment    = TextAnchor.MiddleRight;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            void MakeResourceIcon(string goName, string spritePath)
            {
                var go  = MakeUIGO(goName, iconPanel.transform);
                var rt  = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(36f, 36f);
                var img = go.AddComponent<Image>();
                img.preserveAspect = true;
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sp != null) img.sprite = sp;
                else Debug.LogWarning($"[GameSceneSetup] Resource icon not found: {spritePath}");
                go.SetActive(false); // shown at runtime when resource is present
            }

            MakeResourceIcon("POIDetailHe3Icon",     "Assets/Art/UI/Other/fuel.png");
            MakeResourceIcon("POIDetailIridiumIcon", "Assets/Art/UI/Other/iridium.png");
            MakeResourceIcon("POIDetailSalvageIcon", "Assets/Art/UI/Other/salvage.png");
        }

        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);

        // SET COURSE button — bottom-left
        GameObject navigateGO;
        if (btnPrefab != null)
        {
            navigateGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            navigateGO.name = "POIDetailNavigateButton";
            var mb = navigateGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Set Course";
        }
        else
        {
            navigateGO = MakeImage(card.transform, "POIDetailNavigateButton", BtnNormal);
            navigateGO.AddComponent<Button>();
            var lbl = MakeTMP(navigateGO.transform, "Label", "Set Course", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt = navigateGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(200f, 48f);
            rt.anchoredPosition = new Vector2(20f, 12f);
        }

        // COLLECT button — bottom-left, same position as Set Course; shown when ship is at this POI
        GameObject collectGO;
        if (btnPrefab != null)
        {
            collectGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            collectGO.name = "POIDetailCollectButton";
            var mb = collectGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Collect";
        }
        else
        {
            collectGO = MakeImage(card.transform, "POIDetailCollectButton", BtnNormal);
            collectGO.AddComponent<Button>();
            var lbl = MakeTMP(collectGO.transform, "Label", "Collect", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt = collectGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.sizeDelta = new Vector2(200f, 48f);
            rt.anchoredPosition = new Vector2(20f, 12f);
        }
        // Keep active so the Shift MainButton Animator stays bound — hidden via CanvasGroup at runtime.

        // CLOSE button — bottom-right
        GameObject closeGO;
        if (btnPrefab != null)
        {
            closeGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            closeGO.name = "POIDetailCloseButton";
            var mb = closeGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Close";
        }
        else
        {
            Debug.LogWarning("[GameSceneSetup] Shift MainButton prefab not found for close.");
            closeGO = MakeImage(card.transform, "POIDetailCloseButton", BtnNormal);
            var closeBtn       = closeGO.AddComponent<Button>();
            var cc             = closeBtn.colors;
            cc.highlightedColor = new Color(0.12f, 0.38f, 0.60f, 1f);
            cc.pressedColor     = new Color(0.04f, 0.16f, 0.28f, 1f);
            closeBtn.colors    = cc;
            closeBtn.targetGraphic = closeGO.GetComponent<Image>();
            var closeLbl = MakeTMP(closeGO.transform, "Label", "Close", 22, TextWhite);
            Stretch(closeLbl);
            closeLbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        PlaceRect(closeGO, anchor(1f, 0f), anchor(1f, 0f), v2(-102f, 36f), v2(180f, 48f));

        // HE-3 + DAYS labels — LabelControl prefabs, side-by-side above the Set Course button
        // Button bottom edge is at y=12, top at y=60. Labels sit just above at y=68.
        const string LabelControlPrefabPath = "Assets/Prefabs/UI/LabelControl.prefab";
        var labelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabelControlPrefabPath);
        if (labelPrefab != null)
        {
            // He-3 fuel cost
            var fuelLabel = (GameObject)Object.Instantiate(labelPrefab, card.transform);
            fuelLabel.name = "POIDetailFuelCostLabel";
            var fuelRT     = fuelLabel.GetComponent<RectTransform>();
            fuelRT.anchorMin = fuelRT.anchorMax = new Vector2(0f, 0f);
            fuelRT.pivot     = new Vector2(0f, 0f);
            fuelRT.anchoredPosition = new Vector2(20f, 68f);
            var fuelTitle = fuelLabel.transform.Find("Normal/Border/Title")?.GetComponent<TMP_Text>();
            if (fuelTitle != null) fuelTitle.text = "He-3";

            // Days travel time
            var daysLabel = (GameObject)Object.Instantiate(labelPrefab, card.transform);
            daysLabel.name = "POIDetailDaysLabel";
            var daysRT     = daysLabel.GetComponent<RectTransform>();
            daysRT.anchorMin = daysRT.anchorMax = new Vector2(0f, 0f);
            daysRT.pivot     = new Vector2(0f, 0f);
            daysRT.anchoredPosition = new Vector2(168f, 68f);
            var daysTitle = daysLabel.transform.Find("Normal/Border/Title")?.GetComponent<TMP_Text>();
            if (daysTitle != null) daysTitle.text = "Days";
        }
        else
        {
            Debug.LogWarning("[GameSceneSetup] LabelControl prefab not found at " + LabelControlPrefabPath);
        }

        return panel;
    }

    // -----------------------------------------------------------------------
    // Recruitment Panel — full-screen CanvasGroup overlay, sibling of Body
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // Level Up Panel — full-screen CanvasGroup overlay, sibling of Body
    // -----------------------------------------------------------------------

    static GameObject BuildLevelUpPanel(Transform parent)
    {
        // Root — stays active; show/hide via CanvasGroup.
        // Never call SetActive(false) — contains Shift buttons whose Animators must
        // remain continuously bound to avoid the stuck-highlight bug.
        var panel = MakeUIGO("LevelUpPanel", parent);
        Stretch(panel);
        var cg = panel.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;
        panel.AddComponent<LevelUpController>();

        // Scrim — dark semi-transparent backdrop
        var scrim = MakeImage(panel.transform, "Scrim", Scrim);
        Stretch(scrim);

        // Card — wide (2× the single-column version) to host two skill columns side-by-side
        var card = MakeImage(panel.transform, "Card", PanelBg);
        PlaceRect(card, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f), v2(0f, 0f), v2(1400f, 580f));

        // Top cyan accent bar
        var topBar = MakeImage(card.transform, "TopBar", AccentCyan);
        PlaceRect(topBar, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -2f), v2(0f, 4f));

        // "LEVEL UP" title
        var title = MakeTMP(card.transform, "TitleText", "Level Up", 42, TextWhite);
        PlaceRect(title, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -32f), v2(-60f, 52f));
        title.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        title.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Crew name — updated at runtime
        var crewName = MakeTMP(card.transform, "CrewNameText", "Crew Name", 28, AccentCyan);
        PlaceRect(crewName, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -82f), v2(-60f, 36f));
        crewName.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        crewName.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        // Points remaining — updated at runtime; amber when points > 0, subtle when 0
        var points = MakeTMP(card.transform, "PointsText", "3 skill points to spend", 24,
                             new Color(1.00f, 0.75f, 0.20f, 1f));
        PlaceRect(points, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -116f), v2(-60f, 30f));
        points.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

        // Divider below header
        var divider = MakeImage(card.transform, "DividerRule", DividerColor);
        PlaceRect(divider, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -148f), v2(-40f, 2f));

        // ── Two-column skills area ────────────────────────────────────────────
        // ColumnsArea: HLG, fills the middle between dividers.
        // Left column gets skills 0-4, right column gets skills 5-8.
        // No ScrollRect needed — 5 rows × 46px fits comfortably in the available height.
        var columnsGO = MakeUIGO("ColumnsArea", card.transform);
        {
            var rt       = columnsGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 148f);   // 148px above bottom (82 buttons + 58 recommended + 8 gap)
            rt.offsetMax = new Vector2(-20f, -152f); // 152px below card top (below divider)
        }
        var hlg = columnsGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 20f;
        hlg.padding                = new RectOffset(0, 0, 0, 0);
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        // Left column VLG — SkillsContainer (rows 0-4)
        var leftCol = MakeUIGO("SkillsContainer", columnsGO.transform);
        var leftVLG = leftCol.AddComponent<VerticalLayoutGroup>();
        leftVLG.padding                = new RectOffset(4, 4, 4, 4);
        leftVLG.spacing                = 4f;
        leftVLG.childControlWidth      = true;
        leftVLG.childControlHeight     = false;
        leftVLG.childForceExpandWidth  = true;
        leftVLG.childForceExpandHeight = false;
        leftVLG.childAlignment         = TextAnchor.UpperLeft;

        // Right column VLG — SkillsContainerRight (rows 5-8)
        var rightCol = MakeUIGO("SkillsContainerRight", columnsGO.transform);
        var rightVLG = rightCol.AddComponent<VerticalLayoutGroup>();
        rightVLG.padding                = new RectOffset(4, 4, 4, 4);
        rightVLG.spacing                = 4f;
        rightVLG.childControlWidth      = true;
        rightVLG.childControlHeight     = false;
        rightVLG.childForceExpandWidth  = true;
        rightVLG.childForceExpandHeight = false;
        rightVLG.childAlignment         = TextAnchor.UpperLeft;

        // ── Skill rows (built statically) ─────────────────────────────────────
        // The +/- buttons are built here, at scene-build time, rather than being
        // instantiated at runtime. Freshly-instantiated Shift buttons hit the
        // Animator playable-binding race (see CLAUDE.md) and never settle to their
        // visible Normal rest frame. Built into the scene, their Animators bind at
        // scene load and settle correctly — exactly like the CANCEL/CONFIRM
        // MainButtons in this same panel. LevelUpController binds to these rows by
        // name ("Row_<skill>") at runtime and only updates their values.
        var squadBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SquadMemberBtnPrefabPath);
        if (squadBtnPrefab == null)
            Debug.LogWarning("[GameSceneSetup] Squad Member Button prefab not found — skill +/- will use plain buttons.");

        // Force-import the icon PNGs if Unity hasn't processed them yet.
        AssetDatabase.ImportAsset(PlusIconPath,  ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(MinusIconPath, ImportAssetOptions.ForceSynchronousImport);
        var plusSprite  = AssetDatabase.LoadAssetAtPath<Sprite>(PlusIconPath);
        var minusSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MinusIconPath);
        if (plusSprite  == null) Debug.LogWarning("[GameSceneSetup] icon_plus.png not found at " + PlusIconPath);
        if (minusSprite == null) Debug.LogWarning("[GameSceneSetup] icon_minus.png not found at " + MinusIconPath);

        var allSkills = Constants.Skills.All;
        int skillSplit = (allSkills.Length + 1) / 2; // left column gets the larger share
        for (int i = 0; i < allSkills.Length; i++)
        {
            var col = (i < skillSplit) ? leftCol.transform : rightCol.transform;
            BuildSkillRowStatic(allSkills[i], col, squadBtnPrefab, plusSprite, minusSprite);
        }

        // Divider above buttons
        var divider2 = MakeImage(card.transform, "DividerRule2", DividerColor);
        PlaceRect(divider2, anchor(0f, 0f), anchor(1f, 0f), v2(0f, 80f), v2(-40f, 2f));

        // CANCEL and CONFIRM buttons — centred pair at card bottom (pivot-first placement)
        var mainBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);

        GameObject cancelGO;
        if (mainBtnPrefab != null)
        {
            cancelGO      = (GameObject)Object.Instantiate(mainBtnPrefab, card.transform);
            cancelGO.name = "CancelButton";
            var mb = cancelGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Cancel";
        }
        else
        {
            cancelGO = MakeImage(card.transform, "CancelButton", BtnNormal);
            cancelGO.AddComponent<Button>();
            var lbl = MakeTMP(cancelGO.transform, "Label", "Cancel", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt              = cancelGO.GetComponent<RectTransform>();
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(-120f, 14f);
            rt.sizeDelta        = new Vector2(210f, 52f);
        }

        GameObject confirmGO;
        if (mainBtnPrefab != null)
        {
            confirmGO      = (GameObject)Object.Instantiate(mainBtnPrefab, card.transform);
            confirmGO.name = "ConfirmButton";
            var mb = confirmGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Confirm";
        }
        else
        {
            confirmGO = MakeImage(card.transform, "ConfirmButton", BtnNormal);
            confirmGO.AddComponent<Button>();
            var lbl = MakeTMP(confirmGO.transform, "Label", "Confirm", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt              = confirmGO.GetComponent<RectTransform>();
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(120f, 14f);
            rt.sizeDelta        = new Vector2(210f, 52f);
        }

        // RECOMMENDED button — centred between Cancel and Confirm; only shown for
        // the level-1 captain first-launch screen. Hidden by default; LevelUpController
        // activates it when showing a locked level-up (first-launch captain only).
        GameObject recommendedGO;
        if (mainBtnPrefab != null)
        {
            recommendedGO      = (GameObject)Object.Instantiate(mainBtnPrefab, card.transform);
            recommendedGO.name = "RecommendedButton";
            var mb = recommendedGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "Recommended";
        }
        else
        {
            recommendedGO = MakeImage(card.transform, "RecommendedButton", new Color(0.10f, 0.35f, 0.55f, 1f));
            recommendedGO.AddComponent<Button>();
            var lbl = MakeTMP(recommendedGO.transform, "Label", "Recommended", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt              = recommendedGO.GetComponent<RectTransform>();
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.anchorMin        = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 86f);   // above DividerRule2 (which sits at y=80)
            rt.sizeDelta        = new Vector2(280f, 52f);
        }
        recommendedGO.SetActive(false);   // hidden until LevelUpController enables it

        // Skill rows + their +/- buttons are built statically above; LevelUpController
        // binds to them by name at runtime. Container/CanvasGroup/confirm/cancel
        // references are wired in WireController.
        return panel;
    }

    // -----------------------------------------------------------------------
    // Level-up skill rows (static build)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds one skill row: Label | MinusButton | RankText | PlusButton, parented under
    /// the given column container. Mirrors the layout LevelUpController used to build at
    /// runtime, but baked into the scene so the Shift button Animators bind at load and
    /// rest correctly. The row is named "Row_&lt;skill&gt;" so the controller can find it.
    /// </summary>
    static void BuildSkillRowStatic(string skillName, Transform container,
                                    GameObject squadBtnPrefab, Sprite plusSprite, Sprite minusSprite)
    {
        var rowGO = MakeUIGO($"Row_{skillName}", container);
        // Column VLG has childControlHeight=false, so set height explicitly. Width is
        // overridden by the VLG (childControlWidth=true), so x doesn't matter.
        rowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 60f);

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 8f;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;  // respect preferred widths, don't bloat
        hlg.childForceExpandHeight = true;   // children fill row height
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 60f;
        rowLE.flexibleHeight  = 0f;

        // Skill label
        var labelGO  = MakeTMP(rowGO.transform, "Label", skillName, 24, TextSubtle);
        labelGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        var labelLE  = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 155f;
        labelLE.flexibleWidth  = 0f;

        // Minus button (Shift Squad Member Button prefab, minus icon)
        MakeSkillButtonStatic(rowGO.transform, "MinusButton", squadBtnPrefab, minusSprite);

        // Rank text — "N / cap" (placeholder; LevelUpController fills it at runtime)
        var rankGO  = MakeTMP(rowGO.transform, "RankText", "0 / 2", 20, AccentCyan);
        rankGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        var rankLE  = rankGO.AddComponent<LayoutElement>();
        rankLE.preferredWidth = 58f;
        rankLE.flexibleWidth  = 0f;

        // Plus button (Shift Squad Member Button prefab, plus icon)
        MakeSkillButtonStatic(rowGO.transform, "PlusButton", squadBtnPrefab, plusSprite);
    }

    /// <summary>
    /// Instantiates a small Shift Squad Member Button as a skill +/- button, hides its
    /// Profile Picture and Border Glow, and stamps the icon sprite on all three animation
    /// states. The Animator is left enabled and untouched — built into the scene it binds
    /// at load and rests at the visible Normal frame. Falls back to a plain styled button
    /// when the prefab is missing.
    /// </summary>
    static GameObject MakeSkillButtonStatic(Transform parent, string goName,
                                            GameObject squadBtnPrefab, Sprite icon)
    {
        GameObject go;

        if (squadBtnPrefab != null)
        {
            go      = (GameObject)Object.Instantiate(squadBtnPrefab, parent);
            go.name = goName;

            // Border Glow (12-unit outset, baked alpha ~0.39) looks muddy at small sizes.
            var normalT = go.transform.Find("Normal");
            if (normalT != null)
            {
                var bgT = normalT.Find("Border Glow");
                if (bgT != null) bgT.gameObject.SetActive(false);
            }

            // Hide the Profile Picture overlay — we want an icon button, not a portrait.
            var profilePic = go.transform.Find("Profile Picture");
            if (profilePic != null) profilePic.gameObject.SetActive(false);

            // Stamp the icon on Normal / Highlighted / Pressed so it shows in every state.
            if (icon != null)
            {
                foreach (string stateName in new[] { "Normal", "Highlighted", "Pressed" })
                {
                    var stateT = go.transform.Find(stateName);
                    var iconT  = stateT?.Find("Icon");
                    var img    = iconT?.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite         = icon;
                        img.preserveAspect = true;
                        img.color          = Color.white;
                    }
                }
            }
        }
        else
        {
            // Fallback — plain Image + Button with TMP label
            string label = (goName == "PlusButton") ? "+" : "−";
            go = MakeImage(parent, goName, new Color(0.10f, 0.28f, 0.46f, 1f));
            go.AddComponent<Button>().targetGraphic = go.GetComponent<Image>();
            var lbl = MakeTMP(go.transform, "Label", label, 26, TextWhite);
            Stretch(lbl);
        }

        // Squad Member Button is designed at 60×60. Lock that size so the border glow
        // outset stays proportional and the row HLG can't bloat it.
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth  = 60f;
        le.preferredHeight = 60f;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        return go;
    }

    // -----------------------------------------------------------------------
    // Serialised field wiring
    // -----------------------------------------------------------------------

    static void WireController(SystemViewController ctrl,
                                GameObject background,
                                GameObject header, GameObject body,
                                GameObject navBar, GameObject poiDetail,
                                GameObject combatView,
                                GameObject levelUpPanel,
                                GameObject researchView, GameObject researchCompletePopup)
    {
        var so = new SerializedObject(ctrl);

        if (researchCompletePopup != null)
        {
            Set(so, "researchCompleteGroup",     researchCompletePopup.GetComponent<CanvasGroup>());
            Set(so, "researchCompleteNameText",  Find<TMP_Text>(researchCompletePopup, "Panel/NameText"));
            Set(so, "researchCompleteDescText",  Find<TMP_Text>(researchCompletePopup, "Panel/DescText"));
            Set(so, "researchCompleteIconImage", Find<Image>(researchCompletePopup, "Panel/Icon"));
            var okTf = researchCompletePopup.transform.Find("Panel/OkButton");
            Set(so, "researchCompleteOkButton",  okTf?.GetComponentInChildren<Button>(true));
        }

        Set(so, "starfieldBackground",    background.GetComponent<RawImage>());
        Set(so, "systemNameText",         Find<TMP_Text>(header, "SystemNameText"));
        Set(so, "daysText",               Find<TMP_Text>(header, "DaysWidget/DaysText"));
        Set(so, "salvageText",            Find<TMP_Text>(header, "SalvageWidget/SalvageText"));
        Set(so, "salvageWidgetGO",        header.transform.Find("SalvageWidget")?.gameObject);
        Set(so, "daysWidgetGO",           header.transform.Find("DaysWidget")?.gameObject);
        Set(so, "fuelText",               Find<TMP_Text>(header, "FuelWidget/FuelText"));
        Set(so, "fuelWidgetGO",           header.transform.Find("FuelWidget")?.gameObject);
        Set(so, "loyaltyText",            Find<TMP_Text>(header, "LoyaltyWidget/LoyaltyText"));
        Set(so, "loyaltyWidgetGO",        header.transform.Find("LoyaltyWidget")?.gameObject);
        Set(so, "crewText",               Find<TMP_Text>(header, "CrewWidget/CrewText"));
        Set(so, "crewWidgetGO",           header.transform.Find("CrewWidget")?.gameObject);
        Set(so, "iridiumText",            Find<TMP_Text>(header, "IridiumWidget/IridiumText"));
        Set(so, "iridiumWidgetGO",        header.transform.Find("IridiumWidget")?.gameObject);
        Set(so, "combatHeaderStatsGroup", header.transform.Find("CombatHeaderStats")?.GetComponent<CanvasGroup>());
        Set(so, "systemMapArea",       FindRT(body, "SystemMap"));
        Set(so, "starNode",            Find<Image>(body, "SystemMap/StarNode"));

        SetNavButton(so, "systemNavButton",   navBar, "ButtonContainer/SystemButton");
        SetNavButton(so, "galaxyNavButton",   navBar, "ButtonContainer/GalaxyButton");
        SetNavButton(so, "shipNavButton",     navBar, "ButtonContainer/ShipButton");
        SetNavButton(so, "crewNavButton",     navBar, "ButtonContainer/CrewButton");
        SetNavButton(so, "researchNavButton", navBar, "ButtonContainer/ResearchButton");

        // Research view
        if (researchView != null)
        {
            so.FindProperty("researchViewPanel").objectReferenceValue = researchView;
            var rvc = researchView.GetComponent<ResearchViewController>();
            if (rvc != null)
            {
                so.FindProperty("researchViewController").objectReferenceValue = rvc;

                var rvcSo = new SerializedObject(rvc);
                Set(rvcSo, "treeContent",      FindRT(researchView, "TreeScrollRect/Viewport/TreeContent"));
                Set(rvcSo, "connectionLayer",  researchView.transform.Find("TreeScrollRect/Viewport/TreeContent/ConnectionLayer")?.gameObject);
                Set(rvcSo, "nodeLayer",        researchView.transform.Find("TreeScrollRect/Viewport/TreeContent/NodeLayer")?.gameObject);

                var detailRoot = researchView.transform.Find("DetailRoot");
                if (detailRoot != null)
                {
                    Set(rvcSo, "detailPanelGroup",   detailRoot.GetComponent<CanvasGroup>());
                    Set(rvcSo, "detailNameText",     Find<TMP_Text>(detailRoot.gameObject, "DetailPanel/DetailNameText"));
                    Set(rvcSo, "detailCategoryText", Find<TMP_Text>(detailRoot.gameObject, "DetailPanel/DetailCategoryText"));
                    Set(rvcSo, "detailDescText",     Find<TMP_Text>(detailRoot.gameObject, "DetailPanel/DetailDescText"));
                    Set(rvcSo, "detailCostText",     Find<TMP_Text>(detailRoot.gameObject, "DetailPanel/DetailCostText"));
                    Set(rvcSo, "unlockButton",           detailRoot.Find("DetailPanel/UnlockButton")?.GetComponentInChildren<Button>(true));
                    Set(rvcSo, "debugResearchNowButton", detailRoot.Find("DetailPanel/DebugResearchNowButton")?.GetComponentInChildren<Button>(true));
                    Set(rvcSo, "closeDetailButton",      detailRoot.Find("DetailPanel/CloseDetailButton")?.GetComponentInChildren<Button>(true));
                }
                else Debug.LogWarning("[GameSceneSetup] DetailPanel not found under ResearchView.");

                var loadingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Shift - Complete Sci-Fi UI/Prefabs/Loader/Loading.prefab");
                Set(rvcSo, "researchingOverlayPrefab", loadingPrefab);

                rvcSo.ApplyModifiedProperties();
            }
            else Debug.LogWarning("[GameSceneSetup] ResearchViewController not found on ResearchView.");
        }
        else Debug.LogWarning("[GameSceneSetup] ResearchView not found under Body.");

        so.FindProperty("poiDetailPanel").objectReferenceValue = poiDetail;
        Set(so, "poiDetailNameText", Find<TMP_Text>(poiDetail, "Card/POIDetailNameText"));
        Set(so, "poiDetailTypeText", Find<TMP_Text>(poiDetail, "Card/POIDetailTypeText"));
        Set(so, "poiDetailDescText",    Find<TMP_Text>(poiDetail, "Card/POIDetailDescText"));
        Set(so, "poiDetailScannerText", Find<TMP_Text>(poiDetail, "Card/POIDetailScannerText"));
        var closeTf    = poiDetail.transform.Find("Card/POIDetailCloseButton");
        Set(so, "poiDetailCloseButton",    closeTf?.GetComponentInChildren<Button>(true));
        var navigateTf = poiDetail.transform.Find("Card/POIDetailNavigateButton");
        Set(so, "poiDetailNavigateButton", navigateTf?.GetComponentInChildren<Button>(true));
        Set(so, "poiDetailFuelCostText", Find<TMP_Text>(poiDetail, "Card/POIDetailFuelCostLabel/Normal/Text"));
        Set(so, "poiDetailDaysText",     Find<TMP_Text>(poiDetail, "Card/POIDetailDaysLabel/Normal/Text"));
        Set(so, "poiDetailHe3Icon",     Find<Image>(poiDetail, "Card/POIDetailResourcePanel/POIDetailHe3Icon"));
        Set(so, "poiDetailIridiumIcon", Find<Image>(poiDetail, "Card/POIDetailResourcePanel/POIDetailIridiumIcon"));
        Set(so, "poiDetailSalvageIcon", Find<Image>(poiDetail, "Card/POIDetailResourcePanel/POIDetailSalvageIcon"));
        var collectTf = poiDetail.transform.Find("Card/POIDetailCollectButton");
        Set(so, "poiDetailCollectButton", collectTf?.GetComponentInChildren<Button>(true));

        // Audio
        var svcAudioSrc   = GameObject.Find("UI Audio")?.GetComponent<AudioSource>();
        Set(so, "sfxSource", svcAudioSrc);
        var sublightClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sublight_engine.wav");
        if (sublightClip != null)
            Set(so, "sublightEngineClip", sublightClip);
        else
            Debug.LogWarning("[GameSceneSetup] sublight_engine.wav not found at Assets/Audio/SFX/.");

        const string StationSpritePath = "Assets/Art/Sprites/SpaceStation.png";
        var stationSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StationSpritePath);
        if (stationSprite != null) Set(so, "stationSprite", stationSprite);
        else Debug.LogWarning("[GameSceneSetup] SpaceStation.png not found at " + StationSpritePath);

        const string PlanetRingsSpritePath = "Assets/Art/Sprites/planet_rings.png";
        {
            var imp = AssetImporter.GetAtPath(PlanetRingsSpritePath) as TextureImporter;
            if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(PlanetRingsSpritePath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[GameSceneSetup] Re-imported planet_rings.png as Single sprite.");
            }
            var ringsSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlanetRingsSpritePath);
            if (ringsSprite != null) Set(so, "planetRingsSprite", ringsSprite);
            else Debug.LogWarning("[GameSceneSetup] planet_rings.png not found at " + PlanetRingsSpritePath);
        }

        const string DerelictPrefabPath =
            "Assets/2DSpaceshipsFreeTrial/Prefabs/2DSpaceshipsFreeTrialTopView/" +
            "2DScifiFrigateCorsairTopViewMasterPrefab.prefab";
        var derelictPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DerelictPrefabPath);
        if (derelictPrefab != null)
        {
            var dsr = derelictPrefab.GetComponentInChildren<SpriteRenderer>(false);
            if (dsr != null) Set(so, "derelictSprite", dsr.sprite);
            else Debug.LogWarning("[GameSceneSetup] No active SpriteRenderer in FrigateCorsair prefab.");
        }
        else Debug.LogWarning("[GameSceneSetup] FrigateCorsair prefab not found at " + DerelictPrefabPath);

        // ── Player ship sprite — blue_corvette_1 from DGB Spaceships ─────────
        const string PlayerShipSpritePath = "Assets/DGB Spaceships/Spaceship Sprites/blue_corvette_1.png";
        Sprite shipSprite = null;
        {
            var imp = AssetImporter.GetAtPath(PlayerShipSpritePath) as TextureImporter;
            if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(PlayerShipSpritePath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[GameSceneSetup] Re-imported blue_corvette_1.png as Single sprite.");
            }
            shipSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerShipSpritePath);
            if (shipSprite != null) Set(so, "shipSprite", shipSprite);
            else Debug.LogWarning("[GameSceneSetup] blue_corvette_1.png not found — check the DGB Spaceships import.");
        }

        // ── Thruster sprite ────────────────────────────────────────────────────
        const string ThrusterSpritePath = "Assets/DGB Spaceships/Effects/thrusters00.png";
        Sprite thrusterSprite = null;
        {
            var imp = AssetImporter.GetAtPath(ThrusterSpritePath) as TextureImporter;
            if (imp != null && imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePivot      = new Vector2(0.5f, 0.5f);
                AssetDatabase.ImportAsset(ThrusterSpritePath, ImportAssetOptions.ForceUpdate);
                Debug.Log("[GameSceneSetup] Re-imported thrusters00.png as Single sprite.");
            }
            thrusterSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ThrusterSpritePath);
            if (thrusterSprite != null) Set(so, "thrusterSprite", thrusterSprite);
            else Debug.LogWarning("[GameSceneSetup] thrusters00.png not found — thruster effect will be invisible.");
        }

        // Galaxy view
        var galaxyViewGO = body.transform.Find("GalaxyView")?.gameObject;
        if (galaxyViewGO != null)
        {
            Set(so, "galaxyViewPanel",      galaxyViewGO);
            var gvc = galaxyViewGO.GetComponent<GalaxyViewController>();
            Set(so, "galaxyViewController", gvc);
            if (gvc != null)
            {
                var gvcSo = new SerializedObject(gvc);
                var bgTf  = galaxyViewGO.transform.Find("GalaxyMap/GalaxyBackground");
                Set(gvcSo, "galaxyBackground", bgTf?.GetComponent<RawImage>());
                Set(gvcSo, "systemNodesContainer", FindRT(galaxyViewGO, "GalaxyMap/SystemNodesContainer"));
                if (shipSprite    != null) Set(gvcSo, "shipSprite",     shipSprite);
                if (thrusterSprite != null) Set(gvcSo, "thrusterSprite", thrusterSprite);

                // System info panel
                var infoPanel = galaxyViewGO.transform.Find("SystemInfoPanel")?.gameObject;
                if (infoPanel != null)
                {
                    gvcSo.FindProperty("systemInfoPanel").objectReferenceValue = infoPanel;
                    Set(gvcSo, "systemInfoNameText",     Find<TMP_Text>(infoPanel, "Card/SystemInfoNameText"));
                    Set(gvcSo, "systemInfoSubtitleText", Find<TMP_Text>(infoPanel, "Card/SystemInfoSubtitleText"));
                    Set(gvcSo, "systemInfoDistanceText", Find<TMP_Text>(infoPanel, "Card/SystemInfoDistanceText"));
                    Set(gvcSo, "systemInfoPOIText",      Find<TMP_Text>(infoPanel, "Card/SystemInfoPOIText"));

                    var travelTf      = infoPanel.transform.Find("Card/SystemInfoTravelButton");
                    Set(gvcSo, "systemInfoTravelButton", travelTf?.GetComponentInChildren<Button>(true));
                    var infoCloseTf   = infoPanel.transform.Find("Card/SystemInfoCloseButton");
                    Set(gvcSo, "systemInfoCloseButton", infoCloseTf?.GetComponentInChildren<Button>(true));
                    Set(gvcSo, "systemInfoFuelCostText", Find<TMP_Text>(infoPanel, "Card/SystemInfoFuelCostLabel/Normal/Text"));
                    Set(gvcSo, "systemInfoDaysText",     Find<TMP_Text>(infoPanel, "Card/SystemInfoDaysLabel/Normal/Text"));
                }
                else Debug.LogWarning("[GameSceneSetup] SystemInfoPanel not found under GalaxyView.");

                // Audio — wire the shared UI AudioSource and the warp SFX clip
                var uiAudioSrc = GameObject.Find("UI Audio")?.GetComponent<AudioSource>();
                Set(gvcSo, "sfxSource", uiAudioSrc);
                var warpClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/warp_speed.mp3");
                if (warpClip != null)
                    Set(gvcSo, "warpSoundClip", warpClip);
                else
                    Debug.LogWarning("[GameSceneSetup] warp_speed.mp3 not found at Assets/Audio/SFX/.");

                gvcSo.ApplyModifiedProperties();
            }
            else Debug.LogWarning("[GameSceneSetup] GalaxyViewController not found on GalaxyView.");
        }
        else Debug.LogWarning("[GameSceneSetup] GalaxyView not found under Body.");

        // Ship view
        var shipViewGO = body.transform.Find("ShipView")?.gameObject;
        if (shipViewGO != null)
        {
            so.FindProperty("shipViewPanel").objectReferenceValue = shipViewGO;

            // Apply the ship sprite directly to the ShipImage component.
            var shipImgComp = shipViewGO.transform.Find("ShipImage")?.GetComponent<Image>();
            if (shipImgComp != null && shipSprite != null) shipImgComp.sprite = shipSprite;

            // Wire ShipViewController — lives on the ShipView root.
            var svc    = shipViewGO.GetComponent<ShipViewController>();
            var svcObj = svc != null ? new SerializedObject(svc) : null;
            if (svcObj != null)
            {
                so.FindProperty("shipViewController").objectReferenceValue = svc;

                var overlay   = shipViewGO.transform.Find("EquipmentDetailOverlay");
                var card      = overlay?.Find("DetailCard");
                if (overlay != null)
                    svcObj.FindProperty("detailOverlayGroup").objectReferenceValue =
                        overlay.GetComponent<CanvasGroup>();
                if (card != null)
                {
                    svcObj.FindProperty("detailIconImage").objectReferenceValue =
                        Find<Image>(card.gameObject, "DetailIcon");
                    svcObj.FindProperty("detailNameText").objectReferenceValue =
                        Find<TMP_Text>(card.gameObject, "DetailName");
                    svcObj.FindProperty("detailTierText").objectReferenceValue =
                        Find<TMP_Text>(card.gameObject, "DetailTier");
                    svcObj.FindProperty("detailDescText").objectReferenceValue =
                        Find<TMP_Text>(card.gameObject, "DetailDesc");
                    svcObj.FindProperty("detailCostText").objectReferenceValue =
                        Find<TMP_Text>(card.gameObject, "CostValue");

                    var upgradeTf      = card.Find("UpgradeButton");
                    svcObj.FindProperty("upgradeButton").objectReferenceValue =
                        upgradeTf?.GetComponentInChildren<Button>(true);
                    var detailCloseTf  = card.Find("DetailCloseButton");
                    svcObj.FindProperty("closeButton").objectReferenceValue =
                        detailCloseTf?.GetComponentInChildren<Button>(true);
                }
                else Debug.LogWarning("[GameSceneSetup] DetailCard not found under EquipmentDetailOverlay.");

                // ── Upgrade confirmation dialog ──────────────────────────
                var confirmOverlay = overlay?.Find("ConfirmOverlay");
                var confirmCard    = confirmOverlay?.Find("ConfirmCard");
                if (confirmOverlay != null)
                    svcObj.FindProperty("confirmOverlayGroup").objectReferenceValue =
                        confirmOverlay.GetComponent<CanvasGroup>();
                if (confirmCard != null)
                {
                    svcObj.FindProperty("confirmText").objectReferenceValue =
                        Find<TMP_Text>(confirmCard.gameObject, "ConfirmText");
                    var yesTf = confirmCard.Find("ConfirmYesButton");
                    svcObj.FindProperty("confirmYesButton").objectReferenceValue =
                        yesTf?.GetComponentInChildren<Button>(true);
                    var noTf = confirmCard.Find("ConfirmNoButton");
                    svcObj.FindProperty("confirmNoButton").objectReferenceValue =
                        noTf?.GetComponentInChildren<Button>(true);
                }
                else Debug.LogWarning("[GameSceneSetup] ConfirmCard not found under EquipmentDetailOverlay.");

                svcObj.ApplyModifiedProperties();
            }
            else Debug.LogWarning("[GameSceneSetup] ShipViewController not found on ShipView.");
        }
        else Debug.LogWarning("[GameSceneSetup] ShipView not found under Body.");

        // Crew view
        var crewViewGO = body.transform.Find("CrewView")?.gameObject;
        if (crewViewGO != null)
        {
            so.FindProperty("crewViewPanel").objectReferenceValue = crewViewGO;
            var cvcComp = crewViewGO.GetComponent<CrewViewController>();
            so.FindProperty("crewViewController").objectReferenceValue = cvcComp;
            if (cvcComp == null)
            {
                Debug.LogWarning("[GameSceneSetup] CrewViewController component not found on CrewView.");
            }
            else
            {
                // Wire levelUpController — panel is built after CrewView, so wired here
                var lucComp = levelUpPanel?.GetComponent<LevelUpController>();
                if (lucComp != null)
                {
                    var cvcSo2 = new SerializedObject(cvcComp);
                    cvcSo2.FindProperty("levelUpController").objectReferenceValue = lucComp;
                    cvcSo2.ApplyModifiedProperties();
                }
                else
                    Debug.LogWarning("[GameSceneSetup] LevelUpController not found on levelUpPanel — level-up button will be broken.");
            }
        }
        else
            Debug.LogWarning("[GameSceneSetup] CrewView not found under Body.");

        // Combat view — passed directly (it's a sibling of Body, not a child)
        if (combatView != null)
        {
            so.FindProperty("combatViewPanel").objectReferenceValue = combatView;
            so.FindProperty("combatViewCanvasGroup").objectReferenceValue =
                combatView.GetComponent<CanvasGroup>();
            var cvc = combatView.GetComponent<CombatViewController>();
            so.FindProperty("combatViewController").objectReferenceValue = cvc;

            if (cvc != null)
            {
                var cvcSo = new SerializedObject(cvc);

                // Player ship
                var playerImg = Find<Image>(combatView, "PlayerShipImage");
                cvcSo.FindProperty("playerShipImage").objectReferenceValue = playerImg;
                if (shipSprite != null && playerImg != null)
                    playerImg.sprite = shipSprite;

                // Enemy slots — CanvasGroup + RectTransform on slot root, Image on child
                var leftSlot   = combatView.transform.Find("EnemyLeftSlot");
                var centerSlot = combatView.transform.Find("EnemyCenterSlot");
                var rightSlot  = combatView.transform.Find("EnemyRightSlot");

                cvcSo.FindProperty("enemyLeftGroup").objectReferenceValue =
                    leftSlot?.GetComponent<CanvasGroup>();
                cvcSo.FindProperty("enemyLeftSlotRT").objectReferenceValue =
                    leftSlot?.GetComponent<RectTransform>();
                cvcSo.FindProperty("enemyLeftImage").objectReferenceValue =
                    leftSlot?.Find("EnemyLeftImage")?.GetComponent<Image>();
                cvcSo.FindProperty("enemyLeftButton").objectReferenceValue =
                    leftSlot?.Find("EnemyLeftImage")?.GetComponent<Button>();

                cvcSo.FindProperty("enemyCenterGroup").objectReferenceValue =
                    centerSlot?.GetComponent<CanvasGroup>();
                cvcSo.FindProperty("enemyCenterSlotRT").objectReferenceValue =
                    centerSlot?.GetComponent<RectTransform>();
                cvcSo.FindProperty("enemyCenterImage").objectReferenceValue =
                    centerSlot?.Find("EnemyCenterImage")?.GetComponent<Image>();
                cvcSo.FindProperty("enemyCenterButton").objectReferenceValue =
                    centerSlot?.Find("EnemyCenterImage")?.GetComponent<Button>();

                cvcSo.FindProperty("enemyRightGroup").objectReferenceValue =
                    rightSlot?.GetComponent<CanvasGroup>();
                cvcSo.FindProperty("enemyRightSlotRT").objectReferenceValue =
                    rightSlot?.GetComponent<RectTransform>();
                cvcSo.FindProperty("enemyRightImage").objectReferenceValue =
                    rightSlot?.Find("EnemyRightImage")?.GetComponent<Image>();
                cvcSo.FindProperty("enemyRightButton").objectReferenceValue =
                    rightSlot?.Find("EnemyRightImage")?.GetComponent<Button>();

                cvcSo.FindProperty("combatActionBar").objectReferenceValue =
                    combatView.transform.Find("CombatActionBar")?.gameObject;

                // Fire buttons — inside their containers (Shift prefab is the direct child)
                var torpContainerTf = combatView.transform.Find("CombatActionBar/FireTorpedesContainer");
                var cannonContainerTf = combatView.transform.Find("CombatActionBar/FireParticleCannonContainer");
                cvcSo.FindProperty("fireTorpedesButton").objectReferenceValue =
                    torpContainerTf?.GetComponentInChildren<Button>(true);
                cvcSo.FindProperty("fireParticleCannonButton").objectReferenceValue =
                    cannonContainerTf?.GetComponentInChildren<Button>(true);

                var thrustContainerTf = combatView.transform.Find("CombatActionBar/EmergencyThrustContainer");
                cvcSo.FindProperty("emergencyThrustContainer").objectReferenceValue =
                    thrustContainerTf?.gameObject;
                cvcSo.FindProperty("emergencyThrustButton").objectReferenceValue =
                    thrustContainerTf?.GetComponentInChildren<Button>(true);
                cvcSo.FindProperty("emergencyThrustStatusText").objectReferenceValue =
                    Find<TMP_Text>(combatView, "CombatActionBar/EmergencyThrustContainer/EmergencyThrustStatusText");

                // Torpedo count — now in the header combat stats bar
                cvcSo.FindProperty("torpedoCountText").objectReferenceValue =
                    Find<TMP_Text>(header, "CombatHeaderStats/TorpedoIconContainer/TorpedoCountText");

                // Pilot maneuver dropdown
                var maneuverContainerTf = combatView.transform.Find("CombatActionBar/PilotManeuverContainer");
                cvcSo.FindProperty("pilotManeuverDropdown").objectReferenceValue =
                    maneuverContainerTf?.GetComponentInChildren<TMP_Dropdown>(true);

                // Combat log text (compact) + tap button
                cvcSo.FindProperty("combatLogText").objectReferenceValue =
                    Find<TMP_Text>(combatView, "CombatLogPanel/CombatLogText");
                cvcSo.FindProperty("combatLogButton").objectReferenceValue =
                    combatView.transform.Find("CombatLogPanel")?.GetComponent<Button>();

                // Expanded log panel
                var expandedLogTf = combatView.transform.Find("ExpandedLogPanel");
                cvcSo.FindProperty("expandedLogCanvasGroup").objectReferenceValue =
                    expandedLogTf?.GetComponent<CanvasGroup>();
                cvcSo.FindProperty("expandedLogScrollRect").objectReferenceValue =
                    Find<ScrollRect>(combatView, "ExpandedLogPanel/ExpandedLogScrollRect");
                cvcSo.FindProperty("expandedLogText").objectReferenceValue =
                    Find<TMP_Text>(combatView, "ExpandedLogPanel/ExpandedLogScrollRect/Viewport/ExpandedLogText");
                cvcSo.FindProperty("expandedLogCloseButton").objectReferenceValue =
                    Find<Button>(combatView, "ExpandedLogPanel/ExpandedLogHeader/ExpandedLogCloseButton");

                // Target info texts
                cvcSo.FindProperty("targetInfoTitle").objectReferenceValue =
                    Find<TMP_Text>(combatView, "CombatActionBar/TargetInfoPanel/TargetInfoTitle");
                cvcSo.FindProperty("targetShieldText").objectReferenceValue =
                    Find<TMP_Text>(combatView, "CombatActionBar/TargetInfoPanel/TargetStatsRow/TargetShieldText");
                cvcSo.FindProperty("targetHullText").objectReferenceValue =
                    Find<TMP_Text>(combatView, "CombatActionBar/TargetInfoPanel/TargetStatsRow/TargetHullText");

                // Player stats texts (in the header combat overlay)
                cvcSo.FindProperty("playerShieldText").objectReferenceValue =
                    Find<TMP_Text>(header, "CombatHeaderStats/PlayerShieldText");
                cvcSo.FindProperty("playerHullText").objectReferenceValue =
                    Find<TMP_Text>(header, "CombatHeaderStats/PlayerHullText");

                // Crosshair — wire textures from TooManyCrosshairs then wire the component
                var outerTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Crosshairs/xArrowheadInwards128.png");

                var crosshairRootTf = combatView.transform.Find("CrosshairRoot");
                if (crosshairRootTf != null)
                {
                    var crosshairComp = crosshairRootTf.GetComponent<CombatCrosshair>();
                    if (crosshairComp != null)
                    {
                        var chSo = new SerializedObject(crosshairComp);

                        var outerRI = crosshairRootTf.Find("CrosshairOuter")?.GetComponent<RawImage>();

                        if (outerTex != null && outerRI != null)  outerRI.texture = outerTex;
                        else if (outerTex == null) Debug.LogWarning("[GameSceneSetup] xArrowheadInwards128.png not found at Assets/Art/Crosshairs/ — crosshair will be blank.");

                        chSo.FindProperty("outerImage").objectReferenceValue = outerRI;
                        chSo.ApplyModifiedProperties();

                        cvcSo.FindProperty("combatCrosshair").objectReferenceValue = crosshairComp;
                        Debug.Log("[GameSceneSetup] CombatCrosshair wired.");
                    }
                    else Debug.LogWarning("[GameSceneSetup] CombatCrosshair component not found on CrosshairRoot.");
                }
                else Debug.LogWarning("[GameSceneSetup] CrosshairRoot not found under CombatView.");

                // Load all DGB ship sprites and populate the library array.
                // PNGs default to Texture2D on first import — reimport as Single
                // sprite so LoadAssetAtPath<Sprite> returns a valid asset.
                const string DgbSpritesFolder = "Assets/DGB Spaceships/Spaceship Sprites";
                var dgbGuids   = AssetDatabase.FindAssets("t:Texture2D", new[] { DgbSpritesFolder });
                var dgbSprites = new System.Collections.Generic.List<Sprite>();
                foreach (var guid in dgbGuids)
                {
                    var path     = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    // Must be Sprite type AND Single mode — Multiple mode appends "_0"
                    // to the sprite name, breaking our "{color}_{class}_{variant}" lookup.
                    if (importer != null &&
                        (importer.textureType      != TextureImporterType.Sprite ||
                         importer.spriteImportMode != SpriteImportMode.Single))
                    {
                        importer.textureType      = TextureImporterType.Sprite;
                        importer.spriteImportMode = SpriteImportMode.Single;
                        importer.spritePivot      = new Vector2(0.5f, 0.5f);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        Debug.Log($"[GameSceneSetup] Reimported as Single Sprite: {System.IO.Path.GetFileName(path)}");
                    }
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null) dgbSprites.Add(sprite);
                    else Debug.LogWarning($"[GameSceneSetup] Could not load sprite: {path}");
                }
                var spritesProp = cvcSo.FindProperty("dgbShipSprites");
                spritesProp.arraySize = dgbSprites.Count;
                for (int i = 0; i < dgbSprites.Count; i++)
                    spritesProp.GetArrayElementAtIndex(i).objectReferenceValue = dgbSprites[i];

                Debug.Log($"[GameSceneSetup] Loaded {dgbSprites.Count} DGB ship sprites.");

                // ── Torpedo projectile sprite ─────────────────────────────
                // missile_1.png must be imported as Single sprite for the
                // "{name}" lookup to work (no _0 suffix appended).
                const string EffectsFolder = "Assets/DGB Spaceships/Effects";
                const string MissilePath   = DgbSpritesFolder + "/missile_1.png";
                {
                    var imp = AssetImporter.GetAtPath(MissilePath) as TextureImporter;
                    if (imp != null &&
                        (imp.textureType      != TextureImporterType.Sprite ||
                         imp.spriteImportMode != SpriteImportMode.Single))
                    {
                        imp.textureType      = TextureImporterType.Sprite;
                        imp.spriteImportMode = SpriteImportMode.Single;
                        imp.spritePivot      = new Vector2(0.5f, 0.5f);
                        AssetDatabase.ImportAsset(MissilePath, ImportAssetOptions.ForceUpdate);
                        Debug.Log($"[GameSceneSetup] Reimported as Single Sprite: missile_1.png");
                    }
                }
                var missileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MissilePath);
                if (missileSprite == null)
                    Debug.LogWarning($"[GameSceneSetup] Missile sprite not found: {MissilePath}");
                cvcSo.FindProperty("missileSprite").objectReferenceValue = missileSprite;

                // Wire missile sprite to the header torpedo icon image
                var torpIconImg = header.transform.Find(
                    "CombatHeaderStats/TorpedoIconContainer/TorpedoIcon")?.GetComponent<Image>();
                if (torpIconImg != null && missileSprite != null)
                    torpIconImg.sprite = missileSprite;
                else if (torpIconImg == null)
                    Debug.LogWarning("[GameSceneSetup] TorpedoIcon Image not found in CombatHeaderStats.");

                // ── Particle cannon textures ──────────────────────────────────
                // laser_noise00.png → particle cannon beam line (RawImage — keep as Texture2D, not Sprite)
                // glow_round00.png  → impact glow + muzzle flash (Image — import as Single Sprite)
                const string LaserTexPath = EffectsFolder + "/laser_noise00.png";
                const string GlowPath     = EffectsFolder + "/glow_round00.png";

                // Ensure laser is a plain Texture2D (not Sprite).
                {
                    var imp = AssetImporter.GetAtPath(LaserTexPath) as TextureImporter;
                    if (imp != null && imp.textureType != TextureImporterType.Default)
                    {
                        imp.textureType = TextureImporterType.Default;
                        AssetDatabase.ImportAsset(LaserTexPath, ImportAssetOptions.ForceUpdate);
                        Debug.Log("[GameSceneSetup] Reimported laser_noise00.png as Default texture.");
                    }
                }
                // Ensure glow is a Single Sprite.
                {
                    var imp = AssetImporter.GetAtPath(GlowPath) as TextureImporter;
                    if (imp != null &&
                        (imp.textureType      != TextureImporterType.Sprite ||
                         imp.spriteImportMode != SpriteImportMode.Single))
                    {
                        imp.textureType      = TextureImporterType.Sprite;
                        imp.spriteImportMode = SpriteImportMode.Single;
                        imp.spritePivot      = new Vector2(0.5f, 0.5f);
                        AssetDatabase.ImportAsset(GlowPath, ImportAssetOptions.ForceUpdate);
                        Debug.Log("[GameSceneSetup] Reimported glow_round00.png as Single Sprite.");
                    }
                }

                var cannonTex   = AssetDatabase.LoadAssetAtPath<Texture2D>(LaserTexPath);
                var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);

                if (cannonTex   == null) Debug.LogWarning($"[GameSceneSetup] Beam/glow texture not found: {LaserTexPath}");
                if (glowSprite == null) Debug.LogWarning($"[GameSceneSetup] Glow sprite not found: {GlowPath}");

                cvcSo.FindProperty("beamTexture").objectReferenceValue    = cannonTex;
                cvcSo.FindProperty("beamGlowSprite").objectReferenceValue = glowSprite;

                // ── Combat audio ──────────────────────────────────────────
                var combatAudioSrc = GameObject.Find("UI Audio")?.GetComponent<AudioSource>();
                cvcSo.FindProperty("sfxSource").objectReferenceValue = combatAudioSrc;

                // Short SFX must use DecompressOnLoad — Compressed In Memory adds
                // a decode delay that makes the sound play mid-flight instead of
                // on button press.
                EnsureAudioDecompressOnLoad("Assets/Audio/SFX/beam_weapon.mp3");
                EnsureAudioDecompressOnLoad("Assets/Audio/SFX/torpedo.mp3");
                EnsureAudioDecompressOnLoad("Assets/Audio/SFX/explosion.mp3");

                var cannonClip      = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/beam_weapon.mp3");
                var torpClip      = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/torpedo.mp3");
                var explosionClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/explosion.mp3");
                if (cannonClip      != null) cvcSo.FindProperty("particleCannonClip").objectReferenceValue = cannonClip;
                else Debug.LogWarning("[GameSceneSetup] beam_weapon.mp3 not found at Assets/Audio/SFX/.");
                if (torpClip      != null) cvcSo.FindProperty("torpedoClip").objectReferenceValue = torpClip;
                else Debug.LogWarning("[GameSceneSetup] torpedo.mp3 not found at Assets/Audio/SFX/.");
                if (explosionClip != null) cvcSo.FindProperty("explosionClip").objectReferenceValue = explosionClip;
                else Debug.LogWarning("[GameSceneSetup] explosion.mp3 not found at Assets/Audio/SFX/.");

                // Projectile container
                var projLayerTf = combatView.transform.Find("ProjectileLayer");
                cvcSo.FindProperty("projectileContainer").objectReferenceValue =
                    projLayerTf?.GetComponent<RectTransform>();
                if (projLayerTf == null)
                    Debug.LogWarning("[GameSceneSetup] ProjectileLayer not found under CombatView.");

                cvcSo.ApplyModifiedProperties();
            }
            else Debug.LogWarning("[GameSceneSetup] CombatViewController not found on CombatView.");
        }
        else Debug.LogWarning("[GameSceneSetup] CombatView is null — check Setup().");

        // NavBar CanvasGroup — hidden during combat so it doesn't show through CombatView
        var navBarCG = navBar.GetComponent<CanvasGroup>();
        if (navBarCG != null)
            so.FindProperty("navBarCanvasGroup").objectReferenceValue = navBarCG;
        else
            Debug.LogWarning("[GameSceneSetup] CanvasGroup not found on NavBar.");

        // Debug combat buttons — now in NavBar/DebugButtonContainer (moved from header)
        var dbgContainerTf = navBar.transform.Find("DebugButtonContainer");
        var combatDbgTf = dbgContainerTf?.Find("CombatDebugButton");
        if (combatDbgTf != null)
            so.FindProperty("combatDebugButton").objectReferenceValue =
                combatDbgTf.GetComponentInChildren<Button>(true);
        else
            Debug.LogWarning("[GameSceneSetup] CombatDebugButton not found in NavBar/DebugButtonContainer.");

        var randomDbgTf = dbgContainerTf?.Find("RandomCombatButton");
        if (randomDbgTf != null)
            so.FindProperty("randomCombatButton").objectReferenceValue =
                randomDbgTf.GetComponentInChildren<Button>(true);
        else
            Debug.LogWarning("[GameSceneSetup] RandomCombatButton not found in NavBar/DebugButtonContainer.");

        // ── Level Up panel ────────────────────────────────────────────────
        if (levelUpPanel != null)
        {
            var luc = levelUpPanel.GetComponent<LevelUpController>();

            if (luc != null)
            {
                var lucSo = new SerializedObject(luc);
                lucSo.FindProperty("panelCanvasGroup").objectReferenceValue =
                    levelUpPanel.GetComponent<CanvasGroup>();
                lucSo.FindProperty("crewNameText").objectReferenceValue =
                    Find<TMP_Text>(levelUpPanel, "Card/CrewNameText");
                lucSo.FindProperty("pointsText").objectReferenceValue =
                    Find<TMP_Text>(levelUpPanel, "Card/PointsText");
                lucSo.FindProperty("skillsContainer").objectReferenceValue =
                    levelUpPanel.transform.Find("Card/ColumnsArea/SkillsContainer");
                lucSo.FindProperty("skillsContainerRight").objectReferenceValue =
                    levelUpPanel.transform.Find("Card/ColumnsArea/SkillsContainerRight");
                var confirmTf = levelUpPanel.transform.Find("Card/ConfirmButton");
                lucSo.FindProperty("confirmButton").objectReferenceValue =
                    confirmTf?.GetComponentInChildren<Button>(true);
                var cancelTf = levelUpPanel.transform.Find("Card/CancelButton");
                lucSo.FindProperty("cancelButton").objectReferenceValue =
                    cancelTf?.GetComponentInChildren<Button>(true);
                var recommendedTf = levelUpPanel.transform.Find("Card/RecommendedButton");
                lucSo.FindProperty("recommendedButton").objectReferenceValue =
                    recommendedTf?.GetComponentInChildren<Button>(true);
                lucSo.ApplyModifiedProperties();
            }
            else Debug.LogWarning("[GameSceneSetup] LevelUpController not found on LevelUpPanel.");

            // Wire LevelUpController onto CrewViewController (found inside body/CrewView)
            var crewViewGO2 = body.transform.Find("CrewView")?.gameObject;
            if (crewViewGO2 != null)
            {
                var cvc = crewViewGO2.GetComponent<CrewViewController>();
                if (cvc != null)
                {
                    var cvcSo2 = new SerializedObject(cvc);
                    cvcSo2.FindProperty("levelUpController").objectReferenceValue =
                        levelUpPanel.GetComponent<LevelUpController>();
                    cvcSo2.ApplyModifiedProperties();
                }
                else Debug.LogWarning("[GameSceneSetup] CrewViewController not found on CrewView.");
            }
            else Debug.LogWarning("[GameSceneSetup] CrewView not found under Body (level-up wiring).");
        }
        else Debug.LogWarning("[GameSceneSetup] LevelUpPanel is null.");

        so.ApplyModifiedProperties();
    }

    static void SetNavButton(SerializedObject so, string prop, GameObject navBar, string path)
    {
        var t = navBar?.transform.Find(path);
        if (t == null) { Debug.LogWarning($"[GameSceneSetup] Nav button not found: '{path}'"); return; }
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = t.GetComponentInChildren<Button>(true);
    }

    // -----------------------------------------------------------------------
    // UI factory helpers
    // -----------------------------------------------------------------------

    static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject MakeUIGO(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject MakeImage(Transform parent, string name, Color color)
    {
        var go = MakeUIGO(name, parent);
        go.AddComponent<Image>().color = color;
        return go;
    }

    static GameObject MakeRawImage(Transform parent, string name, Color color)
    {
        var go = MakeUIGO(name, parent);
        go.AddComponent<RawImage>().color = color;
        return go;
    }

    static GameObject MakeTMP(Transform parent, string name, string text, int size, Color color)
    {
        var go  = MakeUIGO(name, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Positions a child flush against a top-anchored strip inside its parent.
    /// Uses offsetMin/offsetMax directly so the intent is unambiguous regardless
    /// of pivot. All values are in canvas units, measured inward from each edge.
    ///   leftInset  — gap from the parent's left edge
    ///   topInset   — gap from the parent's top edge
    ///   rightInset — gap from the parent's right edge
    ///   height     — element height
    /// </summary>
    static void TopStretch(GameObject go, float leftInset, float topInset, float rightInset, float height)
    {
        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(leftInset,  -(topInset + height));
        rt.offsetMax = new Vector2(-rightInset, -topInset);
    }

    static void PlaceRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
    }

    /// <summary>
    /// Ensures an audio clip is imported with DecompressOnLoad so it plays
    /// with zero decode latency.  Required for short SFX — Compressed In Memory
    /// decompresses on-the-fly and adds an audible delay on PlayOneShot.
    /// </summary>
    static void EnsureAudioDecompressOnLoad(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp == null) return;
        var settings = imp.defaultSampleSettings;
        if (settings.loadType == AudioClipLoadType.DecompressOnLoad) return;
        settings.loadType          = AudioClipLoadType.DecompressOnLoad;
        imp.defaultSampleSettings  = settings;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[GameSceneSetup] Set DecompressOnLoad: {System.IO.Path.GetFileName(path)}");
    }

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[GameSceneSetup] Property not found: {prop}");
    }

    static T Find<T>(GameObject root, string path) where T : Component
    {
        var t = root.transform.Find(path);
        if (t == null) { Debug.LogWarning($"[GameSceneSetup] Path not found: '{path}' on {root.name}"); return null; }
        return t.GetComponent<T>();
    }

    static RectTransform FindRT(GameObject root, string path)
    {
        var t = root.transform.Find(path);
        if (t == null) { Debug.LogWarning($"[GameSceneSetup] Path not found: '{path}' on {root.name}"); return null; }
        return t.GetComponent<RectTransform>();
    }

    /// <summary>
    /// Sets the RectTransform size of an Image to preserve its sprite's aspect ratio
    /// at the given target height. Call after assigning img.sprite.
    /// No-op if the sprite is null or has zero height.
    /// </summary>
    static void SizeIconToHeight(Image img, float targetHeight)
    {
        if (img.sprite == null) return;
        float h = img.sprite.rect.height;
        if (h <= 0f) return;
        float aspect = img.sprite.rect.width / h;
        var rt = img.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(targetHeight * aspect, targetHeight);
    }

    static Vector2 anchor(float x, float y) => new Vector2(x, y);
    static Vector2 v2(float x, float y)     => new Vector2(x, y);
}
