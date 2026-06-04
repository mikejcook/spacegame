using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populates the Galaxy View with star system nodes loaded from the database
/// and handles the player ship navigating between systems.
///
/// ── Scene hierarchy expected ──────────────────────────────────────────────
///
///   GalaxyView                       ← this MonoBehaviour (starts inactive)
///   ├─ GalaxyMap                     ← RectTransform; has SystemMapZoomController
///   │  ├─ GalaxyBackground           ← RawImage; galaxy texture applied in Start()
///   │  └─ SystemNodesContainer       ← RectTransform; system nodes + ship spawned here
///   └─ SystemInfoPanel               ← CanvasGroup; shown when a system node is tapped
///      └─ Card
///         ├─ SystemInfoNameText
///         ├─ SystemInfoSubtitleText
///         ├─ SystemInfoDistanceText
///         ├─ SystemInfoPOIText
///         ├─ SystemInfoTravelButton
///         └─ SystemInfoCloseButton
///
/// ── System node layout ───────────────────────────────────────────────────
///
///   Each system uses anchor-based positioning (GalaxyX / GalaxyY as anchor
///   fractions within SystemNodesContainer) so nodes stay glued to the correct
///   galaxy-map position regardless of zoom or pan.  The ship uses the same
///   technique; its anchors are lerped during flight.
///
///   The current system is highlighted with a UIRingGraphic ring indicator.
///
/// ── Distance calculation ──────────────────────────────────────────────────
///
///   The galaxy map uses normalised coordinates (0–1). We treat the full
///   1.0-unit span as 100,000 light years (the Milky Way diameter), so:
///
///       distanceLY = Euclidean distance in normalised units × 100,000
///
///   All systems use this algorithm. The y-axis is weighted at 0.15 so that
///   left-right position dominates — systems on the left always read as farther.
/// </summary>
public class GalaxyViewController : MonoBehaviour
{
    // ── Serialised references (wired by GameSceneSetup) ───────────────────

    [SerializeField] private RawImage       galaxyBackground;
    [SerializeField] private RectTransform  systemNodesContainer;
    [SerializeField] private Sprite         shipSprite;
    [SerializeField] private Sprite         thrusterSprite;

    [Header("System Info Panel")]
    [SerializeField] private GameObject systemInfoPanel;
    [SerializeField] private TMP_Text   systemInfoNameText;
    [SerializeField] private TMP_Text   systemInfoSubtitleText;
    [SerializeField] private TMP_Text   systemInfoDistanceText;
    [SerializeField] private TMP_Text   systemInfoPOIText;
    [SerializeField] private Button     systemInfoTravelButton;
    [SerializeField] private Button     systemInfoCloseButton;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip   warpSoundClip;

    // ── Callback wired by SystemViewController ────────────────────────────

    /// <summary>
    /// Invoked when the ship arrives at a star system node.
    /// SystemViewController uses this to switch to that system's view.
    /// </summary>
    public System.Action<StarSystem> OnSystemSelected;

    /// <summary>Invoked when galaxy-map warp flight begins.</summary>
    public System.Action OnFlightStarted;

    /// <summary>Invoked when galaxy-map warp flight ends (arrival or instant-snap).</summary>
    public System.Action OnFlightEnded;

    // ── Private state ─────────────────────────────────────────────────────

    private PlanetLibrary            _planetLibrary;
    private bool _initialised     = false;
    private bool _zoomInitialised = false;
    private int  _currentSystemId = -1;

    private SystemMapZoomController _zoomController;
    private CanvasGroup             _infoPanelCG;
    private StarSystem              _pendingTravelTarget;

    // Scale factor: 1 normalised unit = 100,000 light years (Milky Way diameter)
    private const float LightYearsPerUnit = 100_000f;

    // Initial view: centre on the Sol / Alpha / Barnard's cluster.
    // Centroid of Sol (0.595, 0.448) + Alpha (0.628, 0.428) + Barnard's (0.658, 0.458).
    private const float InitialZoom    = 1.95f;
    private const float ClusterCentreX = 0.627f;
    private const float ClusterCentreY = 0.445f;

