using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime controller for the Crew View panel.
///
/// ── Layout (built by GameSceneSetup.BuildCrewView) ───────────────────────
///
///   CrewView                       ← this MonoBehaviour's GameObject
///   ├─ CrewListPanel               ← left 38 % — scrollable crew buttons
///   │  └─ Scroll View
///   │     └─ Viewport
///   │        └─ Content            (VerticalLayoutGroup)
///   │           ├─ CrewRow_0       ← captain (always first)
///   │           └─ CrewRow_N…
///   ├─ Divider
///   └─ CrewDetailPanel
///      ├─ PortraitImage            ← 160×160, top-left (pivot 0,1)
///      ├─ CrewNameText             ← bold 42pt, left of level badge
///      ├─ LevelText                ← "Level N", top-right badge
///      ├─ CrewRoleText             ← italic 30pt, below name
///      ├─ XPLabel                  ← "EXPERIENCE", below role (right of portrait)
///      ├─ XPBar                    ← Slider (non-interactable), below XP label
///      ├─ XPText                   ← "N / M XP" overlaid on bar, right-aligned
///      ├─ SkillsDivider            ← full-width rule below portrait
///      ├─ SkillsHeader             ← "SKILLS"
///      └─ SkillsContainer          ← fills remaining height
///
/// References are resolved by name at Start() so the view works even when
/// SerializedField wiring from the editor build script doesn't survive a
/// scene save / domain reload.
/// </summary>
public class CrewViewController : MonoBehaviour
{
    // ── Resolved at Start() — no editor wiring needed ───────────────────────

    private Transform  _crewListContent;
    private Image      _detailPortrait;
    private TMP_Text   _detailNameText;
    private TMP_Text   _detailRoleText;
    private TMP_Text   _detailLevelText;
    private Slider     _detailXPBar;
    private TMP_Text   _detailXPText;
    private Transform  _skillsContainer;

    // ── Colours (match GameSceneSetup palette) ───────────────────────────────

    private static readonly Color TextWhite   = new Color(0.92f, 0.95f, 1.00f, 1f);
    private static readonly Color TextSubtle  = new Color(0.60f, 0.72f, 0.85f, 1f);
    private static readonly Color AccentCyan  = new Color(0.30f, 0.85f, 1.00f, 1f);
    private static readonly Color RowSelected = new Color(0.08f, 0.25f, 0.42f, 0.90f);
    private static readonly Color RowNormal   = new Color(0.05f, 0.10f, 0.18f, 0.80f);

    // ── State ────────────────────────────────────────────────────────────────

    private readonly List<Character>  _crew       = new List<Character>();
    private readonly List<GameObject> _rowButtons = new List<GameObject>();
    private int                       _selectedIdx = -1;
    private PortraitLibrary           _portraitLib;
    private bool                      _resolved;

    // ── Unity lifecycle ──────────────────────────────────────────────────────

