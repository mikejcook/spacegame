using System.Collections.Generic;
using SQLite;

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------

public enum StarType
{
    YellowDwarf,
    RedDwarf,
    BlueGiant,
    WhiteDwarf,
    NeutronStar,
    BinarySystem
}

// ---------------------------------------------------------------------------
// Star system model
// ---------------------------------------------------------------------------

/// <summary>
/// Represents one star system on the galaxy map.
/// Known systems (Sol, Alpha Centauri, etc.) are pre-seeded;
/// unexplored systems are procedurally generated on first entry.
/// </summary>
[Table("StarSystems")]
public class StarSystem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int    SaveGameId { get; set; }
    public string Name       { get; set; }
    public StarType StarType { get; set; }

    // Galaxy map position (normalized coordinates)
    public float GalaxyX { get; set; }
    public float GalaxyY { get; set; }

    // Flags
    public bool IsKnown       { get; set; } = false; // pre-seeded vs procedural
    public bool IsExplored    { get; set; } = false; // player has visited
    public bool HasSpaceStation { get; set; } = false;

    // Danger level 1-5 (affects encounter DCs and enemy strength)
    public int DangerLevel { get; set; } = 1;

    // Reproducible generation
    public int Seed { get; set; }

    public string Description { get; set; }
}

// ---------------------------------------------------------------------------
// Point of Interest model
// ---------------------------------------------------------------------------

/// <summary>
/// A navigable location within a star system: planet, station,
/// asteroid field, derelict, etc.
/// </summary>
[Table("PointsOfInterest")]
public class PointOfInterest
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int    SaveGameId  { get; set; }
    public int    StarSystemId { get; set; }
    public string Name        { get; set; }
    public string POIType     { get; set; } // see Constants.POI.Types

    // Position within the system view (0-1 normalized, mapped to screen space)
    public float SystemX { get; set; }
    public float SystemY { get; set; }

    // Planet details
    public string PlanetType    { get; set; }
    public bool   HasAtmosphere { get; set; }
    public bool   IsHabitable   { get; set; }

    // Exploration state
    public bool IsDiscovered { get; set; } = true;  // visible on sensors
    public bool IsExplored   { get; set; } = false; // player has visited

    // Derelict/boarding
    public bool IsBoardable { get; set; } = false;
    public bool IsLooted    { get; set; } = false;

    // Danger level (can exceed parent system danger for derelicts, anomalies)
    public int DangerLevel { get; set; } = 1;

    public string Description { get; set; }

    // Resources available (JSON: resource name -> amount remaining)
    [Column("ResourcesJson")]
    public string ResourcesJson { get; set; } = "{}";

    [Ignore]
    public Dictionary<string, int> Resources
    {
        get => Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(ResourcesJson)
               ?? new Dictionary<string, int>();
        set => ResourcesJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }
}
