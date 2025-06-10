using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Reflection;
using BepInEx.Logging;

[BepInPlugin("productionmanager", "Production Manager", "1.0.0")]
public class ProductionManagerPlugin : BaseUnityPlugin
{
    // Path for JSON files within the ProductionManager plugin folder
    private static readonly string PluginFolder = Path.Combine(Paths.PluginPath, "ProductionManager");
    private static readonly string ResourcesJsonPath = Path.Combine(PluginFolder, "Resources.json");
    private static readonly string BuildingsJsonPath = Path.Combine(PluginFolder, "Buildings.json");
    private static bool resourcesExported = false;
    private static bool buildingsExported = false;

    public void Awake()
    {
        Logger.LogInfo("Production Manager by Xenoyia loaded!");
        var harmony = new Harmony("productionmanager");
        harmony.PatchAll();
    }

    // Serializable class for resource export
    public class ResourceExport
    {
        public string Name;
        public string Recipe_A;
        public int Quantity_A;
        public int Workload_A;
        public string Recipe_B;
        public int Quantity_B;
        public int Workload_B;
        public string Recipe_C;
        public int Quantity_C;
        public int Workload_C;
    }

    // Serializable class for building export
    public class BuildingExport
    {
        public string Name;
        public string Products;
    }

    // Top-level resource JSON structure
    public class ResourcesJson
    {
        public List<ResourceExport> Resources;

        public void ApplyTo(Res_DB1 resDb)
        {
            // Only update enabled resources that match by Name
            foreach (var sheet in resDb.sheets)
            {
                foreach (var param in sheet.list)
                {
                    if (param.Enable == 0 || string.IsNullOrEmpty(param.Name))
                        continue;
                    var match = Resources.FirstOrDefault(r => r.Name == param.Name);
                    if (match != null)
                    {
                        param.Material_A = match.Recipe_A;
                        param.Product_A = match.Quantity_A;
                        param.BP_A = match.Workload_A;
                        param.Material_B = match.Recipe_B;
                        param.Product_B = match.Quantity_B;
                        param.BP_B = match.Workload_B;
                        param.Material_C = match.Recipe_C;
                        param.Product_C = match.Quantity_C;
                        param.BP_C = match.Workload_C;
                    }
                }
            }
        }
    }

    // Top-level building JSON structure
    public class BuildingsJson
    {
        public List<BuildingExport> Buildings;

        public void ApplyTo(Building_DB1 db)
        {
            // Only update enabled buildings that match by Name
            foreach (var sheet in db.sheets)
            {
                foreach (var param in sheet.list)
                {
                    if (param.Enable == 0)
                        continue;
                    var match = Buildings.FirstOrDefault(b => b.Name == param.Name);
                    if (match != null)
                    {
                        param.Effect_Value3 = match.Products;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(DB_Mgr), "Res_DB_Setting")]
    [HarmonyPriority(Priority.Last)]
    public class DB_Mgr_Res_DB_Setting_Patch
    {
        static void Prefix(DB_Mgr __instance)
        {
            var resDb = __instance.m_Res_DB1;
            if (resDb == null)
            {
                ProductionManagerPlugin.LogStaticError("DB_Mgr.m_Res_DB1 is null in Harmony patch!");
                return;
            }
            // Import from JSON if it exists
            if (File.Exists(ResourcesJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(ResourcesJsonPath);
                    var importData = JsonConvert.DeserializeObject<ResourcesJson>(json);
                    importData.ApplyTo(resDb);
                    LogSource?.LogInfo($"Imported resources from {ResourcesJsonPath}");
                }
                catch (System.Exception e)
                {
                    LogSource?.LogError($"Error importing Resources.json: {e.Message}");
                }
            }
        }

        static void Postfix(DB_Mgr __instance)
        {
            if (resourcesExported) return;
            var resDb = __instance.m_Res_DB1;
            if (resDb == null)
            {
                ProductionManagerPlugin.LogStaticError("DB_Mgr.m_Res_DB1 is null in Harmony patch!");
                return;
            }
            try
            {
                if (!Directory.Exists(PluginFolder))
                    Directory.CreateDirectory(PluginFolder);

                var resources = new List<ResourceExport>();
                foreach (var sheet in resDb.sheets)
                {
                    foreach (var param in sheet.list)
                    {
                        if (param.Enable != 0 && !string.IsNullOrEmpty(param.Name))
                        {
                            resources.Add(new ResourceExport
                            {
                                Name = param.Name,
                                Recipe_A = param.Material_A,
                                Quantity_A = param.Product_A,
                                Workload_A = param.BP_A,
                                Recipe_B = param.Material_B,
                                Quantity_B = param.Product_B,
                                Workload_B = param.BP_B,
                                Recipe_C = param.Material_C,
                                Quantity_C = param.Product_C,
                                Workload_C = param.BP_C
                            });
                        }
                    }
                }
                var export = new ResourcesJson
                {
                    Resources = resources
                };
                File.WriteAllText(ResourcesJsonPath, JsonConvert.SerializeObject(export, Formatting.Indented));
                resourcesExported = true;
                LogSource?.LogInfo($"Exported resources to {ResourcesJsonPath}");
            }
            catch (System.Exception e)
            {
                LogSource?.LogError($"Error writing Resources.json: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(DB_Mgr), "Build_DB_Setting")]
    [HarmonyPriority(Priority.Last)]
    public class DB_Mgr_Build_DB_Setting_Patch
    {
        static void Prefix(DB_Mgr __instance)
        {
            var db = __instance.m_Building_DB1;
            if (db == null)
            {
                ProductionManagerPlugin.LogStaticError("DB_Mgr.m_Building_DB1 is null in Harmony patch!");
                return;
            }
            // Import from JSON if it exists
            if (File.Exists(BuildingsJsonPath))
            {
                try
                {
                    var json = File.ReadAllText(BuildingsJsonPath);
                    var importData = JsonConvert.DeserializeObject<BuildingsJson>(json);
                    importData.ApplyTo(db);
                    LogSource?.LogInfo($"Imported buildings from {BuildingsJsonPath}");
                }
                catch (System.Exception e)
                {
                    LogSource?.LogError($"Error importing Buildings.json: {e.Message}");
                }
            }
        }

        static void Postfix(DB_Mgr __instance)
        {
            if (buildingsExported) return;
            var db = __instance.m_Building_DB1;
            if (db == null)
            {
                ProductionManagerPlugin.LogStaticError("DB_Mgr.m_Building_DB1 is null in Harmony patch!");
                return;
            }
            try
            {
                if (!Directory.Exists(PluginFolder))
                    Directory.CreateDirectory(PluginFolder);

                var buildings = new List<BuildingExport>();
                foreach (var sheet in db.sheets)
                {
                    foreach (var param in sheet.list)
                    {
                        if (param.Enable == 1)
                        {
                            buildings.Add(new BuildingExport
                            {
                                Name = param.Name,
                                Products = param.Effect_Value3
                            });
                        }
                    }
                }
                var export = new BuildingsJson
                {
                    Buildings = buildings
                };
                File.WriteAllText(BuildingsJsonPath, JsonConvert.SerializeObject(export, Formatting.Indented));
                buildingsExported = true;
                LogSource?.LogInfo($"Exported buildings to {BuildingsJsonPath}");
            }
            catch (System.Exception e)
            {
                LogSource?.LogError($"Error writing Buildings.json: {e.Message}");
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