using System.Collections.Generic;
using UnityEngine;
using static ShootingGallery.Painter;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、美術館の絵 (ビットマップ)。
    /// 元の絵 (512×384) を Painter で細かく描き、Assets/ShootingGallery/Paintings/{名前}.png に保存する。
    /// そのフォルダーに同じ名前の PNG がもうあれば、描かずにそれを使う (自分で描いた絵に差し替えられる。描き直したい時はファイルを消す)。
    /// 元の絵から、当てるたびに重ねる3段の絵 (1: 大まかな下塗り / 2: 形 / 3: 細かい仕上げ) を作り、アルファに「筆で塗る順番」を入れて
    /// Assets/ShootingGallery/Generated/Paintings に保存する。実行時は Unlit の切り抜き (Alpha Clip) の材質で、_Cutoff を下げて塗っていく
    /// (実行中に絵を描いたりテクスチャを書き換えたりはしないので軽い)
    /// </summary>
    public partial class GalleryBuilder
    {
        const int PaintW = 512, PaintH = 384;
        const string PaintingSourceDir = "Assets/ShootingGallery/Paintings";
        const string PaintingGeneratedDir = "Assets/ShootingGallery/Generated/Paintings";

        // 絵は、オリジナルの風景画と、作者が亡くなってから長い年月がたち著作権の切れた名画 (ゴッホ・ダ・ヴィンチ・北斎) の題材をもとに描いたものだけ
        const int CanvasDesigns = 5;
        static readonly string[] PaintingFiles = { "SunnyHills", "Sunflowers", "Portrait", "StarryNight", "GreatWave" };
        static readonly string[] CanvasTitles = { "SUNNY HILLS", "SUNFLOWERS", "THE SMILE", "STARRY NIGHT", "THE GREAT WAVE" };
        // 1段目・2段目の絵の細かさ (横のマスの数。荒いほどぼんやり)
        static readonly int[] StageCells = { 10, 36, 0 };

        readonly Dictionary<string, Color[]> paintingSources = new Dictionary<string, Color[]>();

        /// <summary>design 番の絵の、段ごとの切り抜きの材質 (0〜2)</summary>
        Material[] PaintingStageMats(int design)
        {
            design %= CanvasDesigns;
            var src = PaintingSource(PaintingFiles[design], p => DrawPainting(p, design), design);
            var result = new Material[3];
            for (int s = 0; s < 3; s++)
            {
                string name = $"Painting_{PaintingFiles[design]}_S{s + 1}";
                if (mats.TryGetValue(name, out var cached)) { result[s] = cached; continue; }
                var tex = StageTexture(name, src, StageCells[s], PaintingMask.StageRows[s], design * 7 + s);
                result[s] = PaintingMat(name, tex, true);
            }
            return result;
        }

        /// <summary>design 番の完成した絵の、切り抜きなしの材質 (飾りの名画)</summary>
        Material PaintingFullMat(int design)
        {
            design %= CanvasDesigns;
            string name = $"Painting_{PaintingFiles[design]}_Full";
            if (mats.TryGetValue(name, out var cached)) return cached;
            var src = PaintingSource(PaintingFiles[design], p => DrawPainting(p, design), design);
            return PaintingMat(name, StageTexture(name, src, 0, 0, 0), false);
        }

        /// <summary>行きのキャンバスに描く、美術館の中の絵 (切り抜きの材質。塗る順番は PaintingMask.IntroRows)</summary>
        Material IntroPaintingMat()
        {
            const string name = "Painting_MuseumIntro";
            if (mats.TryGetValue(name, out var cached)) return cached;
            var src = PaintingSource("MuseumInterior", DrawMuseumInterior, 99);
            return PaintingMat(name, StageTexture(name, src, 0, PaintingMask.IntroRows, 99), true);
        }

        Material PaintingMat(string name, Texture2D tex, bool clip)
        {
            var mat = new Material(unlitShader) { name = name };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            if (clip)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0f); // 0 = 全部見える。塗っている途中だけ、その絵の _Cutoff を上げる (PaintingMask)
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        // 元の絵: フォルダーに PNG があればそれを (大きさを合わせて) 使い、なければ描いて保存する
        Color[] PaintingSource(string file, System.Action<Painter> draw, int seed)
        {
            if (paintingSources.TryGetValue(file, out var cached)) return cached;
            Color[] result = null;
#if UNITY_EDITOR
            string path = $"{PaintingSourceDir}/{file}.png";
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", path));
            if (editMode && System.IO.File.Exists(full))
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (t.LoadImage(System.IO.File.ReadAllBytes(full)))
                {
                    result = new Color[PaintW * PaintH];
                    for (int y = 0; y < PaintH; y++)
                        for (int x = 0; x < PaintW; x++)
                        {
                            var c = t.GetPixelBilinear((x + 0.5f) / PaintW, (y + 0.5f) / PaintH);
                            c.a = 1f;
                            result[y * PaintW + x] = c;
                        }
                }
                DestroyImmediate(t);
            }
#endif
            if (result == null)
            {
                var p = new Painter(PaintW, PaintH, 1234 + seed * 77);
                draw(p);
                result = p.px;
#if UNITY_EDITOR
                if (editMode)
                {
                    EnsureFolder(PaintingSourceDir);
                    var t = new Texture2D(PaintW, PaintH, TextureFormat.RGB24, false);
                    t.SetPixels(result);
                    System.IO.File.WriteAllBytes(full, t.EncodeToPNG());
                    DestroyImmediate(t);
                    UnityEditor.AssetDatabase.ImportAsset(path);
                }
#endif
            }
            paintingSources[file] = result;
            return result;
        }

        /// <summary>
        /// 段の絵: cells > 0 なら荒いマスに平均してからなめらかに戻した、ぼんやりした絵 (下塗り)。
        /// rows > 0 ならアルファに筆で塗る順番 (1 - 順番。早く塗る所ほど大きい) を入れる。PNG に保存して読み込み直す
        /// </summary>
        Texture2D StageTexture(string name, Color[] src, int cells, int rows, int seed)
        {
            var img = cells > 0 ? Coarse(src, cells, seed) : (Color[])src.Clone();
            for (int y = 0; y < PaintH; y++)
                for (int x = 0; x < PaintW; x++)
                {
                    int i = y * PaintW + x;
                    img[i].a = rows > 0 ? StrokeAlpha(x / (PaintW - 1f), y / (PaintH - 1f), rows, seed) : 1f;
                }
            var tex = new Texture2D(PaintW, PaintH, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(img);
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
                    imp.alphaSource = rows > 0 ? UnityEditor.TextureImporterAlphaSource.FromInput : UnityEditor.TextureImporterAlphaSource.None;
                    imp.alphaIsTransparency = false; // アルファは塗る順番 (色をにじませない)
                    imp.mipmapEnabled = true;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.maxTextureSize = 512;
                    imp.textureCompression = UnityEditor.TextureImporterCompression.Compressed;
                    imp.SaveAndReimport();
                }
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
#endif
            return tex;
        }

        // 筆で塗る順番のアルファ: 上から rows 本の帯を、左右に行ったり来たりして塗る (帯のさかいめと塗り先は、少しぎざぎざ)
        static float StrokeAlpha(float u, float v, int rows, int seed)
        {
            float wave = (Noise(u * 5f + seed * 3.1f, seed * 1.3f) - 0.5f) * 0.6f + Mathf.Sin(u * 9f + seed) * 0.12f;
            int r = Mathf.Clamp(Mathf.FloorToInt((1f - v) * rows + wave), 0, rows - 1);
            float p = r % 2 == 0 ? u : 1f - u;
            p = Mathf.Clamp01(p + (Noise(u * 30f + seed, v * 70f) - 0.5f) * 0.06f);
            float order = (r + p) / rows;
            return Mathf.Clamp(1f - order, 0f, 0.996f);
        }

        // 荒いマスに平均してから、なめらかに引きのばす (下塗りのぼんやりした絵)。少し明るさのむらを足して、筆で塗ったように
        static Color[] Coarse(Color[] src, int cols, int seed)
        {
            int rows = Mathf.Max(1, Mathf.RoundToInt(cols * PaintH / (float)PaintW));
            var small = new Color[cols * rows];
            var count = new int[cols * rows];
            for (int y = 0; y < PaintH; y++)
                for (int x = 0; x < PaintW; x++)
                {
                    int i = (y * rows / PaintH) * cols + x * cols / PaintW;
                    small[i] += src[y * PaintW + x];
                    count[i]++;
                }
            for (int i = 0; i < small.Length; i++) small[i] /= Mathf.Max(1, count[i]);
            var outp = new Color[PaintW * PaintH];
            for (int y = 0; y < PaintH; y++)
                for (int x = 0; x < PaintW; x++)
                {
                    float fx = Mathf.Clamp((x + 0.5f) / PaintW * cols - 0.5f, 0f, cols - 1f), fy = Mathf.Clamp((y + 0.5f) / PaintH * rows - 0.5f, 0f, rows - 1f);
                    int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                    int x1 = Mathf.Min(cols - 1, x0 + 1), y1 = Mathf.Min(rows - 1, y0 + 1);
                    float tx = fx - x0, ty = fy - y0;
                    var c = Color.Lerp(Color.Lerp(small[y0 * cols + x0], small[y0 * cols + x1], tx), Color.Lerp(small[y1 * cols + x0], small[y1 * cols + x1], tx), ty);
                    float n = (Noise(x * 0.08f + seed, y * 0.02f) - 0.5f) * 0.08f;
                    outp[y * PaintW + x] = new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n), 1f);
                }
            return outp;
        }

        // ------------------------------------------------------------------
        // 絵 (座標はピクセル。左下が原点、幅 512・高さ 384)
        // ------------------------------------------------------------------

        static void DrawPainting(Painter p, int design)
        {
            switch (design)
            {
                case 0: DrawSunnyHills(p); break;
                case 1: DrawSunflowers(p); break;
                case 2: DrawPortrait(p); break;
                case 3: DrawStarryNight(p); break;
                default: DrawGreatWave(p); break;
            }
        }

        static List<Vector2> Pts(params float[] xy)
        {
            var l = new List<Vector2>(xy.Length / 2);
            for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(new Vector2(xy[i], xy[i + 1]));
            return l;
        }

        // 葉の茂った木 (幹と、暗い → 明るい葉の点を重ねる)
        static void Tree(Painter p, float x, float y, float size)
        {
            p.Line(x, y, x + size * 0.05f, y + size * 0.9f, size * 0.12f, C(0.35f, 0.22f, 0.12f));
            var dark = C(0.12f, 0.32f, 0.14f);
            var mid = C(0.2f, 0.48f, 0.2f);
            var light = C(0.45f, 0.7f, 0.3f);
            for (int i = 0; i < 26; i++)
            {
                float a = p.R(0f, Mathf.PI * 2f), r = p.R(0f, size * 0.55f);
                p.Ellipse(x + Mathf.Cos(a) * r, y + size * 1.05f + Mathf.Sin(a) * r * 0.85f, size * p.R(0.18f, 0.28f), size * p.R(0.14f, 0.22f), p.Jit(dark, 0.04f));
            }
            for (int i = 0; i < 22; i++)
            {
                float a = p.R(0f, Mathf.PI * 2f), r = p.R(0f, size * 0.45f);
                p.Ellipse(x - size * 0.1f + Mathf.Cos(a) * r, y + size * 1.15f + Mathf.Sin(a) * r * 0.8f, size * p.R(0.12f, 0.2f), size * p.R(0.1f, 0.16f), p.Jit(mid, 0.05f));
            }
            for (int i = 0; i < 12; i++)
            {
                float a = p.R(0f, Mathf.PI * 2f), r = p.R(0f, size * 0.3f);
                p.Ellipse(x - size * 0.2f + Mathf.Cos(a) * r, y + size * 1.3f + Mathf.Sin(a) * r * 0.7f, size * p.R(0.06f, 0.12f), size * p.R(0.05f, 0.1f), p.Jit(light, 0.05f));
            }
        }

        static void Cloud(Painter p, float cx, float cy, float size)
        {
            for (int i = 0; i < 9; i++)
                p.Ellipse(cx + p.R(-1.6f, 1.6f) * size, cy - size * 0.25f + p.R(-0.2f, 0.1f) * size, size * p.R(0.5f, 0.9f), size * p.R(0.3f, 0.45f), C(0.78f, 0.8f, 0.88f));
            for (int i = 0; i < 12; i++)
                p.Ellipse(cx + p.R(-1.5f, 1.5f) * size, cy + p.R(-0.1f, 0.35f) * size, size * p.R(0.45f, 0.8f), size * p.R(0.35f, 0.55f), p.Jit(C(0.98f, 0.98f, 1f), 0.02f));
        }

        // 0: 晴れた丘 (青空と雲・雪の山・重なる丘・川・木・赤い屋根の家・花畑)
        static void DrawSunnyHills(Painter p)
        {
            p.VGrad(0, PaintH, C(0.82f, 0.92f, 1f), C(0.3f, 0.55f, 0.9f));
            p.Glow(410, 318, 110, C(1f, 0.97f, 0.75f), 0.55f);
            p.Ellipse(410, 318, 24, 24, C(1f, 0.93f, 0.55f));
            Cloud(p, 110, 320, 26);
            Cloud(p, 270, 345, 20);
            Cloud(p, 330, 280, 14);
            // 遠くの雪山
            var mtn = p.RidgeLine(225, 45, 7f, 26, 1.3f);
            p.Ridge(mtn, 150, C(0.55f, 0.6f, 0.72f), C(0.45f, 0.52f, 0.66f), 200, 0.03f);
            for (int x = 0; x < PaintW; x++)
                if (mtn[x] > 238f)
                    for (int y = Mathf.RoundToInt(mtn[x] - (mtn[x] - 238f) * 0.8f); y <= mtn[x]; y++)
                        p.Rect(x, y, x + 1, y + 1, C(0.96f, 0.97f, 1f), 0.9f);
            // 重なる丘 (遠い → 近い)
            p.Ridge(p.RidgeLine(165, 14, 5f, 6, 4.1f), 100, C(0.45f, 0.62f, 0.5f), C(0.38f, 0.55f, 0.42f), 300, 0.04f);
            p.Ridge(p.RidgeLine(128, 18, 4f, 5, 2.2f), 60, C(0.42f, 0.66f, 0.3f), C(0.33f, 0.56f, 0.24f), 500, 0.05f);
            // 川 (手前から奥へ細くなる)
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f, y = t * 135f;
                float cx = 260 + Mathf.Sin(t * 5f) * 40f * (1f - t), w = Mathf.Lerp(70f, 3f, t);
                left.Add(new Vector2(cx - w, y));
                right.Add(new Vector2(cx + w, y));
            }
            right.Reverse();
            left.AddRange(right);
            p.Poly(left, C(0.45f, 0.68f, 0.9f));
            for (int i = 0; i < 60; i++) { float y = p.R(0, 120); p.Stroke(260 + Mathf.Sin(y / 135f * 5f) * 40f * (1f - y / 135f) + p.R(-30, 30) * (1f - y / 135f), y, 0, p.R(5, 14), 1.6f, C(0.85f, 0.93f, 1f), 0.8f); }
            // 手前の丘 (川の両側)
            var near = p.RidgeLine(70, 14, 3f, 4, 5.5f);
            for (int x = 0; x < PaintW; x++) if (Mathf.Abs(x - 260) < 90 - near[x] * 0.3f) near[x] = Mathf.Min(near[x], 30f + Mathf.Abs(x - 260) * 0.4f);
            p.Ridge(near, 0, C(0.62f, 0.78f, 0.3f), C(0.42f, 0.62f, 0.22f), 900, 0.06f);
            // 赤い屋根の家と柵
            p.Rect(80, 118, 132, 150, C(0.95f, 0.93f, 0.86f));
            p.Rect(120, 118, 132, 150, C(0.8f, 0.78f, 0.72f));
            p.Poly(Pts(74, 150, 138, 150, 106, 176), C(0.75f, 0.18f, 0.12f));
            p.Rect(116, 158, 122, 172, C(0.4f, 0.3f, 0.28f));
            p.Rect(100, 118, 110, 136, C(0.4f, 0.25f, 0.15f));
            p.Rect(86, 134, 96, 144, C(0.4f, 0.6f, 0.85f));
            for (int k = 0; k < 9; k++) p.Line(150 + k * 10, 108 + k * 0.6f, 150 + k * 10, 120 + k * 0.6f, 2, C(0.45f, 0.32f, 0.2f));
            p.Line(148, 116, 232, 121, 1.5f, C(0.45f, 0.32f, 0.2f));
            // 木
            foreach (var (x, y, s) in new[] { (40f, 120f, 34f), (190f, 132f, 22f), (360f, 140f, 18f), (420f, 128f, 26f), (470f, 60f, 48f), (30f, 30f, 44f), (330f, 30f, 30f) })
                Tree(p, x, y, s);
            // 花畑
            var flowers = new[] { C(0.95f, 0.25f, 0.3f), C(1f, 0.9f, 0.3f), C(1f, 1f, 1f), C(0.75f, 0.45f, 0.9f) };
            for (int i = 0; i < 260; i++)
            {
                float x = p.R(0, PaintW), y = p.R(0, 55);
                if (Mathf.Abs(x - 260) < 75 - y * 0.6f) continue;
                p.Stroke(x, y - 3, 90, 6, 1.2f, C(0.25f, 0.5f, 0.2f));
                p.Ellipse(x, y, 2.4f, 2.2f, p.Pick(flowers));
            }
            p.Grain(0.025f);
        }

        // 1: ひまわり (黄色い壁・机・花瓶にいけたひまわり・レモンとりんご)
        static void DrawSunflowers(Painter p)
        {
            p.VGrad(110, PaintH, C(0.92f, 0.78f, 0.38f), C(0.95f, 0.86f, 0.52f));
            for (int i = 0; i < 900; i++)
            {
                float x = p.R(0, PaintW), y = p.R(110, PaintH);
                p.Stroke(x, y, 90 + p.R(-12, 12), p.R(8, 18), p.R(2, 3.5f), p.Jit(C(0.93f, 0.8f, 0.42f), 0.06f), 0.7f);
            }
            p.Rect(0, 0, PaintW, 112, C(0.62f, 0.4f, 0.18f));
            for (int i = 0; i < 500; i++)
                p.Stroke(p.R(0, PaintW), p.R(0, 108), p.R(-6, 6), p.R(12, 26), p.R(2, 3.5f), p.Jit(C(0.66f, 0.42f, 0.2f), 0.07f), 0.7f);
            p.Line(0, 110, PaintW, 110, 4, C(0.42f, 0.25f, 0.1f));
            // 花瓶の影と花瓶
            p.Ellipse(285, 58, 95, 16, C(0.3f, 0.18f, 0.08f), 0.4f);
            p.Ellipse(256, 125, 66, 72, C(0.9f, 0.68f, 0.22f));
            p.Ellipse(278, 120, 46, 64, C(0.72f, 0.5f, 0.12f), 0.45f);
            p.Ellipse(232, 140, 18, 34, C(1f, 0.92f, 0.6f), 0.55f, -10f);
            p.Rect(226, 175, 286, 210, C(0.88f, 0.66f, 0.22f));
            p.Ellipse(256, 210, 32, 8, C(0.82f, 0.6f, 0.2f));
            for (int x = 192; x < 320; x++)
            {
                float dx = (x - 256f) / 66f;
                if (Mathf.Abs(dx) < 1f) p.Rect(x, 125 + 72 * Mathf.Sqrt(1f - dx * dx) * 0.05f + 4, x + 1, 134, C(0.15f, 0.3f, 0.7f));
            }
            // 茎と葉
            var heads = new[] { (170f, 295f, 34f), (256f, 330f, 36f), (345f, 300f, 32f), (205f, 238f, 28f), (310f, 240f, 30f), (410f, 345f, 26f), (110f, 345f, 24f) };
            foreach (var (x, y, r) in heads)
            {
                p.Line(256 + (x - 256) * 0.1f, 205, x, y - r * 0.6f, 4, C(0.35f, 0.5f, 0.18f));
                p.Ellipse((x + 256) / 2f + 10, (y + 205) / 2f, 18, 7, C(0.3f, 0.48f, 0.16f), 1f, Mathf.Atan2(y - 205, x - 256) * Mathf.Rad2Deg + 40);
            }
            // ひまわりの花 (花びらと、粒々の真ん中)
            foreach (var (x, y, r) in heads)
            {
                int petals = 18;
                for (int k = 0; k < petals; k++)
                {
                    float a = k * 360f / petals + p.R(-6, 6);
                    float rad = a * Mathf.Deg2Rad;
                    p.Ellipse(x + Mathf.Cos(rad) * r * 0.95f, y + Mathf.Sin(rad) * r * 0.95f, r * 0.5f, r * 0.16f, p.Jit(k % 2 == 0 ? C(1f, 0.78f, 0.12f) : C(0.95f, 0.62f, 0.08f), 0.05f), 1f, a);
                }
                p.Ellipse(x, y, r * 0.58f, r * 0.58f, C(0.42f, 0.25f, 0.08f));
                for (int k = 0; k < 40; k++)
                {
                    float a = p.R(0, Mathf.PI * 2f), d = Mathf.Sqrt(p.R()) * r * 0.52f;
                    p.Ellipse(x + Mathf.Cos(a) * d, y + Mathf.Sin(a) * d, 1.6f, 1.6f, p.R() < 0.5f ? C(0.22f, 0.12f, 0.04f) : C(0.65f, 0.42f, 0.1f));
                }
            }
            // レモンとりんご
            p.Ellipse(390, 70, 26, 18, C(0.98f, 0.88f, 0.2f), 1f, 10f);
            p.Ellipse(383, 76, 10, 6, C(1f, 0.97f, 0.7f), 0.7f, 10f);
            p.Ellipse(430, 62, 22, 20, C(0.8f, 0.12f, 0.1f));
            p.Ellipse(424, 70, 7, 5, C(1f, 0.6f, 0.55f), 0.7f);
            p.Ellipse(120, 66, 24, 17, C(0.98f, 0.85f, 0.2f), 1f, -15f);
            p.Grain(0.03f);
        }

        // 2: 微笑む人の肖像 (かすんだ山と道の背景・黒い服・組んだ手・やわらかい陰影)
        static void DrawPortrait(Painter p)
        {
            p.VGrad(0, PaintH, C(0.42f, 0.48f, 0.32f), C(0.5f, 0.58f, 0.48f));
            p.Ridge(p.RidgeLine(250, 30, 6f, 18, 2f), 120, C(0.42f, 0.55f, 0.5f), C(0.35f, 0.42f, 0.32f), 300, 0.03f);
            p.Ridge(p.RidgeLine(205, 22, 4f, 10, 5f), 100, C(0.4f, 0.42f, 0.28f), C(0.32f, 0.34f, 0.2f), 300, 0.03f);
            p.Path(Pts(40, 200, 70, 215, 60, 232, 100, 245, 90, 258), 4, C(0.62f, 0.5f, 0.3f));
            p.Path(Pts(470, 190, 440, 205, 455, 222, 420, 240), 4, C(0.62f, 0.5f, 0.3f));
            p.Ellipse(450, 262, 38, 6, C(0.62f, 0.7f, 0.7f), 0.8f);
            // 髪 (後ろ)
            p.Ellipse(256, 262, 64, 92, C(0.16f, 0.1f, 0.06f));
            p.Ellipse(204, 190, 26, 62, C(0.16f, 0.1f, 0.06f), 1f, -8f);
            p.Ellipse(308, 190, 26, 62, C(0.16f, 0.1f, 0.06f), 1f, 8f);
            // 服 (肩から下)
            p.Poly(Pts(80, 0, 110, 120, 170, 175, 220, 192, 292, 192, 342, 175, 402, 120, 432, 0), C(0.14f, 0.12f, 0.07f));
            p.Ellipse(256, 168, 50, 30, C(0.84f, 0.66f, 0.46f));
            p.Poly(Pts(150, 0, 170, 150, 215, 160, 256, 150, 297, 160, 342, 150, 362, 0), C(0.14f, 0.12f, 0.07f));
            p.Path(Pts(205, 162, 256, 152, 307, 162), 3, C(0.5f, 0.38f, 0.15f));
            for (int i = 0; i < 160; i++)
            {
                float x = p.R(110, 400), y = p.R(0, 150);
                p.Stroke(x, y, 70 + p.R(-30, 30), p.R(10, 24), 2, p.Jit(C(0.2f, 0.17f, 0.1f), 0.04f), 0.6f);
            }
            // 首と顔
            p.Rect(238, 180, 274, 225, C(0.8f, 0.62f, 0.42f));
            p.Ellipse(256, 270, 44, 58, C(0.88f, 0.7f, 0.48f));
            p.Ellipse(272, 262, 26, 50, C(0.65f, 0.48f, 0.3f), 0.35f);
            p.Ellipse(248, 300, 26, 16, C(0.98f, 0.85f, 0.65f), 0.35f);
            // 髪 (前) とベール
            p.Ellipse(220, 268, 14, 50, C(0.18f, 0.11f, 0.06f), 1f, 8f);
            p.Ellipse(292, 268, 14, 50, C(0.18f, 0.11f, 0.06f), 1f, -8f);
            p.Ellipse(256, 322, 50, 18, C(0.14f, 0.09f, 0.05f), 0.6f);
            // 目・まゆ・鼻・口
            foreach (float s in new[] { -1f, 1f })
            {
                float ex = 256 + s * 17;
                p.Ellipse(ex, 282, 9, 4.2f, C(0.92f, 0.86f, 0.74f));
                p.Ellipse(ex + 1, 282, 4, 4, C(0.25f, 0.16f, 0.08f));
                p.Ellipse(ex + 1, 282, 1.8f, 1.8f, C(0.05f, 0.03f, 0.02f));
                p.Line(ex - 9, 285, ex + 9, 286, 1.5f, C(0.4f, 0.25f, 0.15f));
                p.Line(ex - 10, 294, ex + 10, 295 + s * 0.5f, 1.5f, C(0.5f, 0.35f, 0.22f), 0.35f);
            }
            p.Line(258, 280, 261, 258, 3, C(0.6f, 0.42f, 0.26f), 0.5f);
            p.Ellipse(256, 256, 7, 3, C(0.55f, 0.36f, 0.22f), 0.6f);
            p.Path(Pts(240, 243, 248, 240, 256, 240, 264, 240, 272, 243), 2.5f, C(0.55f, 0.25f, 0.2f));
            p.Line(240, 243, 237, 246, 1.5f, C(0.55f, 0.32f, 0.22f), 0.6f);
            p.Line(272, 243, 275, 246, 1.5f, C(0.55f, 0.32f, 0.22f), 0.6f);
            p.Ellipse(256, 222, 20, 6, C(0.7f, 0.52f, 0.34f), 0.3f);
            // 組んだ手
            p.Ellipse(222, 52, 46, 20, C(0.86f, 0.68f, 0.46f), 1f, 12f);
            p.Ellipse(285, 46, 50, 20, C(0.84f, 0.66f, 0.44f), 1f, -6f);
            for (int k = 0; k < 4; k++) p.Line(300 + k * 9, 36, 312 + k * 9, 56, 1.5f, C(0.6f, 0.44f, 0.28f), 0.6f);
            p.Blur(1);
            p.Grain(0.02f);
        }

        // 3: 星月夜 (うずまく夜空・光る月と星・村と教会・黒い糸杉)
        static void DrawStarryNight(Painter p)
        {
            p.VGrad(0, PaintH, C(0.1f, 0.18f, 0.45f), C(0.06f, 0.1f, 0.3f));
            var vortex = new[] { new Vector3(210, 285, 1f), new Vector3(330, 300, -0.7f), new Vector3(110, 330, 0.6f) };
            var sky = new[] { C(0.18f, 0.32f, 0.72f), C(0.3f, 0.45f, 0.82f), C(0.5f, 0.65f, 0.92f), C(0.75f, 0.85f, 0.95f), C(0.1f, 0.16f, 0.42f), C(0.25f, 0.38f, 0.55f) };
            for (int i = 0; i < 2600; i++)
            {
                float x = p.R(0, PaintW), y = p.R(120, PaintH);
                Vector2 dir = new Vector2(1f, 0.1f);
                foreach (var v in vortex)
                {
                    var d = new Vector2(x - v.x, y - v.y);
                    float w = v.z * 3000f / (d.sqrMagnitude + 900f);
                    dir += new Vector2(-d.y, d.x).normalized * w;
                }
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                p.Stroke(x, y, ang, p.R(9, 17), p.R(2.5f, 4f), p.Jit(p.Pick(sky), 0.04f), 0.85f);
            }
            // 大きなうず
            for (float t = 0f; t < 1f; t += 0.004f)
            {
                float a = t * Mathf.PI * 5f, r = 8f + t * 62f;
                float x = 210 + Mathf.Cos(a) * r * 1.4f, y = 285 + Mathf.Sin(a) * r * 0.75f;
                p.Stroke(x, y, a * Mathf.Rad2Deg + 90f, 10, 4, p.Jit(t < 0.5f ? C(0.85f, 0.9f, 0.95f) : C(0.55f, 0.7f, 0.95f), 0.04f), 0.9f);
            }
            // 月と星 (光の輪)
            void Halo(float cx, float cy, float r, Color c)
            {
                for (int ring = 0; ring < 3; ring++)
                {
                    float rr = r * (1.4f + ring * 0.55f);
                    for (int k = 0; k < 28; k++)
                    {
                        float a = k * Mathf.PI * 2f / 28;
                        p.Stroke(cx + Mathf.Cos(a) * rr, cy + Mathf.Sin(a) * rr, a * Mathf.Rad2Deg + 90f, rr * 0.3f, 3.2f, p.Jit(Color.Lerp(c, C(0.6f, 0.75f, 0.9f), ring * 0.35f), 0.05f), 0.85f);
                    }
                }
                p.Ellipse(cx, cy, r, r, c);
                p.Ellipse(cx - r * 0.2f, cy + r * 0.2f, r * 0.5f, r * 0.5f, C(1f, 1f, 0.85f), 0.8f);
            }
            Halo(450, 330, 22, C(1f, 0.85f, 0.25f));
            p.Ellipse(458, 336, 15, 17, C(0.85f, 0.65f, 0.15f), 0.75f);
            foreach (var (x, y, r) in new[] { (60f, 350f, 8f), (150f, 360f, 7f), (290f, 350f, 9f), (370f, 300f, 7f), (40f, 260f, 6f), (500f, 260f, 6f), (260f, 220f, 7f), (420f, 230f, 6f) })
                Halo(x, y, r, C(1f, 0.95f, 0.55f));
            // 丘
            var hill = p.RidgeLine(135, 16, 4f, 6, 0.5f);
            p.Ridge(hill, 0, C(0.12f, 0.2f, 0.42f), C(0.06f, 0.12f, 0.28f), 700, 0.04f);
            // 村 (家と光る窓・教会)
            for (int i = 0; i < 22; i++)
            {
                float x = 160 + i * 14 + p.R(-4, 4), y = p.R(30, 95), w = p.R(12, 20), h = p.R(10, 16);
                p.Rect(x, y, x + w, y + h, p.Jit(C(0.18f, 0.22f, 0.38f), 0.04f));
                p.Poly(Pts(x - 2, y + h, x + w + 2, y + h, x + w / 2f, y + h + 8), C(0.1f, 0.12f, 0.25f));
                if (p.R() < 0.7f) p.Rect(x + 3, y + 3, x + 7, y + 8, C(1f, 0.85f, 0.3f));
            }
            p.Rect(318, 70, 336, 118, C(0.15f, 0.2f, 0.35f));
            p.Poly(Pts(316, 118, 338, 118, 327, 185), C(0.1f, 0.12f, 0.25f));
            // 糸杉 (炎のような形)
            var cyp = new List<Vector2>();
            for (int i = 0; i <= 24; i++) { float t = i / 24f; cyp.Add(new Vector2(70 - Mathf.Lerp(40, 4, t) + Mathf.Sin(t * 19f) * 6f, t * 340f)); }
            for (int i = 24; i >= 0; i--) { float t = i / 24f; cyp.Add(new Vector2(70 + Mathf.Lerp(40, 4, t) + Mathf.Sin(t * 17f + 1f) * 6f, t * 340f)); }
            p.Poly(cyp, C(0.06f, 0.1f, 0.06f));
            for (int i = 0; i < 260; i++)
            {
                float t = p.R(0f, 0.97f), half = Mathf.Lerp(36, 3, t);
                p.Stroke(70 + p.R(-half, half), t * 340f, 90 + p.R(-20, 20), p.R(12, 24), 2.5f, p.Jit(p.R() < 0.5f ? C(0.12f, 0.22f, 0.1f) : C(0.25f, 0.2f, 0.1f), 0.04f), 0.85f);
            }
            p.Grain(0.025f);
        }

        // 4: 大波 (高い波頭としぶき・小さな富士山・波にもまれる舟)
        static void DrawGreatWave(Painter p)
        {
            p.VGrad(0, PaintH, C(0.88f, 0.83f, 0.68f), C(0.72f, 0.68f, 0.58f));
            p.Rect(0, 350, PaintW, PaintH, C(0.6f, 0.55f, 0.48f), 0.5f);
            // 富士山
            p.Poly(Pts(300, 95, 430, 95, 370, 160), C(0.22f, 0.32f, 0.52f));
            p.Poly(Pts(352, 140, 388, 140, 370, 160), C(0.97f, 0.97f, 0.95f));
            // 海
            p.Rect(0, 0, PaintW, 100, C(0.12f, 0.22f, 0.4f));
            for (int i = 0; i < 6; i++)
            {
                var pts = new List<Vector2>();
                for (int x = 0; x <= PaintW; x += 16) pts.Add(new Vector2(x, 20 + i * 14 + Mathf.Sin(x * 0.04f + i) * 5f));
                p.Path(pts, 2f, C(0.55f, 0.68f, 0.82f), 0.8f);
            }
            // 大波 (右下から立ち上がって、左上で巻き込む)
            var wave = Pts(512, 30, 470, 60, 420, 100, 370, 150, 320, 205, 270, 255, 225, 292, 185, 310, 150, 312, 125, 300, 112, 280, 125, 262, 150, 262, 160, 250, 140, 230, 110, 200, 85, 150, 60, 90, 30, 40, 0, 20, 0, 0, 512, 0);
            p.Poly(wave, C(0.1f, 0.2f, 0.42f));
            // 波の中の筋 (明るい青の線を、波の形にそって何本も)
            for (int k = 1; k <= 7; k++)
            {
                var band = new List<Vector2>();
                for (int i = 0; i < 16; i++)
                {
                    var a = wave[i];
                    band.Add(new Vector2(a.x + k * 9f, a.y - k * 12f));
                }
                p.Path(band, 3f, k % 2 == 0 ? C(0.35f, 0.55f, 0.75f) : C(0.75f, 0.85f, 0.92f), 0.8f);
            }
            // 波頭の泡 (白い粒と、つめのように分かれる先)
            for (int i = 0; i < 15; i++)
            {
                var a = wave[i];
                var b = wave[i + 1];
                for (int k = 0; k < 10; k++)
                {
                    var q = Vector2.Lerp(a, b, k / 10f);
                    p.Ellipse(q.x + p.R(-4, 4), q.y + p.R(-2, 6), p.R(3, 7), p.R(3, 6), C(0.96f, 0.96f, 0.93f));
                    if (k % 3 == 0)
                        for (int c = 0; c < 3; c++)
                        {
                            float ang = 100f + c * 25f + p.R(-10, 10);
                            p.Stroke(q.x + Mathf.Cos(ang * Mathf.Deg2Rad) * 8f, q.y + 6f + Mathf.Sin(ang * Mathf.Deg2Rad) * 8f, ang, 12, 2.5f, C(0.96f, 0.96f, 0.93f));
                        }
                }
            }
            for (int i = 0; i < 90; i++) p.Ellipse(p.R(100, 330), p.R(300, 370), p.R(1.5f, 3.5f), p.R(1.5f, 3.5f), C(0.97f, 0.97f, 0.95f));
            // 舟と、こぎ手
            foreach (var (x, y, ang) in new[] { (300f, 120f, 35f), (170f, 70f, -10f), (440f, 45f, 20f) })
            {
                p.Ellipse(x, y, 52, 8, C(0.88f, 0.78f, 0.55f), 1f, ang);
                for (int k = -3; k <= 3; k++)
                {
                    float r = ang * Mathf.Deg2Rad;
                    p.Ellipse(x + Mathf.Cos(r) * k * 11f - Mathf.Sin(r) * 7f, y + Mathf.Sin(r) * k * 11f + Mathf.Cos(r) * 7f, 3, 3, C(0.15f, 0.15f, 0.2f));
                }
            }
            p.Grain(0.025f);
        }

        // 行きのキャンバス: 美術館の中 (黒い壁の奥行き・黒い柱と金の飾り・黒と灰色の市松の床・天窓の光・金の額の名画・白い彫像)
        static void DrawMuseumInterior(Painter p)
        {
            var wall = C(0.08f, 0.08f, 0.09f);
            var wallSide = C(0.04f, 0.04f, 0.05f);
            var cream = C(0.2f, 0.2f, 0.22f);
            var gold = C(0.95f, 0.75f, 0.25f);
            p.Fill(cream);
            // 天井・床・左右の壁・奥の壁
            p.Poly(Pts(0, PaintH, PaintW, PaintH, 362, 300, 150, 300), C(0.12f, 0.12f, 0.13f));
            p.Poly(Pts(0, 0, 150, 110, 150, 300, 0, PaintH), wallSide);
            p.Poly(Pts(PaintW, 0, 362, 110, 362, 300, PaintW, PaintH), wallSide);
            p.Rect(150, 110, 362, 300, wall);
            // 床: 大理石の市松模様 (奥ほど小さく)
            const int cols = 8, rowsN = 7;
            for (int j = 0; j < rowsN; j++)
            {
                float t0 = 1f - Mathf.Pow(1f - j / (float)rowsN, 1.6f), t1 = 1f - Mathf.Pow(1f - (j + 1) / (float)rowsN, 1.6f);
                float y0 = t0 * 110f, y1 = t1 * 110f;
                float l0 = Mathf.Lerp(0, 150, t0), r0 = Mathf.Lerp(PaintW, 362, t0), l1 = Mathf.Lerp(0, 150, t1), r1 = Mathf.Lerp(PaintW, 362, t1);
                for (int i = 0; i < cols; i++)
                {
                    float a = i / (float)cols, b = (i + 1) / (float)cols;
                    p.Poly(Pts(Mathf.Lerp(l0, r0, a), y0, Mathf.Lerp(l0, r0, b), y0, Mathf.Lerp(l1, r1, b), y1, Mathf.Lerp(l1, r1, a), y1),
                           (i + j) % 2 == 0 ? C(0.1f, 0.1f, 0.11f) : C(0.24f, 0.24f, 0.26f));
                }
            }
            // 金の手すりと腰板
            p.Line(150, 150, 362, 150, 3, gold);
            p.Line(0, 60, 150, 150, 3, gold);
            p.Line(PaintW, 60, 362, 150, 3, gold);
            // 奥の壁の名画 (金の額と中の絵)
            void Frame(float x0, float y0, float x1, float y1, Color a, Color b)
            {
                p.Rect(x0 - 5, y0 - 5, x1 + 5, y1 + 5, gold);
                p.Rect(x0, y0, x1, y1, a);
                p.Ellipse((x0 + x1) / 2f, (y0 + y1) / 2f - 4, (x1 - x0) * 0.3f, (y1 - y0) * 0.25f, b);
                p.Rect(x0, y0, x1, y0 + (y1 - y0) * 0.3f, Color.Lerp(a, b, 0.5f), 0.7f);
            }
            Frame(170, 175, 225, 240, C(0.3f, 0.5f, 0.85f), C(1f, 0.85f, 0.3f));
            Frame(240, 190, 272, 280, C(0.15f, 0.25f, 0.2f), C(0.9f, 0.72f, 0.5f));
            Frame(288, 175, 342, 240, C(0.4f, 0.65f, 0.35f), C(0.95f, 0.95f, 0.95f));
            // 左右の壁の名画 (奥行きにそってゆがんだ額)
            foreach (float s in new[] { -1f, 1f })
            {
                float xa = s < 0 ? 40 : PaintW - 40, xb = s < 0 ? 110 : PaintW - 110;
                p.Poly(Pts(xa, 160, xb, 175, xb, 255, xa, 285), gold);
                p.Poly(Pts(xa + s * -6, 168, xb + s * 5, 180, xb + s * 5, 250, xa + s * -6, 276), s < 0 ? C(0.75f, 0.35f, 0.2f) : C(0.2f, 0.3f, 0.6f));
            }
            // 柱 (左右の壁の手前と奥)
            foreach (var (x, w, y0, y1) in new[] { (20f, 26f, 15f, 370f), (125f, 12f, 92f, 312f), (PaintW - 20f, 26f, 15f, 370f), (PaintW - 125f, 12f, 92f, 312f) })
            {
                p.Rect(x - w / 2f, y0, x + w / 2f, y1, cream);
                p.Rect(x + w * 0.15f, y0, x + w / 2f, y1, C(0.1f, 0.1f, 0.11f));
                p.Rect(x - w * 0.7f, y1 - 8, x + w * 0.7f, y1, gold);
                p.Rect(x - w * 0.7f, y0, x + w * 0.7f, y0 + 8, gold);
            }
            // 天窓と光の筋
            p.Ellipse(256, 345, 90, 20, gold);
            p.Ellipse(256, 345, 84, 16, C(0.75f, 0.88f, 1f));
            for (int k = -2; k <= 2; k++) p.Line(256 + k * 34, 330, 256 + k * 34, 360, 2, gold);
            p.Poly(Pts(176, 335, 336, 335, 360, 40, 150, 40), C(1f, 0.98f, 0.85f), 0.14f);
            // 真ん中の彫像
            p.Rect(236, 70, 276, 125, C(0.2f, 0.2f, 0.22f));
            p.Rect(232, 122, 280, 128, gold);
            p.Ellipse(256, 150, 13, 24, C(0.95f, 0.94f, 0.92f));
            p.Ellipse(256, 182, 9, 10, C(0.95f, 0.94f, 0.92f));
            p.Line(266, 160, 278, 186, 5, C(0.95f, 0.94f, 0.92f));
            p.Ellipse(259, 147, 6, 18, C(0.75f, 0.74f, 0.72f), 0.5f);
            // ベンチ
            p.Rect(120, 40, 200, 52, C(0.45f, 0.08f, 0.1f));
            p.Rect(126, 22, 134, 40, C(0.25f, 0.16f, 0.1f));
            p.Rect(186, 22, 194, 40, C(0.25f, 0.16f, 0.1f));
            p.Grain(0.02f);
        }
    }
}
