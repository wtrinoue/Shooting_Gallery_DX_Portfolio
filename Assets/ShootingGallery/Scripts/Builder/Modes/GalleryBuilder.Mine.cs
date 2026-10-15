using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 発掘モード (つるはしの的を撃つと始まる) の生成: ハッチを覆う土の床と掘られる穴、木の枠で支えた長い縦穴、プレイヤーを乗せて上下する昇降台、
    /// その下の広くていびつな洞窟 (岩の壁と天井・つらら石・石筍・光る結晶・木のやぐら・木の枠のトンネルの穴)、
    /// ぐるぐる回る線路と、穴から穴へ抜ける線路、鉱石や宝石を積んだトロッコ、壁から生えた宝石 (的)、飛び回るコウモリ、つるはしの的の見た目。
    /// プレイヤーの足元 (地上) が原点。洞窟はその真下 MineDepth の所にあり、洞窟のローカル座標ではやぐらの上 (プレイヤーが止まる所) が原点。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float MineDepth = 16f;     // 地上からやぐらの上までの深さ (縦穴 6m + 洞窟の天井からやぐらまで 10m)
        const float CaveFloor = -4f;     // 洞窟の床 (洞窟のローカル。やぐらの上が 0)
        const float CaveCeiling = 10f;   // 縦穴が洞窟の天井に出る高さ (洞窟のローカル)
        const float RailGauge = 0.6f;

        readonly List<Vector3> mineTrackPoints = new List<Vector3>(); // 石筍などを線路の上に置かないように、線路の点を覚えておく
        readonly List<Transform> mineSparkles = new List<Transform>(); // キラッと光る十字の光 (MineCave が脈打たせる)

        /// <summary>キラッと光る十字の光 (いつもプレイヤーの方を向く)。MineCave がときどき大きく光らせる</summary>
        void MineSparkle(Transform parent, Vector3 pos, float size)
        {
            var face = Group(parent, "Sparkle");
            face.localPosition = pos;
            face.gameObject.AddComponent<FaceCenter>();
            var cross = Group(face, "Cross");
            cross.localScale = Vector3.one * size;
            var mat = Mat("Mine_Sparkle", new Color(1f, 0.98f, 0.9f), null, true);
            Prim(PrimitiveType.Cube, "RayH", cross, Vector3.zero, new Vector3(1f, 0.05f, 0.01f), mat, false);
            Prim(PrimitiveType.Cube, "RayV", cross, Vector3.zero, new Vector3(0.05f, 1f, 0.01f), mat, false);
            foreach (float r in new[] { 45f, -45f })
                Prim(PrimitiveType.Cube, "RayD", cross, Vector3.zero, new Vector3(0.45f, 0.035f, 0.01f), mat, false).transform.localRotation = Quaternion.Euler(0f, 0f, r);
            mineSparkles.Add(cross);
        }

        /// <summary>
        /// 洞窟の空気: 天井の穴から差し込む、うっすらした光の筋 / 明かりの中を漂うほこり / 壁の岩に光る金と銀の鉱脈 (ときどきキラッと光る)。
        /// ほこりの Transform を返す (MineCave がゆっくり漂わせる)
        /// </summary>
        List<Transform> BuildCaveAtmosphere(Transform cave)
        {
            var air = Group(cave, "Atmosphere");
            // 天井の穴から差し込む光の筋
            var beam = new GameObject("LightShaft");
            beam.transform.SetParent(air, false);
            beam.transform.localPosition = new Vector3(0f, CaveCeiling, 0f);
            beam.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 先が上、下へ広がる
            beam.transform.localScale = new Vector3(2.6f, 2.6f, CaveCeiling - CaveFloor);
            beam.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            beam.AddComponent<MeshRenderer>().sharedMaterial = TransparentMat("Mine_LightShaft", new Color(0.75f, 0.85f, 1f, 0.05f));
            // 漂うほこり (明かりのまわりに多め)
            var dustMat = TransparentMat("Mine_Dust", new Color(1f, 0.85f, 0.6f, 0.4f));
            var motes = new List<Transform>();
            for (int i = 0; i < 70; i++)
            {
                Vector3 p = i % 3 == 0 ? new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(-3f, CaveCeiling - 1f), Random.Range(-2.5f, 2.5f)) // 光の筋の中
                                       : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(3f, 18f) + Vector3.up * Random.Range(-3f, 5f);
                motes.Add(Prim(PrimitiveType.Sphere, "Dust", air, p, Vector3.one * Random.Range(0.025f, 0.05f), dustMat, false).transform);
            }
            // 壁の鉱脈 (金・銀のかけらが岩に埋まっていて、ときどきキラッと光る)
            var gold = Mat("Ore_Gold", new Color(1f, 0.8f, 0.25f), new Color(0.35f, 0.25f, 0.02f), false, 0.8f);
            var silver = Mat("Ore_Silver", new Color(0.85f, 0.87f, 0.92f), new Color(0.12f, 0.12f, 0.14f), false, 0.85f);
            for (int i = 0; i < 26; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(24f, 27f);
                var vein = Group(air, "OreVein");
                vein.localPosition = new Vector3(Mathf.Sin(a) * r, CaveFloor + Random.Range(0.5f, 9f), Mathf.Cos(a) * r);
                vein.localRotation = Quaternion.LookRotation(-new Vector3(vein.localPosition.x, 0f, vein.localPosition.z));
                var m = i % 3 == 0 ? silver : gold;
                for (int k = 0; k < 6; k++)
                    Prim(PrimitiveType.Cube, "Fleck", vein, new Vector3(Random.Range(-0.9f, 0.9f), Random.Range(-0.5f, 0.5f), Random.Range(-0.2f, 0.1f)), Vector3.one * Random.Range(0.08f, 0.2f), m, false).transform.localRotation = Random.rotation;
                if (i % 2 == 0) MineSparkle(vein, new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.3f), 0.3f), Random.Range(0.6f, 1f));
            }
            return motes;
        }

        MineCave BuildMine()
        {
            var go = new GameObject("MineCave");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;
            var shown = new List<GameObject>();
            mineTrackPoints.Clear();
            mineSparkles.Clear();

            var dirt = Mat("Mine_Dirt", new Color(0.36f, 0.24f, 0.14f), null, false, 0.05f);
            var dirtDark = Mat("Mine_DirtDark", new Color(0.22f, 0.14f, 0.08f), null, false, 0.05f);
            var wood = Mat("Mine_Wood", new Color(0.4f, 0.27f, 0.15f), null, false, 0.15f);
            var woodDark = Mat("Mine_WoodDark", new Color(0.25f, 0.16f, 0.09f), null, false, 0.15f);
            var lantern = Mat("Mine_Lantern", new Color(1f, 0.7f, 0.3f), new Color(2.4f, 1.3f, 0.4f));

            // ---- 1. ハッチを覆う土の床と、掘られる穴 ----
            var cover = Group(root, "DigCover");
            var coverRing = new GameObject("DirtFloor");
            coverRing.transform.SetParent(cover, false);
            coverRing.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            coverRing.AddComponent<MeshFilter>().sharedMesh = AnnulusMesh(1.15f, 3.75f, 48);
            coverRing.AddComponent<MeshRenderer>().sharedMaterial = dirt;
            // (穴のまわりの盛り土や土くれは置かない。平らな土の床に穴があくだけ)
            var plug = Prim(PrimitiveType.Cylinder, "Plug", cover, new Vector3(0f, 0.01f, 0f), new Vector3(2.4f, 0.01f, 2.4f), dirtDark, false).transform;

            // ---- 2. 縦穴 (地上から洞窟の天井まで) ----
            float shaftLen = MineDepth - CaveCeiling;
            var shaft = Group(root, "Shaft");
            shown.Add(shaft.gameObject);
            var shaftWall = Mat("Mine_ShaftWall", new Color(0.26f, 0.18f, 0.12f), null, false, 0.05f);
            var tube = new GameObject("ShaftWall");
            tube.transform.SetParent(shaft, false);
            tube.transform.localScale = new Vector3(1.2f, shaftLen, 1.2f);
            tube.AddComponent<MeshFilter>().sharedMesh = TubeMesh(24);
            tube.AddComponent<MeshRenderer>().sharedMaterial = shaftWall;
            for (float y = -1.5f; y > -shaftLen; y -= 2.5f) // 木の枠と、ときどきランタン
            {
                FlatRing(shaft, "ShaftRing", 1.12f, y, 0.12f, 0.14f, 16, woodDark);
                if (Mathf.RoundToInt(-y / 2.5f) % 2 == 0)
                {
                    float a = y * 1.7f;
                    Prim(PrimitiveType.Sphere, "ShaftLantern", shaft, new Vector3(Mathf.Sin(a) * 1.05f, y + 0.3f, Mathf.Cos(a) * 1.05f), new Vector3(0.12f, 0.18f, 0.12f), lantern, false);
                }
            }
            for (int k = 0; k < 4; k++) // 縦の柱
            {
                float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cube, "ShaftBeam", shaft, new Vector3(Mathf.Sin(a) * 1.13f, -shaftLen / 2f, Mathf.Cos(a) * 1.13f), new Vector3(0.12f, shaftLen, 0.12f), wood, false)
                    .transform.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
            }
            for (int i = 0; i < 30; i++) // 壁から突き出た石
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Rock("ShaftRock", shaft, new Vector3(Mathf.Sin(a) * 1.25f, Random.Range(-shaftLen + 0.5f, -0.8f), Mathf.Cos(a) * 1.25f), Vector3.one * Random.Range(0.25f, 0.5f), Mat("Mine_Rock", new Color(0.32f, 0.29f, 0.27f), null, false, 0.42f), false);
            }

            // ---- 昇降台 (プレイヤーといっしょに上下する) ----
            var lift = Group(root, "Lift");
            shown.Add(lift.gameObject);
            Prim(PrimitiveType.Cylinder, "LiftDeck", lift, new Vector3(0f, -0.06f, 0f), new Vector3(1.9f, 0.05f, 1.9f), wood, false);
            for (int k = -3; k <= 3; k++)
                Prim(PrimitiveType.Cube, "LiftPlank", lift, new Vector3(k * 0.26f, -0.005f, 0f), new Vector3(0.02f, 0.01f, Mathf.Sqrt(Mathf.Max(0.01f, 0.9f - k * k * 0.07f)) * 2f), woodDark, false);
            FlatRing(lift, "LiftRail", 0.97f, 0.95f, 0.06f, 0.06f, 20, woodDark);
            for (int k = 0; k < 6; k++)
            {
                float a = (k * 60f + 30f) * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cube, "LiftPost", lift, new Vector3(Mathf.Sin(a) * 0.97f, 0.47f, Mathf.Cos(a) * 0.97f), new Vector3(0.06f, 0.95f, 0.06f), woodDark, false);
            }
            Prim(PrimitiveType.Sphere, "LiftLantern", lift, new Vector3(0.97f, 1.05f, 0f), new Vector3(0.12f, 0.18f, 0.12f), lantern, false);

            // ---- 3. 洞窟 ----
            var cave = Group(root, "Cave");
            cave.localPosition = new Vector3(0f, -MineDepth, 0f);
            shown.Add(cave.gameObject);

            // 線路 (先に道すじを決める。石筍などを線路の上に置かないように)
            var tracks = new List<MineTrack>();
            var loopA = MakeTrack(cave, "Track_LoopA", Ellipse(7.5f, 7.5f, CaveFloor, 0f, 40), true);
            var loopB = MakeTrack(cave, "Track_LoopB", Ellipse(15f, 12f, CaveFloor + 2.6f, 0.7f, 56), true);
            var tunnelTracks = new List<MineTrack>();
            var portals = new List<(Vector3 pos, Vector3 outward)>();
            (float a0, float a1, float ac, float rc)[] tunnels = { (35f, 145f, 90f, 13f), (205f, 320f, 262f, 12f), (112f, 248f, 180f, 21f) };
            for (int i = 0; i < tunnels.Length; i++)
            {
                var t = tunnels[i];
                var pts = TunnelPath(t.a0, t.a1, t.ac, t.rc, out var out0, out var out1);
                tunnelTracks.Add(MakeTrack(cave, $"Track_Tunnel{i}", pts, false));
                portals.Add((pts[2], out0));
                portals.Add((pts[pts.Length - 3], out1));
            }
            tracks.Add(loopA);
            tracks.Add(loopB);
            tracks.AddRange(tunnelTracks);
            var steel = Mat("Rail_Steel", new Color(0.5f, 0.5f, 0.52f), null, false, 0.8f);
            foreach (var tr in tracks) BuildRails(cave, tr, steel, woodDark, tr == loopB);

            BuildCaveShell(cave);
            foreach (var p in portals) TunnelPortal(cave, p.pos, p.outward, wood, woodDark, lantern);
            BuildCaveTower(cave, wood, woodDark, lantern);
            BuildCaveDecor(cave, wood, woodDark, lantern);
            var flicker = BuildCaveLights(cave);
            BuildCaveBats(cave);
            var motes = BuildCaveAtmosphere(cave);

            // ---- 的: トロッコ (荷台の鉱石・宝石) と、壁の宝石 ----
            var loopCarts = new List<MineCart>();
            for (int i = 0; i < 5; i++) loopCarts.Add(BuildMineCart(cave, $"Cart_A{i}", loopA, 2.6f));
            for (int i = 0; i < 7; i++) loopCarts.Add(BuildMineCart(cave, $"Cart_B{i}", loopB, 3.4f));
            var tunnelCarts = new List<MineCart>();
            for (int i = 0; i < 8; i++) tunnelCarts.Add(BuildMineCart(cave, $"Cart_T{i}", tunnelTracks[0], 4.5f));
            var gems = BuildMineGems(cave);

            // 洞窟と縦穴は暗いので、影は落とさない (重くしないように)
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var mine = go.AddComponent<MineCave>();
            mine.digCover = cover.gameObject;
            mine.plug = plug;
            mine.dirtMaterial = TransparentMat("Mine_DirtPuff", new Color(0.45f, 0.32f, 0.2f, 0.7f));
            mine.showDuringStage = shown.ToArray();
            mine.lift = lift;
            mine.depth = MineDepth;
            mine.loopCarts = loopCarts.ToArray();
            mine.tunnelCarts = tunnelCarts.ToArray();
            mine.tunnelTracks = tunnelTracks.ToArray();
            mine.gems = gems.ToArray();
            mine.caveRoomHeight = CaveCeiling;
            mine.sparkles = mineSparkles.ToArray();
            mine.flickerLights = flicker.ToArray();
            mine.motes = motes.ToArray();
            // 発掘モードの時だけ出る (シーンに生成した時も隠しておく)
            cover.gameObject.SetActive(false);
            foreach (var g in shown) g.SetActive(false);
            go.SetActive(true);
            return mine;
        }

        // ------------------------------------------------------------------
        // メッシュ (穴のあいた円盤 / 筒)
        // ------------------------------------------------------------------

        /// <summary>真ん中に穴のあいた平らな円盤 (XZ 平面。表裏の両面)</summary>
        Mesh AnnulusMesh(float inner, float outer, int segs)
        {
            string name = $"Shape_Annulus_{inner:0.00}_{outer:0.00}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                int k = verts.Count;
                verts.Add(d0 * inner); verts.Add(d0 * outer); verts.Add(d1 * outer); verts.Add(d1 * inner);
                tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });     // 上向き
                tris.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });     // 下向き
            }
            return FinishMesh(name, verts, tris);
        }

        /// <summary>半径 1、上端 y = 0 から下端 y = -1 までの筒の側面 (内側からも見えるよう両面)</summary>
        Mesh TubeMesh(int segs)
        {
            string name = $"Shape_Tube_{segs}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < segs; i++)
            {
                float a0 = i * Mathf.PI * 2f / segs, a1 = (i + 1) * Mathf.PI * 2f / segs;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0));
                var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                int k = verts.Count;
                verts.Add(d0); verts.Add(d1); verts.Add(d1 + Vector3.down); verts.Add(d0 + Vector3.down);
                tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                tris.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
            }
            return FinishMesh(name, verts, tris);
        }

        const int RockVariants = 8;

        /// <summary>
        /// ごつごつした岩のメッシュ (直径 1 くらい。球の代わり)。正二十面体を細かくして、頂点ごとに出っ張り・へこみをつけ、
        /// 何か所かを平らに削り落とし、面ごとに頂点を分けて角ばった (切り出した石のような) 見た目にする。variant ごとに形が違う
        /// </summary>
        Mesh RockMesh(int variant)
        {
            string name = $"Shape_Rock_{variant}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var rng = new System.Random(1000 + variant * 7919);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // 正二十面体
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // 1回細かくする (辺の真ん中に頂点を足す)
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int m)) return m;
                v.Add(((v[a] + v[b]) * 0.5f).normalized);
                mid[key] = v.Count - 1;
                return v.Count - 1;
            }
            var f2 = new List<int>();
            for (int i = 0; i < f.Count; i += 3)
            {
                int a = f[i], b = f[i + 1], c = f[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                f2.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            // 出っ張り・へこみと、全体のゆがみ
            var stretch = new Vector3(Rand(0.8f, 1.2f), Rand(0.7f, 1.1f), Rand(0.8f, 1.2f));
            for (int i = 0; i < v.Count; i++)
                v[i] = Vector3.Scale(v[i] * Rand(0.82f, 1.12f), stretch);
            // 平らに削り落とす面 (切り出した石のような角)
            int cuts = 3 + variant % 3;
            for (int k = 0; k < cuts; k++)
            {
                var n = new Vector3(Rand(-1f, 1f), Rand(-1f, 1f), Rand(-1f, 1f)).normalized;
                float d = Rand(0.62f, 0.85f);
                for (int i = 0; i < v.Count; i++)
                {
                    float over = Vector3.Dot(v[i], n) - d;
                    if (over > 0f) v[i] -= n * over;
                }
            }
            // 面ごとに頂点を分ける (角ばった陰影)。直径がだいたい 1 になるように縮める
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < f2.Count; i++)
            {
                verts.Add(v[f2[i]] * 0.5f);
                tris.Add(i);
            }
            return FinishMesh(name, verts, tris);
        }

        /// <summary>ごつごつした岩 (球の Prim の代わり)。形はばらばら、向きもばらばら。引数の並びは Prim と同じ (形の種類なし)</summary>
        GameObject Rock(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = false)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            // 細長い岩 (つらら石・石筍) は、立てたまま Y まわりにだけ回す (横に寝ないように)
            bool tall = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) > Mathf.Min(scale.x, Mathf.Min(scale.y, scale.z)) * 1.6f;
            g.transform.localRotation = tall ? Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f))
                                             : Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            g.transform.localScale = scale;
            var mesh = RockMesh(Random.Range(0, RockVariants));
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (collider)
            {
                var mc = g.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
            return g;
        }

        Mesh FinishMesh(string name, List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------
        // 線路
        // ------------------------------------------------------------------

        // 楕円の線路の点 (bob = 上下にうねる幅)
        static Vector3[] Ellipse(float rx, float rz, float y, float bob, int count)
        {
            var pts = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                pts[i] = new Vector3(Mathf.Sin(a) * rx, y + Mathf.Sin(a * 3f) * bob, Mathf.Cos(a) * rz);
            }
            return pts;
        }

        // トンネルの穴 (角度 a0) から出て、ゆるく曲がって、別の穴 (角度 a1) へ入る線路。両端は穴の奥 (暗い中) まで続く
        static Vector3[] TunnelPath(float a0, float a1, float ac, float rc, out Vector3 out0, out Vector3 out1)
        {
            const float R = 27f;
            Vector3 Dir(float deg) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad));
            Vector3 p0 = Dir(a0) * R + Vector3.up * CaveFloor, p2 = Dir(a1) * R + Vector3.up * CaveFloor, c = Dir(ac) * rc + Vector3.up * CaveFloor;
            const int N = 30;
            var list = new List<Vector3>();
            out0 = (p0 - c).normalized;
            out1 = (p2 - c).normalized;
            list.Add(p0 + out0 * 5f);
            list.Add(p0 + out0 * 2.5f);
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                list.Add((1f - t) * (1f - t) * p0 + 2f * (1f - t) * t * c + t * t * p2);
            }
            list.Add(p2 + out1 * 2.5f);
            list.Add(p2 + out1 * 5f);
            return list.ToArray();
        }

        MineTrack MakeTrack(Transform cave, string name, Vector3[] pts, bool loop)
        {
            var t = Group(cave, name).gameObject.AddComponent<MineTrack>();
            t.points = pts;
            t.loop = loop;
            mineTrackPoints.AddRange(pts);
            return t;
        }

        bool NearTrack(Vector3 p, float dist)
        {
            foreach (var q in mineTrackPoints)
                if ((new Vector2(p.x - q.x, p.z - q.z)).sqrMagnitude < dist * dist) return true;
            return false;
        }

        // 2本のレールと枕木。高架の線路は、ときどき床まで木の脚を立てる
        void BuildRails(Transform cave, MineTrack track, Material steel, Material sleeper, bool trestle)
        {
            var group = Group(cave, track.name + "_Rails");
            var pts = track.points;
            int n = track.loop ? pts.Length : pts.Length - 1;
            for (int i = 0; i < n; i++)
            {
                if (!track.loop && (i < 2 || i >= pts.Length - 3)) continue; // トンネルの奥 (暗くて見えない所) は作らない
                Vector3 a = pts[i], b = pts[(i + 1) % pts.Length];
                Vector3 d = b - a;
                float len = d.magnitude;
                if (len < 0.01f) continue;
                var rot = Quaternion.LookRotation(d);
                var right = rot * Vector3.right;
                var mid = (a + b) / 2f;
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Rail", group, mid + right * s * RailGauge / 2f + Vector3.up * 0.06f, new Vector3(0.05f, 0.06f, len + 0.04f), steel, false).transform.localRotation = rot;
                Prim(PrimitiveType.Cube, "Sleeper", group, mid + Vector3.up * 0.015f, new Vector3(RailGauge + 0.35f, 0.05f, 0.18f), sleeper, false).transform.localRotation = rot;
                if (trestle && i % 4 == 0)
                {
                    float h = a.y - CaveFloor;
                    foreach (float s in new[] { -1f, 1f })
                        Prim(PrimitiveType.Cube, "TrestleLeg", group, new Vector3(a.x, CaveFloor + h / 2f, a.z) + right * s * 0.55f, new Vector3(0.14f, h, 0.14f), sleeper, false);
                    Prim(PrimitiveType.Cube, "TrestleBeam", group, a + Vector3.down * 0.08f, new Vector3(1.3f, 0.12f, 0.14f), sleeper, false).transform.localRotation = rot;
                    var brace = Prim(PrimitiveType.Cube, "TrestleBrace", group, new Vector3(a.x, CaveFloor + h / 2f, a.z), new Vector3(0.08f, Mathf.Sqrt(h * h + 1.2f), 0.08f), sleeper, false);
                    brace.transform.localRotation = rot * Quaternion.Euler(0f, 0f, Mathf.Atan2(1.1f, h) * Mathf.Rad2Deg);
                }
            }
        }

        // トンネルの穴: 奥が真っ暗な口と、木の枠 (2本の柱・横木・すじかい)、まわりの岩、ランタン
        void TunnelPortal(Transform cave, Vector3 pos, Vector3 outward, Material wood, Material woodDark, Material lantern)
        {
            var portal = Group(cave, "TunnelPortal");
            portal.localPosition = new Vector3(pos.x, CaveFloor, pos.z);
            portal.localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.z)); // +Z が穴の奥
            Prim(PrimitiveType.Cube, "TunnelDark", portal, new Vector3(0f, 1.45f, 3.2f), new Vector3(2.7f, 2.9f, 5f), Mat("Mine_TunnelDark", new Color(0.01f, 0.008f, 0.006f), null, true), false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "PortalPost", portal, new Vector3(s * 1.45f, 1.55f, 0.6f), new Vector3(0.28f, 3.1f, 0.28f), wood, false);
                Prim(PrimitiveType.Cube, "PortalBrace", portal, new Vector3(s * 1.15f, 2.75f, 0.45f), new Vector3(0.14f, 0.8f, 0.14f), woodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, s * 45f);
                Prim(PrimitiveType.Sphere, "PortalLantern", portal, new Vector3(s * 1.45f, 2.4f, 0.4f), new Vector3(0.14f, 0.2f, 0.14f), lantern, false);
                for (int k = 0; k < 3; k++) // まわりの岩
                    Rock("PortalRock", portal, new Vector3(s * Random.Range(2.4f, 3.6f), Random.Range(0.5f, 3.5f), Random.Range(1f, 2.5f)), Vector3.one * Random.Range(1.8f, 3f), Mat("Mine_RockDark", new Color(0.2f, 0.18f, 0.17f), null, false, 0.42f), false);
            }
            Prim(PrimitiveType.Cube, "PortalLintel", portal, new Vector3(0f, 3.15f, 0.6f), new Vector3(3.4f, 0.32f, 0.36f), wood, false);
            Rock("PortalTopRock", portal, new Vector3(0f, 4.5f, 1.8f), new Vector3(5.5f, 3f, 3.5f), Mat("Mine_RockDark", new Color(0.2f, 0.18f, 0.17f), null, false, 0.42f), false);
            var sign = Prim(PrimitiveType.Cube, "PortalSign", portal, new Vector3(0f, 3.55f, 0.4f), new Vector3(1.3f, 0.35f, 0.05f), Mat("Mine_SignBoard", new Color(0.55f, 0.4f, 0.22f)), false);
            sign.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
            MakeText(sign.transform, "SignText", "TUNNEL " + (char)('A' + Random.Range(0, 6)), new Vector3(0f, 0f, -0.6f), 0.55f, new Color(0.15f, 0.08f, 0.03f)).transform.localScale = new Vector3(1f / 1.3f, 1f / 0.35f, 1f);
        }

        // ------------------------------------------------------------------
        // 洞窟の岩・やぐら・飾り・明かり・コウモリ
        // ------------------------------------------------------------------

        /// <summary>洞窟の床・壁・天井: 大きな岩をいびつに並べて、広い空間を囲む。天井から下がるつらら石、床から伸びる石筍、壁に光る結晶</summary>
        void BuildCaveShell(Transform cave)
        {
            var shell = Group(cave, "CaveShell");
            var rock = Mat("Mine_Rock", new Color(0.32f, 0.29f, 0.27f), null, false, 0.42f);
            var rockDark = Mat("Mine_RockDark", new Color(0.2f, 0.18f, 0.17f), null, false, 0.42f);
            var rockBrown = Mat("Mine_RockBrown", new Color(0.36f, 0.26f, 0.18f), null, false, 0.42f);
            Material[] rocks = { rock, rockDark, rockBrown };

            Prim(PrimitiveType.Cylinder, "CaveFloor", shell, new Vector3(0f, CaveFloor - 0.1f, 0f), new Vector3(72f, 0.1f, 72f), rockBrown, false);
            for (int i = 0; i < 70; i++) // 床のでこぼこと小石
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(3f, 32f);
                var p = new Vector3(Mathf.Sin(a) * r, CaveFloor, Mathf.Cos(a) * r);
                if (NearTrack(p, 1.3f)) continue;
                float w = Random.Range(0.3f, 2.2f);
                Rock("FloorRock", shell, p, new Vector3(w, w * Random.Range(0.3f, 0.7f), w * Random.Range(0.7f, 1.3f)), rocks[i % 3], false);
            }
            // 壁: 下と上の2段の大岩 (いびつに大きさと距離を変える)
            for (int i = 0; i < 40; i++)
            {
                float a = (i * 9f + Random.Range(-3f, 3f)) * Mathf.Deg2Rad;
                float r = Random.Range(31f, 36f);
                Rock("WallRock", shell, new Vector3(Mathf.Sin(a) * r, CaveFloor + Random.Range(2f, 5f), Mathf.Cos(a) * r),
                     new Vector3(Random.Range(9f, 15f), Random.Range(12f, 20f), Random.Range(8f, 12f)), rocks[i % 3], false).transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), a * Mathf.Rad2Deg, Random.Range(-15f, 15f));
            }
            for (int i = 0; i < 32; i++)
            {
                float a = (i * 11.25f + Random.Range(-4f, 4f)) * Mathf.Deg2Rad;
                float r = Random.Range(27f, 32f);
                Rock("WallRockHigh", shell, new Vector3(Mathf.Sin(a) * r, CaveFloor + Random.Range(10f, 14f), Mathf.Cos(a) * r),
                     new Vector3(Random.Range(10f, 16f), Random.Range(8f, 12f), Random.Range(8f, 12f)), rocks[(i + 1) % 3], false);
            }
            // 天井: 縦穴のまわりの岩の天井と、外へ向かって少し下がりながら広がる大岩
            var ceil = new GameObject("CeilingRing");
            ceil.transform.SetParent(shell, false);
            ceil.transform.localPosition = new Vector3(0f, CaveCeiling, 0f);
            ceil.AddComponent<MeshFilter>().sharedMesh = AnnulusMesh(1.2f, 7.5f, 32);
            ceil.AddComponent<MeshRenderer>().sharedMaterial = rockDark;
            int[] ringCounts = { 8, 12, 16, 20 };
            float[] ringRadii = { 7f, 13f, 19f, 25f };
            for (int ring = 0; ring < ringCounts.Length; ring++)
                for (int i = 0; i < ringCounts[ring]; i++)
                {
                    float a = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / ringCounts[ring];
                    float r = ringRadii[ring] + Random.Range(-1.5f, 1.5f);
                    float size = ring == 0 ? Random.Range(6f, 8f) : Random.Range(8f, 13f);
                    Rock("CeilingRock", shell, new Vector3(Mathf.Sin(a) * r, CaveCeiling + 3f + Random.Range(0f, 3f) - ring * 0.6f, Mathf.Cos(a) * r),
                         new Vector3(size, size * Random.Range(0.5f, 0.8f), size), rocks[(i + ring) % 3], false);
                }
            // つらら石 (天井から下がる、とがった岩)
            for (int i = 0; i < 36; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(4f, 26f);
                float len = Random.Range(1.5f, 4.5f);
                float top = CaveCeiling + 2.5f - r * 0.05f;
                var st = Group(shell, "Stalactite");
                st.localPosition = new Vector3(Mathf.Sin(a) * r, top, Mathf.Cos(a) * r);
                for (int k = 0; k < 3; k++)
                {
                    float w = len * 0.22f * (1f - k * 0.3f);
                    Rock("Drip", st, new Vector3(0f, -len * (0.2f + k * 0.28f), 0f), new Vector3(w, len * 0.45f, w), rocks[(i + k) % 3], false);
                }
            }
            // 石筍 (床から伸びる、とがった岩)
            for (int i = 0; i < 40; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(4f, 29f);
                var p = new Vector3(Mathf.Sin(a) * r, CaveFloor, Mathf.Cos(a) * r);
                if (NearTrack(p, 2f)) continue;
                float h = Random.Range(1f, 4f);
                var sg = Group(shell, "Stalagmite");
                sg.localPosition = p;
                for (int k = 0; k < 3; k++)
                {
                    float w = h * 0.3f * (1f - k * 0.3f);
                    Rock("Spire", sg, new Vector3(0f, h * (0.15f + k * 0.28f), 0f), new Vector3(w, h * 0.45f, w), rocks[(i + k) % 3], false);
                }
            }
            // 壁と床に光る結晶 (飾り。的ではない)
            var gemMats = GemMaterials();
            for (int i = 0; i < 34; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                bool wall = i % 3 != 0;
                float r = wall ? Random.Range(25f, 28f) : Random.Range(6f, 22f);
                var p = new Vector3(Mathf.Sin(a) * r, wall ? CaveFloor + Random.Range(0f, 9f) : CaveFloor, Mathf.Cos(a) * r);
                if (!wall && NearTrack(p, 1.6f)) continue;
                var c = Group(shell, "Crystals");
                c.localPosition = p;
                c.localRotation = Quaternion.LookRotation(-new Vector3(p.x, 0f, p.z)) * Quaternion.Euler(wall ? -60f : 0f, 0f, 0f);
                float cs = Random.Range(0.6f, 1.6f);
                CrystalCluster(c, gemMats[Random.Range(0, gemMats.Length)].glow, cs, 5);
                if (i % 2 == 0) MineSparkle(c, new Vector3(0f, cs * 0.9f, 0f), cs * 0.9f);
            }
        }

        /// <summary>結晶のかたまり: 根元から外へ広がる、細長い四角い柱を何本か (原点が根元、+Y が伸びる向き)</summary>
        void CrystalCluster(Transform parent, Material mat, float size, int count, bool collider = false)
        {
            for (int k = 0; k < count; k++)
            {
                float h = size * Random.Range(0.6f, 1.2f) * (k == 0 ? 1.3f : 1f);
                var tilt = Quaternion.Euler(Random.Range(-35f, 35f), Random.Range(0f, 360f), Random.Range(-35f, 35f));
                if (k == 0) tilt = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 90f), Random.Range(-8f, 8f));
                var prism = Prim(PrimitiveType.Cube, "Crystal", parent, tilt * Vector3.up * h * 0.45f, new Vector3(size * 0.22f, h, size * 0.22f), mat, collider);
                prism.transform.localRotation = tilt * Quaternion.Euler(0f, 45f, 0f);
                var tip = Prim(PrimitiveType.Cube, "CrystalTip", parent, tilt * Vector3.up * h * 0.95f, Vector3.one * size * 0.16f, mat, false);
                tip.transform.localRotation = tilt * Quaternion.Euler(45f, 45f, 0f);
            }
        }

        /// <summary>宝石の種類: 名前・点数・光る材質・破片の材質 (安い順)</summary>
        (string name, int points, Material glow, Material shard)[] GemMaterials() => new[]
        {
            ("AMETHYST", 100, Mat("Gem_Amethyst", new Color(0.7f, 0.35f, 1f), new Color(0.9f, 0.35f, 1.6f), false, 0.9f), Mat("Gem_Amethyst", new Color(0.7f, 0.35f, 1f), new Color(0.9f, 0.35f, 1.6f), false, 0.9f)),
            ("EMERALD", 200, Mat("Gem_Emerald", new Color(0.2f, 1f, 0.45f), new Color(0.2f, 1.6f, 0.5f), false, 0.9f), Mat("Gem_Emerald", new Color(0.2f, 1f, 0.45f), new Color(0.2f, 1.6f, 0.5f), false, 0.9f)),
            ("SAPPHIRE", 300, Mat("Gem_Sapphire", new Color(0.2f, 0.45f, 1f), new Color(0.25f, 0.6f, 2f), false, 0.9f), Mat("Gem_Sapphire", new Color(0.2f, 0.45f, 1f), new Color(0.25f, 0.6f, 2f), false, 0.9f)),
            ("RUBY", 500, Mat("Gem_Ruby", new Color(1f, 0.15f, 0.25f), new Color(2f, 0.2f, 0.35f), false, 0.9f), Mat("Gem_Ruby", new Color(1f, 0.15f, 0.25f), new Color(2f, 0.2f, 0.35f), false, 0.9f)),
            ("DIAMOND", 1000, Mat("Gem_Diamond", new Color(0.9f, 0.97f, 1f), new Color(2.2f, 2.6f, 3f), false, 1f), Mat("Gem_Diamond", new Color(0.9f, 0.97f, 1f), new Color(2.2f, 2.6f, 3f), false, 1f)),
        };

        /// <summary>真ん中のやぐら (プレイヤーの昇降台が止まる所): 4本の柱・すじかい・床の枠・はしご・ランタン・天井までの案内の綱・看板</summary>
        void BuildCaveTower(Transform cave, Material wood, Material woodDark, Material lantern)
        {
            var tower = Group(cave, "Tower");
            const float H = -CaveFloor;
            foreach (var c in new[] { new Vector2(-1.25f, -1.25f), new Vector2(1.25f, -1.25f), new Vector2(-1.25f, 1.25f), new Vector2(1.25f, 1.25f) })
            {
                Prim(PrimitiveType.Cube, "TowerPost", tower, new Vector3(c.x, CaveFloor + H / 2f, c.y), new Vector3(0.22f, H, 0.22f), wood, false);
                Prim(PrimitiveType.Cylinder, "GuideRope", tower, new Vector3(c.x * 0.85f, CaveCeiling / 2f, c.y * 0.85f), new Vector3(0.03f, CaveCeiling / 2f, 0.03f), Mat("Ghost_Rope", new Color(0.55f, 0.45f, 0.3f)), false);
                Prim(PrimitiveType.Sphere, "TowerLantern", tower, new Vector3(c.x, 0.3f, c.y), new Vector3(0.14f, 0.2f, 0.14f), lantern, false);
            }
            for (int side = 0; side < 4; side++)
            {
                var rot = Quaternion.Euler(0f, side * 90f, 0f);
                for (int k = 0; k < 2; k++)
                {
                    var br = Prim(PrimitiveType.Cube, "TowerBrace", tower, rot * new Vector3(0f, CaveFloor + 1f + k * 2f, 1.25f), new Vector3(0.1f, 2.9f, 0.1f), woodDark, false);
                    br.transform.localRotation = rot * Quaternion.Euler(0f, 0f, (k % 2 == 0 ? 1f : -1f) * 50f);
                }
                Prim(PrimitiveType.Cube, "DeckFrame", tower, rot * new Vector3(0f, -0.08f, 1.15f), new Vector3(2.7f, 0.14f, 0.4f), wood, false).transform.localRotation = rot;
            }
            // はしご
            for (int k = 0; k < 2; k++)
                Prim(PrimitiveType.Cube, "LadderSide", tower, new Vector3(-0.25f + k * 0.5f, CaveFloor + H / 2f, -1.42f), new Vector3(0.06f, H, 0.06f), woodDark, false);
            for (float y = CaveFloor + 0.3f; y < -0.2f; y += 0.35f)
                Prim(PrimitiveType.Cube, "LadderRung", tower, new Vector3(0f, y, -1.42f), new Vector3(0.5f, 0.04f, 0.04f), woodDark, false);
            var sign = Prim(PrimitiveType.Cube, "TowerSign", tower, new Vector3(0f, -1.2f, -1.4f), new Vector3(1.6f, 0.45f, 0.05f), Mat("Mine_SignBoard", new Color(0.55f, 0.4f, 0.22f)), false);
            MakeText(sign.transform, "SignText", "GEM MINE", new Vector3(0f, 0f, -0.6f), 0.6f, new Color(0.15f, 0.08f, 0.03f)).transform.localScale = new Vector3(1f / 1.6f, 1f / 0.45f, 1f);
        }

        /// <summary>洞窟の飾り: 木の支え (柱と梁)、木箱・たる・鉱石の山・立てかけたつるはし</summary>
        void BuildCaveDecor(Transform cave, Material wood, Material woodDark, Material lantern)
        {
            var decor = Group(cave, "Decor");
            // 壁ぎわの木の支え
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45f + 22f) * Mathf.Deg2Rad, r = 23.5f;
                var frame = Group(decor, "Support");
                frame.localPosition = new Vector3(Mathf.Sin(a) * r, CaveFloor, Mathf.Cos(a) * r);
                frame.localRotation = Quaternion.LookRotation(frame.localPosition);
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "SupportPost", frame, new Vector3(s * 1.6f, 2.2f, 0f), new Vector3(0.3f, 4.4f, 0.3f), wood, false).transform.localRotation = Quaternion.Euler(0f, 0f, s * Random.Range(-4f, 4f));
                Prim(PrimitiveType.Cube, "SupportBeam", frame, new Vector3(0f, 4.4f, 0f), new Vector3(3.8f, 0.35f, 0.35f), wood, false);
                Prim(PrimitiveType.Sphere, "SupportLantern", frame, new Vector3(0f, 4f, -0.25f), new Vector3(0.14f, 0.2f, 0.14f), lantern, false);
            }
            // 木箱・たる・鉱石の山・つるはし
            var ore = Mat("Ore_Gold", new Color(1f, 0.8f, 0.25f), new Color(0.35f, 0.25f, 0.02f), false, 0.8f);
            for (int i = 0; i < 14; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(3.2f, 22f);
                var p = new Vector3(Mathf.Sin(a) * r, CaveFloor, Mathf.Cos(a) * r);
                if (NearTrack(p, 1.8f)) continue;
                var g = Group(decor, "Prop");
                g.localPosition = p;
                g.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                switch (i % 4)
                {
                    case 0:
                        Prim(PrimitiveType.Cube, "Crate", g, new Vector3(0f, 0.35f, 0f), Vector3.one * 0.7f, wood, false);
                        Prim(PrimitiveType.Cube, "CrateBand", g, new Vector3(0f, 0.35f, 0f), new Vector3(0.72f, 0.1f, 0.72f), woodDark, false);
                        break;
                    case 1:
                        Prim(PrimitiveType.Cylinder, "Barrel", g, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0.6f), wood, false);
                        Prim(PrimitiveType.Cylinder, "BarrelBand", g, new Vector3(0f, 0.7f, 0f), new Vector3(0.62f, 0.03f, 0.62f), Mat("Ghost_Rust", new Color(0.4f, 0.22f, 0.12f), null, false, 0.3f), false);
                        break;
                    case 2:
                        for (int k = 0; k < 6; k++)
                            Rock("OreLump", g, new Vector3(Random.Range(-0.4f, 0.4f), 0.15f + (k % 3) * 0.12f, Random.Range(-0.4f, 0.4f)), Vector3.one * Random.Range(0.25f, 0.4f), k % 2 == 0 ? ore : Mat("Mine_RockDark", new Color(0.2f, 0.18f, 0.17f), null, false, 0.42f), false);
                        break;
                    default:
                        var pick = Group(g, "LeaningPickaxe");
                        pick.localPosition = new Vector3(0f, 0f, 0f);
                        pick.localRotation = Quaternion.Euler(0f, 0f, 20f);
                        Prim(PrimitiveType.Cylinder, "Handle", pick, new Vector3(0f, 0.45f, 0f), new Vector3(0.05f, 0.45f, 0.05f), wood, false);
                        foreach (float s in new[] { -1f, 1f })
                            Prim(PrimitiveType.Cube, "Head", pick, new Vector3(s * 0.15f, 0.9f - Mathf.Abs(s) * 0.02f, 0f), new Vector3(0.32f, 0.06f, 0.06f), Mat("Pickaxe_Head", new Color(0.55f, 0.56f, 0.6f), null, false, 0.7f), false).transform.localRotation = Quaternion.Euler(0f, 0f, -s * 12f);
                        break;
                }
            }
        }

        // 洞窟の明かり: やぐらの上と、まわりのいくつかにゆらめくオレンジのランプ (影なし)。壁の結晶のそばには、結晶の色の弱い明かり。
        // ゆらめかせるランプを返す
        List<Light> BuildCaveLights(Transform cave)
        {
            var flicker = new List<Light>();
            var lights = Group(cave, "CaveLights");
            var spots = new List<Vector3> { new Vector3(0f, 2.5f, 0f) };
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                spots.Add(new Vector3(Mathf.Sin(a) * 17f, CaveFloor + 4f, Mathf.Cos(a) * 17f));
            }
            for (int i = 0; i < spots.Count; i++)
            {
                var lg = new GameObject("CaveLight");
                lg.transform.SetParent(lights, false);
                lg.transform.localPosition = spots[i];
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = i == 0 ? 20f : 18f;
                l.intensity = i == 0 ? 2.2f : 3f;
                l.color = new Color(1f, 0.72f, 0.4f);
                l.shadows = LightShadows.None;
                flicker.Add(l);
            }
            // 結晶の色の明かり (紫・青・緑・水色。壁を色づかせる)
            Color[] glows = { new Color(0.7f, 0.35f, 1f), new Color(0.3f, 0.5f, 1f), new Color(0.3f, 1f, 0.5f), new Color(0.4f, 0.9f, 1f) };
            for (int i = 0; i < glows.Length; i++)
            {
                float a = (i * 90f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                var lg = new GameObject("CrystalLight");
                lg.transform.SetParent(lights, false);
                lg.transform.localPosition = new Vector3(Mathf.Sin(a) * 22f, CaveFloor + 2f, Mathf.Cos(a) * 22f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 12f;
                l.intensity = 2.2f;
                l.color = glows[i];
                l.shadows = LightShadows.None;
                var c = Group(lights, "GlowCrystals");
                c.localPosition = new Vector3(Mathf.Sin(a) * 23.5f, CaveFloor, Mathf.Cos(a) * 23.5f);
                c.localRotation = Quaternion.LookRotation(-c.localPosition);
                CrystalCluster(c, GemMaterials()[(i % 3) + 1].glow, 2.2f, 7); // 明かりのもとの大きな結晶
                MineSparkle(c, new Vector3(0f, 2.4f, 0.3f), 1.6f);
            }
            return flicker;
        }

        // コウモリの群れ: 洞窟の中をぐるぐる飛び回る (群れごとに高さ・半径・速さ・向きが違う)。羽はぱたぱた動く
        void BuildCaveBats(Transform cave)
        {
            var batMat = Mat("Ghost_Bat", new Color(0.04f, 0.03f, 0.05f));
            (float r, float y, float speed)[] flocks = { (9f, 5f, 40f), (16f, 7f, -28f), (22f, 3f, 22f) };
            foreach (var f in flocks)
            {
                var flock = Group(cave, "BatFlock");
                flock.localPosition = new Vector3(0f, f.y, 0f);
                var rot = flock.gameObject.AddComponent<Rotator>();
                rot.axis = Vector3.up;
                rot.speed = f.speed;
                for (int k = 0; k < 5; k++)
                {
                    float a = (k * 22f + Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
                    var bat = Group(flock, "Bat");
                    bat.localPosition = new Vector3(Mathf.Sin(a) * f.r, Random.Range(-1.2f, 1.2f), Mathf.Cos(a) * f.r);
                    // 回る向き (円の接線) に体を向ける
                    var tangent = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)) * Mathf.Sign(f.speed);
                    bat.localRotation = Quaternion.LookRotation(tangent);
                    Prim(PrimitiveType.Sphere, "BatBody", bat, Vector3.zero, new Vector3(0.18f, 0.16f, 0.28f), batMat, false);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var wing = Group(bat, "Wing");
                        var flap = wing.gameObject.AddComponent<Pendulum>();
                        flap.amplitude = 40f * s;
                        flap.speed = 16f;
                        flap.phase = k;
                        ShapeObj(wing, "WingShape", Shape.Triangle, new Vector3(s * 0.3f, 0f, 0f), new Vector3(0.55f, 0.35f, 2f), batMat, s * -90f)
                            .transform.localRotation = Quaternion.Euler(90f, 0f, s * -90f);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // 的: トロッコと宝石
        // ------------------------------------------------------------------

        /// <summary>
        /// トロッコ: さびた鉄の荷台・車輪4つ。荷台に山盛りの荷 (銅 / 銀 / 金の鉱石・赤と青の宝石・ダイヤモンド) のどれか1つと、その上に点数。
        /// 荷そのものが的 (当てると割れる)
        /// </summary>
        MineCart BuildMineCart(Transform cave, string name, MineTrack track, float speed)
        {
            var root = Group(cave, name);
            root.gameObject.SetActive(false);
            var body = Group(root, "Body");
            var rust = Mat("Cart_Rust", new Color(0.45f, 0.25f, 0.15f), null, false, 0.35f);
            var iron = Mat("Cart_Iron", new Color(0.22f, 0.22f, 0.24f), null, false, 0.6f);
            Prim(PrimitiveType.Cube, "Tub", body, new Vector3(0f, 0.55f, 0f), new Vector3(0.95f, 0.55f, 1.25f), rust, false);
            Prim(PrimitiveType.Cube, "TubRim", body, new Vector3(0f, 0.84f, 0f), new Vector3(1.02f, 0.06f, 1.32f), iron, false);
            foreach (float z in new[] { -0.4f, 0f, 0.4f })
                Prim(PrimitiveType.Cube, "TubRib", body, new Vector3(0f, 0.55f, z), new Vector3(1f, 0.58f, 0.06f), iron, false);
            Prim(PrimitiveType.Cube, "Chassis", body, new Vector3(0f, 0.24f, 0f), new Vector3(0.7f, 0.08f, 1.3f), iron, false);
            var wheels = new List<Transform>();
            foreach (float s in new[] { -1f, 1f })
                foreach (float z in new[] { -0.42f, 0.42f })
                {
                    var w = Group(body, "Wheel");
                    w.localPosition = new Vector3(s * RailGauge / 2f, 0.18f, z);
                    Prim(PrimitiveType.Cylinder, "WheelDisc", w, Vector3.zero, new Vector3(0.36f, 0.03f, 0.36f), iron, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    Prim(PrimitiveType.Cube, "WheelSpoke", w, new Vector3(s * 0.035f, 0f, 0f), new Vector3(0.02f, 0.3f, 0.05f), rust, false);
                    wheels.Add(w);
                }

            // 荷 (的)
            var mount = Group(body, "Load");
            mount.localPosition = new Vector3(0f, 0.85f, 0f);
            mount.localScale = Vector3.one * 1.35f; // 荷台からはみ出すくらい山盛り (大きく見やすく)
            var visual = Group(mount, "LoadVisual");
            var gemKinds = GemMaterials();
            var copper = Mat("Ore_Copper", new Color(0.78f, 0.45f, 0.25f), new Color(0.12f, 0.05f, 0.02f), false, 0.7f);
            var silver = Mat("Ore_Silver", new Color(0.85f, 0.87f, 0.92f), new Color(0.12f, 0.12f, 0.14f), false, 0.85f);
            var gold = Mat("Ore_Gold", new Color(1f, 0.8f, 0.25f), new Color(0.35f, 0.25f, 0.02f), false, 0.8f);
            var oreRock = Mat("Mine_RockDark", new Color(0.2f, 0.18f, 0.17f), null, false, 0.42f);
            var loads = new List<GameObject>
            {
                OrePile(visual, "Load_Copper", copper, oreRock, 7, 0.28f),
                OrePile(visual, "Load_Silver", silver, oreRock, 7, 0.3f),
                OrePile(visual, "Load_Gold", gold, oreRock, 8, 0.32f),
                GemPile(visual, "Load_RubySapphire", gemKinds[3].glow, gemKinds[2].glow, 0.55f),
                GemPile(visual, "Load_Diamond", gemKinds[4].glow, gemKinds[4].glow, 0.75f),
            };
            // 当たり判定: 荷の山の形の見えない箱 (どの荷でも同じ大きさ。当てやすいよう荷台より少し大きめ)
            var hitBox = Prim(PrimitiveType.Cube, "LoadHit", visual, new Vector3(0f, 0.35f, 0f), new Vector3(1.3f, 1.05f, 1.6f), oreRock);
            hitBox.GetComponent<Renderer>().enabled = false;
            // 点数 (いつもプレイヤーの方を向く)
            var sign = Group(visual, "PointSign");
            sign.localPosition = new Vector3(0f, 1.05f, 0f);
            sign.gameObject.AddComponent<FaceCenter>();
            Prim(PrimitiveType.Cube, "Board", sign, Vector3.zero, new Vector3(0.75f, 0.34f, 0.04f), Mat("Mine_PointBoard", new Color(0.12f, 0.08f, 0.05f)), false);
            Prim(PrimitiveType.Cube, "Edge", sign, new Vector3(0f, 0f, 0.015f), new Vector3(0.82f, 0.41f, 0.03f), Mat("Mine_PointEdge", new Color(1f, 0.75f, 0.3f), new Color(1.2f, 0.7f, 0.2f)), false);
            var label = MakeText(sign, "Points", "100", new Vector3(0f, 0f, -0.03f), 0.24f, new Color(1f, 0.95f, 0.5f));

            var target = mount.gameObject.AddComponent<Target>();
            target.kind = TargetKind.MineObject;
            target.manualControl = true;
            target.shrinkOnHit = true; // 割れて小さくなって消える
            target.visual = visual;
            target.SetAvailable(false);

            var cart = root.gameObject.AddComponent<MineCart>();
            cart.track = track;
            cart.hitTarget = target;
            cart.loads = loads.ToArray();
            cart.loadPoints = new[] { 100, 200, 300, 500, 1000 };
            cart.loadShards = new[] { copper, silver, gold, gemKinds[3].shard, gemKinds[4].shard };
            cart.pointLabels = new[] { label };
            cart.speed = speed;
            cart.wheels = wheels.ToArray();
            cart.body = body;
            return cart;
        }

        // 鉱石の山: 黒っぽい土台に、ごつごつした塊 (金属の色で、少しつやがある)
        GameObject OrePile(Transform parent, string name, Material ore, Material rock, int count, float size)
        {
            var g = Group(parent, name);
            Rock("PileBase", g, new Vector3(0f, 0.02f, 0f), new Vector3(0.85f, 0.3f, 1.1f), rock, false);
            for (int k = 0; k < count; k++)
            {
                var p = new Vector3(Random.Range(-0.28f, 0.28f), 0.12f + (k % 3) * 0.1f, Random.Range(-0.38f, 0.38f));
                var lump = Rock("OreLump", g, p, new Vector3(size, size * Random.Range(0.6f, 0.9f), size * Random.Range(0.7f, 1.1f)), ore, false);
                lump.transform.localRotation = Random.rotation;
            }
            g.gameObject.SetActive(false);
            return g.gameObject;
        }

        // 宝石の山: 土台から、光る結晶がいくつも突き出す (a と b の2色を交互に)
        GameObject GemPile(Transform parent, string name, Material a, Material b, float size)
        {
            var g = Group(parent, name);
            Rock("PileBase", g, new Vector3(0f, 0.02f, 0f), new Vector3(0.85f, 0.3f, 1.1f), Mat("Mine_RockDark", new Color(0.2f, 0.18f, 0.17f), null, false, 0.42f), false);
            for (int k = 0; k < 3; k++)
            {
                var c = Group(g, "GemCluster");
                c.localPosition = new Vector3((k - 1) * 0.25f, 0.1f, (k - 1) * 0.3f);
                CrystalCluster(c, k % 2 == 0 ? a : b, size, 4);
            }
            if (a == b) // ダイヤモンドは、まわりにきらきらした光のかさ
                Prim(PrimitiveType.Sphere, "Sparkle", g, new Vector3(0f, 0.5f, 0f), Vector3.one * 1.4f, TransparentMat("Gem_DiamondHalo", new Color(0.85f, 0.95f, 1f, 0.15f)), false);
            g.gameObject.SetActive(false);
            return g.gameObject;
        }

        /// <summary>壁や床から生えた宝石 (的)。宝石そのものに点数があり、そばに大きく点数が浮かんでいる。高い宝石ほど大きく、少なく、遠く</summary>
        List<MineGem> BuildMineGems(Transform cave)
        {
            var kinds = GemMaterials();
            // 種類ごとの数 (アメジスト 11・エメラルド 11・サファイア 9・ルビー 8・ダイヤ 5) を、ばらばらの順に並べる
            var kindList = new List<int>();
            int[] counts = { 11, 11, 9, 8, 5 };
            for (int k = 0; k < counts.Length; k++) for (int n = 0; n < counts[k]; n++) kindList.Add(k);
            for (int i = kindList.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (kindList[i], kindList[j]) = (kindList[j], kindList[i]); }
            int[] kindOf = kindList.ToArray();
            var sparkle = TransparentMat("Gem_Sparkle", new Color(1f, 1f, 1f, 0.6f));
            var gems = new List<MineGem>();
            for (int i = 0; i < kindOf.Length; i++)
            {
                var kind = kinds[kindOf[i]];
                float a = (i * 360f / kindOf.Length + Random.Range(-4f, 4f)) * Mathf.Deg2Rad;
                bool wall = i % 3 != 2;
                float r = wall ? Random.Range(20f, 24.5f) : Random.Range(9f, 18f);
                var p = new Vector3(Mathf.Sin(a) * r, wall ? CaveFloor + Random.Range(0.5f, 6f) : CaveFloor, Mathf.Cos(a) * r);
                if (!wall && NearTrack(p, 2.2f)) p += new Vector3(p.x, 0f, p.z).normalized * 3f;
                float size = 1.25f + kindOf[i] * 0.2f; // 大きめ (遠くからでもよく見える)

                var root = Group(cave, $"Gem_{kind.name}_{i}");
                root.localPosition = p;
                root.localRotation = Quaternion.LookRotation(new Vector3(p.x, 0f, p.z)); // -Z がプレイヤー向き
                // 根元の岩 (壁の宝石は、壁から突き出た岩に付いている)
                Rock("GemRock", root, new Vector3(0f, wall ? 0f : -0.2f, wall ? 0.6f : 0f), new Vector3(2.2f, 1.6f, 2f) * size, Mat("Mine_RockDark", new Color(0.2f, 0.18f, 0.17f), null, false, 0.42f), false);

                var mount = Group(root, "Mount");
                mount.localRotation = wall ? Quaternion.Euler(-35f, 0f, 0f) : Quaternion.identity; // 壁の宝石は手前へ斜めに生える
                var visual = Group(mount, "Visual");
                var cluster = Group(visual, "Cluster");
                CrystalCluster(cluster, kind.glow, size * 1.2f, 6);
                // 当たり判定: 結晶のかたまりをまるごと包む見えない箱 (細い結晶そのものより大きめで当てやすい)
                var gemHit = Prim(PrimitiveType.Cube, "GemHit", visual, new Vector3(0f, size * 0.75f, 0f), new Vector3(size * 2f, size * 1.9f, size * 2f), kind.glow);
                gemHit.GetComponent<Renderer>().enabled = false;
                MineSparkle(visual, new Vector3(0.15f, size * 1.1f, -0.2f), size * 1.1f); // 宝石の上でキラッ
                // 点数 (宝石のすぐ上。いつもプレイヤーの方を向く)
                var sign = Group(visual, "PointSign");
                sign.localPosition = new Vector3(0f, size * 2.1f, 0f);
                sign.gameObject.AddComponent<FaceCenter>();
                float labelH = 0.5f + kindOf[i] * 0.05f;
                var label = MakeText(sign, "Points", kind.points.ToString(), Vector3.zero, labelH, kind.points >= 1000 ? new Color(0.9f, 1f, 1f) : kind.points >= 500 ? new Color(1f, 0.55f, 0.6f) : new Color(1f, 0.95f, 0.5f));
                Prim(PrimitiveType.Cube, "Board", sign, new Vector3(0f, 0f, 0.03f), new Vector3(labelH * 2.9f, labelH * 1.35f, 0.03f), Mat("Mine_PointBoard", new Color(0.12f, 0.08f, 0.05f)), false);

                var target = mount.gameObject.AddComponent<Target>();
                target.kind = TargetKind.MineObject;
                target.manualControl = true;
                target.shrinkOnHit = true;
                target.visual = visual;
                target.SetAvailable(false);

                var gem = root.gameObject.AddComponent<MineGem>();
                gem.displayName = kind.name;
                gem.points = kind.points;
                gem.hitTarget = target;
                gem.pointLabel = label;
                gem.shardMaterial = kind.shard;
                gem.sparkleMaterial = sparkle;
                gems.Add(gem);
            }
            return gems;
        }

        /// <summary>つるはしの的: 土色の板に、斜めのつるはしと小さな宝石、「MINE」の文字</summary>
        GameObject MineLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Mine_Plate", new Color(0.42f, 0.28f, 0.15f), new Color(0.06f, 0.03f, 0.01f)));
            var pick = Group(look, "Pickaxe");
            pick.localPosition = c + new Vector3(0f, 0.02f, -0.03f);
            pick.localRotation = Quaternion.Euler(0f, 0f, 35f);
            var rock = pick.gameObject.AddComponent<Pendulum>(); // カツン、カツンと振る
            rock.amplitude = 14f;
            rock.speed = 6f;
            Prim(PrimitiveType.Cube, "Handle", pick, new Vector3(0f, -0.02f, 0f), new Vector3(0.018f, 0.2f, 0.012f), Mat("Mine_Wood", new Color(0.4f, 0.27f, 0.15f), null, false, 0.15f), false);
            var head = Mat("Pickaxe_Head", new Color(0.55f, 0.56f, 0.6f), null, false, 0.7f);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Head", pick, new Vector3(s * 0.055f, 0.075f - 0.008f, 0f), new Vector3(0.11f, 0.024f, 0.014f), head, false).transform.localRotation = Quaternion.Euler(0f, 0f, -s * 14f);
            var gemKinds = GemMaterials();
            for (int k = 0; k < 3; k++)
                Prim(PrimitiveType.Cube, "Gem", look, c + new Vector3(-0.09f + k * 0.09f, -0.085f, -0.025f), Vector3.one * 0.03f, gemKinds[2 + k].glow, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            MakeText(look, "Label", "MINE", c + new Vector3(0f, -0.125f, -0.025f), 0.04f, new Color(1f, 0.8f, 0.4f));
            return look.gameObject;
        }
    }
}
