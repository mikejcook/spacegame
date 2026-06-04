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
/// The slot is sized at runtime to match the sprite's natural pixel dimensions,
/// so larger sprite textures appear larger in the arena.
/// </summary>
[System.Serializable]
public class EnemyShipConfig
{
    public DGBShipColor color     = DGBShipColor.Red;
    public DGBShipClass shipClass = DGBShipClass.Cruiser;

    /// <summary>
    /// Sprite variant number (1, 2, …). Not all class/colour combos have multiple
    /// variants — the lookup falls back to variant 1 if the requested number is missing.
    /// </summary>
    public int variant = 1;
}
