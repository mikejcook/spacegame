using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Factory for creating Character instances with appropriate starting stats.
/// Names and genders are generated via NameGenerator. Pass a specific Gender to
/// override the random pick (e.g. for characters whose gender is fixed by lore).
/// </summary>
public static class CharacterFactory
{
    // ---------------------------------------------------------------------------
    // Starting crew
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates the player captain. The full name string is stored as-is in FirstName
    /// (no last name split). The captain has no gender — pronouns are always second-person.
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
            [Constants.Skills.Command]    = 2,
            [Constants.Skills.Piloting]   = 1,
            [Constants.Skills.Combat]     = 1,
            [Constants.Skills.Navigation] = 1,
            [Constants.Skills.Diplomacy]  = 1,
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
            [Constants.Skills.Piloting]   = 3,
            [Constants.Skills.Navigation] = 2,
            [Constants.Skills.Combat]     = 1,
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
            [Constants.Skills.Engineering] = 3,
            [Constants.Skills.Electronics] = 2,
            [Constants.Skills.Science]     = 1,
        };

        return engineer;
    }

    // ---------------------------------------------------------------------------
    // Procedural recruitment
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Generate a recruitable crew member for the given role at the given level.
    /// Used by space stations and random encounters.
    /// </summary>
    public static Character CreateRandomCrewMember(string role, int level = 1)
    {
        var generated = NameGenerator.Generate();

        var crew = new Character
        {
            FirstName     = generated.firstName,
            LastName      = generated.lastName,
            Gender        = generated.gender,
            Role          = role,
            Level         = level,
            MaxHealth     = 8 + (level * 2),
            CurrentHealth = 8 + (level * 2),
            Background    = GenerateBackground(role),
            Homeworld     = GenerateHomeworld(),
        };

        crew.Skills = GenerateSkillsForRole(role, level);

        return crew;
    }

    // ---------------------------------------------------------------------------
    // Internal helpers
    // ---------------------------------------------------------------------------

    private static Dictionary<string, int> GenerateSkillsForRole(string role, int level)
    {
        var skills     = new Dictionary<string, int>();
        int totalPoints = level + 2;

        string primary = role switch
        {
            Constants.Crew.Roles.Pilot          => Constants.Skills.Piloting,
            Constants.Crew.Roles.Engineer       => Constants.Skills.Engineering,
            Constants.Crew.Roles.Scientist      => Constants.Skills.Science,
            Constants.Crew.Roles.Doctor         => Constants.Skills.Medicine,
            Constants.Crew.Roles.WeaponsOfficer => Constants.Skills.Combat,
            Constants.Crew.Roles.Soldier        => Constants.Skills.Combat,
            _                                   => Constants.Skills.Command
        };

        string secondary = role switch
        {
            Constants.Crew.Roles.Pilot          => Constants.Skills.Navigation,
            Constants.Crew.Roles.Engineer       => Constants.Skills.Electronics,
            Constants.Crew.Roles.Scientist      => Constants.Skills.Electronics,
            Constants.Crew.Roles.Doctor         => Constants.Skills.Science,
            Constants.Crew.Roles.WeaponsOfficer => Constants.Skills.Electronics,
            Constants.Crew.Roles.Soldier        => Constants.Skills.Survival,
            _                                   => Constants.Skills.Diplomacy
        };

        skills[primary]   = Mathf.Min(totalPoints,     5);
        skills[secondary] = Mathf.Min(totalPoints / 2, 3);

        return skills;
    }

    private static readonly string[] Homeworlds =
    {
        "Earth", "Mars", "Titan Colony", "Europa Station", "Ceres",
        "Proxima b", "New Shanghai", "Brasilia Station", "Lunar City"
    };

    private static string GenerateHomeworld()
        => Homeworlds[Random.Range(0, Homeworlds.Length)];

    private static string GenerateBackground(string role)
    {
        return role switch
        {
            Constants.Crew.Roles.Pilot          => "Veteran pilot, logs spanning a dozen systems.",
            Constants.Crew.Roles.Engineer       => "Ship engineer with a knack for coaxing power out of junk.",
            Constants.Crew.Roles.Scientist      => "Researcher chasing discoveries beyond the frontier.",
            Constants.Crew.Roles.Doctor         => "Field medic who has patched people up in worse places than this.",
            Constants.Crew.Roles.WeaponsOfficer => "Former military, expert in ship-to-ship combat.",
            Constants.Crew.Roles.Soldier        => "Mercenary ground-pounder, good in a fight.",
            _                                   => "A capable spacer looking for steady work."
        };
    }
}
