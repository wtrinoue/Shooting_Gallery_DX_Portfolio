using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// タイトルの看板の生成: メイン屋台の左に操作方法の看板 (いちばん下にクレジットの的)、右にモードの説明の看板。
    /// どちらも屋台と同じように輪の上に立ってプレイヤーの方を向き、タイトルの間だけ表示される
    /// (ゲーム中はフィーバーの屋台が並ぶ場所なので GameManager が2枚まとめて隠す)。
    /// 操作方法は、左右のコントローラーの絵と、ボタンごとの操作を色分けして書く (ボタンの色は長押しのインジケーターの色と同じ)。
    /// </summary>
    public partial class GalleryBuilder
    {
        static readonly Color TriggerColor = new Color(1f, 0.3f, 0.25f);
        static readonly Color ResetColor = new Color(1f, 0.45f, 0.15f);
        static readonly Color EndlessColor = new Color(0.3f, 1f, 0.85f);
        static readonly Color DebugColor = new Color(0.6f, 1f, 0.3f);

        // 看板を置く向き (メイン屋台 = 0度 から左右へ)。看板 (幅 4.5m) の端が、メイン屋台の屋根の角・提灯にかからない角度
        const float TitleBoardAngle = 75f;

        /// <summary>タイトルの看板 (左に操作方法、右にクレジット) をまとめて作る。GameManager はこのまとまりごと出し入れする</summary>
        GameObject BuildControlsBoard()
        {
            var root = Group(world, "TitleBoards");
            var board = TitleBoardRoot(root, "ControlsBoard", -TitleBoardAngle);

            var wood = Mat("Wood", new Color(0.45f, 0.28f, 0.15f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var panel = Mat("Controls_Panel", new Color(0.06f, 0.08f, 0.18f), new Color(0.03f, 0.04f, 0.1f));
            var banner = Mat("Cloth_Red", new Color(0.75f, 0.1f, 0.12f));

            // 柱・板・金のふち・上の赤い幕
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Post", board, new Vector3(s * 2.2f, 1.35f, 0.05f), new Vector3(0.1f, 2.7f, 0.1f), wood);
            Prim(PrimitiveType.Cube, "Panel", board, new Vector3(0f, 1.45f, 0.03f), new Vector3(4.3f, 1.9f, 0.05f), panel);
            foreach (float y in new[] { 0.5f, 2.4f })
                Prim(PrimitiveType.Cube, "TrimH", board, new Vector3(0f, y, 0f), new Vector3(4.34f, 0.04f, 0.03f), gold, false);
            Prim(PrimitiveType.Cube, "Divider", board, new Vector3(0f, 1.55f, 0f), new Vector3(0.02f, 1.3f, 0.02f), gold, false);
            Prim(PrimitiveType.Cube, "Banner", board, new Vector3(0f, 2.6f, 0.02f), new Vector3(4.5f, 0.34f, 0.06f), banner);
            MakeText(board, "Title", "HOW TO PLAY   操作方法", new Vector3(0f, 2.6f, -0.02f), 0.18f, Color.white);

            // 左手
            MakeText(board, "LeftTitle", "LEFT  左手", new Vector3(-1.05f, 2.22f, -0.01f), 0.1f, new Color(1f, 0.9f, 0.6f));
            ControllerFigure(board, new Vector3(-1.75f, 1.6f, -0.01f), false);
            ControlRow(board, new Vector3(-1.4f, 1.82f, -0.01f), "TRIG", TriggerColor, "トリガー : 左の銃で撃つ", "(フィーバー中の二丁拳銃)");
            ControlRow(board, new Vector3(-1.4f, 1.42f, -0.01f), "X", DebugColor, "X 長押し : デバッグモード", "(全部の的を試せる)");

            // 右手
            MakeText(board, "RightTitle", "RIGHT  右手", new Vector3(1.05f, 2.22f, -0.01f), 0.1f, new Color(1f, 0.9f, 0.6f));
            ControllerFigure(board, new Vector3(0.35f, 1.6f, -0.01f), true);
            ControlRow(board, new Vector3(0.7f, 1.95f, -0.01f), "TRIG", TriggerColor, "トリガー : 撃つ", "(マシンガンは押しっぱなしで連射)");
            ControlRow(board, new Vector3(0.7f, 1.58f, -0.01f), "B", EndlessColor, "B 長押し : エンドレスモード", "(時間切れなし)"); // (B が上、A が下。ユーザーの指定)
            ControlRow(board, new Vector3(0.7f, 1.21f, -0.01f), "A", ResetColor, "A 長押し : 最初に戻る", "(リセット)");

            // 下: 遊び方
            MakeText(board, "HowTo1", "2 MIN か 100,000 POINTS の的を撃ってゲーム開始！", new Vector3(0f, 0.85f, -0.01f), 0.1f, new Color(1f, 0.85f, 0.3f));
            MakeText(board, "HowTo2", "特別な的を撃つと、武器やステージが変わるよ", new Vector3(0f, 0.65f, -0.01f), 0.07f, Color.white);

            BuildCredits(board, panel, gold);       // いちばん下の CREDITS の的 (撃つと看板の前にクレジットが開く)
            BuildModesBoard(root, panel, gold, wood, banner); // 右の看板: モードの説明
            return root.gameObject;
        }

        /// <summary>タイトルの看板1枚の置き場所: 屋台と同じく、輪の上の angle 度の向きに立てて、表 (-Z) をプレイヤーの方へ向ける</summary>
        /// <summary>
        /// ラウンドの始まりの看板 (GoalSign): 上にプロペラの付いた紺色の板に、金のふち。
        /// 1行目「Get 100,000 points !!」、2行目に大きくカウントダウンの数字。メイン屋台の手前の空中に降りてくる
        /// </summary>
        GoalSign BuildGoalSign(int goalScore)
        {
            var root = Group(world, "GoalSign");
            var body = Group(root, "Body");
            var navy = Mat("GoalSign_Panel", new Color(0.06f, 0.08f, 0.2f), new Color(0.03f, 0.04f, 0.12f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var chrome = Mat("Casino_Chrome", new Color(0.78f, 0.8f, 0.85f), null, false, 0.92f);
            var blade = Mat("GoalSign_Blade", new Color(0.85f, 0.15f, 0.15f), new Color(0.2f, 0.02f, 0.02f), false, 0.6f);
            const float W = 2.4f, H = 1.1f;

            // 板と金のふち
            Prim(PrimitiveType.Cube, "Panel", body, Vector3.zero, new Vector3(W, H, 0.06f), navy, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cube, "FrameH", body, new Vector3(0f, s * H / 2f, -0.01f), new Vector3(W + 0.08f, 0.06f, 0.08f), gold, false);
                Prim(PrimitiveType.Cube, "FrameV", body, new Vector3(s * W / 2f, 0f, -0.01f), new Vector3(0.06f, H + 0.08f, 0.08f), gold, false);
            }
            string goal = goalScore.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            var headline = MakeText(body, "Goal", $"Get {goal} points !!", new Vector3(0f, 0.25f, -0.045f), 0.2f, new Color(1f, 0.85f, 0.35f));
            var count = MakeText(body, "Count", "3", new Vector3(0f, -0.17f, -0.045f), 0.5f, Color.white);

            // 上のプロペラ (柱・軸・2枚の羽根)
            // 柱は長めにして、プロペラを屋台の看板 (地面から約 3.6m) より上で回す (看板の真ん中 2.5m + 板の上 0.55m + 柱 1.8m = 約 4.85m)
            const float MastLen = 1.8f;
            Prim(PrimitiveType.Cylinder, "Mast", body, new Vector3(0f, H / 2f + MastLen / 2f, 0f), new Vector3(0.06f, MastLen / 2f, 0.06f), chrome, false);
            foreach (float s in new[] { -1f, 1f }) // 板を吊る2本の支え
            {
                var strut = Prim(PrimitiveType.Cylinder, "Strut", body, new Vector3(s * 0.32f, H / 2f + 0.17f, 0f), new Vector3(0.03f, 0.22f, 0.03f), chrome, false);
                strut.transform.localRotation = Quaternion.Euler(0f, 0f, -s * 55f);
            }
            var rotor = Group(body, "Rotor");
            rotor.localPosition = new Vector3(0f, H / 2f + MastLen + 0.02f, 0f);
            Prim(PrimitiveType.Sphere, "Hub", rotor, Vector3.zero, Vector3.one * 0.14f, gold, false);
            for (int i = 0; i < 2; i++)
            {
                var b = Prim(PrimitiveType.Cube, "Blade", rotor, Vector3.zero, new Vector3(2f, 0.025f, 0.14f), blade, false);
                b.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }

            var sign = root.gameObject.AddComponent<GoalSign>();
            sign.body = body;
            sign.rotor = rotor;
            sign.headline = headline;
            sign.countText = count;
            sign.hoverPos = new Vector3(0f, 2.5f, stallRadius * 0.7f); // メイン屋台の手前の空中 (見上げすぎない高さ)
            body.gameObject.SetActive(false);
            return sign;
        }

        Transform TitleBoardRoot(Transform parent, string name, float angle)
        {
            var t = Group(parent, name);
            var rot = Quaternion.Euler(0f, angle, 0f);
            t.localPosition = rot * new Vector3(0f, 0f, stallRadius);
            t.localRotation = rot;
            return t;
        }

        /// <summary>
        /// 操作方法の看板のいちばん下の「CREDITS / LICENSES」の的と、撃つと看板の前に開くクレジットとライセンス全文のパネル (CreditsViewer)。
        /// パネルの下の的 (前へ / 閉じる / 次へ) でページを送る。文章は Resources/THIRD_PARTY_NOTICES.txt
        /// </summary>
        void BuildCredits(Transform board, Material panelMat, Material gold)
        {
            var dark = new Color(0.05f, 0.05f, 0.08f);
            var open = CreditsButton(board, "CreditsButton", new Vector3(0f, 0.27f, -0.04f), new Vector2(2.3f, 0.26f), "CREDITS / LICENSES  (shoot)", new Color(0.95f, 0.8f, 0.35f), 0.075f, dark);

            var panel = Group(board, "CreditsPanel");
            panel.localPosition = new Vector3(0f, 1.5f, -0.15f); // 看板の前に開く
            Prim(PrimitiveType.Cube, "Back", panel, Vector3.zero, new Vector3(4.4f, 2.3f, 0.04f), panelMat);
            foreach (float y in new[] { -1.15f, 1.15f })
                Prim(PrimitiveType.Cube, "FrameH", panel, new Vector3(0f, y, -0.02f), new Vector3(4.44f, 0.04f, 0.03f), gold, false);
            foreach (float x in new[] { -2.2f, 2.2f })
                Prim(PrimitiveType.Cube, "FrameV", panel, new Vector3(x, 0f, -0.02f), new Vector3(0.04f, 2.34f, 0.03f), gold, false);
            MakeText(panel, "Title", "CREDITS & LICENSES", new Vector3(0f, 1f, -0.03f), 0.12f, new Color(1f, 0.85f, 0.35f));
            // 文章: 行は左そろえで、文字のかたまり全体を看板の真ん中に置く (長い行は CreditsViewer が lineWidth で折り返す)
            var page = MakeText(panel, "PageText", "", new Vector3(0f, 0.86f, -0.03f), 0.075f, Color.white);
            page.anchor = TextAnchor.UpperCenter;
            page.alignment = TextAlignment.Left;
            var number = MakeText(panel, "PageNumber", "1 / 1", new Vector3(0f, -0.54f, -0.03f), 0.07f, new Color(0.75f, 0.78f, 0.88f));
            var prev = CreditsButton(panel, "PrevButton", new Vector3(-1.5f, -0.88f, -0.05f), new Vector2(0.95f, 0.26f), "< PREV", new Color(0.4f, 0.75f, 1f), 0.08f, dark);
            var close = CreditsButton(panel, "CloseButton", new Vector3(0f, -0.88f, -0.05f), new Vector2(0.95f, 0.26f), "CLOSE  X", new Color(1f, 0.4f, 0.35f), 0.08f, dark);
            var next = CreditsButton(panel, "NextButton", new Vector3(1.5f, -0.88f, -0.05f), new Vector2(0.95f, 0.26f), "NEXT >", new Color(0.4f, 0.75f, 1f), 0.08f, dark);

            var viewer = board.gameObject.AddComponent<CreditsViewer>();
            viewer.notices = Resources.Load<TextAsset>("THIRD_PARTY_NOTICES");
            viewer.panel = panel.gameObject;
            viewer.pageText = page;
            viewer.pageNumber = number;
            viewer.linesPerPage = 13;
            viewer.lineWidth = 4f;
            viewer.openButton = open;
            viewer.prevButton = prev;
            viewer.nextButton = next;
            viewer.closeButton = close;
            panel.gameObject.SetActive(false); // (撃つと開く)
        }

        /// <summary>
        /// メイン屋台の右のモードの看板: タイトルの的 (左から 2 MIN / 100,000 POINTS / WORLD GATE) を、上から順に日本語と英語で説明する。
        /// それぞれの行の左に、その的と同じ色の小さな的の印
        /// </summary>
        void BuildModesBoard(Transform root, Material panelMat, Material gold, Material wood, Material banner)
        {
            var board = TitleBoardRoot(root, "ModesBoard", TitleBoardAngle);
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "Post", board, new Vector3(s * 2.2f, 1.35f, 0.05f), new Vector3(0.1f, 2.7f, 0.1f), wood);
            Prim(PrimitiveType.Cube, "Panel", board, new Vector3(0f, 1.45f, 0.03f), new Vector3(4.3f, 1.9f, 0.05f), panelMat);
            foreach (float y in new[] { 0.5f, 2.4f })
                Prim(PrimitiveType.Cube, "TrimH", board, new Vector3(0f, y, 0f), new Vector3(4.34f, 0.04f, 0.03f), gold, false);
            Prim(PrimitiveType.Cube, "Banner", board, new Vector3(0f, 2.6f, 0.02f), new Vector3(4.5f, 0.34f, 0.06f), banner);
            MakeText(board, "Title", "MODES   モード", new Vector3(0f, 2.6f, -0.02f), 0.18f, Color.white);

            // 説明の右の空いている所に、金の細いふちを付けた絵 (ぽこち)
            var picTex = LoadSourceAsset(PocochiImagePath, a => a.pocochiImage);
            if (picTex != null)
            {
                var pic = Group(board, "Pocochi");
                pic.localPosition = new Vector3(1.3f, 1.45f, -0.005f);
                const float P = 1.45f; // 絵の大きさ (正方形。板の高さ 1.9m に収まる)
                foreach (float s in new[] { -1f, 1f })
                {
                    Prim(PrimitiveType.Cube, "PicFrameH", pic, new Vector3(0f, s * (P / 2f + 0.015f), 0f), new Vector3(P + 0.06f, 0.03f, 0.02f), gold, false);
                    Prim(PrimitiveType.Cube, "PicFrameV", pic, new Vector3(s * (P / 2f + 0.015f), 0f, 0f), new Vector3(0.03f, P + 0.06f, 0.02f), gold, false);
                }
                var quad = Prim(PrimitiveType.Quad, "Picture", pic, new Vector3(0f, 0f, -0.002f), new Vector3(P, P, 1f), PaintingMat("Title_Pocochi", picTex, false), false);
                quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var white = Mat("Target_White", Color.white);
            var modes = new (string name, string ja, string en, Material outer, Material center)[]
            {
                ("2 MIN", "二分間撃って点数を稼ぐモード！", "Shoot for 2 minutes and rack up points!",
                    Mat("Target_TimeGreen", new Color(0.15f, 0.65f, 0.25f), new Color(0.03f, 0.2f, 0.06f)), Mat("Target_TimeYellow", new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.2f, 0f))),
                ("100,000", "100,000点取るまでタイムアタック！", "Race the clock to 100,000 points!",
                    Mat("Target_StartRed", new Color(0.85f, 0.1f, 0.1f), new Color(0.3f, 0f, 0f)), Mat("Target_StartRed", new Color(0.85f, 0.1f, 0.1f), new Color(0.3f, 0f, 0f))),
                ("WORLD GATE", "別世界へ飛ぶ的を総まとめ！", "All the world-jump targets in one place!",
                    Mat("Target_GateBlue", new Color(0.2f, 0.25f, 0.85f), new Color(0.05f, 0.06f, 0.3f)), Mat("Target_GatePurple", new Color(0.6f, 0.2f, 0.85f), new Color(0.2f, 0.05f, 0.3f))),
            };
            for (int i = 0; i < modes.Length; i++)
            {
                var m = modes[i];
                float y = 2.05f - i * 0.6f; // 上から順に
                var badge = Group(board, $"Badge_{i}");
                badge.localPosition = new Vector3(-1.75f, y, -0.01f);
                foreach (var (d, z, mat) in new[] { (0.38f, 0f, m.outer), (0.25f, -0.004f, white), (0.11f, -0.008f, m.center) })
                {
                    var disc = Prim(PrimitiveType.Cylinder, "Disc", badge, new Vector3(0f, 0f, z), new Vector3(d, 0.004f, d), mat, false);
                    disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                LeftText(board, "ModeName", m.name, new Vector3(-1.45f, y + 0.13f, -0.01f), 0.13f, new Color(1f, 0.85f, 0.35f));
                LeftText(board, "ModeJa", m.ja, new Vector3(-1.45f, y - 0.03f, -0.01f), 0.1f, Color.white);
                LeftText(board, "ModeEn", m.en, new Vector3(-1.45f, y - 0.16f, -0.01f), 0.07f, new Color(0.8f, 0.82f, 0.9f));
            }
        }

        /// <summary>撃つと押せる板の的 (点数なし)。当たると縮んで、すぐにふくらんで戻る</summary>
        Target CreditsButton(Transform parent, string name, Vector3 pos, Vector2 size, string label, Color color, float textHeight, Color textColor)
        {
            var pivot = Group(parent, name);
            pivot.localPosition = pos;
            var visual = Group(pivot, "Visual");
            Prim(PrimitiveType.Cube, "Plate", visual, Vector3.zero, new Vector3(size.x, size.y, 0.04f), Mat($"Credits_{name}", color, color * 0.45f));
            MakeText(visual, "Label", label, new Vector3(0f, 0f, -0.03f), textHeight, textColor);
            var t = pivot.gameObject.AddComponent<Target>();
            t.kind = TargetKind.Credits;
            t.points = 0;
            t.visual = visual;
            t.shrinkOnHit = true;
            t.respawnDelay = 0.35f;
            return t;
        }

        /// <summary>1行分: 色付きのボタンの印と、操作の説明 (大きい文字 + 小さい補足)。pos は印の位置。文字は印の右に左寄せ</summary>
        void ControlRow(Transform parent, Vector3 pos, string badge, Color color, string main, string sub)
        {
            var mat = Mat($"Controls_{badge}", color, color * 0.6f, true);
            bool trigger = badge == "TRIG";
            if (trigger)
                Prim(PrimitiveType.Cube, "Badge", parent, pos, new Vector3(0.22f, 0.11f, 0.01f), mat, false);
            else
            {
                var disc = Prim(PrimitiveType.Cylinder, "Badge", parent, pos, new Vector3(0.13f, 0.005f, 0.13f), mat, false);
                disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            MakeText(parent, "BadgeText", badge, pos + new Vector3(0f, 0f, -0.012f), trigger ? 0.05f : 0.08f, new Color(0.05f, 0.05f, 0.08f));
            LeftText(parent, "Main", main, pos + new Vector3(0.16f, 0.035f, 0f), 0.07f, color);
            LeftText(parent, "Sub", sub, pos + new Vector3(0.16f, -0.05f, 0f), 0.045f, new Color(0.85f, 0.85f, 0.9f));
        }

        void LeftText(Transform parent, string name, string text, Vector3 pos, float height, Color color)
        {
            var tm = MakeText(parent, name, text, pos, height, color);
            tm.anchor = TextAnchor.MiddleLeft;
            tm.alignment = TextAlignment.Left;
        }

        // Quest 3 のコントローラーのボタンの並び (丸い灰色の面の上。上にスティック、その下に3つ):
        //   左手 = メニュー (≡, 左) / X (真ん中の下) / Y (右)、右手 = B (左) / A (真ん中の下) / Meta (∞, 右)
        static readonly Color ControllerGray = new Color(0.36f, 0.37f, 0.39f);

        /// <summary>モードの看板の右に飾る絵</summary>
        public const string PocochiImagePath = "Assets/Source/POCO/images/pocochi.jpg";

        /// <summary>
        /// コントローラーの絵 (Quest 3 を上から見た形): 大きな丸い灰色の面に、上のスティックと3つのボタン。面の下から外側へ白い握りが伸びる。
        /// 握りの横に赤いトリガー。ボタンの色は説明の印と同じ (Y・メニュー・Meta は使わないので灰色)
        /// </summary>
        void ControllerFigure(Transform parent, Vector3 pos, bool right)
        {
            var c = Group(parent, right ? "RightController" : "LeftController");
            c.localPosition = pos;
            float s = right ? 1f : -1f;
            var body = Mat("Controller_Body", new Color(0.9f, 0.9f, 0.92f), new Color(0.15f, 0.15f, 0.17f));
            var face = Mat("Controller_Face", ControllerGray, ControllerGray * 0.25f);
            var dark = Mat("Controller_Stick", new Color(0.12f, 0.12f, 0.13f));
            // 白い握り (面の下から外側の下へ)
            var grip = Prim(PrimitiveType.Capsule, "Grip", c, new Vector3(s * 0.1f, -0.19f, 0.004f), new Vector3(0.1f, 0.15f, 0.04f), body, false);
            grip.transform.localRotation = Quaternion.Euler(0f, 0f, s * 28f);
            // 丸い灰色の面
            var disc = Prim(PrimitiveType.Cylinder, "Face", c, new Vector3(0f, 0f, -0.01f), new Vector3(0.3f, 0.005f, 0.3f), face, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // スティック (面の上の方)
            var stick = Prim(PrimitiveType.Cylinder, "Stick", c, new Vector3(0f, 0.07f, -0.02f), new Vector3(0.075f, 0.005f, 0.075f), dark, false);
            stick.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // 3つのボタン
            if (right)
            {
                Button(c, new Vector3(-0.07f, -0.01f, -0.02f), "B", EndlessColor);
                Button(c, new Vector3(0f, -0.055f, -0.02f), "A", ResetColor);
                Button(c, new Vector3(0.07f, -0.01f, -0.02f), "∞", ControllerGray * 0.6f, 0.04f, 0.035f);
            }
            else
            {
                Button(c, new Vector3(-0.07f, -0.01f, -0.02f), "≡", ControllerGray * 0.6f, 0.04f, 0.035f);
                Button(c, new Vector3(0f, -0.055f, -0.02f), "X", DebugColor);
                Button(c, new Vector3(0.07f, -0.01f, -0.02f), "Y", new Color(0.5f, 0.5f, 0.55f));
            }
            // トリガー (握りの内側の横)
            Prim(PrimitiveType.Cube, "Trigger", c, new Vector3(-s * 0.02f, -0.17f, 0f), new Vector3(0.05f, 0.1f, 0.03f), Mat("Controls_TRIG", TriggerColor, TriggerColor * 0.6f, true), false);
        }

        /// <summary>
        /// 手に持つコントローラーの立体の模型 (Quest 3 の形。タイトルで操作方法の看板を見ている間だけ、銃の代わりに出す。PlayerRig.SetControllerView)。
        /// 上に大きな丸い灰色の面 (少しプレイヤーの方へ傾く) と、スティック・3つのボタン。その下に白い頭と、手のひらの中へ伸びる白い握り。前に赤いトリガー
        /// </summary>
        GameObject HandControllerModel(Transform hand, bool right)
        {
            var root = Group(hand, right ? "RightControllerModel" : "LeftControllerModel");
            float s = right ? 1f : -1f;
            var body = Mat("Controller_Body", new Color(0.9f, 0.9f, 0.92f), new Color(0.15f, 0.15f, 0.17f));
            var face = Mat("Controller_Face", ControllerGray, ControllerGray * 0.25f);
            var dark = Mat("Controller_Stick", new Color(0.12f, 0.12f, 0.13f));
            var small = Mat("Controller_SmallButton", ControllerGray * 0.6f, null);
            // 白い頭 (面の下) と、手のひらの中へ伸びる握り (下へ、少し後ろと外側へ)
            Prim(PrimitiveType.Sphere, "Head", root, new Vector3(0f, -0.004f, 0f), new Vector3(0.064f, 0.026f, 0.064f), body, false);
            var grip = Prim(PrimitiveType.Capsule, "Grip", root, new Vector3(s * 0.008f, -0.05f, -0.028f), new Vector3(0.034f, 0.05f, 0.04f), body, false);
            grip.transform.localRotation = Quaternion.Euler(-25f, 0f, -s * 10f);
            // 丸い灰色の面 (上の面が少しプレイヤーの方を向く)。ボタンは面の上 (面のローカル XZ。+Z が先の方)
            var faceRoot = Group(root, "FaceRoot");
            faceRoot.localPosition = new Vector3(0f, 0.008f, 0f);
            faceRoot.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            Prim(PrimitiveType.Cylinder, "Face", faceRoot, Vector3.zero, new Vector3(0.066f, 0.004f, 0.066f), face, false);
            Prim(PrimitiveType.Cylinder, "StickBase", faceRoot, new Vector3(0f, 0.004f, 0.014f), new Vector3(0.018f, 0.002f, 0.018f), dark, false);
            Prim(PrimitiveType.Sphere, "StickTop", faceRoot, new Vector3(0f, 0.01f, 0.014f), new Vector3(0.017f, 0.009f, 0.017f), dark, false);
            Material Btn(string letter, Color col) => Mat($"Controls_Button_{letter}", col, col * 0.6f, true);
            var low = right ? Btn("A", ResetColor) : Btn("X", DebugColor);
            var side = right ? Btn("B", EndlessColor) : Btn("Y", new Color(0.5f, 0.5f, 0.55f));
            Prim(PrimitiveType.Cylinder, "ButtonLow", faceRoot, new Vector3(0f, 0.003f, -0.017f), new Vector3(0.012f, 0.003f, 0.012f), low, false);       // A / X (真ん中の下)
            Prim(PrimitiveType.Cylinder, "ButtonSide", faceRoot, new Vector3(-s * 0.016f, 0.003f, -0.004f), new Vector3(0.012f, 0.003f, 0.012f), side, false); // B (右手の左) / Y (左手の右)
            Prim(PrimitiveType.Cylinder, "ButtonSystem", faceRoot, new Vector3(s * 0.016f, 0.003f, -0.004f), new Vector3(0.009f, 0.002f, 0.009f), small, false); // Meta / メニュー
            // 前のトリガー (人差し指)
            var trigger = Prim(PrimitiveType.Cube, "Trigger", root, new Vector3(0f, -0.02f, 0.026f), new Vector3(0.016f, 0.03f, 0.012f), Mat("Controls_TRIG", TriggerColor, TriggerColor * 0.6f, true), false);
            trigger.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        void Button(Transform parent, Vector3 pos, string letter, Color color, float size = 0.065f, float textHeight = 0.045f)
        {
            var b = Prim(PrimitiveType.Cylinder, $"Button_{letter}", parent, pos, new Vector3(size, 0.005f, size), Mat($"Controls_Button_{letter}", color, color * 0.6f, true), false);
            b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MakeText(parent, $"Label_{letter}", letter, pos + new Vector3(0f, 0f, -0.006f), textHeight, new Color(0.05f, 0.05f, 0.08f));
        }
    }
}
