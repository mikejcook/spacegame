using System.Collections.Generic;

/// <summary>
/// Defines a feat — a discrete special ability a character can learn.
/// FeatDefinitions are static game data (not stored per-save).
/// Which feats a character HAS is stored in Character.FeatsJson.
/// </summary>
[System.Serializable]
public class FeatDefinition
{
    public string Id          { get; set; }
    public string Name        { get; set; }
    public string Description { get; set; }

    // Prerequisites
    public Dictionary<string, int> RequiredSkillRanks { get; set; } = new();
    public List<string>            RequiredFeats       { get; set; } = new();
    public int                     RequiredLevel       { get; set; } = 1;

    // Empty = available to all roles
    public List<string> AvailableToRoles { get; set; } = new();

    // Effect hook — game systems read EffectType to apply bonuses/abilities
    public string EffectType      { get; set; }
    public float  EffectMagnitude { get; set; }

    // ---------------------------------------------------------------------------
    // Validation
    // ---------------------------------------------------------------------------

    public bool CanCharacterLearn(Character character)
    {
        if (character.HasFeat(Id))           return false;
        if (character.Level < RequiredLevel)  return false;

        foreach (var (skill, required) in RequiredSkillRanks)
            if (character.GetSkillRank(skill) < required) return false;

        foreach (var feat in RequiredFeats)
            if (!character.HasFeat(feat)) return false;

        if (AvailableToRoles.Count > 0 && !AvailableToRoles.Contains(character.Role))
            return false;

        return true;
    }
}

// ---------------------------------------------------------------------------
// Static registry of all feats in the game
// ---------------------------------------------------------------------------

/// <summary>
/// Single source of truth for feat definitions.
/// Add new feats here as game design evolves.
/// </summary>
public static class FeatRegistry
{
    private static Dictionary<string, FeatDefinition> _feats;

    public static Dictionary<string, FeatDefinition> All
    {
        get { if (_feats == null) Build(); return _feats; }
    }

    public static FeatDefinition Get(string id)
        => All.TryGetValue(id, out var f) ? f : null;

    public static List<FeatDefinition> AvailableFor(Character character)
    {
        var available = new List<FeatDefinition>();
        foreach (var feat in All.Values)
            if (feat.CanCharacterLearn(character))
                available.Add(feat);
        return available;
    }

    private static void Build()
    {
        _feats = new Dictionary<string, FeatDefinition>
        {
            ["ace_pilot"] = new FeatDefinition
            {
                Id          = "ace_pilot",
                Name        = "Ace Pilot",
                Description = "Exceptional piloting instincts. +3 to all Piloting checks. Once per encounter may reroll a failed evasion roll.",
                RequiredSkillRanks = new() { [Constants.Skills.Piloting] = 3 },
                RequiredLevel      = 3,
                AvailableToRoles   = new() { Constants.Crew.Roles.Pilot, Constants.Crew.Roles.Captain },
                EffectType         = "piloting_bonus",
                EffectMagnitude    = 3
            },

            ["beam_weapon_specialist"] = new FeatDefinition
            {
                Id          = "beam_weapon_specialist",
                Name        = "Beam Weapon Specialist",
                Description = "+4 to attack rolls with beam weapons. Beam attacks may ignore 2 points of enemy shielding.",
                RequiredSkillRanks = new() { [Constants.Skills.Combat] = 3 },
                RequiredLevel      = 3,
                AvailableToRoles   = new() { Constants.Crew.Roles.WeaponsOfficer, Constants.Crew.Roles.Captain },
                EffectType         = "beam_weapon_bonus",
                EffectMagnitude    = 4
            },

            ["miracle_worker"] = new FeatDefinition
            {
                Id          = "miracle_worker",
                Name        = "Miracle Worker",
                Description = "Once per session, automatically succeed on one Engineering check to repair a critical system.",
                RequiredSkillRanks = new() { [Constants.Skills.Engineering] = 5 },
                RequiredLevel      = 5,
                AvailableToRoles   = new() { Constants.Crew.Roles.Engineer },
                EffectType         = "auto_repair",
                EffectMagnitude    = 1
            },

            ["field_medic"] = new FeatDefinition
            {
                Id          = "field_medic",
                Name        = "Field Medic",
                Description = "Can treat injuries without a medical bay. +2 to Medicine checks in the field.",
                RequiredSkillRanks = new() { [Constants.Skills.Medicine] = 3 },
                RequiredLevel      = 3,
                AvailableToRoles   = new() { Constants.Crew.Roles.Doctor, Constants.Crew.Roles.Captain },
                EffectType         = "field_medicine_bonus",
                EffectMagnitude    = 2
            },

            ["xeno_linguist"] = new FeatDefinition
            {
                Id          = "xeno_linguist",
                Name        = "Xeno-Linguist",
                Description = "+3 to Diplomacy checks with alien species. May attempt first contact without the standard penalty.",
                RequiredSkillRanks = new() { [Constants.Skills.Diplomacy] = 3, [Constants.Skills.Science] = 2 },
                RequiredLevel      = 4,
                EffectType         = "diplomacy_alien_bonus",
                EffectMagnitude    = 3
            },

            ["veteran_explorer"] = new FeatDefinition
            {
                Id          = "veteran_explorer",
                Name        = "Veteran Explorer",
                Description = "+2 to Survival and Navigation. Advantage on checks to spot hidden dangers in unexplored environments.",
                RequiredSkillRanks = new() { [Constants.Skills.Survival] = 3, [Constants.Skills.Navigation] = 2 },
                RequiredLevel      = 4,
                EffectType         = "exploration_bonus",
                EffectMagnitude    = 2
            },

            ["systems_hacker"] = new FeatDefinition
            {
                Id          = "systems_hacker",
                Name        = "Systems Hacker",
                Description = "+4 to Electronics when interfacing with alien or hostile systems. Can attempt to disable enemy systems mid-combat.",
                RequiredSkillRanks = new() { [Constants.Skills.Electronics] = 4 },
                RequiredLevel      = 4,
                EffectType         = "hacking_bonus",
                EffectMagnitude    = 4
            },

            ["iron_constitution"] = new FeatDefinition
            {
                Id          = "iron_constitution",
                Name        = "Iron Constitution",
                Description = "+4 max HP. Recovers from injuries in half the normal time.",
                RequiredLevel = 2,
                EffectType    = "hp_bonus",
                EffectMagnitude = 4
            },

            ["quick_draw"] = new FeatDefinition
            {
                Id          = "quick_draw",
                Name        = "Quick Draw",
                Description = "Always acts first in personal combat. +2 to Combat on the opening round.",
                RequiredSkillRanks = new() { [Constants.Skills.Combat] = 2 },
                RequiredLevel = 2,
                EffectType    = "initiative_bonus",
                EffectMagnitude = 2
            }
        };
    }
}
