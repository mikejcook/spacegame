/// <summary>
/// Planet visual categories, each mapping to a named sprite set in PlanetLibrary.
///
/// Solid types correspond directly to the asset pack folder names
/// (e.g. Terrestrial → Terrestrial_01 … Terrestrial_05).
///
/// Gaseous types map to their asset filenames:
///   GaseousBlue   → BlueGiant_XX
///   GaseousGreen  → GreenGiant_XX
///   GaseousOrange → OrgangeGiant_XX   ← original asset typo preserved
///   GaseousRed    → RedGiant_XX
///   GaseousYellow → YellowGiant_XX
///
/// The int values are stored in the database as-is. Do NOT reorder or
/// renumber existing entries — add new ones at the end only.
/// </summary>
public enum PlanetType
{
    // ── Solid ──────────────────────────────────────────────────────────────
    Airless     = 0,
    Aquamarine  = 1,
    Arid        = 2,
    Barren      = 3,
    Cloudy      = 4,
    Cratered    = 5,
    Dry         = 6,
    Frozen      = 7,
    Glacial     = 8,
    Icy         = 9,
    Lunar       = 10,
    Lush        = 11,
    Magma       = 12,
    Muddy       = 13,
    Oasis       = 14,
    Ocean       = 15,
    Rocky       = 16,
    Snowy       = 17,
    Terrestrial = 18,
    Tropical    = 19,

    // ── Gaseous ────────────────────────────────────────────────────────────
    GaseousBlue   = 20,
    GaseousGreen  = 21,
    GaseousOrange = 22,
    GaseousRed    = 23,
    GaseousYellow = 24,
}

/// <summary>
/// Extension helpers for PlanetType.
/// </summary>
public static class PlanetTypeExtensions
{
    /// <summary>
    /// Returns true if this is a gaseous planet type.
    /// Gaseous planets are displayed larger and have gas giant resource profiles.
    /// </summary>
    public static bool IsGaseous(this PlanetType type) => type >= PlanetType.GaseousBlue;

    /// <summary>
    /// Base display diameter (canvas px) for a planet of this type, before any
    /// per-system size variation is applied. Five tiers:
    ///   tiny   (airless rocks)        ~40 px
    ///   small  (arid/frozen worlds)   ~52 px
    ///   medium (habitable/ocean)      ~64 px
    ///   large  (icy/magma)            ~72 px
    ///   giant  (all gaseous types)    ~90 px
    /// </summary>
    public static float BaseDisplaySize(this PlanetType type)
    {
        if (type.IsGaseous()) return 90f;
        return type switch
        {
            PlanetType.Airless or PlanetType.Barren or PlanetType.Cratered
                or PlanetType.Lunar or PlanetType.Rocky                       => 40f,
            PlanetType.Arid or PlanetType.Dry or PlanetType.Frozen
                or PlanetType.Glacial or PlanetType.Snowy                     => 52f,
            PlanetType.Icy or PlanetType.Magma                                => 72f,
            _                                                                  => 64f,
        };
    }

    /// <summary>
    /// Returns the filename stem used by PlanetLibrary (without the _XX suffix).
    /// Handles the typo in the original asset pack ("OrgangeGiant" vs "OrangeGiant").
    /// </summary>
    public static string AssetPrefix(this PlanetType type) => type switch
    {
        PlanetType.GaseousBlue   => "BlueGiant",
        PlanetType.GaseousGreen  => "GreenGiant",
        PlanetType.GaseousOrange => "OrgangeGiant",   // original asset typo
        PlanetType.GaseousRed    => "RedGiant",
        PlanetType.GaseousYellow => "YellowGiant",
        _                        => type.ToString(),  // solid types match their name exactly
    };
}
