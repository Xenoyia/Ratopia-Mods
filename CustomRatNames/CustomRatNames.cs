using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using I2.Loc;
using System.Linq;
using BepInEx.Logging;
using System;
using CasselGames.UI;

namespace CustomRatNames
{
    [BepInPlugin("customratnames", "Custom Rat Names", "1.0.0")]
    public class CustomRatNamesPlugin : BaseUnityPlugin
    {
        public static string PluginDir => Path.Combine(Paths.PluginPath, "CustomRatNames");
        public static string MalePath => Path.Combine(PluginDir, "Male.txt");
        public static string FemalePath => Path.Combine(PluginDir, "Female.txt");
        public static string ConfigPath => Path.Combine(PluginDir, "Config.cfg");

        public static ConfigEntry<bool> DisableVanillaNames;
        public static ConfigEntry<bool> DebugMode;
        internal static ManualLogSource LogSource;

        private void Awake()
        {
            LogSource = Logger;
            Directory.CreateDirectory(PluginDir);
            if (!File.Exists(MalePath)) File.WriteAllText(MalePath, "");
            if (!File.Exists(FemalePath)) File.WriteAllText(FemalePath, "");
            var configFile = new ConfigFile(ConfigPath, true);
            DisableVanillaNames = configFile.Bind("General", "DisableVanillaNames", false, "If true, only custom names from Male.txt and Female.txt will be used.");
            DebugMode = configFile.Bind("General", "DebugMode", false, "If true, enables detailed logging for debugging purposes.");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo($"Custom Rat Names loaded. Files at {PluginDir}");
        }

        public static List<string> GetCustomNames(string gender)
        {
            string path = gender == "Male" ? MalePath : FemalePath;
            if (!File.Exists(path)) return new List<string>();
            var lines = File.ReadAllLines(path);
            var names = new List<string>();
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    names.Add(trimmed);
            }
            return names;
        }

        public static void AddName(string gender, string name)
        {
            string path = gender == "Male" ? MalePath : FemalePath;
            name = name.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var names = new HashSet<string>(File.ReadAllLines(path));
            if (!names.Contains(name))
            {
                File.AppendAllText(path, name + "\n");
            }
        }

        public static void LogDebug(string message)
        {
            if (DebugMode.Value)
            {
                LogSource.LogInfo($"[CustomRatNames] {message}");
            }
        }

        public static int GetLanguageSheetIndex()
        {
            string lang = LocalizationManager.CurrentLanguage;
            return lang switch
            {
                "Korean" => 0,
                "English" => 2,
                "Japanese" => 4,
                "Chinese_Simplified" => 6,
                "Chinese_Traditional" => 8,
                "French" => 10,
                "German" => 12,
                "Russian" => 14,
                "Ukrainian" => 16,
                "Brazilian_Portuguese" => 18,
                "Spanish" => 20,
                "Thai" => 22,
                "Vietnamese" => 24,
                _ => 2 // Default to English
            };
        }

