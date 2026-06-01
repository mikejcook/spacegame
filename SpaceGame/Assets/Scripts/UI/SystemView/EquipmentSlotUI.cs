using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to each equipment slot root (Slot_*) in the ShipView.
///
/// Implements IPointerClickHandler directly so that any click anywhere on the
/// slot rect (including the dark letterbox areas around a preserveAspect icon)
/// opens the detail popup.  This is more reliable than wiring Button.onClick
/// listeners, which depend on Button interactability and listener wiring order.
///
/// The editor builder (GameSceneSetup) bakes in <see cref="defaultIcon"/> and
/// wires <see cref="iconImage"/> / <see cref="borderImage"/> via SerializedObject.
/// </summary>
public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("The icon Image inside the tier-border frame.")]
    [SerializeField] public Image iconImage;

    [Tooltip("The border Image that is tinted by equipment tier.")]
    [SerializeField] public Image borderImage;

    [Tooltip("Default icon for this slot type — baked by GameSceneSetup.")]
    [SerializeField] public Sprite defaultIcon;

    // -----------------------------------------------------------------------
    // IPointerClickHandler
    // -----------------------------------------------------------------------

    public void OnPointerClick(PointerEventData eventData)
    {
        // Walk up to the ShipViewController; it owns the popup logic.
        var svc = GetComponentInParent<ShipViewController>();
        svc?.OnSlotClicked(this);
    }

    // -----------------------------------------------------------------------
    // Runtime API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Update the slot's visual to reflect <paramref name="item"/>.
    /// Pass <c>null</c> to show the slot as empty.
    /// </summary>
    public void Refresh(EquipmentItem item)
    {
        if (iconImage   == null) return;
        if (borderImage == null) return;

        if (iconImage.sprite == null)
            iconImage.sprite = defaultIcon;
        iconImage.color = Color.white;

        borderImage.color = item != null
            ? Constants.Ship.TierColor(item.Tier)
            : Constants.Ship.EmptySlotBorderColor;
    }
}
