using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// This file is part of the Ratatouille library plugin for BepInEx mods.
// It exposes utility functions for other mods to interact with the game.

namespace Ratatouille
{
    public static class RatatouilleAPI
    {
        #region Item Methods
        /// <summary>
        /// Spawns an item (TileObject) at the given position with the specified quantity.
        /// </summary>
        /// <param name="type">The TileType of the item to spawn.</param>
        /// <param name="position">The world position to spawn at.</param>
        /// <param name="quantity">How many items to spawn.</param>
        /// <param name="state">The TObjState for the spawned item (default: MakeProduct).</param>
        /// <returns>The spawned TileObject, or null if failed.</returns>
        public static object SpawnItem(object type, Vector3 position, int quantity = 1, object state = null)
        {
            // Defensive: Check for GameMgr, PoolMgr, etc.
            if (GameMgr.Instance == null || GameMgr.Instance._PoolMgr == null)
                throw new System.Exception("GameMgr or PoolMgr not initialized!");

            // Default state to TObjState.MakeProduct if not provided
            if (state == null)
            {
                state = TObjState.MakeProduct;
            }

            // Pool_TileObject is a MemoryPool, GetNextObj returns a GameObject
            var obj = GameMgr.Instance._PoolMgr.Pool_TileObject.GetNextObj();
            var tileObj = obj.GetComponent<TileObject>();
            if (tileObj == null)
                throw new System.Exception("TileObject component missing from pooled object!");

            // Call ObjectInit (TileType, TObjState, Vector3, int, bool)
            tileObj.ObjectInit((TileType)type, (TObjState)state, position, quantity, false);
            return tileObj;
        }

        /// <summary>
        /// Drops an item at a citizen's current position.
        /// </summary>
        public static void SpawnItemAtCitizen(T_Citizen citizen, TileType itemType, int quantity = 1)
        {
            if (citizen == null) return;
            SpawnItem(itemType, citizen.transform.position, quantity);
        }

        /// <summary>
        /// Spawns an item at a specific node.
        /// </summary>
        public static object SpawnItemAtNode(TileType type, Node node, int quantity = 1, TObjState state = TObjState.MakeProduct)
        {
            if (node == null) return null;
            return SpawnItem(type, node.GetPos(), quantity, state);
        }
        #endregion

        #region Buff Methods
        /// <summary>
        /// Checks if a citizen currently has a buff by reference name.
        /// </summary>
        public static bool CitizenHasBuff(T_Citizen citizen, string buffName)
        {
            if (citizen == null) return false;
            return citizen.m_Buff.IsExistRef(buffName);
        }

        /// <summary>
        /// Gives a buff to a citizen by buff type, reference name, value, and duration.
        /// </summary>
        public static void GiveBuffToCitizen(T_Citizen citizen, string buffName, float value, int hours, C_Buff buffType = C_Buff.HappyUp, C_Buff_Category category = C_Buff_Category.Character)
        {
            if (citizen == null) return;
            citizen.m_Buff.BuffRefSet(buffType, buffName, category, value, hours);
        }

        /// <summary>
        /// Removes a buff from a citizen by reference name.
        /// </summary>
        public static void RemoveBuffFromCitizen(T_Citizen citizen, string buffName)
        {
            if (citizen == null) return;
            citizen.m_Buff.RefKill(buffName);
        }

        /// <summary>
        /// Returns all citizens who currently have a specific buff.
        /// </summary>
        public static List<T_Citizen> GetCitizensWithBuff(string buffName)
        {
            return GetAllCitizens().Where(c => CitizenHasBuff(c, buffName)).ToList();
        }

        /// <summary>
        /// Returns a list of all buff reference names currently on a citizen.
        /// </summary>
        public static List<string> GetBuffsOnCitizen(T_Citizen citizen)
        {
            if (citizen == null || citizen.m_Buff == null) return new List<string>();
            return citizen.m_Buff.List_BuffIcon.Select(b => b.ReferenceName).Where(n => !string.IsNullOrEmpty(n)).ToList();
        }