        public static void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    [HarmonyPatch(typeof(DB_Mgr), "Name_DB_Setting")]
    public class DB_Mgr_Name_DB_Setting_Patch
    {
        static void Postfix(DB_Mgr __instance)
        {
            try
            {
                if (__instance.m_Name_DB1 == null)
                {
                    CustomRatNamesPlugin.LogSource.LogError("[CustomRatNames] DB_Mgr.m_Name_DB1 is null in Harmony patch!");
                    return;
                }

                CustomRatNamesPlugin.LogSource.LogInfo("[CustomRatNames] Adding custom names to database...");
                int i = CustomRatNamesPlugin.GetLanguageSheetIndex();
                string lang = LocalizationManager.CurrentLanguage;
                CustomRatNamesPlugin.LogDebug($"Current language: {lang}");
                
                int maleSheet = i;
                int femaleSheet = i + 1;

                CustomRatNamesPlugin.LogDebug($"Using sheets {maleSheet} for male and {femaleSheet} for female names");

                var maleNames = CustomRatNamesPlugin.GetCustomNames("Male");
                var femaleNames = CustomRatNamesPlugin.GetCustomNames("Female");

                CustomRatNamesPlugin.LogDebug($"Loaded {maleNames.Count} male and {femaleNames.Count} female names from files");

                // Log the first few names from our files to verify they're loaded correctly
                CustomRatNamesPlugin.LogDebug($"First few male names from file: {string.Join(", ", maleNames.Take(5))}");
                CustomRatNamesPlugin.LogDebug($"First few female names from file: {string.Join(", ", femaleNames.Take(5))}");

                // Log the current state of the database before modifications
                CustomRatNamesPlugin.LogDebug($"Before modification - Male names in DB: {__instance.m_Name_DB1.sheets[maleSheet].list.Count}");
                CustomRatNamesPlugin.LogDebug($"Before modification - Female names in DB: {__instance.m_Name_DB1.sheets[femaleSheet].list.Count}");

                // Optionally clear vanilla names if config is set
                if (CustomRatNamesPlugin.DisableVanillaNames.Value)
                {
                    __instance.m_Name_DB1.sheets[maleSheet].list.Clear();
                    __instance.m_Name_DB1.sheets[femaleSheet].list.Clear();
                    CustomRatNamesPlugin.LogDebug("Cleared vanilla names as per config");
                }

                int addedMale = 0;
                int addedFemale = 0;
                foreach (var name in maleNames)
                {
                    var trimmed = name.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    if (!__instance.m_Name_DB1.sheets[maleSheet].list.Any(p => p.Name == trimmed))
                    {
                        var param = new Name_DB1.Param { Name = trimmed };
                        __instance.m_Name_DB1.sheets[maleSheet].list.Add(param);
                        addedMale++;
                    }
                }
                foreach (var name in femaleNames)
                {
                    var trimmed = name.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    if (!__instance.m_Name_DB1.sheets[femaleSheet].list.Any(p => p.Name == trimmed))
                    {
                        var param = new Name_DB1.Param { Name = trimmed };
                        __instance.m_Name_DB1.sheets[femaleSheet].list.Add(param);
                        addedFemale++;
                    }
                }
                int totalMale = __instance.m_Name_DB1.sheets[maleSheet].list.Count;
                int totalFemale = __instance.m_Name_DB1.sheets[femaleSheet].list.Count;
                CustomRatNamesPlugin.LogSource.LogInfo($"[CustomRatNames] Added {addedMale} male and {addedFemale} female custom names. Total: {totalMale} male, {totalFemale} female.");

                // Log a few sample names from each sheet to verify
                var maleSample = __instance.m_Name_DB1.sheets[maleSheet].list.Take(5).Select(p => p.Name).ToList();
                var femaleSample = __instance.m_Name_DB1.sheets[femaleSheet].list.Take(5).Select(p => p.Name).ToList();
                CustomRatNamesPlugin.LogDebug($"Sample male names in DB: {string.Join(", ", maleSample)}");
                CustomRatNamesPlugin.LogDebug($"Sample female names in DB: {string.Join(", ", femaleSample)}");

                // Verify our custom names are actually in the database
                var customMaleInDb = maleNames.Take(5).All(name => __instance.m_Name_DB1.sheets[maleSheet].list.Any(p => p.Name == name));
                var customFemaleInDb = femaleNames.Take(5).All(name => __instance.m_Name_DB1.sheets[femaleSheet].list.Any(p => p.Name == name));
                CustomRatNamesPlugin.LogDebug($"First 5 custom male names in DB: {customMaleInDb}");
                CustomRatNamesPlugin.LogDebug($"First 5 custom female names in DB: {customFemaleInDb}");

                // Log all names in the database for debugging
                CustomRatNamesPlugin.LogDebug("All male names in DB:");
                foreach (var param in __instance.m_Name_DB1.sheets[maleSheet].list)
                {
                    CustomRatNamesPlugin.LogDebug($"  - {param.Name}");
                }
                CustomRatNamesPlugin.LogDebug("All female names in DB:");
                foreach (var param in __instance.m_Name_DB1.sheets[femaleSheet].list)
                {
                    CustomRatNamesPlugin.LogDebug($"  - {param.Name}");
                }
            }
            catch (Exception ex)
            {
                CustomRatNamesPlugin.LogSource.LogError($"[CustomRatNames] Error in Name_DB_Setting patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(DB_Mgr), "GetRandomName")]
    public class DB_Mgr_GetRandomName_Patch
    {
        static bool Prefix(DB_Mgr __instance, Gender _gender, ref string __result)
        {
            try
            {
                if (__instance.m_Name_DB1 == null)
                {
                    CustomRatNamesPlugin.LogSource.LogError("[CustomRatNames] DB_Mgr.m_Name_DB1 is null in GetRandomName patch!");
                    return true; // Let the original method handle it
                }

                int i = CustomRatNamesPlugin.GetLanguageSheetIndex();
                int sheet = _gender == Gender.Male ? i : i + 1;
                var list = __instance.m_Name_DB1.sheets[sheet].list;

                CustomRatNamesPlugin.LogDebug($"GetRandomName called for {_gender} (sheet {sheet})");
                CustomRatNamesPlugin.LogDebug($"Total names in sheet: {list.Count}");

                if (list.Count > 0)
                {
                    // Create a copy of the list to avoid modifying the original
                    var shuffledList = new List<Name_DB1.Param>(list);
                    CustomRatNamesPlugin.ShuffleList(shuffledList);
                    
                    __result = shuffledList[0].Name;
                    CustomRatNamesPlugin.LogDebug($"Selected name: {__result} (from shuffled list)");
                    return false; // Skip the original method
                }

                return true; // Let the original method handle it if we couldn't get a name
            }
            catch (Exception ex)
            {
                CustomRatNamesPlugin.LogSource.LogError($"[CustomRatNames] Error in GetRandomName patch: {ex}");
                return true; // Let the original method handle it
            }
        }
    }

    [HarmonyPatch(typeof(DB_Mgr), "GetRandomName_ByNoCheck")]
    public class DB_Mgr_GetRandomName_ByNoCheck_Patch
    {
        static bool Prefix(DB_Mgr __instance, Gender _gender, ref string __result)
        {
            try
            {
                if (__instance.m_Name_DB1 == null)
                {
                    CustomRatNamesPlugin.LogSource.LogError("[CustomRatNames] DB_Mgr.m_Name_DB1 is null in GetRandomName_ByNoCheck patch!");
                    return true; // Let the original method handle it
                }

                int i = CustomRatNamesPlugin.GetLanguageSheetIndex();
                int sheet = _gender == Gender.Male ? i : i + 1;
                var list = __instance.m_Name_DB1.sheets[sheet].list;

                CustomRatNamesPlugin.LogDebug($"GetRandomName_ByNoCheck called for {_gender} (sheet {sheet})");
                CustomRatNamesPlugin.LogDebug($"Total names in sheet: {list.Count}");

                if (list.Count > 0)
                {
                    // Create a copy of the list to avoid modifying the original
                    var shuffledList = new List<Name_DB1.Param>(list);
                    CustomRatNamesPlugin.ShuffleList(shuffledList);
                    
                    __result = shuffledList[0].Name;
                    CustomRatNamesPlugin.LogDebug($"Selected name: {__result} (from shuffled list)");
                    return false; // Skip the original method
                }

                return true; // Let the original method handle it if we couldn't get a name
            }
            catch (Exception ex)
            {
                CustomRatNamesPlugin.LogSource.LogError($"[CustomRatNames] Error in GetRandomName_ByNoCheck patch: {ex}");
                return true; // Let the original method handle it
            }
        }
    }

    [HarmonyPatch(typeof(CCMake_Info), MethodType.Constructor)]
    public class CCMake_Info_Constructor_Patch
    {
        static void Postfix(CCMake_Info __instance)
        {
            try
            {
                if (GameMgr.Instance._DB_Mgr == null || GameMgr.Instance._DB_Mgr.m_Name_DB1 == null)
                {
                    CustomRatNamesPlugin.LogSource.LogError("[CustomRatNames] DB_Mgr or m_Name_DB1 is null in CCMake_Info constructor patch!");
                    return;
                }

                int i = CustomRatNamesPlugin.GetLanguageSheetIndex();
                int sheet = __instance.m_Gender == Gender.Male ? i : i + 1;
                var list = GameMgr.Instance._DB_Mgr.m_Name_DB1.sheets[sheet].list;

                CustomRatNamesPlugin.LogDebug($"CCMake_Info constructor called for {__instance.m_Gender} (sheet {sheet})");
                CustomRatNamesPlugin.LogDebug($"Total names in sheet: {list.Count}");

                if (list.Count > 0)
                {
                    // Create a copy of the list to avoid modifying the original
                    var shuffledList = new List<Name_DB1.Param>(list);
                    CustomRatNamesPlugin.ShuffleList(shuffledList);
                    
                    __instance.Name = shuffledList[0].Name;
                    CustomRatNamesPlugin.LogDebug($"Selected name: {__instance.Name} (from shuffled list)");
                }
                else
                {
                    CustomRatNamesPlugin.LogSource.LogError("[CustomRatNames] No names available in sheet for CCMake_Info constructor patch!");
                }
            }
            catch (Exception ex)
            {
                CustomRatNamesPlugin.LogSource.LogError($"[CustomRatNames] Error in CCMake_Info constructor patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(T_UnitMgr), "MakeBabyRat")]
    public class T_UnitMgr_MakeBabyRat_Patch
    {
        static void Postfix(T_UnitMgr __instance, BabyRat __result)
        {
            try
            {
                if (__result == null || GameMgr.Instance._DB_Mgr == null)
                {
                    CustomRatNamesPlugin.LogSource.LogError("[CustomRatNames] BabyRat or DB_Mgr is null in MakeBabyRat patch!");
                    return;
                }

                // Get a random name using our existing DB_Mgr.GetRandomName patch
                __result.m_UnitName = GameMgr.Instance._DB_Mgr.GetRandomName(__result.m_Gender);
                __result.NameUpdate();
                CustomRatNamesPlugin.LogDebug($"MakeBabyRat called for {__result.m_Gender}, selected name: {__result.m_UnitName}");
            }
            catch (Exception ex)
            {
                CustomRatNamesPlugin.LogSource.LogError($"[CustomRatNames] Error in MakeBabyRat patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(CitizenCaveUI), "MakeCitizenList")]
    public class CitizenCaveUI_MakeCitizenList_Patch
    {
        static void Postfix(CitizenCaveUI __instance)
        {
            try
            {
                if (GameMgr.Instance._DB_Mgr == null || GameMgr.Instance._DB_Mgr.m_Name_DB1 == null)
                {
                    CustomRatNamesPlugin.LogSource.LogError("[CustomRatNames] DB_Mgr or m_Name_DB1 is null in CitizenCaveUI.MakeCitizenList patch!");
                    return;
                }

                // Get the list of citizens that were just created
                var _list1 = __instance.GetType().GetField("_list1", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(__instance) as List<CCMake_Info>[];
                if (_list1 == null || _list1.Length != 3)
                {
                    CustomRatNamesPlugin.LogSource.LogError($"[CustomRatNames] Could not find _list1 field in CitizenCaveUI or it has incorrect length! Expected array of 3 lists, got {(object)_list1?.Length ?? 0} lists.");
                    return;
                }

                CustomRatNamesPlugin.LogDebug($"Processing citizens in MakeCitizenList (found {_list1.Length} lists)");

                // Process each list in the array
                for (int listIndex = 0; listIndex < _list1.Length; listIndex++)
                {
                    var citizenList = _list1[listIndex];
                    if (citizenList == null)
                    {
                        CustomRatNamesPlugin.LogDebug($"List at index {listIndex} is null, skipping...");
                        continue;
                    }

                    CustomRatNamesPlugin.LogDebug($"Processing list {listIndex} with {citizenList.Count} citizens");

                    // Keep track of used names in this list
                    var usedNames = new HashSet<string>();

                    foreach (var citizen in citizenList)
                    {
                        if (citizen == null) continue;

                        int i = CustomRatNamesPlugin.GetLanguageSheetIndex();
                        int sheet = citizen.m_Gender == Gender.Male ? i : i + 1;
                        var list = GameMgr.Instance._DB_Mgr.m_Name_DB1.sheets[sheet].list;

                        if (list.Count > 0)
                        {
                            // Create a copy of the list to avoid modifying the original
                            var shuffledList = new List<Name_DB1.Param>(list);
                            CustomRatNamesPlugin.ShuffleList(shuffledList);
                            
                            // Find the first unused name
                            string selectedName = null;
                            foreach (var nameParam in shuffledList)
                            {
                                if (!usedNames.Contains(nameParam.Name))
                                {
                                    selectedName = nameParam.Name;
                                    break;
                                }
                            }

                            // If we couldn't find an unused name, use the first one and log a warning
                            if (selectedName == null)
                            {
                                selectedName = shuffledList[0].Name;
                                CustomRatNamesPlugin.LogSource.LogWarning($"[CustomRatNames] Could not find unique name for citizen in list {listIndex}! Reusing name: {selectedName}");
                            }

                            citizen.Name = selectedName;
                            usedNames.Add(selectedName);
                            CustomRatNamesPlugin.LogDebug($"Selected unique name for citizen in list {listIndex}: {citizen.Name} (from shuffled list)");
                        }
                        else
                        {
                            CustomRatNamesPlugin.LogSource.LogError($"[CustomRatNames] No names available in sheet for citizen in list {listIndex}!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CustomRatNamesPlugin.LogSource.LogError($"[CustomRatNames] Error in CitizenCaveUI.MakeCitizenList patch: {ex}");
            }
        }
    }
} 