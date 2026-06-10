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

    /// <summary>
    /// True after PrepareNewGame until the player has spent their first-level skill points.
    /// SystemViewController checks this on Start() to show the captain level-up screen
    /// before entering the system view.
    /// </summary>
    public bool IsFirstLaunch { get; private set; }

    /// <summary>Called by SystemViewController after the first-launch level-up is complete.</summary>
    public void AcknowledgeFirstLaunch() => IsFirstLaunch = false;

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
                CaptainName  = captainName,
                ShipName     = shipName,
                Credits      = Constants.Economy.StartingCredits,
                Fuel         = Constants.Resources.StartingFuel,
                CrewLoyalty  = Constants.Resources.StartingLoyalty,
                CreatedAt    = System.DateTime.Now,
                LastSavedAt  = System.DateTime.Now
            };
            Database.SaveGames.Insert(CurrentSave);
            Debug.Log($"[GameManager] Save record created (Id={CurrentSave.Id}).");

            // Create starting captain (with chosen portrait)
            var captain            = CharacterFactory.CreateCaptain(captainName, portraitFileName);
            captain.SaveGameId     = CurrentSave.Id;
            Database.Characters.Insert(captain);
            CurrentSave.CaptainId  = captain.Id;

            // Load portrait library and track used portraits to avoid duplicates
            var portraitLibrary = Resources.Load<PortraitLibrary>("PortraitLibrary");
            var usedPortraits   = new System.Collections.Generic.HashSet<string>();
            if (!string.IsNullOrEmpty(captain.PortraitId))
                usedPortraits.Add(captain.PortraitId);

            // Create starting pilot and engineer with gender-appropriate, non-duplicate portraits
            var pilot    = CharacterFactory.CreateStartingPilot(portraitLibrary, usedPortraits);
            if (!string.IsNullOrEmpty(pilot.PortraitId))
                usedPortraits.Add(pilot.PortraitId);
            var engineer = CharacterFactory.CreateStartingEngineer(portraitLibrary, usedPortraits);
            pilot.SaveGameId    = CurrentSave.Id;
            engineer.SaveGameId = CurrentSave.Id;
            Database.Characters.Insert(pilot);
            Database.Characters.Insert(engineer);
            Debug.Log($"[GameManager] Crew created: {pilot.Name}, {engineer.Name}.");

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
            foreach (var poi in solPOIs) { poi.IsExplored = true; Database.POIs.Insert(poi); }
            Debug.Log($"[GameManager] {solPOIs.Count} Sol POIs inserted (all pre-explored).");

            CurrentSave.CurrentSystemId = sol.Id;

            // ── Sol cluster — Alpha Centauri & Barnard's Star ─────────────
            // These are settled, well-known systems — mark all POIs explored at game start.
            InsertSystemWithPOIs(StarSystemGenerator.GenerateAlphaCentauri(), CurrentSave.Id,
                                  StarSystemGenerator.GenerateAlphaCentauriPOIs, preExplored: true);
            InsertSystemWithPOIs(StarSystemGenerator.GenerateBarnardsStar(), CurrentSave.Id,
                                  preExplored: true);

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

                    bool tierHabitableGuaranteed = false;
                    for (int i = 0; i < toPlace; i++)
                    {
                        var (name, starType) = candidates[catalogueIdx];
                        var sys = StarSystemGenerator.GenerateExtraSystem(
                            name, starType,
                            positions[i].gx, positions[i].gy,
                            seed:            galaxySeed + 200 + catalogueIdx,
                            saveGameId:      CurrentSave.Id,
                            ftlTierRequired: ftlTier);

                        // Guarantee exactly one habitable planet per tier cluster.
                        // Use the first system in the tier as the designated carrier;
                        // once set, subsequent systems in the same tier generate freely.
                        bool guaranteeHabitable = !tierHabitableGuaranteed;
                        InsertSystemWithPOIs(sys, CurrentSave.Id,
                            poiGenerator: (s, id) => StarSystemGenerator.GeneratePOIsForSystem(s, id, guaranteeHabitable));
                        tierHabitableGuaranteed = true;

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
            IsFirstLaunch = true;
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
        System.Func<StarSystem, int, List<PointOfInterest>> poiGenerator = null,
        bool preExplored = false)
    {
        system.SaveGameId = saveGameId;
        Database.StarSystems.Insert(system);
        if (poiGenerator == null) poiGenerator = (s, id) => StarSystemGenerator.GeneratePOIsForSystem(s, id);
        var pois = poiGenerator(system, saveGameId);
        foreach (var poi in pois)
        {
            if (preExplored) poi.IsExplored = true;
            Database.POIs.Insert(poi);
        }
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
        PlayerShip      = Database.Ships.Get(CurrentSave.ShipId);

        LoadGameScene();
    }

    /// <summary>Persist current state to the database.</summary>
    public void SaveGame()
    {
        if (CurrentSave == null) return;

        CurrentSave.LastSavedAt = System.DateTime.Now;
        Database.SaveGames.Update(CurrentSave);
        Database.Ships.Update(PlayerShip);
        var captain = Database.Characters.Get(CurrentSave.CaptainId);
        if (captain != null) Database.Characters.Update(captain);

        Debug.Log("[GameManager] Game saved.");
    }

    // -----------------------------------------------------------------------
    // Resource management
    // -----------------------------------------------------------------------

    /// <summary>Adds salvage and persists.</summary>
    public void AddSalvage(int amount)
    {
        if (CurrentSave == null || amount <= 0) return;
        CurrentSave.Salvage += amount;
        SaveGame();
    }

    /// <summary>Removes salvage (clamped to 0) and persists.</summary>
    public void RemoveSalvage(int amount)
    {
        if (CurrentSave == null || amount <= 0) return;
        CurrentSave.Salvage = Mathf.Max(0, CurrentSave.Salvage - amount);
        SaveGame();
    }

    /// <summary>Adds fuel up to the cap and persists.</summary>
    public void AddFuel(int amount)
    {
        if (CurrentSave == null || amount <= 0) return;
        CurrentSave.Fuel = Mathf.Min(Constants.Resources.MaxFuel, CurrentSave.Fuel + amount);
        SaveGame();
    }

    /// <summary>
    /// Consumes fuel (clamped to 0) and persists.
    /// Returns the actual amount consumed (may be less than requested if tank is low).
    /// </summary>
    public int ConsumeFuel(int amount)
    {
        if (CurrentSave == null || amount <= 0) return 0;
        int before = CurrentSave.Fuel;
        CurrentSave.Fuel = Mathf.Max(0, CurrentSave.Fuel - amount);
        SaveGame();
        return before - CurrentSave.Fuel;
    }

    /// <summary>Adds crew loyalty up to the cap and persists.</summary>
    public void AddLoyalty(int amount)
    {
        if (CurrentSave == null || amount <= 0) return;
        CurrentSave.CrewLoyalty = Mathf.Min(Constants.Resources.MaxLoyalty, CurrentSave.CrewLoyalty + amount);
        SaveGame();
    }

    /// <summary>
    /// Reduces crew loyalty (clamped to 0) and persists.
    /// Returns true if loyalty hit or crossed the mutiny threshold.
    /// </summary>
    public bool RemoveLoyalty(int amount)
    {
        if (CurrentSave == null || amount <= 0) return false;
        CurrentSave.CrewLoyalty = Mathf.Max(0, CurrentSave.CrewLoyalty - amount);
        SaveGame();
        return CurrentSave.CrewLoyalty <= Constants.Resources.MutinyThreshold;
    }

    // -----------------------------------------------------------------------
    // Travel time
    // -----------------------------------------------------------------------

    /// <summary>
    /// Computes the number of in-game days to travel between two star systems
    /// via FTL drive, with a minimum of <see cref="Constants.Travel.MinTravelDays"/>.
    /// Uses the same galaxy-distance formula as GalaxyViewController (y weighted
    /// by 0.15 to reflect the landscape galaxy layout).
    /// </summary>
    public static float CalculateTravelDays(StarSystem from, StarSystem to)
    {
        if (from == null || to == null) return Constants.Travel.MinTravelDays;

        float dx = to.GalaxyX - from.GalaxyX;
        float dy = to.GalaxyY - from.GalaxyY;
        float distanceLY = Mathf.Sqrt(dx * dx + dy * dy * 0.15f) * Constants.Travel.LightYearsPerUnit;
        float days = distanceLY / Constants.Travel.FtlLyPerDay;
        return Mathf.Max(Constants.Travel.MinTravelDays, days);
    }

    /// <summary>
    /// Adds the FTL travel time from <paramref name="from"/> to <paramref name="to"/>
    /// to DaysPassed and persists the save.
    /// </summary>
    public void AddTravelTime(StarSystem from, StarSystem to)
    {
        if (CurrentSave == null) return;
        CurrentSave.DaysPassed += CalculateTravelDays(from, to);
        SaveGame();
    }

    /// <summary>
    /// Adds the minimum travel time for an in-system sublight hop (POI to POI).
    /// Adds sublight travel time for an in-system hop between two POIs.
    /// Distance is computed from their normalised SystemX/Y positions (centre = 0.5,0.5).
    /// A null <paramref name="from"/> is treated as the star centre (0.5, 0.5).
    /// Result is clamped to a minimum of <see cref="Constants.Travel.MinTravelDays"/>.
    /// </summary>
    public void AddInSystemTravelTime(PointOfInterest from, PointOfInterest to)
    {
        if (CurrentSave == null) return;
        CurrentSave.DaysPassed += CalculateInSystemTravelDays(from, to);
        SaveGame();
    }

    public static float CalculateInSystemTravelDays(PointOfInterest from, PointOfInterest to)
    {
        float fromX = from?.SystemX ?? 0.5f;
        float fromY = from?.SystemY ?? 0.5f;
        float toX   = to?.SystemX   ?? 0.5f;
        float toY   = to?.SystemY   ?? 0.5f;

        float dx   = toX - fromX;
        float dy   = toY - fromY;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float days = dist * Constants.Travel.SubLightDaysPerSystemUnit;
        return Mathf.Max(Constants.Travel.MinTravelDays, days);
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
