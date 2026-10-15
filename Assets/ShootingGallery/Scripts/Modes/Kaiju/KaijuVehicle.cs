using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 怪獣モードの動く的: 道路を走る車・街の上を横切る飛行機・怪獣のまわりを回るヘリコプター。上に点数の札 (いつもプレイヤーの方を向く)。
    /// 炎の玉が当たると、車は爆発してひっくり返り、飛行機とヘリコプターは煙を吐きながらきりもみで落ちて、地面で爆発する。
    /// しばらくすると別の道 / 別の向きから、また出てくる。位置は親 (KaijuCity。プレイヤーの足元が原点) のローカル座標で決める。
    /// </summary>
    public class KaijuVehicle : MonoBehaviour
    {
        public enum Kind { Car, Plane, Helicopter }

        public Kind kind;
        [Tooltip("傾いたり、落ちる時に回ったりする見た目 (根元が乗り物の真ん中)")]
        public Transform body;
        [Tooltip("回るプロペラ・回転翼と、それぞれの回る軸 (ローカル)")]
        public Transform[] rotors;
        public Vector3[] rotorAxes;
        public float rotorSpeed = 1500f;
        [Tooltip("乗り物の当たり判定 (kind = KaijuVehicle, manualControl。visual は空のオブジェクト)")]
        public Target target;
        [Tooltip("点数を書いた札")]
        public TextMesh[] pointLabels;
        public int[] pointChoices = { 100, 200 };
        [Tooltip("進む速さ (m/秒)。ヘリコプターは回る速さ")]
        public float speed = 3f;
        [Tooltip("飛ぶ高さの範囲 (m)")]
        public Vector2 altitude = new Vector2(2.5f, 4f);
        [Tooltip("プレイヤーからの距離の範囲 (m)。飛行機は通り道の近さ、ヘリコプターは回る半径")]
        public Vector2 distance = new Vector2(10f, 20f);
        [Tooltip("爆発の大きさ (m)")]
        public float size = 0.6f;
        public Material explosionMaterial;
        public Material smokeMaterial;
        [Tooltip("やられてから、また出てくるまでの秒数")]
        public Vector2 respawnDelay = new Vector2(2f, 4f);

        [Header("車の道")]
        [Tooltip("地面 (道路) の高さ")]
        public float groundY = 0.04f;
        [Tooltip("道路の間隔 (m) / 走る道の番号の範囲 (真ん中の公園を通る 0 は使わない) / 道の端までの距離 (m) / 道の真ん中からの車線のずれ (m)")]
        public float roadStep = 6f;
        public int roadMin = 1, roadMax = 5;
        public float roadExtent = 40f;
        public float laneOffset = 0.28f;

        const float PlaneHalfPath = 55f; // 飛行機が出てくる所 / 去っていく所 (通り道の真ん中から)
        const float FallGravity = 6f;

        enum State { Off, Waiting, Moving, Falling, Wreck }
        State state = State.Off;
        float t, timer, phase;
        bool armed;
        // 走る / 飛ぶ道すじ
        Vector3 from, dir;
        float pathLength;
        float orbitR, orbitA, orbitDir, alt, stay;
        // 落ちる時
        Vector3 vel, spinAxis;
        float spinSpeed;
        int puffs;

        void Awake()
        {
            target.SetAvailable(false);
            gameObject.SetActive(false);
        }

        /// <summary>怪獣モードの街ができた: delay 秒後に出てくる</summary>
        public void Activate(float delay)
        {
            gameObject.SetActive(true);
            SetVisible(false);
            state = State.Waiting;
            timer = delay;
        }

        /// <summary>怪獣モードの終わり: すぐに消す</summary>
        public void Deactivate()
        {
            state = State.Off;
            target.SetAvailable(false);
            gameObject.SetActive(false);
        }

        void SetVisible(bool visible)
        {
            body.gameObject.SetActive(visible);
            foreach (var l in pointLabels) if (l != null) l.gameObject.SetActive(visible);
        }

        // 新しい道すじを決めて出てくる
        void Launch()
        {
            state = State.Moving;
            t = 0f;
            armed = false;
            phase = Random.value * 10f;
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            switch (kind)
            {
                case Kind.Car:
                {
                    // 道路の1本を端から端まで走る (左側通行)
                    int k = Random.Range(roadMin, roadMax + 1) * (Random.value < 0.5f ? -1 : 1);
                    bool alongX = Random.value < 0.5f;
                    float sign = Random.value < 0.5f ? -1f : 1f;
                    dir = alongX ? new Vector3(sign, 0f, 0f) : new Vector3(0f, 0f, sign);
                    var left = Vector3.Cross(dir, Vector3.up); // 進む向きの左
                    var road = alongX ? new Vector3(0f, groundY, k * roadStep) : new Vector3(k * roadStep, groundY, 0f);
                    from = road - dir * roadExtent + left * laneOffset;
                    pathLength = roadExtent * 2f;
                    break;
                }
                case Kind.Plane:
                {
                    // 遠くから現れて、街の上をまっすぐ横切っていく
                    float heading = Random.Range(0f, 360f);
                    dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                    var side = Vector3.Cross(Vector3.up, dir);
                    alt = Random.Range(altitude.x, altitude.y);
                    var mid = side * Random.Range(distance.x, distance.y) + Vector3.up * alt;
                    from = mid - dir * PlaneHalfPath;
                    pathLength = PlaneHalfPath * 2f;
                    break;
                }
                default:
                    // 遠くから寄ってきて、しばらく怪獣のまわりを回り、また遠くへ去る
                    orbitR = Random.Range(distance.x, distance.y);
                    orbitA = Random.Range(0f, Mathf.PI * 2f);
                    orbitDir = Random.value < 0.5f ? -1f : 1f;
                    alt = Random.Range(altitude.x, altitude.y);
                    stay = Random.Range(14f, 22f);
                    break;
            }

            target.points = pointChoices.Length > 0 ? pointChoices[Random.Range(0, pointChoices.Length)] : 100;
            foreach (var l in pointLabels) if (l != null) l.text = PointText.Of(target.points);
            SetVisible(true);
            Place();
            target.SetAvailable(true);
            target.Raise(9999f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (rotors != null && state != State.Off && state != State.Waiting && state != State.Wreck)
                for (int i = 0; i < rotors.Length; i++)
                    if (rotors[i] != null) rotors[i].Rotate(rotorAxes[i], rotorSpeed * dt, Space.Self);

            switch (state)
            {
                case State.Waiting:
                    timer -= dt;
                    if (timer <= 0f) Launch();
                    break;
                case State.Moving:
                    t += dt;
                    if (target.IsUp) armed = true;
                    else if (armed) { StartFall(); break; } // 炎の玉が当たった
                    if (!Place()) Rest(0.5f); // 道の端まで走った / 飛び去った: すぐに次の道すじで出てくる
                    break;
                case State.Falling:
                    Fall(dt);
                    break;
                case State.Wreck:
                    timer -= dt;
                    if (timer <= 0f) Rest(Random.Range(respawnDelay.x, respawnDelay.y));
                    break;
            }
        }

        // 今の位置に置く。道すじを走りきったら false
        bool Place()
        {
            switch (kind)
            {
                case Kind.Car:
                {
                    float s = speed * t;
                    transform.localPosition = from + dir * s;
                    transform.localRotation = Quaternion.LookRotation(dir);
                    // でこぼこ道を小刻みに揺れながら走る
                    body.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin((t + phase) * 14f)) * 0.012f);
                    return s < pathLength;
                }
                case Kind.Plane:
                {
                    float s = speed * t;
                    transform.localPosition = from + dir * s + Vector3.up * (Mathf.Sin((t + phase) * 0.7f) * 0.25f);
                    transform.localRotation = Quaternion.LookRotation(dir);
                    body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin((t + phase) * 0.5f) * 8f); // ゆったり翼をかたむける
                    return s < pathLength;
                }
                default:
                {
                    // 寄ってくる (4秒) → 回る → 外へ去る (5秒)
                    float come = Mathf.Clamp01(t / 4f), go = Mathf.Clamp01((t - 4f - stay) / 5f);
                    float r = Mathf.Lerp(orbitR + 35f, orbitR, come * come * (3f - 2f * come)) + go * go * 45f;
                    float a = orbitA + orbitDir * speed * t / Mathf.Max(1f, orbitR);
                    var pos = new Vector3(Mathf.Sin(a) * r, alt + Mathf.Sin((t + phase) * 1.3f) * 0.2f + go * 3f, Mathf.Cos(a) * r);
                    var tangent = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)) * orbitDir;
                    var move = come < 1f ? Vector3.Lerp(-pos.normalized, tangent, come) : go > 0f ? Vector3.Lerp(tangent, pos.normalized, go) : tangent;
                    move.y = 0f;
                    transform.localPosition = pos;
                    if (move.sqrMagnitude > 0.001f) transform.localRotation = Quaternion.LookRotation(move.normalized);
                    body.localRotation = Quaternion.Euler(10f + Mathf.Sin((t + phase) * 0.9f) * 3f, 0f, -orbitDir * 8f * (1f - go)); // 少し前のめりで、内側へかたむく
                    return go < 1f;
                }
            }
        }

        // 炎の玉が当たった: 爆発して、車はひっくり返り、空を飛ぶものは煙を吐いてきりもみで落ちる
        void StartFall()
        {
            state = State.Falling;
            timer = 0f;
            puffs = 0;
            Explosion.Spawn(body.position, size * 1.4f, explosionMaterial);
            if (kind == Kind.Car)
            {
                vel = Vector3.up * 2.6f + transform.forward * 0.5f;
                spinAxis = transform.forward;
                spinSpeed = 540f;
            }
            else
            {
                vel = transform.forward * speed * (kind == Kind.Plane ? 0.6f : 0.15f) + Vector3.up * 0.5f;
                spinAxis = kind == Kind.Plane ? transform.forward : Vector3.up;
                spinSpeed = kind == Kind.Plane ? 300f : 600f;
            }
        }

        void Fall(float dt)
        {
            timer += dt;
            vel += Vector3.down * FallGravity * dt;
            transform.localPosition += vel * dt;
            body.Rotate(spinAxis, spinSpeed * dt, Space.World);
            if (kind == Kind.Plane) // 機首が下がっていく
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(vel.normalized), dt * 2f);
            // 煙を吐きながら落ちる
            if (timer >= puffs * 0.12f)
            {
                puffs++;
                Explosion.Spawn(body.position + Random.insideUnitSphere * size * 0.3f, size * Random.Range(0.6f, 0.9f), smokeMaterial);
            }
            if (transform.localPosition.y <= groundY && vel.y < 0f)
            {
                // 地面に落ちた: もう一度爆発して消える
                var p = transform.localPosition;
                p.y = groundY;
                transform.localPosition = p;
                Explosion.Spawn(body.position, size * 1.8f, explosionMaterial);
                Explosion.Spawn(body.position + Vector3.up * size * 0.5f, size * 1.4f, smokeMaterial);
                SetVisible(false);
                target.SetAvailable(false);
                state = State.Wreck;
                timer = 0.4f;
            }
        }

        // 隠れて、delay 秒後に別の道すじで出てくる
        void Rest(float delay)
        {
            target.SetAvailable(false);
            SetVisible(false);
            state = State.Waiting;
            timer = delay;
        }
    }
}
