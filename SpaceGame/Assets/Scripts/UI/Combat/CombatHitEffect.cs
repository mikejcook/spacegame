using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quick expanding glow ring spawned at the point of impact.
///
///   Beam hit    : small cyan ring that expands and fades (~0.28 s).
///   Explosion   : two overlapping orange/yellow rings (~0.34 s total).
///
/// Usage:
///   CombatHitEffect.SpawnAt(projectileContainer, worldPos, glowSprite, HitType.Explosion);
///
/// Safe to call when glowSprite is null — falls back to a plain coloured quad.
/// The host GameObject self-destructs when the animation completes.
/// </summary>
public class CombatHitEffect : MonoBehaviour
{
    public enum HitType { Beam, Explosion }

    // ── Sizes (canvas units at 1920 × 1080 reference) ────────────────────────
    private const float BeamRingSize   = 110f;
    private const float ExplosionSize1 = 200f;   // outer orange ring
    private const float ExplosionSize2 = 150f;   // inner yellow flash

    // ── Duration (seconds) ────────────────────────────────────────────────────
    private const float BeamDuration       = 0.28f;
    private const float ExplosionDuration  = 0.34f;

    // ── End-scale: the ring expands from 1× to this while fading ─────────────
    private const float BeamEndScale      = 2.0f;
    private const float ExplosionEndScale = 2.4f;

    // ── Colors ────────────────────────────────────────────────────────────────
    private static readonly Color BeamHitColor    = new Color(0.30f, 0.85f, 1.00f, 1f);  // cyan
    private static readonly Color ExplosionColor1 = new Color(1.00f, 0.55f, 0.10f, 1f);  // orange
    private static readonly Color ExplosionColor2 = new Color(1.00f, 0.92f, 0.30f, 1f);  // yellow

    // ── Static factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Spawn a hit effect inside <paramref name="container"/> at <paramref name="worldPos"/>.
    /// No-ops gracefully if container is null.
    /// </summary>
    public static void SpawnAt(RectTransform container, Vector3 worldPos,
                                Sprite glowSprite, HitType hitType)
    {
        if (container == null) return;
        var go = new GameObject("HitEffect", typeof(RectTransform));
        go.transform.SetParent(container, worldPositionStays: false);
        go.AddComponent<CombatHitEffect>().Fire(worldPos, glowSprite, hitType);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Fire(Vector3 worldPos, Sprite glowSprite, HitType hitType)
    {
        StartCoroutine(hitType == HitType.Beam
            ? BeamRoutine(worldPos, glowSprite)
            : ExplosionRoutine(worldPos, glowSprite));
    }

    private IEnumerator BeamRoutine(Vector3 worldPos, Sprite glowSprite)
    {
        yield return RunRing(worldPos, glowSprite, BeamRingSize,
                             BeamEndScale, BeamHitColor, BeamDuration);
        Destroy(gameObject);
    }

    private IEnumerator ExplosionRoutine(Vector3 worldPos, Sprite glowSprite)
    {
        // Inner yellow flash fires immediately; outer orange ring runs slightly longer.
        StartCoroutine(RunRing(worldPos, glowSprite, ExplosionSize2,
                               1.8f, ExplosionColor2, ExplosionDuration * 0.7f));
        yield return RunRing(worldPos, glowSprite, ExplosionSize1,
                             ExplosionEndScale, ExplosionColor1, ExplosionDuration);
        Destroy(gameObject);
    }

    /// <summary>
    /// Animate one ring: expands from 1× to endScale while alpha fades 1→0.
    /// The ring child GameObject is destroyed when done.
    /// </summary>
    private IEnumerator RunRing(Vector3 worldPos, Sprite sprite,
                                 float startSize, float endScale,
                                 Color color, float duration)
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
            var raw            = go.AddComponent<RawImage>();
            raw.raycastTarget  = false;
            graphic            = raw;
        }

        // Start fully visible; SmoothStep fades to 0 over the duration.
        graphic.color = new Color(color.r, color.g, color.b, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.SmoothStep(1f, 0f, t);
            float scale = Mathf.Lerp(1f, endScale, t);
            rt.localScale = new Vector3(scale, scale, 1f);
            graphic.color = new Color(color.r, color.g, color.b, alpha);
            yield return null;
        }

        Destroy(go);
    }
}
