using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 宇宙モード (宇宙の的を撃つと始まる) の生成: まわりを囲む星空 (色とりどりの点々の星・天の川・うっすら光る星雲・十字に光る明るい星・流れ星)、
    /// 的 (点数の書いた立体の星のパネル・惑星・隕石・ロケット)、宇宙の的の見た目。
    /// </summary>
    public partial class GalleryBuilder
    {
        SpaceStage BuildSpace(BonusStage bonusStage)
        {
            var go = new GameObject("SpaceStage");
            go.SetActive(false);
            go.transform.SetParent(world, false);
            var root = go.transform;

            // ---- 的ではない星々 ----
            var sky = Group(root, "SpaceSky");
            Color[] starColors =
            {
                new Color(1f, 1f, 1f), new Color(0.75f, 0.85f, 1f), new Color(1f, 0.93f, 0.75f),
                new Color(1f, 0.75f, 0.55f), new Color(0.7f, 0.95f, 1f), new Color(0.95f, 0.8f, 1f)
            };
            var clusters = new List<Renderer>();
            // 空いっぱいに散らばった星 (固まりごとにまたたく)
            for (int c = 0; c < 12; c++)
                clusters.Add(StarCluster(sky, $"Stars_{c}", StarFieldMesh($"SpaceStars_{c}", 140, 62f, 88f, 0.16f, 0.45f, null),
                                         Mat($"Space_Star_{c % starColors.Length}", starColors[c % starColors.Length], null, true)));
            // 天の川 (空を斜めに横切る、細かい星の帯)
            var band = new Vector3(0.35f, 1f, 0.45f).normalized;
            for (int c = 0; c < 6; c++)
                clusters.Add(StarCluster(sky, $"MilkyWay_{c}", StarFieldMesh($"SpaceMilkyWay_{c}", 280, 66f, 90f, 0.09f, 0.24f, band),
                                         Mat($"Space_MilkyWay_{c % 3}", Color.Lerp(starColors[c % 3], new Color(0.8f, 0.85f, 1f), 0.5f), null, true)));

            // うっすら光る星雲 (天の川に沿って、色の違う大きな雲を重ねる)
            var nebulae = new List<Renderer>();
            Color[] nebulaColors = { new Color(0.55f, 0.3f, 0.95f, 0.07f), new Color(0.25f, 0.5f, 1f, 0.06f), new Color(1f, 0.35f, 0.65f, 0.06f), new Color(0.3f, 0.9f, 0.85f, 0.05f) };
            for (int i = 0; i < 5; i++)
            {
                var dir = Quaternion.AngleAxis(i * 72f + Random.Range(-15f, 15f), band) * Vector3.Cross(band, Vector3.forward).normalized;
                var center = dir * 78f; // カメラの見える距離 (100m) より手前に収める
                var col = nebulaColors[i % nebulaColors.Length];
                float size = Random.Range(26f, 36f);
                nebulae.Add(Prim(PrimitiveType.Sphere, "Nebula", sky, center, new Vector3(size, size * 0.6f, size), TransparentMat($"Space_Nebula_{i % nebulaColors.Length}", col), false).GetComponent<Renderer>());
                var core = new Color(col.r, col.g, col.b, col.a * 1.6f);
                nebulae.Add(Prim(PrimitiveType.Sphere, "NebulaCore", sky, center + Random.insideUnitSphere * 4f, Vector3.one * size * 0.45f, TransparentMat($"Space_NebulaCore_{i % nebulaColors.Length}", core), false).GetComponent<Renderer>());
            }
            foreach (var n in nebulae) n.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 十字に光る明るい星 (1つずつ脈打つようにきらめく)
            var sparkles = new List<Transform>();
            Material[] sparkleMats =
            {
                Mat("Space_Sparkle_White", Color.white, null, true), Mat("Space_Sparkle_Blue", new Color(0.75f, 0.88f, 1f), null, true),
                Mat("Space_Sparkle_Gold", new Color(1f, 0.92f, 0.65f), null, true)
            };
            var halo = TransparentMat("Space_SparkleHalo", new Color(0.8f, 0.9f, 1f, 0.16f));
            for (int i = 0; i < 48; i++)
            {
                var dir = Random.onUnitSphere;
                float r = Random.Range(55f, 80f);
                var s = Group(sky, "Sparkle");
                s.localPosition = dir * r;
                s.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
                s.localScale = Vector3.one * (r / 70f) * Random.Range(0.8f, 1.5f);
                var mat = sparkleMats[i % sparkleMats.Length];
                Prim(PrimitiveType.Sphere, "Core", s, Vector3.zero, Vector3.one * 0.38f, mat, false);
                Prim(PrimitiveType.Cube, "RayH", s, Vector3.zero, new Vector3(2.6f, 0.06f, 0.06f), mat, false);
                Prim(PrimitiveType.Cube, "RayV", s, Vector3.zero, new Vector3(0.06f, 2.6f, 0.06f), mat, false);
                var diag = Prim(PrimitiveType.Cube, "RayD", s, Vector3.zero, new Vector3(1.2f, 0.04f, 0.04f), mat, false);
                diag.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var diag2 = Prim(PrimitiveType.Cube, "RayD2", s, Vector3.zero, new Vector3(1.2f, 0.04f, 0.04f), mat, false);
                diag2.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                Prim(PrimitiveType.Sphere, "Halo", s, Vector3.zero, Vector3.one * 1.3f, halo, false);
                foreach (var rend in s.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                sparkles.Add(s);
            }

            // 的ではない流れ星 (遠くの空をときどき流れる)
            var shooting = new List<TrailRenderer>();
            var shootMat = TransparentMat("Space_ShootingStar", new Color(0.9f, 0.95f, 1f, 0.85f));
            for (int i = 0; i < 3; i++)
            {
                var head = Prim(PrimitiveType.Sphere, "ShootingStar", sky, Vector3.zero, Vector3.one * 0.35f, sparkleMats[0], false);
                var trail = head.AddComponent<TrailRenderer>();
                trail.sharedMaterial = shootMat;
                trail.time = 0.45f;
                trail.startWidth = 0.4f;
                trail.endWidth = 0f;
                trail.emitting = false;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                shooting.Add(trail);
            }

            // ---- 的 ----
            var explosion = TransparentMat("Space_Explosion", new Color(1f, 0.7f, 0.3f, 0.8f));
            var debris = Mat("Space_Debris", new Color(1f, 0.95f, 0.7f), null, true);
            var panels = new List<SpaceObject>();
            int[] panelPoints = { 100, 100, 100, 200, 200, 200, 300, 300, 500, 1000 };
            for (int i = 0; i < panelPoints.Length; i++) panels.Add(BuildStarPanel(root, i, panelPoints[i], explosion, debris));
            var planets = new List<SpaceObject>
            {
                BuildPlanet(root, "EARTH", PlanetStyle.Earth, 1.3f, 400, explosion, debris),
                BuildPlanet(root, "SATURN", PlanetStyle.Ringed, 1.5f, 500, explosion, debris),
                BuildPlanet(root, "MARS", PlanetStyle.Red, 0.85f, 800, explosion, debris),
                BuildPlanet(root, "JUPITER", PlanetStyle.Striped, 1.9f, 300, explosion, debris),
            };
            var meteors = new List<SpaceObject>();
            for (int i = 0; i < 5; i++) meteors.Add(BuildMeteor(root, i, explosion, debris));
            var rockets = new List<SpaceObject>();
            for (int i = 0; i < 2; i++) rockets.Add(BuildRocket(root, i, explosion, debris));

            var stage = go.AddComponent<SpaceStage>();
            var ground = new List<Transform>();
            foreach (var name in new[] { "Ground", "PitRim" })
            {
                var t = world.Find(name);
                if (t != null) ground.Add(t);
            }
            if (playerMark != null) ground.Add(playerMark.transform);
            stage.groundParts = ground.ToArray();
            // 床の材質ごとに、透けさせる時の半透明版を作っておく (シーンに保存して、実機でも半透明の描き方が使えるようにする)
            var fadeFrom = new List<Material>();
            var fadeTo = new List<Material>();
            foreach (var g in ground)
                foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<TextMesh>() != null) continue;
                    foreach (var m in r.sharedMaterials)
                        if (m != null && !fadeFrom.Contains(m)) { fadeFrom.Add(m); fadeTo.Add(FadeVariant(m)); }
                }
            stage.fadeSourceMaterials = fadeFrom.ToArray();
            stage.fadeMaterials = fadeTo.ToArray();
            stage.showDuringStage = new[] { sky.gameObject };
            stage.starClusters = clusters.ToArray();
            stage.sparkles = sparkles.ToArray();
            stage.nebulae = nebulae.ToArray();
            stage.shootingStars = shooting.ToArray();
            stage.starPanels = panels.ToArray();
            stage.planets = planets.ToArray();
            stage.meteors = meteors.ToArray();
            stage.rockets = rockets.ToArray();
            // 宇宙は宇宙モードの時だけ出る (シーンに生成した時も隠しておく)
            sky.gameObject.SetActive(false);
            foreach (var group in new[] { panels, planets, meteors, rockets })
                foreach (var o in group) o.gameObject.SetActive(false);
            go.SetActive(true);
            return stage;
        }

        /// <summary>材質の半透明版 (色・光り方はそのままで、_BaseColor のアルファで透けさせられる)。名前は元の名前 + "_Fade"</summary>
        Material FadeVariant(Material src)
        {
            string name = src.name + "_Fade";
            if (mats.TryGetValue(name, out var cached)) return cached;
            var mat = new Material(src) { name = name };
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            // 透けている間に、つやの照り返し (空の色) が残って色づいて見えないよう、つやと映り込みを切っておく
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }


        Renderer StarCluster(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>
        /// 星の点々のメッシュ: 原点 (プレイヤー) を向いた小さなひし形を count 個、半径 rMin〜rMax の球面のまわりに散らす。
        /// band を渡すと、その向きを軸にした大きな円 (天の川) の近くに集める。遠い星ほど大きくして、見た目の大きさをそろえる
        /// </summary>
        Mesh StarFieldMesh(string name, int count, float rMin, float rMax, float sizeMin, float sizeMax, Vector3? band)
        {
            if (meshes.TryGetValue(name, out var cached)) return cached;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                if (band.HasValue)
                {
                    var n = band.Value;
                    dir = (dir - n * Vector3.Dot(dir, n) * Random.Range(0.82f, 1f)).normalized; // 帯の近くに寄せる
                }
                float r = Random.Range(rMin, rMax);
                // 小さな星が多く、大きな星は少ない
                float s = Mathf.Lerp(sizeMin, sizeMax, Mathf.Pow(Random.value, 2.5f)) * r / 75f;
                var p = dir * r;
                var t1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var t2 = Vector3.Cross(dir, t1);
                int k = verts.Count;
                verts.Add(p + t1 * s); verts.Add(p + t2 * s); verts.Add(p - t1 * s); verts.Add(p - t2 * s);
                tris.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 }); // 表と裏の両方 (どちらから見ても見える)
                tris.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
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

        // 的の共通部分: 当たり判定 (Target) と SpaceObject を付ける
        SpaceObject SpaceTarget(Transform root, Transform body, SpaceObject.Kind kind, string displayName, int[] points, List<TextMesh> labels, Transform labelRoot,
                                Material explosion, Material debris, float blastSize)
        {
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.SpaceObject;
            target.manualControl = true;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var o = root.gameObject.AddComponent<SpaceObject>();
            o.kind = kind;
            o.displayName = displayName;
            o.body = body;
            o.hitTarget = target;
            o.pointChoices = points;
            o.pointLabels = labels.ToArray();
            o.labelRoot = labelRoot;
            o.explosionMaterial = explosion;
            o.debrisMaterial = debris;
            o.blastSize = blastSize;
            return o;
        }

        // 光の尾 (流れる的の後ろに引く)
        TrailRenderer SpaceTrail(GameObject go, Material mat, float time, float width)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = mat;
            trail.time = time;
            trail.startWidth = width;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.15f;
            trail.emitting = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        // いつもプレイヤーの方を向く点数の札 (大きく光る数字と、うしろの暗い板)
        TextMesh SpacePointLabel(Transform labelRoot, float size, Color color)
        {
            Prim(PrimitiveType.Cube, "LabelBack", labelRoot, new Vector3(0f, 0f, 0.03f), new Vector3(size * 2.3f, size * 1.05f, 0.02f),
                 TransparentMat("Space_LabelBack", new Color(0.02f, 0.03f, 0.1f, 0.55f)), false);
            return MakeText(labelRoot, "Points", "100", Vector3.zero, size, color);
        }

        static Color PointColor(int points) => points >= 1000 ? new Color(1f, 0.82f, 0.25f)
                                             : points >= 500 ? new Color(1f, 0.45f, 0.3f)
                                             : points >= 300 ? new Color(1f, 0.45f, 0.85f)
                                             : points >= 200 ? new Color(0.35f, 0.9f, 1f)
                                             : new Color(1f, 0.95f, 0.4f);

        /// <summary>
        /// 立体の星のパネル: 厚みのある星形の板 (点数ごとに色が違う。1000 点は金) の表に、大きく点数。流星群のように光の尾を引いて流れる。
        /// 表はいつもプレイヤーの方を向く
        /// </summary>
        SpaceObject BuildStarPanel(Transform parent, int index, int points, Material explosion, Material debris)
        {
            var root = Group(parent, $"StarPanel_{points}_{index}");
            var body = Group(root, "Body");
            Color c = PointColor(points);
            bool gold = points >= 1000;
            float size = gold ? 1.5f : 1.2f;
            var face = Mat($"Space_StarPanel_{points}", c, c * 0.45f, false, gold ? 0.85f : 0.5f);
            var edge = Mat($"Space_StarPanelEdge_{points}", c * 0.6f, c * 0.2f, false, 0.6f);
            var front = new GameObject("Star");
            front.transform.SetParent(body, false);
            front.transform.localScale = new Vector3(size, size, 5f); // 厚さ 0.1m の立体の星
            front.AddComponent<MeshFilter>().sharedMesh = ShapeMesh(Shape.Star);
            front.AddComponent<MeshRenderer>().sharedMaterial = face;
            var back = new GameObject("StarEdge");
            back.transform.SetParent(body, false);
            back.transform.localPosition = Vector3.forward * 0.05f;
            back.transform.localScale = new Vector3(size * 1.12f, size * 1.12f, 4f);
            back.AddComponent<MeshFilter>().sharedMesh = ShapeMesh(Shape.Star);
            back.AddComponent<MeshRenderer>().sharedMaterial = edge;
            body.gameObject.AddComponent<SphereCollider>().radius = size * 0.42f;
            var labels = new List<TextMesh> { MakeText(body, "Points", points.ToString(), new Vector3(0f, -0.02f, -0.07f), size * (points >= 1000 ? 0.26f : 0.32f),
                                                       gold ? new Color(0.45f, 0.08f, 0.05f) : new Color(0.04f, 0.05f, 0.18f)) };
            var o = SpaceTarget(root, body, SpaceObject.Kind.StarPanel, gold ? "GOLDEN STAR" : "STAR", new[] { points }, labels, null, explosion, debris, gold ? 1.3f : 0.9f);
            o.spinSpeed = 0f;
            o.trail = SpaceTrail(root.gameObject, TransparentMat($"Space_StarTrail_{points}", new Color(c.r, c.g, c.b, 0.5f)), 1.1f, size * 0.7f);
            return o;
        }

        enum PlanetStyle { Earth, Ringed, Red, Striped }

        /// <summary>惑星 (地球のような青い星 / 輪のある星 / 赤い小さな星 / しま模様の大きな星)。上に大きく点数の札が浮かぶ</summary>
        SpaceObject BuildPlanet(Transform parent, string name, PlanetStyle style, float r, int points, Material explosion, Material debris)
        {
            var root = Group(parent, $"Planet_{name}");
            var body = Group(root, "Body");
            switch (style)
            {
                case PlanetStyle.Earth:
                {
                    Prim(PrimitiveType.Sphere, "Ocean", body, Vector3.zero, Vector3.one * r * 2f, Mat("Planet_Ocean", new Color(0.15f, 0.4f, 0.9f), new Color(0.03f, 0.08f, 0.2f), false, 0.7f));
                    var land = Mat("Planet_Land", new Color(0.25f, 0.6f, 0.25f), new Color(0.04f, 0.1f, 0.04f));
                    var cloud = Mat("Planet_Cloud", new Color(0.95f, 0.97f, 1f), new Color(0.2f, 0.2f, 0.22f));
                    for (int i = 0; i < 7; i++)
                        Prim(PrimitiveType.Sphere, "Land", body, Random.onUnitSphere * r * 0.82f, Vector3.one * r * Random.Range(0.55f, 0.85f), land, false);
                    for (int i = 0; i < 6; i++)
                        Prim(PrimitiveType.Sphere, "Cloud", body, Random.onUnitSphere * r * 0.95f, new Vector3(r * 0.7f, r * 0.12f, r * 0.4f), cloud, false)
                            .transform.localRotation = Random.rotation;
                    break;
                }
                case PlanetStyle.Ringed:
                {
                    body.localRotation = Quaternion.Euler(0f, 0f, 22f);
                    Prim(PrimitiveType.Sphere, "Globe", body, Vector3.zero, Vector3.one * r * 2f, Mat("Planet_Tan", new Color(0.9f, 0.78f, 0.5f), new Color(0.15f, 0.12f, 0.06f)));
                    Prim(PrimitiveType.Sphere, "Band", body, Vector3.zero, new Vector3(r * 2.01f, r * 0.5f, r * 2.01f), Mat("Planet_TanDark", new Color(0.75f, 0.58f, 0.35f)), false);
                    FlatRing(body, "Ring", r * 1.55f, 0f, r * 0.32f, 0.02f, 48, Mat("Planet_Ring", new Color(0.95f, 0.88f, 0.7f), new Color(0.2f, 0.18f, 0.12f)));
                    FlatRing(body, "Ring", r * 1.95f, 0f, r * 0.38f, 0.02f, 56, Mat("Planet_Ring2", new Color(0.8f, 0.7f, 0.55f), new Color(0.15f, 0.13f, 0.08f)));
                    break;
                }
                case PlanetStyle.Red:
                {
                    Prim(PrimitiveType.Sphere, "Globe", body, Vector3.zero, Vector3.one * r * 2f, Mat("Planet_Red", new Color(0.85f, 0.35f, 0.2f), new Color(0.15f, 0.04f, 0.02f)));
                    var crater = Mat("Planet_Crater", new Color(0.55f, 0.2f, 0.12f));
                    for (int i = 0; i < 8; i++)
                        Prim(PrimitiveType.Sphere, "Crater", body, Random.onUnitSphere * r * 0.9f, Vector3.one * r * Random.Range(0.25f, 0.45f), crater, false);
                    Prim(PrimitiveType.Sphere, "IceCap", body, Vector3.up * r * 0.88f, new Vector3(r * 0.9f, r * 0.3f, r * 0.9f), Mat("Planet_Cloud", new Color(0.95f, 0.97f, 1f), new Color(0.2f, 0.2f, 0.22f)), false);
                    break;
                }
                default: // しま模様
                {
                    Prim(PrimitiveType.Sphere, "Globe", body, Vector3.zero, Vector3.one * r * 2f, Mat("Planet_Orange", new Color(0.92f, 0.65f, 0.4f), new Color(0.15f, 0.1f, 0.05f)));
                    Material[] stripes = { Mat("Planet_StripeA", new Color(0.7f, 0.4f, 0.25f)), Mat("Planet_StripeB", new Color(0.98f, 0.88f, 0.7f)) };
                    for (int i = -3; i <= 3; i++)
                    {
                        float h = i * r * 0.24f;
                        float rr = Mathf.Sqrt(Mathf.Max(0.01f, r * r - h * h)) + 0.01f;
                        Prim(PrimitiveType.Cylinder, "Stripe", body, Vector3.up * h, new Vector3(rr * 2f, r * 0.05f, rr * 2f), stripes[(i + 3) % 2], false);
                    }
                    Prim(PrimitiveType.Sphere, "RedSpot", body, new Vector3(r * 0.55f, -r * 0.3f, -r * 0.72f), new Vector3(r * 0.4f, r * 0.22f, r * 0.2f), Mat("Planet_Spot", new Color(0.8f, 0.25f, 0.15f)), false);
                    break;
                }
            }
            // うすい大気の光
            Prim(PrimitiveType.Sphere, "Atmosphere", body, Vector3.zero, Vector3.one * r * 2.25f, TransparentMat($"Planet_Atmos_{style}", style == PlanetStyle.Red ? new Color(1f, 0.5f, 0.3f, 0.12f) : new Color(0.5f, 0.75f, 1f, 0.12f)), false);

            var labelRoot = Group(root, "Label");
            labelRoot.localPosition = Vector3.up * (r * (style == PlanetStyle.Ringed ? 1.7f : 1.25f) + 0.7f);
            var labels = new List<TextMesh> { SpacePointLabel(labelRoot, 0.8f, PointColor(points)) };
            var o = SpaceTarget(root, body, SpaceObject.Kind.Planet, name, new[] { points }, labels, labelRoot, explosion, debris, r * 1.3f);
            o.spinAxis = Vector3.up;
            o.spinSpeed = Random.Range(18f, 30f);
            o.appearTime = 1.1f;
            return o;
        }

        /// <summary>隕石: ごつごつした岩 (いくつかの球を重ねる) が、ごろごろ回りながら炎の尾を引いて横切る</summary>
        SpaceObject BuildMeteor(Transform parent, int index, Material explosion, Material debris)
        {
            var root = Group(parent, $"Meteor_{index}");
            var body = Group(root, "Body");
            float r = Random.Range(0.45f, 0.7f);
            var rock = Mat("Meteor_Rock", new Color(0.42f, 0.36f, 0.32f), null, false, 0.15f);
            var rockDark = Mat("Meteor_RockDark", new Color(0.28f, 0.24f, 0.22f), null, false, 0.15f);
            Prim(PrimitiveType.Sphere, "Rock", body, Vector3.zero, Vector3.one * r * 2f, rock);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Sphere, "Lump", body, Random.onUnitSphere * r * 0.6f, Vector3.one * r * Random.Range(0.8f, 1.2f), i % 2 == 0 ? rockDark : rock, false);
            var glow = Mat("Meteor_Glow", new Color(1f, 0.5f, 0.15f), new Color(2.2f, 0.8f, 0.15f));
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Sphere, "Ember", body, Random.onUnitSphere * r * 0.9f, Vector3.one * r * 0.35f, glow, false);
            var labelRoot = Group(root, "Label");
            labelRoot.localPosition = Vector3.up * (r + 0.6f);
            var labels = new List<TextMesh> { SpacePointLabel(labelRoot, 0.55f, PointColor(150)) };
            var o = SpaceTarget(root, body, SpaceObject.Kind.Meteor, "METEOR", new[] { 100, 150, 200 }, labels, labelRoot, explosion, debris, r * 1.6f);
            o.spinSpeed = Random.Range(60f, 120f);
            o.trail = SpaceTrail(root.gameObject, TransparentMat("Meteor_Trail", new Color(1f, 0.55f, 0.2f, 0.45f)), 0.9f, r * 1.6f);
            return o;
        }

        /// <summary>ロケット: 白い胴体・赤い先端と羽根・丸い窓。お尻から炎を噴き、白い煙を引いて飛んでいく (+Z が機首)</summary>
        SpaceObject BuildRocket(Transform parent, int index, Material explosion, Material debris)
        {
            var root = Group(parent, $"Rocket_{index}");
            var body = Group(root, "Body");
            var white = Mat("Rocket_White", new Color(0.93f, 0.94f, 0.97f), new Color(0.12f, 0.12f, 0.14f), false, 0.6f);
            var red = Mat("Rocket_Red", new Color(0.9f, 0.15f, 0.12f), new Color(0.2f, 0.02f, 0.02f), false, 0.5f);
            var window = Mat("Rocket_Window", new Color(0.4f, 0.8f, 1f), new Color(0.3f, 0.7f, 1.2f));
            var flame = Mat("Rocket_Flame", new Color(1f, 0.7f, 0.25f), new Color(3f, 1.6f, 0.4f), true);
            Prim(PrimitiveType.Capsule, "Hull", body, Vector3.zero, new Vector3(0.7f, 1.1f, 0.7f), white).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "Nose", body, new Vector3(0f, 0f, 1.05f), new Vector3(0.5f, 0.5f, 0.7f), red, false);
            Prim(PrimitiveType.Cylinder, "Band", body, new Vector3(0f, 0f, 0.45f), new Vector3(0.72f, 0.05f, 0.72f), red, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 3; i++)
            {
                var fin = Prim(PrimitiveType.Cube, "Fin", body, Quaternion.Euler(0f, 0f, i * 120f) * new Vector3(0f, 0.45f, -0.75f), new Vector3(0.05f, 0.45f, 0.55f), red, false);
                fin.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
            }
            foreach (float z in new[] { 0.15f, -0.25f })
                Prim(PrimitiveType.Sphere, "Window", body, new Vector3(0f, 0.3f, z), new Vector3(0.22f, 0.12f, 0.22f), window, false);
            Prim(PrimitiveType.Sphere, "Flame", body, new Vector3(0f, 0f, -1.25f), new Vector3(0.38f, 0.38f, 0.7f), flame, false);
            Prim(PrimitiveType.Sphere, "FlameCore", body, new Vector3(0f, 0f, -1.1f), new Vector3(0.24f, 0.24f, 0.4f), Mat("Rocket_FlameCore", new Color(1f, 1f, 0.85f), null, true), false);
            var labelRoot = Group(root, "Label");
            labelRoot.localPosition = Vector3.up * 1.2f;
            var labels = new List<TextMesh> { SpacePointLabel(labelRoot, 0.65f, PointColor(600)) };
            var o = SpaceTarget(root, body, SpaceObject.Kind.Rocket, "ROCKET", new[] { 600 }, labels, labelRoot, explosion, debris, 1.6f);
            o.spinAxis = Vector3.forward; // 機首の向きを軸に、ゆっくり回りながら飛ぶ
            o.spinSpeed = 35f;
            o.trail = SpaceTrail(root.gameObject, TransparentMat("Rocket_Smoke", new Color(0.92f, 0.94f, 1f, 0.4f)), 1.6f, 0.55f);
            return o;
        }

        /// <summary>宇宙の的: 紺色の板に、輪のある惑星と小さな星々、「SPACE」の文字。惑星がゆっくり回る</summary>
        GameObject SpaceLook(Transform look)
        {
            var c = new Vector3(0f, TargetDiscY, 0f);
            Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.3f, 0.3f, 0.04f), Mat("Space_Plate", new Color(0.05f, 0.06f, 0.2f), new Color(0.02f, 0.02f, 0.08f)));
            var star = Mat("Space_Sparkle_White", Color.white, null, true);
            for (int i = 0; i < 9; i++)
                Prim(PrimitiveType.Sphere, "Star", look, c + new Vector3(Random.Range(-0.13f, 0.13f), Random.Range(-0.08f, 0.13f), -0.022f), Vector3.one * Random.Range(0.008f, 0.016f), star, false);
            var spin = Group(look, "Planet");
            spin.localPosition = c + new Vector3(0f, 0.02f, -0.05f);
            spin.localRotation = Quaternion.Euler(-20f, 0f, 18f);
            var rot = spin.gameObject.AddComponent<Rotator>();
            rot.axis = Vector3.up;
            rot.speed = 60f;
            Prim(PrimitiveType.Sphere, "Globe", spin, Vector3.zero, Vector3.one * 0.1f, Mat("Planet_Tan", new Color(0.9f, 0.78f, 0.5f), new Color(0.15f, 0.12f, 0.06f)), false);
            FlatRing(spin, "Ring", 0.085f, 0f, 0.025f, 0.003f, 24, Mat("Planet_Ring", new Color(0.95f, 0.88f, 0.7f), new Color(0.2f, 0.18f, 0.12f)));
            MakeText(look, "Label", "SPACE", c + new Vector3(0f, -0.115f, -0.025f), 0.04f, new Color(0.6f, 0.8f, 1f));
            return look.gameObject;
        }
    }
}
