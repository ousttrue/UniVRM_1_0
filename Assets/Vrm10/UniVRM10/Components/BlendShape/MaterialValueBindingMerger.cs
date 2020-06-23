using System;
using System.Collections.Generic;
using UnityEngine;

namespace UniVRM10
{
    ///
    /// Base + (A.Target - Base) * A.Weight + (B.Target - Base) * B.Weight ...
    ///
    class MaterialValueBindingMerger
    {
        #region MaterialMap
        /// <summary>
        /// MaterialValueBinding の対象になるマテリアルの情報を記録する
        /// </summary>
        Dictionary<string, PreviewMaterialItem> m_materialMap = new Dictionary<string, PreviewMaterialItem>();

        void InitializeMaterialMap(Dictionary<BlendShapeKey, BlendShapeClip> clipMap, Transform root)
        {
            Dictionary<string, Material> materialNameMap = new Dictionary<string, Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!materialNameMap.ContainsKey(material.name))
                    {
                        materialNameMap.Add(material.name, material);
                    }
                }
            }

            foreach (var kv in clipMap)
            {
                foreach (var binding in kv.Value.MaterialColorBindings)
                {
                    PreviewMaterialItem item;
                    if (!m_materialMap.TryGetValue(binding.MaterialName, out item))
                    {
                        if (!materialNameMap.TryGetValue(binding.MaterialName, out Material material))
                        {
                            // not found skip
                            continue;
                        }
                        item = new PreviewMaterialItem(material);
                        m_materialMap.Add(binding.MaterialName, item);
                    }
                    var propName = VrmLib.MaterialBindTypeExtensions.GetProperty(binding.BindType);
                    item.PropMap.Add(binding.BindType, new PropItem
                    {
                        Name = propName,
                        DefaultValues = item.Material.GetVector(propName),
                    });
                }

                foreach (var binding in kv.Value.MaterialUVBindings)
                {
                    PreviewMaterialItem item;
                    if (!m_materialMap.TryGetValue(binding.MaterialName, out item))
                    {
                        if (!materialNameMap.TryGetValue(binding.MaterialName, out Material material))
                        {
                            // not found skip
                            continue;
                        }
                        item = new PreviewMaterialItem(material);
                        m_materialMap.Add(binding.MaterialName, item);
                    }
                }
            }
        }

        /// <summary>
        /// m_materialMap に記録した値に Material を復旧する
        /// </summary>
        public void RestoreMaterialInitialValues()
        {
            foreach (var kv in m_materialMap)
            {
                kv.Value.RestoreInitialValues();
            }
        }
        #endregion

        #region Accumulate
        struct DictionaryKeyMaterialValueBindingComparer : IEqualityComparer<MaterialColorBinding>
        {
            public bool Equals(MaterialColorBinding x, MaterialColorBinding y)
            {
                return x.TargetValue == y.TargetValue && x.MaterialName == y.MaterialName && x.BindType == y.BindType;
            }

            public int GetHashCode(MaterialColorBinding obj)
            {
                return obj.GetHashCode();
            }
        }

        static DictionaryKeyMaterialValueBindingComparer comparer = new DictionaryKeyMaterialValueBindingComparer();

        /// <summary>
        /// MaterialValueの適用値を蓄積する
        /// </summary>
        /// <typeparam name="MaterialValueBinding"></typeparam>
        /// <typeparam name="float"></typeparam>
        /// <returns></returns>
        Dictionary<MaterialColorBinding, float> m_materialColorMap = new Dictionary<MaterialColorBinding, float>(comparer);

        /// <summary>
        /// UV Scale/Offset
        /// </summary>
        Dictionary<string, Vector4> m_materialUVMap = new Dictionary<string, Vector4>();

        static readonly Vector4 DefaultUVScaleOffset = new Vector4(1, 1, 0, 0);

        public void AccumulateValue(BlendShapeClip clip, float value)
        {
            // material color
            foreach (var binding in clip.MaterialColorBindings)
            {
                float acc;
                if (m_materialColorMap.TryGetValue(binding, out acc))
                {
                    m_materialColorMap[binding] = acc + value;
                }
                else
                {
                    m_materialColorMap[binding] = value;
                }
            }

            // maetrial uv
            foreach (var binding in clip.MaterialUVBindings)
            {
                Vector4 acc;
                if (!m_materialUVMap.TryGetValue(binding.MaterialName, out acc))
                {
                    acc = DefaultUVScaleOffset;
                }

                var delta = binding.ScalingOffset - DefaultUVScaleOffset;
                m_materialUVMap[binding.MaterialName] = acc + delta * value;
            }
        }

        struct MaterialTarget : IEquatable<MaterialTarget>
        {
            public string MaterialName;
            public string ValueName;

            public bool Equals(MaterialTarget other)
            {
                return MaterialName == other.MaterialName
                    && ValueName == other.ValueName;
            }

            public override bool Equals(object obj)
            {
                if (obj is MaterialTarget)
                {
                    return Equals((MaterialTarget)obj);
                }
                else
                {
                    return false;
                }
            }

            public override int GetHashCode()
            {
                if (MaterialName == null || ValueName == null)
                {
                    return 0;
                }
                return MaterialName.GetHashCode() + ValueName.GetHashCode();
            }

            public static MaterialTarget Create(MaterialColorBinding binding)
            {
                return new MaterialTarget
                {
                    MaterialName = binding.MaterialName,
                    ValueName = VrmLib.MaterialBindTypeExtensions.GetProperty(binding.BindType),
                };
            }
        }

        HashSet<MaterialTarget> m_used = new HashSet<MaterialTarget>();
        public void Apply()
        {
            {
                m_used.Clear();
                foreach (var kv in m_materialColorMap)
                {
                    var key = MaterialTarget.Create(kv.Key);
                    PreviewMaterialItem item;
                    if (m_materialMap.TryGetValue(key.MaterialName, out item))
                    {
                        // 初期値(コンストラクタで記録)
                        var initial = item.PropMap[kv.Key.BindType].DefaultValues;
                        if (!m_used.Contains(key))
                        {
                            //
                            // m_used に入っていない場合は、このフレームで初回の呼び出しになる。
                            // (Apply はフレームに一回呼ばれる想定)
                            // 初回は、値を初期値に戻す。
                            //
                            item.Material.SetColor(key.ValueName, initial);
                            m_used.Add(key);
                        }

                        // 現在値
                        var current = item.Material.GetVector(key.ValueName);
                        // 変化量
                        var value = (kv.Key.TargetValue - initial) * kv.Value;
                        // 適用
                        item.Material.SetColor(key.ValueName, current + value);
                    }
                    else
                    {
                        // エラー？
                    }
                }
                m_materialColorMap.Clear();
            }

            {
                foreach (var kv in m_materialUVMap)
                {
                    PreviewMaterialItem item;
                    if (m_materialMap.TryGetValue(kv.Key, out item))
                    {
                        //
                        // Standard and MToon use _MainTex_ST as uv0 scale/offset
                        //
                        item.Material.SetVector("_MainTex_ST", kv.Value);
                    }
                }
                m_materialUVMap.Clear();
            }
        }
        #endregion

        public MaterialValueBindingMerger(Dictionary<BlendShapeKey, BlendShapeClip> clipMap, Transform root)
        {
            InitializeMaterialMap(clipMap, root);
        }
    }
}
