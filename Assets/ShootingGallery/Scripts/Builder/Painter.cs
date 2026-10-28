using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 美術館の絵をビルダーで描くための、小さなお絵かきの道具 (色の配列に、四角・だ円・多角形・線・ぼかし などを塗る)。
    /// 座標はピクセル (左下が原点、上が +y)。描いた絵は PNG に保存して、実行時は普通のテクスチャとして使う (実行中に描くことはない)
    /// </summary>
    public sealed class Painter
    {
        public readonly int W, H;
        public readonly Color[] px;
        readonly System.Random rng;
        readonly bool transparent; // 透明なキャンバス (塗った所だけ不透明になる。的の絵のステッカー用)

        /// <summary>
        /// 描く位置をまとめて縮めて動かす (ピクセル = 座標 × Scale + Offset)。四角・だ円・光・多角形・線 (折れ線・筆も) に効く。
        /// 的の絵では、絵全体を少し縮めてまわりに余白を作り、黒いふちがキャンバスの端で切れないようにする。ふつうは 1 と 0 (そのまま)
        /// </summary>
        public float Scale = 1f;
        public Vector2 Offset;
        float TX(float x) => x * Scale + Offset.x;
        float TY(float y) => y * Scale + Offset.y;

        public Painter(int w, int h, int seed) : this(w, h, seed, false) { }

        /// <summary>transparent = 透明なキャンバスから描く (塗った所だけが見え、アルファに形が入る)</summary>
        public Painter(int w, int h, int seed, bool transparent)
        {
            W = w;
            H = h;
            px = new Color[w * h];
            rng = new System.Random(seed);
            this.transparent = transparent;
            Fill(transparent ? Color.clear : Color.white);
        }

        public static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public float R() => (float)rng.NextDouble();
        public float R(float a, float b) => a + (b - a) * R();
        public int RI(int a, int b) => rng.Next(a, b);
        public Color Jit(Color c, float j) => new Color(Mathf.Clamp01(c.r + R(-j, j)), Mathf.Clamp01(c.g + R(-j, j)), Mathf.Clamp01(c.b + R(-j, j)), 1f);
        public Color Pick(Color[] palette) => palette[rng.Next(palette.Length)];

        void Blend(int x, int y, Color c, float a)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H || a <= 0f) return;
            int i = y * W + x;
            var d = px[i];
            if (a >= 1f) { px[i] = new Color(c.r, c.g, c.b, 1f); return; }
            if (transparent)
            {   // 透明なキャンバス: 下の色の不透明さも考えて重ねる
                float outA = a + d.a * (1f - a);
                if (outA <= 0f) return;
                float k = d.a * (1f - a);
                px[i] = new Color((c.r * a + d.r * k) / outA, (c.g * a + d.g * k) / outA, (c.b * a + d.b * k) / outA, outA);
                return;
            }
            px[i] = new Color(d.r + (c.r - d.r) * a, d.g + (c.g - d.g) * a, d.b + (c.b - d.b) * a, 1f);
        }

        /// <summary>
        /// 透明なキャンバスの絵のまわりに、太さ width ピクセルのふち (ステッカーの黒いふち) を付ける。
        /// 塗った所 (アルファ 0.5 以上) から width 以内の透明な所を c で塗る (ふちの外側は少しぼかす)
        /// </summary>
        public void Outline(float width, Color c)
        {
            int r = Mathf.CeilToInt(width + 1f);
            var src = (Color[])px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (src[i].a >= 0.99f) continue;
                    float best = float.MaxValue;
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int yy = y + dy;
                        if ((uint)yy >= (uint)H) continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx;
                            if ((uint)xx >= (uint)W || src[yy * W + xx].a < 0.5f) continue;
                            float d2 = dx * dx + dy * dy;
                            if (d2 < best) best = d2;
                        }
                    }
                    if (best == float.MaxValue) continue;
                    float cov = Mathf.Clamp01(width + 0.5f - Mathf.Sqrt(best));
                    if (cov <= 0f) continue;
                    // ふちを、もとの (半透明の) ふちの下に敷く
                    var top = src[i];
                    float outA = top.a + cov * (1f - top.a);
                    float k = cov * (1f - top.a);
                    px[i] = new Color((top.r * top.a + c.r * k) / outA, (top.g * top.a + c.g * k) / outA, (top.b * top.a + c.b * k) / outA, outA);
                }
        }

        public Color Get(int x, int y) => px[Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];

        public void Fill(Color c)
        {
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        /// <summary>y0〜y1 の横いっぱいを、下の色 bottom から上の色 top へのグラデーションで塗る</summary>
        public void VGrad(float y0, float y1, Color bottom, Color top)
        {
            int a = Mathf.Max(0, Mathf.FloorToInt(y0)), b = Mathf.Min(H - 1, Mathf.CeilToInt(y1));
            for (int y = a; y <= b; y++)
            {
                var c = Color.Lerp(bottom, top, Mathf.InverseLerp(y0, y1, y));
                for (int x = 0; x < W; x++) px[y * W + x] = c;
            }
        }

        public void Rect(float x0, float y0, float x1, float y1, Color c, float a = 1f)
        {
            x0 = TX(x0); x1 = TX(x1); y0 = TY(y0); y1 = TY(y1);
            int xa = Mathf.Max(0, Mathf.RoundToInt(Mathf.Min(x0, x1))), xb = Mathf.Min(W - 1, Mathf.RoundToInt(Mathf.Max(x0, x1)) - 1);
            int ya = Mathf.Max(0, Mathf.RoundToInt(Mathf.Min(y0, y1))), yb = Mathf.Min(H - 1, Mathf.RoundToInt(Mathf.Max(y0, y1)) - 1);
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++) Blend(x, y, c, a);
        }

        /// <summary>だ円 (rot = 傾き 度)。ふちは soft ピクセルだけぼかす</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color c, float a = 1f, float rot = 0f, float soft = 1.2f)
        {
            cx = TX(cx); cy = TY(cy); rx *= Scale; ry *= Scale;
            rx = Mathf.Max(0.5f, rx);
            ry = Mathf.Max(0.5f, ry);
            float r = Mathf.Max(rx, ry) + soft;
            float cs = Mathf.Cos(-rot * Mathf.Deg2Rad), sn = Mathf.Sin(-rot * Mathf.Deg2Rad);
            float minR = Mathf.Min(rx, ry);
            int xa = Mathf.Max(0, Mathf.FloorToInt(cx - r)), xb = Mathf.Min(W - 1, Mathf.CeilToInt(cx + r));
            int ya = Mathf.Max(0, Mathf.FloorToInt(cy - r)), yb = Mathf.Min(H - 1, Mathf.CeilToInt(cy + r));
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float lx = dx * cs - dy * sn, ly = dx * sn + dy * cs;
                    float d = Mathf.Sqrt(lx * lx / (rx * rx) + ly * ly / (ry * ry));
                    float edge = (d - 1f) * minR;
                    float cov = Mathf.Clamp01(0.5f - edge / soft);
                    if (cov > 0f) Blend(x, y, c, a * cov);
                }
        }

        /// <summary>まわりへ向かって薄くなる丸い光</summary>
        public void Glow(float cx, float cy, float radius, Color c, float a)
        {
            cx = TX(cx); cy = TY(cy); radius *= Scale;
            int xa = Mathf.Max(0, Mathf.FloorToInt(cx - radius)), xb = Mathf.Min(W - 1, Mathf.CeilToInt(cx + radius));
            int ya = Mathf.Max(0, Mathf.FloorToInt(cy - radius)), yb = Mathf.Min(H - 1, Mathf.CeilToInt(cy + radius));
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
                    if (d < 1f) Blend(x, y, c, a * (1f - d) * (1f - d));
                }
        }

        /// <summary>多角形 (中が塗られる。ふちのぼかしなし)</summary>
        public void Poly(IList<Vector2> pts, Color c, float a = 1f)
        {
            if (Scale != 1f || Offset != Vector2.zero) { var moved = new List<Vector2>(pts.Count); foreach (var q in pts) moved.Add(new Vector2(TX(q.x), TY(q.y))); pts = moved; }
            if (pts.Count < 3) return;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            int ya = Mathf.Max(0, Mathf.FloorToInt(minY)), yb = Mathf.Min(H - 1, Mathf.CeilToInt(maxY));
            var xs = new List<float>(8);
            for (int y = ya; y <= yb; y++)
            {
                float sy = y + 0.5f;
                xs.Clear();
                for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
                {
                    var p = pts[i];
                    var q = pts[j];
                    if ((p.y > sy) != (q.y > sy)) xs.Add(p.x + (sy - p.y) / (q.y - p.y) * (q.x - p.x));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int xa = Mathf.Max(0, Mathf.RoundToInt(xs[k])), xb = Mathf.Min(W - 1, Mathf.RoundToInt(xs[k + 1]) - 1);
                    for (int x = xa; x <= xb; x++) Blend(x, y, c, a);
                }
            }
        }

        /// <summary>太さ w の線 (両端は丸い)</summary>
        public void Line(float x0, float y0, float x1, float y1, float w, Color c, float a = 1f)
        {
            x0 = TX(x0); x1 = TX(x1); y0 = TY(y0); y1 = TY(y1); w *= Scale;
            float hw = w * 0.5f;
            int xa = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - hw - 1)), xb = Mathf.Min(W - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + hw + 1));
            int ya = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - hw - 1)), yb = Mathf.Min(H - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + hw + 1));
            float dx = x1 - x0, dy = y1 - y0, len2 = Mathf.Max(0.0001f, dx * dx + dy * dy);
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float px_ = x + 0.5f - x0, py = y + 0.5f - y0;
                    float t = Mathf.Clamp01((px_ * dx + py * dy) / len2);
                    float ex = px_ - dx * t, ey = py - dy * t;
                    float dist = Mathf.Sqrt(ex * ex + ey * ey);
                    float cov = Mathf.Clamp01(hw - dist + 0.5f);
                    if (cov > 0f) Blend(x, y, c, a * cov);
                }
        }

        /// <summary>(x, y) を真ん中に、angle 度の向きへ長さ len の筆のひと塗り</summary>
        public void Stroke(float x, float y, float angle, float len, float w, Color c, float a = 1f)
        {
            float dx = Mathf.Cos(angle * Mathf.Deg2Rad) * len * 0.5f, dy = Mathf.Sin(angle * Mathf.Deg2Rad) * len * 0.5f;
            Line(x - dx, y - dy, x + dx, y + dy, w, c, a);
        }

        /// <summary>折れ線 (点を順につなぐ)</summary>
        public void Path(IList<Vector2> pts, float w, Color c, float a = 1f)
        {
            for (int i = 0; i + 1 < pts.Count; i++) Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, w, c, a);
        }

        /// <summary>
        /// 稜線 (x ごとの高さ heights) より下を塗る。上の方ほど top の色、下ほど bottom の色。
        /// 塗った所に、横向きの短い筆のあとを散らして絵の具らしくする
        /// </summary>
        public void Ridge(float[] heights, float floorY, Color top, Color bottom, int strokes, float jitter)
        {
            float maxH = floorY;
            foreach (var h in heights) maxH = Mathf.Max(maxH, h);
            for (int x = 0; x < W; x++)
            {
                int hy = Mathf.Min(H - 1, Mathf.RoundToInt(heights[x]));
                for (int y = Mathf.Max(0, Mathf.FloorToInt(floorY)); y <= hy; y++)
                    Blend(x, y, Color.Lerp(bottom, top, Mathf.InverseLerp(floorY, maxH, y)), 1f);
            }
            for (int i = 0; i < strokes; i++)
            {
                int x = RI(0, W);
                float y = R(floorY, heights[x]);
                if (y >= heights[x] - 2f) continue;
                var c = Jit(Color.Lerp(bottom, top, Mathf.InverseLerp(floorY, maxH, y)), jitter);
                Stroke(x, y, R(-15f, 15f), R(6f, 14f), R(2f, 3.5f), c, 0.8f);
            }
        }

        /// <summary>稜線の高さ (なめらかな起伏 + 細かいでこぼこ)</summary>
        public float[] RidgeLine(float baseY, float amp, float freq, float rough, float seed)
        {
            var h = new float[W];
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                h[x] = baseY + amp * (Mathf.Sin(u * freq + seed) * 0.6f + Mathf.Sin(u * freq * 2.3f + seed * 1.7f) * 0.3f)
                     + rough * (Noise(u * 18f + seed, seed) - 0.5f);
            }
            return h;
        }

        /// <summary>全体をぼかす (半径 r の箱)。肖像画のやわらかい陰影に</summary>
        public void Blur(int r)
        {
            if (r <= 0) return;
            var tmp = new Color[px.Length];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color s = Color.clear;
                    int n = 0;
                    for (int k = -r; k <= r; k++) { s += px[y * W + Mathf.Clamp(x + k, 0, W - 1)]; n++; }
                    tmp[y * W + x] = s / n;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color s = Color.clear;
                    int n = 0;
                    for (int k = -r; k <= r; k++) { s += tmp[Mathf.Clamp(y + k, 0, H - 1) * W + x]; n++; }
                    var c = s / n;
                    c.a = 1f;
                    px[y * W + x] = c;
                }
        }

        /// <summary>キャンバスの布目 (細かい明るさのむら)</summary>
        public void Grain(float amount)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float g = ((x + y) % 3 == 0 ? 0.6f : 0f) + ((x * 7 + y * 3) % 5 == 0 ? -0.6f : 0f) + (R() - 0.5f);
                    var c = px[y * W + x];
                    float d = g * amount;
                    px[y * W + x] = new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), 1f);
                }
        }

        /// <summary>なめらかなノイズ (0〜1)</summary>
        public static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }
    }
}
