using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a sci-fi beam weapon attack as two UI elements inside the
/// ProjectileLayer:
///
///   • BeamLine  — a RawImage using laser_noise00.png, stretched and rotated
///                 to span from the player ship to the targeted enemy slot.
///   • ImpactGlow — an Image using glow_round00.png centred on the target.
///
/// Animation: fade-in → hold → fade-out.  The whole GameObject destroys
/// itself when the animation finishes.
///
/// Usage (called by CombatViewController.LaunchBeamWeapon):
///
///   var go     = new GameObject("BeamEffect", typeof(RectTransform));
///   go.transform.SetParent(projectileContainer, worldPositionStays: false);
///   var effect = go.AddComponent&lt;CombatBeamEffect&gt;();
///   effect.Fire(startWorld, endWorld, beamTexture, glowSprite);
/// </summary>
public class CombatBeamEffect : MonoBehaviour
{
    // ── Visual constants (canvas units at 1920×1080 reference) ───────────────
    private const float BeamThickness = 14f;   // height of the beam strip
    private const float GlowSize      = 130f;  // diameter of impact glow circle
    private const float MuzzleSize    = 60f;   // diameter of origin flash

    // Tile the laser noise once every N canvas units along the beam length.
    private const float TilePer = 96f;

    // ── Tint ─────────────────────────────────────────────────────────────────
    // Matches AccentCyan from GameSceneSetup (0.30, 0.85, 1.00).
    private static readonly Color BeamTint   = new Color(0.30f, 0.85f, 1.00f, 1f);
    private static readonly Color GlowTint   = new Color(0.30f, 0.85f, 1.00f, 1f);
    private static readonly Color MuzzleTint = new Color(0.70f, 0.95f, 1.00f, 1f);

    // ── Timing (seconds) ─────────────────────────────────────────────────────
    private const float FadeInTime  = 0.05f;
    private const float HoldTime    = 0.12f;
    private const float FadeOutTime = 0.20f;

    // ── Peak alpha for each element ───────────────────────────────────────────
    private const float BeamAlpha   = 0.90f;
    private const float GlowAlpha   = 0.85f;
    private const float MuzzleAlpha = 0.75f;

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Begin the beam effect.  Call once immediately after AddComponent.
    /// </summary>
    /// <param name="startWorld">World-space centre of the player ship.</param>
    /// <param name="endWorld">World-space centre of the targeted enemy slot.</param>
    /// <param name="beamTexture">laser_noise00 texture for the beam strip (may be null — beam still fires).</param>
    /// <param name="glowSprite">glow_round00 sprite for the impact circle (may be null).</param>
    public void Fire(Vector3 startWorld, Vector3 endWorld,
                     Texture beamTexture, Sprite glowSprite)
    {
        StartCoroutine(BeamRoutine(startWorld, endWorld, beamTexture, glowSprite));
    }

    // -----------------------------------------------------------------------
    // Private — animation coroutine
    // -----------------------------------------------------------------------

    private IEnumerator BeamRoutine(Vector3 start, Vector3 end,
                                    Texture beamTexture, Sprite glowSprite)
    {
        // ── Geometry ──────────────────────────────────────────────────────
        Vector3 dir      = end - start;
        float   distance = dir.magnitude;
        float   angle    = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;  // angle from +X

        // ── Beam line ─────────────────────────────────────────────────────
        var beamRI = MakeBeamLine(start, end, distance, angle, beamTexture);

        // ── Impact glow ───────────────────────────────────────────────────
        Image    glowImg = null;
        RawImage glowRI  = null;
        if (glowSprite != null)
        {
            glowImg = MakeGlowImage(end, GlowSize, glowSprite);
        }
        else
        {
            // Fallback: plain white rectangle if the glow sprite is missing.
            glowRI = MakeGlowRaw(end, GlowSize);
        }

        // ── Muzzle flash ──────────────────────────────────────────────────
        // Small glow at the origin, fades out on its own half-way through.
        Image    muzzleImg = null;
        RawImage muzzleRI  = null;
        if (glowSprite != null)
        {
            muzzleImg = MakeGlowImage(start, MuzzleSize, glowSprite);
            muzzleImg.color = new Color(MuzzleTint.r, MuzzleTint.g, MuzzleTint.b, 0f);
        }
        else
        {
            muzzleRI = MakeGlowRaw(start, MuzzleSize);
        }

        // ── Helpers ───────────────────────────────────────────────────────
        // Sets the alpha on all four possible graphic references.
        void SetBeamAlpha(float a)
        {
            SetGraphicAlpha(beamRI,   a * BeamAlpha);
        }

        void SetGlowAlpha(float a)
        {
            SetGraphicAlpha(glowImg,  a * GlowAlpha);
            SetGraphicAlpha(glowRI,   a * GlowAlpha);
        }

        void SetMuzzleAlpha(float a)
        {
            SetGraphicAlpha(muzzleImg, a * MuzzleAlpha);
            SetGraphicAlpha(muzzleRI,  a * MuzzleAlpha);
        }

        // ── Fade in ───────────────────────────────────────────────────────
        for (float t = 0f; t < 1f; t += Time.deltaTime / FadeInTime)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            SetBeamAlpha(s);
            SetGlowAlpha(s);
            SetMuzzleAlpha(s);
            yield return null;
        }
        SetBeamAlpha(1f); SetGlowAlpha(1f); SetMuzzleAlpha(1f);

