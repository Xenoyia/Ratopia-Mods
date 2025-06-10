using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine.UI;
using UnityEngine;
using CasselGames.UI;
using System.Linq;
using System.Collections;
using BepInEx.Configuration;
using System.Reflection;
using CasselGames.Data;
using CasselGames.Input;
using Spine.Unity;
using Spine;
using System;
using System.Globalization;

[BepInPlugin("bettertraits", "Better Traits", "1.0.8")]
public class BetterTraitsPlugin : BaseUnityPlugin
{
    public static ConfigFile TraitConfig;
    public static Dictionary<string, ConfigEntry<bool>> TraitEnabled = new();
    public static Dictionary<string, ConfigEntry<string>> TraitName = new();
    public static Dictionary<string, ConfigEntry<string>> TraitTName = new();
    public static Dictionary<string, ConfigEntry<string>> TraitIcon = new();
    public static Dictionary<string, ConfigEntry<string>> TraitDescription = new();
    public static Dictionary<string, ConfigEntry<string>> EffectValue = new();
    // Special fields for custom logic traits
    public static ConfigEntry<float> Pollinator_GrowthRate;
    public static ConfigEntry<float> Pollinator_Radius;
    public static ConfigEntry<float> Pollinator_Interval;
    public static ConfigEntry<string> Pollinator_PlantTypes;
    public static ConfigEntry<float> Spiritual_Happiness;
    public static ConfigEntry<float> Spiritual_Duration;
    public static ConfigEntry<float> Socialite_Happiness;
    public static ConfigEntry<float> Socialite_Radius;
    public static ConfigEntry<float> Socialite_Interval;
    public static ConfigEntry<float> Socialite_DurationInHours;
    public static ConfigEntry<int> Chef_ExtraProductCount;
    public static ConfigEntry<int> Smith_ExtraProductCount;
    public static ConfigEntry<float> TableEater_Duration;
    public static ConfigEntry<float> TableEater_Happiness;

    private static MethodInfo NpcAlarmCallMethod;
    private static Type AlarmStateType, GameMgrType, NpcAlarmUIType;
    private static object AlarmStateBasic;
    private static PropertyInfo GameMgrInstanceProp;
    private static FieldInfo NpcAlarmUIField;

    // Hardcoded vanilla trait index -> T_Name mapping
    public static readonly Dictionary<int, string> VanillaTraitNames = new Dictionary<int, string>
    {
        {0, "Hardy"},
        {1, "Fragile"},
        {2, "Athletic"},
        {3, "Inactive"},
        {4, "Intelligent"},
        {5, "Foolish"},
        {6, "Quick"},
        {7, "Slow"},
        {8, "Small Appetite"},
        {9, "Big Appetite"},
        {10, "Frugal"},
        {11, "Extravagant"},
        {12, "Quiet"},
        {13, "Playful"},
        {14, "Neat"},
        {15, "Slob"},
        {16, "Skilful"},
        {17, "Clumsy"},
        {104, "Elitist"},
        {105, "Cooperationist"},
        {106, "Pacifist"},
        {107, "Militant"},
        {108, "Conservative"},
        {109, "Progressive"},
        {110, "Anarchist"},
        {111, "Royalist"},
        {112, "Monetarist"},
        {113, "Floorer"},
        {115, "Hypertension"},
        {116, "Crier"},
        {117, "Optimistic"},
        {118, "Pessimistic"}
    };

    public static Dictionary<int, CharacterInfo> VanillaTraitData = new();

    public static BetterTraitsPlugin Instance;

    void Awake()
    {
        Instance = this;
        // Glorious reflection
        GameMgrType = AccessTools.TypeByName("GameMgr");
        NpcAlarmUIType = AccessTools.TypeByName("NpcAlarmUI");
        GameMgrInstanceProp = GameMgrType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        NpcAlarmUIField = GameMgrType.GetField("_NpcAlarmUI", BindingFlags.Instance | BindingFlags.Public);
        AlarmStateType = NpcAlarmUIType.GetNestedType("AlarmState");
        NpcAlarmCallMethod = NpcAlarmUIType.GetMethod("NpcAlarm_Call", new[] { typeof(string), typeof(bool), AlarmStateType, typeof(int) });
        AlarmStateBasic = Enum.Parse(AlarmStateType, "Basic");
        // Ensure the BetterTraits directory exists
        Directory.CreateDirectory(Path.Combine(Paths.PluginPath, "BetterTraits"));
        TraitConfig = new ConfigFile(Path.Combine(Paths.PluginPath, "BetterTraits", "Config.cfg"), true);
        SetupTraitConfig();
        Harmony.CreateAndPatchAll(typeof(BetterTraitsPlugin));
    }

    public void NPCAlarm(string message) {
        var gameMgr = GameMgrInstanceProp.GetValue(null);
        var npcAlarmUI = NpcAlarmUIField.GetValue(gameMgr);
        NpcAlarmCallMethod.Invoke(npcAlarmUI, new object[] { message, false, AlarmStateBasic, 0 });
    }

    private void SetupTraitConfig()
    {
        var allTraits = BuiltinTraits.AllTraits.Values.ToList();
        // Debug: Log all trait abilities and values
        foreach (var trait in allTraits)
        {
            string abilities = trait.List_Ability != null ? string.Join(",", trait.List_Ability) : "null";
            string values = trait.List_AbilityValue != null ? string.Join(",", trait.List_AbilityValue) : "null";
        }
        foreach (var trait in allTraits)
        {
            string section = trait.Name;
            string enabledDesc = $"Enable the {trait.Name} trait";
            if (VanillaTraitNames.TryGetValue(trait.Index, out var vanillaTName) && vanillaTName != trait.T_Name)
            {
                enabledDesc += $" [{vanillaTName} will be re-enabled if this is disabled!";
            }
            string safeName = trait.Name.Replace("#", "{HASH}");
            string safeDesc = trait.Description.Replace("#", "{HASH}");
            TraitEnabled[trait.Name] = TraitConfig.Bind(section, "Enabled", true, enabledDesc);
            TraitIcon[trait.Name] = TraitConfig.Bind(section, "Icon", trait.Icon, $"Icon for this trait");
            TraitName[trait.Name] = TraitConfig.Bind(section, "Name", safeName, $"Internal unique name for this trait");
            TraitTName[trait.Name] = TraitConfig.Bind(section, "T_Name", trait.T_Name, $"Displayed name for this trait");
            TraitDescription[trait.Name] = TraitConfig.Bind(section, "Description", safeDesc, $"Description for this trait");
            if (trait.Name != "Table Eater" && trait.Name != "Efficient Smith" && trait.Name != "Master Chef")
                EffectValue[trait.Name] = TraitConfig.Bind(
                    section,
                    "EffectValue",
                    trait.List_AbilityValue != null && trait.List_AbilityValue.Count > 0
                        ? string.Join(",", trait.List_AbilityValue.Select(v => v.ToString(CultureInfo.InvariantCulture)))
                        : "",
                    $"Ability value(s) for {trait.Name}, comma-separated");
            if (trait.Name == "Master Chef")
                Chef_ExtraProductCount = TraitConfig.Bind(section, "ExtraProductCount", 1, "Extra product count for Chef");
            if (trait.Name == "Efficient Smith")
                Smith_ExtraProductCount = TraitConfig.Bind(section, "ExtraProductCount", 1, "Extra product count for Efficient Smith");
        }
        // Special fields for custom logic traits
        Pollinator_GrowthRate = TraitConfig.Bind("Pollinator", "GrowthRate", 0.5f, "Growth rate for Pollinator");
        Pollinator_Radius = TraitConfig.Bind("Pollinator", "Radius", 10f, "Radius for Pollinator");
        Pollinator_Interval = TraitConfig.Bind("Pollinator", "Interval", 20f, "Interval for Pollinator");
        Pollinator_PlantTypes = TraitConfig.Bind("Pollinator", "PlantTypes", "FlowerPlant,GrainPlant,BerryBushPlant,TreePlant,GrassPlant,JewelTreePlant,SeaweedPlant,CoralTreePlant,LightGrassPlant,SeaanemonePlant,CactusPlant,PapyrusPlant,ThornTreePlant,JungleTreePlant,InfectionPlant,AshTreePlant,ExplosiveGrassPlant,PurifyingPlant", "Plant types for Pollinator");
        Spiritual_Happiness = TraitConfig.Bind("Spiritual", "Happiness", 5f, "Happiness bonus for Spiritual");
        Spiritual_Duration = TraitConfig.Bind("Spiritual", "Duration", 24f, "Buff duration (hours) for Spiritual");
        Socialite_Happiness = TraitConfig.Bind("Socialite", "Happiness", 3f, "Happiness bonus for Socialite");
        Socialite_Radius = TraitConfig.Bind("Socialite", "Radius", 5f, "Aura radius for Socialite");
        Socialite_Interval = TraitConfig.Bind("Socialite", "Interval", 10f, "Aura interval for Socialite");
        Socialite_DurationInHours = TraitConfig.Bind("Socialite", "DurationInHours", 4f, "Buff duration (hours) for Socialite");
        TableEater_Duration = TraitConfig.Bind("Table Eater", "BuffDurationHours", 8f, "Buff duration (hours) for Table Eater trait");
        TableEater_Happiness = TraitConfig.Bind("Table Eater", "Happiness", 10f, "Happiness bonus for Table Eater (applies to EffectValue_A)");
    }

