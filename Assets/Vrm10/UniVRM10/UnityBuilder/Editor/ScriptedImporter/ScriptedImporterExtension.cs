﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Experimental.AssetImporters;
using UnityEditor;
using System;
using UnityEngine;

namespace UniVRM10
{
    public static class ScriptedImporterExtension
    {
        public static void ClearExtarnalObjects<T>(this ScriptedImporter importer) where T : UnityEngine.Object
        {
            foreach (var extarnalObject in importer.GetExternalObjectMap().Where(x => x.Key.type == typeof(T)))
            {
                importer.RemoveRemap(extarnalObject.Key);
            }

            AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
            AssetDatabase.ImportAsset(importer.assetPath, ImportAssetOptions.ForceUpdate);
        }

        public static void ClearExtarnalObjects(this ScriptedImporter importer)
        {
            foreach (var extarnalObject in importer.GetExternalObjectMap())
            {
                importer.RemoveRemap(extarnalObject.Key);
            }

            AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
            AssetDatabase.ImportAsset(importer.assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static T GetSubAsset<T>(this ScriptedImporter importer, string assetPath) where T : UnityEngine.Object
        {
            return importer.GetSubAssets<T>(assetPath)
                .FirstOrDefault();
        }

        public static IEnumerable<T> GetSubAssets<T>(this ScriptedImporter importer, string assetPath) where T : UnityEngine.Object
        {
            return AssetDatabase
                .LoadAllAssetsAtPath(assetPath)
                .Where(x => AssetDatabase.IsSubAsset(x))
                .Where(x => x is T)
                .Select(x => x as T);
        }

        private static void ExtractFromAsset(UnityEngine.Object subAsset, string destinationPath, bool isForceUpdate)
        {
            string assetPath = AssetDatabase.GetAssetPath(subAsset);

            var clone = UnityEngine.Object.Instantiate(subAsset);
            AssetDatabase.CreateAsset(clone, destinationPath);

            var assetImporter = AssetImporter.GetAtPath(assetPath);
            assetImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(subAsset), clone);

            if (isForceUpdate)
            {
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        public static void ExtractAssets<T>(this ScriptedImporter importer, string dirName, string extension) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(importer.assetPath))
                return;

            var subAssets = importer.GetSubAssets<T>(importer.assetPath);

            var path = string.Format("{0}/{1}.{2}",
                Path.GetDirectoryName(importer.assetPath),
                Path.GetFileNameWithoutExtension(importer.assetPath),
                dirName
                );

            var info = importer.SafeCreateDirectory(path);

            foreach (var asset in subAssets)
            {
                ExtractFromAsset(asset, string.Format("{0}/{1}{2}", path, asset.name, extension), false);
            }
        }


        public static void ExtractTextures(this ScriptedImporter importer, string dirName, Func<string, VrmLib.Model> CreateModel, Action onComplited = null)
        {
            if (string.IsNullOrEmpty(importer.assetPath))
                return;

            var subAssets = importer.GetSubAssets<UnityEngine.Texture2D>(importer.assetPath);

            var path = string.Format("{0}/{1}.{2}",
                Path.GetDirectoryName(importer.assetPath),
                Path.GetFileNameWithoutExtension(importer.assetPath),
                dirName
                );

            importer.SafeCreateDirectory(path);

            Dictionary<VrmLib.ImageTexture, string> targetPaths = new Dictionary<VrmLib.ImageTexture, string>();

            // Reload Model
            var model = CreateModel(importer.assetPath);
            var mimeTypeReg = new System.Text.RegularExpressions.Regex("image/(?<mime>.*)$");
            int count = 0;
            foreach (var texture in model.Textures)
            {
                var imageTexture = texture as VrmLib.ImageTexture;
                if (imageTexture == null) continue;

                var mimeType = mimeTypeReg.Match(imageTexture.Image.MimeType);
                var assetName = !string.IsNullOrEmpty(imageTexture.Name) ? imageTexture.Name : string.Format("{0}_img{1}", model.Root.Name, count);
                var targetPath = string.Format("{0}/{1}.{2}",
                    path,
                    assetName,
                    mimeType.Groups["mime"].Value);
                imageTexture.Name = assetName;

                if (imageTexture.TextureType == VrmLib.Texture.TextureTypes.MetallicRoughness
                    || imageTexture.TextureType == VrmLib.Texture.TextureTypes.Occlusion)
                {
                    var subAssetTexture = subAssets.Where(x => x.name == imageTexture.Name).FirstOrDefault();
                    File.WriteAllBytes(targetPath, subAssetTexture.EncodeToPNG());
                }
                else
                {
                    File.WriteAllBytes(targetPath, imageTexture.Image.Bytes.ToArray());
                }

                AssetDatabase.ImportAsset(targetPath);
                targetPaths.Add(imageTexture, targetPath);

                count++;
            }

            EditorApplication.delayCall += () =>
            {
                foreach (var targetPath in targetPaths)
                {
                    var imageTexture = targetPath.Key;
                    var targetTextureImporter = AssetImporter.GetAtPath(targetPath.Value) as TextureImporter;
                    targetTextureImporter.sRGBTexture = (imageTexture.ColorSpace == VrmLib.Texture.ColorSpaceTypes.Srgb);
                    if (imageTexture.TextureType == VrmLib.Texture.TextureTypes.NormalMap)
                    {
                        targetTextureImporter.textureType = TextureImporterType.NormalMap;
                    }
                    targetTextureImporter.SaveAndReimport();

                    var externalObject = AssetDatabase.LoadAssetAtPath(targetPath.Value, typeof(UnityEngine.Texture2D));
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(UnityEngine.Texture2D), imageTexture.Name), externalObject);
                }

                //AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                AssetDatabase.ImportAsset(importer.assetPath, ImportAssetOptions.ForceUpdate);

                if (onComplited != null)
                {
                    onComplited();
                }
            };
        }

        public static DirectoryInfo SafeCreateDirectory(this ScriptedImporter importer, string path)
        {
            if (Directory.Exists(path))
            {
                return null;
            }
            return Directory.CreateDirectory(path);
        }
    }
}