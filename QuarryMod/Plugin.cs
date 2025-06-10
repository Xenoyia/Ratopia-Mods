using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using I2.Loc;
using System.IO;
using MEC;
using BepInEx.Configuration;
using System.Text.RegularExpressions;

[BepInPlugin("quarry", "Quarry", "1.0.1")]
[BepInDependency("resourceloader", BepInDependency.DependencyFlags.HardDependency)]
public class QuarryPlugin : BaseUnityPlugin
{
    public static QuarryPlugin Instance { get; private set; }
    private ConfigEntry<string> recipeConfig;
    private ConfigEntry<int> researchPointsRequiredConfig;

    private readonly string[] resourceNames = new[] {
        "Dirt", "Mud", "Snow", "Slime", "Limestone", "Basalt", "Halite", "Rock", "Obsidian", "TileCoal", "Sandrock", "Iron", "Gold", "Copper", "Nickel"
    };
    private Dictionary<string, ConfigEntry<int>> resourceQuantities = new();
    private Dictionary<string, ConfigEntry<int>> resourceWorkloads = new();
    private Dictionary<string, ConfigEntry<bool>> resourceEnabled = new();

    private void Awake()
    {
        Instance = this;
        recipeConfig = Config.Bind("Build_DB", "recipe", "StoneBrick(8),Branch(12),Lumber(4)", "Recipe for the building (Material field)");
        researchPointsRequiredConfig = Config.Bind("Tech_DB", "researchpointsrequired", 7, "Research points required for the tech (Point field)");

        foreach (var res in resourceNames)
        {
            resourceEnabled[res] = Config.Bind("Build_DB", $"{res.ToLower()}_enabled", true, $"Enable {res} as a product");
            resourceQuantities[res] = Config.Bind("Res_DB", $"{res.ToLower()}_quantity", GetDefaultQuantity(res), $"Quantity for {res}");
            resourceWorkloads[res] = Config.Bind("Res_DB", $"{res.ToLower()}_workload", GetDefaultWorkload(res), $"Workload for {res}");
        }

        Logger.LogInfo("Quarry by Xenoyia loaded!");
        Harmony.CreateAndPatchAll(typeof(QuarryPlugin).Assembly);
    }

    public string GetRecipe() => recipeConfig.Value;
    public int GetResearchPointsRequired() => researchPointsRequiredConfig.Value;
    public string GetProducts() => string.Join(",", resourceNames.Where(res => resourceEnabled[res].Value).Select(res => $"{res}(A)"));
    public Dictionary<string, (int quantity, int workload)> GetResDbTable()
    {
        var dict = new Dictionary<string, (int, int)>();
        foreach (var res in resourceNames)
        {
            dict[res] = (resourceQuantities[res].Value, resourceWorkloads[res].Value);
        }
        return dict;
    }
    private int GetDefaultQuantity(string res)
    {
        return res switch
        {
            "Dirt" => 4,
            "Mud" => 4,
            "Snow" => 4,
            "Slime" => 4,
            "Limestone" => 4,
            "Basalt" => 4,
            "Halite" => 4,
            "Rock" => 4,
            "Obsidian" => 4,
            "TileCoal" => 4,
            "Sandrock" => 4,
            "Iron" => 4,
            "Gold" => 4,
            "Copper" => 4,
            "Nickel" => 4,
            _ => 4
        };
    }
    private int GetDefaultWorkload(string res)
    {
        return res switch
        {
            "Dirt" => 400,
            "Mud" => 600,
            "Snow" => 400,
            "Slime" => 600,
            "Limestone" => 1000,
            "Basalt" => 1000,
            "Halite" => 1000,
            "Rock" => 800,
            "Obsidian" => 1600,
            "TileCoal" => 1000,
            "Sandrock" => 800,
            "Iron" => 1800,
            "Gold" => 2400,
            "Copper" => 2000,
            "Nickel" => 3000,
            _ => 1000
        };
    }
}

public static class QuarryLog
{
    public static void Log(string msg)
    {
        Debug.Log($"[QuarryMod] {msg}");
    }
}

