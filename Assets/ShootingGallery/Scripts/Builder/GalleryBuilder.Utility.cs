using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GalleryBuilder のうち、生成に使う道具 (プリミティブ・文字・マテリアルの作成と保存)。</summary>
    public partial class GalleryBuilder
    {
        // ------------------------------------------------------------------
        // ユーティリティ
        // ------------------------------------------------------------------

        /// <summary>プリミティブを作る。position / scale は parent のローカル。collider = false なら当たり判定なし。</summary>
        protected GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : world, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            if (!collider) RemoveCollider(go);
            else if (type == PrimitiveType.Cylinder && scale.y * 2f < Mathf.Max(scale.x, scale.z))
            {
                // 円柱の標準の当たり判定はカプセル型なので、薄い円盤にすると直径と同じ大きさの「球」になってしまい
                // 周りの的まで覆って弾をさえぎる。形どおりの凸メッシュの当たり判定に置き換える。
                DestroyImmediate(go.GetComponent<Collider>());
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = true;
            }
            return go;
        }

        protected static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>当たり判定を消す (飾りの部品用)。渡した GameObject をそのまま返す。</summary>
        protected static GameObject RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
            return go;
        }

        protected TextMesh MakeText(Transform parent, string name, string text, Vector3 localPos, float height, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = height / 6.4f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        Material LanternMat() => Mat("Lantern", new Color(1f, 0.55f, 0.2f), new Color(1.6f, 0.6f, 0.15f));

        /// <summary>URP マテリアルを生成 (同じ名前なら使い回す)。</summary>
        protected Material Mat(string name, Color color, Color? emission = null, bool unlit = false, float smoothness = 0.25f)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;

            var mat = new Material(unlit ? unlitShader : litShader) { name = name };
            mat.SetColor("_BaseColor", color);
            if (!unlit) mat.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        /// <summary>半透明の Unlit マテリアル (爆発エフェクト用)。</summary>
        protected Material TransparentMat(string name, Color color)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;

            var mat = new Material(unlitShader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        /// <summary>
        /// 編集中の生成なら、マテリアルを Assets/ShootingGallery/Generated/Materials に .mat として保存する。
        /// (アセットにしておかないと、シーンに保存・コピペした時にマテリアルが外れてしまう)
        /// 同じ名前のファイルがあれば中身を上書きして使い回すので、何度生成してもファイルは増えない。
        /// </summary>
        Material SaveAsAssetIfEditing(Material mat, string name)
        {
#if UNITY_EDITOR
            if (!editMode) return mat;

            EnsureFolder(GeneratedMaterialDir);
            string path = $"{GeneratedMaterialDir}/{name}.mat";
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(mat, path);
                return mat;
            }

            existing.shader = mat.shader;
            existing.CopyPropertiesFromMaterial(mat);
            existing.shaderKeywords = mat.shaderKeywords;
            existing.SetOverrideTag("RenderType", mat.GetTag("RenderType", false));
            existing.renderQueue = mat.renderQueue;
            UnityEditor.EditorUtility.SetDirty(existing);
            DestroyImmediate(mat);
            return existing;
#else
            return mat;
#endif
        }

#if UNITY_EDITOR
        static void EnsureFolder(string path)
        {
            if (UnityEditor.AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            UnityEditor.AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
#endif
    }
}
