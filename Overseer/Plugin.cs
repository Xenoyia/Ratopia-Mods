using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Globalization;

namespace Overseer
{
    [BepInPlugin("overseer", "Overseer", "1.1.0")]
    public class OverseerPlugin : BaseUnityPlugin
    {
        public static OverseerPlugin Instance { get; private set; }

        private static readonly (string Category, string Field, string Description)[] FieldDescriptions = new[] {
            // General
            ("General", "CarrierLimit", "Maximum number of items a porter station can be set to transfer at once."),
            ("General", "m_BluePrintMax", "Maximum number of blueprints a player can have or use at once (limits construction planning or blueprint storage)."),
            ("General", "CustomWorkRange", "How far away a ratizen can interact with world objects (demolish, repair, water, mine etc.)"),
            ("General", "BodyDirtHour", "How long (in in-game hours) it takes for a dead citizen or bot to be removed from the map. In the game code, this is set to 576 hours, or 24 days."),
            ("General", "Time_Children", "How long (in in-game hours) it takes for a child to grow up."),
            ("General", "MaxWebNum", "Maximum number of webs that spiders can make in total before becoming idle."),
            
            // Citizen Stats & Needs
            ("Citizen Stats & Needs", "FatigueCut", "Fatigue threshold for citizens (higher = tire faster, may trigger rest or negative effects sooner)."),
            ("Citizen Stats & Needs", "HungerCut", "Hunger threshold for citizens (higher = get hungry faster, may trigger eating or negative effects sooner)."),
            ("Citizen Stats & Needs", "FunCut", "Fun threshold for citizens (higher = need fun more often, may trigger boredom or negative effects sooner)."),
            ("Citizen Stats & Needs", "CleanCut", "Cleanliness threshold for citizens (higher = get dirty faster, may trigger cleaning needs sooner)."),
            ("Citizen Stats & Needs", "FatigueMinimum", "Minimum possible fatigue value for a citizen. Fatigue cannot go below this value."),
            ("Citizen Stats & Needs", "Maximum_CitizenCondition", "Maximum condition/health value for citizens (upper bound for health/condition stats)."),
            ("Citizen Stats & Needs", "Maximum_Dodge", "Maximum dodge value for citizens (affects chance to avoid attacks or hazards)."),
            ("Citizen Stats & Needs", "IsCitizenWanderOK", "Allow citizens to wander freely when idle (true/false)."),
            ("Citizen Stats & Needs", "IsCitizenRebellionOK", "Allow citizens to rebel (true/false, disables rebellion if false)."),
            ("Citizen Stats & Needs", "DiseaseLv1Cut", "If a ratizen's cleanliness is below this value, they will get disease level 2."),
            ("Citizen Stats & Needs", "DiseaseLv2Per", "Percentage chance for disease level 2 to proceed to level 3."),
            ("Citizen Stats & Needs", "DiseaseLv3Per", "Percentage chance for disease to kill the ratizen."),

            // Enemies
            ("Enemies", "m_LizardAreaMax", "Maximum number of lizard camps that can spawn."),
            ("Enemies", "m_LizardAgainTerm", "Time (in days) between lizard invasions. It represents the number of days that must pass before a new lizard invasion can occur"),
            ("Enemies", "m_Weasel_ThemaPoint", "The prosperity point threshold for the game to transition from zombies to weasels. At this point, the game removes zombie holes, spawns weasel tents, and changes the enemy waves to weasels."),
            ("Enemies", "m_Lizard_ThemaPoint", "The prosperity point threshold for lizard enemies to transition from weasels to lizards. This removes all zombie holes, weasel tents, and changes the enemy waves to lizards."),
            ("Enemies", "Time_ZombieDieHunger", "Time (in hours) until a zombie dies from hunger (if applicable). If too low, they may die before even getting to you."),
            ("Enemies", "Time_WeaselBack", "Time (in hours) until a weasel retreats to their tent. Basically a cooldown period to ensure they engage in combat for this long."),
            
            // Work & Labor
            ("Work & Labor", "Mining_LaborValueOrigin", "Base amount paid for mining actions, will speed up or slow down mining."),
            ("Work & Labor", "Mining_FixValue", "Increases the amount of workload done per action. It says mining, but actually affects almost all work actions."),
            ("Work & Labor", "Gathering_LaborValueOrigin", "Base amount paid for gathering actions, will speed up or slow down gathering."),
            ("Work & Labor", "m_ServiceWorkExp", "Amount of experience gained per service work action (affects how quickly citizens level up or improve from service jobs)."),
            ("Work & Labor", "m_MaxCustomBuildingRange", "Maximum range for buildings, for example, the range of a hunter's hut. May lag on higher values. Adjusted range shows up after a building is constructed."),
            ("Work & Labor", "m_SoulConsumeLabor", "How much workload/labor is provided by consuming a soul."),

            // Events & Timers
            ("Events & Timers", "RandLoopEventCoolTime", "Cooldown (in minutes) for random loop events (e.g., random incidents or checks)."),
            ("Events & Timers", "PrisonTime", "How long (in hours) a citizen stays in prison after being arrested."),
            ("Events & Timers", "Time_Religion", "Minimum time (in hours) a ratizen is part of a religion."),
            ("Events & Timers", "DiseaseLv1_Time", "Time (in hours) for disease level 1 to resolve or progress."),
            ("Events & Timers", "DiseaseLv2_Time", "Time (in hours) for disease level 2 to resolve or progress."),
            ("Events & Timers", "DiseaseLv3_Time", "Time (in hours) for disease level 3 to resolve or progress."),
            ("Events & Timers", "DiseasePrevention_Time", "Time (in hours) that disease prevention effects last."),
            ("Events & Timers", "FloodMinimumHeight", "Minimum height of water for buildings to be disabled due to flooding."),

            // Research & Progression
            ("Research & Progression", "m_ResearchTime", "Base time required for research (in minutes) to complete a research project."),
            ("Research & Progression", "MaxConcurrentResearches", "Maximum number of research projects that can be active at once."),

            // Prosperity & Happiness
            ("Prosperity & Happiness", "Grade2GoldCut", "Threshold of gold for a citizen to be considered middle-class."),
            ("Prosperity & Happiness", "Grade2LifeCut", "Threshold of necessities for a citizen to be considered upper-class."),
            ("Prosperity & Happiness", "Grade3GoldCut", "Threshold of gold for a citizen to be considered upper-class."),
            ("Prosperity & Happiness", "Grade3LifeCut", "Threshold of necessities for a citizen to be considered upper-class."),
            ("Prosperity & Happiness", "Grade1ProsperityValue", "Prosperity points contributed by each grade 1 citizen to the city's total prosperity (used for city progression and upgrades)."),
            ("Prosperity & Happiness", "Grade2ProsperityValue", "Prosperity points contributed by each grade 2 citizen to the city's total prosperity (used for city progression and upgrades)."),
            ("Prosperity & Happiness", "Grade3ProsperityValue", "Prosperity points contributed by each grade 3 citizen to the city's total prosperity (used for city progression and upgrades)."),

            // Combat & Danger
            ("Combat & Danger", "TheftDamage", "Amount of damage (usually negative) applied to a citizen's happiness, gold, or other stat when a theft event occurs."),
            ("Combat & Danger", "DrownDmg", "Amount of damage (usually negative) applied to a citizen's health or happiness when they are drowning."),

            // Stat Ticks
            ("Stat Ticks", "FunTick", "How quickly fun decreases per tick (higher = faster loss of fun)."),
            ("Stat Ticks", "FatigueTick", "How quickly fatigue increases per tick (higher = tire faster)."),
            ("Stat Ticks", "HungerTick", "How quickly hunger increases per tick (higher = get hungry faster)."),
            ("Stat Ticks", "CleanTick", "How quickly cleanliness decreases per tick (higher = get dirty faster)."),
            ("Stat Ticks", "m_DurabilityTick", "Amount of damage buildings take from being attacked."),
        };

