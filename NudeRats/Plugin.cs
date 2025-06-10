using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System;
using CasselGames.Data;
using BepInExLogger = BepInEx.Logging.Logger;

namespace NudeRats
{
    [BepInPlugin("nuderats", "Nude Rats", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo("NudeRats loaded: All rats will be naked!");

            var harmony = new Harmony("nuderats");
            harmony.PatchAll();
        }
    }

    // Patch the method that updates rat clothes
    [HarmonyPatch(typeof(GameUnit), "ClothesUpdate")]
    public static class Patch_GameUnit_ClothesUpdate
    {
        static void Postfix(GameUnit __instance, int num, bool isCombine)
        {
            if (__instance is T_Citizen citizen)
            {
                var skinInfo = citizen.m_SkinInfo;
                Plugin.Logger.LogInfo($"Citizen name: {citizen.m_UnitName}");
                Plugin.Logger.LogInfo($"Citizen ID: {citizen.m_ID}");
                Plugin.Logger.LogInfo($"Current base skin: {skinInfo.GetSkin("Skin")}");
                // Set the base body skin
                //skinInfo.SetStyle("Skin", "Skin_base");
                // Remove overlays
                // Add any other overlay categories you want to clear
                Plugin.Logger.LogInfo($"Current dress: {skinInfo.GetSkin("Dress")}");
                Plugin.Logger.LogInfo($"Current hat: {skinInfo.GetSkin("Hat")}");
                Plugin.Logger.LogInfo($"Current glasses: {skinInfo.GetSkin("Glasses")}");
                Plugin.Logger.LogInfo($"Current makeup: {skinInfo.GetSkin("Makeup")}");
                Plugin.Logger.LogInfo($"Current bread: {skinInfo.GetSkin("Bread")}");
                Plugin.Logger.LogInfo($"Current hair: {skinInfo.GetSkin("Hair")}");
                Plugin.Logger.LogInfo($"Current cheek: {skinInfo.GetSkin("Cheek")}");
                Plugin.Logger.LogInfo($"Current preset: {skinInfo.GetSkin("Preset")}");

            }
        }
    }

    public static class Patch_T_Citizen_LoadSetting
    {
        static void Postfix(T_Citizen __instance, Citizen_Data _data)
        {
            // Null checks to avoid errors during loading
            if (__instance == null || __instance.m_SkinInfo == null)
                return;

            var skinInfo = __instance.m_SkinInfo;

            // Optional: check for valid name/ID before logging
            string name = __instance.m_UnitName ?? "(null)";
            string id = __instance.m_ID.ToString();

            Plugin.Logger.LogInfo($"[LoadSetting] Citizen name: {name}, ID: {id}");

            // Set the base body skin
            //skinInfo.SetStyle("Skin", "Skin_base");
            // Remove overlays
            // Add any other overlay categories you want to clear

            Plugin.Logger.LogInfo($"[LoadSetting] Current dress: {skinInfo.GetSkin("Dress")}");
            Plugin.Logger.LogInfo($"[LoadSetting] Current hat: {skinInfo.GetSkin("Hat")}");
            Plugin.Logger.LogInfo($"[LoadSetting] Current glasses: {skinInfo.GetSkin("Glasses")}");
            Plugin.Logger.LogInfo($"[LoadSetting] Current makeup: {skinInfo.GetSkin("Makeup")}");
            Plugin.Logger.LogInfo($"[LoadSetting] Current bread: {skinInfo.GetSkin("Bread")}");
            Plugin.Logger.LogInfo($"[LoadSetting] Current hair: {skinInfo.GetSkin("Hair")}");
            Plugin.Logger.LogInfo($"[LoadSetting] Current cheek: {skinInfo.GetSkin("Cheek")}");
            Plugin.Logger.LogInfo($"[LoadSetting] Current preset: {skinInfo.GetSkin("Preset")}");

        }
    }
}