using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime controller for the Crew View panel.
///
/// ── Tab layout ────────────────────────────────────────────────────────────
///
///   CrewView
///   ├─ CrewTabBar                  ← 52 px strip at top; two tab buttons
///   │  ├─ CrewListTabButton
///   │  └─ AssignmentsTabButton
///   ├─ CrewContent                 ← list + detail (hidden when Assignments tab active)
///   │  ├─ CrewListPanel            ← left 38 % — scrollable crew buttons
///   │  ├─ Divider
///   │  └─ CrewDetailPanel
///   └─ CrewAssignmentPanel         ← assignment slots view (wired to CrewAssignmentViewController)
///
/// ── Recruitment mode ─────────────────────────────────────────────────────
///   Activated by SystemViewController when arriving at a space station.
///   The left list shows 2–3 generated candidates instead of the ship crew.
///   The right panel shows the selected candidate's details plus a HIRE button.
///   Exiting the view (navigating to another tab) ends recruitment mode and
///   restores the normal crew display on next open.
/// </summary>
public class CrewViewController : MonoBehaviour
{
    // ── Wired by GameSceneSetup ──────────────────────────────────────────────

    [SerializeField] private LevelUpController          levelUpController;
    [SerializeField] private Button                     crewListTabButton;
    [SerializeField] private Button                     assignmentsTabButton;
    [SerializeField] private GameObject                 crewContent;           // list + detail container
    [SerializeField] private CrewAssignmentViewController assignmentViewController;

    // ── Resolved at Start() — no editor wiring needed ───────────────────────

    private Transform  _crewListContent;
    private Image      _detailPortrait;
    private TMP_Text   _detailNameText;
    private TMP_Text   _detailRoleText;
    private TMP_Text   _detailLevelText;
    private Slider     _detailXPBar;
    private TMP_Text   _detailXPText;
    private Transform  _skillsContainer;
    private GameObject _levelUpButtonGO;
    private GameObject _hireButtonGO;

    // ── Colours (match GameSceneSetup palette) ───────────────────────────────

    private static readonly Color TextWhite   = new Color(0.92f, 0.95f, 1.00f, 1f);
    private static readonly Color TextSubtle  = new Color(0.60f, 0.72f, 0.85f, 1f);
    private static readonly Color AccentCyan  = new Color(0.30f, 0.85f, 1.00f, 1f);
    private static readonly Color RowSelected = new Color(0.08f, 0.25f, 0.42f, 0.90f);
    private static readonly Color RowNormal   = new Color(0.05f, 0.10f, 0.18f, 0.80f);
    private static readonly Color RowHired    = new Color(0.04f, 0.18f, 0.10f, 0.80f);

    // ── State ────────────────────────────────────────────────────────────────

    private readonly List<Character>  _crew        = new List<Character>();
    private readonly List<Character>  _candidates  = new List<Character>();
    private readonly List<GameObject> _rowButtons  = new List<GameObject>();
    private readonly HashSet<int>     _hired       = new HashSet<int>();
    private int                       _selectedIdx  = -1;
    private PortraitLibrary           _portraitLib;
    private bool                      _resolved;

    // ── Recruitment mode ─────────────────────────────────────────────────────

    private bool   _isRecruitmentMode;
    private string _stationName;
    private int    _stationPoiId;

