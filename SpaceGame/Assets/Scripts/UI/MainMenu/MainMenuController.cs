using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Drives the main menu UI for Star Captain.
///
/// Scene setup:
///   - Attach to a "MainMenuController" GameObject in the MainMenu scene.
///   - Wire up all [SerializeField] references in the Inspector.
///
/// Panel structure (child GameObjects under a Canvas):
///   MainPanel         -> Continue / New Game / Load Game / Settings / Quit buttons
///   NewGamePanel      -> CaptainName input, portrait selector, ship name input +
///                        random button, Start button, Back button
///   PortraitPickerPanel -> (handled by PortraitPickerPanel script — assign below)
///   LoadGamePanel     -> ScrollView containing SaveSlot prefabs, Back button
///   SettingsPanel     -> Music/SFX sliders, Back button
///
/// New Game Panel — portrait button setup:
///   - Create a Button (e.g. "PortraitButton") inside NewGamePanel.
///   - Add a RawImage child named "PortraitImage" — this shows the selected portrait.
///   - Add a TMP_Text child named "ChangeHint" with text "Tap to change".
///   - Assign the Button to portraitButton and the RawImage to portraitDisplay below.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector references — panels
    // -----------------------------------------------------------------------
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject newGamePanel;
    [SerializeField] private GameObject loadGamePanel;
    [SerializeField] private GameObject settingsPanel;

    // -----------------------------------------------------------------------
    // Inspector references — main panel buttons
    // -----------------------------------------------------------------------
    [Header("Main Panel")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    // -----------------------------------------------------------------------
    // Inspector references — new game panel
    // -----------------------------------------------------------------------
    [Header("New Game Panel — Inputs")]
    [SerializeField] private TMP_InputField captainNameInput;
    [SerializeField] private TMP_InputField shipNameInput;
    [SerializeField] private Button         randomShipNameButton;
    [SerializeField] private Button         startGameButton;
    [SerializeField] private Button         newGameBackButton;
    [SerializeField] private TMP_Text       nameValidationText;

    [Header("New Game Panel — Portrait")]
    [Tooltip("The Button the player clicks to open the portrait picker.")]
    [SerializeField] private Button    portraitButton;
    [Tooltip("RawImage child of portraitButton that displays the current selection.")]
    [SerializeField] private RawImage  portraitDisplay;
    [Tooltip("Reference to the PortraitPickerPanel in the scene.")]
    [SerializeField] private PortraitPickerPanel portraitPickerPanel;
    [Tooltip("The same PortraitLibrary asset assigned to PortraitPickerPanel — used to pick a random default portrait.")]
    [SerializeField] private PortraitLibrary portraitLibrary;

    // -----------------------------------------------------------------------
    // Inspector references — load game panel
    // -----------------------------------------------------------------------
    [Header("Load Game Panel")]
    [SerializeField] private Transform  saveSlotContainer;
    [SerializeField] private GameObject saveSlotPrefab;
    [SerializeField] private Button     loadGameBackButton;
    [SerializeField] private TMP_Text   noSavesText;

    // -----------------------------------------------------------------------
    // Inspector references — settings panel
    // -----------------------------------------------------------------------
    [Header("Settings Panel")]
    [SerializeField] private Slider   musicVolumeSlider;
    [SerializeField] private Slider   sfxVolumeSlider;
    [SerializeField] private Button   settingsBackButton;

    // -----------------------------------------------------------------------
    // Inspector references — story interlude
    // -----------------------------------------------------------------------
    [Header("Story Interlude")]
    [Tooltip("StoryInterludeController sitting on a high-sort-order Canvas overlay in this scene.")]
    [SerializeField] private StoryInterludeController storyInterludeController;

    // -----------------------------------------------------------------------
    // Private state
    // -----------------------------------------------------------------------
    private List<SaveGame> _saveGames = new();

    /// <summary>Filename of the portrait the player has selected (e.g. "7.png").</summary>
    private string _selectedPortraitFileName = string.Empty;

    // -----------------------------------------------------------------------
    // Unity lifecycle
    // -----------------------------------------------------------------------
    private void Start()
    {
        // Auto-load portrait library from Resources if not wired in the Inspector.
        // This means you only need to run Star Captain -> Build Portrait Library once;
        // no scene rebuild or manual assignment is required afterward.
        if (portraitLibrary == null)
            portraitLibrary = Resources.Load<PortraitLibrary>("PortraitLibrary");

        LoadSaveGames();
        BindButtons();
        ShowMainPanel();
        LoadSavedSettings();
    }

    // -----------------------------------------------------------------------
    // Initialisation
    // -----------------------------------------------------------------------
    private void LoadSaveGames()
    {
        _saveGames = GameManager.Instance?.Database?.SaveGames?.GetAll()
                     ?? new List<SaveGame>();

        bool hasSaves = _saveGames.Count > 0;
        if (continueButton) continueButton.interactable = hasSaves;
        if (loadGameButton) loadGameButton.interactable = hasSaves;
    }

    private void BindButtons()
    {
        continueButton?.onClick.AddListener(OnContinueClicked);
        newGameButton?.onClick.AddListener(OnNewGameClicked);
        loadGameButton?.onClick.AddListener(OnLoadGameClicked);
        settingsButton?.onClick.AddListener(OnSettingsClicked);
        quitButton?.onClick.AddListener(OnQuitClicked);

        startGameButton?.onClick.AddListener(OnStartGameClicked);
        newGameBackButton?.onClick.AddListener(ShowMainPanel);

        randomShipNameButton?.onClick.AddListener(OnRandomShipName);
        portraitButton?.onClick.AddListener(OnPortraitButtonClicked);

        loadGameBackButton?.onClick.AddListener(ShowMainPanel);
        settingsBackButton?.onClick.AddListener(OnSettingsBack);

        musicVolumeSlider?.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider?.onValueChanged.AddListener(OnSFXVolumeChanged);
    }

    private void LoadSavedSettings()
    {
        if (musicVolumeSlider) musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        if (sfxVolumeSlider)   sfxVolumeSlider.value   = PlayerPrefs.GetFloat("SFXVolume",   1.0f);
    }

    // -----------------------------------------------------------------------
    // Panel navigation
    // -----------------------------------------------------------------------
    private void ShowMainPanel()
    {
        SetActivePanel(mainPanel);
    }

    private void SetActivePanel(GameObject target)
    {
        mainPanel?.SetActive(target == mainPanel);
        newGamePanel?.SetActive(target == newGamePanel);
        loadGamePanel?.SetActive(target == loadGamePanel);
        settingsPanel?.SetActive(target == settingsPanel);
    }

    // -----------------------------------------------------------------------
    // Button handlers
    // -----------------------------------------------------------------------
    private void OnContinueClicked()
    {
        if (_saveGames.Count == 0) return;
        _saveGames.Sort((a, b) => b.LastSavedAt.CompareTo(a.LastSavedAt));
        GameManager.Instance.LoadGame(_saveGames[0].Id);
    }

    private void OnNewGameClicked()
    {
        if (captainNameInput)    captainNameInput.text = "";
        if (nameValidationText)  nameValidationText.text = "";

        // Pick a random starting ship name
        ApplyRandomShipName(forceNew: false);

        // Pick a random starting portrait
        PickRandomPortrait();

        SetActivePanel(newGamePanel);
    }

    private void OnLoadGameClicked()
    {
        SetActivePanel(loadGamePanel);
        PopulateSaveSlots();
    }

    private void OnSettingsClicked()
    {
        SetActivePanel(settingsPanel);
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnStartGameClicked()
    {
        // ── Validate captain name ─────────────────────────────────────────
        string captainName = captainNameInput?.text?.Trim() ?? "";
        if (string.IsNullOrEmpty(captainName))
        {
            if (nameValidationText) nameValidationText.text = "Please enter a captain name.";
            return;
        }

        // ── Validate ship name ────────────────────────────────────────────
        string shipName = shipNameInput?.text?.Trim() ?? "";
        if (string.IsNullOrEmpty(shipName))
        {
            if (nameValidationText) nameValidationText.text = "Please enter a ship name.";
            return;
        }

        if (nameValidationText) nameValidationText.text = "";

        string portraitFileName = _selectedPortraitFileName;

        // ── Prepare game data first so interlude tokens have live values ──
        GameManager.Instance.PrepareNewGame(captainName, shipName, portraitFileName);

        // ── Show opening story interlude, then launch the game scene ──────
        var stories   = StoryCollection.LoadFromResources();
        var interlude = stories?.GetInterlude(Constants.Interludes.NewGameIntroId);

        if (interlude != null && storyInterludeController != null)
        {
            storyInterludeController.Play(interlude, () =>
            {
                GameManager.Instance.LaunchNewGame();
            });
        }
        else
        {
            // Fallback: interlude not configured — go straight to game
            GameManager.Instance.LaunchNewGame();
        }
    }

    // -----------------------------------------------------------------------
    // Ship name — random
    // -----------------------------------------------------------------------

    /// <summary>
    /// Assigns a random ship name, guaranteeing it differs from the current value.
    /// </summary>
    private void OnRandomShipName()
    {
        ApplyRandomShipName(forceNew: true);
    }

    private void ApplyRandomShipName(bool forceNew)
    {
        var names = Constants.NewGame.ShipNames;
        if (names == null || names.Length == 0) return;

        if (shipNameInput == null) return;

        string current = shipNameInput.text?.Trim() ?? "";
        string chosen;

        if (forceNew && names.Length > 1)
        {
            // Keep picking until we get a different name
            do
            {
                chosen = names[Random.Range(0, names.Length)];
            }
            while (chosen == current);
        }
        else
        {
            chosen = names[Random.Range(0, names.Length)];
        }

        shipNameInput.text = chosen;
    }

    // -----------------------------------------------------------------------
    // Portrait selection
    // -----------------------------------------------------------------------

    private void OnPortraitButtonClicked()
    {
        if (portraitPickerPanel == null)
        {
            Debug.LogWarning("[MainMenuController] portraitPickerPanel is not assigned!");
            return;
        }

        portraitPickerPanel.Open(OnPortraitChosen);
    }

    private void OnPortraitChosen(string fileName)
    {
        _selectedPortraitFileName = fileName;

        // Update the portrait display image
        if (portraitDisplay != null && portraitLibrary != null && portraitLibrary.IsValid)
        {
            int idx = portraitLibrary.IndexOf(fileName);
            if (idx >= 0)
                portraitDisplay.texture = portraitLibrary.Portraits[idx];
        }
    }

    /// <summary>
    /// Picks a random portrait from the library and shows it in the portrait button.
    /// </summary>
    private void PickRandomPortrait()
    {
        if (portraitLibrary == null || !portraitLibrary.IsValid)
        {
            _selectedPortraitFileName = string.Empty;
            if (portraitDisplay) portraitDisplay.texture = null;
            return;
        }

        int idx = portraitLibrary.RandomIndex();
        _selectedPortraitFileName = portraitLibrary.FileNames[idx];

        if (portraitDisplay != null)
            portraitDisplay.texture = portraitLibrary.Portraits[idx];
    }

    // -----------------------------------------------------------------------
    // Load game — save slot population
    // -----------------------------------------------------------------------
    private void PopulateSaveSlots()
    {
        if (!saveSlotContainer) return;

        foreach (Transform child in saveSlotContainer)
            Destroy(child.gameObject);

        if (_saveGames.Count == 0)
        {
            if (noSavesText) noSavesText.gameObject.SetActive(true);
            return;
        }

        if (noSavesText) noSavesText.gameObject.SetActive(false);

        _saveGames.Sort((a, b) => b.LastSavedAt.CompareTo(a.LastSavedAt));

        foreach (var save in _saveGames)
        {
            if (!saveSlotPrefab) continue;
            var slot       = Instantiate(saveSlotPrefab, saveSlotContainer);
            var controller = slot.GetComponent<SaveSlotController>();
            controller?.Initialize(save, () => GameManager.Instance.LoadGame(save.Id));
        }
    }

    // -----------------------------------------------------------------------
    // Settings handlers
    // -----------------------------------------------------------------------
    private void OnMusicVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("MusicVolume", value);
        // GameManager.Instance.Audio?.SetMusicVolume(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        PlayerPrefs.SetFloat("SFXVolume", value);
        // GameManager.Instance.Audio?.SetSFXVolume(value);
    }

    private void OnSettingsBack()
    {
        PlayerPrefs.Save();
        ShowMainPanel();
    }
}
