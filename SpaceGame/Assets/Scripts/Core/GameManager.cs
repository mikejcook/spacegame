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
    public SaveGame CurrentSave  { get; private set; }
    public Ship     PlayerShip   { get; private set; }
    public Character PlayerCaptain { get; private set; }

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

    /// <summary>Start a brand-new game with the given captain name, ship name, and portrait.</summary>
    public void StartNewGame(string captainName, string shipName = "Horizon", string portraitFileName = "")
    {
        SetState(GameState.Loading);

        // Build the save record
        CurrentSave = new SaveGame
        {
            CaptainName   = captainName,
            ShipName      = shipName,
            Credits       = Constants.Economy.StartingCredits,
            CreatedAt     = System.DateTime.Now,
            LastSavedAt   = System.DateTime.Now
        };
        Database.SaveGames.Insert(CurrentSave);

        // Create starting captain (with chosen portrait)
        PlayerCaptain            = CharacterFactory.CreateCaptain(captainName, portraitFileName);
        PlayerCaptain.SaveGameId = CurrentSave.Id;
        Database.Characters.Insert(PlayerCaptain);
        CurrentSave.CaptainId    = PlayerCaptain.Id;

        // Create starting pilot and engineer
        var pilot    = CharacterFactory.CreateStartingPilot();
        var engineer = CharacterFactory.CreateStartingEngineer();
        pilot.SaveGameId    = CurrentSave.Id;
        engineer.SaveGameId = CurrentSave.Id;
        Database.Characters.Insert(pilot);
        Database.Characters.Insert(engineer);

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

        LoadGameScene();
    }

    /// <summary>Resume an existing save.</summary>
    public void LoadGame(int saveId)
    {
        SetState(GameState.Loading);

        CurrentSave   = Database.SaveGames.Get(saveId);
        PlayerCaptain = Database.Characters.Get(CurrentSave.CaptainId);
        PlayerShip    = Database.Ships.Get(CurrentSave.ShipId);

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
