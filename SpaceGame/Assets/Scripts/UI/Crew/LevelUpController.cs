using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime controller for the Level Up overlay panel.
///
/// ── Triggered by ─────────────────────────────────────────────────────────────
///   CrewViewController calls ShowLevelUp(character) when the selected crew
///   member has AvailableSkillPoints > 0.
///
/// ── Layout (built by GameSceneSetup.BuildLevelUpPanel) ───────────────────────
///
///   LevelUpPanel              ← CanvasGroup (alpha=0 when hidden)
///     Scrim                   ← full-screen dark overlay
///     Card                    ← 1400×490, wide to host two skill columns
///       TopBar                ← thin cyan accent strip
///       TitleText             ← "LEVEL UP"
///       CrewNameText          ← character name (italic, cyan)
///       PointsText            ← "X skill points to spend" (amber)
///       DividerRule
///       ColumnsArea           ← HLG (two equal columns)
///         SkillsContainer     ← VLG, skills 0-4 (Athletics…Gunnery)
///           SkillRow          ← HLG (Label | PanelBtn− | RankText | PanelBtn+ | Dots)
///         SkillsContainerRight← VLG, skills 5-8 (Medicine…Science)
///           SkillRow          ← same structure
///       DividerRule2
///       CancelButton          ← centred-left (pivot-first placed)
///       ConfirmButton         ← centred-right (only interactable when 0 points left)
///
/// ── Rules enforced ───────────────────────────────────────────────────────────
///   • Each skill is capped at character.Level + 1.
///   • Points cannot go below 0 (can't spend into negatives).
///   • Cancel discards all pending changes (reverts to saved values).
///   • Confirm writes changes to DB via GameManager.Database.
/// </summary>
public class LevelUpController : MonoBehaviour
{
    // ── Wired by GameSceneSetup ───────────────────────────────────────────────

    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TMP_Text    crewNameText;
    [SerializeField] private TMP_Text    pointsText;
    [SerializeField] private Transform   skillsContainer;       // left column (skills 0-4)
    [SerializeField] private Transform   skillsContainerRight;  // right column (skills 5-8)
    [SerializeField] private GameObject  skillBtnPrefab;        // Shift Squad Member Button prefab
    [SerializeField] private Sprite      plusIcon;              // icon_plus sprite
    [SerializeField] private Sprite      minusIcon;             // icon_minus sprite
    [SerializeField] private Button      confirmButton;
    [SerializeField] private Button      cancelButton;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color TextWhite   = new Color(0.92f, 0.95f, 1.00f, 1f);
    private static readonly Color TextSubtle  = new Color(0.60f, 0.72f, 0.85f, 1f);
    private static readonly Color AccentCyan  = new Color(0.30f, 0.85f, 1.00f, 1f);
    private static readonly Color AccentAmber = new Color(1.00f, 0.75f, 0.20f, 1f);

    // ── State ─────────────────────────────────────────────────────────────────

    private Character                    _character;
    private Dictionary<string, int>      _pending;    // edits not yet confirmed
    private int                          _pointsLeft;
    private readonly List<SkillRowUI>    _rowUIs = new List<SkillRowUI>();

