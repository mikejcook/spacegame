using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Builds the MainMenu scene structure with one click.
/// Menu: Star Captain -> Setup Main Menu Scene
///
/// Run this once in a fresh/empty scene, then save it as
/// Assets/Scenes/MainMenu.unity
/// </summary>
public static class MainMenuSceneSetup
{
    // -----------------------------------------------------------------------
    // Colour palette — sci-fi dark theme
    // -----------------------------------------------------------------------
    static readonly Color BgColor      = new Color(0.04f, 0.06f, 0.10f, 1.00f);
    static readonly Color PanelBg      = new Color(0.04f, 0.06f, 0.10f, 0.97f);
    static readonly Color BtnNormal    = new Color(0.08f, 0.25f, 0.42f, 1.00f);
    static readonly Color BtnHighlight = new Color(0.12f, 0.38f, 0.60f, 1.00f);
    static readonly Color BtnPressed   = new Color(0.04f, 0.16f, 0.28f, 1.00f);
    static readonly Color BtnBack      = new Color(0.12f, 0.12f, 0.18f, 1.00f);
    static readonly Color AccentCyan   = new Color(0.30f, 0.85f, 1.00f, 1.00f);
    static readonly Color TextWhite    = new Color(0.92f, 0.95f, 1.00f, 1.00f);
    static readonly Color TextSubtle   = new Color(0.60f, 0.72f, 0.85f, 1.00f);
    static readonly Color TextError    = new Color(1.00f, 0.38f, 0.38f, 1.00f);
    static readonly Color InputBg      = new Color(0.08f, 0.12f, 0.18f, 1.00f);
    static readonly Color InputPh      = new Color(0.40f, 0.52f, 0.65f, 1.00f);
    static readonly Color OverlayDark  = new Color(0.00f, 0.00f, 0.00f, 0.40f);
    static readonly Color PortraitBg   = new Color(0.06f, 0.08f, 0.14f, 1.00f);
    static readonly Color PortraitHint = new Color(0.00f, 0.00f, 0.00f, 0.60f);

    // -----------------------------------------------------------------------
    // Entry point
    // -----------------------------------------------------------------------
    [MenuItem("Star Captain/Setup Main Menu Scene")]
    public static void Setup()
    {
        if (!EditorUtility.DisplayDialog(
            "Setup Main Menu Scene",
            "This will populate the current scene with the GameManager hierarchy " +
            "and full MainMenu Canvas.\n\nRun in a new empty scene for best results.",
            "Build It", "Cancel"))
            return;

        // GameManager + DatabaseManager
        var gmGO = MakeGO("GameManager", null);
        gmGO.AddComponent<GameManager>();
        Undo.RegisterCreatedObjectUndo(gmGO, "Create GameManager");

        var dbGO = MakeGO("DatabaseManager", gmGO.transform);
        dbGO.AddComponent<DatabaseManager>();

        // Camera
        var camGO = MakeGO("Main Camera", null);
        camGO.AddComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        camGO.GetComponent<Camera>().backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
        camGO.tag = "MainCamera";
        Undo.RegisterCreatedObjectUndo(camGO, "Create Main Camera");

        // EventSystem
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

        // Canvas
        var canvasGO = MakeGO("Canvas", null);
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); // landscape phone/tablet
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f; // blend width+height scaling

        canvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen background
        var bg = MakeImage(canvasGO.transform, "Background", BgColor);
        Stretch(bg);

        // Build panels
        var mainPanel          = BuildMainPanel(canvasGO.transform);
        var newGamePanel       = BuildNewGamePanel(canvasGO.transform);
        var loadGamePanel      = BuildLoadGamePanel(canvasGO.transform);
        var settingsPanel      = BuildSettingsPanel(canvasGO.transform);
        var portraitPickerPanel = BuildPortraitPickerPanel(canvasGO.transform);

        newGamePanel.SetActive(false);
        loadGamePanel.SetActive(false);
        settingsPanel.SetActive(false);
        portraitPickerPanel.SetActive(false);

        // Story interlude overlay (separate high-sort-order Canvas)
        var storyCtrl = BuildStoryInterludeOverlay();

