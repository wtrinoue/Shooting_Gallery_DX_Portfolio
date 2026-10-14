using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>屋台1つ分。的の一括制御と、フィーバー時のせり上がり / 輪が広がるときの移動演出。</summary>
    public class Stall : MonoBehaviour
    {
        public Target[] targets;
        public float hiddenDepth = 4f;

        Vector3 restPos;
        Quaternion restRot;
        bool restPosCaptured;
        Coroutine moveRoutine;
        bool enableTargetsOnArrive; // せり上がり途中に Slide されても、着いたら的を出す

        /// <summary>表示中で、せり上がり / 沈み込みの途中でない</summary>
        public bool IsSettled => gameObject.activeInHierarchy && moveRoutine == null && !worldScaling;
        /// <summary>止まる場所 (地上に出ている時の位置と向き。親のローカル座標)。動いている途中でも行き先を返す</summary>
        public Vector3 RestPosition { get { CaptureRestPos(); return restPos; } }
        public Quaternion RestRotation { get { CaptureRestPos(); return restRot; } }

        Vector3 restScale; // 元の大きさ (入れ替えの演出で横に縮める前)
        bool worldScaling; // 怪獣モードで床といっしょに小さく / 大きくなっている途中

        void Awake()
        {
            CaptureRestPos();
            CaptureScale();
        }

        void CaptureScale()
        {
            if (restScale == Vector3.zero) restScale = transform.localScale;
        }

        void CaptureRestPos()
        {
            if (restPosCaptured) return;
            restPos = transform.localPosition;
            restRot = transform.localRotation;
            restPosCaptured = true;
        }

        public void SetTargetsAvailable(bool value)
        {
            // しまう時は、せり上がり途中の「着いたら的を出す」も取り消す (ゲーム終了後に的が出てこないように)
            if (!value) enableTargetsOnArrive = false;
            foreach (var t in targets)
                if (t != null) t.SetAvailable(value);
        }

        /// <summary>
        /// 止まる場所に瞬時に出す (動きを見せない)。enableTargets なら的もすぐ出す。
        /// 西部劇モードの帰り: 砂嵐で何も見えない間に屋台と的を並べておき、晴れた時にはもう遊べるようにする
        /// </summary>
        public void ShowImmediate(bool enableTargets)
        {
            CaptureRestPos();
            CaptureScale();
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
            worldScaling = false;
            enableTargetsOnArrive = false;
            gameObject.SetActive(true);
            transform.localPosition = restPos;
            transform.localRotation = restRot;
            transform.localScale = restScale;
            SetTargetsAvailable(enableTargets);
        }

        /// <summary>地面の下へ瞬時に隠す (起動時用 / 発掘モードで、プレイヤーから見えなくなった屋台を隠す時)</summary>
        public void HideImmediate()
        {
            CaptureRestPos();
            CaptureScale();
            SetTargetsAvailable(false);
            transform.localScale = restScale;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
            gameObject.SetActive(false);
        }

        /// <summary>隠れている屋台の置き場所を変える (フィーバーのたびにランダムな場所へ並べ替える用)</summary>
        public void PlaceAt(Vector3 localPos, Quaternion localRot)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
            CaptureScale();
            transform.localScale = restScale;
            restPos = localPos;
            restRot = localRot;
            restPosCaptured = true;
            transform.localRotation = localRot;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
        }

        /// <summary>地下からせり上がる。enableTargets = false なら的は出さない (ゲーム終了時に屋台だけ戻す時など)</summary>
        public void Rise(float duration, bool enableTargets = true)
        {
            CaptureRestPos();
            gameObject.SetActive(true);
            enableTargetsOnArrive = enableTargets;
            Move(restPos, transform.localRotation, duration, null);
        }

        public void Sink(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            Move(restPos + Vector3.down * hiddenDepth, transform.localRotation, duration, () => gameObject.SetActive(false));
        }

        /// <summary>
        /// 出ている屋台を地上のまま別の場所へ滑らせる (輪が広がるときに後ろへ下がる / 元の位置へ戻る用)。
        /// 的は出したまま動く。隠れている屋台なら置き場所だけ変える。
        /// </summary>
        public void Slide(Vector3 localPos, Quaternion localRot, float duration)
        {
            CaptureRestPos();
            restPos = localPos;
            restRot = localRot;
            if (!gameObject.activeInHierarchy)
            {
                transform.localRotation = localRot;
                transform.localPosition = restPos + Vector3.down * hiddenDepth;
                return;
            }
            Move(restPos, localRot, duration, null);
        }

        /// <summary>
        /// 豪華屋台への入れ替え用: その場でカードを裏返すように横へ縮んで消える (沈まない)。的はすぐしまう。
        /// 消えた後は地下の置き場所に戻しておく (次に Rise / SwapIn で出せるように)。
        /// </summary>
        public void SwapOut(float duration)
        {
            if (!gameObject.activeInHierarchy) return;
            CaptureRestPos();
            CaptureScale();
            enableTargetsOnArrive = false;
            SetTargetsAvailable(false);
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(Squash(1f, 0f, duration, () =>
            {
                transform.localScale = restScale;
                transform.localPosition = restPos + Vector3.down * hiddenDepth;
                gameObject.SetActive(false);
            }));
        }

        /// <summary>豪華屋台への入れ替え用: 止まる場所で、細い線から横へぱっと広がって現れる (少し行き過ぎて戻る)。着いたら的を出す</summary>
        public void SwapIn(float duration, bool enableTargets = true)
        {
            CaptureRestPos();
            CaptureScale();
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
            gameObject.SetActive(true);
            transform.localPosition = restPos;
            transform.localRotation = restRot;
            enableTargetsOnArrive = enableTargets;
            moveRoutine = StartCoroutine(Squash(0f, 1f, duration, null));
        }

        // 横幅を from → to に変える (縮む時はだんだん速く、広がる時は少し行き過ぎて戻る)。途中で少しだけ縦に伸びる
        IEnumerator Squash(float from, float to, float duration, System.Action onDone)
        {
            bool grow = to > from;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float k = grow ? EaseOutBack(t) : t * t;
                float w = Mathf.Max(0.001f, Mathf.LerpUnclamped(from, to, k));
                transform.localScale = new Vector3(restScale.x * w, restScale.y * (1f + 0.06f * Mathf.Sin(t * Mathf.PI)), restScale.z);
                yield return null;
            }
            transform.localScale = grow ? restScale : new Vector3(restScale.x * 0.001f, restScale.y, restScale.z);
            moveRoutine = null;
            if (enableTargetsOnArrive)
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(true);
            }
            onDone?.Invoke();
        }

        // ---- 怪獣モード: 床 (地面・穴のふち・ボーナスステージのハッチ) と屋台をまとめて、プレイヤーの足元を中心に小さくする ----
        // 大きさは GameManager が全部の部品に同じ倍率で毎フレーム渡す (屋台ごとにばらばらに縮まず、世界全体が1つになって小さくなる)

        /// <summary>まとめて小さくする / 大きくする準備。growing なら隠れていた屋台を出し、着いたら的を出す (enableTargets)。縮める時は的をすぐしまう</summary>
        public bool BeginWorldScale(bool growing, bool enableTargets = true)
        {
            if (!growing && !gameObject.activeInHierarchy) return false;
            CaptureRestPos();
            CaptureScale();
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
            worldScaling = true;
            if (growing)
            {
                gameObject.SetActive(true);
                transform.localRotation = restRot;
                enableTargetsOnArrive = enableTargets;
            }
            else
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(false);
            }
            return true;
        }

        /// <summary>親の原点 (プレイヤーの足元) を中心に、位置と大きさを元の s 倍にする</summary>
        public void SetWorldScale(float s)
        {
            transform.localPosition = restPos * s;
            transform.localScale = restScale * s;
        }

        /// <summary>小さくなりきった: 元の大きさに戻して地下の置き場所に隠す</summary>
        public void FinishWorldShrink()
        {
            worldScaling = false;
            transform.localScale = restScale;
            transform.localPosition = restPos + Vector3.down * hiddenDepth;
            gameObject.SetActive(false);
        }

        /// <summary>元の大きさに戻りきった: 的を出す</summary>
        public void FinishWorldGrow()
        {
            worldScaling = false;
            SetWorldScale(1f);
            if (!enableTargetsOnArrive) return;
            enableTargetsOnArrive = false;
            SetTargetsAvailable(true);
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.6f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * t * t * t + c * t * t;
        }

        void Move(Vector3 toPos, Quaternion toRot, float duration, System.Action onDone)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            CaptureScale();
            transform.localScale = restScale; // 入れ替えの途中で止められても元の大きさに戻す
            worldScaling = false;
            moveRoutine = StartCoroutine(MoveTo(toPos, toRot, duration, onDone));
        }

        IEnumerator MoveTo(Vector3 toPos, Quaternion toRot, float duration, System.Action onDone)
        {
            Vector3 fromPos = transform.localPosition;
            Quaternion fromRot = transform.localRotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease out
                transform.localPosition = Vector3.LerpUnclamped(fromPos, toPos, e);
                transform.localRotation = Quaternion.SlerpUnclamped(fromRot, toRot, e);
                yield return null;
            }
            transform.localPosition = toPos;
            transform.localRotation = toRot;
            moveRoutine = null;
            if (enableTargetsOnArrive)
            {
                enableTargetsOnArrive = false;
                SetTargetsAvailable(true);
            }
            onDone?.Invoke();
        }
    }
}
