using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Factory for creating Character instances with appropriate starting stats.
///
/// Skills: Athletics, Combat, Command, Engineering, Gunnery, Medicine, Perception, Piloting, Science.
///
/// Allocation rules for procedurally generated crew:
///   • Total skill points = level × 3 (Constants.Skills.PointsPerLevel).
///   • No single skill may exceed level + 1 ranks.
///   • Recruited crew have Role = "" (unassigned) — the role parameter is used only
///     to guide skill distribution, not stored on the character.
///   • AvailableSkillPoints is 0 — all points are pre-allocated at creation.
/// </summary>
public static class CharacterFactory
{
    // -------------------------------------------------------------------------
    // Starting crew
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates the player captain. Name stored entirely in FirstName.
    /// Gender.None → second-person pronouns everywhere.
    /// </summary>
    public static Character CreateCaptain(string name, string portraitFileName = "")
    {
        var captain = new Character
        {
            FirstName       = name,
            LastName        = "",
            Gender          = Gender.None,
            Role            = Constants.Crew.Roles.Captain,
            IsPlayerCaptain = true,
            Level           = 1,
            MaxHealth       = 12,
            CurrentHealth   = 12,
            Background      = "A seasoned spacer who decided to strike out on their own.",
            Homeworld       = "Earth",
            PortraitId      = string.IsNullOrEmpty(portraitFileName) ? "male_1.png" : portraitFileName
        };

        // Captain starts with no pre-allocated skills — the player spends 3 points
        // in the first-launch level-up screen (Recommended: +2 Command, +1 Perception).
        captain.Skills               = new Dictionary<string, int>();
        captain.AvailableSkillPoints = Constants.Skills.PointsPerLevel;

        return captain;
    }

    public static Character CreateStartingPilot(PortraitLibrary portraits = null, HashSet<string> usedPortraits = null)
    {
        var generated = NameGenerator.Generate();

        var pilot = new Character
        {
            FirstName     = generated.firstName,
            LastName      = generated.lastName,
            Gender        = generated.gender,
            Role          = Constants.Crew.Roles.Pilot,
            Level         = 1,
            MaxHealth     = 10,
            CurrentHealth = 10,
            PortraitId    = portraits != null
                                ? portraits.RandomFileNameForGender(generated.gender, usedPortraits)
                                : (generated.gender == Gender.Female ? "female_5.png" : "male_5.png"),
            Background    = "Former colonial courier pilot, fast and reckless.",
            Homeworld     = GenerateHomeworld(),
        };

        pilot.Skills = new Dictionary<string, int>
        {
            [Constants.Skills.Piloting]   = 2,
            [Constants.Skills.Perception] = 1
        };

        return pilot;
    }

    public static Character CreateStartingEngineer(PortraitLibrary portraits = null, HashSet<string> usedPortraits = null)
    {
        var generated = NameGenerator.Generate();

        var engineer = new Character
        {
            FirstName     = generated.firstName,
            LastName      = generated.lastName,
            Gender        = generated.gender,
            Role          = Constants.Crew.Roles.Engineer,
            Level         = 1,
            MaxHealth     = 10,
            CurrentHealth = 10,
            PortraitId    = portraits != null
                                ? portraits.RandomFileNameForGender(generated.gender, usedPortraits)
                                : (generated.gender == Gender.Female ? "female_6.png" : "male_6.png"),
            Background    = "Self-taught ship mechanic who can fix anything with duct tape and spite.",
            Homeworld     = GenerateHomeworld(),
        };

        engineer.Skills = new Dictionary<string, int>
        {
            [Constants.Skills.Engineering] = 2,
            [Constants.Skills.Science]     = 1
        };

        return engineer;
    }

    // -------------------------------------------------------------------------
    // Procedural recruitment — used by space stations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates a recruitable crew member.
    /// The <paramref name="specialty"/> guides skill allocation but is NOT stored
    /// as the character's Role — recruited crew are unassigned (Role = "").
    /// Skill points = level × PointsPerLevel, capped at level + 1 per skill.
    /// AvailableSkillPoints is 0 (all pre-allocated).
    /// </summary>
    public static Character CreateRandomCrewMember(string specialty, int level = 1,
        PortraitLibrary portraits = null, HashSet<string> usedPortraits = null)
    {
        level = Mathf.Max(1, level);
        var generated = NameGenerator.Generate();

        var crew = new Character
        {
            FirstName            = generated.firstName,
            LastName             = generated.lastName,
            Gender               = generated.gender,
            Role                 = "",   // unassigned — player places them via Assignments screen
            Level                = level,
            MaxHealth            = 8 + (level * 2),
            CurrentHealth        = 8 + (level * 2),
            Background           = GenerateBackground(specialty),
            Homeworld            = GenerateHomeworld(),
            AvailableSkillPoints = 0,
            PortraitId           = portraits != null
                                       ? portraits.RandomFileNameForGender(generated.gender, usedPortraits)
                                       : (generated.gender == Gender.Female ? "female_1.png" : "male_1.png"),
        };

        crew.Skills = AllocateSkillsForRole(specialty, level);
        return crew;
    }

