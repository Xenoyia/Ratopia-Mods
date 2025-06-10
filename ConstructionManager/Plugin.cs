using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Reflection;
using BepInEx.Logging;

[BepInPlugin("constructionmanager", "Construction Manager", "1.0.0")]
public class ConstructionManagerPlugin : BaseUnityPlugin
{
    // Path for JSON file within the ConstructionManager plugin folder
    private static readonly string PluginFolder = Path.Combine(Paths.PluginPath, "ConstructionManager");
    private static readonly string JsonFilePath = Path.Combine(PluginFolder, "Buildings.json");
    private static readonly string ResourceListPath = Path.Combine(PluginFolder, "ResourceList.txt");
    private static bool resourceListWritten = false;

    public void Awake()
    {
        Logger.LogInfo("Construction Manager by Xenoyia loaded!");
        var harmony = new Harmony("constructionmanager");
        harmony.PatchAll();
    }

    /// <summary>
    /// Export or import Building_DB1 data to/from JSON file.
    /// </summary>
    public static void ExportImportBuildingDB1(Building_DB1 db, BepInEx.Logging.ManualLogSource logger)
    {
        try
        {
            if (!Directory.Exists(PluginFolder))
                Directory.CreateDirectory(PluginFolder);

            if (!File.Exists(JsonFilePath))
            {
                // Export only enabled buildings with Name and Material (as Recipe)
                var exportData = new BuildingsSerializable(db);
                File.WriteAllText(JsonFilePath, JsonConvert.SerializeObject(exportData, Formatting.Indented));
                logger.LogInfo($"Exported enabled buildings to {JsonFilePath}");
            }
            else
            {
                // Import and overwrite only enabled buildings with Name and Material (from Recipe)
                var json = File.ReadAllText(JsonFilePath);
                var importData = JsonConvert.DeserializeObject<BuildingsSerializable>(json);
                importData.ApplyTo(db);
                logger.LogInfo($"Imported enabled buildings from {JsonFilePath}");
            }
        }
        catch (System.Exception e)
        {
            logger.LogError($"Error handling Buildings.json: {e.Message}");
        }
    }

    // User-friendly serializable class for enabled buildings
    [System.Serializable]
    public class BuildingsSerializable
    {
        public List<BuildingEntry> Buildings = new List<BuildingEntry>();

        public BuildingsSerializable() { }
        public BuildingsSerializable(Building_DB1 db)
        {
            foreach (var sheet in db.sheets)
            {
                foreach (var param in sheet.list)
                {
                    if (param.Enable != 0)
                    {
                        Buildings.Add(new BuildingEntry
                        {
                            Name = param.Name,
                            Recipe = param.Material
                        });
                    }
                }
            }
        }

        public void ApplyTo(Building_DB1 db)
        {
            // Only update Name/Material for enabled buildings that match by Name
            foreach (var sheet in db.sheets)
            {
                foreach (var param in sheet.list)
                {
                    if (param.Enable == 0)
                        continue;
                    var match = Buildings.FirstOrDefault(b => b.Name == param.Name);
                    if (match != null)
                    {
                        param.Material = match.Recipe;
                    }
                }
            }
        }
    }

    [System.Serializable]
    public class BuildingEntry
    {
        public string Name;
        public string Recipe;
    }

    [HarmonyPatch(typeof(DB_Mgr), "Build_DB_Setting")]
    public class DB_Mgr_Build_DB_Setting_Patch
    {
        static void Prefix(DB_Mgr __instance)
        {
            var db = __instance.m_Building_DB1;
            if (db == null)
            {
                ConstructionManagerPlugin.LogStaticError("DB_Mgr.m_Building_DB1 is null in Harmony patch!");
                return;
            }
            ConstructionManagerPlugin.ExportImportBuildingDB1(db, ConstructionManagerPlugin.LogSource);
        }
    }

    [HarmonyPatch(typeof(DB_Mgr), "Res_DB_Setting")]
    public class DB_Mgr_Res_DB_Setting_Patch
    {
        static void Prefix(DB_Mgr __instance)
        {
            if (resourceListWritten) return;
            var resDb = __instance.m_Res_DB1;
            if (resDb == null)
            {
                ConstructionManagerPlugin.LogStaticError("DB_Mgr.m_Res_DB1 is null in Harmony patch!");
                return;
            }
            try
            {
                var names = new List<string>();
                foreach (var sheet in resDb.sheets)
                {
                    foreach (var param in sheet.list)
                    {
                        if (param.Enable != 0 && !string.IsNullOrEmpty(param.Name))
                            names.Add(param.Name);
                    }
                }
                names.Sort();
                if (!Directory.Exists(PluginFolder))
                    Directory.CreateDirectory(PluginFolder);
                File.WriteAllLines(ResourceListPath, names);
                resourceListWritten = true;
                LogSource?.LogInfo($"Wrote resource list to {ResourceListPath}");
            }
            catch (System.Exception e)
            {
                LogSource?.LogError($"Error writing ResourceList.txt: {e.Message}");
            }
        }
    }

    // Static logger for use in Harmony patch
    internal static ManualLogSource LogSource;
    internal static void LogStaticError(string msg)
    {
        if (LogSource != null)
            LogSource.LogError(msg);
    }
    
    void OnEnable()
    {
        LogSource = Logger;
    }
}
