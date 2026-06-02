using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Adds pinch-to-zoom, scroll-wheel zoom, and single-finger / mouse drag pan
/// to the System Map.  Attach to the SystemMap RectTransform.
///
/// Requires the New Input System package (activeInputHandler = 1 or 2).
/// Uses EnhancedTouch for reliable multi-touch tracking.
/// </summary>
public class SystemMapZoomController : MonoBehaviour
{
    // ── Tuning ────────────────────────────────────────────────────────────────

    [Tooltip("Minimum zoom level (1 = full system visible).")]
    [SerializeField] private float minZoom = 0.7f;

    [Tooltip("Maximum zoom level.")]
    [SerializeField] private float maxZoom = 4.0f;

    [Tooltip("How much each scroll-wheel tick zooms. Smaller = gentler.")]
    [SerializeField] private float scrollSensitivity = 0.12f;

    [Tooltip("Multiplier on the horizontal pan clamp. 1 = edge of viewport; 2 = one full viewport width beyond centre.")]
    [SerializeField] private float panBoundsX = 1.0f;

    [Tooltip("Multiplier on the vertical pan clamp. Increase this to allow more up/down panning for large systems.")]
    [SerializeField] private float panBoundsY = 2.0f;

    // ── Private state ─────────────────────────────────────────────────────────

    private RectTransform _rt;
    private Canvas        _canvas;

    private float   _zoom   = 1f;
    private Vector2 _offset = Vector2.zero;

    // Drag
    private bool    _dragging        = false;
    private Vector2 _dragStartScreen;
    private Vector2 _dragStartOffset;

