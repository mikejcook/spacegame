using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A UI-space projectile that flies from a start world position to a target
/// world position over a fixed duration, then destroys itself.
///
/// Usage (called by CombatViewController.LaunchProjectile):
///
///   var go  = new GameObject("Projectile", typeof(RectTransform));
///   go.transform.SetParent(projectileContainer, worldPositionStays: false);
///   var proj = go.AddComponent&lt;CombatProjectile&gt;();
///   proj.Launch(startWorld, endWorld, sprite, displaySize, duration);
///
/// The projectile rotates to face its direction of travel. Sprites are
/// assumed to face upward (+Y) in their texture — same convention as DGB
/// Spaceship Sprites and the missile_1/missile_2 assets.
/// </summary>
public class CombatProjectile : MonoBehaviour
{
    // ── Set by Launch() — no Inspector wiring needed ─────────────────────

    private RectTransform _rt;
    private Image         _image;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        _rt    = GetComponent<RectTransform>();
        _image = gameObject.AddComponent<Image>();
        _image.raycastTarget = false;    // projectiles don't absorb taps
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Begin the flight.  Call once, immediately after AddComponent.
    /// </summary>
    /// <param name="startWorld">World-space (= screen-space on ScreenSpaceOverlay) origin.</param>
    /// <param name="endWorld">World-space destination — centre of the targeted enemy slot.</param>
    /// <param name="projectileSprite">The sprite to display (plasma bolt or missile).</param>
    /// <param name="displaySize">RectTransform sizeDelta in canvas units.</param>
    /// <param name="duration">Flight time in seconds.</param>
    public void Launch(Vector3 startWorld, Vector3 endWorld,
                       Sprite projectileSprite, Vector2 displaySize, float duration)
    {
        // Configure Image
        _image.sprite         = projectileSprite;
        _image.preserveAspect = true;
        _image.type           = Image.Type.Simple;
        _image.color          = Color.white;

        // Size and position
        _rt.sizeDelta = displaySize;
        _rt.position  = startWorld;

        // Rotate so the sprite's nose (up, +Y) points toward the target.
        // Atan2 gives the angle from +X; subtract 90° because +Y is our "forward".
        Vector3 dir = endWorld - startWorld;
        if (dir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            _rt.localEulerAngles = new Vector3(0f, 0f, angle);
        }

        StartCoroutine(FlyRoutine(startWorld, endWorld, duration));
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private IEnumerator FlyRoutine(Vector3 start, Vector3 end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _rt.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        // Ensure we land exactly on the target before disappearing
        _rt.position = end;
        Destroy(gameObject);
    }
}
