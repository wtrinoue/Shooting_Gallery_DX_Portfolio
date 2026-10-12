using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 海戦モード (錨の的を撃つと始まる) の生成: 自分の船の甲板・海面・後ろへ流れる景色・敵の船・錨の的の見た目。
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
        /// 海賊船。黒い帆に金色の大きな点数。船体と帆のどこにバズーカを当てても、帆の点数が入って撃沈する (小さな的は付いていない)。
        /// 帆はいつもプレイヤーの方を向くので、船がどちらへ走っていても点数が読める。種類ごとに大きさ・点数・速さが違う。
        /// </summary>
        EnemyShip BuildEnemyShip(Transform parent, string name, ShipType type, Material explosion)
        {
            var root = Group(parent, name);
            root.gameObject.SetActive(false);
            var body = Group(root, "Body");

            var hullMat = Mat("Pirate_Hull", new Color(0.16f, 0.09f, 0.06f), null, false, 0.4f);
            var woodDark = Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var sailMat = Mat("Pirate_Sail", new Color(0.08f, 0.08f, 0.09f));
            var port = Mat("Pirate_Port", new Color(0.02f, 0.02f, 0.02f));
            var lantern = Mat("Ship_Lantern", new Color(1f, 0.75f, 0.35f), new Color(2f, 1.2f, 0.4f));
            var numberColor = new Color(1f, 0.82f, 0.3f);

            float length, width, height, mastH;
            float[] masts;
            switch (type)
            {
                case ShipType.Sloop: length = 2.2f; width = 0.75f; height = 0.6f; mastH = 2.4f; masts = new[] { 0.2f }; break;
                case ShipType.Frigate: length = 3.4f; width = 1.0f; height = 0.8f; mastH = 3.0f; masts = new[] { -0.8f, 0.8f }; break;
                default: length = 4.8f; width = 1.4f; height = 1.0f; mastH = 3.6f; masts = new[] { -1.5f, 0f, 1.5f }; break;
            }
            float deckY = height - 0.3f; // 船体の底は海の中

            // 船体 (当たり判定あり)
            Prim(PrimitiveType.Cube, "Hull", body, new Vector3(0f, deckY - height / 2f, 0f), new Vector3(width, height, length), hullMat);
            var bow = Prim(PrimitiveType.Cube, "Bow", body, new Vector3(0f, deckY - height / 2f, length / 2f), new Vector3(width * 0.707f, height, width * 0.707f), hullMat);
            bow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "GoldBand", body, new Vector3(s * (width / 2f + 0.005f), deckY - 0.08f, 0f), new Vector3(0.012f, 0.05f, length), gold, false);
                int ports = Mathf.RoundToInt(length / 0.6f);
                for (int k = 0; k < ports; k++)
                    Prim(PrimitiveType.Cube, "GunPort", body, new Vector3(s * (width / 2f + 0.006f), deckY - height * 0.4f, -length / 2f + (k + 0.5f) * length / ports),
                         new Vector3(0.012f, 0.12f, 0.14f), port, false);
            }
            if (type != ShipType.Sloop)
            {
                // 船尾楼 (一段高い船室) とランタン
                Prim(PrimitiveType.Cube, "SternCastle", body, new Vector3(0f, deckY + 0.25f, -length / 2f + 0.45f), new Vector3(width, 0.5f, 0.9f), hullMat);
                Prim(PrimitiveType.Cube, "SternTrim", body, new Vector3(0f, deckY + 0.52f, -length / 2f + 0.45f), new Vector3(width + 0.04f, 0.04f, 0.94f), gold, false);
                Prim(PrimitiveType.Sphere, "SternLantern", body, new Vector3(0f, deckY + 0.75f, -length / 2f + 0.05f), new Vector3(0.14f, 0.2f, 0.14f), lantern, false);
            }

            // 帆柱と黒い帆 (当たり判定あり)。帆はいつもプレイヤーの方を向き、真ん中の帆に大きく点数を書く
            var labels = new List<TextMesh>();
            int main = masts.Length / 2;
            for (int m = 0; m < masts.Length; m++)
            {
                float z = masts[m];
                bool isMain = m == main;
                float h = mastH * (isMain ? 1f : 0.82f);
                Prim(PrimitiveType.Cylinder, "Mast", body, new Vector3(0f, deckY + h / 2f, z), new Vector3(0.08f, h / 2f, 0.08f), woodDark, false);
                var sailRoot = Group(body, "Sail");
                sailRoot.localPosition = new Vector3(0f, deckY + h * 0.58f, z);
                sailRoot.gameObject.AddComponent<FaceCenter>();
                float sailW = Mathf.Max(1.3f, width * 1.6f) * (isMain ? 1f : 0.8f), sailH = h * 0.62f;
                Prim(PrimitiveType.Cube, "SailCloth", sailRoot, Vector3.zero, new Vector3(sailW, sailH, 0.04f), sailMat);
                Prim(PrimitiveType.Cube, "SailTrim", sailRoot, new Vector3(0f, sailH / 2f, -0.025f), new Vector3(sailW, 0.04f, 0.01f), gold, false);
                Prim(PrimitiveType.Cube, "SailTrim", sailRoot, new Vector3(0f, -sailH / 2f, -0.025f), new Vector3(sailW, 0.04f, 0.01f), gold, false);
                if (isMain)
                    labels.Add(MakeText(sailRoot, "Points", "300", new Vector3(0f, 0f, -0.03f), sailH * 0.5f, numberColor));
                else
                {
                    var skull = Prim(PrimitiveType.Cylinder, "Skull", sailRoot, new Vector3(0f, 0f, -0.025f), new Vector3(sailH * 0.4f, 0.004f, sailH * 0.4f), Mat("Target_White", Color.white), false);
                    skull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                Prim(PrimitiveType.Cube, "Flag", body, new Vector3(0.18f, deckY + h + 0.05f, z), new Vector3(0.36f, 0.22f, 0.01f), sailMat, false);
            }

            // 船全体の当たり判定: 船の根元に Target を付ける (船体と帆の当たり判定はこの子なので、どこに当たってもこの Target が撃たれる)。
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
                    ship.displayName = "PIRATE SLOOP"; ship.pointChoices = new[] { 100, 200, 300 }; ship.speed = 3.2f; ship.distance = new Vector2(8f, 11f);
                    ship.routeChoices = new[] { EnemyShip.Route.Cross, EnemyShip.Route.Cross, EnemyShip.Route.Orbit };
                    break;
                case ShipType.Frigate:
                    ship.displayName = "PIRATE FRIGATE"; ship.pointChoices = new[] { 300, 500 }; ship.speed = 2.4f; ship.distance = new Vector2(10f, 13f);
                    ship.routeChoices = new[] { EnemyShip.Route.Cross, EnemyShip.Route.Orbit };
                    break;
                default:
                    ship.displayName = "PIRATE FLAGSHIP"; ship.pointChoices = new[] { 1000 }; ship.speed = 1.6f; ship.distance = new Vector2(12f, 14f);
                    ship.routeChoices = new[] { EnemyShip.Route.Approach };
                    ship.bobHeight = 0.05f; ship.rollDeg = 2.5f;
                    break;
            }
            return ship;
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
