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

        /// <summary>これより小さい部品 (いちばん長い辺, m) は影を落とさない</summary>
        const float ShadowMinSize = 0.8f;

        /// <summary>プリミティブを作る。position / scale は parent のローカル。collider = false なら当たり判定なし。</summary>
        protected GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : world, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = mat;
            // 影は大きな部品 (屋台の本体・柱・屋根・壁など) だけに落とさせる。小さな部品・光る部品・半透明の部品の影は目立たないのに、
            // 影の描画 (VR は両目分) の数だけ重くなるので切る
            var size = rend.bounds.size;
            bool glowing = mat != null && mat.IsKeywordEnabled("_EMISSION");
            bool transparent = mat != null && mat.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest + 50;
            if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < ShadowMinSize || glowing || transparent)
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

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

        /// <summary>
        /// 3D の文字を作る。ふつうは手前の物に隠れる文字 (Shaders/Text3DDepth.shader)。
        /// overlay = true なら Unity 標準の文字のまま、何があっても透けて見える (HUD・銃の上の文字など、いつも見えてほしい文字)
        /// </summary>
        protected TextMesh MakeText(Transform parent, string name, string text, Vector3 localPos, float height, Color color, bool overlay = false)
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
            var depthMat = overlay ? null : DepthTextMat();
            go.GetComponent<MeshRenderer>().sharedMaterial = depthMat != null ? depthMat : font.material;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 文字は影を落とさない
            return tm;
        }

        /// <summary>手前の物に隠れる文字のマテリアル (全部の文字で1つ。文字の画像は実行中に DepthText が付け直す)。シェーダーが見つからなければ null</summary>
        Material DepthTextMat()
        {
            const string Name = "Text_DepthTested";
            if (mats.TryGetValue(Name, out var cached)) return cached;
            var shader = Shader.Find(DepthText.ShaderName);
            if (shader == null) return null;
            var mat = new Material(shader) { name = Name };
            mat.mainTexture = font.material.mainTexture;
            mat = SaveAsAssetIfEditing(mat, Name);
            mats[Name] = mat;
            return mat;
        }

        Material LanternMat() => Mat("Lantern", new Color(1f, 0.55f, 0.2f), new Color(1.6f, 0.6f, 0.15f));

        /// <summary>URP マテリアルを生成 (同じ名前なら使い回す)。matte = true なら、まわりの景色の映り込みとハイライトを切る (曲面でも、平らな部品と同じ見た目になる)</summary>
        protected Material Mat(string name, Color color, Color? emission = null, bool unlit = false, float smoothness = 0.25f, bool matte = false)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;

            var mat = new Material(unlit ? unlitShader : litShader) { name = name };
            mat.SetColor("_BaseColor", color);
            if (!unlit) mat.SetFloat("_Smoothness", smoothness);
            if (!unlit && matte)
            {
                mat.SetFloat("_SpecularHighlights", 0f);
                mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                mat.SetFloat("_EnvironmentReflections", 0f);
                mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
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
