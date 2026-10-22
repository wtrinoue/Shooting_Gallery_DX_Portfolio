using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 怪獣モード (怪獣の的を撃つと始まる) の生成: 遠くまで続く街 (道路の格子・ビル・公園の木・商店街)、色のついた的のビル、
    /// 流れる雲と太陽、怪獣の的の見た目。プレイヤーは真ん中の公園に立つ巨大な怪獣の大きさ (ビルはひざから胸くらい)。
    /// </summary>
    public partial class GalleryBuilder
    {
        const float CityBlock = 6f;      // 道路の間隔 (1区画の大きさ)
        const float CityRoad = 1.2f;     // 道路の幅
        const int CityCells = 8;         // 中心から片側に並ぶ区画の数 (8 × 6m = 48m 先まで街が続く。街は最後に 0.6 倍になるので、約 29m 先まで)
        const float CityY = 0.03f;       // 街の地面の上面の高さ
        const float CityHeight = 0.7f;   // ビルの高さの倍率 (小さいほどプレイヤーが大きく感じる)

        KaijuCity BuildKaijuCity()
        {
            var go = new GameObject("KaijuCity");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;

            var asphalt = Mat("City_Ground", new Color(0.44f, 0.44f, 0.45f));
            var road = Mat("City_Road", new Color(0.18f, 0.18f, 0.2f));
            var line = Mat("City_RoadLine", new Color(0.62f, 0.62f, 0.6f));
            var grass = Mat("City_Grass", new Color(0.42f, 0.47f, 0.41f));
            float extent = CityCells * CityBlock;

            // ---- 地面・道路・真ん中の公園 (伸びてこない。街といっしょに表示される) ----
            var ground = Group(root, "CityGround");
            Prim(PrimitiveType.Cube, "Ground", ground, new Vector3(0f, CityY / 2f, 0f), new Vector3(extent * 2.6f, CityY, extent * 2.6f), asphalt, false);
            for (int k = -CityCells; k <= CityCells; k++)
            {
                float p = k * CityBlock;
                Prim(PrimitiveType.Cube, "RoadX", ground, new Vector3(0f, CityY + 0.003f, p), new Vector3(extent * 2f, 0.006f, CityRoad), road, false);
                Prim(PrimitiveType.Cube, "RoadZ", ground, new Vector3(p, CityY + 0.004f, 0f), new Vector3(CityRoad, 0.006f, extent * 2f), road, false);
                if (k % 2 == 0) // 大通りの白線
                {
                    Prim(PrimitiveType.Cube, "LineX", ground, new Vector3(0f, CityY + 0.008f, p), new Vector3(extent * 2f, 0.004f, 0.05f), line, false);
                    Prim(PrimitiveType.Cube, "LineZ", ground, new Vector3(p, CityY + 0.009f, 0f), new Vector3(0.05f, 0.004f, extent * 2f), line, false);
                }
            }
            Prim(PrimitiveType.Cylinder, "CenterPark", ground, new Vector3(0f, CityY + 0.012f, 0f), new Vector3(10f, 0.006f, 10f), grass, false);

            // ---- 的のビルを置く区画を先に決める (いろいろな方向・距離に散らす) ----
            var targetCells = new Dictionary<(int, int), int>();
            int targetCount = 20;
            for (int i = 0; i < targetCount; i++)
            {
                float a = (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / targetCount;
                float d = Mathf.Lerp(7f, extent * 0.85f, (i % 4) / 3f) + Random.Range(-2f, 2f);
                int cx = Mathf.Clamp(Mathf.FloorToInt(Mathf.Sin(a) * d / CityBlock), -CityCells, CityCells - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt(Mathf.Cos(a) * d / CityBlock), -CityCells, CityCells - 1);
                if (IsCenterCell(cx, cz)) cz += cz >= 0 ? 1 : -1;
                if (!targetCells.ContainsKey((cx, cz))) targetCells[(cx, cz)] = targetCells.Count;
            }

            // ---- 区画ごとに建物を建てる ----
            var lots = new List<Transform>();
            var targets = new List<KaijuBuilding>();
            var explosion = TransparentMat("Fire_Explosion", new Color(1f, 0.45f, 0.08f, 0.85f));
            var dust = TransparentMat("Dust", new Color(0.6f, 0.55f, 0.48f, 0.6f));
            for (int cx = -CityCells; cx < CityCells; cx++)
                for (int cz = -CityCells; cz < CityCells; cz++)
                {
                    if (IsCenterCell(cx, cz)) { CityTrees(root, lots, new Vector3((cx + 0.5f) * CityBlock, 0f, (cz + 0.5f) * CityBlock), 3); continue; }
                    var center = new Vector3((cx + 0.5f) * CityBlock, 0f, (cz + 0.5f) * CityBlock);
                    float dist = new Vector2(center.x, center.z).magnitude;
                    if (targetCells.TryGetValue((cx, cz), out int ti))
                    {
                        targets.Add(KaijuTargetBuilding(root, lots, center, dist, ti, ti == targetCells.Count - 1, explosion, dust));
                        continue;
                    }
                    float r = Random.value;
                    if (r < 0.14f) CityPark(root, lots, center);
                    else if (r < 0.32f) ShoppingStreet(root, lots, center, dist);
                    else CityBuildings(root, lots, center, dist);
                }

            // ---- 動く的: 道路を走る車・街の上を横切る飛行機・まわりを回るヘリコプター ----
            var vehicles = new List<KaijuVehicle>();
            for (int i = 0; i < 12; i++) vehicles.Add(KaijuCar(root, i, explosion, dust));
            for (int i = 0; i < 4; i++) vehicles.Add(KaijuPlane(root, i, explosion, dust));
            for (int i = 0; i < 4; i++) vehicles.Add(KaijuHelicopter(root, i, explosion, dust));

            // ---- 空: 流れる雲と太陽 ----
            var cloud = Mat("Cloud", new Color(0.96f, 0.97f, 1f), new Color(0.32f, 0.34f, 0.38f));
            var cloudBelts = new List<ConveyorBelt>();
            var show = new List<GameObject> { ground.gameObject };
            for (int b = 0; b < 2; b++)
            {
                var belt = Group(root, $"Clouds_{b}");
                var items = new List<Transform>();
                for (int i = 0; i < 12; i++)
                    items.Add(CloudPuff(belt, "Cloud", new Vector3(-70f + i * 140f / 12f + Random.Range(-3f, 3f), Random.Range(14f, 22f) + b * 4f, Random.Range(-60f, 60f)), Random.Range(3f, 6f), cloud));
                var conveyor = belt.gameObject.AddComponent<ConveyorBelt>();
                conveyor.items = items.ToArray();
                conveyor.length = 140f;
                conveyor.speed = 0f;
                cloudBelts.Add(conveyor);
                show.Add(belt.gameObject);
            }
            var sun = Group(root, "Sun");
            sun.localPosition = Quaternion.Euler(-32f, -40f, 0f) * Vector3.forward * 85f;
            Prim(PrimitiveType.Sphere, "SunDisc", sun, Vector3.zero, Vector3.one * 9f, Mat("Sun", new Color(1f, 0.95f, 0.65f), null, true), false);
            Prim(PrimitiveType.Sphere, "SunGlow", sun, Vector3.zero, Vector3.one * 14f, TransparentMat("Sun_Glow", new Color(1f, 0.92f, 0.6f, 0.25f)), false);
            show.Add(sun.gameObject);

            var city = go.AddComponent<KaijuCity>();
            city.lots = lots.ToArray();
            city.targets = targets.ToArray();
            city.vehicles = vehicles.ToArray();
            city.clouds = cloudBelts.ToArray();
            city.showDuringStage = show.ToArray();
            var groundParts = new List<Transform>();
            foreach (var name in new[] { "Ground", "PitRim", "BonusStage" })
            {
                var t = world.Find(name);
                if (t != null) groundParts.Add(t);
            }
            if (playerMark != null) groundParts.Add(playerMark.transform);
            city.groundParts = groundParts.ToArray();
            // 街は怪獣モードの時だけ出る (シーンに生成した時も隠しておく)
            foreach (var l in lots) l.gameObject.SetActive(false);
            foreach (var s in show) s.SetActive(false);
            go.SetActive(true);
            return city;
        }

        static bool IsCenterCell(int cx, int cz) => (cx == 0 || cx == -1) && (cz == 0 || cz == -1);

        // 地面から伸びてくる区画 (根元が地面)
        Transform CityLot(Transform parent, List<Transform> lots, Vector3 pos)
        {
            var lot = Group(parent, "Lot");
            lot.localPosition = new Vector3(pos.x, CityY, pos.z);
            lots.Add(lot);
            return lot;
        }

        /// <summary>普通のビルが1〜4棟 (灰色・ベージュ・白)。遠いほど背が高い。近いビルには階の筋を入れる</summary>
        void CityBuildings(Transform parent, List<Transform> lots, Vector3 center, float dist)
        {
            var colors = new[]
            {
                Mat("City_Gray1", new Color(0.62f, 0.63f, 0.66f)), Mat("City_Gray2", new Color(0.5f, 0.52f, 0.56f)),
                Mat("City_Gray3", new Color(0.72f, 0.72f, 0.72f)), Mat("City_Gray4", new Color(0.56f, 0.56f, 0.57f)),
                Mat("City_Gray5", new Color(0.67f, 0.67f, 0.68f))
            };
            var window = Mat("City_Window", new Color(0.36f, 0.37f, 0.39f), null, false, 0.6f);
            var roof = Mat("City_Roof", new Color(0.35f, 0.36f, 0.38f));
            float inner = CityBlock - CityRoad - 0.4f;
            int n = Random.Range(1, 5);
            for (int i = 0; i < n; i++)
            {
                // 区画の中を田の字に分けて、1つずつ置く
                float qx = n == 1 ? 0f : (i % 2 == 0 ? -1f : 1f) * inner / 4f;
                float qz = n <= 2 ? 0f : (i < 2 ? -1f : 1f) * inner / 4f;
                float w = n == 1 ? Random.Range(1.6f, inner * 0.8f) : Random.Range(0.9f, inner / 2f - 0.2f);
                float d = n <= 2 ? Random.Range(1.4f, inner * 0.8f) : Random.Range(0.9f, inner / 2f - 0.2f);
                float tall = Mathf.Lerp(0.8f, 2.4f, Random.value) * Mathf.Lerp(1f, 1.9f, Mathf.Clamp01(dist / 45f)) * CityHeight;
                if (Random.value < 0.1f) tall *= 1.6f; // たまに高層ビル
                var lot = CityLot(parent, lots, center + new Vector3(qx, 0f, qz));
                Prim(PrimitiveType.Cube, "Building", lot, new Vector3(0f, tall / 2f, 0f), new Vector3(w, tall, d), colors[Random.Range(0, colors.Length)]);
                Prim(PrimitiveType.Cube, "Roof", lot, new Vector3(0f, tall + 0.02f, 0f), new Vector3(w * 0.9f, 0.04f, d * 0.9f), roof, false);
                if (dist < 16f)
                    for (float y = 0.35f; y < tall - 0.2f; y += 0.4f)
                        Prim(PrimitiveType.Cube, "Windows", lot, new Vector3(0f, y, 0f), new Vector3(w + 0.01f, 0.1f, d + 0.01f), window, false);
            }
        }

        /// <summary>商店街: 道ぞいに小さなお店がずらりと並ぶ (色とりどりの日よけと看板)</summary>
        void ShoppingStreet(Transform parent, List<Transform> lots, Vector3 center, float dist)
        {
            var awnings = new[]
            {
                Mat("Shop_Gray1", new Color(0.45f, 0.45f, 0.47f)), Mat("Shop_Gray2", new Color(0.55f, 0.55f, 0.56f)),
                Mat("Shop_Gray3", new Color(0.38f, 0.38f, 0.4f)), Mat("Shop_Gray4", new Color(0.62f, 0.62f, 0.63f)),
                Mat("Shop_Gray5", new Color(0.5f, 0.5f, 0.52f))
            };
            var wall = Mat("Shop_Wall", new Color(0.7f, 0.7f, 0.7f));
            var glass = Mat("Shop_Glass", new Color(0.4f, 0.42f, 0.45f), null, false, 0.8f);
            float inner = CityBlock - CityRoad - 0.4f;
            int shops = 5;
            float sw = inner / shops;
            // 区画の片側 (プレイヤーに近い側の道ぞい) に並べ、お店の表を道 (外側) へ向ける
            bool alongX = Mathf.Abs(center.z) > Mathf.Abs(center.x);
            float edge = (alongX ? -Mathf.Sign(center.z) : -Mathf.Sign(center.x)) * (inner / 2f - 0.45f);
            for (int i = 0; i < shops; i++)
            {
                float along = -inner / 2f + sw * (i + 0.5f);
                var pos = center + (alongX ? new Vector3(along, 0f, edge) : new Vector3(edge, 0f, along));
                var lot = CityLot(parent, lots, pos);
                lot.localRotation = Quaternion.LookRotation(alongX ? new Vector3(0f, 0f, -Mathf.Sign(edge)) : new Vector3(-Mathf.Sign(edge), 0f, 0f)); // 表 (-Z) が道へ
                float h = Random.Range(0.45f, 0.75f);
                Prim(PrimitiveType.Cube, "Shop", lot, new Vector3(0f, h / 2f, 0f), new Vector3(sw - 0.06f, h, 0.8f), wall);
                Prim(PrimitiveType.Cube, "Window", lot, new Vector3(0f, h * 0.35f, -0.405f), new Vector3(sw * 0.7f, h * 0.4f, 0.01f), glass, false);
                var awning = Prim(PrimitiveType.Cube, "Awning", lot, new Vector3(0f, h * 0.68f, -0.5f), new Vector3(sw - 0.04f, 0.03f, 0.25f), awnings[i % awnings.Length], false);
                awning.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                Prim(PrimitiveType.Cube, "Sign", lot, new Vector3(0f, h + 0.08f, -0.35f), new Vector3(sw * 0.8f, 0.14f, 0.03f), awnings[(i + 2) % awnings.Length], false);
            }
            // 残りの所には小さめのビル
            CityBuildingSmall(parent, lots, center - (alongX ? new Vector3(0f, 0f, edge) : new Vector3(edge, 0f, 0f)) * 0.6f, dist);
        }

        void CityBuildingSmall(Transform parent, List<Transform> lots, Vector3 pos, float dist)
        {
            var lot = CityLot(parent, lots, pos);
            float tall = Random.Range(0.8f, 1.6f) * CityHeight;
            Prim(PrimitiveType.Cube, "Building", lot, new Vector3(0f, tall / 2f, 0f), new Vector3(2f, tall, 1.6f), Mat("City_Gray1", new Color(0.62f, 0.63f, 0.66f)));
        }

        /// <summary>公園: 芝生と木々 (当たり判定なし)</summary>
        void CityPark(Transform parent, List<Transform> lots, Vector3 center)
        {
            var lot = CityLot(parent, lots, center);
            float inner = CityBlock - CityRoad - 0.3f;
            Prim(PrimitiveType.Cube, "Lawn", lot, new Vector3(0f, 0.01f, 0f), new Vector3(inner, 0.02f, inner), Mat("City_Grass", new Color(0.42f, 0.47f, 0.41f)), false);
            Prim(PrimitiveType.Cylinder, "Pond", lot, new Vector3(Random.Range(-0.8f, 0.8f), 0.025f, Random.Range(-0.8f, 0.8f)), new Vector3(1.6f, 0.005f, 1.1f),
                 Mat("City_Pond", new Color(0.4f, 0.44f, 0.5f), null, false, 0.9f), false);
            CityTrees(parent, lots, center, Random.Range(5, 9));
        }

        void CityTrees(Transform parent, List<Transform> lots, Vector3 center, int count)
        {
            var trunk = Mat("City_Trunk", new Color(0.38f, 0.36f, 0.34f));
            var leaves = new[] { Mat("City_Tree1", new Color(0.36f, 0.42f, 0.36f)), Mat("City_Tree2", new Color(0.42f, 0.47f, 0.41f)), Mat("City_Tree3", new Color(0.32f, 0.37f, 0.33f)) };
            float inner = CityBlock - CityRoad - 0.8f;
            for (int i = 0; i < count; i++)
            {
                var pos = center + new Vector3(Random.Range(-inner, inner) / 2f, 0f, Random.Range(-inner, inner) / 2f);
                if (new Vector2(pos.x, pos.z).magnitude < 1.4f) continue; // プレイヤーの足元には生やさない
                var lot = CityLot(parent, lots, pos);
                float h = Random.Range(0.35f, 0.7f);
                Prim(PrimitiveType.Cylinder, "Trunk", lot, new Vector3(0f, h * 0.35f, 0f), new Vector3(0.07f, h * 0.35f, 0.07f), trunk, false);
                Prim(PrimitiveType.Sphere, "Leaves", lot, new Vector3(0f, h * 0.8f, 0f), new Vector3(h * 0.7f, h * 0.65f, h * 0.7f), leaves[Random.Range(0, leaves.Length)], false);
            }
        }


        // ---- 動く的 (KaijuVehicle) ----

        // 乗り物の共通部分: 根元 (KaijuVehicle と当たり判定の Target)・見た目 (Body)・上に浮かぶ点数の札 (いつもプレイヤーの方を向く)
        KaijuVehicle VehicleRoot(Transform parent, string name, KaijuVehicle.Kind kind, float signY, float signSize, Color numberColor, out Transform body, out TextMesh label)
        {
            var root = Group(parent, name);
            body = Group(root, "Body");
            var sign = Group(body, "Sign");
            sign.localPosition = Vector3.up * signY;
            sign.gameObject.AddComponent<FaceCenter>();
            Prim(PrimitiveType.Cube, "SignBoard", sign, Vector3.zero, new Vector3(signSize * 2.2f, signSize * 0.9f, 0.04f), Mat("KaijuSign", new Color(0.08f, 0.08f, 0.1f), new Color(0.04f, 0.04f, 0.05f)), false);
            label = MakeText(sign, "Points", "100", new Vector3(0f, 0f, -0.03f), signSize * 0.7f, numberColor);

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.KaijuVehicle;
            target.manualControl = true;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var v = root.gameObject.AddComponent<KaijuVehicle>();
            v.kind = kind;
            v.body = body;
            v.target = target;
            v.pointLabels = new[] { label };
            v.groundY = CityY + 0.01f;
            v.roadStep = CityBlock;
            v.roadMin = 1;
            v.roadMax = CityCells - 2;
            v.roadExtent = (CityCells - 0.5f) * CityBlock;
            v.laneOffset = CityRoad * 0.24f;
            return v;
        }

        /// <summary>道路を走る車 (セダン・タクシー・バス・トラックを順に)。ビルの大きさに合わせた小さな車</summary>
        KaijuVehicle KaijuCar(Transform parent, int index, Material explosion, Material smoke)
        {
            int type = index % 4; // 0 セダン / 1 タクシー / 2 バス / 3 トラック
            var colors = KaijuTargetColors; // (的はカラフルに)
            int ci = (index * 3) % colors.Length;
            var paint = Mat($"KaijuCar_{ci}", colors[ci], colors[ci] * 0.25f, false, 0.7f);
            var glass = Mat("KaijuCar_Glass", new Color(0.25f, 0.35f, 0.45f), null, false, 0.9f);
            var tire = Mat("KaijuCar_Tire", new Color(0.08f, 0.08f, 0.08f));
            var lamp = Mat("KaijuCar_Lamp", new Color(1f, 0.95f, 0.7f), new Color(1.5f, 1.3f, 0.8f));
            float len = type >= 2 ? 0.9f : 0.55f, wid = type >= 2 ? 0.3f : 0.26f;
            var v = VehicleRoot(parent, $"KaijuCar_{index}", KaijuVehicle.Kind.Car, type >= 2 ? 0.75f : 0.62f, 0.26f, new Color(1f, 0.85f, 0.35f), out var body, out _);
            switch (type)
            {
                case 2: // バス: 長い箱に窓の列
                    Prim(PrimitiveType.Cube, "BusBody", body, new Vector3(0f, 0.17f, 0f), new Vector3(wid, 0.26f, len), paint);
                    Prim(PrimitiveType.Cube, "BusWindows", body, new Vector3(0f, 0.22f, 0f), new Vector3(wid + 0.01f, 0.08f, len * 0.9f), glass, false);
                    Prim(PrimitiveType.Cube, "BusStripe", body, new Vector3(0f, 0.12f, 0f), new Vector3(wid + 0.012f, 0.03f, len), Mat("KaijuCar_White", Color.white), false);
                    break;
                case 3: // トラック: 運転席と荷台の箱
                    Prim(PrimitiveType.Cube, "Cab", body, new Vector3(0f, 0.15f, len * 0.36f), new Vector3(wid, 0.22f, len * 0.26f), Mat("KaijuCar_Blue", new Color(0.2f, 0.4f, 0.85f), null, false, 0.7f));
                    Prim(PrimitiveType.Cube, "CabGlass", body, new Vector3(0f, 0.2f, len * 0.495f), new Vector3(wid * 0.85f, 0.08f, 0.01f), glass, false);
                    Prim(PrimitiveType.Cube, "Cargo", body, new Vector3(0f, 0.19f, -len * 0.13f), new Vector3(wid + 0.02f, 0.3f, len * 0.7f), paint);
                    break;
                default: // セダン / タクシー
                    Prim(PrimitiveType.Cube, "CarBody", body, new Vector3(0f, 0.09f, 0f), new Vector3(wid, 0.1f, len), paint);
                    Prim(PrimitiveType.Cube, "Cabin", body, new Vector3(0f, 0.17f, -0.03f), new Vector3(wid * 0.85f, 0.08f, len * 0.5f), paint);
                    Prim(PrimitiveType.Cube, "CabinGlass", body, new Vector3(0f, 0.17f, -0.03f), new Vector3(wid * 0.86f, 0.055f, len * 0.46f), glass, false);
                    if (type == 1) Prim(PrimitiveType.Cube, "TaxiSign", body, new Vector3(0f, 0.23f, -0.03f), new Vector3(0.08f, 0.04f, 0.04f), lamp, false);
                    break;
            }
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Headlight", body, new Vector3(s * wid * 0.32f, 0.09f, len / 2f + 0.002f), new Vector3(0.05f, 0.03f, 0.01f), lamp, false);
                foreach (float z in new[] { -1f, 1f })
                {
                    var w = Prim(PrimitiveType.Cylinder, "Wheel", body, new Vector3(s * wid / 2f, 0.045f, z * len * 0.32f), new Vector3(0.09f, 0.02f, 0.09f), tire, false);
                    w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            v.pointChoices = type >= 2 ? new[] { 200, 300 } : new[] { 100, 200 };
            v.speed = type >= 2 ? 3.2f : Random.Range(4f, 5.2f);
            v.transform.localScale = Vector3.one * 2f; // (的なので大きめに)
            v.size = 0.45f;
            v.explosionMaterial = explosion;
            v.smokeMaterial = smoke;
            v.respawnDelay = new Vector2(0.6f, 1.6f);
            return v;
        }

        /// <summary>街の上を横切るプロペラ旅客機 (白い胴体に青い線・2つのプロペラ)</summary>
        KaijuVehicle KaijuPlane(Transform parent, int index, Material explosion, Material smoke)
        {
            var white = Mat("KaijuPlane_White", new Color(0.93f, 0.94f, 0.96f), null, false, 0.6f);
            var stripe = Mat("KaijuPlane_Blue", new Color(0.15f, 0.3f, 0.75f), null, false, 0.6f);
            var glass = Mat("KaijuCar_Glass", new Color(0.25f, 0.35f, 0.45f), null, false, 0.9f);
            var dark = Mat("KaijuPlane_Prop", new Color(0.12f, 0.12f, 0.14f));
            var v = VehicleRoot(parent, $"KaijuPlane_{index}", KaijuVehicle.Kind.Plane, 1.1f, 0.45f, new Color(1f, 0.85f, 0.35f), out var body, out _);
            const float L = 2.2f;
            int pc = (index * 3 + 1) % KaijuTargetColors.Length; // (的なので胴体と翼はカラフルに)
            white = Mat($"KaijuPlane_{pc}", KaijuTargetColors[pc], KaijuTargetColors[pc] * 0.25f, false, 0.6f);
            var hull = Prim(PrimitiveType.Capsule, "Fuselage", body, Vector3.zero, new Vector3(0.36f, L / 2f, 0.36f), white);
            hull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var line = Prim(PrimitiveType.Capsule, "Stripe", body, new Vector3(0f, 0.02f, 0f), new Vector3(0.37f, L / 2f * 0.92f, 0.1f), stripe, false);
            line.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Cockpit", body, new Vector3(0f, 0.1f, L / 2f - 0.22f), new Vector3(0.24f, 0.08f, 0.12f), glass, false);
            Prim(PrimitiveType.Cube, "Wing", body, new Vector3(0f, -0.04f, 0.1f), new Vector3(2.6f, 0.04f, 0.42f), white);
            Prim(PrimitiveType.Cube, "TailWing", body, new Vector3(0f, 0.04f, -L / 2f + 0.15f), new Vector3(0.9f, 0.03f, 0.22f), white, false);
            Prim(PrimitiveType.Cube, "TailFin", body, new Vector3(0f, 0.3f, -L / 2f + 0.15f), new Vector3(0.04f, 0.45f, 0.3f), stripe);
            var rotors = new List<Transform>();
            foreach (float s in new[] { -1f, 1f })
            {
                var eng = Prim(PrimitiveType.Cylinder, "Engine", body, new Vector3(s * 0.65f, -0.07f, 0.28f), new Vector3(0.14f, 0.16f, 0.14f), white, false);
                eng.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var prop = Group(body, "Propeller");
                prop.localPosition = new Vector3(s * 0.65f, -0.07f, 0.46f);
                for (int b = 0; b < 3; b++)
                {
                    var blade = Prim(PrimitiveType.Cube, "Blade", prop, Vector3.zero, new Vector3(0.04f, 0.42f, 0.01f), dark, false);
                    blade.transform.localRotation = Quaternion.Euler(0f, 0f, b * 120f);
                    blade.transform.localPosition = blade.transform.localRotation * Vector3.up * 0.1f;
                }
                rotors.Add(prop);
            }
            v.rotors = rotors.ToArray();
            v.rotorAxes = new[] { Vector3.forward, Vector3.forward };
            v.rotorSpeed = 1400f;
            v.pointChoices = new[] { 300, 500 };
            v.speed = 7.5f;
            v.transform.localScale = Vector3.one * 1.6f;
            v.altitude = new Vector2(3.2f, 5f);
            v.distance = new Vector2(6f, 20f);
            v.size = 0.9f;
            v.explosionMaterial = explosion;
            v.smokeMaterial = smoke;
            v.respawnDelay = new Vector2(0.8f, 2f);
            return v;
        }

        /// <summary>怪獣のまわりを回るヘリコプター (報道ヘリ: 青と白、大きな回転翼)</summary>
        KaijuVehicle KaijuHelicopter(Transform parent, int index, Material explosion, Material smoke)
        {
            int hc = (index * 3 + 2) % KaijuTargetColors.Length;
            var paint = Mat($"KaijuHeli_{hc}", KaijuTargetColors[hc], KaijuTargetColors[hc] * 0.25f, false, 0.6f);
            var white = Mat("KaijuPlane_White", new Color(0.93f, 0.94f, 0.96f), null, false, 0.6f);
            var glass = Mat("KaijuCar_Glass", new Color(0.25f, 0.35f, 0.45f), null, false, 0.9f);
            var dark = Mat("KaijuPlane_Prop", new Color(0.12f, 0.12f, 0.14f));
            var v = VehicleRoot(parent, $"KaijuHeli_{index}", KaijuVehicle.Kind.Helicopter, 0.95f, 0.38f, new Color(1f, 0.85f, 0.35f), out var body, out _);
            Prim(PrimitiveType.Sphere, "Cabin", body, Vector3.zero, new Vector3(0.5f, 0.45f, 0.75f), paint);
            Prim(PrimitiveType.Sphere, "Window", body, new Vector3(0f, 0.04f, 0.16f), new Vector3(0.42f, 0.32f, 0.48f), glass, false);
            Prim(PrimitiveType.Cube, "TailBoom", body, new Vector3(0f, 0.05f, -0.6f), new Vector3(0.09f, 0.09f, 0.7f), white);
            Prim(PrimitiveType.Cube, "TailFin", body, new Vector3(0f, 0.15f, -0.92f), new Vector3(0.03f, 0.25f, 0.14f), paint, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Skid", body, new Vector3(s * 0.2f, -0.3f, 0f), new Vector3(0.03f, 0.03f, 0.7f), dark, false);
                Prim(PrimitiveType.Cube, "SkidLeg", body, new Vector3(s * 0.17f, -0.24f, 0f), new Vector3(0.02f, 0.12f, 0.3f), dark, false);
            }
            Prim(PrimitiveType.Cylinder, "Mast", body, new Vector3(0f, 0.27f, 0f), new Vector3(0.05f, 0.05f, 0.05f), dark, false);
            var main = Group(body, "MainRotor");
            main.localPosition = new Vector3(0f, 0.32f, 0f);
            for (int b = 0; b < 2; b++)
            {
                var blade = Prim(PrimitiveType.Cube, "Blade", main, Vector3.zero, new Vector3(1.7f, 0.015f, 0.08f), dark, false);
                blade.transform.localRotation = Quaternion.Euler(0f, b * 90f, 0f);
            }
            var tail = Group(body, "TailRotor");
            tail.localPosition = new Vector3(0.05f, 0.12f, -0.95f);
            Prim(PrimitiveType.Cube, "TailBlade", tail, Vector3.zero, new Vector3(0.01f, 0.3f, 0.04f), dark, false);
            v.rotors = new[] { main, tail };
            v.rotorAxes = new[] { Vector3.up, Vector3.right };
            v.rotorSpeed = 1500f;
            v.pointChoices = new[] { 300, 500 };
            v.speed = 3.5f;
            v.transform.localScale = Vector3.one * 1.7f;
            v.altitude = new Vector2(2.2f, 3.6f);
            v.distance = new Vector2(7f, 14f);
            v.size = 0.7f;
            v.explosionMaterial = explosion;
            v.smokeMaterial = smoke;
            v.respawnDelay = new Vector2(1f, 2.5f);
            return v;
        }

        /// <summary>怪獣の的: 緑の板に怪獣の足あとと、「KAIJU」の文字。まわりを炎の玉が回る</summary>
        GameObject KaijuLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Kaiju_Plate", new Color(0.15f, 0.45f, 0.18f), new Color(0.05f, 0.15f, 0.05f)));
            var dark = Mat("Kaiju_Dark", new Color(0.1f, 0.3f, 0.12f));
            // 足あと: 大きな丸と3本の爪
            var pad = Prim(PrimitiveType.Cylinder, "Footprint", look, c + new Vector3(0f, -0.005f, -0.022f), new Vector3(0.11f, 0.004f, 0.12f), dark, false);
            pad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = -1; i <= 1; i++)
            {
                var toe = Prim(PrimitiveType.Cube, "Toe", look, c + new Vector3(i * 0.04f, 0.075f - Mathf.Abs(i) * 0.012f, -0.022f), new Vector3(0.022f, 0.05f, 0.006f), dark, false);
                toe.transform.localRotation = Quaternion.Euler(0f, 0f, -i * 18f);
            }
            MakeText(look, "Label", "KAIJU", c + new Vector3(0f, -0.115f, -0.025f), 0.04f, new Color(1f, 0.9f, 0.3f));
            var ring = Group(look, "Flames");
            ring.localPosition = c + Vector3.back * 0.03f;
            var spin = ring.gameObject.AddComponent<Rotator>();
            spin.axis = Vector3.forward;
            spin.speed = 120f;
            var flame = Mat("Fireball", new Color(1f, 0.55f, 0.1f), new Color(3f, 1.2f, 0.2f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6;
                Prim(PrimitiveType.Sphere, "Flame", ring, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.2f, Vector3.one * 0.035f, flame, false);
            }
            return look.gameObject;
        }
    }
}
