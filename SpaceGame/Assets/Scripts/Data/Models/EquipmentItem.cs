using SQLite;

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------

public enum EquipmentType
{
    Engine,
    Shield,
    Reactor,
    Sensor,
    Weapon,
    CargoExpansion,
    Special
}

public enum WeaponType
{
    None,
    BeamLaser,
    PulseCannon,
    Missile,
    Torpedo,
    RailGun
}

public enum EquipmentTier
{
    Basic    = 1,
    Standard = 2,
    Advanced = 3,
    Military = 4,
    Prototype = 5
}

// ---------------------------------------------------------------------------
// Equipment model
// ---------------------------------------------------------------------------

/// <summary>
/// Represents any installable ship component or weapon.
/// Items live in the player's inventory (IsInstalled = false) or
/// are slotted into the ship (IsInstalled = true).
/// </summary>
[Table("Equipment")]
public class EquipmentItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int    SaveGameId   { get; set; }
    public string Name         { get; set; }
    public string Description  { get; set; }

    public EquipmentType EquipmentType { get; set; }
    public WeaponType    WeaponType    { get; set; }
    public EquipmentTier Tier          { get; set; }

    // Stat contributions — relevance depends on EquipmentType:
    //   Engine   -> PowerRating = speed/FTL bonus
    //   Reactor  -> PowerRating = power output (unlocks better equipment)
    //   Shield   -> DefenseRating = shield HP added
    //   Sensor   -> RangeRating = detection radius
    //   Weapon   -> PowerRating = damage, RangeRating = range
    //   Cargo    -> CargoRating = extra cargo units
    public int PowerRating   { get; set; }
    public int DefenseRating { get; set; }
    public int RangeRating   { get; set; }
    public int CargoRating   { get; set; }

    // Economy
    public int BuyPrice  { get; set; }
    public int SellPrice { get; set; }

    // Installation state
    public bool   IsInstalled       { get; set; } = false;
    public int    InstalledOnShipId { get; set; } = 0;
    public string InstalledInSlot   { get; set; }

    // Condition (0-100); below 50 = degraded performance; 0 = destroyed
    public int Condition { get; set; } = 100;

    [Ignore] public bool IsDamaged   => Condition < 50;
    [Ignore] public bool IsDestroyed => Condition <= 0;
}
