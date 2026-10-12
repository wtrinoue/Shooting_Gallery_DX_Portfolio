using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 空中ステージ (プロペラの的を撃つと始まる) の生成: 自分のプロペラ機 (胴体の上に立つ)・下に広がる雲・後ろへ流れる雲と風の筋・敵の気球とプロペラ機・プロペラの的の見た目。
    /// プレイヤーは中心から動かず、翼も揺れない (酔い対策)。
    /// </summary>
    public partial class GalleryBuilder
    {
        enum AirType { Balloon, Fighter, Bomber }

        const float WingTop = 0.1f; // プレイヤーの足元 (胴体の上の台の上面) の高さ。機体の Model の中では翼の上面の高さにも使う

        SkyBattle BuildSkyBattle(BonusStage bonusStage)
        {
            var go = new GameObject("SkyBattle");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            var plane = BuildPlayerPlane(go.transform);

            // ---- 下に広がる雲の海 (動かない。流れて見えるのはその上の雲) ----
            var cloud = Mat("Cloud", new Color(0.96f, 0.97f, 1f), new Color(0.32f, 0.34f, 0.38f));
            var cloudShade = Mat("Cloud_Shade", new Color(0.8f, 0.85f, 0.93f), new Color(0.2f, 0.22f, 0.27f));
            var cloudSea = Prim(PrimitiveType.Cube, "CloudSea", go.transform, new Vector3(0f, -16f, 0f), new Vector3(120f, 0.1f, 120f), cloudShade, false);

            // ---- 後ろへ流れる景色 (ConveyorBelt のローカル X が後ろ (-Z)、ローカル Z が右 (+X) を向く) ----
            var scenery = new List<ConveyorBelt>();
            var factors = new List<float>();
            // 翼の下を流れる雲
            scenery.Add(SceneryBelt(go.transform, "LowClouds", 64f, 1f, factors, (belt, items) =>
            {
                for (int i = 0; i < 22; i++)
                    items.Add(CloudPuff(belt, $"LowCloud_{i}", new Vector3(-32f + i * 64f / 22f + Random.Range(-1f, 1f), Random.Range(-13f, -6f), Random.Range(-28f, 28f)),
                                        Random.Range(2.2f, 4f), i % 3 == 0 ? cloudShade : cloud));
            }));
            // 遠くの横を流れる雲 (敵が飛ぶ所より外)
            scenery.Add(SceneryBelt(go.transform, "FarClouds", 64f, 1f, factors, (belt, items) =>
            {
                for (int i = 0; i < 14; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    items.Add(CloudPuff(belt, $"FarCloud_{i}", new Vector3(-32f + i * 64f / 14f, Random.Range(-3f, 9f), side * Random.Range(17f, 27f)),
                                        Random.Range(2f, 3.5f), cloud));
                }
            }));
            // 翼のまわりを速く流れる風の筋 (プレイヤーの体の所は通らない)
            var wind = TransparentMat("Wind", new Color(1f, 1f, 1f, 0.35f));
            scenery.Add(SceneryBelt(go.transform, "Wind", 18f, 4f, factors, (belt, items) =>
            {
                for (int i = 0; i < 16; i++)
                {
                    float x = (i % 2 == 0 ? 1f : -1f) * Random.Range(1.8f, 10f); // 左右の翼のまわり
                    var streak = Prim(PrimitiveType.Cube, "WindStreak", belt, new Vector3(-9f + i * 18f / 16f, Random.Range(-1.5f, 3f), x),
                                      new Vector3(Random.Range(1.2f, 2.4f), 0.015f, 0.015f), wind, false);
                    items.Add(streak.transform);
                }
            }));

            // ---- 敵 (時間切れまで、空いている敵からランダムに次々出てくる。数が多い種類ほどよく出る。爆撃機はまれ) ----
            var explosion = TransparentMat("Explosion", new Color(1f, 0.55f, 0.15f, 0.8f));
            var smoke = TransparentMat("Smoke", new Color(0.25f, 0.25f, 0.27f, 0.55f));
            var pool = new (AirType type, int count)[] { (AirType.Balloon, 6), (AirType.Fighter, 5), (AirType.Bomber, 1) };
            var enemies = new List<SkyEnemy>();
            foreach (var (type, count) in pool)
                for (int i = 0; i < count; i++)
                    enemies.Add(BuildSkyEnemy(go.transform, $"Sky_{type}_{i}", type, i, explosion, smoke));

            var battle = go.AddComponent<SkyBattle>();
            battle.plane = plane;
            var ground = new List<Transform>();
            foreach (var name in new[] { "Ground", "PitRim" })
            {
                var t = world.Find(name);
                if (t != null) ground.Add(t);
            }
            if (playerMark != null) ground.Add(playerMark.transform);
            if (bonusStage != null) ground.Add(bonusStage.transform);
            battle.groundParts = ground.ToArray();
            battle.scenery = scenery.ToArray();
            battle.sceneryFactors = factors.ToArray();
            battle.showDuringStage = new[] { cloudSea };
            battle.enemies = enemies.ToArray();
            battle.maxFlying = 6;
            // 翼・雲は空中ステージの時だけ出る (シーンに生成した時も隠しておき、Begin で表示する)
            plane.gameObject.SetActive(false);
            cloudSea.SetActive(false);
            foreach (var belt in scenery) belt.gameObject.SetActive(false);
            go.SetActive(true);
            return battle;
        }

        /// <summary>もこもこの雲 (当たり判定なし)。いくつかの白い球を重ねる</summary>
        Transform CloudPuff(Transform parent, string name, Vector3 pos, float size, Material mat)
        {
            var c = Group(parent, name);
            c.localPosition = pos;
            int n = Random.Range(4, 7);
            for (int k = 0; k < n; k++)
            {
                var offset = new Vector3(Random.Range(-1f, 1f) * size * 0.6f, Random.Range(-0.15f, 0.3f) * size, Random.Range(-1f, 1f) * size * 0.4f);
                float s = size * Random.Range(0.55f, 0.9f);
                Prim(PrimitiveType.Sphere, "Puff", c, offset, new Vector3(s * 1.3f, s * 0.75f, s), mat, false);
            }
            return c;
        }

        // ------------------------------------------------------------------
        // 自分のプロペラ機 (揺れない)
        // ------------------------------------------------------------------

        /// <summary>
        /// プレイヤーが乗るプロペラ機: プレイヤーは胴体の真ん中の上 (操縦席の後ろの黒い台) に立つ。
        /// 前に操縦席のパイロットと機首のプロペラ、左右に翼とエンジン、後ろに尾翼。当たり判定はない (下を飛ぶ敵も撃てるように)。
        /// 機体は Model の中に作り、台の上面がプレイヤーの足元 (原点の高さ WingTop) に来るように Model ごとずらす。
        /// </summary>
        Transform BuildPlayerPlane(Transform parent)
        {
            var planeRoot = Group(parent, "PlayerPlane");
            var p = Group(planeRoot, "Model");
            var red = Mat("Plane_Red", new Color(0.78f, 0.13f, 0.1f), null, false, 0.55f);
            var cream = Mat("Plane_Cream", new Color(0.95f, 0.9f, 0.78f), null, false, 0.5f);
            var metal = Mat("Plane_Metal", new Color(0.6f, 0.62f, 0.66f), null, false, 0.8f);
            var dark = Mat("Plane_Dark", new Color(0.12f, 0.12f, 0.13f));
            var glass = Mat("Plane_Glass", new Color(0.55f, 0.8f, 0.95f), new Color(0.08f, 0.15f, 0.22f), false, 0.95f);
            var walk = Mat("Plane_Walkway", new Color(0.18f, 0.18f, 0.2f));
            var yellow = Mat("Plane_Yellow", new Color(1f, 0.8f, 0.15f), new Color(0.3f, 0.22f, 0f));
            var blue = Mat("Plane_Blue", new Color(0.15f, 0.3f, 0.75f));
            var white = Mat("Target_White", Color.white);
            var blur = TransparentMat("Prop_Blur", new Color(0.85f, 0.85f, 0.85f, 0.22f));

            const float FuseX = -2.4f, FuseY = 0.45f;
            const float Lead = 1.7f, Trail = -1.4f;   // 翼の前の縁 / 後ろの縁
            const float RightRoot = -1.8f, RightTip = 7.6f;
            const float LeftRoot = 2f * FuseX - RightRoot, LeftTip = 2f * FuseX - RightTip;
            float chord = Lead - Trail, midZ = (Lead + Trail) / 2f;
            const float FuseTop = FuseY + 0.8f; // 胴体のてっぺん
            const float StandZ = -2f;           // プレイヤーが立つ所 (操縦席の後ろ)
            p.localPosition = new Vector3(-FuseX, WingTop - FuseTop, -StandZ);

            // ---- 翼 (左右) ----
            foreach (var (root, tip) in new[] { (RightRoot, RightTip), (LeftRoot, LeftTip) })
            {
                float cx = (root + tip) / 2f, span = Mathf.Abs(tip - root), s = Mathf.Sign(tip - root);
                Prim(PrimitiveType.Cube, "Wing", p, new Vector3(cx, WingTop - 0.07f, midZ), new Vector3(span, 0.14f, chord), cream, false);
                var edge = Prim(PrimitiveType.Cylinder, "LeadingEdge", p, new Vector3(cx, WingTop - 0.07f, Lead), new Vector3(0.155f, span / 2f, 0.155f), red, false);
                edge.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                // 翼より少し厚く・前後に長くして、面が重なってちらつかないようにする
                Prim(PrimitiveType.Cylinder, "WingTip", p, new Vector3(tip, WingTop - 0.07f, midZ), new Vector3(1.2f, 0.085f, chord + 0.04f), red, false);
                // 補助翼の継ぎ目とリベットの列
                Prim(PrimitiveType.Cube, "Aileron", p, new Vector3(root + s * span * 0.7f, WingTop + 0.003f, Trail + 0.5f), new Vector3(span * 0.5f, 0.004f, 0.02f), dark, false);
                foreach (float z in new[] { -0.7f, 0.3f, 1.2f })
                    Prim(PrimitiveType.Cube, "Rivets", p, new Vector3(cx, WingTop + 0.002f, z), new Vector3(span - 0.3f, 0.003f, 0.012f), metal, false);
                // 翼の先の丸い印
                var mark = new Vector3(tip - s * 1.8f, WingTop, midZ);
                FlatDisc(p, "Roundel_Blue", mark + Vector3.up * 0.004f, 1.5f, blue);
                FlatDisc(p, "Roundel_White", mark + Vector3.up * 0.006f, 1.0f, white);
                FlatDisc(p, "Roundel_Red", mark + Vector3.up * 0.008f, 0.5f, red);
            }

            // プレイヤーが立つ台 (胴体の上に載せた黒い板と黄色いふち。下の赤い土台で丸い胴体とのすき間を埋める)
            const float DeckW = 1.0f, DeckL = 1.6f;
            Prim(PrimitiveType.Cube, "StandBase", p, new Vector3(FuseX, FuseTop - 0.14f, StandZ), new Vector3(DeckW - 0.1f, 0.2f, DeckL - 0.1f), red, false);
            Prim(PrimitiveType.Cube, "StandDeck", p, new Vector3(FuseX, FuseTop - 0.02f, StandZ), new Vector3(DeckW, 0.04f, DeckL), walk, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "StandLine", p, new Vector3(FuseX, FuseTop + 0.002f, StandZ + s * (DeckL / 2f - 0.03f)), new Vector3(DeckW, 0.004f, 0.05f), yellow, false);
                Prim(PrimitiveType.Cube, "StandLine", p, new Vector3(FuseX + s * (DeckW / 2f - 0.03f), FuseTop + 0.002f, StandZ), new Vector3(0.05f, 0.004f, DeckL), yellow, false);
            }
            var walkText = MakeText(p, "WalkText", "SKY GALLERY DX", new Vector3(4.2f, WingTop + 0.01f, -0.9f), 0.32f, new Color(1f, 0.8f, 0.15f));
            walkText.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // ---- 胴体 ----
            var fuse = Prim(PrimitiveType.Cylinder, "Fuselage", p, new Vector3(FuseX, FuseY, -1f), new Vector3(1.6f, 5f, 1.6f), red, false);
            fuse.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float z in new[] { -4.5f, 2.6f })
            {
                var band = Prim(PrimitiveType.Cylinder, "Band", p, new Vector3(FuseX, FuseY, z), new Vector3(1.63f, 0.15f, 1.63f), cream, false);
                band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var tail1 = Prim(PrimitiveType.Cylinder, "TailCone", p, new Vector3(FuseX, FuseY + 0.1f, -6.8f), new Vector3(1.2f, 0.9f, 1.2f), red, false);
            tail1.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tail2 = Prim(PrimitiveType.Cylinder, "TailCone2", p, new Vector3(FuseX, FuseY + 0.2f, -8.1f), new Vector3(0.75f, 0.5f, 0.75f), red, false);
            tail2.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var cowl = Prim(PrimitiveType.Cylinder, "Cowling", p, new Vector3(FuseX, FuseY, 4.2f), new Vector3(1.5f, 0.25f, 1.5f), metal, false);
            cowl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Propeller(p, "NoseProp", new Vector3(FuseX, FuseY, 4.6f), 1.3f, 3, red, dark, blur);

            // 操縦席: ガラスの風防と、ゴーグルのパイロット
            Prim(PrimitiveType.Sphere, "Canopy", p, new Vector3(FuseX, FuseY + 0.7f, 0.9f), new Vector3(0.95f, 0.75f, 2.2f), glass, false);
            Prim(PrimitiveType.Cube, "CanopyFrame", p, new Vector3(FuseX, FuseY + 1.07f, 0.9f), new Vector3(0.05f, 0.03f, 2.1f), metal, false);
            Prim(PrimitiveType.Sphere, "PilotHead", p, new Vector3(FuseX, FuseY + 0.85f, 1.1f), Vector3.one * 0.3f, Mat("Pilot_Skin", new Color(0.95f, 0.75f, 0.6f)), false);
            Prim(PrimitiveType.Sphere, "PilotCap", p, new Vector3(FuseX, FuseY + 0.92f, 1.08f), new Vector3(0.32f, 0.22f, 0.33f), Mat("Pilot_Leather", new Color(0.4f, 0.24f, 0.12f)), false);
            Prim(PrimitiveType.Cube, "Goggles", p, new Vector3(FuseX, FuseY + 0.88f, 1.24f), new Vector3(0.24f, 0.06f, 0.05f), glass, false);
            var scarf = Prim(PrimitiveType.Cube, "Scarf", p, new Vector3(FuseX + 0.12f, FuseY + 0.72f, 0.8f), new Vector3(0.08f, 0.03f, 0.5f), white, false);
            var flutter = scarf.AddComponent<Oscillator>(); // マフラーが風にはためく
            flutter.offset = new Vector3(0.03f, 0.02f, 0f);
            flutter.speed = 18f;

            // 尾翼
            Prim(PrimitiveType.Cube, "Stabilizer", p, new Vector3(FuseX, FuseY + 0.2f, -8.1f), new Vector3(4.6f, 0.08f, 1.2f), cream, false);
            Prim(PrimitiveType.Cube, "Fin", p, new Vector3(FuseX, FuseY + 1.15f, -8.3f), new Vector3(0.08f, 1.8f, 1.4f), red, false);
            foreach (float s in new[] { -1f, 1f })
            {
                var fin = MakeText(p, "FinText", "DX", new Vector3(FuseX + s * 0.05f, FuseY + 1.25f, -8.3f), 0.7f, new Color(1f, 0.8f, 0.15f));
                fin.transform.localRotation = Quaternion.Euler(0f, -90f * s, 0f);
            }

            // ---- 両方の翼のエンジンとプロペラ ----
            foreach (float x in new[] { 3.6f, 2f * FuseX - 3.6f })
            {
                var nacelle = Prim(PrimitiveType.Cylinder, "Nacelle", p, new Vector3(x, WingTop + 0.05f, 1.5f), new Vector3(0.9f, 1.3f, 0.9f), red, false);
                nacelle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var ring = Prim(PrimitiveType.Cylinder, "NacelleRing", p, new Vector3(x, WingTop + 0.05f, 2.7f), new Vector3(0.95f, 0.12f, 0.95f), metal, false);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                foreach (float s in new[] { -1f, 1f })
                {
                    var pipe = Prim(PrimitiveType.Cylinder, "Exhaust", p, new Vector3(x + s * 0.47f, WingTop + 0.1f, 0.6f), new Vector3(0.08f, 0.25f, 0.08f), dark, false);
                    pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                Propeller(p, "WingProp", new Vector3(x, WingTop + 0.05f, 2.85f), 1.1f, 3, red, dark, blur);
            }
            return planeRoot;
        }

        /// <summary>寝かせた薄い円盤 (見た目だけ)</summary>
        void FlatDisc(Transform parent, string name, Vector3 pos, float diameter, Material mat)
        {
            Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(diameter, 0.001f, diameter), mat, false);
        }

        /// <summary>
        /// 回るプロペラ (見た目だけ)。前 (+Z) を向いた軸のまわりに回る羽根と、回って見える半透明の円盤、先の丸いスピナー。
        /// Rotator で回すので、敵のプロペラ以外はこれで回り続ける。返り値は回る部分
        /// </summary>
        Transform Propeller(Transform parent, string name, Vector3 pos, float radius, int blades, Material spinnerMat, Material bladeMat, Material blurMat)
        {
            var hub = Group(parent, name);
            hub.localPosition = pos;
            Prim(PrimitiveType.Sphere, "Spinner", hub, new Vector3(0f, 0f, 0.1f), new Vector3(radius * 0.3f, radius * 0.3f, radius * 0.45f), spinnerMat, false);
            var spin = Group(hub, "Blades");
            var rot = spin.gameObject.AddComponent<Rotator>();
            rot.axis = Vector3.forward;
            rot.speed = 1500f;
            for (int i = 0; i < blades; i++)
            {
                var arm = Group(spin, "Arm");
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 360f / blades);
                var blade = Prim(PrimitiveType.Cube, "Blade", arm, new Vector3(0f, radius * 0.5f, 0f), new Vector3(radius * 0.13f, radius, 0.03f), bladeMat, false);
                blade.transform.localRotation = Quaternion.Euler(0f, 20f, 0f); // 羽根のひねり
            }
            var disc = Prim(PrimitiveType.Cylinder, "Blur", hub, Vector3.zero, new Vector3(radius * 2f, 0.003f, radius * 2f), blurMat, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return spin;
        }

        // ------------------------------------------------------------------
        // 敵 (気球 / プロペラ機)
        // ------------------------------------------------------------------

        static readonly Color[][] BalloonColors =
        {
            new[] { new Color(0.9f, 0.15f, 0.12f), new Color(1f, 0.85f, 0.2f) },
            new[] { new Color(0.15f, 0.4f, 0.9f), new Color(0.95f, 0.95f, 0.95f) },
            new[] { new Color(0.2f, 0.7f, 0.3f), new Color(1f, 0.55f, 0.1f) },
            new[] { new Color(0.6f, 0.25f, 0.8f), new Color(1f, 0.6f, 0.8f) },
            new[] { new Color(1f, 0.5f, 0.1f), new Color(0.2f, 0.2f, 0.25f) },
            new[] { new Color(0.1f, 0.7f, 0.75f), new Color(1f, 0.95f, 0.5f) },
        };

        /// <summary>
        /// 敵: 気球 (球皮に白い丸と大きな点数) / 戦闘機・爆撃機 (後ろに点数を書いた幕を引っぱる)。
        /// 点数の幕と丸はいつもプレイヤーの方を向くので、どちらへ飛んでいても読める。どこに当てても1発と数える。
        /// </summary>
        SkyEnemy BuildSkyEnemy(Transform parent, string name, AirType type, int index, Material explosion, Material smoke)
        {
            var root = Group(parent, name);
            root.gameObject.SetActive(false);
            var body = Group(root, "Body");
            var labels = new List<TextMesh>();
            var props = new List<Transform>();
            Transform envelope = null;
            var dark = Mat("Plane_Dark", new Color(0.12f, 0.12f, 0.13f));
            var metal = Mat("Plane_Metal", new Color(0.6f, 0.62f, 0.66f), null, false, 0.8f);
            var glass = Mat("Plane_Glass", new Color(0.55f, 0.8f, 0.95f), new Color(0.08f, 0.15f, 0.22f), false, 0.95f);
            var white = Mat("Target_White", Color.white);
            var blur = TransparentMat("Prop_Blur", new Color(0.85f, 0.85f, 0.85f, 0.22f));

            if (type == AirType.Balloon)
            {
                var colors = BalloonColors[index % BalloonColors.Length];
                var main = Mat($"Balloon_{index % BalloonColors.Length}_A", colors[0], colors[0] * 0.15f);
                var stripe = Mat($"Balloon_{index % BalloonColors.Length}_B", colors[1], colors[1] * 0.15f);
                envelope = Group(body, "Envelope");
                envelope.localPosition = new Vector3(0f, 2.3f, 0f);
                Prim(PrimitiveType.Sphere, "Skin", envelope, Vector3.zero, new Vector3(2.2f, 2.5f, 2.2f), main);
                Prim(PrimitiveType.Sphere, "Stripe", envelope, Vector3.zero, new Vector3(2.26f, 2.53f, 0.7f), stripe, false);
                Prim(PrimitiveType.Sphere, "Stripe", envelope, Vector3.zero, new Vector3(0.7f, 2.53f, 2.26f), stripe, false);
                Prim(PrimitiveType.Cylinder, "Skirt", envelope, new Vector3(0f, -1.3f, 0f), new Vector3(0.7f, 0.2f, 0.7f), stripe, false);
                // 点数: いつもプレイヤーの方を向く白い丸に大きく書く
                var face = Group(envelope, "PointFace");
                face.gameObject.AddComponent<FaceCenter>();
                var disc = Prim(PrimitiveType.Cylinder, "PointDisc", face, new Vector3(0f, 0.05f, -1.16f), new Vector3(1.45f, 0.005f, 1.45f), white, false);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                labels.Add(MakeText(face, "Points", "100", new Vector3(0f, 0.05f, -1.18f), 0.62f, colors[0] * 0.8f));
                // ロープ・バーナー・かご
                foreach (var c in new[] { new Vector3(-0.3f, 0f, -0.3f), new Vector3(0.3f, 0f, -0.3f), new Vector3(-0.3f, 0f, 0.3f), new Vector3(0.3f, 0f, 0.3f) })
                    DomeLine(body, "Rope", c + Vector3.up * 0.55f, c * 1.3f + Vector3.up * 1.05f, 0.02f, Mat("Rope", new Color(0.85f, 0.8f, 0.65f)));
                Prim(PrimitiveType.Cylinder, "Burner", body, new Vector3(0f, 0.85f, 0f), new Vector3(0.2f, 0.08f, 0.2f), metal, false);
                Prim(PrimitiveType.Sphere, "Flame", body, new Vector3(0f, 1.0f, 0f), new Vector3(0.14f, 0.25f, 0.14f), Mat("Burner_Flame", new Color(1f, 0.6f, 0.2f), new Color(2.5f, 1.2f, 0.3f)), false);
                Prim(PrimitiveType.Cube, "Basket", body, new Vector3(0f, 0.28f, 0f), new Vector3(0.75f, 0.55f, 0.75f), Mat("Basket", new Color(0.6f, 0.42f, 0.22f)));
                Prim(PrimitiveType.Cube, "BasketRim", body, new Vector3(0f, 0.57f, 0f), new Vector3(0.8f, 0.06f, 0.8f), Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f)), false);
            }
            else
            {
                bool big = type == AirType.Bomber;
                var hull = big ? Mat("Enemy_Bomber", new Color(0.18f, 0.2f, 0.17f), null, false, 0.5f)
                               : Mat("Enemy_Fighter", new Color(0.3f, 0.34f, 0.4f), null, false, 0.55f);
                var accent = big ? Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f))
                                 : Mat("Enemy_Accent", new Color(0.9f, 0.2f, 0.15f));
                float len = big ? 5.2f : 3f, dia = big ? 1.1f : 0.6f, span = big ? 8f : 4.2f, wingChord = big ? 1.5f : 0.9f;

                var fuse = Prim(PrimitiveType.Cylinder, "Fuselage", body, Vector3.zero, new Vector3(dia, len / 2f, dia), hull);
                fuse.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var nose = Prim(PrimitiveType.Cylinder, "Nose", body, new Vector3(0f, 0f, len / 2f), new Vector3(dia * 0.95f, 0.12f, dia * 0.95f), accent, false);
                nose.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Sphere, "TailCone", body, new Vector3(0f, 0.05f, -len / 2f), new Vector3(dia * 0.7f, dia * 0.7f, dia * 1.4f), hull, false);
                Prim(PrimitiveType.Cube, "Wing", body, new Vector3(0f, -dia * 0.2f, len * 0.08f), new Vector3(span, 0.08f, wingChord), hull);
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "WingTip", body, new Vector3(s * (span / 2f - 0.25f), -dia * 0.2f + 0.05f, len * 0.08f), new Vector3(0.5f, 0.012f, wingChord + 0.02f), accent, false); // 翼の上面から少し浮かせる (ちらつき防止)
                Prim(PrimitiveType.Cube, "Stabilizer", body, new Vector3(0f, 0.05f, -len / 2f + 0.25f), new Vector3(span * 0.38f, 0.06f, wingChord * 0.55f), hull);
                if (big)
                {   // 双尾翼
                    foreach (float s in new[] { -1f, 1f })
                        Prim(PrimitiveType.Cube, "Fin", body, new Vector3(s * span * 0.19f, 0.45f, -len / 2f + 0.25f), new Vector3(0.06f, 0.9f, 0.7f), hull);
                }
                else Prim(PrimitiveType.Cube, "Fin", body, new Vector3(0f, 0.35f, -len / 2f + 0.25f), new Vector3(0.05f, 0.6f, 0.55f), hull);
                Prim(PrimitiveType.Sphere, "Canopy", body, new Vector3(0f, dia * 0.45f, len * 0.15f), new Vector3(dia * 0.6f, dia * 0.55f, dia * 1.3f), glass, false);

                // プロペラ (爆撃機は両方の翼に2つずつ、戦闘機は機首に1つ)
                if (big)
                {
                    foreach (float x in new[] { -2.6f, -1.3f, 1.3f, 2.6f })
                    {
                        var eng = Prim(PrimitiveType.Cylinder, "Engine", body, new Vector3(x, -0.15f, 0.9f), new Vector3(0.45f, 0.55f, 0.45f), hull, false);
                        eng.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        props.Add(Propeller(body, "Prop", new Vector3(x, -0.15f, 1.5f), 0.6f, 3, accent, dark, blur));
                    }
                }
                else props.Add(Propeller(body, "Prop", new Vector3(0f, 0f, len / 2f + 0.08f), 0.65f, 2, accent, dark, blur));
                foreach (var pr in props) Object.DestroyImmediate(pr.GetComponent<Rotator>()); // 敵のプロペラは SkyEnemy が回す (撃墜されたら止まる)

                // 後ろに引っぱる点数の幕 (いつもプレイヤーの方を向く)。ロープは機体の後ろから
                float bannerZ = -len / 2f - (big ? 2.6f : 2f);
                var banner = Group(root, "Banner");
                banner.localPosition = new Vector3(0f, -0.3f, bannerZ);
                banner.gameObject.AddComponent<FaceCenter>();
                float bw = big ? 3.8f : 2.6f, bh = big ? 1.5f : 1.1f;
                var cloth = big ? Mat("Banner_Gold", new Color(1f, 0.82f, 0.3f), new Color(0.35f, 0.25f, 0.05f)) : white;
                Prim(PrimitiveType.Cube, "BannerCloth", banner, Vector3.zero, new Vector3(bw, bh, 0.03f), cloth);
                foreach (float y in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "BannerTrim", banner, new Vector3(0f, y * bh / 2f, -0.02f), new Vector3(bw, 0.08f, 0.01f), big ? dark : accent, false);
                labels.Add(MakeText(banner, "Points", "300", new Vector3(0f, 0f, -0.03f), bh * 0.72f, big ? new Color(0.1f, 0.08f, 0.05f) : new Color(0.85f, 0.12f, 0.1f)));
                DomeLine(root, "TowLine", new Vector3(0f, 0f, -len / 2f - 0.3f), new Vector3(0f, -0.3f, bannerZ + 0.1f), 0.02f, Mat("Rope", new Color(0.85f, 0.8f, 0.65f)));
            }

            // 全体の当たり判定: 根元に Target を付ける (どこに当たってもこの Target が撃たれる)。
            // 当てても倒れない (stayUpOnHit)。何発で撃墜かは SkyEnemy が数える。見た目は持たせない
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Aerial;
            target.manualControl = true;
            target.stayUpOnHit = true;
            target.multiHitInterval = 0.05f;
            target.hitWobbleDeg = 0f;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);

            var enemy = root.gameObject.AddComponent<SkyEnemy>();
            enemy.body = body;
            enemy.hitTarget = target;
            enemy.pointLabels = labels.ToArray();
            enemy.propellers = props.ToArray();
            enemy.envelope = envelope;
            enemy.explosionMaterial = explosion;
            enemy.smokeMaterial = smoke;
            switch (type)
            {
                case AirType.Balloon:
                    enemy.displayName = "BALLOON"; enemy.isBalloon = true;
                    enemy.pointChoices = new[] { 100, 200, 300 }; enemy.hitPoints = 3; enemy.speed = 1.6f;
                    enemy.distance = new Vector2(7f, 10f); enemy.altitude = new Vector2(-0.5f, 3.5f);
                    enemy.routeChoices = new[] { SkyEnemy.Route.Rise, SkyEnemy.Route.Rise, SkyEnemy.Route.Cross };
                    break;
                case AirType.Fighter:
                    enemy.displayName = "FIGHTER"; enemy.pointChoices = new[] { 300, 500 }; enemy.hitPoints = 6; enemy.speed = 6f;
                    enemy.distance = new Vector2(9f, 13f); enemy.altitude = new Vector2(-1f, 6f); enemy.bankDeg = 30f; enemy.bobHeight = 0.1f;
                    enemy.routeChoices = new[] { SkyEnemy.Route.Cross, SkyEnemy.Route.Cross, SkyEnemy.Route.Orbit };
                    break;
                default:
                    enemy.displayName = "BIG BOMBER"; enemy.pointChoices = new[] { 1000 }; enemy.hitPoints = 15; enemy.speed = 3f;
                    enemy.distance = new Vector2(12f, 15f); enemy.altitude = new Vector2(3f, 6f); enemy.bankDeg = 15f; enemy.bobHeight = 0.06f;
                    enemy.routeChoices = new[] { SkyEnemy.Route.Approach };
                    break;
            }
            return enemy;
        }

        // ------------------------------------------------------------------
        // プロペラの的の見た目
        // ------------------------------------------------------------------

        /// <summary>プロペラの的: 空色の板に赤い飛行機と回るプロペラ、「SKY BATTLE」の文字。まわりを白い雲の輪が回る</summary>
        GameObject SkyLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            var red = Mat("Plane_Red", new Color(0.78f, 0.13f, 0.1f), null, false, 0.55f);
            var cloud = Mat("Cloud", new Color(0.96f, 0.97f, 1f), new Color(0.32f, 0.34f, 0.38f));
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Sky_Plate", new Color(0.3f, 0.62f, 0.98f), new Color(0.08f, 0.2f, 0.4f)));
            // 小さな雲
            Prim(PrimitiveType.Sphere, "Cloud", look, c + new Vector3(-0.08f, -0.07f, -0.022f), new Vector3(0.09f, 0.04f, 0.01f), cloud, false);
            Prim(PrimitiveType.Sphere, "Cloud", look, c + new Vector3(0.09f, 0.08f, -0.022f), new Vector3(0.07f, 0.03f, 0.01f), cloud, false);
            // 正面から見た赤い飛行機: 胴体の丸・翼・尾翼・回るプロペラ
            var body = Prim(PrimitiveType.Cylinder, "Body", look, c + new Vector3(0f, 0.02f, -0.026f), new Vector3(0.05f, 0.004f, 0.05f), red, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Wing", look, c + new Vector3(0f, 0.01f, -0.025f), new Vector3(0.22f, 0.02f, 0.006f), red, false);
            Prim(PrimitiveType.Cube, "Fin", look, c + new Vector3(0f, 0.06f, -0.024f), new Vector3(0.008f, 0.05f, 0.006f), red, false);
            var prop = Group(look, "Prop");
            prop.localPosition = c + new Vector3(0f, 0.02f, -0.032f);
            var spin = prop.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.forward;
            spin.speed = 720f;
            Prim(PrimitiveType.Cube, "Blade", prop, Vector3.zero, new Vector3(0.012f, 0.1f, 0.004f), Mat("Plane_Dark", new Color(0.12f, 0.12f, 0.13f)), false);
            MakeText(look, "Label", "SKY BATTLE", c + new Vector3(0f, -0.115f, -0.025f), 0.035f, Color.white);

            var ring = Group(look, "Clouds");
            ring.localPosition = c + Vector3.back * 0.03f;
            var turn = ring.gameObject.AddComponent<Rotator>();
            turn.axis = Vector3.forward;
            turn.speed = 70f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6;
                Prim(PrimitiveType.Sphere, "Puff", ring, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.2f, new Vector3(0.06f, 0.035f, 0.03f), cloud, false);
            }
            return look.gameObject;
        }
    }
}
