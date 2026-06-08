using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Large multi-ring explosion played when an enemy ship is destroyed.
/// Much bigger and longer than <see cref="CombatHitEffect"/> — this is the
/// "ship gone" event, not a hit flash.
///
/// Five overlapping rings fire with staggered delays and sizes to produce a
/// realistic expansion burst.  The host GameObject self-destructs when done.
///
/// Usage:
///   CombatShipExplosion.SpawnAt(projectileContainer, worldPos, glowSprite);
/// </summary>
public class CombatShipExplosion : MonoBehaviour
{
    // ── Total lifetime ─────────────────────────────────────────────────────────
    /// <summary>
    /// How long the caller should wait for the explosion to finish.
    /// Yield on this before resolving combat-end checks.
    /// </summary>
    public const float Duration = 1.8f;

    // ── Colors ────────────────────────────────────────────────────────────────
    private static readonly Color FlashWhite = new Color(1.00f, 1.00f, 1.00f, 1f);
    private static readonly Color CoreYellow = new Color(1.00f, 0.92f, 0.20f, 1f);
    private static readonly Color MidOrange  = new Color(1.00f, 0.50f, 0.05f, 1f);
    private static readonly Color OuterRed   = new Color(0.90f, 0.18f, 0.06f, 1f);
    private static readonly Color FarOrange  = new Color(1.00f, 0.40f, 0.00f, 1f);

    // ── Static factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Spawn an explosion inside <paramref name="container"/> centred at
    /// <paramref name="worldPos"/>.  No-ops gracefully if container is null.
    /// </summary>
    public static void SpawnAt(RectTransform container, Vector3 worldPos, Sprite glowSprite)
    {
        if (container == null) return;
        var go = new GameObject("ShipExplosion", typeof(RectTransform));
        go.transform.SetParent(container, worldPositionStays: false);
        go.AddComponent<CombatShipExplosion>().Fire(worldPos, glowSprite);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Fire(Vector3 worldPos, Sprite glowSprite)
    {
        StartCoroutine(ExplosionRoutine(worldPos, glowSprite));
    }

    private IEnumerator ExplosionRoutine(Vector3 worldPos, Sprite glowSprite)
    {
        // Ring 1: white core flash — immediate, small, very fast
        StartCoroutine(RunRing(worldPos, glowSprite, 130f,  FlashWhite, 0.22f, 1.8f));
        // Ring 2: yellow core — tiny delay, medium size
        StartCoroutine(DelayedRing(0.04f, worldPos, glowSprite, 280f, CoreYellow, 0.55f, 2.2f));
        // Ring 3: orange mid — slightly later, large
        StartCoroutine(DelayedRing(0.10f, worldPos, glowSprite, 420f, MidOrange,  0.90f, 2.8f));
        // Ring 4: red outer — slow expand, longest tail
        StartCoroutine(DelayedRing(0.20f, worldPos, glowSprite, 560f, OuterRed,   1.40f, 3.0f));
        // Ring 5: second orange pulse — rides in over the red ring
        StartCoroutine(DelayedRing(0.45f, worldPos, glowSprite, 380f, FarOrange,  0.80f, 2.4f));

        yield return new WaitForSeconds(Duration);
        Destroy(gameObject);
    }

    private IEnumerator DelayedRing(float delay, Vector3 worldPos, Sprite sprite,
                                     float startSize, Color color,
                                     float duration, float endScale)
    {
        yield return new WaitForSeconds(delay);
        yield return RunRing(worldPos, sprite, startSize, color, duration, endScale);
    }

    /// <summary>
    /// Animate a single ring: expands from 1× to endScale while alpha fades 1→0.
    /// Power-curve easing (t^0.6) on scale makes the early expansion feel fast
    /// and punchy before slowing into the fade.
    /// </summary>
    private IEnumerator RunRing(Vector3 worldPos, Sprite sprite,
                                 float startSize, Color color,
                                 float duration, float endScale)
    {
        var go = new GameObject("Ring", typeof(RectTransform));
        go.transform.SetParent(transform, worldPositionStays: false);

        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.position  = worldPos;
        rt.sizeDelta = new Vector2(startSize, startSize);

        Graphic graphic;
        if (sprite != null)
        {
            var img            = go.AddComponent<Image>();
            img.sprite         = sprite;
            img.preserveAspect = true;
            img.type           = Image.Type.Simple;
            img.raycastTarget  = false;
            graphic            = img;
        }
        else
        {
            var raw           = go.AddComponent<RawImage>();
            raw.raycastTarget = false;
            graphic           = raw;
        }

        graphic.color = new Color(color.r, color.g, color.b, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.SmoothStep(1f, 0f, t);
            float scale = Mathf.Lerp(1f, endScale, Mathf.Pow(t, 0.6f));
            rt.localScale = new Vector3(scale, scale, 1f);
            graphic.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        Destroy(go);
    }
}
