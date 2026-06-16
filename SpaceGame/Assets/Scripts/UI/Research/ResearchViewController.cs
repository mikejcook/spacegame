using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime controller for the Research tree screen.
///
/// The panel hierarchy (built by GameSceneSetup.BuildResearchView) is:
///
///   ResearchView                (CanvasGroup for show/hide)
///     ResearchBackground        (solid dark blue — same as ShipView)
///     TreeScrollRect            (ScrollRect — pan to navigate)
///       Viewport                (RectMask2D)
///         TreeContent           (2400×1800 fixed canvas; nodes + lines)
///           ConnectionLayer     (UILineRenderer per edge)
///           NodeLayer           (one node GO per ResearchNode)
///     DetailPanel               (CanvasGroup — shown on node tap)
///       TopAccent / DetailNameText / DetailCategoryText
///       Rule / DetailDescText / DetailCostText
///       UnlockButton / CloseDetailButton
///
/// Node visual style matches the ShipView equipment slots:
///   NodeRoot  dark bg + Button
///     TierBorder  fills root — grey (unlearned) or white (researched)
///       SlotInner  dark bg, inset 3 px — makes the border ring visible
///         Icon  stretch-fills SlotInner, preserveAspect=true
///
/// Costs are paid in Salvage (GameManager.CurrentSave.Salvage).
/// </summary>
public class ResearchViewController : MonoBehaviour
{
    // ── Canvas size — must match GameSceneSetup.ResearchTreeW/H ──────────────
    public const float TreeCanvasW  = 2400f;
    public const float TreeCanvasH  = 1800f;
    // Must match GameSceneSetup.ResearchPadding — empty border around the node area.
    public const float TreePadding  = 300f;

    // ── Serialised references (wired by GameSceneSetup) ──────────────────────
    [SerializeField] private RectTransform treeContent;
    [SerializeField] private GameObject    connectionLayer;
    [SerializeField] private GameObject    nodeLayer;

    [SerializeField] private CanvasGroup   detailPanelGroup;
    [SerializeField] private TMP_Text      detailNameText;
    [SerializeField] private TMP_Text      detailDescText;
    [SerializeField] private TMP_Text      detailCostText;
    [SerializeField] private TMP_Text      detailCategoryText;
    [SerializeField] private Button        unlockButton;
    [SerializeField] private Button        closeDetailButton;

    // ── Node visual settings ──────────────────────────────────────────────────
    [SerializeField] private float nodeSize          = 140f;
    [SerializeField] private Color lineColorUnlocked = new Color(0.30f, 0.85f, 1.00f, 0.70f);
    [SerializeField] private Color lineColorLocked   = new Color(0.20f, 0.30f, 0.45f, 0.50f);
    [SerializeField] private float lineWidth         = 3f;

    // ── Node colours — matching ShipView equipment slot palette ──────────────
    static readonly Color NodeBgColor      = new Color(0.06f, 0.10f, 0.18f, 0.90f);
    static readonly Color NodeBgColorSolid = new Color(0.06f, 0.10f, 0.18f, 1.00f);
    static readonly Color BorderUnlearned  = new Color(0.45f, 0.45f, 0.45f, 1.00f); // grey
    static readonly Color BorderResearched = new Color(0.64f, 0.21f, 0.93f, 1.00f); // MkV purple

    // ── Runtime state ─────────────────────────────────────────────────────────
    private ResearchCollection  _collection;
    private HashSet<string>     _unlockedIds = new HashSet<string>();
    private ResearchNode        _selectedNode;
    private Dictionary<string, RectTransform> _nodeRTs     = new Dictionary<string, RectTransform>();
    private Dictionary<string, Image>         _nodeBorders = new Dictionary<string, Image>();
    private Dictionary<string, Image>         _nodeIcons   = new Dictionary<string, Image>();

    // ── Text colours ─────────────────────────────────────────────────────────
    static readonly Color TextWhite  = new Color(0.92f, 0.95f, 1.00f, 1.00f);
    static readonly Color TextAmber  = new Color(1.00f, 0.78f, 0.20f, 1.00f);

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        unlockButton?.onClick.AddListener(OnUnlockClicked);
        closeDetailButton?.onClick.AddListener(HideDetail);

