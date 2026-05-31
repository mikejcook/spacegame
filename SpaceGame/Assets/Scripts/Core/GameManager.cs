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
            InsertSystemWithPOIs(StarSystemGenerator.GenerateAlphaCentauri(), CurrentSave.Id);
            InsertSystemWithPOIs(StarSystemGenerator.GenerateBarnardsStar(), CurrentSave.Id);

            // ── 20 extra systems from the CSV catalogue ────────────────────
            var catalogueAsset = Resources.Load<TextAsset>("Data/systems");
            if (catalogueAsset != null)
            {
                var catalogue = StarSystemGenerator.ParseSystemsCSV(catalogueAsset.text);

                // Exclude the three hand-crafted systems
                var excluded = new System.Collections.Generic.HashSet<string>(
                    System.StringComparer.OrdinalIgnoreCase)
                    { "Sol", "Alpha Centauri", "Barnard's Star" };

                // Shuffle with a unique seed — mix save ID with current time so
                // repeated new-game calls (which reset the DB and reuse ID=1 in the
                // editor) still produce different galaxies each run.
                int galaxySeed = CurrentSave.Id ^ (int)System.DateTime.UtcNow.Ticks ^ 0x1337;
                CurrentSave.GalaxySeed = galaxySeed;
                var rng        = new System.Random(galaxySeed);
                var candidates = new System.Collections.Generic.List<(string name, StarType starType)>();
                foreach (var entry in catalogue)
                    if (!excluded.Contains(entry.name)) candidates.Add(entry);
                candidates.Sort((_, __) => rng.Next(-1, 2));   // Fisher-Yates-ish
                int take = System.Math.Min(20, candidates.Count);

                // Get well-spread galaxy positions for the batch.
                // Pass the three fixed cluster positions so no random system
                // lands on top of Sol, Alpha Centauri, or Barnard's Star.
                var solCluster = new (float gx, float gy)[]
                {
                    (StarSystemGenerator.SolGX,      StarSystemGenerator.SolGY),
                    (StarSystemGenerator.AlphaGX,    StarSystemGenerator.AlphaGY),
                    (StarSystemGenerator.BarnardsGX,  StarSystemGenerator.BarnardsGY),
                };
                var positions = StarSystemGenerator.GenerateGalaxyPositions(
                    take, galaxySeed,
                    innerR:          0.12f,
                    outerR:          0.40f,   // widened from 0.27 to give 20 systems more room
                    avoidPositions:  solCluster,
                    minSpacing:      0.13f);  // raised from 0.09: covers full node+label footprint
                                              // (label 160px wide → 0.083 horiz; label bottom
                                              // 52px below centre vs 22px half-hit-area → 0.069
                                              // vert; 0.13 clears both axes with margin)

                for (int i = 0; i < take; i++)
                {
                    var sys = StarSystemGenerator.GenerateExtraSystem(
                        candidates[i].name,
                        candidates[i].starType,
                        positions[i].gx,
                        positions[i].gy,
                        seed:       galaxySeed + 200 + i,
                        saveGameId: CurrentSave.Id);
                    InsertSystemWithPOIs(sys, CurrentSave.Id);
                }
                Debug.Log($"[GameManager] {take} extra systems inserted from catalogue.");
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
    private void InsertSystemWithPOIs(StarSystem system, int saveGameId)
    {
        system.SaveGameId = saveGameId;
        Database.StarSystems.Insert(system);
        var pois = StarSystemGenerator.GeneratePOIsForSystem(system, saveGameId);
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
