﻿using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Vrm10;
using VrmLib;
using VrmLib.Diff;

namespace UniVRM10.Test
{
    public class MaterialTests
    {
        const string _vrmPath = "Tests/Models/Alicia_vrm-1.00/AliciaSolid_vrm-1.00.vrm";

        string[] _mtooSrgbTextureProperties = {
            VrmLib.MToon.Utils.PropMainTex,
            VrmLib.MToon.Utils.PropShadeTexture,
            VrmLib.MToon.Utils.PropEmissionMap,
            VrmLib.MToon.Utils.PropSphereAdd,
            VrmLib.MToon.Utils.PropRimTexture,
        };

        string[] _mtoonLinearTextureProperties = {
            VrmLib.MToon.Utils.PropBumpMap,
            VrmLib.MToon.Utils.PropOutlineWidthTexture,
            VrmLib.MToon.Utils.PropUvAnimMaskTexture
        };

        private ModelAsset ToUnity(string path)
        {
            var fi = new FileInfo(_vrmPath);
            var bytes = File.ReadAllBytes(fi.FullName);
            return ToUnity(bytes);
        }

        private ModelAsset ToUnity(byte[] bytes)
        {
            // Vrm => Model
            var model = VrmLoader.CreateVrmModel(bytes, new FileInfo("tmp.vrm"));
            model.RemoveSecondary();

            return ToUnity(model);
        }

        private ModelAsset ToUnity(Model model)
        {
            // Model => Unity
            var assets = RuntimeUnityBuilder.ToUnityAsset(model);
            UniVRM10.ComponentBuilder.Build10(model, assets);
            return assets;
        }

        private Model ToVrmModel(GameObject root)
        {
            var exporter = new UniVRM10.RuntimeVrmConverter();
            var model = exporter.ToModelFrom10(root, root.GetComponent<VRMMeta>().Meta);

            model.ConvertCoordinate(VrmLib.Coordinates.Gltf, ignoreVrm: false);
            return model;
        }

        void EqualColor(Color color1, Color color2)
        {
            Assert.AreEqual(color1.r, color2.r, 0.001f);
            Assert.AreEqual(color1.g, color2.g, 0.001f);
            Assert.AreEqual(color1.b, color2.b, 0.001f);
            Assert.AreEqual(color1.a, color2.a, 0.001f);
        }

        void EqualVector4(System.Numerics.Vector4 vec1, System.Numerics.Vector4 vec2)
        {
            Assert.AreEqual(vec1.X, vec2.X, 0.001f);
            Assert.AreEqual(vec1.Y, vec2.Y, 0.001f);
            Assert.AreEqual(vec1.Z, vec2.Z, 0.001f);
            Assert.AreEqual(vec1.W, vec2.W, 0.001f);
        }

        #region Color

        [UnityTest]
        public IEnumerator ColorSpace_UnityBaseColorToLiner()
        {
            var assets = ToUnity(_vrmPath);
            var srcMaterial = assets.Map.Materials.First();
            var key = srcMaterial.Key;
            var srcColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            var srcGammaColor = srcColor;
            var srclinerColor = srcColor.linear;

            srcMaterial.Value.color = srcColor;

            var model = ToVrmModel(assets.Root);
            var dstMaterial = model.Materials.First(x => x.Name == key.Name);

            EqualColor(srclinerColor, dstMaterial.BaseColorFactor.RGBA.ToUnityColor());

            yield return null;
        }

        [UnityTest]
        public IEnumerator ColorSpace_GltfBaseColorToGamma()
        {
            var assets = ToUnity(_vrmPath);
            var srcMaterial = assets.Map.Materials.First();
            var key = srcMaterial.Key;
            var srcColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            var srclinerColor = srcColor.ToVector4();
            var srcGammaColor = srcColor.gamma.ToVector4();


            var model = ToVrmModel(assets.Root);
            var gltfMaterial = model.Materials.First(x => x.Name == key.Name);
            gltfMaterial.BaseColorFactor = new LinearColor
            {
                RGBA = srclinerColor
            };

            var bytes = model.ToGlb();

            var dstAssets = ToUnity(bytes);
            var dstMaterial = dstAssets.Map.Materials.First(x => x.Value.name == key.Name);

            EqualColor(srcGammaColor.ToUnityColor(), dstMaterial.Value.color);

            yield return null;
        }

