using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 西部劇モードの舞台。屋台が沈んだあと砂嵐が吹きつけて何も見えなくなり、砂嵐が晴れると、プレイヤーは機関車・炭水車・客車3両の列車の
    /// 1両目の屋根の上にもう立っている (床が動くところは見せない)。列車は的を撃った方へまっすぐ走っていて、線路の枕木・荒野のでこぼこ・サボテン・
    /// 電柱・遠くの岩山が後ろへ流れ、タンブルウィードが転がっていく。まわりを馬に乗ったカウボーイ (たまに馬車) が並んで走る。
    /// 屋台の出し入れ・点数・終わる時間の判断は GameManager が行い、ここは見た目と的の出し方だけを受け持つ。
    ///
    /// 酔い対策: プレイヤーの位置も向きも変えず、列車も揺らさない。走って見えるのは景色を後ろへ流しているから (scrollSpeed = 0 で止まる)。
    /// </summary>
    public class WesternTrain : MonoBehaviour
    {
        [Tooltip("列車 (機関車・炭水車・客車)")]
        public Transform train;
        [Tooltip("お祭りの地面の部品 (地面・穴のふち・足元の目印・ボーナスステージ)。列車の上にいる間は隠す")]
        public Transform[] groundParts;
        [Tooltip("西部劇モードの間だけ表示する物 (荒野の地面・線路のレール)")]
        public GameObject[] showDuringStage;
        [Tooltip("後ろへ流す景色。ConveyorBelt のローカル X が後ろ向きになるように置く")]
        public ConveyorBelt[] scenery;
        public float[] sceneryFactors;
        [Tooltip("列車が走って見えるように景色を流す速さ (m/秒)。0 で止まる")]
        public float scrollSpeed = 6f;
        [Tooltip("回る車輪")]
        public Transform[] wheels;
        [Tooltip("煙突の先 (煙が出る所)")]
        public Transform smokeStack;
        public Material smokeMaterial;

        [Header("的 (時間切れまで、空いている的からランダムに次々出てくる)")]
        public WesternRider[] riders;
        public Vector2 launchInterval = new Vector2(0.8f, 1.5f);
        public int maxRunning = 5;
        public float stopLaunchBefore = 2f;

        [Header("砂嵐 (行きも帰りも、砂嵐で何も見えない間に舞台を入れ替える)")]
        [Tooltip("視界を覆う砂の色")]
        public Color sandColor = new Color(0.78f, 0.62f, 0.4f);
        [Tooltip("吹き飛んでいく砂粒の材質 (半透明。なければ煙の材質を使う)")]
        public Material sandMaterial;
        [Tooltip("1秒あたりに飛ばす砂粒の数")]
        public float grainsPerSecond = 160f;

        [Header("Timing")]
        public float duration = 19f;
        [Tooltip("始まってから砂嵐が来るまでの秒数 (屋台が沈み終わるのを待つ)")]
        public float dropDelay = 1.2f;
        [Tooltip("砂嵐で視界が覆われるまで / 何も見えないまま入れ替える / 砂嵐が晴れるまで の秒数")]
        public float stormInTime = 1.1f;
        public float stormHold = 0.7f;
        public float stormOutTime = 1.4f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回倒した数</summary>
        public int Downed { get; private set; }
        public event System.Action<WesternRider> RiderDown;
        /// <summary>片付けにかかる秒数</summary>
        public float CloseTime => stormInTime + stormHold + stormOutTime * 0.5f + 0.1f;
        /// <summary>砂嵐で何も見えない間に列車の上へ来た時 / いつもの地面へ戻った時 (GameManager が空や霧の色を切り替え、帰りは屋台と的を用意する)</summary>
        public event System.Action Arrived, Left;

        float startTime, nextLaunchTime;
        readonly System.Collections.Generic.List<WesternRider> free = new System.Collections.Generic.List<WesternRider>(); // 空いている的 (使い回す)
        bool lastLeft;
        Coroutine anim;
        bool onTrain;
        bool[] groundWasActive;
        float wheelSpeed;
        // 砂嵐
        float sandLevel, sandAccum; // 砂嵐の強さ (0〜1) / 次の砂粒までのたまり
        Vector3 windDir = Vector3.right;
        readonly System.Collections.Generic.List<Grain> grains = new System.Collections.Generic.List<Grain>();
        float nextSmokeTime;
        readonly System.Collections.Generic.List<(Transform t, float age)> smoke = new System.Collections.Generic.List<(Transform, float)>();

        void Awake()
        {
            groundWasActive = new bool[groundParts.Length];
            SetStageVisible(false);
        }

        /// <summary>西部劇モードを始める。yawDeg = 列車の進む向き (的を撃った方)</summary>
        public void Begin(float yawDeg)
        {
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            nextLaunchTime = Time.time + dropDelay + stormInTime + stormHold + stormOutTime * 0.5f;
            Downed = 0;
            transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            for (int i = 0; i < scenery.Length; i++)
                scenery[i].speed = scrollSpeed * (sceneryFactors != null && i < sceneryFactors.Length ? sceneryFactors[i] : 1f);
            Play(Enter());
        }

        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            foreach (var r in riders) r.Retreat();
            Play(Leave());
        }

        void Update()
        {
            UpdateSmoke();
            UpdateSand();
            if (onTrain)
                foreach (var w in wheels) w.Rotate(wheelSpeed * Time.deltaTime, 0f, 0f, Space.Self);
            if (!IsActive) return;
            if (Time.time < nextLaunchTime || TimeLeft < stopLaunchBefore) return;
            nextLaunchTime = Time.time + Random.Range(launchInterval.x, launchInterval.y);

            int running = 0;
            free.Clear();
            foreach (var r in riders)
            {
                if (r.IsRunning) running++;
                if (!r.IsBusy) free.Add(r);
            }
            if (running >= maxRunning || free.Count == 0) return;
            lastLeft = Random.value < 0.35f ? lastLeft : !lastLeft; // だいたい左右交互に
            free[Random.Range(0, free.Count)].Launch(lastLeft, OnRiderDown);
        }

        void OnRiderDown(WesternRider r)
        {
            Downed++;
            RiderDown?.Invoke(r);
        }

        // 煙突から煙がもくもく出て、後ろへ流れながら広がって消える
        void UpdateSmoke()
        {
            float dt = Time.deltaTime;
            for (int i = smoke.Count - 1; i >= 0; i--)
            {
                var (t, age) = smoke[i];
                age += dt;
                if (t == null || age > 2.2f)
                {
                    if (t != null) FxPool.Release("TrainSmoke", t.gameObject);
                    smoke.RemoveAt(i);
                    continue;
                }
                t.position += (-transform.forward * scrollSpeed * 0.9f + Vector3.up * 1.2f) * dt;
                t.localScale = Vector3.one * (0.4f + age * 0.8f);
                smoke[i] = (t, age);
            }
            if (!IsActive || !onTrain || smokeStack == null || Time.time < nextSmokeTime) return;
            nextSmokeTime = Time.time + 0.18f;
            var puff = FxPool.Get("TrainSmoke", () => FxPool.Primitive(PrimitiveType.Sphere, smokeMaterial)); // 作って消さずに使い回す
            puff.transform.position = smokeStack.position;
            puff.transform.localScale = Vector3.one * 0.4f;
            smoke.Add((puff.transform, 0f));
        }

        // 始まり: 屋台が沈んだら砂嵐が吹きつけて何も見えなくなり、その間に列車の屋根の上へ (床は動かさない)。汽笛とともに砂嵐が晴れる
        IEnumerator Enter()
        {
            yield return new WaitForSeconds(dropDelay);
            yield return Storm(() => { SetOnTrain(true); Arrived?.Invoke(); }, null); // (汽笛は鳴らさない。ユーザーの指定)
            anim = null;
        }

        // 終わり: また砂嵐に覆われ、その間にいつものお祭りの地面へ戻す (GameManager が屋台と的をもう並べておく)。晴れると屋台が並んでいる
        IEnumerator Leave()
        {
            yield return Storm(() => { SetOnTrain(false); Left?.Invoke(); }, null);
            anim = null;
        }

        // 砂嵐: 風の音とともに砂粒が横殴りに吹き、視界が砂の色に覆われる → 何も見えない間に atPeak (舞台の入れ替え) → だんだん晴れる
        IEnumerator Storm(System.Action atPeak, string clearSound)
        {
            Sfx.Play("sandstorm");
            windDir = transform.right * (Random.value < 0.5f ? 1f : -1f); // 列車の横から吹きつける
            ScreenFader.FadeTo(1f, stormInTime, sandColor);
            for (float t = 0f; t < stormInTime; t += Time.deltaTime)
            {
                sandLevel = t / stormInTime;
                yield return null;
            }
            sandLevel = 1f;
            yield return new WaitForSeconds(stormHold * 0.5f);
            atPeak?.Invoke();
            yield return new WaitForSeconds(stormHold * 0.5f);
            if (clearSound != null) Sfx.Play(clearSound);
            ScreenFader.FadeTo(0f, stormOutTime);
            for (float t = 0f; t < stormOutTime; t += Time.deltaTime)
            {
                sandLevel = 1f - t / stormOutTime;
                yield return null;
            }
            sandLevel = 0f;
        }

        // 砂粒: カメラのまわりに横殴りに飛んでくる細い筋 (と、ときどき大きな砂煙)。砂嵐の強さ (sandLevel) に合わせた数だけ出す
        void UpdateSand()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main;
            for (int i = grains.Count - 1; i >= 0; i--)
            {
                var g = grains[i];
                g.age += dt;
                if (g.t == null || g.age > g.life)
                {
                    if (g.t != null) FxPool.Release(g.puff ? "SandPuff" : "SandGrain", g.t.gameObject);
                    grains.RemoveAt(i);
                    continue;
                }
                g.t.position += g.vel * dt;
                if (!g.puff && cam != null)
                {
                    // 筋の長い方が飛ぶ向きに沿い、面がカメラを向くように
                    var fwd = g.t.position - cam.transform.position;
                    var up = Vector3.Cross(fwd, g.vel);
                    if (fwd.sqrMagnitude > 0.0001f && up.sqrMagnitude > 0.0001f) g.t.rotation = Quaternion.LookRotation(fwd, up);
                }
                grains[i] = g;
            }

            var mat = sandMaterial != null ? sandMaterial : smokeMaterial;
            if (sandLevel <= 0f || cam == null || mat == null) { sandAccum = 0f; return; }
            sandAccum += grainsPerSecond * sandLevel * dt;
            while (sandAccum >= 1f)
            {
                sandAccum -= 1f;
                SpawnGrain(cam.transform.position, mat, Random.value < 0.08f);
            }
        }

        void SpawnGrain(Vector3 eye, Material mat, bool puff)
        {
            // 1秒に何百個も出るので、作って消さずに使い回す
            var go = FxPool.Get(puff ? "SandPuff" : "SandGrain", () => FxPool.Primitive(puff ? PrimitiveType.Sphere : PrimitiveType.Quad, mat));
            go.GetComponent<Renderer>().sharedMaterial = mat;
            // カメラのまわり (少し風上) に出して、風下へ吹き飛ばす
            var dir = Quaternion.Euler(Random.Range(-25f, 20f), Random.Range(0f, 360f), 0f) * Vector3.forward;
            float dist = puff ? Random.Range(2f, 6f) : Random.Range(0.4f, 4.5f);
            go.transform.position = eye + dir * dist - windDir * Random.Range(0f, 3f);
            go.transform.localScale = puff ? Vector3.one * Random.Range(0.8f, 1.8f)
                                           : new Vector3(Random.Range(0.12f, 0.35f), Random.Range(0.012f, 0.03f), 1f);
            var vel = windDir * (puff ? Random.Range(5f, 8f) : Random.Range(10f, 18f)) + Vector3.down * Random.Range(0f, 1.5f) + Random.insideUnitSphere * 1.5f;
            grains.Add(new Grain { t = go.transform, vel = vel, life = puff ? Random.Range(0.7f, 1.1f) : Random.Range(0.35f, 0.7f), puff = puff });
        }

        struct Grain
        {
            public Transform t;
            public Vector3 vel;
            public float age, life;
            public bool puff;
        }

        // 列車の屋根の上にいる (荒野・列車・景色を出して、お祭りの地面を隠す) / いない
        void SetOnTrain(bool on)
        {
            if (on == onTrain) return;
            onTrain = on;
            for (int i = 0; i < groundParts.Length; i++)
            {
                if (on) groundWasActive[i] = groundParts[i].gameObject.activeSelf;
                groundParts[i].gameObject.SetActive(!on && groundWasActive[i]);
            }
            SetStageVisible(on);
            wheelSpeed = scrollSpeed / 0.5f * Mathf.Rad2Deg; // 半径 0.5m の車輪が景色と同じ速さで回る
        }

        void SetStageVisible(bool visible)
        {
            train.gameObject.SetActive(visible);
            foreach (var s in scenery) s.gameObject.SetActive(visible);
            if (showDuringStage != null)
                foreach (var go in showDuringStage) if (go != null) go.SetActive(visible);
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
