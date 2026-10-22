using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 発掘モードの舞台。演出の流れ:
    ///   1. プレイヤーの足元に土の床が広がって床下のハッチを覆い、ザクッザクッと真ん中に穴が掘られる。
    ///   2. プレイヤーは木の昇降台に乗って、穴の中を真下へゆっくり降りていく (屋台はそのまま動いている。十分に降りて見えなくなったら GameManager が止める)。
    ///   3. 長い縦穴を抜けると、宝石が光るいびつで広い洞窟の天井から、木のやぐらの上まで降りる。まわりではトロッコが線路を走り
    ///      (ぐるぐる回る線路と、トンネルの穴から出てきて別の穴に入る線路)、コウモリが飛んでいる。宝石とトロッコの的を撃つ。
    ///   4. 時間切れで、プレイヤーはまたゆっくり上がっていき、元の穴を抜ける。屋台が見えるくらいまで来た時には、もう屋台が動いている (GameManager)。
    ///      上まで戻ると穴が土で埋まり、土の床が消えてハッチが戻る。
    /// 屋台の出し入れ・点数・明るさの判断は GameManager が行い、ここは見た目と、プレイヤーの上げ下げ・的の出し方を受け持つ。
    /// </summary>
    public class MineCave : MonoBehaviour
    {
        [Header("穴を掘る")]
        [Tooltip("ハッチを覆う土の床 (真ん中に穴のあいた輪と、まわりの盛り土)")]
        public GameObject digCover;
        [Tooltip("穴をふさいでいる土 (掘ると小さくなって消え、戻ると大きくなって埋まる)")]
        public Transform plug;
        [Tooltip("掘る時に飛び散る土の色")]
        public Material dirtMaterial;
        public float digTime = 1.3f;
        public float fillTime = 0.8f;
        [Tooltip("土の床が足元から広がりきるまで / 縮んで消えるまでの秒数")]
        public float coverGrowTime = 0.9f;
        [Tooltip("昇降台の手すりが地面から生えてくる / 地面へ沈む秒数")]
        public float liftRiseTime = 0.7f;

        [Header("縦穴と洞窟")]
        [Tooltip("発掘モードの間だけ表示する物 (縦穴・洞窟)")]
        public GameObject[] showDuringStage;
        [Tooltip("プレイヤーといっしょに上下する木の昇降台")]
        public Transform lift;
        [Tooltip("洞窟のやぐらの上 (プレイヤーの足元が止まる所) の深さ (m)")]
        public float depth = 16f;
        [Tooltip("降りる / 上がる秒数")]
        public float descendTime = 16f;
        public float ascendTime = 13f;
        [Tooltip("洞窟の天井からやぐらの上までの高さ (m)。縦穴はその上 (depth - これ)")]
        public float caveRoomHeight = 10f;

        [Header("的")]
        [Tooltip("ぐるぐる回る線路のトロッコ (線路は各トロッコに設定済み)")]
        public MineCart[] loopCarts;
        [Tooltip("トンネルの穴から出てきて別の穴へ入るトロッコと、その線路")]
        public MineCart[] tunnelCarts;
        public MineTrack[] tunnelTracks;
        public Vector2 tunnelInterval = new Vector2(0.7f, 1.4f);
        public MineGem[] gems;
        [Tooltip("同時に光っている宝石の数")]
        public int maxGems = 24;
        [Tooltip("宝石を割ってから、別の宝石が光り始めるまでの秒数")]
        public Vector2 gemInterval = new Vector2(0.8f, 1.8f);

        [Header("天井から落ちてくる物")]
        [Tooltip("洞窟 (やぐらの上が原点)。天井・床の高さはこの座標")]
        public Transform caveRoot;
        public float ceilingY = 10f;
        public float floorY = -4f;
        [Tooltip("天井からちょろちょろ崩れ落ちる小石 (隠してある。使い回す)")]
        public Transform[] fallingRocks;
        [Tooltip("小石がこぼれ始める間隔 (秒)。1回に何粒か続けてこぼれる")]
        public Vector2 trickleInterval = new Vector2(0.5f, 1.6f);
        [Tooltip("ときどき天井から落ちてくる宝石 (床に当たるまでに撃てば点数)")]
        public MineGem[] fallingGems;
        public Vector2 gemDropInterval = new Vector2(2f, 4f);

        [Header("洞窟の雰囲気 (きらきら・ゆらめく明かり・舞うほこり)")]
        [Tooltip("結晶や鉱脈の上で、ときどきキラッと光る十字の光 (大きさが脈打つ)")]
        public Transform[] sparkles;
        [Tooltip("ゆらゆらゆらめくランタン・たいまつの明かり")]
        public Light[] flickerLights;
        [Tooltip("たいまつの炎 (大きさがゆらゆら揺れる)")]
        public Transform[] flames;
        [Tooltip("明かりの中をゆっくり漂うほこり")]
        public Transform[] motes;

        [Header("Timing")]
        [Tooltip("始まってから上がり始めるまでの秒数 (掘る・降りる時間も含む)")]
        public float duration = 42f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回撃った的 (宝石・トロッコ) の数</summary>
        public int Hits { get; private set; }
        /// <summary>上がって穴が埋まるまでの秒数</summary>
        public float CloseTime => ascendTime + fillTime + 0.1f;

        /// <summary>降りていって縦穴を抜け、縦穴の口からも地上の屋台が見えなくなった時 (GameManager が屋台をその場で隠す)</summary>
        public event System.Action WentDeep;
        /// <summary>縦穴を抜けて洞窟に入った時 / 上がり始めた時 (GameManager が明るさを変える)</summary>
        public event System.Action EnteredCave, Rising;
        /// <summary>上がってきて縦穴に入った時。まだ屋台は見えないので、GameManager がここで屋台を並べておく</summary>
        public event System.Action NearSurface;
        /// <summary>上がりきって穴が土で埋まった時 (GameManager がハッチを戻す)</summary>
        public event System.Action Closed;
        /// <summary>土の床が広がりきってハッチを覆った時 (GameManager がその下でハッチを消す)</summary>
        public event System.Action Covered;
        public event System.Action<MineGem> GemBroken;

        Transform player;
        float startTime, nextTunnel, playerStartY;
        Coroutine anim;
        Vector3 plugScale;
        readonly List<MineGem> hiddenGems = new List<MineGem>();

        void Awake()
        {
            plugScale = plug != null ? plug.localScale : Vector3.one;
            // きらきら・明かり・ほこりの元の状態と、ばらばらのリズム
            sparkleScale = new Vector3[sparkles.Length];
            sparklePhase = new float[sparkles.Length];
            sparkleRate = new float[sparkles.Length];
            for (int i = 0; i < sparkles.Length; i++)
            {
                sparkleScale[i] = sparkles[i].localScale;
                sparklePhase[i] = Random.value * 20f;
                sparkleRate[i] = Random.Range(0.6f, 1.6f);
            }
            lightBase = new float[flickerLights.Length];
            for (int i = 0; i < flickerLights.Length; i++) lightBase[i] = flickerLights[i].intensity;
            moteHome = new Vector3[motes.Length];
            for (int i = 0; i < motes.Length; i++) moteHome[i] = motes[i].localPosition;
            // 点数の文字 (TextMesh) は岩や地面を透けて見えるので、洞窟に入っている間だけ表示する
            var labels = new List<Renderer>();
            foreach (var g in gems) if (g != null && g.pointLabel != null) labels.Add(g.pointLabel.GetComponent<Renderer>());
            foreach (var c in loopCarts) if (c != null) foreach (var l in c.pointLabels) if (l != null) labels.Add(l.GetComponent<Renderer>());
            foreach (var c in tunnelCarts) if (c != null) foreach (var l in c.pointLabels) if (l != null) labels.Add(l.GetComponent<Renderer>());
            if (fallingGems == null) fallingGems = new MineGem[0];
            foreach (var g in fallingGems) if (g != null && g.pointLabel != null) labels.Add(g.pointLabel.GetComponent<Renderer>());
            labelRenderers = labels.ToArray();
            if (fallingRocks == null) fallingRocks = new Transform[0];
            rockVel = new float[fallingRocks.Length];
            rockSpin = new Vector3[fallingRocks.Length];
            rockScale = new Vector3[fallingRocks.Length];
            for (int i = 0; i < fallingRocks.Length; i++) rockScale[i] = fallingRocks[i].localScale;
            SetStageVisible(false);
            if (digCover != null) digCover.SetActive(false);
        }

        /// <summary>発掘モードを始める。playerRoot = 上げ下げするプレイヤー (足元が原点)</summary>
        public void Begin(Transform playerRoot)
        {
            if (IsActive) return;
            IsActive = true;
            player = playerRoot;
            playerStartY = player.localPosition.y;
            startTime = Time.time;
            Hits = 0;
            nextTunnel = Time.time + digTime + descendTime * 0.6f;
            Play(Enter());
        }

        /// <summary>時間切れ: 上がって元の穴へ戻る。immediate なら、その場ですぐ地上へ戻して全部隠す (ラウンドの終わり・リセット)</summary>
        public void End(bool immediate = false)
        {
            if (!IsActive && !immediate) return;
            IsActive = false;
            if (immediate)
            {
                if (anim != null) StopCoroutine(anim);
                anim = null;
                StopTargets();
                SetPlayerY(playerStartY);
                if (plug != null) plug.localScale = plugScale;
                if (digCover != null) digCover.SetActive(false);
                SetCover(1f);
                liftOffset = 0f;
                SetStageVisible(false);
                NearSurface?.Invoke();
                Closed?.Invoke();
                return;
            }
            Play(Leave());
        }

        IEnumerator Enter()
        {
            // 1. 足元から土の床がむくっと広がってハッチを覆い (少し行き過ぎて戻る)、昇降台の手すりが地面から生えてくる
            if (plug != null) plug.localScale = plugScale;
            SetCover(0f);
            if (digCover != null) digCover.SetActive(true);
            liftOffset = -LiftHidden;
            for (float t = 0f; t < 1f; t += Time.deltaTime / coverGrowTime)
            {
                SetCover(EaseOutBack(t));
                yield return null;
            }
            SetCover(1f);
            Covered?.Invoke(); // 土の床の下でハッチが消える (見えない)
            // 縦穴・洞窟・昇降台は、土の床でふさいでから出す (広がる途中に穴の下が見えないように)
            SetStageVisible(true);
            SetPlayerY(playerStartY);
            StartTargets();
            for (float t = 0f; t < 1f; t += Time.deltaTime / liftRiseTime)
            {
                liftOffset = -LiftHidden * (1f - EaseOutBack(t));
                SetPlayerY(playerStartY);
                yield return null;
            }
            liftOffset = 0f;
            SetPlayerY(playerStartY);
            // 2. ザクッ、ザクッ、ザクッと3回に分けて、真ん中に穴が掘られる (音に合わせて、ひと掘りごとにぐっと広がる)
            Sfx.Play("dig");
            for (float t = 0f; t < 1f; t += Time.deltaTime / digTime)
            {
                float step = Mathf.Min(2f, Mathf.Floor(t * 3f)), frac = Mathf.Clamp01((t * 3f - step) * 2.2f);
                float k = (step + 1f - (1f - frac) * (1f - frac)) / 3f; // 0 → 1 (ひと掘りごとに速く広がって止まる)
                if (plug != null) plug.localScale = new Vector3(plugScale.x * (1f - k), plugScale.y, plugScale.z * (1f - k));
                if (Random.value < 0.25f && dirtMaterial != null && plug != null)
                    Explosion.Spawn(plug.position + Random.insideUnitSphere * 0.6f + Vector3.up * 0.2f, Random.Range(0.3f, 0.6f), dirtMaterial);
                yield return null;
            }
            if (plug != null) plug.localScale = new Vector3(0.001f, plugScale.y, 0.001f);
            // 3. ゆっくり真下へ降りる
            Sfx.Play("door");
            bool deep = false, inCave = false;
            float caveTop = depth - caveRoomHeight; // 縦穴を抜けて洞窟の天井に出る深さ
            for (float t = 0f; t < 1f; t += Time.deltaTime / descendTime)
            {
                float e = t * t * (3f - 2f * t);
                float d = depth * e;
                SetPlayerY(playerStartY - d);
                if (!deep && d > caveTop) { deep = true; WentDeep?.Invoke(); } // 縦穴の口からも地上の屋台が見えない深さ (洞窟に入った所)
                if (!inCave && d > caveTop) { inCave = true; EnteredCave?.Invoke(); }
                yield return null;
            }
            SetPlayerY(playerStartY - depth);
            if (!deep) WentDeep?.Invoke();
            if (!inCave) EnteredCave?.Invoke();
            anim = null;
        }

        IEnumerator Leave()
        {
            Rising?.Invoke();
            CancelInvoke(nameof(ShowRandomGem));
            Sfx.Play("door");
            // 3. ゆっくり上がる。縦穴に入ったら (まだ屋台は見えない)、もう屋台が並んで動いている (GameManager)
            float from = player != null ? playerStartY - player.localPosition.y : 0f;
            bool near = false, stopped = false;
            for (float t = 0f; t < 1f; t += Time.deltaTime / ascendTime)
            {
                float e = t * t * (3f - 2f * t);
                float d = Mathf.Lerp(from, 0f, e);
                SetPlayerY(playerStartY - d);
                if (!stopped && d < depth - caveRoomHeight) { stopped = true; StopTargets(); } // 縦穴に入って洞窟が見えなくなったら、トロッコと宝石を片付ける
                if (!near && d < depth - caveRoomHeight) { near = true; NearSurface?.Invoke(); } // まだ縦穴の口から屋台が見えない深さのうちに並べておく
                yield return null;
            }
            SetPlayerY(playerStartY);
            if (!stopped) StopTargets();
            if (!near) NearSurface?.Invoke();
            // 4. 穴が土でじわっと埋まり、昇降台の手すりが地面へ沈み、土の床が足元へ縮んで消える (その下でハッチが戻る)
            for (float t = 0f; t < 1f; t += Time.deltaTime / fillTime)
            {
                float e = t * t * (3f - 2f * t);
                if (plug != null) plug.localScale = new Vector3(plugScale.x * e, plugScale.y, plugScale.z * e);
                yield return null;
            }
            if (plug != null) plug.localScale = plugScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / liftRiseTime)
            {
                liftOffset = -LiftHidden * t * t;
                SetPlayerY(playerStartY);
                yield return null;
            }
            SetStageVisible(false);
            liftOffset = 0f;
            Closed?.Invoke(); // ハッチが土の床の下でふくらみ始める
            for (float t = 0f; t < 1f; t += Time.deltaTime / coverGrowTime)
            {
                SetCover(1f - t * t * (3f - 2f * t));
                yield return null;
            }
            if (digCover != null) digCover.SetActive(false);
            SetCover(1f);
            anim = null;
        }

        const float LiftHidden = 1.2f; // 昇降台が地面の下に隠れている時の深さ
        float liftOffset;

        // 土の床の広がり (0 = 足元の点 → 1 = ハッチを覆う大きさ)
        void SetCover(float k)
        {
            if (digCover == null) return;
            float s = Mathf.Max(0.001f, k);
            digCover.transform.localScale = new Vector3(s, 1f, s);
        }

        // むくっと出る: 少し行き過ぎてから戻る
        static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c = 1.7f;
            float x = t - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }

        void SetPlayerY(float y)
        {
            if (player == null) return;
            var p = player.localPosition;
            player.localPosition = new Vector3(p.x, y, p.z);
            if (lift != null)
            {
                var l = lift.position;
                lift.position = new Vector3(l.x, player.position.y + liftOffset, l.z); // 昇降台はプレイヤーの足元 (地面から生える / 沈む間だけ下へずらす)
            }
        }

        void StartTargets()
        {
            for (int i = 0; i < loopCarts.Length; i++)
            {
                var c = loopCarts[i];
                if (c == null || c.track == null) continue;
                // 同じ線路のトロッコは、等間隔にばらして走らせる
                int same = 0, index = 0;
                for (int k = 0; k < loopCarts.Length; k++)
                    if (loopCarts[k] != null && loopCarts[k].track == c.track) { if (k < i) index++; same++; }
                c.Run(c.track.Length * index / Mathf.Max(1, same), false);
            }
            hiddenGems.Clear();
            foreach (var g in gems) { g.Hide(); hiddenGems.Add(g); }
            for (int i = 0; i < maxGems; i++) ShowRandomGem();
            foreach (var g in fallingGems) if (g != null) g.Hide();
            nextTrickle = Time.time + Random.Range(trickleInterval.x, trickleInterval.y);
            nextGemDrop = Time.time + Random.Range(gemDropInterval.x, gemDropInterval.y);
        }

        void StopTargets()
        {
            CancelInvoke(nameof(ShowRandomGem));
            foreach (var c in loopCarts) if (c != null) c.Stop();
            foreach (var c in tunnelCarts) if (c != null) c.Stop();
            foreach (var g in gems) if (g != null) g.Hide();
            foreach (var g in fallingGems) if (g != null) g.Hide();
            pendingDrops.Clear();
            hiddenGems.Clear();
        }

        void ShowRandomGem()
        {
            if (!IsActive || hiddenGems.Count == 0) return;
            int i = Random.Range(0, hiddenGems.Count);
            var g = hiddenGems[i];
            hiddenGems.RemoveAt(i);
            g.Show(OnGemBroken);
        }

        void OnGemBroken(MineGem g)
        {
            Hits++;
            GemBroken?.Invoke(g);
            hiddenGems.Add(g);
            Invoke(nameof(ShowRandomGem), Random.Range(gemInterval.x, gemInterval.y)); // 別の宝石が光り始める
        }

        /// <summary>トロッコの的が撃たれた (GameManager から。数えるだけ)</summary>
        public void RegisterCartHit() => Hits++;

        Vector3[] sparkleScale, moteHome;
        float[] sparklePhase, sparkleRate, lightBase;

        // ---- 天井から落ちてくる物 ----

        float[] rockVel;
        Vector3[] rockSpin, rockScale;
        float nextTrickle, nextGemDrop;
        // これからこぼれる小石 (時刻・天井の場所 (洞窟の座標)・大きさの倍率)、落ちる前に天井がこぼれる宝石 (時刻・場所・宝石)
        readonly List<(float at, Vector3 spot, float size)> pendingRocks = new List<(float, Vector3, float)>();
        readonly List<(float at, Vector3 spot, MineGem gem)> pendingDrops = new List<(float, Vector3, MineGem)>();

        bool InCave => player != null && playerStartY - player.localPosition.y > depth - caveRoomHeight;

        // 天井の、ある場所の高さ (洞窟の座標)。縦穴のまわりが一番高く、外へ向かって少し下がる
        float CeilingAt(float r) => ceilingY + 1f - r * 0.06f;

        Vector3 RandomCeilingSpot(float rMin, float rMax)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(rMin, rMax);
            return new Vector3(Mathf.Sin(a) * r, CeilingAt(r), Mathf.Cos(a) * r);
        }

        void UpdateFalling()
        {
            if (caveRoot == null) return;
            float now = Time.time;
            bool spawning = IsActive && InCave;
            // 天井のどこかから、小石がちょろちょろ (ほこりが舞って、何粒か続けてこぼれる。ときどき少し大きな石)
            if (spawning && now >= nextTrickle)
            {
                nextTrickle = now + Random.Range(trickleInterval.x, trickleInterval.y);
                var spot = RandomCeilingSpot(4f, 24f);
                Dust(spot, Random.Range(0.4f, 0.8f));
                int n = Random.Range(2, 6);
                float t = now + Random.Range(0.05f, 0.2f);
                for (int i = 0; i < n; i++)
                {
                    pendingRocks.Add((t, spot + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f)), Random.value < 0.12f ? Random.Range(2f, 3f) : Random.Range(0.6f, 1.2f)));
                    t += Random.Range(0.08f, 0.3f);
                }
            }
            // ときどき宝石が落ちてくる: まず天井がぱらぱらこぼれて (前ぶれ)、少しして宝石が落ちる
            if (spawning && now >= nextGemDrop && TimeLeft > 5f)
            {
                nextGemDrop = now + Random.Range(gemDropInterval.x, gemDropInterval.y);
                var free = new List<MineGem>();
                foreach (var g in fallingGems) if (g != null && !g.IsShown && !Pending(g)) free.Add(g);
                if (free.Count > 0)
                {
                    var gem = free[Random.Range(0, free.Count)];
                    var spot = RandomCeilingSpot(5f, 14f);
                    Dust(spot, 1f);
                    Sfx.Play("crack");
                    for (int i = 0; i < 4; i++) pendingRocks.Add((now + i * 0.12f, spot + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f)), Random.Range(0.6f, 1f)));
                    pendingDrops.Add((now + 0.7f, spot, gem));
                }
            }
            for (int i = pendingRocks.Count - 1; i >= 0; i--)
            {
                if (now < pendingRocks[i].at) continue;
                DropRock(pendingRocks[i].spot, pendingRocks[i].size);
                pendingRocks.RemoveAt(i);
            }
            for (int i = pendingDrops.Count - 1; i >= 0; i--)
            {
                if (now < pendingDrops[i].at) continue;
                var (_, spot, gem) = pendingDrops[i];
                pendingDrops.RemoveAt(i);
                if (!IsActive) continue;
                Dust(spot, 0.9f);
                gem.Drop(caveRoot.TransformPoint(spot + Vector3.down * 0.4f), caveRoot.TransformPoint(new Vector3(0f, floorY + 0.2f, 0f)).y, OnFallingGemBroken, null);
            }
            // 落ちている小石を動かす (床に当たったら、小さなほこりを立てて消える)
            float floorWorld = caveRoot.TransformPoint(new Vector3(0f, floorY, 0f)).y;
            for (int i = 0; i < fallingRocks.Length; i++)
            {
                var r = fallingRocks[i];
                if (!r.gameObject.activeSelf) continue;
                rockVel[i] += 9.8f * Time.deltaTime;
                r.position += Vector3.down * (rockVel[i] * Time.deltaTime);
                r.Rotate(rockSpin[i] * Time.deltaTime, Space.Self);
                if (r.position.y > floorWorld) continue;
                if (r.localScale.x > rockScale[i].x * 1.8f || Random.value < 0.4f) Dust(caveRoot.InverseTransformPoint(r.position), r.localScale.x * 2.5f);
                r.gameObject.SetActive(false);
            }
        }

        bool Pending(MineGem g)
        {
            foreach (var d in pendingDrops) if (d.gem == g) return true;
            return false;
        }

        void DropRock(Vector3 spot, float size)
        {
            for (int i = 0; i < fallingRocks.Length; i++)
            {
                var r = fallingRocks[i];
                if (r.gameObject.activeSelf) continue;
                r.position = caveRoot.TransformPoint(spot);
                r.localScale = rockScale[i] * size;
                r.rotation = Random.rotation;
                rockVel[i] = Random.Range(0f, 0.6f);
                rockSpin[i] = Random.insideUnitSphere * 360f;
                r.gameObject.SetActive(true);
                return;
            }
        }

        // 天井や床で舞うほこり (洞窟の座標)
        void Dust(Vector3 spot, float size)
        {
            if (dirtMaterial != null && caveRoot != null) Explosion.Spawn(caveRoot.TransformPoint(spot), size, dirtMaterial);
        }

        // 落ちてくる宝石を撃った (点数はもう入っている)
        void OnFallingGemBroken(MineGem g)
        {
            Hits++;
            GemBroken?.Invoke(g);
        }

        void HideFalling()
        {
            pendingRocks.Clear();
            pendingDrops.Clear();
            foreach (var r in fallingRocks) if (r != null) r.gameObject.SetActive(false);
            foreach (var g in fallingGems) if (g != null) g.Hide();
        }

        void Update()
        {
            if (stageVisible) { UpdateAtmosphere(); UpdateFalling(); }
            UpdateLabels();
            if (!IsActive || Time.time < nextTunnel || tunnelTracks == null || tunnelTracks.Length == 0) return;
            nextTunnel = Time.time + Random.Range(tunnelInterval.x, tunnelInterval.y);
            var free = new List<MineCart>();
            foreach (var c in tunnelCarts) if (c != null && !c.IsRunning) free.Add(c);
            if (free.Count == 0) return;
            var cart = free[Random.Range(0, free.Count)];
            var track = tunnelTracks[Random.Range(0, tunnelTracks.Length)];
            bool reverse = Random.value < 0.5f;
            cart.track = track;
            cart.Run(reverse ? track.Length : 0f, reverse);
        }

        void SetStageVisible(bool visible)
        {
            stageVisible = visible;
            if (!visible) HideFalling();
            if (showDuringStage != null)
                foreach (var go in showDuringStage) if (go != null) go.SetActive(visible);
        }

        bool stageVisible;

        Renderer[] labelRenderers;
        bool labelsShown = true;

        // 点数の文字は、プレイヤーが縦穴を抜けて洞窟の中にいる間だけ出す (地上や縦穴から透けて見えないように)
        void UpdateLabels()
        {
            bool show = stageVisible && player != null && playerStartY - player.localPosition.y > depth - caveRoomHeight;
            if (show == labelsShown || labelRenderers == null) return;
            labelsShown = show;
            foreach (var r in labelRenderers) if (r != null) r.enabled = show;
        }

        // 洞窟の雰囲気: 結晶の上の十字の光がときどきキラッと光り (ふだんは小さく、ときどき大きく脈打つ)、
        // ランタンの明かりがゆらゆらゆらめき、ほこりが明かりの中をゆっくり漂う
        void UpdateAtmosphere()
        {
            float time = Time.time;
            for (int i = 0; i < sparkles.Length; i++)
            {
                float w = Mathf.Sin(time * sparkleRate[i] * 2.1f + sparklePhase[i]);
                float flash = Mathf.Pow(Mathf.Max(0f, w), 12f); // たまにだけ大きく光る
                float s = 0.15f + 1.1f * flash + 0.08f * Mathf.Sin(time * 9f + sparklePhase[i]);
                sparkles[i].localScale = sparkleScale[i] * Mathf.Max(0.001f, s);
                sparkles[i].localRotation = Quaternion.Euler(0f, 0f, time * 25f + sparklePhase[i] * 10f);
            }
            for (int i = 0; i < flickerLights.Length; i++)
                flickerLights[i].intensity = lightBase[i] * (0.82f + 0.18f * Mathf.PerlinNoise(time * 3.2f, i * 7.3f) + 0.05f * Mathf.Sin(time * 17f + i));
            if (flames != null)
                for (int i = 0; i < flames.Length; i++)
                {
                    // 炎は細かく伸び縮みしながら、ゆらゆら揺れる
                    float n = Mathf.PerlinNoise(time * 6f, i * 3.1f);
                    flames[i].localScale = new Vector3(0.9f + 0.2f * Mathf.PerlinNoise(time * 5f + 7f, i), 0.8f + 0.45f * n, 0.9f + 0.2f * Mathf.PerlinNoise(time * 5f, i + 11f));
                    flames[i].localRotation = Quaternion.Euler((Mathf.PerlinNoise(time * 2.5f, i + 3f) - 0.5f) * 16f, 0f, (Mathf.PerlinNoise(time * 2.5f + 5f, i) - 0.5f) * 16f);
                }
            for (int i = 0; i < motes.Length; i++)
            {
                float ph = i * 1.37f;
                motes[i].localPosition = moteHome[i] + new Vector3(Mathf.Sin(time * 0.13f + ph) * 0.9f, Mathf.Sin(time * 0.09f + ph * 2f) * 0.6f, Mathf.Cos(time * 0.11f + ph) * 0.9f);
            }
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
