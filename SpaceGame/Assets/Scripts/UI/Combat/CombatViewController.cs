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

    [Header("Combat Log — Expanded View")]
    [Tooltip("Button on CombatLogPanel; tap to open expanded detail log.")]
    [SerializeField] private Button       combatLogButton;
    [Tooltip("CanvasGroup on ExpandedLogPanel; shown/hidden to toggle the expanded log.")]
    [SerializeField] private CanvasGroup  expandedLogCanvasGroup;
    [Tooltip("TMP_Text inside ExpandedLogScrollRect/Viewport; holds the full-detail log.")]
    [SerializeField] private TMP_Text     expandedLogText;
    [Tooltip("ScrollRect inside ExpandedLogPanel; scrolled to bottom on each new entry.")]
    [SerializeField] private ScrollRect   expandedLogScrollRect;
    [Tooltip("Button that closes the expanded log.")]
    [SerializeField] private Button       expandedLogCloseButton;

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

    [Header("Pilot Maneuver Dropdown — wired by GameSceneSetup")]
    [Tooltip("Shift Dropdown prefab in CombatActionBar/PilotManeuverContainer.")]
    [SerializeField] private TMP_Dropdown pilotManeuverDropdown;

    [Header("Turn Banner — wired by GameSceneSetup")]
    [Tooltip("Full-screen TMP_Text flashed at the start of each player turn (e.g. 'Player's Turn').")]
    [SerializeField] private TMP_Text turnBannerText;
    [SerializeField] private CanvasGroup turnBannerCanvasGroup;

    [Header("Audio — wired by GameSceneSetup")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip   beamWeaponClip;
    [SerializeField] private AudioClip   torpedoClip;
    [Tooltip("Played when an enemy ship is destroyed. Assign in Inspector — add a clip at Assets/Audio/SFX/explosion.mp3.")]
    [SerializeField] private AudioClip   explosionClip;

    // -----------------------------------------------------------------------
    // Pilot maneuver
    // -----------------------------------------------------------------------

    /// <summary>
    /// Which piloting stance the player has selected this combat.
    /// Standard     → no modifiers
    /// Evasive      → +2 defense DC, −2 attack
    /// AttackPattern → −2 defense DC, +2 attack
    /// </summary>
    public enum PilotManeuver { Standard = 0, EvasiveManeuvers = 1, AttackPattern = 2 }

    /// <summary>Added to the player's attack total this turn. Set by dropdown selection.</summary>
    public int ManeuverAttackBonus  { get; private set; }
    /// <summary>Added to the player's defense DC this turn. Set by dropdown selection.</summary>
    public int ManeuverDefenseBonus { get; private set; }

    /// <summary>Push the current maneuver bonuses into _combatState before each resolver call.</summary>
    private void SyncManeuverBonuses()
    {
        if (_combatState == null) return;
        _combatState.ManeuverAttackBonus  = ManeuverAttackBonus;
        _combatState.ManeuverDefenseBonus = ManeuverDefenseBonus;
    }

    private void ApplyManeuver(PilotManeuver m)
    {
        switch (m)
        {
            case PilotManeuver.EvasiveManeuvers:
                ManeuverAttackBonus  = -2;
                ManeuverDefenseBonus = +2;
                break;
            case PilotManeuver.AttackPattern:
                ManeuverAttackBonus  = +2;
                ManeuverDefenseBonus = -2;
                break;
            default:
                ManeuverAttackBonus  = 0;
                ManeuverDefenseBonus = 0;
                break;
        }
    }

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

    // Maps each slot RectTransform to its CanvasGroup (used to hide destroyed ships).
    private readonly Dictionary<RectTransform, CanvasGroup> _slotToGroup = new();

    // Slots that have already had their destruction explosion played this combat.
    private readonly HashSet<RectTransform> _destroyedSlots = new();

    // Live D20 combat data — set by StartCombat().
    private CombatState _combatState;

    // ── Combat log ────────────────────────────────────────────────────────────
    private const int CombatLogMaxLines = 4;
    private readonly Queue<string>  _logLines       = new(CombatLogMaxLines);
    private readonly List<string>   _detailLogLines = new();
    private bool                    _expandedLogVisible;

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

        // Pilot maneuver dropdown
        if (pilotManeuverDropdown != null)
        {
            pilotManeuverDropdown.ClearOptions();
            pilotManeuverDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Standard",
                "Evasive Maneuvers",
                "Attack Pattern",
            });
            pilotManeuverDropdown.value = 0;
            ApplyManeuver(PilotManeuver.Standard);
            pilotManeuverDropdown.onValueChanged.AddListener(idx =>
                ApplyManeuver((PilotManeuver)idx));
        }

        // Resolve expanded-log references at runtime if the builder didn't wire them.
        // CombatViewController sits on the CombatView root, so all paths start there.
        if (combatLogButton == null)
            combatLogButton = transform.Find("CombatLogPanel")?.GetComponent<Button>();
        if (expandedLogCanvasGroup == null)
            expandedLogCanvasGroup = transform.Find("ExpandedLogPanel")?.GetComponent<CanvasGroup>();
        if (expandedLogScrollRect == null)
            expandedLogScrollRect = transform.Find("ExpandedLogPanel/ExpandedLogScrollRect")
                                             ?.GetComponent<ScrollRect>();
        if (expandedLogText == null)
            expandedLogText = transform.Find("ExpandedLogPanel/ExpandedLogScrollRect/Viewport/ExpandedLogText")
                                       ?.GetComponent<TMP_Text>();
        if (expandedLogCloseButton == null)
            expandedLogCloseButton = transform.Find("ExpandedLogPanel/ExpandedLogHeader/ExpandedLogCloseButton")
                                              ?.GetComponent<Button>();

        // Self-wire turn banner if not assigned by the builder.
        if (turnBannerCanvasGroup == null)
            turnBannerCanvasGroup = transform.Find("TurnBanner")?.GetComponent<CanvasGroup>();
        if (turnBannerText == null)
            turnBannerText = transform.Find("TurnBanner/TurnBannerText")?.GetComponent<TMP_Text>();

        combatLogButton?.onClick.AddListener(ToggleCombatLog);
        expandedLogCloseButton?.onClick.AddListener(() => SetExpandedLogVisible(false));

        Debug.Log($"[CombatViewController] combatLogButton={combatLogButton}, expandedLogCG={expandedLogCanvasGroup}");

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
        SyncManeuverBonuses();

        // Resolve dice immediately (damage is tracked internally).
        var result = CombatResolver.PlayerFireBeam(_combatState, target);
        Debug.Log($"[Combat] {result.Description}");
        AppendLog(FormatPlayerAttack(result));
        AppendDetailLog($"<color={ColPlayer}>YOU</color>  {result.Description}");

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

        // Wait for the full animation, then check for ship destruction before
        // refreshing the displays and checking combat end.
        yield return new WaitForSeconds(CombatBeamEffect.AnimationDuration);
        SpawnDamageText(GetEnemyNoseWorld(_currentTargetRT), result);
        yield return StartCoroutine(PlayDestroyedShipEffects());
        RefreshDisplaysFromState();
        CheckCombatEnd();

        if (_combatState.Phase == CombatPhase.PlayerTurn)
            yield return StartCoroutine(PlayerTurnRepairRoutine());

        if (_combatState.Phase == CombatPhase.PlayerTurn)
        {
            yield return new WaitForSeconds(0.6f);
            yield return StartCoroutine(EnemyTurnRoutine());
        }

        if (_combatState.Phase == CombatPhase.PlayerTurn)
        {
            StartCoroutine(ShowTurnBanner("Player's Turn"));
            SetFireButtonsEnabled(true);
        }
    }

    private IEnumerator PlayerTorpedoRoutine(EnemyCombatState target)
    {
        SetFireButtonsEnabled(false);
        SyncManeuverBonuses();

        var result = CombatResolver.PlayerFireTorpedo(_combatState, target);
        Debug.Log($"[Combat] {result.Description}");
        AppendLog(FormatPlayerAttack(result));
        AppendDetailLog($"<color={ColPlayer}>You</color>  {result.Description}");

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

        SpawnDamageText(endWorld, result);
        yield return StartCoroutine(PlayDestroyedShipEffects());
        RefreshDisplaysFromState();
        CheckCombatEnd();

        if (_combatState.Phase == CombatPhase.PlayerTurn)
            yield return StartCoroutine(PlayerTurnRepairRoutine());

        if (_combatState.Phase == CombatPhase.PlayerTurn)
        {
            yield return new WaitForSeconds(0.6f);
            yield return StartCoroutine(EnemyTurnRoutine());
        }

        if (_combatState.Phase == CombatPhase.PlayerTurn)
        {
            StartCoroutine(ShowTurnBanner("Player's Turn"));
            SetFireButtonsEnabled(true);
        }
    }

    // -----------------------------------------------------------------------
    // Repair coroutine (end of player turn)
    // -----------------------------------------------------------------------

    /// <summary>
    /// If the player has taken damage and an engineer is aboard, roll a repair
    /// check and log the result.  Only called after the player's weapon fires.
    /// </summary>
    private IEnumerator PlayerTurnRepairRoutine()
    {
        if (_combatState?.Engineer == null) yield break;

        bool isDamaged = _combatState.PlayerCurrentShields < _combatState.PlayerMaxShields
                      || _combatState.PlayerCurrentHull    < _combatState.PlayerMaxHull;
        if (!isDamaged) yield break;

        var repair = CombatResolver.EngineerRepair(_combatState);
        Debug.Log($"[Combat] {repair.Description}");
        AppendLog(FormatRepair(repair));
        AppendDetailLog($"<color={ColEngineer}>Engineer</color> {repair.Description}");

        if (repair.Attempted && repair.Success)
        {
            yield return new WaitForSeconds(0.3f);
            RefreshDisplaysFromState();
        }
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
            AppendDetailLog($"<color={ColEnemy}>Enemy</color> {result.Description}");

            var slotRT = GetSlotForEnemy(enemy);
            float waitTime = 0f;
            if (slotRT != null && playerShipImage != null)
                waitTime = LaunchEnemyAttackEffect(slotRT, result.IsBeamAttack, result.IsHit);

            yield return new WaitForSeconds(waitTime);

            SpawnDamageText(GetNoseWorld(playerShipImage), result);
            RefreshDisplaysFromState();
            CheckCombatEnd();

            if (_combatState.Phase != CombatPhase.PlayerTurn) yield break;
        }
    }

    // -----------------------------------------------------------------------
    // Turn banner
    // -----------------------------------------------------------------------

    private const float TurnBannerFadeIn  = 0.25f;
    private const float TurnBannerHold    = 0.75f;
    private const float TurnBannerFadeOut = 0.40f;

    /// <summary>
    /// Flashes the turn banner with the given text, then hides it.
    /// Safe to call even if the banner references are null.
    /// </summary>
    private IEnumerator ShowTurnBanner(string text)
    {
        if (turnBannerCanvasGroup == null || turnBannerText == null) yield break;

        turnBannerText.text           = text;
        turnBannerCanvasGroup.alpha   = 0f;
        turnBannerCanvasGroup.blocksRaycasts = false;

        // Fade in
        float t = 0f;
        while (t < TurnBannerFadeIn)
        {
            t += Time.deltaTime;
            turnBannerCanvasGroup.alpha = Mathf.Clamp01(t / TurnBannerFadeIn);
            yield return null;
        }

        yield return new WaitForSeconds(TurnBannerHold);

        // Fade out
        t = 0f;
        while (t < TurnBannerFadeOut)
        {
            t += Time.deltaTime;
            turnBannerCanvasGroup.alpha = Mathf.Clamp01(1f - t / TurnBannerFadeOut);
            yield return null;
        }

        turnBannerCanvasGroup.alpha = 0f;
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
            // Impact glow scaled to the player (target); muzzle scaled to the firing enemy.
            float impactSize = EffectSizeForRect(playerShipImage != null ? playerShipImage.rectTransform : null,
                                                 maxSize: 130f, minSize: 28f);
            float muzzleSize = EffectSizeForRect(enemyImg != null ? enemyImg.rectTransform : null,
                                                 maxSize: 60f, minSize: 28f);
            go.AddComponent<CombatBeamEffect>().Fire(
                fromWorld, toWorld, beamTexture, beamGlowSprite,
                tint: CombatBeamEffect.EnemyBeamTint,
                impactSize: impactSize, muzzleSize: muzzleSize);

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

    /// <summary>
    /// Spawns a floating damage number (or "MISS") at <paramref name="worldPos"/>.
    /// Uses the total damage dealt (shield + hull overflow).
    /// </summary>
    private void SpawnDamageText(Vector3 worldPos, CombatResolver.AttackResult result)
    {
        if (projectileContainer == null) return;
        string text = result.IsHit
            ? (result.FinalDamage + result.HullOverflow).ToString()
            : "Miss";
        CombatDamageText.Spawn(projectileContainer, worldPos, text);
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
    // How far from the sprite centre toward the nose tip to place beam endpoints.
    // 0 = centre, 1 = exact nose tip. 0.82 lands on the hull rather than the extreme tip.
    // Works for both player (no rotation, yMax = visual top = nose) and enemy
    // (180°+ rotation: TransformPoint of local yMax maps to the visual bottom = nose facing player).
    private const float NoseFraction = 0.82f;

    private static Vector3 GetNoseWorld(Image img)
    {
        if (img == null) return Vector3.zero;
        var rt   = img.rectTransform;
        float yPos = Mathf.Lerp(rt.rect.center.y, rt.rect.yMax, NoseFraction);
        return rt.TransformPoint(new Vector3(0f, yPos, 0f));
    }

    /// <summary>Returns the nose position for whatever enemy is in the given slot.</summary>
    private Vector3 GetEnemyNoseWorld(RectTransform slotRT)
    {
        var img = GetEnemyImageForSlot(slotRT);
        return img != null ? GetNoseWorld(img) : (slotRT != null ? slotRT.position : Vector3.zero);
    }

    // Beam impact glow / muzzle flash diameters must scale to the ship they land on,
    // or a fixed 130-unit glow swallows a 24×29 fighter whole (looks like a wild
    // overshoot). Roughly match the ship's smaller on-screen dimension, with a floor
    // so tiny ships still read, and a ceiling matching the original constant so large
    // ships are unchanged. NOTE: this scales the *effect*, not the ship sprite.
    private static float EffectSizeForRect(RectTransform rt, float maxSize, float minSize)
    {
        if (rt == null) return maxSize;
        float m = Mathf.Min(rt.rect.width, rt.rect.height);
        return Mathf.Clamp(m * 1.1f, minSize, maxSize);
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
        _detailLogLines.Clear();
        if (combatLogText    != null) combatLogText.text    = "";
        if (expandedLogText  != null) expandedLogText.text  = "";
        SetExpandedLogVisible(false);
    }

    /// <summary>
    /// Append a full-detail line to the expanded log (no line cap).
    /// Updates the expanded TMP_Text and scrolls to the bottom if the panel is open.
    /// </summary>
    private void AppendDetailLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        _detailLogLines.Add(line);
        if (expandedLogText == null) return;
        expandedLogText.text = string.Join("\n", _detailLogLines);
        if (_expandedLogVisible)
            StartCoroutine(ScrollExpandedLogToBottom());
    }

    private IEnumerator ScrollExpandedLogToBottom()
    {
        // Wait one frame for ContentSizeFitter to recalculate the content height.
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (expandedLogScrollRect != null)
            expandedLogScrollRect.verticalNormalizedPosition = 0f;
    }

    private void ToggleCombatLog()
    {
        Debug.Log($"[CombatViewController] ToggleCombatLog → visible={!_expandedLogVisible}, cg={expandedLogCanvasGroup}");
        SetExpandedLogVisible(!_expandedLogVisible);
    }

    private void SetExpandedLogVisible(bool visible)
    {
        _expandedLogVisible = visible;
        if (expandedLogCanvasGroup == null) return;
        expandedLogCanvasGroup.alpha          = visible ? 1f : 0f;
        expandedLogCanvasGroup.blocksRaycasts = visible;
        expandedLogCanvasGroup.interactable   = visible;
        if (visible)
            StartCoroutine(ScrollExpandedLogToBottom());
    }

    // ── Entry formatters ──────────────────────────────────────────────────────

    private static string FormatPlayerAttack(CombatResolver.AttackResult r)
    {
        if (r.AttackerRoll == 0) return null;    // no weapon — nothing to log
        string wpn   = r.IsBeamAttack ? "Beam" : "Torpedo";
        string rolls = $"<color={ColRolls}>({r.AttackerTotal} vs {r.DefenderTotal})</color>";
        if (!r.IsHit)
            return $"<color={ColPlayer}>You</color> {wpn} miss {rolls}";
        string dmg = r.HullOverflow > 0 ? $"{r.FinalDamage}+{r.HullOverflow}" : $"{r.FinalDamage}";
        string shd = r.ShieldsBroken ? " Shields Down!" : "";
        return $"<color={ColPlayer}>You</color> {wpn} hit {dmg} dmg{shd} {rolls}";
    }

    private static string FormatEnemyAttack(CombatResolver.AttackResult r)
    {
        if (r.AttackerRoll == 0) return null;
        string wpn   = r.IsBeamAttack ? "Beam" : "Torpedo";
        string rolls = $"<color={ColRolls}>({r.AttackerTotal} vs {r.DefenderTotal})</color>";
        if (!r.IsHit)
            return $"<color={ColEnemy}>Enemy</color> {wpn} miss {rolls}";
        string dmg = r.HullOverflow > 0 ? $"{r.FinalDamage}+{r.HullOverflow}" : $"{r.FinalDamage}";
        string shd = r.ShieldsBroken ? " Shields Down!" : "";
        return $"<color={ColEnemy}>Enemy</color> {wpn} hit {dmg} dmg{shd} {rolls}";
    }

    private static string FormatRepair(CombatResolver.EngineerRepairResult r)
    {
        if (!r.Attempted) return null;
        string rolls = $"<color={ColRolls}>({r.Total} vs {r.DC})</color>";
        if (!r.Success)
            return $"<color={ColEngineer}>Engineer</color> failed {rolls}";
        string what;
        if (r.ShieldsRepaired > 0 && r.HullRepaired > 0)
            what = $"shields +{r.ShieldsRepaired} hull +{r.HullRepaired}";
        else if (r.ShieldsRepaired > 0)
            what = $"shields +{r.ShieldsRepaired}";
        else if (r.HullRepaired > 0)
            what = $"hull +{r.HullRepaired}";
        else
            what = "nothing to repair";
        return $"<color={ColEngineer}>Engineer</color> {what} {rolls}";
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
        // Impact glow scaled to the target enemy; muzzle flash scaled to the player ship.
        float impactSize = EffectSizeForRect(GetEnemyImageForSlot(_currentTargetRT)?.rectTransform,
                                             maxSize: 130f, minSize: 28f);
        float muzzleSize = EffectSizeForRect(playerShipImage != null ? playerShipImage.rectTransform : null,
                                             maxSize: 60f, minSize: 28f);
        effect.Fire(startWorld, endWorld, beamTexture, beamGlowSprite,
                    tint: null, impactSize: impactSize, muzzleSize: muzzleSize);
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
        if (playerShieldText != null) playerShieldText.text = $"Shields  {playerShieldPct:0}%";
        if (playerHullText   != null) playerHullText.text   = $"Hull  {playerHullPct:0}%";
        if (targetShieldText != null) targetShieldText.text = $"Shields  {targetShieldPct:0}%";
        if (targetHullText   != null) targetHullText.text   = $"Hull  {targetHullPct:0}%";
    }

    public void OnCombatEnter()
    {
        StopAllCoroutines();   // cancel any in-flight turn routine from a previous combat
        _slotToConfig.Clear();
        _slotToState.Clear();
        _slotToImage.Clear();
        _slotToGroup.Clear();
        _destroyedSlots.Clear();
        _combatState = null;
        SetFireButtonsEnabled(true);
        if (targetInfoTitle != null) targetInfoTitle.text = "Target";
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
                targetInfoTitle.text = config.shipClass.ToString();
            else
                targetInfoTitle.text = "Target";
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

        if (enemies == null || enemies.Length == 0 || enemies.Length > 3)
        {
            Debug.LogWarning($"[CombatViewController] StartCombat: invalid enemy count {enemies?.Length}");
            return;
        }

        // Slot assignment by enemy count:
        //   1 → centre;   2 → left + right;   3 → all three
        var slots = new (RectTransform rt, CanvasGroup group, Image img)[]
        {
            (enemyLeftSlotRT,   enemyLeftGroup,   enemyLeftImage),
            (enemyCenterSlotRT, enemyCenterGroup, enemyCenterImage),
            (enemyRightSlotRT,  enemyRightGroup,  enemyRightImage),
        };

        int[] indices = enemies.Length switch
        {
            1 => new[] { 1 },          // centre only
            2 => new[] { 0, 2 },       // left + right
            _ => new[] { 0, 1, 2 },    // all three
        };

        for (int i = 0; i < enemies.Length; i++)
        {
            var config = enemies[i];
            var (rt, cg, img) = slots[indices[i]];

            // Assign sprite
            var sprite = GetEnemySprite(config);
            if (img != null)
            {
                img.sprite  = sprite;
                img.enabled = true;
                img.color   = Color.white;
            }

            // Size the slot to the sprite's natural pixel dimensions, capped so
            // even a dreadnaught fits within a reasonable arena region.
            if (rt != null && sprite != null)
            {
                const float MaxDim = 280f;
                float w = sprite.rect.width;
                float h = sprite.rect.height;
                float scale = Mathf.Min(MaxDim / Mathf.Max(w, h, 1f), 1f);
                rt.sizeDelta = new Vector2(w * scale, h * scale);
            }

            // Show the slot
            if (cg != null)
            {
                cg.alpha          = 1f;
                cg.blocksRaycasts = true;
                cg.interactable   = true;
            }

            // Register in lookup dictionaries.
            // Reuse the EnemyCombatState from _combatState.Enemies if available — this
            // keeps the reference identical to what EnemyTurnRoutine iterates, so
            // GetSlotForEnemy's reference comparison succeeds and enemy attack visuals fire.
            // Fall back to a fresh FromConfig only when running without D20 state (display mode).
            var existingState = _combatState?.Enemies.Find(e => e.Config == config);
            _slotToConfig[rt] = config;
            _slotToState[rt]  = existingState ?? EnemyCombatState.FromConfig(config);
            if (img != null) _slotToImage[rt] = img;
            if (cg  != null) _slotToGroup[rt] = cg;
        }

        // Initial target: centre for 1 and 3 enemies, left for 2
        RectTransform initialTarget = enemies.Length == 2 ? enemyLeftSlotRT : enemyCenterSlotRT;
        SetTarget(initialTarget);

        RefreshTorpedoCountDisplay();   // OnCombatEnter ran before SetCombatState, so refresh now
        StartCoroutine(ShowTurnBanner("Player's Turn"));
        Debug.Log($"[CombatViewController] StartCombat — {enemies.Length} enemy(s).");
    }

    private void HideAllEnemySlots()
    {
        HideSlot(enemyLeftGroup);
        HideSlot(enemyCenterGroup);
        HideSlot(enemyRightGroup);
    }

    private static void HideSlot(CanvasGroup cg)
    {
        if (cg == null) return;
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;
    }

    // -----------------------------------------------------------------------
    // Ship destruction effects
    // -----------------------------------------------------------------------

    /// <summary>
    /// For every enemy that just reached 0 hull, hides its sprite and plays
    /// the ship-destruction explosion and sound.  Yields for the explosion
    /// duration so the caller can wait before resolving combat end.
    ///
    /// Safe to call even when no ships were destroyed this turn — exits
    /// immediately without yielding.
    /// </summary>
    private IEnumerator PlayDestroyedShipEffects()
    {
        bool anyDestroyed = false;

        foreach (var kvp in _slotToState)
        {
            var slotRT = kvp.Key;
            var state  = kvp.Value;

            if (!state.IsDestroyed || _destroyedSlots.Contains(slotRT)) continue;

            _destroyedSlots.Add(slotRT);
            anyDestroyed = true;

            // Hide the ship image immediately so the explosion plays over nothing.
            if (_slotToImage.TryGetValue(slotRT, out var img) && img != null)
                img.enabled = false;

            // Play the destruction sound (null-safe).
            sfxSource?.PlayOneShot(explosionClip);

            // Spawn the large ship-destruction explosion centred on the slot.
            if (projectileContainer != null)
                CombatShipExplosion.SpawnAt(projectileContainer, slotRT.position, beamGlowSprite);

            AppendLog($"<color={ColEnemy}>Destroyed!</color>");
            AppendDetailLog($"<color={ColEnemy}>Enemy</color> ship destroyed!");
        }

        if (anyDestroyed)
            yield return new WaitForSeconds(CombatShipExplosion.Duration);
    }

    // -----------------------------------------------------------------------
    // DGB sprite lookup
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the best-matching sprite from <see cref="dgbShipSprites"/> for
    /// the given config.  Tries the requested variant first, then falls back to
    /// variant 1.  Returns null if no match is found (caller shows a blank slot).
    /// </summary>
    private Sprite GetEnemySprite(EnemyShipConfig config)
    {
        if (dgbShipSprites == null || dgbShipSprites.Length == 0) return null;

        // Skip unavailable color/class combos immediately.
        if (UnavailableCombos.Contains((config.color, config.shipClass)))
        {
            Debug.LogWarning($"[CombatViewController] No sprite for {config.color} {config.shipClass} (unavailable combo).");
            return null;
        }

        string colorStr = config.color.ToString().ToLowerInvariant();
        string classStr = config.shipClass.ToString().ToLowerInvariant();
        string target   = $"{colorStr}_{classStr}_{config.variant}";

        // Exact match
        foreach (var s in dgbShipSprites)
            if (s != null && s.name.Equals(target, System.StringComparison.OrdinalIgnoreCase))
                return s;

        // Fall back to variant 1
        if (config.variant != 1)
        {
            string fallback = $"{colorStr}_{classStr}_1";
            foreach (var s in dgbShipSprites)
                if (s != null && s.name.Equals(fallback, System.StringComparison.OrdinalIgnoreCase))
                    return s;
        }

        Debug.LogWarning($"[CombatViewController] No sprite found for {target}");
        return null;
    }
}
