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
///           POIDetailCloseButton  (Shift MainButton — "CLOSE")
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

    // -----------------------------------------------------------------------
    // Entry point
    // -----------------------------------------------------------------------

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
        var header    = BuildHeader(svcGO.transform);
        var body      = BuildBody(svcGO.transform);
        var navBar    = BuildNavBar(svcGO.transform);
        var poiDetail = BuildPOIDetailPanel(svcGO.transform);

        // ── Wire UIElementSound audio source on all Shift buttons ─────────
        var uiAudio = uiAudioGO.GetComponent<AudioSource>();
        foreach (var ues in canvasGO.GetComponentsInChildren<Michsky.UI.Shift.UIElementSound>(true))
        {
            var so = new SerializedObject(ues);
            so.FindProperty("audioObject").objectReferenceValue = uiAudio;
            so.ApplyModifiedProperties();
        }

        // ── Wire serialised fields ────────────────────────────────────────
        WireController(controller, bg, header, body, navBar, poiDetail);

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
        nameText.GetComponent<TextMeshProUGUI>().enableWordWrapping = false;
        nameText.GetComponent<TextMeshProUGUI>().overflowMode = TextOverflowModes.Ellipsis;

        // Subtitle — star type + danger level
        var subtitleText = MakeTMP(card.transform, "SystemInfoSubtitleText", "Yellow Dwarf  ·  Danger: Safe", 26, TextSubtle);
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
            if (mb != null) mb.buttonText = "SET COURSE";
        }
        else
        {
            travelGO = MakeImage(card.transform, "SystemInfoTravelButton", BtnNormal);
            travelGO.AddComponent<Button>();
            var lbl = MakeTMP(travelGO.transform, "Label", "SET COURSE", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        // Pivot (1,0) = bottom-right corner of each button, so anchoredPosition is the
        // inset from the card's bottom-right corner — no centre-offset math required.
        {
            var rt       = travelGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(200f, 52f);
            rt.anchoredPosition = new Vector2(-204f, 16f);   // left of close button
        }

        GameObject closeGO;
        if (btnPrefab != null)
        {
            closeGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            closeGO.name = "SystemInfoCloseButton";
            var mb = closeGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "CLOSE";
        }
        else
        {
            closeGO = MakeImage(card.transform, "SystemInfoCloseButton", BtnNormal);
            closeGO.AddComponent<Button>();
            var lbl = MakeTMP(closeGO.transform, "Label", "CLOSE", 22, TextWhite);
            Stretch(lbl);
            lbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        {
            var rt       = closeGO.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(180f, 52f);
            rt.anchoredPosition = new Vector2(-16f, 16f);    // 16 px from right/bottom edges
        }

        return panel;
    }

    // ── Ship view — full body area, starts hidden ────────────────────────────

    static GameObject BuildShipView(Transform parent)
    {
        var shipView = MakeUIGO("ShipView", parent);
        PlaceRect(shipView, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(0f, 0f));
        shipView.SetActive(false);

        // ── Layer 1: solid dark blue background ───────────────────────────
        var bgGO = MakeImage(shipView.transform, "ShipViewBackground", new Color(0.03f, 0.05f, 0.12f, 1f));
        Stretch(bgGO);

        // ── Layer 2: component columns — left (power/defense) and right (offense/utility)
        // Each column is 150 canvas-units wide, full height of the view.
        BuildComponentColumn(shipView.transform, "LeftComponentColumn", isLeft: true,
            "Reactor", "FTL Drive", "Engines", "Shields", "Armor");
        BuildComponentColumn(shipView.transform, "RightComponentColumn", isLeft: false,
            "Beam Weapons", "Torpedoes", "Scanner", "Cargo Hold", "Crew Quarters");

        // ── Layer 3: ship sprite — inset to leave room for the 150 px columns.
        // At 1920 reference width, 150 px ≈ 7.8 %, so anchor at 0.09 / 0.91.
        // preserveAspect constrains to the shorter axis so the sprite stays square.
        var shipImg = MakeImage(shipView.transform, "ShipImage", Color.white);
        PlaceRect(shipImg, anchor(0.09f, 0.03f), anchor(0.91f, 0.97f), v2(0f, 0f), v2(0f, 0f));
        shipImg.GetComponent<RectTransform>().localEulerAngles = new Vector3(0f, 0f, 180f);
        var img = shipImg.GetComponent<Image>();
        img.preserveAspect = true;
        img.type           = Image.Type.Simple;

        return shipView;
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
    //         ComponentIcon Image — icon sprite, centered, aspect-preserved
    //
    // AspectRatioFitter on ComponentIcon keeps the art undistorted regardless of
    // the slot's height (which varies because the column VLG force-expands height).

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
        var borderGO = MakeImage(slotGO.transform, "TierBorder",
                                 Constants.Ship.EmptySlotBorderColor);
        var borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = Vector2.zero;
        borderRT.offsetMax = Vector2.zero;

        // SlotInner — dark bg inset 3 px; the 3 px gap around it IS the border.
        var innerGO = MakeImage(borderGO.transform, "SlotInner",
                                new Color(0.06f, 0.10f, 0.18f, 1f));
        var innerRT = innerGO.GetComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(3f, 3f);
        innerRT.offsetMax = new Vector2(-3f, -3f);

        // ComponentIcon — centered inside SlotInner; AspectRatioFitter prevents
        // distortion when the slot is taller than it is wide.
        //
        // IMPORTANT: ARF only works correctly when anchorMin == anchorMax (a point
        // anchor, not a stretch anchor).  With stretch anchors the effective rect
        // size = parent size + sizeDelta, so ARF's sizeDelta writes are overridden
        // by the layout system and the image distorts.  Use a centred point anchor
        // and let ARF set sizeDelta to whatever fits the parent.
        var iconGO  = MakeImage(innerGO.transform, "ComponentIcon", Color.white);
        var iconRT  = iconGO.GetComponent<RectTransform>();
        iconRT.anchorMin        = new Vector2(0.5f, 0.5f);
        iconRT.anchorMax        = new Vector2(0.5f, 0.5f);
        iconRT.pivot            = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = Vector2.zero;
        iconRT.sizeDelta        = Vector2.zero;
        var iconImg = iconGO.GetComponent<Image>();
        var iconArf = iconGO.AddComponent<AspectRatioFitter>();
        iconArf.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
        iconArf.aspectRatio = 1f;   // overwritten below when actual sprite is loaded

        // Load the icon sprite from the EquipmentIcons art folder.
        Sprite iconSprite = null;
        if (Constants.Ship.EquipmentSlots.IconNames.TryGetValue(componentName, out var iconFile))
        {
            var iconPath = $"Assets/Art/UI/EquipmentIcons/{iconFile}.png";
            iconSprite   = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (iconSprite == null)
                Debug.LogWarning($"[GameSceneSetup] Icon not found at {iconPath}");
        }

        iconImg.sprite = iconSprite;
        iconImg.color  = Color.white;
        if (iconSprite != null)
            iconArf.aspectRatio = iconSprite.rect.width / iconSprite.rect.height;

        // Wire EquipmentSlotUI references.
        slotUISo.FindProperty("iconImage").objectReferenceValue   = iconImg;
        slotUISo.FindProperty("borderImage").objectReferenceValue = borderGO.GetComponent<Image>();
        slotUISo.FindProperty("defaultIcon").objectReferenceValue = iconSprite;
        slotUISo.ApplyModifiedProperties();
    }

    // ── Crew view — full body area, starts hidden ────────────────────────────

    static GameObject BuildCrewView(Transform parent)
    {
        var crewView = MakeUIGO("CrewView", parent);
        PlaceRect(crewView, anchor(0f, 0f), anchor(1f, 1f), v2(0f, 0f), v2(0f, 0f));
        crewView.AddComponent<CrewViewController>();
        crewView.SetActive(false);

        // ── Background ───────────────────────────────────────────────────
        var bg = MakeImage(crewView.transform, "CrewViewBackground", new Color(0.03f, 0.05f, 0.12f, 1f));
        Stretch(bg);

        // ── Left panel — crew list (38 % width, full height, with padding) ─
        //   offsetMin.x=0, offsetMax.x = -(1920*0.62)  →  anchor (0,0)-(0.38,1)
        var listPanel = MakeImage(crewView.transform, "CrewListPanel", new Color(0.04f, 0.07f, 0.13f, 1f));
        PlaceRect(listPanel, anchor(0f, 0f), anchor(0.38f, 1f), v2(0f, 0f), v2(0f, 0f));

        // The panel is pinned to the screen's left edge, so in landscape the notch
        // overlaps it. SHRINK it away from the notch rather than shifting it: only
        // the left edge moves inward while the right edge holds at the divider, so
        // the list/buttons narrow to stop at the start of the detail panel instead
        // of sliding their right edge into it. Body's inset only covers the bottom
        // edge, so the left edge must be handled here.
        var listSafeInset   = listPanel.AddComponent<SafeAreaInset>();
        var listSafeInsetSo = new SerializedObject(listSafeInset);
        listSafeInsetSo.FindProperty("_left").boolValue = true;
        listSafeInsetSo.FindProperty("_shrinkTowardSafeArea").boolValue = true;
        listSafeInsetSo.ApplyModifiedProperties();

        // Scroll View inside the list panel
        var scrollGO   = MakeUIGO("Scroll View", listPanel.transform);
        Stretch(scrollGO);

        var sr = scrollGO.AddComponent<ScrollRect>();
        sr.horizontal         = false;
        sr.vertical           = true;
        sr.scrollSensitivity  = 30f;
        sr.movementType       = ScrollRect.MovementType.Clamped;

        // Viewport (RectMask2D clips content)
        var viewport = MakeUIGO("Viewport", scrollGO.transform);
        Stretch(viewport);
        viewport.AddComponent<RectMask2D>();
        sr.viewport = viewport.GetComponent<RectTransform>();

        // Content — VerticalLayoutGroup, grows to fit crew rows
        var content = MakeUIGO("Content", viewport.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin        = new Vector2(0f, 1f);
        contentRT.anchorMax        = new Vector2(1f, 1f);
        contentRT.pivot            = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta        = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(8, 8, 8, 8);
        vlg.spacing                = 6f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        sr.content = contentRT;

        // ── Vertical divider between list and detail ──────────────────────
        var divider = MakeImage(crewView.transform, "Divider", new Color(0.20f, 0.35f, 0.50f, 0.55f));
        PlaceRect(divider, anchor(0.38f, 0f), anchor(0.38f, 1f), v2(0f, 0f), v2(2f, 0f));

        // ── Right panel — crew detail ─────────────────────────────────────
        var detailPanel = MakeImage(crewView.transform, "CrewDetailPanel", new Color(0.04f, 0.07f, 0.13f, 0.60f));
        PlaceRect(detailPanel, anchor(0.38f, 0f), anchor(1f, 1f), v2(4f, 0f), v2(0f, 0f));

        // ── Detail panel layout ───────────────────────────────────────────────
        //
        //   Portrait (24,24) 160×160        │ Name (bold, 42pt)    │ Level (32pt, right)
        //                                   │ Role (italic, 30pt)  │
        //                                   │ EXPERIENCE (22pt)    │
        //                                   │ [== XP bar ========] │
        //   ─────────────────────────────────────────────────────────────────
        //   SKILLS header
        //   [skills container fills remaining height]

        // Portrait — top-left, 160×160. Pivot (0,1) so anchoredPosition = top-left corner.
        var portrait = MakeImage(detailPanel.transform, "PortraitImage", AccentCyan);
        PlaceRect(portrait, anchor(0f, 1f), anchor(0f, 1f), v2(24f, -24f), v2(160f, 160f));
        portrait.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        portrait.GetComponent<Image>().preserveAspect = true;

        // Name — top-right area, leaving 200 px on the far right for the level badge.
        // TopStretch(go, leftInset, topInset, rightInset, height)
        var nameText = MakeTMP(detailPanel.transform, "CrewNameText", "Crew Member", 42, TextWhite);
        TopStretch(nameText, 204f, 24f, 210f, 52f);
        nameText.GetComponent<TextMeshProUGUI>().alignment        = TextAlignmentOptions.Left;
        nameText.GetComponent<TextMeshProUGUI>().fontStyle        = FontStyles.Bold;
        nameText.GetComponent<TextMeshProUGUI>().enableWordWrapping = false;
        nameText.GetComponent<TextMeshProUGUI>().overflowMode     = TextOverflowModes.Ellipsis;

        // Level badge — top-right corner, same row as the name.
        var levelText = MakeTMP(detailPanel.transform, "LevelText", "Level 1", 28, AccentCyan);
        {
            var rt = levelText.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot                   = new Vector2(1f, 1f);
            rt.anchoredPosition        = new Vector2(-24f, -24f);
            rt.sizeDelta               = new Vector2(186f, 52f);
        }
        levelText.GetComponent<TextMeshProUGUI>().alignment        = TextAlignmentOptions.Right;
        levelText.GetComponent<TextMeshProUGUI>().fontStyle        = FontStyles.Bold;
        levelText.GetComponent<TextMeshProUGUI>().enableWordWrapping = false;

        // Role — below name, same horizontal span as name.
        var roleText = MakeTMP(detailPanel.transform, "CrewRoleText", "Role", 30, AccentCyan);
        TopStretch(roleText, 204f, 82f, 24f, 36f);
        roleText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        roleText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        // XP label — below role, right of portrait.
        var xpLabel = MakeTMP(detailPanel.transform, "XPLabel", "EXPERIENCE", 20, TextSubtle);
        TopStretch(xpLabel, 204f, 124f, 24f, 24f);
        xpLabel.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        // XP bar — below XP label, fills to portrait bottom.
        var xpBarGO = MakeUIGO("XPBar", detailPanel.transform);
        TopStretch(xpBarGO, 204f, 152f, 24f, 28f);
        var slider = xpBarGO.AddComponent<Slider>();
        slider.interactable = false;
        slider.minValue     = 0f;
        slider.maxValue     = 100f;
        slider.value        = 0f;
        slider.direction    = Slider.Direction.LeftToRight;

        var xpBg = MakeImage(xpBarGO.transform, "Background", new Color(0.12f, 0.20f, 0.32f, 1f));
        Stretch(xpBg);
        slider.targetGraphic = xpBg.GetComponent<Image>();

        var fillArea = MakeUIGO("Fill Area", xpBarGO.transform);
        Stretch(fillArea);
        fillArea.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        fillArea.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        var fill = MakeImage(fillArea.transform, "Fill", AccentCyan);
        Stretch(fill);
        slider.fillRect = fill.GetComponent<RectTransform>();

        // XP numbers — overlaid on the bar, right-aligned.
        var xpText = MakeTMP(detailPanel.transform, "XPText", "0 / 100 XP", 18, TextSubtle);
        TopStretch(xpText, 204f, 152f, 24f, 28f);
        xpText.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

        // Skill section divider — full width, just below portrait bottom.
        var skillDivider = MakeImage(detailPanel.transform, "SkillsDivider", new Color(0.20f, 0.35f, 0.50f, 0.55f));
        TopStretch(skillDivider, 24f, 196f, 24f, 2f);

        // Skills header.
        var skillsHeader = MakeTMP(detailPanel.transform, "SkillsHeader", "SKILLS", 22, TextSubtle);
        TopStretch(skillsHeader, 24f, 202f, 24f, 28f);
        skillsHeader.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;

        // Skills container — VerticalLayoutGroup, fills remaining height below header.
        var skillsContainer = MakeUIGO("SkillsContainer", detailPanel.transform);
        {
            var rt      = skillsContainer.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(24f, 16f);
            rt.offsetMax = new Vector2(-24f, -234f);
        }
        var skillsVLG = skillsContainer.AddComponent<VerticalLayoutGroup>();
        skillsVLG.padding                = new RectOffset(0, 0, 0, 0);
        skillsVLG.spacing                = 6f;
        skillsVLG.childControlWidth      = true;
        skillsVLG.childControlHeight     = true;
        skillsVLG.childForceExpandWidth  = true;
        skillsVLG.childForceExpandHeight = false;
        skillsVLG.childAlignment         = TextAnchor.UpperLeft;

        // ── Wire CrewViewController serialised fields ──────────────────────
        return crewView;
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

        BuildNavButton(container.transform, "SystemButton", "System");
        BuildNavButton(container.transform, "GalaxyButton", "Galaxy");
        BuildNavButton(container.transform, "ShipButton",   "Ship");
        BuildNavButton(container.transform, "CrewButton",   "Crew");

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
        descTMP.enableWordWrapping = true;
        descTMP.overflowMode      = TextOverflowModes.ScrollRect;

        var btnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainBtnPrefabPath);
        GameObject closeGO;
        if (btnPrefab != null)
        {
            closeGO      = (GameObject)Object.Instantiate(btnPrefab, card.transform);
            closeGO.name = "POIDetailCloseButton";
            var mb = closeGO.GetComponent<Michsky.UI.Shift.MainButton>();
            if (mb != null) mb.buttonText = "CLOSE";
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
            var closeLbl = MakeTMP(closeGO.transform, "Label", "CLOSE", 22, TextWhite);
            Stretch(closeLbl);
            closeLbl.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }
        PlaceRect(closeGO, anchor(1f, 0f), anchor(1f, 0f), v2(-102f, 36f), v2(180f, 48f));

        return panel;
    }

    // -----------------------------------------------------------------------
    // Serialised field wiring
    // -----------------------------------------------------------------------

    static void WireController(SystemViewController ctrl,
                                GameObject background,
                                GameObject header, GameObject body,
                                GameObject navBar, GameObject poiDetail)
    {
        var so = new SerializedObject(ctrl);

        Set(so, "starfieldBackground", background.GetComponent<RawImage>());
        Set(so, "systemNameText",      Find<TMP_Text>(header, "SystemNameText"));
        Set(so, "systemMapArea",       FindRT(body, "SystemMap"));
        Set(so, "starNode",            Find<Image>(body, "SystemMap/StarNode"));

        SetNavButton(so, "systemNavButton", navBar, "ButtonContainer/SystemButton");
        SetNavButton(so, "galaxyNavButton", navBar, "ButtonContainer/GalaxyButton");
        SetNavButton(so, "shipNavButton",   navBar, "ButtonContainer/ShipButton");
        SetNavButton(so, "crewNavButton",   navBar, "ButtonContainer/CrewButton");

        so.FindProperty("poiDetailPanel").objectReferenceValue = poiDetail;
        Set(so, "poiDetailNameText", Find<TMP_Text>(poiDetail, "Card/POIDetailNameText"));
        Set(so, "poiDetailTypeText", Find<TMP_Text>(poiDetail, "Card/POIDetailTypeText"));
        Set(so, "poiDetailDescText", Find<TMP_Text>(poiDetail, "Card/POIDetailDescText"));
        var closeTf = poiDetail.transform.Find("Card/POIDetailCloseButton");
        Set(so, "poiDetailCloseButton", closeTf?.GetComponentInChildren<Button>(true));

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

        const string ShipPrefabPath =
            "Assets/2DSpaceshipsFreeTrial/Prefabs/2DSpaceshipsFreeTrialTopView/" +
            "2DScifiFighterExcaliburTopViewMasterPrefab.prefab";
        var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath);
        Sprite shipSprite = null;
        if (shipPrefab != null)
        {
            var sr = shipPrefab.GetComponentInChildren<SpriteRenderer>(false);
            if (sr != null) { shipSprite = sr.sprite; Set(so, "shipSprite", shipSprite); }
            else Debug.LogWarning("[GameSceneSetup] No active SpriteRenderer in ship prefab.");
        }
        else Debug.LogWarning("[GameSceneSetup] Ship prefab not found.");

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
                if (shipSprite != null) Set(gvcSo, "shipSprite", shipSprite);

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
            // shipViewImage / shipComponentButtons are not serialised on SystemViewController.
            var shipImgComp = shipViewGO.transform.Find("ShipImage")?.GetComponent<Image>();
            if (shipImgComp != null && shipSprite != null) shipImgComp.sprite = shipSprite;
        }
        else Debug.LogWarning("[GameSceneSetup] ShipView not found under Body.");

        // Crew view
        var crewViewGO = body.transform.Find("CrewView")?.gameObject;
        if (crewViewGO != null)
            so.FindProperty("crewViewPanel").objectReferenceValue = crewViewGO;
        else
            Debug.LogWarning("[GameSceneSetup] CrewView not found under Body.");

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

    static Vector2 anchor(float x, float y) => new Vector2(x, y);
    static Vector2 v2(float x, float y)     => new Vector2(x, y);
}
