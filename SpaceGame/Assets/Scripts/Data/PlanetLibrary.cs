using UnityEngine;

/// <summary>
/// ScriptableObject holding all planet and star sprites, keyed by filename stem.
///
/// Populated by the editor menu item:  Star Captain → Build Planet Library
/// Asset location:  Assets/Resources/PlanetLibrary.asset
///
/// ── Key format ────────────────────────────────────────────────────────────
///
///   Planet keys  :  "{TypePrefix}_{variant:D2}"  e.g. "Terrestrial_01", "BlueGiant_03"
///   Star keys    :  "Sun_{Color}_{variant:D2}"   e.g. "Sun_Yellow_01",  "Sun_Red_02"
///
///   Note: the orange gaseous type is keyed "OrgangeGiant_XX" (asset pack typo).
///
/// ── Lookup API ────────────────────────────────────────────────────────────
///
///   GetPlanetSprite(PlanetType.Terrestrial)          → Terrestrial_01 sprite
///   GetPlanetSprite(PlanetType.GaseousOrange, 3)     → OrgangeGiant_03 sprite
///   GetStarSprite(StarType.YellowDwarf)              → Sun_Yellow_01 sprite
///   GetStarSprite(StarType.RedDwarf, 2)              → Sun_Red_02 sprite
/// </summary>
[CreateAssetMenu(fileName = "PlanetLibrary", menuName = "Star Captain/Planet Library")]
public class PlanetLibrary : ScriptableObject
{
    [Header("Planet Sprites")]
    [Tooltip("All planet sprites (solid and gaseous). Parallel array with PlanetKeys.")]
    public Sprite[] PlanetSprites;

    [Tooltip("Filename stem for each planet sprite, e.g. 'Terrestrial_01'. Parallel array with PlanetSprites.")]
    public string[] PlanetKeys;

    [Header("Star Sprites")]
    [Tooltip("All star/sun sprites. Parallel array with StarKeys.")]
    public Sprite[] StarSprites;

    [Tooltip("Filename stem for each star sprite, e.g. 'Sun_Yellow_01'. Parallel array with StarSprites.")]
    public string[] StarKeys;

    // -----------------------------------------------------------------------
    // Validity
    // -----------------------------------------------------------------------

    public bool IsValid =>
        PlanetSprites != null && PlanetKeys   != null &&
        StarSprites   != null && StarKeys     != null &&
        PlanetSprites.Length == PlanetKeys.Length &&
        StarSprites.Length   == StarKeys.Length &&
        PlanetSprites.Length > 0 &&
        StarSprites.Length   > 0;

    // -----------------------------------------------------------------------
    // Planet lookups
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the sprite for the given planet type and variant (1–5).
    /// Falls back to variant 1, then to null if not found.
    /// </summary>
    public Sprite GetPlanetSprite(PlanetType type, int variant = 1)
    {
        variant = Mathf.Clamp(variant, 1, 5);
        string key = $"{type.AssetPrefix()}_{variant:D2}";
        return GetPlanetSprite(key) ?? GetPlanetSprite($"{type.AssetPrefix()}_01");
    }

    /// <summary>Returns the sprite for an exact key string, or null if not found.</summary>
    public Sprite GetPlanetSprite(string key)
    {
        if (PlanetSprites == null || PlanetKeys == null) return null;
        for (int i = 0; i < PlanetKeys.Length; i++)
            if (PlanetKeys[i] == key) return PlanetSprites[i];
        return null;
    }

    // -----------------------------------------------------------------------
    // Star lookups
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the sprite for the given star type and variant (1–5).
    /// Falls back to variant 1, then to null if not found.
    /// </summary>
    public Sprite GetStarSprite(StarType type, int variant = 1)
    {
        variant = Mathf.Clamp(variant, 1, 5);
        string key = StarKey(type, variant);
        return GetStarSprite(key) ?? GetStarSprite(StarKey(type, 1));
    }

    /// <summary>Returns the sprite for an exact star key string, or null if not found.</summary>
    public Sprite GetStarSprite(string key)
    {
        if (StarSprites == null || StarKeys == null) return null;
        for (int i = 0; i < StarKeys.Length; i++)
            if (StarKeys[i] == key) return StarSprites[i];
        return null;
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private static string StarKey(StarType type, int variant)
    {
        string color = type switch
        {
            StarType.YellowDwarf => "Yellow",
            StarType.RedDwarf    => "Red",
            StarType.BlueGiant   => "Blue",
            _                    => "Yellow",
        };
        return $"Sun_{color}_{variant:D2}";
    }
}
