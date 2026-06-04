using UnityEngine;

/// <summary>
/// Resolves one combat action: an attack roll, damage roll, and DR application.
///
/// ── Attack resolution (opposed rolls) ────────────────────────────────────
///
///   Attacker:  d20 + weapon skill + weapon tier bonus
///   Defender:  d20 + pilot skill  + shield defense rating (shields up)
///                                 OR armor defense rating (shields down)
///
///   A tie goes to the attacker (≥ is a hit).
///   There is no automatic success on a natural 20 — just a high roll.
///
/// ── Weapon tier bonus ────────────────────────────────────────────────────
///
///   Mk I = +0, Mk II = +1 … Mk VI = +5  (applies to both attack and damage)
///
/// ── Damage ───────────────────────────────────────────────────────────────
///
///   Beam weapons  : 2d6 + tier bonus
///   Torpedoes     : 3d6 + tier bonus
///
/// ── DR (Damage Reduction) ────────────────────────────────────────────────
///
///   Beams vs active shields   : no DR — beams are efficient at stripping shields
///   Torpedoes vs active shields: DR 10 — shields strongly resist torpedoes
///   Beams vs hull             : DR 3  — armor resists beam weapons
///   Torpedoes vs hull         : no DR — torpedoes crack hull effectively
///
///   While shields are up, hull takes zero damage regardless of weapon type.
///   Only after shields reach 0 does hull come under fire.
///
/// ── Enemy attacks ────────────────────────────────────────────────────────
///
///   Enemies randomly choose beam or torpedo each round.
///   The same DR rules apply symmetrically against the player.
///
/// </summary>
public static class CombatResolver
{
    // ── DR constants (tunable) ────────────────────────────────────────────────

    /// <summary>
    /// How much torpedo damage shields absorb. High by design — torpedoes are
    /// the wrong tool against active shields; use beams to strip them first.
    /// </summary>
    public const int TorpedoVsShieldDR = 10;

    /// <summary>
    /// How much beam damage hull armor absorbs. Low enough that beams still
    /// threaten hull, but torpedoes are clearly the better hull-cracker.
    /// </summary>
    public const int BeamVsHullDR = 3;

    // ── Attack/damage result ──────────────────────────────────────────────────

    public struct AttackResult
    {
        /// <summary>Raw d20 the attacker rolled.</summary>
        public int  AttackerRoll;
        /// <summary>Attacker's full total (roll + skill + tier bonus).</summary>
        public int  AttackerTotal;
        /// <summary>Raw d20 the defender rolled.</summary>
        public int  DefenderRoll;
        /// <summary>Defender's full total (roll + pilot + defense rating).</summary>
        public int  DefenderTotal;
        /// <summary>True when AttackerTotal >= DefenderTotal.</summary>
        public bool IsHit;
        /// <summary>Damage before DR.</summary>
        public int  RawDamage;
        /// <summary>DR applied (0 when shields absorb nothing for beams, or when hull is unarmored vs torpedoes).</summary>
        public int  DR;
        /// <summary>Damage actually dealt (max(0, RawDamage - DR)).</summary>
        public int  FinalDamage;
        /// <summary>True = shields took the hit; false = hull took it.</summary>
        public bool HitShields;
        /// <summary>True = beam weapon; false = torpedo. Used by the view to pick the correct effect.</summary>
        public bool IsBeamAttack;
        /// <summary>Human-readable description for a combat log.</summary>
        public string Description;
    }

    // ── Player fires ──────────────────────────────────────────────────────────

