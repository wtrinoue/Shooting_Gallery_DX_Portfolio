using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、カジノの本物らしい的と豪華な飾り:
    ///   ダーツボード (20 の数字・ダブル・トリプル・ブル・ソフトダーツの細かい穴。木の扉の付いた壁掛けの箱に入る)、
    ///   大きなスロットマシン (光るアーチの看板・ガラス越しのリール・配当の画面・ボタン台・コイン受け・レバー)、
    ///   床の飾り (カードのテーブル・ルーレット台・壁ぎわの小さなスロット・ロープの柵・小さなシャンデリア)。
    /// 数字の並びなどの決まりは使うが、特定の製品やロゴはまねしない (名前や色はこのゲームのオリジナル)
    /// </summary>
    public partial class GalleryBuilder
    {
        const float DartR = 0.85f; // ダーツボードのダブルの外側の半径 (遠くからでも狙えるように、本物より大きい)
        static readonly float[] DartRings = { 0.045f, 0.1f, 0.53f, 0.6f, 0.9f, 1f };
        static readonly int[] DartNumbers = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };

        // ------------------------------------------------------------------
        // ダーツボード
        // ------------------------------------------------------------------

        /// <summary>
        /// 壁掛けのダーツボード (parent の原点が壁、表は -Z)。木の箱 (両側に開いた扉。扉の内側は点数を書く黒板) の中に、
        /// 黒い数字の輪とダーツボード (絵は1枚のテクスチャ。ソフトダーツの細かい穴まで描く)。点数は RouletteWheel (dartboard) が数える
        /// </summary>
        RouletteWheel BuildDartboard(Transform parent, Vector3 pos, int index)
        {
            var root = Group(parent, "Dartboard");
            root.localPosition = pos;
            var wood = Mat("Dart_Cabinet", new Color(0.38f, 0.2f, 0.1f), null, false, 0.45f);
            var woodDark = Mat("Dart_CabinetDark", new Color(0.22f, 0.11f, 0.05f), null, false, 0.4f);
            var chalk = Mat("Dart_Chalkboard", new Color(0.08f, 0.16f, 0.12f), null, false, 0.1f, matte: true);
            var ringMat = Mat("Dart_NumberRing", new Color(0.06f, 0.06f, 0.07f), null, false, 0.35f);
            var chrome = Mat("Casino_Chrome", new Color(0.78f, 0.8f, 0.85f), null, false, 0.92f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            float outer = DartR * 1.18f;

            // 木の箱 (奥の板と、まわりの枠) と、左右に開いた扉 (内側は黒板)
            float box = outer * 2f + 0.3f;
            Prim(PrimitiveType.Cube, "CabinetBack", root, new Vector3(0f, 0f, 0.06f), new Vector3(box, box, 0.06f), woodDark, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "CabinetSide", root, new Vector3(s * box / 2f, 0f, -0.02f), new Vector3(0.08f, box, 0.2f), wood, false);
                Prim(PrimitiveType.Cube, "CabinetTopBottom", root, new Vector3(0f, s * box / 2f, -0.02f), new Vector3(box + 0.08f, 0.08f, 0.2f), wood, false);
                var door = Group(root, "Door");
                door.localPosition = new Vector3(s * box / 2f, 0f, -0.12f);
                door.localRotation = Quaternion.Euler(0f, s * 65f, 0f); // 外へ大きく開いて、内側の黒板がこちらを向く
                Prim(PrimitiveType.Cube, "DoorPanel", door, new Vector3(s * box / 4f, 0f, 0f), new Vector3(box / 2f, box, 0.05f), wood, false);
                Prim(PrimitiveType.Cube, "Chalkboard", door, new Vector3(s * box / 4f, 0f, -0.03f), new Vector3(box / 2f - 0.14f, box - 0.2f, 0.01f), chalk, false);
                var ink = new Color(0.9f, 0.92f, 0.88f);
                MakeText(door, "ChalkTitle", s < 0 ? "HOME" : "GUEST", new Vector3(s * box / 4f, box / 2f - 0.25f, -0.04f), 0.13f, ink);

                for (int k = 0; k < 4; k++) // 点数のメモ (黒板の数字)
                {
                    MakeText(door, "ChalkScore", ((index * 37 + k * 53 + (s < 0 ? 11 : 29)) % 140 + 20).ToString(), new Vector3(s * box / 4f, box / 2f - 0.55f - k * 0.3f, -0.04f), 0.11f, ink);

                }
                Prim(PrimitiveType.Sphere, "DoorKnob", door, new Vector3(s * (box / 2f - 0.08f), 0f, 0.05f), Vector3.one * 0.06f, gold, false);
            }

            // 回らない台 (ダーツが刺さる当たり判定もこの中)
            var spinner = Group(root, "Spinner");
            // 円柱の厚みは scale.y × 2。表 (-Z) は z = 0.03 - 0.02 = 0.01 で、絵の面 (z = -0.005) より奥にする (前は黒い台の表が絵より手前に来て、絵を隠していた)
            var hit = Prim(PrimitiveType.Cylinder, "Board", spinner, new Vector3(0f, 0f, 0.03f), new Vector3(outer * 2f, 0.02f, outer * 2f), ringMat);
            hit.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var rim = Group(spinner, "Rim");
            rim.localPosition = new Vector3(0f, 0f, -0.025f);
            RingSegments(rim, "Rim", outer, 48, 0.035f, chrome);
            // 数字の輪 (白い数字。どれもまっすぐ立てて読める)
            for (int i = 0; i < DartNumbers.Length; i++)
            {
                float a = i * 18f * Mathf.Deg2Rad;
                MakeText(spinner, "Number", DartNumbers[i].ToString(), new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * (DartR * 1.09f) + Vector3.back * 0.03f, 0.15f, new Color(0.95f, 0.95f, 0.92f));
            }
            // ダーツボードの面 (テクスチャの絵。丸の外は切り抜き)
            var face = Prim(PrimitiveType.Quad, "Face", spinner, new Vector3(0f, 0f, -0.005f), new Vector3(DartR * 2f, DartR * 2f, 1f), DartboardMaterial(), false);
            face.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // ブル (真ん中) は少しふくらませる
            Prim(PrimitiveType.Cylinder, "BullCap", spinner, new Vector3(0f, 0f, -0.016f), new Vector3(DartR * 0.09f, 0.004f, DartR * 0.09f), Mat("Dart_BullCap", new Color(0.8f, 0.1f, 0.12f), new Color(0.2f, 0.02f, 0.03f), false, 0.5f), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Roulette;
            target.manualControl = true;
            target.stayUpOnHit = true;
            target.multiHitInterval = 0.05f;
            target.hitWobbleDeg = 0f;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var board = root.gameObject.AddComponent<RouletteWheel>();
            board.target = target;
            board.spinner = spinner;
            board.speed = 0f; // ダーツボードは回らない
            board.dartboard = true;
            board.boardRadius = DartR;
            board.rings = (float[])DartRings.Clone();
            board.numbers = (int[])DartNumbers.Clone();
            board.pointsPerNumber = 10;
            board.outerBullPoints = 500;
            board.bullPoints = 1000;
            board.bullRadius = DartR * DartRings[0];
            return board;
        }

        /// <summary>
        /// ダーツボードの面の材質 (512×512 の絵を1回だけ描いて PNG に保存)。シングルは黒と生成り、ダブル・トリプルは赤と青、
        /// ブルは緑と赤。区切りの細い線と、ソフトダーツの細かい穴 (六角形に並んだ点) まで描く。丸の外は切り抜き (Alpha Clip)
        /// </summary>
        Material DartboardMaterial()
        {
            const string name = "Dartboard_Face";
            if (mats.TryGetValue(name, out var cached)) return cached;
            const int N = 512;
            var px = new Color[N * N];
            Color black = new Color(0.07f, 0.07f, 0.08f), cream = new Color(0.9f, 0.87f, 0.78f);
            Color red = new Color(0.82f, 0.12f, 0.14f), blue = new Color(0.13f, 0.38f, 0.85f);
            Color bullOuter = new Color(0.1f, 0.55f, 0.3f), bullInner = new Color(0.85f, 0.1f, 0.12f), sep = new Color(0.16f, 0.16f, 0.18f);
            float half = N / 2f;
            const float Hex = 6.5f, Hole = 1.8f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / half - 1f, v = (y + 0.5f) / half - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    int i = y * N + x;
                    if (r > 1f) { px[i] = new Color(0f, 0f, 0f, 0f); continue; }
                    float a = Mathf.Repeat(Mathf.Atan2(u, v) * Mathf.Rad2Deg + 9f, 360f);
                    int sector = Mathf.FloorToInt(a / 18f) % 20;
                    Color c;
                    if (r <= DartRings[0]) c = bullInner;
                    else if (r <= DartRings[1]) c = bullOuter;
                    else if ((r >= DartRings[2] && r <= DartRings[3]) || r >= DartRings[4]) c = sector % 2 == 0 ? red : blue;
                    else c = sector % 2 == 0 ? black : cream;
                    // 区切りの線 (輪の境目と、扇の境目。ブルの中には引かない)
                    float rpx = r * half;
                    bool line = false;
                    foreach (float ring in DartRings) if (Mathf.Abs(rpx - ring * half) < 1.1f) line = true;
                    if (r > DartRings[1])
                    {
                        float off = Mathf.Min(a % 18f, 18f - a % 18f) * Mathf.Deg2Rad * rpx;
                        if (off < 1.1f) line = true;
                    }
                    if (line) c = sep;
                    else
                    {
                        // ソフトダーツの細かい穴 (六角形に並んだ点)
                        float row = Mathf.Round(y / (Hex * 0.866f));
                        float ox = (Mathf.Abs(row) % 2f) * Hex * 0.5f;
                        float cx = Mathf.Round((x - ox) / Hex) * Hex + ox, cy = row * Hex * 0.866f;
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        if (d < Hole) c *= 0.55f;
                        else if (d < Hole + 0.8f) c *= 0.8f;
                    }
                    c.a = 1f;
                    px[i] = c;
                }
            var tex = SaveGeneratedTexture(name, px, N, N, true);
            var mat = new Material(litShader) { name = name };
            mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.25f);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        /// <summary>描いた絵 (色の配列) をテクスチャにする。シーンに生成する時は Generated/Paintings に PNG として保存して読み込み直す</summary>
        Texture2D SaveGeneratedTexture(string name, Color[] px, int w, int h, bool alpha, bool repeat = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp };
            tex.SetPixels(px);
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
                    imp.alphaSource = alpha ? UnityEditor.TextureImporterAlphaSource.FromInput : UnityEditor.TextureImporterAlphaSource.None;
                    imp.alphaIsTransparency = alpha;
                    imp.mipmapEnabled = true;
                    imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    imp.maxTextureSize = Mathf.Max(w, h);
                    imp.textureCompression = UnityEditor.TextureImporterCompression.Compressed;
                    imp.SaveAndReimport();
                }
                return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
