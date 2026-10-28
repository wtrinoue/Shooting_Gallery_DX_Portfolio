using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、音楽モード (四分音符の的) のコンサートホールと丸いカーテン・五線譜・的の見た目。
    /// ホールのローカルの原点 = プレイヤーの足元、+Z = ステージの方 (始まる時に、的があった方へ回す)。
    /// 楽器は GalleryBuilder.MusicInstruments.cs
    /// </summary>
    public partial class GalleryBuilder
    {
        const float HallFront = 4.6f;      // ステージの手前の縁 (z)
        const float HallBack = 17.6f;      // ステージの奥の壁 (z)
        const float StageTop = 1f;         // ステージの床の高さ
        const float StageHalfW = 11.2f;    // ステージの幅の半分
        const float OpeningHalfW = 10.2f;  // 舞台の額縁 (プロセニアム) の開口の幅の半分
        const float OpeningTop = 8.6f;     // 開口の上端
        const float HallHalfW = 14f;       // 客席の壁 (x)
        const float HallRear = -14f;       // 客席の後ろの壁 (z)
        const float HallH = 12f;           // 客席の天井
        const float ConductorZ = 5.8f;     // 指揮台 (楽器はここを中心に扇形に並ぶ)
        const float ConcertCurtainR = 2f;  // プレイヤーを囲むカーテンの半径
        const float ConcertCurtainH = 2.35f;
        const float HallShiftZ = 0.8f;     // ホールを奥へずらす量 (プレイヤーは客席の前の方、ステージの手前の縁から約 5.4m)
        const float ConcertLiftH = 4.2f;   // プレイヤーを浮き床ごと持ち上げる高さ (ステージ全体を見下ろせて、奥のソロの楽器も狙いやすいように)
        public const string ConcertBgmPath = "Assets/Source/music_stage/music_stage.mp3"; // 音楽モードの BGM (魔王魂。約134BPM・ヘ短調)
        public const string ConcertBeatMapPath = "Assets/Source/music_stage/music_stage.csv"; // その曲の拍の表 (time,bar,beat。ユーザーが手で打った)
        const float PlatformR = 1.4f;      // 浮き床の半径 (足元だけの小さな台)
        const float RailH = 0.35f;         // 浮き床の手すりの高さ (足元を囲むだけの低い手すり)

        static readonly float[] RiserR = { 3.9f, 5.4f, 6.9f, 8.6f };  // ひな壇の段の内側の半径 (指揮台から)
        const float RiserStep = 0.75f;   // ひな壇の1段の高さ (奥の楽器も手前の楽器に隠れず、狙いやすいように高め)
        const float RiserMaxDeg = 65f;

        ConcertHall BuildConcertHall()
        {
            var rootGo = new GameObject("ConcertHall");
            rootGo.SetActive(false); // 部品をそろえてから動かす (再生中に作る時、ConcertHall.Awake が先に走らないように)
            rootGo.transform.SetParent(world, false);
            var root = rootGo.transform;
            var hall = Group(root, "Hall");
            hall.localPosition = new Vector3(0f, 0f, HallShiftZ);
            var lift = Group(root, "Lift"); // カーテンと浮き床 (プレイヤーといっしょに上がる)
            var platform = BuildLiftPlatform(lift);

            var lights = BuildHallShell(hall);
            BuildHallSeats(hall);
            BuildHallBalconies(hall);
            BuildHallCeiling(hall);
            BuildHallDetails(hall);
            BuildStage(hall);
            var instruments = BuildOrchestra(hall);
            var baton = BuildConductor(hall);
            var staffs = new List<MusicStaff>();
            for (int i = 0; i < 4; i++) staffs.Add(BuildMusicStaff(root, i));
            var curtain = BuildRoundCurtain(lift);

            var concert = rootGo.AddComponent<ConcertHall>();
            concert.hall = hall.gameObject;
            concert.curtain = curtain;
            concert.stageLights = lights.stage;
            concert.houseLight = lights.house;
            concert.stageFrontZ = HallFront; // これより奥の部品だけを、ステージの明かりで照らす
            concert.followSpots = lights.follow;
            concert.followBeams = lights.beams;
            concert.showWhilePlaying = lights.showWhilePlaying;
            concert.instruments = instruments.ToArray();
            concert.baton = baton;
            concert.staffs = staffs.ToArray();
            concert.lift = lift;
            concert.platform = platform;
            concert.liftHeight = ConcertLiftH;
            // BGM: 魔王魂 の曲 (実機では GalleryRuntimeAssets から。無ければコードで作った曲。クレジットは Resources/THIRD_PARTY_NOTICES.txt)
            concert.bgmClip = LoadSourceAsset(ConcertBgmPath, a => a.concertBgm);
            concert.beatMap = LoadSourceAsset(ConcertBeatMapPath, a => a.concertBeatMap); // 人が曲に合わせて打った拍の表 (指揮棒・楽器の拍)
            // 五線譜が出てくる所: ステージの左右の端の上空。五線譜の始まり (縦線・ト音記号・調号) が奥の壁・横の幕・天井の反響板に食い込まないように、
            // どれからも余裕をとって離す (線の帯は上下に ±0.6m、ト音記号はさらに上へ 0.5m ほど出る)
            concert.staffStartZ = HallBack + HallShiftZ - 3.5f;  //   奥から (奥の壁より 3.5m 手前。パイプオルガンにもかからない)
            concert.staffNearZ = HallFront + HallShiftZ + 4.6f;  //   手前まで (端の手前のコントラバス・第1バイオリンより奥)
            concert.staffEdgeX = OpeningHalfW - 2.3f;            //   舞台の両わきの幕 (OpeningHalfW - 1.6 〜 OpeningHalfW) より 0.7m 以上内側
            concert.staffLowY = StageTop + 1.3f;                 //   ステージの床のすぐ上 (ト音記号が床に埋まらない)
            concert.staffHighY = OpeningTop - 2.1f;              //   〜 天井の反響板 (8.35m) より 1.2m 以上下 (ト音記号の頭も当たらない)

            // 動かない部品 (客席・壁・飾りなど) をまとめる。ステージの明かりはステージ側の部品だけを照らすので、ステージ側と客席側は分けてまとめ、
            // ステージ側にはここで StageLightLayer を付けておく (実行中の ConcertHall.SplitStageLighting は、まとめた部品を飛ばす)
            var hallT = hall;
            CombineStatic(rootGo.transform, "Concert", 8f, null,
                c => hallT.InverseTransformPoint(c).z > HallFront ? 1 : 0,
                (mr, part) => { if (part == 1) mr.renderingLayerMask |= ConcertHall.StageLightLayer; });
            hall.gameObject.SetActive(false);
            curtain.gameObject.SetActive(false);
            platform.SetActive(false);
            foreach (var s in staffs) s.gameObject.SetActive(false);
            rootGo.SetActive(true);
            return concert;
        }

        // ------------------------------------------------------------------
        // ホールの建物 (床・壁・天井・舞台の額縁・幕・照明)
        // ------------------------------------------------------------------

        (Light[] stage, Light house, Light[] follow, Transform[] beams, GameObject[] showWhilePlaying) BuildHallShell(Transform hall)
        {
            var carpet = Mat("Concert_Carpet", new Color(0.32f, 0.05f, 0.07f));
            var aisle = Mat("Concert_Aisle", new Color(0.45f, 0.13f, 0.1f));
            var wall = Mat("Concert_Wall", new Color(0.62f, 0.42f, 0.25f), null, false, 0.4f);
            var pilaster = Mat("Concert_Pilaster", new Color(0.46f, 0.29f, 0.16f), null, false, 0.45f);
            var ceiling = Mat("Concert_Ceiling", new Color(0.8f, 0.76f, 0.68f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var velvet = Mat("Concert_Velvet", new Color(0.5f, 0.04f, 0.07f), new Color(0.05f, 0f, 0.01f));
            var sconce = Mat("Concert_Sconce", new Color(1f, 0.8f, 0.5f), new Color(1.2f, 0.8f, 0.4f));
            var black = Mat("Concert_Black", new Color(0.04f, 0.04f, 0.05f), null, false, 0.7f);

            float depth = HallBack - HallRear;
            float midZ = (HallBack + HallRear) / 2f;
            // 床 (赤いじゅうたん) と通路
            Prim(PrimitiveType.Cube, "HallFloor", hall, new Vector3(0f, 0f, midZ), new Vector3(HallHalfW * 2f, 0.02f, depth), carpet);
            foreach (float x in new[] { -7f, 7f })
                Prim(PrimitiveType.Cube, "Aisle", hall, new Vector3(x, 0.012f, (HallRear + HallFront) / 2f), new Vector3(1f, 0.004f, HallFront - HallRear), aisle, false);
            // 壁と天井
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "SideWall", hall, new Vector3(s * (HallHalfW + 0.2f), HallH / 2f, midZ), new Vector3(0.4f, HallH, depth), wall);
                for (float z = HallRear + 2f; z < HallFront; z += 4f)
                {
                    Prim(PrimitiveType.Cube, "Pilaster", hall, new Vector3(s * (HallHalfW - 0.15f), HallH / 2f - 0.2f, z), new Vector3(0.3f, HallH - 0.4f, 0.5f), pilaster, false);
                    Prim(PrimitiveType.Cube, "Sconce", hall, new Vector3(s * (HallHalfW - 0.35f), 2.3f, z + 2f), new Vector3(0.12f, 0.3f, 0.15f), sconce, false);
                }
            }
            Prim(PrimitiveType.Cube, "BackWall", hall, new Vector3(0f, HallH / 2f, HallRear - 0.2f), new Vector3(HallHalfW * 2f + 0.8f, HallH, 0.4f), wall);
            Prim(PrimitiveType.Cube, "Ceiling", hall, new Vector3(0f, HallH + 0.2f, midZ), new Vector3(HallHalfW * 2f + 0.8f, 0.4f, depth + 0.8f), ceiling);
            Chandelier(hall, new Vector3(0f, HallH - 2.4f, -4f), gold, sconce);

            // 舞台の額縁 (プロセニアム): 開口のまわりの壁と金の縁
            float sideW = HallHalfW - OpeningHalfW;
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Proscenium", hall, new Vector3(s * (OpeningHalfW + sideW / 2f), HallH / 2f, HallFront), new Vector3(sideW, HallH, 0.6f), wall);
                Prim(PrimitiveType.Cube, "FrameGold", hall, new Vector3(s * (OpeningHalfW + 0.1f), (StageTop + OpeningTop) / 2f, HallFront - 0.32f), new Vector3(0.35f, OpeningTop - StageTop, 0.12f), gold, false);
            }
            Prim(PrimitiveType.Cube, "ProsceniumTop", hall, new Vector3(0f, (OpeningTop + HallH) / 2f, HallFront), new Vector3(OpeningHalfW * 2f, HallH - OpeningTop, 0.6f), wall);
            Prim(PrimitiveType.Cube, "FrameGoldTop", hall, new Vector3(0f, OpeningTop + 0.1f, HallFront - 0.32f), new Vector3(OpeningHalfW * 2f + 0.55f, 0.35f, 0.12f), gold, false);

            // 両わきにたぐって留めた赤い幕と、上の飾り幕
            var drapeMesh = PleatPanelMesh(1.6f, OpeningTop - StageTop, 10, 0.08f);
            foreach (float s in new[] { -1f, 1f })
            {
                var drape = MeshPart(hall, "StageDrape", drapeMesh, velvet);
                drape.localPosition = new Vector3(s * (OpeningHalfW - 0.8f), StageTop, HallFront + 0.3f);
                Prim(PrimitiveType.Cylinder, "DrapeTie", hall, new Vector3(s * (OpeningHalfW - 0.8f), StageTop + 3.2f, HallFront + 0.2f), new Vector3(1.7f, 0.05f, 0.3f), gold, false);
            }
            var valance = MeshPart(hall, "StageValance", PleatPanelMesh(OpeningHalfW * 2f, 1.1f, 60, 0.05f), velvet);
            valance.localPosition = new Vector3(0f, OpeningTop - 1.1f, HallFront + 0.25f);
            Prim(PrimitiveType.Cube, "ValanceFringe", hall, new Vector3(0f, OpeningTop - 1.12f, HallFront + 0.2f), new Vector3(OpeningHalfW * 2f, 0.08f, 0.06f), gold, false);

            // 照明: ステージを照らす2灯 + 上からの明かり / 客席の明かり / ソロを追う2灯
            var stage = new List<Light>();
            var showWhilePlaying = new List<GameObject>();
            foreach (float s in new[] { -1f, 1f })
                stage.Add(HallSpot(hall, "StageWash", new Vector3(s * 6f, HallH - 1.2f, 1.5f), new Vector3(s * 1.5f, StageTop + 0.5f, 10.5f), 80f, 50f, 36f, new Color(1f, 0.93f, 0.8f), black));
            // 楽器の形が分かるように、ステージの上の真ん中から1つの明かりで全体を照らす (前は左右2つ。明かりが多いと Quest で重くなるので1つにまとめた)
            stage.Add(HallPoint(hall, "StageFill", new Vector3(0f, 7.4f, 10.5f), new Color(1f, 0.92f, 0.8f), 24f, 18f));
            // (前は手前の楽器を正面から照らす明かりもあったが、GPU が重くなるのでやめた。ステージは真上の明かりと環境光で照らす)
            var house = HallPoint(hall, "HouseLight", new Vector3(0f, HallH - 3f, -5f), new Color(1f, 0.85f, 0.7f), 1.6f, 15f); // (客席だけを照らす広さ。ステージまで届くと、ステージの物が明かりを1つ多く計算して重くなる)
            var follow = new List<Light>();
            var beams = new List<Transform>();
            var beamMat = TransparentMat("Concert_Beam", new Color(1f, 0.95f, 0.8f, 0.1f));
            foreach (float s in new[] { -1f, 1f })
            {
                var spot = HallSpot(hall, "FollowSpot", new Vector3(s * 3.5f, HallH - 0.6f, 0.5f), new Vector3(0f, StageTop + 1f, 10f), 9f, 60f, 36f, new Color(1f, 0.97f, 0.9f), black);
                follow.Add(spot);
                var beam = new GameObject("FollowBeam");
                beam.transform.SetParent(hall, false);
                beam.AddComponent<MeshFilter>().sharedMesh = ConeMesh(0.12f, 0.75f, 1f);
                var br = beam.AddComponent<MeshRenderer>();
                br.sharedMaterial = beamMat;
                br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.transform.localPosition = spot.transform.localPosition;
                beam.transform.localRotation = spot.transform.localRotation;
                beam.transform.localScale = new Vector3(1f, 1f, 10f);
                beams.Add(beam.transform);
                showWhilePlaying.Add(beam);
            }
            // ステージの縁の明かり (演奏中だけ点く)
            var foot = Group(hall, "Footlights");
            var footMat = Mat("Concert_Footlight", new Color(1f, 0.9f, 0.6f), new Color(2f, 1.6f, 0.9f), true);
            for (int i = 0; i < 25; i++)
                Prim(PrimitiveType.Sphere, "Footlight", foot, new Vector3(-9.6f + i * 0.8f, StageTop + 0.03f, HallFront + 0.12f), new Vector3(0.12f, 0.06f, 0.12f), footMat, false);
            showWhilePlaying.Add(foot.gameObject);
            return (stage.ToArray(), house, follow.ToArray(), beams.ToArray(), showWhilePlaying.ToArray());
        }

        Light HallSpot(Transform parent, string name, Vector3 pos, Vector3 aim, float angle, float intensity, float range, Color color, Material housing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.LookRotation(aim - pos);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.intensity = intensity;
            l.range = range;
            l.color = color;
            l.shadows = LightShadows.None;
            // 天井の照明器具 (黒い箱)
            Prim(PrimitiveType.Cube, "SpotHousing", parent, pos + Vector3.up * 0.2f, new Vector3(0.4f, 0.35f, 0.4f), housing, false);
            return l;
        }

        Light HallPoint(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        // 客席の上のシャンデリア (光る玉の輪。明かりは HouseLight)
        void Chandelier(Transform hall, Vector3 pos, Material gold, Material bulb)
        {
            var c = Group(hall, "Chandelier");
            c.localPosition = pos;
            Prim(PrimitiveType.Cylinder, "ChainRod", c, Vector3.up * 1.2f, new Vector3(0.05f, 1.2f, 0.05f), gold, false);
            Prim(PrimitiveType.Sphere, "Center", c, Vector3.zero, Vector3.one * 0.5f, bulb, false);
            for (int ring = 0; ring < 2; ring++)
            {
                float r = ring == 0 ? 1.1f : 0.7f;
                int n = ring == 0 ? 14 : 9;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    Prim(PrimitiveType.Sphere, "Bulb", c, new Vector3(Mathf.Sin(a) * r, -0.25f - ring * 0.35f, Mathf.Cos(a) * r), Vector3.one * 0.16f, bulb, false);
                }
            }
            FlatRing(c, "ChandelierRing", 1.1f, -0.33f, 0.05f, 0.05f, 28, gold);
        }

        Transform MeshPart(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>ひだのある幕 (幅 width・高さ height。原点が下の縁の真ん中、XY 平面。表裏とも見える)</summary>
        Mesh PleatPanelMesh(float width, float height, int pleats, float depth)
        {
            string name = $"Shape_Pleat_{width:0.0}_{height:0.0}_{pleats}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int cols = pleats * 2 + 1;
            for (int side = 0; side < 2; side++)
            {
                int o = verts.Count;
                for (int j = 0; j < cols; j++)
                {
                    float x = -width / 2f + width * j / (cols - 1);
                    float z = depth * (j % 2 == 0 ? 1f : -1f);
                    verts.Add(new Vector3(x, height, z));
                    verts.Add(new Vector3(x, 0f, z));
                }
                for (int j = 0; j < cols - 1; j++)
                {
                    int a = o + j * 2, b = a + 1, c = a + 2, d = a + 3;
                    if (side == 0) tris.AddRange(new[] { a, c, b, b, c, d });
                    else tris.AddRange(new[] { a, b, c, b, d, c });
                }
            }
            return FinishMesh(name, verts, tris);
        }

        // ------------------------------------------------------------------
        // 客席 (座席は全部まとめて2つのメッシュにする。数百個を別々に置くと重いので)
        // ------------------------------------------------------------------

        void BuildHallSeats(Transform hall)
        {
            var cushion = new List<(Vector3 c, Vector3 s)>();
            var frame = new List<(Vector3 c, Vector3 s)>();
            for (float z = HallFront - 1.2f; z > HallRear + 1f; z -= 1f)
            {
                for (float x = -HallHalfW + 1.4f; x <= HallHalfW - 1.4f + 0.01f; x += 0.6f)
                {
                    if (Mathf.Abs(Mathf.Abs(x) - 7f) < 0.65f) continue;                  // 通路
                    if (new Vector2(x, z + HallShiftZ).magnitude < PlatformR + 0.5f) continue; // プレイヤーの浮き床の下は空ける
                    cushion.Add((new Vector3(x, 0.44f, z), new Vector3(0.5f, 0.1f, 0.46f)));          // 座面
                    cushion.Add((new Vector3(x, 0.8f, z - 0.25f), new Vector3(0.52f, 0.62f, 0.09f)));  // 背もたれ (ステージと反対側)
                    frame.Add((new Vector3(x + 0.29f, 0.32f, z - 0.03f), new Vector3(0.06f, 0.62f, 0.5f))); // ひじかけ・脚
                }
            }
            MeshPart(hall, "Seats", BoxesMesh("Shape_ConcertSeats", cushion), Mat("Concert_Seat", new Color(0.45f, 0.06f, 0.09f)));
            MeshPart(hall, "SeatFrames", BoxesMesh("Shape_ConcertSeatFrames", frame), Mat("Concert_SeatFrame", new Color(0.12f, 0.08f, 0.06f)));
        }

        /// <summary>たくさんの箱を1つのメッシュにまとめる (面ごとに頂点を分けて、角がくっきり見えるように)</summary>
        Mesh BoxesMesh(string name, List<(Vector3 c, Vector3 s)> boxes)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var verts = new List<Vector3>(boxes.Count * 24);
            var normals = new List<Vector3>(boxes.Count * 24);
            var tris = new List<int>(boxes.Count * 36);
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var (c, s) in boxes)
            {
                var h = s * 0.5f;
                foreach (var n in dirs)
                {
                    // 面の2つの辺の向き (u × v = n になるように)
                    Vector3 u = n.y != 0f ? Vector3.right : Vector3.up;
                    Vector3 v = Vector3.Cross(n, u);
                    int o = verts.Count;
                    Vector3 fc = c + Vector3.Scale(n, h);
                    Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                    verts.Add(fc - du - dv); verts.Add(fc + du - dv); verts.Add(fc + du + dv); verts.Add(fc - du + dv);
                    for (int k = 0; k < 4; k++) normals.Add(n);
                    tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
                }
            }
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh = SaveMeshIfEditing(mesh, name);
            meshes[name] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------
        // バルコニー席 (後ろの壁に3段、横の壁に階段状にずれたボックス席を2段)。
        // 手すりの上の縁と、床の下の縁に暖かい光のラインを通す (客席を振り返った時に、明るい木のホールに光の線が何本も走って見える)。
        // 部品が多いので、材質ごとに1つのメッシュにまとめる
        // ------------------------------------------------------------------

        static readonly float[] BalconyY = { 3.4f, 6.3f, 9.1f }; // 各段の床の高さ (後ろの壁)。横のボックス席は下の2段
        const float BalconyDepth = 3.2f;   // 後ろのバルコニーの奥行き
        const float BoxDepth = 1.7f;       // 横のボックス席の奥行き (いちばん浅い所)

        void BuildHallBalconies(Transform hall)
        {
            var wood = new List<(Vector3 c, Vector3 s)>();   // 手すりの板・床の厚み
            var soffit = new List<(Vector3 c, Vector3 s)>(); // 床の下の面 (明るい色)
            var glow = new List<(Vector3 c, Vector3 s)>();   // 光のライン
            var cushion = new List<(Vector3 c, Vector3 s)>();
            var frame = new List<(Vector3 c, Vector3 s)>();
            const float Slab = 0.35f, Rail = 0.95f;

            // 後ろのバルコニー: 壁いっぱいの幅。上の段ほど少し奥まる。席は3列で、後ろの列ほど高い
            float halfW = HallHalfW;
            for (int k = 0; k < BalconyY.Length; k++)
            {
                float y = BalconyY[k];
                float front = HallRear + BalconyDepth - k * 0.35f;
                float d = front - HallRear;
                float cz = (front + HallRear) / 2f;
                wood.Add((new Vector3(0f, y - Slab / 2f, cz), new Vector3(halfW * 2f, Slab, d)));                     // 床の厚み
                soffit.Add((new Vector3(0f, y - Slab - 0.005f, cz), new Vector3(halfW * 2f - 0.1f, 0.01f, d - 0.1f))); // 床の下の面
                wood.Add((new Vector3(0f, y + Rail / 2f, front - 0.06f), new Vector3(halfW * 2f, Rail, 0.12f)));        // 手すりの板
                glow.Add((new Vector3(0f, y + Rail + 0.03f, front - 0.06f), new Vector3(halfW * 2f, 0.05f, 0.16f)));    // 手すりの上の光
                glow.Add((new Vector3(0f, y - Slab - 0.03f, front - 0.15f), new Vector3(halfW * 2f, 0.05f, 0.08f)));    // 床の下の縁の光
                for (int row = 0; row < 3; row++)
                {
                    float z = front - 0.75f - row * 0.85f;
                    float fy = y + row * 0.38f; // 段々
                    if (row > 0) wood.Add((new Vector3(0f, y + row * 0.19f, z - 0.05f), new Vector3(halfW * 2f, row * 0.38f, 0.85f))); // 段 (床から積む)
                    for (float x = -halfW + 0.8f; x <= halfW - 0.8f + 0.01f; x += 0.6f)
                        AddSeat(cushion, frame, new Vector3(x, fy, z), Vector3.forward);
                }
            }

            // 横のボックス席: 3つずつに区切り、ステージに近いほど張り出して階段状に見せる
            float[] segZ = { -10.6f, -6.4f, -2.2f, 2f };
            for (int k = 0; k < 2; k++)
            {
                float y = BalconyY[k];
                foreach (float s in new[] { -1f, 1f })
                    for (int g = 0; g < 3; g++)
                    {
                        float z0 = segZ[g] + 0.15f, z1 = segZ[g + 1] - 0.15f, cz = (z0 + z1) / 2f, len = z1 - z0;
                        float depth = BoxDepth + g * 0.35f;
                        float inner = HallHalfW - depth;          // 手すりの x (内側)
                        float cx = s * (HallHalfW - depth / 2f);
                        wood.Add((new Vector3(cx, y - Slab / 2f, cz), new Vector3(depth, Slab, len)));
                        soffit.Add((new Vector3(cx, y - Slab - 0.005f, cz), new Vector3(depth - 0.1f, 0.01f, len - 0.1f)));
                        wood.Add((new Vector3(s * (inner + 0.06f), y + Rail / 2f, cz), new Vector3(0.12f, Rail, len)));
                        foreach (float ez in new[] { z0 + 0.05f, z1 - 0.05f })                                                // 両端の仕切り
                            wood.Add((new Vector3(cx, y + Rail / 2f, ez), new Vector3(depth, Rail, 0.1f)));
                        glow.Add((new Vector3(s * (inner + 0.06f), y + Rail + 0.03f, cz), new Vector3(0.16f, 0.05f, len)));
                        glow.Add((new Vector3(s * (inner + 0.15f), y - Slab - 0.03f, cz), new Vector3(0.08f, 0.05f, len)));
                        for (float z = z0 + 0.45f; z <= z1 - 0.45f + 0.01f; z += 0.6f)
                            AddSeat(cushion, frame, new Vector3(s * (inner + 0.7f), y, z), new Vector3(-s, 0f, 0f)); // 真ん中 (ステージの方) を向く
                    }
            }

            MeshPart(hall, "BalconyWood", BoxesMesh("Shape_ConcertBalconyWood", wood), Mat("Concert_BalconyWood", new Color(0.55f, 0.35f, 0.19f), null, false, 0.45f));
            MeshPart(hall, "BalconySoffit", BoxesMesh("Shape_ConcertBalconySoffit", soffit), Mat("Concert_Soffit", new Color(0.78f, 0.62f, 0.42f)));
            MeshPart(hall, "BalconyGlow", BoxesMesh("Shape_ConcertBalconyGlow", glow), Mat("Concert_BalconyGlow", new Color(0.7f, 0.55f, 0.38f), new Color(0.55f, 0.38f, 0.18f), true) /* (客席側は控えめに光らせる) */);
            MeshPart(hall, "BalconySeats", BoxesMesh("Shape_ConcertBalconySeats", cushion), Mat("Concert_Seat", new Color(0.45f, 0.06f, 0.09f)));
            MeshPart(hall, "BalconySeatFrames", BoxesMesh("Shape_ConcertBalconySeatFrames", frame), Mat("Concert_SeatFrame", new Color(0.12f, 0.08f, 0.06f)));
        }

        // 座席1つ (pos = 床の高さの座席の真ん中、face = 座った人が向く水平の向き。+Z か ±X のどれか)
        static void AddSeat(List<(Vector3 c, Vector3 s)> cushion, List<(Vector3 c, Vector3 s)> frame, Vector3 pos, Vector3 face)
        {
            bool alongZ = Mathf.Abs(face.z) > 0.5f;
            Vector3 Size(float w, float h, float d) => alongZ ? new Vector3(w, h, d) : new Vector3(d, h, w);
            cushion.Add((pos + Vector3.up * 0.44f, Size(0.5f, 0.1f, 0.46f)));                // 座面
            cushion.Add((pos + Vector3.up * 0.8f - face * 0.25f, Size(0.52f, 0.62f, 0.09f))); // 背もたれ
            var side = alongZ ? Vector3.right : Vector3.forward;
            frame.Add((pos + side * 0.29f + Vector3.up * 0.32f, Size(0.06f, 0.62f, 0.5f)));   // ひじかけ・脚
        }

        // ------------------------------------------------------------------
        // ホールの細かい飾り (壁の木のルーバー、バルコニーの手すりの縦のリブと金の飾り線、後ろの出口の扉と非常口の灯り、
        // 通路の足元灯、列の端の木の袖板、ステージの上の反響板、舞台の額縁の金の飾り)。材質ごとに1つのメッシュにまとめる
        // ------------------------------------------------------------------

        void BuildHallDetails(Transform hall)
        {
            var slat = new List<(Vector3 c, Vector3 s)>();   // 濃い木: 壁のルーバー・リブ・袖板・扉
            var gold = new List<(Vector3 c, Vector3 s)>();   // 金: 飾り線・取っ手
            var glow = new List<(Vector3 c, Vector3 s)>();   // 足元灯
            const float Slab = 0.35f, Rail = 0.95f;

            // 横の壁の下 (ボックス席の下) に、縦の木のルーバー。腰の高さに金の線
            foreach (float s in new[] { -1f, 1f })
            {
                for (float z = HallRear + 0.3f; z < HallFront - 0.4f; z += 0.26f)
                    slat.Add((new Vector3(s * (HallHalfW - 0.04f), 1.65f, z), new Vector3(0.07f, 2.7f, 0.1f)));
                gold.Add((new Vector3(s * (HallHalfW - 0.09f), 1f, (HallRear + HallFront) / 2f), new Vector3(0.03f, 0.04f, HallFront - HallRear)));
            }
            // 後ろの壁の下にもルーバー (扉の所は空ける)
            float[] doorX = { -7f, 0f, 7f };
            for (float x = -HallHalfW + 0.3f; x < HallHalfW - 0.2f; x += 0.26f)
            {
                bool door = false;
                foreach (float dx in doorX) if (Mathf.Abs(x - dx) < 1.05f) door = true;
                if (!door) slat.Add((new Vector3(x, 1.5f, HallRear + 0.04f), new Vector3(0.1f, 2.4f, 0.07f)));
            }
            // 出口の扉 (両開き。金の取っ手) と、上の緑の非常口の灯り
            var exitMat = Mat("Concert_Exit", new Color(0.1f, 0.85f, 0.35f), new Color(0.3f, 1.6f, 0.6f), true);
            foreach (float dx in doorX)
            {
                slat.Add((new Vector3(dx, 2.35f, HallRear + 0.05f), new Vector3(2.1f, 0.12f, 0.1f)));                  // 枠の上
                foreach (float e in new[] { -1f, 1f })
                {
                    slat.Add((new Vector3(dx + e * 1.0f, 1.15f, HallRear + 0.05f), new Vector3(0.1f, 2.3f, 0.1f)));      // 枠の横
                    slat.Add((new Vector3(dx + e * 0.47f, 1.12f, HallRear + 0.03f), new Vector3(0.9f, 2.2f, 0.04f)));    // 扉
                    gold.Add((new Vector3(dx + e * 0.12f, 1.05f, HallRear + 0.07f), new Vector3(0.03f, 0.35f, 0.04f)));  // 取っ手
                }
                Prim(PrimitiveType.Cube, "ExitSign", hall, new Vector3(dx, 2.62f, HallRear + 0.08f), new Vector3(0.6f, 0.22f, 0.06f), exitMat, false);
                var t = MakeText(hall, "ExitText", "EXIT", new Vector3(dx, 2.62f, HallRear + 0.12f), 0.12f, Color.white);
                t.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // ステージの方から読める向き
            }

            // バルコニー・ボックス席の手すりの表に、縦のリブと金の飾り線 (BuildHallBalconies と同じ寸法)
            for (int k = 0; k < BalconyY.Length; k++)
            {
                float y = BalconyY[k];
                float front = HallRear + BalconyDepth - k * 0.35f;
                for (float x = -HallHalfW + 0.4f; x < HallHalfW - 0.3f; x += 0.5f)
                    slat.Add((new Vector3(x, y + Rail / 2f, front + 0.01f), new Vector3(0.05f, Rail - 0.1f, 0.05f)));
                gold.Add((new Vector3(0f, y + 0.12f, front + 0.02f), new Vector3(HallHalfW * 2f, 0.035f, 0.04f)));
                gold.Add((new Vector3(0f, y - Slab + 0.05f, front + 0.01f), new Vector3(HallHalfW * 2f, 0.035f, 0.04f)));
            }
            float[] segZ = { -10.6f, -6.4f, -2.2f, 2f };
            for (int k = 0; k < 2; k++)
            {
                float y = BalconyY[k];
                foreach (float s in new[] { -1f, 1f })
                    for (int g = 0; g < 3; g++)
                    {
                        float z0 = segZ[g] + 0.15f, z1 = segZ[g + 1] - 0.15f;
                        float inner = HallHalfW - (BoxDepth + g * 0.35f);
                        for (float z = z0 + 0.3f; z < z1 - 0.2f; z += 0.5f)
                            slat.Add((new Vector3(s * (inner - 0.01f), y + Rail / 2f, z), new Vector3(0.05f, Rail - 0.1f, 0.05f)));
                        gold.Add((new Vector3(s * (inner - 0.02f), y + 0.12f, (z0 + z1) / 2f), new Vector3(0.04f, 0.035f, z1 - z0)));
                    }
            }

            // 通路: 列ごとに、通路の両側の袖板 (木) と足元灯
            for (float z = HallFront - 1.2f; z > HallRear + 1f; z -= 1f)
                foreach (float ax in new[] { -7f, 7f })
                    foreach (float e in new[] { -1f, 1f })
                    {
                        if (new Vector2(ax + e * 0.62f, z + HallShiftZ).magnitude < PlatformR + 0.5f) continue; // 浮き床の下は空ける
                        slat.Add((new Vector3(ax + e * 0.62f, 0.5f, z - 0.05f), new Vector3(0.05f, 0.95f, 0.55f)));
                        glow.Add((new Vector3(ax + e * 0.58f, 0.12f, z - 0.05f), new Vector3(0.03f, 0.05f, 0.12f)));
                    }

            MeshPart(hall, "DetailWood", BoxesMesh("Shape_ConcertDetailWood", slat), Mat("Concert_Slat", new Color(0.4f, 0.25f, 0.13f), null, false, 0.45f));
            MeshPart(hall, "DetailGold", BoxesMesh("Shape_ConcertDetailGold", gold), Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f)));
            MeshPart(hall, "AisleLights", BoxesMesh("Shape_ConcertAisleLights", glow), Mat("Concert_AisleLight", new Color(1f, 0.8f, 0.5f), new Color(1.4f, 0.95f, 0.45f), true));

            // ステージの上の反響板 (少し手前に傾けた明るい木の板3枚)
            var reflector = Mat("Concert_Reflector", new Color(0.72f, 0.52f, 0.32f), null, false, 0.5f);
            for (int i = 0; i < 3; i++)
            {
                var p = Prim(PrimitiveType.Cube, "Reflector", hall, new Vector3(0f, 8.38f + i * 0.04f, 7.5f + i * 2.8f), new Vector3(OpeningHalfW * 1.6f, 0.06f, 2.4f), reflector, false); // 天井のすぐ下 (五線譜が下を通れるように)
                p.transform.localRotation = Quaternion.Euler(-5f, 0f, 0f);
            }

            // 舞台の額縁の金の飾り: 内側にもう1本の細い金の線と、等間隔の丸い飾り (ロゼット)
            var goldMat = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "FrameInner", hall, new Vector3(s * (OpeningHalfW + 0.42f), (StageTop + OpeningTop) / 2f, HallFront - 0.31f), new Vector3(0.06f, OpeningTop - StageTop, 0.06f), goldMat, false);
                for (float y = StageTop + 0.8f; y < OpeningTop - 0.3f; y += 1.2f)
                    Prim(PrimitiveType.Sphere, "Rosette", hall, new Vector3(s * (OpeningHalfW + 0.1f), y, HallFront - 0.4f), new Vector3(0.22f, 0.22f, 0.1f), goldMat, false);
            }
            Prim(PrimitiveType.Cube, "FrameInnerTop", hall, new Vector3(0f, OpeningTop + 0.42f, HallFront - 0.31f), new Vector3(OpeningHalfW * 2f + 0.9f, 0.06f, 0.06f), goldMat, false);
            for (float x = -OpeningHalfW + 0.8f; x < OpeningHalfW - 0.5f; x += 1.2f)
                Prim(PrimitiveType.Sphere, "Rosette", hall, new Vector3(x, OpeningTop + 0.1f, HallFront - 0.4f), new Vector3(0.22f, 0.22f, 0.1f), goldMat, false);
        }

        // 天井: 明るい格子 (格天井) と、その升目ごとのダウンライト。シャンデリアの上に光る楕円の天窓
        void BuildHallCeiling(Transform hall)
        {
            var beams = new List<(Vector3 c, Vector3 s)>();
            var lights = new List<(Vector3 c, Vector3 s)>();
            float y = HallH, z0 = HallRear, z1 = HallFront, w = HallHalfW * 2f;
            const float Cell = 2f, BeamH = 0.4f;
            for (float x = -HallHalfW + Cell; x < HallHalfW - 0.01f; x += Cell)
                beams.Add((new Vector3(x, y - BeamH / 2f, (z0 + z1) / 2f), new Vector3(0.22f, BeamH, z1 - z0)));
            for (float z = z0 + Cell; z < z1 - 0.01f; z += Cell)
                beams.Add((new Vector3(0f, y - BeamH / 2f, z), new Vector3(w, BeamH, 0.22f)));
            for (float x = -HallHalfW + Cell / 2f; x < HallHalfW; x += Cell)
                for (float z = z0 + Cell / 2f; z < z1; z += Cell)
                {
                    if (Mathf.Abs(x) < 5f && Mathf.Abs(z + 4f) < 3.5f) continue; // 天窓の所は空ける
                    lights.Add((new Vector3(x, y - 0.03f, z), new Vector3(0.22f, 0.05f, 0.22f)));
                }
            MeshPart(hall, "CeilingBeams", BoxesMesh("Shape_ConcertCeilingBeams", beams), Mat("Concert_Coffer", new Color(0.68f, 0.62f, 0.52f)));
            MeshPart(hall, "CeilingLights", BoxesMesh("Shape_ConcertCeilingLights", lights), Mat("Concert_Downlight", new Color(1f, 0.95f, 0.82f), new Color(1.6f, 1.4f, 1f), true));

            // 天窓 (シャンデリアの真上。楕円の光る面と、まわりの縁)
            Prim(PrimitiveType.Cylinder, "SkylightFrame", hall, new Vector3(0f, y - BeamH - 0.03f, -4f), new Vector3(10.4f, 0.03f, 7.2f), Mat("Concert_Coffer", new Color(0.68f, 0.62f, 0.52f)), false);
            Prim(PrimitiveType.Cylinder, "Skylight", hall, new Vector3(0f, y - BeamH - 0.07f, -4f), new Vector3(9.4f, 0.03f, 6.4f), Mat("Concert_Skylight", new Color(1f, 0.96f, 0.86f), new Color(1.15f, 1.05f, 0.85f), true), false);
        }

        // ------------------------------------------------------------------
        // ステージ (床・奥の壁・パイプオルガン・ひな壇)
        // ------------------------------------------------------------------

        void BuildStage(Transform hall)
        {
            var floor = Mat("Concert_StageFloor", new Color(0.13f, 0.1f, 0.09f), null, false, 0.55f);
            var front = Mat("Concert_StageFront", new Color(0.18f, 0.1f, 0.06f), null, false, 0.4f);
            var shell = Mat("Concert_Shell", new Color(0.34f, 0.23f, 0.14f), null, false, 0.4f);
            var shellDark = Mat("Concert_ShellDark", new Color(0.24f, 0.16f, 0.1f), null, false, 0.4f);
            var riser = Mat("Concert_Riser", new Color(0.09f, 0.075f, 0.07f), null, false, 0.45f);
            var pipe = Mat("Concert_Pipe", new Color(0.82f, 0.82f, 0.86f), new Color(0.06f, 0.06f, 0.07f), false, 0.85f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));

            float depth = HallBack - HallFront;
            float midZ = (HallBack + HallFront) / 2f;
            Prim(PrimitiveType.Cube, "Stage", hall, new Vector3(0f, StageTop / 2f, midZ), new Vector3(StageHalfW * 2f, StageTop, depth), floor);
            Prim(PrimitiveType.Cube, "StageFront", hall, new Vector3(0f, StageTop / 2f, HallFront - 0.02f), new Vector3(StageHalfW * 2f, StageTop - 0.02f, 0.04f), front, false);
            Prim(PrimitiveType.Cube, "StageEdge", hall, new Vector3(0f, StageTop, HallFront - 0.03f), new Vector3(OpeningHalfW * 2f, 0.05f, 0.06f), gold, false);
            // 奥の壁 (音を響かせる木の板) と横の壁・天井
            float wallH = OpeningTop - StageTop;
            Prim(PrimitiveType.Cube, "ShellBack", hall, new Vector3(0f, StageTop + wallH / 2f, HallBack + 0.2f), new Vector3(StageHalfW * 2f + 0.4f, wallH, 0.4f), shell);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "ShellSide", hall, new Vector3(s * (StageHalfW + 0.2f), StageTop + wallH / 2f, midZ), new Vector3(0.4f, wallH, depth), shell);
            Prim(PrimitiveType.Cube, "ShellCeiling", hall, new Vector3(0f, OpeningTop + 0.2f, midZ), new Vector3(StageHalfW * 2f + 0.8f, 0.4f, depth + 0.4f), shellDark);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cube, "ShellPanel", hall, new Vector3(-8f + i * 4f, StageTop + 1.6f, HallBack - 0.02f), new Vector3(3.4f, 2.6f, 0.08f), shellDark, false);

            // パイプオルガン (真ん中が高い)
            var organ = Group(hall, "Organ");
            Prim(PrimitiveType.Cube, "OrganCase", organ, new Vector3(0f, 4.1f, HallBack - 0.35f), new Vector3(11.6f, 0.5f, 0.5f), shellDark, false);
            for (int i = -10; i <= 10; i++)
            {
                float x = i * 0.52f;
                float h = 1.6f + 2.6f * (1f - Mathf.Abs(i) / 10.5f) + (i % 2 == 0 ? 0.35f : 0f);
                float d = Mathf.Abs(i) < 3 ? 0.32f : 0.24f;
                Prim(PrimitiveType.Cylinder, "OrganPipe", organ, new Vector3(x, 4.35f + h / 2f, HallBack - 0.4f), new Vector3(d, h / 2f, d), pipe, false);
                Prim(PrimitiveType.Cube, "PipeMouth", organ, new Vector3(x, 4.65f, HallBack - 0.4f - d / 2f), new Vector3(d * 0.5f, 0.08f, 0.02f), Mat("Concert_Black", new Color(0.04f, 0.04f, 0.05f), null, false, 0.7f), false);
            }
            Prim(PrimitiveType.Cube, "OrganCrown", organ, new Vector3(0f, 8.25f, HallBack - 0.35f), new Vector3(3.2f, 0.18f, 0.3f), gold, false);

            // ひな壇 (指揮台を中心にした扇形の段。奥ほど高い)
            for (int k = 0; k < RiserR.Length; k++)
            {
                var part = MeshPart(hall, $"Riser{k}", SectorMesh(RiserR[k], 11.6f, RiserMaxDeg, RiserStep * (k + 1)), riser);
                part.localPosition = new Vector3(0f, StageTop, ConductorZ);
                part.gameObject.AddComponent<MeshCollider>().sharedMesh = part.GetComponent<MeshFilter>().sharedMesh;
            }
        }

        /// <summary>指揮台から見た扇形の段 (内側の半径 inner 〜 外側 outer、角度 ±maxDeg、高さ h)。上の面・手前の面・両はしの面</summary>
        Mesh SectorMesh(float inner, float outer, float maxDeg, float h)
        {
            string name = $"Shape_Sector_{inner:0.0}_{outer:0.0}_{maxDeg:0}_{h:0.00}";
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            const int Segs = 26;
            Vector3 P(float deg, float r, float y) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r); }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) // a-b-c-d を表から見て時計回り
            {
                int o = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
            for (int i = 0; i < Segs; i++)
            {
                float d0 = Mathf.Lerp(-maxDeg, maxDeg, i / (float)Segs), d1 = Mathf.Lerp(-maxDeg, maxDeg, (i + 1) / (float)Segs);
                Quad(P(d0, inner, h), P(d0, outer, h), P(d1, outer, h), P(d1, inner, h));  // 上の面
                Quad(P(d0, inner, 0f), P(d0, inner, h), P(d1, inner, h), P(d1, inner, 0f)); // 手前の面 (指揮台の方を向く)
            }
            Quad(P(-maxDeg, outer, 0f), P(-maxDeg, outer, h), P(-maxDeg, inner, h), P(-maxDeg, inner, 0f)); // 左のはし
            Quad(P(maxDeg, inner, 0f), P(maxDeg, inner, h), P(maxDeg, outer, h), P(maxDeg, outer, 0f));     // 右のはし
            return FinishMesh(name, verts, tris);
        }

        /// <summary>指揮台を中心に、角度 deg (0 = 正面奥、マイナス = 客席から見て左)・半径 r の所 (ステージの床またはひな壇の上)</summary>
        static Vector3 OrchestraSpot(float deg, float r)
        {
            float a = deg * Mathf.Deg2Rad;
            float y = StageTop;
            if (Mathf.Abs(deg) <= RiserMaxDeg)
                for (int k = 0; k < RiserR.Length; k++)
                    if (r >= RiserR[k]) y = StageTop + RiserStep * (k + 1);
            return new Vector3(Mathf.Sin(a) * r, y, ConductorZ + Mathf.Cos(a) * r);
        }

        // 指揮台と譜面台、拍子をとる指揮棒 (指揮者はいない)
        Transform BuildConductor(Transform hall)
        {
            var c = Group(hall, "Conductor");
            var black = Mat("Concert_Black", new Color(0.04f, 0.04f, 0.05f), null, false, 0.7f);
            Prim(PrimitiveType.Cube, "Podium", c, new Vector3(0f, StageTop + 0.11f, ConductorZ), new Vector3(0.9f, 0.22f, 0.9f), Mat("Concert_Carpet", new Color(0.32f, 0.05f, 0.07f)));
            Prim(PrimitiveType.Cube, "PodiumRail", c, new Vector3(0f, StageTop + 0.65f, ConductorZ - 0.42f), new Vector3(0.8f, 0.04f, 0.04f), Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f)), false);
            Prim(PrimitiveType.Cylinder, "StandPole", c, new Vector3(0f, StageTop + 0.7f, ConductorZ + 0.45f), new Vector3(0.03f, 0.48f, 0.03f), black, false);
            var desk = Prim(PrimitiveType.Cube, "StandDesk", c, new Vector3(0f, StageTop + 1.22f, ConductorZ + 0.42f), new Vector3(0.55f, 0.38f, 0.02f), black, false);
            desk.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            var score = Prim(PrimitiveType.Cube, "Score", c, new Vector3(0f, StageTop + 1.24f, ConductorZ + 0.405f), new Vector3(0.5f, 0.33f, 0.01f), Mat("Concert_Paper", new Color(0.95f, 0.93f, 0.85f)), false);
            score.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            // 指揮棒 (譜面台の上に浮かんで、4拍子の図形を描く)
            var pivot = Group(c, "Baton");
            pivot.localPosition = new Vector3(0.2f, StageTop + 1.55f, ConductorZ + 0.1f);
            Prim(PrimitiveType.Sphere, "BatonGrip", pivot, Vector3.zero, new Vector3(0.04f, 0.04f, 0.06f), Mat("Concert_Cork", new Color(0.65f, 0.45f, 0.25f)), false);
            var stick = Prim(PrimitiveType.Cylinder, "BatonStick", pivot, new Vector3(0f, 0.04f, 0.22f), new Vector3(0.012f, 0.23f, 0.012f), Mat("Concert_White", new Color(0.95f, 0.95f, 0.92f), new Color(0.2f, 0.2f, 0.18f)), false);
            stick.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            return pivot;
        }

        // ------------------------------------------------------------------
        // プレイヤーを囲む丸いカーテン
        // ------------------------------------------------------------------

        StageCurtain BuildRoundCurtain(Transform root)
        {
            var go = new GameObject("RoundCurtain");
            go.transform.SetParent(root, false);
            var frame = Group(go.transform, "Frame");
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var velvet = Mat("Concert_Velvet", new Color(0.5f, 0.04f, 0.07f), new Color(0.05f, 0f, 0.01f));
            // 天井 (濃い灰色の円盤) とレール
            Prim(PrimitiveType.Cylinder, "CurtainCeiling", frame, Vector3.up * (ConcertCurtainH + 0.1f), new Vector3((ConcertCurtainR + 0.2f) * 2f, 0.03f, (ConcertCurtainR + 0.2f) * 2f),
                 Mat("Concert_CurtainCeiling", new Color(0.16f, 0.16f, 0.18f)), false);
            FlatRing(frame, "CurtainRail", ConcertCurtainR, ConcertCurtainH + 0.03f, 0.05f, 0.05f, 48, gold);
            var fabric = MeshPart(frame, "CurtainFabric", null, velvet);
            var valance = MeshPart(frame, "CurtainValance", null, velvet);
            FlatRing(frame, "ValanceFringe", ConcertCurtainR + 0.07f, ConcertCurtainH + 0.06f - 0.28f, 0.03f, 0.05f, 48, gold);
            var lampGo = new GameObject("CurtainLamp");
            lampGo.transform.SetParent(frame, false);
            lampGo.transform.localPosition = Vector3.up * (ConcertCurtainH - 0.15f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.8f, 0.6f);
            lamp.intensity = 1.5f;
            lamp.range = 4.5f;
            lamp.shadows = LightShadows.None;
            Prim(PrimitiveType.Sphere, "CurtainBulb", frame, Vector3.up * (ConcertCurtainH + 0.02f), new Vector3(0.25f, 0.12f, 0.25f), Mat("Concert_Sconce", new Color(1f, 0.8f, 0.5f), new Color(1.2f, 0.8f, 0.4f)), false);

            var curtain = go.AddComponent<StageCurtain>();
            curtain.frame = frame;
            curtain.fabric = fabric.GetComponent<MeshFilter>();
            curtain.valance = valance.GetComponent<MeshFilter>();
            curtain.lamp = lamp;
            curtain.radius = ConcertCurtainR;
            curtain.height = ConcertCurtainH;
            frame.localPosition = Vector3.up * 6f; // 上に隠しておく (降りてくる)
            return curtain;
        }

        /// <summary>
        /// プレイヤーが乗る丸い浮き床 (赤いじゅうたん・金の縁と手すり・下に光る輪)。カーテンが閉まっている間に、カーテンといっしょに客席の上へ上がる。
        /// 手すりは当たり判定なし (下のステージを撃つ弾をさえぎらないように)
        /// </summary>
        GameObject BuildLiftPlatform(Transform lift)
        {
            var p = Group(lift, "LiftPlatform");
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            Prim(PrimitiveType.Cylinder, "PlatformFloor", p, new Vector3(0f, -0.13f, 0f), new Vector3(PlatformR * 2f, 0.14f, PlatformR * 2f), Mat("Concert_Carpet", new Color(0.32f, 0.05f, 0.07f)));
            Prim(PrimitiveType.Cylinder, "PlatformBase", p, new Vector3(0f, -0.4f, 0f), new Vector3(PlatformR * 1.6f, 0.14f, PlatformR * 1.6f), Mat("Concert_PianoBlack", new Color(0.03f, 0.03f, 0.035f), null, false, 0.92f), false);
            FlatRing(p, "PlatformEdge", PlatformR, 0.005f, 0.08f, 0.04f, 48, gold);
            FlatRing(p, "PlatformGlow", PlatformR * 0.8f, -0.48f, 0.06f, 0.03f, 40, Mat("Concert_Footlight", new Color(1f, 0.9f, 0.6f), new Color(2f, 1.6f, 0.9f), true));
            // 手すり (柱と上の輪。低い)
            float railR = PlatformR - 0.06f;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Prim(PrimitiveType.Cylinder, "RailPost", p, new Vector3(Mathf.Sin(a) * railR, RailH / 2f, Mathf.Cos(a) * railR), new Vector3(0.035f, RailH / 2f, 0.035f), gold, false);
            }
            FlatRing(p, "RailTop", railR, RailH, 0.05f, 0.05f, 48, gold);
            return p.gameObject;
        }

        // ------------------------------------------------------------------
        // 五線譜と音符
        // ------------------------------------------------------------------

        MusicStaff BuildMusicStaff(Transform root, int index)
        {
            var go = new GameObject($"MusicStaff{index}");
            go.transform.SetParent(root, false);
            var staff = go.AddComponent<MusicStaff>();
            var lineMat = StaffFadeMat(); // 始まりは薄く、終わりほど濃く (MusicStaff が線の頭と終わりの透明度を決める)
            var lines = new List<LineRenderer>();
            for (int i = 0; i < 5; i++) lines.Add(StaffLine(go.transform, $"Line{i}", lineMat, 0.028f));
            var bars = new List<LineRenderer>();
            for (int i = 0; i < 7; i++) bars.Add(StaffLine(go.transform, $"BarLine{i}", lineMat, 0.024f)); // 始まりの縦線 + 流れる小節線 (帯 25m ÷ 間隔 6m より多め)
            staff.lines = lines.ToArray();
            staff.barLines = bars.ToArray();
            staff.lineSpacing = 0.3f; // 太い五線譜 (大きな音符が線と間に上下に分かれて乗る)
            staff.sparkMaterial = Mat("Note_Spark", new Color(1f, 0.9f, 0.5f), new Color(2.4f, 2f, 0.9f), true);

            // 始まりの記号: ト音記号・ヘ音記号 (出すたびにどちらか) と、調号のシャープ・フラット 4個ずつ (MusicStaff が正しい線と間に並べる)
            var markMat = Mat("Clef_Mark", new Color(1f, 0.85f, 0.4f), null, true);
            float s = staff.lineSpacing;
            staff.trebleClef = BuildTrebleClef(go.transform, markMat, s);
            staff.bassClef = BuildBassClef(go.transform, markMat, s);
            var sharps = new List<Transform>();
            var flats = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                sharps.Add(BuildKeySharp(go.transform, markMat, s, i));
                flats.Add(BuildKeyFlat(go.transform, markMat, s, i));
            }
            staff.sharps = sharps.ToArray();
            staff.flats = flats.ToArray();
            // 拍子記号 (出すたびにどれか1つを調号の後ろに)
            staff.timeSignatures = new[]
            {
                BuildTimeSignature(go.transform, markMat, s, "2/4", 2, 4), BuildTimeSignature(go.transform, markMat, s, "3/4", 3, 4),
                BuildTimeSignature(go.transform, markMat, s, "4/4", 4, 4), BuildCommonTime(go.transform, markMat, s, false),
                BuildTimeSignature(go.transform, markMat, s, "3/8", 3, 8), BuildTimeSignature(go.transform, markMat, s, "6/8", 6, 8),
                BuildTimeSignature(go.transform, markMat, s, "2/2", 2, 2), BuildCommonTime(go.transform, markMat, s, true),
            };

            // 流れてくる記号: 15種類 (音符6・休符4・フェルマータ・タイでつないだ音符2・連桁でつないだ音符2) を1つずつと、よく出る4分・8分音符をもう1つずつ。
            // MusicStaff が小節ごとに拍子に合うよう抽選して (MeasureStock) 並べる。足りない種類は MusicStaff が複製して増やす
            var notes = new List<MusicNote>();
            var kinds = new List<int>();
            for (int k = 0; k < NoteKindCount; k++) kinds.Add(k);
            kinds.Add(NoteQuarter);
            kinds.Add(NoteEighth);
            for (int i = 0; i < kinds.Count; i++) notes.Add(BuildNote(go.transform, kinds[i], i, staff.lineSpacing));
            staff.notes = notes.ToArray();
            return staff;
        }

        /// <summary>
        /// 流れてくる音符の色 (蛍光色の7色。出すたびにこの中からランダム)。
        /// ホールの照明や環境光に関係なく、いつも同じ明るさで光って見えるように、光の当たり方を計算しない (Unlit) マテリアルにする。
        /// 丸みは BuildNote の白いハイライト (Shine) で見せる
        /// </summary>
        Material[] NotePalette()
        {
            var colors = new[]
            {
                new Color(1f, 0.1f, 0.4f), new Color(1f, 0.5f, 0f), new Color(0.95f, 1f, 0.1f), new Color(0.2f, 1f, 0.25f),
                new Color(0f, 1f, 0.95f), new Color(0.2f, 0.5f, 1f), new Color(0.9f, 0.2f, 1f),
            };
            var mats = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++) mats[i] = Mat($"Note_Neon{i}", colors[i], null, true);
            return mats;
        }

        /// <summary>
        /// 五線譜の線の半透明のマテリアル。LineRenderer の頂点の色 (startColor / endColor の透明度) が効くように、パーティクル用の Unlit を使う
        /// (見つからなければ普通の半透明。その時は透明度のグラデーションはつかない)
        /// </summary>
        Material StaffFadeMat()
        {
            const string name = "Staff_LineFade";
            var color = new Color(1f, 0.92f, 0.65f, 1f);
            if (mats.TryGetValue(name, out var cached)) return cached;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) return TransparentMat(name, color);
            var mat = new Material(shader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        // ------------------------------------------------------------------
        // 記号の線 (筆で書いたような、太さが変わるなめらかな線)。
        // 点を Catmull-Rom 曲線でなめらかにつなぎ、点ごとの太さで、断面が楕円の管のメッシュにする (同じ名前のメッシュは使い回す)
        // ------------------------------------------------------------------

        /// <summary>
        /// なめらかな線のメッシュ。pts は XY 平面の点 (m)、widths は点ごとの太さ (m。点と同じ数)。depth は奥行きの厚み (細い所は太さに合わせて薄くなる)。
        /// 両端はふさぐ
        /// </summary>
        Mesh GlyphStrokeMesh(string name, Vector2[] pts, float[] widths, float depth)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            const int Ring = 10, PerSeg = 8;
            int n = pts.Length;
            var samples = new List<Vector2>();
            var ws = new List<float>();
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 a = pts[Mathf.Max(i - 1, 0)], b = pts[i], c = pts[i + 1], d = pts[Mathf.Min(i + 2, n - 1)];
                for (int k = 0; k < PerSeg; k++)
                {
                    float t = k / (float)PerSeg;
                    samples.Add(0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (3f * b - a - 3f * c + d) * t * t * t));
                    ws.Add(Mathf.Lerp(widths[i], widths[i + 1], t));
                }
            }
            samples.Add(pts[n - 1]);
            ws.Add(widths[n - 1]);

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            int m = samples.Count;
            for (int j = 0; j < m; j++)
            {
                var tan = (samples[Mathf.Min(j + 1, m - 1)] - samples[Mathf.Max(j - 1, 0)]).normalized;
                var side = new Vector3(-tan.y, tan.x, 0f);
                float half = ws[j] * 0.5f, halfD = Mathf.Min(depth, ws[j]) * 0.5f;
                for (int r = 0; r < Ring; r++)
                {
                    float ang = r * Mathf.PI * 2f / Ring;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    verts.Add((Vector3)samples[j] + side * (cs * half) + Vector3.forward * (sn * halfD));
                    normals.Add((side * cs + Vector3.forward * sn).normalized);
                }
            }
            for (int j = 0; j < m - 1; j++)
                for (int r = 0; r < Ring; r++)
                {
                    int a = j * Ring + r, b = (j + 1) * Ring + r, c = (j + 1) * Ring + (r + 1) % Ring, d = j * Ring + (r + 1) % Ring;
                    tris.AddRange(new[] { a, d, b, b, d, c });
                }
            // 両端のふた
            for (int e = 0; e < 2; e++)
            {
                int ring = e == 0 ? 0 : m - 1;
                var tan = e == 0 ? (samples[0] - samples[1]).normalized : (samples[m - 1] - samples[m - 2]).normalized;
                int center = verts.Count;
                verts.Add(samples[ring]);
                normals.Add(new Vector3(tan.x, tan.y, 0f));
                for (int r = 0; r < Ring; r++)
                {
                    int a = ring * Ring + r, b = ring * Ring + (r + 1) % Ring;
                    if (e == 0) tris.AddRange(new[] { center, b, a });
                    else tris.AddRange(new[] { center, a, b });
                }
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

        /// <summary>
        /// 記号の線を1本置く。p は「線の間隔 s」の単位の点 (原点は記号の基準の高さ)、w は点ごとの太さ (同じく s の単位)。
        /// name は形ごとにちがう名前 (メッシュを使い回すため)
        /// </summary>
        void GlyphStroke(Transform parent, Material mat, string name, float s, float depth, Vector2[] p, float[] w)
        {
            var pts = new Vector2[p.Length];
            var ws = new float[w.Length];
            for (int i = 0; i < p.Length; i++) pts[i] = p[i] * s;
            for (int i = 0; i < w.Length; i++) ws[i] = w[i] * s;
            MeshPart(parent, name, GlyphStrokeMesh($"Glyph_{name}_{s:0.000}", pts, ws, depth), mat);
        }

        // 記号の丸い点 (s の単位の位置と直径。少しつぶした玉)
        void GlyphDot(Transform parent, Material mat, float s, Vector2 p, float d, float depth)
            => Prim(PrimitiveType.Sphere, "Dot", parent, new Vector3(p.x * s, p.y * s, 0f), new Vector3(d * s, d * s, Mathf.Min(depth, d * s)), mat, false);

        static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>
        /// ト音記号 (原点が真ん中の線、s = 線の間隔)。書き始めは下から2本目 (上から4本目) の線 (ソ) の上で、そこから渦を巻いて広がり、
        /// 右側から斜めに上へのぼって上の輪を描き、縦の線で五線の下まで下りて、先が左へ丸まって玉で終わる
        /// </summary>
        Transform BuildTrebleClef(Transform parent, Material mat, float s)
        {
            var t = Group(parent, "TrebleClef");
            const float Depth = 0.05f;
            GlyphStroke(t, mat, "Treble", s, Depth,
                new[]
                {
                    // 渦: ソの線 (上から4本目) の真ん中から上へ出て、左 → 下 → 右と大きく回る (左と下が太い)。渦の中心がソの線に来る
                    V(0.2f, -1f), V(0.15f, -0.4f), V(-0.2f, -0.15f), V(-0.6f, -0.5f), V(-0.78f, -1.1f), V(-0.5f, -1.7f),
                    V(0.05f, -1.95f), V(0.6f, -1.65f), V(0.85f, -1f), V(0.7f, -0.35f), V(0.3f, 0.15f),
                    // 左上へ太くなりながらのぼり、五線の 1.5 間上の先で右へ折り返す
                    V(-0.2f, 0.6f), V(-0.5f, 1.4f), V(-0.45f, 2.3f), V(-0.1f, 3.1f), V(0.25f, 3.5f), V(0.4f, 3.1f),
                    // 細く下りて、のぼってきた線と交わり、少し右寄りの縦の線になって五線の下まで下り、左へ巻く
                    V(0.3f, 2.4f), V(0f, 1.7f), V(0.05f, 0.8f), V(0.15f, -0.4f), V(0.25f, -1.8f), V(0.28f, -2.8f),
                    V(0.1f, -3.35f), V(-0.25f, -3.4f), V(-0.45f, -3.1f),
                },
                new[]
                {
                    0.1f, 0.13f, 0.18f, 0.29f, 0.38f, 0.4f,
                    0.37f, 0.29f, 0.2f, 0.14f, 0.14f,
                    0.2f, 0.29f, 0.29f, 0.2f, 0.13f, 0.11f,
                    0.14f, 0.17f, 0.13f, 0.13f, 0.13f, 0.14f,
                    0.16f, 0.14f, 0.11f,
                });
            GlyphDot(t, mat, s, V(-0.3f, -3f), 0.62f, Depth); // 下の先の玉
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>
        /// ヘ音記号 (原点が真ん中の線)。書き始めは上から2本目の線 (ファ) の上の玉で、そこから右上へ巻いて太くふくらみ、左下へ細くなって下りる。
        /// 右にファの線をはさむ2つの点
        /// </summary>
        Transform BuildBassClef(Transform parent, Material mat, float s)
        {
            var t = Group(parent, "BassClef");
            const float Depth = 0.05f;
            GlyphDot(t, mat, s, V(-0.45f, 1f), 0.76f, Depth); // ファの線の上の大きな玉
            // 玉の上からいちばん上の線まで弧を描いてのぼり、右側でいちばん太くふくらんで、いちばん下の線の近くまで細くなりながら左下へ下りる
            GlyphStroke(t, mat, "Bass", s, Depth,
                new[] { V(-0.55f, 1.25f), V(-0.3f, 1.8f), V(0.2f, 2.05f), V(0.7f, 1.8f), V(0.95f, 1.2f), V(0.88f, 0.45f), V(0.55f, -0.4f), V(0f, -1.15f), V(-0.65f, -1.85f) },
                new[] { 0.13f, 0.16f, 0.24f, 0.36f, 0.44f, 0.42f, 0.31f, 0.18f, 0.05f });
            GlyphDot(t, mat, s, V(1.35f, 1.5f), 0.36f, Depth); // ファの線をはさむ2つの点
            GlyphDot(t, mat, s, V(1.35f, 0.5f), 0.36f, Depth);
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>調号のシャープ (原点がその音の線か間の高さ)。細い縦線2本と、右上がりの太い横棒2本</summary>
        Transform BuildKeySharp(Transform parent, Material mat, float s, int index)
        {
            var t = Group(parent, $"KeySharp{index}");
            const float Depth = 0.04f;
            GlyphStroke(t, mat, "SharpL", s, Depth, new[] { V(-0.22f, -1.35f), V(-0.22f, 1.15f) }, new[] { 0.12f, 0.12f });
            GlyphStroke(t, mat, "SharpR", s, Depth, new[] { V(0.22f, -1.15f), V(0.22f, 1.35f) }, new[] { 0.12f, 0.12f });
            GlyphStroke(t, mat, "SharpTop", s, Depth, new[] { V(-0.52f, 0.17f), V(0.52f, 0.43f) }, new[] { 0.3f, 0.3f });
            GlyphStroke(t, mat, "SharpBottom", s, Depth, new[] { V(-0.52f, -0.43f), V(0.52f, -0.17f) }, new[] { 0.3f, 0.3f });
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>調号のフラット (原点がその音の線か間の高さ)。長い細い縦線と、下の右へふくらんだ涙形の輪</summary>
        Transform BuildKeyFlat(Transform parent, Material mat, float s, int index)
        {
            var t = Group(parent, $"KeyFlat{index}");
            const float Depth = 0.04f;
            GlyphStroke(t, mat, "FlatStem", s, Depth, new[] { V(-0.3f, -0.5f), V(-0.3f, 2.3f) }, new[] { 0.12f, 0.12f });
            GlyphStroke(t, mat, "FlatBowl", s, Depth,
                new[] { V(-0.3f, 0.05f), V(0f, 0.38f), V(0.35f, 0.38f), V(0.38f, 0.05f), V(0.1f, -0.28f), V(-0.3f, -0.5f) },
                new[] { 0.08f, 0.16f, 0.24f, 0.27f, 0.19f, 0.08f });
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>拍子記号 (原点が真ん中の線)。上の数字 (拍の数) は真ん中の線から上の線まで、下の数字 (拍の音符) は下の線から真ん中の線までの高さ</summary>
        Transform BuildTimeSignature(Transform parent, Material mat, float s, string label, int top, int bottom)
        {
            var t = Group(parent, $"Time{label.Replace('/', '_')}");
            TimeDigit(t, mat, s, top, 1f);
            TimeDigit(t, mat, s, bottom, -1f);
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>4分の4拍子の「C」(cut = true なら 2分の2拍子の、縦線の通った C)。原点が真ん中の線</summary>
        Transform BuildCommonTime(Transform parent, Material mat, float s, bool cut)
        {
            var t = Group(parent, cut ? "TimeCut" : "TimeCommon");
            const float Depth = 0.05f;
            // 右の上から左回りに、左側がいちばん太い弧を描いて右の下へ。上の先は玉
            var p = new Vector2[12];
            var w = new float[12];
            for (int i = 0; i < p.Length; i++)
            {
                float a = Mathf.Lerp(50f, 315f, i / (p.Length - 1f)) * Mathf.Deg2Rad;
                p[i] = V(0.05f + Mathf.Cos(a) * 0.6f, Mathf.Sin(a) * 0.95f);
                w[i] = Mathf.Lerp(0.1f, 0.38f, Mathf.Clamp01(-Mathf.Cos(a)));
            }
            GlyphStroke(t, mat, "TimeC", s, Depth, p, w);
            GlyphDot(t, mat, s, p[0] + V(-0.05f, -0.12f), 0.36f, Depth);
            if (cut) GlyphStroke(t, mat, "TimeCutLine", s, Depth, new[] { V(0.02f, -1.5f), V(0.02f, 1.5f) }, new[] { 0.13f, 0.13f });
            t.gameObject.SetActive(false);
            return t;
        }

        // 拍子記号の数字 1 つ (太い印刷の数字)。y = 数字の真ん中の高さ (線の間隔 s の単位)。数字は高さ 2 (線の間 2 つぶん)
        void TimeDigit(Transform parent, Material mat, float s, int digit, float y)
        {
            var t = Group(parent, $"Digit{digit}");
            t.localPosition = new Vector3(0f, y * s, 0f);
            const float Depth = 0.05f;
            void Line(string n, Vector2[] p, float[] w) => GlyphStroke(t, mat, $"Digit{digit}_{n}", s, Depth, p, w);
            // だ円の輪 (縦の左右が太い)。継ぎ目が目立たないよう、少し重ねて閉じる
            void Ring(string n, float cy, float rx, float ry)
            {
                const int N = 18;
                var p = new Vector2[N];
                var w = new float[N];
                for (int i = 0; i < N; i++)
                {
                    float a = (i / 16f) * Mathf.PI * 2f + Mathf.PI * 0.5f;
                    p[i] = V(Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
                    w[i] = Mathf.Lerp(0.1f, 0.3f, Mathf.Abs(Mathf.Cos(a)));
                }
                Line(n, p, w);
            }
            switch (digit)
            {
                case 2: // 左上の玉から右へ丸くふくらみ、左下へ流れて、太い横棒の底
                    GlyphDot(t, mat, s, V(-0.42f, 0.5f), 0.38f, Depth);
                    Line("Curve", new[] { V(-0.5f, 0.5f), V(-0.3f, 0.82f), V(0.1f, 0.92f), V(0.45f, 0.7f), V(0.5f, 0.3f), V(0.25f, -0.1f), V(-0.2f, -0.5f), V(-0.55f, -0.85f) },
                        new[] { 0.14f, 0.16f, 0.18f, 0.3f, 0.36f, 0.32f, 0.22f, 0.16f });
                    Line("Base", new[] { V(-0.58f, -0.84f), V(0.58f, -0.84f) }, new[] { 0.24f, 0.24f });
                    break;
                case 3: // 上と下の、右へふくらんだ2つの弧。左の先は玉
                    GlyphDot(t, mat, s, V(-0.45f, 0.58f), 0.34f, Depth);
                    GlyphDot(t, mat, s, V(-0.5f, -0.55f), 0.36f, Depth);
                    Line("Top", new[] { V(-0.45f, 0.6f), V(-0.15f, 0.9f), V(0.25f, 0.88f), V(0.45f, 0.6f), V(0.35f, 0.25f), V(0f, 0.05f), V(-0.15f, 0.02f) },
                        new[] { 0.14f, 0.16f, 0.22f, 0.32f, 0.3f, 0.18f, 0.12f });
                    Line("Bottom", new[] { V(-0.15f, 0.02f), V(0.25f, -0.05f), V(0.52f, -0.4f), V(0.4f, -0.8f), V(0f, -0.95f), V(-0.4f, -0.82f), V(-0.55f, -0.55f) },
                        new[] { 0.12f, 0.2f, 0.36f, 0.3f, 0.18f, 0.14f, 0.12f });
                    break;
                case 4: // 細い斜めの線と横棒、右の太い縦の棒
                    Line("Diag", new[] { V(0.2f, 0.88f), V(-0.6f, -0.35f) }, new[] { 0.14f, 0.14f });
                    Line("Bar", new[] { V(-0.62f, -0.38f), V(0.62f, -0.38f) }, new[] { 0.2f, 0.2f });
                    Line("Stem", new[] { V(0.25f, 0.88f), V(0.25f, -0.85f) }, new[] { 0.34f, 0.34f });
                    break;
                case 6: // 右上の玉から左へ大きく回って下り、下の輪を巻く
                    GlyphDot(t, mat, s, V(0.4f, 0.62f), 0.34f, Depth);
                    Line("Curve", new[] { V(0.42f, 0.65f), V(0.15f, 0.92f), V(-0.25f, 0.85f), V(-0.52f, 0.4f), V(-0.52f, -0.35f), V(-0.25f, -0.9f), V(0.2f, -0.9f), V(0.5f, -0.5f), V(0.45f, -0.1f), V(0.1f, 0.1f), V(-0.25f, 0f), V(-0.48f, -0.3f) },
                        new[] { 0.12f, 0.16f, 0.22f, 0.36f, 0.38f, 0.24f, 0.18f, 0.3f, 0.3f, 0.16f, 0.14f, 0.14f });
                    break;
                case 8: // 上の小さな輪と下の大きな輪
                    Ring("Top", 0.47f, 0.38f, 0.43f);
                    Ring("Bottom", -0.45f, 0.48f, 0.5f);
                    break;
            }
        }

        LineRenderer StaffLine(Transform parent, string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.widthMultiplier = width;
            lr.sharedMaterial = mat;
            lr.positionCount = 0;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        // 流れてくる記号の種類 (BuildNote の kind)
        const int NoteQuarter = 0, NoteEighth = 1, NoteHalf = 2, NoteWhole = 3, NoteSixteenth = 4, NoteBeamed = 5,
                  RestWhole = 6, RestHalf = 7, RestQuarter = 8, RestEighth = 9, Fermata = 10,
                  NoteTiedQuarters = 11, NoteTiedEighths = 12, // タイでつないだ 4分音符3つ / 連桁とタイでつないだ 8分音符3つ
                  NoteBeamed3 = 13, NoteBeamed16 = 14;         // 連桁 (旗をつなげた線) でつないだ 8分音符3つ / 2本の連桁でつないだ 16分音符4つ
        const int NoteKindCount = 15; // シャープ・フラットは流さず、五線譜の始まりの調号に並べる (MusicStaff)

        // 記号ごとの点数 (形がややこしい物ほど高い)
        static readonly int[] NoteKindPoints = { 100, 150, 200, 250, 200, 250, 150, 150, 200, 200, 300, 300, 350, 300, 400 };
        // 記号ごとの長さ (32分音符が何個分か)。MusicStaff が拍子にぴったり合うように小節ごとに抽選する (MeasureStock)。
        // つないだ音符は全部の合計 (タイの4分音符3つ = 24 など)。
        // 全休符 (拍子ぶんの長さで決まった長さがない) とフェルマータ (音符ではないので拍がない) は 0 にして長さの抽選から外し、別に抽選する (MusicNote.role)
        static readonly int[] NoteKindLength = { 8, 4, 16, 32, 2, 8, 0, 16, 8, 4, 0, 24, 12, 12, 8 };

        /// <summary>
        /// 流れてくる記号の的。kind は NoteQuarter 〜 NoteBeamed16 (音符6種・休符4種・フェルマータ・タイでつないだ音符2種・連桁でつないだ音符2種)。
        /// 原点が記号の真ん中 (音符は玉の真ん中)、表 (-Z) がプレイヤー向き。色を付け替える部品は Body の下にまとめる
        /// </summary>
        MusicNote BuildNote(Transform parent, int kind, int index, float lineSpacing)
        {
            var root = Group(parent, $"Note{index}");
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.MusicNote;
            target.manualControl = true;
            target.shrinkOnHit = true;
            var visual = Group(root, "Visual");
            target.visual = visual;

            // 見た目は Shape にまとめて大きくする (Visual の大きさは、当たった時に縮む動きに使われるので変えない)
            var shape = Group(visual, "Shape");
            const float ShapeScale = 1.9f;
            shape.localScale = Vector3.one * ShapeScale;
            var palette = NotePalette();
            var mat = palette[kind % palette.Length];
            var body = Group(shape, "Body"); // 色を付け替える部品
            var shineMat = Mat("Note_Shine", new Color(1f, 1f, 1f), null, true);
            var holeMat = Mat("Note_HoleDark", new Color(0.05f, 0.04f, 0.08f), null, true);

            // 玉: 厚みのあるつやつやの楕円の玉。光り方は照明に左右されない (Unlit) ので、左上に白いつやを描いて丸みを見せる。hollow は白抜き (2分・全音符)
            void Head(Vector3 p, float w, float tilt, bool hollow)
            {
                var head = Prim(PrimitiveType.Sphere, "Head", body, p, new Vector3(w, 0.145f, 0.15f), mat, false);
                head.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                if (hollow) // 玉の真ん中が抜けて見えるように、暗い楕円を表と裏の両方にはめ込む
                    foreach (float z in new[] { -0.052f, 0.052f })
                    {
                        var hole = Prim(PrimitiveType.Sphere, "Hole", shape, p + new Vector3(0f, 0f, z), new Vector3(w * 0.6f, 0.06f, 0.06f), holeMat, false);
                        hole.transform.localRotation = Quaternion.Euler(0f, 0f, tilt == 22f ? 22f : -40f); // 全音符 (傾けない玉) の穴は斜め
                    }
                var shine = Prim(PrimitiveType.Sphere, "Shine", shape, p + new Vector3(-0.042f * w / 0.2f, 0.03f, -0.066f), new Vector3(0.065f, 0.032f, 0.02f), shineMat, false);
                shine.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
                Prim(PrimitiveType.Sphere, "ShineDot", shape, p + new Vector3(-0.012f, 0.045f, -0.063f), Vector3.one * 0.016f, shineMat, false);
            }
            // 線: 点をなめらかにつなぎ、点ごとの太さ w で描く (GlyphStroke。メッシュは記号の種類ごとに使い回す)
            int strokeNo = 0;
            void StrokeW(Vector2[] p, float[] w) => GlyphStroke(body, mat, $"Note{kind}_{strokeNo++}", 1f, 0.06f, p, w);
            // 同じ太さ d の線 (点が1つなら玉)
            void Stroke(float d, params Vector3[] pts)
            {
                if (pts.Length == 1) { Prim(PrimitiveType.Sphere, "Dot", body, pts[0], Vector3.one * d, mat, false); return; }
                var p = new Vector2[pts.Length];
                var w = new float[pts.Length];
                for (int i = 0; i < pts.Length; i++) { p[i] = pts[i]; w[i] = d; }
                StrokeW(p, w);
            }
            // 棒 (x の所に y0 〜 y1)。ふちに白いつやの線
            void Stem(float x, float y0, float y1)
            {
                Stroke(0.03f, new Vector3(x, y0, 0f), new Vector3(x, y1, 0f));
                Prim(PrimitiveType.Cube, "StemShine", shape, new Vector3(x - 0.007f, (y0 + y1) / 2f + 0.02f, -0.013f), new Vector3(0.007f, (y1 - y0) * 0.65f, 0.006f), shineMat, false);
            }
            // 旗: 棒の先から右へふくらんで、下へしなりながら細くなる
            void Flag(float x, float top) => StrokeW(
                new[] { V(x, top), V(x + 0.035f, top - 0.05f), V(x + 0.08f, top - 0.11f), V(x + 0.075f, top - 0.17f), V(x + 0.04f, top - 0.23f) },
                new[] { 0.05f, 0.055f, 0.045f, 0.03f, 0.016f });
            // つないだ音符: count 個の音符を同じ高さに横に並べる (五線と平行)。棒の先を beams 本の水平な太い連桁で (8分 = 1本、16分 = 2本)、tie なら玉の下を水平な弧 (タイ) でつなぐ
            const float GroupStep = 0.26f; // 音符の間隔 (Shape の単位)
            int groupCount = 1;
            void NoteGroup(int count, int beams, bool tie)
            {
                groupCount = count;
                float x0 = -(count - 1) * GroupStep * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    float x = x0 + i * GroupStep;
                    Head(new Vector3(x, 0f, 0f), 0.2f, 22f, false);
                    Stem(x + 0.088f, 0f, 0.4f);
                }
                for (int b = 0; b < beams; b++)
                {
                    float y = 0.39f - b * 0.11f;
                    Stroke(0.06f, new Vector3(x0 + 0.088f, y, 0f), new Vector3(x0 + (count - 1) * GroupStep + 0.088f, y, 0f));
                }
                if (tie)
                    for (int i = 0; i < count - 1; i++)
                    {
                        float a = x0 + i * GroupStep + 0.06f, b = x0 + (i + 1) * GroupStep - 0.06f;
                        StrokeW(new[] { V(a, -0.09f), V((a + b) * 0.5f, -0.135f), V(b, -0.09f) }, new[] { 0.012f, 0.032f, 0.012f });
                    }
            }
            // 板 (休符の四角や五線の線の短い切れはし)
            void Block(Vector3 c, Vector3 s) => Prim(PrimitiveType.Cube, "Block", body, c, s, mat, false);

            float labelY = -0.17f;
            // 五線の決まった高さに置く記号 (休符・フェルマータ)。高さは真ん中の線から上へ (m)。記号の形の寸法 (Shape の単位) × ShapeScale から決める
            bool fixedH = false;
            float fixedY = 0f;
            int fixedSlot = 4;
            switch (kind)
            {
                case NoteQuarter: Head(Vector3.zero, 0.2f, 22f, false); Stem(0.088f, 0f, 0.4f); break;
                case NoteEighth: Head(Vector3.zero, 0.2f, 22f, false); Stem(0.088f, 0f, 0.4f); Flag(0.088f, 0.4f); break;
                case NoteHalf: Head(Vector3.zero, 0.2f, 22f, true); Stem(0.088f, 0f, 0.4f); break;
                case NoteWhole: Head(Vector3.zero, 0.27f, 0f, true); break; // 棒のない、横に広い白抜きの玉
                case NoteSixteenth: Head(Vector3.zero, 0.2f, 22f, false); Stem(0.088f, 0f, 0.46f); Flag(0.088f, 0.46f); Flag(0.088f, 0.34f); break;
                case NoteBeamed: // 8分音符2つを太い連桁でつなぐ (五線と平行)
                    NoteGroup(2, beams: 1, tie: false);
                    break;
                case NoteTiedQuarters: // 4分音符3つをタイでつなぐ
                    NoteGroup(3, beams: 0, tie: true);
                    labelY = -0.24f;
                    break;
                case NoteTiedEighths: // 8分音符3つを連桁でつなぎ、タイでも結ぶ
                    NoteGroup(3, beams: 1, tie: true);
                    labelY = -0.24f;
                    break;
                case NoteBeamed3: // 8分音符3つを連桁でつなぐ
                    NoteGroup(3, beams: 1, tie: false);
                    break;
                case NoteBeamed16: // 16分音符4つを2本の連桁でつなぐ
                    NoteGroup(4, beams: 2, tie: false);
                    break;
                case RestWhole: // 全休符: 線の下にぶら下がる四角
                    Block(new Vector3(0f, 0.09f, 0f), new Vector3(0.36f, 0.025f, 0.05f));
                    Block(new Vector3(0f, 0.035f, 0f), new Vector3(0.2f, 0.09f, 0.1f));
                    fixedH = true; fixedY = lineSpacing - 0.09f * ShapeScale; // 線 (上の板) が上から2本目の線に重なり、四角がその下にぶら下がる (これが正しい位置)
                    break;
                case RestHalf: // 2分休符: 線の上に乗る四角
                    Block(new Vector3(0f, -0.04f, 0f), new Vector3(0.36f, 0.025f, 0.05f));
                    Block(new Vector3(0f, 0.015f, 0f), new Vector3(0.2f, 0.09f, 0.1f));
                    fixedH = true; fixedY = 0.04f * ShapeScale; // 線 (下の板) が真ん中の線に重なり、四角がその上に乗る
                    break;
                case RestQuarter: // 4分休符: 細い斜めの線 → 太い斜めの線 → 折り返して太い斜めの線 → 下の小さな「c」の鉤
                    StrokeW(new[] { V(-0.035f, 0.25f), V(0.045f, 0.15f), V(0f, 0.08f), V(-0.02f, 0.04f), V(0.055f, -0.06f), V(0f, -0.08f), V(-0.04f, -0.12f), V(-0.01f, -0.18f), V(0.035f, -0.21f) },
                            new[] { 0.02f, 0.06f, 0.03f, 0.03f, 0.065f, 0.03f, 0.03f, 0.035f, 0.015f });
                    labelY = -0.28f;
                    fixedH = true; fixedY = -0.02f * ShapeScale; // 形の真ん中を五線の真ん中に
                    break;
                case RestEighth: // 8分休符: 左上の玉から右へ巻き上がり、そこから左下へまっすぐ下りる線
                    Stroke(0.085f, new Vector3(-0.055f, 0.13f, 0f));
                    StrokeW(new[] { V(-0.05f, 0.09f), V(0f, 0.08f), V(0.05f, 0.105f), V(0.075f, 0.145f) }, new[] { 0.03f, 0.03f, 0.03f, 0.025f });
                    StrokeW(new[] { V(0.075f, 0.145f), V(-0.005f, -0.17f) }, new[] { 0.025f, 0.035f });
                    labelY = -0.24f;
                    fixedH = true; fixedY = 0f; // 形の真ん中を五線の真ん中に
                    break;
                case Fermata: // フェルマータ: 両端が細く、真ん中ほど太い半円の弧と、その下の点
                {
                    var arc = new Vector2[9];
                    var aw = new float[9];
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i / 8f * Mathf.PI;
                        arc[i] = V(Mathf.Cos(a) * 0.2f, Mathf.Sin(a) * 0.17f - 0.03f);
                        aw[i] = Mathf.Lerp(0.015f, 0.065f, Mathf.Sin(a));
                    }
                    StrokeW(arc, aw);
                    Stroke(0.08f, new Vector3(0f, 0.01f, 0f));
                    labelY = -0.12f;
                    fixedH = true; fixedY = 2f * lineSpacing + 0.2f + 0.03f * ShapeScale; fixedSlot = 8; // 五線のいちばん上の線から少し (0.2m) 離れた上に、点の下が来る
                    break;
                }
            }
            if (kind >= RestWhole && kind <= Fermata) // 線だけの記号にも、左上に小さなつやを置く
                Prim(PrimitiveType.Sphere, "ShineDot", shape, new Vector3(-0.06f, 0.12f, -0.06f), Vector3.one * 0.018f, shineMat, false);
            var colored = new List<Renderer>(body.GetComponentsInChildren<Renderer>());

            // 金色の音符の光の輪
            var gold = Group(shape, "GoldMark");
            gold.localPosition = new Vector3(0.03f, 0.1f, 0.03f);
            var spin = gold.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.forward;
            spin.speed = -160f;
            RingSegments(gold, "GoldRing", 0.27f, 16, 0.025f, Mat("Note_Gold", new Color(1f, 0.82f, 0.25f), new Color(1.8f, 1.3f, 0.3f)));
            var label = MakeText(shape, "Points", "100", new Vector3(0f, labelY, -0.06f), 0.09f, Color.white);

            // 当たり判定: 見た目より大きめ (流れてくる記号を当てやすく)。つないだ音符は横長の箱で全体を包む
            float groupWidth = (groupCount - 1) * GroupStep;
            var hit = groupCount > 1
                ? Prim(PrimitiveType.Cube, "Hit", shape, new Vector3(0.04f, 0.12f, 0f), new Vector3(groupWidth + 0.5f, 0.65f, 0.4f), null)
                : Prim(PrimitiveType.Sphere, "Hit", shape, new Vector3(0.04f, 0.06f, 0f), Vector3.one * 0.6f, null);
            HitOnly(hit);

            var note = root.gameObject.AddComponent<MusicNote>();
            note.hasFixedHeight = fixedH;
            note.fixedHeight = fixedY;
            note.fixedSlot = fixedSlot;
            note.extraHalfWidth = groupWidth * ShapeScale * 0.5f; // 横に長いぶん、前後の音符との間をあける (MusicStaff)
            note.target = target;
            note.basePoints = NoteKindPoints[Mathf.Clamp(kind, 0, NoteKindPoints.Length - 1)];
            note.kind = kind;
            note.length32 = NoteKindLength[Mathf.Clamp(kind, 0, NoteKindLength.Length - 1)];
            note.role = kind == RestWhole ? MusicNote.MeasureRole.WholeRest : kind == Fermata ? MusicNote.MeasureRole.Fermata : MusicNote.MeasureRole.Timed;
            note.goldMark = gold.gameObject;
            note.label = label;
            note.bodyRenderers = colored.ToArray();
            note.palette = palette;
            root.gameObject.SetActive(false);
            return note;
        }

        // ------------------------------------------------------------------
        // 四分音符の的 (普通の的が化ける)
        // ------------------------------------------------------------------

        /// <summary>音楽の的: 大きな八分音符とコントラバスとトランペット (LookArt の絵のステッカー。はみ出して大胆に)</summary>
        GameObject MusicLook(Transform look) => StickerLook(look, "Music", "MUSIC", new Color(1f, 0.88f, 0.4f));
    }
}
