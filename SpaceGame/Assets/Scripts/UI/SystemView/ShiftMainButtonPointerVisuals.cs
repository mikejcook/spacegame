using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drives hover / press visuals for a Shift MainButton by writing its
/// <c>Normal</c> / <c>Highlighted</c> / <c>Pressed</c> child CanvasGroup
/// alphas directly from <c>IPointer*</c> events, bypassing the Shift
/// Animator entirely.
///
/// Why: the Shift MainButton's Animator has a same-frame playable-binding
/// race on re-enabled panels that leaves the button stuck in the Normal
/// clip's t=0 frame — which visually looks highlighted. See the Shift
/// gotcha in CLAUDE.md and <see cref="SystemViewController.Start"/> for the
/// full story. Disabling the Animator removes the bug; this component
/// restores the visual flourish that the Animator used to provide.
///
/// Setup: attach to a Shift MainButton root that has already had its
/// Animator disabled and its <c>Button.transition</c> set to
/// <c>Selectable.Transition.None</c>. The component finds its
/// Normal / Highlighted / Pressed children by name.
///
/// Trade-off vs. the original Animator: visual state changes are
/// instantaneous instead of cross-faded over 0.167 s. Good enough for a
/// dialog close button; if smooth fades are wanted, lerp the alphas in
/// <see cref="ApplyVisual"/> over a couple of frames.
/// </summary>
[DisallowMultipleComponent]
public class ShiftMainButtonPointerVisuals : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler,  IPointerUpHandler
{
    private CanvasGroup _normalCG;
    private CanvasGroup _highlightedCG;
    private CanvasGroup _pressedCG;

    private bool _pointerInside;
    private bool _pointerDown;

    private void Awake()
    {
        _normalCG      = FindChildCanvasGroup("Normal");
        _highlightedCG = FindChildCanvasGroup("Highlighted");
        _pressedCG     = FindChildCanvasGroup("Pressed");
        ApplyVisual();
    }

    private void OnDisable()
    {
        // Reset to Normal-at-rest when the button is hidden, so the next
        // re-enable doesn't reveal a stale hover / press visual.
        _pointerInside = false;
        _pointerDown   = false;
        ApplyVisual();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        ApplyVisual();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        ApplyVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerDown = true;
        ApplyVisual();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pointerDown = false;
        ApplyVisual();
    }

    /// <summary>
    /// Picks the visual state from the current pointer flags and writes the
    /// three child CanvasGroup alphas. Priority: Pressed &gt; Highlighted &gt;
    /// Normal. Pressed only counts while the pointer is still inside the
    /// button, matching Selectable's behaviour — if the user drags off while
    /// holding, the visual reverts to Normal until they drag back in.
    /// </summary>
    private void ApplyVisual()
    {
        bool pressed     = _pointerDown && _pointerInside;
        bool highlighted = _pointerInside && !pressed;
        bool normal      = !pressed && !highlighted;

        if (_normalCG      != null) _normalCG.alpha      = normal      ? 1f : 0f;
        if (_highlightedCG != null) _highlightedCG.alpha = highlighted ? 1f : 0f;
        if (_pressedCG     != null) _pressedCG.alpha     = pressed     ? 1f : 0f;
    }

    private CanvasGroup FindChildCanvasGroup(string childName)
    {
        var child = transform.Find(childName);
        return child != null ? child.GetComponent<CanvasGroup>() : null;
    }
}
