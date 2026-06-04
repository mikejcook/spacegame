using System;
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
/// </summary>
public class CombatViewController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Display-size constants (canvas units, 1920×990 reference)
    // Baked from: reduce previous large slot by 1/3, then scale Medium/Small.
    // -----------------------------------------------------------------------

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

    private float _playerShieldPct = 100f;
    private float _playerHullPct   = 100f;
    private float _targetShieldPct = 100f;
    private float _targetHullPct   = 100f;

    // The slot RectTransform that is currently targeted — updated by SetTarget().
    private RectTransform _currentTargetRT;

    // Maps each slot RectTransform to the EnemyShipConfig it displays, so
    // SetTarget can update the title label with the correct ship class.
    private readonly Dictionary<RectTransform, EnemyShipConfig> _slotToConfig = new();

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

        RefreshDisplays();
    }

    /// <summary>Fire torpedoes at the current target.</summary>
    public void FireTorpedoes()
    {
        if (_currentTargetRT == null) return;
        PulseCrosshair();
        sfxSource?.PlayOneShot(torpedoClip);
        // Missiles are larger and slower than plasma bolts.
        LaunchProjectile(missileSprite, new Vector2(40f, 96f), duration: 0.55f);
        Debug.Log("[CombatViewController] Fire Torpedoes!");
        // TODO: apply torpedo damage to target
    }

    /// <summary>Fire beam weapon at the current target.</summary>
    public void FireBeamWeapon()
    {
        if (_currentTargetRT == null) return;
        PulseCrosshair();
        sfxSource?.PlayOneShot(beamWeaponClip);
        LaunchBeamWeapon();
        Debug.Log("[CombatViewController] Fire Beam Weapon!");
        // TODO: apply beam damage to target
    }

    // -----------------------------------------------------------------------
    // Projectile helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spawn a projectile that travels from the player ship to the current target.
    /// Safe to call even when projectileContainer or the sprites are not wired —
    /// it just skips silently so combat doesn't break on machines without the assets.
    /// </summary>
    private void LaunchProjectile(Sprite sprite, Vector2 displaySize, float duration)
    {
        if (projectileContainer == null || playerShipImage == null || _currentTargetRT == null)
            return;

        // Origin: world-space centre of the player ship image.
        Vector3 startWorld = playerShipImage.transform.position;

        // Destination: world-space centre of the targeted enemy slot.
        Vector3 endWorld = _currentTargetRT.position;

        // Spawn as a plain RectTransform child of the projectile layer.
        var go   = new GameObject("Projectile", typeof(RectTransform));
        go.transform.SetParent(projectileContainer, worldPositionStays: false);

        var proj = go.AddComponent<CombatProjectile>();
        proj.Launch(startWorld, endWorld, sprite, displaySize, duration);
    }

    /// <summary>
    /// Spawn a CombatBeamEffect from the player ship to the current target.
    /// Beam line uses laser_noise00.png; impact + muzzle glows use glow_round00.png.
    /// </summary>
    private void LaunchBeamWeapon()
    {
        if (projectileContainer == null || playerShipImage == null || _currentTargetRT == null)
            return;

        // Origin: nose = top-centre of the player ship image (sprite faces up).
        var shipCorners = new Vector3[4];
        playerShipImage.rectTransform.GetWorldCorners(shipCorners);
        // corners: [0]=bottom-left  [1]=top-left  [2]=top-right  [3]=bottom-right
        Vector3 noseWorld = (shipCorners[1] + shipCorners[2]) * 0.5f;

        var go = new GameObject("BeamEffect", typeof(RectTransform));
        go.transform.SetParent(projectileContainer, worldPositionStays: false);

        var effect = go.AddComponent<CombatBeamEffect>();
        effect.Fire(
            noseWorld,
            _currentTargetRT.position,
            beamTexture,
            beamGlowSprite
        );
    }

    /// <summary>Set player shield/hull percentages and refresh the header display.</summary>
    public void SetPlayerStats(float shieldPct, float hullPct)
    {
        _playerShieldPct = Mathf.Clamp(shieldPct, 0f, 100f);
        _playerHullPct   = Mathf.Clamp(hullPct,   0f, 100f);
        RefreshDisplays();
    }

    /// <summary>Set the targeted enemy's shield/hull percentages.</summary>
    public void SetTargetStats(float shieldPct, float hullPct)
    {
        _targetShieldPct = Mathf.Clamp(shieldPct, 0f, 100f);
        _targetHullPct   = Mathf.Clamp(hullPct,   0f, 100f);
        RefreshDisplays();
    }

    private void RefreshDisplays()
    {
        if (playerShieldText != null) playerShieldText.text = $"SHIELDS  {_playerShieldPct:0}%";
        if (playerHullText   != null) playerHullText.text   = $"HULL  {_playerHullPct:0}%";
        if (targetShieldText != null) targetShieldText.text = $"SHIELDS  {_targetShieldPct:0}%";
        if (targetHullText   != null) targetHullText.text   = $"HULL  {_targetHullPct:0}%";
    }

    public void OnCombatEnter()
    {
        _slotToConfig.Clear();
        _playerShieldPct = 100f;
        _playerHullPct   = 100f;
        _targetShieldPct = 100f;
        _targetHullPct   = 100f;
        if (targetInfoTitle != null) targetInfoTitle.text = "TARGET";
        RefreshDisplays();
        Debug.Log("[CombatViewController] Combat entered.");
    }

    public void OnCombatExit()   { HideAllEnemySlots(); combatCrosshair?.Hide(); _currentTargetRT = null; Debug.Log("[CombatViewController] Combat exited."); }

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

        // Reset target stats to 100% for the newly selected enemy (placeholder until real combat data)
        _targetShieldPct = 100f;
        _targetHullPct   = 100f;
        RefreshDisplays();
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

        RectTransform defaultTarget = null;
        switch (count)
        {
            case 1:
                ShowEnemySlot(enemyCenterGroup, enemyCenterSlotRT, enemyCenterImage, enemies[0]);
                _slotToConfig[enemyCenterSlotRT] = enemies[0];
                defaultTarget = enemyCenterSlotRT;
                break;
            case 2:
                ShowEnemySlot(enemyLeftGroup,  enemyLeftSlotRT,  enemyLeftImage,  enemies[0]);
                ShowEnemySlot(enemyRightGroup, enemyRightSlotRT, enemyRightImage, enemies[1]);
                _slotToConfig[enemyLeftSlotRT]  = enemies[0];
                _slotToConfig[enemyRightSlotRT] = enemies[1];
                defaultTarget = enemyLeftSlotRT;
                break;
            case 3:
                ShowEnemySlot(enemyLeftGroup,   enemyLeftSlotRT,   enemyLeftImage,   enemies[0]);
                ShowEnemySlot(enemyCenterGroup, enemyCenterSlotRT, enemyCenterImage, enemies[1]);
                ShowEnemySlot(enemyRightGroup,  enemyRightSlotRT,  enemyRightImage,  enemies[2]);
                _slotToConfig[enemyLeftSlotRT]   = enemies[0];
                _slotToConfig[enemyCenterSlotRT] = enemies[1];
                _slotToConfig[enemyRightSlotRT]  = enemies[2];
                defaultTarget = enemyCenterSlotRT;  // always open on the middle enemy
                break;
        }

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
