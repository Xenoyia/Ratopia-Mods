using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using CasselGames.Audio;
using I2.Loc;
using MEC;
using Utility.Achievement;
using BepInEx.Configuration;
using System;
using System.IO;
using Extensions;
using HarmonyLib;
using System.Reflection;
using System.Linq;

namespace EnhancedRatMating
{
    [BepInPlugin("enhancedratmating", "Enhanced Rat Mating", "1.0.1")]
    public class EnhancedRatMating : BaseUnityPlugin
    {
        public static EnhancedRatMating Instance { get; private set; }
        private ConfigEntry<int> MarriageChance;
        private ConfigEntry<int> MatingChance;
        private ConfigEntry<int> TryForMarriageTime;
        private ConfigEntry<int> TryForMatingTime;
        internal ConfigEntry<bool> AllowMarriage;
        internal ConfigEntry<bool> AllowMating;
        private ConfigEntry<bool> Overpopulation;
        private ConfigEntry<bool> MarriageAlert;
        private ConfigEntry<string> MarriageAlertMsg;
        private ConfigEntry<bool> GayMarriages;
        private ConfigEntry<bool> GayBabies;
        private ConfigEntry<bool> BabyAlert;
        private ConfigEntry<string> BabyAlertMsg;
        private ConfigEntry<int> MarriageHappiness;
        private ConfigEntry<bool> DebugLogging;
        private ConfigEntry<bool> AllowAchievement;
        private ConfigEntry<float> PairingRadius;
        private ConfigEntry<bool> RatIncest;
        private ConfigEntry<bool> IncestBabies;

        private ConfigFile customConfig;

        internal static bool routinesStarted = false;

        private static Type GameMgrType, NpcAlarmUIType;
        private static PropertyInfo GameMgrInstanceProp;
        private static FieldInfo NpcAlarmUIField;
        private static MethodInfo NpcAlarmCallMethod;
        private static Type AlarmStateType;
        private static object AlarmStateBasic;
        private static bool npcAlarmReflectionInitialized = false;

        public static Dictionary<BabyRat, (T_Citizen, T_Citizen)> ModBabyParents = new();

        protected void Awake()
        {
            Instance = this;
            string configDir = Path.Combine(Paths.PluginPath, "EnhancedRatMating");
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            string configPath = Path.Combine(configDir, "Config.cfg");
            customConfig = new ConfigFile(configPath, true);

            MarriageChance = customConfig.Bind("General", "MarriageChance", 50, "Chance (percent) for two eligible rats to marry each interval.");
            MatingChance = customConfig.Bind("General", "MatingChance", 20, "Chance (percent) for a married, sleeping pair to have a baby each interval.");
            TryForMarriageTime = customConfig.Bind("General", "TryForMarriageTime", 60, "Interval in real seconds between marriage attempts.");
            TryForMatingTime = customConfig.Bind("General", "TryForMatingTime", 60, "Interval in real seconds between mating attempts.");
            AllowMarriage = customConfig.Bind("General", "AllowMarriage", true, "If false, disables all marriage attempts.");
            AllowMating = customConfig.Bind("General", "AllowMating", true, "If false, disables all mating attempts.");
            Overpopulation = customConfig.Bind("General", "Overpopulation", false, "If true, disables the population cap for baby creation. May cause issues and many, many rat babies.");
            MarriageAlert = customConfig.Bind("General", "MarriageAlert", true, "If false, disables the marriage alert message.");
            MarriageAlertMsg = customConfig.Bind("General", "MarriageAlertMsg", "{Rat1} and {Rat2} have gotten married!", "Message shown when two rats get married. Use {Rat1} and {Rat2} as placeholders for the rat names.");
            GayMarriages = customConfig.Bind("General", "GayMarriages", false, "If true, allows any gender combination to get married. If false, only male-female pairs can marry.");
            GayBabies = customConfig.Bind("General", "GayBabies", false, "If true, allows any married pair to have babies. If false, only male-female pairs can have babies.");
            BabyAlert = customConfig.Bind("General", "BabyAlert", true, "If false, disables the baby alert message.");
            BabyAlertMsg = customConfig.Bind("General", "BabyAlertMsg", "{Rat1} and {Rat2} have had a baby named {BabyRat}! Welcome to the family!", "Message shown when two rats have a baby. Use {Rat1}, {Rat2}, and {BabyRat} as placeholders for the rat and baby names.");
            MarriageHappiness = customConfig.Bind("General", "MarriageHappiness", 5, "The amount of happiness given to each rat when they get married.");
            DebugLogging = customConfig.Bind("General", "Debug", false, "Enable debug logging for Enhanced Rat Mating.");
            AllowAchievement = customConfig.Bind("General", "AllowAchievement", false, "If true, enables achievement progress for marriages.");
            PairingRadius = customConfig.Bind("General", "PairingRadius", 5f, "Maximum distance (in world units) for two rats to be considered for marriage or baby making.");
            RatIncest = customConfig.Bind("General", "RatIncest", false, "If false, prevents rats from marrying their direct parent/child.");
            IncestBabies = customConfig.Bind("General", "IncestBabies", false, "If false, prevents rats from having babies with their direct parent/child.");

            Logger.LogInfo($"Enhanced Rat Mating by Xenoyia loaded!");
            var harmony = new HarmonyLib.Harmony("enhancedratmating");
            harmony.PatchAll();
        }

