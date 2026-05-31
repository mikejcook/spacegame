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

    // ── Sol cluster galaxy positions ────────────────────────────────────────
    // Placed in a spiral-arm region (~0.14 units from galactic centre).
    // The three systems form a small triangle so their labels don't overlap
    // at the default zoom level.
    //
    // Positions are chosen for visual clarity, not to-scale distances.
    // Real-world distances from Sol are stored as constants below and are
    // displayed verbatim in the galaxy info popup for these known systems;
    // all other systems use the normalised-coordinate algorithm.

    public const float SolGX          = 0.595f;
    public const float SolGY          = 0.448f;
    public const float AlphaGX        = 0.628f;   // closer cluster — Alpha Centauri (4.34 ly)
    public const float AlphaGY        = 0.428f;
    public const float BarnardsGX     = 0.658f;   // farther cluster — Barnard's Star (5.96 ly)
    public const float BarnardsGY     = 0.458f;

    // Display distances from Sol — shown verbatim in the galaxy info popup.
    // Expressed in ly (not normalised units) and scaled to produce "k" formatting.
    public const float AlphaDistanceLY    = 4_340f;   // → "4.34k ly away"
    public const float BarnardsDistanceLY = 5_960f;   // → "5.96k ly away"

    public static StarSystem GenerateSolSystem()
    {
        return new StarSystem
        {
            Name            = "Sol",
            StarType        = StarType.YellowDwarf,
            IsKnown         = true,
            GalaxyX         = SolGX,
            GalaxyY         = SolGY,
            IsExplored      = true,
            HasSpaceStation = true,
            DangerLevel     = 1,
            Seed            = 0,
            Description     = "Home system of humanity. Earth, Mars, and several orbital stations " +
                              "make Sol the hub of known space trade."
        };
    }

    public static StarSystem GenerateAlphaCentauri()
    {
        return new StarSystem
        {
            Name            = "Alpha Centauri",
            StarType        = StarType.YellowDwarf,
            IsKnown         = true,
            GalaxyX         = AlphaGX,
            GalaxyY         = AlphaGY,
            IsExplored      = false,
            HasSpaceStation = true,
            DangerLevel     = 2,
            Seed            = 1,
            Description     = "A triple-star system and humanity's nearest stellar neighbour. " +
                              "The two yellow dwarfs host a handful of colonised worlds and a busy trade station."
        };
    }

    public static StarSystem GenerateBarnardsStar()
    {
        return new StarSystem
        {
            Name            = "Barnard's Star",
            StarType        = StarType.RedDwarf,
            IsKnown         = true,
            GalaxyX         = BarnardsGX,
            GalaxyY         = BarnardsGY,
            IsExplored      = false,
            HasSpaceStation = false,
            DangerLevel     = 3,
            Seed            = 2,
            Description     = "A fast-moving red dwarf just six light years from Sol. " +
                              "Its rocky planets sit deep in a tidal-lock zone — cold, dark, and largely unexplored."
        };
    }

    // ---------------------------------------------------------------------------
    // CSV catalogue helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Parses a "Name,StarType" CSV (with a header row) into an array of tuples.
    /// StarType values "Yellow", "Red", "Blue" are mapped to the StarType enum.
    /// </summary>
    public static (string name, StarType starType)[] ParseSystemsCSV(string csvText)
    {
        if (string.IsNullOrEmpty(csvText)) return Array.Empty<(string, StarType)>();

        var result    = new List<(string, StarType)>();
        var lines     = csvText.Split('\n');
        bool skipNext = true;   // skip header row

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

            if (skipNext) { skipNext = false; continue; }  // header

            var parts = line.Split(',');
            if (parts.Length < 2) continue;

            result.Add((parts[0].Trim(), ParseStarType(parts[1].Trim())));
        }
        return result.ToArray();
    }

    private static StarType ParseStarType(string s) =>
        s.ToLowerInvariant() switch
        {
            "yellow" or "yellow dwarf" or "yellowdwarf" => StarType.YellowDwarf,
            "red"    or "red dwarf"    or "reddwarf"    => StarType.RedDwarf,
            "blue"   or "blue giant"   or "bluegiant"   => StarType.BlueGiant,
            _                                            => StarType.YellowDwarf
        };

    /// <summary>
    /// Builds a <see cref="StarSystem"/> from a CSV catalogue entry.
    /// The system is marked <see cref="StarSystem.IsKnown"/> = true so it
    /// appears on the galaxy map, but not yet explored.
    /// </summary>
    public static StarSystem GenerateExtraSystem(
        string   name,
        StarType starType,
        float    galaxyX,
        float    galaxyY,
        int      seed,
        int      saveGameId)
    {
        var rng         = new Random(seed);
        int dangerLevel = rng.Next(1, 6);
        bool hasStation = rng.Next(0, 10) < 2;   // 20 % chance

        return new StarSystem
        {
            Name            = name,
            StarType        = starType,
            IsKnown         = true,
            GalaxyX         = galaxyX,
            GalaxyY         = galaxyY,
            IsExplored      = false,
            HasSpaceStation = hasStation,
            DangerLevel     = dangerLevel,
            Seed            = seed,
            SaveGameId      = saveGameId,
            Description     = BuildSystemDescription(starType, dangerLevel, hasStation)
        };
    }

    /// <summary>
    /// Returns <paramref name="count"/> galaxy-map positions spread around the
    /// galactic disk using a golden-angle spiral.  Any candidate that falls within
    /// <paramref name="minSpacing"/> of an <paramref name="avoidPositions"/> entry
    /// (or of another already-placed position) is retried up to 40 times before
    /// accepting the best available fallback.
    /// </summary>
    public static (float gx, float gy)[] GenerateGalaxyPositions(
        int                  count,
        int                  seed,
        float                innerR         = 0.10f,
        float                outerR         = 0.38f,
        (float gx, float gy)[] avoidPositions = null,
        float                minSpacing     = 0.09f)
    {
        if (count <= 0) return Array.Empty<(float, float)>();

        var rng = new Random(seed ^ 0xABCDEF);

        // Golden angle in radians (~137.508°)
        const double GoldenAngle = 2.39996322972865332;
        double startAngle = rng.NextDouble() * Math.PI * 2;

        var result = new (float gx, float gy)[count];
        var placed = new List<(float gx, float gy)>(count);

        for (int i = 0; i < count; i++)
        {
            float bestGx = 0f, bestGy = 0f;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                float gx, gy;

                if (attempt == 0)
                {
                    // Primary: golden-angle spiral position
                    float t      = (i + 0.5f) / count;
                    float r      = innerR + t * (outerR - innerR);
                    float jitter = (float)(rng.NextDouble() - 0.5) * 0.035f;
                    r = Math.Clamp(r + jitter, innerR, outerR);
                    double angle = startAngle + i * GoldenAngle;
                    gx = Math.Clamp(0.5f + r * (float)Math.Cos(angle), 0.05f, 0.95f);
                    gy = Math.Clamp(0.5f + r * (float)Math.Sin(angle), 0.05f, 0.95f);
                }
                else
                {
                    // Fallback: random position anywhere in the ring
                    float r      = innerR + (float)rng.NextDouble() * (outerR - innerR);
                    double angle = rng.NextDouble() * Math.PI * 2;
                    gx = Math.Clamp(0.5f + r * (float)Math.Cos(angle), 0.05f, 0.95f);
                    gy = Math.Clamp(0.5f + r * (float)Math.Sin(angle), 0.05f, 0.95f);
                }

                if (!TooClose(gx, gy, avoidPositions, minSpacing) &&
                    !TooClose(gx, gy, placed,          minSpacing))
                {
                    bestGx = gx;
                    bestGy = gy;
                    goto placed;
                }

                // Keep the first attempt as the last-resort fallback
                if (attempt == 0) { bestGx = gx; bestGy = gy; }
            }

            placed:
            result[i] = (bestGx, bestGy);
            placed.Add((bestGx, bestGy));
        }
        return result;
    }

    private static bool TooClose(
        float gx, float gy,
        IEnumerable<(float gx, float gy)> others,
        float minDist)
    {
        if (others == null) return false;
        float d2 = minDist * minDist;
        foreach (var o in others)
        {
            float dx = gx - o.gx;
            float dy = gy - o.gy;
            if (dx * dx + dy * dy < d2) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------------------
    // Procedural system generation
    // ---------------------------------------------------------------------------

    /// <summary>Generate a fully random star system from a seed.</summary>
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

        int  dangerLevel = rng.Next(1, 6);
        bool hasStation  = isKnown || rng.Next(0, 10) < 2;

        return new StarSystem
        {
            Name            = name,
            StarType        = starType,
            IsKnown         = isKnown,
            GalaxyX         = galaxyX,
            GalaxyY         = galaxyY,
            IsExplored      = false,
            HasSpaceStation = hasStation,
            DangerLevel     = dangerLevel,
            Seed            = seed,
            Description     = BuildSystemDescription(starType, dangerLevel, hasStation)
        };
    }

    // ---------------------------------------------------------------------------
    // Sol POI generation — hand-crafted
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates Sol's eight planets plus Earth Station.
    /// Orbital radii increase from Mercury (innermost) to Neptune (outermost).
    /// Angles are seeded so each session uses the same "random" placement.
    /// Call after sol has been inserted into the database (needs sol.Id).
    /// </summary>
    public static List<PointOfInterest> GenerateSolPOIs(StarSystem sol, int saveGameId)
    {
        var rng  = new Random(sol.Seed + 9999);
        var pois = new List<PointOfInterest>();

        // ── Planets in orbital order ───────────────────────────────────────
        // Radii are in normalised map space (0-1), star at (0.5, 0.5).
        // Each planet gets a fully random angle from the seeded rng.

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Mercury", type: PlanetType.Barren, variant: 1, orbitalRadius: 0.07f,
            hasAtmosphere: false, isHabitable: false,
            desc: "A scorched, airless rock baked by the sun. Rich in heavy metals."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Venus", type: PlanetType.Cloudy, variant: 1, orbitalRadius: 0.12f,
            hasAtmosphere: true, isHabitable: false,
            desc: "A hellish world shrouded in thick, toxic clouds. Surface pressure crushes unshielded hulls."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Earth", type: PlanetType.Terrestrial, variant: 1, orbitalRadius: 0.17f,
            hasAtmosphere: true, isHabitable: true,
            desc: "Humanity's birthplace. Still the most populated world in known space, and the seat of the Colonial Authority."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Mars", type: PlanetType.Arid, variant: 1, orbitalRadius: 0.22f,
            hasAtmosphere: true, isHabitable: false,
            desc: "The first world humanity terraformed. Dome cities dot the rust-red surface; full breathable atmosphere is still a century away."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Jupiter", type: PlanetType.GaseousOrange, variant: 1, orbitalRadius: 0.30f,
            hasAtmosphere: true, isHabitable: false,
            desc: "The system's gas colossus. Hydrogen-3 mining platforms skim its upper atmosphere; its moons host the largest orbital shipyards in Sol."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Saturn", type: PlanetType.GaseousYellow, variant: 1, orbitalRadius: 0.36f,
            hasAtmosphere: true, isHabitable: false,
            desc: "Its iconic rings make it the most recognisable sight in Sol. Ring-mining consortia operate here under heavy Authority licence."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Uranus", type: PlanetType.Icy, variant: 1, orbitalRadius: 0.41f,
            hasAtmosphere: true, isHabitable: false,
            desc: "A frigid ice giant tilted almost on its side. Remote, inhospitable, but a useful waypoint for deep-system runs."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Neptune", type: PlanetType.Ocean, variant: 1, orbitalRadius: 0.46f,
            hasAtmosphere: true, isHabitable: false,
            desc: "The outermost major planet. Deep, swirling cobalt storms rage across its surface. Few ships venture this far without good reason."));

        // ── Earth Station ──────────────────────────────────────────────────
        // Placed near Earth's orbital radius at a fixed angle offset
        float earthAngle = (float)(rng.NextDouble() * Math.PI * 2) + 0.6f;
        pois.Add(new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = sol.Id,
            Name         = "Earth Station",
            POIType      = Constants.POI.Types.SpaceStation,
            SystemX      = Math.Clamp(0.5f + 0.17f * (float)Math.Cos(earthAngle), 0.05f, 0.95f),
            SystemY      = Math.Clamp(0.5f + 0.17f * (float)Math.Sin(earthAngle), 0.05f, 0.95f),
            DangerLevel  = 0,
            IsBoardable  = true,
            Description  = "Humanity's oldest space station — still the busiest port in known space. " +
                           "Trade, repairs, crew recruitment, and rumours in equal measure."
        });

        return pois;
    }

    private static PointOfInterest MakeSolPlanet(
        StarSystem sol, int saveGameId, Random rng,
        string name, PlanetType type, int variant, float orbitalRadius,
        bool hasAtmosphere, bool isHabitable, string desc)
    {
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float x = Math.Clamp(0.5f + orbitalRadius * (float)Math.Cos(angle), 0.05f, 0.95f);
        float y = Math.Clamp(0.5f + orbitalRadius * (float)Math.Sin(angle), 0.05f, 0.95f);

        return new PointOfInterest
        {
            SaveGameId    = saveGameId,
            StarSystemId  = sol.Id,
            Name          = name,
            POIType       = Constants.POI.Types.Planet,
            PlanetType    = type,
            PlanetVariant = variant,
            HasAtmosphere = hasAtmosphere,
            IsHabitable   = isHabitable,
            SystemX       = x,
            SystemY       = y,
            DangerLevel   = 1,
            Description   = desc,
            Resources     = GeneratePlanetResources(type, rng)
        };
    }

    // ---------------------------------------------------------------------------
    // General POI generation (non-Sol procedural systems)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generate all POIs for a procedurally generated star system.
    /// Call after the StarSystem has been inserted into the DB (needs system.Id).
    /// </summary>
    public static List<PointOfInterest> GeneratePOIsForSystem(StarSystem system, int saveGameId)
    {
        var rng  = new Random(system.Seed + 1000);
        var pois = new List<PointOfInterest>();

        // Planets (1-6)
        int planetCount = rng.Next(1, 7);
        for (int i = 0; i < planetCount; i++)
            pois.Add(GeneratePlanet(system, saveGameId, i, planetCount, rng));

        // Asteroid field (50% chance)
        if (rng.Next(0, 10) < 5)
            pois.Add(GenerateAsteroidField(system, saveGameId, rng));

        // Derelict (30% chance)
        if (rng.Next(0, 10) < 3)
            pois.Add(GenerateDerelict(system, saveGameId, rng));

        // Anomaly (10% chance)
        if (rng.Next(0, 10) < 1)
            pois.Add(GenerateAnomaly(system, saveGameId, rng));

        // Space station
        if (system.HasSpaceStation)
            pois.Add(GenerateSpaceStation(system, saveGameId, rng));

        return pois;
    }

    // ---------------------------------------------------------------------------
    // Individual POI generators
    // ---------------------------------------------------------------------------

    private static PointOfInterest GeneratePlanet(
        StarSystem system, int saveGameId, int index, int total, Random rng)
    {
        // Pick a random planet type from all available types
        var allTypes  = (PlanetType[])Enum.GetValues(typeof(PlanetType));
        var type      = allTypes[rng.Next(allTypes.Length)];
        int variant   = rng.Next(1, 6);
        bool hasAtmo  = !IsAirlessType(type) && rng.Next(0, 10) < 7;
        bool habitable = hasAtmo && type == PlanetType.Terrestrial && rng.Next(0, 10) < 4;

        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float radius = 0.08f + (index / (float)Math.Max(total, 1)) * 0.35f;

        return new PointOfInterest
        {
            SaveGameId    = saveGameId,
            StarSystemId  = system.Id,
            Name          = $"{system.Name} {RomanNumeral(index + 1)}",
            POIType       = Constants.POI.Types.Planet,
            PlanetType    = type,
            PlanetVariant = variant,
            HasAtmosphere = hasAtmo,
            IsHabitable   = habitable,
            SystemX       = Math.Clamp(0.5f + radius * (float)Math.Cos(angle), 0.05f, 0.95f),
            SystemY       = Math.Clamp(0.5f + radius * (float)Math.Sin(angle), 0.05f, 0.95f),
            DangerLevel   = Math.Clamp(system.DangerLevel + rng.Next(-1, 2), 1, 5),
            Description   = BuildPlanetDescription(type, habitable),
            Resources     = GeneratePlanetResources(type, rng)
        };
    }

    private static PointOfInterest GenerateAsteroidField(
        StarSystem system, int saveGameId, Random rng)
    {
        float angle  = (float)(rng.NextDouble() * Math.PI * 2);
        float radius = 0.25f + (float)rng.NextDouble() * 0.15f;
        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = $"{system.Name} Asteroid Belt",
            POIType      = Constants.POI.Types.AsteroidField,
            SystemX      = Math.Clamp(0.5f + radius * (float)Math.Cos(angle), 0.05f, 0.95f),
            SystemY      = Math.Clamp(0.5f + radius * (float)Math.Sin(angle), 0.05f, 0.95f),
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
        bool isShip  = rng.Next(0, 2) == 0;
        float angle  = (float)(rng.NextDouble() * Math.PI * 2);
        float radius = 0.20f + (float)rng.NextDouble() * 0.20f;
        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = isShip ? "Derelict Vessel" : "Derelict Station",
            POIType      = isShip ? Constants.POI.Types.DerelictShip : Constants.POI.Types.DerelictStation,
            SystemX      = Math.Clamp(0.5f + radius * (float)Math.Cos(angle), 0.05f, 0.95f),
            SystemY      = Math.Clamp(0.5f + radius * (float)Math.Sin(angle), 0.05f, 0.95f),
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
        string[] types = { "Spatial Rift", "Gravitational Lens", "Exotic Radiation Source", "Unknown Signal" };
        float angle    = (float)(rng.NextDouble() * Math.PI * 2);
        float radius   = 0.15f + (float)rng.NextDouble() * 0.30f;
        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = types[rng.Next(types.Length)],
            POIType      = Constants.POI.Types.Anomaly,
            SystemX      = Math.Clamp(0.5f + radius * (float)Math.Cos(angle), 0.05f, 0.95f),
            SystemY      = Math.Clamp(0.5f + radius * (float)Math.Sin(angle), 0.05f, 0.95f),
            DangerLevel  = Math.Clamp(system.DangerLevel + 2, 1, 5),
            Description  = "Sensors are going haywire. Approach with extreme caution."
        };
    }

    private static PointOfInterest GenerateSpaceStation(
        StarSystem system, int saveGameId, Random rng)
    {
        float angle  = (float)(rng.NextDouble() * Math.PI * 2);
        float radius = 0.10f + (float)rng.NextDouble() * 0.15f;
        return new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = $"{system.Name} Station",
            POIType      = Constants.POI.Types.SpaceStation,
            SystemX      = Math.Clamp(0.5f + radius * (float)Math.Cos(angle), 0.05f, 0.95f),
            SystemY      = Math.Clamp(0.5f + radius * (float)Math.Sin(angle), 0.05f, 0.95f),
            DangerLevel  = 0,
            IsBoardable  = true,
            Description  = "A bustling waystation offering trade, repairs, crew recruitment, and a bar with surprisingly decent food."
        };
    }

    // ---------------------------------------------------------------------------
    // Resource tables
    // ---------------------------------------------------------------------------

    private static Dictionary<string, int> GeneratePlanetResources(PlanetType type, Random rng)
    {
        if (type.IsGaseous())
            return new Dictionary<string, int>
            {
                ["Hydrogen"] = rng.Next(100, 500),
                ["Helium3"]  = rng.Next(50, 200)
            };

        return type switch
        {
            PlanetType.Terrestrial or PlanetType.Lush or PlanetType.Tropical or PlanetType.Oasis
                => new Dictionary<string, int> { ["Organics"] = rng.Next(20, 100), ["Iron"] = rng.Next(10, 50) },

            PlanetType.Ocean or PlanetType.Aquamarine
                => new Dictionary<string, int> { ["Water"] = rng.Next(200, 600), ["Organics"] = rng.Next(50, 200) },

            PlanetType.Frozen or PlanetType.Glacial or PlanetType.Icy or PlanetType.Snowy
                => new Dictionary<string, int> { ["Water"] = rng.Next(100, 300), ["Hydrogen"] = rng.Next(20, 80) },

            PlanetType.Magma
                => new Dictionary<string, int> { ["Iron"] = rng.Next(100, 300), ["Titanium"] = rng.Next(30, 100) },

            PlanetType.Arid or PlanetType.Dry or PlanetType.Muddy
                => new Dictionary<string, int> { ["Silicon"] = rng.Next(50, 150), ["Iron"] = rng.Next(30, 100) },

            _ => new Dictionary<string, int>()  // Airless, Barren, Cratered, Lunar, Rocky
        };
    }

    // ---------------------------------------------------------------------------
    // Description builders
    // ---------------------------------------------------------------------------

    private static string BuildSystemDescription(StarType starType, int dangerLevel, bool hasStation)
    {
        string star = starType switch
        {
            StarType.YellowDwarf => "A yellow dwarf star",
            StarType.RedDwarf    => "A dim red dwarf",
            StarType.BlueGiant   => "A massive blue giant",
            _                    => "An unusual stellar object"
        };
        string danger  = dangerLevel switch
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

    private static string BuildPlanetDescription(PlanetType type, bool habitable)
    {
        string suffix = habitable ? " Signs of life detected." : "";
        return type switch
        {
            PlanetType.Terrestrial => $"A rocky world with varied terrain.{suffix}",
            PlanetType.Lush        => $"Verdant and alive, draped in dense vegetation.{suffix}",
            PlanetType.Tropical    => $"Hot and humid, teeming with biology.{suffix}",
            PlanetType.Oasis       => $"Arid wastelands broken by fertile green zones.{suffix}",
            PlanetType.Ocean       => $"Almost entirely covered by deep, dark ocean.{suffix}",
            PlanetType.Aquamarine  => "A shallow-seas world, mineral-rich and strikingly blue.",
            PlanetType.Arid        => "A scorched, arid planet baking under its star.",
            PlanetType.Dry         => "Cracked earth and dust storms. Little moisture remains.",
            PlanetType.Muddy       => "Thick mud and shallow bogs stretch as far as sensors can tell.",
            PlanetType.Cloudy      => "Smothered in dense cloud cover. Surface conditions unknown.",
            PlanetType.Frozen      => "A frozen world locked in perpetual winter.",
            PlanetType.Glacial     => "Vast glacial sheets grind across an ancient surface.",
            PlanetType.Icy         => "Perpetual ice and sub-zero temperatures. Hostile to most life.",
            PlanetType.Snowy       => "Blanketed in snow. Cold, quiet, and largely unexplored.",
            PlanetType.Barren      => "A barren, airless rock — unremarkable, but quiet.",
            PlanetType.Airless     => "No atmosphere. Ancient impact craters scar the surface.",
            PlanetType.Lunar       => "Grey and pocked with craters, resembling an ancient moon.",
            PlanetType.Cratered    => "Heavily scarred by ancient impacts. Something hit this world hard.",
            PlanetType.Rocky       => "Rugged terrain, sheer cliffs, and little else.",
            PlanetType.Magma       => "Tectonically violent. Rich in heavy metals, dangerous to approach.",
            _ when type.IsGaseous() => "A vast gas giant — no surface to land on, but rich in atmospheric gases.",
            _                       => "An unremarkable world."
        };
    }

    // ---------------------------------------------------------------------------
    // Utilities
    // ---------------------------------------------------------------------------

    private static bool IsAirlessType(PlanetType type) =>
        type is PlanetType.Airless or PlanetType.Barren or PlanetType.Lunar or PlanetType.Cratered;

    private static string RomanNumeral(int n) => n switch
    {
        1 => "I",  2 => "II",  3 => "III", 4 => "IV",  5 => "V",
        6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X",
        _ => n.ToString()
    };
}
