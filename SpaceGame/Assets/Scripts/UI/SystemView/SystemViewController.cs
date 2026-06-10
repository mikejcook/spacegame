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

    [Header("Background")]
    [Tooltip("Full-screen RawImage. A random starfield texture from Resources/Backgrounds is applied at Start().")]
    [SerializeField] private RawImage starfieldBackground;

    [Header("Header")]
    [SerializeField] private TMP_Text    systemNameText;
    [SerializeField] private TMP_Text    daysText;
    [SerializeField] private TMP_Text    salvageText;
    [SerializeField] private TMP_Text    fuelText;
    [SerializeField] private TMP_Text    loyaltyText;
    [SerializeField] private GameObject  salvageWidgetGO;
    [SerializeField] private GameObject  daysWidgetGO;
    [SerializeField] private GameObject  fuelWidgetGO;
    [SerializeField] private GameObject  loyaltyWidgetGO;
    [SerializeField] private CanvasGroup combatHeaderStatsGroup;

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

    [Header("Player Ship")]
    [Tooltip("Ship sprite — blue_corvette_1 from DGB Spaceships, wired by GameSceneSetup.")]
    [SerializeField] private Sprite shipSprite;
    [Tooltip("Thruster flame sprite shown while the ship is flying — wired by GameSceneSetup.")]
    [SerializeField] private Sprite thrusterSprite;

    [Header("POI Sprites")]
    [Tooltip("Sprite used for Space Station POI nodes — wired by GameSceneSetup.")]
    [SerializeField] private Sprite stationSprite;
    [Tooltip("Sprite used for Derelict Ship POI nodes — extracted from FrigateCorsair prefab by GameSceneSetup.")]
    [SerializeField] private Sprite derelictSprite;
    [Tooltip("Planetary ring overlay sprite — wired by GameSceneSetup. Applied to all gaseous planets.")]
    [SerializeField] private Sprite planetRingsSprite;

    [Header("Galaxy View")]
    [Tooltip("Root GalaxyView panel — toggled when the Galaxy nav button is pressed.")]
    [SerializeField] private GameObject          galaxyViewPanel;
    [SerializeField] private GalaxyViewController galaxyViewController;

    [Header("Ship View")]
    [SerializeField] private GameObject      shipViewPanel;
    [SerializeField] private ShipViewController shipViewController;

    [Header("Crew View")]
    [SerializeField] private GameObject      crewViewPanel;
    [SerializeField] private CrewViewController crewViewController;

    [Header("Combat View")]
    [Tooltip("Root CombatView panel — sibling of Body, covers full area below header including NavBar.")]
    [SerializeField] private GameObject           combatViewPanel;
    [Tooltip("CanvasGroup on CombatView — used for show/hide so Shift button Animators inside stay continuously bound.")]
    [SerializeField] private CanvasGroup          combatViewCanvasGroup;
    [SerializeField] private CombatViewController combatViewController;

    [Header("Combat — Debug")]
    [Tooltip("Temporary button in the header that toggles the combat view. Remove once combat is triggered by gameplay events.")]
    [SerializeField] private Button combatDebugButton;
    [Tooltip("Enters combat with a randomly generated enemy fleet (1–3 ships, random class/colour).")]
    [SerializeField] private Button randomCombatButton;

    [Header("Nav Bar")]
    [Tooltip("CanvasGroup on the NavBar — hidden during combat so it doesn't show through the CombatView.")]
    [SerializeField] private CanvasGroup navBarCanvasGroup;

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip   sublightEngineClip;

    [Header("POI Detail Panel")]
    [Tooltip("Root panel toggled by SetActive. The card + all detail fields live inside.")]
    [SerializeField] private GameObject poiDetailPanel;
    [SerializeField] private TMP_Text   poiDetailNameText;
    [SerializeField] private TMP_Text   poiDetailTypeText;
    [SerializeField] private TMP_Text   poiDetailDescText;
    [SerializeField] private Button     poiDetailCloseButton;
    [SerializeField] private Button     poiDetailNavigateButton;
    [SerializeField] private TMP_Text   poiDetailScannerText;

    [Header("Crew Recruitment")]
    [Tooltip("Recruitment overlay — shown automatically when the ship docks at a functioning space station.")]
    [SerializeField] private RecruitmentController recruitmentController;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private StarSystem            _currentSystem;
    private List<PointOfInterest> _pois;
    private readonly List<PointOfInterest> _sortedPois = new List<PointOfInterest>();
    private readonly List<GameObject>      _poiNodes   = new List<GameObject>();
    private readonly List<GameObject>      _orbitRings = new List<GameObject>();

    // ── POI detail state ──────────────────────────────────────────────────────
    private PointOfInterest _detailPoi; // POI currently shown in the detail panel

    // ── Ship state ────────────────────────────────────────────────────────────
    private GameObject      _shipNodeGO;
    private RectTransform   _shipRT;
    private Image           _shipBackdropImg;
    private RectTransform   _shipBackdropRT;
    private Image           _thrusterImg;
    private PointOfInterest _shipCurrentPoi;
    private bool            _shipFlying;
    private Coroutine       _flyCoroutine;

    private PlanetLibrary            _planetLibrary;
    private SystemMapZoomController  _systemMapZoomController;

    // ── Combat state ──────────────────────────────────────────────────────────
    private bool _inCombat;

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
        // Pick a random starfield background
        ApplyRandomStarfield();

        // Load the planet/star sprite library
        _planetLibrary = Resources.Load<PlanetLibrary>("PlanetLibrary");
        if (_planetLibrary == null || !_planetLibrary.IsValid)
            Debug.LogWarning("[SystemViewController] PlanetLibrary not found or invalid. " +
                             "Run 'Star Captain → Build Planet Library' in the editor.");

        // Wire listeners
        poiDetailCloseButton?.onClick.AddListener(HidePOIDetail);

        // Zoom controller lives on the SystemMap GO
        _systemMapZoomController = systemMapArea?.GetComponent<SystemMapZoomController>();

        // Nav buttons
        systemNavButton?.onClick.AddListener(ShowSystemView);
        galaxyNavButton?.onClick.AddListener(ShowGalaxyView);
        shipNavButton?.onClick.AddListener(ShowShipView);
        crewNavButton?.onClick.AddListener(ShowCrewView);

        // Debug combat toggle — temporary, removed once combat has real triggers.
        // Default: one red medium enemy (variant 0).
        combatDebugButton?.onClick.AddListener(() => ToggleCombatView(new[]
        {
            new EnemyShipConfig { color = DGBShipColor.Green, shipClass = DGBShipClass.Fighter, variant = 1 },
        }));

        randomCombatButton?.onClick.AddListener(() => StartOrRestartCombat(BuildRandomEnemies()));

        // Galaxy view → arriving at a system switches the System View to it.
        // Galaxy view → disable/re-enable nav buttons during warp flight.
        if (galaxyViewController != null)
        {
            galaxyViewController.OnSystemSelected = OnGalaxySystemSelected;
            galaxyViewController.OnFlightStarted  = () => SetNavButtonsInteractable(false);
            galaxyViewController.OnFlightEnded    = () => SetNavButtonsInteractable(true);
        }

        // ── Neutralise the Shift MainButton Animator on the close button ──
        //
        // The Shift Animator's Normal "state" is a transition clip whose t=0
        // visually looks highlighted; combined with Unity's deferred-playable-
        // binding race on re-enable, every attempt to seek past t=0 on show
        // (Play, Rebind+Play+Update, CanvasGroup writes) has been silently
        // overwritten by the Animator's first sample. We take the Animator
        // out of the loop entirely and drive the hover / press visuals
        // ourselves:
        //   1. Disable the Animator component so it can never sample again.
        //   2. Switch the Button's transition mode to None so Selectable does
        //      not try to drive triggers on a disabled Animator.
        //   3. Attach ShiftMainButtonPointerVisuals — writes the
        //      Normal / Highlighted / Pressed child CanvasGroup alphas
        //      directly from IPointer* events, with no Animator involved.
        //
        // We can do all of this while the panel is still inactive (its
        // initial state from GameSceneSetup) — GetComponent / AddComponent
        // work on inactive GameObjects, and the disabled Animator never
        // initialises when the panel later activates.
        if (poiDetailCloseButton != null)
        {
            var anim = poiDetailCloseButton.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            poiDetailCloseButton.transition = Selectable.Transition.None;

            if (poiDetailCloseButton.GetComponent<ShiftMainButtonPointerVisuals>() == null)
                poiDetailCloseButton.gameObject.AddComponent<ShiftMainButtonPointerVisuals>();
        }

        if (poiDetailNavigateButton != null)
        {
            var anim = poiDetailNavigateButton.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;
            poiDetailNavigateButton.transition = Selectable.Transition.None;
            if (poiDetailNavigateButton.GetComponent<ShiftMainButtonPointerVisuals>() == null)
                poiDetailNavigateButton.gameObject.AddComponent<ShiftMainButtonPointerVisuals>();
            poiDetailNavigateButton.onClick.AddListener(OnNavigateClicked);
        }

        HidePOIDetail();

        var gm = GameManager.Instance;

