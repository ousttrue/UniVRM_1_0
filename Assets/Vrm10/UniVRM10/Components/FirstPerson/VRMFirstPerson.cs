﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace UniVRM10
{
    [AddComponentMenu("VRM/VRMFirstPerson")]
    [DisallowMultipleComponent]
    public class VRMFirstPerson : MonoBehaviour
    {
        // If no layer names are set, use the default layer IDs.
        // Otherwise use the two Unity layers called "VRMFirstPersonOnly" and "VRMThirdPersonOnly".
        public static bool TriedSetupLayer = false;
        public static int FIRSTPERSON_ONLY_LAYER = 9;
        public static int THIRDPERSON_ONLY_LAYER = 10;

        [Serializable]
        public struct RendererFirstPersonFlags
        {
            public Renderer Renderer;
            public VrmLib.FirstPersonMeshType FirstPersonFlag;
            public Mesh SharedMesh
            {
                get
                {
                    var renderer = Renderer as SkinnedMeshRenderer;
                    if (renderer != null)
                    {
                        return renderer.sharedMesh;
                    }

                    var filter = Renderer.GetComponent<MeshFilter>();
                    if (filter != null)
                    {
                        return filter.sharedMesh;
                    }

                    return null;
                }
            }
        }

        [SerializeField]
        public List<RendererFirstPersonFlags> Renderers = new List<RendererFirstPersonFlags>();

        public void CopyTo(GameObject _dst, Dictionary<Transform, Transform> map)
        {
            var dst = _dst.AddComponent<VRMFirstPerson>();
            dst.Renderers = Renderers.Select(x =>
            {
                var renderer = map[x.Renderer.transform].GetComponent<Renderer>();
                return new VRMFirstPerson.RendererFirstPersonFlags
                {
                    Renderer = renderer,
                    FirstPersonFlag = x.FirstPersonFlag,
                };
            }).ToList();
        }

        public static void SetupLayers()
        {
            if (!TriedSetupLayer)
            {
                TriedSetupLayer = true;
                int layer = LayerMask.NameToLayer("VRMFirstPersonOnly");
                FIRSTPERSON_ONLY_LAYER = (layer == -1) ? FIRSTPERSON_ONLY_LAYER : layer;
                layer = LayerMask.NameToLayer("VRMThirdPersonOnly");
                THIRDPERSON_ONLY_LAYER = (layer == -1) ? THIRDPERSON_ONLY_LAYER : layer;
            }
        }


        bool m_done;

        /// <summary>
        /// 配下のモデルのレイヤー設定など
        /// </summary>
        public void Setup()
        {
            SetupLayers();
            if (m_done) return;
            m_done = true;

            var FirstPersonBone = GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Head);
            foreach (var x in Renderers)
            {
                switch (x.FirstPersonFlag)
                {
                    case VrmLib.FirstPersonMeshType.Auto:
                        {
                            if (x.Renderer is SkinnedMeshRenderer smr)
                            {
                                var eraseBones = GetBonesThatHasAncestor(smr, FirstPersonBone);
                                if (eraseBones.Any())
                                {
                                    // オリジナルのモデルを３人称用にする                                
                                    smr.gameObject.layer = THIRDPERSON_ONLY_LAYER;

                                    // 頭を取り除いた複製モデルを作成し、１人称用にする
                                    var headless = CreateHeadlessMesh(smr, eraseBones);
                                    headless.gameObject.layer = FIRSTPERSON_ONLY_LAYER;
                                    headless.transform.SetParent(smr.transform, false);
                                }
                                else
                                {
                                    // 削除対象が含まれないので何もしない
                                }
                            }
                            else if (x.Renderer is MeshRenderer mr)
                            {
                                if (mr.transform.Ancestors().Any(y => y == FirstPersonBone))
                                {
                                    // 頭の子孫なので１人称では非表示に
                                    mr.gameObject.layer = THIRDPERSON_ONLY_LAYER;
                                }
                                else
                                {
                                    // 特に変更しない => 両方表示
                                }
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        }
                        break;

                    case VrmLib.FirstPersonMeshType.FirstPersonOnly:
                        // １人称のカメラでだけ描画されるようにする
                        x.Renderer.gameObject.layer = FIRSTPERSON_ONLY_LAYER;
                        break;

                    case VrmLib.FirstPersonMeshType.ThirdPersonOnly:
                        // ３人称のカメラでだけ描画されるようにする
                        x.Renderer.gameObject.layer = THIRDPERSON_ONLY_LAYER;
                        break;

                    case VrmLib.FirstPersonMeshType.Both:
                        // 特に何もしない。すべてのカメラで描画される
                        break;
                }
            }
        }

        static int[] GetBonesThatHasAncestor(SkinnedMeshRenderer smr, Transform ancestor)
        {
            var eraseBones = smr.bones
            .Where(x => x.Ancestor().Any(y => y == ancestor))
            .Select(x => smr.bones.IndexOf(x))
            .ToArray();
            return eraseBones;
        }

        // <summary>
        // 頭部を取り除いたモデルを複製する
        // </summary>
        // <parameter>renderer: 元になるSkinnedMeshRenderer</parameter>
        // <parameter>eraseBones: 削除対象になるボーンのindex</parameter>
        private static SkinnedMeshRenderer CreateHeadlessMesh(SkinnedMeshRenderer renderer, int[] eraseBones)
        {
            var mesh = BoneMeshEraser.CreateErasedMesh(renderer.sharedMesh, eraseBones);

            var go = new GameObject("_headless_" + renderer.name);
            var erased = go.AddComponent<SkinnedMeshRenderer>();
            erased.sharedMesh = mesh;
            erased.sharedMaterials = renderer.sharedMaterials;
            erased.bones = renderer.bones;
            erased.rootBone = renderer.rootBone;
            erased.updateWhenOffscreen = true;

            return erased;
        }
    }
}