[HarmonyPatch(typeof(DB_Mgr), "Build_DB_Setting")]
public static class Patch_BuildingDB
{
    static void Prefix(DB_Mgr __instance)
    {
        try {
            var dbField = __instance.GetType().GetField("m_Building_DB1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var buildingDb = dbField?.GetValue(__instance);
            if (buildingDb == null) { QuarryLog.Log("m_Building_DB1 is null"); return; }
            var sheetsField = buildingDb.GetType().GetField("sheets");
            var sheets = sheetsField?.GetValue(buildingDb) as System.Collections.IList;
            if (sheets == null || sheets.Count == 0) { QuarryLog.Log("sheets is null or empty"); return; }
            var sheet = sheets[0];
            var listField = sheet.GetType().GetField("list");
            var paramList = listField?.GetValue(sheet) as System.Collections.IList;
            if (paramList == null) { QuarryLog.Log("paramList is null"); return; }
            foreach (var param in paramList)
            {
                var nameField = param.GetType().GetField("Name");
                string name = (string)nameField.GetValue(param);
                if (name == "GuardPost")
                {
                    param.GetType().GetField("Enable").SetValue(param, 1);
                    param.GetType().GetField("Index").SetValue(param, 52);
                    param.GetType().GetField("PDI").SetValue(param, 1);
                    param.GetType().GetField("Master").SetValue(param, 1);
                    param.GetType().GetField("Width").SetValue(param, 4);
                    param.GetType().GetField("Height").SetValue(param, 3);
                    param.GetType().GetField("Range").SetValue(param, 0);
                    param.GetType().GetField("Cost").SetValue(param, 200);
                    param.GetType().GetField("Payment").SetValue(param, 100);
                    param.GetType().GetField("BP").SetValue(param, 600);
                    param.GetType().GetField("HP").SetValue(param, 400);
                    param.GetType().GetField("Category").SetValue(param, 3);
                    param.GetType().GetField("Material").SetValue(param, QuarryPlugin.Instance.GetRecipe());
                    param.GetType().GetField("AbilityCode_A").SetValue(param, "Masonry");
                    param.GetType().GetField("Effect_Value3").SetValue(param, QuarryPlugin.Instance.GetProducts());
                    param.GetType().GetField("Variation").SetValue(param, 0);
                    param.GetType().GetField("Description").SetValue(param, "A reusable mine to gather resources.");
                }
            }
        } catch (System.Exception ex) {
            QuarryLog.Log($"[Patch_BuildingDB] Exception: {ex}");
        }
    }
}

[HarmonyPatch(typeof(DB_Mgr), "Res_DB_Setting")]
public static class Patch_ResDB
{
    static void Prefix(DB_Mgr __instance)
    {
        try {
            var dbField = __instance.GetType().GetField("m_Res_DB1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var resDb = dbField?.GetValue(__instance);
            if (resDb == null) { QuarryLog.Log("m_Res_DB1 is null"); return; }
            var sheetsField = resDb.GetType().GetField("sheets");
            var sheets = sheetsField?.GetValue(resDb) as System.Collections.IList;
            if (sheets == null || sheets.Count == 0) { QuarryLog.Log("sheets is null or empty"); return; }
            var sheet = sheets[0];
            var listField = sheet.GetType().GetField("list");
            var paramList = listField?.GetValue(sheet) as System.Collections.IList;
            if (paramList == null) { QuarryLog.Log("paramList is null"); return; }
            var table = QuarryPlugin.Instance.GetResDbTable();
            foreach (var param in paramList)
            {
                var nameField = param.GetType().GetField("Name");
                string name = (string)nameField.GetValue(param);
                if (table.ContainsKey(name))
                {
                    param.GetType().GetField("Material_A").SetValue(param, "");
                    param.GetType().GetField("Product_A").SetValue(param, table[name].Item1);
                    param.GetType().GetField("BP_A").SetValue(param, table[name].Item2);
                }
            }
        } catch (System.Exception ex) {
            QuarryLog.Log($"[Patch_ResDB] Exception: {ex}");
        }
    }
}

[HarmonyPatch(typeof(DB_Mgr), "Tech_DB_Setting")]
public static class Patch_TechDB
{
    static void Prefix(DB_Mgr __instance)
    {
        try {
            var dbField = __instance.GetType().GetField("m_Tech_DB1", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var techDb = dbField?.GetValue(__instance);
            if (techDb == null) { QuarryLog.Log("m_Tech_DB1 is null"); return; }
            var sheetsField = techDb.GetType().GetField("sheets");
            var sheets = sheetsField?.GetValue(techDb) as System.Collections.IList;
            if (sheets == null || sheets.Count == 0) { QuarryLog.Log("sheets is null or empty"); return; }
            var sheet = sheets[0];
            var listField = sheet.GetType().GetField("list");
            var paramList = listField?.GetValue(sheet) as System.Collections.IList;
            if (paramList == null) { QuarryLog.Log("paramList is null"); return; }
            foreach (var param in paramList)
            {
                var indexField = param.GetType().GetField("Index");
                int idx = (int)indexField.GetValue(param);
                if (idx == 63)
                {
                    QuarryLog.Log("Found Tech index 63, patching...");
                    param.GetType().GetField("Enable").SetValue(param, 1);
                    param.GetType().GetField("Name").SetValue(param, "63");
                    param.GetType().GetField("Category").SetValue(param, 2);
                    param.GetType().GetField("Point").SetValue(param, QuarryPlugin.Instance.GetResearchPointsRequired());
                    param.GetType().GetField("Tier").SetValue(param, 3);
                    param.GetType().GetField("X_Pos").SetValue(param, 4);
                    param.GetType().GetField("Y_Pos").SetValue(param, -2);
                    param.GetType().GetField("Need_Index").SetValue(param, "[62]");
                    param.GetType().GetField("Tech_Name1").SetValue(param, 0);
                    param.GetType().GetField("Tech_Value1").SetValue(param, 52);
                    param.GetType().GetField("Tech_Name2").SetValue(param, -1);
                }
            }
        } catch (System.Exception ex) {
            QuarryLog.Log($"[Patch_TechDB] Exception: {ex}");
        }
    }
}

[HarmonyPatch(typeof(I2.Loc.LocalizationManager), "RegisterSourceInResources")]
public static class Patch_I2Languages_RegisterSourceInResources
{
    static void Postfix()
    {
        try {
            var sources = I2.Loc.LocalizationManager.Sources;
            if (sources == null || sources.Count == 0) {
                QuarryLog.Log("LocalizationManager.Sources is null or empty");
                return;
            }
            var source = sources[0];
            if (source == null) {
                QuarryLog.Log("LocalizationManager.Sources[0] is null");
                return;
            }
            
            var nameTranslations = new Dictionary<string, string> {
                {"English", "Quarry"},
                {"Korean", "채석장"},
                {"Japanese", "採石場"},
                {"Chinese (Simplified)", "采石场"},
                {"Chinese (Traditional)", "採石場"},
                {"French", "Carrière"},
                {"German", "Steinbruch"},
                {"Spanish", "Cantera"},
                {"Russian", "Карьера"},
                {"Portuguese (Brazil)", "Pedreira"},
                {"Turkish", "Taş Ocağı"},
                {"Polish", "Kamieniołom"},
                {"Ukrainian", "Кар'єр"},
                {"Italian", "Cava"},
                {"Dutch", "Steengroeve"},
                {"Czech", "Lom"},
                {"Hungarian", "Kőbánya"},
                {"Romanian", "Carieră"},
                {"Thai", "เหมืองหิน"},
                {"Vietnamese", "Mỏ đá"},
                {"Indonesian", "Tambang Batu"},
                {"Arabic", "مقلع حجارة"},
                {"Hindi", "पत्थर की खान"},
                {"Bengali", "পাথরের খনি"},
                {"Malay", "Kuari"},
                {"Greek", "Λατομείο"},
                {"Bulgarian", "Кариер"},
                {"Serbian", "Каменолом"},
                {"Croatian", "Kamenolom"},
                {"Slovak", "Kameňolom"},
                {"Finnish", "Kivilouhos"},
                {"Swedish", "Stenbrott"},
                {"Norwegian", "Steinbrudd"},
                {"Danish", "Stenbrud"},
                {"Hebrew", "מחצבה"},
                {"Persian", "معدن سنگ"},
                {"Urdu", "پتھر کی کان"},
                {"Tagalog", "Kuwary"},
                {"Swahili", "Machimbo"},
                {"Afrikaans", "Steengroef"},
                {"Esperanto", "Ŝtonminejo"},
            };
            var descTranslations = new Dictionary<string, string> {
                {"English", "A reusable mine to gather resources."},
                {"Korean", "자원을 채취할 수 있는 재사용 가능한 광산입니다."},
                {"Japanese", "資源を採取できる再利用可能な鉱山です。"},
                {"Chinese (Simplified)", "可重复使用的采矿场以收集资源。"},
                {"Chinese (Traditional)", "可重複使用的採礦場以收集資源。"},
                {"French", "Une mine réutilisable pour collecter des ressources."},
                {"German", "Eine wiederverwendbare Mine zur Rohstoffgewinnung."},
                {"Spanish", "Una mina reutilizable para recolectar recursos."},
                {"Russian", "Многоразовая шахта для добычи ресурсов."},
                {"Portuguese (Brazil)", "Uma mina reutilizável para coletar recursos."},
                {"Turkish", "Kaynak toplamak için tekrar kullanılabilir bir maden."},
                {"Polish", "Wielokrotnego użytku kopalnia do zbierania surowców."},
                {"Ukrainian", "Багаторазова шахта для збору ресурсів."},
                {"Italian", "Una miniera riutilizzabile per raccogliere risorse."},
                {"Dutch", "Een herbruikbare mijn om grondstoffen te verzamelen."},
                {"Czech", "Znovupoužitelný důl pro získávání surovin."},
                {"Hungarian", "Újrahasználható bánya erőforrások gyűjtésére."},
                {"Romanian", "O mină reutilizabilă pentru a colecta resurse."},
                {"Thai", "เหมืองที่ใช้ซ้ำได้สำหรับเก็บทรัพยากร."},
                {"Vietnamese", "Mỏ có thể tái sử dụng để thu thập tài nguyên."},
                {"Indonesian", "Tambang yang dapat digunakan kembali untuk mengumpulkan sumber daya."},
                {"Arabic", "منجم قابل لإعادة الاستخدام لجمع الموارد."},
                {"Hindi", "संसाधन इकट्ठा करने के लिए पुन: प्रयोज्य खान।"},
                {"Bengali", "সম্পদ সংগ্রহের জন্য পুনঃব্যবহারযোগ্য খনি।"},
                {"Malay", "Lombong boleh diguna semula untuk mengumpul sumber."},
                {"Greek", "Ένα επαναχρησιμοποιήσιμο ορυχείο για τη συλλογή πόρων."},
                {"Bulgarian", "Многоразова мина за събиране на ресурси."},
                {"Serbian", "Поново употребљива рудник за прикупљање ресурса."},
                {"Croatian", "Višekratna jama za prikupljanje resursa."},
                {"Slovak", "Opakovane použiteľná baňa na získavanie zdrojov."},
                {"Finnish", "Uudelleenkäytettävä kaivos resurssien keräämiseen."},
                {"Swedish", "En återanvändbar gruva för att samla resurser."},
                {"Norwegian", "En gjenbrukbar gruve for å samle ressurser."},
                {"Danish", "En genanvendelig mine til at samle ressourcer."},
                {"Hebrew", "מכרה לשימוש חוזר לאיסוף משאבים."},
                {"Persian", "معدن قابل استفاده مجدد برای جمع آوری منابع."},
                {"Urdu", "وسائل جمع کرنے کے لیے دوبارہ قابل استعمال کان۔"},
                {"Tagalog", "Isang magagamit muli na minahan para mangolekta ng mga mapagkukunan."},
                {"Swahili", "Mgodi unaoweza kutumika tena kukusanya rasilimali."},
                {"Afrikaans", "'n Herbruikbare myn om hulpbronne in te samel."},
                {"Esperanto", "Reuzebla minejo por kolekti rimedojn."},
            };
            
            // Helper to set or add a term
            void SetTerm(string term, Dictionary<string, string> translations) {
                var termData = source.GetTermData(term, false);
                if (termData == null) {
                    termData = source.AddTerm(term);
                }
                for (int i = 0; i < source.mLanguages.Count; i++) {
                    var lang = source.mLanguages[i].Name;
                    if (translations.TryGetValue(lang, out var value)) {
                        termData.Languages[i] = value;
                    }
                }
            }
            
            // Set tech translations
            SetTerm("Tech_DB(Product)/Nm_63", nameTranslations);
            SetTerm("Tech_DB(Product)/Scr_63", descTranslations);
            
            // Set building translations
            SetTerm("Build_DB/GuardPost_Nm", nameTranslations);
            SetTerm("Build_DB/GuardPost_Scr", descTranslations);
            
            // Set building translations for Quarry
            SetTerm("Build_DB/Quarry_Nm", nameTranslations);
            SetTerm("Build_DB/Quarry_Scr", descTranslations);
            
        } catch (System.Exception ex) {
            QuarryLog.Log($"[Patch_I2Languages_RegisterSourceInResources] Exception: {ex}");
        }
    }
}