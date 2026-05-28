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
        // Fills canvas minus 90 px header at top and 80 px nav bar at bottom.
        // anchoredPosition.y = (80 - 90) / 2 = -5  (centre shifts slightly downward)
        // sizeDelta.y = -(90 + 80) = -170
        var body = MakeUIGO("Body", parent);
        PlaceRect(body, anchor(0f, 0f), anchor(1f, 1f), v2(0f, -5f), v2(0f, -170f));

        BuildSystemMap(body.transform);
        BuildGalaxyView(body.transform);

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

        return galaxyView;
    }

    // -----------------------------------------------------------------------
    // Nav bar — bottom 80 px with four centred navigation buttons
    // -----------------------------------------------------------------------

    static GameObject BuildNavBar(Transform parent)
    {
        var bar = MakeImage(parent, "NavBar", HeaderBar);
        PlaceRect(bar, anchor(0f, 0f), anchor(1f, 0f), v2(0f, 40f), v2(0f, 80f));

        // Cyan accent line along the top of the nav bar
        var rule = MakeImage(bar.transform, "AccentLine", AccentCyan);
        PlaceRect(rule, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -1f), v2(0f, 2f));

        // Fixed-width container centred inside the bar
        // 4 buttons × ~190 px + 3 gaps × 16 px + 32 px padding = ~828 px — use 860 px
        var container = MakeUIGO("ButtonContainer", bar.transform);
        PlaceRect(container, anchor(0.5f, 0f), anchor(0.5f, 1f), v2(0f, 0f), v2(860f, -16f));

        var hlg = container.AddComponent<HorizontalLayoutGroup>();
        hlg.padding               = new RectOffset(0, 0, 0, 0);
        hlg.spacing               = 16f;
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment        = TextAnchor.MiddleCenter;

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
            Debug.LogWarning($"[GameSceneSetup] Shift MainButton prefab not found — using plain button for '{name}'.");
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
    // POI Detail Panel — full-screen scrim with centred card
    // -----------------------------------------------------------------------

    static GameObject BuildPOIDetailPanel(Transform parent)
    {
        var panel = MakeImage(parent, "POIDetailPanel", Scrim);
        Stretch(panel);
        panel.SetActive(false);

        // Card: 700 × 480, centred in the panel
        var card = MakeImage(panel.transform, "Card", PanelBg);
        PlaceRect(card, anchor(0.5f, 0.5f), anchor(0.5f, 0.5f), v2(0f, 0f), v2(700f, 480f));

        // ── TopBar: 4 px cyan accent at the very top of the card (0–4 px) ─────
        var topBar = MakeImage(card.transform, "TopBar", AccentCyan);
        PlaceRect(topBar, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -2f), v2(0f, 4f));

        // ── Name: 16–84 px from card top (height 68, centre at -50) ──────────
        var poiName = MakeTMP(card.transform, "POIDetailNameText", "POI Name", 52, TextWhite);
        PlaceRect(poiName, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -50f), v2(-60f, 68f));
        poiName.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        poiName.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // ── Type: 94–138 px from card top (height 44, centre at -116) ────────
        var poiType = MakeTMP(card.transform, "POIDetailTypeText", "Planet", 32, AccentCyan);
        PlaceRect(poiType, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -116f), v2(-60f, 44f));
        poiType.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Left;
        poiType.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        // ── Rule: 2 px bar at 150 px from card top ───────────────────────────
        var rule = MakeImage(card.transform, "Rule", DividerColor);
        PlaceRect(rule, anchor(0f, 1f), anchor(1f, 1f), v2(0f, -150f), v2(-40f, 2f));

        // ── Desc: 162 px from top → 72 px from bottom (no overlap with anything)
        // offsetMax.y = −162  →  anchoredPosition.y = (−162 + 72) / 2 = −45
        // sizeDelta.y = −162 − 72 = −234
        var desc = MakeTMP(card.transform, "POIDetailDescText",
            "A rocky world with varied terrain.", 30, TextSubtle);
        PlaceRect(desc, anchor(0f, 0f), anchor(1f, 1f), v2(0f, -45f), v2(-60f, -234f));
        var descTMP               = desc.GetComponent<TextMeshProUGUI>();
        descTMP.alignment         = TextAlignmentOptions.TopLeft;
        descTMP.enableWordWrapping = true;
        descTMP.overflowMode      = TextOverflowModes.ScrollRect;

        // ── Close button: bottom-right, 12 px from right edge, 12 px from bottom
        // centre at (−102, 36) from anchor(1, 0)  →  top at 60 px from bottom (420 from top)
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
            Debug.LogWarning("[GameSceneSetup] Shift MainButton prefab not found — using plain button for close.");
            closeGO = MakeImage(card.transform, "POIDetailCloseButton", BtnNormal);
            var closeBtn            = closeGO.AddComponent<Button>();
            var closeBtnColors      = closeBtn.colors;
            closeBtnColors.highlightedColor = new Color(0.12f, 0.38f, 0.60f, 1f);
            closeBtnColors.pressedColor     = new Color(0.04f, 0.16f, 0.28f, 1f);
            closeBtn.colors         = closeBtnColors;
            closeBtn.targetGraphic  = closeGO.GetComponent<Image>();
            var closeLabel = MakeTMP(closeGO.transform, "CloseLabel", "CLOSE", 22, TextWhite);
            Stretch(closeLabel);
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

        // Starfield background
        Set(so, "starfieldBackground", background.GetComponent<RawImage>());

        // Header
        Set(so, "systemNameText", Find<TMP_Text>(header, "SystemNameText"));

        // System map
        Set(so, "systemMapArea", FindRT(body, "SystemMap"));
        Set(so, "starNode",      Find<Image>(body, "SystemMap/StarNode"));

        // Nav buttons — Button may be nested inside the Shift prefab hierarchy
        SetNavButton(so, "systemNavButton", navBar, "ButtonContainer/SystemButton");
        SetNavButton(so, "galaxyNavButton", navBar, "ButtonContainer/GalaxyButton");
        SetNavButton(so, "shipNavButton",   navBar, "ButtonContainer/ShipButton");
        SetNavButton(so, "crewNavButton",   navBar, "ButtonContainer/CrewButton");

        // POI detail panel
        so.FindProperty("poiDetailPanel").objectReferenceValue = poiDetail;
        Set(so, "poiDetailNameText", Find<TMP_Text>(poiDetail, "Card/POIDetailNameText"));
        Set(so, "poiDetailTypeText", Find<TMP_Text>(poiDetail, "Card/POIDetailTypeText"));
        Set(so, "poiDetailDescText", Find<TMP_Text>(poiDetail, "Card/POIDetailDescText"));
        // Close button — use GetComponentInChildren so it resolves whether the
        // root is a plain Button or a Shift prefab with a nested Button component.
        var closeTf = poiDetail.transform.Find("Card/POIDetailCloseButton");
        Set(so, "poiDetailCloseButton", closeTf?.GetComponentInChildren<Button>(true));

        // ── Ship sprite ───────────────────────────────────────────────────
        // Extract the sprite from the active (Blue) SpriteRenderer in the prefab.
        const string ShipPrefabPath =
            "Assets/2DSpaceshipsFreeTrial/Prefabs/2DSpaceshipsFreeTrialTopView/" +
            "2DScifiFighterExcaliburTopViewMasterPrefab.prefab";
        var shipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath);
        if (shipPrefab != null)
        {
            // GetComponentInChildren with includeInactive=false returns the first
            // active SpriteRenderer, which is the Blue variant by default.
            var sr = shipPrefab.GetComponentInChildren<SpriteRenderer>(false);
            if (sr != null)
                Set(so, "shipSprite", sr.sprite);
            else
                Debug.LogWarning("[GameSceneSetup] No active SpriteRenderer in ship prefab — shipSprite not wired.");
        }
        else
        {
            Debug.LogWarning("[GameSceneSetup] Ship prefab not found at: " + ShipPrefabPath);
        }

        // ── Galaxy view ───────────────────────────────────────────────────
        // GalaxyView is a direct child of Body; GalaxyViewController lives on it.
        var galaxyViewGO = body.transform.Find("GalaxyView")?.gameObject;
        if (galaxyViewGO != null)
        {
            // Wire SystemViewController's two galaxy fields
            Set(so, "galaxyViewPanel",      galaxyViewGO);

            var gvc = galaxyViewGO.GetComponent<GalaxyViewController>();
            Set(so, "galaxyViewController", gvc);

            // Wire GalaxyViewController's own serialised fields
            if (gvc != null)
            {
                var gvcSo = new SerializedObject(gvc);

                var bgTf = galaxyViewGO.transform.Find("GalaxyMap/GalaxyBackground");
                Set(gvcSo, "galaxyBackground",
                    bgTf != null ? bgTf.GetComponent<RawImage>() : null);

                Set(gvcSo, "systemNodesContainer",
                    FindRT(galaxyViewGO, "GalaxyMap/SystemNodesContainer"));

                gvcSo.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[GameSceneSetup] GalaxyViewController component not found on GalaxyView.");
            }
        }
        else
        {
            Debug.LogWarning("[GameSceneSetup] GalaxyView not found under Body — galaxy fields not wired.");
        }

        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Finds a nav button GameObject by path, then locates its Button component
    /// (which may be on the root or nested inside a Shift prefab hierarchy).
    /// </summary>
    static void SetNavButton(SerializedObject so, string prop, GameObject navBar, string path)
    {
        var t = navBar?.transform.Find(path);
        if (t == null)
        {
            Debug.LogWarning($"[GameSceneSetup] Could not find nav button at '{path}' under {navBar?.name}");
            return;
        }
        var btn = t.GetComponentInChildren<Button>(true);
        if (btn == null)
            Debug.LogWarning($"[GameSceneSetup] No Button component found under '{path}'");
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = btn;
        else Debug.LogWarning($"[GameSceneSetup] Property not found: {prop}");
    }

    // -----------------------------------------------------------------------
    // Core factory helpers (mirrors MainMenuSceneSetup conventions)
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
        var ri = go.AddComponent<RawImage>();
        ri.color = color;
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
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) { Debug.LogError($"[GameSceneSetup] No RectTransform on {go.name}"); return; }
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static void PlaceRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) { Debug.LogError($"[GameSceneSetup] No RectTransform on {go.name}"); return; }
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
        if (root == null) return null;
        var t = root.transform.Find(path);
        if (t == null)
        {
            Debug.LogWarning($"[GameSceneSetup] Could not find '{path}' under {root.name}");
            return null;
        }
        return t.GetComponent<T>();
    }

    static RectTransform FindRT(GameObject root, string path)
    {
        if (root == null) return null;
        var t = root.transform.Find(path);
        if (t == null)
        {
            Debug.LogWarning($"[GameSceneSetup] Could not find RT '{path}' under {root.name}");
            return null;
        }
        return t.GetComponent<RectTransform>();
    }

    static Vector2 anchor(float x, float y) => new Vector2(x, y);
    static Vector2 v2(float x, float y)     => new Vector2(x, y);
}
