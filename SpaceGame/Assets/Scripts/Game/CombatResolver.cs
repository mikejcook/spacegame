using UnityEngine;

/// <summary>
/// Resolves one combat action: an attack roll, damage roll, and DR application.
///
/// ── Attack resolution ────────────────────────────────────────────────────
///
///   Attacker:  d20 + weapon skill + weapon tier bonus + ManeuverAttackBonus
///   Defense:   10  + pilot skill + shield defense rating (shields up)  + ManeuverDefenseBonus
///                              OR armor defense rating  (shields down) + ManeuverDefenseBonus
///
///   No defender roll — flat DC makes hitting more consistent and combat faster.
///   Pilot skill still adds directly, so a skilled pilot is a meaningful defensive asset.
///   ManeuverAttackBonus / ManeuverDefenseBonus come from the pilot maneuver dropdown:
///     Standard          → both 0
///     Evasive Maneuvers → attack −2, defense +2
///     Attack Pattern    → attack +2, defense −2
///   A tie goes to the attacker (≥ is a hit).
///   There is no automatic success on a natural 20 — just a high roll.
///
/// ── Weapon tier bonus ────────────────────────────────────────────────────
///
///   Mk I = +0, Mk II = +1 … Mk VI = +5  (applies to both attack and damage)
///
/// ── Damage ───────────────────────────────────────────────────────────────
///
///   particle cannons  : 2d6 + tier bonus
///   Torpedoes     : 3d6 + tier bonus
///
/// ── DR (Damage Reduction) ────────────────────────────────────────────────
///
///   Particle cannons vs active shields   : no DR — particle cannons are efficient at stripping shields
///   Torpedoes vs active shields: DR 10 — shields strongly resist torpedoes
///   Particle cannons vs hull             : DR 3  — armor resists particle cannons
///   Torpedoes vs hull         : no DR — torpedoes crack hull effectively
///
///   While shields are up, hull takes zero damage regardless of weapon type.
///   Only after shields reach 0 does hull come under fire.
///
/// ── Enemy attacks ────────────────────────────────────────────────────────
///
///   Enemies randomly choose particle cannon or torpedo each round.
///   The same DR rules apply symmetrically against the player.
///
/// </summary>
public static class CombatResolver
{
    // ── DR constants (tunable) ────────────────────────────────────────────────

    /// <summary>
    /// How much torpedo damage shields absorb. High by design — torpedoes are
    /// the wrong tool against active shields; use particle cannons to strip them first.
    /// </summary>
    public const int TorpedoVsShieldDR = 10;

    /// <summary>
    /// How much particle cannon damage hull armor absorbs. Low enough that particle cannons still
    /// threaten hull, but torpedoes are clearly the better hull-cracker.
    /// </summary>
    public const int ParticleCannonVsHullDR = 2;

    /// <summary>DC for the engineer's end-of-turn repair check (d20 + Engineering).</summary>
    public const int RepairDC = 12;

    // ── Attack/damage result ──────────────────────────────────────────────────

    public struct AttackResult
    {
        /// <summary>Raw d20 the attacker rolled.</summary>
        public int  AttackerRoll;
        /// <summary>Attacker's full total (roll + skill + tier bonus).</summary>
        public int  AttackerTotal;
        /// <summary>Flat defense value (10 + defense rating). No roll — DefenderRoll is always 0.</summary>
        public int  DefenderRoll;
        /// <summary>Defender's flat DC (10 + defense rating).</summary>
        public int  DefenderTotal;
        /// <summary>True when AttackerTotal >= DefenderTotal.</summary>
        public bool IsHit;
        /// <summary>Damage before DR.</summary>
        public int  RawDamage;
        /// <summary>DR applied to shields (0 for particle cannons vs shields, TorpedoVsShieldDR for torpedoes vs shields).</summary>
        public int  DR;
        /// <summary>Damage actually dealt to the primary target (shields or hull).</summary>
        public int  FinalDamage;
        /// <summary>True = shields took the hit; false = hull took it.</summary>
        public bool HitShields;
        /// <summary>True if this hit zeroed the target's shields (overflow may follow).</summary>
        public bool ShieldsBroken;
        /// <summary>Hull damage from overflow when shields were broken (after hull DR).</summary>
        public int  HullOverflow;
        /// <summary>True = particle cannons; false = torpedo. Used by the view to pick the correct effect.</summary>
        public bool IsParticleCannonAttack;
        /// <summary>Human-readable description for a combat log.</summary>
        public string Description;
    }

