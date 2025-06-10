using System;
using System.Collections.Generic;
using UnityEngine;
using Spine;
using Spine.Unity;

namespace Ratatouille
{
    /// <summary>
    /// Spine/skin/bone utility methods for modders. Wraps common Spine-Unity operations and exposes helpers for overlays, skin management, attachments, and more.
    /// </summary>
    public static class RatatouilleSpineAPI
    {
        /// <summary>
        /// Attaches a GameObject to a Spine bone so it follows the bone's position, rotation, and scale.
        /// </summary>
        /// <param name="target">The GameObject to follow the bone.</param>
        /// <param name="skeletonRenderer">The SkeletonRenderer of the character.</param>
        /// <param name="boneName">The name of the bone to follow.</param>
        /// <param name="followRotation">Whether to follow bone rotation.</param>
        /// <param name="followScale">Whether to follow bone scale.</param>
        /// <param name="followZ">Whether to follow Z position.</param>
        public static void AttachToBone(GameObject target, SkeletonRenderer skeletonRenderer, string boneName, bool followRotation = true, bool followScale = true, bool followZ = false)
        {
            var boneFollower = target.GetComponent<BoneFollower>() ?? target.AddComponent<BoneFollower>();
            boneFollower.skeletonRenderer = skeletonRenderer;
            boneFollower.boneName = boneName;
            boneFollower.followBoneRotation = followRotation;
            boneFollower.followLocalScale = followScale;
            boneFollower.followZPosition = followZ;
            boneFollower.Initialize();
        }

        /// <summary>
        /// Gets all skin names for a given SkeletonDataAsset.
        /// </summary>
        public static List<string> GetSkinNames(SkeletonDataAsset asset)
        {
            var result = new List<string>();
            var data = asset.GetSkeletonData(true);
            foreach (var skin in data.Skins)
                result.Add(skin.Name);
            return result;
        }

        /// <summary>
        /// Sets the skin on a SkeletonRenderer by name.
        /// </summary>
        public static void SetSkin(SkeletonRenderer renderer, string skinName)
        {
            renderer.Skeleton.SetSkin(skinName);
            renderer.Skeleton.SetSlotsToSetupPose();
            renderer.LateUpdate();
        }

        /// <summary>
        /// Creates a new skin by merging multiple skins.
        /// </summary>
        public static Skin MergeSkins(params Skin[] skins)
        {
            if (skins == null || skins.Length == 0) throw new ArgumentException("No skins provided");
            var merged = new Skin("merged");
            foreach (var skin in skins)
                merged.AddSkin(skin);
            return merged;
        }

        /// <summary>
        /// Repack a skin into a new texture/material.
        /// </summary>
        public static Skin RepackSkin(Skin skin, Material material, out Material outputMaterial, out Texture2D outputTexture)
        {
            return Spine.Unity.AttachmentTools.AtlasUtilities.GetRepackedSkin(skin, "repacked", material, out outputMaterial, out outputTexture);
        }

        /// <summary>
        /// Gets an attachment from a skeleton by slot and attachment name.
        /// </summary>
        public static Attachment GetAttachment(Skeleton skeleton, string slotName, string attachmentName)
        {
            int slotIndex = skeleton.Data.FindSlot(slotName).Index;
            return skeleton.GetAttachment(slotIndex, attachmentName);
        }

        /// <summary>
        /// Sets an attachment on a skeleton.
        /// </summary>
        public static void SetAttachment(Skeleton skeleton, string slotName, string attachmentName)
        {
            skeleton.SetAttachment(slotName, attachmentName);
        }

        /// <summary>
        /// Gets the world position of a bone.
        /// </summary>
        public static Vector3 GetBoneWorldPosition(SkeletonRenderer renderer, string boneName)
        {
            var bone = renderer.Skeleton.FindBone(boneName);
            return renderer.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
        }

        /// <summary>
        /// Sets the local position of a bone.
        /// </summary>
        public static void SetBoneLocalPosition(Skeleton skeleton, string boneName, Vector2 position)
        {
            var bone = skeleton.FindBone(boneName);
            bone.X = position.x;
            bone.Y = position.y;
        }

        /// <summary>
        /// Adds a PolygonCollider2D to a slot's bounding box attachment.
        /// </summary>
        public static PolygonCollider2D AddBoundingBoxCollider(Skeleton skeleton, string skinName, string slotName, string attachmentName, Transform parent, bool isTrigger = true)
        {
            return SkeletonUtility.AddBoundingBoxGameObject(skeleton, skinName, slotName, attachmentName, parent, isTrigger);
        }
    }
} 