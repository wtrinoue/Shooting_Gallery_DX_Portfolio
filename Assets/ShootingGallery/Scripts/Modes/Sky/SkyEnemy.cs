using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 空中ステージの敵 (気球 / プロペラ機)。プレイヤーのまわりの空を飛び、マシンガンで hitPoints 発当てると撃墜。
    /// 撃墜すると大きく書かれた点数が入り、煙を吐きながら下へ落ちていく (気球はしぼんで落ちる)。
    /// 当てるたびに火花が散り、半分以上当てると煙を吐き始める (あと少しだと分かるように)。
    /// 飛び方: 横切る (Cross) / 遠くから近づいて回る (Orbit) / 正面から近づいてからゆっくり回る (Approach) / 下から昇ってくる (Rise)。
    /// 位置は親 (SkyBattle。プレイヤーの足元が原点) のローカル座標で決める。反撃はしない。
    /// </summary>
    public class SkyEnemy : MonoBehaviour
    {
        public enum Route { Cross, Orbit, Approach, Rise }

        public string displayName = "BALLOON";
        [Tooltip("揺れたり傾いたりする部分 (機体・気球)")]
        public Transform body;
        [Tooltip("全体の当たり判定 (この敵の Target。kind = Aerial, stayUpOnHit。visual は空のオブジェクトにしておく)")]
        public Target hitTarget;
        [Tooltip("大きく書いた点数")]
        public TextMesh[] pointLabels;
        [Tooltip("出てくるたびに、この中からランダムに点数を選ぶ")]
        public int[] pointChoices = { 100 };
        [Tooltip("出てくるたびに、この中からランダムに飛び方を選ぶ")]
        public Route[] routeChoices = { Route.Cross };
        [Tooltip("撃墜までに当てる回数")]
        public int hitPoints = 3;
        [Tooltip("気球なら ON (撃墜するとしぼんで落ちる。機体を傾けずに飛ぶ)")]
        public bool isBalloon;
        [Tooltip("撃墜でしぼむ部分 (気球の球皮)")]
        public Transform envelope;
        [Tooltip("回るプロペラ (飛んでいる間だけ回る)")]
        public Transform[] propellers;
        [Tooltip("飛ぶ速さ (m/秒)")]
        public float speed = 4f;
        [Tooltip("プレイヤーからの距離の範囲 (m)。横切る時は一番近づく距離、回る時は回る半径")]
        public Vector2 distance = new Vector2(9f, 12f);
        [Tooltip("飛ぶ高さの範囲 (m。プレイヤーの足元が 0)")]
        public Vector2 altitude = new Vector2(0f, 4f);
        [Tooltip("回る時に機体を傾ける角度 (度)")]
        public float bankDeg = 25f;
        public float bobHeight = 0.15f;
        public Material explosionMaterial;
        public Material smokeMaterial;

        const float FarRadius = 27f;   // ここより遠くは霧で見えない (出てくる所 / 去っていく所)
        const float FallTime = 3f;
        const float RiseStartY = -8f;  // 下から昇ってくる時に出てくる高さ (翼の下の雲の中)

        public bool IsFlying { get; private set; }
        /// <summary>撃墜されて落ちている途中 (撃墜は1機につき1回)</summary>
        public bool IsFalling { get; private set; }
        public bool IsBusy => IsFlying || IsFalling || retreating;
        /// <summary>今回の点数 (大きく書いてある数字)</summary>
        public int Points => points;
        /// <summary>あと何発で撃墜か</summary>
        public int HitsLeft => hitsLeft;

        System.Action<SkyEnemy> onDown;
        Route route;
        float t;
        float angle;      // 飛び始めの方向 (ラジアン)
        float dist;
        float height;
        float dir;        // +1 / -1 (飛ぶ向き)
        float fallT;
        bool retreating;
        int explosionsLeft;
        float phase;
        int points;
        int hitsLeft;
        float nextSmokeTime;
        float flash;      // 当たった時のぐらつき (1 → 0)
        Vector3 velocity;
        float fallPitch, fallRoll;
        Vector3 envelopeScale;

        // 敵は非表示で作っておき、Launch で初めて表示される (その時に Awake → 続けて Launch が当たり判定を出す)
        void Awake()
        {
            hitTarget.SetAvailable(false);
            if (envelope != null) envelopeScale = envelope.localScale;
        }

        /// <summary>飛び始める。angleDeg = 出てくる方向 (プレイヤーから見た水平の角度)</summary>
        public void Launch(float angleDeg, System.Action<SkyEnemy> downCallback)
        {
            gameObject.SetActive(true);
            route = routeChoices.Length > 0 ? routeChoices[Random.Range(0, routeChoices.Length)] : Route.Cross;
            onDown = downCallback;
            angle = angleDeg * Mathf.Deg2Rad;
            dist = Random.Range(distance.x, distance.y);
            height = Random.Range(altitude.x, altitude.y);
            dir = Random.value < 0.5f ? -1f : 1f;
            t = 0f;
            phase = Random.value * 10f;
            flash = 0f;
            IsFlying = true;
            IsFalling = false;
            retreating = false;
            hitsLeft = Mathf.Max(1, hitPoints);
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            if (envelope != null) envelope.localScale = envelopeScale;
            Place(0f, true);

            // 点数を決めて大きく書く
            points = pointChoices.Length > 0 ? pointChoices[Random.Range(0, pointChoices.Length)] : 100;
            hitTarget.points = points;
            foreach (var label in pointLabels) if (label != null) label.text = points.ToString();
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f);
        }

        /// <summary>
        /// 弾が当たった (GameManager から)。撃墜した1発なら点数を返し、それ以外は 0。
        /// </summary>
        public int TakeHit(Vector3 point)
        {
            if (!IsFlying) return 0;
            hitsLeft--;
            flash = 1f;
            Explosion.Spawn(point, Random.Range(0.12f, 0.2f), explosionMaterial); // 火花
            if (hitsLeft > 0) return 0;
            ShootDown();
            return points;
        }

        /// <summary>撃墜されずに去る (空中ステージの終わり)。外へ遠ざかりながら上へ昇って消える</summary>
        public void Retreat()
        {
            if (!IsFlying) return;
            IsFlying = false;
            retreating = true;
            fallT = 0f;
            Vector3 away = transform.localPosition;
            away.y = 0f;
            velocity = (away.sqrMagnitude > 0.01f ? away.normalized : transform.forward) * Mathf.Max(speed * 2f, 8f) + Vector3.up * 3f;
            hitTarget.SetAvailable(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var p in propellers)
                if (p != null && !IsFalling) p.Rotate(0f, 0f, 1500f * dt, Space.Self);

            if (IsFlying)
            {
                t += dt;
                Place(t, false);
                float w = Time.time + phase;
                flash = Mathf.MoveTowards(flash, 0f, dt * 6f);
                // ふわふわ揺れる / 当たるとぐらっと揺れる
                float roll = isBalloon ? Mathf.Sin(w * 0.8f) * 3f : TurnRoll();
                body.localPosition = Vector3.up * (Mathf.Sin(w * 1.1f) * bobHeight);
                body.localRotation = Quaternion.Euler(Mathf.Sin(w * 0.7f) * 2f + flash * 8f, 0f, roll + Mathf.Sin(w * 37f) * flash * 10f);

                // 半分以上当てると煙を吐く (あと少しの合図)
                if (hitsLeft * 2 <= hitPoints && Time.time >= nextSmokeTime)
                {
                    nextSmokeTime = Time.time + 0.12f;
                    Explosion.Spawn(body.position - transform.forward * 0.6f, Random.Range(0.2f, 0.35f), smokeMaterial);
                }

                if (route == Route.Cross && t > CrossDuration) Vanish();          // 横切り終わった (霧の向こう)
                else if (route == Route.Rise && transform.localPosition.y > 14f) Vanish(); // 昇りきった
                return;
            }

            if (IsFalling || retreating)
            {
                fallT += dt;
                if (IsFalling)
                {
                    // 下へ落ちながら回る。機首を下げてきりもみ (気球はしぼみながらまっすぐ落ちる)
                    velocity += Vector3.down * (9.8f * 0.6f * dt);
                    transform.localPosition += velocity * dt;
                    if (isBalloon)
                    {
                        if (envelope != null) envelope.localScale = Vector3.Lerp(envelope.localScale, envelopeScale * 0.25f, dt * 4f);
                        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(0f, 0f, 30f * dir), dt * 2f);
                    }
                    else
                    {
                        fallPitch = Mathf.MoveTowards(fallPitch, 55f, 40f * dt);
                        fallRoll += 300f * dt * dir;
                        body.localRotation = Quaternion.Euler(fallPitch, 0f, fallRoll);
                    }
                    if (Time.time >= nextSmokeTime)
                    {
                        nextSmokeTime = Time.time + 0.07f;
                        Explosion.Spawn(body.position, Random.Range(0.3f, 0.55f), smokeMaterial);
                    }
                    if (explosionsLeft > 0 && fallT >= (3 - explosionsLeft) * 0.2f)
                    {
                        explosionsLeft--;
                        Explosion.Spawn(body.position + Random.insideUnitSphere * 0.6f, Random.Range(0.7f, 1.2f), explosionMaterial);
                    }
                    if (fallT >= FallTime) Vanish();
                }
                else
                {
                    transform.localPosition += velocity * dt;
                    if (fallT >= 2.5f) Vanish();
                }
            }
        }

        void ShootDown()
        {
            IsFlying = false;
            IsFalling = true;
            fallT = 0f;
            explosionsLeft = 3;
            nextSmokeTime = 0f;
            var e = body.localEulerAngles;
            fallPitch = Mathf.DeltaAngle(0f, e.x);
            fallRoll = Mathf.DeltaAngle(0f, e.z);
            velocity = (PathPoint(t + 0.1f) - PathPoint(t)) / 0.1f * 0.6f;
            if (isBalloon) velocity *= 0.3f;
            hitTarget.SetAvailable(false);
            onDown?.Invoke(this);
        }

        void Vanish()
        {
            IsFlying = IsFalling = retreating = false;
            hitTarget.SetAvailable(false);
            gameObject.SetActive(false);
        }

        // 回っている時は内側へ傾ける
        float TurnRoll()
        {
            if (route == Route.Cross || route == Route.Rise) return 0f;
            float k = route == Route.Orbit ? Smooth(t / 3f) : Smooth(t / 5f);
            return bankDeg * k * dir;
        }

        float CrossHalf => Mathf.Sqrt(Mathf.Max(1f, FarRadius * FarRadius - dist * dist));
        float CrossDuration => 2f * CrossHalf / Mathf.Max(0.1f, speed);

        Vector3 PathPoint(float time)
        {
            float a, r;
            switch (route)
            {
                case Route.Cross:
                {
                    // 一番近づく距離 dist の弦の上を、霧の向こうから霧の向こうまで横切る
                    float along = (-CrossHalf + speed * time) * dir;
                    var p = new Vector3(along, height, dist);
                    return Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f) * p;
                }
                case Route.Orbit:
                    // 霧の向こうから3秒で半径 dist まで近づき、そのまま回る
                    r = Mathf.Lerp(FarRadius, dist, Smooth(time / 3f));
                    a = angle + dir * speed / dist * time;
                    return new Vector3(Mathf.Sin(a) * r, height, Mathf.Cos(a) * r);
                case Route.Rise:
                {
                    // 翼の下の雲の中から、ゆっくり横へ流されながら昇ってくる
                    a = angle + dir * 0.25f / dist * time;
                    float y = RiseStartY + speed * time;
                    return new Vector3(Mathf.Sin(a) * dist, y, Mathf.Cos(a) * dist);
                }
                default:
                {
                    // 正面からまっすぐ近づき (5秒)、近づくにつれて横を向いてゆっくり回り始める
                    float k = Smooth(time / 5f);
                    r = Mathf.Lerp(FarRadius, dist, k);
                    a = angle + dir * speed / dist * time * k;
                    return new Vector3(Mathf.Sin(a) * r, height, Mathf.Cos(a) * r);
                }
            }
        }

        void Place(float time, bool snap)
        {
            Vector3 p = PathPoint(time);
            Vector3 ahead = PathPoint(time + 0.1f) - p;
            transform.localPosition = p;
            ahead.y = 0f; // 機首は水平に向ける (昇る気球も上を向かない)
            if (ahead.sqrMagnitude < 0.000001f) return;
            var rot = Quaternion.LookRotation(ahead.normalized); // 機首を進む向きへ
            transform.localRotation = snap ? rot : Quaternion.Slerp(transform.localRotation, rot, Time.deltaTime * 3f);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
