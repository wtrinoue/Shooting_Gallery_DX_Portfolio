using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ボーナスステージ (床下の地下室) が開く時に、屋台ごとに目の前に出る「下を見て」の矢印のパネル (BonusStage が屋台の数だけ複製して使う)。
    /// 屋台の手前の床に小さなハッチが現れて開き、中から台がすっとせり上がる。台の柱の上の、矢印の形に切り抜いたパネルは、
    /// 地面に対して 45 度下 (プレイヤーの足元の方) を指して、ぴょこぴょこ動く。
    /// しまう時は、台が下がってハッチが閉じて消える。ローカルの +Z が穴の真ん中 (プレイヤー) の方
    /// </summary>
    public class BonusArrow : MonoBehaviour
    {
        [Tooltip("ハッチ全部の親 (ここを中心にふくらむ / 縮む)")]
        public Transform body;
        [Tooltip("2枚の扉と、扉の合わせ目の線 (左 / 右の順)")]
        public Transform[] doors, seams;
        [Tooltip("せり上がる台 (柱と、矢印の形のパネル)")]
        public Transform lift;
        [Tooltip("ぴょこぴょこ動かす矢印のパネル")]
        public Transform arrow;
        [Tooltip("ハッチの穴の横の長さ / 奥行き (m)")]
        public float length = 0.6f, width = 0.3f;
        [Tooltip("台が下がりきった時の深さ (パネルの上のはしまで床の下に入る)")]
        public float liftDepth = 0.55f;

        const float GrowTime = 0.15f, DoorTime = 0.25f, RiseTime = 0.4f, LowerTime = 0.3f, ShrinkTime = 0.2f;
        Coroutine routine;
        Vector3 arrowHome;
        bool showing;

        /// <summary>出ている (しまう途中を含まない)</summary>
        public bool Showing => showing;

        bool homeCaptured;

        // 矢印のパネルの止まる位置 (柱の先) を覚える。動かす前に、最初に出す時に1回だけ
        // (Awake で覚えると、ビルダーがこの部品を付けてから arrow を渡すまでの間に呼ばれて、原点 (床) を覚えてしまうことがある)
        void CaptureHome()
        {
            if (homeCaptured || arrow == null) return;
            arrowHome = arrow.localPosition;
            homeCaptured = true;
        }

        /// <summary>置き場所 (親のローカル座標の床の上。+Z を穴の真ん中へ向ける)。屋台といっしょに滑らせる時は毎フレーム呼ぶ</summary>
        public void Place(Vector3 localPos)
        {
            var flat = new Vector3(localPos.x, 0f, localPos.z);
            transform.localPosition = localPos;
            if (flat.sqrMagnitude > 0.0001f) transform.localRotation = Quaternion.LookRotation(-flat.normalized);
        }

        /// <summary>ハッチが開いて、パネルがせり上がって倒れる</summary>
        public void Show()
        {
            if (showing) return;
            showing = true;
            CaptureHome();
            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(true);
            Run(ShowRoutine(!wasActive));
        }

        /// <summary>パネルが起き上がって下がり、ハッチが閉じて消える</summary>
        public void Hide()
        {
            if (!showing) return;
            showing = false;
            if (!gameObject.activeInHierarchy) return;
            Run(HideRoutine());
        }

        /// <summary>その場で消す (ラウンドの終わりなど)</summary>
        public void HideImmediate()
        {
            showing = false;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            gameObject.SetActive(false);
        }

        void Run(IEnumerator r)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(r);
        }

        IEnumerator ShowRoutine(bool fresh)
        {
            if (fresh)
            {
                SetDoors(0f);
                SetLift(0f);
                body.localScale = Vector3.one * 0.001f;
            }
            float s0 = body.localScale.x;
            yield return Animate(GrowTime * (1f - s0), k => body.localScale = Vector3.one * Mathf.Lerp(s0, 1f, 1f - (1f - k) * (1f - k)));
            float d0 = doorOpen;
            yield return Animate(DoorTime * (1f - d0), k => SetDoors(Mathf.Lerp(d0, 1f, Mathf.SmoothStep(0f, 1f, k))));
            float l0 = liftUp;
            yield return Animate(RiseTime * (1f - l0), k => SetLift(Mathf.Lerp(l0, 1f, 1f - (1f - k) * (1f - k) * (1f - k))));
            // 矢印のパネルが、指す向き (パネルの下) へぴょこぴょこ動く
            for (float t = 0f; ; t += Time.deltaTime)
            {
                if (arrow != null) arrow.localPosition = arrowHome + arrow.localRotation * Vector3.down * (Mathf.Abs(Mathf.Sin(t * 5f)) * 0.08f);
                yield return null;
            }
        }

        IEnumerator HideRoutine()
        {
            CaptureHome();
            if (arrow != null) arrow.localPosition = arrowHome;
            float l0 = liftUp;
            yield return Animate(LowerTime * l0, k => SetLift(Mathf.Lerp(l0, 0f, k * k)));
            float d0 = doorOpen;
            yield return Animate(DoorTime * d0, k => SetDoors(Mathf.Lerp(d0, 0f, Mathf.SmoothStep(0f, 1f, k))));
            float s0 = body.localScale.x;
            yield return Animate(ShrinkTime, k => body.localScale = Vector3.one * Mathf.Max(0.001f, Mathf.Lerp(s0, 0f, k * k)));
            gameObject.SetActive(false);
            routine = null;
        }

        static IEnumerator Animate(float time, System.Action<float> step)
        {
            if (time > 0.001f)
                for (float t = 0f; t < 1f; t += Time.deltaTime / time)
                {
                    step(t);
                    yield return null;
                }
            step(1f);
        }

        float liftUp, doorOpen;

        // 台の高さ: 0 = 床の下に隠れている, 1 = 柱の根元が床と同じ高さ
        void SetLift(float k)
        {
            liftUp = k;
            lift.localPosition = new Vector3(0f, -liftDepth * (1f - k), 0f);
        }

        // 2枚の扉: 左右のはしにくっついたまま、見えている長さが短くなる (はしの床の下へすべり込んで見える)。0 = 閉じている
        void SetDoors(float open)
        {
            doorOpen = open;
            float half = length / 2f, len = Mathf.Max(0.001f, half * (1f - open));
            const float SeamW = 0.025f;
            for (int i = 0; i < 2 && i < doors.Length; i++)
            {
                float s = i == 0 ? -1f : 1f;
                doors[i].localPosition = new Vector3(s * (half - len / 2f), 0.01f, 0f);
                doors[i].localScale = new Vector3(len, 0.014f, width);
                doors[i].gameObject.SetActive(open < 0.999f);
                if (seams == null || i >= seams.Length) continue;
                seams[i].localPosition = new Vector3(s * (half - Mathf.Max(0f, len - SeamW / 2f)), 0.0185f, 0f);
                seams[i].localScale = new Vector3(Mathf.Min(SeamW, len), 0.003f, width);
                seams[i].gameObject.SetActive(open < 0.999f);
            }
        }
    }
}
