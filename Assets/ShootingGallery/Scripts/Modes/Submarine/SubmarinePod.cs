using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 潜水艦モードで、プレイヤーを包む丸い潜水艦 (黄色い球)。頭を真ん中にして、上下2つの半球と、前のレンズのような丸い窓 (ドア) でできている。
    ///   閉じる: 口を大きく開けた2つの半球がふくらんで現れ、パックンのように「ぱっくん」と閉じてから、開いていたレンズの窓がドアのように閉まる。
    ///   閉じている間: 窓がいつも頭の向いている方に来るように、球ごと視線を追いかけて回る (水の中に体がそのまま出ないように)。
    ///   開く: 窓が開き、2つの半球が口を開けて、小さくなって消える。
    /// 球の当たり判定はない (中から撃った魚雷や照準をさえぎらないように)。
    /// </summary>
    public class SubmarinePod : MonoBehaviour
    {
        [Tooltip("上の半球 / 下の半球 (どちらも球の真ん中を通る左右の軸のまわりに回る)")]
        public Transform upper, lower;
        [Tooltip("レンズの窓 (ドア)。窓の左はしの縦の軸 (ちょうつがい) のまわりに回る")]
        public Transform lensDoor;
        [Tooltip("口を開けている時の、上の半球 (後ろへ持ち上がる) と下の半球 (前が下がる) の角度")]
        public float upperOpenDeg = -95f, lowerOpenDeg = 50f;
        [Tooltip("開いている時のレンズの窓の角度 (外へ開く)")]
        public float doorOpenDeg = -115f;
        public float growTime = 0.5f, chompTime = 0.32f, doorTime = 0.55f;
        [Tooltip("視線を追いかける速さ (大きいほどすぐ追いつく)")]
        public float followSpeed = 7f;
        [Tooltip("窓が上下を向ける角度の限界 (度)")]
        public float maxPitch = 75f;
        [Tooltip("前を照らすライト (窓といっしょに視線の方を向く)")]
        public Light headlight;
        [Tooltip("計器: 深さの針 (深くなるほど回る) / ソナーの回る線")]
        public Transform depthNeedle;
        [Tooltip("後ろから泡を出す所 (上下に動いている時に、ぶくぶく出る)")]
        public Transform[] bubbleVents;
        public Material bubbleMaterial;

        /// <summary>閉じきって、視線を追いかけているか</summary>
        public bool Sealed { get; private set; }
        public bool IsShown => gameObject.activeSelf;

        Transform head;
        float headlightIntensity;
        Vector3 lastPos;
        float nextBubble;

        void Awake()
        {
            if (headlight != null) headlightIntensity = headlight.intensity;
        }

        /// <summary>head (カメラ) のまわりに現れて、ぱっくん閉じる (コルーチン)</summary>
        public IEnumerator Close(Transform headTransform)
        {
            head = headTransform;
            Sealed = false;
            gameObject.SetActive(true);
            transform.position = head.position;
            transform.rotation = YawOnly(head.forward);
            lastPos = transform.position;
            SetJaws(1f);
            SetDoor(1f);
            if (headlight != null) headlight.intensity = 0f;
            // 1. 口を開けたまま、むくっとふくらんで現れる
            for (float t = 0f; t < 1f; t += Time.deltaTime / growTime)
            {
                transform.localScale = Vector3.one * Mathf.Max(0.001f, EaseOutBack(t));
                yield return null;
            }
            transform.localScale = Vector3.one;
            yield return new WaitForSeconds(0.15f);
            // 2. ぱっくん! (だんだん速く閉じて、閉じた所で少し跳ね返る)
            for (float t = 0f; t < 1f; t += Time.deltaTime / chompTime)
            {
                SetJaws(1f - t * t * t);
                yield return null;
            }
            Sfx.Play("hatchclose");
            XRBridge.Haptic(UnityEngine.XR.XRNode.RightHand, 0.7f, 0.12f);
            XRBridge.Haptic(UnityEngine.XR.XRNode.LeftHand, 0.7f, 0.12f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
            {
                SetJaws(Mathf.Sin(t * Mathf.PI) * 0.035f); // カチンと少し跳ね返る
                yield return null;
            }
            SetJaws(0f);
            yield return new WaitForSeconds(0.2f);
            // 3. レンズの窓がドアのように閉まる
            Sfx.Play("click");
            for (float t = 0f; t < 1f; t += Time.deltaTime / doorTime)
            {
                float e = t * t * (3f - 2f * t);
                SetDoor(1f - e);
                if (headlight != null) headlight.intensity = headlightIntensity * e; // 閉まるにつれてライトが点く
                yield return null;
            }
            SetDoor(0f);
            Sfx.Play("thunk");
            if (headlight != null) headlight.intensity = headlightIntensity;
            Sealed = true;
        }

        /// <summary>窓が開き、口を開けて、小さくなって消える (コルーチン)</summary>
        public IEnumerator Open()
        {
            Sealed = false;
            // 窓が上や下を向いていたら、まっすぐ前に戻してから開く
            Quaternion from = transform.rotation, to = head != null ? YawOnly(head.forward) : YawOnly(transform.forward);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                transform.rotation = Quaternion.Slerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }
            Sfx.Play("click");
            for (float t = 0f; t < 1f; t += Time.deltaTime / doorTime)
            {
                float e = t * t * (3f - 2f * t);
                SetDoor(e);
                if (headlight != null) headlight.intensity = headlightIntensity * (1f - e);
                yield return null;
            }
            SetDoor(1f);
            Sfx.Play("hatchclose", 0.6f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                SetJaws(1f - (1f - t) * (1f - t));
                yield return null;
            }
            SetJaws(1f);
            yield return new WaitForSeconds(0.1f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
            {
                transform.localScale = Vector3.one * Mathf.Max(0.001f, 1f - t * t);
                yield return null;
            }
            Hide();
        }

        /// <summary>その場ですぐに消す (ラウンドの終わり・リセット)</summary>
        public void Hide()
        {
            Sealed = false;
            transform.localScale = Vector3.one;
            SetJaws(1f);
            SetDoor(1f);
            gameObject.SetActive(false);
        }

        /// <summary>計器の深さの針 (0 = 水面 〜 1 = いちばん深い所)</summary>
        public void SetDepth(float k)
        {
            if (depthNeedle != null) depthNeedle.localRotation = Quaternion.Euler(0f, 0f, 120f - 240f * Mathf.Clamp01(k));
        }

        // 0 = 閉じている 〜 1 = 口を開けている
        void SetJaws(float open)
        {
            if (upper != null) upper.localRotation = Quaternion.Euler(upperOpenDeg * open, 0f, 0f);
            if (lower != null) lower.localRotation = Quaternion.Euler(lowerOpenDeg * open, 0f, 0f);
        }

        // 0 = 閉じている 〜 1 = 開いている
        void SetDoor(float open)
        {
            if (lensDoor != null) lensDoor.localRotation = Quaternion.Euler(0f, doorOpenDeg * open, 0f);
        }

        void LateUpdate()
        {
            if (head == null) return;
            // 球の真ん中はいつも頭の位置 (潜っていく間も、プレイヤーといっしょに動く)
            transform.position = head.position;
            if (Sealed)
            {
                // 窓が視線の方に来るように、球ごと回る (首をかしげても球は傾かない)
                Vector3 f = head.forward;
                float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg, -maxPitch, maxPitch);
                float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                var target = Quaternion.Euler(pitch, yaw, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            }
            // 上下に動いている間は、後ろからぶくぶく泡が出る
            float dy = transform.position.y - lastPos.y;
            lastPos = transform.position;
            if (Sealed && Mathf.Abs(dy) > 0.002f && Time.time >= nextBubble && bubbleVents != null && bubbleVents.Length > 0)
            {
                nextBubble = Time.time + 0.06f;
                var v = bubbleVents[Random.Range(0, bubbleVents.Length)];
                Bubble.Spawn(v.position + Random.insideUnitSphere * 0.05f, Random.Range(0.04f, 0.09f), bubbleMaterial, Vector3.up * 0.4f + Random.insideUnitSphere * 0.2f, 1.6f);
            }
        }

        static Quaternion YawOnly(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized) : Quaternion.identity;
        }

        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.7f;
            float x = t - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }
}