    private void Start()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        // ResolveReferences first time if Start() hasn't run yet
        // (panel is active from the beginning of the scene, which it isn't, but be safe)
        if (!_resolved) ResolveReferences();
        Populate();
    }

    // ── Reference resolution by hierarchy path ───────────────────────────────

    private void ResolveReferences()
    {
        _resolved = true;

        var t = transform;

        // Crew list content (scroll view content root)
        var contentTf = t.Find("CrewListPanel/Scroll View/Viewport/Content");
        if (contentTf != null)
            _crewListContent = contentTf;
        else
            Debug.LogWarning("[CrewViewController] Could not find CrewListPanel/Scroll View/Viewport/Content");

        // Detail panel children
        var detail = t.Find("CrewDetailPanel");
        if (detail == null)
        {
            Debug.LogWarning("[CrewViewController] Could not find CrewDetailPanel");
            return;
        }

        _detailPortrait  = detail.Find("PortraitImage")?.GetComponent<Image>();
        _detailNameText  = detail.Find("CrewNameText")?.GetComponent<TMP_Text>();
        _detailRoleText  = detail.Find("CrewRoleText")?.GetComponent<TMP_Text>();
        _detailLevelText = detail.Find("LevelText")?.GetComponent<TMP_Text>();
        _detailXPBar     = detail.Find("XPBar")?.GetComponent<Slider>();
        _detailXPText    = detail.Find("XPText")?.GetComponent<TMP_Text>();
        _skillsContainer = detail.Find("SkillsContainer");

        if (_detailPortrait  == null) Debug.LogWarning("[CrewViewController] PortraitImage not found");
        if (_detailNameText  == null) Debug.LogWarning("[CrewViewController] CrewNameText not found");
        if (_detailRoleText  == null) Debug.LogWarning("[CrewViewController] CrewRoleText not found");
        if (_detailLevelText == null) Debug.LogWarning("[CrewViewController] LevelText not found");
        if (_detailXPBar     == null) Debug.LogWarning("[CrewViewController] XPBar not found");
        if (_detailXPText    == null) Debug.LogWarning("[CrewViewController] XPText not found");
        if (_skillsContainer == null) Debug.LogWarning("[CrewViewController] SkillsContainer not found");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Populate()
    {
        _crew.Clear();
        foreach (var go in _rowButtons)
            if (go != null) Destroy(go);
        _rowButtons.Clear();
        _selectedIdx = -1;

        if (_portraitLib == null)
            _portraitLib = Resources.Load<PortraitLibrary>("PortraitLibrary");

        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null)
        {
            ShowEmptyDetail();
            return;
        }

        var allCrew = gm.Database.GetCrewForSave(gm.CurrentSave.Id);
        if (allCrew == null || allCrew.Count == 0)
        {
            ShowEmptyDetail();
            return;
        }

        // Captain first, then others
        foreach (var c in allCrew)
            if (c.IsPlayerCaptain) _crew.Insert(0, c);
            else                   _crew.Add(c);

        if (_crewListContent != null)
        {
            for (int i = 0; i < _crew.Count; i++)
                BuildCrewRow(_crew[i], i);
        }

        if (_crew.Count > 0)
            SelectCrew(0);
    }

    // ── List row builder ─────────────────────────────────────────────────────

    private void BuildCrewRow(Character crew, int index)
    {
        var rowGO = new GameObject($"CrewRow_{index}", typeof(RectTransform));
        rowGO.transform.SetParent(_crewListContent, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = RowNormal;

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

        int capturedIndex = index;
        btn.onClick.AddListener(() => SelectCrew(capturedIndex));

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
        LoadPortraitInto(portraitImg, crew.PortraitId);
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

        // Name
        var nameGO  = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(textCol.transform, false);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = crew.IsPlayerCaptain ? $"{crew.Name} (You)" : crew.Name;
        nameTMP.fontSize         = 28f;
        nameTMP.color            = TextWhite;
        nameTMP.fontStyle        = FontStyles.Bold;
        nameTMP.alignment        = TextAlignmentOptions.Left;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode     = TextOverflowModes.Ellipsis;
        nameGO.AddComponent<LayoutElement>().preferredHeight = 36f;

        // Role
        var roleGO  = new GameObject("Role", typeof(RectTransform));
        roleGO.transform.SetParent(textCol.transform, false);
        var roleTMP = roleGO.AddComponent<TextMeshProUGUI>();
        roleTMP.text      = FormatRole(crew.Role);
        roleTMP.fontSize  = 22f;
        roleTMP.color     = AccentCyan;
        roleTMP.alignment = TextAlignmentOptions.Left;
        roleGO.AddComponent<LayoutElement>().preferredHeight = 28f;

        _rowButtons.Add(rowGO);
    }

    // ── Selection ────────────────────────────────────────────────────────────

    private void SelectCrew(int index)
    {
        if (index < 0 || index >= _crew.Count) return;

        if (_selectedIdx >= 0 && _selectedIdx < _rowButtons.Count)
        {
            var prevImg = _rowButtons[_selectedIdx]?.GetComponent<Image>();
            if (prevImg != null) prevImg.color = RowNormal;
        }

        _selectedIdx = index;
        var selImg = _rowButtons[index]?.GetComponent<Image>();
        if (selImg != null) selImg.color = RowSelected;

        ShowDetail(_crew[index]);
    }

    // ── Detail panel ─────────────────────────────────────────────────────────

    private void ShowDetail(Character crew)
    {
        if (_detailPortrait != null)
        {
            LoadPortraitInto(_detailPortrait, crew.PortraitId);
            _detailPortrait.preserveAspect = true;
        }

        if (_detailNameText != null)
            _detailNameText.text = crew.IsPlayerCaptain ? $"{crew.Name} (You)" : crew.Name;

        if (_detailRoleText != null)
            _detailRoleText.text = FormatRole(crew.Role);

        if (_detailLevelText != null)
            _detailLevelText.text = $"Level {crew.Level}";

        if (_detailXPBar != null)
        {
            _detailXPBar.minValue = 0f;
            _detailXPBar.maxValue = Mathf.Max(1f, crew.ExperienceToNextLevel);
            _detailXPBar.value    = crew.ExperiencePoints;
        }

        if (_detailXPText != null)
            _detailXPText.text = $"{crew.ExperiencePoints} / {crew.ExperienceToNextLevel} XP";

        if (_skillsContainer != null)
        {
            for (int i = _skillsContainer.childCount - 1; i >= 0; i--)
                Destroy(_skillsContainer.GetChild(i).gameObject);

            var skills = crew.Skills;
            bool anySkill = false;
            foreach (var skillName in Constants.Skills.All)
            {
                skills.TryGetValue(skillName, out int rank);
                if (rank > 0)
                {
                    BuildSkillRow(skillName, rank);
                    anySkill = true;
                }
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
    }

    private void ShowEmptyDetail()
    {
        if (_detailNameText  != null) _detailNameText.text  = "—";
        if (_detailRoleText  != null) _detailRoleText.text  = "";
        if (_detailLevelText != null) _detailLevelText.text = "";
        if (_detailXPText    != null) _detailXPText.text    = "";
        if (_detailXPBar     != null) _detailXPBar.value    = 0f;
    }

    // ── Skill row ────────────────────────────────────────────────────────────

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

        // Skill label — fixed-width column so dots align across all rows
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

        // Rating dots: filled = rank, hollow = rest up to MaxRank
        const int   MaxRank  = 10;
        const float DotSize  = 16f;

        for (int i = 0; i < MaxRank; i++)
        {
            var dotGO  = new GameObject($"Dot_{i}", typeof(RectTransform));
            dotGO.transform.SetParent(rowGO.transform, false);
            var dotImg = dotGO.AddComponent<Image>();
            dotImg.color = i < rank
                ? AccentCyan
                : new Color(0.25f, 0.35f, 0.45f, 0.70f);
            var dotLE = dotGO.AddComponent<LayoutElement>();
            dotLE.preferredWidth  = DotSize;
            dotLE.preferredHeight = DotSize;
            dotLE.flexibleWidth   = 0f;
        }
    }

    // ── Portrait loading ─────────────────────────────────────────────────────

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
                    img.color  = Color.white;
                    return;
                }
            }
        }

        // Fallback: cyan square
        img.sprite = null;
        img.color  = AccentCyan;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FormatRole(string role) => role switch
    {
        Constants.Crew.Roles.Captain        => "Captain",
        Constants.Crew.Roles.Pilot          => "Pilot",
        Constants.Crew.Roles.Engineer       => "Engineer",
        Constants.Crew.Roles.Scientist      => "Scientist",
        Constants.Crew.Roles.WeaponsOfficer => "Weapons Officer",
        Constants.Crew.Roles.Doctor         => "Doctor",
        Constants.Crew.Roles.Soldier        => "Soldier",
        _                                   => role,
    };
}
