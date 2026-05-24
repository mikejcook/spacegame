# Space Game — Project Setup Guide

## 1. Folder Structure

Copy the `Scripts/` folder into your Unity project:

```
code\spacegame\Assets\
  Scripts\
    Core\
      Constants.cs
      EventBus.cs
      GameManager.cs
      DatabaseManager.cs
    Data\
      Models\
        SaveGame.cs
        Character.cs
        Ship.cs
        EquipmentItem.cs
        StarSystem.cs          <- also contains PointOfInterest
        FeatDefinition.cs      <- also contains FeatRegistry
    Game\
      DiceRoller.cs
      Factories\
        CharacterFactory.cs
      Generation\
        StarSystemGenerator.cs
    UI\
      MainMenu\
        MainMenuController.cs
        SaveSlotController.cs
  Scenes\
  Prefabs\
  Art\
  Audio\
```

Also create these folders inside Unity's Project panel (right-click → Create → Folder):

- `Assets/Scenes`
- `Assets/Prefabs/UI`
- `Assets/Art/Sprites`
- `Assets/Art/UI`
- `Assets/Audio/Music`
- `Assets/Audio/SFX`
- `Assets/Resources/Portraits`

---

## 2. Install Required Packages

### A — NuGetForUnity (package manager for .NET libraries)

1. In Unity go to **Window → Package Manager**
2. Click the **+** button → **Add package from git URL**
3. Paste: `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity`
4. Click **Add**

### B — sqlite-net-pcl (SQLite ORM)

1. After NuGetForUnity installs, go to **NuGet → Manage NuGet Packages**
2. Search for `sqlite-net-pcl`
3. Install it

### C — Newtonsoft JSON (JSON.NET)

1. In **Package Manager** → **Add package by name**
2. Enter: `com.unity.nuget.newtonsoft-json`
3. Click **Add**

---

## 3. Scene Setup

### MainMenu Scene

1. Create a new scene: **File → New Scene** → save as `Assets/Scenes/MainMenu.unity`
2. Create an empty GameObject → rename to **`GameManager`**
3. Attach the `GameManager` script to it
4. Create a child GameObject called **`DatabaseManager`** and attach the `DatabaseManager` script
5. Create a **Canvas** (right-click Hierarchy → UI → Canvas)
   - Set Canvas Scaler to **Scale With Screen Size**, reference resolution **1920×1080**
   - Anchor mode: **Landscape**
6. Under the Canvas create these child panels (UI → Panel for each):
   - `MainPanel`
   - `NewGamePanel`
   - `LoadGamePanel`
   - `SettingsPanel`
7. Create a new empty GameObject under the Canvas → attach `MainMenuController`
8. Wire all the Inspector references on `MainMenuController` to your UI elements

### GameScene

1. Create a new scene → save as `Assets/Scenes/GameScene.unity`
2. This is where the star system view will live — leave it mostly empty for now

### Build Settings / Build Profiles

Add both scenes:
- Index 0: `Assets/Scenes/MainMenu.unity`
- Index 1: `Assets/Scenes/GameScene.unity`

---

## 4. Verify the Scripts Compile

After copying the scripts and installing packages:

1. Return to Unity — it will recompile automatically
2. Check the **Console** window for errors
3. Common first-run errors and fixes:

| Error | Fix |
|-------|-----|
| `The type or namespace 'SQLite' could not be found` | sqlite-net-pcl not installed yet |
| `The type or namespace 'Newtonsoft' could not be found` | JSON.NET package not installed yet |
| `The type or namespace 'TMPro' could not be found` | Install TextMeshPro: **Window → Package Manager → search TextMeshPro** |

---

## 5. Testing the Foundation

Once compiling cleanly:

1. Open the **MainMenu** scene
2. Hit **Play** — the GameManager and DatabaseManager will initialize
3. Check the Console for `[DatabaseManager] Database ready.`
4. The database file will be created at Unity's `Application.persistentDataPath`:
   - Windows: `C:\Users\<you>\AppData\LocalLow\<CompanyName>\<ProductName>\spacegame.db`

---

## 6. Architecture Notes

### How systems talk to each other

Use `EventBus` for loose coupling. Don't take direct references between UI and game logic:

```csharp
// Publishing (game logic side)
EventBus.Publish(new CreditsChangedEvent(GameManager.Instance.CurrentSave.Credits));

// Subscribing (UI side)
void OnEnable()  => EventBus.Subscribe<CreditsChangedEvent>(OnCreditsChanged);
void OnDisable() => EventBus.Unsubscribe<CreditsChangedEvent>(OnCreditsChanged);
void OnCreditsChanged(CreditsChangedEvent e) => creditsText.text = $"{e.NewTotal:N0}";
```

### D20 Dice System

```csharp
// Skill check example
var result = DiceRoller.RollSkillCheck(
    character: pilot,
    skillName: Constants.Skills.Piloting,
    difficultyClass: Constants.Dice.DC_Hard
);

if (result.IsSuccess)
    Debug.Log($"Success! {result}");
else
    Debug.Log($"Failed: {result}");
```

### Adding new feats

Open `FeatDefinition.cs` and add an entry to `FeatRegistry.Build()`:

```csharp
["my_new_feat"] = new FeatDefinition
{
    Id          = "my_new_feat",
    Name        = "My New Feat",
    Description = "Does something cool.",
    RequiredSkillRanks = new() { [Constants.Skills.Combat] = 4 },
    RequiredLevel = 5,
    EffectType    = "my_effect",
    EffectMagnitude = 3
},
```

---

## 7. What's Next

Once the foundation compiles and the main menu loads:

- [ ] Build the MainMenu UI layout in the Canvas
- [ ] Create the SaveSlot prefab and wire it to SaveSlotController
- [ ] Set up the GameScene with a placeholder star system view
- [ ] Add the HUD (credits display, nav buttons along the bottom)
- [ ] Implement the StarSystem view — render POIs as clickable buttons
- [ ] Implement the ship management screen
- [ ] Implement the crew management screen
```
