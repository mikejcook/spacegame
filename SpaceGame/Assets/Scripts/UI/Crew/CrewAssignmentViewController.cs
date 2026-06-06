using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the Crew Assignments panel — a ship-layout view showing which crew
/// member is filling each role slot.
///
/// ── Layout (built by GameSceneSetup.BuildCrewAssignmentPanel) ─────────────
///
///   CrewAssignmentPanel
///   ├─ AssignmentBackground
///   ├─ LeftSlotColumn           (Pilot / Engineer / Scientist)
///   ├─ RightSlotColumn          (Gunner / Doctor / Soldier)
///   └─ CaptainSlot              (always filled; centred where ship image was)
///
/// Each slot (built by BuildAssignmentSlot) contains:
///   ├─ RoleLabel    (TMP — role name at top)
///   ├─ PortraitImage
///   └─ NameText     (TMP — crew name or "— VACANT —")
///
/// ── Assignment picker ─────────────────────────────────────────────────────
///   Tapping a non-captain slot opens AssignmentPickerPanel, which lists all
///   crew members with "Assign" / "Unassign" options.
///   Saving writes Character.Role to the DB and refreshes the slot display.
/// </summary>
public class CrewAssignmentViewController : MonoBehaviour
{
    // ── Slot references wired by GameSceneSetup ───────────────────────────────

    [System.Serializable]
    public class SlotUI
    {
        public string     role;
        public GameObject root;
        public Image      portrait;
        public TMP_Text   nameText;
        public Button     button;
    }

    [SerializeField] public SlotUI captainSlot;
    [SerializeField] public SlotUI[] assignmentSlots = new SlotUI[6];   // matches Constants.Crew.AssignmentSlots

    // ── Picker panel (built by GameSceneSetup) ────────────────────────────────

    [SerializeField] private GameObject pickerPanel;
    [SerializeField] private Transform  pickerContent;   // VLG content for crew rows
    [SerializeField] private TMP_Text   pickerRoleLabel;
    [SerializeField] private Button     pickerCloseButton;

    private CanvasGroup _pickerCG;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color TextWhite   = new Color(0.92f, 0.95f, 1.00f, 1f);
    private static readonly Color TextSubtle  = new Color(0.60f, 0.72f, 0.85f, 1f);
    private static readonly Color AccentCyan  = new Color(0.30f, 0.85f, 1.00f, 1f);
    private static readonly Color VacantGrey  = new Color(0.35f, 0.40f, 0.50f, 1f);
    private static readonly Color RowNormal   = new Color(0.05f, 0.10f, 0.18f, 0.80f);
    private static readonly Color RowSelected = new Color(0.08f, 0.25f, 0.42f, 0.90f);

    // ── State ─────────────────────────────────────────────────────────────────

    private PortraitLibrary _portraitLib;
    private string          _pickerTargetRole;
    private List<GameObject> _pickerRows = new List<GameObject>();

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // Cache the CanvasGroup — picker is always Active; we show/hide via alpha
        // to keep the Shift MainButton Animator continuously bound (avoids stuck-highlight bug).
        if (pickerPanel != null)
            _pickerCG = pickerPanel.GetComponent<CanvasGroup>();

