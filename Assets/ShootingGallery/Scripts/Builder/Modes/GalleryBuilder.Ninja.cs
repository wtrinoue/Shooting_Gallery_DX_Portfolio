using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 忍者モード (手裏剣の的を撃つと始まる) の生成: 忍者屋敷の一室 (起き上がる4枚の壁・降りてくる天井・畳)、
    /// 正面の一面のふすまと、その奥の奥行きのある奥座敷 (畳の下・左右のふすまの奥・天井裏から忍者のパネルが出る)、手裏剣の的の見た目。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float DojoR = 3.4f;      // 中心から壁の内側までの距離
        const float DojoH = 3.5f;      // 天井の高さ
        const float DoorTop = 2.6f;    // ふすま (鴨居) の高さ
        const float InnerDepth = 26f;  // 奥座敷の奥行き
        const float DepthK = InnerDepth / 20f; // (忍者や行灯の場所は、奥行き 20m の時に決めたものを、この倍率で奥へ広げる)

        NinjaDojo BuildNinjaDojo()
        {
            var go = new GameObject("NinjaDojo");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            // ---- 4枚の壁 (正面 = ふすま、右・後ろ・左 = ただの壁) ----
            var wallsGroup = Group(go.transform, "Walls");
            var walls = new Transform[4];
            Transform leftDoors = null, rightDoors = null;
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0f, i * 90f, 0f);
                var pivot = Group(wallsGroup, $"Wall_{i}");
                pivot.localPosition = rot * new Vector3(0f, 0f, DojoR);
                pivot.localRotation = rot; // ローカル +Z が外向き (表の -Z がプレイヤー向き)
                if (i == 0) (leftDoors, rightDoors) = BuildFrontWall(pivot);
                else BuildPlainWall(pivot, i);
                walls[i] = pivot;
            }

            var ceiling = BuildDojoCeiling(go.transform);
            var tatami = BuildTatami(go.transform);
            var panels = new List<Target>();
            var inner = BuildInnerRoom(go.transform, panels);

            var dojo = go.AddComponent<NinjaDojo>();
            dojo.walls = walls;
            dojo.ceiling = ceiling;
            dojo.showDuringStage = new[] { tatami };
            dojo.leftDoors = leftDoors;
            dojo.rightDoors = rightDoors;
            dojo.doorSlide = DojoR; // 左右の壁の向こうへ滑り込む
            dojo.innerRoom = inner;
            dojo.panels = panels.ToArray();
            // 部屋は忍者モードの時だけ出る (シーンに生成した時も隠しておき、Begin で表示する)
            foreach (var w in walls) w.gameObject.SetActive(false);
            ceiling.gameObject.SetActive(false);
            tatami.SetActive(false);
            inner.SetActive(false);
            go.SetActive(true);
            return dojo;
        }

        Material DojoWood => Mat("Dojo_Wood", new Color(0.36f, 0.22f, 0.12f), null, false, 0.35f);
        Material DojoWoodDark => Mat("Dojo_WoodDark", new Color(0.16f, 0.1f, 0.06f));
        Material DojoPlaster => Mat("Dojo_Plaster", new Color(0.72f, 0.85f, 0.42f)); // 黄緑の壁 (部屋の基調の色。忍者の紺・ワインレッド・濃いオレンジが目立つように)
        Material FusumaPaper => Mat("Fusuma_Paper", new Color(0.82f, 0.92f, 0.56f));  // 黄緑がかったふすま・掛け軸の紙

        /// <summary>
        /// 正面の壁 (ローカル: 根元の真ん中が原点、+Z が外)。両端の柱・鴨居・上の白い壁と「NINJA」の額、一面のふすま4枚。
        /// ふすまは左2枚 / 右2枚の組で、開く時は左右の壁の向こうへ滑り込む。
        /// </summary>
        (Transform left, Transform right) BuildFrontWall(Transform w)
        {
            float width = 2f * DojoR + 0.3f;
            Prim(PrimitiveType.Cube, "Lintel", w, new Vector3(0f, DoorTop + 0.06f, 0f), new Vector3(width, 0.12f, 0.16f), DojoWood);
            Prim(PrimitiveType.Cube, "UpperWall", w, new Vector3(0f, (DoorTop + 0.12f + DojoH) / 2f, 0.05f), new Vector3(width, DojoH - DoorTop - 0.12f, 0.1f), DojoPlaster);
            Prim(PrimitiveType.Cube, "TopBeam", w, new Vector3(0f, DojoH - 0.05f, 0f), new Vector3(width, 0.1f, 0.16f), DojoWoodDark, false);
            Prim(PrimitiveType.Cube, "Sill", w, new Vector3(0f, 0.045f, -0.06f), new Vector3(2f * DojoR, 0.012f, 0.12f), DojoWoodDark, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Post", w, new Vector3(s * (DojoR - 0.06f), DojoH / 2f, 0f), new Vector3(0.12f, DojoH, 0.16f), DojoWood);
            Prim(PrimitiveType.Cube, "SignBoard", w, new Vector3(0f, (DoorTop + DojoH) / 2f + 0.03f, -0.01f), new Vector3(1.6f, 0.42f, 0.04f), DojoWoodDark, false);
            MakeText(w, "Sign", "NINJA", new Vector3(0f, (DoorTop + DojoH) / 2f + 0.03f, -0.035f), 0.3f, new Color(0.95f, 0.8f, 0.4f));

            // ふすま 4枚 (1枚の幅 = 壁の幅の 1/4)
            float dw = 2f * DojoR / 4f, dh = DoorTop - 0.06f;
            var left = Group(w, "Fusuma_Left");
            var right = Group(w, "Fusuma_Right");
            for (int k = 0; k < 4; k++)
            {
                float x = -DojoR + dw / 2f + k * dw;
                Fusuma(k < 2 ? left : right, new Vector3(x, 0.05f + dh / 2f, -0.1f), dw, dh, k % 2 == 0);
            }
            return (left, right);
        }

        /// <summary>
        /// ただの壁 (右・後ろ・左)。腰板・白い壁・長押・柱・欄間。右の壁は刀掛け、後ろの壁は月明かりの障子窓、左の壁は掛け軸で飾る。
        /// </summary>
        void BuildPlainWall(Transform w, int index)
        {
            float width = 2f * DojoR + 0.3f; // 角で隣の壁と重なってすき間ができないように少し長く
            var dark = Mat("Ninja_Eye", new Color(0.02f, 0.02f, 0.02f));
            Prim(PrimitiveType.Cube, "Wainscot", w, new Vector3(0f, 0.45f, 0.05f), new Vector3(width, 0.9f, 0.1f), DojoWood);
            Prim(PrimitiveType.Cube, "WainscotRail", w, new Vector3(0f, 0.9f, -0.01f), new Vector3(width, 0.05f, 0.04f), DojoWoodDark, false);
            Prim(PrimitiveType.Cube, "Plaster", w, new Vector3(0f, (0.9f + DoorTop) / 2f, 0.06f), new Vector3(width, DoorTop - 0.9f, 0.08f), DojoPlaster);
            Prim(PrimitiveType.Cube, "Nageshi", w, new Vector3(0f, DoorTop + 0.06f, 0f), new Vector3(width, 0.12f, 0.14f), DojoWood);
            Prim(PrimitiveType.Cube, "UpperWall", w, new Vector3(0f, (DoorTop + 0.12f + DojoH) / 2f, 0.05f), new Vector3(width, DojoH - DoorTop - 0.12f, 0.1f), DojoPlaster);
            Prim(PrimitiveType.Cube, "TopBeam", w, new Vector3(0f, DojoH - 0.05f, 0f), new Vector3(width, 0.1f, 0.16f), DojoWoodDark, false);
            for (int k = 0; k <= 4; k++)
                Prim(PrimitiveType.Cube, "Post", w, new Vector3(-DojoR + 0.06f + k * (2f * DojoR - 0.12f) / 4f, DojoH / 2f, 0f), new Vector3(0.12f, DojoH, 0.16f), DojoWood);
            for (int k = -11; k <= 11; k++)
                Prim(PrimitiveType.Cube, "Ranma", w, new Vector3(k * 0.3f, (DoorTop + 0.12f + DojoH) / 2f, -0.005f), new Vector3(0.025f, DojoH - DoorTop - 0.32f, 0.01f), DojoWoodDark, false);

            switch (index)
            {
                case 1: // 刀掛け
                    Prim(PrimitiveType.Cube, "RackBoard", w, new Vector3(0f, 1.45f, 0.005f), new Vector3(1.2f, 0.7f, 0.02f), DojoWoodDark, false);
                    var steel = Mat("Shuriken_Steel", new Color(0.75f, 0.77f, 0.8f), null, false, 0.85f);
                    foreach (float y in new[] { 1.3f, 1.6f })
                    {
                        Prim(PrimitiveType.Cube, "Saya", w, new Vector3(0.1f, y, -0.03f), new Vector3(0.95f, 0.04f, 0.03f), dark, false);
                        Prim(PrimitiveType.Cube, "Tsuka", w, new Vector3(-0.5f, y, -0.03f), new Vector3(0.28f, 0.045f, 0.035f), Mat("Ninja_Sash_100", new Color(0.85f, 0.12f, 0.1f)), false);
                        Prim(PrimitiveType.Cube, "Tsuba", w, new Vector3(-0.35f, y, -0.03f), new Vector3(0.02f, 0.09f, 0.05f), steel, false);
                    }
                    break;
                case 2: // 月明かりの障子窓 (白く光る紙と格子)
                    var shoji = Mat("Shoji_Glow", new Color(0.92f, 0.94f, 1f), new Color(0.45f, 0.5f, 0.65f));
                    Prim(PrimitiveType.Cube, "ShojiPaper", w, new Vector3(0f, 1.6f, 0.005f), new Vector3(2.4f, 1.1f, 0.02f), shoji, false);
                    for (int k = -4; k <= 4; k++)
                        Prim(PrimitiveType.Cube, "ShojiV", w, new Vector3(k * 0.3f, 1.6f, -0.01f), new Vector3(0.025f, 1.1f, 0.012f), DojoWoodDark, false);
                    for (int k = -1; k <= 1; k++)
                        Prim(PrimitiveType.Cube, "ShojiH", w, new Vector3(0f, 1.6f + k * 0.36f, -0.01f), new Vector3(2.4f, 0.025f, 0.012f), DojoWoodDark, false);
                    break;
                default: // 掛け軸 (墨の丸)
                    Prim(PrimitiveType.Cube, "Scroll", w, new Vector3(0f, 1.55f, -0.005f), new Vector3(0.6f, 1.4f, 0.01f), FusumaPaper, false);
                    foreach (float y in new[] { 0.83f, 2.27f })
                        Prim(PrimitiveType.Cube, "Roller", w, new Vector3(0f, y, -0.01f), new Vector3(0.68f, 0.04f, 0.03f), DojoWoodDark, false);
                    var ring = Group(w, "Enso");
                    ring.localPosition = new Vector3(0f, 1.65f, -0.012f);
                    RingSegments(ring, "Ink", 0.18f, 18, 0.035f, dark);
                    break;
            }
        }

        /// <summary>ふすま1枚: クリーム色の紙に黒い縁、引き手。金の月か青い波の絵</summary>
        Transform Fusuma(Transform parent, Vector3 pos, float width, float height, bool moon)
        {
            var paper = FusumaPaper;
            var frame = Mat("Fusuma_Frame", new Color(0.12f, 0.08f, 0.06f));
            var door = Group(parent, "Fusuma");
            door.localPosition = pos;
            Prim(PrimitiveType.Cube, "Paper", door, Vector3.zero, new Vector3(width, height, 0.02f), paper);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "FrameV", door, new Vector3(s * (width / 2f - 0.02f), 0f, -0.012f), new Vector3(0.04f, height, 0.008f), frame, false);
                Prim(PrimitiveType.Cube, "FrameH", door, new Vector3(0f, s * (height / 2f - 0.02f), -0.012f), new Vector3(width, 0.04f, 0.008f), frame, false);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                var pull = Prim(PrimitiveType.Cylinder, "Pull", door, new Vector3(s * (width / 2f - 0.12f), -0.05f, -0.012f), new Vector3(0.07f, 0.004f, 0.07f), frame, false);
                pull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            if (moon)
            {
                var m = Prim(PrimitiveType.Cylinder, "Moon", door, new Vector3(0.15f, height * 0.22f, -0.012f), new Vector3(0.6f, 0.003f, 0.6f),
                             Mat("Fusuma_Gold", new Color(0.95f, 0.75f, 0.3f), new Color(0.25f, 0.18f, 0.03f)), false);
                m.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var wave = Mat("Fusuma_Wave", new Color(0.2f, 0.3f, 0.6f));
            for (int i = 0; i < 4; i++)
            {
                var line = Prim(PrimitiveType.Cube, "Wave", door, new Vector3(0f, -height * 0.32f + i * 0.1f, -0.012f), new Vector3(width - 0.16f, 0.03f, 0.004f), wave, false);
                line.transform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 3f : -3f);
            }
            return door;
        }

        // ------------------------------------------------------------------
        // 奥座敷 (ふすまの奥)
        // ------------------------------------------------------------------

        const int TatamiCols = 4;        // 横に並ぶ畳の数 (畳の長い方は横向き)
        const float TatamiDepth = 0.9f;  // 畳1枚の奥行き
        const int SideBays = 8;          // 左右の壁のふすまの区画の数

        /// <summary>
        /// ふすまの奥の奥座敷 (ローカルは部屋と同じ。+Z が奥)。1枚ずつ敷いた畳・左右の壁のふすま・柱・天井・途中の2つの欄間の仕切り・
        /// 両側の行灯・一番奥の金の屏風絵。忍者は 畳を跳ね上げて床下から / 左右のふすまを開けて壁の奥から / 天井板を開けて天井裏から
        /// 出てくる (奥ほど高い点数)。
        /// </summary>
        GameObject BuildInnerRoom(Transform parent, List<Target> panels)
        {
            var room = Group(parent, "InnerRoom");
            float z0 = DojoR + 0.1f, zEnd = z0 + InnerDepth, midZ = (z0 + zEnd) / 2f;
            var gold = Mat("Fusuma_Gold", new Color(0.95f, 0.75f, 0.3f), new Color(0.25f, 0.18f, 0.03f));
            var closet = Mat("Dojo_Closet", new Color(0.03f, 0.03f, 0.035f));

            // ---- 床: 暗い床下の上に、畳を1枚ずつ敷く (忍者が出てくる畳は跳ね上がる仕掛け) ----
            Prim(PrimitiveType.Cube, "Underfloor", room, new Vector3(0f, 0.015f, midZ), new Vector3(2f * DojoR, 0.03f, InnerDepth), closet, false);
            float tw = 2f * DojoR / TatamiCols;
            int rows = Mathf.FloorToInt(InnerDepth / TatamiDepth);
            var tatamiTraps = new (int col, int row, int points)[] { (1, 3, 100), (2, 6, 100), (0, 9, 200), (3, 12, 300), (1, 15, 300), (2, 18, 500), (1, 21, 1000) };
            for (int i = 0; i < tatamiTraps.Length; i++) tatamiTraps[i].row = Mathf.RoundToInt(tatamiTraps[i].row * DepthK);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < TatamiCols; c++)
                {
                    bool trap = false;
                    foreach (var t in tatamiTraps) if (t.col == c && t.row == r) trap = true;
                    if (!trap) TatamiMat(room, TatamiCell(c, r, z0, tw), tw);
                }
            foreach (var (c, r, points) in tatamiTraps)
            {
                var cell = TatamiCell(c, r, z0, tw);
                if (r >= rows) continue;
                // 奥の縁を軸に、手前が持ち上がって忍者の後ろに立つ
                var hinge = Group(room, "TatamiTrap");
                hinge.localPosition = cell + new Vector3(0f, 0f, TatamiDepth / 2f);
                TatamiMat(hinge, new Vector3(0f, 0f, -TatamiDepth / 2f), tw);
                var ninja = NinjaPanel(room, cell + Vector3.up * 0.07f, points);
                AddTrap(ninja, hinge, Vector3.zero, new Vector3(95f, 0f, 0f), new Vector3(0f, -1.8f, 0f)); // 床下からせり上がる
                panels.Add(ninja);
            }

            // ---- 左右の壁: 区画ごとにふすま2枚。忍者が出てくる区画は奥が暗い押し入れで、手前のふすまが横へ開く ----
            float bayL = InnerDepth / SideBays;
            foreach (float s in new[] { -1f, 1f })
            {
                var trapBays = s < 0f ? new[] { (1, 100), (3, 200), (6, 500) } : new[] { (2, 200), (4, 300), (7, 500) };
                Prim(PrimitiveType.Cube, "SideUpperWall", room, new Vector3(s * (DojoR + 0.05f), (DoorTop + DojoH) / 2f, midZ), new Vector3(0.1f, DojoH - DoorTop, InnerDepth), DojoPlaster);
                Prim(PrimitiveType.Cube, "SideNageshi", room, new Vector3(s * (DojoR - 0.02f), DoorTop + 0.06f, midZ), new Vector3(0.1f, 0.12f, InnerDepth), DojoWood, false);
                for (int k = 0; k <= SideBays; k++)
                    Prim(PrimitiveType.Cube, "SidePost", room, new Vector3(s * (DojoR - 0.05f), DojoH / 2f, z0 + k * bayL), new Vector3(0.14f, DojoH, 0.14f), DojoWood, false);

                for (int k = 0; k < SideBays; k++)
                {
                    float zc = z0 + (k + 0.5f) * bayL;
                    int trapPoints = 0;
                    foreach (var (bay, p) in trapBays) if (bay == k) trapPoints = p;

                    if (trapPoints == 0)
                        Prim(PrimitiveType.Cube, "SideWall", room, new Vector3(s * (DojoR + 0.05f), DoorTop / 2f, zc), new Vector3(0.1f, DoorTop, bayL), DojoPlaster);
                    else
                    {   // 押し入れ (中が暗い箱)
                        Prim(PrimitiveType.Cube, "ClosetBack", room, new Vector3(s * (DojoR + 0.85f), DoorTop / 2f, zc), new Vector3(0.04f, DoorTop, bayL), closet);
                        Prim(PrimitiveType.Cube, "ClosetTop", room, new Vector3(s * (DojoR + 0.45f), DoorTop, zc), new Vector3(0.8f, 0.04f, bayL), closet, false);
                        Prim(PrimitiveType.Cube, "ClosetFloor", room, new Vector3(s * (DojoR + 0.45f), 0.02f, zc), new Vector3(0.8f, 0.04f, bayL), closet, false);
                        foreach (float e in new[] { -1f, 1f })
                            Prim(PrimitiveType.Cube, "ClosetSide", room, new Vector3(s * (DojoR + 0.45f), DoorTop / 2f, zc + e * bayL / 2f), new Vector3(0.8f, DoorTop, 0.04f), closet, false);
                    }

                    // ふすま (区画のローカル: -Z が部屋の中、+X が区画の中のふすまの並び)
                    var bayRoot = Group(room, "SideFusuma");
                    bayRoot.localPosition = new Vector3(s * (DojoR - 0.01f), 0f, zc);
                    bayRoot.localRotation = Quaternion.Euler(0f, s * 90f, 0f);
                    float dw = (bayL - 0.16f) / 2f, dh = DoorTop - 0.06f;
                    // 忍者が出てくる区画: 0 枚目のふすまは手前のレールにあり、1 枚目の前へ滑って開く
                    var slider = Fusuma(bayRoot, new Vector3(-0.5f * dw, 0.05f + dh / 2f, trapPoints > 0 ? -0.03f : 0f), dw, dh, k % 2 == 0);
                    Fusuma(bayRoot, new Vector3(0.5f * dw, 0.05f + dh / 2f, 0f), dw, dh, k % 2 != 0);
                    if (trapPoints == 0) continue;

                    // 忍者: 開いた所の前に出てきて、プレイヤーの方を向く。隠れている時は押し入れの奥
                    Vector3 stand = bayRoot.localPosition + bayRoot.localRotation * new Vector3(-0.5f * dw, 0.04f, -0.35f);
                    var ninja = NinjaPanel(room, stand, trapPoints);
                    ninja.transform.localRotation = Quaternion.LookRotation(new Vector3(stand.x, 0f, stand.z));
                    AddTrap(ninja, slider, new Vector3(dw, 0f, 0f), Vector3.zero, bayRoot.localRotation * new Vector3(0f, 0f, 0.8f));
                    panels.Add(ninja);
                }
            }

            // ---- 天井と梁。忍者が出てくる天井板は下へ開く ----
            var hatches = new (float x, float z, int points)[] { (-1.5f, z0 + 5f * DepthK, 300), (1.5f, z0 + 10f * DepthK, 300), (0f, z0 + 16f * DepthK, 500) };
            Prim(PrimitiveType.Cube, "InnerCeiling", room, new Vector3(0f, DojoH + 0.05f, midZ), new Vector3(2f * DojoR + 0.3f, 0.1f, InnerDepth), DojoWoodDark, false);
            for (float z = z0 + 0.6f; z < zEnd; z += 1.1f)
            {
                bool nearHatch = false;
                foreach (var h in hatches) if (Mathf.Abs(h.z - z) < 0.65f) nearHatch = true;
                if (!nearHatch) Prim(PrimitiveType.Cube, "InnerBeam", room, new Vector3(0f, DojoH - 0.04f, z), new Vector3(2f * DojoR, 0.08f, 0.08f), DojoWood, false);
            }
            foreach (var (x, z, points) in hatches)
            {
                var hinge = Group(room, "CeilingTrap");
                hinge.localPosition = new Vector3(x, DojoH - 0.025f, z + 0.5f);
                Prim(PrimitiveType.Cube, "Hatch", hinge, new Vector3(0f, 0f, -0.5f), new Vector3(1f, 0.04f, 1f), DojoWood);
                Prim(PrimitiveType.Cube, "HatchFrame", hinge, new Vector3(0f, -0.022f, -0.5f), new Vector3(0.9f, 0.006f, 0.9f), DojoWoodDark, false);
                // 逆さまにぶら下がって出てくる (倒れる時は天井の方へ跳ね上がる)
                var ninja = NinjaPanel(room, new Vector3(x, DojoH - 0.05f, z), points);
                ninja.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                var label = ninja.visual.Find("Figure/Points");
                if (label != null) label.localRotation = Quaternion.Euler(0f, 0f, 180f); // 点数の文字は読める向きに
                Prim(PrimitiveType.Cylinder, "Rope", ninja.visual, new Vector3(0f, -0.1f, 0f), new Vector3(0.02f, 0.1f, 0.02f), Mat("Rope", new Color(0.85f, 0.8f, 0.65f)), false);
                AddTrap(ninja, hinge, Vector3.zero, new Vector3(-95f, 0f, 0f), new Vector3(0f, 1.8f, 0f)); // 天井裏から降りてくる
                panels.Add(ninja);
            }

            // ---- 何もない畳の上に、煙とともにどろろんと現れる忍者 (仕掛けの所とは重ならない場所) ----
            foreach (var (x, z, points) in new[] { (-2.2f, z0 + 3.3f * DepthK, 200), (2.2f, z0 + 6.3f * DepthK, 300), (0.6f, z0 + 8.9f * DepthK, 300), (-1.9f, z0 + 11.5f * DepthK, 500), (2f, z0 + 15.5f * DepthK, 500) })
                panels.Add(SmokeNinja(room, new Vector3(x, 0.07f, z), points));

            // ---- 途中の2つの仕切り (欄間と柱)。奥行きが分かりやすくなる ----
            foreach (float z in new[] { z0 + InnerDepth / 3f, z0 + InnerDepth * 2f / 3f })
            {
                Prim(PrimitiveType.Cube, "GateLintel", room, new Vector3(0f, DoorTop + 0.06f, z), new Vector3(2f * DojoR, 0.12f, 0.14f), DojoWood, false);
                for (int k = -11; k <= 11; k++)
                    Prim(PrimitiveType.Cube, "GateRanma", room, new Vector3(k * 0.3f, (DoorTop + 0.12f + DojoH) / 2f, z), new Vector3(0.03f, DojoH - DoorTop - 0.12f, 0.03f), DojoWoodDark, false);
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "GatePost", room, new Vector3(s * (DojoR - 0.2f), DoorTop / 2f, z), new Vector3(0.14f, DoorTop, 0.14f), DojoWood, false);
            }

            // ---- 一番奥: 金の屏風絵 (赤い日と松) ----
            Prim(PrimitiveType.Cube, "BackWall", room, new Vector3(0f, DojoH / 2f, zEnd + 0.05f), new Vector3(2f * DojoR + 0.2f, DojoH, 0.1f), DojoPlaster);
            Prim(PrimitiveType.Cube, "BackScreen", room, new Vector3(0f, 1.25f, zEnd - 0.02f), new Vector3(2f * DojoR - 0.6f, 2.1f, 0.02f), gold, false);
            var sun = Prim(PrimitiveType.Cylinder, "Sun", room, new Vector3(1.4f, 1.75f, zEnd - 0.035f), new Vector3(0.9f, 0.004f, 0.9f), Mat("Painted_Sun", new Color(0.85f, 0.15f, 0.1f), new Color(0.3f, 0.04f, 0.02f)), false);
            sun.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var pine = Mat("Painted_Pine", new Color(0.12f, 0.32f, 0.18f));
            Prim(PrimitiveType.Cube, "PineTrunk", room, new Vector3(-1.5f, 1.0f, zEnd - 0.035f), new Vector3(0.12f, 1.4f, 0.004f), Mat("Painted_Trunk", new Color(0.3f, 0.2f, 0.12f)), false);
            for (int k = 0; k < 3; k++)
                Prim(PrimitiveType.Cube, "PineLeaves", room, new Vector3(-1.5f + (k - 1) * 0.35f, 1.5f + k * 0.18f, zEnd - 0.04f), new Vector3(0.9f - k * 0.2f, 0.18f, 0.004f), pine, false);

            // ---- 両側の行灯と、部屋を照らす明かり ----
            var andon = Mat("Andon", new Color(1f, 0.88f, 0.65f), new Color(1.6f, 1.1f, 0.5f));
            foreach (float z in new[] { z0 + 3f * DepthK, z0 + 10f * DepthK, z0 + 17f * DepthK })
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Cube, "AndonStand", room, new Vector3(s * (DojoR - 0.45f), 0.3f, z), new Vector3(0.08f, 0.5f, 0.08f), DojoWoodDark, false);
                    Prim(PrimitiveType.Cube, "Andon", room, new Vector3(s * (DojoR - 0.45f), 0.8f, z), new Vector3(0.3f, 0.5f, 0.3f), andon, false);
                }
                var lightGo = new GameObject("InnerLight");
                lightGo.transform.SetParent(room, false);
                lightGo.transform.localPosition = new Vector3(0f, DojoH - 0.5f, z);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.5f);
                light.intensity = 2f;
                light.range = 8f;
                light.shadows = LightShadows.None;
            }
            return room.gameObject;
        }

        Vector3 TatamiCell(int col, int row, float z0, float tw) => new Vector3(-DojoR + tw * (col + 0.5f), 0f, z0 + TatamiDepth * (row + 0.5f));

        /// <summary>畳1枚 (pos は畳の真ん中の床の高さ。長い方が横)。い草色の板と、長い辺の黒い縁</summary>
        void TatamiMat(Transform parent, Vector3 pos, float width)
        {
            var mat = Prim(PrimitiveType.Cube, "Tatami", parent, pos + new Vector3(0f, 0.05f, 0f), new Vector3(width - 0.03f, 0.04f, TatamiDepth - 0.03f),
                           Mat("Tatami", new Color(0.62f, 0.62f, 0.38f), null, false, 0.15f));
            var edge = Mat("Tatami_Edge", new Color(0.1f, 0.14f, 0.1f));
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "TatamiEdge", mat.transform.parent, pos + new Vector3(0f, 0.072f, s * (TatamiDepth / 2f - 0.04f)), new Vector3(width - 0.03f, 0.004f, 0.04f), edge, false);
        }

        // 忍者の隠れ場所の仕掛けを付ける (忍者が出ている間だけ板が開き、忍者は hideOffset の所から出てくる)。出てくるたびにポーズが変わる。
        // 畳・ふすま・天井板から出てくる忍者は、煙なしでふつうに出てくる (煙は、何もない所に現れる忍者だけ: SmokeNinja)
        void AddTrap(Target ninja, Transform flap, Vector3 flapOffset, Vector3 flapEuler, Vector3 hideOffset)
        {
            var trap = ninja.gameObject.AddComponent<NinjaTrap>();
            trap.target = ninja;
            trap.flap = flap;
            trap.flapOpenOffset = flapOffset;
            trap.flapOpenEuler = flapEuler;
            trap.hideOffset = hideOffset;
            trap.pose = ninja.GetComponent<NinjaPose>();
        }

        // 何もない所 (奥座敷の床の上) に、前触れもなく煙とともにどろろんと現れる忍者。消える時も煙
        Target SmokeNinja(Transform room, Vector3 pos, int points)
        {
            var ninja = NinjaPanel(room, pos, points);
            ninja.transform.localRotation = Quaternion.LookRotation(new Vector3(pos.x, 0f, pos.z)); // プレイヤーの方を向く
            var trap = ninja.gameObject.AddComponent<NinjaTrap>();
            trap.target = ninja;
            trap.openTime = 0.01f; // その場にぱっと現れる
            trap.smoke = TransparentMat("Ninja_Smoke", new Color(0.88f, 0.9f, 0.92f, 0.65f));
            trap.pose = ninja.GetComponent<NinjaPose>();
            return ninja;
        }

        /// <summary>
        /// 忍者のパネル (根元が原点。後ろへ倒れて寝ている)。覆面の忍者で、目だけ出ている。腕は肩、脚は腰から動き、出てくるたびにポーズが変わる (NinjaPose)。
        /// 黄緑の部屋の中で目立つように、装束は点数ごとにはっきりした色 (100 紺 / 200 ワインレッド / 300 濃いオレンジ / 500 以上 金) で、帯は反対の色。
        /// 胸の大きな白い丸に、太い図形の数字で点数。
        /// </summary>
        Target NinjaPanel(Transform parent, Vector3 basePos, int points)
        {
            var root = Group(parent, $"Ninja_{points}");
            root.localPosition = basePos;
            var pivot = Group(root, "Pivot");
            var figure = Group(pivot, "Figure"); // 体全体 (低く構える時に下がる)

            Color suit = points >= 500 ? new Color(0.9f, 0.68f, 0.12f) : points >= 300 ? new Color(0.9f, 0.38f, 0.04f)
                       : points >= 200 ? new Color(0.5f, 0.04f, 0.13f) : new Color(0.07f, 0.11f, 0.4f);
            Color sash = points >= 500 ? new Color(0.5f, 0.04f, 0.13f) : points >= 300 ? new Color(0.07f, 0.11f, 0.4f)
                       : points >= 200 ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.45f, 0.08f);
            int tier = points >= 500 ? 500 : points >= 300 ? 300 : points >= 200 ? 200 : 100;
            var suitMat = Mat($"Ninja_Suit_{tier}", suit, tier >= 500 ? new Color(0.25f, 0.18f, 0.02f) : suit * 0.12f, false, tier >= 500 ? 0.7f : 0.25f);
            var sashMat = Mat($"Ninja_Sash_{tier}", sash);
            var skin = Mat("Pilot_Skin", new Color(0.95f, 0.75f, 0.6f));
            var black = Mat("Ninja_Eye", new Color(0.02f, 0.02f, 0.02f));
            var steel = Mat("Shuriken_Steel", new Color(0.75f, 0.77f, 0.8f), null, false, 0.85f);

            // 脚 (腰から動く)。当たり判定あり
            Transform Limb(string name, Vector3 joint, float length, float width)
            {
                var j = Group(figure, name);
                j.localPosition = joint;
                Prim(PrimitiveType.Cube, name + "Part", j, new Vector3(0f, -length / 2f, 0f), new Vector3(width, length, 0.03f), suitMat);
                return j;
            }
            var legL = Limb("LegL", new Vector3(-0.1f, 0.6f, 0f), 0.6f, 0.14f);
            var legR = Limb("LegR", new Vector3(0.1f, 0.6f, 0f), 0.6f, 0.14f);
            Prim(PrimitiveType.Cube, "Body", figure, new Vector3(0f, 0.85f, 0f), new Vector3(0.44f, 0.52f, 0.03f), suitMat);
            Prim(PrimitiveType.Cube, "Sash", figure, new Vector3(0f, 0.63f, -0.003f), new Vector3(0.46f, 0.07f, 0.03f), sashMat, false);
            // 腕 (肩から動く)
            var armL = Limb("ArmL", new Vector3(-0.21f, 1.07f, 0.002f), 0.42f, 0.1f);
            var armR = Limb("ArmR", new Vector3(0.21f, 1.07f, 0.002f), 0.42f, 0.1f);
            Prim(PrimitiveType.Sphere, "Head", figure, new Vector3(0f, 1.3f, 0f), new Vector3(0.34f, 0.36f, 0.05f), suitMat);
            // 覆面から出た目元と、鋭い目
            Prim(PrimitiveType.Cube, "EyeBand", figure, new Vector3(0f, 1.32f, -0.022f), new Vector3(0.26f, 0.075f, 0.01f), skin, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, "Eye", figure, new Vector3(s * 0.06f, 1.32f, -0.028f), new Vector3(0.045f, 0.025f, 0.008f), black, false);
                var brow = Prim(PrimitiveType.Cube, "Brow", figure, new Vector3(s * 0.065f, 1.355f, -0.028f), new Vector3(0.07f, 0.014f, 0.006f), black, false);
                brow.transform.localRotation = Quaternion.Euler(0f, 0f, s * -15f);
            }
            // 頭の結び目 (はちまきの端)
            var knot = Prim(PrimitiveType.Cube, "Knot", figure, new Vector3(0.22f, 1.4f, 0.005f), new Vector3(0.22f, 0.05f, 0.02f), sashMat, false);
            knot.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            // 右手の刀 (手の先から、腕と同じ向きに伸びる) と、左手の手裏剣
            var katana = Group(armR, "Katana");
            katana.localPosition = new Vector3(0f, -0.42f, -0.012f);
            Prim(PrimitiveType.Cube, "Blade", katana, new Vector3(0f, -0.36f, 0f), new Vector3(0.03f, 0.7f, 0.01f), steel, false);
            Prim(PrimitiveType.Cube, "Tsuba", katana, new Vector3(0f, -0.01f, 0f), new Vector3(0.09f, 0.025f, 0.012f), black, false);
            var held = Group(armL, "HeldShuriken");
            held.localPosition = new Vector3(0f, -0.44f, -0.012f);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Blade", held, Vector3.zero, new Vector3(0.025f, 0.13f, 0.006f), steel, false).transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            // 胸の点数 (大きな白い丸に、太い図形の数字)
            var disc = Prim(PrimitiveType.Cylinder, "PointDisc", figure, new Vector3(0f, 0.86f, -0.018f), new Vector3(0.36f, 0.003f, 0.36f), Mat("Target_White", Color.white), false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PointDigits(figure, points, new Vector3(0f, 0.86f, -0.024f), 0.31f, 0.17f, DigitMat);

            var pose = root.gameObject.AddComponent<NinjaPose>();
            pose.figure = figure;
            pose.armL = armL;
            pose.armR = armR;
            pose.legL = legL;
            pose.legR = legR;
            pose.katana = katana.gameObject;
            pose.shuriken = held.gameObject;
            pose.Apply(0);

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.NinjaPanel;
            target.points = points;
            target.manualControl = true;
            target.visual = pivot;
            target.SetAvailable(false);
            return target;
        }

        /// <summary>天井 (ローカル原点が天井の高さ): 黒い板に格子の梁、真ん中に吊るした行灯と、部屋を照らす明かり</summary>
        Transform BuildDojoCeiling(Transform parent)
        {
            var c = Group(parent, "Ceiling");
            c.localPosition = new Vector3(0f, DojoH, 0f);
            float size = 2f * DojoR + 0.5f;
            Prim(PrimitiveType.Cube, "CeilingBoard", c, new Vector3(0f, 0.05f, 0f), new Vector3(size, 0.1f, size), DojoWoodDark);
            for (int k = -5; k <= 5; k++)
            {
                Prim(PrimitiveType.Cube, "BeamX", c, new Vector3(0f, -0.03f, k * 0.62f), new Vector3(2f * DojoR, 0.06f, 0.06f), DojoWood, false);
                Prim(PrimitiveType.Cube, "BeamZ", c, new Vector3(k * 0.62f, -0.03f, 0f), new Vector3(0.06f, 0.06f, 2f * DojoR), DojoWood, false);
            }
            // 行灯 (頭の上、手が届かない高さ)
            Prim(PrimitiveType.Cylinder, "LanternCord", c, new Vector3(0f, -0.2f, 0f), new Vector3(0.015f, 0.18f, 0.015f), DojoWoodDark, false);
            Prim(PrimitiveType.Cylinder, "Lantern", c, new Vector3(0f, -0.58f, 0f), new Vector3(0.36f, 0.2f, 0.36f), Mat("Andon", new Color(1f, 0.88f, 0.65f), new Color(1.6f, 1.1f, 0.5f)), false);
            foreach (float y in new[] { -0.39f, -0.77f })
                Prim(PrimitiveType.Cylinder, "LanternRim", c, new Vector3(0f, y, 0f), new Vector3(0.38f, 0.015f, 0.38f), DojoWoodDark, false);
            var lightGo = new GameObject("RoomLight");
            lightGo.transform.SetParent(c, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.7f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.78f, 0.5f);
            light.intensity = 2.2f;
            light.range = 8f;
            light.shadows = LightShadows.None;
            return c;
        }

        /// <summary>畳 (足元の穴の上も覆う)。い草色の板と、縁の黒い線</summary>
        GameObject BuildTatami(Transform parent)
        {
            var t = Group(parent, "Tatami");
            float size = 2f * DojoR;
            Prim(PrimitiveType.Cube, "Mat", t, new Vector3(0f, 0.02f, 0f), new Vector3(size, 0.04f, size), Mat("Tatami", new Color(0.62f, 0.62f, 0.38f), null, false, 0.15f));
            var edge = Mat("Tatami_Edge", new Color(0.1f, 0.14f, 0.1f));
            for (int k = -1; k <= 1; k++)
            {
                Prim(PrimitiveType.Cube, "EdgeX", t, new Vector3(k * size / 4f, 0.042f, 0f), new Vector3(0.04f, 0.004f, size), edge, false);
                Prim(PrimitiveType.Cube, "EdgeZ", t, new Vector3(0f, 0.042f, k * size / 4f), new Vector3(size, 0.004f, 0.04f), edge, false);
            }
            return t.gameObject;
        }

        /// <summary>手裏剣の的: 紫の板に白い手裏剣が回り、「NINJA」の文字</summary>
        GameObject NinjaLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Ninja_Plate", new Color(0.3f, 0.1f, 0.4f), new Color(0.1f, 0.03f, 0.15f)));
            var spin = Group(look, "Shuriken");
            spin.localPosition = c + new Vector3(0f, 0.02f, -0.03f);
            var rot = spin.gameObject.AddComponent<Rotator>();
            rot.axis = Vector3.forward;
            rot.speed = 300f;
            var steel = Mat("Shuriken_Steel", new Color(0.75f, 0.77f, 0.8f), null, false, 0.85f);
            for (int i = 0; i < 4; i++)
            {
                var blade = Prim(PrimitiveType.Cube, "Blade", spin, Vector3.zero, new Vector3(0.03f, 0.18f, 0.006f), steel, false);
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            }
            var hub = Prim(PrimitiveType.Cylinder, "Hub", spin, new Vector3(0f, 0f, -0.004f), new Vector3(0.05f, 0.004f, 0.05f), Mat("Ninja_Eye", new Color(0.02f, 0.02f, 0.02f)), false);
            hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MakeText(look, "Label", "NINJA", c + new Vector3(0f, -0.115f, -0.025f), 0.04f, Color.white);
            return look.gameObject;
        }
    }
}
