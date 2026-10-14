using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 操作方法の看板の「CREDITS / LICENSES」の的を撃つと開く、クレジットとライセンス全文のパネル。
    /// 文章 (Resources/THIRD_PARTY_NOTICES.txt) をページに分けて大きな文字で出し、パネルの下の的 (前へ / 次へ / 閉じる) を撃ってページを送る。
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
        [Tooltip("1ページの行数 / 1行の最大文字数 (これより長い行は折り返す)")]
        public int linesPerPage = 15;
        public int wrapColumns = 82;

        readonly List<string> pages = new List<string>();
        readonly List<Renderer> boardTexts = new List<Renderer>(); // 看板のほかの文字 (パネルを透けて見えるので、開いている間は隠す)
        int page;
        Coroutine pop;

        void Awake()
        {
            BuildPages();
            if (panel != null) panel.SetActive(false);
            // 文字 (TextMesh) は前にある板を透けて描かれるので、パネルの後ろになる看板の文字を覚えておく (パネルと CREDITS の的の文字は除く)
            foreach (var tm in GetComponentsInChildren<TextMesh>(true))
            {
                var t = tm.transform;
                if ((panel != null && t.IsChildOf(panel.transform)) || (openButton != null && t.IsChildOf(openButton.transform))) continue;
                boardTexts.Add(tm.GetComponent<Renderer>());
            }
        }

        /// <summary>的が撃たれた (GameManager から)</summary>
        public void OnButton(Target target)
        {
            if (target == openButton) { if (panel != null && panel.activeSelf) Close(); else Open(); }
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
            if (panel != null) panel.SetActive(false);
            SetBoardTexts(true);
        }

        void SetBoardTexts(bool show)
        {
            foreach (var r in boardTexts) if (r != null) r.enabled = show;
        }

        void ShowPage(int index)
        {
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

        // 文章を行に分け (長い行は単語の切れ目で折り返す)、linesPerPage 行ずつのページにする
        void BuildPages()
        {
            pages.Clear();
            if (notices == null) return;
            var lines = new List<string>();
            foreach (var raw in notices.text.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                while (line.Length > wrapColumns)
                {
                    int cut = line.LastIndexOf(' ', wrapColumns);
                    if (cut <= 0) cut = wrapColumns;
                    lines.Add(line.Substring(0, cut).TrimEnd());
                    line = "    " + line.Substring(cut).TrimStart();
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
    }
}
