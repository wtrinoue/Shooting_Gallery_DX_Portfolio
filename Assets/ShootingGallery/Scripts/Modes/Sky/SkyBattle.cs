using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 空中ステージの舞台。床 (地面・穴のふち) がその場で消えて一面の空になり (屋台はばらばらに空から落ちていく。GameManager)、プレイヤーの足元にプロペラ機がせり上がる (プレイヤーは胴体の真ん中の上に立つ)。
    /// 帰りは足元から床が広がって戻り、屋台はパラシュートで降りてくる (GameManager)。
    /// まわりの空を気球や敵のプロペラ機が次々に飛び、マシンガンで何発か当てると撃墜できる (大きく書かれた点数が入る)。反撃はない。
    /// 屋台の出し入れ・点数・終わる時間の判断は GameManager が行い、ここは見た目と敵の出し方だけを受け持つ。
    ///
    /// 酔い対策: プレイヤーの位置も向きも変えず、翼も揺らさない。揺れるのは敵だけ。
    /// 飛んで見えるのは、まわりの雲と風の筋を後ろへ流しているから (scrollSpeed = 0 で止まる)。
    /// </summary>
    public class SkyBattle : MonoBehaviour
    {
        [Tooltip("自分のプロペラ機 (翼・胴体・エンジン)。下からせり上がる")]
        public Transform plane;
        public float planeHiddenDepth = 6f;
        [Tooltip("空中ステージの間は消しておく床の部品 (地面・穴のふち)")]
        public Transform[] groundParts;
        [Tooltip("後ろへ流す景色 (雲・風の筋)。ConveyorBelt のローカル X が後ろ向きになるように置く")]
        public ConveyorBelt[] scenery;
        [Tooltip("それぞれの景色の流れる速さの倍率 (scenery と同じ順)")]
        public float[] sceneryFactors;
        [Tooltip("飛んで見えるように景色を流す速さ (m/秒)。0 で止まる (酔いやすい人向け)")]
        public float scrollSpeed = 3f;
        [Tooltip("空中ステージの間だけ表示する物 (下に広がる雲の海など)")]
        public GameObject[] showDuringStage;

        [Header("敵 (時間切れまで、空いている敵からランダムに次々出てくる。入れる数で出やすさが決まる)")]
        public SkyEnemy[] enemies;
        [Tooltip("次の敵が出てくるまでの秒数 (この範囲でランダム)")]
        public Vector2 launchInterval = new Vector2(0.8f, 1.4f);
        [Tooltip("同時に飛んでいる敵の最大数")]
        public int maxFlying = 5;
        [Tooltip("終わりのこの秒数前からは新しい敵を出さない (出てきてすぐ去るのを防ぐ)")]
        public float stopLaunchBefore = 2f;

        [Header("BGM")]
        [Tooltip("自分のプロペラ機に乗った (足元にせり上がりきった) 時から流す曲 (Assets/Source/sky_stage/oozora.mp3。音楽の卵「大空は君のもの」。クレジットは Resources/THIRD_PARTY_NOTICES.txt)。空なら流さない")]
        public AudioClip bgmClip;
        [Range(0f, 1f)] public float bgmVolume = 0.5f;
        [Tooltip("曲がふわっと大きくなる / 消えていく秒数")]
        public float bgmFade = 1f;

        [Header("Timing")]
        [Tooltip("始まってから終わるまでの秒数 (屋台が落ちる時間も含む)")]
        public float duration = 18f;
        [Tooltip("始まってから翼がせり上がり始めるまでの秒数")]
        public float dropDelay = 1.2f;
        [Tooltip("翼がせり上がる (沈む) 秒数")]
        public float riseTime = 2.5f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回撃墜した数</summary>
        public int Downed { get; private set; }
        /// <summary>敵を撃墜した瞬間 (GameManager が演出を出す)</summary>
        public event System.Action<SkyEnemy> EnemyDown;

        float startTime;
        readonly System.Collections.Generic.List<SkyEnemy> free = new System.Collections.Generic.List<SkyEnemy>(); // 空いている敵 (使い回す)
        float nextLaunchTime;
        float lastAngle;
        Coroutine anim;
        Vector3 planeRest;
        Vector3[] groundRest;
        bool[] groundWasActive;
        float currentRise;
        AudioSource bgm;
        float bgmTarget;   // 曲の音量の行き先 (0 になったら止める)
        bool bgmStarted;   // 今回の空中ステージで、もう曲を流し始めたか

        void Awake()
        {
            planeRest = plane.localPosition;
            groundRest = new Vector3[groundParts.Length];
            groundWasActive = new bool[groundParts.Length];
            groundScale = new Vector3[groundParts.Length];
            for (int i = 0; i < groundParts.Length; i++)
            {
                groundRest[i] = groundParts[i].localPosition;
                groundScale[i] = groundParts[i].localScale;
            }
            SetRise(0f);
            SetStageVisible(false);
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.playOnAwake = false;
            bgm.loop = true;
            bgm.spatialBlend = 0f; // どこにいても同じ大きさ
            bgm.volume = 0f;
        }

        /// <summary>空中ステージを始める (少し待ってから地面を落とし、翼をせり上げ、敵を出し始める)。yawDeg = 自分のプロペラ機の前 (機首) を向ける向き (度。撃った的の方)</summary>
        public void Begin(float yawDeg)
        {
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f); // 機首 (と雲の流れ) を撃った的の方へ。翼・雲・敵はみなこの子なので、いっしょに回る (落とす地面は子ではないが、真下へ落とすだけなので向きは関係ない)
            nextLaunchTime = Time.time + dropDelay + riseTime * 0.8f; // 翼がせり上がり終わる頃に1機目
            Downed = 0;
            lastAngle = Random.Range(0f, 360f);
            bgmStarted = false;
            if (bgmClip != null && bgmClip.loadState == AudioDataLoadState.Unloaded) bgmClip.LoadAudioData(); // 乗るまでに読み込んでおく
            for (int i = 0; i < scenery.Length; i++)
                scenery[i].speed = scrollSpeed * (sceneryFactors != null && i < sceneryFactors.Length ? sceneryFactors[i] : 1f);
            // 床 (地面・穴のふち) はその場できれいさっぱり消す。下は一面の空と雲の海 (屋台は GameManager が空中から落とす)
            for (int i = 0; i < groundParts.Length; i++)
            {
                groundWasActive[i] = groundParts[i].gameObject.activeSelf;
                groundParts[i].gameObject.SetActive(false);
            }
            SetStageVisible(true);
            Play(Rise(1f, dropDelay));
        }

        /// <summary>空中ステージを終える。残っている敵は去り、足元から床が広がって戻り、翼が沈む (屋台は GameManager がパラシュートで降ろす)</summary>
        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            foreach (var e in enemies) e.Retreat();
            bgmTarget = 0f; // 曲はふわっと消える
            if (groundRoutine != null) StopCoroutine(groundRoutine);
            groundRoutine = StartCoroutine(GrowGround());
            Play(Rise(0f, 0f));
        }

        [Tooltip("帰りに床が足元から広がって戻る秒数")]
        public float groundGrowTime = 0.6f;
        Coroutine groundRoutine;
        Vector3[] groundScale;

        // 床が足元 (原点) から外へ広がって戻る
        IEnumerator GrowGround()
        {
            for (int i = 0; i < groundParts.Length; i++)
            {
                groundParts[i].localPosition = groundRest[i];
                if (groundWasActive[i]) groundParts[i].gameObject.SetActive(true);
            }
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, groundGrowTime))
            {
                float e = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < groundParts.Length; i++)
                    groundParts[i].localScale = new Vector3(groundScale[i].x * Mathf.Max(0.001f, e), groundScale[i].y, groundScale[i].z * Mathf.Max(0.001f, e));
                yield return null;
            }
            for (int i = 0; i < groundParts.Length; i++) groundParts[i].localScale = groundScale[i];
            groundRoutine = null;
        }

        void Update()
        {
            // BGM の音量をなめらかに行き先へ (消えきったら止める)
            if (bgm != null && bgm.isPlaying)
            {
                bgm.volume = Mathf.MoveTowards(bgm.volume, bgmTarget, Time.deltaTime * bgmVolume / Mathf.Max(0.05f, bgmFade));
                if (bgmTarget <= 0f && bgm.volume <= 0f) bgm.Stop();
            }
            if (!IsActive) return;
            if (Time.time < nextLaunchTime || TimeLeft < stopLaunchBefore) return;
            nextLaunchTime = Time.time + Random.Range(launchInterval.x, launchInterval.y);

            int flying = 0;
            free.Clear();
            foreach (var e in enemies)
            {
                if (e.IsFlying) flying++;
                if (!e.IsBusy) free.Add(e);
            }
            if (flying >= maxFlying || free.Count == 0) return;
            // 前の敵と離れた方向から出す (プレイヤーがまわりを見回すように)
            lastAngle += Random.Range(80f, 150f) * (Random.value < 0.5f ? -1f : 1f);
            free[Random.Range(0, free.Count)].Launch(lastAngle, OnEnemyDown);
        }

        void OnEnemyDown(SkyEnemy enemy)
        {
            Downed++;
            EnemyDown?.Invoke(enemy);
        }

        IEnumerator Rise(float to, float delay)
        {
            if (to > 0f) SetStageVisible(true);
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float from = currentRise;
            for (float t = 0f; t < 1f; t += Time.deltaTime / riseTime)
            {
                SetRise(Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
                yield return null;
            }
            SetRise(to);
            if (to <= 0f) SetStageVisible(false);
            anim = null;
        }

        void SetRise(float k)
        {
            currentRise = k;
            // 翼は前半でせり上がる (床はもう消えているので動かさない)
            float wing = Mathf.Clamp01(k / 0.5f);
            wing = wing * wing * (3f - 2f * wing);
            plane.localPosition = planeRest + Vector3.down * (planeHiddenDepth * (1f - wing));
            // プロペラ機が足元までせり上がりきった (プレイヤーが乗った): 曲を頭から流す
            if (IsActive && !bgmStarted && wing >= 1f) StartBgm();
        }

        void StartBgm()
        {
            bgmStarted = true;
            if (bgm == null || bgmClip == null) return;
            bgm.clip = bgmClip;
            bgm.time = 0f;
            bgm.volume = 0f;
            bgm.Play();
            bgmTarget = bgmVolume;
        }

        void SetStageVisible(bool visible)
        {
            plane.gameObject.SetActive(visible);
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
