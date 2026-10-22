using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// クレジットとライセンス全文のパネル (メイン屋台の右のクレジットの看板)。
    /// 文章 (Resources/THIRD_PARTY_NOTICES.txt) をページに分けて大きな文字で出し、パネルの下の的 (前へ / 次へ) を撃ってページを送る。
    /// openButton があれば、それを撃つと開く / 閉じる パネルになる (なければ、いつも開いたまま)。
    /// 使っているライブラリ (WebXR Export: Apache License 2.0) の「ライセンスの写しを渡す」条件を、どのビルド (PC・Quest・WebGL) でも満たすためのもの。
    /// 的は TargetKind.Credits (タイトルの間でも撃てる。GameManager.OnTargetHit から OnButton が呼ばれる)
    /// </summary>
    public class CreditsViewer : MonoBehaviour
    {
        [Tooltip("表示する文章 (クレジットとライセンス全文)")]
        public TextAsset notices;
        [Tooltip("開いた時に出るパネル (文章とページ送りの的)")]
        public GameObject panel;
        public TextMesh pageText, pageNumber;
        public Target openButton, prevButton, nextButton, closeButton;
        [Tooltip("1ページの行数")]
        public int linesPerPage = 15;
        [Tooltip("1行の最大の幅 (m。看板の内側に収まる幅)。これより長い行は、単語の切れ目 (日本語は文字の間) で折り返す")]
        public float lineWidth = 4.1f;
        [Tooltip("フォントから文字の幅を測れない時の、1行の最大文字数")]
        public int wrapColumns = 82;

        readonly List<string> pages = new List<string>();
        readonly List<Renderer> boardTexts = new List<Renderer>(); // 看板のほかの文字 (パネルを透けて見えるので、開いている間は隠す)
        int page;
        Coroutine pop;

        bool AlwaysOpen => openButton == null;

        void Awake()
        {
            BuildPages();
            if (AlwaysOpen) return;
            if (panel != null) panel.SetActive(false);
            // 文字 (TextMesh) は前にある板を透けて描かれるので、パネルの後ろになる看板の文字を覚えておく (パネルと CREDITS の的の文字は除く)
            foreach (var tm in GetComponentsInChildren<TextMesh>(true))
            {
                var t = tm.transform;
                if ((panel != null && t.IsChildOf(panel.transform)) || (openButton != null && t.IsChildOf(openButton.transform))) continue;
                boardTexts.Add(tm.GetComponent<Renderer>());
            }
        }

        // いつも開いたままのパネルは、看板が出るたびに1ページ目から
        void OnEnable()
        {
            if (AlwaysOpen) Refresh();
        }

        // (その場で生成した時は、AddComponent の瞬間に Awake / OnEnable が走り、文章を渡す前なので、ここでもう一度出す)
        void Start()
        {
            if (AlwaysOpen) Refresh();
        }

        /// <summary>パネルを出して1ページ目を表示する (生成した直後にも呼ぶ。シーンに生成した時も1ページ目が見えるように)</summary>
        public void Refresh()
        {
            if (panel != null) panel.SetActive(true);
            ShowPage(0);
        }

        /// <summary>的が撃たれた (GameManager から)</summary>
        public void OnButton(Target target)
        {
            if (target == null) return;
            if (!AlwaysOpen && target == openButton) { if (panel != null && panel.activeSelf) Close(); else Open(); }
            else if (target == nextButton) ShowPage(page + 1);
            else if (target == prevButton) ShowPage(page - 1);
            else if (target == closeButton) Close();
        }

        void Open()
        {
            if (panel == null) return;
            panel.SetActive(true);
            SetBoardTexts(false);
            ShowPage(0);
            Sfx.Play("pickup");
            if (pop != null) StopCoroutine(pop);
            pop = StartCoroutine(Pop());
        }

        void Close()
        {
            if (panel == null) return;
            panel.SetActive(false);
            SetBoardTexts(true);
            Sfx.Play("hit");
        }

        // ゲームが始まって看板ごと隠れた時は、パネルを閉じておく (次のタイトルでは閉じた状態から)
        void OnDisable()
        {
            if (AlwaysOpen) return;
            if (panel != null) panel.SetActive(false);
            SetBoardTexts(true);
        }

        void SetBoardTexts(bool show)
        {
            foreach (var r in boardTexts) if (r != null) r.enabled = show;
        }

        void ShowPage(int index)
        {
            if (pages.Count == 0) BuildPages(); // (Awake の時はまだ文章が渡されていなかった)
            if (pages.Count == 0) return;
            page = (index % pages.Count + pages.Count) % pages.Count; // 最後の次は最初へ、最初の前は最後へ
            if (pageText != null) pageText.text = pages[page];
            if (pageNumber != null) pageNumber.text = $"{page + 1} / {pages.Count}";
            if (index != 0) Sfx.Play("hit");
        }

        // ぽんっとふくらんで出る
        IEnumerator Pop()
        {
            var t = panel.transform;
            for (float k = 0f; k < 1f; k += Time.deltaTime / 0.25f)
            {
                float x = k - 1f;
                t.localScale = Vector3.one * Mathf.Max(0.001f, 1f + 2.7f * x * x * x + 1.7f * x * x);
                yield return null;
            }
            t.localScale = Vector3.one;
            pop = null;
        }

        // 文章を行に分け (lineWidth より長い行は、単語の切れ目で折り返す。単語ごと入らない時は文字の間で)、linesPerPage 行ずつのページにする
        void BuildPages()
        {
            pages.Clear();
            if (notices == null) return;
            const string Indent = "    "; // 折り返した続きの行は字下げする
            var lines = new List<string>();
            foreach (var raw in notices.text.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                while (!Fits(line))
                {
                    int cut = FitLength(line);
                    int space = line.LastIndexOf(' ', Mathf.Min(cut, line.Length - 1));
                    if (space > Indent.Length && space >= cut / 2) cut = space; // 単語の切れ目で (行の前半しか残らない時は、文字の間で)
                    cut = Mathf.Max(cut, Indent.Length + 1);
                    lines.Add(line.Substring(0, cut).TrimEnd());
                    line = Indent + line.Substring(cut).TrimStart();
                }
                lines.Add(line);
            }
            for (int i = 0; i < lines.Count; i += linesPerPage)
            {
                // ページの先頭の空行は詰める
                int start = i;
                while (start < lines.Count && start < i + linesPerPage && lines[start].Length == 0) start++;
                int end = Mathf.Min(lines.Count, i + linesPerPage);
                if (start >= end) continue;
                pages.Add(string.Join("\n", lines.GetRange(start, end - start)));
            }
        }

        // ---- 文字の幅 (フォントの文字ごとの送り幅から、pageText の座標の m で測る) ----

        bool Fits(string s) => FitLength(s) >= s.Length;

        // 先頭から何文字までなら lineWidth に収まるか
        int FitLength(string s)
        {
            var font = pageText != null ? pageText.font : null;
            if (font == null) return Mathf.Min(s.Length, wrapColumns);
            float scale = PixelScale();
            float width = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                width += Advance(font, s[i]) * scale;
                if (width > lineWidth) return i;
            }
            return s.Length;
        }

        int FontSize => pageText.fontSize > 0 ? pageText.fontSize : pageText.font.fontSize;

        // 1文字の送り幅 (フォントのピクセル)
        float Advance(Font font, char c)
        {
            font.RequestCharactersInTexture(c.ToString(), FontSize, pageText.fontStyle);
            return font.GetCharacterInfo(c, out var info, FontSize, pageText.fontStyle) ? info.advance : FontSize * 0.6f;
        }

        float pixelScale; // フォントの 1 ピクセルが何 m か (初めの1回だけ測る)

        // TextMesh では、フォントの 1 ピクセル = characterSize / 10 (m)。測れる時は、実際に文字を置いた幅から測って合わせる
        float PixelScale()
        {
            if (pixelScale > 0f) return pixelScale;
            pixelScale = pageText.characterSize * 0.1f;
            var r = pageText.GetComponent<Renderer>();
            if (r == null) return pixelScale;
            const string Sample = "The quick brown fox jumps over the lazy dog 0123456789";
            string old = pageText.text;
            pageText.text = Sample;
            float measured = r.localBounds.size.x;
            pageText.text = old;
            float pixels = 0f;
            foreach (char c in Sample) pixels += Advance(pageText.font, c);
            if (measured > 0.001f && pixels > 0f) pixelScale = measured / pixels;
            return pixelScale;
        }
    }
}