        /// <summary>
        /// Removes a specific buff from all citizens.
        /// </summary>
        public static void RemoveBuffFromAllCitizens(string buffName)
        {
            foreach (var citizen in GetAllCitizens())
            {
                RemoveBuffFromCitizen(citizen, buffName);
            }
        }

        /// <summary>
        /// Removes all buffs from a citizen.
        /// </summary>
        public static void RemoveAllBuffsFromCitizen(T_Citizen citizen)
        {
            if (citizen?.m_Buff?.List_BuffIcon == null) return;
            var buffs = citizen.m_Buff.List_BuffIcon.Select(b => b.ReferenceName).Where(n => !string.IsNullOrEmpty(n)).ToList();
            foreach (var buff in buffs)
                RemoveBuffFromCitizen(citizen, buff);
        }

        /// <summary>
        /// Removes all buffs from all citizens.
        /// </summary>
        public static void RemoveAllBuffsFromAllCitizens()
        {
            foreach (var citizen in GetAllCitizens())
                RemoveAllBuffsFromCitizen(citizen);
        }
        #endregion

        #region Citizen Methods
        /// <summary>
        /// Finds all citizens within a radius of a given world position.
        /// </summary>
        public static List<T_Citizen> FindCitizensInRadius(Vector3 center, float radius)
        {
            var citizens = new List<T_Citizen>();
            if (GameMgr.Instance?._T_UnitMgr?.List_Citizen == null) return citizens;
            foreach (var citizen in GameMgr.Instance._T_UnitMgr.List_Citizen)
            {
                if (citizen != null && Vector3.Distance(citizen.transform.position, center) <= radius)
                    citizens.Add(citizen);
            }
            return citizens;
        }

        /// <summary>
        /// Finds the closest citizen to a given world position.
        /// </summary>
        public static T_Citizen GetClosestCitizen(Vector3 position)
        {
            return GetAllCitizens()
                .OrderBy(c => Vector3.Distance(c.transform.position, position))
                .FirstOrDefault();
        }

        /// <summary>
        /// Checks if a citizen is within a certain distance of a position.
        /// </summary>
        public static bool IsCitizenNearPosition(T_Citizen citizen, Vector3 position, float radius)
        {
            if (citizen == null) return false;
            return Vector3.Distance(citizen.transform.position, position) <= radius;
        }

        /// <summary>
        /// Returns all citizens currently at a specific node.
        /// </summary>
        public static List<T_Citizen> GetCitizensAtNode(Node node)
        {
            if (node == null) return new List<T_Citizen>();
            return GetAllCitizens().Where(c => c.m_CurNode == node).ToList();
        }

        /// <summary>
        /// Finds a citizen by their in-game name.
        /// </summary>
        public static T_Citizen FindCitizenByName(string name)
        {
            if (GameMgr.Instance?._T_UnitMgr?.List_Citizen == null) return null;
            return GameMgr.Instance._T_UnitMgr.List_Citizen.FirstOrDefault(c => c != null && c.m_UnitName == name);
        }

        /// <summary>
        /// Returns a list of all citizens in the game.
        /// </summary>
        public static List<T_Citizen> GetAllCitizens()
        {
            if (GameMgr.Instance?._T_UnitMgr?.List_Citizen == null) return new List<T_Citizen>();
            return GameMgr.Instance._T_UnitMgr.List_Citizen.Where(c => c != null).ToList();
        }

        /// <summary>
        /// Returns all citizens who have a specific trait index.
        /// </summary>
        public static List<T_Citizen> GetCitizensWithTrait(int traitIndex)
        {
            return GetAllCitizens().Where(c => c.List_CharInfo != null && c.List_CharInfo.Contains(traitIndex)).ToList();
        }
        #endregion

        #region World/Node Methods
        /// <summary>
        /// Gets the tile node at a given world position.
        /// </summary>
        public static Node GetNodeAtPosition(Vector3 position)
        {
            return GameMgr.Instance?._TileMgr?.GetNodeByLimit(position);
        }

        /// <summary>
        /// Removes all items at a specific node (if supported by the game).
        /// </summary>
        public static void RemoveAllItemsAtNode(Node node)
        {
            if (node?.m_WorldObj != null)
            {
                // Example: Remove all items logic, adapt as needed
                node.m_WorldObj.RemoveAllItems();
            }
        }
        #endregion

