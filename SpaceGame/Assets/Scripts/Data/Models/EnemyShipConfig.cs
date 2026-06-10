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
///
/// Combat stats default to values appropriate for the ship class but can be
/// overridden per-encounter for boss fights or scripted events.
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

    // ── Combat stats ──────────────────────────────────────────────────────────
    // All fields default to -1 meaning "derive from ship class."
    // Set a field explicitly to override the class default for a specific encounter.

    /// <summary>Max shield HP. -1 = use class default.</summary>
    public int maxShields = -1;
    /// <summary>Max hull HP. -1 = use class default.</summary>
    public int maxHull    = -1;

    /// <summary>Pilot's skill rank added to defense rolls. -1 = use class default.</summary>
    public int pilotSkill  = -1;
    /// <summary>Weapon operator's skill rank added to attack rolls. -1 = use class default.</summary>
    public int weaponSkill = -1;

    /// <summary>Beam weapon tier (1–6). Adds (tier-1) to attack and damage. -1 = use class default.</summary>
    public int beamTier    = -1;
    /// <summary>Torpedo tier (1–6). Adds (tier-1) to attack and damage. -1 = use class default.</summary>
    public int torpedoTier = -1;

    /// <summary>Defense bonus from shields added to defense roll while shields are up. -1 = use class default.</summary>
    public int shieldTier = -1;
    /// <summary>Defense bonus from armor added to defense roll when shields are down. -1 = use class default.</summary>
    public int armorTier  = -1;

    // ── Resolved accessors (apply class defaults when field == -1) ───────────

    public int MaxShields          => maxShields          >= 0 ? maxShields          : ClassDefaults.shields;
    public int MaxHull             => maxHull             >= 0 ? maxHull             : ClassDefaults.hull;
    public int PilotSkill          => pilotSkill          >= 0 ? pilotSkill          : ClassDefaults.pilot;
    public int WeaponSkill         => weaponSkill         >= 0 ? weaponSkill         : ClassDefaults.weapon;
    public int BeamTier            => beamTier            >= 0 ? beamTier            : ClassDefaults.beamTier;
    public int TorpedoTier         => torpedoTier         >= 0 ? torpedoTier         : ClassDefaults.torpedoTier;
    public int ShieldTier          => shieldTier          >= 0 ? shieldTier          : ClassDefaults.shieldTier;
    public int ArmorTier           => armorTier           >= 0 ? armorTier           : ClassDefaults.armorTier;

    // ── Class defaults ────────────────────────────────────────────────────────
    // Tuning table: (maxShields, maxHull, pilotSkill, weaponSkill, beamTier, torpedoTier, shieldDR, armorDR)
    //
    //   Fighter     — fast, fragile, aggressive pilot
    //   Corvette    — balanced starter-tier enemy
    //   Cruiser     — heavy hitter, mid-tier defenses
    //   Battleship  — tanky, slow, hits hard
    //   Dreadnaught — boss-tier, built to survive
    //
    // Defense rolls: d20 + pilotSkill + shieldDR (shields up) or armorDR (shields down)
    // Attack rolls:  d20 + weaponSkill + (beamTier - 1) or (torpedoTier - 1)

    private (int shields, int hull, int pilot, int weapon, int beamTier, int torpedoTier, int shieldTier, int armorTier)
        ClassDefaults => shipClass switch
    {
        DGBShipClass.Fighter     => (10,  18, 1, 1, 0, 0, 0, 0),
        DGBShipClass.Corvette    => (10,  30, 2, 2, 1, 1, 1, 1),
        DGBShipClass.Cruiser     => (35,  55, 3, 3, 2, 2, 2, 2),
        DGBShipClass.Battleship  => (50,  80, 4, 4, 3, 3, 3, 3),
        DGBShipClass.Dreadnaught => (70, 120, 5, 5, 4, 4, 4, 4),
        _                        => (15,  25, 1, 1, 1, 1, 0, 0)
    };
}
