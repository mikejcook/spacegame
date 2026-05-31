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
///   └─ GalaxyMap                     ← RectTransform; has SystemMapZoomController
///      ├─ GalaxyBackground           ← RawImage; galaxy texture applied in Start()
///      └─ SystemNodesContainer       ← RectTransform; system nodes + ship spawned here
///
/// ── System node layout ───────────────────────────────────────────────────
///
///   Each system uses anchor-based positioning (GalaxyX / GalaxyY as anchor
///   fractions within SystemNodesContainer) so nodes stay glued to the correct
///   galaxy-map position regardless of zoom or pan.  The ship uses the same
///   technique; its anchors are lerped during flight.
///
///   The current system is highlighted with a UIRingGraphic ring indicator.
/// </summary>
public class GalaxyViewController : MonoBehaviour
{
    // ── Serialised references (wired by GameSceneSetup) ───────────────────

    [SerializeField] private RawImage       galaxyBackground;
    [SerializeField] private RectTransform  systemNodesContainer;
    [SerializeField] private Sprite         shipSprite;

    // ── Callback wired by SystemViewController ────────────────────────────

    /// <summary>
    /// Invoked when the ship arrives at a star system node.
    /// SystemViewController uses this to switch to that system's view.
    /// </summary>
    public System.Action<StarSystem> OnSystemSelected;

    // ── Private state ─────────────────────────────────────────────────────

    private bool _initialised     = false;
    private bool _zoomInitialised = false;
    private int  _currentSystemId = -1;

    private SystemMapZoomController _zoomController;

    // Initial view: centre on the Sol / Alpha / Proxima cluster at 1.5× zoom.
    // Centroid of Sol (0.595, 0.448) + Alpha (0.658, 0.458) + Proxima (0.628, 0.428).
    private const float InitialZoom    = 1.95f;
    private const float ClusterCentreX = 0.627f;
    private const float ClusterCentreY = 0.445f;

    // System nodes — parallel lists so we can look up a system by index
    private readonly List<GameObject>  _nodes   = new List<GameObject>();
    private readonly List<StarSystem>  _systems = new List<StarSystem>();

    // Ship
    private GameObject   _shipNodeGO;
    private RectTransform _shipRT;
    private StarSystem    _shipCurrentSystem;
    private bool          _shipFlying;
    private Coroutine     _flyCoroutine;

    // Node visual constants
    private const float NodeSize        = 14f;
    private const float CurrentNodeSize = 18f;
    private const float RingSize        = 30f;
    private const float LabelOffset     = 14f;
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

        // ── Anchor to galaxy-map fraction ────────────────────────────────
        var go = new GameObject(system.Name, typeof(RectTransform));
        go.transform.SetParent(systemNodesContainer, false);

        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = new Vector2(system.GalaxyX, system.GalaxyY);
        rt.anchorMax    = new Vector2(system.GalaxyX, system.GalaxyY);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta    = new Vector2(size, size);

        // ── Dot visual ───────────────────────────────────────────────────
        var img   = go.AddComponent<Image>();
        img.color = isCurrent ? Color.white : StarColour(system.StarType, system.IsExplored);

