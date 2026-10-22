using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、カジノの斜めのボールのルーレットと、ゲームの上に浮かぶ「SHOOT!」の目印。
    /// </summary>
    public partial class GalleryBuilder
    {
        static readonly int[] RoulettePocketPoints = { 1000, 100, 300, 200, 500, 100, 200, 300, 100, 500, 200, 300 }; // 0 番 (緑) が大当たり

        /// <summary>
        /// 斜めに傾いたボールのルーレット (parent の原点が台の足元の真ん中、表 -Z がプレイヤー向き)。
        /// 木の台の上で、盤がプレイヤーの方へ傾いている。盤の奥の縁の上に、ボールが出てくる金の箱 (BALL) と、そこから縁へ下りる金の管。
        /// 盤は 12 のポケット (緑の 0 = 1000 と、赤・黒が交互) に点数が書いてあり、金の仕切りと真ん中の金の飾りといっしょにゆっくり回る
        /// </summary>
        BallRoulette BuildBallRoulette(Transform parent)
        {
            var root = Group(parent, "BallRoulette");
            var woodDark = Mat("Dart_CabinetDark", new Color(0.22f, 0.11f, 0.05f), null, false, 0.4f);
            var wood = Mat("Roulette_Wood", new Color(0.45f, 0.25f, 0.12f), null, false, 0.5f);
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var red = Mat("Roulette_Red", new Color(0.85f, 0.1f, 0.12f), new Color(0.25f, 0.02f, 0.03f));
            var black = Mat("Roulette_Black", new Color(0.08f, 0.08f, 0.1f), new Color(0.03f, 0.03f, 0.04f));
            var green = Mat("Roulette_Green", new Color(0.1f, 0.6f, 0.25f), new Color(0.03f, 0.2f, 0.06f));
            var chrome = Mat("Casino_Chrome", new Color(0.78f, 0.8f, 0.85f), null, false, 0.92f);

            // 台 (脚と、金のふちのテーブル)
            Prim(PrimitiveType.Cylinder, "Pedestal", root, new Vector3(0f, 0.45f, 0f), new Vector3(0.4f, 0.45f, 0.4f), woodDark, false);
            Prim(PrimitiveType.Cylinder, "Foot", root, new Vector3(0f, 0.04f, 0f), new Vector3(1f, 0.04f, 1f), woodDark, false);
            Prim(PrimitiveType.Cylinder, "TableTop", root, new Vector3(0f, 0.92f, 0f), new Vector3(1.9f, 0.04f, 1.9f), woodDark, false);
            FlatRing(root, "TableTrim", 0.95f, 0.96f, 0.05f, 0.03f, 32, gold);

            // 傾いた盤 (ローカルの XZ 面が盤の面、+Y が表。奥 (+Z) の縁が高い)
            var tilt = Group(root, "Tilt");
            tilt.localPosition = new Vector3(0f, 1.4f, 0.1f);
            tilt.localRotation = Quaternion.Euler(-48f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "Bowl", tilt, new Vector3(0f, -0.04f, 0f), new Vector3(1.75f, 0.04f, 1.75f), woodDark); // (当たり判定はこの盤)
            Prim(PrimitiveType.Cylinder, "Track", tilt, new Vector3(0f, 0f, 0f), new Vector3(1.6f, 0.005f, 1.6f), wood, false);
            FlatRing(tilt, "RimWall", 0.84f, 0.05f, 0.06f, 0.12f, 40, gold);

            // 回る盤: ポケット・仕切り・点数・真ん中の飾り
            var spinner = Group(tilt, "Spinner");
            spinner.localPosition = new Vector3(0f, 0.012f, 0f);
            int n = RoulettePocketPoints.Length;
            float step = 360f / n;
            for (int i = 0; i < n; i++)
            {
                var wedge = new GameObject("Pocket");
                wedge.transform.SetParent(spinner, false);
                wedge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 扇の面 (表 -Z) を上 (+Y) へ
                wedge.AddComponent<MeshFilter>().sharedMesh = WedgeMesh(0.33f, 0.58f, i * step, (i + 1) * step);
                wedge.AddComponent<MeshRenderer>().sharedMaterial = i == 0 ? green : i % 2 == 0 ? black : red;
                // 仕切り (ポケットの境目の金の板)
                var div = Prim(PrimitiveType.Cube, "Divider", spinner, Quaternion.Euler(0f, i * step, 0f) * new Vector3(0f, 0.02f, 0.455f), new Vector3(0.012f, 0.04f, 0.25f), gold, false);
                div.transform.localRotation = Quaternion.Euler(0f, i * step, 0f);
                // 点数 (上を向いた文字。文字の上が外向き)
                float c = (i + 0.5f) * step;
                var label = MakeText(spinner, "PocketPoints", RoulettePocketPoints[i].ToString(), Quaternion.Euler(0f, c, 0f) * new Vector3(0f, 0.008f, 0.455f), i == 0 ? 0.07f : 0.06f, i == 0 ? new Color(1f, 0.95f, 0.5f) : Color.white);
                label.transform.localRotation = Quaternion.Euler(0f, c, 0f) * Quaternion.Euler(90f, 0f, 0f);
            }
            Prim(PrimitiveType.Cylinder, "Turret", spinner, new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 0.03f, 0.6f), gold, false);
            Prim(PrimitiveType.Cylinder, "TurretTop", spinner, new Vector3(0f, 0.09f, 0f), new Vector3(0.18f, 0.06f, 0.18f), gold, false);
            for (int k = 0; k < 4; k++)
            {
                var arm = Prim(PrimitiveType.Cube, "TurretArm", spinner, Quaternion.Euler(0f, k * 90f, 0f) * new Vector3(0f, 0.14f, 0.12f), new Vector3(0.03f, 0.03f, 0.22f), gold, false);
                arm.transform.localRotation = Quaternion.Euler(0f, k * 90f, 0f);
            }

            // ボール
            var ball = Prim(PrimitiveType.Sphere, "Ball", tilt, new Vector3(0f, 0.06f, 0.7f), Vector3.one * 0.07f, Mat("Roulette_Ball", new Color(0.97f, 0.97f, 0.95f), new Color(0.3f, 0.3f, 0.3f), false, 0.9f), false);

            // ボールが出てくる金の箱と、盤の縁へ下りる金の管 (どこからか補給されるように)
            var path = new[] { new Vector3(0f, 0.95f, 1.08f), new Vector3(0f, 0.42f, 1f), new Vector3(0f, 0.14f, 0.88f), new Vector3(0f, 0.06f, 0.72f) };
            for (int i = 1; i < path.Length; i++) DomeLine(tilt, "Chute", path[i - 1], path[i], 0.1f, gold);
            var hopper = Group(tilt, "Hopper");
            hopper.localPosition = path[0] + new Vector3(0f, 0.12f, 0f);
            hopper.localRotation = Quaternion.Euler(48f, 0f, 0f); // (箱はまっすぐ立てる)
            Prim(PrimitiveType.Cube, "HopperBox", hopper, Vector3.zero, new Vector3(0.36f, 0.3f, 0.3f), gold, false);
            Prim(PrimitiveType.Cube, "HopperFace", hopper, new Vector3(0f, 0f, -0.152f), new Vector3(0.3f, 0.24f, 0.01f), black, false);
            MakeText(hopper, "HopperLabel", "BALL", new Vector3(0f, 0f, -0.16f), 0.09f, new Color(1f, 0.85f, 0.35f));

            // 台の前のふちの札 (大当たりの点数)
            var plate = Group(root, "PayPlate");
            plate.localPosition = new Vector3(0f, 0.8f, -0.97f);
            Prim(PrimitiveType.Cube, "PlateBoard", plate, Vector3.zero, new Vector3(0.75f, 0.22f, 0.02f), black, false);
            MakeText(plate, "PlateText", "GREEN 0 = 1000!", new Vector3(0f, 0f, -0.015f), 0.07f, new Color(0.5f, 1f, 0.6f));

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.BallRoulette;
            target.points = 0;
            target.manualControl = true;
            target.stayUpOnHit = true;
            target.multiHitInterval = 0.3f;
            target.hitWobbleDeg = 0f;
            target.visual = Group(root, "HitMarker");
            target.SetAvailable(false);
            var roulette = root.gameObject.AddComponent<BallRoulette>();
            roulette.target = target;
            roulette.tilt = tilt;
            roulette.spinner = spinner;
            roulette.ball = ball.transform;
            roulette.chutePath = path;
            roulette.rimRadius = 0.72f;
            roulette.pocketRadius = 0.45f;
            roulette.pocketPoints = (int[])RoulettePocketPoints.Clone();
            roulette.wheelSpeed = 40f;
            ball.SetActive(false);
            return roulette;
        }

        /// <summary>
        /// ゲームの上に浮かぶ「SHOOT!」の目印 (光る文字と、下を指して弾む矢印。いつもプレイヤーの方を向く)。撃てる時だけ出る (CasinoHint)。スロットはレバーの玉の真上に置いて、玉を指す
        /// </summary>
        CasinoHint CasinoShootHint(Transform parent, Vector3 pos, Target target, SlotMachine slot, BallRoulette roulette, float size = 1f)
        {
            var root = Group(parent, "ShootHint");
            root.localPosition = pos;
            root.gameObject.AddComponent<FaceCenter>();
            var visual = Group(root, "HintVisual");
            visual.localScale = Vector3.one * size;
            var glow = Mat("Casino_HintGlow", new Color(1f, 0.85f, 0.3f), new Color(2.4f, 1.8f, 0.5f), true);
            MakeText(visual, "HintText", "SHOOT!", new Vector3(0f, 0.28f, 0f), 0.3f, new Color(1f, 0.9f, 0.4f));
            var arrow = Group(visual, "HintArrow");
            arrow.localPosition = new Vector3(0f, -0.05f, 0f);
            ShapeObj(Shape.Triangle, arrow, "ArrowHead", Vector3.zero, 0.32f, glow, false, 0.02f).transform.localRotation = Quaternion.Euler(0f, 0f, 180f); // 下を指す
            var hint = root.gameObject.AddComponent<CasinoHint>();
            hint.target = target;
            hint.slot = slot;
            hint.roulette = roulette;
            hint.visual = visual.gameObject;
            hint.arrow = arrow;
            return hint;
        }
    }
}
