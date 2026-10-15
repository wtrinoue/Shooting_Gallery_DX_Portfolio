using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 発掘モードのトロッコ。線路 (MineTrack) の上を走り、荷台に鉱石か宝石が山盛りに載っている。荷台の鉱石・宝石そのものが的で、
    /// 点数が高いほどきれい (銅 → 銀 → 金の鉱石 → 赤い宝石 → ダイヤモンド)。当てると割れて、破片が飛び散る。
    /// ループの線路のトロッコは、モードの間ずっと回り続け、割れた荷は少しすると別の荷に変わって積み直される。
    /// トンネルの線路のトロッコは、穴から出てきて別の穴へ入ると消える (荷は1回だけ)。
    /// </summary>
    public class MineCart : MonoBehaviour
    {
        public MineTrack track;
        [Tooltip("荷台の荷の当たり判定 (kind = MineObject, manualControl, shrinkOnHit。visual = 荷の見た目)")]
        public Target hitTarget;
        [Tooltip("荷の種類ごとの見た目 (どれか1つだけ出す)。loadPoints / loadShards と同じ順")]
        public GameObject[] loads;
        public int[] loadPoints = { 100, 200, 300, 500, 1000 };
        [Tooltip("割れた時に飛び散る破片の色")]
        public Material[] loadShards;
        [Tooltip("荷の種類ごとの出やすさ")]
        public float[] loadWeights = { 4f, 3f, 2f, 1f, 0.4f };
        [Tooltip("荷の上に浮かぶ点数")]
        public TextMesh[] pointLabels;
        [Tooltip("走る速さ (m/秒)")]
        public float speed = 3f;
        [Tooltip("回る車輪 (ローカル X まわり)")]
        public Transform[] wheels;
        [Tooltip("ゆれる車体")]
        public Transform body;
        [Tooltip("ループのトロッコの荷が、割れてから積み直されるまでの秒数")]
        public float respawnDelay = 3.5f;

        public bool IsRunning { get; private set; }

        float dist, dir = 1f, downTimer;
        bool armed, broken;
        int loadIndex;

        void Awake() => hitTarget.SetAvailable(false);

        /// <summary>走り始める。startDist = 線路のはじめからの位置, reverse = 逆向きに走る</summary>
        public void Run(float startDist, bool reverse)
        {
            gameObject.SetActive(true);
            dist = startDist;
            dir = reverse ? -1f : 1f;
            IsRunning = true;
            Place();
            Load();
        }

        /// <summary>止めて隠す (モードの終わり)</summary>
        public void Stop()
        {
            IsRunning = false;
            hitTarget.SetAvailable(false);
            gameObject.SetActive(false);
        }

        // 荷を積む: 種類を出やすさに応じて選び、その見た目だけを出して点数を書く
        void Load()
        {
            float total = 0f;
            for (int i = 0; i < loads.Length; i++) total += i < loadWeights.Length ? loadWeights[i] : 1f;
            float r = Random.value * total;
            loadIndex = loads.Length - 1;
            for (int i = 0; i < loads.Length; i++)
            {
                r -= i < loadWeights.Length ? loadWeights[i] : 1f;
                if (r <= 0f) { loadIndex = i; break; }
            }
            for (int i = 0; i < loads.Length; i++) if (loads[i] != null) loads[i].SetActive(i == loadIndex);
            hitTarget.points = loadIndex < loadPoints.Length ? loadPoints[loadIndex] : 100;
            foreach (var label in pointLabels) if (label != null) label.text = hitTarget.points.ToString();
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f);
            armed = broken = false;
            downTimer = 0f;
        }

        void Update()
        {
            if (!IsRunning || track == null) return;
            float dt = Time.deltaTime;
            dist += speed * dir * dt;
            Place();
            foreach (var w in wheels) if (w != null) w.Rotate(speed / 0.18f * Mathf.Rad2Deg * dt, 0f, 0f, Space.Self);
            if (body != null) body.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 9f + dist) * 1.5f, 0f, Mathf.Sin(Time.time * 7f) * 2f); // ガタゴト

            if (!track.loop && (dist < -0.5f || dist > track.Length + 0.5f)) { Stop(); return; } // 別の穴へ入った

            if (hitTarget.IsUp) armed = true;
            if (armed && !hitTarget.IsUp)
            {
                if (!broken)
                {
                    // 割れた: 荷の色の破片が飛び散る (点数はもう入っている)
                    broken = true;
                    var p = hitTarget.transform.position + Vector3.up * 0.2f;
                    var shard = loadIndex < loadShards.Length ? loadShards[loadIndex] : null;
                    if (shard != null) SpaceDebris.Spawn(p, 0.7f, shard, 14);
                    Sfx.Play(hitTarget.points >= 500 ? "coin" : "thunk");
                }
                downTimer += dt;
                if (track.loop && downTimer >= respawnDelay) Load(); // 別の荷を積み直す
            }
        }

        void Place()
        {
            track.Evaluate(dist, out var pos, out var fwd);
            transform.localPosition = pos;
            var f = fwd * dir;
            f.y *= 0.6f;
            if (f.sqrMagnitude > 0.0001f) transform.localRotation = Quaternion.LookRotation(f);
        }
    }
}
