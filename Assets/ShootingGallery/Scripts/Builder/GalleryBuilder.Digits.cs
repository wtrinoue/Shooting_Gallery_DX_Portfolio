using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、的の点数の数字 (文字ではなく図形)。
    /// 太い線で数字の形をなぞった平らなメッシュを作る。同じ点数の的は同じメッシュを使い回すので、1つの的につき部品は1個だけ
    /// (文字を重ねて太く見せるより軽く、線の太さも自由に決められる)。表 (-Z) から見える面だけ
    /// </summary>
    public partial class GalleryBuilder
    {
        const float DigitStroke = 0.17f; // 線の太さ (数字の高さ 1 に対して)
        const float DigitGap = 0.1f;     // 数字と数字の間
        const float DigitHeight = 0.8f;  // 数字の縦の長さ (幅はそのままで、縦だけ縮めてずんぐりさせる。線の太さは変えない)
        const int DigitRoundSides = 12;  // 線の端や角の丸みの細かさ

        /// <summary>点数の数字の色 (普通の屋台・豪華屋台で共通の黒)</summary>
        Material DigitMat => Mat("Target_Digits", new Color(0.07f, 0.07f, 0.07f), null, false, 0.2f, true);

        /// <summary>
        /// 点数の数字を置く。表 (-Z) を向き、center が数字のまん中。maxWidth / maxHeight に収まるいちばん大きな大きさにする
        /// (高さは縮める前の数字 (高さ 1) で測る。縦を縮めた分だけ、数字は maxHeight より低くなる)
        /// </summary>
        GameObject PointDigits(Transform parent, int points, Vector3 center, float maxWidth, float maxHeight, Material mat)
        {
            var mesh = DigitsMesh(points.ToString());
            var b = mesh.bounds.size;
            float s = Mathf.Min(maxWidth / b.x, maxHeight / (1f + DigitStroke));
            var go = MeshObj(parent, "Points", mesh, center, mat);
            go.transform.localScale = new Vector3(s, s, 1f);
            return go;
        }

        /// <summary>横長の角丸の板 (両端が半円)。幅 width・高さ height、まん中が原点、表 (-Z) を向く。点数の数字の下敷き用</summary>
        GameObject PillPlate(Transform parent, string name, Vector3 center, float width, float height, Material mat)
        {
            float r = height / 2f, half = Mathf.Max(0f, width / 2f - r);
            string meshName = $"Pill_{width:0.000}x{height:0.000}";
            if (!meshes.TryGetValue(meshName, out var mesh))
            {
                var verts = new List<Vector3>();
                var tris = new List<int>();
                AddStroke(verts, tris, new[] { new Vector2(-half, 0f), new Vector2(half, 0f) }, false, r);
                mesh = FinishFlatMesh(meshName, verts, tris);
            }
            return MeshObj(parent, name, mesh, center, mat);
        }

        /// <summary>数字の列のメッシュ (高さ 1 + 線の太さ。まん中が原点)。同じ文字列なら使い回す</summary>
        Mesh DigitsMesh(string text)
        {
            string name = $"Digits_{text}";
            if (meshes.TryGetValue(name, out var cached)) return cached;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            float x = 0f;
            foreach (char c in text)
            {
                var (strokes, width) = DigitStrokes(c);
                foreach (var (pts, closed) in strokes)
                {
                    var moved = new Vector2[pts.Length];
                    for (int i = 0; i < pts.Length; i++) moved[i] = new Vector2(pts[i].x + x, pts[i].y * DigitHeight);
                    AddStroke(verts, tris, moved, closed, DigitStroke / 2f);
                }
                x += width + DigitStroke + DigitGap;
            }

            // まん中を原点に
            var min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            var max = new Vector3(float.MinValue, float.MinValue, 0f);
            foreach (var v in verts) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
            var mid = (min + max) / 2f;
            mid.z = 0f;
            for (int i = 0; i < verts.Count; i++) verts[i] -= mid;
            return FinishFlatMesh(name, verts, tris);
        }

        Mesh FinishFlatMesh(string name, List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;
            mesh.normals = normals;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }

        /// <summary>
        /// 線 (点を順につないだ折れ線) を、太さ 2·half の帯にして足す。開いた線は両端を丸くする。
        /// 急に曲がる所 (数字の角) では線を分けて、つなぎ目を丸でふさぐ (とがった角が長く伸びないように)
        /// </summary>
        static void AddStroke(List<Vector3> verts, List<int> tris, Vector2[] pts, bool closed, float half)
        {
            if (closed)
            {
                AddStrip(verts, tris, pts, true, half);
                return;
            }
            var piece = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Length; i++)
            {
                piece.Add(pts[i]);
                bool sharp = i < pts.Length - 1 && Vector2.Dot((pts[i] - pts[i - 1]).normalized, (pts[i + 1] - pts[i]).normalized) < 0.6f;
                if (!sharp) continue;
                AddStrip(verts, tris, piece.ToArray(), false, half);
                piece = new List<Vector2> { pts[i] };
            }
            if (piece.Count > 1) AddStrip(verts, tris, piece.ToArray(), false, half);
        }

        static void AddStrip(List<Vector3> verts, List<int> tris, Vector2[] pts, bool closed, float half)
        {
            int n = pts.Length;
            int start = verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(i - 1, 0)];
                Vector2 next = closed ? pts[(i + 1) % n] : pts[Mathf.Min(i + 1, n - 1)];
                Vector2 dIn = (pts[i] - prev).normalized, dOut = (next - pts[i]).normalized;
                if (dIn == Vector2.zero) dIn = dOut;
                if (dOut == Vector2.zero) dOut = dIn;
                Vector2 tan = (dIn + dOut).normalized;
                Vector2 normal = new Vector2(-tan.y, tan.x);
                // 曲がり目でも線の太さが変わらないように、左右へ少し広げる (広げすぎない)
                float miter = half / Mathf.Max(0.5f, Vector2.Dot(normal, new Vector2(-dOut.y, dOut.x)));
                verts.Add(pts[i] + normal * miter);  // 左
                verts.Add(pts[i] - normal * miter);  // 右
            }
            int segs = closed ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                int l0 = start + i * 2, r0 = l0 + 1;
                int l1 = start + (i + 1) % n * 2, r1 = l1 + 1;
                tris.Add(r0); tris.Add(l0); tris.Add(l1); // 表 (-Z) から見て時計回り
                tris.Add(r0); tris.Add(l1); tris.Add(r1);
            }
            if (closed) return;
            AddRound(verts, tris, pts[0], half);
            AddRound(verts, tris, pts[n - 1], half);
        }

        // 線の端・角をふさぐ丸
        static void AddRound(List<Vector3> verts, List<int> tris, Vector2 c, float r)
        {
            int center = verts.Count;
            verts.Add(c);
            for (int k = 0; k < DigitRoundSides; k++)
            {
                float a = k * Mathf.PI * 2f / DigitRoundSides;
                verts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            for (int k = 0; k < DigitRoundSides; k++)
            {
                tris.Add(center);
                tris.Add(center + 1 + (k + 1) % DigitRoundSides);
                tris.Add(center + 1 + k);
            }
        }

        /// <summary>
        /// 数字1文字の線 (高さ 1、左下が原点。線のまん中の位置) と、その文字の幅。
        /// 線は (点の列, 閉じているか) の組
        /// </summary>
        static (List<(Vector2[] pts, bool closed)> strokes, float width) DigitStrokes(char c)
        {
            const float W = 0.5f;
            var s = new List<(Vector2[], bool)>();
            switch (c)
            {
                case '0':
                    s.Add((Arc(0.25f, 0.5f, 0.25f, 0.5f, 90f, -270f, false), true));
                    break;
                case '1':
                    s.Add((new[] { new Vector2(0f, 0.76f), new Vector2(0.2f, 1f), new Vector2(0.2f, 0f) }, false));
                    return (s, 0.22f);
                case '2':
                    s.Add((Join(Arc(0.25f, 0.73f, 0.25f, 0.27f, 165f, -35f), new[] { new Vector2(0f, 0f), new Vector2(W, 0f) }), false));
                    break;
                case '3':
                    s.Add((Arc(0.25f, 0.75f, 0.23f, 0.25f, 155f, -90f), false));
                    s.Add((Arc(0.25f, 0.26f, 0.25f, 0.26f, 90f, -155f), false));
                    break;
                case '4':
                    s.Add((new[] { new Vector2(0.36f, 0f), new Vector2(0.36f, 1f), new Vector2(0f, 0.32f), new Vector2(W, 0.32f) }, false));
                    break;
                case '5':
                    s.Add((Join(new[] { new Vector2(0.47f, 1f), new Vector2(0.06f, 1f) }, Arc(0.24f, 0.31f, 0.26f, 0.31f, 140f, -150f)), false));
                    break;
                case '6':
                    s.Add((Bezier(new Vector2(0.44f, 0.97f), new Vector2(0.02f, 0.88f), new Vector2(0f, 0.32f)), false));
                    s.Add((Arc(0.25f, 0.3f, 0.25f, 0.3f, 90f, -270f, false), true));
                    break;
                case '7':
                    s.Add((new[] { new Vector2(0f, 1f), new Vector2(W, 1f), new Vector2(0.15f, 0f) }, false));
                    break;
                case '8':
                    s.Add((Arc(0.25f, 0.76f, 0.2f, 0.24f, 90f, -270f, false), true));
                    s.Add((Arc(0.25f, 0.27f, 0.25f, 0.27f, 90f, -270f, false), true));
                    break;
                case '9': // 6 を逆さにした形
                    foreach (var (pts, closed) in DigitStrokes('6').strokes)
                    {
                        var flipped = new Vector2[pts.Length];
                        for (int i = 0; i < pts.Length; i++) flipped[i] = new Vector2(W - pts[i].x, 1f - pts[i].y);
                        s.Add((flipped, closed));
                    }
                    break;
            }
            return (s, W);
        }

        // 楕円の弧 (度。from → to の向きに回る)。includeEnd = false なら終わりの点を入れない (閉じた輪にする時)
        static Vector2[] Arc(float cx, float cy, float rx, float ry, float fromDeg, float toDeg, bool includeEnd = true)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 12f));
            var pts = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                if (i == steps && !includeEnd) break;
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
            }
            return pts.ToArray();
        }

        // 2次ベジェ曲線
        static Vector2[] Bezier(Vector2 a, Vector2 control, Vector2 b)
        {
            const int Steps = 10;
            var pts = new Vector2[Steps + 1];
            for (int i = 0; i <= Steps; i++)
            {
                float t = (float)i / Steps;
                pts[i] = (1f - t) * (1f - t) * a + 2f * (1f - t) * t * control + t * t * b;
            }
            return pts;
        }

        static Vector2[] Join(Vector2[] a, Vector2[] b)
        {
            var all = new Vector2[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }
    }
}
