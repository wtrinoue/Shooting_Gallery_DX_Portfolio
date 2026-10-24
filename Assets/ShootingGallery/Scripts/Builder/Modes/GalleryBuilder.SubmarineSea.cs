using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 潜水艦モードの的の生成: 海の生き物 (クラゲ・フグ・アンコウ・タコ・ウミガメ・サメ)、さまよっているほかの潜水艦、神殿の壺、開いたり閉じたりする宝箱、潜水艦の的の見た目。
    /// どの生き物も頭 (前) が +Z。泳ぐ的は SeaCreature が進む向きへ +Z を向ける。
    /// </summary>
    public partial class GalleryBuilder
    {
        /// <summary>
        /// 海の的の骨組み: 根元 (SeaCreature) → Body (Target) → Visual (見た目と、見えない大きめの当たり判定の球)。
        /// 細かい部品には当たり判定を付けず、当たり判定の球だけで当てやすくする
        /// </summary>
        SeaCreature NewSeaTarget(Transform sea, string name, int points, float hitRadius, Vector3 hitCenter, Vector3 labelPos, float labelSize, Material bubble, out Transform visual)
        {
            var root = Group(sea, name);
            var body = Group(root, "Body");
            visual = Group(body, "Visual");
            var hit = Prim(PrimitiveType.Sphere, "Hit", visual, hitCenter, Vector3.one * hitRadius * 2f, null);
            HitOnly(hit);
            SeaPointSign(visual, points, labelPos, labelSize);
            var target = body.gameObject.AddComponent<Target>();
            target.kind = TargetKind.SubmarineObject;
            target.points = points;
            target.manualControl = true;
            target.shrinkOnHit = true;
            target.visual = visual;
            target.SetAvailable(false);
            var c = root.gameObject.AddComponent<SeaCreature>();
            c.hitTarget = target;
            c.points = points;
            c.bubbleMaterial = bubble;
            return c;
        }

        readonly List<GameObject> subSigns = new List<GameObject>(); // 海の的の点数の札 (SubmarineDive がもぐっている間だけ出す)

        /// <summary>
        /// 的のすぐ上に浮かぶ点数の札 (暗い板に大きな数字。いつもプレイヤーの方を向く)。
        /// 的の見た目 (visual) の子にするので、当たって縮むといっしょに消え、出直すといっしょに出る
        /// </summary>
        void SeaPointSign(Transform visual, int points, Vector3 pos, float height)
        {
            var sign = Group(visual, "PointSign");
            sign.localPosition = pos;
            sign.gameObject.AddComponent<FaceCenter>();
            var color = points >= 1000 ? new Color(1f, 0.85f, 0.3f) : points >= 400 ? new Color(1f, 0.55f, 0.6f) : points >= 200 ? new Color(0.6f, 1f, 0.7f) : new Color(0.75f, 0.95f, 1f);
            MakeText(sign, "Points", points.ToString(), Vector3.zero, height, color);
            float w = height * (points >= 1000 ? 2.7f : 2.1f);
            Prim(PrimitiveType.Cube, "Board", sign, new Vector3(0f, 0f, 0.03f), new Vector3(w, height * 1.3f, 0.03f), Mat("Sea_PointBoard", new Color(0.03f, 0.1f, 0.18f), new Color(0.01f, 0.04f, 0.08f)), false);
            subSigns.Add(sign.gameObject);
        }

        /// <summary>
        /// 見えない当たり判定にする: 見た目 (MeshRenderer と MeshFilter) を取り除いて、コライダーだけ残す。
        /// (マテリアルのない MeshRenderer を残すと、SceneEnvironment が「マテリアルが外れている」と警告するため)
        /// </summary>
        static void HitOnly(GameObject hit)
        {
            DestroyImmediate(hit.GetComponent<MeshRenderer>());
            DestroyImmediate(hit.GetComponent<MeshFilter>());
        }

        // 目 (白目と黒目)。pos は目の真ん中、外向き out の方を見る
        void SeaEye(Transform parent, Vector3 pos, float size, Vector3 outward)
        {
            var white = Mat("Sea_EyeWhite", new Color(0.97f, 0.97f, 0.95f), null, false, 0.7f);
            var black = Mat("Sea_EyeBlack", new Color(0.03f, 0.03f, 0.04f), null, false, 0.9f);
            Prim(PrimitiveType.Sphere, "Eye", parent, pos, Vector3.one * size, white, false);
            Prim(PrimitiveType.Sphere, "Pupil", parent, pos + outward.normalized * size * 0.32f, Vector3.one * size * 0.55f, black, false);
        }

        List<SeaCreature> BuildSeaCreatures(Transform sea, Material bubble)
        {
            var list = new List<SeaCreature>();
            for (int i = 0; i < 9; i++) list.Add(Jellyfish(sea, i, bubble));
            for (int i = 0; i < 4; i++) list.Add(Pufferfish(sea, i, bubble));
            for (int i = 0; i < 3; i++) list.Add(Anglerfish(sea, i, bubble));
            float[] octoDeg = { 195f, 60f, 330f };
            for (int i = 0; i < octoDeg.Length; i++) list.Add(Octopus(sea, i, Polar(octoDeg[i], Random.Range(9f, 12f), SeaFloor + 0.05f), bubble));
            for (int i = 0; i < 2; i++) list.Add(SeaTurtle(sea, i, bubble));
            for (int i = 0; i < 2; i++) list.Add(Shark(sea, i, bubble));
            for (int i = 0; i < 3; i++) list.Add(EnemySub(sea, i, bubble));
            return list;
        }

        // ------------------------------------------------------------------
        // 生き物
        // ------------------------------------------------------------------

        /// <summary>クラゲ (100点): すき通ったかさがふわふわふくらみ、長い足がゆらゆら揺れる。ふわふわ漂う</summary>
        SeaCreature Jellyfish(Transform sea, int i, Material bubble)
        {
            Color[] colors = { new(1f, 0.5f, 0.8f), new(0.7f, 0.5f, 1f), new(0.4f, 0.9f, 1f), new(1f, 0.72f, 0.35f) };
            var col = colors[i % colors.Length];
            var c = NewSeaTarget(sea, $"Jellyfish_{i}", 100, 0.6f, Vector3.zero, new Vector3(0f, 0.85f, 0f), 0.45f, bubble, out var v);
            var bell = Group(v, "Bell");
            Prim(PrimitiveType.Sphere, "Dome", bell, Vector3.zero, new Vector3(0.95f, 0.6f, 0.95f), TransparentMat($"Jelly_Bell{i % colors.Length}", new Color(col.r, col.g, col.b, 0.45f)), false);
            Prim(PrimitiveType.Sphere, "Core", bell, new Vector3(0f, 0.05f, 0f), new Vector3(0.45f, 0.3f, 0.45f), Mat($"Jelly_Core{i % colors.Length}", col, col * 1.2f), false);
            var legMat = TransparentMat($"Jelly_Leg{i % colors.Length}", new Color(col.r, col.g, col.b, 0.55f));
            var parts = new List<Transform>();
            for (int k = 0; k < 8; k++) // 長い足
            {
                float a = k * Mathf.PI * 2f / 8f;
                var pivot = Group(v, "Tentacle");
                pivot.localPosition = new Vector3(Mathf.Sin(a) * 0.35f, -0.12f, Mathf.Cos(a) * 0.35f);
                pivot.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f);
                Prim(PrimitiveType.Capsule, "Leg", pivot, new Vector3(0f, -0.55f, 0f), new Vector3(0.035f, 0.55f, 0.035f), legMat, false);
                parts.Add(pivot);
            }
            for (int k = 0; k < 4; k++) // 真ん中のひらひらした口の腕
            {
                var pivot = Group(v, "OralArm");
                pivot.localPosition = new Vector3(0f, -0.15f, 0f);
                pivot.localRotation = Quaternion.Euler(0f, k * 90f + 45f, 0f);
                Prim(PrimitiveType.Cube, "Frill", pivot, new Vector3(0f, -0.4f, 0.06f), new Vector3(0.14f, 0.75f, 0.02f), legMat, false).transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
                parts.Add(pivot);
            }
            c.motion = SeaCreature.Motion.Drift;
            c.home = Polar(i * 40f + Random.Range(-12f, 12f), Random.Range(5f, 15f), Random.Range(1.2f, 8f));
            c.bob = 0.6f;
            c.bobSpeed = Random.Range(0.45f, 0.75f);
            c.driftRange = 1.2f;
            c.pulse = bell;
            c.pulseAmount = new Vector3(0.12f, -0.16f, 0.12f);
            c.pulseSpeed = 2.6f;
            c.wiggle = parts.ToArray();
            c.wiggleAxis = Vector3.right;
            c.wiggleAmp = 14f;
            c.wiggleSpeed = 2.4f;
            c.wigglePhaseStep = 0.8f;
            c.respawnDelay = 3f;
            c.displayName = "JELLYFISH";
            c.transform.localPosition = c.home;
            return c;
        }

        /// <summary>フグ (150点): とげとげの丸い体で、ときどきぷくーっとふくらむ。まわりをぐるっと泳ぐ</summary>
        SeaCreature Pufferfish(Transform sea, int i, Material bubble)
        {
            var c = NewSeaTarget(sea, $"Pufferfish_{i}", 150, 0.55f, Vector3.zero, new Vector3(0f, 0.75f, 0f), 0.45f, bubble, out var v);
            var skin = Mat("Puffer_Skin", new Color(0.9f, 0.78f, 0.4f), new Color(0.08f, 0.06f, 0.02f), false, 0.4f);
            var belly = Mat("Puffer_Belly", new Color(0.97f, 0.95f, 0.88f), null, false, 0.4f);
            var spot = Mat("Puffer_Spot", new Color(0.35f, 0.25f, 0.12f), null, false, 0.3f);
            var puff = Group(v, "Puff");
            Prim(PrimitiveType.Sphere, "Body", puff, Vector3.zero, Vector3.one * 0.62f, skin, false);
            Prim(PrimitiveType.Sphere, "Belly", puff, new Vector3(0f, -0.1f, 0.03f), new Vector3(0.52f, 0.42f, 0.55f), belly, false);
            for (int k = 0; k < 7; k++)
                Prim(PrimitiveType.Sphere, "Spot", puff, Random.onUnitSphere.normalized * 0.3f + Vector3.up * 0.06f, Vector3.one * 0.07f, spot, false);
            for (int k = 0; k < 18; k++) // とげ
            {
                var d = Random.onUnitSphere;
                if (d.z > 0.75f) d.z = 0.3f; // 顔にはとげを付けない
                var spike = Prim(PrimitiveType.Cube, "Spike", puff, d.normalized * 0.33f, new Vector3(0.025f, 0.025f, 0.14f), spot, false);
                spike.transform.localRotation = Quaternion.LookRotation(d.normalized);
            }
            SeaEye(puff, new Vector3(-0.15f, 0.1f, 0.23f), 0.13f, new Vector3(-0.3f, 0.2f, 1f));
            SeaEye(puff, new Vector3(0.15f, 0.1f, 0.23f), 0.13f, new Vector3(0.3f, 0.2f, 1f));
            Prim(PrimitiveType.Sphere, "Mouth", puff, new Vector3(0f, -0.05f, 0.31f), new Vector3(0.09f, 0.06f, 0.05f), Mat("Puffer_Mouth", new Color(0.95f, 0.5f, 0.35f)), false);
            var parts = new List<Transform>();
            foreach (float s in new[] { -1f, 1f }) // 胸びれ
            {
                var fin = Group(v, "Fin");
                fin.localPosition = new Vector3(s * 0.3f, 0f, 0f);
                Prim(PrimitiveType.Cube, "FinBlade", fin, new Vector3(s * 0.07f, 0f, -0.02f), new Vector3(0.14f, 0.1f, 0.02f), skin, false);
                parts.Add(fin);
            }
            var tail = Group(v, "Tail");
            tail.localPosition = new Vector3(0f, 0f, -0.32f);
            Prim(PrimitiveType.Cube, "TailFin", tail, new Vector3(0f, 0f, -0.08f), new Vector3(0.02f, 0.22f, 0.16f), skin, false);
            parts.Add(tail);
            c.motion = SeaCreature.Motion.Circle;
            c.home = new Vector3(0f, Random.Range(1.5f, 5f), 0f);
            c.radius = Random.Range(5f, 8f);
            c.squash = Random.Range(0.75f, 1f);
            c.angularSpeed = Random.Range(14f, 20f) * (i % 2 == 0 ? 1f : -1f);
            c.bob = 0.4f;
            c.bobSpeed = 0.8f;
            c.pulse = puff;
            c.puffer = true;
            c.pulseSpeed = Random.Range(1.6f, 2.4f);
            c.wiggle = parts.ToArray();
            c.wiggleAxis = Vector3.up;
            c.wiggleAmp = 30f;
            c.wiggleSpeed = 9f;
            c.wigglePhaseStep = 1.5f;
            c.respawnDelay = 3.5f;
            c.displayName = "PUFFERFISH";
            return c;
        }

        /// <summary>アンコウ (300点): 黒っぽい大きな頭に、とがった歯の並んだ大きな口。頭の上の光るちょうちんでゆらゆら誘う。海底の近くをゆっくり泳ぐ</summary>
        SeaCreature Anglerfish(Transform sea, int i, Material bubble)
        {
            var c = NewSeaTarget(sea, $"Anglerfish_{i}", 300, 0.8f, Vector3.zero, new Vector3(0f, 1.45f, 0f), 0.55f, bubble, out var v);
            var skin = Mat("Angler_Skin", new Color(0.24f, 0.2f, 0.2f), null, false, 0.35f);
            var dark = Mat("Angler_Mouth", new Color(0.08f, 0.02f, 0.03f));
            var tooth = Mat("Angler_Tooth", new Color(0.95f, 0.95f, 0.9f), new Color(0.15f, 0.15f, 0.15f), false, 0.7f);
            Prim(PrimitiveType.Sphere, "Body", v, Vector3.zero, new Vector3(1.05f, 0.85f, 1.2f), skin, false);
            Prim(PrimitiveType.Sphere, "MouthInside", v, new Vector3(0f, -0.12f, 0.45f), new Vector3(0.8f, 0.4f, 0.4f), dark, false);
            Prim(PrimitiveType.Cube, "Jaw", v, new Vector3(0f, -0.32f, 0.42f), new Vector3(0.85f, 0.12f, 0.45f), skin, false).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            for (int k = 0; k < 9; k++) // 上下にとがった歯
            {
                float x = -0.34f + k * 0.085f;
                Prim(PrimitiveType.Cube, "ToothUp", v, new Vector3(x, -0.02f, 0.6f - Mathf.Abs(x) * 0.25f), new Vector3(0.03f, 0.12f, 0.03f), tooth, false).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                Prim(PrimitiveType.Cube, "ToothDown", v, new Vector3(x + 0.04f, -0.22f, 0.62f - Mathf.Abs(x) * 0.25f), new Vector3(0.03f, 0.14f, 0.03f), tooth, false).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            }
            SeaEye(v, new Vector3(-0.28f, 0.2f, 0.42f), 0.1f, new Vector3(-0.4f, 0f, 1f));
            SeaEye(v, new Vector3(0.28f, 0.2f, 0.42f), 0.1f, new Vector3(0.4f, 0f, 1f));
            var parts = new List<Transform>();
            // 頭の上のちょうちん (曲がった竿の先に光る玉。小さな明かり付き)
            var lure = Group(v, "Lure");
            lure.localPosition = new Vector3(0f, 0.38f, 0.25f);
            Prim(PrimitiveType.Cylinder, "Rod1", lure, new Vector3(0f, 0.2f, 0.04f), new Vector3(0.03f, 0.2f, 0.03f), skin, false).transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "Rod2", lure, new Vector3(0f, 0.43f, 0.22f), new Vector3(0.025f, 0.15f, 0.025f), skin, false).transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            var bulbMat = Mat("Angler_Lure", new Color(0.85f, 1f, 0.55f), new Color(2.2f, 2.8f, 1.2f));
            Prim(PrimitiveType.Sphere, "Bulb", lure, new Vector3(0f, 0.5f, 0.38f), Vector3.one * 0.14f, bulbMat, false);
            var lg = new GameObject("LureLight");
            lg.transform.SetParent(lure, false);
            lg.transform.localPosition = new Vector3(0f, 0.5f, 0.45f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4f;
            l.intensity = 2.2f;
            l.color = new Color(0.8f, 1f, 0.55f);
            l.shadows = LightShadows.None;
            parts.Add(lure);
            var tail = Group(v, "Tail");
            tail.localPosition = new Vector3(0f, 0f, -0.55f);
            Prim(PrimitiveType.Cube, "TailFin", tail, new Vector3(0f, 0f, -0.2f), new Vector3(0.03f, 0.5f, 0.4f), skin, false);
            parts.Add(tail);
            c.motion = SeaCreature.Motion.Circle;
            c.home = new Vector3(0f, Random.Range(0.4f, 1.4f), 0f);
            c.radius = Random.Range(9f, 12f);
            c.squash = Random.Range(0.8f, 1f);
            c.angularSpeed = Random.Range(7f, 9f) * (i % 2 == 0 ? -1f : 1f);
            c.bob = 0.3f;
            c.bobSpeed = 0.5f;
            c.wiggle = parts.ToArray();
            c.wiggleAxis = Vector3.right;
            c.wiggleAmp = 10f;
            c.wiggleSpeed = 2.2f;
            c.wigglePhaseStep = 1.2f;
            c.respawnDelay = 4f;
            c.displayName = "ANGLERFISH";
            return c;
        }

        /// <summary>タコ (200点): 海底の上で8本の足をうねうね動かす。当てると墨をはく</summary>
        SeaCreature Octopus(Transform sea, int i, Vector3 home, Material bubble)
        {
            var c = NewSeaTarget(sea, $"Octopus_{i}", 200, 0.85f, new Vector3(0f, 0.75f, 0f), new Vector3(0f, 1.95f, 0f), 0.5f, bubble, out var v);
            var skin = Mat("Octopus_Skin", new Color(0.92f, 0.32f, 0.25f), new Color(0.12f, 0.03f, 0.02f), false, 0.45f);
            var spot = Mat("Octopus_Spot", new Color(1f, 0.6f, 0.5f), null, false, 0.4f);
            Prim(PrimitiveType.Sphere, "Head", v, new Vector3(0f, 1.05f, -0.1f), new Vector3(0.8f, 1f, 0.85f), skin, false);
            for (int k = 0; k < 6; k++)
                Prim(PrimitiveType.Sphere, "Spot", v, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.9f, 1.4f), -0.1f) + Random.onUnitSphere * 0.05f + Vector3.back * 0.35f, Vector3.one * 0.08f, spot, false);
            SeaEye(v, new Vector3(-0.2f, 0.72f, 0.28f), 0.18f, new Vector3(-0.2f, 0f, 1f));
            SeaEye(v, new Vector3(0.2f, 0.72f, 0.28f), 0.18f, new Vector3(0.2f, 0f, 1f));
            Prim(PrimitiveType.Cylinder, "Mouth", v, new Vector3(0f, 0.52f, 0.36f), new Vector3(0.12f, 0.06f, 0.12f), spot, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var parts = new List<Transform>();
            for (int k = 0; k < 8; k++) // 足 (3つの節。根元から先へ、うねりが伝わる)
            {
                float a = (k * 45f + 22.5f);
                Transform prev = Group(v, "Arm");
                prev.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.45f, 0.22f);
                prev.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(70f, 0f, 0f); // 外向き・下向き
                for (int s = 0; s < 3; s++)
                {
                    var joint = s == 0 ? prev : Group(prev, "ArmJoint");
                    if (s > 0) joint.localPosition = Vector3.forward * 0.32f;
                    if (s > 0) joint.localRotation = Quaternion.Euler(18f, 0f, 0f); // 先が上へくるんと
                    float w = 0.13f - s * 0.03f;
                    Prim(PrimitiveType.Capsule, "ArmSeg", joint, Vector3.forward * 0.16f, new Vector3(w, 0.19f, w), skin, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    parts.Add(joint);
                    prev = joint;
                }
            }
            c.motion = SeaCreature.Motion.Drift;
            c.home = home;
            c.bob = 0.25f;
            c.bobSpeed = 0.9f;
            c.driftRange = 0.6f;
            c.wiggle = parts.ToArray();
            c.wiggleAxis = Vector3.right;
            c.wiggleAmp = 16f;
            c.wiggleSpeed = 3f;
            c.wigglePhaseStep = 0.55f;
            c.inkMaterial = TransparentMat("Octopus_Ink", new Color(0.05f, 0.03f, 0.08f, 0.85f));
            c.respawnDelay = 4f;
            c.displayName = "OCTOPUS";
            c.transform.localPosition = home;
            c.transform.localRotation = Quaternion.LookRotation(new Vector3(-home.x, 0f, -home.z).normalized); // プレイヤーの方を向く
            return c;
        }

        /// <summary>ウミガメ (250点): 大きなひれをゆったり羽ばたかせて、高い所を大きく回る</summary>
        SeaCreature SeaTurtle(Transform sea, int i, Material bubble)
        {
            var c = NewSeaTarget(sea, $"SeaTurtle_{i}", 250, 0.95f, Vector3.zero, new Vector3(0f, 0.95f, 0f), 0.6f, bubble, out var v);
            var shell = Mat("Turtle_Shell", new Color(0.35f, 0.42f, 0.2f), null, false, 0.5f);
            var plate = Mat("Turtle_Plate", new Color(0.52f, 0.45f, 0.22f), null, false, 0.5f);
            var skin = Mat("Turtle_Skin", new Color(0.55f, 0.62f, 0.45f), null, false, 0.35f);
            Prim(PrimitiveType.Sphere, "Shell", v, Vector3.zero, new Vector3(1.3f, 0.45f, 1.6f), shell, false);
            Prim(PrimitiveType.Sphere, "Belly", v, new Vector3(0f, -0.08f, 0f), new Vector3(1.15f, 0.25f, 1.45f), Mat("Turtle_Belly", new Color(0.9f, 0.85f, 0.6f)), false);
            for (int k = 0; k < 7; k++) // 甲羅の模様
            {
                var p = k == 0 ? Vector3.zero : Quaternion.Euler(0f, k * 60f, 0f) * new Vector3(0f, 0f, 0.42f);
                Prim(PrimitiveType.Sphere, "Plate", v, p + Vector3.up * 0.17f, new Vector3(0.35f, 0.1f, 0.4f), plate, false);
            }
            Prim(PrimitiveType.Sphere, "Head", v, new Vector3(0f, 0.04f, 0.92f), new Vector3(0.36f, 0.3f, 0.42f), skin, false);
            SeaEye(v, new Vector3(-0.13f, 0.1f, 1.04f), 0.08f, new Vector3(-1f, 0f, 0.5f));
            SeaEye(v, new Vector3(0.13f, 0.1f, 1.04f), 0.08f, new Vector3(1f, 0f, 0.5f));
            var parts = new List<Transform>();
            foreach (var (pos, len, s) in new[] { (new Vector3(-0.55f, 0f, 0.45f), 0.95f, -1f), (new Vector3(0.55f, 0f, 0.45f), 0.95f, 1f), (new Vector3(-0.45f, 0f, -0.6f), 0.45f, -1f), (new Vector3(0.45f, 0f, -0.6f), 0.45f, 1f) })
            {
                var fl = Group(v, "Flipper");
                fl.localPosition = pos;
                // ひれは付け根の +X の方へ伸ばし、左は 180 度回して鏡写しにする (同じ角度で揺らすと左右いっしょに上下する)。少し後ろへ流す
                fl.localRotation = Quaternion.Euler(0f, s > 0f ? 25f : 155f, 0f);
                Prim(PrimitiveType.Sphere, "FlipperBlade", fl, new Vector3(len * 0.5f, 0f, 0f), new Vector3(len, 0.06f, len * 0.38f), skin, false);
                parts.Add(fl);
            }
            c.motion = SeaCreature.Motion.Circle;
            c.home = new Vector3(0f, Random.Range(3f, 5.5f), 0f);
            c.radius = Random.Range(11f, 14f);
            c.squash = 0.9f;
            c.angularSpeed = Random.Range(6f, 8f) * (i % 2 == 0 ? 1f : -1f);
            c.bob = 0.6f;
            c.bobSpeed = 0.4f;
            c.wiggle = parts.ToArray();
            c.wiggleAxis = Vector3.forward;
            c.wiggleAmp = 28f;
            c.wiggleSpeed = 2f;
            c.wigglePhaseStep = 0f; // 左右いっしょに羽ばたく
            c.respawnDelay = 4.5f;
            c.displayName = "SEA TURTLE";
            return c;
        }

        /// <summary>サメ (500点): 大きくて速い。遠くを大きく回り、しっぽを左右に振って泳ぐ</summary>
        SeaCreature Shark(Transform sea, int i, Material bubble)
        {
            var c = NewSeaTarget(sea, $"Shark_{i}", 500, 1.1f, new Vector3(0f, 0f, 0.2f), new Vector3(0f, 1.5f, 0f), 0.8f, bubble, out var v);
            var skin = Mat("Shark_Skin", new Color(0.42f, 0.5f, 0.6f), null, false, 0.55f);
            var belly = Mat("Shark_Belly", new Color(0.92f, 0.93f, 0.95f), null, false, 0.5f);
            Prim(PrimitiveType.Sphere, "Body", v, Vector3.zero, new Vector3(0.8f, 0.85f, 3f), skin, false);
            Prim(PrimitiveType.Sphere, "Belly", v, new Vector3(0f, -0.18f, 0.1f), new Vector3(0.66f, 0.5f, 2.6f), belly, false);
            Prim(PrimitiveType.Cube, "Dorsal", v, new Vector3(0f, 0.55f, -0.1f), new Vector3(0.06f, 0.7f, 0.55f), skin, false).transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "Pectoral", v, new Vector3(s * 0.55f, -0.25f, 0.45f), new Vector3(0.7f, 0.04f, 0.35f), skin, false).transform.localRotation = Quaternion.Euler(0f, s * 30f, s * -20f);
                SeaEye(v, new Vector3(s * 0.27f, 0.12f, 1.12f), 0.09f, new Vector3(s, 0f, 0.3f));
                for (int k = 0; k < 3; k++) // えらの筋
                    Prim(PrimitiveType.Cube, "Gill", v, new Vector3(s * 0.38f, 0f, 0.62f - k * 0.1f), new Vector3(0.01f, 0.28f, 0.02f), Mat("Shark_Gill", new Color(0.25f, 0.3f, 0.36f)), false);
            }
            Prim(PrimitiveType.Cube, "Mouth", v, new Vector3(0f, -0.25f, 1.05f), new Vector3(0.35f, 0.04f, 0.15f), Mat("Angler_Mouth", new Color(0.08f, 0.02f, 0.03f)), false);
            var tail = Group(v, "Tail");
            tail.localPosition = new Vector3(0f, 0f, -1.35f);
            Prim(PrimitiveType.Sphere, "TailStem", tail, new Vector3(0f, 0f, -0.2f), new Vector3(0.3f, 0.35f, 0.6f), skin, false);
            Prim(PrimitiveType.Cube, "TailUp", tail, new Vector3(0f, 0.35f, -0.55f), new Vector3(0.05f, 0.8f, 0.3f), skin, false).transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
            Prim(PrimitiveType.Cube, "TailDown", tail, new Vector3(0f, -0.22f, -0.5f), new Vector3(0.05f, 0.5f, 0.25f), skin, false).transform.localRotation = Quaternion.Euler(40f, 0f, 0f);
            c.motion = SeaCreature.Motion.Circle;
            c.home = new Vector3(0f, Random.Range(2f, 5f), 0f);
            c.radius = i == 0 ? 18f : 22f;
            c.squash = Random.Range(0.85f, 1f);
            c.angularSpeed = Random.Range(10f, 13f) * (i % 2 == 0 ? 1f : -1f);
            c.bob = 0.8f;
            c.bobSpeed = 0.35f;
            c.wiggle = new[] { tail };
            c.wiggleAxis = Vector3.up;
            c.wiggleAmp = 22f;
            c.wiggleSpeed = 4f;
            c.respawnDelay = 5f;
            c.displayName = "SHARK";
            return c;
        }

        /// <summary>ほかの潜水艦 (400点): 細長い船体と司令塔、光る丸窓。プロペラを回して泡を引きながら、あたりをさまよう</summary>
        SeaCreature EnemySub(Transform sea, int i, Material bubble)
        {
            Color[] hullColors = { new(0.75f, 0.2f, 0.18f), new(0.25f, 0.55f, 0.35f), new(0.45f, 0.48f, 0.52f) };
            var c = NewSeaTarget(sea, $"EnemySub_{i}", 400, 1.3f, Vector3.zero, new Vector3(0f, 2.05f, 0f), 0.75f, bubble, out var v);
            var hull = Mat($"EnemySub_Hull{i}", hullColors[i % hullColors.Length], null, false, 0.6f);
            var steel = Mat("Sub_Steel", new Color(0.4f, 0.42f, 0.46f), null, false, 0.7f);
            var brass = Mat("Sub_Brass", new Color(0.85f, 0.65f, 0.28f), null, false, 0.8f);
            var glowWin = Mat("EnemySub_Window", new Color(1f, 0.9f, 0.5f), new Color(2.2f, 1.7f, 0.6f));
            Prim(PrimitiveType.Capsule, "Hull", v, Vector3.zero, new Vector3(1.15f, 1.8f, 1.15f), hull, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Tower", v, new Vector3(0f, 0.75f, 0.3f), new Vector3(0.45f, 0.6f, 1f), hull, false);
            Prim(PrimitiveType.Cylinder, "Periscope", v, new Vector3(0f, 1.25f, 0.55f), new Vector3(0.07f, 0.25f, 0.07f), steel, false);
            Prim(PrimitiveType.Cube, "PeriscopeHead", v, new Vector3(0f, 1.48f, 0.62f), new Vector3(0.1f, 0.1f, 0.18f), steel, false);
            Prim(PrimitiveType.Sphere, "NoseWindow", v, new Vector3(0f, 0.1f, 1.68f), new Vector3(0.45f, 0.45f, 0.2f), glowWin, false);
            foreach (float s in new[] { -1f, 1f })
                for (int k = 0; k < 3; k++)
                    Prim(PrimitiveType.Cylinder, "Porthole", v, new Vector3(s * 0.56f, 0.1f, 0.7f - k * 0.6f), new Vector3(0.22f, 0.02f, 0.22f), glowWin, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            for (int k = 0; k < 4; k++) // 後ろの十字のひれ
                Prim(PrimitiveType.Cube, "Fin", v, new Vector3(0f, 0f, -1.55f), new Vector3(1.2f, 0.05f, 0.4f), steel, false).transform.localRotation = Quaternion.Euler(0f, 0f, k * 90f);
            var prop = Group(v, "Propeller");
            prop.localPosition = new Vector3(0f, 0f, -1.9f);
            Prim(PrimitiveType.Sphere, "Hub", prop, Vector3.zero, Vector3.one * 0.15f, brass, false);
            for (int k = 0; k < 3; k++)
                Prim(PrimitiveType.Cube, "Blade", prop, Quaternion.Euler(0f, 0f, k * 120f) * new Vector3(0f, 0.2f, 0f), new Vector3(0.14f, 0.36f, 0.02f), brass, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, k * 120f) * Quaternion.Euler(0f, 30f, 0f);
            var vent = Group(v, "TrailVent");
            vent.localPosition = new Vector3(0f, 0f, -2.1f);
            c.motion = SeaCreature.Motion.Circle;
            c.home = new Vector3(0f, Random.Range(2.5f, 6f), 0f);
            c.radius = 14f + i * 5f;
            c.squash = Random.Range(0.7f, 1f);
            c.angularSpeed = Random.Range(6f, 9f) * (i % 2 == 0 ? -1f : 1f);
            c.bob = 0.5f;
            c.bobSpeed = 0.3f;
            c.spin = new[] { prop };
            c.spinAxis = Vector3.forward;
            c.spinSpeed = 500f;
            c.trailVent = vent;
            c.debrisMaterial = steel;
            c.respawnDelay = 5f;
            c.displayName = "SUBMARINE";
            return c;
        }

        /// <summary>神殿の壺 (150点): 素焼きの壺。当てると割れて、かけらが飛び散る</summary>
        List<SeaCreature> BuildAmphoras(Transform sea, (Transform root, Vector3 chestSpot, List<Vector3> potSpots) temple, Material bubble)
        {
            var clay = Mat("Amphora_Clay", new Color(0.78f, 0.42f, 0.22f), null, false, 0.3f);
            var band = Mat("Amphora_Band", new Color(0.15f, 0.1f, 0.08f), null, false, 0.3f);
            var list = new List<SeaCreature>();
            for (int i = 0; i < temple.potSpots.Count; i++)
            {
                var c = NewSeaTarget(sea, $"Amphora_{i}", 150, 0.5f, new Vector3(0f, 0.5f, 0f), new Vector3(0f, 1.35f, 0f), 0.4f, bubble, out var v);
                Prim(PrimitiveType.Sphere, "Belly", v, new Vector3(0f, 0.42f, 0f), new Vector3(0.5f, 0.7f, 0.5f), clay, false);
                Prim(PrimitiveType.Cylinder, "Neck", v, new Vector3(0f, 0.85f, 0f), new Vector3(0.18f, 0.12f, 0.18f), clay, false);
                Prim(PrimitiveType.Cylinder, "Lip", v, new Vector3(0f, 0.98f, 0f), new Vector3(0.26f, 0.03f, 0.26f), clay, false);
                Prim(PrimitiveType.Cylinder, "Band", v, new Vector3(0f, 0.5f, 0f), new Vector3(0.505f, 0.05f, 0.505f), band, false);
                Prim(PrimitiveType.Cylinder, "Foot", v, new Vector3(0f, 0.06f, 0f), new Vector3(0.2f, 0.06f, 0.2f), clay, false);
                foreach (float s in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cube, "Handle", v, new Vector3(s * 0.2f, 0.78f, 0f), new Vector3(0.05f, 0.25f, 0.04f), clay, false).transform.localRotation = Quaternion.Euler(0f, 0f, s * 20f);
                c.motion = SeaCreature.Motion.Still;
                c.home = temple.potSpots[i];
                c.debrisMaterial = clay;
                c.respawnDelay = 4f;
                c.displayName = "AMPHORA";
                c.transform.localPosition = c.home;
                c.transform.localRotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
                list.Add(c);
            }
            return list;
        }

        // ------------------------------------------------------------------
        // 宝箱
        // ------------------------------------------------------------------

        /// <summary>
        /// 宝箱 (1000点): 木の箱に金の帯と錠前。ふた (後ろのちょうつがいで開く) が開いている間だけ、中の金貨と宝石の山が光って撃てる。
        /// 表 (-Z) がプレイヤーの方を向く
        /// </summary>
        TreasureChest BuildTreasureChest(Transform sea, string name, Vector3 pos, Material bubble)
        {
            var wood = Mat("Chest_Wood", new Color(0.45f, 0.28f, 0.14f), null, false, 0.3f);
            var woodDark = Mat("Chest_WoodDark", new Color(0.3f, 0.18f, 0.09f), null, false, 0.3f);
            var gold = Mat("Chest_Gold", new Color(1f, 0.8f, 0.25f), new Color(0.45f, 0.3f, 0.02f), false, 0.85f);
            var root = Group(sea, name);
            root.localPosition = pos;
            root.localRotation = FacingCenter(pos);
            // 箱 (魚雷が当たるように当たり判定あり)
            Prim(PrimitiveType.Cube, "Box", root, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.7f), wood);
            Prim(PrimitiveType.Cube, "Inside", root, new Vector3(0f, 0.585f, 0f), new Vector3(1f, 0.02f, 0.6f), woodDark, false);
            foreach (float x in new[] { -0.42f, 0.42f })
                Prim(PrimitiveType.Cube, "Band", root, new Vector3(x, 0.3f, 0f), new Vector3(0.08f, 0.62f, 0.72f), gold, false);
            Prim(PrimitiveType.Cube, "Lock", root, new Vector3(0f, 0.45f, -0.36f), new Vector3(0.16f, 0.2f, 0.03f), gold, false);
            // ふた (後ろの上のちょうつがいのまわりに開く)
            var lid = Group(root, "Lid");
            lid.localPosition = new Vector3(0f, 0.6f, 0.35f);
            Prim(PrimitiveType.Cube, "LidBase", lid, new Vector3(0f, 0.06f, -0.35f), new Vector3(1.1f, 0.12f, 0.7f), wood, false);
            Prim(PrimitiveType.Cylinder, "LidTop", lid, new Vector3(0f, 0.12f, -0.35f), new Vector3(0.7f, 0.55f, 0.7f), wood, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float x in new[] { -0.42f, 0.42f })
                Prim(PrimitiveType.Cylinder, "LidBand", lid, new Vector3(x, 0.12f, -0.35f), new Vector3(0.72f, 0.04f, 0.72f), gold, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // 中の宝 (金貨の山と宝石と真珠。開いている間だけふくらんで光る = 的)
            var body = Group(root, "Treasure");
            body.localPosition = new Vector3(0f, 0.55f, 0f);
            var visual = Group(body, "Visual");
            Prim(PrimitiveType.Sphere, "GoldPile", visual, new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.3f, 0.5f), gold, false);
            for (int k = 0; k < 10; k++)
                Prim(PrimitiveType.Cylinder, "Coin", visual, new Vector3(Random.Range(-0.38f, 0.38f), Random.Range(0.1f, 0.2f), Random.Range(-0.2f, 0.2f)), new Vector3(0.12f, 0.01f, 0.12f), gold, false)
                    .transform.localRotation = Quaternion.Euler(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f));
            var gemMats = new[] { Mat("Chest_Ruby", new Color(1f, 0.15f, 0.25f), new Color(1.2f, 0.1f, 0.2f)), Mat("Chest_Emerald", new Color(0.2f, 1f, 0.45f), new Color(0.1f, 1.1f, 0.3f)), Mat("Chest_Sapphire", new Color(0.25f, 0.45f, 1f), new Color(0.15f, 0.3f, 1.3f)) };
            for (int k = 0; k < 5; k++)
                Prim(PrimitiveType.Cube, "Gem", visual, new Vector3(-0.3f + k * 0.15f, 0.22f, Random.Range(-0.12f, 0.12f)), Vector3.one * 0.1f, gemMats[k % 3], false).transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            Prim(PrimitiveType.Sphere, "Pearl", visual, new Vector3(0.25f, 0.2f, 0.1f), Vector3.one * 0.12f, Mat("Chest_Pearl", new Color(1f, 0.97f, 0.95f), new Color(0.35f, 0.33f, 0.3f), false, 0.9f), false);
            var shine = Group(visual, "Shine"); // きらっと光る十字
            shine.localPosition = new Vector3(0.1f, 0.38f, -0.1f);
            shine.gameObject.AddComponent<FaceCenter>();
            var shineMat = Mat("Chest_Shine", new Color(1f, 0.97f, 0.8f), null, true);
            Prim(PrimitiveType.Cube, "RayH", shine, Vector3.zero, new Vector3(0.45f, 0.03f, 0.01f), shineMat, false);
            Prim(PrimitiveType.Cube, "RayV", shine, Vector3.zero, new Vector3(0.03f, 0.45f, 0.01f), shineMat, false);
            var hit = Prim(PrimitiveType.Cube, "Hit", visual, new Vector3(0f, 0.2f, -0.05f), new Vector3(1.3f, 0.9f, 1f), null);
            HitOnly(hit);
            SeaPointSign(visual, 1000, new Vector3(0f, 1.05f, 0f), 0.5f); // ふたが開いている間だけ出る (中の宝といっしょ)
            var target = body.gameObject.AddComponent<Target>();
            target.kind = TargetKind.SubmarineObject;
            target.points = 1000;
            target.manualControl = true;
            target.shrinkOnHit = true;
            target.visual = visual;
            target.SetAvailable(false);
            // 中の金色の明かり (開いている間だけ点く)
            var lg = new GameObject("TreasureGlow");
            lg.transform.SetParent(root, false);
            lg.transform.localPosition = new Vector3(0f, 1f, -0.3f);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 4f;
            l.intensity = 2.5f;
            l.color = new Color(1f, 0.82f, 0.4f);
            l.shadows = LightShadows.None;
            // まわりの砂と、こぼれた金貨
            Prim(PrimitiveType.Sphere, "SandMound", root, new Vector3(0f, 0f, 0f), new Vector3(1.8f, 0.25f, 1.4f), Mat("Sea_Sand", new Color(0.86f, 0.78f, 0.58f), null, false, 0.1f), false);
            for (int k = 0; k < 5; k++)
                Prim(PrimitiveType.Cylinder, "SpilledCoin", root, new Vector3(Random.Range(-0.9f, 0.9f), 0.1f, Random.Range(-0.8f, -0.4f)), new Vector3(0.12f, 0.01f, 0.12f), gold, false);

            var chest = root.gameObject.AddComponent<TreasureChest>();
            chest.lid = lid;
            chest.hitTarget = target;
            chest.glow = l;
            chest.coinMaterial = gold;
            chest.sparkleMaterial = TransparentMat("Chest_Sparkle", new Color(1f, 0.9f, 0.5f, 0.7f));
            chest.bubbleMaterial = bubble;
            return chest;
        }

        // ------------------------------------------------------------------
        // 潜水艦の的 (普通の的が化ける)
        // ------------------------------------------------------------------

        /// <summary>潜水艦の的: 海の底の宝箱と丸い黄色の潜水艦 (LookArt の絵のステッカー。はみ出して大胆に)</summary>
        GameObject SubmarineLook(Transform look) => StickerLook(look, "Submarine", "SUB", new Color(0.25f, 0.12f, 0.02f));
    }
}
