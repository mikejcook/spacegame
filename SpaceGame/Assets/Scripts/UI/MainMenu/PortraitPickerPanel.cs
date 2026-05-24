using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Overlay panel that displays up to 50 portrait options in a 3-column
/// scrollable grid. Calls onPortraitSelected when the player taps a portrait,
/// then closes itself.
///
/// Scene setup:
///   - Create a full-screen panel child of the Canvas (e.g. "PortraitPickerPanel").
///   - Add a semi-transparent background Image.
///   - Add a ScrollView child. Set its Content's GridLayoutGroup to:
///       Constraint = Fixed Column Count, Constraint Count = 3
///       Cell Size  = e.g. (150, 150)
///       Spacing    = e.g. (10, 10)
///   - Add a "Close" Button outside or above the ScrollView.
///   - Assign this script, wire up gridContainer (= ScrollView Content), closeButton.
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
    [Tooltip("The Content RectTransform inside the ScrollRect. Must have a GridLayoutGroup (3 cols).")]
    [SerializeField] private RectTransform gridContainer;

    [SerializeField] private Button closeButton;

    [Header("Data")]
    [SerializeField] private PortraitLibrary portraitLibrary;

    [Header("Grid item appearance")]
    [Tooltip("Size of each portrait cell in the grid (should match the GridLayoutGroup CellSize).")]
    [SerializeField] private Vector2 cellSize = new Vector2(150f, 150f);

    [Tooltip("Color tint applied to the selected portrait button on hover (default white = no tint).")]
    [SerializeField] private Color highlightColor = new Color(0.8f, 0.9f, 1f, 1f);

    // -----------------------------------------------------------------------
    // Runtime state
    // -----------------------------------------------------------------------
    private Action<string> _onPortraitSelected; // fileName (e.g. "1.png")
    private bool _populated;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private void Awake()
    {
        closeButton?.onClick.AddListener(Close);
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

        // Build the grid lazily on first open (portraits don't change at runtime)
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
    // Grid building
    // -----------------------------------------------------------------------
    private void PopulateGrid()
    {
        if (gridContainer == null)
        {
            Debug.LogError("[PortraitPickerPanel] gridContainer is not assigned!");
            return;
        }

        // Clear any existing items (e.g. placeholder objects left in the scene)
        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        if (portraitLibrary == null || !portraitLibrary.IsValid)
        {
            Debug.LogWarning("[PortraitPickerPanel] No valid PortraitLibrary assigned. " +
                             "Run Star Captain -> Build Portrait Library first.");
            return;
        }

        for (int i = 0; i < portraitLibrary.Count; i++)
        {
            CreatePortraitItem(i, portraitLibrary.Portraits[i], portraitLibrary.FileNames[i]);
        }
    }

    private void CreatePortraitItem(int index, Texture2D texture, string fileName)
    {
        // --- Root button object ---
        var itemGO = new GameObject($"Portrait_{index}", typeof(RectTransform));
        itemGO.transform.SetParent(gridContainer, false);

        var itemRT = itemGO.GetComponent<RectTransform>();
        itemRT.sizeDelta = cellSize;

        // Image acts as the button click target and background
        var bgImage    = itemGO.AddComponent<Image>();
        bgImage.color  = Color.white;

        var button = itemGO.AddComponent<Button>();
        button.targetGraphic = bgImage;

        // Colour block: highlight on hover/select
        var colors            = button.colors;
        colors.highlightedColor = highlightColor;
        colors.selectedColor    = highlightColor;
        button.colors           = colors;

        // --- RawImage child for the portrait texture ---
        var rawGO = new GameObject("Texture", typeof(RectTransform));
        rawGO.transform.SetParent(itemGO.transform, false);

        var rawRT         = rawGO.GetComponent<RectTransform>();
        rawRT.anchorMin   = Vector2.zero;
        rawRT.anchorMax   = Vector2.one;
        rawRT.sizeDelta   = Vector2.zero;
        rawRT.anchoredPosition = Vector2.zero;

        var rawImage      = rawGO.AddComponent<RawImage>();
        rawImage.texture  = texture;
        rawImage.uvRect   = new Rect(0, 0, 1, 1);

        // --- Wire up click ---
        string capturedFileName = fileName; // capture for lambda
        button.onClick.AddListener(() =>
        {
            _onPortraitSelected?.Invoke(capturedFileName);
            Close();
        });
    }
}
