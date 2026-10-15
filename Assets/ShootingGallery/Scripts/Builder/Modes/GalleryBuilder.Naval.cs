using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 海戦モード (錨の的を撃つと始まる) の生成: 自分の船の甲板・海面・後ろへ流れる景色 (波・島・イルカ)・敵の海賊船・錨の的の見た目。
    /// プレイヤーは中心から動かず、甲板も揺れない (酔い対策)。
    /// </summary>
    public partial class GalleryBuilder
    {
        enum ShipType { Sloop, Frigate, Flagship }

        const float SeaY = 0.09f;      // 海面の高さ (穴のふちのランプより少し上)
        const float DeckTop = 0.15f;   // 甲板の上面の高さ

        NavalBattle BuildNavalBattle()
        {
            var go = new GameObject("NavalBattle");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            // ---- 海面 (地面の下からせり上がって地面を覆う) ----
            var sea = Prim(PrimitiveType.Cube, "Sea", go.transform, new Vector3(0f, SeaY - 0.01f, 0f), new Vector3(64f, 0.02f, 64f),
                           Mat("Sea", new Color(0.03f, 0.16f, 0.32f), new Color(0.01f, 0.05f, 0.12f), false, 0.95f));

            var deck = BuildPlayerShip(go.transform);

            // ---- 後ろへ流れる景色 (ConveyorBelt のローカル X が後ろ (-Z) を向くように、Y 軸まわりに 90 度回して置く) ----
            var foam = Mat("Foam", new Color(0.85f, 0.92f, 1f), new Color(0.15f, 0.2f, 0.28f));
            var scenery = new List<ConveyorBelt>();
            var factors = new List<float>();
            // 船べりの波しぶき (船のすぐ横)
            scenery.Add(SceneryBelt(go.transform, "Wake", 14f, 1f, factors, (belt, items) =>
            {
                for (int i = 0; i < 16; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    var streak = Prim(PrimitiveType.Cube, "Streak", belt, new Vector3(-7f + i * 14f / 16f, SeaY + 0.01f, side * Random.Range(1.8f, 2.5f)),
                                      new Vector3(Random.Range(0.6f, 1.2f), 0.01f, Random.Range(0.06f, 0.14f)), foam, false);
                    items.Add(streak.transform);
                }
            }));
            // 海に散らばる白波
            scenery.Add(SceneryBelt(go.transform, "Waves", 52f, 1f, factors, (belt, items) =>
            {
                for (int i = 0; i < 44; i++)
                {
                    float x = Random.Range(-26f, 26f), z = Random.Range(-24f, 24f);
                    if (Mathf.Abs(z) < 2.8f && Mathf.Abs(x) < 6f) z += Mathf.Sign(z == 0f ? 1f : z) * 3f; // 甲板の上には置かない
                    var cap = Prim(PrimitiveType.Cube, "Whitecap", belt, new Vector3(x, SeaY + 0.005f, z),
                                   new Vector3(Random.Range(0.4f, 0.9f), 0.01f, Random.Range(0.12f, 0.25f)), foam, false);
                    items.Add(cap.transform);
                }
            }));
            // 遠くの岩・小島・ブイ (当たり判定なし)
            scenery.Add(SceneryBelt(go.transform, "Scenery", 56f, 1f, factors, (belt, items) =>
            {
                var rock = Mat("Rock", new Color(0.3f, 0.3f, 0.33f), null, false, 0.2f);
                var sand = Mat("Sand", new Color(0.75f, 0.65f, 0.45f));
                var palm = Mat("Palm", new Color(0.15f, 0.5f, 0.2f));
                var buoyRed = Mat("Buoy_Red", new Color(0.9f, 0.15f, 0.1f));
                var buoyLight = Mat("Buoy_Light", new Color(1f, 0.85f, 0.4f), new Color(2f, 1.5f, 0.5f));
                for (int i = 0; i < 12; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    var item = Group(belt, $"Scenery_{i}");
                    item.localPosition = new Vector3(-28f + i * 56f / 12f, SeaY, side * Random.Range(11f, 22f));
                    switch (i % 3)
                    {
                        case 0: // 岩
                            Prim(PrimitiveType.Sphere, "Rock", item, Vector3.zero, new Vector3(Random.Range(1.2f, 2.4f), Random.Range(0.6f, 1.4f), Random.Range(1.2f, 2f)), rock, false);
                            Prim(PrimitiveType.Sphere, "Rock2", item, new Vector3(0.8f, 0f, 0.3f), new Vector3(0.9f, 0.5f, 0.8f), rock, false);
                            break;
                        case 1: // ヤシの木の小島
                            Prim(PrimitiveType.Cylinder, "Island", item, Vector3.zero, new Vector3(3f, 0.15f, 2.2f), sand, false);
                            Prim(PrimitiveType.Cylinder, "Trunk", item, new Vector3(0.2f, 0.9f, 0f), new Vector3(0.12f, 0.8f, 0.12f), Mat("Trunk", new Color(0.45f, 0.3f, 0.15f)), false);
                            for (int k = 0; k < 5; k++)
                            {
                                var leaf = Prim(PrimitiveType.Cube, "Leaf", item, new Vector3(0.2f, 1.7f, 0f), new Vector3(0.18f, 0.04f, 1.1f), palm, false);
                                leaf.transform.localRotation = Quaternion.Euler(20f, k * 72f, 0f);
                            }
                            break;
                        default: // 光るブイ
                            Prim(PrimitiveType.Cylinder, "Buoy", item, new Vector3(0f, 0.35f, 0f), new Vector3(0.45f, 0.4f, 0.45f), buoyRed, false);
                            Prim(PrimitiveType.Cylinder, "BuoyBand", item, new Vector3(0f, 0.4f, 0f), new Vector3(0.47f, 0.08f, 0.47f), Mat("Target_White", Color.white), false);
                            Prim(PrimitiveType.Sphere, "BuoyLight", item, new Vector3(0f, 0.9f, 0f), Vector3.one * 0.22f, buoyLight, false);
                            break;
                    }
                    items.Add(item);
                }
            }));

            // 大きな島 (砂浜・緑の丘・ヤシの木・小屋。遠くをゆっくり流れる)
            scenery.Add(SceneryBelt(go.transform, "Islands", 96f, 0.55f, factors, (belt, items) =>
            {
                for (int i = 0; i < 6; i++)
                {
                    float side = i % 2 == 0 ? 1f : -1f;
                    var island = Group(belt, $"Island_{i}");
                    island.localPosition = new Vector3(-48f + i * 16f + Random.Range(-3f, 3f), SeaY, side * Random.Range(19f, 26f));
                    BuildIsland(island, Random.Range(2.8f, 4.2f), i);
                    items.Add(island);
                }
            }));
            // イルカ (自分の船の横を並んで泳ぎ、ときどき飛び跳ねる。2〜3頭の群れ)
            var splash = TransparentMat("Sea_Splash", new Color(0.9f, 0.97f, 1f, 0.6f));
            scenery.Add(SceneryBelt(go.transform, "Dolphins", 40f, 0.3f, factors, (belt, items) =>
            {
                for (int g = 0; g < 4; g++)
                {
                    float side = g % 2 == 0 ? -1f : 1f;
                    var pod = Group(belt, $"DolphinPod_{g}");
                    pod.localPosition = new Vector3(-20f + g * 10f + Random.Range(-2f, 2f), SeaY, side * Random.Range(4.5f, 6.5f));
                    int n = Random.Range(2, 4);
                    for (int k = 0; k < n; k++)
                        BuildDolphin(pod, $"Dolphin_{k}", new Vector3(Random.Range(-1.5f, 1.5f), 0f, k * 0.9f * side), splash);
                    items.Add(pod);
                }
            }));

            // ---- 敵の船 (出てくる順) ----
            var explosion = TransparentMat("Explosion", new Color(1f, 0.55f, 0.15f, 0.8f));
            // 時間切れまで、空いている船からランダムに次々出てくる。数が多い種類ほどよく出る (旗艦はまれ)
            var pool = new (ShipType type, int count)[] { (ShipType.Sloop, 6), (ShipType.Frigate, 4), (ShipType.Flagship, 1) };
            var ships = new List<EnemyShip>();
            foreach (var (type, count) in pool)
                for (int i = 0; i < count; i++)
                    ships.Add(BuildEnemyShip(go.transform, $"EnemyShip_{type}_{i}", type, explosion));

            var battle = go.AddComponent<NavalBattle>();
            battle.deck = deck;
            battle.sea = sea.transform;
            battle.seaY = SeaY - 0.01f;
            battle.seaHiddenY = -0.4f;
            var ground = new List<Transform>(); // 海が覆ったら消す床 (ハッチは GameManager が消す)
            foreach (var name in new[] { "Ground", "PitRim" })
            {
                var t = world.Find(name);
                if (t != null) ground.Add(t);
            }
            battle.groundParts = ground.ToArray();
            battle.splashMaterial = splash;
            battle.scenery = scenery.ToArray();
            battle.sceneryFactors = factors.ToArray();
            battle.ships = ships.ToArray();
            battle.duration = 16.5f; // 屋台が沈みきるのを待つ分も含む
            // 甲板・海・景色は海戦モードの時だけ出る (シーンに生成した時も隠しておき、Begin で表示する)
            deck.gameObject.SetActive(false);
            sea.SetActive(false);
            foreach (var belt in scenery) belt.gameObject.SetActive(false);
            go.SetActive(true);
            return battle;
        }

        ConveyorBelt SceneryBelt(Transform parent, string name, float length, float factor, List<float> factors, System.Action<Transform, List<Transform>> fill)
        {
            var belt = Group(parent, name);
            belt.localRotation = Quaternion.Euler(0f, 90f, 0f); // ローカル X → 後ろ (-Z)
            var items = new List<Transform>();
            fill(belt, items);
            var conveyor = belt.gameObject.AddComponent<ConveyorBelt>();
            conveyor.items = items.ToArray();
            conveyor.length = length;
            conveyor.speed = 2f;
            factors.Add(factor);
            return conveyor;
        }

        // ------------------------------------------------------------------
        // 自分の船 (揺れない)
        // ------------------------------------------------------------------

        /// <summary>
        /// プレイヤーの足元の船: 板張りの甲板、船べりの手すりと金の縁、前に突き出た船首とバウスプリット、後ろの船室とランタン、
        /// プレイヤーの後ろに太い帆柱と2枚の帆 (頭より高い所なので前と横の視界はさえぎらない)、両舷の大砲、舵輪。
        /// </summary>
        Transform BuildPlayerShip(Transform parent)
        {
            var deck = Group(parent, "PlayerShip");
            var plank = Mat("Deck_Wood", new Color(0.45f, 0.3f, 0.18f), null, false, 0.35f);
            var woodDark = Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f));
            var hull = Mat("Ship_Hull", new Color(0.2f, 0.11f, 0.06f), null, false, 0.4f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var sail = Mat("Ship_Sail", new Color(0.9f, 0.87f, 0.78f));
            var rope = Mat("Rope", new Color(0.85f, 0.8f, 0.65f));
            var iron = Mat("Cannon_Iron", new Color(0.12f, 0.12f, 0.14f), null, false, 0.6f);
            var glow = Mat("Ship_Lantern", new Color(1f, 0.75f, 0.35f), new Color(2f, 1.2f, 0.4f));
            const float HalfW = 1.65f, BowZ = 3.5f, SternZ = -3.6f;
            float midZ = (BowZ + SternZ) / 2f, len = BowZ - SternZ;

            // 甲板と板の継ぎ目
            Prim(PrimitiveType.Cube, "Deck", deck, new Vector3(0f, DeckTop - 0.05f, midZ), new Vector3(HalfW * 2f, 0.1f, len), plank);
            var bowDeck = Prim(PrimitiveType.Cube, "BowDeck", deck, new Vector3(0f, DeckTop - 0.05f, BowZ), new Vector3(HalfW * 1.414f, 0.1f, HalfW * 1.414f), plank);
            bowDeck.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            for (int i = -4; i <= 4; i++)
                Prim(PrimitiveType.Cube, "PlankLine", deck, new Vector3(i * 0.34f, DeckTop + 0.002f, midZ), new Vector3(0.012f, 0.004f, len), woodDark, false);

            // 船体の側面 (海の中まで) と手すり
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "HullSide", deck, new Vector3(s * HalfW, -0.35f, midZ), new Vector3(0.14f, 1.1f, len), hull, false);
                Prim(PrimitiveType.Cube, "Bulwark", deck, new Vector3(s * HalfW, DeckTop + 0.28f, midZ), new Vector3(0.1f, 0.56f, len), hull, false);
                Prim(PrimitiveType.Cube, "Rail", deck, new Vector3(s * HalfW, DeckTop + 0.58f, midZ), new Vector3(0.16f, 0.05f, len), gold, false);
                // 船首へすぼまる手すり
                var from = new Vector3(s * HalfW, 0f, BowZ);
                var to = new Vector3(0f, 0f, BowZ + HalfW);
                var mid = (from + to) / 2f;
                var bowSide = Prim(PrimitiveType.Cube, "BowBulwark", deck, mid + Vector3.up * (DeckTop + 0.28f), new Vector3(0.1f, 0.56f, (to - from).magnitude), hull, false);
                bowSide.transform.localRotation = Quaternion.LookRotation(to - from);
                var bowRail = Prim(PrimitiveType.Cube, "BowRail", deck, mid + Vector3.up * (DeckTop + 0.58f), new Vector3(0.16f, 0.05f, (to - from).magnitude), gold, false);
                bowRail.transform.localRotation = Quaternion.LookRotation(to - from);
                var bowHull = Prim(PrimitiveType.Cube, "BowHull", deck, mid + Vector3.down * 0.35f, new Vector3(0.14f, 1.1f, (to - from).magnitude), hull, false);
                bowHull.transform.localRotation = Quaternion.LookRotation(to - from);

                // 大砲 (外へ突き出す)
                foreach (float z in new[] { -2.4f, 0.4f, 2.2f })
                {
                    Prim(PrimitiveType.Cube, "Carriage", deck, new Vector3(s * (HalfW - 0.35f), DeckTop + 0.12f, z), new Vector3(0.4f, 0.24f, 0.32f), woodDark, false);
                    var barrel = Prim(PrimitiveType.Cylinder, "Cannon", deck, new Vector3(s * (HalfW - 0.05f), DeckTop + 0.34f, z), new Vector3(0.16f, 0.4f, 0.16f), iron, false);
                    barrel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            var sprit = Prim(PrimitiveType.Cylinder, "Bowsprit", deck, new Vector3(0f, 0.9f, BowZ + HalfW + 0.5f), new Vector3(0.12f, 0.9f, 0.12f), woodDark, false);
            sprit.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "BowOrnament", deck, new Vector3(0f, DeckTop + 0.65f, BowZ + HalfW - 0.1f), Vector3.one * 0.18f, gold, false);

            // 船尾: 横板と船室 (窓が光る)、ランタン
            Prim(PrimitiveType.Cube, "Transom", deck, new Vector3(0f, DeckTop + 0.28f, SternZ), new Vector3(HalfW * 2f, 0.56f, 0.1f), hull, false);
            Prim(PrimitiveType.Cube, "SternCabin", deck, new Vector3(0f, DeckTop + 0.55f, SternZ + 0.5f), new Vector3(HalfW * 1.8f, 1.1f, 0.9f), hull, false);
            Prim(PrimitiveType.Cube, "SternCabinRoof", deck, new Vector3(0f, DeckTop + 1.12f, SternZ + 0.5f), new Vector3(HalfW * 1.9f, 0.06f, 1.0f), gold, false);
            for (int k = -2; k <= 2; k++)
                Prim(PrimitiveType.Cube, "CabinWindow", deck, new Vector3(k * 0.5f, DeckTop + 0.6f, SternZ + 0.96f), new Vector3(0.28f, 0.22f, 0.02f), glow, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cylinder, "LanternPost", deck, new Vector3(s * 1.35f, DeckTop + 1.45f, SternZ + 0.5f), new Vector3(0.04f, 0.3f, 0.04f), gold, false);
                Prim(PrimitiveType.Sphere, "Lantern", deck, new Vector3(s * 1.35f, DeckTop + 1.8f, SternZ + 0.5f), new Vector3(0.18f, 0.24f, 0.18f), glow, false);
            }

            // 舵輪 (プレイヤーの後ろ)
            var wheel = Group(deck, "Helm");
            wheel.localPosition = new Vector3(0f, DeckTop + 1.05f, -2.3f);
            Prim(PrimitiveType.Cube, "HelmPost", deck, new Vector3(0f, DeckTop + 0.5f, -2.25f), new Vector3(0.12f, 1.0f, 0.12f), woodDark, false);
            RingSegments(wheel, "HelmRing", 0.36f, 20, 0.04f, gold);
            for (int k = 0; k < 4; k++)
            {
                var spoke = Prim(PrimitiveType.Cube, "HelmSpoke", wheel, Vector3.zero, new Vector3(0.03f, 0.9f, 0.03f), woodDark, false);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, k * 45f);
            }

            // 帆柱と帆 (プレイヤーの後ろ。帆は頭より高い所)
            const float MastZ = -1.6f, MastTop = 7.4f;
            Prim(PrimitiveType.Cylinder, "Mast", deck, new Vector3(0f, (DeckTop + MastTop) / 2f, MastZ), new Vector3(0.22f, (MastTop - DeckTop) / 2f, 0.22f), woodDark, false);
            Prim(PrimitiveType.Cube, "Yard_Low", deck, new Vector3(0f, 3.0f, MastZ - 0.1f), new Vector3(3.8f, 0.1f, 0.1f), woodDark, false);
            Prim(PrimitiveType.Cube, "Sail_Low", deck, new Vector3(0f, 4.1f, MastZ - 0.15f), new Vector3(3.4f, 2.1f, 0.03f), sail, false);
            Prim(PrimitiveType.Cube, "Yard_High", deck, new Vector3(0f, 5.4f, MastZ - 0.1f), new Vector3(3.0f, 0.09f, 0.09f), woodDark, false);
            Prim(PrimitiveType.Cube, "Sail_High", deck, new Vector3(0f, 6.25f, MastZ - 0.15f), new Vector3(2.6f, 1.6f, 0.03f), sail, false);
            Prim(PrimitiveType.Cube, "Flag", deck, new Vector3(0.35f, MastTop - 0.1f, MastZ), new Vector3(0.7f, 0.4f, 0.01f), Mat("Flag_Player", new Color(0.15f, 0.35f, 0.9f), new Color(0.05f, 0.12f, 0.35f)), false);
            // 帆柱を支えるロープ
            foreach (var anchor in new[] { new Vector3(-HalfW, DeckTop + 0.6f, MastZ - 0.8f), new Vector3(HalfW, DeckTop + 0.6f, MastZ - 0.8f),
                                           new Vector3(0f, 1.5f, BowZ + HalfW + 1.2f), new Vector3(-HalfW, DeckTop + 0.6f, SternZ + 0.3f), new Vector3(HalfW, DeckTop + 0.6f, SternZ + 0.3f) })
                DomeLine(deck, "Rigging", anchor, new Vector3(0f, MastTop - 0.4f, MastZ), 0.018f, rope);

            // 甲板を照らすランタンの光
            var lightGo = new GameObject("DeckLight");
            lightGo.transform.SetParent(deck, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.4f, -2.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.45f);
            light.intensity = 1.6f;
            light.range = 7f;
            light.shadows = LightShadows.None;
            return deck;
        }

        // ------------------------------------------------------------------
        // 敵の船
        // ------------------------------------------------------------------

        /// <summary>
        /// 海賊船 (大航海時代の木の帆船)。丸みのある木の船体 (上は明るい板・下は濃い板、金の縁、砲門と大砲)、船首楼と船尾楼 (窓が光る)、
        /// 船首から突き出たバウスプリットと三角帆、帆柱ごとにふくらんだ生成りの横帆 (少し斜めに張って、横からも見える)、見張り台、ロープ。
        /// メインマストのてっぺんの大きな黒い海賊旗に点数を書く (旗はいつもプレイヤーの方を向くので、船がどちらへ走っていても読める)。
        /// 船体・帆・旗のどこにバズーカを当てても、旗の点数が入って撃沈する (小さな的は付いていない)。種類ごとに大きさ・点数・速さが違う。
        /// </summary>
        EnemyShip BuildEnemyShip(Transform parent, string name, ShipType type, Material explosion)
        {
            var root = Group(parent, name);
            root.gameObject.SetActive(false);
            var body = Group(root, "Body");

            var hullDark = Mat("Galleon_HullDark", new Color(0.26f, 0.15f, 0.08f), null, false, 0.3f);
            var hullLight = Mat("Galleon_HullLight", new Color(0.55f, 0.36f, 0.18f), null, false, 0.3f);
            var deckMat = Mat("Deck_Wood", new Color(0.45f, 0.3f, 0.18f), null, false, 0.35f);
            var woodDark = Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var canvas = Mat("Galleon_Sail", new Color(0.93f, 0.88f, 0.74f), null, false, 0.1f);
            var flagMat = Mat("Pirate_Flag", new Color(0.06f, 0.06f, 0.07f));
            var bone = Mat("Target_White", Color.white);
            var port = Mat("Pirate_Port", new Color(0.02f, 0.02f, 0.02f));
            var portLid = Mat("Galleon_PortLid", new Color(0.55f, 0.1f, 0.08f));
            var iron = Mat("Cannon_Iron", new Color(0.12f, 0.12f, 0.14f), null, false, 0.6f);
            var rope = Mat("Rope", new Color(0.85f, 0.8f, 0.65f));
            var lantern = Mat("Ship_Lantern", new Color(1f, 0.75f, 0.35f), new Color(2f, 1.2f, 0.4f));
            var pennant = Mat("Galleon_Pennant", new Color(0.8f, 0.12f, 0.1f));
            var numberColor = new Color(1f, 0.82f, 0.3f);

            float L, W, H, mastH, flagW;
            float[] masts; // 帆柱の位置 (船尾 0 〜 船首 1)
            switch (type)
            {
                case ShipType.Sloop: L = 3.4f; W = 1.1f; H = 0.95f; mastH = 3.6f; flagW = 1.35f; masts = new[] { 0.52f }; break;
                case ShipType.Frigate: L = 4.8f; W = 1.4f; H = 1.15f; mastH = 4.4f; flagW = 1.6f; masts = new[] { 0.32f, 0.62f }; break;
                default: L = 6.6f; W = 1.85f; H = 1.45f; mastH = 5.4f; flagW = 2f; masts = new[] { 0.24f, 0.5f, 0.74f }; break;
            }
            // 船体は海面から少し沈める (喫水)。y = 0 が海面
            var hull = Group(body, "Hull");
            hull.localPosition = Vector3.up * (H * 0.45f);
            string key = $"Galleon_{type}";
            var lower = MeshPart(hull, "HullLower", GalleonHullMesh(key, L, W, H, 0), hullDark);
            lower.gameObject.AddComponent<BoxCollider>(); // 船体の当たり判定 (メッシュの大きさに合う箱)
            MeshPart(hull, "HullUpper", GalleonHullMesh(key, L, W, H, 1), hullLight);
            MeshPart(hull, "Deck", GalleonHullMesh(key, L, W, H, 2), deckMat);
            MeshPart(hull, "Transom", GalleonHullMesh(key, L, W, H, 3), hullDark);
            lower.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; // 船体だけは影を落とす

            // 舷側の金の縁と、砲門 (赤いふた) と大砲
            foreach (float s in new[] { -1f, 1f })
            {
                int ports = Mathf.Max(3, Mathf.RoundToInt(L / 0.75f));
                for (int k = 0; k < ports; k++)
                {
                    float u = Mathf.Lerp(0.18f, 0.66f, (k + 0.5f) / ports);
                    var p = GalleonPoint(L, W, H, u, 0.3f);
                    float x = s * (p.x + 0.01f);
                    Prim(PrimitiveType.Cube, "GunPort", hull, new Vector3(x, p.y, p.z), new Vector3(0.02f, 0.16f, 0.18f), port, false);
                    Prim(PrimitiveType.Cube, "PortLid", hull, new Vector3(x, p.y + 0.13f, p.z), new Vector3(0.02f, 0.06f, 0.2f), portLid, false);
                    var gun = Prim(PrimitiveType.Cylinder, "Cannon", hull, new Vector3(x + s * 0.08f, p.y, p.z), new Vector3(0.07f, 0.1f, 0.07f), iron, false);
                    gun.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                for (int k = 0; k < 10; k++) // 金の縁 (船べりのすぐ下に沿って)
                {
                    var a = GalleonPoint(L, W, H, k / 10f, 0.04f);
                    var b = GalleonPoint(L, W, H, (k + 1) / 10f, 0.04f);
                    DomeLine(hull, "GoldWale", new Vector3(s * (a.x + 0.012f), a.y, a.z), new Vector3(s * (b.x + 0.012f), b.y, b.z), 0.035f, gold);
                }
            }

            // 船尾楼 (一段高い船室。窓が光り、ランタン) と船首楼
            var stern = GalleonPoint(L, W, H, 0.08f, 0f);
            float castleH = H * 0.42f, castleLen = L * 0.2f;
            float deckY = GalleonDeckY(H, 0.1f);
            Prim(PrimitiveType.Cube, "SternCastle", hull, new Vector3(0f, deckY + castleH / 2f, -L / 2f + castleLen / 2f + 0.05f), new Vector3(stern.x * 1.85f, castleH, castleLen), hullLight);
            Prim(PrimitiveType.Cube, "SternCastleTrim", hull, new Vector3(0f, deckY + castleH + 0.02f, -L / 2f + castleLen / 2f + 0.05f), new Vector3(stern.x * 1.9f, 0.05f, castleLen + 0.04f), gold, false);
            for (int k = -1; k <= 1; k++)
                Prim(PrimitiveType.Cube, "SternWindow", hull, new Vector3(k * stern.x * 0.5f, deckY + castleH * 0.45f, -L / 2f + 0.04f), new Vector3(stern.x * 0.32f, castleH * 0.35f, 0.02f), lantern, false);
            Prim(PrimitiveType.Sphere, "SternLantern", hull, new Vector3(0f, deckY + castleH + 0.35f, -L / 2f + 0.15f), new Vector3(0.16f, 0.24f, 0.16f), lantern, false);
            var bowTop = GalleonPoint(L, W, H, 0.84f, 0f);
            Prim(PrimitiveType.Cube, "Forecastle", hull, new Vector3(0f, GalleonDeckY(H, 0.84f) + castleH * 0.3f, L * 0.31f), new Vector3(bowTop.x * 1.7f, castleH * 0.6f, L * 0.12f), hullLight);

            // バウスプリット (船首から斜め上へ) と三角帆 (横から見える)
            var tip = GalleonPoint(L, W, H, 1f, 0f);
            Vector3 spritFrom = new Vector3(0f, tip.y, tip.z - 0.1f);
            Vector3 spritTo = spritFrom + Quaternion.Euler(-22f, 0f, 0f) * Vector3.forward * (L * 0.32f);
            DomeLine(hull, "Bowsprit", spritFrom, spritTo, 0.07f, woodDark);
            float foreZ = -L / 2f + masts[masts.Length - 1] * L;
            float foreTop = GalleonDeckY(H, masts[masts.Length - 1]) + mastH * (masts.Length == 1 ? 1f : 0.85f) * 0.88f;
            var jib = MeshPart(hull, "Jib", TriSailMesh($"{key}_Jib", new Vector3(0f, foreTop, foreZ), spritTo, new Vector3(0f, GalleonDeckY(H, 0.9f) + 0.2f, L * 0.42f)), canvas);
            jib.gameObject.AddComponent<MeshCollider>().sharedMesh = jib.GetComponent<MeshFilter>().sharedMesh;

            // 帆柱・横帆・見張り台・ロープ。帆は少し斜めに張る (真横から見ても帆の形が分かる)
            int main = masts.Length / 2;
            Transform flagPole = null;
            for (int m = 0; m < masts.Length; m++)
            {
                float u = masts[m];
                bool isMain = m == main;
                float h = mastH * (isMain ? 1f : 0.85f);
                float z = -L / 2f + u * L;
                float baseY = GalleonDeckY(H, u);
                Prim(PrimitiveType.Cylinder, "Mast", hull, new Vector3(0f, baseY + h / 2f, z), new Vector3(0.11f, h / 2f, 0.11f), woodDark, false);
                float sailW = W * (isMain ? 2.1f : 1.8f);
                var rig = Group(hull, "Rig");
                rig.localPosition = new Vector3(0f, baseY, z);
                rig.localRotation = Quaternion.Euler(0f, (m % 2 == 0 ? 1f : -1f) * 28f, 0f); // 帆桁を斜めに
                // 下の大きな帆と上の帆 (それぞれ帆桁から下がる)
                foreach (var (lo, hi, wk) in new[] { (0.24f, 0.56f, 1f), (0.6f, 0.86f, 0.78f) })
                {
                    float sw = sailW * wk, sh = h * (hi - lo);
                    DomeLine(rig, "Yard", new Vector3(-sw * 0.55f, h * hi, 0.02f), new Vector3(sw * 0.55f, h * hi, 0.02f), 0.06f, woodDark);
                    var sail = MeshPart(rig, "Sail", SailMesh($"{key}_Sail_{(isMain ? "M" : "S")}_{wk}", sw, sh, sh * 0.18f), canvas);
                    sail.localPosition = new Vector3(0f, h * (lo + hi) / 2f, 0.06f);
                    sail.gameObject.AddComponent<BoxCollider>();
                }
                if (isMain) // 見張り台
                {
                    Prim(PrimitiveType.Cylinder, "CrowsNest", hull, new Vector3(0f, baseY + h * 0.9f, z), new Vector3(0.42f, 0.09f, 0.42f), woodDark, false);
                    flagPole = Group(hull, "FlagPole");
                    flagPole.localPosition = new Vector3(0f, baseY + h + 0.05f, z);
                }
                else // ほかの帆柱のてっぺんには赤い吹き流し
                {
                    var pen = Prim(PrimitiveType.Cube, "Pennant", hull, new Vector3(0f, baseY + h + 0.08f, z - 0.35f), new Vector3(0.02f, 0.12f, 0.7f), pennant, false);
                    pen.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
                }
                // ロープ (帆柱のてっぺんから両舷へ)
                var side = GalleonPoint(L, W, H, Mathf.Clamp01(u - 0.06f), 0f);
                foreach (float s in new[] { -1f, 1f })
                    DomeLine(hull, "Shroud", new Vector3(0f, baseY + h * 0.9f, z), new Vector3(s * side.x, side.y, side.z), 0.018f, rope);
            }

            // メインマストのてっぺんの大きな海賊旗 (どくろと点数)。いつもプレイヤーの方を向く
            var labels = new List<TextMesh>();
            var flag = Group(flagPole != null ? flagPole : hull, "PirateFlag");
            flag.gameObject.AddComponent<FaceCenter>();
            float flagH = flagW * 0.62f;
            Prim(PrimitiveType.Cube, "FlagCloth", flag, new Vector3(0f, flagH / 2f + 0.05f, 0f), new Vector3(flagW, flagH, 0.03f), flagMat);
            Prim(PrimitiveType.Cube, "FlagTrim", flag, new Vector3(0f, flagH / 2f + 0.05f, 0.005f), new Vector3(flagW + 0.08f, flagH + 0.08f, 0.02f), gold, false);
            Prim(PrimitiveType.Sphere, "Skull", flag, new Vector3(-flagW * 0.33f, flagH * 0.62f, -0.03f), new Vector3(flagH * 0.3f, flagH * 0.3f, 0.02f), bone, false);
            foreach (float a in new[] { 40f, -40f })
            {
                var b = Prim(PrimitiveType.Cube, "Bone", flag, new Vector3(-flagW * 0.33f, flagH * 0.4f, -0.028f), new Vector3(flagH * 0.42f, flagH * 0.06f, 0.015f), bone, false);
                b.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            }
            labels.Add(MakeText(flag, "Points", "300", new Vector3(flagW * 0.12f, flagH / 2f + 0.05f, -0.04f), flagH * 0.62f, numberColor));
            Prim(PrimitiveType.Cylinder, "FlagStaff", flag, new Vector3(-flagW / 2f - 0.05f, flagH / 2f, 0f), new Vector3(0.05f, flagH / 2f + 0.1f, 0.05f), woodDark, false);

            // 船全体を囲む見えない箱の当たり判定 (船体・帆柱・帆・旗をまとめて包む。帆と帆のすき間や、ロープの間も弾が抜けない)。
            // 船といっしょに揺れるように Body の子にする。旗はいつもプレイヤーの方を向いて回るので、前後にも旗の幅の分だけ広げておく
            var hitBounds = new Bounds();
            bool firstBounds = true;
            foreach (var mf in body.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    var local = body.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (firstBounds) { hitBounds = new Bounds(local, Vector3.zero); firstBounds = false; }
                    else hitBounds.Encapsulate(local);
                }
            }
            hitBounds.Expand(new Vector3(0.2f, 0.1f, 0.2f));
            hitBounds.Encapsulate(new Bounds(hitBounds.center, new Vector3(flagW + 0.4f, 0f, flagW + 0.4f)));
            var hitBox = new GameObject("HitBox").AddComponent<BoxCollider>();
            hitBox.transform.SetParent(body, false);
            hitBox.center = hitBounds.center;
            hitBox.size = hitBounds.size;

            // 船全体の当たり判定: 船の根元に Target を付ける (船体・帆・旗の当たり判定はこの子なので、どこに当たってもこの Target が撃たれる)。
            // 見た目は持たせない (撃たれても船は倒れず、EnemyShip が撃沈の演出をする)
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Normal;
            target.manualControl = true;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);

            var ship = root.gameObject.AddComponent<EnemyShip>();
            ship.body = body;
            ship.hitTarget = target;
            ship.pointLabels = labels.ToArray();
            ship.explosionMaterial = explosion;
            switch (type)
            {
                case ShipType.Sloop:
                    ship.displayName = "PIRATE SLOOP"; ship.pointChoices = new[] { 100, 200, 300 }; ship.speed = 3.2f; ship.distance = new Vector2(9f, 12f);
                    ship.routeChoices = new[] { EnemyShip.Route.Cross, EnemyShip.Route.Cross, EnemyShip.Route.Orbit };
                    ship.bobHeight = 0.24f; ship.rollDeg = 9f; ship.pitchDeg = 5f;
                    break;
                case ShipType.Frigate:
                    ship.displayName = "PIRATE FRIGATE"; ship.pointChoices = new[] { 300, 500 }; ship.speed = 2.4f; ship.distance = new Vector2(11f, 14f);
                    ship.routeChoices = new[] { EnemyShip.Route.Cross, EnemyShip.Route.Orbit };
                    ship.bobHeight = 0.2f; ship.rollDeg = 7f; ship.pitchDeg = 4f;
                    break;
                default:
                    ship.displayName = "PIRATE FLAGSHIP"; ship.pointChoices = new[] { 1000 }; ship.speed = 1.6f; ship.distance = new Vector2(13f, 15f);
                    ship.routeChoices = new[] { EnemyShip.Route.Approach };
                    ship.bobHeight = 0.15f; ship.rollDeg = 5f; ship.pitchDeg = 3f;
                    break;
            }
            return ship;
        }

        // ---- 帆船の船体の形 (船尾 u = 0 〜 船首 u = 1。y = 0 が船べりの基準、x は片側の幅) ----

        // 船の横幅の割合: 船尾は少しすぼまり、真ん中は太く、船首へ向かって丸くとがる
        static float GalleonHalfWidth(float u)
        {
            if (u < 0.12f) return Mathf.Lerp(0.8f, 1f, u / 0.12f);
            if (u > 0.55f) return Mathf.Max(0.02f, 1f - Mathf.Pow((u - 0.55f) / 0.45f, 1.8f));
            return 1f;
        }

        // 船べりの高さ: 船首と船尾が反り上がる (船尾のほうが高い)
        static float GalleonTop(float H, float u) => H * (0.14f * (2f * u - 1f) * (2f * u - 1f) + (u < 0.5f ? 0.1f * (1f - 2f * u) : 0f));

        // 甲板の高さ (船べりより少し下)
        static float GalleonDeckY(float H, float u) => GalleonTop(H, u) - H * 0.2f;

        // 船底 (竜骨) の深さ: 船首に向かって持ち上がる
        static float GalleonKeel(float H, float u) => -H * (u > 0.75f ? Mathf.Lerp(1f, 0.45f, (u - 0.75f) / 0.25f) : 1f);

        // u の所の、断面の s (0 = 船べり 〜 1 = 竜骨) の点 (右舷。x は正)
        static Vector3 GalleonPoint(float L, float W, float H, float u, float s)
        {
            float half = W / 2f * GalleonHalfWidth(u);
            // 船べりの近くは太く、竜骨へ丸くすぼまる (s = 1 で cos が float の誤差でわずかに負になり Pow が NaN になるので 0 で止める)
            float f = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(s * Mathf.PI / 2f)), 0.6f);
            return new Vector3(half * f, Mathf.Lerp(GalleonTop(H, u), GalleonKeel(H, u), s), -L / 2f + u * L);
        }

        /// <summary>帆船の船体のメッシュ。part: 0 = 下の濃い板 / 1 = 上の明るい板 (内側も見える) / 2 = 甲板 / 3 = 船尾の板</summary>
        Mesh GalleonHullMesh(string key, float L, float W, float H, int part)
        {
            string name = $"{key}_Hull{part}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int Stations = 16;
            float[] ss = part == 1 ? new[] { 0f, 0.22f } : new[] { 0.22f, 0.45f, 0.7f, 0.88f, 1f };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            if (part <= 1)
            {
                for (int i = 0; i < Stations; i++)
                {
                    float u0 = i / (float)Stations, u1 = (i + 1) / (float)Stations;
                    for (int j = 0; j < ss.Length - 1; j++)
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Vector3 P(float u, float s) { var p = GalleonPoint(L, W, H, u, s); p.x *= side; return p; }
                            Vector3 a = P(u0, ss[j]), b = P(u1, ss[j]), c = P(u1, ss[j + 1]), d = P(u0, ss[j + 1]);
                            float midY = (GalleonTop(H, u0) + GalleonKeel(H, u0)) / 2f;
                            var outward = new Vector3((a.x + c.x) / 2f, (a.y + c.y) / 2f - midY, u0 > 0.55f ? 0.6f : 0f);
                            AddTri(verts, tris, a, b, c, outward);
                            AddTri(verts, tris, a, c, d, outward);
                            if (part == 1) // 船べりの内側 (甲板から見える)
                            {
                                AddTri(verts, tris, a, b, c, -outward);
                                AddTri(verts, tris, a, c, d, -outward);
                            }
                        }
                }
            }
            else if (part == 2)
            {
                for (int i = 0; i < Stations; i++)
                {
                    float u0 = i / (float)Stations, u1 = (i + 1) / (float)Stations;
                    Vector3 D(float u, float side)
                    {
                        var p = GalleonPoint(L, W, H, u, 0.12f);
                        return new Vector3(p.x * side, GalleonDeckY(H, u), p.z);
                    }
                    AddTri(verts, tris, D(u0, -1f), D(u1, -1f), D(u1, 1f), Vector3.up);
                    AddTri(verts, tris, D(u0, -1f), D(u1, 1f), D(u0, 1f), Vector3.up);
                }
            }
            else
            {
                // 船尾の板: 船尾の断面をふさぐ
                var all = new[] { 0f, 0.22f, 0.45f, 0.7f, 0.88f, 1f };
                var center = new Vector3(0f, (GalleonTop(H, 0f) + GalleonKeel(H, 0f)) / 2f, -L / 2f);
                for (int j = 0; j < all.Length - 1; j++)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var a = GalleonPoint(L, W, H, 0f, all[j]); a.x *= side;
                        var b = GalleonPoint(L, W, H, 0f, all[j + 1]); b.x *= side;
                        AddTri(verts, tris, center, a, b, Vector3.back);
                    }
                var t0 = GalleonPoint(L, W, H, 0f, 0f);
                AddTri(verts, tris, center, t0, new Vector3(-t0.x, t0.y, t0.z), Vector3.back);
            }
            return FinishMesh(name, verts, tris);
        }

        /// <summary>風をはらんでふくらんだ四角い帆 (幅 w・高さ h。真ん中が前 (+Z) へ bulge だけふくらむ。原点が真ん中。表裏とも見える)</summary>
        Mesh SailMesh(string name, float w, float h, float bulge)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int NX = 6, NY = 4;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            Vector3 P(int i, int j)
            {
                float x = (i / (float)NX - 0.5f) * w, y = (j / (float)NY - 0.5f) * h;
                float fx = 2f * x / w, fy = 2f * y / h;
                return new Vector3(x * (1f - 0.12f * (0.5f - j / (float)NY)), y, bulge * (1f - fx * fx) * (1f - 0.6f * fy * fy)); // 下の辺は少し広い
            }
            for (int i = 0; i < NX; i++)
                for (int j = 0; j < NY; j++)
                {
                    Vector3 a = P(i, j), b = P(i + 1, j), c = P(i + 1, j + 1), d = P(i, j + 1);
                    AddTri(verts, tris, a, b, c, Vector3.forward);
                    AddTri(verts, tris, a, c, d, Vector3.forward);
                    AddTri(verts, tris, a, b, c, Vector3.back);
                    AddTri(verts, tris, a, c, d, Vector3.back);
                }
            return FinishMesh(name, verts, tris);
        }

        /// <summary>三角の帆 (3つの角の位置。少しふくらませる。表裏とも見える)</summary>
        Mesh TriSailMesh(string name, Vector3 top, Vector3 front, Vector3 foot)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var mid = (top + front + foot) / 3f + Vector3.right * 0.12f; // 風をはらんで横へふくらむ
            foreach (var (a, b) in new[] { (top, front), (front, foot), (foot, top) })
            {
                AddTri(verts, tris, a, b, mid, Vector3.right);
                AddTri(verts, tris, a, b, mid, Vector3.left);
            }
            return FinishMesh(name, verts, tris);
        }

        /// <summary>海に浮かぶ島: 砂浜・緑の丘・ヤシの木・岩・小さな小屋 (飾り。当たり判定なし)。原点が海面の島の真ん中</summary>
        void BuildIsland(Transform island, float size, int variant)
        {
            var sand = Mat("Sand", new Color(0.75f, 0.65f, 0.45f));
            var grass = Mat("Island_Grass", new Color(0.22f, 0.5f, 0.2f), null, false, 0.1f);
            var rock = Mat("Rock", new Color(0.3f, 0.3f, 0.33f), null, false, 0.2f);
            var trunk = Mat("Trunk", new Color(0.45f, 0.3f, 0.15f));
            var palm = Mat("Palm", new Color(0.15f, 0.5f, 0.2f));
            var thatch = Mat("Island_Thatch", new Color(0.7f, 0.58f, 0.32f));
            Prim(PrimitiveType.Sphere, "Beach", island, new Vector3(0f, -size * 0.12f, 0f), new Vector3(size * 2.4f, size * 0.4f, size * 1.7f), sand, false);
            Prim(PrimitiveType.Sphere, "Hill", island, new Vector3(size * 0.15f, size * 0.05f, 0f), new Vector3(size * 1.4f, size * 0.75f, size * 1.1f), grass, false);
            for (int k = 0; k < 3; k++)
                Prim(PrimitiveType.Sphere, "IslandRock", island, new Vector3(Random.Range(-1f, 1f) * size, 0f, Random.Range(-0.8f, 0.8f) * size),
                     new Vector3(Random.Range(0.4f, 0.9f), Random.Range(0.3f, 0.6f), Random.Range(0.4f, 0.8f)) * size * 0.4f, rock, false);
            int palms = 2 + variant % 3;
            for (int k = 0; k < palms; k++)
            {
                var p = Group(island, "Palm");
                p.localPosition = new Vector3(Random.Range(-0.8f, 0.6f) * size, size * 0.1f, Random.Range(-0.5f, 0.5f) * size);
                float lean = Random.Range(-18f, 18f), h = Random.Range(1.8f, 2.8f);
                // 少し曲がった幹 (4つの節)
                Vector3 pos = Vector3.zero;
                for (int s = 0; s < 4; s++)
                {
                    var dir = Quaternion.Euler(lean * (s + 1) / 4f, 0f, lean * 0.5f * (s + 1) / 4f) * Vector3.up;
                    DomeLine(p, "PalmTrunk", pos, pos + dir * h / 4f, 0.16f - s * 0.02f, trunk);
                    pos += dir * h / 4f;
                }
                for (int l = 0; l < 6; l++) // 垂れ下がる葉
                {
                    var leaf = Prim(PrimitiveType.Cube, "PalmLeaf", p, pos, new Vector3(0.22f, 0.03f, 1.3f), palm, false);
                    leaf.transform.localRotation = Quaternion.Euler(28f, l * 60f + Random.Range(-10f, 10f), 0f);
                    leaf.transform.localPosition = pos + leaf.transform.localRotation * Vector3.forward * 0.55f;
                }
            }
            if (variant % 2 == 0) // 小さな小屋
            {
                var hut = Group(island, "Hut");
                hut.localPosition = new Vector3(-size * 0.4f, size * 0.15f, size * 0.25f);
                Prim(PrimitiveType.Cube, "HutWall", hut, new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.7f, 0.8f), Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f)), false);
                Prim(PrimitiveType.Cylinder, "HutRoof", hut, new Vector3(0f, 0.95f, 0f), new Vector3(1.3f, 0.3f, 1.2f), thatch, false);
            }
        }

        /// <summary>飛び跳ねるイルカ (飾り)。原点が海面、+Z が頭の向き (ベルトの中で、船と同じ前を向くように回して置く)</summary>
        void BuildDolphin(Transform parent, string name, Vector3 pos, Material splash)
        {
            var root = Group(parent, name);
            root.localPosition = pos;
            root.localRotation = Quaternion.Euler(0f, -90f, 0f); // ベルトのローカル -X (= 船の前) を向く
            var skin = Mat("Dolphin_Skin", new Color(0.42f, 0.52f, 0.62f), null, false, 0.6f);
            var belly = Mat("Dolphin_Belly", new Color(0.86f, 0.88f, 0.9f), null, false, 0.5f);
            var body = Group(root, "Body");
            Prim(PrimitiveType.Capsule, "DolphinBody", body, Vector3.zero, new Vector3(0.36f, 0.62f, 0.4f), skin, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Capsule, "DolphinBelly", body, new Vector3(0f, -0.06f, 0.05f), new Vector3(0.3f, 0.5f, 0.3f), belly, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Capsule, "DolphinBeak", body, new Vector3(0f, -0.04f, 0.68f), new Vector3(0.09f, 0.14f, 0.09f), skin, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var fin = Prim(PrimitiveType.Cube, "DolphinFin", body, new Vector3(0f, 0.22f, -0.05f), new Vector3(0.04f, 0.26f, 0.2f), skin, false);
            fin.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                var flipper = Prim(PrimitiveType.Cube, "DolphinFlipper", body, new Vector3(s * 0.18f, -0.1f, 0.2f), new Vector3(0.22f, 0.03f, 0.12f), skin, false);
                flipper.transform.localRotation = Quaternion.Euler(0f, s * 25f, s * -25f);
                var fluke = Prim(PrimitiveType.Cube, "DolphinFluke", body, new Vector3(s * 0.14f, 0f, -0.68f), new Vector3(0.26f, 0.03f, 0.14f), skin, false);
                fluke.transform.localRotation = Quaternion.Euler(0f, s * -20f, 0f);
            }
            var d = root.gameObject.AddComponent<Dolphin>();
            d.body = body;
            d.splashMaterial = splash;
            body.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // 錨の的の見た目
        // ------------------------------------------------------------------

        /// <summary>錨の的: 紺の板に金の錨と「SEA BATTLE」の文字。まわりを白い波の輪が回る</summary>
        GameObject NavalLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Naval_Plate", new Color(0.05f, 0.15f, 0.4f), new Color(0.02f, 0.06f, 0.2f)));
            // 錨: 縦棒・横棒・下の弧・上の輪
            Prim(PrimitiveType.Cube, "Shank", look, c + new Vector3(0f, 0.02f, -0.024f), new Vector3(0.022f, 0.15f, 0.008f), gold, false);
            Prim(PrimitiveType.Cube, "Stock", look, c + new Vector3(0f, 0.075f, -0.024f), new Vector3(0.1f, 0.018f, 0.008f), gold, false);
            for (int k = -3; k <= 3; k++)
            {
                float a = k * 18f * Mathf.Deg2Rad;
                var seg = Prim(PrimitiveType.Cube, "Arm", look, c + new Vector3(Mathf.Sin(a) * 0.075f, -0.035f - Mathf.Cos(a) * 0.03f, -0.024f), new Vector3(0.026f, 0.016f, 0.008f), gold, false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, k * 18f);
            }
            var ring = Group(look, "AnchorRing");
            ring.localPosition = c + new Vector3(0f, 0.11f, -0.024f);
            RingSegments(ring, "Ring", 0.022f, 10, 0.008f, gold);
            MakeText(look, "Label", "SEA BATTLE", c + new Vector3(0f, -0.115f, -0.025f), 0.035f, Color.white);

            var waves = Group(look, "Waves");
            waves.localPosition = c + Vector3.back * 0.03f;
            var spin = waves.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.forward;
            spin.speed = -90f;
            var foam = Mat("Foam", new Color(0.85f, 0.92f, 1f), new Color(0.15f, 0.2f, 0.28f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8;
                Prim(PrimitiveType.Sphere, "Drop", waves, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.2f, Vector3.one * (i % 2 == 0 ? 0.03f : 0.02f), foam, false);
            }
            return look.gameObject;
        }
    }
}