        public static int MaxConcurrentResearches = 3;
        public static ConfigEntry<int> CustomWorkRange;
        public static ConfigEntry<int> BlueprintBuildRange;
        public static ConfigEntry<float> MiddleClassWorkBuff;
        public static ConfigEntry<float> UpperClassWorkBuff;
        public static ConfigEntry<float> MiddleClassSpeedBuff;
        public static ConfigEntry<float> UpperClassSpeedBuff;
        public static ConfigEntry<bool> AlwaysSummonEnabled;
        public static ConfigEntry<float> StorageSlots;
        public static ConfigEntry<float> MiniStorageSlots;
        public static ConfigEntry<float> QuantumStorageSlots;
        public static ConfigEntry<bool> SuppressToiletSounds;
        public static ConfigEntry<bool> SuppressPoliceWhistle;
        public static ConfigEntry<bool> FloatingBlueprintsEnabled;
        public static ConfigEntry<bool> NoItemDespawnEnabled;
        public static ConfigEntry<int> CustomPopValue;
        public static ConfigEntry<int> ExtraMaxRatrons;
        public static ConfigEntry<bool> MakeInferniteMineable;
        public static ConfigEntry<bool> MakeBasaltMineable;
        public static ConfigEntry<bool> UnlockAllResearch;
        public static ConfigEntry<int> MinimumCarryCapacity;
        public static ConfigEntry<bool> AllowBlueprintPlanning;
        public static ConfigEntry<bool> DebugMode;

        // --- Prosperity DB1 Config ---
        public static Dictionary<int, ConfigEntry<string>> ProsperityNameConfigs = new();
        public static Dictionary<int, ConfigEntry<int>> ProsperityNeedValueConfigs = new();
        public static Dictionary<int, ConfigEntry<int>> ProsperityCitizenAbilityConfigs = new();
        public static Dictionary<int, ConfigEntry<int>> ProsperityPolicyNumConfigs = new();
        public static Dictionary<int, ConfigEntry<int>> ProsperityPopConfigs = new();

        // Queen Buffs
        public static class QueenBuffs
        {
            public static ConfigEntry<float> MoveSpeed;
            public static ConfigEntry<float> CarryCapacity;
            public static ConfigEntry<float> ExpRate;
            public static ConfigEntry<float> AttackPower;
            public static ConfigEntry<float> Defense;
            public static ConfigEntry<float> MaxHP;
            public static ConfigEntry<float> Strength;
            public static ConfigEntry<float> Dexterity;
            public static ConfigEntry<float> Intelligence;
            public static ConfigEntry<float> HealthRegen;
            public static ConfigEntry<float> DodgeChance;
        }

        private ConfigFile customConfig;
        private ConfigFile prosperityConfig;
        private ConfigFile queenConfig;

        // Add a static flag to track if we're in the middle of a placement
        private static bool isPlacingBlueprint = false;

