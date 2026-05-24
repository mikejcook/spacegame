using System;
using System.Collections.Generic;

/// <summary>
/// Procedurally generates star systems and their points of interest.
/// All generation is seeded for reproducibility — the same seed always
/// produces the same system, allowing saves to reference IDs rather than
/// re-generating content.
/// </summary>
public static class StarSystemGenerator
{
    // ---------------------------------------------------------------------------
    // Known / hand-crafted systems
    // ---------------------------------------------------------------------------

    public static StarSystem GenerateSolSystem()
    {
        return new StarSystem
        {
            Name           = "Sol",
            StarType       = StarType.YellowDwarf,
            IsKnown        = true,
            GalaxyX        = 0.5f,
            GalaxyY        = 0.5f,
            IsExplored     = true,
            HasSpaceStation = true,
            DangerLevel    = 1,
            Seed           = 0,
            Description    = "Home system of humanity. Earth, Mars, and several orbital stations " +
                             "make Sol the hub of known space trade."
        };
    }

    public static StarSystem GenerateAlphaCentauri()
    {
        return new StarSystem
        {
            Name           = "Alpha Centauri",
            StarType       = StarType.BinarySystem,
            IsKnown        = true,
            GalaxyX        = 0.54f,
            GalaxyY        = 0.49f,
            IsExplored     = false,
            HasSpaceStation = true,
            DangerLevel    = 2,
            Seed           = 1,
            Description    = "Nearest star system to Sol. A binary pair hosts a handful of " +
                             "colonised worlds and a busy trade station."
        };
    }

    public static StarSystem GenerateProximaCentauri()
    {
        return new StarSystem
        {
            Name           = "Proxima Centauri",
            StarType       = StarType.RedDwarf,
            IsKnown        = true,
            GalaxyX        = 0.53f,
            GalaxyY        = 0.48f,
            IsExplored     = false,
            HasSpaceStation = false,
            DangerLevel    = 3,
            Seed           = 2,
            Description    = "A red dwarf with a tidally-locked habitable world. Rumours of " +
                             "pre-human ruins on the surface draw explorers and treasure-hunters alike."
        };
    }

    // ---------------------------------------------------------------------------
    // Procedural system generation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generate a fully random star system from a seed.
    /// </summary>
    public static StarSystem GenerateSystem(
        string name,
        float galaxyX,
        float galaxyY,
        int seed,
        bool isKnown = false)
    {
        var rng = new Random(seed);

        var starTypes = (StarType[])Enum.GetValues(typeof(StarType));
        var starType  = starTypes[rng.Next(starTypes.Length)];

        int  dangerLevel  = rng.Next(1, 6);
        bool hasStation   = isKnown || rng.Next(0, 10) < 2;

        return new StarSystem
        {
            Name           = name,
            StarType       = starType,
            IsKnown        = isKnown,
            GalaxyX        = galaxyX,
            GalaxyY        = galaxyY,
            IsExplored     = false,
            HasSpaceStation = hasStation,
            DangerLevel    = dangerLevel,
            Seed           = seed,
            Description    = BuildSystemDescription(starType, dangerLevel, hasStation)
        };
    }

    // ---------------------------------------------------------------------------
    // POI generation for a system
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generate all POIs for a star system.
    /// Call after the StarSystem has been inserted into the DB (needs system.Id).
    /// </summary>
    public static List<PointOfInterest> GeneratePOIsForSystem(StarSystem system, int saveGameId)
    {
        var rng  = new Random(system.Seed + 1000);
        var pois = new List<PointOfInterest>();

        // Planets (1-6)
        int planetCount = rng.Next(1, 7);
        for (int i = 0; i < planetCount; i++)
            pois.Add(GeneratePlanet(system, saveGameId, i, rng));

        // Asteroid field (50% chance)
        if (rng.Next(0, 10) < 5)
            pois.Add(GenerateAsteroidField(system, saveGameId, rng));

        // Derelict (30% chance)
        if (rng.Next(0, 10) < 3)
            pois.Add(GenerateDerelict(system, saveGameId, rng));

        // Anomaly (10% chance)
        if (rng.Next(0, 10) < 1)
            pois.Add(GenerateAnomaly(system, saveGameId, rng));

        // Space station (if the system has one)
        if (system.HasSpaceStation)
            pois.Add(GenerateSpaceStation(system, saveGameId, rng));

        return pois;
    }

