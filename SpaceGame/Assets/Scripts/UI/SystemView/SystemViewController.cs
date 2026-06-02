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

    [Header("Player Ship")]
    [Tooltip("Sprite extracted from the ship prefab — wired by GameSceneSetup.")]
    [SerializeField] private Sprite shipSprite;

    [Header("POI Sprites")]
    [Tooltip("Sprite used for Space Station POI nodes — wired by GameSceneSetup.")]
    [SerializeField] private Sprite stationSprite;

    [Header("Galaxy View")]
    [Tooltip("Root GalaxyView panel — toggled when the Galaxy nav button is pressed.")]
    [SerializeField] private GameObject          galaxyViewPanel;
    [SerializeField] private GalaxyViewController galaxyViewController;

    [Header("Ship View")]
    [SerializeField] private GameObject      shipViewPanel;
    [SerializeField] private ShipViewController shipViewController;

    [Header("Crew View")]
    [SerializeField] private GameObject crewViewPanel;

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
    private PointOfInterest _shipCurrentPoi;
    private bool            _shipFlying;
    private Coroutine       _flyCoroutine;

    private PlanetLibrary            _planetLibrary;
    private SystemMapZoomController  _systemMapZoomController;

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
        if (gm == null)
        {
            Debug.LogWarning("[SystemViewController] GameManager not found — showing placeholder.");
            ShowPlaceholder();
            return;
        }

        PopulateFromGameManager(gm);
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
        _shipNodeGO     = null;
        _shipRT         = null;
        _shipCurrentPoi = null;
        _shipFlying     = false;

        if (_pois == null) return;

        // Force a layout pass so systemMapArea.rect is accurate
        Canvas.ForceUpdateCanvases();

        // ── Pixel bounds for orbits ───────────────────────────────────────
        // Map area is landscape (≈1920×870 canvas units); use the shorter axis
        // so orbits stay circular regardless of aspect ratio.
        Rect  mapRect    = systemMapArea.rect;
        float mapShort   = Mathf.Min(mapRect.width, mapRect.height);
        float minOrbitPx = 65f + 40f;          // clear the 130 px star + breathing room
        float maxOrbitPx = mapShort * 0.5f * 0.88f;

        // ── Find each POI's normalised orbital radius ─────────────────────
        // Data is stored as  SystemX/Y = 0.5 ± r*cos/sin(angle)
        int n = _pois.Count;
        var normRadii = new float[n];
        float maxNormR = 0f;
        for (int i = 0; i < n; i++)
        {
            float dx = _pois[i].SystemX - 0.5f;
            float dy = _pois[i].SystemY - 0.5f;
            normRadii[i] = Mathf.Sqrt(dx * dx + dy * dy);
            if (normRadii[i] > maxNormR) maxNormR = normRadii[i];
        }
        if (maxNormR < 0.01f) maxNormR = 0.5f;

        // ── Sort POIs by orbit radius ─────────────────────────────────────
        var indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;
        System.Array.Sort(indices, (a, b) => normRadii[a].CompareTo(normRadii[b]));

        // ── Assign pixel orbit radii with enforced minimum spacing ────────
        // 1. Start from an ideal Lerp distribution that preserves relative spacing.
        // 2. Walk sorted orbits and bump each up by minGapPx if needed.
        // 3. If the last orbit overflowed maxOrbitPx, compress proportionally.
        const float MinGapPx = 55f;

        var orbitPxArr = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = normRadii[indices[i]] / maxNormR;
            orbitPxArr[i] = Mathf.Lerp(minOrbitPx, maxOrbitPx, t);
        }
        for (int i = 1; i < n; i++)
            orbitPxArr[i] = Mathf.Max(orbitPxArr[i], orbitPxArr[i - 1] + MinGapPx);

        if (n > 1 && orbitPxArr[n - 1] > maxOrbitPx)
        {
            float overflow  = orbitPxArr[n - 1] - minOrbitPx;
            float available = maxOrbitPx          - minOrbitPx;
            float scale     = available / overflow;
            for (int i = 0; i < n; i++)
                orbitPxArr[i] = minOrbitPx + (orbitPxArr[i] - minOrbitPx) * scale;
        }

        // ── Scale node sizes down for dense systems ───────────────────────
        float sizeMult = n <= 5 ? 1.00f
                       : n <= 8 ? 0.82f
                                : 0.68f;

        // ── Spawn orbit rings first (rendered behind planet nodes) ────────
        for (int i = 0; i < n; i++)
            _orbitRings.Add(SpawnOrbitRing(orbitPxArr[i]));

        // ── Spawn POI nodes on top ────────────────────────────────────────
        for (int i = 0; i < n; i++)
        {
            var   poi    = _pois[indices[i]];
            bool  planet = poi.POIType == Constants.POI.Types.Planet;
            float base_  = planet ? (poi.PlanetType.IsGaseous() ? 80f : 62f) : 48f;
            float size   = base_ * sizeMult;

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
                    // Spawn just outside the visible map edge, opposite the first
                    // destination POI, as if the ship just dropped out of warp.
                    Vector2 dir      = destPos.magnitude > 0.01f ? destPos.normalized : Vector2.up;
                    float   edgeDist = Mathf.Min(mapRect.width, mapRect.height) * 0.5f + 60f;
                    SpawnShip(-dir * edgeDist, null);

                    // Point the ship toward the star (center of the map).
                    // dir already points inward; the sprite faces left so +180° compensates.
                    float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 180f;
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

    private GameObject SpawnPOINode(PointOfInterest poi,
                                    float orbitPx, float angle, float nodeSize)
    {
        // ── Container: anchored to map centre, offset along the orbit ─────
        var nodeGO = new GameObject(poi.Name, typeof(RectTransform));
        nodeGO.transform.SetParent(systemMapArea, false);

        var rt = nodeGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(orbitPx * Mathf.Cos(angle),
                                          orbitPx * Mathf.Sin(angle));
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

        return nodeGO;
    }

    // -----------------------------------------------------------------------
    // Player ship
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates the ship UI node as the last child of systemMapArea (renders on top).
    /// </summary>
    private void SpawnShip(Vector2 startPos, PointOfInterest startPoi)
    {
        _shipNodeGO = new GameObject("PlayerShip", typeof(RectTransform));
        _shipNodeGO.transform.SetParent(systemMapArea, false);

        _shipRT             = _shipNodeGO.GetComponent<RectTransform>();
        _shipRT.anchorMin   = new Vector2(0.5f, 0.5f);
        _shipRT.anchorMax   = new Vector2(0.5f, 0.5f);
        _shipRT.pivot       = new Vector2(0.5f, 0.5f);
        _shipRT.sizeDelta   = new Vector2(50f, 50f);
        _shipRT.anchoredPosition = startPos;

        var img = _shipNodeGO.AddComponent<Image>();
        if (shipSprite != null)
        {
            img.sprite         = shipSprite;
            img.color          = Color.white;
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;
        }
        else
        {
            // Fallback: bright cyan triangle-ish square if sprite not wired
            img.sprite = null;
            img.color  = new Color(0.2f, 0.9f, 1.0f, 1f);
        }

        // Tapping the ship while it's at a planet opens that planet's POI popup.
        var shipBtn = _shipNodeGO.AddComponent<Button>();
        shipBtn.transition = Selectable.Transition.None;
        shipBtn.onClick.AddListener(OnShipClicked);

        _shipCurrentPoi = startPoi;
    }

    private void OnShipClicked()
    {
        if (_shipFlying || _shipCurrentPoi == null) return;
        ShowPOIDetail(_shipCurrentPoi);
    }

    /// <summary>
    /// Called when any POI is tapped. If the ship is already there, show the
    /// popup immediately. If it's flying, ignore the tap. Otherwise fly there first.
    /// </summary>
    private void OnPOIClicked(PointOfInterest poi)
    {
        if (_shipFlying) return;
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
        _shipFlying = true;
        SetNavButtonsInteractable(false);

        Vector2 dir      = toPos - fromPos;
        float   distance = dir.magnitude;
        if (distance < 0.1f)
        {
            _shipCurrentPoi = target;
            _shipFlying     = false;
            SetNavButtonsInteractable(true);
            MarkVisited(target);
            ShowPOIDetail(target);
            yield break;
        }

        // The raw sprite points LEFT (−X) — add 180° to Atan2 result.
        float targetAngleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 180f;
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

        SetNavButtonsInteractable(true);
        MarkVisited(target);
        ShowPOIDetail(target);
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
        bool fullAccess = poi.IsExplored;
        int  sensorLevel = fullAccess ? int.MaxValue : GetSensorLevel();

        if (poiDetailNameText) poiDetailNameText.text = poi.Name;

        // Type line — visible at sensor level 2+
        if (poiDetailTypeText)
        {
            poiDetailTypeText.text    = sensorLevel >= 2 ? POITypeLabel(poi) : "";
            poiDetailTypeText.enabled = sensorLevel >= 2;
        }

        // Description — visible at sensor level 3+
        if (poiDetailDescText)
        {
            poiDetailDescText.text    = sensorLevel >= 3 ? (poi.Description ?? "") : "";
            poiDetailDescText.enabled = sensorLevel >= 3;
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
