using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 豪華屋台 (DELUXE の的を撃つと、出ている屋台が全部これに入れ替わる) の生成。
    /// 黒の漆塗りと金を基調に、流れる電飾・光る差し色・王冠・スポットライトで飾る。
    /// 的は点数ごとに形が変わる: 100 = 丸, 200 = 四角, 300 = 三角, 500 以上 = 星 (1000 は豪華屋台だけの桃色の星)。
    /// 屋台の大きさと置き場所は普通の屋台と同じ (GameManager が同じ場所で入れ替える)。
    /// </summary>
    public partial class GalleryBuilder
    {
        const string GeneratedMeshDir = "Assets/ShootingGallery/Generated/Meshes";

        enum DeluxeGimmick { Grand, ShipParade, GrandCarousel, StarWheel, Chandelier, Orbit, DoubleRoulette, Express, Fountain }
        enum Shape { Circle, Square, Triangle, Star }

        /// <summary>豪華版のフィーバー屋台 (輪に並ぶ最大数 - 1 = 7 軒以上必要)</summary>
        static readonly DeluxeGimmick[] DeluxeFeverGimmicks =
        {
            DeluxeGimmick.ShipParade, DeluxeGimmick.GrandCarousel, DeluxeGimmick.StarWheel, DeluxeGimmick.Chandelier,
            DeluxeGimmick.Orbit, DeluxeGimmick.DoubleRoulette, DeluxeGimmick.Express, DeluxeGimmick.Fountain
        };

        // 屋台ごとの差し色 (0 = メイン)。光る帯・幕・電飾に使う
        static readonly Color[] DeluxeAccents =
        {
            new Color(1f, 0.15f, 0.25f), new Color(0.2f, 0.5f, 1f), new Color(1f, 0.25f, 0.8f), new Color(0.6f, 0.3f, 1f),
            new Color(1f, 0.6f, 0.15f), new Color(0.1f, 0.9f, 0.85f), new Color(0.2f, 1f, 0.45f), new Color(0.55f, 0.85f, 1f),
            new Color(1f, 0.45f, 0.6f)
        };

        readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        // ---- 豪華屋台の色 ----
        Material DxBlack => Mat("Dx_Black", new Color(0.035f, 0.035f, 0.04f), null, false, 0.85f);      // 黒の漆塗り
        Material DxVelvet => Mat("Dx_Velvet", new Color(0.07f, 0.05f, 0.08f), null, false, 0.05f);      // 奥の壁のビロード
        Material DxGold => Mat("Dx_Gold", new Color(1f, 0.78f, 0.32f), new Color(0.32f, 0.2f, 0.03f), false, 0.8f);
        Material DxGoldDark => Mat("Dx_GoldDark", new Color(0.55f, 0.4f, 0.15f), null, false, 0.7f);
        Material DxIvory => Mat("Dx_Ivory", new Color(0.95f, 0.92f, 0.85f), null, false, 0.5f);
        Material DxBulbOn => Mat("Dx_BulbOn", new Color(1f, 0.92f, 0.6f), new Color(2.4f, 1.8f, 0.7f));
        Material DxBulbOff => Mat("Dx_BulbOff", new Color(0.3f, 0.24f, 0.14f), new Color(0.06f, 0.04f, 0.01f));
        Material DxWater => Mat("Dx_Water", new Color(0.05f, 0.2f, 0.55f), new Color(0.02f, 0.12f, 0.4f), false, 0.9f);
        Material DxOpening => Mat("Dx_Opening", new Color(0.01f, 0.01f, 0.015f), null, false, 0f);

        Material DxGlow(int accent)
        {
            var c = DeluxeAccents[accent % DeluxeAccents.Length];
            return Mat($"Dx_Glow_{accent}", c, c * 1.5f);
        }

        Material DxCloth(int accent)
        {
            var c = DeluxeAccents[accent % DeluxeAccents.Length];
            return Mat($"Dx_Cloth_{accent}", c * 0.6f, null, false, 0.5f);
        }

        static string DeluxeSign(DeluxeGimmick g) => g switch
        {
            DeluxeGimmick.Grand => "GRAND GALLERY DX",
            DeluxeGimmick.ShipParade => "SHIP PARADE",
            DeluxeGimmick.GrandCarousel => "GRAND CAROUSEL",
            DeluxeGimmick.StarWheel => "STAR WHEEL",
            DeluxeGimmick.Chandelier => "CHANDELIER",
            DeluxeGimmick.Orbit => "PLANETARIUM",
            DeluxeGimmick.DoubleRoulette => "DOUBLE ROULETTE",
            DeluxeGimmick.Express => "ROYAL EXPRESS",
            _ => "FOUNTAIN"
        };

        // ------------------------------------------------------------------
        // 屋台 (外観 + ギミック)
        // ------------------------------------------------------------------

        /// <summary>豪華屋台を1軒作る。隠れた状態で作り、再生時は GameManager が普通の屋台と入れ替えて出す。</summary>
        Stall BuildDeluxeStall(string name, DeluxeGimmick gimmick, int accent, float angleDeg)
        {
            var t = Group(world, name);
            t.gameObject.SetActive(false);
            var rot = Quaternion.Euler(0f, angleDeg, 0f);
            t.localPosition = rot * new Vector3(0f, 0f, stallRadius);
            t.localRotation = rot;

            var cloth = DxCloth(accent);
            var glow = DxGlow(accent);
            BuildDeluxeFrame(t, cloth, glow, DeluxeAccents[accent % DeluxeAccents.Length], DeluxeSign(gimmick));

            var targetsRoot = Group(t, "Targets");
            targetsRoot.gameObject.SetActive(false);
            var targets = new List<Target>();
            switch (gimmick)
            {
                case DeluxeGimmick.Grand: BuildDxGrand(t, targetsRoot, targets, glow); break;
                case DeluxeGimmick.ShipParade: BuildDxShipParade(t, targets, cloth, glow); break;
                case DeluxeGimmick.GrandCarousel: BuildDxCarousel(t, targets, cloth, glow); break;
                case DeluxeGimmick.StarWheel: BuildDxStarWheel(t, targets, glow); break;
                case DeluxeGimmick.Chandelier: BuildDxChandelier(t, targets, glow); break;
                case DeluxeGimmick.Orbit: BuildDxOrbit(t, targets, glow); break;
                case DeluxeGimmick.DoubleRoulette: BuildDxRoulette(t, targets, cloth, glow); break;
                case DeluxeGimmick.Express: BuildDxExpress(t, targets, cloth, glow); break;
                case DeluxeGimmick.Fountain: BuildDxFountain(t, targets, glow); break;
            }

            var stall = t.gameObject.AddComponent<Stall>();
            stall.targets = targets.ToArray();
            if (!editMode) stall.SetTargetsAvailable(false);
            targetsRoot.gameObject.SetActive(true);
            return stall;
        }

        /// <summary>
        /// 豪華屋台の骨組み (普通の屋台と同じ寸法)。黒のカウンターに金の天板とひし形の飾り、ビロードの奥の壁に金の縦じま、
        /// 金の柱、差し色と黒の幕に金の房、クリスタルのランプ、金の縁と流れる電球に囲まれた看板、回る金の星の王冠、スポットライト。
        /// </summary>
        void BuildDeluxeFrame(Transform t, Material cloth, Material glow, Color accent, string sign)
        {
            var black = DxBlack;
            var gold = DxGold;

            // カウンター
            Prim(PrimitiveType.Cube, "Counter", t, new Vector3(0f, 0.5f, -1.2f), new Vector3(2.4f, 1.0f, 0.4f), black);
            Prim(PrimitiveType.Cube, "CounterTop", t, new Vector3(0f, 1.02f, -1.2f), new Vector3(2.5f, 0.05f, 0.5f), gold);
            Prim(PrimitiveType.Cube, "CounterGlow", t, new Vector3(0f, 0.9f, -1.405f), new Vector3(2.4f, 0.025f, 0.01f), glow, false);
            Prim(PrimitiveType.Cube, "CounterTrim", t, new Vector3(0f, 0.1f, -1.405f), new Vector3(2.4f, 0.04f, 0.01f), gold, false);
            for (int i = -2; i <= 2; i++)
            {
                var d = Prim(PrimitiveType.Cube, "Diamond", t, new Vector3(i * 0.45f, 0.5f, -1.406f), new Vector3(0.13f, 0.13f, 0.012f), i % 2 == 0 ? gold : glow, false);
                d.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // 奥の壁 (ビロード + 金の縦じま + 光る縁)
            Prim(PrimitiveType.Cube, "BackWall", t, new Vector3(0f, 1.4f, 0.8f), new Vector3(3.6f, 2.8f, 0.08f), DxVelvet);
            for (int k = 0; k < 9; k++)
                Prim(PrimitiveType.Cube, "Pinstripe", t, new Vector3(-1.6f + k * 0.4f, 1.4f, 0.755f), new Vector3(0.015f, 2.7f, 0.01f), DxGoldDark, false);
            Prim(PrimitiveType.Cube, "WallGlow_Top", t, new Vector3(0f, 2.7f, 0.752f), new Vector3(3.5f, 0.03f, 0.01f), glow, false);
            foreach (float x in new[] { -1.74f, 1.74f })
                Prim(PrimitiveType.Cube, "WallGlow_Side", t, new Vector3(x, 1.4f, 0.752f), new Vector3(0.03f, 2.6f, 0.01f), glow, false);

            // 金の柱 (黒い台座 + 光る輪 + 金の玉)
            const float Px = 1.7f;
            foreach (var p in new[] { new Vector3(-Px, 0f, -0.9f), new Vector3(Px, 0f, -0.9f), new Vector3(-Px, 0f, 0.75f), new Vector3(Px, 0f, 0.75f) })
            {
                Prim(PrimitiveType.Cylinder, "Pillar", t, p + Vector3.up * 1.4f, new Vector3(0.1f, 1.4f, 0.1f), gold);
                Prim(PrimitiveType.Cube, "PillarBase", t, p + Vector3.up * 0.15f, new Vector3(0.2f, 0.3f, 0.2f), black, false);
                Prim(PrimitiveType.Sphere, "Finial", t, p + Vector3.up * 2.88f, Vector3.one * 0.16f, gold, false);
                if (p.z < 0f)
                    foreach (float y in new[] { 0.8f, 1.5f, 2.2f })
                        Prim(PrimitiveType.Cylinder, "PillarRing", t, p + Vector3.up * y, new Vector3(0.13f, 0.01f, 0.13f), glow, false);
            }

            // 屋根 (当たり判定なし: 空の UFO を狙う弾をさえぎらないように)
            Prim(PrimitiveType.Cube, "Roof", t, new Vector3(0f, 2.8f, -0.05f), new Vector3(3.7f, 0.08f, 1.8f), black, false);
            Prim(PrimitiveType.Cube, "RoofTrim", t, new Vector3(0f, 2.8f, -0.955f), new Vector3(3.72f, 0.1f, 0.02f), gold, false);

            // 幕: 差し色と黒を交互に、下に金の房
            const int Awnings = 8;
            const float AwningW = 3.6f / Awnings;
            for (int i = 0; i < Awnings; i++)
            {
                float x = -1.8f + (i + 0.5f) * AwningW;
                Prim(PrimitiveType.Cube, $"Awning_{i}", t, new Vector3(x, 2.58f, -0.95f), new Vector3(AwningW, 0.38f, 0.04f), i % 2 == 0 ? cloth : black, false);
                Prim(PrimitiveType.Sphere, "Fringe", t, new Vector3(x, 2.37f, -0.975f), Vector3.one * 0.06f, gold, false);
            }
            Prim(PrimitiveType.Cube, "AwningTrim", t, new Vector3(0f, 2.4f, -0.975f), new Vector3(3.6f, 0.015f, 0.01f), gold, false);

            // クリスタルのランプ (提灯の代わり)
            foreach (float x in new[] { -Px, Px })
            {
                Prim(PrimitiveType.Sphere, "Crystal", t, new Vector3(x, 2.15f, -1.0f), new Vector3(0.2f, 0.28f, 0.2f), glow, false);
                Prim(PrimitiveType.Cylinder, "CrystalCap", t, new Vector3(x, 2.31f, -1.0f), new Vector3(0.12f, 0.025f, 0.12f), gold, false);
                Prim(PrimitiveType.Sphere, "CrystalDrop", t, new Vector3(x, 1.97f, -1.0f), Vector3.one * 0.06f, DxBulbOn, false);
            }

            // 看板: 黒い板に金の縁と金の文字、まわりを電球の光が流れる
            Prim(PrimitiveType.Cube, "SignBoard", t, new Vector3(0f, 3.15f, -0.9f), new Vector3(2.6f, 0.55f, 0.06f), black, false);
            Prim(PrimitiveType.Cube, "SignFrame_T", t, new Vector3(0f, 3.43f, -0.935f), new Vector3(2.66f, 0.04f, 0.02f), gold, false);
            Prim(PrimitiveType.Cube, "SignFrame_B", t, new Vector3(0f, 2.87f, -0.935f), new Vector3(2.66f, 0.04f, 0.02f), gold, false);
            foreach (float x in new[] { -1.33f, 1.33f })
                Prim(PrimitiveType.Cube, "SignFrame_S", t, new Vector3(x, 3.15f, -0.935f), new Vector3(0.04f, 0.6f, 0.02f), gold, false);
            MakeText(t, "SignText", sign, new Vector3(0f, 3.15f, -0.945f), 0.2f, new Color(1f, 0.85f, 0.4f));

            var marquee = Group(t, "Marquee");
            var bulbs = new List<Renderer>();
            const int Row = 15;
            for (int i = 0; i < Row; i++) bulbs.Add(Bulb(marquee, new Vector3(-1.19f + i * 2.38f / (Row - 1), 3.36f, -0.945f), 0.045f)); // 上: 左 → 右
            foreach (float y in new[] { 3.22f, 3.08f }) bulbs.Add(Bulb(marquee, new Vector3(1.26f, y, -0.945f), 0.045f));         // 右: 上 → 下
            for (int i = Row - 1; i >= 0; i--) bulbs.Add(Bulb(marquee, new Vector3(-1.19f + i * 2.38f / (Row - 1), 2.94f, -0.945f), 0.045f)); // 下: 右 → 左
            foreach (float y in new[] { 3.08f, 3.22f }) bulbs.Add(Bulb(marquee, new Vector3(-1.26f, y, -0.945f), 0.045f));        // 左: 下 → 上
            Marquee(marquee, bulbs, 3, 0.1f, false);

            // 王冠: 看板の上で回る金の星
            var crown = Group(t, "Crown");
            crown.localPosition = new Vector3(0f, 3.66f, -0.9f);
            var spin = crown.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.up;
            spin.speed = 60f;
            ShapeObj(Shape.Star, crown, "CrownStar", Vector3.zero, 0.38f, DxGold, false, 0.04f);
            Prim(PrimitiveType.Sphere, "CrownGem", t, new Vector3(0f, 3.47f, -0.93f), Vector3.one * 0.07f, glow, false);

            // 差し色のスポットライト (屋台の中を照らす)
            var lightGo = new GameObject("DeluxeLight");
            lightGo.transform.SetParent(t, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.3f, -0.6f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.Lerp(accent, Color.white, 0.35f);
            light.intensity = 2f;
            light.range = 4.5f;
            light.shadows = LightShadows.None;
        }

        Renderer Bulb(Transform parent, Vector3 pos, float size)
        {
            return Prim(PrimitiveType.Sphere, "Bulb", parent, pos, Vector3.one * size, DxBulbOff, false).GetComponent<Renderer>();
        }

        void Marquee(Transform group, List<Renderer> bulbs, int spacing, float step, bool reverse)
        {
            var m = group.gameObject.AddComponent<MarqueeLights>();
            m.bulbs = bulbs.ToArray();
            m.onMaterial = DxBulbOn;
            m.offMaterial = DxBulbOff;
            m.spacing = spacing;
            m.step = step;
            m.reverse = reverse;
        }

        /// <summary>radius の円に沿って細い棒を並べた輪 (見た目だけ)。parent のローカル XY 平面に作る</summary>
        void RingSegments(Transform parent, string name, float radius, int segments, float thickness, Material a, Material b = null, float z = 0f)
        {
            for (int i = 0; i < segments; i++)
            {
                float ang = i * Mathf.PI * 2f / segments;
                var seg = Prim(PrimitiveType.Cube, name, parent, new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * radius + Vector3.forward * (z + (i % 2) * 0.002f),
                               new Vector3(thickness, 2f * Mathf.PI * radius / segments + 0.006f, thickness), b != null && i % 2 == 1 ? b : a, false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
            }
        }

        // ---- メイン: 黒と金のひな壇。左右の回る輪に高得点の的、真ん中にフィーバーの的 ----
        void BuildDxGrand(Transform t, Transform root, List<Target> targets, Material glow)
        {
            for (int i = 0; i < TierHeights.Length; i++)
            {
                Prim(PrimitiveType.Cube, $"Tier_{i + 1}", t, new Vector3(0f, TierHeights[i] / 2f, TierZ[i]), new Vector3(3.0f, TierHeights[i], 0.25f), DxBlack);
                Prim(PrimitiveType.Cube, "TierTrim", t, new Vector3(0f, TierHeights[i] + 0.006f, TierZ[i] - 0.125f), new Vector3(3.02f, 0.02f, 0.02f), DxGold, false);
                Prim(PrimitiveType.Cube, "TierGlow", t, new Vector3(0f, TierHeights[i] - 0.07f, TierZ[i] - 0.127f), new Vector3(3.0f, 0.015f, 0.005f), glow, false);
            }
            // 奥の壁の大きな金の星の紋章 (飾り)
            ShapeObj(Shape.Star, t, "Emblem", new Vector3(0f, 2.35f, 0.74f), 0.6f, DxGoldDark, false);

            Vector3 Slot(int tier, int s) => new Vector3(SlotX[s], TierHeights[tier], TierZ[tier]);

            // 下段: 100, 200, 300, 200, 100
            int[] lower = { 100, 200, 300, 200, 100 };
            for (int s = 0; s < 5; s++) targets.Add(ShapeTarget(root, lower[s], Slot(0, s)));

            // 中段: 200, 300, (左右に動く 500), 300, 200
            targets.Add(ShapeTarget(root, 200, Slot(1, 0)));
            targets.Add(ShapeTarget(root, 300, Slot(1, 1)));
            var slider = ShapeTarget(root, 500, Slot(1, 2));
            slider.moving = true;
            slider.moveAmplitude = 0.28f;
            slider.moveSpeed = 2.8f;
            targets.Add(slider);
            targets.Add(ShapeTarget(root, 300, Slot(1, 3)));
            targets.Add(ShapeTarget(root, 200, Slot(1, 4)));

            // 上段: (回る輪), 500, フィーバー, 500, (回る輪)
            targets.Add(ShapeTarget(root, 500, Slot(2, 1)));
            targets.Add(FeverTarget(root, Slot(2, 2)));
            targets.Add(ShapeTarget(root, 500, Slot(2, 3)));

            // 上段の左右: 金の輪が回り、2つの的がその上を回る (的はいつも正面を向く)
            int[][] ringValues = { new[] { 500, 300 }, new[] { 1000, 300 } };
            float[] ringSpeeds = { 70f, -80f };
            for (int r = 0; r < 2; r++)
            {
                float x = r == 0 ? -1.2f : 1.2f;
                var center = new Vector3(x, TierHeights[2] + TargetDiscY, TierZ[2] - 0.05f);
                Prim(PrimitiveType.Cube, "RingPost", t, new Vector3(x, TierHeights[2] + TargetDiscY / 2f, TierZ[2] + 0.04f), new Vector3(0.04f, TargetDiscY, 0.04f), DxGold, false);
                var ringVisual = Group(t, "RingVisual");
                ringVisual.localPosition = center + Vector3.forward * 0.04f;
                RingSegments(ringVisual, "Ring", 0.24f, 16, 0.015f, DxGold, glow);
                Prim(PrimitiveType.Sphere, "RingHub", ringVisual, Vector3.zero, Vector3.one * 0.06f, glow, false);

                var orbit = Group(t, $"Orbit_{r}");
                orbit.localPosition = center;
                var rotator = orbit.gameObject.AddComponent<Rotator>();
                rotator.axis = Vector3.forward;
                rotator.speed = ringSpeeds[r];
                for (int i = 0; i < 2; i++)
                {
                    var holder = Group(orbit, $"Holder_{i}");
                    holder.localPosition = new Vector3(i == 0 ? 0.24f : -0.24f, 0f, 0f);
                    holder.gameObject.AddComponent<KeepUpright>().reference = t;
                    targets.Add(ShapeTarget(holder, ringValues[r][i], new Vector3(0f, -TargetDiscY, -0.02f)));
                }
            }
        }

        // ---- 船の行進: 水路を帆船と豪華なヨットが波に揺られて横切る (アヒルの行進の豪華版) ----
        void BuildDxShipParade(Transform t, List<Target> targets, Material cloth, Material glow)
        {
            // 手前: 3隻の帆船 (甲板の前後とマストの上に的)。奥: 4隻のヨットが逆向きに速く走る
            DxLane(t, targets, "Lane_Lower", 0.9f, 0.0f, 0.36f, true,
                   new[] { new[] { 100, 300, 200 }, new[] { 200, 500, 100 }, new[] { 300, 1000, 200 } }, cloth, glow);
            DxLane(t, targets, "Lane_Upper", 1.45f, 0.45f, -0.6f, false,
                   new[] { new[] { 500 }, new[] { 300 }, new[] { 500 }, new[] { 200 } }, cloth, glow);
        }

        void DxLane(Transform t, List<Target> targets, string name, float y, float z, float speed, bool bigShips, int[][] values, Material cloth, Material glow)
        {
            const float Width = 3.0f;
            const float LoopLength = 3.4f; // 両端の城門の中で折り返す
            float gateH = bigShips ? 1.0f : 0.6f;

            Prim(PrimitiveType.Cube, name + "_Trough", t, new Vector3(0f, y / 2f, z), new Vector3(Width, y, 0.35f), DxBlack);
            Prim(PrimitiveType.Cube, name + "_Trim", t, new Vector3(0f, y, z - 0.176f), new Vector3(Width, 0.025f, 0.01f), DxGold, false);
            Prim(PrimitiveType.Cube, name + "_Glow", t, new Vector3(0f, y - 0.08f, z - 0.177f), new Vector3(Width, 0.012f, 0.005f), glow, false);
            Prim(PrimitiveType.Cube, name + "_Water", t, new Vector3(0f, y + 0.005f, z), new Vector3(Width - 0.04f, 0.01f, 0.3f), DxWater, false);
            // 両端の城門 (船はこの中で折り返す)
            foreach (float x in new[] { -Width / 2f, Width / 2f })
            {
                Prim(PrimitiveType.Cube, name + "_Gate", t, new Vector3(x, y + gateH / 2f, z), new Vector3(0.45f, gateH, 0.42f), DxBlack);
                Prim(PrimitiveType.Cube, "GateOpening", t, new Vector3(x, y + gateH * 0.38f, z - 0.212f), new Vector3(0.28f, gateH * 0.76f, 0.005f), DxOpening, false);
                Prim(PrimitiveType.Cube, "GateArch", t, new Vector3(x, y + gateH * 0.78f, z - 0.214f), new Vector3(0.32f, 0.03f, 0.01f), DxGold, false);
                Prim(PrimitiveType.Cube, "GateTop", t, new Vector3(x, y + gateH, z - 0.215f), new Vector3(0.47f, 0.04f, 0.012f), DxGold, false);
                foreach (float s in new[] { -0.2f, 0.2f })
                    Prim(PrimitiveType.Sphere, "GateFinial", t, new Vector3(x + s, y + gateH + 0.05f, z - 0.17f), Vector3.one * 0.07f, DxGold, false);
                Prim(PrimitiveType.Sphere, "GateLamp", t, new Vector3(x, y + gateH * 0.9f, z - 0.22f), Vector3.one * 0.05f, glow, false);
            }

            var lane = Group(t, name);
            lane.localPosition = new Vector3(0f, y, z);
            var items = new List<Transform>();
            float facing = Mathf.Sign(speed); // 船首を進む向きへ
            for (int i = 0; i < values.Length; i++)
            {
                var ship = Group(lane, $"Ship_{i}");
                ship.localPosition = new Vector3(-LoopLength / 2f + (i + 0.5f) * LoopLength / values.Length, 0f, 0f);
                var hull = Group(ship, "Hull");
                var rock = hull.gameObject.AddComponent<Pendulum>(); // 波に揺られて前後に傾く
                rock.amplitude = bigShips ? 5f : 8f;
                rock.speed = bigShips ? 1.4f + 0.2f * i : 2.2f + 0.3f * i;
                rock.phase = i * 1.7f;
                if (bigShips) BuildSailingShip(hull, facing, values[i], targets, cloth, glow);
                else BuildYacht(hull, facing, values[i][0], targets, glow);
                items.Add(ship);
            }

            var conveyor = lane.gameObject.AddComponent<ConveyorBelt>();
            conveyor.items = items.ToArray();
            conveyor.length = LoopLength;
            conveyor.speed = speed;
            conveyor.waveHeight = bigShips ? 0.03f : 0.05f;
            conveyor.waves = 2f;
            conveyor.waveTargetsOnly = false; // 船ごと波打つ
        }

        // 帆船: 黒い船体に金の帯と光る丸窓、白い船室、帆 (的の後ろ)。甲板の前後とマストの上に的
        void BuildSailingShip(Transform hull, float f, int[] values, List<Target> targets, Material cloth, Material glow)
        {
            Prim(PrimitiveType.Cube, "HullBody", hull, new Vector3(0f, 0.05f, 0f), new Vector3(0.56f, 0.1f, 0.17f), DxBlack);
            var bow = Prim(PrimitiveType.Cube, "Bow", hull, new Vector3(0.29f * f, 0.06f, 0f), new Vector3(0.12f, 0.1f, 0.12f), DxBlack, false);
            bow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Prim(PrimitiveType.Cube, "GoldStripe", hull, new Vector3(0f, 0.075f, -0.087f), new Vector3(0.56f, 0.015f, 0.005f), DxGold, false);
            for (int k = -1; k <= 1; k++)
                Prim(PrimitiveType.Sphere, "Porthole", hull, new Vector3(k * 0.13f, 0.04f, -0.086f), Vector3.one * 0.025f, glow, false);
            Prim(PrimitiveType.Cube, "Cabin", hull, new Vector3(-0.22f * f, 0.13f, 0.03f), new Vector3(0.12f, 0.07f, 0.12f), DxIvory, false);
            Prim(PrimitiveType.Cube, "CabinRoof", hull, new Vector3(-0.22f * f, 0.17f, 0.03f), new Vector3(0.14f, 0.012f, 0.14f), DxGold, false);
            Prim(PrimitiveType.Cylinder, "Mast", hull, new Vector3(0f, 0.38f, 0.04f), new Vector3(0.015f, 0.28f, 0.015f), DxGold, false);
            Prim(PrimitiveType.Cube, "Sail", hull, new Vector3(0f, 0.37f, 0.06f), new Vector3(0.22f, 0.26f, 0.005f), cloth, false);
            Prim(PrimitiveType.Cube, "SailStripe", hull, new Vector3(0f, 0.37f, 0.057f), new Vector3(0.22f, 0.03f, 0.004f), DxGold, false);
            Prim(PrimitiveType.Cube, "Pennant", hull, new Vector3(0.32f * f, 0.16f, 0f), new Vector3(0.06f, 0.035f, 0.005f), glow, false);

            targets.Add(ShapeTarget(hull, values[0], new Vector3(-0.17f, 0.1f, -0.03f)));
            targets.Add(ShapeTarget(hull, values[1], new Vector3(0f, 0.62f, -0.01f)));  // マストの上
            targets.Add(ShapeTarget(hull, values[2], new Vector3(0.17f, 0.1f, -0.03f)));
        }

        // ヨット: 白い船体に金の帯、光る窓の船室。的は1つ
        void BuildYacht(Transform hull, float f, int value, List<Target> targets, Material glow)
        {
            Prim(PrimitiveType.Cube, "HullBody", hull, new Vector3(0f, 0.035f, 0f), new Vector3(0.32f, 0.07f, 0.13f), DxIvory);
            var bow = Prim(PrimitiveType.Cube, "Bow", hull, new Vector3(0.17f * f, 0.04f, 0f), new Vector3(0.09f, 0.07f, 0.09f), DxIvory, false);
            bow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Prim(PrimitiveType.Cube, "GoldStripe", hull, new Vector3(0f, 0.05f, -0.066f), new Vector3(0.32f, 0.012f, 0.004f), DxGold, false);
            Prim(PrimitiveType.Cube, "Cabin", hull, new Vector3(-0.08f * f, 0.09f, 0.02f), new Vector3(0.12f, 0.045f, 0.09f), DxBlack, false);
            Prim(PrimitiveType.Cube, "CabinGlow", hull, new Vector3(-0.08f * f, 0.09f, -0.026f), new Vector3(0.1f, 0.015f, 0.004f), glow, false);
            targets.Add(ShapeTarget(hull, value, new Vector3(0.06f * f, 0.07f, -0.02f)));
        }

        // ---- 豪華な回転木馬: 天蓋の電飾が回り、下の段の的は木馬のように上下しながら回る ----
        void BuildDxCarousel(Transform t, List<Target> targets, Material cloth, Material glow)
        {
            var center = new Vector3(0f, 0f, -0.05f);
            Prim(PrimitiveType.Cylinder, "Base", t, center + Vector3.up * 0.45f, new Vector3(0.55f, 0.45f, 0.55f), DxBlack);
            Prim(PrimitiveType.Cylinder, "BaseTrim", t, center + Vector3.up * 0.9f, new Vector3(0.58f, 0.012f, 0.58f), DxGold, false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                Prim(PrimitiveType.Cube, "BaseGlow", t, center + new Vector3(Mathf.Sin(a) * 0.276f, 0.45f, Mathf.Cos(a) * 0.276f), new Vector3(0.02f, 0.8f, 0.02f), glow, false);
            }
            Prim(PrimitiveType.Cylinder, "Pole", t, center + Vector3.up * 1.62f, new Vector3(0.07f, 0.73f, 0.07f), DxGold);

            // 天蓋 (ゆっくり回る。縁に幕と電球)
            var canopy = Group(t, "Canopy");
            canopy.localPosition = center + Vector3.up * 2.35f; // 星の先が屋根 (下面 2.76) に届かない高さ
            var canopySpin = canopy.gameObject.AddComponent<Rotator>();
            canopySpin.axis = Vector3.up;
            canopySpin.speed = 20f;
            Prim(PrimitiveType.Cylinder, "Roof", canopy, Vector3.zero, new Vector3(1.6f, 0.03f, 1.6f), DxBlack, false);
            Prim(PrimitiveType.Cylinder, "Roof2", canopy, Vector3.up * 0.08f, new Vector3(1.1f, 0.05f, 1.1f), DxGold, false);
            Prim(PrimitiveType.Cylinder, "Roof3", canopy, Vector3.up * 0.17f, new Vector3(0.65f, 0.05f, 0.65f), DxBlack, false);
            Prim(PrimitiveType.Cylinder, "Roof4", canopy, Vector3.up * 0.25f, new Vector3(0.3f, 0.04f, 0.3f), DxGold, false);
            ShapeObj(Shape.Star, canopy, "TopStar", Vector3.up * 0.3f, 0.2f, DxGold, false, 0.03f);
            var canopyBulbs = new List<Renderer>();
            const int Valance = 16;
            for (int i = 0; i < Valance; i++)
            {
                float a = i * Mathf.PI * 2f / Valance;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var panel = Prim(PrimitiveType.Cube, "Valance", canopy, dir * 0.8f + Vector3.down * 0.07f, new Vector3(0.32f, 0.12f, 0.01f), i % 2 == 0 ? cloth : DxBlack, false);
                panel.transform.localRotation = Quaternion.LookRotation(dir);
                Prim(PrimitiveType.Sphere, "Tassel", canopy, dir * 0.81f + Vector3.down * 0.15f, Vector3.one * 0.04f, DxGold, false);
                canopyBulbs.Add(Bulb(canopy, dir * 0.82f + Vector3.up * 0.02f, 0.045f));
                canopyBulbs.Add(Bulb(canopy, dir * 0.55f + Vector3.up * 0.11f, 0.04f));
            }
            Marquee(canopy, canopyBulbs, 4, 0.09f, false);

            // 下の段: 8本の金の柱に乗った的が、上下しながら回る
            var lowerTable = Group(t, "Turntable_Lower");
            lowerTable.localPosition = center + Vector3.up * 0.92f;
            var lowerSpin = lowerTable.gameObject.AddComponent<Rotator>();
            lowerSpin.axis = Vector3.up;
            lowerSpin.speed = 30f;
            Prim(PrimitiveType.Cylinder, "Disc", lowerTable, new Vector3(0f, -0.02f, 0f), new Vector3(1.6f, 0.02f, 1.6f), DxBlack);
            Prim(PrimitiveType.Cylinder, "DiscRim", lowerTable, new Vector3(0f, -0.035f, 0f), new Vector3(1.64f, 0.012f, 1.64f), DxGold, false);
            int[] lowerValues = { 100, 200, 300, 200, 100, 500, 200, 300 };
            for (int i = 0; i < lowerValues.Length; i++)
            {
                float a = i * Mathf.PI * 2f / lowerValues.Length;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Prim(PrimitiveType.Cylinder, "RiderPole", lowerTable, dir * 0.7f + Vector3.up * 0.715f, new Vector3(0.02f, 0.715f, 0.02f), DxGold, false);
                var rider = Group(lowerTable, $"Rider_{i}");
                rider.localPosition = dir * 0.6f + Vector3.up * 0.2f;
                rider.localRotation = Quaternion.LookRotation(-dir); // 外側を向かせる
                var bob = rider.gameObject.AddComponent<Oscillator>();
                bob.offset = new Vector3(0f, 0.17f, 0f);
                bob.speed = 1.8f;
                bob.phase = i * 0.8f;
                Prim(PrimitiveType.Cube, "Saddle", rider, new Vector3(0f, -0.01f, 0f), new Vector3(0.14f, 0.02f, 0.1f), i % 2 == 0 ? cloth : DxGold, false);
                targets.Add(ShapeTarget(rider, lowerValues[i], Vector3.zero));
            }

            // 上の段: 逆向きに回る
            var upperTable = Group(t, "Turntable_Upper");
            upperTable.localPosition = center + Vector3.up * 1.6f;
            var upperSpin = upperTable.gameObject.AddComponent<Rotator>();
            upperSpin.axis = Vector3.up;
            upperSpin.speed = -45f;
            Prim(PrimitiveType.Cylinder, "Disc", upperTable, new Vector3(0f, -0.02f, 0f), new Vector3(1.0f, 0.02f, 1.0f), cloth);
            Prim(PrimitiveType.Cylinder, "DiscRim", upperTable, new Vector3(0f, -0.035f, 0f), new Vector3(1.04f, 0.012f, 1.04f), DxGold, false);
            int[] upperValues = { 300, 500, 300, 1000, 300 };
            for (int i = 0; i < upperValues.Length; i++)
            {
                float a = i * Mathf.PI * 2f / upperValues.Length;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var target = ShapeTarget(upperTable, upperValues[i], dir * 0.36f);
                target.transform.localRotation = Quaternion.LookRotation(-dir);
                targets.Add(target);
            }
        }

        // ---- 星の観覧車: 大きな輪のゴンドラと、内側で逆に回る星の輪 ----
        void BuildDxStarWheel(Transform t, List<Target> targets, Material glow)
        {
            const float Radius = 0.8f;
            var center = new Vector3(0f, 1.6f, 0.35f);

            foreach (float side in new[] { -1f, 1f })
            {
                var leg = Prim(PrimitiveType.Cube, "WheelLeg", t, new Vector3(side * 0.38f, center.y / 2f, center.z + 0.14f), new Vector3(0.07f, center.y + 0.12f, 0.07f), DxGold);
                leg.transform.localRotation = Quaternion.Euler(0f, 0f, side * 13f);
            }
            Prim(PrimitiveType.Cube, "WheelBase", t, new Vector3(0f, 0.04f, center.z + 0.14f), new Vector3(1.1f, 0.08f, 0.2f), DxBlack, false);

            var wheel = Group(t, "Wheel");
            wheel.localPosition = center;
            var rotator = wheel.gameObject.AddComponent<Rotator>();
            rotator.axis = Vector3.forward;
            rotator.speed = 18f;
            for (int i = 0; i < 4; i++)
            {
                var spoke = Prim(PrimitiveType.Cube, "Spoke", wheel, new Vector3(0f, 0f, 0.02f), new Vector3(Radius * 2f, 0.022f, 0.022f), DxBlack, false);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
            }
            RingSegments(wheel, "Rim", Radius, 32, 0.03f, DxGold, glow, 0.02f);
            RingSegments(wheel, "InnerRim", 0.55f, 24, 0.015f, glow, null, 0.03f);
            var wheelBulbs = new List<Renderer>();
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16;
                wheelBulbs.Add(Bulb(wheel, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (Radius + 0.05f) + Vector3.forward * 0.01f, 0.04f));
            }
            Marquee(wheel, wheelBulbs, 2, 0.15f, false);

            int[] values = { 200, 300, 500, 200, 1000, 300, 500, 200 };
            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var gondola = Group(wheel, $"Gondola_{i}");
                gondola.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius;
                gondola.gameObject.AddComponent<KeepUpright>().reference = t;
                Prim(PrimitiveType.Cube, "Hanger", gondola, new Vector3(0f, -0.1f, 0f), new Vector3(0.012f, 0.2f, 0.012f), DxGold, false);
                Prim(PrimitiveType.Cube, "Floor", gondola, new Vector3(0f, -0.2f, 0f), new Vector3(0.26f, 0.03f, 0.14f), DxBlack);
                Prim(PrimitiveType.Cube, "FloorTrim", gondola, new Vector3(0f, -0.2f, -0.072f), new Vector3(0.26f, 0.012f, 0.005f), DxGold, false);
                targets.Add(ShapeTarget(gondola, values[i], new Vector3(0f, -0.185f, 0f)));
            }

            // 内側の星の輪 (逆回り。的はいつも正面を向く)
            var starRing = Group(t, "StarRing");
            starRing.localPosition = center + Vector3.back * 0.1f;
            var starSpin = starRing.gameObject.AddComponent<Rotator>();
            starSpin.axis = Vector3.forward;
            starSpin.speed = -40f;
            int[] starValues = { 500, 300, 500, 300, 300 };
            for (int i = 0; i < starValues.Length; i++)
            {
                float a = i * Mathf.PI * 2f / starValues.Length + Mathf.PI / 2f;
                var holder = Group(starRing, $"Star_{i}");
                holder.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.36f;
                holder.gameObject.AddComponent<KeepUpright>().reference = t;
                targets.Add(ShapeTarget(holder, starValues[i], new Vector3(0f, -TargetDiscY, 0f)));
            }
            Prim(PrimitiveType.Sphere, "Hub", t, center + Vector3.back * 0.02f, Vector3.one * 0.14f, glow, false);
        }

        // ---- シャンデリア: 回るシャンデリアの腕から、的が振り子のように揺れる (的はいつも正面を向く) ----
        void BuildDxChandelier(Transform t, List<Target> targets, Material glow)
        {
            Prim(PrimitiveType.Cube, "Beam", t, new Vector3(0f, 2.68f, 0.2f), new Vector3(3.3f, 0.07f, 0.07f), DxGold);
            DxChandelier(t, targets, glow, "Chandelier_Center", new Vector3(0f, 2.45f, 0.2f), 0.6f, 22f,
                         new[] { 300, 500, 200, 1000, 300, 200 }, new[] { 0.45f, 0.7f, 0.55f, 0.75f, 0.5f, 0.65f });
            DxChandelier(t, targets, glow, "Chandelier_Left", new Vector3(-1.15f, 2.5f, 0.2f), 0.28f, -35f,
                         new[] { 100, 200, 300 }, new[] { 0.55f, 0.75f, 0.65f });
            DxChandelier(t, targets, glow, "Chandelier_Right", new Vector3(1.15f, 2.5f, 0.2f), 0.28f, 35f,
                         new[] { 200, 100, 500 }, new[] { 0.7f, 0.5f, 0.6f });
        }

        void DxChandelier(Transform t, List<Target> targets, Material glow, string name, Vector3 pos, float armRadius, float speed, int[] values, float[] lengths)
        {
            Prim(PrimitiveType.Cylinder, name + "_Chain", t, new Vector3(pos.x, (2.68f + pos.y) / 2f, pos.z), new Vector3(0.015f, (2.68f - pos.y) / 2f, 0.015f), DxGold, false);
            var body = Group(t, name);
            body.localPosition = pos;
            var spin = body.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.up;
            spin.speed = speed;

            Prim(PrimitiveType.Sphere, "Core", body, Vector3.zero, Vector3.one * (armRadius * 0.3f + 0.06f), DxGold, false);
            Prim(PrimitiveType.Cylinder, "Tier1", body, Vector3.down * 0.04f, new Vector3(armRadius * 0.9f, 0.01f, armRadius * 0.9f), DxGold, false);
            Prim(PrimitiveType.Sphere, "Crystal", body, Vector3.down * 0.13f, new Vector3(0.07f, 0.12f, 0.07f), glow, false);

            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var arm = Prim(PrimitiveType.Cube, "Arm", body, dir * armRadius / 2f, new Vector3(0.02f, 0.02f, armRadius), DxGold, false);
                arm.transform.localRotation = Quaternion.LookRotation(dir);
                Prim(PrimitiveType.Cylinder, "Candle", body, dir * armRadius + Vector3.up * 0.04f, new Vector3(0.025f, 0.035f, 0.025f), DxIvory, false);
                Prim(PrimitiveType.Sphere, "Flame", body, dir * armRadius + Vector3.up * 0.095f, new Vector3(0.025f, 0.04f, 0.025f), DxBulbOn, false);
                Prim(PrimitiveType.Sphere, "Drop", body, dir * armRadius * 0.6f + Vector3.down * 0.05f, Vector3.one * 0.03f, glow, false);

                // 腕の先: いつも正面を向く吊り具 → 振り子 → 鎖と的
                var hanger = Group(body, $"Hanger_{i}");
                hanger.localPosition = dir * armRadius + Vector3.down * 0.02f;
                hanger.gameObject.AddComponent<KeepUpright>().reference = t;
                var swing = Group(hanger, "Swing");
                var pendulum = swing.gameObject.AddComponent<Pendulum>();
                pendulum.amplitude = 18f;
                pendulum.speed = 1.5f + 0.3f * i;
                pendulum.phase = i * 1.1f;
                float len = lengths[i % lengths.Length];
                Prim(PrimitiveType.Cube, "Chain", swing, new Vector3(0f, -len / 2f, 0f), new Vector3(0.01f, len, 0.01f), DxGoldDark, false);
                float d = DxSize(values[i]);
                targets.Add(ShapeTarget(swing, values[i], new Vector3(0f, -len - d / 2f - TargetDiscY, 0f)));
            }
        }

        // ---- プラネタリウム: 光る太陽のまわりを、傾いた3本の軌道で的が回る。1つの惑星には衛星 (1000 点) ----
        void BuildDxOrbit(Transform t, List<Target> targets, Material glow)
        {
            var center = new Vector3(0f, 1.65f, 0.1f);
            Prim(PrimitiveType.Cylinder, "Stand", t, new Vector3(0f, center.y / 2f, center.z + 0.3f), new Vector3(0.06f, center.y / 2f, 0.06f), DxGold, false);
            Prim(PrimitiveType.Cylinder, "StandBase", t, new Vector3(0f, 0.04f, center.z + 0.3f), new Vector3(0.6f, 0.04f, 0.6f), DxBlack, false);
            // 太陽 (当たり判定なし: 後ろを通る的を撃てるように) と回る光の筋
            Prim(PrimitiveType.Sphere, "Sun", t, center, Vector3.one * 0.3f, Mat("Dx_Sun", new Color(1f, 0.7f, 0.2f), new Color(2.2f, 1.3f, 0.3f)), false);
            var corona = Group(t, "Corona");
            corona.localPosition = center;
            var coronaSpin = corona.gameObject.AddComponent<Rotator>();
            coronaSpin.axis = Vector3.forward;
            coronaSpin.speed = 30f;
            for (int i = 0; i < 8; i++)
            {
                var ray = Prim(PrimitiveType.Cube, "SunRay", corona, Vector3.zero, new Vector3(0.012f, 0.5f, 0.012f), DxGold, false);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, i * 22.5f);
            }

            DxOrbitRing(t, targets, glow, "Ring_A", center, Quaternion.Euler(62f, 0f, 0f), 0.72f, 32f, new[] { 200, 300, 100, 500 }, 1);
            DxOrbitRing(t, targets, glow, "Ring_B", center, Quaternion.Euler(22f, 48f, 0f), 0.68f, -26f, new[] { 300, 100, 200, 300 }, -1);
            DxOrbitRing(t, targets, glow, "Ring_C", center, Quaternion.Euler(22f, -48f, 0f), 0.64f, 40f, new[] { 100, 200, 500, 200 }, -1);
        }

        void DxOrbitRing(Transform t, List<Target> targets, Material glow, string name, Vector3 center, Quaternion tilt, float radius, float speed, int[] values, int moonIndex)
        {
            var path = Group(t, name + "_Path");
            path.localPosition = center;
            path.localRotation = tilt;
            RingSegments(path, "Path", radius, 36, 0.01f, glow);

            var orbit = Group(t, name);
            orbit.localPosition = center;
            orbit.localRotation = tilt;
            var spin = orbit.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.forward; // 傾いた軌道の面の中で回る
            spin.speed = speed;
            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var holder = Group(orbit, $"Planet_{i}");
                holder.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
                holder.gameObject.AddComponent<KeepUpright>().reference = t;
                targets.Add(ShapeTarget(holder, values[i], new Vector3(0f, -TargetDiscY, 0f)));

                if (i != moonIndex) continue;
                // 衛星: 惑星のまわりを速く回る小さな星
                var moonOrbit = Group(holder, "MoonOrbit");
                var moonSpin = moonOrbit.gameObject.AddComponent<Rotator>();
                moonSpin.axis = Vector3.forward;
                moonSpin.speed = 120f;
                var moon = Group(moonOrbit, "Moon");
                moon.localPosition = new Vector3(0.25f, 0f, -0.03f);
                moon.gameObject.AddComponent<KeepUpright>().reference = t;
                targets.Add(ShapeTarget(moon, 1000, new Vector3(0f, -TargetDiscY, 0f)));
            }
        }

        // ---- 二重ルーレット: 外の輪と内の円盤が逆向きに回る。真ん中は 1000 点 ----
        void BuildDxRoulette(Transform t, List<Target> targets, Material cloth, Material glow)
        {
            var center = new Vector3(0f, 1.65f, 0.5f);
            Prim(PrimitiveType.Cube, "Post", t, new Vector3(0f, center.y / 2f, center.z + 0.14f), new Vector3(0.14f, center.y, 0.08f), DxBlack);
            var plate = Prim(PrimitiveType.Cylinder, "Backplate", t, center + Vector3.forward * 0.07f, new Vector3(2.24f, 0.01f, 2.24f), DxBlack, false);
            plate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 回らない電飾の輪と、上の矢印
            var frame = Group(t, "Frame");
            frame.localPosition = center;
            RingSegments(frame, "OuterRim", 1.04f, 36, 0.04f, DxGold, null, 0.03f);
            var frameBulbs = new List<Renderer>();
            for (int i = 0; i < 28; i++)
            {
                float a = i * Mathf.PI * 2f / 28;
                frameBulbs.Add(Bulb(frame, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.1f + Vector3.forward * 0.02f, 0.045f));
            }
            Marquee(frame, frameBulbs, 3, 0.08f, true);
            var pointer = Prim(PrimitiveType.Cube, "Pointer", t, center + new Vector3(0f, 1.15f, -0.06f), new Vector3(0.1f, 0.1f, 0.03f), glow, false);
            pointer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // 外の輪: 差し色と黒の扇形の板に8つの的
            var outer = Group(t, "OuterRing");
            outer.localPosition = center;
            var outerSpin = outer.gameObject.AddComponent<Rotator>();
            outerSpin.axis = Vector3.forward;
            outerSpin.speed = 28f;
            for (int i = 0; i < 16; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 16;
                var panel = Prim(PrimitiveType.Cube, "Panel", outer, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.83f + Vector3.forward * 0.02f,
                                 new Vector3(0.34f, 0.33f, 0.01f), i % 2 == 0 ? cloth : DxBlack, false);
                panel.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
            RingSegments(outer, "InnerRim", 0.65f, 28, 0.025f, DxGold, null, 0.01f);
            int[] outerValues = { 100, 200, 300, 200, 100, 500, 200, 300 };
            for (int i = 0; i < outerValues.Length; i++)
            {
                float a = i * Mathf.PI * 2f / outerValues.Length;
                var pos = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.83f;
                targets.Add(ShapeTarget(outer, outerValues[i], pos + new Vector3(0f, -TargetDiscY, -0.03f)));
            }

            // 内の円盤: 逆向きに速く回る。金と光る差し色の縞、5つの的と真ん中の 1000 点
            var inner = Group(t, "InnerDisc");
            inner.localPosition = center + Vector3.back * 0.01f;
            var innerSpin = inner.gameObject.AddComponent<Rotator>();
            innerSpin.axis = Vector3.forward;
            innerSpin.speed = -55f;
            var disc = Prim(PrimitiveType.Cylinder, "Disc", inner, Vector3.forward * 0.005f, new Vector3(1.24f, 0.01f, 1.24f), DxBlack, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int k = 0; k < 6; k++)
            {
                var stripe = Prim(PrimitiveType.Cube, "Stripe", inner, Vector3.back * (0.002f + k * 0.002f), new Vector3(1.2f, 0.05f, 0.004f), k % 2 == 0 ? DxGold : glow, false);
                stripe.transform.localRotation = Quaternion.Euler(0f, 0f, k * 30f);
            }
            int[] innerValues = { 300, 500, 300, 500, 300 };
            for (int i = 0; i < innerValues.Length; i++)
            {
                float a = i * Mathf.PI * 2f / innerValues.Length;
                var pos = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.42f;
                targets.Add(ShapeTarget(inner, innerValues[i], pos + new Vector3(0f, -TargetDiscY, -0.04f)));
            }
            targets.Add(ShapeTarget(inner, 1000, new Vector3(0f, -TargetDiscY, -0.05f)));
        }

        // ---- ロイヤルエクスプレス: 手前を蒸気機関車が客車を引いて走り、奥を熱気球がふわふわ逆向きに流れる ----
        void BuildDxExpress(Transform t, List<Target> targets, Material cloth, Material glow)
        {
            const float LoopLength = 3.6f; // 両端のトンネル / 幕の中で折り返す
            const float TrackY = 0.95f, TrackZ = 0.0f;

            // 線路
            Prim(PrimitiveType.Cube, "TrackBed", t, new Vector3(0f, (TrackY - 0.02f) / 2f, TrackZ), new Vector3(3.0f, TrackY - 0.02f, 0.3f), DxBlack);
            Prim(PrimitiveType.Cube, "TrackGlow", t, new Vector3(0f, TrackY - 0.1f, TrackZ - 0.151f), new Vector3(3.0f, 0.012f, 0.005f), glow, false);
            foreach (float rz in new[] { -0.06f, 0.06f })
                Prim(PrimitiveType.Cube, "Rail", t, new Vector3(0f, TrackY - 0.01f, TrackZ + rz), new Vector3(3.0f, 0.02f, 0.015f), DxGold, false);
            // 両端のトンネル (黒いレンガ + 金のアーチ)
            foreach (float x in new[] { -1.6f, 1.6f })
            {
                Prim(PrimitiveType.Cube, "Tunnel", t, new Vector3(x, TrackY + 0.4f, TrackZ), new Vector3(0.9f, 0.8f, 0.42f), DxBlack);
                Prim(PrimitiveType.Cube, "TunnelOpening", t, new Vector3(x - Mathf.Sign(x) * 0.15f, TrackY + 0.3f, TrackZ - 0.212f), new Vector3(0.36f, 0.6f, 0.005f), DxOpening, false);
                Prim(PrimitiveType.Cube, "TunnelArch", t, new Vector3(x - Mathf.Sign(x) * 0.15f, TrackY + 0.62f, TrackZ - 0.214f), new Vector3(0.42f, 0.035f, 0.01f), DxGold, false);
                Prim(PrimitiveType.Cube, "TunnelTop", t, new Vector3(x, TrackY + 0.8f, TrackZ - 0.215f), new Vector3(0.92f, 0.04f, 0.012f), DxGold, false);
            }

            var train = Group(t, "Train");
            train.localPosition = new Vector3(0f, TrackY, TrackZ);
            var trainItems = new List<Transform>();
            int[][] carValues = { new[] { 100, 200 }, new[] { 300, 200 }, new[] { 200, 100 } };
            for (int i = 0; i < 4; i++)
            {
                var unit = Group(train, i == 3 ? "Locomotive" : $"Car_{i}");
                unit.localPosition = new Vector3(-LoopLength / 2f + (i + 0.5f) * LoopLength / 4f, 0f, 0f);
                if (i == 3) BuildLocomotive(unit, targets, glow);
                else BuildCoach(unit, carValues[i], targets, i % 2 == 0 ? cloth : DxBlack, glow);
                trainItems.Add(unit);
            }
            var trainBelt = train.gameObject.AddComponent<ConveyorBelt>();
            trainBelt.items = trainItems.ToArray();
            trainBelt.length = LoopLength;
            trainBelt.speed = 0.45f; // 機関車 (一番右) が先頭

            // 奥: 熱気球 (左右の幕の中で折り返す)
            const float SkyY = 1.55f, SkyZ = 0.45f;
            foreach (float x in new[] { -1.6f, 1.6f })
            {
                Prim(PrimitiveType.Cube, "Curtain", t, new Vector3(x, 2.05f, SkyZ), new Vector3(0.9f, 1.1f, 0.42f), DxVelvet);
                Prim(PrimitiveType.Cube, "CurtainTrim", t, new Vector3(x - Mathf.Sign(x) * 0.45f, 2.05f, SkyZ - 0.1f), new Vector3(0.02f, 1.1f, 0.2f), DxGold, false);
                Prim(PrimitiveType.Sphere, "Tassel", t, new Vector3(x - Mathf.Sign(x) * 0.46f, 1.7f, SkyZ - 0.2f), new Vector3(0.05f, 0.09f, 0.05f), DxGold, false);
            }
            var sky = Group(t, "Balloons");
            sky.localPosition = new Vector3(0f, SkyY, SkyZ);
            var balloonItems = new List<Transform>();
            int[] balloonValues = { 300, 500, 1000, 200 };
            for (int i = 0; i < balloonValues.Length; i++)
            {
                var balloon = Group(sky, $"Balloon_{i}");
                balloon.localPosition = new Vector3(-LoopLength / 2f + (i + 0.5f) * LoopLength / balloonValues.Length, 0f, 0f);
                var envelope = i % 2 == 0 ? glow : cloth;
                Prim(PrimitiveType.Sphere, "Envelope", balloon, new Vector3(0f, 0.68f, 0.03f), new Vector3(0.3f, 0.36f, 0.3f), envelope, false);
                Prim(PrimitiveType.Cylinder, "Band", balloon, new Vector3(0f, 0.62f, 0.03f), new Vector3(0.31f, 0.015f, 0.31f), DxGold, false);
                Prim(PrimitiveType.Cube, "Basket", balloon, new Vector3(0f, 0.0f, 0.03f), new Vector3(0.1f, 0.06f, 0.1f), DxGoldDark, false);
                foreach (float s in new[] { -0.045f, 0.045f })
                    Prim(PrimitiveType.Cube, "Rope", balloon, new Vector3(s, 0.25f, 0.03f), new Vector3(0.005f, 0.48f, 0.005f), DxGoldDark, false);
                targets.Add(ShapeTarget(balloon, balloonValues[i], new Vector3(0f, 0.03f, -0.04f)));
                balloonItems.Add(balloon);
            }
            var skyBelt = sky.gameObject.AddComponent<ConveyorBelt>();
            skyBelt.items = balloonItems.ToArray();
            skyBelt.length = LoopLength;
            skyBelt.speed = -0.32f;
            skyBelt.waveHeight = 0.1f;
            skyBelt.waves = 1.5f;
            skyBelt.waveTargetsOnly = false;
        }

        // 蒸気機関車: 黒いボイラーに金の煙突と帯、白い運転室、光るヘッドライト、湯気。運転室の屋根に的
        void BuildLocomotive(Transform unit, List<Target> targets, Material glow)
        {
            var boiler = Prim(PrimitiveType.Cylinder, "Boiler", unit, new Vector3(0.06f, 0.15f, 0f), new Vector3(0.16f, 0.17f, 0.16f), DxBlack);
            boiler.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float x in new[] { -0.04f, 0.1f })
            {
                var band = Prim(PrimitiveType.Cylinder, "BoilerBand", unit, new Vector3(x, 0.15f, 0f), new Vector3(0.165f, 0.01f, 0.165f), DxGold, false);
                band.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            Prim(PrimitiveType.Cylinder, "Chimney", unit, new Vector3(0.15f, 0.28f, 0f), new Vector3(0.05f, 0.06f, 0.05f), DxGold, false);
            Prim(PrimitiveType.Sphere, "Headlight", unit, new Vector3(0.24f, 0.17f, 0f), Vector3.one * 0.05f, DxBulbOn, false);
            var catcher = Prim(PrimitiveType.Cube, "Cowcatcher", unit, new Vector3(0.24f, 0.04f, 0f), new Vector3(0.06f, 0.06f, 0.16f), DxGold, false);
            catcher.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Prim(PrimitiveType.Cube, "Cab", unit, new Vector3(-0.15f, 0.17f, 0f), new Vector3(0.16f, 0.2f, 0.18f), DxIvory);
            Prim(PrimitiveType.Cube, "CabWindow", unit, new Vector3(-0.15f, 0.21f, -0.091f), new Vector3(0.1f, 0.05f, 0.004f), glow, false);
            Prim(PrimitiveType.Cube, "CabRoof", unit, new Vector3(-0.15f, 0.28f, 0f), new Vector3(0.2f, 0.02f, 0.2f), DxGold, false);
            var steam = Mat("Dx_Steam", new Color(0.9f, 0.9f, 0.95f), new Color(0.25f, 0.25f, 0.28f));
            Prim(PrimitiveType.Sphere, "Steam", unit, new Vector3(0.13f, 0.4f, 0.02f), Vector3.one * 0.08f, steam, false);
            Prim(PrimitiveType.Sphere, "Steam", unit, new Vector3(0.06f, 0.48f, 0.03f), Vector3.one * 0.1f, steam, false);
            Wheels(unit, new[] { -0.15f, 0f, 0.12f }, 0.075f);
            targets.Add(ShapeTarget(unit, 500, new Vector3(-0.15f, 0.29f, 0f)));
        }

        // 客車: 差し色 / 黒の車体に金の帯と光る窓。屋根の前後に的
        void BuildCoach(Transform unit, int[] values, List<Target> targets, Material body, Material glow)
        {
            Prim(PrimitiveType.Cube, "Body", unit, new Vector3(0f, 0.13f, 0f), new Vector3(0.6f, 0.15f, 0.19f), body);
            Prim(PrimitiveType.Cube, "Trim", unit, new Vector3(0f, 0.07f, -0.096f), new Vector3(0.6f, 0.015f, 0.004f), DxGold, false);
            for (int k = -2; k <= 2; k++)
                Prim(PrimitiveType.Cube, "Window", unit, new Vector3(k * 0.11f, 0.15f, -0.096f), new Vector3(0.07f, 0.045f, 0.004f), glow, false);
            Prim(PrimitiveType.Cube, "Roof", unit, new Vector3(0f, 0.215f, 0f), new Vector3(0.62f, 0.02f, 0.2f), DxGold, false);
            Prim(PrimitiveType.Cube, "Coupler", unit, new Vector3(0.34f, 0.06f, 0f), new Vector3(0.1f, 0.02f, 0.02f), DxGoldDark, false);
            Wheels(unit, new[] { -0.2f, 0.2f }, 0.06f);
            targets.Add(ShapeTarget(unit, values[0], new Vector3(-0.17f, 0.225f, 0f)));
            targets.Add(ShapeTarget(unit, values[1], new Vector3(0.17f, 0.225f, 0f)));
        }

        void Wheels(Transform unit, float[] xs, float diameter)
        {
            foreach (float x in xs)
            {
                var w = Prim(PrimitiveType.Cylinder, "Wheel", unit, new Vector3(x, diameter / 2f, -0.1f), new Vector3(diameter, 0.01f, diameter), DxBlack, false);
                w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var hub = Prim(PrimitiveType.Cylinder, "Hub", unit, new Vector3(x, diameter / 2f, -0.106f), new Vector3(diameter * 0.4f, 0.004f, diameter * 0.4f), DxGold, false);
                hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        // ---- 噴水: 奥では噴き上がる水に乗った的が上下し、手前では噴水のまわりの回転台が回る ----
        void BuildDxFountain(Transform t, List<Target> targets, Material glow)
        {
            const float BackZ = 0.45f;
            var jet = TransparentMat("Dx_Jet", new Color(0.55f, 0.85f, 1f, 0.45f));

            // 奥の水盤と5本の噴水
            Prim(PrimitiveType.Cube, "Basin", t, new Vector3(0f, 0.45f, BackZ), new Vector3(3.0f, 0.9f, 0.3f), DxBlack);
            Prim(PrimitiveType.Cube, "BasinTrim", t, new Vector3(0f, 0.9f, BackZ - 0.151f), new Vector3(3.02f, 0.025f, 0.01f), DxGold, false);
            Prim(PrimitiveType.Cube, "BasinGlow", t, new Vector3(0f, 0.8f, BackZ - 0.152f), new Vector3(3.0f, 0.012f, 0.005f), glow, false);
            Prim(PrimitiveType.Cube, "BasinWater", t, new Vector3(0f, 0.905f, BackZ), new Vector3(2.96f, 0.01f, 0.26f), DxWater, false);
            float[] xs = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };
            int[] values = { 200, 300, 1000, 300, 200 };
            float[] speeds = { 1.1f, 1.8f, 2.4f, 1.6f, 1.2f };
            for (int i = 0; i < xs.Length; i++)
            {
                var lily = Group(t, $"Jet_{i}");
                lily.localPosition = new Vector3(xs[i], 1.45f, BackZ);
                var osc = lily.gameObject.AddComponent<Oscillator>();
                osc.offset = new Vector3(0f, 0.4f, 0f);
                osc.speed = speeds[i];
                osc.phase = i * 1.2f;
                // 水柱は台から下へ伸ばす (低い時は水盤の中に隠れる)
                Prim(PrimitiveType.Cylinder, "Water", lily, new Vector3(0f, -0.5f, 0f), new Vector3(0.06f, 0.5f, 0.06f), jet, false);
                Prim(PrimitiveType.Cylinder, "Splash", lily, new Vector3(0f, -0.01f, 0f), new Vector3(0.3f, 0.004f, 0.3f), jet, false);
                Prim(PrimitiveType.Cylinder, "Pad", lily, new Vector3(0f, -0.02f, 0f), new Vector3(0.22f, 0.012f, 0.22f), DxGold);
                targets.Add(ShapeTarget(lily, values[i], Vector3.zero));
            }

            // 手前: 金の柱に乗った回転台。真ん中に小さな噴水と回る星
            var tableCenter = new Vector3(0f, 1.0f, -0.35f);
            Prim(PrimitiveType.Cylinder, "Pedestal", t, new Vector3(tableCenter.x, tableCenter.y / 2f, tableCenter.z), new Vector3(0.14f, tableCenter.y / 2f, 0.14f), DxGold);
            var table = Group(t, "Turntable");
            table.localPosition = tableCenter;
            var spin = table.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.up;
            spin.speed = 30f;
            Prim(PrimitiveType.Cylinder, "Disc", table, new Vector3(0f, -0.02f, 0f), new Vector3(1.1f, 0.02f, 1.1f), DxBlack);
            Prim(PrimitiveType.Cylinder, "DiscRim", table, new Vector3(0f, -0.035f, 0f), new Vector3(1.14f, 0.012f, 1.14f), DxGold, false);
            Prim(PrimitiveType.Cylinder, "Spout", table, new Vector3(0f, 0.08f, 0f), new Vector3(0.06f, 0.08f, 0.06f), DxGold, false);
            Prim(PrimitiveType.Cylinder, "SpoutWater", table, new Vector3(0f, 0.3f, 0f), new Vector3(0.04f, 0.15f, 0.04f), jet, false);
            var star = Group(table, "SpoutStar");
            star.localPosition = new Vector3(0f, 0.5f, 0f);
            var starSpin = star.gameObject.AddComponent<Rotator>();
            starSpin.axis = Vector3.up;
            starSpin.speed = -90f;
            ShapeObj(Shape.Star, star, "Star", Vector3.zero, 0.16f, glow, false, 0.03f);
            int[] ringValues = { 100, 200, 100, 300, 100, 200, 500 };
            for (int i = 0; i < ringValues.Length; i++)
            {
                float a = i * Mathf.PI * 2f / ringValues.Length;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var target = ShapeTarget(table, ringValues[i], dir * 0.44f);
                target.transform.localRotation = Quaternion.LookRotation(-dir);
                targets.Add(target);
            }
        }

        // ------------------------------------------------------------------
        // 形のある的
        // ------------------------------------------------------------------

        static Shape ShapeFor(int points) => points >= 500 ? Shape.Star : points >= 300 ? Shape.Triangle : points >= 200 ? Shape.Square : Shape.Circle;

        /// <summary>的の形の大きさ (外接円の直径)。とがった形は面積が小さいので少し大きくする</summary>
        static float DxSize(int points)
        {
            float d = PointStyle.Diameter(points);
            return ShapeFor(points) switch
            {
                Shape.Star => d * 1.5f,
                Shape.Triangle => d * 1.3f,
                _ => d * 1.05f
            };
        }

        /// <summary>
        /// 豪華屋台の点数付きの的。点数で形が変わる (丸 / 四角 / 三角 / 星)。金の縁取り、光る色の板、真ん中の黒い丸に金の数字。
        /// 根元が原点で、形の中心は TargetDiscY の高さ (普通の的と同じなので、武器の的に化ける時も同じ所に箱が付く)。
        /// </summary>
        Target ShapeTarget(Transform parent, int points, Vector3 localPos, float respawnDelay = 3f)
        {
            var shape = ShapeFor(points);
            var root = Group(parent, $"Target_{points}");
            root.localPosition = localPos;
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Normal;
            target.points = points;
            target.respawnDelay = respawnDelay;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;

            float d = DxSize(points);
            float standTop = TargetDiscY - d * 0.35f;
            Prim(PrimitiveType.Cube, "Stand", pivot, new Vector3(0f, standTop / 2f, 0f), new Vector3(0.03f, standTop, 0.03f), DxGoldDark);

            var face = Group(pivot, "Face");
            target.face = face.gameObject;
            var c = PointStyle.TargetColor(points);
            var body = Mat($"Dx_Points_{points}", c, c * 0.45f, false, 0.6f);
            var center = new Vector3(0f, TargetDiscY, 0f);
            ShapeObj(shape, face, "Rim", center + Vector3.forward * 0.006f, d * 1.16f, DxGold, false);
            ShapeObj(shape, face, "Body", center, d, body, true);
            float inner = shape switch { Shape.Star => 0.42f, Shape.Triangle => 0.48f, _ => 0.6f } * d;
            ShapeObj(Shape.Circle, face, "Inner", center + Vector3.back * 0.008f, inner, DxBlack, false);
            MakeText(face, "Points", points.ToString(), center + Vector3.back * 0.016f, inner * (points >= 1000 ? 0.32f : 0.4f), new Color(1f, 0.85f, 0.4f));
            return target;
        }

        /// <summary>平たい形を1つ作る。表 (-Z) を向き、size は外接円の直径。collider = true なら形どおりの当たり判定を付ける</summary>
        GameObject ShapeObj(Shape shape, Transform parent, string name, Vector3 pos, float size, Material mat, bool collider, float thickness = 0.01f)
        {
            switch (shape)
            {
                case Shape.Circle:
                {
                    var go = Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(size, thickness / 2f, size), mat, collider);
                    go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    return go;
                }
                case Shape.Square:
                    return Prim(PrimitiveType.Cube, name, parent, pos, new Vector3(size * 0.78f, size * 0.78f, thickness), mat, collider);
                default:
                {
                    var go = new GameObject(name);
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = pos;
                    go.transform.localScale = new Vector3(size, size, thickness / 0.02f);
                    var mesh = ShapeMesh(shape);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    if (collider)
                    {
                        var mc = go.AddComponent<MeshCollider>();
                        mc.sharedMesh = mesh;
                        mc.convex = true;
                    }
                    return go;
                }
            }
        }

        /// <summary>三角形 / 星形の板のメッシュ (外接円の直径 1, 厚さ 0.02)。シーンに生成する時はアセットとして保存する</summary>
        Mesh ShapeMesh(Shape shape)
        {
            string name = $"Shape_{shape}";
            if (meshes.TryGetValue(name, out var cached)) return cached;

            var outline = new List<Vector2>();
            if (shape == Shape.Triangle)
                for (int i = 0; i < 3; i++)
                {
                    float a = Mathf.PI / 2f + i * Mathf.PI * 2f / 3f;
                    outline.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f);
                }
            else
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    outline.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? 0.5f : 0.24f));
                }

            const float H = 0.01f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // 面ごとに頂点を分けて、向きが合うように三角形の順番を決める (外から見て時計回りが表)
            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, c) = (c, b);
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
            }
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % n];
                // 表と裏 (中心からの扇形。星も中心から見て全部の頂点が見えるので扇形でよい)
                Tri(new Vector3(0f, 0f, -H), new Vector3(p.x, p.y, -H), new Vector3(q.x, q.y, -H), Vector3.back);
                Tri(new Vector3(0f, 0f, H), new Vector3(p.x, p.y, H), new Vector3(q.x, q.y, H), Vector3.forward);
                // 側面 (外向き = 辺の右側。輪郭は反時計回り)
                var outward = new Vector3(q.y - p.y, -(q.x - p.x), 0f);
                Vector3 a0 = new Vector3(p.x, p.y, -H), b0 = new Vector3(q.x, q.y, -H), a1 = new Vector3(p.x, p.y, H), b1 = new Vector3(q.x, q.y, H);
                Tri(a0, b0, b1, outward);
                Tri(a0, b1, a1, outward);
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

        Mesh SaveMeshIfEditing(Mesh mesh, string name)
        {
#if UNITY_EDITOR
            if (!editMode) return mesh;

            EnsureFolder(GeneratedMeshDir);
            string path = $"{GeneratedMeshDir}/{name}.asset";
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.triangles = mesh.triangles;
            existing.normals = mesh.normals;
            existing.RecalculateBounds();
            UnityEditor.EditorUtility.SetDirty(existing);
            DestroyImmediate(mesh);
            return existing;
#else
            return mesh;
#endif
        }

        // ------------------------------------------------------------------
        // DELUXE の的の見た目 / 花火の色
        // ------------------------------------------------------------------

        /// <summary>DELUXE の的: 黒い板に金の縁と金の星、「DELUXE」の文字。まわりを光の粒が回る</summary>
        GameObject DeluxeLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), DxBlack);
            Prim(PrimitiveType.Cube, "Frame_T", look, c + new Vector3(0f, 0.145f, -0.022f), new Vector3(0.3f, 0.015f, 0.006f), DxGold, false);
            Prim(PrimitiveType.Cube, "Frame_B", look, c + new Vector3(0f, -0.145f, -0.022f), new Vector3(0.3f, 0.015f, 0.006f), DxGold, false);
            Prim(PrimitiveType.Cube, "Frame_L", look, c + new Vector3(-0.145f, 0f, -0.022f), new Vector3(0.015f, 0.3f, 0.006f), DxGold, false);
            Prim(PrimitiveType.Cube, "Frame_R", look, c + new Vector3(0.145f, 0f, -0.022f), new Vector3(0.015f, 0.3f, 0.006f), DxGold, false);
            ShapeObj(Shape.Star, look, "Star", c + new Vector3(0f, 0.025f, -0.026f), 0.17f, DxGold, false);
            MakeText(look, "Label", "DELUXE", c + new Vector3(0f, -0.1f, -0.026f), 0.045f, new Color(1f, 0.85f, 0.4f));

            var sparkle = Group(look, "Sparkle");
            sparkle.localPosition = c + Vector3.back * 0.03f;
            var spin = sparkle.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.forward;
            spin.speed = 120f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8;
                Prim(PrimitiveType.Sphere, "Spark", sparkle, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.2f, Vector3.one * (i % 2 == 0 ? 0.03f : 0.02f),
                     i % 2 == 0 ? DxBulbOn : DxGlow(2), false);
            }
            return look.gameObject;
        }

        Material[] FireworkMaterials() => new[]
        {
            TransparentMat("Firework_Gold", new Color(1f, 0.8f, 0.3f, 0.85f)),
            TransparentMat("Firework_Pink", new Color(1f, 0.35f, 0.75f, 0.85f)),
            TransparentMat("Firework_Cyan", new Color(0.3f, 0.9f, 1f, 0.85f)),
            TransparentMat("Firework_Violet", new Color(0.65f, 0.4f, 1f, 0.85f)),
            TransparentMat("Firework_White", new Color(1f, 1f, 0.95f, 0.85f)),
        };
    }
}