        [UnityTest]
        public IEnumerator MToonUnityColorToGltf()
        {
            var assets = ToUnity(_vrmPath);
            var srcMaterial = assets.Map.Materials.First();
            var key = srcMaterial.Key;
            var srcColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            var srcGammaColor = srcColor;
            var srclinerColor = srcColor.linear;

            srcMaterial.Value.SetColor(VrmLib.MToon.Utils.PropColor, srcColor);
            srcMaterial.Value.SetColor(VrmLib.MToon.Utils.PropShadeColor, srcColor);
            srcMaterial.Value.SetColor(VrmLib.MToon.Utils.PropEmissionColor, srcColor);
            srcMaterial.Value.SetColor(VrmLib.MToon.Utils.PropRimColor, srcColor);
            srcMaterial.Value.SetColor(VrmLib.MToon.Utils.PropOutlineColor, srcColor);

            var model = ToVrmModel(assets.Root);
            var dstMaterial = model.Materials.First(x => x.Name == key.Name) as VrmLib.MToonMaterial;

            // sRGB
            EqualColor(srclinerColor, dstMaterial.Definition.Color.LitColor.RGBA.ToUnityColor());
            EqualColor(srclinerColor, dstMaterial.Definition.Color.ShadeColor.RGBA.ToUnityColor());
            EqualColor(srclinerColor, dstMaterial.Definition.Outline.OutlineColor.RGBA.ToUnityColor());
            // HDR Color
            EqualColor(srcColor, dstMaterial.Definition.Emission.EmissionColor.RGBA.ToUnityColor());
            EqualColor(srcColor, dstMaterial.Definition.Rim.RimColor.RGBA.ToUnityColor());

            yield return null;
        }

        [UnityTest]
        public IEnumerator MtoonGltfColorToUnity()
        {
            var assets = ToUnity(_vrmPath);
            var srcMaterial = assets.Map.Materials.First();
            var key = srcMaterial.Key;
            var srcColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            var srclinerColor = srcColor.ToVector4();
            var srcGammaColor = srcColor.gamma.ToVector4();

            var model = ToVrmModel(assets.Root);
            var gltfMaterial = model.Materials.First(x => x.Name == key.Name) as VrmLib.MToonMaterial;
            gltfMaterial.Definition.Color.LitColor = new LinearColor { RGBA = srclinerColor };
            gltfMaterial.Definition.Color.ShadeColor = new LinearColor { RGBA = srclinerColor };
            gltfMaterial.Definition.Outline.OutlineColor = new LinearColor { RGBA = srclinerColor };
            gltfMaterial.Definition.Emission.EmissionColor = new LinearColor { RGBA = srclinerColor };
            gltfMaterial.Definition.Rim.RimColor = new LinearColor { RGBA = srclinerColor };

            var bytes = model.ToGlb();

            var dstAssets = ToUnity(bytes);
            var dstMaterial = dstAssets.Map.Materials.First(x => x.Value.name == key.Name).Value;
            // sRGB
            EqualColor(srcGammaColor.ToUnityColor(), dstMaterial.GetColor(VrmLib.MToon.Utils.PropColor));
            EqualColor(srcGammaColor.ToUnityColor(), dstMaterial.GetColor(VrmLib.MToon.Utils.PropShadeColor));
            EqualColor(srcGammaColor.ToUnityColor(), dstMaterial.GetColor(VrmLib.MToon.Utils.PropOutlineColor));
            // HDR Color
            EqualColor(srcColor, dstMaterial.GetColor(VrmLib.MToon.Utils.PropEmissionColor));
            EqualColor(srcColor, dstMaterial.GetColor(VrmLib.MToon.Utils.PropRimColor));

            yield return null;
        }
        #endregion

        #region Texture

