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

        /// <summary>Loyalty earned per in-game day during travel (base rate, no research).</summary>
        public const float LoyaltyPerDay  = 1f;
    }

    public static class CargoBay
    {
        /// <summary>
        /// Helium-3 capacity at each cargo bay upgrade level (index = level - 1).
        /// Capacity increases linearly by 100 per level.
        /// </summary>
        public static readonly int[] Helium3Capacity = { 100, 200, 300, 400, 500, 600 };

        /// <summary>
        /// Iridium and Salvage capacity at each cargo bay upgrade level (index = level - 1).
        /// Each tier is ~1.5× the previous, starting at 150.
        /// </summary>
        public static readonly int[] IridiumCapacity = { 150, 225, 350, 525, 800, 1_200 };
        public static readonly int[] SalvageCapacity = { 150, 225, 350, 525, 800, 1_200 };

        /// <summary>Returns the He-3 capacity for a given bay level (clamped to valid range).</summary>
        public static int GetHelium3Capacity(int level)
        {
            int idx = UnityEngine.Mathf.Clamp(level - 1, 0, Helium3Capacity.Length - 1);
            return Helium3Capacity[idx];
        }

        /// <summary>Returns the Iridium capacity for a given bay level (clamped to valid range).</summary>
        public static int GetIridiumCapacity(int level)
        {
            int idx = UnityEngine.Mathf.Clamp(level - 1, 0, IridiumCapacity.Length - 1);
            return IridiumCapacity[idx];
        }

        /// <summary>Returns the Salvage capacity for a given bay level (clamped to valid range).</summary>
        public static int GetSalvageCapacity(int level)
        {
            int idx = UnityEngine.Mathf.Clamp(level - 1, 0, SalvageCapacity.Length - 1);
            return SalvageCapacity[idx];
        }
    }

    public static class Research
    {
        /// <summary>Salvage cost for a research node = this fraction of its tier's max Salvage capacity.</summary>
        public const float CostPercentOfSalvageCap = 0.35f;

        /// <summary>Flat number of in-game days a research item takes to complete.</summary>
        public const float DurationDays = 10f;

        /// <summary>Returns the Salvage cost to research a node of the given tier (1-6).</summary>
        public static int GetCost(int tier)
        {
            return UnityEngine.Mathf.RoundToInt(CostPercentOfSalvageCap * CargoBay.GetSalvageCapacity(tier));
        }

        /// <summary>
        /// Node IDs from research_tree.json. Keep in sync with that file — these are the
        /// strings stored in SaveGame.UnlockedResearchJson and matched against unlock effects.
        /// </summary>
        public static class Ids
        {
            // Crew
            public const string TrainingProgram         = "crew_1";
            public const string RecreationalFacilities  = "crew_2";
            public const string CrewQuartersImprovement = "crew_3";

            // Propulsion
            public const string PlasmaExhaust           = "prop_1";
            public const string SlipstreamCalibration   = "prop_2";
            public const string EmergencyThrust         = "prop_3a";
            public const string ManeuveringThrusters    = "prop_3b";
            public const string LowEmissionDrive        = "prop_4";

            // Armor
            public const string ReactivePlating         = "armor_1";
            public const string AdaptiveComposite       = "armor_2";
            public const string AblativePlating         = "armor_3";
            public const string RegenerativeHull        = "armor_4";
            public const string NullImpactArmor         = "armor_5";

            // Shields
            public const string HarmonicShields         = "shields_1";
            public const string AdaptiveFrequencies     = "shields_2";
            public const string TessellatedBarrier      = "shields_3";
            public const string RegenerativeField       = "shields_4";
            public const string PhaseInversion          = "shields_5";

            // Combat
            public const string PredictiveTargeting     = "combat_1";
            public const string CoherenceAmplifier      = "combat_2a";
            public const string ShapedChargeWarheads    = "combat_2b";
            public const string ShieldDisruption        = "combat_3a";
            public const string WarheadMiniaturization  = "combat_3b";
            public const string OverchargeCapacitors    = "combat_4";
            public const string SplitFire               = "combat_5";

            // Starship Upgrades (equipment tier unlocks)
            public const string MarkIIEquipment         = "upgrades_1";
            public const string MarkIIIEquipment        = "upgrades_2";
            public const string MarkIVEquipment         = "upgrades_3";
            public const string MarkVEquipment          = "upgrades_4";
            public const string MarkVIEquipment         = "upgrades_5";
        }

        /// <summary>
        /// Tunable magnitudes for each research effect. Centralised here so balance can be
        /// adjusted without touching gameplay code. The frameworks that consume these live in
        /// <see cref="ResearchEffects"/> (combat) and GameManager (skill points / tier gating).
        /// </summary>
        public static class Effects
        {
            // ── Crew ────────────────────────────────────────────────────────────
            /// <summary>Extra skill points granted per character level by Training Program.</summary>
            public const int TrainingProgramPointsPerLevel = 1;

            /// <summary>Extra loyalty per day from Recreational Facilities (crew_2), when He3 > 0.</summary>
            public const float RecreationalFacilitiesLoyaltyPerDay = 1f;

            /// <summary>Extra loyalty cap from Crew Quarters Improvement (crew_3).</summary>
            public const int CrewQuartersMaxLoyaltyBonus = 20;

            // ── Propulsion ───────────────────────────────────────────────────────
            /// <summary>Fraction by which Plasma Exhaust (prop_1) reduces sub-light He3 costs.</summary>
            public const float PlasmaExhaustFuelReduction = 0.25f;

            /// <summary>Fraction by which Slipstream Calibration (prop_2) reduces FTL He3 costs.</summary>
            public const float SlipstreamCalibrationFuelReduction = 0.25f;

            /// <summary>Defense DC bonus while Emergency Thrust (prop_3a) is active.</summary>
            public const int EmergencyThrustDefenseBonus  = 2;
            /// <summary>Number of turns Emergency Thrust stays active after activation.</summary>
            public const int EmergencyThrustActiveTurns   = 3;
            /// <summary>Cooldown turns after Emergency Thrust expires before it can be used again.</summary>
            public const int EmergencyThrustCooldownTurns = 4;

            // ── Starship Upgrades — equipment tier gating ───────────────────────
            /// <summary>Research id that unlocks a given equipment tier (2-6). Tier 1 needs none.</summary>
            public static string EquipmentTierResearchId(int tier) => tier switch
            {
                2 => Ids.MarkIIEquipment,
                3 => Ids.MarkIIIEquipment,
                4 => Ids.MarkIVEquipment,
                5 => Ids.MarkVEquipment,
                6 => Ids.MarkVIEquipment,
                _ => null,
            };

            /// <summary>Display name of the research required to reach a given equipment tier (2-6).</summary>
            public static string EquipmentTierResearchName(int tier) => tier switch
            {
                2 => "Mark II Equipment",
                3 => "Mark III Equipment",
                4 => "Mark IV Equipment",
                5 => "Mark V Equipment",
                6 => "Mark VI Equipment",
                _ => "",
            };

            // ── Combat: player offense ──────────────────────────────────────────
            public const int   PredictiveTargetingAttackBonus    = 2;    // combat_1: +to-hit, all weapons
            public const int   CoherenceAmplifierHullPenetration = 2;    // combat_2a: reduces particle-cannon-vs-hull DR
            public const int   ShapedChargeShieldPenetration     = 4;    // combat_2b: reduces torpedo-vs-shield DR
            public const float ShieldDisruptionChance            = 0.25f; // combat_3a: chance per particle-cannon hit
            public const int   ShieldDisruptionAmount            = 2;    // combat_3a: enemy shield defense reduction
            public const float WarheadMiniaturizationTorpedoMult = 1.5f;  // combat_3b: torpedo magazine multiplier

            // ── Combat: player defense ──────────────────────────────────────────
            public const int HarmonicShieldsDefense      = 1;  // shields_1: +defense while shields up
            public const int AdaptiveFrequenciesDefense  = 1;  // shields_2: additional +defense while shields up
            public const int TessellatedBarrierDR        = 2;  // shields_3: damage reduction while shields up
            /// <summary>
            /// Multiplier applied to the shield-tier base regen by Regenerative Field (shields_4).
            /// Base regen = shieldTier pts/turn; researched = max(1, RoundToInt(shieldTier * mult)).
            /// </summary>
            public const float RegenerativeFieldRegenMultiplier = 1.5f;  // shields_4
            public const float PhaseInversionIgnoreChance = 0.10f; // shields_5: chance to ignore a hit while shields up

            public const int ReactivePlatingDR           = 1;  // armor_1: hull damage reduction
            public const int AdaptiveCompositeDR         = 1;  // armor_2: additional hull damage reduction
            public const int AblativePlatingDR           = 2;  // armor_3: additional hull damage reduction
            public const int RegenerativeHullPerTurn     = 2;  // armor_4: hull regen per combat turn (framework)
            public const float NullImpactIgnoreChance    = 0.10f; // armor_5: chance to ignore a hit while shields down

            public const int ManeuveringThrustersDefense = 1;  // prop_3b: +defense vs all attacks
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
