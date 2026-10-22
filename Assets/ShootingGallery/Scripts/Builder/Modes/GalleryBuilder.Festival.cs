using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、夏祭りモード (提灯の的を撃つと始まる) の生成:
    /// プレイヤーの前・右・後ろ・左に並ぶ4軒の屋台 (射的・輪投げ・金魚すくい・食べ物)、頭の上の提灯の飾り、花火の材質、提灯の的の見た目。
    /// 屋台はどれも視界いっぱいくらいの幅 (6.4m) で、手前がプレイヤーから 3.6m。屋台のローカル座標は +Z が奥 (プレイヤーから離れる方)。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float FestivalStallDistance = 4.6f; // プレイヤーから屋台の原点まで
        const float FestivalStallWidth = 6.4f;
        const float FestivalFrontZ = -1.0f;       // 屋台のローカルで、いちばん手前 (カウンター) の位置
        const float FestivalRoofY = 3.3f;

        FestivalStage BuildFestival()
        {
            var go = new GameObject("FestivalStage");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;

            var shateki = BuildShatekiStall(root);
            var ringToss = BuildRingTossStall(root);
            var goldfish = BuildGoldfishStall(root, out var water, out var fish, out var scoopPoi, out var bowl);
            var food = BuildFoodStall(root, out var slots, out var balance);
            var lanterns = BuildFestivalLanterns(root);

            var stage = go.AddComponent<FestivalStage>();
            stage.stalls = new[] { shateki, ringToss, goldfish, food };
            stage.stallWeapons = new[] { WeaponType.CorkGun, WeaponType.RingToss, WeaponType.Poi, WeaponType.Coin };
            stage.decorations = lanterns;
            stage.water = water;
            stage.fish = fish;
            stage.scoopPoi = scoopPoi;
            stage.scoopBowl = bowl;
            stage.tableSlots = slots;
            stage.balanceText = balance;
            stage.font = font;
            stage.splashMaterial = TransparentMat("Festival_Splash", new Color(0.8f, 0.92f, 1f, 0.6f));
            // 花火の火の粉 (光る色。明るいほどまぶしく見える)
            stage.fireworkMaterials = new[]
            {
                Mat("Hanabi_Gold", new Color(2.6f, 1.8f, 0.6f), null, true), // しだれ柳はいつもこの色
                Mat("Hanabi_Red", new Color(2.6f, 0.5f, 0.4f), null, true),
                Mat("Hanabi_Green", new Color(0.6f, 2.4f, 0.7f), null, true),
                Mat("Hanabi_Blue", new Color(0.6f, 1.2f, 2.8f), null, true),
                Mat("Hanabi_Pink", new Color(2.6f, 0.8f, 2f), null, true),
                Mat("Hanabi_White", new Color(2.4f, 2.4f, 2.2f), null, true),
            };
            stage.fireworkShellMaterial = Mat("Hanabi_Shell", new Color(2f, 1.6f, 1f), null, true);
            stage.nightSky = BuildFestivalSky(root, out var clusters);
            stage.starClusters = clusters;
            go.SetActive(true);
            return stage;
        }

        /// <summary>
        /// 夏祭りの星空: 空いっぱいの星 (色の違う固まりごとにまたたく) と、空を斜めに横切る天の川。
        /// カメラの見える距離 (100m) より手前、花火 (60m くらいまで) より奥に置き、地平線より上にだけ散らす。隠した状態で作る
        /// </summary>
        GameObject BuildFestivalSky(Transform root, out Renderer[] clusters)
        {
            var sky = Group(root, "FestivalSky");
            Color[] starColors =
            {
                new Color(1f, 1f, 1f), new Color(0.75f, 0.85f, 1f), new Color(1f, 0.93f, 0.75f),
                new Color(1f, 0.8f, 0.6f), new Color(0.7f, 0.95f, 1f), new Color(0.95f, 0.85f, 1f)
            };
            var list = new List<Renderer>();
            for (int c = 0; c < 10; c++)
                list.Add(StarCluster(sky, $"Stars_{c}", StarFieldMesh($"FestivalStars_{c}", 170, 72f, 94f, 0.14f, 0.5f, null, 0.04f),
                                     Mat($"Festival_Star_{c % starColors.Length}", starColors[c % starColors.Length], null, true)));
            var band = new Vector3(0.5f, 0.35f, 0.8f).normalized; // (天の川が空の高い所を横切るように)
            for (int c = 0; c < 6; c++)
                list.Add(StarCluster(sky, $"MilkyWay_{c}", StarFieldMesh($"FestivalMilkyWay_{c}", 320, 76f, 95f, 0.08f, 0.22f, band, 0.04f),
                                     Mat($"Festival_MilkyWay_{c % 3}", Color.Lerp(starColors[c % 3], new Color(0.8f, 0.85f, 1f), 0.5f), null, true)));
            clusters = list.ToArray();
            sky.gameObject.SetActive(false);
            return sky.gameObject;
        }

        // ------------------------------------------------------------------
        // 共通: 屋台の置き場所と骨組み
        // ------------------------------------------------------------------

        // index 番目 (0 = 前, 1 = 右, 2 = 後ろ, 3 = 左) の屋台の入れ物。止まる場所に、隠した状態で作る (再生すると FestivalStage が地下へ隠す)
        Transform FestivalStallRoot(Transform root, string name, int index)
        {
            var t = Group(root, name);
            t.gameObject.SetActive(false);
            var rot = Quaternion.Euler(0f, index * 90f, 0f);
            t.localPosition = rot * new Vector3(0f, 0f, FestivalStallDistance);
            t.localRotation = rot;
            return t;
        }

        Stall FinishFestivalStall(Transform t, List<Target> targets)
        {
            var stall = t.gameObject.AddComponent<Stall>();
            stall.targets = targets.ToArray();
            stall.hiddenDepth = 7f; // 背の高い屋台なので深く沈める
            stall.SetTargetsAvailable(false);
            return stall;
        }

        /// <summary>
        /// 柱・横の幕・紅白のしま模様の屋根・前の幕・軒下の提灯・看板。backZ = 奥の柱の位置。
        /// fullRoof = false なら屋根は手前だけ (輪投げの屋台: 奥まで投げる輪が屋根に当たらないように)
        /// </summary>
        void FestivalStallFrame(Transform t, Material cloth, string sign, float backZ, bool fullRoof, string subtitle = null)
        {
            var wood = Mat("Festival_Wood", new Color(0.5f, 0.32f, 0.18f), null, false, 0.3f);
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));
            float hw = FestivalStallWidth / 2f;
            const float H = FestivalRoofY;

            foreach (float x in new[] { -hw, hw })
            {
                foreach (float z in new[] { FestivalFrontZ, backZ })
                    Prim(PrimitiveType.Cylinder, "Post", t, new Vector3(x, H / 2f, z), new Vector3(0.12f, H / 2f, 0.12f), wood);
                // 横の幕 (腰の高さまで)
                Prim(PrimitiveType.Cube, "SidePanel", t, new Vector3(x, 0.9f, (FestivalFrontZ + backZ) / 2f), new Vector3(0.04f, 1.8f, backZ - FestivalFrontZ), cloth);
            }

            // 紅白のしま模様の屋根 (当たり判定なし。投げた物や弾をさえぎらない)
            float roofFrom = FestivalFrontZ - 0.4f, roofTo = fullRoof ? backZ + 0.2f : FestivalFrontZ + 1.4f;
            const int Stripes = 10;
            float stripeW = (FestivalStallWidth + 0.4f) / Stripes;
            for (int i = 0; i < Stripes; i++)
                Prim(PrimitiveType.Cube, "Roof", t, new Vector3(-hw - 0.2f + (i + 0.5f) * stripeW, H + 0.04f, (roofFrom + roofTo) / 2f), new Vector3(stripeW, 0.06f, roofTo - roofFrom), i % 2 == 0 ? cloth : white, false);
            // 前の幕
            const int Flaps = 14;
            float flapW = (FestivalStallWidth + 0.4f) / Flaps;
            for (int i = 0; i < Flaps; i++)
                Prim(PrimitiveType.Cube, "Valance", t, new Vector3(-hw - 0.2f + (i + 0.5f) * flapW, H - 0.2f, roofFrom + 0.02f), new Vector3(flapW, 0.4f, 0.03f), i % 2 == 0 ? cloth : white, false);
            // 軒下の提灯
            for (int i = 0; i < 6; i++)
                Chochin(t, new Vector3(-hw + 0.55f + i * (FestivalStallWidth - 1.1f) / 5f, H - 0.68f, roofFrom + 0.1f), 0.24f, i % 2 == 0);
            // 看板 (屋根の上)
            Prim(PrimitiveType.Cube, "SignBoard", t, new Vector3(0f, H + 0.5f, roofFrom + 0.15f), new Vector3(3.6f, 0.7f, 0.08f), Mat("Festival_Sign", new Color(0.12f, 0.06f, 0.04f)), false);
            Prim(PrimitiveType.Cube, "SignFrame", t, new Vector3(0f, H + 0.5f, roofFrom + 0.17f), new Vector3(3.75f, 0.82f, 0.06f), cloth, false);
            // 看板の名前と、その下の小さな説明 (この屋台でお金がたまる / 使える)
            bool sub = !string.IsNullOrEmpty(subtitle);
            MakeText(t, "SignText", sign, new Vector3(0f, H + (sub ? 0.58f : 0.5f), roofFrom + 0.1f), sub ? 0.3f : 0.36f, new Color(1f, 0.88f, 0.4f));
            if (sub) MakeText(t, "SignSubtitle", subtitle, new Vector3(0f, H + 0.3f, roofFrom + 0.1f), 0.11f, new Color(0.6f, 1f, 0.7f));
        }

        // 提灯 (光る胴と、上下の黒いふた)。当たり判定なし
        void Chochin(Transform parent, Vector3 pos, float size, bool red)
        {
            var body = red ? Mat("Chochin_Red", new Color(0.95f, 0.22f, 0.12f), new Color(1.5f, 0.35f, 0.1f))
                           : Mat("Chochin_White", new Color(1f, 0.95f, 0.85f), new Color(1.3f, 1.05f, 0.7f));
            var cap = Mat("Chochin_Cap", new Color(0.08f, 0.06f, 0.05f));
            Prim(PrimitiveType.Sphere, "Chochin", parent, pos, new Vector3(size, size * 1.3f, size), body, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "ChochinCap", parent, pos + Vector3.up * (s * size * 0.62f), new Vector3(size * 0.55f, size * 0.05f, size * 0.55f), cap, false);
        }

        // ------------------------------------------------------------------
        // 射的: 縦に重なった3枚の奥のない棚に景品。コルクが当たると後ろへ倒れて、棚の後ろへ落ちる
        // ------------------------------------------------------------------

        Stall BuildShatekiStall(Transform root)
        {
            var t = FestivalStallRoot(root, "Festival_Shateki", 0);
            var cloth = Mat("Festival_Cloth_Red", new Color(0.8f, 0.12f, 0.12f));
            FestivalStallFrame(t, cloth, "SHATEKI", 1.8f, true, "KNOCK DOWN PRIZES = EARN MONEY");
            var wood = Mat("Festival_Wood", new Color(0.5f, 0.32f, 0.18f), null, false, 0.3f);
            var felt = Mat("Festival_Felt_Red", new Color(0.55f, 0.06f, 0.08f));
            var card = Mat("Festival_Card", new Color(0.97f, 0.96f, 0.9f));
            Prim(PrimitiveType.Cube, "BackWall", t, new Vector3(0f, 1.6f, 1.8f), new Vector3(FestivalStallWidth, 3.2f, 0.06f), Mat("Festival_Wall_Cream", new Color(0.95f, 0.88f, 0.7f)));
            Prim(PrimitiveType.Cube, "Counter", t, new Vector3(0f, 0.45f, FestivalFrontZ + 0.2f), new Vector3(FestivalStallWidth - 0.4f, 0.9f, 0.4f), wood);
            Prim(PrimitiveType.Cube, "CounterCloth", t, new Vector3(0f, 0.6f, FestivalFrontZ - 0.005f), new Vector3(FestivalStallWidth - 0.4f, 0.45f, 0.01f), cloth, false);
            // カウンターに置いてあるコルク銃 (飾り)
            for (int i = 0; i < 3; i++)
            {
                var g = Prim(PrimitiveType.Cube, "SpareCorkGun", t, new Vector3(-1.6f + i * 1.6f, 0.93f, FestivalFrontZ + 0.2f), new Vector3(0.05f, 0.05f, 0.5f), Mat("CorkGun_Wood", new Color(0.62f, 0.4f, 0.2f), null, false, 0.35f), false);
                g.transform.localRotation = Quaternion.Euler(0f, 70f + i * 15f, 0f);
            }

            // 縦に4枚、背の高いラックのように重なった奥のない棚 (浅い棚板を一つの面にそろえて積む。倒れた景品は棚板の後ろからそのまま床へ落ちる)
            float[] shelfY = { 0.95f, 1.45f, 1.95f, 2.45f };
            const float ShelfZ = 0.0f, ShelfDepth = 0.26f, ShelfW = FestivalStallWidth - 0.7f;
            for (int i = 0; i < shelfY.Length; i++)
            {
                Prim(PrimitiveType.Cube, $"Shelf_{i}", t, new Vector3(0f, shelfY[i] - 0.02f, ShelfZ), new Vector3(ShelfW, 0.04f, ShelfDepth), felt);
                Prim(PrimitiveType.Cube, "ShelfLip", t, new Vector3(0f, shelfY[i] - 0.06f, ShelfZ - ShelfDepth / 2f), new Vector3(ShelfW, 0.12f, 0.02f), wood, false);
            }
            // 両はしの背の高い側板 (棚が縦に立っているのがよく分かるように)
            float rackH = shelfY[shelfY.Length - 1] + 0.45f;
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "RackSide", t, new Vector3(s * (ShelfW / 2f + 0.03f), rackH / 2f, ShelfZ), new Vector3(0.06f, rackH, ShelfDepth + 0.04f), wood);
            Prim(PrimitiveType.Cube, "RackTop", t, new Vector3(0f, rackH, ShelfZ), new Vector3(ShelfW + 0.12f, 0.06f, ShelfDepth + 0.04f), wood, false);

            // 景品: 下の段は大きくて取りやすい (低い点数)、上の段ほど小さくて高い点数
            var rows = new (string name, int points, System.Func<Transform, Vector3> build)[][]
            {
                new (string, int, System.Func<Transform, Vector3>)[] { ("BEAR", 100, PrizeBear), ("PENGUIN", 100, PrizePenguin), ("TRUCK", 100, PrizeTruck), ("DINO", 100, PrizeDino), ("CHIPS", 100, PrizeChips) },
                new (string, int, System.Func<Transform, Vector3>)[] { ("ROBOT_BOX", 200, PrizeRobotBox), ("CAR", 200, PrizeCar), ("DUCK", 200, PrizeDuck), ("CARAMEL", 200, PrizeCaramel), ("BALL", 200, PrizeBall) },
                new (string, int, System.Func<Transform, Vector3>)[] { ("DARUMA", 300, PrizeDaruma), ("ROBOT", 300, PrizeRobot), ("CHOCOLATE", 300, PrizeChocolate), ("PIGGY", 300, PrizePiggy), ("ROCKET", 300, PrizeRocket) },
                new (string, int, System.Func<Transform, Vector3>)[] { ("LUCKY_CAT", 500, PrizeLuckyCat), ("KOKESHI", 300, PrizeKokeshi), ("GAME", 500, PrizeGame), ("TOP", 300, PrizeTop), ("KENDAMA", 300, PrizeKendama), ("RABBIT", 300, PrizeRabbitSmall) },
            };
            var targets = new List<Target>();
            for (int r = 0; r < rows.Length; r++)
                for (int k = 0; k < rows[r].Length; k++)
                {
                    var (name, points, build) = rows[r][k];
                    int n = rows[r].Length;
                    float x = (k - (n - 1) / 2f) * (ShelfW - 0.5f) / (n - 1);
                    targets.Add(Prize(t, name, new Vector3(x, shelfY[r], ShelfZ - 0.03f), points, build, shelfY[r]));
                    // 棚板の前のふちに点数の札
                    Prim(PrimitiveType.Cube, "PriceCard", t, new Vector3(x, shelfY[r] - 0.06f, ShelfZ - ShelfDepth / 2f - 0.015f), new Vector3(0.2f, 0.09f, 0.01f), card, false);
                    MakeText(t, "PriceText", points.ToString(), new Vector3(x, shelfY[r] - 0.06f, ShelfZ - ShelfDepth / 2f - 0.022f), 0.07f, new Color(0.75f, 0.1f, 0.08f));
                }
            return FinishFestivalStall(t, targets);
        }

        // 景品 1つ (的)。build が見た目を Model の下に作り、大きさ (幅・高さ・奥行き) を返す。当たり判定はその大きさの見えない箱
        Target Prize(Transform parent, string name, Vector3 pos, int points, System.Func<Transform, Vector3> build, float shelfHeight)
        {
            var root = Group(parent, $"Prize_{name}");
            root.localPosition = pos;
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.FestivalPrize;
            target.points = points;
            target.respawnDelay = 4f;
            target.knockOffOnHit = true; // 後ろへ倒れて、奥のない棚の後ろから床へ落ちる
            target.knockBackSpeed = 1f;
            target.knockUpSpeed = 1f;
            target.knockMaxBack = 1.2f; // 奥の壁の手前まで
            target.knockDropHeight = shelfHeight; // 床まで落ちたら消える
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            var model = Group(pivot, "Model");
            var size = build(model);
            HitBox(pivot, size);
            return target;
        }

        // 見えない当たり判定の箱 (足元が原点)。見た目よりかなり大きく (横は約2倍、高さは上の棚にかからない 0.46m まで、手前にも張り出す)
        static void HitBox(Transform parent, Vector3 size)
        {
            var hit = new GameObject("Hit");
            hit.transform.SetParent(parent, false);
            float h = Mathf.Clamp(size.y * 1.6f, 0.3f, 0.46f);
            hit.transform.localPosition = new Vector3(0f, h / 2f, -0.05f);
            var box = hit.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Max(0.36f, size.x * 2f), h, Mathf.Max(0.3f, size.z * 1.8f));
        }

        // 飾りの部品 (当たり判定なし)
        GameObject Deco(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var g = Prim(type, type.ToString(), parent, pos, scale, mat, false);
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            return g;
        }

        Material PrizeMat(string name, Color color, float smooth = 0.3f) => Mat("Prize_" + name, color, null, false, smooth);
        Material Black => PrizeMat("Black", new Color(0.04f, 0.04f, 0.05f), 0.6f);

        // 2つの黒い目 (顔の前 -Z 側)
        void Eyes(Transform p, float y, float z, float dx, float size)
        {
            foreach (float s in new[] { -1f, 1f })
                Deco(PrimitiveType.Sphere, p, new Vector3(s * dx, y, z), Vector3.one * size, Black);
        }

        Vector3 PrizeBear(Transform p)
        {
            var fur = PrizeMat("Bear", new Color(0.55f, 0.35f, 0.2f), 0.1f);
            var tan = PrizeMat("Bear_Tan", new Color(0.85f, 0.7f, 0.5f), 0.1f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.12f, 0f), new Vector3(0.22f, 0.24f, 0.18f), fur);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.12f, -0.06f), new Vector3(0.12f, 0.14f, 0.08f), tan);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.17f, fur);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.28f, -0.075f), new Vector3(0.07f, 0.055f, 0.05f), tan);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.293f, -0.1f), Vector3.one * 0.022f, Black);
            Eyes(p, 0.32f, -0.07f, 0.035f, 0.022f);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(PrimitiveType.Sphere, p, new Vector3(s * 0.065f, 0.38f, 0f), Vector3.one * 0.06f, fur);
                Deco(PrimitiveType.Sphere, p, new Vector3(s * 0.12f, 0.15f, -0.02f), new Vector3(0.06f, 0.1f, 0.06f), fur);
                Deco(PrimitiveType.Sphere, p, new Vector3(s * 0.06f, 0.03f, -0.06f), new Vector3(0.07f, 0.06f, 0.09f), fur);
            }
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.22f, -0.05f), new Vector3(0.12f, 0.03f, 0.04f), PrizeMat("Ribbon_Red", new Color(0.9f, 0.1f, 0.15f)));
            return new Vector3(0.26f, 0.42f, 0.2f);
        }

        Vector3 PrizeRabbit(Transform p)
        {
            var white = PrizeMat("Rabbit", new Color(0.97f, 0.96f, 0.95f), 0.1f);
            var pink = PrizeMat("Rabbit_Pink", new Color(1f, 0.6f, 0.7f), 0.1f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.11f, 0f), new Vector3(0.19f, 0.22f, 0.16f), white);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.27f, 0f), Vector3.one * 0.15f, white);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(PrimitiveType.Capsule, p, new Vector3(s * 0.04f, 0.42f, 0f), new Vector3(0.05f, 0.09f, 0.03f), white, new Vector3(0f, 0f, s * -10f));
                Deco(PrimitiveType.Capsule, p, new Vector3(s * 0.04f, 0.42f, -0.012f), new Vector3(0.025f, 0.07f, 0.01f), pink, new Vector3(0f, 0f, s * -10f));
            }
            Eyes(p, 0.29f, -0.065f, 0.032f, 0.022f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.26f, -0.075f), Vector3.one * 0.02f, pink);
            return new Vector3(0.2f, 0.5f, 0.17f);
        }

        // 小さなうさぎ (いちばん上の棚に入る大きさ)
        Vector3 PrizeRabbitSmall(Transform p)
        {
            var g = Group(p, "Small");
            g.localScale = Vector3.one * 0.7f;
            return PrizeRabbit(g) * 0.7f;
        }

        Vector3 PrizePenguin(Transform p)
        {
            var navy = PrizeMat("Penguin", new Color(0.1f, 0.12f, 0.25f), 0.2f);
            var white = PrizeMat("Penguin_Belly", new Color(0.97f, 0.97f, 0.97f), 0.2f);
            var orange = PrizeMat("Penguin_Beak", new Color(1f, 0.6f, 0.1f));
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.17f, 0f), new Vector3(0.2f, 0.34f, 0.18f), navy);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.15f, -0.05f), new Vector3(0.15f, 0.26f, 0.1f), white);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.25f, -0.095f), new Vector3(0.04f, 0.025f, 0.05f), orange);
            Eyes(p, 0.28f, -0.08f, 0.035f, 0.02f);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(PrimitiveType.Sphere, p, new Vector3(s * 0.1f, 0.15f, 0f), new Vector3(0.03f, 0.15f, 0.07f), navy, new Vector3(0f, 0f, s * 15f));
                Deco(PrimitiveType.Sphere, p, new Vector3(s * 0.04f, 0.01f, -0.04f), new Vector3(0.06f, 0.025f, 0.08f), orange);
            }
            return new Vector3(0.22f, 0.36f, 0.19f);
        }

        Vector3 PrizeDino(Transform p)
        {
            var green = PrizeMat("Dino", new Color(0.3f, 0.75f, 0.3f), 0.2f);
            var yellow = PrizeMat("Dino_Belly", new Color(0.95f, 0.9f, 0.45f), 0.2f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.12f, 0f), new Vector3(0.18f, 0.2f, 0.22f), green);
            Deco(PrimitiveType.Capsule, p, new Vector3(0f, 0.25f, -0.06f), new Vector3(0.08f, 0.1f, 0.08f), green, new Vector3(-20f, 0f, 0f));
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.35f, -0.1f), new Vector3(0.12f, 0.1f, 0.14f), green);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.1f, -0.06f), new Vector3(0.12f, 0.15f, 0.12f), yellow);
            Eyes(p, 0.37f, -0.155f, 0.035f, 0.02f);
            Deco(PrimitiveType.Capsule, p, new Vector3(0f, 0.08f, 0.15f), new Vector3(0.07f, 0.1f, 0.07f), green, new Vector3(70f, 0f, 0f));
            for (int i = 0; i < 4; i++)
                Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.23f - i * 0.02f + (i == 0 ? 0.1f : 0f), 0.02f + i * 0.05f - (i == 0 ? 0.1f : 0f)), new Vector3(0.015f, 0.05f, 0.04f), yellow, new Vector3(45f, 0f, 0f));
            foreach (float s in new[] { -1f, 1f })
                Deco(PrimitiveType.Sphere, p, new Vector3(s * 0.07f, 0.03f, 0.02f), new Vector3(0.06f, 0.06f, 0.08f), green);
            return new Vector3(0.2f, 0.42f, 0.36f);
        }

        Vector3 PrizeTruck(Transform p)
        {
            var blue = PrizeMat("Truck_Blue", new Color(0.15f, 0.4f, 0.9f), 0.6f);
            var yellow = PrizeMat("Truck_Yellow", new Color(1f, 0.8f, 0.1f), 0.5f);
            var glass = PrizeMat("Toy_Glass", new Color(0.55f, 0.8f, 0.95f), 0.9f);
            Deco(PrimitiveType.Cube, p, new Vector3(-0.09f, 0.1f, 0f), new Vector3(0.1f, 0.12f, 0.14f), blue);
            Deco(PrimitiveType.Cube, p, new Vector3(-0.142f, 0.12f, 0f), new Vector3(0.005f, 0.06f, 0.11f), glass);
            Deco(PrimitiveType.Cube, p, new Vector3(0.07f, 0.12f, 0f), new Vector3(0.22f, 0.15f, 0.15f), yellow);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.04f, 0f), new Vector3(0.32f, 0.03f, 0.13f), Black);
            foreach (float x in new[] { -0.1f, 0.12f })
                foreach (float s in new[] { -1f, 1f })
                    Deco(PrimitiveType.Cylinder, p, new Vector3(x, 0.035f, s * 0.07f), new Vector3(0.07f, 0.012f, 0.07f), Black, new Vector3(90f, 0f, 0f));
            return new Vector3(0.34f, 0.22f, 0.16f);
        }

        Vector3 PrizeChips(Transform p)
        {
            var bag = PrizeMat("Chips_Bag", new Color(0.95f, 0.15f, 0.12f), 0.7f);
            var label = PrizeMat("Chips_Label", new Color(1f, 0.85f, 0.2f), 0.5f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.15f, 0f), new Vector3(0.2f, 0.28f, 0.08f), bag);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.29f, 0f), new Vector3(0.18f, 0.03f, 0.02f), bag);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.02f, 0f), new Vector3(0.18f, 0.03f, 0.02f), bag);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.15f, -0.035f), new Vector3(0.12f, 0.1f, 0.02f), label);
            return new Vector3(0.2f, 0.31f, 0.09f);
        }

        Vector3 PrizeRobotBox(Transform p)
        {
            var box = PrizeMat("RobotBox", new Color(0.2f, 0.55f, 0.95f), 0.4f);
            var window = PrizeMat("RobotBox_Window", new Color(0.8f, 0.92f, 1f), 0.9f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.14f, 0f), new Vector3(0.18f, 0.28f, 0.1f), box);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.15f, -0.051f), new Vector3(0.12f, 0.17f, 0.002f), window);
            var silver = PrizeMat("Robot_Silver", new Color(0.75f, 0.77f, 0.8f), 0.8f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.12f, -0.04f), new Vector3(0.05f, 0.07f, 0.02f), silver);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.18f, -0.04f), new Vector3(0.04f, 0.04f, 0.02f), silver);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.255f, -0.051f), new Vector3(0.16f, 0.03f, 0.003f), PrizeMat("Ribbon_Red", new Color(0.9f, 0.1f, 0.15f)));
            return new Vector3(0.18f, 0.28f, 0.1f);
        }

        Vector3 PrizeCar(Transform p)
        {
            var red = PrizeMat("Car_Red", new Color(0.92f, 0.1f, 0.1f), 0.8f);
            var glass = PrizeMat("Toy_Glass", new Color(0.55f, 0.8f, 0.95f), 0.9f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.045f, 0f), new Vector3(0.22f, 0.05f, 0.1f), red);
            Deco(PrimitiveType.Cube, p, new Vector3(-0.01f, 0.085f, 0f), new Vector3(0.1f, 0.045f, 0.085f), glass);
            Deco(PrimitiveType.Cube, p, new Vector3(-0.01f, 0.11f, 0f), new Vector3(0.1f, 0.008f, 0.088f), red);
            foreach (float x in new[] { -0.07f, 0.07f })
                foreach (float s in new[] { -1f, 1f })
                    Deco(PrimitiveType.Cylinder, p, new Vector3(x, 0.025f, s * 0.05f), new Vector3(0.05f, 0.01f, 0.05f), Black, new Vector3(90f, 0f, 0f));
            return new Vector3(0.22f, 0.12f, 0.11f);
        }

        Vector3 PrizeCaramel(Transform p)
        {
            var yellow = PrizeMat("Caramel_Box", new Color(1f, 0.85f, 0.2f), 0.4f);
            var red = PrizeMat("Caramel_Label", new Color(0.85f, 0.1f, 0.1f), 0.4f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.08f, 0f), new Vector3(0.09f, 0.16f, 0.05f), yellow);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.1f, -0.026f), new Vector3(0.08f, 0.05f, 0.002f), red);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.05f, -0.026f), new Vector3(0.035f, 0.035f, 0.003f), PrizeMat("Caramel_Brown", new Color(0.6f, 0.35f, 0.1f)));
            return new Vector3(0.1f, 0.16f, 0.06f);
        }

        Vector3 PrizeDaruma(Transform p)
        {
            var red = PrizeMat("Daruma", new Color(0.9f, 0.1f, 0.08f), 0.7f);
            var white = PrizeMat("Daruma_Face", new Color(0.98f, 0.95f, 0.88f), 0.5f);
            var gold = PrizeMat("Daruma_Gold", new Color(1f, 0.8f, 0.3f), 0.8f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.09f, 0f), new Vector3(0.15f, 0.18f, 0.14f), red);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.12f, -0.045f), new Vector3(0.09f, 0.07f, 0.05f), white);
            Eyes(p, 0.125f, -0.07f, 0.02f, 0.018f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.05f, -0.065f), new Vector3(0.06f, 0.02f, 0.01f), gold);
            return new Vector3(0.15f, 0.18f, 0.14f);
        }

        Vector3 PrizeDuck(Transform p)
        {
            var yellow = PrizeMat("Duck", new Color(1f, 0.88f, 0.15f), 0.7f);
            var orange = PrizeMat("Duck_Beak", new Color(1f, 0.5f, 0.1f), 0.6f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.05f, 0.01f), new Vector3(0.13f, 0.1f, 0.16f), yellow);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.12f, -0.04f), Vector3.one * 0.09f, yellow);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.11f, -0.09f), new Vector3(0.05f, 0.02f, 0.04f), orange);
            Eyes(p, 0.135f, -0.08f, 0.025f, 0.015f);
            return new Vector3(0.13f, 0.17f, 0.17f);
        }

        Vector3 PrizeChocolate(Transform p)
        {
            var brown = PrizeMat("Choco_Box", new Color(0.35f, 0.18f, 0.08f), 0.5f);
            var gold = PrizeMat("Choco_Ribbon", new Color(1f, 0.78f, 0.25f), 0.85f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.04f, 0f), new Vector3(0.18f, 0.08f, 0.12f), brown);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.04f, 0f), new Vector3(0.025f, 0.082f, 0.122f), gold);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.04f, 0f), new Vector3(0.182f, 0.082f, 0.025f), gold);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.09f, 0f), new Vector3(0.06f, 0.03f, 0.04f), gold);
            return new Vector3(0.18f, 0.1f, 0.12f);
        }

        Vector3 PrizeRobot(Transform p)
        {
            var silver = PrizeMat("Robot_Silver", new Color(0.75f, 0.77f, 0.8f), 0.8f);
            var red = PrizeMat("Robot_Eye", new Color(1f, 0.2f, 0.15f), 0.5f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.08f, 0f), new Vector3(0.1f, 0.1f, 0.07f), silver);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.17f, 0f), new Vector3(0.08f, 0.07f, 0.07f), silver);
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(PrimitiveType.Cube, p, new Vector3(s * 0.02f, 0.18f, -0.036f), new Vector3(0.02f, 0.015f, 0.005f), red);
                Deco(PrimitiveType.Cube, p, new Vector3(s * 0.065f, 0.08f, 0f), new Vector3(0.025f, 0.08f, 0.03f), silver);
                Deco(PrimitiveType.Cube, p, new Vector3(s * 0.03f, 0.015f, 0f), new Vector3(0.035f, 0.03f, 0.05f), silver);
            }
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.23f, 0f), new Vector3(0.008f, 0.025f, 0.008f), silver);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.26f, 0f), Vector3.one * 0.02f, red);
            return new Vector3(0.14f, 0.27f, 0.08f);
        }

        Vector3 PrizeBall(Transform p)
        {
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.08f, 0f), Vector3.one * 0.16f, PrizeMat("Ball_White", new Color(0.97f, 0.97f, 0.97f), 0.7f));
            var colors = new[] { new Color(0.95f, 0.15f, 0.12f), new Color(0.15f, 0.45f, 0.95f), new Color(1f, 0.85f, 0.15f) };
            for (int i = 0; i < 3; i++)
                Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.08f, 0f), new Vector3(0.162f, 0.018f, 0.162f), PrizeMat($"Ball_{i}", colors[i], 0.7f), new Vector3(0f, i * 60f, 90f));
            return new Vector3(0.16f, 0.16f, 0.16f);
        }

        Vector3 PrizeLuckyCat(Transform p)
        {
            var white = PrizeMat("LuckyCat", new Color(0.98f, 0.97f, 0.95f), 0.7f);
            var red = PrizeMat("LuckyCat_Red", new Color(0.9f, 0.1f, 0.12f), 0.6f);
            var gold = PrizeMat("Daruma_Gold", new Color(1f, 0.8f, 0.3f), 0.8f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.06f, 0f), new Vector3(0.1f, 0.12f, 0.09f), white);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.14f, 0f), Vector3.one * 0.09f, white);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.105f, 0f), new Vector3(0.07f, 0.006f, 0.07f), red);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.095f, -0.035f), Vector3.one * 0.018f, gold);
            Eyes(p, 0.15f, -0.04f, 0.018f, 0.012f);
            foreach (float s in new[] { -1f, 1f })
                Deco(PrimitiveType.Cube, p, new Vector3(s * 0.025f, 0.19f, 0f), new Vector3(0.025f, 0.03f, 0.015f), white, new Vector3(0f, 0f, 45f));
            Deco(PrimitiveType.Capsule, p, new Vector3(0.05f, 0.17f, -0.01f), new Vector3(0.025f, 0.035f, 0.025f), white); // 招く手
            return new Vector3(0.11f, 0.21f, 0.1f);
        }

        Vector3 PrizeKokeshi(Transform p)
        {
            var body = PrizeMat("Kokeshi_Body", new Color(0.95f, 0.85f, 0.65f), 0.5f);
            var red = PrizeMat("Kokeshi_Red", new Color(0.85f, 0.12f, 0.12f), 0.5f);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.06f, 0f), new Vector3(0.06f, 0.06f, 0.06f), body);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.07f, 0f), new Vector3(0.062f, 0.02f, 0.062f), red);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.15f, 0f), Vector3.one * 0.07f, body);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.165f, 0.008f), new Vector3(0.072f, 0.05f, 0.07f), Black);
            Eyes(p, 0.145f, -0.033f, 0.012f, 0.008f);
            return new Vector3(0.07f, 0.19f, 0.07f);
        }

        Vector3 PrizeGame(Transform p)
        {
            var black = PrizeMat("Game_Box", new Color(0.08f, 0.08f, 0.1f), 0.6f);
            var screen = Mat("Game_Screen", new Color(0.3f, 0.8f, 1f), new Color(0.3f, 0.8f, 1.2f));
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.065f, 0f), new Vector3(0.14f, 0.13f, 0.04f), black);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.08f, -0.021f), new Vector3(0.1f, 0.06f, 0.002f), screen);
            Deco(PrimitiveType.Cube, p, new Vector3(-0.04f, 0.03f, -0.021f), new Vector3(0.025f, 0.008f, 0.003f), PrizeMat("Game_Button", new Color(0.9f, 0.9f, 0.9f)));
            Deco(PrimitiveType.Sphere, p, new Vector3(0.04f, 0.03f, -0.021f), new Vector3(0.015f, 0.015f, 0.005f), PrizeMat("Robot_Eye", new Color(1f, 0.2f, 0.15f), 0.5f));
            return new Vector3(0.14f, 0.13f, 0.05f);
        }

        Vector3 PrizeRocket(Transform p)
        {
            var white = PrizeMat("Rocket", new Color(0.95f, 0.95f, 0.97f), 0.6f);
            var red = PrizeMat("Rocket_Red", new Color(0.9f, 0.12f, 0.1f), 0.6f);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.09f, 0f), new Vector3(0.05f, 0.07f, 0.05f), white);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.17f, 0f), new Vector3(0.05f, 0.07f, 0.05f), red);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.11f, -0.024f), new Vector3(0.02f, 0.02f, 0.005f), PrizeMat("Toy_Glass", new Color(0.55f, 0.8f, 0.95f), 0.9f));
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Deco(PrimitiveType.Cube, p, new Vector3(Mathf.Sin(a) * 0.03f, 0.03f, Mathf.Cos(a) * 0.03f), new Vector3(0.005f, 0.05f, 0.03f), red, new Vector3(0f, i * 120f, 0f));
            }
            return new Vector3(0.08f, 0.21f, 0.08f);
        }

        Vector3 PrizeTop(Transform p)
        {
            var colors = new[] { new Color(0.95f, 0.2f, 0.15f), new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.5f, 0.95f) };
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.012f, 0f), new Vector3(0.02f, 0.012f, 0.02f), PrizeMat("Top_Wood", new Color(0.6f, 0.4f, 0.2f)));
            for (int i = 0; i < 3; i++)
            {
                float d = 0.06f + i * 0.025f;
                Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.03f + i * 0.012f, 0f), new Vector3(d, 0.006f, d), PrizeMat($"Top_{i}", colors[i], 0.6f));
            }
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.09f, 0f), new Vector3(0.016f, 0.03f, 0.016f), PrizeMat("Top_Wood", new Color(0.6f, 0.4f, 0.2f)));
            return new Vector3(0.12f, 0.12f, 0.12f);
        }

        Vector3 PrizePiggy(Transform p)
        {
            var pink = PrizeMat("Piggy", new Color(1f, 0.6f, 0.7f), 0.7f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.06f, 0f), new Vector3(0.11f, 0.1f, 0.13f), pink);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.065f, -0.065f), new Vector3(0.04f, 0.01f, 0.035f), pink, new Vector3(90f, 0f, 0f));
            Eyes(p, 0.085f, -0.05f, 0.025f, 0.012f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.11f, 0.01f), new Vector3(0.03f, 0.004f, 0.008f), Black); // お金を入れる口
            foreach (float s in new[] { -1f, 1f })
            {
                Deco(PrimitiveType.Cube, p, new Vector3(s * 0.03f, 0.11f, -0.035f), new Vector3(0.025f, 0.025f, 0.008f), pink, new Vector3(0f, 0f, 45f));
                Deco(PrimitiveType.Cylinder, p, new Vector3(s * 0.03f, 0.012f, -0.03f), new Vector3(0.025f, 0.012f, 0.025f), pink);
                Deco(PrimitiveType.Cylinder, p, new Vector3(s * 0.03f, 0.012f, 0.035f), new Vector3(0.025f, 0.012f, 0.025f), pink);
            }
            return new Vector3(0.12f, 0.13f, 0.14f);
        }

        Vector3 PrizeKendama(Transform p)
        {
            var wood = PrizeMat("Kendama_Wood", new Color(0.85f, 0.65f, 0.4f), 0.5f);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.06f, 0f), new Vector3(0.025f, 0.06f, 0.025f), wood);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.12f, 0f), new Vector3(0.08f, 0.015f, 0.04f), wood, new Vector3(0f, 0f, 0f));
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.17f, 0f), Vector3.one * 0.06f, PrizeMat("Kendama_Ball", new Color(0.9f, 0.1f, 0.12f), 0.85f));
            return new Vector3(0.08f, 0.2f, 0.06f);
        }

        // ------------------------------------------------------------------
        // 輪投げ: 40° 傾けた板に5段の杭 (板に垂直)。奥ほど高い点数。屋根は手前だけ
        // ------------------------------------------------------------------

        Stall BuildRingTossStall(Transform root)
        {
            var t = FestivalStallRoot(root, "Festival_RingToss", 1);
            var cloth = Mat("Festival_Cloth_Blue", new Color(0.15f, 0.35f, 0.8f));
            const float BackZ = 2.7f; // 板を傾けたので奥行きは短い
            FestivalStallFrame(t, cloth, "RING TOSS", BackZ, false, "RING THE PEGS = EARN MONEY");
            var wood = Mat("Festival_Wood", new Color(0.5f, 0.32f, 0.18f), null, false, 0.3f);
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));
            // 手前の低い柵 (台がよく見えるように低く)
            Prim(PrimitiveType.Cube, "FrontRail", t, new Vector3(0f, 0.55f, FestivalFrontZ + 0.1f), new Vector3(FestivalStallWidth - 0.4f, 0.08f, 0.08f), wood);
            Prim(PrimitiveType.Cube, "FrontPanel", t, new Vector3(0f, 0.26f, FestivalFrontZ + 0.1f), new Vector3(FestivalStallWidth - 0.4f, 0.5f, 0.04f), cloth);
            // 奥の壁と、飾りの大きなぬいぐるみ
            Prim(PrimitiveType.Cube, "BackWall", t, new Vector3(0f, 1.6f, BackZ), new Vector3(FestivalStallWidth, 3.2f, 0.06f), white);
            Prim(PrimitiveType.Cube, "PrizeShelf", t, new Vector3(0f, 2.45f, BackZ - 0.2f), new Vector3(FestivalStallWidth - 0.6f, 0.06f, 0.35f), wood, false);
            for (int i = 0; i < 4; i++)
            {
                var deco = Group(t, "PrizeDisplay");
                deco.localPosition = new Vector3(-2.1f + i * 1.4f, 2.48f, BackZ - 0.2f); // 傾いた板の上からのぞく高さ
                deco.localScale = Vector3.one * 1.8f;
                if (i % 2 == 0) PrizeBear(deco);
                else PrizeRabbit(deco);
            }

            int[] rowPoints = { 50, 100, 200, 300, 500 };
            var felts = new[] { Mat("Festival_Felt_Green", new Color(0.12f, 0.45f, 0.22f)), Mat("Festival_Felt_DarkGreen", new Color(0.08f, 0.32f, 0.16f)) };
            var tops = new[]
            {
                Mat("Peg_White", new Color(0.95f, 0.95f, 0.95f)),
                Mat("Peg_Blue", new Color(0.2f, 0.45f, 1f), new Color(0.05f, 0.1f, 0.3f)),
                Mat("Peg_Green", new Color(0.3f, 0.95f, 0.4f), new Color(0.06f, 0.25f, 0.08f)),
                Mat("Peg_Red", new Color(1f, 0.25f, 0.2f), new Color(0.35f, 0.05f, 0.03f)),
                Mat("Peg_Gold", new Color(1f, 0.82f, 0.25f), new Color(1.2f, 0.85f, 0.15f)),
            };
            // 杭を立てた板ごと、奥を持ち上げてプレイヤーの方へ傾ける (金魚すくいの水槽と同じ 40°)。
            // 手前の下のふちを軸に BoardTilt 度。板の中 (Board) は、板の面が y=0・奥へ +z の座標で作る。杭は板に垂直に立つ
            const float BoardTilt = 40f, BoardLift = 0.1f, BoardFrontZ = -0.45f, RowLen = 0.66f;
            float boardLen = RowLen * rowPoints.Length, boardW = FestivalStallWidth - 0.6f;
            var board = Group(t, "Board");
            board.localPosition = new Vector3(0f, BoardLift, BoardFrontZ);
            board.localRotation = Quaternion.Euler(-BoardTilt, 0f, 0f);
            var targets = new List<Target>();
            for (int r = 0; r < rowPoints.Length; r++)
            {
                float z = (r + 0.5f) * RowLen;
                Prim(PrimitiveType.Cube, $"Step_{r}", board, new Vector3(0f, -0.04f, z), new Vector3(boardW, 0.08f, RowLen), felts[r % 2]);
                // 段の手前のふちに点数 (板の上に寝かせて書く)
                MakeText(board, "RowPoints", rowPoints[r].ToString(), new Vector3(-boardW / 2f + 0.35f, 0.005f, z - RowLen * 0.25f), 0.14f, new Color(1f, 0.95f, 0.6f))
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                int pegs = r == rowPoints.Length - 1 ? 3 : 5; // いちばん奥は3本だけ
                for (int c = 0; c < pegs; c++)
                {
                    float x = (c - (pegs - 1) / 2f) * (pegs == 5 ? 1.1f : 1.5f) + (r % 2 == 1 ? 0.3f : 0f);
                    targets.Add(Peg(board, new Vector3(x, 0f, z), rowPoints[r], tops[r], wood));
                }
            }
            // 傾いた板を下から支える台 (持ち上がった奥のふちの下の板と、すみの脚)
            var stand = Mat("Festival_WoodDark", new Color(0.3f, 0.2f, 0.12f));
            float standY = BoardLift + boardLen * Mathf.Sin(BoardTilt * Mathf.Deg2Rad), standZ = BoardFrontZ + boardLen * Mathf.Cos(BoardTilt * Mathf.Deg2Rad);
            Prim(PrimitiveType.Cube, "BoardStandBack", t, new Vector3(0f, standY / 2f, standZ + 0.04f), new Vector3(boardW - 0.2f, standY, 0.08f), stand);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "BoardStandLeg", t, new Vector3(s * (boardW / 2f - 0.1f), standY / 2f, standZ + 0.04f), new Vector3(0.1f, standY, 0.1f), stand);
            // 台の上に張った提灯の列 (屋根がない所の明かり)
            foreach (float x in new[] { -1.6f, 1.6f })
            {
                Vector3 a = new Vector3(x, 3.1f, FestivalFrontZ + 1.4f), b = new Vector3(x, 3.1f, BackZ);
                Rod("LanternLine", t, a, b, 0.012f, Mat("Chochin_Cap", new Color(0.08f, 0.06f, 0.05f)));
                for (int i = 0; i < 5; i++)
                    Chochin(t, Vector3.Lerp(a, b, (i + 0.5f) / 5f) + Vector3.down * 0.25f, 0.22f, i % 2 == 0);
            }
            return FinishFestivalStall(t, targets);
        }

        // 輪投げの杭 1本 (的)。輪がかかっても倒れない (かかるたびに点数。少し揺れる)
        Target Peg(Transform parent, Vector3 pos, int points, Material top, Material wood)
        {
            var root = Group(parent, $"Peg_{points}");
            root.localPosition = pos;
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.FestivalRing;
            target.points = points;
            target.stayUpOnHit = true;
            target.multiHitInterval = 0.25f;
            target.hitWobbleDeg = 8f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            Prim(PrimitiveType.Cylinder, "PegBase", pivot, new Vector3(0f, 0.01f, 0f), new Vector3(0.2f, 0.01f, 0.2f), Mat("Festival_WoodDark", new Color(0.3f, 0.2f, 0.12f)), false);
            Prim(PrimitiveType.Cylinder, "Stake", pivot, new Vector3(0f, 0.2f, 0f), new Vector3(0.07f, 0.2f, 0.07f), wood);
            Prim(PrimitiveType.Sphere, "PegTop", pivot, new Vector3(0f, 0.42f, 0f), Vector3.one * 0.11f, top, false);
            return target;
        }

        // ------------------------------------------------------------------
        // 金魚すくい: 大きな水槽で、大きさも色もいろいろな金魚が泳ぐ。いちばん高いのは金色に光る金魚
        // ------------------------------------------------------------------

        Stall BuildGoldfishStall(Transform root, out Collider water, out FestivalFish[] fish, out GameObject scoopPoi, out Transform bowl)
        {
            var t = FestivalStallRoot(root, "Festival_Goldfish", 2);
            var cloth = Mat("Festival_Cloth_Aqua", new Color(0.1f, 0.55f, 0.75f));
            FestivalStallFrame(t, cloth, "KINGYO SUKUI", 1.9f, true, "SCOOP GOLDFISH = EARN MONEY");
            Prim(PrimitiveType.Cube, "BackWall", t, new Vector3(0f, 1.6f, 1.9f), new Vector3(FestivalStallWidth, 3.2f, 0.06f), Mat("Festival_Wall_Sky", new Color(0.75f, 0.9f, 1f)));

            // 水槽 (青いプラスチックの箱)。真ん中が (0, 0, 0.25)、外の大きさ 5.8 × 0.72 × 2.3
            var blue = Mat("Tank_Blue", new Color(0.15f, 0.4f, 0.85f), null, false, 0.6f);
            var inner = Mat("Tank_Inner", new Color(0.55f, 0.8f, 0.95f), null, false, 0.4f);
            var rim = Mat("Tank_Rim", new Color(0.9f, 0.95f, 1f), null, false, 0.6f);
            const float TW = 5.8f, TD = 2.3f, TH = 0.72f, TZ = 0.25f;
            // 水槽ごと、奥を持ち上げてプレイヤーの方へ傾ける (本当なら水がこぼれるが、水槽の中が全部見えるように)。
            // 手前の下のふちを軸に TankTilt 度。中身は TankBody の中に、傾ける前の座標で作る
            const float TankTilt = 40f, TankLift = 0.1f;
            var tank = Group(t, "Tank");
            tank.localPosition = new Vector3(0f, TankLift, TZ - TD / 2f);
            tank.localRotation = Quaternion.Euler(-TankTilt, 0f, 0f);
            var body = Group(tank, "TankBody");
            body.localPosition = new Vector3(0f, 0f, -(TZ - TD / 2f));
            Prim(PrimitiveType.Cube, "TankBottom", body, new Vector3(0f, 0.04f, TZ), new Vector3(TW, 0.08f, TD), inner);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "TankWallZ", body, new Vector3(0f, TH / 2f, TZ + s * TD / 2f), new Vector3(TW, TH, 0.06f), blue);
                Prim(PrimitiveType.Cube, "TankWallX", body, new Vector3(s * TW / 2f, TH / 2f, TZ), new Vector3(0.06f, TH, TD), blue);
                Prim(PrimitiveType.Cube, "TankRimZ", body, new Vector3(0f, TH + 0.02f, TZ + s * TD / 2f), new Vector3(TW + 0.1f, 0.04f, 0.12f), rim, false);
                Prim(PrimitiveType.Cube, "TankRimX", body, new Vector3(s * TW / 2f, TH + 0.02f, TZ), new Vector3(0.12f, 0.04f, TD + 0.1f), rim, false);
            }
            // 傾いた水槽を下から支える台 (持ち上がった奥のふちの下の板と、すみの脚)
            var stand = Mat("Festival_WoodDark", new Color(0.3f, 0.2f, 0.12f));
            float standY = TankLift + TD * Mathf.Sin(TankTilt * Mathf.Deg2Rad), standZ = TZ - TD / 2f + TD * Mathf.Cos(TankTilt * Mathf.Deg2Rad);
            Prim(PrimitiveType.Cube, "TankStandBack", t, new Vector3(0f, standY / 2f, standZ - 0.05f), new Vector3(TW - 0.2f, standY, 0.08f), stand);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "TankStandLeg", t, new Vector3(s * (TW / 2f - 0.1f), standY / 2f, standZ - 0.05f), new Vector3(0.1f, standY, 0.1f), stand);
            // 水 (透ける青。上の面が水面)。当たり判定は水面の薄い板だけ (ポイが入ったかを見る)
            const float WaterTop = 0.64f;
            Prim(PrimitiveType.Cube, "Water", body, new Vector3(0f, (WaterTop + 0.08f) / 2f, TZ), new Vector3(TW - 0.08f, WaterTop - 0.08f, TD - 0.08f), TransparentMat("Festival_Water", new Color(0.3f, 0.6f, 0.95f, 0.3f)), false);
            var surface = new GameObject("WaterSurface");
            surface.transform.SetParent(body, false);
            surface.transform.localPosition = new Vector3(0f, WaterTop - 0.01f, TZ);
            var box = surface.AddComponent<BoxCollider>();
            box.size = new Vector3(TW - 0.08f, 0.02f, TD - 0.08f);
            water = box;
            // 水面に浮かぶ泡 (飾り)
            var bubble = TransparentMat("Festival_Bubble", new Color(0.9f, 0.97f, 1f, 0.45f));
            for (int i = 0; i < 14; i++)
                Prim(PrimitiveType.Sphere, "Bubble", body, new Vector3(Random.Range(-2.6f, 2.6f), WaterTop + 0.005f, TZ + Random.Range(-1f, 1f)), new Vector3(0.05f, 0.01f, 0.05f) * Random.Range(0.6f, 1.4f), bubble, false);

            // 金魚 (水槽の真ん中を原点に、傾いた水槽の中を泳ぐ)
            var fishRoot = Group(body, "Fish");
            fishRoot.localPosition = new Vector3(0f, WaterTop, TZ);
            var list = new List<FestivalFish>();
            var red = Mat("Goldfish_Red", new Color(1f, 0.32f, 0.1f), new Color(0.15f, 0.03f, 0f), false, 0.6f);
            var redFin = Mat("Goldfish_RedFin", new Color(1f, 0.5f, 0.25f), null, false, 0.4f);
            var black = Mat("Goldfish_Black", new Color(0.06f, 0.05f, 0.06f), null, false, 0.6f);
            var white = Mat("Goldfish_White", new Color(1f, 0.97f, 0.94f), null, false, 0.6f);
            var orange = Mat("Goldfish_Orange", new Color(1f, 0.55f, 0.12f), new Color(0.12f, 0.05f, 0f), false, 0.6f);
            var gold = Mat("Goldfish_Gold", new Color(1f, 0.82f, 0.25f), new Color(1.6f, 1.1f, 0.2f), false, 0.9f);
            for (int i = 0; i < 9; i++) list.Add(Goldfish(fishRoot, $"Red{i}", 50, 0.13f, red, redFin, null, false, false, 0.42f));
            for (int i = 0; i < 4; i++) list.Add(Goldfish(fishRoot, $"Demekin{i}", 100, 0.15f, black, black, null, true, false, 0.32f));
            for (int i = 0; i < 3; i++) list.Add(Goldfish(fishRoot, $"Sarasa{i}", 200, 0.18f, white, redFin, red, false, false, 0.34f));
            for (int i = 0; i < 2; i++) list.Add(Goldfish(fishRoot, $"Calico{i}", 300, 0.21f, white, white, black, false, false, 0.3f, red));
            for (int i = 0; i < 2; i++) list.Add(Goldfish(fishRoot, $"Ranchu{i}", 500, 0.27f, orange, orange, null, false, false, 0.24f));
            list.Add(Goldfish(fishRoot, "Golden", 1000, 0.3f, gold, gold, null, false, true, 0.36f));
            fish = list.ToArray();

            // 奥の棚: すくった金魚を入れる袋 (飾り)
            Prim(PrimitiveType.Cube, "BagBar", t, new Vector3(0f, 2.1f, 1.7f), new Vector3(FestivalStallWidth - 0.8f, 0.04f, 0.04f), Mat("Festival_Wood", new Color(0.5f, 0.32f, 0.18f), null, false, 0.3f), false);
            var bag = TransparentMat("Goldfish_Bag", new Color(0.85f, 0.95f, 1f, 0.35f));
            for (int i = 0; i < 7; i++)
            {
                var p = new Vector3(-2.4f + i * 0.8f, 1.85f, 1.7f);
                Prim(PrimitiveType.Sphere, "Bag", t, p, new Vector3(0.2f, 0.26f, 0.2f), bag, false);
                Prim(PrimitiveType.Cylinder, "BagTie", t, p + Vector3.up * 0.17f, new Vector3(0.04f, 0.04f, 0.04f), Mat("Ribbon_Red", new Color(0.9f, 0.1f, 0.15f)), false);
                Prim(PrimitiveType.Sphere, "BagFish", t, p + new Vector3(0.02f, -0.03f, 0f), new Vector3(0.05f, 0.035f, 0.08f), i % 3 == 1 ? black : red, false)
                    .transform.localRotation = Quaternion.Euler(0f, 90f + i * 30f, 0f);
            }
            // すくった金魚を入れるお椀 (水槽の右手前の台の上)。白いお椀に水、赤いふち
            var stool = Group(t, "BowlStool");
            stool.localPosition = new Vector3(2.3f, 0f, -1.7f);
            Prim(PrimitiveType.Cylinder, "StoolTop", stool, new Vector3(0f, 0.68f, 0f), new Vector3(0.55f, 0.03f, 0.55f), Mat("Festival_Wood", new Color(0.5f, 0.32f, 0.18f), null, false, 0.3f), false);
            Prim(PrimitiveType.Cylinder, "StoolLeg", stool, new Vector3(0f, 0.34f, 0f), new Vector3(0.08f, 0.34f, 0.08f), Mat("Festival_WoodDark", new Color(0.3f, 0.2f, 0.12f)), false);
            Prim(PrimitiveType.Cylinder, "Bowl", stool, new Vector3(0f, 0.77f, 0f), new Vector3(0.44f, 0.07f, 0.44f), Mat("Bowl_White", new Color(0.97f, 0.97f, 0.97f), null, false, 0.7f), false);
            Prim(PrimitiveType.Cylinder, "BowlRim", stool, new Vector3(0f, 0.84f, 0f), new Vector3(0.46f, 0.01f, 0.46f), Mat("Ribbon_Red", new Color(0.9f, 0.1f, 0.15f)), false);
            Prim(PrimitiveType.Cylinder, "BowlWater", stool, new Vector3(0f, 0.835f, 0f), new Vector3(0.4f, 0.004f, 0.4f), TransparentMat("Festival_Water", new Color(0.3f, 0.6f, 0.95f, 0.3f)), false);
            bowl = Group(stool, "BowlInside");
            bowl.localPosition = new Vector3(0f, 0.815f, 0f);

            // すくう時に出るポイ (FestivalStage がこれを複製して、金魚を乗せて動かす)。紙が破れた時は Torn に入れ替わる
            var poi = Group(t, "ScoopPoi");
            poi.localScale = Vector3.one * 1.6f;
            var poiFrame = Mat("Poi_Frame", new Color(0.95f, 0.4f, 0.6f), null, false, 0.5f);
            var poiPaper = Mat("Poi_Paper", new Color(0.97f, 0.97f, 0.94f));
            Prim(PrimitiveType.Cylinder, "Frame", poi, Vector3.zero, new Vector3(0.12f, 0.004f, 0.12f), poiFrame, false);
            Prim(PrimitiveType.Cube, "Handle", poi, new Vector3(0f, 0.002f, -0.11f), new Vector3(0.018f, 0.008f, 0.11f), poiFrame, false);
            Prim(PrimitiveType.Cylinder, "Paper", poi, new Vector3(0f, 0.003f, 0f), new Vector3(0.105f, 0.002f, 0.105f), poiPaper, false);
            var torn = Group(poi, "Torn");
            foreach (var (x, z, sx) in new[] { (-0.03f, 0.025f, 0.05f), (0.035f, -0.03f, 0.04f) })
                Prim(PrimitiveType.Cube, "TornPiece", torn, new Vector3(x, 0.003f, z), new Vector3(sx, 0.002f, 0.035f), poiPaper, false)
                    .transform.localRotation = Quaternion.Euler(0f, x * 900f, 0f);
            torn.gameObject.SetActive(false);
            poi.gameObject.SetActive(false);
            scoopPoi = poi.gameObject;

            return FinishFestivalStall(t, new List<Target>(System.Array.ConvertAll(fish, f => f.target)));
        }

        // 金魚 1匹 (的)。len = 体の長さ。patch = 体のまだら模様の色 (null なら無地)。demekin = 目が飛び出している。golden = 金色に光ってキラッと光る
        FestivalFish Goldfish(Transform parent, string name, int points, float len, Material body, Material fin, Material patch, bool demekin, bool golden, float speed, Material patch2 = null)
        {
            var root = Group(parent, $"Goldfish_{name}");
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.FestivalFish;
            target.points = points;
            target.shrinkOnHit = true; // すくわれると、その場で縮んで消える
            target.respawnDelay = golden ? 6f : 2.5f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            bool fat = points >= 500; // らんちゅう: 丸くて背びれがない
            Prim(PrimitiveType.Sphere, "Body", pivot, Vector3.zero, new Vector3(len * (fat ? 0.6f : 0.45f), len * (fat ? 0.5f : 0.38f), len), body, false);
            if (patch != null) Prim(PrimitiveType.Sphere, "Patch", pivot, new Vector3(len * 0.06f, len * 0.08f, len * 0.08f), new Vector3(len * 0.38f, len * 0.3f, len * 0.5f), patch, false);
            if (patch2 != null) Prim(PrimitiveType.Sphere, "Patch2", pivot, new Vector3(-len * 0.07f, len * 0.07f, -len * 0.15f), new Vector3(len * 0.35f, len * 0.28f, len * 0.4f), patch2, false);
            var eye = Mat("Goldfish_Eye", new Color(0.02f, 0.02f, 0.02f), null, false, 0.9f);
            foreach (float s in new[] { -1f, 1f })
            {
                float ex = demekin ? len * 0.26f : len * 0.17f;
                Prim(PrimitiveType.Sphere, "Eye", pivot, new Vector3(s * ex, len * 0.08f, len * 0.3f), Vector3.one * len * (demekin ? 0.18f : 0.08f), eye, false);
                Prim(PrimitiveType.Cube, "SideFin", pivot, new Vector3(s * len * 0.22f, -len * 0.08f, len * 0.12f), new Vector3(len * 0.18f, 0.004f, len * 0.12f), fin, false)
                    .transform.localRotation = Quaternion.Euler(0f, s * 25f, s * -20f);
            }
            if (!fat) Prim(PrimitiveType.Cube, "DorsalFin", pivot, new Vector3(0f, len * 0.2f, -len * 0.05f), new Vector3(0.004f, len * 0.18f, len * 0.35f), fin, false);
            // ひらひらの尾びれ (FestivalFish が左右に振る)
            var tail = Group(pivot, "Tail");
            tail.localPosition = new Vector3(0f, 0f, -len * 0.45f);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "TailFin", tail, new Vector3(s * len * 0.06f, 0f, -len * 0.2f), new Vector3(len * 0.14f, 0.004f, len * (golden || fat ? 0.55f : 0.45f)), fin, false)
                    .transform.localRotation = Quaternion.Euler(0f, s * 20f, 0f);
            if (golden) MineSparkle(pivot, new Vector3(0f, len * 0.35f, 0f), 0.22f); // キラッと光る十字
            var f = root.gameObject.AddComponent<FestivalFish>();
            f.target = target;
            f.tail = tail;
            f.halfSize = new Vector2(2.6f, 0.98f);
            f.swimY = -0.17f;
            f.speed = speed;
            return f;
        }

        // ------------------------------------------------------------------
        // 食べ物の屋台: 料理ごとのメニューの板 (料理の見本・名前・値段) が2段に並ぶ。板にお金が当たると買えて、屋台の前の机に並ぶ。
        // 板は当たっても消えない (いつでも何度でも買える)
        // ------------------------------------------------------------------

        Stall BuildFoodStall(Transform root, out Transform[] slots, out TextMesh balance)
        {
            var t = FestivalStallRoot(root, "Festival_Food", 3);
            var cloth = Mat("Festival_Cloth_Orange", new Color(0.95f, 0.5f, 0.1f));
            FestivalStallFrame(t, cloth, "YATAI FOOD", 1.6f, true, "SPEND YOUR MONEY HERE");
            var wood = Mat("Festival_Wood", new Color(0.5f, 0.32f, 0.18f), null, false, 0.3f);
            var woodDark = Mat("Festival_WoodDark", new Color(0.3f, 0.2f, 0.12f));
            var whiteCloth = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));
            Prim(PrimitiveType.Cube, "BackWall", t, new Vector3(0f, 1.6f, 1.6f), new Vector3(FestivalStallWidth, 3.2f, 0.06f), Mat("Festival_Wall_Cream", new Color(0.95f, 0.88f, 0.7f)));
            const float CounterZ = FestivalFrontZ + 0.25f, CounterTop = 0.9f;
            Prim(PrimitiveType.Cube, "Counter", t, new Vector3(0f, CounterTop / 2f, CounterZ), new Vector3(FestivalStallWidth - 0.4f, CounterTop, 0.5f), wood);
            Prim(PrimitiveType.Cube, "CounterCloth", t, new Vector3(0f, CounterTop + 0.01f, CounterZ), new Vector3(FestivalStallWidth - 0.3f, 0.02f, 0.55f), whiteCloth, false);

            BuildFoodKitchen(t);
            balance = BalancePanel(t, new Vector3(-2.1f, CounterTop + 0.02f, CounterZ - 0.2f));

            // メニューの板を掛ける木の枠 (横木2本と、両はしの柱)。下の段は調理場が見えるように、調理場より上に掛ける
            const float BoardZ = 0.45f;
            float[] rowY = { 1.4f, 2.26f }; // 板の下のふちの高さ
            foreach (float y in rowY)
                Prim(PrimitiveType.Cube, "MenuRail", t, new Vector3(0f, y + 0.45f, BoardZ + 0.06f), new Vector3(FestivalStallWidth - 0.4f, 0.06f, 0.04f), woodDark, false);
            foreach (float x in new[] { -(FestivalStallWidth / 2f - 0.25f), FestivalStallWidth / 2f - 0.25f })
                Prim(PrimitiveType.Cube, "MenuPost", t, new Vector3(x, 1.55f, BoardZ + 0.06f), new Vector3(0.06f, 3.1f, 0.06f), woodDark);

            // 料理 (名前, 値段, 見本)。上の段に6つ、下の段に6つ
            var foods = new (string name, int price, System.Func<Transform, Vector3> build)[]
            {
                ("COTTON_CANDY", 500, FoodCottonCandy), ("IKA_YAKI", 700, FoodIkaYaki), ("FRANKFURT", 400, FoodFrankfurt),
                ("YAKI_CORN", 400, FoodCorn), ("OKONOMIYAKI", 600, FoodOkonomiyaki), ("BABY_CASTELLA", 500, FoodCastella),
                ("RINGO_AME", 300, FoodRingoAme), ("TAKOYAKI", 500, FoodTakoyaki), ("YAKISOBA", 600, FoodYakisoba),
                ("KAKIGORI", 400, FoodKakigori), ("CHOCO_BANANA", 300, FoodChocoBanana), ("RAMUNE", 200, FoodRamune),
            };
            var boardColors = new[]
            {
                new Color(0.8f, 0.12f, 0.12f), new Color(0.95f, 0.5f, 0.1f), new Color(0.15f, 0.4f, 0.8f),
                new Color(0.15f, 0.55f, 0.3f), new Color(0.85f, 0.3f, 0.55f), new Color(0.55f, 0.3f, 0.12f),
            };
            var targets = new List<Target>();
            for (int i = 0; i < foods.Length; i++)
            {
                var (name, price, build) = foods[i];
                int k = i % 6;
                float x = -2.4f + k * 0.96f;
                float y = i < 6 ? rowY[1] : rowY[0];
                var board = Mat($"Menu_Board_{k}", boardColors[k], null, false, 0.4f);
                targets.Add(MenuBoard(t, name, price, new Vector3(x, y, BoardZ), board, build));
            }

            // 屋台の前の机 (買った物がここに並ぶ)。当たり判定なし (投げたお金をさえぎらないように)
            var table = Group(t, "Table");
            table.localPosition = new Vector3(0f, 0f, FestivalFrontZ - 0.75f);
            var tableCloth = Mat("Festival_TableCloth", new Color(0.85f, 0.15f, 0.15f));
            Prim(PrimitiveType.Cube, "TableTop", table, new Vector3(0f, 0.72f, 0f), new Vector3(4.6f, 0.05f, 0.6f), wood, false);
            Prim(PrimitiveType.Cube, "TableCloth", table, new Vector3(0f, 0.75f, 0f), new Vector3(4.4f, 0.01f, 0.5f), tableCloth, false);
            foreach (float x in new[] { -2.2f, 2.2f })
                foreach (float z in new[] { -0.25f, 0.25f })
                    Prim(PrimitiveType.Cylinder, "TableLeg", table, new Vector3(x, 0.35f, z), new Vector3(0.05f, 0.35f, 0.05f), woodDark, false);
            var slotList = new List<Transform>();
            for (int row = 0; row < 2; row++)
                for (int k = 0; k < 8; k++)
                {
                    var s = Group(table, $"Slot_{row}_{k}");
                    s.localPosition = new Vector3(-1.96f + k * 0.56f, 0.755f, row == 0 ? -0.12f : 0.14f);
                    slotList.Add(s);
                }
            slots = slotList.ToArray();
            return FinishFestivalStall(t, targets);
        }

        /// <summary>
        /// カウンターのすぐ後ろの調理場: 湯気の立つ鍋、焼きそばを焼く鉄板とヘラ、ソースの容器、たこ焼き器、綿あめの機械。
        /// メニューの板より低いので、板の下から見える
        /// </summary>
        void BuildFoodKitchen(Transform t)
        {
            var steel = Mat("Festival_Steel", new Color(0.55f, 0.56f, 0.6f), null, false, 0.75f);
            var black = Mat("Festival_Teppan", new Color(0.08f, 0.08f, 0.09f), null, false, 0.8f);
            const float Z = -0.22f, Top = 0.95f;
            var kitchen = Group(t, "Kitchen");
            Prim(PrimitiveType.Cube, "WorkTable", kitchen, new Vector3(0f, Top / 2f, Z), new Vector3(FestivalStallWidth - 0.8f, Top, 0.42f), Mat("Festival_Steel_Dark", new Color(0.3f, 0.3f, 0.33f), null, false, 0.6f), false);

            // 鍋 (湯気がもくもく)
            Prim(PrimitiveType.Cylinder, "Pot", kitchen, new Vector3(-2.2f, Top + 0.15f, Z), new Vector3(0.36f, 0.15f, 0.36f), steel, false);
            Prim(PrimitiveType.Cylinder, "PotSoup", kitchen, new Vector3(-2.2f, Top + 0.29f, Z), new Vector3(0.32f, 0.005f, 0.32f), FoodMat("Soup", new Color(0.75f, 0.55f, 0.25f), 0.7f), false);
            Prim(PrimitiveType.Cylinder, "PotLid", kitchen, new Vector3(-1.98f, Top + 0.31f, Z + 0.05f), new Vector3(0.38f, 0.01f, 0.38f), steel, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
            Steam(kitchen, new Vector3(-2.2f, Top + 0.3f, Z), 0.8f, 0.18f);

            // 鉄板と焼きそば・キャベツ・ヘラ
            Prim(PrimitiveType.Cube, "Teppan", kitchen, new Vector3(-0.3f, Top + 0.02f, Z), new Vector3(1.5f, 0.04f, 0.38f), black, false);
            var noodle = Mat("Food_Noodle", new Color(0.55f, 0.32f, 0.12f), null, false, 0.4f);
            var cabbage = FoodMat("Cabbage", new Color(0.65f, 0.85f, 0.45f));
            for (int i = 0; i < 10; i++)
                Prim(PrimitiveType.Sphere, "Noodles", kitchen, new Vector3(-0.85f + (i % 5) * 0.22f, Top + 0.06f, Z - 0.06f + (i / 5) * 0.12f), new Vector3(0.2f, 0.05f, 0.12f), noodle, false)
                    .transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
            for (int i = 0; i < 8; i++)
                Prim(PrimitiveType.Cube, "Cabbage", kitchen, new Vector3(-0.8f + i * 0.13f, Top + 0.085f, Z + (i % 2) * 0.08f - 0.04f), new Vector3(0.04f, 0.01f, 0.03f), cabbage, false)
                    .transform.localRotation = Quaternion.Euler(0f, i * 50f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                var hera = Group(kitchen, "Hera");
                hera.localPosition = new Vector3(0.1f + s * 0.12f, Top + 0.09f, Z - 0.05f);
                hera.localRotation = Quaternion.Euler(-20f, s * 15f, 0f);
                Prim(PrimitiveType.Cube, "Blade", hera, Vector3.zero, new Vector3(0.1f, 0.005f, 0.08f), steel, false);
                Prim(PrimitiveType.Cylinder, "Handle", hera, new Vector3(0f, 0.04f, -0.1f), new Vector3(0.02f, 0.06f, 0.02f), Mat("Festival_WoodDark", new Color(0.3f, 0.2f, 0.12f)), false)
                    .transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
                // へらが鉄板の上を行ったり来たり
                var move = hera.gameObject.AddComponent<Oscillator>();
                move.offset = new Vector3(0.18f, 0f, 0.03f);
                move.speed = 3f + s * 0.7f;
                move.phase = s;
            }
            Steam(kitchen, new Vector3(-0.6f, Top + 0.08f, Z), 0.7f, 0.15f);
            Steam(kitchen, new Vector3(0.05f, Top + 0.08f, Z), 0.6f, 0.13f);

            // ソースとマヨネーズの容器
            Prim(PrimitiveType.Cylinder, "SauceBottle", kitchen, new Vector3(0.65f, Top + 0.1f, Z - 0.08f), new Vector3(0.07f, 0.1f, 0.07f), FoodMat("SauceBottle", new Color(0.35f, 0.12f, 0.06f), 0.6f), false);
            Prim(PrimitiveType.Cylinder, "MayoBottle", kitchen, new Vector3(0.8f, Top + 0.1f, Z - 0.08f), new Vector3(0.07f, 0.1f, 0.07f), FoodMat("MayoBottle", new Color(1f, 0.92f, 0.4f), 0.6f), false);
            foreach (float x in new[] { 0.65f, 0.8f })
                Prim(PrimitiveType.Cylinder, "Nozzle", kitchen, new Vector3(x, Top + 0.22f, Z - 0.08f), new Vector3(0.02f, 0.025f, 0.02f), Mat("Festival_Card", new Color(0.97f, 0.96f, 0.9f)), false);

            // たこ焼き器 (穴の中で丸いたこ焼きが焼けている) とピック
            Prim(PrimitiveType.Cube, "TakoyakiPlate", kitchen, new Vector3(1.45f, Top + 0.03f, Z), new Vector3(0.62f, 0.06f, 0.36f), black, false);
            var ball = FoodMat("Takoyaki", new Color(0.6f, 0.38f, 0.15f));
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 5; c++)
                    Prim(PrimitiveType.Sphere, "TakoyakiBall", kitchen, new Vector3(1.21f + c * 0.12f, Top + 0.07f, Z - 0.11f + r * 0.11f), Vector3.one * 0.075f, ball, false);
            Prim(PrimitiveType.Cylinder, "Pick", kitchen, new Vector3(1.75f, Top + 0.12f, Z - 0.1f), new Vector3(0.008f, 0.08f, 0.008f), steel, false)
                .transform.localRotation = Quaternion.Euler(30f, 0f, 20f);
            Steam(kitchen, new Vector3(1.45f, Top + 0.08f, Z), 0.5f, 0.11f);

            // 綿あめの機械 (銀の器の中で、ピンクの綿がくるくる回る)
            Prim(PrimitiveType.Cylinder, "CandyMachine", kitchen, new Vector3(2.35f, Top + 0.1f, Z), new Vector3(0.42f, 0.1f, 0.42f), steel, false);
            Prim(PrimitiveType.Cylinder, "CandyBowl", kitchen, new Vector3(2.35f, Top + 0.24f, Z), new Vector3(0.5f, 0.05f, 0.5f), TransparentMat("Festival_Bubble", new Color(0.9f, 0.97f, 1f, 0.45f)), false);
            var fluff = Group(kitchen, "CandyFluff");
            fluff.localPosition = new Vector3(2.35f, Top + 0.24f, Z);
            var spin = fluff.gameObject.AddComponent<Rotator>();
            spin.speed = 200f;
            var pink = Mat("Food_CottonCandy", new Color(1f, 0.7f, 0.85f), new Color(0.15f, 0.05f, 0.1f), false, 0.05f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Prim(PrimitiveType.Sphere, "Fluff", fluff, new Vector3(Mathf.Sin(a) * 0.14f, 0.02f, Mathf.Cos(a) * 0.14f), new Vector3(0.12f, 0.08f, 0.12f), pink, false);
            }
        }

        // 湯気: 半透明の玉がのぼっていく (FestivalSteam が動かす)
        void Steam(Transform parent, Vector3 pos, float height, float size)
        {
            var steam = Group(parent, "Steam");
            steam.localPosition = pos;
            var mat = TransparentMat("Festival_Steam", new Color(1f, 1f, 1f, 0.22f));
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Sphere, "Puff", steam, Vector3.zero, Vector3.one * size, mat, false);
            var s = steam.gameObject.AddComponent<FestivalSteam>();
            s.height = height;
            s.size = size;
        }

        /// <summary>
        /// HUD のお金の表示 (夏祭りモードの間だけ出る。スコアなどの文字の上に、紺の板と金の縁、両側に硬貨、大きな金色の数字)。
        /// 数字は GameManager が書き換え、増えると緑、減ると赤にふくらんで光る
        /// </summary>
        void BuildMoneyPanel(Transform hud, GameManager gm)
        {
            var panel = Group(hud, "MoneyPanel");
            panel.localPosition = new Vector3(0f, 0.47f, 0f);
            const float W = 0.62f, H = 0.2f;
            // 下に夏祭りの残り時間 (減っていくバーと秒数)
            var timeRow = Group(panel, "TimeRow");
            timeRow.localPosition = new Vector3(0f, -H / 2f - 0.045f, 0f);
            Prim(PrimitiveType.Cube, "TimeBarBack", timeRow, new Vector3(-0.06f, 0f, 0f), new Vector3(0.44f, 0.035f, 0.004f), TransparentMat("Money_Board", new Color(0.03f, 0.04f, 0.15f, 0.85f)), false);
            var barPivot = Group(timeRow, "TimeBar"); // 左はしが原点 (X の大きさで長さを変える)
            barPivot.localPosition = new Vector3(-0.27f, 0f, -0.003f);
            Prim(PrimitiveType.Cube, "TimeBarFill", barPivot, new Vector3(0.21f, 0f, 0f), new Vector3(0.42f, 0.022f, 0.002f), Mat("Festival_TimeBar", Color.white, null, true), false);
            var timeText = MakeText(timeRow, "TimeText", "40s", new Vector3(0.24f, 0f, -0.006f), 0.05f, Color.white, overlay: true);
            var gold = Mat("Coin_Gold", new Color(1f, 0.8f, 0.3f), new Color(0.25f, 0.18f, 0.02f), false, 0.85f);
            Prim(PrimitiveType.Cube, "Frame", panel, new Vector3(0f, 0f, 0.006f), new Vector3(W + 0.02f, H + 0.02f, 0.004f), Mat("Money_Frame", new Color(1f, 0.8f, 0.3f), null, true), false);
            Prim(PrimitiveType.Cube, "Board", panel, Vector3.zero, new Vector3(W, H, 0.004f), TransparentMat("Money_Board", new Color(0.03f, 0.04f, 0.15f, 0.85f)), false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "Coin", panel, new Vector3(s * (W / 2f - 0.07f), 0f, -0.006f), new Vector3(0.08f, 0.004f, 0.08f), gold, false)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MakeText(panel, "Label", "MONEY", new Vector3(0f, 0.065f, -0.006f), 0.035f, new Color(0.85f, 0.9f, 1f), overlay: true);
            var text = MakeText(panel, "Amount", "¥0", new Vector3(0f, -0.02f, -0.006f), 0.1f, new Color(1f, 0.85f, 0.3f), overlay: true);
            panel.gameObject.SetActive(false);
            gm.moneyPanel = panel.gameObject;
            gm.moneyText = text;
            gm.festivalTimeText = timeText;
            gm.festivalTimeBar = barPivot;
        }

        /// <summary>
        /// 残高のパネル (カウンターの上にまっすぐ立てた小さな板。メニューの板をふさがないように小さく低く)。
        /// 「YOUR MONEY」、今のお金 (FestivalStage が書き換える)、お金のため方。pos = パネルの下のふちの真ん中
        /// </summary>
        TextMesh BalancePanel(Transform t, Vector3 pos)
        {
            var panel = Group(t, "BalancePanel");
            panel.localPosition = pos;
            panel.localScale = Vector3.one * 0.75f; // 小さめ (0.71 × 0.23m)。まっすぐ立てる (傾けない)
            const float W = 0.95f, H = 0.3f;
            var gold = Mat("Coin_Gold", new Color(1f, 0.8f, 0.3f), new Color(0.25f, 0.18f, 0.02f), false, 0.85f);
            Prim(PrimitiveType.Cube, "Frame", panel, new Vector3(0f, H / 2f, 0.01f), new Vector3(W + 0.05f, H + 0.05f, 0.03f), gold, false);
            Prim(PrimitiveType.Cube, "Board", panel, new Vector3(0f, H / 2f, -0.008f), new Vector3(W, H, 0.015f), Mat("Balance_Board", new Color(0.05f, 0.06f, 0.18f), new Color(0.02f, 0.02f, 0.08f)), false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "CoinIcon", panel, new Vector3(s * (W / 2f - 0.09f), H * 0.55f, -0.02f), new Vector3(0.09f, 0.006f, 0.09f), gold, false)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MakeText(panel, "Title", "YOUR MONEY", new Vector3(0f, H - 0.04f, -0.018f), 0.035f, new Color(0.85f, 0.9f, 1f));
            var balance = MakeText(panel, "Balance", "¥0", new Vector3(0f, H * 0.53f, -0.018f), 0.1f, new Color(1f, 0.85f, 0.3f));
            MakeText(panel, "Hint", "WIN GAMES = MONEY", new Vector3(0f, 0.04f, -0.018f), 0.028f, new Color(0.6f, 1f, 0.7f));
            return balance;
        }

        /// <summary>
        /// メニューの板 1枚 (的)。色の付いた板に、料理の見本 (小さな棚の上)・名前・値段。お金が当たると買えるが、板は消えずに少し揺れるだけ。
        /// 見本は Pivot/Model の下 (買った時に、これを複製して机に並べる)。pos = 板の下のふちの真ん中
        /// </summary>
        Target MenuBoard(Transform parent, string name, int price, Vector3 pos, Material board, System.Func<Transform, Vector3> build)
        {
            const float W = 0.88f, H = 0.82f;
            var root = Group(parent, $"Food_{name}");
            root.localPosition = pos;
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.FestivalFood;
            target.points = price;
            target.stayUpOnHit = true;  // 当たっても消えない
            target.multiHitInterval = 0.3f;
            target.hitWobbleDeg = 6f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            Prim(PrimitiveType.Cube, "Board", pivot, new Vector3(0f, H / 2f, 0f), new Vector3(W, H, 0.04f), board); // 当たり判定はこの板
            Prim(PrimitiveType.Cube, "BoardCard", pivot, new Vector3(0f, H * 0.5f, -0.021f), new Vector3(W - 0.08f, H * 0.56f, 0.005f), Mat("Festival_Card", new Color(0.97f, 0.96f, 0.9f)), false);
            MakeText(pivot, "Name", name.Replace('_', ' '), new Vector3(0f, H - 0.08f, -0.025f), 0.062f, Color.white);
            MakeText(pivot, "Price", $"¥{price}", new Vector3(0f, 0.085f, -0.025f), 0.11f, new Color(1f, 0.9f, 0.35f));
            // 料理の見本 (板の前の小さな棚に置く。板に収まる大きさに縮める)
            Prim(PrimitiveType.Cube, "Ledge", pivot, new Vector3(0f, 0.19f, -0.07f), new Vector3(W * 0.6f, 0.02f, 0.12f), Mat("Festival_Wood", new Color(0.5f, 0.32f, 0.18f), null, false, 0.3f), false);
            var model = Group(pivot, "Model");
            model.localPosition = new Vector3(0f, 0.2f, -0.07f);
            var size = build(model);
            model.localScale = Vector3.one * Mathf.Min(1f, 0.42f / Mathf.Max(0.01f, size.y), 0.6f / Mathf.Max(0.01f, size.x));
            return target;
        }

        Material FoodMat(string name, Color color, float smooth = 0.35f) => Mat("Food_" + name, color, null, false, smooth);

        // 割りばし / 串
        void Stick(Transform p, float height) =>
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, height / 2f, 0f), new Vector3(0.012f, height / 2f, 0.012f), FoodMat("Stick", new Color(0.9f, 0.8f, 0.6f)));

        // 串をさして立てる台 (発泡スチロールの台)
        void StickStand(Transform p) =>
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.02f, 0f), new Vector3(0.12f, 0.04f, 0.12f), FoodMat("Stand", new Color(0.95f, 0.95f, 0.95f)));

        Vector3 FoodRingoAme(Transform p)
        {
            StickStand(p);
            Stick(p, 0.2f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.22f, 0f), Vector3.one * 0.12f, Mat("Food_RingoAme", new Color(0.9f, 0.05f, 0.08f), new Color(0.2f, 0f, 0f), false, 0.97f));
            Deco(PrimitiveType.Cube, p, new Vector3(0.02f, 0.29f, 0f), new Vector3(0.03f, 0.005f, 0.015f), FoodMat("Leaf", new Color(0.2f, 0.6f, 0.2f)), new Vector3(0f, 0f, 20f));
            return new Vector3(0.13f, 0.3f, 0.13f);
        }

        Vector3 FoodTakoyaki(Transform p)
        {
            var tray = FoodMat("Tray", new Color(0.85f, 0.7f, 0.45f));
            var ball = FoodMat("Takoyaki", new Color(0.6f, 0.38f, 0.15f));
            var sauce = FoodMat("Sauce", new Color(0.25f, 0.1f, 0.05f), 0.7f);
            var mayo = FoodMat("Mayo", new Color(1f, 0.97f, 0.85f), 0.6f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.015f, 0f), new Vector3(0.22f, 0.03f, 0.14f), tray);
            for (int i = 0; i < 6; i++)
            {
                var c = new Vector3(-0.065f + (i % 3) * 0.065f, 0.055f, i < 3 ? -0.032f : 0.032f);
                Deco(PrimitiveType.Sphere, p, c, Vector3.one * 0.06f, ball);
                Deco(PrimitiveType.Sphere, p, c + Vector3.up * 0.022f, new Vector3(0.05f, 0.02f, 0.05f), sauce);
            }
            for (int i = 0; i < 4; i++)
                Deco(PrimitiveType.Cube, p, new Vector3(-0.07f + i * 0.045f, 0.087f, 0f), new Vector3(0.006f, 0.004f, 0.12f), mayo, new Vector3(0f, i % 2 == 0 ? 30f : -30f, 0f));
            Deco(PrimitiveType.Cylinder, p, new Vector3(0.09f, 0.08f, 0.05f), new Vector3(0.006f, 0.05f, 0.006f), FoodMat("Stick", new Color(0.9f, 0.8f, 0.6f)), new Vector3(60f, 0f, 0f)); // つまようじ
            return new Vector3(0.22f, 0.1f, 0.14f);
        }

        Vector3 FoodYakisoba(Transform p)
        {
            var pack = FoodMat("Pack", new Color(0.97f, 0.97f, 0.97f), 0.6f);
            var noodle = Mat("Food_Noodle", new Color(0.55f, 0.32f, 0.12f), null, false, 0.4f);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.015f, 0f), new Vector3(0.24f, 0.03f, 0.16f), pack);
            for (int i = 0; i < 6; i++)
                Deco(PrimitiveType.Sphere, p, new Vector3(-0.07f + (i % 3) * 0.07f, 0.045f, i < 3 ? -0.03f : 0.03f), new Vector3(0.1f, 0.04f, 0.07f), noodle, new Vector3(0f, i * 40f, 0f));
            Deco(PrimitiveType.Cube, p, new Vector3(0.05f, 0.07f, -0.02f), new Vector3(0.03f, 0.008f, 0.02f), FoodMat("Ginger", new Color(0.95f, 0.15f, 0.25f)));
            for (int i = 0; i < 5; i++)
                Deco(PrimitiveType.Cube, p, new Vector3(-0.06f + i * 0.03f, 0.068f, 0.02f * (i % 2)), Vector3.one * 0.01f, FoodMat("Aonori", new Color(0.2f, 0.5f, 0.15f)));
            return new Vector3(0.24f, 0.08f, 0.16f);
        }

        Vector3 FoodKakigori(Transform p)
        {
            var cup = FoodMat("Cup", new Color(0.97f, 0.97f, 0.97f), 0.5f);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.04f, 0f), new Vector3(0.1f, 0.04f, 0.1f), cup);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.06f, 0f), new Vector3(0.102f, 0.006f, 0.102f), FoodMat("Cup_Stripe", new Color(0.2f, 0.5f, 0.95f)));
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.1f, 0f), new Vector3(0.13f, 0.12f, 0.13f), FoodMat("Ice", new Color(0.97f, 0.98f, 1f), 0.8f));
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.09f, 0.12f), Mat("Food_Syrup", new Color(1f, 0.15f, 0.25f), new Color(0.15f, 0f, 0.02f), false, 0.9f));
            Deco(PrimitiveType.Cube, p, new Vector3(0.03f, 0.17f, 0f), new Vector3(0.012f, 0.12f, 0.004f), FoodMat("Spoon", new Color(0.3f, 0.75f, 1f)), new Vector3(0f, 0f, -15f));
            return new Vector3(0.13f, 0.22f, 0.13f);
        }

        Vector3 FoodChocoBanana(Transform p)
        {
            StickStand(p);
            Stick(p, 0.14f);
            Deco(PrimitiveType.Capsule, p, new Vector3(0f, 0.22f, 0f), new Vector3(0.055f, 0.08f, 0.055f), FoodMat("Chocolate", new Color(0.3f, 0.15f, 0.06f), 0.75f));
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.305f, 0f), new Vector3(0.04f, 0.03f, 0.04f), FoodMat("Banana", new Color(1f, 0.9f, 0.4f)));
            var colors = new[] { new Color(1f, 0.3f, 0.5f), new Color(0.3f, 0.8f, 1f), new Color(1f, 0.9f, 0.2f), new Color(0.4f, 0.9f, 0.4f) };
            for (int i = 0; i < 10; i++)
            {
                float a = i * 1.7f;
                Deco(PrimitiveType.Cube, p, new Vector3(Mathf.Sin(a) * 0.028f, 0.19f + (i % 5) * 0.022f, Mathf.Cos(a) * 0.028f), new Vector3(0.012f, 0.004f, 0.004f), FoodMat($"Sprinkle_{i % 4}", colors[i % 4]), new Vector3(0f, a * 50f, 0f));
            }
            return new Vector3(0.12f, 0.33f, 0.12f);
        }

        Vector3 FoodRamune(Transform p)
        {
            var glass = Mat("Food_RamuneGlass", new Color(0.45f, 0.85f, 0.9f), new Color(0.03f, 0.1f, 0.12f), false, 0.95f);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.06f, 0f), new Vector3(0.065f, 0.06f, 0.065f), glass);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.13f, 0f), new Vector3(0.06f, 0.04f, 0.06f), glass);
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.17f, 0f), new Vector3(0.035f, 0.03f, 0.035f), glass);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.15f, 0f), Vector3.one * 0.025f, FoodMat("Marble", new Color(0.9f, 1f, 1f), 0.95f));
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.205f, 0f), new Vector3(0.04f, 0.008f, 0.04f), FoodMat("RamuneCap", new Color(0.2f, 0.45f, 0.95f)));
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.06f, 0f), new Vector3(0.067f, 0.02f, 0.067f), FoodMat("RamuneLabel", new Color(0.97f, 0.97f, 0.97f)));
            return new Vector3(0.07f, 0.22f, 0.07f);
        }

        Vector3 FoodCottonCandy(Transform p)
        {
            StickStand(p);
            Stick(p, 0.22f);
            var pink = Mat("Food_CottonCandy", new Color(1f, 0.7f, 0.85f), new Color(0.15f, 0.05f, 0.1f), false, 0.05f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.18f, pink);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                Deco(PrimitiveType.Sphere, p, new Vector3(Mathf.Sin(a) * 0.07f, 0.3f + Mathf.Cos(a * 2f) * 0.04f, Mathf.Cos(a) * 0.07f), Vector3.one * 0.11f, pink);
            }
            return new Vector3(0.24f, 0.42f, 0.24f);
        }

        Vector3 FoodIkaYaki(Transform p)
        {
            StickStand(p);
            Stick(p, 0.12f);
            var squid = FoodMat("Squid", new Color(0.75f, 0.35f, 0.18f), 0.7f);
            Deco(PrimitiveType.Sphere, p, new Vector3(0f, 0.26f, 0f), new Vector3(0.1f, 0.24f, 0.04f), squid);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.38f, 0f), new Vector3(0.11f, 0.11f, 0.02f), squid, new Vector3(0f, 0f, 45f)); // えんぺら
            for (int i = 0; i < 5; i++)
                Deco(PrimitiveType.Cylinder, p, new Vector3(-0.04f + i * 0.02f, 0.11f, 0f), new Vector3(0.012f, 0.04f, 0.012f), squid, new Vector3(0f, 0f, (i - 2) * 10f));
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.26f, -0.021f), new Vector3(0.06f, 0.15f, 0.003f), FoodMat("Sauce", new Color(0.25f, 0.1f, 0.05f), 0.7f));
            return new Vector3(0.12f, 0.45f, 0.08f);
        }

        Vector3 FoodFrankfurt(Transform p)
        {
            StickStand(p);
            Stick(p, 0.12f);
            Deco(PrimitiveType.Capsule, p, new Vector3(0f, 0.22f, 0f), new Vector3(0.05f, 0.09f, 0.05f), FoodMat("Sausage", new Color(0.7f, 0.25f, 0.12f), 0.75f));
            for (int i = 0; i < 4; i++)
                Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.16f + i * 0.04f, -0.024f), new Vector3(0.04f, 0.006f, 0.004f), FoodMat("Mustard", new Color(1f, 0.85f, 0.1f)), new Vector3(0f, 0f, i % 2 == 0 ? 25f : -25f));
            return new Vector3(0.12f, 0.32f, 0.12f);
        }

        Vector3 FoodCorn(Transform p)
        {
            StickStand(p);
            Stick(p, 0.1f);
            var corn = FoodMat("Corn", new Color(0.95f, 0.75f, 0.2f), 0.6f);
            Deco(PrimitiveType.Capsule, p, new Vector3(0f, 0.22f, 0f), new Vector3(0.065f, 0.1f, 0.065f), corn);
            var burnt = FoodMat("Corn_Burnt", new Color(0.45f, 0.25f, 0.08f), 0.6f);
            for (int i = 0; i < 4; i++)
                Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.16f + i * 0.04f, 0f), new Vector3(0.067f, 0.005f, 0.067f), burnt);
            return new Vector3(0.12f, 0.34f, 0.12f);
        }

        Vector3 FoodOkonomiyaki(Transform p)
        {
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.01f, 0f), new Vector3(0.24f, 0.01f, 0.24f), FoodMat("Pack", new Color(0.97f, 0.97f, 0.97f), 0.6f));
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.03f, 0f), new Vector3(0.2f, 0.02f, 0.2f), FoodMat("Okonomiyaki", new Color(0.75f, 0.5f, 0.25f)));
            Deco(PrimitiveType.Cylinder, p, new Vector3(0f, 0.051f, 0f), new Vector3(0.18f, 0.003f, 0.18f), FoodMat("Sauce", new Color(0.25f, 0.1f, 0.05f), 0.7f));
            for (int i = 0; i < 4; i++)
                Deco(PrimitiveType.Cube, p, new Vector3(-0.06f + i * 0.04f, 0.055f, 0f), new Vector3(0.006f, 0.004f, 0.16f), FoodMat("Mayo", new Color(1f, 0.97f, 0.85f), 0.6f), new Vector3(0f, 25f, 0f));
            for (int i = 0; i < 8; i++)
                Deco(PrimitiveType.Cube, p, new Vector3(Random.Range(-0.06f, 0.06f), 0.06f, Random.Range(-0.06f, 0.06f)), new Vector3(0.02f, 0.004f, 0.015f), FoodMat("Bonito", new Color(0.85f, 0.6f, 0.45f)), new Vector3(Random.Range(-30f, 30f), Random.Range(0f, 360f), 0f));
            return new Vector3(0.24f, 0.07f, 0.24f);
        }

        Vector3 FoodCastella(Transform p)
        {
            var bag = FoodMat("CastellaBag", new Color(0.97f, 0.95f, 0.9f));
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.08f, 0f), new Vector3(0.13f, 0.16f, 0.08f), bag);
            Deco(PrimitiveType.Cube, p, new Vector3(0f, 0.1f, -0.041f), new Vector3(0.13f, 0.03f, 0.002f), FoodMat("CastellaStripe", new Color(0.9f, 0.15f, 0.12f)));
            var cake = FoodMat("Castella", new Color(0.85f, 0.6f, 0.3f));
            for (int i = 0; i < 5; i++)
                Deco(PrimitiveType.Sphere, p, new Vector3(-0.04f + (i % 3) * 0.04f, 0.17f + (i / 3) * 0.025f, (i % 2) * 0.015f), Vector3.one * 0.04f, cake);
            return new Vector3(0.13f, 0.21f, 0.09f);
        }

        // ------------------------------------------------------------------
        // 頭の上の提灯の飾り: 4すみの柱から柱へ、四角と対角線に提灯の列を張る (この並び順に灯る)
        // ------------------------------------------------------------------

        Transform BuildFestivalLanterns(Transform root)
        {
            var deco = Group(root, "Lanterns");
            var pole = Mat("Festival_Pole", new Color(0.75f, 0.15f, 0.12f), null, false, 0.4f);
            var line = Mat("Chochin_Cap", new Color(0.08f, 0.06f, 0.05f));
            const float PoleR = 6.6f, PoleH = 7.6f;
            var tops = new Vector3[4];
            for (int k = 0; k < 4; k++)
            {
                float a = (45f + k * 90f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * PoleR, 0f, Mathf.Cos(a) * PoleR);
                Prim(PrimitiveType.Cylinder, "LanternPole", deco, p + Vector3.up * PoleH / 2f, new Vector3(0.14f, PoleH / 2f, 0.14f), pole, false);
                Prim(PrimitiveType.Sphere, "PoleTop", deco, p + Vector3.up * (PoleH + 0.08f), Vector3.one * 0.2f, Mat("Daruma_Gold", new Color(1f, 0.8f, 0.3f), null, false, 0.8f), false);
                tops[k] = p + Vector3.up * (PoleH - 0.4f);
            }
            // 四角 (となりの柱へ)
            for (int k = 0; k < 4; k++)
                LanternString(deco, tops[k], tops[(k + 1) % 4], 0.9f, 10, line);
            // 対角線 (頭の上で交わる)
            LanternString(deco, tops[0], tops[2], 1.4f, 14, line);
            LanternString(deco, tops[1], tops[3], 1.4f, 14, line);
            deco.gameObject.SetActive(false);
            return deco;
        }

        // a から b へ、真ん中が sag だけ垂れた線を張り、count 個の提灯を下げる (紅白を交互に)
        void LanternString(Transform parent, Vector3 a, Vector3 b, float sag, int count, Material line)
        {
            Vector3 At(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (sag * 4f * t * (1f - t));
            const int Segments = 8;
            for (int i = 0; i < Segments; i++)
                Rod("LanternLine", parent, At(i / (float)Segments), At((i + 1) / (float)Segments), 0.015f, line);
            for (int i = 0; i < count; i++)
                Chochin(parent, At((i + 0.5f) / count) + Vector3.down * 0.28f, 0.32f, i % 2 == 0);
        }

        // ------------------------------------------------------------------
        // 提灯の的 / 輪の形
        // ------------------------------------------------------------------

        /// <summary>提灯の的: 紺の夜空の板に赤い提灯と小さな花火、「MATSURI」の文字</summary>
        GameObject FestivalLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Festival_Plate", new Color(0.05f, 0.05f, 0.18f), new Color(0.02f, 0.02f, 0.08f)));
            // 小さな花火 (光の粒の輪) を左上と右上に
            var sparkA = Mat("Hanabi_Pink", new Color(2.6f, 0.8f, 2f), null, true);
            var sparkB = Mat("Hanabi_Gold", new Color(2.6f, 1.8f, 0.6f), null, true);
            foreach (var (center, mat) in new[] { (new Vector3(-0.08f, 0.08f, -0.022f), sparkA), (new Vector3(0.085f, 0.09f, -0.022f), sparkB) })
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4f;
                    Prim(PrimitiveType.Sphere, "Spark", look, c + center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.035f, Vector3.one * 0.012f, mat, false);
                }
            // ゆらゆら揺れる提灯
            var lantern = Group(look, "Lantern");
            lantern.localPosition = c + new Vector3(0f, -0.01f, -0.035f);
            var sway = lantern.gameObject.AddComponent<Oscillator>();
            sway.offset = new Vector3(0.01f, 0f, 0f);
            sway.speed = 2.5f;
            Chochin(lantern, Vector3.zero, 0.1f, true);
            Prim(PrimitiveType.Cube, "LanternBand", lantern, new Vector3(0f, 0f, -0.05f), new Vector3(0.06f, 0.012f, 0.002f), Mat("Chochin_Cap", new Color(0.08f, 0.06f, 0.05f)), false);
            MakeText(look, "Label", "MATSURI", c + new Vector3(0f, -0.115f, -0.025f), 0.04f, new Color(1f, 0.6f, 0.35f));
            return look.gameObject;
        }

        /// <summary>輪投げの輪 (ドーナツ形)。外の直径がだいたい 1.16 (太さ 0.16)。水平 (XZ 面) に寝ている</summary>
        Mesh TorusMesh()
        {
            const string name = "Shape_Torus";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int Major = 28, Minor = 10;
            const float R = 0.5f, r = 0.08f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < Major; i++)
            {
                for (int j = 0; j < Minor; j++)
                {
                    // 1つの四角を2つの三角形に (頂点は共有して、なめらかな陰影にする)
                    int a = i * Minor + j, b = ((i + 1) % Major) * Minor + j, c2 = ((i + 1) % Major) * Minor + (j + 1) % Minor, d = i * Minor + (j + 1) % Minor;
                    tris.AddRange(new[] { a, d, c2, a, c2, b });
                }
            }
            for (int i = 0; i < Major; i++)
            {
                float u = i * Mathf.PI * 2f / Major;
                var dir = new Vector3(Mathf.Cos(u), 0f, Mathf.Sin(u));
                for (int j = 0; j < Minor; j++)
                {
                    float v = j * Mathf.PI * 2f / Minor;
                    verts.Add(dir * (R + r * Mathf.Cos(v)) + Vector3.up * (r * Mathf.Sin(v)));
                }
            }
            return FinishMesh(name, verts, tris);
        }
    }
}