        private void Awake()
        {
            Instance = this;
            try
            {
                // Use custom config file location
                string overseerDir = Path.Combine(Paths.PluginPath, "Overseer");
                Directory.CreateDirectory(overseerDir);
                string configPath = Path.Combine(overseerDir, "Config.cfg");
                customConfig = new ConfigFile(configPath, true);
                // Use a separate config file for Prosperity settings
                string prosperityPath = Path.Combine(overseerDir, "Prosperity.cfg");
                prosperityConfig = new ConfigFile(prosperityPath, true);
                // Queen Buffs config in Queen.cfg
                string queenPath = Path.Combine(overseerDir, "Queen.cfg");
                queenConfig = new ConfigFile(queenPath, true);

                // --- Static Prosperity Config Initialization (Levels 1-9) ---
                // Defaults from DB asset
                var prosperityDefaults = new[] {
                    new { Level = 1, Name = "Settlement",   NeedValue = 0,      Pop = 20,  CitizenAbilityValue = 2,  PolicyNum = 1 },
                    new { Level = 2, Name = "Hamlet",       NeedValue = 10000,  Pop = 35,  CitizenAbilityValue = 3,  PolicyNum = 2 },
                    new { Level = 3, Name = "Viliage",      NeedValue = 30000,  Pop = 50,  CitizenAbilityValue = 4,  PolicyNum = 3 },
                    new { Level = 4, Name = "Town",         NeedValue = 60000,  Pop = 65,  CitizenAbilityValue = 5,  PolicyNum = 4 },
                    new { Level = 5, Name = "Small City",   NeedValue = 100000, Pop = 80,  CitizenAbilityValue = 6,  PolicyNum = 6 },
                    new { Level = 6, Name = "City",         NeedValue = 150000, Pop = 90,  CitizenAbilityValue = 7,  PolicyNum = 8 },
                    new { Level = 7, Name = "Ratropolis",   NeedValue = 250000, Pop = 100, CitizenAbilityValue = 8,  PolicyNum = 10 },
                    new { Level = 8, Name = "Regalopolis",  NeedValue = 400000, Pop = 105, CitizenAbilityValue = 9,  PolicyNum = 12 },
                    new { Level = 9, Name = "Ratopia",      NeedValue = 700000, Pop = 110, CitizenAbilityValue = 10, PolicyNum = 15 },
                };
                for (int i = 0; i < prosperityDefaults.Length; i++)
                {
                    var def = prosperityDefaults[i];
                    string section = $"Level_{def.Level}";
                    ProsperityNameConfigs[def.Level] = prosperityConfig.Bind(section, "Name", def.Name, $"Override Name for Prosperity Level {def.Level} (leave blank for default)");
                    ProsperityNeedValueConfigs[def.Level] = prosperityConfig.Bind(section, "NeedValue", def.NeedValue, $"Override NeedValue for Prosperity Level {def.Level} (-1 for default)");
                    ProsperityCitizenAbilityConfigs[def.Level] = prosperityConfig.Bind(section, "CitizenAbilityValue", def.CitizenAbilityValue, $"Override CitizenAbilityValue for Prosperity Level {def.Level} (-1 for default)");
                    ProsperityPolicyNumConfigs[def.Level] = prosperityConfig.Bind(section, "PolicyNum", def.PolicyNum, $"Override PolicyNum for Prosperity Level {def.Level} (-1 for default)");
                    ProsperityPopConfigs[def.Level] = prosperityConfig.Bind(section, "Pop", def.Pop, $"Override Pop for Prosperity Level {def.Level} (-1 for default)");
                }
                var definesType = Type.GetType("Defines") ?? Type.GetType("Defines, Assembly-CSharp");
                if (definesType == null)
                {
                    Logger.LogError("Could not find Defines type!");
                    return;
                }
                foreach (var (category, fieldName, desc) in FieldDescriptions)
                {
                    var field = definesType.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field == null) continue;
                    if (field.FieldType == typeof(int))
                    {
                        int current = (int)field.GetValue(null);
                        var entry = customConfig.Bind(category, fieldName, current, desc);
                        SetReadonlyField(field, entry.Value);
                    }
                    else if (field.FieldType == typeof(float))
                    {
                        float current = (float)field.GetValue(null);
                        var entry = customConfig.Bind(category, fieldName, current, desc);
                        // Ensure float is parsed using InvariantCulture
                        float value = 0f;
                        if (entry.BoxedValue is string s)
                        {
                            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                        }
                        else
                        {
                            value = Convert.ToSingle(entry.BoxedValue, CultureInfo.InvariantCulture);
                        }
                        SetReadonlyField(field, value);
                    }
                    else if (field.FieldType == typeof(bool))
                    {
                        bool current = (bool)field.GetValue(null);
                        var entry = customConfig.Bind(category, fieldName, current, desc);
                        SetReadonlyField(field, entry.Value);
                    }
                }
                // Custom config for max concurrent researches
                var maxResearchEntry = customConfig.Bind("Special", "MaxConcurrentResearches", 3, "Maximum number of research projects that can be active at once.");
                MaxConcurrentResearches = maxResearchEntry.Value;
                CustomWorkRange = customConfig.Bind("Special", "CustomWorkRange", 2, "How far away ratizens can interact with world objects (demolish, repair, water, etc.)");
                BlueprintBuildRange = customConfig.Bind("Special", "BlueprintBuildRange", 5, "How far away you can be to build blueprints (default 5)");
                MiddleClassWorkBuff = customConfig.Bind("Citizen Stats & Needs", "MiddleClassWorkBuff", 0.05f, "Work buff for middle class citizens (e.g. 0.05 = 5%)");
                MiddleClassWorkBuff.Value = float.Parse(MiddleClassWorkBuff.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                UpperClassWorkBuff = customConfig.Bind("Citizen Stats & Needs", "UpperClassWorkBuff", 0.10f, "Work buff for upper class citizens (e.g. 0.10 = 10%)");
                UpperClassWorkBuff.Value = float.Parse(UpperClassWorkBuff.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                MiddleClassSpeedBuff = customConfig.Bind("Citizen Stats & Needs", "MiddleClassSpeedBuff", 0.05f, "Move speed buff for middle class citizens (e.g. 0.05 = 5%)");
                MiddleClassSpeedBuff.Value = float.Parse(MiddleClassSpeedBuff.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                UpperClassSpeedBuff = customConfig.Bind("Citizen Stats & Needs", "UpperClassSpeedBuff", 0.10f, "Move speed buff for upper class citizens (e.g. 0.10 = 10%)");
                UpperClassSpeedBuff.Value = float.Parse(UpperClassSpeedBuff.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                // Add AlwaysSummon config
                AlwaysSummonEnabled = customConfig.Bind("Special", "AlwaysSummonEnabled", false, "If true, you can always summon the Queen (removes restrictions).");
                // Add BiggerStorage config
                StorageSlots = customConfig.Bind("Special", "StorageSlots", 40f, "Slot count for Storage buildings");
                StorageSlots.Value = float.Parse(StorageSlots.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                MiniStorageSlots = customConfig.Bind("Special", "MiniStorageSlots", 20f, "Slot count for Mini Storage buildings");
                MiniStorageSlots.Value = float.Parse(MiniStorageSlots.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                QuantumStorageSlots = customConfig.Bind("Special", "QuantumStorageSlots", 10f, "Slot count for Quantum Storage buildings");
                QuantumStorageSlots.Value = float.Parse(QuantumStorageSlots.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                // Add NoNoisyToilets config
                SuppressToiletSounds = customConfig.Bind("Special", "SuppressToiletSounds", false, "If true, toilet sounds will be suppressed.");
                // Add NoNoisyPolice config
                SuppressPoliceWhistle = customConfig.Bind("Special", "SuppressPoliceWhistle", false, "If true, police whistle sounds will be suppressed.");
                // Add FloatingBlueprints config
                FloatingBlueprintsEnabled = customConfig.Bind("Special", "FloatingBlueprintsEnabled", false, "If true, blueprints can be placed without ground.");
                // Add NoItemDespawn config
                NoItemDespawnEnabled = customConfig.Bind("Special", "NoItemDespawnEnabled", false, "If true, items will not despawn or fade.");
                // Add CustomPopValue config
                CustomPopValue = customConfig.Bind("Special", "ExtraMaxPopulation", 0, "Extra max population value added to max population count.");
                // Add ExtraMaxRatrons config
                ExtraMaxRatrons = customConfig.Bind("Special", "ExtraMaxRatrons", 0, "Extra max Ratrons allowed above the normal limit.");
                // Set Defines.TileObject_LimitMinute to int.MaxValue if enabled
                if (NoItemDespawnEnabled.Value)
                {
                    var despawnDefinesType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => a.GetTypes())
                        .FirstOrDefault(t => t.Name == "Defines");
                    if (despawnDefinesType != null)
                    {
                        var field = despawnDefinesType.GetField("TileObject_LimitMinute", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field != null)
                            field.SetValue(null, int.MaxValue);
                        else
                            Logger.LogWarning("Could not find TileObject_LimitMinute field on Defines!");
                    }
                    else
                    {
                        Logger.LogWarning("Could not find Defines type!");
                    }
                }
                // Add mineable configs
                MakeInferniteMineable = customConfig.Bind("Special", "MakeInferniteMineable", false, "If true, Infernite (element 29) will be mineable.");
                MakeBasaltMineable = customConfig.Bind("Special", "MakeBasaltMineable", false, "If true, Basalt (element 20) will be mineable.");
                UnlockAllResearch = customConfig.Bind("Special", "UnlockAllResearch", false, "If true, all research will be unlocked on load using the cheat function.");
                // Add MinimumCarryCapacity config
                MinimumCarryCapacity = customConfig.Bind("General", "MinimumCarryCapacity", 2, "Minimum carry capacity for all units (Defines.Minimum_Capacity)");
                // Set Defines.Minimum_Capacity
                var definesType2 = Type.GetType("Defines") ?? Type.GetType("Defines, Assembly-CSharp");
                if (definesType2 != null)
                {
                    var minCapField = definesType2.GetField("Minimum_Capacity", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (minCapField != null)
                        minCapField.SetValue(null, MinimumCarryCapacity.Value);
                }
                // Add DebugMode config
                DebugMode = customConfig.Bind("Special", "DebugMode", false, "Enables cheats and debug features (F1 puts a ton of resources in your storage, F2 shows ratizen paths, F3 shows co-ordinates of every tile, F5 quicksaves, F7 disables the UI, F8 opens the cheat menu). Most debug actions are written in Korean.");
                // Special handling for DebugMode
                if (DebugMode.Value)
                {
                    // Set Cheat and IsPublicVersion
                    var cheatField = definesType2.GetField("Cheat", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    var isPublicVersionField = definesType2.GetField("IsPublicVersion", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (cheatField != null) cheatField.SetValue(null, true);
                    if (isPublicVersionField != null) isPublicVersionField.SetValue(null, false);
                }

                // Queen Buffs config in Queen.cfg
                QueenBuffs.MoveSpeed = queenConfig.Bind("Queen Buffs", "MoveSpeed", 0.0f, "Percentage move speed bonus (0-100)");
                QueenBuffs.CarryCapacity = queenConfig.Bind("Queen Buffs", "CarryCapacity", 0.0f, "Flat carry capacity bonus");
                QueenBuffs.ExpRate = queenConfig.Bind("Queen Buffs", "ExpRate", 0.0f, "Percentage extra EXP (0-100)");
                QueenBuffs.AttackPower = queenConfig.Bind("Queen Buffs", "AttackPower", 0.0f, "Attack power bonus");
                QueenBuffs.Defense = queenConfig.Bind("Queen Buffs", "Defense", 0.0f, "Defense bonus");
                QueenBuffs.MaxHP = queenConfig.Bind("Queen Buffs", "MaxHP", 0.0f, "Max HP bonus");
                QueenBuffs.Strength = queenConfig.Bind("Queen Buffs", "Strength", 0.0f, "Strength bonus");
                QueenBuffs.Dexterity = queenConfig.Bind("Queen Buffs", "Dexterity", 0.0f, "Dexterity bonus");
                QueenBuffs.Intelligence = queenConfig.Bind("Queen Buffs", "Intelligence", 0.0f, "Intelligence bonus");
                QueenBuffs.HealthRegen = queenConfig.Bind("Queen Buffs", "HealthRegen", 0.0f, "HP regen per tick");
                QueenBuffs.DodgeChance = queenConfig.Bind("Queen Buffs", "DodgeChance", 0.0f, "Dodge chance percentage (0-100)");
                // --- Prosperity DB1 Config Setup ---
                // (Dynamic config creation is now handled in Patch_DB_Mgr_Awake below)
                Logger.LogInfo("Overseer by Xenoyia loaded and applied config values!");
                Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
                // Add AllowBlueprintPlanning config
                AllowBlueprintPlanning = customConfig.Bind("Special", "AllowBlueprintPlanning", false, "Allow placing blueprints even if required resources are not in storage (planning mode)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Overseer failed: {ex}");
            }
        }

        // Helper to set readonly static fields via reflection
        private static void SetReadonlyField(FieldInfo field, object value)
        {
            try
            {
                // Remove readonly flag if needed
                if (field.IsInitOnly)
                {
                    var attr = typeof(FieldInfo).GetField("m_fieldAttributes", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (attr != null)
                    {
                        var attrs = (FieldAttributes)attr.GetValue(field);
                        attr.SetValue(field, attrs & ~FieldAttributes.InitOnly);
                    }
                }
                field.SetValue(null, value);
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("Overseer").LogError($"Failed to set field {field.Name}: {ex}");
            }
        }

        // Harmony transpiler to patch Tech_RPInfo.UpgradBtn research limit
        [HarmonyPatch(typeof(Tech_RPInfo), "UpgradBtn")]
        public static class Patch_Tech_RPInfo_UpgradBtn
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var maxResearchesField = AccessTools.Field(typeof(OverseerPlugin), nameof(MaxConcurrentResearches));
                foreach (var code in instructions)
                {
                    // Replace ldc.i4.3 (pushes 3 onto the stack) with ldsfld for MaxConcurrentResearches
                    if (code.opcode == OpCodes.Ldc_I4_3)
                    {
                        yield return new CodeInstruction(OpCodes.Ldsfld, maxResearchesField);
                    }
                    else
                    {
                        yield return code;
                    }
                }
            }
        }

        // Helper to generate offset list for List_WM_EnableArea
        public static List<Vector2Int> GetWorkAreaOffsets(int extraRange)
        {
            // Vanilla offsets
            var vanilla = new List<Vector2Int>
            {
                new Vector2Int(-1, 0),
                new Vector2Int(1, 0),
                new Vector2Int(0, 0),
                new Vector2Int(-1, 1),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(-1, -1),
                new Vector2Int(0, -1),
                new Vector2Int(1, -1),
                new Vector2Int(0, -2),
                new Vector2Int(-1, -2),
                new Vector2Int(1, -2),
            };

            if (extraRange <= 0)
                return new List<Vector2Int>(vanilla);

            var result = new List<Vector2Int>(vanilla);
            var used = new HashSet<Vector2Int>(vanilla);

            // For each vanilla offset except (0,0), treat as a direction
            foreach (var offset in vanilla)
            {
                if (offset == Vector2Int.zero)
                    continue;

                // Direction vector from (0,0) to offset
                int dx = offset.x;
                int dy = offset.y;
                int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
                // Normalize direction
                int dirX = (dx == 0) ? 0 : dx / Math.Abs(dx);
                int dirY = (dy == 0) ? 0 : dy / Math.Abs(dy);

                // For each extra range, add a new point in this direction
                for (int n = 1; n <= extraRange; n++)
                {
                    var newOffset = new Vector2Int(dx + dirX * n, dy + dirY * n);
                    if (!used.Contains(newOffset))
                    {
                        result.Add(newOffset);
                        used.Add(newOffset);
                    }
                }
            }

            return result;
        }

        // Patch for SystemMgr.Awake to override List_WM_EnableArea
        [HarmonyPatch]
        public static class SystemMgr_Awake_Patch
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("SystemMgr");
                return type?.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            static void Postfix(object __instance)
            {
                var field = __instance.GetType().GetField("List_WM_EnableArea", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    int extraRange = CustomWorkRange.Value - 2; // 2 is vanilla's max manhattan distance
                    field.SetValue(__instance, OverseerPlugin.GetWorkAreaOffsets(extraRange));
                }
            }
        }

        // Patch for BuildingMgr.GetEnableBP_Building to use BlueprintBuildRange (transpiler to be added)
        [HarmonyPatch]
        public static class BuildingMgr_GetEnableBP_Building_Patch
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("BuildingMgr");
                return type?.GetMethod("GetEnableBP_Building", new[] { AccessTools.TypeByName("GameUnit"), AccessTools.TypeByName("BP_State") });
            }
        }

        [HarmonyPatch]
        public static class Patch_Helpers_GetGradeLaborEffect
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("Helpers");
                var method = type?.GetMethod("GetGradeLaborEffect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return method;
            }
            static bool Prefix(CitizenGrade _grade, ref float __result)
            {
                try
                {
                    if (_grade == CitizenGrade.Grade_Low)
                        __result = 0f;
                    else if (_grade == CitizenGrade.Grade_Mid)
                        __result = OverseerPlugin.MiddleClassWorkBuff.Value;
                    else if (_grade == CitizenGrade.Grade_High)
                        __result = OverseerPlugin.UpperClassWorkBuff.Value;
                    else
                        __result = 0f;
                }
                catch (Exception)
                {
                    __result = 0f;
                }
                return false;
            }
        }

        [HarmonyPatch]
        public static class Patch_Helpers_GetGradeSpeedEffect
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("Helpers");
                var method = type?.GetMethod("GetGradeSpeedEffect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return method;
            }
            static bool Prefix(CitizenGrade _grade, ref float __result)
            {
                try
                {
                    if (_grade == CitizenGrade.Grade_Low)
                        __result = 0f;
                    else if (_grade == CitizenGrade.Grade_Mid)
                        __result = OverseerPlugin.MiddleClassSpeedBuff.Value;
                    else if (_grade == CitizenGrade.Grade_High)
                        __result = OverseerPlugin.UpperClassSpeedBuff.Value;
                    else
                        __result = 0f;
                }
                catch (Exception)
                {
                    __result = 0f;
                }
                return false;
            }
        }

        // Remove Patch_GameMgr_Awake
        // Add a Harmony patch for SystemMgr.Start to refresh citizen grade buffs after initialization
        [HarmonyPatch]
        public static class Patch_SystemMgr_Start
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("SystemMgr");
                return type?.GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            static void Postfix(object __instance)
            {
                // Unlock all research if enabled
                if (OverseerPlugin.UnlockAllResearch != null && OverseerPlugin.UnlockAllResearch.Value)
                {
                    try
                    {
                        var debugMgrType = AccessTools.TypeByName("DebugMgr");
                        var cheatMgrField = debugMgrType?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                        if (cheatMgrField != null)
                        {
                            var cheatMgr = cheatMgrField.GetType().GetField("_CheatMgr", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(cheatMgrField);
                            if (cheatMgr != null)
                            {
                                var unlockMethod = cheatMgr.GetType().GetMethod("Unlock_All_ResearchBtn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                if (unlockMethod != null)
                                {
                                    unlockMethod.Invoke(cheatMgr, null);
                                }
                                else
                                {
                                    BepInEx.Logging.Logger.CreateLogSource("Overseer").LogWarning("Unlock_All_ResearchBtn method not found on CheatMgr.");
                                }
                            }
                            else
                            {
                                BepInEx.Logging.Logger.CreateLogSource("Overseer").LogWarning("_CheatMgr is null on DebugMgr.Instance.");
                            }
                        }
                        else
                        {
                            BepInEx.Logging.Logger.CreateLogSource("Overseer").LogWarning("DebugMgr.Instance is null.");
                        }
                    }
                    catch (Exception ex)
                    {
                        BepInEx.Logging.Logger.CreateLogSource("Overseer").LogError($"Failed to unlock all research: {ex}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(T_Citizen), "MayICallQueenPos")]
        public static class Patch_T_Citizen_MayICallQueenPos
        {
            static bool Prefix(ref bool __result)
            {
                if (AlwaysSummonEnabled != null && AlwaysSummonEnabled.Value)
                {
                    __result = true;
                    return false; // Always allow summon
                }
                return true; // Run original if not enabled
            }
        }

        [HarmonyPatch(typeof(StorageInfo), "Init")]
        public static class Patch_StorageInfo_Init
        {
            static void Postfix(StorageInfo __instance)
            {
                var info = __instance.m_Building?.m_Info;
                if (info == null) return;
                if (info.BuildingNameToString == "Storage")
                {
                    info.EffectValue1_Num = StorageSlots.Value;
                }
                else if (info.BuildingNameToString == "MiniStorage")
                {
                    info.EffectValue1_Num = MiniStorageSlots.Value;
                }
                else if (info.BuildingNameToString == "ElecStorage")
                {
                    info.EffectValue1_Num = QuantumStorageSlots.Value;
                }
            }
        }

        // --- No Noisy Toilets/Police Feature ---
        [HarmonyPatch(typeof(CasselGames.Audio.AudioController), "PlaySFXOneShotAnimation", typeof(string), typeof(Vector2), typeof(Vector2), typeof(float), typeof(float), typeof(bool), typeof(Action))]
        public static class Patch_AudioController_PlaySFXOneShotAnimation
        {
            static bool Prefix(string animationKey, Vector2 viewPos, Vector2 actorPos, float volume = 1f, float pitch = 1f, bool ignorePause = false, Action endCallback = null)
            {
                if (SuppressToiletSounds != null && SuppressToiletSounds.Value && (animationKey == "Toilet" || animationKey == "Toilet_Elec"))
                {
                    return false;
                }
                if (SuppressPoliceWhistle != null && SuppressPoliceWhistle.Value && (animationKey == "Idle_Police2" || animationKey == "Idle_Police3"))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(CasselGames.Audio.AudioController), "PlaySFXOneShot", typeof(string), typeof(float), typeof(bool), typeof(Action))]
        public static class Patch_AudioController_PlaySFXOneShot1
        {
            static bool Prefix(string key, float volume = 1f, bool isLoop = false, Action endCallback = null)
            {
                if (SuppressToiletSounds != null && SuppressToiletSounds.Value && (key == "SFX_Build_Toilet_F_Full" || key == "SFX_Build_Toilet_Elec_F_Full"))
                {
                    return false;
                }
                if (SuppressPoliceWhistle != null && SuppressPoliceWhistle.Value && (key == "SFX_Citizen_Idle_Police2_F_Full" || key == "SFX_Citizen_Idle_Police3_F_Full"))
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(CasselGames.Audio.AudioController), "PlaySFXOneShot", typeof(string), typeof(Vector2), typeof(Vector2), typeof(float), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(Action))]
        public static class Patch_AudioController_PlaySFXOneShot2
        {
            static bool Prefix(string key, Vector2 viewPos, Vector2 actorPos, float volume = 1f, bool isOverlap = true, bool isLoop = false, bool useDistance = true, bool ignorePause = false, Action endCallback = null)
            {
                if (SuppressToiletSounds != null && SuppressToiletSounds.Value && (key == "SFX_Build_Toilet_F_Full" || key == "SFX_Build_Toilet_Elec_F_Full"))
                {
                    return false;
                }
                if (SuppressPoliceWhistle != null && SuppressPoliceWhistle.Value && (key == "SFX_Citizen_Idle_Police2_F_Full" || key == "SFX_Citizen_Idle_Police3_F_Full"))
                {
                    return false;
                }
                return true;
            }
        }

        // --- Floating Blueprints Feature ---
        [HarmonyPatch]
        public static class Patch_MiningBox_MiningBoxSet_AllowFloatingBlueprints
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("MiningBox");
                return type?.GetMethod("MiningBoxSet", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            static void Postfix(object __instance, object _mode, bool _delete, int _num)
            {
                if (FloatingBlueprintsEnabled != null && FloatingBlueprintsEnabled.Value)
                {
                    // Only act in Building mode and not deleting
                    if (_mode.ToString() != "Building" || (bool)_delete) return;
                    var buildInfoField = __instance.GetType().GetField("m_BuildInfo", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var buildInfo = buildInfoField?.GetValue(__instance);
                    if (buildInfo == null) return;
                    var listCondField = buildInfo.GetType().GetField("List_Condition", BindingFlags.Instance | BindingFlags.Public);
                    var listCond = listCondField?.GetValue(buildInfo) as System.Collections.IList;
                    if (listCond == null || listCond.Count == 0) return;
                    var buildCondType = listCond[0].GetType();
                    var onlyGround = Enum.Parse(buildCondType, "OnlyGround");
                    if (listCond.Contains(onlyGround))
                    {
                        listCond.Remove(onlyGround);
                    }
                }
            }
        }

        // --- No Item Despawn Feature ---
        [HarmonyPatch(typeof(TileObject), "LifeTimeCheck")]
        public static class Patch_TileObject_LifeTimeCheck
        {
            static bool Prefix(ref bool __result)
            {
                if (NoItemDespawnEnabled != null && NoItemDespawnEnabled.Value)
                {
                    __result = false; // Never despawn, never fade
                    return false; // Skip original method
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(CocosFunc), "FadeOut2")]
        [HarmonyPrefix]
        public static bool Patch_CocosFunc_FadeOut2(CocosFunc __instance, float time, float rate, ref SpriteRenderer sp)
        {
            if (NoItemDespawnEnabled != null && NoItemDespawnEnabled.Value && sp != null && sp.gameObject.GetComponent<TileObject>() != null)
            {
                if (sp != null) sp.color = new Color(sp.color.r, sp.color.g, sp.color.b, 1f);
                return false;
            }
            return true;
        }

        // Patch ProspertiyInfo to set m_CustomPopValue from config
        [HarmonyPatch]
        public static class Patch_ProspertiyInfo_Constructor
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("ProspertiyInfo");
                var paramType = AccessTools.TypeByName("Prosperity_DB1+Param");
                return type?.GetConstructor(new[] { paramType });
            }
            static void Postfix(object __instance)
            {
                try
                {
                    var levelField = __instance.GetType().GetField("Level", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    int level = (int)(levelField?.GetValue(__instance) ?? 0);
                    // Always apply config for levels 1-9
                    if (OverseerPlugin.ProsperityNameConfigs.TryGetValue(level, out var nameCfg))
                    {
                        var nameField = __instance.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (nameField != null) nameField.SetValue(__instance, nameCfg.Value);
                    }
                    if (OverseerPlugin.ProsperityNeedValueConfigs.TryGetValue(level, out var needCfg))
                    {
                        var needField = __instance.GetType().GetField("NeedValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (needField != null) needField.SetValue(__instance, needCfg.Value);
                    }
                    if (OverseerPlugin.ProsperityCitizenAbilityConfigs.TryGetValue(level, out var caCfg))
                    {
                        var caField = __instance.GetType().GetField("CitizenAbility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (caField != null) caField.SetValue(__instance, caCfg.Value);
                    }
                    if (OverseerPlugin.ProsperityPolicyNumConfigs.TryGetValue(level, out var polCfg))
                    {
                        var polField = __instance.GetType().GetField("PolicyNum", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (polField != null) polField.SetValue(__instance, polCfg.Value);
                    }
                    if (OverseerPlugin.ProsperityPopConfigs.TryGetValue(level, out var popCfg))
                    {
                        var popField = __instance.GetType().GetField("Pop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (popField != null) popField.SetValue(__instance, popCfg.Value);
                    }
                    // Additive pop patch
                    var field = __instance.GetType().GetField("Pop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field != null && OverseerPlugin.CustomPopValue != null)
                    {
                        int original = (int)field.GetValue(__instance);
                        int newValue = original + OverseerPlugin.CustomPopValue.Value;
                        field.SetValue(__instance, newValue);
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("Overseer").LogError($"Failed to set ProspertiyInfo fields: {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_DB_Mgr_Res_DB_Setting
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("DB_Mgr");
                return type?.GetMethod("Res_DB_Setting", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            static void Postfix(object __instance)
            {
                try
                {
                    var resDbField = __instance.GetType().GetField("m_Res_DB1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var resDb = resDbField?.GetValue(__instance);
                    if (resDb != null)
                    {
                        var sheetsProp = resDb.GetType().GetField("sheets");
                        var sheets = sheetsProp?.GetValue(resDb) as System.Collections.IList;
                        if (sheets != null && sheets.Count > 0)
                        {
                            var listField = sheets[0].GetType().GetField("list");
                            var list = listField?.GetValue(sheets[0]) as System.Collections.IList;
                            if (list != null)
                            {
                                // Patch Mine field for Infernite and Basalt by copying from Iron (index 101) if enabled
                                // Infernite: element 29
                                if (OverseerPlugin.MakeInferniteMineable != null && OverseerPlugin.MakeInferniteMineable.Value && list.Count > 29)
                                {
                                    var param = list[29];
                                    var catField = param.GetType().GetField("Category");
                                    var semiTypeField = param.GetType().GetField("SemiType");
                                    if (catField != null) catField.SetValue(param, 0); // 0 = mineable
                                    if (semiTypeField != null) semiTypeField.SetValue(param, 4); // 4 = mineable semi type
                                }
                                // Basalt: element 20
                                if (OverseerPlugin.MakeBasaltMineable != null && OverseerPlugin.MakeBasaltMineable.Value && list.Count > 20)
                                {
                                    var param = list[20];
                                    var catField = param.GetType().GetField("Category");
                                    var semiTypeField = param.GetType().GetField("SemiType");
                                    if (catField != null) catField.SetValue(param, 0); // 0 = mineable
                                    if (semiTypeField != null) semiTypeField.SetValue(param, 4); // 4 = mineable semi type
                                }
                                // Also patch Dic_TileDB for Infernite and Basalt
                                var tileDbField = __instance.GetType().GetField("Dic_TileDB", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                var tileDb = tileDbField?.GetValue(__instance) as System.Collections.IDictionary;
                                var tileTypeEnum = AccessTools.TypeByName("TileType");
                                if (tileDb != null && tileTypeEnum != null)
                                {
                                    var inferniteEnum = Enum.Parse(tileTypeEnum, "Infernite");
                                    var basaltEnum = Enum.Parse(tileTypeEnum, "Basalt");
                                    // Patch Infernite
                                    if (OverseerPlugin.MakeInferniteMineable != null && OverseerPlugin.MakeInferniteMineable.Value && tileDb.Contains(inferniteEnum))
                                    {
                                        var tileInfo = tileDb[inferniteEnum];
                                        var catField = tileInfo.GetType().GetField("Category");
                                        var semiTypeField = tileInfo.GetType().GetField("SemiType");
                                        if (catField != null) catField.SetValue(tileInfo, Enum.ToObject(AccessTools.TypeByName("ResCateogry"), 0));
                                        if (semiTypeField != null) semiTypeField.SetValue(tileInfo, 4);
                                    }
                                    // Patch Basalt
                                    if (OverseerPlugin.MakeBasaltMineable != null && OverseerPlugin.MakeBasaltMineable.Value && tileDb.Contains(basaltEnum))
                                    {
                                        var tileInfo = tileDb[basaltEnum];
                                        var catField = tileInfo.GetType().GetField("Category");
                                        var semiTypeField = tileInfo.GetType().GetField("SemiType");
                                        if (catField != null) catField.SetValue(tileInfo, Enum.ToObject(AccessTools.TypeByName("ResCateogry"), 0));
                                        if (semiTypeField != null) semiTypeField.SetValue(tileInfo, 4);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("Overseer").LogError($"Failed to patch mineable resources: {ex}");
                }
            }
        }

        [HarmonyPatch]
        public static class Patch_C_Tile_MakeTile
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("C_Tile");
                return type?.GetMethod("MakeTile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            static void Postfix(object __instance)
            {
                try
                {
                    var tileTypeField = __instance.GetType().GetField("m_TileType");
                    var isMineField = __instance.GetType().GetField("IsMine");
                    if (tileTypeField != null && isMineField != null)
                    {
                        var tileType = tileTypeField.GetValue(__instance);
                        if (tileType != null)
                        {
                            string typeName = tileType.ToString();
                            if ((typeName == "Infernite" && OverseerPlugin.MakeInferniteMineable != null && OverseerPlugin.MakeInferniteMineable.Value) ||
                                (typeName == "Basalt" && OverseerPlugin.MakeBasaltMineable != null && OverseerPlugin.MakeBasaltMineable.Value))
                            {
                                isMineField.SetValue(__instance, true);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("Overseer").LogError($"Failed to patch IsMine for special tiles: {ex}");
                }
            }
        }

        // Helper to clone a list (shallow copy)
        private static object CloneList(object list)
        {
            var type = list.GetType();
            var clone = Activator.CreateInstance(type);
            var addMethod = type.GetMethod("Add");
            foreach (var item in (System.Collections.IEnumerable)list)
                addMethod.Invoke(clone, new[] { item });
            return clone;
        }

        [HarmonyPatch]
        public static class Patch_C_Tile_DestroyTile
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("C_Tile");
                return type?.GetMethod("DestroyTile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            static void Postfix(object __instance)
            {
                try
                {
                    var tileTypeField = __instance.GetType().GetField("m_TileType");
                    var xField = __instance.GetType().GetField("m_X");
                    var yField = __instance.GetType().GetField("m_Y");
                    if (tileTypeField == null || xField == null || yField == null) return;
                    var tileType = tileTypeField.GetValue(__instance)?.ToString();
                    if (tileType != "Infernite" || OverseerPlugin.MakeInferniteMineable == null || !OverseerPlugin.MakeInferniteMineable.Value) return;
                    int x = (int)xField.GetValue(__instance);
                    int y = (int)yField.GetValue(__instance);
                    FloodFillDestroyLavaTiles(new UnityEngine.Vector2Int(x, y));
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("Overseer").LogError($"Failed to destroy adjacent lava tiles: {ex}");
                }
            }
        }

        // Helper: Recursively destroy all adjacent LavaTile and LavaTile2 objects
        private static void FloodFillDestroyLavaTiles(UnityEngine.Vector2Int start)
        {
            // Get GameMgr.Instance
            var gameMgrType = AccessTools.TypeByName("GameMgr");
            var instanceProp = gameMgrType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var gameMgrInstance = instanceProp?.GetValue(null);
            if (gameMgrInstance == null) return;
            // Get _MapObjMgr
            var mapObjMgrField = gameMgrType.GetField("_MapObjMgr", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var mapObjMgr = mapObjMgrField?.GetValue(gameMgrInstance);
            if (mapObjMgr == null) return;
            var listLavaTileField = mapObjMgr.GetType().GetField("List_LavaTile");
            var listLavaTile2Field = mapObjMgr.GetType().GetField("List_LavaTile2");
            var listLavaTile = listLavaTileField?.GetValue(mapObjMgr) as System.Collections.IEnumerable;
            var listLavaTile2 = listLavaTile2Field?.GetValue(mapObjMgr) as System.Collections.IEnumerable;
            var visited = new HashSet<UnityEngine.Vector2Int>();
            var queue = new Queue<UnityEngine.Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);
            int[] dx = { 0, 1, 0, -1 };
            int[] dy = { 1, 0, -1, 0 };

            // First pass: collect all connected lava tile positions
            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                // Check adjacent positions
                for (int d = 0; d < 4; d++)
                {
                    var next = new UnityEngine.Vector2Int(pos.x + dx[d], pos.y + dy[d]);
                    if (!visited.Contains(next))
                    {
                        bool isLava = false;
                        // Check if next is a lava tile in List_LavaTile
                        if (listLavaTile != null)
                        {
                            foreach (var lava in listLavaTile)
                            {
                                var buildPosField = lava.GetType().GetField("List_BuildPos");
                                var buildPosList = buildPosField?.GetValue(lava) as System.Collections.IEnumerable;
                                if (buildPosList != null)
                                {
                                    foreach (UnityEngine.Vector2Int lavaPos in buildPosList)
                                    {
                                        if (lavaPos == next) { isLava = true; break; }
                                    }
                                }
                                if (isLava) break;
                            }
                        }
                        // Check if next is a lava tile in List_LavaTile2
                        if (!isLava && listLavaTile2 != null)
                        {
                            foreach (var lava in listLavaTile2)
                            {
                                var buildPosField = lava.GetType().GetField("List_BuildPos");
                                var buildPosList = buildPosField?.GetValue(lava) as System.Collections.IEnumerable;
                                if (buildPosList != null)
                                {
                                    foreach (UnityEngine.Vector2Int lavaPos in buildPosList)
                                    {
                                        if (lavaPos == next) { isLava = true; break; }
                                    }
                                }
                                if (isLava) break;
                            }
                        }
                        if (isLava)
                        {
                            queue.Enqueue(next);
                            visited.Add(next);
                        }
                    }
                }
            }

            // Second pass: Demolish all lava tiles whose List_BuildPos contains any of the visited positions
            if (listLavaTile != null)
            {
                var toDemolish = new List<object>();
                foreach (var lava in listLavaTile)
                {
                    var buildPosField = lava.GetType().GetField("List_BuildPos");
                    var buildPosList = buildPosField?.GetValue(lava) as System.Collections.IEnumerable;
                    if (buildPosList != null)
                    {
                        foreach (UnityEngine.Vector2Int lavaPos in buildPosList)
                        {
                            if (visited.Contains(lavaPos))
                            {
                                toDemolish.Add(lava);
                                break;
                            }
                        }
                    }
                }
                foreach (var lava in toDemolish)
                {
                    var demolition = lava.GetType().GetMethod("Demolition", new Type[] { typeof(bool) });
                    demolition?.Invoke(lava, new object[] { true });
                }
            }
            if (listLavaTile2 != null)
            {
                var toDemolish = new List<object>();
                foreach (var lava in listLavaTile2)
                {
                    var buildPosField = lava.GetType().GetField("List_BuildPos");
                    var buildPosList = buildPosField?.GetValue(lava) as System.Collections.IEnumerable;
                    if (buildPosList != null)
                    {
                        foreach (UnityEngine.Vector2Int lavaPos in buildPosList)
                        {
                            if (visited.Contains(lavaPos))
                            {
                                toDemolish.Add(lava);
                                break;
                            }
                        }
                    }
                }
                foreach (var lava in toDemolish)
                {
                    var demolition = lava.GetType().GetMethod("Demolition", new Type[] { typeof(bool) });
                    demolition?.Invoke(lava, new object[] { true });
                }
            }
        }

        // --- Queen Buff Patch ---
        [HarmonyPatch]
        public static class Patch_T_Queen_LoadSetting
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("T_Queen");
                return type?.GetMethod("LoadSetting", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { AccessTools.TypeByName("QueenData"), typeof(bool) }, null);
            }
            static void Postfix(T_Queen __instance)
            {
                if (__instance.m_Buff.IsExistRef("Overseer"))
                    __instance.m_Buff.RefKill("Overseer");
                OverseerPlugin.ApplyQueenBuff(__instance);
            }
        }
        public static void ApplyQueenBuff(T_Queen queen)
        {
            queen.m_Buff.RefKill("Overseer");
            var abilities = new List<Res_Ability>();
            var values = new List<float>();
            void AddBuff(Res_Ability ability, float value)
            {
                if (value > 0.0f)
                {
                    abilities.Add(ability);
                    values.Add(value);
                }
            }
            AddBuff(Res_Ability.SPD, QueenBuffs.MoveSpeed.Value);      // SPD
            AddBuff(Res_Ability.TP, QueenBuffs.CarryCapacity.Value);  // TP
            AddBuff(Res_Ability.EXP, QueenBuffs.ExpRate.Value);       // EXP
            AddBuff(Res_Ability.ATK, QueenBuffs.AttackPower.Value);    // ATK
            AddBuff(Res_Ability.DEF, QueenBuffs.Defense.Value);        // DEF
            AddBuff(Res_Ability.HP, QueenBuffs.MaxHP.Value);          // HP
            AddBuff(Res_Ability.STR, QueenBuffs.Strength.Value);       // STR
            AddBuff(Res_Ability.DEX, QueenBuffs.Dexterity.Value);      // DEX
            AddBuff(Res_Ability.INT, QueenBuffs.Intelligence.Value);   // INT
            AddBuff(Res_Ability.HpGen, QueenBuffs.HealthRegen.Value);  // HpGen
            AddBuff(Res_Ability.Dodge, QueenBuffs.DodgeChance.Value);  // Dodge
            if (abilities.Count == 0) return; // No buffs to apply
            for (int i = 0; i < abilities.Count; i++)
            {
                var ability = abilities[i];
                var value = values[i];
                // Divide by 100 for certain abilities, just like vanilla
                if (ability == Res_Ability.SLP || ability == Res_Ability.FUN || ability == Res_Ability.CLN ||
                    ability == Res_Ability.SPD || ability == Res_Ability.HUG || ability == Res_Ability.LIF ||
                    ability == Res_Ability.PDT || ability == Res_Ability.EXP)
                {
                    if (Mathf.Abs(value) >= 1.0f)
                        value /= 100f;
                }
                var buff = CitizenBuff.ResToBuff(ability, value);
                queen.m_Buff.BuffRefSet(buff, "Overseer", C_Buff_Category.Character, value, -999);
            }
        }

        // Keep the BuildMid_NeedMatSlot.IsSatisfy patch but modify it to work with the flag
        [HarmonyPatch]
        public static class Patch_BuildMid_NeedMatSlot_IsSatisfy
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("BuildMid_NeedMatSlot");
                return type?.GetMethod("IsSatisfy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            static bool Prefix(ref bool __result)
            {
                if (AllowBlueprintPlanning != null && AllowBlueprintPlanning.Value && !isPlacingBlueprint)
                {
                    __result = true;
                    return false; // Skip original
                }
                return true; // Run original
            }
        }

        // --- Deduplicate overlapping blueprints after placement ---
        [HarmonyPatch]
        public static class Patch_BP_Building_BluePrintSet_Deduplicate
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("BP_Building");
                return type?.GetMethod("BluePrintSet", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            static void Postfix(BP_Building __instance, BuildInfo info, Vector2 pos, int _att_num, int _func_num)
            {
                try
                {
                    // Get the list of all blueprints
                    var buildingMgrType = AccessTools.TypeByName("BuildingMgr");
                    var listField = buildingMgrType?.GetField("List_BP_BlueBuilding", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var buildingMgr = AccessTools.Property(typeof(GameMgr), "Instance")?.GetValue(null)?.GetType().GetField("_BuildingMgr", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(AccessTools.Property(typeof(GameMgr), "Instance")?.GetValue(null));
                    if (listField == null || buildingMgr == null) return;
                    var list = listField.GetValue(buildingMgr) as System.Collections.IList;
                    if (list == null) return;
                    // Find all blueprints of the same type at the same position
                    var toRemove = new List<object>();
                    int found = 0;
                    foreach (var bp in list)
                    {
                        if (bp == null || bp == __instance) continue;
                        var bpInfo = bp.GetType().GetField("m_Info")?.GetValue(bp);
                        var bpPos = (Vector2)bp.GetType().GetField("Pos_Tile")?.GetValue(bp);
                        var bpAttNum = (int)bp.GetType().GetField("m_AttNum")?.GetValue(bp);
                        var bpFuncNum = (int)bp.GetType().GetField("m_FuncNum")?.GetValue(bp);
                        if (bpInfo == info && bpPos == pos && bpAttNum == _att_num && bpFuncNum == _func_num)
                        {
                            found++;
                            if (found >= 1) // keep only the first
                                toRemove.Add(bp);
                        }
                    }
                    // Remove all but one
                    foreach (var bp in toRemove)
                    {
                        list.Remove(bp);
                        var go = (bp as UnityEngine.MonoBehaviour)?.gameObject;
                        if (go != null)
                            UnityEngine.Object.Destroy(go);
                    }
                }
                catch (Exception ex)
                {
                    BepInEx.Logging.Logger.CreateLogSource("Overseer").LogError($"Failed to deduplicate blueprints: {ex}");
                }
            }
        }

        // Patch BuildingMgr.GetCanUseMaterialNum to always return a large value when blueprint planning is enabled
        [HarmonyPatch]
        public static class Patch_BuildingMgr_GetCanUseMaterialNum
        {
            static MethodInfo TargetMethod()
            {
                var type = AccessTools.TypeByName("BuildingMgr");
                return type?.GetMethod("GetCanUseMaterialNum", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            static bool Prefix(ref int __result)
            {
                if (AllowBlueprintPlanning != null && AllowBlueprintPlanning.Value)
                {
                    __result = 99999;
                    return false; // Skip original
                }
                return true; // Run original
            }
        }
    }
}