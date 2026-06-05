using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the Space Battle view.
///
/// ── Scene hierarchy expected ──────────────────────────────────────────────
///
///   CombatView                        ← this MonoBehaviour
///   ├─ EnemyLeftSlot                  ← point-anchored, upper-left third
///   │  └─ EnemyLeftImage              ← Image, rotation baked in builder
///   ├─ EnemyCenterSlot                ← point-anchored, upper-centre
///   │  └─ EnemyCenterImage
///   ├─ EnemyRightSlot                 ← point-anchored, upper-right third
///   │  └─ EnemyRightImage
///   ├─ PlayerShipImage                ← bottom-centre, nose up
///   └─ CombatActionBar
///
/// ── Enemy layout rules ────────────────────────────────────────────────────
///
///   1 enemy  → centre slot only
///   2 enemies → left + right
///   3 enemies → left + centre + right
///
///   Each slot's RectTransform sizeDelta is set at runtime to match the
///   sprite's natural pixel dimensions so larger sprites appear larger.
///
///   Ship rotation angles are baked into the builder (per slot):
///     Left   ≈ 235 °  (faces down-right toward the player)
///     Centre = 180 °  (faces straight down)
///     Right  ≈ 125 °  (faces down-left toward the player)
///
/// ── Beam / torpedo origin points ──────────────────────────────────────────
///
///   All projectiles and beams originate and terminate at each ship's
///   "nose" — the world-space position of the sprite's local +Y tip after
///   rotation.  For the player ship (no rotation) this is the top-centre
///   of the image; for enemy ships it is their visual bottom (facing the
///   player) because their sprites are rotated 180° ± slot angle.
///   See GetNoseWorld(Image img).
/// </summary>
public class CombatViewController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Sprite availability — not every color/class combo has an asset.
    // Update this set if sprites are added or removed from DGB Spaceships/.
    // -----------------------------------------------------------------------

    private static readonly HashSet<(DGBShipColor, DGBShipClass)> UnavailableCombos = new()
    {
        (DGBShipColor.Red, DGBShipClass.Fighter),
    };

    // Max variant per color/class combo (default 1 if not listed here).
    private static readonly Dictionary<(DGBShipColor, DGBShipClass), int> MaxVariant = new()
    {
        { (DGBShipColor.Blue,  DGBShipClass.Battleship), 2 },
        { (DGBShipColor.Blue,  DGBShipClass.Corvette),   2 },
        { (DGBShipColor.Blue,  DGBShipClass.Cruiser),    2 },
        { (DGBShipColor.Blue,  DGBShipClass.Fighter),    2 },
        { (DGBShipColor.Green, DGBShipClass.Battleship), 2 },
        { (DGBShipColor.Green, DGBShipClass.Corvette),   2 },
        { (DGBShipColor.Green, DGBShipClass.Cruiser),    2 },
        { (DGBShipColor.Red,   DGBShipClass.Battleship), 2 },
        { (DGBShipColor.Red,   DGBShipClass.Corvette),   2 },
        { (DGBShipColor.Red,   DGBShipClass.Cruiser),    2 },
    };

    // -----------------------------------------------------------------------
    // Inspector references — wired by GameSceneSetup
    // -----------------------------------------------------------------------

    [Header("Player Ship")]
    [SerializeField] private Image playerShipImage;

    [Header("Enemy Slots — CanvasGroups (show/hide)")]
    [SerializeField] private CanvasGroup    enemyLeftGroup;
    [SerializeField] private RectTransform  enemyLeftSlotRT;
    [SerializeField] private Image          enemyLeftImage;

    [SerializeField] private CanvasGroup    enemyCenterGroup;
    [SerializeField] private RectTransform  enemyCenterSlotRT;
    [SerializeField] private Image          enemyCenterImage;

    [SerializeField] private CanvasGroup    enemyRightGroup;
    [SerializeField] private RectTransform  enemyRightSlotRT;
    [SerializeField] private Image          enemyRightImage;

    [Header("DGB Ship Sprite Library")]
    [Tooltip("All sprites from Assets/DGB Spaceships/Spaceship Sprites/ — loaded by GameSceneSetup.")]
    [SerializeField] private Sprite[] dgbShipSprites;

    [Header("Action Bar")]
    [SerializeField] private GameObject combatActionBar;

    [Header("Action Bar Buttons — wired by GameSceneSetup")]
    [SerializeField] private Button fireTorpedesButton;
    [SerializeField] private Button fireBeamWeaponButton;

    [Header("Torpedo Count Label — wired by GameSceneSetup")]
    [Tooltip("Small label below the Fire Torpedoes button showing remaining count (e.g. ×8).")]
    [SerializeField] private TMP_Text torpedoCountText;

    [Header("Combat Log — wired by GameSceneSetup")]
    [Tooltip("TMP_Text inside CombatLogPanel; shows rolling combat events.")]
    [SerializeField] private TMP_Text combatLogText;

    [Header("Target Info Display — wired by GameSceneSetup")]
    [SerializeField] private TMP_Text targetInfoTitle;
    [SerializeField] private TMP_Text targetShieldText;
    [SerializeField] private TMP_Text targetHullText;

    [Header("Player Stats Display (header) — wired by GameSceneSetup")]
    [SerializeField] private TMP_Text playerShieldText;
    [SerializeField] private TMP_Text playerHullText;

    [Header("Crosshair")]
    [SerializeField] private CombatCrosshair combatCrosshair;

    [Header("Enemy Tap Buttons — wired by GameSceneSetup")]
    [SerializeField] private Button enemyLeftButton;
    [SerializeField] private Button enemyCenterButton;
    [SerializeField] private Button enemyRightButton;

    [Header("Projectiles — wired by GameSceneSetup")]
    [Tooltip("RectTransform that projectiles are spawned into (covers combat arena, above ships).")]
    [SerializeField] private RectTransform projectileContainer;
    [Tooltip("missile_1 sprite — used for torpedo fire.")]
    [SerializeField] private Sprite missileSprite;

    [Header("Beam Weapon — wired by GameSceneSetup")]
    [Tooltip("laser_noise00 texture stretched along the beam line.")]
    [SerializeField] private Texture2D beamTexture;
    [Tooltip("glow_round00 sprite used for the impact circle and muzzle flash.")]
    [SerializeField] private Sprite beamGlowSprite;

    [Header("Audio — wired by GameSceneSetup")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip   beamWeaponClip;
    [SerializeField] private AudioClip   torpedoClip;

    // -----------------------------------------------------------------------
    // Combat state
    // -----------------------------------------------------------------------

    // The slot RectTransform that is currently targeted — updated by SetTarget().
    private RectTransform _currentTargetRT;

    // Maps each slot RectTransform to the EnemyShipConfig it displays.
    private readonly Dictionary<RectTransform, EnemyShipConfig> _slotToConfig = new();

    // Maps each slot RectTransform to its live EnemyCombatState.
    private readonly Dictionary<RectTransform, EnemyCombatState> _slotToState = new();

    // Maps each slot RectTransform to the Image component for nose-position calculations.
    private readonly Dictionary<RectTransform, Image> _slotToImage = new();

    // Live D20 combat data — set by StartCombat().
    private CombatState _combatState;

    // ── Combat log ────────────────────────────────────────────────────────────
    private const int CombatLogMaxLines = 5;
    private readonly Queue<string> _logLines = new(CombatLogMaxLines);

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    private void Awake()
    {
        int count = dgbShipSprites != null ? dgbShipSprites.Length : 0;
        Debug.Log($"[CombatViewController] Awake — dgbShipSprites count: {count}");
        if (count > 0)
            foreach (var s in dgbShipSprites)
                Debug.Log($"  sprite: '{s?.name}'");
    }

    private void Start()
    {
        enemyLeftButton?.onClick.AddListener(  () => SetTarget(enemyLeftSlotRT));
        enemyCenterButton?.onClick.AddListener(() => SetTarget(enemyCenterSlotRT));
        enemyRightButton?.onClick.AddListener( () => SetTarget(enemyRightSlotRT));

        fireTorpedesButton?.onClick.AddListener(FireTorpedoes);
        fireBeamWeaponButton?.onClick.AddListener(FireBeamWeapon);

        RefreshDisplays(100f, 100f, 100f, 100f);
        RefreshTorpedoCountDisplay();
    }

    // Duration constants used when yielding for animations to finish.
    private const float TorpedoFlightDuration = 0.55f;

    /// <summary>Fire torpedoes at the current target.</summary>
    public void FireTorpedoes()
    {
        if (_currentTargetRT == null || _combatState == null) return;
        if (_combatState.Phase != CombatPhase.PlayerTurn) return;
        var target = GetTargetCombatState();
        if (target == null) return;
        StartCoroutine(PlayerTorpedoRoutine(target));
    }

    /// <summary>Fire beam weapon at the current target.</summary>
    public void FireBeamWeapon()
    {
        if (_currentTargetRT == null || _combatState == null) return;
        if (_combatState.Phase != CombatPhase.PlayerTurn) return;
        var target = GetTargetCombatState();
        if (target == null) return;
        StartCoroutine(PlayerBeamRoutine(target));
    }

    // -----------------------------------------------------------------------
    // Player action coroutines
    // -----------------------------------------------------------------------

    private IEnumerator PlayerBeamRoutine(EnemyCombatState target)
    {
        SetFireButtonsEnabled(false);

        // Resolve dice immediately (damage is tracked internally).
        var result = CombatResolver.PlayerFireBeam(_combatState, target);
        Debug.Log($"[Combat] {result.Description}");
        AppendLog(FormatPlayerAttack(result));

        // Fire the visual and sound.
        PulseCrosshair();
        sfxSource?.PlayOneShot(beamWeaponClip);
        LaunchBeamWeapon();

        // If the attack hits, spawn an impact flash after the beam's fade-in completes.
        if (result.IsHit)
        {
            StartCoroutine(DelayedHitEffect(
                CombatBeamEffect.AnimationDuration * 0.25f,  // ~25% through the animation
                GetEnemyNoseWorld(_currentTargetRT),
                CombatHitEffect.HitType.Beam));
        }

        // Wait for the full animation, then apply damage numbers.
        yield return new WaitForSeconds(CombatBeamEffect.AnimationDuration);
        RefreshDisplaysFromState();
        CheckCombatEnd();

        if (_combatState.Phase == CombatPhase.PlayerTurn)
            yield return StartCoroutine(EnemyTurnRoutine());

        if (_combatState.Phase == CombatPhase.PlayerTurn)
            SetFireButtonsEnabled(true);
    }

    private IEnumerator PlayerTorpedoRoutine(EnemyCombatState target)
    {
        SetFireButtonsEnabled(false);

        var result = CombatResolver.PlayerFireTorpedo(_combatState, target);
        Debug.Log($"[Combat] {result.Description}");
        AppendLog(FormatPlayerAttack(result));

        // Update torpedo count immediately (resolver decremented it).
        RefreshTorpedoCountDisplay();

        PulseCrosshair();
        sfxSource?.PlayOneShot(torpedoClip);

        Vector3 startWorld = GetNoseWorld(playerShipImage);
        Vector3 endWorld   = GetEnemyNoseWorld(_currentTargetRT);
        LaunchProjectileFromTo(startWorld, endWorld, missileSprite,
                               new Vector2(40f, 96f), TorpedoFlightDuration);

        yield return new WaitForSeconds(TorpedoFlightDuration);

        // Explosion at point of impact.
        if (result.IsHit)
            CombatHitEffect.SpawnAt(projectileContainer, endWorld,
                                    beamGlowSprite, CombatHitEffect.HitType.Explosion);

        RefreshDisplaysFromState();
        CheckCombatEnd();

        if (_combatState.Phase == CombatPhase.PlayerTurn)
            yield return StartCoroutine(EnemyTurnRoutine());

        if (_combatState.Phase == CombatPhase.PlayerTurn)
            SetFireButtonsEnabled(true);
    }

    // -----------------------------------------------------------------------
    // Enemy turn coroutine
    // -----------------------------------------------------------------------

    private IEnumerator EnemyTurnRoutine()
    {
        foreach (var enemy in _combatState.Enemies)
        {
            if (enemy.IsDestroyed) continue;

            var result = CombatResolver.EnemyAttack(_combatState, enemy);
            Debug.Log($"[Combat] {result.Description}");
            AppendLog(FormatEnemyAttack(result));

            var slotRT = GetSlotForEnemy(enemy);
            float waitTime = 0f;
            if (slotRT != null && playerShipImage != null)
                waitTime = LaunchEnemyAttackEffect(slotRT, result.IsBeamAttack, result.IsHit);

            yield return new WaitForSeconds(waitTime);

            RefreshDisplaysFromState();
            CheckCombatEnd();

            if (_combatState.Phase != CombatPhase.PlayerTurn) yield break;
        }

        // After all enemies have attacked, give the engineer a repair attempt.
        if (_combatState.Phase == CombatPhase.PlayerTurn && _combatState.Engineer != null)
        {
            var repair = CombatResolver.EngineerRepair(_combatState);
            Debug.Log($"[Combat] {repair.Description}");
            AppendLog(FormatRepair(repair));

            if (repair.Attempted && repair.Success)
            {
                yield return new WaitForSeconds(0.3f);  // brief pause for flavor
                RefreshDisplaysFromState();
            }
        }
    }

    /// <summary>
    /// Spawns the beam or torpedo visual from an enemy slot toward the player ship,
    /// plays the matching sound, and returns the animation duration to wait for.
    /// </summary>
    private float LaunchEnemyAttackEffect(RectTransform enemySlotRT, bool isBeam, bool isHit)
    {
        if (projectileContainer == null || playerShipImage == null) return 0f;

        // Origin: nose of the enemy ship (sprite local +Y tip, rotated to face player).
        Image enemyImg    = GetEnemyImageForSlot(enemySlotRT);
        Vector3 fromWorld = enemyImg != null ? GetNoseWorld(enemyImg) : enemySlotRT.position;

        // Target: nose (top-centre) of the player ship.
        Vector3 toWorld = GetNoseWorld(playerShipImage);

        if (isBeam)
        {
            sfxSource?.PlayOneShot(beamWeaponClip);
            var go = new GameObject("EnemyBeamEffect", typeof(RectTransform));
            go.transform.SetParent(projectileContainer, worldPositionStays: false);
            go.AddComponent<CombatBeamEffect>().Fire(
                fromWorld, toWorld, beamTexture, beamGlowSprite,
                tint: CombatBeamEffect.EnemyBeamTint);

            if (isHit)
                StartCoroutine(DelayedHitEffect(
                    CombatBeamEffect.AnimationDuration * 0.25f,
                    toWorld,
                    CombatHitEffect.HitType.Beam));

            return CombatBeamEffect.AnimationDuration;
        }
        else
        {
            sfxSource?.PlayOneShot(torpedoClip);
            LaunchProjectileFromTo(fromWorld, toWorld, missileSprite,
                                   new Vector2(40f, 96f), TorpedoFlightDuration);

            if (isHit)
                StartCoroutine(DelayedHitEffect(
                    TorpedoFlightDuration,
                    toWorld,
                    CombatHitEffect.HitType.Explosion));

            return TorpedoFlightDuration;
        }
    }

    /// <summary>Waits delay seconds then spawns a hit effect at worldPos.</summary>
    private IEnumerator DelayedHitEffect(float delay, Vector3 worldPos, CombatHitEffect.HitType hitType)
    {
        yield return new WaitForSeconds(delay);
        CombatHitEffect.SpawnAt(projectileContainer, worldPos, beamGlowSprite, hitType);
    }

    /// <summary>Like LaunchProjectile but takes explicit world positions instead of using _currentTargetRT.</summary>
    private void LaunchProjectileFromTo(Vector3 startWorld, Vector3 endWorld,
                                        Sprite sprite, Vector2 displaySize, float duration)
    {
        if (projectileContainer == null) return;

        var go   = new GameObject("Projectile", typeof(RectTransform));
        go.transform.SetParent(projectileContainer, worldPositionStays: false);

        var proj = go.AddComponent<CombatProjectile>();
        proj.Launch(startWorld, endWorld, sprite, displaySize, duration);
    }

    /// <summary>Reverse lookup: find the slot RectTransform that holds a given enemy state.</summary>
    private RectTransform GetSlotForEnemy(EnemyCombatState enemy)
    {
        foreach (var kvp in _slotToState)
            if (kvp.Value == enemy) return kvp.Key;
        return null;
    }

    /// <summary>Look up the Image component for an enemy slot (used for nose-position calculation).</summary>
    private Image GetEnemyImageForSlot(RectTransform slotRT)
    {
        if (slotRT == null) return null;
        _slotToImage.TryGetValue(slotRT, out var img);
        return img;
    }

    private void SetFireButtonsEnabled(bool enabled)
    {
        if (fireBeamWeaponButton != null)
            fireBeamWeaponButton.interactable = enabled;

        // Torpedo button also disabled when out of torpedoes.
        if (fireTorpedesButton != null)
            fireTorpedesButton.interactable = enabled &&
                (_combatState == null || _combatState.PlayerTorpedoCount > 0);
    }

    // -----------------------------------------------------------------------
    // Combat end checks
    // -----------------------------------------------------------------------

    private void CheckCombatEnd()
    {
        if (_combatState == null) return;

        if (_combatState.AllEnemiesDestroyed)
        {
            _combatState.Phase = CombatPhase.Victory;
            Debug.Log("[Combat] Victory!");
            // TODO: trigger victory sequence / XP / loot
        }
        else if (_combatState.PlayerDefeated)
        {
            _combatState.Phase = CombatPhase.Defeat;
            Debug.Log("[Combat] Defeated!");
            // TODO: trigger defeat sequence / game over
        }
    }

    // -----------------------------------------------------------------------
    // State helpers
    // -----------------------------------------------------------------------

    private EnemyCombatState GetTargetCombatState()
    {
        if (_currentTargetRT == null) return null;
        _slotToState.TryGetValue(_currentTargetRT, out var state);
        return state;
    }

    private void RefreshDisplaysFromState()
    {
        if (_combatState == null) { RefreshDisplays(100f, 100f, 100f, 100f); return; }

        float pShield = _combatState.PlayerShieldPct * 100f;
        float pHull   = _combatState.PlayerHullPct   * 100f;

        var target = GetTargetCombatState();
        float tShield = target != null ? target.ShieldPct * 100f : 100f;
        float tHull   = target != null ? target.HullPct   * 100f : 100f;

        RefreshDisplays(pShield, pHull, tShield, tHull);
    }

    // -----------------------------------------------------------------------
    // Nose-position helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the world-space "nose" of a ship image — the point at the tip
    /// of the sprite's local +Y axis after all parent transforms are applied.
    ///
    /// Player ship (no rotation)  → top-centre in world space.
    /// Enemy ship (rotated ≈180°) → visual bottom in world space (facing the player).
    /// </summary>
    private static Vector3 GetNoseWorld(Image img)
    {
        if (img == null) return Vector3.zero;
        var rt = img.rectTransform;
        return rt.TransformPoint(new Vector3(0f, rt.rect.yMax, 0f));
    }

    /// <summary>Returns the nose position for whatever enemy is in the given slot.</summary>
    private Vector3 GetEnemyNoseWorld(RectTransform slotRT)
    {
        var img = GetEnemyImageForSlot(slotRT);
        return img != null ? GetNoseWorld(img) : (slotRT != null ? slotRT.position : Vector3.zero);
    }

    // -----------------------------------------------------------------------
    // Torpedo count display
    // -----------------------------------------------------------------------

    private void RefreshTorpedoCountDisplay()
    {
        if (torpedoCountText == null) return;

        if (_combatState == null || _combatState.PlayerTorpedoTier <= 0)
        {
            torpedoCountText.text = "";
            return;
        }

        torpedoCountText.text = $"×{_combatState.PlayerTorpedoCount}";
    }

    // -----------------------------------------------------------------------
    // Combat log
    // -----------------------------------------------------------------------

    // TMP rich-text colours
    private const string ColPlayer  = "#4DD9FF";   // cyan  — player actions
    private const string ColEnemy   = "#FF6633";   // orange — enemy actions
    private const string ColEngineer= "#60FF90";   // green  — engineer repair
    private const string ColRolls   = "#8AADCC";   // dim blue-grey — roll detail

    /// <summary>
    /// Append a formatted line to the rolling combat log, capped at
    /// <see cref="CombatLogMaxLines"/> entries.  Null/empty lines are silently dropped.
    /// </summary>
    private void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(line) || combatLogText == null) return;
        if (_logLines.Count >= CombatLogMaxLines) _logLines.Dequeue();
        _logLines.Enqueue(line);
        combatLogText.text = string.Join("\n", _logLines);
    }

    private void ClearLog()
    {
        _logLines.Clear();
        if (combatLogText != null) combatLogText.text = "";
    }

    // ── Entry formatters ──────────────────────────────────────────────────────

    private static string FormatPlayerAttack(CombatResolver.AttackResult r)
    {
        if (r.AttackerRoll == 0) return null;    // no weapon — nothing to log
        string wpn   = r.IsBeamAttack ? "Beam" : "Torp";
        string rolls = $"<color={ColRolls}>({r.AttackerTotal}▶{r.DefenderTotal})</color>";
        if (!r.IsHit)
            return $"<color={ColPlayer}>YOU</color>  {wpn} miss  {rolls}";
        string dmg = r.HullOverflow > 0 ? $"{r.FinalDamage}+{r.HullOverflow}" : $"{r.FinalDamage}";
        string shd = r.ShieldsBroken ? " ▼SHD" : "";
        return $"<color={ColPlayer}>YOU</color>  {wpn} hit {dmg} dmg{shd}  {rolls}";
    }

    private static string FormatEnemyAttack(CombatResolver.AttackResult r)
    {
        if (r.AttackerRoll == 0) return null;
        string wpn   = r.IsBeamAttack ? "Beam" : "Torp";
        string rolls = $"<color={ColRolls}>({r.AttackerTotal}▶{r.DefenderTotal})</color>";
        if (!r.IsHit)
            return $"<color={ColEnemy}>ENE</color>  {wpn} miss  {rolls}";
        string dmg = r.HullOverflow > 0 ? $"{r.FinalDamage}+{r.HullOverflow}" : $"{r.FinalDamage}";
        string shd = r.ShieldsBroken ? " ▼SHD" : "";
        return $"<color={ColEnemy}>ENE</color>  {wpn} hit {dmg} dmg{shd}  {rolls}";
    }

    private static string FormatRepair(CombatResolver.EngineerRepairResult r)
    {
        if (!r.Attempted) return null;
        string rolls = $"<color={ColRolls}>({r.Total}▶{r.DC})</color>";
        if (!r.Success)
            return $"<color={ColEngineer}>ENG</color>  failed  {rolls}";
        string what;
        if (r.ShieldsRepaired > 0 && r.HullRepaired > 0)
            what = $"shlds +{r.ShieldsRepaired} hull +{r.HullRepaired}";
        else if (r.ShieldsRepaired > 0)
            what = $"shlds +{r.ShieldsRepaired}";
        else if (r.HullRepaired > 0)
            what = $"hull +{r.HullRepaired}";
        else
            what = "nothing to repair";
        return $"<color={ColEngineer}>ENG</color>  {what}  {rolls}";
    }

    // -----------------------------------------------------------------------
    // Projectile helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spawn a CombatBeamEffect from the player ship's nose to the targeted enemy's nose.
    /// </summary>
    private void LaunchBeamWeapon()
    {
        if (projectileContainer == null || playerShipImage == null || _currentTargetRT == null)
            return;

        Vector3 startWorld = GetNoseWorld(playerShipImage);
        Vector3 endWorld   = GetEnemyNoseWorld(_currentTargetRT);

        var go = new GameObject("BeamEffect", typeof(RectTransform));
        go.transform.SetParent(projectileContainer, worldPositionStays: false);

        var effect = go.AddComponent<CombatBeamEffect>();
        effect.Fire(startWorld, endWorld, beamTexture, beamGlowSprite);
    }

    /// <summary>
    /// Initialise live D20 combat state. Call before StartCombat() when you have
    /// access to the ship and crew. If not called, combat falls back to display-only mode.
    /// </summary>
    public void SetCombatState(CombatState combatState)
    {
        _combatState = combatState;
    }

    private void RefreshDisplays(float playerShieldPct, float playerHullPct,
                                  float targetShieldPct, float targetHullPct)
    {
        if (playerShieldText != null) playerShieldText.text = $"SHIELDS  {playerShieldPct:0}%";
        if (playerHullText   != null) playerHullText.text   = $"HULL  {playerHullPct:0}%";
        if (targetShieldText != null) targetShieldText.text = $"SHIELDS  {targetShieldPct:0}%";
        if (targetHullText   != null) targetHullText.text   = $"HULL  {targetHullPct:0}%";
    }

    public void OnCombatEnter()
    {
        StopAllCoroutines();   // cancel any in-flight turn routine from a previous combat
        _slotToConfig.Clear();
        _slotToState.Clear();
        _slotToImage.Clear();
        _combatState = null;
        SetFireButtonsEnabled(true);
        if (targetInfoTitle != null) targetInfoTitle.text = "TARGET";
        RefreshDisplays(100f, 100f, 100f, 100f);
        RefreshTorpedoCountDisplay();
        ClearLog();
        Debug.Log("[CombatViewController] Combat entered.");
    }

    public void OnCombatExit()
    {
        HideAllEnemySlots();
        combatCrosshair?.Hide();
        _currentTargetRT = null;
        Debug.Log("[CombatViewController] Combat exited.");
    }

    /// <summary>Trigger a single crosshair pulse — call when the player fires.</summary>
    public void PulseCrosshair() => combatCrosshair?.Pulse();

    /// <summary>Move the crosshair onto the given slot. Also called internally on tap.</summary>
    public void SetTarget(RectTransform slotRT)
    {
        if (slotRT == null) return;
        _currentTargetRT = slotRT;
        combatCrosshair?.SnapToSlot(slotRT);

        // Update title with the targeted ship's class name
        if (targetInfoTitle != null)
        {
            if (_slotToConfig.TryGetValue(slotRT, out var config))
                targetInfoTitle.text = config.shipClass.ToString().ToUpper();
            else
                targetInfoTitle.text = "TARGET";
        }

        RefreshDisplaysFromState();
        Debug.Log($"[CombatViewController] Targeted: {slotRT.name}");
    }

    /// <summary>
    /// Configures the enemy fleet. Call after ShowCombatView().
    /// enemies.Length must be 1–3.
    ///
    ///   1 enemy  → centre slot
    ///   2 enemies → left + right
    ///   3 enemies → left + centre + right
    /// </summary>
    public void StartCombat(EnemyShipConfig[] enemies)
    {
        HideAllEnemySlots();

        if (enemies == null || enemies.Length == 0)
        {
            Debug.LogWarning("[CombatViewController] StartCombat called with no enemies.");
            return;
        }

        int count = Mathf.Clamp(enemies.Length, 1, 3);

        _slotToConfig.Clear();
        _slotToState.Clear();
        _slotToImage.Clear();

        // If a CombatState was set via SetCombatState(), wire enemy states to slots.
        // Index order matches the slot layout: [0]=left/centre, [1]=right, [2]=centre(3-enemy).
        EnemyCombatState EnemyState(int idx) =>
            (_combatState != null && idx < _combatState.Enemies.Count)
                ? _combatState.Enemies[idx] : null;

        RectTransform defaultTarget = null;
        switch (count)
        {
            case 1:
                ShowEnemySlot(enemyCenterGroup, enemyCenterSlotRT, enemyCenterImage, enemies[0]);
                _slotToConfig[enemyCenterSlotRT] = enemies[0];
                if (EnemyState(0) != null) _slotToState[enemyCenterSlotRT] = EnemyState(0);
                defaultTarget = enemyCenterSlotRT;
                break;
            case 2:
                ShowEnemySlot(enemyLeftGroup,  enemyLeftSlotRT,  enemyLeftImage,  enemies[0]);
                ShowEnemySlot(enemyRightGroup, enemyRightSlotRT, enemyRightImage, enemies[1]);
                _slotToConfig[enemyLeftSlotRT]  = enemies[0];
                _slotToConfig[enemyRightSlotRT] = enemies[1];
                if (EnemyState(0) != null) _slotToState[enemyLeftSlotRT]  = EnemyState(0);
                if (EnemyState(1) != null) _slotToState[enemyRightSlotRT] = EnemyState(1);
                defaultTarget = enemyLeftSlotRT;
                break;
            case 3:
                ShowEnemySlot(enemyLeftGroup,   enemyLeftSlotRT,   enemyLeftImage,   enemies[0]);
                ShowEnemySlot(enemyCenterGroup, enemyCenterSlotRT, enemyCenterImage, enemies[1]);
                ShowEnemySlot(enemyRightGroup,  enemyRightSlotRT,  enemyRightImage,  enemies[2]);
                _slotToConfig[enemyLeftSlotRT]   = enemies[0];
                _slotToConfig[enemyCenterSlotRT] = enemies[1];
                _slotToConfig[enemyRightSlotRT]  = enemies[2];
                if (EnemyState(0) != null) _slotToState[enemyLeftSlotRT]   = EnemyState(0);
                if (EnemyState(1) != null) _slotToState[enemyCenterSlotRT] = EnemyState(1);
                if (EnemyState(2) != null) _slotToState[enemyRightSlotRT]  = EnemyState(2);
                defaultTarget = enemyCenterSlotRT;
                break;
        }

        RefreshTorpedoCountDisplay();
        SetTarget(defaultTarget);
    }

    public void SetPlayerShipSprite(Sprite sprite)
    {
        if (playerShipImage == null) return;
        playerShipImage.sprite = sprite;
        playerShipImage.color  = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
    }

    // -----------------------------------------------------------------------
    // Sprite lookup
    // -----------------------------------------------------------------------

    public Sprite GetEnemySprite(DGBShipColor color, DGBShipClass shipClass, int variant = 1)
    {
        if (dgbShipSprites == null) return null;

        if (UnavailableCombos.Contains((color, shipClass)))
        {
            Debug.LogWarning($"[CombatViewController] No DGB sprite exists for {color} {shipClass}.");
            return null;
        }

        var sprite = FindSprite(BuildSpriteName(color, shipClass, variant));
        // Fall back to variant 1 if the requested variant doesn't exist
        if (sprite == null && variant != 1)
            sprite = FindSprite(BuildSpriteName(color, shipClass, 1));

        if (sprite == null)
            Debug.LogWarning($"[CombatViewController] No DGB sprite: {color} {shipClass} v{variant}");

        return sprite;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void HideAllEnemySlots()
    {
        SetSlotVisible(enemyLeftGroup,   false);
        SetSlotVisible(enemyCenterGroup, false);
        SetSlotVisible(enemyRightGroup,  false);
    }

    private void ShowEnemySlot(CanvasGroup group, RectTransform slotRT,
                                Image img, EnemyShipConfig config)
    {
        if (config == null) return;

        var sprite = GetEnemySprite(config.color, config.shipClass, config.variant);

        if (slotRT != null && sprite != null)
            slotRT.sizeDelta = sprite.rect.size;

        if (img != null)
        {
            img.sprite = sprite;
            img.color  = sprite != null ? Color.white : new Color(0.6f, 0.2f, 0.2f, 0.4f);
        }

        // Register the image for nose-position lookups.
        if (slotRT != null && img != null)
            _slotToImage[slotRT] = img;

        SetSlotVisible(group, true);
    }

    private static void SetSlotVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha          = visible ? 1f : 0f;
        group.blocksRaycasts = visible;
        group.interactable   = visible;
    }

    // Filename format: "{color}_{class}_{variant}"  e.g. "red_cruiser_1"
    private static string BuildSpriteName(DGBShipColor color, DGBShipClass shipClass, int variant)
        => $"{color.ToString().ToLower()}_{shipClass.ToString().ToLower()}_{variant}";

    private Sprite FindSprite(string name)
    {
        foreach (var s in dgbShipSprites)
            if (s != null && s.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return s;
        return null;
    }
}
