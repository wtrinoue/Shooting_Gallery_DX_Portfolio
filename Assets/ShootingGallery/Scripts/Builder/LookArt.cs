using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 別の世界へ飛ぶモードの的の絵 (ステッカーのような1枚の絵)。Painter の透明なキャンバス (Size × Size ピクセル) に描く。
    /// 真ん中の丸い背景 (BgCenter / BgRadius) から、モチーフが外へ大胆にはみ出し、いちばん下にリボン (名前の文字は GalleryBuilder が TextMesh で重ねる)、
    /// まわりに黒いふち (Outline) を付ける。座標はピクセル (左下が原点、上が +y)。
    /// GalleryBuilder に頼らない (Unity の外でも PNG に描き出して確かめられるように)。描いた絵は GalleryBuilder.StickerLook が板に貼る。
    /// </summary>
    public static class LookArt
    {
        public const int Size = 256;
        // 絵は 0〜Size の座標で描き、全体を Fit 倍に縮めてまわりに余白を作る (はみ出した所の黒いふちが、キャンバスの端で切れないように)
        const float Fit = 0.88f;
        static float Margin => Size * (1f - Fit) * 0.5f;
        const float RibbonDrawY = 35f;
        /// <summary>リボンの真ん中の高さ (キャンバスのピクセル。名前の文字を置く所)</summary>
        public static float RibbonY => RibbonDrawY * Fit + Margin;
        const float BgX = 128f, BgY = 142f, BgR = 90f;

        static readonly Color Ink = new Color(0.06f, 0.05f, 0.08f); // ふちと線の色
        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>モードの名前と、その絵を描く関数</summary>
        public static readonly Dictionary<string, System.Action<Painter>> Designs = new Dictionary<string, System.Action<Painter>>
        {
            { "Naval", Naval }, { "Sky", Sky }, { "Ninja", Ninja }, { "Kaiju", Kaiju }, { "Casino", Casino }, { "Western", Western },
            { "Ghost", Ghost }, { "Mine", Mine }, { "Festival", Festival }, { "Submarine", Submarine }, { "Music", Music },
            { "Museum", Museum }, { "Unknown", Unknown }, { "Space", Space },
        };

        /// <summary>name の絵を描いた Painter (ふち付き)。ribbon = リボンの色</summary>
        public static Painter Draw(string name)
        {
            var p = new Painter(Size, Size, name.Length * 97 + name[0], true) { Scale = Fit, Offset = Vector2.one * Margin };
            Designs[name](p);
            p.Outline(4.5f, Ink);
            return p;
        }

        // ------------------------------------------------------------------
        // 道具
        // ------------------------------------------------------------------

        static void Circle(Painter p, float x, float y, float r, Color c, float a = 1f) => p.Ellipse(x, y, r, r, c, a);

        // 丸い背景: 外側の色から内側の色へ (何重かの円で、なめらかに)
        static void Backdrop(Painter p, Color outer, Color inner)
        {
            for (int i = 0; i < 6; i++)
            {
                float k = i / 5f;
                Circle(p, BgX, BgY + k * 10f, BgR * (1f - k * 0.5f), Color.Lerp(outer, inner, k));
            }
        }

        // 円の線 (輪)
        static void Ring(Painter p, float x, float y, float r, float w, Color c, float a = 1f, float from = 0f, float to = 360f)
        {
            var pts = new List<Vector2>();
            int n = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(to - from) / 6f));
            for (int i = 0; i <= n; i++)
            {
                float t = Mathf.Lerp(from, to, i / (float)n) * Mathf.Deg2Rad;
                pts.Add(V(x + Mathf.Cos(t) * r, y + Mathf.Sin(t) * r));
            }
            p.Path(pts, w, c, a);
        }

        // 角の丸い四角
        static void RoundRect(Painter p, float x0, float y0, float x1, float y1, float r, Color c)
        {
            p.Rect(x0 + r, y0, x1 - r, y1, c);
            p.Rect(x0, y0 + r, x1, y1 - r, c);
            Circle(p, x0 + r, y0 + r, r, c);
            Circle(p, x1 - r, y0 + r, r, c);
            Circle(p, x0 + r, y1 - r, r, c);
            Circle(p, x1 - r, y1 - r, r, c);
        }

        // 点の並びを center のまわりに deg 度回し、scale 倍して offset へ動かす
        static List<Vector2> Tf(IList<Vector2> pts, Vector2 offset, float deg, float scale = 1f)
        {
            float cs = Mathf.Cos(deg * Mathf.Deg2Rad), sn = Mathf.Sin(deg * Mathf.Deg2Rad);
            var r = new List<Vector2>(pts.Count);
            foreach (var q in pts) r.Add(offset + new Vector2(q.x * cs - q.y * sn, q.x * sn + q.y * cs) * scale);
            return r;
        }

        static Vector2 Tf(Vector2 q, Vector2 offset, float deg, float scale = 1f) => Tf(new[] { q }, offset, deg, scale)[0];

        // キラッと光る4方の星
        static void Sparkle(Painter p, float x, float y, float r, Color c)
        {
            p.Poly(new[] { V(x, y + r), V(x + r * 0.22f, y + r * 0.22f), V(x + r, y), V(x + r * 0.22f, y - r * 0.22f), V(x, y - r), V(x - r * 0.22f, y - r * 0.22f), V(x - r, y), V(x - r * 0.22f, y + r * 0.22f) }, c);
        }

        // 5方の星
        static void Star(Painter p, float x, float y, float r, Color c, float deg = 90f)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float t = (deg + i * 36f) * Mathf.Deg2Rad;
                float rr = i % 2 == 0 ? r : r * 0.45f;
                pts.Add(V(x + Mathf.Cos(t) * rr, y + Mathf.Sin(t) * rr));
            }
            p.Poly(pts, c);
        }

        // いちばん下のリボン (両はしは後ろに折り返した切り込みのある帯)
        static void Ribbon(Painter p, Color c)
        {
            var dark = c * 0.6f;
            dark.a = 1f;
            p.Poly(new[] { V(4, 16), V(44, 16), V(44, 46), V(4, 46), V(18, 31) }, dark);
            p.Poly(new[] { V(252, 16), V(212, 16), V(212, 46), V(252, 46), V(238, 31) }, dark);
            p.Poly(new[] { V(26, 20), V(230, 20), V(230, 52), V(26, 52) }, c);
            p.Line(30, 49, 226, 49, 2f, Color.Lerp(c, Color.white, 0.4f));
        }

        // ------------------------------------------------------------------
        // 海戦: 堂々とした帆船と、錨とその縄。下に波
        // ------------------------------------------------------------------
        static void Naval(Painter p)
        {
            Backdrop(p, C(0.35f, 0.62f, 0.95f), C(0.75f, 0.9f, 1f));
            p.Glow(184, 196, 46, C(1f, 0.97f, 0.8f), 0.9f);
            // 帆船 (右向き)
            var wood = C(0.45f, 0.24f, 0.11f);
            var mast = C(0.3f, 0.16f, 0.07f);
            var sail = C(0.98f, 0.95f, 0.86f);
            p.Line(108, 104, 108, 250, 6f, mast);
            p.Line(162, 104, 162, 240, 6f, mast);
            p.Line(205, 112, 236, 158, 4f, mast); // 船首の斜めの帆柱
            foreach (var (x, top, bottom, w) in new[] { (108f, 236f, 176f, 34f), (108f, 172f, 116f, 42f), (162f, 228f, 172f, 30f), (162f, 168f, 118f, 38f) })
            {
                var pts = new List<Vector2> { V(x - w, top), V(x + w, top) };
                for (int i = 0; i <= 6; i++) { float t = i / 6f; pts.Add(V(x + w + 10f * Mathf.Sin(t * Mathf.PI), Mathf.Lerp(top, bottom, t))); }
                pts.Add(V(x - w, bottom));
                p.Poly(pts, sail);
                p.Line(x - w, top, x + w, top, 3f, mast);
            }
            p.Poly(new[] { V(206, 116), V(234, 156), V(206, 150) }, sail);
            foreach (var x in new[] { 108f, 162f })
                p.Poly(new[] { V(x, 252), V(x + 26, 246), V(x, 240) }, C(0.85f, 0.12f, 0.12f));
            p.Poly(new[] { V(52, 118), V(214, 118), V(232, 128), V(222, 92), V(70, 86) }, wood);
            p.Rect(52, 118, 92, 136, wood);
            p.Line(60, 106, 220, 110, 4f, C(0.95f, 0.75f, 0.25f));
            for (int i = 0; i < 6; i++) Circle(p, 86 + i * 22, 98, 4f, C(0.15f, 0.08f, 0.04f));
            // 波 (横いっぱい、白いしぶき)
            var sea = C(0.1f, 0.35f, 0.75f);
            var crest = new List<Vector2> { V(-4, 56) };
            for (int x = -4; x <= 260; x += 8) crest.Add(V(x, 100 + 9f * Mathf.Sin(x * 0.06f) + 5f * Mathf.Sin(x * 0.17f)));
            crest.Add(V(260, 56));
            p.Poly(crest, sea);
            var light = new List<Vector2> { V(-4, 56) };
            for (int x = -4; x <= 260; x += 8) light.Add(V(x, 82 + 7f * Mathf.Sin(x * 0.08f + 1.5f)));
            light.Add(V(260, 56));
            p.Poly(light, C(0.2f, 0.55f, 0.9f));
            for (int x = 6; x < 256; x += 30)
            {
                float y = 100 + 9f * Mathf.Sin(x * 0.06f) + 5f * Mathf.Sin(x * 0.17f);
                Circle(p, x, y + 2, 9f, Color.white);
                Circle(p, x + 9, y + 6, 6f, Color.white);
                p.Ellipse(x - 4, y - 6, 10, 4, C(0.85f, 0.95f, 1f));
            }
            // 錨と縄 (手前の左。はみ出して大きく)
            var gold = C(0.98f, 0.78f, 0.22f);
            var goldDark = C(0.75f, 0.52f, 0.1f);
            var rope = C(0.88f, 0.74f, 0.5f);
            var ropePts = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                ropePts.Add(V(Mathf.Lerp(60, 26, t) + 22f * Mathf.Sin(t * Mathf.PI * 1.6f), Mathf.Lerp(206, 244, t) - 16f * Mathf.Sin(t * Mathf.PI)));
            }
            p.Path(ropePts, 9f, rope);
            for (int i = 0; i < ropePts.Count - 1; i += 2)
            {
                var d = (ropePts[i + 1] - ropePts[i]).normalized;
                var n = new Vector2(-d.y, d.x) * 3.5f;
                p.Line(ropePts[i].x - n.x, ropePts[i].y - n.y, ropePts[i].x + n.x + d.x * 3f, ropePts[i].y + n.y + d.y * 3f, 1.6f, C(0.6f, 0.45f, 0.25f));
            }
            Ring(p, 60, 206, 13f, 7f, gold);
            p.Line(60, 192, 60, 70, 14f, gold);
            p.Line(66, 192, 66, 76, 3f, goldDark);
            p.Line(34, 178, 86, 178, 11f, gold);
            Circle(p, 34, 178, 7f, gold);
            Circle(p, 86, 178, 7f, gold);
            Ring(p, 60, 112, 42f, 12f, gold, 1f, 195f, 345f);
            p.Poly(new[] { V(10, 112), V(30, 132), V(34, 102) }, gold);
            p.Poly(new[] { V(110, 112), V(90, 132), V(86, 102) }, gold);
            p.Poly(new[] { V(48, 74), V(72, 74), V(60, 56) }, gold);
            Ribbon(p, C(0.1f, 0.22f, 0.55f));
        }

        // ------------------------------------------------------------------
        // 空中: 斜め上へ飛ぶ赤い複葉機が、雲と空の中で機関銃を撃つ
        // ------------------------------------------------------------------
        static void Sky(Painter p)
        {
            Backdrop(p, C(0.3f, 0.6f, 0.98f), C(0.7f, 0.88f, 1f));
            var cloud = Color.white;
            var cloudShade = C(0.82f, 0.88f, 0.97f);
            void Cloud(float x, float y, float s)
            {
                p.Ellipse(x, y - 4 * s, 34 * s, 12 * s, cloudShade);
                Circle(p, x - 18 * s, y, 14 * s, cloud);
                Circle(p, x + 2 * s, y + 8 * s, 18 * s, cloud);
                Circle(p, x + 22 * s, y + 1 * s, 13 * s, cloud);
                p.Ellipse(x, y - 4 * s, 34 * s, 9 * s, cloud);
            }
            Cloud(214, 214, 1.2f);
            Cloud(36, 92, 1.4f);
            Cloud(150, 78, 0.9f);
            // 飛んできた跡 (左下へ流れる線)
            var o = V(122, 140);
            const float Ang = 32f;
            for (int i = 0; i < 4; i++)
            {
                var a = Tf(V(-90 - i * 6, -30 + i * 20), o, Ang);
                var b = Tf(V(-120 - i * 6, -30 + i * 20), o, Ang);
                p.Line(a.x, a.y, b.x, b.y, 3f, Color.white, 0.85f);
            }
            // 機関銃の弾 (前へ光の筋)
            var tracer = C(1f, 0.85f, 0.25f);
            foreach (var dy in new[] { -8f, 10f })
                for (int k = 0; k < 2; k++)
                {
                    var a = Tf(V(96 + k * 28, dy), o, Ang);
                    var b = Tf(V(112 + k * 28, dy), o, Ang);
                    p.Glow(a.x, a.y, 10, tracer, 0.6f);
                    p.Line(a.x, a.y, b.x, b.y, 4f, tracer);
                }
            // 赤い複葉機 (右向きに描いて、斜め上へ回す)
            var red = C(0.85f, 0.14f, 0.12f);
            var redDark = C(0.6f, 0.08f, 0.08f);
            var steel = C(0.55f, 0.57f, 0.62f);
            p.Poly(Tf(new[] { V(-80, 6), V(-104, 40), V(-114, 40), V(-104, 4) }, o, Ang), redDark);            // 尾翼
            p.Poly(Tf(new[] { V(-34, 30), V(40, 30), V(44, 42), V(-30, 42) }, o, Ang), red);                    // 上の翼
            foreach (var x in new[] { -20f, 24f })
            {
                var a = Tf(V(x, 30), o, Ang);
                var b = Tf(V(x + 4, -8), o, Ang);
                p.Line(a.x, a.y, b.x, b.y, 3f, C(0.2f, 0.12f, 0.08f));
            }
            var body = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f * Mathf.PI * 2f;
                float rx = Mathf.Cos(t) > 0f ? 78f : 96f;
                body.Add(V(Mathf.Cos(t) * rx, Mathf.Sin(t) * (Mathf.Cos(t) > 0f ? 18f : 14f - 6f * -Mathf.Cos(t))));
            }
            p.Poly(Tf(body, o, Ang), red);
            p.Poly(Tf(new[] { V(-40, -12), V(36, -12), V(40, -2), V(-36, -2) }, o, Ang), redDark);              // 下の翼
            var cowl = Tf(V(70, 0), o, Ang);
            p.Ellipse(cowl.x, cowl.y, 14, 18, steel, 1f, Ang);
            var star = Tf(V(-40, 2), o, Ang);
            Circle(p, star.x, star.y, 11f, Color.white);
            Star(p, star.x, star.y, 9f, red, 90f + Ang);
            var pilot = Tf(V(2, 22), o, Ang);
            Circle(p, pilot.x, pilot.y, 10f, C(0.45f, 0.3f, 0.18f));
            var goggle = Tf(V(9, 24), o, Ang);
            Circle(p, goggle.x, goggle.y, 4.5f, C(0.6f, 0.85f, 1f));
            var scarf = Tf(new[] { V(-4, 16), V(-34, 26), V(-30, 14) }, o, Ang);
            p.Poly(scarf, Color.white);
            // プロペラ (回っている丸い影と、2枚の羽根)
            var prop = Tf(V(84, 0), o, Ang);
            p.Ellipse(prop.x, prop.y, 8, 40, C(0.85f, 0.85f, 0.9f), 0.45f, Ang);
            p.Ellipse(prop.x, prop.y, 4, 38, C(0.2f, 0.15f, 0.1f), 1f, Ang + 8f);
            // 銃口の火花
            var flash = Tf(V(94, 6), o, Ang);
            Sparkle(p, flash.x, flash.y, 16f, C(1f, 0.9f, 0.4f));
            Ribbon(p, C(0.8f, 0.16f, 0.14f));
        }

        // ------------------------------------------------------------------
        // 忍者: 満月と松の枝の前で、忍者が跳び、大きな手裏剣が飛ぶ
        // ------------------------------------------------------------------
        static void Ninja(Painter p)
        {
            Backdrop(p, C(0.1f, 0.08f, 0.28f), C(0.22f, 0.16f, 0.42f));
            // 大きな満月 (跳ぶ忍者の影がくっきり見えるように、忍者の真後ろに)
            p.Glow(116, 136, 96, C(1f, 0.95f, 0.75f), 0.55f);
            Circle(p, 116, 136, 68f, C(1f, 0.95f, 0.72f));
            p.Ellipse(80, 176, 12, 8, C(0.93f, 0.86f, 0.6f));
            p.Ellipse(150, 100, 15, 10, C(0.93f, 0.86f, 0.6f));
            p.Ellipse(160, 168, 8, 6, C(0.93f, 0.86f, 0.6f));
            // 松の枝 (右上から。はみ出して)
            var trunk = C(0.3f, 0.18f, 0.1f);
            p.Path(new[] { V(262, 236), V(222, 214), V(186, 222), V(150, 214), V(118, 226), V(92, 220) }, 13f, trunk);
            p.Path(new[] { V(186, 222), V(170, 246), V(150, 258) }, 8f, trunk);
            var pineDark = C(0.08f, 0.32f, 0.16f);
            var pine = C(0.16f, 0.5f, 0.24f);
            foreach (var (x, y, s) in new[] { (236f, 236f, 1.1f), (196f, 238f, 1f), (152f, 232f, 1.15f), (104f, 236f, 1f), (164f, 254f, 0.8f), (226f, 208f, 0.8f) })
            {
                p.Ellipse(x, y, 30 * s, 13 * s, pineDark);
                p.Ellipse(x + 2, y + 5 * s, 24 * s, 8 * s, pine);
                for (int k = -3; k <= 3; k++) p.Line(x + k * 7 * s, y + 2, x + k * 9 * s, y + 13 * s, 1.5f, C(0.3f, 0.65f, 0.35f));
            }
            // 跳んでいる忍者 (紺色。赤いはちまきと襟巻き)
            var navy = C(0.1f, 0.11f, 0.24f);
            var scarf = C(0.85f, 0.12f, 0.1f);
            p.Path(new[] { V(96, 152), V(66, 160), V(40, 150), V(20, 158) }, 9f, scarf);
            p.Line(104, 100, 70, 80, 15f, navy);
            p.Line(70, 80, 46, 92, 13f, navy);
            p.Line(116, 98, 104, 66, 15f, navy);
            p.Line(104, 66, 126, 52, 13f, navy);
            p.Ellipse(112, 120, 20, 32, navy, 1f, -32f);
            p.Line(124, 136, 152, 150, 12f, navy);
            p.Line(152, 150, 174, 146, 10f, navy);
            p.Line(100, 138, 80, 162, 11f, navy);
            Circle(p, 100, 156, 21f, navy);
            p.Line(80, 162, 120, 160, 6f, scarf);
            p.Poly(new[] { V(82, 160), V(60, 172), V(64, 160) }, scarf);
            p.Ellipse(104, 156, 12, 5, C(0.95f, 0.8f, 0.65f));
            p.Line(98, 156, 104, 157, 2.5f, Ink);
            p.Line(108, 157, 113, 156, 2.5f, Ink);
            // 手裏剣 (紺色の丸から四方へ灰色の三角形)
            void Shuriken(float x, float y, float r, float deg)
            {
                var blade = C(0.62f, 0.64f, 0.68f);
                var bladeLight = C(0.82f, 0.84f, 0.88f);
                for (int i = 0; i < 4; i++)
                {
                    float a = deg + i * 90f;
                    var tip = Tf(V(r, 0), V(x, y), a);
                    var l = Tf(V(r * 0.12f, r * 0.3f), V(x, y), a);
                    var rr = Tf(V(r * 0.12f, -r * 0.3f), V(x, y), a);
                    p.Poly(new[] { tip, l, rr }, blade);
                    p.Poly(new[] { tip, l, V(x, y) }, bladeLight);
                }
                Circle(p, x, y, r * 0.34f, C(0.08f, 0.12f, 0.38f));
                Circle(p, x, y, r * 0.12f, C(0.3f, 0.35f, 0.6f));
            }
            for (int k = 0; k < 3; k++) p.Line(150 - k * 18, 108 + k * 8, 112 - k * 18, 100 + k * 8, 3f, Color.white, 0.7f);
            Shuriken(192, 96, 56f, 18f);
            Shuriken(186, 150, 15f, 40f);
            Ribbon(p, C(0.38f, 0.12f, 0.48f));
        }

        // ------------------------------------------------------------------
        // 怪獣: 町のビルの間から、恐竜の顔と胴体 (足は見えない) がそびえて吠える
        // ------------------------------------------------------------------
        static void Kaiju(Painter p)
        {
            Backdrop(p, C(0.95f, 0.45f, 0.2f), C(1f, 0.82f, 0.45f));
            var back = C(0.25f, 0.22f, 0.36f);
            foreach (var (x0, x1, h) in new[] { (20f, 58f, 150f), (176f, 214f, 160f), (214f, 246f, 136f), (60f, 84f, 128f) })
            {
                p.Rect(x0, 56, x1, h, back);
                for (float y = 66; y < h - 8; y += 12)
                    for (float x = x0 + 5; x < x1 - 6; x += 10)
                        if (((int)(x * 7 + y * 3) % 5) != 0) p.Rect(x, y, x + 4, y + 6, C(1f, 0.9f, 0.5f));
            }
            // 恐竜 (緑)。背中のとげ → 胴体 → 首と頭 → 口の中・歯 → 目
            var green = C(0.28f, 0.62f, 0.28f);
            var greenDark = C(0.16f, 0.42f, 0.18f);
            var belly = C(0.82f, 0.85f, 0.55f);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                var b = V(Mathf.Lerp(72, 120, t), Mathf.Lerp(108, 200, t));
                p.Poly(new[] { b + V(-10, -6), b + V(-26, 10), b + V(6, 6) }, C(0.95f, 0.85f, 0.3f));
            }
            p.Ellipse(112, 104, 50, 66, green, 1f, -12f);
            p.Ellipse(124, 98, 26, 50, belly, 1f, -12f);
            for (int i = 0; i < 5; i++) p.Line(108, 70 + i * 16, 140, 74 + i * 16, 2f, C(0.65f, 0.68f, 0.4f));
            p.Poly(new[] { V(96, 150), V(140, 222), V(176, 196), V(146, 132) }, green);
            p.Poly(new[] { V(150, 206), V(236, 196), V(232, 176), V(156, 182) }, C(0.55f, 0.08f, 0.1f));     // 口の中
            p.Poly(new[] { V(124, 222), V(148, 248), V(214, 246), V(250, 226), V(246, 206), V(156, 204) }, green); // 上あご
            p.Poly(new[] { V(150, 186), V(234, 180), V(226, 160), V(162, 160) }, green);                      // 下あご
            p.Poly(new[] { V(162, 166), V(222, 166), V(226, 160), V(162, 160) }, greenDark);
            for (float x = 166; x < 240; x += 13)
            {
                p.Poly(new[] { V(x, 205), V(x + 9, 205), V(x + 4.5f, 192) }, Color.white);
                if (x < 224) p.Poly(new[] { V(x + 4, 183), V(x + 12, 183), V(x + 8, 195) }, Color.white);
            }
            Circle(p, 238, 236, 3.5f, greenDark);
            Circle(p, 186, 230, 11f, C(1f, 0.9f, 0.2f));
            p.Ellipse(187, 230, 3.5f, 9f, Ink);
            p.Line(172, 244, 200, 238, 4f, greenDark);
            // 小さな腕と爪
            p.Line(150, 132, 176, 122, 10f, green);
            p.Line(176, 122, 186, 128, 4f, Color.white);
            p.Line(176, 122, 188, 118, 4f, Color.white);
            // 手前のビル (胴体の下を隠す = 町の中にいる)
            var front = C(0.14f, 0.15f, 0.26f);
            foreach (var (x0, x1, h) in new[] { (-4f, 34f, 112f), (34f, 74f, 92f), (74f, 104f, 120f), (104f, 150f, 96f), (150f, 182f, 116f), (182f, 222f, 100f), (222f, 260f, 118f) })
            {
                p.Rect(x0, 50, x1, h, front);
                p.Rect(x0, h - 3, x1, h, C(0.25f, 0.27f, 0.4f));
                for (float y = 60; y < h - 10; y += 13)
                    for (float x = x0 + 5; x < x1 - 6; x += 11)
                        if (((int)(x * 3 + y * 5) % 4) != 0) p.Rect(x, y, x + 5, y + 7, C(1f, 0.85f, 0.4f));
            }
            Ribbon(p, C(0.15f, 0.5f, 0.2f));
        }

        // 7 の形 (x, y = 真ん中、h = 高さ)
        static List<Vector2> SevenShape(float x, float y, float h)
        {
            float w = h * 0.72f;
            var unit = new[] { V(0, 1), V(1, 1), V(1, 0.84f), V(0.48f, 0), V(0.2f, 0), V(0.72f, 0.82f), V(0, 0.82f) };
            var r = new List<Vector2>();
            foreach (var u in unit) r.Add(V(x - w / 2 + u.x * w, y - h / 2 + u.y * h));
            return r;
        }

        // ------------------------------------------------------------------
        // カジノ: 大きなラッキーセブンと、スロットマシンと、こぼれるコイン
        // ------------------------------------------------------------------
        static void Casino(Painter p)
        {
            Backdrop(p, C(0.5f, 0.04f, 0.14f), C(0.85f, 0.15f, 0.3f));
            var gold = C(1f, 0.8f, 0.22f);
            var goldDark = C(0.8f, 0.55f, 0.1f);
            Ring(p, BgX, BgY, BgR - 4, 6f, gold);
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f * Mathf.Deg2Rad;
                Circle(p, BgX + Mathf.Cos(a) * (BgR - 4), BgY + Mathf.Sin(a) * (BgR - 4), 4.5f, C(1f, 0.97f, 0.75f));
            }
            // スロットマシン (左下)
            RoundRect(p, 18, 66, 116, 168, 12, C(0.85f, 0.1f, 0.15f));
            RoundRect(p, 24, 152, 110, 182, 10, gold);
            p.Rect(28, 92, 106, 140, C(0.08f, 0.06f, 0.1f));
            for (int i = 0; i < 3; i++)
            {
                p.Rect(31 + i * 25, 95, 53 + i * 25, 137, Color.white);
                p.Poly(SevenShape(42 + i * 25, 116, 30), C(0.9f, 0.1f, 0.12f));
            }
            p.Line(122, 150, 132, 182, 5f, C(0.6f, 0.6f, 0.65f));
            Circle(p, 133, 186, 9f, C(0.9f, 0.1f, 0.12f));
            p.Rect(36, 70, 98, 84, C(0.2f, 0.05f, 0.08f));
            // 大きなラッキーセブン (金のふち + 赤)
            var big = SevenShape(160, 166, 168);
            var outline = new List<Vector2>();
            foreach (var q in big) outline.Add(q + (q - V(160, 166)).normalized * 9f);
            p.Poly(outline, gold);
            p.Poly(big, C(0.92f, 0.1f, 0.12f));
            p.Poly(SevenShape(154, 172, 120), C(1f, 0.35f, 0.3f), 0.5f);
            // コイン (こぼれて飛び出す)
            void Coin(float x, float y, float r, float tilt)
            {
                p.Ellipse(x, y - 2, r, r * tilt, goldDark);
                p.Ellipse(x, y, r, r * tilt, gold);
                p.Ellipse(x, y, r * 0.65f, r * 0.65f * tilt, C(1f, 0.9f, 0.45f));
                p.Line(x, y - r * 0.4f * tilt, x, y + r * 0.4f * tilt, 2.5f, goldDark);
            }
            foreach (var (x, y, r, t) in new[] { (206f, 70f, 18f, 0.5f), (232f, 92f, 14f, 0.9f), (180f, 60f, 16f, 0.45f), (24f, 210f, 15f, 0.8f), (244f, 150f, 12f, 1f), (40f, 238f, 11f, 0.6f), (226f, 228f, 13f, 0.7f) })
                Coin(x, y, r, t);
            Sparkle(p, 214, 196, 14f, Color.white);
            Sparkle(p, 66, 206, 10f, Color.white);
            Ribbon(p, C(0.75f, 0.08f, 0.15f));
        }

        // ------------------------------------------------------------------
        // 西部劇: 煙を吐く蒸気機関車と、帽子のカウボーイ
        // ------------------------------------------------------------------
        static void Western(Painter p)
        {
            Backdrop(p, C(0.95f, 0.55f, 0.2f), C(1f, 0.85f, 0.45f));
            p.Glow(150, 168, 50, C(1f, 0.95f, 0.7f), 0.8f);
            var mesa = C(0.6f, 0.3f, 0.15f);
            p.Poly(new[] { V(50, 96), V(64, 136), V(104, 138), V(116, 96) }, mesa);
            p.Poly(new[] { V(170, 96), V(182, 124), V(218, 126), V(228, 96) }, mesa);
            p.Rect(36, 70, 230, 98, C(0.85f, 0.62f, 0.35f));
            // 煙 (上へ大きくはみ出す)
            var smoke = C(0.8f, 0.8f, 0.82f);
            foreach (var (x, y, r) in new[] { (214f, 186f, 13f), (204f, 202f, 17f), (188f, 218f, 20f), (164f, 230f, 22f), (134f, 238f, 22f), (104f, 242f, 18f) })
            {
                Circle(p, x, y, r, smoke);
                Circle(p, x - r * 0.3f, y + r * 0.3f, r * 0.6f, Color.white, 0.6f);
            }
            // 機関車 (右向き。右へはみ出す)
            var black = C(0.12f, 0.12f, 0.14f);
            var red = C(0.75f, 0.12f, 0.1f);
            var brass = C(0.95f, 0.75f, 0.3f);
            p.Rect(150, 104, 246, 146, black);
            Circle(p, 246, 125, 21f, C(0.25f, 0.25f, 0.28f));
            Circle(p, 246, 125, 9f, C(1f, 0.95f, 0.6f));
            p.Glow(250, 125, 30, C(1f, 0.95f, 0.6f), 0.6f);
            for (int i = 0; i < 3; i++) p.Line(166 + i * 26, 104, 166 + i * 26, 146, 3f, brass);
            p.Poly(new[] { V(206, 146), V(220, 146), V(228, 178), V(198, 178) }, black);
            p.Rect(196, 176, 230, 184, black);
            Circle(p, 184, 150, 10f, brass);
            p.Rect(118, 98, 156, 176, red);
            p.Rect(112, 174, 162, 182, black);
            p.Rect(126, 140, 148, 164, C(1f, 0.85f, 0.45f));
            p.Poly(new[] { V(244, 100), V(262, 78), V(262, 100) }, red);
            p.Rect(116, 92, 250, 104, black);
            foreach (var (x, r) in new[] { (140f, 22f), (186f, 22f), (232f, 13f) })
            {
                Circle(p, x, 88, r, red);
                Circle(p, x, 88, r * 0.7f, black);
                for (int k = 0; k < 6; k++) { float a = k * 30f * Mathf.Deg2Rad; p.Line(x - Mathf.Cos(a) * r * 0.7f, 88 - Mathf.Sin(a) * r * 0.7f, x + Mathf.Cos(a) * r * 0.7f, 88 + Mathf.Sin(a) * r * 0.7f, 2.5f, red); }
                Circle(p, x, 88, 4f, brass);
            }
            p.Line(140, 82, 186, 82, 4f, C(0.6f, 0.6f, 0.65f));
            // カウボーイ (手前の左。はみ出して)
            var skin = C(0.95f, 0.75f, 0.58f);
            var hat = C(0.55f, 0.32f, 0.15f);
            var shirt = C(0.85f, 0.2f, 0.15f);
            var vest = C(0.38f, 0.22f, 0.1f);
            p.Ellipse(62, 96, 34, 52, shirt);
            p.Poly(new[] { V(32, 60), V(48, 60), V(56, 132), V(36, 120) }, vest);
            p.Poly(new[] { V(92, 60), V(76, 60), V(68, 132), V(88, 120) }, vest);
            Star(p, 44, 104, 8f, C(1f, 0.85f, 0.3f));
            p.Line(86, 112, 118, 128, 11f, shirt);
            Circle(p, 120, 129, 7f, skin);
            p.Rect(118, 128, 150, 136, C(0.45f, 0.45f, 0.5f));          // 拳銃
            p.Rect(114, 120, 124, 134, C(0.35f, 0.22f, 0.12f));
            p.Poly(new[] { V(44, 136), V(80, 136), V(62, 116) }, C(0.15f, 0.35f, 0.75f)); // バンダナ
            Circle(p, 62, 150, 20f, skin);
            Circle(p, 55, 152, 2.5f, Ink);
            Circle(p, 69, 152, 2.5f, Ink);
            p.Line(56, 142, 68, 142, 2f, C(0.5f, 0.25f, 0.2f));
            p.Ellipse(62, 168, 46, 9, hat);
            RoundRect(p, 44, 166, 80, 198, 8, hat);
            p.Ellipse(62, 198, 16, 4, C(0.42f, 0.24f, 0.1f));
            p.Rect(44, 170, 80, 176, C(0.25f, 0.12f, 0.06f));
            Ribbon(p, C(0.55f, 0.3f, 0.12f));
        }

        // ------------------------------------------------------------------
        // お化け: 月夜の洋館と、大きな白いお化け
        // ------------------------------------------------------------------
        static void Ghost(Painter p)
        {
            Backdrop(p, C(0.2f, 0.07f, 0.32f), C(0.42f, 0.2f, 0.58f));
            p.Glow(176, 204, 50, C(1f, 0.95f, 0.8f), 0.7f);
            Circle(p, 176, 204, 28f, C(1f, 0.96f, 0.82f));
            // 洋館 (黒い影絵に、黄色く灯る窓)
            var house = C(0.1f, 0.05f, 0.15f);
            var lit = C(1f, 0.85f, 0.35f);
            p.Rect(64, 64, 196, 156, house);
            p.Poly(new[] { V(56, 156), V(204, 156), V(130, 200) }, house);
            p.Rect(36, 64, 82, 196, house);
            p.Poly(new[] { V(28, 196), V(90, 196), V(59, 254) }, house);
            p.Line(59, 252, 59, 262, 2f, house);
            p.Rect(170, 140, 206, 184, house);
            p.Poly(new[] { V(164, 184), V(212, 184), V(188, 220) }, house);
            p.Rect(108, 196, 120, 222, house);
            foreach (var (x, y) in new[] { (48f, 170f), (48f, 132f), (48f, 96f), (96f, 126f), (124f, 126f), (152f, 126f), (180f, 160f), (96f, 88f), (152f, 88f) })
            {
                p.Rect(x, y, x + 16, y + 22, lit);
                p.Line(x + 8, y, x + 8, y + 22, 2f, house);
                p.Line(x, y + 11, x + 16, y + 11, 2f, house);
            }
            Circle(p, 130, 170, 9f, lit);
            p.Rect(118, 64, 140, 100, C(0.35f, 0.15f, 0.1f));
            for (float x = 4; x < 252; x += 14) p.Poly(new[] { V(x, 50), V(x + 6, 50), V(x + 6, 78), V(x + 3, 86), V(x, 78) }, house);
            p.Rect(0, 62, 256, 66, house);
            // こうもり
            void Bat(float x, float y, float s)
            {
                p.Poly(new[] { V(x, y), V(x - 16 * s, y + 8 * s), V(x - 11 * s, y + 2 * s), V(x - 22 * s, y + 3 * s), V(x - 8 * s, y - 6 * s) }, house);
                p.Poly(new[] { V(x, y), V(x + 16 * s, y + 8 * s), V(x + 11 * s, y + 2 * s), V(x + 22 * s, y + 3 * s), V(x + 8 * s, y - 6 * s) }, house);
                Circle(p, x, y, 4 * s, house);
            }
            Bat(150, 232, 1f);
            Bat(212, 242, 0.8f);
            Bat(22, 226, 0.9f);
            // 大きなお化け (手前の右。はみ出して)
            var sheet = C(0.97f, 0.97f, 1f);
            var shade = C(0.78f, 0.8f, 0.95f);
            var body = new List<Vector2> { V(152, 150) };
            for (int i = 0; i <= 8; i++)
            {
                float t = i / 8f;
                body.Add(V(Mathf.Lerp(150, 250, t), 62 + (i % 2 == 0 ? 0f : 14f)));
            }
            body.Add(V(240, 150));
            p.Poly(body, sheet);
            Circle(p, 196, 150, 46f, sheet);
            p.Ellipse(232, 112, 26, 12, sheet, 1f, 35f);
            p.Ellipse(158, 118, 24, 11, sheet, 1f, -35f);
            p.Ellipse(214, 94, 20, 30, shade, 0.6f);
            p.Ellipse(180, 160, 9, 14, Ink);
            p.Ellipse(212, 160, 9, 14, Ink);
            Circle(p, 183, 165, 3f, Color.white);
            Circle(p, 215, 165, 3f, Color.white);
            p.Ellipse(196, 132, 10, 13, Ink);
            p.Ellipse(176, 144, 7, 4, C(1f, 0.6f, 0.75f), 0.7f);
            p.Ellipse(218, 144, 7, 4, C(1f, 0.6f, 0.75f), 0.7f);
            Ribbon(p, C(0.4f, 0.15f, 0.55f));
        }

        // ------------------------------------------------------------------
        // 鉱山: つるはしと、結晶のような宝石を山盛りにしたトロッコ
        // ------------------------------------------------------------------
        static void Mine(Painter p)
        {
            Backdrop(p, C(0.28f, 0.18f, 0.1f), C(0.45f, 0.32f, 0.2f));
            foreach (var (x, y, r) in new[] { (70f, 200f, 16f), (196f, 210f, 12f), (60f, 120f, 10f), (210f, 120f, 14f) })
                p.Ellipse(x, y, r, r * 0.7f, C(0.22f, 0.14f, 0.08f));
            // つるはし (後ろに大きく斜めに。はみ出して)
            p.Line(28, 52, 196, 206, 13f, C(0.55f, 0.35f, 0.18f));
            p.Line(32, 50, 194, 200, 4f, C(0.7f, 0.5f, 0.3f));
            var steel = C(0.6f, 0.62f, 0.68f);
            var steelLight = C(0.85f, 0.87f, 0.92f);
            var head = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float t = Mathf.Lerp(-1f, 1f, i / 12f);
                head.Add(V(196 - 50 * t, 206 + 46 * t - 18 * (1 - t * t)));
            }
            p.Path(head, 17f, steel);
            p.Path(head, 6f, steelLight);
            p.Poly(new[] { head[0] + V(-6, -6), head[0] + V(16, -16), head[0] + V(6, 10) }, steel);
            p.Poly(new[] { head[12] + V(-10, 4), head[12] + V(-16, 14), head[12] + V(8, 4) }, steel);
            RoundRect(p, 184, 192, 208, 218, 5, C(0.4f, 0.4f, 0.45f));
            // 宝石 (結晶) をトロッコから山盛りに
            void Crystal(float x, float y, float w, float h, float deg, Color col)
            {
                var light = Color.Lerp(col, Color.white, 0.45f);
                var dark = col * 0.65f;
                dark.a = 1f;
                var pts = Tf(new[] { V(-w, 0), V(-w, h * 0.72f), V(0, h), V(w, h * 0.72f), V(w, 0) }, V(x, y), deg);
                p.Poly(pts, col);
                p.Poly(Tf(new[] { V(-w, 0), V(-w, h * 0.72f), V(0, h), V(0, 0) }, V(x, y), deg), light);
                p.Poly(Tf(new[] { V(w * 0.35f, 0), V(w * 0.35f, h * 0.76f), V(w, h * 0.72f), V(w, 0) }, V(x, y), deg), dark);
            }
            Crystal(84, 112, 15, 70, 18f, C(0.62f, 0.3f, 0.9f));
            Crystal(176, 112, 14, 66, -20f, C(0.25f, 0.85f, 0.95f));
            Crystal(130, 110, 18, 112, 0f, C(0.3f, 0.95f, 0.95f));
            Crystal(106, 112, 13, 84, 8f, C(1f, 0.4f, 0.7f));
            Crystal(154, 112, 13, 90, -8f, C(0.3f, 0.9f, 0.45f));
            Crystal(62, 108, 10, 46, 34f, C(1f, 0.75f, 0.2f));
            Crystal(198, 108, 10, 44, -36f, C(0.62f, 0.3f, 0.9f));
            // トロッコ
            var cart = C(0.42f, 0.3f, 0.22f);
            var iron = C(0.3f, 0.3f, 0.34f);
            p.Poly(new[] { V(44, 126), V(212, 126), V(196, 64), V(60, 64) }, cart);
            p.Rect(40, 118, 216, 130, iron);
            p.Rect(56, 84, 200, 92, iron);
            for (int i = 0; i < 6; i++) Circle(p, 66 + i * 25, 122, 3f, C(0.75f, 0.75f, 0.8f));
            foreach (var x in new[] { 84f, 172f })
            {
                Circle(p, x, 58, 19f, Ink);
                Circle(p, x, 58, 12f, iron);
                Circle(p, x, 58, 5f, C(0.75f, 0.75f, 0.8f));
            }
            Sparkle(p, 130, 232, 14f, Color.white);
            Sparkle(p, 70, 186, 9f, Color.white);
            Sparkle(p, 194, 178, 10f, Color.white);
            Ribbon(p, C(0.6f, 0.38f, 0.15f));
        }

        // ------------------------------------------------------------------
        // 夏祭り: 夜空の花火、ずらりと灯る提灯、射的のコルク銃
        // ------------------------------------------------------------------
        static void Festival(Painter p)
        {
            Backdrop(p, C(0.05f, 0.06f, 0.22f), C(0.12f, 0.14f, 0.38f));
            void Firework(float x, float y, float r, Color col, int n)
            {
                p.Glow(x, y, r * 0.8f, col, 0.35f);
                for (int i = 0; i < n; i++)
                {
                    float a = i * 360f / n * Mathf.Deg2Rad;
                    float x0 = x + Mathf.Cos(a) * r * 0.25f, y0 = y + Mathf.Sin(a) * r * 0.25f;
                    float x1 = x + Mathf.Cos(a) * r, y1 = y + Mathf.Sin(a) * r;
                    p.Line(x0, y0, x1, y1, 3f, col);
                    Circle(p, x1, y1, 4f, Color.Lerp(col, Color.white, 0.5f));
                }
                Circle(p, x, y, 5f, Color.white);
            }
            Firework(84, 196, 62, C(1f, 0.35f, 0.75f), 18);
            Firework(188, 214, 50, C(1f, 0.8f, 0.25f), 16);
            Firework(156, 148, 30, C(0.35f, 0.9f, 1f), 12);
            // 提灯の列 (上の綱からぶら下がる。左右へはみ出す)
            var line = new List<Vector2>();
            for (int i = 0; i <= 16; i++) { float t = i / 16f; line.Add(V(Mathf.Lerp(-6, 262, t), 252 - 26 * Mathf.Sin(t * Mathf.PI))); }
            p.Path(line, 3f, Ink);
            foreach (var t in new[] { 0.06f, 0.26f, 0.5f, 0.74f, 0.94f })
            {
                float x = Mathf.Lerp(-6, 262, t), y = 252 - 26 * Mathf.Sin(t * Mathf.PI) - 26;
                p.Glow(x, y, 34, C(1f, 0.55f, 0.25f), 0.5f);
                p.Rect(x - 9, y + 17, x + 9, y + 25, Ink);
                p.Rect(x - 9, y - 25, x + 9, y - 17, Ink);
                p.Ellipse(x, y, 17, 21, C(0.9f, 0.14f, 0.12f));
                for (int k = -2; k <= 2; k++) p.Line(x - 15 + Mathf.Abs(k) * 2, y + k * 7, x + 15 - Mathf.Abs(k) * 2, y + k * 7, 1.5f, C(0.6f, 0.06f, 0.06f));
                p.Ellipse(x - 5, y + 4, 4, 9, C(1f, 0.6f, 0.4f), 0.6f);
            }
            // 射的のコルク銃 (左下から右上へ。景品のだるまを狙う)
            var wood = C(0.6f, 0.36f, 0.16f);
            p.Poly(new[] { V(2, 58), V(40, 50), V(78, 84), V(66, 96), V(30, 80) }, wood);
            p.Line(66, 88, 176, 138, 10f, C(0.25f, 0.25f, 0.3f));
            p.Line(70, 84, 172, 132, 3f, C(0.5f, 0.5f, 0.56f));
            Circle(p, 184, 142, 8f, C(0.85f, 0.65f, 0.4f));
            p.Line(72, 80, 66, 66, 5f, C(0.25f, 0.25f, 0.3f));
            Circle(p, 218, 90, 26f, C(0.88f, 0.12f, 0.12f));
            p.Ellipse(218, 96, 14, 11, C(0.98f, 0.9f, 0.8f));
            Circle(p, 213, 98, 2.5f, Ink);
            Circle(p, 223, 98, 2.5f, Ink);
            p.Ellipse(218, 72, 14, 6, C(1f, 0.8f, 0.25f));
            Ribbon(p, C(0.85f, 0.15f, 0.15f));
        }

        // ------------------------------------------------------------------
        // 潜水艦: 海の底の宝箱と、丸い黄色の潜水艦
        // ------------------------------------------------------------------
        static void Submarine(Painter p)
        {
            Backdrop(p, C(0.03f, 0.18f, 0.38f), C(0.15f, 0.45f, 0.7f));
            for (int i = 0; i < 4; i++)
                p.Poly(new[] { V(60 + i * 40, 232), V(84 + i * 40, 232), V(50 + i * 34, 80), V(36 + i * 34, 80) }, Color.white, 0.07f);
            // 海藻 (左右へはみ出して揺れる)
            void Weed(float x, float h, float phase, Color col)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i <= 10; i++) { float t = i / 10f; pts.Add(V(x + 10f * Mathf.Sin(t * 6f + phase), 70 + t * h)); }
                p.Path(pts, 8f, col);
            }
            Weed(14, 120, 0f, C(0.15f, 0.6f, 0.3f));
            Weed(30, 90, 1.5f, C(0.25f, 0.75f, 0.35f));
            Weed(236, 110, 2f, C(0.15f, 0.6f, 0.3f));
            Weed(250, 80, 0.7f, C(0.3f, 0.8f, 0.4f));
            // 砂の海底
            var sand = new List<Vector2> { V(-4, 54) };
            for (int x = -4; x <= 260; x += 8) sand.Add(V(x, 84 + 6f * Mathf.Sin(x * 0.05f)));
            sand.Add(V(260, 54));
            p.Poly(sand, C(0.85f, 0.75f, 0.5f));
            for (int i = 0; i < 14; i++) p.Ellipse(10 + i * 18, 70 + (i % 3) * 4, 3, 2, C(0.7f, 0.6f, 0.38f));
            p.Ellipse(200, 82, 20, 12, C(0.45f, 0.42f, 0.4f));
            // 宝箱 (ふたが開いて、金貨と宝石が光る)
            var wood = C(0.5f, 0.28f, 0.12f);
            var gold = C(1f, 0.8f, 0.25f);
            p.Glow(84, 118, 50, C(1f, 0.85f, 0.4f), 0.8f);
            p.Poly(new[] { V(40, 122), V(128, 122), V(118, 156), V(50, 156) }, wood);
            p.Line(42, 124, 126, 124, 4f, gold);
            for (int i = 0; i < 8; i++) Circle(p, 54 + i * 9, 118 + (i % 2) * 4, 7f, gold);
            Circle(p, 70, 126, 6f, C(0.9f, 0.15f, 0.3f));
            Circle(p, 102, 126, 6f, C(0.2f, 0.7f, 1f));
            p.Rect(40, 76, 128, 120, wood);
            p.Rect(40, 112, 128, 120, gold);
            p.Rect(56, 76, 64, 120, gold);
            p.Rect(104, 76, 112, 120, gold);
            p.Rect(78, 94, 90, 108, gold);
            Circle(p, 84, 100, 2.5f, Ink);
            // 丸い黄色の潜水艦 (右上へはみ出す)
            var hull = C(1f, 0.8f, 0.15f);
            var hullDark = C(0.85f, 0.6f, 0.08f);
            p.Poly(new[] { V(208, 196), V(246, 214), V(246, 174) }, hullDark);
            p.Ellipse(250, 194, 6, 24, C(0.85f, 0.65f, 0.3f));
            Circle(p, 168, 176, 50f, hull);
            p.Ellipse(176, 162, 40, 34, hullDark, 0.35f);
            RoundRect(p, 152, 220, 184, 240, 6, hullDark);
            p.Line(176, 240, 176, 254, 4f, C(0.4f, 0.42f, 0.46f));
            p.Line(176, 254, 188, 254, 4f, C(0.4f, 0.42f, 0.46f));
            Circle(p, 160, 178, 26f, C(0.35f, 0.38f, 0.42f));
            Circle(p, 160, 178, 20f, C(0.4f, 0.8f, 1f));
            p.Ellipse(152, 186, 7, 4, Color.white, 0.8f, 30f);
            for (int i = 0; i < 8; i++) { float a = i * 45f * Mathf.Deg2Rad; Circle(p, 160 + Mathf.Cos(a) * 23, 178 + Mathf.Sin(a) * 23, 2.2f, C(0.75f, 0.75f, 0.8f)); }
            // 泡
            foreach (var (x, y, r) in new[] { (110f, 196f, 8f), (100f, 218f, 6f), (114f, 236f, 9f), (96f, 252f, 5f), (220f, 238f, 6f) })
            {
                Ring(p, x, y, r, 2.5f, Color.white, 0.9f);
                Circle(p, x - r * 0.35f, y + r * 0.35f, r * 0.25f, Color.white);
            }
            Ribbon(p, C(0.95f, 0.7f, 0.1f));
        }

        // ------------------------------------------------------------------
        // 音楽: 大きな八分音符と、コントラバスとトランペットを少し
        // ------------------------------------------------------------------
        static void Music(Painter p)
        {
            Backdrop(p, C(0.32f, 0.05f, 0.12f), C(0.6f, 0.12f, 0.22f));
            var gold = C(1f, 0.82f, 0.25f);
            for (int k = 0; k < 5; k++)
            {
                var pts = new List<Vector2>();
                for (int x = -4; x <= 260; x += 8) pts.Add(V(x, 150 + k * 9 + 18f * Mathf.Sin(x * 0.02f + 0.5f)));
                p.Path(pts, 2f, C(1f, 0.9f, 0.6f), 0.7f);
            }
            // コントラバス (左に傾けて。上へはみ出す)
            var o = V(70, 132);
            const float Tilt = 14f;
            var bassBody = C(0.6f, 0.28f, 0.1f);
            var bassDark = C(0.38f, 0.16f, 0.05f);
            var neck = Tf(new[] { V(-6, 40), V(6, 40), V(5, 106), V(-5, 106) }, o, Tilt);
            p.Poly(neck, C(0.12f, 0.08f, 0.06f));
            var scroll = Tf(V(0, 110), o, Tilt);
            Circle(p, scroll.x, scroll.y, 8f, bassDark);
            foreach (var (y, r) in new[] { (-46f, 42f), (18f, 32f) })
            {
                var c = Tf(V(0, y), o, Tilt);
                p.Ellipse(c.x, c.y, r, r * 1.05f, bassBody, 1f, Tilt);
            }
            var waist = Tf(V(0, -14), o, Tilt);
            p.Ellipse(waist.x, waist.y, 26, 18, bassBody, 1f, Tilt);
            foreach (var s in new[] { -1f, 1f })
            {
                var f0 = Tf(V(s * 14, -30), o, Tilt);
                var f1 = Tf(V(s * 14, 2), o, Tilt);
                p.Line(f0.x, f0.y, f1.x, f1.y, 3f, Ink);
            }
            for (int k = -1; k <= 1; k++)
            {
                var s0 = Tf(V(k * 3, -66), o, Tilt);
                var s1 = Tf(V(k * 2, 104), o, Tilt);
                p.Line(s0.x, s0.y, s1.x, s1.y, 1.2f, C(0.9f, 0.85f, 0.7f));
            }
            var bridge = Tf(V(0, -40), o, Tilt);
            p.Ellipse(bridge.x, bridge.y, 12, 3, C(0.9f, 0.8f, 0.6f), 1f, Tilt);
            // トランペット (右上へ。朝顔がはみ出す)
            var t0 = V(150, 108);
            const float Ta = 38f;
            var tube = Tf(new[] { V(0, -4), V(90, -4), V(90, 4), V(0, 4) }, t0, Ta);
            p.Poly(tube, gold);
            p.Poly(Tf(new[] { V(14, -14), V(70, -14), V(70, -8), V(14, -8) }, t0, Ta), gold);
            foreach (var x in new[] { 34f, 46f, 58f })
            {
                var v0 = Tf(V(x, -18), t0, Ta);
                var v1 = Tf(V(x, 16), t0, Ta);
                p.Line(v0.x, v0.y, v1.x, v1.y, 6f, C(0.85f, 0.62f, 0.15f));
                Circle(p, v1.x, v1.y, 4.5f, C(1f, 0.92f, 0.6f));
            }
            p.Poly(Tf(new[] { V(84, -6), V(124, -26), V(130, -26), V(130, 26), V(124, 26), V(84, 6) }, t0, Ta), gold);
            var rim = Tf(V(128, 0), t0, Ta);
            p.Ellipse(rim.x, rim.y, 7, 27, C(1f, 0.92f, 0.55f), 1f, Ta);
            p.Ellipse(rim.x, rim.y, 4, 22, C(0.7f, 0.45f, 0.08f), 1f, Ta);
            var mouth = Tf(V(-6, 0), t0, Ta);
            Circle(p, mouth.x, mouth.y, 5f, C(0.85f, 0.85f, 0.9f));
            // 大きな八分音符 (手前の真ん中)
            p.Glow(132, 120, 60, C(1f, 0.85f, 0.4f), 0.45f);
            p.Ellipse(118, 88, 28, 20, gold, 1f, 25f);
            p.Ellipse(112, 92, 10, 6, C(1f, 0.95f, 0.7f), 0.8f, 25f);
            p.Line(142, 96, 142, 226, 10f, gold);
            var flag = new List<Vector2> { V(138, 226) };
            for (int i = 0; i <= 10; i++) { float t = i / 10f; flag.Add(V(142 + 34 * Mathf.Sin(t * 2.2f), 226 - t * 70)); }
            for (int i = 10; i >= 0; i--) { float t = i / 10f; flag.Add(V(142 + 22 * Mathf.Sin(t * 2.2f), 206 - t * 56)); }
            p.Poly(flag, gold);
            Sparkle(p, 196, 222, 12f, Color.white);
            Sparkle(p, 40, 214, 9f, Color.white);
            Ribbon(p, C(0.55f, 0.08f, 0.2f));
        }

        // ------------------------------------------------------------------
        // 美術館: パレット、金の額縁に入った絵、大きな筆
        // ------------------------------------------------------------------
        static void Museum(Painter p)
        {
            Backdrop(p, C(0.42f, 0.06f, 0.1f), C(0.65f, 0.15f, 0.18f));
            var gold = C(1f, 0.78f, 0.25f);
            var goldDark = C(0.75f, 0.5f, 0.1f);
            // 額縁入りの絵 (右上に傾けて。はみ出す)
            var o = V(178, 180);
            const float Fa = -9f;
            p.Poly(Tf(new[] { V(-72, -56), V(72, -56), V(72, 56), V(-72, 56) }, o, Fa), goldDark);
            p.Poly(Tf(new[] { V(-66, -50), V(66, -50), V(66, 50), V(-66, 50) }, o, Fa), gold);
            p.Poly(Tf(new[] { V(-54, -38), V(54, -38), V(54, 38), V(-54, 38) }, o, Fa), C(0.45f, 0.72f, 0.98f));
            p.Poly(Tf(new[] { V(-54, -38), V(54, -38), V(54, -6), V(20, 6), V(-10, -4), V(-54, 10) }, o, Fa), C(0.3f, 0.65f, 0.3f));
            p.Poly(Tf(new[] { V(-54, -38), V(54, -38), V(54, -20), V(-20, -10), V(-54, -18) }, o, Fa), C(0.2f, 0.5f, 0.22f));
            var sun = Tf(V(28, 20), o, Fa);
            Circle(p, sun.x, sun.y, 10f, C(1f, 0.9f, 0.35f));
            var cloud = Tf(V(-22, 22), o, Fa);
            p.Ellipse(cloud.x, cloud.y, 14, 6, Color.white, 1f, Fa);
            foreach (var corner in new[] { V(-66, -50), V(66, -50), V(66, 50), V(-66, 50) })
            {
                var q = Tf(corner, o, Fa);
                Circle(p, q.x, q.y, 9f, gold);
                Circle(p, q.x, q.y, 4f, goldDark);
            }
            // パレット (左下に大きく)
            var wood = C(0.72f, 0.5f, 0.28f);
            p.Ellipse(84, 104, 72, 52, C(0.5f, 0.32f, 0.16f), 1f, 12f);
            p.Ellipse(82, 108, 70, 50, wood, 1f, 12f);
            p.Ellipse(108, 96, 12, 9, C(0.42f, 0.06f, 0.1f), 1f, 12f);
            var paints = new[] { C(0.92f, 0.15f, 0.15f), C(1f, 0.82f, 0.15f), C(0.2f, 0.65f, 0.3f), C(0.2f, 0.42f, 0.9f), C(0.98f, 0.98f, 0.98f), C(0.6f, 0.25f, 0.75f) };
            for (int i = 0; i < paints.Length; i++)
            {
                float a = (150f + i * 34f) * Mathf.Deg2Rad;
                float x = 82 + Mathf.Cos(a) * 46, y = 108 + Mathf.Sin(a) * 30;
                Circle(p, x, y, 11f, paints[i]);
                Circle(p, x - 3, y + 3, 3.5f, Color.white, 0.6f);
            }
            // 大きな筆 (右下から左上へ。はみ出して)、筆の先から絵の具のひと塗り
            var stroke = new List<Vector2>();
            for (int i = 0; i <= 12; i++) { float t = i / 12f; stroke.Add(V(Mathf.Lerp(30, 120, t), 186 + 22 * Mathf.Sin(t * Mathf.PI))); }
            p.Path(stroke, 14f, C(0.2f, 0.42f, 0.9f));
            p.Path(stroke, 5f, C(0.5f, 0.7f, 1f));
            p.Line(254, 44, 150, 156, 13f, C(0.55f, 0.08f, 0.1f));
            p.Line(252, 48, 152, 154, 4f, C(0.8f, 0.3f, 0.3f));
            p.Line(150, 156, 132, 176, 15f, C(0.78f, 0.8f, 0.85f));
            p.Line(142, 166, 138, 170, 15f, C(0.6f, 0.62f, 0.68f));
            p.Poly(new[] { V(124, 170), V(140, 186), V(112, 206), V(100, 210), V(104, 196) }, C(0.85f, 0.7f, 0.45f));
            p.Poly(new[] { V(112, 194), V(118, 202), V(102, 210), V(100, 208) }, C(0.2f, 0.42f, 0.9f));
            Ribbon(p, C(0.6f, 0.1f, 0.14f));
        }

        // ------------------------------------------------------------------
        // 宇宙: 星の散らばる宇宙を背景に、輪のある惑星と、炎を吹いて斜め上へ飛ぶロケット
        // ------------------------------------------------------------------
        static void Space(Painter p)
        {
            // 宇宙の背景 (濃い紺から紫へ。星雲のもやと星)
            Backdrop(p, C(0.03f, 0.03f, 0.14f), C(0.16f, 0.08f, 0.34f));
            p.Glow(96, 176, 60, C(0.7f, 0.3f, 0.9f), 0.35f);
            p.Glow(170, 110, 50, C(0.25f, 0.5f, 1f), 0.3f);
            foreach (var (x, y, r) in new[] { (70f, 210f, 2.2f), (104f, 98f, 1.6f), (190f, 188f, 1.8f), (150f, 222f, 1.4f), (58f, 150f, 1.5f), (210f, 140f, 2f), (128f, 70f, 1.4f), (86f, 128f, 1.2f), (176f, 72f, 1.6f), (118f, 196f, 1.2f) })
                Circle(p, x, y, r, Color.white);
            Sparkle(p, 150, 200, 9f, Color.white);
            Sparkle(p, 66, 104, 7f, C(0.8f, 0.9f, 1f));
            // 輪のある大きな惑星 (左上へはみ出す)。輪の後ろ側 → 本体 → 輪の前側
            var ringCol = C(0.95f, 0.85f, 0.6f);
            var ringDark = C(0.75f, 0.6f, 0.38f);
            p.Ellipse(70, 196, 66, 16, ringDark, 1f, 18f);
            p.Ellipse(70, 196, 54, 10, C(0.16f, 0.08f, 0.34f), 1f, 18f);
            Circle(p, 70, 196, 40f, C(0.95f, 0.62f, 0.3f));
            for (int k = -2; k <= 2; k++) p.Ellipse(70, 196 + k * 13, 39 - Mathf.Abs(k) * 7, 4, C(0.85f, 0.45f, 0.2f), 0.8f, 18f);
            p.Ellipse(58, 210, 14, 9, C(1f, 0.85f, 0.6f), 0.6f, 18f);
            var front = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float t = Mathf.Lerp(-10f, 190f, i / 20f) * Mathf.Deg2Rad; // 輪の手前の半分
                front.Add(Tf(V(Mathf.Cos(t) * 62, -Mathf.Sin(t) * 13), V(70, 196), 18f));
            }
            p.Path(front, 7f, ringCol);
            // 小さな青い惑星と月 (右下・右上)
            Circle(p, 214, 96, 24f, C(0.25f, 0.55f, 0.95f));
            p.Ellipse(206, 102, 10, 6, C(0.35f, 0.8f, 0.45f), 1f, -20f);
            p.Ellipse(222, 88, 8, 5, C(0.35f, 0.8f, 0.45f), 1f, 10f);
            p.Ellipse(206, 106, 6, 4, Color.white, 0.5f);
            Circle(p, 228, 210, 12f, C(0.8f, 0.8f, 0.85f));
            Circle(p, 224, 214, 3f, C(0.6f, 0.6f, 0.66f));
            Circle(p, 232, 205, 2.5f, C(0.6f, 0.6f, 0.66f));
            // ロケット (左下から右上へ。炎を吹いて、上へはみ出す)
            var o = V(140, 140);
            const float Ang = 40f; // 上向きのロケットを右へ傾ける
            var body = C(0.95f, 0.95f, 0.97f);
            var red = C(0.88f, 0.15f, 0.15f);
            var flameOuter = C(1f, 0.55f, 0.15f);
            var flameInner = C(1f, 0.92f, 0.45f);
            p.Poly(Tf(new[] { V(-20, -54), V(0, -122), V(20, -54) }, o, -Ang), flameOuter);
            p.Poly(Tf(new[] { V(-11, -54), V(0, -100), V(11, -54) }, o, -Ang), flameInner);
            for (int k = 0; k < 3; k++)
            {
                var a = Tf(V(-16 + k * 16, -130 - k * 6), o, -Ang);
                Circle(p, a.x, a.y, 7f - k, C(0.85f, 0.85f, 0.9f), 0.8f);
            }
            p.Poly(Tf(new[] { V(-24, -30), V(-46, -66), V(-22, -58) }, o, -Ang), red);       // 左の羽根
            p.Poly(Tf(new[] { V(24, -30), V(46, -66), V(22, -58) }, o, -Ang), red);          // 右の羽根
            var hull = new List<Vector2>();
            for (int i = 0; i <= 16; i++) { float t = i / 16f; hull.Add(V(-24 + 4 * Mathf.Sin(t * Mathf.PI), Mathf.Lerp(-58, 40, t))); }
            for (int i = 0; i <= 12; i++) { float t = i / 12f * Mathf.PI; hull.Add(V(-24 * Mathf.Cos(t), 40 + 46 * Mathf.Sin(t))); }
            for (int i = 16; i >= 0; i--) { float t = i / 16f; hull.Add(V(24 - 4 * Mathf.Sin(t * Mathf.PI), Mathf.Lerp(-58, 40, t))); }
            p.Poly(Tf(hull, o, -Ang), body);
            p.Poly(Tf(new[] { V(4, -56), V(18, -56), V(20, 40), V(6, 40) }, o, -Ang), C(0.78f, 0.8f, 0.86f));   // 影
            var nose = new List<Vector2>();
            for (int i = 0; i <= 12; i++) { float t = i / 12f * Mathf.PI; nose.Add(V(-24 * Mathf.Cos(t), 52 + 34 * Mathf.Sin(t))); }
            p.Poly(Tf(nose, o, -Ang), red);
            p.Poly(Tf(new[] { V(-24, -58), V(24, -58), V(20, -46), V(-20, -46) }, o, -Ang), C(0.4f, 0.42f, 0.48f));
            var window = Tf(V(0, 14), o, -Ang);
            Circle(p, window.x, window.y, 15f, C(0.45f, 0.47f, 0.52f));
            Circle(p, window.x, window.y, 11f, C(0.4f, 0.8f, 1f));
            Circle(p, window.x - 4, window.y + 4, 3.5f, Color.white, 0.8f);
            Ribbon(p, C(0.15f, 0.2f, 0.6f));
        }

        // ------------------------------------------------------------------
        // Unknown: 渦を巻くワープホールと、そこから飛び出す時計
        // ------------------------------------------------------------------
        static void Unknown(Painter p)
        {
            Circle(p, BgX, BgY, BgR + 6, C(0.55f, 0.35f, 1f));
            var rings = new[] { C(0.55f, 0.35f, 1f), C(1f, 0.45f, 0.8f), C(0.4f, 0.85f, 1f), C(0.35f, 0.25f, 0.85f), C(0.15f, 0.1f, 0.4f), C(0.05f, 0.03f, 0.15f) };
            for (int i = 0; i < rings.Length; i++) Circle(p, BgX, BgY, (BgR + 2) * (1f - i * 0.15f), rings[i]);
            for (int arm = 0; arm < 4; arm++)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i <= 30; i++)
                {
                    float t = i / 30f;
                    float a = (arm * 90f + t * 300f) * Mathf.Deg2Rad;
                    float r = Mathf.Lerp(12f, BgR - 6, t);
                    pts.Add(V(BgX + Mathf.Cos(a) * r, BgY + Mathf.Sin(a) * r));
                }
                p.Path(pts, 3f, C(1f, 0.85f, 1f), 0.6f);
            }
            foreach (var (x, y, r) in new[] { (110f, 150f, 2.5f), (150f, 130f, 2f), (126f, 120f, 1.8f), (140f, 165f, 2.2f), (98f, 132f, 1.5f), (160f, 152f, 1.5f) })
                Circle(p, x, y, r, Color.white);
            Circle(p, 140, 140, 12f, C(1f, 0.6f, 0.3f));
            p.Ellipse(140, 140, 20, 5, C(1f, 0.85f, 0.6f), 0.9f, -20f);
            // 飛び出す時計 (右上へはみ出す。傾けて)
            void Clock(float x, float y, float r, float deg, float hour, float minute)
            {
                p.Ellipse(x + 4, y - 4, r, r, C(0.1f, 0.05f, 0.2f), 0.4f);
                Circle(p, x, y, r, C(0.85f, 0.65f, 0.25f));
                Circle(p, x, y, r * 0.86f, C(1f, 0.98f, 0.92f));
                for (int i = 0; i < 12; i++)
                {
                    float a = (deg + i * 30f) * Mathf.Deg2Rad;
                    float r0 = i % 3 == 0 ? r * 0.62f : r * 0.7f;
                    p.Line(x + Mathf.Cos(a) * r0, y + Mathf.Sin(a) * r0, x + Mathf.Cos(a) * r * 0.78f, y + Mathf.Sin(a) * r * 0.78f, i % 3 == 0 ? 3.5f : 2f, Ink);
                }
                float ha = (90f - hour * 30f + deg) * Mathf.Deg2Rad, ma = (90f - minute * 6f + deg) * Mathf.Deg2Rad;
                p.Line(x, y, x + Mathf.Cos(ha) * r * 0.42f, y + Mathf.Sin(ha) * r * 0.42f, 5f, Ink);
                p.Line(x, y, x + Mathf.Cos(ma) * r * 0.66f, y + Mathf.Sin(ma) * r * 0.66f, 3.5f, Ink);
                Circle(p, x, y, 4f, C(0.85f, 0.2f, 0.2f));
            }
            Clock(206, 206, 42f, 15f, 10f, 10f);
            // 小さな時計とドア (左下へ)
            Clock(44, 92, 22f, -25f, 3f, 40f);
            var door = Tf(new[] { V(-16, -26), V(16, -26), V(16, 26), V(-16, 26) }, V(40, 210), 18f);
            p.Poly(door, C(0.62f, 0.38f, 0.2f));
            p.Poly(Tf(new[] { V(-11, -21), V(11, -21), V(11, 21), V(-11, 21) }, V(40, 210), 18f), C(0.72f, 0.48f, 0.28f));
            var knob = Tf(V(8, 0), V(40, 210), 18f);
            Circle(p, knob.x, knob.y, 3f, C(1f, 0.85f, 0.3f));
            Sparkle(p, 236, 132, 12f, Color.white);
            Sparkle(p, 78, 236, 9f, Color.white);
            Ribbon(p, C(0.3f, 0.2f, 0.7f));
        }
    }
}
