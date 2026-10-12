using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 宇宙モードの舞台。演出の流れ:
    ///   1. 屋台が沈んだあと、まわりが暗くなり (Darken)、床 (地面・穴のふち・足元の目印・ボーナスステージ) がだんだん透けていき、見えなくなる。
    ///      プレイヤーは真っ暗な空間に浮かんでいる。
    ///   2. 的ではない星々が、あちこちで1つ、また1つときらめき始め、やがて天の川・星雲・十字に光る明るい星・流れ星で宇宙いっぱいになる。
    ///   3. 的が出てくる: 点数の書いた立体の星のパネルが流星群のように光の尾を引いて流れ、惑星がふわっと現れて漂い、隕石が横切り、
    ///      ロケットが煙を吐いて飛んでいく。武器はまっすぐ飛ぶミサイル。
    ///   4. 時間切れで的は消え、星々は輝きを失って暗くなり (Restore)、床がだんだん見えてくる。そのあと GameManager が屋台を戻す。
    /// 屋台の出し入れ・点数・明るさの判断は GameManager が行い、ここは見た目の順番と的の出し方だけを受け持つ。プレイヤーは動かない。
    /// </summary>
    public class SpaceStage : MonoBehaviour
    {
        [Header("透けて消える床")]
        [Tooltip("宇宙モードの間は透けさせて消しておく部品 (地面・穴のふち・足元の目印・ボーナスステージ)")]
        public Transform[] groundParts;
        [Tooltip("床の材質 (fadeSourceMaterials) と、その半透明版 (fadeMaterials。同じ順)。透ける間だけ差し替える")]
        public Material[] fadeSourceMaterials;
        public Material[] fadeMaterials;
        [Tooltip("床が透けて消えていく秒数 (戻る時も同じ)。早めに消して、すぐにまわり中が宇宙になるように")]
        public float groundFadeTime = 1.2f; // 名前を変えて、シーンに保存された前の値 (3.2秒) を使わないようにしている
        [Tooltip("宇宙モードの間だけ表示する物 (星空)")]
        public GameObject[] showDuringStage;

        [Header("的ではない星々")]
        [Tooltip("点々の星の固まり (固まりごとに明るさがゆらいで、またたいて見える)")]
        public Renderer[] starClusters;
        [Tooltip("十字に光る明るい星 (1つずつ大きさが脈打つ)")]
        public Transform[] sparkles;
        [Tooltip("うっすら光る星雲 (半透明)")]
        public Renderer[] nebulae;
        [Tooltip("的ではない流れ星 (光の尾 TrailRenderer 付き)")]
        public TrailRenderer[] shootingStars;
        [Tooltip("星がきらめき始めてから全部そろうまで / 輝きを失って消えるまでの秒数")]
        public float starFadeIn = 4.5f;
        public float starFadeOut = 2.6f;

        [Header("的 (時間切れまで、種類ごとに決まった間隔で出てくる)")]
        public SpaceObject[] starPanels;
        public SpaceObject[] planets;
        public SpaceObject[] meteors;
        public SpaceObject[] rockets;
        [Tooltip("流星群 (星のパネルが何枚か並んで流れる) の間隔 (秒)")]
        public Vector2 showerInterval = new Vector2(3.2f, 4.5f);
        public Vector2 meteorInterval = new Vector2(1.4f, 2.4f);
        public Vector2 planetInterval = new Vector2(3.5f, 5.5f);
        public Vector2 rocketInterval = new Vector2(5f, 8f);
        public int maxPlanets = 2;
        [Tooltip("流れる的の速さ (m/秒)。ミサイルで狙いやすいように、ゆっくりめ")]
        public float panelSpeed = 3.4f;
        public float meteorSpeed = 4f;
        public float rocketSpeed = 3f;
        public float stopLaunchBefore = 2.5f;

        [Header("Timing")]
        [Tooltip("始まってから終わるまでの秒数 (屋台が沈む・床が透ける・星がきらめき始める時間も含む)")]
        public float duration = 26f;
        [Tooltip("始まってから暗くなり始めるまでの秒数 (屋台が沈み終わるのを待つ)")]
        public float dropDelay = 1.2f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回壊した的の数</summary>
        public int Hits { get; private set; }
        /// <summary>片付け (星が消えて床が戻る) にかかる秒数</summary>
        public float CloseTime => starFadeOut + groundFadeTime + 0.1f;

        /// <summary>暗くなる時 (GameManager が宇宙の暗さにする)</summary>
        public event System.Action Darken;
        /// <summary>星が消えて床が戻り始める時 (GameManager がいつもの夜の明るさへ戻す)</summary>
        public event System.Action Restore;
        /// <summary>的を壊した (点数はもう入っている。GameManager が大きな的の時に演出を出す)</summary>
        public event System.Action<SpaceObject> ObjectDestroyed;

        float startTime, launchStart;
        float nextShower, nextMeteor, nextPlanet, nextRocket;
        float lastAngle;
        Coroutine anim;
        bool groundFading;
        readonly List<(Renderer r, Material[] originals, bool enabled)> swapped = new List<(Renderer, Material[], bool)>();
        readonly Dictionary<Material, Material> fadeInstances = new Dictionary<Material, Material>();
        readonly Dictionary<Material, Color> fadeBaseColor = new Dictionary<Material, Color>();
        readonly Dictionary<Material, Color> fadeBaseEmission = new Dictionary<Material, Color>();
        bool[] groundWasActive;
        float groundK;
        Coroutine groundRoutine;
        Material[] clusterMats, nebulaMats;

        // 星々の明るさ (0 = 真っ暗 → 1 = 全部きらめいている)
        float starLevel;
        Color[] clusterColor, nebulaColor;
        float[] clusterOffset, clusterRate, clusterPhase;
        Vector3[] sparkleScale;
        float[] sparkleOffset, sparkleRate, sparklePhase;
        // 流れ星
        Vector3[] shootFrom, shootTo;
        float[] shootT, shootLen;
        float nextShootingStar;

        void Awake()
        {
            groundWasActive = new bool[groundParts.Length];

            // 星の材質は実行中だけの複製にして、明るさを変えても元の材質 (アセット) は変えない
            clusterMats = new Material[starClusters.Length];
            clusterColor = new Color[starClusters.Length];
            clusterOffset = new float[starClusters.Length];
            clusterRate = new float[starClusters.Length];
            clusterPhase = new float[starClusters.Length];
            for (int i = 0; i < starClusters.Length; i++)
            {
                var m = clusterMats[i] = starClusters[i].material;
                clusterColor[i] = m.GetColor("_BaseColor");
                clusterOffset[i] = Random.value;
                clusterRate[i] = Random.Range(1.5f, 4.5f);
                clusterPhase[i] = Random.value * 10f;
            }
            nebulaColor = new Color[nebulae.Length];
            nebulaMats = new Material[nebulae.Length];
            for (int i = 0; i < nebulae.Length; i++) nebulaColor[i] = (nebulaMats[i] = nebulae[i].material).GetColor("_BaseColor");
            sparkleScale = new Vector3[sparkles.Length];
            sparkleOffset = new float[sparkles.Length];
            sparkleRate = new float[sparkles.Length];
            sparklePhase = new float[sparkles.Length];
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkleScale[i] = sparkles[i].localScale;
                sparkleOffset[i] = Random.value;
                sparkleRate[i] = Random.Range(1.2f, 3.5f);
                sparklePhase[i] = Random.value * 10f;
            }
            shootFrom = new Vector3[shootingStars.Length];
            shootTo = new Vector3[shootingStars.Length];
            shootT = new float[shootingStars.Length];
            shootLen = new float[shootingStars.Length];
            for (int i = 0; i < shootingStars.Length; i++) shootT[i] = 1f;

            SetStars(0f);
            SetStageVisible(false);
        }

        /// <summary>宇宙モードを始める</summary>
        public void Begin()
        {
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            Hits = 0;
            lastAngle = Random.Range(0f, 360f);
            launchStart = Time.time + dropDelay + 0.8f + starFadeIn * 0.7f; // 星がだいたいそろった頃から的を出す
            nextShower = launchStart;
            nextMeteor = launchStart + 1.2f;
            nextPlanet = launchStart + 0.6f;
            nextRocket = launchStart + Random.Range(rocketInterval.x, rocketInterval.y) * 0.6f;
            for (int i = 0; i < groundParts.Length; i++) groundWasActive[i] = groundParts[i].gameObject.activeSelf;
            Play(Intro());
        }

        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            foreach (var group in AllObjects()) foreach (var o in group) o.Retreat();
            Play(Outro());
        }

        IEnumerable<SpaceObject[]> AllObjects()
        {
            yield return starPanels;
            yield return planets;
            yield return meteors;
            yield return rockets;
        }

        IEnumerator Intro()
        {
            yield return new WaitForSeconds(dropDelay);
            // 1. 暗くなり、床がだんだん透けて消えていく
            Darken?.Invoke();
            Sfx.Play("warpin");
            SetStageVisible(true);
            groundRoutine = StartCoroutine(FadeGround(1f));
            yield return new WaitForSeconds(0.8f);
            // 2. 星々が1つ、また1つときらめき始める
            Sfx.Play("twinkle");
            bool second = false;
            for (float t = 0f; t < starFadeIn; t += Time.deltaTime)
            {
                SetStars(Mathf.SmoothStep(0f, 1f, t / starFadeIn));
                if (!second && t > starFadeIn * 0.5f) { second = true; Sfx.Play("twinkle"); }
                yield return null;
            }
            SetStars(1f);
            anim = null;
        }

        IEnumerator Outro()
        {
            // 星々が輝きを失って暗くなる → 明るさを戻し、透けていた床がだんだん見えてくる
            float from = starLevel;
            for (float t = 0f; t < starFadeOut; t += Time.deltaTime)
            {
                SetStars(Mathf.Lerp(from, 0f, Mathf.SmoothStep(0f, 1f, t / starFadeOut)));
                yield return null;
            }
            SetStars(0f);
            Restore?.Invoke();
            Sfx.Play("warpout");
            for (int i = 0; i < groundParts.Length; i++)
                if (groundWasActive[i]) groundParts[i].gameObject.SetActive(true);
            if (groundRoutine != null) StopCoroutine(groundRoutine);
            groundRoutine = null;
            yield return FadeGround(0f);
            SetStageVisible(false);
            anim = null;
        }

        // 床を透けさせて消す (to = 1) / 戻す (to = 0)
        IEnumerator FadeGround(float to)
        {
            float from = groundK;
            for (float t = 0f; t < 1f; t += Time.deltaTime / groundFadeTime)
            {
                float e = t * t * (3f - 2f * t); // なめらかに (消え始めからすぐ透けていく)
                SetGround(Mathf.Lerp(from, to, e));
                yield return null;
            }
            SetGround(to);
            if (to > 0f) foreach (var g in groundParts) g.gameObject.SetActive(false); // 見えなくなった床は隠す
        }

        // k = 0 (いつもの床) 〜 1 (完全に透明)。透けている間だけ、床の材質を半透明版 (fadeMaterials) の複製に差し替える
        void SetGround(float k)
        {
            groundK = k;
            if (k <= 0f) { RestoreGroundMaterials(); return; }
            if (!groundFading) SwapGroundMaterials();
            float a = 1f - k;
            foreach (var pair in fadeInstances)
            {
                // 透明にするだけだと、半透明の床に空や光の色 (濃い紫など) が映って見えることがあるので、
                // 色も宇宙と同じ黒へ近づけながら透けさせる (光る線も同じように消す)
                var m = pair.Value;
                var c = fadeBaseColor[m];
                m.SetColor("_BaseColor", new Color(c.r * a, c.g * a, c.b * a, c.a * a));
                if (fadeBaseEmission.TryGetValue(m, out var e)) m.SetColor("_EmissionColor", e * a);
            }
        }

        void SwapGroundMaterials()
        {
            groundFading = true;
            swapped.Clear();
            foreach (var g in groundParts)
                foreach (var r in g.GetComponentsInChildren<Renderer>(true))
                {
                    var originals = r.sharedMaterials;
                    var faded = new Material[originals.Length];
                    bool all = r.GetComponent<TextMesh>() == null;
                    for (int i = 0; i < originals.Length && all; i++)
                    {
                        faded[i] = FadeInstanceOf(originals[i]);
                        if (faded[i] == null) all = false;
                    }
                    swapped.Add((r, originals, r.enabled));
                    if (all) r.sharedMaterials = faded;
                    else r.enabled = false; // 半透明版のない物 (文字など) は、透け始めたら隠す
                }
        }

        void RestoreGroundMaterials()
        {
            if (!groundFading) return;
            groundFading = false;
            foreach (var (r, originals, enabled) in swapped)
            {
                if (r == null) continue;
                r.sharedMaterials = originals;
                r.enabled = enabled;
            }
            swapped.Clear();
        }

        // 元の材質に対応する半透明版の、実行中だけの複製 (同じ材質の部品は同じ複製を使い、透明度を1回で変えられるようにする)
        Material FadeInstanceOf(Material original)
        {
            if (original == null || fadeSourceMaterials == null) return null;
            if (fadeInstances.TryGetValue(original, out var inst)) return inst;
            int i = System.Array.IndexOf(fadeSourceMaterials, original);
            if (i < 0 || i >= fadeMaterials.Length || fadeMaterials[i] == null) return null;
            inst = new Material(fadeMaterials[i]);
            // 透けている床に、つやの照り返し (まわりの空の色) が残って色づいて見えないよう、つやと映り込みを切る
            if (inst.HasProperty("_Smoothness")) inst.SetFloat("_Smoothness", 0f);
            if (inst.HasProperty("_SpecularHighlights")) { inst.SetFloat("_SpecularHighlights", 0f); inst.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); }
            if (inst.HasProperty("_EnvironmentReflections")) { inst.SetFloat("_EnvironmentReflections", 0f); inst.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
            fadeInstances[original] = inst;
            fadeBaseColor[inst] = inst.GetColor("_BaseColor");
            if (inst.IsKeywordEnabled("_EMISSION") && inst.HasProperty("_EmissionColor")) fadeBaseEmission[inst] = inst.GetColor("_EmissionColor");
            return inst;
        }

        void SetStars(float level) => starLevel = level;

        // 星ごとの、今の明るさ (0〜1)。offset が大きい星ほど遅く点き、早く消える
        static float Reveal(float level, float offset) => Mathf.Clamp01((level - offset * 0.65f) / 0.35f);

        void Update()
        {
            UpdateStars();
            if (!IsActive || Time.time < launchStart || TimeLeft < stopLaunchBefore) return;
            float now = Time.time;
            if (now >= nextShower) { nextShower = now + Random.Range(showerInterval.x, showerInterval.y); StartCoroutine(MeteorShower()); }
            if (now >= nextMeteor) { nextMeteor = now + Random.Range(meteorInterval.x, meteorInterval.y); LaunchCrossing(meteors, meteorSpeed, 8f, 17f, -1.5f, 5f); }
            if (now >= nextRocket) { nextRocket = now + Random.Range(rocketInterval.x, rocketInterval.y); LaunchCrossing(rockets, rocketSpeed, 9f, 15f, 0f, 5f, 0.25f); }
            if (now >= nextPlanet)
            {
                nextPlanet = now + Random.Range(planetInterval.x, planetInterval.y);
                int out_ = 0;
                foreach (var p in planets) if (p.IsBusy) out_++;
                if (out_ < maxPlanets) LaunchPlanet();
            }
        }

        // 星のまたたき: 固まりごとに明るさがゆらぎ、十字の星は大きさが脈打つ。点き始めた瞬間はパッと大きく光る
        void UpdateStars()
        {
            if (starLevel <= 0f && !IsActive) return;
            float time = Time.time;
            for (int i = 0; i < starClusters.Length; i++)
            {
                float b = Reveal(starLevel, clusterOffset[i]);
                float twinkle = 0.72f + 0.28f * Mathf.Sin(time * clusterRate[i] + clusterPhase[i]);
                clusterMats[i].SetColor("_BaseColor", clusterColor[i] * (b * twinkle));
            }
            for (int i = 0; i < nebulae.Length; i++)
            {
                var c = nebulaColor[i];
                c.a *= Mathf.Clamp01(starLevel * 1.4f - 0.4f);
                nebulaMats[i].SetColor("_BaseColor", c);
            }
            for (int i = 0; i < sparkles.Length; i++)
            {
                float b = Reveal(starLevel, sparkleOffset[i]);
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * sparkleRate[i] + sparklePhase[i]);
                float pop = IsActive ? 1f + 1.6f * Mathf.Sin(Mathf.PI * b) : 1f; // 点く時だけパッと大きく
                float s = b * pop * (0.55f + 0.45f * pulse * pulse * pulse);
                sparkles[i].localScale = sparkleScale[i] * Mathf.Max(0.001f, s);
                bool show = s > 0.01f;
                if (sparkles[i].gameObject.activeSelf != show) sparkles[i].gameObject.SetActive(show);
            }
            UpdateShootingStars();
        }

        // 遠くの空を、ときどき流れ星がすっと流れる (的ではない)
        void UpdateShootingStars()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < shootingStars.Length; i++)
            {
                if (shootT[i] >= 1f) continue;
                shootT[i] += dt * 32f / Mathf.Max(1f, shootLen[i]);
                shootingStars[i].transform.localPosition = Vector3.Lerp(shootFrom[i], shootTo[i], shootT[i]);
                if (shootT[i] >= 1f) shootingStars[i].emitting = false;
            }
            if (starLevel < 0.8f || Time.time < nextShootingStar) return;
            nextShootingStar = Time.time + Random.Range(0.9f, 2.2f);
            for (int i = 0; i < shootingStars.Length; i++)
            {
                if (shootT[i] < 1f) continue;
                var start = Quaternion.Euler(Random.Range(-35f, -5f), Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(55f, 75f);
                var dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * new Vector3(1f, -0.45f, 0f);
                shootFrom[i] = start;
                shootTo[i] = start + dir.normalized * Random.Range(18f, 30f);
                shootLen[i] = Vector3.Distance(shootFrom[i], shootTo[i]);
                shootT[i] = 0f;
                shootingStars[i].emitting = false;
                shootingStars[i].transform.localPosition = start;
                shootingStars[i].Clear();
                shootingStars[i].emitting = true;
                break;
            }
        }

        // 流星群: 同じ向きに、星のパネルが何枚か少しずつずれて流れていく
        IEnumerator MeteorShower()
        {
            var free = Free(starPanels);
            if (free.Count == 0) yield break;
            int count = Mathf.Min(free.Count, Random.Range(3, 6));
            NextAngle();
            float a = lastAngle * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var side = Vector3.Cross(Vector3.up, outward) * (Random.value < 0.5f ? 1f : -1f);
            var dir = (side + Vector3.down * Random.Range(0.2f, 0.55f)).normalized; // 斜め下へ流れる
            for (int i = 0; i < count; i++)
            {
                var o = free[i];
                float d = Random.Range(8.5f, 15f);
                var center = outward * d + Vector3.up * Random.Range(1f, 5.5f) + side * Random.Range(-3f, 3f);
                const float Half = 15f;
                o.Launch(center - dir * Half, center + dir * Half, panelSpeed * Random.Range(0.85f, 1.15f), OnDestroyed);
                yield return new WaitForSeconds(Random.Range(0.25f, 0.5f));
            }
        }

        // まわりを横切る (隕石・ロケット)。rise = 上向きに飛ぶ割合
        void LaunchCrossing(SpaceObject[] pool, float speed, float near, float far, float lowY, float highY, float rise = -0.15f)
        {
            var free = Free(pool);
            if (free.Count == 0) return;
            NextAngle();
            float a = lastAngle * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var side = Vector3.Cross(Vector3.up, outward) * (Random.value < 0.5f ? 1f : -1f);
            var dir = (side + Vector3.up * rise * Random.Range(0.5f, 1.5f)).normalized;
            var center = outward * Random.Range(near, far) + Vector3.up * Random.Range(lowY, highY);
            const float Half = 16f;
            free[Random.Range(0, free.Count)].Launch(center - dir * Half, center + dir * Half, speed * Random.Range(0.85f, 1.2f), OnDestroyed);
        }

        // 惑星: 遠めの所にふわっと現れて、ゆっくり漂いながら回り、しばらくすると小さくなって消える
        void LaunchPlanet()
        {
            var free = Free(planets);
            if (free.Count == 0) return;
            NextAngle();
            float a = lastAngle * Mathf.Deg2Rad;
            var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var center = outward * Random.Range(13f, 20f) + Vector3.up * Random.Range(1.5f, 7f);
            var drift = Vector3.Cross(Vector3.up, outward) * Random.Range(-2.5f, 2.5f) + Vector3.up * Random.Range(-0.8f, 0.8f);
            free[Random.Range(0, free.Count)].Launch(center - drift, center + drift, 1f, OnDestroyed, Random.Range(8f, 11f));
        }

        // 前の的と離れた方向から出す (プレイヤーがまわりを見回すように)
        void NextAngle() => lastAngle += Random.Range(70f, 150f) * (Random.value < 0.5f ? -1f : 1f);

        static List<SpaceObject> Free(SpaceObject[] pool)
        {
            var list = new List<SpaceObject>();
            foreach (var o in pool) if (!o.IsBusy) list.Add(o);
            // 選ぶ順をばらばらに
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        void OnDestroyed(SpaceObject o)
        {
            Hits++;
            ObjectDestroyed?.Invoke(o);
        }

        void SetStageVisible(bool visible)
        {
            if (showDuringStage != null)
                foreach (var go in showDuringStage) if (go != null) go.SetActive(visible);
            if (!visible)
                for (int i = 0; i < shootingStars.Length; i++) { shootT[i] = 1f; shootingStars[i].emitting = false; shootingStars[i].Clear(); }
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
