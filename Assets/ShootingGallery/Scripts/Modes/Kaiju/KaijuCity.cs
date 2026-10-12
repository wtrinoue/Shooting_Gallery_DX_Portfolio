using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 怪獣モードの舞台。屋台がプレイヤーの足元へ向かって小さくなって消えたあと、まわりに遠くまで続く街 (ビル・公園の木・商店街) が
    /// プレイヤーの近くから遠くへ順に地面から伸びてくる。プレイヤーは巨大な怪獣になった気分で、手の怪獣の頭から炎の玉を吐いて
    /// 色のついた的のビルを壊す。空は青く、雲が流れ、太陽が出ている。
    /// 屋台の出し入れ・点数・終わる時間の判断は GameManager が行い、ここは街の見た目と出し入れだけを受け持つ。プレイヤーは動かない。
    /// </summary>
    public class KaijuCity : MonoBehaviour
    {
        [Tooltip("地面から伸びてくる建物 (根元が地面のグループ)。近い順でなくてよい")]
        public Transform[] lots;
        [Tooltip("的のビル")]
        public KaijuBuilding[] targets;
        [Tooltip("屋台といっしょに足元へ小さくなる床の部品 (地面・穴のふち・足元の目印・ボーナスステージのハッチ)。GameManager が縮める")]
        public Transform[] groundParts;
        [Tooltip("怪獣モードの間だけ表示する物 (街の地面・道路・雲・太陽)")]
        public GameObject[] showDuringStage;
        [Tooltip("流れる雲 (ConveyorBelt)")]
        public ConveyorBelt[] clouds;
        public float cloudSpeed = 1.2f;
        [Tooltip("街の地面の上面の高さ (大きい間はこの分だけお祭りの地面の下へ沈めておく)")]
        public float groundTop = 0.03f;

        [Header("Timing")]
        [Tooltip("始まってから終わるまでの秒数 (屋台が縮む・街が伸びる時間も含む)")]
        public float duration = 24f;
        [Tooltip("始まってから街が伸び始めるまでの秒数")]
        public float dropDelay = 0f;
        [Tooltip("建物1つが伸びきるまでの秒数")]
        public float riseTime = 0.5f;
        [Tooltip("一番近い建物から一番遠い建物まで、伸び始める時間のずれ (秒)。近くから遠くへ波のように広がる")]
        public float spreadTime = 1.6f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回壊したビルの数</summary>
        public int Destroyed { get; private set; }
        /// <summary>片付け (街が沈む) にかかる秒数</summary>
        public float CloseTime => riseTime + spreadTime * 0.5f;

        float startTime;
        Coroutine anim;
        Vector3[] lotScale;
        float[] lotDelay; // 近いほど早く伸びる (0〜1)

        void Awake()
        {
            lotScale = new Vector3[lots.Length];
            lotDelay = new float[lots.Length];
            float far = 1f;
            foreach (var l in lots) far = Mathf.Max(far, Flat(l.localPosition).magnitude);
            for (int i = 0; i < lots.Length; i++)
            {
                lotScale[i] = lots[i].localScale;
                lotDelay[i] = Flat(lots[i].localPosition).magnitude / far;
            }
            SetCity(0f, false);
            SetStageVisible(false);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>
        /// 怪獣モードを始める。街は bigScale 倍の大きさ (プレイヤーがふつうの人の大きさに見える、見上げるほどのビル) で、近くから遠くへ地面から伸びてくる。
        /// そのあと GameManager が屋台や床といっしょに SetCityScale で小さくしていき、FinishShrink で今の大きさに落ち着く (ここで的のビルが使えるようになる)
        /// </summary>
        public void Begin(float bigScale)
        {
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            Destroyed = 0;
            foreach (var c in clouds) c.speed = cloudSpeed;
            SetCityScale(bigScale, 0f);
            Play(Intro());
        }

        /// <summary>
        /// 街全体の大きさ (プレイヤーの足元が中心)。progress = 小さくなる途中の進み具合 (0〜1)。
        /// 大きい間は街の地面をお祭りの地面の少し下に沈めておき、お祭りの地面 (と屋台) が上に見えるようにする
        /// </summary>
        public void SetCityScale(float scale, float progress)
        {
            transform.localScale = Vector3.one * scale;
            transform.localPosition = Vector3.down * ((groundTop * scale + 0.01f) * (1f - Mathf.Clamp01(progress)));
        }

        /// <summary>小さくなりきった: 今の大きさに落ち着いて、的のビルを使えるようにする</summary>
        public void FinishShrink()
        {
            SetCityScale(1f, 1f);
            if (!IsActive) return;
            foreach (var b in targets) b.Activate();
        }

        /// <summary>街が伸びきるまでの秒数</summary>
        public float RiseDuration => dropDelay + riseTime + spreadTime;

        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            Play(Outro());
        }

        /// <summary>的のビルを壊した (GameManager から)</summary>
        public void RegisterHit() => Destroyed++;

        IEnumerator Intro()
        {
            yield return new WaitForSeconds(dropDelay);
            SetStageVisible(true);
            float total = riseTime + spreadTime;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                SetCity(t, false);
                yield return null;
            }
            SetCity(total, false);
            // 的のビルは、世界全体が小さくなりきってから (FinishShrink)
            anim = null;
        }

        IEnumerator Outro()
        {
            foreach (var b in targets) b.Deactivate();
            // 遠くから近くへ沈む (速め)
            float total = riseTime + spreadTime * 0.5f;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                SetCity(t, true);
                yield return null;
            }
            SetStageVisible(false);
            anim = null;
        }

        // t = 始まってからの秒数。sinking なら逆向き (遠くの建物から先に沈む)
        void SetCity(float t, bool sinking)
        {
            for (int i = 0; i < lots.Length; i++)
            {
                float k;
                if (!sinking) k = Mathf.Clamp01((t - lotDelay[i] * spreadTime) / riseTime);
                else k = 1f - Mathf.Clamp01((t - (1f - lotDelay[i]) * spreadTime * 0.5f) / riseTime);
                // 伸びる時は少し行き過ぎて戻る (ニョキッと生える)
                float e = sinking ? k * k : EaseOutBack(k);
                var s = lotScale[i];
                lots[i].localScale = new Vector3(s.x, s.y * Mathf.Max(0.001f, e), s.z);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.5f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * t * t * t + c * t * t;
        }

        void SetStageVisible(bool visible)
        {
            foreach (var l in lots) l.gameObject.SetActive(visible);
            if (showDuringStage != null)
                foreach (var go in showDuringStage) if (go != null) go.SetActive(visible);
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