    /// <summary>
    /// Player fires beam weapons at the current target.
    /// Returns a fully-populated AttackResult and modifies the target's HP in place.
    /// </summary>
    public static AttackResult PlayerFireBeam(CombatState state, EnemyCombatState target)
    {
        if (state.PlayerBeamTier <= 0)
            return NoWeapon("beam weapons");

        var r = new AttackResult();

        // Attack roll
        int skill       = GetPlayerWeaponSkill(state, isBeam: true);
        int tierBonus   = TierBonus(state.PlayerBeamTier);
        r.AttackerRoll  = DiceRoller.D20();
        r.AttackerTotal = r.AttackerRoll + skill + tierBonus;

        // Defense roll
        int pilotSkill     = GetEnemyPilotSkill(target);
        int defenseBonus   = target.ShieldsUp ? target.Config.ShieldDefenseRating : target.Config.ArmorDefenseRating;
        r.DefenderRoll     = DiceRoller.D20();
        r.DefenderTotal    = r.DefenderRoll + pilotSkill + defenseBonus;

        r.IsBeamAttack = true;
        r.IsHit = r.AttackerTotal >= r.DefenderTotal;

        if (!r.IsHit)
        {
            r.Description = $"Beam attack misses! ({r.AttackerTotal} vs {r.DefenderTotal})";
            return r;
        }

        r.RawDamage = DiceRoller.D6() + DiceRoller.D6() + TierBonus(state.PlayerBeamTier);

        if (target.ShieldsUp)
        {
            // Beams are efficient against shields — no DR
            r.DR        = 0;
            r.HitShields = true;
        }
        else
        {
            // Hull armor resists beams
            r.DR        = BeamVsHullDR;
            r.HitShields = false;
        }

        r.FinalDamage = Mathf.Max(0, r.RawDamage - r.DR);
        ApplyDamageToEnemy(target, r.FinalDamage, r.HitShields);

        string where = r.HitShields ? "shields" : "hull";
        r.Description = BuildHitDescription("Beam", where, r);
        return r;
    }

    /// <summary>
    /// Player fires torpedoes at the current target.
    /// </summary>
    public static AttackResult PlayerFireTorpedo(CombatState state, EnemyCombatState target)
    {
        if (state.PlayerTorpedoTier <= 0)
            return NoWeapon("torpedoes");

        var r = new AttackResult();

        int skill       = GetPlayerWeaponSkill(state, isBeam: false);
        int tierBonus   = TierBonus(state.PlayerTorpedoTier);
        r.AttackerRoll  = DiceRoller.D20();
        r.AttackerTotal = r.AttackerRoll + skill + tierBonus;

        int pilotSkill   = GetEnemyPilotSkill(target);
        int defenseBonus = target.ShieldsUp ? target.Config.ShieldDefenseRating : target.Config.ArmorDefenseRating;
        r.DefenderRoll   = DiceRoller.D20();
        r.DefenderTotal  = r.DefenderRoll + pilotSkill + defenseBonus;

        r.IsBeamAttack = false;
        r.IsHit = r.AttackerTotal >= r.DefenderTotal;

        if (!r.IsHit)
        {
            r.Description = $"Torpedo misses! ({r.AttackerTotal} vs {r.DefenderTotal})";
            return r;
        }

        r.RawDamage = DiceRoller.D6() + DiceRoller.D6() + DiceRoller.D6() + TierBonus(state.PlayerTorpedoTier);

        if (target.ShieldsUp)
        {
            // Shields strongly resist torpedoes
            r.DR         = TorpedoVsShieldDR;
            r.HitShields = true;
        }
        else
        {
            // Hull has no DR vs torpedoes
            r.DR         = 0;
            r.HitShields = false;
        }

        r.FinalDamage = Mathf.Max(0, r.RawDamage - r.DR);
        ApplyDamageToEnemy(target, r.FinalDamage, r.HitShields);

        string where = r.HitShields ? "shields" : "hull";
        r.Description = BuildHitDescription("Torpedo", where, r);
        return r;
    }