    // Track which trait indexes were patched
    static HashSet<int> PatchedTraitIndexes = new HashSet<int>();

    // Track which category 1 trait indexes and names were patched
    static Dictionary<int, string> CustomCategory1TraitIndexes = new Dictionary<int, string>();

    // Store all custom traits loaded from Traits.json
    static List<CharacterInfo> AllCustomTraits = new List<CharacterInfo>();

    // Unified set of all overridden trait indexes (category 0 and 1)
    static HashSet<int> OverriddenTraitIndexes => new HashSet<int>(PatchedTraitIndexes.Union(CustomCategory1TraitIndexes.Keys));

    // Helper to apply config overrides to a CharacterInfo
    static void ApplyConfigOverrides(CharacterInfo trait)
    {
        if (trait == null) return;
        string configName = trait.Name; // Always use the original name for lookups

        // Only apply overrides if the trait is enabled in the config
        if (BetterTraitsPlugin.TraitEnabled.TryGetValue(configName, out var enabled) && !enabled.Value)
            return;

        if (BetterTraitsPlugin.TraitName.TryGetValue(configName, out var nameEntry))
            trait.Name = nameEntry.Value.Replace("{HASH}", "#");
        if (BetterTraitsPlugin.TraitTName.TryGetValue(configName, out var tnameEntry))
            trait.T_Name = tnameEntry.Value;
        if (BetterTraitsPlugin.TraitIcon.TryGetValue(configName, out var iconEntry))
            trait.Icon = iconEntry.Value;
        if (BetterTraitsPlugin.TraitDescription.TryGetValue(configName, out var descEntry))
            trait.Description = descEntry.Value.Replace("{HASH}", "#");
        // EffectValue config override for List_AbilityValue
        if (BetterTraitsPlugin.EffectValue.TryGetValue(configName, out var effectValueEntry) && !string.IsNullOrWhiteSpace(effectValueEntry.Value))
        {
            var valueStrs = effectValueEntry.Value.Split(',');
            var newValues = new List<float>();
            foreach (var s in valueStrs)
            {
                if (float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    newValues.Add(v);
            }
            if (newValues.Count > 0)
                trait.List_AbilityValue = newValues;
        }
    }

    [HarmonyPatch(typeof(DB_Mgr), "Character_DB_Setting")]
    [HarmonyPostfix]
    static void PatchTraits(DB_Mgr __instance)
    {
        try
        {
            // Store vanilla trait data before any modification, only if not already stored
            if (VanillaTraitData.Count == 0)
            {
                foreach (var trait in __instance.m_CharacterDB.List_Char1_DB)
                {
                    VanillaTraitData[trait.Index] = CloneTrait(trait);
                }
                foreach (var trait in __instance.m_CharacterDB.List_Char2_DB)
                {
                    VanillaTraitData[trait.Index] = CloneTrait(trait);
                }
            }
            var allTraits = BuiltinTraits.AllTraits.Values.ToList();
            var builtinTraitIndexes = new HashSet<int>(allTraits.Select(t => t.Index));
            // Apply config overrides only to traits in BuiltinTraits
            foreach (var trait in allTraits)
                ApplyConfigOverrides(trait);
            AllCustomTraits = allTraits; // Store for later use
            var replacementTraits = new Dictionary<int, CharacterInfo>();
            foreach (var trait in allTraits)
            {
                if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
                    continue; // Skip disabled traits
                if (trait.Category == 0)
                {
                    replacementTraits[trait.Index] = trait;
                    PatchedTraitIndexes.Add(trait.Index);
                }
                if (trait.Category == 1)
                {
                    CustomCategory1TraitIndexes[trait.Index] = trait.Name;
                }
            }
            int replaced = 0;
            var list = __instance.m_CharacterDB.List_Char1_DB;
            for (int i = 0; i < list.Count; i++)
            {
                var trait = list[i];
                // Only modify if index is in BuiltinTraits and trait is enabled
                if (trait.Category == 0 && replacementTraits.TryGetValue(trait.Index, out var newTrait))
                {
                    ApplyConfigOverrides(newTrait);
                    trait.Name = newTrait.Name;
                    trait.Icon = newTrait.Icon;
                    trait.T_Name = newTrait.T_Name;
                    trait.EffectValue_A = newTrait.EffectValue_A;
                    trait.EffectValue_B = newTrait.EffectValue_B;
                    trait.Description = newTrait.Description;
                    trait.List_Ability = new List<Res_Ability>(newTrait.List_Ability);
                    trait.List_AbilityValue = new List<float>(newTrait.List_AbilityValue);
                    replaced++;
                }
            }
            var list2 = __instance.m_CharacterDB.List_Char2_DB;
            foreach (var trait in allTraits)
            {
                if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
                    continue; // Skip disabled traits
                if (trait.Category == 1)
                {
                    for (int i = 0; i < list2.Count; i++)
                    {
                        var trait2 = list2[i];
                        if (trait2.Index == trait.Index)
                        {
                            ApplyConfigOverrides(trait);
                            trait2.Name = trait.Name;
                            trait2.Icon = trait.Icon;
                            trait2.T_Name = trait.T_Name;
                            trait2.EffectValue_A = trait.EffectValue_A;
                            trait2.EffectValue_B = trait.EffectValue_B;
                            trait2.Description = trait.Description;
                            trait2.List_Ability = new List<Res_Ability>(trait.List_Ability);
                            trait2.List_AbilityValue = new List<float>(trait.List_AbilityValue);
                            replaced++;
                        }
                    }
                }
            }
            Debug.Log($"[BetterTraits] Replaced {replaced} traits in Character DB.");
            // Add new Category 0 traits
            foreach (var trait in allTraits)
            {
                if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
                    continue; // Skip disabled traits
                if (trait.Category == 0)
                {
                    bool exists = list.Any(t => t.Index == trait.Index);
                    if (!exists)
                    {
                        ApplyConfigOverrides(trait);
                        list.Add(CloneTrait(trait));
                        Debug.Log($"[BetterTraits] Added new Category 0 trait: {trait.Name} (Index {trait.Index})");
                    }
                }
            }
            // Add new Category 1 traits
            foreach (var trait in allTraits)
            {
                if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
                    continue; // Skip disabled traits
                if (trait.Category == 1)
                {
                    bool exists = list2.Any(t => t.Index == trait.Index);
                    if (!exists)
                    {
                        ApplyConfigOverrides(trait);
                        list2.Add(CloneTrait(trait));
                        Debug.Log($"[BetterTraits] Added new Category 1 trait: {trait.Name} (Index {trait.Index})");
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BetterTraits] Exception: {ex}");
        }
    }

    // Unified method for applying size trait to any list of CharacterInfo and a transform
    static void ApplySizeTraitToTraits(List<CharacterInfo> traits, Transform spineTransform)
    {
        if (traits == null || spineTransform == null)
            return;
        float sizeUpValue = 0f;
        foreach (var trait in traits)
        {
            if (trait?.List_Ability != null)
            {
                for (int j = 0; j < trait.List_Ability.Count; j++)
                {
                    if (trait.List_Ability[j] == Res_Ability.OQ_SizeUp || (int)trait.List_Ability[j] == 512)
                    {
                        sizeUpValue += trait.List_AbilityValue[j];
                    }
                }
            }
        }
        if (Mathf.Abs(sizeUpValue) > 0.01f)
        {
            float scale = 1f + sizeUpValue;
            spineTransform.localScale = new Vector3(scale, scale, 1f);
        }
        else
        {
            spineTransform.localScale = Vector3.one;
        }
    }

    // In-game citizen scaling
    static void ApplySizeTrait(T_Citizen __instance)
    {
        if (__instance == null || __instance.m_Spine == null)
            return;
        ApplySizeTraitToTraits(__instance.List_CharInfoValue, __instance.m_Spine.transform);
    }

    [HarmonyPatch(typeof(T_Citizen), "LoadSetting")]
    [HarmonyPostfix]
    static void ApplySizeTraitOnInit(T_Citizen __instance)
    {
        ApplySizeTrait(__instance);
        ApplyCustomTraitBuffs(__instance);
        HandleDisabledTraits(__instance);
    }

    static void ApplyCustomTraitBuffs(T_Citizen __instance)
    {
        // Apply custom trait buffs and ensure custom logic traits are initialized
        if (__instance?.List_CharInfo != null)
        {
            foreach (var idx in __instance.List_CharInfo)
            {
                var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(idx);
                if (trait == null) continue;
                if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
                    continue;
                // Always ensure custom logic trait MonoBehaviours are present if the trait is present
                if (trait.Name == "Pollinator")
                {
                    if (__instance.GetComponent<PollinatorBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<PollinatorBehaviour>().Init(__instance);
                    }
                }
                if (trait.Name == "Socialite")
                {
                    if (__instance.GetComponent<SocialiteBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<SocialiteBehaviour>().Init(__instance);
                    }
                }
                if (trait.Name == "Master Chef")
                {
                    if (__instance.GetComponent<ChefBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<ChefBehaviour>().Init(__instance);
                    }
                }
                if (trait.Name == "Spiritual")
                {
                    if (__instance.m_Buff.IsExist(C_Buff.DarkReligion) || __instance.m_Buff.IsExist(C_Buff.SunReligion))
                    {
                        __instance.m_Buff.BuffRefSet(
                            C_Buff.HappyUp,
                            BetterTraitsPlugin.TraitName[trait.Name].Value.Replace("{HASH}", "#"),
                            C_Buff_Category.Character,
                            BetterTraitsPlugin.Spiritual_Happiness.Value,
                            (int)BetterTraitsPlugin.Spiritual_Duration.Value
                        );
                    }
                }
                if (trait.Name == "Efficient Smith")
                {
                    if (__instance.GetComponent<SmithBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<SmithBehaviour>().Init(__instance);
                    }
                }
            }
        }
        // Apply buffs for all traits with abilities (category 0 and 1)
        if (__instance?.List_CharInfo != null)
        {
            foreach (var idx in __instance.List_CharInfo)
            {
                var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(idx);
                // Remove any lingering vanilla buff for this trait index, but only if the custom trait overwriting it is enabled
                if (trait != null && VanillaTraitNames.TryGetValue(trait.Index, out var vanillaName))
                {
                    // Find if there is a custom trait with this index and it is enabled
                    var customTrait = BuiltinTraits.AllTraits.Values.FirstOrDefault(t => t.Index == trait.Index && t.Name != vanillaName);
                    if (customTrait != null && BetterTraitsPlugin.TraitEnabled.TryGetValue(customTrait.Name, out var customEnabled) && customEnabled.Value)
                    {
                        Debug.Log($"[BetterTraits] Removing lingering vanilla buff: {vanillaName} for overwritten trait index {trait.Index} (custom trait {customTrait.Name} is enabled)");
                        __instance.m_Buff.RefKill(vanillaName);
                    }
                }
                // Optionally, also remove any buff with an empty reference name (defensive)
                __instance.m_Buff.RefKill("");
                if (trait != null && trait.List_Ability != null && trait.List_Ability.Count > 0)
                {
                    if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
                        continue;
                    ApplyAllTraitBuffs(__instance, trait);
                }
            }
        }
    }

    [HarmonyPatch(typeof(T_Citizen), "LoadCitizen")]
    [HarmonyPostfix]
    static void ApplySizeTraitOnLoad(T_Citizen __instance)
    {
        HandleDisabledTraits(__instance);
        ApplySizeTrait(__instance);
        ApplyCustomTraitBuffs(__instance);
    }

    [HarmonyPatch(typeof(T_Citizen), "CharInit")]
    [HarmonyPostfix]
    static void Postfix_CharInit(T_Citizen __instance)
    {
        HandleDisabledTraits(__instance);
        ApplySizeTrait(__instance);
        ApplyCustomTraitBuffs(__instance);
        // Apply custom trait buffs
        if (__instance?.List_CharInfo != null)
        {
            foreach (var idx in __instance.List_CharInfo)
            {
                var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(idx);
                if (trait == null) continue;
                if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
                    continue;
                if (CustomCategory1TraitIndexes.ContainsKey(idx))
                {
                    __instance.Update_CharAbility(idx, true);
                }
                // Pollinator
                if (trait.Name == "Pollinator")
                {
                    if (__instance.GetComponent<PollinatorBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<PollinatorBehaviour>().Init(__instance);
                    }
                }
                // Socialite
                if (trait.Name == "Socialite")
                {
                    if (__instance.GetComponent<SocialiteBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<SocialiteBehaviour>().Init(__instance);
                    }
                }
                // Master Chef
                if (trait.Name == "Master Chef")
                {
                    if (__instance.GetComponent<ChefBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<ChefBehaviour>().Init(__instance);
                    }
                }
                // Spiritual
                if (trait.Name == "Spiritual")
                {
                    if (__instance.m_Buff.IsExist(C_Buff.DarkReligion) || __instance.m_Buff.IsExist(C_Buff.SunReligion))
                    {
                        __instance.m_Buff.BuffRefSet(
                            C_Buff.HappyUp,
                            BetterTraitsPlugin.TraitName[trait.Name].Value.Replace("{HASH}", "#"),
                            C_Buff_Category.Character,
                            BetterTraitsPlugin.Spiritual_Happiness.Value,
                            (int)BetterTraitsPlugin.Spiritual_Duration.Value
                        );
                    }
                }
                // Efficient Smith
                if (trait.Name == "Efficient Smith")
                {
                    if (__instance.GetComponent<SmithBehaviour>() == null)
                    {
                        __instance.gameObject.AddComponent<SmithBehaviour>().Init(__instance);
                    }
                }
            }
        }
    }

    // Preview slot scaling
    [HarmonyPatch(typeof(CC_CitizenSlot), "SlotSet", typeof(int), typeof(CCMake_Info))]
    [HarmonyPostfix]
    static void Postfix_CC_CitizenSlot_SlotSet(CC_CitizenSlot __instance, int _group_num, CCMake_Info _info)
    {
        if (__instance == null || _info == null || __instance.Img_CharIcon == null)
            return;
        for (int i = 0; i < _info.List_CharInfo.Count && i < __instance.Img_CharIcon.Length; i++)
        {
            var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(_info.List_CharInfo[i]);
            if (trait != null && (PatchedTraitIndexes.Contains(trait.Index) || CustomCategory1TraitIndexes.ContainsKey(trait.Index)) && !string.IsNullOrEmpty(trait.Icon) && trait.Icon.ToLower().Contains("gamescene"))
            {
                var customSprite = Func.Instance.LoadSprite(trait.Icon);
                if (customSprite != null)
                {
                    __instance.Img_CharIcon[i].sprite = customSprite;
                }
            }
        }
        // Restore size trait logic for preview
        var traits = new List<CharacterInfo>();
        foreach (var traitIndex in _info.List_CharInfo)
        {
            var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(traitIndex);
            if (trait != null)
                traits.Add(trait);
        }
        if (__instance.m_Spine != null)
            ApplySizeTraitToTraits(traits, __instance.m_Spine.transform);
    }
    
    [HarmonyPatch(typeof(AbilityStatusCitizenSlotUI), "SetData", typeof(T_Citizen), typeof(int))]
    [HarmonyPostfix]
    static void Postfix_AbilityStatusCitizenSlotUI_SetData(AbilityStatusCitizenSlotUI __instance, T_Citizen citizen, int index)
    {
        if (citizen == null || GameMgr.Instance?._DB_Mgr == null)
            return;
        var dB_Mgr = GameMgr.Instance._DB_Mgr;
        CharacterInfo characterInfo = null;
        if (citizen.m_UnitKind == UnitKind.GBot)
            characterInfo = dB_Mgr.GetRatronCharacterInfo(citizen.List_CharInfo[index]);
        else
            characterInfo = dB_Mgr.GetCharacterInfo(citizen.List_CharInfo[index]);
        if (characterInfo != null && (PatchedTraitIndexes.Contains(characterInfo.Index) || CustomCategory1TraitIndexes.ContainsKey(characterInfo.Index)) && !string.IsNullOrEmpty(characterInfo.Icon) && characterInfo.Icon.ToLower().Contains("gamescene"))
        {
            var customSprite = Func.Instance.LoadSprite(characterInfo.Icon);
            if (customSprite != null)
            {
                var iconField = typeof(AbilityStatusCitizenSlotUI).GetField("_icon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (iconField != null)
                {
                    var iconImage = iconField.GetValue(__instance) as UnityEngine.UI.Image;
                    if (iconImage != null)
                        iconImage.sprite = customSprite;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Char_Tooltip), "CharInfoSet", typeof(CharacterInfo))]
    [HarmonyPostfix]
    static void Postfix_Char_Tooltip_CharInfoSet(Char_Tooltip __instance, CharacterInfo _info)
    {
        if (_info != null && !string.IsNullOrEmpty(_info.Icon) && _info.Icon.ToLower().Contains("gamescene"))
        {
            var customSprite = Func.Instance.LoadSprite(_info.Icon);
            if (customSprite != null)
            {
                __instance.Img.sprite = customSprite;
            }
        }
    }

    [HarmonyPatch(typeof(Char_Tooltip), "UnionCharInfoSet", typeof(CharacterInfo))]
    [HarmonyPostfix]
    static void Postfix_Char_Tooltip_UnionCharInfoSet(Char_Tooltip __instance, CharacterInfo _info)
    {
        if (_info != null && !string.IsNullOrEmpty(_info.Icon) && _info.Icon.ToLower().Contains("gamescene"))
        {
            var customSprite = Func.Instance.LoadSprite(_info.Icon);
            if (customSprite != null)
            {
                __instance.Img.sprite = customSprite;
            }
        }
    }

    // Helper to apply all trait buffs for a trait to a citizen
    static void ApplyAllTraitBuffs(T_Citizen citizen, CharacterInfo trait)
    {
        if (trait?.List_Ability == null) return;
        if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
            return;
        string refName = trait.Name;
        if (BetterTraitsPlugin.TraitName.TryGetValue(trait.Name, out var nameEntry) && !string.IsNullOrEmpty(nameEntry.Value) && nameEntry.Value != trait.Name)
            refName = nameEntry.Value.Replace("{HASH}", "#");

        citizen.m_Buff.RefKill(refName);
        for (int i = 0; i < trait.List_Ability.Count; i++)
        {
            var ability = trait.List_Ability[i];
            float value = (trait.List_AbilityValue != null && i < trait.List_AbilityValue.Count) ? trait.List_AbilityValue[i] : trait.EffectValue_A;
            float useDuration = trait.EffectValue_B;
            if (useDuration == 0) useDuration = -999;

            // Divide by 100 for certain abilities, just like vanilla
            if (ability == Res_Ability.SLP || ability == Res_Ability.FUN || ability == Res_Ability.CLN ||
                ability == Res_Ability.SPD || ability == Res_Ability.HUG || ability == Res_Ability.LIF ||
                ability == Res_Ability.PDT || ability == Res_Ability.EXP)
            {
                if (Mathf.Abs(value) >= 1.0f)
                    value /= 100f;
                // else, use as-is (already a fraction)
            }

            var buff = CitizenBuff.ResToBuff(ability, value);
            if (buff != C_Buff.None)
            {
                citizen.m_Buff.BuffRefSet(buff, refName, C_Buff_Category.Character, value, (int)useDuration);
            }
        }
    }

    [HarmonyPatch(typeof(T_Citizen), "Update_CharAbility")]
    [HarmonyPrefix]
    static bool Prefix_Update_CharAbility(T_Citizen __instance, int _index, bool _check)
    {
        // Only handle category 1 traits for explicit buff application
        if (CustomCategory1TraitIndexes.ContainsKey(_index) && __instance.List_CharInfo.Contains(_index))
        {
            if (_check)
            {
                var info = GameMgr.Instance._DB_Mgr.GetCharacterInfo(_index);
                if (info == null) return false;
                __instance.m_Buff.RefKill(info.Name);
                ApplyAllTraitBuffs(__instance, info);
            }
            // Always skip original for custom category 1 traits
            return false;
        }
        return true; // Let original run for all others
    }

    // Patch StatusCitizenInfoNoticeUI.SetData(BuffInfo) to replace with custom trait icon if needed
    [HarmonyPatch(typeof(CasselGames.UI.StatusCitizenInfoNoticeUI), "SetData", new System.Type[] { typeof(BuffInfo) })]
    [HarmonyPostfix]
    static void Postfix_StatusCitizenInfoNoticeUI_SetData_BuffInfo(object __instance, BuffInfo info)
    {
        if (!string.IsNullOrEmpty(info.ReferenceName) && 
            (CustomCategory1TraitIndexes.ContainsValue(info.ReferenceName) || 
             PatchedTraitIndexes.Any(idx => GameMgr.Instance._DB_Mgr.GetCharacterInfo(idx)?.Name == info.ReferenceName)))
        {
            var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(info.ReferenceName);
            if (trait != null && !string.IsNullOrEmpty(trait.Icon))
            {
                var customSprite = Func.Instance.LoadSprite(trait.Icon);
                if (customSprite != null)
                {
                    var iconField = __instance.GetType().GetField("_icon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (iconField != null)
                    {
                        var iconImage = iconField.GetValue(__instance) as UnityEngine.UI.Image;
                        if (iconImage != null)
                            iconImage.sprite = customSprite;
                    }
                }
            }
        }
    }

    // Patch StatusCitizenInfoNoticeUI.SetData(CitizenBuff.RefInfo) to replace with custom trait icon if needed
    [HarmonyPatch(typeof(CasselGames.UI.StatusCitizenInfoNoticeUI), "SetData", new System.Type[] { typeof(CitizenBuff.RefInfo) })]
    [HarmonyPostfix]
    static void Postfix_StatusCitizenInfoNoticeUI_SetData_RefInfo(object __instance, CitizenBuff.RefInfo info)
    {
        if (!string.IsNullOrEmpty(info.RefName) && 
            (CustomCategory1TraitIndexes.ContainsValue(info.RefName) || 
             PatchedTraitIndexes.Any(idx => GameMgr.Instance._DB_Mgr.GetCharacterInfo(idx)?.Name == info.RefName)))
        {
            var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(info.RefName);
            if (trait != null && !string.IsNullOrEmpty(trait.Icon))
            {
                var customSprite = Func.Instance.LoadSprite(trait.Icon);
                if (customSprite != null)
                {
                    var iconField = __instance.GetType().GetField("_icon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (iconField != null)
                    {
                        var iconImage = iconField.GetValue(__instance) as UnityEngine.UI.Image;
                        if (iconImage != null)
                            iconImage.sprite = customSprite;
                    }
                }
            }
        }
    }

    // Helper to clone a CharacterInfo for safe DB insertion
    static CharacterInfo CloneTrait(CharacterInfo trait)
    {
        return new CharacterInfo {
            Index = trait.Index,
            Name = trait.Name,
            Icon = trait.Icon,
            T_Name = trait.T_Name,
            EffectValue_A = trait.EffectValue_A,
            EffectValue_B = trait.EffectValue_B,
            Description = trait.Description,
            Category = trait.Category,
            List_Ability = trait.List_Ability != null ? new List<Res_Ability>(trait.List_Ability) : null,
            List_AbilityValue = trait.List_AbilityValue != null ? new List<float>(trait.List_AbilityValue) : null
        };
    }

    // Patch StatusCitizenInfoDetailLayoutUI.SetData(BuffInfo[] arr) to set correct custom trait icon
    [HarmonyPatch(typeof(CasselGames.UI.StatusCitizenInfoDetailLayoutUI), "SetData", new System.Type[] { typeof(BuffInfo[]) })]
    [HarmonyPrefix]
    static bool Prefix_StatusCitizenInfoDetailLayoutUI_SetData_BuffInfo(CasselGames.UI.StatusCitizenInfoDetailLayoutUI __instance, BuffInfo[] arr)
    {
        // Use reflection to get _slotUI and _scrollRect
        var slotUIField = typeof(CasselGames.UI.StatusCitizenInfoDetailLayoutUI).GetField("_slotUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var scrollRectField = typeof(CasselGames.UI.StatusCitizenInfoDetailLayoutUI).GetField("_scrollRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var layoutUIField = typeof(CasselGames.UI.StatusCitizenInfoDetailLayoutUI).GetField("_layoutUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var slotUI = slotUIField?.GetValue(__instance) as CasselGames.UI.StatusCitizenInfoSlotUI;
        var scrollRect = scrollRectField?.GetValue(__instance) as UnityEngine.UI.ScrollRect;
        var layoutUI = layoutUIField?.GetValue(__instance);
        if (slotUI == null || scrollRect == null || layoutUI == null) return true; // fallback to original

        // Properly reset/hide all slots before clearing
        var nowIndexProp = layoutUI.GetType().GetProperty("NowIndex");
        nowIndexProp?.SetValue(layoutUI, 0);
        var totalCountProp = layoutUI.GetType().GetProperty("TotalCount");
        int totalCount = totalCountProp != null ? (int)totalCountProp.GetValue(layoutUI) : 0;
        var indexer = layoutUI.GetType().GetProperty("Item");
        for (int i = 0; i < totalCount; i++)
        {
            var btn = indexer?.GetValue(layoutUI, new object[] { i }) as UnityEngine.Component;
            btn?.GetComponent<CasselGames.UI.StatusCitizenInfoSlotUI>()?.Hide();
        }
        var clearMethod = layoutUI.GetType().GetMethod("Clear");
        clearMethod?.Invoke(layoutUI, null);

        for (int i = 0; i < arr.Length; i++)
        {
            var slot = UIUtility.CreateOrGet(slotUI, scrollRect.content, i, $"UI@Slot_{i}");
            // Try to find a custom trait by ReferenceName
            Sprite customSprite = null;
            if (!string.IsNullOrEmpty(arr[i].ReferenceName))
            {
                var trait = AllCustomTraits.FirstOrDefault(t => t.Name == arr[i].ReferenceName);
                if (trait != null && !string.IsNullOrEmpty(trait.Icon))
                {
                    customSprite = Func.Instance.LoadSprite(trait.Icon);
                }
            }
            slot.SetData(customSprite ?? arr[i].GetIcon());
            slot.Show();
            // Add to layout
            var addMethod = layoutUI.GetType().GetMethod("Add");
            addMethod?.Invoke(layoutUI, new object[] { slot.GetComponent<Utility.UI.LayoutButtonUI>() });
        }
        return false; // skip original
    }

    // Patch StatusCitizenInfoDetailLayoutUI.SetData(CitizenBuff.RefInfo[] arr) to set correct custom trait icon
    [HarmonyPatch(typeof(CasselGames.UI.StatusCitizenInfoDetailLayoutUI), "SetData", new System.Type[] { typeof(CitizenBuff.RefInfo[]) })]
    [HarmonyPrefix]
    static bool Prefix_StatusCitizenInfoDetailLayoutUI_SetData_RefInfo(CasselGames.UI.StatusCitizenInfoDetailLayoutUI __instance, CitizenBuff.RefInfo[] arr)
    {
        // Use reflection to get _slotUI and _scrollRect
        var slotUIField = typeof(CasselGames.UI.StatusCitizenInfoDetailLayoutUI).GetField("_slotUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var scrollRectField = typeof(CasselGames.UI.StatusCitizenInfoDetailLayoutUI).GetField("_scrollRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var layoutUIField = typeof(CasselGames.UI.StatusCitizenInfoDetailLayoutUI).GetField("_layoutUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var slotUI = slotUIField?.GetValue(__instance) as CasselGames.UI.StatusCitizenInfoSlotUI;
        var scrollRect = scrollRectField?.GetValue(__instance) as UnityEngine.UI.ScrollRect;
        var layoutUI = layoutUIField?.GetValue(__instance);
        if (slotUI == null || scrollRect == null || layoutUI == null) return true; // fallback to original

        // Properly reset/hide all slots before clearing
        var nowIndexProp = layoutUI.GetType().GetProperty("NowIndex");
        nowIndexProp?.SetValue(layoutUI, 0);
        var totalCountProp = layoutUI.GetType().GetProperty("TotalCount");
        int totalCount = totalCountProp != null ? (int)totalCountProp.GetValue(layoutUI) : 0;
        var indexer = layoutUI.GetType().GetProperty("Item");
        for (int i = 0; i < totalCount; i++)
        {
            var btn = indexer?.GetValue(layoutUI, new object[] { i }) as UnityEngine.Component;
            btn?.GetComponent<CasselGames.UI.StatusCitizenInfoSlotUI>()?.Hide();
        }
        var clearMethod = layoutUI.GetType().GetMethod("Clear");
        clearMethod?.Invoke(layoutUI, null);

        for (int i = 0; i < arr.Length; i++)
        {
            var slot = UIUtility.CreateOrGet(slotUI, scrollRect.content, i, $"UI@Slot_{i}");
            // Try to find a custom trait by RefName
            Sprite customSprite = null;
            if (!string.IsNullOrEmpty(arr[i].RefName))
            {
                var trait = AllCustomTraits.FirstOrDefault(t => t.Name == arr[i].RefName);
                if (trait != null && !string.IsNullOrEmpty(trait.Icon))
                {
                    customSprite = Func.Instance.LoadSprite(trait.Icon);
                }
            }
            slot.SetData(customSprite ?? arr[i].GetIcon());
            slot.Show();
            // Add to layout
            var addMethod = layoutUI.GetType().GetMethod("Add");
            addMethod?.Invoke(layoutUI, new object[] { slot.GetComponent<Utility.UI.LayoutButtonUI>() });
        }
        return false; // skip original
    }

    // Patch StatusCitizenInfoContentsLayoutUI.SetData(BuffInfo[] arr) to set correct custom trait icon
    [HarmonyPatch(typeof(CasselGames.UI.StatusCitizenInfoContentsLayoutUI), "SetData", new System.Type[] { typeof(BuffInfo[]) })]
    [HarmonyPrefix]
    static bool Prefix_StatusCitizenInfoContentsLayoutUI_SetData_BuffInfo(CasselGames.UI.StatusCitizenInfoContentsLayoutUI __instance, BuffInfo[] arr)
    {
        // Use reflection to get _slotUI, _contents, and _list
        var slotUIField = typeof(CasselGames.UI.StatusCitizenInfoContentsLayoutUI).GetField("_slotUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var contentsField = typeof(CasselGames.UI.StatusCitizenInfoContentsLayoutUI).GetField("_contents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var listField = typeof(CasselGames.UI.StatusCitizenInfoContentsLayoutUI).GetField("_list", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var slotUI = slotUIField?.GetValue(__instance) as CasselGames.UI.StatusCitizenInfoSlotUI;
        var contents = contentsField?.GetValue(__instance) as UnityEngine.Transform;
        var list = listField?.GetValue(__instance) as List<CasselGames.UI.StatusCitizenInfoSlotUI>;
        if (slotUI == null || contents == null || list == null) return true; // fallback to original

        // Clear/hide all slots
        for (int i = 0; i < list.Count; i++)
            list[i].Hide();

        if (arr == null)
            return false;

        for (int i = 0; i < arr.Length; i++)
        {
            var slot = UIUtility.CreateOrGet(slotUI, contents, list, $"UI@Slot_{i}");
            slot.SetIgnore(isIgnore: true);
            slot.Show();
            if (i >= 6)
            {
                slot.SetOver();
                break;
            }
            // Try to find a custom trait by ReferenceName
            Sprite customSprite = null;
            if (!string.IsNullOrEmpty(arr[i].ReferenceName))
            {
                var trait = AllCustomTraits.FirstOrDefault(t => t.Name == arr[i].ReferenceName);
                if (trait != null && !string.IsNullOrEmpty(trait.Icon))
                {
                    customSprite = Func.Instance.LoadSprite(trait.Icon);
                }
            }
            slot.SetData(customSprite ?? arr[i].GetIcon());
        }
        return false; // skip original
    }

    // Patch StatusCitizenInfoContentsLayoutUI.SetData(CitizenBuff.RefInfo[] arr) to set correct custom trait icon
    [HarmonyPatch(typeof(CasselGames.UI.StatusCitizenInfoContentsLayoutUI), "SetData", new System.Type[] { typeof(CitizenBuff.RefInfo[]) })]
    [HarmonyPrefix]
    static bool Prefix_StatusCitizenInfoContentsLayoutUI_SetData_RefInfo(CasselGames.UI.StatusCitizenInfoContentsLayoutUI __instance, CitizenBuff.RefInfo[] arr)
    {
        // Use reflection to get _slotUI, _contents, and _list
        var slotUIField = typeof(CasselGames.UI.StatusCitizenInfoContentsLayoutUI).GetField("_slotUI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var contentsField = typeof(CasselGames.UI.StatusCitizenInfoContentsLayoutUI).GetField("_contents", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var listField = typeof(CasselGames.UI.StatusCitizenInfoContentsLayoutUI).GetField("_list", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var slotUI = slotUIField?.GetValue(__instance) as CasselGames.UI.StatusCitizenInfoSlotUI;
        var contents = contentsField?.GetValue(__instance) as UnityEngine.Transform;
        var list = listField?.GetValue(__instance) as List<CasselGames.UI.StatusCitizenInfoSlotUI>;
        if (slotUI == null || contents == null || list == null) return true; // fallback to original

        // Clear/hide all slots
        for (int i = 0; i < list.Count; i++)
            list[i].Hide();

        if (arr == null)
            return false;

        for (int i = 0; i < arr.Length; i++)
        {
            var slot = UIUtility.CreateOrGet(slotUI, contents, list, $"UI@Slot_{i}");
            slot.SetIgnore(isIgnore: true);
            slot.Show();
            if (i >= 6)
            {
                slot.SetOver();
                break;
            }
            // Try to find a custom trait by RefName
            Sprite customSprite = null;
            if (!string.IsNullOrEmpty(arr[i].RefName))
            {
                var trait = AllCustomTraits.FirstOrDefault(t => t.Name == arr[i].RefName);
                if (trait != null && !string.IsNullOrEmpty(trait.Icon))
                {
                    customSprite = Func.Instance.LoadSprite(trait.Icon);
                }
            }
            slot.SetData(customSprite ?? arr[i].GetIcon());
        }
        return false; // skip original
    }

    [HarmonyPatch(typeof(StorageUI_MatSlot), "CharacterInfoSet")]
    [HarmonyPostfix]
    static void Postfix_StorageUI_MatSlot_CharacterInfoSet(StorageUI_MatSlot __instance, CharacterInfo _info)
    {
        if (_info != null && !string.IsNullOrEmpty(_info.Icon) && _info.Icon.ToLower().Contains("gamescene"))
        {
            var customSprite = Func.Instance.LoadSprite(_info.Icon);
            if (customSprite != null)
            {
                __instance.Img_Icon.sprite = customSprite;
            }
        }
    }

    static void HandleDisabledTraits(T_Citizen citizen)
    {
        if (citizen?.List_CharInfo == null) {
            return;
        }
        var dbMgr = GameMgr.Instance?._DB_Mgr;
        if (dbMgr == null) {
            return;
        }
        var allTraits = dbMgr.m_CharacterDB.List_Char1_DB.Concat(dbMgr.m_CharacterDB.List_Char2_DB).ToList();

        // Remove buffs whose ReferenceName matches a trait that is disabled in the config
        int beforeBuffCount = citizen.m_Buff.List_BuffIcon.Count;
        citizen.m_Buff.List_BuffIcon.RemoveAll(buff =>
            !string.IsNullOrEmpty(buff.ReferenceName)
            && BetterTraitsPlugin.TraitEnabled.TryGetValue(buff.ReferenceName, out var enabled)
            && enabled != null && !enabled.Value);
        int afterBuffCount = citizen.m_Buff.List_BuffIcon.Count;
        citizen.m_Buff.Dic_Buff_Refresh();

        // Iterate backwards to avoid index shifting
        for (int i = citizen.List_CharInfo.Count - 1; i >= 0; i--)
        {
            int idx = citizen.List_CharInfo[i];
            var trait = dbMgr.GetCharacterInfo(idx);
            if (trait == null)
            {
                // Determine category by slot index: 0 = category 0, 1 = category 1
                int cat = (i == 0) ? 0 : 1;
                var enabledTraits = allTraits.Where(t => t != null && t.Category == cat && TraitEnabled.TryGetValue(t.Name, out var en) && en.Value).ToList();
                if (enabledTraits.Count > 0)
                {
                    var newTrait = enabledTraits[UnityEngine.Random.Range(0, enabledTraits.Count)];
                    citizen.List_CharInfo[i] = newTrait.Index;
                    citizen.List_CharInfoValue[i] = newTrait;
                    // Remove any lingering buffs from the replaced vanilla trait
                    if (VanillaTraitNames.TryGetValue(newTrait.Index, out var vanillaName))
                    {
                        citizen.m_Buff.RefKill(vanillaName);
                    }
                    ApplyAllTraitBuffs(citizen, newTrait);
                }
                else
                {
                    Debug.Log($"[BetterTraits] No enabled traits found for category {cat}, clearing slot at index {i}.");
                    citizen.List_CharInfoValue[i] = null;
                }
                continue;
            }
            if (BetterTraitsPlugin.TraitEnabled.TryGetValue(trait.Name, out var enabled) && !enabled.Value)
            {
                // Remove all buffs with the old trait's name or config reference name
                string oldRefName = trait.Name;
                if (BetterTraitsPlugin.TraitName.TryGetValue(trait.Name, out var nameEntry) && !string.IsNullOrEmpty(nameEntry.Value))
                    oldRefName = nameEntry.Value.Replace("{HASH}", "#");
                citizen.m_Buff.RefKill(trait.Name);
                if (oldRefName != trait.Name)
                    citizen.m_Buff.RefKill(oldRefName);
                // Remove buffs by ability name (for vanilla/ability-based buffs)
                if (trait.List_Ability != null)
                {
                    foreach (var ability in trait.List_Ability)
                    {
                        citizen.m_Buff.RefKill(ability.ToString());
                    }
                }
                // Remove any lingering vanilla buff for this trait index, but only if the custom trait overwriting it is enabled
                if (VanillaTraitNames.TryGetValue(trait.Index, out var vanillaName))
                {
                    // Find if there is a custom trait with this index and it is enabled
                    var customTrait = BuiltinTraits.AllTraits.Values.FirstOrDefault(t => t.Index == trait.Index && t.Name != vanillaName);
                    if (customTrait != null && BetterTraitsPlugin.TraitEnabled.TryGetValue(customTrait.Name, out var customEnabled) && customEnabled.Value)
                    {
                        citizen.m_Buff.RefKill(vanillaName);
                    }
                }
                // Optionally, also remove any buff with an empty reference name (defensive)
                citizen.m_Buff.RefKill("");

                // Defensive: Remove any buff with empty T_Name (should never be empty unless orphaned)
                var buffsToRemove = citizen.m_Buff.List_BuffIcon.Where(b => string.IsNullOrEmpty(b.T_Name)).ToList();
                foreach (var buff in buffsToRemove)
                {
                    citizen.m_Buff.List_BuffIcon.Remove(buff);
                }

                int cat = trait.Category;
                // Find enabled traits of same category (excluding the current trait)
                var enabledTraits = allTraits.Where(t => t != null && t.Category == cat && t.Name != trait.Name && TraitEnabled.TryGetValue(t.Name, out var en) && en.Value).ToList();
                if (enabledTraits.Count > 0)
                {
                    var newTrait = enabledTraits[UnityEngine.Random.Range(0, enabledTraits.Count)];
                    citizen.List_CharInfo[i] = newTrait.Index;
                    citizen.List_CharInfoValue[i] = newTrait;
                    ApplyAllTraitBuffs(citizen, newTrait);
                }
                else if (VanillaTraitData.TryGetValue(trait.Index, out var vanillaTrait))
                {
                    citizen.List_CharInfoValue[i] = CloneTrait(vanillaTrait);
                    ApplyAllTraitBuffs(citizen, citizen.List_CharInfoValue[i]);
                }
                else
                {
                    citizen.List_CharInfoValue[i] = null;
                }
            }
        }
    }

    [HarmonyPatch(typeof(BatchR_CharInfo), "SlotSet", new Type[] { typeof(int), typeof(UnitKind) })]
    [HarmonyPostfix]
    static void Postfix_BatchR_CharInfo_SlotSet(BatchR_CharInfo __instance, int _char_index, UnitKind _kind)
    {
        CharacterInfo trait = null;
        if (_kind == UnitKind.GBot)
            trait = GameMgr.Instance._DB_Mgr.GetRatronCharacterInfo(_char_index);
        else
            trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(_char_index);
        if (trait != null && BetterTraitsPlugin.TraitIcon.TryGetValue(trait.Name, out var iconEntry) && !string.IsNullOrEmpty(iconEntry.Value))
        {
            var customSprite = Func.Instance.LoadSprite(iconEntry.Value);
            if (customSprite != null)
            {
                var iconField = typeof(BatchR_CharInfo).GetField("Img_Icon", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (iconField != null)
                {
                    var iconImage = iconField.GetValue(__instance) as UnityEngine.UI.Image;
                    if (iconImage != null)
                        iconImage.sprite = customSprite;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ToiletInfo), "SellService")]
    [HarmonyPostfix]
    static void Postfix_ToiletInfo_SellService(ToiletInfo __instance, T_Citizen _unit, bool _value)
    {
        if (__instance.m_Building != null && __instance.m_Building.m_Info.Name == BuildingName.Table && _unit != null && _unit.List_CharInfo != null)
        {
            var dbMgr = GameMgr.Instance?._DB_Mgr;
            if (dbMgr != null)
            {
                foreach (var idx in _unit.List_CharInfo)
                {
                    var trait = dbMgr.GetCharacterInfo(idx);
                    if (trait != null && trait.Name == "Table Eater")
                    {
                        float value = BetterTraitsPlugin.TableEater_Happiness.Value;
                        float duration = BetterTraitsPlugin.TableEater_Duration.Value;
                        _unit.m_Buff.BuffRefSet(C_Buff.HappyUp, trait.Name, C_Buff_Category.Character, value, (int)duration);
                        break;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(T_UnitMgr), "ApplyReligion")]
    [HarmonyPostfix]
    static void Postfix_T_UnitMgr_ApplyReligion(Religion _type, T_Citizen _unit)
    {
        if (_unit == null || _unit.List_CharInfo == null) return;
        var dbMgr = GameMgr.Instance?._DB_Mgr;
        if (dbMgr == null) return;
        foreach (var idx in _unit.List_CharInfo)
        {
            var trait = dbMgr.GetCharacterInfo(idx);
            if (trait != null && trait.Name == "Spiritual")
            {
                // Only apply if in a religion
                if (_unit.m_Religion != Religion.None)
                {
                    _unit.m_Buff.RefKill("Spiritual");
                    _unit.m_Buff.BuffRefSet(
                        C_Buff.HappyUp,
                        "Spiritual",
                        C_Buff_Category.Character,
                        BetterTraitsPlugin.Spiritual_Happiness.Value,
                        (int)BetterTraitsPlugin.Spiritual_Duration.Value
                    );
                }
                else
                {
                    // Remove the buff if not in a religion
                    _unit.m_Buff.RefKill("Spiritual");
                }
            }
        }
    }

    [HarmonyPatch(typeof(T_Citizen), "Update")]
    [HarmonyPostfix]
    static void Postfix_T_Citizen_Update(T_Citizen __instance)
    {
        if (__instance == null || __instance.List_CharInfo == null) return;
        var dbMgr = GameMgr.Instance?._DB_Mgr;
        if (dbMgr == null) return;
        bool hasSpiritual = false;
        foreach (var idx in __instance.List_CharInfo)
        {
            var trait = dbMgr.GetCharacterInfo(idx);
            if (trait != null && trait.Name == "Spiritual")
            {
                hasSpiritual = true;
                break;
            }
        }
        if (hasSpiritual)
        {
            if (__instance.m_Religion != Religion.None)
            {
                // Ensure the buff is present
                __instance.m_Buff.RefKill("Spiritual");
                __instance.m_Buff.BuffRefSet(
                    C_Buff.HappyUp,
                    "Spiritual",
                    C_Buff_Category.Character,
                    BetterTraitsPlugin.Spiritual_Happiness.Value,
                    (int)BetterTraitsPlugin.Spiritual_Duration.Value
                );
            }
            else
            {
                // Remove the buff if not in a religion
                if (__instance.m_Buff.IsExistRef("Spiritual"))
                {
                    __instance.m_Buff.RefKill("Spiritual");
                }
            }
        }
    }

    public static void ReloadConfigAndReapply()
    {
        TraitConfig.Reload();
        Instance.SetupTraitConfig();
        PatchTraits(GameMgr.Instance._DB_Mgr);
        foreach (var citizen in GameMgr.Instance._T_UnitMgr.List_Citizen)
        {
            HandleDisabledTraits(citizen);
            ApplyCustomTraitBuffs(citizen);
            ApplySizeTrait(citizen);
        }
        Instance.NPCAlarm("Better Traits config reloaded and all citizens updated!");
    }
}

public class PollinatorBehaviour : MonoBehaviour
{
    private T_Citizen citizen;
    private Coroutine pollinateRoutine;

    public void Init(T_Citizen citizen)
    {
        this.citizen = citizen;
        pollinateRoutine = StartCoroutine(PollinateRoutine());
    }

    private IEnumerator PollinateRoutine()
    {
        while (true)
        {
            float interval = BetterTraitsPlugin.Pollinator_Interval.Value;
            yield return new WaitForSeconds(interval);
            if (citizen == null) yield break;
            PollinateNearbyPlants();
        }
    }

    private void PollinateNearbyPlants()
    {
        float radius = BetterTraitsPlugin.Pollinator_Radius.Value;
        float growthRate = BetterTraitsPlugin.Pollinator_GrowthRate.Value;
        var plantTypes = new HashSet<TileType>();
        foreach (var s in BetterTraitsPlugin.Pollinator_PlantTypes.Value.Split(','))
        {
            if (System.Enum.TryParse<TileType>(s.Trim(), out var t))
                plantTypes.Add(t);
        }
        var center = citizen.m_CurNode.GetPos();
        foreach (var offset in GetOffsetsInRadius(radius))
        {
            var node = GameMgr.Instance._TileMgr.GetNodeByLimit(center + offset);
            if (node == null) continue;
            var t_grass = node.m_WorldObj;
            if (t_grass != null && plantTypes.Contains(t_grass.m_Info.NameType) && t_grass.m_Level <= 2 && t_grass.m_Unit == null && t_grass.m_GrowBuffEffect == null)
            {
                t_grass.AddBuff("PlantCare", growthRate);
                if (t_grass.m_GrowBuffEffect == null)
                {
                    GameMgr.Instance._PoolMgr.Pool_GrowBuffEffect.GetNextObj().GetComponent<GrowBuffEffect>().GrowBuffEffectSet(t_grass);
                }
            }
        }
    }

    private IEnumerable<Vector2> GetOffsetsInRadius(float radius)
    {
        int r = Mathf.CeilToInt(radius);
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                if (dx * dx + dy * dy <= radius * radius)
                    yield return new Vector2(dx, dy);
            }
        }
    }
}

public class SocialiteBehaviour : MonoBehaviour
{
    private T_Citizen citizen;
    private float lastAuraTime;

    public void Init(T_Citizen citizen)
    {
        this.citizen = citizen;
        lastAuraTime = Time.time;
    }

    void Update()
    {
        if (citizen == null) return;
        float auraInterval = BetterTraitsPlugin.Socialite_Interval.Value;
        if (Time.time - lastAuraTime >= auraInterval)
        {
            ApplyHappinessAura();
            lastAuraTime = Time.time;
        }
    }

    private void ApplyHappinessAura()
    {
        float auraRadius = BetterTraitsPlugin.Socialite_Radius.Value;
        float happinessBonus = BetterTraitsPlugin.Socialite_Happiness.Value;
        float durationInHours = BetterTraitsPlugin.Socialite_DurationInHours.Value;
        var center = citizen.m_CurNode.GetPos();
        bool foundAny = false;
        foreach (var offset in GetOffsetsInRadius(auraRadius))
        {
            var node = GameMgr.Instance._TileMgr.GetNodeByLimit(center + offset);
            if (node?.m_WorldObj?.m_Unit?.GetComponent<T_Citizen>() != null)
            {
                var nearbyCitizen = node.m_WorldObj.m_Unit.GetComponent<T_Citizen>();
                if (nearbyCitizen != citizen)
                {
                    if (!nearbyCitizen.m_Buff.IsExistRef("Socialite"))
                    {
                        nearbyCitizen.m_Buff.BuffRefSet(
                            C_Buff.HappyUp,
                            "Socialite",
                            C_Buff_Category.Character,
                            happinessBonus,
                            (int)durationInHours
                        );
                        foundAny = true;
                    }
                }
            }
        }
        if (!foundAny)
        {
            foreach (var otherCitizen in GameMgr.Instance._T_UnitMgr.List_Citizen)
            {
                if (otherCitizen != null && otherCitizen != citizen)
                {
                    float dist = Vector3.Distance(otherCitizen.transform.position, center);
                    if (dist <= auraRadius)
                    {
                        if (!otherCitizen.m_Buff.IsExistRef("Socialite"))
                        {
                            otherCitizen.m_Buff.BuffRefSet(
                                C_Buff.HappyUp,
                                "Socialite",
                                C_Buff_Category.Character,
                                happinessBonus,
                                (int)durationInHours
                            );
                        }
                    }
                }
            }
        }
    }

    private IEnumerable<Vector2> GetOffsetsInRadius(float radius)
    {
        int r = Mathf.CeilToInt(radius);
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                if (dx * dx + dy * dy <= radius * radius)
                    yield return new Vector2(dx, dy);
            }
        }
    }
}

public class ChefBehaviour : MonoBehaviour
{
    private T_Citizen citizen;
    private Dictionary<Building, float> lastWorkTimes = new Dictionary<Building, float>();

    public void Init(T_Citizen citizen)
    {
        this.citizen = citizen;
    }

    void Update()
    {
        if (citizen == null) return;
        if (citizen.m_Job != null && IsCookingWork(citizen.m_Job))
        {
            var workplace = citizen.m_Job;
            if (workplace != null)
            {
                MonitorCookingProgress(workplace);
            }
        }
    }

    private bool IsCookingWork(Building job)
    {
        if (job?.m_Info?.Name != null)
        {
            var workplaceName = job.m_Info.Name;
            return workplaceName == BuildingName.Grill || workplaceName == BuildingName.Bakery;
        }
        return false;
    }

    private void MonitorCookingProgress(Building workplace)
    {
        if (workplace.m_BuildInfoUI is MasonryInfo masonryInfo)
        {
            if (!lastWorkTimes.ContainsKey(workplace))
            {
                lastWorkTimes[workplace] = masonryInfo.m_CurTime;
                return;
            }
            float previousTime = lastWorkTimes[workplace];
            float currentTime = masonryInfo.m_CurTime;
            if (previousTime > 0 && currentTime <= 0.1f)
            {
                ApplyChefBonus(workplace);
            }
            lastWorkTimes[workplace] = currentTime;
        }
    }

    private void ApplyChefBonus(Building workplace)
    {
        if (workplace.m_BuildInfoUI is MasonryInfo masonryInfo && workplace.m_Info?.List_Effect3 != null && workplace.m_Info.List_Effect3.Count > 0)
        {
            // Get the current product based on the production progress
            int productIndex = masonryInfo.GetProductIndex();
            if (productIndex >= workplace.m_Info.List_Effect3.Count)
                productIndex = workplace.m_Info.List_Effect3.Count - 1;
            
            var currentProduct = workplace.m_Info.List_Effect3[productIndex];
            var position = workplace.transform.position;
            int extraProductCount = BetterTraitsPlugin.Chef_ExtraProductCount.Value;
            for (int i = 0; i < extraProductCount; i++)
            {
                var spawned = SpawnItem(currentProduct, position, 1);
            }
        }
    }

    public static object SpawnItem(object type, Vector3 position, int quantity = 1, object state = null)
    {
        if (GameMgr.Instance == null || GameMgr.Instance._PoolMgr == null)
            throw new System.Exception("GameMgr or PoolMgr not initialized!");
        if (state == null)
        {
            state = TObjState.MakeProduct;
        }
        var obj = GameMgr.Instance._PoolMgr.Pool_TileObject.GetNextObj();
        var tileObj = obj.GetComponent<TileObject>();
        if (tileObj == null)
            throw new System.Exception("TileObject component missing from pooled object!");
        tileObj.ObjectInit((TileType)type, (TObjState)state, position, quantity, false);
        return tileObj;
    }
}

public class SmithBehaviour : MonoBehaviour
{
    private T_Citizen citizen;
    private Dictionary<Building, float> lastWorkTimes = new Dictionary<Building, float>();

    public void Init(T_Citizen citizen)
    {
        this.citizen = citizen;
    }

    void Update()
    {
        if (citizen == null) return;
        if (citizen.m_Job != null && IsSmithingWork(citizen.m_Job))
        {
            var workplace = citizen.m_Job;
            if (workplace != null)
            {
                MonitorSmithingProgress(workplace);
            }
        }
    }

    private bool IsSmithingWork(Building job)
    {
        if (job?.m_Info?.Name != null)
        {
            var workplaceName = job.m_Info.Name;
            return workplaceName == BuildingName.Blacksmith || workplaceName == BuildingName.Furnace;
        }
        return false;
    }

    private void MonitorSmithingProgress(Building workplace)
    {
        if (workplace.m_BuildInfoUI is MasonryInfo masonryInfo)
        {
            if (!lastWorkTimes.ContainsKey(workplace))
            {
                lastWorkTimes[workplace] = masonryInfo.m_CurTime;
                return;
            }
            float previousTime = lastWorkTimes[workplace];
            float currentTime = masonryInfo.m_CurTime;
            if (previousTime > 0 && currentTime <= 0.1f)
            {
                ApplySmithBonus(workplace);
            }
            lastWorkTimes[workplace] = currentTime;
        }
    }

    private void ApplySmithBonus(Building workplace)
    {
        if (workplace.m_BuildInfoUI is MasonryInfo masonryInfo && workplace.m_Info?.List_Effect3 != null && workplace.m_Info.List_Effect3.Count > 0)
        {
            // Get the current product based on the production progress
            int productIndex = masonryInfo.GetProductIndex();
            if (productIndex >= workplace.m_Info.List_Effect3.Count)
                productIndex = workplace.m_Info.List_Effect3.Count - 1;
            
            var currentProduct = workplace.m_Info.List_Effect3[productIndex];
            var position = workplace.transform.position;
            int extraProductCount = BetterTraitsPlugin.Smith_ExtraProductCount.Value;
            for (int i = 0; i < extraProductCount; i++)
            {
                ChefBehaviour.SpawnItem(currentProduct, position, 1);
            }
        }
    }
} 