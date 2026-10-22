using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ラウンドの始まりの看板。タイトルの的 (2 MIN / 100,000 POINTS) を撃つと、上にプロペラの付いた看板が真上から降りてくる。
    /// 1行目にルール (「Get 100,000 points !!」など)、2行目に大きく数字。降りてきて落ち着いたら 3 → 2 → 1 → GO!! と数え、
    /// GO!! でラウンドが始まり (onGo)、看板はプロペラを速く回して真上へ飛んでいく。
    /// </summary>
    public class GoalSign : MonoBehaviour
    {
        [Tooltip("看板の見た目 (プロペラごと。降りてきて、飛んでいく)")]
        public Transform body;
        [Tooltip("回るプロペラ (ローカル Y まわり)")]
        public Transform rotor;
        [Tooltip("1行目の文字 (ルールの説明。Play で変える)")]
        public TextMesh headline;
        [Tooltip("2行目の大きな文字 (3, 2, 1, GO!!)")]
        public TextMesh countText;
        [Tooltip("止まる所 (この物のローカル)")]
        public Vector3 hoverPos = new Vector3(0f, 2.4f, 0f);
        [Tooltip("降りてくる高さ (止まる所より上 m) / 降りてくる秒数")]
        public float dropHeight = 7f;
        public float dropTime = 1.8f;
        [Tooltip("止まってから数え始めるまで / 数字1つの秒数")]
        public float settleTime = 0.5f;
        public float countInterval = 0.9f;
        [Tooltip("飛んでいく秒数")]
        public float leaveTime = 1.6f;

        Coroutine routine;
        float rotorSpeed;

        public bool IsPlaying => routine != null;

        void Awake() => Hide();

        /// <summary>真上から降りてきて数え、GO!! で onGo を呼んでから真上へ飛んでいく。text = 1行目の文字、onCount = 3 を出して数え始めた時</summary>
        public void Play(string text, System.Action onGo, System.Action onCount = null)
        {
            Stop();
            if (headline != null && !string.IsNullOrEmpty(text)) headline.text = text;
            body.gameObject.SetActive(true);
            routine = StartCoroutine(Run(onGo, onCount));
        }

        /// <summary>途中でやめて隠す (リセットされた時など)</summary>
        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Hide();
        }

        void Hide()
        {
            if (body != null) body.gameObject.SetActive(false);
        }

        void Update()
        {
            if (rotor != null && body.gameObject.activeSelf) rotor.Rotate(0f, rotorSpeed * Time.deltaTime, 0f, Space.Self);
        }

        IEnumerator Run(System.Action onGo, System.Action onCount)
        {
            SetCount("3");
            rotorSpeed = 900f;
            body.localScale = Vector3.one;
            body.localRotation = Quaternion.identity;
            // 1. 真上から降りてくる (少し行き過ぎて、ふわっと戻って止まる)
            for (float t = 0f; t < dropTime; t += Time.deltaTime)
            {
                body.localPosition = hoverPos + Vector3.up * (dropHeight * (1f - EaseOutBack(t / dropTime)));
                yield return null;
            }
            // 2. 落ち着く (ふわふわ浮かぶ)
            float hover = 0f;
            for (float t = 0f; t < settleTime; t += Time.deltaTime)
            {
                Hover(hover += Time.deltaTime);
                yield return null;
            }
            // 3. 3, 2, 1 と数える (数字が変わるたびに、ぽんっとふくらむ)
            onCount?.Invoke(); // 数え始めた (ラウンドの BGM がここから流れる)
            foreach (var n in new[] { "3", "2", "1" })
            {
                SetCount(n);
                Sfx.Play("click");
                for (float t = 0f; t < countInterval; t += Time.deltaTime)
                {
                    Pop(t);
                    Hover(hover += Time.deltaTime);
                    yield return null;
                }
            }
            // 4. GO!!: ラウンド開始。少し見せてから、プロペラを速く回して真上へ飛んでいく
            SetCount("GO!!");
            countText.color = new Color(1f, 0.85f, 0.2f);
            onGo?.Invoke();
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                Pop(t);
                Hover(hover += Time.deltaTime);
                yield return null;
            }
            rotorSpeed = 2200f;
            Vector3 from = body.localPosition;
            for (float t = 0f; t < leaveTime; t += Time.deltaTime)
            {
                float k = t / leaveTime;
                body.localPosition = from + Vector3.up * (k * k * 16f); // 真上へ
                yield return null;
            }
            routine = null;
            Hide();
        }

        // ふわふわ浮かぶ (上下だけ)
        void Hover(float t) => body.localPosition = hoverPos + Vector3.up * (Mathf.Sin(t * 2.2f) * 0.05f);

        void Pop(float t)
        {
            float k = Mathf.Clamp01(t / 0.25f);
            countText.transform.localScale = Vector3.one * (1f + 0.45f * (1f - k) * (1f - k));
        }

        void SetCount(string s)
        {
            countText.text = s;
            countText.color = Color.white;
            countText.transform.localScale = Vector3.one;
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.4f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * t * t * t + c * t * t;
        }
    }
}