    // Pinch — tracked from gesture start so zoom doesn't drift
    private bool    _pinching          = false;
    private float   _pinchStartDist    = 0f;
    private float   _pinchStartZoom    = 1f;
    private Vector2 _pinchStartMid;
    private Vector2 _pinchStartOffset;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();
    }

    private void Awake()
    {
        _rt     = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
    }

    private void Update()
    {
        var activeTouches = Touch.activeTouches;

        if (activeTouches.Count >= 2)
            HandlePinch(activeTouches[0], activeTouches[1]);
        else if (activeTouches.Count == 1)
            HandleTouchDrag(activeTouches[0]);
        else
            HandleMouseInput();
    }

    // ── Pinch zoom ────────────────────────────────────────────────────────────

    private void HandlePinch(Touch t0, Touch t1)
    {
        _dragging = false;

        bool freshGesture = t0.phase == UnityEngine.InputSystem.TouchPhase.Began
                         || t1.phase == UnityEngine.InputSystem.TouchPhase.Began
                         || !_pinching;

        if (freshGesture)
        {
            _pinchStartDist   = Vector2.Distance(t0.screenPosition, t1.screenPosition);
            _pinchStartZoom   = _zoom;
            _pinchStartMid    = (t0.screenPosition + t1.screenPosition) * 0.5f;
            _pinchStartOffset = _offset;
            _pinching         = true;
            return;
        }

        float currDist = Vector2.Distance(t0.screenPosition, t1.screenPosition);
        if (_pinchStartDist < 0.01f) return;

        float newZoom   = Mathf.Clamp(_pinchStartZoom * (currDist / _pinchStartDist),
                                       minZoom, maxZoom);
        Vector2 currMid = (t0.screenPosition + t1.screenPosition) * 0.5f;

        // Zoom toward pinch midpoint + track midpoint translation
        Vector2 midDelta = (currMid - _pinchStartMid) / _canvas.scaleFactor;
        _zoom   = newZoom;
        _offset = _pinchStartOffset
                  + midDelta
                  + ZoomOffsetCorrection(_pinchStartMid, _pinchStartOffset,
                                         _pinchStartZoom, newZoom);
        Commit();
    }

    // ── Touch drag pan ────────────────────────────────────────────────────────

    private void HandleTouchDrag(Touch t)
    {
        _pinching = false;

        if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            _dragging        = true;
            _dragStartScreen = t.screenPosition;
            _dragStartOffset = _offset;
        }
        else if (_dragging && t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
        {
            ApplyDrag(t.screenPosition);
        }
        else if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended
              || t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
        {
            _dragging = false;
        }
    }

    // ── Mouse input (editor / desktop) ───────────────────────────────────────

    private void HandleMouseInput()
    {
        _pinching = false;

        var mouse = Mouse.current;
        if (mouse == null) return;

        // Scroll wheel zoom — only when not dragging; on trackpads a left-button drag
        // also generates scroll events, and letting both run simultaneously causes
        // "panning causes zoom" — the zoom overpowers the pan delta every frame.
        float scroll = mouse.scroll.ReadValue().y;
        if (!_dragging && Mathf.Abs(scroll) > 0.01f)
        {
            float newZoom = Mathf.Clamp(_zoom * (1f + Mathf.Sign(scroll) * scrollSensitivity),
                                        minZoom, maxZoom);
            _offset += ZoomOffsetCorrection(mouse.position.ReadValue(), _offset, _zoom, newZoom);
            _zoom    = newZoom;
            Commit();
        }

        // Mouse drag pan
        if (mouse.leftButton.wasPressedThisFrame)
        {
            _dragging        = true;
            _dragStartScreen = mouse.position.ReadValue();
            _dragStartOffset = _offset;
        }
        if (_dragging && mouse.leftButton.isPressed)
        {
            ApplyDrag(mouse.position.ReadValue());
        }
        if (mouse.leftButton.wasReleasedThisFrame)
        {
            _dragging = false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the anchoredPosition correction so that <paramref name="screenPivot"/>
    /// stays fixed in canvas space when zoom changes from <paramref name="oldZoom"/>
    /// to <paramref name="newZoom"/>.
    /// </summary>
    private Vector2 ZoomOffsetCorrection(Vector2 screenPivot, Vector2 currentOffset,
                                          float oldZoom, float newZoom)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rt.parent as RectTransform,
            screenPivot,
            null,   // Screen Space Overlay — no camera needed
            out Vector2 pivotInParent);

        float   scaleDelta = newZoom / oldZoom;
        Vector2 relOffset  = pivotInParent - currentOffset;
        return relOffset * (1f - scaleDelta);
    }

    private void ApplyDrag(Vector2 currentScreen)
    {
        Vector2 delta = (currentScreen - _dragStartScreen) / _canvas.scaleFactor;
        _offset = _dragStartOffset + delta;
        Commit();
    }

    /// <summary>
    /// Centres the map on a canvas-pixel local position at the given zoom level.
    /// <paramref name="localPos"/> is an <c>anchoredPosition</c> offset from the
    /// map centre — the same coordinate system POI/ship nodes use.
    /// Safe to call immediately (no layout pass required).
    /// </summary>
    public void FocusOn(Vector2 localPos, float zoom)
    {
        _zoom   = Mathf.Clamp(zoom, minZoom, maxZoom);
        _offset = -localPos * _zoom;
        Commit();
    }

    /// <summary>
    /// Programmatically sets zoom and centres the view on a galaxy-coordinate
    /// focal point (normalised 0-1 fractions within the container).
    /// Call after at least one frame so the RectTransform rect is populated.
    /// </summary>
    public void SetInitialView(float zoom, float focusGX, float focusGY)
    {
        Canvas.ForceUpdateCanvases();
        _zoom   = Mathf.Clamp(zoom, minZoom, maxZoom);
        float w = _rt.rect.width;
        float h = _rt.rect.height;
        _offset = new Vector2(
            -(focusGX - 0.5f) * w * _zoom,
            -(focusGY - 0.5f) * h * _zoom);
        Commit();
    }

    private void Commit()
    {
        // Clamp pan so the player cannot drag past the map's own edges.
        // At zoom z the content is (w*z) × (h*z); the farthest the centre can
        // travel before the edge leaves the viewport is half that size.
        if (_rt.parent is RectTransform parentRT)
        {
            float hw = parentRT.rect.width  * _zoom * 0.5f * panBoundsX;
            float hh = parentRT.rect.height * _zoom * 0.5f * panBoundsY;
            _offset.x = Mathf.Clamp(_offset.x, -hw, hw);
            _offset.y = Mathf.Clamp(_offset.y, -hh, hh);
        }

        _rt.anchoredPosition = _offset;
        _rt.localScale       = new Vector3(_zoom, _zoom, 1f);
    }
}
