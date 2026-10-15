using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 操作方法の看板の生成。メイン屋台の反対側 (プレイヤーの背中側) に立ち、タイトルの間だけ表示される
    /// (ゲーム中はフィーバーの屋台が並ぶ場所なので GameManager が隠す)。
    /// 左右のコントローラーの絵と、ボタンごとの操作を色分けして書く (ボタンの色は長押しのインジケーターの色と同じ)。
    /// </summary>
    public partial class GalleryBuilder
    {
        static readonly Color TriggerColor = new Color(1f, 0.3f, 0.25f);
        static readonly Color ResetColor = new Color(1f, 0.45f, 0.15f);
        static readonly Color EndlessColor = new Color(0.3f, 1f, 0.85f);
        static readonly Color DebugColor = new Color(0.6f, 1f, 0.3f);

        GameObject BuildControlsBoard()
        {
            var board = Group(world, "ControlsBoard");
            board.localPosition = new Vector3(0f, 0f, -3.2f);
            board.localRotation = Quaternion.Euler(0f, 180f, 0f); // 表 (-Z) がプレイヤーの方を向く

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
            ControlRow(board, new Vector3(0.7f, 1.58f, -0.01f), "A", ResetColor, "A 長押し : 最初に戻る", "(リセット)");
            ControlRow(board, new Vector3(0.7f, 1.21f, -0.01f), "B", EndlessColor, "B 長押し : エンドレスモード", "(時間切れなし)");

            // 下: 遊び方
            MakeText(board, "HowTo1", "START の的を撃つとゲーム開始！", new Vector3(0f, 0.85f, -0.01f), 0.1f, new Color(1f, 0.85f, 0.3f));
            MakeText(board, "HowTo2", "特別な的を撃つと、武器やステージが変わるよ", new Vector3(0f, 0.65f, -0.01f), 0.07f, Color.white);

            BuildCredits(board, panel, gold);
            return board.gameObject;
        }

        /// <summary>
        /// 看板のいちばん下の「CREDITS / LICENSES」の的と、撃つと看板の前に開くクレジットとライセンス全文のパネル (CreditsViewer)。
        /// パネルは大きな文字でページに分けて出し、下の的 (前へ / 閉じる / 次へ) を撃ってページを送る。文章は Resources/THIRD_PARTY_NOTICES.txt
        /// </summary>
        void BuildCredits(Transform board, Material panelMat, Material gold)
        {
            var dark = new Color(0.05f, 0.05f, 0.08f);
            var open = CreditsButton(board, "CreditsButton", new Vector3(0f, 0.27f, -0.04f), new Vector2(2.3f, 0.26f), "CREDITS / LICENSES  (shoot)", new Color(0.95f, 0.8f, 0.35f), 0.075f, dark);

            var panel = Group(board, "CreditsPanel");
            panel.localPosition = new Vector3(0f, 1.6f, -0.14f); // 下はしが CREDITS の的より上 (開いている間も的が隠れない)
            Prim(PrimitiveType.Cube, "Back", panel, Vector3.zero, new Vector3(4.5f, 2.3f, 0.04f), panelMat, false);
            foreach (float y in new[] { -1.15f, 1.15f })
                Prim(PrimitiveType.Cube, "FrameH", panel, new Vector3(0f, y, -0.02f), new Vector3(4.54f, 0.04f, 0.03f), gold, false);
            foreach (float x in new[] { -2.25f, 2.25f })
                Prim(PrimitiveType.Cube, "FrameV", panel, new Vector3(x, 0f, -0.02f), new Vector3(0.04f, 2.34f, 0.03f), gold, false);
            MakeText(panel, "Title", "CREDITS & LICENSES", new Vector3(0f, 1f, -0.03f), 0.12f, new Color(1f, 0.85f, 0.35f));
            var page = MakeText(panel, "PageText", "", new Vector3(-2.1f, 0.86f, -0.03f), 0.075f, Color.white);
            page.anchor = TextAnchor.UpperLeft;
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
            viewer.openButton = open;
            viewer.prevButton = prev;
            viewer.nextButton = next;
            viewer.closeButton = close;
            panel.gameObject.SetActive(false);
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

        /// <summary>
        /// コントローラーの絵 (正面から見た形): 握り・ボタンの面・上の輪。右手は A (下) / B (上)、左手は X (下) / Y (上、使わないので灰色)。
        /// 握りの横に赤いトリガー。ボタンの色は説明の印と同じ
        /// </summary>
        void ControllerFigure(Transform parent, Vector3 pos, bool right)
        {
            var c = Group(parent, right ? "RightController" : "LeftController");
            c.localPosition = pos;
            float s = right ? 1f : -1f;
            var body = Mat("Controller_Body", new Color(0.85f, 0.86f, 0.9f), new Color(0.15f, 0.15f, 0.17f));
            var face = Mat("Controller_Face", new Color(0.2f, 0.21f, 0.24f), new Color(0.05f, 0.05f, 0.06f));
            var grip = Prim(PrimitiveType.Capsule, "Grip", c, new Vector3(s * 0.03f, -0.2f, 0f), new Vector3(0.13f, 0.17f, 0.04f), body, false);
            grip.transform.localRotation = Quaternion.Euler(0f, 0f, s * 12f);
            var disc = Prim(PrimitiveType.Cylinder, "Face", c, new Vector3(0f, 0.02f, -0.01f), new Vector3(0.26f, 0.005f, 0.26f), face, false);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ring = Group(c, "Ring");
            ring.localPosition = new Vector3(-s * 0.05f, 0.12f, 0f);
            RingSegments(ring, "Ring", 0.17f, 20, 0.03f, body);
            // 親指のスティック (使わない)
            var stick = Prim(PrimitiveType.Cylinder, "Stick", c, new Vector3(-s * 0.06f, 0.06f, -0.02f), new Vector3(0.06f, 0.005f, 0.06f), Mat("Controller_Stick", new Color(0.08f, 0.08f, 0.09f)), false);
            stick.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // ボタン (下 / 上)
            Button(c, new Vector3(s * 0.05f, -0.02f, -0.02f), right ? "A" : "X", right ? ResetColor : DebugColor);
            Button(c, new Vector3(s * 0.01f, 0.08f, -0.02f), right ? "B" : "Y", right ? EndlessColor : new Color(0.4f, 0.4f, 0.45f));
            // トリガー (握りの横)
            Prim(PrimitiveType.Cube, "Trigger", c, new Vector3(-s * 0.1f, -0.1f, 0f), new Vector3(0.05f, 0.12f, 0.03f), Mat("Controls_TRIG", TriggerColor, TriggerColor * 0.6f, true), false);
        }

        void Button(Transform parent, Vector3 pos, string letter, Color color)
        {
            var b = Prim(PrimitiveType.Cylinder, $"Button_{letter}", parent, pos, new Vector3(0.065f, 0.005f, 0.065f), Mat($"Controls_Button_{letter}", color, color * 0.6f, true), false);
            b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            MakeText(parent, $"Label_{letter}", letter, pos + new Vector3(0f, 0f, -0.006f), 0.045f, new Color(0.05f, 0.05f, 0.08f));
        }
    }
}
