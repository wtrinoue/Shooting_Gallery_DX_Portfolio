using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 海戦モードの舞台。まず海面がせり上がって床 (地面・穴のふち) をきれいに覆い、覆いきったら床は消して一面の海にする
    /// (屋台は GameManager が海に沈める)。そのあと、プレイヤーの足元に自分の船の甲板がせり上がる。
    /// まわりの海を敵の海賊船が次々に走り、バズーカを当てると旗の点数が入って撃沈できる。反撃はない。時間切れまで船は出続ける。
    /// 屋台の出し入れ・点数・終わる時間の判断は GameManager が行い、ここは見た目と敵の船の出し方だけを受け持つ。
    ///
    /// 酔い対策: プレイヤーの位置も向きも変えず、甲板も揺らさない。揺れるのは敵の船だけ。
    /// 自分の船が進んで見えるのは、まわりの景色 (波しぶき・島・イルカ) を後ろへ流しているから (scrollSpeed = 0 で止まる)。
    /// </summary>
    public class NavalBattle : MonoBehaviour
    {
        [Tooltip("自分の船 (甲板・帆柱・手すり)。海の中からせり上がる")]
        public Transform deck;
        public float deckHiddenDepth = 9f;
        [Tooltip("海面。地面の下からせり上がって地面を覆う")]
        public Transform sea;
        public float seaY = 0.05f;
        public float seaHiddenY = -0.4f;
        [Tooltip("自分の船の大きさ (屋台を船の外の海へはね飛ばし、船の後ろで沈める時に使う。ローカル座標): 横幅の半分 / 船尾の Z")]
        public float shipHalfWidth = 1.65f;
        public float shipSternZ = -3.6f;
        /// <summary>海面の高さ (ワールド)</summary>
        public float SeaWorldY => transform.TransformPoint(0f, seaY, 0f).y;
        [Tooltip("海が覆ったら消す床の部品 (地面・穴のふち)。帰りは海が引く前に戻す")]
        public Transform[] groundParts;
        [Tooltip("屋台が海に沈む時の水しぶき (半透明)")]
        public Material splashMaterial;
        [Tooltip("後ろへ流す景色 (波しぶき・島・イルカ)。ConveyorBelt のローカル X が後ろ向きになるように置く")]
        public ConveyorBelt[] scenery;
        [Tooltip("それぞれの景色の流れる速さの倍率 (scenery と同じ順)")]
        public float[] sceneryFactors;
        [Tooltip("自分の船が進んで見えるように景色を流す速さ (m/秒)。0 で止まる (酔いやすい人向け)")]
        public float scrollSpeed = 2f;

        [Header("敵の船 (時間切れまで、空いている船からランダムに次々出てくる。入れる数で出やすさが決まる)")]
        public EnemyShip[] ships;
        [Tooltip("次の船が出てくるまでの秒数 (この範囲でランダム)")]
        public Vector2 launchInterval = new Vector2(0.9f, 1.6f);
        [Tooltip("同時に走っている船の最大数")]
        public int maxSailing = 5;
        [Tooltip("終わりのこの秒数前からは新しい船を出さない (出てきてすぐ去るのを防ぐ)")]
        public float stopLaunchBefore = 2f;

        [Header("Timing")]
        public float duration = 16.5f; // 屋台が沈むのを待つ分 (dropDelay) も含む
        [Tooltip("始まってすぐ、海面がせり上がって床を覆う秒数")]
        public float seaRiseTime = 0.8f;
        [Tooltip("甲板がせり上がる / 甲板と海面が沈む秒数")]
        public float riseTime = 1.5f;
        [Tooltip("始まってから甲板がせり上がり始めるまでの秒数 (屋台が海に沈むのを待つ)")]
        public float dropDelay = 0f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回撃沈した数</summary>
        public int Sunk { get; private set; }
        /// <summary>敵の船を撃沈した瞬間 (GameManager がボーナス点を入れる)</summary>
        public event System.Action<EnemyShip> ShipSunk;

        float startTime;
        float nextLaunchTime;
        float lastAngle;
        Coroutine anim;
        Vector3 deckRest;
        bool[] groundWasActive;
        readonly List<EnemyShip> free = new List<EnemyShip>(); // 空いている船 (使い回す)

        void Awake()
        {
            deckRest = deck.localPosition;
            if (groundParts == null) groundParts = new Transform[0];
            groundWasActive = new bool[groundParts.Length];
            SetDeck(0f);
            SetSea(0f);
            SetStageVisible(false);
        }

        /// <summary>海戦モードを始める (海面がせり上がって床を覆い、甲板をせり上げ、敵の船を出し始める)。yawDeg = 自分の船の前 (船首) を向ける向き (度。撃った的の方)</summary>
        public void Begin(float yawDeg)
        {
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f); // 船首 (と景色の流れ) を撃った的の方へ。船・海・景色はみなこの子なので、いっしょに回る
            nextLaunchTime = Time.time + dropDelay + riseTime * 0.8f; // 甲板がせり上がり終わる頃に1隻目
            Sunk = 0;
            lastAngle = Random.Range(0f, 360f);
            for (int i = 0; i < scenery.Length; i++)
                scenery[i].speed = scrollSpeed * (sceneryFactors != null && i < sceneryFactors.Length ? sceneryFactors[i] : 1f);
            for (int i = 0; i < groundParts.Length; i++) groundWasActive[i] = groundParts[i].gameObject.activeSelf;
            Play(Enter());
        }

        /// <summary>海戦モードを終える。残っている船は去り、床を戻してから甲板と海面が沈む</summary>
        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            foreach (var ship in ships) ship.Retreat();
            for (int i = 0; i < groundParts.Length; i++)
                if (groundWasActive[i]) groundParts[i].gameObject.SetActive(true); // まだ海の下。海が引くと見えてくる
            Play(Leave());
        }

        void Update()
        {
            if (!IsActive) return;
            if (Time.time < nextLaunchTime || TimeLeft < stopLaunchBefore) return;
            nextLaunchTime = Time.time + Random.Range(launchInterval.x, launchInterval.y);

            int sailing = 0;
            free.Clear();
            foreach (var s in ships)
            {
                if (s.IsSailing) sailing++;
                if (!s.IsBusy) free.Add(s);
            }
            if (sailing >= maxSailing || free.Count == 0) return;
            // 前の船と離れた方向から出す (プレイヤーがまわりを見回すように)
            lastAngle += Random.Range(80f, 150f) * (Random.value < 0.5f ? -1f : 1f);
            free[Random.Range(0, free.Count)].Launch(lastAngle, OnShipSunk);
        }

        void OnShipSunk(EnemyShip ship)
        {
            Sunk++;
            ShipSunk?.Invoke(ship);
        }

        // 海面がせり上がって床を覆い (覆いきったら床を消す)、屋台が沈むのを待ってから甲板をせり上げる
        IEnumerator Enter()
        {
            SetStageVisible(true);
            deck.gameObject.SetActive(false); // 甲板はせり上がる時に出す
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seaRiseTime))
            {
                SetSea(1f - (1f - t) * (1f - t));
                yield return null;
            }
            SetSea(1f);
            foreach (var g in groundParts) g.gameObject.SetActive(false); // 海の下の床は、きれいさっぱり消す
            float wait = dropDelay - seaRiseTime;
            if (wait > 0f) yield return new WaitForSeconds(wait);
            deck.gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.deltaTime / riseTime)
            {
                SetDeck(t * t * (3f - 2f * t));
                yield return null;
            }
            SetDeck(1f);
            anim = null;
        }

        // 甲板と海面が沈んで、床が見えてくる
        IEnumerator Leave()
        {
            float deckFrom = currentDeck, seaFrom = currentSea;
            for (float t = 0f; t < 1f; t += Time.deltaTime / riseTime)
            {
                float e = t * t * (3f - 2f * t);
                SetDeck(Mathf.Lerp(deckFrom, 0f, e));
                SetSea(Mathf.Lerp(seaFrom, 0f, e));
                yield return null;
            }
            SetDeck(0f);
            SetSea(0f);
            SetStageVisible(false);
            anim = null;
        }

        float currentDeck, currentSea;

        void SetDeck(float k)
        {
            currentDeck = k;
            deck.localPosition = deckRest + Vector3.down * (deckHiddenDepth * (1f - k));
        }

        void SetSea(float k)
        {
            currentSea = k;
            var p = sea.localPosition;
            p.y = Mathf.Lerp(seaHiddenY, seaY, k);
            sea.localPosition = p;
        }

        void SetStageVisible(bool visible)
        {
            deck.gameObject.SetActive(visible);
            sea.gameObject.SetActive(visible);
            foreach (var s in scenery) s.gameObject.SetActive(visible);
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