        Texture2D CreateMonoTexture(float mono, float alpha, bool isLinear)
        {
            Texture2D texture = new Texture2D(128, 128, TextureFormat.ARGB32, mipChain: false, linear: isLinear);
            Color col = new Color(mono, mono, mono, alpha);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, col);
                }
            }
            texture.Apply();
            return texture;
        }

        void EqualTextureColor(Texture2D texture, VrmLib.ImageTexture imageTexture, bool isLinear)
        {
            var srcColor = texture.GetPixel(0, 0);

            var dstTexture = new Texture2D(2, 2, TextureFormat.ARGB32, mipChain: false, linear: isLinear);
            dstTexture.LoadImage(imageTexture.Image.Bytes.ToArray());
            var dstColor = dstTexture.GetPixel(0, 0);

            Debug.LogFormat("src:{0}, dst{1}", srcColor, dstColor);
            EqualColor(srcColor, dstColor);
        }

        [UnityTest]
        public IEnumerator MToonTextureToGltf_BaseColorTexture()
        {
            var assets = ToUnity(_vrmPath);
            var srcMaterial = assets.Map.Materials.First();
            var key = srcMaterial.Key;
            var srcSrgbTexture = CreateMonoTexture(0.5f, 0.5f, false);

            srcMaterial.Value.SetTexture(VrmLib.MToon.Utils.PropMainTex, srcSrgbTexture);

            var model = ToVrmModel(assets.Root);
            var dstMaterial = model.Materials.First(x => x.Name == key.Name) as VrmLib.MToonMaterial;

            var imageTexture = dstMaterial.Definition.Color.LitMultiplyTexture.Texture as VrmLib.ImageTexture;
            EqualTextureColor(srcSrgbTexture, imageTexture, false);

            yield return null;
        }

        [UnityTest]
        public IEnumerator MToonTextureToGltf_OutlineWidthTexture()
        {
            var assets = ToUnity(_vrmPath);
            var srcMaterial = assets.Map.Materials.First();
            var key = srcMaterial.Key;
            var srcLinearTexture = CreateMonoTexture(0.5f, 0.5f, true);

            srcMaterial.Value.SetTexture(VrmLib.MToon.Utils.PropOutlineWidthTexture, srcLinearTexture);

            var model = ToVrmModel(assets.Root);
            var dstMaterial = model.Materials.First(x => x.Name == key.Name) as VrmLib.MToonMaterial;

            var imageTexture = dstMaterial.Definition.Outline.OutlineWidthMultiplyTexture.Texture as VrmLib.ImageTexture;
            EqualTextureColor(srcLinearTexture, imageTexture, true);

            yield return null;
        }

        [UnityTest]
        public IEnumerator GetOrCreateTextureTest()
        {
            var converter = new RuntimeVrmConverter();
            var material = new UnityEngine.Material(Shader.Find("VRM/MToon"));
            var srcLinearTexture = CreateMonoTexture(0.5f, 1.0f, true);
            var srcSRGBTexture = CreateMonoTexture(0.5f, 1.0f, false);

            {
                material.SetTexture(VrmLib.MToon.Utils.PropOutlineWidthTexture, srcSRGBTexture);
                var textureInfo = converter.GetOrCreateTexture(material, srcSRGBTexture, VrmLib.Texture.ColorSpaceTypes.Srgb, VrmLib.Texture.TextureTypes.Default);
                var imageTexture = textureInfo.Texture as VrmLib.ImageTexture;
                EqualTextureColor(srcSRGBTexture, imageTexture, false);
            }

            {
                material.SetTexture(VrmLib.MToon.Utils.PropOutlineWidthTexture, srcLinearTexture);
                var textureInfo = converter.GetOrCreateTexture(material, srcLinearTexture, VrmLib.Texture.ColorSpaceTypes.Linear, VrmLib.Texture.TextureTypes.Default);
                var imageTexture = textureInfo.Texture as VrmLib.ImageTexture;
                EqualTextureColor(srcLinearTexture, imageTexture, true);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator MToonTextureToUnity()
        {
            yield return null;
        }
        #endregion
    }
}