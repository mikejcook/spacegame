using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Factory for creating Character instances with appropriate starting stats.
/// Named crew members are placeholders — you'll want to replace them with
/// a proper recruitment/procedural system later.
/// </summary>
public static class CharacterFactory
{
    // ---------------------------------------------------------------------------
    // Starting crew
    // ---------------------------------------------------------------------------

    public static Character CreateCaptain(string name, string portraitFileName = "")
    {
        var captain = new Character
        {
            Name            = name,
            Role            = Constants.Crew.Roles.Captain,
            IsPlayerCaptain = true,
            Level           = 1,
            MaxHealth       = 12,
            CurrentHealth   = 12,
            Background      = "A seasoned spacer who decided to strike out on their own.",
            Homeworld       = "Earth",
            // PortraitId stores the filename chosen during new-game setup (e.g. "7.png")
            PortraitId      = string.IsNullOrEmpty(portraitFileName) ? "portrait_default" : portraitFileName
        };

        // Captains are generalists with a command focus
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
        var pilot = new Character
        {
            Name          = "Alex Chen",
            Role          = Constants.Crew.Roles.Pilot,
            Level         = 1,
            MaxHealth     = 10,
            CurrentHealth = 10,
            PortraitId    = "portrait_pilot_01",
            Background    = "Former colonial courier pilot, fast and reckless.",
            Homeworld     = "Mars"
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
        var engineer = new Character
        {
            Name          = "Mira Santos",
            Role          = Constants.Crew.Roles.Engineer,
            Level         = 1,
            MaxHealth     = 10,
            CurrentHealth = 10,
            PortraitId    = "portrait_engineer_01",
            Background    = "Self-taught ship mechanic who can fix anything with duct tape and spite.",
            Homeworld     = "Titan Colony"
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
        var crew = new Character
        {
            Name          = GenerateRandomName(),
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
        int totalPoints = level + 2; // grows with level

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

    private static readonly string[] FirstNames =
    {
        "Jordan", "Riley", "Sam", "Morgan", "Casey", "Drew", "Avery", "Quinn",
        "Kai", "Reese", "Dakota", "Skyler", "Ren", "Noa", "Blair", "Sage"
    };

    private static readonly string[] LastNames =
    {
        "Chen", "Patel", "Kim", "Santos", "Okonkwo", "Reyes", "Fischer",
        "Nakamura", "Petrov", "Osei", "Lindqvist", "Mbeki", "Kowalski", "Diallo"
    };

    private static readonly string[] Homeworlds =
    {
        "Earth", "Mars", "Titan Colony", "Europa Station", "Ceres",
        "Proxima b", "New Shanghai", "Brasilia Station", "Lunar City"
    };

    private static string GenerateRandomName()
    {
        var rng = new System.Random();
        return $"{FirstNames[rng.Next(FirstNames.Length)]} {LastNames[rng.Next(LastNames.Length)]}";
    }

    private static string GenerateHomeworld()
    {
        var rng = new System.Random();
        return Homeworlds[rng.Next(Homeworlds.Length)];
    }

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