    // ---------------------------------------------------------------------------
    // Individual POI generators
    // ---------------------------------------------------------------------------

    private static PointOfInterest GeneratePlanet(
        StarSystem system, int saveGameId, int index, Random rng)
    {
        string[] types       = { "Terrestrial", "GasGiant", "IceWorld", "DesertWorld", "OceanWorld", "VolcanicWorld", "Barren" };
        string   planetType  = types[rng.Next(types.Length)];
        bool     hasAtmo     = planetType != "Barren" && rng.Next(0, 10) < 7;
        bool     habitable   = hasAtmo && planetType == "Terrestrial" && rng.Next(0, 10) < 4;

        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = $"{system.Name} {RomanNumeral(index + 1)}",
            POIType      = Constants.POI.Types.Planet,
            PlanetType   = planetType,
            HasAtmosphere = hasAtmo,
            IsHabitable  = habitable,
            SystemX      = Spread(rng, index, 6),
            SystemY      = (float)(0.3 + rng.NextDouble() * 0.4),
            DangerLevel  = Math.Clamp(system.DangerLevel + rng.Next(-1, 2), 1, 5),
            IsBoardable  = false,
            Description  = BuildPlanetDescription(planetType, habitable),
            Resources    = GeneratePlanetResources(planetType, rng)
        };
    }

    private static PointOfInterest GenerateAsteroidField(
        StarSystem system, int saveGameId, Random rng)
    {
        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = $"{system.Name} Asteroid Belt",
            POIType      = Constants.POI.Types.AsteroidField,
            SystemX      = (float)rng.NextDouble(),
            SystemY      = (float)rng.NextDouble(),
            DangerLevel  = system.DangerLevel,
            Description  = "A dense field of rocky debris — rich in minerals but hazardous to navigate.",
            Resources    = new Dictionary<string, int>
            {
                ["Iron"]     = rng.Next(50, 200),
                ["Titanium"] = rng.Next(10, 80),
                ["Crystals"] = rng.Next(0,  30)
            }
        };
    }

    private static PointOfInterest GenerateDerelict(
        StarSystem system, int saveGameId, Random rng)
    {
        bool isShip = rng.Next(0, 2) == 0;
        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = isShip ? "Derelict Vessel" : "Derelict Station",
            POIType      = isShip ? Constants.POI.Types.DerelictShip : Constants.POI.Types.DerelictStation,
            SystemX      = (float)rng.NextDouble(),
            SystemY      = (float)rng.NextDouble(),
            DangerLevel  = Math.Clamp(system.DangerLevel + 1, 1, 5),
            IsBoardable  = true,
            IsLooted     = false,
            Description  = isShip
                ? "A drifting wreck. Her markings are faded. No life signs detected — yet."
                : "An abandoned station, dark and silent. Could be salvage, could be a trap."
        };
    }

    private static PointOfInterest GenerateAnomaly(
        StarSystem system, int saveGameId, Random rng)
    {
        string[] anomalyTypes = { "Spatial Rift", "Gravitational Lens", "Exotic Radiation Source", "Unknown Signal" };
        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = anomalyTypes[rng.Next(anomalyTypes.Length)],
            POIType      = Constants.POI.Types.Anomaly,
            SystemX      = (float)rng.NextDouble(),
            SystemY      = (float)rng.NextDouble(),
            DangerLevel  = Math.Clamp(system.DangerLevel + 2, 1, 5),
            Description  = "Sensors are going haywire. Approach with extreme caution."
        };
    }

    private static PointOfInterest GenerateSpaceStation(
        StarSystem system, int saveGameId, Random rng)
    {
        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = $"{system.Name} Station",
            POIType      = Constants.POI.Types.SpaceStation,
            SystemX      = 0.5f + (float)(rng.NextDouble() * 0.1 - 0.05f),
            SystemY      = 0.5f + (float)(rng.NextDouble() * 0.1 - 0.05f),
            DangerLevel  = 0,
            IsBoardable  = true,
            Description  = "A bustling waystation offering trade, repairs, crew recruitment, and a bar with surprisingly decent food."
        };
    }

    // ---------------------------------------------------------------------------
    // Resource tables
    // ---------------------------------------------------------------------------

    private static Dictionary<string, int> GeneratePlanetResources(string planetType, Random rng)
    {
        return planetType switch
        {
            "Terrestrial"  => new() { ["Organics"] = rng.Next(20, 100), ["Iron"] = rng.Next(10, 50) },
            "GasGiant"     => new() { ["Hydrogen"] = rng.Next(100, 500), ["Helium3"] = rng.Next(50, 200) },
            "IceWorld"     => new() { ["Water"] = rng.Next(100, 300), ["Hydrogen"] = rng.Next(20, 80) },
            "DesertWorld"  => new() { ["Silicon"] = rng.Next(50, 150), ["Iron"] = rng.Next(30, 100) },
            "OceanWorld"   => new() { ["Water"] = rng.Next(200, 600), ["Organics"] = rng.Next(50, 200) },
            "VolcanicWorld" => new() { ["Iron"] = rng.Next(100, 300), ["Titanium"] = rng.Next(30, 100) },
            _              => new()
        };
    }

    // ---------------------------------------------------------------------------
    // Description builders
    // ---------------------------------------------------------------------------

    private static string BuildSystemDescription(StarType starType, int dangerLevel, bool hasStation)
    {
        string star = starType switch
        {
            StarType.YellowDwarf  => "A yellow dwarf star",
            StarType.RedDwarf     => "A dim red dwarf",
            StarType.BlueGiant    => "A massive blue giant",
            StarType.WhiteDwarf   => "The cooling remnant of a dead star",
            StarType.NeutronStar  => "A dense, rapidly-spinning neutron star",
            StarType.BinarySystem => "A binary star pair",
            _                     => "An unusual stellar object"
        };

        string danger = dangerLevel switch
        {
            1 => "relatively safe",
            2 => "mildly hazardous",
            3 => "moderately dangerous",
            4 => "quite dangerous",
            _ => "extremely perilous"
        };

        string station = hasStation ? " A space station orbits here." : "";
        return $"{star}, {danger} for travellers.{station}";
    }

    private static string BuildPlanetDescription(string type, bool habitable)
    {
        string suffix = habitable ? " Life has taken hold here." : "";
        return type switch
        {
            "Terrestrial"   => $"A rocky world with varied terrain.{suffix}",
            "GasGiant"      => "A vast gas giant. No surface to land on, but rich in atmospheric gases.",
            "IceWorld"      => "A frozen world locked in perpetual winter.",
            "DesertWorld"   => "A scorched, arid planet baking under its star.",
            "OceanWorld"    => $"Covered almost entirely by deep oceans.{suffix}",
            "VolcanicWorld" => "Tectonically violent. Rich in heavy metals, dangerous to approach.",
            _               => "A barren, airless rock — unremarkable, but quiet."
        };
    }

    // ---------------------------------------------------------------------------
    // Utilities
    // ---------------------------------------------------------------------------

    /// <summary>Spread POIs evenly across the X axis with some random jitter.</summary>
    private static float Spread(Random rng, int index, int total)
    {
        float step   = 1.0f / (total + 1);
        float jitter = (float)(rng.NextDouble() * step * 0.4 - step * 0.2);
        return Math.Clamp(step * (index + 1) + jitter, 0.05f, 0.95f);
    }

    private static string RomanNumeral(int n) => n switch
    {
        1 => "I",  2 => "II", 3 => "III", 4 => "IV",  5 => "V",
        6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X",
        _ => n.ToString()
    };
}
