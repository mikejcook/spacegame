using System;
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
///   Each slot's RectTransform sizeDelta is resized at runtime based on
///   EnemyShipConfig.displaySize (Small ≈ player ship, Medium, Large).
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

    private static readonly Vector2 DisplaySizeLarge  = new Vector2(240f, 213f);
    private static readonly Vector2 DisplaySizeMedium = new Vector2(160f, 142f);
    // Small ≈ player ship footprint (player slot is ~144 × 178 canvas units)
    private static readonly Vector2 DisplaySizeSmall  = new Vector2(128f, 114f);

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

    public void OnCombatEnter()  => Debug.Log("[CombatViewController] Combat entered.");
    public void OnCombatExit()   { HideAllEnemySlots(); Debug.Log("[CombatViewController] Combat exited."); }

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

        switch (count)
        {
            case 1:
                ShowEnemySlot(enemyCenterGroup, enemyCenterSlotRT, enemyCenterImage, enemies[0]);
                break;
            case 2:
                ShowEnemySlot(enemyLeftGroup,  enemyLeftSlotRT,  enemyLeftImage,  enemies[0]);
                ShowEnemySlot(enemyRightGroup, enemyRightSlotRT, enemyRightImage, enemies[1]);
                break;
            case 3:
                ShowEnemySlot(enemyLeftGroup,   enemyLeftSlotRT,   enemyLeftImage,   enemies[0]);
                ShowEnemySlot(enemyCenterGroup, enemyCenterSlotRT, enemyCenterImage, enemies[1]);
                ShowEnemySlot(enemyRightGroup,  enemyRightSlotRT,  enemyRightImage,  enemies[2]);
                break;
        }
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

        var sprite = FindSprite(BuildSpriteName(color, shipClass, variant));
        // Fall back to variant 1 (the base variant) if the requested one doesn't exist
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

        if (slotRT != null)
            slotRT.sizeDelta = GetDisplaySize(config.displaySize);

        if (img != null)
        {
            var sprite = GetEnemySprite(config.color, config.shipClass, config.variant);
            img.sprite = sprite;
            img.color  = sprite != null ? Color.white : new Color(0.6f, 0.2f, 0.2f, 0.4f);
        }

        SetSlotVisible(group, true);
    }

    private static Vector2 GetDisplaySize(CombatShipDisplaySize displaySize) => displaySize switch
    {
        CombatShipDisplaySize.Large  => DisplaySizeLarge,
        CombatShipDisplaySize.Medium => DisplaySizeMedium,
        _                            => DisplaySizeSmall,
    };

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
