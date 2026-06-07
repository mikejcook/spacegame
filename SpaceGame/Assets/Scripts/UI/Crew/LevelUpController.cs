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
///           Row_<skill>       ← HLG (Label | MinusButton | RankText | PlusButton)
///         SkillsContainerRight← VLG, skills 5-8 (Medicine…Science)
///           Row_<skill>       ← same structure
///       DividerRule2
///       CancelButton          ← centred-left (pivot-first placed)
///       ConfirmButton         ← centred-right (only interactable when 0 points left)
///
/// ── Skill rows are STATIC ────────────────────────────────────────────────────
///   The skill rows and their +/- buttons are built once at scene-build time by
///   GameSceneSetup (named "Row_&lt;skill&gt;"). Building them at runtime caused the
///   Shift button Animator playable-binding race (see CLAUDE.md) — freshly
///   instantiated buttons never settled to their visible Normal rest frame. This
///   controller binds to the existing rows by name in Start() and only updates
///   their values; it never creates or destroys them.
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
    [SerializeField] private Button      confirmButton;
    [SerializeField] private Button      cancelButton;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color TextSubtle  = new Color(0.60f, 0.72f, 0.85f, 1f);
    private static readonly Color AccentAmber = new Color(1.00f, 0.75f, 0.20f, 1f);

    // ── State ─────────────────────────────────────────────────────────────────

    private Character                    _character;
    private Dictionary<string, int>      _pending;    // edits not yet confirmed
    private int                          _pointsLeft;
    private readonly List<SkillRowUI>    _rowUIs = new List<SkillRowUI>();
    private bool                         _rowsBound;

    /// <summary>Fired after Confirm saves the character. Caller should refresh its view.</summary>
    public System.Action OnConfirmed;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton  != null) cancelButton.onClick.AddListener(OnCancelClicked);
        BindSkillRows();
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

        if (!_rowsBound) BindSkillRows();   // safety: bind on first use if Start hasn't run

        _character  = character;
        _pending    = new Dictionary<string, int>(character.Skills);
        _pointsLeft = character.AvailableSkillPoints;

        // Ensure every skill has an entry
        foreach (var s in Constants.Skills.All)
            if (!_pending.ContainsKey(s)) _pending[s] = 0;

        if (crewNameText != null)
            crewNameText.text = character.Name;

        RefreshUI();
        Show();
    }

    // ── Skill rows ────────────────────────────────────────────────────────────

    /// <summary>
    /// Binds to the statically-built "Row_&lt;skill&gt;" objects under the two column
    /// containers, caching their RankText/+/- references and wiring the +/- click
    /// listeners. Called once. The rows themselves are never created or destroyed here.
    /// </summary>
    private void BindSkillRows()
    {
        _rowUIs.Clear();

        var all   = Constants.Skills.All;
        int split = (all.Length + 1) / 2; // ceil-half → left column gets the larger share

        for (int i = 0; i < all.Length; i++)
        {
            var container = (i < split) ? skillsContainer : (skillsContainerRight ?? skillsContainer);
            if (container == null)
            {
                Debug.LogWarning("[LevelUpController] Skill container not wired.");
                continue;
            }

            var rowT = container.Find($"Row_{all[i]}");
            if (rowT == null)
            {
                Debug.LogWarning($"[LevelUpController] Missing Row_{all[i]} under {container.name} — re-run Star Captain → Setup Game Scene.");
                continue;
            }

            var rankTMP  = rowT.Find("RankText")?.GetComponent<TMP_Text>();
            var minusBtn = rowT.Find("MinusButton")?.GetComponentInChildren<Button>(true);
            var plusBtn  = rowT.Find("PlusButton")?.GetComponentInChildren<Button>(true);

            var row = new SkillRowUI
            {
                root      = rowT.gameObject,
                skillName = all[i],
                rankText  = rankTMP,
                minusBtn  = minusBtn,
                plusBtn   = plusBtn,
            };

            // Listeners are added in code (editor-script AddListener doesn't persist).
            string captured = all[i];
            if (minusBtn != null) minusBtn.onClick.AddListener(() => AdjustSkill(captured, -1));
            if (plusBtn  != null) plusBtn.onClick.AddListener(()  => AdjustSkill(captured, +1));

            _rowUIs.Add(row);
        }

        _rowsBound = true;
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
            int rank     = _pending.TryGetValue(row.skillName, out int r) ? r : 0;
            int baseline = _character.Skills.TryGetValue(row.skillName, out int b) ? b : 0;

            if (row.rankText != null)
                row.rankText.text = $"{rank} / {cap}";

            if (row.minusBtn != null)
                row.minusBtn.interactable = rank > baseline;

            if (row.plusBtn != null)
                row.plusBtn.interactable = _pointsLeft > 0 && rank < cap;
        }

        if (confirmButton != null)
            confirmButton.interactable = (_pointsLeft == 0); // require all points spent
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
