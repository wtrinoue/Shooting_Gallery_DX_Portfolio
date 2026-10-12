using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 西部劇モードの舞台。屋台が沈んだあと目の前がいったん暗くなり、明るくなると、プレイヤーは機関車・炭水車・客車3両の列車の
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

        [Header("Timing")]
        public float duration = 19f;
        [Tooltip("始まってから暗くなり始めるまでの秒数 (屋台が沈み終わるのを待つ)")]
        public float dropDelay = 1.2f;
        [Tooltip("暗くなる / 真っ暗なまま入れ替える / 明るくなる 秒数")]
        public float fadeOutTime = 0.7f;
        public float darkHold = 0.4f;
        public float fadeInTime = 0.9f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回倒した数</summary>
        public int Downed { get; private set; }
        public event System.Action<WesternRider> RiderDown;
        /// <summary>片付けにかかる秒数</summary>
        public float CloseTime => fadeOutTime + darkHold + fadeInTime * 0.5f + 0.1f;
        /// <summary>真っ暗な間に列車の上へ来た時 / いつもの地面へ戻った時 (GameManager が空や霧の色を切り替える)</summary>
        public event System.Action Arrived, Left;

        float startTime, nextLaunchTime;
        bool lastLeft;
        Coroutine anim;
        bool onTrain;
        bool[] groundWasActive;
        float wheelSpeed;
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
            nextLaunchTime = Time.time + dropDelay + fadeOutTime + darkHold + fadeInTime * 0.5f;
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
            if (onTrain)
                foreach (var w in wheels) w.Rotate(wheelSpeed * Time.deltaTime, 0f, 0f, Space.Self);
            if (!IsActive) return;
            if (Time.time < nextLaunchTime || TimeLeft < stopLaunchBefore) return;
            nextLaunchTime = Time.time + Random.Range(launchInterval.x, launchInterval.y);

            int running = 0;
            var free = new System.Collections.Generic.List<WesternRider>();
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
                    if (t != null) Destroy(t.gameObject);
                    smoke.RemoveAt(i);
                    continue;
                }
                t.position += (-transform.forward * scrollSpeed * 0.9f + Vector3.up * 1.2f) * dt;
                t.localScale = Vector3.one * (0.4f + age * 0.8f);
                smoke[i] = (t, age);
            }
            if (!IsActive || !onTrain || smokeStack == null || Time.time < nextSmokeTime) return;
            nextSmokeTime = Time.time + 0.18f;
            var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "Smoke";
            Destroy(puff.GetComponent<Collider>());
            var r = puff.GetComponent<Renderer>();
            r.sharedMaterial = smokeMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            puff.transform.position = smokeStack.position;
            puff.transform.localScale = Vector3.one * 0.4f;
            smoke.Add((puff.transform, 0f));
        }

        // 始まり: 屋台が沈んだら目の前を暗くし、真っ暗な間に列車の屋根の上へ (床は動かさない)。汽笛とともに明るくなる
        IEnumerator Enter()
        {
            yield return new WaitForSeconds(dropDelay);
            ScreenFader.FadeTo(1f, fadeOutTime);
            yield return new WaitForSeconds(fadeOutTime + darkHold * 0.5f);
            SetOnTrain(true);
            Arrived?.Invoke();
            yield return new WaitForSeconds(darkHold * 0.5f);
            Sfx.Play("whistle");
            ScreenFader.FadeTo(0f, fadeInTime);
            anim = null;
        }

        // 終わり: 暗くして、真っ暗な間にいつものお祭りの地面へ戻す
        IEnumerator Leave()
        {
            ScreenFader.FadeTo(1f, fadeOutTime);
            yield return new WaitForSeconds(fadeOutTime + darkHold * 0.5f);
            SetOnTrain(false);
            Left?.Invoke();
            yield return new WaitForSeconds(darkHold * 0.5f);
            ScreenFader.FadeTo(0f, fadeInTime);
            anim = null;
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
