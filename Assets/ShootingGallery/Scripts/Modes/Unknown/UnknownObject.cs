using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// Unknown ステージの的: ワープゲートの奥の世界から、ふしぎな空間に紛れ込んでくる物 (牛・トラクター・UFO・タクシー・雪だるま…)。
    /// ゲートの奥から出てきて入り口をくぐり、プレイヤーのまわりまで来て、しばらくふわふわ漂い、やがて小さくなって消える。
    /// 紫の電撃で撃つと、別の世界へ転送される: 物の後ろ (プレイヤーから見て奥) に紫のプラズマボールが現れ、
    /// 物の中心とボールの芯から紫のびりびりが出続けて、物とボールが一緒に少しずつ縮んで消える (Warp)。
    /// 点数は大きく書いてあり、いつもプレイヤーの方を向く。反撃はしない。
    /// </summary>
    public class UnknownObject : MonoBehaviour
    {
        public string displayName = "COW";
        [Tooltip("どのゲート (UnknownStage.gates の番号) から出てくるか")]
        public int gateIndex;
        [Tooltip("回る・向きを変える見た目の部分 (前は +Z)")]
        public Transform body;
        [Tooltip("当たり判定 (kind = UnknownObject。visual は空のオブジェクト)")]
        public Target hitTarget;
        [Tooltip("大きく書いた点数 (Launch のたびに書き直す)")]
        public TextMesh[] pointLabels;
        [Tooltip("点数の札 (いつもプレイヤーの方を向く)")]
        public Transform labelRoot;
        public int[] pointChoices = { 100 };
        [Tooltip("進む方へ前 (+Z) を向ける (乗り物・動物)。OFF なら spinAxis で回る")]
        public bool faceTravel;
        [Tooltip("いつもプレイヤーの方に表 (-Z) を向ける (雪だるまなど)")]
        public bool facePlayer;
        public Vector3 spinAxis = Vector3.up;
        public float spinSpeed = 30f;
        [Tooltip("上下にゆらゆらする幅 (m) / 漂う速さ (m/秒)")]
        public float bob = 0.15f;
        public float drift = 0.3f;
        [Tooltip("消える秒数")]
        public float fadeTime = 0.6f;
        [Tooltip("消えきる時に散る、きらきらの粒の材質と広がり (m)")]
        public Material sparkleMaterial;
        public float size = 0.6f;
        [Tooltip("転送される時のびりびり (紫の稲妻) の材質 / 縮んで消えるまでの秒数")]
        public Material boltMaterial;
        public float warpTime = 0.8f;
        [Tooltip("転送される時に、物の後ろに出るプラズマボール (ふだんは隠しておく。大きさはこの物の localScale)")]
        public Transform plasma;

        const float SinkGuard = 0.4f; // 出てすぐ (起き上がりの途中) は当たったと数えない

        public bool IsFlying { get; private set; }
        public bool IsBusy => IsFlying || fading || warping;
        public int Points => hitTarget != null ? hitTarget.points : 0;

        System.Action<UnknownObject> onDown;
        Vector3 start, control, dest, driftDir;
        float travel, hover, t, fadeT, phase, warpT, nextBolt;
        bool fading, armed, popIn, initialized, warping;
        Vector3 fullScale, plasmaScale;
        BoxCollider box; // 当たり判定の箱 (体の真ん中を測る)

        void Awake() => Init();

        // 元の大きさを覚える。隠れたまま一度も出ていない物は Awake がまだ呼ばれていないので、Launch / HideNow からも呼ぶ
        // (覚える前に HideNow で fullScale (= 0) を入れてしまうと、次に出た時に本体が大きさ 0 のまま見えず、当たり判定もなくなる)
        void Init()
        {
            if (initialized) return;
            initialized = true;
            fullScale = body.localScale;
            box = body.GetComponent<BoxCollider>();
            if (plasma != null) { plasmaScale = plasma.localScale; plasma.gameObject.SetActive(false); }
            hitTarget.SetAvailable(false);
        }

        /// <summary>from (ゲートの奥) から mouth (入り口) をくぐって to まで travelTime 秒で来て、hoverTime 秒漂う。from と to が同じなら、その場にぽんと現れる</summary>
        public void Launch(Vector3 from, Vector3 mouth, Vector3 to, float travelTime, float hoverTime, System.Action<UnknownObject> downCallback)
        {
            Init();
            gameObject.SetActive(true);
            start = from;
            dest = to;
            control = 2f * mouth - 0.5f * (from + to); // 2次ベジェ曲線がちょうど真ん中で入り口を通るように
            travel = Mathf.Max(0.2f, travelTime);
            hover = hoverTime;
            t = 0f;
            phase = Random.value * 10f;
            var flat = to - mouth;
            flat.y = 0f;
            driftDir = flat.sqrMagnitude > 0.01f ? Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * flat.normalized : Vector3.forward;
            fading = armed = false;
            onDown = downCallback;
            transform.position = start;
            popIn = (to - from).sqrMagnitude < 0.0001f; // その場に現れる物 (時計・ドア): ぽんとふくらんで出る
            body.localScale = popIn ? fullScale * 0.001f : fullScale;
            if (labelRoot != null) labelRoot.localScale = Vector3.one;
            IsFlying = true;
            hitTarget.points = pointChoices.Length > 0 ? pointChoices[Random.Range(0, pointChoices.Length)] : 100;
            foreach (var label in pointLabels) if (label != null) label.text = hitTarget.points.ToString();
            hitTarget.SetAvailable(true);
            hitTarget.Raise(9999f);
        }

        /// <summary>撃たれずに去る (Unknown ステージの終わり)。小さくなって消える</summary>
        public void Retreat()
        {
            if (IsFlying) BeginFade();
        }

        /// <summary>その場ですぐに消す</summary>
        public void HideNow()
        {
            Init();
            IsFlying = fading = warping = false;
            if (plasma != null) { plasma.localScale = plasmaScale; plasma.gameObject.SetActive(false); }
            hitTarget.SetAvailable(false);
            body.localScale = fullScale;
            if (labelRoot != null) labelRoot.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (warping)
            {
                UpdateWarp(dt);
                return;
            }
            if (fading)
            {
                fadeT += dt / Mathf.Max(0.05f, fadeTime);
                float s = Mathf.Max(0.001f, 1f - fadeT);
                body.localScale = fullScale * s;
                if (labelRoot != null) labelRoot.localScale = Vector3.one * s;
                if (fadeT >= 1f) HideNow();
                return;
            }
            if (!IsFlying) return;

            t += dt;
            if (popIn && t < 0.6f)
            {
                float k = Mathf.Clamp01(t / 0.6f) - 1f;
                body.localScale = fullScale * Mathf.Max(0.001f, 1f + 2.7f * k * k * k + 1.7f * k * k);
            }
            else if (popIn) body.localScale = fullScale;
            Vector3 heading;
            if (t < travel)
            {
                // ゲートの奥から入り口をくぐって、プレイヤーのまわりへ (着く前にゆっくりになる)
                float u = t / travel;
                u = 1f - (1f - u) * (1f - u) * 0.6f - (1f - u) * 0.4f; // 少しだけ減速
                transform.position = Bezier(u);
                heading = Bezier(Mathf.Min(1f, u + 0.02f)) - Bezier(Mathf.Max(0f, u - 0.02f));
            }
            else
            {
                float h = t - travel;
                // ぷかぷか: 止まった高さのまま、横に少しずつ流れながら上下にゆらゆら (下へは沈んでいかない)
                transform.position = dest + driftDir * (drift * h) + Vector3.up * (Mathf.Sin((h + phase) * 1.4f) * bob);
                heading = driftDir;
                if (h >= hover) BeginFade();
            }
            if (faceTravel)
            {
                heading.y = 0f;
                if (heading.sqrMagnitude > 0.0001f)
                    body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(heading.normalized) * Quaternion.Euler(0f, 0f, Mathf.Sin((t + phase) * 2f) * 4f), dt * 4f);
            }
            else if (!facePlayer) body.Rotate(spinAxis, spinSpeed * dt, Space.Self);

            if (hitTarget.IsUp) armed = true;
            if (armed && !hitTarget.IsUp && t > SinkGuard) Warp(); // 電撃が当たった
        }

        Vector3 Bezier(float u)
        {
            float a = 1f - u;
            return a * a * start + 2f * a * u * control + u * u * dest;
        }

        void LateUpdate()
        {
            if (!IsFlying || Camera.main == null) return;
            Vector3 eye = Camera.main.transform.position;
            if (facePlayer)
            {
                var flat = body.position - eye;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.001f) body.rotation = Quaternion.LookRotation(flat) * Quaternion.Euler(0f, 0f, Mathf.Sin((t + phase) * 1.7f) * 8f);
            }
            if (labelRoot != null) labelRoot.rotation = Quaternion.LookRotation(labelRoot.position - eye);
        }

        // 電撃が当たった: 別の世界へ転送される (点数はすぐ入る。見た目は UpdateWarp で、びりびりしながら縮んで消える)
        void Warp()
        {
            IsFlying = false;
            warping = true;
            warpT = 0f;
            nextBolt = 0f;
            if (plasma != null)
            {
                plasma.localScale = plasmaScale * 0.001f;
                plasma.gameObject.SetActive(true);
                PlacePlasma(Center(), 0.001f);
            }
            Sfx.Play("warpzap");
            onDown?.Invoke(this);
        }

        // 物の後ろにプラズマボールがふくらんで現れ、物の中心とボールの芯から紫のびりびりが出続ける。
        // 物 (体と点数の札) とボールが一緒に少しずつ縮んでいき、消えきる時に小さな光の粒が散る
        void UpdateWarp(float dt)
        {
            warpT += dt / Mathf.Max(0.05f, warpTime);
            var center = Center();
            if (warpT >= 1f)
            {
                if (sparkleMaterial != null) SpaceDebris.Spawn(center, size * 0.4f, sparkleMaterial, 6);
                HideNow();
                return;
            }
            float s = Mathf.Max(0.001f, 1f - warpT * warpT * (3f - 2f * warpT)); // だんだん縮む (はじめと終わりはゆっくり)
            body.localScale = fullScale * s;
            if (labelRoot != null) labelRoot.localScale = Vector3.one * s;
            float grow = Mathf.Clamp01(warpT / 0.15f); // ボールは、はじめにぱっとふくらんでから一緒に縮む
            float ps = Mathf.Max(0.001f, s * (1f - (1f - grow) * (1f - grow)));
            if (plasma != null) PlacePlasma(center, ps);

            if (boltMaterial != null && warpT >= nextBolt)
            {
                nextBolt = warpT + 0.05f / Mathf.Max(0.05f, warpTime);
                float width = Mathf.Lerp(0.055f, 0.02f, warpT);
                float reach = size * (0.9f + 0.6f * (1f - warpT));
                for (int i = 0; i < 2; i++) // 物の中心から四方へ
                    LightningBolt.Spawn(center, center + Random.onUnitSphere * reach * Random.Range(0.6f, 1.1f), boltMaterial,
                                        width, Random.Range(0.07f, 0.12f), 0.25f, body);
                if (plasma != null) // ボールの芯から外殻へ (プラズマボールの中の光の筋)
                {
                    float r = plasmaScale.x * ps * 0.48f;
                    for (int i = 0; i < 3; i++)
                        LightningBolt.Spawn(plasma.position, plasma.position + Random.onUnitSphere * r, boltMaterial,
                                            width * 0.8f, Random.Range(0.06f, 0.1f), 0.3f, plasma);
                }
            }
        }

        // プラズマボールを、物の真後ろ (プレイヤーから見て奥) に、大きさ k (元の大きさに対する割合) で置く
        void PlacePlasma(Vector3 center, float k)
        {
            var away = Camera.main != null ? center - Camera.main.transform.position : body.forward;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
            plasma.position = center + away * (plasmaScale.x * k * 0.45f); // (半分ほど物の後ろに隠れる)
            plasma.localScale = plasmaScale * k;
        }

        // 体の真ん中 (当たり判定の箱の中心。なければ体の原点)
        Vector3 Center() => box != null ? body.TransformPoint(box.center) : body.position;

        void BeginFade()
        {
            IsFlying = false;
            fading = true;
            fadeT = 0f;
            hitTarget.SetAvailable(false);
        }
    }
}
