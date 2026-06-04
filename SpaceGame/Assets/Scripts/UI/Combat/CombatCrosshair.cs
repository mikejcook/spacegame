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
    [SerializeField] private float slotPadding    = 30f;   // extra canvas units added on each side
    [SerializeField] private float minSize        = 160f;  // smallest crosshair (covers tiny fighters)
    [SerializeField] private float maxSize        = 480f;  // largest crosshair (dreadnaught needs ~386)

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

        // Match the slot's point-anchor position inside the shared parent (CombatView).
        _rt.anchorMin        = slotRT.anchorMin;
        _rt.anchorMax        = slotRT.anchorMax;
        _rt.anchoredPosition = slotRT.anchoredPosition;

        // Frame the ship with padding.  slotRT.sizeDelta is updated at runtime
        // by CombatViewController.ShowEnemySlot once the sprite is known.
        // Use the larger dimension to keep the crosshair square, then clamp
        // so tiny fighters get a minimum visible size and huge dreadnaughts
        // don't overflow the viewport.
        float raw  = Mathf.Max(slotRT.sizeDelta.x, slotRT.sizeDelta.y) + slotPadding * 2f;
        float size = Mathf.Clamp(raw, minSize, maxSize);
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
