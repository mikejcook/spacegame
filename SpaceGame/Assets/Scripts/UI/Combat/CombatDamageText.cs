using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Floating damage / miss label that appears at a world position and drifts
/// upward while fading out.
///
/// Usage:
///   CombatDamageText.Spawn(projectileContainer, worldPos, "14");
///   CombatDamageText.Spawn(projectileContainer, worldPos, "MISS");
///
/// Spawned inside the projectile container (same layer as beams/torpedoes)
/// so it renders above the ship images.  Self-destructs when done.
/// </summary>
public class CombatDamageText : MonoBehaviour
{
    // ── Timing ────────────────────────────────────────────────────────────────
    private const float Duration      = 1.6f;   // total seconds before destruction
    private const float HoldFraction  = 0.30f;  // fraction of Duration at full alpha before fading

    // ── Motion ────────────────────────────────────────────────────────────────
    // World-space pixels to drift upward (Screen Space Overlay: 1 world unit ≈ 1 screen pixel).
    // Expressed as a fraction of screen height so it scales across resolutions.
    private const float FloatScreenFraction = 0.07f;

    // ── Style ─────────────────────────────────────────────────────────────────
    private static readonly Color DamageColor = new Color(1.00f, 0.20f, 0.10f, 1f);  // vivid red
    private const float FontSize = 56f;

    // ── Static factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Spawn a damage/miss label inside <paramref name="container"/> at
    /// <paramref name="worldPos"/>.  No-ops gracefully if container is null.
    /// </summary>
    public static void Spawn(RectTransform container, Vector3 worldPos, string text)
    {
        if (container == null || string.IsNullOrEmpty(text)) return;
        var go = new GameObject("DamageText", typeof(RectTransform));
        go.transform.SetParent(container, worldPositionStays: false);
        go.AddComponent<CombatDamageText>().Fire(worldPos, text);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void Fire(Vector3 worldPos, string text)
    {
        // Position the rect at the target world position.
        var rt       = GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.position  = worldPos;
        rt.sizeDelta = new Vector2(220f, 90f);

        // TMP label — no raycast target so it doesn't block ship tap buttons.
        var tmp               = gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text              = text;
        tmp.fontSize          = FontSize;
        tmp.fontStyle         = FontStyles.Bold;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.color             = DamageColor;
        tmp.raycastTarget     = false;
        tmp.overflowMode      = TextOverflowModes.Overflow;
        tmp.enableWordWrapping = false;

        StartCoroutine(AnimateRoutine(rt, tmp));
    }

    private IEnumerator AnimateRoutine(RectTransform rt, TextMeshProUGUI tmp)
    {
        float floatPixels = Screen.height * FloatScreenFraction;
        Color baseColor   = tmp.color;

        Vector3 startPos = rt.position;
        Vector3 endPos   = startPos + new Vector3(0f, floatPixels, 0f);

        float elapsed = 0f;
        while (elapsed < Duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);

            // Float upward with a fast-then-slow ease (power curve).
            rt.position = Vector3.Lerp(startPos, endPos, Mathf.Pow(t, 0.35f));

            // Hold full alpha for the first HoldFraction, then linear fade to zero.
            float fadeT = Mathf.InverseLerp(HoldFraction, 1f, t);
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b,
                                  Mathf.Lerp(1f, 0f, fadeT));

            yield return null;
        }

        Destroy(gameObject);
    }
}
