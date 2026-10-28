using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、動かない部品をまとめる処理 (Quest で重くならないように、描く部品の数を減らす)。
    /// </summary>
    public partial class GalleryBuilder
    {
        /// <summary>
        /// root の下の動かない部品を、「いっしょに動く単位」・材質・場所 (cell m 四方の区画) ごとに1つの部品にまとめる。
        /// いっしょに動く単位 (anchor) = root の下のコンポーネント (NinjaDojo・MineCave・NinjaTrap など) が指している物 (壁・天井・ふすま・跳ね上がる畳など)。
        /// 部品は、いちばん近い単位の中でまとめるので、単位ごと動かしたり、出したり隠したりしても、まとめる前と同じに見える。
        /// まとめないもの: 単位そのものの部品 (それ自体を動かす・色を変えるかもしれない)、コンポーネントが付いている物とその子 (的・トロッコ・回る物など)、
        /// extraMoving (コンポーネントから指されていないが動かす物)、当たり判定のある物、半透明の物、文字。
        /// 場所でも分けるのは、1つの部品が計算する明かりは 4 つまでなので、大きな範囲を1つにすると近くの明かりが当たらなくなるから。
        /// split: 部品の中心 (ワールド座標) から、まとめてはいけない組を分ける番号を返す (音楽モードのステージと客席など)。onCombined: まとめてできた部品と、その番号
        /// 実行中に、子の部品を名前や順番で探す所 (GetChild・GetComponentsInChildren など) のあるモードには使わないこと
        /// </summary>
        /// <returns>まとめた元の部品の数と、まとめた後の部品の数</returns>
        (int before, int after) CombineStatic(Transform root, string meshPrefix, float cell = 8f, IEnumerable<Transform> extraMoving = null,
            System.Func<Vector3, int> split = null, System.Action<MeshRenderer, int> onCombined = null)
        {
            // いっしょに動く単位を集める (コンポーネントの欄が指している物)
            var anchors = new HashSet<Transform> { root };
            if (extraMoving != null) foreach (var t in extraMoving) if (t != null) anchors.Add(t);
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) CollectReferencedTransforms(mb, anchors);

            bool CanCombine(MeshRenderer r, out Transform anchor)
            {
                anchor = null;
                if (anchors.Contains(r.transform)) return false;
                if (r.GetComponent<TextMesh>() != null || r.GetComponent<Collider>() != null) return false;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) return false;
                if (r.sharedMaterials.Length != 1 || r.sharedMaterial == null || r.sharedMaterial.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest + 50) return false;
                for (var t = r.transform; t != null; t = t.parent)
                {
                    if (t != root && t.GetComponent<MonoBehaviour>() != null) return false; // (的・トロッコなど、自分で動く物)
                    if (t != r.transform && anchors.Contains(t)) { anchor = t; return true; }
                    if (t == root) break;
                }
                return false;
            }

            // (単位, 材質, 区画) ごとに集める
            var groups = new Dictionary<(Transform, Material, int, int, int, int), List<MeshRenderer>>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!CanCombine(r, out var anchor)) continue;
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                var world = r.transform.TransformPoint(mesh.bounds.center); // (組み立て中は非表示のことがあるので renderer.bounds は使わない)
                var p = anchor.InverseTransformPoint(world);
                var key = (anchor, r.sharedMaterial, Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell), split != null ? split(world) : 0);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
                list.Add(r);
            }

            // まとめた部品を作る (1つしかない区画は、まとめても減らないのでそのまま)
            int before = 0, after = 0;
            var used = new List<MeshRenderer>();
            var anchorIndex = new Dictionary<Transform, int>();
            var created = new List<Mesh>();
            foreach (var pair in groups)
            {
                if (pair.Value.Count < 2) { after++; continue; }
                var (anchor, mat, cx, cy, cz, part) = pair.Key;
                var instances = new List<CombineInstance>();
                bool shadow = false; // (元の部品のどれかが影を落とすなら、まとめた部品も落とす)
                foreach (var r in pair.Value)
                {
                    var m = r.GetComponent<MeshFilter>().sharedMesh;
                    for (int s = 0; s < m.subMeshCount; s++)
                        instances.Add(new CombineInstance { mesh = m, subMeshIndex = s, transform = anchor.worldToLocalMatrix * r.transform.localToWorldMatrix });
                    if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadow = true;
                    used.Add(r);
                }
                if (!anchorIndex.TryGetValue(anchor, out int ai)) anchorIndex[anchor] = ai = anchorIndex.Count; // (同じ名前の単位があっても、メッシュの名前が重ならないように)
                var mesh = new Mesh { name = $"{meshPrefix}_{anchor.name}{ai}_{mat.name}_{cx}_{cy}_{cz}_{part}" };
                int verts = 0;
                foreach (var ci in instances) verts += ci.mesh.vertexCount;
                if (verts > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(instances.ToArray(), true, true);
                mesh.RecalculateBounds();
                created.Add(mesh);
                var g = new GameObject("Combined_" + mat.name);
                g.transform.SetParent(anchor, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                onCombined?.Invoke(mr, part);
                after++;
            }

            SaveCombinedMeshes(meshPrefix, created);

            // 元の部品は消す (中身のなくなった空の物も消す)
            before = used.Count;
            foreach (var r in used)
            {
                var go = r.gameObject;
                var mf = go.GetComponent<MeshFilter>();
                DestroyImmediate(r);
                if (mf != null) DestroyImmediate(mf);
                if (go.transform.childCount == 0 && go.GetComponents<Component>().Length == 1) DestroyImmediate(go); // (Transform だけ残った)
            }
            Debug.Log($"[{meshPrefix}] 動かない部品 {before} 個を、{after} 個にまとめました");
            return (before, after);
        }

        // まとめた形を、モードごとに1つのファイルにまとめて保存する (シーンに生成した時だけ)。
        // 1つずつ別のファイルにすると、何百回も読み込み直しが起きて、シーンへの生成がなかなか終わらなかったので
        void SaveCombinedMeshes(string meshPrefix, List<Mesh> meshes)
        {
#if UNITY_EDITOR
            if (!editMode || meshes.Count == 0) return;
            EnsureFolder(GeneratedMeshDir);
            string path = $"{GeneratedMeshDir}/{meshPrefix}_Combined.asset";
            UnityEditor.AssetDatabase.DeleteAsset(path); // (前に生成した分は作り直す)
            UnityEditor.AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Count; i++) UnityEditor.AssetDatabase.AddObjectToAsset(meshes[i], path);
            UnityEditor.AssetDatabase.ImportAsset(path);
#endif
        }

        // コンポーネントの欄 (public と [SerializeField]) が指している物を、いっしょに動く単位として集める
        static void CollectReferencedTransforms(MonoBehaviour mb, HashSet<Transform> into)
        {
            for (var type = mb.GetType(); type != null && type != typeof(MonoBehaviour); type = type.BaseType)
                foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!f.IsPublic && f.GetCustomAttribute<SerializeField>() == null) continue;
                    var v = f.GetValue(mb);
                    if (v is Object) AddRef(v, into); // (Transform も中の子を数えられるので、先に1つの物として扱う)
                    else if (v is System.Collections.IEnumerable list && !(v is string))
                    {
                        foreach (var item in list) AddRef(item, into);
                    }
                    else AddRef(v, into);
                }
        }

        static void AddRef(object v, HashSet<Transform> into)
        {
            if (v is GameObject go && go != null) into.Add(go.transform);
            else if (v is Component c && c != null) into.Add(c.transform);
        }
    }
}
