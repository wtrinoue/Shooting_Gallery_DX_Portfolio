using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 美術館モード (絵筆の的を撃つと始まる) の生成:
    ///   行き: 目の前に出るイーゼルと真っ白なキャンバス、ひとりでに美術館の中を描く筆と、その線。
    ///   美術館: 16角形の円い展示室 (深い赤の壁・白い柱・大理石の床・天窓)。後ろに大きな出入り口。上の段に飾りの名画、柱のそばに飾りの彫像、ベンチ。
    ///   的 (未完成の作品): 壁の白いキャンバス 12枚・イーゼルのキャンバス 4枚・台の上の灰色の直方体 8つ。当たるたびに絵が塗られていく / 彫られていく。
    ///   絵筆の的の見た目 (パレットと筆)。
    /// プレイヤーは中心から動かない。
    /// </summary>
    public partial class GalleryBuilder
    {
        public const string MuseumBgmPath = "Assets/Source/museum_stage/cks_russianoodori.mp3"; // 美術館モードの BGM
        const float MuseumR = 14f;       // 壁までの半径
        const float MuseumWallH = 14f;   // 壁の高さ (天井を高くして、広々とした大きな展示室に)
        const int MuseumSides = 16;

        ArtMuseum BuildMuseum()
        {
            var go = new GameObject("ArtMuseum");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            // ---- 行き: 目の前のイーゼルとキャンバス、ひとりでに描く筆 ----
            var easel = BuildIntroEasel(go.transform, out var brush, out var painting);

            // ---- 美術館 (中にいる間だけ出す) ----
            var hall = Group(go.transform, "MuseumHall");
            BuildMuseumHall(hall);

            // ---- 的: 未完成の作品 ----
            var works = new List<Artwork>();
            var chip = TransparentMat("Museum_Chip", new Color(0.75f, 0.74f, 0.72f, 0.85f));
            var sparkle = TransparentMat("Museum_Sparkle", new Color(1f, 0.88f, 0.45f, 0.75f));
            int design = 0;
            // 配置: プレイヤー (真ん中) から見て、どの作品も別の作品の前に重ならないように、向きをずらして置く。
            //   壁 16 面 (22.5° ごと) のうち、壁のキャンバスは 12 面、イーゼルは残りの 3 面 (0・4・12。後ろの 8 は出入り口) の手前、
            //   彫刻は壁のつなぎ目 (柱の前。左右のキャンバスのすき間) の手前
            // 壁のキャンバス (出入り口と、イーゼルを置く面はよける)
            foreach (int panel in new[] { 1, 2, 3, 5, 6, 7, 9, 10, 11, 13, 14, 15 })
            {
                float a = panel * 360f / MuseumSides;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var pos = dir * (MuseumR * Mathf.Cos(Mathf.PI / MuseumSides) - 0.17f) + Vector3.up * 2.3f;
                works.Add(BuildCanvasWork(hall, $"WallCanvas_{panel}", pos, Quaternion.LookRotation(dir), 2.4f, 1.8f, design++ % CanvasDesigns, false, chip, sparkle));
            }
            // イーゼルのキャンバス (床の上。少し上を向けて立てかける。後ろの壁にはキャンバスがない面)
            foreach (int panel in new[] { 0, 4, 12 })
            {
                var dir = Quaternion.Euler(0f, panel * 360f / MuseumSides, 0f) * Vector3.forward;
                works.Add(BuildCanvasWork(hall, $"EaselCanvas_{panel}", dir * 6f, Quaternion.LookRotation(dir), 1.6f, 1.2f, design++ % CanvasDesigns, true, chip, sparkle));
            }
            // 台の上の彫刻 (壁のつなぎ目の手前。左右の壁のキャンバスのすき間に見える)
            int sculpt = 0;
            foreach (float joint in new[] { 1.5f, 2.5f, 5.5f, 6.5f, 9.5f, 10.5f, 13.5f, 14.5f })
            {
                var dir = Quaternion.Euler(0f, joint * 360f / MuseumSides, 0f) * Vector3.forward;
                works.Add(BuildSculptureWork(hall, $"Sculpture_{sculpt}", dir * 8.5f, Quaternion.LookRotation(dir), sculpt % SculptureDesigns, chip, sparkle));
                sculpt++;
            }
            // 天井から吊るした粘土の作品 (飛行機・恐竜の骨・クジラ。目の高さよりずっと上なので、ほかの作品に重ならない)
            BuildHangingWorks(hall, works, sparkle);

            var museum = go.AddComponent<ArtMuseum>();
            museum.easel = easel;
            museum.introBrush = brush;
            museum.introPainting = painting;
            museum.hall = hall.gameObject;
            museum.bgmClip = LoadSourceAsset(MuseumBgmPath, a => a.museumBgm); // BGM (実機では GalleryRuntimeAssets から)
            museum.artworks = works.ToArray();
            hall.gameObject.SetActive(false);
            go.SetActive(true);
            return museum;
        }

        Material PaintMat(string name, Color c) => Mat("Paint_" + name, c, null, false, 0.15f, matte: true);

        // ------------------------------------------------------------------
        // 行き: イーゼルと、美術館の中を描く線
        // ------------------------------------------------------------------

        /// <summary>
        /// 目の前 (2.2m 先) のイーゼルと真っ白なキャンバス (幅 1.6m)。キャンバスの表に、美術館の中の絵 (ビットマップ) を重ねておき、
        /// ArtMuseum が筆の動きに合わせて塗っていく (PaintingMask)。脚と横木はキャンバスの後ろ (表は -Z。プレイヤーの方)
        /// </summary>
        Transform BuildIntroEasel(Transform parent, out Transform brush, out Renderer painting)
        {
            var root = Group(parent, "IntroEasel");
            root.localPosition = new Vector3(0f, 0f, 2.2f);
            var wood = Mat("Easel_Wood", new Color(0.55f, 0.36f, 0.2f), null, false, 0.3f);
            const float W = 1.6f, H = 1.2f, CY = 1.5f;
            // 脚 (前の2本はキャンバスの後ろを通り、上で合わさる。後ろの1本は斜めに支える) と、キャンバスを載せる横木 (下の辺の前)
            float top = CY + H / 2f + 0.18f;
            foreach (float s in new[] { -1f, 1f })
                DomeLine(root, "EaselLeg", new Vector3(s * 0.62f, 0f, 0.16f), new Vector3(s * 0.05f, top, 0.07f), 0.05f, wood);
            DomeLine(root, "EaselLegBack", new Vector3(0f, 0f, 0.85f), new Vector3(0f, top - 0.15f, 0.1f), 0.05f, wood);
            Prim(PrimitiveType.Cube, "EaselLedge", root, new Vector3(0f, CY - H / 2f - 0.03f, -0.02f), new Vector3(W + 0.12f, 0.06f, 0.16f), wood, false);
            Prim(PrimitiveType.Cube, "EaselClamp", root, new Vector3(0f, CY + H / 2f + 0.03f, 0.03f), new Vector3(0.22f, 0.06f, 0.1f), wood, false);
            Prim(PrimitiveType.Cube, "Canvas", root, new Vector3(0f, CY, 0.02f), new Vector3(W, H, 0.04f), Mat("Canvas_White", new Color(0.97f, 0.96f, 0.93f), null, false, 0.1f, matte: true), false);

            // 美術館の中の絵 (キャンバスの表のすぐ手前。初めは何も塗られていない)
            var pic = Prim(PrimitiveType.Quad, "Painting", root, new Vector3(0f, CY, -0.002f), new Vector3(W - 0.04f, H - 0.04f, 1f), IntroPaintingMat(), false);
            painting = pic.GetComponent<Renderer>();
            painting.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ひとりでに描く筆 (原点が筆先。柄は上へ)
            brush = BuildBrush(parent, "IntroBrush", 1.4f);
            root.gameObject.SetActive(false);
            return root;
        }

        /// <summary>絵筆 (原点が筆先。柄はローカル +Y へ)。size 倍の大きさ</summary>
        Transform BuildBrush(Transform parent, string name, float size)
        {
            var b = Group(parent, name);
            var tip = Group(b, "Tip");
            tip.localScale = Vector3.one * size;
            Prim(PrimitiveType.Capsule, "Bristle", tip, new Vector3(0f, 0.035f, 0f), new Vector3(0.022f, 0.035f, 0.022f), PaintMat("BrushTip", new Color(0.8f, 0.2f, 0.3f)), false);
            Prim(PrimitiveType.Cylinder, "Ferrule", tip, new Vector3(0f, 0.085f, 0f), new Vector3(0.02f, 0.02f, 0.02f), Mat("Brush_Ferrule", new Color(0.8f, 0.8f, 0.82f), null, false, 0.8f), false);
            Prim(PrimitiveType.Cylinder, "Handle", tip, new Vector3(0f, 0.19f, 0f), new Vector3(0.014f, 0.085f, 0.014f), Mat("Brush_Handle", new Color(0.75f, 0.15f, 0.12f), null, false, 0.6f), false);
            return b;
        }

        /// <summary>トンカチ (原点が頭の真ん中。柄はローカル -Y へ)</summary>
        Transform BuildHammer(Transform parent, string name)
        {
            var h = Group(parent, name);
            Prim(PrimitiveType.Cube, "HammerHead", h, Vector3.zero, new Vector3(0.18f, 0.07f, 0.07f), Mat("Hammer_Iron", new Color(0.35f, 0.36f, 0.4f), null, false, 0.7f), false);
            Prim(PrimitiveType.Cylinder, "HammerHandle", h, new Vector3(0f, -0.17f, 0f), new Vector3(0.035f, 0.16f, 0.035f), Mat("Easel_Wood", new Color(0.55f, 0.36f, 0.2f), null, false, 0.3f), false);
            return h;
        }

        // ------------------------------------------------------------------
        // 美術館の建物
        // ------------------------------------------------------------------

        void BuildMuseumHall(Transform hall)
        {
            // 黒を基調にする (真っ白なキャンバスと灰色の石の塊 = 的が、ひと目で分かるように)。金の縁取りだけ明るい
            var marble = Mat("Museum_Marble", new Color(0.07f, 0.07f, 0.08f), null, false, 0.85f);
            var marbleDark = Mat("Museum_MarbleDark", new Color(0.17f, 0.17f, 0.18f), null, false, 0.7f);
            var wall = Mat("Museum_Wall", new Color(0.045f, 0.045f, 0.05f), null, false, 0.15f, matte: true);
            var cream = Mat("Museum_Trim", new Color(0.11f, 0.11f, 0.12f), null, false, 0.5f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var woodDark = Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f));
            var glass = Mat("Museum_Skylight", new Color(0.75f, 0.88f, 1f), new Color(0.9f, 1.05f, 1.2f));
            float inner = MuseumR * Mathf.Cos(Mathf.PI / MuseumSides);
            float panelW = 2f * MuseumR * Mathf.Sin(Mathf.PI / MuseumSides) + 0.05f;

            // 床: 大理石と、濃い大理石の輪と真ん中の星。
            // 上に重ねる模様は、上の面どうしを 1cm ずつ離す (前は 4mm 差で、遠くの所がちかちかした)。元の世界の地面 (高さ 0) とも 3cm 離す
            const float Step = 0.01f;
            void Layer(string name, float top, float diameter, Material mat) =>
                Prim(PrimitiveType.Cylinder, name, hall, new Vector3(0f, top - 0.0025f, 0f), new Vector3(diameter, 0.0025f, diameter), mat, false);
            const float FloorTop = 0.03f;
            Prim(PrimitiveType.Cylinder, "Floor", hall, new Vector3(0f, FloorTop / 2f, 0f), new Vector3(MuseumR * 2.1f, FloorTop / 2f, MuseumR * 2.1f), marble, false);
            Layer("FloorRing", FloorTop + Step, MuseumR * 1.73f, marbleDark);
            Layer("FloorRingInner", FloorTop + Step * 2f, MuseumR * 1.67f, marble);
            Layer("FloorCenter", FloorTop + Step * 3f, 6f, marbleDark);
            Layer("FloorCenterInner", FloorTop + Step * 4f, 5.5f, marble);
            // 星: 真ん中から外へ向かう16本の帯 (帯どうしは重ならない所から始める) と、真ん中の丸い飾り (帯より上)
            for (int k = 0; k < 16; k++)
            {
                var rot = Quaternion.Euler(0f, k * 22.5f, 0f);
                var ray = Prim(PrimitiveType.Cube, "FloorStar", hall, rot * new Vector3(0f, FloorTop + Step * 5f - 0.0025f, 1.65f), new Vector3(0.22f, 0.005f, 1.9f), k % 2 == 0 ? gold : marbleDark, false);
                ray.transform.localRotation = rot;
            }
            Layer("FloorMedallion", FloorTop + Step * 6f, 1.5f, gold);
            Layer("FloorMedallionInner", FloorTop + Step * 7f, 1.2f, marbleDark);

            // 壁 (16枚)・柱・天井の縁・床の縁
            for (int i = 0; i < MuseumSides; i++)
            {
                float a = i * 360f / MuseumSides;
                var rot = Quaternion.Euler(0f, a, 0f);
                var dir = rot * Vector3.forward;
                if (i == 4 || i == 8 || i == 12)
                {
                    // 大きな出入り口 (後ろと左右): 壁を左右と上だけにして、その先へ果てしなく続く廊下 (霧にかすんで、果てが見えない)
                    float doorW = CorridorW, doorH = CorridorH;
                    foreach (float s in new[] { -1f, 1f })
                        Prim(PrimitiveType.Cube, "WallSide", hall, dir * inner + rot * new Vector3(s * (panelW + doorW) / 4f, MuseumWallH / 2f, 0.15f), new Vector3((panelW - doorW) / 2f, MuseumWallH, 0.3f), wall, false).transform.localRotation = rot;
                    Prim(PrimitiveType.Cube, "WallTop", hall, dir * inner + rot * new Vector3(0f, (MuseumWallH + doorH) / 2f, 0.15f), new Vector3(doorW, MuseumWallH - doorH, 0.3f), wall, false).transform.localRotation = rot;
                    BuildCorridor(hall, dir, rot, inner, i);
                    var arch = Prim(PrimitiveType.Cube, "DoorFrame", hall, dir * (inner - 0.02f) + rot * new Vector3(0f, doorH + 0.1f, 0f), new Vector3(doorW + 0.3f, 0.2f, 0.12f), gold, false);
                    arch.transform.localRotation = rot;
                    MakeText(hall, "DoorSign", i == 8 ? "GALLERY" : i == 4 ? "EAST WING" : "WEST WING", dir * (inner - 0.1f) + Vector3.up * (doorH + 0.55f), 0.32f, new Color(1f, 0.85f, 0.45f)).transform.localRotation = Quaternion.LookRotation(dir);
                }
                else
                {
                    var panel = Prim(PrimitiveType.Cube, "Wall", hall, dir * (inner + 0.15f) + Vector3.up * MuseumWallH / 2f, new Vector3(panelW, MuseumWallH, 0.3f), wall, false);
                    panel.transform.localRotation = rot;
                    var rail = Prim(PrimitiveType.Cube, "ChairRail", hall, dir * (inner - 0.01f) + Vector3.up * 0.95f, new Vector3(panelW, 0.06f, 0.04f), gold, false);
                    rail.transform.localRotation = rot;
                }
                var base_ = Prim(PrimitiveType.Cube, "Baseboard", hall, dir * (inner - 0.02f) + Vector3.up * 0.12f, new Vector3(panelW, 0.24f, 0.06f), woodDark, false);
                base_.transform.localRotation = rot;
                var cornice = Prim(PrimitiveType.Cube, "Cornice", hall, dir * (inner - 0.1f) + Vector3.up * (MuseumWallH - 0.2f), new Vector3(panelW, 0.4f, 0.25f), cream, false);
                cornice.transform.localRotation = rot;
                // 柱 (壁のつなぎ目)
                var colDir = Quaternion.Euler(0f, a + 180f / MuseumSides, 0f) * Vector3.forward;
                var colPos = colDir * (MuseumR - 0.3f);
                Prim(PrimitiveType.Cylinder, "Column", hall, colPos + Vector3.up * MuseumWallH / 2f, new Vector3(0.6f, MuseumWallH / 2f, 0.6f), cream, false);
                Prim(PrimitiveType.Cube, "ColumnBase", hall, colPos + Vector3.up * 0.15f, new Vector3(0.85f, 0.3f, 0.85f), cream, false).transform.localRotation = Quaternion.LookRotation(colDir);
                Prim(PrimitiveType.Cube, "ColumnCapital", hall, colPos + Vector3.up * (MuseumWallH - 0.55f), new Vector3(0.85f, 0.25f, 0.85f), gold, false).transform.localRotation = Quaternion.LookRotation(colDir);
            }

            // 天井と天窓 (光る)
            Prim(PrimitiveType.Cylinder, "Ceiling", hall, new Vector3(0f, MuseumWallH + 0.05f, 0f), new Vector3(MuseumR * 2.1f, 0.05f, MuseumR * 2.1f), cream, false);
            Prim(PrimitiveType.Cylinder, "Skylight", hall, new Vector3(0f, MuseumWallH - 0.02f, 0f), new Vector3(MuseumR * 0.8f, 0.01f, MuseumR * 0.8f), glass, false);
            var frame = Group(hall, "SkylightFrame");
            frame.localPosition = new Vector3(0f, MuseumWallH - 0.04f, 0f);
            frame.localRotation = Quaternion.Euler(90f, 0f, 0f);
            RingSegments(frame, "SkylightRing", MuseumR * 0.4f, 48, 0.14f, gold);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, "SkylightBar", hall, new Vector3(0f, MuseumWallH - 0.05f, 0f), new Vector3(0.1f, 0.05f, MuseumR * 0.8f), gold, false).transform.localRotation = Quaternion.Euler(0f, k * 45f, 0f);

            // 上の段の飾りの名画 (的のキャンバスより高い所。完成した絵なので、白い的と見分けがつく)
            int deco = 0;
            for (int i = 1; i < MuseumSides; i += 2)
            {
                if (i == 8) continue;
                var rot = Quaternion.Euler(0f, i * 360f / MuseumSides, 0f);
                DecorPainting(hall, rot * Vector3.forward * (inner - 0.03f) + Vector3.up * 7.6f, rot, 2.8f, 1.9f, deco++);
            }
            // 柱のそばの飾りの彫像 (完成品)
            for (int k = 0; k < 4; k++)
            {
                var dir = Quaternion.Euler(0f, k * 90f + 11.25f + 22.5f * 3f, 0f) * Vector3.forward;
                var s = Group(hall, "DecorStatue");
                s.localPosition = dir * (MuseumR - 1.3f);
                s.localRotation = Quaternion.LookRotation(-dir);
                Prim(PrimitiveType.Cube, "Plinth", s, new Vector3(0f, 0.6f, 0f), new Vector3(0.8f, 1.2f, 0.8f), marbleDark, false);
                var top = Group(s, "Statue");
                top.localPosition = Vector3.up * 1.2f;
                top.localScale = Vector3.one * 1.5f;
                SculptureShape(top, (k + 1) % SculptureDesigns, 2, SculpturePalette(2, k + 1), gold); // 飾り (的ではない) は白くしない
            }
            // ベンチ
            foreach (float a in new[] { 135f, 315f })
            {
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var b = Group(hall, "Bench");
                b.localPosition = dir * 4f;
                b.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir));
                Prim(PrimitiveType.Cube, "BenchSeat", b, new Vector3(0f, 0.42f, 0f), new Vector3(0.5f, 0.1f, 1.6f), Mat("Museum_Velvet", new Color(0.45f, 0.08f, 0.1f), null, false, 0.2f), false);
                foreach (float z in new[] { -0.65f, 0.65f })
                    Prim(PrimitiveType.Cube, "BenchLeg", b, new Vector3(0f, 0.2f, z), new Vector3(0.45f, 0.4f, 0.08f), woodDark, false);
            }

            // 天窓からの明かり (1灯だけ。あとは環境光)
            var lightGo = new GameObject("MuseumLight");
            lightGo.transform.SetParent(hall, false);
            lightGo.transform.localPosition = new Vector3(0f, MuseumWallH - 0.6f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 5f; // 天井が高く床まで遠いので、少し強く
            light.range = 36f;
            light.shadows = LightShadows.None;
        }

        const float CorridorW = 4f;      // 廊下の幅 (出入り口の幅)
        const float CorridorH = 6.5f;    // 廊下の高さ (出入り口の高さ)
        const float CorridorLength = 90f; // 廊下の長さ (霧で 60m 先からは見えない。果ては見えず、どこまでも続いているように見える)

        /// <summary>
        /// 出入り口の先へまっすぐ続く長い廊下 (黒い壁・黒い大理石の床と赤いじゅうたん・金のアーチ・天井の明かり・両側の名画)。
        /// アーチと名画が一定の間隔で並び、遠くほど霧にかすんで消えていくので、奥行きがどこまでも続いて見える。
        /// 部品は多いが動かないので、ArtMuseum が始める前に材質ごとに1つにまとめる (MeshMerger)
        /// </summary>
        void BuildCorridor(Transform hall, Vector3 dir, Quaternion rot, float inner, int panel)
        {
            var g = Group(hall, $"Corridor_{panel}");
            g.localPosition = dir * inner;
            g.localRotation = rot; // 廊下の中: +Z が奥、+X が右
            var floor = Mat("Museum_Marble", new Color(0.07f, 0.07f, 0.08f), null, false, 0.85f);
            var tile = Mat("Museum_MarbleDark", new Color(0.17f, 0.17f, 0.18f), null, false, 0.7f);
            var wall = Mat("Museum_Wall", new Color(0.045f, 0.045f, 0.05f), null, false, 0.15f, matte: true);
            var trim = Mat("Museum_Trim", new Color(0.11f, 0.11f, 0.12f), null, false, 0.5f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var carpet = Mat("Museum_Velvet", new Color(0.45f, 0.08f, 0.1f), null, false, 0.2f);
            var lamp = Mat("Corridor_Lamp", new Color(1f, 0.93f, 0.78f), new Color(1.6f, 1.4f, 1.05f), true);
            const float W = CorridorW, H = CorridorH, L = CorridorLength;
            // 床の上の面は、展示室の床 (0.03) より上で、模様ごとに 1cm ずつ離す (ちかちかしないように)
            Prim(PrimitiveType.Cube, "Floor", g, new Vector3(0f, 0.025f, L / 2f), new Vector3(W, 0.03f, L), floor, false);         // 上の面 0.04
            Prim(PrimitiveType.Cube, "Carpet", g, new Vector3(0f, 0.0575f, L / 2f), new Vector3(W * 0.45f, 0.005f, L), carpet, false); // 上の面 0.06
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Wall", g, new Vector3(s * (W / 2f + 0.15f), H / 2f, L / 2f), new Vector3(0.3f, H, L), wall, false);
                Prim(PrimitiveType.Cube, "Rail", g, new Vector3(s * (W / 2f - 0.01f), 0.95f, L / 2f), new Vector3(0.04f, 0.06f, L), gold, false);
                Prim(PrimitiveType.Cube, "Baseboard", g, new Vector3(s * (W / 2f - 0.02f), 0.12f, L / 2f), new Vector3(0.06f, 0.24f, L), trim, false);
            }
            Prim(PrimitiveType.Cube, "Ceiling", g, new Vector3(0f, H + 0.15f, L / 2f), new Vector3(W + 0.6f, 0.3f, L), trim, false);
            Prim(PrimitiveType.Cube, "End", g, new Vector3(0f, H / 2f, L), new Vector3(W, H, 0.3f), wall, false); // 突き当たり (霧の向こうで見えない)
            // 一定の間隔の金のアーチと天井の明かり、床の模様 (遠くへ並んで小さくなり、霧にかすんでいく)
            for (float z = 4f; z < L; z += 6f)
            {
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Pilaster", g, new Vector3(s * (W / 2f - 0.12f), H / 2f, z), new Vector3(0.25f, H, 0.3f), trim, false);
                Prim(PrimitiveType.Cube, "Arch", g, new Vector3(0f, H - 0.18f, z), new Vector3(W, 0.36f, 0.3f), gold, false);
                Prim(PrimitiveType.Cube, "Lamp", g, new Vector3(0f, H - 0.05f, z + 3f), new Vector3(0.6f, 0.06f, 0.6f), lamp, false);
                Prim(PrimitiveType.Cube, "FloorBand", g, new Vector3(0f, 0.0475f, z), new Vector3(W, 0.005f, 0.5f), tile, false); // 上の面 0.05 (じゅうたんの下)
            }
            // 両側の壁の名画 (左右交互)
            int design = panel;
            int side = 0;
            for (float z = 7f; z < L - 3f; z += 6f)
            {
                float s = side++ % 2 == 0 ? -1f : 1f;
                var pos = dir * inner + rot * new Vector3(s * (W / 2f - 0.02f), 2.6f, z);
                DecorPainting(hall, pos, rot * Quaternion.Euler(0f, s * 90f, 0f), 1.8f, 1.3f, design++);
            }
        }

        /// <summary>飾りの名画 (金の額と、描き上がった絵。当たり判定なし)。rot = 壁の向き (外向き)</summary>
        void DecorPainting(Transform parent, Vector3 pos, Quaternion rot, float w, float h, int design)
        {
            var p = Group(parent, "DecorPainting");
            p.localPosition = pos;
            p.localRotation = rot;
            PictureFrame(p, w, h, 0f);
            Prim(PrimitiveType.Cube, "Canvas", p, new Vector3(0f, 0f, -0.01f), new Vector3(w, h, 0.02f), Mat("Canvas_White", new Color(0.97f, 0.96f, 0.93f), null, false, 0.1f, matte: true), false);
            var pic = Prim(PrimitiveType.Quad, "Painting", p, new Vector3(0f, 0f, -0.022f), new Vector3(w, h, 1f), PaintingFullMat((design + 2) % CanvasDesigns), false);
            pic.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// キャンバスの段ごとの額縁 (0: 細い白木 / 1: 木と黒い内縁 / 2: 銀と黒い内縁 / 3: 太い金に、外の縁取り・四隅の飾り・上の飾り)。
        /// 段 0 だけ出しておき、残りは隠す (Artwork が段に合わせて入れ替える)
        /// </summary>
        GameObject[] CanvasFrames(Transform parent, float w, float h)
        {
            var pine = Mat("Frame_Pine", new Color(0.72f, 0.6f, 0.42f), null, false, 0.15f, matte: true);
            var walnut = Mat("Frame_Walnut", new Color(0.32f, 0.18f, 0.09f), null, false, 0.45f);
            var silver = Mat("Frame_Silver", new Color(0.6f, 0.62f, 0.67f), null, false, 0.85f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var liner = Mat("Frame_Liner", new Color(0.1f, 0.08f, 0.06f));
            var tiers = new[]
            {
                FrameTier(parent, "Frame0_Pine", w, h, 0.035f, 0.02f, pine, null, null),
                FrameTier(parent, "Frame1_Wood", w, h, 0.07f, 0.04f, walnut, liner, null),
                FrameTier(parent, "Frame2_Silver", w, h, 0.1f, 0.05f, silver, liner, null),
                FrameTier(parent, "Frame3_Gold", w, h, 0.17f, 0.08f, gold, walnut, gold),
            };
            for (int i = 1; i < tiers.Length; i++) tiers[i].SetActive(false);
            return tiers;
        }

        // 額縁1つ: 太さ F・厚み depth の4本の枠。liner があれば内側に細い縁、ornament があれば外の縁取り・四隅の飾り・上の飾り
        GameObject FrameTier(Transform parent, string name, float w, float h, float F, float depth, Material mat, Material liner, Material ornament)
        {
            var g = Group(parent, name);
            float z = -depth / 2f;
            void Bars(float inset, float width, float zz, float d, Material m, string part)
            {
                float ow = w + inset * 2f + width * 2f, oh = h + inset * 2f;
                Prim(PrimitiveType.Cube, part, g, new Vector3(0f, oh / 2f + width / 2f, zz), new Vector3(ow, width, d), m, false);
                Prim(PrimitiveType.Cube, part, g, new Vector3(0f, -oh / 2f - width / 2f, zz), new Vector3(ow, width, d), m, false);
                Prim(PrimitiveType.Cube, part, g, new Vector3(-ow / 2f + width / 2f, 0f, zz), new Vector3(width, oh, d), m, false);
                Prim(PrimitiveType.Cube, part, g, new Vector3(ow / 2f - width / 2f, 0f, zz), new Vector3(width, oh, d), m, false);
            }
            Bars(0f, F, z, depth, mat, "Frame");
            if (liner != null) Bars(-F * 0.22f, F * 0.22f, -0.018f, 0.01f, liner, "Liner"); // 絵のふちに重ねる細い内縁
            if (ornament != null)
            {
                Bars(F * 0.75f, F * 0.25f, z - depth * 0.35f, depth * 0.5f, ornament, "Molding"); // 外側の盛り上がった縁取り
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sy in new[] { -1f, 1f })
                        Prim(PrimitiveType.Sphere, "Rosette", g, new Vector3(sx * (w / 2f + F / 2f), sy * (h / 2f + F / 2f), z - depth * 0.5f), new Vector3(F * 1.3f, F * 1.3f, depth), ornament, false);
                Prim(PrimitiveType.Sphere, "Crest", g, new Vector3(0f, h / 2f + F * 1.1f, z - depth * 0.3f), new Vector3(F * 3.2f, F * 1.8f, depth), ornament, false);
                foreach (float sx in new[] { -1f, 1f })
                    Prim(PrimitiveType.Sphere, "CrestLeaf", g, new Vector3(sx * F * 2.2f, h / 2f + F * 0.9f, z - depth * 0.3f), new Vector3(F * 2f, F * 1f, depth), ornament, false);
            }
            return g.gameObject;
        }

        // 金の額 (幅 w・高さ h の絵のまわり)。表は -Z
        void PictureFrame(Transform parent, float w, float h, float z)
        {
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            const float F = 0.09f;
            Prim(PrimitiveType.Cube, "FrameTop", parent, new Vector3(0f, h / 2f + F / 2f, z - 0.02f), new Vector3(w + F * 2f, F, 0.06f), gold, false);
            Prim(PrimitiveType.Cube, "FrameBottom", parent, new Vector3(0f, -h / 2f - F / 2f, z - 0.02f), new Vector3(w + F * 2f, F, 0.06f), gold, false);
            Prim(PrimitiveType.Cube, "FrameLeft", parent, new Vector3(-w / 2f - F / 2f, 0f, z - 0.02f), new Vector3(F, h, 0.06f), gold, false);
            Prim(PrimitiveType.Cube, "FrameRight", parent, new Vector3(w / 2f + F / 2f, 0f, z - 0.02f), new Vector3(F, h, 0.06f), gold, false);
        }

        // ------------------------------------------------------------------
        // 的: キャンバス
        // ------------------------------------------------------------------

        /// <summary>
        /// 未完成のキャンバス (的)。真っ白なキャンバスに、当たるたびに絵 (ビットマップ) が1段ずつ重ねて塗られていく (3回で完成。下塗り → 形 → 細かい仕上げ)。
        /// 壁に掛ける (金の額付き) か、イーゼルに立てかける。rot = 外向き (表の -Z がプレイヤーの方)
        /// </summary>
        Artwork BuildCanvasWork(Transform parent, string name, Vector3 pos, Quaternion rot, float w, float h, int design, bool onEasel, Material chip, Material sparkle)
        {
            var root = Group(parent, name);
            root.localPosition = pos;
            root.localRotation = rot;
            var body = Group(root, "Body");
            const float EaselY = 1.35f;
            if (onEasel)
            {
                // 脚と横木はキャンバスの後ろ (キャンバスに食い込まない)
                var wood = Mat("Easel_Wood", new Color(0.55f, 0.36f, 0.2f), null, false, 0.3f);
                float top = EaselY + h / 2f + 0.15f;
                foreach (float s in new[] { -1f, 1f })
                    DomeLine(root, "EaselLeg", new Vector3(s * 0.5f, 0f, 0.2f), new Vector3(s * 0.05f, top, 0.12f), 0.04f, wood);
                DomeLine(root, "EaselLegBack", new Vector3(0f, 0f, 0.75f), new Vector3(0f, top - 0.12f, 0.14f), 0.04f, wood);
                Prim(PrimitiveType.Cube, "EaselLedge", root, new Vector3(0f, EaselY - h / 2f - 0.19f, 0.02f), new Vector3(w + 0.1f, 0.05f, 0.14f), wood, false);
                body.localPosition = new Vector3(0f, EaselY - h / 2f, 0.06f); // 揺れる時は下の辺を軸に
                body.localRotation = Quaternion.Euler(-6f, 0f, 0f);         // 少し後ろへ立てかける
            }
            else body.localPosition = new Vector3(0f, -h / 2f, 0f);
            var canvasRoot = Group(body, "CanvasRoot");
            canvasRoot.localPosition = new Vector3(0f, h / 2f, 0f);
            var frames = CanvasFrames(canvasRoot, w, h); // 段ごとの額縁 (安っぽい白木 → 木 → 銀 → 豪華な金)

            // 段 0: 真っ白なキャンバス (当たり判定はこれ。いつも一番後ろに出しておく)。段 1〜3: 絵 (表のすぐ手前に、段ごとに少しずつ手前へ重ねる)
            var blank = Prim(PrimitiveType.Cube, "BlankCanvas", canvasRoot, new Vector3(0f, 0f, 0.005f), new Vector3(w, h, 0.03f),
                             Mat("Canvas_Blank", new Color(0.97f, 0.96f, 0.93f), new Color(0.2f, 0.2f, 0.19f), false, 0.1f, matte: true));
            var stageMats = PaintingStageMats(design);
            var stages = new List<GameObject> { blank };
            for (int s = 0; s < 3; s++)
            {
                var layer = Prim(PrimitiveType.Quad, $"Paint{s + 1}", canvasRoot, new Vector3(0f, 0f, -0.012f - 0.002f * s), new Vector3(w, h, 1f), stageMats[s], false);
                layer.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                layer.SetActive(false);
                stages.Add(layer);
            }

            // 作品の下の札 (次の1発の点数 / 完成したら題名)
            var plate = Group(root, "Plaque");
            plate.localPosition = new Vector3(0f, onEasel ? 0.5f : -h / 2f - 0.3f, onEasel ? -0.35f : -0.02f);
            Prim(PrimitiveType.Cube, "PlaqueBoard", plate, Vector3.zero, new Vector3(0.8f, 0.2f, 0.02f), Mat("Museum_Plaque", new Color(0.1f, 0.09f, 0.08f), null, false, 0.6f), false);
            var label = MakeText(plate, "PlaqueText", "100", new Vector3(0f, 0f, -0.015f), 0.13f, new Color(0.95f, 0.88f, 0.7f));

            // 塗っている時に絵の上を動く筆 (作品の子。ふだんは隠す)
            var tool = BuildBrush(root, "Brush", 1.8f);

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Artwork;
            target.manualControl = true;
            target.stayUpOnHit = true;
            target.shrinkOnHit = true; // 出てくる時は、その場でふくらむ (倒れて起き上がらない)
            target.hitWobbleDeg = 5f;
            target.multiHitInterval = 0.3f;
            target.visual = body;
            target.SetAvailable(false);

            var art = root.gameObject.AddComponent<Artwork>();
            art.kind = Artwork.Kind.Canvas;
            art.title = CanvasTitles[design % CanvasTitles.Length];
            art.target = target;
            art.stages = stages.ToArray();
            art.stagePoints = new[] { 100, 200, 300 };
            art.completeBonus = 500;
            art.plaque = label;
            art.frames = frames;
            art.tool = tool;
            art.chipMaterial = chip;
            art.sparkleMaterial = sparkle;
            AddArtField(root, Artwork.Kind.Canvas, body.localPosition + body.localRotation * canvasRoot.localPosition, Mathf.Max(w, h) * 0.5f + 0.25f); // (絵のすぐまわりだけ。広いと、近くを飛ぶたびに形が変わってちかちかする)
            AddGlint(art, canvasRoot, new Vector3(0f, 0f, -0.03f), new Vector2(w, h), false, 0f); // 絵の面の上を流れる光の帯
            root.gameObject.SetActive(false);
            return art;
        }

        // まだ当てられる作品の上を流れる光の帯 (袈裟懸けに左上から右下へ。完成すると出なくなる)。
        // anchor のローカルの center を真ん中に、幅・高さ size の四角形の中に流す。faceCamera なら、作品の手前 depth の所でプレイヤーの方を向ける
        void AddGlint(Artwork art, Transform anchor, Vector3 center, Vector2 size, bool faceCamera, float depth, Transform wrapSource = null)
        {
            var glint = art.gameObject.AddComponent<ArtGlint>();
            glint.art = art;
            glint.anchor = anchor;
            glint.center = center;
            glint.size = size;
            glint.faceCamera = faceCamera;
            glint.depth = depth;
            glint.material = TransparentMat("Art_Glint", new Color(1f, 1f, 1f, 0.55f));
            if (wrapSource != null) // (板ではなく、形の表面ぜんたいを包んで流す)
            {
                glint.wrapSource = wrapSource;
                glint.wrapMaterial = VertexColorMat("Art_GlintWrap", Color.white);
            }
        }

        // イマジネーションの玉が近づくと、筆 / トンカチに変わる範囲 (当たり判定とは別。物理は使わない)
        static void AddArtField(Transform root, Artwork.Kind kind, Vector3 center, float radius)
        {
            var field = root.gameObject.AddComponent<ArtField>();
            field.kind = kind;
            field.center = center;
            field.radius = radius;
        }

        // ------------------------------------------------------------------
        // 的: 彫刻
        // ------------------------------------------------------------------

        const int SculptureDesigns = 3;

        /// <summary>
        /// 未完成の彫刻 (的)。台の上の灰色の直方体が、当たるたびに 荒削り → 形 → 大理石の完成品 と変わる (3回で完成)。rot = 外向き
        /// </summary>
        Artwork BuildSculptureWork(Transform parent, string name, Vector3 pos, Quaternion rot, int design, Material chip, Material sparkle)
        {
            var root = Group(parent, name);
            root.localPosition = pos;
            root.localRotation = rot;
            root.localScale = Vector3.one * 1.35f; // 広い美術館でも見えるように、ひと回り大きく
            const float PlinthH = 0.95f;
            Prim(PrimitiveType.Cube, "Plinth", root, new Vector3(0f, PlinthH / 2f, 0f), new Vector3(0.75f, PlinthH, 0.75f), Mat("Museum_MarbleDark", new Color(0.32f, 0.26f, 0.24f), null, false, 0.7f), false);
            Prim(PrimitiveType.Cube, "PlinthTop", root, new Vector3(0f, PlinthH + 0.025f, 0f), new Vector3(0.82f, 0.05f, 0.82f), Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f)), false);
            var body = Group(root, "Body");
            body.localPosition = Vector3.up * (PlinthH + 0.05f);

            // 段 0 は真っ白な塊 (白いものが撃つべき的)。彫るほど、いろいろな色の部品でできた作品になっていく
            var stone = Mat("Sculpt_Stone", new Color(0.96f, 0.96f, 0.94f), new Color(0.2f, 0.2f, 0.19f), false, 0.1f, matte: true);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var stages = new GameObject[4];
            // 段 0: 灰色の直方体 (当たり判定はこれ。どの段でも同じ大きさの箱で当たる)
            var block = Prim(PrimitiveType.Cube, "Block", body, new Vector3(0f, 0.55f, 0f), new Vector3(0.55f, 1.1f, 0.5f), stone);
            stages[0] = Group(body, "Stage0").gameObject;
            block.transform.SetParent(stages[0].transform, true);
            var hit = Prim(PrimitiveType.Cube, "Hit", body, new Vector3(0f, 0.55f, 0f), new Vector3(0.6f, 1.15f, 0.55f), null);
            Object.DestroyImmediate(hit.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(hit.GetComponent<MeshFilter>());
            for (int s = 1; s <= 3; s++)
            {
                var g = Group(body, $"Stage{s}");
                SculptureShape(g, design, s - 1, SculpturePalette(s - 1), gold);
                g.gameObject.SetActive(false);
                stages[s] = g.gameObject;
            }
            // 完成品の足元の金の銘板
            var plate = Group(root, "Plaque");
            plate.localPosition = new Vector3(0f, PlinthH * 0.6f, -0.385f);
            Prim(PrimitiveType.Cube, "PlaqueBoard", plate, Vector3.zero, new Vector3(0.5f, 0.16f, 0.02f), Mat("Museum_Plaque", new Color(0.1f, 0.09f, 0.08f), null, false, 0.6f), false);
            var label = MakeText(plate, "PlaqueText", "100", new Vector3(0f, 0f, -0.015f), 0.11f, new Color(0.95f, 0.88f, 0.7f));

            var tool = BuildHammer(root, "Hammer");

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Artwork;
            target.manualControl = true;
            target.stayUpOnHit = true;
            target.shrinkOnHit = true;
            target.hitWobbleDeg = 4f;
            target.multiHitInterval = 0.3f;
            target.visual = body;
            target.SetAvailable(false);

            var art = root.gameObject.AddComponent<Artwork>();
            art.kind = Artwork.Kind.Sculpture;
            art.title = SculptureTitles[design % SculptureTitles.Length];
            art.target = target;
            art.stages = stages;
            art.stagePoints = new[] { 150, 250, 350 };
            art.completeBonus = 600;
            art.plaque = label;
            art.tool = tool;
            art.chipMaterial = chip;
            art.sparkleMaterial = sparkle;
            AddArtField(root, Artwork.Kind.Sculpture, new Vector3(0f, PlinthH + 0.6f, 0f), 0.8f); // (彫刻のすぐまわりだけ)
            AddGlint(art, body, new Vector3(0f, 0.55f, 0f), new Vector2(0.55f, 1.1f), true, 0.3f); // 白い塊の手前を流れる光の帯
            root.gameObject.SetActive(false);
            return art;
        }

        /// <summary>
        /// 彫刻の段ごとの色の組。level 0 (荒削り): 白っぽい石に、色が少しだけ / 1 (形): 素焼き・青緑・黄土・ばら色の部品 / 2 (完成): 赤・青・緑・紫・橙のあざやかな部品。
        /// shift = 色の順番をずらす (飾りの彫像を、的の完成品と少し違う色にする)
        /// </summary>
        Material[] SculpturePalette(int level, int shift = 0)
        {
            Material M(string name, Color c, float smooth) => Mat(name, c, null, false, smooth);
            Material[] pal = level switch
            {
                0 => new[] { M("Sculpt_RoughStone", new Color(0.82f, 0.8f, 0.76f), 0.15f), M("Sculpt_Terracotta", new Color(0.78f, 0.42f, 0.28f), 0.2f), M("Sculpt_RoughStone", new Color(0.82f, 0.8f, 0.76f), 0.15f) },
                1 => new[] { M("Sculpt_Terracotta", new Color(0.78f, 0.42f, 0.28f), 0.2f), M("Sculpt_Teal", new Color(0.2f, 0.58f, 0.58f), 0.3f), M("Sculpt_Ochre", new Color(0.85f, 0.65f, 0.25f), 0.3f), M("Sculpt_Rose", new Color(0.85f, 0.45f, 0.55f), 0.3f) },
                _ => new[] { M("Sculpt_Red", new Color(0.85f, 0.15f, 0.15f), 0.75f), M("Sculpt_Cobalt", new Color(0.15f, 0.3f, 0.85f), 0.75f), M("Sculpt_Emerald", new Color(0.1f, 0.65f, 0.4f), 0.75f),
                             M("Sculpt_Purple", new Color(0.55f, 0.25f, 0.8f), 0.75f), M("Sculpt_Orange", new Color(1f, 0.55f, 0.15f), 0.75f) },
            };
            if (shift == 0) return pal;
            var shifted = new Material[pal.Length];
            for (int i = 0; i < pal.Length; i++) shifted[i] = pal[(i + shift) % pal.Length];
            return shifted;
        }

        static readonly string[] SculptureTitles = { "THE BUST", "THE DANCER", "INFINITY" };

        /// <summary>
        /// 彫刻の形 (原点が台の上面の真ん中、高さはおよそ 1.1m)。level: 0 = 荒削り (角ばった塊) / 1 = 形 (丸みのある形) / 2 = 完成 (形 + 金の飾り)。
        /// design: 0 = 胸像 / 1 = 踊る人 / 2 = ねじれた抽象
        /// </summary>
        void SculptureShape(Transform g, int design, int level, Material[] palette, Material gold)
        {
            int part = 0; // 部品ごとに、色の組から順に色を変える
            void Box(Vector3 p, Vector3 s, float ry = 0f) => Prim(PrimitiveType.Cube, "Rough", g, p, s, palette[part++ % palette.Length], false).transform.localRotation = Quaternion.Euler(0f, ry, 0f);
            void Part(PrimitiveType t, Vector3 p, Vector3 s, Vector3 euler) => Prim(t, "Form", g, p, s, palette[part++ % palette.Length], false).transform.localRotation = Quaternion.Euler(euler);
            switch (design % SculptureDesigns)
            {
                case 0: // 胸像
                    if (level == 0)
                    {
                        Box(new Vector3(0f, 0.25f, 0f), new Vector3(0.55f, 0.45f, 0.42f));
                        Box(new Vector3(0f, 0.62f, 0f), new Vector3(0.2f, 0.25f, 0.22f));
                        Box(new Vector3(0f, 0.86f, 0f), new Vector3(0.32f, 0.38f, 0.34f));
                        break;
                    }
                    Part(PrimitiveType.Capsule, new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.3f, 0.38f), new Vector3(0f, 0f, 90f)); // 肩
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0.55f, 0f), new Vector3(0.14f, 0.12f, 0.14f), Vector3.zero);       // 首
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.82f, 0f), new Vector3(0.27f, 0.34f, 0.3f), Vector3.zero);          // 頭
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.92f, 0.04f), new Vector3(0.29f, 0.2f, 0.3f), Vector3.zero);        // 髪
                    Part(PrimitiveType.Cube, new Vector3(0f, 0.8f, -0.15f), new Vector3(0.04f, 0.08f, 0.05f), new Vector3(-10f, 0f, 0f)); // 鼻
                    if (level == 2)
                    {
                        var wreath = Group(g, "Laurel");
                        wreath.localPosition = new Vector3(0f, 0.9f, 0f);
                        wreath.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        RingSegments(wreath, "LaurelLeaf", 0.16f, 12, 0.03f, gold);
                    }
                    break;
                case 1: // 踊る人 (片手を上げて、片足で立つ)
                    if (level == 0)
                    {
                        Box(new Vector3(0f, 0.22f, 0f), new Vector3(0.25f, 0.44f, 0.2f));
                        Box(new Vector3(0f, 0.6f, 0f), new Vector3(0.32f, 0.36f, 0.22f));
                        Box(new Vector3(0.12f, 0.92f, 0f), new Vector3(0.4f, 0.3f, 0.2f));
                        break;
                    }
                    Part(PrimitiveType.Capsule, new Vector3(-0.04f, 0.22f, 0f), new Vector3(0.09f, 0.22f, 0.09f), new Vector3(0f, 0f, -4f));   // 立っている足
                    Part(PrimitiveType.Capsule, new Vector3(0.13f, 0.32f, 0.05f), new Vector3(0.08f, 0.2f, 0.08f), new Vector3(20f, 0f, 55f)); // 上げた足
                    Part(PrimitiveType.Capsule, new Vector3(0f, 0.62f, 0f), new Vector3(0.2f, 0.2f, 0.15f), Vector3.zero);                     // 胴
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.9f, 0f), new Vector3(0.15f, 0.17f, 0.15f), Vector3.zero);                      // 頭
                    Part(PrimitiveType.Capsule, new Vector3(0.14f, 0.9f, 0f), new Vector3(0.06f, 0.18f, 0.06f), new Vector3(0f, 0f, -25f));    // 上げた腕
                    Part(PrimitiveType.Capsule, new Vector3(-0.16f, 0.66f, 0f), new Vector3(0.06f, 0.16f, 0.06f), new Vector3(0f, 0f, 60f));   // 横へ伸ばした腕
                    if (level == 2) Prim(PrimitiveType.Sphere, "Star", g, new Vector3(0.22f, 1.08f, 0f), Vector3.one * 0.09f, gold, false);
                    break;
                default: // ねじれた抽象 (箱が少しずつ回りながら積み上がる)
                    int count = level == 0 ? 3 : 7;
                    for (int k = 0; k < count; k++)
                    {
                        float t = k / (float)(count - 1);
                        float size = Mathf.Lerp(0.5f, 0.24f, t);
                        float hh = 1.05f / count;
                        if (level == 0) Box(new Vector3(0f, hh * (k + 0.5f), 0f), new Vector3(size, hh, size), k * 15f);
                        else Part(PrimitiveType.Cube, new Vector3(0f, hh * (k + 0.5f), 0f), new Vector3(size, hh * 0.92f, size), new Vector3(0f, k * 18f, 0f));
                    }
                    if (level == 2) Prim(PrimitiveType.Sphere, "GoldOrb", g, new Vector3(0f, 1.18f, 0f), Vector3.one * 0.16f, gold, false);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // 絵筆の的の見た目
        // ------------------------------------------------------------------

        /// <summary>美術館の的: パレットと額縁入りの絵と大きな筆 (LookArt の絵のステッカー。はみ出して大胆に)</summary>
        GameObject MuseumLook(Transform look) => StickerLook(look, "Museum", "ART MUSEUM", new Color(1f, 0.9f, 0.6f));

        /// <summary>頂点の色が効く半透明の材質 (光の筋など。虹色の筋を TrailRenderer の色で付ける)</summary>
        Material VertexColorMat(string name, Color color)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return TransparentMat(name, color);
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }
    }
}
