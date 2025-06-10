using HarmonyLib;
using System.Reflection;

namespace Ratatouille
{
    /// <summary>
    /// Applies Harmony patches to hook into key game lifecycle events for the Ratatouille modding API.
    /// </summary>
    public static class RatatouilleBootstrap
    {
        private static bool _initialized = false;

        /// <summary>
        /// Call this once (e.g. from your plugin's Awake) to apply the lifecycle patches.
        /// </summary>
        public static void Init()
        {
            if (_initialized) return;
            var harmony = new Harmony("ratatouille.lifecycle");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            _initialized = true;
        }

        // --- Patch: OnStart (MainUI.Start) ---
        [HarmonyPatch("CasselGames.UI.MainUI", "Start")]
        private static class Patch_MainUI_Start
        {
            static void Postfix()
            {
                RatatouilleAPI.InvokeOnStart();
            }
        }

        // --- Patch: OnLoad (TitleUI.OnLoadEvent) ---
        [HarmonyPatch("CasselGames.UI.TitleUI", "OnLoadEvent")]
        private static class Patch_TitleUI_OnLoadEvent
        {
            static void Postfix()
            {
                RatatouilleAPI.InvokeOnLoad();
            }
        }

        // --- Patch: OnGameStart (TitleUI.OnStartEvent) ---
        [HarmonyPatch("CasselGames.UI.TitleUI", "OnStartEvent")]
        private static class Patch_TitleUI_OnStartEvent
        {
            static void Postfix()
            {
                RatatouilleAPI.InvokeOnGameStart();
            }
        }
    }
} 