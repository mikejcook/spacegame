using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which side gets to act next (or whether combat has ended).
/// </summary>
public enum CombatPhase
{
    PlayerTurn,
    EnemyTurn,
    Victory,
    Defeat,
}

// ---------------------------------------------------------------------------
// Per-enemy live state
// ---------------------------------------------------------------------------

/// <summary>
/// Tracks the live HP for a single enemy ship during a combat encounter.
/// Constructed from an EnemyShipConfig at the start of combat.
/// </summary>
public class EnemyCombatState
{
    public EnemyShipConfig Config          { get; set; }
    public int             MaxShields      { get; set; }
    public int             CurrentShields  { get; set; }
    public int             MaxHull         { get; set; }
    public int             CurrentHull     { get; set; }

    public bool ShieldsUp   => CurrentShields > 0;
    public bool IsDestroyed => CurrentHull    <= 0;

    /// <summary>Convenience: 0–1 fraction for UI bar fills.</summary>
    public float ShieldPct => MaxShields > 0 ? (float)CurrentShields / MaxShields : 0f;
    public float HullPct   => MaxHull    > 0 ? (float)CurrentHull    / MaxHull    : 0f;

    /// <summary>Build from config, initialising HP to full.</summary>
    public static EnemyCombatState FromConfig(EnemyShipConfig config)
    {
        return new EnemyCombatState
        {
            Config         = config,
            MaxShields     = config.MaxShields,
            CurrentShields = config.MaxShields,
            MaxHull        = config.MaxHull,
            CurrentHull    = config.MaxHull,
        };
    }
}

// ---------------------------------------------------------------------------
// Full combat state
// ---------------------------------------------------------------------------

/// <summary>
/// Snapshot of all mutable data for one space battle.
/// Initialise via CombatState.Begin() at the start of each encounter.
/// Written back to Ship at the end of combat via WriteBackToShip().
/// </summary>
public class CombatState
{
    // ── Crew references ──────────────────────────────────────────────────────
    // The resolver falls back to Captain when a specialist slot is empty.

    public Character Captain        { get; set; }
    /// <summary>Gunner if one is aboard; null otherwise → Captain fires the guns.</summary>
    public Character Gunner { get; set; }
    /// <summary>Pilot if one is aboard; null otherwise → Captain flies the ship.</summary>
    public Character Pilot          { get; set; }
    /// <summary>Engineer if one is aboard; null otherwise → no end-of-turn repairs.</summary>
    public Character Engineer       { get; set; }

    // ── Player ship live HP ───────────────────────────────────────────────────

    public int PlayerMaxShields     { get; set; }
    public int PlayerCurrentShields { get; set; }
    public int PlayerMaxHull        { get; set; }
    public int PlayerCurrentHull    { get; set; }

    public bool PlayerShieldsUp  => PlayerCurrentShields > 0;
    public bool PlayerDefeated   => PlayerCurrentHull    <= 0;

    public float PlayerShieldPct => PlayerMaxShields > 0 ? (float)PlayerCurrentShields / PlayerMaxShields : 0f;
    public float PlayerHullPct   => PlayerMaxHull    > 0 ? (float)PlayerCurrentHull    / PlayerMaxHull    : 0f;

    // ── Player ship equipment modifiers (read from installed gear at combat start) ──

    /// <summary>Installed shield equipment tier (1–6). 0 = no shields installed.</summary>
    public int PlayerShieldTier          { get; set; }
    /// <summary>Added to defense roll while player shields are up.</summary>
    public int PlayerShieldDefenseRating { get; set; }
    /// <summary>Added to defense roll when player shields are down.</summary>
    public int PlayerArmorDefenseRating  { get; set; }

    /// <summary>particle cannon tier (1 = Mk I … 6 = Mk VI). 0 = not installed.</summary>
    public int PlayerParticleCannonTier    { get; set; }
    /// <summary>Torpedo tier (1–6). 0 = not installed.</summary>
    public int PlayerTorpedoTier { get; set; }
    /// <summary>
    /// Remaining torpedo shots for this battle.
    /// Initialised from tier (tier × 4); decremented on each firing; blocks firing at 0.
    /// </summary>
    public int PlayerTorpedoCount { get; set; }

    // ── Enemy fleet ───────────────────────────────────────────────────────────

    public List<EnemyCombatState> Enemies      { get; set; } = new();
    public int                    TargetIndex  { get; set; } = 0;

    public EnemyCombatState CurrentTarget =>
        (Enemies != null && TargetIndex >= 0 && TargetIndex < Enemies.Count)
            ? Enemies[TargetIndex] : null;

    public bool AllEnemiesDestroyed => Enemies.TrueForAll(e => e.IsDestroyed);

    // ── Pilot maneuver modifiers (set each turn by CombatViewController) ─────

    /// <summary>
    /// Added to the player's attack total when firing. Negative for Evasive Maneuvers,
    /// positive for Attack Pattern, zero for Standard.
    /// </summary>
    public int ManeuverAttackBonus  { get; set; }

    /// <summary>
    /// Added to the player's defense DC against incoming fire. Positive for Evasive
    /// Maneuvers, negative for Attack Pattern, zero for Standard.
    /// </summary>
    public int ManeuverDefenseBonus { get; set; }

