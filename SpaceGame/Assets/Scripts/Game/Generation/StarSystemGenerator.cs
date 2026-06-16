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
            FtlTierRequired = 1,   // nearest system — Mk I FTL sufficient
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
            FtlTierRequired = 1,   // still in the inner cluster — Mk I FTL sufficient
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
        int      saveGameId,
        int      ftlTierRequired = 1)
    {
        var rng         = new Random(seed);
        int dangerLevel = rng.Next(1, 6);
        bool hasStation = rng.Next(0, 10) < 2;   // 20 % chance

        return new StarSystem
        {
            Name             = name,
            StarType         = starType,
            IsKnown          = true,
            GalaxyX          = galaxyX,
            GalaxyY          = galaxyY,
            IsExplored       = false,
            HasSpaceStation  = hasStation,
            DangerLevel      = dangerLevel,
            Seed             = seed,
            SaveGameId       = saveGameId,
            FtlTierRequired  = ftlTierRequired,
            Description      = BuildSystemDescription(starType, dangerLevel, hasStation)
        };
    }

    /// <summary>
    /// Generates <paramref name="count"/> galaxy-map positions in a ring centred on a specific
    /// point (e.g. Sol's position) rather than the galaxy centre.  Used to produce FTL-tier
    /// clusters at increasing radial distance from Sol.
    ///
    /// Candidates that fall within <paramref name="minSpacing"/> of anything in
    /// <paramref name="avoidPositions"/> or already-placed positions are retried up to 40 times.
    /// </summary>
    // Galaxy image centre in normalised coordinates.
    // The bright galactic core extends roughly this far — any candidate inside
    // this radius is rejected so systems don't spawn in the unreadable glow.
    public  const float GalacticCoreGX     = 0.50f;
    public  const float GalacticCoreGY     = 0.50f;
    public  const float GalacticCoreRadius = 0.14f;

    public static (float gx, float gy)[] GenerateClusterPositions(
        float                centerGx,
        float                centerGy,
        float                innerR,
        float                outerR,
        int                  count,
        int                  seed,
        (float gx, float gy)[] avoidPositions = null,
        float                minSpacing        = 0.05f,
        bool                 avoidGalacticCore = true)
    {
        if (count <= 0) return Array.Empty<(float, float)>();

        var rng = new Random(seed ^ unchecked((int)0xBEEF1234));
        const double GoldenAngle = 2.39996322972865332;
        double startAngle = rng.NextDouble() * Math.PI * 2;

        var result = new (float gx, float gy)[count];
        var placed = new List<(float gx, float gy)>(count);

        for (int i = 0; i < count; i++)
        {
            float bestGx = 0f, bestGy = 0f;
            float bestMinDist = -1f;

            for (int attempt = 0; attempt < 60; attempt++)
            {
                float gx, gy;
                if (attempt == 0)
                {
                    float t      = (i + 0.5f) / count;
                    float r      = innerR + t * (outerR - innerR);
                    float jitter = (float)(rng.NextDouble() - 0.5) * 0.025f;
                    r = Math.Clamp(r + jitter, innerR, outerR);
                    double angle = startAngle + i * GoldenAngle;
                    gx = Math.Clamp(centerGx + r * (float)Math.Cos(angle), 0.05f, 0.95f);
                    gy = Math.Clamp(centerGy + r * (float)Math.Sin(angle), 0.05f, 0.95f);
                }
                else
                {
                    float r      = innerR + (float)rng.NextDouble() * (outerR - innerR);
                    double angle = rng.NextDouble() * Math.PI * 2;
                    gx = Math.Clamp(centerGx + r * (float)Math.Cos(angle), 0.05f, 0.95f);
                    gy = Math.Clamp(centerGy + r * (float)Math.Sin(angle), 0.05f, 0.95f);
                }

                // Reject candidates inside the bright galactic core
                if (avoidGalacticCore)
                {
                    float cdx = gx - GalacticCoreGX;
                    float cdy = gy - GalacticCoreGY;
                    if (cdx * cdx + cdy * cdy < GalacticCoreRadius * GalacticCoreRadius)
                        continue;
                }

                if (!TooClose(gx, gy, avoidPositions, minSpacing) &&
                    !TooClose(gx, gy, placed,          minSpacing))
                {
                    bestGx = gx;
                    bestGy = gy;
                    goto placed;
                }

                float minDist = MinDistTo(gx, gy, avoidPositions, placed);
                if (minDist > bestMinDist)
                {
                    bestMinDist = minDist;
                    bestGx      = gx;
                    bestGy      = gy;
                }
            }

            placed:
            result[i] = (bestGx, bestGy);
            placed.Add((bestGx, bestGy));
        }
        return result;
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
            float bestGx      = 0f, bestGy = 0f;
            float bestMinDist = -1f;   // tracks the "least bad" candidate when no attempt clears minSpacing

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

                // No valid spot found yet — keep whichever candidate is farthest
                // from its nearest neighbour so the forced fallback is least bad.
                float minDist = MinDistTo(gx, gy, avoidPositions, placed);
                if (minDist > bestMinDist)
                {
                    bestMinDist = minDist;
                    bestGx      = gx;
                    bestGy      = gy;
                }
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

    /// <summary>
    /// Returns the distance to the nearest point in either list.
    /// Used to pick the "least bad" fallback when no candidate clears minSpacing.
    /// </summary>
    private static float MinDistTo(
        float gx, float gy,
        IEnumerable<(float gx, float gy)> a,
        IEnumerable<(float gx, float gy)> b)
    {
        float min = float.MaxValue;
        if (a != null)
            foreach (var o in a)
            {
                float dx = gx - o.gx, dy = gy - o.gy;
                float d  = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < min) min = d;
            }
        if (b != null)
            foreach (var o in b)
            {
                float dx = gx - o.gx, dy = gy - o.gy;
                float d  = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d < min) min = d;
            }
        return min == float.MaxValue ? 0f : min;
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
            name: "Venus", type: PlanetType.Muddy, variant: 1, orbitalRadius: 0.13f,
            hasAtmosphere: true, isHabitable: false,
            desc: "A hellish world shrouded in thick, toxic clouds. Surface pressure crushes unshielded hulls."));

        rng.NextDouble(); // consume the rng slot Earth's angle used to occupy
        const float earthAngle = (float)(Math.PI * 5.0 / 6.0); // 150° — west-northwest
        pois.Add(MakeSolPlanetAtAngle(sol, saveGameId, rng,
            name: "Earth", type: PlanetType.Terrestrial, variant: 1, orbitalRadius: 0.16f,
            angle: earthAngle,
            hasAtmosphere: true, isHabitable: true,
            desc: "Humanity's birthplace. Still the most populated world in known space, and the seat of the Colonial Authority."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Mars", type: PlanetType.Arid, variant: 1, orbitalRadius: 0.19f,
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
            name: "Uranus", type: PlanetType.Icy, variant: 1, orbitalRadius: 0.42f,
            hasAtmosphere: true, isHabitable: false,
            desc: "A frigid ice giant tilted almost on its side. Remote, inhospitable, but a useful waypoint for deep-system runs."));

        pois.Add(MakeSolPlanet(sol, saveGameId, rng,
            name: "Neptune", type: PlanetType.Ocean, variant: 1, orbitalRadius: 0.46f,
            hasAtmosphere: true, isHabitable: false,
            desc: "The outermost major planet. Deep, swirling cobalt storms rage across its surface. Few ships venture this far without good reason."));

        // ── Earth Station ──────────────────────────────────────────────────
        // Placed just ahead of Earth on the same orbit (small angular offset).
        float stationAngle = earthAngle + 0.15f;   // ~8.6° ahead of Earth
        pois.Add(new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = sol.Id,
            Name         = "Earth Station",
            POIType      = Constants.POI.Types.SpaceStation,
            SystemX      = Math.Clamp(0.5f + 0.16f * (float)Math.Cos(stationAngle), 0.05f, 0.95f),
            SystemY      = Math.Clamp(0.5f + 0.16f * (float)Math.Sin(stationAngle), 0.05f, 0.95f),
            DangerLevel  = 0,
            IsBoardable  = true,
            Description  = "Humanity's oldest space station — still the busiest port in known space. " +
                           "Trade, repairs, crew recruitment, and rumours in equal measure."
        });

        return pois;
    }

    private static PointOfInterest MakeSolPlanetAtAngle(
        StarSystem sol, int saveGameId, Random rng,
        string name, PlanetType type, int variant, float orbitalRadius, float angle,
        bool hasAtmosphere, bool isHabitable, string desc)
    {
        float x = Math.Clamp(0.5f + orbitalRadius * (float)Math.Cos(angle), 0.05f, 0.95f);
        float y = Math.Clamp(0.5f + orbitalRadius * (float)Math.Sin(angle), 0.05f, 0.95f);
        var poi = new PointOfInterest
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
            Description   = desc
        };
        return poi;
    }

    private static PointOfInterest MakeSolPlanet(
        StarSystem sol, int saveGameId, Random rng,
        string name, PlanetType type, int variant, float orbitalRadius,
        bool hasAtmosphere, bool isHabitable, string desc)
    {
        float angle = (float)(rng.NextDouble() * Math.PI * 2);
        float x = Math.Clamp(0.5f + orbitalRadius * (float)Math.Cos(angle), 0.05f, 0.95f);
        float y = Math.Clamp(0.5f + orbitalRadius * (float)Math.Sin(angle), 0.05f, 0.95f);
        var poi = new PointOfInterest
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
            Description   = desc
        };
        return poi;
    }

    // ---------------------------------------------------------------------------
    // Alpha Centauri POI generation — hand-crafted
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generates Alpha Centauri's planets and station.
    /// A settled, "clean" system — no derelicts or anomalies.
    /// Planets are placed at fixed angular slots so nothing overlaps the station.
    /// </summary>
    public static List<PointOfInterest> GenerateAlphaCentauriPOIs(StarSystem system, int saveGameId)
    {
        var rng  = new Random(system.Seed + 9999);
        var pois = new List<PointOfInterest>();

        // ── Planets ───────────────────────────────────────────────────────────
        // Four planets at evenly-spaced angles (90° apart), seeded for
        // reproducibility. The station sits at a fifth slot (45° offset from
        // planet I) so it never shares an angular neighbourhood with any planet.

        (string name, PlanetType type, float orbitalRadius, bool hasAtmo, bool habitable, string desc)[] planetDefs =
        {
            ("Alpha Centauri I",   PlanetType.Magma,       0.10f, false, false,
             "A young, tectonically violent inner world. Lava plains and heavy-metal deposits make it valuable but dangerous."),
            ("Alpha Centauri II",  PlanetType.Arid,        0.18f, true,  false,
             "A dry, sun-baked world under the twin suns. Early terraforming efforts left scattered atmospheric processors across the surface."),
            ("Alpha Centauri III", PlanetType.Terrestrial, 0.27f, true,  true,
             "The system's primary colony world. Moderate climate and breathable air support a population of several million."),
            ("Alpha Centauri IV",  PlanetType.Frozen,      0.38f, true,  false,
             "A cold outer world beyond the habitable zone. Research stations monitor its unusual magnetic field anomalies."),
        };

        // Seed the base angle from the rng for variety, then space evenly at 90° increments.
        float baseAngle = (float)(rng.NextDouble() * Math.PI * 2);
        float slotWidth = (float)(Math.PI / 2);   // 90° between planets

        for (int i = 0; i < planetDefs.Length; i++)
        {
            var   d     = planetDefs[i];
            float angle = baseAngle + i * slotWidth;
            float x     = Math.Clamp(0.5f + d.orbitalRadius * (float)Math.Cos(angle), 0.05f, 0.95f);
            float y     = Math.Clamp(0.5f + d.orbitalRadius * (float)Math.Sin(angle), 0.05f, 0.95f);

            var poi = new PointOfInterest
            {
                SaveGameId    = saveGameId,
                StarSystemId  = system.Id,
                Name          = d.name,
                POIType       = Constants.POI.Types.Planet,
                PlanetType    = d.type,
                PlanetVariant = rng.Next(1, 6),
                HasAtmosphere = d.hasAtmo,
                IsHabitable   = d.habitable,
                SystemX       = x,
                SystemY       = y,
                DangerLevel   = system.DangerLevel,
                Description   = d.desc
            };
            pois.Add(poi);
        }

        // ── Station ───────────────────────────────────────────────────────────
        // Placed at 45° past the last planet slot — clear of all four planets.
        float stationAngle  = baseAngle + planetDefs.Length * slotWidth + (float)(Math.PI / 4);
        float stationRadius = 0.20f;
        pois.Add(new PointOfInterest
        {
            SaveGameId   = saveGameId,
            StarSystemId = system.Id,
            Name         = "Alpha Centauri Station",
            POIType      = Constants.POI.Types.SpaceStation,
            SystemX      = Math.Clamp(0.5f + stationRadius * (float)Math.Cos(stationAngle), 0.05f, 0.95f),
            SystemY      = Math.Clamp(0.5f + stationRadius * (float)Math.Sin(stationAngle), 0.05f, 0.95f),
            DangerLevel  = 0,
            IsBoardable  = true,
            Description  = "The oldest human outpost beyond Sol. A well-equipped port hub for the twin-sun colonies."
        });

        return pois;
    }

    // ---------------------------------------------------------------------------
    // General POI generation (non-Sol procedural systems)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generate all POIs for a procedurally generated star system.
    /// Call after the StarSystem has been inserted into the DB (needs system.Id).
    /// </summary>
    /// <param name="guaranteeHabitable">
    /// When true, at least one planet in this system will be habitable.
    /// Use this on one system per FTL-tier cluster to ensure the cluster has
    /// a habitable world even if random generation doesn't produce one naturally.
    /// </param>
    public static List<PointOfInterest> GeneratePOIsForSystem(
        StarSystem system, int saveGameId, bool guaranteeHabitable = false,
        bool generateResources = true)
    {
        var rng  = new Random(system.Seed + 1000);
        var pois = new List<PointOfInterest>();

        // Planets (4-6)
        // Base angle randomises the whole system's orientation; individual planets are then
        // distributed evenly (with small jitter) so no two land in the same angular slot.
        int   planetCount = rng.Next(5, 10);
        float baseAngle   = (float)(rng.NextDouble() * Math.PI * 2);
        for (int i = 0; i < planetCount; i++)
        {
            var planet = GeneratePlanet(system, saveGameId, i, planetCount, rng, baseAngle);
            if (generateResources) AssignPlanetResources(planet, system, rng);
            pois.Add(planet);
        }

        // Ensure at least one habitable planet if requested and none generated naturally.
        if (guaranteeHabitable && !pois.Exists(p => p.POIType == Constants.POI.Types.Planet && p.IsHabitable))
        {
            // Pick a random planet slot and upgrade it to a habitable Terrestrial.
            var p           = pois[rng.Next(0, planetCount)];
            p.PlanetType    = PlanetType.Terrestrial;
            p.HasAtmosphere = true;
            p.IsHabitable   = true;
            p.Description   = BuildPlanetDescription(PlanetType.Terrestrial, habitable: true);
        }

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

        if (generateResources)
            EnsureSystemResources(pois, system, rng);
        return pois;
    }

    // ---------------------------------------------------------------------------
    // Resource generation helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Rolls resource presence and amount for a single planet POI based on its type.
    /// Gas planets heavily favour He-3; rocky planets favour Iridium and Salvage.
    /// Amounts scale with the system's FTL tier so deeper systems have larger deposits.
    /// </summary>
    private static void AssignPlanetResources(PointOfInterest poi, StarSystem system, Random rng)
    {
        if (poi.POIType != Constants.POI.Types.Planet) return;

        bool isGas   = poi.PlanetType.IsGaseous();
        bool isRocky = poi.PlanetType.IsRocky();
        int  tier    = system.FtlTierRequired;

        poi.HasHelium3 = isGas   ? rng.Next(10) < 7
                       : isRocky ? rng.Next(10) < 1
                                 : rng.Next(10) < 1;

        poi.HasIridium = isRocky  ? rng.Next(10) < 7
                       : !isGas   ? rng.Next(10) < 2
                                  : false;

        poi.HasSalvage = isRocky ? rng.Next(10) < 4
                                 : false;

        if (poi.HasHelium3) poi.Helium3Amount = RollResourceAmount(Constants.CargoBay.GetHelium3Capacity(tier), rng);
        if (poi.HasIridium) poi.IridiumAmount = RollResourceAmount(Constants.CargoBay.GetIridiumCapacity(tier), rng);
        if (poi.HasSalvage) poi.SalvageAmount = RollResourceAmount(Constants.CargoBay.GetSalvageCapacity(tier), rng);

        // Every planet must carry at least one resource.
        if (!poi.HasHelium3 && !poi.HasIridium && !poi.HasSalvage)
        {
            if (isGas)
            {
                poi.HasHelium3    = true;
                poi.Helium3Amount = RollResourceAmount(Constants.CargoBay.GetHelium3Capacity(tier), rng);
            }
            else if (isRocky)
            {
                poi.HasIridium    = true;
                poi.IridiumAmount = RollResourceAmount(Constants.CargoBay.GetIridiumCapacity(tier), rng);
            }
            else
            {
                poi.HasHelium3    = true;
                poi.Helium3Amount = RollResourceAmount(Constants.CargoBay.GetHelium3Capacity(tier), rng);
            }
        }
    }

    /// <summary>
    /// Rolls a resource deposit amount within a tier-capacity-relative band.
    /// Distribution: 25% → [25%–50%], 65% → [50%–75%], 15% → [75%–100%].
    /// The minimum possible result is 25% of capacity.
    /// </summary>
    private static int RollResourceAmount(int capacity, Random rng)
    {
        int roll = rng.Next(100);
        double lo, hi;
        if      (roll < 25) { lo = 0.25; hi = 0.50; }
        else if (roll < 90) { lo = 0.50; hi = 0.75; }
        else                { lo = 0.75; hi = 1.00; }
        return Math.Max(1, (int)(capacity * (lo + rng.NextDouble() * (hi - lo))));
    }

    /// <summary>
    /// Guarantees that at least one planet per system carries each resource type.
    /// Prefers the most thematically appropriate planet type for each resource.
    /// Also generates an amount for any resource forced on here.
    /// </summary>
    private static void EnsureSystemResources(List<PointOfInterest> pois, StarSystem system, Random rng)
    {
        var planets = pois.FindAll(p => p.POIType == Constants.POI.Types.Planet);
        if (planets.Count == 0) return;

        int tier = system.FtlTierRequired;

        if (!planets.Exists(p => p.HasHelium3))
        {
            var gas = planets.FindAll(p => p.PlanetType.IsGaseous());
            var p   = gas.Count > 0 ? gas[rng.Next(gas.Count)] : planets[rng.Next(planets.Count)];
            p.HasHelium3    = true;
            p.Helium3Amount = RollResourceAmount(Constants.CargoBay.GetHelium3Capacity(tier), rng);
        }

        if (!planets.Exists(p => p.HasIridium))
        {
            var rocky = planets.FindAll(p => p.PlanetType.IsRocky());
            var p     = rocky.Count > 0 ? rocky[rng.Next(rocky.Count)] : planets[rng.Next(planets.Count)];
            p.HasIridium    = true;
            p.IridiumAmount = RollResourceAmount(Constants.CargoBay.GetIridiumCapacity(tier), rng);
        }

        if (!planets.Exists(p => p.HasSalvage))
        {
            var rocky = planets.FindAll(p => p.PlanetType.IsRocky());
            var p     = rocky.Count > 0 ? rocky[rng.Next(rocky.Count)] : planets[rng.Next(planets.Count)];
            p.HasSalvage    = true;
            p.SalvageAmount = RollResourceAmount(Constants.CargoBay.GetSalvageCapacity(tier), rng);
        }
    }

    // ---------------------------------------------------------------------------
    // Individual POI generators
    // ---------------------------------------------------------------------------

    private static PointOfInterest GeneratePlanet(
        StarSystem system, int saveGameId, int index, int total, Random rng,
        float baseAngle = 0f)
    {
        // Pick a random planet type from all available types
        var allTypes  = (PlanetType[])Enum.GetValues(typeof(PlanetType));
        var type      = allTypes[rng.Next(allTypes.Length)];
        int variant   = rng.Next(1, 6);
        bool hasAtmo  = !IsAirlessType(type) && rng.Next(0, 10) < 7;
        bool habitable = hasAtmo && type == PlanetType.Terrestrial && rng.Next(0, 10) < 4;

        // Distribute planets evenly around the system, with up to ±25% per-slot jitter,
        // so no two planets land in the same angular neighbourhood regardless of count.
        float slotWidth   = (float)(2.0 * Math.PI / Math.Max(total, 1));
        float jitter      = (float)(rng.NextDouble() - 0.5) * slotWidth * 0.5f;
        float angle       = baseAngle + index * slotWidth + jitter;
        float baseRadius  = 0.08f + (index / (float)Math.Max(total, 1)) * 0.35f;
        float radJitter   = (float)(rng.NextDouble() - 0.5) * 0.06f;  // ±0.03 variation
        float radius      = Math.Clamp(baseRadius + radJitter, 0.07f, 0.46f);

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
            Description   = BuildPlanetDescription(type, habitable)
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
            Description  = "A dense field of rocky debris — rich in minerals but hazardous to navigate."
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
