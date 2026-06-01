using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central repository for all game-wide constants.
/// Avoids magic strings and numbers scattered throughout the codebase.
/// </summary>
public static class Constants
{
    public static class Game
    {
        public const string Title = "Star Captain";
    }

    public static class NewGame
    {
        public static readonly string[] ShipNames =
        {
            "Resolute", "Vigilance", "Endurance", "Prospector", "Ascendant",
            "Dauntless", "Solstice", "Trailblazer", "Ardent Star", "Radiant Horizon",
            "Horizon", "Odyssey's Crest", "Beacon's Reach", "Wayfarer", "Nova",
            "Discovery", "Endeavor", "Venture", "Sojourn", "Solace"
        };
    }

    public static class Scenes
    {
        public const string MainMenu = "MainMenu";
        public const string Game    = "GameScene";
    }

    public static class Database
    {
        public const string FileName = "spacegame.db";
    }

    public static class Skills
    {
        public const string Piloting    = "Piloting";
        public const string Engineering = "Engineering";
        public const string Science     = "Science";
        public const string Medicine    = "Medicine";
        public const string Combat      = "Combat";
        public const string Diplomacy   = "Diplomacy";
        public const string Survival    = "Survival";
        public const string Navigation  = "Navigation";
        public const string Electronics = "Electronics";
        public const string Command     = "Command";

        public static readonly string[] All =
        {
            Piloting, Engineering, Science, Medicine, Combat,
            Diplomacy, Survival, Navigation, Electronics, Command
        };
    }

    public static class Crew
    {
        public static class Roles
        {
            public const string Captain        = "Captain";
            public const string Pilot          = "Pilot";
            public const string Engineer       = "Engineer";
            public const string Scientist      = "Scientist";
            public const string WeaponsOfficer = "WeaponsOfficer";
            public const string Doctor         = "Doctor";
            public const string Soldier        = "Soldier";
        }
    }

    public static class Ship
    {
        public static class EquipmentSlots
        {
            public const string Reactor      = "Reactor";
            public const string FtlDrive     = "FTL Drive";
            public const string Engines      = "Engines";
            public const string Shields      = "Shields";
            public const string Armor        = "Armor";
            public const string BeamWeapons  = "Beam Weapons";
            public const string Torpedoes    = "Torpedoes";
            public const string Scanner      = "Scanner";
            public const string CargoHold    = "Cargo Hold";
            public const string CrewQuarters = "Crew Quarters";

            /// <summary>
            /// Maps each slot display name to the corresponding icon filename
            /// (without extension) under Assets/Art/UI/EquipmentIcons/.
            /// </summary>
            public static readonly Dictionary<string, string> IconNames =
                new Dictionary<string, string>
                {
                    [Reactor]      = "Reactor",
                    [FtlDrive]     = "FtlDrive",
                    [Engines]      = "Engines",
                    [Shields]      = "Shields",
                    [Armor]        = "Armor",
                    [BeamWeapons]  = "BeamWeapons",
                    [Torpedoes]    = "Torpedoes",
                    [Scanner]      = "Scanner",
                    [CargoHold]    = "CargoHold",
                    [CrewQuarters] = "CrewQuarters",
                };
        }

        // ── Tier colours (Warcraft-style quality tiers) ──────────────────────

        public static readonly Color EmptySlotBorderColor =
            new Color(0.45f, 0.45f, 0.45f, 1.00f);

        public static Color TierColor(EquipmentTier tier) => tier switch
        {
            EquipmentTier.MkI   => new Color(0.60f, 0.60f, 0.60f), // Grey
            EquipmentTier.MkII  => new Color(1.00f, 1.00f, 1.00f), // White
            EquipmentTier.MkIII => new Color(0.12f, 0.74f, 0.12f), // Green
            EquipmentTier.MkIV  => new Color(0.00f, 0.44f, 0.87f), // Blue
            EquipmentTier.MkV   => new Color(0.64f, 0.21f, 0.93f), // Purple
            EquipmentTier.MkVI  => new Color(1.00f, 0.50f, 0.00f), // Orange
            _                   => Color.grey
        };
    }

    public static class Dice
    {
        public const int AutoSuccessRoll = 20;

        // Difficulty Classes (DC)
        public const int DC_Easy             =  8;
        public const int DC_Moderate         = 12;
        public const int DC_Hard             = 16;
        public const int DC_VeryHard         = 20;
        public const int DC_NearlyImpossible = 25;
    }

    public static class POI
    {
        public static class Types
        {
            public const string Planet          = "Planet";
            public const string SpaceStation    = "SpaceStation";
            public const string DerelictShip    = "DerelictShip";
            public const string DerelictStation = "DerelictStation";
            public const string AsteroidField   = "AsteroidField";
            public const string Ship            = "Ship";
            public const string Anomaly         = "Anomaly";
        }
    }

    public static class Economy
    {
        public const int StartingCredits = 1000;
    }

    public static class Interludes
    {
        /// <summary>Resources path to the single story collection file (no extension).</summary>
        public const string StoriesPath = "Interludes/stories";

        /// <summary>interludeId of the intro sequence played when a new game begins.</summary>
        public const string NewGameIntroId = "new_game_intro";
    }
}
