using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// Unknown ステージで、灰色に止まった空間を「1つの立体」として、ガラスが割れるように破片に切り分けて散らばらせる仕組み。
    ///   切り方: 空中のひび割れガラス (UnknownStage の球) の破片 1枚ごとに、頭からその破片を通って外へ広がる三角の筒 (錐) を考え、
    ///          さらに頭からの距離で 手前・中ほど・奥 に切る。屋台・提灯・柱・ハッチ・地面などの面を、この筒と距離の境目で切り分けて、
    ///          入っている所ごとに 1つの破片 (メッシュ) にまとめる。提灯や柱も途中で切れる (部品ごとには分けない)。
    ///          ひびの線と筒の境目が同じなので、プレイヤーからは、見えている景色がひびの線どおりに立体のまま割れて見える。
    ///   流れ: 世界が灰色に止まったら Build (重いので、ひびが広がっている間に、1フレーム数ミリ秒ずつ少しずつ切る) →
    ///         割れる瞬間に Show (本物の屋台・地面は GameManager / UnknownStage が隠す。破片は同じ形・同じ灰色なので入れ替わったことに気づかない) →
    ///         ApplyPieces で散らばる (UnknownStage が決めた一片ごとの動き = ガラスの破片 1枚と同じ動きで、その錐の中の破片をみな動かす。
    ///         頭を中心にした回転と拡大なので、目から見て、ひびで切り取られた景色の一片が奥行きを持ったまま、まとまって剥がれ落ちていく)。
    ///         帰りは同じ道を逆にたどって組み上がり、Clear で本物と入れ替える。
    ///   切り口: 面を切っただけだと、柱や提灯の中が空洞に見える (ねぎの輪切りのように) ので、切り口に同じ灰色のふた (断面) を貼る。
    ///          部品 (立方体・円柱・球など。まとめたメッシュは、頂点の位置でつながっている所ごとに分ける) ごとに、
    ///          破片の境目の面の上にできた点を集め、その凸包を断面にする (平らな地面などは点が一直線に並ぶので、ふたは作らない)。
    ///   3D の文字 (看板・点数。TextMesh) は、フォントの文字の形から 1文字ずつの板のメッシュに組み立て直し (BakeText)、ほかの面と同じように切る
    ///   (文字の途中でも、ひびの線どおりに割れる)。並びは、実際に描かれている文字の範囲にぴったり合わせる。色は頂点の色で渡す。
    ///   破片の GameObject とメッシュは使い回す (オブジェクトプール)。同じ形のメッシュの頂点は1回だけ読む。
    /// </summary>
    public class WorldShatter : MonoBehaviour
    {
        [Tooltip("頭からの距離で切る所 (m)。手前・中ほど・奥 の3段になる (動きは同じ一片としていっしょ)")]
        public float[] layerCuts = { 6f, 14f };
        [Tooltip("切り分けに使ってよい、1フレームあたりの時間 (ミリ秒)")]
        public float budgetMs = 3f;

        // ---- 錐 (ガラスの破片 1枚 → 頭から外へ広がる三角の筒) ----
        Vector3[] coneAxis;
        Vector3[,] conePlanes; // [錐, 3] 頭を通る3枚の面の向き (内側が正)
        Vector3[,] coneUnit;   // その長さ 1 の向き (切り口の点が面の上にあるかを測る)
        int[][] cellCones;     // 緯度・経度のマスごとの錐の番号
        int rows, cols;
        float radius, down;
        int Layers => layerCuts.Length + 1;

        struct V
        {
            public Vector3 p, n;
            public Vector2 uv;
            public Color c; // 頂点の色 (3D の文字の色。ほかの面は白)
            public static V Lerp(V a, V b, float t) => new V { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t), uv = Vector2.Lerp(a.uv, b.uv, t), c = Color.Lerp(a.c, b.c, t) };
        }

        class MeshData
        {
            public Vector3[] v, n;
            public Vector2[] uv;
            public int[][] tris;
            public int[][] comp; // 三角形ごとの、つながっている部品の番号 (切り口のふたを部品ごとに作るため)
        }

        // 1つの破片の中の 1つの部品の、境目の面ごとの切り口の点 (使い回す)
        class CapSet
        {
            public readonly List<Vector3>[] points = { new List<Vector3>(), new List<Vector3>(), new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
            public Material mat;
            public int builder, cone, layer;
        }

        // 破片 1つ分の、切った面を集める入れ物 (使い回す)
        class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<Material> mats = new List<Material>();
            public readonly List<List<int>> subs = new List<List<int>>();
            public readonly List<(TextMesh text, Renderer renderer)> texts = new List<(TextMesh, Renderer)>();
            public bool Empty => v.Count == 0 && texts.Count == 0;
            public void Clear()
            {
                v.Clear(); n.Clear(); uv.Clear(); colors.Clear(); mats.Clear(); texts.Clear();
                foreach (var s in subs) s.Clear();
            }
            public List<int> Sub(Material m)
            {
                for (int i = 0; i < mats.Count; i++) if (mats[i] == m) return subs[i];
                mats.Add(m);
                if (subs.Count < mats.Count) subs.Add(new List<int>());
                return subs[mats.Count - 1];
            }
        }

        class Chunk
        {
            public GameObject go;
            public Transform t;
            public MeshFilter filter;
            public MeshRenderer renderer;
            public Mesh mesh;
            public Vector3 pos; // 元の場所 (破片の真ん中。ワールド)
            public int cone;    // どのガラスの破片 (一片) の錐に入っているか
            public readonly List<GameObject> texts = new List<GameObject>();
        }

        Builder[] builders;
        readonly Dictionary<Mesh, MeshData> meshCache = new Dictionary<Mesh, MeshData>();
        readonly Dictionary<Material, Material> chunkMats = new Dictionary<Material, Material>();
        readonly List<Chunk> pool = new List<Chunk>();
        readonly List<Chunk> active = new List<Chunk>();
        readonly List<GameObject> textPool = new List<GameObject>();
        readonly List<MeshRenderer> renderersTmp = new List<MeshRenderer>();
        readonly List<Vector3> shiftTmp = new List<Vector3>();
        readonly V[] bufA = new V[24], bufB = new V[24], poly = new V[24];
        Vector3 head;
        // 切り口のふた
        const float CapEps = 0.002f; // この距離より面に近い点は、面の上にあるとみなす (m)
        readonly Dictionary<long, CapSet> caps = new Dictionary<long, CapSet>();
        readonly List<CapSet> capPool = new List<CapSet>();
        readonly List<Vector2> hullPts = new List<Vector2>();
        readonly List<int> hullOrder = new List<int>(), hull = new List<int>();
        int capComp = -1; // 今切っている三角形の部品の番号 (-1 = ふたを作らない)

        /// <summary>切り分けが終わって、Show できるか</summary>
        public bool Ready { get; private set; }
        /// <summary>破片を出した (本物と入れ替えた) か</summary>
        public bool Shown { get; private set; }

        /// <summary>
        /// ガラスの破片の形 (頭から見た3つの角の位置) と、緯度・経度のマスごとの破片の番号を受け取る (UnknownStage.BuildShell から1回だけ)。
        /// shellRadius / shellDown = ガラスの球の横の半径と下の半径 (下は縦につぶしてある)
        /// </summary>
        public void SetCones(List<(Vector3 a, Vector3 b, Vector3 c)> tris, int[][] cells, int gridRows, int gridCols, float shellRadius, float shellDown)
        {
            int n = tris.Count;
            coneAxis = new Vector3[n];
            conePlanes = new Vector3[n, 3];
            coneUnit = new Vector3[n, 3];
            for (int i = 0; i < n; i++)
            {
                var (a, b, c) = tris[i];
                coneAxis[i] = (a + b + c).normalized;
                conePlanes[i, 0] = Inward(a, b, c);
                conePlanes[i, 1] = Inward(b, c, a);
                conePlanes[i, 2] = Inward(c, a, b);
                for (int k = 0; k < 3; k++) coneUnit[i, k] = conePlanes[i, k].normalized;
            }
            cellCones = cells;
            rows = gridRows;
            cols = gridCols;
            radius = shellRadius;
            down = shellDown;
            builders = new Builder[n * Layers];
            for (int i = 0; i < builders.Length; i++) builders[i] = new Builder();
        }

        // 頭・p・q を通る面の向き (r の側が正)
        static Vector3 Inward(Vector3 p, Vector3 q, Vector3 r)
        {
            var nrm = Vector3.Cross(p, q);
            return Vector3.Dot(nrm, r) < 0f ? -nrm : nrm;
        }

        /// <summary>
        /// roots の下の見えている物と地面を、錐と距離で切り分けて破片を作る (少しずつ。終わると Ready)。
        /// headPos = 頭の位置 (ガラスの球の真ん中)
        /// </summary>
        public IEnumerator Build(IList<Transform> roots, Renderer ground, Vector3 headPos)
        {
            Clear();
            Ready = false;
            head = headPos;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var b in builders) b.Clear();
            foreach (var set in caps.Values) // (前の切り分けが途中で止められた時の残り)
            {
                foreach (var pts in set.points) pts.Clear();
                set.mat = null;
                capPool.Add(set);
            }
            caps.Clear();
            capComp = -1;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            var list = new List<MeshRenderer>();
            foreach (var root in roots)
            {
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                renderersTmp.Clear();
                root.GetComponentsInChildren(false, renderersTmp);
                foreach (var r in renderersTmp) if (r.enabled) list.Add(r);
            }
            if (ground is MeshRenderer gm && ground.gameObject.activeInHierarchy && ground.enabled) list.Add(gm);

            // 1. 面を切り分けて、破片ごとに集める
            foreach (var r in list)
            {
                var tm = r.GetComponent<TextMesh>();
                if (tm != null)
                {
                    if (!BakeText(tm, r)) AddText(tm, r); // (組み立てられない文字は、真ん中が入っている破片にそのまま乗せる)
                    continue;
                }
                var mf = r.GetComponent<MeshFilter>();
                var data = mf != null ? DataOf(mf.sharedMesh) : null;
                if (data == null) continue;
                var l2w = r.transform.localToWorldMatrix;
                var nrmM = l2w.inverse.transpose;
                var mats = r.sharedMaterials;
                for (int s = 0; s < data.tris.Length; s++)
                {
                    var mat = ChunkMaterial(mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null);
                    if (mat == null) continue;
                    var tris = data.tris[s];
                    for (int k = 0; k + 2 < tris.Length; k += 3)
                    {
                        capComp = data.comp[s][k / 3];
                        AddTriangle(Vert(data, tris[k], l2w, nrmM), Vert(data, tris[k + 1], l2w, nrmM), Vert(data, tris[k + 2], l2w, nrmM), mat);
                        if ((k & 63) == 0 && watch.Elapsed.TotalMilliseconds > budgetMs) { yield return null; watch.Restart(); }
                    }
                }
                capComp = -1;
                FlushCaps(); // この見た目の部品の切り口に、ふたを貼る
            }

            // 2. 集めた面から、破片の GameObject とメッシュを作る (使い回し)。動きは一片 (錐) ごとに UnknownStage が決める
            int layers = Layers;
            for (int i = 0; i < builders.Length; i++)
            {
                var b = builders[i];
                if (b.Empty) continue;
                var c = MakeChunk(b);
                c.cone = i / layers;
                if (watch.Elapsed.TotalMilliseconds > budgetMs) { yield return null; watch.Restart(); }
            }
            Ready = true;
        }

        /// <summary>割れる瞬間: 破片を出す (同じ場所・同じ形・同じ灰色)</summary>
        public void Show()
        {
            foreach (var c in active) c.go.SetActive(true);
            Shown = true;
        }

        /// <summary>
        /// 一片ごとの今の動き (rot = 頭を中心にした回転、scale = 頭から遠ざかる倍率、vanish = 消えていく大きさ。添え字はガラスの破片の番号) で、
        /// 破片を置く。同じ一片 (錐) の破片は、手前・中ほど・奥のどれも、ガラスの破片とまったく同じに動く
        /// </summary>
        public void ApplyPieces(Quaternion[] rot, float[] scale, float[] vanish)
        {
            foreach (var c in active)
            {
                float v = vanish[c.cone];
                if (v <= 0.002f)
                {
                    if (c.go.activeSelf) c.go.SetActive(false); // 消えきった (描かない)
                    continue;
                }
                if (!c.go.activeSelf) c.go.SetActive(true);
                var r = rot[c.cone];
                float s = scale[c.cone];
                c.t.SetPositionAndRotation(head + r * ((c.pos - head) * s), r);
                c.t.localScale = Vector3.one * (s * v);
            }
        }

        /// <summary>全部の破片を元の場所に (組み上がった形)</summary>
        public void Home()
        {
            foreach (var c in active)
            {
                if (!c.go.activeSelf) c.go.SetActive(true);
                c.t.SetPositionAndRotation(c.pos, Quaternion.identity);
                c.t.localScale = Vector3.one;
            }
        }

        /// <summary>破片を全部しまって、プールに戻す</summary>
        public void Clear()
        {
            foreach (var c in active)
            {
                foreach (var tx in c.texts)
                {
                    tx.SetActive(false);
                    tx.transform.SetParent(transform, false);
                    textPool.Add(tx);
                }
                c.texts.Clear();
                c.go.SetActive(false);
                c.mesh.Clear();
                pool.Add(c);
            }
            active.Clear();
            Ready = false;
            Shown = false;
        }

        // ---- 切り分け ----

        V Vert(MeshData d, int i, Matrix4x4 l2w, Matrix4x4 nrm) => new V
        {
            p = l2w.MultiplyPoint3x4(d.v[i]) - head,
            n = d.n != null && d.n.Length > i ? nrm.MultiplyVector(d.n[i]).normalized : Vector3.up,
            uv = d.uv != null && d.uv.Length > i ? d.uv[i] : Vector2.zero,
            c = Color.white
        };

        // 三角形 1枚を、重なる錐と距離の段ごとに切って、それぞれの破片に足す
        void AddTriangle(V a, V b, V c, Material mat)
        {
            // 3つの角が、緯度・経度のどのマスのあたりにあるか (ガラスの格子は少しずらしてあるので、まわり1マスも調べる)
            Cell(a.p, out float ra, out float ca);
            Cell(b.p, out float rb, out float cb);
            Cell(c.p, out float rc, out float cc);
            // 経度は 360° で一周するので、いちばん近い向きにそろえる
            if (cb - ca > cols / 2f) cb -= cols; else if (ca - cb > cols / 2f) cb += cols;
            if (cc - ca > cols / 2f) cc -= cols; else if (ca - cc > cols / 2f) cc += cols;
            int r0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ra, Mathf.Min(rb, rc))) - 1);
            int r1 = Mathf.Min(rows - 1, Mathf.FloorToInt(Mathf.Max(ra, Mathf.Max(rb, rc))) + 1);
            int c0 = Mathf.FloorToInt(Mathf.Min(ca, Mathf.Min(cb, cc))) - 1;
            int c1 = Mathf.FloorToInt(Mathf.Max(ca, Mathf.Max(cb, cc))) + 1;
            bool allCols = c1 - c0 + 1 >= cols;
            for (int r = r0; r <= r1; r++)
            {
                bool pole = r == 0 || r == rows - 1; // 極のまわりは、どの経度にもまたがりうる
                int from = allCols || pole ? 0 : c0, to = allCols || pole ? cols - 1 : c1;
                for (int col = from; col <= to; col++)
                {
                    int cell = r * cols + ((col % cols) + cols) % cols;
                    foreach (int cone in cellCones[cell]) ClipToCone(a, b, c, cone, mat);
                }
            }
        }

        // 頭から見た向きが、ガラスの格子のどのマスか (行・列。小数)。下の半分は縦につぶした球なので、元に戻してから測る
        void Cell(Vector3 p, out float row, out float col)
        {
            if (p.y < 0f) p.y *= radius / Mathf.Max(0.01f, down);
            float len = p.magnitude;
            if (len < 1e-5f) { row = rows * 0.5f; col = 0f; return; }
            float lat = Mathf.Asin(Mathf.Clamp(p.y / len, -1f, 1f)) * Mathf.Rad2Deg;
            float lon = Mathf.Repeat(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg, 360f);
            row = (lat + 90f) / 180f * rows;
            col = lon / 360f * cols;
        }

        void ClipToCone(V a, V b, V c, int cone, Material mat)
        {
            bufA[0] = a; bufA[1] = b; bufA[2] = c;
            int n = Clip(bufA, 3, bufB, conePlanes[cone, 0], 0f);
            if (n < 3) return;
            n = Clip(bufB, n, bufA, conePlanes[cone, 1], 0f);
            if (n < 3) return;
            n = Clip(bufA, n, bufB, conePlanes[cone, 2], 0f);
            if (n < 3) return;
            // 距離の段ごとに切る (頭からの、錐の軸に沿った距離)
            var axis = coneAxis[cone];
            int layers = Layers;
            for (int layer = 0; layer < layers; layer++)
            {
                for (int i = 0; i < n; i++) poly[i] = bufB[i];
                int m = n;
                if (layer > 0)
                {
                    m = Clip(poly, m, bufA, axis, layerCuts[layer - 1]);
                    if (m < 3) continue;
                    for (int i = 0; i < m; i++) poly[i] = bufA[i];
                }
                if (layer < layers - 1)
                {
                    m = Clip(poly, m, bufA, -axis, -layerCuts[layer]);
                    if (m < 3) continue;
                    for (int i = 0; i < m; i++) poly[i] = bufA[i];
                }
                AddPolygon(builders[cone * layers + layer], poly, m, mat);
                RecordCap(cone * layers + layer, cone, layer, m, mat);
            }
        }

        // ---- 切り口のふた ----

        // 切った多角形 (poly) の角のうち、破片の境目の面の上にある点を、部品ごと・面ごとに覚えておく
        void RecordCap(int builder, int cone, int layer, int count, Material mat)
        {
            if (capComp < 0) return;
            long key = ((long)builder << 32) | (uint)capComp;
            if (!caps.TryGetValue(key, out var set))
            {
                if (capPool.Count > 0) { set = capPool[capPool.Count - 1]; capPool.RemoveAt(capPool.Count - 1); }
                else set = new CapSet();
                set.mat = mat;
                set.builder = builder;
                set.cone = cone;
                set.layer = layer;
                caps.Add(key, set);
            }
            var axis = coneAxis[cone];
            bool near = layer > 0, far = layer < Layers - 1;
            for (int i = 0; i < count; i++)
            {
                var p = poly[i].p;
                for (int k = 0; k < 3; k++)
                    if (Mathf.Abs(Vector3.Dot(coneUnit[cone, k], p)) < CapEps) set.points[k].Add(p);
                float along = Vector3.Dot(axis, p);
                if (near && Mathf.Abs(along - layerCuts[layer - 1]) < CapEps) set.points[3].Add(p);
                if (far && Mathf.Abs(along - layerCuts[layer]) < CapEps) set.points[4].Add(p);
            }
        }

        // 覚えておいた点から、面ごとに断面 (凸包) を作って破片に足す
        void FlushCaps()
        {
            foreach (var set in caps.Values)
            {
                var b = builders[set.builder];
                var axis = coneAxis[set.cone];
                for (int k = 0; k < 5; k++)
                {
                    var pts = set.points[k];
                    if (pts.Count >= 3)
                    {
                        // 外向き (破片の外) の向き
                        var outward = k < 3 ? -coneUnit[set.cone, k] : k == 3 ? -axis : axis;
                        AddHull(b, pts, outward, set.mat);
                    }
                    pts.Clear();
                }
                set.mat = null;
                capPool.Add(set);
            }
            caps.Clear();
        }

        // 面の上の点の凸包を、1枚の多角形として足す (面積がほとんどない = 平らな物を切っただけの時は足さない)
        void AddHull(Builder b, List<Vector3> pts, Vector3 normal, Material mat)
        {
            var u = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var w = Vector3.Cross(normal, u);
            hullPts.Clear();
            hullOrder.Clear();
            for (int i = 0; i < pts.Count; i++)
            {
                hullPts.Add(new Vector2(Vector3.Dot(pts[i], u), Vector3.Dot(pts[i], w)));
                hullOrder.Add(i);
            }
            hullOrder.Sort(CompareHullPoints);
            // Andrew の方法 (下側と上側をつなぐ)
            hull.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int j = 0; j < hullOrder.Count; j++)
                {
                    int i = hullOrder[pass == 0 ? j : hullOrder.Count - 1 - j];
                    while (hull.Count >= start + 2 && Cross(hullPts[hull[hull.Count - 2]], hullPts[hull[hull.Count - 1]], hullPts[i]) <= 1e-7f) hull.RemoveAt(hull.Count - 1);
                    hull.Add(i);
                }
                hull.RemoveAt(hull.Count - 1); // (次の側の始まりと同じ点)
            }
            if (hull.Count < 3) return;
            float area = 0f;
            for (int i = 1; i + 1 < hull.Count; i++) area += Cross(hullPts[hull[0]], hullPts[hull[i]], hullPts[hull[i + 1]]);
            if (area * 0.5f < 1e-5f) return;

            var sub = b.Sub(mat);
            int first = b.v.Count;
            foreach (int i in hull)
            {
                b.v.Add(pts[i]);
                b.n.Add(normal);
                b.uv.Add(Vector2.zero);
                b.colors.Add(Color.white);
            }
            for (int i = 1; i + 1 < hull.Count; i++)
            {
                sub.Add(first);
                sub.Add(first + i);
                sub.Add(first + i + 1);
            }
        }

        int CompareHullPoints(int a, int b)
        {
            var p = hullPts[a];
            var q = hullPts[b];
            int c = p.x.CompareTo(q.x);
            return c != 0 ? c : p.y.CompareTo(q.y);
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        // 多角形を、面 (n・p >= d の側) で切る (Sutherland–Hodgman)
        static int Clip(V[] src, int count, V[] dst, Vector3 n, float d)
        {
            int o = 0;
            for (int i = 0; i < count; i++)
            {
                var cur = src[i];
                var nxt = src[(i + 1) % count];
                float dc = Vector3.Dot(n, cur.p) - d, dn = Vector3.Dot(n, nxt.p) - d;
                if (dc >= 0f && o < dst.Length) dst[o++] = cur;
                if ((dc >= 0f) != (dn >= 0f) && o < dst.Length) dst[o++] = V.Lerp(cur, nxt, dc / (dc - dn));
            }
            return o;
        }

        static void AddPolygon(Builder b, V[] p, int count, Material mat)
        {
            var sub = b.Sub(mat);
            int start = b.v.Count;
            for (int i = 0; i < count; i++)
            {
                b.v.Add(p[i].p);
                b.n.Add(p[i].n);
                b.uv.Add(p[i].uv);
                b.colors.Add(p[i].c);
            }
            for (int i = 1; i + 1 < count; i++)
            {
                sub.Add(start);
                sub.Add(start + i);
                sub.Add(start + i + 1);
            }
        }

        // ---- 3D の文字をメッシュに組み立て直す ----

        struct Glyph { public Vector2 min, max, uvBL, uvBR, uvTR, uvTL; }
        readonly List<Glyph> glyphs = new List<Glyph>(); // 使い回す
        readonly List<float> lineWidths = new List<float>();

        /// <summary>
        /// 3D の文字を、フォントの文字の形 (1文字ずつの四角い板と、フォントの画像の中の位置) からメッシュに組み立て直して、ほかの面と同じように切る。
        /// 並べ方は TextMesh の行・揃え方に合わせ、全体を、実際に描かれている文字の範囲 (localBounds) にぴったり合わせて置く
        /// (TextMesh の大きさの計算をまねなくても、見た目が割れる前と同じになる)。組み立てられない時は false
        /// </summary>
        bool BakeText(TextMesh tm, Renderer r)
        {
            var font = tm.font;
            string text = tm.text;
            if (font == null || string.IsNullOrEmpty(text) || r.sharedMaterial == null) return false;
            if (tm.richText && text.IndexOf('<') >= 0) return false; // (色などの指定が入った文字は、そのまま乗せる)
            int size = tm.fontSize > 0 ? tm.fontSize : font.fontSize;
            var style = tm.fontStyle;
            font.RequestCharactersInTexture(text, size, style);
            float lineHeight = font.lineHeight * (font.fontSize > 0 && size > 0 ? size / (float)font.fontSize : 1f) * tm.lineSpacing;

            // 1. 行ごとの幅 (揃え方のため)
            lineWidths.Clear();
            float w = 0f;
            foreach (char ch in text)
            {
                if (ch == '\n') { lineWidths.Add(w); w = 0f; continue; }
                if (font.GetCharacterInfo(ch, out var ci, size, style)) w += ci.advance;
            }
            lineWidths.Add(w);
            float widest = 0f;
            foreach (float lw in lineWidths) widest = Mathf.Max(widest, lw);

            // 2. 1文字ずつの板を並べる (文字の単位はフォントのピクセル。下の行ほど下へ)
            glyphs.Clear();
            int line = 0;
            float pen = LineStart(tm.alignment, lineWidths[0], widest);
            foreach (char ch in text)
            {
                if (ch == '\n')
                {
                    line++;
                    pen = LineStart(tm.alignment, lineWidths[line], widest);
                    continue;
                }
                if (!font.GetCharacterInfo(ch, out var ci, size, style)) continue;
                float y = -line * lineHeight;
                if (ci.maxX > ci.minX && ci.maxY > ci.minY)
                    glyphs.Add(new Glyph
                    {
                        min = new Vector2(pen + ci.minX, y + ci.minY), max = new Vector2(pen + ci.maxX, y + ci.maxY),
                        uvBL = ci.uvBottomLeft, uvBR = ci.uvBottomRight, uvTR = ci.uvTopRight, uvTL = ci.uvTopLeft
                    });
                pen += ci.advance;
            }
            if (glyphs.Count == 0) return false;

            // 3. 並べた全体を、実際に描かれている文字の範囲に合わせる。大きさは横幅で決めて、縦も同じ倍率にする
            //    (描かれている範囲は縦に行の高さの余白まで含むので、縦も範囲に合わせると文字が縦に伸びてしまう)。上下は範囲の真ん中に置く
            Vector2 lo = glyphs[0].min, hi = glyphs[0].max;
            foreach (var g in glyphs) { lo = Vector2.Min(lo, g.min); hi = Vector2.Max(hi, g.max); }
            var lb = r.localBounds;
            if (hi.x - lo.x < 1e-4f || hi.y - lo.y < 1e-4f || lb.size.x < 1e-5f) return false;
            var l2w = r.transform.localToWorldMatrix;
            var normal = l2w.MultiplyVector(Vector3.back).normalized; // (文字は -Z の方から読める)
            var mat = r.sharedMaterial;
            var color = tm.color; // (もう灰色になっている)
            float k = lb.size.x / (hi.x - lo.x); // (フォントのピクセル → 文字のローカル座標)
            float midY = (lo.y + hi.y) * 0.5f;
            V At(float x, float y, Vector2 uv) => new V
            {
                p = l2w.MultiplyPoint3x4(new Vector3(lb.min.x + (x - lo.x) * k, lb.center.y + (y - midY) * k, lb.center.z)) - head,
                n = normal, uv = uv, c = color
            };

            // 4. ほかの面と同じように、錐と距離で切って破片に足す (文字は平らなので、切り口のふたは作らない)
            capComp = -1;
            foreach (var g in glyphs)
            {
                var bl = At(g.min.x, g.min.y, g.uvBL);
                var tl = At(g.min.x, g.max.y, g.uvTL);
                var tr = At(g.max.x, g.max.y, g.uvTR);
                var br = At(g.max.x, g.min.y, g.uvBR);
                AddTriangle(bl, tl, tr, mat);
                AddTriangle(bl, tr, br, mat);
            }
            return true;
        }

        // 行の書き始めの位置 (揃え方: 左 / 真ん中 / 右。いちばん長い行の中で揃える)
        static float LineStart(TextAlignment alignment, float width, float widest) =>
            alignment == TextAlignment.Center ? (widest - width) * 0.5f : alignment == TextAlignment.Right ? widest - width : 0f;

        // 組み立てられない 3D の文字は、真ん中が入っている破片にそのまま乗せる
        void AddText(TextMesh src, Renderer r)
        {
            var p = r.bounds.center - head;
            Cell(p, out float row, out float col);
            int best = -1;
            float bestDot = -2f;
            int r0 = Mathf.Max(0, Mathf.FloorToInt(row) - 1), r1 = Mathf.Min(rows - 1, Mathf.FloorToInt(row) + 1);
            for (int rr = r0; rr <= r1; rr++)
                for (int cc = Mathf.FloorToInt(col) - 1; cc <= Mathf.FloorToInt(col) + 1; cc++)
                    foreach (int cone in cellCones[rr * cols + ((cc % cols) + cols) % cols])
                    {
                        float dot = Vector3.Dot(coneAxis[cone], p.normalized);
                        if (dot > bestDot) { bestDot = dot; best = cone; }
                    }
            if (best < 0) return;
            float dist = Vector3.Dot(coneAxis[best], p);
            int layer = 0;
            while (layer < layerCuts.Length && dist >= layerCuts[layer]) layer++;
            builders[best * Layers + layer].texts.Add((src, r));
        }

        // 集めた面から破片を 1つ作る (原点を破片の真ん中にして、その場で回るように)
        Chunk MakeChunk(Builder b)
        {
            var c = pool.Count > 0 ? PopChunk() : NewChunk();
            Vector3 center = Vector3.zero;
            if (b.v.Count > 0)
            {
                foreach (var v in b.v) center += v;
                center /= b.v.Count;
            }
            else
            {
                foreach (var (text, r) in b.texts) center += r.bounds.center - head;
                center /= b.texts.Count;
            }
            shiftTmp.Clear();
            foreach (var v in b.v) shiftTmp.Add(v - center);
            var mesh = c.mesh;
            mesh.Clear();
            mesh.indexFormat = b.v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(shiftTmp);
            mesh.SetNormals(b.n);
            mesh.SetUVs(0, b.uv);
            mesh.SetColors(b.colors);
            mesh.subMeshCount = b.mats.Count;
            for (int s = 0; s < b.mats.Count; s++) mesh.SetTriangles(b.subs[s], s, false);
            mesh.RecalculateBounds();
            c.renderer.sharedMaterials = b.mats.ToArray();
            c.pos = head + center;
            c.t.SetPositionAndRotation(c.pos, Quaternion.identity);
            c.t.localScale = Vector3.one;
            foreach (var (text, r) in b.texts) c.texts.Add(CloneText(text, r, c.t));
            c.go.SetActive(false);
            active.Add(c);
            return c;
        }

        GameObject CloneText(TextMesh src, Renderer r, Transform parent)
        {
            GameObject go;
            if (textPool.Count > 0) { go = textPool[textPool.Count - 1]; textPool.RemoveAt(textPool.Count - 1); }
            else { go = new GameObject("ShatterText"); go.AddComponent<TextMesh>(); go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            var t = go.GetComponent<TextMesh>();
            t.text = src.text;
            t.font = src.font;
            t.fontSize = src.fontSize;
            t.fontStyle = src.fontStyle;
            t.characterSize = src.characterSize;
            t.lineSpacing = src.lineSpacing;
            t.anchor = src.anchor;
            t.alignment = src.alignment;
            t.tabSize = src.tabSize;
            t.richText = src.richText;
            t.offsetZ = src.offsetZ;
            t.color = src.color; // (もう灰色になっている)
            go.GetComponent<MeshRenderer>().sharedMaterial = r.sharedMaterial;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            go.transform.localScale = src.transform.lossyScale;
            go.SetActive(true);
            return go;
        }

        // 破片を描く順番: 割れたガラス (灰色の空の色の破片。Geometry+450 / +451) の後。ガラスより奥にあっても、ガラスの前に見える
        const int ChunkQueue = 2460;

        // 破片の材質: 灰色の材質の複製の、裏も描く版 (切った所から中が見えても、穴が空いて見えないように)
        Material ChunkMaterial(Material gray)
        {
            if (gray == null) return null;
            if (!chunkMats.TryGetValue(gray, out var m))
            {
                m = new Material(gray) { name = gray.name + "_Chunk" };
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                if (m.renderQueue <= 2500) m.renderQueue = ChunkQueue; // (半透明の物は、もともと後で描かれる)
                chunkMats.Add(gray, m);
            }
            return m;
        }

        MeshData DataOf(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable) return null;
            if (meshCache.TryGetValue(mesh, out var d)) return d;
            d = new MeshData { v = mesh.vertices, n = mesh.normals, uv = mesh.uv, tris = new int[mesh.subMeshCount][] };
            for (int s = 0; s < mesh.subMeshCount; s++) d.tris[s] = mesh.GetTriangles(s);
            d.comp = Components(d);
            meshCache.Add(mesh, d);
            return d;
        }

        // 三角形ごとに、つながっている部品の番号をつける (同じ位置の頂点はつながっているとみなす。まとめたメッシュの中の、立方体1つ・球1つ…)
        static int[][] Components(MeshData d)
        {
            var parent = new int[d.v.Length];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
            var weld = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < d.v.Length; i++)
            {
                var p = d.v[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
                if (weld.TryGetValue(key, out int j)) Union(i, j);
                else weld.Add(key, i);
            }
            foreach (var tris in d.tris)
                for (int k = 0; k + 2 < tris.Length; k += 3) { Union(tris[k], tris[k + 1]); Union(tris[k + 1], tris[k + 2]); }
            var ids = new Dictionary<int, int>();
            var comp = new int[d.tris.Length][];
            for (int s = 0; s < d.tris.Length; s++)
            {
                var tris = d.tris[s];
                comp[s] = new int[tris.Length / 3];
                for (int k = 0; k + 2 < tris.Length; k += 3)
                {
                    int root = Find(tris[k]);
                    if (!ids.TryGetValue(root, out int id)) ids.Add(root, id = ids.Count);
                    comp[s][k / 3] = id;
                }
            }
            return comp;
        }

        Chunk PopChunk()
        {
            var c = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return c;
        }

        Chunk NewChunk()
        {
            var go = new GameObject("ShatterChunk");
            go.transform.SetParent(transform, false);
            var c = new Chunk { go = go, t = go.transform, filter = go.AddComponent<MeshFilter>(), renderer = go.AddComponent<MeshRenderer>() };
            c.mesh = new Mesh { name = "ShatterChunk" };
            c.mesh.MarkDynamic();
            c.filter.sharedMesh = c.mesh;
            go.SetActive(false);
            return c;
        }
    }
}