#endif
            return tex;
        }

        // ------------------------------------------------------------------
        // スロットマシン
        // ------------------------------------------------------------------

        /// <summary>
        /// 壇と大きなスロットマシン (parent の原点が壇の真ん中の床)。本物の台のような作り:
        /// 上に光る電球の並んだアーチの看板 (LUCKY STAR) と金の星、配当の画面、ガラス越しの3つのリール (7・BAR・ベル・チェリー・星)、
        /// 傾いたボタン台 (光るボタンと大きな SPIN ボタン)、看板の下のパネル、コイン受け、右のレバー、角の銀の縁
        /// </summary>
        SlotMachine BuildSlotStage(Transform parent)
        {
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var black = Mat("Dx_Black", new Color(0.05f, 0.05f, 0.06f), null, false, 0.6f);
            const float StageH = 0.7f;

            // 壇 (2段)
            Prim(PrimitiveType.Cylinder, "Stage", parent, new Vector3(0f, StageH / 2f, 0f), new Vector3(4.4f, StageH / 2f, 3.4f), black);
            FlatRing(parent, "StageTrim", 2.2f, StageH, 0.1f, 0.03f, 40, gold);
            Prim(PrimitiveType.Cylinder, "Step", parent, new Vector3(0f, 0.12f, -0.4f), new Vector3(5f, 0.12f, 3.8f), black, false);
            return BuildSlotMachine(parent, new Vector3(0f, StageH, 0f), 1f);
        }

        /// <summary>
        /// スロットマシン本体 (parent のローカルの pos が足元の真ん中、表 -Z。scale = 大きさの倍率。壁ぎわの小さなスロットにも使う)
        /// </summary>
        SlotMachine BuildSlotMachine(Transform parent, Vector3 pos, float scale)
        {
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var red = Mat("Slot_Red", new Color(0.62f, 0.04f, 0.1f), new Color(0.12f, 0f, 0.02f), false, 0.75f);
            var black = Mat("Dx_Black", new Color(0.05f, 0.05f, 0.06f), null, false, 0.6f);
            var gloss = Mat("Slot_GlossBlack", new Color(0.03f, 0.03f, 0.04f), null, false, 0.9f);
            var chrome = Mat("Casino_Chrome", new Color(0.78f, 0.8f, 0.85f), null, false, 0.92f);

            var root = Group(parent, "SlotMachine");
            root.localPosition = pos;
            root.localScale = Vector3.one * scale;
            const float W = 2.6f, H = 3.4f, D = 1.4f, ReelY = 1.85f, FrontZ = -D / 2f;
            const float WinW = 0.62f, WinH = 0.62f;
            float[] reelX = { -0.72f, 0f, 0.72f };

            // ---- 箱 (中は空洞。窓の所だけ前の板を抜いて、奥のリールが見えるようにする) ----
            Prim(PrimitiveType.Cube, "Back", root, new Vector3(0f, H / 2f, D / 2f - 0.1f), new Vector3(W, H, 0.2f), red);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Side", root, new Vector3(s * (W / 2f - 0.05f), H / 2f, 0f), new Vector3(0.1f, H, D), red);
                Prim(PrimitiveType.Cylinder, "ChromeEdge", root, new Vector3(s * (W / 2f), H / 2f, FrontZ), new Vector3(0.07f, H / 2f, 0.07f), chrome, false); // 角の銀の縁
            }
            Prim(PrimitiveType.Cube, "Top", root, new Vector3(0f, H - 0.05f, 0f), new Vector3(W, 0.1f, D), red);
            Prim(PrimitiveType.Cube, "Inside", root, new Vector3(0f, ReelY, D / 2f - 0.21f), new Vector3(W - 0.2f, 1f, 0.01f), black, false); // リールの後ろは暗く
            // 窓のまわりの前の板 (下・上・窓の間の柱)
            Prim(PrimitiveType.Cube, "FrontLow", root, new Vector3(0f, (ReelY - WinH / 2f + 1.45f) / 2f, FrontZ + 0.05f), new Vector3(W, ReelY - WinH / 2f - 1.45f, 0.1f), gloss);
            Prim(PrimitiveType.Cube, "FrontHigh", root, new Vector3(0f, (ReelY + WinH / 2f + H) / 2f, FrontZ + 0.05f), new Vector3(W, H - ReelY - WinH / 2f, 0.1f), red);
            for (int k = 0; k < 4; k++)
            {
                float left = k == 0 ? -W / 2f : reelX[k - 1] + WinW / 2f;
                float right = k == 3 ? W / 2f : reelX[k] - WinW / 2f;
                Prim(PrimitiveType.Cube, "FrontPillar", root, new Vector3((left + right) / 2f, ReelY, FrontZ + 0.05f), new Vector3(right - left, WinH, 0.1f), gloss);
            }
            // 窓をまとめて囲む銀の太い縁と、窓ごとの金の細い縁
            var bezel = Group(root, "ReelBezel");
            bezel.localPosition = new Vector3(0f, ReelY, FrontZ - 0.02f);
            float bw = reelX[2] - reelX[0] + WinW + 0.24f, bh = WinH + 0.24f;
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "BezelH", bezel, new Vector3(0f, s * bh / 2f, 0f), new Vector3(bw + 0.1f, 0.1f, 0.06f), chrome, false);
                Prim(PrimitiveType.Cube, "BezelV", bezel, new Vector3(s * bw / 2f, 0f, 0f), new Vector3(0.1f, bh + 0.1f, 0.06f), chrome, false);
            }
            foreach (float x in reelX)
            {
                var frame = Group(root, "WindowFrame");
                frame.localPosition = new Vector3(x, ReelY, FrontZ - 0.01f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Cube, "FrameH", frame, new Vector3(0f, s * WinH / 2f, 0f), new Vector3(WinW + 0.06f, 0.03f, 0.03f), gold, false);
                    Prim(PrimitiveType.Cube, "FrameV", frame, new Vector3(s * WinW / 2f, 0f, 0f), new Vector3(0.03f, WinH + 0.06f, 0.03f), gold, false);
                }
            }
            // 真ん中の当たりの線と、窓のガラス (うすい映り込みの筋)
            Prim(PrimitiveType.Cube, "PayLine", root, new Vector3(0f, ReelY, FrontZ - 0.03f), new Vector3(bw - 0.1f, 0.015f, 0.01f), Mat("Slot_PayLine", new Color(1f, 0.2f, 0.2f), null, true), false);
            Prim(PrimitiveType.Quad, "Glass", root, new Vector3(0f, ReelY, FrontZ - 0.045f), new Vector3(bw, bh, 1f), TransparentMat("Slot_Glass", new Color(0.85f, 0.92f, 1f, 0.06f)), false);
            var glare = Prim(PrimitiveType.Quad, "GlassGlare", root, new Vector3(-0.3f, ReelY + 0.05f, FrontZ - 0.047f), new Vector3(0.18f, bh * 1.1f, 1f), TransparentMat("Slot_Glare", new Color(1f, 1f, 1f, 0.12f)), false);
            glare.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);

            // ---- リール (窓の奥で回るドラム。絵柄の板を8枚ぐるりと貼る) ----
            string[] symbols = { "7", "BAR", "STAR", "BELL", "CHERRY", "BAR", "STAR", "CHERRY" };
            var reelFace = Mat("Slot_ReelFace", new Color(0.97f, 0.95f, 0.88f), new Color(0.3f, 0.29f, 0.26f));
            const float ReelR = 0.45f;
            float step = 360f / symbols.Length;
            float faceH = 2f * ReelR * Mathf.Tan(step / 2f * Mathf.Deg2Rad) + 0.01f;
            var reels = new Transform[reelX.Length];
            for (int r = 0; r < reelX.Length; r++)
            {
                var reel = Group(root, $"Reel_{r}");
                reel.localPosition = new Vector3(reelX[r], ReelY, FrontZ + ReelR + 0.11f); // 絵柄の板が前の板のすぐ奥に来る
                for (int s = 0; s < symbols.Length; s++)
                {
                    var face = Group(reel, $"Face_{symbols[s]}");
                    face.localRotation = Quaternion.Euler(s * step, 0f, 0f);
                    Prim(PrimitiveType.Cube, "Plate", face, new Vector3(0f, 0f, -ReelR), new Vector3(WinW - 0.04f, faceH, 0.02f), reelFace, false);
                    SlotSymbol(face, symbols[s], -ReelR - 0.012f);
                }
                reels[r] = reel;
            }

            // ---- 窓の上の配当の画面 (光る文字) ----
            var screen = Group(root, "PayScreen");
            screen.localPosition = new Vector3(0f, ReelY + 0.78f, FrontZ - 0.01f);
            Prim(PrimitiveType.Cube, "ScreenFrame", screen, Vector3.zero, new Vector3(2.2f, 0.62f, 0.03f), chrome, false);
            Prim(PrimitiveType.Cube, "Screen", screen, new Vector3(0f, 0f, -0.02f), new Vector3(2.1f, 0.54f, 0.01f), Mat("Slot_Screen", new Color(0.03f, 0.015f, 0.015f), new Color(0.04f, 0.01f, 0.01f)), false);
            MakeText(screen, "Pay1", "7 7 7 = 3000   BAR = 1500   BELL = 1000", new Vector3(0f, 0.14f, -0.03f), 0.085f, new Color(1f, 0.85f, 0.3f));
            MakeText(screen, "Pay2", "CHERRY = 500   STAR = 300", new Vector3(0f, -0.02f, -0.03f), 0.085f, new Color(1f, 0.92f, 0.75f));
            MakeText(screen, "Pay3", "PAIR = 50   MISS = 10", new Vector3(0f, -0.17f, -0.03f), 0.08f, new Color(0.9f, 0.75f, 0.6f));

            // ---- 傾いたボタン台 (光る小さなボタンと、大きな SPIN ボタン) ----
            var deck = Group(root, "ButtonDeck");
            deck.localPosition = new Vector3(0f, 1.32f, FrontZ - 0.18f);
            deck.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Deck", deck, Vector3.zero, new Vector3(W - 0.1f, 0.1f, 0.42f), gloss, false);
            Prim(PrimitiveType.Cube, "DeckTrim", deck, new Vector3(0f, 0f, -0.21f), new Vector3(W - 0.08f, 0.11f, 0.03f), chrome, false);
            var buttonColors = new[] { new Color(1f, 0.95f, 0.6f), new Color(0.5f, 1f, 0.6f), new Color(0.5f, 0.8f, 1f), new Color(1f, 0.7f, 0.3f), new Color(1f, 0.55f, 0.9f) };
            for (int i = 0; i < buttonColors.Length; i++)
            {
                var c = buttonColors[i];
                Prim(PrimitiveType.Cylinder, "Button", deck, new Vector3(-0.95f + i * 0.33f, 0.065f, 0f), new Vector3(0.2f, 0.02f, 0.14f), Mat($"Slot_Button{i}", c, c * 1.4f, true), false);
            }
            Prim(PrimitiveType.Cylinder, "SpinButton", deck, new Vector3(0.95f, 0.07f, 0f), new Vector3(0.26f, 0.03f, 0.26f), Mat("Slot_SpinButton", new Color(1f, 0.15f, 0.15f), new Color(2.4f, 0.3f, 0.3f), true), false);
            MakeText(deck, "SpinLabel", "SPIN", new Vector3(0.95f, 0.1f, 0.18f), 0.07f, Color.white).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // ---- 下のパネル (看板の名前と星) とコイン受け ----
            var belly = Group(root, "BellyPanel");
            belly.localPosition = new Vector3(0f, 0.78f, FrontZ - 0.01f);
            Prim(PrimitiveType.Cube, "BellyFrame", belly, Vector3.zero, new Vector3(2.2f, 0.72f, 0.03f), gold, false);
            Prim(PrimitiveType.Cube, "BellyArt", belly, new Vector3(0f, 0f, -0.02f), new Vector3(2.08f, 0.6f, 0.01f), Mat("Slot_BellyArt", new Color(0.2f, 0.02f, 0.04f), new Color(0.1f, 0.01f, 0.02f)), false);
            MakeText(belly, "BellyTitle", "LUCKY STAR", new Vector3(0f, 0.05f, -0.03f), 0.22f, new Color(1f, 0.85f, 0.35f));
            MakeText(belly, "BellySub", "- 777 JACKPOT -", new Vector3(0f, -0.18f, -0.03f), 0.09f, new Color(1f, 0.92f, 0.78f));
            var starMat = Mat("Slot_Star", new Color(1f, 0.85f, 0.25f), new Color(2.2f, 1.7f, 0.4f), true);
            foreach (float s in new[] { -1f, 1f })
                ShapeObj(Shape.Star, belly, "BellyStar", new Vector3(s * 0.85f, 0.03f, -0.03f), 0.28f, starMat, false, 0.02f);
            Prim(PrimitiveType.Cube, "CoinTray", root, new Vector3(0f, 0.28f, FrontZ - 0.12f), new Vector3(1.1f, 0.14f, 0.3f), chrome, false);
            Prim(PrimitiveType.Cube, "CoinTrayInside", root, new Vector3(0f, 0.33f, FrontZ - 0.12f), new Vector3(1f, 0.06f, 0.24f), gloss, false);
            Prim(PrimitiveType.Cube, "KickPlate", root, new Vector3(0f, 0.08f, FrontZ - 0.01f), new Vector3(W, 0.16f, 0.05f), chrome, false);

            // ---- 上のアーチの看板 (電球が並ぶ) と金の星 ----
            const float ArchH = 0.8f;
            var arch = Prim(PrimitiveType.Cylinder, "Arch", root, new Vector3(0f, H, 0f), new Vector3(W, D / 2f, ArchH * 2f), red, false);
            arch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 筒の軸を前後に: 前から見ると半円のアーチ
            var marquee = Group(root, "Marquee");
            marquee.localPosition = new Vector3(0f, H, FrontZ - 0.01f);
            Prim(PrimitiveType.Cube, "MarqueeBack", marquee, new Vector3(0f, 0.32f, 0f), new Vector3(W * 0.78f, 0.5f, 0.02f), gloss, false);
            MakeText(marquee, "MarqueeTitle", "LUCKY STAR", new Vector3(0f, 0.4f, -0.02f), 0.26f, new Color(1f, 0.8f, 0.4f));
            MakeText(marquee, "MarqueeSub", "7  7  7", new Vector3(0f, 0.16f, -0.02f), 0.16f, new Color(1f, 0.85f, 0.3f));
            ShapeObj(Shape.Star, root, "TopperStar", new Vector3(0f, H + ArchH + 0.28f, 0f), 0.55f, starMat, false, 0.08f);
            Prim(PrimitiveType.Cylinder, "TopperPost", root, new Vector3(0f, H + ArchH + 0.02f, 0f), new Vector3(0.06f, 0.06f, 0.06f), chrome, false);

            // ---- レバー (右横の前寄り。箱の横から外へ突き出た軸に付いていて、撃つと先の赤い玉ごとプレイヤーの方へお辞儀する) ----
            // 軸は箱の横の取り付け板から X 方向へまっすぐ外へ伸び、根元に金の軸受け、先に金の留め具。レバーはその軸のまわりに回る
            const float LeverY = ReelY - 0.35f, LeverZ = FrontZ + 0.4f, LeverLen = 1.05f, BallD = 0.46f;
            const float AxleOut = 0.48f;                   // 箱の横から、軸の先までの長さ
            const float LeverX = W / 2f + AxleOut - 0.1f;  // レバーの付く所 (軸の先の留め具のすぐ内側)
            Prim(PrimitiveType.Cube, "LeverPlate", root, new Vector3(W / 2f + 0.03f, LeverY, LeverZ), new Vector3(0.06f, 0.55f, 0.45f), chrome, false); // 箱の横の取り付け板
            var axleRot = Quaternion.Euler(0f, 0f, 90f); // (円柱の軸を X 方向へ)
            Prim(PrimitiveType.Cylinder, "AxleBearing", root, new Vector3(W / 2f + 0.1f, LeverY, LeverZ), new Vector3(0.3f, 0.06f, 0.3f), gold, false).transform.localRotation = axleRot;
            Prim(PrimitiveType.Cylinder, "Axle", root, new Vector3(W / 2f + AxleOut / 2f, LeverY, LeverZ), new Vector3(0.11f, AxleOut / 2f, 0.11f), chrome, false).transform.localRotation = axleRot;
            Prim(PrimitiveType.Cylinder, "AxleCap", root, new Vector3(W / 2f + AxleOut, LeverY, LeverZ), new Vector3(0.2f, 0.035f, 0.2f), gold, false).transform.localRotation = axleRot;
            var lever = Group(root, "Lever");
            lever.localPosition = new Vector3(LeverX, LeverY, LeverZ);
            Prim(PrimitiveType.Cylinder, "LeverHub", lever, Vector3.zero, new Vector3(0.22f, 0.07f, 0.22f), chrome, false).transform.localRotation = axleRot; // 軸にはまる輪
            Prim(PrimitiveType.Cylinder, "LeverPole", lever, new Vector3(0f, LeverLen / 2f, 0f), new Vector3(0.08f, LeverLen / 2f, 0.08f), chrome, false);
            Prim(PrimitiveType.Cylinder, "LeverCollar", lever, new Vector3(0f, LeverLen - 0.04f, 0f), new Vector3(0.13f, 0.03f, 0.13f), gold, false);
            Prim(PrimitiveType.Sphere, "LeverBall", lever, new Vector3(0f, LeverLen + BallD / 2f - 0.06f, 0f), Vector3.one * BallD, Mat("Slot_LeverBall", new Color(0.95f, 0.1f, 0.1f), new Color(0.7f, 0.04f, 0.04f), false, 0.85f), false);
            // レバーの当たり判定 (見た目より大きく: 玉のまわりに大きな球、棒のまわりに太いカプセル)。レバーといっしょにお辞儀する。箱の当たり判定でも回る
            var leverHit = Group(lever, "LeverHit");
            var ballHit = leverHit.gameObject.AddComponent<SphereCollider>();
            ballHit.center = new Vector3(0f, LeverLen + BallD / 2f - 0.06f, 0f);
            ballHit.radius = BallD / 2f + 0.22f;
            var poleHit = leverHit.gameObject.AddComponent<CapsuleCollider>();
            poleHit.direction = 1;
            poleHit.center = new Vector3(0f, LeverLen / 2f, 0f);
            poleHit.height = LeverLen + 0.3f;
            poleHit.radius = 0.2f;
            // 「SHOOT!」の目印を置く所 (玉の真上、箱の前の面より手前)
            var leverHint = Group(root, "LeverHint");
            leverHint.localPosition = new Vector3(LeverX, LeverY + LeverLen + BallD + 0.35f, FrontZ - 0.15f);

            // ---- 電球 (アーチのふちと、箱の両側) ----
            var lampOn = Mat("Slot_LampOn", new Color(1f, 0.95f, 0.6f), new Color(3f, 2.4f, 0.8f), true);
            var lampOff = Mat("Slot_LampOff", new Color(0.35f, 0.3f, 0.2f), null, true);
            var lamps = new List<Renderer>();
            for (int i = 0; i <= 16; i++) // アーチのふち
            {
                float a = i / 16f * Mathf.PI;
                var p = new Vector3(Mathf.Cos(a) * (W / 2f - 0.07f), H + Mathf.Sin(a) * (ArchH - 0.07f), FrontZ - 0.02f);
                lamps.Add(Prim(PrimitiveType.Sphere, "Lamp", root, p, Vector3.one * 0.09f, lampOff, false).GetComponent<Renderer>());
            }
            for (int i = 0; i < 10; i++) // 箱の両側
                foreach (float s in new[] { -1f, 1f })
                    lamps.Add(Prim(PrimitiveType.Sphere, "Lamp", root, new Vector3(s * (W / 2f + 0.02f), 0.3f + i * 0.33f, FrontZ - 0.05f), Vector3.one * 0.08f, lampOff, false).GetComponent<Renderer>());
            var winMats = new[]
            {
                Mat("Slot_Win_0", new Color(1f, 0.2f, 0.2f), new Color(2.5f, 0.4f, 0.4f), true), Mat("Slot_Win_1", new Color(1f, 0.85f, 0.2f), new Color(2.5f, 2f, 0.4f), true),
                Mat("Slot_Win_2", new Color(0.3f, 1f, 0.4f), new Color(0.6f, 2.5f, 0.8f), true), Mat("Slot_Win_3", new Color(0.3f, 0.7f, 1f), new Color(0.6f, 1.5f, 2.5f), true),
                Mat("Slot_Win_4", new Color(1f, 0.4f, 0.9f), new Color(2.5f, 0.8f, 2.2f), true)
            };
            MakeText(root, "Shoot", "SHOOT THE LEVER!", new Vector3(0f, 1.52f, FrontZ - 0.45f), 0.1f, Color.white).transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);

            var target = root.gameObject.AddComponent<Target>(); // (箱に当たっても、レバーの大きな当たり判定に当たっても回る)
            target.kind = TargetKind.SlotMachine;
            target.points = 0;
            target.manualControl = true;
            target.stayUpOnHit = true;
            target.multiHitInterval = 0.3f;
            target.hitWobbleDeg = 0f;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var slot = root.gameObject.AddComponent<SlotMachine>();
            slot.target = target;
            slot.lever = lever;
            slot.leverHint = leverHint;
            slot.reels = reels;
            slot.symbols = symbols;
            slot.lamps = lamps.ToArray();
            slot.lampOn = lampOn;
            slot.lampOff = lampOff;
            slot.winMaterials = winMats;
            return slot;
        }

        /// <summary>リールの絵柄 (板の表 -Z の上、z の所に置く)。7 と BAR は文字、ベル・チェリー・星は立体の形</summary>
        void SlotSymbol(Transform face, string symbol, float z)
        {
            switch (symbol)
            {
                case "7":
                    MakeText(face, "Symbol", "7", new Vector3(0f, 0f, z), 0.36f, new Color(0.88f, 0.06f, 0.1f));
                    break;
                case "BAR":
                    Prim(PrimitiveType.Cube, "BarPlate", face, new Vector3(0f, 0f, z + 0.004f), new Vector3(0.44f, 0.17f, 0.008f), Mat("Slot_BarPlate", new Color(0.06f, 0.06f, 0.08f)), false);
                    MakeText(face, "Symbol", "BAR", new Vector3(0f, 0f, z - 0.002f), 0.14f, new Color(1f, 0.85f, 0.3f));
                    break;
                case "BELL":
                {
                    var bell = Mat("Slot_Bell", new Color(1f, 0.78f, 0.15f), new Color(0.35f, 0.22f, 0.02f), false, 0.8f);
                    Prim(PrimitiveType.Sphere, "BellDome", face, new Vector3(0f, 0.03f, z), new Vector3(0.22f, 0.24f, 0.04f), bell, false);
                    Prim(PrimitiveType.Cube, "BellLip", face, new Vector3(0f, -0.09f, z), new Vector3(0.3f, 0.05f, 0.04f), bell, false);
                    Prim(PrimitiveType.Sphere, "BellClapper", face, new Vector3(0f, -0.14f, z), Vector3.one * 0.06f, bell, false);
                    Prim(PrimitiveType.Sphere, "BellTop", face, new Vector3(0f, 0.16f, z), Vector3.one * 0.05f, bell, false);
                    break;
                }
                case "CHERRY":
                {
                    var cherry = Mat("Slot_Cherry", new Color(0.85f, 0.05f, 0.1f), new Color(0.2f, 0f, 0.02f), false, 0.85f);
                    var stem = Mat("Slot_CherryStem", new Color(0.2f, 0.55f, 0.15f));
                    foreach (var (x, y) in new[] { (-0.08f, -0.07f), (0.08f, -0.1f) })
                    {
                        Prim(PrimitiveType.Sphere, "Cherry", face, new Vector3(x, y, z), new Vector3(0.14f, 0.14f, 0.04f), cherry, false);
                        var st = Prim(PrimitiveType.Cube, "Stem", face, new Vector3((x + 0.02f) / 2f, (y + 0.13f) / 2f + 0.02f, z + 0.002f), new Vector3(0.015f, 0.2f, 0.01f), stem, false);
                        st.transform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(0.02f - x, 0.15f - y) * Mathf.Rad2Deg);
                    }
                    var leaf = Prim(PrimitiveType.Sphere, "Leaf", face, new Vector3(0.07f, 0.13f, z), new Vector3(0.12f, 0.05f, 0.02f), stem, false);
                    leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                    break;
                }
                default: // STAR
                    ShapeObj(Shape.Star, face, "Symbol3D", new Vector3(0f, 0f, z), 0.32f, Mat("Slot_StarSymbol", new Color(1f, 0.8f, 0.1f), new Color(0.5f, 0.35f, 0.02f), false, 0.7f), false, 0.03f);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // 床の飾り (豪華にする。どれも的ではない。壁のダーツボードの前はふさがない低い物だけ)
        // ------------------------------------------------------------------

        void BuildCasinoFloorDecor(Transform lux)
        {
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var chrome = Mat("Casino_Chrome", new Color(0.78f, 0.8f, 0.85f), null, false, 0.92f);
            var felt = Mat("Casino_Felt", new Color(0.05f, 0.4f, 0.2f), null, false, 0.1f, matte: true);
            var leather = Mat("Casino_Leather", new Color(0.25f, 0.1f, 0.05f), null, false, 0.6f);
            var woodDark = Mat("Dart_CabinetDark", new Color(0.22f, 0.11f, 0.05f), null, false, 0.4f);
            var velvet = Mat("Casino_Velvet", new Color(0.36f, 0.03f, 0.06f), new Color(0.05f, 0f, 0.01f), false, 0.3f);
            var chipMats = new[] { Mat("Chip_Red", new Color(0.85f, 0.1f, 0.1f)), Mat("Chip_Blue", new Color(0.15f, 0.3f, 0.9f)), Mat("Chip_Green", new Color(0.1f, 0.6f, 0.25f)), Mat("Chip_Black", new Color(0.08f, 0.08f, 0.08f)) };
            var card = Mat("Casino_Card", new Color(0.95f, 0.94f, 0.9f), null, false, 0.3f);

            // カードのテーブル (楕円の緑のテーブル・革のふち・チップの山・配られたカード・まわりの椅子)
            foreach (float a in new[] { 120f, 240f })
            {
                var rot = Quaternion.Euler(0f, a, 0f);
                var t = Group(lux, "CardTable");
                t.localPosition = rot * new Vector3(0f, 0f, 4.3f);
                t.localRotation = rot;
                Prim(PrimitiveType.Cylinder, "Pedestal", t, new Vector3(0f, 0.38f, 0f), new Vector3(0.35f, 0.38f, 0.35f), woodDark, false);
                Prim(PrimitiveType.Cylinder, "Rim", t, new Vector3(0f, 0.8f, 0f), new Vector3(2.2f, 0.05f, 1.3f), leather, false);
                Prim(PrimitiveType.Cylinder, "Felt", t, new Vector3(0f, 0.86f, 0f), new Vector3(2f, 0.012f, 1.12f), felt, false);
                FlatRing(t, "FeltLine", 0.42f, 0.875f, 0.02f, 0.004f, 32, gold);
                for (int k = 0; k < 5; k++)
                {
                    float x = -0.7f + k * 0.35f;
                    var c = Prim(PrimitiveType.Cube, "Card", t, new Vector3(x, 0.876f, -0.25f), new Vector3(0.1f, 0.003f, 0.14f), card, false);
                    c.transform.localRotation = Quaternion.Euler(0f, Random.Range(-12f, 12f), 0f);
                    for (int h = 0; h < 4; h++)
                        Prim(PrimitiveType.Cylinder, "Chip", t, new Vector3(x + 0.12f, 0.88f + h * 0.02f, -0.05f), new Vector3(0.09f, 0.008f, 0.09f), chipMats[(k + h) % chipMats.Length], false);
                }
                Prim(PrimitiveType.Cube, "ChipRack", t, new Vector3(0f, 0.9f, 0.4f), new Vector3(0.6f, 0.06f, 0.16f), chrome, false);
                for (int k = 0; k < 4; k++)
                    Prim(PrimitiveType.Cube, "RackChips", t, new Vector3(-0.21f + k * 0.14f, 0.93f, 0.4f), new Vector3(0.11f, 0.04f, 0.13f), chipMats[k], false);
                for (int k = 0; k < 4; k++) // 椅子 (手前の弧)
                {
                    float sa = (-45f + k * 30f) * Mathf.Deg2Rad;
                    var p = new Vector3(Mathf.Sin(sa) * 1.15f, 0f, -Mathf.Cos(sa) * 0.9f);
                    Prim(PrimitiveType.Cylinder, "StoolPole", t, p + Vector3.up * 0.3f, new Vector3(0.05f, 0.3f, 0.05f), chrome, false);
                    Prim(PrimitiveType.Cylinder, "StoolSeat", t, p + Vector3.up * 0.62f, new Vector3(0.36f, 0.05f, 0.36f), velvet, false);
                }
            }

            // (ルーレットと壁ぎわのスロットは、遊べるゲームとして BuildCasino が置く。床の飾りには置かない)

            // スロットの壇の前の、金の柱と赤いロープの柵
            {
                var center = new Vector3(0f, 0f, 4.6f);
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= 6; i++)
                {
                    float a = (-70f + i * (140f / 6f)) * Mathf.Deg2Rad;
                    var p = center + new Vector3(Mathf.Sin(a) * 2.75f, 0f, -Mathf.Cos(a) * 2.75f);
                    Prim(PrimitiveType.Cylinder, "RopePost", lux, p + Vector3.up * 0.45f, new Vector3(0.07f, 0.45f, 0.07f), gold, false);
                    Prim(PrimitiveType.Cylinder, "RopePostBase", lux, p + Vector3.up * 0.03f, new Vector3(0.28f, 0.03f, 0.28f), gold, false);
                    Prim(PrimitiveType.Sphere, "RopePostTop", lux, p + Vector3.up * 0.93f, Vector3.one * 0.1f, gold, false);
                    if (i > 0) // ロープ (まん中が少したるむ)
                    {
                        var mid = (prev + p) / 2f + Vector3.up * 0.68f;
                        DomeLine(lux, "Rope", prev + Vector3.up * 0.85f, mid, 0.04f, velvet);
                        DomeLine(lux, "Rope", mid, p + Vector3.up * 0.85f, 0.04f, velvet);
                    }
                    prev = p;
                }
            }

            // 小さなシャンデリア (4つ。明かりは使わず、光る球だけ) と、天井の金の放射の梁
            var bulb = Mat("Chandelier_Bulb", new Color(1f, 0.95f, 0.8f), new Color(3f, 2.6f, 1.8f), true);
            var crystal = Mat("Crystal", new Color(0.85f, 0.95f, 1f), new Color(0.4f, 0.5f, 0.6f), false, 0.95f);
            foreach (float a in new[] { 45f, 135f, 225f, 315f })
            {
                var rot = Quaternion.Euler(0f, a, 0f);
                var ch = Group(lux, "SmallChandelier");
                ch.localPosition = rot * new Vector3(0f, CasinoH - 2.4f, 4.4f);
                Prim(PrimitiveType.Cylinder, "Chain", ch, new Vector3(0f, 1.2f, 0f), new Vector3(0.04f, 1.2f, 0.04f), gold, false);
                FlatRing(ch, "ChandRing", 0.4f, 0f, 0.04f, 0.03f, 16, gold);
                for (int i = 0; i < 8; i++)
                {
                    float b = i * Mathf.PI * 2f / 8f;
                    Prim(PrimitiveType.Sphere, "Bulb", ch, new Vector3(Mathf.Sin(b) * 0.4f, 0.07f, Mathf.Cos(b) * 0.4f), Vector3.one * 0.08f, bulb, false);
                    Prim(PrimitiveType.Sphere, "Crystal", ch, new Vector3(Mathf.Sin(b) * 0.4f, -0.1f, Mathf.Cos(b) * 0.4f), new Vector3(0.035f, 0.09f, 0.035f), crystal, false);
                }
            }
            for (int i = 0; i < 16; i++)
            {
                var beam = Prim(PrimitiveType.Cube, "CeilingBeam", lux, Quaternion.Euler(0f, i * 22.5f, 0f) * new Vector3(0f, CasinoH - 0.1f, (CasinoR + 1.5f) / 2f), new Vector3(0.12f, 0.08f, CasinoR - 1.5f), gold, false);
                beam.transform.localRotation = Quaternion.Euler(0f, i * 22.5f, 0f);
            }
        }
    }
}
