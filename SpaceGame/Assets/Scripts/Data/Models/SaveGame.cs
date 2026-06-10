using SQLite4Unity3d;
using System;

/// <summary>
/// Represents one save slot. Stores references to the captain,
/// ship, current system, and top-level progression data.
/// </summary>
[Table("SaveGames")]
public class SaveGame
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // Display info
    public string CaptainName { get; set; }
    public string ShipName    { get; set; }

    // Foreign keys to core entities
    public int CaptainId       { get; set; }
    public int ShipId          { get; set; }
    public int CurrentSystemId { get; set; }

    // Galaxy generation seed — stored so the same galaxy can be reproduced
    // if needed (e.g. loading a save, debugging). Set during PrepareNewGame.
    public int GalaxySeed { get; set; }

    // Progression
    public int   Credits      { get; set; } = Constants.Economy.StartingCredits;
    public int   Salvage      { get; set; } = 0;
    public int   Fuel         { get; set; } = Constants.Resources.StartingFuel;
    public int   Iridium      { get; set; } = 0;
    public int   CrewLoyalty  { get; set; } = Constants.Resources.StartingLoyalty;
    public float DaysPassed   { get; set; } = 0f;
    public float PlayTime     { get; set; } = 0f; // seconds

    // Timestamps
    public DateTime CreatedAt   { get; set; }
    public DateTime LastSavedAt { get; set; }

    // Future: story/quest flag storage (JSON string)
    public string StoryFlagsJson { get; set; } = "{}";
}
