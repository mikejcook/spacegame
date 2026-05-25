using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Overlay panel that displays all portraits from PortraitLibrary in a 6-column
/// scrollable grid. Calls onPortraitSelected when the player taps a portrait,
/// then closes itself.
///
/// A three-way toggle (All / Masculine / Feminine) sits above the grid and filters
/// by filename prefix ("male_" / "female_"). Portraits whose names don't match
/// either prefix are always shown in all modes.
///
/// Scene setup:
///   - Create a full-screen panel child of the Canvas (e.g. "PortraitPickerPanel").
///   - Add a semi-transparent background Image.
///   - Add a FilterBar child with three Buttons: FilterAll, FilterMasculine, FilterFeminine.
///   - Add a ScrollView child. Set its Content's GridLayoutGroup to:
///       Constraint = Fixed Column Count, Constraint Count = 6
///       Cell Size  = e.g. (150, 150)
///       Spacing    = e.g. (12, 12)
///   - Add a "Close" Button outside or above the ScrollView.
///   - Assign this script, wire up the fields below.
///   - Assign the PortraitLibrary asset (built via Star Captain -> Build Portrait Library).
///   - Disable the panel GameObject by default in the Inspector.
///
/// The portraits are created procedurally at runtime — no prefab required.
/// </summary>
public class PortraitPickerPanel : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector references
    // -----------------------------------------------------------------------
    [Header("References")]
    [Tooltip("The Content RectTransform inside the ScrollRect. Must have a GridLayoutGroup (6 cols).")]
    [SerializeField] private RectTransform gridContainer;

    [SerializeField] private Button closeButton;

    [Header("Filter Buttons")]
    [Tooltip("Shows all portraits.")]
    [SerializeField] private Button filterAllButton;
    [Tooltip("Shows only masculine-presenting portraits (filename starts with 'male_').")]
    [SerializeField] private Button filterMasculineButton;
    [Tooltip("Shows only feminine-presenting portraits (filename starts with 'female_').")]
    [SerializeField] private Button filterFeminineButton;

    [Header("Data")]
    [SerializeField] private PortraitLibrary portraitLibrary;

    [Header("Grid item appearance")]
    [Tooltip("Size of each portrait cell in the grid (should match the GridLayoutGroup CellSize).")]
    [SerializeField] private Vector2 cellSize = new Vector2(150f, 150f);

    [Tooltip("Color tint applied to a portrait button on hover.")]
    [SerializeField] private Color highlightColor = new Color(0.8f, 0.9f, 1f, 1f);

    // -----------------------------------------------------------------------
    // Filter state
    // -----------------------------------------------------------------------
    private enum PortraitFilter { All, Masculine, Feminine }
    private PortraitFilter _currentFilter = PortraitFilter.All;

    // -----------------------------------------------------------------------
    // Runtime state
    // -----------------------------------------------------------------------
    private Action<string> _onPortraitSelected; // fileName (e.g. "male_1.png")
    private bool _populated;

    private struct PortraitEntry
    {
        public GameObject go;
        public string     fileName;
    }
    private readonly List<PortraitEntry> _entries = new List<PortraitEntry>();

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private void Awake()
    {
        closeButton?.onClick.AddListener(Close);

        filterAllButton?.onClick.AddListener(()       => ApplyFilter(PortraitFilter.All));
        filterMasculineButton?.onClick.AddListener(() => ApplyFilter(PortraitFilter.Masculine));
        filterFeminineButton?.onClick.AddListener(()  => ApplyFilter(PortraitFilter.Feminine));

        // Ensure button visuals match the default filter (All) on first frame.
        UpdateFilterButtonVisuals();

        // Do NOT call SetActive(false) here — the scene already has this panel
        // inactive. Calling it here would cause a lifecycle bug: on the first
        // Open() call, SetActive(true) defers Awake until activation, which
        // then immediately calls SetActive(false) again, hiding the panel.
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Open the picker. The callback receives the chosen portrait filename.
    /// </summary>
    public void Open(Action<string> onPortraitSelected)
    {
        _onPortraitSelected = onPortraitSelected;

        // Load library lazily here (not in Awake) so it's ready before PopulateGrid.
        if (portraitLibrary == null)
            portraitLibrary = Resources.Load<PortraitLibrary>("PortraitLibrary");

        gameObject.SetActive(true);

        // Build the grid lazily on first open (portraits don't change at runtime).
        if (!_populated)
        {
            PopulateGrid();
            _populated = true;
        }

        // Force an immediate layout pass so all RectTransforms have correct sizes
        // on the very first frame the panel is visible.
        Canvas.ForceUpdateCanvases();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Filter logic
    // -----------------------------------------------------------------------
    private void ApplyFilter(PortraitFilter filter)
    {
        _currentFilter = filter;
        UpdateFilterButtonVisuals();
        RefreshGridVisibility();
    }

    private void UpdateFilterButtonVisuals()
    {
        SetFilterBtnActive(filterAllButton,       _currentFilter == PortraitFilter.All);
        SetFilterBtnActive(filterMasculineButton, _currentFilter == PortraitFilter.Masculine);
        SetFilterBtnActive(filterFeminineButton,  _currentFilter == PortraitFilter.Feminine);
    }

    private void SetFilterBtnActive(Button btn, bool active)
    {
        if (btn == null) return;

        // Active/inactive state is communicated via CanvasGroup alpha so it doesn't
        // conflict with the Shift UI animator that drives the button's child Images.
        var cg = btn.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = active ? 1f : 0.5f;
        }
    }

    private void RefreshGridVisibility()
    {
        foreach (var entry in _entries)
        {
            bool show = _currentFilter switch
            {
                PortraitFilter.Masculine => entry.fileName.StartsWith("male_",   StringComparison.OrdinalIgnoreCase),
                PortraitFilter.Feminine  => entry.fileName.StartsWith("female_", StringComparison.OrdinalIgnoreCase),
                _                        => true,
            };
            entry.go.SetActive(show);
        }
    }

    // -----------------------------------------------------------------------
    // Grid building
    // -----------------------------------------------------------------------
    private void PopulateGrid()
    {
        if (gridContainer == null)
        {
            Debug.LogError("[PortraitPickerPanel] gridContainer is not assigned!");
            return;
        }

        // Clear any existing items (e.g. placeholder objects left in the scene).
        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        _entries.Clear();

        if (portraitLibrary == null || !portraitLibrary.IsValid)
        {
            Debug.LogWarning("[PortraitPickerPanel] No valid PortraitLibrary assigned. " +
                             "Run Star Captain -> Build Portrait Library first.");
            return;
        }

        for (int i = 0; i < portraitLibrary.Count; i++)
        {
            var go = CreatePortraitItem(i, portraitLibrary.Portraits[i], portraitLibrary.FileNames[i]);
            _entries.Add(new PortraitEntry { go = go, fileName = portraitLibrary.FileNames[i] });
        }
    }

    private GameObject CreatePortraitItem(int index, Texture2D texture, string fileName)
    {
        // --- Root button object ---
        var itemGO = new GameObject($"Portrait_{index}", typeof(RectTransform));
        itemGO.transform.SetParent(gridContainer, false);

        var itemRT = itemGO.GetComponent<RectTransform>();
        itemRT.sizeDelta = cellSize;

        // Image acts as the button click target and background.
        var bgImage   = itemGO.AddComponent<Image>();
        bgImage.color = Color.white;

        var button = itemGO.AddComponent<Button>();
        button.targetGraphic = bgImage;

        var colors              = button.colors;
        colors.highlightedColor = highlightColor;
        colors.selectedColor    = highlightColor;
        button.colors           = colors;

        // --- RawImage child for the portrait texture ---
        var rawGO = new GameObject("Texture", typeof(RectTransform));
        rawGO.transform.SetParent(itemGO.transform, false);

        var rawRT              = rawGO.GetComponent<RectTransform>();
        rawRT.anchorMin        = Vector2.zero;
        rawRT.anchorMax        = Vector2.one;
        rawRT.sizeDelta        = Vector2.zero;
        rawRT.anchoredPosition = Vector2.zero;

        var rawImage     = rawGO.AddComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.uvRect  = new Rect(0, 0, 1, 1);

        // --- Wire up click ---
        string capturedFileName = fileName; // capture for lambda
        button.onClick.AddListener(() =>
        {
            _onPortraitSelected?.Invoke(capturedFileName);
            Close();
        });

        return itemGO;
    }
}
