using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central game manager. Singleton that persists across scenes.
/// Owns references to sub-managers and drives high-level game flow.
///
/// Scene setup: Create an empty GameObject called "GameManager" in the
/// MainMenu scene, attach this script, and add DatabaseManager and
/// AudioManager as child GameObjects.
/// </summary>
public class GameManager : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Singleton
    // -----------------------------------------------------------------------
    public static GameManager Instance { get; private set; }

    // -----------------------------------------------------------------------
    // Game state
    // -----------------------------------------------------------------------
    public enum GameState
    {
        MainMenu,
        Loading,
        InGame,
        Paused,
        GameOver
    }

    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    // -----------------------------------------------------------------------
    // Sub-managers (assigned via child GameObjects in the Inspector)
    // -----------------------------------------------------------------------
    public DatabaseManager Database { get; private set; }
    // public AudioManager Audio { get; private set; }  // Uncomment when ready

    // -----------------------------------------------------------------------
    // Active game session data
    // -----------------------------------------------------------------------
    public SaveGame  CurrentSave    { get; private set; }
    public Ship      PlayerShip     { get; private set; }
    public Character PlayerCaptain  { get; private set; }
    public Character PlayerPilot    { get; private set; }
    public Character PlayerEngineer { get; private set; }

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeManagers();
    }

    private void InitializeManagers()
    {
        Database = GetComponentInChildren<DatabaseManager>();
        Database?.Initialize();

        // Audio = GetComponentInChildren<AudioManager>();
    }

    // -----------------------------------------------------------------------
    // Game flow
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates all new-game data (save record, captain, crew, ship, star system)
    /// and populates the Player* properties, but does NOT load the game scene.
    /// Call this before playing an opening interlude, then call
    /// <see cref="LaunchNewGame"/> when the interlude completes.
    /// </summary>
    public void PrepareNewGame(string captainName, string shipName = "Horizon", string portraitFileName = "")
    {
        if (Database == null || Database.SaveGames == null)
        {
            Debug.LogError("[GameManager] PrepareNewGame: DatabaseManager is not ready.");
            return;
        }

        SetState(GameState.Loading);

        try
        {
            // Build the save record
            CurrentSave = new SaveGame
            {
                CaptainName = captainName,
                ShipName    = shipName,
                Credits     = Constants.Economy.StartingCredits,
                CreatedAt   = System.DateTime.Now,
                LastSavedAt = System.DateTime.Now
            };
            Database.SaveGames.Insert(CurrentSave);
            Debug.Log($"[GameManager] Save record created (Id={CurrentSave.Id}).");

            // Create starting captain (with chosen portrait)
            PlayerCaptain            = CharacterFactory.CreateCaptain(captainName, portraitFileName);
            PlayerCaptain.SaveGameId = CurrentSave.Id;
            Database.Characters.Insert(PlayerCaptain);
            CurrentSave.CaptainId    = PlayerCaptain.Id;

            // Create starting pilot and engineer
            PlayerPilot               = CharacterFactory.CreateStartingPilot();
            PlayerEngineer            = CharacterFactory.CreateStartingEngineer();
            PlayerPilot.SaveGameId    = CurrentSave.Id;
            PlayerEngineer.SaveGameId = CurrentSave.Id;
            Database.Characters.Insert(PlayerPilot);
            Database.Characters.Insert(PlayerEngineer);
            Debug.Log($"[GameManager] Crew created: {PlayerPilot.Name}, {PlayerEngineer.Name}.");

            // Create starting ship
            PlayerShip            = Ship.CreateStartingShip(shipName);
            PlayerShip.SaveGameId = CurrentSave.Id;
            Database.Ships.Insert(PlayerShip);
            CurrentSave.ShipId    = PlayerShip.Id;

            // Equip every slot with a starting Mk I component so the ship begins
            // fully outfitted (rather than showing "Not installed" slots).
            foreach (var slotName in new List<string>(PlayerShip.EquipmentSlots.Keys))
            {
                var startingItem = new EquipmentItem
                {
                    SaveGameId        = CurrentSave.Id,
                    Name              = $"{slotName} {Constants.Ship.TierLabel(EquipmentTier.MkI)}",
                    Description       = Constants.Ship.EquipmentSlots.Descriptions.TryGetValue(slotName, out var d) ? d : "",
                    EquipmentType     = Constants.Ship.SlotEquipmentType(slotName),
                    Tier              = EquipmentTier.MkI,
                    IsInstalled       = true,
                    InstalledOnShipId = PlayerShip.Id,
                    InstalledInSlot   = slotName,
                    Condition         = 100,
                };
                Database.Equipment.Insert(startingItem);          // assigns Id
                PlayerShip.InstallEquipment(slotName, startingItem.Id);
            }
            Database.Ships.Update(PlayerShip);

            // ── Sol — starting system ─────────────────────────────────────
            var sol        = StarSystemGenerator.GenerateSolSystem();
            sol.SaveGameId = CurrentSave.Id;
            Database.StarSystems.Insert(sol);
            Debug.Log($"[GameManager] Sol inserted (Id={sol.Id}).");

            var solPOIs = StarSystemGenerator.GenerateSolPOIs(sol, CurrentSave.Id);
            foreach (var poi in solPOIs) Database.POIs.Insert(poi);
            Debug.Log($"[GameManager] {solPOIs.Count} Sol POIs inserted.");

            CurrentSave.CurrentSystemId = sol.Id;

            // ── Sol cluster — Alpha Centauri & Barnard's Star ─────────────
            InsertSystemWithPOIs(StarSystemGenerator.GenerateAlphaCentauri(), CurrentSave.Id,
                                  StarSystemGenerator.GenerateAlphaCentauriPOIs);
            InsertSystemWithPOIs(StarSystemGenerator.GenerateBarnardsStar(), CurrentSave.Id);

            // ── Procedural systems: 6 FTL-tier clusters from CSV catalogue ──
            //
            // Each cluster sits in a ring at increasing radial distance from Sol.
            // The tier number equals the minimum FTL drive tier (Mk I–Mk VI) the
            // player must have installed to reach those systems.
            //
            // Ring radii are in normalised galaxy-map units, centred on Sol.
            //
            //  Tier 1 (Mk I ): 0.04–0.07  — 4 systems  (same neighbourhood as Alpha/Barnard's)
            //  Tier 2 (Mk II): 0.09–0.12  — 4 systems
            //  Tier 3 (MkIII): 0.14–0.17  — 4 systems
            //  Tier 4 (Mk IV): 0.19–0.22  — 4 systems
            //  Tier 5 (Mk V ): 0.24–0.28  — 3 systems
            //  Tier 6 (Mk VI): 0.30–0.37  — 3 systems
            //                                ─────────
            //                                22 systems total

            var catalogueAsset = Resources.Load<TextAsset>("Data/systems");
            if (catalogueAsset != null)
            {
                var catalogue = StarSystemGenerator.ParseSystemsCSV(catalogueAsset.text);

                // Exclude the three hand-crafted systems.
                var excluded = new System.Collections.Generic.HashSet<string>(
                    System.StringComparer.OrdinalIgnoreCase)
                    { "Sol", "Alpha Centauri", "Barnard's Star" };

                // Unique seed per run so repeated new-game calls produce different galaxies.
                int galaxySeed = CurrentSave.Id ^ (int)System.DateTime.UtcNow.Ticks ^ 0x1337;
                CurrentSave.GalaxySeed = galaxySeed;
                var rng        = new System.Random(galaxySeed);
                var candidates = new System.Collections.Generic.List<(string name, StarType starType)>();
                foreach (var entry in catalogue)
                    if (!excluded.Contains(entry.name)) candidates.Add(entry);
                candidates.Sort((_, __) => rng.Next(-1, 2));   // Fisher-Yates-ish shuffle

                // Tier definitions: (innerR, outerR, systemCount, minSpacing)
                // All radii are measured from Sol's position, not the galaxy centre.
                // Tier 1 starts at 0.10 so it clears Alpha Centauri (~0.039) and
                // Barnard's Star (~0.064) with a comfortable gap.
                // minSpacing increases with tier because outer rings have more angular room.
                var tiers = new (float innerR, float outerR, int count, float minSpacing)[]
                {
                    (0.10f, 0.14f, 3, 0.09f),   // Tier 1 — Mk I
                    (0.16f, 0.20f, 3, 0.09f),   // Tier 2 — Mk II
                    (0.22f, 0.27f, 4, 0.10f),   // Tier 3 — Mk III
                    (0.29f, 0.34f, 4, 0.10f),   // Tier 4 — Mk IV
                    (0.36f, 0.42f, 3, 0.11f),   // Tier 5 — Mk V
                    (0.44f, 0.51f, 3, 0.11f),   // Tier 6 — Mk VI
                };

                // Build a growing avoid-list so clusters don't overlap each other
                // or the three hand-crafted systems.
                var allPlaced = new System.Collections.Generic.List<(float gx, float gy)>
                {
                    (StarSystemGenerator.SolGX,     StarSystemGenerator.SolGY),
                    (StarSystemGenerator.AlphaGX,   StarSystemGenerator.AlphaGY),
                    (StarSystemGenerator.BarnardsGX, StarSystemGenerator.BarnardsGY),
                };

                int catalogueIdx  = 0;
                int totalInserted = 0;

                for (int tier = 0; tier < tiers.Length; tier++)
                {
                    int ftlTier = tier + 1;
                    var (innerR, outerR, clusterCount, minSpacing) = tiers[tier];

                    // Don't exceed available catalogue entries.
                    int toPlace = System.Math.Min(clusterCount, candidates.Count - catalogueIdx);
                    if (toPlace <= 0) break;

                    var positions = StarSystemGenerator.GenerateClusterPositions(
                        StarSystemGenerator.SolGX,
                        StarSystemGenerator.SolGY,
                        innerR, outerR,
                        toPlace,
                        seed:           galaxySeed + tier * 1000,
                        avoidPositions: allPlaced.ToArray(),
                        minSpacing:     minSpacing);

                    for (int i = 0; i < toPlace; i++)
                    {
                        var (name, starType) = candidates[catalogueIdx];
                        var sys = StarSystemGenerator.GenerateExtraSystem(
                            name, starType,
                            positions[i].gx, positions[i].gy,
                            seed:            galaxySeed + 200 + catalogueIdx,
                            saveGameId:      CurrentSave.Id,
                            ftlTierRequired: ftlTier);
                        InsertSystemWithPOIs(sys, CurrentSave.Id);
                        allPlaced.Add((positions[i].gx, positions[i].gy));
                        catalogueIdx++;
                        totalInserted++;
                    }
                }
                Debug.Log($"[GameManager] {totalInserted} extra systems inserted across 6 FTL-tier clusters.");
            }
            else
            {
                Debug.LogWarning("[GameManager] Data/systems.csv not found in Resources — " +
                                 "only Sol, Alpha Centauri, and Barnard's Star were created.");
            }

            Database.SaveGames.Update(CurrentSave);
            Debug.Log("[GameManager] PrepareNewGame complete.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GameManager] PrepareNewGame failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    /// <summary>
    /// Inserts a star system into the database (setting its SaveGameId) then
    /// generates and inserts all of its POIs. Shared by PrepareNewGame for all
    /// non-Sol systems so the same flow isn't duplicated for each one.
    /// </summary>
    /// <summary>
    /// Returns the tier (1–6) of the FTL Drive currently installed on the player's ship,
    /// or 0 if no drive is installed.  Used to gate travel to higher-tier systems.
    /// </summary>
    public int GetPlayerFtlTier()
    {
        if (PlayerShip == null || Database == null) return 0;

        var slots = PlayerShip.EquipmentSlots;
        if (!slots.TryGetValue(Constants.Ship.EquipmentSlots.FtlDrive, out int itemId) || itemId <= 0)
            return 0;

        var item = Database.Equipment.Query()
                           .Where(e => e.Id == itemId)
                           .FirstOrDefault();
        return item != null ? (int)item.Tier : 0;
    }

    private void InsertSystemWithPOIs(StarSystem system, int saveGameId,
        System.Func<StarSystem, int, List<PointOfInterest>> poiGenerator = null)
    {
        system.SaveGameId = saveGameId;
        Database.StarSystems.Insert(system);
        poiGenerator ??= StarSystemGenerator.GeneratePOIsForSystem;
        var pois = poiGenerator(system, saveGameId);
        foreach (var poi in pois) Database.POIs.Insert(poi);
    }

    /// <summary>
    /// Transitions to the game scene. Call this after <see cref="PrepareNewGame"/>
    /// and any opening interlude have completed.
    /// </summary>
    public void LaunchNewGame() => LoadGameScene();

    /// <summary>
    /// Convenience method: prepares a new game and immediately loads the game scene
    /// without playing any interlude. Prefer <see cref="PrepareNewGame"/> +
    /// <see cref="LaunchNewGame"/> when an opening interlude is involved.
    /// </summary>
    public void StartNewGame(string captainName, string shipName = "Horizon", string portraitFileName = "")
    {
        PrepareNewGame(captainName, shipName, portraitFileName);
        LaunchNewGame();
    }

    /// <summary>Resume an existing save.</summary>
    public void LoadGame(int saveId)
    {
        SetState(GameState.Loading);

        CurrentSave     = Database.SaveGames.Get(saveId);
        PlayerCaptain   = Database.Characters.Get(CurrentSave.CaptainId);
        PlayerShip      = Database.Ships.Get(CurrentSave.ShipId);
        PlayerPilot     = Database.GetCrewByRole(CurrentSave.Id, Constants.Crew.Roles.Pilot);
        PlayerEngineer  = Database.GetCrewByRole(CurrentSave.Id, Constants.Crew.Roles.Engineer);

        LoadGameScene();
    }

    /// <summary>Persist current state to the database.</summary>
    public void SaveGame()
    {
        if (CurrentSave == null) return;

        CurrentSave.LastSavedAt = System.DateTime.Now;
        Database.SaveGames.Update(CurrentSave);
        Database.Ships.Update(PlayerShip);
        Database.Characters.Update(PlayerCaptain);

        Debug.Log("[GameManager] Game saved.");
    }

    public void ReturnToMainMenu()
    {
        EventBus.Clear();
        SetState(GameState.MainMenu);
        SceneManager.LoadScene(Constants.Scenes.MainMenu);
    }

    public void PauseGame()
    {
        if (CurrentState != GameState.InGame) return;
        SetState(GameState.Paused);
        Time.timeScale = 0f;
    }
    public void UnpauseGame()
    {
        if (CurrentState != GameState.Paused) return;
        SetState(GameState.InGame);
        Time.timeScale = 1f;
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        Debug.Log($"[GameManager] State → {newState}");
    }

    private void LoadGameScene()
    {
        SetState(GameState.InGame);
        SceneManager.LoadScene(Constants.Scenes.Game);
    }
}