    /// <summary>Fired after Confirm saves the character. Caller should refresh its view.</summary>
    public System.Action OnConfirmed;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton  != null) cancelButton.onClick.AddListener(OnCancelClicked);
        Hide();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Opens the level-up panel for the given character.</summary>
    public void ShowLevelUp(Character character)
    {
        if (character == null || character.AvailableSkillPoints <= 0)
        {
            Debug.LogWarning("[LevelUpController] ShowLevelUp called with no skill points available.");
            return;
        }

        _character  = character;
        _pending    = new Dictionary<string, int>(character.Skills);
        _pointsLeft = character.AvailableSkillPoints;

        // Ensure all 6 skills have an entry
        foreach (var s in Constants.Skills.All)
            if (!_pending.ContainsKey(s)) _pending[s] = 0;

        if (crewNameText != null)
            crewNameText.text = character.Name;

        BuildSkillRows();
        RefreshUI();
        Show();
    }

    // ── Skill rows ────────────────────────────────────────────────────────────

    private void BuildSkillRows()
    {
        // Clear old rows
        foreach (var r in _rowUIs)
            if (r.root != null) Destroy(r.root);
        _rowUIs.Clear();

        if (skillsContainer == null) return;

        var all   = Constants.Skills.All;
        int split = (all.Length + 1) / 2; // ceil-half → left column gets the larger share

        for (int i = 0; i < all.Length; i++)
        {
            var container = (i < split) ? skillsContainer : (skillsContainerRight ?? skillsContainer);
            _rowUIs.Add(BuildRow(all[i], container));
        }
    }

    private SkillRowUI BuildRow(string skillName, Transform container)
    {
        var rowGO = new GameObject($"Row_{skillName}", typeof(RectTransform));
        rowGO.transform.SetParent(container, false);
        // Column VLG has childControlHeight=false, so we must set sizeDelta.y explicitly.
        // Width will be overridden by the VLG (childControlWidth=true), so x doesn't matter.
        rowGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 46f);

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 8f;
        hlg.childControlWidth      = true;   // read LayoutElement.preferredWidth
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;  // respect preferred widths, don't bloat
        hlg.childForceExpandHeight = true;   // children fill row height
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        var rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 46f;
        rowLE.flexibleHeight  = 0f;

        // Skill label
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rowGO.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = skillName;
        labelTMP.fontSize  = 24f;
        labelTMP.color     = TextSubtle;
        labelTMP.alignment = TextAlignmentOptions.Left;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth = 155f;
        labelLE.flexibleWidth  = 0f;

        // Minus button — Shift Squad Member Button prefab if available, fallback plain button
        var minusGO  = MakeSkillButton(rowGO.transform, "MinusButton", minusIcon);
        var minusBtn = minusGO.GetComponentInChildren<Button>(true);

        // Rank text — "N / cap"
        int cap     = Constants.Skills.MaxRankForLevel(_character.Level);
        var rankGO  = new GameObject("RankText", typeof(RectTransform));
        rankGO.transform.SetParent(rowGO.transform, false);
        var rankTMP = rankGO.AddComponent<TextMeshProUGUI>();
        rankTMP.text      = $"0 / {cap}";
        rankTMP.fontSize  = 20f;
        rankTMP.color     = AccentCyan;
        rankTMP.alignment = TextAlignmentOptions.Center;
        var rankLE = rankGO.AddComponent<LayoutElement>();
        rankLE.preferredWidth = 58f;
        rankLE.flexibleWidth  = 0f;

        // Plus button
        var plusGO  = MakeSkillButton(rowGO.transform, "PlusButton", plusIcon);
        var plusBtn = plusGO.GetComponentInChildren<Button>(true);

        var row = new SkillRowUI
        {
            root      = rowGO,
            skillName = skillName,
            rankText  = rankTMP,
            minusBtn  = minusBtn,
            plusBtn   = plusBtn,
        };

        string captured = skillName;
        if (minusBtn != null) minusBtn.onClick.AddListener(() => AdjustSkill(captured, -1));
        if (plusBtn  != null) plusBtn.onClick.AddListener(()  => AdjustSkill(captured, +1));

        return row;
    }

    /// <summary>
    /// Creates a small skill +/− button using the Shift Squad Member Button prefab when
    /// available (wired by GameSceneSetup). The Squad Member Button uses a plain Unity
    /// Button on the root with Normal / Highlighted / Pressed animation states, each
    /// containing an "Icon" child Image. We set the icon sprite on all three so the
    /// correct art shows across every animation state. The "Profile Picture" child is
    /// hidden since we only want the icon.
    ///
    /// Also handles the older Panel Button prefab gracefully: if the wired prefab has a
    /// MainPanelButton component, its default "TITLE" buttonText is cleared and the
    /// root-level "Text" child is hidden before the icon is applied.
    ///
    /// Falls back to a plain styled button when no prefab is set.
    /// </summary>
    private GameObject MakeSkillButton(Transform parent, string goName, Sprite icon)
    {
        GameObject go;

        if (skillBtnPrefab != null)
        {
            go      = Instantiate(skillBtnPrefab, parent);
            go.name = goName;

            // ── Squad Member Button path ──────────────────────────────────
            // Hide the "Profile Picture" overlay — we want an icon button,
            // not a portrait/squad-member picker.
            var profilePic = go.transform.Find("Profile Picture");
            if (profilePic != null) profilePic.gameObject.SetActive(false);

            // ── Panel Button fallback path ────────────────────────────────
            // If the prefab has MainPanelButton (older wiring), clear its
            // default "TITLE" text so it doesn't appear over the icon.
            var mpb = go.GetComponent<Michsky.UI.Shift.MainPanelButton>();
            if (mpb != null)
            {
                go.SetActive(false);
                mpb.buttonText = "";    // prevents OnEnable stamping "TITLE"
                mpb.hasIcon    = true;  // keep icon layout
                go.SetActive(true);
                // Also hide the dedicated root-level "Text" child Panel Button carries.
                var textChild = go.transform.Find("Text");
                if (textChild != null) textChild.gameObject.SetActive(false);
            }

            // ── Icon sprites (both button types) ─────────────────────────
            // Normal / Highlighted / Pressed each contain a child named "Icon"
            // with an Image. Set the sprite on all three states so the icon
            // appears correctly regardless of animation state.
            if (icon != null)
            {
                foreach (string stateName in new[] { "Normal", "Highlighted", "Pressed" })
                {
                    var stateT = go.transform.Find(stateName);
                    if (stateT == null) continue;
                    var iconT = stateT.Find("Icon");
                    if (iconT == null) continue;
                    var img = iconT.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite         = icon;
                        img.preserveAspect = true;
                        img.color          = Color.white;
                    }
                }
            }
        }
        else
        {
            // Fallback — plain Image + Button with TMP label
            string label = (icon == plusIcon) ? "+" : "−";
            go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.10f, 0.28f, 0.46f, 1f);
            go.AddComponent<Button>().targetGraphic = img;
            var lblGO  = new GameObject("Label", typeof(RectTransform));
            lblGO.transform.SetParent(go.transform, false);
            var lbl    = lblGO.AddComponent<TextMeshProUGUI>();
            lbl.text      = label;
            lbl.fontSize  = 26f;
            lbl.color     = TextWhite;
            lbl.alignment = TextAlignmentOptions.Center;
            var lblRT      = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = lblRT.offsetMax = Vector2.zero;
        }

        // The Squad Member Button prefab is 60×60 and has no LayoutElement by default.
        // Pin preferred size and lock flexibleWidth so the row HLG doesn't bloat it.
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth  = 46f;
        le.preferredHeight = 46f;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        return go;
    }

    // ── Skill adjustment ─────────────────────────────────────────────────────

    private void AdjustSkill(string skillName, int delta)
    {
        if (_pending == null || _character == null) return;

        int current = _pending.TryGetValue(skillName, out int v) ? v : 0;
        int cap     = Constants.Skills.MaxRankForLevel(_character.Level);

        if (delta > 0)
        {
            if (_pointsLeft <= 0)          return;  // no points left
            if (current >= cap)            return;  // already at cap
            _pending[skillName] = current + 1;
            _pointsLeft--;
        }
        else
        {
            // Can't remove points the character already had before this level-up session
            int baseline = _character.Skills.TryGetValue(skillName, out int b) ? b : 0;
            if (current <= baseline)       return;  // can't refund pre-existing ranks
            _pending[skillName] = current - 1;
            _pointsLeft++;
        }

        RefreshUI();
    }

    // ── UI refresh ────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        if (pointsText != null)
        {
            pointsText.text  = $"{_pointsLeft} skill point{(_pointsLeft == 1 ? "" : "s")} to spend";
            pointsText.color = _pointsLeft > 0 ? AccentAmber : TextSubtle;
        }

        int cap = Constants.Skills.MaxRankForLevel(_character.Level);

        foreach (var row in _rowUIs)
        {
            int rank = _pending.TryGetValue(row.skillName, out int r) ? r : 0;
            int baseline = _character.Skills.TryGetValue(row.skillName, out int b) ? b : 0;

            if (row.rankText != null)
                row.rankText.text = $"{rank} / {cap}";

            if (row.minusBtn != null)
                row.minusBtn.interactable = rank > baseline;

            if (row.plusBtn != null)
                row.plusBtn.interactable = _pointsLeft > 0 && rank < cap;
        }

        if (confirmButton != null)
        {
            // Allow confirm even if points remain — player can leave some unspent... actually
            // let's require all points to be spent before confirming.
            confirmButton.interactable = (_pointsLeft == 0);
        }
    }

    // ── Confirm / Cancel ──────────────────────────────────────────────────────

    private void OnConfirmClicked()
    {
        if (_character == null || _pending == null) return;
        if (_pointsLeft != 0) return;  // safety check

        // Apply pending skills, zero out available points
        _character.Skills               = _pending;
        _character.AvailableSkillPoints = 0;

        // Persist to DB
        var gm = GameManager.Instance;
        if (gm?.Database != null)
        {
            try
            {
                gm.Database.Characters.Update(_character);
                Debug.Log($"[LevelUpController] Saved level-up for {_character.Name}.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LevelUpController] DB update failed: {e.Message}");
            }
        }

        Hide();
        OnConfirmed?.Invoke();
    }

    private void OnCancelClicked() => Hide();

    // ── Show / hide ───────────────────────────────────────────────────────────

    private void Show()
    {
        if (panelCanvasGroup == null) return;
        panelCanvasGroup.alpha          = 1f;
        panelCanvasGroup.blocksRaycasts = true;
        panelCanvasGroup.interactable   = true;
    }

    public void Hide()
    {
        if (panelCanvasGroup == null) return;
        panelCanvasGroup.alpha          = 0f;
        panelCanvasGroup.blocksRaycasts = false;
        panelCanvasGroup.interactable   = false;
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private class SkillRowUI
    {
        public GameObject root;
        public string     skillName;
        public TMP_Text   rankText;
        public Button     minusBtn;
        public Button     plusBtn;
    }
}
