using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 幽霊モードの舞台。演出の流れ (視界はふさがない):
    ///   1. 屋台が沈み、ハッチも消えて平らな床になる (GameManager)。
    ///   2. ひゅうぅ…という音とともに、足元から外へ向かって床がでこぼこの濃い茶色の土に変わっていき、空が紫に変わる (GameManager)。
    ///   3. 朽ちたぎざぎざの木・お墓・バケツを上げ下ろしする古井戸・お化け屋敷 (上へいくほど太くなるいびつで豪華な建物) が、
    ///      下の部品から順にぽんぽんと組み上がっていく。最後に月と雲が出て、鬼火が灯る。
    ///   4. まわりをお化けがぐるぐる回り、ほうきに乗った魔女が飛び、お墓の前からゾンビや骸骨がこんにちはする (どれも点数の見えるパネル)。
    ///   5. 終わりは逆に、上の部品から順に崩れて消え、土が足元へ縮んで平らな床に戻る。そのあと GameManager が屋台とハッチを戻す。
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

        [Header("空 (月に近いほど明るく、離れるほど暗い紫になるドーム。幽霊の世界の間だけ、すっと現れる)")]
        public Renderer skyDome;
        public float skyFadeTime = 2.5f;

        [Header("魔女の鍋 (ぶくぶく泡が上がり、下で火が燃える)")]
        [Tooltip("鍋の中から上がって消える泡")]
        public Transform[] cauldronBubbles;
        [Tooltip("泡が出てくる高さ (鍋の中の液の面。泡の親のローカル)")]
        public float cauldronSurfaceY = 1f;
        [Tooltip("鍋の下で燃える火 (大きさがゆらゆら揺れる)")]
        public Transform[] cauldronFlames;

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

        [Header("土に変わる床と、組み上がる舞台")]
        [Tooltip("足元から外へ広がる土の円盤 (ローカル X・Z の大きさを広げる)")]
        public Transform soil;
        [Tooltip("土が広がりきるまでの秒数 (終わりは組み上げを逆にたどるので、同じ秒数で縮む)")]
        public float soilInTime = 1.8f;
        [Tooltip("組み上げる物のまとまり (土のでこぼこ・木・お墓・井戸・お化け屋敷・空・鬼火)。中の見える部品が1つずつぽんと出てくる")]
        public Transform[] assembleRoots;
        [Tooltip("まとまりごとの、組み上げ始める時刻 (秒。土が広がり始めてから)")]
        public float[] assembleDelays;
        [Tooltip("まとまりごとの、いちばん下の部品からいちばん上の部品までにかける秒数")]
        public float[] assembleSpreads;
        [Tooltip("まとまりごとに、高さの順ではなく、真ん中からの距離の順に出す (土のでこぼこ用)")]
        public bool[] assembleRadial;
        [Tooltip("1つの部品がぽんと出る秒数 (終わりはこの逆で、ふくらんでからしぼんで消える)")]
        public float popTime = 0.35f;

        [Header("Timing")]
        public float duration = 26f;
        [Tooltip("始まってから床が土に変わり始めるまでの秒数 (屋台が沈み、平らな床になるのを待つ)")]
        public float dropDelay = 1.2f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回倒した数</summary>
        public int Hits { get; private set; }
        /// <summary>片付け (組み上げを逆にたどってしまわれ、土が縮んで平らな床に戻る) にかかる秒数 = 組み上げと同じ</summary>
        public float CloseTime => BuildTime + 0.1f;
        /// <summary>組み上げにかかる秒数 (土が広がり始めてから、最後の部品が出るまで)</summary>
        public float BuildTime { get; private set; }
        /// <summary>
        /// 床が土に変わり始めた時 (GameManager が空を紫に変えていく) / 崩れ始めた時 (いつもの夜空へ戻していく) /
        /// 土が縮みきって平らな床に戻った時 (GameManager が屋台とハッチを戻す)
        /// </summary>
        public event System.Action Arrived, Left, Closed;
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
            soilScale = soil != null ? soil.localScale : Vector3.one;
            if (skyDome != null) skyMat = skyDome.material; // (実行中だけの複製。透明度を変えても元の材質は変えない)
            if (cauldronBubbles == null) cauldronBubbles = new Transform[0];
            if (cauldronFlames == null) cauldronFlames = new Transform[0];
            bubbleHome = new Vector3[cauldronBubbles.Length];
            bubblePhase = new float[cauldronBubbles.Length];
            for (int i = 0; i < cauldronBubbles.Length; i++)
            {
                bubbleHome[i] = cauldronBubbles[i].localPosition;
                bubblePhase[i] = Random.value;
            }
            CollectParts();
            SetStageVisible(false);
            SetSky(0f);
        }

        // ---- 空のドーム ----
        Material skyMat;
        float skyLevel, skyTarget;

        void SetSky(float a)
        {
            skyLevel = a;
            if (skyMat == null) return;
            skyMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, a));
            if (skyDome != null) skyDome.enabled = a > 0.001f;
        }

        // ---- 魔女の鍋 ----
        Vector3[] bubbleHome;
        float[] bubblePhase;

        // 泡が液の面からぷくっと出て、ふくらみながら上がって消える (泡ごとにずらして)。火はゆらゆら伸び縮みする
        void UpdateCauldron()
        {
            float time = Time.time;
            for (int i = 0; i < cauldronBubbles.Length; i++)
            {
                float k = Mathf.Repeat(time * 0.6f + bubblePhase[i], 1f);
                var home = bubbleHome[i];
                cauldronBubbles[i].localPosition = new Vector3(home.x + Mathf.Sin(time * 3f + i) * 0.03f, cauldronSurfaceY + k * 0.55f, home.z);
                cauldronBubbles[i].localScale = Vector3.one * (0.06f + 0.12f * Mathf.Sin(k * Mathf.PI));
            }
            for (int i = 0; i < cauldronFlames.Length; i++)
            {
                float n = Mathf.PerlinNoise(time * 6f, i * 2.7f);
                cauldronFlames[i].localScale = new Vector3(0.9f + 0.2f * Mathf.PerlinNoise(time * 5f, i + 9f), 0.75f + 0.5f * n, 0.9f + 0.2f * Mathf.PerlinNoise(time * 5f + 4f, i));
            }
        }

        // ---- 組み上げる部品 ----

        struct Part
        {
            public Transform t;
            public Vector3 scale;
            public float at; // 出始める時刻 (土が広がり始めてから)
        }
        Part[] parts = new Part[0];
        Vector3 soilScale;

        // 組み上げるまとまりの中の、見える部品 (Renderer) を全部集め、出る時刻を決める: 下 (または真ん中) の部品ほど早い
        void CollectParts()
        {
            var list = new List<Part>();
            BuildTime = 0f;
            for (int g = 0; g < assembleRoots.Length; g++)
            {
                var root = assembleRoots[g];
                if (root == null) continue;
                float delay = assembleDelays != null && g < assembleDelays.Length ? assembleDelays[g] : 0f;
                float spread = assembleSpreads != null && g < assembleSpreads.Length ? assembleSpreads[g] : 1f;
                bool radial = assembleRadial != null && g < assembleRadial.Length && assembleRadial[g];
                var rends = root.GetComponentsInChildren<Renderer>(true);
                float min = float.MaxValue, max = float.MinValue;
                foreach (var r in rends) { float v = Key(r.transform, radial); min = Mathf.Min(min, v); max = Mathf.Max(max, v); }
                foreach (var r in rends)
                {
                    float k = max > min ? (Key(r.transform, radial) - min) / (max - min) : 0f;
                    float at = delay + k * spread + Random.Range(0f, 0.12f);
                    list.Add(new Part { t = r.transform, scale = r.transform.localScale, at = at });
                    BuildTime = Mathf.Max(BuildTime, at + popTime);
                }
            }
            parts = list.ToArray();
        }

        // 出る順番を決める値: 高さ (舞台の中での y)、または真ん中からの水平の距離
        float Key(Transform t, bool radial)
        {
            var p = transform.InverseTransformPoint(t.position);
            return radial ? new Vector2(p.x, p.z).magnitude : p.y;
        }

        // 組み上げの進み具合 (土が広がり始めてから time 秒) に合わせて、部品の大きさを決める。出る時は少し大きくなってから戻る
        void SetAssembly(float time)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.t == null) continue;
                float k = Mathf.Clamp01((time - p.at) / popTime);
                float s = k <= 0f ? 0.0001f : k >= 1f ? 1f : PopCurve(k);
                p.t.localScale = p.scale * s;
            }
        }

        static float PopCurve(float k)
        {
            const float c = 1.9f; // 行き過ぎる強さ
            float x = k - 1f;
            return Mathf.Max(0.0001f, 1f + (c + 1f) * x * x * x + c * x * x);
        }

        void SetSoil(float k)
        {
            if (soil == null) return;
            float e = Mathf.Max(0.002f, k * k * (3f - 2f * k));
            soil.localScale = new Vector3(soilScale.x * e, soilScale.y, soilScale.z * e);
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
            launchStart = Time.time + dropDelay + BuildTime * 0.85f; // ほとんど組み上がった頃から的を出す
            nextGhost = launchStart;
            nextRise = launchStart + 0.6f;
            nextWitch = launchStart + 2f;
            Play(Enter());
        }

        /// <summary>時間切れ: 崩れて消え、平らな床に戻る。immediate なら、その場ですぐ全部消す (ラウンドの終わり・リセット)</summary>
        public void End(bool immediate = false)
        {
            if (!IsActive) return;
            IsActive = false;
            foreach (var s in AllSpooks()) s.Retreat();
            if (!immediate)
            {
                Play(Leave());
                return;
            }
            if (anim != null) StopCoroutine(anim);
            anim = null;
            SetAssembly(0f);
            SetSoil(0f);
            SetInWorld(false);
            Left?.Invoke();
            Closed?.Invoke();
        }

        IEnumerable<GhostSpook> AllSpooks()
        {
            foreach (var s in ghosts) yield return s;
            foreach (var s in witches) yield return s;
            foreach (var s in risers) yield return s;
        }

        // 始まり: 平らな床になったら、足元から土が広がり、空が紫に変わり、木・お墓・井戸・お化け屋敷が下から組み上がっていく
        IEnumerator Enter()
        {
            yield return new WaitForSeconds(dropDelay);
            SetAssembly(0f);
            SetSoil(0f);
            SetStageVisible(true);
            inWorld = true;
            skyTarget = 1f; // 空がすっと現れる (Update)
            Arrived?.Invoke();
            Sfx.Play("ghost");
            bool groundHidden = false, rumble = false;
            for (float t = 0f; t < BuildTime; t += Time.deltaTime)
            {
                SetSoil(Mathf.Clamp01(t / soilInTime));
                SetAssembly(t);
                if (!groundHidden && t >= soilInTime) { groundHidden = true; HideGround(true); } // 土で覆いきった: お祭りの地面を隠す
                if (!rumble && t >= soilInTime * 0.6f) { rumble = true; Sfx.Play("door"); } // ゴゴゴ… と組み上がる
                yield return null;
            }
            SetSoil(1f);
            SetAssembly(BuildTime + 1f);
            if (!groundHidden) HideGround(true);
            anim = null;
        }

        // 終わり: 組み上げ (Enter) をそのまま逆にたどってしまわれていく (ユーザーの指定)。同じ速さで、時刻を BuildTime から 0 へ戻す:
        // 鬼火 → 月と雲 → お化け屋敷 (上の部品から下へ)・木・お墓・井戸 → 土のでこぼこ (外から真ん中へ) → 土の円盤が足元へ縮む。
        // 空は最後に消えきるように、終わりの skyFadeTime 秒前から消し始める
        IEnumerator Leave()
        {
            Left?.Invoke();
            Sfx.Play("ghost");
            float total = Mathf.Max(0.1f, BuildTime);
            bool groundShown = false, skyFading = false;
            for (float t = total; t > 0f; t -= Time.deltaTime)
            {
                SetAssembly(t);
                SetSoil(Mathf.Clamp01(t / soilInTime));
                if (!groundShown && t < soilInTime) { groundShown = true; HideGround(false); } // 土の下に、お祭りの地面を戻しておく (組み上げの時に隠した時刻)
                if (!skyFading && t <= skyFadeTime) { skyFading = true; skyTarget = 0f; } // 空はすっと消えていく (Update)
                yield return null;
            }
            SetAssembly(0f);
            SetSoil(0f);
            if (!groundShown) HideGround(false);
            SetInWorld(false);
            Closed?.Invoke();
            anim = null;
        }

        // お祭りの地面 (地面・穴のふち・足元の目印) を隠す / 戻す
        void HideGround(bool hide)
        {
            for (int i = 0; i < groundParts.Length; i++)
            {
                if (hide) groundWasActive[i] = groundParts[i].gameObject.activeSelf;
                groundParts[i].gameObject.SetActive(!hide && groundWasActive[i]);
            }
            groundHiddenNow = hide;
        }
        bool groundHiddenNow;

        void Update()
        {
            if (inWorld)
            {
                UpdateWell();
                UpdateWisps();
                UpdateCauldron();
            }
            if (!Mathf.Approximately(skyLevel, skyTarget))
                SetSky(Mathf.MoveTowards(skyLevel, skyTarget, Time.deltaTime / Mathf.Max(0.1f, skyFadeTime)));
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
            inWorld = on;
            if (!on) { skyTarget = 0f; SetSky(0f); }
            if (!on && groundHiddenNow) HideGround(false);
            SetStageVisible(on);
            if (!on) SetSoil(1f); // 次に出す時のために、元の大きさへ (出す時に 0 から広げる)
            if (!on) SetAssembly(BuildTime + 1f);
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