        public void GameLoaded()
        {
            if (DebugLogging.Value) Logger.LogInfo("[EnhancedRatMating] Game loaded, starting routines.");
            if (AllowMarriage.Value)
                Timing.RunCoroutine(FamilyMarriageRoutine());
            else
                Logger.LogInfo("[EnhancedRatMating] AllowMarriage is false, not starting marriage routine.");
            if (AllowMating.Value)
                Timing.RunCoroutine(FamilyMatingRoutine());
            else
                Logger.LogInfo("[EnhancedRatMating] AllowMating is false, not starting mating routine.");
            routinesStarted = true;
        }

        internal IEnumerator<float> FamilyMarriageRoutine()
        {
            while (true)
            {
                TryPairCitizens();
                yield return Timing.WaitForSeconds(TryForMarriageTime.Value);
            }
        }

        internal IEnumerator<float> FamilyMatingRoutine()
        {
            while (true)
            {
                TryMakeBabies();
                yield return Timing.WaitForSeconds(TryForMatingTime.Value);
            }
        }

        private void EnsureNpcAlarmReflection()
        {
            if (npcAlarmReflectionInitialized) return;
            GameMgrType = AccessTools.TypeByName("GameMgr");
            NpcAlarmUIType = AccessTools.TypeByName("NpcAlarmUI");
            GameMgrInstanceProp = GameMgrType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            NpcAlarmUIField = GameMgrType.GetField("_NpcAlarmUI", BindingFlags.Instance | BindingFlags.Public);
            AlarmStateType = NpcAlarmUIType.GetNestedType("AlarmState");
            NpcAlarmCallMethod = NpcAlarmUIType.GetMethod("NpcAlarm_Call", new[] { typeof(string), typeof(bool), AlarmStateType, typeof(int) });
            AlarmStateBasic = System.Enum.Parse(AlarmStateType, "Basic");
            npcAlarmReflectionInitialized = true;
        }

        private void ShowMarriageNpcAlarm(string message)
        {
            EnsureNpcAlarmReflection();
            var gameMgr = GameMgrInstanceProp.GetValue(null);
            var npcAlarmUI = NpcAlarmUIField.GetValue(gameMgr);
            NpcAlarmCallMethod.Invoke(npcAlarmUI, new object[] { message, false, AlarmStateBasic, 0 });
        }

