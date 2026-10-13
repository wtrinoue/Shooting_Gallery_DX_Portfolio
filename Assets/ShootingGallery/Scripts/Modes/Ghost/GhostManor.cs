using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 幽霊モードの舞台。屋台が沈んだあと、ひゅうぅ…という音とともに視界が濃い紫の闇に包まれ、その間に舞台を入れ替える。
    /// 闇が晴れると、屋台もハッチもなく、足元はでこぼこの濃い茶色の土。朽ちたぎざぎざの木・バケツを上げ下ろしする古井戸・お墓が並び、
    /// 的を撃った方の先には、上へいくほど太くなるいびつで豪華なお化け屋敷が建っている。空は紫で、月が照っている。
    /// まわりをお化けがぐるぐる回り、ほうきに乗った魔女が飛び、お墓の前からゾンビや骸骨がこんにちはする (どれも点数の見えるパネル)。
    /// 終わりはまた紫の闇に包まれ、その間にいつものお祭りの地面へ戻す (GameManager が屋台と的をもう並べておく)。
    /// 屋台の出し入れ・点数・空の色の判断は GameManager が行い、ここは見た目の順番と的の出し方だけを受け持つ。プレイヤーは動かない。
    /// </summary>
    public class GhostManor : MonoBehaviour
    {
        [Tooltip("お祭りの地面の部品 (地面・穴のふち・足元の目印)。幽霊モードの間は隠す")]
        public Transform[] groundParts;
        [Tooltip("幽霊モードの間だけ表示する物 (土の地面・木・井戸・お墓・お化け屋敷・月など)")]
        public GameObject[] showDuringStage;

        [Header("古井戸 (バケツを下ろして、また引き上げる)")]
        public Transform wellBucket;
        [Tooltip("ロープ (ローカル Y の大きさがロープの長さ。上端が滑車の軸)")]
        public Transform wellRope;
        [Tooltip("回る巻き上げのハンドル")]
        public Transform wellCrank;
        [Tooltip("バケツの高さ: 上 (井戸のふちの上) / 下 (井戸の中)。井戸のローカル座標")]
        public float bucketTopY = 1.15f;
        public float bucketBottomY = -0.6f;
        [Tooltip("滑車の軸の高さ (井戸のローカル座標)")]
        public float pulleyY = 1.95f;
        [Tooltip("上で止まる / 下ろす / 下で止まる / 引き上げる 秒数")]
        public float bucketWaitTop = 2f, bucketLower = 2.4f, bucketWaitBottom = 1.4f, bucketRaise = 2.8f;

        [Header("ふわふわ漂う鬼火 (的ではない)")]
        public Transform[] wisps;

        [Header("的 (時間切れまで、種類ごとに決まった間隔で出てくる)")]
        public GhostSpook[] ghosts;
        public GhostSpook[] witches;
        [Tooltip("お墓から出てくる的 (ゾンビ・骸骨)")]
        public GhostSpook[] risers;
        [Tooltip("ゾンビ・骸骨が出てくる地面の場所 (お墓の前。このオブジェクトのローカル座標)")]
        public Vector3[] graveSpots;
        public Vector2 ghostInterval = new Vector2(0.7f, 1.3f);
        public Vector2 witchInterval = new Vector2(3.5f, 6f);
        public Vector2 riseInterval = new Vector2(0.9f, 1.7f);
        public int maxGhosts = 6;
        public int maxWitches = 2;
        public int maxRisers = 4;
        public float stopLaunchBefore = 2.5f;

        [Header("紫の闇 (行きも帰りも、何も見えない間に舞台を入れ替える)")]
        public Color darkColor = new Color(0.12f, 0.03f, 0.18f);
        public float darkInTime = 1.0f;
        public float darkHold = 0.6f;
        public float darkOutTime = 1.5f;

        [Header("Timing")]
        public float duration = 24f;
        [Tooltip("始まってから闇に包まれ始めるまでの秒数 (屋台が沈み終わるのを待つ)")]
        public float dropDelay = 1.2f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回倒した数</summary>
        public int Hits { get; private set; }
        /// <summary>片付けにかかる秒数</summary>
        public float CloseTime => darkInTime + darkHold + darkOutTime * 0.5f + 0.1f;
        /// <summary>闇の中で幽霊の世界へ来た時 / いつもの地面へ戻った時 (GameManager が空や霧の色を切り替え、帰りは屋台と的を用意する)</summary>
        public event System.Action Arrived, Left;
        /// <summary>的を倒した (点数はもう入っている)</summary>
        public event System.Action<GhostSpook> SpookDown;

        float startTime, launchStart, nextGhost, nextWitch, nextRise;
        float lastAngle;
        bool inWorld;
        bool[] groundWasActive;
        Coroutine anim;
        float bucketT;
        Vector3[] wispHome;
        float[] wispPhase;

        void Awake()
        {
            groundWasActive = new bool[groundParts.Length];
            wispHome = new Vector3[wisps.Length];
            wispPhase = new float[wisps.Length];
            for (int i = 0; i < wisps.Length; i++)
            {
                wispHome[i] = wisps[i].localPosition;
                wispPhase[i] = Random.value * 10f;
            }
            SetStageVisible(false);
        }

        /// <summary>幽霊モードを始める。yawDeg = お化け屋敷が建つ向き (的を撃った方)</summary>
        public void Begin(float yawDeg)
        {
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            Hits = 0;
            transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            lastAngle = Random.Range(0f, 360f);
            launchStart = Time.time + dropDelay + darkInTime + darkHold + darkOutTime * 0.4f;
            nextGhost = launchStart;
            nextRise = launchStart + 0.6f;
            nextWitch = launchStart + 2f;
            Play(Enter());
        }

        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            foreach (var s in AllSpooks()) s.Retreat();
            Play(Leave());
        }

        IEnumerable<GhostSpook> AllSpooks()
        {
            foreach (var s in ghosts) yield return s;
            foreach (var s in witches) yield return s;
            foreach (var s in risers) yield return s;
        }

        IEnumerator Enter()
        {
            yield return new WaitForSeconds(dropDelay);
            yield return Dark(() => { SetInWorld(true); Arrived?.Invoke(); }, "ghost");
            anim = null;
        }

        IEnumerator Leave()
        {
            yield return Dark(() => { SetInWorld(false); Left?.Invoke(); }, "ghost");
            anim = null;
        }

        // 紫の闇に包まれる → 何も見えない間に atPeak (舞台の入れ替え) → だんだん晴れる
        IEnumerator Dark(System.Action atPeak, string sound)
        {
            if (sound != null) Sfx.Play(sound);
            ScreenFader.FadeTo(1f, darkInTime, darkColor);
            yield return new WaitForSeconds(darkInTime + darkHold * 0.5f);
            atPeak?.Invoke();
            yield return new WaitForSeconds(darkHold * 0.5f);
            ScreenFader.FadeTo(0f, darkOutTime);
        }

        void Update()
        {
            if (inWorld)
            {
                UpdateWell();
                UpdateWisps();
            }
            if (!IsActive || Time.time < launchStart || TimeLeft < stopLaunchBefore) return;
            float now = Time.time;
            if (now >= nextGhost)
            {
                nextGhost = now + Random.Range(ghostInterval.x, ghostInterval.y);
                if (CountOut(ghosts) < maxGhosts) LaunchOrbit(ghosts);
            }
            if (now >= nextWitch)
            {
                nextWitch = now + Random.Range(witchInterval.x, witchInterval.y);
                if (CountOut(witches) < maxWitches) LaunchOrbit(witches);
            }
            if (now >= nextRise)
            {
                nextRise = now + Random.Range(riseInterval.x, riseInterval.y);
                if (CountOut(risers) < maxRisers) LaunchRiser();
            }
        }

        static int CountOut(GhostSpook[] pool)
        {
            int n = 0;
            foreach (var s in pool) if (s.IsBusy) n++;
            return n;
        }

        // 前に出した所と離れた方向から出す (プレイヤーがまわりを見回すように)
        void LaunchOrbit(GhostSpook[] pool)
        {
            var free = Free(pool);
            if (free.Count == 0) return;
            lastAngle += Random.Range(70f, 150f) * (Random.value < 0.5f ? -1f : 1f);
            free[Random.Range(0, free.Count)].LaunchOrbit(lastAngle, OnDown);
        }

        // 空いているお墓を1つ選び、その前の土からゾンビか骸骨をせり上げる
        void LaunchRiser()
        {
            if (graveSpots == null || graveSpots.Length == 0) return;
            var free = Free(risers);
            if (free.Count == 0) return;
            var graves = new List<int>();
            for (int g = 0; g < graveSpots.Length; g++)
            {
                bool used = false;
                foreach (var r in risers) if (r.Grave == g) { used = true; break; }
                if (!used) graves.Add(g);
            }
            if (graves.Count == 0) return;
            int grave = graves[Random.Range(0, graves.Count)];
            free[Random.Range(0, free.Count)].LaunchRise(graveSpots[grave], grave, OnDown);
        }

        static List<GhostSpook> Free(GhostSpook[] pool)
        {
            var list = new List<GhostSpook>();
            foreach (var s in pool) if (!s.IsBusy) list.Add(s);
            return list;
        }

        void OnDown(GhostSpook s)
        {
            Hits++;
            SpookDown?.Invoke(s);
        }

        // 古井戸: 上で止まる → ハンドルが回ってバケツが井戸の中へ下りる → 下で止まる → ゆらゆら揺れながら引き上げられる
        void UpdateWell()
        {
            if (wellBucket == null) return;
            float cycle = bucketWaitTop + bucketLower + bucketWaitBottom + bucketRaise;
            bucketT = (bucketT + Time.deltaTime) % cycle;
            float t = bucketT, y, crankSpeed = 0f;
            if (t < bucketWaitTop) y = bucketTopY;
            else if ((t -= bucketWaitTop) < bucketLower)
            {
                float k = t / bucketLower;
                y = Mathf.Lerp(bucketTopY, bucketBottomY, k * k * (3f - 2f * k));
                crankSpeed = -260f;
            }
            else if ((t -= bucketLower) < bucketWaitBottom) y = bucketBottomY;
            else
            {
                t -= bucketWaitBottom;
                float k = t / bucketRaise;
                y = Mathf.Lerp(bucketBottomY, bucketTopY, k * k * (3f - 2f * k));
                crankSpeed = 220f;
            }
            var p = wellBucket.localPosition;
            wellBucket.localPosition = new Vector3(p.x, y, p.z);
            wellBucket.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2.3f) * 4f, 0f, Mathf.Sin(Time.time * 1.7f) * 5f);
            if (wellRope != null)
            {
                var s = wellRope.localScale;
                wellRope.localScale = new Vector3(s.x, Mathf.Max(0.01f, pulleyY - y - 0.3f), s.z);
            }
            if (wellCrank != null && crankSpeed != 0f) wellCrank.Rotate(crankSpeed * Time.deltaTime, 0f, 0f, Space.Self);
        }

        // 鬼火: ゆっくり上下・左右に漂い、明るさが脈打つように大きさが変わる
        void UpdateWisps()
        {
            float time = Time.time;
            for (int i = 0; i < wisps.Length; i++)
            {
                float ph = wispPhase[i];
                wisps[i].localPosition = wispHome[i] + new Vector3(Mathf.Sin(time * 0.5f + ph) * 0.8f, Mathf.Sin(time * 0.9f + ph * 2f) * 0.35f, Mathf.Cos(time * 0.4f + ph) * 0.8f);
                wisps[i].localScale = Vector3.one * (0.8f + 0.25f * Mathf.Sin(time * 3f + ph));
            }
        }

        // 幽霊の世界にいる (土の地面・木・井戸・お墓・屋敷・月を出して、お祭りの地面を隠す) / いない
        void SetInWorld(bool on)
        {
            if (on == inWorld) return;
            inWorld = on;
            for (int i = 0; i < groundParts.Length; i++)
            {
                if (on) groundWasActive[i] = groundParts[i].gameObject.activeSelf;
                groundParts[i].gameObject.SetActive(!on && groundWasActive[i]);
            }
            SetStageVisible(on);
        }

        void SetStageVisible(bool visible)
        {
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