    // ── Engineer repair result ────────────────────────────────────────────────

    public struct EngineerRepairResult
    {
        /// <summary>False when no engineer is aboard — no check attempted.</summary>
        public bool Attempted;
        public int  Roll;
        public int  EngineerSkill;
        public int  Total;
        public int  DC;
        public bool Success;
        public int  ShieldsRepaired;
        public int  HullRepaired;
        /// <summary>Human-readable description for the combat log.</summary>
        public string Description;
    }

    // ── Player fires ──────────────────────────────────────────────────────────

    /// <summary>
    /// Player fires particle cannons at the current target.
    /// Returns a fully-populated AttackResult and modifies the target's HP in place.
    /// </summary>
    public static AttackResult PlayerFireParticleCannon(CombatState state, EnemyCombatState target)
    {
        if (state.PlayerParticleCannonTier <= 0)
            return NoWeapon("particle cannons");

        var r = new AttackResult();

        // Attack roll
        int skill       = GetPlayerWeaponSkill(state, isParticleCannon: true);
        int tierBonus   = TierBonus(state.PlayerParticleCannonTier);
        r.AttackerRoll  = DiceRoller.D20();
        r.AttackerTotal = r.AttackerRoll + skill + tierBonus + state.ManeuverAttackBonus
                          + state.Research.WeaponAttackBonus;   // combat_1 Predictive Targeting

        // Flat defense DC — pilot skill + defense rating, no roll
        int pilotSkill     = GetEnemyPilotSkill(target);
        int defenseBonus   = target.ShieldsUp ? target.Config.ShieldTier : target.Config.ArmorTier;
        r.DefenderRoll     = 0;
        r.DefenderTotal    = 10 + pilotSkill + defenseBonus;

        r.IsParticleCannonAttack = true;
        r.IsHit = r.AttackerTotal >= r.DefenderTotal;

        if (!r.IsHit)
        {
            r.Description = $"Particle cannon misses! ({r.AttackerTotal} vs {r.DefenderTotal})";
            return r;
        }

        r.RawDamage = DiceRoller.D6() + DiceRoller.D6() + TierBonus(state.PlayerParticleCannonTier);

        // combat_2a Coherence Amplifier reduces the hull DR particle cannons must overcome.
        int hullDR = Mathf.Max(0, ParticleCannonVsHullDR - state.Research.ParticleCannonHullPenetration);

        if (target.ShieldsUp)
        {
            // Particle cannons are efficient against shields — no DR
            r.DR        = 0;
            r.HitShields = true;
        }
        else
        {
            // Hull armor resists particle cannons (reduced by penetration research)
            r.DR        = hullDR;
            r.HitShields = false;
        }

        r.FinalDamage = Mathf.Max(0, r.RawDamage - r.DR);

        bool hadShields = target.ShieldsUp;
        (_, r.HullOverflow) = ApplyDamageToEnemy(target, r.FinalDamage, r.HitShields,
                                                  hullDR: hullDR);
        r.ShieldsBroken = hadShields && !target.ShieldsUp;

        string where = r.HitShields ? "shields" : "hull";
        r.Description = BuildHitDescription("Particle cannon", where, r);
        return r;
    }

