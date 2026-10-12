using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 西部劇モード (保安官バッジの的を撃つと始まる) の生成: 荒野の地面と線路、機関車・炭水車・客車3両 (プレイヤーは1両目の屋根の上)、
    /// 後ろへ流れる景色 (枕木・荒野のでこぼこ・サボテン・電柱・岩・タンブルウィード・遠くの岩山)、棒の上に乗ったカウボーイと馬車、的の見た目。
    /// 列車はローカル +Z へ走る (向きは WesternTrain が的を撃った方へ回す)。プレイヤーの足元 (原点) が1両目の屋根の上。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float RoofTop = 3.0f;      // 客車の床から、屋根の上の板の道 (プレイヤーの足元) までの高さ
        const float CarRailTop = -0.98f; // 客車の床から見たレールの上面
        const float DesertY = -1.1f - RoofTop;  // 荒野の地面の高さ (プレイヤーの足元が 0)
        const float RailTop = CarRailTop - RoofTop; // レールの上面

        WesternTrain BuildWestern()
        {
            var go = new GameObject("WesternTrain");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;

            var sand = Mat("Desert_Sand", new Color(0.82f, 0.62f, 0.4f), null, false, 0.1f);
            var sandDark = Mat("Desert_SandDark", new Color(0.72f, 0.52f, 0.33f), null, false, 0.1f);
            var gravel = Mat("Rail_Gravel", new Color(0.45f, 0.4f, 0.36f));
            var steel = Mat("Rail_Steel", new Color(0.5f, 0.5f, 0.52f), null, false, 0.8f);

            // ---- 荒野の地面と線路 (動かない) ----
            var desert = Prim(PrimitiveType.Cube, "Desert", root, new Vector3(0f, DesertY - 0.05f, 0f), new Vector3(260f, 0.1f, 260f), sand, false);
            var track = Group(root, "Track");
            Prim(PrimitiveType.Cube, "Ballast", track, new Vector3(0f, DesertY + 0.03f, 0f), new Vector3(3f, 0.06f, 200f), gravel, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Rail", track, new Vector3(s * 0.72f, RailTop - 0.04f, 0f), new Vector3(0.08f, 0.08f, 200f), steel, false);

            var train = BuildTrain(root, out var wheels, out var stack);

            // ---- 後ろへ流れる景色 ----
            var scenery = new List<ConveyorBelt>();
            var factors = new List<float>();
            var wood = Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f));
            scenery.Add(SceneryBelt(root, "Sleepers", 80f, 1f, factors, (belt, items) =>
            {
                for (int i = 0; i < 100; i++)
                    items.Add(Prim(PrimitiveType.Cube, "Sleeper", belt, new Vector3(-40f + i * 0.8f, DesertY + 0.07f, 0f), new Vector3(0.22f, 0.06f, 2.4f), wood, false).transform);
            }));
            // 荒野のでこぼこ (線路と並んで流れる低い丘)
            scenery.Add(SceneryBelt(root, "Bumps", 120f, 1f, factors, (belt, items) =>
            {
                for (int i = 0; i < 70; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    float lateral = side * Random.Range(3.5f, 50f);
                    float w = Random.Range(2f, 7f);
                    var bump = Prim(PrimitiveType.Sphere, "Bump", belt, new Vector3(-60f + i * 120f / 70f, DesertY, lateral),
                                    new Vector3(w * Random.Range(1.2f, 2.2f), Random.Range(0.4f, 1.6f) * Mathf.Lerp(0.5f, 1.5f, Mathf.Abs(lateral) / 50f), w), i % 3 == 0 ? sandDark : sand, false);
                    items.Add(bump.transform);
                }
            }));
            // サボテン・岩・電柱
            var cactus = Mat("Cactus", new Color(0.25f, 0.5f, 0.25f));
            var rock = Mat("Rock_Red", new Color(0.6f, 0.35f, 0.25f), null, false, 0.15f);
            scenery.Add(SceneryBelt(root, "Props", 120f, 1f, factors, (belt, items) =>
            {
                for (int i = 0; i < 36; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    var item = Group(belt, "Prop");
                    item.localPosition = new Vector3(-60f + i * 120f / 36f + Random.Range(-1f, 1f), DesertY, side * Random.Range(4f, 35f));
                    if (i % 3 != 2) Saguaro(item, Random.Range(1.2f, 2.6f), cactus);
                    else
                    {
                        Prim(PrimitiveType.Sphere, "Rock", item, Vector3.up * 0.2f, new Vector3(Random.Range(0.8f, 1.8f), Random.Range(0.5f, 1.2f), Random.Range(0.8f, 1.5f)), rock, false);
                        Prim(PrimitiveType.Sphere, "Rock2", item, new Vector3(0.6f, 0.1f, 0.3f), new Vector3(0.6f, 0.4f, 0.5f), rock, false);
                    }
                    items.Add(item);
                }
                for (int i = 0; i < 10; i++) // 電柱 (線路の右側に等間隔。流れていくのがよく分かる)
                {
                    var pole = Group(belt, "TelegraphPole");
                    pole.localPosition = new Vector3(-60f + i * 12f, DesertY, 3.6f);
                    Prim(PrimitiveType.Cylinder, "Pole", pole, new Vector3(0f, 2.4f, 0f), new Vector3(0.14f, 2.4f, 0.14f), wood, false);
                    Prim(PrimitiveType.Cube, "Crossbar", pole, new Vector3(0f, 4.4f, 0f), new Vector3(0.1f, 0.1f, 1.4f), wood, false);
                    items.Add(pole);
                }
            }));
            // タンブルウィード (風に吹かれて、景色より少し速く転がっていく)
            var weed = Mat("Tumbleweed", new Color(0.55f, 0.4f, 0.22f));
            scenery.Add(SceneryBelt(root, "Tumbleweeds", 90f, 1.5f, factors, (belt, items) =>
            {
                for (int i = 0; i < 10; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    var tw = Group(belt, "Tumbleweed");
                    float r = Random.Range(0.3f, 0.55f);
                    tw.localPosition = new Vector3(-45f + i * 9f, DesertY + r, side * Random.Range(2.5f, 18f));
                    var roll = Group(tw, "Roll");
                    var spin = roll.gameObject.AddComponent<Rotator>();
                    spin.axis = Vector3.forward; // ベルトのローカル Z (横) まわりに転がる
                    spin.speed = -300f;
                    for (int k = 0; k < 4; k++)
                    {
                        var ring = Group(roll, "Ring");
                        ring.localRotation = Quaternion.Euler(k * 45f, k * 70f, 0f);
                        RingSegments(ring, "Twig", r, 10, 0.025f, weed);
                    }
                    items.Add(tw);
                }
            }));
            // 遠くの岩山 (とてもゆっくり流れる)
            var mesa = Mat("Mesa", new Color(0.7f, 0.38f, 0.25f), null, false, 0.1f);
            var mesaTop = Mat("Mesa_Top", new Color(0.62f, 0.33f, 0.22f), null, false, 0.1f);
            scenery.Add(SceneryBelt(root, "Mesas", 240f, 0.12f, factors, (belt, items) =>
            {
                for (int i = 0; i < 12; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    var m = Group(belt, "Mesa");
                    m.localPosition = new Vector3(-120f + i * 20f + Random.Range(-4f, 4f), DesertY, side * Random.Range(60f, 80f));
                    float w = Random.Range(10f, 22f), h = Random.Range(7f, 16f);
                    Prim(PrimitiveType.Cylinder, "MesaBody", m, new Vector3(0f, h / 2f, 0f), new Vector3(w, h / 2f, w * 0.6f), mesa, false);
                    Prim(PrimitiveType.Cylinder, "MesaTop", m, new Vector3(0f, h + 0.2f, 0f), new Vector3(w * 1.05f, 0.3f, w * 0.63f), mesaTop, false);
                    items.Add(m);
                }
            }));

            // ---- 的: カウボーイ・悪党・馬車 ----
            var riders = new List<WesternRider>();
            for (int i = 0; i < 8; i++) riders.Add(BuildCowboy(root, $"Cowboy_{i}", false, i));
            for (int i = 0; i < 4; i++) riders.Add(BuildCowboy(root, $"Bandit_{i}", true, i));
            for (int i = 0; i < 2; i++) riders.Add(BuildStagecoach(root, i));

            var western = go.AddComponent<WesternTrain>();
            western.train = train;
            var ground = new List<Transform>();
            foreach (var name in new[] { "Ground", "PitRim" })
            {
                var t = world.Find(name);
                if (t != null) ground.Add(t);
            }
            if (playerMark != null) ground.Add(playerMark.transform);
            western.groundParts = ground.ToArray();
            western.showDuringStage = new[] { desert, track.gameObject };
            western.scenery = scenery.ToArray();
            western.sceneryFactors = factors.ToArray();
            western.wheels = wheels.ToArray();
            western.smokeStack = stack;
            western.smokeMaterial = TransparentMat("Smoke_Train", new Color(0.85f, 0.85f, 0.85f, 0.45f));
            western.sandMaterial = TransparentMat("Western_Sand", new Color(0.84f, 0.68f, 0.44f, 0.55f)); // 砂嵐の砂粒
            western.riders = riders.ToArray();
            western.maxRunning = 9;
            western.launchInterval = new Vector2(0.45f, 0.9f);
            // 西部劇モードの時だけ出る (シーンに生成した時も隠しておく)
            train.gameObject.SetActive(false);
            desert.SetActive(false);
            track.gameObject.SetActive(false);
            foreach (var belt in scenery) belt.gameObject.SetActive(false);
            go.SetActive(true);
            return western;
        }

        void Saguaro(Transform parent, float h, Material mat)
        {
            Prim(PrimitiveType.Capsule, "Trunk", parent, new Vector3(0f, h / 2f, 0f), new Vector3(0.3f, h / 2f, 0.3f), mat, false);
            foreach (float s in new[] { -1f, 1f })
            {
                float y = h * Random.Range(0.4f, 0.6f);
                Prim(PrimitiveType.Capsule, "ArmOut", parent, new Vector3(s * 0.25f, y, 0f), new Vector3(0.2f, 0.18f, 0.2f), mat, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Prim(PrimitiveType.Capsule, "ArmUp", parent, new Vector3(s * 0.4f, y + 0.25f, 0f), new Vector3(0.2f, h * 0.18f, 0.2f), mat, false);
            }
        }

        // ------------------------------------------------------------------
        // 列車
        // ------------------------------------------------------------------

        /// <summary>
        /// 列車 (+Z が前): 機関車 (黒いボイラー・煙突・運転室・排障器・前照灯・赤い動輪) → 炭水車 (石炭) → 同じ形の客車3両
        /// (窓が光る緑の車体と赤い丸屋根)。プレイヤーは1両目の屋根の上の板の道に立つ。
        /// </summary>
        Transform BuildTrain(Transform parent, out List<Transform> wheels, out Transform stack)
        {
            var trainRoot = Group(parent, "Train");
            var train = Group(trainRoot, "Cars"); // 客車の床が原点。1両目の屋根の上がプレイヤーの足元に来るように下げる
            train.localPosition = Vector3.down * RoofTop;
            wheels = new List<Transform>();
            var black = Mat("Loco_Black", new Color(0.1f, 0.1f, 0.11f), null, false, 0.6f);
            var red = Mat("Loco_Red", new Color(0.65f, 0.1f, 0.08f), null, false, 0.4f);
            var brass = Mat("Revolver_Brass", new Color(0.9f, 0.7f, 0.3f), null, false, 0.7f);
            var plank = Mat("Deck_Wood", new Color(0.45f, 0.3f, 0.18f), null, false, 0.35f);
            var green = Mat("Car_Green", new Color(0.15f, 0.35f, 0.22f), null, false, 0.4f);
            var roof = Mat("Car_Roof", new Color(0.5f, 0.12f, 0.1f));
            var window = Mat("Car_Window", new Color(1f, 0.85f, 0.5f), new Color(0.9f, 0.6f, 0.25f));
            var lantern = Mat("Ship_Lantern", new Color(1f, 0.75f, 0.35f), new Color(2f, 1.2f, 0.4f));
            const float W = 2.8f;

            // 客車3両 (どれも同じ形: 窓が光る緑の車体と赤い丸屋根)。1両目 (z = -3.5 〜 3.5) の屋根の上の板の道がプレイヤーの足元
            foreach (float cz in new[] { 0f, -7.8f, -15.6f })
            {
                Prim(PrimitiveType.Cube, "CarBody", train, new Vector3(0f, 1.2f, cz), new Vector3(W, 2.6f, 7f), green);
                Prim(PrimitiveType.Cube, "CarFrame", train, new Vector3(0f, -0.25f, cz), new Vector3(W - 0.2f, 0.4f, 7f), black, false);
                var roofTop = Prim(PrimitiveType.Cylinder, "CarRoof", train, new Vector3(0f, 2.55f, cz), new Vector3(W + 0.2f, 3.6f, 0.8f), roof, false);
                roofTop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // 屋根の上の板の道 (上面が RoofTop) と、両端のランタン・小さな煙出し
                Prim(PrimitiveType.Cube, "RoofWalk", train, new Vector3(0f, RoofTop - 0.04f, cz), new Vector3(1.3f, 0.08f, 7.1f), plank, false);
                for (int k = -8; k <= 8; k++)
                    Prim(PrimitiveType.Cube, "RoofWalkLine", train, new Vector3(0f, RoofTop + 0.002f, cz + k * 0.42f), new Vector3(1.3f, 0.004f, 0.015f), Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f)), false);
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Cylinder, "RoofVent", train, new Vector3(s * 0.95f, RoofTop - 0.12f, cz + s * 1.8f), new Vector3(0.22f, 0.12f, 0.22f), black, false);
                    Prim(PrimitiveType.Sphere, "Lantern", train, new Vector3(s * (W / 2f - 0.1f), 2.3f, cz + 3.45f), new Vector3(0.14f, 0.2f, 0.14f), lantern, false);
                    for (int k = 0; k < 6; k++)
                        Prim(PrimitiveType.Cube, "CarWindow", train, new Vector3(s * (W / 2f + 0.005f), 1.5f, cz - 2.7f + k * 1.08f), new Vector3(0.01f, 0.7f, 0.6f), window, false);
                    Prim(PrimitiveType.Cube, "GoldStripe", train, new Vector3(s * (W / 2f + 0.006f), 0.6f, cz), new Vector3(0.012f, 0.06f, 7f), brass, false);
                }
                // 車両の間の連結デッキ
                Prim(PrimitiveType.Cube, "EndDeck", train, new Vector3(0f, -0.05f, cz + 3.7f), new Vector3(W - 0.6f, 0.1f, 0.6f), plank, false);
                Bogies(train, wheels, new[] { cz - 2.4f, cz + 2.4f }, black, red);
            }

            // 炭水車 (z = 4 〜 8)
            Prim(PrimitiveType.Cube, "Tender", train, new Vector3(0f, 0.55f, 6f), new Vector3(W - 0.2f, 1.7f, 3.8f), black);
            Prim(PrimitiveType.Cube, "TenderTrim", train, new Vector3(0f, 1.42f, 6f), new Vector3(W - 0.1f, 0.06f, 3.9f), brass, false);
            var coal = Mat("Coal", new Color(0.05f, 0.05f, 0.06f), null, false, 0.6f);
            for (int i = 0; i < 14; i++)
                Prim(PrimitiveType.Sphere, "Coal", train, new Vector3(Random.Range(-1f, 1f), 1.45f, 6f + Random.Range(-1.6f, 1.6f)), Vector3.one * Random.Range(0.25f, 0.45f), coal, false);
            Bogies(train, wheels, new[] { 5f, 7f }, black, red);

            // 機関車 (z = 8.5 〜 18)
            Prim(PrimitiveType.Cube, "LocoFrame", train, new Vector3(0f, -0.2f, 13.2f), new Vector3(W - 0.4f, 0.5f, 9.4f), red, false);
            var boiler = Prim(PrimitiveType.Cylinder, "Boiler", train, new Vector3(0f, 1.0f, 13.8f), new Vector3(1.6f, 3f, 1.6f), black);
            boiler.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var smokebox = Prim(PrimitiveType.Cylinder, "SmokeBox", train, new Vector3(0f, 1.0f, 16.9f), new Vector3(1.7f, 0.25f, 1.7f), Mat("Loco_Steel", new Color(0.25f, 0.25f, 0.27f), null, false, 0.6f), false);
            smokebox.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float z in new[] { 11.8f, 14.2f, 16.4f })
            {
                var band = Prim(PrimitiveType.Cylinder, "BoilerBand", train, new Vector3(0f, 1.0f, z), new Vector3(1.64f, 0.04f, 1.64f), brass, false);
                band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Prim(PrimitiveType.Cylinder, "Stack", train, new Vector3(0f, 2.4f, 16.2f), new Vector3(0.45f, 0.65f, 0.45f), black, false);
            Prim(PrimitiveType.Cylinder, "StackTop", train, new Vector3(0f, 3.1f, 16.2f), new Vector3(0.85f, 0.15f, 0.85f), black, false);
            stack = Group(train, "SmokePoint");
            stack.localPosition = new Vector3(0f, 3.3f, 16.2f);
            Prim(PrimitiveType.Sphere, "SteamDome", train, new Vector3(0f, 1.9f, 13.8f), new Vector3(0.6f, 0.5f, 0.6f), brass, false);
            Prim(PrimitiveType.Sphere, "Bell", train, new Vector3(0f, 2.0f, 12.4f), new Vector3(0.3f, 0.35f, 0.3f), brass, false);
            // 運転室
            Prim(PrimitiveType.Cube, "Cab", train, new Vector3(0f, 1.3f, 9.6f), new Vector3(W - 0.1f, 2.6f, 2.2f), red);
            Prim(PrimitiveType.Cube, "CabRoof", train, new Vector3(0f, 2.68f, 9.6f), new Vector3(W + 0.2f, 0.12f, 2.6f), black, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "CabWindow", train, new Vector3(s * (W / 2f - 0.04f), 1.8f, 9.6f), new Vector3(0.01f, 0.6f, 0.9f), window, false);
            // 前: 前照灯・排障器
            Prim(PrimitiveType.Cylinder, "Headlamp", train, new Vector3(0f, 1.95f, 17.1f), new Vector3(0.4f, 0.2f, 0.4f), Mat("Headlamp", new Color(1f, 0.95f, 0.7f), new Color(3f, 2.6f, 1.5f)), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                var catcher = Prim(PrimitiveType.Cube, "Cowcatcher", train, new Vector3(s * 0.5f, -0.6f, 18.1f), new Vector3(1.2f, 0.7f, 0.1f), red, false);
                catcher.transform.localRotation = Quaternion.Euler(-35f, s * 35f, 0f);
            }
            // 大きな動輪 (片側3つ) と前の小さな車輪
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float z in new[] { 10.6f, 12.3f, 14f })
                    wheels.Add(TrainWheel(train, new Vector3(s * (W / 2f - 0.25f), CarRailTop + 0.75f, z), 0.75f, red, black));
                wheels.Add(TrainWheel(train, new Vector3(s * (W / 2f - 0.3f), CarRailTop + 0.4f, 16.6f), 0.4f, red, black));
                Prim(PrimitiveType.Cube, "SideRod", train, new Vector3(s * (W / 2f - 0.12f), CarRailTop + 0.75f, 12.3f), new Vector3(0.05f, 0.08f, 3.6f), Mat("Loco_Steel", new Color(0.25f, 0.25f, 0.27f), null, false, 0.6f), false);
            }
            return trainRoot;
        }

        // 台車: z の位置ごとに、左右2輪ずつ
        void Bogies(Transform train, List<Transform> wheels, float[] zs, Material frame, Material wheelMat)
        {
            foreach (float z in zs)
            {
                Prim(PrimitiveType.Cube, "Bogie", train, new Vector3(0f, CarRailTop + 0.45f, z), new Vector3(2f, 0.2f, 1.4f), frame, false);
                foreach (float s in new[] { -1f, 1f })
                    foreach (float dz in new[] { -0.45f, 0.45f })
                        wheels.Add(TrainWheel(train, new Vector3(s * 0.72f, CarRailTop + 0.4f, z + dz), 0.4f, wheelMat, frame));
            }
        }

        // 車輪 (ローカル X まわりに回るグループ。回っているのが分かるように、スポークを1本付ける)
        Transform TrainWheel(Transform parent, Vector3 pos, float r, Material mat, Material spokeMat)
        {
            var w = Group(parent, "Wheel");
            w.localPosition = pos;
            var disc = Prim(PrimitiveType.Cylinder, "Disc", w, Vector3.zero, new Vector3(r * 2f, 0.06f, r * 2f), mat, false);
            disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cube, "Spoke", w, new Vector3(Mathf.Sign(pos.x) * 0.07f, 0f, 0f), new Vector3(0.02f, r * 1.8f, 0.08f), spokeMat, false);
            return w;
        }

        // ------------------------------------------------------------------
        // 的: カウボーイ・悪党・馬車
        // ------------------------------------------------------------------

        /// <summary>
        /// 棒の上に乗ったカウボーイ (射的の人形のように、地面から伸びた棒の上に立体の馬と人)。悪党は黒い帽子と服で点数が高い。
        /// 棒の札に点数 (いつもプレイヤーの方を向く)。当てると人形が後ろへ倒れる。
        /// </summary>
        WesternRider BuildCowboy(Transform parent, string name, bool bandit, int index)
        {
            var root = Group(parent, name);
            root.gameObject.SetActive(false);
            var body = Group(root, "Body");
            var labels = new List<TextMesh>();
            const float PoleH = 2.0f; // 屋根の上から見下ろしても見やすいように高めの棒

            var poleMat = Mat("Rider_Pole", new Color(0.35f, 0.3f, 0.25f), null, false, 0.6f);
            Prim(PrimitiveType.Cylinder, "Pole", body, new Vector3(0f, PoleH / 2f, 0f), new Vector3(0.06f, PoleH / 2f, 0.06f), poleMat, false);
            Prim(PrimitiveType.Cylinder, "PoleFoot", body, new Vector3(0f, 0.03f, 0f), new Vector3(0.4f, 0.03f, 0.4f), poleMat, false);
            labels.Add(PointSign(body, new Vector3(0f, PoleH * 0.5f, 0f), 0.55f));

            var mount = Group(body, "Mount");
            mount.localPosition = new Vector3(0f, PoleH, 0f);
            var figure = Group(mount, "Figure");
            Color[] horseColors = { new Color(0.45f, 0.28f, 0.15f), new Color(0.3f, 0.2f, 0.12f), new Color(0.85f, 0.8f, 0.72f), new Color(0.15f, 0.12f, 0.1f) };
            var horse = Mat($"Horse_{index % horseColors.Length}", horseColors[index % horseColors.Length]);
            Horse(figure, Vector3.zero, horse);
            Rider(figure, new Vector3(0f, 0.55f, -0.1f), bandit, index);

            var target = mount.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Rider;
            target.manualControl = true;
            target.visual = figure;
            target.SetAvailable(false);

            var rider = root.gameObject.AddComponent<WesternRider>();
            rider.body = body;
            rider.hitTarget = target;
            rider.pointLabels = labels.ToArray();
            rider.groundY = DesertY;
            if (bandit)
            {
                rider.displayName = "OUTLAW";
                rider.pointChoices = new[] { 300, 500 };
                rider.speed = 3f;
                rider.distance = new Vector2(8f, 13f);
            }
            else
            {
                rider.displayName = "COWBOY";
                rider.pointChoices = new[] { 100, 200, 300 };
                rider.speed = 2.4f;
                rider.distance = new Vector2(6.5f, 12f);
            }
            rider.routeChoices = new[] { WesternRider.Route.Overtake, WesternRider.Route.FallBehind, WesternRider.Route.Approach };
            return rider;
        }

        /// <summary>馬車 (2頭の馬が引く箱馬車と御者)。2本の棒に乗っている。1000 点</summary>
        WesternRider BuildStagecoach(Transform parent, int index)
        {
            var root = Group(parent, $"Stagecoach_{index}");
            root.gameObject.SetActive(false);
            var body = Group(root, "Body");
            const float PoleH = 2.0f; // 屋根の上から見下ろしても見やすいように高めの棒
            var poleMat = Mat("Rider_Pole", new Color(0.35f, 0.3f, 0.25f), null, false, 0.6f);
            foreach (float z in new[] { -0.8f, 1.6f })
            {
                Prim(PrimitiveType.Cylinder, "Pole", body, new Vector3(0f, PoleH / 2f, z), new Vector3(0.07f, PoleH / 2f, 0.07f), poleMat, false);
                Prim(PrimitiveType.Cylinder, "PoleFoot", body, new Vector3(0f, 0.03f, z), new Vector3(0.45f, 0.03f, 0.45f), poleMat, false);
            }
            var labels = new List<TextMesh> { PointSign(body, new Vector3(0f, PoleH * 0.5f, 0.4f), 0.75f) };

            var mount = Group(body, "Mount");
            mount.localPosition = new Vector3(0f, PoleH, 0f);
            var figure = Group(mount, "Figure");
            var horse = Mat("Horse_0", new Color(0.45f, 0.28f, 0.15f));
            foreach (float s in new[] { -0.32f, 0.32f })
                Horse(figure, new Vector3(s, 0f, 2.2f), horse);
            // 箱馬車
            var coachRed = index % 2 == 0 ? Mat("Coach_Red", new Color(0.6f, 0.12f, 0.1f), null, false, 0.4f) : Mat("Coach_Green", new Color(0.15f, 0.4f, 0.25f), null, false, 0.4f);
            var brass = Mat("Revolver_Brass", new Color(0.9f, 0.7f, 0.3f), null, false, 0.7f);
            Prim(PrimitiveType.Cube, "Coach", figure, new Vector3(0f, 0.75f, 0f), new Vector3(1.4f, 1.3f, 2f), coachRed);
            Prim(PrimitiveType.Cube, "CoachRoof", figure, new Vector3(0f, 1.45f, 0f), new Vector3(1.55f, 0.1f, 2.2f), Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f)), false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "CoachWindow", figure, new Vector3(s * 0.705f, 0.95f, 0f), new Vector3(0.01f, 0.45f, 0.7f), Mat("Car_Window", new Color(1f, 0.85f, 0.5f), new Color(0.9f, 0.6f, 0.25f)), false);
                Prim(PrimitiveType.Cube, "CoachTrim", figure, new Vector3(s * 0.71f, 0.45f, 0f), new Vector3(0.012f, 0.05f, 2f), brass, false);
                foreach (float z in new[] { -0.75f, 0.75f })
                {
                    var wheel = Prim(PrimitiveType.Cylinder, "CoachWheel", figure, new Vector3(s * 0.75f, 0.1f, z), new Vector3(0.75f, 0.03f, 0.75f), Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f)), false);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            Prim(PrimitiveType.Cube, "DriverSeat", figure, new Vector3(0f, 1.2f, 1.2f), new Vector3(1.2f, 0.12f, 0.5f), Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f)), false);
            Rider(figure, new Vector3(0f, 1.3f, 1.2f), false, 1);
            Prim(PrimitiveType.Cube, "Shaft", figure, new Vector3(0f, 0.35f, 1.6f), new Vector3(0.08f, 0.08f, 1.6f), Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f)), false);

            var target = mount.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Rider;
            target.manualControl = true;
            target.visual = figure;
            target.SetAvailable(false);

            var rider = root.gameObject.AddComponent<WesternRider>();
            rider.displayName = "STAGECOACH";
            rider.body = body;
            rider.hitTarget = target;
            rider.pointLabels = labels.ToArray();
            rider.groundY = DesertY;
            rider.pointChoices = new[] { 1000 };
            rider.speed = 1.6f;
            rider.distance = new Vector2(10f, 14f);
            rider.gallopHeight = 0.04f;
            rider.routeChoices = new[] { WesternRider.Route.Overtake, WesternRider.Route.FallBehind };
            return rider;
        }

        // 点数の札 (棒に付いていて、いつもプレイヤーの方を向く)
        TextMesh PointSign(Transform parent, Vector3 pos, float size)
        {
            var sign = Group(parent, "PointSign");
            sign.localPosition = pos;
            sign.gameObject.AddComponent<FaceCenter>();
            Prim(PrimitiveType.Cube, "Board", sign, Vector3.zero, new Vector3(size * 1.6f, size * 0.7f, 0.04f), Mat("Wanted_Paper", new Color(0.95f, 0.88f, 0.68f)), false);
            Prim(PrimitiveType.Cube, "Border", sign, new Vector3(0f, 0f, 0.01f), new Vector3(size * 1.7f, size * 0.8f, 0.03f), Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f)), false);
            return MakeText(sign, "Points", "100", new Vector3(0f, 0f, -0.03f), size * 0.55f, new Color(0.45f, 0.12f, 0.05f));
        }

        // 走っている形の馬 (前足と後ろ足を開いている)。原点が胴の真ん中。+Z が前
        void Horse(Transform parent, Vector3 pos, Material mat)
        {
            var h = Group(parent, "Horse");
            h.localPosition = pos;
            var dark = Mat("Horse_Mane", new Color(0.1f, 0.08f, 0.06f));
            Prim(PrimitiveType.Capsule, "HorseBody", h, Vector3.zero, new Vector3(0.38f, 0.65f, 0.42f), mat).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var neck = Prim(PrimitiveType.Cube, "Neck", h, new Vector3(0f, 0.3f, 0.55f), new Vector3(0.18f, 0.5f, 0.22f), mat);
            neck.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            var head = Prim(PrimitiveType.Cube, "Head", h, new Vector3(0f, 0.5f, 0.82f), new Vector3(0.16f, 0.18f, 0.42f), mat);
            head.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Mane", h, new Vector3(0f, 0.42f, 0.5f), new Vector3(0.05f, 0.4f, 0.12f), dark, false).transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            var tail = Prim(PrimitiveType.Cube, "Tail", h, new Vector3(0f, 0.05f, -0.7f), new Vector3(0.06f, 0.45f, 0.08f), dark, false);
            tail.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                var front = Prim(PrimitiveType.Cube, "FrontLeg", h, new Vector3(s * 0.12f, -0.38f, 0.42f), new Vector3(0.08f, 0.6f, 0.08f), mat);
                front.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                var back = Prim(PrimitiveType.Cube, "BackLeg", h, new Vector3(s * 0.12f, -0.38f, -0.42f), new Vector3(0.09f, 0.6f, 0.09f), mat);
                back.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            }
        }

        // 馬に乗った人 (帽子・バンダナ・振り上げた手に拳銃)。原点が鞍の上。悪党は黒ずくめ
        void Rider(Transform parent, Vector3 pos, bool bandit, int index)
        {
            var r = Group(parent, "Rider");
            r.localPosition = pos;
            Color[] shirts = { new Color(0.75f, 0.2f, 0.15f), new Color(0.2f, 0.4f, 0.75f), new Color(0.85f, 0.7f, 0.3f), new Color(0.3f, 0.55f, 0.3f) };
            var shirt = bandit ? Mat("Bandit_Shirt", new Color(0.12f, 0.12f, 0.14f)) : Mat($"Cowboy_Shirt_{index % shirts.Length}", shirts[index % shirts.Length]);
            var jeans = Mat("Cowboy_Jeans", new Color(0.2f, 0.25f, 0.4f));
            var hat = bandit ? Mat("Bandit_Hat", new Color(0.05f, 0.05f, 0.06f)) : Mat("Cowboy_Hat", new Color(0.55f, 0.38f, 0.2f));
            var skin = Mat("Pilot_Skin", new Color(0.95f, 0.75f, 0.6f));
            var bandana = Mat(bandit ? "Bandit_Bandana" : "Cowboy_Bandana", bandit ? new Color(0.6f, 0.05f, 0.05f) : new Color(0.85f, 0.15f, 0.12f));
            foreach (float s in new[] { -1f, 1f })
            {
                var leg = Prim(PrimitiveType.Cube, "Leg", r, new Vector3(s * 0.2f, -0.1f, 0.05f), new Vector3(0.1f, 0.42f, 0.12f), jeans);
                leg.transform.localRotation = Quaternion.Euler(-20f, 0f, s * 10f);
            }
            Prim(PrimitiveType.Cube, "Torso", r, new Vector3(0f, 0.32f, 0f), new Vector3(0.34f, 0.46f, 0.2f), shirt);
            var armUp = Prim(PrimitiveType.Cube, "ArmUp", r, new Vector3(0.26f, 0.55f, 0.05f), new Vector3(0.09f, 0.4f, 0.09f), shirt);
            armUp.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
            Prim(PrimitiveType.Cube, "Pistol", r, new Vector3(0.36f, 0.78f, 0.08f), new Vector3(0.04f, 0.06f, 0.16f), Mat("Revolver_Steel", new Color(0.25f, 0.25f, 0.28f), null, false, 0.8f), false);
            var armRein = Prim(PrimitiveType.Cube, "ArmRein", r, new Vector3(-0.2f, 0.25f, 0.15f), new Vector3(0.09f, 0.35f, 0.09f), shirt);
            armRein.transform.localRotation = Quaternion.Euler(-50f, 0f, 10f);
            Prim(PrimitiveType.Cube, "Bandana", r, new Vector3(0f, 0.6f, -0.02f), new Vector3(0.24f, 0.08f, 0.22f), bandana, false);
            Prim(PrimitiveType.Sphere, "Head", r, new Vector3(0f, 0.76f, 0f), new Vector3(0.22f, 0.25f, 0.22f), skin);
            if (bandit) Prim(PrimitiveType.Cube, "Mask", r, new Vector3(0f, 0.72f, 0.08f), new Vector3(0.23f, 0.1f, 0.1f), bandana, false);
            Prim(PrimitiveType.Cylinder, "HatBrim", r, new Vector3(0f, 0.88f, 0f), new Vector3(0.5f, 0.015f, 0.5f), hat, false);
            Prim(PrimitiveType.Cylinder, "HatCrown", r, new Vector3(0f, 0.97f, 0f), new Vector3(0.24f, 0.09f, 0.24f), hat, false);
        }

        /// <summary>保安官バッジの的: こげ茶の板に金の星のバッジ、「WILD WEST」の文字</summary>
        GameObject WesternLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Western_Plate", new Color(0.4f, 0.24f, 0.12f), new Color(0.1f, 0.05f, 0.02f)));
            var spin = Group(look, "Badge");
            spin.localPosition = c + new Vector3(0f, 0.02f, -0.028f);
            var rot = spin.gameObject.AddComponent<Rotator>();
            rot.axis = Vector3.forward; // 板と平行なまま、くるくる回る
            rot.speed = 120f;
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var star = new GameObject("Star");
            star.transform.SetParent(spin, false);
            star.transform.localScale = Vector3.one * 0.17f;
            star.transform.localPosition = Vector3.back * 0.006f;
            star.AddComponent<MeshFilter>().sharedMesh = ShapeMesh(Shape.Star);
            star.AddComponent<MeshRenderer>().sharedMaterial = gold;
            Prim(PrimitiveType.Cylinder, "BadgeRing", spin, Vector3.forward * 0.004f, new Vector3(0.2f, 0.004f, 0.2f), Mat("Revolver_Brass", new Color(0.9f, 0.7f, 0.3f), null, false, 0.7f), false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MakeText(look, "Label", "WILD WEST", c + new Vector3(0f, -0.115f, -0.025f), 0.035f, new Color(1f, 0.85f, 0.4f));
            return look.gameObject;
        }
    }
}
