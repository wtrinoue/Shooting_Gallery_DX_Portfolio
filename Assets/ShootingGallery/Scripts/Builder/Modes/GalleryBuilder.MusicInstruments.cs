using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、音楽モードの合奏団の楽器 (演奏者はいない)。オーケストラの並び方を参考に、指揮台を中心に扇形に置く:
    ///   手前: 弦楽器 (左の手前に第1バイオリン・その奥に第2バイオリン、指揮者の前にビオラ、右の手前にチェロ・右の外側にコントラバス)。
    ///   真ん中の段: 木管楽器 (内側の輪にピッコロ・フルート / オーボエ、外側の輪にクラリネット・バスクラリネット / ファゴット・コントラファゴット)。
    ///   その奥の段: 金管楽器 (ホルン・トランペット・ピッコロトランペット・トロンボーン・バストロンボーン・ユーフォニアム・チューバ)。
    ///   いちばん奥: 打楽器 (ティンパニ4つ・大太鼓・小太鼓・シンバル・ドラ・木琴・鉄琴・チューブラーベル)。
    /// 同じ仲間の楽器は大きさを変えて作る (バイオリン < ビオラ < チェロ < コントラバスなど)。並びはユーザーの配置図どおり (BuildOrchestra)。
    /// 楽器ごとに MusicInstrument を付け、根元の -Z がプレイヤーの方を向く
    /// </summary>
    public partial class GalleryBuilder
    {
        List<MusicInstrument> BuildOrchestra(Transform hall)
        {
            var orchestra = Group(hall, "Orchestra");
            var list = new List<MusicInstrument>();

            // 角度 deg は指揮台から見た向き (0 = 真後ろの奥、マイナス = 客席から見て左、±90 = 真横)、r は指揮台からの距離 (m)。
            // 客席から見た並び (ユーザーの配置図どおり):
            //   左の手前 = 第1バイオリン、その奥 = 第2バイオリン、指揮者のすぐ前 = ビオラ、右の手前 = チェロ、右の外側 = コントラバス、
            //   真ん中の内側の輪 = フルート (左)・オーボエ (右)、その外の輪 = クラリネット (左)・ファゴット (右)、
            //   さらに外の輪 = ホルン・トランペット・トロンボーン・チューバ (左から)、いちばん奥 = 打楽器
            const float RViola = 2.6f, RFlute = 4.6f, RClarinet = 6.1f, RBrass = 7.7f, RPerc = 9.6f; // ひな壇 (RiserR) の各段の真ん中あたり

            // ---- 弦楽器 ----
            list.Add(BowedString(orchestra, "1ST VIOLIN", -81f, 2.3f, 0.7f, 0, 500, 79));
            list.Add(BowedString(orchestra, "1ST VIOLIN", -85f, 3.7f, 0.7f, 0, 500, 76));
            list.Add(BowedString(orchestra, "1ST VIOLIN", -77f, 5.1f, 0.7f, 0, 500, 74));
            list.Add(BowedString(orchestra, "1ST VIOLIN", -85f, 6.5f, 0.7f, 0, 500, 72));
            list.Add(BowedString(orchestra, "1ST VIOLIN", -76f, 7.6f, 0.7f, 0, 500, 71));
            list.Add(BowedString(orchestra, "2ND VIOLIN", -62f, 2.9f, 0.7f, 0, 500, 69));
            list.Add(BowedString(orchestra, "2ND VIOLIN", -71f, 4.4f, 0.7f, 0, 500, 67));
            list.Add(BowedString(orchestra, "2ND VIOLIN", -54f, 4.4f, 0.7f, 0, 500, 66));
            list.Add(BowedString(orchestra, "2ND VIOLIN", -69f, 6f, 0.7f, 0, 500, 64));
            list.Add(BowedString(orchestra, "2ND VIOLIN", -55f, 6.1f, 0.7f, 0, 500, 62));
            list.Add(BowedString(orchestra, "VIOLA", -30f, RViola, 0.82f, 1, 400, 62));
            list.Add(BowedString(orchestra, "VIOLA", 0f, RViola, 0.82f, 1, 400, 60));
            list.Add(BowedString(orchestra, "VIOLA", 30f, RViola, 0.82f, 1, 400, 57));
            list.Add(BowedString(orchestra, "CELLO", 56f, 2.7f, 1.4f, 2, 300, 50));
            list.Add(BowedString(orchestra, "CELLO", 84f, 2.7f, 1.4f, 2, 300, 48));
            list.Add(BowedString(orchestra, "CELLO", 73f, 4.2f, 1.4f, 2, 300, 45)); // (ひな壇の端 65° から離す。前は 70°)
            list.Add(BowedString(orchestra, "CELLO", 59f, 5.7f, 1.4f, 2, 300, 43));
            list.Add(BowedString(orchestra, "CELLO", 82f, 5.7f, 1.4f, 2, 300, 41));
            // コントラバスは床の上 (ひな壇の端 RiserMaxDeg 65° より外)。段の側面にめり込まないよう、端から 1m 以上離す (前は 67° で 0.26m しかなく当たっていた)
            list.Add(BowedString(orchestra, "CONTRABASS", 74f, 7.5f, 2.1f, 3, 200, 36));
            list.Add(BowedString(orchestra, "CONTRABASS", 86f, 7.5f, 2.1f, 3, 200, 33));
            list.Add(BowedString(orchestra, "CONTRABASS", 80f, 9.1f, 2.1f, 3, 200, 31));

            // ---- 木管楽器 (内側の輪: フルート | オーボエ、外側の輪: クラリネット | ファゴット) ----
            list.Add(Flute(orchestra, "PICCOLO", -40f, RFlute, 0.36f, 800, 86));
            list.Add(Flute(orchestra, "FLUTE", -25f, RFlute, 0.7f, 500, 76));
            list.Add(Flute(orchestra, "FLUTE", -10f, RFlute, 0.7f, 500, 74));
            list.Add(Reed(orchestra, "OBOE", 10f, RFlute, 0.65f, 0.034f, 500, 69, false));
            list.Add(Reed(orchestra, "OBOE", 25f, RFlute, 0.65f, 0.034f, 500, 67, false));
            list.Add(Reed(orchestra, "OBOE", 40f, RFlute, 0.65f, 0.034f, 500, 65, false));
            list.Add(Reed(orchestra, "CLARINET", -44f, RClarinet, 0.68f, 0.045f, 500, 64, true));
            list.Add(Reed(orchestra, "CLARINET", -31f, RClarinet, 0.68f, 0.045f, 500, 62, true));
            list.Add(BassClarinet(orchestra, -17f, RClarinet));
            list.Add(Bassoon(orchestra, "BASSOON", 9f, RClarinet, 1f, 300, 46));
            list.Add(Bassoon(orchestra, "BASSOON", 22f, RClarinet, 1f, 300, 43));
            list.Add(Bassoon(orchestra, "CONTRABASSOON", 36f, RClarinet, 1.3f, 200, 34));

            // ---- 金管楽器 (左から ホルン | トランペット | トロンボーン | チューバ) ----
            list.Add(Horn(orchestra, -54f, RBrass, 400, 57));
            list.Add(Horn(orchestra, -44f, RBrass, 400, 55));
            list.Add(Horn(orchestra, -34f, RBrass, 400, 53));
            list.Add(Trumpet(orchestra, "TRUMPET", -22f, RBrass, 0.55f, 400, 67));
            list.Add(Trumpet(orchestra, "TRUMPET", -12f, RBrass, 0.55f, 400, 64));
            list.Add(Trumpet(orchestra, "PICCOLO TRUMPET", -3f, RBrass, 0.38f, 700, 76));
            list.Add(Trombone(orchestra, "TROMBONE", 9f, RBrass, 1f, 300, 53, false));
            list.Add(Trombone(orchestra, "TROMBONE", 20f, RBrass, 1f, 300, 50, false));
            list.Add(Trombone(orchestra, "BASS TROMBONE", 31f, RBrass, 1.15f, 300, 43, true));
            list.Add(Tuba(orchestra, "EUPHONIUM", 42f, RBrass, 0.72f, 300, 46));
            list.Add(Tuba(orchestra, "TUBA", 53f, RBrass + 0.2f, 1f, 200, 34));

            // ---- 打楽器 (いちばん奥の輪。真ん中にティンパニ) ----
            list.Add(TubularBells(orchestra, -48f, RPerc));
            list.Add(Xylophone(orchestra, -40f, RPerc, true));
            list.Add(Xylophone(orchestra, -30f, RPerc, false));
            float[] timpDeg = { -19f, -10f, -1f, 8f };
            float[] timpD = { 0.84f, 0.77f, 0.7f, 0.63f };
            int[] timpMidi = { 41, 43, 45, 48 };
            for (int i = 0; i < 4; i++) list.Add(Timpani(orchestra, timpDeg[i], RPerc, timpD[i], timpMidi[i]));
            list.Add(BassDrum(orchestra, 18f, RPerc));
            list.Add(SnareDrum(orchestra, 28f, RPerc - 0.3f));
            list.Add(Cymbals(orchestra, 37f, RPerc));
            list.Add(Gong(orchestra, 46f, RPerc + 0.2f));
            return list;
        }

        // ------------------------------------------------------------------
        // 共通
        // ------------------------------------------------------------------

        const float InstrumentScale = 1.2f; // 楽器の大きさ (形は 1 の大きさで作り、根元ごと拡大する)

        Material InstBrass => Mat("Concert_Brass", new Color(1f, 0.8f, 0.32f), new Color(0.24f, 0.16f, 0.035f), false, 0.9f);
        Material InstSilver => Mat("Concert_Silver", new Color(0.85f, 0.86f, 0.9f), new Color(0.11f, 0.11f, 0.12f), false, 0.9f);
        Material InstBlack => Mat("Concert_InstBlack", new Color(0.05f, 0.05f, 0.06f), null, false, 0.75f);
        Material InstPearl => Mat("Concert_Pearl", new Color(0.97f, 0.95f, 0.9f), null, false, 0.8f);

        /// <summary>
        /// 楽器の土台: 根元 (Target・MusicInstrument・椅子) → Wobble (撃たれるとぐらっと揺れる) → Pivot (拍に合わせて揺れる。ここに楽器を作る)。
        /// ソロの時に出す光の粒と点数の札 (signY の高さ) も作る。hitCenter / hitSize は Pivot のローカルの当たり判定の箱
        /// </summary>
        MusicInstrument NewInstrument(Transform parent, string displayName, Vector3 pos, ConcertMusic.Voice voice, int midi, int points, float signY,
                                      Vector3 hitCenter, Vector3 hitSize, bool chair, out Transform pivot)
        {
            var root = Group(parent, displayName.Replace(' ', '_'));
            root.localPosition = pos;
            root.localScale = Vector3.one * InstrumentScale; // 楽器・椅子・ソロの札をまとめて一回り大きく
            var flat = new Vector3(pos.x, 0f, pos.z + HallShiftZ); // プレイヤーは、ホールの原点から HallShiftZ だけ手前
            if (flat.sqrMagnitude > 0.01f) root.localRotation = Quaternion.LookRotation(flat.normalized); // 表 (-Z) がプレイヤーの方

            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.MusicInstrument;
            target.points = 0;
            target.stayUpOnHit = true;
            target.hitWobbleDeg = 8f;
            target.multiHitInterval = 0.35f;
            var wobble = Group(root, "Wobble");
            target.visual = wobble;
            pivot = Group(wobble, "Pivot");
            var hit = Prim(PrimitiveType.Cube, "Hit", pivot, hitCenter, hitSize * 1.2f, null); // 見た目より少し大きめ (ソロの間はさらに MusicInstrument.soloHitScale 倍)
            HitOnly(hit);
            if (chair) InstrumentChair(root);

            // 足元の光の輪: 暗いステージの上で、楽器の輪郭が1つずつくっきり浮かぶように (外ほど薄い3重の円で、ぼかして見せる)
            float pool = Mathf.Clamp(Mathf.Max(hitSize.x, hitSize.z) * 0.6f, 0.45f, 0.8f);
            var poolMat = TransparentMat("Concert_InstPool", new Color(1f, 0.85f, 0.55f, 0.13f));
            for (int i = 0; i < 3; i++)
            {
                float r = pool * (1f - i * 0.25f);
                Prim(PrimitiveType.Cylinder, "Pool", root, new Vector3(0f, 0.004f + i * 0.002f, 0f), new Vector3(r * 2f, 0.001f, r * 2f), poolMat, false);
            }

            // ソロの印: まわりを回る光の粒と、点数の札
            var mark = Group(root, "SoloMark");
            var glow = Mat("Solo_Glow", new Color(1f, 0.9f, 0.5f), new Color(2.2f, 1.8f, 0.7f), true);
            var orbit = Group(mark, "Sparkles");
            orbit.localPosition = Vector3.up * signY * 0.55f;
            var rot = orbit.gameObject.AddComponent<Rotator>();
            rot.axis = Vector3.up;
            rot.speed = 100f;
            float ring = Mathf.Clamp(hitSize.x * 0.75f, 0.45f, 1f);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                Prim(PrimitiveType.Sphere, "Sparkle", orbit, new Vector3(Mathf.Sin(a) * ring, Mathf.Sin(a * 3f) * 0.12f, Mathf.Cos(a) * ring), Vector3.one * 0.07f, glow, false);
            }
            // 輪の五線譜: 楽器のまわりをぐるりと囲む5本の線 (ゆるく波打つ)。その上で音符が拍に合わせて跳ねる (動きは MusicInstrument)
            var soloStaff = Group(mark, "SoloStaff");
            soloStaff.localPosition = Vector3.up * signY * 0.42f;
            float staffR = ring + 0.25f;
            const int LoopPts = 48;
            const float SoloLineGap = 0.07f;
            var soloLineMat = Mat("Solo_StaffLine", new Color(1f, 0.9f, 0.6f), null, true);
            for (int k = 0; k < 5; k++)
            {
                var lr = StaffLine(soloStaff, $"SoloLine{k}", soloLineMat, 0.012f);
                lr.loop = true;
                lr.positionCount = LoopPts;
                for (int i = 0; i < LoopPts; i++)
                {
                    float a = i * Mathf.PI * 2f / LoopPts;
                    lr.SetPosition(i, new Vector3(Mathf.Sin(a) * staffR, (k - 2) * SoloLineGap + Mathf.Sin(a * 2f) * 0.08f, Mathf.Cos(a) * staffR));
                }
            }
            var notePalette = NotePalette();
            var soloNotes = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 0.5f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var at = Group(soloStaff, $"SoloNote{i}");
                at.localPosition = dir * staffR + Vector3.up * ((i % 3 - 1) * SoloLineGap + Mathf.Sin(a * 2f) * 0.08f); // 線か間の上
                at.localRotation = Quaternion.LookRotation(-dir); // 表 (-Z) が外側
                var hop = Group(at, "Hop");
                var noteMat = notePalette[(i * 2 + 1) % notePalette.Length];
                var head = Prim(PrimitiveType.Sphere, "Head", hop, Vector3.zero, new Vector3(0.11f, 0.08f, 0.07f), noteMat, false);
                head.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
                Prim(PrimitiveType.Cylinder, "Stem", hop, new Vector3(0.048f, 0.11f, 0f), new Vector3(0.018f, 0.11f, 0.018f), noteMat, false);
                if (i % 2 == 1) // 8分音符は旗つき
                    Prim(PrimitiveType.Cube, "Flag", hop, new Vector3(0.075f, 0.18f, 0f), new Vector3(0.018f, 0.09f, 0.015f), noteMat, false).transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                soloNotes.Add(hop);
            }
            var sign = Group(mark, "Sign");
            sign.localPosition = Vector3.up * signY;
            sign.gameObject.AddComponent<FaceCenter>();
            Prim(PrimitiveType.Cube, "SignBoard", sign, new Vector3(0f, 0f, 0.03f), new Vector3(0.62f, 0.34f, 0.03f), Mat("Solo_Board", new Color(0.12f, 0.03f, 0.06f), new Color(0.05f, 0.01f, 0.02f)), false);
            Prim(PrimitiveType.Cube, "SignFrame", sign, new Vector3(0f, 0f, 0.045f), new Vector3(0.68f, 0.4f, 0.02f), Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f)), false);
            MakeText(sign, "SoloText", "SOLO", new Vector3(0f, 0.09f, 0f), 0.08f, new Color(1f, 0.6f, 0.75f));
            var label = MakeText(sign, "Points", points.ToString(), new Vector3(0f, -0.04f, 0f), 0.16f, new Color(1f, 0.88f, 0.35f));
            mark.gameObject.SetActive(false);

            var inst = root.gameObject.AddComponent<MusicInstrument>();
            inst.displayName = displayName;
            inst.hitTarget = target;
            inst.points = points;
            inst.voice = voice;
            inst.midi = midi;
            inst.pivot = pivot;
            inst.soloMark = mark.gameObject;
            inst.soloLabel = label;
            inst.soloStaff = soloStaff;
            inst.soloNotes = soloNotes.ToArray();
            inst.soloMaterials = SoloNeonMats();
            inst.hitBox = hit.transform;
            inst.aimHeight = signY;
            inst.phaseBeats = Mathf.Repeat(pos.x * 0.09f, 2f) * 0.25f; // 横に並んだ楽器が、波のように少しずつずれて揺れる
            return inst;
        }

        // ソロの楽器の蛍光色 (拍ごとに入れ替わる)。形の陰影は残しつつ、強く自分で光らせて、暗いステージでもひと目で分かるように
        Material[] SoloNeonMats()
        {
            var colors = new[] { new Color(1f, 1f, 0.15f), new Color(1f, 0.15f, 0.75f), new Color(0.1f, 1f, 1f), new Color(0.35f, 1f, 0.2f) };
            var mats = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++) mats[i] = Mat($"Solo_Neon{i}", colors[i], colors[i] * 1.2f, false, 0.8f);
            return mats;
        }

        // 演奏者の椅子 (楽器の後ろ。黒い椅子)
        void InstrumentChair(Transform root)
        {
            var c = Group(root, "Chair");
            c.localPosition = new Vector3(0f, 0f, 0.38f);
            var black = Mat("Concert_Black", new Color(0.04f, 0.04f, 0.05f), null, false, 0.7f);
            Prim(PrimitiveType.Cube, "Seat", c, new Vector3(0f, 0.46f, 0f), new Vector3(0.44f, 0.05f, 0.42f), black, false);
            Prim(PrimitiveType.Cube, "Back", c, new Vector3(0f, 0.72f, 0.2f), new Vector3(0.44f, 0.42f, 0.04f), black, false);
            Prim(PrimitiveType.Cube, "LegsFront", c, new Vector3(0f, 0.22f, -0.18f), new Vector3(0.4f, 0.44f, 0.03f), black, false);
            Prim(PrimitiveType.Cube, "LegsBack", c, new Vector3(0f, 0.22f, 0.18f), new Vector3(0.4f, 0.44f, 0.03f), black, false);
        }

        /// <summary>a から b までの丸い管 (円柱)</summary>
        GameObject Pipe(Transform parent, string name, Vector3 a, Vector3 b, float diameter, Material mat)
        {
            var dir = b - a;
            float len = dir.magnitude;
            var g = Prim(PrimitiveType.Cylinder, name, parent, (a + b) * 0.5f, new Vector3(diameter, Mathf.Max(0.001f, len / 2f), diameter), mat, false);
            if (len > 0.0001f) g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
            return g;
        }

        /// <summary>点を順につないだ管 (つなぎ目に玉を入れて、曲がり目をなめらかに見せる)</summary>
        void PipePath(Transform parent, string name, Vector3[] pts, float diameter, Material mat)
        {
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Pipe(parent, name, pts[i], pts[i + 1], diameter, mat);
                if (i > 0) Prim(PrimitiveType.Sphere, name + "Joint", parent, pts[i], Vector3.one * diameter, mat, false);
            }
        }

        /// <summary>巻いた管: center のまわりに、from (center からの向きと半径) を normal 軸で deg0 → deg1 度回した弧</summary>
        void PipeArc(Transform parent, string name, Vector3 center, Vector3 normal, Vector3 from, float deg0, float deg1, int segs, float diameter, Material mat)
        {
            var pts = new Vector3[segs + 1];
            for (int i = 0; i <= segs; i++) pts[i] = center + Quaternion.AngleAxis(Mathf.Lerp(deg0, deg1, i / (float)segs), normal) * from;
            PipePath(parent, name, pts, diameter, mat);
        }

        /// <summary>広がるベル (朝顔): pos から dir の向きへ、半径 r0 から r1 へ広がる長さ len の筒</summary>
        Transform Flare(Transform parent, string name, Vector3 pos, Vector3 dir, float r0, float r1, float len, Material mat)
        {
            // 同じ形のメッシュを使い回せるように、広がり方 (r1 / r0) を 0.5 きざみにした単位の円すいを、大きさで伸ばす
            float ratio = Mathf.Max(0.5f, Mathf.Round(r1 / Mathf.Max(0.001f, r0) * 2f) / 2f);
            var t = MeshPart(parent, name, ConeMesh(1f, ratio, 1f), mat);
            t.localPosition = pos;
            t.localRotation = Quaternion.LookRotation(dir.normalized);
            t.localScale = new Vector3(r0, r0, len);
            return t;
        }

        // ------------------------------------------------------------------
        // 弦楽器 (kind: 0 = バイオリン / 1 = ビオラ (手に持つ) / 2 = チェロ (椅子に座って立てる) / 3 = コントラバス (立てる))
        // L = 胴から渦巻きまでの長さの目安。形は同じで大きさだけ違う
        // ------------------------------------------------------------------

        MusicInstrument BowedString(Transform parent, string name, float deg, float r, float L, int kind, int points, int midi)
        {
            bool upright = kind >= 2;
            var spot = OrchestraSpot(deg, r);
            float signY = kind == 3 ? 2.6f : upright ? 2.1f : 1.9f;
            Vector3 hitC = upright ? new Vector3(0f, 0.43f * L, -0.12f) : new Vector3(0f, 1.2f, -0.05f);
            Vector3 hitS = upright ? new Vector3(0.48f * L, 0.95f * L, 0.35f) : new Vector3(0.95f * L + 0.1f, 0.4f, 0.55f);
            var inst = NewInstrument(parent, name, spot, ConcertMusic.Voice.Strings, midi, points, signY, hitC, hitS, kind != 3, out var pivot);

            var wood = kind <= 1 ? Mat("Concert_Violin", new Color(0.66f, 0.29f, 0.08f), new Color(0.11f, 0.045f, 0.008f), false, 0.75f)
                                 : Mat("Concert_Cello", new Color(0.46f, 0.18f, 0.06f), new Color(0.09f, 0.035f, 0.008f), false, 0.75f);
            var body = Group(pivot, "Body");
            if (upright)
            {
                float pin = kind == 3 ? 0.22f : 0.12f;
                body.localPosition = new Vector3(0f, 0.27f * L + pin, -0.12f);
                body.localRotation = Quaternion.Euler(kind == 3 ? 8f : 12f, 0f, kind == 3 ? -8f : -10f);
            }
            else
            {
                // 肩に乗せた向き: 棹が客席から見て右、表が上を向く
                body.localPosition = new Vector3(0f, 1.2f, -0.05f);
                body.localRotation = Quaternion.Euler(0f, 20f, 0f) * Quaternion.Euler(60f, 0f, 0f) * Quaternion.Euler(0f, 0f, -90f);
            }
            StringBody(body, L, wood, upright ? (kind == 3 ? 0.22f : 0.12f) : 0f);

            // 弓: 駒と指板の間で弦をこすり、拍に合わせて行ったり来たりする
            var bow = Group(body, "Bow");
            bow.localPosition = new Vector3(0f, -0.04f * L, -0.08f * L);
            bow.localRotation = Quaternion.Euler(0f, 0f, upright ? -12f : -8f);
            float bowLen = (upright ? 0.55f : 1.1f) * L;
            float stick = upright ? 0.014f : 0.009f;
            Pipe(bow, "BowStick", new Vector3(-bowLen / 2f, 0.02f * L, 0f), new Vector3(bowLen / 2f, 0.02f * L, 0f), stick, Mat("Concert_BowWood", new Color(0.3f, 0.12f, 0.05f), null, false, 0.6f));
            Prim(PrimitiveType.Cube, "BowHair", bow, Vector3.zero, new Vector3(bowLen * 0.95f, 0.006f, 0.012f), Mat("Concert_BowHair", new Color(0.95f, 0.93f, 0.85f)), false);
            Prim(PrimitiveType.Cube, "BowFrog", bow, new Vector3(-bowLen / 2f + 0.03f, 0.01f * L, 0f), new Vector3(0.05f, 0.04f, 0.025f), InstBlack, false);
            inst.bow = bow;
            inst.bowStroke = new Vector3(bowLen * 0.3f, 0f, 0f);
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = upright ? 3.5f : 6f;
            inst.bob = 0.03f;
            return inst;
        }

        // 弦楽器の胴・棹・渦巻き・駒・弦 (原点が胴の真ん中、棹が +Y、表が -Z)。pin > 0 ならエンドピン
        void StringBody(Transform b, float L, Material wood, float pin)
        {
            var ebony = Mat("Concert_Ebony", new Color(0.06f, 0.05f, 0.05f), null, false, 0.6f);
            var maple = Mat("Concert_Maple", new Color(0.88f, 0.72f, 0.46f));
            Prim(PrimitiveType.Sphere, "LowerBout", b, new Vector3(0f, -0.12f * L, 0f), new Vector3(0.36f * L, 0.3f * L, 0.09f * L), wood, false);
            Prim(PrimitiveType.Sphere, "UpperBout", b, new Vector3(0f, 0.1f * L, 0f), new Vector3(0.29f * L, 0.24f * L, 0.085f * L), wood, false);
            Prim(PrimitiveType.Cube, "Waist", b, new Vector3(0f, -0.01f * L, 0f), new Vector3(0.2f * L, 0.14f * L, 0.075f * L), wood, false);
            Prim(PrimitiveType.Cube, "Fingerboard", b, new Vector3(0f, 0.27f * L, -0.05f * L), new Vector3(0.05f * L, 0.44f * L, 0.018f * L), ebony, false);
            Prim(PrimitiveType.Cube, "Neck", b, new Vector3(0f, 0.38f * L, -0.025f * L), new Vector3(0.045f * L, 0.2f * L, 0.04f * L), wood, false);
            Prim(PrimitiveType.Cube, "Pegbox", b, new Vector3(0f, 0.5f * L, -0.02f * L), new Vector3(0.05f * L, 0.1f * L, 0.045f * L), wood, false);
            Prim(PrimitiveType.Sphere, "Scroll", b, new Vector3(0f, 0.565f * L, -0.02f * L), Vector3.one * 0.06f * L, wood, false);
            for (int i = 0; i < 2; i++)
                Pipe(b, "Peg", new Vector3(-0.045f * L, (0.47f + i * 0.05f) * L, -0.02f * L), new Vector3(0.045f * L, (0.47f + i * 0.05f) * L, -0.02f * L), 0.012f * L, ebony);
            Prim(PrimitiveType.Cube, "Bridge", b, new Vector3(0f, -0.1f * L, -0.056f * L), new Vector3(0.13f * L, 0.035f * L, 0.012f * L), maple, false);
            Prim(PrimitiveType.Cube, "Tailpiece", b, new Vector3(0f, -0.2f * L, -0.05f * L), new Vector3(0.065f * L, 0.11f * L, 0.012f * L), ebony, false);
            Prim(PrimitiveType.Cube, "Strings", b, new Vector3(0f, 0.12f * L, -0.063f * L), new Vector3(0.035f * L, 0.66f * L, 0.003f * L), InstSilver, false);
            foreach (float x in new[] { -0.085f, 0.085f })
                Prim(PrimitiveType.Cube, "FHole", b, new Vector3(x * L, -0.08f * L, -0.044f * L), new Vector3(0.012f * L, 0.1f * L, 0.004f * L), ebony, false);
            if (pin > 0f) Pipe(b, "Endpin", new Vector3(0f, -0.27f * L, 0f), new Vector3(0f, -0.27f * L - pin - 0.03f, 0f), 0.014f * L, InstSilver);
        }

        // ------------------------------------------------------------------
        // 木管楽器
        // ------------------------------------------------------------------

        // フルート / ピッコロ: 横に構えた銀の管 (L = 長さ)
        MusicInstrument Flute(Transform parent, string name, float deg, float r, float L, int points, int midi)
        {
            var inst = NewInstrument(parent, name, OrchestraSpot(deg, r), ConcertMusic.Voice.Flute, midi, points, 1.85f, new Vector3(0f, 1.28f, -0.12f), new Vector3(L + 0.25f, 0.3f, 0.3f), true, out var pivot);
            var f = Group(pivot, "Flute");
            f.localPosition = new Vector3(0f, 1.28f, -0.12f);
            f.localRotation = Quaternion.Euler(0f, 15f, -8f);
            float d = L < 0.5f ? 0.02f : 0.026f;
            Pipe(f, "Tube", new Vector3(-L / 2f, 0f, 0f), new Vector3(L / 2f, 0f, 0f), d, InstSilver);
            Prim(PrimitiveType.Sphere, "Cap", f, new Vector3(-L / 2f, 0f, 0f), Vector3.one * d * 1.2f, InstSilver, false);
            Prim(PrimitiveType.Cube, "LipPlate", f, new Vector3(-L / 2f + 0.07f, d * 0.55f, 0f), new Vector3(0.05f, 0.01f, 0.03f), InstSilver, false);
            for (int i = 0; i < 6; i++)
                Prim(PrimitiveType.Sphere, "Key", f, new Vector3(-L / 2f + (0.3f + i * 0.1f) * L, d * 0.6f, 0f), Vector3.one * d * 0.9f, InstPearl, false);
            inst.swayAxis = Vector3.right;
            inst.swayDeg = 5f;
            inst.bob = 0.025f;
            return inst;
        }

        // オーボエ / クラリネット: 黒い管を斜め前に向けて構える (L = 長さ、bell = ベルの半径)
        MusicInstrument Reed(Transform parent, string name, float deg, float r, float L, float bell, int points, int midi, bool clarinet)
        {
            var inst = NewInstrument(parent, name, OrchestraSpot(deg, r), ConcertMusic.Voice.Reed, midi, points, 1.85f, new Vector3(0f, 1f, -0.2f), new Vector3(0.3f, L + 0.15f, 0.4f), true, out var pivot);
            var c = Group(pivot, "Tube");
            c.localPosition = new Vector3(0f, 1f, -0.15f);
            c.localRotation = Quaternion.Euler(28f, 0f, 0f); // 下のベルが客席の方へ
            var body = clarinet ? InstBlack : Mat("Concert_Oboe", new Color(0.12f, 0.07f, 0.05f), null, false, 0.75f);
            Pipe(c, "Body", new Vector3(0f, L / 2f, 0f), new Vector3(0f, -L / 2f, 0f), 0.034f, body);
            Flare(c, "Bell", new Vector3(0f, -L / 2f, 0f), Vector3.down, 0.017f, bell, 0.1f, body);
            Pipe(c, "Mouthpiece", new Vector3(0f, L / 2f, 0f), new Vector3(0f, L / 2f + 0.06f, 0f), clarinet ? 0.03f : 0.012f, clarinet ? InstBlack : Mat("Concert_Reed", new Color(0.85f, 0.7f, 0.45f)));
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cylinder, "KeyRing", c, new Vector3(0f, L / 2f - 0.1f - i * L * 0.15f, 0f), new Vector3(0.042f, 0.004f, 0.042f), InstSilver, false);
            if (clarinet) Prim(PrimitiveType.Cylinder, "Barrel", c, new Vector3(0f, L / 2f - 0.05f, 0f), new Vector3(0.045f, 0.03f, 0.045f), InstBlack, false);
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 6f;
            return inst;
        }

        // バスクラリネット: 長い黒い管、上に銀の曲がった首、下に上を向いたベル
        MusicInstrument BassClarinet(Transform parent, float deg, float r)
        {
            var inst = NewInstrument(parent, "BASS CLARINET", OrchestraSpot(deg, r), ConcertMusic.Voice.Reed, 46, 300, 1.95f, new Vector3(0f, 0.8f, -0.15f), new Vector3(0.35f, 1.1f, 0.4f), true, out var pivot);
            var c = Group(pivot, "Tube");
            c.localPosition = new Vector3(0f, 0.85f, -0.15f);
            c.localRotation = Quaternion.Euler(8f, 0f, 0f);
            Pipe(c, "Body", new Vector3(0f, 0.42f, 0f), new Vector3(0f, -0.4f, 0f), 0.05f, InstBlack);
            PipePath(c, "Neck", new[] { new Vector3(0f, 0.42f, 0f), new Vector3(0f, 0.54f, 0.02f), new Vector3(0f, 0.6f, 0.11f) }, 0.022f, InstSilver);
            Pipe(c, "Mouthpiece", new Vector3(0f, 0.6f, 0.11f), new Vector3(0f, 0.61f, 0.17f), 0.03f, InstBlack);
            PipePath(c, "Bow", new[] { new Vector3(0f, -0.4f, 0f), new Vector3(0f, -0.5f, -0.05f), new Vector3(0f, -0.48f, -0.13f) }, 0.05f, InstSilver);
            Flare(c, "Bell", new Vector3(0f, -0.48f, -0.13f), new Vector3(0f, 0.6f, -0.8f), 0.03f, 0.08f, 0.13f, InstSilver);
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cylinder, "KeyRing", c, new Vector3(0f, 0.3f - i * 0.14f, 0f), new Vector3(0.06f, 0.005f, 0.06f), InstSilver, false);
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 4f;
            return inst;
        }

        // ファゴット / コントラファゴット: えんじ色の長い管が2本並び、銀の細い管 (ボーカル) が口の方へ曲がる (s = 大きさ)
        MusicInstrument Bassoon(Transform parent, string name, float deg, float r, float s, int points, int midi)
        {
            var inst = NewInstrument(parent, name, OrchestraSpot(deg, r), ConcertMusic.Voice.Reed, midi, points, 1.4f * s + 0.7f, new Vector3(0f, 0.62f * s + 0.1f, -0.15f), new Vector3(0.4f, 1.4f * s, 0.4f), true, out var pivot);
            var maroon = Mat("Concert_Bassoon", new Color(0.45f, 0.15f, 0.08f), null, false, 0.65f);
            var c = Group(pivot, "Bassoon");
            c.localPosition = new Vector3(0.05f, 0.62f * s + 0.1f, -0.15f);
            c.localRotation = Quaternion.Euler(0f, 0f, -20f);
            Pipe(c, "LongJoint", new Vector3(0f, -0.6f * s, 0f), new Vector3(0f, 0.65f * s, 0f), 0.07f * s, maroon);
            Pipe(c, "WingJoint", new Vector3(0.07f * s, -0.6f * s, 0f), new Vector3(0.07f * s, 0.25f * s, 0f), 0.06f * s, maroon);
            Prim(PrimitiveType.Cube, "Boot", c, new Vector3(0.035f * s, -0.64f * s, 0f), new Vector3(0.16f * s, 0.12f * s, 0.1f * s), maroon, false);
            Prim(PrimitiveType.Cylinder, "BellRing", c, new Vector3(0f, 0.65f * s, 0f), new Vector3(0.085f * s, 0.02f * s, 0.085f * s), InstPearl, false);
            PipePath(c, "Bocal", new[] { new Vector3(0.07f * s, 0.25f * s, 0f), new Vector3(0.07f * s, 0.36f * s, 0.02f * s), new Vector3(0.03f * s, 0.42f * s, 0.14f * s) }, 0.014f, InstSilver);
            for (int i = 0; i < 4; i++)
                Prim(PrimitiveType.Sphere, "Key", c, new Vector3(-0.035f * s, (-0.3f + i * 0.15f) * s, -0.02f * s), Vector3.one * 0.03f, InstSilver, false);
            if (s > 1.1f) Flare(c, "MetalBell", new Vector3(0f, 0.65f * s, 0f), new Vector3(0f, -0.4f, -1f), 0.04f * s, 0.09f * s, 0.12f * s, InstSilver); // コントラファゴットの下向きのベル
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 4f;
            inst.swayBeats = 4f;
            return inst;
        }

        // ------------------------------------------------------------------
        // 金管楽器
        // ------------------------------------------------------------------

        // ホルン (フレンチホルン): 客席に輪の面を見せる。太い主管の二重の輪、輪の下から太くなって後ろ左へ大きく開く朝顔、
        // 右寄りに縦に並ぶロータリーバルブ4つとレバー、上に並ぶU字のバルブ管、内側の小さな輪と横切る管、右上へ伸びる細い管とマウスピース
        MusicInstrument Horn(Transform parent, float deg, float r, int points, int midi)
        {
            var inst = NewInstrument(parent, "HORN", OrchestraSpot(deg, r), ConcertMusic.Voice.Brass, midi, points, 2f, new Vector3(-0.05f, 1.15f, -0.1f), new Vector3(0.75f, 0.6f, 0.5f), true, out var pivot);
            var c = Group(pivot, "Horn");
            c.localPosition = new Vector3(0f, 1.15f, -0.1f);
            c.localRotation = Quaternion.Euler(0f, -15f, 0f);
            var brass = InstBrass;
            // 主管の輪 (二重) と、内側の小さな輪
            PipeArc(c, "Hoop", Vector3.zero, Vector3.forward, new Vector3(0.21f, 0f, 0f), 0f, 360f, 32, 0.034f, brass);
            PipeArc(c, "Hoop2", new Vector3(0f, 0f, 0.025f), Vector3.forward, new Vector3(0.18f, 0f, 0f), 0f, 360f, 28, 0.026f, brass);
            PipeArc(c, "InnerLoop", new Vector3(-0.08f, 0.04f, -0.015f), Vector3.forward, new Vector3(0.075f, 0f, 0f), 0f, 360f, 18, 0.02f, brass);
            PipeArc(c, "LowerLoop", new Vector3(0.02f, -0.11f, -0.015f), Vector3.forward, new Vector3(0.08f, 0f, 0f), 180f, 360f, 12, 0.02f, brass);
            // 輪を横切る管
            Pipe(c, "CrossTube", new Vector3(-0.17f, 0.035f, -0.01f), new Vector3(0.18f, 0.035f, -0.01f), 0.018f, brass);
            Pipe(c, "CrossTube", new Vector3(-0.14f, -0.04f, -0.01f), new Vector3(0.18f, -0.04f, -0.01f), 0.018f, brass);
            // 上に並ぶU字のバルブ管
            float[] loopX = { -0.02f, 0.06f, 0.13f };
            for (int i = 0; i < loopX.Length; i++)
            {
                float lr = 0.035f + i * 0.004f, top = 0.15f + i * 0.012f;
                PipeArc(c, "ValveSlide", new Vector3(loopX[i], top, -0.02f), Vector3.forward, new Vector3(lr, 0f, 0f), 0f, 180f, 8, 0.016f, brass);
                foreach (float e in new[] { -1f, 1f })
                    Pipe(c, "SlideLeg", new Vector3(loopX[i] + e * lr, top, -0.02f), new Vector3(loopX[i] + e * lr, 0.05f, -0.02f), 0.016f, brass);
            }
            // ロータリーバルブ4つ (金の丸い胴に銀の芯) と、右のレバー
            for (int i = 0; i < 4; i++)
            {
                float y = 0.1f - i * 0.058f;
                var rotor = Prim(PrimitiveType.Cylinder, "Rotor", c, new Vector3(0.085f, y, -0.03f), new Vector3(0.052f, 0.014f, 0.052f), brass, false);
                rotor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var cap = Prim(PrimitiveType.Cylinder, "RotorCap", c, new Vector3(0.085f, y, -0.045f), new Vector3(0.026f, 0.006f, 0.026f), InstSilver, false);
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                if (i < 3) Pipe(c, "Lever", new Vector3(0.11f, y, -0.04f), new Vector3(0.2f, y + 0.015f, -0.045f), 0.01f, InstSilver);
            }
            Pipe(c, "LeverBar", new Vector3(0.2f, 0.115f, -0.045f), new Vector3(0.2f, -0.03f, -0.045f), 0.008f, InstSilver);
            // 右上へ伸びる細い管と、銀のマウスピース
            Pipe(c, "Leadpipe", new Vector3(0.16f, 0.13f, 0f), new Vector3(0.25f, 0.34f, 0.01f), 0.016f, brass);
            Pipe(c, "Mouthpiece", new Vector3(0.25f, 0.34f, 0.01f), new Vector3(0.265f, 0.39f, 0.012f), 0.02f, InstSilver);
            Prim(PrimitiveType.Sphere, "MouthpieceCup", c, new Vector3(0.267f, 0.395f, 0.012f), new Vector3(0.03f, 0.016f, 0.03f), InstSilver, false);
            // 朝顔: 輪の左下から太くなりながら出て、後ろ左へ大きく開く (根元から先までなめらかに広がる)
            PipePath(c, "BellTail", new[] { new Vector3(0.02f, -0.21f, 0f), new Vector3(-0.08f, -0.2f, 0.01f), new Vector3(-0.15f, -0.17f, 0.03f) }, 0.036f, brass);
            var bellDir = new Vector3(-0.75f, -0.1f, 0.65f).normalized;
            var bell = MeshPart(c, "Bell", TrumpetBellMesh(0.018f, 0.17f, 0.42f), brass);
            bell.localPosition = new Vector3(-0.15f, -0.17f, 0.03f);
            bell.localRotation = Quaternion.LookRotation(bellDir);
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 5f;
            return inst;
        }

        // トランペット / ピッコロトランペット: 3本のピストンと、客席の方を向いた朝顔 (L = 長さ)
        MusicInstrument Trumpet(Transform parent, string name, float deg, float r, float L, int points, int midi)
        {
            var inst = NewInstrument(parent, name, OrchestraSpot(deg, r), ConcertMusic.Voice.Brass, midi, points, 1.95f, new Vector3(0f, 1.25f, -0.2f), new Vector3(0.3f, 0.3f, L + 0.2f), true, out var pivot);
            var c = Group(pivot, "Trumpet");
            c.localPosition = new Vector3(0f, 1.25f, -0.2f);
            c.localRotation = Quaternion.Euler(10f, 0f, 0f); // 朝顔を少し上へ
            Pipe(c, "Leadpipe", new Vector3(0f, 0f, 0.3f * L), new Vector3(0f, 0f, -0.2f * L), 0.04f * L, InstBrass);
            Flare(c, "Mouthpiece", new Vector3(0f, 0f, 0.28f * L), Vector3.forward, 0.012f, 0.025f, 0.06f, InstSilver);
            for (int i = 0; i < 3; i++)
            {
                float z = (0.08f - i * 0.08f) * L;
                Pipe(c, "Valve", new Vector3(0f, -0.07f * L, z), new Vector3(0f, 0.09f * L, z), 0.06f * L, InstSilver);
                Prim(PrimitiveType.Sphere, "ValveCap", c, new Vector3(0f, 0.1f * L, z), Vector3.one * 0.045f * L, InstPearl, false);
            }
            Pipe(c, "LowerTube", new Vector3(0f, -0.1f * L, 0.25f * L), new Vector3(0f, -0.1f * L, -0.15f * L), 0.04f * L, InstBrass);
            PipeArc(c, "Crook", new Vector3(0f, -0.05f * L, 0.25f * L), Vector3.right, new Vector3(0f, -0.05f * L, 0f), 0f, -180f, 6, 0.04f * L, InstBrass);
            Pipe(c, "BellTube", new Vector3(0f, 0f, -0.2f * L), new Vector3(0f, 0f, -0.45f * L), 0.045f * L, InstBrass);
            Flare(c, "Bell", new Vector3(0f, 0f, -0.45f * L), Vector3.back, 0.025f * L, 0.12f * L, 0.18f * L, InstBrass);
            inst.swayAxis = Vector3.right;
            inst.swayDeg = 5f;
            inst.bob = 0.03f;
            return inst;
        }

        // トロンボーン / バストロンボーン: 長いスライドが伸び縮みする (s = 大きさ。バスは朝顔が大きく、管がもう1つ巻いてある)
        MusicInstrument Trombone(Transform parent, string name, float deg, float r, float s, int points, int midi, bool bass)
        {
            var inst = NewInstrument(parent, name, OrchestraSpot(deg, r), ConcertMusic.Voice.Brass, midi, points, 2f, new Vector3(0f, 1.3f, -0.3f), new Vector3(0.35f, 0.35f, 1.1f * s), true, out var pivot);
            var c = Group(pivot, "Trombone");
            c.localPosition = new Vector3(0f, 1.3f, -0.25f);
            c.localRotation = Quaternion.Euler(6f, 0f, 0f);
            // 朝顔の管 (右)
            Pipe(c, "BellTube", new Vector3(0.07f * s, 0f, 0.4f * s), new Vector3(0.07f * s, 0f, -0.15f * s), 0.04f * s, InstBrass);
            Flare(c, "Bell", new Vector3(0.07f * s, 0f, -0.15f * s), Vector3.back, 0.025f * s, (bass ? 0.15f : 0.13f) * s, 0.3f * s, InstBrass);
            PipeArc(c, "TuningSlide", new Vector3(0.02f * s, 0f, 0.4f * s), Vector3.up, new Vector3(0.05f * s, 0f, 0f), 0f, -180f, 6, 0.035f * s, InstBrass);
            // 内側のスライド (動かない) とマウスピース
            Pipe(c, "InnerSlide", new Vector3(-0.03f * s, 0f, 0.4f * s), new Vector3(-0.03f * s, 0f, -0.05f * s), 0.024f * s, InstSilver);
            Pipe(c, "InnerSlide2", new Vector3(-0.1f * s, 0f, 0.25f * s), new Vector3(-0.1f * s, 0f, -0.05f * s), 0.024f * s, InstSilver);
            Pipe(c, "Mouthpiece", new Vector3(-0.1f * s, 0f, 0.25f * s), new Vector3(-0.1f * s, 0f, 0.32f * s), 0.03f * s, InstSilver);
            // 外側のスライド (伸び縮みする)
            var slide = Group(c, "Slide");
            Pipe(slide, "OuterSlide", new Vector3(-0.03f * s, 0f, 0.05f * s), new Vector3(-0.03f * s, 0f, -0.55f * s), 0.032f * s, InstBrass);
            Pipe(slide, "OuterSlide2", new Vector3(-0.1f * s, 0f, 0.05f * s), new Vector3(-0.1f * s, 0f, -0.55f * s), 0.032f * s, InstBrass);
            PipeArc(slide, "SlideCrook", new Vector3(-0.065f * s, 0f, -0.55f * s), Vector3.up, new Vector3(0.035f * s, 0f, 0f), 0f, 180f, 6, 0.032f * s, InstBrass);
            Prim(PrimitiveType.Cube, "SlideBrace", slide, new Vector3(-0.065f * s, 0f, -0.3f * s), new Vector3(0.07f * s, 0.012f, 0.012f), InstBrass, false);
            if (bass) PipeArc(c, "Attachment", new Vector3(0.07f * s, -0.07f * s, 0.25f * s), Vector3.right, new Vector3(0f, 0.065f * s, 0f), 0f, 360f, 12, 0.03f * s, InstBrass);
            inst.slider = slide;
            inst.slideOffset = new Vector3(0f, 0f, -0.3f * s);
            inst.slideBeats = 2f;
            inst.swayAxis = Vector3.right;
            inst.swayDeg = 4f;
            return inst;
        }

        // チューバ / ユーフォニアム: 縦に太くなる管と、上を向いた大きな朝顔 (T = 高さ)
        MusicInstrument Tuba(Transform parent, string name, float deg, float r, float T, int points, int midi)
        {
            float cy = 0.45f + 0.5f * T;
            var inst = NewInstrument(parent, name, OrchestraSpot(deg, r), ConcertMusic.Voice.Brass, midi, points, cy + 0.6f * T + 0.45f, new Vector3(0f, cy, -0.15f), new Vector3(0.45f * T + 0.1f, 1.1f * T, 0.45f), true, out var pivot);
            var c = Group(pivot, "Tuba");
            c.localPosition = new Vector3(0f, cy, -0.15f);
            c.localRotation = Quaternion.Euler(0f, 0f, -8f);
            PipeArc(c, "Bottom", new Vector3(0f, -0.35f * T, 0f), Vector3.forward, new Vector3(-0.1f * T, 0f, 0f), 0f, 180f, 8, 0.09f * T, InstBrass);
            Pipe(c, "LeftLeg", new Vector3(-0.1f * T, -0.35f * T, 0f), new Vector3(-0.1f * T, 0.12f * T, 0f), 0.07f * T, InstBrass);
            Flare(c, "Branch", new Vector3(0.1f * T, -0.35f * T, 0f), Vector3.up, 0.045f * T, 0.09f * T, 0.6f * T, InstBrass);
            Flare(c, "Bell", new Vector3(0.1f * T, 0.25f * T, 0f), new Vector3(0.12f, 1f, 0.05f), 0.09f * T, 0.24f * T, 0.3f * T, InstBrass);
            PipeArc(c, "FrontLoop", new Vector3(0f, -0.02f * T, -0.09f * T), Vector3.right, new Vector3(0f, 0.12f * T, 0f), 0f, 360f, 16, 0.035f * T, InstBrass);
            for (int i = 0; i < 4; i++)
            {
                float x = (-0.05f + i * 0.035f) * T;
                Pipe(c, "Valve", new Vector3(x, 0f, -0.1f * T), new Vector3(x, 0.16f * T, -0.1f * T), 0.032f * T, InstSilver);
                Prim(PrimitiveType.Sphere, "ValveCap", c, new Vector3(x, 0.17f * T, -0.1f * T), Vector3.one * 0.03f * T, InstPearl, false);
            }
            PipePath(c, "Leadpipe", new[] { new Vector3(-0.1f * T, 0.12f * T, 0f), new Vector3(-0.12f * T, 0.2f * T, 0.08f * T), new Vector3(-0.16f * T, 0.24f * T, 0.18f * T) }, 0.025f * T, InstBrass);
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 4f;
            inst.swayBeats = 4f;
            inst.bob = 0.035f;
            return inst;
        }

        // ------------------------------------------------------------------
        // 打楽器
        // ------------------------------------------------------------------

        // マレット・スティック: 根元 (from) を軸に、先 (to) の玉が楽器を打つ。axis のまわりに振り上がる
        Transform Mallet(Transform parent, Vector3 from, Vector3 to, float stick, float head, Material headMat)
        {
            var m = Group(parent, "Mallet");
            m.localPosition = from;
            Pipe(m, "Stick", Vector3.zero, to - from, stick, Mat("Concert_Stick", new Color(0.75f, 0.6f, 0.4f)));
            Prim(PrimitiveType.Sphere, "Head", m, to - from, Vector3.one * head, headMat, false);
            return m;
        }

        // ティンパニ: 銅の釜と白い皮。後ろの2本のマレットが交互に打つ (d = 直径)
        MusicInstrument Timpani(Transform parent, float deg, float r, float d, int midi)
        {
            var inst = NewInstrument(parent, "TIMPANI", OrchestraSpot(deg, r), ConcertMusic.Voice.Timpani, midi, 300, 1.75f, new Vector3(0f, 0.6f, 0f), new Vector3(d + 0.1f, 0.9f, d + 0.1f), false, out var pivot);
            var copper = Mat("Concert_Copper", new Color(0.85f, 0.45f, 0.25f), new Color(0.1f, 0.04f, 0.02f), false, 0.85f);
            Flare(pivot, "Kettle", new Vector3(0f, 0.8f, 0f), Vector3.down, d / 2f, d * 0.28f, 0.42f, copper);
            Prim(PrimitiveType.Sphere, "KettleBottom", pivot, new Vector3(0f, 0.39f, 0f), new Vector3(d * 0.56f, 0.12f, d * 0.56f), copper, false);
            Prim(PrimitiveType.Cylinder, "Head", pivot, new Vector3(0f, 0.805f, 0f), new Vector3(d + 0.01f, 0.006f, d + 0.01f), Mat("Concert_Head", new Color(0.95f, 0.93f, 0.86f)), false);
            FlatRing(pivot, "Rim", d / 2f + 0.012f, 0.8f, 0.03f, 0.045f, 24, InstSilver);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 60f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Pipe(pivot, "Leg", dir * d * 0.3f + Vector3.up * 0.45f, dir * d * 0.45f + Vector3.up * 0.01f, 0.03f, InstSilver);
            }
            Prim(PrimitiveType.Cube, "Pedal", pivot, new Vector3(0f, 0.03f, d * 0.5f), new Vector3(0.2f, 0.04f, 0.12f), InstBlack, false);
            var felt = Mat("Concert_Felt", new Color(0.92f, 0.9f, 0.85f));
            var m1 = Mallet(pivot, new Vector3(-0.13f, 1.1f, d * 0.5f + 0.15f), new Vector3(-0.1f, 0.84f, 0.05f), 0.014f, 0.055f, felt);
            var m2 = Mallet(pivot, new Vector3(0.13f, 1.1f, d * 0.5f + 0.15f), new Vector3(0.1f, 0.84f, -0.05f), 0.014f, 0.055f, felt);
            inst.beaters = new[] { m1, m2 };
            inst.beaterAxis = Vector3.right;
            inst.beaterDeg = 28f;
            inst.beaterEvery = 2f;
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 2f;
            inst.bob = 0.02f;
            return inst;
        }

        // 大太鼓: 横向きの大きな太鼓を台に乗せ、ふわふわのばちが2拍ごとに打つ
        MusicInstrument BassDrum(Transform parent, float deg, float r)
        {
            var inst = NewInstrument(parent, "BASS DRUM", OrchestraSpot(deg, r), ConcertMusic.Voice.Drum, 36, 200, 2.1f, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 1.1f, 1.1f), false, out var pivot);
            var shell = Mat("Concert_DrumRed", new Color(0.65f, 0.08f, 0.1f), new Color(0.05f, 0f, 0f), false, 0.8f);
            var headMat = Mat("Concert_Head", new Color(0.95f, 0.93f, 0.86f));
            Prim(PrimitiveType.Cylinder, "Shell", pivot, new Vector3(0f, 0.8f, 0f), new Vector3(1f, 0.2f, 1f), shell, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float x in new[] { -0.205f, 0.205f })
            {
                Prim(PrimitiveType.Cylinder, "DrumHead", pivot, new Vector3(x, 0.8f, 0f), new Vector3(0.98f, 0.004f, 0.98f), headMat, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var hoop = Group(pivot, "Hoop");
                hoop.localPosition = new Vector3(x, 0.8f, 0f);
                hoop.localRotation = Quaternion.Euler(0f, 90f, 0f);
                RingSegments(hoop, "HoopSeg", 0.5f, 20, 0.035f, Mat("Concert_HarpWood", new Color(0.72f, 0.46f, 0.2f), null, false, 0.7f));
            }
            foreach (float z in new[] { -0.3f, 0.3f })
                Pipe(pivot, "Stand", new Vector3(0f, 0.32f, z), new Vector3(0f, 0.01f, z * 1.3f), 0.04f, InstBlack);
            var beater = Mallet(pivot, new Vector3(0.48f, 1.35f, 0f), new Vector3(0.24f, 0.85f, 0f), 0.02f, 0.11f, Mat("Concert_Felt", new Color(0.92f, 0.9f, 0.85f)));
            inst.beaters = new[] { beater };
            inst.beaterAxis = Vector3.forward;
            inst.beaterDeg = 30f;
            inst.beaterEvery = 2f;
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 2f;
            return inst;
        }

        // 小太鼓: 銀の胴の小さな太鼓。2本のスティックが8分音符で交互に打つ
        MusicInstrument SnareDrum(Transform parent, float deg, float r)
        {
            var inst = NewInstrument(parent, "SNARE DRUM", OrchestraSpot(deg, r), ConcertMusic.Voice.Snare, 60, 500, 1.7f, new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 0.4f, 0.5f), false, out var pivot);
            Prim(PrimitiveType.Cylinder, "Shell", pivot, new Vector3(0f, 0.75f, 0f), new Vector3(0.38f, 0.08f, 0.38f), InstSilver, false);
            Prim(PrimitiveType.Cylinder, "DrumHead", pivot, new Vector3(0f, 0.832f, 0f), new Vector3(0.37f, 0.003f, 0.37f), Mat("Concert_Head", new Color(0.95f, 0.93f, 0.86f)), false);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Pipe(pivot, "Leg", Vector3.up * 0.66f, dir * 0.25f + Vector3.up * 0.01f, 0.02f, InstBlack);
            }
            var wood = Mat("Concert_Stick", new Color(0.75f, 0.6f, 0.4f));
            var s1 = Mallet(pivot, new Vector3(-0.12f, 1f, 0.3f), new Vector3(-0.05f, 0.85f, -0.02f), 0.014f, 0.025f, wood);
            var s2 = Mallet(pivot, new Vector3(0.12f, 1f, 0.3f), new Vector3(0.05f, 0.85f, 0.02f), 0.014f, 0.025f, wood);
            inst.beaters = new[] { s1, s2 };
            inst.beaterAxis = Vector3.right;
            inst.beaterDeg = 25f;
            inst.beaterEvery = 0.5f;
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 3f;
            inst.bob = 0.03f;
            return inst;
        }

        // シンバル: 2枚の金色の円盤が、小節の頭でジャーンと打ち合わされる
        MusicInstrument Cymbals(Transform parent, float deg, float r)
        {
            var inst = NewInstrument(parent, "CYMBALS", OrchestraSpot(deg, r), ConcertMusic.Voice.Cymbal, 60, 300, 2.1f, new Vector3(0f, 1.4f, 0f), new Vector3(0.8f, 0.6f, 0.4f), false, out var pivot);
            var brass = Mat("Concert_CymbalBrass", new Color(0.95f, 0.75f, 0.35f), new Color(0.15f, 0.1f, 0.03f), false, 0.92f);
            var clappers = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                var cym = Group(pivot, i == 0 ? "CymbalL" : "CymbalR");
                cym.localPosition = new Vector3(s * 0.035f, 1.4f, 0f);
                Prim(PrimitiveType.Cylinder, "Disc", cym, Vector3.zero, new Vector3(0.48f, 0.004f, 0.48f), brass, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Prim(PrimitiveType.Sphere, "Dome", cym, new Vector3(s * 0.01f, 0f, 0f), new Vector3(0.03f, 0.11f, 0.11f), brass, false);
                Prim(PrimitiveType.Cube, "Strap", cym, new Vector3(s * 0.04f, 0f, 0f), new Vector3(0.03f, 0.1f, 0.03f), InstBlack, false);
                clappers[i] = cym;
            }
            inst.clappers = clappers;
            inst.clapOffset = new Vector3(-0.18f, 0f, 0f);
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 4f;
            inst.bob = 0.04f;
            return inst;
        }

        // ドラ (ゴング): 枠に吊った大きな青銅の円盤。8拍ごとに大きなばちで打つ
        MusicInstrument Gong(Transform parent, float deg, float r)
        {
            var inst = NewInstrument(parent, "GONG", OrchestraSpot(deg, r), ConcertMusic.Voice.Gong, 60, 300, 2.5f, new Vector3(0f, 1.15f, 0f), new Vector3(1.3f, 1.3f, 0.3f), false, out var pivot);
            var bronze = Mat("Concert_Bronze", new Color(0.75f, 0.5f, 0.22f), new Color(0.1f, 0.05f, 0.01f), false, 0.8f);
            var frameMat = Mat("Concert_PianoBlack", new Color(0.03f, 0.03f, 0.035f), null, false, 0.92f);
            foreach (float x in new[] { -0.78f, 0.78f }) Pipe(pivot, "Post", new Vector3(x, 0f, 0f), new Vector3(x, 1.95f, 0f), 0.07f, frameMat);
            Pipe(pivot, "TopBar", new Vector3(-0.85f, 1.92f, 0f), new Vector3(0.85f, 1.92f, 0f), 0.08f, frameMat);
            var disc = Group(pivot, "GongDisc");
            disc.localPosition = new Vector3(0f, 1.15f, 0f);
            Prim(PrimitiveType.Cylinder, "Disc", disc, Vector3.zero, new Vector3(1.1f, 0.012f, 1.1f), bronze, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "Boss", disc, new Vector3(0f, 0f, -0.01f), new Vector3(0.2f, 0.2f, 0.06f), bronze, false);
            foreach (float x in new[] { -0.3f, 0.3f }) Pipe(pivot, "Cord", new Vector3(x, 1.9f, 0f), new Vector3(x * 0.9f, 1.66f, 0f), 0.015f, InstBlack);
            var mallet = Mallet(pivot, new Vector3(0.45f, 1.6f, -0.4f), new Vector3(0.08f, 1.2f, -0.12f), 0.03f, 0.14f, Mat("Concert_Felt", new Color(0.92f, 0.9f, 0.85f)));
            inst.beaters = new[] { mallet };
            inst.beaterAxis = Vector3.right;
            inst.beaterDeg = 35f;
            inst.beaterEvery = 8f;
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 3f;
            inst.swayBeats = 4f;
            return inst;
        }

        // 木琴 (木の鍵盤と金色の共鳴管) / 鉄琴 (glock = true: 小さな銀の鍵盤)。2本のマレットが交互に打つ
        MusicInstrument Xylophone(Transform parent, float deg, float r, bool glock)
        {
            string name = glock ? "GLOCKENSPIEL" : "XYLOPHONE";
            var inst = NewInstrument(parent, name, OrchestraSpot(deg, r), glock ? ConcertMusic.Voice.Bell : ConcertMusic.Voice.Mallet, glock ? 84 : 72, glock ? 500 : 400, 1.75f,
                                     new Vector3(0f, 0.9f, 0f), glock ? new Vector3(0.8f, 0.5f, 0.5f) : new Vector3(1.4f, 0.5f, 0.6f), false, out var pivot);
            var frame = Mat("Concert_PianoBlack", new Color(0.03f, 0.03f, 0.035f), null, false, 0.92f);
            var bars = glock ? InstSilver : Mat("Concert_Rosewood", new Color(0.55f, 0.22f, 0.12f), null, false, 0.6f);
            float w = glock ? 0.75f : 1.35f;
            int n = glock ? 12 : 14;
            float barY = glock ? 0.99f : 0.89f;
            Prim(PrimitiveType.Cube, "Frame", pivot, new Vector3(0f, barY - 0.04f, 0f), new Vector3(w + 0.08f, 0.05f, glock ? 0.36f : 0.5f), frame, false);
            for (int i = 0; i < n; i++)
            {
                float x = -w / 2f + (i + 0.5f) * w / n;
                float len = Mathf.Lerp(glock ? 0.18f : 0.4f, glock ? 0.09f : 0.2f, i / (float)(n - 1)); // 低い音ほど長い
                Prim(PrimitiveType.Cube, "Bar", pivot, new Vector3(x, barY, 0f), new Vector3(w / n * 0.8f, glock ? 0.012f : 0.025f, len), bars, false);
                if (!glock) Pipe(pivot, "Resonator", new Vector3(x, barY - 0.07f, 0f), new Vector3(x, barY - 0.07f - Mathf.Lerp(0.5f, 0.15f, i / (float)(n - 1)), 0f), 0.045f, InstBrass);
            }
            foreach (float x in new[] { -w / 2f, w / 2f })
                Pipe(pivot, "Leg", new Vector3(x, barY - 0.06f, 0f), new Vector3(x, 0.01f, 0f), 0.035f, frame);
            var headMat = glock ? InstBrass : Mat("Concert_MalletHead", new Color(1f, 0.85f, 0.3f));
            var m1 = Mallet(pivot, new Vector3(-0.25f, barY + 0.28f, 0.4f), new Vector3(-0.18f, barY + 0.03f, 0f), 0.012f, 0.045f, headMat);
            var m2 = Mallet(pivot, new Vector3(0.25f, barY + 0.28f, 0.4f), new Vector3(0.18f, barY + 0.03f, 0f), 0.012f, 0.045f, headMat);
            inst.beaters = new[] { m1, m2 };
            inst.beaterAxis = Vector3.right;
            inst.beaterDeg = 30f;
            inst.beaterEvery = 0.5f;
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 2f;
            inst.bob = 0.02f;
            return inst;
        }

        // チューブラーベル: 枠に吊った長さの違う金属の管。4拍ごとに槌で打つ
        MusicInstrument TubularBells(Transform parent, float deg, float r)
        {
            var inst = NewInstrument(parent, "TUBULAR BELLS", OrchestraSpot(deg, r), ConcertMusic.Voice.Bell, 72, 400, 2.6f, new Vector3(0f, 1.3f, 0f), new Vector3(1.2f, 1.6f, 0.3f), false, out var pivot);
            var frame = Mat("Concert_PianoBlack", new Color(0.03f, 0.03f, 0.035f), null, false, 0.92f);
            foreach (float x in new[] { -0.6f, 0.6f }) Pipe(pivot, "Post", new Vector3(x, 0f, 0f), new Vector3(x, 2.1f, 0f), 0.05f, frame);
            Pipe(pivot, "TopBar", new Vector3(-0.65f, 2.05f, 0f), new Vector3(0.65f, 2.05f, 0f), 0.06f, frame);
            for (int i = 0; i < 8; i++)
            {
                float x = -0.45f + i * (0.9f / 7f);
                float len = Mathf.Lerp(1.5f, 0.9f, i / 7f);
                Pipe(pivot, "Tube", new Vector3(x, 1.95f, 0f), new Vector3(x, 1.95f - len, 0f), 0.05f, i % 2 == 0 ? InstSilver : InstBrass);
                Prim(PrimitiveType.Cylinder, "TubeCap", pivot, new Vector3(x, 1.96f, 0f), new Vector3(0.06f, 0.015f, 0.06f), InstSilver, false);
            }
            var hammer = Mallet(pivot, new Vector3(0.25f, 1.95f, -0.4f), new Vector3(0.05f, 1.75f, -0.05f), 0.02f, 0.06f, Mat("Concert_MalletHead", new Color(1f, 0.85f, 0.3f)));
            inst.beaters = new[] { hammer };
            inst.beaterAxis = Vector3.right;
            inst.beaterDeg = 30f;
            inst.beaterEvery = 4f;
            inst.swayAxis = Vector3.forward;
            inst.swayDeg = 2f;
            return inst;
        }
    }
}
