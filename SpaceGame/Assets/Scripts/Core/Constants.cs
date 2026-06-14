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
        public const string Athletics   = "Athletics";
        public const string Combat      = "Combat";
        public const string Command     = "Command";
        public const string Engineering = "Engineering";
        public const string Gunnery     = "Gunnery";
        public const string Medicine    = "Medicine";
        public const string Perception  = "Perception";
        public const string Piloting    = "Piloting";
        public const string Science     = "Science";

        /// <summary>Skill points granted each time a character gains a level.</summary>
        public const int PointsPerLevel = 3;

        /// <summary>Maximum rank in any single skill = character level + 1.</summary>
        public static int MaxRankForLevel(int level) => level + 1;

        public static readonly string[] All =
        {
            Athletics, Combat, Command, Engineering, Gunnery, Medicine, Perception, Piloting, Science
        };
    }

    public static class Crew
    {
        public static class Roles
        {
            public const string Captain   = "Captain";
            public const string Pilot     = "Pilot";
            public const string Engineer  = "Engineer";
            public const string Scientist = "Scientist";
            public const string Gunner    = "Gunner";
            public const string Doctor    = "Doctor";
            public const string Soldier   = "Soldier";
        }

        /// <summary>
        /// Ordered assignment slots (excluding Captain, who is always in the centre).
        /// First three go in the left column, last three in the right column.
        /// </summary>
        public static readonly string[] AssignmentSlots =
        {
            Roles.Pilot, Roles.Engineer, Roles.Scientist,   // left column
            Roles.Gunner, Roles.Doctor,  Roles.Soldier,     // right column
        };
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
            public const string ParticleCannons  = "Particle Cannons";
            public const string Torpedoes    = "Torpedoes";
            public const string Scanner      = "Scanner";
            public const string CargoHold    = "Cargo Hold";
            public const string CrewQuarters = "Crew Quarters";

            /// <summary>
            /// Placeholder descriptions shown in the equipment detail popup.
            /// Keyed by slot display name.  Replace with authored content later.
            /// </summary>
            public static readonly Dictionary<string, string> Descriptions =
                new Dictionary<string, string>
                {
                    [Reactor]      = "Generates power for all ship systems. Higher-rated reactors unlock more advanced equipment and improve overall energy efficiency.",
                    [FtlDrive]     = "Enables faster-than-light travel between star systems. Upgraded drives reduce jump charge time and extend maximum jump range.",
                    [Engines]      = "Sublight propulsion system. Better engines improve combat maneuverability, evasion rating, and in-system transit speed.",
                    [Shields]      = "Energy-based defense layer that absorbs incoming damage before the hull is hit. Regenerates automatically between encounters.",
                    [Armor]        = "Reinforced hull plating that provides passive damage reduction on every hit and increases the ship's maximum hull points.",
                    [ParticleCannons]  = "Kinetic particle accelerators that deal sustained damage. Highly effective against shields. Efficiency scales with reactor output.",
                    [Torpedoes]    = "Guided projectile weapons with high burst damage. Effective against armored targets. Limited magazine requires resupply at stations.",
                    [Scanner]      = "Long-range sensor array for threat detection and resource mapping. Improves combat awareness and anomaly discovery range.",
                    [CargoHold]    = "Expanded storage modules for trade goods, salvage, and raw materials. Each upgrade tier increases maximum cargo capacity.",
                    [CrewQuarters] = "Improved crew accommodations that increase maximum crew capacity and provide morale bonuses that boost all crew skill checks.",
                };

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
                    [ParticleCannons]  = "ParticleCannons",
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

        // ── Upgrade economy / tier helpers ───────────────────────────────────

        /// <summary>Highest tier a component can be upgraded to.</summary>
        public const EquipmentTier MaxTier = EquipmentTier.MkVI;

        /// <summary>
        /// Maps each equipment slot display name to the EquipmentType used when a
        /// fresh component is created for that slot (first upgrade of an empty slot).
        /// </summary>
        public static EquipmentType SlotEquipmentType(string slotDisplayName) => slotDisplayName switch
        {
            EquipmentSlots.Reactor      => EquipmentType.Reactor,
            EquipmentSlots.FtlDrive     => EquipmentType.Engine,
            EquipmentSlots.Engines      => EquipmentType.Engine,
            EquipmentSlots.Shields      => EquipmentType.Shield,
            EquipmentSlots.Armor        => EquipmentType.Shield,
            EquipmentSlots.ParticleCannons  => EquipmentType.Weapon,
            EquipmentSlots.Torpedoes    => EquipmentType.Weapon,
            EquipmentSlots.Scanner      => EquipmentType.Sensor,
            EquipmentSlots.CargoHold    => EquipmentType.CargoExpansion,
            EquipmentSlots.CrewQuarters => EquipmentType.Special,
            _                           => EquipmentType.Special
        };

        /// <summary>Human-readable tier label, e.g. EquipmentTier.MkII -> "Mk II".</summary>
        public static string TierLabel(EquipmentTier tier) => tier switch
        {
            EquipmentTier.MkI   => "Mk I",
            EquipmentTier.MkII  => "Mk II",
            EquipmentTier.MkIII => "Mk III",
            EquipmentTier.MkIV  => "Mk IV",
            EquipmentTier.MkV   => "Mk V",
            EquipmentTier.MkVI  => "Mk VI",
            _                   => tier.ToString()
        };

        /// <summary>
        /// Placeholder resource cost to upgrade a component up to <paramref name="targetTier"/>.
        /// Resource depletion is not yet implemented — this only drives the prompt text.
        /// </summary>
        public static int UpgradeCost(EquipmentTier targetTier) => (int)targetTier * 100;
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

    public static class Recruitment
    {
        /// <summary>
        /// Number of days that must pass before a space station refreshes its recruit pool.
        /// </summary>
        public const int RecruitRefreshDays = 7;
    }

    public static class Economy
    {
        public const int StartingCredits = 1000;
    }

    public static class Resources
    {
        public const int StartingHelium3  = 100;
        public const int StartingLoyalty  = 75;
        public const int MaxLoyalty       = 100;
        public const int MutinyThreshold  = 0;   // loyalty <= this triggers game over
        public const int MaxIridium       = 9999;
    }

    public static class CargoBay
    {
        /// <summary>
        /// Helium-3 capacity at each cargo bay upgrade level (index = level - 1).
        /// Capacity increases linearly by 100 per level.
        /// </summary>
        public static readonly int[] Helium3Capacity = { 100, 200, 300, 400, 500, 600 };

        /// <summary>Returns the He3 capacity for a given bay level (clamped to valid range).</summary>
        public static int GetHelium3Capacity(int level)
        {
            int idx = UnityEngine.Mathf.Clamp(level - 1, 0, Helium3Capacity.Length - 1);
            return Helium3Capacity[idx];
        }
    }

    public static class Interludes
    {
        /// <summary>Resources path to the single story collection file (no extension).</summary>
        public const string StoriesPath = "Interludes/stories";

        /// <summary>interludeId of the intro sequence played when a new game begins.</summary>
        public const string NewGameIntroId = "new_game_intro";
    }

    public static class Travel
    {
        /// <summary>
        /// Multiplier from normalised galaxy units to light-years.
        /// Matches GalaxyViewController.LightYearsPerUnit.
        /// </summary>
        public const float LightYearsPerUnit = 100_000f;

        /// <summary>
        /// FTL cruise speed in light-years per day.
        /// At this rate Sol → Alpha Centauri (~4,340 ly) takes ~4.3 days.
        /// </summary>
        public const float FtlLyPerDay = 1_000f;

        /// <summary>Minimum travel time for any in-system hop, regardless of distance.</summary>
        public const float MinTravelDays = 0.5f;

        /// <summary>
        /// In-system sublight travel speed expressed as days per normalised system unit.
        /// System coords are 0-1 centred at (0.5, 0.5); orbital radii run 0.07 (Mercury)
        /// to 0.46 (Neptune). At this rate a 0.17-unit hop (e.g. Earth→Mars same side)
        /// hits the MinTravelDays floor; a 0.35-unit cross-system trip costs ~1.05 days.
        /// </summary>
        public const float SubLightDaysPerSystemUnit = 3.0f;

        /// <summary>
        /// Helium-3 consumed per normalised galaxy unit of FTL travel.
        /// Adjacent systems (~0.10 units) cost ~20 He3; far systems (~0.40 units) cost ~80 He3.
        /// </summary>
        public const float FtlFuelPerGalaxyUnit = 200f;

        /// <summary>
        /// Helium-3 consumed per normalised system unit of sub-light travel.
        /// A short hop (0.10 units) costs ~4 He3; a cross-system trip (0.35 units) costs ~14 He3.
        /// </summary>
        public const float SubLightFuelPerSystemUnit = 40f;
    }
}