    // -------------------------------------------------------------------------
    // Skill allocation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Distributes level × PointsPerLevel skill points guided by a specialty string.
    /// ~50 % goes to the primary skill, ~30 % to the secondary; remainder scatters randomly.
    /// </summary>
    private static Dictionary<string, int> AllocateSkillsForRole(string specialty, int level)
    {
        int totalPoints = level * Constants.Skills.PointsPerLevel;
        int cap         = Constants.Skills.MaxRankForLevel(level);

        var skills = new Dictionary<string, int>();
        foreach (var s in Constants.Skills.All)
            skills[s] = 0;

        string primary, secondary;
        switch (specialty)
        {
            case Constants.Crew.Roles.Pilot:
                primary   = Constants.Skills.Piloting;
                secondary = Constants.Skills.Perception;
                break;
            case Constants.Crew.Roles.Engineer:
                primary   = Constants.Skills.Engineering;
                secondary = Constants.Skills.Science;
                break;
            case Constants.Crew.Roles.Scientist:
                primary   = Constants.Skills.Science;
                secondary = Constants.Skills.Engineering;
                break;
            case Constants.Crew.Roles.Gunner:
                primary   = Constants.Skills.Gunnery;
                secondary = Constants.Skills.Combat;
                break;
            case Constants.Crew.Roles.Doctor:
                primary   = Constants.Skills.Medicine;
                secondary = Constants.Skills.Science;
                break;
            case Constants.Crew.Roles.Soldier:
                primary   = Constants.Skills.Combat;
                secondary = Constants.Skills.Athletics;
                break;
            case Constants.Crew.Roles.Captain:
            default:
                primary   = Constants.Skills.Command;
                secondary = Constants.Skills.Piloting;
                break;
        }

        int primaryPoints   = Mathf.Min(cap, Mathf.CeilToInt(totalPoints * 0.50f));
        int secondaryPoints = Mathf.Min(cap, Mathf.CeilToInt(totalPoints * 0.30f));
        skills[primary]   = primaryPoints;
        skills[secondary] = secondaryPoints;
        int spent         = primaryPoints + secondaryPoints;

        // Scatter remainder
        int attempts = 0;
        while (spent < totalPoints && attempts < 1000)
        {
            attempts++;
            string pick = Constants.Skills.All[Random.Range(0, Constants.Skills.All.Length)];
            if (skills[pick] < cap) { skills[pick]++; spent++; }
        }

        // Strip zeros so the UI only shows invested skills
        var result = new Dictionary<string, int>();
        foreach (var kv in skills)
            if (kv.Value > 0) result[kv.Key] = kv.Value;
        return result;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static readonly string[] Homeworlds =
    {
        "Earth", "Mars", "Titan Colony", "Europa Station", "Ceres",
        "Proxima b", "New Shanghai", "Brasilia Station", "Lunar City"
    };

    private static string GenerateHomeworld()
        => Homeworlds[Random.Range(0, Homeworlds.Length)];

    private static string GenerateBackground(string specialty) => specialty switch
    {
        Constants.Crew.Roles.Pilot      => "Veteran pilot, logs spanning a dozen systems.",
        Constants.Crew.Roles.Engineer   => "Ship engineer with a knack for coaxing power out of junk.",
        Constants.Crew.Roles.Scientist  => "Researcher chasing discoveries beyond the frontier.",
        Constants.Crew.Roles.Gunner     => "Former military, expert in ship-to-ship weapons.",
        Constants.Crew.Roles.Doctor     => "Field medic who has patched up crews in the worst conditions.",
        Constants.Crew.Roles.Soldier    => "Hardened veteran looking for a crew worth fighting for.",
        Constants.Crew.Roles.Captain    => "Ex-officer who decided command meant more than a rank.",
        _                               => "A capable spacer looking for steady work."
    };
}
