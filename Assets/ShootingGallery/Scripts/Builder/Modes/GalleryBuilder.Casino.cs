using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// カジノモード (カジノの的を撃つと始まる) の生成: 地味な円柱の壁と天井、明かりが点くと現れる豪華な内装
    /// (赤いビロードと金の壁・ネオン・シャンデリア・じゅうたん・大きなサイコロとチップ)、壇の上のスロットマシン、壁のルーレット、カジノの的の見た目。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float CasinoR = 7.2f;   // 円柱の内側の半径 (屋台6軒の輪が入る)
        const float CasinoH = 9f;     // 天井の高さ (普通の3倍ほど)
        const int CasinoPanels = 28;  // 壁の板の数

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

            // 暗闇の中で1つずつ現れる: スポットライトに照らされたルーレット (正面のスロットのまわりは空けて、ぐるりと並べる)
            var steps = new List<GameObject>();
            var wheels = new List<RouletteWheel>();
            int[][] sectorSets =
            {
                new[] { 10, 100, 20, 200, 30, 500, 40, 300 },
                new[] { 50, 10, 300, 20, 100, 30, 500, 40 },
                new[] { 20, 200, 10, 500, 30, 100, 40, 300 },
            };
            float[] angles = { 52f, 104f, 156f, 204f, 256f, 308f };
            for (int i = 0; i < angles.Length; i++)
            {
                var step = Group(root, $"Reveal_{i}");
                var rot = Quaternion.Euler(0f, angles[i], 0f);
                step.localPosition = rot * new Vector3(0f, 0f, CasinoR - 0.25f);
                step.localRotation = rot;
                wheels.Add(BuildRouletteWheel(step, new Vector3(0f, 3.4f, 0f), sectorSets[i % sectorSets.Length], i % 2 == 0 ? 45f : -55f));
                SpotlightGlow(step, new Vector3(0f, 3.4f, 0.05f), 1.6f);
                step.gameObject.SetActive(false);
                steps.Add(step.gameObject);
            }

            // 最後に現れる主役: 正面の壇とスロットマシン
            var finale = Group(root, "Reveal_Slot");
            finale.localPosition = new Vector3(0f, 0f, 4.6f);
            var slot = BuildSlotStage(finale);
            SpotFixture(finale, new Vector3(0f, CasinoH - 1.2f, -2.6f), new Vector3(0f, 2.4f, -0.7f), 1.9f); // 暗闇の中でスロットだけを照らす
            finale.gameObject.SetActive(false);

            var hall = go.AddComponent<CasinoHall>();
            hall.wallPanels = panels;
            hall.ceiling = ceiling;
            hall.showDuringStage = new[] { floor };
            hall.revealSteps = steps.ToArray();
            hall.revealFinale = finale.gameObject;
            hall.luxury = luxury.gameObject;
            hall.slot = slot;
            hall.wheels = wheels.ToArray();
            // 明かりが点く時の演出: ネオンがチカチカと点き、広間の明かりがふっと強く点いて落ち着き、紙吹雪が舞う
            var neon = new List<Renderer>();
            foreach (var r in luxury.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Neon")) neon.Add(r);
            hall.neon = neon.ToArray();
            hall.hallLights = luxury.GetComponentsInChildren<Light>(true);
            hall.confettiMaterials = new[]
            {
                Mat("Confetti_Gold", new Color(1f, 0.82f, 0.3f), new Color(0.6f, 0.45f, 0.1f), true), Mat("Confetti_Pink", new Color(1f, 0.4f, 0.7f), null, true),
                Mat("Confetti_Cyan", new Color(0.4f, 0.95f, 1f), null, true), Mat("Confetti_White", new Color(1f, 1f, 1f), null, true),
                Mat("Confetti_Red", new Color(0.95f, 0.15f, 0.2f), null, true)
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
        /// 豪華な内装: 壁に赤いビロードと金の柱・金の帯、上の方に2本のネオンの輪、正面の壁に「CASINO」のネオン、
        /// 天井の金の飾りとシャンデリア (明かり)、赤いじゅうたんに金の輪の模様、壁ぎわの大きなサイコロとチップの山。
        /// </summary>
        void BuildCasinoLuxury(Transform lux)
        {
            var velvet = Mat("Casino_Velvet", new Color(0.45f, 0.04f, 0.1f), new Color(0.08f, 0f, 0.02f), false, 0.3f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var neonPink = Mat("Neon_Pink", new Color(1f, 0.3f, 0.7f), new Color(2.5f, 0.6f, 1.6f), true);
            var neonCyan = Mat("Neon_Cyan", new Color(0.3f, 1f, 1f), new Color(0.6f, 2.5f, 2.5f), true);
            var carpet = Mat("Casino_Carpet", new Color(0.55f, 0.06f, 0.1f), null, false, 0.15f);

            float pw = 2f * Mathf.PI * CasinoR / CasinoPanels + 0.06f;
            for (int i = 0; i < CasinoPanels; i++)
            {
                var rot = Quaternion.Euler(0f, i * 360f / CasinoPanels, 0f);
                var p = Group(lux, "LuxPanel");
                p.localPosition = rot * new Vector3(0f, 0f, CasinoR - 0.02f);
                p.localRotation = rot;
                Prim(PrimitiveType.Cube, "Velvet", p, new Vector3(0f, 3.8f, 0f), new Vector3(pw, 7.2f, 0.05f), velvet, false);
                Prim(PrimitiveType.Cube, "Pilaster", p, new Vector3(pw / 2f, 3.8f, -0.06f), new Vector3(0.14f, 7.4f, 0.08f), gold, false);
                Prim(PrimitiveType.Cube, "Wainscot", p, new Vector3(0f, 0.3f, -0.03f), new Vector3(pw, 0.6f, 0.06f), Mat("Dx_Black", new Color(0.05f, 0.05f, 0.06f), null, false, 0.6f), false);
            }
            foreach (float y in new[] { 0.62f, 7.4f })
                FlatRingVertical(lux, "GoldBand", CasinoR - 0.08f, y, 0.12f, gold);
            FlatRingVertical(lux, "NeonPink", CasinoR - 0.12f, 7.9f, 0.08f, neonPink);
            FlatRingVertical(lux, "NeonCyan", CasinoR - 0.12f, 8.4f, 0.08f, neonCyan);

            // 正面の壁の「CASINO」のネオン (スロットの上)
            var sign = Group(lux, "CasinoSign");
            sign.localPosition = new Vector3(0f, 6.4f, CasinoR - 0.15f);
            Prim(PrimitiveType.Cube, "SignBack", sign, Vector3.zero, new Vector3(4.6f, 1.2f, 0.05f), Mat("Dx_Black", new Color(0.05f, 0.05f, 0.06f), null, false, 0.6f), false);
            MakeText(sign, "Neon", "CASINO", new Vector3(0f, 0f, -0.05f), 0.9f, new Color(1f, 0.35f, 0.75f));
            MakeText(sign, "Neon2", "- JACKPOT 777 -", new Vector3(0f, -0.95f, -0.05f), 0.35f, new Color(0.4f, 1f, 1f));

            // じゅうたん
            Prim(PrimitiveType.Cylinder, "Carpet", lux, new Vector3(0f, 0.035f, 0f), new Vector3(2f * CasinoR, 0.005f, 2f * CasinoR), carpet, false);
            foreach (float r in new[] { 2.2f, 4.2f, 6.2f })
                FlatRing(lux, "CarpetRing", r, 0.04f, 0.08f, 0.004f, 48, gold);

            // 天井の金の飾りとシャンデリア
            Prim(PrimitiveType.Cylinder, "CeilingInner", lux, new Vector3(0f, CasinoH - 0.03f, 0f), new Vector3(2f * CasinoR, 0.02f, 2f * CasinoR), Mat("Casino_CeilingLux", new Color(0.18f, 0.03f, 0.06f), new Color(0.04f, 0f, 0.01f)), false);
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
            AddPointLight(chand, "ChandelierLight", new Vector3(0f, -0.8f, 0f), new Color(1f, 0.85f, 0.65f), 3f, 16f);
            AddPointLight(lux, "SlotSpot", new Vector3(0f, 5f, 2.8f), new Color(1f, 0.75f, 0.8f), 2.5f, 9f);

            // 壁ぎわの大きなサイコロとチップの山
            var dieMat = Mat("Casino_Die", new Color(0.9f, 0.1f, 0.12f), new Color(0.15f, 0f, 0f), false, 0.7f);
            var pip = Mat("Target_White", Color.white);
            var chipMats = new[] { Mat("Chip_Red", new Color(0.85f, 0.1f, 0.1f)), Mat("Chip_Blue", new Color(0.15f, 0.3f, 0.9f)), Mat("Chip_Green", new Color(0.1f, 0.6f, 0.25f)), Mat("Chip_Black", new Color(0.08f, 0.08f, 0.08f)) };
            foreach (float a in new[] { 30f, 130f, 230f, 330f })
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
        // ルーレット
        // ------------------------------------------------------------------

        /// <summary>壁のルーレット: 金の縁の中で、扇形に色分けされた円盤が回る (扇ごとに点数)。真ん中の金の丸は 1000 点</summary>
        RouletteWheel BuildRouletteWheel(Transform parent, Vector3 pos, int[] sectors, float speed)
        {
            const float R = 1.1f;
            var root = Group(parent, "Roulette");
            root.localPosition = pos;
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            Prim(PrimitiveType.Cylinder, "Backing", root, new Vector3(0f, 0f, 0.04f), new Vector3(R * 2.3f, 0.02f, R * 2.3f), Mat("Dx_Black", new Color(0.05f, 0.05f, 0.06f), null, false, 0.6f), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var rim = Group(root, "Rim");
            rim.localPosition = new Vector3(0f, 0f, -0.04f);
            RingSegments(rim, "Rim", R + 0.06f, 40, 0.07f, gold);
            // 回る円盤 (ダーツが刺さる当たり判定もこの中)
            var spinner = Group(root, "Spinner");
            var hit = Prim(PrimitiveType.Cylinder, "Board", spinner, Vector3.zero, new Vector3(R * 2f, 0.03f, R * 2f), Mat("Roulette_Base", new Color(0.1f, 0.1f, 0.1f)));
            hit.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var colors = new[]
            {
                Mat("Roulette_Red", new Color(0.85f, 0.1f, 0.12f), new Color(0.25f, 0.02f, 0.03f)),
                Mat("Roulette_Black", new Color(0.08f, 0.08f, 0.1f), new Color(0.03f, 0.03f, 0.04f)),
            };
            var green = Mat("Roulette_Green", new Color(0.1f, 0.6f, 0.25f), new Color(0.03f, 0.2f, 0.06f));
            int n = sectors.Length;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * 360f / n, a1 = (i + 1) * 360f / n;
                var wedge = new GameObject("Sector");
                wedge.transform.SetParent(spinner, false);
                wedge.transform.localPosition = new Vector3(0f, 0f, -0.017f);
                wedge.AddComponent<MeshFilter>().sharedMesh = WedgeMesh(0.16f, R - 0.03f, a0, a1);
                wedge.AddComponent<MeshRenderer>().sharedMaterial = sectors[i] >= 500 ? green : colors[i % 2];
                float mid = (a0 + a1) / 2f * Mathf.Deg2Rad;
                var label = MakeText(spinner, "Points", sectors[i].ToString(), new Vector3(Mathf.Sin(mid) * R * 0.66f, Mathf.Cos(mid) * R * 0.66f, -0.02f), 0.17f,
                                     sectors[i] >= 500 ? new Color(1f, 0.9f, 0.4f) : Color.white);
                label.transform.localRotation = Quaternion.Euler(0f, 0f, -(a0 + a1) / 2f); // 外向きに読める向き
                // 扇の境目の金の線
                var div = Prim(PrimitiveType.Cube, "Divider", spinner, new Vector3(Mathf.Sin(a0 * Mathf.Deg2Rad), Mathf.Cos(a0 * Mathf.Deg2Rad), 0f) * (R * 0.55f) + Vector3.back * 0.02f,
                               new Vector3(0.02f, R * 0.85f, 0.005f), gold, false);
                div.transform.localRotation = Quaternion.Euler(0f, 0f, -a0);
            }
            var bull = Prim(PrimitiveType.Cylinder, "Bull", spinner, new Vector3(0f, 0f, -0.022f), new Vector3(0.3f, 0.005f, 0.3f), gold, false);
            bull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MakeText(spinner, "BullPoints", "1000", new Vector3(0f, 0f, -0.026f), 0.07f, new Color(0.3f, 0.05f, 0.05f));
            // 上の矢印 (回らない)
            var arrow = Prim(PrimitiveType.Cube, "Pointer", root, new Vector3(0f, R + 0.12f, -0.06f), new Vector3(0.1f, 0.1f, 0.04f), gold, false);
            arrow.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Roulette;
            target.manualControl = true;
            target.stayUpOnHit = true;
            target.multiHitInterval = 0.05f;
            target.hitWobbleDeg = 0f;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var wheel = root.gameObject.AddComponent<RouletteWheel>();
            wheel.target = target;
            wheel.spinner = spinner;
            wheel.sectorPoints = sectors;
            wheel.bullRadius = 0.15f;
            wheel.speed = speed;
            return wheel;
        }

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

        // ------------------------------------------------------------------
        // スロットマシン
        // ------------------------------------------------------------------

        /// <summary>
        /// 壇とスロットマシン (parent の原点が壇の真ん中の床)。赤と金の箱に3つの窓、窓の奥で回る3つのリール、右のレバー、
        /// 上の「SLOT」の看板と、まわりの電球。前に配当表 (出目ごとの点数) を書く。
        /// </summary>
        SlotMachine BuildSlotStage(Transform parent)
        {
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var red = Mat("Slot_Red", new Color(0.75f, 0.06f, 0.1f), new Color(0.15f, 0f, 0.02f), false, 0.6f);
            var black = Mat("Dx_Black", new Color(0.05f, 0.05f, 0.06f), null, false, 0.6f);
            const float StageH = 0.7f;

            // 壇 (2段)
            Prim(PrimitiveType.Cylinder, "Stage", parent, new Vector3(0f, StageH / 2f, 0f), new Vector3(4.4f, StageH / 2f, 3.4f), black);
            FlatRing(parent, "StageTrim", 2.2f, StageH, 0.1f, 0.03f, 40, gold);
            Prim(PrimitiveType.Cylinder, "Step", parent, new Vector3(0f, 0.12f, -0.4f), new Vector3(5f, 0.12f, 3.8f), black, false);

            var root = Group(parent, "SlotMachine");
            root.localPosition = new Vector3(0f, StageH, 0f);
            const float W = 2.6f, H = 3.4f, D = 1.4f, ReelY = 1.75f, FrontZ = -D / 2f;
            // 箱 (中は空洞。窓の所だけ前の板を抜いて、奥のリールが見えるようにする)
            Prim(PrimitiveType.Cube, "Back", root, new Vector3(0f, H / 2f, D / 2f - 0.1f), new Vector3(W, H, 0.2f), red);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Side", root, new Vector3(s * (W / 2f - 0.05f), H / 2f, 0f), new Vector3(0.1f, H, D), red);
            Prim(PrimitiveType.Cube, "Top", root, new Vector3(0f, H - 0.05f, 0f), new Vector3(W, 0.1f, D), red);
            Prim(PrimitiveType.Cube, "Inside", root, new Vector3(0f, ReelY, D / 2f - 0.21f), new Vector3(W - 0.2f, 1f, 0.01f), black, false); // リールの後ろは暗く
            const float WinW = 0.62f, WinH = 0.62f;
            float[] reelX = { -0.72f, 0f, 0.72f };
            Prim(PrimitiveType.Cube, "FrontLow", root, new Vector3(0f, (ReelY - WinH / 2f) / 2f, FrontZ + 0.05f), new Vector3(W, ReelY - WinH / 2f, 0.1f), red);
            Prim(PrimitiveType.Cube, "FrontHigh", root, new Vector3(0f, (ReelY + WinH / 2f + H) / 2f, FrontZ + 0.05f), new Vector3(W, H - ReelY - WinH / 2f, 0.1f), red);
            for (int k = 0; k < 4; k++) // 窓の間の柱
            {
                float left = k == 0 ? -W / 2f : reelX[k - 1] + WinW / 2f;
                float right = k == 3 ? W / 2f : reelX[k] - WinW / 2f;
                Prim(PrimitiveType.Cube, "FrontPillar", root, new Vector3((left + right) / 2f, ReelY, FrontZ + 0.05f), new Vector3(right - left, WinH, 0.1f), red);
            }
            foreach (float x in reelX) // 窓の金の縁
            {
                var frame = Group(root, "WindowFrame");
                frame.localPosition = new Vector3(x, ReelY, FrontZ - 0.01f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Cube, "FrameH", frame, new Vector3(0f, s * WinH / 2f, 0f), new Vector3(WinW + 0.08f, 0.04f, 0.03f), gold, false);
                    Prim(PrimitiveType.Cube, "FrameV", frame, new Vector3(s * WinW / 2f, 0f, 0f), new Vector3(0.04f, WinH + 0.08f, 0.03f), gold, false);
                }
            }
            // 真ん中の当たりの線
            Prim(PrimitiveType.Cube, "PayLine", root, new Vector3(0f, ReelY, FrontZ - 0.03f), new Vector3(W - 0.2f, 0.015f, 0.01f), Mat("Slot_PayLine", new Color(1f, 0.2f, 0.2f), null, true), false);

            // リール (窓の奥で回るドラム。絵柄の板を8枚ぐるりと貼る)
            string[] symbols = { "7", "BAR", "3", "$", "5", "BAR", "3", "5" };
            var reelFace = Mat("Slot_ReelFace", new Color(0.97f, 0.95f, 0.88f), new Color(0.3f, 0.29f, 0.26f));
            const float ReelR = 0.45f;
            float step = 360f / symbols.Length;
            float faceH = 2f * ReelR * Mathf.Tan(step / 2f * Mathf.Deg2Rad) + 0.01f;
            var reels = new Transform[reelX.Length];
            for (int r = 0; r < reelX.Length; r++)
            {
                var reel = Group(root, $"Reel_{r}");
                reel.localPosition = new Vector3(reelX[r], ReelY, FrontZ + ReelR + 0.11f); // 絵柄の板が前の板のすぐ奥に来る
                for (int s = 0; s < symbols.Length; s++)
                {
                    var face = Group(reel, $"Face_{symbols[s]}");
                    face.localRotation = Quaternion.Euler(s * step, 0f, 0f);
                    Prim(PrimitiveType.Cube, "Plate", face, new Vector3(0f, 0f, -ReelR), new Vector3(WinW - 0.04f, faceH, 0.02f), reelFace, false);
                    MakeText(face, "Symbol", symbols[s], new Vector3(0f, 0f, -ReelR - 0.015f), symbols[s] == "BAR" ? 0.2f : 0.34f, SymbolColor(symbols[s]));
                }
                reels[r] = reel;
            }

            // レバー (右横。根元が軸)
            var lever = Group(root, "Lever");
            lever.localPosition = new Vector3(W / 2f + 0.12f, ReelY - 0.3f, 0f);
            Prim(PrimitiveType.Cylinder, "LeverBase", root, new Vector3(W / 2f + 0.08f, ReelY - 0.3f, 0f), new Vector3(0.22f, 0.06f, 0.22f), gold, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cylinder, "LeverPole", lever, new Vector3(0.06f, 0.5f, 0f), new Vector3(0.06f, 0.5f, 0.06f), Mat("Revolver_Steel", new Color(0.25f, 0.25f, 0.28f), null, false, 0.8f), false);
            Prim(PrimitiveType.Sphere, "LeverBall", lever, new Vector3(0.06f, 1.05f, 0f), Vector3.one * 0.22f, Mat("Slot_LeverBall", new Color(0.95f, 0.1f, 0.1f), new Color(0.4f, 0.02f, 0.02f), false, 0.8f), false);

            // 上の看板とまわりの電球
            Prim(PrimitiveType.Cube, "TopSign", root, new Vector3(0f, H + 0.45f, 0f), new Vector3(W, 0.9f, 0.5f), black);
            MakeText(root, "SlotTitle", "SLOT", new Vector3(0f, H + 0.5f, -0.27f), 0.55f, new Color(1f, 0.85f, 0.3f));
            var lampOn = Mat("Slot_LampOn", new Color(1f, 0.95f, 0.6f), new Color(3f, 2.4f, 0.8f), true);
            var lampOff = Mat("Slot_LampOff", new Color(0.35f, 0.3f, 0.2f), null, true);
            var lamps = new List<Renderer>();
            for (int i = 0; i < 12; i++) // 看板のまわり
            {
                float x = -W / 2f + 0.1f + i * (W - 0.2f) / 11f;
                lamps.Add(Prim(PrimitiveType.Sphere, "Lamp", root, new Vector3(x, H + 0.92f, -0.26f), Vector3.one * 0.1f, lampOff, false).GetComponent<Renderer>());
            }
            for (int i = 0; i < 10; i++) // 箱の両側
                foreach (float s in new[] { -1f, 1f })
                    lamps.Add(Prim(PrimitiveType.Sphere, "Lamp", root, new Vector3(s * (W / 2f - 0.05f), 0.3f + i * 0.33f, FrontZ - 0.02f), Vector3.one * 0.09f, lampOff, false).GetComponent<Renderer>());
            var winMats = new[]
            {
                Mat("Slot_Win_0", new Color(1f, 0.2f, 0.2f), new Color(2.5f, 0.4f, 0.4f), true), Mat("Slot_Win_1", new Color(1f, 0.85f, 0.2f), new Color(2.5f, 2f, 0.4f), true),
                Mat("Slot_Win_2", new Color(0.3f, 1f, 0.4f), new Color(0.6f, 2.5f, 0.8f), true), Mat("Slot_Win_3", new Color(0.3f, 0.7f, 1f), new Color(0.6f, 1.5f, 2.5f), true),
                Mat("Slot_Win_4", new Color(1f, 0.4f, 0.9f), new Color(2.5f, 0.8f, 2.2f), true)
            };

            // 前の配当表
            var table = Group(root, "PayTable");
            table.localPosition = new Vector3(0f, 0.65f, FrontZ - 0.01f);
            Prim(PrimitiveType.Cube, "TableBoard", table, Vector3.zero, new Vector3(1.9f, 0.85f, 0.02f), black, false);
            MakeText(table, "Pay1", "7 7 7 = 3000   BAR = 1500   $ = 1000", new Vector3(0f, 0.2f, -0.015f), 0.11f, new Color(1f, 0.85f, 0.3f));
            MakeText(table, "Pay2", "5 5 5 = 500   3 3 3 = 300", new Vector3(0f, 0f, -0.015f), 0.11f, new Color(1f, 0.85f, 0.3f));
            MakeText(table, "Pay3", "PAIR = 50   MISS = 10", new Vector3(0f, -0.2f, -0.015f), 0.11f, new Color(0.85f, 0.85f, 0.9f));
            MakeText(table, "Shoot", "SHOOT TO SPIN!", new Vector3(0f, -0.52f, -0.04f), 0.1f, Color.white);

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.SlotMachine;
            target.points = 0;
            target.manualControl = true;
            target.stayUpOnHit = true;
            target.multiHitInterval = 0.3f;
            target.hitWobbleDeg = 0f;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var slot = root.gameObject.AddComponent<SlotMachine>();
            slot.target = target;
            slot.lever = lever;
            slot.reels = reels;
            slot.symbols = symbols;
            slot.lamps = lamps.ToArray();
            slot.lampOn = lampOn;
            slot.lampOff = lampOff;
            slot.winMaterials = winMats;
            return slot;
        }

        static Color SymbolColor(string s) => s switch
        {
            "7" => new Color(0.9f, 0.08f, 0.1f),
            "BAR" => new Color(0.08f, 0.08f, 0.1f),
            "$" => new Color(0.1f, 0.55f, 0.2f),
            "5" => new Color(0.15f, 0.3f, 0.85f),
            _ => new Color(0.75f, 0.45f, 0.05f)
        };

        /// <summary>カジノの的: 桃色の板に赤いチップ (白い切れ込み) と、「CASINO」の文字</summary>
        GameObject CasinoLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Casino_Plate", new Color(0.6f, 0.08f, 0.35f), new Color(0.2f, 0.02f, 0.1f)));
            var spin = Group(look, "Chip");
            spin.localPosition = c + new Vector3(0f, 0.02f, -0.03f);
            var rot = spin.gameObject.AddComponent<Rotator>();
            rot.axis = Vector3.forward;
            rot.speed = 150f;
            var chip = Prim(PrimitiveType.Cylinder, "ChipDisc", spin, Vector3.zero, new Vector3(0.16f, 0.006f, 0.16f), Mat("Chip_Red", new Color(0.85f, 0.1f, 0.1f)), false);
            chip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6;
                var notch = Prim(PrimitiveType.Cube, "Notch", spin, new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * 0.065f + Vector3.back * 0.004f, new Vector3(0.025f, 0.03f, 0.004f), Mat("Target_White", Color.white), false);
                notch.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 60f);
            }
            MakeText(spin, "Seven", "7", new Vector3(0f, 0f, -0.006f), 0.08f, Color.white);
            MakeText(look, "Label", "CASINO", c + new Vector3(0f, -0.115f, -0.025f), 0.04f, new Color(1f, 0.85f, 0.3f));
            return look.gameObject;
        }
    }
}
