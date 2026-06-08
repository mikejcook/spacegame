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

    // ── Default tint (player beam) ────────────────────────────────────────────
    // Matches AccentCyan from GameSceneSetup (0.30, 0.85, 1.00).
    public static readonly Color PlayerBeamTint = new Color(0.30f, 0.85f, 1.00f, 1f);
    // Enemy beam: hostile orange-red.
    public static readonly Color EnemyBeamTint  = new Color(1.00f, 0.35f, 0.10f, 1f);

    // ── Timing (seconds) ─────────────────────────────────────────────────────
    private const float FadeInTime  = 0.05f;
    private const float HoldTime    = 0.12f;
    private const float FadeOutTime = 0.20f;

    /// <summary>Total wall-clock duration of the beam animation. Use this to know when to wait.</summary>
    public const float AnimationDuration = FadeInTime + HoldTime + FadeOutTime;

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
    /// <param name="startWorld">World-space origin of the beam.</param>
    /// <param name="endWorld">World-space target of the beam.</param>
    /// <param name="beamTexture">laser_noise00 texture for the beam strip (may be null).</param>
    /// <param name="glowSprite">glow_round00 sprite for the impact circle (may be null).</param>
    /// <param name="tint">Base color for beam, glow, and muzzle flash. Defaults to PlayerBeamTint (cyan).</param>
    /// <param name="impactSize">Diameter of the impact glow at <paramref name="endWorld"/>.
    /// Defaults to GlowSize; pass a value scaled to the target sprite so the glow
    /// doesn't dwarf small ships (a 24×29 fighter vs a 280px dreadnaught).</param>
    /// <param name="muzzleSize">Diameter of the muzzle flash at <paramref name="startWorld"/>.
    /// Defaults to MuzzleSize; scale to the firing sprite for the same reason.</param>
    public void Fire(Vector3 startWorld, Vector3 endWorld,
                     Texture beamTexture, Sprite glowSprite,
                     Color? tint = null,
                     float impactSize = GlowSize,
                     float muzzleSize = MuzzleSize)
    {
        Color baseColor = tint ?? PlayerBeamTint;
        StartCoroutine(BeamRoutine(startWorld, endWorld, beamTexture, glowSprite,
                                   baseColor, impactSize, muzzleSize));
    }

    // -----------------------------------------------------------------------
    // Private — animation coroutine
    // -----------------------------------------------------------------------

    private IEnumerator BeamRoutine(Vector3 start, Vector3 end,
                                    Texture beamTexture, Sprite glowSprite,
                                    Color baseColor,
                                    float impactSize, float muzzleSize)
    {
        // Derive per-instance tints from the base color.
        Color beamTint   = baseColor;
        Color glowTint   = baseColor;
        Color muzzleTint = Color.Lerp(baseColor, Color.white, 0.4f);

        // ── Geometry ──────────────────────────────────────────────────────
        Vector3 dir      = end - start;
        float   distance = dir.magnitude;
        float   angle    = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;  // angle from +X

        // ── Beam line ─────────────────────────────────────────────────────
        var beamRI = MakeBeamLine(start, end, distance, angle, beamTexture, beamTint);

        // ── Impact glow ───────────────────────────────────────────────────
        Image    glowImg = null;
        RawImage glowRI  = null;
        if (glowSprite != null)
        {
            glowImg = MakeGlowImage(end, impactSize, glowSprite, glowTint);
        }
        else
        {
            // Fallback: plain colored rectangle if the glow sprite is missing.
            glowRI = MakeGlowRaw(end, impactSize, glowTint);
        }

        // ── Muzzle flash ──────────────────────────────────────────────────
        Image    muzzleImg = null;
        RawImage muzzleRI  = null;
        if (glowSprite != null)
        {
            muzzleImg = MakeGlowImage(start, muzzleSize, glowSprite, muzzleTint);
            muzzleImg.color = new Color(muzzleTint.r, muzzleTint.g, muzzleTint.b, 0f);
        }
        else
        {
            muzzleRI = MakeGlowRaw(start, muzzleSize, muzzleTint);
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
                                   Texture tex, Color tint)
    {
        var go = new GameObject("BeamLine", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt          = go.GetComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.5f, 0.5f);
        rt.anchorMax    = new Vector2(0.5f, 0.5f);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.position     = (start + end) * 0.5f;

        // `distance` is a WORLD-space magnitude — start/end come from
        // RectTransform.TransformPoint. But sizeDelta is expressed in this rect's
        // LOCAL units, and under a CanvasScaler the canvas root scale is not 1
        // (≈ Screen height / 1080 on device). Assigning the world distance
        // directly therefore makes the beam the wrong length: it overshoots the
        // target on the way out and, fired the other way, begins behind the
        // shooter. Divide by lossyScale to convert world units → local units.
        // (rt.position is already world-space, so the centre stays correct — only
        // the length needs converting. BeamThickness is authored in local units.)
        float worldScale = rt.lossyScale.x;
        if (Mathf.Abs(worldScale) < 1e-4f) worldScale = 1f;
        rt.sizeDelta    = new Vector2(distance / worldScale, BeamThickness);
        rt.localEulerAngles = new Vector3(0f, 0f, angleDeg);

        var ri           = go.AddComponent<RawImage>();
        ri.texture       = tex;
        ri.color         = new Color(tint.r, tint.g, tint.b, 0f);
        ri.raycastTarget = false;

        if (tex != null)
        {
            float tiles = Mathf.Max(1f, distance / TilePer);
            ri.uvRect   = new Rect(0f, 0f, tiles, 1f);
        }

        return ri;
    }

    private Image MakeGlowImage(Vector3 worldPos, float size, Sprite sprite, Color tint)
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
        img.color         = new Color(tint.r, tint.g, tint.b, 0f);
        img.raycastTarget = false;

        return img;
    }

    private RawImage MakeGlowRaw(Vector3 worldPos, float size, Color tint)
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
        ri.color         = new Color(tint.r, tint.g, tint.b, 0f);
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
