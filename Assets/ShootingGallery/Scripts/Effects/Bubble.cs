using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 水の中の泡 (潜水艦モード)。ゆらゆら揺れながら上がっていき、少しずつふくらんで、最後にぱちんと小さくなって消える。
    /// 魚雷の後ろに引く泡・的に当たった時にぶわっと出る泡・海底から立ちのぼる泡に使う。使い終わったら FxPool にしまって使い回す
    /// </summary>
    public class Bubble : MonoBehaviour
    {
        const string PoolKey = "Bubble";
        // 同時に出ている泡の上限。半透明の泡は重なるほど GPU が重くなるので、魚雷を撃ちまくっても、これより多くは出さない
        const int MaxActive = 120;
        static int active; // 今出ている泡の数 (OnEnable / OnDisable で数える)

        Vector3 velocity;
        float age, life, size, phase, wobble;

        void OnEnable() => active++;
        void OnDisable() => active--;

        /// <summary>泡を1つ出す。velocity = 最初の速さ (だんだん弱まって、上へ浮かぶ速さに変わる)。泡が多すぎる時は出さない</summary>
        public static void Spawn(Vector3 position, float size, Material mat, Vector3 velocity, float life = 1.2f)
        {
            if (mat == null || active >= MaxActive) return;
            var go = FxPool.Get(PoolKey, () =>
            {
                var g = FxPool.Primitive(PrimitiveType.Sphere, mat);
                g.GetComponent<MeshFilter>().sharedMesh = LowPolySphere(); // (標準の球は三角形 約 760 枚。小さな泡には細かすぎるので、約 80 枚の球に)
                g.AddComponent<Bubble>();
                return g;
            });
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.position = position;
            var b = go.GetComponent<Bubble>();
            b.velocity = velocity;
            b.age = 0f;
            b.life = life * Random.Range(0.8f, 1.2f);
            b.size = size;
            b.phase = Random.value * 10f;
            b.wobble = Random.Range(0.15f, 0.35f);
            go.transform.localScale = Vector3.one * (size * 0.4f);
        }

        /// <summary>ぶわっと泡が湧き出す (的に当たった時・魚雷が爆発した時)</summary>
        public static void Burst(Vector3 center, float radius, Material mat, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                Spawn(center + dir * radius * Random.Range(0f, 0.5f), Random.Range(0.04f, 0.12f) * Mathf.Max(0.6f, radius),
                      mat, dir * Random.Range(1f, 2.5f) * Mathf.Max(0.6f, radius), Random.Range(0.9f, 1.8f));
            }
        }

        // 粗い球 (正二十面体の面を1回ずつ4つに分けた形。頂点 42・三角形 80。直径 1)。全部の泡で使い回す
        static Mesh lowPoly;
        static Mesh LowPolySphere()
        {
            if (lowPoly != null) return lowPoly;
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] ico =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var mid = new System.Collections.Generic.Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int i)) return i;
                verts.Add((verts[a] + verts[b]) * 0.5f);
                return mid[key] = verts.Count - 1;
            }
            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < ico.Length; i += 3)
            {
                int a = ico[i], b = ico[i + 1], c = ico[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized * 0.5f;
            for (int i = 0; i < tris.Count; i += 3) // (面の表が外を向くように、向きをそろえる)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
            lowPoly = new Mesh { name = "Bubble_LowPoly" };
            lowPoly.SetVertices(verts);
            lowPoly.SetTriangles(tris, 0);
            lowPoly.RecalculateNormals();
            lowPoly.RecalculateBounds();
            return lowPoly;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            // 最初の勢いはすぐに弱まり、浮く力で上へ (ゆらゆら左右に揺れながら)
            velocity = Vector3.Lerp(velocity, Vector3.up * (0.6f + size * 4f), 1f - Mathf.Exp(-3f * dt));
            Vector3 sway = new Vector3(Mathf.Sin(age * 7f + phase), 0f, Mathf.Cos(age * 6f + phase * 1.3f)) * wobble;
            transform.position += (velocity + sway) * dt;
            float k = age / life;
            // ふくらみながら上がり、最後の 15% でぱちんと消える
            float grow = Mathf.Lerp(0.4f, 1.15f, Mathf.Clamp01(k * 3f));
            float pop = k > 0.85f ? 1f - (k - 0.85f) / 0.15f : 1f;
            transform.localScale = Vector3.one * size * grow * Mathf.Max(0f, pop);
            if (k >= 1f) FxPool.Release(PoolKey, gameObject);
        }
    }
}
