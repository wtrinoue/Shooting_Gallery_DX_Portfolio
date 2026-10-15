using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 空中ステージの舞台。屋台が沈んだあと地面が遠くへ落ちていき (床がなくなる)、プレイヤーの足元にプロペラ機がせり上がる (プレイヤーは胴体の真ん中の上に立つ)。
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
        [Tooltip("空中ステージの間は遠くへ落としておく地面の部品 (地面・穴のふち・足元の目印・ボーナスステージ)")]
        public Transform[] groundParts;
        [Tooltip("地面が落ちていく深さ (m)。霧で見えなくなる所まで")]
        public float groundDropDepth = 35f;
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

        [Header("Timing")]
        [Tooltip("始まってから終わるまでの秒数 (屋台が沈む・地面が落ちる時間も含む)")]
        public float duration = 18f;
        [Tooltip("始まってから地面が落ち始めるまでの秒数 (屋台が沈み終わるのを待つ)")]
        public float dropDelay = 1.2f;
        [Tooltip("翼がせり上がり、地面が落ちていく (戻る) 秒数")]
        public float riseTime = 2.5f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回撃墜した数</summary>
        public int Downed { get; private set; }
        /// <summary>敵を撃墜した瞬間 (GameManager が演出を出す)</summary>
        public event System.Action<SkyEnemy> EnemyDown;

        float startTime;
        float nextLaunchTime;
        float lastAngle;
        Coroutine anim;
        Vector3 planeRest;
        Vector3[] groundRest;
        bool[] groundWasActive;
        float currentRise;

        void Awake()
        {
            planeRest = plane.localPosition;
            groundRest = new Vector3[groundParts.Length];
            groundWasActive = new bool[groundParts.Length];
            for (int i = 0; i < groundParts.Length; i++) groundRest[i] = groundParts[i].localPosition;
            SetRise(0f);
            SetStageVisible(false);
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
            for (int i = 0; i < scenery.Length; i++)
                scenery[i].speed = scrollSpeed * (sceneryFactors != null && i < sceneryFactors.Length ? sceneryFactors[i] : 1f);
            for (int i = 0; i < groundParts.Length; i++) groundWasActive[i] = groundParts[i].gameObject.activeSelf;
            Play(Rise(1f, dropDelay));
        }

        /// <summary>空中ステージを終える。残っている敵は去り、地面が戻ってきて翼が沈む</summary>
        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            foreach (var e in enemies) e.Retreat();
            for (int i = 0; i < groundParts.Length; i++)
                if (groundWasActive[i]) groundParts[i].gameObject.SetActive(true);
            Play(Rise(0f, 0f));
        }

        void Update()
        {
            if (!IsActive) return;
            if (Time.time < nextLaunchTime || TimeLeft < stopLaunchBefore) return;
            nextLaunchTime = Time.time + Random.Range(launchInterval.x, launchInterval.y);

            int flying = 0;
            var free = new System.Collections.Generic.List<SkyEnemy>();
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
            else // 落ちきった地面は隠す (霧の向こう)
                foreach (var g in groundParts) g.gameObject.SetActive(false);
            anim = null;
        }

        void SetRise(float k)
        {
            currentRise = k;
            // 翼は前半でせり上がり、地面は後半ほど速く落ちていく
            float wing = Mathf.Clamp01(k / 0.5f);
            wing = wing * wing * (3f - 2f * wing);
            plane.localPosition = planeRest + Vector3.down * (planeHiddenDepth * (1f - wing));
            for (int i = 0; i < groundParts.Length; i++)
                groundParts[i].localPosition = groundRest[i] + Vector3.down * (groundDropDepth * k * k);
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
