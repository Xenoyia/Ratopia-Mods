using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.IO;
using Spine.Unity;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

using System.Linq;

namespace RaiseThoseTails
{
    [BepInPlugin("raisethosetails", "Raise Those Tails", "1.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        internal static new BepInEx.Logging.ManualLogSource Logger;
        private static readonly Vector3 OverlayOffset = new Vector3(0.3f, 0.3f, 0.0f);
        private static readonly Vector3 OverlayScale = new Vector3(1f, 1f, 1f);
        private static string overlayPath;

        private void Awake()
        {
            Logger = base.Logger;
            overlayPath = Path.Combine(Paths.PluginPath, "RaiseThoseTails", "tail.png");
            Harmony.CreateAndPatchAll(typeof(Plugin));
            Logger.LogInfo($"Raise Those Tails by Xenoyia loaded. Tails are being raised!");
        }

        public static void AddOrUpdateOverlay(GameUnit citizen)
        {
            try
            {
                if (citizen == null) return;

                // Use reflection to get m_SkinInfo, m_MeshRender, and transform if not public
                var skinInfoField = citizen.GetType().GetField("m_SkinInfo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var meshRenderField = citizen.GetType().GetField("m_MeshRender", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var transformProp = citizen.GetType().GetProperty("transform", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                var m_SkinInfo = skinInfoField?.GetValue(citizen);
                var m_MeshRender = meshRenderField?.GetValue(citizen) as Component;
                var unitTransform = transformProp != null ? transformProp.GetValue(citizen, null) as Transform : (citizen as Component)?.transform;

                if (m_SkinInfo == null || m_MeshRender == null || unitTransform == null)
                {
                    return;
                }

                // Remove existing overlay if present
                var existing = (unitTransform as Transform)?.Find("RaiseThoseTailsOverlay");
                if (existing != null)
                    GameObject.Destroy(existing.gameObject);

                string pathToUse = overlayPath;
                if (citizen.GetType() == typeof(GBot))
                {
                    // Use reflection to get m_SkinInfo and m_typeStyleSkin
                    var typeStyleSkinField = m_SkinInfo.GetType().GetField("m_typeStyleSkin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var typeStyleSkin = typeStyleSkinField != null ? typeStyleSkinField.GetValue(m_SkinInfo) : null;
                    string combatRatronName = "CombatRatron";
                    if (typeStyleSkin != null && typeStyleSkin.ToString() == combatRatronName)
                        pathToUse = Path.Combine(Paths.PluginPath, "RaiseThoseTails", "robotail_red.png");
                    else
                        pathToUse = Path.Combine(Paths.PluginPath, "RaiseThoseTails", "robotail.png");
                }
                byte[] imageData = File.ReadAllBytes(pathToUse);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(imageData);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                Sprite overlaySprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

                // Create a parent GameObject for BoneFollower
                GameObject overlayParent = new GameObject("RaiseThoseTailsOverlay");
                overlayParent.transform.SetParent(m_MeshRender.transform.parent, false);
                overlayParent.layer = (unitTransform as Transform).gameObject.layer;

                // Add BoneFollower for perfect bone following
                var skeletonRenderer = (unitTransform as Transform).GetComponentInChildren<SkeletonRenderer>();
                if (skeletonRenderer != null)
                {
                    var boneFollower = overlayParent.AddComponent<BoneFollower>();
                    boneFollower.skeletonRenderer = skeletonRenderer;
                    boneFollower.boneName = "Body";
                    boneFollower.followZPosition = false;
                    boneFollower.followBoneRotation = true;
                    boneFollower.followLocalScale = true;
                }
                else
                {
                    Logger.LogWarning("No SkeletonRenderer found on unit for BoneFollower.");
                }

                // Create the child for the overlay sprite and apply the offset
                GameObject overlayObj = new GameObject("RaiseThoseTailsOverlaySprite");
                overlayObj.transform.SetParent(overlayParent.transform, false);
                overlayObj.transform.localPosition = OverlayOffset;
                overlayObj.transform.localScale = OverlayScale;
                overlayObj.layer = (unitTransform as Transform).gameObject.layer;

                var sr = overlayObj.AddComponent<SpriteRenderer>();
                sr.sprite = overlaySprite;
                // After adding the SpriteRenderer (sr)
                // Instead, ensure the overlay Z is always just above the character
                overlayObj.AddComponent<OverlayZFollower>().Init(unitTransform as Transform);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Raise Those Tails: Failed to add overlay: {ex}");
            }
        }

        [HarmonyPatch(typeof(T_Citizen), "CitizenInit")]
        [HarmonyPostfix]
        public static void Patch_CitizenInit(T_Citizen __instance) => AddOrUpdateOverlay(__instance);

        [HarmonyPatch(typeof(T_Citizen), "LoadSetting")]
        [HarmonyPostfix]
        public static void Patch_LoadSetting(T_Citizen __instance, Citizen_Data _data) => AddOrUpdateOverlay(__instance);

        // Add support for T_Queen
        [HarmonyPatch(typeof(T_Queen), "Init")]
        [HarmonyPostfix]
        public static void Patch_QueenInit(T_Queen __instance) => AddOrUpdateOverlay(__instance);

        [HarmonyPatch(typeof(T_Queen), "LoadSetting")]
        [HarmonyPostfix]
        public static void Patch_QueenLoadSetting(T_Queen __instance, Citizen_Data _data) => AddOrUpdateOverlay(__instance);
    }

    class OverlayZFollower : MonoBehaviour {
        private Transform target;
        private float zOffset = 0.0001f;
        public void Init(Transform followTarget) { target = followTarget; UpdateZ(); }
        void LateUpdate() { UpdateZ(); }
        void UpdateZ() {
            if (target != null) {
                var pos = transform.position;
                pos.z = target.position.z + zOffset;
                transform.position = pos;
            }
        }
    }
} 