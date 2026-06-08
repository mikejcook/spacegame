using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime controller for the Crew Recruitment overlay panel.
///
/// ── Triggered by ─────────────────────────────────────────────────────────────
///   SystemViewController calls ShowRecruitment(stationName, captainLevel) when
///   the player's ship arrives at a functioning (non-derelict) space station.
///
/// ── Layout (built by GameSceneSetup.BuildRecruitmentPanel) ───────────────────
///
///   RecruitmentPanel          ← CanvasGroup (alpha=0 when hidden)
///     Scrim                   ← full-screen dark overlay
///     Card
///       TopBar                ← thin cyan strip
///       TitleText             ← "CREW RECRUITMENT"
///       StationNameText       ← "[Station Name]"
///       DividerRule
///       CandidatesRow         ← HorizontalLayoutGroup, 2–3 candidate cards
///         CandidateCard_0..2
///           PortraitImage
///           NameText
///           RoleText
///           LevelText
///           SkillsContainer   ← VLG, skill rows
///           HireButton
///       CloseButton
///
/// ── Show/hide pattern ────────────────────────────────────────────────────────
///   Uses CanvasGroup (not SetActive) so any Shift buttons inside stay
///   animator-bound across multiple shows. Panel is kept active for the
///   scene's lifetime; only alpha + blocksRaycasts change.
/// </summary>
public class RecruitmentController : MonoBehaviour
{
    // ── Panel wired by GameSceneSetup ────────────────────────────────────────

    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TMP_Text    stationNameText;
    [SerializeField] private Transform   candidatesRow;
    [SerializeField] private Button      closeButton;

    // ── Colours ──────────────────────────────────────────────────────────────

    private static readonly Color TextWhite  = new Color(0.92f, 0.95f, 1.00f, 1f);
    private static readonly Color TextSubtle = new Color(0.60f, 0.72f, 0.85f, 1f);
    private static readonly Color AccentCyan = new Color(0.30f, 0.85f, 1.00f, 1f);
    private static readonly Color PanelDark  = new Color(0.06f, 0.09f, 0.14f, 0.97f);
    private static readonly Color RowBg      = new Color(0.04f, 0.07f, 0.13f, 1.00f);

    // ── State ─────────────────────────────────────────────────────────────────

    private PortraitLibrary             _portraitLib;
    private readonly List<Character>    _candidates  = new List<Character>();
    private readonly List<GameObject>   _cardGOs     = new List<GameObject>();

    // ── Callbacks ─────────────────────────────────────────────────────────────

    /// <summary>Fired when the panel is closed (via Close or after hiring).</summary>
    public System.Action OnClosed;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        Hide();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the set of portrait IDs currently in use by the captain and all hired crew.
    /// Unhired recruits do NOT contribute — their portrait is released between visits.
    /// </summary>
    private System.Collections.Generic.HashSet<string> CollectUsedPortraits()
    {
        var used = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null) return used;

        // All hired crew for this save
        var crew = gm.Database.GetCrewForSave(gm.CurrentSave.Id);
        if (crew != null)
            foreach (var c in crew)
                if (!string.IsNullOrEmpty(c.PortraitId))
                    used.Add(c.PortraitId);

