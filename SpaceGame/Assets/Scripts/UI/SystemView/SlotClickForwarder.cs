using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Placed on the ClickBlocker child of each equipment slot.
/// Wired to the slot's EquipmentSlotUI by GameSceneSetup so there is zero
/// runtime parent-traversal — the click fires directly with no ambiguity.
/// </summary>
public class SlotClickForwarder : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] public EquipmentSlotUI target;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (target == null) return;
        var svc = target.GetComponentInParent<ShipViewController>();
        svc?.OnSlotClicked(target);
    }
}
