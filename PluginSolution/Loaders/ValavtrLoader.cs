﻿using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using UnityEngine;

namespace ValheimPlayerModels.Loaders
{
    public class ValavtrLoader : AvatarLoaderBase
    {
        private AssetBundle avatarBundle;

        public override IEnumerator LoadFile(string file)
        {
            AssetBundleCreateRequest bundleRequest =
                AssetBundle.LoadFromFileAsync(file);
            yield return bundleRequest;

            avatarBundle = bundleRequest.assetBundle;
            if (!avatarBundle)
            {
                Plugin.Log.LogError("Avatar Bundle " + file + " couldn't load!");
                LoadedSuccessfully = false;
                yield break;
            }

            LoadedSuccessfully = true;
        }

        public override AvatarInstance LoadAvatar(PlayerModel playerModel)
        {
            AvatarInstance avatarInstance = new AvatarInstance(playerModel);

            GameObject avatarAsset = avatarBundle.LoadAsset<GameObject>("_avatar");
            if (!avatarAsset)
            {
                Plugin.Log.LogError("Couldn't find avatar prefab");
                return null;
            }

            avatarInstance.AvatarObject = Object.Instantiate(avatarAsset);
            avatarInstance.AvatarDescriptor = avatarInstance.AvatarObject.GetComponent<ValheimAvatarDescriptor>();
            avatarInstance.Animator = avatarInstance.AvatarObject.GetComponent<Animator>();

            avatarInstance.Transform = avatarInstance.AvatarObject.transform;
            avatarInstance.Transform.SetParent(playerModel.transform, false);

            avatarInstance.LeftFoot = avatarInstance.Animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            avatarInstance.RightFoot = avatarInstance.Animator.GetBoneTransform(HumanBodyBones.RightFoot);
            avatarInstance.Hips = avatarInstance.Animator.GetBoneTransform(HumanBodyBones.Hips);


            avatarInstance.lodGroup = avatarInstance.AvatarObject.AddComponent<LODGroup>();
            var lodMeshes = new LOD(0.1f, avatarInstance.AvatarObject.GetComponentsInChildren<SkinnedMeshRenderer>());
            avatarInstance.lodGroup.SetLODs(new LOD[] { lodMeshes });
            avatarInstance.lodGroup.RecalculateBounds();

            var playerLodGroup = playerModel.player.GetVisual().GetComponent<LODGroup>();
            avatarInstance.lodGroup.fadeMode = playerLodGroup.fadeMode;
            avatarInstance.lodGroup.animateCrossFading = playerLodGroup.animateCrossFading;

            #region Convert Material Shaders

            Renderer[] renderers = avatarInstance.AvatarObject.GetComponentsInChildren<Renderer>();
            var playerShader = Shader.Find("Custom/Player") ?? ((Shader[])Resources.FindObjectsOfTypeAll(typeof(Shader))).FirstOrDefault(x => x.name == "Custom/Player");
            if (playerShader == null)
            {
                Plugin.Log.LogError("Couldn't find Custom/Player shader!");
            }
            foreach (Renderer renderer in renderers)
            {
                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (mat && mat.shader.name == "Valheim/Standard")
                    {
                        mat.shader = playerShader;

                        var mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") as Texture2D : null;
                        var bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;

                        mat.SetTexture("_MainTex", mainTex);
                        mat.SetTexture("_SkinBumpMap", bumpMap);
                        mat.SetTexture("_ChestTex", mainTex);
                        mat.SetTexture("_ChestBumpMap", bumpMap);
                        mat.SetTexture("_LegsTex", mainTex);
                        mat.SetTexture("_LegsBumpMap", bumpMap);
                    }
                }
            }

            #endregion

            #region Import Parameters

            avatarInstance.AvatarDescriptor.Validate();

            avatarInstance.Parameters = new Dictionary<int, AvatarInstance.AvatarParameter>();

            if (avatarInstance.AvatarDescriptor.boolParameters != null)
            {
                for (int i = 0; i < avatarInstance.AvatarDescriptor.boolParameters.Count; i++)
                {
                    int hash = Animator.StringToHash(avatarInstance.AvatarDescriptor.boolParameters[i]);
                    if (!avatarInstance.Parameters.ContainsKey(hash))
                    {
                        avatarInstance.Parameters.Add(hash, new AvatarInstance.AvatarParameter { type = AvatarInstance.ParameterType.Bool, boolValue = avatarInstance.AvatarDescriptor.boolParametersDefault[i] });
                        avatarInstance.Animator.SetBool(hash, avatarInstance.AvatarDescriptor.boolParametersDefault[i]);
                    }
                }
            }

            if (avatarInstance.AvatarDescriptor.intParameters != null)
            {
                for (int i = 0; i < avatarInstance.AvatarDescriptor.intParameters.Count; i++)
                {
                    int hash = Animator.StringToHash(avatarInstance.AvatarDescriptor.intParameters[i]);
                    if (!avatarInstance.Parameters.ContainsKey(hash))
                    {
                        avatarInstance.Parameters.Add(hash, new AvatarInstance.AvatarParameter { type = AvatarInstance.ParameterType.Int, intValue = avatarInstance.AvatarDescriptor.intParametersDefault[i] });
                        avatarInstance.Animator.SetInteger(hash, avatarInstance.AvatarDescriptor.intParametersDefault[i]);
                    }
                }
            }

            if (avatarInstance.AvatarDescriptor.floatParameters != null)
            {
                for (int i = 0; i < avatarInstance.AvatarDescriptor.floatParameters.Count; i++)
                {
                    int hash = Animator.StringToHash(avatarInstance.AvatarDescriptor.floatParameters[i]);
                    if (!avatarInstance.Parameters.ContainsKey(hash))
                    {
                        avatarInstance.Parameters.Add(hash, new AvatarInstance.AvatarParameter { type = AvatarInstance.ParameterType.Float, floatValue = avatarInstance.AvatarDescriptor.floatParametersDefault[i] });
                        avatarInstance.Animator.SetFloat(hash, avatarInstance.AvatarDescriptor.floatParametersDefault[i]);
                    }
                }
            }

            #endregion

            #region Load Menu

            avatarInstance.MenuControls = new List<AvatarInstance.MenuControl>();

            if (avatarInstance.AvatarDescriptor.controlName != null)
            {
                for (int i = 0; i < avatarInstance.AvatarDescriptor.controlName.Length; i++)
                {
                    avatarInstance.MenuControls.Add(new AvatarInstance.MenuControl
                    {
                        name = avatarInstance.AvatarDescriptor.controlName[i],
                        type = avatarInstance.AvatarDescriptor.controlTypes[i],
                        parameter = avatarInstance.AvatarDescriptor.controlParameterNames[i],
                        value = avatarInstance.AvatarDescriptor.controlValues[i]
                    });
                }
            }

            #endregion

            return avatarInstance;
        }

        public override void Unload()
        {
            if (avatarBundle) avatarBundle.Unload(true);
        }
    }
}