        #region Logging
        /// <summary>
        /// Logs a message with a [Ratatouille] prefix.
        /// </summary>
        public static void Log(string message)
        {
            Debug.Log($"[Ratatouille] {message}");
        }
        #endregion

        #region Trait/Ability Methods
        /// <summary>
        /// Returns all CharacterInfo traits (abilities) currently on a citizen.
        /// </summary>
        public static List<CharacterInfo> GetTraitsOnCitizen(T_Citizen citizen)
        {
            if (citizen == null || citizen.List_CharInfo == null)
                return new List<CharacterInfo>();
            var traits = new List<CharacterInfo>();
            foreach (var idx in citizen.List_CharInfo)
            {
                var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(idx);
                if (trait != null)
                    traits.Add(trait);
            }
            return traits;
        }

        /// <summary>
        /// Replaces the citizen's trait of the given category (0 or 1) with the specified trait index, and applies its buffs.
        /// </summary>
        public static void SetTrait(T_Citizen citizen, int traitIndex, int category)
        {
            if (citizen == null) return;
            // Remove existing trait of this category
            for (int i = 0; i < citizen.List_CharInfo.Count; i++)
            {
                var trait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(citizen.List_CharInfo[i]);
                if (trait != null && trait.Category == category)
                {
                    citizen.List_CharInfo.RemoveAt(i);
                    citizen.List_CharInfoValue.RemoveAt(i);
                    break;
                }
            }
            // Add new trait
            var newTrait = GameMgr.Instance._DB_Mgr.GetCharacterInfo(traitIndex);
            if (newTrait != null && newTrait.Category == category)
            {
                citizen.List_CharInfo.Add(traitIndex);
                citizen.List_CharInfoValue.Add(newTrait);
                // Remove all character buffs (to avoid stacking)
                citizen.m_Buff.CategoryKill(C_Buff_Category.Character);
                // Apply all buffs from all traits
                foreach (var t in citizen.List_CharInfoValue)
                {
                    var buffs = GetBuffsFromTrait(t);
                    foreach (var (buff, value) in buffs)
                    {
                        GiveBuffToCitizen(citizen, t.Name, value, -999);
                    }
                }
            }
        }

        /// <summary>
        /// Finds a trait (CharacterInfo) by its name.
        /// </summary>
        public static CharacterInfo FindTraitByName(string name)
        {
            return GameMgr.Instance._DB_Mgr.List_Char1_DB.Concat(GameMgr.Instance._DB_Mgr.List_Char2_DB)
                .FirstOrDefault(t => t.Name == name);
        }

        /// <summary>
        /// Returns all traits (CharacterInfo) of a given category (0 or 1).
        /// </summary>
        public static List<CharacterInfo> GetAllTraitsOfCategory(int category)
        {
            return GameMgr.Instance._DB_Mgr.List_Char1_DB.Concat(GameMgr.Instance._DB_Mgr.List_Char2_DB)
                .Where(t => t.Category == category).ToList();
        }
        #endregion

        #region Lifecycle Hooks
        /// <summary>
        /// Invoked when the game first opens to the main menu (after Unity scene load, before any save is loaded).
        /// Modders can subscribe to this event to run code at game startup.
        /// </summary>
        public static event System.Action OnStart;
        /// <summary>
        /// Invoked when a save is loaded (before the game is fully running).
        /// Modders can subscribe to this event to run code after a save is loaded.
        /// </summary>
        public static event System.Action OnLoad;
        /// <summary>
        /// Invoked when the game is fully loaded and the player can interact.
        /// Modders can subscribe to this event to run code after the game has started.
        /// </summary>
        public static event System.Action OnGameStart;

        // Internal methods for the API to invoke these events (not for modders)
        internal static void InvokeOnStart() => OnStart?.Invoke();
        internal static void InvokeOnLoad() => OnLoad?.Invoke();
        internal static void InvokeOnGameStart() => OnGameStart?.Invoke();
        #endregion
    } 
}