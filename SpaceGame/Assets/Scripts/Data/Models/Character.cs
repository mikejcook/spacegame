using System.Collections.Generic;
using SQLite4Unity3d;
using UnityEngine;

/// <summary>
/// Represents the player captain or any crew member.
/// Skills are stored as JSON (skill name -> rank 0-10).
/// Feats are stored as JSON (list of feat IDs).
///
/// DEPENDENCY: Requires Newtonsoft.Json (JSON.NET for Unity).
///   Install via Package Manager -> Add by name: com.unity.nuget.newtonsoft-json
/// </summary>
[Table("Characters")]
public class Character
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int  SaveGameId      { get; set; }
    public string Role          { get; set; }  // see Constants.Crew.Roles
    public bool IsPlayerCaptain { get; set; }

    // Name — stored as two columns; the combined Name is computed
    public string FirstName { get; set; }
    public string LastName  { get; set; }

    /// <summary>Full display name. Not stored — derived from FirstName + LastName.</summary>
    [Ignore]
    public string Name => string.IsNullOrEmpty(LastName) ? FirstName ?? "" : $"{FirstName} {LastName}";

    // Gender — stored as a tinyint (maps to the Gender enum)
    public Gender Gender { get; set; } = Gender.NonBinary;

    // Portrait references a filename in Resources/PortraitsNew/ (e.g. "male_1.png")
    public string PortraitId { get; set; } = "male_1.png";

    /// <summary>
    /// If > 0, this character is a pending recruit at the space station with this POI ID.
    /// 0 = hired crew (part of the ship's active roster).
    /// Set to 0 when the player hires the recruit.
    /// </summary>
    public int StationPoiId { get; set; } = 0;

    // Progression
    public int Level                { get; set; } = 1;
    public int ExperiencePoints     { get; set; } = 0;
    public int ExperienceToNextLevel { get; set; } = 300; // XP to reach level 2

    /// <summary>
    /// Unspent skill points available to allocate. Grants
    /// <see cref="Constants.Skills.PointsPerLevel"/> each level-up.
    /// </summary>
    public int AvailableSkillPoints { get; set; } = 0;

    // Vitals
    public int MaxHealth     { get; set; } = 10;
    public int CurrentHealth { get; set; } = 10;

    // Status flags
    public bool IsActive     { get; set; } = true;
    public bool IsInjured    { get; set; } = false;
    public bool IsRecovering { get; set; } = false;

    // Lore
    public string Background { get; set; }
    public string Homeworld  { get; set; }

    // ---------------------------------------------------------------------------
    // Serialized collections — stored as JSON in the DB
    // ---------------------------------------------------------------------------

    [Column("SkillsJson")]
    public string SkillsJson { get; set; } = "{}";

    [Column("FeatsJson")]
    public string FeatsJson  { get; set; } = "[]";

    // ---------------------------------------------------------------------------
    // In-memory helpers (not stored in DB directly)
    // ---------------------------------------------------------------------------

    [Ignore]
    public Dictionary<string, int> Skills
    {
        get => Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, int>>(SkillsJson)
               ?? new Dictionary<string, int>();
        set => SkillsJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }

    [Ignore]
    public List<string> Feats
    {
        get => Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(FeatsJson)
               ?? new List<string>();
        set => FeatsJson = Newtonsoft.Json.JsonConvert.SerializeObject(value);
    }

    // ---------------------------------------------------------------------------
    // Pronouns
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Which pronoun form to retrieve.
    ///   Subject          → he / she / they
    ///   Object           → him / her / them
    ///   PossessiveAdj    → his / her / their      ("his ship", "her ship")
    ///   PossessivePronoun → his / hers / theirs   ("the ship is his")
    ///   Reflexive        → himself / herself / themselves
    /// </summary>
    public enum PronounType : byte
    {
        Subject          = 0,
        Object           = 1,
        PossessiveAdj    = 2,
        PossessivePronoun = 3,
        Reflexive        = 4,
    }

    /// <summary>
    /// Returns the correct pronoun for this character's gender.
    /// Characters with Gender.None (i.e. the player captain) always return second-person forms.
    /// </summary>
    public string GetPronoun(PronounType type)
    {
        return (Gender, type) switch
        {
            (Gender.Male,      PronounType.Subject)           => "he",
            (Gender.Male,      PronounType.Object)            => "him",
            (Gender.Male,      PronounType.PossessiveAdj)     => "his",
            (Gender.Male,      PronounType.PossessivePronoun) => "his",
            (Gender.Male,      PronounType.Reflexive)         => "himself",

            (Gender.Female,    PronounType.Subject)           => "she",
            (Gender.Female,    PronounType.Object)            => "her",
            (Gender.Female,    PronounType.PossessiveAdj)     => "her",
            (Gender.Female,    PronounType.PossessivePronoun) => "hers",
            (Gender.Female,    PronounType.Reflexive)         => "herself",

            (Gender.NonBinary, PronounType.Subject)           => "they",
            (Gender.NonBinary, PronounType.Object)            => "them",
            (Gender.NonBinary, PronounType.PossessiveAdj)     => "their",
            (Gender.NonBinary, PronounType.PossessivePronoun) => "theirs",
            (Gender.NonBinary, PronounType.Reflexive)         => "themselves",

            // Gender.None — player captain, always addressed in second person
            (Gender.None,      PronounType.Subject)           => "you",
            (Gender.None,      PronounType.Object)            => "you",
            (Gender.None,      PronounType.PossessiveAdj)     => "your",
            (Gender.None,      PronounType.PossessivePronoun) => "yours",
            (Gender.None,      PronounType.Reflexive)         => "yourself",

            _ => "they",
        };
    }

    // ---------------------------------------------------------------------------
    // Convenience methods
    // ---------------------------------------------------------------------------

    public int GetSkillRank(string skillName)
    {
        var skills = Skills;
        return skills.TryGetValue(skillName, out var rank) ? rank : 0;
    }

    public void SetSkillRank(string skillName, int rank)
    {
        var skills = Skills;
        skills[skillName] = Mathf.Clamp(rank, 0, 10);
        Skills = skills;
    }

    public void AddSkillRank(string skillName, int amount = 1)
        => SetSkillRank(skillName, GetSkillRank(skillName) + amount);

    public bool HasFeat(string featId)
        => Feats.Contains(featId);

    public void AddFeat(string featId)
    {
        var feats = Feats;
        if (!feats.Contains(featId))
        {
            feats.Add(featId);
            Feats = feats;
        }
    }

    public void GainExperience(int amount)
    {
        ExperiencePoints += amount;
        while (ExperiencePoints >= ExperienceToNextLevel)
        {
            ExperiencePoints -= ExperienceToNextLevel;
            LevelUp();
        }
    }

    /// <summary>
    /// Grants one level: increments Level, awards skill points, and increases max health.
    /// Called automatically by <see cref="GainExperience"/> on each level threshold,
    /// and may be called directly for constructed characters (e.g. to start at level 3).
    /// </summary>
    public void LevelUp()
    {
        Level                  += 1;
        AvailableSkillPoints   += Constants.Skills.PointsPerLevel;
        MaxHealth              += 2;
        CurrentHealth          += 2;
        ExperienceToNextLevel   = CalculateXPForLevel(Level + 1);
        Debug.Log($"[Character] {Name} reached level {Level}! (+{Constants.Skills.PointsPerLevel} skill points)");
    }

    // XP required to reach the given level
    private static int CalculateXPForLevel(int level) => level switch
    {
        2  => 300,
        3  => 600,
        4  => 1_800,
        5  => 3_800,
        6  => 6_500,
        7  => 8_000,
        8  => 9_000,
        9  => 12_000,
        10 => 14_000,
        11 => 20_000,
        12 => 24_000,
        _  => 24_000 + (level - 12) * 4_000,  // +4,000 per level beyond 12
    };
}
