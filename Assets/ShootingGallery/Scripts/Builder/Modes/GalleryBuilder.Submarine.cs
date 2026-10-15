using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 潜水艦モード (潜水艦の的を撃つと始まる) の生成: 足元から広がる砂の床と、一気に広がる海の水面、プレイヤーを包む丸い黄色い潜水艦 (上下の半球とレンズの窓・計器・ライト・プロペラ)、
    /// 水面から差しこむ光の筋、海の底 (砂地・岩・崖・サンゴ礁・海藻の森・沈んだ船・神殿・岩のアーチ・泡の出る割れ目・マリンスノー・小魚の群れ)。
    /// 的 (海の生き物・ほかの潜水艦・壺・宝箱) は GalleryBuilder.SubmarineSea.cs。
    /// プレイヤーの足元 (地上) が原点。海はその真下 SubDepth の所にあり、海のローカル座標ではプレイヤーの足元が止まる所が原点。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float SubDepth = 20f;      // 地上から、海の底でプレイヤーの足元が止まる所までの深さ
        const float SandRadius = 12f;    // 足元から広がる砂の床の半径 (屋台の足元まで覆う)
        const float SubHatch = 1.6f;     // 砂の床の真ん中の、潜水艦が通るハッチの半径 (潜水艦の半径 1m より大きく)
        const float SeaRadius = 150f;    // 一気に広がる海の水面の半径 (海の底が遠くまで続いて見えるように、広く)
        const float SeaFloor = -1.2f;    // 海の底 (海のローカル。プレイヤーの足元が止まる所が 0。潜水艦の下はしは 0.6 の高さ)
        const float PodRadius = 1f;      // 潜水艦の球の半径 (頭が真ん中)
        const float PodWindowDeg = 52f;  // 窓の大きさ (正面からの角度)

        Material BubbleMat() => TransparentMat("Sea_Bubble", new Color(0.85f, 0.97f, 1f, 0.45f));

        SubmarineDive BuildSubmarine()
        {
            var go = new GameObject("SubmarineDive");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;
            var shown = new List<GameObject>();
            subKelp.Clear();
            subSigns.Clear();

            var sand = Mat("Sea_Sand", new Color(0.86f, 0.78f, 0.58f), null, false, 0.1f);
            var sandDark = Mat("Sea_SandDark", new Color(0.55f, 0.48f, 0.34f), null, false, 0.1f);
            var bubble = BubbleMat();

            // ---- 1. 足元から広がる砂の床 (ハッチと屋台の足元を覆う。屋台はここに斜めに埋もれていく) ----
            var cover = Group(root, "SandCover");
            var sandFloor = new GameObject("SandFloor"); // 真ん中は潜水艦が通るハッチの穴
            sandFloor.transform.SetParent(cover, false);
            sandFloor.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            sandFloor.AddComponent<MeshFilter>().sharedMesh = AnnulusMesh(SubHatch, SandRadius, 64);
            sandFloor.AddComponent<MeshRenderer>().sharedMaterial = sand;
            var irisBlades = BuildSubHatch(cover);
            BuildSandSlab(cover);
            for (int i = 0; i < 26; i++) // 砂の小山と、砂紋の筋
            {
                var p = Polar(Random.Range(0f, 360f), Random.Range(SubHatch + 0.7f, SandRadius - 1f), 0.01f);
                if (i % 2 == 0)
                    Prim(PrimitiveType.Sphere, "SandMound", cover, p, new Vector3(Random.Range(0.6f, 1.6f), Random.Range(0.06f, 0.14f), Random.Range(0.5f, 1.2f)), sandDark, false);
                else
                    Prim(PrimitiveType.Cube, "SandRipple", cover, p + Vector3.up * 0.008f, new Vector3(Random.Range(0.8f, 2f), 0.006f, 0.05f), sandDark, false)
                        .transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            // ---- 2. 真ん中から一気に広がる海の水面 (上から見ると青い海。もぐってからは天井の光る水面) ----
            var water = Group(root, "SeaWater");
            water.localPosition = new Vector3(0f, 0.03f, 0f);
            Prim(PrimitiveType.Cylinder, "Surface", water, Vector3.zero, new Vector3(2f * SeaRadius, 0.004f, 2f * SeaRadius),
                 TransparentMat("Sea_Water", new Color(0.1f, 0.42f, 0.7f, 0.8f)), false);
            var below = Prim(PrimitiveType.Quad, "SurfaceBelow", water, new Vector3(0f, -0.02f, 0f), new Vector3(2f * SeaRadius, 2f * SeaRadius, 1f),
                             TransparentMat("Sea_SurfaceBelow", new Color(0.55f, 0.88f, 1f, 0.88f)), false);
            below.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 下からだけ見える (もぐってから見上げると光る水面。地上の物が透けないように濃いめ)
            var ripples = new List<Transform>();
            var rippleMat = TransparentMat("Sea_Ripple", new Color(0.85f, 0.97f, 1f, 0.3f));
            for (int i = 0; i < 4; i++)
            {
                var ring = new GameObject("Ripple");
                ring.transform.SetParent(water, false);
                ring.transform.localPosition = new Vector3(0f, 0.006f, 0f);
                ring.AddComponent<MeshFilter>().sharedMesh = AnnulusMesh(SandRadius * 0.96f, SandRadius, 64);
                ring.AddComponent<MeshRenderer>().sharedMaterial = rippleMat;
                ripples.Add(ring.transform);
            }

            // ---- 3. 海の中 (水面から差しこむ光の筋) ----
            var sea = Group(root, "Sea");
            sea.localPosition = new Vector3(0f, -SubDepth, 0f);
            shown.Add(sea.gameObject);
            float surfaceY = SubDepth; // 海のローカルでの水面の高さ
            var rayMat = TransparentMat("Sea_LightRay", new Color(0.75f, 0.95f, 1f, 0.05f));
            for (int i = 0; i < 18; i++) // 水面から斜めに差しこむ光の筋 (遠くの筋は霧にかすんで、奥行きが分かる)
            {
                float a = (i * 20f + Random.Range(-8f, 8f)) * Mathf.Deg2Rad, r = i < 12 ? Random.Range(4f, 26f) : Random.Range(32f, 55f);
                var ray = new GameObject("LightRay");
                ray.transform.SetParent(sea, false);
                ray.transform.localPosition = new Vector3(Mathf.Sin(a) * r, surfaceY, Mathf.Cos(a) * r);
                ray.transform.localRotation = Quaternion.Euler(90f + Random.Range(-12f, 12f), 0f, Random.Range(-12f, 12f)); // 先が上、下へ広がる
                float w = Random.Range(1.2f, 2.8f);
                ray.transform.localScale = new Vector3(w, w, surfaceY - SeaFloor + 1f);
                ray.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
                ray.AddComponent<MeshRenderer>().sharedMaterial = rayMat;
            }

            // ---- 4. 海の底 ----
            BuildSeaFloor(sea, sand, sandDark);
            BuildFarWhales(sea);
            var vents = BuildSeaVents(sea);
            BuildCoralReef(sea);
            BuildKelpForest(sea);
            var wreck = BuildShipwreck(sea, 40f, 18f);
            var temple = BuildSeaTemple(sea, 205f, 18f);
            BuildRockArch(sea, 125f, 24f);
            var motes = BuildMarineSnow(sea);
            var schools = BuildFishSchools(sea);

            // ---- 5. 的 ----
            var creatures = BuildSeaCreatures(sea, bubble);
            creatures.AddRange(BuildAmphoras(sea, temple, bubble));
            var chests = new List<TreasureChest>
            {
                BuildTreasureChest(sea, "Chest_Wreck", wreck.chestSpot, bubble),
                BuildTreasureChest(sea, "Chest_Temple", temple.chestSpot, bubble),
                BuildTreasureChest(sea, "Chest_ReefA", Polar(135f, 9f, SeaFloor), bubble),
                BuildTreasureChest(sea, "Chest_ReefB", Polar(305f, 10.5f, SeaFloor), bubble),
            };

            // 海の中は暗くて霧が濃いので、影は落とさない (重くしないように)
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ---- 6. 丸い潜水艦 (縦穴・海とは別。いつもは隠しておき、閉じる時に出る) ----
            var pod = BuildSubPod(root, bubble);

            var dive = go.AddComponent<SubmarineDive>();
            dive.cover = cover.gameObject;
            dive.water = water;
            dive.ripples = ripples.ToArray();
            dive.pod = pod;
            dive.showDuringStage = shown.ToArray();
            dive.depth = SubDepth;
            dive.sandPuffMaterial = TransparentMat("Sea_SandPuff", new Color(0.88f, 0.78f, 0.58f, 0.7f));
            dive.creatures = creatures.ToArray();
            dive.chests = chests.ToArray();
            dive.bubbleVents = vents.ToArray();
            dive.bubbleMaterial = bubble;
            dive.motes = motes.ToArray();
            dive.kelp = subKelp.ToArray();
            dive.schools = schools.ToArray();
            dive.pointSigns = subSigns.ToArray();
            dive.irisBlades = irisBlades.ToArray();
            // 潜水艦モードの時だけ出る (シーンに生成した時も隠しておく)
            cover.gameObject.SetActive(false);
            water.gameObject.SetActive(false);
            foreach (var g in shown) g.SetActive(false);
            pod.gameObject.SetActive(false);
            go.SetActive(true);
            return dive;
        }

        readonly List<Transform> subKelp = new List<Transform>(); // ゆらゆら揺れる海藻の節 (SubmarineDive が揺らす)

        const float SandSlab = 0.4f; // 砂の床の厚み (下から見上げた時に、床の上の物が見えないように包む)

        /// <summary>
        /// 砂の床を、厚みのある平らな砂の板で包む: 平らな裏面 (真ん中にハッチの穴)、ハッチの穴の内側の壁、まわりのふちの壁。
        /// 潜っていく途中で見上げても、砂の小山・開いた絞り羽根・地面の穴のふち・埋もれていく屋台の根元などが見えず、
        /// 継ぎ目のない砂の天井に、ドレミのハッチの丸い穴が1つあいているだけに見える
        /// </summary>
        void BuildSandSlab(Transform cover)
        {
            var under = Mat("Sea_SandUnder", new Color(0.66f, 0.58f, 0.42f), null, false, 0.1f);
            var slab = Group(cover, "SandSlab");
            // 平らな裏面 (表裏とも描かれる面。上は砂の床の下に隠れる)
            var bottom = new GameObject("SlabBottom");
            bottom.transform.SetParent(slab, false);
            bottom.transform.localPosition = new Vector3(0f, -SandSlab, 0f);
            bottom.AddComponent<MeshFilter>().sharedMesh = AnnulusMesh(SubHatch, SandRadius, 64);
            bottom.AddComponent<MeshRenderer>().sharedMaterial = under;
            // ハッチの穴の内側の壁と、まわりのふちの壁 (地面の高さから裏面まで)
            foreach (var (name, radius, mat) in new[] { ("HatchWall", SubHatch, Mat("SubHatch_Wall", new Color(0.2f, 0.32f, 0.44f), null, false, 0.6f)), ("SlabEdge", SandRadius, under) })
            {
                var wall = new GameObject(name);
                wall.transform.SetParent(slab, false);
                wall.transform.localScale = new Vector3(radius, SandSlab, radius);
                wall.AddComponent<MeshFilter>().sharedMesh = TubeMesh(name == "HatchWall" ? 32 : 64);
                wall.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            // 下から見たハッチの縁取り (真鍮の輪)
            FlatRing(slab, "HatchUnderRim", SubHatch + 0.05f, -SandSlab - 0.015f, 0.1f, 0.03f, 32, Mat("Sub_Brass", new Color(0.85f, 0.65f, 0.28f), null, false, 0.8f));
        }

        /// <summary>
        /// 砂の床の真ん中の、潜水艦が通るハッチ (ボーナスステージのドレミのハッチと同じ絞り羽根。8枚が渦を巻いて開き、砂の下に隠れる)。
        /// ふちに真鍮の輪と虹色の小さなランプ。開閉は SubmarineDive (羽根の付け根の縦の軸のまわりに回す)
        /// </summary>
        List<Transform> BuildSubHatch(Transform cover)
        {
            const int Blades = 8;
            const float Lean = 20f;                     // 羽根が中心から少しずれた向きに伸びる角度 (渦巻きに重なる)
            const float BladeLength = SubHatch * 1.3f;
            const float BladeWidth = SubHatch;
            const float BladeThickness = 0.006f;
            const float BladeStep = 0.008f;             // 羽根1枚ごとの高さの差 (重なった羽根がちらつかないように。全部、砂の床の面より下)
            var bladeMats = new[]
            {
                Mat("SubHatch_Blade_A", new Color(0.18f, 0.3f, 0.42f), null, false, 0.75f),
                Mat("SubHatch_Blade_B", new Color(0.24f, 0.38f, 0.5f), null, false, 0.75f)
            };
            var edgeMat = Mat("Iris_Edge", new Color(1f, 0.75f, 0.3f), new Color(0.5f, 0.32f, 0.05f), false, 0.7f);
            var blades = new List<Transform>();
            for (int i = 0; i < Blades; i++)
            {
                float a = i * Mathf.PI * 2f / Blades;
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var pivot = Group(cover, $"SubHatchBlade_{i}");
                pivot.localPosition = outward * SubHatch + Vector3.up * (0.008f - i * BladeStep);
                pivot.localRotation = Quaternion.LookRotation(Quaternion.Euler(0f, Lean, 0f) * -outward);
                Prim(PrimitiveType.Cube, "Blade", pivot, new Vector3(0f, 0f, BladeLength / 2f), new Vector3(BladeWidth, BladeThickness, BladeLength), bladeMats[i % 2], false);
                Prim(PrimitiveType.Cube, "Edge", pivot, new Vector3(BladeWidth / 2f - 0.015f, BladeThickness / 2f + 0.001f, BladeLength / 2f), new Vector3(0.03f, 0.002f, BladeLength), edgeMat, false);
                blades.Add(pivot);
            }
            // ふちの真鍮の輪と、ド レ ミ ファ ソ ラ シ ド の虹色のランプ
            FlatRing(cover, "SubHatchRim", SubHatch + 0.06f, 0.025f, 0.12f, 0.03f, 32, Mat("Sub_Brass", new Color(0.85f, 0.65f, 0.28f), null, false, 0.8f));
            Color[] rainbow = { new(1f, 0.25f, 0.25f), new(1f, 0.6f, 0.2f), new(1f, 0.95f, 0.25f), new(0.35f, 1f, 0.35f), new(0.3f, 0.85f, 1f), new(0.3f, 0.45f, 1f), new(0.7f, 0.35f, 1f), new(1f, 0.45f, 0.85f) };
            for (int i = 0; i < rainbow.Length; i++)
                Prim(PrimitiveType.Sphere, "SubHatchLamp", cover, Polar(i * 45f + 22.5f, SubHatch + 0.06f, 0.05f), Vector3.one * 0.07f, Mat($"SubHatch_Lamp{i}", rainbow[i], rainbow[i] * 1.6f), false);
            return blades;
        }

        /// <summary>海のローカルで、プレイヤーの真下から見た角度 (度) と距離の所</summary>
        static Vector3 Polar(float deg, float dist, float y)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * dist, y, Mathf.Cos(a) * dist);
        }

        /// <summary>pos からプレイヤーの方 (海の真ん中) を -Z が向く回転 (表がプレイヤー向き)</summary>
        static Quaternion FacingCenter(Vector3 pos)
        {
            var d = new Vector3(pos.x, 0f, pos.z);
            return d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d.normalized) : Quaternion.identity;
        }

        // ------------------------------------------------------------------
        // 丸い潜水艦
        // ------------------------------------------------------------------

        SubmarinePod BuildSubPod(Transform root, Material bubble)
        {
            var go = new GameObject("SubmarinePod");
            go.transform.SetParent(root, false);
            var pod = go.transform;
            var hull = Mat("Sub_Hull", new Color(1f, 0.78f, 0.12f), new Color(0.12f, 0.08f, 0f), false, 0.65f);
            var inner = Mat("Sub_HullInner", new Color(0.32f, 0.36f, 0.42f), null, false, 0.35f);
            var brass = Mat("Sub_Brass", new Color(0.85f, 0.65f, 0.28f), null, false, 0.8f);
            var steel = Mat("Sub_Steel", new Color(0.4f, 0.42f, 0.46f), null, false, 0.7f);
            float sinW = Mathf.Sin(PodWindowDeg * Mathf.Deg2Rad), cosW = Mathf.Cos(PodWindowDeg * Mathf.Deg2Rad);
            float rw = PodRadius * sinW, zw = PodRadius * cosW; // 窓の半径と、窓の面の前後の位置

            // 上下の半球 (球の真ん中を通る左右の軸のまわりに、パックンのように開く)
            var upper = Group(pod, "Upper");
            var lower = Group(pod, "Lower");
            foreach (var (half, isUpper) in new[] { (upper, true), (lower, false) })
            {
                var shell = new GameObject("Shell");
                shell.transform.SetParent(half, false);
                shell.AddComponent<MeshFilter>().sharedMesh = PodShellMesh(PodRadius, 0.05f, PodWindowDeg, isUpper);
                var mr = shell.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { hull, inner }; // 外側と切り口は黄色、内側は灰色
            }
            // 窓のふちの真鍮の輪とびょう (上半分は上の半球、下半分は下の半球に付ける)
            const int RimSegs = 28;
            for (int i = 0; i < RimSegs; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / RimSegs;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var half = radial.y >= 0f ? upper : lower;
                var seg = Prim(PrimitiveType.Cube, "WindowRim", half, radial * (rw + 0.035f) + Vector3.forward * (zw + 0.01f),
                               new Vector3(2f * Mathf.PI * (rw + 0.035f) / RimSegs + 0.01f, 0.07f, 0.05f), brass, false);
                seg.transform.localRotation = Quaternion.LookRotation(Vector3.forward, radial);
                if (i % 2 == 0)
                    Prim(PrimitiveType.Sphere, "Rivet", half, radial * (rw + 0.035f) + Vector3.forward * (zw + 0.04f), Vector3.one * 0.025f, steel, false);
            }
            // 上の半球: てっぺんのハッチ・潜望鏡・前の2つのライト
            Prim(PrimitiveType.Cylinder, "TopHatch", upper, new Vector3(0f, PodRadius * 0.99f, -0.12f), new Vector3(0.38f, 0.03f, 0.38f), steel, false);
            Prim(PrimitiveType.Cylinder, "HatchWheel", upper, new Vector3(0f, PodRadius * 0.99f + 0.05f, -0.12f), new Vector3(0.22f, 0.012f, 0.22f), brass, false);
            Prim(PrimitiveType.Cylinder, "Periscope", upper, new Vector3(0.28f, PodRadius * 0.95f + 0.2f, -0.28f), new Vector3(0.07f, 0.25f, 0.07f), steel, false);
            Prim(PrimitiveType.Cube, "PeriscopeHead", upper, new Vector3(0.28f, PodRadius * 0.95f + 0.45f, -0.24f), new Vector3(0.1f, 0.1f, 0.18f), steel, false);
            var lampMat = Mat("Sub_Lamp", new Color(1f, 0.97f, 0.85f), new Color(2.5f, 2.4f, 2f));
            foreach (float b in new[] { 55f, 125f })
            {
                float al = (PodWindowDeg + 12f) * Mathf.Deg2Rad, bl = b * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(al) * Mathf.Cos(bl), Mathf.Sin(al) * Mathf.Sin(bl), Mathf.Cos(al));
                Prim(PrimitiveType.Cylinder, "LampHousing", upper, d * (PodRadius + 0.02f), new Vector3(0.14f, 0.04f, 0.14f), steel, false).transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
                Prim(PrimitiveType.Sphere, "Lamp", upper, d * (PodRadius + 0.05f), new Vector3(0.11f, 0.11f, 0.11f), lampMat, false);
            }
            // 下の半球: 後ろの大きなプロペラと左右の小さなプロペラ (回っている)、泡の出る所
            var vents = new List<Transform>();
            var props = new List<(Vector3 pos, float size)> { (new Vector3(0f, -0.15f, -PodRadius - 0.08f), 1f), (new Vector3(-0.78f, -0.45f, -0.45f), 0.6f), (new Vector3(0.78f, -0.45f, -0.45f), 0.6f) };
            foreach (var (pos, size) in props)
            {
                Prim(PrimitiveType.Cylinder, "PropHousing", lower, pos + Vector3.forward * 0.08f * size, new Vector3(0.22f, 0.1f, 0.22f) * size, steel, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var prop = Group(lower, "Propeller");
                prop.localPosition = pos - Vector3.forward * 0.05f * size;
                var rot = prop.gameObject.AddComponent<Rotator>();
                rot.axis = Vector3.forward;
                rot.speed = 420f;
                Prim(PrimitiveType.Sphere, "Hub", prop, Vector3.zero, Vector3.one * 0.07f * size, brass, false);
                for (int k = 0; k < 3; k++)
                {
                    var blade = Prim(PrimitiveType.Cube, "Blade", prop, Quaternion.Euler(0f, 0f, k * 120f) * new Vector3(0f, 0.1f * size, 0f), new Vector3(0.07f, 0.18f, 0.012f) * size, brass, false);
                    blade.transform.localRotation = Quaternion.Euler(0f, 0f, k * 120f) * Quaternion.Euler(0f, 30f, 0f);
                }
                vents.Add(prop);
            }
            // 下の半球の内側: 窓の下の計器盤 (深さの針・回るソナー・光るボタン)
            var dash = Group(lower, "Dashboard");
            var panel = Mat("Sub_Panel", new Color(0.18f, 0.2f, 0.24f), null, false, 0.4f);
            for (int i = -4; i <= 4; i++)
            {
                float b = i * 12f * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b));
                var p = Prim(PrimitiveType.Cube, "Panel", dash, d * 0.8f + Vector3.up * -0.48f, new Vector3(0.18f, 0.16f, 0.03f), panel, false);
                p.transform.localRotation = Quaternion.LookRotation(d) * Quaternion.Euler(-40f, 0f, 0f);
            }
            var btnMats = new[] { Mat("Sub_BtnRed", new Color(1f, 0.25f, 0.2f), new Color(1.5f, 0.2f, 0.1f)), Mat("Sub_BtnGreen", new Color(0.3f, 1f, 0.4f), new Color(0.2f, 1.4f, 0.3f)), Mat("Sub_BtnAmber", new Color(1f, 0.75f, 0.2f), new Color(1.5f, 0.9f, 0.1f)) };
            for (int i = 0; i < 12; i++)
            {
                float b = (-44f + i * 8f) * Mathf.Deg2Rad;
                if (Mathf.Abs(b) < 14f * Mathf.Deg2Rad) continue; // 真ん中は計器
                var d = new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b));
                Prim(PrimitiveType.Sphere, "Button", dash, d * 0.78f + Vector3.up * (-0.45f + (i % 2) * 0.05f), Vector3.one * 0.022f, btnMats[i % 3], false);
            }
            // 深さ計 (白い文字盤と赤い針) とソナー (緑の画面に回る線)
            var gauge = Group(dash, "DepthGauge");
            gauge.localPosition = new Vector3(-0.09f, -0.44f, 0.76f);
            gauge.localRotation = Quaternion.Euler(-40f, 180f, 0f); // 文字盤がプレイヤーの方を向く
            Prim(PrimitiveType.Cylinder, "Face", gauge, Vector3.zero, new Vector3(0.11f, 0.004f, 0.11f), Mat("Sub_GaugeFace", new Color(0.95f, 0.95f, 0.9f), new Color(0.25f, 0.25f, 0.22f)), false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var needle = Group(gauge, "Needle");
            needle.localPosition = new Vector3(0f, 0f, -0.006f);
            Prim(PrimitiveType.Cube, "NeedleBar", needle, new Vector3(0f, 0.022f, 0f), new Vector3(0.006f, 0.045f, 0.003f), Mat("Sub_Needle", new Color(1f, 0.15f, 0.1f), new Color(0.8f, 0.05f, 0f)), false);
            var sonar = Group(dash, "Sonar");
            sonar.localPosition = new Vector3(0.09f, -0.44f, 0.76f);
            sonar.localRotation = Quaternion.Euler(-40f, 180f, 0f);
            Prim(PrimitiveType.Cylinder, "Screen", sonar, Vector3.zero, new Vector3(0.12f, 0.004f, 0.12f), Mat("Sub_Sonar", new Color(0.05f, 0.35f, 0.15f), new Color(0.05f, 0.5f, 0.15f)), false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var sweep = Group(sonar, "Sweep");
            sweep.localPosition = new Vector3(0f, 0f, -0.006f);
            var sweepRot = sweep.gameObject.AddComponent<Rotator>();
            sweepRot.axis = Vector3.forward;
            sweepRot.speed = 140f;
            Prim(PrimitiveType.Cube, "SweepLine", sweep, new Vector3(0f, 0.028f, 0f), new Vector3(0.004f, 0.055f, 0.002f), Mat("Sub_SonarLine", new Color(0.5f, 1f, 0.6f), new Color(0.6f, 2f, 0.7f), true), false);

            // レンズの窓 (左はしのちょうつがいで、ドアのように外へ開く)
            var door = Group(pod, "LensDoor");
            door.localPosition = new Vector3(-rw - 0.035f, 0f, zw + 0.03f);
            var leaf = Group(door, "Leaf");
            leaf.localPosition = new Vector3(rw + 0.035f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "Lens", leaf, Vector3.zero, new Vector3(2f * (rw + 0.03f), 2f * (rw + 0.03f), 0.16f), TransparentMat("Sub_Lens", new Color(0.7f, 0.93f, 1f, 0.14f)), false);
            for (int i = 0; i < RimSegs; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / RimSegs;
                var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var seg = Prim(PrimitiveType.Cube, "LensRim", leaf, radial * (rw + 0.02f), new Vector3(2f * Mathf.PI * (rw + 0.02f) / RimSegs + 0.01f, 0.05f, 0.06f), brass, false);
                seg.transform.localRotation = Quaternion.LookRotation(Vector3.forward, radial);
                if (i % 3 == 0) Prim(PrimitiveType.Sphere, "LensRivet", leaf, radial * (rw + 0.02f) + Vector3.forward * 0.03f, Vector3.one * 0.022f, steel, false);
            }
            Prim(PrimitiveType.Cube, "LensHandle", leaf, new Vector3(rw - 0.02f, 0f, 0.06f), new Vector3(0.03f, 0.14f, 0.03f), steel, false);
            foreach (float y in new[] { -0.3f, 0.3f })
                Prim(PrimitiveType.Cylinder, "Hinge", door, new Vector3(0f, y * rw, 0f), new Vector3(0.05f, 0.06f, 0.05f), steel, false);

            // 前を照らすライト (窓といっしょに視線の方を向く)
            var lightGo = new GameObject("Headlight");
            lightGo.transform.SetParent(pod, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.45f, 0.5f);
            lightGo.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            var spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 24f;
            spot.spotAngle = 70f;
            spot.intensity = 3f;
            spot.color = new Color(0.92f, 0.97f, 1f);
            spot.shadows = LightShadows.None;

            foreach (var r in pod.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var podComp = go.AddComponent<SubmarinePod>();
            podComp.upper = upper;
            podComp.lower = lower;
            podComp.lensDoor = door;
            podComp.headlight = spot;
            podComp.depthNeedle = needle;
            podComp.bubbleVents = vents.ToArray();
            podComp.bubbleMaterial = bubble;
            return podComp;
        }

        /// <summary>
        /// 潜水艦の半球の殻 (厚みのある球の半分。正面に丸い窓の穴)。正面 (+Z) を極にした球面座標で作るので、窓のふちがきれいな円になる。
        /// upper = 上半分 (y ≧ 0) / 下半分。サブメッシュ 0 = 外側と切り口、1 = 内側
        /// </summary>
        Mesh PodShellMesh(float r, float thick, float windowDeg, bool upper)
        {
            string name = $"Shape_PodShell_{(upper ? "U" : "L")}_{r:0.00}_{windowDeg:0}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int NA = 18, NB = 24;
            float a0 = windowDeg * Mathf.Deg2Rad, b0 = upper ? 0f : Mathf.PI;
            float ri = r - thick;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var outerTris = new List<int>();
            var innerTris = new List<int>();
            Vector3 Dir(int i, int j)
            {
                float a = Mathf.Lerp(a0, Mathf.PI, i / (float)NA), b = b0 + Mathf.PI * j / NB;
                return new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Sin(a) * Mathf.Sin(b), Mathf.Cos(a));
            }
            void Quad(List<int> tris, int v0, int v1, int v2, int v3, Vector3 want)
            {
                var n = Vector3.Cross(verts[v1] - verts[v0], verts[v2] - verts[v0]) + Vector3.Cross(verts[v2] - verts[v0], verts[v3] - verts[v0]);
                if (Vector3.Dot(n, want) >= 0f) tris.AddRange(new[] { v0, v1, v2, v0, v2, v3 });
                else tris.AddRange(new[] { v0, v2, v1, v0, v3, v2 });
            }
            // 外側の面と内側の面
            foreach (bool outer in new[] { true, false })
            {
                int start = verts.Count;
                for (int i = 0; i <= NA; i++)
                    for (int j = 0; j <= NB; j++)
                    {
                        var d = Dir(i, j);
                        verts.Add(d * (outer ? r : ri));
                        normals.Add(outer ? d : -d);
                    }
                for (int i = 0; i < NA; i++)
                    for (int j = 0; j < NB; j++)
                    {
                        int v0 = start + i * (NB + 1) + j;
                        var mid = Dir(i, j) + Dir(i + 1, j + 1);
                        Quad(outer ? outerTris : innerTris, v0, v0 + NB + 1, v0 + NB + 2, v0 + 1, outer ? mid : -mid);
                    }
            }
            // 切り口 (外側と内側をつなぐ平らな面。半球の左右のふちと、窓のふち)
            void Rim(Vector3 p0, Vector3 p1, Vector3 want)
            {
                int k = verts.Count;
                verts.Add(p0 * r); verts.Add(p1 * r); verts.Add(p1 * ri); verts.Add(p0 * ri);
                for (int n = 0; n < 4; n++) normals.Add(want.normalized);
                Quad(outerTris, k, k + 1, k + 2, k + 3, want);
            }
            for (int i = 0; i < NA; i++)
            {
                // 左右のふち (j = 0 と j = NB)。外向き = 半球の外へ (角度が減る / 増える方向)
                var d0 = Dir(i, 0); var d1 = Dir(i + 1, 0);
                Rim(d0, d1, upper ? Vector3.down : Vector3.up);
                d0 = Dir(i, NB); d1 = Dir(i + 1, NB);
                Rim(d0, d1, upper ? Vector3.down : Vector3.up);
            }
            for (int j = 0; j < NB; j++)
            {
                var d0 = Dir(0, j); var d1 = Dir(0, j + 1);
                var mid = (d0 + d1) * 0.5f;
                var toward = Vector3.forward - mid * Vector3.Dot(Vector3.forward, mid); // 窓の真ん中の方 (球面にそって)
                Rim(d0, d1, toward);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(outerTris, 0);
            mesh.SetTriangles(innerTris, 1);
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------
        // 海の底の地形
        // ------------------------------------------------------------------

        void BuildSeaFloor(Transform sea, Material sand, Material sandDark)
        {
            // 砂地 (魚雷が当たって爆発するように当たり判定あり)
            Prim(PrimitiveType.Cylinder, "Seabed", sea, new Vector3(0f, SeaFloor - 0.05f, 0f), new Vector3(320f, 0.05f, 320f), sand);
            for (int i = 0; i < 56; i++) // ゆるやかな砂の丘 (遠くまで続く)
            {
                var p = Polar(Random.Range(0f, 360f), i < 40 ? Random.Range(5f, 34f) : Random.Range(34f, 70f), SeaFloor - 0.1f);
                Prim(PrimitiveType.Sphere, "Dune", sea, p, new Vector3(Random.Range(3f, 7f), Random.Range(0.3f, 0.8f), Random.Range(3f, 6f)), i % 3 == 0 ? sandDark : sand, false)
                    .transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
            var rock = Mat("Sea_Rock", new Color(0.34f, 0.38f, 0.42f), null, false, 0.3f);
            var rockDark = Mat("Sea_RockDark", new Color(0.2f, 0.24f, 0.28f), null, false, 0.3f);
            for (int i = 0; i < 36; i++) // 岩
            {
                var p = Polar(Random.Range(0f, 360f), Random.Range(5f, 30f), SeaFloor);
                float s = Random.Range(0.6f, 2.4f);
                Rock("SeaRock", sea, p, new Vector3(s * Random.Range(0.8f, 1.4f), s * Random.Range(0.5f, 1f), s), i % 3 == 0 ? rockDark : rock);
            }
            // 遠くの景色を何段にも重ねて、海がずっと広がっているように見せる (遠いほど霧にかすむ):
            //   1. 中くらいの所に、ぽつぽつと立つ岩の塔 (すき間から、その奥が見える)
            for (int i = 0; i < 14; i++)
            {
                var p = Polar(i * 360f / 14f + Random.Range(-10f, 10f), Random.Range(36f, 50f), SeaFloor);
                float h = Random.Range(6f, 16f);
                Rock("Pinnacle", sea, p, new Vector3(Random.Range(2.5f, 4.5f), h, Random.Range(2.5f, 4.5f)), rockDark);
                Rock("PinnacleBase", sea, p + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1.5f, 1.5f)), new Vector3(Random.Range(4f, 7f), Random.Range(2f, 4f), Random.Range(4f, 7f)), rock);
            }
            //   2. ずっと遠くの、大きな海底の山なみ (霧の向こうに影だけ見える。すき間をあけて、さらに奥へ続いているように)
            for (int i = 0; i < 18; i++)
            {
                if (i % 6 == 2) continue; // 山なみの切れ目 (その先は果てしない青)
                var p = Polar(i * 360f / 18f + Random.Range(-6f, 6f), Random.Range(72f, 90f), SeaFloor + Random.Range(-2f, 2f));
                Rock("FarRidge", sea, p, new Vector3(Random.Range(16f, 26f), Random.Range(14f, 30f), Random.Range(12f, 18f)), rockDark);
            }
            // 足元 (潜水艦の真下) の小さな岩とサンゴ (下をのぞくと見える)
            var corals = CoralMaterials();
            for (int i = 0; i < 10; i++)
            {
                var p = Polar(i * 36f + Random.Range(-10f, 10f), Random.Range(1.8f, 4f), SeaFloor);
                if (i % 2 == 0) Rock("FootRock", sea, p, Vector3.one * Random.Range(0.4f, 0.8f), rock);
                else BrainCoral(sea, p, Random.Range(0.4f, 0.7f), corals[i % corals.Length]);
            }
            // ヒトデ・貝・ウニ
            var star = new[] { Mat("Sea_StarRed", new Color(0.95f, 0.3f, 0.25f), null, false, 0.2f), Mat("Sea_StarOrange", new Color(1f, 0.6f, 0.2f), null, false, 0.2f) };
            var shell = Mat("Sea_Shell", new Color(0.98f, 0.88f, 0.8f), null, false, 0.6f);
            var urchin = Mat("Sea_Urchin", new Color(0.18f, 0.08f, 0.25f), null, false, 0.4f);
            for (int i = 0; i < 30; i++)
            {
                var p = Polar(Random.Range(0f, 360f), Random.Range(2.5f, 20f), SeaFloor + 0.02f);
                var g = Group(sea, "SeabedDecor");
                g.localPosition = p;
                g.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                switch (i % 3)
                {
                    case 0:
                        for (int k = 0; k < 5; k++)
                            Prim(PrimitiveType.Cube, "Arm", g, Quaternion.Euler(0f, k * 72f, 0f) * new Vector3(0f, 0.02f, 0.11f), new Vector3(0.07f, 0.035f, 0.22f), star[i % 2], false)
                                .transform.localRotation = Quaternion.Euler(0f, k * 72f, 0f);
                        break;
                    case 1:
                        Prim(PrimitiveType.Sphere, "Shell", g, new Vector3(0f, 0.04f, 0f), new Vector3(0.22f, 0.08f, 0.2f), shell, false);
                        break;
                    default:
                        Prim(PrimitiveType.Sphere, "Urchin", g, new Vector3(0f, 0.08f, 0f), Vector3.one * 0.18f, urchin, false);
                        for (int k = 0; k < 8; k++)
                            Prim(PrimitiveType.Cube, "Spine", g, new Vector3(0f, 0.08f, 0f), new Vector3(0.012f, 0.32f, 0.012f), urchin, false).transform.localRotation = Random.rotation;
                        break;
                }
            }
        }

        /// <summary>
        /// 遠くをゆっくり泳ぐ大きなクジラの影 (霧にかすんで、影だけが見える)。海の広さと深さを感じさせる。
        /// 真ん中を中心にゆっくり回る台 (Rotator) の先に置くだけなので軽い。的ではない (当たり判定なし)
        /// </summary>
        void BuildFarWhales(Transform sea)
        {
            var dark = Mat("Sea_WhaleShadow", new Color(0.12f, 0.2f, 0.28f), null, false, 0.3f);
            var belly = Mat("Sea_WhaleShadowBelly", new Color(0.25f, 0.35f, 0.42f), null, false, 0.3f);
            foreach (var (radius, height, size, speed, start) in new[] { (48f, 9f, 1f, 2.2f, 30f), (62f, 15f, 1.4f, -1.6f, 200f) })
            {
                var pivot = Group(sea, "FarWhalePivot");
                pivot.localRotation = Quaternion.Euler(0f, start, 0f);
                var spin = pivot.gameObject.AddComponent<Rotator>();
                spin.axis = Vector3.up;
                spin.speed = speed; // 度/秒 (とてもゆっくり)
                var w = Group(pivot, "FarWhale");
                w.localPosition = new Vector3(0f, SeaFloor + height, radius);
                w.localRotation = Quaternion.Euler(0f, speed > 0f ? 90f : -90f, 0f); // 回る向きに頭 (+Z) を向ける
                w.localScale = Vector3.one * size;
                Prim(PrimitiveType.Sphere, "Body", w, Vector3.zero, new Vector3(2.4f, 2f, 9f), dark, false);
                Prim(PrimitiveType.Sphere, "Belly", w, new Vector3(0f, -0.45f, 0.6f), new Vector3(2f, 1.3f, 7f), belly, false);
                Prim(PrimitiveType.Capsule, "Tail", w, new Vector3(0f, 0.1f, -5f), new Vector3(1f, 1.6f, 1f), dark, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Sphere, "Fluke", w, new Vector3(s * 1f, 0.1f, -6.4f), new Vector3(2.2f, 0.15f, 0.9f), dark, false).transform.localRotation = Quaternion.Euler(0f, s * 25f, 0f);
                    Prim(PrimitiveType.Sphere, "Flipper", w, new Vector3(s * 1.5f, -0.6f, 1.5f), new Vector3(3f, 0.15f, 0.7f), dark, false).transform.localRotation = Quaternion.Euler(0f, -s * 30f, s * 20f);
                }
            }
        }

        // 泡が立ちのぼる海底の割れ目
        List<Transform> BuildSeaVents(Transform sea)
        {
            var vents = new List<Transform>();
            var rock = Mat("Sea_RockDark", new Color(0.2f, 0.24f, 0.28f), null, false, 0.3f);
            float[] angles = { 20f, 95f, 160f, 240f, 290f, 335f };
            foreach (float deg in angles)
            {
                var p = Polar(deg, Random.Range(6f, 14f), SeaFloor);
                for (int k = 0; k < 3; k++)
                    Rock("VentRock", sea, p + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f)), Vector3.one * Random.Range(0.4f, 0.8f), rock);
                var v = Group(sea, "BubbleVent");
                v.localPosition = p + Vector3.up * 0.3f;
                vents.Add(v);
            }
            return vents;
        }

        Material[] CoralMaterials() => new[]
        {
            Mat("Coral_Pink", new Color(1f, 0.45f, 0.6f), new Color(0.2f, 0.05f, 0.08f), false, 0.3f),
            Mat("Coral_Orange", new Color(1f, 0.55f, 0.2f), new Color(0.2f, 0.08f, 0f), false, 0.3f),
            Mat("Coral_Purple", new Color(0.62f, 0.35f, 0.9f), new Color(0.1f, 0.04f, 0.18f), false, 0.3f),
            Mat("Coral_Yellow", new Color(1f, 0.88f, 0.3f), new Color(0.2f, 0.16f, 0.02f), false, 0.3f),
            Mat("Coral_Teal", new Color(0.2f, 0.85f, 0.75f), new Color(0.02f, 0.18f, 0.15f), false, 0.3f),
            Mat("Coral_Red", new Color(0.92f, 0.2f, 0.25f), new Color(0.2f, 0.02f, 0.03f), false, 0.3f),
        };

        // サンゴ礁: 枝サンゴ・脳サンゴ・うちわサンゴ・管サンゴ・イソギンチャクを、かたまりにして散らす
        void BuildCoralReef(Transform sea)
        {
            var mats = CoralMaterials();
            (float deg, float dist, int count)[] reefs =
            {
                (130f, 9f, 9), (150f, 13f, 7), (110f, 16f, 6), (300f, 10f, 9), (320f, 15f, 7), (270f, 7f, 6),
                (75f, 8f, 6), (180f, 7.5f, 5), (0f, 12f, 7), (230f, 12f, 6), (20f, 22f, 6), (250f, 22f, 6),
            };
            foreach (var reef in reefs)
            {
                var center = Polar(reef.deg, reef.dist, SeaFloor);
                for (int i = 0; i < reef.count; i++)
                {
                    var p = center + new Vector3(Random.Range(-2.2f, 2.2f), 0f, Random.Range(-2.2f, 2.2f));
                    var m = mats[Random.Range(0, mats.Length)];
                    float s = Random.Range(0.6f, 1.3f);
                    switch (Random.Range(0, 5))
                    {
                        case 0: BranchCoral(sea, p, s, m); break;
                        case 1: BrainCoral(sea, p, s * 0.8f, m); break;
                        case 2: FanCoral(sea, p, s, m); break;
                        case 3: TubeCoral(sea, p, s, m); break;
                        default: Anemone(sea, p, s, m); break;
                    }
                }
            }
        }

        void BranchCoral(Transform parent, Vector3 pos, float size, Material mat)
        {
            var g = Group(parent, "BranchCoral");
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            g.localScale = Vector3.one * size;
            Prim(PrimitiveType.Capsule, "Trunk", g, new Vector3(0f, 0.25f, 0f), new Vector3(0.12f, 0.28f, 0.12f), mat, false);
            int n = Random.Range(4, 7);
            for (int k = 0; k < n; k++)
            {
                var b = Prim(PrimitiveType.Capsule, "Branch", g, Vector3.zero, new Vector3(0.08f, Random.Range(0.22f, 0.35f), 0.08f), mat, false).transform;
                b.localRotation = Quaternion.Euler(Random.Range(20f, 50f), k * 360f / n + Random.Range(-15f, 15f), 0f);
                b.localPosition = new Vector3(0f, 0.4f, 0f) + b.localRotation * Vector3.up * b.localScale.y;
                var tip = Prim(PrimitiveType.Capsule, "Twig", g, Vector3.zero, new Vector3(0.06f, 0.14f, 0.06f), mat, false).transform;
                tip.localRotation = b.localRotation * Quaternion.Euler(-30f, 0f, 0f);
                tip.localPosition = b.localPosition + b.localRotation * Vector3.up * b.localScale.y + tip.localRotation * Vector3.up * 0.12f;
            }
        }

        void BrainCoral(Transform parent, Vector3 pos, float size, Material mat)
        {
            Prim(PrimitiveType.Sphere, "BrainCoral", parent, pos + Vector3.up * size * 0.2f, new Vector3(size, size * 0.65f, size), mat, false)
                .transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        void FanCoral(Transform parent, Vector3 pos, float size, Material mat)
        {
            var g = Group(parent, "FanCoral");
            g.localPosition = pos;
            g.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            g.localScale = Vector3.one * size;
            Prim(PrimitiveType.Cylinder, "Stem", g, new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.15f, 0.05f), mat, false);
            Prim(PrimitiveType.Cylinder, "Fan", g, new Vector3(0f, 0.65f, 0f), new Vector3(1f, 0.01f, 0.8f), mat, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        void TubeCoral(Transform parent, Vector3 pos, float size, Material mat)
        {
            var g = Group(parent, "TubeCoral");
            g.localPosition = pos;
            g.localScale = Vector3.one * size;
            var inside = Mat("Coral_TubeInside", new Color(0.15f, 0.08f, 0.1f));
            for (int k = 0; k < 6; k++)
            {
                float h = Random.Range(0.2f, 0.55f);
                var p = new Vector3(Random.Range(-0.25f, 0.25f), h, Random.Range(-0.25f, 0.25f));
                Prim(PrimitiveType.Cylinder, "Tube", g, p, new Vector3(0.12f, h, 0.12f), mat, false);
                Prim(PrimitiveType.Cylinder, "TubeHole", g, p + Vector3.up * (h + 0.002f), new Vector3(0.08f, 0.002f, 0.08f), inside, false);
            }
        }

        void Anemone(Transform parent, Vector3 pos, float size, Material mat)
        {
            var g = Group(parent, "Anemone");
            g.localPosition = pos;
            g.localScale = Vector3.one * size;
            Prim(PrimitiveType.Cylinder, "Base", g, new Vector3(0f, 0.08f, 0f), new Vector3(0.3f, 0.08f, 0.3f), mat, false);
            var tip = Mat("Anemone_Tip", new Color(1f, 0.95f, 0.8f), new Color(0.4f, 0.35f, 0.2f));
            for (int k = 0; k < 14; k++)
            {
                var t = Prim(PrimitiveType.Capsule, "Tentacle", g, Vector3.zero, new Vector3(0.035f, 0.13f, 0.035f), k % 4 == 0 ? tip : mat, false).transform;
                t.localRotation = Quaternion.Euler(Random.Range(10f, 45f), k * 360f / 14f, 0f);
                t.localPosition = new Vector3(0f, 0.16f, 0f) + t.localRotation * Vector3.up * 0.11f;
            }
        }

        // 海藻の森: 節ごとに揺れる長い海藻 (節は SubmarineDive が揺らす)
        void BuildKelpForest(Transform sea)
        {
            var kelp = new[] { Mat("Kelp_Green", new Color(0.3f, 0.6f, 0.22f), null, false, 0.25f), Mat("Kelp_Olive", new Color(0.45f, 0.5f, 0.18f), null, false, 0.25f), Mat("Kelp_Dark", new Color(0.15f, 0.42f, 0.2f), null, false, 0.25f) };
            for (int i = 0; i < 34; i++)
            {
                // 半分は北西の森にまとめ、残りはあちこちに
                float deg = i < 18 ? Random.Range(265f, 345f) : Random.Range(0f, 360f);
                var p = Polar(deg, i < 18 ? Random.Range(11f, 22f) : Random.Range(7f, 26f), SeaFloor);
                int segs = Random.Range(4, 7);
                float segLen = Random.Range(0.8f, 1.3f);
                var m = kelp[i % kelp.Length];
                Transform prev = Group(sea, "Kelp");
                prev.localPosition = p;
                prev.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                for (int s = 0; s < segs; s++)
                {
                    var joint = Group(prev, "KelpJoint");
                    joint.localPosition = s == 0 ? Vector3.zero : Vector3.up * segLen;
                    subKelp.Add(joint);
                    Prim(PrimitiveType.Cube, "Blade", joint, new Vector3(0f, segLen * 0.5f, 0f), new Vector3(0.28f, segLen, 0.03f), m, false);
                    Prim(PrimitiveType.Sphere, "Float", joint, new Vector3(0.12f, segLen * 0.9f, 0f), Vector3.one * 0.09f, kelp[0], false);
                    prev = joint;
                }
            }
        }

        // 岩のアーチ (くぐれそうな大きな門)
        void BuildRockArch(Transform sea, float deg, float dist)
        {
            var rock = Mat("Sea_Rock", new Color(0.34f, 0.38f, 0.42f), null, false, 0.3f);
            var g = Group(sea, "RockArch");
            g.localPosition = Polar(deg, dist, SeaFloor);
            g.localRotation = FacingCenter(g.localPosition) * Quaternion.Euler(0f, 90f, 0f);
            for (int k = 0; k < 4; k++)
            {
                Rock("ArchLeg", g, new Vector3(-3f, k * 1.4f + 0.7f, 0f), new Vector3(1.8f, 1.6f, 1.8f), rock);
                Rock("ArchLeg", g, new Vector3(3f, k * 1.4f + 0.7f, 0f), new Vector3(1.8f, 1.6f, 1.8f), rock);
            }
            for (int k = -2; k <= 2; k++)
                Rock("ArchTop", g, new Vector3(k * 1.3f, 6f + (2 - Mathf.Abs(k)) * 0.35f, 0f), new Vector3(1.8f, 1.5f, 1.8f), rock);
            var corals = CoralMaterials();
            for (int k = 0; k < 4; k++) BrainCoral(g, new Vector3(Random.Range(-3f, 3f), 6.8f, Random.Range(-0.4f, 0.4f)), 0.6f, corals[k]);
        }

        // マリンスノー (白い小さな粒がゆっくり沈んでいく)
        List<Transform> BuildMarineSnow(Transform sea)
        {
            var mat = TransparentMat("Sea_Snow", new Color(0.92f, 0.98f, 1f, 0.55f));
            var list = new List<Transform>();
            for (int i = 0; i < 90; i++)
            {
                var p = Polar(Random.Range(0f, 360f), Random.Range(1.8f, 18f), Random.Range(SeaFloor + 0.5f, 10f));
                list.Add(Prim(PrimitiveType.Sphere, "Snow", sea, p, Vector3.one * Random.Range(0.025f, 0.05f), mat, false).transform);
            }
            return list;
        }

        // 小魚の群れ (群れごと真ん中のまわりを回る。偶数番目は左回り、奇数番目は右回り: SubmarineDive と合わせる)
        List<Transform> BuildFishSchools(Transform sea)
        {
            var fishMats = new[]
            {
                Mat("Fish_Silver", new Color(0.8f, 0.85f, 0.9f), new Color(0.08f, 0.1f, 0.12f), false, 0.8f),
                Mat("Fish_Blue", new Color(0.2f, 0.5f, 1f), new Color(0.03f, 0.08f, 0.2f), false, 0.6f),
                Mat("Fish_Yellow", new Color(1f, 0.85f, 0.15f), new Color(0.15f, 0.12f, 0f), false, 0.5f),
                Mat("Fish_Clown", new Color(1f, 0.45f, 0.1f), new Color(0.15f, 0.05f, 0f), false, 0.5f),
            };
            var schools = new List<Transform>();
            for (int i = 0; i < 7; i++)
            {
                var pivot = Group(sea, "FishSchool");
                pivot.localPosition = new Vector3(0f, Random.Range(0.5f, 7f), 0f);
                pivot.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float r = Random.Range(6f, 20f), dir = i % 2 == 0 ? 1f : -1f;
                var m = fishMats[i % fishMats.Length];
                int n = Random.Range(10, 18);
                for (int k = 0; k < n; k++)
                {
                    var fish = Group(pivot, "Fish");
                    fish.localPosition = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.6f, 0.6f), r + Random.Range(-1f, 1f));
                    fish.localRotation = Quaternion.LookRotation(Vector3.right * dir); // 回る向きに泳ぐ
                    Prim(PrimitiveType.Sphere, "Body", fish, Vector3.zero, new Vector3(0.07f, 0.12f, 0.26f), m, false);
                    Prim(PrimitiveType.Cube, "Tail", fish, new Vector3(0f, 0f, -0.15f), new Vector3(0.01f, 0.1f, 0.07f), m, false).transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                }
                schools.Add(pivot);
            }
            return schools;
        }

        // ------------------------------------------------------------------
        // 沈んだ船と神殿
        // ------------------------------------------------------------------

        /// <summary>砂に半分うまって傾いた、古い木の帆船。宝箱を置く所を返す</summary>
        (Transform root, Vector3 chestSpot) BuildShipwreck(Transform sea, float deg, float dist)
        {
            var wood = Mat("Wreck_Wood", new Color(0.36f, 0.26f, 0.16f), null, false, 0.15f);
            var woodDark = Mat("Wreck_WoodDark", new Color(0.2f, 0.14f, 0.08f), null, false, 0.15f);
            var moss = Mat("Wreck_Moss", new Color(0.28f, 0.42f, 0.25f), null, false, 0.1f);
            var sail = Mat("Wreck_Sail", new Color(0.75f, 0.72f, 0.6f), null, false, 0.05f);
            var iron = Mat("Wreck_Iron", new Color(0.25f, 0.22f, 0.2f), null, false, 0.5f);
            var g = Group(sea, "Shipwreck");
            var basePos = Polar(deg, dist, SeaFloor);
            g.localPosition = basePos + Vector3.up * 0.6f;
            g.localRotation = FacingCenter(basePos) * Quaternion.Euler(0f, 70f, 0f) * Quaternion.Euler(4f, 0f, 16f); // 横向きに、少し傾いて沈んでいる
            // 船体 (左右の舷側と底と甲板。魚雷が当たるように当たり判定あり)
            Prim(PrimitiveType.Cube, "Keel", g, new Vector3(0f, 0.3f, 0f), new Vector3(2.4f, 0.8f, 11f), woodDark);
            foreach (float s in new[] { -1f, 1f })
            {
                var side = Prim(PrimitiveType.Cube, "Hull", g, new Vector3(s * 1.5f, 1.4f, 0f), new Vector3(0.25f, 2.4f, 10.5f), wood);
                side.transform.localRotation = Quaternion.Euler(0f, 0f, s * -12f);
                for (int k = 0; k < 4; k++) // 舷側の板の継ぎ目と、丸い窓
                {
                    Prim(PrimitiveType.Cube, "Plank", g, new Vector3(s * (1.55f + k * 0.03f), 0.6f + k * 0.55f, 0f), new Vector3(0.05f, 0.05f, 10.4f), woodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, s * -12f);
                    Prim(PrimitiveType.Cylinder, "Porthole", g, new Vector3(s * 1.72f, 1.6f, -3f + k * 2f), new Vector3(0.35f, 0.03f, 0.35f), iron, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f - s * 12f);
                }
            }
            Prim(PrimitiveType.Cube, "Bow", g, new Vector3(0f, 1.6f, 5.6f), new Vector3(2.4f, 2.6f, 1.2f), wood).transform.localRotation = Quaternion.Euler(-20f, 45f, 0f);
            Prim(PrimitiveType.Cube, "Deck", g, new Vector3(0f, 2.3f, -1f), new Vector3(2.9f, 0.12f, 7f), wood, false);
            Prim(PrimitiveType.Cube, "Cabin", g, new Vector3(0f, 3f, -4.2f), new Vector3(2.6f, 1.6f, 2.2f), wood);
            Prim(PrimitiveType.Cube, "CabinRoof", g, new Vector3(0f, 3.85f, -4.2f), new Vector3(2.8f, 0.15f, 2.4f), woodDark, false);
            // 大きな穴 (船の横腹が破れて、中が暗い)
            Prim(PrimitiveType.Sphere, "Breach", g, new Vector3(-1.62f, 1.2f, 1.6f), new Vector3(0.3f, 1.3f, 1.8f), Mat("Wreck_Hole", new Color(0.03f, 0.03f, 0.04f)), false);
            // 折れたマストと、ぼろぼろの帆
            Prim(PrimitiveType.Cylinder, "Mast", g, new Vector3(0f, 4.6f, 1f), new Vector3(0.28f, 2.4f, 0.28f), woodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
            Prim(PrimitiveType.Cube, "Yard", g, new Vector3(0.2f, 6.2f, 1f), new Vector3(3.6f, 0.15f, 0.15f), woodDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
            Prim(PrimitiveType.Cube, "Sail", g, new Vector3(0.6f, 5.2f, 1.1f), new Vector3(2.6f, 1.9f, 0.03f), sail, false).transform.localRotation = Quaternion.Euler(10f, 0f, -16f);
            var broken = Prim(PrimitiveType.Cylinder, "FallenMast", sea, basePos + Polar(deg + 90f, 2.2f, 0.25f), new Vector3(0.24f, 3f, 0.24f), woodDark, false);
            broken.transform.localRotation = Quaternion.Euler(90f, deg + 30f, 0f);
            // 錨と、こけ・ふじつぼ
            Prim(PrimitiveType.Cylinder, "AnchorShaft", g, new Vector3(1.9f, 0.6f, 4.6f), new Vector3(0.08f, 0.7f, 0.08f), iron, false);
            Prim(PrimitiveType.Cube, "AnchorArm", g, new Vector3(1.9f, 0f, 4.6f), new Vector3(0.9f, 0.1f, 0.1f), iron, false);
            for (int k = 0; k < 14; k++)
                Prim(PrimitiveType.Sphere, "Moss", g, new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(0.4f, 3f), Random.Range(-5f, 5f)), Vector3.one * Random.Range(0.2f, 0.45f), moss, false);
            // 宝箱は、船の手前 (プレイヤー側) の砂の上
            var chest = basePos + Polar(deg + 180f, 4.6f, 0f);
            chest.y = SeaFloor;
            return (g, chest);
        }

        /// <summary>海に沈んだ古代の神殿 (階段の台・柱・屋根の残り・倒れた柱・像)。宝箱を置く所と壺を置く所を返す</summary>
        (Transform root, Vector3 chestSpot, List<Vector3> potSpots) BuildSeaTemple(Transform sea, float deg, float dist)
        {
            var marble = Mat("Temple_Marble", new Color(0.82f, 0.82f, 0.76f), null, false, 0.35f);
            var marbleDark = Mat("Temple_MarbleDark", new Color(0.6f, 0.62f, 0.58f), null, false, 0.3f);
            var gold = Mat("Temple_Gold", new Color(1f, 0.8f, 0.3f), new Color(0.3f, 0.2f, 0.02f), false, 0.85f);
            var moss = Mat("Wreck_Moss", new Color(0.28f, 0.42f, 0.25f), null, false, 0.1f);
            var basePos = Polar(deg, dist, SeaFloor);
            var g = Group(sea, "SeaTemple");
            g.localPosition = basePos;
            g.localRotation = FacingCenter(basePos) * Quaternion.Euler(0f, 180f, 0f); // 正面 (+Z) がプレイヤーの方
            // 3段の台
            for (int s = 0; s < 3; s++)
                Prim(PrimitiveType.Cube, "Step", g, new Vector3(0f, 0.2f + s * 0.4f, -s * 0.5f), new Vector3(10f - s * 0.8f, 0.4f, 7f - s * 1f), s == 0 ? marbleDark : marble);
            float top = 1.2f;
            // 柱 (正面に6本・奥に4本。何本かは折れている)
            var potSpots = new List<Vector3>();
            for (int k = 0; k < 6; k++)
            {
                float x = -3.75f + k * 1.5f;
                bool brokenCol = k == 1 || k == 4;
                float h = brokenCol ? Random.Range(1.2f, 2.4f) : 4.2f;
                Prim(PrimitiveType.Cylinder, "Column", g, new Vector3(x, top + h * 0.5f, 1.6f), new Vector3(0.6f, h * 0.5f, 0.6f), marble, true);
                Prim(PrimitiveType.Cube, "ColumnBase", g, new Vector3(x, top + 0.1f, 1.6f), new Vector3(0.8f, 0.2f, 0.8f), marbleDark, false);
                if (!brokenCol) Prim(PrimitiveType.Cube, "Capital", g, new Vector3(x, top + h + 0.12f, 1.6f), new Vector3(0.85f, 0.25f, 0.85f), marbleDark, false);
                if (k % 2 == 0) Prim(PrimitiveType.Sphere, "Moss", g, new Vector3(x + 0.2f, top + Random.Range(0.4f, 1.6f), 1.85f), Vector3.one * 0.3f, moss, false);
            }
            for (int k = 0; k < 4; k++)
            {
                float x = -3.75f + k * 2.5f;
                Prim(PrimitiveType.Cylinder, "BackColumn", g, new Vector3(x, top + 2.1f, -1.8f), new Vector3(0.6f, 2.1f, 0.6f), marble, false);
            }
            // 屋根の残り (梁と三角の破風。片側が崩れている)
            Prim(PrimitiveType.Cube, "Beam", g, new Vector3(0.9f, top + 4.5f, 0f), new Vector3(7.6f, 0.45f, 4.4f), marble, false).transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            Prim(PrimitiveType.Cube, "Pediment", g, new Vector3(1.4f, top + 5.2f, 1.6f), new Vector3(3.6f, 3.6f, 0.4f), marbleDark, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Cube, "PedimentCut", g, new Vector3(1.4f, top + 4.1f, 1.6f), new Vector3(5.4f, 2.2f, 0.5f), marble, false); // 三角の下半分を梁で隠す
            // 倒れた柱のかけら
            for (int k = 0; k < 3; k++)
            {
                var piece = Prim(PrimitiveType.Cylinder, "FallenColumn", g, new Vector3(-5.5f + k * 4.5f, 0.3f, 3.6f + k * 0.4f), new Vector3(0.55f, 0.9f, 0.55f), marble, false);
                piece.transform.localRotation = Quaternion.Euler(90f, Random.Range(-40f, 40f), 0f);
            }
            // 真ん中の金色の像 (三つ又の槍を持った海の神さま)
            var statue = Group(g, "Statue");
            statue.localPosition = new Vector3(0f, top, -0.4f);
            Prim(PrimitiveType.Cube, "Pedestal", statue, new Vector3(0f, 0.4f, 0f), new Vector3(1f, 0.8f, 1f), marbleDark, false);
            Prim(PrimitiveType.Capsule, "Body", statue, new Vector3(0f, 1.6f, 0f), new Vector3(0.6f, 0.8f, 0.45f), gold, false);
            Prim(PrimitiveType.Sphere, "Head", statue, new Vector3(0f, 2.6f, 0f), Vector3.one * 0.42f, gold, false);
            Prim(PrimitiveType.Sphere, "Beard", statue, new Vector3(0f, 2.4f, 0.15f), new Vector3(0.32f, 0.35f, 0.2f), gold, false);
            Prim(PrimitiveType.Cylinder, "Trident", statue, new Vector3(0.5f, 2f, 0.1f), new Vector3(0.06f, 1.3f, 0.06f), gold, false);
            for (int k = -1; k <= 1; k++)
                Prim(PrimitiveType.Cube, "Prong", statue, new Vector3(0.5f + k * 0.13f, 3.4f, 0.1f), new Vector3(0.05f, 0.35f, 0.05f), gold, false);
            // 壺を置く所 (階段の上と、台のまわり。神殿のローカル → 海のローカル)
            Vector3[] pots = { new(-3f, 0.85f, 2.1f), new(2.8f, 0.85f, 2.2f), new(-4.6f, 0.05f, 4.1f), new(4.4f, 0.05f, 4.3f), new(0.9f, 0.85f, 2.2f) }; // 2段目の上と、台のまわり
            foreach (var p in pots) potSpots.Add(basePos + g.localRotation * p);
            var chest = basePos + g.localRotation * new Vector3(-1.2f, 0f, 4.6f);
            chest.y = SeaFloor;
            return (g, chest, potSpots);
        }
    }
}
