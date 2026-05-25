using System.Collections.Generic;
using SQLite;
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

    // Portrait references a sprite name in Resources/Portraits/
    public string PortraitId { get; set; } = "portrait_default";

    // Progression
    public int Level                { get; set; } = 1;
    public int ExperiencePoints     { get; set; } = 0;
    public int ExperienceToNextLevel { get; set; } = 100;

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
            ExperiencePoints      -= ExperienceToNextLevel;
            Level                 += 1;
            ExperienceToNextLevel  = CalculateXPForLevel(Level + 1);
            OnLevelUp();
        }
    }

    private void OnLevelUp()
    {
        MaxHealth     += 2;
        CurrentHealth += 2;
        Debug.Log($"[Character] {Name} reached level {Level}!");
    }

    private static int CalculateXPForLevel(int level) => level * 100;
}