        private static bool IsSleepingAniState(AniState state)
        {
            return state == AniState.Sleep ||
                   state == AniState.Sleep_bed_Start ||
                   state == AniState.Sleep_bed_Start_Floor ||
                   state == AniState.Sleep_bed_End ||
                   state == AniState.Sleep_bed ||
                   state == AniState.Sleep_End ||
                   state == AniState.Sleep_Start;
        }

        // Helper to check if mate is alive, else reset ID and remove Wedding buff
        internal static bool isMateAlive(T_Citizen citizen)
        {
            if (citizen.m_MateID == -1) return false;
            var mate = GameMgr.Instance._T_UnitMgr.List_Citizen.Find(x => x.m_ID == citizen.m_MateID);
            if (mate == null || mate.m_CharState == CharState.Death)
            {
                citizen.m_MateID = -1;
                citizen.m_Buff.RefKill("Wedding");
                return false;
            }
            return true;
        }

        // Helper to check if child is alive, else reset ID
        internal static bool isChildAlive(T_Citizen citizen)
        {
            if (citizen.m_ChildID == -1) return false;
            var child = GameMgr.Instance._T_UnitMgr.List_Citizen.Find(x => x.m_ID == citizen.m_ChildID);
            if (child == null || child.m_CharState == CharState.Death)
            {
                citizen.m_ChildID = -1;
                return false;
            }
            return true;
        }

