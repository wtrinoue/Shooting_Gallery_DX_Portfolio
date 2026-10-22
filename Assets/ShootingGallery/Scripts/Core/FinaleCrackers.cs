using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 2 MIN の終わりのフィナーレ。残り時間が少なくなると、メイン屋台のカウンターの左右にクラッカーがぽんっと出て待ち (Prepare)、
    /// 時間切れと同時に、クラッカーの口からプレイヤーの頭の上へ紙吹雪と紙テープが飛び出す (Burst。破裂音はなし)。
    /// 紙吹雪は、はじめの勢いがすぐ弱まり、ゆらゆら揺れながらゆっくり落ちて、床に着いたら消える (FxPool で使い回す)。
    /// クラッカーはメイン屋台 (stall) の子なので、屋台が動いたり沈んだりするといっしょに動く。
    /// 紙吹雪は、屋台がどこにいても「メイン屋台の最初の位置」のクラッカーの口から出す (時間切れの時は屋台が戻ってくる途中のことがあるので)
    /// </summary>
    public class FinaleCrackers : MonoBehaviour
    {
        [Tooltip("クラッカーを載せているメイン屋台 (普通のメイン屋台)")]
        public Transform stall;
        [Tooltip("クラッカー1本ずつの根元 (台ごと。ふくらんで出てくる)")]
        public Transform[] crackers;
        [Tooltip("クラッカーの口 (+Z が紙吹雪の飛び出す向き)")]
        public Transform[] mouths;
        [Tooltip("口をふさいでいる金紙 (飛び出す時に消える)")]
        public GameObject[] caps;
        [Tooltip("紙吹雪・紙テープの色")]
        public Material[] confettiMaterials;
        public int confettiPerCracker = 80;
        public int streamersPerCracker = 10;
        [Tooltip("飛び出す向きのばらつき (度)")]
        public float spreadDeg = 26f;
        [Tooltip("ぽんっと出てくる秒数 / しまう秒数")]
        public float appearTime = 0.5f;
        public float hideTime = 0.35f;

        /// <summary>クラッカーが出て待っている (または飛び出した後)</summary>
        public bool Prepared { get; private set; }

        float shown;        // クラッカーの出ぐあい (0 = しまってある 〜 1 = 出ている)
        float appearClock;  // 出始めてからの秒数 (ぽんっとふくらむ動き)
        float kickClock = -1f; // 飛び出してからの秒数 (クラッカーが一瞬縮んで反動で伸びる)
        Quaternion[] restRot;
        readonly List<Piece> pieces = new List<Piece>();

        // 初めて使う時に、クラッカーの元の向きを覚える
        // (再生中にビルダーが組み立てる時は、AddComponent した瞬間 (Awake) にはまだ crackers が入っていないので、Awake ではしない)
        bool EnsureInit()
        {
            if (crackers == null) return false;
            if (restRot != null && restRot.Length == crackers.Length) return true;
            restRot = new Quaternion[crackers.Length];
            for (int i = 0; i < crackers.Length; i++) restRot[i] = crackers[i] != null ? crackers[i].localRotation : Quaternion.identity;
            return true;
        }

        /// <summary>クラッカーをぽんっと出して待つ (もう出ていれば何もしない)</summary>
        public void Prepare()
        {
            if (Prepared) return;
            Prepared = true;
            appearClock = 0f;
            kickClock = -1f;
            foreach (var c in caps) if (c != null) c.SetActive(true);
        }

        /// <summary>
        /// 紙吹雪を飛び出させる。homePos / homeRot = メイン屋台の最初の位置 (stall の親のローカル)。
        /// クラッカーがまだ出ていなければ、その場ですぐ出してから飛び出させる
        /// </summary>
        public void Burst(Vector3 homePos, Quaternion homeRot)
        {
            if (!Prepared)
            {
                Prepare();
                appearClock = appearTime;
                SetShown(1f);
            }
            kickClock = 0f;
            foreach (var c in caps) if (c != null) c.SetActive(false);
            if (confettiMaterials == null || confettiMaterials.Length == 0) return;
            Transform space = stall.parent;
            for (int i = 0; i < mouths.Length; i++)
            {
                // 屋台から見た口の位置と向きを、最初の位置の屋台に置き直す
                Vector3 localPos = stall.InverseTransformPoint(mouths[i].position);
                Vector3 localDir = stall.InverseTransformDirection(mouths[i].forward);
                Vector3 from = transform.InverseTransformPoint(space.TransformPoint(homePos + homeRot * localPos));
                Vector3 axis = transform.InverseTransformDirection(space.TransformDirection(homeRot * localDir)).normalized;
                for (int n = 0; n < confettiPerCracker; n++) Launch(from, axis, false);
                for (int n = 0; n < streamersPerCracker; n++) Launch(from, axis, true);
            }
        }

        /// <summary>クラッカーをしまう (タイトルに戻る時。落ちている紙吹雪はそのまま落ちきる)</summary>
        public void Hide()
        {
            Prepared = false;
            kickClock = -1f;
        }

        /// <summary>クラッカーも紙吹雪も、その場ですぐ片付ける (ラウンドの始まり・リセット)</summary>
        public void ClearAll()
        {
            Hide();
            SetShown(0f);
            foreach (var p in pieces) if (p.t != null) FxPool.Release(p.key, p.t.gameObject);
            pieces.Clear();
        }

        void Launch(Vector3 from, Vector3 axis, bool streamer)
        {
            var mat = confettiMaterials[Random.Range(0, confettiMaterials.Length)];
            string key = streamer ? "FinaleStreamer" : "FinaleConfetti";
            // 薄い板 (裏からも見えるように Quad ではなく Cube)
            var go = FxPool.Get(key, () => FxPool.Primitive(PrimitiveType.Cube, mat), transform);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var t = go.transform;
            t.localPosition = from + Random.insideUnitSphere * 0.03f;
            t.localRotation = Random.rotation;
            t.localScale = streamer ? new Vector3(0.016f, Random.Range(0.22f, 0.38f), 0.003f) : new Vector3(0.05f, 0.034f, 0.003f);
            // 口の向きを中心に、spreadDeg の円すいの中へ
            Vector3 side = Vector3.Cross(axis, Random.onUnitSphere).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.Cross(axis, Vector3.up).normalized;
            Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, spreadDeg), side) * axis;
            float speed = streamer ? Random.Range(5f, 7.5f) : Random.Range(3.5f, 8f);
            pieces.Add(new Piece
            {
                t = t, key = key, velocity = dir * speed, spinAxis = Random.onUnitSphere,
                spin = streamer ? Random.Range(90f, 240f) : Random.Range(250f, 650f),
                sway = streamer ? Random.Range(0.15f, 0.35f) : Random.Range(0.3f, 0.7f),
                fall = streamer ? 0.9f : 0.55f, phase = Random.value * 10f
            });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            UpdateCrackers(dt);
            // 紙吹雪: はじめの勢いはすぐ弱まり、あとはゆらゆら揺れながらゆっくり落ちる。床に着いたら消える
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                if (p.t == null) { pieces.RemoveAt(i); continue; }
                p.velocity = Vector3.Lerp(p.velocity, new Vector3(0f, -p.fall, 0f), dt * 2.2f);
                float w = (Time.time + p.phase) * 3f;
                p.t.localPosition += (p.velocity + new Vector3(Mathf.Sin(w), 0f, Mathf.Cos(w * 0.8f)) * p.sway) * dt;
                p.t.Rotate(p.spinAxis, p.spin * dt, Space.Self);
                if (p.t.localPosition.y < 0.05f)
                {
                    FxPool.Release(p.key, p.t.gameObject);
                    pieces.RemoveAt(i);
                    continue;
                }
                pieces[i] = p;
            }
        }

        // クラッカーの出し入れ (ぽんっとふくらむ)・待っている間のゆらゆら・飛び出した時の反動
        void UpdateCrackers(float dt)
        {
            if (!EnsureInit()) return;
            if (Prepared)
            {
                appearClock += dt;
                SetShown(EaseOutBack(appearClock / Mathf.Max(0.05f, appearTime)));
            }
            else if (shown > 0f) SetShown(Mathf.Max(0f, shown - dt / Mathf.Max(0.05f, hideTime)));
            if (shown <= 0f) return;

            if (kickClock >= 0f) kickClock += dt;
            for (int i = 0; i < crackers.Length; i++)
            {
                if (crackers[i] == null) continue;
                float sway = kickClock < 0f ? Mathf.Sin(Time.time * 2.4f + i * 1.7f) * 2.5f : 0f; // 待っている間はゆらゆら
                float kick = kickClock >= 0f && kickClock < 0.3f ? -12f * Mathf.Sin(kickClock / 0.3f * Mathf.PI) : 0f; // 飛び出した反動で後ろへ
                crackers[i].localRotation = restRot[i] * Quaternion.Euler(kick, 0f, sway);
            }
        }

        void SetShown(float k)
        {
            shown = k;
            if (crackers == null) return;
            foreach (var c in crackers)
            {
                if (c == null) continue;
                bool on = k > 0.001f;
                if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
                c.localScale = Vector3.one * Mathf.Max(0.001f, k);
            }
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.7f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * t * t * t + c * t * t;
        }

        struct Piece
        {
            public Transform t;
            public string key;
            public Vector3 velocity, spinAxis;
            public float spin, sway, fall, phase;
        }
    }
}
