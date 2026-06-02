using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the ShipView panel: refreshes equipment slot visuals and drives
/// the equipment detail popup when the player taps a slot.
///
/// ── Popup show/hide uses CanvasGroup (not SetActive) because the popup
///    contains Shift MainButtons whose Animators must stay continuously bound.
///    See CLAUDE.md "Shift MainButton on a re-enabled panel renders stuck-highlighted".
/// </summary>
public class ShipViewController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector references — wired by GameSceneSetup
    // -----------------------------------------------------------------------

    [Header("Detail Popup")]
    [SerializeField] private CanvasGroup detailOverlayGroup;
    [SerializeField] private Image       detailIconImage;
    [SerializeField] private TMP_Text    detailNameText;
    [SerializeField] private TMP_Text    detailTierText;
    [SerializeField] private TMP_Text    detailDescText;
    [SerializeField] private TMP_Text    detailCostText;
    [SerializeField] private Button      upgradeButton;
    [SerializeField] private Button      closeButton;

    [Header("Upgrade Confirmation")]
    [SerializeField] private CanvasGroup confirmOverlayGroup;
    [SerializeField] private TMP_Text    confirmText;
    [SerializeField] private Button      confirmYesButton;
    [SerializeField] private Button      confirmNoButton;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private EquipmentSlotUI[] _slots;
    private bool              _detailVisible;

    // Currently displayed slot context (set by ShowDetail, used by upgrade flow).
    private string           _currentSlotName;
    private EquipmentItem    _currentItem;
    private Sprite           _currentIcon;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        // Collect every slot in this panel once.
        _slots = GetComponentsInChildren<EquipmentSlotUI>(includeInactive: true);

        // Slot clicks are handled via IPointerClickHandler on EquipmentSlotUI —
        // no Button listener wiring needed here.

        // Wire popup buttons.
        closeButton?.onClick.AddListener(HideDetail);
        upgradeButton?.onClick.AddListener(OnUpgradeClicked);

        // Wire confirmation buttons.
        confirmYesButton?.onClick.AddListener(OnConfirmYes);
        confirmNoButton?.onClick.AddListener(HideConfirm);

        // Start with popup + confirmation hidden.
        SetDetailVisible(false);
        SetConfirmVisible(false);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Refresh all slot visuals from the current GameManager ship data.
    /// Called by SystemViewController each time the Ship tab is shown.
    /// </summary>
    public void Refresh()
    {
        if (_slots == null)
            _slots = GetComponentsInChildren<EquipmentSlotUI>(includeInactive: true);

        var gm   = GameManager.Instance;
        var ship = gm?.PlayerShip;

        // Build slot-name → EquipmentItem lookup.
        Dictionary<string, EquipmentItem> installedBySlot = null;
        if (ship != null && gm.Database != null)
        {
            installedBySlot = new Dictionary<string, EquipmentItem>();
            foreach (var kvp in ship.EquipmentSlots)
            {
                if (kvp.Value <= 0) continue;
                try
                {
                    var item = gm.Database.Equipment.Get(kvp.Value);
                    if (item != null)
                        installedBySlot[kvp.Key] = item;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[ShipViewController] Could not load equipment id {kvp.Value}: {e.Message}");
                }
            }
        }

        foreach (var slot in _slots)
        {
            EquipmentItem item = null;
            if (installedBySlot != null)
            {
                foreach (var kvp in Constants.Ship.EquipmentSlots.IconNames)
                {
                    if (slot.gameObject.name == "Slot_" + kvp.Key.Replace(" ", ""))
                    {
                        installedBySlot.TryGetValue(kvp.Key, out item);
                        break;
                    }
                }
            }
            slot.Refresh(item);
        }
    }

    // -----------------------------------------------------------------------
    // Slot click → show popup
    // -----------------------------------------------------------------------

    public void OnSlotClicked(EquipmentSlotUI slot)
    {
        // Resolve the slot's display name from its GameObject name ("Slot_FTLDrive" → "FTL Drive").
        string displayName = slot.gameObject.name;   // fallback
        foreach (var kvp in Constants.Ship.EquipmentSlots.IconNames)
        {
            if (slot.gameObject.name == "Slot_" + kvp.Key.Replace(" ", ""))
            {
                displayName = kvp.Key;
                break;
            }
        }

        // Resolve installed item (if any).
        EquipmentItem item = null;
        var gm   = GameManager.Instance;
        var ship = gm?.PlayerShip;
        if (ship != null && gm.Database != null && ship.EquipmentSlots.TryGetValue(displayName, out var itemId) && itemId > 0)
        {
            try { item = gm.Database.Equipment.Get(itemId); }
            catch { /* handled below */ }
        }

        ShowDetail(displayName, item, slot.defaultIcon);
    }

    private void ShowDetail(string slotDisplayName, EquipmentItem item, Sprite icon)
    {
        // Remember context for the upgrade flow.
        _currentSlotName = slotDisplayName;
        _currentItem     = item;
        _currentIcon     = icon;

        // Icon
        if (detailIconImage != null)
        {
            detailIconImage.sprite         = icon;
            detailIconImage.preserveAspect = true;
            detailIconImage.color          = Color.white;
        }

        // Name
        if (detailNameText != null)
            detailNameText.text = item != null ? item.Name : slotDisplayName;

        // Current tier ("Mk II", or "Not installed" for an empty slot).
        if (detailTierText != null)
            detailTierText.text = item != null
                ? Constants.Ship.TierLabel(item.Tier)
                : "Not installed";

        // Description
        if (detailDescText != null)
        {
            string desc = item != null && !string.IsNullOrEmpty(item.Description)
                ? item.Description
                : (Constants.Ship.EquipmentSlots.Descriptions.TryGetValue(slotDisplayName, out var d) ? d : "No description available.");
            detailDescText.text = desc;
        }

        // Upgrade cost + button state — disable Upgrade at max tier.
        bool canUpgrade = TryGetNextTier(item, out var next);
        if (detailCostText != null)
            detailCostText.text = canUpgrade
                ? $"{Constants.Ship.UpgradeCost(next)} Resources"
                : "MAX TIER";
        if (upgradeButton != null)
            upgradeButton.interactable = canUpgrade;

        SetDetailVisible(true);
        SetConfirmVisible(false);

        // Snap close and upgrade button animators to Normal@end so they don't
        // appear stuck-highlighted on each open (panel has been active since Start).
        SnapButtonAnimator(closeButton);
        SnapButtonAnimator(upgradeButton);
    }

    private void HideDetail()
    {
        SetConfirmVisible(false);
        SetDetailVisible(false);
    }

    // -----------------------------------------------------------------------
    // Upgrade flow
    // -----------------------------------------------------------------------

    private void OnUpgradeClicked()
    {
        // Already at the maximum tier — nothing to confirm.
        if (!TryGetNextTier(_currentItem, out var nextTier))
        {
            if (confirmText != null)
                confirmText.text =
                    $"{(_currentItem != null ? _currentItem.Name : _currentSlotName)} is already at maximum tier " +
                    $"({Constants.Ship.TierLabel(Constants.Ship.MaxTier)}).";
            // Both buttons simply dismiss — OnConfirmYes no-ops at max tier.
            ShowConfirm();
            return;
        }

        int cost = Constants.Ship.UpgradeCost(nextTier);
        string compName = _currentItem != null ? _currentItem.Name : _currentSlotName;

        if (confirmText != null)
        {
            confirmText.text = _currentItem != null
                ? $"Do you wish to upgrade {compName} from {Constants.Ship.TierLabel(_currentItem.Tier)} " +
                  $"to {Constants.Ship.TierLabel(nextTier)} for {cost} Resources?"
                : $"Do you wish to install {compName} at {Constants.Ship.TierLabel(nextTier)} for {cost} Resources?";
        }

        ShowConfirm();
    }

    private void OnConfirmYes()
    {
        var gm   = GameManager.Instance;
        var ship = gm?.PlayerShip;

        if (ship == null || gm.Database == null)
        {
            Debug.LogWarning("[ShipViewController] Cannot upgrade — no active ship/database.");
            HideConfirm();
            return;
        }

        if (!TryGetNextTier(_currentItem, out var nextTier))
        {
            // Max tier (or guard) — just dismiss.
            HideConfirm();
            return;
        }

        if (_currentItem == null)
        {
            // Empty slot — create and install a fresh component at the first tier.
            var newItem = new EquipmentItem
            {
                SaveGameId        = ship.SaveGameId,
                Name              = $"{_currentSlotName} {Constants.Ship.TierLabel(nextTier)}",
                Description       = Constants.Ship.EquipmentSlots.Descriptions.TryGetValue(_currentSlotName, out var d)
                                        ? d : "",
                EquipmentType     = Constants.Ship.SlotEquipmentType(_currentSlotName),
                Tier              = nextTier,
                IsInstalled       = true,
                InstalledOnShipId = ship.Id,
                InstalledInSlot   = _currentSlotName,
                Condition         = 100,
            };
            gm.Database.Equipment.Insert(newItem);          // assigns newItem.Id
            ship.InstallEquipment(_currentSlotName, newItem.Id);
            gm.Database.Ships.Update(ship);
            _currentItem = newItem;
        }
        else
        {
            // Bump the installed component up one tier and persist.
            _currentItem.Tier = nextTier;
            _currentItem.Name = ReTier(_currentItem.Name, _currentSlotName, nextTier);
            gm.Database.Equipment.Update(_currentItem);
        }

        Debug.Log($"[ShipViewController] Upgraded {_currentSlotName} to {Constants.Ship.TierLabel(nextTier)}.");

        HideConfirm();

        // Re-render popup (updates tier label + next cost) and recolour slot borders.
        ShowDetail(_currentSlotName, _currentItem, _currentIcon);
        Refresh();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Determines the tier an upgrade would produce. For an empty slot (item == null)
    /// the next tier is the first tier. Returns false when already at the maximum.
    /// </summary>
    private static bool TryGetNextTier(EquipmentItem item, out EquipmentTier nextTier)
    {
        if (item == null)
        {
            nextTier = EquipmentTier.MkI;
            return true;
        }
        if ((int)item.Tier >= (int)Constants.Ship.MaxTier)
        {
            nextTier = item.Tier;
            return false;
        }
        nextTier = (EquipmentTier)((int)item.Tier + 1);
        return true;
    }

    /// <summary>
    /// Updates an auto-generated component name's tier suffix. If the name doesn't
    /// look auto-generated, leaves the author-supplied name untouched.
    /// </summary>
    private static string ReTier(string currentName, string slotName, EquipmentTier newTier)
    {
        string newSuffix = Constants.Ship.TierLabel(newTier);
        // Auto-generated names look like "Reactor Mk II".
        if (!string.IsNullOrEmpty(currentName) && currentName.StartsWith(slotName + " Mk"))
            return $"{slotName} {newSuffix}";
        return currentName;
    }

    private void ShowConfirm()
    {
        SetConfirmVisible(true);
        SnapButtonAnimator(confirmYesButton);
        SnapButtonAnimator(confirmNoButton);
    }

    private void HideConfirm()
    {
        SetConfirmVisible(false);
    }

    private void SetDetailVisible(bool visible)
    {
        _detailVisible = visible;
        if (detailOverlayGroup == null) return;
        detailOverlayGroup.alpha          = visible ? 1f : 0f;
        detailOverlayGroup.blocksRaycasts = visible;
        detailOverlayGroup.interactable   = visible;
    }

    private void SetConfirmVisible(bool visible)
    {
        if (confirmOverlayGroup == null) return;
        confirmOverlayGroup.alpha          = visible ? 1f : 0f;
        confirmOverlayGroup.blocksRaycasts = visible;
        confirmOverlayGroup.interactable   = visible;
    }

    /// <summary>
    /// Snaps the button's Animator to the end of the Normal clip so it doesn't
    /// render stuck-highlighted when the panel is shown (see CLAUDE.md).
    /// Safe to call only after the panel has been active since Start().
    /// </summary>
    private static void SnapButtonAnimator(Button btn)
    {
        if (btn == null) return;
        var anim = btn.GetComponent<Animator>();
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            anim.Play("Normal", 0, 1.0f);
            anim.Update(0f);
        }
    }
}