    /// <summary>
    /// Player fires torpedoes at the current target.
    /// </summary>
    public static AttackResult PlayerFireTorpedo(CombatState state, EnemyCombatState target)
    {
        if (state.PlayerTorpedoTier <= 0)
            return NoWeapon("torpedoes");
        if (state.PlayerTorpedoCount <= 0)
            return NoWeapon("torpedoes (none remaining)");

        var r = new AttackResult();

        int skill       = GetPlayerWeaponSkill(state, isParticleCannon: false);
        int tierBonus   = TierBonus(state.PlayerTorpedoTier);
        r.AttackerRoll  = DiceRoller.D20();
        r.AttackerTotal = r.AttackerRoll + skill + tierBonus + state.ManeuverAttackBonus
                          + state.Research.WeaponAttackBonus;   // combat_1 Predictive Targeting

        // Flat defense DC — pilot skill + defense rating, no roll
        int pilotSkill   = GetEnemyPilotSkill(target);
        int defenseBonus = target.ShieldsUp ? target.Config.ShieldTier : target.Config.ArmorTier;
        r.DefenderRoll   = 0;
        r.DefenderTotal  = 10 + pilotSkill + defenseBonus;

        r.IsParticleCannonAttack = false;
        r.IsHit = r.AttackerTotal >= r.DefenderTotal;

        // Consume one torpedo regardless of hit or miss.
        state.PlayerTorpedoCount = Mathf.Max(0, state.PlayerTorpedoCount - 1);

        if (!r.IsHit)
        {
            r.Description = $"Torpedo misses! ({r.AttackerTotal} vs {r.DefenderTotal})";
            return r;
        }

        r.RawDamage = DiceRoller.D6() + DiceRoller.D6() + DiceRoller.D6() + TierBonus(state.PlayerTorpedoTier);

        if (target.ShieldsUp)
        {
            // Shields strongly resist torpedoes — combat_2b Shaped Charge Warheads cuts into that DR.
            r.DR         = Mathf.Max(0, TorpedoVsShieldDR - state.Research.TorpedoShieldPenetration);
            r.HitShields = true;
        }
        else
        {
            // Hull has no DR vs torpedoes
            r.DR         = 0;
            r.HitShields = false;
        }

        r.FinalDamage = Mathf.Max(0, r.RawDamage - r.DR);

        bool hadShields = target.ShieldsUp;
        (_, r.HullOverflow) = ApplyDamageToEnemy(target, r.FinalDamage, r.HitShields,
                                                  hullDR: 0);  // torpedoes crack hull — no hull DR
        r.ShieldsBroken = hadShields && !target.ShieldsUp;

        string where = r.HitShields ? "shields" : "hull";
        r.Description = BuildHitDescription("Torpedo", where, r);
        return r;
    }