        private void TryPairCitizens()
        {
            var citizens = GameMgr.Instance._T_UnitMgr.List_Citizen;
            List<T_Citizen> eligible = new List<T_Citizen>();
            foreach (var citizen in citizens)
            {
                // Check mate/child alive status before considering for pairing
                isMateAlive(citizen);
                isChildAlive(citizen);
                if (citizen.m_MateID == -1 && citizen.m_ImFatigue != 0 && citizen.m_CharState != CharState.Children && IsSleepingAniState(citizen.m_AniState))
                {
                    eligible.Add(citizen);
                }
            }
            if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Found {eligible.Count} eligible citizens for marriage");
            int pairsFormed = 0;
            var used = new HashSet<int>();

            float radius = PairingRadius.Value;

            if (!GayMarriages.Value)
            {
                // Prefer opposite-gender pairs
                var males = new List<T_Citizen>();
                var females = new List<T_Citizen>();
                foreach (var c in eligible)
                {
                    if (c.m_Gender == Gender.Male) males.Add(c);
                    else if (c.m_Gender == Gender.Female) females.Add(c);
                }
                males.Shuffle<T_Citizen>();
                females.Shuffle<T_Citizen>();
                var pairedFemales = new HashSet<int>();
                for (int i = 0; i < males.Count; i++)
                {
                    var a = males[i];
                    if (used.Contains(a.m_ID)) continue;
                    // Find a nearby eligible female
                    T_Citizen b = null;
                    float minDist = float.MaxValue;
                    for (int j = 0; j < females.Count; j++)
                    {
                        var f = females[j];
                        if (used.Contains(f.m_ID) || pairedFemales.Contains(f.m_ID)) continue;
                        float dist = Vector3.Distance(a.Tf.position, f.Tf.position);
                        if (dist <= radius && dist < minDist)
                        {
                            // Prevent parent-child marriage unless RatIncest is true
                            if (!RatIncest.Value && (a.m_ChildID == f.m_ID || f.m_ChildID == a.m_ID))
                                continue;
                            b = f;
                            minDist = dist;
                        }
                    }
                    if (b == null) continue;
                    int roll = UnityEngine.Random.Range(1, 101);
                    if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Trying to pair {a.m_UnitName} and {b.m_UnitName} (roll={roll}, threshold={MarriageChance.Value}, dist={minDist})");
                    if (roll <= MarriageChance.Value)
                    {
                        AudioController.PlayUIOneShot("SFX_Other_Married_F_Full");
                        string text = Helpers.ScriptSetting(LocalizationManager.GetTranslation("Alarm/Someone got married"), a.m_UnitName, b.m_UnitName);
                        if (MarriageAlert.Value)
                        {
                            string marriageMsg = MarriageAlertMsg.Value.Replace("{Rat1}", a.m_UnitName).Replace("{Rat2}", b.m_UnitName);
                            ShowMarriageNpcAlarm(marriageMsg);
                        }
                        a.m_MateID = b.m_ID;
                        b.m_MateID = a.m_ID;
                        int happyAmount = MarriageHappiness.Value;
                        a.m_Buff.BuffRefSet(C_Buff.HappyUp, "Wedding", C_Buff_Category.None, happyAmount, -999);
                        b.m_Buff.BuffRefSet(C_Buff.HappyUp, "Wedding", C_Buff_Category.None, happyAmount, -999);
                        pairsFormed++;
                        if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Married {a.m_UnitName} and {b.m_UnitName}");
                        if (AllowAchievement.Value) AchievementManager.Instance.AddValue(TypeAchievementCategory.NewFamily, "val", 1f);
                        used.Add(a.m_ID);
                        used.Add(b.m_ID);
                        pairedFemales.Add(b.m_ID);
                    }
                    else
                    {
                        if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Pairing failed due to chance");
                    }
                }
            }
            else
            {
                eligible.Shuffle<T_Citizen>(); // Randomize order for fairness
                for (int i = 0; i < eligible.Count - 1; i++)
                {
                    if (used.Contains(eligible[i].m_ID)) continue;
                    var a = eligible[i];
                    bool paired = false;
                    float radiusVal = PairingRadius.Value;
                    for (int j = i + 1; j < eligible.Count; j++)
                    {
                        if (used.Contains(eligible[j].m_ID)) continue;
                        var b = eligible[j];
                        if (!GayMarriages.Value && a.m_Gender == b.m_Gender)
                            continue;
                        if (a != b && b.m_MateID == -1 && b.m_ImFatigue != 0 && b.m_CharState != CharState.Children && IsSleepingAniState(b.m_AniState))
                        {
                            float dist = Vector3.Distance(a.Tf.position, b.Tf.position);
                            if (dist > radiusVal) continue;
                            // Prevent parent-child marriage unless RatIncest is true
                            if (!RatIncest.Value && (a.m_ChildID == b.m_ID || b.m_ChildID == a.m_ID))
                                continue;
                            int roll = UnityEngine.Random.Range(1, 101);
                            if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Trying to pair {a.m_UnitName} and {b.m_UnitName} (roll={roll}, threshold={MarriageChance.Value}, dist={dist})");
                            if (roll <= MarriageChance.Value)
                            {
                                AudioController.PlayUIOneShot("SFX_Other_Married_F_Full");
                                string text = Helpers.ScriptSetting(LocalizationManager.GetTranslation("Alarm/Someone got married"), a.m_UnitName, b.m_UnitName);
                                if (MarriageAlert.Value)
                                {
                                    string marriageMsg = MarriageAlertMsg.Value.Replace("{Rat1}", a.m_UnitName).Replace("{Rat2}", b.m_UnitName);
                                    ShowMarriageNpcAlarm(marriageMsg);
                                }
                                a.m_MateID = b.m_ID;
                                b.m_MateID = a.m_ID;
                                int happyAmount = MarriageHappiness.Value;
                                a.m_Buff.BuffRefSet(C_Buff.HappyUp, "Wedding", C_Buff_Category.None, happyAmount, -999);
                                b.m_Buff.BuffRefSet(C_Buff.HappyUp, "Wedding", C_Buff_Category.None, happyAmount, -999);
                                pairsFormed++;
                                if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Married {a.m_UnitName} and {b.m_UnitName}");
                                if (AllowAchievement.Value) AchievementManager.Instance.AddValue(TypeAchievementCategory.NewFamily, "val", 1f);
                                used.Add(a.m_ID);
                                used.Add(b.m_ID);
                                paired = true;
                                break;
                            }
                            else
                            {
                                if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Pairing failed due to chance");
                                used.Add(a.m_ID);
                                used.Add(b.m_ID);
                                paired = true;
                                break;
                            }
                        }
                    }
                    if (!paired)
                    {
                        used.Add(a.m_ID); // Could not pair, mark as used
                    }
                }
            }
            if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Formed {pairsFormed} new marriages this interval");
        }