        // ── Current-system ring ──────────────────────────────────────────
        if (isCurrent)
        {
            var ringGO = new GameObject("CurrentRing", typeof(RectTransform));
            ringGO.transform.SetParent(go.transform, false);

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

        // ── Label ────────────────────────────────────────────────────────
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(go.transform, false);

        var labelRT             = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin       = new Vector2(0.5f, 0f);
        labelRT.anchorMax       = new Vector2(0.5f, 0f);
        labelRT.pivot           = new Vector2(0.5f, 1f);
        labelRT.anchoredPosition = new Vector2(0f, -LabelOffset);
        labelRT.sizeDelta       = new Vector2(160f, 24f);

        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text          = system.Name;
        tmp.fontSize      = 14f;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = isCurrent
                            ? new Color(0.85f, 1.0f, 1.0f, 1.0f)
                            : new Color(0.80f, 0.80f, 0.85f, 0.75f);
        tmp.overflowMode  = TextOverflowModes.Overflow;

        // ── Button ───────────────────────────────────────────────────────
        var btn    = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = img.color;
        colors.highlightedColor = Color.Lerp(img.color, Color.white, 0.4f);
        colors.pressedColor     = Color.Lerp(img.color, Color.black, 0.3f);
        btn.colors              = colors;
        btn.targetGraphic       = img;

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

        _shipRT          = _shipNodeGO.GetComponent<RectTransform>();
        _shipRT.anchorMin = new Vector2(gx, gy);
        _shipRT.anchorMax = new Vector2(gx, gy);
        _shipRT.pivot     = new Vector2(0.5f, 0.5f);
        _shipRT.anchoredPosition = Vector2.zero;
        _shipRT.sizeDelta = new Vector2(ShipSize, ShipSize);

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
            img.sprite = null;
            img.color  = new Color(0.2f, 0.9f, 1.0f, 1f);   // cyan fallback
        }
    }

    // ── Interaction ───────────────────────────────────────────────────────

    private void OnSystemNodeClicked(StarSystem system)
    {
        if (_shipFlying) return;

        // Already at this system — open it immediately
        if (_shipCurrentSystem != null && system.Id == _shipCurrentSystem.Id)
        {
            OnSystemSelected?.Invoke(system);
            return;
        }

        float fromGX = _shipCurrentSystem?.GalaxyX ?? system.GalaxyX;
        float fromGY = _shipCurrentSystem?.GalaxyY ?? system.GalaxyY;

        if (_flyCoroutine != null) StopCoroutine(_flyCoroutine);
        _flyCoroutine = StartCoroutine(
            FlyShipToCoroutine(system, fromGX, fromGY, system.GalaxyX, system.GalaxyY));
    }

    private IEnumerator FlyShipToCoroutine(
        StarSystem target,
        float fromGX, float fromGY,
        float toGX,   float toGY)
    {
        _shipFlying = true;

        // Convert normalised galaxy coords to canvas pixels for angle + duration
        Canvas.ForceUpdateCanvases();
        var cRect    = systemNodesContainer.rect;
        float screenDX = (toGX - fromGX) * cRect.width;
        float screenDY = (toGY - fromGY) * cRect.height;
        float distance = Mathf.Sqrt(screenDX * screenDX + screenDY * screenDY);

        if (distance < 0.5f)
        {
            _shipCurrentSystem = target;
            _shipFlying        = false;
            OnSystemSelected?.Invoke(target);
            yield break;
        }

        float duration = Mathf.Clamp(distance / 280f, 0.5f, 4f);

        // Sprite nose faces left (−X) → add 180° to standard Atan2 result
        float targetAngleDeg = Mathf.Atan2(screenDY, screenDX) * Mathf.Rad2Deg + 180f;
        float startAngleDeg  = _shipRT != null ? _shipRT.localEulerAngles.z : 0f;
        float deltaAngle     = Mathf.DeltaAngle(startAngleDeg, targetAngleDeg);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t    = Mathf.Clamp01(elapsed / duration);
            float posT = t * t * (3f - 2f * t);   // smooth-step ease in/out

            float curGX = Mathf.Lerp(fromGX, toGX, posT);
            float curGY = Mathf.Lerp(fromGY, toGY, posT);

            if (_shipRT != null)
            {
                _shipRT.anchorMin        = new Vector2(curGX, curGY);
                _shipRT.anchorMax        = new Vector2(curGX, curGY);

                float rotT               = Mathf.Clamp01(t / 0.25f);
                _shipRT.localEulerAngles = new Vector3(0f, 0f,
                    startAngleDeg + rotT * deltaAngle);
            }

            yield return null;
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

        OnSystemSelected?.Invoke(target);
    }

    // ── Colour helpers ────────────────────────────────────────────────────

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
