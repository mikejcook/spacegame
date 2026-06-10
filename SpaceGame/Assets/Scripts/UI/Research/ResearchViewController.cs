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
/// Costs are paid in Salvage (GameManager.CurrentSave.Salvage).
/// </summary>
public class ResearchViewController : MonoBehaviour
{
    // ── Canvas size — must match GameSceneSetup.ResearchTreeW/H ──────────────
    public const float TreeCanvasW = 2400f;
    public const float TreeCanvasH = 1800f;

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
    [SerializeField] private float nodeSize          = 120f;
    [SerializeField] private Color unlockedColor     = new Color(0.30f, 0.85f, 1.00f, 1.00f);
    [SerializeField] private Color availableColor    = new Color(0.20f, 0.55f, 0.80f, 1.00f);
    [SerializeField] private Color lockedColor       = new Color(0.18f, 0.22f, 0.30f, 1.00f);
    [SerializeField] private Color lineColorUnlocked = new Color(0.30f, 0.85f, 1.00f, 0.70f);
    [SerializeField] private Color lineColorLocked   = new Color(0.20f, 0.30f, 0.45f, 0.50f);
    [SerializeField] private float lineWidth         = 3f;

    // ── Runtime state ──────────────────────────────────────────────────────────
    private ResearchCollection  _collection;
    private HashSet<string>     _unlockedIds = new HashSet<string>();
    private ResearchNode        _selectedNode;
    private Dictionary<string, RectTransform> _nodeRTs = new Dictionary<string, RectTransform>();

    // ── Colours ───────────────────────────────────────────────────────────────
    static readonly Color TextWhite  = new Color(0.92f, 0.95f, 1.00f, 1.00f);
    static readonly Color TextSubtle = new Color(0.60f, 0.72f, 0.85f, 1.00f);
    static readonly Color TextAmber  = new Color(1.00f, 0.78f, 0.20f, 1.00f);

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        _unlockedIds.Add("root");

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

        foreach (Transform child in nodeLayer.transform)      Object.Destroy(child.gameObject);
        foreach (Transform child in connectionLayer.transform) Object.Destroy(child.gameObject);
        _nodeRTs.Clear();

        // ── Spawn node widgets ──────────────────────────────────────────────
        foreach (var node in _collection.nodes)
        {
            var go = new GameObject(node.id, typeof(RectTransform));
            go.transform.SetParent(nodeLayer.transform, false);

            var rt       = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(nodeSize, nodeSize);
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(node.posX * TreeCanvasW, node.posY * TreeCanvasH);

            var bg   = go.AddComponent<Image>();
            bg.color = lockedColor;

            // Label below node
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var labelRT  = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin        = new Vector2(0f, 0f);
            labelRT.anchorMax        = new Vector2(1f, 0f);
            labelRT.pivot            = new Vector2(0.5f, 1f);
            labelRT.anchoredPosition = new Vector2(0f, -4f);
            labelRT.sizeDelta        = new Vector2(180f, 42f);
            var labelTMP             = labelGO.AddComponent<TextMeshProUGUI>();
            labelTMP.text            = node.displayName;
            labelTMP.fontSize        = 21f;
            labelTMP.color           = TextWhite;
            labelTMP.alignment       = TextAlignmentOptions.Center;
            labelTMP.textWrappingMode = TMPro.TextWrappingModes.Normal;

            // Cost badge (top-right)
            if (node.cost > 0)
            {
                var costGO  = new GameObject("Cost", typeof(RectTransform));
                costGO.transform.SetParent(go.transform, false);
                var costRT  = costGO.GetComponent<RectTransform>();
                costRT.anchorMin = costRT.anchorMax = new Vector2(1f, 1f);
                costRT.pivot     = new Vector2(0.5f, 0.5f);
                costRT.anchoredPosition = Vector2.zero;
                costRT.sizeDelta = new Vector2(48f, 28f);
                var costTMP  = costGO.AddComponent<TextMeshProUGUI>();
                costTMP.text      = node.cost.ToString();
                costTMP.fontSize  = 18f;
                costTMP.color     = TextAmber;
                costTMP.alignment = TextAlignmentOptions.Center;
            }

            // Click handler
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
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

                var lineRT       = lineGO.GetComponent<RectTransform>();
                // pivot (0,0) places local (0,0) at the bottom-left of ConnectionLayer,
                // matching the anchor (0,0) origin used by node anchoredPositions.
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
            if (!_nodeRTs.TryGetValue(node.id, out var rt)) continue;
            var img = rt.GetComponent<Image>();
            if (img == null) continue;

            if (_unlockedIds.Contains(node.id))
                img.color = unlockedColor;
            else if (IsAvailable(node))
                img.color = availableColor;
            else
                img.color = lockedColor;
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
        if (detailCostText     != null) detailCostText.text     = node.cost > 0
                                                                  ? $"Cost: {node.cost} salvage"
                                                                  : "Free";
        if (detailCategoryText != null) detailCategoryText.text = node.category;

        bool unlocked  = _unlockedIds.Contains(node.id);
        bool available = IsAvailable(node);
        int  salvage   = GameManager.Instance?.CurrentSave?.Salvage ?? 0;
        bool canAfford = salvage >= node.cost;

        if (unlockButton != null)
        {
            unlockButton.interactable = !unlocked && available && canAfford && node.cost > 0;
            var lbl = unlockButton.GetComponentInChildren<TMP_Text>();
            if (lbl != null)
                lbl.text = unlocked   ? "Unlocked"
                         : !available ? "Locked"
                         : !canAfford ? $"Need {node.cost} salvage"
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

        var gm = GameManager.Instance;
        int salvage = gm?.CurrentSave?.Salvage ?? 0;
        if (salvage < _selectedNode.cost) return;

        gm.RemoveSalvage(_selectedNode.cost);
        _unlockedIds.Add(_selectedNode.id);

        RefreshAllNodeVisuals();
        ShowDetail(_selectedNode); // refresh button state
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Save / load integration (future)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Restores unlock state from a saved game.</summary>
    public void LoadState(HashSet<string> unlockedIds)
    {
        _unlockedIds = unlockedIds ?? new HashSet<string>();
        _unlockedIds.Add("root");
        if (_collection != null) RefreshAllNodeVisuals();
    }

    /// <summary>Returns current unlock state for saving.</summary>
    public HashSet<string> GetUnlockedIds() => _unlockedIds;
}
