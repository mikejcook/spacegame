using System;

/// <summary>
/// D20-based dice rolling system.
///
/// Rules:
///   - A natural 20 is always an automatic success regardless of DC.
///   - A natural 1 is NOT an automatic failure; it simply produces a very low total.
///   - Success is determined by: rawRoll + skillRank + modifiers >= DC.
///
/// Usage:
///   var result = DiceRoller.RollSkillCheck(character, Constants.Skills.Piloting, Constants.Dice.DC_Hard);
///   if (result.IsSuccess) { ... }
///   Debug.Log(result); // "Roll: 14 + 3 = 17 vs DC 16 -> Success"
/// </summary>
public static class DiceRoller
{
    private static readonly Random _rng = new Random();

    // ---------------------------------------------------------------------------
    // Basic rolls
    // ---------------------------------------------------------------------------

    public static int Roll(int sides) => _rng.Next(1, sides + 1);

    public static int D4()   => Roll(4);
    public static int D6()   => Roll(6);
    public static int D8()   => Roll(8);
    public static int D10()  => Roll(10);
    public static int D12()  => Roll(12);
    public static int D20()  => Roll(20);
    public static int D100() => Roll(100);

    // ---------------------------------------------------------------------------
    // Skill checks (character-based)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Roll a D20 skill check for a character against a difficulty class.
    /// </summary>
    public static RollResult RollSkillCheck(
        Character character,
        string skillName,
        int difficultyClass,
        int extraBonus = 0)
    {
        int raw       = D20();
        int skillRank = character.GetSkillRank(skillName);
        int total     = raw + skillRank + extraBonus;

        bool autoSuccess = raw == Constants.Dice.AutoSuccessRoll;
        bool success     = autoSuccess || total >= difficultyClass;
        int  margin      = total - difficultyClass;

        return new RollResult
        {
            RawRoll         = raw,
            SkillRank       = skillRank,
            BonusModifier   = extraBonus,
            TotalRoll       = total,
            DifficultyClass = difficultyClass,
            IsAutoSuccess   = autoSuccess,
            IsSuccess       = success,
            Degree          = Classify(autoSuccess, margin),
            Margin          = margin,
            SkillName       = skillName,
            CharacterName   = character.Name
        };
    }

    // ---------------------------------------------------------------------------
    // Ship checks (ship class + crew skill combined)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Roll a D20 ship check.
    /// checkType maps to a ship stat and a relevant crew skill.
    /// </summary>
    public static RollResult RollShipCheck(
        Ship ship,
        string checkType,
        int difficultyClass,
        Character operatingCrew = null,
        int extraBonus = 0)
    {
        int raw = D20();

        int shipBonus = checkType switch
        {
            "attack"     => ship.BaseAttackBonus,
            "defense"    => ship.BaseDefenseBonus,
            "sensors"    => ship.BaseSensorRange,
            _            => 0
        };

        int crewBonus = 0;
        if (operatingCrew != null)
        {
            string skill = checkType switch
            {
                "attack"     => Constants.Skills.Gunnery,
                "defense"    => Constants.Skills.Piloting,
                "sensors"    => Constants.Skills.Science,
                "navigation" => Constants.Skills.Piloting,
                _            => Constants.Skills.Command
            };
            crewBonus = operatingCrew.GetSkillRank(skill);
        }

        int  total       = raw + shipBonus + crewBonus + extraBonus;
        bool autoSuccess = raw == Constants.Dice.AutoSuccessRoll;
        bool success     = autoSuccess || total >= difficultyClass;
        int  margin      = total - difficultyClass;

        return new RollResult
        {
            RawRoll         = raw,
            SkillRank       = crewBonus,
            BonusModifier   = shipBonus + extraBonus,
            TotalRoll       = total,
            DifficultyClass = difficultyClass,
            IsAutoSuccess   = autoSuccess,
            IsSuccess       = success,
            Degree          = Classify(autoSuccess, margin),
            Margin          = margin,
            SkillName       = checkType,
            CharacterName   = operatingCrew?.Name ?? "Ship"
        };
    }

    // ---------------------------------------------------------------------------
    // Landing party check (best of up to 3 crew members)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Roll a skill check for a landing party.
    /// The crew member with the highest relevant skill leads the roll,
    /// with a small bonus for each additional party member who also has the skill.
    /// </summary>
    public static RollResult RollLandingPartyCheck(
        System.Collections.Generic.List<Character> party,
        string skillName,
        int difficultyClass,
        int extraBonus = 0)
    {
        if (party == null || party.Count == 0)
            return RollSkillCheck(new Character { FirstName = "Nobody" }, skillName, difficultyClass);

        // Find best-skilled member
        Character lead = party[0];
        int       best = lead.GetSkillRank(skillName);
        foreach (var member in party)
        {
            int rank = member.GetSkillRank(skillName);
            if (rank > best) { best = rank; lead = member; }
        }

        // +1 synergy bonus per additional party member who has any rank in the skill
        int synergy = 0;
        foreach (var member in party)
            if (member != lead && member.GetSkillRank(skillName) > 0) synergy++;

        return RollSkillCheck(lead, skillName, difficultyClass, extraBonus + synergy);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static SuccessDegree Classify(bool autoSuccess, int margin)
    {
        if (autoSuccess || margin >= 10) return SuccessDegree.CriticalSuccess;
        if (margin >= 5)                 return SuccessDegree.GreatSuccess;
        if (margin >= 0)                 return SuccessDegree.Success;
        if (margin >= -4)                return SuccessDegree.Failure;
        return SuccessDegree.CriticalFailure;
    }
}

// ---------------------------------------------------------------------------
// Result types
// ---------------------------------------------------------------------------

public class RollResult
{
    public int    RawRoll         { get; set; }
    public int    SkillRank       { get; set; }
    public int    BonusModifier   { get; set; }
    public int    TotalRoll       { get; set; }
    public int    DifficultyClass { get; set; }
    public bool   IsAutoSuccess   { get; set; }
    public bool   IsSuccess       { get; set; }
    public SuccessDegree Degree   { get; set; }
    public int    Margin          { get; set; }
    public string SkillName       { get; set; }
    public string CharacterName   { get; set; }

    public override string ToString()
    {
        string auto = IsAutoSuccess ? " (Natural 20!)" : "";
        return $"{CharacterName} [{SkillName}]: {RawRoll} + {SkillRank + BonusModifier} = {TotalRoll} vs DC {DifficultyClass} → {Degree}{auto}";
    }
}

public enum SuccessDegree
{
    CriticalFailure,
    Failure,
    Success,
    GreatSuccess,
    CriticalSuccess
}