        // MainMenuController — wire everything
        var ctrlGO     = MakeUIGO("MainMenuController", canvasGO.transform);
        var controller = ctrlGO.AddComponent<MainMenuController>();
        WireController(controller, mainPanel, newGamePanel, loadGamePanel,
                       settingsPanel, portraitPickerPanel, storyCtrl);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Done!",
            "Main Menu scene built.\n\nSave it now as:\nAssets/Scenes/MainMenu.unity\n(File -> Save As...)\n\n" +
            "Remember to run:\n  Star Captain -> Build Portrait Library\nbefore entering Play Mode.",
            "OK");

        Debug.Log("[Star Captain] MainMenu scene setup complete.");
    }

    // -----------------------------------------------------------------------
    // Panel builders
    // -----------------------------------------------------------------------

    static GameObject BuildMainPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "MainPanel");

        // Game title
        var title = MakeTMP(panel.transform, "TitleText", "STAR CAPTAIN", 84, AccentCyan);
        PlaceRect(title, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, 160), v2(900, 110));
        title.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Subtitle
        var sub = MakeTMP(panel.transform, "SubtitleText", "A PRIVATEER'S JOURNEY", 26, TextSubtle);
        PlaceRect(sub, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, 105), v2(700, 40));
        sub.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Italic;

        // Decorative rule
        var rule = MakeImage(panel.transform, "Rule", AccentCyan);
        PlaceRect(rule, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, 78), v2(500, 2));

        // Button column
        var col = MakeUIGO("ButtonGroup", panel.transform);
        PlaceRect(col, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, -80), v2(360, 10));
        var vlg = col.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 14;
        vlg.childAlignment        = TextAnchor.UpperCenter;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = true;
        vlg.childForceExpandWidth = true;
        col.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeMenuBtn(col.transform, "ContinueButton", "CONTINUE");
        MakeMenuBtn(col.transform, "NewGameButton",  "NEW GAME");
        MakeMenuBtn(col.transform, "LoadGameButton", "LOAD GAME");
        MakeMenuBtn(col.transform, "SettingsButton", "SETTINGS");
        MakeMenuBtn(col.transform, "QuitButton",     "QUIT",     danger: true);

        return panel;
    }

    /// <summary>
    /// New Game panel — landscape two-column layout:
    ///   Left  → Captain Name + Ship Name fields
    ///   Right → Portrait selector
    ///   Below → Validation text + Begin Journey button centred under both columns
    /// </summary>
    static GameObject BuildNewGamePanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "NewGamePanel");
        MakePanelTitle(panel.transform, "NEW GAME");

        // ── Wide landscape card ───────────────────────────────────────────────
        var card = MakeImage(panel.transform, "Card", OverlayDark);
        PlaceRect(card, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, -10), v2(1480, 580));

        // Outer VLG: columns on top, validation + button below
        var content = MakeUIGO("Content", card.transform);
        PlaceRect(content, anchor(0f, 0f), anchor(1f, 1f), Vector2.zero, Vector2.zero);
        {
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing               = 14;
            vlg.padding               = new RectOffset(48, 48, 40, 40);
            vlg.childControlWidth     = true;
            vlg.childControlHeight    = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight= false;
        }

        // ── Two-column row ────────────────────────────────────────────────────
        var columns = MakeUIGO("Columns", content.transform);
        {
            var le = columns.AddComponent<LayoutElement>();
            le.flexibleHeight = 1; // takes all remaining vertical space
            var hlg = columns.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 60;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
        }

        // ── LEFT COLUMN — captain name + ship name ────────────────────────────
        var leftCol = MakeUIGO("LeftColumn", columns.transform);
        {
            var le = leftCol.AddComponent<LayoutElement>();
            le.flexibleWidth = 1; // fills remaining space after right column
            var vlg = leftCol.AddComponent<VerticalLayoutGroup>();
            vlg.spacing               = 14;
            vlg.childControlWidth     = true;
            vlg.childControlHeight    = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight= false;
            vlg.childAlignment        = TextAnchor.UpperLeft;
        }

        // Captain name label
        var lblCaptain = MakeUIGO("LblCaptain", leftCol.transform);
        {
            var le  = lblCaptain.AddComponent<LayoutElement>();
            le.preferredHeight = 46; le.minHeight = 46;
            var tmp       = lblCaptain.AddComponent<TextMeshProUGUI>();
            tmp.text      = "CAPTAIN NAME";
            tmp.fontSize  = 34;
            tmp.color     = TextSubtle;
            tmp.fontStyle = FontStyles.Bold;
        }

        // Captain name input
        MakeLargeInputField(leftCol.transform, "CaptainNameInput", "Enter your name");

        MakeSpacer(leftCol.transform, 10);

        // Ship name label
        var lblShip = MakeUIGO("LblShip", leftCol.transform);
        {
            var le  = lblShip.AddComponent<LayoutElement>();
            le.preferredHeight = 46; le.minHeight = 46;
            var tmp       = lblShip.AddComponent<TextMeshProUGUI>();
            tmp.text      = "SHIP NAME";
            tmp.fontSize  = 34;
            tmp.color     = TextSubtle;
            tmp.fontStyle = FontStyles.Bold;
        }

        // Ship name row: input + Random button
        var shipRow = MakeUIGO("ShipNameRow", leftCol.transform);
        {
            var le = shipRow.AddComponent<LayoutElement>();
            le.preferredHeight = 88; le.minHeight = 88;
            var hlg = shipRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 12;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = false;
        }

        // Ship name input — flexible
        var shipInput = MakeUIGO("ShipNameInput", shipRow.transform);
        {
            var le = shipInput.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            shipInput.AddComponent<Image>().color = InputBg;
            MakeTMPInputField(shipInput, "Horizon", 34);
        }

        // Random button — fixed width
        var randomBtn = MakeUIGO("RandomShipNameButton", shipRow.transform);
        {
            var le = randomBtn.AddComponent<LayoutElement>();
            le.preferredWidth = 200; le.minWidth = 200;

            var img = randomBtn.AddComponent<Image>();
            img.color = BtnNormal;
            var btn    = randomBtn.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = BtnNormal;
            colors.highlightedColor = BtnHighlight;
            colors.pressedColor     = BtnPressed;
            btn.colors = colors;

            var lblGO = MakeUIGO("Label", randomBtn.transform);
            Stretch(lblGO);
            var tmp       = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "Random";
            tmp.fontSize  = 30;
            tmp.color     = TextWhite;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
        }

        // ── RIGHT COLUMN — portrait ───────────────────────────────────────────
        var rightCol = MakeUIGO("RightColumn", columns.transform);
        {
            var le = rightCol.AddComponent<LayoutElement>();
            le.preferredWidth  = 340;
            le.minWidth        = 340;
            var vlg = rightCol.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment         = TextAnchor.MiddleCenter;
            vlg.childControlWidth      = true;   // must be true so LayoutElement sizes are respected
            vlg.childControlHeight     = true;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing                = 10;
        }

        var portraitBtn = MakeUIGO("PortraitButton", rightCol.transform);
        {
            var le = portraitBtn.AddComponent<LayoutElement>();
            le.preferredWidth  = 300; le.minWidth  = 300;
            le.preferredHeight = 300; le.minHeight = 300;

            var img = portraitBtn.AddComponent<Image>();
            img.color = PortraitBg;
            var btn    = portraitBtn.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = PortraitBg;
            colors.highlightedColor = BtnNormal;
            colors.pressedColor     = BtnPressed;
            btn.colors = colors;
        }

        // Portrait RawImage — filled at runtime by PortraitLibrary
        var portraitImg = MakeUIGO("PortraitImage", portraitBtn.transform);
        {
            var rt = portraitImg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
            portraitImg.AddComponent<RawImage>().color = Color.white;
        }

        // "Tap to Change" label sits BELOW the portrait button (not overlaid)
        var tapHint = MakeUIGO("TapToChangeLabel", rightCol.transform);
        {
            var le = tapHint.AddComponent<LayoutElement>();
            le.preferredHeight = 34; le.minHeight = 34;
            var tmp       = tapHint.AddComponent<TextMeshProUGUI>();
            tmp.text      = "TAP TO CHANGE";
            tmp.fontSize  = 22;
            tmp.color     = TextSubtle;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Italic;
        }

        // ── Validation text + Begin Journey — centred under both columns ──────
        MakeTMPLayout(content.transform, "ValidationText", "", 26, TextError, 36);

        var startBtn = MakeMenuBtn(content.transform, "StartButton", "BEGIN JOURNEY");
        {
            var le = startBtn.GetComponent<LayoutElement>();
            if (le == null) le = startBtn.AddComponent<LayoutElement>();
            le.preferredHeight = 80;
            le.minHeight       = 80;
            var lbl = startBtn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl != null) lbl.fontSize = 32;
        }

        MakeSpacer(content.transform, 16); // breathing room below Begin Journey

        MakeBackBtn(panel.transform, "BackButton");
        return panel;
    }

    /// <summary>
    /// Full-screen portrait picker overlay.
    /// Layout uses a VLG so nothing can misalign via absolute positioning.
    /// Populated at runtime by PortraitPickerPanel from PortraitLibrary.
    ///
    /// IMPORTANT: Run  Star Captain → Build Portrait Library  before Play Mode
    /// so the portrait textures are loaded into the asset.
    /// </summary>
    /// <summary>
    /// Full-screen portrait picker overlay.
    /// Uses explicit anchor-based positioning for header, divider, and scroll view
    /// so sizes are deterministic and do not depend on layout group computation order.
    ///
    /// Hierarchy (no "Inner" wrapper — everything is a direct child of Container):
    ///   PortraitPickerPanel  (full-screen scrim)
    ///     Container          (centred box)
    ///       Header           (absolute, anchored top)
    ///         Title
    ///         CloseButton
    ///       Divider          (absolute, anchored below header)
    ///       ScrollView       (fills remaining space via stretch anchors + offset)
    ///         Viewport
    ///           Content      (GridLayoutGroup, populated at runtime)
    /// </summary>
    static GameObject BuildPortraitPickerPanel(Transform canvas)
    {
        // Root — full-screen scrim, blocks clicks behind it
        var root = MakeUIGO("PortraitPickerPanel", canvas);
        Stretch(root);
        root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
        root.AddComponent<PortraitPickerPanel>();

        // Centred box ─────────────────────────────────────────────────────────
        // Layout budget (canvas units, 1920x1080 reference resolution):
        //   3 portrait cells × 150px = 450
        //   2 inter-cell spacing × 12 =  24
        //   Grid padding (left+right) = 24
        //   Subtotal content width    = 498
        //   ScrollView inset from box = 32  (16 each side)
        //   Box internal margin       = 30  (~15 each side breathing room)
        //   ───────────────────────────────────
        //   Required box width        ≈ 560 → use 600 for breathing room
        //
        // Height: header(60) + divider(2) + gap(8) + scroll(>=434) + padding(16+16) = 536
        const float boxW = 600f;
        const float boxH = 536f;

        // Derived constants (all canvas units)
        const float pad    = 16f;  // left/right/top/bottom padding
        const float hdrH   = 60f;  // header height
        const float divH   =  2f;  // divider height
        const float gap    =  8f;  // gap between divider and scroll
        const float scrollTop = pad + hdrH + divH + gap; // = 86
        const float scrollBot = pad;                      // = 16

        var box = MakeUIGO("Container", root.transform);
        PlaceRect(box, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, 0), v2(boxW, boxH));
        box.AddComponent<Image>().color = PanelBg;

        // ── Header: anchored to top of box, full width minus padding ─────────
        var header = MakeUIGO("Header", box.transform);
        {
            var rt = header.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.sizeDelta        = new Vector2(-pad * 2f, hdrH);
            rt.anchoredPosition = new Vector2(0f, -pad);

            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 8;
            hlg.childAlignment         = TextAnchor.MiddleLeft;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = true;   // title expands to fill
            hlg.childForceExpandHeight = true;
        }

        var titleGO = MakeUIGO("Title", header.transform);
        {
            var le = titleGO.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            var tmp       = titleGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "SELECT PORTRAIT";
            tmp.fontSize  = 30;
            tmp.color     = AccentCyan;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        var closeGO = MakeUIGO("CloseButton", header.transform);
        {
            var le = closeGO.AddComponent<LayoutElement>();
            le.preferredWidth  = 56; le.minWidth  = 56;
            le.flexibleWidth   = 0;  // don't expand

            var img    = closeGO.AddComponent<Image>();
            img.color  = BtnBack;
            var btn    = closeGO.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor      = BtnBack;
            colors.highlightedColor = BtnNormal;
            colors.pressedColor     = BtnPressed;
            btn.colors = colors;

            var lblGO = MakeUIGO("Label", closeGO.transform);
            Stretch(lblGO);
            var tmp       = lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "✕";
            tmp.fontSize  = 28;
            tmp.color     = TextWhite;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        // ── Divider: 2px cyan line, immediately below header ─────────────────
        var divider = MakeUIGO("Divider", box.transform);
        {
            var rt = divider.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(0.5f, 1f);
            rt.sizeDelta        = new Vector2(-pad * 2f, divH);
            rt.anchoredPosition = new Vector2(0f, -(pad + hdrH));
            divider.AddComponent<Image>().color = AccentCyan;
        }

        // ── ScrollView: stretch-anchored, fills everything below the divider ──
        // offsetMin = (left, bottom) from anchor corners
        // offsetMax = (right, top) from anchor corners  (negative = inset)
        var svGO = MakeUIGO("ScrollView", box.transform);
        {
            var rt = svGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2( pad,       scrollBot);
            rt.offsetMax = new Vector2(-pad,      -scrollTop);
            svGO.AddComponent<Image>().color = Color.clear;
        }
        var scroll = svGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var vp = MakeUIGO("Viewport", svGO.transform);
        Stretch(vp);
        // IMPORTANT: Mask uses the underlying Graphic's alpha to define the masked region.
        // If this Image is Color.clear (alpha=0), the mask is empty and NOTHING inside the
        // scroll view renders. Use opaque white + showMaskGraphic=false: the image shape
        // becomes the mask but the image itself is not drawn.
        vp.AddComponent<Image>().color = Color.white;
        vp.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = vp.GetComponent<RectTransform>();

        // Grid content — populated at runtime by PortraitPickerPanel
        var gridContent = MakeUIGO("Content", vp.transform);
        {
            var crt       = gridContent.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot     = new Vector2(0.5f, 1f);
            crt.sizeDelta = Vector2.zero;

            var grid = gridContent.AddComponent<GridLayoutGroup>();
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize        = new Vector2(150f, 150f);
            grid.spacing         = new Vector2(12f, 12f);
            grid.padding         = new RectOffset(12, 12, 12, 12);
            grid.childAlignment  = TextAnchor.UpperCenter;

            gridContent.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = crt;
        }

        return root;
    }

    static GameObject BuildLoadGamePanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "LoadGamePanel");
        MakePanelTitle(panel.transform, "LOAD GAME");

        var noSaves = MakeTMP(panel.transform, "NoSavesText", "No saved games found.", 30, TextSubtle);
        PlaceRect(noSaves, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, 0), v2(700, 50));

        var sv = MakeUIGO("ScrollView", panel.transform);
        PlaceRect(sv, anchor(.15f, .15f), anchor(.85f, .82f), Vector2.zero, Vector2.zero);
        sv.AddComponent<Image>().color = OverlayDark;
        var scroll = sv.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var vp = MakeUIGO("Viewport", sv.transform);
        Stretch(vp);
        vp.AddComponent<Image>().color = Color.clear;
        var mask = vp.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scroll.viewport = vp.GetComponent<RectTransform>();

        var content = MakeUIGO("Content", vp.transform);
        var crt     = content.GetComponent<RectTransform>();
        crt.anchorMin   = new Vector2(0f, 1f);
        crt.anchorMax   = new Vector2(1f, 1f);
        crt.pivot       = new Vector2(.5f, 1f);
        crt.sizeDelta   = Vector2.zero;
        var cvlg = content.AddComponent<VerticalLayoutGroup>();
        cvlg.spacing              = 10;
        cvlg.padding              = new RectOffset(10, 10, 10, 10);
        cvlg.childControlWidth    = true;
        cvlg.childControlHeight   = true;
        cvlg.childForceExpandWidth= true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = crt;

        MakeBackBtn(panel.transform, "BackButton");
        return panel;
    }

    static GameObject BuildSettingsPanel(Transform canvas)
    {
        var panel = MakePanel(canvas, "SettingsPanel");
        MakePanelTitle(panel.transform, "SETTINGS");

        var card = MakeImage(panel.transform, "Card", OverlayDark);
        PlaceRect(card, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, -30), v2(640, 260));

        var opts = MakeUIGO("Options", card.transform);
        PlaceRect(opts, anchor(.5f, .5f), anchor(.5f, .5f), v2(0, 0), v2(560, 200));
        var vlg = opts.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = 24;
        vlg.padding               = new RectOffset(20, 20, 20, 20);
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = true;
        vlg.childForceExpandWidth = true;

        MakeSliderRow(opts.transform, "MusicRow", "MUSIC VOLUME", "MusicSlider", 0.8f);
        MakeSliderRow(opts.transform, "SFXRow",   "SFX VOLUME",   "SFXSlider",   1.0f);

        MakeBackBtn(panel.transform, "BackButton");
        return panel;
    }

    // -----------------------------------------------------------------------
    // Story Interlude overlay builder
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates the StoryInterlude overlay on its own Canvas (sort order 100),
    /// wires all internal serialised fields, and returns the controller component.
    ///
    /// Hierarchy produced:
    ///   InterludeCanvas              Canvas, sortOrder 100
    ///   └─ StoryInterludeOverlay     StoryInterludeController
    ///      └─ OverlayRoot            full-screen black panel (starts inactive)
    ///         ├─ SceneImage          RawImage, full-screen
    ///         ├─ PlaceholderPanel    Image (dark), full-screen
    ///         ├─ CaptionBar          Image (semi-transparent), bottom 22%
    ///         │  └─ CaptionText      TMP_Text
    ///         ├─ TapHint             TMP_Text "TAP TO CONTINUE", bottom-right
    ///         └─ TapButton           full-screen transparent Button
    /// </summary>
    static StoryInterludeController BuildStoryInterludeOverlay()
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        var canvasGO = MakeGO("InterludeCanvas", null);
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create InterludeCanvas");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // above the main menu canvas

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Controller root ───────────────────────────────────────────────────
        var ctrlGO = MakeUIGO("StoryInterludeOverlay", canvasGO.transform);
        Stretch(ctrlGO);
        var ctrl = ctrlGO.AddComponent<StoryInterludeController>();

        // ── OverlayRoot — starts hidden; controller shows/hides this ─────────
        var root = MakeUIGO("OverlayRoot", ctrlGO.transform);
        Stretch(root);
        root.AddComponent<Image>().color = Color.black;
        root.SetActive(false);

        // ── SceneImage — full-screen RawImage for scene pictures ──────────────
        var sceneImgGO = MakeUIGO("SceneImage", root.transform);
        Stretch(sceneImgGO);
        sceneImgGO.AddComponent<RawImage>().color = Color.white;

        // ── PlaceholderPanel — shown when no imageResource is provided ─────────
        var placeholderGO = MakeUIGO("PlaceholderPanel", root.transform);
        Stretch(placeholderGO);
        placeholderGO.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 1f);

        // ── CaptionBar — semi-transparent strip at the bottom 22% ─────────────
        var captionBar = MakeUIGO("CaptionBar", root.transform);
        {
            var rt = captionBar.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(1f, 0.22f);
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            captionBar.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        }

        // CaptionText inside CaptionBar
        var captionTextGO = MakeUIGO("CaptionText", captionBar.transform);
        {
            var rt = captionTextGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.04f, 0.12f);
            rt.anchorMax        = new Vector2(0.96f, 0.88f);
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            var tmp = captionTextGO.AddComponent<TextMeshProUGUI>();
            tmp.text               = "";
            tmp.fontSize           = 38;
            tmp.color              = TextWhite;
            tmp.alignment          = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
        }

        // ── TapHint — full-width, right-aligned, so it can never be clipped ────
        var tapHintGO = MakeUIGO("TapHint", root.transform);
        {
            var rt = tapHintGO.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(0.95f, 0.22f);  // 5% right margin — never clipped
            rt.sizeDelta        = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            var tmp       = tapHintGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = "TAP TO CONTINUE";
            tmp.fontSize  = 22;
            tmp.color     = new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.80f);
            tmp.alignment = TextAlignmentOptions.BottomRight;
            tmp.fontStyle = FontStyles.Italic;
        }

        // ── TapButton — invisible full-screen button that advances the scene ───
        var tapBtnGO = MakeUIGO("TapButton", root.transform);
        Stretch(tapBtnGO);
        var tapImg    = tapBtnGO.AddComponent<Image>();
        tapImg.color  = Color.clear;
        var tapBtn    = tapBtnGO.AddComponent<Button>();
        var tapColors = tapBtn.colors;
        tapColors.normalColor      = Color.clear;
        tapColors.highlightedColor = Color.clear;
        tapColors.pressedColor     = new Color(1f, 1f, 1f, 0.04f);
        tapColors.selectedColor    = Color.clear;
        tapBtn.colors = tapColors;

        // ── Wire serialised fields on StoryInterludeController ────────────────
        var so = new SerializedObject(ctrl);
        Set(so, "overlayRoot",      root);
        Set(so, "sceneImage",       sceneImgGO.GetComponent<RawImage>());
        Set(so, "placeholderPanel", placeholderGO.GetComponent<Image>());
        Set(so, "captionText",      captionTextGO.GetComponent<TextMeshProUGUI>());
        Set(so, "tapHint",          tapHintGO);
        Set(so, "tapButton",        tapBtnGO.GetComponent<Button>());
        so.ApplyModifiedProperties();

        return ctrl;
    }

    // -----------------------------------------------------------------------
    // Wire MainMenuController serialised fields
    // -----------------------------------------------------------------------
    static void WireController(
        MainMenuController ctrl,
        GameObject main,
        GameObject newGame,
        GameObject load,
        GameObject settings,
        GameObject portraitPicker,
        StoryInterludeController storyInterlude = null)
    {
        var so = new SerializedObject(ctrl);

        // Panels
        Set(so, "mainPanel",     main);
        Set(so, "newGamePanel",  newGame);
        Set(so, "loadGamePanel", load);
        Set(so, "settingsPanel", settings);

        // Main panel buttons
        Set(so, "continueButton", Find<Button>(main, "ButtonGroup/ContinueButton"));
        Set(so, "newGameButton",  Find<Button>(main, "ButtonGroup/NewGameButton"));
        Set(so, "loadGameButton", Find<Button>(main, "ButtonGroup/LoadGameButton"));
        Set(so, "settingsButton", Find<Button>(main, "ButtonGroup/SettingsButton"));
        Set(so, "quitButton",     Find<Button>(main, "ButtonGroup/QuitButton"));

        // New game panel — inputs
        Set(so, "captainNameInput",    Find<TMP_InputField>(newGame, "Card/Content/Columns/LeftColumn/CaptainNameInput"));
        Set(so, "shipNameInput",       Find<TMP_InputField>(newGame, "Card/Content/Columns/LeftColumn/ShipNameRow/ShipNameInput"));
        Set(so, "randomShipNameButton",Find<Button>        (newGame, "Card/Content/Columns/LeftColumn/ShipNameRow/RandomShipNameButton"));
        Set(so, "startGameButton",     Find<Button>        (newGame, "Card/Content/StartButton"));
        Set(so, "newGameBackButton",   Find<Button>        (newGame, "BackButton"));
        Set(so, "nameValidationText",  Find<TMP_Text>      (newGame, "Card/Content/ValidationText"));

        // New game panel — portrait
        Set(so, "portraitButton",  Find<Button>   (newGame, "Card/Content/Columns/RightColumn/PortraitButton"));
        Set(so, "portraitDisplay", Find<RawImage> (newGame, "Card/Content/Columns/RightColumn/PortraitButton/PortraitImage"));
        Set(so, "portraitPickerPanel", portraitPicker.GetComponent<PortraitPickerPanel>());

        // Wire portrait library from Resources if it already exists
        var library = AssetDatabase.LoadAssetAtPath<PortraitLibrary>("Assets/Resources/PortraitLibrary.asset");
        if (library != null)
        {
            Set(so, "portraitLibrary", library);

            // Also wire the library into the picker panel script
            var pickerSO = new SerializedObject(portraitPicker.GetComponent<PortraitPickerPanel>());
            Set(pickerSO, "portraitLibrary", library);
            pickerSO.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("[Star Captain] PortraitLibrary not found at Assets/Resources/PortraitLibrary.asset. " +
                             "Run Star Captain -> Build Portrait Library, then re-run scene setup " +
                             "(or assign it manually in the Inspector).");
        }

        // Wire picker panel references (grid container, close button).
        // NOTE: the hierarchy no longer contains an "Inner" VerticalLayoutGroup wrapper —
        // BuildPortraitPickerPanel was rewritten to use absolute anchor-based positioning,
        // because Unity's ChildForceExpandHeight overrides a child's explicit flexibleHeight=0
        // and stretches every child equally (which was making the 2px Divider into a
        // giant cyan rectangle that filled half the popup). Paths are now flat under Container.
        var pickerScript = new SerializedObject(portraitPicker.GetComponent<PortraitPickerPanel>());
        Set(pickerScript, "gridContainer",
            FindRT(portraitPicker, "Container/ScrollView/Viewport/Content"));
        Set(pickerScript, "closeButton",
            Find<Button>(portraitPicker, "Container/Header/CloseButton"));
        pickerScript.ApplyModifiedProperties();

        // Load game panel
        Set(so, "saveSlotContainer",  Find<Transform>(load, "ScrollView/Viewport/Content"));
        Set(so, "loadGameBackButton", Find<Button>   (load, "BackButton"));
        Set(so, "noSavesText",        Find<TMP_Text> (load, "NoSavesText"));

        // Settings panel
        Set(so, "musicVolumeSlider",  Find<Slider>(settings, "Card/Options/MusicRow/MusicSlider"));
        Set(so, "sfxVolumeSlider",    Find<Slider>(settings, "Card/Options/SFXRow/SFXSlider"));
        Set(so, "settingsBackButton", Find<Button>(settings, "BackButton"));

        // Story interlude
        if (storyInterlude != null)
            Set(so, "storyInterludeController", storyInterlude);

        so.ApplyModifiedProperties();
    }

    // -----------------------------------------------------------------------
    // UI component factories
    // -----------------------------------------------------------------------
    static GameObject MakePanel(Transform parent, string name)
    {
        var go = MakeUIGO(name, parent);
        go.AddComponent<Image>().color = PanelBg;
        Stretch(go);
        return go;
    }

    static void MakePanelTitle(Transform parent, string text)
    {
        var go = MakeTMP(parent, "PanelTitle", text, 56, AccentCyan);
        PlaceRect(go, anchor(.5f, 1f), anchor(.5f, 1f), v2(0, -70), v2(800, 80));
        go.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        var rule = MakeImage(parent, "TitleRule", AccentCyan);
        PlaceRect(rule, anchor(.5f, 1f), anchor(.5f, 1f), v2(0, -115), v2(600, 2));
    }

    static GameObject MakeMenuBtn(Transform parent, string name, string label, bool danger = false)
    {
        var go  = MakeUIGO(name, parent);
        var img = go.AddComponent<Image>();
        img.color = danger ? BtnBack : BtnNormal;

        var btn    = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = danger ? BtnBack    : BtnNormal;
        colors.highlightedColor = danger ? BtnNormal  : BtnHighlight;
        colors.pressedColor     = BtnPressed;
        colors.fadeDuration     = 0.08f;
        btn.colors = colors;

        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 58;
        le.minHeight       = 58;

        var txtGO = MakeUIGO("Label", go.transform);
        Stretch(txtGO);
        var tmp       = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 22;
        tmp.color     = danger ? TextSubtle : TextWhite;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return go;
    }

    static void MakeBackBtn(Transform parent, string name)
    {
        var go  = MakeUIGO(name, parent);
        PlaceRect(go, anchor(0f, 0f), anchor(0f, 0f), v2(110, 36), v2(180, 50));
        go.AddComponent<Image>().color = BtnBack;
        var btn = go.AddComponent<Button>();
        var c   = btn.colors;
        c.normalColor      = BtnBack;
        c.highlightedColor = BtnNormal;
        c.pressedColor     = BtnPressed;
        btn.colors = c;

        var txtGO = MakeUIGO("Label", go.transform);
        Stretch(txtGO);
        var tmp       = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "← BACK";
        tmp.fontSize  = 19;
        tmp.color     = TextSubtle;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    static void MakeFieldLabel(Transform parent, string name, string text)
    {
        var go  = MakeUIGO(name, parent);
        var le  = go.AddComponent<LayoutElement>();
        le.preferredHeight = 26;
        le.minHeight       = 26;
        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 16;
        tmp.color     = TextSubtle;
        tmp.fontStyle = FontStyles.Bold;
    }

    static void MakeInputField(Transform parent, string name, string placeholder)
    {
        var go = MakeUIGO(name, parent);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 52;
        le.minHeight       = 52;
        go.AddComponent<Image>().color = InputBg;
        go.AddComponent<TMP_InputField>(); // MakeTMPInputField will find or add it
        MakeTMPInputField(go, placeholder, 22);
    }

    /// <summary>
    /// Larger input field with LayoutElement baked in — used by the New Game panel.
    /// </summary>
    static void MakeLargeInputField(Transform parent, string name, string placeholder)
    {
        var go = MakeUIGO(name, parent);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 88;
        le.minHeight       = 88;
        go.AddComponent<Image>().color = InputBg;
        MakeTMPInputField(go, placeholder, 34);
    }

    /// <summary>
    /// Adds TMP_InputField internals (Text Area, Placeholder, Text) to an existing GO.
    /// The GO must already have an Image and LayoutElement set up.
    /// </summary>
    static void MakeTMPInputField(GameObject go, string placeholder, int fontSize)
    {
        var field  = go.GetComponent<TMP_InputField>() ?? go.AddComponent<TMP_InputField>();

        var area   = MakeUIGO("Text Area", go.transform);
        var areaRT = area.GetComponent<RectTransform>();
        areaRT.anchorMin = new Vector2(0.03f, 0.1f);
        areaRT.anchorMax = new Vector2(0.97f, 0.9f);
        areaRT.sizeDelta = Vector2.zero;
        area.AddComponent<RectMask2D>();

        var phGO     = MakeUIGO("Placeholder", area.transform);
        Stretch(phGO);
        var ph       = phGO.AddComponent<TextMeshProUGUI>();
        ph.text      = placeholder;
        ph.fontSize  = fontSize;
        ph.color     = InputPh;
        ph.fontStyle = FontStyles.Italic;
        field.placeholder = ph;

        var inGO      = MakeUIGO("Text", area.transform);
        Stretch(inGO);
        var inTMP     = inGO.AddComponent<TextMeshProUGUI>();
        inTMP.fontSize = fontSize;
        inTMP.color    = TextWhite;
        field.textComponent = inTMP;
        field.textViewport  = areaRT;
    }

    static void MakeSliderRow(Transform parent, string rowName, string label, string sliderName, float defaultVal)
    {
        var row = MakeUIGO(rowName, parent);
        var le  = row.AddComponent<LayoutElement>();
        le.preferredHeight = 50;
        le.minHeight       = 50;
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing             = 24;
        hlg.childAlignment      = TextAnchor.MiddleLeft;
        hlg.childControlHeight  = true;
        hlg.childForceExpandHeight = true;

        var lbl   = MakeUIGO("Label", row.transform);
        var lblLE = lbl.AddComponent<LayoutElement>();
        lblLE.preferredWidth = 220;
        lblLE.minWidth       = 220;
        var lTMP       = lbl.AddComponent<TextMeshProUGUI>();
        lTMP.text      = label;
        lTMP.fontSize  = 22;
        lTMP.color     = TextSubtle;
        lTMP.alignment = TextAlignmentOptions.MidlineRight;

        var sGO = MakeUIGO(sliderName, row.transform);
        var sLE = sGO.AddComponent<LayoutElement>();
        sLE.preferredWidth = 280;
        sLE.minWidth       = 280;
        var slider    = sGO.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value    = defaultVal;
        sGO.AddComponent<Image>().color = InputBg;
    }

    static GameObject MakeTMPLayout(Transform parent, string name, string text, int size, Color color, int height)
    {
        var go  = MakeUIGO(name, parent);
        var le  = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight       = height;
        var tmp       = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        return go;
    }

    static void MakeSpacer(Transform parent, int height)
    {
        var go = MakeUIGO("Spacer", parent);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight       = height;
    }

    // -----------------------------------------------------------------------
    // Primitive helpers
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
        if (rt == null)
        {
            Debug.LogError($"[SceneSetup] No RectTransform on {go.name} — use MakeUIGO instead of MakeGO for UI.");
            return;
        }
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static void PlaceRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) { Debug.LogError($"[SceneSetup] No RectTransform on {go.name}"); return; }
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
    }

    static void Set(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning($"[SceneSetup] Property not found: {prop}");
    }

    static T Find<T>(GameObject root, string path) where T : Component
    {
        var t = root.transform.Find(path);
        if (t == null) { Debug.LogWarning($"[SceneSetup] Could not find '{path}' on {root.name}"); return null; }
        return t.GetComponent<T>();
    }

    /// <summary>Finds a RectTransform by path (for grid container wiring).</summary>
    static RectTransform FindRT(GameObject root, string path)
    {
        var t = root.transform.Find(path);
        if (t == null) { Debug.LogWarning($"[SceneSetup] Could not find '{path}' on {root.name}"); return null; }
        return t.GetComponent<RectTransform>();
    }

    static Vector2 anchor(float x, float y) => new Vector2(x, y);
    static Vector2 v2(float x, float y)     => new Vector2(x, y);
}
