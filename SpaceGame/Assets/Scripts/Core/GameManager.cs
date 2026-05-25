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
        SetState(GameState.Loading);

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

        // Create starting captain (with chosen portrait)
        PlayerCaptain            = CharacterFactory.CreateCaptain(captainName, portraitFileName);
        PlayerCaptain.SaveGameId = CurrentSave.Id;
        Database.Characters.Insert(PlayerCaptain);
        CurrentSave.CaptainId    = PlayerCaptain.Id;

        // Create starting pilot and engineer
        PlayerPilot              = CharacterFactory.CreateStartingPilot();
        PlayerEngineer           = CharacterFactory.CreateStartingEngineer();
        PlayerPilot.SaveGameId   = CurrentSave.Id;
        PlayerEngineer.SaveGameId = CurrentSave.Id;
        Database.Characters.Insert(PlayerPilot);
        Database.Characters.Insert(PlayerEngineer);

        // Create starting ship
        PlayerShip            = Ship.CreateStartingShip(shipName);
        PlayerShip.SaveGameId = CurrentSave.Id;
        Database.Ships.Insert(PlayerShip);
        CurrentSave.ShipId    = PlayerShip.Id;

        // Generate Sol system as the starting system
        var sol            = StarSystemGenerator.GenerateSolSystem();
        sol.SaveGameId     = CurrentSave.Id;
        Database.StarSystems.Insert(sol);

        // Generate and insert Sol's POIs
        var solPOIs = StarSystemGenerator.GeneratePOIsForSystem(sol, CurrentSave.Id);
        foreach (var poi in solPOIs)
            Database.POIs.Insert(poi);

        CurrentSave.CurrentSystemId = sol.Id;
        Database.SaveGames.Update(CurrentSave);
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

    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;
        SetState(GameState.InGame);
        Time.timeScale = 1f;
    }

    // -----------------------------------------------------------------------
    // Economy helpers
    // -----------------------------------------------------------------------
    public bool SpendCredits(int amount)
    {
        if (CurrentSave == null || CurrentSave.Credits < amount) return false;
        CurrentSave.Credits -= amount;
        EventBus.Publish(new CreditsChangedEvent(CurrentSave.Credits));
        return true;
    }

    public void AddCredits(int amount)
    {
        if (CurrentSave == null) return;
        CurrentSave.Credits += amount;
        EventBus.Publish(new CreditsChangedEvent(CurrentSave.Credits));
    }

    // -----------------------------------------------------------------------
    // Internals
    // -----------------------------------------------------------------------
    private void LoadGameScene()
    {
        SceneManager.LoadScene(Constants.Scenes.Game);
        SetState(GameState.InGame);
    }

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        EventBus.Publish(new GameStateChangedEvent(newState));
    }
}
