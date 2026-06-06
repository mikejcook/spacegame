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
///     Card
///       TopBar                ← thin cyan strip
///       TitleText             ← "LEVEL UP"
///       CrewNameText          ← character name
///       PointsText            ← "X skill points to spend"
///       DividerRule
///       SkillsContainer       ← VLG, one SkillRow per skill
///         SkillRow
///           SkillLabel
///           MinusButton
///           RankText          ← "N / cap"
///           PlusButton
///       DividerRule2
///       ConfirmButton
///       CancelButton
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
    [SerializeField] private Transform   skillsContainer;
    [SerializeField] private Button      confirmButton;
    [SerializeField] private Button      cancelButton;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color TextWhite   = new Color(0.92f, 0.95f, 1.00f, 1f);
    private static readonly Color TextSubtle  = new Color(0.60f, 0.72f, 0.85f, 1f);
    private static readonly Color AccentCyan  = new Color(0.30f, 0.85f, 1.00f, 1f);
    private static readonly Color AccentAmber = new Color(1.00f, 0.75f, 0.20f, 1f);
    private static readonly Color BtnGreen    = new Color(0.08f, 0.38f, 0.22f, 1f);
    private static readonly Color BtnRed      = new Color(0.38f, 0.08f, 0.08f, 1f);

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

        foreach (var skillName in Constants.Skills.All)
            _rowUIs.Add(BuildRow(skillName));
    }

    private SkillRowUI BuildRow(string skillName)
    {
        var rowGO = new GameObject($"Row_{skillName}", typeof(RectTransform));
        rowGO.transform.SetParent(skillsContainer, false);

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 10f;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 48f;

        // Skill label
        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rowGO.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = skillName;
        labelTMP.fontSize  = 26f;
        labelTMP.color     = TextSubtle;
        labelTMP.alignment = TextAlignmentOptions.Left;
        labelGO.AddComponent<LayoutElement>().preferredWidth = 220f;

        // Minus button
        var minusGO  = MakeSmallButton(rowGO.transform, "MinusButton", "−", BtnRed);
        var minusBtn = minusGO.GetComponent<Button>();

        // Rank text — "N / cap"
        int cap     = Constants.Skills.MaxRankForLevel(_character.Level);
        var rankGO  = new GameObject("RankText", typeof(RectTransform));
        rankGO.transform.SetParent(rowGO.transform, false);
        var rankTMP = rankGO.AddComponent<TextMeshProUGUI>();
        rankTMP.text      = $"0 / {cap}";
        rankTMP.fontSize  = 26f;
        rankTMP.color     = AccentCyan;
        rankTMP.alignment = TextAlignmentOptions.Center;
        rankGO.AddComponent<LayoutElement>().preferredWidth = 80f;

        // Plus button
        var plusGO  = MakeSmallButton(rowGO.transform, "PlusButton", "+", BtnGreen);
        var plusBtn = plusGO.GetComponent<Button>();

        // Dot row — visual representation of current rank
        var dotsGO = new GameObject("Dots", typeof(RectTransform));
        dotsGO.transform.SetParent(rowGO.transform, false);
        var dotsHLG = dotsGO.AddComponent<HorizontalLayoutGroup>();
        dotsHLG.spacing                = 4f;
        dotsHLG.childControlWidth      = false;
        dotsHLG.childControlHeight     = false;
        dotsHLG.childForceExpandWidth  = false;
        dotsHLG.childForceExpandHeight = false;
        dotsHLG.childAlignment         = TextAnchor.MiddleLeft;
        dotsGO.AddComponent<LayoutElement>().preferredWidth = 160f;

        var dotImages = new Image[cap];
        for (int i = 0; i < cap; i++)
        {
            var dotGO = new GameObject($"Dot_{i}", typeof(RectTransform));
            dotGO.transform.SetParent(dotsGO.transform, false);
            var dotImg = dotGO.AddComponent<Image>();
            dotImg.color = new Color(0.25f, 0.35f, 0.45f, 0.70f);
            var le = dotGO.AddComponent<LayoutElement>();
            le.preferredWidth  = 16f;
            le.preferredHeight = 16f;
            le.flexibleWidth   = 0f;
            dotImages[i] = dotImg;
        }

        var row = new SkillRowUI
        {
            root      = rowGO,
            skillName = skillName,
            rankText  = rankTMP,
            minusBtn  = minusBtn,
            plusBtn   = plusBtn,
            dotImages = dotImages,
        };

        string captured = skillName;
        minusBtn.onClick.AddListener(() => AdjustSkill(captured, -1));
        plusBtn.onClick.AddListener(()  => AdjustSkill(captured, +1));

        return row;
    }

    private static GameObject MakeSmallButton(Transform parent, string name, string label, Color bg)
    {
        var go  = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = bg;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.highlightedColor = Color.Lerp(bg, Color.white, 0.25f);
        c.pressedColor     = Color.Lerp(bg, Color.black, 0.25f);
        btn.colors = c;

        go.AddComponent<LayoutElement>().preferredWidth  = 44f;
        go.AddComponent<LayoutElement>().preferredHeight = 44f;

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(go.transform, false);
        var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
        lblTMP.text      = label;
        lblTMP.fontSize  = 28f;
        lblTMP.color     = new Color(0.92f, 0.95f, 1f, 1f);
        lblTMP.alignment = TextAlignmentOptions.Center;
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;

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

            // Refresh dots
            for (int i = 0; i < row.dotImages.Length; i++)
            {
                if (row.dotImages[i] == null) continue;
                if (i < rank)
                    row.dotImages[i].color = AccentCyan;
                else if (i < baseline)
                    row.dotImages[i].color = new Color(0.15f, 0.40f, 0.55f, 0.80f); // dim existing
                else
                    row.dotImages[i].color = new Color(0.25f, 0.35f, 0.45f, 0.70f); // empty
            }
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
        public Image[]    dotImages;
    }
}
