using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sci-fi targeting crosshair for the combat view.
///
/// Sits as a child of CombatView alongside the enemy slots.  Call
/// SnapToSlot(slotRT) to move it onto any enemy — it matches the slot's
/// canvas position and sizes itself to frame the ship with a small padding.
/// An entrance animation (quick scale-in) plays each time it re-targets.
///
/// Call Pulse() when the player fires to get an expand-and-snap-back flash.
/// Call Hide() when combat ends.
///
/// Wired by GameSceneSetup.  Shown/hidden alongside CombatView via its
/// parent CanvasGroup — no SetActive calls needed.
/// </summary>
public class CombatCrosshair : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector references — wired by GameSceneSetup
    // -----------------------------------------------------------------------

    [Header("Crosshair Images")]
    [SerializeField] private RawImage outerImage;   // xArrowheadInwards128Glow — inward arrowheads

    [Header("Appearance")]
    [SerializeField] private Color crosshairColor = new Color(0f, 0.71f, 1f, 1f);  // Shift UI blue #00B5FF

    // Sizing constants — not serialized so code changes take effect immediately
    // without requiring a scene rebuild.
    private const float SlotPadding = 80f;  // extra canvas units added on each side of the diagonal
    private const float MinSize     = 160f; // floor so tiny fighters always get a visible frame
    private const float MaxSize     = 600f; // ceiling so dreadnaughts don't overflow the viewport

    [Header("Snap Animation")]
    [SerializeField] private float snapInTime     = 0.12f;  // seconds for entrance scale-in

    [Header("Pulse Animation")]
    [SerializeField] private float pulseScale      = 1.25f;
    [SerializeField] private float pulseOutTime    = 0.08f;
    [SerializeField] private float pulseReturnTime = 0.18f;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------

    private RectTransform _rt;
    private CanvasGroup   _cg;
    private Vector3       _normalScale;
    private bool          _isPulsing;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();

        // CanvasGroup for hide/show without disabling the GameObject
        _cg       = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;   // hidden until first target is set

        if (outerImage != null)
            outerImage.color = crosshairColor;

        _normalScale = transform.localScale;
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Snap the crosshair onto <paramref name="slotRT"/>, sizing it to frame
    /// the ship with a small padding.  Plays a quick scale-in entrance.
    /// </summary>
    public void SnapToSlot(RectTransform slotRT)
    {
        if (slotRT == null || _rt == null) return;

        // Derive the slot's world-space centre from its rendered corners.
        // This is robust against any anchor/pivot ambiguity — we never copy
        // anchor values between RectTransforms because Unity adjusts
        // anchoredPosition differently depending on the current sizeDelta.
        var corners = new Vector3[4];
        slotRT.GetWorldCorners(corners);
        // corners: [0]=bottom-left [1]=top-left [2]=top-right [3]=bottom-right
        var worldCenter = (corners[0] + corners[2]) * 0.5f;

        // Convert to the crosshair parent's local space and pin with a fixed
        // centre anchor so the position is expressed as a simple offset.
        var parentRT = _rt.parent as RectTransform;
        if (parentRT == null) return;
        _rt.anchorMin        = new Vector2(0.5f, 0.5f);
        _rt.anchorMax        = new Vector2(0.5f, 0.5f);
        _rt.anchoredPosition = parentRT.InverseTransformPoint(worldCenter);

        // Size: use the sprite diagonal so the crosshair frames the ship
        // correctly regardless of its baked rotation (235°, 180°, 125°).
        Vector2 s = slotRT.sizeDelta;
        float raw = Mathf.Sqrt(s.x * s.x + s.y * s.y) + SlotPadding * 2f;
        float size = Mathf.Clamp(raw, MinSize, MaxSize);
        _rt.sizeDelta = new Vector2(size, size);

        StopAllCoroutines();
        _isPulsing = false;
        StartCoroutine(SnapInRoutine());
    }

    /// <summary>
    /// Quick expand-and-contract flash.  Call when the player fires.
    /// Safe to call while a snap animation is in progress — it will be ignored.
    /// </summary>
    public void Pulse()
    {
        if (!_isPulsing)
            StartCoroutine(PulseRoutine());
    }

    /// <summary>Fade out the crosshair (e.g. when combat ends).</summary>
    public void Hide()
    {
        StopAllCoroutines();
        _isPulsing = false;
        if (_cg != null) _cg.alpha = 0f;
        transform.localScale = _normalScale;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private IEnumerator SnapInRoutine()
    {
        // Fade in while scaling from 1.4× → 1×
        _cg.alpha = 1f;
        var bigScale = _normalScale * 1.4f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / snapInTime;
            transform.localScale = Vector3.Lerp(bigScale, _normalScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.localScale = _normalScale;
    }

    private IEnumerator PulseRoutine()
    {
        _isPulsing = true;
        var rt         = outerImage != null ? outerImage.rectTransform : null;
        var baseScale  = rt != null ? rt.localScale : Vector3.one;
        var targetScale = baseScale * pulseScale;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / pulseOutTime;
            if (rt != null) rt.localScale = Vector3.Lerp(baseScale, targetScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / pulseReturnTime;
            if (rt != null) rt.localScale = Vector3.Lerp(targetScale, baseScale, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (rt != null) rt.localScale = baseScale;
        _isPulsing = false;
    }
}
