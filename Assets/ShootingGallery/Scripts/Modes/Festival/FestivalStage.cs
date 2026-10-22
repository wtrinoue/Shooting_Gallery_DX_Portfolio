using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 夏祭りモードの舞台。世界 (地面・空) はそのままで、演出の流れは:
    ///   1. いつもの屋台が沈み (GameManager)、プレイヤーの前・右・後ろ・左に、夏祭りの屋台が4軒せり上がる
    ///      (射的・輪投げ・金魚すくい・食べ物。どれも視界いっぱいくらいの大きさと近さ)。
    ///   2. 頭の上に張りめぐらせた提灯が、ぽんぽんと順に灯る。空高く花火が上がり始める。
    ///   3. 見ている屋台によって武器が変わる (WeaponFor。GameManager が毎フレーム聞く): 射的 = コルク銃、輪投げ = 輪、金魚すくい = ポイ、食べ物 = お金。
    ///   4. 時間切れで、提灯が消え、屋台が沈む。そのあと GameManager がいつもの屋台を戻す。
    /// 金魚すくいの結果 (すくえた / 破れた) と、食べ物を買えるか (お金 = スコアが足りるか) と、買った物を机に並べるのもここで受け持つ。
    /// </summary>
    public class FestivalStage : MonoBehaviour
    {
        [Header("屋台 (前・右・後ろ・左の順) と、それぞれを見ている時の武器")]
        public Stall[] stalls;
        public WeaponType[] stallWeapons = { WeaponType.CorkGun, WeaponType.RingToss, WeaponType.Poi, WeaponType.Coin };
        [Tooltip("今の屋台から隣の屋台へ武器が変わるのは、境目からこの角度 (度) だけ隣の屋台の方を見た時 (境目でちらちら変わらないように)")]
        public float switchMargin = 6f;

        [Header("提灯の飾り (始まると順にぽんぽんと灯り、終わると消える)")]
        public Transform decorations;
        [Tooltip("全部の提灯が灯るまでの秒数")]
        public float lightUpTime = 2.4f;
        public float popTime = 0.3f;

        [Header("花火")]
        [Tooltip("火の粉の色 (光る材質)。毎回この中から選ぶ")]
        public Material[] fireworkMaterials;
        [Tooltip("打ち上がる光の玉")]
        public Material fireworkShellMaterial;
        public Vector2 fireworkInterval = new Vector2(0.5f, 1.3f);
        [Tooltip("打ち上げる所までの水平の距離 (m)")]
        public Vector2 fireworkDistance = new Vector2(34f, 52f);
        [Tooltip("開く高さ (m)")]
        public Vector2 fireworkHeight = new Vector2(26f, 40f);
        [Tooltip("開いた大きさ (半径 m)")]
        public Vector2 fireworkRadius = new Vector2(8f, 14f);
        [Tooltip("ときどき、同じ方角から続けざまに何発も上がる (スターマイン) 確率")]
        public float starmineChance = 0.15f;

        [Header("星空 (夏祭りの間だけ、ゆっくり現れる)")]
        [Tooltip("星空 (星の固まり・天の川)。夏祭りの間だけ出す")]
        public GameObject nightSky;
        [Tooltip("またたかせる星の固まり (固まりごとに明るさがゆっくり揺れる)")]
        public Renderer[] starClusters;
        public float starFadeTime = 2.5f;

        [Header("金魚すくい")]
        [Tooltip("水面の当たり判定 (ポイがここに入ったら、すくえたか決める)")]
        public Collider water;
        public FestivalFish[] fish;
        [Tooltip("ポイが入った所から、この距離 (m) までの金魚をすくおうとする (金魚は水面より少し下を泳いでいる)")]
        public float scoopRadius = 0.5f;
        [Tooltip("金魚の点数ごとの、すくえる確率 (大きくて点数の高い金魚ほど、ポイが破れやすい)")]
        public int[] scoopPoints = { 50, 100, 200, 300, 500, 1000 };
        public float[] scoopChance = { 0.85f, 0.75f, 0.6f, 0.5f, 0.4f, 0.3f };
        [Tooltip("水しぶき (半透明の材質)")]
        public Material splashMaterial;
        [Tooltip("すくう時に出すポイ (隠してある見本。すくうたびに複製する。子に Paper と、破れた時の Torn)")]
        public GameObject scoopPoi;
        [Tooltip("すくえた金魚を入れるお椀の中 (水面のあたり)")]
        public Transform scoopBowl;

        [Header("食べ物の屋台 (お金は夏祭りに入ってから稼いだ分。スコアとは別)")]
        [Tooltip("買った物を並べる、屋台の前の机の上の置き場所")]
        public Transform[] tableSlots;
        [Tooltip("ON: 買うとお金 (夏祭りで稼いだ分) が減る。OFF: 値段の分だけスコアが増える")]
        public bool spendMoneyOnFood = true;
        [Tooltip("食べ物の屋台のカウンターに立てた残高のパネルの数字 (今のお金 = スコア)")]
        public TextMesh balanceText;
        public Font font;

        [Header("Timing")]
        public float duration = 40f;
        [Tooltip("始まってから屋台がせり上がり始めるまでの秒数 (いつもの屋台が沈むのを待つ。GameManager が決める)")]
        public float riseDelay = 1.6f;
        [Tooltip("屋台がせり上がる / 沈む秒数 (GameManager が決める)")]
        public float moveTime = 1.2f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>景品を取った・輪がかかった・金魚をすくった・料理を買った数</summary>
        public int Hits { get; private set; }
        /// <summary>片付け (提灯が消え、屋台が沈みきる) にかかる秒数</summary>
        public float CloseTime => Mathf.Max(moveTime, popTime + 0.6f) + 0.2f;

        float startTime, nextFirework;
        bool fireworksOn;
        Coroutine anim;
        Renderer[] lanterns = new Renderer[0];
        Vector3[] lanternScales = new Vector3[0];
        int nextSlot;
        readonly List<GameObject> bought = new List<GameObject>();

        // 星の材質は実行中だけの複製にして、明るさを変えても元の材質 (アセット) は変えない
        Material[] starMats = new Material[0];
        Color[] starColors = new Color[0];
        float[] starPhase = new float[0];
        float starLevel, starTarget;

        void Awake()
        {
            if (starClusters == null) starClusters = new Renderer[0];
            starMats = new Material[starClusters.Length];
            starColors = new Color[starClusters.Length];
            starPhase = new float[starClusters.Length];
            for (int i = 0; i < starClusters.Length; i++)
            {
                if (starClusters[i] == null) continue;
                starMats[i] = starClusters[i].material;
                starColors[i] = starMats[i].GetColor("_BaseColor");
                starPhase[i] = Random.value * 10f;
            }
            if (nightSky != null) nightSky.SetActive(false);
            if (decorations != null)
            {
                lanterns = decorations.GetComponentsInChildren<Renderer>(true);
                lanternScales = new Vector3[lanterns.Length];
                for (int i = 0; i < lanterns.Length; i++) lanternScales[i] = lanterns[i].transform.localScale;
                decorations.gameObject.SetActive(false);
            }
        }

        void Start()
        {
            // 屋台は止まる場所に作ってあるので、ここで地下へ隠す (止まる場所を覚えてから)
            foreach (var s in stalls) if (s != null) s.HideImmediate();
        }

        /// <summary>夏祭りモードを始める。yawDeg = 正面 (射的の屋台) の向き</summary>
        public void Begin(float yawDeg)
        {
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            Hits = 0;
            prizeHits.Clear();
            ClearTable();
            transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            Play(Enter());
        }

        /// <summary>時間切れ: 提灯が消え、屋台が沈む。immediate なら、その場ですぐ全部隠す (ラウンドの終わり・リセット)</summary>
        public void End(bool immediate = false)
        {
            if (!IsActive) return;
            IsActive = false;
            fireworksOn = false;
            if (immediate)
            {
                if (anim != null) StopCoroutine(anim);
                anim = null;
                foreach (var s in stalls) if (s != null) s.HideImmediate();
                SetLanterns(0f, false);
                if (decorations != null) decorations.gameObject.SetActive(false);
                starLevel = starTarget = 0f;
                if (nightSky != null) nightSky.SetActive(false);
                ClearTable();
                return;
            }
            starTarget = 0f; // 星空はゆっくり消える
            Play(Leave());
        }

        IEnumerator Enter()
        {
            // 星空がゆっくり現れる (いつもの屋台が沈んでいる間から)
            if (nightSky != null) nightSky.SetActive(true);
            starTarget = 1f;
            UpdateStars();
            yield return new WaitForSeconds(riseDelay);
            foreach (var s in stalls) if (s != null) s.Rise(moveTime, true);
            Sfx.Play("taiko");
            if (decorations != null) decorations.gameObject.SetActive(true);
            SetLanterns(0f, true);
            nextFirework = Time.time + moveTime;
            fireworksOn = true;
            for (float t = 0f; t < lightUpTime + popTime; t += Time.deltaTime)
            {
                SetLanterns(t, true);
                yield return null;
            }
            SetLanterns(lightUpTime + popTime, true);
            anim = null;
        }

        IEnumerator Leave()
        {
            foreach (var s in stalls) if (s != null) s.Sink(moveTime);
            for (float t = 0f; t < lightUpTime * 0.4f + popTime; t += Time.deltaTime)
            {
                SetLanterns(t, false);
                yield return null;
            }
            SetLanterns(0f, false);
            if (decorations != null) decorations.gameObject.SetActive(false);
            yield return new WaitForSeconds(Mathf.Max(0f, moveTime - lightUpTime * 0.4f));
            ClearTable();
            anim = null;
        }

        // 提灯をぽんと出す (on) / 消す。出す時は飾りの並び順に、消す時はまとめて少し早く
        void SetLanterns(float time, bool on)
        {
            int n = lanterns.Length;
            float span = on ? lightUpTime : lightUpTime * 0.4f;
            for (int i = 0; i < n; i++)
            {
                if (lanterns[i] == null) continue;
                float at = n > 1 ? span * i / (n - 1) : 0f;
                float k = Mathf.Clamp01((time - at) / popTime);
                float s = on ? (k <= 0f ? 0.0001f : k >= 1f ? 1f : PopCurve(k)) : Mathf.Max(0.0001f, 1f - k);
                lanterns[i].transform.localScale = lanternScales[i] * s;
            }
        }

        static float PopCurve(float k)
        {
            const float c = 1.9f;
            float x = k - 1f;
            return Mathf.Max(0.0001f, 1f + (c + 1f) * x * x * x + c * x * x);
        }

        int shownBalance = -1;

        void Update()
        {
            UpdateBalance();
            UpdateStars();
            if (!fireworksOn || Time.time < nextFirework) return;
            nextFirework = Time.time + Random.Range(fireworkInterval.x, fireworkInterval.y);
            if (Random.value < starmineChance)
            {
                // スターマイン: 同じ方角から、少しずつずらして続けざまに何発も
                float a = Random.Range(0f, Mathf.PI * 2f);
                int n = Random.Range(5, 9);
                for (int i = 0; i < n; i++) StartCoroutine(LaunchLater(i * Random.Range(0.12f, 0.22f), a + Random.Range(-0.25f, 0.25f), 0.7f));
                nextFirework += 1.2f;
                return;
            }
            int volley = Random.value < 0.35f ? Random.Range(2, 5) : 1; // ときどき何発か同時に
            for (int i = 0; i < volley; i++) LaunchFirework(Random.Range(0f, Mathf.PI * 2f), 1f);
        }

        IEnumerator LaunchLater(float delay, float angle, float size)
        {
            yield return new WaitForSeconds(delay);
            if (fireworksOn) LaunchFirework(angle, size);
        }

        // 星空: ゆっくり現れて / 消えて、固まりごとに明るさがゆっくり揺れる (またたき)
        void UpdateStars()
        {
            if (nightSky == null) return;
            starLevel = Mathf.MoveTowards(starLevel, starTarget, Time.deltaTime / Mathf.Max(0.1f, starFadeTime));
            if (starLevel <= 0f && starTarget <= 0f)
            {
                if (nightSky.activeSelf) nightSky.SetActive(false);
                return;
            }
            float level = Mathf.SmoothStep(0f, 1f, starLevel);
            for (int i = 0; i < starMats.Length; i++)
            {
                if (starMats[i] == null) continue;
                float tw = 0.75f + 0.25f * Mathf.Sin(Time.time * (0.8f + i * 0.13f) + starPhase[i]);
                starMats[i].SetColor("_BaseColor", starColors[i] * (level * tw));
            }
        }

        // 残高のパネル: お金 (夏祭りで稼いだ分) が変わった時だけ書き換える
        void UpdateBalance()
        {
            if (balanceText == null || GameManager.I == null) return;
            int money = GameManager.I.FestivalMoney;
            if (money == shownBalance) return;
            shownBalance = money;
            balanceText.text = "¥" + money.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        // angle の方角に1発打ち上げる。size = 開く大きさの倍率 (スターマインは小さめ)
        void LaunchFirework(float angle, float size)
        {
            if (fireworkMaterials == null || fireworkMaterials.Length == 0) return;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 from = transform.position + dir * Random.Range(fireworkDistance.x, fireworkDistance.y);
            Vector3 burst = from + Vector3.up * Random.Range(fireworkHeight.x, fireworkHeight.y) + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            float r = Random.value;
            var style = r < 0.55f ? FestivalFirework.Style.Peony : r < 0.78f ? FestivalFirework.Style.Ring : FestivalFirework.Style.Willow;
            int c = Random.Range(0, fireworkMaterials.Length);
            var mat = style == FestivalFirework.Style.Willow ? fireworkMaterials[0] : fireworkMaterials[c];
            // 菊は内側に別の色の芯を入れる
            var core = fireworkMaterials.Length > 1 ? fireworkMaterials[(c + Random.Range(1, fireworkMaterials.Length)) % fireworkMaterials.Length] : null;
            FestivalFirework.Launch(from, burst, Random.Range(fireworkRadius.x, fireworkRadius.y) * size, style, mat, fireworkShellMaterial, core);
        }

        // ---- 見ている屋台と武器 ----

        /// <summary>
        /// 頭の向き forward で見ている屋台の武器。今の武器の屋台の方をまだ見ているうちは (境目を switchMargin 度越えるまでは) 今の武器のまま
        /// </summary>
        public WeaponType WeaponFor(Vector3 forward, WeaponType current)
        {
            var me = transform.forward;
            float baseYaw = Mathf.Atan2(me.x, me.z) * Mathf.Rad2Deg;
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg - baseYaw;
            int count = Mathf.Min(stalls.Length, stallWeapons.Length);
            if (count == 0) return current;
            float step = 360f / count;
            int idx = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / step) % count;
            int cur = System.Array.IndexOf(stallWeapons, current);
            if (cur >= 0 && cur < count && cur != idx && Mathf.Abs(Mathf.DeltaAngle(yaw, cur * step)) < step / 2f + switchMargin) idx = cur;
            return stallWeapons[idx];
        }

        /// <summary>夏祭りの武器か (モードが終わったらいつもの銃に戻す)</summary>
        public bool IsFestivalWeapon(WeaponType type) => System.Array.IndexOf(stallWeapons, type) >= 0;

        public void RegisterHit() => Hits++;

        // ---- 金魚すくい ----

        // ---- 射的 ----

        [Header("射的 (当たるたびに倒れやすくなり、決まった回数で必ず倒れる)")]
        [Tooltip("1発目, 2発目, … に当たった時に倒れる確率 (今は 50% → 75% → 100%)。最後の値は 1 (その回数で必ず倒れる)")]
        public float[] prizeFallOdds = { 0.5f, 0.75f, 1f };
        readonly Dictionary<Target, int> prizeHits = new Dictionary<Target, int>(); // 景品ごとの、倒れずに当たった回数

        /// <summary>コルクが景品に当たった: 当たった回数に応じた確率で倒れる (1発目から50%、3発目で必ず)。倒れなければぐらっと揺れるだけ</summary>
        public void HitPrize(Target prize, Vector3 point)
        {
            prizeHits.TryGetValue(prize, out int n);
            float chance = prizeFallOdds.Length > 0 ? prizeFallOdds[Mathf.Min(n, prizeFallOdds.Length - 1)] : 1f;
            if (Random.value < chance)
            {
                prizeHits.Remove(prize); // 次に出てきた時は、また1発目から
                prize.Hit(point);        // 後ろへ倒れて落ちる。点数・音は GameManager
                return;
            }
            prizeHits[prize] = n + 1;
            prize.Wobble(14f + 6f * n); // 当てるほど大きく揺れる (もう少しで倒れそう)
            Sfx.Play("thunk");
            Popup(point, n + 2 >= prizeFallOdds.Length ? "ALMOST!" : "WOBBLE", new Color(1f, 0.85f, 0.5f), 0.1f);
        }

        public bool IsWater(Collider c) => water != null && c == water;

        /// <summary>
        /// ポイが水に入った: 近くの金魚を、点数ごとの確率ですくう / ポイが破れるかを決めて、すくう動き (ScoopRoutine) を始める。
        /// 近くに金魚がいなければ MISS
        /// </summary>
        public void Scoop(Vector3 point)
        {
            Sfx.Play("splash");
            if (splashMaterial != null) Explosion.Spawn(point, 0.22f, splashMaterial);
            FestivalFish best = null;
            float bestD = scoopRadius;
            foreach (var f in fish)
            {
                if (f == null || f.target == null || !f.target.IsUp || f.Captured) continue;
                float d = Vector3.Distance(f.transform.position, point); // 水槽は傾いているので、水平ではなくそのままの距離
                if (d < bestD) { bestD = d; best = f; }
            }
            if (best == null)
            {
                Popup(point, "MISS", new Color(0.7f, 0.85f, 1f), 0.12f);
                return;
            }
            bool success = Random.value < ChanceFor(best.target.points);
            if (scoopPoi == null || scoopBowl == null)
            {   // 動きの部品がない時は、その場で結果だけ
                if (success) best.target.Hit(best.transform.position);
                else { Sfx.Play("rip"); Popup(point, "RIPPED!", new Color(1f, 0.5f, 0.45f), 0.15f); best.Flee(); }
                return;
            }
            StartCoroutine(ScoopRoutine(best, point, success));
        }

        /// <summary>
        /// すくう動き: 水の中からポイが上がって金魚を乗せ、金魚がポイの上でぴちぴち暴れる。
        /// 失敗 = 紙が破れて、金魚が水へ落ちて逃げる。成功 = ポイが弧を描いてお椀まで運び、金魚がお椀に入って少し泳ぐ (ここで点数が入る)
        /// </summary>
        IEnumerator ScoopRoutine(FestivalFish f, Vector3 point, bool success)
        {
            f.Captured = true;
            var fishT = f.transform;
            var poi = Instantiate(scoopPoi, scoopPoi.transform.parent, false);
            poi.SetActive(true);
            var poiT = poi.transform;
            // 水面に沿って寝かせ、持ち手をプレイヤーの方へ向ける
            Vector3 up = water != null ? water.transform.up : Vector3.up;
            Vector3 viewer = Camera.main != null ? Camera.main.transform.position : point - transform.forward;
            Vector3 fwd = Vector3.ProjectOnPlane(point - viewer, up);
            Quaternion flat = Quaternion.LookRotation(fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward, up);
            poiT.rotation = flat;
            Vector3 start = point - up * 0.08f, lifted = point + up * 0.28f;
            Vector3 fishFrom = fishT.position;
            Quaternion fishRot = fishT.rotation;

            // 1. ポイが水の中から上がり、金魚がポイの上へ乗る
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float e = Smooth(t / 0.35f);
                poiT.position = Vector3.Lerp(start, lifted, e);
                fishT.position = Vector3.Lerp(fishFrom, poiT.position + up * 0.04f, e);
                fishT.rotation = Quaternion.Slerp(fishRot, flat, e);
                yield return null;
            }
            Sfx.Play("splash");
            // 2. ポイの上でぴちぴち暴れる (はねて、体をくねらせる)
            for (float t = 0f; t < 0.75f; t += Time.deltaTime)
            {
                poiT.position = lifted + up * (Mathf.Sin(t * 30f) * 0.008f);
                Flop(fishT, poiT.position, up, flat, t, 1f);
                yield return null;
            }

            if (!success)
            {
                // 3. 紙が破れて、金魚がぽちゃんと水へ落ちて逃げる。ポイは水の中へ沈んで消える
                SetPoiTorn(poiT);
                Sfx.Play("rip");
                Popup(lifted, "RIPPED!", new Color(1f, 0.5f, 0.45f), 0.15f);
                Vector3 from = fishT.position;
                for (float t = 0f; t < 0.3f; t += Time.deltaTime)
                {
                    float k = t / 0.3f;
                    fishT.position = Vector3.Lerp(from, point - up * 0.12f, k * k);
                    fishT.rotation = flat * Quaternion.Euler(0f, t * 900f, 0f);
                    poiT.position = lifted - up * (0.35f * k);
                    yield return null;
                }
                Sfx.Play("splash");
                if (splashMaterial != null) Explosion.Spawn(point, 0.18f, splashMaterial);
                f.Captured = false;
                f.Flee();
                Destroy(poi);
                yield break;
            }

            // 3. ポイが弧を描いてお椀まで運ぶ (金魚は少し暴れたまま)
            Vector3 a = poiT.position, b = scoopBowl.position + Vector3.up * 0.12f;
            Quaternion bowlFlat = Quaternion.LookRotation(Vector3.ProjectOnPlane(b - viewer, Vector3.up).normalized, Vector3.up);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = Smooth(t / 0.6f);
                var r = Quaternion.Slerp(flat, bowlFlat, k);
                poiT.SetPositionAndRotation(Vector3.Lerp(a, b, k) + Vector3.up * (0.35f * 4f * k * (1f - k)), r);
                Flop(fishT, poiT.position, r * Vector3.up, r, t + 0.75f, 0.5f);
                yield return null;
            }
            // 4. お椀の中へぽちゃん: お椀には見た目の複製を入れて少し泳がせ、本物の的は点数を入れて消える
            Sfx.Play("splash");
            if (splashMaterial != null) Explosion.Spawn(scoopBowl.position, 0.15f, splashMaterial);
            var copy = Instantiate(f.target.visual.gameObject, scoopBowl, false);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            copy.transform.localScale = Vector3.one * 0.8f;
            StartCoroutine(SwimInBowl(copy.transform));
            fishT.position = scoopBowl.position;
            f.target.Hit(scoopBowl.position); // 点数・音・メッセージは GameManager
            f.Captured = false; // 次に出てくる時は、水槽の別の所から (FestivalFish)
            Destroy(poi);
        }

        // ポイの上で暴れる金魚: 上下にはねて、体を左右にくねらせ、ときどきひっくり返る
        static void Flop(Transform fishT, Vector3 poiPos, Vector3 up, Quaternion flat, float t, float strength)
        {
            fishT.position = poiPos + up * (0.03f + Mathf.Abs(Mathf.Sin(t * 16f)) * 0.07f * strength);
            fishT.rotation = flat * Quaternion.Euler(Mathf.Sin(t * 23f) * 35f * strength, Mathf.Sin(t * 11f) * 50f * strength, Mathf.Sin(t * 19f) * 60f * strength);
        }

        // 紙が破れたポイ: 紙を消して、破れ残りの切れ端を出す
        static void SetPoiTorn(Transform poi)
        {
            var paper = poi.Find("Paper");
            var torn = poi.Find("Torn");
            if (paper != null) paper.gameObject.SetActive(false);
            if (torn != null) torn.gameObject.SetActive(true);
        }

        // お椀に入った金魚: しばらくお椀の中をくるくる泳いで、小さくなって消える
        IEnumerator SwimInBowl(Transform copy)
        {
            const float Time0 = 2.6f;
            for (float t = 0f; t < Time0; t += Time.deltaTime)
            {
                if (copy == null) yield break;
                float a = t * 2.4f;
                copy.localPosition = new Vector3(Mathf.Sin(a) * 0.08f, 0f, Mathf.Cos(a) * 0.08f);
                copy.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 90f, 0f);
                if (t > Time0 - 0.4f) copy.localScale = Vector3.one * (0.8f * (Time0 - t) / 0.4f);
                yield return null;
            }
            if (copy != null) Destroy(copy.gameObject);
        }

        static float Smooth(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        float ChanceFor(int points)
        {
            for (int i = 0; i < scoopPoints.Length && i < scoopChance.Length; i++)
                if (points <= scoopPoints[i]) return scoopChance[i];
            return scoopChance.Length > 0 ? scoopChance[scoopChance.Length - 1] : 0.5f;
        }

        // ---- 食べ物の屋台 ----

        /// <summary>お金が料理に当たった: お金 (夏祭りで稼いだ分) が足りれば買う (料理の的に当たる)。足りなければ買えない</summary>
        public void TryBuy(Target food, Vector3 point)
        {
            var gm = GameManager.I;
            if (spendMoneyOnFood && gm != null && gm.FestivalMoney < food.points)
            {
                Sfx.Play("snipermiss");
                Popup(point, $"NEED ¥{food.points}\nWIN PRIZES TO EARN!", new Color(1f, 0.4f, 0.35f), 0.15f);
                return;
            }
            food.Hit(point); // お金の計算・音・メッセージは GameManager (BuyFood)
        }

        /// <summary>料理を買った: 同じ見た目を、屋台の前の机に並べる (いっぱいになったら古い物から入れ替える)</summary>
        public void OnFoodBought(Target food)
        {
            if (tableSlots == null || tableSlots.Length == 0 || food.visual == null) return;
            var model = food.visual.Find("Model");
            if (model == null) return;
            var slot = tableSlots[nextSlot % tableSlots.Length];
            nextSlot++;
            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                var old = slot.GetChild(i).gameObject;
                bought.Remove(old);
                Destroy(old);
            }
            var copy = Instantiate(model.gameObject, slot, false);
            copy.name = "Bought_" + food.name;
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f);
            copy.SetActive(true);
            bought.Add(copy);
            StartCoroutine(PopIn(copy.transform));
        }

        IEnumerator PopIn(Transform t)
        {
            for (float k = 0f; k < 1f; k += Time.deltaTime / popTime)
            {
                if (t == null) yield break;
                t.localScale = Vector3.one * PopCurve(k);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        void ClearTable()
        {
            foreach (var go in bought) if (go != null) Destroy(go);
            bought.Clear();
            nextSlot = 0;
        }

        void Popup(Vector3 point, string text, Color color, float size)
        {
            if (font != null) ScorePopup.Spawn(font, point + Vector3.up * 0.25f, text, color, size);
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
