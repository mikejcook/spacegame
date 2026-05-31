using UnityEngine;

/// <summary>
/// Nudges this RectTransform at runtime so it stays within the device safe area.
/// Attach to panels that may be obscured by notches, punch-holes, or home indicators.
///
/// Set which edges to honour in the Inspector or via GameSceneSetup. The script
/// captures the panel's designed position at Start() as a baseline and re-applies
/// cleanly if the safe area changes (e.g. on orientation change).
///
/// _expandInsteadOfShift mode: instead of translating the rect (moving both edges
/// equally), only the INNER edge moves, expanding the rect into the unsafe zone.
/// Use this for nav bars: the bar background grows downward to cover the home
/// indicator strip while the button container (top-anchored inside the bar) stays
/// clear of it. The opposite direction applies for notch-side toolbars.
///
/// Conversion: Screen.safeArea is in screen pixels. Dividing by canvas.scaleFactor
/// gives canvas reference units, which is what RectTransform offsets expect.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaInset : MonoBehaviour
{
    [SerializeField] private bool _left;
    [SerializeField] private bool _right;
    [SerializeField] private bool _top;
    [SerializeField] private bool _bottom;

    /// <summary>
    /// When true the rect EXPANDS into the unsafe zone rather than shifting away
    /// from it. Only the edge closest to the unsafe zone moves; the opposite edge
    /// stays put. Ideal for bottom nav bars: bar grows downward, content stays up.
    /// </summary>
    [SerializeField] private bool _expandInsteadOfShift;

    /// <summary>
    /// When true the rect SHRINKS away from the unsafe zone: only the edge facing
    /// the unsafe zone moves inward; the opposite edge holds. Use this on the Body
    /// panel so its bottom rises to stay flush with the expanded nav bar on iPhone.
    /// (_expandInsteadOfShift and _shrinkTowardSafeArea are mutually exclusive.)
    /// </summary>
    [SerializeField] private bool _shrinkTowardSafeArea;

    private RectTransform _rt;

    // Baseline offsets captured before any safe-area adjustment so re-applies
    // don't stack on top of each other (e.g. after an orientation change).
    private Vector2 _baseOffsetMin;
    private Vector2 _baseOffsetMax;

    // State of the LAST SUCCESSFUL apply. Crucially these are only written once
    // Apply() has actually adjusted the offsets — never before the canvas/scale
    // guards. If they were written up-front, a first-frame early-out (canvas
    // scaleFactor not yet computed → scale == 0) would record the current safe
    // area as "applied" without touching anything, and Update()'s change-detection
    // would then never retry. The panel would stay frozen at its baked design
    // offsets on device (where bottomPx > 0) for the rest of the session.
    private bool  _hasApplied      = false;
    private Rect  _appliedSafeArea  = Rect.zero;
    private float _appliedScale     = 0f;

    private void Start()
    {
        _rt            = GetComponent<RectTransform>();
        _baseOffsetMin = _rt.offsetMin;
        _baseOffsetMax = _rt.offsetMax;
        Apply();
    }

    private void Update()
    {
        // Re-apply until we get a successful pass, and thereafter whenever the
        // safe area or canvas scale factor changes (device rotation, the
        // CanvasScaler finishing its first layout pass, etc.).
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 0f;
        if (!_hasApplied || Screen.safeArea != _appliedSafeArea || scale != _appliedScale)
            Apply();
    }

    private void Apply()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // canvas.scaleFactor is the pixel-per-canvas-unit ratio Unity computed
        // from the CanvasScaler settings. Dividing by it converts screen pixels
        // to the canvas reference-unit space that RectTransform offsets live in.
        // On the very first frame the CanvasScaler may not have run yet, leaving
        // scaleFactor at 0 — bail WITHOUT recording state so Update() retries.
        float scale = canvas.scaleFactor;
        if (scale <= 0f) return;

        var sa = Screen.safeArea;
        float leftPx   = sa.xMin / scale;
        float rightPx  = (Screen.width  - sa.xMax) / scale;
        float bottomPx = sa.yMin / scale;
        float topPx    = (Screen.height - sa.yMax) / scale;

        // Reset to baseline so this method is idempotent.
        var oMin = _baseOffsetMin;
        var oMax = _baseOffsetMax;

        if (_expandInsteadOfShift)
        {
            // Expand into the unsafe zone. Only the edge at the unsafe boundary
            // moves outward; the far (content) edge stays put.
            // e.g. _bottom + expand: nav bar top (oMax.y) rises so the bar grows
            // taller while its button area at the top stays in place.
            if (_bottom) oMax.y += bottomPx;
            if (_top)    oMin.y -= topPx;
            if (_left)   oMin.x -= leftPx;
            if (_right)  oMax.x += rightPx;
        }
        else if (_shrinkTowardSafeArea)
        {
            // Shrink away from the unsafe zone. Only the edge facing the unsafe
            // boundary moves inward; the opposite edge holds.
            // e.g. _bottom + shrink: body bottom (oMin.y) rises to stay flush
            // with the expanded nav bar, without disturbing the body's top.
            if (_bottom) oMin.y += bottomPx;
            if (_top)    oMax.y -= topPx;
            if (_left)   oMin.x += leftPx;
            if (_right)  oMax.x -= rightPx;
        }
        else
        {
            // Shift both edges equally — moves the panel without resizing it.
            if (_left)   { oMin.x += leftPx;   oMax.x += leftPx;   }
            if (_right)  { oMin.x -= rightPx;  oMax.x -= rightPx;  }
            if (_bottom) { oMin.y += bottomPx; oMax.y += bottomPx; }
            if (_top)    { oMin.y -= topPx;    oMax.y -= topPx;    }
        }

        _rt.offsetMin = oMin;
        _rt.offsetMax = oMax;

        // Record state ONLY now that the adjustment has actually been applied,
        // so a prior early-out can't suppress the retry in Update().
        _appliedSafeArea = Screen.safeArea;
        _appliedScale    = scale;
        _hasApplied      = true;
    }
}
