using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attached to each equipment slot root (Slot_*) in the ShipView.
///
/// The editor builder (GameSceneSetup) bakes in <see cref="defaultIcon"/> and
/// wires <see cref="iconImage"/> / <see cref="borderImage"/> via SerializedObject.
///
/// At runtime, SystemViewController calls <see cref="Refresh"/> to reflect the
/// currently installed item (or an empty slot).
/// </summary>
public class EquipmentSlotUI : MonoBehaviour
{
    [Tooltip("The icon Image inside the tier-border frame.")]
    [SerializeField] public Image iconImage;

    [Tooltip("The border Image that is tinted by equipment tier.")]
    [SerializeField] public Image borderImage;

    [Tooltip("Default icon for this slot type — baked by GameSceneSetup.")]
    [SerializeField] public Sprite defaultIcon;

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

        // Icon is always shown as-is — no tinting.
        if (iconImage.sprite == null)
            iconImage.sprite = defaultIcon;
        iconImage.color = Color.white;

        // Only the border changes to reflect tier.
        borderImage.color = item != null
            ? Constants.Ship.TierColor(item.Tier)
            : Constants.Ship.EmptySlotBorderColor;
    }
}