    // ── Enemy fires ───────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve a single enemy ship's attack against the player.
    /// The enemy picks particle cannon or torpedo randomly (50/50).
    /// Modifies CombatState HP in place.
    /// </summary>
    public static AttackResult EnemyAttack(CombatState state, EnemyCombatState enemy)
    {
        // Always use particle cannons while player shields are up — particle cannons strip shields with no DR,
        // while torpedoes are strongly resisted (DR 10) making them wasteful.
        // Once shields are down, always use torpedoes to crack the hull.
        bool useParticleCannon = state.PlayerShieldsUp;
        int  enemyTier   = useParticleCannon ? enemy.Config.ParticleCannonTier : enemy.Config.TorpedoTier;
        string weaponName = useParticleCannon ? "particle cannon" : "torpedo";

        var r = new AttackResult();

        // Enemy attack roll
        r.AttackerRoll  = DiceRoller.D20();
        r.AttackerTotal = r.AttackerRoll + enemy.Config.WeaponSkill + TierBonus(enemyTier);

        // Flat defense DC — pilot skill + defense rating + maneuver bonus, no roll
        int pilotSkill   = GetPlayerPilotSkill(state);
        int defenseBonus = state.PlayerShieldsUp ? state.PlayerShieldDefenseRating : state.PlayerArmorDefenseRating;
        // Research defense: prop_3b adds vs all; shields_1/2 add while shields are up.
        int researchDefense = state.Research.DefenseBonusVsAll
                            + (state.PlayerShieldsUp ? state.Research.ShieldDefenseBonus : 0);
        r.DefenderRoll   = 0;
        r.DefenderTotal  = 10 + pilotSkill + defenseBonus + state.ManeuverDefenseBonus
                           + researchDefense + state.EmergencyThrustDefenseBonus;

        r.IsParticleCannonAttack = useParticleCannon;
        r.IsHit = r.AttackerTotal >= r.DefenderTotal;

        if (!r.IsHit)
        {
            r.Description = $"Enemy {weaponName} misses! ({r.AttackerTotal} vs {r.DefenderTotal})";
            return r;
        }

        // Damage
        if (useParticleCannon)
        {
            r.RawDamage = DiceRoller.D6() + DiceRoller.D6() + TierBonus(enemyTier);
            r.DR        = state.PlayerShieldsUp ? 0 : ParticleCannonVsHullDR;
            r.HitShields = state.PlayerShieldsUp;
        }
        else
        {
            r.RawDamage  = DiceRoller.D6() + DiceRoller.D6() + DiceRoller.D6() + TierBonus(enemyTier);
            r.DR         = state.PlayerShieldsUp ? TorpedoVsShieldDR : 0;
            r.HitShields = state.PlayerShieldsUp;
        }

        // Research damage reduction: shields_3 (Tessellated Barrier) while shields up,
        // armor_1/2/3 (plating) while shields are down.
        int researchDR = state.PlayerShieldsUp
            ? state.Research.ShieldDamageReduction
            : state.Research.ArmorDamageReduction;
        r.FinalDamage = Mathf.Max(0, r.RawDamage - r.DR - researchDR);

        // Chance to ignore the hit entirely: shields_5 (Phase-Inversion) while shields up,
        // armor_5 (Null-Impact) while shields down.
        float ignoreChance = state.PlayerShieldsUp
            ? state.Research.ShieldIgnoreChance
            : state.Research.ArmorIgnoreChance;
        if (ignoreChance > 0f && Random.value < ignoreChance)
        {
            r.FinalDamage = 0;
            string layer = state.PlayerShieldsUp ? "Shields phase the hit" : "Armor negates the hit";
            r.Description = $"{layer} — Enemy {weaponName} deals no damage! ({r.AttackerTotal} vs {r.DefenderTotal})";
            return r;
        }

        bool playerHadShields = state.PlayerShieldsUp;
        int  hullDR           = useParticleCannon ? ParticleCannonVsHullDR : 0;
        (_, r.HullOverflow) = ApplyDamageToPlayer(state, r.FinalDamage, r.HitShields, hullDR);
        r.ShieldsBroken = playerHadShields && !state.PlayerShieldsUp;

        string where = r.HitShields ? "your shields" : "your hull";
        r.Description = BuildHitDescription($"Enemy {weaponName}", where, r);
        return r;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Tier attack/damage bonus: Mk I = +0, Mk II = +1 … Mk VI = +5.</summary>
    public static int TierBonus(int tier) => Mathf.Max(0, tier - 1);

    /// <summary>
    /// Apply damage to an enemy ship.
    /// If shields are hit and finalDamage exceeds remaining shields, overflow
    /// (minus hullDR) bleeds through to hull.
    /// Returns (shieldDamageDealt, hullOverflowDealt).
    /// </summary>
    private static (int shieldDmg, int hullOverflow) ApplyDamageToEnemy(
        EnemyCombatState target, int finalDamage, bool hitShields, int hullDR = 0)
    {
        if (!hitShields)
        {
            // Direct hull hit — finalDamage already has hull DR applied.
            // Return (0, 0): hull damage is captured in r.FinalDamage, not an overflow.
            target.CurrentHull = Mathf.Max(0, target.CurrentHull - finalDamage);
            return (0, 0);
        }

        int shieldDmg = Mathf.Min(target.CurrentShields, finalDamage);
        target.CurrentShields = Mathf.Max(0, target.CurrentShields - finalDamage);

        int rawOverflow = finalDamage - shieldDmg;
        if (rawOverflow <= 0) return (shieldDmg, 0);

        int hullOverflow = Mathf.Max(0, rawOverflow - hullDR);
        target.CurrentHull = Mathf.Max(0, target.CurrentHull - hullOverflow);
        return (shieldDmg, hullOverflow);
    }

    /// <summary>
    /// Apply damage to the player ship.
    /// If shields are hit and finalDamage exceeds remaining shields, overflow
    /// (minus hullDR) bleeds through to hull.
    /// Returns (shieldDamageDealt, hullOverflowDealt).
    /// </summary>
    private static (int shieldDmg, int hullOverflow) ApplyDamageToPlayer(
        CombatState state, int finalDamage, bool hitShields, int hullDR = 0)
    {
        if (!hitShields)
        {
            // Direct hull hit — finalDamage already has hull DR applied.
            // Return (0, 0): hull damage is captured in r.FinalDamage, not an overflow.
            state.PlayerCurrentHull = Mathf.Max(0, state.PlayerCurrentHull - finalDamage);
            return (0, 0);
        }

        int shieldDmg = Mathf.Min(state.PlayerCurrentShields, finalDamage);
        state.PlayerCurrentShields = Mathf.Max(0, state.PlayerCurrentShields - finalDamage);

        int rawOverflow = finalDamage - shieldDmg;
        if (rawOverflow <= 0) return (shieldDmg, 0);

        int hullOverflow = Mathf.Max(0, rawOverflow - hullDR);
        state.PlayerCurrentHull = Mathf.Max(0, state.PlayerCurrentHull - hullOverflow);
        return (shieldDmg, hullOverflow);
    }

    // ── Shield regen (end of player turn, before engineer repair) ────────────

    public struct ShieldRegenResult
    {
        /// <summary>False when shields are not installed or already full.</summary>
        public bool Applied;
        public int  Amount;
        public bool Researched;
        public string Description;
    }

    /// <summary>
    /// Passive shield regeneration at the end of the player's turn.
    /// Base: 1 pt per shield tier. Regenerative Field (shields_4) increases it by 50%, min 1.
    /// Only applies while shields are installed and below maximum.
    /// </summary>
    public static ShieldRegenResult ShieldRegen(CombatState state)
    {
        if (state.PlayerShieldTier <= 0 || state.PlayerCurrentShields >= state.PlayerMaxShields)
            return new ShieldRegenResult { Description = "" };

        int   basePts    = state.PlayerShieldTier;
        bool  researched = state.Research.ShieldRegenResearched > 0;
        int   amount     = researched
            ? Mathf.Max(1, Mathf.RoundToInt(basePts * Constants.Research.Effects.RegenerativeFieldRegenMultiplier))
            : basePts;

        state.PlayerCurrentShields = Mathf.Min(state.PlayerMaxShields, state.PlayerCurrentShields + amount);

        string bonus = researched ? " (Regenerative Field)" : "";
        return new ShieldRegenResult
        {
            Applied    = true,
            Amount     = amount,
            Researched = researched,
            Description = $"Shields regenerate {amount} pts{bonus}.",
        };
    }

    // ── Engineer repair ───────────────────────────────────────────────────────

    /// <summary>
    /// Engineer rolls d20 + Engineering skill vs DC <see cref="RepairDC"/>.
    /// On success, repairs d6 + (Engineering/2) points — shields first, then hull.
    /// Call once at the end of the enemy turn (before re-enabling player buttons).
    /// Returns a fully-populated <see cref="EngineerRepairResult"/>.
    /// </summary>
    public static EngineerRepairResult EngineerRepair(CombatState state)
    {
        if (state.Engineer == null)
            return new EngineerRepairResult
            {
                Description = "No engineer aboard — no repairs this turn."
            };

        var r = new EngineerRepairResult { Attempted = true };
        r.EngineerSkill = state.Engineer.GetSkillRank(Constants.Skills.Engineering);
        r.Roll          = DiceRoller.D20();
        r.Total         = r.Roll + r.EngineerSkill;
        r.DC            = RepairDC;

        if (r.Total < r.DC)
        {
            r.Success     = false;
            r.Description = $"Engineer repair check fails — {r.Total} vs DC {r.DC}.";
            return r;
        }

        r.Success = true;
        int pool  = DiceRoller.D6() + r.EngineerSkill / 2;

        // Repair shields first (if damaged), then hull with any remainder.
        if (state.PlayerCurrentShields < state.PlayerMaxShields)
        {
            r.ShieldsRepaired         = Mathf.Min(pool, state.PlayerMaxShields - state.PlayerCurrentShields);
            state.PlayerCurrentShields += r.ShieldsRepaired;
            pool                      -= r.ShieldsRepaired;
        }
        if (pool > 0 && state.PlayerCurrentHull < state.PlayerMaxHull)
        {
            r.HullRepaired         = Mathf.Min(pool, state.PlayerMaxHull - state.PlayerCurrentHull);
            state.PlayerCurrentHull += r.HullRepaired;
        }

        string what = (r.ShieldsRepaired > 0 && r.HullRepaired > 0)
            ? $"shields +{r.ShieldsRepaired}, hull +{r.HullRepaired}"
            : r.ShieldsRepaired > 0 ? $"shields +{r.ShieldsRepaired}"
            : r.HullRepaired    > 0 ? $"hull +{r.HullRepaired}"
            : "nothing needed repairs";

        r.Description = $"Engineer repairs {what}! (roll {r.Total} vs DC {r.DC})";
        return r;
    }

    private static int GetPlayerWeaponSkill(CombatState state, bool isParticleCannon)
    {
        if (state.Gunner != null)
            return state.Gunner.GetSkillRank(Constants.Skills.Gunnery);

        var captain = state.Captain;
        if (captain == null) return 0;

        int gunnery  = captain.GetSkillRank(Constants.Skills.Gunnery);
        int command  = Mathf.Max(1, captain.GetSkillRank(Constants.Skills.Command) / 2);
        return Mathf.Max(gunnery, command);
    }

    private static int GetPlayerPilotSkill(CombatState state)
    {
        if (state.Pilot != null)
            return state.Pilot.GetSkillRank(Constants.Skills.Piloting);

        var captain = state.Captain;
        if (captain == null) return 0;

        int gunnery = captain.GetSkillRank(Constants.Skills.Piloting);
        int command = Mathf.Max(1, captain.GetSkillRank(Constants.Skills.Command) / 2);
        return Mathf.Max(gunnery, command);
    }

    private static int GetEnemyPilotSkill(EnemyCombatState enemy)
        => enemy.Config?.PilotSkill ?? 1;

    private static AttackResult NoWeapon(string weaponName)
        => new AttackResult { Description = $"No {weaponName} installed!" };

    private static string BuildHitDescription(string weapon, string where, in AttackResult r)
    {
        string drPart       = r.DR > 0 ? $" − DR {r.DR}" : "";
        string overflowPart = r.HullOverflow > 0 ? $" + hull {r.HullOverflow}" : "";
        string shieldBreak  = r.ShieldsBroken ? " — Shields Down!" : "";
        return $"{weapon} hits {where} for {r.FinalDamage}{overflowPart}!{shieldBreak} " +
               $"(attack {r.AttackerTotal} vs defense {r.DefenderTotal}, " +
               $"raw {r.RawDamage}{drPart})";
    }
}
