using System.Collections.Generic;
using static Constants.Research.Effects;

/// <summary>
/// Combat modifiers derived from the player's unlocked research. Built once at the
/// start of each battle (see CombatState.Begin) and read by CombatResolver.
///
/// All magnitudes come from <see cref="Constants.Research.Effects"/> so they can be
/// tuned in one place. Fields are grouped by where they apply in the resolver.
/// </summary>
public struct CombatResearchModifiers
{
    // ── Player offense ───────────────────────────────────────────────────────
    /// <summary>Added to the player's to-hit total for every weapon (combat_1).</summary>
    public int WeaponAttackBonus;
    /// <summary>Reduces the DR the enemy's hull applies to particle cannons (combat_2a).</summary>
    public int ParticleCannonHullPenetration;
    /// <summary>Reduces the DR the enemy's shields apply to torpedoes (combat_2b).</summary>
    public int TorpedoShieldPenetration;
    /// <summary>Chance per particle-cannon hit to lower the enemy's shield defense (combat_3a). Framework.</summary>
    public float ShieldDisruptionChance;
    /// <summary>Magnitude of the shield-defense reduction on a successful disruption (combat_3a). Framework.</summary>
    public int ShieldDisruptionAmount;
    /// <summary>Multiplier applied to the starting torpedo magazine (combat_3b).</summary>
    public float TorpedoCountMultiplier;

    // ── Player defense ───────────────────────────────────────────────────────
    /// <summary>Added to the player's defense DC against all incoming fire (prop_3b).</summary>
    public int DefenseBonusVsAll;
    /// <summary>Added to the player's defense DC while shields are up (shields_1/2).</summary>
    public int ShieldDefenseBonus;
    /// <summary>Flat damage reduction on incoming hits while shields are up (shields_3).</summary>
    public int ShieldDamageReduction;
    /// <summary>Flat damage reduction on incoming hits while shields are down (armor_1/2/3).</summary>
    public int ArmorDamageReduction;
    /// <summary>Chance to ignore an incoming hit entirely while shields are up (shields_5).</summary>
    public float ShieldIgnoreChance;
    /// <summary>Chance to ignore an incoming hit entirely while shields are down (armor_5).</summary>
    public float ArmorIgnoreChance;

    // ── Per-turn passives ─────────────────────────────────────────────────────
    /// <summary>True (1) when Regenerative Field (shields_4) is unlocked. Used by CombatResolver.ShieldRegen.</summary>
    public int ShieldRegenResearched;
    /// <summary>Hull regenerated at the end of each combat turn (armor_4). Framework.</summary>
    public int HullRegenPerTurn;

    // ── Active abilities (framework — flags only, need combat-view UI) ─────────
    public bool HasEmergencyThrust;       // prop_3a
    public bool HasOverchargeCapacitors;  // combat_4
    public bool HasSplitFire;             // combat_5
}

/// <summary>
/// Central translator from "which research is unlocked" to concrete gameplay effects.
/// Combat effects are returned as a <see cref="CombatResearchModifiers"/>; equipment-tier
/// gating and skill-point bonuses live on GameManager (which owns the save state).
/// </summary>
public static class ResearchEffects
{
    /// <summary>
    /// Builds the combat modifier bundle from a set of unlocked research IDs.
    /// Pure — safe to call with a null/empty set (returns identity modifiers).
    /// </summary>
    public static CombatResearchModifiers GetCombatModifiers(HashSet<string> unlocked)
    {
        var m = new CombatResearchModifiers { TorpedoCountMultiplier = 1f };

        bool U(string id) => unlocked != null && unlocked.Contains(id);

        // ── Offense ──────────────────────────────────────────────────────────
        if (U(Constants.Research.Ids.PredictiveTargeting))
            m.WeaponAttackBonus += PredictiveTargetingAttackBonus;
        if (U(Constants.Research.Ids.CoherenceAmplifier))
            m.ParticleCannonHullPenetration += CoherenceAmplifierHullPenetration;
        if (U(Constants.Research.Ids.ShapedChargeWarheads))
            m.TorpedoShieldPenetration += ShapedChargeShieldPenetration;
        if (U(Constants.Research.Ids.ShieldDisruption))
        {
            m.ShieldDisruptionChance = ShieldDisruptionChance;
            m.ShieldDisruptionAmount = ShieldDisruptionAmount;
        }
        if (U(Constants.Research.Ids.WarheadMiniaturization))
            m.TorpedoCountMultiplier *= WarheadMiniaturizationTorpedoMult;

        // ── Defense — shields ────────────────────────────────────────────────
        if (U(Constants.Research.Ids.HarmonicShields))
            m.ShieldDefenseBonus += HarmonicShieldsDefense;
        if (U(Constants.Research.Ids.AdaptiveFrequencies))
            m.ShieldDefenseBonus += AdaptiveFrequenciesDefense;
        if (U(Constants.Research.Ids.TessellatedBarrier))
            m.ShieldDamageReduction += TessellatedBarrierDR;
        if (U(Constants.Research.Ids.RegenerativeField))
            m.ShieldRegenResearched = 1;
        if (U(Constants.Research.Ids.PhaseInversion))
            m.ShieldIgnoreChance = PhaseInversionIgnoreChance;

        // ── Defense — armor / hull ───────────────────────────────────────────
        if (U(Constants.Research.Ids.ReactivePlating))
            m.ArmorDamageReduction += ReactivePlatingDR;
        if (U(Constants.Research.Ids.AdaptiveComposite))
            m.ArmorDamageReduction += AdaptiveCompositeDR;
        if (U(Constants.Research.Ids.AblativePlating))
            m.ArmorDamageReduction += AblativePlatingDR;
        if (U(Constants.Research.Ids.RegenerativeHull))
            m.HullRegenPerTurn += RegenerativeHullPerTurn;
        if (U(Constants.Research.Ids.NullImpactArmor))
            m.ArmorIgnoreChance = NullImpactIgnoreChance;

        // ── Defense — propulsion ─────────────────────────────────────────────
        if (U(Constants.Research.Ids.ManeuveringThrusters))
            m.DefenseBonusVsAll += ManeuveringThrustersDefense;

        // ── Active abilities (flags only) ─────────────────────────────────────
        m.HasEmergencyThrust      = U(Constants.Research.Ids.EmergencyThrust);
        m.HasOverchargeCapacitors = U(Constants.Research.Ids.OverchargeCapacitors);
        m.HasSplitFire            = U(Constants.Research.Ids.SplitFire);

        return m;
    }
}