#if UNITY_EDITOR
        // Debug convenience: opening GameScene directly skips MainMenu entirely,
        // so GameManager never gets created. Spin one up on the fly so the scene
        // is testable without any manual setup steps.
        if (gm == null)
        {
            Debug.LogWarning("[SystemViewController] [Debug] GameManager not found — " +
                             "creating one for editor testing.");
            // DatabaseManager must be a child BEFORE GameManager.Awake fires
            // (Awake calls InitializeManagers → GetComponentInChildren<DatabaseManager>).
            var gmGO = new UnityEngine.GameObject("GameManager");
            var dbGO = new UnityEngine.GameObject("DatabaseManager");
            dbGO.transform.SetParent(gmGO.transform);
            dbGO.AddComponent<DatabaseManager>();          // child exists first
            gmGO.AddComponent<GameManager>();              // Awake fires here, finds DB
            gm = GameManager.Instance;
        }

        if (gm != null && gm.CurrentSave == null)
        {
            var portraitLib     = Resources.Load<PortraitLibrary>("PortraitLibrary");
            var debugPortrait   = portraitLib != null ? portraitLib.RandomFileName() : string.Empty;
            Debug.LogWarning("[SystemViewController] [Debug] No active game detected — " +
                             $"auto-creating debug game (captain: Player, ship: Horizon, portrait: {debugPortrait}).");
            gm.PrepareNewGame("Player", "Horizon", debugPortrait);

            // Debug: skip first-launch UI — auto-apply recommended captain skills.
            gm.AcknowledgeFirstLaunch();
            var debugCaptain = gm.Database?.Characters.Get(gm.CurrentSave.CaptainId);
            if (debugCaptain != null && debugCaptain.AvailableSkillPoints > 0)
            {
                // Skills is a computed property (get deserialises, set serialises) —
                // mutating the returned dict does nothing. Assign the whole dict back.
                var skills = debugCaptain.Skills;
                skills[Constants.Skills.Command]    = 2;
                skills[Constants.Skills.Perception] = 1;
                debugCaptain.Skills               = skills;
                debugCaptain.AvailableSkillPoints = 0;
                try { gm.Database?.Characters.Update(debugCaptain); } catch { }
            }
        }
