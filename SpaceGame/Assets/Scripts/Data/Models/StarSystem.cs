using System.Collections.Generic;
using SQLite4Unity3d;

// ---------------------------------------------------------------------------
// Enums
// ---------------------------------------------------------------------------

/// <summary>
/// The three star types currently in use. Sprites come from PlanetLibrary's
/// StarSprites — one set of five per type.
///
/// Do NOT reorder or renumber — these values are stored as integers in the
/// database. Add new types at the end only.
/// </summary>
public enum StarType
{
    YellowDwarf = 0,   // Sun_Yellow_XX sprites
    RedDwarf    = 1,   // Sun_Red_XX sprites
    BlueGiant   = 2,   // Sun_Blue_XX sprites
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
    public bool IsKnown         { get; set; } = false;
    public bool IsExplored      { get; set; } = false;
    public bool HasSpaceStation { get; set; } = false;

    // Danger level 1-5 (affects encounter DCs and enemy strength)
    public int DangerLevel { get; set; } = 1;

    // Minimum FTL drive tier required to reach this system (1-6, maps to EquipmentTier.MkI-MkVI).
    // Sol = 1; each outer cluster increments by one tier.
    public int FtlTierRequired { get; set; } = 1;

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

    public int    SaveGameId   { get; set; }
    public int    StarSystemId { get; set; }
    public string Name         { get; set; }
    public string POIType      { get; set; } // see Constants.POI.Types

    // Position within the system view (0-1 normalized, mapped to screen space)
    public float SystemX { get; set; }
    public float SystemY { get; set; }

    // Planet visual — only meaningful when POIType == Constants.POI.Types.Planet
    public PlanetType PlanetType    { get; set; }   // stored as int; drives sprite selection
    public int        PlanetVariant { get; set; } = 1; // 1-5 — which art variant to display

    public bool HasAtmosphere { get; set; }
    public bool IsHabitable   { get; set; }

    // Exploration state
    public bool IsDiscovered { get; set; } = true;
    public bool IsExplored   { get; set; } = false;

    // Derelict / boarding
    public bool IsBoardable { get; set; } = false;
    public bool IsLooted    { get; set; } = false;

    /// <summary>
    /// The DaysPassed value when this station's recruit pool was last generated.
    /// -1 means recruits have never been generated.
    /// Used to determine when to refresh the pool (see Constants.Recruitment.RecruitRefreshDays).
    /// Only meaningful for POIType == Constants.POI.Types.SpaceStation.
    /// </summary>
    public int RecruitRefreshDay { get; set; } = -1;

    // Danger level (can exceed parent system danger for derelicts, anomalies)
    public int DangerLevel { get; set; } = 1;

    // Collectable resources — only meaningful when POIType == Constants.POI.Types.Planet.
    // Gas planets favour He-3; rocky planets favour Iridium and Salvage.
    // At least one planet per system is guaranteed to carry each resource type.
    // Amount fields are 0 when the corresponding Has* flag is false.
    public bool HasHelium3 { get; set; }
    public bool HasIridium { get; set; }
    public bool HasSalvage { get; set; }
    public int  Helium3Amount { get; set; }
    public int  IridiumAmount { get; set; }
    public int  SalvageAmount { get; set; }

    public string Description { get; set; }
}