    // System nodes — parallel lists so we can look up a system by index
    private readonly List<GameObject>  _nodes   = new List<GameObject>();
    private readonly List<StarSystem>  _systems = new List<StarSystem>();

    // Ship
    private GameObject    _shipNodeGO;
    private RectTransform _shipRT;
    private Image         _thrusterImg;
    private StarSystem    _shipCurrentSystem;
    private bool          _shipFlying;
    private Coroutine     _flyCoroutine;

    // Node visual constants
    private const float NodeSize        = 22f;
    private const float CurrentNodeSize = 30f;
    private const float RingSize        = 30f;
    private const float LabelOffset     = 6f;    // gap between hit-area edge and label top
    private const float HitAreaSize     = 44f;   // tap target (Apple HIG minimum), invisible
    private const float ShipSize        = 20f;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        // Awake is called inline with SetActive(true), so this is assigned
        // before SetCurrentSystem() is called by ShowGalaxyView().
        _zoomController = GetComponentInChildren<SystemMapZoomController>();
    }

    private void Start()
    {
        if (_initialised) return;
        _initialised = true;

        _planetLibrary = Resources.Load<PlanetLibrary>("PlanetLibrary");
        if (_planetLibrary == null || !_planetLibrary.IsValid)
            Debug.LogWarning("[GalaxyViewController] PlanetLibrary not found or invalid — " +
                             "star nodes will fall back to coloured dots.");

        // Keep the info panel active so Shift button animators stay bound.
        // Hide it via CanvasGroup rather than SetActive(false).
        if (systemInfoPanel != null)
        {
            systemInfoPanel.SetActive(true);
            _infoPanelCG = systemInfoPanel.GetComponent<CanvasGroup>()
                           ?? systemInfoPanel.AddComponent<CanvasGroup>();
        }
        HideSystemInfo();

        if (systemInfoTravelButton != null)
            systemInfoTravelButton.onClick.AddListener(OnTravelButtonClicked);
        if (systemInfoCloseButton != null)
            systemInfoCloseButton.onClick.AddListener(HideSystemInfo);

        LoadGalaxyBackground();
        PopulateSystemNodes();
    }

    // Called by SystemViewController each time the galaxy panel is shown
    public void SetCurrentSystem(int systemId)
    {
        _currentSystemId = systemId;
        RefreshNodeVisuals();

        // On the very first open, zoom in on the Sol/Alpha/Proxima cluster.
        // We wait one frame so the RectTransform rect has been laid out.
        if (!_zoomInitialised && _zoomController != null)
        {
            _zoomInitialised = true;
            StartCoroutine(ApplyInitialZoomCoroutine());
        }
    }

    private IEnumerator ApplyInitialZoomCoroutine()
    {
        yield return null;  // let layout settle
        _zoomController.SetInitialView(InitialZoom, ClusterCentreX, ClusterCentreY);
    }

    // ── Background ────────────────────────────────────────────────────────

    private void LoadGalaxyBackground()
    {
        if (galaxyBackground == null) return;

        var tex = Resources.Load<Texture2D>("GalaxyBackground");
        if (tex != null)
        {
            galaxyBackground.texture = tex;
            galaxyBackground.color   = Color.white;
        }
        else
        {
            galaxyBackground.texture = null;
            galaxyBackground.color   = new Color(0.03f, 0.04f, 0.12f, 1f);
            Debug.LogWarning("[GalaxyViewController] GalaxyBackground texture not found. " +
                             "Run 'Star Captain → Generate Galaxy Texture' first.");
        }
    }

    // ── System node spawning ──────────────────────────────────────────────

    private void PopulateSystemNodes()
    {
        // Clean up previous nodes and ship
        if (_flyCoroutine != null) { StopCoroutine(_flyCoroutine); _flyCoroutine = null; }
        foreach (var n in _nodes) if (n != null) Destroy(n);
        if (_shipNodeGO != null)  { Destroy(_shipNodeGO); _shipNodeGO = null; _shipRT = null; }
        _nodes.Clear();
        _systems.Clear();
        _shipFlying = false;

        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null) return;

        int saveId  = gm.CurrentSave.Id;
        var systems = gm.Database.StarSystems
                        .Query()
                        .Where(s => s.SaveGameId == saveId && s.IsKnown)
                        .ToList();

        foreach (var system in systems)
        {
            _systems.Add(system);
            _nodes.Add(SpawnSystemNode(system));
        }

        // Spawn ship on the current system — reset position whenever nodes are rebuilt
        var currentSys = _systems.FirstOrDefault(s => s.Id == _currentSystemId);
        if (currentSys != null)
        {
            _shipCurrentSystem = currentSys;
            SpawnShip(currentSys.GalaxyX, currentSys.GalaxyY);
        }
    }

    private GameObject SpawnSystemNode(StarSystem system)
    {
        bool isCurrent = system.Id == _currentSystemId;
        float size     = isCurrent ? CurrentNodeSize : NodeSize;
        Color dotColor = isCurrent ? Color.white : StarColour(system.StarType, system.IsExplored);

        // ── Anchor to galaxy-map fraction ────────────────────────────────
        // The root GO uses a generous hit-area rect (HitAreaSize) so fingers
        // reliably land on the right node even when two systems are close.
        // The visible dot is a child sized to `size`, keeping the visual small.
        var go = new GameObject(system.Name, typeof(RectTransform));
        go.transform.SetParent(systemNodesContainer, false);

        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = new Vector2(system.GalaxyX, system.GalaxyY);
        rt.anchorMax    = new Vector2(system.GalaxyX, system.GalaxyY);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta    = new Vector2(HitAreaSize, HitAreaSize);

        // Transparent image fills the hit area — invisible but catches raycasts
        var hitImg   = go.AddComponent<Image>();
        hitImg.color = Color.clear;

        // ── Star sprite visual (child, visually sized) ────────────────────
        // Variant is derived from the system ID so each system consistently
        // gets the same sprite across sessions, while still varying across systems.
        int variant = (system.Id % 5) + 1;
        Sprite starSprite = _planetLibrary?.GetStarSprite(system.StarType, variant);

        var dotGO = new GameObject("Dot", typeof(RectTransform));
        dotGO.transform.SetParent(go.transform, false);
        var dotRT           = dotGO.GetComponent<RectTransform>();
        dotRT.anchorMin     = new Vector2(0.5f, 0.5f);
        dotRT.anchorMax     = new Vector2(0.5f, 0.5f);
        dotRT.pivot         = new Vector2(0.5f, 0.5f);
        dotRT.anchoredPosition = Vector2.zero;
        dotRT.sizeDelta     = new Vector2(size, size);
        var dotImg          = dotGO.AddComponent<Image>();
        if (starSprite != null)
        {
            dotImg.sprite         = starSprite;
            dotImg.preserveAspect = true;
            dotImg.type           = Image.Type.Simple;
            dotImg.color          = dotColor;   // tints unexplored systems darker
        }
        else
        {
            // Fallback: plain coloured dot if PlanetLibrary is unavailable
            dotImg.color = dotColor;
        }

        // ── Current-system ring ──────────────────────────────────────────
        if (isCurrent)
        {
            var ringGO = new GameObject("CurrentRing", typeof(RectTransform));
            ringGO.transform.SetParent(dotGO.transform, false);

            var ringRT          = ringGO.GetComponent<RectTransform>();
            ringRT.anchorMin    = new Vector2(0.5f, 0.5f);
            ringRT.anchorMax    = new Vector2(0.5f, 0.5f);
            ringRT.pivot        = new Vector2(0.5f, 0.5f);
            ringRT.anchoredPosition = Vector2.zero;
            ringRT.sizeDelta    = new Vector2(RingSize, RingSize);

            var ring       = ringGO.AddComponent<UIRingGraphic>();
            ring.color     = new Color(0.4f, 0.9f, 1.0f, 0.85f);
            ring.Thickness = 1.5f;
            ring.Segments  = 48;
        }

        // ── Label (child of root, positioned below dot) ───────────────────
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);

        var labelRT              = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin        = new Vector2(0.5f, 0f);
        labelRT.anchorMax        = new Vector2(0.5f, 0f);
        labelRT.pivot            = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, -(HitAreaSize * 0.5f + LabelOffset));
        labelRT.sizeDelta        = new Vector2(180f, 24f);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        bool isHandcrafted = HandcraftedNames.Contains(system.Name);
        tmp.text          = isHandcrafted ? system.Name : $"{system.Name} [{system.FtlTierRequired}]";
        tmp.fontSize      = 14f;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = isCurrent
                            ? new Color(0.85f, 1.0f, 1.0f, 1.0f)
                            : LabelColor(system);
        tmp.overflowMode  = TextOverflowModes.Overflow;
        // Prevent the label from blocking raycasts — only the root hit area should
        tmp.raycastTarget = false;

        // ── Button on the root (transparent hit area) ─────────────────────
        var btn    = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor     = Color.white;
        btn.colors              = colors;
        btn.targetGraphic       = hitImg;   // drives colour tint on the invisible area

        var captured = system;
        btn.onClick.AddListener(() => OnSystemNodeClicked(captured));

        return go;
    }

    // ── Visual refresh ────────────────────────────────────────────────────

    private void RefreshNodeVisuals()
    {
        if (_initialised) PopulateSystemNodes();
    }

    // ── Player ship ───────────────────────────────────────────────────────

    private void SpawnShip(float gx, float gy)
    {
        _shipNodeGO = new GameObject("PlayerShip", typeof(RectTransform));
        _shipNodeGO.transform.SetParent(systemNodesContainer, false);

        _shipRT           = _shipNodeGO.GetComponent<RectTransform>();
        _shipRT.anchorMin = new Vector2(gx, gy);
        _shipRT.anchorMax = new Vector2(gx, gy);
        _shipRT.pivot     = new Vector2(0.5f, 0.5f);
        _shipRT.anchoredPosition = Vector2.zero;
        _shipRT.sizeDelta = new Vector2(ShipSize, ShipSize);

        // ── Thruster child — rendered before (behind) the ship sprite ─────
        var thrusterGO = new GameObject("ThrusterImage", typeof(RectTransform));
        thrusterGO.transform.SetParent(_shipNodeGO.transform, false);
        var thrusterRT              = thrusterGO.GetComponent<RectTransform>();
        thrusterRT.anchorMin        = new Vector2(0.5f, 0.5f);
        thrusterRT.anchorMax        = new Vector2(0.5f, 0.5f);
        thrusterRT.pivot            = new Vector2(0.5f, 0.5f);
        thrusterRT.anchoredPosition = new Vector2(0f, -12f);  // rear of ship in local space
        thrusterRT.sizeDelta        = new Vector2(11f, 11f);

        _thrusterImg                = thrusterGO.AddComponent<Image>();
        _thrusterImg.sprite         = thrusterSprite;
        _thrusterImg.color          = Color.white;
        _thrusterImg.preserveAspect = true;
        _thrusterImg.type           = Image.Type.Simple;
        _thrusterImg.enabled        = false;

        // ── Ship sprite (rendered on top of thruster) ─────────────────────
        var shipGO = new GameObject("ShipImage", typeof(RectTransform));
        shipGO.transform.SetParent(_shipNodeGO.transform, false);
        var shipChildRT              = shipGO.GetComponent<RectTransform>();
        shipChildRT.anchorMin        = new Vector2(0.5f, 0.5f);
        shipChildRT.anchorMax        = new Vector2(0.5f, 0.5f);
        shipChildRT.pivot            = new Vector2(0.5f, 0.5f);
        shipChildRT.anchoredPosition = Vector2.zero;
        shipChildRT.sizeDelta        = new Vector2(ShipSize, ShipSize);

        var img = shipGO.AddComponent<Image>();
        if (shipSprite != null)
        {
            img.sprite         = shipSprite;
            img.color          = Color.white;
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;
        }
        else
        {
            img.sprite = null;
            img.color  = new Color(0.2f, 0.9f, 1.0f, 1f);   // cyan fallback
        }
    }

    // ── Interaction ───────────────────────────────────────────────────────

    private void OnSystemNodeClicked(StarSystem system)
    {
        if (_shipFlying) return;

        // Already at this system — open it immediately (no info popup needed)
        if (_shipCurrentSystem != null && system.Id == _shipCurrentSystem.Id)
        {
            OnSystemSelected?.Invoke(system);
            return;
        }

        ShowSystemInfo(system);
    }

    private void OnTravelButtonClicked()
    {
        if (_pendingTravelTarget == null || _shipFlying) return;

        HideSystemInfo();

        var target = _pendingTravelTarget;
        _pendingTravelTarget = null;

        float fromGX = _shipCurrentSystem?.GalaxyX ?? target.GalaxyX;
        float fromGY = _shipCurrentSystem?.GalaxyY ?? target.GalaxyY;

        if (_flyCoroutine != null) StopCoroutine(_flyCoroutine);
        _flyCoroutine = StartCoroutine(
            FlyShipToCoroutine(target, fromGX, fromGY, target.GalaxyX, target.GalaxyY));
    }

    // ── System info popup ─────────────────────────────────────────────────

    private void ShowSystemInfo(StarSystem system)
    {
        _pendingTravelTarget = system;

        if (systemInfoNameText != null)
            systemInfoNameText.text = system.Name;

        // Subtitle: star type (always shown)
        if (systemInfoSubtitleText != null)
        {
            string starTypeName = system.StarType switch
            {
                StarType.YellowDwarf => "Yellow Dwarf",
                StarType.RedDwarf    => "Red Dwarf",
                StarType.BlueGiant   => "Blue Giant",
                _                    => "Unknown Star"
            };
            systemInfoSubtitleText.text = starTypeName;
        }

        // FTL gate — check before showing distance or enabling travel
        int playerFtlTier = GameManager.Instance?.GetPlayerFtlTier() ?? 0;
        bool ftlLocked    = system.FtlTierRequired > playerFtlTier;

        // Distance / lock message
        if (systemInfoDistanceText != null)
        {
            if (ftlLocked)
            {
                string tierLabel = Constants.Ship.TierLabel((EquipmentTier)system.FtlTierRequired);
                systemInfoDistanceText.text  = $"FTL Drive {tierLabel} required";
                systemInfoDistanceText.color = new Color(1.00f, 0.35f, 0.20f, 1f);  // red-orange
            }
            else if (_shipCurrentSystem != null)
            {
                float distanceLY = CalculateDistanceLY(_shipCurrentSystem, system);
                systemInfoDistanceText.text  = FormatLightYears(distanceLY);
                systemInfoDistanceText.color = Color.white;
            }
            else
            {
                systemInfoDistanceText.text  = "Distance unknown";
                systemInfoDistanceText.color = Color.white;
            }
        }

        // POI summary from database (gated by scanner level)
        if (systemInfoPOIText != null)
            systemInfoPOIText.text = BuildPOISummary(system, GetPlayerScannerTier());

        // Show via CanvasGroup (Shift buttons stay active, animators stay bound)
        if (_infoPanelCG != null)
        {
            _infoPanelCG.alpha          = 1f;
            _infoPanelCG.blocksRaycasts = true;
            _infoPanelCG.interactable   = true;
        }

        // Snap travel button's Shift animator to rest state now that the panel
        // has been active since Start() — playables are already bound.
        if (systemInfoTravelButton != null)
        {
            systemInfoTravelButton.interactable = !ftlLocked;
            var anim = systemInfoTravelButton.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.Play("Normal", 0, 1.0f);
                anim.Update(0f);
            }
        }
        if (systemInfoCloseButton != null)
        {
            var anim = systemInfoCloseButton.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.Play("Normal", 0, 1.0f);
                anim.Update(0f);
            }
        }
    }

    private void HideSystemInfo()
    {
        if (_infoPanelCG != null)
        {
            _infoPanelCG.alpha          = 0f;
            _infoPanelCG.blocksRaycasts = false;
            _infoPanelCG.interactable   = false;
        }
    }

    // ── Distance helpers ──────────────────────────────────────────────────

    // Known distances from Sol displayed verbatim (scaled to produce "k" formatting).
    private static readonly System.Collections.Generic.Dictionary<string, float> KnownSolDistancesLY
        = new System.Collections.Generic.Dictionary<string, float>
    {
        { "Alpha Centauri", StarSystemGenerator.AlphaDistanceLY },
        { "Barnard's Star",  StarSystemGenerator.BarnardsDistanceLY },
    };

    private static float CalculateDistanceLY(StarSystem from, StarSystem to)
    {
        if (from.Name == "Sol" && KnownSolDistancesLY.TryGetValue(to.Name,   out float d)) return d;
        if (to.Name   == "Sol" && KnownSolDistancesLY.TryGetValue(from.Name, out float d2)) return d2;

        float dx = to.GalaxyX - from.GalaxyX;
        float dy = to.GalaxyY - from.GalaxyY;

        // The galaxy is landscape — left/right is the dominant distance axis.
        // Weighting y by 0.15 prevents vertical displacement from making a
        // right-side system appear farther than a left-side one.
        return Mathf.Sqrt(dx * dx + dy * dy * 0.15f) * LightYearsPerUnit;
    }

    private static string FormatLightYears(float ly)
    {
        if (ly < 10f)
            return $"{ly:F1} ly away";
        if (ly < 1_000f)
            return $"{Mathf.RoundToInt(ly)} ly away";
        if (ly < 10_000f)
            return $"{ly / 1_000f:F2}k ly away";
        return $"{Mathf.RoundToInt(ly / 1_000f)}k ly away";
    }

    /// Returns the tier (1–6) of the Scanner currently installed, or 0 if none.
    private static int GetPlayerScannerTier()
    {
        var gm = GameManager.Instance;
        if (gm?.PlayerShip == null || gm.Database == null) return 0;

        var slots = gm.PlayerShip.EquipmentSlots;
        if (!slots.TryGetValue(Constants.Ship.EquipmentSlots.Scanner, out int itemId) || itemId <= 0)
            return 0;

        var item = gm.Database.Equipment.Query()
                              .Where(e => e.Id == itemId)
                              .FirstOrDefault();
        return item != null ? (int)item.Tier : 0;
    }

    /// <summary>
    /// Builds the POI summary line gated by scanner level:
    ///   Level 1 → star type only (no POI line)
    ///   Level 2 → planets
    ///   Level 3+ → planets + stations, asteroids, derelicts, anomalies
    /// </summary>
    private static string BuildPOISummary(StarSystem system, int scannerTier)
    {
        // Level 1 (or no scanner): can't resolve individual bodies
        if (scannerTier <= 1) return "Scanner range insufficient for body detection";

        var gm = GameManager.Instance;
        if (gm?.Database == null) return "No data available";

        var pois = gm.Database.POIs.Query()
                     .Where(p => p.StarSystemId == system.Id)
                     .ToList();

        if (pois.Count == 0) return "No bodies detected";

        var parts = new System.Collections.Generic.List<string>();

        // Level 2+: planets
        int planets = pois.Count(p => p.POIType == Constants.POI.Types.Planet);
        if (planets > 0) parts.Add($"{planets} planet{(planets == 1 ? "" : "s")}");

        // Level 3+: other POIs
        if (scannerTier >= 3)
        {
            int stations  = pois.Count(p => p.POIType == Constants.POI.Types.SpaceStation);
            int derelicts = pois.Count(p =>
                p.POIType == Constants.POI.Types.DerelictShip ||
                p.POIType == Constants.POI.Types.DerelictStation);
            int asteroids = pois.Count(p => p.POIType == Constants.POI.Types.AsteroidField);
            int anomalies = pois.Count(p => p.POIType == Constants.POI.Types.Anomaly);

            if (stations  > 0) parts.Add($"{stations} station{(stations  == 1 ? "" : "s")}");
            if (asteroids > 0) parts.Add($"{asteroids} asteroid field{(asteroids == 1 ? "" : "s")}");
            if (derelicts > 0) parts.Add($"{derelicts} derelict{(derelicts == 1 ? "" : "s")}");
            if (anomalies > 0) parts.Add($"{anomalies} anomal{(anomalies  == 1 ? "y" : "ies")}");
        }

        return parts.Count > 0 ? string.Join("  ·  ", parts) : "No bodies detected";
    }

    private IEnumerator FlyShipToCoroutine(
        StarSystem target,
        float fromGX, float fromGY,
        float toGX,   float toGY)
    {
        _shipFlying = true;
        if (_thrusterImg != null) _thrusterImg.enabled = true;
        OnFlightStarted?.Invoke();

        // Calculate angle so the ship can rotate before/during warp-up
        Canvas.ForceUpdateCanvases();
        var cRect      = systemNodesContainer.rect;
        float screenDX = (toGX - fromGX) * cRect.width;
        float screenDY = (toGY - fromGY) * cRect.height;
        float distance = Mathf.Sqrt(screenDX * screenDX + screenDY * screenDY);

        if (distance < 0.5f)
        {
            _shipCurrentSystem = target;
            _shipFlying        = false;
            if (_thrusterImg != null) _thrusterImg.enabled = false;
            OnFlightEnded?.Invoke();
            OnSystemSelected?.Invoke(target);
            yield break;
        }

        // DGB sprites face UP (+Y). Formula for a +Y-facing sprite: Atan2(-dx, dy).
        float targetAngleDeg = Mathf.Atan2(-screenDX, screenDY) * Mathf.Rad2Deg;
        float startAngleDeg  = _shipRT != null ? _shipRT.localEulerAngles.z : 0f;
        float deltaAngle     = Mathf.DeltaAngle(startAngleDeg, targetAngleDeg);

        // ── Phase 1: warp-up ─────────────────────────────────────────────
        // Play sound; ship turns and accelerates (ease-in²) over the clip's
        // duration, covering warpFraction of the total journey.  By the time
        // the sound ends the ship is at full cruise speed.
        const float warpFraction = 0.25f;   // fraction of distance covered during warp-up
        float warpDuration = (sfxSource != null && warpSoundClip != null)
                             ? warpSoundClip.length : 0f;

        if (sfxSource != null && warpSoundClip != null)
            sfxSource.PlayOneShot(warpSoundClip);

        float elapsed = 0f;
        while (elapsed < warpDuration)
        {
            elapsed += Time.deltaTime;
            float t    = Mathf.Clamp01(elapsed / warpDuration);
            float posT = t * t;                     // ease-in quadratic → starts slow, builds speed

            if (_shipRT != null)
            {
                float curGX = Mathf.Lerp(fromGX, toGX, posT * warpFraction);
                float curGY = Mathf.Lerp(fromGY, toGY, posT * warpFraction);
                _shipRT.anchorMin = new Vector2(curGX, curGY);
                _shipRT.anchorMax = new Vector2(curGX, curGY);

                // Rotate to face destination in the first 30% of the warp phase
                float rotT = Mathf.Clamp01(t / 0.3f);
                _shipRT.localEulerAngles = new Vector3(0f, 0f, startAngleDeg + rotT * deltaAngle);
            }

            yield return null;
        }

        // ── Phase 2: cruise ──────────────────────────────────────────────
        // Speed at end of ease-in² is 2·warpFraction/warpDuration (fraction/sec).
        // Cruise the remaining distance at that constant speed.
        // If there was no sound, fall back to the original smooth-step flight.
        float remainingFraction = 1f - warpFraction;

        if (warpDuration > 0f)
        {
            float fullSpeed     = 2f * warpFraction / warpDuration;  // fraction per second
            float cruiseDuration = remainingFraction / fullSpeed;

            elapsed = 0f;
            while (elapsed < cruiseDuration)
            {
                elapsed += Time.deltaTime;
                float t       = Mathf.Clamp01(elapsed / cruiseDuration);
                float fraction = warpFraction + t * remainingFraction;

                if (_shipRT != null)
                {
                    float curGX = Mathf.Lerp(fromGX, toGX, fraction);
                    float curGY = Mathf.Lerp(fromGY, toGY, fraction);
                    _shipRT.anchorMin = new Vector2(curGX, curGY);
                    _shipRT.anchorMax = new Vector2(curGX, curGY);
                }

                yield return null;
            }
        }
        else
        {
            // No sound clip — original smooth-step behaviour
            float duration = Mathf.Clamp(distance / 280f, 0.5f, 4f);
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t    = Mathf.Clamp01(elapsed / duration);
                float posT = t * t * (3f - 2f * t);

                if (_shipRT != null)
                {
                    _shipRT.anchorMin        = new Vector2(Mathf.Lerp(fromGX, toGX, posT),
                                                           Mathf.Lerp(fromGY, toGY, posT));
                    _shipRT.anchorMax        = _shipRT.anchorMin;
                    float rotT               = Mathf.Clamp01(t / 0.25f);
                    _shipRT.localEulerAngles = new Vector3(0f, 0f, startAngleDeg + rotT * deltaAngle);
                }

                yield return null;
            }
        }

        // Snap to destination
        if (_shipRT != null)
        {
            _shipRT.anchorMin = new Vector2(toGX, toGY);
            _shipRT.anchorMax = new Vector2(toGX, toGY);
        }

        _shipCurrentSystem = target;
        _shipFlying        = false;
        _flyCoroutine      = null;
        if (_thrusterImg != null) _thrusterImg.enabled = false;

        OnFlightEnded?.Invoke();
        OnSystemSelected?.Invoke(target);
    }

    // ── Colour helpers ────────────────────────────────────────────────────

    // The three hand-crafted systems are always white.
    // All other systems are coloured from green (tier 1) to red (tier 6).
    private static readonly System.Collections.Generic.HashSet<string> HandcraftedNames
        = new System.Collections.Generic.HashSet<string>
          { "Sol", "Alpha Centauri", "Barnard's Star" };

    private static Color LabelColor(StarSystem system)
    {
        if (HandcraftedNames.Contains(system.Name))
            return new Color(1.00f, 1.00f, 1.00f, 0.90f);   // white

        return system.FtlTierRequired switch
        {
            1 => new Color(0.30f, 1.00f, 0.30f, 0.90f),   // green
            2 => new Color(0.70f, 1.00f, 0.20f, 0.90f),   // yellow-green
            3 => new Color(1.00f, 1.00f, 0.20f, 0.90f),   // yellow
            4 => new Color(1.00f, 0.65f, 0.10f, 0.90f),   // orange
            5 => new Color(1.00f, 0.35f, 0.10f, 0.90f),   // orange-red
            _ => new Color(1.00f, 0.15f, 0.15f, 0.90f),   // red  (tier 6+)
        };
    }

    private static Color StarColour(StarType type, bool explored)
    {
        Color c = type switch
        {
            StarType.YellowDwarf => new Color(1.00f, 0.92f, 0.55f, 1f),
            StarType.RedDwarf    => new Color(1.00f, 0.40f, 0.30f, 1f),
            StarType.BlueGiant   => new Color(0.60f, 0.80f, 1.00f, 1f),
            _                    => new Color(0.90f, 0.90f, 0.90f, 1f),
        };
        if (!explored) c = Color.Lerp(c, new Color(0.3f, 0.3f, 0.4f, 1f), 0.55f);
        return c;
    }
}
