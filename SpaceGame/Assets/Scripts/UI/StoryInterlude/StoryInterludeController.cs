using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays a story interlude — a sequence of full-screen images with caption
/// text, advanced by tapping anywhere on screen.
///
/// ── Scene Setup ──────────────────────────────────────────────────────────────
///
/// Create a hierarchy like this inside your scene's Canvas:
///
///   StoryInterludeOverlay          ← GameObject with this script
///   └─ OverlayRoot                 ← Panel, full-screen, Black background
///      ├─ SceneImage               ← RawImage, anchored full-screen
///      ├─ PlaceholderPanel         ← Image (solid dark colour), full-screen
///      │                              (visible when no imageResource is set)
///      ├─ CaptionBar               ← Image (semi-transparent), anchored bottom
///      │  └─ CaptionText           ← TMP_Text inside CaptionBar
///      ├─ TapHint                  ← GameObject with TMP_Text "Tap to continue"
///      │                              anchored bottom-right
///      └─ TapButton                ← Button, full-screen, Alpha 0, Raycast On
///
/// Set the Canvas Sort Order high enough to render above all other UI.
/// Wire all [SerializeField] references in the Inspector.
///
/// ── Usage ────────────────────────────────────────────────────────────────────
///
///   // Prepare game data first so token substitution has live values to work with.
///   GameManager.Instance.PrepareNewGame(captainName, shipName, portraitFileName);
///
///   var stories   = StoryCollection.LoadFromResources();
///   var interlude = stories?.GetInterlude(Constants.Interludes.NewGameIntroId);
///   storyInterludeController.Play(interlude, () => GameManager.Instance.LaunchNewGame());
///
/// ── JSON Format ──────────────────────────────────────────────────────────────
///
///   Scene text supports {Namespace.Target.Field} substitution via StoryTextResolver.
///   Example: "Welcome aboard, {Character.Captain.Name}. Your {Ship.ShipClass} awaits."
///
///   {
///     "interludeId": "new_game_intro",
///     "startSceneId": 1,
///     "scenes": [
///       { "id": 1, "imageResource": "", "text": "Year 2387. The stars await.", "nextSceneId": 2 },
///       { "id": 2, "imageResource": "", "text": "Your crew is ready, Captain.", "nextSceneId": 0 }
///     ]
///   }
///
/// nextSceneId == 0 ends the interlude and fires the onComplete callback.
/// </summary>
public class StoryInterludeController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector references
    // -----------------------------------------------------------------------
    [Header("Overlay Structure")]
    [Tooltip("Root panel that is shown/hidden for the entire interlude.")]
    [SerializeField] private GameObject overlayRoot;

    [Header("Image Area")]
    [Tooltip("RawImage that fills the screen when a scene has an imageResource assigned.")]
    [SerializeField] private RawImage sceneImage;

    [Tooltip("Solid-colour panel shown when a scene has no image (placeholder mode).")]
    [SerializeField] private Image placeholderPanel;

    [Header("Caption")]
    [Tooltip("TMP_Text at the bottom of the screen showing the scene's text.")]
    [SerializeField] private TMP_Text captionText;

    [Header("Tap Hint")]
    [Tooltip("Optional 'Tap to continue' hint GameObject. Shown after each new scene appears.")]
    [SerializeField] private GameObject tapHint;

    [Header("Input")]
    [Tooltip("Full-screen transparent Button that captures taps to advance the scene.")]
    [SerializeField] private Button tapButton;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------
    private StoryInterlude _interlude;
    private Action         _onComplete;
    private int            _currentSceneId;
    private bool           _isRunning;
    private bool           _inputLocked;   // brief lock after scene change to prevent double-taps

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private void Awake()
    {
        HideOverlay();
        tapButton?.onClick.AddListener(OnTapped);
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------


    /// <summary>
    /// Begin the interlude sequence.
    /// If <paramref name="interlude"/> is null, <paramref name="onComplete"/> is called immediately.
    /// </summary>
    public void Play(StoryInterlude interlude, Action onComplete)
    {
        if (interlude == null || interlude.scenes == null || interlude.scenes.Count == 0)
        {
            Debug.LogWarning("[StoryInterludeController] No interlude data — skipping.");
            onComplete?.Invoke();
            return;
        }

        _interlude  = interlude;
        _onComplete = onComplete;
        _isRunning  = true;

        ShowOverlay();
        ShowScene(_interlude.startSceneId);
    }

    // -----------------------------------------------------------------------
    // Scene display
    // -----------------------------------------------------------------------
    private void ShowScene(int sceneId)
    {
        var scene = _interlude?.GetScene(sceneId);
        if (scene == null)
        {
            Debug.LogWarning($"[StoryInterludeController] Scene {sceneId} not found — ending interlude.");
            EndInterlude();
            return;
        }

        _currentSceneId = sceneId;

        // ── Image ────────────────────────────────────────────────────────
        bool hasImage = !string.IsNullOrEmpty(scene.imageResource);
        if (hasImage)
        {
            var tex = Resources.Load<Texture2D>(scene.imageResource);
            if (tex != null)
            {
                ApplyImage(tex);
            }
            else
            {
                Debug.LogWarning($"[StoryInterludeController] Texture not found at Resources/{scene.imageResource} — using placeholder.");
                ApplyPlaceholder();
            }
        }
        else
        {
            ApplyPlaceholder();
        }

        // ── Caption ──────────────────────────────────────────────────────
        if (captionText) captionText.text = StoryTextResolver.Resolve(scene.text);

        // ── Tap hint ─────────────────────────────────────────────────────
        if (tapHint) tapHint.SetActive(true);

        // Brief input lock so a tap that triggers ShowScene can't immediately
        // advance again on the same frame.
        StartCoroutine(UnlockInputNextFrame());
    }

    private void ApplyImage(Texture2D tex)
    {
        if (sceneImage)
        {
            sceneImage.texture = tex;
            sceneImage.gameObject.SetActive(true);
        }
        if (placeholderPanel) placeholderPanel.gameObject.SetActive(false);
    }

    private void ApplyPlaceholder()
    {
        if (sceneImage)       sceneImage.gameObject.SetActive(false);
        if (placeholderPanel) placeholderPanel.gameObject.SetActive(true);
    }

    // -----------------------------------------------------------------------
    // Tap handler
    // -----------------------------------------------------------------------
    private void OnTapped()
    {
        if (!_isRunning || _inputLocked) return;

        var scene = _interlude?.GetScene(_currentSceneId);
        if (scene == null) { EndInterlude(); return; }

        if (scene.nextSceneId == 0)
        {
            EndInterlude();
        }
        else
        {
            ShowScene(scene.nextSceneId);
        }
    }

    // -----------------------------------------------------------------------
    // Interlude lifecycle helpers
    // -----------------------------------------------------------------------
    private void ShowOverlay()
    {
        if (overlayRoot) overlayRoot.SetActive(true);
    }

    private void HideOverlay()
    {
        if (overlayRoot) overlayRoot.SetActive(false);
    }

    private void EndInterlude()
    {
        _isRunning = false;
        HideOverlay();

        var callback = _onComplete;
        _onComplete  = null;
        callback?.Invoke();
    }

    // -----------------------------------------------------------------------
    // Input lock coroutine
    // -----------------------------------------------------------------------
    private IEnumerator UnlockInputNextFrame()
    {
        _inputLocked = true;
        yield return null;   // wait one frame
        _inputLocked = false;
    }
}
