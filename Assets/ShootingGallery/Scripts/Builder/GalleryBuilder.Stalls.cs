using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GalleryBuilder のうち、屋台 (共通の骨組みと、ギミックごとの中身) の生成。</summary>
    public partial class GalleryBuilder
    {
        // ------------------------------------------------------------------
        // 屋台 (共通の骨組み + ギミックごとの中身)
        // ------------------------------------------------------------------

        enum Gimmick { Classic, Conveyor, FerrisWheel, PopUp, Pendulum, Carousel, Elevator, DuckParade, Roulette, Turnstile }

        /// <summary>
        /// 屋台の外観 (見た目だけ・機能なし)。準備中の表示にも使う。
        /// active = false なら非表示のまま作る (フィーバー屋台)。radius を省略すると stallRadius。
        /// </summary>
        Transform BuildStallShell(string name, float angleDeg, Gimmick gimmick, Material cloth, bool active, float radius = -1f)
        {
            var t = Group(world, name);
            t.gameObject.SetActive(active);
            var rot = Quaternion.Euler(0f, angleDeg, 0f);
            t.localPosition = rot * new Vector3(0f, 0f, radius > 0f ? radius : stallRadius);
            t.localRotation = rot;

            string sign = gimmick switch
            {
                Gimmick.Classic => "SHOOTING GALLERY DX",
                Gimmick.Conveyor => "CONVEYOR",
                Gimmick.FerrisWheel => "FERRIS WHEEL",
                Gimmick.PopUp => "POP-UP PANIC",
                Gimmick.Pendulum => "PENDULUM",
                Gimmick.Carousel => "CAROUSEL",
                Gimmick.Elevator => "ELEVATOR",
                Gimmick.DuckParade => "DUCK PARADE",
                Gimmick.Roulette => "ROULETTE",
                _ => "TURNSTILE"
            };
            BuildStallFrame(t, cloth, sign);

            if (gimmick == Gimmick.Classic)
            {
                // ひな壇も外観に含めておくと、準備中でも完成形に近く見える
                for (int i = 0; i < TierHeights.Length; i++)
                    Prim(PrimitiveType.Cube, $"Tier_{i + 1}", t, new Vector3(0f, TierHeights[i] / 2f, TierZ[i]), new Vector3(3.0f, TierHeights[i], 0.25f), cloth);
            }
            return t;
        }

        /// <summary>屋台の中身 (的とギミック)。的は全部しまった状態で作る。</summary>
        Stall BuildStallContent(Transform t, Gimmick gimmick, Material cloth)
        {
            // 的は組み立て中は非アクティブにしておき、しまった状態にしてから有効化する
            var targetsRoot = Group(t, "Targets");
            targetsRoot.gameObject.SetActive(false);
            var targets = new List<Target>();
            switch (gimmick)
            {
                case Gimmick.Classic: BuildClassic(targetsRoot, targets); break;
                case Gimmick.Conveyor: BuildConveyor(t, targets); break;
                case Gimmick.FerrisWheel: BuildFerrisWheel(t, targets, cloth); break;
                case Gimmick.PopUp: BuildPopUp(t, targetsRoot, targets, cloth); break;
                case Gimmick.Pendulum: BuildPendulum(t, targets); break;
                case Gimmick.Carousel: BuildCarousel(t, targets, cloth); break;
                case Gimmick.Elevator: BuildElevator(t, targets, cloth); break;
                case Gimmick.DuckParade: BuildDuckParade(t, targets); break;
                case Gimmick.Roulette: BuildRoulette(t, targets, cloth); break;
                case Gimmick.Turnstile: BuildTurnstile(t, targets, cloth); break;
            }

            var stall = t.gameObject.AddComponent<Stall>();
            stall.targets = targets.ToArray();
            stall.SetTargetsAvailable(false); // 点数の的はゲームが始まるまで出さない (シーンに生成した時も隠しておく)
            targetsRoot.gameObject.SetActive(true);
            return stall;
        }

        /// <summary>カウンター・奥の壁・柱・屋根・幕・提灯・看板。どの屋台も共通。wide = 横幅の倍率 (マシンガンチャレンジの横長の屋台用)</summary>
        void BuildStallFrame(Transform t, Material cloth, string sign, float wide = 1f)
        {
            var wood = Mat("Wood", new Color(0.55f, 0.36f, 0.2f));
            var woodDark = Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f));
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));

            Prim(PrimitiveType.Cube, "Counter", t, new Vector3(0f, 0.5f, -1.2f), new Vector3(2.4f * wide, 1.0f, 0.4f), wood);
            Prim(PrimitiveType.Cube, "CounterTop", t, new Vector3(0f, 1.02f, -1.2f), new Vector3(2.5f * wide, 0.05f, 0.5f), woodDark);
            // カウンターの前に掛けた布 (屋台の色に、白い線を上下に)
            Prim(PrimitiveType.Cube, "CounterCloth", t, new Vector3(0f, 0.62f, -1.405f), new Vector3(2.4f * wide, 0.5f, 0.01f), cloth, false);
            foreach (float y in new[] { 0.83f, 0.41f })
                Prim(PrimitiveType.Cube, "CounterClothLine", t, new Vector3(0f, y, -1.412f), new Vector3(2.4f * wide, 0.025f, 0.005f), white, false);

            float px = 1.7f * wide; // 柱の位置
            Prim(PrimitiveType.Cube, "BackWall", t, new Vector3(0f, 1.4f, 0.8f), new Vector3(3.6f * wide, 2.8f, 0.08f), white);
            foreach (var p in new[] { new Vector3(-px, 0f, -0.9f), new Vector3(px, 0f, -0.9f), new Vector3(-px, 0f, 0.75f), new Vector3(px, 0f, 0.75f) })
            {
                Prim(PrimitiveType.Cylinder, "Pillar", t, p + Vector3.up * 1.4f, new Vector3(0.1f, 1.4f, 0.1f), woodDark);
                Prim(PrimitiveType.Cube, "PillarBase", t, p + Vector3.up * 0.06f, new Vector3(0.17f, 0.12f, 0.17f), wood, false);
            }
            // 屋根と看板は当たり判定なし (空の UFO を狙う弾をさえぎらないように)
            Prim(PrimitiveType.Cube, "Roof", t, new Vector3(0f, 2.8f, -0.05f), new Vector3(3.7f * wide, 0.08f, 1.8f), woodDark, false);

            // 幕: 縦じまの布の下のふちを、波形 (半円が並ぶ形) にする
            int awnings = Mathf.Max(2, Mathf.RoundToInt(8 * wide));
            float awningW = 3.6f * wide / awnings; // 幕は奥の壁と同じ幅に並べる (普通の屋台は 0.45m × 8枚)
            for (int i = 0; i < awnings; i++)
            {
                var mat = i % 2 == 0 ? cloth : white;
                float x = -1.8f * wide + (i + 0.5f) * awningW;
                RemoveCollider(Prim(PrimitiveType.Cube, $"Awning_{i}", t, new Vector3(x, 2.58f, -0.95f), new Vector3(awningW, 0.38f, 0.04f), mat));
                var scallop = Prim(PrimitiveType.Cylinder, $"AwningScallop_{i}", t, new Vector3(x, 2.39f, -0.95f), new Vector3(awningW, 0.015f, 0.24f), mat, false);
                scallop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            foreach (float x in new[] { -px, px })
                BuildLantern(t, new Vector3(x, 2.2f, -0.9f), 1.08f - 0.9f, woodDark);

            BuildSignBoard(t, sign, wide, cloth, wood, woodDark);
        }

        /// <summary>
        /// 赤い提灯 (蛇腹の胴と、上下の黒いふた・つる)。柱から前へ突き出した腕 (根元 armRoot) に吊るす。当たり判定なし
        /// </summary>
        void BuildLantern(Transform t, Vector3 armRoot, float reach, Material arm)
        {
            const float Diameter = 0.3f, Height = 0.32f;
            var black = Mat("Lantern_Cap", new Color(0.05f, 0.045f, 0.04f), null, false, 0.75f);
            var metal = Mat("Lantern_Wire", new Color(0.7f, 0.7f, 0.72f), null, false, 0.6f);

            // 柱から前へ出した腕と、そこから下がる吊り金具
            var hook = armRoot + Vector3.back * reach;
            Prim(PrimitiveType.Cube, "LanternArm", t, (armRoot + hook) / 2f, new Vector3(0.025f, 0.025f, reach + 0.02f), arm, false);

            float capH = 0.045f;
            var center = hook + Vector3.down * (0.03f + 0.05f + capH + Height / 2f); // 吊り金具 0.03 + つる 0.05 + ふた + 胴の半分
            float top = center.y + Height / 2f;
            float bottom = center.y - Height / 2f;

            MeshObj(t, "Lantern", LanternMesh(), center, LanternMat()).transform.localScale = new Vector3(Diameter, Height, Diameter);
            // 上のふた (大きめ) と下のふた。ふちに銀の線、正面に鋲
            Prim(PrimitiveType.Cylinder, "LanternCapTop", t, new Vector3(center.x, top + capH / 2f, center.z), new Vector3(0.16f, capH / 2f, 0.16f), black, false);
            Prim(PrimitiveType.Cylinder, "LanternCapTopRim", t, new Vector3(center.x, top + 0.004f, center.z), new Vector3(0.165f, 0.004f, 0.165f), metal, false);
            Prim(PrimitiveType.Sphere, "LanternRivet", t, new Vector3(center.x, top + capH / 2f, center.z - 0.08f), Vector3.one * 0.016f, metal, false);
            Prim(PrimitiveType.Cylinder, "LanternCapBottom", t, new Vector3(center.x, bottom - 0.0175f, center.z), new Vector3(0.14f, 0.0175f, 0.14f), black, false);
            Prim(PrimitiveType.Cylinder, "LanternCapBottomRim", t, new Vector3(center.x, bottom - 0.004f, center.z), new Vector3(0.145f, 0.004f, 0.145f), metal, false);

            // つる: ふたの左右から山形に上がって、吊り金具で1つになる
            float capTop = top + capH;
            var apex = new Vector3(center.x, capTop + 0.05f, center.z);
            foreach (float s in new[] { -1f, 1f })
            {
                var foot = new Vector3(center.x + s * 0.075f, capTop - 0.01f, center.z);
                var wire = Prim(PrimitiveType.Cube, "LanternBail", t, (foot + apex) / 2f, new Vector3(0.006f, Vector3.Distance(foot, apex), 0.006f), metal, false);
                wire.transform.localRotation = Quaternion.FromToRotation(Vector3.up, apex - foot);
            }
            Prim(PrimitiveType.Cube, "LanternHook", t, (apex + hook) / 2f, new Vector3(0.008f, hook.y - apex.y + 0.01f, 0.008f), metal, false);
        }

        /// <summary>
        /// 提灯の胴のメッシュ (直径 1・高さ 1。上下は口が開いていて、ふたでふさぐ)。
        /// 丸い胴に細かい溝を横に何本も入れて、竹ひごの蛇腹に見せる
        /// </summary>
        Mesh LanternMesh()
        {
            const string name = "Lantern_Ribbed";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int Ribs = 22, PerRib = 4, Segments = 28;
            const float EndRadius = 0.24f; // 上下の口の半径 (ふたより少し小さく)
            float k = 1f - (EndRadius / 0.5f) * (EndRadius / 0.5f);

            int rows = Ribs * PerRib + 1;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < rows; i++)
            {
                float y = -0.5f + (float)i / (rows - 1);
                float r = 0.5f * Mathf.Sqrt(1f - k * (2f * y) * (2f * y));
                // 1本の節の中で、境目 (溝) をへこませ、真ん中をふくらませる
                int phase = i % PerRib;
                r *= phase == 0 ? 0.94f : phase == 2 ? 1f : 0.985f;
                for (int j = 0; j < Segments; j++)
                {
                    float a = j * Mathf.PI * 2f / Segments;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
            }
            for (int i = 0; i < rows - 1; i++)
                for (int j = 0; j < Segments; j++)
                {
                    int a = i * Segments + j, b = i * Segments + (j + 1) % Segments;
                    int c = a + Segments, d = b + Segments;
                    tris.Add(a); tris.Add(c); tris.Add(b); // 外向きが表
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }

        /// <summary>
        /// 看板: 木の額縁に、細い金の線で縁取った黒い板。上には小さな切妻屋根、左右には屋台の色の丸い紋。屋根の上に2本の脚で立てる
        /// </summary>
        void BuildSignBoard(Transform t, string sign, float wide, Material cloth, Material wood, Material woodDark)
        {
            const float Y = 3.32f, Z = -0.9f, H = 0.56f; // 額縁の下に、屋根から立てた脚が少し見える高さ
            float w = 2.5f * wide;
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var panel = Mat("Sign", new Color(0.1f, 0.08f, 0.06f));

            // 脚 (屋根から看板の下まで)
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "SignPost", t, new Vector3(s * (w / 2f - 0.25f), (2.84f + Y) / 2f, Z), new Vector3(0.08f, Y - 2.84f, 0.06f), woodDark, false);

            // 額縁と板と金の縁取り
            Prim(PrimitiveType.Cube, "SignFrame", t, new Vector3(0f, Y, Z), new Vector3(w + 0.14f, H + 0.14f, 0.07f), woodDark, false);
            Prim(PrimitiveType.Cube, "SignBoard", t, new Vector3(0f, Y, Z - 0.04f), new Vector3(w, H, 0.02f), panel, false);
            float lz = Z - 0.053f, iw = w - 0.08f, ih = H - 0.08f;
            Prim(PrimitiveType.Cube, "SignLine_T", t, new Vector3(0f, Y + ih / 2f, lz), new Vector3(iw, 0.012f, 0.005f), gold, false);
            Prim(PrimitiveType.Cube, "SignLine_B", t, new Vector3(0f, Y - ih / 2f, lz), new Vector3(iw, 0.012f, 0.005f), gold, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "SignLine_S", t, new Vector3(s * iw / 2f, Y, lz), new Vector3(0.012f, ih, 0.005f), gold, false);
            MakeText(t, "SignText", sign, new Vector3(0f, Y, Z - 0.06f), 0.2f, new Color(1f, 0.85f, 0.35f));

            // 左右の丸い紋 (額縁の外へはみ出す飾り。屋台の色に金の輪)
            foreach (float s in new[] { -1f, 1f })
            {
                var c = new Vector3(s * (w / 2f + 0.2f), Y, Z - 0.05f);
                var ring = Prim(PrimitiveType.Cylinder, "SignCrestRing", t, c, new Vector3(0.36f, 0.012f, 0.36f), gold, false);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var crest = Prim(PrimitiveType.Cylinder, "SignCrest", t, c + Vector3.back * 0.008f, new Vector3(0.3f, 0.012f, 0.3f), cloth, false);
                crest.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Sphere, "SignCrestDot", t, c + Vector3.back * 0.02f, Vector3.one * 0.08f, gold, false);
            }

            // 小さな切妻屋根 (2枚の板が真ん中で合わさる) と、てっぺんの飾り
            float half = w / 2f + 0.4f, rise = 0.16f, roofY = Y + H / 2f + 0.09f; // 丸い紋の上まで覆う幅
            float len = Mathf.Sqrt(half * half + rise * rise) + 0.04f;
            float tilt = Mathf.Atan2(rise, half) * Mathf.Rad2Deg;
            foreach (float s in new[] { -1f, 1f })
            {
                var plank = Prim(PrimitiveType.Cube, "SignRoof", t, new Vector3(s * half / 2f, roofY + rise / 2f, Z), new Vector3(len, 0.05f, 0.24f), woodDark, false);
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, -s * tilt);
                var edge = Prim(PrimitiveType.Cube, "SignRoofEdge", t, new Vector3(s * half / 2f, roofY + rise / 2f - 0.03f, Z - 0.12f), new Vector3(len, 0.03f, 0.015f), cloth, false);
                edge.transform.localRotation = Quaternion.Euler(0f, 0f, -s * tilt);
            }
            Prim(PrimitiveType.Sphere, "SignRoofTop", t, new Vector3(0f, roofY + rise + 0.04f, Z), Vector3.one * 0.09f, gold, false);
            // 屋根と額縁の間の小さな束 (板を支える)
            Prim(PrimitiveType.Cube, "SignRoofStrut", t, new Vector3(0f, (Y + H / 2f + 0.07f + roofY + rise) / 2f, Z), new Vector3(0.05f, roofY + rise - (Y + H / 2f + 0.07f), 0.05f), wood, false);
        }

        // ---- メイン: ひな壇 (特殊な的はここにだけある) ----
        // (ひな壇は BuildStallShell で作成済み)
        void BuildClassic(Transform root, List<Target> targets)
        {
            Vector3 Slot(int tier, int s) => new Vector3(SlotX[s], TierHeights[tier], TierZ[tier]);

            // 下段: 10点
            for (int s = 0; s < 5; s++) targets.Add(PointTarget(root, 10, Slot(0, s)));

            // 中段: 10, 50, (左右に動く100), 50, 10
            targets.Add(PointTarget(root, 10, Slot(1, 0)));
            targets.Add(PointTarget(root, 50, Slot(1, 1)));
            var slider = PointTarget(root, 100, Slot(1, 2));
            slider.moving = true;
            slider.moveAmplitude = 0.28f;
            slider.moveSpeed = 2.5f;
            targets.Add(slider);
            targets.Add(PointTarget(root, 50, Slot(1, 3)));
            targets.Add(PointTarget(root, 10, Slot(1, 4)));

            // 上段: 50, 100, フィーバー(赤), 100, 50
            // (武器の的は WeaponDropper が全屋台にランダムで出す)
            targets.Add(PointTarget(root, 50, Slot(2, 0)));
            targets.Add(PointTarget(root, 100, Slot(2, 1)));
            targets.Add(FeverTarget(root, Slot(2, 2)));
            targets.Add(PointTarget(root, 100, Slot(2, 3)));
            targets.Add(PointTarget(root, 50, Slot(2, 4)));
        }

        // ---- ベルトコンベアー: 上下2本のベルトが逆向きに流れる ----
        void BuildConveyor(Transform t, List<Target> targets)
        {
            var beltMat = Mat("Belt", new Color(0.12f, 0.12f, 0.13f));
            var slatMat = Mat("BeltSlat", new Color(0.35f, 0.35f, 0.38f));
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            var tunnelMat = Mat("Tunnel", new Color(0.85f, 0.75f, 0.2f));

            BuildBelt(t, targets, "Belt_Lower", 0.95f, 0.05f, 0.5f, new[] { 100, 200, 100, 300, 100, 200 }, beltMat, slatMat, metal, tunnelMat);
            BuildBelt(t, targets, "Belt_Upper", 1.65f, 0.4f, -0.85f, new[] { 200, 300, 200, 500, 300, 500 }, beltMat, slatMat, metal, tunnelMat);
        }

        void BuildBelt(Transform t, List<Target> targets, string name, float y, float z, float speed, int[] values,
                       Material beltMat, Material slatMat, Material metal, Material tunnelMat)
        {
            const float BeltWidth = 3.0f;
            const float LoopLength = 3.4f; // 両端のトンネルの中で折り返す

            Prim(PrimitiveType.Cube, name + "_Body", t, new Vector3(0f, y - 0.04f, z), new Vector3(BeltWidth, 0.08f, 0.32f), beltMat);
            foreach (float x in new[] { -BeltWidth / 2f, BeltWidth / 2f })
            {
                var roller = Prim(PrimitiveType.Cylinder, name + "_Roller", t, new Vector3(x, y - 0.04f, z), new Vector3(0.1f, 0.17f, 0.1f), metal);
                roller.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cube, name + "_Leg", t, new Vector3(x * 0.9f, (y - 0.08f) / 2f, z), new Vector3(0.06f, y - 0.08f, 0.06f), metal);
                Prim(PrimitiveType.Cube, name + "_Tunnel", t, new Vector3(x, y + 0.27f, z), new Vector3(0.45f, 0.55f, 0.42f), tunnelMat);
            }

            var belt = Group(t, name);
            belt.localPosition = new Vector3(0f, y, z);
            var items = new List<Transform>();

            // 流れて見えるようにベルト表面の板も一緒に動かす
            for (int i = 0; i < 16; i++)
            {
                var slat = Prim(PrimitiveType.Cube, "Slat", belt, new Vector3(-LoopLength / 2f + i * LoopLength / 16f, 0.005f, 0f), new Vector3(0.03f, 0.01f, 0.3f), slatMat);
                RemoveCollider(slat);
                items.Add(slat.transform);
            }

            for (int i = 0; i < values.Length; i++)
            {
                var target = PointTarget(belt, values[i], new Vector3(-LoopLength / 2f + (i + 0.5f) * LoopLength / values.Length, 0.01f, 0f));
                targets.Add(target);
                items.Add(target.transform);
            }

            var conveyor = belt.gameObject.AddComponent<ConveyorBelt>();
            conveyor.items = items.ToArray();
            conveyor.length = LoopLength;
            conveyor.speed = speed;
        }

        // ---- 観覧車: 回る輪にぶら下がったゴンドラの上に的 ----
        void BuildFerrisWheel(Transform t, List<Target> targets, Material cloth)
        {
            const float Radius = 0.75f;
            var center = new Vector3(0f, 1.7f, 0.35f);
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));

            // 支柱 (ハの字)
            foreach (float side in new[] { -1f, 1f })
            {
                var leg = Prim(PrimitiveType.Cube, "WheelLeg", t, new Vector3(side * 0.35f, center.y / 2f, center.z + 0.12f), new Vector3(0.06f, center.y + 0.1f, 0.06f), metal);
                leg.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
            }

            var wheel = Group(t, "Wheel");
            wheel.localPosition = center;
            var rotator = wheel.gameObject.AddComponent<Rotator>();
            rotator.axis = Vector3.forward;
            rotator.speed = 22f;

            RemoveCollider(Prim(PrimitiveType.Cylinder, "Hub", wheel, Vector3.zero, new Vector3(0.16f, 0.06f, 0.16f), cloth)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                var spoke = Prim(PrimitiveType.Cube, "Spoke", wheel, Vector3.zero, new Vector3(Radius * 2f, 0.025f, 0.025f), white);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                RemoveCollider(spoke);
            }
            const int RimSegments = 24;
            for (int i = 0; i < RimSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RimSegments;
                var seg = Prim(PrimitiveType.Cube, "Rim", wheel, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius,
                               new Vector3(0.03f, 2f * Mathf.PI * Radius / RimSegments + 0.01f, 0.03f), i % 2 == 0 ? cloth : white);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                RemoveCollider(seg);
            }

            int[] values = { 200, 300, 500, 200, 500, 300 };
            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var gondola = Group(wheel, $"Gondola_{i}");
                gondola.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius;
                gondola.gameObject.AddComponent<KeepUpright>().reference = t;

                RemoveCollider(Prim(PrimitiveType.Cube, "Hanger", gondola, new Vector3(0f, -0.1f, 0f), new Vector3(0.015f, 0.2f, 0.015f), metal));
                Prim(PrimitiveType.Cube, "Floor", gondola, new Vector3(0f, -0.2f, 0f), new Vector3(0.24f, 0.03f, 0.14f), cloth);
                targets.Add(PointTarget(gondola, values[i], new Vector3(0f, -0.185f, 0f)));
            }
        }

        // ---- もぐら叩き: 穴からランダムに一瞬だけ顔を出す ----
        void BuildPopUp(Transform t, Transform root, List<Target> targets, Material cloth)
        {
            float[] rowHeights = { 0.95f, 1.15f, 1.35f };
            float[] rowZ = { -0.15f, 0.2f, 0.55f };
            float[] colX = { -1.05f, -0.35f, 0.35f, 1.05f };
            int[][] values =
            {
                new[] { 100, 200, 100, 200 },
                new[] { 200, 300, 200, 300 },
                new[] { 300, 500, 500, 300 },
            };
            var hole = Mat("Hole", new Color(0.05f, 0.04f, 0.04f));

            var popTargets = new List<Target>();
            for (int r = 0; r < rowHeights.Length; r++)
            {
                Prim(PrimitiveType.Cube, $"Step_{r + 1}", t, new Vector3(0f, rowHeights[r] / 2f, rowZ[r]), new Vector3(3.0f, rowHeights[r], 0.35f), cloth);
                for (int c = 0; c < colX.Length; c++)
                {
                    var pos = new Vector3(colX[c], rowHeights[r], rowZ[r]);
                    RemoveCollider(Prim(PrimitiveType.Cylinder, "Hole", t, pos + Vector3.up * 0.003f, new Vector3(0.3f, 0.003f, 0.3f), hole));
                    var target = PointTarget(root, values[r][c], pos);
                    target.manualControl = true;
                    popTargets.Add(target);
                }
            }
            targets.AddRange(popTargets);

            var popUp = root.gameObject.AddComponent<PopUpGimmick>();
            popUp.targets = popTargets.ToArray();
            popUp.interval = 0.4f;
            popUp.stayTime = 1.4f;
            popUp.maxUp = 5;
        }

        // ---- 振り子: 梁から吊るされた的が左右に揺れる ----
        void BuildPendulum(Transform t, List<Target> targets)
        {
            const float BeamY = 2.6f;
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            var rope = Mat("Rope", new Color(0.85f, 0.8f, 0.65f));
            Prim(PrimitiveType.Cube, "Beam", t, new Vector3(0f, BeamY, 0.2f), new Vector3(3.3f, 0.07f, 0.07f), metal);

            float[] xs = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };
            float[] lengths = { 0.85f, 1.1f, 0.7f, 1.1f, 0.85f };
            int[] values = { 200, 300, 500, 300, 200 };
            float[] speeds = { 1.8f, 1.5f, 2.6f, 1.6f, 2.0f };

            for (int i = 0; i < xs.Length; i++)
            {
                var pivot = Group(t, $"Pendulum_{i}");
                pivot.localPosition = new Vector3(xs[i], BeamY, 0.2f);
                var swing = pivot.gameObject.AddComponent<Pendulum>();
                swing.amplitude = 22f;
                swing.speed = speeds[i];
                swing.phase = i * 1.3f;

                RemoveCollider(Prim(PrimitiveType.Cube, "Rope", pivot, new Vector3(0f, -lengths[i] / 2f, 0f), new Vector3(0.012f, lengths[i], 0.012f), rope));
                // 的の円の上端がロープの先に来るように吊るす
                float d = StallTargetDiameter(values[i]);
                targets.Add(PointTarget(pivot, values[i], new Vector3(0f, -lengths[i] - d / 2f - TargetDiscY, 0f), stand: false)); // ロープから吊るすので棒なし
            }
        }

        // ---- 回転木馬: 2段のターンテーブルが逆向きに回る ----
        void BuildCarousel(Transform t, List<Target> targets, Material cloth)
        {
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var center = new Vector3(0f, 0f, 0.0f);

            Prim(PrimitiveType.Cylinder, "Base", t, center + Vector3.up * 0.45f, new Vector3(0.5f, 0.45f, 0.5f), white);
            Prim(PrimitiveType.Cylinder, "Pole", t, center + Vector3.up * 1.65f, new Vector3(0.06f, 0.75f, 0.06f), gold);
            RemoveCollider(Prim(PrimitiveType.Sphere, "Top", t, center + Vector3.up * 2.45f, Vector3.one * 0.18f, gold));

            BuildTurntable(t, targets, "Turntable_Lower", center + Vector3.up * 0.92f, 0.75f, 0.62f, 35f, new[] { 100, 200, 300, 200, 100, 500, 200, 300 }, cloth);
            BuildTurntable(t, targets, "Turntable_Upper", center + Vector3.up * 1.55f, 0.5f, 0.38f, -50f, new[] { 300, 500, 300, 500, 300 }, white);
        }

        void BuildTurntable(Transform t, List<Target> targets, string name, Vector3 pos, float discRadius, float targetRadius, float speed, int[] values, Material discMat)
        {
            var table = Group(t, name);
            table.localPosition = pos;
            var rotator = table.gameObject.AddComponent<Rotator>();
            rotator.axis = Vector3.up;
            rotator.speed = speed;

            Prim(PrimitiveType.Cylinder, "Disc", table, new Vector3(0f, -0.02f, 0f), new Vector3(discRadius * 2f, 0.02f, discRadius * 2f), discMat);
            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var target = PointTarget(table, values[i], dir * targetRadius);
                target.transform.localRotation = Quaternion.LookRotation(-dir); // 外側を向かせる
                targets.Add(target);
            }
        }

        // ---- エレベーター: 縦のレールに沿って、台に乗った的が上下する ----
        void BuildElevator(Transform t, List<Target> targets, Material cloth)
        {
            const float CenterY = 1.45f, Range = 0.5f, Z = 0.25f;
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            float[] xs = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };
            int[] values = { 200, 300, 500, 300, 200 };
            float[] speeds = { 1.2f, 1.7f, 2.6f, 1.5f, 1.1f };

            for (int i = 0; i < xs.Length; i++)
            {
                Prim(PrimitiveType.Cylinder, "Rail", t, new Vector3(xs[i], 1.6f, Z + 0.1f), new Vector3(0.03f, 0.85f, 0.03f), metal);

                var car = Group(t, $"Car_{i}");
                car.localPosition = new Vector3(xs[i], CenterY, Z);
                var osc = car.gameObject.AddComponent<Oscillator>();
                osc.offset = new Vector3(0f, Range, 0f);
                osc.speed = speeds[i];
                osc.phase = i * 1.1f;

                Prim(PrimitiveType.Cube, "Platform", car, new Vector3(0f, -0.02f, 0f), new Vector3(0.3f, 0.03f, 0.18f), cloth);
                targets.Add(PointTarget(car, values[i], Vector3.zero));
            }
        }

        // ---- アヒルの行進: 水路を、アヒルに乗った的が波打ちながら流れる ----
        void BuildDuckParade(Transform t, List<Target> targets)
        {
            var water = Mat("Water", new Color(0.15f, 0.45f, 0.85f), new Color(0.02f, 0.08f, 0.2f));
            var wood = Mat("Wood", new Color(0.55f, 0.36f, 0.2f));
            var tunnelMat = Mat("Tunnel", new Color(0.85f, 0.75f, 0.2f));

            BuildChannel(t, targets, "Channel_Lower", 0.9f, 0.0f, 0.45f, new[] { 100, 200, 100, 300, 100, 200, 100 }, water, wood, tunnelMat);
            BuildChannel(t, targets, "Channel_Upper", 1.5f, 0.4f, -0.7f, new[] { 200, 500, 300, 500, 200, 300 }, water, wood, tunnelMat);
        }

        void BuildChannel(Transform t, List<Target> targets, string name, float y, float z, float speed, int[] values,
                          Material water, Material wood, Material tunnelMat)
        {
            const float Width = 3.0f;
            const float LoopLength = 3.4f; // 両端のトンネルの中で折り返す
            var yellow = Mat("Duck", new Color(1f, 0.85f, 0.15f));
            var orange = Mat("DuckBeak", new Color(1f, 0.45f, 0.1f));

            Prim(PrimitiveType.Cube, name + "_Trough", t, new Vector3(0f, y / 2f, z), new Vector3(Width, y, 0.35f), wood);
            Prim(PrimitiveType.Cube, name + "_Water", t, new Vector3(0f, y + 0.005f, z), new Vector3(Width - 0.04f, 0.01f, 0.3f), water, false);
            foreach (float x in new[] { -Width / 2f, Width / 2f })
                Prim(PrimitiveType.Cube, name + "_Tunnel", t, new Vector3(x, y + 0.25f, z), new Vector3(0.45f, 0.5f, 0.42f), tunnelMat);

            var lane = Group(t, name);
            lane.localPosition = new Vector3(0f, y, z);
            var items = new List<Transform>();
            float facing = Mathf.Sign(speed); // アヒルのくちばしを進む向きへ
            for (int i = 0; i < values.Length; i++)
            {
                var target = PointTarget(lane, values[i], new Vector3(-LoopLength / 2f + (i + 0.5f) * LoopLength / values.Length, 0f, 0f));
                Prim(PrimitiveType.Sphere, "DuckBody", target.visual, new Vector3(0f, 0.05f, 0f), new Vector3(0.22f, 0.11f, 0.14f), yellow, false);
                Prim(PrimitiveType.Sphere, "DuckHead", target.visual, new Vector3(0.07f * facing, 0.13f, 0f), Vector3.one * 0.08f, yellow, false);
                Prim(PrimitiveType.Cube, "DuckBeak", target.visual, new Vector3(0.115f * facing, 0.125f, 0f), new Vector3(0.04f, 0.015f, 0.03f), orange, false);
                targets.Add(target);
                items.Add(target.transform);
            }

            var conveyor = lane.gameObject.AddComponent<ConveyorBelt>();
            conveyor.items = items.ToArray();
            conveyor.length = LoopLength;
            conveyor.speed = speed;
            conveyor.waveHeight = 0.06f;
            conveyor.waves = 3f;
        }

        // ---- ルーレット: 大きな円盤ごと的が回る (観覧車と違って的も一緒に傾く) ----
        void BuildRoulette(Transform t, List<Target> targets, Material cloth)
        {
            const float Radius = 0.9f;
            var center = new Vector3(0f, 1.7f, 0.5f);
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));

            Prim(PrimitiveType.Cube, "Post", t, new Vector3(0f, center.y / 2f, center.z + 0.1f), new Vector3(0.12f, center.y, 0.08f), metal);
            // 上の矢印 (回らない)
            var pointer = Prim(PrimitiveType.Cube, "Pointer", t, new Vector3(0f, center.y + Radius + 0.04f, center.z - 0.06f), new Vector3(0.1f, 0.1f, 0.03f),
                               Mat("Explosive_Band", new Color(0.9f, 0.1f, 0.05f), new Color(0.8f, 0.05f, 0f)), false);
            pointer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var wheel = Group(t, "Wheel");
            wheel.localPosition = center;
            var rotator = wheel.gameObject.AddComponent<Rotator>();
            rotator.axis = Vector3.forward;
            rotator.speed = 40f;

            var disc = Prim(PrimitiveType.Cylinder, "Disc", wheel, new Vector3(0f, 0f, 0.03f), new Vector3(Radius * 2f, 0.02f, Radius * 2f), white);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int k = 0; k < 8; k++) // 放射状の縞模様 (中心で重なる板は少しずつ手前にずらして、ちらつかないようにする)
            {
                var stripe = Prim(PrimitiveType.Cube, "Stripe", wheel, new Vector3(0f, 0f, 0.005f - k * 0.003f), new Vector3(Radius * 2f - 0.05f, 0.12f, 0.01f), k % 2 == 0 ? cloth : gold, false);
                stripe.transform.localRotation = Quaternion.Euler(0f, 0f, k * 22.5f);
            }
            const int RimSegments = 24;
            for (int i = 0; i < RimSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RimSegments;
                // となりの節と重なる端がちらつかないよう、1つおきに少し手前へずらす
                var seg = Prim(PrimitiveType.Cube, "Rim", wheel, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius + Vector3.back * (i % 2 * 0.003f),
                               new Vector3(0.04f, 2f * Mathf.PI * Radius / RimSegments + 0.01f, 0.06f), gold, false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }

            // 的: 円の中心が円盤上のその位置に来るように、根元を TargetDiscY だけ下げて置く (縞模様より手前)
            int[] values = { 200, 300, 500, 200, 500, 300 };
            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var pos = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.62f;
                targets.Add(WallTarget(wheel, values[i], pos + new Vector3(0f, -TargetDiscY, -0.04f)));
            }
            targets.Add(WallTarget(wheel, 500, new Vector3(0f, -TargetDiscY, -0.04f))); // 真ん中は500点
        }

        // ---- 回転扉: くるくる回る板の表と裏に的。板が横を向いた瞬間は当てにくい ----
        void BuildTurnstile(Transform t, List<Target> targets, Material cloth)
        {
            const float Z = 0.2f;
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            float[] xs = { -1.1f, -0.37f, 0.37f, 1.1f };
            float[] ys = { 1.25f, 1.75f, 1.25f, 1.75f };
            float[] speeds = { 70f, -90f, 110f, -60f };
            int[][] values = { new[] { 100, 300 }, new[] { 200, 500 }, new[] { 300, 500 }, new[] { 200, 500 } };

            for (int i = 0; i < xs.Length; i++)
            {
                float axleHeight = ys[i] - 0.25f;
                Prim(PrimitiveType.Cylinder, "Axle", t, new Vector3(xs[i], axleHeight / 2f, Z), new Vector3(0.03f, axleHeight / 2f, 0.03f), metal);

                var panel = Group(t, $"Panel_{i}");
                panel.localPosition = new Vector3(xs[i], ys[i], Z);
                var rotator = panel.gameObject.AddComponent<Rotator>();
                rotator.axis = Vector3.up;
                rotator.speed = speeds[i];

                Prim(PrimitiveType.Cube, "Board", panel, Vector3.zero, new Vector3(0.5f, 0.5f, 0.03f), cloth);
                targets.Add(WallTarget(panel, values[i][0], new Vector3(0f, -TargetDiscY, -0.02f)));     // 表
                var back = WallTarget(panel, values[i][1], new Vector3(0f, -TargetDiscY, 0.02f));       // 裏
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                targets.Add(back);
            }
        }
    }
}
