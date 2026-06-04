using System.Collections.Generic;

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
    /// <summary>WeaponsOfficer if one is aboard; null otherwise → Captain fires the guns.</summary>
    public Character WeaponsOfficer { get; set; }
    /// <summary>Pilot if one is aboard; null otherwise → Captain flies the ship.</summary>
    public Character Pilot          { get; set; }

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

    /// <summary>Added to defense roll while player shields are up.</summary>
    public int PlayerShieldDefenseRating { get; set; }
    /// <summary>Added to defense roll when player shields are down.</summary>
    public int PlayerArmorDefenseRating  { get; set; }

    /// <summary>Beam weapon tier (1 = Mk I … 6 = Mk VI). 0 = not installed.</summary>
    public int PlayerBeamTier    { get; set; }
    /// <summary>Torpedo tier (1–6). 0 = not installed.</summary>
    public int PlayerTorpedoTier { get; set; }

    // ── Enemy fleet ───────────────────────────────────────────────────────────

    public List<EnemyCombatState> Enemies      { get; set; } = new();
    public int                    TargetIndex  { get; set; } = 0;

    public EnemyCombatState CurrentTarget =>
        (Enemies != null && TargetIndex >= 0 && TargetIndex < Enemies.Count)
            ? Enemies[TargetIndex] : null;

    public bool AllEnemiesDestroyed => Enemies.TrueForAll(e => e.IsDestroyed);

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
        Character        weaponsOfficer,
        Character        pilot,
        EnemyShipConfig[] enemies,
        EquipmentItem    beamWeapon   = null,
        EquipmentItem    torpedoes    = null,
        EquipmentItem    shields      = null,
        EquipmentItem    armor        = null)
    {
        var state = new CombatState
        {
            Captain        = captain,
            WeaponsOfficer = weaponsOfficer,
            Pilot          = pilot,

            PlayerMaxShields     = ship.MaxShieldPoints,
            PlayerCurrentShields = ship.ShieldPoints,
            PlayerMaxHull        = ship.MaxHullPoints,
            PlayerCurrentHull    = ship.HullPoints,

            PlayerBeamTier    = beamWeapon  != null ? (int)beamWeapon.Tier  : 0,
            PlayerTorpedoTier = torpedoes   != null ? (int)torpedoes.Tier   : 0,

            // DefenseRating on shield equipment boosts defense vs attacks while shields are up.
            // DefenseRating on armor boosts defense when shields are down.
            PlayerShieldDefenseRating = shields != null ? shields.DefenseRating : 0,
            PlayerArmorDefenseRating  = armor   != null ? armor.DefenseRating   : 0,

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
