using System;
using System.Collections.Generic;
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

    [Header("Crosshair")]
    [SerializeField] private CombatCrosshair combatCrosshair;

    [Header("Enemy Tap Buttons — wired by GameSceneSetup")]
    [SerializeField] private Button enemyLeftButton;
    [SerializeField] private Button enemyCenterButton;
    [SerializeField] private Button enemyRightButton;

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
    }

    public void OnCombatEnter()  => Debug.Log("[CombatViewController] Combat entered.");
    public void OnCombatExit()   { HideAllEnemySlots(); combatCrosshair?.Hide(); Debug.Log("[CombatViewController] Combat exited."); }

    /// <summary>Trigger a single crosshair pulse — call when the player fires.</summary>
    public void PulseCrosshair() => combatCrosshair?.Pulse();

    /// <summary>Move the crosshair onto the given slot. Also called internally on tap.</summary>
    public void SetTarget(RectTransform slotRT)
    {
        if (slotRT == null) return;
        combatCrosshair?.SnapToSlot(slotRT);
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

        RectTransform firstSlot = null;
        switch (count)
        {
            case 1:
                ShowEnemySlot(enemyCenterGroup, enemyCenterSlotRT, enemyCenterImage, enemies[0]);
                firstSlot = enemyCenterSlotRT;
                break;
            case 2:
                ShowEnemySlot(enemyLeftGroup,  enemyLeftSlotRT,  enemyLeftImage,  enemies[0]);
                ShowEnemySlot(enemyRightGroup, enemyRightSlotRT, enemyRightImage, enemies[1]);
                firstSlot = enemyLeftSlotRT;
                break;
            case 3:
                ShowEnemySlot(enemyLeftGroup,   enemyLeftSlotRT,   enemyLeftImage,   enemies[0]);
                ShowEnemySlot(enemyCenterGroup, enemyCenterSlotRT, enemyCenterImage, enemies[1]);
                ShowEnemySlot(enemyRightGroup,  enemyRightSlotRT,  enemyRightImage,  enemies[2]);
                firstSlot = enemyLeftSlotRT;
                break;
        }

        // Auto-target the first active enemy.
        SetTarget(firstSlot);
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