        return used;
    }

    // ── Card builder ─────────────────────────────────────────────────────────

    private void BuildCandidateCards()
    {
        // Destroy previous cards
        foreach (var go in _cardGOs)
            if (go != null) Destroy(go);
        _cardGOs.Clear();

        if (candidatesRow == null) return;

        for (int i = 0; i < _candidates.Count; i++)
            _cardGOs.Add(BuildCard(_candidates[i], i));
    }

    private GameObject BuildCard(Character candidate, int index)
    {
        // Card root
        var card = new GameObject($"CandidateCard_{index}", typeof(RectTransform));
        card.transform.SetParent(candidatesRow, false);

        var cardImg = card.AddComponent<Image>();
        cardImg.color = RowBg;

        var cardLE = card.AddComponent<LayoutElement>();
        cardLE.flexibleWidth   = 1f;
        cardLE.preferredHeight = 460f;

        var vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.padding                = new RectOffset(16, 16, 16, 16);
        vlg.spacing                = 10f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment         = TextAnchor.UpperCenter;

        // Portrait
        var portraitGO  = new GameObject("PortraitImage", typeof(RectTransform));
        portraitGO.transform.SetParent(card.transform, false);
        var portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.preserveAspect = true;
        LoadPortraitInto(portraitImg, candidate.PortraitId);
        var portraitLE = portraitGO.AddComponent<LayoutElement>();
        portraitLE.preferredHeight = 130f;
        portraitLE.preferredWidth  = 130f;
        portraitLE.flexibleWidth   = 0f;

        // Name
        AddLabel(card.transform, "NameText",  candidate.Name,               28f, TextWhite,  true,  36f);
        // Role
        AddLabel(card.transform, "RoleText",  FormatRole(candidate.Role),   22f, AccentCyan, false, 28f);
        // Level
        AddLabel(card.transform, "LevelText", $"Level {candidate.Level}",   20f, TextSubtle, false, 26f);

        // Divider
        var div    = new GameObject("Divider", typeof(RectTransform));
        div.transform.SetParent(card.transform, false);
        div.AddComponent<Image>().color = new Color(0.20f, 0.35f, 0.50f, 0.55f);
        div.AddComponent<LayoutElement>().preferredHeight = 1f;

        // Skills
        var skillsHeader = new GameObject("SkillsHeader", typeof(RectTransform));
        skillsHeader.transform.SetParent(card.transform, false);
        var shTMP = skillsHeader.AddComponent<TextMeshProUGUI>();
        shTMP.text      = "SKILLS";
        shTMP.fontSize  = 18f;
        shTMP.color     = TextSubtle;
        shTMP.alignment = TextAlignmentOptions.Left;
        skillsHeader.AddComponent<LayoutElement>().preferredHeight = 22f;

        var skillsContainer = new GameObject("SkillsContainer", typeof(RectTransform));
        skillsContainer.transform.SetParent(card.transform, false);
        var sVLG = skillsContainer.AddComponent<VerticalLayoutGroup>();
        sVLG.spacing                = 4f;
        sVLG.childControlWidth      = true;
        sVLG.childControlHeight     = true;
        sVLG.childForceExpandWidth  = true;
        sVLG.childForceExpandHeight = false;
        skillsContainer.AddComponent<LayoutElement>().flexibleHeight = 1f;

        var skills = candidate.Skills;
        foreach (var skillName in Constants.Skills.All)
        {
            skills.TryGetValue(skillName, out int rank);
            if (rank > 0)
                BuildSkillRow(skillsContainer.transform, skillName, rank);
        }

        // Spacer
        var spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(card.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleHeight = 1f;

        // Hire button
        var hireBtnGO = new GameObject("HireButton", typeof(RectTransform));
        hireBtnGO.transform.SetParent(card.transform, false);
        var hireBg = hireBtnGO.AddComponent<Image>();
        hireBg.color = new Color(0.08f, 0.38f, 0.22f, 1f);
        var hireBtn = hireBtnGO.AddComponent<Button>();
        hireBtn.targetGraphic = hireBg;
        var hireColors = hireBtn.colors;
        hireColors.highlightedColor = new Color(0.12f, 0.55f, 0.32f, 1f);
        hireColors.pressedColor     = new Color(0.04f, 0.22f, 0.12f, 1f);
        hireBtn.colors = hireColors;
        hireBtnGO.AddComponent<LayoutElement>().preferredHeight = 52f;

        var hireLblGO = new GameObject("Label", typeof(RectTransform));
        hireLblGO.transform.SetParent(hireBtnGO.transform, false);
        var hireLbl = hireLblGO.AddComponent<TextMeshProUGUI>();
        hireLbl.text      = "HIRE";
        hireLbl.fontSize  = 24f;
        hireLbl.color     = TextWhite;
        hireLbl.alignment = TextAlignmentOptions.Center;
        hireLbl.fontStyle = FontStyles.Bold;
        var hireLblRT = hireLblGO.GetComponent<RectTransform>();
        hireLblRT.anchorMin = Vector2.zero;
        hireLblRT.anchorMax = Vector2.one;
        hireLblRT.offsetMin = Vector2.zero;
        hireLblRT.offsetMax = Vector2.zero;

        int capturedIdx = index;
        hireBtn.onClick.AddListener(() => OnHireClicked(capturedIdx, hireBtnGO));

        return card;
    }

    private void BuildSkillRow(Transform parent, string skillName, int rank)
    {
        var row = new GameObject($"Skill_{skillName}", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing                = 6f;
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        row.AddComponent<LayoutElement>().preferredHeight = 22f;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(row.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = skillName;
        labelTMP.fontSize  = 18f;
        labelTMP.color     = TextSubtle;
        labelTMP.alignment = TextAlignmentOptions.Left;
        labelGO.AddComponent<LayoutElement>().preferredWidth = 160f;

        const int   MaxRank = 10;
        const float DotSize = 12f;
        for (int i = 0; i < MaxRank; i++)
        {
            var dotGO  = new GameObject($"Dot_{i}", typeof(RectTransform));
            dotGO.transform.SetParent(row.transform, false);
            dotGO.AddComponent<Image>().color = i < rank
                ? AccentCyan
                : new Color(0.25f, 0.35f, 0.45f, 0.60f);
            var le = dotGO.AddComponent<LayoutElement>();
            le.preferredWidth  = DotSize;
            le.preferredHeight = DotSize;
            le.flexibleWidth   = 0f;
        }
    }

    private static GameObject AddLabel(Transform parent, string goName, string text,
                                        float size, Color color, bool bold, float height)
    {
        var go  = new GameObject(goName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = size;
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.fontStyle        = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.enableWordWrapping = false;
        tmp.overflowMode     = TextOverflowModes.Ellipsis;
        go.AddComponent<LayoutElement>().preferredHeight = height;
        return go;
    }

    // ── Hire logic ────────────────────────────────────────────────────────────

    private void OnHireClicked(int candidateIndex, GameObject hireBtnGO)
    {
        if (candidateIndex < 0 || candidateIndex >= _candidates.Count) return;

        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null)
        {
            Debug.LogWarning("[RecruitmentController] No active save — cannot hire crew.");
            return;
        }

        var candidate = _candidates[candidateIndex];
        candidate.SaveGameId = gm.CurrentSave.Id;

        try
        {
            gm.Database.Characters.Insert(candidate);
            Debug.Log($"[RecruitmentController] Hired {candidate.Name} ({candidate.Role} Lv{candidate.Level}).");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RecruitmentController] Failed to save crew member: {e.Message}");
            return;
        }

        // Disable the hire button so the player can't double-hire
        if (hireBtnGO != null)
        {
            var btn = hireBtnGO.GetComponent<Button>();
            if (btn != null) btn.interactable = false;

            // Change label to "HIRED"
            var lbl = hireBtnGO.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null)
            {
                lbl.text  = "HIRED";
                lbl.color = AccentCyan;
            }
            var img = hireBtnGO.GetComponent<Image>();
            if (img != null) img.color = new Color(0.05f, 0.14f, 0.10f, 1f);
        }
    }

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
        OnClosed?.Invoke();
    }

    // ── Portrait loading ──────────────────────────────────────────────────────

    private void LoadPortraitInto(Image img, string portraitId)
    {
        if (img == null) return;

        if (_portraitLib != null && !string.IsNullOrEmpty(portraitId))
        {
            int idx = _portraitLib.IndexOf(portraitId);
            if (idx >= 0 && idx < _portraitLib.Portraits.Length)
            {
                var tex = _portraitLib.Portraits[idx];
                if (tex != null)
                {
                    img.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f));
                    img.color = Color.white;
                    return;
                }
            }
        }

        img.sprite = null;
        img.color  = AccentCyan;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatRole(string role) => role switch
    {
        Constants.Crew.Roles.Captain   => "Captain",
        Constants.Crew.Roles.Pilot     => "Pilot",
        Constants.Crew.Roles.Engineer  => "Engineer",
        Constants.Crew.Roles.Scientist => "Scientist",
        Constants.Crew.Roles.Gunner    => "Gunner",
        Constants.Crew.Roles.Doctor    => "Doctor",
        Constants.Crew.Roles.Soldier   => "Soldier",
        _ when string.IsNullOrEmpty(role) => "Unassigned",
        _                              => role,
    };
}