        private void TryMakeBabies()
        {
            var citizens = GameMgr.Instance._T_UnitMgr.List_Citizen;
            var checkedPairs = new HashSet<int>();
            int babiesMade = 0;
            var citizensCopy = citizens.ToList();
            float radius = PairingRadius.Value;
            foreach (var a in citizensCopy)
            {
                // Check mate/child alive status before considering for baby making
                isMateAlive(a);
                isChildAlive(a);
                if (a.m_MateID == -1 || a.m_ChildID != -1 || a.m_CharState == CharState.Children) continue;
                var b = GameMgr.Instance._T_UnitMgr.List_Citizen.Find(x => x.m_ID == a.m_MateID);
                if (b == null || b.m_ChildID != -1 || b.m_MateID != a.m_ID || b.m_CharState == CharState.Children) continue;
                // Prevent parent-child babies unless IncestBabies is true
                if (!IncestBabies.Value && (a.m_ChildID == b.m_ID || b.m_ChildID == a.m_ID))
                    continue;
                int pairKey = a.m_ID < b.m_ID ? (a.m_ID << 16) | b.m_ID : (b.m_ID << 16) | a.m_ID;
                if (checkedPairs.Contains(pairKey)) continue;
                checkedPairs.Add(pairKey);
                if (!IsSleepingAniState(a.m_AniState) || !IsSleepingAniState(b.m_AniState))
                {
                    if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Pair {a.m_UnitName} aniState: {a.m_AniState}");
                    if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Pair {b.m_UnitName} aniState: {b.m_AniState}");
                    if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Pair {a.m_UnitName} and {b.m_UnitName} not both sleeping (by aniState)");
                    continue;
                }
                float dist = Vector3.Distance(a.Tf.position, b.Tf.position);
                if (dist > radius)
                {
                    if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Pair {a.m_UnitName} and {b.m_UnitName} are too far apart (dist={dist}, radius={radius})");
                    continue;
                }
                if (!Overpopulation.Value && GameMgr.Instance._T_UnitMgr.List_Citizen.Count + 1 > GameMgr.Instance._ProsperityUI.GetMaxCitizenCount())
                {
                    if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Population cap reached, cannot make baby");
                    continue;
                }
                int roll = UnityEngine.Random.Range(1, 101);
                if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Trying to make baby for {a.m_UnitName} and {b.m_UnitName} (roll={roll}, threshold={MatingChance.Value}, dist={dist})");
                if (roll > MatingChance.Value)
                {
                    if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Baby making failed due to chance");
                    continue;
                }
                if (!GayBabies.Value && a.m_Gender == b.m_Gender)
                {
                    if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Pair {a.m_UnitName} and {b.m_UnitName} are same gender, cannot make baby");
                    continue;
                }
                T_Citizen t_Citizen2 = (a.m_Gender == Gender.Male) ? a : b;
                T_Citizen t_Citizen3 = (a.m_Gender == Gender.Female) ? a : b;
                BabyRat babyRat = GameMgr.Instance._T_UnitMgr.MakeBabyRat(t_Citizen2.m_SkinInfo, t_Citizen3.m_SkinInfo, t_Citizen2.Tf.position);
                EnhancedRatMating.ModBabyParents[babyRat] = (a, b);
                babyRat.m_Job = null;
                if (UnityEngine.Random.Range(0, 2) == 0)
                {
                    babyRat.CharApply(a.List_CharInfo[0], b.List_CharInfo[1]);
                }
                else
                {
                    babyRat.CharApply(b.List_CharInfo[0], a.List_CharInfo[1]);
                }
                babyRat.m_UnitName = GameMgr.Instance._DB_Mgr.GetRandomName(babyRat.m_Gender);
                babyRat.NameUpdate();
                babyRat.m_Buff.BuffRefSet(C_Buff.Children, "", C_Buff_Category.None, 1f, 12, false, false);
                a.m_ChildID = babyRat.m_ID;
                b.m_ChildID = babyRat.m_ID;
                if (BabyAlert.Value)
                {
                    string babyMsg = BabyAlertMsg.Value.Replace("{Rat1}", a.m_UnitName).Replace("{Rat2}", b.m_UnitName).Replace("{BabyRat}", babyRat.m_UnitName);
                    ShowMarriageNpcAlarm(babyMsg);
                }
                babiesMade++;
                if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] {a.m_UnitName} and {b.m_UnitName} had a baby: {babyRat.m_UnitName}");
            }
            if (DebugLogging.Value) Logger.LogInfo($"[EnhancedRatMating] Made {babiesMade} babies this interval");
        }

