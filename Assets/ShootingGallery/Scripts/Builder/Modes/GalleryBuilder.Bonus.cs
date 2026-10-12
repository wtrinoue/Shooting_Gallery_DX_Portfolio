using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 地面 (足元の丸い穴) とボーナスステージ (絞り羽根の床と半球の地下室) の生成。
    /// </summary>
    public partial class GalleryBuilder
    {
        // 足元の丸い穴 (ハッチ) の半径。屋台6軒の輪 (半径 3.5m。屋台は中心から 2.3m 〜 4.3m) の、屋台の奥行きの半分くらいまで重なる大きさ。
        // 時間がたって輪が広がっても、ハッチは広がらない
        const float PitRadius = 3.3f;
        // 地面の厚さ。絞り羽根はこの厚さの中を回り、開くと周りの地面の下に隠れる
        const float GroundThickness = 0.2f;

        /// <summary>地面: 足元に丸い穴が空いた大きな床 (穴はボーナスステージの絞り羽根でふさがっている) と、穴のふちの金属の輪</summary>
        void BuildGround()
        {
            const float Far = 30f;
            var ground = new GameObject("Ground");
            ground.transform.SetParent(world, false);
            var mesh = GroundMesh(Far, PitRadius, GroundThickness);
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            ground.AddComponent<MeshRenderer>().sharedMaterial = Mat("Ground", new Color(0.22f, 0.18f, 0.14f));
            ground.AddComponent<MeshCollider>().sharedMesh = mesh;

            // 穴のふち: 金属の輪と、内側の光る線
            var rim = Group(world, "PitRim");
            var rimMat = Mat("Pit_Rim", new Color(0.6f, 0.6f, 0.65f), null, false, 0.7f);
            var rimGlow = Mat("Pit_RimGlow", new Color(1f, 0.75f, 0.3f), new Color(1.2f, 0.75f, 0.2f));
            FlatRing(rim, "Rim", PitRadius + 0.05f, 0.008f, 0.08f, 0.012f, 64, rimMat);
            FlatRing(rim, "RimGlow", PitRadius + 0.005f, 0.014f, 0.015f, 0.01f, 64, rimGlow);
        }

        /// <summary>地面に寝かせた輪 (見た目だけ)。radius の円に沿って、幅 width の細い板を並べる</summary>
        void FlatRing(Transform parent, string name, float radius, float y, float width, float height, int segments, Material mat)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var seg = Prim(PrimitiveType.Cube, name, parent, dir * radius + Vector3.up * y,
                               new Vector3(width, height, 2f * Mathf.PI * radius / segments + 0.005f), mat, false);
                seg.transform.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir)); // 長い方を円の接線に
            }
        }

        /// <summary>
        /// ボーナスステージ:
        ///   床: 穴をふさぐ8枚の絞り羽根。穴のふちの軸のまわりに全部同じ向きに回り、カメラの絞りのように渦を巻いて開く (開くと周りの地面の下に隠れる)。
        ///   地下室: 穴と同じ半径の半球。内側の2段の輪と底に的が並び、どれを撃っても撃つたびに ド→レ→ミ… と上がる。
        ///   ランプ: 穴のふちに8個。当てるたびに虹色に1つずつ点く。
        /// </summary>
        BonusStage BuildBonusStage()
        {
            var go = new GameObject("BonusStage");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            // ---- 絞り羽根 ----
            const int Blades = 8;
            const float Lean = 20f;                   // 羽根が中心から少しずれた向きに伸びる角度 (渦巻きに重なる)
            const float BladeLength = PitRadius * 1.3f;
            const float BladeWidth = PitRadius;
            const float BladeThickness = 0.01f;
            const float BladeStep = 0.02f;           // 羽根1枚ごとの高さの差 (8枚で 0.16m。地面の厚さ 0.2m の中に収まる)
            var bladeMats = new[]
            {
                Mat("Iris_Blade_A", new Color(0.16f, 0.16f, 0.19f), null, false, 0.75f),
                Mat("Iris_Blade_B", new Color(0.22f, 0.22f, 0.26f), null, false, 0.75f)
            };
            var edgeMat = Mat("Iris_Edge", new Color(1f, 0.75f, 0.3f), new Color(0.5f, 0.32f, 0.05f), false, 0.7f);
            var blades = new Transform[Blades];
            for (int i = 0; i < Blades; i++)
            {
                float a = i * Mathf.PI * 2f / Blades;
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var pivot = Group(go.transform, $"IrisBlade_{i}");
                // 重なった羽根がちらつかないよう、1枚ずつ BladeStep ずつ低くする。羽根 (と縁) の厚さより十分大きな差にして、
                // 隣の羽根の面と同じ高さにならないようにする (VR の奥行きの精度は低いので数 mm の差ではちらつく)。全部地面の厚さの中
                pivot.localPosition = outward * PitRadius + Vector3.down * (0.025f + i * BladeStep);
                pivot.localRotation = Quaternion.LookRotation(Quaternion.Euler(0f, Lean, 0f) * -outward);
                Prim(PrimitiveType.Cube, "Blade", pivot, new Vector3(0f, 0f, BladeLength / 2f), new Vector3(BladeWidth, BladeThickness, BladeLength), bladeMats[i % 2]);
                Prim(PrimitiveType.Cube, "Edge", pivot, new Vector3(BladeWidth / 2f - 0.015f, BladeThickness / 2f + 0.002f, BladeLength / 2f), new Vector3(0.03f, 0.004f, BladeLength), edgeMat, false); // 羽根の上面に薄く乗せる
                blades[i] = pivot;
            }

            // ---- 半球の地下室 (中心は地面の下面。内側を向いた面) ----
            var room = Group(go.transform, "Basement");
            var domeCenter = Vector3.down * GroundThickness;
            var dome = new GameObject("Dome");
            dome.transform.SetParent(room, false);
            dome.transform.localPosition = domeCenter;
            dome.transform.localScale = Vector3.one * PitRadius;
            var domeMesh = DomeMesh(48, 16);
            dome.AddComponent<MeshFilter>().sharedMesh = domeMesh;
            dome.AddComponent<MeshRenderer>().sharedMaterial = Mat("Basement_Wall", new Color(0.13f, 0.1f, 0.24f), new Color(0.03f, 0.02f, 0.08f), false, 0.6f);
            dome.AddComponent<MeshCollider>().sharedMesh = domeMesh;

            // 光る緯線と経線 (深さと丸みが分かるように)
            var lightMat = Mat("Basement_Light", new Color(0.5f, 0.8f, 1f), new Color(0.6f, 1.2f, 1.8f));
            var lineMat = Mat("Basement_Line", new Color(0.35f, 0.3f, 0.7f), new Color(0.2f, 0.15f, 0.5f));
            Vector3 OnDome(float thetaDeg, float phi, float inset) // theta: 底 = 0, ふち = 90
            {
                float th = thetaDeg * Mathf.Deg2Rad;
                return domeCenter + new Vector3(Mathf.Sin(th) * Mathf.Sin(phi), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(phi)) * (PitRadius - inset);
            }
            foreach (float theta in new[] { 25f, 55f, 82f })
            {
                const int Segs = 48;
                for (int i = 0; i < Segs; i++)
                {
                    float p0 = i * Mathf.PI * 2f / Segs, p1 = (i + 1) * Mathf.PI * 2f / Segs;
                    DomeLine(room, "Latitude", OnDome(theta, p0, 0.01f), OnDome(theta, p1, 0.01f), 0.025f, lightMat);
                }
            }
            for (int m = 0; m < 8; m++)
            {
                float phi = (m + 0.5f) * Mathf.PI * 2f / 8;
                for (int k = 0; k < 12; k++)
                    DomeLine(room, "Meridian", OnDome(k * 7.5f, phi, 0.01f), OnDome((k + 1) * 7.5f, phi, 0.01f), 0.015f, lineMat);
            }

            // ---- 的: 内側の2段の輪 (8個ずつ) と底の大きな的。どれもプレイヤーの方を向いている ----
            Color[] rainbow =
            {
                new Color(1f, 0.2f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.1f), new Color(0.3f, 1f, 0.3f),
                new Color(0.2f, 0.9f, 1f), new Color(0.25f, 0.4f, 1f), new Color(0.7f, 0.3f, 1f), new Color(1f, 0.4f, 0.8f)
            };
            var colorMats = new Material[rainbow.Length];
            for (int i = 0; i < rainbow.Length; i++) colorMats[i] = Mat($"Bonus_Target_{i}", rainbow[i], rainbow[i] * 0.35f);
            var white = Mat("Target_White", Color.white);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var bracketMat = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            var eye = new Vector3(0f, 1.75f, 0f); // プレイヤーの目のだいたいの位置

            var targets = new List<Target>();
            var labels = new List<TextMesh>();
            Target WallTarget(string name, float thetaDeg, float phi, float diameter, int colorIndex)
            {
                var wall = OnDome(thetaDeg, phi, 0f);
                var inward = (domeCenter - wall).normalized;
                var pos = wall + inward * 0.16f;
                // 的の上側は外 (穴のふち) 向き。真下の的はメイン屋台の方が上
                var upHint = thetaDeg < 1f ? Vector3.forward : new Vector3(Mathf.Sin(phi), 0f, Mathf.Cos(phi));
                var root = Group(room, name);
                root.localPosition = pos;
                root.localRotation = Quaternion.LookRotation(pos - eye, upHint); // 的の表 (-Z) がプレイヤーを向く

                // 壁から的の後ろまでの支柱 (的が消えても残る)
                var bracket = Prim(PrimitiveType.Cylinder, "Bracket", room, wall + inward * 0.08f, new Vector3(0.04f, 0.08f, 0.04f), bracketMat, false);
                bracket.transform.localRotation = Quaternion.FromToRotation(Vector3.up, inward);

                var target = root.gameObject.AddComponent<Target>();
                target.kind = TargetKind.BigBonus;
                target.points = 1000; // 実際の点数は開くたびに BonusStage がランダムに決める (1000 / 2000 / 3000)
                target.manualControl = true;
                target.shrinkOnHit = true; // 後ろは壁なので、倒れずに縮んで消える
                var pivot = Group(root, "Pivot");
                target.visual = pivot;
                Disc(pivot, "Back", 0f, diameter * 1.12f, 0.008f, gold);
                Bullseye(pivot, 0f, diameter, colorMats[colorIndex % colorMats.Length], white, colorMats[(colorIndex + 4) % colorMats.Length]);
                // 点数の文字 (BonusStage が開くたびに書き換える)。真ん中の円に重ねる
                labels.Add(MakeText(pivot, "Points", "1000", new Vector3(0f, 0f, -0.022f), diameter * 0.3f, Color.white));
                targets.Add(target);
                return target;
            }
            // 穴が大きくなったので、輪の的の数を増やして同じくらいの間隔でまんべんなく並べる
            const int RingCount = 12;
            const float RingDiameter = 0.62f;
            for (int i = 0; i < RingCount; i++)
                WallTarget($"Target_Upper_{i}", 70f, i * Mathf.PI * 2f / RingCount, RingDiameter, i);
            for (int i = 0; i < RingCount; i++)
                WallTarget($"Target_Lower_{i}", 42f, (i + 0.5f) * Mathf.PI * 2f / RingCount, RingDiameter, i + 3);
            const float BottomDiameter = 1.2f;
            var bottom = WallTarget("Target_Bottom", 0f, 0f, BottomDiameter, 5);
            MakeText(bottom.visual, "Label", "DO RE MI", new Vector3(0f, BottomDiameter * 0.34f, -0.02f), BottomDiameter * 0.11f, Color.white);

            // ---- 当てるたびに点くランプ (穴のふちに8個、ド〜ドの虹色。メイン屋台側から時計回り) ----
            var lampOff = Mat("Lamp_Off", new Color(0.25f, 0.25f, 0.3f));
            var lampsOn = new GameObject[rainbow.Length];
            for (int i = 0; i < rainbow.Length; i++)
            {
                float a = i * Mathf.PI * 2f / rainbow.Length;
                var pos = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (PitRadius + 0.05f) + Vector3.up * 0.02f; // ふちの輪の上
                Prim(PrimitiveType.Sphere, $"Lamp_{i}_Off", go.transform, pos, Vector3.one * 0.1f, lampOff, false);
                lampsOn[i] = Prim(PrimitiveType.Sphere, $"Lamp_{i}_On", go.transform, pos, Vector3.one * 0.13f, Mat($"Lamp_On_{i}", rainbow[i], rainbow[i] * 2.2f), false);
            }

            var stage = go.AddComponent<BonusStage>();
            stage.irisBlades = blades;
            stage.irisOpenAngle = 180f - Lean; // 羽根がまっすぐ外を向くまで回して、全部地面の下に隠す
            stage.targets = targets.ToArray();
            stage.pointLabels = labels.ToArray();
            stage.lampsOn = lampsOn;
            stage.hideWhileOpen = playerMark != null ? new[] { playerMark } : new GameObject[0];
            foreach (var t in targets) t.SetAvailable(false);
            foreach (var lamp in lampsOn) lamp.SetActive(false);
            go.SetActive(true);
            return stage;
        }

        /// <summary>a から b まで細い棒を渡す (半球の内側の光る線用)</summary>
        void DomeLine(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            var seg = Prim(PrimitiveType.Cube, name, parent, (a + b) / 2f, new Vector3(thickness, thickness, (b - a).magnitude + 0.004f), mat, false);
            seg.transform.localRotation = Quaternion.LookRotation(b - a);
        }

        // ------------------------------------------------------------------
        // メッシュ
        // ------------------------------------------------------------------

        static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, c) = (c, b); // 外 (outward) から見て時計回りが表
            int k = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(k); tris.Add(k + 1); tris.Add(k + 2);
        }

        /// <summary>中心に丸い穴が空いた正方形の地面 (上面と、穴の内側の壁)。上面は y = 0、厚さ thick</summary>
        Mesh GroundMesh(float far, float holeRadius, float thick)
        {
            string name = "Ground_WithHole";
            if (meshes.TryGetValue(name, out var cached)) return cached;

            const int N = 64; // 4 の倍数にすると、正方形の角がちょうど頂点になる
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < N; i++)
            {
                Vector3 Dir(int k)
                {
                    float a = k * Mathf.PI * 2f / N;
                    return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                }
                Vector3 d0 = Dir(i), d1 = Dir(i + 1);
                Vector3 p0 = d0 * holeRadius, p1 = d1 * holeRadius;
                Vector3 q0 = d0 * (far / Mathf.Max(Mathf.Abs(d0.x), Mathf.Abs(d0.z))), q1 = d1 * (far / Mathf.Max(Mathf.Abs(d1.x), Mathf.Abs(d1.z)));
                // 上面
                AddTri(verts, tris, p0, q0, q1, Vector3.up);
                AddTri(verts, tris, p0, q1, p1, Vector3.up);
                // 穴の内側の壁 (中心を向く)
                var down = Vector3.down * thick;
                var inward = -(d0 + d1);
                AddTri(verts, tris, p0, p1, p1 + down, inward);
                AddTri(verts, tris, p0, p1 + down, p0 + down, inward);
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

        /// <summary>半径 1 の下半分の球 (お椀)。内側を向いた面で、なめらかに見えるよう頂点を共有する。ふちが y = 0</summary>
        Mesh DomeMesh(int longitude, int latitude)
        {
            string name = "Bonus_Dome";
            if (meshes.TryGetValue(name, out var cached)) return cached;

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            for (int la = 0; la <= latitude; la++)
            {
                float th = la * Mathf.PI / 2f / latitude; // 0 = 底, π/2 = ふち
                for (int lo = 0; lo <= longitude; lo++)
                {
                    float ph = lo * Mathf.PI * 2f / longitude;
                    var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), -Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    verts.Add(p);
                    normals.Add(-p); // 内側向き
                }
            }
            var tris = new List<int>();
            int row = longitude + 1;
            void Tri(int a, int b, int c)
            {
                // 内側 (中心の方) から見て時計回りになるように並べる
                var center = (verts[a] + verts[b] + verts[c]) / 3f;
                if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), -center) < 0f) (b, c) = (c, b);
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
            for (int la = 0; la < latitude; la++)
                for (int lo = 0; lo < longitude; lo++)
                {
                    int a = la * row + lo, b = a + 1, c = a + row, d = c + 1;
                    if (la > 0) Tri(a, b, c); // 底の点 (la = 0) では1つ目の三角形がつぶれるので作らない
                    Tri(b, d, c);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }
    }
}
