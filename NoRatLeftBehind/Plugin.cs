using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using CasselGames.Audio;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace NoRatLeftBehind
{
    [BepInPlugin("noratleftbehind", "No Rat Left Behind", "1.0.0")]
    public class NoRatLeftBehindPlugin : BaseUnityPlugin
    {
        public static ConfigEntry<float> RescueRadius;

        private void Awake()
        {
            RescueRadius = Config.Bind("General", "RescueRadius", 40f, "Radius within which rats will attempt to rescue injured rats.");
            Harmony.CreateAndPatchAll(typeof(NoRatLeftBehindPatch));
            Logger.LogInfo("No Rat Left Behind plugin loaded!");
        }
    }

    [HarmonyPatch]
    public static class NoRatLeftBehindPatch
    {
        private static readonly Dictionary<T_Citizen, RescueState> RescueStates = new();
        private static readonly HashSet<Building_House> ReservedBeds = new();
        private static readonly HashSet<T_Citizen> BeingRescued = new();

        // For stuck detection
        private static readonly Dictionary<T_Citizen, Vector3> LastPositions = new();
        private static readonly Dictionary<T_Citizen, float> LastMoveTimes = new();
        private static readonly Dictionary<T_Citizen, float> RescuerCooldowns = new();

        private static MethodInfo isNormalIdleMethod;

        [HarmonyPatch(typeof(T_Citizen), "UpdateFunction")]
        [HarmonyPostfix]
        public static void CitizenUpdate_Postfix(T_Citizen __instance)
        {
            try
            {
            if (__instance.m_CharState == CharState.Injury || __instance.m_CharState == CharState.Death)
                return;
                if (RescueStates.ContainsKey(__instance))
                {
                    DoRescue(__instance, RescueStates[__instance]);
                return;
                }
                if (IsBusy(__instance))
                    return;

            var allCitizens = GameMgr.Instance._T_UnitMgr.List_Citizen;
                foreach (var injured in allCitizens)
                {
                    if (injured.m_CharState != CharState.Injury) continue;
                    if (IsBeingRescued(injured)) continue;
                    var rescuer = FindClosestAvailableRescuer(injured, allCitizens);
                    if (rescuer != null)
                    {
                        var bed = FindNearestFreeBed(injured.Tf.position);
                        if (bed == null)
                            continue;
                        ReserveBed(bed);
                        MarkBeingRescued(injured); // Mark immediately to prevent race
                        RescueStates[rescuer] = new RescueState { Target = injured, Bed = bed, Phase = RescuePhase.Scared, ScaredUntil = Time.time + 0.5f };
                        BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogInfo($"[NoRatLeftBehind] {rescuer.m_UnitName} will attempt rescue of {injured.m_UnitName}.");
                        PlayScaredAnimation(rescuer);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogError($"[NoRatLeftBehind] Exception in CitizenUpdate_Postfix: {ex}\n{ex.StackTrace}");
            }
        }

        private static void DoRescue(T_Citizen rescuer, RescueState state)
        {
            var injured = state.Target;
            var bed = state.Bed;
            // Stuck detection
            if (!LastPositions.ContainsKey(rescuer))
            {
                LastPositions[rescuer] = rescuer.Tf.position;
                LastMoveTimes[rescuer] = Time.time;
            }
            else
            {
                if ((rescuer.Tf.position - LastPositions[rescuer]).sqrMagnitude > 0.01f)
                {
                    LastPositions[rescuer] = rescuer.Tf.position;
                    LastMoveTimes[rescuer] = Time.time;
                }
            }
            float stuckTime = Time.time - LastMoveTimes[rescuer];

            switch (state.Phase)
            {
                case RescuePhase.Scared:
                    if (Time.time < state.ScaredUntil)
                        return;
                    state.Phase = RescuePhase.ToInjured;
                    break;
                case RescuePhase.ToInjured:
                    if (injured == null || injured.m_CharState != CharState.Injury)
                    {
                        LogAndAbortRescue(rescuer, state, "Injured rat is gone or not injured");
                        return;
                    }
                    if (rescuer.List_Gathering.Exists(x => x.m_Type == TileType.A_Citizen))
                    {
                        state.Phase = RescuePhase.ToBed;
                        goto case RescuePhase.ToBed;
                    }
                    if (Vector2.Distance(rescuer.Tf.position, injured.Tf.position) > 1.5f)
                    {
                        rescuer.PathFindCall(injured.Tf.position, 0, C_Key.None, false);
                        if (stuckTime > 3f)
                        {
                            LogAndTryAnotherBed(rescuer, state, "Stuck trying to reach injured rat");
                        }
                        return;
                    }
                    GatherCitizen(rescuer, injured, bed);
                    state.Phase = RescuePhase.ToBed;
                    break;
                case RescuePhase.ToBed:
                    if (bed == null || !IsBedReserved(bed))
                    {
                        LogAndAbortRescue(rescuer, state, "Bed is null or not reserved");
                        return;
                    }
                    if (!rescuer.List_Gathering.Exists(x => x.m_Type == TileType.A_Citizen))
                    {
                        LogAndAbortRescue(rescuer, state, "Lost gathered citizen");
                        return;
                    }
                    if (Vector2.Distance(rescuer.Tf.position, bed.Tf.position) > 1.5f)
                    {
                        rescuer.PathFindCall(bed.Tf.position, 0, C_Key.None, false);
                        if (stuckTime > 3f)
                        {
                            LogAndTryAnotherBed(rescuer, state, "Stuck trying to reach bed");
                        }
                        return;
                    }
                    PlaceCitizenInBed(rescuer, bed);
                    FinishRescue(rescuer, state);
                    break;
            }
        }

        private static void PlayScaredAnimation(T_Citizen rescuer)
        {
            try
            {
                var m_ImFatigueField = rescuer.GetType().GetField("m_ImFatigue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                int m_ImFatigue = m_ImFatigueField != null ? (int)m_ImFatigueField.GetValue(rescuer) : 0;
                var isMoveStateMethod = rescuer.GetType().GetMethod("IsMoveState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                bool isMoveState = isMoveStateMethod != null && (bool)isMoveStateMethod.Invoke(rescuer, null);
                if (m_ImFatigue == 0 && !isMoveState)
                {
                    var playAniMethod = rescuer.GetType().GetMethod(
                        "PlayAniOneShot_EndIdle",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                        null,
                        new Type[] { typeof(AniState), typeof(string) },
                        null
                    );
                    playAniMethod?.Invoke(rescuer, new object[] { AniState.Idle, "Motion_Scared_Start" });
                }
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogWarning($"[NoRatLeftBehind] Failed to play scared animation: {ex}");
            }
        }

        private static void LogAndTryAnotherBed(T_Citizen rescuer, RescueState state, string reason)
        {
            BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogWarning($"[NoRatLeftBehind] {rescuer.m_UnitName} rescue stuck: {reason}. Trying another bed.");
            if (state.Bed != null)
                UnreserveBed(state.Bed);
            var newBed = FindNearestFreeBed(rescuer.Tf.position);
            if (newBed != null && newBed != state.Bed)
            {
                ReserveBed(newBed);
                state.Bed = newBed;
                LastMoveTimes[rescuer] = Time.time;
                LastPositions[rescuer] = rescuer.Tf.position;
                BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogInfo($"[NoRatLeftBehind] {rescuer.m_UnitName} switching to new bed at {newBed.Tf.position}");
            }
            else
            {
                LogAndAbortRescue(rescuer, state, "No reachable bed found");
            }
        }

        private static void LogAndAbortRescue(T_Citizen rescuer, RescueState state, string reason)
        {
            BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogWarning($"[NoRatLeftBehind] Aborting rescue for {rescuer.m_UnitName}: {reason}");
            // Drop the citizen if being carried
            if (rescuer.List_Gathering.Exists(x => x.m_Type == TileType.A_Citizen))
            {
                rescuer.List_Gathering.RemoveAll(x => x.m_Type == TileType.A_Citizen);
                var injured = state.Target;
                if (injured != null)
                {
                    injured.gameObject.SetActive(true);
                    injured.Tf.position = rescuer.Tf.position + new Vector3(0.5f, 0, 0);
                }
                rescuer.m_GatherUnitIndex = 0;
            }
            rescuer.List_State.Remove(CitizenState.Carrying);
            rescuer.m_CharState = CharState.None;
            rescuer.m_TargetBuilding = null;
            RescuerCooldowns[rescuer] = Time.time + 10f;
            FinishRescue(rescuer, state);
        }

        private static void GatherCitizen(T_Citizen rescuer, T_Citizen injured, Building_House bed)
        {
            var pool = GameMgr.Instance._PoolMgr.Pool_TileObject;
            var tileObj = pool.GetNextObj(false).GetComponent<TileObject>();
            tileObj.ObjectInit(TileType.A_Citizen, TObjState.Basic, new Vector3(0f, 0f, injured.m_ID), 1, false);
            tileObj.ObjectGathered(rescuer, true);
            injured.gameObject.SetActive(false);
            rescuer.m_GatherUnitIndex = injured.m_ID;
            if (!rescuer.List_State.Contains(CitizenState.Carrying))
                rescuer.List_State.Add(CitizenState.Carrying);
            rescuer.m_CharState = CharState.Carrying;
            rescuer.m_TargetBuilding = bed;
        }

        private static void PlaceCitizenInBed(T_Citizen rescuer, Building_House bed)
        {
            bed.IsGuestUse = false;
            var gatherIdx = rescuer.m_GatherUnitIndex;
            var injured = GameMgr.Instance._T_UnitMgr.FindCitizen(gatherIdx);
            if (injured != null)
            {
                rescuer.List_Gathering.RemoveAll(x => x.m_Type == TileType.A_Citizen);
                injured.gameObject.SetActive(true);
                bed.GuestSet(injured);
                AudioController.PlaySFXOneShot("SFX_Citizen_Inbed_F_Full", GameMgr.Instance._CamMgr.m_MainCam.transform.position, rescuer.Tf.position, 1f, true, false, true, false, null);
            }
            rescuer.m_GatherUnitIndex = 0;
            rescuer.List_State.Remove(CitizenState.Carrying);
            rescuer.m_CharState = CharState.None;
            rescuer.m_TargetBuilding = null;
        }

        private static T_Citizen FindClosestAvailableRescuer(T_Citizen injured, List<T_Citizen> allCitizens)
        {
            T_Citizen closest = null;
            float minDist = float.MaxValue;
            float now = Time.time;
            foreach (var rescuer in allCitizens)
            {
                if (rescuer == injured) continue;
                if (!IsTrulyIdle(rescuer)) continue;
                if (IsBusy(rescuer)) continue;
                if (RescuerCooldowns.TryGetValue(rescuer, out float cooldown) && cooldown > now) continue;
                float dist = Vector2.Distance(rescuer.Tf.position, injured.Tf.position);
                if (dist < minDist && dist <= NoRatLeftBehindPlugin.RescueRadius.Value)
                {
                    minDist = dist;
                    closest = rescuer;
                }
            }
            return closest;
        }

        private static Building_House FindNearestFreeBed(Vector3 fromPos)
        {
            var houses = GameMgr.Instance._BuildingMgr.List_House;
            Building_House nearest = null;
            float minDist = float.MaxValue;
            foreach (var house in houses)
            {
                if (!house.m_BuildInfoUI.IsGuestReady()) continue;
                if (ReservedBeds.Contains(house)) continue;
                float dist = Vector2.Distance(fromPos, house.Tf.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = house;
                }
            }
            return nearest;
        }

        private static void ReserveBed(Building_House bed)
        {
            bed.IsGuestUse = true;
            ReservedBeds.Add(bed);
        }
        private static void UnreserveBed(Building_House bed)
        {
            bed.IsGuestUse = false;
            ReservedBeds.Remove(bed);
        }
        private static bool IsBedReserved(Building_House bed) => ReservedBeds.Contains(bed);

        private static void MarkBeingRescued(T_Citizen injured) => BeingRescued.Add(injured);
        private static void UnmarkBeingRescued(T_Citizen injured) => BeingRescued.Remove(injured);
        private static bool IsBeingRescued(T_Citizen injured) => BeingRescued.Contains(injured);

        private static bool IsBusy(T_Citizen citizen)
        {
            if (RescueStates.ContainsKey(citizen)) return true;
            if (!IsTrulyIdle(citizen)) return true;
            if (citizen.List_Gathering.Exists(x => x.m_Type == TileType.A_Citizen)) return true;
            return false;
        }

        private static bool IsTrulyIdle(T_Citizen citizen)
        {
            if (isNormalIdleMethod == null)
            {
                isNormalIdleMethod = citizen.GetType().GetMethod("IsNormalIdle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (isNormalIdleMethod == null)
                {
                    BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogWarning("[NoRatLeftBehind] Could not find IsNormalIdle method via reflection!");
                    return false;
                }
            }
            try
            {
                return (bool)isNormalIdleMethod.Invoke(citizen, null);
            }
            catch (Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogWarning($"[NoRatLeftBehind] Exception calling IsNormalIdle: {ex}");
                return false;
            }
        }

        private static void FinishRescue(T_Citizen rescuer, RescueState state)
            {
                if (state.Bed != null)
                UnreserveBed(state.Bed);
            if (state.Target != null)
                UnmarkBeingRescued(state.Target);
            RescueStates.Remove(rescuer);
            LastPositions.Remove(rescuer);
            LastMoveTimes.Remove(rescuer);
        }

        private class RescueState
        {
            public T_Citizen Target;
            public Building_House Bed;
            public RescuePhase Phase;
            public float ScaredUntil;
        }
        private enum RescuePhase
        {
            Scared,
            ToInjured,
            ToBed
        }
    }

    [HarmonyPatch(typeof(T_Citizen), "EmployJob_Function")]
    public static class EmployJobFunctionPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(T_Citizen __instance)
        {
            BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogInfo("[NoRatLeftBehind] EmployJob_Function patch called");
            var jobField = __instance.GetType().GetField("m_Job", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (jobField == null || jobField.GetValue(__instance) == null)
            {
                BepInEx.Logging.Logger.CreateLogSource("NoRatLeftBehind").LogWarning("[NoRatLeftBehind] EmployJob_Function: m_Job is null, skipping to prevent crash");
                return false;
            }
            return true;
        }
    }
}