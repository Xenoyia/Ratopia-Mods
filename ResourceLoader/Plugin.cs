using BepInEx;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;

[BepInPlugin("resourceloader", "Resource Loader", "1.0.0")]
public class ResourceLoaderPlugin : BaseUnityPlugin
{
    public static ResourceLoaderPlugin Instance;

    // Static logger for use in static methods
    public static ManualLogSource StaticLogger;

    // Optional: Expose loaded resources to other plugins
    public static Dictionary<string, UnityEngine.Object> LoadedOverrides = new Dictionary<string, UnityEngine.Object>();

    // Static flag to prevent recursion in Harmony patch
    public static bool SkipPatch = false;

    private void Awake()
    {
        Instance = this;
        StaticLogger = Logger;
        // Initialize Harmony and apply patch
        var harmony = new Harmony("resourceloader.harmony");
        harmony.PatchAll();
    }

    public static UnityEngine.Object LoadOverride(string path, System.Type type)
    {
        string overrideRoot = Path.Combine(Paths.PluginPath, "Resources");
        string relPath = path.Replace('\\', '/');
        string[] possibleExts = { ".png", ".jpg", ".jpeg", ".txt", ".bytes", ".asset" };

        foreach (var ext in possibleExts)
        {
            string overrideFile = Path.Combine(overrideRoot, relPath + ext);
            if (File.Exists(overrideFile))
            {
                // Sprite/Texture2D
                if (type == typeof(Sprite) || type == typeof(Texture2D))
                {
                    byte[] data = File.ReadAllBytes(overrideFile);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    tex.LoadImage(data);
                    tex.name = Path.GetFileNameWithoutExtension(overrideFile);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;

                    if (type == typeof(Sprite))
                    {
                        // Try to load the original sprite (skip patch to avoid recursion)
                        ResourceLoaderPlugin.SkipPatch = true;
                        var originalObj = Resources.Load(path, typeof(Sprite));
                        ResourceLoaderPlugin.SkipPatch = false;

                        Vector2 pivot = new Vector2(0, 0);
                        float pixelsPerUnit = 100f;
                        Rect rect = new Rect(0, 0, tex.width, tex.height);
                        Vector4 border = Vector4.zero;

                        if (originalObj is Sprite originalSprite)
                        {
                            // Copy settings from the original sprite
                            pivot = new Vector2(
                                originalSprite.pivot.x / originalSprite.rect.width,
                                originalSprite.pivot.y / originalSprite.rect.height
                            );
                            pixelsPerUnit = originalSprite.pixelsPerUnit;
                            rect = new Rect(0, 0, tex.width, tex.height); // Usually the same, but you could match originalSprite.rect if needed
                            border = originalSprite.border;
                        }
                        else if (path.IndexOf("blueprint", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            pivot = new Vector2(0.5f, 0f); // fallback for blueprints
                        }

                        var sprite = Sprite.Create(
                            tex,
                            rect,
                            pivot,
                            pixelsPerUnit,
                            0,
                            SpriteMeshType.Tight,
                            border
                        );
                        sprite.name = tex.name;
                        LoadedOverrides[path] = sprite;

                        // Special logic for Building_ resources
                        if (path.Contains("Building_"))
                        {
                            try
                            {
                                string buildingName = null;
                                int idx = path.IndexOf("Building_") + "Building_".Length;
                                if (idx < path.Length)
                                    buildingName = path.Substring(idx);

                                foreach (var body in UnityEngine.Object.FindObjectsOfType<UnityEngine.MonoBehaviour>())
                                {
                                    var bodyType = body.GetType();
                                    if (bodyType.Name == "BuildingBody")
                                    {
                                        var go = (body as MonoBehaviour).gameObject;
                                        if (go != null && buildingName != null && go.name == buildingName)
                                        {
                                            var m_BuildPartsField = bodyType.GetField("m_BuildParts");
                                            if (m_BuildPartsField != null)
                                            {
                                                var buildParts = m_BuildPartsField.GetValue(body) as Array;
                                                if (buildParts != null && buildParts.Length > 0)
                                                {
                                                    // Only keep the first BuildPart
                                                    var firstPart = buildParts.GetValue(0);
                                                    var buildPartsType = buildParts.GetType();
                                                    var newBuildParts = Array.CreateInstance(buildPartsType.GetElementType(), 1);
                                                    newBuildParts.SetValue(firstPart, 0);
                                                    m_BuildPartsField.SetValue(body, newBuildParts);

                                                    // Set transform.localPosition and rotation
                                                    var partMB = firstPart as MonoBehaviour;
                                                    if (partMB != null)
                                                    {
                                                        var t = partMB.transform;
                                                        t.localPosition = Vector3.zero;
                                                        t.localRotation = Quaternion.identity;
                                                    }

                                                    var sprField = firstPart.GetType().GetField("m_Spr");
                                                    var renderField = firstPart.GetType().GetField("m_Render");
                                                    if (sprField != null)
                                                    {
                                                        var sprArr = sprField.GetValue(firstPart) as Sprite[];
                                                        if (sprArr != null)
                                                        {
                                                            for (int i = 0; i < sprArr.Length; i++)
                                                            {
                                                                var customSprite = Sprite.Create(
                                                                    sprite.texture,
                                                                    new Rect(0, 0, sprite.texture.width, sprite.texture.height),
                                                                    new Vector2(0.5f, 0f), // bottom center for constructed buildings
                                                                    sprite.pixelsPerUnit,
                                                                    0,
                                                                    SpriteMeshType.Tight,
                                                                    sprite.border
                                                                );
                                                                customSprite.name = sprite.name;
                                                                sprArr[i] = customSprite;
                                                                if (renderField != null)
                                                                {
                                                                    var renderer = renderField.GetValue(firstPart) as SpriteRenderer;
                                                                    if (renderer != null)
                                                                        renderer.sprite = customSprite;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                StaticLogger?.LogError($"[ResourceLoader] Error applying global Building_ patch: {ex}");
                            }
                        }

                        return sprite;
                    }
                    else
                    {
                        LoadedOverrides[path] = tex;
                        return tex;
                    }
                }
                // TextAsset
                else if (type == typeof(TextAsset))
                {
                    byte[] data = File.ReadAllBytes(overrideFile);
                    var textAsset = new TextAsset(System.Text.Encoding.UTF8.GetString(data));
                    LoadedOverrides[path] = textAsset;
                    return textAsset;
                }
                // Fallback: load as bytes and cache
                else
                {
                    byte[] data = File.ReadAllBytes(overrideFile);
                    LoadedOverrides[path] = null; // Can't return byte[] as Object
                    return null;
                }
            }
        }
        // Fallback: call original Resources.Load, skipping the patch
        SkipPatch = true;
        var result = Resources.Load(path, type);
        SkipPatch = false;
        return result;
    }
}

// Harmony patch class
[HarmonyPatch(typeof(Resources), nameof(Resources.Load), new Type[] { typeof(string), typeof(Type) })]
public static class ResourcesLoadPatch
{
    static bool Prefix(string path, Type systemTypeInstance, ref UnityEngine.Object __result)
    {
        if (ResourceLoaderPlugin.SkipPatch)
        {
            return true; // Don't intercept, call original
        }

        var overrideObj = ResourceLoaderPlugin.LoadOverride(path, systemTypeInstance);
        if (overrideObj != null)
        {
            __result = overrideObj;
            return false; // Skip original
        }
        return true; // Continue to original
    }
}

[HarmonyPatch(typeof(Building), "BuildingSet", new Type[] { typeof(BuildInfo), typeof(Vector2), typeof(int) })]
public static class BuildingSetPatch
{
    static void Postfix(Building __instance)
    {
        // Get the BuildingBody
        var body = __instance.m_Body;
        if (body == null) return;

        // Get the building name (GameObject name)
        string buildingName = body.gameObject.name;

        // Only patch if we have a custom sprite for this building
        string overrideKey = "GameScene/Map/Building/Building_" + buildingName;
        if (!ResourceLoaderPlugin.LoadedOverrides.TryGetValue(overrideKey, out var obj) || !(obj is Sprite sprite))
            return;

        // Collapse m_BuildParts to one part
        var m_BuildPartsField = body.GetType().GetField("m_BuildParts");
        if (m_BuildPartsField != null)
        {
            var buildParts = m_BuildPartsField.GetValue(body) as Array;
            if (buildParts != null && buildParts.Length > 0)
            {
                var firstPart = buildParts.GetValue(0);
                var buildPartsType = buildParts.GetType();
                var newBuildParts = Array.CreateInstance(buildPartsType.GetElementType(), 1);
                newBuildParts.SetValue(firstPart, 0);
                m_BuildPartsField.SetValue(body, newBuildParts);

                // Set transform.localPosition and rotation
                var partMB = firstPart as MonoBehaviour;
                if (partMB != null)
                {
                    var t = partMB.transform;
                    t.localPosition = Vector3.zero;
                    t.localRotation = Quaternion.identity;
                }

                // Assign the custom sprite with bottom center pivot
                var sprField = firstPart.GetType().GetField("m_Spr");
                var renderField = firstPart.GetType().GetField("m_Render");
                if (sprField != null)
                {
                    var sprArr = sprField.GetValue(firstPart) as Sprite[];
                    if (sprArr != null)
                    {
                        for (int i = 0; i < sprArr.Length; i++)
                        {
                            var customSprite = Sprite.Create(
                                sprite.texture,
                                new Rect(0, 0, sprite.texture.width, sprite.texture.height),
                                new Vector2(0.5f, 0f), // bottom center
                                sprite.pixelsPerUnit,
                                0,
                                SpriteMeshType.Tight,
                                sprite.border
                            );
                            customSprite.name = sprite.name;
                            sprArr[i] = customSprite;
                            if (renderField != null)
                            {
                                var renderer = renderField.GetValue(firstPart) as SpriteRenderer;
                                if (renderer != null)
                                    renderer.sprite = customSprite;
                            }
                        }
                    }
                }
            }
        }
    }
}