        // Muzzle fades out mid-hold while the beam and glow hold steady.
        float muzzleElapsed = 0f;
        float holdElapsed   = 0f;
        while (holdElapsed < HoldTime)
        {
            float dt = Time.deltaTime;
            holdElapsed   += dt;
            muzzleElapsed += dt;
            float muzzleFade = 1f - Mathf.Clamp01(muzzleElapsed / (HoldTime * 0.6f));
            SetMuzzleAlpha(muzzleFade);
            yield return null;
        }
        SetMuzzleAlpha(0f);

        // ── Fade out ──────────────────────────────────────────────────────
        for (float t = 0f; t < 1f; t += Time.deltaTime / FadeOutTime)
        {
            float s = Mathf.SmoothStep(1f, 0f, t);
            SetBeamAlpha(s);
            SetGlowAlpha(s);
            yield return null;
        }

        Destroy(gameObject);
    }

    // -----------------------------------------------------------------------
    // Factory helpers — build child UI elements
    // -----------------------------------------------------------------------

    private RawImage MakeBeamLine(Vector3 start, Vector3 end,
                                   float distance, float angleDeg,
                                   Texture tex)
    {
        var go = new GameObject("BeamLine", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.5f, 0.5f);
        rt.anchorMax    = new Vector2(0.5f, 0.5f);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.position     = (start + end) * 0.5f;   // world-space midpoint
        rt.sizeDelta    = new Vector2(distance, BeamThickness);
        rt.localEulerAngles = new Vector3(0f, 0f, angleDeg);

        var ri           = go.AddComponent<RawImage>();
        ri.texture       = tex;
        ri.color         = new Color(BeamTint.r, BeamTint.g, BeamTint.b, 0f);
        ri.raycastTarget = false;

        if (tex != null)
        {
            // Tile the noise texture along the beam's length for a richer look.
            float tiles = Mathf.Max(1f, distance / TilePer);
            ri.uvRect   = new Rect(0f, 0f, tiles, 1f);
        }

        return ri;
    }

    private Image MakeGlowImage(Vector3 worldPos, float size, Sprite sprite)
    {
        var go = new GameObject("Glow", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.5f, 0.5f);
        rt.anchorMax    = new Vector2(0.5f, 0.5f);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.position     = worldPos;
        rt.sizeDelta    = new Vector2(size, size);

        var img           = go.AddComponent<Image>();
        img.sprite        = sprite;
        img.preserveAspect = true;
        img.type          = Image.Type.Simple;
        img.color         = new Color(GlowTint.r, GlowTint.g, GlowTint.b, 0f);
        img.raycastTarget = false;

        return img;
    }

    private RawImage MakeGlowRaw(Vector3 worldPos, float size)
    {
        var go = new GameObject("Glow", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.5f, 0.5f);
        rt.anchorMax    = new Vector2(0.5f, 0.5f);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.position     = worldPos;
        rt.sizeDelta    = new Vector2(size, size);

        var ri           = go.AddComponent<RawImage>();
        ri.color         = new Color(GlowTint.r, GlowTint.g, GlowTint.b, 0f);
        ri.raycastTarget = false;
        return ri;
    }

    // -----------------------------------------------------------------------
    // Utility
    // -----------------------------------------------------------------------

    private static void SetGraphicAlpha(Graphic g, float a)
    {
        if (g == null) return;
        var c = g.color;
        c.a   = Mathf.Clamp01(a);
        g.color = c;
    }
}