    // ── Enemy fires ───────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve a single enemy ship's attack against the player.
    /// The enemy picks beam or torpedo randomly (50/50).
    /// Modifies CombatState HP in place.
    /// </summary>
    public static AttackResult EnemyAttack(CombatState state, EnemyCombatState enemy)
    {
        bool useBeam     = DiceRoller.Roll(2) == 1;
        int  enemyTier   = useBeam ? enemy.Config.BeamTier : enemy.Config.TorpedoTier;
        string weaponName = useBeam ? "beam" : "torpedo";

        var r = new AttackResult();

        // Enemy attack roll
        r.AttackerRoll  = DiceRoller.D20();
        r.AttackerTotal = r.AttackerRoll + enemy.Config.WeaponSkill + TierBonus(enemyTier);

        // Player defense roll
        int pilotSkill   = GetPlayerPilotSkill(state);
        int defenseBonus = state.PlayerShieldsUp ? state.PlayerShieldDefenseRating : state.PlayerArmorDefenseRating;
        r.DefenderRoll   = DiceRoller.D20();
        r.DefenderTotal  = r.DefenderRoll + pilotSkill + defenseBonus;

        r.IsBeamAttack = useBeam;
        r.IsHit = r.AttackerTotal >= r.DefenderTotal;

        if (!r.IsHit)
        {
            r.Description = $"Enemy {weaponName} misses! ({r.AttackerTotal} vs {r.DefenderTotal})";
            return r;
        }

        // Damage
        if (useBeam)
        {
            r.RawDamage = DiceRoller.D6() + DiceRoller.D6() + TierBonus(enemyTier);
            r.DR        = state.PlayerShieldsUp ? 0 : BeamVsHullDR;
            r.HitShields = state.PlayerShieldsUp;
        }
        else
        {
            r.RawDamage  = DiceRoller.D6() + DiceRoller.D6() + DiceRoller.D6() + TierBonus(enemyTier);
            r.DR         = state.PlayerShieldsUp ? TorpedoVsShieldDR : 0;
            r.HitShields = state.PlayerShieldsUp;
        }

        r.FinalDamage = Mathf.Max(0, r.RawDamage - r.DR);
        ApplyDamageToPlayer(state, r.FinalDamage, r.HitShields);

        string where = r.HitShields ? "your shields" : "your hull";
        r.Description = BuildHitDescription($"Enemy {weaponName}", where, r);
        return r;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Tier attack/damage bonus: Mk I = +0, Mk II = +1 … Mk VI = +5.</summary>
    public static int TierBonus(int tier) => Mathf.Max(0, tier - 1);

    private static void ApplyDamageToEnemy(EnemyCombatState target, int damage, bool hitShields)
    {
        if (hitShields)
            target.CurrentShields = Mathf.Max(0, target.CurrentShields - damage);
        else
            target.CurrentHull    = Mathf.Max(0, target.CurrentHull    - damage);
    }

    private static void ApplyDamageToPlayer(CombatState state, int damage, bool hitShields)
    {
        if (hitShields)
            state.PlayerCurrentShields = Mathf.Max(0, state.PlayerCurrentShields - damage);
        else
            state.PlayerCurrentHull    = Mathf.Max(0, state.PlayerCurrentHull    - damage);
    }

    private static int GetPlayerWeaponSkill(CombatState state, bool isBeam)
    {
        string skillName = isBeam ? Constants.Skills.BeamWeapons : Constants.Skills.Torpedoes;
        var    officer   = state.WeaponsOfficer ?? state.Captain;
        return officer?.GetSkillRank(skillName) ?? 0;
    }

    private static int GetPlayerPilotSkill(CombatState state)
    {
        var pilot = state.Pilot ?? state.Captain;
        return pilot?.GetSkillRank(Constants.Skills.Piloting) ?? 0;
    }

    private static int GetEnemyPilotSkill(EnemyCombatState enemy)
        => enemy.Config?.PilotSkill ?? 1;

    private static AttackResult NoWeapon(string weaponName)
        => new AttackResult { Description = $"No {weaponName} installed!" };

    private static string BuildHitDescription(string weapon, string where, in AttackResult r)
    {
        string drPart = r.DR > 0 ? $" − DR {r.DR}" : "";
        return $"{weapon} hits {where} for {r.FinalDamage}! " +
               $"(attack {r.AttackerTotal} vs defense {r.DefenderTotal}, " +
               $"raw {r.RawDamage}{drPart})";
    }
}