        pickerCloseButton?.onClick.AddListener(HidePicker);
        HidePicker();
    }

    private void OnEnable()
    {
        Populate();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Refreshes all slot displays from the current DB state.</summary>
    public void Populate()
    {
        if (_portraitLib == null)
            _portraitLib = Resources.Load<PortraitLibrary>("PortraitLibrary");

        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null) return;

        // Captain slot — always the player captain
        PopulateCaptainSlot(gm.PlayerCaptain);

        // Assignment slots
        for (int i = 0; i < assignmentSlots.Length; i++)
        {
            var slot = assignmentSlots[i];
            if (slot == null) continue;

            var assigned = gm.Database?.GetCrewByRole(gm.CurrentSave.Id, slot.role);
            PopulateSlot(slot, assigned);
        }
    }

    // ── Slot population ───────────────────────────────────────────────────────

    private void PopulateCaptainSlot(Character captain)
    {
        if (captainSlot == null) return;
        PopulateSlot(captainSlot, captain);
        // Captain button does nothing (role is fixed)
        if (captainSlot.button != null)
            captainSlot.button.interactable = false;
    }

    private void PopulateSlot(SlotUI slot, Character crew)
    {
        bool filled = crew != null;

        if (slot.nameText != null)
        {
            slot.nameText.text  = filled ? crew.Name : "— VACANT —";
            slot.nameText.color = filled ? TextWhite : VacantGrey;
        }

        if (slot.portrait != null)
        {
            if (filled)
            {
                LoadPortraitInto(slot.portrait, crew.PortraitId);
                slot.portrait.color = Color.white;
            }
            else
            {
                slot.portrait.sprite = null;
                slot.portrait.color  = new Color(0.15f, 0.20f, 0.30f, 1f);
            }
        }

        // Wire click — skip for captain (handled separately)
        if (slot.button != null && slot.role != Constants.Crew.Roles.Captain)
        {
            slot.button.onClick.RemoveAllListeners();
            string capturedRole = slot.role;
            slot.button.onClick.AddListener(() => OpenPicker(capturedRole));
        }
    }

    // ── Picker ────────────────────────────────────────────────────────────────

    private void OpenPicker(string role)
    {
        _pickerTargetRole = role;

        if (pickerRoleLabel != null)
            pickerRoleLabel.text = CrewViewController.FormatRole(role).ToUpper();

        BuildPickerRows(role);

        if (_pickerCG != null)
        {
            _pickerCG.alpha          = 1f;
            _pickerCG.blocksRaycasts = true;
            _pickerCG.interactable   = true;
        }

        // Snap the Shift MainButton CLOSE button to its rest state.
        // The Animator is already bound (panel was never disabled), so Play is reliable here.
        if (pickerCloseButton != null)
        {
            var anim = pickerCloseButton.GetComponent<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null)
            {
                anim.Play("Normal", 0, 1.0f);
                anim.Update(0f);
            }
        }
    }

    private void HidePicker()
    {
        if (_pickerCG != null)
        {
            _pickerCG.alpha          = 0f;
            _pickerCG.blocksRaycasts = false;
            _pickerCG.interactable   = false;
        }
        ClearPickerRows();
    }

    private void BuildPickerRows(string role)
    {
        ClearPickerRows();
        if (pickerContent == null) return;

        var gm = GameManager.Instance;
        if (gm?.CurrentSave == null) return;

        var allCrew = gm.Database?.GetCrewForSave(gm.CurrentSave.Id);
        if (allCrew == null) return;

        // Currently assigned character (may be null)
        var currentlyAssigned = gm.Database?.GetCrewByRole(gm.CurrentSave.Id, role);

        foreach (var crew in allCrew)
        {
            if (crew.IsPlayerCaptain) continue;  // captain is never re-assignable

            bool isAssignedHere = currentlyAssigned != null && crew.Id == currentlyAssigned.Id;
            bool isAssignedElsewhere = !string.IsNullOrEmpty(crew.Role) && crew.Role != role;

            var rowGO = BuildPickerRow(crew, isAssignedHere, isAssignedElsewhere);
            _pickerRows.Add(rowGO);
        }

        // Unassign row (only if someone is currently in this slot)
        if (currentlyAssigned != null)
        {
            var unassignRow = BuildUnassignRow(currentlyAssigned);
            _pickerRows.Add(unassignRow);
        }
    }

    private GameObject BuildPickerRow(Character crew, bool isAssignedHere, bool isAssignedElsewhere)
    {
        var rowGO = new GameObject($"PickerRow_{crew.Id}", typeof(RectTransform));
        rowGO.transform.SetParent(pickerContent, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = isAssignedHere ? RowSelected : RowNormal;

        var le = rowGO.AddComponent<LayoutElement>();
        le.preferredHeight = 80f;
        le.flexibleWidth   = 1f;

        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(12, 12, 8, 8);
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
        portraitLE.preferredWidth  = 64f;
        portraitLE.preferredHeight = 64f;
        portraitLE.flexibleWidth   = 0f;

        // Text column
        var textCol = new GameObject("Text", typeof(RectTransform));
        textCol.transform.SetParent(rowGO.transform, false);
        textCol.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var vlg = textCol.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.MiddleLeft;
        vlg.spacing = 2f;

        var nameGO  = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(textCol.transform, false);
        var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text               = crew.Name;
        nameTMP.fontSize           = 26f;
        nameTMP.color              = isAssignedHere ? AccentCyan : TextWhite;
        nameTMP.fontStyle          = FontStyles.Bold;
        nameTMP.alignment          = TextAlignmentOptions.Left;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode       = TextOverflowModes.Ellipsis;
        nameGO.AddComponent<LayoutElement>().preferredHeight = 32f;

        string statusText = isAssignedHere     ? "● Assigned here" :
                            isAssignedElsewhere ? $"● {CrewViewController.FormatRole(crew.Role)}" :
                            $"Lv {crew.Level}";
        var statusGO  = new GameObject("Status", typeof(RectTransform));
        statusGO.transform.SetParent(textCol.transform, false);
        var statusTMP = statusGO.AddComponent<TextMeshProUGUI>();
        statusTMP.text      = statusText;
        statusTMP.fontSize  = 20f;
        statusTMP.color     = isAssignedHere ? AccentCyan : TextSubtle;
        statusTMP.alignment = TextAlignmentOptions.Left;
        statusGO.AddComponent<LayoutElement>().preferredHeight = 26f;

        // Assign button (disabled if already assigned here)
        if (!isAssignedHere)
        {
            var btn = rowGO.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            var cols = btn.colors;
            cols.normalColor      = Color.white;
            cols.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cols.pressedColor     = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = cols;
            var captured = crew;
            btn.onClick.AddListener(() => AssignCrew(captured, _pickerTargetRole));
        }

        return rowGO;
    }

    private GameObject BuildUnassignRow(Character currentlyAssigned)
    {
        var rowGO = new GameObject("UnassignRow", typeof(RectTransform));
        rowGO.transform.SetParent(pickerContent, false);

        var rowImg = rowGO.AddComponent<Image>();
        rowImg.color = new Color(0.20f, 0.08f, 0.08f, 0.80f);

        var le = rowGO.AddComponent<LayoutElement>();
        le.preferredHeight = 60f;
        le.flexibleWidth   = 1f;

        var btn = rowGO.AddComponent<Button>();
        btn.targetGraphic = rowImg;
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.35f, 0.12f, 0.12f, 1f);
        cols.pressedColor     = new Color(0.15f, 0.05f, 0.05f, 1f);
        btn.colors = cols;
        btn.onClick.AddListener(() => UnassignCrew(currentlyAssigned));

        var labelGO  = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rowGO.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;
        var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
        labelTMP.text      = $"Remove {currentlyAssigned.Name} from this post";
        labelTMP.fontSize  = 22f;
        labelTMP.color     = new Color(1f, 0.45f, 0.45f, 1f);
        labelTMP.alignment = TextAlignmentOptions.Center;

        return rowGO;
    }

    private void ClearPickerRows()
    {
        foreach (var r in _pickerRows)
            if (r != null) Object.Destroy(r);
        _pickerRows.Clear();
    }

    // ── Assignment logic ──────────────────────────────────────────────────────

    private void AssignCrew(Character crew, string role)
    {
        var gm = GameManager.Instance;
        if (gm?.Database == null || gm.CurrentSave == null) return;

        // Unassign anyone currently in this slot
        var current = gm.Database.GetCrewByRole(gm.CurrentSave.Id, role);
        if (current != null && current.Id != crew.Id)
        {
            current.Role = "";
            gm.Database.Characters.Update(current);
        }

        // If the incoming crew was elsewhere, clear their old slot too
        if (!string.IsNullOrEmpty(crew.Role) && crew.Role != role)
        {
            crew.Role = "";
            gm.Database.Characters.Update(crew);
        }

        crew.Role = role;
        gm.Database.Characters.Update(crew);

        Debug.Log($"[CrewAssignment] Assigned {crew.Name} → {role}");
        HidePicker();
        Populate();
    }

    private void UnassignCrew(Character crew)
    {
        var gm = GameManager.Instance;
        if (gm?.Database == null) return;

        crew.Role = "";
        gm.Database.Characters.Update(crew);

        Debug.Log($"[CrewAssignment] Unassigned {crew.Name}");
        HidePicker();
        Populate();
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
}
