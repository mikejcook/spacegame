/// <summary>
/// How large the enemy ship appears in the combat arena.
/// Independent of the ship's class — a Fighter can be shown at Large display
/// size and a Dreadnaught at Small if the encounter calls for it.
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
/// Faction colour of a DGB ship sprite.
/// Maps to the first token of the filename: "{color}_{class}_{variant}.png".
/// </summary>
public enum DGBShipColor
{
    Blue,
    Green,
    Red,
}

/// <summary>
/// Ship class tier, smallest to largest.
/// Maps to the second token of the filename: "{color}_{class}_{variant}.png".
/// Not every class exists for every colour — the sprite lookup falls back
/// gracefully when a combination is missing.
/// </summary>
public enum DGBShipClass
{
    Fighter,
    Corvette,
    Cruiser,
    Battleship,
    Dreadnaught,
}

/// <summary>
/// Describes a single enemy ship for space combat.
/// Passed to CombatViewController.StartCombat() to configure each enemy slot.
/// </summary>
[System.Serializable]
public class EnemyShipConfig
{
    public DGBShipColor          color       = DGBShipColor.Red;
    public DGBShipClass          shipClass   = DGBShipClass.Cruiser;
    public CombatShipDisplaySize displaySize = CombatShipDisplaySize.Medium;

    /// <summary>
    /// Sprite variant number (1, 2, …). Not all class/colour combos have multiple
    /// variants — the lookup falls back to variant 1 if the requested number is missing.
    /// </summary>
    public int variant = 1;
}
