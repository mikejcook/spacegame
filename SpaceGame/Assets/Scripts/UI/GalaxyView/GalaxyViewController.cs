using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Populates the Galaxy View with star system nodes loaded from the database.
///
/// ── Scene hierarchy expected ──────────────────────────────────────────────
///
///   GalaxyView                       ← this MonoBehaviour (starts inactive)
///   └─ GalaxyMap                     ← RectTransform; has SystemMapZoomController
///      ├─ GalaxyBackground           ← RawImage; galaxy texture applied in Start()
///      └─ SystemNodesContainer       ← RectTransform; system nodes spawned here
///
/// ── System node layout ───────────────────────────────────────────────────
///
///   Each system uses anchor-based positioning (GalaxyX / GalaxyY as anchor
///   fractions within SystemNodesContainer) so nodes stay glued to the correct
///   galaxy-map position regardless of zoom or pan.
///
///   The current system is highlighted with a UIRingGraphic ring indicator.
/// </summary>
public class GalaxyViewController : MonoBehaviour
{
    // ── Serialised references (wired by GameSceneSetup) ───────────────────

    [SerializeField] private RawImage      galaxyBackground;
    [SerializeField] private RectTransform systemNodesContainer;

    // ── Callback wired by SystemViewController ────────────────────────────

    /// <summary>
    /// Invoked when the player taps a star system node.
    /// SystemViewController handles the panel switch and save update.
    /// </summary>
    public System.Action<StarSystem> OnSystemSelected;

    // ── Private state ─────────────────────────────────────────────────────

    private bool _initialised = false;
    private int  _currentSystemId = -1;

    private readonly List<GameObject> _nodes = new List<GameObject>();

    // Node visual constants
    private const float NodeSize        = 14f;   // canvas units, for normal systems
    private const float CurrentNodeSize = 18f;   // slightly larger for current
    private const float RingSize        = 30f;   // orbit ring around current system
    private const float LabelOffset     = 14f;   // distance below node centre to label

    // ── Unity lifecycle ───────────────────────────────────────────────────

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
            // Fallback: deep blue solid so the view is usable without the texture
            galaxyBackground.texture = null;
            galaxyBackground.color   = new Color(0.03f, 0.04f, 0.12f, 1f);
            Debug.LogWarning("[GalaxyViewController] GalaxyBackground texture not found. " +
                             "Run 'Star Captain → Generate Galaxy Texture' first.");
        }
    }

    // ── System node spawning ──────────────────────────────────────────────

    private void PopulateSystemNodes()
    {
        foreach (var n in _nodes) if (n != null) Destroy(n);
        _nodes.Clear();

        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null) return;

        int saveId  = gm.CurrentSave.Id;
        var systems = gm.Database.StarSystems
                        .Query()
                        .Where(s => s.SaveGameId == saveId && s.IsKnown)
                        .ToList();

        foreach (var system in systems)
            _nodes.Add(SpawnSystemNode(system));
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

        // ── Current-system ring (UIRingGraphic, same as orbit rings) ─────
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
        // Cheapest path: re-spawn all nodes.  With O(10s) of systems this is fine.
        if (_initialised) PopulateSystemNodes();
    }

    // ── Interaction ───────────────────────────────────────────────────────

    private void OnSystemNodeClicked(StarSystem system)
    {
        OnSystemSelected?.Invoke(system);
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
        // Unexplored systems are dimmer
        if (!explored) c = Color.Lerp(c, new Color(0.3f, 0.3f, 0.4f, 1f), 0.55f);
        return c;
    }
}
