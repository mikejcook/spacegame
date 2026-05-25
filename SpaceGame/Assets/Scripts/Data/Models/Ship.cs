using System.Collections.Generic;
using SQLite;

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------

public enum ShipClass
{
    Freighter,
    Corvette,
    Frigate,
    Cruiser
}

// ---------------------------------------------------------------------------
// Ship model
// ---------------------------------------------------------------------------

/// <summary>
/// Represents the player's ship. Equipment slots and cargo are stored
/// as JSON dictionaries inside single DB columns.
/// </summary>
[Table("Ships")]
public class Ship
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int       SaveGameId { get; set; }
    public string    Name       { get; set; }
    public ShipClass ShipClass  { get; set; }

    // Hull & shields
    public int MaxHullPoints   { get; set; }
    public int HullPoints      { get; set; }
    public int MaxShieldPoints { get; set; }
    public int ShieldPoints    { get; set; }

    // Capacity
    public int CargoCapacity  { get; set; }
    public int UsedCargoSpace { get; set; }
    public int MaxCrew        { get; set; }
    public int MaxPassengers  { get; set; }

    // Navigation
    public float FTLSpeed { get; set; } = 1.0f; // multiplied by engine rating

    // Position
    public int CurrentSystemId { get; set; }

    // ---------------------------------------------------------------------------
    // JSON-backed collections
    // ---------------------------------------------------------------------------

    /// <summary>Equipment slot name -> EquipmentItem ID (0 = empty)</summary>
    [Column("EquipmentSlotsJson")]
    public string EquipmentSlotsJson { get; set; } = "{}";

    /// <summary>EquipmentItem ID -> quantity (cargo hold contents)</summary>
    [Column("CargoJson")]
    public string CargoJson { get; set; } = "{}";

    [Ignore]
    public Dictionary<string, int> EquipmentSlots
    {
        get => Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(EquipmentSlotsJson)
               ?? new Dictionary<string, int>();
        set => EquipmentSlotsJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }

    [Ignore]
    public Dictionary<int, int> Cargo
    {
        get => Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<int, int>>(CargoJson)
               ?? new Dictionary<int, int>();
        set => CargoJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }

    // ---------------------------------------------------------------------------
    // Base stats by ship class (equipment adds on top of these)
    // ---------------------------------------------------------------------------

    [Ignore] public int BaseAttackBonus  => ClassStat("attack");
    [Ignore] public int BaseDefenseBonus => ClassStat("defense");
    [Ignore] public int BaseSensorRange  => ClassStat("sensors");

    private int ClassStat(string stat) => (ShipClass, stat) switch
    {
        (ShipClass.Freighter, "attack")  => 0,
        (ShipClass.Freighter, "defense") => 1,
        (ShipClass.Freighter, "sensors") => 1,
        (ShipClass.Corvette,       "attack")  => 2,
        (ShipClass.Corvette,       "defense") => 2,
        (ShipClass.Corvette,       "sensors") => 2,
        (ShipClass.Frigate,        "attack")  => 4,
        (ShipClass.Frigate,        "defense") => 4,
        (ShipClass.Frigate,        "sensors") => 3,
        (ShipClass.Cruiser,        "attack")  => 6,
        (ShipClass.Cruiser,        "defense") => 6,
        (ShipClass.Cruiser,        "sensors") => 4,
        _ => 0
    };

    // ---------------------------------------------------------------------------
    // Equipment helpers
    // ---------------------------------------------------------------------------

    public void InstallEquipment(string slotName, int equipmentId)
    {
        var slots = EquipmentSlots;
        slots[slotName] = equipmentId;
        EquipmentSlots = slots;
    }

    public void RemoveEquipment(string slotName)
    {
        var slots = EquipmentSlots;
        slots[slotName] = 0;
        EquipmentSlots = slots;
    }

    public bool HasEquipmentInSlot(string slotName)
    {
        var slots = EquipmentSlots;
        return slots.TryGetValue(slotName, out var id) && id > 0;
    }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    public static Ship CreateStartingShip(string name = "Horizon")
    {
        var ship = new Ship
        {
            Name           = name,
            ShipClass      = ShipClass.Freighter,
            MaxHullPoints  = 50,
            HullPoints     = 50,
            MaxShieldPoints = 20,
            ShieldPoints   = 20,
            CargoCapacity  = 20,
            MaxCrew        = 4,   // captain + 3 crew
            MaxPassengers  = 2,
            FTLSpeed       = 1.0f
        };

        // Pre-populate slot keys so the UI can render all slots even if empty
        ship.EquipmentSlots = new Dictionary<string, int>
        {
            [Constants.Ship.EquipmentSlots.Engine]      = 0,
            [Constants.Ship.EquipmentSlots.Shield]      = 0,
            [Constants.Ship.EquipmentSlots.Reactor]     = 0,
            [Constants.Ship.EquipmentSlots.Sensors]     = 0,
            [Constants.Ship.EquipmentSlots.WeaponPort1] = 0,
            [Constants.Ship.EquipmentSlots.WeaponPort2] = 0,
            [Constants.Ship.EquipmentSlots.CargoHold]   = 0,
        };

        return ship;
    }
}
