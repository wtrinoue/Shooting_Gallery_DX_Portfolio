using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShootingGallery
{
    /// <summary>
    /// host の下の、動かない部品の見た目を材質ごとに1つのメッシュにまとめる (描画の回数を減らして軽くする。WebGL の VR は両目で2回描くので特に効く)。
    /// まとめた物は host の子 (Merged_材質名) になり、host といっしょに動く。元の部品は見た目 (Renderer) だけ消し、当たり判定などはそのまま残す。
    /// まとめない物: exclude の下 (別に動く部品・出したり消したりする物)、スクリプトの付いた物の下、隠れている物、材質が2つ以上の物、読めないメッシュ。
    /// 始める前 (Awake など) に1回だけ呼ぶ (メッシュを作るので、遊んでいる最中に呼ぶと重い)
    /// </summary>
    public static class MeshMerger
    {
        class Group
        {
            public readonly List<CombineInstance> parts = new List<CombineInstance>();
            public readonly List<MeshRenderer> sources = new List<MeshRenderer>();
            public int vertices;
        }

        /// <summary>まとめて消した元の部品の数を返す</summary>
        public static int Merge(Transform host, ICollection<Transform> exclude)
        {
            if (host == null) return 0;
            var groups = new Dictionary<Material, Group>();
            var toHost = host.worldToLocalMatrix;
            foreach (var r in host.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled || r.sharedMaterials.Length != 1 || r.sharedMaterial == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1) continue;
                if (!Mergeable(r.transform, host, exclude)) continue;
                if (!groups.TryGetValue(r.sharedMaterial, out var g)) groups.Add(r.sharedMaterial, g = new Group());
                g.parts.Add(new CombineInstance { mesh = mesh, transform = toHost * r.transform.localToWorldMatrix });
                g.sources.Add(r);
                g.vertices += mesh.vertexCount;
            }

            int merged = 0;
            foreach (var pair in groups)
            {
                var g = pair.Value;
                if (g.sources.Count < 2) continue; // 1つだけならまとめても変わらない
                var mesh = new Mesh { name = "Merged_" + pair.Key.name };
                if (g.vertices > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(g.parts.ToArray(), true, true);

                var first = g.sources[0];
                var go = new GameObject(mesh.name);
                go.layer = first.gameObject.layer;
                go.transform.SetParent(host, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = pair.Key;
                mr.shadowCastingMode = first.shadowCastingMode;
                mr.receiveShadows = first.receiveShadows;
                foreach (var r in g.sources) r.enabled = false;
                merged += g.sources.Count;
            }
            return merged;
        }

        // t から host までの間に、まとめてはいけない物がないか
        static bool Mergeable(Transform t, Transform host, ICollection<Transform> exclude)
        {
            for (var p = t; p != null && p != host; p = p.parent)
            {
                if (exclude != null && exclude.Contains(p)) return false;
                if (!p.gameObject.activeSelf) return false;          // 出したり消したりする物
                if (p.GetComponent<MonoBehaviour>() != null) return false; // スクリプトで動かしている物
            }
            return true;
        }
    }
}
