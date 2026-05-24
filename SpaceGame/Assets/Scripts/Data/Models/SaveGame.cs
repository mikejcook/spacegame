using SQLite;
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

    // Progression
    public int   Credits    { get; set; } = Constants.Economy.StartingCredits;
    public int   DaysPassed { get; set; } = 0;
    public float PlayTime   { get; set; } = 0f; // seconds

    // Timestamps
    public DateTime CreatedAt   { get; set; }
    public DateTime LastSavedAt { get; set; }

    // Future: story/quest flag storage (JSON string)
    public string StoryFlagsJson { get; set; } = "{}";
}
