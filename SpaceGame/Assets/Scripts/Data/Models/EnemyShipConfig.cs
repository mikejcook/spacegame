/// <summary>
/// How large the enemy ship appears in the combat arena.
/// Independent of the DGB sprite's own size tier — a Tiny sprite can be
/// shown at Large display size and vice versa.
/// </summary>
public enum CombatShipDisplaySize
{
    /// <summary>Roughly the same footprint as the player ship.</summary>
    Small,
    /// <summary>Midpoint between Small and Large.</summary>
    Medium,
    /// <summary>Full-size threat — the largest slot.</summary>
    Large,
}

/// <summary>
/// Colour faction of a DGB Spaceship sprite.
/// Maps directly to the filename prefix (e.g. "red_medium00.png").
/// </summary>
public enum DGBShipColor
{
    Blue,
    Green,
    Red,
}

/// <summary>
/// Size class of a DGB Spaceship sprite.
/// Maps to the size token in the filename (e.g. "medium" in "red_medium00.png").
/// Note: Missile is only available in the Red colour.
/// </summary>
public enum DGBShipSize
{
    Tiny,
    Small,
    Medium,
    Large,
    Huge,
    Missile,
}

/// <summary>
/// Describes a single enemy ship for space combat.
/// Passed to CombatViewController.StartCombat() to configure each enemy slot.
/// </summary>
[System.Serializable]
public class EnemyShipConfig
{
    public DGBShipColor          color       = DGBShipColor.Red;
    public DGBShipSize           size        = DGBShipSize.Medium;
    public CombatShipDisplaySize displaySize = CombatShipDisplaySize.Medium;

    /// <summary>
    /// Sprite variant index (0 or 1). Not all size/colour combos have a variant 1
    /// — the lookup falls back to variant 0 if variant 1 is missing.
    /// </summary>
    public int variant = 0;
}