        internal new BepInEx.Logging.ManualLogSource Logger => base.Logger;

        public static (T_Citizen, T_Citizen) GetParentsForBaby(BabyRat baby)
        {
            // 1. Try to get from mod mapping
            if (EnhancedRatMating.ModBabyParents.TryGetValue(baby, out var parents) && parents.Item1 != null && parents.Item2 != null)
                return parents;

            // 2. Search all citizens for m_ChildID == baby.m_ID
            var allCitizens = GameMgr.Instance._T_UnitMgr.List_Citizen;
            var foundParents = allCitizens.Where(c => c.m_ChildID == baby.m_ID && c.m_CharState != CharState.Death).ToList();

            if (foundParents.Count == 2)
                return (foundParents[0], foundParents[1]);
            if (foundParents.Count == 1)
                return (foundParents[0], foundParents[0]);

            // 3. Fallback: pick two random, alive, non-child citizens
            var candidates = allCitizens.Where(c => c.m_CharState != CharState.Death && c.m_CharState != CharState.Children).ToList();
            if (candidates.Count >= 2)
            {
                var rnd = new System.Random();
                int idx1 = rnd.Next(candidates.Count);
                int idx2;
                do { idx2 = rnd.Next(candidates.Count); } while (idx2 == idx1);
                return (candidates[idx1], candidates[idx2]);
            }
            else if (candidates.Count == 1)
            {
                return (candidates[0], candidates[0]);
            }
            else
            {
                // As a last resort, return the baby itself twice (should never happen)
                return (baby, baby);
            }
        }
    }

    [HarmonyPatch(typeof(T_Queen), "LoadSetting")]
    public class T_Queen_LoadSetting_Patch
    {
        static void Postfix()
        {
           EnhancedRatMating.Instance.GameLoaded();
        }
    }

    [HarmonyPatch(typeof(BabyRat), "DeathCheckC")]
    class Patch_BabyRat_DeathCheckC
    {
        static bool Prefix(BabyRat __instance, int _key, ref IEnumerator<float> __result)
        {
            var logger = EnhancedRatMating.Instance.Logger;
            logger.LogInfo($"[Patch_BabyRat_DeathCheckC] Prefix called for baby: {__instance?.m_UnitName ?? "null"}");
            var (parentA, parentB) = EnhancedRatMating.GetParentsForBaby(__instance);
            if (parentA != null && parentB != null)
            {
                logger.LogInfo($"[Patch_BabyRat_DeathCheckC] Using modded death check for: {__instance.m_UnitName}");
                __result = ModdedDeathCheckC(__instance, _key, parentA, parentB);
                EnhancedRatMating.ModBabyParents.Remove(__instance); // Clean up if present
                return false; // Skip vanilla
            }
            logger.LogInfo($"[Patch_BabyRat_DeathCheckC] Using vanilla death check for: {__instance?.m_UnitName ?? "null"}");
            return true; // Use vanilla if not a mod baby
        }

        static IEnumerator<float> ModdedDeathCheckC(BabyRat baby, int _key, T_Citizen parentA, T_Citizen parentB)
        {
            var logger = EnhancedRatMating.Instance.Logger;
            if (parentA == null || parentB == null)
            {
                logger.LogError("parentA or parentB is null in ModdedDeathCheckC!");
                yield break;
            }
            if (parentA.List_CharInfo == null || parentA.List_CharInfo.Count < 1)
            {
                logger.LogError($"parentA.List_CharInfo is null or too short! Count: {(parentA.List_CharInfo == null ? -1 : parentA.List_CharInfo.Count)}");
                yield break;
            }
            if (parentB.List_CharInfo == null || parentB.List_CharInfo.Count < 2)
            {
                logger.LogError($"parentB.List_CharInfo is null or too short! Count: {(parentB.List_CharInfo == null ? -1 : parentB.List_CharInfo.Count)}");
                yield break;
            }
            if (baby == null)
            {
                logger.LogError("baby is null in ModdedDeathCheckC!");
                yield break;
            }
            if (baby.Tf == null)
            {
                logger.LogError("baby.Tf is null in ModdedDeathCheckC!");
                yield break;
            }
            if (GameMgr.Instance == null || GameMgr.Instance._T_UnitMgr == null)
            {
                logger.LogError("GameMgr.Instance or _T_UnitMgr is null in ModdedDeathCheckC!");
                yield break;
            }
            if (GameMgr.Instance._EcoMgr == null || GameMgr.Instance._EcoMgr.m_CitizenUI == null)
            {
                logger.LogError("GameMgr.Instance._EcoMgr or m_CitizenUI is null in ModdedDeathCheckC!");
                yield break;
            }
            if (GameMgr.Instance._PoolMgr == null || GameMgr.Instance._PoolMgr.Pool_BabyRat == null)
            {
                logger.LogError("GameMgr.Instance._PoolMgr or Pool_BabyRat is null in ModdedDeathCheckC!");
                yield break;
            }

            GameMgr.Instance._T_UnitMgr.List_Citizen.Remove(baby);
            GameMgr.Instance._T_UnitMgr.List_AllTeam.Remove(baby);
            GameMgr.Instance._PathFindMgr.InfoUpdate();

            var ccInfo = new CCMake_Info(0, 0);
            ccInfo.m_Gender = baby.m_Gender;
            ccInfo.Name = baby.m_UnitName;
            ccInfo.SkinInfo = Helpers.GetCombineSkin(parentA.m_SkinInfo, parentB.m_SkinInfo, ccInfo.m_Gender);

            ccInfo.Power = Helpers.GetAutoRange(parentA.m_Power, parentB.m_Power + 1);
            ccInfo.Dex = Helpers.GetAutoRange(parentA.m_Dex, parentB.m_Dex + 1);
            ccInfo.Int = Helpers.GetAutoRange(parentA.m_Int, parentB.m_Int + 1);

            ccInfo.List_CharInfo.Clear();
            ccInfo.List_CharInfo.AddRange(new[] { parentA.List_CharInfo[0], parentB.List_CharInfo[1] });

            ccInfo.m_Religion = (UnityEngine.Random.Range(0, 2) == 0) ? parentA.m_Religion : parentB.m_Religion;

            T_Citizen newAdult = GameMgr.Instance._T_UnitMgr.MakeCitizenByChild(baby.Tf.position, ccInfo, baby.m_ID);
            newAdult.m_UnitName = baby.m_UnitName;
            newAdult.NameUpdate();

            // Optionally: show effect, update UI, etc.
            GameMgr.Instance._EcoMgr.m_CitizenUI.CitizenTxtUpdate();
            GameMgr.Instance._PoolMgr.Pool_BabyRat.AddObj(baby.gameObject);

            yield break;
        }
    }

    [HarmonyPatch(typeof(BatchR_Char), "RightSkinSet")]
    class Patch_BatchR_Char_RightSkinSet
    {
        static void Postfix(BatchR_Char __instance, T_Citizen _unit)
        {
            // Find a BatchR_CharSlot to clone (m_Slot_Religion)
            var slotTemplate = Traverse.Create(__instance).Field("m_Slot_Religion").GetValue<BatchR_CharSlot>();
            if (slotTemplate == null) return;
            var parent = slotTemplate.transform.parent;

            // Helper to get or create a slot
            BatchR_CharSlot GetOrCreateSlot(string name)
            {
                var tr = parent.Find(name);
                if (tr != null)
                    return tr.GetComponent<BatchR_CharSlot>();
                var go = GameObject.Instantiate(slotTemplate.gameObject, parent);
                go.name = name;
                return go.GetComponent<BatchR_CharSlot>();
            }

            // Helper to set the label (left side) of the slot
            void SetSlotLabel(BatchR_CharSlot slot, string label)
            {
                var labelObj = slot.transform.Find("Txt_InfoSlot_Name");
                if (labelObj != null)
                {
                    var tmp = labelObj.GetComponent<TMPro.TextMeshProUGUI>();
                    if (tmp != null)
                        tmp.text = label;
                }
            }

            // Mate slot
            var mateSlot = GetOrCreateSlot("Slot_Mate");
            SetSlotLabel(mateSlot, "<sprite name=FS_QueenMate>Mate:");
            if (_unit.m_MateID != -1)
            {
                if (!EnhancedRatMating.isMateAlive(_unit))
                {
                    mateSlot.SlotSet(LocalizationManager.GetTranslation("Word/Undesignated", true, 0, true, false, null, null, true));
                }
                else
                {
                    var mate = GameMgr.Instance._T_UnitMgr.List_Citizen.Find(x => x.m_ID == _unit.m_MateID);
                    var mateGender = (mate.m_Gender == Gender.Male) ? "<sprite name=FS_Male>" : "<sprite name=FS_Female>";
                    mateSlot.SlotSet(mateGender + " " + mate.m_UnitName);
                }
            }
            else
            {
                mateSlot.SlotSet(LocalizationManager.GetTranslation("Word/Undesignated", true, 0, true, false, null, null, true));
            }

            // Child slot
            var childSlot = GetOrCreateSlot("Slot_Child");
            var childIcon = "<sprite name=FS_Children>";
            SetSlotLabel(childSlot, childIcon + "Child:");
            if (_unit.m_ChildID != -1)
            {
                if (!EnhancedRatMating.isChildAlive(_unit))
                {
                    childSlot.SlotSet(LocalizationManager.GetTranslation("Word/Undesignated", true, 0, true, false, null, null, true));
                }
                else
                {
                    var child = GameMgr.Instance._T_UnitMgr.List_Citizen.Find(x => x.m_ID == _unit.m_ChildID);
                    childSlot.SlotSet(childIcon + " " + child.m_UnitName);
                }
            }
            else
            {
                childSlot.SlotSet(LocalizationManager.GetTranslation("Word/Undesignated", true, 0, true, false, null, null, true));
            }

            // Place them directly after m_Slot_Religion
            int religionIndex = slotTemplate.transform.GetSiblingIndex();
            mateSlot.transform.SetSiblingIndex(religionIndex + 1);
            childSlot.transform.SetSiblingIndex(religionIndex + 2);

            // Remove any duplicate mate/child slots
            foreach (Transform child in parent)
            {
                if ((child.name == "Slot_Mate" && child != mateSlot.transform) ||
                    (child.name == "Slot_Child" && child != childSlot.transform))
                {
                    GameObject.Destroy(child.gameObject);
                }
            }
        }
    }
} 