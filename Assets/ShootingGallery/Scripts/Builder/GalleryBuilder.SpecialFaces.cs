using System.Collections.Generic;
using UnityEngine;
using static ShootingGallery.Painter;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、フィーバーの的と床下のハッチが開く的 (青い玉) の顔。美術館の絵と同じ Painter で、筆で塗ったような絵にする。
    ///   フィーバー: 赤と白の縞 (同心円) の的に、太い文字で「Fever!」
    ///   ハッチ (青): 青と白の縞 (同心円) の的に、太い文字で「Under」
    /// 絵は丸く切り抜いた透明な PNG (Assets/ShootingGallery/Generated/Paintings) にして、Unlit の切り抜きの材質で円盤の前に貼る
    /// </summary>
    public partial class GalleryBuilder
    {
        const int FaceSize = 512;

        static readonly Color FaceWhite = C(0.97f, 0.95f, 0.9f);

        /// <summary>フィーバーの的の顔の材質 (赤と白の縞に、立体に見える太い文字で「Fever!」。濃い赤の厚みとふち取り)</summary>
        Material FeverFaceMat() => TargetFaceMat("Face_Fever", 11, p =>
        {
            DrawStripedRings(p, C(0.88f, 0.12f, 0.12f), FaceWhite);
            DrawWord3D(p, (q, c, s, w) => DrawFever(q, c, s, w), C(0.55f, 0.04f, 0.05f), C(0.3f, 0.01f, 0.02f), 12);
        });

        /// <summary>
        /// 立体に見える文字 (飛び出した看板の文字のように):
        ///   1. 文字を右下へ少しずつずらして何枚も重ね、濃い色の厚み (側面) を作る
        ///   2. その上に、白い表の面 (表の左上に明るいつや、ふちに細い影の線)
        ///   3. 全体を一番濃い色で太くふち取り
        /// draw(キャンバス, 色, ずらす量, 線の太さの倍率) で文字を描く
        /// </summary>
        static void DrawWord3D(Painter p, System.Action<Painter, Color, Vector2, float> draw, Color side, Color edge, int seed)
        {
            const int Depth = 12;                         // 厚みの段の数
            var step = new Vector2(1.3f, -1.3f);          // 1段ずつずらす向き (右下へ)
            var solid = new Painter(FaceSize, FaceSize, seed, true);
            for (int k = Depth; k >= 1; k--)               // 奥の段ほど暗く
                draw(solid, Color.Lerp(side, edge, k / (float)Depth * 0.6f), step * k, 1f);
            draw(solid, C(0.86f, 0.85f, 0.82f), Vector2.zero, 1f);           // 表の面 (少し灰色。つやを白で重ねる)
            draw(solid, Color.white, new Vector2(-2.5f, 2.5f), 0.5f);         // 表の左上のつや
            solid.Outline(8f, edge);
            Composite(p, solid);
        }

        // 太い文字で「Fever!」 (線を太い筆で引いた文字。真ん中に並べる。Under と同じ書き方)
        static void DrawFever(Painter p, Color c, Vector2 shift = default, float widthScale = 1f)
        {
            float W = 24f * widthScale;              // 線の太さ
            float baseY = FaceSize / 2f - 52f + shift.y; // 文字の下の線 (shift = 文字全体をずらす量)
            void Stroke(float ox, params float[] xy)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(ox + shift.x + xy[i], baseY + xy[i + 1]));
                p.Path(pts, W, c);
            }
            float[] Arc(float cx, float cy, float r, float from, float to, int n)
            {
                var list = new List<float>();
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.Lerp(from, to, i / (float)n) * Mathf.Deg2Rad;
                    list.Add(cx + Mathf.Cos(a) * r);
                    list.Add(cy + Mathf.Sin(a) * r);
                }
                return list.ToArray();
            }
            // F
            Stroke(72f, 0, 0, 0, 118, 52, 118);
            Stroke(72f, 0, 64, 42, 64);
            // e
            Stroke(146f, 0, 40, 56, 40);
            Stroke(146f, Arc(28, 40, 28, 0, 320, 26));
            // v
            Stroke(224f, 0, 80, 28, 0, 56, 80);
            // e
            Stroke(302f, 0, 40, 56, 40);
            Stroke(302f, Arc(28, 40, 28, 0, 320, 26));
            // r
            Stroke(380f, 0, 0, 0, 80);
            Stroke(380f, 0, 50, 8, 68, 22, 78, 38, 78);
            // !
            Stroke(444f, 0, 118, 0, 36);
            p.Ellipse(444f + shift.x, baseY + 6f, 14f * widthScale, 14f * widthScale, c);
        }

        /// <summary>床下のハッチが開く的 (青い玉) の顔の材質 (青と白の縞に、立体に見える太い文字で Under。紺色の厚みとふち取り)</summary>
        Material UnderFaceMat() => TargetFaceMat("Face_Under", 21, p =>
        {
            DrawStripedRings(p, C(0.12f, 0.36f, 0.9f), FaceWhite);
            DrawWord3D(p, (q, c, s, w) => DrawUnder(q, c, s, w), C(0.05f, 0.16f, 0.48f), C(0.02f, 0.06f, 0.24f), 22);
        });

        /// <summary>
        /// 特別な的の円盤 (pivot の y の高さ、表 -Z がプレイヤー向き): 濃い灰色のふちの上に、絵を貼った板。当たり判定は円盤の箱
        /// </summary>
        void PaintedTargetDisc(Transform parent, float y, float diameter, Material face)
        {
            var rim = Disc(parent, "Rim", y, diameter + 0.02f, 0.006f, Mat("Target_Rim", new Color(0.2f, 0.2f, 0.22f)));
            rim.AddComponent<BoxCollider>();
            // (ふちの円盤は厚み 2cm で、表が z = -0.004。絵はそのすぐ手前に貼る。中に埋まると、ふちの黒い色しか見えない)
            var quad = Prim(PrimitiveType.Quad, "Face", parent, new Vector3(0f, y, -0.008f), new Vector3(diameter, diameter, 1f), face, false);
            quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // 透明なキャンバスに絵を描いて、丸く切り抜いた材質にする (編集中は PNG に保存して読み込み直す)
        Material TargetFaceMat(string name, int seed, System.Action<Painter> draw)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            var p = new Painter(FaceSize, FaceSize, 4321 + seed, true);
            draw(p);
            p.Outline(4f, C(0.1f, 0.08f, 0.08f)); // 的のいちばん外のふち
            // 紙のざらつき (Painter.Grain は透明な所も塗ってしまうので、透明さを残したまま)
            for (int i = 0; i < p.px.Length; i++)
            {
                var c = p.px[i];
                if (c.a <= 0f) continue;
                float d = (p.R() - 0.5f) * 0.04f;
                p.px[i] = new Color(Mathf.Clamp01(c.r + d), Mathf.Clamp01(c.g + d), Mathf.Clamp01(c.b + d), c.a);
            }
            var tex = new Texture2D(FaceSize, FaceSize, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(p.px);
            tex.Apply(true);
#if UNITY_EDITOR
            if (editMode)
            {
                EnsureFolder(PaintingGeneratedDir);
                string path = $"{PaintingGeneratedDir}/{name}.png";
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", path)), tex.EncodeToPNG());
                DestroyImmediate(tex);
                UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                if (UnityEditor.AssetImporter.GetAtPath(path) is UnityEditor.TextureImporter imp)
                {
                    imp.textureType = UnityEditor.TextureImporterType.Default;
                    imp.sRGBTexture = true;
                    imp.alphaSource = UnityEditor.TextureImporterAlphaSource.FromInput;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = true;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.maxTextureSize = FaceSize;
                    imp.textureCompression = UnityEditor.TextureImporterCompression.Compressed;
                    imp.SaveAndReimport();
                }
                tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
#endif
            var mat = new Material(unlitShader) { name = name };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f); // (丸の外は透明)
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        // 縞の的 (同心円の帯を、外から交互の色で塗る)。帯の上に、帯に沿った筆のあとを重ねる
        static void DrawStripedRings(Painter p, Color a, Color b)
        {
            const int Bands = 6;
            float cx = FaceSize / 2f, cy = FaceSize / 2f, r0 = FaceSize / 2f - 8f;
            for (int i = 0; i < Bands; i++)
            {
                float r = r0 * (1f - i / (float)Bands);
                var col = i % 2 == 0 ? a : b;
                p.Ellipse(cx, cy, r, r, col);
                // 筆のあと (帯の真ん中あたりを、円に沿って)
                float rin = r0 * (1f - (i + 1f) / Bands), mid = (r + rin) / 2f, band = r - rin;
                int strokes = Mathf.RoundToInt(mid * 0.9f);
                for (int k = 0; k < strokes; k++)
                {
                    float ang = p.R(0f, 360f);
                    float rr = mid + p.R(-0.3f, 0.3f) * band;
                    var pos = new Vector2(cx, cy) + (Vector2)(Quaternion.Euler(0f, 0f, ang) * Vector3.right) * rr;
                    p.Stroke(pos.x, pos.y, ang + 90f, p.R(10f, 26f), p.R(2f, 4f), p.Jit(col, 0.06f), 0.45f);
                }
            }
            // まんなかの点
            p.Ellipse(cx, cy, r0 / Bands * 0.45f, r0 / Bands * 0.45f, a);
        }

        // 太い文字で「Under」 (線を太い筆で引いた文字。真ん中に並べる)
        static void DrawUnder(Painter p, Color c, Vector2 shift = default, float widthScale = 1f)
        {
            float W = 24f * widthScale;              // 線の太さ
            float baseY = FaceSize / 2f - 52f + shift.y; // 文字の下の線 (shift = 文字全体をずらす量)
            void Stroke(float ox, params float[] xy)
            {
                var pts = new List<Vector2>();
                for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(ox + shift.x + xy[i], baseY + xy[i + 1]));
                p.Path(pts, W, c);
            }
            List<float> Arc(float cx, float cy, float r, float from, float to, int n)
            {
                var list = new List<float>();
                for (int i = 0; i <= n; i++)
                {
                    float a = Mathf.Lerp(from, to, i / (float)n) * Mathf.Deg2Rad;
                    list.Add(cx + Mathf.Cos(a) * r);
                    list.Add(cy + Mathf.Sin(a) * r);
                }
                return list;
            }
            // U
            Stroke(88f, 0, 118, 0, 40, 4, 18, 16, 4, 30, 0, 44, 4, 56, 18, 60, 40, 60, 118);
            // n
            Stroke(178f, 0, 0, 0, 80);
            Stroke(178f, 0, 52, 8, 70, 24, 80, 40, 76, 50, 62, 52, 45, 52, 0);
            // d (丸と、背の高い縦の線)
            Stroke(254f, Arc(28, 38, 30, 0, 360, 28).ToArray());
            Stroke(254f, 58, 0, 58, 118);
            // e (横の線と、右下が開いた丸)
            Stroke(336f, 0, 40, 56, 40);
            Stroke(336f, Arc(28, 40, 28, 0, 320, 26).ToArray());
            // r
            Stroke(406f, 0, 0, 0, 80);
            Stroke(406f, 0, 50, 8, 68, 22, 78, 38, 78);
        }

        // 透明なキャンバスに描いた絵 (top) を、下の絵 (p) に重ねる
        static void Composite(Painter p, Painter top)
        {
            for (int i = 0; i < p.px.Length; i++)
            {
                var t = top.px[i];
                if (t.a <= 0f) continue;
                var b = p.px[i];
                float a = t.a + b.a * (1f - t.a);
                if (a <= 0f) continue;
                p.px[i] = new Color((t.r * t.a + b.r * b.a * (1f - t.a)) / a, (t.g * t.a + b.g * b.a * (1f - t.a)) / a, (t.b * t.a + b.b * b.a * (1f - t.a)) / a, a);
            }
        }
    }
}