    // ── Research-derived combat modifiers (set at Begin from unlocked research) ──

    /// <summary>All passive/offensive combat modifiers granted by the player's research.</summary>
    public CombatResearchModifiers Research { get; set; }

    // ── Emergency Thrust active ability (prop_3a) ─────────────────────────────

    /// <summary>Turns remaining where the +2 defense bonus is active. 0 = inactive.</summary>
    public int EmergencyThrustActiveTurns   { get; set; }
    /// <summary>Turns remaining on cooldown. 0 = ready to activate.</summary>
    public int EmergencyThrustCooldownTurns { get; set; }

    /// <summary>Defense bonus applied to all incoming attacks while active.</summary>
    public int EmergencyThrustDefenseBonus =>
        EmergencyThrustActiveTurns > 0 ? Constants.Research.Effects.EmergencyThrustDefenseBonus : 0;

    /// <summary>True when the ability can be activated (researched check is the caller's responsibility).</summary>
    public bool CanActivateEmergencyThrust =>
        EmergencyThrustActiveTurns == 0 && EmergencyThrustCooldownTurns == 0;

    /// <summary>Activates Emergency Thrust for the configured number of turns.</summary>
    public void ActivateEmergencyThrust()
    {
        EmergencyThrustActiveTurns   = Constants.Research.Effects.EmergencyThrustActiveTurns;
        EmergencyThrustCooldownTurns = 0;
    }

    /// <summary>
    /// Called at the end of each enemy turn. Decrements the active counter; when it
    /// hits zero the cooldown starts. Decrements cooldown when active is already zero.
    /// </summary>
    public void AdvanceEmergencyThrustTurn()
    {
        if (EmergencyThrustActiveTurns > 0)
        {
            EmergencyThrustActiveTurns--;
            if (EmergencyThrustActiveTurns == 0)
                EmergencyThrustCooldownTurns = Constants.Research.Effects.EmergencyThrustCooldownTurns;
        }
        else if (EmergencyThrustCooldownTurns > 0)
        {
            EmergencyThrustCooldownTurns--;
        }
    }

    // ── Phase ─────────────────────────────────────────────────────────────────

    public CombatPhase Phase { get; set; } = CombatPhase.PlayerTurn;

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialise a combat state from the current game state.
    /// Call this just before showing the CombatView.
    /// </summary>
    public static CombatState Begin(
        Ship             ship,
        Character        captain,
        Character        gunner,
        Character        pilot,
        EnemyShipConfig[] enemies,
        EquipmentItem    particleCannon   = null,
        EquipmentItem    torpedoes    = null,
        EquipmentItem    shields      = null,
        EquipmentItem    armor        = null,
        Character        engineer     = null,
        CombatResearchModifiers research = default)
    {
        int torpedoTier  = torpedoes != null ? (int)torpedoes.Tier : 0;
        int torpedoCount = torpedoTier > 0 ? torpedoTier * 4 : 0;  // Mk I = 4 shots … Mk VI = 24

        // Research can enlarge the torpedo magazine (combat_3b Warhead Miniaturization).
        if (torpedoCount > 0 && research.TorpedoCountMultiplier > 1f)
            torpedoCount = Mathf.RoundToInt(torpedoCount * research.TorpedoCountMultiplier);

        // Each tier of Shields/Armor adds +5 to the respective max HP pool.
        int shieldTierBonus = shields != null ? (int)shields.Tier * 5 : 0;
        int armorTierBonus  = armor   != null ? (int)armor.Tier   * 5 : 0;

        int maxShields = ship.MaxShieldPoints + shieldTierBonus;
        int maxHull    = ship.MaxHullPoints   + armorTierBonus;

        var state = new CombatState
        {
            Captain = captain,
            Gunner  = gunner,
            Pilot          = pilot,
            Engineer       = engineer,

            PlayerMaxShields     = maxShields,
            PlayerCurrentShields = Mathf.Min(ship.ShieldPoints + shieldTierBonus, maxShields),
            PlayerMaxHull        = maxHull,
            PlayerCurrentHull    = Mathf.Min(ship.HullPoints   + armorTierBonus,  maxHull),

            PlayerParticleCannonTier     = particleCannon != null ? (int)particleCannon.Tier : 0,
            PlayerTorpedoTier  = torpedoTier,
            PlayerTorpedoCount = torpedoCount,

            PlayerShieldTier          = shields != null ? (int)shields.Tier     : 0,
            // DefenseRating on shield equipment boosts defense vs attacks while shields are up.
            // DefenseRating on armor boosts defense when shields are down.
            PlayerShieldDefenseRating = shields != null ? shields.DefenseRating : 0,
            PlayerArmorDefenseRating  = armor   != null ? armor.DefenseRating   : 0,

            Research = research,

            Phase = CombatPhase.PlayerTurn,
        };

        if (enemies != null)
            foreach (var cfg in enemies)
                state.Enemies.Add(EnemyCombatState.FromConfig(cfg));

        return state;
    }

    /// <summary>
    /// Write HP back to the Ship model after combat ends.
    /// Call this before saving the game.
    /// </summary>
    public void WriteBackToShip(Ship ship)
    {
        ship.ShieldPoints = PlayerCurrentShields;
        ship.HullPoints   = PlayerCurrentHull;
    }
}
