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
        const float HallShiftZ = 5f;       // ホールを奥へずらす量 (プレイヤーが客席の後ろの方になる)
        const float ConcertLiftH = 2.6f;   // プレイヤーを浮き床ごと持ち上げる高さ (ステージ全体が見渡せるように)
        const float PlatformR = 2.4f;      // 浮き床の半径

        static readonly float[] RiserR = { 3.7f, 5f, 6.4f, 8f };  // ひな壇の段の内側の半径 (指揮台から)
        const float RiserStep = 0.5f;   // ひな壇の1段の高さ (奥の楽器も手前の楽器に隠れず、狙いやすいように高め)
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
            concert.followSpots = lights.follow;
            concert.followBeams = lights.beams;
            concert.showWhilePlaying = lights.showWhilePlaying;
            concert.instruments = instruments.ToArray();
            concert.baton = baton;
            concert.staffs = staffs.ToArray();
            concert.lift = lift;
            concert.platform = platform;
            concert.liftHeight = ConcertLiftH;
            concert.staffStartZ = HallFront + HallShiftZ + 0.8f;

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
            var wall = Mat("Concert_Wall", new Color(0.36f, 0.2f, 0.12f), null, false, 0.35f);
            var pilaster = Mat("Concert_Pilaster", new Color(0.24f, 0.13f, 0.08f), null, false, 0.45f);
            var ceiling = Mat("Concert_Ceiling", new Color(0.09f, 0.07f, 0.07f));
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
                    Prim(PrimitiveType.Cube, "Sconce", hall, new Vector3(s * (HallHalfW - 0.35f), 3.6f, z + 2f), new Vector3(0.12f, 0.3f, 0.15f), sconce, false);
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
            // 楽器の形が分かるように、ステージの上から左右2つの明かりで全体を照らす
            foreach (float s in new[] { -1f, 1f })
                stage.Add(HallPoint(hall, "StageFill", new Vector3(s * 4.5f, 7.4f, 10.5f), new Color(1f, 0.92f, 0.8f), 16f, 16f));
            stage.Add(HallPoint(hall, "StageFront", new Vector3(0f, 5f, HallFront + 1.5f), new Color(1f, 0.93f, 0.82f), 12f, 13f)); // 手前の楽器を正面から
            var house = HallPoint(hall, "HouseLight", new Vector3(0f, HallH - 3f, -5f), new Color(1f, 0.85f, 0.7f), 1.6f, 24f);
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
        // ステージ (床・奥の壁・パイプオルガン・ひな壇)
        // ------------------------------------------------------------------

        void BuildStage(Transform hall)
        {
            var floor = Mat("Concert_StageFloor", new Color(0.5f, 0.3f, 0.15f), new Color(0.06f, 0.035f, 0.018f), false, 0.6f);
            var front = Mat("Concert_StageFront", new Color(0.18f, 0.1f, 0.06f), null, false, 0.4f);
            var shell = Mat("Concert_Shell", new Color(0.55f, 0.38f, 0.22f), new Color(0.07f, 0.045f, 0.025f), false, 0.4f);
            var shellDark = Mat("Concert_ShellDark", new Color(0.4f, 0.26f, 0.15f), null, false, 0.4f);
            var riser = Mat("Concert_Riser", new Color(0.36f, 0.21f, 0.11f), new Color(0.045f, 0.026f, 0.014f), false, 0.5f);
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
            // 手すり (柱と上の輪)
            float railR = PlatformR - 0.06f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                Prim(PrimitiveType.Cylinder, "RailPost", p, new Vector3(Mathf.Sin(a) * railR, 0.45f, Mathf.Cos(a) * railR), new Vector3(0.035f, 0.45f, 0.035f), gold, false);
            }
            FlatRing(p, "RailTop", railR, 0.9f, 0.05f, 0.05f, 48, gold);
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
            var lineMat = Mat("Staff_Line", new Color(1f, 0.92f, 0.65f), new Color(1.2f, 1f, 0.6f), true);
            var lines = new List<LineRenderer>();
            for (int i = 0; i < 5; i++) lines.Add(StaffLine(go.transform, $"Line{i}", lineMat, 0.028f));
            var bars = new List<LineRenderer>();
            for (int i = 0; i < 5; i++) bars.Add(StaffLine(go.transform, $"BarLine{i}", lineMat, 0.024f));
            staff.lines = lines.ToArray();
            staff.barLines = bars.ToArray();
            staff.lineSpacing = 0.3f; // 太い五線譜 (大きな音符が線と間に上下に分かれて乗る)
            staff.sparkMaterial = Mat("Note_Spark", new Color(1f, 0.9f, 0.5f), new Color(2.4f, 2f, 0.9f), true);

            // 先頭の飾り: 光る玉と、それを巻く輪 (ト音記号の代わり)
            var clef = Group(go.transform, "Clef");
            Prim(PrimitiveType.Sphere, "ClefOrb", clef, Vector3.zero, Vector3.one * 0.16f, Mat("Clef_Orb", new Color(1f, 0.85f, 0.4f), new Color(1.6f, 1.2f, 0.4f), true), false);
            var spin = Group(clef, "ClefSpin");
            var rot = spin.gameObject.AddComponent<Rotator>();
            rot.axis = Vector3.forward;
            rot.speed = 120f;
            RingSegments(spin, "ClefRing", 0.2f, 14, 0.02f, lineMat);
            staff.clef = clef;

            // 音符: 4分 (100) / 8分 (150) / 2分 (200)。それぞれ何個か
            var notes = new List<MusicNote>();
            var kinds = new[] { 0, 1, 0, 2, 1, 0, 1, 2, 0, 1 };
            for (int i = 0; i < kinds.Length; i++) notes.Add(BuildNote(go.transform, kinds[i], i));
            staff.notes = notes.ToArray();
            return staff;
        }

        /// <summary>
        /// 流れてくる音符の色 (蛍光色の7色。出すたびにこの中からランダム)。
        /// 照明に関係なくいつも同じ明るさで見えるように、光の当たり方を計算しない (Unlit) マテリアルにする
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

        /// <summary>音符の的。kind: 0 = 4分音符 / 1 = 8分音符 (旗つき) / 2 = 2分音符 (白い玉)。原点が玉の真ん中、表 (-Z) がプレイヤー向き</summary>
        MusicNote BuildNote(Transform parent, int kind, int index)
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
            shape.localScale = Vector3.one * 1.9f;
            var palette = NotePalette();
            var mat = palette[kind % palette.Length];
            var colored = new List<Renderer>();
            var head = Prim(PrimitiveType.Sphere, "Head", shape, Vector3.zero, new Vector3(0.2f, 0.14f, 0.08f), mat, false);
            colored.Add(head.GetComponent<Renderer>());
            head.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            if (kind == 2) // 2分音符: 白い玉の真ん中が抜けて見えるように、暗い楕円を重ねる
            {
                var hole = Prim(PrimitiveType.Sphere, "Hole", shape, new Vector3(0f, 0f, -0.012f), new Vector3(0.12f, 0.06f, 0.06f), Mat("Note_HoleDark", new Color(0.05f, 0.04f, 0.08f), null, true), false);
                hole.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            }
            colored.Add(Prim(PrimitiveType.Cube, "Stem", shape, new Vector3(0.088f, 0.2f, 0f), new Vector3(0.022f, 0.4f, 0.022f), mat, false).GetComponent<Renderer>());
            if (kind == 1)
            {
                var flag = Prim(PrimitiveType.Cube, "Flag", shape, new Vector3(0.14f, 0.3f, 0f), new Vector3(0.025f, 0.2f, 0.02f), mat, false);
                colored.Add(flag.GetComponent<Renderer>());
                flag.transform.localRotation = Quaternion.Euler(0f, 0f, 40f);
            }
            // 金色の音符の光の輪
            var gold = Group(shape, "GoldMark");
            gold.localPosition = new Vector3(0.03f, 0.1f, 0.03f);
            var spin = gold.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.forward;
            spin.speed = -160f;
            RingSegments(gold, "GoldRing", 0.27f, 16, 0.025f, Mat("Note_Gold", new Color(1f, 0.82f, 0.25f), new Color(1.8f, 1.3f, 0.3f)));
            var label = MakeText(shape, "Points", "100", new Vector3(0f, -0.17f, -0.02f), 0.09f, Color.white);

            var hit = Prim(PrimitiveType.Sphere, "Hit", shape, new Vector3(0.04f, 0.1f, 0f), Vector3.one * 0.55f, null); // 見た目より大きめ (流れてくる音符を当てやすく)
            HitOnly(hit);

            var note = root.gameObject.AddComponent<MusicNote>();
            note.target = target;
            note.basePoints = kind == 0 ? 100 : kind == 1 ? 150 : 200;
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

        GameObject MusicLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Music_Plate", new Color(0.35f, 0.06f, 0.12f), new Color(0.08f, 0.01f, 0.03f)));
            var note = Group(look, "QuarterNote");
            note.localPosition = c + new Vector3(-0.02f, -0.02f, -0.04f);
            var bob = note.gameObject.AddComponent<Oscillator>(); // 拍に乗るように、ぴょこぴょこ跳ねる
            bob.offset = new Vector3(0f, 0.015f, 0f);
            bob.speed = 7f;
            var gold = Mat("Note_Gold", new Color(1f, 0.82f, 0.25f), new Color(1.8f, 1.3f, 0.3f));
            var head = Prim(PrimitiveType.Sphere, "Head", note, new Vector3(0f, -0.05f, 0f), new Vector3(0.1f, 0.07f, 0.04f), gold, false);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            Prim(PrimitiveType.Cube, "Stem", note, new Vector3(0.045f, 0.035f, 0f), new Vector3(0.013f, 0.17f, 0.013f), gold, false);
            for (int k = 0; k < 2; k++)
                Prim(PrimitiveType.Sphere, "Sparkle", look, c + new Vector3(0.09f - k * 0.17f, 0.09f - k * 0.03f, -0.04f), Vector3.one * 0.025f, Mat("Clef_Orb", new Color(1f, 0.85f, 0.4f), new Color(1.6f, 1.2f, 0.4f), true), false);
            MakeText(look, "Label", "MUSIC", c + new Vector3(0f, -0.115f, -0.025f), 0.04f, new Color(1f, 0.85f, 0.3f));
            return look.gameObject;
        }
    }
}
