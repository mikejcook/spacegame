using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Factory for creating Character instances with appropriate starting stats.
///
/// Skills: Command, Piloting, Beam Weapons, Torpedo Weapons, Science, Engineering.
///
/// Allocation rules for procedurally generated crew:
///   • Total skill points = level × 3 (Constants.Skills.PointsPerLevel).
///   • No single skill may exceed level + 1 ranks.
///   • Beam Weapons and Torpedo Weapons tend to go together.
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

        captain.Skills = new Dictionary<string, int>
        {
            [Constants.Skills.Command]  = 2,
            [Constants.Skills.Piloting] = 1,
        };

        return captain;
    }

    public static Character CreateStartingPilot()
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
            PortraitId    = "male_5.png",
            Background    = "Former colonial courier pilot, fast and reckless.",
            Homeworld     = GenerateHomeworld(),
        };

        pilot.Skills = new Dictionary<string, int>
        {
            [Constants.Skills.Piloting] = 2,
            [Constants.Skills.Command]  = 1,
        };

        return pilot;
    }

    public static Character CreateStartingEngineer()
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
            PortraitId    = "female_6.png",
            Background    = "Self-taught ship mechanic who can fix anything with duct tape and spite.",
            Homeworld     = GenerateHomeworld(),
        };

        engineer.Skills = new Dictionary<string, int>
        {
            [Constants.Skills.Engineering] = 2,
            [Constants.Skills.Science]     = 1,
        };

        return engineer;
    }

    // -------------------------------------------------------------------------
    // Procedural recruitment — used by space stations
    // -------------------------------------------------------------------------

    /// <summary>
    /// Generates a recruitable crew member for the given role at the given level.
    /// Skill points = level × PointsPerLevel, capped at level + 1 per skill.
    /// AvailableSkillPoints is 0 (all pre-allocated).
    /// </summary>
    public static Character CreateRandomCrewMember(string role, int level = 1)
    {
        level = Mathf.Max(1, level);
        var generated = NameGenerator.Generate();

        var crew = new Character
        {
            FirstName            = generated.firstName,
            LastName             = generated.lastName,
            Gender               = generated.gender,
            Role                 = role,
            Level                = level,
            MaxHealth            = 8 + (level * 2),
            CurrentHealth        = 8 + (level * 2),
            Background           = GenerateBackground(role),
            Homeworld            = GenerateHomeworld(),
            AvailableSkillPoints = 0,  // all points pre-spent below
        };

        crew.Skills = AllocateSkillsForRole(role, level);
        return crew;
    }

    // -------------------------------------------------------------------------
    // Skill allocation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Distributes level × PointsPerLevel skill points:
    ///   1. Assign primary and secondary skills based on role (weapons officer gets
    ///      both Beam Weapons and Torpedo Weapons weighted equally).
    ///   2. Scatter remaining points randomly, respecting the per-skill cap (level+1).
    /// </summary>
    private static Dictionary<string, int> AllocateSkillsForRole(string role, int level)
    {
        int totalPoints = level * Constants.Skills.PointsPerLevel;
        int cap         = Constants.Skills.MaxRankForLevel(level);

        var skills = new Dictionary<string, int>();
        foreach (var s in Constants.Skills.All)
            skills[s] = 0;

        // Primary and secondary skill for each role
        string primary, secondary;

        switch (role)
        {
            case Constants.Crew.Roles.Pilot:
                primary   = Constants.Skills.Piloting;
                secondary = Constants.Skills.Command;
                break;
            case Constants.Crew.Roles.Engineer:
                primary   = Constants.Skills.Engineering;
                secondary = Constants.Skills.Science;
                break;
            case Constants.Crew.Roles.Scientist:
                primary   = Constants.Skills.Science;
                secondary = Constants.Skills.Engineering;
                break;
            case Constants.Crew.Roles.WeaponsOfficer:
                primary   = Constants.Skills.BeamWeapons;
                secondary = Constants.Skills.TorpedoWeapons;
                break;
            case Constants.Crew.Roles.Captain:
            default:
                primary   = Constants.Skills.Command;
                secondary = Constants.Skills.Piloting;
                break;
        }

        // Spend ~50 % on primary, ~30 % on secondary (both capped)
        int primaryPoints   = Mathf.Min(cap, Mathf.CeilToInt(totalPoints * 0.50f));
        int secondaryPoints = Mathf.Min(cap, Mathf.CeilToInt(totalPoints * 0.30f));

        skills[primary]   = primaryPoints;
        skills[secondary] = secondaryPoints;
        int spent         = primaryPoints + secondaryPoints;

        // Weapons officer bonus: try to equalise the two weapon skills
        if (role == Constants.Crew.Roles.WeaponsOfficer && spent < totalPoints)
        {
            int torpCap  = cap - skills[Constants.Skills.TorpedoWeapons];
            int bonus    = Mathf.Min(torpCap, totalPoints - spent);
            if (bonus > 0)
            {
                skills[Constants.Skills.TorpedoWeapons] += bonus;
                spent += bonus;
            }
        }

        // Scatter remainder across all skills respecting the cap
        int attempts = 0;
        while (spent < totalPoints && attempts < 1000)
        {
            attempts++;
            string pick = Constants.Skills.All[Random.Range(0, Constants.Skills.All.Length)];
            if (skills[pick] < cap)
            {
                skills[pick]++;
                spent++;
            }
        }

        // Strip zero-rank entries so the UI only shows invested skills
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

    private static string GenerateBackground(string role) => role switch
    {
        Constants.Crew.Roles.Pilot          => "Veteran pilot, logs spanning a dozen systems.",
        Constants.Crew.Roles.Engineer       => "Ship engineer with a knack for coaxing power out of junk.",
        Constants.Crew.Roles.Scientist      => "Researcher chasing discoveries beyond the frontier.",
        Constants.Crew.Roles.WeaponsOfficer => "Former military, expert in ship-to-ship combat.",
        Constants.Crew.Roles.Captain        => "Ex-officer who decided command meant more than a rank.",
        _                                   => "A capable spacer looking for steady work."
    };
}
