using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GalleryBuilder のうち、マシンガンチャレンジの生成。</summary>
    public partial class GalleryBuilder
    {
        /// <summary>
        /// マシンガンチャレンジの横長の屋台 (普通の屋台の 1.8 倍の幅)。カップは全部 100 点。
        /// カップの並べ方は4種類あり、チャレンジのたびに MachineGunChallenge がランダムに1つ選んで表示する。
        ///   SHELF   : 3段の棚にカップがすき間なく並ぶ
        ///   PYRAMID : テーブルの上にカップのピラミッド。下のカップを落とすと上のカップも崩れ落ちる
        ///   TURNTABLE: 回転台の上にカップが輪になって並び、真ん中に小さなピラミッド
        ///   SLIDER  : 3段の棚が左右に行ったり来たりする
        /// チャレンジのたびに、ランダムな3個のカップが爆弾に変わる (MachineGunChallenge.bombLooks の数)。
        /// カップは撃たれると後ろへ弾き飛ばされて床まで落ちる。屋台は隠れた状態で作り、チャレンジのたびに
        /// MachineGunChallenge が撃った屋台の方向に置いてせり上げる。
        /// </summary>
        MachineGunChallenge BuildMachineGunChallenge()
        {
            const float Wide = 1.8f;
            const int BombCount = 3;     // 毎回まぎれこむ爆弾の数
            const float BackWallZ = 0.8f; // 奥の壁の位置 (カップはこの手前で落ちる)

            var go = new GameObject("MachineGunChallenge");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var challenge = go.AddComponent<MachineGunChallenge>();

            var t = Group(go.transform, "Stall_MachineGunChallenge");
            t.localPosition = new Vector3(0f, 0f, stallRadius); // 仮の置き場所 (再生時は MachineGunChallenge が置き直す)
            var cloth = Mat("Cloth_MgChallenge", new Color(0.12f, 0.2f, 0.55f));
            BuildStallFrame(t, cloth, "MACHINE GUN CHALLENGE", Wide);

            var wood = Mat("Wood", new Color(0.55f, 0.36f, 0.2f));
            var woodDark = Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f));
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            var white = Mat("Target_White", Color.white);
            var inside = Mat("Cup_Inside", new Color(0.25f, 0.2f, 0.18f));
            var cupColors = new[]
            {
                Mat("Cup_Red", new Color(0.9f, 0.15f, 0.15f)), Mat("Cup_Blue", new Color(0.2f, 0.45f, 1f)),
                Mat("Cup_Yellow", new Color(1f, 0.8f, 0.15f)), Mat("Cup_Green", new Color(0.2f, 0.75f, 0.3f))
            };

            // カップ: 紙コップ風 (色つきの胴 + 白い帯と縁 + 暗い中)。根元が原点。
            // floorY = 根元の床からの高さ (そこまで落ちる), maxBack = 奥へ飛べる距離 (奥の壁の手前まで)
            const float CupHeight = 0.176f; // 縁の上まで。積む時はこの高さずつ上に乗せる
            Target Cup(Transform parent, string name, Vector3 localPos, int colorIndex, float floorY, float maxBack, List<Target> list)
            {
                var root = Group(parent, name);
                root.localPosition = localPos;
                var target = root.gameObject.AddComponent<Target>();
                target.kind = TargetKind.MachineGunChallengeTarget;
                target.points = 100;
                target.manualControl = true;
                target.knockOffOnHit = true;
                target.knockDropHeight = floorY;                 // 床まで落ちる
                target.knockMaxBack = Mathf.Max(0.1f, maxBack);
                var pivot = Group(root, "Pivot");
                target.visual = pivot;
                // カップの見た目は「顔」にまとめる (爆弾に変わる時は、ここを隠して爆弾の見た目を付ける)
                var cup = Group(pivot, "Cup");
                target.face = cup.gameObject;

                Prim(PrimitiveType.Cylinder, "Body", cup, new Vector3(0f, 0.085f, 0f), new Vector3(0.12f, 0.085f, 0.12f), cupColors[colorIndex % cupColors.Length]);
                Prim(PrimitiveType.Cylinder, "Band", cup, new Vector3(0f, 0.09f, 0f), new Vector3(0.123f, 0.012f, 0.123f), white, false);
                Prim(PrimitiveType.Cylinder, "Rim", cup, new Vector3(0f, 0.172f, 0f), new Vector3(0.132f, 0.008f, 0.132f), white, false);
                Prim(PrimitiveType.Cylinder, "Inside", cup, new Vector3(0f, 0.178f, 0f), new Vector3(0.11f, 0.002f, 0.11f), inside, false);
                target.SetAvailable(false); // チャレンジの時だけ出る
                list.Add(target);
                return target;
            }

            // カップのピラミッド (下の段が baseCount 個、1段ごとに1個ずつ減る)。上のカップは下の2個に乗っていて、どちらかが落ちると崩れる。
            // pos = 一番下の段の真ん中の根元 (parent のローカル), floorY / maxBack は Cup と同じ
            void Pyramid(Transform parent, string name, Vector3 pos, int baseCount, float spacing, float floorY, float maxBack,
                         List<Target> list, List<MachineGunChallenge.CupLink> links)
            {
                Target[] below = null;
                for (int row = 0; row < baseCount; row++)
                {
                    int n = baseCount - row;
                    var cups = new Target[n];
                    for (int i = 0; i < n; i++)
                    {
                        var p = pos + new Vector3((i - (n - 1) / 2f) * spacing, row * CupHeight, 0f);
                        cups[i] = Cup(parent, $"{name}_{row + 1}_{i + 1}", p, row + i, floorY + row * CupHeight, maxBack, list);
                        if (below != null)
                        {
                            links.Add(new MachineGunChallenge.CupLink { lower = below[i], upper = cups[i] });
                            links.Add(new MachineGunChallenge.CupLink { lower = below[i + 1], upper = cups[i] });
                        }
                    }
                    below = cups;
                }
            }

            var layouts = new List<MachineGunChallenge.Layout>();
            MachineGunChallenge.Layout NewLayout(string name, out Transform root, out List<Target> list, out List<MachineGunChallenge.CupLink> links)
            {
                root = Group(t, "Layout_" + name);
                list = new List<Target>();
                links = new List<MachineGunChallenge.CupLink>();
                var layout = new MachineGunChallenge.Layout { name = name, root = root.gameObject };
                layouts.Add(layout);
                return layout;
            }

            // 棚板と細い脚 (カップが板の後ろへ落ちていくのが見えるように、下はふさがない)
            void Shelf(Transform parent, string name, float y, float z, float halfWidth)
            {
                Prim(PrimitiveType.Cube, name, parent, new Vector3(0f, y, z), new Vector3(halfWidth * 2f, 0.04f, 0.24f), wood);
                foreach (float x in new[] { -halfWidth + 0.1f, 0f, halfWidth - 0.1f })
                    Prim(PrimitiveType.Cube, "Leg", parent, new Vector3(x, y / 2f, z + 0.08f), new Vector3(0.05f, y, 0.05f), woodDark, false);
            }

            float[] shelfY = { 1.15f, 1.5f, 1.85f };   // 棚板の中心の高さ (手前ほど低い)
            float[] shelfZ = { -0.75f, -0.25f, 0.25f }; // 棚板の奥行きの位置

            // ---- SHELF: 3段の棚にカップがすき間なく 26 個ずつ並ぶ ----
            {
                const int CupsPerShelf = 26;
                const float CupSpacing = 0.17f; // カップの縁の直径は 0.132 なので、すき間は 4cm くらい
                const float HalfWidth = CupSpacing * (CupsPerShelf - 1) / 2f + 0.15f;
                var layout = NewLayout("SHELF", out var root, out var list, out var links);
                for (int s = 0; s < shelfY.Length; s++)
                {
                    Shelf(root, $"Shelf_{s + 1}", shelfY[s], shelfZ[s], HalfWidth);
                    float top = shelfY[s] + 0.02f;
                    float shift = s % 2 == 1 ? CupSpacing * 0.25f : -CupSpacing * 0.25f; // 段ごとに少しずらす
                    for (int c = 0; c < CupsPerShelf; c++)
                        Cup(root, $"Cup_{s + 1}_{c + 1}", new Vector3((c - (CupsPerShelf - 1) / 2f) * CupSpacing + shift, top, shelfZ[s]),
                            s + c, top, BackWallZ - 0.1f - shelfZ[s], list);
                }
                layout.targets = list.ToArray();
                layout.links = links.ToArray();
            }

            // ---- PYRAMID: 手前に4台 (10個のピラミッド)、奥の高いテーブルに3台 (15個のピラミッド) ----
            {
                const float Spacing = 0.14f;
                var layout = NewLayout("PYRAMID", out var root, out var list, out var links);
                void Table(string name, float x, float topY, float z, float width)
                {
                    Prim(PrimitiveType.Cube, name, root, new Vector3(x, topY - 0.02f, z), new Vector3(width, 0.04f, 0.3f), wood);
                    Prim(PrimitiveType.Cube, "Leg", root, new Vector3(x, (topY - 0.04f) / 2f, z + 0.05f), new Vector3(0.08f, topY - 0.04f, 0.08f), woodDark, false);
                }
                const float FrontY = 1.1f, FrontZ = -0.55f, BackY = 1.45f, BackZ = 0.3f;
                float[] frontX = { -2.1f, -0.7f, 0.7f, 2.1f };
                for (int i = 0; i < frontX.Length; i++)
                {
                    Table($"Table_Front_{i + 1}", frontX[i], FrontY, FrontZ, 0.8f);
                    Pyramid(root, $"Cup_F{i + 1}", new Vector3(frontX[i], FrontY, FrontZ), 4, Spacing, FrontY, BackWallZ - 0.1f - FrontZ, list, links);
                }
                float[] backX = { -1.4f, 0f, 1.4f };
                for (int i = 0; i < backX.Length; i++)
                {
                    Table($"Table_Back_{i + 1}", backX[i], BackY, BackZ, 0.95f);
                    Pyramid(root, $"Cup_B{i + 1}", new Vector3(backX[i], BackY, BackZ), 5, Spacing, BackY, BackWallZ - 0.1f - BackZ, list, links);
                }
                layout.targets = list.ToArray();
                layout.links = links.ToArray();
            }

            // ---- TURNTABLE: 手前に3台、奥の高い所に2台の回転台。カップが輪になって並び、真ん中に3個のピラミッド ----
            {
                var layout = NewLayout("TURNTABLE", out var root, out var list, out var links);
                int index = 0;
                void Turntable(Vector3 pos, float discRadius, float ringRadius, int ringCount, float speed)
                {
                    index++;
                    Prim(PrimitiveType.Cylinder, $"Pole_{index}", root, new Vector3(pos.x, (pos.y - 0.04f) / 2f, pos.z), new Vector3(0.08f, (pos.y - 0.04f) / 2f, 0.08f), metal, false);
                    var table = Group(root, $"Turntable_{index}");
                    table.localPosition = pos;
                    var rotator = table.gameObject.AddComponent<Rotator>();
                    rotator.axis = Vector3.up;
                    rotator.speed = speed;
                    Prim(PrimitiveType.Cylinder, "Disc", table, new Vector3(0f, -0.02f, 0f), new Vector3(discRadius * 2f, 0.02f, discRadius * 2f), index % 2 == 0 ? white : cloth);

                    // 回っているので、カップはどこを向いていてもプレイヤーから遠ざかる向きへ飛ぶ
                    float maxBack = BackWallZ - 0.1f - (pos.z + discRadius) + 0.2f;
                    for (int i = 0; i < ringCount; i++)
                    {
                        float a = i * Mathf.PI * 2f / ringCount;
                        var cup = Cup(table, $"Cup_{index}_{i + 1}", new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * ringRadius, i, pos.y, maxBack, list);
                        cup.knockAwayFromCamera = true;
                    }
                    int first = list.Count;
                    Pyramid(table, $"Cup_{index}_C", Vector3.zero, 2, 0.14f, pos.y, maxBack, list, links);
                    for (int i = first; i < list.Count; i++) list[i].knockAwayFromCamera = true;
                }
                Turntable(new Vector3(-2.0f, 1.15f, -0.4f), 0.55f, 0.45f, 12, 35f);
                Turntable(new Vector3(0f, 1.15f, -0.4f), 0.55f, 0.45f, 12, -45f);
                Turntable(new Vector3(2.0f, 1.15f, -0.4f), 0.55f, 0.45f, 12, 35f);
                Turntable(new Vector3(-1.0f, 1.6f, 0.3f), 0.45f, 0.36f, 10, -55f);
                Turntable(new Vector3(1.0f, 1.6f, 0.3f), 0.45f, 0.36f, 10, 55f);
                layout.targets = list.ToArray();
                layout.links = links.ToArray();
            }

            // ---- SLIDER: 3段の棚 (18 個ずつ) が、段ごとに逆向き・違う速さで左右に行ったり来たりする ----
            {
                const int CupsPerShelf = 18;
                const float CupSpacing = 0.17f;
                const float HalfWidth = CupSpacing * (CupsPerShelf - 1) / 2f + 0.15f;
                float[] amplitudes = { 0.9f, -0.8f, 0.9f };
                float[] speeds = { 1.3f, 1.9f, 1.0f };
                var layout = NewLayout("SLIDER", out var root, out var list, out var links);
                for (int s = 0; s < shelfY.Length; s++)
                {
                    var cart = Group(root, $"SlideShelf_{s + 1}");
                    var osc = cart.gameObject.AddComponent<Oscillator>();
                    osc.offset = new Vector3(amplitudes[s], 0f, 0f);
                    osc.speed = speeds[s];
                    osc.phase = s * 1.3f;
                    Shelf(cart, "Shelf", shelfY[s], shelfZ[s], HalfWidth);
                    float top = shelfY[s] + 0.02f;
                    for (int c = 0; c < CupsPerShelf; c++)
                        Cup(cart, $"Cup_{s + 1}_{c + 1}", new Vector3((c - (CupsPerShelf - 1) / 2f) * CupSpacing, top, shelfZ[s]),
                            s + c, top, BackWallZ - 0.1f - shelfZ[s], list);
                }
                layout.targets = list.ToArray();
                layout.links = links.ToArray();
            }

            // 並べ方の台とカップは選ばれた時だけ表示する (編集中は SHELF だけ見せておく)
            for (int i = 0; i < layouts.Count; i++) layouts[i].root.SetActive(i == 0);

            // カップは Stall.targets に入れない (沈む時にカップを消さず、乗せたまま沈めるため)
            var stall = t.gameObject.AddComponent<Stall>();
            stall.targets = new Target[0];
            challenge.stall = stall;
            challenge.layouts = layouts.ToArray();

            // 爆弾の見た目 (カップと同じくらいの大きさの黒い爆弾 + 赤い帯 + 導火線 + 火花)。根元 (棚の上) が原点
            var bombLooks = new GameObject[BombCount];
            for (int i = 0; i < BombCount; i++)
            {
                var look = Group(go.transform, $"Look_Bomb_{i}");
                const float R = 0.075f; // 爆弾の半径
                Prim(PrimitiveType.Sphere, "BombBody", look, new Vector3(0f, R, 0f), Vector3.one * R * 2f, Mat("Explosive_Body", new Color(0.08f, 0.08f, 0.1f)));
                var band = Prim(PrimitiveType.Cylinder, "Band", look, new Vector3(0f, R, 0f), new Vector3(R * 2.05f, 0.012f, R * 2.05f),
                                Mat("Explosive_Band", new Color(0.9f, 0.1f, 0.05f), new Color(0.8f, 0.05f, 0f)), false);
                band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cylinder, "Cap", look, new Vector3(0f, R * 2f, 0f), new Vector3(0.04f, 0.012f, 0.04f), Mat("Gun_Metal", new Color(0.18f, 0.18f, 0.2f)), false);
                var fuse = Prim(PrimitiveType.Cylinder, "Fuse", look, new Vector3(0.012f, R * 2f + 0.03f, 0f), new Vector3(0.01f, 0.03f, 0.01f),
                                Mat("Rope", new Color(0.85f, 0.8f, 0.65f)), false);
                fuse.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
                Prim(PrimitiveType.Sphere, "Spark", look, new Vector3(0.028f, R * 2f + 0.06f, 0f), Vector3.one * 0.035f,
                     Mat("Explosive_Spark", new Color(1f, 0.85f, 0.3f), new Color(2f, 1.4f, 0.3f)), false);
                look.gameObject.SetActive(false);
                bombLooks[i] = look.gameObject;
            }
            challenge.bombLooks = bombLooks;
            t.gameObject.SetActive(false); // チャレンジの時だけせり上がる
            go.SetActive(true);
            return challenge;
        }
    }
}
