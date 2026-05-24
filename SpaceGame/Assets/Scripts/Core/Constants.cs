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
            public const string Engine      = "Engine";
            public const string Shield      = "Shield";
            public const string Reactor     = "Reactor";
            public const string Sensors     = "Sensors";
            public const string WeaponPort1 = "WeaponPort1";
            public const string WeaponPort2 = "WeaponPort2";
            public const string WeaponPort3 = "WeaponPort3";
            public const string WeaponPort4 = "WeaponPort4";
            public const string CargoHold   = "CargoHold";
        }
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
        /// <summary>Intro sequence played when a new game begins.</summary>
        public const string NewGameIntro = "Interludes/new_game_intro";
    }
}