        HideDetail();
    }

    /// <summary>Called by SystemViewController when the Research tab is shown.</summary>
    public void Refresh()
    {
        if (_collection == null)
        {
            _collection = ResearchCollection.LoadFromResources();
            if (_collection == null) return;
            BuildTree();
        }
        RefreshAllNodeVisuals();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tree construction
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildTree()
    {
        if (treeContent == null || _collection?.nodes == null) return;

        foreach (Transform child in nodeLayer.transform)       Object.Destroy(child.gameObject);
        foreach (Transform child in connectionLayer.transform) Object.Destroy(child.gameObject);
        _nodeRTs.Clear();
        _nodeBorders.Clear();
        _nodeIcons.Clear();

        // ── Spawn node widgets ──────────────────────────────────────────────
        foreach (var node in _collection.nodes)
        {
            // ── Root ───────────────────────────────────────────────────────
            var go = new GameObject(node.id, typeof(RectTransform));
            go.transform.SetParent(nodeLayer.transform, false);

            var rt       = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(nodeSize, nodeSize);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(TreePadding + node.posX * TreeCanvasW,
                                              TreePadding + node.posY * TreeCanvasH);

            var bg   = go.AddComponent<Image>();
            bg.color = NodeBgColor;

            var btn = go.AddComponent<Button>();
            var btnColors = btn.colors;
            btnColors.normalColor      = Color.white;
            btnColors.highlightedColor = new Color(0.55f, 0.90f, 1.00f, 1f);
            btnColors.pressedColor     = new Color(0.30f, 0.55f, 0.70f, 1f);
            btn.colors        = btnColors;
            btn.targetGraphic = bg;

            // ── TierBorder — fills root; its color IS the visible border ring ──
            var borderGO  = new GameObject("TierBorder", typeof(RectTransform));
            borderGO.transform.SetParent(go.transform, false);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color         = BorderUnlearned;
            borderImg.raycastTarget = false;
            var borderRT  = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = borderRT.offsetMax = Vector2.zero;
            _nodeBorders[node.id] = borderImg;

            // ── SlotInner — dark bg, inset 3 px — creates the border gap ───
            var innerGO  = new GameObject("SlotInner", typeof(RectTransform));
            innerGO.transform.SetParent(borderGO.transform, false);
            var innerImg = innerGO.AddComponent<Image>();
            innerImg.color         = NodeBgColorSolid;
            innerImg.raycastTarget = false;
            var innerRT  = innerGO.GetComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2( 3f,  3f);
            innerRT.offsetMax = new Vector2(-3f, -3f);

            // ── Icon — stretch-fills SlotInner ─────────────────────────────
            // Load as Texture2D and create a sprite manually so the full
            // texture is used regardless of the asset's sprite import mode.
            // (Copying PNGs into Resources via the filesystem can leave them
            // in Multiple mode, causing Resources.Load<Sprite> to return only
            // a sub-sprite slice instead of the whole icon.)
            if (!string.IsNullOrEmpty(node.iconPath))
            {
                var tex = Resources.Load<Texture2D>(node.iconPath);
                if (tex != null)
                {
                    var sprite  = Sprite.Create(tex,
                                      new Rect(0, 0, tex.width, tex.height),
                                      new Vector2(0.5f, 0.5f), 100f);
                    var iconGO  = new GameObject("Icon", typeof(RectTransform));
                    iconGO.transform.SetParent(innerGO.transform, false);
                    var iconImg = iconGO.AddComponent<Image>();
                    iconImg.sprite          = sprite;
                    iconImg.preserveAspect  = true;
                    iconImg.raycastTarget   = false;
                    iconImg.color           = new Color(1f, 1f, 1f, 0.25f); // start dim; refreshed below
                    var iconRT  = iconGO.GetComponent<RectTransform>();
                    iconRT.anchorMin = Vector2.zero;
                    iconRT.anchorMax = Vector2.one;
                    iconRT.offsetMin = iconRT.offsetMax = Vector2.zero;
                    _nodeIcons[node.id] = iconImg;
                }
            }

            // ── Label below node ───────────────────────────────────────────
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var labelRT  = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin        = new Vector2(0.5f, 0f);
            labelRT.anchorMax        = new Vector2(0.5f, 0f);
            labelRT.pivot            = new Vector2(0.5f, 1f);
            labelRT.anchoredPosition = new Vector2(0f, -4f);
            labelRT.sizeDelta        = new Vector2(nodeSize + 20f, 80f);
            var labelTMP             = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text             = node.displayName;
            labelTMP.fontSize         = 21f;
            labelTMP.color            = TextWhite;
            labelTMP.alignment        = TextAlignmentOptions.Center;
            labelTMP.textWrappingMode = TMPro.TextWrappingModes.Normal;

            // ── Click handler ──────────────────────────────────────────────
            string capturedId = node.id;
            btn.onClick.AddListener(() => OnNodeClicked(capturedId));

            _nodeRTs[node.id] = rt;
        }

        // ── Draw connection lines ───────────────────────────────────────────
        foreach (var node in _collection.nodes)
        {
            if (node.prerequisites == null) continue;
            foreach (var prereqId in node.prerequisites)
            {
                if (!_nodeRTs.TryGetValue(prereqId, out var fromRT)) continue;
                if (!_nodeRTs.TryGetValue(node.id,  out var toRT))   continue;

                var lineGO = new GameObject($"Line_{prereqId}_{node.id}", typeof(RectTransform));
                lineGO.transform.SetParent(connectionLayer.transform, false);

                var lineRT   = lineGO.GetComponent<RectTransform>();
                lineRT.pivot     = Vector2.zero;
                lineRT.anchorMin = Vector2.zero;
                lineRT.anchorMax = Vector2.one;
                lineRT.offsetMin = lineRT.offsetMax = Vector2.zero;

                var lr   = lineGO.AddComponent<UILineRenderer>();
                lr.lineWidth = lineWidth;
                lr.color     = lineColorLocked;
                lr.SetPoints(new List<Vector2>
                {
                    fromRT.anchoredPosition,
                    toRT.anchoredPosition
                });
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Visual refresh
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshAllNodeVisuals()
    {
        if (_collection?.nodes == null) return;
        foreach (var node in _collection.nodes)
        {
            if (!_nodeRTs.ContainsKey(node.id)) continue;

            bool unlocked  = _unlockedIds.Contains(node.id);
            bool available = IsAvailable(node);

            // Border: white = researched, grey = everything else
            if (_nodeBorders.TryGetValue(node.id, out var borderImg))
                borderImg.color = unlocked ? BorderResearched : BorderUnlearned;

            // Icon alpha: bright when unlocked, medium when available, dim when locked
            if (_nodeIcons.TryGetValue(node.id, out var iconImg))
            {
                iconImg.color = unlocked  ? new Color(1f, 1f, 1f, 0.90f)
                              : available ? new Color(1f, 1f, 1f, 0.60f)
                                          : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        foreach (var lr in connectionLayer.GetComponentsInChildren<UILineRenderer>())
        {
            // Name: "Line_{from}_{to}"
            var parts = lr.gameObject.name.Split('_');
            if (parts.Length >= 3)
                lr.color = _unlockedIds.Contains(parts[1]) ? lineColorUnlocked : lineColorLocked;
        }
    }

    private bool IsAvailable(ResearchNode node)
    {
        if (node.prerequisites == null || node.prerequisites.Length == 0) return true;
        foreach (var prereq in node.prerequisites)
            if (!_unlockedIds.Contains(prereq)) return false;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Detail panel
    // ─────────────────────────────────────────────────────────────────────────

    private void OnNodeClicked(string nodeId)
    {
        var node = _collection?.GetNode(nodeId);
        if (node == null) return;
        _selectedNode = node;
        ShowDetail(node);
    }

    private void ShowDetail(ResearchNode node)
    {
        if (detailNameText     != null) detailNameText.text     = node.displayName;
        if (detailDescText     != null) detailDescText.text     = node.description;
        if (detailCostText     != null) detailCostText.gameObject.SetActive(false);
        if (detailCategoryText != null) detailCategoryText.text = node.category;

        bool unlocked  = _unlockedIds.Contains(node.id);
        bool available = IsAvailable(node);

        if (unlockButton != null)
        {
            unlockButton.interactable = !unlocked && available;
            var lbl = unlockButton.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
                lbl.text = unlocked   ? "Unlocked"
                         : !available ? "Locked"
                                      : "Unlock";
        }

        if (detailPanelGroup != null)
        {
            detailPanelGroup.alpha          = 1f;
            detailPanelGroup.blocksRaycasts = true;
            detailPanelGroup.interactable   = true;
        }
    }

    private void HideDetail()
    {
        _selectedNode = null;
        if (detailPanelGroup != null)
        {
            detailPanelGroup.alpha          = 0f;
            detailPanelGroup.blocksRaycasts = false;
            detailPanelGroup.interactable   = false;
        }
    }

    private void OnUnlockClicked()
    {
        if (_selectedNode == null) return;
        if (_unlockedIds.Contains(_selectedNode.id)) return;
        if (!IsAvailable(_selectedNode)) return;

        _unlockedIds.Add(_selectedNode.id);

        RefreshAllNodeVisuals();
        HideDetail();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save / load integration (future)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Restores unlock state from a saved game.</summary>
    public void LoadState(HashSet<string> unlockedIds)
    {
        _unlockedIds = unlockedIds ?? new HashSet<string>();
        if (_collection != null) RefreshAllNodeVisuals();
    }

    /// <summary>Returns current unlock state for saving.</summary>
    public HashSet<string> GetUnlockedIds() => _unlockedIds;
}
