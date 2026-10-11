using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// スナイパーチャレンジの「遠くの大きな的」の出し入れ。
    /// 進行 (屋台を引っ込める・武器を渡す・成功/失敗の判定) は GameManager が行い、ここは見た目と時間だけを受け持つ。
    /// 的は屋台があった方向の遠く・高い所に浮かんで現れ、プレイヤーの方を向く。
    /// 出ている間は霧を遠ざけて、遠くの的がはっきり見えるようにする。
    /// </summary>
    public class SniperChallenge : MonoBehaviour
    {
        [Tooltip("動かす / 拡大縮小する親 (大きな的と飾り)")]
        public Transform root;
        [Tooltip("遠くの大きな的 (kind = SniperBig)")]
        public Target bigTarget;

        [Header("Placement")]
        [Tooltip("プレイヤーから的までの水平距離 (m)")]
        public float distance = 22f;
        [Tooltip("的の中心の高さ (m)")]
        public float height = 3.5f;
        [Tooltip("ふわふわ上下に揺れる幅 (m)")]
        public float bobAmplitude = 0.15f;
        public float bobSpeed = 1.2f;

        [Header("Timing")]
        [Tooltip("撃たずにこの秒数たつと失敗")]
        public float timeLimit = 10f;
        public float appearTime = 0.6f;
        public float vanishTime = 0.4f;

        [Header("Fog")]
        [Tooltip("出ている間だけ霧を遠ざける")]
        public bool clearFog = true;
        public float clearFogEnd = 70f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, timeLimit - (Time.time - startTime)) : 0f;

        float startTime;
        Vector3 basePos;
        Coroutine anim;
        float savedFogStart, savedFogEnd;
        bool fogSaved;

        void Awake()
        {
            if (root != null) root.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsActive || root == null) return;
            root.localPosition = basePos + Vector3.up * Mathf.Sin((Time.time - startTime) * bobSpeed * Mathf.PI * 2f) * bobAmplitude;
        }

        /// <summary>direction (水平) の遠くに的を出す。eye = プレイヤーの目の位置 (的をそちらに向ける)</summary>
        public void Show(Vector3 direction, Vector3 eye)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            direction.Normalize();

            IsActive = true;
            startTime = Time.time;
            var parent = root.parent;
            Vector3 worldPos = (parent != null ? parent.position : Vector3.zero) + direction * distance + Vector3.up * height;
            basePos = parent != null ? parent.InverseTransformPoint(worldPos) : worldPos;
            root.localPosition = basePos;
            root.rotation = Quaternion.LookRotation(worldPos - eye); // 的の表 (-Z) をプレイヤーに向ける
            root.gameObject.SetActive(true);

            bigTarget.SetAvailable(true);
            bigTarget.Raise(timeLimit + 60f); // 消す時は Hide で引っ込める

            if (clearFog)
            {
                if (!fogSaved)
                {
                    savedFogStart = RenderSettings.fogStartDistance;
                    savedFogEnd = RenderSettings.fogEndDistance;
                    fogSaved = true;
                }
            }
            Play(Scale(0f, 1f, appearTime, false));
        }

        /// <summary>的を消す。hit = true なら倒れるのを少し見せてから縮んで消える</summary>
        public void Hide(bool hit)
        {
            if (!IsActive) return;
            IsActive = false;
            if (!hit) bigTarget.SetAvailable(false);
            Play(HideRoutine(hit ? 0.7f : 0f));
        }

        IEnumerator HideRoutine(float wait)
        {
            if (wait > 0f) yield return new WaitForSeconds(wait);
            yield return Scale(root.localScale.x, 0f, vanishTime, true);
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }

        IEnumerator Scale(float from, float to, float duration, bool deactivateAtEnd)
        {
            float t = 0f;
            float fogFrom = RenderSettings.fogEndDistance;
            float fogTo = deactivateAtEnd ? savedFogEnd : clearFogEnd;
            float fogStartFrom = RenderSettings.fogStartDistance;
            float fogStartTo = deactivateAtEnd ? savedFogStart : clearFogEnd * 0.5f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                float e = Mathf.Clamp01(t);
                // 出る時は少し大きくなってから戻る (ポンッと出る)、消える時はすっと縮む
                float s = to > from ? Mathf.LerpUnclamped(from, to, 1f + 2.7f * Mathf.Pow(e - 1f, 3f) + 1.7f * Mathf.Pow(e - 1f, 2f))
                                    : Mathf.Lerp(from, to, e * e);
                root.localScale = Vector3.one * Mathf.Max(0f, s);
                if (clearFog && fogSaved)
                {
                    RenderSettings.fogEndDistance = Mathf.Lerp(fogFrom, fogTo, e);
                    RenderSettings.fogStartDistance = Mathf.Lerp(fogStartFrom, fogStartTo, e);
                }
                yield return null;
            }
            root.localScale = Vector3.one * to;
            if (deactivateAtEnd)
            {
                root.gameObject.SetActive(false);
                if (fogSaved)
                {
                    RenderSettings.fogEndDistance = savedFogEnd;
                    RenderSettings.fogStartDistance = savedFogStart;
                    fogSaved = false;
                }
            }
            anim = null;
        }
    }
}
