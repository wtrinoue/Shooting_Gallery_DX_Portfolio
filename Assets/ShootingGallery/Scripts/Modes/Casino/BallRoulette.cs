using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// カジノの、斜めに傾いたボールのルーレット。撃つと、盤の上の金の管からボールが転がり込んできて (補給)、
    /// 盤の縁を回る (盤とは逆向き。だんだん遅くなる) → 内側へ落ちて、ポケットのどれかに入る → そのポケットの点数が入る (Result)。
    /// 盤 (spinner) はいつもゆっくり回っている。ボールが入ったポケットは盤といっしょに回り、少しすると消えて、また撃てるようになる。
    /// 座標はすべて傾いた盤 (この物の親 = tilt) のローカル: 盤の面は XZ 平面 (表 = +Y)、角度は +Z から +X へ回る向き
    /// </summary>
    public class BallRoulette : MonoBehaviour
    {
        [Tooltip("当たり判定 (kind = BallRoulette, stayUpOnHit)")]
        public Target target;
        [Tooltip("傾いた盤の基準 (ボールの位置はこのローカル)")]
        public Transform tilt;
        [Tooltip("回る盤 (ポケット)。tilt のローカル Y まわりに回る")]
        public Transform spinner;
        public Transform ball;
        [Tooltip("ボールが転がり込んでくる管の道すじ (tilt のローカル。最後の点が盤の縁)")]
        public Vector3[] chutePath;
        [Tooltip("盤の縁 (ボールが回る所) / ポケット (ボールが止まる所) の半径と高さ")]
        public float rimRadius = 0.7f;
        public float pocketRadius = 0.45f;
        public float rimY = 0.06f;
        public float pocketY = 0.04f;
        [Tooltip("ポケットごとの点数 (spinner のローカルで、+Z から +X へ回る順)")]
        public int[] pocketPoints = { 1000, 100, 300, 200, 500, 100, 200, 300, 100, 500, 200, 300 };
        [Tooltip("盤が回る速さ (度/秒)")]
        public float wheelSpeed = 45f;

        /// <summary>ボールがポケットに入った (このルーレット, 点数, ポケットの番号)</summary>
        public event System.Action<BallRoulette, int, int> Result;
        /// <summary>撃てる (ボールが出ていない)</summary>
        public bool IsReady => state == State.Idle && active;

        enum State { Idle, Chute, Orbit, Drop, Settled }
        State state = State.Idle;
        bool active;
        float t, theta, omega, wheelAngle, pocketOffset;
        const float ChuteTime = 0.4f, OrbitTime = 1.6f, DropTime = 0.5f, SettleTime = 1.5f; // (撃ってから 0.4 + 1.6 + 0.5 = 2.5 秒でポケットが決まる)

        void Awake()
        {
            if (target != null) target.SetAvailable(false);
            if (ball != null) ball.gameObject.SetActive(false);
        }

        public void Activate()
        {
            active = true;
            state = State.Idle;
            if (ball != null) ball.gameObject.SetActive(false);
            target.SetAvailable(true);
            target.Raise(9999f);
        }

        public void Deactivate()
        {
            active = false;
            state = State.Idle;
            if (ball != null) ball.gameObject.SetActive(false);
            target.SetAvailable(false);
        }

        /// <summary>撃たれた: ボールを補給して転がす。ボールが出ている間は false</summary>
        public bool Launch()
        {
            if (!IsReady || chutePath == null || chutePath.Length < 2) return false;
            state = State.Chute;
            t = 0f;
            ball.gameObject.SetActive(true);
            ball.localPosition = chutePath[0];
            Sfx.Play("lever");
            return true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            wheelAngle = Mathf.Repeat(wheelAngle + wheelSpeed * dt, 360f);
            if (spinner != null) spinner.localRotation = Quaternion.Euler(0f, wheelAngle, 0f);
            if (state == State.Idle) return;
            t += dt;
            switch (state)
            {
                case State.Chute:
                {
                    // 金の管の中を転がって、盤の縁へ
                    float k = Mathf.Clamp01(t / ChuteTime);
                    ball.localPosition = AlongPath(k * k);
                    if (k >= 1f)
                    {
                        var p = chutePath[chutePath.Length - 1];
                        theta = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
                        omega = -Random.Range(620f, 760f); // 盤とは逆向きに勢いよく
                        state = State.Orbit;
                        t = 0f;
                        Sfx.Play("reelstop");
                    }
                    break;
                }
                case State.Orbit:
                {
                    // 縁を回りながら、だんだん遅くなる
                    float u = Mathf.Clamp01(t / OrbitTime);
                    float w = omega * Mathf.Pow(1f - u, 1.4f);
                    if (Mathf.Abs(w) < 120f) w = Mathf.Sign(omega) * 120f;
                    theta += w * dt;
                    Place(rimRadius, rimY);
                    if (u >= 1f) { state = State.Drop; t = 0f; }
                    break;
                }
                case State.Drop:
                {
                    // 内側へ落ちながら、ポケットの間の仕切りに当たってコトコト跳ねる
                    float u = Mathf.Clamp01(t / DropTime);
                    theta += Mathf.Sign(omega) * 90f * (1f - u) * dt;
                    float r = Mathf.Lerp(rimRadius, pocketRadius, u * u * (3f - 2f * u));
                    float hop = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)) * 0.05f * (1f - u);
                    Place(r, Mathf.Lerp(rimY, pocketY, u) + hop);
                    if (u >= 1f) Settle();
                    break;
                }
                case State.Settled:
                    // 入ったポケットごと、盤といっしょに回る
                    theta = wheelAngle + pocketOffset;
                    Place(pocketRadius, pocketY);
                    if (t >= SettleTime)
                    {
                        ball.gameObject.SetActive(false);
                        state = State.Idle;
                    }
                    break;
            }
        }

        // ボールがいちばん近いポケットに入る: 点数を知らせる
        void Settle()
        {
            int n = Mathf.Max(1, pocketPoints.Length);
            float step = 360f / n;
            float rel = Mathf.Repeat(theta - wheelAngle, 360f);
            int index = Mathf.Clamp(Mathf.FloorToInt(rel / step), 0, n - 1);
            pocketOffset = (index + 0.5f) * step;
            state = State.Settled;
            t = 0f;
            Sfx.Play("click");
            Result?.Invoke(this, pocketPoints[index], index);
        }

        void Place(float r, float y)
        {
            float a = theta * Mathf.Deg2Rad;
            ball.localPosition = new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r);
        }

        // 管の道すじの k (0〜1) の所 (長さに比例)
        Vector3 AlongPath(float k)
        {
            float total = 0f;
            for (int i = 1; i < chutePath.Length; i++) total += (chutePath[i] - chutePath[i - 1]).magnitude;
            float d = k * total;
            for (int i = 1; i < chutePath.Length; i++)
            {
                float seg = (chutePath[i] - chutePath[i - 1]).magnitude;
                if (d <= seg || i == chutePath.Length - 1) return Vector3.Lerp(chutePath[i - 1], chutePath[i], seg > 0f ? Mathf.Clamp01(d / seg) : 1f);
                d -= seg;
            }
            return chutePath[chutePath.Length - 1];
        }
    }
}
