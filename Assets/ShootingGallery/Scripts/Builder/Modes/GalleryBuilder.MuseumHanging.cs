using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、美術館の天井から吊るした粘土の作品 (的)。飛行機・恐竜の骨・クジラ。
    /// 初めは真っ白な粘土の塊 (白いものが撃つべき的) で、イマジネーションの粘土が当たるたびに 大まかな形 → 形 → 色のついた完成品 と変わる。
    /// 天井の点から細いワイヤーで吊るされ、ゆっくり揺れている。目の高さより上の高い所なので、ほかの作品の前には重ならない
    /// </summary>
    public partial class GalleryBuilder
    {
        const int HangingDesigns = 3; // 0 = 飛行機 / 1 = 恐竜の骨 / 2 = クジラ
        static readonly string[] HangingTitles = { "SKY DREAM", "ANCIENT GIANT", "OCEAN SONG" };

        // 天井から吊るす作品を並べる (真ん中の上に大きなクジラ、まわりに飛行機・恐竜の骨・小さなクジラ)
        void BuildHangingWorks(Transform hall, List<Artwork> works, Material sparkle)
        {
            var chip = TransparentMat("Museum_ClayChip", new Color(0.85f, 0.6f, 0.45f, 0.85f));
            var list = new (float angle, float radius, float height, float scale, int design)[]
            {
                (0f, 0f, 10f, 1.25f, 2),   // 真ん中の上: 大きなクジラ
                (45f, 7.5f, 8.5f, 1f, 0),  // 飛行機
                (135f, 7.5f, 8f, 1f, 1),   // 恐竜の骨
                (225f, 7.5f, 8.5f, 1f, 0), // 飛行機
                (315f, 7.5f, 9f, 0.8f, 2), // 小さなクジラ
            };
            for (int i = 0; i < list.Length; i++)
            {
                var (angle, radius, height, scale, design) = list[i];
                var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                works.Add(BuildHangingWork(hall, $"Hanging_{i}", dir * radius, angle + 90f, height, scale, design, chip, sparkle));
            }
        }

        /// <summary>天井から吊るした粘土の作品 (的) を1つ。pos = 真下の床の位置、yaw = 長い向き (ローカル X) の向き、height = 作品の真ん中の高さ</summary>
        Artwork BuildHangingWork(Transform parent, string name, Vector3 pos, float yaw, float height, float scale, int design, Material chip, Material sparkle)
        {
            // 根元は天井の吊り点 (ここを中心にゆっくり揺れる。出てくる時は天井からふくらむ)
            var root = Group(parent, name);
            root.localPosition = new Vector3(pos.x, MuseumWallH, pos.z);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.localScale = Vector3.one * scale;
            float drop = (MuseumWallH - height) / scale;
            var body = Group(root, "Body");
            body.localPosition = Vector3.down * drop;

            // ワイヤー (天井から作品の2か所へ)
            var wire = Mat("Museum_Wire", new Color(0.35f, 0.35f, 0.38f), null, false, 0.6f);
            float span = design == 1 ? 1.4f : 1.1f;
            foreach (float x in new[] { -span, span })
                DomeLine(root, "Wire", new Vector3(x * 0.6f, 0f, 0f), new Vector3(x, -drop + HangingTop(design), 0f), 0.012f / scale, wire);

            // 段 0: 真っ白な粘土の塊 (当たり判定は別の見えない箱。どの段でも同じ大きさで当たる)
            var size = HangingSize(design);
            var stages = new GameObject[4];
            var white = Mat("Clay_White", new Color(0.97f, 0.96f, 0.94f), new Color(0.2f, 0.2f, 0.19f), false, 0.1f, matte: true);
            var lump = Group(body, "Stage0");
            Prim(PrimitiveType.Sphere, "Lump", lump, Vector3.zero, size * 0.8f, white, false);
            Prim(PrimitiveType.Sphere, "LumpBump", lump, new Vector3(size.x * 0.25f, size.y * 0.15f, 0f), size * 0.45f, white, false);
            Prim(PrimitiveType.Sphere, "LumpBump", lump, new Vector3(-size.x * 0.28f, -size.y * 0.1f, size.z * 0.1f), size * 0.4f, white, false);
            stages[0] = lump.gameObject;
            for (int s = 1; s <= 3; s++)
            {
                var g = Group(body, $"Stage{s}");
                HangingShape(g, design, s - 1);
                g.gameObject.SetActive(false);
                stages[s] = g.gameObject;
            }
            var hit = new GameObject("Hit").AddComponent<BoxCollider>();
            hit.transform.SetParent(body, false);
            hit.size = size + Vector3.one * 0.3f;

            // 点数の札 (作品の下。いつもプレイヤーの方を向く)
            var plate = Group(body, "Plaque");
            plate.localPosition = Vector3.down * (size.y * 0.5f + 0.45f);
            plate.gameObject.AddComponent<FaceCenter>();
            Prim(PrimitiveType.Cube, "PlaqueBoard", plate, Vector3.zero, new Vector3(0.9f, 0.26f, 0.02f), Mat("Museum_Plaque", new Color(0.1f, 0.09f, 0.08f), null, false, 0.6f), false);
            var label = MakeText(plate, "PlaqueText", "200", new Vector3(0f, 0f, -0.015f), 0.17f, new Color(0.95f, 0.88f, 0.7f));

            // 当たった所にくっつく粘土のかたまり (作品の子。ふだんは隠す)
            var tool = Group(root, "ClayLump");
            var clayMat = Mat("Clay_Raw", new Color(0.78f, 0.52f, 0.4f), null, false, 0.15f, matte: true);
            Prim(PrimitiveType.Sphere, "Clay", tool, Vector3.zero, new Vector3(0.3f, 0.26f, 0.28f), clayMat, false);
            Prim(PrimitiveType.Sphere, "ClayBump", tool, new Vector3(0.09f, 0.07f, 0f), new Vector3(0.16f, 0.14f, 0.16f), clayMat, false);

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
            art.kind = Artwork.Kind.Hanging;
            art.title = HangingTitles[design % HangingTitles.Length];
            art.target = target;
            art.stages = stages;
            art.stagePoints = new[] { 200, 300, 400 };
            art.completeBonus = 800;
            art.plaque = label;
            art.tool = tool;
            art.chipMaterial = chip;
            art.sparkleMaterial = sparkle;
            art.sparkleOffset = Vector3.down * drop;

            // 天井の吊り点を中心に、ゆっくり揺れる
            var swing = root.gameObject.AddComponent<Pendulum>();
            swing.amplitude = 2.5f;
            swing.speed = Random.Range(0.45f, 0.65f);
            swing.phase = Random.Range(0f, 6.28f);

            AddArtField(root, Artwork.Kind.Hanging, Vector3.down * drop, Mathf.Max(size.x, size.y) * 0.5f + 0.4f); // (作品のすぐまわりだけ。広いとちかちかする)
            AddGlint(art, body, Vector3.zero, new Vector2(size.x, size.y), true, 0f, lump); // 白い粘土の塊の表面ぜんたいを、包むように流れる光の帯
            root.gameObject.SetActive(false);
            return art;
        }

        // 作品のおおよその大きさ (長さ X・高さ Y・幅 Z。粘土の塊と当たり判定の箱の大きさ)
        static Vector3 HangingSize(int design) => design switch
        {
            0 => new Vector3(3.8f, 1.3f, 4.4f),  // 飛行機 (翼の幅が Z)
            1 => new Vector3(5.2f, 2.4f, 1.2f),  // 恐竜の骨
            _ => new Vector3(5.6f, 1.8f, 2.4f),  // クジラ
        };

        // ワイヤーを結ぶ高さ (作品の真ん中から)
        static float HangingTop(int design) => design == 0 ? 0.75f : design == 1 ? 0.9f : 0.85f;

        /// <summary>
        /// 吊るす作品の形 (原点が作品の真ん中、長い向きが X、頭 / 機首が +X)。
        /// level 0: 粘土の大まかな形 (素焼き色の塊) / 1: 形 (淡い色の粘土の部品) / 2: 完成 (それぞれの色)
        /// </summary>
        void HangingShape(Transform g, int design, int level)
        {
            // 部品の役目ごとの色 (完成の時だけ本当の色。それまでは粘土の色を順に)
            var raw = new[] { Mat("Clay_Raw", new Color(0.78f, 0.52f, 0.4f), null, false, 0.15f, matte: true), Mat("Clay_RawDark", new Color(0.66f, 0.42f, 0.32f), null, false, 0.15f, matte: true) };
            var pastel = new[] { Mat("Clay_Peach", new Color(0.95f, 0.7f, 0.55f), null, false, 0.25f), Mat("Clay_Mint", new Color(0.55f, 0.82f, 0.7f), null, false, 0.25f),
                                 Mat("Clay_Sky", new Color(0.55f, 0.72f, 0.92f), null, false, 0.25f), Mat("Clay_Lemon", new Color(0.95f, 0.88f, 0.5f), null, false, 0.25f) };
            int n = 0;
            Material Pick(Material final) => level == 2 ? final : level == 1 ? pastel[n++ % pastel.Length] : raw[n++ % raw.Length];
            GameObject P(PrimitiveType t, string part, Vector3 p, Vector3 s, Vector3 euler, Material final)
            {
                var o = Prim(t, part, g, p, s, Pick(final), false);
                o.transform.localRotation = Quaternion.Euler(euler);
                return o;
            }
            switch (design % HangingDesigns)
            {
                case 0: // 飛行機 (昔のプロペラの複葉機)
                {
                    var red = Mat("Plane_Red", new Color(0.85f, 0.15f, 0.12f), null, false, 0.6f);
                    var yellow = Mat("Plane_Yellow", new Color(1f, 0.8f, 0.15f), null, false, 0.6f);
                    var dark = Mat("Plane_Dark", new Color(0.12f, 0.12f, 0.14f), null, false, 0.5f);
                    var glass = Mat("Plane_Glass", new Color(0.3f, 0.55f, 0.85f), null, false, 0.9f);
                    if (level == 0)
                    {
                        P(PrimitiveType.Cube, "Rough", Vector3.zero, new Vector3(3.4f, 0.6f, 0.6f), Vector3.zero, red);
                        P(PrimitiveType.Cube, "Rough", new Vector3(0.3f, 0f, 0f), new Vector3(0.9f, 0.15f, 4.2f), Vector3.zero, yellow);
                        P(PrimitiveType.Cube, "Rough", new Vector3(-1.6f, 0.3f, 0f), new Vector3(0.5f, 0.6f, 1.4f), Vector3.zero, yellow);
                        break;
                    }
                    P(PrimitiveType.Capsule, "Fuselage", Vector3.zero, new Vector3(0.6f, 1.75f, 0.6f), new Vector3(0f, 0f, 90f), red);
                    P(PrimitiveType.Sphere, "Nose", new Vector3(1.75f, 0f, 0f), new Vector3(0.45f, 0.45f, 0.45f), Vector3.zero, yellow);
                    P(PrimitiveType.Cube, "LowerWing", new Vector3(0.35f, -0.15f, 0f), new Vector3(0.9f, 0.07f, 4.4f), Vector3.zero, yellow);
                    P(PrimitiveType.Cube, "TailWing", new Vector3(-1.6f, 0.05f, 0f), new Vector3(0.55f, 0.05f, 1.5f), Vector3.zero, yellow);
                    P(PrimitiveType.Cube, "TailFin", new Vector3(-1.65f, 0.4f, 0f), new Vector3(0.55f, 0.65f, 0.05f), Vector3.zero, red);
                    P(PrimitiveType.Sphere, "Cockpit", new Vector3(0.1f, 0.32f, 0f), new Vector3(0.55f, 0.32f, 0.42f), Vector3.zero, glass);
                    if (level == 2)
                    {
                        P(PrimitiveType.Cube, "UpperWing", new Vector3(0.35f, 0.6f, 0f), new Vector3(0.9f, 0.07f, 4.4f), Vector3.zero, yellow);
                        foreach (float z in new[] { -1.4f, 1.4f })
                            P(PrimitiveType.Cylinder, "Strut", new Vector3(0.35f, 0.22f, z), new Vector3(0.05f, 0.38f, 0.05f), Vector3.zero, dark);
                        foreach (float a in new[] { 0f, 90f })
                            P(PrimitiveType.Cube, "Propeller", new Vector3(2.0f, 0f, 0f), new Vector3(0.06f, 1.4f, 0.12f), new Vector3(a, 0f, 0f), dark);
                        foreach (float z in new[] { -0.35f, 0.35f })
                            P(PrimitiveType.Cylinder, "Wheel", new Vector3(0.6f, -0.62f, z), new Vector3(0.32f, 0.04f, 0.32f), new Vector3(90f, 0f, 0f), dark);
                        P(PrimitiveType.Cube, "Stripe", new Vector3(-0.6f, 0f, 0f), new Vector3(0.18f, 0.62f, 0.62f), Vector3.zero, yellow);
                    }
                    break;
                }
                case 1: // 恐竜の骨 (背骨・あばら骨・頭の骨・足・しっぽ)
                {
                    var bone = Mat("Fossil_Bone", new Color(0.82f, 0.68f, 0.45f), null, false, 0.35f); // 化石の骨の色 (白くしない)
                    var joint = Mat("Fossil_Joint", new Color(0.55f, 0.4f, 0.25f), null, false, 0.4f);
                    var socket = Mat("Fossil_Socket", new Color(0.12f, 0.08f, 0.06f));
                    if (level == 0)
                    {
                        P(PrimitiveType.Cube, "Rough", new Vector3(0f, 0f, 0f), new Vector3(1.8f, 1.2f, 1f), Vector3.zero, bone);
                        P(PrimitiveType.Cube, "Rough", new Vector3(1.8f, 0.5f, 0f), new Vector3(1.4f, 0.6f, 0.6f), new Vector3(0f, 0f, 15f), bone);
                        P(PrimitiveType.Cube, "Rough", new Vector3(-1.9f, -0.1f, 0f), new Vector3(2f, 0.4f, 0.4f), new Vector3(0f, 0f, -8f), bone);
                        P(PrimitiveType.Cube, "Rough", new Vector3(0f, -1f, 0f), new Vector3(0.5f, 0.9f, 0.8f), Vector3.zero, bone);
                        break;
                    }
                    // 背骨: しっぽの先 → 腰 → 首 → 頭の点を、背骨の玉でつなぐ
                    var spine = new[] { new Vector3(-2.6f, -0.3f, 0f), new Vector3(-1.4f, 0f, 0f), new Vector3(-0.3f, 0.25f, 0f), new Vector3(0.8f, 0.3f, 0f), new Vector3(1.5f, 0.7f, 0f) };
                    int count = level == 2 ? 26 : 12;
                    for (int k = 0; k < count; k++)
                    {
                        float t = k / (count - 1f) * (spine.Length - 1);
                        int seg = Mathf.Min(spine.Length - 2, (int)t);
                        var p = Vector3.Lerp(spine[seg], spine[seg + 1], t - seg);
                        float sz = Mathf.Lerp(0.1f, 0.24f, Mathf.Sin(Mathf.Clamp01(k / (count - 1f)) * Mathf.PI * 0.85f));
                        P(PrimitiveType.Sphere, "Vertebra", p, new Vector3(sz, sz * 1.3f, sz), Vector3.zero, k % 2 == 0 ? bone : joint);
                    }
                    // 頭の骨 (長い頭とあご、目の穴)
                    P(PrimitiveType.Cube, "Skull", new Vector3(2.05f, 0.75f, 0f), new Vector3(0.9f, 0.42f, 0.38f), new Vector3(0f, 0f, -8f), bone);
                    P(PrimitiveType.Cube, "Jaw", new Vector3(2.05f, 0.48f, 0f), new Vector3(0.8f, 0.12f, 0.32f), new Vector3(0f, 0f, -14f), bone);
                    // 足 (太ももとすね)
                    foreach (float z in new[] { -0.25f, 0.25f })
                    {
                        P(PrimitiveType.Capsule, "Thigh", new Vector3(-0.3f, -0.4f, z), new Vector3(0.18f, 0.45f, 0.18f), new Vector3(0f, 0f, -20f), bone);
                        P(PrimitiveType.Capsule, "Shin", new Vector3(-0.15f, -1.05f, z), new Vector3(0.13f, 0.42f, 0.13f), new Vector3(0f, 0f, 15f), bone);
                    }
                    if (level == 2)
                    {
                        // あばら骨 (背骨から左右へ弓なりに下がる)
                        for (int k = 0; k < 7; k++)
                        {
                            float x = -0.6f + k * 0.22f;
                            float len = 0.3f + Mathf.Sin(k / 6f * Mathf.PI) * 0.25f;
                            foreach (float z in new[] { -1f, 1f })
                                P(PrimitiveType.Capsule, "Rib", new Vector3(x, 0.05f - len * 0.55f, z * 0.2f), new Vector3(0.05f, len, 0.05f), new Vector3(z * 22f, 0f, -5f), bone);
                        }
                        foreach (float z in new[] { -0.2f, 0.2f })
                        {
                            P(PrimitiveType.Sphere, "EyeSocket", new Vector3(2.15f, 0.82f, z * 0.95f), new Vector3(0.13f, 0.13f, 0.05f), Vector3.zero, socket);
                            P(PrimitiveType.Capsule, "Arm", new Vector3(1.1f, 0f, z * 1.2f), new Vector3(0.07f, 0.22f, 0.07f), new Vector3(0f, 0f, 40f), bone);
                            P(PrimitiveType.Cube, "Foot", new Vector3(0.05f, -1.48f, z * 1.25f), new Vector3(0.4f, 0.08f, 0.2f), Vector3.zero, joint);
                        }
                        for (int k = 0; k < 6; k++) // 歯
                            P(PrimitiveType.Cube, "Tooth", new Vector3(1.75f + k * 0.11f, 0.56f - k * 0.02f, 0f), new Vector3(0.04f, 0.08f, 0.3f), Vector3.zero, joint);
                    }
                    break;
                }
                default: // クジラ (ザトウクジラ: 長い胸びれと、のどのすじ)
                {
                    var blue = Mat("Whale_Blue", new Color(0.18f, 0.32f, 0.55f), null, false, 0.6f);
                    var belly = Mat("Whale_Belly", new Color(0.62f, 0.72f, 0.82f), null, false, 0.5f); // 白くしない (淡い青)
                    var groove = Mat("Whale_Groove", new Color(0.1f, 0.18f, 0.32f));
                    var eye = Mat("Whale_Eye", new Color(0.05f, 0.05f, 0.08f));
                    if (level == 0)
                    {
                        P(PrimitiveType.Sphere, "Rough", Vector3.zero, new Vector3(4.6f, 1.6f, 1.8f), Vector3.zero, blue);
                        P(PrimitiveType.Cube, "Rough", new Vector3(-2.6f, 0f, 0f), new Vector3(0.6f, 0.3f, 1.8f), Vector3.zero, blue);
                        break;
                    }
                    P(PrimitiveType.Sphere, "Body", Vector3.zero, new Vector3(4.8f, 1.6f, 1.7f), Vector3.zero, blue);
                    P(PrimitiveType.Sphere, "Belly", new Vector3(0.4f, -0.3f, 0f), new Vector3(3.8f, 1.1f, 1.5f), Vector3.zero, belly);
                    P(PrimitiveType.Capsule, "TailStock", new Vector3(-2.3f, 0.05f, 0f), new Vector3(0.6f, 0.9f, 0.6f), new Vector3(0f, 0f, 90f), blue);
                    foreach (float z in new[] { -1f, 1f })
                    {
                        P(PrimitiveType.Sphere, "Fluke", new Vector3(-3.05f, 0.1f, z * 0.55f), new Vector3(0.55f, 0.08f, 1.2f), new Vector3(0f, z * 25f, 0f), blue);
                        P(PrimitiveType.Sphere, "Flipper", new Vector3(0.9f, -0.45f, z * 1.05f), new Vector3(1.6f, 0.08f, 0.35f), new Vector3(z * 15f, z * 35f, -20f), level == 2 ? belly : blue);
                    }
                    if (level == 2)
                    {
                        foreach (float z in new[] { -1f, 1f })
                            P(PrimitiveType.Sphere, "Eye", new Vector3(1.55f, -0.05f, z * 0.62f), new Vector3(0.1f, 0.1f, 0.06f), Vector3.zero, eye);
                        for (int k = 0; k < 6; k++) // のどのすじ
                            P(PrimitiveType.Cube, "Groove", new Vector3(1.3f, -0.6f, -0.45f + k * 0.18f), new Vector3(1.6f, 0.03f, 0.03f), Vector3.zero, groove);
                        P(PrimitiveType.Sphere, "DorsalFin", new Vector3(-1.2f, 0.72f, 0f), new Vector3(0.4f, 0.25f, 0.08f), new Vector3(0f, 0f, -20f), blue);
                        for (int k = 0; k < 5; k++) // 頭のこぶ
                            P(PrimitiveType.Sphere, "Knob", new Vector3(1.6f + k * 0.15f, 0.45f - k * 0.06f, (k % 2 == 0 ? 0.12f : -0.12f)), Vector3.one * 0.12f, Vector3.zero, groove);
                    }
                    break;
                }
            }
        }
    }
}