    /// <summary>
    /// Called when the header text should change.
    /// SystemViewController wires this to update its systemNameText.
    /// </summary>
    public System.Action<string> OnHeaderTextChanged;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        ResolveReferences();
        crewListTabButton?.onClick.AddListener(ShowCrewList);
        assignmentsTabButton?.onClick.AddListener(ShowAssignments);
        ShowCrewList();  // default to crew list tab
    }

    private void OnEnable()
    {
        if (!_resolved) ResolveReferences();
        Populate();
    }

    private void OnDisable()
    {
        // Leaving the crew panel always ends recruitment mode silently.
        // The header is reset by SystemViewController when it hides this panel.
        _isRecruitmentMode = false;
        _candidates.Clear();
        _hired.Clear();
    }

    // ── Reference resolution ─────────────────────────────────────────────────

    private void ResolveReferences()
    {
        _resolved = true;
        // Panels now live under CrewContent (a child added by GameSceneSetup for tab switching).
        // Fall back to searching directly on transform for backwards compatibility.
        var root = crewContent != null ? crewContent.transform : transform;

        var contentTf = root.Find("CrewListPanel/Scroll View/Viewport/Content");
        if (contentTf != null)
            _crewListContent = contentTf;
        else
            Debug.LogWarning("[CrewViewController] Could not find CrewListPanel/Scroll View/Viewport/Content");

        var detail = root.Find("CrewDetailPanel");
        if (detail == null) { Debug.LogWarning("[CrewViewController] Could not find CrewDetailPanel"); return; }

        _detailPortrait  = detail.Find("PortraitImage")?.GetComponent<Image>();
        _detailNameText  = detail.Find("CrewNameText")?.GetComponent<TMP_Text>();
        _detailRoleText  = detail.Find("CrewRoleText")?.GetComponent<TMP_Text>();
        _detailLevelText = detail.Find("LevelText")?.GetComponent<TMP_Text>();
        _detailXPBar     = detail.Find("XPBar")?.GetComponent<Slider>();
        _detailXPText    = detail.Find("XPText")?.GetComponent<TMP_Text>();
        _skillsContainer = detail.Find("SkillsContainer");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates recruitment mode. Loads persisted recruits for this station if they
    /// are still fresh; otherwise clears them and generates a new pool.
    /// </summary>
    public void StartRecruitmentMode(PointOfInterest poi, int captainLevel)
    {
        _isRecruitmentMode = true;
        _stationName       = poi.Name;
        _stationPoiId      = poi.Id;
        _hired.Clear();

        if (_portraitLib == null)
            _portraitLib = Resources.Load<PortraitLibrary>("PortraitLibrary");

        var gm = GameManager.Instance;
        float daysPassed = gm?.CurrentSave?.DaysPassed ?? 0f;
        int saveId     = gm?.CurrentSave?.Id ?? 0;

        bool needsRefresh = poi.RecruitRefreshDay < 0
            || (daysPassed - poi.RecruitRefreshDay) >= Constants.Recruitment.RecruitRefreshDays;

        if (needsRefresh)
        {
            // Wipe the old pool and generate a fresh one
            if (gm?.Database != null) gm.Database.ClearStationRecruits(poi.Id);
            GenerateAndPersistCandidates(captainLevel, poi, saveId, daysPassed, gm);
        }
        else
        {
            // Load the persisted pool
            _candidates.Clear();
            if (gm?.Database != null)
                _candidates.AddRange(gm.Database.GetStationRecruits(poi.Id));
        }

        OnHeaderTextChanged?.Invoke($"{poi.Name} — Crew Recruitment");
        Populate();
    }

    // ── Tab switching ─────────────────────────────────────────────────────────

    public void ShowCrewList()
    {
        if (crewContent != null)             crewContent.SetActive(true);
        if (assignmentViewController != null) assignmentViewController.gameObject.SetActive(false);
    }

    public void ShowAssignments()
    {
        if (crewContent != null)             crewContent.SetActive(false);
        if (assignmentViewController != null)
        {
            assignmentViewController.gameObject.SetActive(true);
            assignmentViewController.Populate();
        }
    }

    public void Populate()
    {
        _crew.Clear();
        foreach (var go in _rowButtons)
            if (go != null) Destroy(go);
        _rowButtons.Clear();
        _selectedIdx = -1;

        if (_portraitLib == null)
            _portraitLib = Resources.Load<PortraitLibrary>("PortraitLibrary");

        if (_isRecruitmentMode)
            PopulateRecruitment();
        else
            PopulateCrew();
    }

    // ── List population ───────────────────────────────────────────────────────

    private void PopulateCrew()
    {
        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null) { ShowEmptyDetail(); return; }

        var allCrew = gm.Database.GetCrewForSave(gm.CurrentSave.Id);
        if (allCrew == null || allCrew.Count == 0) { ShowEmptyDetail(); return; }

        foreach (var c in allCrew)
            if (c.IsPlayerCaptain) _crew.Insert(0, c);
            else                   _crew.Add(c);

        if (_crewListContent != null)
            for (int i = 0; i < _crew.Count; i++)
                BuildListRow(_crew[i], i, hired: false);

        if (_crew.Count > 0) SelectRow(0);
    }

    private void PopulateRecruitment()
    {
        if (_crewListContent != null)
            for (int i = 0; i < _candidates.Count; i++)
                BuildListRow(_candidates[i], i, hired: _hired.Contains(i));

        if (_candidates.Count > 0) SelectRow(0);
        else ShowEmptyDetail();
    }

    // ── Candidate generation ──────────────────────────────────────────────────

    private void GenerateAndPersistCandidates(int captainLevel, PointOfInterest poi,
                                               int saveId, float daysPassed, GameManager gm)
    {
        _candidates.Clear();

        // Specialties guide skill distribution; generated characters are unassigned (Role = "")
        string[] specialties =
        {
            Constants.Crew.Roles.Pilot,
            Constants.Crew.Roles.Engineer,
            Constants.Crew.Roles.Scientist,
            Constants.Crew.Roles.Gunner,
            Constants.Crew.Roles.Doctor,
            Constants.Crew.Roles.Soldier,
        };

        var shuffled = new List<string>(specialties);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        // Portrait pool: exclude portraits already used by hired crew and captain.
        var usedPortraits      = CollectUsedPortraits(gm);
        var availablePortraits = new List<string>();
        if (_portraitLib != null && _portraitLib.IsValid)
        {
            foreach (var fn in _portraitLib.FileNames)
                if (!usedPortraits.Contains(fn))
                    availablePortraits.Add(fn);

            for (int i = availablePortraits.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (availablePortraits[i], availablePortraits[j]) = (availablePortraits[j], availablePortraits[i]);
            }
        }

        int count = Random.Range(2, 4);
        for (int i = 0; i < count; i++)
        {
            int level     = Random.Range(1, Mathf.Max(2, captainLevel + 1));
            var candidate = CharacterFactory.CreateRandomCrewMember(shuffled[i], level);

            if (availablePortraits.Count > 0)
            {
                candidate.PortraitId = availablePortraits[0];
                availablePortraits.RemoveAt(0);
            }

            // Persist as a station recruit; Id is assigned by the DB after Insert.
            candidate.SaveGameId    = saveId;
            candidate.StationPoiId  = poi.Id;
            if (gm?.Database != null)
                gm.Database.Characters.Insert(candidate);

            _candidates.Add(candidate);
        }

        // Record when this pool was generated so we know when to refresh it.
        poi.RecruitRefreshDay = Mathf.FloorToInt(daysPassed);
        if (gm?.Database != null)
            gm.Database.POIs.Update(poi);
    }

    /// <summary>
    /// Returns the set of portrait IDs in use by the captain and all hired crew.
    /// Station recruits (StationPoiId > 0) are excluded — they haven't joined the ship.
    /// </summary>
    private HashSet<string> CollectUsedPortraits(GameManager gm = null)
    {
        var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        gm = gm ?? GameManager.Instance;
        if (gm?.CurrentSave == null) return used;

        if (gm.PlayerCaptain != null && !string.IsNullOrEmpty(gm.PlayerCaptain.PortraitId))
            used.Add(gm.PlayerCaptain.PortraitId);

        var crew = gm.Database.GetCrewForSave(gm.CurrentSave.Id);
        if (crew != null)
            foreach (var c in crew)
                if (!string.IsNullOrEmpty(c.PortraitId))
                    used.Add(c.PortraitId);

        return used;
    }

    // ── List row builder ──────────────────────────────────────────────────────

    private void BuildListRow(Character person, int index, bool hired)
    {
        var rowGO = new GameObject($"CrewRow_{index}", typeof(RectTransform));
        rowGO.transform.SetParent(_crewListContent, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = hired ? RowHired : RowNormal;

        var le = rowGO.AddComponent<LayoutElement>();
        le.preferredHeight = 90f;
        le.flexibleWidth   = 1f;

        var btn = rowGO.AddComponent<Button>();
        btn.targetGraphic = rowImg;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        colors.pressedColor     = new Color(0.80f, 0.80f, 0.80f, 1f);
        btn.colors = colors;

        int captured = index;
        btn.onClick.AddListener(() => SelectRow(captured));

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(10, 10, 8, 8);
        hlg.spacing                = 12f;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childAlignment         = TextAnchor.MiddleLeft;

        // Portrait thumbnail
        var portraitGO  = new GameObject("Portrait", typeof(RectTransform));
        portraitGO.transform.SetParent(rowGO.transform, false);
        var portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.preserveAspect = true;
        LoadPortraitInto(portraitImg, person.PortraitId);
        var portraitLE = portraitGO.AddComponent<LayoutElement>();
        portraitLE.preferredWidth  = 74f;
        portraitLE.preferredHeight = 74f;
        portraitLE.flexibleWidth   = 0f;

        // Text column
        var textCol = new GameObject("TextCol", typeof(RectTransform));
        textCol.transform.SetParent(rowGO.transform, false);
        textCol.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var textVLG = textCol.AddComponent<VerticalLayoutGroup>();
        textVLG.childControlWidth      = true;
        textVLG.childControlHeight     = true;
        textVLG.childForceExpandWidth  = true;
        textVLG.childForceExpandHeight = false;
        textVLG.childAlignment         = TextAnchor.MiddleLeft;
        textVLG.spacing                = 4f;

        var nameGO  = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(textCol.transform, false);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text               = hired ? $"{person.Name}  ✓" : (person.IsPlayerCaptain ? $"{person.Name} (You)" : person.Name);
        nameTMP.fontSize           = 28f;
        nameTMP.color              = hired ? AccentCyan : TextWhite;
        nameTMP.fontStyle          = FontStyles.Bold;
        nameTMP.alignment          = TextAlignmentOptions.Left;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode       = TextOverflowModes.Ellipsis;
        nameGO.AddComponent<LayoutElement>().preferredHeight = 36f;

        var roleGO  = new GameObject("Role", typeof(RectTransform));
        roleGO.transform.SetParent(textCol.transform, false);
        var roleTMP = roleGO.AddComponent<TextMeshProUGUI>();
        roleTMP.text      = FormatRole(person.Role);
        roleTMP.fontSize  = 22f;
        roleTMP.color     = AccentCyan;
        roleTMP.alignment = TextAlignmentOptions.Left;
        roleGO.AddComponent<LayoutElement>().preferredHeight = 28f;

        _rowButtons.Add(rowGO);
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    private void SelectRow(int index)
    {
        var list = _isRecruitmentMode ? _candidates : _crew;
        if (index < 0 || index >= list.Count) return;

        if (_selectedIdx >= 0 && _selectedIdx < _rowButtons.Count)
        {
            var prevImg = _rowButtons[_selectedIdx]?.GetComponent<Image>();
            if (prevImg != null)
                prevImg.color = (_isRecruitmentMode && _hired.Contains(_selectedIdx)) ? RowHired : RowNormal;
        }

        _selectedIdx = index;
        var selImg = _rowButtons[index]?.GetComponent<Image>();
        if (selImg != null) selImg.color = RowSelected;

        ShowDetail(list[index]);
    }

    // ── Detail panel ──────────────────────────────────────────────────────────

    private void ShowDetail(Character person)
    {
        if (_detailPortrait != null)
        {
            LoadPortraitInto(_detailPortrait, person.PortraitId);
            _detailPortrait.preserveAspect = true;
        }

        if (_detailNameText  != null)
            _detailNameText.text  = person.IsPlayerCaptain ? $"{person.Name} (You)" : person.Name;
        if (_detailRoleText  != null)
            _detailRoleText.text  = FormatRole(person.Role);
        if (_detailLevelText != null)
            _detailLevelText.text = $"Level {person.Level}";

        // XP row — hidden in recruitment mode (candidates haven't joined yet)
        bool showXP = !_isRecruitmentMode;
        if (_detailXPBar != null)
        {
            _detailXPBar.gameObject.SetActive(showXP);
            if (showXP)
            {
                _detailXPBar.minValue = 0f;
                _detailXPBar.maxValue = Mathf.Max(1f, person.ExperienceToNextLevel);
                _detailXPBar.value    = person.ExperiencePoints;
            }
        }
        if (_detailXPText != null)
        {
            _detailXPText.gameObject.SetActive(showXP);
            if (showXP)
                _detailXPText.text = $"{person.ExperiencePoints} / {person.ExperienceToNextLevel} XP";
        }

        // Find XPLabel sibling too (it's built above XPBar in the hierarchy)
        var xpLabel = _detailXPBar?.transform.parent?.Find("XPLabel")?.gameObject;
        if (xpLabel != null) xpLabel.SetActive(showXP);

        // Skills
        if (_skillsContainer != null)
        {
            for (int i = _skillsContainer.childCount - 1; i >= 0; i--)
                Destroy(_skillsContainer.GetChild(i).gameObject);

            bool anySkill = false;
            foreach (var skillName in Constants.Skills.All)
            {
                person.Skills.TryGetValue(skillName, out int rank);
                if (rank > 0) { BuildSkillRow(skillName, rank); anySkill = true; }
            }

            if (!anySkill)
            {
                var noSkillsGO = new GameObject("NoSkills", typeof(RectTransform));
                noSkillsGO.transform.SetParent(_skillsContainer, false);
                var tmp = noSkillsGO.AddComponent<TextMeshProUGUI>();
                tmp.text      = "No skills yet.";
                tmp.fontSize  = 24f;
                tmp.color     = TextSubtle;
                tmp.alignment = TextAlignmentOptions.Left;
                noSkillsGO.AddComponent<LayoutElement>().preferredHeight = 32f;
            }
        }

        if (_isRecruitmentMode)
            ShowOrHideHireButton(person, _selectedIdx);
        else
            ShowOrHideLevelUpButton(person);
    }

    // ── Hire button (recruitment mode) ────────────────────────────────────────

    private void ShowOrHideHireButton(Character candidate, int index)
    {
        // Hide level-up button
        if (_levelUpButtonGO != null) _levelUpButtonGO.SetActive(false);

        var detail = _skillsContainer?.parent;
        if (detail == null) return;

        if (_hireButtonGO == null)
            _hireButtonGO = BuildActionButton(detail, "HireButton",
                                              new Color(0.08f, 0.38f, 0.22f, 1f),
                                              new Color(0.12f, 0.55f, 0.32f, 1f),
                                              new Color(0.04f, 0.22f, 0.12f, 1f));

        _hireButtonGO.SetActive(true);

        bool alreadyHired = _hired.Contains(index);
        var hireBtn = _hireButtonGO.GetComponent<Button>();
        var hireLbl = _hireButtonGO.GetComponentInChildren<TextMeshProUGUI>();
        var hireBg  = _hireButtonGO.GetComponent<Image>();

        if (hireLbl != null) hireLbl.text  = alreadyHired ? "HIRED" : "HIRE";
        if (hireLbl != null) hireLbl.color = alreadyHired ? AccentCyan : TextWhite;
        if (hireBg  != null) hireBg.color  = alreadyHired
            ? new Color(0.04f, 0.14f, 0.10f, 1f)
            : new Color(0.08f, 0.38f, 0.22f, 1f);

        if (hireBtn != null)
        {
            hireBtn.interactable = !alreadyHired;
            hireBtn.onClick.RemoveAllListeners();
            if (!alreadyHired)
            {
                int capturedIdx = index;
                var capturedCandidate = candidate;
                hireBtn.onClick.AddListener(() => OnHireClicked(capturedCandidate, capturedIdx));
            }
        }
    }

    private void OnHireClicked(Character candidate, int index)
    {
        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null) { Debug.LogWarning("[CrewViewController] No save — cannot hire."); return; }

        // Promote from station recruit to active crew by clearing StationPoiId.
        // The record already exists in the DB (inserted during generation), so Update, not Insert.
        candidate.StationPoiId = 0;
        try
        {
            gm.Database.Characters.Update(candidate);
            Debug.Log($"[CrewViewController] Hired {candidate.Name} ({candidate.Role} Lv{candidate.Level}).");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CrewViewController] Hire failed: {e.Message}");
            return;
        }

        _hired.Add(index);

        // Update the list row to show hired state
        if (index < _rowButtons.Count && _rowButtons[index] != null)
        {
            var rowImg = _rowButtons[index].GetComponent<Image>();
            if (rowImg != null) rowImg.color = RowHired;

            var nameTmp = _rowButtons[index].GetComponentInChildren<TextMeshProUGUI>();
            if (nameTmp != null)
            {
                nameTmp.text  = $"{candidate.Name}  ✓";
                nameTmp.color = AccentCyan;
            }
        }

        // Refresh the hire button state
        ShowOrHideHireButton(candidate, index);
    }

    // ── Level Up button (normal mode) ─────────────────────────────────────────

    private void ShowOrHideLevelUpButton(Character crew)
    {
        // Hide hire button
        if (_hireButtonGO != null) _hireButtonGO.SetActive(false);

        bool hasPoints = crew.AvailableSkillPoints > 0;
        var detail = _skillsContainer?.parent;
        if (detail == null) return;

        if (_levelUpButtonGO == null && hasPoints)
            _levelUpButtonGO = BuildActionButton(detail, "LevelUpButton",
                                                 new Color(0.20f, 0.55f, 0.10f, 1f),
                                                 new Color(0.28f, 0.72f, 0.15f, 1f),
                                                 new Color(0.10f, 0.32f, 0.06f, 1f));

        if (_levelUpButtonGO != null)
        {
            _levelUpButtonGO.SetActive(hasPoints);
            if (hasPoints)
            {
                var lbl = _levelUpButtonGO.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) lbl.text = $"LEVEL UP  ({crew.AvailableSkillPoints} pts)";

                var btn = _levelUpButtonGO.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    var captured = crew;
                    btn.onClick.AddListener(() => OpenLevelUp(captured));
                }
            }
        }
    }

    private void OpenLevelUp(Character crew)
    {
        if (levelUpController == null) { Debug.LogWarning("[CrewViewController] LevelUpController not wired."); return; }
        levelUpController.OnConfirmed = () => Populate();
        levelUpController.ShowLevelUp(crew);
    }

    // ── Shared action button builder ──────────────────────────────────────────

    private static GameObject BuildActionButton(Transform parent, string goName,
                                                Color bg, Color highlight, Color pressed)
    {
        var go = new GameObject(goName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(24f,  16f);
        rt.offsetMax = new Vector2(-24f, 64f);

        var img = go.AddComponent<Image>();
        img.color = bg;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.highlightedColor = highlight;
        c.pressedColor     = pressed;
        btn.colors = c;

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(go.transform, false);
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text      = goName;
        lbl.fontSize  = 26f;
        lbl.color     = new Color(0.92f, 0.95f, 1f, 1f);
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.fontStyle = FontStyles.Bold;
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;

        return go;
    }

    private void ShowEmptyDetail()
    {
        if (_detailNameText  != null) _detailNameText.text  = "—";
        if (_detailRoleText  != null) _detailRoleText.text  = "";
        if (_detailLevelText != null) _detailLevelText.text = "";
        if (_detailXPText    != null) _detailXPText.text    = "";
        if (_detailXPBar     != null) _detailXPBar.value    = 0f;
    }

    // ── Skill row ─────────────────────────────────────────────────────────────

    private void BuildSkillRow(string skillName, int rank)
    {
        var rowGO = new GameObject($"Skill_{skillName}", typeof(RectTransform));
        rowGO.transform.SetParent(_skillsContainer, false);

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.spacing                = 10f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        rowGO.AddComponent<LayoutElement>().preferredHeight = 34f;

        var labelGO  = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rowGO.transform, false);
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = skillName;
        labelTMP.fontSize  = 24f;
        labelTMP.color     = TextSubtle;
        labelTMP.alignment = TextAlignmentOptions.Left;
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.preferredWidth  = 200f;
        labelLE.preferredHeight = 30f;

        const int   MaxRank = 10;
        const float DotSize = 16f;

        for (int i = 0; i < MaxRank; i++)
        {
            var dotGO  = new GameObject($"Dot_{i}", typeof(RectTransform));
            dotGO.transform.SetParent(rowGO.transform, false);
            dotGO.AddComponent<Image>().color = i < rank
                ? AccentCyan
                : new Color(0.25f, 0.35f, 0.45f, 0.70f);
            var dotLE = dotGO.AddComponent<LayoutElement>();
            dotLE.preferredWidth  = DotSize;
            dotLE.preferredHeight = DotSize;
            dotLE.flexibleWidth   = 0f;
        }
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
                    img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    img.color  = Color.white;
                    return;
                }
            }
        }
        img.sprite = null;
        img.color  = AccentCyan;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static string FormatRole(string role) => role switch
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
