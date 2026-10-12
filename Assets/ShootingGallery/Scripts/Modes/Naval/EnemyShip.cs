using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 海戦モードの敵の船。プレイヤーのまわりの海を走り、どこかに当てると帆に大きく書かれた点数が入り、爆発して沈む (撃沈)。
    /// 走り方は3種類: 横切る (Cross) / 遠くから近づいて回る (Orbit) / 正面から近づいてからゆっくり回る (Approach)。
    /// 位置は親 (NavalBattle。プレイヤーの足元が原点) のローカル座標で決める。反撃はしない。
    /// </summary>
    public class EnemyShip : MonoBehaviour
    {
        public enum Route { Cross, Orbit, Approach }

        public string displayName = "PIRATE SLOOP";
        [Tooltip("揺れたり傾いたりする部分 (船体と帆)")]
        public Transform body;
        [Tooltip("船全体の当たり判定 (この船の Target。船体と帆のどこに当たっても撃沈。visual は空のオブジェクトにしておく)")]
        public Target hitTarget;
        [Tooltip("帆に大きく書いた点数")]
        public TextMesh[] pointLabels;
        [Tooltip("出てくるたびに、この中からランダムに点数を選ぶ")]
        public int[] pointChoices = { 300 };
        [Tooltip("出てくるたびに、この中からランダムに走り方を選ぶ")]
        public Route[] routeChoices = { Route.Cross };
        [Tooltip("走る速さ (m/秒)")]
        public float speed = 3f;
        [Tooltip("プレイヤーからの距離の範囲 (m)。横切る時は一番近づく距離、回る時は回る半径")]
        public Vector2 distance = new Vector2(9f, 12f);
        [Tooltip("撃沈した時の爆発の見た目")]
        public Material explosionMaterial;
        public float bobHeight = 0.08f;
        public float rollDeg = 4f;

        const float FarRadius = 27f;   // ここより遠くは霧で見えない (出てくる所 / 去っていく所)
        const float SinkTime = 2.6f;

        public bool IsSailing { get; private set; }
        /// <summary>撃沈されて沈んでいる途中 (撃沈は1隻につき1回)</summary>
        public bool IsSinking { get; private set; }
        public bool IsBusy => IsSailing || IsSinking || retreating;
        /// <summary>今回の点数 (帆に書いてある数字)</summary>
        public int Points => hitTarget != null ? hitTarget.points : 0;

        System.Action<EnemyShip> onSunk;
        Route route;
        float t;
        float angle;      // 走り始めの方向 (ラジアン)
        float dist;
        float dir;        // +1 / -1 (走る向き)
        float sinkT;
        bool retreating;
        int explosionsLeft;
        float phase;
        Vector3 sinkVelocity;

        // 船は非表示で作っておき、Launch で初めて表示される (その時に Awake → 続けて Launch が当たり判定を出す)
        void Awake()
        {
            hitTarget.SetAvailable(false);
        }

        /// <summary>走り始める。angleDeg = 出てくる方向 (プレイヤーから見た水平の角度)</summary>
        public void Launch(float angleDeg, System.Action<EnemyShip> sunkCallback)
        {
            gameObject.SetActive(true);
            route = routeChoices.Length > 0 ? routeChoices[Random.Range(0, routeChoices.Length)] : Route.Cross;
            onSunk = sunkCallback;
            angle = angleDeg * Mathf.Deg2Rad;
            dist = Random.Range(distance.x, distance.y);
            dir = Random.value < 0.5f ? -1f : 1f;
            t = 0f;
            phase = Random.value * 10f;
            IsSailing = true;
            IsSinking = false;
            retreating = false;
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            Place(0f, true);

            // 点数を決めて帆に書く
            hitTarget.points = pointChoices.Length > 0 ? pointChoices[Random.Range(0, pointChoices.Length)] : 100;
            foreach (var label in pointLabels) if (label != null) label.text = hitTarget.points.ToString();
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f);
        }

        /// <summary>撃沈されずに去る (海戦モードの終わり)。静かに沈んで消える</summary>
        public void Retreat()
        {
            if (!IsSailing) return;
            IsSailing = false;
            retreating = true;
            sinkT = 0f;
            sinkVelocity = transform.localRotation * Vector3.forward * speed * 0.5f;
            hitTarget.SetAvailable(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (IsSailing)
            {
                t += dt;
                Place(t, false);
                // 波に揺られる
                float w = Time.time + phase;
                body.localPosition = Vector3.up * (Mathf.Sin(w * 1.3f) * bobHeight);
                body.localRotation = Quaternion.Euler(Mathf.Sin(w * 0.9f) * rollDeg * 0.5f, 0f, Mathf.Sin(w * 1.1f) * rollDeg);

                if (t > 0.6f && !hitTarget.IsUp) Sink(); // 当たった (出てきてすぐは当たり判定が起き上がる途中なので数えない)
                else if (route == Route.Cross && t > CrossDuration) Vanish(); // 横切り終わった (霧の向こう)
                return;
            }

            if (IsSinking || retreating)
            {
                sinkT += dt;
                float k = Mathf.Clamp01(sinkT / (retreating ? 1.6f : SinkTime));
                transform.localPosition += sinkVelocity * dt + Vector3.down * (dt * (retreating ? 2.5f : 1.6f) * (0.3f + k));
                sinkVelocity = Vector3.Lerp(sinkVelocity, Vector3.zero, dt * 0.8f);
                if (IsSinking)
                {
                    // 横倒しになりながら、船首を上げて沈む
                    body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(-25f, 0f, 40f * dir), dt * 1.2f);
                    if (explosionsLeft > 0 && sinkT >= (3 - explosionsLeft) * 0.25f)
                    {
                        explosionsLeft--;
                        Explosion.Spawn(transform.position + Vector3.up * Random.Range(0.6f, 1.6f) + transform.right * Random.Range(-1f, 1f),
                                        Random.Range(0.9f, 1.5f), explosionMaterial);
                    }
                }
                if (k >= 1f) Vanish();
            }
        }

        void Sink()
        {
            IsSailing = false;
            IsSinking = true;
            sinkT = 0f;
            explosionsLeft = 3;
            sinkVelocity = transform.localRotation * Vector3.forward * speed * 0.3f;
            onSunk?.Invoke(this);
        }

        void Vanish()
        {
            IsSailing = IsSinking = retreating = false;
            hitTarget.SetAvailable(false);
            gameObject.SetActive(false);
        }

        float CrossHalf => Mathf.Sqrt(Mathf.Max(1f, FarRadius * FarRadius - dist * dist));
        float CrossDuration => 2f * CrossHalf / Mathf.Max(0.1f, speed);

        Vector3 PathPoint(float time)
        {
            switch (route)
            {
                case Route.Cross:
                {
                    // 一番近づく距離 dist の弦の上を、霧の向こうから霧の向こうまで横切る
                    float along = (-CrossHalf + speed * time) * dir;
                    var p = new Vector3(along, 0f, dist);
                    return Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f) * p;
                }
                case Route.Orbit:
                {
                    // 霧の向こうから3秒で半径 dist まで近づき、そのまま回る
                    float r = Mathf.Lerp(FarRadius, dist, Smooth(time / 3f));
                    float a = angle + dir * speed / dist * time;
                    return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                }
                default:
                {
                    // 正面からまっすぐ近づき (5秒)、近づくにつれて横を向いてゆっくり回り始める
                    float k = Smooth(time / 5f);
                    float r = Mathf.Lerp(FarRadius, dist, k);
                    float a = angle + dir * speed / dist * time * k;
                    return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                }
            }
        }

        void Place(float time, bool snap)
        {
            Vector3 p = PathPoint(time);
            Vector3 ahead = PathPoint(time + 0.1f) - p;
            transform.localPosition = p;
            if (ahead.sqrMagnitude < 0.000001f) return;
            var rot = Quaternion.LookRotation(ahead.normalized); // 船首を進む向きへ
            transform.localRotation = snap ? rot : Quaternion.Slerp(transform.localRotation, rot, Time.deltaTime * 3f);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
