using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、Unknown ステージ (計画は new_docs/develop.txt) の生成:
    ///   空中のひび割れガラスの材質 (ガラスの球と、空間を切り分けた破片は UnknownStage / WorldShatter が実行時に作る)、
    ///   ふしぎな空間 (夕暮れ色の空・パステル色の市松の床・宙に浮くドアや時計や柱や浮き島)、
    ///   5つのワープゲート (アメリカの田舎・宇宙・海辺・夜の街・雪国。入り口の奥にそれぞれの世界がずっと広がって見える)、
    ///   途中で開いてプレイヤーがくぐる大きなワープホールと、その向こうの背景 (雲海の上の星空。輪の中にだけ見える)、
    ///   紛れ込んでくる物と、宙に現れる時計・ドア (UnknownObject)、屋台に出る的の見た目 (UnknownLook)。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float GateDistance = 13f;  // プレイヤーからゲートの入り口まで (m)
        const float GateY = 2.6f;        // ゲートの真ん中の高さ
        const float GateHole = 1.8f;     // 入り口の半径 (奥の景色は、目から入り口を通して見える範囲 = 四角い筒の中に作る。四角の角はゲートの太い枠に隠れる)
        const float GateDepth = 45f;     // 奥の世界の奥行き
        const float HoleDistance = 42f;  // 大きなワープホールが開く所までの距離 (UnknownStage.holeDistance と同じ)
        const float HoleRadius = 14f;    // 大きなワープホールの入り口の半径 (くぐる時に、大きく包み込むように)

        enum GateTheme { Country, Space, Beach, City, Snow }

        /// <summary>
        /// 入り口の奥の景色を作るための目安。プレイヤーの目 (eye) から入り口 (半径 hole の四角) を通して見える範囲は、奥ほど広がる四角い筒になる。
        /// この筒の中に置いた物は入り口からしか見えないので、外 (ふしぎな空間) からは見えない。奥行き z での筒の広さを返す
        /// </summary>
        class Vista
        {
            public readonly Vector3 eye;
            public readonly float hole;
            public Vista(Vector3 eye, float hole) { this.eye = eye; this.hole = hole; }
            public float S(float z) => (z - eye.z) / -eye.z;
            /// <summary>奥行き z での、横の幅の半分</summary>
            public float Half(float z) => hole * S(z);
            /// <summary>奥行き z での、見える範囲の上のはし / 下のはしの高さ</summary>
            public float Top(float z) => eye.y + (hole - eye.y) * S(z);
            public float Bottom(float z) => eye.y + (-hole - eye.y) * S(z);
            /// <summary>入り口の上の点 (u, v) を、奥行き z まで目からまっすぐ延ばした所</summary>
            public Vector3 P(float u, float v, float z) => new Vector3(eye.x + (u - eye.x) * S(z), eye.y + (v - eye.y) * S(z), z);
        }

        UnknownStage BuildUnknown()
        {
            var go = new GameObject("UnknownStage");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;

            // ---- ふしぎな空間 ----
            var space = Group(root, "WonderSpace");
            var floorT = Group(space, "WonderFloor");
            floorT.localPosition = new Vector3(0f, -0.08f, 0f); // (いつもの地面より少し下。割れて飛んでいく地面の破片と重ならないように)
            var floor = floorT.gameObject.AddComponent<WonderFloor>();
            floor.lightMaterial = Mat("Unknown_FloorLight", new Color(0.96f, 0.93f, 0.86f), null, false, 0.45f);
            floor.darkMaterial = Mat("Unknown_FloorDark", new Color(0.58f, 0.5f, 0.82f), null, false, 0.45f);
            BuildWonderDecor(space);

            // ---- ワープゲート (正面から 72° ごと) ----
            var themes = new[] { GateTheme.Country, GateTheme.Space, GateTheme.Beach, GateTheme.City, GateTheme.Snow };
            var gates = new Transform[themes.Length];
            for (int i = 0; i < themes.Length; i++) gates[i] = BuildGate(space, i * 72f, themes[i]);

            // ---- 大きなワープホールと、その向こうの背景 ----
            var hole = BuildBigHole(root);
            var cosmos = BuildCosmos(root);

            // ---- 紛れ込んでくる物 (ゲートごとに3つ) と、宙に現れる時計・ドア ----
            var sparkle = Mat("Unknown_Sparkle", new Color(1f, 0.92f, 0.6f), new Color(1.6f, 1.4f, 0.7f), true);
            var objects = new List<UnknownObject>
            {
                BuildCow(root, 0, sparkle), BuildTractor(root, 0, sparkle), BuildHayBale(root, 0, sparkle),
                BuildUfo(root, 1, sparkle), BuildSatellite(root, 1, sparkle), BuildAsteroid(root, 1, sparkle),
                BuildBeachBall(root, 2, sparkle), BuildFish(root, 2, sparkle), BuildBoat(root, 2, sparkle),
                BuildTaxi(root, 3, sparkle), BuildBus(root, 3, sparkle), BuildBalloon(root, 3, sparkle),
                BuildSnowman(root, 4, sparkle), BuildSled(root, 4, sparkle), BuildPresent(root, 4, sparkle),
            };
            for (int i = 0; i < 3; i++) objects.Add(BuildWonderClockTarget(root, i, sparkle));
            for (int i = 0; i < 3; i++) objects.Add(BuildWonderDoorTarget(root, i, sparkle));

            var stage = go.AddComponent<UnknownStage>();
            stage.shardMaterial = UnknownShardMaterial();
            stage.shardSolidMaterial = ShaderMaterial("Unknown_ShardSolid", "ShootingGallery/Unknown Shard Solid");
            stage.shardDepthMaterial = ShaderMaterial("Unknown_ShardDepth", "ShootingGallery/Unknown Shard Depth");
            stage.space = space.gameObject;
            stage.floor = floor;
            stage.gates = gates;
            stage.bigHole = hole;
            stage.holeRadius = HoleRadius;
            stage.holeDistance = HoleDistance;
            stage.cosmos = cosmos.gameObject;
            stage.objects = objects.ToArray();
            // 地面も、屋台といっしょに1つの空間として破片に切り分ける
            var groundT = world.Find("Ground");
            stage.groundRenderer = groundT != null ? groundT.GetComponent<MeshRenderer>() : null;
            var ground = new List<Transform>();
            foreach (var name in new[] { "Ground", "PitRim" })
            {
                var t = world.Find(name);
                if (t != null) ground.Add(t);
            }
            if (playerMark != null) ground.Add(playerMark.transform);
            stage.groundParts = ground.ToArray();
            foreach (var o in objects) o.gameObject.SetActive(false);
            hole.gameObject.SetActive(false);
            cosmos.gameObject.SetActive(false);
            space.gameObject.SetActive(false);
            go.SetActive(true);
            return stage;
        }

        /// <summary>空中のひび割れガラスの材質 (Shaders/UnknownShard.shader)。シーンに保存して、実機でもこのシェーダーが入るようにする</summary>
        Material UnknownShardMaterial()
        {
            const string Name = "Unknown_Shard";
            if (mats.TryGetValue(Name, out var cached)) return cached;
            var shader = Shader.Find("ShootingGallery/Unknown Shard");
            if (shader == null)
            {
                Debug.LogWarning("UnknownShard.shader が見つからないので、灰色の Unlit で代わりにします");
                return Mat(Name + "_Fallback", new Color(0.55f, 0.55f, 0.57f), null, true);
            }
            var mat = new Material(shader) { name = Name };
            mat = SaveAsAssetIfEditing(mat, Name);
            mats[Name] = mat;
            return mat;
        }

        /// <summary>シェーダーをそのまま使う材質。シーンに保存して、実機でもそのシェーダーが入るようにする (なければ null。使う側は透けるガラスのままにする)</summary>
        Material ShaderMaterial(string name, string shaderName)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"{shaderName} が見つかりません");
                return null;
            }
            var mat = new Material(shader) { name = name };
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        // 平らな台形 (上が表)。奥行き z0 から z1 まで、幅は奥ほど広がる (ゲートの奥の地面・海)
        Mesh Trapezoid(string name, float z0, float halfNear, float z1, float halfFar)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-halfNear, 0f, z0), new Vector3(-halfFar, 0f, z1), new Vector3(halfFar, 0f, z1), new Vector3(halfNear, 0f, z0) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }

        GameObject MeshObj(Transform parent, string name, Mesh mesh, Vector3 pos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------
        // ふしぎな空間の飾り (どれも的ではない)
        // ------------------------------------------------------------------

        /// <summary>
        /// 宙に浮くドア (すき間から白い光)・時計 (針が逆回り)・柱・行き先のない階段・小さな浮き島・遠くの輪のある星 (ゲートの間の方角に置く)。
        /// </summary>
        void BuildWonderDecor(Transform space)
        {
            var cream = Mat("Unknown_Cream", new Color(0.97f, 0.94f, 0.88f), null, false, 0.4f);
            var lilac = Mat("Unknown_Lilac", new Color(0.72f, 0.62f, 0.95f), null, false, 0.4f);
            var mint = Mat("Unknown_Mint", new Color(0.55f, 0.9f, 0.8f), null, false, 0.4f);
            var peach = Mat("Unknown_Peach", new Color(1f, 0.72f, 0.6f), null, false, 0.4f);
            var gold = Mat("Unknown_ClockRim", new Color(1f, 0.82f, 0.4f), new Color(0.2f, 0.15f, 0.05f), false, 0.7f);
            var rock = Mat("Unknown_IslandRock", new Color(0.55f, 0.45f, 0.5f), null, false, 0.2f);
            var grass = Mat("Unknown_IslandGrass", new Color(0.55f, 0.85f, 0.55f), new Color(0.05f, 0.1f, 0.05f), false, 0.2f);
            var pastel = new[] { cream, lilac, mint, peach };
            var rand = new System.Random(1959);
            float R(float a, float b) => a + (float)rand.NextDouble() * (b - a);
            Transform Item(string name, Vector3 pos)
            {
                var t = Group(space, name);
                t.localPosition = pos;
                return t;
            }

            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f + 36f; // ゲートとゲートの間
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                Vector3 At(float spread, float d0, float d1, float y0, float y1) => Quaternion.Euler(0f, R(-spread, spread), 0f) * dir * R(d0, d1) + Vector3.up * R(y0, y1);

                // 宙に浮く柱 (上下に飾りのある、切れた柱)
                for (int k = 0; k < 2; k++)
                {
                    var col = Item("Column", At(12f, 15f, 24f, -1.5f, 3f));
                    float h = R(3f, 7f);
                    Prim(PrimitiveType.Cylinder, "Shaft", col, Vector3.up * (h / 2f), new Vector3(0.9f, h / 2f, 0.9f), cream, false);
                    for (int f = 0; f < 6; f++) // 柱の縦の溝
                    {
                        float fa = f * 60f * Mathf.Deg2Rad;
                        Prim(PrimitiveType.Cube, "Flute", col, new Vector3(Mathf.Sin(fa) * 0.44f, h / 2f, Mathf.Cos(fa) * 0.44f), new Vector3(0.08f, h * 0.9f, 0.08f), lilac, false)
                            .transform.localRotation = Quaternion.Euler(0f, f * 60f, 0f);
                    }
                    foreach (float y in new[] { -0.15f, h + 0.15f })
                    {
                        Prim(PrimitiveType.Cube, "Slab", col, Vector3.up * y, new Vector3(1.3f, 0.3f, 1.3f), lilac, false);
                        Prim(PrimitiveType.Cylinder, "Ring", col, Vector3.up * (y + (y < 0f ? 0.25f : -0.25f)), new Vector3(1.05f, 0.08f, 1.05f), gold, false);
                    }
                    col.localRotation = Quaternion.Euler(R(-8f, 8f), R(0f, 360f), R(-8f, 8f));
                }
                // 行き先のない階段 (手すり付き)
                var stairs = Item("Stairs", At(10f, 11f, 18f, 1f, 5f));
                stairs.localRotation = Quaternion.Euler(0f, a + R(60f, 120f), R(-10f, 10f));
                for (int s = 0; s < 9; s++)
                {
                    Prim(PrimitiveType.Cube, "Step", stairs, new Vector3(0f, s * 0.35f, s * 0.45f), new Vector3(1.6f, 0.12f, 0.5f), s % 2 == 0 ? cream : mint, false);
                    foreach (float x in new[] { -0.75f, 0.75f })
                        Prim(PrimitiveType.Cylinder, "Baluster", stairs, new Vector3(x, s * 0.35f + 0.4f, s * 0.45f), new Vector3(0.05f, 0.4f, 0.05f), gold, false);
                }
                foreach (float x in new[] { -0.75f, 0.75f })
                    Prim(PrimitiveType.Cube, "Rail", stairs, new Vector3(x, 0.8f + 1.4f, 1.8f), new Vector3(0.06f, 0.06f, 4.6f), gold, false)
                        .transform.localRotation = Quaternion.Euler(-37.9f, 0f, 0f);
                // 宙に浮くドア (少し開いていて、すき間から白い光がもれる)
                var door = Item("FloatingDoor", At(14f, 9f, 15f, 1.2f, 4.5f));
                door.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(R(-12f, 12f), R(-25f, 25f), R(-15f, 15f));
                BuildDoorShape(door, 1f, i % 2 == 0 ? peach : lilac, Mat("Unknown_DoorFrame", new Color(0.35f, 0.28f, 0.45f), null, false, 0.5f),
                               Mat("Unknown_DoorLight", new Color(1f, 1f, 0.95f), new Color(2.2f, 2.2f, 2.4f), true), R(20f, 55f));
                // 宙に浮く時計 (針が逆回り。大きさも傾きもばらばら)
                var clock = Item("FloatingClock", At(18f, 16f, 24f, 4f, 9f));
                clock.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(R(-20f, 20f), R(-30f, 30f), R(-20f, 20f));
                clock.localScale = Vector3.one * R(1.5f, 2.8f);
                BuildClockShape(clock, cream, Mat("Unknown_ClockInk", new Color(0.3f, 0.25f, 0.45f), null, false, 0.5f), gold, -R(40f, 120f));
                // 小さな浮き島 (下が岩、上に草と木。ゆっくり回る)
                for (int k = 0; k < 2; k++)
                {
                    var island = Item("FloatingIsland", At(25f, 8f, 20f, 3f, 9f));
                    var spin = island.gameObject.AddComponent<Rotator>();
                    spin.axis = Vector3.up;
                    spin.speed = R(-12f, 12f);
                    float size = R(0.8f, 1.6f);
                    Prim(PrimitiveType.Sphere, "Rock", island, new Vector3(0f, -0.5f, 0f) * size, new Vector3(2f, 1.6f, 2f) * size, rock, false);
                    Prim(PrimitiveType.Sphere, "RockTip", island, new Vector3(0f, -1.2f, 0f) * size, new Vector3(1f, 1.2f, 1f) * size, rock, false);
                    Prim(PrimitiveType.Cylinder, "Grass", island, new Vector3(0f, 0.05f, 0f) * size, new Vector3(2.05f, 0.08f, 2.05f) * size, grass, false);
                    Prim(PrimitiveType.Cylinder, "Trunk", island, new Vector3(0.3f, 0.45f, 0.1f) * size, new Vector3(0.12f, 0.4f, 0.12f) * size, Mat("Palm_Trunk", new Color(0.55f, 0.38f, 0.2f), null, false, 0.1f), false);
                    Prim(PrimitiveType.Sphere, "Leaves", island, new Vector3(0.3f, 1.05f, 0.1f) * size, new Vector3(0.8f, 0.7f, 0.8f) * size, pastel[(i + k) % pastel.Length] == cream ? mint : grass, false);
                }
                // 遠くの輪のある星 (霧にかすむ)
                var planet = Item("FarPlanet", dir * R(45f, 60f) + Vector3.up * R(10f, 22f));
                float pr = R(3f, 6f);
                Prim(PrimitiveType.Sphere, "Planet", planet, Vector3.zero, Vector3.one * pr * 2f, pastel[i % pastel.Length], false);
                Prim(PrimitiveType.Cylinder, "PlanetRing", planet, Vector3.zero, new Vector3(pr * 3.4f, 0.05f, pr * 3.4f), gold, false)
                    .transform.localRotation = Quaternion.Euler(R(10f, 30f), 0f, R(-20f, 20f));
            }
        }

        // ドア (原点が下の真ん中、表は -Z)。open = 開いている角度。開いたすき間の奥は白く光る
        void BuildDoorShape(Transform parent, float scale, Material panel, Material frame, Material light, float open)
        {
            float w = 0.9f * scale, h = 2f * scale, t = 0.08f * scale;
            Prim(PrimitiveType.Cube, "FrameL", parent, new Vector3(-w / 2f - t / 2f, h / 2f, 0f), new Vector3(t, h + t, t * 1.5f), frame, false);
            Prim(PrimitiveType.Cube, "FrameR", parent, new Vector3(w / 2f + t / 2f, h / 2f, 0f), new Vector3(t, h + t, t * 1.5f), frame, false);
            Prim(PrimitiveType.Cube, "FrameTop", parent, new Vector3(0f, h + t / 2f, 0f), new Vector3(w + t * 2f, t, t * 1.5f), frame, false);
            Prim(PrimitiveType.Cube, "DoorLight", parent, new Vector3(0f, h / 2f, 0.03f * scale), new Vector3(w, h, 0.01f), light, false);
            var hinge = Group(parent, "Hinge");
            hinge.localPosition = new Vector3(-w / 2f, 0f, -0.02f * scale);
            hinge.localRotation = Quaternion.Euler(0f, -open, 0f); // 手前へ開く
            Prim(PrimitiveType.Cube, "Panel", hinge, new Vector3(w / 2f, h / 2f, 0f), new Vector3(w, h, 0.05f * scale), panel, false);
            foreach (float y in new[] { 0.28f, 0.72f }) // 扉の飾りの枠
                Prim(PrimitiveType.Cube, "Inset", hinge, new Vector3(w / 2f, h * y, -0.03f * scale), new Vector3(w * 0.7f, h * 0.3f, 0.01f), frame, false);
            Prim(PrimitiveType.Sphere, "Knob", hinge, new Vector3(w * 0.85f, h * 0.48f, -0.05f * scale), Vector3.one * 0.07f * scale, frame, false);
        }

        // 時計 (原点が文字盤の真ん中、表は -Z)。針は handSpeed (度/秒。マイナスで逆回り) で回る
        void BuildClockShape(Transform parent, Material face, Material ink, Material rim, float handSpeed)
        {
            Prim(PrimitiveType.Cylinder, "Rim", parent, new Vector3(0f, 0f, 0.02f), new Vector3(1.1f, 0.03f, 1.1f), rim, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "Face", parent, Vector3.zero, new Vector3(1f, 0.03f, 1f), face, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var tick = Prim(PrimitiveType.Cube, "Tick", parent, new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * 0.42f + Vector3.back * 0.035f, new Vector3(0.03f, i % 3 == 0 ? 0.1f : 0.06f, 0.01f), ink, false);
                tick.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 30f);
            }
            foreach (var (len, speed, width) in new[] { (0.3f, handSpeed, 0.05f), (0.42f, handSpeed * 12f, 0.03f) })
            {
                var hand = Group(parent, "Hand");
                hand.localPosition = Vector3.back * 0.045f;
                hand.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                Prim(PrimitiveType.Cube, "HandBar", hand, new Vector3(0f, len / 2f, 0f), new Vector3(width, len, 0.01f), ink, false);
                var rot = hand.gameObject.AddComponent<Rotator>();
                rot.axis = Vector3.forward;
                rot.speed = speed;
            }
            Prim(PrimitiveType.Sphere, "Pin", parent, Vector3.back * 0.05f, Vector3.one * 0.06f, ink, false);
            // 上の鈴と脚 (置き時計のように)
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Sphere, "Bell", parent, new Vector3(s * 0.32f, 0.55f, 0.03f), new Vector3(0.28f, 0.22f, 0.2f), rim, false);
                Prim(PrimitiveType.Cube, "Leg", parent, new Vector3(s * 0.3f, -0.6f, 0.03f), new Vector3(0.06f, 0.18f, 0.06f), rim, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, s * 25f);
            }
        }

        // 宙にぽんと現れる時計 (的。いつもこちらを向く)
        UnknownObject BuildWonderClockTarget(Transform parent, int index, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, $"Wonder_Clock_{index}");
            body.localScale = Vector3.one * 0.9f;
            BuildClockShape(body, Mat("Unknown_Cream", new Color(0.97f, 0.94f, 0.88f), null, false, 0.4f), Mat("Unknown_ClockInk", new Color(0.3f, 0.25f, 0.45f), null, false, 0.5f),
                            Mat("Unknown_ClockRim", new Color(1f, 0.82f, 0.4f), new Color(0.2f, 0.15f, 0.05f), false, 0.7f), -90f - index * 40f);
            var o = Visitor(root, body, -1, "CLOCK", new[] { 200, 300 }, 1.05f, Vector3.zero, new Vector3(1.2f, 1.4f, 0.3f), sparkle, 0.6f);
            o.facePlayer = true;
            o.bob = 0.15f;
            return o;
        }

        // 宙にぽんと現れるドア (的。ゆっくり回る)
        UnknownObject BuildWonderDoorTarget(Transform parent, int index, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, $"Wonder_Door_{index}");
            var door = Group(body, "Door");
            door.localPosition = Vector3.down * 0.85f;
            BuildDoorShape(door, 0.85f, index % 2 == 0 ? Mat("Unknown_Peach", new Color(1f, 0.72f, 0.6f), null, false, 0.4f) : Mat("Unknown_Lilac", new Color(0.72f, 0.62f, 0.95f), null, false, 0.4f),
                           Mat("Unknown_DoorFrame", new Color(0.35f, 0.28f, 0.45f), null, false, 0.5f), Mat("Unknown_DoorLight", new Color(1f, 1f, 0.95f), new Color(2.2f, 2.2f, 2.4f), true), 35f + index * 10f);
            var o = Visitor(root, body, -1, "DOOR", new[] { 100, 200 }, 1.2f, Vector3.zero, new Vector3(1f, 1.9f, 0.6f), sparkle, 0.7f);
            o.spinAxis = Vector3.up;
            o.spinSpeed = 40f;
            o.bob = 0.12f;
            return o;
        }

        // ------------------------------------------------------------------
        // ワープゲート
        // ------------------------------------------------------------------

        /// <summary>
        /// ワープゲート (原点が入り口の真ん中、+Z が奥の世界)。太い枠と光る輪の奥に、その世界が 45m 先まで広がっている。
        /// 奥の景色は、プレイヤーの目から入り口を通して見える範囲 (Vista の四角い筒) の中にだけ作るので、ゲートの外からは見えない。
        /// 地面 (台形) と、突き当たりの空 (筒の断面いっぱいの板) で、どの向きに見ても奥の世界が途切れない
        /// </summary>
        Transform BuildGate(Transform space, float angle, GateTheme theme)
        {
            var dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var gate = Group(space, $"Gate_{theme}");
            gate.localPosition = dir * GateDistance + Vector3.up * GateY;
            gate.localRotation = Quaternion.LookRotation(dir);

            Color glowColor = theme switch
            {
                GateTheme.Country => new Color(1f, 0.8f, 0.35f),
                GateTheme.Space => new Color(0.55f, 0.5f, 1f),
                GateTheme.Beach => new Color(0.3f, 0.9f, 1f),
                GateTheme.City => new Color(1f, 0.45f, 0.75f),
                _ => new Color(0.8f, 0.95f, 1f),
            };
            var frame = Mat("Gate_Frame", new Color(0.28f, 0.25f, 0.4f), new Color(0.04f, 0.03f, 0.08f), false, 0.8f);
            var glow = Mat($"Gate_Glow_{theme}", glowColor, glowColor * 1.8f, true);
            RingSegments(gate, "GateFrame", GateHole + 0.65f, 40, 0.75f, frame);
            RingSegments(gate, "GateGlow", GateHole + 0.15f, 40, 0.3f, glow, null, -0.42f);
            var membrane = Prim(PrimitiveType.Cylinder, "Membrane", gate, new Vector3(0f, 0f, -0.05f), new Vector3(GateHole * 2.1f, 0.003f, GateHole * 2.1f),
                                TransparentMat($"Gate_Membrane_{theme}", new Color(glowColor.r, glowColor.g, glowColor.b, 0.1f)), false);
            membrane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MakeText(gate, "GateName", theme.ToString().ToUpper(), new Vector3(0f, GateHole + 1.35f, -0.4f), 0.45f, glowColor);

            var w = Group(gate, "World");
            var v = new Vista(new Vector3(0f, 1.6f - GateY, -GateDistance), GateHole);
            switch (theme)
            {
                case GateTheme.Country: BuildCountryWorld(w, v); break;
                case GateTheme.Space: BuildSpaceWorld(w, v); break;
                case GateTheme.Beach: BuildBeachWorld(w, v); break;
                case GateTheme.City: BuildCityWorld(w, v); break;
                default: BuildSnowWorld(w, v); break;
            }
            return gate;
        }

        // 奥の世界の地面 (台形。y = -GateHole) と、突き当たりの空。band = 地平線のあたりの明るい帯の色 (なければ null)
        void GateGroundAndSky(Transform w, Vista v, Material ground, Material sky, Material band, float groundFrom = 0.02f)
        {
            const float D = GateDepth;
            float g = -GateHole;
            if (ground != null)
                MeshObj(w, "Ground", Trapezoid($"GateGround_{groundFrom:0.00}", groundFrom, v.Half(groundFrom), D + 0.5f, v.Half(D + 0.5f)), new Vector3(0f, g, 0f), ground);
            float bottom = ground != null ? g - 0.2f : v.Bottom(D + 0.5f) - 0.5f, top = v.Top(D + 0.5f) + 0.5f;
            Prim(PrimitiveType.Cube, "Sky", w, new Vector3(0f, (bottom + top) / 2f, D + 0.6f), new Vector3(v.Half(D + 0.6f) * 2f + 1f, top - bottom, 0.1f), sky, false);
            if (band != null)
                Prim(PrimitiveType.Cube, "SkyBand", w, new Vector3(0f, g + (top - g) * 0.12f, D + 0.5f), new Vector3(v.Half(D + 0.5f) * 2f, (top - g) * 0.24f, 0.05f), band, false);
        }

        // 奥行き z で、横 x (幅の半分 halfW) に置いても筒からはみ出さないか
        static bool Fits(Vista v, float x, float halfW, float z, float topY) => Mathf.Abs(x) + halfW <= v.Half(z) * 0.95f && topY <= v.Top(z) * 0.95f;

        // アメリカの田舎: 青空・地平線までまっすぐ続く道・畑の畝・赤い納屋とサイロ・風車・木・電柱・干し草・なだらかな丘・雲・太陽
        void BuildCountryWorld(Transform w, Vista v)
        {
            const float D = GateDepth;
            float g = -GateHole;
            var rand = new System.Random(101);
            float R(float a, float b) => a + (float)rand.NextDouble() * (b - a);
            GateGroundAndSky(w, v, Mat("Country_Grass", new Color(0.4f, 0.68f, 0.28f), new Color(0.06f, 0.1f, 0.03f), false, 0.1f),
                             Mat("Country_Sky", new Color(0.5f, 0.76f, 1f), null, true), Mat("Country_SkyBand", new Color(0.82f, 0.93f, 1f), null, true));
            var road = Mat("Country_Road", new Color(0.35f, 0.33f, 0.32f), new Color(0.04f, 0.04f, 0.04f), false, 0.2f);
            var line = Mat("City_Line", new Color(1f, 0.85f, 0.3f), new Color(0.6f, 0.45f, 0.1f), false, 0.3f);
            Prim(PrimitiveType.Cube, "Road", w, new Vector3(0f, g + 0.01f, D / 2f), new Vector3(2.4f, 0.02f, D), road, false);
            for (float z = 1f; z < D; z += 3f)
                Prim(PrimitiveType.Cube, "RoadDash", w, new Vector3(0f, g + 0.025f, z), new Vector3(0.1f, 0.01f, 1.4f), line, false);
            var white = Mat("Barn_White", new Color(0.95f, 0.95f, 0.92f), new Color(0.12f, 0.12f, 0.12f), false, 0.2f);
            var crop = Mat("Country_Crop", new Color(0.25f, 0.5f, 0.18f), new Color(0.03f, 0.06f, 0.02f), false, 0.1f);
            var soil = Mat("Country_Soil", new Color(0.5f, 0.36f, 0.22f), null, false, 0.1f);
            foreach (float s in new[] { -1f, 1f })
            {
                // 道ばたの白い柵
                for (float z = 1f; z < D; z += 2f)
                    Prim(PrimitiveType.Cube, "FencePost", w, new Vector3(s * 1.45f, g + 0.3f, z), new Vector3(0.08f, 0.6f, 0.08f), white, false);
                Prim(PrimitiveType.Cube, "FenceRail", w, new Vector3(s * 1.45f, g + 0.45f, D / 2f), new Vector3(0.04f, 0.05f, D - 1f), white, false);
                // 畑の畝 (土と作物の筋が、奥まで並ぶ)
                for (int k = 0; k < 7; k++)
                {
                    float x = 2f + k * 0.9f;
                    float z0 = Mathf.Max(2f, (x + 0.6f) / GateHole * GateDistance - GateDistance + 1f);
                    if (z0 > D - 6f) continue;
                    Prim(PrimitiveType.Cube, "Soil", w, new Vector3(s * x, g + 0.03f, (z0 + D - 4f) / 2f), new Vector3(0.8f, 0.04f, D - 4f - z0), soil, false);
                    Prim(PrimitiveType.Cube, "Crop", w, new Vector3(s * x, g + 0.2f, (z0 + D - 4f) / 2f), new Vector3(0.35f, 0.35f, D - 4f - z0), crop, false);
                }
            }
            // 赤い納屋とサイロ
            var red = Mat("Barn_Red", new Color(0.72f, 0.13f, 0.1f), new Color(0.1f, 0.01f, 0.01f), false, 0.2f);
            var roof = Mat("Barn_Roof", new Color(0.35f, 0.1f, 0.08f), null, false, 0.2f);
            var silo = Mat("Silo_Metal", new Color(0.75f, 0.76f, 0.8f), new Color(0.08f, 0.08f, 0.09f), false, 0.7f);
            foreach (var (s, z) in new[] { (-1f, 20f), (1f, 33f) })
            {
                float x = s * (v.Half(z) * 0.55f);
                var barn = Group(w, "Barn");
                barn.localPosition = new Vector3(x, g, z);
                barn.localRotation = Quaternion.Euler(0f, s * 90f, 0f);
                Prim(PrimitiveType.Cube, "BarnBody", barn, new Vector3(0f, 1.6f, 0f), new Vector3(4.5f, 3.2f, 3.5f), red, false);
                Prim(PrimitiveType.Cube, "BarnRoof", barn, new Vector3(0f, 3.2f, 0f), new Vector3(4.7f, 2.5f, 2.5f), roof, false).transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
                Prim(PrimitiveType.Cube, "BarnDoor", barn, new Vector3(0f, 1f, -1.76f), new Vector3(1.8f, 2f, 0.02f), white, false);
                Prim(PrimitiveType.Cube, "DoorX1", barn, new Vector3(0f, 1f, -1.78f), new Vector3(0.12f, 2.6f, 0.02f), red, false).transform.localRotation = Quaternion.Euler(0f, 0f, 42f);
                Prim(PrimitiveType.Cube, "DoorX2", barn, new Vector3(0f, 1f, -1.78f), new Vector3(0.12f, 2.6f, 0.02f), red, false).transform.localRotation = Quaternion.Euler(0f, 0f, -42f);
                Prim(PrimitiveType.Cube, "Loft", barn, new Vector3(0f, 2.9f, -1.76f), new Vector3(0.8f, 0.6f, 0.02f), white, false);
                Prim(PrimitiveType.Cylinder, "Silo", barn, new Vector3(3.2f, 2.8f, 0.5f), new Vector3(1.6f, 2.8f, 1.6f), silo, false);
                Prim(PrimitiveType.Sphere, "SiloTop", barn, new Vector3(3.2f, 5.6f, 0.5f), new Vector3(1.6f, 1.2f, 1.6f), silo, false);
            }
            // 風車 (羽根が回る)
            var metal = Mat("Windmill_Metal", new Color(0.7f, 0.7f, 0.72f), new Color(0.08f, 0.08f, 0.08f), false, 0.6f);
            foreach (var (s, z) in new[] { (1f, 14f), (-1f, 38f) })
            {
                float x = s * v.Half(z) * 0.4f;
                Prim(PrimitiveType.Cylinder, "WindmillTower", w, new Vector3(x, g + 2.2f, z), new Vector3(0.18f, 2.2f, 0.18f), metal, false);
                var blades = Group(w, "WindmillBlades");
                blades.localPosition = new Vector3(x, g + 4.5f, z - 0.2f);
                var spin = blades.gameObject.AddComponent<Rotator>();
                spin.axis = Vector3.forward;
                spin.speed = 60f + z;
                for (int i = 0; i < 12; i++)
                    Prim(PrimitiveType.Cube, "Blade", blades, Quaternion.Euler(0f, 0f, i * 30f) * new Vector3(0f, 0.75f, 0f), new Vector3(0.18f, 1.3f, 0.02f), white, false)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, i * 30f);
                Prim(PrimitiveType.Cube, "Tail", w, new Vector3(x, g + 4.5f, z + 0.8f), new Vector3(0.05f, 0.6f, 1.2f), metal, false);
            }
            // 木 (丸い葉が重なる)
            var trunk = Mat("Palm_Trunk", new Color(0.55f, 0.38f, 0.2f), null, false, 0.1f);
            var leaves = new[] { Mat("Tree_LeafA", new Color(0.3f, 0.55f, 0.22f), new Color(0.03f, 0.06f, 0.02f), false, 0.2f), Mat("Tree_LeafB", new Color(0.4f, 0.65f, 0.25f), new Color(0.04f, 0.07f, 0.02f), false, 0.2f) };
            for (int i = 0; i < 14; i++)
            {
                float z = R(5f, D - 3f), s = i % 2 == 0 ? -1f : 1f;
                float x = s * v.Half(z) * R(0.45f, 0.85f), size = R(1f, 1.6f);
                if (!Fits(v, x, 1.2f * size, z, g + 4f * size)) continue;
                Prim(PrimitiveType.Cylinder, "Trunk", w, new Vector3(x, g + 1f * size, z), new Vector3(0.25f, 1f, 0.25f) * size, trunk, false);
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Sphere, "Leaves", w, new Vector3(x + R(-0.4f, 0.4f) * size, g + (2.3f + k * 0.5f) * size, z + R(-0.4f, 0.4f)), Vector3.one * R(1.2f, 1.8f) * size, leaves[k % 2], false);
            }
            // 電柱と電線 (右の道ばた)
            for (float z = 6f; z < D; z += 6f)
            {
                Prim(PrimitiveType.Cylinder, "Pole", w, new Vector3(2.1f, g + 2f, z), new Vector3(0.1f, 2f, 0.1f), trunk, false);
                Prim(PrimitiveType.Cube, "CrossArm", w, new Vector3(2.1f, g + 3.7f, z), new Vector3(0.9f, 0.06f, 0.06f), trunk, false);
            }
            Prim(PrimitiveType.Cube, "Wire", w, new Vector3(2.1f, g + 3.6f, (6f + D) / 2f), new Vector3(0.02f, 0.02f, D - 6f), Mat("Cow_Black", new Color(0.1f, 0.1f, 0.12f), null, false, 0.3f), false);
            // 干し草
            var hay = Mat("Hay", new Color(0.92f, 0.8f, 0.38f), new Color(0.12f, 0.1f, 0.03f), false, 0.1f);
            for (int i = 0; i < 8; i++)
            {
                float z = R(6f, D - 5f), s = i % 2 == 0 ? -1f : 1f, x = s * v.Half(z) * R(0.35f, 0.7f);
                Prim(PrimitiveType.Cylinder, "HayBale", w, new Vector3(x, g + 0.55f, z), new Vector3(1.1f, 0.6f, 1.1f), hay, false).transform.localRotation = Quaternion.Euler(0f, R(0f, 90f), 90f);
            }
            // なだらかな丘・雲・太陽
            var hill = Mat("Country_Hill", new Color(0.35f, 0.6f, 0.3f), new Color(0.05f, 0.09f, 0.04f), false, 0.1f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * v.Half(D) * 0.4f; // (ゲートの枠に隠れる所までに収める)
                Prim(PrimitiveType.Sphere, "Hill", w, new Vector3(x, g, D - 2f - i), new Vector3(Mathf.Min(R(10f, 16f), 2f * (v.Half(D) * 1.3f - Mathf.Abs(x))), R(4f, 7f), 5f), hill, false);
            }
            var cloud = Mat("Country_Cloud", Color.white, null, true);
            for (int i = 0; i < 6; i++)
            {
                float z = R(18f, D - 3f), x = R(-0.8f, 0.8f) * v.Half(z), y = Mathf.Lerp(g, v.Top(z), R(0.55f, 0.85f));
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Sphere, "Cloud", w, new Vector3(x + (k - 1) * 1.6f, y + R(-0.2f, 0.3f), z), new Vector3(R(2.4f, 3.4f), R(1f, 1.5f), 1.4f), cloud, false);
            }
            Prim(PrimitiveType.Sphere, "Sun", w, new Vector3(-v.Half(D) * 0.5f, Mathf.Lerp(g, v.Top(D), 0.75f), D), Vector3.one * 4f, Mat("Beach_Sun", new Color(1f, 0.95f, 0.6f), null, true), false);
        }

        // 宇宙: たくさんの星・色とりどりの星雲・輪のある惑星や月・小惑星の帯・回る宇宙ステーション・ほうき星
        void BuildSpaceWorld(Transform w, Vista v)
        {
            const float D = GateDepth;
            var rand = new System.Random(77);
            float R(float a, float b) => a + (float)rand.NextDouble() * (b - a);
            GateGroundAndSky(w, v, null, Mat("SpaceWorld_Dark", new Color(0.02f, 0.02f, 0.06f), null, true), null);
            var stars = new[] { Mat("SpaceWorld_Star", new Color(1f, 1f, 0.9f), null, true), Mat("SpaceWorld_StarBlue", new Color(0.7f, 0.85f, 1f), null, true), Mat("SpaceWorld_StarPink", new Color(1f, 0.8f, 0.9f), null, true) };
            for (int i = 0; i < 260; i++)
            {
                float z = R(3f, D);
                Prim(PrimitiveType.Sphere, "Star", w, v.P(R(-GateHole, GateHole), R(-GateHole, GateHole), z), Vector3.one * R(0.03f, 0.08f) * v.S(z), stars[i % 3], false);
            }
            // 星雲 (突き当たりの近くの、色の付いた薄い雲)
            var nebula = new[] { TransparentMat("Nebula_Pink", new Color(1f, 0.4f, 0.75f, 0.16f)), TransparentMat("Nebula_Blue", new Color(0.35f, 0.55f, 1f, 0.16f)), TransparentMat("Nebula_Violet", new Color(0.65f, 0.4f, 1f, 0.16f)) };
            for (int i = 0; i < 7; i++)
            {
                float z = D - 1f - i * 0.6f;
                var q = Prim(PrimitiveType.Quad, "Nebula", w, v.P(R(-1.2f, 1.2f), R(-1.2f, 1.2f), z), new Vector3(R(5f, 9f), R(3f, 5f), 1f), nebula[i % 3], false);
                q.transform.localRotation = Quaternion.Euler(0f, 0f, R(0f, 180f));
            }
            // 惑星
            var gas = Mat("Planet_Gas", new Color(0.95f, 0.75f, 0.45f), new Color(0.25f, 0.15f, 0.05f), false, 0.3f);
            var band = Mat("Planet_GasBand", new Color(0.8f, 0.5f, 0.3f), new Color(0.18f, 0.08f, 0.03f), false, 0.3f);
            var ringMat = Mat("SpaceWorld_Ring", new Color(1f, 0.85f, 0.6f), new Color(0.3f, 0.25f, 0.15f), false, 0.3f);
            var giant = Group(w, "GasGiant");
            giant.localPosition = v.P(0.7f, 0.6f, 32f);
            Prim(PrimitiveType.Sphere, "Planet", giant, Vector3.zero, Vector3.one * 6f, gas, false);
            for (int k = -1; k <= 1; k++)
                Prim(PrimitiveType.Cylinder, "Band", giant, Vector3.up * k * 1.2f, new Vector3(6.02f - Mathf.Abs(k) * 0.9f, 0.25f, 6.02f - Mathf.Abs(k) * 0.9f), band, false);
            Prim(PrimitiveType.Cylinder, "PlanetRing", giant, Vector3.zero, new Vector3(11f, 0.02f, 11f), ringMat, false);
            giant.localRotation = Quaternion.Euler(18f, 0f, -14f);
            Prim(PrimitiveType.Sphere, "RedPlanet", w, v.P(-1.1f, -0.6f, 14f), Vector3.one * 1.6f, Mat("Planet_Red", new Color(0.9f, 0.4f, 0.3f), new Color(0.2f, 0.06f, 0.04f), false, 0.3f), false);
            var blue = Group(w, "BluePlanet");
            blue.localPosition = v.P(-0.5f, 1.1f, 38f);
            Prim(PrimitiveType.Sphere, "Ocean", blue, Vector3.zero, Vector3.one * 4.5f, Mat("Planet_Ocean", new Color(0.2f, 0.45f, 0.95f), new Color(0.04f, 0.1f, 0.25f), false, 0.6f), false);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Sphere, "Land", blue, Random.onUnitSphere * 1.6f, Vector3.one * R(1.2f, 2f), Mat("Planet_Land", new Color(0.3f, 0.7f, 0.35f), new Color(0.05f, 0.12f, 0.05f), false, 0.3f), false);
            Prim(PrimitiveType.Sphere, "Moon", w, v.P(1.2f, -0.9f, 20f), Vector3.one * 1.3f, Mat("SpaceWorld_Moon", new Color(0.8f, 0.8f, 0.85f), new Color(0.2f, 0.2f, 0.22f), false, 0.2f), false);
            // 小惑星の帯
            var rock = Mat("Meteor_Rock", new Color(0.42f, 0.36f, 0.32f), null, false, 0.15f);
            for (int i = 0; i < 40; i++)
            {
                float k = i / 39f, z = Mathf.Lerp(8f, D - 4f, k);
                Prim(PrimitiveType.Sphere, "Asteroid", w, v.P(Mathf.Lerp(-1.6f, 1.5f, k) + R(-0.2f, 0.2f), Mathf.Lerp(-1.3f, 0.1f, k) + R(-0.2f, 0.2f), z), Vector3.one * R(0.15f, 0.5f) * v.S(z) * 0.5f, rock, false)
                    .transform.localRotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), 0f);
            }
            // 回る宇宙ステーション
            var station = Group(w, "Station");
            station.localPosition = v.P(-0.9f, 0.4f, 22f);
            station.localRotation = Quaternion.Euler(25f, 30f, 0f);
            var st = Group(station, "Spin");
            var spin = st.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.forward;
            spin.speed = 12f;
            var hull = Mat("Ufo_Silver", new Color(0.8f, 0.82f, 0.88f), new Color(0.1f, 0.1f, 0.12f), false, 0.9f);
            RingSegments(st, "StationRing", 2.4f, 24, 0.4f, hull);
            Prim(PrimitiveType.Cylinder, "Hub", st, Vector3.zero, new Vector3(0.8f, 0.6f, 0.8f), hull, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Cube, "Spoke", st, Quaternion.Euler(0f, 0f, i * 90f) * new Vector3(0f, 1.2f, 0f), new Vector3(0.15f, 2.4f, 0.15f), hull, false).transform.localRotation = Quaternion.Euler(0f, 0f, i * 90f);
            var panel = Mat("Satellite_Panel", new Color(0.2f, 0.35f, 0.85f), new Color(0.05f, 0.1f, 0.3f), false, 0.8f);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Panel", station, new Vector3(0f, 0f, s * 1.8f), new Vector3(3.4f, 0.03f, 1.2f), panel, false);
            // ほうき星
            var comet = Group(w, "Comet");
            comet.localPosition = v.P(1.3f, 1.3f, 28f);
            comet.localRotation = Quaternion.Euler(0f, 0f, 35f);
            Prim(PrimitiveType.Sphere, "Head", comet, Vector3.zero, Vector3.one * 0.6f, stars[1], false);
            Prim(PrimitiveType.Cube, "Tail", comet, new Vector3(1.8f, 0f, 0f), new Vector3(3.5f, 0.4f, 0.05f), TransparentMat("Comet_Tail", new Color(0.7f, 0.9f, 1f, 0.35f)), false);
        }

        // 海辺: 砂浜の先に水平線まで広がる海と白い波、ヤシの木・パラソル・タオル、帆船、島の灯台、カモメ、雲、夕日のような太陽
        void BuildBeachWorld(Transform w, Vista v)
        {
            const float D = GateDepth, Shore = 11f;
            float g = -GateHole;
            var rand = new System.Random(55);
            float R(float a, float b) => a + (float)rand.NextDouble() * (b - a);
            var sand = Mat("Beach_Sand", new Color(0.95f, 0.86f, 0.62f), new Color(0.12f, 0.1f, 0.06f), false, 0.1f);
            GateGroundAndSky(w, v, null, Mat("Beach_Sky", new Color(0.45f, 0.78f, 1f), null, true), Mat("Beach_SkyBand", new Color(1f, 0.9f, 0.75f), null, true));
            MeshObj(w, "Sand", Trapezoid("GateSand", 0.02f, v.Half(0.02f), Shore, v.Half(Shore)), new Vector3(0f, g, 0f), sand);
            var sea = Mat("Beach_Sea", new Color(0.12f, 0.5f, 0.82f), new Color(0.03f, 0.12f, 0.2f), false, 0.9f);
            MeshObj(w, "Sea", Trapezoid("GateSea", Shore, v.Half(Shore), D + 0.5f, v.Half(D + 0.5f)), new Vector3(0f, g - 0.03f, 0f), sea);
            var foam = Mat("Beach_Foam", new Color(0.95f, 0.98f, 1f), new Color(0.3f, 0.32f, 0.35f), false, 0.5f);
            foreach (float z in new[] { Shore + 0.2f, 13.5f, 16.5f, 20.5f, 26f, 33f })
                Prim(PrimitiveType.Cube, "Wave", w, new Vector3(R(-0.5f, 0.5f), g - 0.01f, z), new Vector3(v.Half(z) * R(1.0f, 1.8f), 0.02f, 0.12f), foam, false);
            // ヤシの木 (しなった幹と、垂れた葉)
            var trunk = Mat("Palm_Trunk", new Color(0.55f, 0.38f, 0.2f), null, false, 0.1f);
            var leaf = Mat("Palm_Leaf", new Color(0.25f, 0.65f, 0.25f), new Color(0.03f, 0.08f, 0.03f), false, 0.2f);
            for (int i = 0; i < 6; i++)
            {
                float z = 3.5f + i * 1.3f, s = i % 2 == 0 ? -1f : 1f;
                var palm = Group(w, "Palm");
                palm.localPosition = new Vector3(s * v.Half(z) * 0.6f, g, z);
                palm.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                Vector3 p = Vector3.zero;
                for (int k = 0; k < 4; k++) // 少しずつ曲がる幹
                {
                    var seg = Prim(PrimitiveType.Cylinder, "Trunk", palm, p + new Vector3(k * 0.08f, 0.45f, 0f), new Vector3(0.22f - k * 0.02f, 0.47f, 0.22f - k * 0.02f), trunk, false);
                    seg.transform.localRotation = Quaternion.Euler(0f, 0f, -k * 6f);
                    p += new Vector3(k * 0.08f + 0.05f, 0.9f, 0f);
                }
                for (int k = 0; k < 7; k++)
                {
                    var l = Prim(PrimitiveType.Sphere, "Leaf", palm, p + Quaternion.Euler(0f, k * 51f, 0f) * new Vector3(0f, -0.1f, 0.7f), new Vector3(0.3f, 0.07f, 1.5f), leaf, false);
                    l.transform.localRotation = Quaternion.Euler(25f, k * 51f, 0f);
                }
                Prim(PrimitiveType.Sphere, "Coconut", palm, p + new Vector3(0.1f, -0.2f, 0f), Vector3.one * 0.18f, trunk, false);
            }
            // パラソルとタオル
            var cloths = new[] { Mat("Beach_Umbrella", new Color(1f, 0.3f, 0.3f), new Color(0.2f, 0.04f, 0.04f), false, 0.3f), Mat("Ball_Blue", new Color(0.25f, 0.5f, 1f), new Color(0.03f, 0.06f, 0.15f), false, 0.7f), Mat("Ball_Yellow", new Color(1f, 0.85f, 0.2f), new Color(0.15f, 0.12f, 0.02f), false, 0.7f) };
            var white = Mat("Barn_White", new Color(0.95f, 0.95f, 0.92f), new Color(0.12f, 0.12f, 0.12f), false, 0.2f);
            for (int i = 0; i < 3; i++)
            {
                float z = 4f + i * 2.5f, x = (i - 1) * v.Half(z) * 0.35f;
                Prim(PrimitiveType.Cylinder, "UmbrellaPole", w, new Vector3(x, g + 0.9f, z), new Vector3(0.05f, 0.9f, 0.05f), white, false);
                Prim(PrimitiveType.Sphere, "Umbrella", w, new Vector3(x, g + 1.8f, z), new Vector3(2f, 0.45f, 2f), cloths[i], false);
                Prim(PrimitiveType.Cube, "Towel", w, new Vector3(x + 0.8f, g + 0.02f, z + 0.3f), new Vector3(0.7f, 0.02f, 1.5f), cloths[(i + 1) % 3], false).transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            }
            // 帆船
            var hull = Mat("Boat_Hull", new Color(0.55f, 0.32f, 0.18f), new Color(0.06f, 0.03f, 0.02f), false, 0.4f);
            for (int i = 0; i < 5; i++)
            {
                float z = R(16f, D - 5f), x = R(-0.7f, 0.7f) * v.Half(z);
                var boat = Group(w, "Sailboat");
                boat.localPosition = new Vector3(x, g, z);
                boat.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                Prim(PrimitiveType.Cube, "Hull", boat, new Vector3(0f, 0.2f, 0f), new Vector3(0.9f, 0.4f, 2.6f), hull, false);
                Prim(PrimitiveType.Cylinder, "Mast", boat, new Vector3(0f, 1.6f, 0f), new Vector3(0.07f, 1.4f, 0.07f), hull, false);
                Prim(PrimitiveType.Cube, "Sail", boat, new Vector3(0f, 1.7f, -0.45f), new Vector3(0.03f, 2.2f, 0.9f), white, false);
                Prim(PrimitiveType.Cube, "Jib", boat, new Vector3(0f, 1.3f, 0.6f), new Vector3(0.03f, 1.4f, 0.6f), cloths[i % 3], false);
            }
            // 島の灯台
            var island = Group(w, "LighthouseIsland");
            island.localPosition = new Vector3(v.Half(34f) * 0.55f, g, 34f);
            Prim(PrimitiveType.Sphere, "Island", island, Vector3.zero, new Vector3(6f, 1.6f, 4f), sand, false);
            Prim(PrimitiveType.Sphere, "IslandGrass", island, new Vector3(-0.6f, 0.4f, 0f), new Vector3(3.5f, 0.9f, 2.5f), Mat("Country_Grass", new Color(0.4f, 0.68f, 0.28f), new Color(0.06f, 0.1f, 0.03f), false, 0.1f), false);
            var lhRed = Mat("Lighthouse_Red", new Color(0.9f, 0.2f, 0.2f), new Color(0.15f, 0.03f, 0.03f), false, 0.4f);
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Cylinder, "Tower", island, new Vector3(0.8f, 0.9f + k * 0.8f, 0f), new Vector3(0.75f - k * 0.06f, 0.4f, 0.75f - k * 0.06f), k % 2 == 0 ? white : lhRed, false);
            Prim(PrimitiveType.Cylinder, "Lamp", island, new Vector3(0.8f, 4.3f, 0f), new Vector3(0.5f, 0.25f, 0.5f), Mat("Lighthouse_Lamp", new Color(1f, 0.95f, 0.6f), new Color(2.5f, 2.2f, 1f), true), false);
            Prim(PrimitiveType.Sphere, "LampRoof", island, new Vector3(0.8f, 4.6f, 0f), new Vector3(0.6f, 0.35f, 0.6f), lhRed, false);
            // カモメ (くの字の白い鳥)
            for (int i = 0; i < 7; i++)
            {
                float z = R(5f, 30f);
                var gull = Group(w, "Gull");
                gull.localPosition = new Vector3(R(-0.7f, 0.7f) * v.Half(z), Mathf.Lerp(g + 2f, v.Top(z), R(0.4f, 0.8f)), z);
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Wing", gull, new Vector3(s * 0.22f, 0.05f, 0f), new Vector3(0.45f, 0.04f, 0.12f), white, false).transform.localRotation = Quaternion.Euler(0f, 0f, s * 20f);
            }
            // 雲と太陽
            var cloud = Mat("Country_Cloud", Color.white, null, true);
            for (int i = 0; i < 5; i++)
            {
                float z = R(20f, D - 3f), x = R(-0.8f, 0.8f) * v.Half(z), y = Mathf.Lerp(g, v.Top(z), R(0.5f, 0.85f));
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Sphere, "Cloud", w, new Vector3(x + (k - 1) * 1.8f, y + R(-0.2f, 0.3f), z), new Vector3(R(2.6f, 3.6f), R(1f, 1.4f), 1.4f), cloud, false);
            }
            Prim(PrimitiveType.Sphere, "Sun", w, new Vector3(-v.Half(D) * 0.3f, Mathf.Lerp(g, v.Top(D), 0.3f), D), Vector3.one * 6f, Mat("Beach_SunLow", new Color(1f, 0.75f, 0.4f), null, true), false);
        }

        // 夜の街: 道の両側に、窓の明かりがともったビルがずっと並ぶ。街灯・車・横断歩道・屋上の給水塔やアンテナ・奥の摩天楼・月と星
        void BuildCityWorld(Transform w, Vista v)
        {
            const float D = GateDepth;
            float g = -GateHole;
            var rand = new System.Random(5);
            float R(float a, float b) => a + (float)rand.NextDouble() * (b - a);
            GateGroundAndSky(w, v, Mat("City_Asphalt", new Color(0.13f, 0.13f, 0.16f), null, false, 0.5f),
                             Mat("City_Night", new Color(0.05f, 0.07f, 0.2f), null, true), Mat("City_NightBand", new Color(0.25f, 0.18f, 0.35f), null, true));
            var line = Mat("City_Line", new Color(1f, 0.85f, 0.3f), new Color(0.6f, 0.45f, 0.1f), false, 0.3f);
            var walk = Mat("City_Sidewalk", new Color(0.4f, 0.4f, 0.44f), null, false, 0.3f);
            var white = Mat("Barn_White", new Color(0.95f, 0.95f, 0.92f), new Color(0.12f, 0.12f, 0.12f), false, 0.2f);
            for (float z = 0.8f; z < D; z += 2f)
                Prim(PrimitiveType.Cube, "LaneDash", w, new Vector3(0f, g + 0.02f, z), new Vector3(0.08f, 0.01f, 1f), line, false);
            for (int k = -3; k <= 3; k++)
                Prim(PrimitiveType.Cube, "Crosswalk", w, new Vector3(k * 0.32f, g + 0.02f, 9f), new Vector3(0.18f, 0.01f, 1.2f), white, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Sidewalk", w, new Vector3(s * 1.55f, g + 0.06f, D / 2f), new Vector3(0.5f, 0.12f, D), walk, false);
            // ビル (窓の明かり・屋上の給水塔やアンテナ)
            var buildings = new[] { Mat("City_BuildingA", new Color(0.25f, 0.27f, 0.38f), null, false, 0.4f), Mat("City_BuildingB", new Color(0.32f, 0.26f, 0.36f), null, false, 0.4f), Mat("City_BuildingC", new Color(0.2f, 0.3f, 0.32f), null, false, 0.4f) };
            var window = Mat("City_Window", new Color(1f, 0.9f, 0.55f), new Color(1.6f, 1.3f, 0.6f), true);
            var windowBlue = Mat("City_WindowBlue", new Color(0.6f, 0.85f, 1f), new Color(0.8f, 1.2f, 1.6f), true);
            var redLight = Mat("City_RedLight", new Color(1f, 0.2f, 0.2f), new Color(2.5f, 0.3f, 0.3f), true);
            var tank = Mat("City_Tank", new Color(0.45f, 0.32f, 0.25f), null, false, 0.3f);
            int n = 0;
            foreach (float s in new[] { -1f, 1f })
                for (float z = 2f; z < D - 2f;)
                {
                    float depth = R(2.5f, 4.5f), zc = z + depth / 2f, inner = 1.85f;
                    float width = Mathf.Min(R(2.5f, 5f), v.Half(z) * 0.95f - inner);
                    z += depth + 0.3f;
                    if (width < 0.6f) continue;
                    float h = Mathf.Min(R(4f, 14f), v.Top(zc) - g - 1.3f); // (屋上の給水塔やアンテナの分もあける)
                    if (h < 2f) continue;
                    float x = s * (inner + width / 2f);
                    Prim(PrimitiveType.Cube, "Building", w, new Vector3(x, g + h / 2f, zc), new Vector3(width, h, depth), buildings[n++ % 3], false);
                    var wm = n % 4 == 0 ? windowBlue : window;
                    for (float wy = 0.8f; wy < h - 0.4f; wy += 0.75f)
                        for (float wz = -depth / 2f + 0.5f; wz < depth / 2f - 0.3f; wz += 0.7f)
                            if (rand.NextDouble() < 0.65)
                                Prim(PrimitiveType.Cube, "Window", w, new Vector3(s * (inner - 0.01f), g + wy, zc + wz), new Vector3(0.02f, 0.4f, 0.35f), wm, false);
                    if (rand.NextDouble() < 0.5)
                    {
                        Prim(PrimitiveType.Cylinder, "WaterTank", w, new Vector3(x, g + h + 0.5f, zc), new Vector3(0.8f, 0.4f, 0.8f), tank, false);
                        Prim(PrimitiveType.Cylinder, "TankRoof", w, new Vector3(x, g + h + 1f, zc), new Vector3(0.9f, 0.08f, 0.9f), tank, false);
                    }
                    else
                    {
                        Prim(PrimitiveType.Cylinder, "Antenna", w, new Vector3(x, g + h + 0.8f, zc), new Vector3(0.05f, 0.8f, 0.05f), walk, false);
                        Prim(PrimitiveType.Sphere, "AntennaLight", w, new Vector3(x, g + h + 1.65f, zc), Vector3.one * 0.15f, redLight, false);
                    }
                }
            // 街灯
            var lampGlow = Mat("City_Lamp", new Color(1f, 0.85f, 0.55f), new Color(2.4f, 1.8f, 0.8f), true);
            foreach (float s in new[] { -1f, 1f })
                for (float z = 3f; z < D; z += 5f)
                {
                    Prim(PrimitiveType.Cylinder, "LampPost", w, new Vector3(s * 1.45f, g + 1.3f, z), new Vector3(0.06f, 1.3f, 0.06f), walk, false);
                    Prim(PrimitiveType.Cube, "LampArm", w, new Vector3(s * 1.3f, g + 2.6f, z), new Vector3(0.35f, 0.04f, 0.04f), walk, false);
                    Prim(PrimitiveType.Sphere, "LampHead", w, new Vector3(s * 1.15f, g + 2.55f, z), new Vector3(0.22f, 0.12f, 0.22f), lampGlow, false);
                }
            // 車 (ヘッドライトとテールランプ)
            var carMats = new[] { Mat("Taxi_Yellow", new Color(1f, 0.82f, 0.15f), new Color(0.15f, 0.12f, 0.02f), false, 0.7f), Mat("Bus_Red", new Color(0.85f, 0.12f, 0.12f), new Color(0.12f, 0.02f, 0.02f), false, 0.6f), Mat("Ufo_Silver", new Color(0.8f, 0.82f, 0.88f), new Color(0.1f, 0.1f, 0.12f), false, 0.9f) };
            var head = Mat("Car_Headlight", new Color(1f, 1f, 0.85f), new Color(2.5f, 2.5f, 2f), true);
            for (int i = 0; i < 5; i++)
            {
                float z = 6f + i * 7f + R(-1f, 1f), x = (i % 2 == 0 ? -0.6f : 0.6f);
                var car = Group(w, "Car");
                car.localPosition = new Vector3(x, g, z);
                if (x > 0f) car.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Prim(PrimitiveType.Cube, "CarBody", car, new Vector3(0f, 0.3f, 0f), new Vector3(0.75f, 0.3f, 1.7f), carMats[i % 3], false);
                Prim(PrimitiveType.Cube, "CarCabin", car, new Vector3(0f, 0.55f, -0.1f), new Vector3(0.68f, 0.25f, 0.9f), Mat("Car_Glass", new Color(0.4f, 0.6f, 0.75f), new Color(0.08f, 0.12f, 0.16f), false, 0.9f), false);
                foreach (float sx in new[] { -0.25f, 0.25f })
                {
                    Prim(PrimitiveType.Cube, "Headlight", car, new Vector3(sx, 0.32f, -0.86f), new Vector3(0.15f, 0.08f, 0.02f), head, false);
                    Prim(PrimitiveType.Cube, "Taillight", car, new Vector3(sx, 0.32f, 0.86f), new Vector3(0.15f, 0.08f, 0.02f), redLight, false);
                }
            }
            // 奥の摩天楼 (影絵のようなビルの並び)
            var dark = Mat("City_Skyline", new Color(0.08f, 0.08f, 0.15f), null, false, 0.3f);
            for (int i = 0; i < 9; i++)
            {
                float x = (i - 4f) / 4f * v.Half(D) * 0.85f, h = R(6f, 13f), wdt = R(1.8f, 3f);
                Prim(PrimitiveType.Cube, "Skyscraper", w, new Vector3(x, g + h / 2f, D - 1.5f), new Vector3(wdt, h, 1.5f), dark, false);
                for (int k = 0; k < 6; k++)
                    Prim(PrimitiveType.Cube, "Window", w, new Vector3(x + R(-wdt * 0.35f, wdt * 0.35f), g + R(1f, h - 0.5f), D - 2.27f), new Vector3(0.3f, 0.3f, 0.02f), window, false);
            }
            Prim(PrimitiveType.Sphere, "Moon", w, new Vector3(v.Half(D) * 0.45f, Mathf.Lerp(g, v.Top(D), 0.8f), D), Vector3.one * 3f, Mat("City_Moon", new Color(1f, 0.97f, 0.8f), null, true), false);
            var star = Mat("SpaceWorld_Star", new Color(1f, 1f, 0.9f), null, true);
            for (int i = 0; i < 40; i++)
                Prim(PrimitiveType.Sphere, "Star", w, new Vector3(R(-1f, 1f) * v.Half(D), Mathf.Lerp(g + 10f, v.Top(D), R(0f, 1f)), D - 0.2f), Vector3.one * R(0.08f, 0.18f), star, false);
        }

        // 雪国: 白い雪原・雪山・もみの木の森・明かりのともる丸太小屋と煙・凍った池・雪だるま・舞う雪
        void BuildSnowWorld(Transform w, Vista v)
        {
            const float D = GateDepth;
            float g = -GateHole;
            var rand = new System.Random(12);
            float R(float a, float b) => a + (float)rand.NextDouble() * (b - a);
            var snow = Mat("Snow_Ground", new Color(0.95f, 0.97f, 1f), new Color(0.15f, 0.16f, 0.18f), false, 0.3f);
            GateGroundAndSky(w, v, snow, Mat("Snow_Sky", new Color(0.62f, 0.78f, 0.95f), null, true), Mat("Snow_SkyBand", new Color(0.9f, 0.94f, 1f), null, true));
            // 雪山 (青みがかった山肌に、白い山頂)
            var rockBlue = Mat("Snow_Mountain", new Color(0.55f, 0.62f, 0.78f), new Color(0.06f, 0.07f, 0.1f), false, 0.2f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * v.Half(D) * 0.4f, h = R(9f, 13f); // (ゲートの枠に隠れる所までに収める)
                Prim(PrimitiveType.Sphere, "Mountain", w, new Vector3(x, g, D - 2f), new Vector3(Mathf.Min(R(12f, 18f), 2f * (v.Half(D) * 1.3f - Mathf.Abs(x))), h * 2f, 5f), rockBlue, false);
                Prim(PrimitiveType.Sphere, "SnowCap", w, new Vector3(x, g + h * 0.75f, D - 2.6f), new Vector3(R(5f, 7f), h * 0.6f, 3f), snow, false);
            }
            // もみの木の森
            var trunk = Mat("Palm_Trunk", new Color(0.55f, 0.38f, 0.2f), null, false, 0.1f);
            var pine = Mat("Snow_Pine", new Color(0.12f, 0.42f, 0.25f), new Color(0.02f, 0.05f, 0.03f), false, 0.2f);
            for (int i = 0; i < 26; i++)
            {
                float z = R(3f, D - 4f), s = i % 2 == 0 ? -1f : 1f, size = R(0.8f, 1.6f);
                float x = s * v.Half(z) * R(0.3f, 0.9f);
                if (!Fits(v, x, 0.9f * size, z, g + 3.4f * size)) continue;
                Prim(PrimitiveType.Cylinder, "PineTrunk", w, new Vector3(x, g + 0.3f * size, z), new Vector3(0.18f, 0.3f, 0.18f) * size, trunk, false);
                for (int k = 0; k < 4; k++)
                {
                    float r = (1.6f - k * 0.35f) * size;
                    Prim(PrimitiveType.Sphere, "PineLayer", w, new Vector3(x, g + (0.8f + k * 0.65f) * size, z), new Vector3(r, 0.8f * size, r), pine, false);
                    Prim(PrimitiveType.Sphere, "PineSnow", w, new Vector3(x, g + (1.1f + k * 0.65f) * size, z), new Vector3(r * 0.7f, 0.2f * size, r * 0.7f), snow, false);
                }
            }
            // 丸太小屋 (窓の明かり・煙突の煙)
            var cabin = Group(w, "Cabin");
            cabin.localPosition = new Vector3(-v.Half(18f) * 0.5f, g, 18f);
            cabin.localRotation = Quaternion.Euler(0f, 70f, 0f);
            var log = Mat("Cabin_Log", new Color(0.5f, 0.32f, 0.18f), new Color(0.05f, 0.03f, 0.02f), false, 0.2f);
            for (int k = 0; k < 6; k++)
                Prim(PrimitiveType.Cylinder, "Log", cabin, new Vector3(0f, 0.2f + k * 0.38f, 0f), new Vector3(0.38f, 1.6f, 0.38f), log, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cube, "CabinBody", cabin, new Vector3(0f, 1.1f, 0f), new Vector3(3f, 2.2f, 2.4f), log, false);
            Prim(PrimitiveType.Cube, "CabinRoof", cabin, new Vector3(0f, 2.4f, 0f), new Vector3(3.3f, 1.9f, 1.9f), snow, false).transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            Prim(PrimitiveType.Cube, "CabinWindow", cabin, new Vector3(0.6f, 1.2f, -1.21f), new Vector3(0.6f, 0.5f, 0.02f), Mat("City_Window", new Color(1f, 0.9f, 0.55f), new Color(1.6f, 1.3f, 0.6f), true), false);
            Prim(PrimitiveType.Cube, "CabinDoor", cabin, new Vector3(-0.6f, 0.75f, -1.21f), new Vector3(0.6f, 1.4f, 0.02f), Mat("Barn_Roof", new Color(0.35f, 0.1f, 0.08f), null, false, 0.2f), false);
            Prim(PrimitiveType.Cube, "Chimney", cabin, new Vector3(0.9f, 3.1f, 0.4f), new Vector3(0.4f, 1f, 0.4f), Mat("Cabin_Stone", new Color(0.5f, 0.5f, 0.52f), null, false, 0.2f), false);
            var smoke = TransparentMat("Cabin_Smoke", new Color(0.85f, 0.87f, 0.9f, 0.5f));
            for (int k = 0; k < 4; k++)
                Prim(PrimitiveType.Sphere, "Smoke", cabin, new Vector3(0.9f + k * 0.25f, 3.9f + k * 0.55f, 0.4f), Vector3.one * (0.4f + k * 0.18f), smoke, false);
            // 凍った池と雪だるま
            Prim(PrimitiveType.Cylinder, "FrozenPond", w, new Vector3(v.Half(14f) * 0.35f, g + 0.02f, 14f), new Vector3(3.5f, 0.01f, 2.4f), Mat("Snow_Ice", new Color(0.7f, 0.88f, 1f), new Color(0.15f, 0.2f, 0.25f), false, 0.95f), false);
            BuildSnowmanShape(w, new Vector3(0.5f, g, 7f), 1.4f);
            BuildSnowmanShape(w, new Vector3(-v.Half(26f) * 0.3f, g, 26f), 1.8f);
            // 舞う雪
            var flake = Mat("Snow_Flake", Color.white, null, true);
            for (int i = 0; i < 90; i++)
            {
                float z = R(1f, D - 1f);
                Prim(PrimitiveType.Sphere, "Snowflake", w, v.P(R(-GateHole, GateHole), R(-1f, GateHole), z), Vector3.one * 0.05f * v.S(z), flake, false);
            }
        }

        // ------------------------------------------------------------------
        // 大きなワープホールと、その向こうの背景 (雲海の上の星空)
        // ------------------------------------------------------------------

        /// <summary>
        /// 大きなワープホール (原点が入り口の真ん中、+Z が奥) の輪。開く時にプレイヤーの方を向け、そのままこちらへ迫ってきて、プレイヤーがくぐる。
        /// 輪の中に見える向こうの背景は、BuildCosmos の星空 (輪を通して見える所だけ描く)。
        /// 輪は大きいので、ふちが床・柱・ゲートを通り抜ける。物に食い込んで見えないように、輪も背景の一部として描く
        /// (星空のすぐ後・床や飾りより先に、奥行きを書かずに描く。床や飾りはいつも輪の手前に見える)
        /// </summary>
        Transform BuildBigHole(Transform root)
        {
            var hole = Group(root, "BigHole");
            var frame = CosmosMat("Hole_Frame", new Color(0.32f, 0.26f, 0.5f), 1f, false, false, BackdropQueue + 50, false);
            var glowA = CosmosMat("Hole_GlowA", new Color(1.2f, 2f, 2.2f), 0f, false, false, BackdropQueue + 51, false);
            var glowB = CosmosMat("Hole_GlowB", new Color(2f, 1.1f, 1.8f), 0f, false, false, BackdropQueue + 51, false);
            RingSegments(hole, "HoleFrame", HoleRadius + 1.3f, 120, 2.6f, frame);
            RingSegments(hole, "HoleGlow", HoleRadius + 0.15f, 120, 0.6f, glowA, null, -1.4f);
            RingSegments(hole, "HoleGlowOuter", HoleRadius + 2.9f, 120, 0.35f, glowB, null, -1.35f);
            return hole;
        }

        // 背景 (ホールの向こうの星空と、ホールの輪) を描く順番。床・飾りなど普通の物 (Geometry = 2000) より先に描く
        const int BackdropQueue = 1800;

        /// <summary>
        /// 大きなホールの向こうの背景の材質 (Shaders/UnknownCosmos.shader。ホールの輪を通して見える所だけ描く)。
        /// shade = 上から光が当たったような明暗 (雲・岩) / transparent = 半透明 (オーロラ) / inside = 内側から見る (空の球) /
        /// queue = 描く順番 / portal = false ならいつも描き、奥行きも書かない (ホールの輪。物に食い込んで見えないように)
        /// </summary>
        Material CosmosMat(string name, Color color, float shade = 0f, bool transparent = false, bool inside = false, int queue = BackdropQueue, bool portal = true)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;
            var shader = Shader.Find("ShootingGallery/Unknown Cosmos");
            if (shader == null)
            {
                Debug.LogWarning("UnknownCosmos.shader が見つからないので、Unlit で代わりにします");
                return transparent ? TransparentMat(name + "_Fallback", color) : Mat(name + "_Fallback", color, null, true);
            }
            var mat = new Material(shader) { name = name };
            mat.SetColor("_Color", color);
            mat.SetFloat("_Shade", shade);
            mat.SetFloat("_Cull", (float)(inside ? UnityEngine.Rendering.CullMode.Front : UnityEngine.Rendering.CullMode.Back));
            mat.SetFloat("_UsePortal", portal ? 1f : 0f);
            if (!portal) mat.SetFloat("_ZWrite", 0f);
            if (transparent)
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
            }
            mat.renderQueue = queue;
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        /// <summary>
        /// ホールの向こうの背景 (くぐった後の背景): 内側から見る夜空の球、空には星とオーロラと大きな月、床の下には雲海。
        /// どれも本物の遠い場所に置き (ふしぎな空間の飾りやゲートの奥の世界より外。カメラの見える距離 100m より内側)、
        /// ホールの輪を通して見える所だけ描く (CosmosMat)。手前の床・飾りは、そのまま前に見える
        /// </summary>
        Transform BuildCosmos(Transform root)
        {
            var cosmos = Group(root, "Cosmos");
            var rand = new System.Random(2027);
            float R(float a, float b) => a + (float)rand.NextDouble() * (b - a);
            Prim(PrimitiveType.Sphere, "Sky", cosmos, Vector3.zero, Vector3.one * 180f, CosmosMat("Cosmos_Sky", new Color(0.04f, 0.05f, 0.16f), 0f, false, true), false);
            var stars = new[] { CosmosMat("Cosmos_Star", new Color(1f, 1f, 0.9f)), CosmosMat("Cosmos_StarBlue", new Color(0.7f, 0.85f, 1f)) };
            for (int i = 0; i < 280; i++)
            {
                var d = new Vector3(R(-1f, 1f), R(0.05f, 1f), R(-1f, 1f));
                Prim(PrimitiveType.Sphere, "Star", cosmos, d.normalized * 85f, Vector3.one * R(0.25f, 0.6f), stars[i % 2], false);
            }
            var cloud = CosmosMat("Cosmos_Cloud", new Color(0.85f, 0.85f, 0.98f), 1f);
            for (int i = 0; i < 46; i++)
            {
                float a = R(0f, Mathf.PI * 2f), d = R(18f, 70f);
                Prim(PrimitiveType.Sphere, "CloudSea", cosmos, new Vector3(Mathf.Sin(a) * d, R(-11f, -7f), Mathf.Cos(a) * d), new Vector3(R(12f, 24f), R(2.5f, 4f), R(12f, 24f)), cloud, false);
            }
            var aurora = new[] { CosmosMat("Cosmos_AuroraGreen", new Color(0.35f, 1f, 0.6f, 0.25f), 0f, true, false, BackdropQueue + 10), CosmosMat("Cosmos_AuroraPink", new Color(1f, 0.45f, 0.85f, 0.2f), 0f, true, false, BackdropQueue + 10) };
            for (int i = 0; i < 7; i++)
            {
                float a0 = R(0f, 360f);
                for (int k = 0; k < 4; k++) // ゆるく曲がった光のカーテン
                {
                    var dir = Quaternion.Euler(0f, a0 + k * 9f, 0f) * Vector3.forward;
                    var q = Prim(PrimitiveType.Quad, "Aurora", cosmos, dir * 72f + Vector3.up * (R(22f, 30f) + k), new Vector3(12f, R(10f, 16f), 1f), aurora[i % 2], false);
                    q.transform.localRotation = Quaternion.LookRotation(dir);
                }
            }
            Prim(PrimitiveType.Sphere, "Moon", cosmos, new Vector3(-35f, 32f, 55f), Vector3.one * 12f, CosmosMat("Cosmos_Moon", new Color(1f, 0.96f, 0.8f)), false);
            return cosmos;
        }
        // ------------------------------------------------------------------
        // 紛れ込んでくる物 (的)
        // ------------------------------------------------------------------

        // 共通: 当たり判定 (体全体を囲む箱)・点数の札・UnknownObject
        UnknownObject Visitor(Transform root, Transform body, int gate, string displayName, int[] points, float labelHeight, Vector3 boxCenter, Vector3 boxSize, Material sparkle, float size)
        {
            var box = body.gameObject.AddComponent<BoxCollider>();
            box.center = boxCenter;
            box.size = boxSize;
            var labelRoot = Group(root, "Label");
            labelRoot.localPosition = Vector3.up * labelHeight;
            var labels = new List<TextMesh> { SpacePointLabel(labelRoot, 0.4f, PointColor(points[points.Length - 1])) };
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.UnknownObject;
            target.manualControl = true;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var o = root.gameObject.AddComponent<UnknownObject>();
            o.displayName = displayName;
            o.gateIndex = gate;
            o.body = body;
            o.hitTarget = target;
            o.pointChoices = points;
            o.pointLabels = labels.ToArray();
            o.labelRoot = labelRoot;
            o.sparkleMaterial = sparkle;
            o.size = size;
            o.boltMaterial = Mat("Lightning_Bolt", new Color(0.8f, 0.5f, 1f), new Color(2.4f, 1.3f, 3.2f), true); // (紫の電撃と同じ稲妻)
            // 転送される時に、物の後ろに出るプラズマボール (半透明の紫の外殻と、光る芯。当たり判定なし。ふだんは隠しておく)
            var plasma = Group(root, "Plasma");
            Prim(PrimitiveType.Sphere, "Shell", plasma, Vector3.zero, Vector3.one, TransparentMat("Plasma_Shell", new Color(0.62f, 0.32f, 1f, 0.32f)), false)
                .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Prim(PrimitiveType.Sphere, "Core", plasma, Vector3.zero, Vector3.one * 0.22f, Mat("Plasma_Core", new Color(1f, 0.85f, 1f), new Color(3f, 1.8f, 4f), true), false)
                .GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            plasma.localScale = Vector3.one * Mathf.Max(boxSize.x, boxSize.y) * 1.2f; // (物より少し大きく、まわりからはみ出して見える)
            plasma.gameObject.SetActive(false);
            o.plasma = plasma;
            return o;
        }

        (Transform root, Transform body) VisitorRoot(Transform parent, string name)
        {
            var root = Group(parent, name);
            return (root, Group(root, "Body"));
        }

        void Wheels(Transform body, Material mat, float x, float y, float z, float d, float w)
        {
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "Wheel", body, new Vector3(s * x, y, z), new Vector3(d, w, d), mat, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // 牛 (白に黒のぶち。とことこ歩くように進む)
        UnknownObject BuildCow(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Cow");
            var white = Mat("Cow_White", new Color(0.97f, 0.96f, 0.93f), new Color(0.1f, 0.1f, 0.1f), false, 0.3f);
            var black = Mat("Cow_Black", new Color(0.1f, 0.1f, 0.12f), null, false, 0.3f);
            var pink = Mat("Cow_Pink", new Color(1f, 0.7f, 0.72f), null, false, 0.3f);
            Prim(PrimitiveType.Cube, "CowBody", body, Vector3.zero, new Vector3(0.5f, 0.42f, 0.85f), white, false);
            Prim(PrimitiveType.Cube, "Spot", body, new Vector3(0.252f, 0.05f, -0.1f), new Vector3(0.01f, 0.22f, 0.3f), black, false);
            Prim(PrimitiveType.Cube, "Spot", body, new Vector3(-0.252f, -0.02f, 0.15f), new Vector3(0.01f, 0.25f, 0.25f), black, false);
            Prim(PrimitiveType.Cube, "Spot", body, new Vector3(0.05f, 0.212f, 0.1f), new Vector3(0.25f, 0.01f, 0.3f), black, false);
            Prim(PrimitiveType.Cube, "Head", body, new Vector3(0f, 0.2f, 0.55f), new Vector3(0.32f, 0.32f, 0.34f), white, false);
            Prim(PrimitiveType.Cube, "Muzzle", body, new Vector3(0f, 0.12f, 0.74f), new Vector3(0.28f, 0.15f, 0.06f), pink, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Horn", body, new Vector3(s * 0.13f, 0.4f, 0.55f), new Vector3(0.05f, 0.1f, 0.05f), Mat("Barn_White", new Color(0.95f, 0.95f, 0.92f), new Color(0.12f, 0.12f, 0.12f), false, 0.2f), false);
                Prim(PrimitiveType.Cube, "Ear", body, new Vector3(s * 0.2f, 0.3f, 0.5f), new Vector3(0.12f, 0.05f, 0.08f), black, false);
                Prim(PrimitiveType.Sphere, "Eye", body, new Vector3(s * 0.09f, 0.26f, 0.72f), Vector3.one * 0.04f, black, false);
                foreach (float z in new[] { -0.28f, 0.28f })
                    Prim(PrimitiveType.Cube, "Leg", body, new Vector3(s * 0.16f, -0.36f, z), new Vector3(0.1f, 0.32f, 0.1f), white, false);
            }
            Prim(PrimitiveType.Cube, "Tail", body, new Vector3(0f, 0f, -0.46f), new Vector3(0.03f, 0.3f, 0.03f), black, false);
            var o = Visitor(root, body, gate, "COW", new[] { 300 }, 0.85f, new Vector3(0f, 0f, 0.1f), new Vector3(0.6f, 1f, 1.2f), sparkle, 0.7f);
            o.faceTravel = true;
            o.bob = 0.1f;
            return o;
        }

        // トラクター (赤い車体・大きな後輪)
        UnknownObject BuildTractor(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Tractor");
            var red = Mat("Tractor_Red", new Color(0.85f, 0.15f, 0.12f), new Color(0.12f, 0.02f, 0.02f), false, 0.5f);
            var tire = Mat("Tire", new Color(0.08f, 0.08f, 0.09f), null, false, 0.2f);
            var glass = Mat("Car_Glass", new Color(0.4f, 0.6f, 0.75f), new Color(0.08f, 0.12f, 0.16f), false, 0.9f);
            Prim(PrimitiveType.Cube, "Hood", body, new Vector3(0f, 0f, 0.15f), new Vector3(0.42f, 0.32f, 0.8f), red, false);
            Prim(PrimitiveType.Cube, "Cab", body, new Vector3(0f, 0.32f, -0.22f), new Vector3(0.48f, 0.42f, 0.42f), glass, false);
            Prim(PrimitiveType.Cube, "CabRoof", body, new Vector3(0f, 0.55f, -0.22f), new Vector3(0.52f, 0.05f, 0.46f), red, false);
            Prim(PrimitiveType.Cylinder, "Exhaust", body, new Vector3(0.12f, 0.3f, 0.4f), new Vector3(0.05f, 0.15f, 0.05f), tire, false);
            Wheels(body, tire, 0.3f, -0.05f, -0.25f, 0.6f, 0.08f);
            Wheels(body, tire, 0.25f, -0.15f, 0.42f, 0.35f, 0.06f);
            var o = Visitor(root, body, gate, "TRACTOR", new[] { 200 }, 0.95f, new Vector3(0f, 0.1f, 0f), new Vector3(0.8f, 0.9f, 1.1f), sparkle, 0.7f);
            o.faceTravel = true;
            o.bob = 0.08f;
            return o;
        }

        // 干し草のロール (ころころ転がる)
        UnknownObject BuildHayBale(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Hay");
            var roll = Group(body, "Roll");
            var spin = roll.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.right;
            spin.speed = 220f;
            Prim(PrimitiveType.Cylinder, "Hay", roll, Vector3.zero, new Vector3(0.7f, 0.35f, 0.7f), Mat("Hay", new Color(0.92f, 0.8f, 0.38f), new Color(0.12f, 0.1f, 0.03f), false, 0.1f), false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cylinder, "HayBand", roll, Vector3.zero, new Vector3(0.72f, 0.03f, 0.72f), Mat("Hay_Band", new Color(0.7f, 0.5f, 0.2f), null, false, 0.1f), false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var o = Visitor(root, body, gate, "HAY", new[] { 100 }, 0.7f, Vector3.zero, new Vector3(0.75f, 0.75f, 0.75f), sparkle, 0.5f);
            o.faceTravel = true;
            o.bob = 0.05f;
            return o;
        }

        // UFO (くるくる回る円盤。窓の明かり)
        UnknownObject BuildUfo(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Ufo");
            Prim(PrimitiveType.Cylinder, "Disc", body, Vector3.zero, new Vector3(1.1f, 0.07f, 1.1f), Mat("Ufo_Silver", new Color(0.8f, 0.82f, 0.88f), new Color(0.1f, 0.1f, 0.12f), false, 0.9f), false);
            Prim(PrimitiveType.Sphere, "Dome", body, new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.38f, 0.5f), Mat("Ufo_Dome", new Color(0.5f, 0.9f, 1f), new Color(0.3f, 0.6f, 0.8f), false, 0.9f), false);
            var lights = new[] { Mat("Ufo_LightA", new Color(1f, 0.85f, 0.3f), null, true), Mat("Ufo_LightB", new Color(0.4f, 1f, 0.6f), null, true) };
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Prim(PrimitiveType.Sphere, "Light", body, new Vector3(Mathf.Sin(a) * 0.5f, -0.02f, Mathf.Cos(a) * 0.5f), Vector3.one * 0.07f, lights[i % 2], false);
            }
            var o = Visitor(root, body, gate, "UFO", new[] { 500 }, 0.6f, Vector3.zero, new Vector3(1.15f, 0.5f, 1.15f), sparkle, 0.7f);
            o.spinAxis = Vector3.up;
            o.spinSpeed = 120f;
            o.bob = 0.2f;
            return o;
        }

        // 人工衛星 (金色の本体と、青い太陽電池の板)
        UnknownObject BuildSatellite(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Satellite");
            Prim(PrimitiveType.Cube, "Core", body, Vector3.zero, new Vector3(0.35f, 0.35f, 0.5f), Mat("Satellite_Gold", new Color(1f, 0.8f, 0.3f), new Color(0.25f, 0.18f, 0.04f), false, 0.85f), false);
            var panel = Mat("Satellite_Panel", new Color(0.2f, 0.35f, 0.85f), new Color(0.05f, 0.1f, 0.3f), false, 0.8f);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Arm", body, new Vector3(s * 0.3f, 0f, 0f), new Vector3(0.25f, 0.03f, 0.03f), Mat("Windmill_Metal", new Color(0.7f, 0.7f, 0.72f), new Color(0.08f, 0.08f, 0.08f), false, 0.6f), false);
                Prim(PrimitiveType.Cube, "Panel", body, new Vector3(s * 0.75f, 0f, 0f), new Vector3(0.65f, 0.02f, 0.35f), panel, false);
            }
            Prim(PrimitiveType.Cylinder, "Dish", body, new Vector3(0f, 0f, 0.3f), new Vector3(0.3f, 0.02f, 0.3f), Mat("Barn_White", new Color(0.95f, 0.95f, 0.92f), new Color(0.12f, 0.12f, 0.12f), false, 0.2f), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var o = Visitor(root, body, gate, "SATELLITE", new[] { 300 }, 0.6f, Vector3.zero, new Vector3(1.6f, 0.4f, 0.6f), sparkle, 0.6f);
            o.spinAxis = new Vector3(0.3f, 1f, 0.2f).normalized;
            o.spinSpeed = 45f;
            return o;
        }

        // 小惑星 (ごろごろ回る岩)
        UnknownObject BuildAsteroid(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Asteroid");
            var rock = Mat("Meteor_Rock", new Color(0.42f, 0.36f, 0.32f), null, false, 0.15f);
            var rockDark = Mat("Meteor_RockDark", new Color(0.28f, 0.24f, 0.22f), null, false, 0.15f);
            Prim(PrimitiveType.Sphere, "Rock", body, Vector3.zero, Vector3.one * 0.6f, rock, false);
            Prim(PrimitiveType.Sphere, "Lump", body, new Vector3(0.18f, 0.1f, 0f), Vector3.one * 0.4f, rockDark, false);
            Prim(PrimitiveType.Sphere, "Lump", body, new Vector3(-0.12f, -0.12f, 0.12f), Vector3.one * 0.38f, rock, false);
            var o = Visitor(root, body, gate, "ASTEROID", new[] { 100 }, 0.6f, Vector3.zero, Vector3.one * 0.7f, sparkle, 0.5f);
            o.spinAxis = new Vector3(1f, 0.6f, 0.3f).normalized;
            o.spinSpeed = 90f;
            return o;
        }

        // ビーチボール (色の帯のある白いボール。くるくる回る)
        UnknownObject BuildBeachBall(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_BeachBall");
            Prim(PrimitiveType.Sphere, "Ball", body, Vector3.zero, Vector3.one * 0.6f, Mat("Ball_White", new Color(0.97f, 0.97f, 0.95f), new Color(0.12f, 0.12f, 0.12f), false, 0.7f), false);
            var bands = new[] { Mat("Ball_Red", new Color(1f, 0.3f, 0.3f), new Color(0.15f, 0.03f, 0.03f), false, 0.7f), Mat("Ball_Blue", new Color(0.25f, 0.5f, 1f), new Color(0.03f, 0.06f, 0.15f), false, 0.7f), Mat("Ball_Yellow", new Color(1f, 0.85f, 0.2f), new Color(0.15f, 0.12f, 0.02f), false, 0.7f) };
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cylinder, "Band", body, Vector3.zero, new Vector3(0.61f, 0.06f, 0.61f), bands[i], false).transform.localRotation = Quaternion.Euler(90f, i * 60f, 0f);
            var o = Visitor(root, body, gate, "BEACH BALL", new[] { 100 }, 0.6f, Vector3.zero, Vector3.one * 0.62f, sparkle, 0.5f);
            o.spinAxis = new Vector3(1f, 0.2f, 0f).normalized;
            o.spinSpeed = 150f;
            o.bob = 0.3f;
            return o;
        }

        // 魚 (オレンジ色。ひれをゆらして泳ぐ)
        UnknownObject BuildFish(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Fish");
            var orange = Mat("Fish_Orange", new Color(1f, 0.55f, 0.2f), new Color(0.2f, 0.08f, 0.02f), false, 0.7f);
            var white = Mat("Ball_White", new Color(0.97f, 0.97f, 0.95f), new Color(0.12f, 0.12f, 0.12f), false, 0.7f);
            Prim(PrimitiveType.Sphere, "FishBody", body, Vector3.zero, new Vector3(0.3f, 0.38f, 0.7f), orange, false);
            Prim(PrimitiveType.Sphere, "Stripe", body, new Vector3(0f, 0f, 0.05f), new Vector3(0.31f, 0.36f, 0.08f), white, false);
            Prim(PrimitiveType.Cube, "Tail", body, new Vector3(0f, 0f, -0.42f), new Vector3(0.04f, 0.3f, 0.22f), orange, false).transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Fin", body, new Vector3(0f, 0.2f, -0.05f), new Vector3(0.03f, 0.12f, 0.2f), orange, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Sphere, "Eye", body, new Vector3(s * 0.12f, 0.06f, 0.22f), Vector3.one * 0.05f, Mat("Cow_Black", new Color(0.1f, 0.1f, 0.12f), null, false, 0.3f), false);
            var o = Visitor(root, body, gate, "FISH", new[] { 200 }, 0.55f, Vector3.zero, new Vector3(0.4f, 0.5f, 0.95f), sparkle, 0.4f);
            o.faceTravel = true;
            o.bob = 0.25f;
            return o;
        }

        // ヨット (茶色い船体と白い帆)
        UnknownObject BuildBoat(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Boat");
            var hull = Mat("Boat_Hull", new Color(0.55f, 0.32f, 0.18f), new Color(0.06f, 0.03f, 0.02f), false, 0.4f);
            var white = Mat("Barn_White", new Color(0.95f, 0.95f, 0.92f), new Color(0.12f, 0.12f, 0.12f), false, 0.2f);
            Prim(PrimitiveType.Cube, "Hull", body, new Vector3(0f, -0.25f, 0f), new Vector3(0.42f, 0.2f, 1.0f), hull, false);
            Prim(PrimitiveType.Cube, "Bow", body, new Vector3(0f, -0.25f, 0.5f), new Vector3(0.3f, 0.2f, 0.3f), hull, false).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            Prim(PrimitiveType.Cylinder, "Mast", body, new Vector3(0f, 0.25f, 0f), new Vector3(0.04f, 0.5f, 0.04f), hull, false);
            Prim(PrimitiveType.Cube, "Sail", body, new Vector3(0f, 0.25f, -0.2f), new Vector3(0.02f, 0.75f, 0.38f), white, false);
            Prim(PrimitiveType.Cube, "Flag", body, new Vector3(0f, 0.8f, -0.06f), new Vector3(0.01f, 0.1f, 0.14f), Mat("Ball_Red", new Color(1f, 0.3f, 0.3f), new Color(0.15f, 0.03f, 0.03f), false, 0.7f), false);
            var o = Visitor(root, body, gate, "YACHT", new[] { 300 }, 1.05f, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 1.2f, 1.2f), sparkle, 0.6f);
            o.faceTravel = true;
            o.bob = 0.12f;
            return o;
        }

        // タクシー (黄色い車)
        UnknownObject BuildTaxi(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Taxi");
            var yellow = Mat("Taxi_Yellow", new Color(1f, 0.82f, 0.15f), new Color(0.15f, 0.12f, 0.02f), false, 0.7f);
            var glass = Mat("Car_Glass", new Color(0.4f, 0.6f, 0.75f), new Color(0.08f, 0.12f, 0.16f), false, 0.9f);
            var tire = Mat("Tire", new Color(0.08f, 0.08f, 0.09f), null, false, 0.2f);
            Prim(PrimitiveType.Cube, "CarBody", body, Vector3.zero, new Vector3(0.55f, 0.25f, 1.05f), yellow, false);
            Prim(PrimitiveType.Cube, "Cabin", body, new Vector3(0f, 0.22f, -0.05f), new Vector3(0.5f, 0.22f, 0.55f), glass, false);
            Prim(PrimitiveType.Cube, "Roof", body, new Vector3(0f, 0.34f, -0.05f), new Vector3(0.52f, 0.03f, 0.57f), yellow, false);
            Prim(PrimitiveType.Cube, "TaxiSign", body, new Vector3(0f, 0.41f, -0.05f), new Vector3(0.22f, 0.1f, 0.08f), Mat("Taxi_Sign", new Color(1f, 1f, 0.9f), null, true), false);
            Wheels(body, tire, 0.27f, -0.12f, 0.33f, 0.22f, 0.05f);
            Wheels(body, tire, 0.27f, -0.12f, -0.33f, 0.22f, 0.05f);
            var o = Visitor(root, body, gate, "TAXI", new[] { 200 }, 0.75f, new Vector3(0f, 0.1f, 0f), new Vector3(0.6f, 0.6f, 1.1f), sparkle, 0.6f);
            o.faceTravel = true;
            o.bob = 0.08f;
            return o;
        }

        // 2階建てのバス (赤い車体と、明かりのついた窓)
        UnknownObject BuildBus(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Bus");
            var red = Mat("Bus_Red", new Color(0.85f, 0.12f, 0.12f), new Color(0.12f, 0.02f, 0.02f), false, 0.6f);
            var window = Mat("City_Window", new Color(1f, 0.9f, 0.55f), new Color(1.6f, 1.3f, 0.6f), true);
            var tire = Mat("Tire", new Color(0.08f, 0.08f, 0.09f), null, false, 0.2f);
            Prim(PrimitiveType.Cube, "BusBody", body, new Vector3(0f, 0.15f, 0f), new Vector3(0.55f, 0.7f, 1.4f), red, false);
            foreach (float y in new[] { 0.0f, 0.33f })
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Windows", body, new Vector3(s * 0.28f, y + 0.08f, 0f), new Vector3(0.01f, 0.14f, 1.2f), window, false);
            Prim(PrimitiveType.Cube, "Windshield", body, new Vector3(0f, 0.2f, 0.705f), new Vector3(0.45f, 0.45f, 0.01f), window, false);
            Wheels(body, tire, 0.27f, -0.2f, 0.45f, 0.24f, 0.05f);
            Wheels(body, tire, 0.27f, -0.2f, -0.45f, 0.24f, 0.05f);
            var o = Visitor(root, body, gate, "BUS", new[] { 300 }, 0.9f, new Vector3(0f, 0.15f, 0f), new Vector3(0.6f, 0.8f, 1.45f), sparkle, 0.7f);
            o.faceTravel = true;
            o.bob = 0.08f;
            return o;
        }

        // 風船 (赤い風船とひも。ふわふわ)
        UnknownObject BuildBalloon(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Balloon");
            var red = Mat("Balloon_Red", new Color(1f, 0.25f, 0.35f), new Color(0.2f, 0.04f, 0.06f), false, 0.85f);
            Prim(PrimitiveType.Sphere, "Balloon", body, new Vector3(0f, 0.2f, 0f), new Vector3(0.45f, 0.55f, 0.45f), red, false);
            Prim(PrimitiveType.Sphere, "Knot", body, new Vector3(0f, -0.09f, 0f), Vector3.one * 0.06f, red, false);
            Prim(PrimitiveType.Cube, "String", body, new Vector3(0f, -0.4f, 0f), new Vector3(0.01f, 0.6f, 0.01f), Mat("Barn_White", new Color(0.95f, 0.95f, 0.92f), new Color(0.12f, 0.12f, 0.12f), false, 0.2f), false);
            var o = Visitor(root, body, gate, "BALLOON", new[] { 100 }, 0.75f, new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.8f, 0.5f), sparkle, 0.4f);
            o.spinSpeed = 15f;
            o.bob = 0.3f;
            return o;
        }

        // 雪だるま (こちらを向いている)
        UnknownObject BuildSnowman(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Snowman");
            BuildSnowmanShape(body, new Vector3(0f, -0.35f, 0f), 1f);
            var o = Visitor(root, body, gate, "SNOWMAN", new[] { 300 }, 0.9f, new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 1.2f, 0.6f), sparkle, 0.6f);
            o.facePlayer = true;
            o.bob = 0.12f;
            return o;
        }

        // 雪だるまの形 (原点が足元、顔は -Z)
        void BuildSnowmanShape(Transform parent, Vector3 basePos, float s)
        {
            var snow = Mat("Snow_Ground", new Color(0.95f, 0.97f, 1f), new Color(0.15f, 0.16f, 0.18f), false, 0.3f);
            var black = Mat("Cow_Black", new Color(0.1f, 0.1f, 0.12f), null, false, 0.3f);
            Prim(PrimitiveType.Sphere, "Bottom", parent, basePos + new Vector3(0f, 0.27f, 0f) * s, Vector3.one * 0.55f * s, snow, false);
            Prim(PrimitiveType.Sphere, "Middle", parent, basePos + new Vector3(0f, 0.65f, 0f) * s, Vector3.one * 0.4f * s, snow, false);
            Prim(PrimitiveType.Sphere, "Head", parent, basePos + new Vector3(0f, 0.95f, 0f) * s, Vector3.one * 0.3f * s, snow, false);
            Prim(PrimitiveType.Cylinder, "HatBrim", parent, basePos + new Vector3(0f, 1.08f, 0f) * s, new Vector3(0.3f, 0.01f, 0.3f) * s, black, false);
            Prim(PrimitiveType.Cylinder, "Hat", parent, basePos + new Vector3(0f, 1.17f, 0f) * s, new Vector3(0.2f, 0.09f, 0.2f) * s, black, false);
            Prim(PrimitiveType.Cylinder, "Scarf", parent, basePos + new Vector3(0f, 0.82f, 0f) * s, new Vector3(0.34f, 0.03f, 0.34f) * s, Mat("Ball_Red", new Color(1f, 0.3f, 0.3f), new Color(0.15f, 0.03f, 0.03f), false, 0.7f), false);
            Prim(PrimitiveType.Cylinder, "Nose", parent, basePos + new Vector3(0f, 0.95f, -0.17f) * s, new Vector3(0.04f, 0.07f, 0.04f) * s, Mat("Snow_Carrot", new Color(1f, 0.5f, 0.1f), new Color(0.2f, 0.08f, 0.01f), false, 0.4f), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float x in new[] { -0.06f, 0.06f })
                Prim(PrimitiveType.Sphere, "Eye", parent, basePos + new Vector3(x, 1.0f, -0.135f) * s, Vector3.one * 0.035f * s, black, false);
            foreach (float y in new[] { 0.58f, 0.7f })
                Prim(PrimitiveType.Sphere, "Button", parent, basePos + new Vector3(0f, y, -0.2f) * s, Vector3.one * 0.04f * s, black, false);
        }

        // そり (赤い板と銀のすべり木。上に小さなプレゼント)
        UnknownObject BuildSled(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Sled");
            var red = Mat("Sled_Red", new Color(0.8f, 0.12f, 0.12f), new Color(0.12f, 0.02f, 0.02f), false, 0.6f);
            var metal = Mat("Windmill_Metal", new Color(0.7f, 0.7f, 0.72f), new Color(0.08f, 0.08f, 0.08f), false, 0.6f);
            Prim(PrimitiveType.Cube, "Seat", body, Vector3.zero, new Vector3(0.45f, 0.06f, 0.9f), red, false);
            Prim(PrimitiveType.Cube, "Back", body, new Vector3(0f, 0.15f, -0.43f), new Vector3(0.45f, 0.3f, 0.05f), red, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Runner", body, new Vector3(s * 0.2f, -0.12f, 0.05f), new Vector3(0.03f, 0.03f, 1.0f), metal, false);
                Prim(PrimitiveType.Cube, "RunnerTip", body, new Vector3(s * 0.2f, -0.05f, 0.55f), new Vector3(0.03f, 0.03f, 0.18f), metal, false).transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            }
            Prim(PrimitiveType.Cube, "Gift", body, new Vector3(0f, 0.13f, 0.1f), Vector3.one * 0.2f, Mat("Gift_Green", new Color(0.2f, 0.7f, 0.35f), new Color(0.03f, 0.1f, 0.05f), false, 0.5f), false);
            var o = Visitor(root, body, gate, "SLED", new[] { 200 }, 0.6f, new Vector3(0f, 0.05f, 0f), new Vector3(0.5f, 0.45f, 1.05f), sparkle, 0.5f);
            o.faceTravel = true;
            o.bob = 0.1f;
            return o;
        }

        // プレゼントの箱 (リボン付き。くるくる回る。点数が高い)
        UnknownObject BuildPresent(Transform parent, int gate, Material sparkle)
        {
            var (root, body) = VisitorRoot(parent, "Visitor_Present");
            var box = Mat("Gift_Blue", new Color(0.3f, 0.5f, 1f), new Color(0.05f, 0.08f, 0.2f), false, 0.6f);
            var ribbon = Mat("Gift_Ribbon", new Color(1f, 0.85f, 0.3f), new Color(0.3f, 0.22f, 0.05f), false, 0.8f);
            Prim(PrimitiveType.Cube, "Box", body, Vector3.zero, Vector3.one * 0.45f, box, false);
            Prim(PrimitiveType.Cube, "RibbonA", body, Vector3.zero, new Vector3(0.08f, 0.46f, 0.46f), ribbon, false);
            Prim(PrimitiveType.Cube, "RibbonB", body, Vector3.zero, new Vector3(0.46f, 0.46f, 0.08f), ribbon, false);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Sphere, "Bow", body, new Vector3(s * 0.07f, 0.26f, 0f), new Vector3(0.12f, 0.07f, 0.07f), ribbon, false);
            var o = Visitor(root, body, gate, "PRESENT", new[] { 500 }, 0.6f, Vector3.zero, Vector3.one * 0.5f, sparkle, 0.5f);
            o.spinAxis = Vector3.up;
            o.spinSpeed = 90f;
            return o;
        }

        // ------------------------------------------------------------------
        // 屋台に出る的の見た目
        // ------------------------------------------------------------------

        /// <summary>Unknown ステージの的: 紺の板に、光る輪の小さなワープゲート。輪の中には星空と惑星が奥に見える</summary>
        GameObject UnknownLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Unknown_LookPlate", new Color(0.12f, 0.12f, 0.28f), new Color(0.03f, 0.03f, 0.08f)));
            var gate = Group(look, "MiniGate");
            gate.localPosition = c + new Vector3(0f, 0.015f, -0.03f);
            RingSegments(gate, "MiniGateRing", 0.09f, 24, 0.022f, Mat("Gate_Glow_Space", new Color(0.55f, 0.5f, 1f), new Color(0.55f, 0.5f, 1f) * 1.8f, true), Mat("Gate_Glow_City", new Color(1f, 0.45f, 0.75f), new Color(1f, 0.45f, 0.75f) * 1.8f, true));
            Prim(PrimitiveType.Cylinder, "MiniGateSky", gate, new Vector3(0f, 0f, 0.008f), new Vector3(0.17f, 0.002f, 0.17f), Mat("SpaceWorld_Dark", new Color(0.02f, 0.02f, 0.06f), null, true), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "MiniPlanet", gate, new Vector3(0.025f, 0.02f, 0.004f), Vector3.one * 0.04f, Mat("SpaceWorld_Planet", new Color(1f, 0.6f, 0.3f), new Color(0.35f, 0.15f, 0.05f), false, 0.3f), false);
            var star = Mat("SpaceWorld_Star", new Color(1f, 1f, 0.9f), null, true);
            foreach (var p in new[] { new Vector2(-0.04f, 0.03f), new Vector2(-0.02f, -0.05f), new Vector2(0.05f, -0.03f), new Vector2(-0.055f, -0.01f) })
                Prim(PrimitiveType.Sphere, "MiniStar", gate, new Vector3(p.x, p.y, 0.004f), Vector3.one * 0.008f, star, false);
            MakeText(look, "Label", "UNKNOWN", c + new Vector3(0f, -0.115f, -0.025f), 0.035f, new Color(0.75f, 0.85f, 1f));
            return look.gameObject;
        }
    }
}
