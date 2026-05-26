using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populates the System View scene from live GameManager data.
///
/// ── Scene hierarchy expected ──────────────────────────────────────────────
///
///   SystemViewController          ← this MonoBehaviour (stretch child of Canvas)
///   ├─ Header
///   │  └─ SystemNameText          ← TMP_Text, centred, proper case
///   ├─ Body
///   │  └─ SystemMap               ← RectTransform, full width
///   │     ├─ StarGlow             ← Image (soft glow behind star)
///   │     └─ StarNode             ← Image (star sprite, centred)
///   ├─ NavBar
///   │  └─ ButtonContainer
///   │     ├─ SystemButton         ← Button (nav, placeholder)
///   │     ├─ GalaxyButton         ← Button (nav, placeholder)
///   │     ├─ ShipButton           ← Button (nav, placeholder)
///   │     └─ CrewButton           ← Button (nav, placeholder)
///   └─ POIDetailPanel             ← GameObject (SetActive false by default)
///      └─ Card
///         ├─ POIDetailNameText    ← TMP_Text
///         ├─ POIDetailTypeText    ← TMP_Text
///         ├─ POIDetailDescText    ← TMP_Text
///         └─ POIDetailCloseButton ← Button
///
/// ── Planet sprites ───────────────────────────────────────────────────────
///
///   Loaded at Start() from PlanetLibrary at Assets/Resources/PlanetLibrary.asset.
///   Each planet POI node displays the sprite matching poi.PlanetType + poi.PlanetVariant.
///   If the library is not built, nodes fall back to coloured dots.
///   Run "Star Captain → Build Planet Library" to populate the library.
/// </summary>
public class SystemViewController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector references — wired by GameSceneSetup editor script
    // -----------------------------------------------------------------------

    [Header("Header")]
    [SerializeField] private TMP_Text systemNameText;

    [Header("System Map")]
    [Tooltip("RectTransform of the map area. POI nodes are spawned as children here.")]
    [SerializeField] private RectTransform systemMapArea;
    [Tooltip("Image centred in the map that represents the star.")]
    [SerializeField] private Image starNode;

    [Header("Navigation")]
    [Tooltip("Bottom nav buttons — onClick listeners added when screens are implemented.")]
    [SerializeField] private Button systemNavButton;
    [SerializeField] private Button galaxyNavButton;
    [SerializeField] private Button shipNavButton;
    [SerializeField] private Button crewNavButton;

    [Header("POI Detail Panel")]
    [Tooltip("Root panel toggled by SetActive. The card + all detail fields live inside.")]
    [SerializeField] private GameObject poiDetailPanel;
    [SerializeField] private TMP_Text   poiDetailNameText;
    [SerializeField] private TMP_Text   poiDetailTypeText;
    [SerializeField] private TMP_Text   poiDetailDescText;
    [SerializeField] private Button     poiDetailCloseButton;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private StarSystem            _currentSystem;
    private List<PointOfInterest> _pois;
    private readonly List<GameObject> _poiNodes = new List<GameObject>();

    private PlanetLibrary _planetLibrary;

    // ── Fallback colours (used when PlanetLibrary has no sprite for a type) ─
    private static readonly Color ColourPlanetSolid   = new Color(0.30f, 0.75f, 0.40f, 1f);
    private static readonly Color ColourPlanetGaseous = new Color(0.90f, 0.65f, 0.25f, 1f);
    private static readonly Color ColourStation       = new Color(0.20f, 0.75f, 0.95f, 1f);
    private static readonly Color ColourAsteroid      = new Color(0.60f, 0.55f, 0.45f, 1f);
    private static readonly Color ColourDerelict      = new Color(0.80f, 0.35f, 0.20f, 1f);
    private static readonly Color ColourAnomaly       = new Color(0.70f, 0.30f, 0.90f, 1f);
    private static readonly Color ColourShip          = new Color(0.90f, 0.80f, 0.20f, 1f);
    private static readonly Color ColourDefault       = new Color(0.70f, 0.70f, 0.70f, 1f);

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        // Load the planet/star sprite library
        _planetLibrary = Resources.Load<PlanetLibrary>("PlanetLibrary");
        if (_planetLibrary == null || !_planetLibrary.IsValid)
            Debug.LogWarning("[SystemViewController] PlanetLibrary not found or invalid. " +
                             "Run 'Star Captain → Build Planet Library' in the editor.");

        // Wire listeners
        poiDetailCloseButton?.onClick.AddListener(HidePOIDetail);

        // Nav buttons — listeners added when screens are implemented
        // systemNavButton?.onClick.AddListener(OnSystemNav);
        // galaxyNavButton?.onClick.AddListener(OnGalaxyNav);
        // shipNavButton?.onClick.AddListener(OnShipNav);
        // crewNavButton?.onClick.AddListener(OnCrewNav);

        HidePOIDetail();

        var gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[SystemViewController] GameManager not found — showing placeholder.");
            ShowPlaceholder();
            return;
        }

        PopulateFromGameManager(gm);
    }

    // -----------------------------------------------------------------------
    // Data population
    // -----------------------------------------------------------------------

    private void PopulateFromGameManager(GameManager gm)
    {
        if (gm.CurrentSave == null)
        {
            Debug.LogWarning("[SystemViewController] No active save — showing placeholder.");
            ShowPlaceholder();
            return;
        }

        _currentSystem = gm.Database.StarSystems.Get(gm.CurrentSave.CurrentSystemId);
        if (_currentSystem == null)
        {
            Debug.LogWarning("[SystemViewController] CurrentSystemId points to a missing record.");
            ShowPlaceholder();
            return;
        }

        _pois = gm.Database.GetPOIsForSystem(_currentSystem.Id);

        RefreshHeader();
        RefreshStarVisual();
        SpawnPOINodes();
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private void RefreshHeader()
    {
        if (systemNameText)
            systemNameText.text = ToTitleCase(_currentSystem.Name);
    }

    // ── Star visual ──────────────────────────────────────────────────────────

    private void RefreshStarVisual()
    {
        if (starNode == null) return;

        var sprite = _planetLibrary?.GetStarSprite(_currentSystem.StarType, variant: 1);
        if (sprite != null)
        {
            starNode.sprite          = sprite;
            starNode.color           = Color.white;
            starNode.preserveAspect  = true;
            starNode.type            = Image.Type.Simple;
        }
        else
        {
            // Fallback: coloured dot
            starNode.sprite = null;
            starNode.color  = StarFallbackColor(_currentSystem.StarType);
        }
    }

    // ── POI nodes ────────────────────────────────────────────────────────────

    private void SpawnPOINodes()
    {
        if (systemMapArea == null) return;

        foreach (var n in _poiNodes)
            if (n != null) Destroy(n);
        _poiNodes.Clear();

        if (_pois == null) return;

        foreach (var poi in _pois)
            _poiNodes.Add(SpawnPOINode(poi));
    }

    private GameObject SpawnPOINode(PointOfInterest poi)
    {
        bool isPlanet = poi.POIType == Constants.POI.Types.Planet;

        // ── Size: gas giants > solid planets > everything else ────────────
        float nodeSize = isPlanet
            ? (poi.PlanetType.IsGaseous() ? 90f : 68f)
            : 52f;

        // ── Container: positioned by anchor within the map area ───────────
        var nodeGO = new GameObject(poi.Name, typeof(RectTransform));
        nodeGO.transform.SetParent(systemMapArea, false);

        var rt              = nodeGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(poi.SystemX, poi.SystemY);
        rt.anchorMax        = new Vector2(poi.SystemX, poi.SystemY);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(nodeSize, nodeSize);

        // ── Visual image ──────────────────────────────────────────────────
        var img = nodeGO.AddComponent<Image>();

        if (isPlanet)
        {
            var sprite = _planetLibrary?.GetPlanetSprite(poi.PlanetType, poi.PlanetVariant);
            if (sprite != null)
            {
                img.sprite         = sprite;
                img.color          = Color.white;
                img.preserveAspect = true;
                img.type           = Image.Type.Simple;
            }
            else
            {
                img.sprite = null;
                img.color  = poi.PlanetType.IsGaseous() ? ColourPlanetGaseous : ColourPlanetSolid;
            }
        }
        else
        {
            img.sprite = null;
            img.color  = NonPlanetColor(poi.POIType);
        }

        // ── Button ────────────────────────────────────────────────────────
        var btn    = nodeGO.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = img.color;
        colors.highlightedColor = Color.Lerp(img.color, Color.white, 0.35f);
        colors.pressedColor     = Color.Lerp(img.color, Color.black, 0.30f);
        btn.colors        = colors;
        btn.targetGraphic = img;

        var capturedPoi = poi;
        btn.onClick.AddListener(() => ShowPOIDetail(capturedPoi));

        // ── Name label (below the node) ───────────────────────────────────
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(nodeGO.transform, false);

        var labelRT              = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin        = new Vector2(0.5f, 0f);
        labelRT.anchorMax        = new Vector2(0.5f, 0f);
        labelRT.pivot            = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, -4f);
        labelRT.sizeDelta        = new Vector2(180f, 32f);

        var tmp                = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text               = poi.Name;
        tmp.fontSize           = 16f;
        tmp.color              = new Color(0.80f, 0.90f, 1.00f, 1f);
        tmp.alignment          = TextAlignmentOptions.Top;
        tmp.enableWordWrapping = false;
        tmp.overflowMode       = TextOverflowModes.Ellipsis;

        return nodeGO;
    }

    // -----------------------------------------------------------------------
    // POI Detail Panel
    // -----------------------------------------------------------------------

    private void ShowPOIDetail(PointOfInterest poi)
    {
        if (poiDetailPanel == null) return;

        if (poiDetailNameText) poiDetailNameText.text = poi.Name;
        if (poiDetailTypeText) poiDetailTypeText.text = POITypeLabel(poi);
        if (poiDetailDescText) poiDetailDescText.text = poi.Description ?? "";

        poiDetailPanel.SetActive(true);
    }

    private void HidePOIDetail()
    {
        if (poiDetailPanel != null) poiDetailPanel.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Placeholder (no GameManager or no active save)
    // -----------------------------------------------------------------------

    private void ShowPlaceholder()
    {
        if (systemNameText) systemNameText.text = "Sol System";

        if (starNode != null)
        {
            var sprite = _planetLibrary?.GetStarSprite(StarType.YellowDwarf, 1);
            if (sprite != null)
            {
                starNode.sprite         = sprite;
                starNode.color          = Color.white;
                starNode.preserveAspect = true;
            }
            else
            {
                starNode.color = StarFallbackColor(StarType.YellowDwarf);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Converts a string to Title Case (e.g. "SOL SYSTEM" → "Sol System").</summary>
    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
    }

    private static string POITypeLabel(PointOfInterest poi)
    {
        if (poi.POIType == Constants.POI.Types.Planet)
        {
            // Show the planet category name, e.g. "Terrestrial Planet" or "Gas Giant"
            string category = poi.PlanetType.IsGaseous() ? "Gas Giant" : $"{poi.PlanetType} Planet";
            return poi.IsHabitable ? $"{category} · Habitable" : category;
        }
        return poi.POIType switch
        {
            Constants.POI.Types.SpaceStation    => "Space Station",
            Constants.POI.Types.AsteroidField   => "Asteroid Field",
            Constants.POI.Types.DerelictShip    => "Derelict Vessel",
            Constants.POI.Types.DerelictStation => "Derelict Station",
            Constants.POI.Types.Anomaly         => "Anomaly",
            Constants.POI.Types.Ship            => "Ship",
            _                                   => poi.POIType,
        };
    }

    private static Color NonPlanetColor(string poiType) => poiType switch
    {
        Constants.POI.Types.SpaceStation    => ColourStation,
        Constants.POI.Types.AsteroidField   => ColourAsteroid,
        Constants.POI.Types.DerelictShip    => ColourDerelict,
        Constants.POI.Types.DerelictStation => ColourDerelict,
        Constants.POI.Types.Anomaly         => ColourAnomaly,
        Constants.POI.Types.Ship            => ColourShip,
        _                                   => ColourDefault,
    };

    private static Color StarFallbackColor(StarType t) => t switch
    {
        StarType.YellowDwarf => new Color(1.00f, 0.90f, 0.50f, 1f),
        StarType.RedDwarf    => new Color(0.90f, 0.30f, 0.20f, 1f),
        StarType.BlueGiant   => new Color(0.50f, 0.70f, 1.00f, 1f),
        _                    => new Color(1.00f, 1.00f, 0.80f, 1f),
    };
}
