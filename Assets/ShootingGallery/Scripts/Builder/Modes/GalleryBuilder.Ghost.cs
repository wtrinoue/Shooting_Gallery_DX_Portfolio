using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 幽霊モード (おばけの的を撃つと始まる) の生成: でこぼこの濃い茶色の土の地面、朽ちたぎざぎざの木、バケツを上げ下ろしする古井戸、お墓、
    /// 上へいくほど太くなるいびつで豪華なお化け屋敷 (ローカル +Z の先。向きは GhostManor が的を撃った方へ回す)、紫の空に照る月と雲、鬼火、
    /// 的 (お化け・ほうきに乗った魔女・お墓から出るゾンビと骸骨のパネル)、おばけの的の見た目。プレイヤーの足元 (原点) が地面の高さ。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float ManorZ = 46f; // お化け屋敷の真ん中までの距離 (高さ約 28m の全体が、見上げずに視界に収まるくらい遠く)
        // 月の場所 (お化け屋敷の左上の、ずっと遠く。お化け屋敷より奥なので手前にかぶらない)。空のドームの明るい所もこの向き
        static readonly Vector3 GhostMoonPos = new Vector3(-0.45f, 0.6f, 0.66f).normalized * 80f;

        GhostManor BuildGhost()
        {
            var go = new GameObject("GhostManor");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;
            var shown = new List<GameObject>();

            // ---- でこぼこの濃い茶色の土 ----
            var soil = Mat("Ghost_Soil", new Color(0.2f, 0.12f, 0.07f), null, false, 0.05f);
            var soilDark = Mat("Ghost_SoilDark", new Color(0.13f, 0.08f, 0.05f), null, false, 0.05f);
            var soilLight = Mat("Ghost_SoilLight", new Color(0.27f, 0.17f, 0.1f), null, false, 0.05f);
            var ground = Group(root, "GhostGround");
            shown.Add(ground.gameObject);
            // 足元から外へ広がる土の円盤 (いつもの地面より少しだけ上。広がる間に地面とちらつかないように)
            var soilDisc = Prim(PrimitiveType.Cylinder, "Soil", root, new Vector3(0f, -0.055f, 0f), new Vector3(180f, 0.06f, 180f), soil, false);
            shown.Add(soilDisc);
            for (int i = 0; i < 110; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Mathf.Lerp(2.2f, 60f, Mathf.Pow(Random.value, 1.6f));
                float near = Mathf.Clamp01((r - 2f) / 12f); // 近くは低く小さく (出てくるゾンビが隠れないように)
                float w = Mathf.Lerp(0.6f, 4f, near) * Random.Range(0.7f, 1.3f);
                float h = Mathf.Lerp(0.12f, 0.9f, near) * Random.Range(0.6f, 1.2f);
                var bump = Prim(PrimitiveType.Sphere, "Bump", ground, new Vector3(Mathf.Sin(a) * r, -h * 0.2f, Mathf.Cos(a) * r),
                                new Vector3(w * Random.Range(1f, 1.8f), h, w), i % 3 == 0 ? soilDark : i % 3 == 1 ? soilLight : soil, false);
                bump.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            // 土くれと小石
            var pebble = Mat("Ghost_Pebble", new Color(0.3f, 0.28f, 0.27f));
            for (int i = 0; i < 40; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(1.2f, 12f);
                Prim(PrimitiveType.Sphere, "Clod", ground, new Vector3(Mathf.Sin(a) * r, 0.02f, Mathf.Cos(a) * r), Vector3.one * Random.Range(0.08f, 0.22f), i % 2 == 0 ? soilDark : pebble, false);
            }
            // お化け屋敷へ続く、くねくねした土の道
            for (int i = 0; i < 13; i++) // (お化け屋敷の玄関まで)
            {
                float z0 = 1.5f + i * 3.1f;
                var path = Prim(PrimitiveType.Cube, "Path", ground, new Vector3(Mathf.Sin(i * 0.9f) * 0.7f, 0.004f, z0 + 1.55f), new Vector3(2.2f, 0.01f, 3.4f), soilLight, false);
                path.transform.localRotation = Quaternion.Euler(0f, Mathf.Cos(i * 0.9f) * 14f, 0f);
            }

            // ---- 朽ちてぎざぎざの濃い灰色の木 ----
            var bark = Mat("Ghost_Bark", new Color(0.2f, 0.2f, 0.21f), null, false, 0.05f);
            var barkDark = Mat("Ghost_BarkDark", new Color(0.12f, 0.12f, 0.13f), null, false, 0.05f);
            // 朽ちた家の場所 (角度・距離・大きさ・窓が光るか)。お化け屋敷 (正面) の手前の左右と、横に
            // 奥のほう (28〜44m) にも、ぽつぽつと朽ちた家が続く (お化け屋敷の正面 ±25° は空けておく)
            var housePlaces = new (float deg, float r, float scale, bool glow)[]
            {
                (22f, 19f, 1f, false), (58f, 23f, 1.1f, true), (282f, 21f, 1f, false), (338f, 17f, 0.95f, true),
                (40f, 36f, 1.2f, false), (95f, 31f, 1.1f, true), (135f, 40f, 1.25f, false), (200f, 34f, 1.15f, true), (245f, 42f, 1.2f, false), (310f, 33f, 1.1f, true),
            };
            Vector3 HousePos(float deg, float r) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad) * r, 0f, Mathf.Cos(deg * Mathf.Deg2Rad) * r);
            bool NearHouse(Vector3 p)
            {
                foreach (var h in housePlaces) if ((HousePos(h.deg, h.r) - p).magnitude < 4.5f) return true;
                return false;
            }

            var trees = Group(root, "DeadTrees");
            shown.Add(trees.gameObject);
            float[] treeAngles = { 40f, 75f, 110f, 150f, 185f, 220f, 255f, 300f, 330f };
            for (int i = 0; i < treeAngles.Length; i++)
            {
                float a = (treeAngles[i] + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
                float r = i % 3 == 0 ? Random.Range(13.5f, 15f) : Random.Range(15f, 26f); // お墓 (〜13m) より外
                var tp = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                if (NearHouse(tp)) continue; // (朽ちた家と重ならないように)
                DeadTree(trees, tp, Random.Range(3.5f, 6.5f), i % 2 == 0 ? bark : barkDark);
            }
            // 奥の木立 (28〜55m。遠いほど大きく、まばらに。お化け屋敷の正面 ±18° は空けて、お屋敷が見えるように)
            for (int i = 0; i < 22; i++)
            {
                float deg = 18f + (i + Random.Range(-0.35f, 0.35f)) * (324f / 22f);
                float r = Random.Range(28f, 55f);
                var tp = new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad) * r, 0f, Mathf.Cos(deg * Mathf.Deg2Rad) * r);
                if (NearHouse(tp)) continue;
                DeadTree(trees, tp, Random.Range(5f, 9f) * Mathf.Lerp(1f, 1.3f, (r - 28f) / 27f), i % 2 == 0 ? barkDark : bark);
            }

            // ---- 朽ちた家 (お化け屋敷のまわりにぽつぽつと。1軒は窓が緑に光る) ----
            var ruins = Group(root, "Ruins");
            shown.Add(ruins.gameObject);
            foreach (var h in housePlaces)
                RuinedHouse(ruins, HousePos(h.deg, h.r), h.scale, h.glow, 100 + (int)h.deg);

            // ---- 古井戸 ----
            var well = BuildWell(root, new Vector3(-4.3f, 0f, 4.3f), out var bucket, out var rope, out var crank);
            shown.Add(well.gameObject);

            // ---- 魔女の鍋 (井戸の右、プレイヤーの左手前。泡と火は組み上げずに GhostManor が動かす) ----
            var cauldronRoot = Group(root, "Cauldron");
            shown.Add(cauldronRoot.gameObject);
            var cauldronFx = Group(root, "CauldronFx");
            shown.Add(cauldronFx.gameObject);
            var bubbles = new List<Transform>();
            var flames = new List<Transform>();
            float ca = 292f * Mathf.Deg2Rad;
            WitchCauldron(cauldronRoot, new Vector3(Mathf.Sin(ca), 0f, Mathf.Cos(ca)) * 3.6f, cauldronFx, bubbles, flames, out float brewY);

            // ---- お墓 (ゾンビと骸骨はこの前の土から出てくる) ----
            var graves = Group(root, "Graves");
            shown.Add(graves.gameObject);
            var spots = new List<Vector3>();
            float[] graveAngles = { 30f, 57f, 84f, 111f, 138f, 165f, 192f, 219f, 246f, 273f }; // 正面 (お化け屋敷への道) と左前 (井戸) はあける
            for (int i = 0; i < graveAngles.Length; i++)
            {
                float a = (graveAngles[i] + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
                float r = 4.8f + (i % 3) * 3.6f + Random.Range(0f, 1.2f); // 近い・中くらい・遠い の3段 (4.8〜13m。出てくるゾンビとの距離がばらばらに)
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Grave(graves, dir * r, dir, i);
                spots.Add(dir * (r - 0.75f)); // お墓の手前の盛り土
            }
            // カボチャのランタン (お墓のそばに少し)
            var pumpkin = Mat("Ghost_Pumpkin", new Color(0.95f, 0.45f, 0.08f), new Color(0.25f, 0.08f, 0f));
            var pumpkinFace = Mat("Ghost_PumpkinFace", new Color(1f, 0.85f, 0.3f), new Color(2.2f, 1.3f, 0.2f));
            foreach (float deg in new[] { 18f, 124f, 232f, 342f })
            {
                float a = deg * Mathf.Deg2Rad;
                var p = Group(graves, "JackOLantern");
                p.localPosition = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 4.6f;
                p.localRotation = Quaternion.LookRotation(p.localPosition);
                Prim(PrimitiveType.Sphere, "Pumpkin", p, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.4f, 0.45f), pumpkin, false);
                Prim(PrimitiveType.Cylinder, "Stem", p, new Vector3(0f, 0.43f, 0f), new Vector3(0.06f, 0.06f, 0.06f), Mat("Ghost_Stem", new Color(0.25f, 0.35f, 0.12f)), false);
                foreach (float s in new[] { -1f, 1f })
                    ShapeObj(p, "Eye", Shape.Triangle, new Vector3(s * 0.1f, 0.26f, -0.22f), new Vector3(0.12f, 0.1f, 1f), pumpkinFace, 0f);
                Prim(PrimitiveType.Cube, "Mouth", p, new Vector3(0f, 0.13f, -0.215f), new Vector3(0.24f, 0.05f, 0.02f), pumpkinFace, false);
            }

            // ---- お化け屋敷 ----
            var manor = BuildManor(root);
            shown.Add(manor.gameObject);

            // ---- 紫の空の月・雲・星 ----
            var sky = BuildGhostSky(root);
            shown.Add(sky.gameObject);
            // 空のドーム (月に近いほど明るく、離れるほど暗い紫。組み上げずに、GhostManor がすっと出す)
            var dome = BuildGhostSkyDome(root, GhostMoonPos);
            shown.Add(dome.gameObject);

            // ---- 鬼火 (的ではない) ----
            var wispRoot = Group(root, "Wisps");
            shown.Add(wispRoot.gameObject);
            var wispMat = Mat("Ghost_Wisp", new Color(0.6f, 1f, 0.9f), new Color(0.8f, 2.4f, 2f), true);
            var wispHalo = TransparentMat("Ghost_WispHalo", new Color(0.5f, 1f, 0.85f, 0.18f));
            var wisps = new List<Transform>();
            for (int i = 0; i < 7; i++)
            {
                float a = (i * 51f + 15f) * Mathf.Deg2Rad, r = Random.Range(3.5f, 9f);
                var w = Group(wispRoot, "Wisp");
                w.localPosition = new Vector3(Mathf.Sin(a) * r, Random.Range(0.7f, 1.8f), Mathf.Cos(a) * r);
                Prim(PrimitiveType.Sphere, "Core", w, Vector3.zero, Vector3.one * 0.1f, wispMat, false);
                Prim(PrimitiveType.Sphere, "Halo", w, Vector3.zero, Vector3.one * 0.32f, wispHalo, false);
                wisps.Add(w);
            }

            // ---- 的 ----
            var puff = TransparentMat("Ghost_Puff", new Color(0.62f, 0.38f, 0.9f, 0.6f));
            var ghosts = new List<GhostSpook>();
            for (int i = 0; i < 8; i++) ghosts.Add(BuildGhostSpook(root, i, puff));
            var witches = new List<GhostSpook>();
            for (int i = 0; i < 3; i++) witches.Add(BuildWitchSpook(root, i, puff));
            var risers = new List<GhostSpook>();
            for (int i = 0; i < 5; i++) risers.Add(BuildZombieSpook(root, i, puff));
            for (int i = 0; i < 4; i++) risers.Add(BuildSkeletonSpook(root, i, puff));

            var manorComp = go.AddComponent<GhostManor>();
            var groundParts = new List<Transform>();
            foreach (var name in new[] { "Ground", "PitRim" })
            {
                var t = world.Find(name);
                if (t != null) groundParts.Add(t);
            }
            if (playerMark != null) groundParts.Add(playerMark.transform);
            manorComp.groundParts = groundParts.ToArray();
            manorComp.showDuringStage = shown.ToArray();
            // 組み上げる順番: 土のでこぼこ (真ん中から外へ) → 木・お墓・井戸・お化け屋敷 (下の部品から上へ) → 月と雲 → 鬼火
            manorComp.soil = soilDisc.transform;
            manorComp.assembleRoots = new[] { ground, trees, ruins, graves, well, cauldronRoot, manor, sky, wispRoot };
            manorComp.assembleDelays = new[] { 0f, 0.8f, 0.9f, 0.9f, 1.1f, 1.2f, 1.0f, 3.6f, 4.1f };
            manorComp.assembleSpreads = new[] { 1.8f, 1.8f, 2.2f, 1.3f, 1.0f, 0.8f, 3.4f, 1.0f, 0.5f };
            manorComp.assembleRadial = new[] { true, false, false, false, false, false, false, false, false };
            manorComp.skyDome = dome;
            manorComp.cauldronBubbles = bubbles.ToArray();
            manorComp.cauldronFlames = flames.ToArray();
            manorComp.cauldronSurfaceY = brewY;
            manorComp.wellBucket = bucket;
            manorComp.wellRope = rope;
            manorComp.wellCrank = crank;
            manorComp.wisps = wisps.ToArray();
            manorComp.ghosts = ghosts.ToArray();
            manorComp.witches = witches.ToArray();
            manorComp.risers = risers.ToArray();
            manorComp.graveSpots = spots.ToArray();
            // 幽霊モードの時だけ出る (シーンに生成した時も隠しておく)
            foreach (var g in shown) g.SetActive(false);
            go.SetActive(true);
            return manorComp;
        }

        /// <summary>懐中電灯の光の筋: 先が原点で +Z へ広がる円すいの側面 (長さ 1, 底の半径 1, ふたなし)。内側からも見えるよう表裏の両面</summary>
        Mesh ConeMesh()
        {
            const string Name = "Shape_Cone";
            if (meshes.TryGetValue(Name, out var cached)) return cached;
            const int Segs = 20;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < Segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / Segs, a1 = (i + 1) * Mathf.PI * 2f / Segs;
                var p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 1f);
                var p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 1f);
                int k = verts.Count;
                verts.Add(Vector3.zero); verts.Add(p0); verts.Add(p1);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2); // 表
                tris.Add(k); tris.Add(k + 2); tris.Add(k + 1); // 裏
            }
            var mesh = new Mesh { name = Name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, Name);
            meshes[Name] = mesh;
            return mesh;
        }

        // 平らな三角形・星の板 (ShapeMesh) を置く。表は -Z
        GameObject ShapeObj(Transform parent, string name, Shape shape, Vector3 pos, Vector3 scale, Material mat, float rotZ)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = ShapeMesh(shape);
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        // ------------------------------------------------------------------
        // 木・井戸・お墓
        // ------------------------------------------------------------------

        /// <summary>朽ちた木: 折れ曲がりながら細くなる幹と、ぎざぎざに折れ曲がって尖った枝。葉はない</summary>
        void DeadTree(Transform parent, Vector3 pos, float h, Material mat)
        {
            var tree = Group(parent, "DeadTree");
            tree.localPosition = pos;
            tree.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Vector3 p = Vector3.zero, dir = Vector3.up;
            float r = h * 0.075f;
            const int Segs = 4;
            float segLen = h / Segs;
            for (int i = 0; i < Segs; i++)
            {
                dir = (dir + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f))).normalized;
                Limb(tree, p, dir, segLen * 1.05f, r, mat);
                p += dir * segLen;
                if (i >= 1)
                {
                    var side = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    Branch(tree, p - dir * segLen * 0.3f, (side + Vector3.up * Random.Range(0.3f, 0.9f)).normalized, segLen * Random.Range(0.8f, 1.2f), r * 0.55f, mat);
                }
                r *= 0.7f;
            }
            Limb(tree, p, (dir + Random.insideUnitSphere * 0.5f).normalized, segLen * 0.6f, r * 0.6f, mat); // 折れた先
            // 根 (地面に張り出す)
            for (int k = 0; k < 4; k++)
            {
                var d = Quaternion.Euler(0f, k * 90f + Random.Range(-20f, 20f), 0f) * new Vector3(0f, -0.25f, 1f);
                Limb(tree, Vector3.up * 0.15f, d.normalized, h * 0.14f, h * 0.05f, mat);
            }
            // うろ
            Prim(PrimitiveType.Sphere, "Hollow", tree, new Vector3(0f, h * 0.3f, -h * 0.06f), new Vector3(h * 0.05f, h * 0.08f, h * 0.03f), Mat("Ghost_Hollow", new Color(0.02f, 0.02f, 0.02f)), false);
        }

        // 2回折れ曲がる枝。先で2本に分かれ、それぞれ尖った小枝になる
        void Branch(Transform parent, Vector3 from, Vector3 dir, float len, float thick, Material mat)
        {
            Vector3 p = from;
            for (int k = 0; k < 2; k++)
            {
                var d = (dir + Random.insideUnitSphere * 0.55f).normalized;
                Limb(parent, p, d, len * 0.5f, thick, mat);
                p += d * len * 0.5f;
                thick *= 0.75f;
            }
            for (int k = 0; k < 2; k++)
                Limb(parent, p, (dir + Random.insideUnitSphere * 0.9f + Vector3.up * 0.2f).normalized, len * Random.Range(0.3f, 0.5f), thick * 0.6f, mat);
        }

        void Limb(Transform parent, Vector3 from, Vector3 dir, float len, float thick, Material mat)
        {
            var limb = Prim(PrimitiveType.Cube, "Limb", parent, from + dir * len / 2f, new Vector3(thick, len, thick), mat, false);
            limb.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir) * Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
        }

        /// <summary>古井戸: 石積みの丸い井戸、2本の柱と小さな屋根、巻き上げの軸とハンドル、ロープの先に古いバケツ (GhostManor が上げ下げする)</summary>
        Transform BuildWell(Transform parent, Vector3 pos, out Transform bucket, out Transform rope, out Transform crank)
        {
            var well = Group(parent, "Well");
            well.localPosition = pos;
            well.localRotation = Quaternion.LookRotation(new Vector3(pos.x, 0f, pos.z)) * Quaternion.Euler(0f, 90f, 0f); // 横からハンドルが見える向き
            var stone = Mat("Ghost_WellStone", new Color(0.35f, 0.33f, 0.34f), null, false, 0.1f);
            var stoneDark = Mat("Ghost_WellStoneDark", new Color(0.25f, 0.24f, 0.26f), null, false, 0.1f);
            var wood = Mat("Ghost_OldWood", new Color(0.3f, 0.22f, 0.15f), null, false, 0.1f);
            var rust = Mat("Ghost_Rust", new Color(0.4f, 0.22f, 0.12f), null, false, 0.3f);
            const int Stones = 14;
            for (int layer = 0; layer < 2; layer++)
                for (int i = 0; i < Stones; i++)
                {
                    float a = (i + layer * 0.5f) * Mathf.PI * 2f / Stones;
                    var s = Prim(PrimitiveType.Cube, "Stone", well, new Vector3(Mathf.Sin(a) * 0.75f, 0.15f + layer * 0.3f, Mathf.Cos(a) * 0.75f),
                                 new Vector3(0.36f, 0.28f, 0.22f), (i + layer) % 3 == 0 ? stoneDark : stone, false);
                    s.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), a * Mathf.Rad2Deg + 90f, Random.Range(-4f, 4f));
                }
            Prim(PrimitiveType.Cylinder, "Water", well, new Vector3(0f, 0.3f, 0f), new Vector3(1.3f, 0.01f, 1.3f), Mat("Ghost_WellWater", new Color(0.02f, 0.03f, 0.04f), null, false, 0.9f), false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Post", well, new Vector3(s * 0.85f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.14f), wood, false);
            foreach (float s in new[] { -1f, 1f })
            {
                var roof = Prim(PrimitiveType.Cube, "Roof", well, new Vector3(0f, 2.45f, s * 0.38f), new Vector3(2.1f, 0.06f, 0.95f), Mat("Ghost_Shingle", new Color(0.22f, 0.1f, 0.1f)), false);
                roof.transform.localRotation = Quaternion.Euler(-s * 35f, 0f, 0f);
            }
            // 巻き上げの軸とハンドル (軸は X 向き。GhostManor がローカル X まわりに回す)
            crank = Group(well, "Crank");
            crank.localPosition = new Vector3(0f, 1.95f, 0f);
            Prim(PrimitiveType.Cylinder, "Axle", crank, Vector3.zero, new Vector3(0.08f, 0.9f, 0.08f), wood, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cylinder, "RopeCoil", crank, Vector3.zero, new Vector3(0.16f, 0.25f, 0.16f), Mat("Ghost_Rope", new Color(0.55f, 0.45f, 0.3f)), false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cube, "CrankArm", crank, new Vector3(0.98f, 0.15f, 0f), new Vector3(0.05f, 0.32f, 0.05f), rust, false);
            Prim(PrimitiveType.Cube, "CrankGrip", crank, new Vector3(1.06f, 0.3f, 0f), new Vector3(0.18f, 0.05f, 0.05f), wood, false);
            // ロープ (上端が軸。ローカル Y の大きさがそのまま長さ)
            rope = Group(well, "Rope");
            rope.localPosition = new Vector3(0f, 1.95f, 0f);
            rope.localScale = new Vector3(1f, 0.5f, 1f);
            Prim(PrimitiveType.Cylinder, "RopeLine", rope, new Vector3(0f, -0.5f, 0f), new Vector3(0.025f, 0.5f, 0.025f), Mat("Ghost_Rope", new Color(0.55f, 0.45f, 0.3f)), false);
            // 古いバケツ (真ん中が原点。取っ手の上が +0.3)
            bucket = Group(well, "Bucket");
            bucket.localPosition = new Vector3(0f, 1.15f, 0f);
            Prim(PrimitiveType.Cylinder, "BucketBody", bucket, Vector3.zero, new Vector3(0.3f, 0.17f, 0.3f), wood, false);
            foreach (float y in new[] { -0.1f, 0.1f })
                Prim(PrimitiveType.Cylinder, "BucketBand", bucket, new Vector3(0f, y, 0f), new Vector3(0.31f, 0.012f, 0.31f), rust, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Handle", bucket, new Vector3(s * 0.15f, 0.22f, 0f), new Vector3(0.015f, 0.17f, 0.015f), rust, false);
            Prim(PrimitiveType.Cube, "HandleTop", bucket, new Vector3(0f, 0.3f, 0f), new Vector3(0.32f, 0.015f, 0.015f), rust, false);
            return well;
        }

        /// <summary>お墓 (表 -Z がプレイヤー向き): 丸い墓石 / 十字架 / オベリスク / 割れた墓石。少し傾いていて、手前に盛り土</summary>
        void Grave(Transform parent, Vector3 pos, Vector3 outward, int index)
        {
            var g = Group(parent, "Grave");
            g.localPosition = pos;
            g.localRotation = Quaternion.LookRotation(outward);
            var stone = index % 2 == 0 ? Mat("Ghost_GraveStone", new Color(0.42f, 0.42f, 0.45f), null, false, 0.15f)
                                       : Mat("Ghost_GraveStoneDark", new Color(0.3f, 0.3f, 0.34f), null, false, 0.15f);
            var moss = Mat("Ghost_Moss", new Color(0.2f, 0.3f, 0.15f));
            var s = Group(g, "Stone");
            s.localRotation = Quaternion.Euler(Random.Range(-10f, 6f), 0f, Random.Range(-12f, 12f));
            switch (index % 4)
            {
                case 0: // 丸い墓石
                    Prim(PrimitiveType.Cube, "Slab", s, new Vector3(0f, 0.38f, 0f), new Vector3(0.62f, 0.76f, 0.16f), stone, false);
                    Prim(PrimitiveType.Cylinder, "Top", s, new Vector3(0f, 0.76f, 0f), new Vector3(0.62f, 0.08f, 0.62f), stone, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    MakeText(s, "RIP", "R.I.P.", new Vector3(0f, 0.55f, -0.09f), 0.14f, new Color(0.12f, 0.12f, 0.14f));
                    break;
                case 1: // 十字架
                    Prim(PrimitiveType.Cube, "CrossV", s, new Vector3(0f, 0.6f, 0f), new Vector3(0.15f, 1.2f, 0.13f), stone, false);
                    Prim(PrimitiveType.Cube, "CrossH", s, new Vector3(0f, 0.85f, 0f), new Vector3(0.62f, 0.15f, 0.13f), stone, false);
                    break;
                case 2: // オベリスク
                    Prim(PrimitiveType.Cube, "Base", s, new Vector3(0f, 0.12f, 0f), new Vector3(0.6f, 0.24f, 0.6f), stone, false);
                    Prim(PrimitiveType.Cube, "Pillar", s, new Vector3(0f, 0.85f, 0f), new Vector3(0.32f, 1.25f, 0.32f), stone, false);
                    Prim(PrimitiveType.Cube, "Tip", s, new Vector3(0f, 1.52f, 0f), new Vector3(0.22f, 0.22f, 0.22f), stone, false).transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
                    break;
                default: // 割れた墓石 (上が欠けて、かけらが転がっている)
                    var slab = Prim(PrimitiveType.Cube, "Broken", s, new Vector3(0f, 0.27f, 0f), new Vector3(0.66f, 0.54f, 0.16f), stone, false);
                    Prim(PrimitiveType.Cube, "Chip", s, new Vector3(0.18f, 0.56f, 0f), new Vector3(0.3f, 0.16f, 0.16f), stone, false).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                    Prim(PrimitiveType.Cube, "Shard", g, new Vector3(-0.45f, 0.06f, -0.2f), new Vector3(0.28f, 0.12f, 0.2f), stone, false).transform.localRotation = Quaternion.Euler(0f, 30f, 10f);
                    break;
            }
            Prim(PrimitiveType.Sphere, "Moss", s, new Vector3(Random.Range(-0.2f, 0.2f), 0.06f, -0.06f), new Vector3(0.3f, 0.12f, 0.12f), moss, false);
            // 手前の盛り土 (ここからゾンビや骸骨が出てくる)
            Prim(PrimitiveType.Sphere, "Mound", g, new Vector3(0f, -0.02f, -0.75f), new Vector3(0.75f, 0.18f, 1.2f), Mat("Ghost_SoilDark", new Color(0.13f, 0.08f, 0.05f), null, false, 0.05f), false);
        }

        // ------------------------------------------------------------------
        // お化け屋敷
        // ------------------------------------------------------------------

        enum WindowShape { Rect, Arch, Round, Diamond, Oval, Triangle, Star, Twin }

        /// <summary>
        /// お化け屋敷 (表 -Z がプレイヤー向き)。5階建てで、上の階ほど幅も奥行きも大きくなり (頭でっかち)、階ごとに少しずつずれて傾いている。
        /// 階ごとに形の違う窓 (四角・アーチ・丸・ひし形・楕円・三角・星・双子のアーチ) が並び、明かりの点いた窓と真っ暗な窓がある。
        /// 正面に大階段・柱の並ぶ玄関ポーチ・三角の破風・半円の明かり窓の付いた大きな両開きの扉と緑の灯籠、その上に丸いバラ窓とバルコニー。
        /// 両脇に外へ傾いた円い塔 (上ほど太い) ととんがり帽子の屋根、屋上にマンサード屋根・屋根窓・曲がった煙突・時計塔。まわりをコウモリが回る
        /// </summary>
        Transform BuildManor(Transform parent)
        {
            var manor = Group(parent, "Manor");
            manor.localPosition = new Vector3(0f, 0f, ManorZ);
            var wall = Mat("Manor_Wall", new Color(0.2f, 0.15f, 0.23f), null, false, 0.15f);
            var wallDark = Mat("Manor_WallDark", new Color(0.14f, 0.1f, 0.17f), null, false, 0.15f);
            var trim = Mat("Manor_Trim", new Color(0.45f, 0.35f, 0.24f), null, false, 0.4f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var roof = Mat("Manor_Roof", new Color(0.12f, 0.08f, 0.17f), null, false, 0.3f);
            var lit = Mat("Manor_WindowLit", new Color(1f, 0.85f, 0.45f), new Color(1.8f, 1.2f, 0.35f));
            var litGreen = Mat("Manor_WindowGreen", new Color(0.6f, 1f, 0.55f), new Color(0.4f, 1.6f, 0.55f));
            var litPurple = Mat("Manor_WindowPurple", new Color(0.8f, 0.5f, 1f), new Color(1.1f, 0.45f, 1.6f));
            var dark = Mat("Manor_WindowDark", new Color(0.03f, 0.02f, 0.05f), null, false, 0.8f);
            var frame = Mat("Manor_Frame", new Color(0.07f, 0.05f, 0.08f));
            Material[] glass = { lit, lit, litGreen, dark, litPurple, lit, dark };

            float[] heights = { 4.8f, 3.5f, 3.5f, 3.3f, 3.1f };
            WindowShape[][] floorShapes =
            {
                new[] { WindowShape.Arch, WindowShape.Rect },
                new[] { WindowShape.Round, WindowShape.Twin, WindowShape.Diamond },
                new[] { WindowShape.Arch, WindowShape.Oval, WindowShape.Triangle },
                new[] { WindowShape.Diamond, WindowShape.Star, WindowShape.Round },
                new[] { WindowShape.Oval, WindowShape.Arch, WindowShape.Triangle },
            };
            float y = 0f, leanX = 0f;
            float w = 0f, d = 0f;
            Transform top = manor;
            for (int i = 0; i < heights.Length; i++)
            {
                float h = heights[i];
                w = 11f * (1f + 0.1f * i);  // 上の階ほど太い
                d = 8f * (1f + 0.07f * i);
                if (i > 0) leanX += Random.Range(-0.45f, 0.45f);
                var fl = Group(manor, $"Floor_{i}");
                fl.localPosition = new Vector3(leanX, y, 0f);
                fl.localRotation = Quaternion.Euler(Random.Range(-1f, 1f), i == 0 ? 0f : Random.Range(-3.5f, 3.5f), i == 0 ? 0f : Random.Range(-1.3f, 1.3f));
                Prim(PrimitiveType.Cube, "Body", fl, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d), i % 2 == 0 ? wall : wallDark, false);
                // 張り出した軒の帯と、その下に並ぶ持ち送り
                Prim(PrimitiveType.Cube, "Cornice", fl, new Vector3(0f, h - 0.12f, 0f), new Vector3(w + 0.6f, 0.26f, d + 0.6f), trim, false);
                Prim(PrimitiveType.Cube, "CorniceGold", fl, new Vector3(0f, h - 0.27f, -d / 2f - 0.27f), new Vector3(w + 0.5f, 0.05f, 0.04f), gold, false);
                for (float x = -w / 2f + 0.6f; x <= w / 2f - 0.5f; x += 1.5f)
                    Prim(PrimitiveType.Cube, "Corbel", fl, new Vector3(x, h - 0.4f, -d / 2f - 0.12f), new Vector3(0.18f, 0.3f, 0.24f), trim, false);
                // 角の柱 (飾り)
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Quoin", fl, new Vector3(s * (w / 2f + 0.05f), h / 2f, -d / 2f - 0.05f), new Vector3(0.35f, h, 0.35f), trim, false);

                // 正面の窓 (1階は真ん中を玄関にあける)
                var shapes = floorShapes[i];
                int count = Mathf.Max(3, Mathf.FloorToInt(w / 2.3f));
                float spacing = (w - 1.6f) / (count - 1);
                for (int k = 0; k < count; k++)
                {
                    float x = -w / 2f + 0.8f + k * spacing;
                    if (i == 0 && Mathf.Abs(x) < 2.4f) continue; // 玄関
                    if (i == 1 && Mathf.Abs(x) < 1.6f) continue; // バラ窓
                    float wy = i == 0 ? 2.6f : h * 0.5f;
                    var shape = shapes[(k + i) % shapes.Length];
                    ManorWindow(fl, new Vector3(x + Random.Range(-0.12f, 0.12f), wy + Random.Range(-0.15f, 0.15f), -d / 2f), shape, glass[Random.Range(0, glass.Length)], frame, trim, Random.Range(-6f, 6f));
                }
                // 横の窓 (斜めから見える)
                foreach (float s in new[] { -1f, 1f })
                {
                    var side = Group(fl, "Side");
                    side.localPosition = new Vector3(s * w / 2f, 0f, 0f);
                    side.localRotation = Quaternion.Euler(0f, -s * 90f, 0f);
                    for (int k = 0; k < 2; k++)
                        ManorWindow(side, new Vector3((k - 0.5f) * d * 0.5f, i == 0 ? 2.6f : h * 0.5f, 0f), shapes[(k + 1) % shapes.Length], glass[Random.Range(0, glass.Length)], frame, trim, Random.Range(-5f, 5f));
                }
                if (i == 0) BuildManorEntrance(fl, d, h, trim, gold, frame, lit);
                if (i == 1) RoseWindow(fl, new Vector3(0f, h * 0.52f, -d / 2f - 0.02f), 1.25f, lit, litPurple, trim, frame);
                if (i == 2) Balcony(fl, d, trim, frame);
                y += h;
                top = fl;
            }

            // 両脇の塔 (外へ傾いて、上ほど太い)。とんがり帽子の屋根
            foreach (float s in new[] { -1f, 1f })
            {
                var tower = Group(manor, "Tower");
                tower.localPosition = new Vector3(s * 6.8f, 0f, -3.6f);
                tower.localRotation = Quaternion.Euler(0f, 0f, -s * 5f); // 外へ傾く (上の太い階に半分めり込みながら)
                float ty = 0f, tr = 1.3f;
                for (int k = 0; k < 6; k++)
                {
                    float th = 3.2f;
                    Prim(PrimitiveType.Cylinder, "TowerBody", tower, new Vector3(0f, ty + th / 2f, 0f), new Vector3(tr * 2f, th / 2f, tr * 2f), k % 2 == 0 ? wallDark : wall, false);
                    Prim(PrimitiveType.Cylinder, "TowerBand", tower, new Vector3(0f, ty + th, 0f), new Vector3(tr * 2f + 0.3f, 0.1f, tr * 2f + 0.3f), trim, false);
                    if (k > 0) ManorWindow(tower, new Vector3(0f, ty + th * 0.5f, -tr), k % 2 == 0 ? WindowShape.Arch : WindowShape.Round, glass[Random.Range(0, glass.Length)], frame, trim, 0f, 0.6f);
                    ty += th;
                    tr *= 1.07f;
                }
                WitchHatRoof(tower, new Vector3(0f, ty, 0f), tr + 0.35f, 7.5f, roof, gold, s);
            }

            // 屋上: マンサード屋根・屋根窓・曲がった煙突・時計塔
            var attic = Group(top, "Attic");
            attic.localPosition = new Vector3(0f, heights[heights.Length - 1], 0f);
            float aw = w, ad = d;
            for (int k = 0; k < 3; k++)
            {
                aw *= 0.86f;
                ad *= 0.82f;
                var tier = Prim(PrimitiveType.Cube, "Mansard", attic, new Vector3(0f, 0.6f + k * 1.2f, 0f), new Vector3(aw, 1.2f, ad), roof, false);
                tier.transform.localRotation = Quaternion.Euler(0f, Random.Range(-2f, 2f), Random.Range(-1.5f, 1.5f));
                Prim(PrimitiveType.Cube, "MansardTrim", attic, new Vector3(0f, 1.2f + k * 1.2f, 0f), new Vector3(aw + 0.2f, 0.1f, ad + 0.2f), trim, false);
            }
            for (int k = -1; k <= 1; k++) // 屋根窓
            {
                var dormer = Group(attic, "Dormer");
                dormer.localPosition = new Vector3(k * aw * 0.38f, 0.9f, -ad * 0.62f);
                dormer.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
                Prim(PrimitiveType.Cube, "DormerBody", dormer, new Vector3(0f, 0.6f, 0f), new Vector3(1.3f, 1.2f, 1.2f), wallDark, false);
                ShapeObj(dormer, "DormerRoof", Shape.Triangle, new Vector3(0f, 1.55f, -0.62f), new Vector3(1.9f, 1.1f, 60f), roof, 0f);
                ManorWindow(dormer, new Vector3(0f, 0.6f, -0.6f), WindowShape.Round, glass[Random.Range(0, glass.Length)], frame, trim, 0f, 0.7f);
            }
            foreach (float cx in new[] { -0.36f, 0.12f, 0.4f }) // 曲がった煙突
            {
                var chimney = Group(attic, "Chimney");
                chimney.localPosition = new Vector3(cx * aw, 2.4f, ad * 0.2f);
                Vector3 cp = Vector3.zero;
                for (int k = 0; k < 3; k++)
                {
                    var seg = Prim(PrimitiveType.Cube, "ChimneySeg", chimney, cp + Vector3.up * 0.6f, new Vector3(0.7f, 1.2f, 0.7f), Mat("Manor_Brick", new Color(0.32f, 0.14f, 0.14f)), false);
                    seg.transform.localRotation = Quaternion.Euler(0f, Random.Range(-10f, 10f), Random.Range(-12f, 12f));
                    cp += new Vector3(Random.Range(-0.25f, 0.25f), 1.15f, 0f);
                }
                Prim(PrimitiveType.Cube, "ChimneyCap", chimney, cp + Vector3.up * 0.1f, new Vector3(0.95f, 0.18f, 0.95f), frame, false);
            }
            // 時計塔 (真夜中で止まった時計) と、ひん曲がった尖塔
            var clock = Group(attic, "ClockTower");
            clock.localPosition = new Vector3(0f, 3.6f, -ad * 0.15f);
            clock.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f));
            Prim(PrimitiveType.Cube, "ClockBody", clock, new Vector3(0f, 1.6f, 0f), new Vector3(2.4f, 3.2f, 2.4f), wall, false);
            Prim(PrimitiveType.Cube, "ClockTrim", clock, new Vector3(0f, 3.2f, 0f), new Vector3(2.7f, 0.2f, 2.7f), trim, false);
            Prim(PrimitiveType.Cylinder, "ClockFace", clock, new Vector3(0f, 1.8f, -1.22f), new Vector3(1.7f, 0.03f, 1.7f), Mat("Manor_ClockFace", new Color(0.95f, 0.92f, 0.75f), new Color(0.9f, 0.85f, 0.5f)), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var clockRing = Group(clock, "ClockRing");
            clockRing.localPosition = new Vector3(0f, 1.8f, -1.26f);
            RingSegments(clockRing, "ClockRim", 0.88f, 20, 0.08f, gold);
            Prim(PrimitiveType.Cube, "HandHour", clock, new Vector3(0f, 2.05f, -1.27f), new Vector3(0.07f, 0.5f, 0.02f), frame, false);
            Prim(PrimitiveType.Cube, "HandMinute", clock, new Vector3(0.02f, 2.15f, -1.28f), new Vector3(0.05f, 0.72f, 0.02f), frame, false).transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            WitchHatRoof(clock, new Vector3(0f, 3.3f, 0f), 1.75f, 6f, roof, gold, Random.value < 0.5f ? -1f : 1f);

            // まわりを回るコウモリ
            var bats = Group(manor, "Bats");
            bats.localPosition = new Vector3(0f, y + 6f, 0f);
            var spin = bats.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.up;
            spin.speed = 35f;
            var batMat = Mat("Ghost_Bat", new Color(0.04f, 0.03f, 0.05f));
            for (int k = 0; k < 9; k++)
            {
                float a = k * 40f * Mathf.Deg2Rad;
                var bat = Group(bats, "Bat");
                bat.localPosition = new Vector3(Mathf.Sin(a) * Random.Range(6f, 10f), Random.Range(-3f, 3f), Mathf.Cos(a) * Random.Range(5f, 8f));
                bat.localRotation = Quaternion.Euler(0f, k * 40f + 90f, Random.Range(-15f, 15f));
                Prim(PrimitiveType.Sphere, "BatBody", bat, Vector3.zero, new Vector3(0.25f, 0.3f, 0.2f), batMat, false);
                foreach (float s in new[] { -1f, 1f })
                {
                    ShapeObj(bat, "Wing", Shape.Triangle, new Vector3(s * 0.4f, 0.05f, 0f), new Vector3(0.7f, 0.4f, 2f), batMat, s * 90f + 180f);
                    ShapeObj(bat, "Ear", Shape.Triangle, new Vector3(s * 0.07f, 0.2f, 0f), new Vector3(0.1f, 0.12f, 2f), batMat, 0f);
                }
            }
            return manor;
        }

        /// <summary>窓: 縁 (frame) の上に、形ごとのガラス (glass)。pos は壁の表面。表は -Z。size = 大きさの倍率</summary>
        void ManorWindow(Transform parent, Vector3 pos, WindowShape shape, Material glass, Material frame, Material trim, float rotZ, float size = 1f)
        {
            var win = Group(parent, $"Window_{shape}");
            win.localPosition = pos;
            win.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            win.localScale = Vector3.one * size;
            const float Z = -0.04f;
            switch (shape)
            {
                case WindowShape.Rect: // 縦長の四角と、片方が外れかけた鎧戸
                    Prim(PrimitiveType.Cube, "Frame", win, Vector3.zero, new Vector3(1.0f, 1.6f, 0.06f), frame, false);
                    Prim(PrimitiveType.Cube, "Glass", win, new Vector3(0f, 0f, Z), new Vector3(0.8f, 1.4f, 0.02f), glass, false);
                    Prim(PrimitiveType.Cube, "Mullion", win, new Vector3(0f, 0f, Z * 1.5f), new Vector3(0.06f, 1.4f, 0.02f), frame, false);
                    Prim(PrimitiveType.Cube, "Sill", win, new Vector3(0f, -0.85f, -0.1f), new Vector3(1.2f, 0.1f, 0.25f), trim, false);
                    var shutter = Prim(PrimitiveType.Cube, "Shutter", win, new Vector3(-0.72f, -0.08f, -0.08f), new Vector3(0.45f, 1.5f, 0.04f), Mat("Manor_Shutter", new Color(0.18f, 0.25f, 0.2f)), false);
                    shutter.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(8f, 22f));
                    break;
                case WindowShape.Arch: // 上が丸いアーチ
                    Prim(PrimitiveType.Cube, "Frame", win, new Vector3(0f, -0.2f, 0f), new Vector3(1.0f, 1.3f, 0.06f), frame, false);
                    Prim(PrimitiveType.Cylinder, "FrameTop", win, new Vector3(0f, 0.45f, 0f), new Vector3(1.0f, 0.03f, 1.0f), frame, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Cube, "Glass", win, new Vector3(0f, -0.2f, Z), new Vector3(0.8f, 1.15f, 0.02f), glass, false);
                    Prim(PrimitiveType.Cylinder, "GlassTop", win, new Vector3(0f, 0.45f, Z), new Vector3(0.8f, 0.01f, 0.8f), glass, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Cube, "Keystone", win, new Vector3(0f, 0.98f, -0.06f), new Vector3(0.22f, 0.3f, 0.1f), trim, false);
                    break;
                case WindowShape.Round: // 丸窓と十字の桟
                    Prim(PrimitiveType.Cylinder, "Frame", win, Vector3.zero, new Vector3(1.15f, 0.03f, 1.15f), frame, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, "Glass", win, new Vector3(0f, 0f, Z), new Vector3(0.95f, 0.01f, 0.95f), glass, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Prim(PrimitiveType.Cube, "BarH", win, new Vector3(0f, 0f, Z * 1.5f), new Vector3(0.95f, 0.05f, 0.02f), frame, false);
                    Prim(PrimitiveType.Cube, "BarV", win, new Vector3(0f, 0f, Z * 1.5f), new Vector3(0.05f, 0.95f, 0.02f), frame, false);
                    break;
                case WindowShape.Diamond: // ひし形
                    Prim(PrimitiveType.Cube, "Frame", win, Vector3.zero, new Vector3(1.0f, 1.0f, 0.06f), frame, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    Prim(PrimitiveType.Cube, "Glass", win, new Vector3(0f, 0f, Z), new Vector3(0.8f, 0.8f, 0.02f), glass, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
                case WindowShape.Oval: // 縦長の楕円
                    Prim(PrimitiveType.Sphere, "Frame", win, Vector3.zero, new Vector3(0.9f, 1.4f, 0.06f), frame, false);
                    Prim(PrimitiveType.Sphere, "Glass", win, new Vector3(0f, 0f, Z), new Vector3(0.72f, 1.2f, 0.02f), glass, false);
                    break;
                case WindowShape.Triangle: // 三角
                    ShapeObj(win, "Frame", Shape.Triangle, Vector3.zero, new Vector3(1.4f, 1.3f, 3f), frame, 0f);
                    ShapeObj(win, "Glass", Shape.Triangle, new Vector3(0f, -0.04f, Z), new Vector3(1.1f, 1.0f, 1f), glass, 0f);
                    break;
                case WindowShape.Star: // 星形
                    ShapeObj(win, "Frame", Shape.Star, Vector3.zero, new Vector3(1.4f, 1.4f, 3f), frame, 0f);
                    ShapeObj(win, "Glass", Shape.Star, new Vector3(0f, 0f, Z), new Vector3(1.15f, 1.15f, 1f), glass, 0f);
                    break;
                default: // 双子のアーチ
                    foreach (float s in new[] { -0.32f, 0.32f })
                    {
                        Prim(PrimitiveType.Cube, "Frame", win, new Vector3(s, -0.15f, 0f), new Vector3(0.55f, 1.2f, 0.06f), frame, false);
                        Prim(PrimitiveType.Cylinder, "FrameTop", win, new Vector3(s, 0.45f, 0f), new Vector3(0.55f, 0.03f, 0.55f), frame, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        Prim(PrimitiveType.Cube, "Glass", win, new Vector3(s, -0.15f, Z), new Vector3(0.42f, 1.05f, 0.02f), glass, false);
                        Prim(PrimitiveType.Cylinder, "GlassTop", win, new Vector3(s, 0.45f, Z), new Vector3(0.42f, 0.01f, 0.42f), glass, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    }
                    Prim(PrimitiveType.Cube, "Sill", win, new Vector3(0f, -0.8f, -0.1f), new Vector3(1.4f, 0.1f, 0.25f), trim, false);
                    break;
            }
        }

        /// <summary>玄関: 大階段、柱が4本並ぶポーチと三角の破風、半円の明かり窓の付いた大きな両開きの扉、扉の両脇の緑の灯籠</summary>
        void BuildManorEntrance(Transform fl, float d, float h, Material trim, Material gold, Material frame, Material lit)
        {
            float front = -d / 2f;
            var stone = Mat("Manor_Step", new Color(0.33f, 0.31f, 0.34f), null, false, 0.15f);
            const float PorchY = 0.9f;
            for (int k = 0; k < 5; k++) // 大階段 (下ほど広い)
                Prim(PrimitiveType.Cube, "Step", fl, new Vector3(0f, 0.09f * (k + 1), front - 3.1f + k * 0.4f), new Vector3(7f - k * 0.45f, 0.18f * (k + 1), 0.42f), stone, false);
            Prim(PrimitiveType.Cube, "Porch", fl, new Vector3(0f, PorchY / 2f, front - 0.6f), new Vector3(5.6f, PorchY, 1.3f), stone, false);
            // 扉 (両開き)。上に半円の明かり窓
            var doorMat = Mat("Manor_Door", new Color(0.35f, 0.07f, 0.1f), null, false, 0.35f);
            Prim(PrimitiveType.Cube, "DoorFrame", fl, new Vector3(0f, PorchY + 1.35f, front - 0.03f), new Vector3(2.1f, 2.7f, 0.08f), trim, false);
            Prim(PrimitiveType.Cylinder, "Fanlight", fl, new Vector3(0f, PorchY + 2.7f, front - 0.03f), new Vector3(1.9f, 0.02f, 1.9f), lit, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "FanlightFrame", fl, new Vector3(0f, PorchY + 2.7f, front - 0.01f), new Vector3(2.2f, 0.02f, 2.2f), trim, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "DoorLeaf", fl, new Vector3(s * 0.46f, PorchY + 1.3f, front - 0.09f), new Vector3(0.88f, 2.6f, 0.08f), doorMat, false);
                Prim(PrimitiveType.Cube, "DoorPanel", fl, new Vector3(s * 0.46f, PorchY + 1.8f, front - 0.14f), new Vector3(0.6f, 0.8f, 0.02f), Mat("Manor_DoorPanel", new Color(0.25f, 0.04f, 0.07f)), false);
                Prim(PrimitiveType.Cube, "DoorPanel", fl, new Vector3(s * 0.46f, PorchY + 0.7f, front - 0.14f), new Vector3(0.6f, 0.8f, 0.02f), Mat("Manor_DoorPanel", new Color(0.25f, 0.04f, 0.07f)), false);
                Prim(PrimitiveType.Sphere, "Knob", fl, new Vector3(s * 0.12f, PorchY + 1.25f, front - 0.16f), Vector3.one * 0.1f, gold, false);
                // 柱 (台と柱頭付き)
                foreach (float cx in new[] { 1.55f, 2.55f })
                {
                    float x = s * cx;
                    Prim(PrimitiveType.Cylinder, "Column", fl, new Vector3(x, PorchY + 1.65f, front - 1.05f), new Vector3(0.42f, 1.65f, 0.42f), Mat("Manor_Column", new Color(0.55f, 0.5f, 0.52f), null, false, 0.3f), false);
                    Prim(PrimitiveType.Cube, "ColumnBase", fl, new Vector3(x, PorchY + 0.1f, front - 1.05f), new Vector3(0.6f, 0.2f, 0.6f), trim, false);
                    Prim(PrimitiveType.Cube, "Capital", fl, new Vector3(x, PorchY + 3.35f, front - 1.05f), new Vector3(0.62f, 0.2f, 0.62f), trim, false);
                }
                // 緑の灯籠
                Prim(PrimitiveType.Cube, "LanternCap", fl, new Vector3(s * 1.05f, PorchY + 2.35f, front - 0.2f), new Vector3(0.3f, 0.06f, 0.3f), frame, false);
                Prim(PrimitiveType.Sphere, "Lantern", fl, new Vector3(s * 1.05f, PorchY + 2.15f, front - 0.2f), new Vector3(0.22f, 0.32f, 0.22f), Mat("Manor_GreenFlame", new Color(0.5f, 1f, 0.5f), new Color(0.6f, 2.4f, 0.7f)), false);
            }
            // ポーチの屋根と三角の破風
            Prim(PrimitiveType.Cube, "PorchRoof", fl, new Vector3(0f, PorchY + 3.6f, front - 0.9f), new Vector3(6.6f, 0.3f, 2f), trim, false);
            ShapeObj(fl, "Pediment", Shape.Triangle, new Vector3(0f, PorchY + 4.2f, front - 1.85f), new Vector3(7.2f, 1.6f, 10f), Mat("Manor_Roof", new Color(0.12f, 0.08f, 0.17f), null, false, 0.3f), 0f);
            ShapeObj(fl, "PedimentStar", Shape.Star, new Vector3(0f, PorchY + 4.1f, front - 1.97f), new Vector3(0.7f, 0.7f, 1f), gold, 0f);
            MakeText(fl, "ManorName", "HAUNTED MANOR", new Vector3(0f, PorchY + 3.6f, front - 1.92f), 0.28f, new Color(1f, 0.8f, 0.35f));
        }

        // 丸いバラ窓: 光る円盤に、放射状の桟と2重の輪
        void RoseWindow(Transform fl, Vector3 pos, float r, Material glass, Material glass2, Material trim, Material frame)
        {
            var rose = Group(fl, "RoseWindow");
            rose.localPosition = pos;
            Prim(PrimitiveType.Cylinder, "Glass", rose, new Vector3(0f, 0f, -0.02f), new Vector3(r * 2f, 0.02f, r * 2f), glass, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "Inner", rose, new Vector3(0f, 0f, -0.04f), new Vector3(r * 0.8f, 0.02f, r * 0.8f), glass2, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int k = 0; k < 8; k++)
                Prim(PrimitiveType.Cube, "Spoke", rose, new Vector3(0f, 0f, -0.06f), new Vector3(0.06f, r * 2f, 0.02f), frame, false).transform.localRotation = Quaternion.Euler(0f, 0f, k * 22.5f);
            RingSegments(rose, "RoseRing", r, 24, 0.14f, trim, null, -0.07f);
            RingSegments(rose, "RoseRingIn", r * 0.42f, 12, 0.08f, frame, null, -0.07f);
        }

        // 3階のバルコニー (床と手すり)
        void Balcony(Transform fl, float d, Material trim, Material frame)
        {
            float front = -d / 2f;
            Prim(PrimitiveType.Cube, "BalconyFloor", fl, new Vector3(0f, 0.15f, front - 0.6f), new Vector3(4f, 0.18f, 1.2f), trim, false);
            Prim(PrimitiveType.Cube, "Rail", fl, new Vector3(0f, 1.05f, front - 1.15f), new Vector3(4f, 0.08f, 0.08f), frame, false);
            for (int k = 0; k < 11; k++)
                Prim(PrimitiveType.Cube, "Baluster", fl, new Vector3(-1.9f + k * 0.38f, 0.6f, front - 1.15f), new Vector3(0.05f, 0.85f, 0.05f), frame, false);
        }

        // とんがり帽子の屋根: 少しずつ細く、少しずつ横へずれていく (先が曲がる)。てっぺんに金の玉
        void WitchHatRoof(Transform parent, Vector3 basePos, float r, float h, Material roof, Material gold, float bend)
        {
            const int Rings = 8;
            Vector3 p = basePos;
            Prim(PrimitiveType.Cylinder, "Eave", parent, p + Vector3.up * 0.05f, new Vector3(r * 2.3f, 0.05f, r * 2.3f), roof, false);
            for (int k = 0; k < Rings; k++)
            {
                float t = k / (float)Rings;
                float rr = r * (1f - t) + 0.08f;
                Prim(PrimitiveType.Cylinder, "RoofRing", parent, p + Vector3.up * (h / Rings / 2f), new Vector3(rr * 2f, h / Rings / 2f, rr * 2f), roof, false);
                p += new Vector3(bend * t * t * 0.35f, h / Rings, 0f);
            }
            Prim(PrimitiveType.Cylinder, "Spire", parent, p + Vector3.up * 0.5f, new Vector3(0.06f, 0.5f, 0.06f), gold, false);
            Prim(PrimitiveType.Sphere, "SpireBall", parent, p + Vector3.up * 1.05f, Vector3.one * 0.22f, gold, false);
        }

        // ------------------------------------------------------------------
        // 空 (月・雲・星)
        // ------------------------------------------------------------------

        Transform BuildGhostSky(Transform parent)
        {
            var sky = Group(parent, "GhostSky");
            // 月 (お化け屋敷の左上の、ずっと遠く。大きく、まわりに光のかさ。霧で少し紫にかすむ)
            const float MoonD = 26f; // 直径 (見かけの大きさは前の約2倍)
            var moonPos = GhostMoonPos;
            var moon = Group(sky, "Moon");
            moon.localPosition = moonPos;
            moon.localRotation = Quaternion.LookRotation(moonPos);
            Prim(PrimitiveType.Sphere, "MoonBody", moon, Vector3.zero, Vector3.one * MoonD, Mat("Ghost_Moon", new Color(1f, 0.96f, 0.8f), new Color(2.6f, 2.4f, 1.7f)), false);
            var crater = Mat("Ghost_MoonCrater", new Color(0.85f, 0.82f, 0.68f), new Color(1.7f, 1.55f, 1.05f));
            float ck = MoonD / 12f; // (クレーターの位置と大きさを、月の大きさに合わせる)
            Vector2[] craters = { new Vector2(-2f, 1.5f), new Vector2(1.8f, 2.2f), new Vector2(0.6f, -1.8f), new Vector2(-1.2f, -0.6f), new Vector2(2.6f, -0.8f) };
            foreach (var c0 in craters)
            {
                var c = c0 * ck;
                float rr = MoonD / 2f;
                float z = -Mathf.Sqrt(Mathf.Max(0f, rr * rr - c.sqrMagnitude)) + 0.25f * ck;
                Prim(PrimitiveType.Sphere, "Crater", moon, new Vector3(c.x, c.y, z), new Vector3(1.6f, 1.6f, 0.5f) * ck * Random.Range(0.6f, 1.2f), crater, false);
            }
            Prim(PrimitiveType.Sphere, "MoonHalo", moon, Vector3.zero, Vector3.one * MoonD * 1.7f, TransparentMat("Ghost_MoonHalo", new Color(0.9f, 0.85f, 1f, 0.14f)), false);
            Prim(PrimitiveType.Sphere, "MoonHalo2", moon, Vector3.zero, Vector3.one * MoonD * 2.6f, TransparentMat("Ghost_MoonHalo2", new Color(0.75f, 0.6f, 1f, 0.07f)), false);
            // ゆっくり流れる紫の雲
            var clouds = Group(sky, "Clouds");
            var spin = clouds.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.up;
            spin.speed = 1.2f;
            var cloudMat = TransparentMat("Ghost_Cloud", new Color(0.33f, 0.2f, 0.45f, 0.55f));
            for (int i = 0; i < 9; i++)
            {
                float a = (i * 40f + Random.Range(-10f, 10f)) * Mathf.Deg2Rad, r = Random.Range(50f, 70f);
                var c = Group(clouds, "Cloud");
                c.localPosition = new Vector3(Mathf.Sin(a) * r, Random.Range(26f, 42f), Mathf.Cos(a) * r);
                c.localRotation = Quaternion.LookRotation(c.localPosition);
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Sphere, "Puff", c, new Vector3((k - 1) * 6f, Random.Range(-1f, 1f), 0f), new Vector3(Random.Range(10f, 15f), Random.Range(2.5f, 4f), 6f), cloudMat, false);
            }
            // まばらな星
            var star = Mat("Space_Sparkle_White", Color.white, null, true);
            for (int i = 0; i < 40; i++)
            {
                var dir = Quaternion.Euler(Random.Range(-75f, -20f), Random.Range(0f, 360f), 0f) * Vector3.forward;
                Prim(PrimitiveType.Sphere, "Star", sky, dir * 80f, Vector3.one * Random.Range(0.25f, 0.5f), star, false);
            }
            return sky;
        }

        // ------------------------------------------------------------------
        // 的: お化け・魔女・ゾンビ・骸骨 (パネル。表 -Z がプレイヤー向き、点数の札付き)
        // ------------------------------------------------------------------

        /// <summary>的の共通部分: 根元 (FaceCenter) → ゆれる部分 → 当たり判定 (縮む見た目) → 反転する絵 + 点数の札</summary>
        GhostSpook SpookShell(Transform parent, string name, Vector3 signPos, float signSize, out Transform figure, out Target target)
        {
            var root = Group(parent, name);
            root.gameObject.SetActive(false);
            root.gameObject.AddComponent<FaceCenter>();
            var body = Group(root, "Body");
            var mount = Group(body, "Mount");
            var visual = Group(mount, "Visual");
            figure = Group(visual, "Figure");
            var label = SpookSign(visual, signPos, signSize);

            target = mount.gameObject.AddComponent<Target>();
            target.kind = TargetKind.GhostSpook;
            target.manualControl = true;
            target.shrinkOnHit = true; // 空中に浮いているので、倒れずにその場で縮む
            target.visual = visual;
            target.SetAvailable(false);

            var spook = root.gameObject.AddComponent<GhostSpook>();
            spook.body = body;
            spook.flip = figure;
            spook.hitTarget = target;
            spook.pointLabels = new[] { label };
            return spook;
        }

        // 点数の札: 黒っぽい紫の板に、オレンジに光る縁と、明るい黄色の大きな数字 (暗い中でもよく見える)
        TextMesh SpookSign(Transform parent, Vector3 pos, float size)
        {
            var sign = Group(parent, "PointSign");
            sign.localPosition = pos;
            Prim(PrimitiveType.Cube, "Board", sign, Vector3.zero, new Vector3(size * 1.9f, size * 0.85f, 0.05f), Mat("Ghost_SignBoard", new Color(0.08f, 0.04f, 0.12f)));
            Prim(PrimitiveType.Cube, "Glow", sign, new Vector3(0f, 0f, 0.02f), new Vector3(size * 2.05f, size * 1.0f, 0.03f), Mat("Ghost_SignGlow", new Color(1f, 0.5f, 0.1f), new Color(1.6f, 0.6f, 0.1f)), false);
            return MakeText(sign, "Points", "100", new Vector3(0f, 0f, -0.035f), size * 0.62f, new Color(1f, 0.95f, 0.45f));
        }

        /// <summary>お化け: 白いシーツのお化け (丸い頭・波打つすそ・ばんざいした手・黒い目と「お」の口・横に流れるしっぽ)。左 (-X) へ進む向き</summary>
        GhostSpook BuildGhostSpook(Transform parent, int index, Material puff)
        {
            var spook = SpookShell(parent, $"Ghost_{index}", new Vector3(0f, -0.75f, 0f), 0.42f, out var fig, out _);
            var white = Mat("Ghost_Sheet", new Color(0.95f, 0.95f, 1f), new Color(0.22f, 0.22f, 0.32f));
            var black = Mat("Ghost_Black", new Color(0.03f, 0.02f, 0.05f));
            const float T = 0.14f;
            Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 0.95f, T), white);
            Prim(PrimitiveType.Cube, "Sheet", fig, new Vector3(0f, 0.08f, 0f), new Vector3(0.95f, 0.8f, T), white);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Sphere, "Hem", fig, new Vector3(-0.36f + k * 0.24f, -0.32f - (k % 2) * 0.06f, 0f), new Vector3(0.28f, 0.32f, T), white, false);
            Prim(PrimitiveType.Sphere, "Tail", fig, new Vector3(0.55f, -0.3f, 0f), new Vector3(0.45f, 0.2f, T * 0.8f), white, false).transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Capsule, "Arm", fig, new Vector3(s * 0.55f, 0.28f, 0f), new Vector3(0.2f, 0.25f, T * 0.8f), white, false).transform.localRotation = Quaternion.Euler(0f, 0f, -s * 55f);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, "Eye", fig, new Vector3(s * 0.17f - 0.06f, 0.58f, -T / 2f - 0.01f), new Vector3(0.14f, 0.22f, 0.04f), black, false);
                Prim(PrimitiveType.Sphere, "Cheek", fig, new Vector3(s * 0.28f - 0.06f, 0.38f, -T / 2f - 0.005f), new Vector3(0.12f, 0.07f, 0.02f), Mat("Ghost_Cheek", new Color(1f, 0.55f, 0.7f)), false);
            }
            Prim(PrimitiveType.Sphere, "Mouth", fig, new Vector3(-0.06f, 0.3f, -T / 2f - 0.01f), new Vector3(0.18f, 0.24f, 0.04f), black, false);
            spook.kind = GhostSpook.Kind.Ghost;
            spook.displayName = "GHOST";
            spook.pointChoices = new[] { 100, 200, 300 };
            spook.orbitRadius = new Vector2(4f, 15f);        // 近くから遠くまで、ばらばらの距離
            spook.orbitHeight = new Vector2(1.3f, 4f);
            spook.orbitLinearSpeed = new Vector2(1f, 2f);    // 距離を保ったまま、横へゆったり流れる (m/秒)
            spook.orbitLife = new Vector2(8f, 11f);          // (照らす時間がいるので、少し長めに出ている)
            spook.puffMaterial = puff;
            return spook;
        }

        /// <summary>魔女: ほうきにまたがり、黒いとんがり帽子・紫のマントの魔女 (緑の顔と長い鼻)。左 (-X) へ飛ぶ向き。高い点数</summary>
        GhostSpook BuildWitchSpook(Transform parent, int index, Material puff)
        {
            var spook = SpookShell(parent, $"Witch_{index}", new Vector3(0f, -0.6f, 0f), 0.48f, out var fig, out _);
            var broom = Mat("Ghost_Broom", new Color(0.45f, 0.3f, 0.15f));
            var straw = Mat("Ghost_Straw", new Color(0.9f, 0.75f, 0.35f));
            var dress = Mat("Ghost_WitchDress", new Color(0.1f, 0.05f, 0.14f));
            var cape = Mat("Ghost_WitchCape", new Color(0.45f, 0.12f, 0.6f), new Color(0.1f, 0.02f, 0.15f));
            var skin = Mat("Ghost_WitchSkin", new Color(0.45f, 0.8f, 0.35f));
            const float T = 0.12f;
            // ほうき (柄と、後ろのわら)
            Prim(PrimitiveType.Cylinder, "Stick", fig, new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.85f, 0.07f), broom).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int k = -1; k <= 1; k++)
                Prim(PrimitiveType.Cube, "Bristle", fig, new Vector3(0.95f, k * 0.06f, 0f), new Vector3(0.45f, 0.1f, T), straw, false).transform.localRotation = Quaternion.Euler(0f, 0f, k * 14f);
            Prim(PrimitiveType.Cube, "BroomTie", fig, new Vector3(0.74f, 0f, 0f), new Vector3(0.06f, 0.16f, T * 1.1f), Mat("Ghost_Rope", new Color(0.55f, 0.45f, 0.3f)), false);
            // 体 (またがって座る)・マント
            Prim(PrimitiveType.Cube, "Dress", fig, new Vector3(0.05f, 0.32f, 0f), new Vector3(0.42f, 0.6f, T), dress);
            Prim(PrimitiveType.Cube, "Skirt", fig, new Vector3(0.12f, 0.02f, 0f), new Vector3(0.55f, 0.18f, T), dress, false).transform.localRotation = Quaternion.Euler(0f, 0f, 10f);
            Prim(PrimitiveType.Cube, "Cape", fig, new Vector3(0.42f, 0.35f, 0.02f), new Vector3(0.55f, 0.65f, T * 0.5f), cape, false).transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
            Prim(PrimitiveType.Cube, "Boot", fig, new Vector3(-0.12f, -0.22f, -0.02f), new Vector3(0.12f, 0.28f, T), dress, false).transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            Prim(PrimitiveType.Sphere, "Hand", fig, new Vector3(-0.28f, 0.05f, -0.03f), new Vector3(0.12f, 0.12f, T), skin, false);
            Prim(PrimitiveType.Cube, "Arm", fig, new Vector3(-0.15f, 0.2f, -0.02f), new Vector3(0.1f, 0.32f, T), dress, false).transform.localRotation = Quaternion.Euler(0f, 0f, -50f);
            // 頭・長い鼻・帽子
            Prim(PrimitiveType.Sphere, "Head", fig, new Vector3(-0.05f, 0.78f, 0f), new Vector3(0.34f, 0.36f, T), skin);
            Prim(PrimitiveType.Capsule, "Nose", fig, new Vector3(-0.25f, 0.76f, 0f), new Vector3(0.07f, 0.1f, 0.07f), skin, false).transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            Prim(PrimitiveType.Sphere, "Eye", fig, new Vector3(-0.13f, 0.84f, -T / 2f - 0.01f), new Vector3(0.07f, 0.07f, 0.02f), Mat("Ghost_WitchEye", new Color(1f, 0.9f, 0.2f), new Color(1f, 0.8f, 0.1f)), false);
            Prim(PrimitiveType.Cube, "Hair", fig, new Vector3(0.14f, 0.68f, 0.01f), new Vector3(0.18f, 0.42f, T * 0.9f), Mat("Ghost_WitchHair", new Color(0.55f, 0.15f, 0.12f)), false).transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            Prim(PrimitiveType.Cube, "HatBrim", fig, new Vector3(-0.03f, 0.95f, 0f), new Vector3(0.62f, 0.05f, T), dress, false).transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            var hat = Group(fig, "Hat");
            hat.localPosition = new Vector3(-0.02f, 0.97f, 0f);
            hat.localRotation = Quaternion.Euler(0f, 0f, -22f);
            Prim(PrimitiveType.Cube, "HatBand", hat, new Vector3(0f, 0.05f, 0f), new Vector3(0.33f, 0.06f, T * 1.05f), cape, false);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, "HatCone", hat, new Vector3(k * 0.03f, 0.1f + k * 0.12f, 0f), new Vector3(0.32f - k * 0.075f, 0.13f, T), dress, false);
            spook.kind = GhostSpook.Kind.Witch;
            spook.displayName = "WITCH";
            spook.pointChoices = new[] { 500, 1000 };
            spook.orbitRadius = new Vector2(8f, 20f);
            spook.orbitHeight = new Vector2(3.5f, 8f);
            spook.orbitLinearSpeed = new Vector2(2.5f, 4f); // 魔女はお化けより速く飛ぶ (m/秒)
            spook.orbitLife = new Vector2(7f, 9f);
            spook.bobHeight = 0.5f;
            spook.puffMaterial = puff;
            spook.puffSize = 1.2f;
            spook.transform.localScale = Vector3.one * 1.35f; // 遠くを飛ぶので大きめ
            return spook;
        }

        /// <summary>ゾンビ: 緑の肌、ぼろぼろの服、前へ伸ばした両手、片目が飛び出た顔と縫い目。お墓の前の土から出てくる (原点が足元)</summary>
        GhostSpook BuildZombieSpook(Transform parent, int index, Material puff)
        {
            var spook = SpookShell(parent, $"Zombie_{index}", new Vector3(0f, 2.2f, 0f), 0.42f, out var fig, out _);
            var skin = Mat("Ghost_ZombieSkin", new Color(0.45f, 0.62f, 0.35f));
            Color[] shirts = { new Color(0.35f, 0.3f, 0.22f), new Color(0.3f, 0.2f, 0.28f), new Color(0.22f, 0.3f, 0.35f) };
            var shirt = Mat($"Ghost_ZombieShirt_{index % shirts.Length}", shirts[index % shirts.Length]);
            var pants = Mat("Ghost_ZombiePants", new Color(0.2f, 0.22f, 0.3f));
            var black = Mat("Ghost_Black", new Color(0.03f, 0.02f, 0.05f));
            const float T = 0.14f;
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Leg", fig, new Vector3(s * 0.13f, 0.38f, 0f), new Vector3(0.18f, 0.76f, T), pants);
            Prim(PrimitiveType.Cube, "Torso", fig, new Vector3(0f, 1.05f, 0f), new Vector3(0.56f, 0.66f, T), shirt);
            for (int k = 0; k < 3; k++) // ぼろぼろのすそ
                Prim(PrimitiveType.Cube, "Rag", fig, new Vector3(-0.18f + k * 0.18f, 0.68f, 0f), new Vector3(0.14f, 0.16f, T * 0.9f), shirt, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Cube, "Hole", fig, new Vector3(0.12f, 1.1f, -T / 2f - 0.005f), new Vector3(0.12f, 0.1f, 0.01f), skin, false);
            foreach (float s in new[] { -1f, 1f }) // 前へ伸ばした手 (パネルなので横へ突き出して、少し上へ)
            {
                var arm = Prim(PrimitiveType.Cube, "Arm", fig, new Vector3(s * 0.44f, 1.25f, 0f), new Vector3(0.14f, 0.5f, T), shirt);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 70f);
                Prim(PrimitiveType.Sphere, "Hand", fig, new Vector3(s * 0.68f, 1.36f, 0f), new Vector3(0.16f, 0.13f, T), skin, false);
            }
            Prim(PrimitiveType.Cube, "Head", fig, new Vector3(0.02f, 1.6f, 0f), new Vector3(0.4f, 0.44f, T), skin).transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            Prim(PrimitiveType.Sphere, "EyeBig", fig, new Vector3(-0.08f, 1.65f, -T / 2f - 0.02f), new Vector3(0.14f, 0.14f, 0.04f), Mat("Ghost_EyeWhite", Color.white), false);
            Prim(PrimitiveType.Sphere, "Pupil", fig, new Vector3(-0.09f, 1.64f, -T / 2f - 0.04f), new Vector3(0.05f, 0.05f, 0.02f), black, false);
            Prim(PrimitiveType.Cube, "EyeX1", fig, new Vector3(0.11f, 1.66f, -T / 2f - 0.01f), new Vector3(0.12f, 0.025f, 0.01f), black, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Cube, "EyeX2", fig, new Vector3(0.11f, 1.66f, -T / 2f - 0.01f), new Vector3(0.12f, 0.025f, 0.01f), black, false).transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            Prim(PrimitiveType.Cube, "Mouth", fig, new Vector3(0.03f, 1.48f, -T / 2f - 0.01f), new Vector3(0.22f, 0.06f, 0.01f), black, false).transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
            Prim(PrimitiveType.Cube, "Stitch", fig, new Vector3(0.05f, 1.76f, -T / 2f - 0.01f), new Vector3(0.24f, 0.02f, 0.01f), black, false);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, "StitchX", fig, new Vector3(-0.06f + k * 0.07f, 1.76f, -T / 2f - 0.015f), new Vector3(0.015f, 0.06f, 0.01f), black, false);
            Prim(PrimitiveType.Cube, "Hair", fig, new Vector3(0.02f, 1.84f, 0f), new Vector3(0.36f, 0.06f, T), Mat("Ghost_ZombieHair", new Color(0.15f, 0.12f, 0.1f)), false);
            spook.kind = GhostSpook.Kind.Zombie;
            spook.displayName = "ZOMBIE";
            spook.pointChoices = new[] { 200, 300 };
            spook.puffMaterial = puff;
            return spook;
        }

        /// <summary>骸骨: 白い骨 (どくろ・背骨・あばら・腰・手足の骨)。片手を上げて「こんにちは」と手を振る。お墓の前の土から出てくる</summary>
        GhostSpook BuildSkeletonSpook(Transform parent, int index, Material puff)
        {
            var spook = SpookShell(parent, $"Skeleton_{index}", new Vector3(0f, 2.25f, 0f), 0.42f, out var fig, out _);
            var bone = Mat("Ghost_Bone", new Color(0.93f, 0.91f, 0.84f), new Color(0.12f, 0.12f, 0.1f));
            var black = Mat("Ghost_Black", new Color(0.03f, 0.02f, 0.05f));
            var back = Mat("Ghost_BoneShadow", new Color(0.06f, 0.04f, 0.08f));
            const float T = 0.1f;
            // どくろ
            Prim(PrimitiveType.Sphere, "Skull", fig, new Vector3(0f, 1.66f, 0f), new Vector3(0.42f, 0.44f, T), bone);
            Prim(PrimitiveType.Cube, "Jaw", fig, new Vector3(0f, 1.44f, 0f), new Vector3(0.26f, 0.12f, T), bone, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Sphere, "Socket", fig, new Vector3(s * 0.1f, 1.68f, -T / 2f - 0.01f), new Vector3(0.13f, 0.14f, 0.03f), black, false);
            ShapeObj(fig, "Nose", Shape.Triangle, new Vector3(0f, 1.57f, -T / 2f - 0.01f), new Vector3(0.07f, 0.07f, 1f), black, 180f);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, "Tooth", fig, new Vector3(-0.075f + k * 0.05f, 1.46f, -T / 2f - 0.01f), new Vector3(0.012f, 0.07f, 0.01f), black, false);
            // 背骨・あばら (後ろに黒い影の板。当たり判定にもなる)
            Prim(PrimitiveType.Cube, "RibShadow", fig, new Vector3(0f, 1.05f, 0.03f), new Vector3(0.5f, 0.6f, 0.04f), back);
            for (int k = 0; k < 6; k++)
                Prim(PrimitiveType.Cube, "Spine", fig, new Vector3(0f, 0.72f + k * 0.11f, 0f), new Vector3(0.07f, 0.08f, T), bone, false);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, "Rib", fig, new Vector3(0f, 1.28f - k * 0.12f, -0.01f), new Vector3(0.48f - k * 0.06f, 0.05f, T * 0.8f), bone, false);
            Prim(PrimitiveType.Cube, "Pelvis", fig, new Vector3(0f, 0.68f, 0f), new Vector3(0.38f, 0.14f, T), bone);
            Prim(PrimitiveType.Cube, "Collar", fig, new Vector3(0f, 1.36f, 0f), new Vector3(0.56f, 0.05f, T), bone, false);
            // 脚
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Femur", fig, new Vector3(s * 0.12f, 0.45f, 0f), new Vector3(0.06f, 0.36f, T), bone, false);
                Prim(PrimitiveType.Sphere, "Knee", fig, new Vector3(s * 0.12f, 0.26f, 0f), new Vector3(0.09f, 0.09f, T), bone, false);
                Prim(PrimitiveType.Cube, "Shin", fig, new Vector3(s * 0.12f, 0.1f, 0f), new Vector3(0.05f, 0.3f, T), bone, false);
            }
            // 腕: 片手は下、片手は上げて手を振る
            Prim(PrimitiveType.Cube, "ArmDown", fig, new Vector3(-0.32f, 1.05f, 0f), new Vector3(0.05f, 0.55f, T), bone, false).transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var wave = Group(fig, "WaveArm");
            wave.localPosition = new Vector3(0.28f, 1.32f, 0f);
            var waver = wave.gameObject.AddComponent<Pendulum>();
            waver.amplitude = 22f; // 手を振る
            waver.speed = 7f;
            waver.phase = index;
            Prim(PrimitiveType.Cube, "UpperArm", wave, new Vector3(0.12f, 0.14f, 0f), new Vector3(0.05f, 0.34f, T), bone, false).transform.localRotation = Quaternion.Euler(0f, 0f, -40f);
            Prim(PrimitiveType.Cube, "Forearm", wave, new Vector3(0.24f, 0.42f, 0f), new Vector3(0.05f, 0.3f, T), bone, false);
            Prim(PrimitiveType.Sphere, "HandBone", wave, new Vector3(0.24f, 0.6f, 0f), new Vector3(0.12f, 0.12f, T), bone, false);
            spook.kind = GhostSpook.Kind.Skeleton;
            spook.displayName = "SKELETON";
            spook.pointChoices = new[] { 300, 500 };
            spook.puffMaterial = puff;
            return spook;
        }

        /// <summary>お化けの的: 月夜の洋館と大きなお化け (LookArt の絵のステッカー。はみ出して大胆に)</summary>
        GameObject GhostLook(Transform look) => StickerLook(look, "Ghost", "GHOST", Color.white);
    }
}