#endif

        if (gm == null)
        {
            Debug.LogWarning("[SystemViewController] GameManager not found — showing placeholder.");
            ShowPlaceholder();
            return;
        }

        PopulateFromGameManager(gm);

        // First launch: take the player straight to the crew view with the captain
        // level-up popup so they spend their 3 starting skill points before playing.
        if (gm.IsFirstLaunch)
        {
            gm.AcknowledgeFirstLaunch();
            StartCoroutine(ShowFirstLaunchFlow());
        }
    }

    /// <summary>
    /// After a new game begins, routes the player to the crew view and immediately
    /// opens the locked captain level-up panel. On confirm, switches to system view.
    /// </summary>
    private System.Collections.IEnumerator ShowFirstLaunchFlow()
    {
        ShowCrewView();
        yield return null;   // one frame so the crew panel is fully visible

        var gm      = GameManager.Instance;
        var captain = gm?.CurrentSave != null
            ? gm.Database?.Characters.Get(gm.CurrentSave.CaptainId)
            : null;

        if (captain == null || captain.AvailableSkillPoints <= 0)
        {
            Debug.LogWarning("[SystemViewController] First-launch captain has no skill points — skipping to system view.");
            ShowSystemView();
            yield break;
        }

        if (crewViewController == null)
        {
            Debug.LogWarning("[SystemViewController] crewViewController not wired — skipping first-launch level-up.");
            ShowSystemView();
            yield break;
        }

        crewViewController.OpenCaptainLevelUpForFirstLaunch(captain, () => ShowSystemView());
    }

    // -----------------------------------------------------------------------
    // Starfield background
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads all Texture2D assets from Resources/Backgrounds and applies a
    /// randomly chosen one to the starfield RawImage. Called once in Start().
    /// </summary>
    private void ApplyRandomStarfield()
    {
        if (starfieldBackground == null) return;

        var textures = Resources.LoadAll<Texture2D>("Backgrounds");
        if (textures == null || textures.Length == 0)
        {
            Debug.LogWarning("[SystemViewController] No starfield textures found in Resources/Backgrounds. " +
                             "Ensure starfield PNGs are present in that folder.");
            return;
        }

        var tex = textures[Random.Range(0, textures.Length)];
        starfieldBackground.texture = tex;
        starfieldBackground.color   = Color.white;
    }

    // -----------------------------------------------------------------------
    // Data population
    // -----------------------------------------------------------------------

    private void PopulateFromGameManager(GameManager gm)
    {
        if (gm.CurrentSave == null)
        {
            Debug.LogError("[SystemViewController] CurrentSave is null — PrepareNewGame likely failed. Check GameManager logs.");
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
        ZoomToShip();
    }

    // ── Header ───────────────────────────────────────────────────────────────

    private void RefreshHeader()
    {
        if (systemNameText)
            systemNameText.text = ToTitleCase(_currentSystem.Name);
        RefreshSalvage();
        RefreshDays();
        RefreshFuel();
        RefreshLoyalty();
    }

    public void RefreshSalvage()
    {
        if (salvageText == null) return;
        var save = GameManager.Instance?.CurrentSave;
        salvageText.text = save != null ? save.Salvage.ToString("N0") : "0";
    }

    public void RefreshDays()
    {
        if (daysText == null) return;
        var save = GameManager.Instance?.CurrentSave;
        daysText.text = save != null ? Mathf.FloorToInt(save.DaysPassed).ToString() : "0";
    }

    public void RefreshFuel()
    {
        if (fuelText == null) return;
        var save = GameManager.Instance?.CurrentSave;
        fuelText.text = save != null ? save.Fuel.ToString() : "0";
    }

    public void RefreshLoyalty()
    {
        if (loyaltyText == null) return;
        var save = GameManager.Instance?.CurrentSave;
        loyaltyText.text = save != null ? save.CrewLoyalty.ToString() : "0";
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

    private void SpawnPOINodes(bool enteringNewSystem = false)
    {
        if (systemMapArea == null) return;

        // ── Clean up previous nodes, rings and ship ───────────────────────
        if (_flyCoroutine != null) { StopCoroutine(_flyCoroutine); _flyCoroutine = null; }
        foreach (var node in _poiNodes)   if (node != null) Destroy(node);
        foreach (var ring in _orbitRings) if (ring != null) Destroy(ring);
        if (_shipNodeGO != null) Destroy(_shipNodeGO);
        _poiNodes.Clear();
        _orbitRings.Clear();
        _sortedPois.Clear();
        _shipNodeGO      = null;
        _shipRT          = null;
        _shipBackdropImg = null;
        _shipBackdropRT  = null;
        _shipCurrentPoi  = null;
        _shipFlying      = false;

        if (_pois == null) return;

        // ── Minimum orbit clearance (star radius + breathing room) ───────
        float minOrbitPx = 65f + 40f;   // 130 px star diameter + 40 px gap

        // ── Find each POI's normalised orbital radius ─────────────────────
        // Data is stored as  SystemX/Y = 0.5 ± r*cos/sin(angle)
        int n = _pois.Count;
        var normRadii = new float[n];
        for (int i = 0; i < n; i++)
        {
            float dx = _pois[i].SystemX - 0.5f;
            float dy = _pois[i].SystemY - 0.5f;
            normRadii[i] = Mathf.Sqrt(dx * dx + dy * dy);
        }

        // ── Sort POIs by orbit radius ─────────────────────────────────────
        var indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;
        System.Array.Sort(indices, (a, b) => normRadii[a].CompareTo(normRadii[b]));

        // ── Assign pixel orbit radii preserving proportional spacing ──────
        // With panning available, orbits no longer need to fit within the
        // viewport — outer planets simply require a pan or zoom-out to see.
        // A fixed px-per-normR scale lets genuine spacing differences (e.g. the
        // wide Mars→Jupiter asteroid-belt gap in Sol) show up directly.
        //
        // Co-orbital POIs (|normR diff| < CoOrbitThresh) share the same ring
        // radius — this correctly handles Earth + Earth Station.
        const float OrbitScalePx  = 1000f;  // canvas px per normalised orbit unit
        const float MinGapPx      = 30f;    // minimum px between distinct rings
        const float CoOrbitThresh = 0.01f;  // normR diff treated as the same orbit

        var orbitPxArr = new float[n];
        for (int i = 0; i < n; i++)
            orbitPxArr[i] = minOrbitPx + normRadii[indices[i]] * OrbitScalePx;

        for (int i = 1; i < n; i++)
        {
            bool coOrbital = Mathf.Abs(normRadii[indices[i]] - normRadii[indices[i - 1]]) < CoOrbitThresh;
            orbitPxArr[i] = coOrbital
                ? orbitPxArr[i - 1]                                     // share the ring
                : Mathf.Max(orbitPxArr[i], orbitPxArr[i - 1] + MinGapPx);
        }

        // ── Spawn orbit rings first (rendered behind planet nodes) ────────
        for (int i = 0; i < n; i++)
            _orbitRings.Add(SpawnOrbitRing(orbitPxArr[i]));

        // ── Spawn POI nodes on top ────────────────────────────────────────
        for (int i = 0; i < n; i++)
        {
            var   poi   = _pois[indices[i]];
            float size  = PlanetDisplaySize(poi);

            float dx    = poi.SystemX - 0.5f;
            float dy    = poi.SystemY - 0.5f;
            float angle = Mathf.Atan2(dy, dx);

            _sortedPois.Add(poi);
            _poiNodes.Add(SpawnPOINode(poi, orbitPxArr[i], angle, size));
        }

        // ── Spawn ship on top of everything ──────────────────────────────
        var startPoi = FindBestStartPoi();
        if (startPoi != null)
        {
            int startIdx = _sortedPois.IndexOf(startPoi);
            if (startIdx >= 0)
            {
                var destPos = _poiNodes[startIdx].GetComponent<RectTransform>().anchoredPosition;

                if (enteringNewSystem)
                {
                    // Spawn beyond the outermost orbit, opposite the first destination POI,
                    // as if the ship just dropped out of warp at the system's edge.
                    Vector2 dir      = destPos.magnitude > 0.01f ? destPos.normalized : Vector2.up;
                    float   outerOrbit = n > 0 ? orbitPxArr[n - 1] : minOrbitPx;
                    float   edgeDist   = outerOrbit + 120f;
                    SpawnShip(-dir * edgeDist, null);

                    // Point the ship toward the star (center of the map).
                    // dir already points inward. DGB sprites face up (+Y): Atan2(-dx, dy).
                    float angleDeg = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
                    if (_shipRT != null)
                        _shipRT.localEulerAngles = new Vector3(0f, 0f, angleDeg);
                }
                else
                {
                    SpawnShip(destPos, startPoi);
                }
            }
        }
    }

    /// <summary>
    /// Spawns a subtle circular orbit-ring indicator centred on the star.
    /// </summary>
    private GameObject SpawnOrbitRing(float orbitPx)
    {
        var go = new GameObject("OrbitRing", typeof(RectTransform));
        go.transform.SetParent(systemMapArea, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(orbitPx * 2f, orbitPx * 2f);

        var ring      = go.AddComponent<UIRingGraphic>();
        ring.color     = new Color(0.45f, 0.75f, 1.00f, 0.18f);   // subtle cyan tint
        ring.Thickness = 1.5f;
        ring.Segments  = 90;

        return go;
    }

    /// <summary>
    /// Returns the display diameter (canvas px) for a POI node.
    ///
    /// Planets: Sol planets use hand-tuned sizes reflecting real relative diameters;
    /// procedural planets get a type-tier base (tiny/small/medium/large/giant) with
    /// ±18 % variation seeded from the POI's database Id so size is stable per save.
    /// Non-planets (stations, derelicts, etc.) are a fixed 48 px.
    /// </summary>
    private static float PlanetDisplaySize(PointOfInterest poi)
    {
        if (poi.POIType != Constants.POI.Types.Planet) return 48f;

        // Sol hand-tuned sizes — loosely reflect real relative planetary diameters,
        // compressed so gas giants aren't absurdly large.
        switch (poi.Name)
        {
            case "Mercury": return 30f;
            case "Venus":   return 60f;
            case "Earth":   return 64f;
            case "Mars":    return 44f;
            case "Jupiter": return 110f;
            case "Saturn":  return  95f;
            case "Uranus":  return  76f;
            case "Neptune": return  74f;
        }

        // Alpha Centauri hand-tuned sizes (4 planets)
        switch (poi.Name)
        {
            case "Alpha Centauri I":   return 46f;   // magma — smallish
            case "Alpha Centauri II":  return 56f;   // arid
            case "Alpha Centauri III": return 66f;   // terrestrial — Earth-like
            case "Alpha Centauri IV":  return 58f;   // frozen — mid-size
        }

        // Procedural: type-tier base ± 18 % variation, seeded from poi.Id
        float baseSize = poi.PlanetType.BaseDisplaySize();
        uint  hash     = unchecked((uint)poi.Id * 2654435761u);  // Knuth multiplicative hash
        float t        = (hash & 0xFFFFu) / 65535f;              // 0..1, uniform-ish
        return baseSize * Mathf.Lerp(0.82f, 1.18f, t);
    }

    /// <summary>
    /// Returns true for planet types that should display a ring overlay.
    /// All gas giants get rings; they're large enough to make them worthwhile.
    /// </summary>
    /// GaseousOrange maps to Jupiter, which has rings in reality but isn't visually
    /// associated with them. All other gas giants display rings.
    private static bool HasRings(PlanetType type) =>
        type.IsGaseous() && type != PlanetType.GaseousOrange;

    /// <summary>
    /// Spawns a single ring-layer Image (RingBack or the masked container for RingFront)
    /// as a child of systemMapArea at the same position as the planet node.
    ///
    /// For RingBack: isFront=false — full ring image behind the planet.
    /// For RingFront: isFront=true — masked container showing only the bottom half,
    /// so the ring appears to pass in front of the planet's lower hemisphere.
    /// </summary>
    private GameObject SpawnRingLayer(string name, Vector2 anchoredPos,
                                      float ringW, float ringH, bool isFront)
    {
        if (isFront)
        {
            // Masked container — clips to bottom half of ring rect.
            // Container is shifted down by ringH/4 so its top edge sits at the
            // planet centre; the child image is offset up so its centre also
            // sits at the planet centre.
            var maskGO = new GameObject(name, typeof(RectTransform));
            maskGO.transform.SetParent(systemMapArea, false);
            var maskRT = maskGO.GetComponent<RectTransform>();
            maskRT.anchorMin        = new Vector2(0.5f, 0.5f);
            maskRT.anchorMax        = new Vector2(0.5f, 0.5f);
            maskRT.pivot            = new Vector2(0.5f, 0.5f);
            maskRT.anchoredPosition = new Vector2(anchoredPos.x, anchoredPos.y - ringH * 0.25f);
            maskRT.sizeDelta        = new Vector2(ringW, ringH * 0.5f);
            maskGO.AddComponent<RectMask2D>();

            var imgGO = new GameObject("RingImg", typeof(RectTransform));
            imgGO.transform.SetParent(maskGO.transform, false);
            var imgRT = imgGO.GetComponent<RectTransform>();
            imgRT.anchorMin        = new Vector2(0.5f, 0.5f);
            imgRT.anchorMax        = new Vector2(0.5f, 0.5f);
            imgRT.pivot            = new Vector2(0.5f, 0.5f);
            imgRT.anchoredPosition = new Vector2(0f, ringH * 0.25f);
            imgRT.sizeDelta        = new Vector2(ringW, ringH);
            var img = imgGO.AddComponent<Image>();
            img.sprite         = planetRingsSprite;
            img.color          = Color.white;
            img.preserveAspect = false;
            img.type           = Image.Type.Simple;
            img.raycastTarget  = false;

            return maskGO;
        }
        else
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(systemMapArea, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = new Vector2(ringW, ringH);
            var img = go.AddComponent<Image>();
            img.sprite         = planetRingsSprite;
            img.color          = Color.white;
            img.preserveAspect = false;
            img.type           = Image.Type.Simple;
            img.raycastTarget  = false;
            return go;
        }
    }

    private GameObject SpawnPOINode(PointOfInterest poi,
                                    float orbitPx, float angle, float nodeSize)
    {
        var anchoredPos = new Vector2(orbitPx * Mathf.Cos(angle),
                                      orbitPx * Mathf.Sin(angle));

        // ── Planet rings — back layer (rendered before the planet) ─────────
        bool isRinged = poi.POIType == Constants.POI.Types.Planet
                     && HasRings(poi.PlanetType)
                     && planetRingsSprite != null;

        if (isRinged)
        {
            float aspect = planetRingsSprite.rect.height > 0
                ? planetRingsSprite.rect.width / planetRingsSprite.rect.height
                : 2.8f;
            float ringW = nodeSize * 2.2f;
            float ringH = ringW / aspect;
            var ringBack = SpawnRingLayer(poi.Name + "_RingBack", anchoredPos,
                                          ringW, ringH, isFront: false);
            _poiNodes.Add(ringBack);
        }

        // ── Container: anchored to map centre, offset along the orbit ─────
        var nodeGO = new GameObject(poi.Name, typeof(RectTransform));
        nodeGO.transform.SetParent(systemMapArea, false);

        var rt = nodeGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = new Vector2(nodeSize, nodeSize);

        bool isPlanet = poi.POIType == Constants.POI.Types.Planet;

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
        else if (poi.POIType == Constants.POI.Types.SpaceStation && stationSprite != null)
        {
            img.sprite         = stationSprite;
            img.color          = Color.white;
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;
        }
        else if (poi.POIType == Constants.POI.Types.DerelictStation && stationSprite != null)
        {
            img.sprite         = stationSprite;
            img.color          = new Color(0.82f, 0.82f, 0.90f, 1f); // light grey-blue: dead, no power
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;

            // Same stable tumble treatment as derelict ships
            float tumbleAngle = ((poi.Id * 137.508f) % 360f);
            rt.localEulerAngles = new Vector3(0f, 0f, tumbleAngle);
        }
        else if (poi.POIType == Constants.POI.Types.DerelictShip && derelictSprite != null)
        {
            img.sprite         = derelictSprite;
            img.color          = new Color(0.82f, 0.82f, 0.90f, 1f); // light grey-blue: dead, no power
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;

            // Stable random tumble angle seeded from the POI's id so it's the same every visit
            float tumbleAngle = ((poi.Id * 137.508f) % 360f);
            rt.localEulerAngles = new Vector3(0f, 0f, tumbleAngle);
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
        btn.onClick.AddListener(() => OnPOIClicked(capturedPoi));

        // ── Planet rings — front layer (rendered after/above the planet) ───
        if (isRinged)
        {
            float aspect = planetRingsSprite.rect.height > 0
                ? planetRingsSprite.rect.width / planetRingsSprite.rect.height
                : 2.8f;
            float ringW = nodeSize * 2.2f;
            float ringH = ringW / aspect;
            var ringFront = SpawnRingLayer(poi.Name + "_RingFront", anchoredPos,
                                           ringW, ringH, isFront: true);
            _poiNodes.Add(ringFront);
        }

        return nodeGO;
    }

    // -----------------------------------------------------------------------
    // Player ship
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates the ship UI node as the last child of systemMapArea (renders on top).
    ///
    /// Hierarchy:
    ///   PlayerShip          RectTransform + Button (positioning root)
    ///     ShipBackdrop      Image — dark semi-transparent circle for contrast
    ///     ThrusterImage     Image — engine flame, hidden until flying
    ///     ShipImage         Image — the actual ship sprite (renders on top of thruster)
    ///
    /// Children share the root's rotation, so the thruster stays behind the
    /// engine as the ship turns. ThrusterImage sits below ShipImage in the
    /// hierarchy so it renders underneath.
    /// </summary>
    private void SpawnShip(Vector2 startPos, PointOfInterest startPoi)
    {
        // ── Container (positioning root + button) ────────────────────────────
        _shipNodeGO = new GameObject("PlayerShip", typeof(RectTransform));
        _shipNodeGO.transform.SetParent(systemMapArea, false);

        _shipRT                  = _shipNodeGO.GetComponent<RectTransform>();
        _shipRT.anchorMin        = new Vector2(0.5f, 0.5f);
        _shipRT.anchorMax        = new Vector2(0.5f, 0.5f);
        _shipRT.pivot            = new Vector2(0.5f, 0.5f);
        _shipRT.sizeDelta        = new Vector2(50f, 50f);
        _shipRT.anchoredPosition = startPos;

        // ── Backdrop: dark circle slightly larger than the ship sprite ───────
        var backdropGO = new GameObject("ShipBackdrop", typeof(RectTransform));
        backdropGO.transform.SetParent(_shipNodeGO.transform, false);
        var backdropRT             = backdropGO.GetComponent<RectTransform>();
        backdropRT.anchorMin       = new Vector2(0.5f, 0.5f);
        backdropRT.anchorMax       = new Vector2(0.5f, 0.5f);
        backdropRT.pivot           = new Vector2(0.5f, 0.5f);
        backdropRT.anchoredPosition = Vector2.zero;
        backdropRT.sizeDelta       = new Vector2(62f, 62f);   // 12 px larger than sprite on each axis

        _shipBackdropRT  = backdropRT;
        _shipBackdropImg        = backdropGO.AddComponent<Image>();
        _shipBackdropImg.sprite = null;                        // solid circle via default Unity sprite
        _shipBackdropImg.color  = new Color(0f, 0f, 0f, 0.55f);

        // ── Thruster flame — rendered before (behind) the ship sprite ────────
        var thrusterGO = new GameObject("ThrusterImage", typeof(RectTransform));
        thrusterGO.transform.SetParent(_shipNodeGO.transform, false);
        var thrusterRT             = thrusterGO.GetComponent<RectTransform>();
        thrusterRT.anchorMin       = new Vector2(0.5f, 0.5f);
        thrusterRT.anchorMax       = new Vector2(0.5f, 0.5f);
        thrusterRT.pivot           = new Vector2(0.5f, 0.5f);
        // Offset below centre — sits at the ship's engine exhaust (rear = -Y in local space)
        thrusterRT.anchoredPosition = new Vector2(0f, -30f);
        thrusterRT.sizeDelta        = new Vector2(28f, 28f);

        _thrusterImg           = thrusterGO.AddComponent<Image>();
        _thrusterImg.sprite    = thrusterSprite;   // null-safe; falls back to white rect
        _thrusterImg.color     = Color.white;
        _thrusterImg.preserveAspect = true;
        _thrusterImg.type      = Image.Type.Simple;
        _thrusterImg.enabled   = false;            // hidden until flying

        // ── Ship sprite ───────────────────────────────────────────────────────
        var spriteGO = new GameObject("ShipImage", typeof(RectTransform));
        spriteGO.transform.SetParent(_shipNodeGO.transform, false);
        var spriteRT             = spriteGO.GetComponent<RectTransform>();
        spriteRT.anchorMin       = new Vector2(0.5f, 0.5f);
        spriteRT.anchorMax       = new Vector2(0.5f, 0.5f);
        spriteRT.pivot           = new Vector2(0.5f, 0.5f);
        spriteRT.anchoredPosition = Vector2.zero;
        spriteRT.sizeDelta       = new Vector2(50f, 50f);

        var img = spriteGO.AddComponent<Image>();
        if (shipSprite != null)
        {
            img.sprite         = shipSprite;
            img.color          = Color.white;
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;
        }
        else
        {
            // Fallback: bright cyan if sprite not wired
            img.sprite = null;
            img.color  = new Color(0.2f, 0.9f, 1.0f, 1f);
        }

        // ── Button on the container — targetGraphic points to the sprite ─────
        var shipBtn            = _shipNodeGO.AddComponent<Button>();
        shipBtn.transition     = Selectable.Transition.None;
        shipBtn.targetGraphic  = img;
        shipBtn.onClick.AddListener(OnShipClicked);

        _shipCurrentPoi = startPoi;
    }

    private void SetShipBackdropVisible(bool visible)
    {
        if (_shipBackdropImg == null) return;

        if (visible && _shipBackdropRT != null)
        {
            // Size the backdrop to cover whichever is larger: the ship sprite
            // or the POI node beneath it, with a small margin either way.
            float poiSize  = _shipCurrentPoi != null ? PlanetDisplaySize(_shipCurrentPoi) : 0f;
            float diameter = Mathf.Max(62f, poiSize + 12f);
            _shipBackdropRT.sizeDelta = new Vector2(diameter, diameter);
        }

        var c = _shipBackdropImg.color;
        c.a   = visible ? 0.55f : 0f;
        _shipBackdropImg.color = c;
    }

    private void OnShipClicked()
    {
        if (_shipFlying || _shipCurrentPoi == null) return;
        OnArrivedAtOrTappedPOI(_shipCurrentPoi);
    }

    /// <summary>
    /// Called when any POI is tapped. If the ship is already there, handle the
    /// arrival action immediately. If it's flying, ignore the tap. Otherwise fly there first.
    /// </summary>
    private void OnPOIClicked(PointOfInterest poi)
    {
        if (_shipFlying) return;
        if (poi == _shipCurrentPoi)
            OnArrivedAtOrTappedPOI(poi);
        else
            ShowPOIDetail(poi);
    }

    /// <summary>
    /// Central handler for "player interacts with the POI their ship is currently at".
    /// Space stations open recruitment directly; everything else shows the info popup.
    /// </summary>
    private void OnArrivedAtOrTappedPOI(PointOfInterest poi)
    {
        if (poi.POIType == Constants.POI.Types.SpaceStation && crewViewController != null)
            StartCoroutine(ShowRecruitmentAfterDelay(poi, GetCaptainLevel()));
        else
            ShowPOIDetail(poi);
    }

    private void OnNavigateClicked()
    {
        var poi = _detailPoi;
        HidePOIDetail();
        if (poi == null || _shipFlying || poi == _shipCurrentPoi) return;

        int targetIdx = _sortedPois.IndexOf(poi);
        if (targetIdx < 0 || _shipRT == null) return;

        Vector2 fromPos = _shipRT.anchoredPosition;
        Vector2 toPos   = _poiNodes[targetIdx].GetComponent<RectTransform>().anchoredPosition;

        if (_flyCoroutine != null) StopCoroutine(_flyCoroutine);
        _flyCoroutine = StartCoroutine(FlyShipToCoroutine(poi, fromPos, toPos));
    }

    /// <summary>
    /// Flies the ship from <paramref name="fromPos"/> to <paramref name="toPos"/>.
    /// Phase 1: engine sound plays while the ship turns and accelerates (ease-in).
    /// Phase 2: constant cruise speed for the remainder of the journey.
    /// </summary>
    private System.Collections.IEnumerator FlyShipToCoroutine(
        PointOfInterest target, Vector2 fromPos, Vector2 toPos)
    {
        // Capture departure POI before _shipCurrentPoi is reassigned on arrival.
        var fromPoi = _shipCurrentPoi;

        _shipFlying = true;
        if (_thrusterImg != null) _thrusterImg.enabled = true;
        SetShipBackdropVisible(false);
        SetNavButtonsInteractable(false);

        Vector2 dir      = toPos - fromPos;
        float   distance = dir.magnitude;
        if (distance < 0.1f)
        {
            _shipCurrentPoi = target;
            _shipFlying     = false;
            if (_thrusterImg != null) _thrusterImg.enabled = false;
            SetShipBackdropVisible(true);
            SetNavButtonsInteractable(true);
            GameManager.Instance?.AddInSystemTravelTime(fromPoi, target);
            RefreshDays();
            MarkVisited(target);
            OnArrivedAtOrTappedPOI(target);
            yield break;
        }

        // DGB sprites face UP (+Y). Formula for a +Y-facing sprite: Atan2(-dir.x, dir.y).
        float targetAngleDeg = Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;
        float startAngleDeg  = _shipRT.localEulerAngles.z;
        float deltaAngle     = Mathf.DeltaAngle(startAngleDeg, targetAngleDeg);

        // ── Phase 1: engine spool-up ─────────────────────────────────────
        // Sound plays while the ship turns and accelerates (ease-in quadratic),
        // covering sublightFraction of the total distance.  Phase duration is
        // the clip length, capped so it can't exceed the full flight time.
        const float sublightFraction = 0.30f;
        float totalDuration  = Mathf.Clamp(distance / 80f, 0.6f, 5.0f);
        float clipLength     = (sfxSource != null && sublightEngineClip != null)
                               ? sublightEngineClip.length : 0f;
        float accelDuration  = Mathf.Min(clipLength, totalDuration * 0.6f);

        if (sfxSource != null && sublightEngineClip != null)
            sfxSource.PlayOneShot(sublightEngineClip);

        float elapsed = 0f;
        while (elapsed < accelDuration)
        {
            elapsed += Time.deltaTime;
            float t    = Mathf.Clamp01(elapsed / accelDuration);
            float posT = t * t;   // ease-in quadratic: starts slow, builds speed

            _shipRT.anchoredPosition = Vector2.Lerp(fromPos, toPos, posT * sublightFraction);

            // Rotate to heading in the first 30 % of the accel phase
            float rotT = Mathf.Clamp01(t / 0.3f);
            _shipRT.localEulerAngles = new Vector3(0f, 0f, startAngleDeg + rotT * deltaAngle);

            yield return null;
        }

        // ── Phase 2: cruise ──────────────────────────────────────────────
        // Speed at end of ease-in² = 2·sublightFraction/accelDuration (fraction/sec).
        // Travel the remaining distance at that constant speed.
        // If there was no clip, fall back to the original smooth-step for the whole journey.
        if (accelDuration > 0f)
        {
            float fullSpeed      = 2f * sublightFraction / accelDuration;
            float remainFraction = 1f - sublightFraction;
            float cruiseDuration = remainFraction / fullSpeed;

            elapsed = 0f;
            while (elapsed < cruiseDuration)
            {
                elapsed += Time.deltaTime;
                float t        = Mathf.Clamp01(elapsed / cruiseDuration);
                float fraction = sublightFraction + t * remainFraction;
                _shipRT.anchoredPosition = Vector2.Lerp(fromPos, toPos, fraction);
                yield return null;
            }
        }
        else
        {
            // No sound clip — smooth-step fallback
            elapsed = 0f;
            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                float t    = Mathf.Clamp01(elapsed / totalDuration);
                float posT = t * t * (3f - 2f * t);
                _shipRT.anchoredPosition = Vector2.Lerp(fromPos, toPos, posT);

                float rotT = Mathf.Clamp01(t / 0.25f);
                _shipRT.localEulerAngles = new Vector3(0f, 0f,
                    startAngleDeg + rotT * deltaAngle);

                yield return null;
            }
        }

        _shipRT.anchoredPosition = toPos;
        _shipCurrentPoi          = target;
        _shipFlying              = false;
        _flyCoroutine            = null;
        if (_thrusterImg != null) _thrusterImg.enabled = false;

        SetShipBackdropVisible(true);
        SetNavButtonsInteractable(true);
        GameManager.Instance?.AddInSystemTravelTime(fromPoi, target);
        RefreshDays();
        MarkVisited(target);
        OnArrivedAtOrTappedPOI(target);
    }

    private int GetCaptainLevel()
    {
        var gm = GameManager.Instance;
        if (gm?.Database == null || gm.CurrentSave == null) return 1;
        try
        {
            var crew = gm.Database.GetCrewForSave(gm.CurrentSave.Id);
            foreach (var c in crew)
                if (c.IsPlayerCaptain) return Mathf.Max(1, c.Level);
        }
        catch { /* DB not ready */ }
        return 1;
    }

    /// <summary>
    /// One-frame delay so the POI detail panel fully collapses before the crew
    /// view opens in recruitment mode — avoids same-frame input bleed.
    /// </summary>
    private System.Collections.IEnumerator ShowRecruitmentAfterDelay(PointOfInterest poi, int captainLevel)
    {
        yield return null;
        ShowCrewView();
        if (crewViewController != null)
        {
            // Wire header callback so the CVC can change the nav text when recruitment ends
            crewViewController.OnHeaderTextChanged = text =>
            {
                if (systemNameText != null) systemNameText.text = text;
            };
            crewViewController.StartRecruitmentMode(poi, captainLevel);
            // systemNameText already set by StartRecruitmentMode via the callback above
        }
    }

    /// <summary>
    /// Picks the best POI to start the ship at. Priority: named "Earth" →
    /// first habitable planet → first planet → first POI.
    /// </summary>
    private PointOfInterest FindBestStartPoi()
    {
        if (_sortedPois == null || _sortedPois.Count == 0) return null;

        // 1. Exact name match for "Earth"
        foreach (var p in _sortedPois)
            if (p.Name == "Earth") return p;

        // 2. First habitable planet
        foreach (var p in _sortedPois)
            if (p.POIType == Constants.POI.Types.Planet && p.IsHabitable) return p;

        // 3. First planet of any kind
        foreach (var p in _sortedPois)
            if (p.POIType == Constants.POI.Types.Planet) return p;

        // 4. Anything
        return _sortedPois[0];
    }

    // -----------------------------------------------------------------------
    // View switching (System ↔ Galaxy)
    // -----------------------------------------------------------------------

    private void ShowSystemView()
    {
        HidePOIDetail();
        if (systemMapArea   != null) systemMapArea.gameObject.SetActive(true);
        if (galaxyViewPanel != null) galaxyViewPanel.SetActive(false);
        if (shipViewPanel   != null) shipViewPanel.SetActive(false);
        if (crewViewPanel   != null) crewViewPanel.SetActive(false);
        RefreshHeader();
        ZoomToShip();
    }

    /// <summary>
    /// Centres the system map on the player's ship at a comfortable zoom level.
    /// Safe to call even before the ship has spawned (no-op in that case).
    /// </summary>
    private void ZoomToShip()
    {
        if (_systemMapZoomController == null || _shipRT == null) return;
        _systemMapZoomController.FocusOn(_shipRT.anchoredPosition, 2.0f);
    }

    private void ShowGalaxyView()
    {
        // Close any open POI popup before switching away
        HidePOIDetail();

        // Hide the system map so only the galaxy panel is visible
        if (systemMapArea != null) systemMapArea.gameObject.SetActive(false);

        if (galaxyViewPanel != null)
        {
            galaxyViewPanel.SetActive(true);
            if (galaxyViewController != null && _currentSystem != null)
                galaxyViewController.SetCurrentSystem(_currentSystem.Id);
        }
        if (shipViewPanel != null) shipViewPanel.SetActive(false);
        if (crewViewPanel != null) crewViewPanel.SetActive(false);

        if (systemNameText != null) systemNameText.text = "Galaxy";
    }

    /// <summary>
    /// Invoked by GalaxyViewController when the player ship arrives at a star
    /// system node. Repoints the System View at the new system — updating the
    /// persisted CurrentSystemId, reloading its POIs, refreshing the header and
    /// star, and switching back to the system map.
    /// </summary>
    private void OnGalaxySystemSelected(StarSystem system)
    {
        if (system == null) return;

        // Same system the ship was already in — just return to the map.
        if (_currentSystem != null && system.Id == _currentSystem.Id)
        {
            ShowSystemView();
            return;
        }

        var gm = GameManager.Instance;
        if (gm?.CurrentSave != null)
        {
            gm.CurrentSave.CurrentSystemId = system.Id;
            gm.SaveGame();
        }

        _currentSystem = system;
        _pois = gm?.Database.GetPOIsForSystem(system.Id);

        RefreshHeader();
        RefreshStarVisual();
        SpawnPOINodes(enteringNewSystem: true);

        ShowSystemView();
    }

    private void SetNavButtonsInteractable(bool interactable)
    {
        if (systemNavButton != null) systemNavButton.interactable = interactable;
        if (galaxyNavButton != null) galaxyNavButton.interactable = interactable;
        if (shipNavButton   != null) shipNavButton.interactable   = interactable;
        if (crewNavButton   != null) crewNavButton.interactable   = interactable;
    }

    private void ShowShipView()
    {
        HidePOIDetail();
        if (systemMapArea   != null) systemMapArea.gameObject.SetActive(false);
        if (galaxyViewPanel != null) galaxyViewPanel.SetActive(false);
        if (crewViewPanel   != null) crewViewPanel.SetActive(false);
        if (systemNameText  != null) systemNameText.text = "Ship";
        if (shipViewPanel   != null) shipViewPanel.SetActive(true);
        shipViewController?.Refresh();
    }

    private void ShowCrewView()
    {
        HidePOIDetail();
        if (systemMapArea   != null) systemMapArea.gameObject.SetActive(false);
        if (galaxyViewPanel != null) galaxyViewPanel.SetActive(false);
        if (shipViewPanel   != null) shipViewPanel.SetActive(false);
        if (systemNameText  != null) systemNameText.text = "Crew";
        if (crewViewPanel   != null) crewViewPanel.SetActive(true);
        // If we were in recruitment mode, the panel may already be active so OnEnable
        // won't fire — explicitly exit recruitment mode to repopulate with actual crew.
        crewViewController?.ExitRecruitmentMode();
    }

    // -----------------------------------------------------------------------
    // Combat View
    // -----------------------------------------------------------------------

    /// <summary>
    /// Switches to the Space Battle view: hides NavBar, hides all other body
    /// panels, activates CombatView, and updates the debug button label.
    /// </summary>
    /// <summary>
    /// Enters combat. Pass 1–3 EnemyShipConfig entries to populate the enemy fleet.
    /// </summary>
    public void ShowCombatView(EnemyShipConfig[] enemies = null)
    {
        if (_inCombat) return;
        _inCombat = true;

        HidePOIDetail();

        // Hide all body panels so they don't render through the CombatView
        // (CombatView has no solid background — the starfield shows through).
        if (systemMapArea   != null) systemMapArea.gameObject.SetActive(false);
        if (galaxyViewPanel != null) galaxyViewPanel.SetActive(false);
        if (shipViewPanel   != null) shipViewPanel.SetActive(false);
        if (crewViewPanel   != null) crewViewPanel.SetActive(false);

        if (combatViewCanvasGroup != null)
        {
            combatViewCanvasGroup.alpha          = 1f;
            combatViewCanvasGroup.blocksRaycasts = true;
            combatViewCanvasGroup.interactable   = true;
        }

        if (navBarCanvasGroup != null)
        {
            navBarCanvasGroup.alpha          = 0f;
            navBarCanvasGroup.blocksRaycasts = false;
            navBarCanvasGroup.interactable   = false;
        }

        // Hide non-combat header elements; show combat stats
        if (systemNameText != null) systemNameText.gameObject.SetActive(false);
        if (salvageWidgetGO != null) salvageWidgetGO.SetActive(false);
        if (daysWidgetGO != null) daysWidgetGO.SetActive(false);
        if (fuelWidgetGO != null) fuelWidgetGO.SetActive(false);
        if (loyaltyWidgetGO != null) loyaltyWidgetGO.SetActive(false);
        if (combatHeaderStatsGroup != null)
        {
            combatHeaderStatsGroup.alpha          = 1f;
            combatHeaderStatsGroup.blocksRaycasts = true;
            combatHeaderStatsGroup.interactable   = true;
        }

        combatViewController?.OnCombatEnter();

        // Build D20 combat state from current game data before handing off to the controller.
        if (combatViewController != null && enemies != null)
        {
            var gm = GameManager.Instance;
            if (gm != null)
            {
                var db   = gm.Database;
                var ship = gm.PlayerShip;

                // Read installed weapon/defense equipment from the ship's slots.
                EquipmentItem GetSlotItem(string slotName)
                {
                    var slots = ship?.EquipmentSlots;
                    if (slots == null) return null;
                    if (!slots.TryGetValue(slotName, out int id) || id <= 0) return null;
                    return db?.Equipment.Get(id);
                }

                var beamItem    = GetSlotItem(Constants.Ship.EquipmentSlots.BeamWeapons);
                var torpedoItem = GetSlotItem(Constants.Ship.EquipmentSlots.Torpedoes);
                var shieldItem  = GetSlotItem(Constants.Ship.EquipmentSlots.Shields);
                var armorItem   = GetSlotItem(Constants.Ship.EquipmentSlots.Armor);

                var saveId = gm.CurrentSave?.Id ?? 0;

                var combatState = CombatState.Begin(
                    ship:       ship,
                    captain:    gm.Database?.Characters.Get(gm.CurrentSave.CaptainId),
                    gunner:     db?.GetCrewByRole(saveId, Constants.Crew.Roles.Gunner),
                    pilot:      db?.GetCrewByRole(saveId, Constants.Crew.Roles.Pilot),
                    enemies:    enemies,
                    beamWeapon: beamItem,
                    torpedoes:  torpedoItem,
                    shields:    shieldItem,
                    armor:      armorItem,
                    engineer:   db?.GetCrewByRole(saveId, Constants.Crew.Roles.Engineer)
                );

                combatViewController.SetCombatState(combatState);
            }
        }

        combatViewController?.StartCombat(enemies);
        UpdateCombatDebugButtonLabel();
    }

    /// <summary>
    /// Leaves the Space Battle view and returns to System View.
    /// </summary>
    public void HideCombatView()
    {
        if (!_inCombat) return;
        _inCombat = false;

        combatViewController?.OnCombatExit();

        // Restore header elements
        if (systemNameText != null) systemNameText.gameObject.SetActive(true);
        if (salvageWidgetGO != null) salvageWidgetGO.SetActive(true);
        if (daysWidgetGO != null) daysWidgetGO.SetActive(true);
        if (fuelWidgetGO != null) fuelWidgetGO.SetActive(true);
        if (loyaltyWidgetGO != null) loyaltyWidgetGO.SetActive(true);
        if (combatHeaderStatsGroup != null)
        {
            combatHeaderStatsGroup.alpha          = 0f;
            combatHeaderStatsGroup.blocksRaycasts = false;
            combatHeaderStatsGroup.interactable   = false;
        }

        if (combatViewCanvasGroup != null)
        {
            combatViewCanvasGroup.alpha          = 0f;
            combatViewCanvasGroup.blocksRaycasts = false;
            combatViewCanvasGroup.interactable   = false;
        }

        if (navBarCanvasGroup != null)
        {
            navBarCanvasGroup.alpha          = 1f;
            navBarCanvasGroup.blocksRaycasts = true;
            navBarCanvasGroup.interactable   = true;
        }

        ShowSystemView();
        UpdateCombatDebugButtonLabel();
    }

    /// <summary>Toggles combat view — used by the temporary debug header button.</summary>
    private void ToggleCombatView(EnemyShipConfig[] enemies = null)
    {
        if (_inCombat) HideCombatView();
        else           ShowCombatView(enemies);
    }

    /// <summary>
    /// Enters combat if not already in it, or re-rolls the enemy fleet if already
    /// in combat. Used by the RANDOM button so you can re-roll mid-battle.
    /// </summary>
    private void StartOrRestartCombat(EnemyShipConfig[] enemies)
    {
        if (_inCombat)
        {
            // Rebuild combat state with the new enemies so _combatState.Enemies
            // matches the new EnemyCombatState objects StartCombat puts in _slotToState.
            // Without this, GetSlotForEnemy's reference comparison fails and enemy
            // attack effects (beams/torpedoes) never fire.
            if (combatViewController != null)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    var db   = gm.Database;
                    var ship = gm.PlayerShip;
                    EquipmentItem GetSlotItem(string slotName)
                    {
                        var slots = ship?.EquipmentSlots;
                        if (slots == null) return null;
                        if (!slots.TryGetValue(slotName, out int id) || id <= 0) return null;
                        return db?.Equipment.Get(id);
                    }
                    var saveId = gm.CurrentSave?.Id ?? 0;
                    var combatState = CombatState.Begin(
                        ship:       ship,
                        captain:    gm.Database?.Characters.Get(gm.CurrentSave.CaptainId),
                        gunner:     db?.GetCrewByRole(saveId, Constants.Crew.Roles.Gunner),
                        pilot:      db?.GetCrewByRole(saveId, Constants.Crew.Roles.Pilot),
                        enemies:    enemies,
                        beamWeapon: GetSlotItem(Constants.Ship.EquipmentSlots.BeamWeapons),
                        torpedoes:  GetSlotItem(Constants.Ship.EquipmentSlots.Torpedoes),
                        shields:    GetSlotItem(Constants.Ship.EquipmentSlots.Shields),
                        armor:      GetSlotItem(Constants.Ship.EquipmentSlots.Armor),
                        engineer:   db?.GetCrewByRole(saveId, Constants.Crew.Roles.Engineer)
                    );
                    combatViewController.OnCombatEnter();
                    combatViewController.SetCombatState(combatState);
                }
                else
                {
                    combatViewController.OnCombatEnter();
                }
                combatViewController.StartCombat(enemies);
            }
        }
        else
        {
            ShowCombatView(enemies);
        }
    }

    private void UpdateCombatDebugButtonLabel()
    {
        if (combatDebugButton == null) return;
        var mb = combatDebugButton.GetComponentInParent<Michsky.UI.Shift.MainButton>();
        if (mb != null) mb.buttonText = _inCombat ? "EXIT BATTLE" : "BATTLE";

        // Random button stays enabled in combat so you can re-roll enemies mid-battle.
    }

    // Builds a random enemy fleet of 1–3 ships for debug/testing.
    // Avoids known unavailable combos (e.g. Red Fighter has no sprite).
    private static EnemyShipConfig[] BuildRandomEnemies()
    {
        var colors  = (DGBShipColor[])System.Enum.GetValues(typeof(DGBShipColor));
        var classes = (DGBShipClass[])System.Enum.GetValues(typeof(DGBShipClass));

        int count   = UnityEngine.Random.Range(1, 4);  // 1, 2, or 3
        var configs = new EnemyShipConfig[count];
        for (int i = 0; i < count; i++)
        {
            DGBShipColor col;
            DGBShipClass cls;
            // Retry until we get a valid combo (avoids Red Fighter, which has no sprite).
            do
            {
                col = colors[UnityEngine.Random.Range(0, colors.Length)];
                cls = classes[UnityEngine.Random.Range(0, classes.Length)];
            }
            while (col == DGBShipColor.Red && cls == DGBShipClass.Fighter);

            configs[i] = new EnemyShipConfig { color = col, shipClass = cls, variant = 1 };
        }
        return configs;
    }

    // -----------------------------------------------------------------------
    // POI Detail Panel
    // -----------------------------------------------------------------------

    // ── Show / Hide ──────────────────────────────────────────────────────────
    //
    // Plain SetActive(true/false) is fine here because the close button's
    // Animator is permanently disabled in Start() — there is no animator that
    // could be left in a bad state across enable/disable cycles. The close
    // button's CanvasGroup alphas were locked once in Start() and never
    // change, so SetActive(false) → SetActive(true) leaves the visual
    // identical to before.

    /// <summary>
    /// Marks a POI as explored in the database the first time the ship arrives.
    /// </summary>
    private void MarkVisited(PointOfInterest poi)
    {
        if (poi == null || poi.IsExplored) return;
        poi.IsExplored = true;
        try { GameManager.Instance?.Database?.POIs.Update(poi); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SystemViewController] Failed to persist IsExplored for {poi.Name}: {e.Message}");
        }
    }

    private void ShowPOIDetail(PointOfInterest poi)
    {
        if (poiDetailPanel == null) return;

        _detailPoi = poi;

        // Previously visited POIs always show full info regardless of scanner level.
        bool fullAccess  = poi.IsExplored;
        int  sensorLevel = fullAccess ? int.MaxValue : GetSensorLevel();

        // ── Name (always visible) ─────────────────────────────────────────
        if (poiDetailNameText != null)
            poiDetailNameText.text = poi.Name;

        // ── Description body — gated by scanner level ─────────────────────
        //   Level 0/1 : limited detail
        //   Level 2+  : description, atmosphere/habitability, danger
        //   Level 3+  : resources
        if (poiDetailDescText != null)
        {
            poiDetailTypeText.text = sensorLevel >= 2 ? POITypeLabel(poi) : "";
            poiDetailTypeText.enabled = sensorLevel >= 2;
            poiDetailDescText.text = string.Empty;

            if (sensorLevel >= 2)
            {
                var sb = new System.Text.StringBuilder();

                if (!string.IsNullOrEmpty(poi.Description))
                    sb.AppendLine(poi.Description);

                poiDetailDescText.text = sb.ToString().TrimEnd();
            }
            else
            {

            }
        }

        // ── Scanner upgrade hint ──────────────────────────────────────────
        if (poiDetailScannerText != null)
        {
            bool showHint = !fullAccess && sensorLevel < 3;
            poiDetailScannerText.gameObject.SetActive(showHint);
            if (showHint)
            {
                poiDetailScannerText.text = "Upgrade Scanner to reveal surface details and resources";
            }
        }

        // Navigate button hidden when ship is already at this POI
        if (poiDetailNavigateButton != null)
            poiDetailNavigateButton.gameObject.SetActive(poi != _shipCurrentPoi);

        poiDetailPanel.SetActive(true);
    }

    /// <summary>
    /// Returns the player's effective sensor level based on the installed Scanner.
    ///   No scanner (or no ship data) → 1
    ///   Mk I  → 1 (name only)
    ///   Mk II → 2 (name + planet type)
    /// </summary>
    private int GetSensorLevel()
    {
        var gm   = GameManager.Instance;
        var ship = gm?.PlayerShip;
        if (ship == null) return 1;

        if (ship.EquipmentSlots.TryGetValue(Constants.Ship.EquipmentSlots.Scanner, out int itemId) && itemId > 0)
        {
            try
            {
                var item = gm.Database?.Equipment.Get(itemId);
                if (item != null) return (int)item.Tier; // MkI=1, MkII=2, etc.
            }
            catch { /* DB not ready */ }
        }

        return 1;
    }

    private void HidePOIDetail()
    {
        if (poiDetailPanel != null) poiDetailPanel.SetActive(false);
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
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
