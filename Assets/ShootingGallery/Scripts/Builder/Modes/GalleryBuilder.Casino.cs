using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// カジノモード (カジノの的を撃つと始まる) の生成: 地味な円柱の壁と天井、明かりが点くと現れる豪華な内装
    /// (赤いビロードと金の壁・ネオン・シャンデリア・じゅうたん・大きなサイコロとチップ・カードのテーブル・ルーレット台・小さなスロット・ロープの柵)、
    /// 壇の上のスロットマシン、壁のダーツボード (GalleryBuilder.CasinoDeluxe.cs)、カジノの的の見た目。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float CasinoR = 7.2f;   // 円柱の内側の半径 (屋台6軒の輪が入る)
        const float CasinoH = 9f;     // 天井の高さ (普通の3倍ほど)
        const int CasinoPanels = 28;  // 壁の板の数
        public const string CasinoBgmPath = "Assets/Source/casino_stage/maou_bgm_acoustic01b.mp3"; // カジノモードの BGM (魔王魂)

        CasinoHall BuildCasino()
        {
            var go = new GameObject("CasinoHall");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;

            var plain = Mat("Casino_Plain", new Color(0.55f, 0.55f, 0.56f));
            var plainDark = Mat("Casino_PlainDark", new Color(0.4f, 0.4f, 0.42f));

            // ---- 地味な外側: 灰色の壁の板・天井・床 ----
            var panels = new Transform[CasinoPanels];
            float pw = 2f * Mathf.PI * CasinoR / CasinoPanels + 0.06f;
            for (int i = 0; i < CasinoPanels; i++)
            {
                float a = i * 360f / CasinoPanels;
                var p = Group(root, $"WallPanel_{i}");
                var rot = Quaternion.Euler(0f, a, 0f);
                p.localPosition = rot * new Vector3(0f, 0f, CasinoR + 0.15f);
                p.localRotation = rot; // 表 (-Z) が中心を向く
                Prim(PrimitiveType.Cube, "Panel", p, new Vector3(0f, CasinoH / 2f, 0f), new Vector3(pw, CasinoH, 0.3f), plain);
                Prim(PrimitiveType.Cube, "Joint", p, new Vector3(pw / 2f, CasinoH / 2f, -0.16f), new Vector3(0.04f, CasinoH, 0.02f), plainDark, false);
                panels[i] = p;
            }
            var ceiling = Group(root, "Ceiling");
            ceiling.localPosition = new Vector3(0f, CasinoH, 0f);
            Prim(PrimitiveType.Cylinder, "CeilingDisc", ceiling, new Vector3(0f, 0.15f, 0f), new Vector3(2f * CasinoR + 1f, 0.15f, 2f * CasinoR + 1f), plain);
            var floor = Prim(PrimitiveType.Cylinder, "PlainFloor", root, new Vector3(0f, 0.015f, 0f), new Vector3(2f * CasinoR + 0.2f, 0.015f, 2f * CasinoR + 0.2f), plainDark, false);

            // ---- 豪華な内側 (明かりが点いた時に現れる) ----
            var luxury = Group(root, "Luxury");
            BuildCasinoLuxury(luxury);

            // 暗闇の中で1つずつ現れる: スポットライトに照らされたゲームを、ぐるりと四方八方に (正面は大きなスロットの壇)。
            // 壁のダーツボード・斜めのボールのルーレット・壁ぎわの小さなスロット。どれも上に「SHOOT!」の目印が浮かぶ (撃てる時だけ)
            var steps = new List<GameObject>();
            var wheels = new List<RouletteWheel>();
            var sideSlots = new List<SlotMachine>();
            var roulettes = new List<BallRoulette>();
            var games = new (float angle, char kind)[] { (55f, 'D'), (100f, 'R'), (145f, 'S'), (180f, 'S'), (215f, 'S'), (260f, 'R'), (305f, 'D') };
            for (int i = 0; i < games.Length; i++)
            {
                var (angle, kind) = games[i];
                var step = Group(root, $"Reveal_{i}");
                var rot = Quaternion.Euler(0f, angle, 0f);
                step.localRotation = rot; // 表 (-Z) が真ん中を向く
                switch (kind)
                {
                    case 'D': // 壁のダーツボード
                    {
                        step.localPosition = rot * new Vector3(0f, 0f, CasinoR - 0.25f);
                        var board = BuildDartboard(step, new Vector3(0f, 3.4f, 0f), i);
                        wheels.Add(board);
                        SpotlightGlow(step, new Vector3(0f, 3.4f, 0.05f), 1.6f);
                        CasinoShootHint(step, new Vector3(0f, 4.75f, -0.3f), board.target, null, null);
                        break;
                    }
                    case 'R': // 斜めのボールのルーレット
                    {
                        step.localPosition = rot * new Vector3(0f, 0f, CasinoR - 1.6f);
                        var roulette = BuildBallRoulette(step);
                        roulettes.Add(roulette);
                        SpotFixture(step, new Vector3(0f, CasinoH - 1.2f, -2.4f), new Vector3(0f, 1.4f, 0f), 1.3f);
                        CasinoShootHint(step, new Vector3(0f, 2.75f, 0f), roulette.target, null, roulette);
                        break;
                    }
                    default: // 壁ぎわの小さなスロット
                    {
                        step.localPosition = rot * new Vector3(0f, 0f, CasinoR - 1.05f);
                        var machine = BuildSlotMachine(step, Vector3.zero, 0.75f);
                        sideSlots.Add(machine);
                        SpotFixture(step, new Vector3(0f, CasinoH - 1.2f, -2.4f), new Vector3(0f, 1.6f, -0.5f), 1.4f);
                        CasinoShootHint(machine.leverHint, Vector3.zero, machine.target, machine, null, 1.1f); // (レバーの玉を指す)
                        break;
                    }
                }
                step.gameObject.SetActive(false);
                steps.Add(step.gameObject);
            }

            // 最後に現れる主役: 正面の壇と大きなスロットマシン
            var finale = Group(root, "Reveal_Slot");
            finale.localPosition = new Vector3(0f, 0f, 4.6f);
            var slot = BuildSlotStage(finale);
            SpotFixture(finale, new Vector3(0f, CasinoH - 1.2f, -2.6f), new Vector3(0f, 2.4f, -0.7f), 1.9f); // 暗闇の中でスロットだけを照らす
            CasinoShootHint(slot.leverHint, Vector3.zero, slot.target, slot, null, 0.9f); // (レバーの玉を指す)
            finale.gameObject.SetActive(false);

            var hall = go.AddComponent<CasinoHall>();
            hall.wallPanels = panels;
            hall.ceiling = ceiling;
            hall.showDuringStage = new[] { floor };
            hall.revealSteps = steps.ToArray();
            hall.revealFinale = finale.gameObject;
            hall.luxury = luxury.gameObject;
            hall.slot = slot;
            hall.sideSlots = sideSlots.ToArray();
            hall.roulettes = roulettes.ToArray();
            hall.wheels = wheels.ToArray();
            hall.coinMaterial = Mat("Casino_Coin", new Color(1f, 0.8f, 0.25f), new Color(0.6f, 0.42f, 0.08f), false, 0.85f);
            hall.revealInterval = 0.35f; // ゲームが1つずつ現れる間隔 (7つ + スロット。ドラムロールの長さ 3.6 秒に合わせる)
            hall.bgmClip = LoadSourceAsset(CasinoBgmPath, a => a.casinoBgm); // BGM (実機では GalleryRuntimeAssets から。実機には UnityEditor がないので)
            // 明かりが点く時の演出: ネオンがチカチカと点き、広間の明かりがふっと強く点いて落ち着き、紙吹雪が舞う
            var neon = new List<Renderer>();
            foreach (var r in luxury.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Neon")) neon.Add(r);
            hall.neon = neon.ToArray();
            hall.hallLights = luxury.GetComponentsInChildren<Light>(true);
            hall.confettiMaterials = new[]
            {
                Mat("Confetti_Gold", new Color(1f, 0.82f, 0.3f), new Color(0.6f, 0.45f, 0.1f), true), Mat("Confetti_DeepGold", new Color(0.85f, 0.6f, 0.2f), null, true),
                Mat("Confetti_White", new Color(1f, 0.97f, 0.9f), null, true), Mat("Confetti_Red", new Color(0.8f, 0.08f, 0.12f), null, true)
            };
            // カジノはカジノモードの時だけ出る (シーンに生成した時も隠しておく)
            foreach (var p in panels) p.gameObject.SetActive(false);
            ceiling.gameObject.SetActive(false);
            floor.SetActive(false);
            luxury.gameObject.SetActive(false);
            go.SetActive(true);
            return hall;
        }

        /// <summary>
        /// 豪華な内装 (赤を基調にした、落ち着いた高級な部屋): 壁に深紅のビロードとマホガニーの腰板・金の柱・金の帯、上の方に金色と深紅の間接照明の輪、
        /// 壁の金の燭台、正面の壁に金文字の「CASINO」の看板と左右の深紅のカーテン、天井の金の飾りとシャンデリア (明かり)、
        /// 深紅のじゅうたんに金の輪の模様、壁ぎわの大きなサイコロとチップの山。
        /// </summary>
        void BuildCasinoLuxury(Transform lux)
        {
            var velvet = Mat("Casino_Velvet", new Color(0.36f, 0.03f, 0.06f), new Color(0.05f, 0f, 0.01f), false, 0.3f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var neonPink = Mat("Neon_Amber", new Color(1f, 0.72f, 0.3f), new Color(1.5f, 0.85f, 0.25f), true); // 金色の間接照明 (派手なネオンの色にはしない)
            var neonCyan = Mat("Neon_Ruby", new Color(0.85f, 0.1f, 0.14f), new Color(1.1f, 0.08f, 0.12f), true); // 深紅の間接照明
            var carpet = Mat("Casino_Carpet", new Color(0.3f, 0.025f, 0.05f), null, false, 0.15f);
            var mahogany = Mat("Casino_Mahogany", new Color(0.2f, 0.05f, 0.03f), null, false, 0.7f);

            float pw = 2f * Mathf.PI * CasinoR / CasinoPanels + 0.06f;
            for (int i = 0; i < CasinoPanels; i++)
            {
                var rot = Quaternion.Euler(0f, i * 360f / CasinoPanels, 0f);
                var p = Group(lux, "LuxPanel");
                p.localPosition = rot * new Vector3(0f, 0f, CasinoR - 0.02f);
                p.localRotation = rot;
                Prim(PrimitiveType.Cube, "Velvet", p, new Vector3(0f, 3.8f, 0f), new Vector3(pw, 7.2f, 0.05f), velvet, false);
                Prim(PrimitiveType.Cube, "Pilaster", p, new Vector3(pw / 2f, 3.8f, -0.06f), new Vector3(0.14f, 7.4f, 0.08f), gold, false);
                Prim(PrimitiveType.Cube, "Wainscot", p, new Vector3(0f, 0.6f, -0.03f), new Vector3(pw, 1.2f, 0.06f), mahogany, false); // マホガニーの腰板
                Prim(PrimitiveType.Cube, "WainscotPanel", p, new Vector3(0f, 0.62f, -0.065f), new Vector3(pw * 0.62f, 0.75f, 0.01f), velvet, false);
            }
            // 壁の金の燭台 (光る球だけ。明かりは使わない)。ダーツボードの間の高い所に
            var sconceBulb = Mat("Sconce_Bulb", new Color(1f, 0.85f, 0.6f), new Color(2.2f, 1.5f, 0.7f), true);
            foreach (float a in new[] { 78f, 130f, 180f, 230f, 282f })
            {
                var rot = Quaternion.Euler(0f, a, 0f);
                var sc = Group(lux, "Sconce");
                sc.localPosition = rot * new Vector3(0f, 5.3f, CasinoR - 0.1f);
                sc.localRotation = rot;
                Prim(PrimitiveType.Cube, "SconcePlate", sc, new Vector3(0f, 0f, -0.01f), new Vector3(0.22f, 0.4f, 0.03f), gold, false);
                foreach (float x in new[] { -0.2f, 0f, 0.2f })
                {
                    float up = x == 0f ? 0.12f : 0f;
                    Prim(PrimitiveType.Cube, "SconceArm", sc, new Vector3(x / 2f, -0.05f, -0.12f), new Vector3(Mathf.Abs(x) + 0.03f, 0.03f, 0.03f), gold, false);
                    Prim(PrimitiveType.Cylinder, "SconceCup", sc, new Vector3(x, 0.02f + up, -0.15f), new Vector3(0.09f, 0.04f, 0.09f), gold, false);
                    Prim(PrimitiveType.Sphere, "SconceBulb", sc, new Vector3(x, 0.13f + up, -0.15f), new Vector3(0.08f, 0.13f, 0.08f), sconceBulb, false);
                }
            }
            foreach (float y in new[] { 1.22f, 7.4f })
                FlatRingVertical(lux, "GoldBand", CasinoR - 0.08f, y, 0.12f, gold);
            FlatRingVertical(lux, "NeonPink", CasinoR - 0.12f, 7.9f, 0.08f, neonPink);
            FlatRingVertical(lux, "NeonCyan", CasinoR - 0.12f, 8.4f, 0.08f, neonCyan);

            // 正面の壁の「CASINO」のネオン (スロットの上)
            var sign = Group(lux, "CasinoSign");
            sign.localPosition = new Vector3(0f, 6.4f, CasinoR - 0.15f);
            Prim(PrimitiveType.Cube, "SignFrame", sign, new Vector3(0f, -0.25f, 0.01f), new Vector3(4.8f, 1.9f, 0.04f), gold, false);
            Prim(PrimitiveType.Cube, "SignBack", sign, new Vector3(0f, -0.25f, -0.01f), new Vector3(4.6f, 1.7f, 0.04f), mahogany, false);
            MakeText(sign, "Neon", "CASINO", new Vector3(0f, 0f, -0.05f), 0.9f, new Color(1f, 0.8f, 0.4f));
            MakeText(sign, "Neon2", "- JACKPOT 777 -", new Vector3(0f, -0.75f, -0.05f), 0.3f, new Color(1f, 0.92f, 0.78f));

            // 看板の左右の深紅のカーテン (ひだ) と金のタッセル
            var drape = Mat("Casino_Drape", new Color(0.42f, 0.03f, 0.07f), new Color(0.04f, 0f, 0.01f), false, 0.45f);
            foreach (float s in new[] { -1f, 1f })
            {
                var rot = Quaternion.Euler(0f, s * 25f, 0f);
                var cur = Group(lux, "Curtain");
                cur.localPosition = rot * new Vector3(0f, 0f, CasinoR - 0.3f);
                cur.localRotation = rot;
                for (int k = 0; k < 6; k++)
                    Prim(PrimitiveType.Cylinder, "Fold", cur, new Vector3((k - 2.5f) * 0.17f, 4.5f, (k % 2) * 0.06f), new Vector3(0.2f, 4.4f, 0.16f), drape, false);
                Prim(PrimitiveType.Cube, "Valance", cur, new Vector3(0f, 9f - 0.4f, -0.05f), new Vector3(1.3f, 0.5f, 0.18f), drape, false);
                Prim(PrimitiveType.Cube, "ValanceTrim", cur, new Vector3(0f, 9f - 0.66f, -0.15f), new Vector3(1.32f, 0.05f, 0.02f), gold, false);
                Prim(PrimitiveType.Cylinder, "TieBack", cur, new Vector3(0f, 2.4f, -0.12f), new Vector3(1.05f, 0.05f, 0.3f), gold, false);
                Prim(PrimitiveType.Sphere, "Tassel", cur, new Vector3(-s * 0.45f, 2.15f, -0.2f), new Vector3(0.1f, 0.22f, 0.1f), gold, false);
            }

            // じゅうたん
            Prim(PrimitiveType.Cylinder, "Carpet", lux, new Vector3(0f, 0.035f, 0f), new Vector3(2f * CasinoR, 0.005f, 2f * CasinoR), carpet, false);
            foreach (float r in new[] { 2.2f, 4.2f, 6.2f })
                FlatRing(lux, "CarpetRing", r, 0.04f, 0.08f, 0.004f, 48, gold);

            // 天井の金の飾りとシャンデリア
            Prim(PrimitiveType.Cylinder, "CeilingInner", lux, new Vector3(0f, CasinoH - 0.03f, 0f), new Vector3(2f * CasinoR, 0.02f, 2f * CasinoR), Mat("Casino_CeilingLux", new Color(0.14f, 0.025f, 0.035f), new Color(0.03f, 0f, 0.005f)), false);
            foreach (float r in new[] { 1.5f, 3.5f, 5.5f })
                FlatRing(lux, "CeilingRing", r, CasinoH - 0.06f, 0.12f, 0.03f, 40, gold);
            var chand = Group(lux, "Chandelier");
            chand.localPosition = new Vector3(0f, CasinoH - 2.2f, 0f);
            Prim(PrimitiveType.Cylinder, "Chain", chand, new Vector3(0f, 1.1f, 0f), new Vector3(0.05f, 1.1f, 0.05f), gold, false);
            var bulb = Mat("Chandelier_Bulb", new Color(1f, 0.95f, 0.8f), new Color(3f, 2.6f, 1.8f), true);
            for (int tier = 0; tier < 3; tier++)
            {
                float r = 0.5f + tier * 0.45f, y = -tier * 0.35f;
                FlatRing(chand, "ChandRing", r, y, 0.05f, 0.04f, 20, gold);
                int n = 8 + tier * 4;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    Prim(PrimitiveType.Sphere, "Bulb", chand, new Vector3(Mathf.Sin(a) * r, y + 0.08f, Mathf.Cos(a) * r), Vector3.one * 0.1f, bulb, false);
                    Prim(PrimitiveType.Sphere, "Crystal", chand, new Vector3(Mathf.Sin(a) * r, y - 0.12f, Mathf.Cos(a) * r), new Vector3(0.04f, 0.1f, 0.04f),
                         Mat("Crystal", new Color(0.85f, 0.95f, 1f), new Color(0.4f, 0.5f, 0.6f), false, 0.95f), false);
                }
            }
            AddPointLight(chand, "ChandelierLight", new Vector3(0f, -0.8f, 0f), new Color(1f, 0.8f, 0.58f), 3f, 16f);
            AddPointLight(lux, "SlotSpot", new Vector3(0f, 5f, 2.8f), new Color(1f, 0.8f, 0.62f), 2.5f, 9f);

            // 壁ぎわの大きなサイコロとチップの山
            var dieMat = Mat("Casino_Die", new Color(0.9f, 0.1f, 0.12f), new Color(0.15f, 0f, 0f), false, 0.7f);
            var pip = Mat("Target_White", Color.white);
            var chipMats = new[] { Mat("Chip_Red", new Color(0.85f, 0.1f, 0.1f)), Mat("Chip_Blue", new Color(0.15f, 0.3f, 0.9f)), Mat("Chip_Green", new Color(0.1f, 0.6f, 0.25f)), Mat("Chip_Black", new Color(0.08f, 0.08f, 0.08f)) };
            foreach (float a in new[] { 25f, 120f, 240f, 335f }) // (ゲームの間に)
            {
                var rot = Quaternion.Euler(0f, a, 0f);
                var die = Group(lux, "BigDie");
                die.localPosition = rot * new Vector3(0f, 0.45f, CasinoR - 1.2f);
                die.localRotation = Quaternion.Euler(0f, a + 25f, 0f);
                Prim(PrimitiveType.Cube, "Die", die, Vector3.zero, Vector3.one * 0.8f, dieMat, false);
                foreach (var (x, y) in new[] { (-0.2f, 0.2f), (0f, 0f), (0.2f, -0.2f) })
                {
                    var dot = Prim(PrimitiveType.Cylinder, "Pip", die, new Vector3(x, y, -0.401f), new Vector3(0.12f, 0.003f, 0.12f), pip, false);
                    dot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                for (int s = 0; s < 3; s++)
                {
                    var stack = rot * new Vector3((s - 1) * 0.5f, 0f, CasinoR - 2.1f);
                    int h = Random.Range(4, 9);
                    for (int k = 0; k < h; k++)
                        Prim(PrimitiveType.Cylinder, "Chip", lux, stack + Vector3.up * (0.05f + k * 0.06f), new Vector3(0.3f, 0.025f, 0.3f), chipMats[(s + k) % chipMats.Length], false);
                }
            }
            BuildCasinoFloorDecor(lux); // カードのテーブル・ルーレット台・小さなスロット・ロープの柵・小さなシャンデリア
        }

        // 円柱の壁に沿った水平の帯 (FlatRing の厚みを縦にしたもの)
        void FlatRingVertical(Transform parent, string name, float radius, float y, float height, Material mat)
        {
            FlatRing(parent, name, radius, y, 0.05f, height, 56, mat);
        }

        void AddPointLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var lightGo = new GameObject(name);
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = pos;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        // スポットライトに照らされた丸い光 (壁の上の明るい輪) と、天井から吊った照明器具・そこから広がる円すいの光
        void SpotlightGlow(Transform parent, Vector3 center, float radius)
        {
            var halo = Prim(PrimitiveType.Cylinder, "SpotHalo", parent, center, new Vector3(radius * 2f, 0.002f, radius * 2f), TransparentMat("Spot_Halo", new Color(1f, 0.95f, 0.7f, 0.28f)), false);
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SpotFixture(parent, new Vector3(center.x, CasinoH - 1f, center.z - 2.6f), center, radius);
        }

        /// <summary>
        /// 天井から吊ったスポットライト: 黒い筒と光るレンズ、レンズから照らす所へ広がる薄い光の円すい、本物のスポットライト (影なし)。
        /// lampPos = 器具の位置、aim = 照らす所、radius = 照らす丸の半径 (どれも parent のローカル)
        /// </summary>
        void SpotFixture(Transform parent, Vector3 lampPos, Vector3 aim, float radius)
        {
            var black = Mat("Dx_Black", new Color(0.05f, 0.05f, 0.06f), null, false, 0.6f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var lens = Mat("Spot_Lens", new Color(1f, 0.97f, 0.85f), new Color(4f, 3.6f, 2.6f), true);
            float hang = CasinoH - lampPos.y;
            Prim(PrimitiveType.Cylinder, "SpotHanger", parent, lampPos + Vector3.up * (hang / 2f), new Vector3(0.05f, hang / 2f, 0.05f), black, false);
            var fixture = Group(parent, "SpotFixture");
            fixture.localPosition = lampPos;
            fixture.localRotation = Quaternion.LookRotation(aim - lampPos); // +Z が照らす向き
            Prim(PrimitiveType.Cylinder, "Housing", fixture, Vector3.back * 0.15f, new Vector3(0.34f, 0.2f, 0.34f), black, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "HousingRim", fixture, Vector3.forward * 0.05f, new Vector3(0.38f, 0.02f, 0.38f), gold, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "Lens", fixture, Vector3.forward * 0.07f, new Vector3(0.28f, 0.005f, 0.28f), lens, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            float len = Vector3.Distance(lampPos, aim);
            var beam = new GameObject("SpotBeam");
            beam.transform.SetParent(fixture, false);
            beam.transform.localPosition = Vector3.forward * 0.08f;
            beam.AddComponent<MeshFilter>().sharedMesh = ConeMesh(0.13f, radius * 0.95f, len - 0.1f);
            var beamRenderer = beam.AddComponent<MeshRenderer>();
            beamRenderer.sharedMaterial = TransparentMat("Spot_Cone", new Color(1f, 0.95f, 0.75f, 0.06f));
            beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("SpotLight");
            lightGo.transform.SetParent(fixture, false);
            lightGo.transform.localPosition = Vector3.forward * 0.1f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.93f, 0.78f);
            light.intensity = 6f;
            light.range = len + 3f;
            light.spotAngle = 2f * Mathf.Atan2(radius * 1.1f, len) * Mathf.Rad2Deg;
            light.shadows = LightShadows.None;
        }

        /// <summary>光の円すい (ローカル +Z へ、半径 r0 から r1 へ広がる長さ len の筒。両端は開いていて、内側からも外側からも見える)</summary>
        Mesh ConeMesh(float r0, float r1, float len)
        {
            string name = $"Cone_{r0:0.00}_{r1:0.00}_{len:0.00}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int Sides = 20;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int s = 0; s <= Sides; s++)
            {
                float a = s * Mathf.PI * 2f / Sides;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                verts.Add(dir * r0);
                verts.Add(dir * r1 + Vector3.forward * len);
            }
            for (int s = 0; s < Sides; s++)
            {
                int a0 = s * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                tris.AddRange(new[] { a0, b0, b1, a0, b1, a1 }); // 外側
                tris.AddRange(new[] { a0, b1, b0, a0, a1, b1 }); // 内側
            }
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
        // 扇形 (ルーレット台の円盤)
        // ------------------------------------------------------------------

        /// <summary>扇形 (内側の半径 r0、外側の半径 r1、12 時から時計回りに a0〜a1 度)。表は -Z</summary>
        Mesh WedgeMesh(float r0, float r1, float a0, float a1)
        {
            string name = $"Wedge_{r0:0.00}_{r1:0.00}_{a0:0}_{a1:0}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int Steps = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int s = 0; s <= Steps; s++)
            {
                float a = Mathf.Lerp(a0, a1, s / (float)Steps) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
                verts.Add(dir * r0);
                verts.Add(dir * r1);
            }
            for (int s = 0; s < Steps; s++)
            {
                int i0 = s * 2, o0 = i0 + 1, i1 = i0 + 2, o1 = i0 + 3;
                tris.AddRange(new[] { i0, o0, o1, i0, o1, i1 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }

        /// <summary>カジノの的: 大きなラッキーセブンとスロットとコイン (LookArt の絵のステッカー。はみ出して大胆に)</summary>
        GameObject CasinoLook(Transform look) => StickerLook(look, "Casino", "CASINO", new Color(1f, 0.88f, 0.35f));
    }
}
