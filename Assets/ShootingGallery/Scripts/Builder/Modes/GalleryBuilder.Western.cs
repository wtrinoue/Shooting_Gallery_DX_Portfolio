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
            for (int i = 0; i < 11; i++) riders.Add(BuildCowboy(root, $"Cowboy_{i}", false, i));
            for (int i = 0; i < 6; i++) riders.Add(BuildCowboy(root, $"Bandit_{i}", true, i));
            for (int i = 0; i < 2; i++) riders.Add(BuildStagecoach(root, i));
            for (int i = 0; i < 3; i++) riders.Add(BuildModelT(root, i)); // ちょっと時代が違うけれど、古い自動車も荒野を走る

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
            western.maxRunning = 13;
            western.launchInterval = new Vector2(0.3f, 0.6f);
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
        /// 列車 (+Z が前): 機関車 (BuildSteamLocomotive) → 炭水車 (BuildTender: 山盛りの石炭) → 同じ形の客車3両
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

            var steelDark = Mat("Loco_Steel", new Color(0.25f, 0.25f, 0.27f), null, false, 0.6f);
            BuildTender(train, wheels, W, black, red, brass, plank, steelDark);
            BuildSteamLocomotive(train, wheels, W, black, red, brass, window, steelDark, out stack);
            return trainRoot;
        }

        /// <summary>
        /// 炭水車 (z = 4 〜 8): 黒い車体に金の線と「WILD WEST R.R.」の文字。後ろ寄り (プレイヤー側) に水槽のふた・道具箱・はしご、
        /// 前寄りの少し外へ開いた囲いの中に、いびつな石炭の塊が山盛りに積もっている
        /// </summary>
        void BuildTender(Transform train, List<Transform> wheels, float W, Material black, Material red, Material brass, Material plank, Material steelDark)
        {
            Prim(PrimitiveType.Cube, "Tender", train, new Vector3(0f, 0.55f, 6f), new Vector3(W - 0.2f, 1.7f, 3.8f), black);
            Prim(PrimitiveType.Cube, "TenderFrame", train, new Vector3(0f, -0.42f, 6f), new Vector3(W - 0.5f, 0.26f, 4f), red, false);
            Prim(PrimitiveType.Cube, "TenderTrim", train, new Vector3(0f, 1.42f, 6f), new Vector3(W - 0.1f, 0.06f, 3.9f), brass, false);
            var gold = new Color(1f, 0.8f, 0.35f);
            foreach (float s in new[] { -1f, 1f })
            {
                // 石炭の囲い (少し外へ開いた板) と、その上の金の縁
                var board = Prim(PrimitiveType.Cube, "CoalBoard", train, new Vector3(s * (W / 2f - 0.13f), 1.62f, 6.35f), new Vector3(0.06f, 0.42f, 3.1f), black, false);
                board.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 12f);
                Prim(PrimitiveType.Cube, "CoalBoardTrim", train, new Vector3(s * (W / 2f - 0.085f), 1.83f, 6.35f), new Vector3(0.05f, 0.04f, 3.1f), brass, false);
                // 車体の横の金の線と文字
                foreach (float y in new[] { 1.12f, -0.05f })
                    Prim(PrimitiveType.Cube, "TenderStripe", train, new Vector3(s * (W / 2f - 0.094f), y, 6f), new Vector3(0.012f, 0.05f, 3.6f), brass, false);
                MakeText(train, "TenderLetter", "WILD WEST R.R.", new Vector3(s * (W / 2f - 0.08f), 0.55f, 6f), 0.3f, gold)
                    .transform.localRotation = Quaternion.Euler(0f, -90f * s, 0f);
                // 後ろのはしご
                Prim(PrimitiveType.Cube, "LadderRail", train, new Vector3(s * 0.35f, 0.75f, 4.07f), new Vector3(0.04f, 1.3f, 0.04f), steelDark, false);
            }
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cube, "LadderRung", train, new Vector3(0f, 0.3f + k * 0.3f, 4.07f), new Vector3(0.7f, 0.03f, 0.03f), steelDark, false);
            Prim(PrimitiveType.Cube, "CoalBoardFront", train, new Vector3(0f, 1.6f, 7.85f), new Vector3(W - 0.35f, 0.38f, 0.06f), black, false);
            Prim(PrimitiveType.Cube, "CoalBulkhead", train, new Vector3(0f, 1.62f, 4.82f), new Vector3(W - 0.35f, 0.42f, 0.06f), black, false);
            Prim(PrimitiveType.Cube, "CoalBulkheadTrim", train, new Vector3(0f, 1.84f, 4.82f), new Vector3(W - 0.3f, 0.04f, 0.08f), brass, false);
            // 水槽のふた (丸いふたと取っ手) と道具箱
            Prim(PrimitiveType.Cylinder, "WaterHatch", train, new Vector3(0.35f, 1.46f, 4.45f), new Vector3(0.5f, 0.04f, 0.5f), steelDark, false);
            Prim(PrimitiveType.Cylinder, "WaterHatchRim", train, new Vector3(0.35f, 1.44f, 4.45f), new Vector3(0.56f, 0.03f, 0.56f), brass, false);
            Prim(PrimitiveType.Cube, "WaterHatchHandle", train, new Vector3(0.35f, 1.52f, 4.45f), new Vector3(0.25f, 0.04f, 0.04f), brass, false);
            Prim(PrimitiveType.Cube, "ToolBox", train, new Vector3(-0.6f, 1.53f, 4.45f), new Vector3(0.7f, 0.22f, 0.4f), plank, false);
            Prim(PrimitiveType.Cube, "ToolBoxLid", train, new Vector3(-0.6f, 1.65f, 4.45f), new Vector3(0.74f, 0.03f, 0.44f), steelDark, false);

            // 石炭の山 (いびつな塊をたくさん積み上げた1つのメッシュ)
            var coal = new GameObject("CoalPile");
            coal.transform.SetParent(train, false);
            coal.transform.localPosition = new Vector3(0f, 1.4f, 6.35f);
            coal.AddComponent<MeshFilter>().sharedMesh = CoalPileMesh(1.12f, 1.35f, 0.6f);
            var coalRend = coal.AddComponent<MeshRenderer>();
            coalRend.sharedMaterial = Mat("Coal_Lump", new Color(0.07f, 0.07f, 0.08f), null, false, 0.45f);
            coalRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Bogies(train, wheels, new[] { 5f, 7f }, black, red);
        }

        /// <summary>
        /// 機関車 (z = 8.5 〜 18.8): 黒いボイラー (金の帯・手すり・配管)、ダイヤモンド形の煙突、蒸気ドーム・砂箱・鐘・汽笛、
        /// 煙室の扉と箱形の前照灯、横のシリンダーと主連棒、スポークと釣り合いおもりの付いた赤い動輪、すのこ状の排障器。
        /// 運転室は後ろが開いていて、プレイヤーの方から火室の扉の明かりと圧力計が見える
        /// </summary>
        void BuildSteamLocomotive(Transform train, List<Transform> wheels, float W, Material black, Material red, Material brass, Material window, Material steelDark, out Transform stack)
        {
            var glow = Mat("Firebox_Glow", new Color(1f, 0.55f, 0.2f), new Color(2.2f, 0.8f, 0.15f));
            var gaugeFace = Mat("Gauge_Face", new Color(0.95f, 0.93f, 0.85f));
            float wheelY = CarRailTop + 0.75f; // 動輪の中心の高さ

            // ---- 台枠・歩み板 ----
            Prim(PrimitiveType.Cube, "LocoFrame", train, new Vector3(0f, -0.1f, 13.6f), new Vector3(1.4f, 0.5f, 8.6f), red, false);
            foreach (float z in new[] { 12.6f, 15.2f })
                Prim(PrimitiveType.Cube, "BoilerSaddle", train, new Vector3(0f, 0.25f, z), new Vector3(1.0f, 0.3f, 0.5f), black, false);
            Prim(PrimitiveType.Cube, "BufferBeam", train, new Vector3(0f, -0.15f, 17.75f), new Vector3(W - 0.3f, 0.36f, 0.2f), red, false);
            Prim(PrimitiveType.Cube, "Coupler", train, new Vector3(0f, -0.2f, 17.95f), new Vector3(0.2f, 0.16f, 0.25f), steelDark, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "RunningBoard", train, new Vector3(s * 1.08f, 0.6f, 14.15f), new Vector3(0.76f, 0.05f, 6.3f), black, false);
                Prim(PrimitiveType.Cube, "RunningBoardEdge", train, new Vector3(s * 1.46f, 0.6f, 14.15f), new Vector3(0.03f, 0.08f, 6.3f), brass, false);
            }

            // ---- ボイラー・火室・煙室 ----
            ZCylinder("Boiler", train, new Vector3(0f, 1.0f, 13.9f), 1.6f, 5.4f, black, true);
            foreach (float z in new[] { 11.5f, 12.7f, 13.9f, 15.1f, 16.3f })
                ZCylinder("BoilerBand", train, new Vector3(0f, 1.0f, z), 1.64f, 0.06f, brass);
            Prim(PrimitiveType.Cube, "Firebox", train, new Vector3(0f, 0.75f, 11.4f), new Vector3(1.7f, 1.3f, 0.8f), black);
            ZCylinder("SmokeBox", train, new Vector3(0f, 1.0f, 17.0f), 1.7f, 0.8f, steelDark);
            ZCylinder("SmokeBoxRing", train, new Vector3(0f, 1.0f, 17.42f), 1.55f, 0.05f, brass);
            ZCylinder("SmokeBoxDoor", train, new Vector3(0f, 1.0f, 17.45f), 1.35f, 0.06f, black);
            ZCylinder("SmokeBoxHub", train, new Vector3(0f, 1.0f, 17.5f), 0.18f, 0.06f, brass);
            foreach (float y in new[] { 0.72f, 1.28f })
                Prim(PrimitiveType.Cube, "DoorHinge", train, new Vector3(0.22f, y, 17.49f), new Vector3(0.9f, 0.05f, 0.03f), brass, false);
            Prim(PrimitiveType.Cube, "NumberPlate", train, new Vector3(0f, 1.45f, 17.49f), new Vector3(0.4f, 0.2f, 0.02f), brass, false);

            // ---- ダイヤモンド形の煙突 (細い首 → 菱形にふくらむ → 火の粉よけの網) ----
            Prim(PrimitiveType.Cylinder, "StackBase", train, new Vector3(0f, 1.78f, 16.3f), new Vector3(0.55f, 0.12f, 0.55f), black, false);
            Prim(PrimitiveType.Cylinder, "Stack", train, new Vector3(0f, 2.2f, 16.3f), new Vector3(0.36f, 0.4f, 0.36f), black, false);
            float[] tierY = { 2.62f, 2.72f, 2.84f, 2.97f, 3.07f };
            float[] tierD = { 0.55f, 0.8f, 0.96f, 0.85f, 0.6f };
            for (int i = 0; i < tierY.Length; i++)
                Prim(PrimitiveType.Cylinder, "StackFlare", train, new Vector3(0f, tierY[i], 16.3f), new Vector3(tierD[i], 0.06f, tierD[i]), black, false);
            Prim(PrimitiveType.Cylinder, "StackRim", train, new Vector3(0f, 3.13f, 16.3f), new Vector3(0.64f, 0.02f, 0.64f), brass, false);
            Prim(PrimitiveType.Cylinder, "SparkScreen", train, new Vector3(0f, 3.14f, 16.3f), new Vector3(0.56f, 0.01f, 0.56f), steelDark, false);
            stack = Group(train, "SmokePoint");
            stack.localPosition = new Vector3(0f, 3.3f, 16.3f);

            // ---- ボイラーの上: 蒸気ドーム・砂箱・鐘・汽笛・安全弁 ----
            Prim(PrimitiveType.Cylinder, "SteamDomeBase", train, new Vector3(0f, 1.85f, 13.6f), new Vector3(0.62f, 0.18f, 0.62f), brass, false);
            Prim(PrimitiveType.Sphere, "SteamDome", train, new Vector3(0f, 2.02f, 13.6f), new Vector3(0.6f, 0.45f, 0.6f), brass, false);
            Prim(PrimitiveType.Cylinder, "SteamDomeCap", train, new Vector3(0f, 2.25f, 13.6f), new Vector3(0.22f, 0.04f, 0.22f), black, false);
            Prim(PrimitiveType.Cylinder, "SandDomeBase", train, new Vector3(0f, 1.85f, 15.1f), new Vector3(0.56f, 0.15f, 0.56f), black, false);
            Prim(PrimitiveType.Sphere, "SandDome", train, new Vector3(0f, 1.98f, 15.1f), new Vector3(0.55f, 0.4f, 0.55f), black, false);
            Prim(PrimitiveType.Cylinder, "SandDomeBand", train, new Vector3(0f, 1.98f, 15.1f), new Vector3(0.57f, 0.02f, 0.57f), brass, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "BellYoke", train, new Vector3(s * 0.18f, 2.0f, 12.4f), new Vector3(0.04f, 0.4f, 0.04f), black, false);
                Rod("SandPipe", train, new Vector3(s * 0.25f, 1.9f, 15.1f), new Vector3(s * 1.0f, 0.05f, 14.3f), 0.04f, steelDark);
            }
            Prim(PrimitiveType.Cube, "BellBar", train, new Vector3(0f, 2.18f, 12.4f), new Vector3(0.42f, 0.04f, 0.05f), black, false);
            Prim(PrimitiveType.Sphere, "Bell", train, new Vector3(0f, 2.0f, 12.4f), new Vector3(0.3f, 0.32f, 0.3f), brass, false);
            Prim(PrimitiveType.Cylinder, "BellLip", train, new Vector3(0f, 1.88f, 12.4f), new Vector3(0.34f, 0.02f, 0.34f), brass, false);
            Prim(PrimitiveType.Cylinder, "Whistle", train, new Vector3(0.25f, 2.0f, 11.5f), new Vector3(0.08f, 0.15f, 0.08f), brass, false);
            Prim(PrimitiveType.Cylinder, "WhistleTop", train, new Vector3(0.25f, 2.17f, 11.5f), new Vector3(0.12f, 0.03f, 0.12f), brass, false);
            Prim(PrimitiveType.Cylinder, "SafetyValve", train, new Vector3(-0.2f, 1.9f, 12.0f), new Vector3(0.12f, 0.1f, 0.12f), brass, false);

            // ---- ボイラーの横: 手すり・配管・空気ポンプ ----
            foreach (float s in new[] { -1f, 1f })
            {
                Rod("Handrail", train, new Vector3(s * 0.72f, 1.55f, 11.3f), new Vector3(s * 0.72f, 1.55f, 16.7f), 0.035f, brass);
                foreach (float z in new[] { 11.6f, 13.0f, 14.4f, 15.8f, 16.6f })
                    Rod("HandrailPost", train, new Vector3(s * 0.56f, 1.55f, z), new Vector3(s * 0.72f, 1.55f, z), 0.03f, brass);
                Rod("BoilerPipe", train, new Vector3(s * 0.82f, 0.85f, 11.4f), new Vector3(s * 0.82f, 0.85f, 16.4f), 0.06f, steelDark);
            }
            Prim(PrimitiveType.Cylinder, "AirPump", train, new Vector3(1.05f, 0.97f, 12.6f), new Vector3(0.3f, 0.35f, 0.3f), steelDark, false);
            foreach (float y in new[] { 0.64f, 1.3f })
                Prim(PrimitiveType.Cylinder, "AirPumpCap", train, new Vector3(1.05f, y, 12.6f), new Vector3(0.33f, 0.03f, 0.33f), brass, false);
            Rod("Injector", train, new Vector3(-0.82f, 0.95f, 11.6f), new Vector3(-1.0f, 0.62f, 12.4f), 0.07f, brass);

            // ---- 前: 箱形の前照灯・排障器 (すのこ状の V 字) ----
            Prim(PrimitiveType.Cube, "HeadlampBracket", train, new Vector3(0f, 1.85f, 17.1f), new Vector3(0.3f, 0.12f, 0.3f), black, false);
            Prim(PrimitiveType.Cube, "HeadlampBox", train, new Vector3(0f, 2.15f, 17.1f), new Vector3(0.5f, 0.45f, 0.5f), black, false);
            Prim(PrimitiveType.Cube, "HeadlampRoof", train, new Vector3(0f, 2.4f, 17.1f), new Vector3(0.56f, 0.06f, 0.56f), black, false);
            Prim(PrimitiveType.Cylinder, "HeadlampChimney", train, new Vector3(0f, 2.5f, 17.05f), new Vector3(0.1f, 0.06f, 0.1f), black, false);
            ZCylinder("HeadlampRim", train, new Vector3(0f, 2.15f, 17.35f), 0.42f, 0.02f, brass);
            ZCylinder("Headlamp", train, new Vector3(0f, 2.15f, 17.36f), 0.36f, 0.03f, Mat("Headlamp", new Color(1f, 0.95f, 0.7f), new Color(3f, 2.6f, 1.5f)));
            Rod("PilotSpine", train, new Vector3(0f, -0.9f, 18.75f), new Vector3(0f, -0.25f, 18.3f), 0.06f, red);
            foreach (float s in new[] { -1f, 1f })
            {
                Rod("PilotTop", train, new Vector3(0f, -0.25f, 18.3f), new Vector3(s * 1.1f, -0.25f, 17.4f), 0.06f, black);
                Rod("PilotBottom", train, new Vector3(0f, -0.9f, 18.75f), new Vector3(s * 1.15f, -0.9f, 17.9f), 0.06f, red);
                for (int k = 1; k <= 5; k++)
                {
                    float t = k / 5f;
                    Rod("PilotSlat", train, new Vector3(s * 1.15f * t, -0.9f, 18.75f - 0.85f * t), new Vector3(s * 1.1f * t, -0.25f, 18.3f - 0.9f * t), 0.05f, red);
                }
            }

            // ---- 横のシリンダー・クロスヘッド・連棒 ----
            foreach (float s in new[] { -1f, 1f })
            {
                ZCylinder("SteamCylinder", train, new Vector3(s * 1.2f, -0.2f, 15.8f), 0.6f, 1.0f, black);
                foreach (float z in new[] { 15.3f, 16.3f })
                    ZCylinder("CylinderCap", train, new Vector3(s * 1.2f, -0.2f, z), 0.64f, 0.04f, brass);
                Prim(PrimitiveType.Cube, "ValveChest", train, new Vector3(s * 1.2f, 0.2f, 15.8f), new Vector3(0.45f, 0.3f, 1.0f), black, false);
                Rod("SteamPipe", train, new Vector3(s * 0.55f, 1.0f, 16.7f), new Vector3(s * 1.15f, 0.3f, 16.1f), 0.12f, black);
                foreach (float y in new[] { -0.08f, -0.32f })
                    Rod("CrossheadGuide", train, new Vector3(s * 1.2f, y, 15.3f), new Vector3(s * 1.2f, y, 14.2f), 0.04f, steelDark);
                Prim(PrimitiveType.Cube, "Crosshead", train, new Vector3(s * 1.2f, -0.2f, 14.6f), new Vector3(0.12f, 0.2f, 0.2f), steelDark, false);
                Rod("MainRod", train, new Vector3(s * 1.27f, -0.2f, 14.6f), new Vector3(s * 1.27f, wheelY + 0.1f, 12.3f), 0.09f, steelDark);
                Prim(PrimitiveType.Cube, "SideRod", train, new Vector3(s * 1.24f, wheelY, 12.3f), new Vector3(0.05f, 0.08f, 3.6f), steelDark, false);
            }

            // ---- 大きな動輪 (片側3つ) と前の小さな先輪 ----
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float z in new[] { 10.6f, 12.3f, 14f })
                    wheels.Add(TrainWheel(train, new Vector3(s * (W / 2f - 0.25f), wheelY, z), 0.75f, red, black, brass));
                wheels.Add(TrainWheel(train, new Vector3(s * (W / 2f - 0.3f), CarRailTop + 0.4f, 16.9f), 0.4f, red, black));
            }
            Prim(PrimitiveType.Cube, "PilotTruck", train, new Vector3(0f, CarRailTop + 0.45f, 16.9f), new Vector3(1.8f, 0.18f, 0.9f), black, false);

            // ---- 運転室 (後ろが開いている) ----
            Prim(PrimitiveType.Cube, "CabFloor", train, new Vector3(0f, 0.0f, 10.0f), new Vector3(W - 0.1f, 0.1f, 2.2f), black, false);
            Prim(PrimitiveType.Cube, "FallPlate", train, new Vector3(0f, 0.05f, 8.45f), new Vector3(1.6f, 0.05f, 1.1f), steelDark, false);
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (W / 2f - 0.04f);
                Prim(PrimitiveType.Cube, "CabSide", train, new Vector3(x, 0.65f, 10.0f), new Vector3(0.08f, 1.2f, 2.0f), red);
                Prim(PrimitiveType.Cube, "CabHeader", train, new Vector3(x, 2.38f, 10.0f), new Vector3(0.08f, 0.25f, 2.0f), red, false);
                foreach (float z in new[] { 9.06f, 10.0f, 10.94f })
                    Prim(PrimitiveType.Cube, "CabPost", train, new Vector3(x, 1.86f, z), new Vector3(0.08f, 1.25f, 0.12f), red, false);
                Prim(PrimitiveType.Cube, "CabSill", train, new Vector3(x, 1.27f, 10.0f), new Vector3(0.12f, 0.04f, 2.0f), brass, false);
                foreach (float z in new[] { 9.53f, 10.47f })
                    Prim(PrimitiveType.Cube, "CabWindow", train, new Vector3(x, 1.84f, z), new Vector3(0.02f, 0.85f, 0.8f), window, false);
                // 横の番号の札
                Prim(PrimitiveType.Cube, "CabNumberPlate", train, new Vector3(s * (W / 2f + 0.005f), 0.75f, 10.0f), new Vector3(0.02f, 0.32f, 0.55f), brass, false);
                MakeText(train, "CabNumber", "7", new Vector3(s * (W / 2f + 0.02f), 0.75f, 10.0f), 0.26f, new Color(0.5f, 0.08f, 0.05f))
                    .transform.localRotation = Quaternion.Euler(0f, -90f * s, 0f);
                // 前の壁 (ボイラーの左右) と前の窓、後ろの握り棒、乗り降りのステップ
                Prim(PrimitiveType.Cube, "CabFront", train, new Vector3(s * 1.1f, 1.3f, 10.96f), new Vector3(0.55f, 2.5f, 0.08f), red, false);
                Prim(PrimitiveType.Cube, "CabFrontWindow", train, new Vector3(s * 1.05f, 2.0f, 10.91f), new Vector3(0.35f, 0.4f, 0.02f), window, false);
                Prim(PrimitiveType.Cylinder, "GrabIron", train, new Vector3(s * (W / 2f + 0.03f), 1.15f, 8.98f), new Vector3(0.04f, 0.65f, 0.04f), brass, false);
                Prim(PrimitiveType.Cube, "CabStep", train, new Vector3(s * (W / 2f - 0.1f), -0.35f, 9.2f), new Vector3(0.3f, 0.04f, 0.35f), black, false);
            }
            Prim(PrimitiveType.Cube, "CabFrontTop", train, new Vector3(0f, 2.2f, 10.96f), new Vector3(1.1f, 0.8f, 0.08f), red, false);
            var cabRoof = Prim(PrimitiveType.Cylinder, "CabRoof", train, new Vector3(0f, 2.62f, 10.0f), new Vector3(W + 0.3f, 1.25f, 0.35f), black, false);
            cabRoof.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, "CabRoofVent", train, new Vector3(0f, 2.83f, 10.0f), new Vector3(0.5f, 0.12f, 0.8f), black, false);
            // 運転台 (火室の後ろの面): 明かりがもれる焚き口の扉・圧力計・水面計・加減弁のてこ・逆転てこ
            Prim(PrimitiveType.Cube, "FireDoorFrame", train, new Vector3(0f, 0.65f, 10.985f), new Vector3(0.6f, 0.5f, 0.02f), brass, false);
            Prim(PrimitiveType.Cube, "FireDoor", train, new Vector3(0f, 0.65f, 10.97f), new Vector3(0.5f, 0.4f, 0.02f), glow, false);
            foreach (float s in new[] { -1f, 1f })
            {
                ZCylinder("Gauge", train, new Vector3(s * 0.3f, 1.45f, 10.97f), 0.18f, 0.03f, brass);
                ZCylinder("GaugeFace", train, new Vector3(s * 0.3f, 1.45f, 10.955f), 0.14f, 0.01f, gaugeFace);
            }
            Prim(PrimitiveType.Cube, "WaterGlass", train, new Vector3(-0.55f, 1.2f, 10.95f), new Vector3(0.04f, 0.3f, 0.04f), brass, false);
            Rod("Throttle", train, new Vector3(0f, 1.6f, 10.95f), new Vector3(0.45f, 1.3f, 10.6f), 0.04f, steelDark);
            Rod("Reverser", train, new Vector3(0.9f, 0.05f, 10.4f), new Vector3(0.85f, 1.0f, 10.2f), 0.05f, steelDark);
        }

        // Z の向きに寝かせた円柱 (ボイラー・シリンダー・円盤など)。d = 直径, len = 長さ
        GameObject ZCylinder(string name, Transform parent, Vector3 pos, float d, float len, Material mat, bool collider = false)
        {
            var g = Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(d, len / 2f, d), mat, collider);
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return g;
        }

        // 2点を結ぶ細い角棒 (手すり・配管・連棒・排障器の棒)
        GameObject Rod(string name, Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            var g = Prim(PrimitiveType.Cube, name, parent, (a + b) * 0.5f, new Vector3(thickness, thickness, (b - a).magnitude), mat, false);
            g.transform.localRotation = Quaternion.LookRotation(b - a);
            return g;
        }

        /// <summary>
        /// 炭水車に山盛りに積もった石炭 (1つのメッシュ)。発掘モードのごつごつした岩のメッシュを小さく、ばらばらにゆがめて、
        /// 真ん中が高い山の形にたくさん積み重ねる (下の段は平たい大きな塊で隙間を埋める)。原点が山の底の真ん中
        /// </summary>
        Mesh CoalPileMesh(float halfX, float halfZ, float height)
        {
            const string name = "Shape_CoalPile";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var rng = new System.Random(4242);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float Height(float x, float z)
            {
                float u = x / halfX, w = z / halfZ;
                return height * Mathf.Clamp01(1f - u * u) * Mathf.Clamp01(1f - w * w * w * w);
            }
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Lump(Vector3 pos, Vector3 size, Quaternion rot)
            {
                var rock = RockMesh(rng.Next(RockVariants));
                var v = rock.vertices;
                foreach (int i in rock.triangles)
                {
                    tris.Add(verts.Count);
                    verts.Add(pos + rot * Vector3.Scale(v[i], size));
                }
            }
            // 下の段: 平たい大きな塊 (横にだけ回す)
            for (int i = 0; i < 14; i++)
            {
                float x = Rand(-0.75f, 0.75f) * halfX, z = Rand(-0.8f, 0.8f) * halfZ;
                Lump(new Vector3(x, Height(x, z) * 0.45f, z), new Vector3(Rand(0.6f, 0.9f), Rand(0.35f, 0.5f), Rand(0.6f, 0.9f)), Quaternion.Euler(0f, Rand(0f, 360f), 0f));
            }
            // 表面: 大きさも形もばらばらの塊を、山の形に沿ってたくさん
            for (int i = 0; i < 150; i++)
            {
                float x = Rand(-0.95f, 0.95f) * halfX, z = Rand(-0.95f, 0.95f) * halfZ;
                float s = i < 12 ? Rand(0.3f, 0.42f) : Rand(0.12f, 0.28f);
                var size = new Vector3(s * Rand(0.7f, 1.3f), s * Rand(0.6f, 1.1f), s * Rand(0.7f, 1.3f));
                Lump(new Vector3(x, Height(x, z) + Rand(-0.04f, 0.03f), z), size, Quaternion.Euler(Rand(0f, 360f), Rand(0f, 360f), Rand(0f, 360f)));
            }
            return FinishMesh(name, verts, tris);
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

        // 車輪 (ローカル X まわりに回るグループ。回っているのが分かるように、スポークを1本付ける)。
        // hubMat を渡すと大きな動輪: 黒い輪金・10本のスポーク・金のハブとクランクピン・釣り合いおもり
        Transform TrainWheel(Transform parent, Vector3 pos, float r, Material mat, Material spokeMat, Material hubMat = null)
        {
            var w = Group(parent, "Wheel");
            w.localPosition = pos;
            float side = Mathf.Sign(pos.x);
            var disc = Prim(PrimitiveType.Cylinder, "Disc", w, Vector3.zero, new Vector3(r * 2f, 0.06f, r * 2f), mat, false);
            disc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (hubMat == null)
            {
                Prim(PrimitiveType.Cube, "Spoke", w, new Vector3(side * 0.07f, 0f, 0f), new Vector3(0.02f, r * 1.8f, 0.08f), spokeMat, false);
                return w;
            }
            Prim(PrimitiveType.Cylinder, "Tire", w, Vector3.zero, new Vector3(r * 2f + 0.1f, 0.05f, r * 2f + 0.1f), spokeMat, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int k = 0; k < 5; k++)
                Prim(PrimitiveType.Cube, "Spoke", w, new Vector3(side * 0.035f, 0f, 0f), new Vector3(0.02f, r * 1.75f, 0.05f), spokeMat, false).transform.localRotation = Quaternion.Euler(k * 36f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Counterweight", w, new Vector3(side * 0.045f, -r * 0.55f, 0f), new Vector3(0.03f, r * 0.45f, r * 0.8f), spokeMat, false);
            Prim(PrimitiveType.Cylinder, "Hub", w, new Vector3(side * 0.05f, 0f, 0f), new Vector3(0.26f, 0.04f, 0.26f), hubMat, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cylinder, "CrankPin", w, new Vector3(side * 0.08f, r * 0.45f, 0f), new Vector3(0.1f, 0.04f, 0.1f), hubMat, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
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
            labels.Add(PointSign(body, new Vector3(0f, PoleH * 0.5f, 0f), 0.68f)); // 遠くからでも読める大きさ

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
            var labels = new List<TextMesh> { PointSign(body, new Vector3(0f, PoleH * 0.5f, 0.4f), 0.9f) };

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

        /// <summary>
        /// 古い自動車 (フォード・モデル T 風): 黒い車体と長いボンネット、真ちゅうのラジエーターとヘッドライト、黒い幌の屋根、木のスポークの細い車輪、
        /// 運転席のカウボーイ。ほかの的と同じく2本の棒の上に乗って、でこぼこ道を跳ねながら速く走る。
        /// 点数の札は屋根の上に高く掲げる (屋根の上から見下ろしても、車に隠れずに読める)。当てると車ごと後ろへ倒れる
        /// </summary>
        WesternRider BuildModelT(Transform parent, int index)
        {
            var root = Group(parent, $"ModelT_{index}");
            root.gameObject.SetActive(false);
            var body = Group(root, "Body");
            const float PoleH = 2.0f;
            var poleMat = Mat("Rider_Pole", new Color(0.35f, 0.3f, 0.25f), null, false, 0.6f);
            foreach (float z in new[] { -0.7f, 0.7f })
            {
                Prim(PrimitiveType.Cylinder, "Pole", body, new Vector3(0f, PoleH / 2f, z), new Vector3(0.07f, PoleH / 2f, 0.07f), poleMat, false);
                Prim(PrimitiveType.Cylinder, "PoleFoot", body, new Vector3(0f, 0.03f, z), new Vector3(0.45f, 0.03f, 0.45f), poleMat, false);
            }

            var mount = Group(body, "Mount");
            mount.localPosition = new Vector3(0f, PoleH, 0f);
            var figure = Group(mount, "Figure");
            var black = Mat("ModelT_Black", new Color(0.05f, 0.05f, 0.06f), null, false, 0.75f);
            var brass = Mat("Revolver_Brass", new Color(0.9f, 0.7f, 0.3f), null, false, 0.7f);
            var leather = Mat("ModelT_Leather", new Color(0.35f, 0.18f, 0.1f), null, false, 0.4f);
            var tire = Mat("ModelT_Tire", new Color(0.12f, 0.11f, 0.1f));
            var spokeWood = Mat("ModelT_Spoke", new Color(0.7f, 0.5f, 0.28f));
            var glass = Mat("ModelT_Glass", new Color(0.7f, 0.85f, 0.95f), new Color(0.1f, 0.15f, 0.2f), false, 0.9f);
            var lampGlow = Mat("ModelT_Lamp", new Color(1f, 0.9f, 0.6f), new Color(1.5f, 1.2f, 0.6f));
            // 車台・ボンネット・ラジエーター
            Prim(PrimitiveType.Cube, "Chassis", figure, new Vector3(0f, 0.3f, 0f), new Vector3(1.0f, 0.12f, 2.6f), black);
            Prim(PrimitiveType.Cube, "Hood", figure, new Vector3(0f, 0.55f, 0.85f), new Vector3(0.75f, 0.42f, 0.9f), black);
            Prim(PrimitiveType.Cube, "Radiator", figure, new Vector3(0f, 0.58f, 1.32f), new Vector3(0.7f, 0.55f, 0.08f), brass, false);
            Prim(PrimitiveType.Cube, "RadiatorGrill", figure, new Vector3(0f, 0.56f, 1.365f), new Vector3(0.52f, 0.4f, 0.01f), black, false);
            // 客席 (後ろが高い箱) と座席
            Prim(PrimitiveType.Cube, "Tub", figure, new Vector3(0f, 0.62f, -0.45f), new Vector3(1.05f, 0.55f, 1.4f), black);
            Prim(PrimitiveType.Cube, "Seat", figure, new Vector3(0f, 0.85f, -0.55f), new Vector3(0.95f, 0.12f, 0.9f), leather, false);
            Prim(PrimitiveType.Cube, "SeatBack", figure, new Vector3(0f, 1.1f, -1.05f), new Vector3(0.95f, 0.5f, 0.12f), leather, false);
            // 窓ガラス・幌の屋根と柱
            var shield = Prim(PrimitiveType.Cube, "Windshield", figure, new Vector3(0f, 1.12f, 0.32f), new Vector3(0.95f, 0.5f, 0.02f), glass, false);
            shield.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Top", figure, new Vector3(0f, 1.75f, -0.4f), new Vector3(1.1f, 0.05f, 1.6f), black, false);
            foreach (float x in new[] { -0.5f, 0.5f })
                foreach (float z in new[] { 0.3f, -1.1f })
                    Prim(PrimitiveType.Cylinder, "TopPost", figure, new Vector3(x, 1.3f, z), new Vector3(0.04f, 0.45f, 0.04f), black, false);
            // 泥よけとステップ
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "RunningBoard", figure, new Vector3(s * 0.62f, 0.3f, 0.05f), new Vector3(0.22f, 0.04f, 1.3f), black, false);
                foreach (float z in new[] { 0.95f, -0.85f })
                {
                    var fender = Prim(PrimitiveType.Cube, "Fender", figure, new Vector3(s * 0.62f, 0.48f, z), new Vector3(0.26f, 0.04f, 0.75f), black, false);
                    fender.transform.localRotation = Quaternion.Euler(z > 0f ? -18f : 18f, 0f, 0f);
                    ModelTWheel(figure, new Vector3(s * 0.62f, 0.12f, z), tire, spokeWood, brass);
                }
                Prim(PrimitiveType.Cylinder, "Headlamp", figure, new Vector3(s * 0.42f, 0.78f, 1.25f), new Vector3(0.16f, 0.06f, 0.16f), brass, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Sphere, "HeadlampGlass", figure, new Vector3(s * 0.42f, 0.78f, 1.31f), new Vector3(0.12f, 0.12f, 0.04f), lampGlow, false);
            }
            // ハンドルと運転手
            var wheel = Group(figure, "SteeringWheel");
            wheel.localPosition = new Vector3(-0.22f, 1.05f, 0.05f);
            wheel.localRotation = Quaternion.Euler(-55f, 0f, 0f);
            RingSegments(wheel, "Steer", 0.14f, 10, 0.025f, black);
            Rider(figure, new Vector3(-0.22f, 0.9f, -0.45f), index % 2 == 1, index + 2);

            // 点数の札は屋根の上に高く掲げる
            Prim(PrimitiveType.Cylinder, "SignPost", body, new Vector3(0f, PoleH + 2.15f, -0.4f), new Vector3(0.05f, 0.25f, 0.05f), poleMat, false);
            var labels = new List<TextMesh> { PointSign(body, new Vector3(0f, PoleH + 2.75f, -0.4f), 0.8f) };

            var target = mount.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Rider;
            target.manualControl = true;
            target.visual = figure;
            target.SetAvailable(false);

            var rider = root.gameObject.AddComponent<WesternRider>();
            rider.displayName = "MODEL T";
            rider.body = body;
            rider.hitTarget = target;
            rider.pointLabels = labels.ToArray();
            rider.groundY = DesertY;
            rider.pointChoices = new[] { 500, 800 };
            rider.speed = 4.2f;                 // 馬より速く、列車を追い越したり置いていかれたり
            rider.distance = new Vector2(7f, 11f);
            rider.gallopHeight = 0.05f;          // でこぼこ道で小刻みに跳ねる
            rider.gallopSpeed = 16f;
            rider.routeChoices = new[] { WesternRider.Route.Overtake, WesternRider.Route.FallBehind, WesternRider.Route.Approach };
            return rider;
        }

        // 木のスポークの細い車輪 (黒いタイヤ・真ちゅうのハブ)。横を向けて置く
        void ModelTWheel(Transform parent, Vector3 pos, Material tire, Material spoke, Material hub)
        {
            var w = Group(parent, "Wheel");
            w.localPosition = pos;
            w.localRotation = Quaternion.Euler(0f, 90f, 0f); // 輪の面が車の横を向く
            RingSegments(w, "Tire", 0.3f, 14, 0.06f, tire);
            for (int k = 0; k < 6; k++)
            {
                var sp = Prim(PrimitiveType.Cube, "Spoke", w, Vector3.zero, new Vector3(0.025f, 0.58f, 0.025f), spoke, false);
                sp.transform.localRotation = Quaternion.Euler(0f, 0f, k * 30f);
            }
            Prim(PrimitiveType.Cylinder, "Hub", w, Vector3.zero, new Vector3(0.09f, 0.03f, 0.09f), hub, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
