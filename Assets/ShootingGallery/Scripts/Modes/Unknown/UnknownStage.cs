using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// Unknown ステージ (ワープゲートの的を撃つと始まる。計画は new_docs/develop.txt)。
    ///   行き: その瞬間、世界がその場で灰色に止まる (キーン。GrayWorld。本物の立体のまま) → 空中に透明なガラスのひびが広がる (ピキピキ)。
    ///         ひびが広がっている間に、屋台・ハッチ・地面をまとめた空間を「1つの立体」として、ひびの線どおりに破片へ切り分けておく (WorldShatter)。
    ///         → 割れる瞬間に、本物を同じ形・同じ灰色の破片と入れ替え (Swapped: GameManager が本物の屋台・ハッチを隠す。見た目は同じなので気づかない)、
    ///         撃った的の方から、ガラスと空間の破片が立体のまま散らばる (ガシャーン)。提灯や柱も、破片の形で途中から切れて飛ぶ。
    ///         割れた向こうは「ふしぎな空間」(夕暮れ色の空・市松の床・宙に浮くドアや時計や柱)。割れた瞬間 (Shattered) に空間の明るさにする。
    ///   中: まわりにワープゲート (アメリカの田舎・宇宙・海辺・夜の街・雪国) が順に開き、奥の世界からいろいろな物が紛れ込んでくる (UnknownObject)。
    ///       宙には時計やドアも、ぽんと現れる。それを撃つ (時間制)。
    ///       しばらくすると空に大きなワープホールが開き、こちらへ迫ってきて、プレイヤーがその輪をくぐる (枠くぐり)。
    ///       輪の中には向こうの背景 (雲海の上の星空) が見えていて、くぐると背景がそのまま星空に入れ替わる。床・飾り・ゲートは何も変わらない。
    ///   帰り: ゲートは開いたまま (半透明の部品だけ消す)。散らばった破片とガラスが逆回しで戻ってきて、灰色の空間がきれいにつながって組み上がる
    ///         (飛んでいる間はひびを見せない) → 本物の屋台と入れ替え (Left。本物も灰色にしてある)。張り合わさった瞬間、手前のガラスに細いひびが見える →
    ///         灰色を少しずつ弱めて色が戻る (戻り始めると同時に、ひびはすっと消える) → 屋台の前 (Closed)。
    /// 写真は使わないので、奥行き・両目の立体感が最後まで残る。後処理エフェクトも使わない (VR・WebGL で軽く)。
    /// プレイヤーは動かない。屋台の出し入れ・点数の判断は GameManager が行い、ここは見た目と的の出し方だけを受け持つ。
    /// </summary>
    public class UnknownStage : MonoBehaviour
    {
        [Header("空中のひび割れガラス (プレイヤーを包む透明な球)")]
        [Tooltip("ガラスの材質 (Shaders/UnknownShard.shader)。始める時に複製して使う")]
        public Material shardMaterial;
        [Tooltip("割れた後のガラスの材質: 灰色に止まった世界の空の色で塗った破片 (Shaders/UnknownShardSolid.shader) / その所の奥行きを一番奥に戻す膜 (Shaders/UnknownShardDepth.shader)")]
        public Material shardSolidMaterial;
        public Material shardDepthMaterial;
        [Tooltip("球の横・上の半径 (m) / 下の半径 (床にめり込まないように、頭の高さより少し小さく)")]
        public float shellRadius = 1.7f; // (屋台のカウンターの手前 (頭から約 2.05m) に食い込まないように。手に持った銃よりは外)
        public float shellDown = 1.45f;
        [Tooltip("球を割る緯度・経度の数 (破片の数 ≒ rows × cols × 2)")]
        public int rows = 9;
        public int cols = 18;
        [Tooltip("割れた景色の一片 (ガラスの破片と、その奥の屋台・地面の破片をひとかたまりで) が剥がれ落ちて消えるまでの秒数")]
        public float glassFlyTime = 2.8f;
        [Tooltip("剥がれ落ちる速さ: 頭を中心に回る、はじめの速さ / だんだん速くなる分 (度/秒, 度/秒²) / 自分の向きのまわりに回る速さ (度/秒) / 頭から遠ざかる強さ")]
        public Vector2 fallSpeed = new Vector2(8f, 18f);
        public Vector2 fallAccel = new Vector2(22f, 34f);
        public Vector2 pieceSpin = new Vector2(20f, 70f);
        public float recede = 0.45f;

        [Header("世界の割れ方")]
        [Tooltip("灰色にしない物 (プレイヤー・HUD)。この演出そのものは自動で除く")]
        public Transform[] grayExclude;
        [Tooltip("地面 (穴のあいた床)。屋台といっしょに破片に切り分ける")]
        public Renderer groundRenderer;

        [Header("ふしぎな空間")]
        [Tooltip("ふしぎな空間 (床・ゲート・浮かぶ飾り)。割れてから帰りに組み上がるまで出す")]
        public GameObject space;
        [Tooltip("ふしぎな空間の間は消しておく、いつもの床の部品 (地面・穴のふち)。地面以外は割れる部品にして一緒に割る")]
        public Transform[] groundParts;
        [Tooltip("ワープゲート (原点が入り口の真ん中。+Z が奥の世界の方)。正面に近いものから順に開く")]
        public Transform[] gates;
        public float gateOpenTime = 0.7f;
        public float gateInterval = 0.45f;
        [Tooltip("ふしぎな空間の床")]
        public WonderFloor floor;

        [Header("大きなワープホール (途中で開いて、こちらへ迫ってきて、プレイヤーがくぐると背景が変わる)")]
        [Tooltip("大きなホール (原点が入り口の真ん中。+Z が奥。開く時にプレイヤーの方を向ける)")]
        public Transform bigHole;
        [Tooltip("入り口の半径 (m。この円の中にだけ、向こうの背景が見える)")]
        public float holeRadius = 14f;
        [Tooltip("開く所: プレイヤーからの距離 (m) / 見上げる角度 (度)")]
        public float holeDistance = 42f;
        public float holeElevation = 18f;
        [Tooltip("遊び始めてから開くまで / 開く / 開いてから動き出すまで / プレイヤーの所まで来る / くぐった後に後ろへ抜けて閉じる 秒数")]
        public float holeDelay = 15f;
        public float holeOpenTime = 1.4f;
        public float holeHoldTime = 0.8f;
        public float holeApproachTime = 3.2f;
        public float holeCloseTime = 1.4f;
        [Tooltip("ホールの向こうの背景 (星空の球・雲海・星・オーロラ・月。Shaders/UnknownCosmos.shader で、ホールの輪を通して見える所だけ描く)")]
        public GameObject cosmos;
        public Color cosmosSky = new Color(0.04f, 0.05f, 0.16f);

        [Header("紛れ込んでくる物 (的)")]
        [Tooltip("撃つ物 (gateIndex のゲートから出てくる)")]
        public UnknownObject[] objects;
        public int maxFlying = 5;
        public Vector2 launchInterval = new Vector2(0.7f, 1.3f);
        [Tooltip("ゲートの奥から出てきて、止まる所までの秒数")]
        public float travelTime = 2.6f;
        [Tooltip("止まる所: プレイヤーからの距離 / 床からの高さ (m) / そこで漂っている秒数")]
        public Vector2 hoverDistance = new Vector2(3.5f, 6.5f);
        public Vector2 hoverHeight = new Vector2(0.8f, 3.2f);
        public Vector2 hoverTime = new Vector2(3.5f, 6f);

        [Header("時間")]
        [Tooltip("Unknown ステージの長さ (秒。始めてから)")]
        public float duration = 45f;
        [Tooltip("灰色に止まったまま待つ / ひびが広がる 秒数")]
        public float grayHold = 1.8f;  // (前は 1.1。急ぎすぎないように、止まった世界を少し見せる)
        public float crackTime = 2.6f; // (前は 1.4。ひびがゆっくり広がっていく)
        [Tooltip("帰り: 部品が戻ってくる (1つ) / 正面から真後ろまでの時間のずれ / ひびがふさがる / 色が戻る 秒数")]
        public float returnTime = 1.3f;
        public float returnSpread = 1.2f;
        [Tooltip("帰り: 組み上がってから灰色のまま止めておく秒数 / 灰色を少しずつ弱めて元の色に戻しきるまでの秒数")]
        public float colorHold = 0.5f;
        public float colorTime = 2.8f;
        [Tooltip("帰り: 張り合わさった瞬間に見せる細いひびの太さ / 濃さ / 色が戻り始めてから消えきるまでの秒数")]
        public float thinCrack = 0.3f;
        public float thinCrackFade = 0.7f;
        public float thinCrackOut = 0.3f;

        [Header("BGM (コードで作る、ふしぎで明るいオルゴールの曲)")]
        [Range(0f, 1f)] public float bgmVolume = 0.35f;
        /// <summary>このモードの効果音 (凍る・ひび・割れる・ワープ・組み上がる…) の大きさ (BGM を聞かせるために小さめ)</summary>
        public const float SfxVolume = 0.7f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        /// <summary>今回撃った数</summary>
        public int Hits { get; private set; }
        /// <summary>時間切れから、色が戻って屋台が見えるまでの秒数</summary>
        public float CloseTime => returnTime + returnSpread + colorHold + colorTime + 0.3f;

        /// <summary>行き: 割れる瞬間、本物を同じ形の灰色の破片に入れ替えた (GameManager が本物の屋台・ハッチを隠す)</summary>
        public event System.Action Swapped;
        /// <summary>行き: 世界が割れた (外はもう、ふしぎな空間。GameManager が空間の明るさにする)</summary>
        public event System.Action Shattered;
        /// <summary>行き: 割れ終わった (遊び始める)</summary>
        public event System.Action Arrived;
        /// <summary>帰り: 灰色の世界が組み上がった (GameManager が本物の屋台・ハッチをその場ですぐに出し、明るさを戻す)。true ならラウンドの終わりなどで、すぐに戻した</summary>
        public event System.Action<bool> Left;
        /// <summary>帰り: 色が戻った (屋台の的を出してよい)</summary>
        public event System.Action Closed;
        /// <summary>的を撃ち落とした (GameManager が演出を出す)</summary>
        public event System.Action<UnknownObject> ObjectDown;

        // ガラスの破片 1枚
        // 割れた景色の一片 = ガラスの破片 1枚と、その奥 (頭から見て同じ切れ目の中) の屋台・地面の破片。みな同じに動く
        struct Shard
        {
            public Vector3 home;      // 球の中心 (頭) からの、元の場所 (破片の真ん中)
            public Vector3 axis;      // 頭から破片の真ん中への向き
            public int first;         // 頂点の始まり (1枚 4頂点)
            public Vector3 fallAxis;  // 頭を通る回す軸 (この軸で回すと、目から見て一片が下・外へずれていく)
            public float fallSpeed, fallAccel, spin;
            public float maxFall;     // 回してよい角度 (度)。これ以上回すと、一番低い所を過ぎて反対側から上がってきてしまう
            public float delay;       // 割れ始めてから剥がれ始めるまでの秒数 (正面ほど早い)
        }
        // 一片ごとの、今の動き (頭を中心にした回転・遠ざかる倍率・消えていく大きさ)。ガラスと空間の破片で同じものを使う
        Quaternion[] pieceRot;
        float[] pieceScale, pieceVanish;

        Transform shell;
        Mesh mesh;
        Material mat, solidMat;
        MeshRenderer shellRenderer, depthRenderer;
        Color graySky; // 灰色に止まった世界の空の色 (割れたガラスの破片の色)
        Shard[] shards;
        Vector3[] localVerts; // 破片の真ん中からの頂点の位置 (元の形)
        Vector3[] verts;      // 書き直す頂点 (使い回す)
        WorldShatter shatter;
        float startTime, nextLaunch;
        bool inside;
        Coroutine anim, gateRoutine, buildRoutine, holeRoutine;
        Vector3 holeScale;
        bool[] groundWasActive;
        bool[] gateOpen;
        Vector3[] gateScale;
        // ゲートの半透明の部品 (入り口の膜・奥の世界の星雲など) と名前の文字。帰りは、戻ってくる灰色の破片の上に透けないように消す
        readonly List<Renderer> gateSeeThrough = new List<Renderer>();
        readonly List<(TextMesh text, Color color)> gateTexts = new List<(TextMesh, Color)>();
        readonly List<UnknownObject> free = new List<UnknownObject>(); // 使い回す
        readonly List<Transform> roots = new List<Transform>();
        readonly GrayWorld gray = new GrayWorld();
        HashSet<Transform> grayExcludeSet;
        AudioSource bgm;
        float bgmTarget;

        static readonly int CrackId = Shader.PropertyToID("_Crack");
        static readonly int CrackGlowId = Shader.PropertyToID("_CrackGlowAmount");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int SkyColorId = Shader.PropertyToID("_SkyColor");
        // ホールの向こうの背景を描く所 (Shaders/UnknownCosmos.shader のグローバルの値)
        static readonly int PortalId = Shader.PropertyToID("_CosmosPortal");
        static readonly int PortalDirId = Shader.PropertyToID("_CosmosPortalDir");
        const float PortalNone = 0f, PortalInside = 1f, PortalOutside = 2f, PortalAll = 3f;
        // 輪が目からこの距離 (m) より近い間は、輪の中かどうかで描き分けずに、星空を全部描く。
        // (輪の面が目のすぐそばを通る時、左右の目・頭の動きで「輪の中を通して見ているか」の判定が1フレームだけ入れ替わり、
        //  元の世界が一瞬ちかっと見えてしまうため。輪はとても大きいので、近い間は視界がもともと全部星空で、見た目は変わらない)
        const float PortalNear = 0.8f;

        void Awake()
        {
            if (groundParts == null) groundParts = new Transform[0];
            groundWasActive = new bool[groundParts.Length];
            if (objects == null) objects = new UnknownObject[0];
            if (gates == null) gates = new Transform[0];
            foreach (var o in objects) if (o != null) o.gameObject.SetActive(false);
            gateOpen = new bool[gates.Length];
            gateScale = new Vector3[gates.Length];
            for (int i = 0; i < gates.Length; i++)
            {
                gateScale[i] = gates[i].localScale;
                MeshMerger.Merge(gates[i], null); // ゲートと奥の世界は、ゲートごとに材質ごとにまとめる (ゲートごと大きさを変えて開くので)
                foreach (var r in gates[i].GetComponentsInChildren<Renderer>(true))
                {
                    var tm = r.GetComponent<TextMesh>();
                    if (tm != null) gateTexts.Add((tm, tm.color));
                    else if (r.sharedMaterial != null && r.sharedMaterial.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent) gateSeeThrough.Add(r);
                }
            }
            if (bigHole != null)
            {
                holeScale = bigHole.localScale;
                MeshMerger.Merge(bigHole, null);
                bigHole.gameObject.SetActive(false);
            }
            if (cosmos != null)
            {
                MeshMerger.Merge(cosmos.transform, null);
                cosmos.SetActive(false);
            }
            SetPortal(Vector3.zero, Vector3.forward, 0f, PortalNone);
            if (space != null)
            {
                var keep = new HashSet<Transform>();
                foreach (var g in gates) keep.Add(g);
                if (floor != null) keep.Add(floor.transform);
                MeshMerger.Merge(space.transform, keep); // 動かない飾りは材質ごとに1つにまとめる (回る飾りはスクリプト付きなのでまとめない)
                space.SetActive(false);
            }
            grayExcludeSet = new HashSet<Transform> { transform };
            if (grayExclude != null) foreach (var t in grayExclude) if (t != null) grayExcludeSet.Add(t);
            shatter = new GameObject("WorldShatter").AddComponent<WorldShatter>();
            shatter.transform.SetParent(transform, false);
            BuildShell();
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.playOnAwake = false;
            bgm.loop = true;
            bgm.spatialBlend = 0f;
            bgm.volume = 0f;
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            if (solidMat != null) Destroy(solidMat);
        }

        /// <summary>
        /// Unknown ステージを始める。yaw = 割れ始める向き (度。撃った的の方)。delay 秒たってから (屋台の的がしまわれてから) 世界が止まる。
        /// breakables = 割れる部品に入れ替える物 (出ている屋台・ハッチ)
        /// </summary>
        public void Begin(float yaw, float delay, IList<Transform> breakables)
        {
            if (IsActive) return;
            IsActive = true;
            startTime = Time.time;
            Hits = 0;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            roots.Clear();
            if (breakables != null) roots.AddRange(breakables);
            foreach (var g in groundParts) if (g != null && (groundRenderer == null || g != groundRenderer.transform)) roots.Add(g); // 穴のふちも一緒に割る
            Play(Enter(delay));
        }

        /// <summary>時間切れ: 部品が戻ってきて元の世界に戻る。immediate なら、その場ですぐに全部しまう (ラウンドの終わり・リセット)</summary>
        public void End(bool immediate = false)
        {
            if (!IsActive && !immediate) return;
            IsActive = false;
            inside = false;
            if (gateRoutine != null) { StopCoroutine(gateRoutine); gateRoutine = null; }
            if (holeRoutine != null) { StopCoroutine(holeRoutine); holeRoutine = null; }
            if (bigHole != null) bigHole.gameObject.SetActive(false);
            foreach (var o in objects) if (o != null) o.Retreat();
            bgmTarget = 0f;
            if (immediate)
            {
                if (anim != null) StopCoroutine(anim);
                anim = null;
                if (buildRoutine != null) { StopCoroutine(buildRoutine); buildRoutine = null; }
                foreach (var o in objects) if (o != null) o.HideNow();
                if (bgm != null) { bgm.Stop(); bgm.volume = 0f; }
                if (gray.Active) gray.Restore();
                shatter.Clear();
                SetSolid(false);
                shell.gameObject.SetActive(false);
                SetSpace(false);
                Left?.Invoke(true);
                Closed?.Invoke();
                return;
            }
            Play(Leave());
        }

        void Update()
        {
            if (bgm != null && bgm.isPlaying)
            {
                bgm.volume = Mathf.MoveTowards(bgm.volume, bgmTarget, Time.deltaTime * bgmVolume / 1.2f);
                if (bgmTarget <= 0f && bgm.volume <= 0f) bgm.Stop();
            }
            if (!IsActive || !inside || Time.time < nextLaunch || TimeLeft < 2.5f) return;
            nextLaunch = Time.time + Random.Range(launchInterval.x, launchInterval.y);
            int flying = 0;
            free.Clear();
            foreach (var o in objects)
            {
                if (o == null) continue;
                if (o.IsFlying) flying++;
                else if (!o.IsBusy && (o.gateIndex < 0 || (o.gateIndex < gates.Length && gateOpen[o.gateIndex]))) free.Add(o); // (gateIndex がマイナスの物 = 時計・ドアは、いつでもその場に現れる)
            }
            if (flying >= maxFlying || free.Count == 0) return;
            Launch(free[Random.Range(0, free.Count)]);
        }

        // ゲートの奥の世界から出てきて、入り口をくぐり、プレイヤーのまわりへ来る (時計・ドアは、プレイヤーのまわりにその場で現れる)
        // 漂う所の高さの基準 (ふしぎな空間の床の高さ = このステージの高さ)。世界の原点 (y = 0) ではなく、床から測る
        // (世界の高さがずれていると、止まる所が床より下になり、ゲートから出た物がゆっくり床下へ沈んでいってしまうので)
        float FloorY => transform.position.y;

        // 止まる所の高さ: 床から hoverHeight の範囲 (床より下にはしない)
        float HoverY() => FloorY + Mathf.Max(0.5f, Random.Range(hoverHeight.x, hoverHeight.y));

        void Launch(UnknownObject o)
        {
            var head = HeadPosition();
            if (o.gateIndex < 0)
            {
                var around = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(hoverDistance.x, hoverDistance.y);
                var spot = new Vector3(head.x, 0f, head.z) + around;
                spot.y = HoverY();
                o.Launch(spot, spot, spot, 0.01f, Random.Range(hoverTime.x, hoverTime.y) + 1f, OnObjectDown);
                return;
            }
            var g = gates[o.gateIndex];
            // ゲートの奥の景色は、プレイヤーの目から入り口を通して見える範囲 (三角の筒) にしかないので、出てくる所もその中から選ぶ
            var eye = g.InverseTransformPoint(head);
            float z = Random.Range(10f, 24f), s = (z - eye.z) / Mathf.Max(0.1f, -eye.z);
            float u = Random.Range(-1.1f, 1.1f), v = Random.Range(-0.5f, 0.9f);
            var start = g.TransformPoint(new Vector3(eye.x + (u - eye.x) * s, eye.y + (v - eye.y) * s, z));
            var mouth = g.TransformPoint(new Vector3(u * 0.5f, v * 0.5f - 0.2f, 0f));
            var flatHead = new Vector3(head.x, 0f, head.z);
            var toGate = g.position - flatHead;
            toGate.y = 0f;
            var dir = Quaternion.Euler(0f, Random.Range(-55f, 55f), 0f) * (toGate.sqrMagnitude > 0.01f ? toGate.normalized : Vector3.forward);
            var dest = flatHead + dir * Random.Range(hoverDistance.x, hoverDistance.y);
            dest.y = HoverY();
            o.Launch(start, mouth, dest, travelTime, Random.Range(hoverTime.x, hoverTime.y), OnObjectDown);
        }

        void OnObjectDown(UnknownObject o)
        {
            Hits++;
            ObjectDown?.Invoke(o);
        }

        // ---- 行き ----
        IEnumerator Enter(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            // 1. 世界がその場で灰色に止まる (キーン。本物の立体のまま)。ひびが広がっている間に、空間を破片に切り分けておく (少しずつ)
            var head = HeadPosition();
            shell.SetPositionAndRotation(head, Quaternion.identity);
            ResetShards();
            gray.Apply(WorldRoot(), grayExcludeSet);
            SetAmbienceGray(1f, RenderSettings.fogColor, RenderSettings.ambientLight, CameraBackground());
            graySky = CameraBackground();
            buildRoutine = StartCoroutine(shatter.Build(roots, groundRenderer, head));
            SetSolid(false);
            SetLook(0f, 0.7f, 1f);
            shell.gameObject.SetActive(true);
            ScreenFader.FadeTo(0.45f, 0.02f, new Color(0.8f, 0.8f, 0.82f)); // (灰色になる一瞬の引っかかりを、灰色の光で隠す)
            Sfx.Play("freeze", SfxVolume);
            yield return null;
            ScreenFader.FadeTo(0f, 0.4f);
            yield return new WaitForSeconds(grayHold);

            // 2. 空中に透明なガラスのひびが広がる (ピキピキ…)
            Sfx.Play("crack", SfxVolume);
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, crackTime))
            {
                SetLook(t * t, 0.7f, 1f); // だんだん速く
                yield return null;
            }
            SetLook(1f, 0.7f, 1f);
            while (!shatter.Ready) yield return null; // (切り分けが終わっていなければ、ひびが入りきったまま待つ)
            buildRoutine = null;

            // 3. 割れる: 本物を同じ形の灰色の破片と入れ替え、撃った的の方から、ガラスと空間の破片が立体のまま散らばる (ガシャーン)。向こうは ふしぎな空間
            //    ガラスの破片は透けずに、灰色に止まった世界の空の色になる (割れて抜けた所にだけ、ふしぎな空間が見える)
            shatter.Show();
            SetSolid(true);
            Swapped?.Invoke();
            SetGround(false);
            gray.Restore();
            SetSpace(true);
            Shattered?.Invoke();
            ScreenFader.FadeTo(0.3f, 0.02f, Color.white); // (入れ替えた瞬間の明かりの変わり目を、割れる光で隠す)
            Sfx.Play("shatter", SfxVolume);
            yield return null;
            ScreenFader.FadeTo(0f, 0.35f);
            float total = 1.4f + glassFlyTime;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                PoseShards(t); // (ガラスと、その奥の空間の破片をいっしょに動かす)
                yield return null;
            }
            shell.gameObject.SetActive(false);

            // 4. ふしぎな空間で遊ぶ: ゲートが順に開き、物が紛れ込んでくる
            inside = true;
            nextLaunch = Time.time + gateOpenTime + 0.2f;
            // (曲は GameManager が音源のファイルを流す。ここで作った曲 (DreamLoop) は流さない)
            gateRoutine = StartCoroutine(OpenGates());
            holeRoutine = StartCoroutine(BigHoleEvent());
            Arrived?.Invoke();
            anim = null;
        }

        // 正面に近いゲートから順に、ぽんと開く
        IEnumerator OpenGates()
        {
            var order = new List<int>();
            for (int i = 0; i < gates.Length; i++) order.Add(i);
            order.Sort((a, b) => GateAngle(a).CompareTo(GateAngle(b)));
            foreach (int i in order)
            {
                StartCoroutine(OpenGate(i));
                yield return new WaitForSeconds(gateInterval);
            }
            gateRoutine = null;
        }

        float GateAngle(int i)
        {
            var p = transform.InverseTransformPoint(gates[i].position);
            return Mathf.Abs(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg);
        }

        IEnumerator OpenGate(int i)
        {
            Sfx.Play("warpin", 0.6f * SfxVolume);
            gates[i].gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, gateOpenTime))
            {
                gates[i].localScale = gateScale[i] * Mathf.Max(0.001f, EaseOutBack(t));
                if (t > 0.5f) gateOpen[i] = true;
                yield return null;
            }
            gates[i].localScale = gateScale[i];
            gateOpen[i] = true;
        }

        // ---- 大きなワープホール ----

        // 空に大きなホールが開き、こちらへ迫ってきて、プレイヤーがその輪をくぐる (枠くぐり)。床・飾り・ゲート・的は、そのまま何も変わらない。
        // 変わるのは背景だけ: ホールの向こうの背景 (雲海の上の星空。本物の遠い場所に置いてある) は、輪を通して見える所にだけ描く
        // (Shaders/UnknownCosmos.shader)。輪が近づくほど、輪の中の星空が視界に広がり、くぐった瞬間に背景が全部星空になる。
        // くぐった後は逆に、遠ざかる輪の中にだけ、元の ふしぎな空間 の空が見える。輪が閉じると、背景は星空だけ
        IEnumerator BigHoleEvent()
        {
            yield return new WaitForSeconds(holeDelay);
            if (!inside || bigHole == null) { holeRoutine = null; yield break; }

            // 1. 正面の空に、プレイヤーの方を向いて開く (輪の中に、向こうの星空が見える)
            var head = HeadPosition();
            var dir = Quaternion.AngleAxis(-holeElevation, transform.right) * transform.forward;
            bigHole.SetPositionAndRotation(head + dir * holeDistance, Quaternion.LookRotation(dir)); // +Z が奥 (プレイヤーから見て向こう)
            bigHole.localScale = holeScale * 0.001f;
            bigHole.gameObject.SetActive(true);
            if (cosmos != null) cosmos.SetActive(true);
            Sfx.Play("warpin", SfxVolume);
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, holeOpenTime))
            {
                float s = Mathf.Max(0.001f, EaseOutBack(t));
                bigHole.localScale = holeScale * s;
                SetPortal(bigHole.position, dir, holeRadius * s, PortalInside);
                yield return null;
            }
            bigHole.localScale = holeScale;
            SetPortal(bigHole.position, dir, holeRadius, PortalInside);
            yield return new WaitForSeconds(holeHoldTime);

            // 2. だんだん速くこちらへ迫ってくる (ゴォォ…)。輪の中の星空が、視界いっぱいに広がっていく
            Sfx.Play("sandstorm", SfxVolume);
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, holeApproachTime))
            {
                bigHole.position = head + dir * (holeDistance * (1f - t * t));
                float ahead = holeDistance * (1f - t * t);
                SetPortal(bigHole.position, dir, holeRadius, ahead < PortalNear ? PortalAll : PortalInside); // (目のすぐ近くでは、全部星空に)
                yield return null;
            }

            // 3. くぐった: 背景はもう星空。輪は同じ速さのまま後ろへ抜けていき、だんだんゆっくり、小さくなって閉じる
            //    (遠ざかる輪の中にだけ、元の空が見える)
            Sfx.Play("warpout", SfxVolume);
            float passDistance = holeDistance * holeCloseTime / Mathf.Max(0.05f, holeApproachTime); // (くぐる時の速さが、前と後でそろう距離)
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, holeCloseTime))
            {
                float s = Mathf.Max(0.001f, 1f - t * t * t);
                float behind = passDistance * (1f - (1f - t) * (1f - t));
                bigHole.position = head - dir * behind;
                bigHole.localScale = holeScale * s;
                SetPortal(bigHole.position, dir, holeRadius * s, behind < PortalNear ? PortalAll : PortalOutside); // (目のすぐ近くでは、全部星空に)
                yield return null;
            }
            bigHole.gameObject.SetActive(false);
            SetPortal(Vector3.zero, dir, 0f, PortalAll);
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = cosmosSky; // (星空の球が全部おおっているので、見た目は変わらない)
            // (宇宙ステージのようなキラキラの音は鳴らさない。ユーザーの指定)
            holeRoutine = null;
        }

        // ホールの向こうの背景を描く所を決める (center = 輪の真ん中、dir = くぐる前のプレイヤーから見て奥の向き、mode = Portal*)
        static void SetPortal(Vector3 center, Vector3 dir, float radius, float mode)
        {
            Shader.SetGlobalVector(PortalId, new Vector4(center.x, center.y, center.z, radius));
            Shader.SetGlobalVector(PortalDirId, new Vector4(dir.x, dir.y, dir.z, mode));
        }

        // ---- 帰り ----
        IEnumerator Leave()
        {
            // (まだ割れる前に終わった時: 切り分けをやめて、作りかけの破片は捨てる。本物はまだ出たまま)
            if (buildRoutine != null) { StopCoroutine(buildRoutine); buildRoutine = null; }
            bool shattered = shatter.Shown;
            if (!shattered) shatter.Clear();
            // 1. 散らばった破片とガラスが、逆回しのように遠くから飛んで戻ってくる (最後に割れた物から。カシャ、カシャ…)
            //    ガラスの破片は灰色の空の色 (組み上がると、灰色の世界の景色になる)。帰りはひびを描かない (破片がきれいにつながって見えるように)
            SetSolid(shattered);
            SetLook(0f, 0f, 1f);
            // ゲートは閉じずに開いたまま (縮めると奥の世界の奥行きが見えてしまうので)。ただし半透明の部品は、灰色の破片より後に描かれて
            // 透けて見えてしまうので消す (膜や星雲はうっすらなので、すぐ消す。名前の文字はすっと薄くして消す)
            for (int i = 0; i < gates.Length; i++) gateOpen[i] = false;
            foreach (var r in gateSeeThrough) if (r != null) r.enabled = false;
            shell.gameObject.SetActive(true);
            Sfx.Play("reassemble", SfxVolume);
            float total = returnSpread + returnTime;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                PoseShardsReturning(t);
                SetGateTextAlpha(1f - Mathf.Clamp01(t / 0.3f));
                yield return null;
            }
            SetGateTextAlpha(0f);
            ResetShards();
            shatter.Home();
            Sfx.Play("click", SfxVolume);

            // 2. 灰色の空間が組み上がった: ふしぎな空間をしまい、破片を本物の屋台・ハッチ・地面と入れ替える (本物も灰色にして、見た目は同じ)
            foreach (var o in objects) if (o != null) o.HideNow();
            SetSpace(false);
            Left?.Invoke(false); // GameManager がその場ですぐに屋台・ハッチを出し、いつもの明るさにする
            Color fogTo = RenderSettings.fogColor, ambientTo = RenderSettings.ambientLight, skyTo = CameraBackground();
            gray.Apply(WorldRoot(), grayExcludeSet);
            SetAmbienceGray(1f, fogTo, ambientTo, skyTo);
            shatter.Clear();
            // 後ろはもう本物の灰色の世界なので、ガラスは透ける見た目に戻す。張り合わさったこの瞬間に、屋台より手前のガラスに細いひびが見える
            // (破片が飛んでいる間は描かない。ガラスの破片の上に描くと、手前に組み上がる屋台の破片の奥に見えてしまうので)
            SetSolid(false);
            SetLook(thinCrack, 0f, thinCrackFade);
            ScreenFader.FadeTo(0.25f, 0.02f, Color.white); // (入れ替えた瞬間の明かりの変わり目を隠す)
            yield return null;
            ScreenFader.FadeTo(0f, 0.3f);
            yield return new WaitForSeconds(colorHold); // (組み上がった灰色の世界を少し見せてから)

            // 3. 灰色にする強さを少しずつ弱めて、元の色に戻していく (形・明るさは変えず、色だけが戻る)
            //    はじめと終わりはゆっくり、途中もほぼ同じ速さ (急に色づく所がないように、なめらかな曲線と直線の間をとる)
            //    細いひびは、色が戻り始めると同時にすっと消える (キラキラの音は鳴らさない。ユーザーの指定)
            float started = Time.time;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, colorTime))
            {
                float k = 1f - Mathf.Lerp(t, t * t * (3f - 2f * t), 0.5f);
                gray.Set(k);
                SetAmbienceGray(k, fogTo, ambientTo, skyTo);
                if (shell.gameObject.activeSelf)
                {
                    float c = 1f - Mathf.Clamp01((Time.time - started) / Mathf.Max(0.01f, thinCrackOut));
                    SetLook(thinCrack, 0f, thinCrackFade * c);
                    if (c <= 0f) shell.gameObject.SetActive(false);
                }
                yield return null;
            }
            shell.gameObject.SetActive(false);
            // 灰色の複製を元と同じ色にしきってから、元の材質に差し戻す (差し戻した瞬間に何も変わらないように)
            gray.Set(0f);
            SetAmbienceGray(0f, fogTo, ambientTo, skyTo);
            yield return null;
            gray.Restore();
            Closed?.Invoke();
            anim = null;
        }

        Transform WorldRoot() => transform.parent != null ? transform.parent : transform;

        // 霧・環境光・空の色を、k = 1 で灰色、0 で to の色に
        void SetAmbienceGray(float k, Color fogTo, Color ambientTo, Color skyTo)
        {
            RenderSettings.fogColor = Color.Lerp(fogTo, gray.Gray(fogTo), k);
            RenderSettings.ambientLight = Color.Lerp(ambientTo, gray.Gray(ambientTo), k);
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Color.Lerp(skyTo, gray.Gray(skyTo), k);
        }

        static Color CameraBackground()
        {
            var cam = Camera.main;
            return cam != null ? cam.backgroundColor : Color.black;
        }

        // ---- 空中のひび割れガラス ----

        // 球を緯度・経度で割り、マス目を斜めに切って三角の破片にする (格子の点を少しずらして、割れたガラスのように不揃いに)
        void BuildShell()
        {
            var go = new GameObject("UnknownGlass");
            go.transform.SetParent(transform, false);
            shell = go.transform;
            mat = shardMaterial != null ? new Material(shardMaterial) : new Material(Shader.Find("ShootingGallery/Unknown Shard"));
            var solidShader = shardSolidMaterial != null ? shardSolidMaterial.shader : Shader.Find("ShootingGallery/Unknown Shard Solid");
            if (solidShader != null) solidMat = shardSolidMaterial != null ? new Material(shardSolidMaterial) : new Material(solidShader);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            shellRenderer = mr;

            var rand = new System.Random(20261016);
            float Jit(float range) => ((float)rand.NextDouble() * 2f - 1f) * range;
            var grid = new Vector3[rows + 1, cols];
            for (int r = 0; r <= rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float lat = -90f + 180f * r / rows, lon = 360f * c / cols;
                    if (r > 0 && r < rows) { lat += Jit(0.32f * 180f / rows); lon += Jit(0.32f * 360f / cols); }
                    float la = lat * Mathf.Deg2Rad, lo = lon * Mathf.Deg2Rad;
                    grid[r, c] = new Vector3(Mathf.Cos(la) * Mathf.Sin(lo), Mathf.Sin(la), Mathf.Cos(la) * Mathf.Cos(lo));
                }
            var tris = new List<(Vector3, Vector3, Vector3)>();
            var cells = new int[rows * cols][]; // 緯度・経度のマスごとの破片の番号 (空間を破片に切り分ける時に使う)
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int first = tris.Count;
                    Vector3 a = grid[r, c], b = grid[r, (c + 1) % cols], cc = grid[r + 1, (c + 1) % cols], d = grid[r + 1, c];
                    if (r == 0) tris.Add((a, cc, d));             // 下の極のまわり (a = b = 極)
                    else if (r == rows - 1) tris.Add((a, b, d));  // 上の極のまわり (c = d = 極)
                    else if (rand.NextDouble() < 0.5) { tris.Add((a, b, cc)); tris.Add((a, cc, d)); }
                    else { tris.Add((a, b, d)); tris.Add((b, cc, d)); }
                    cells[r * cols + c] = tris.Count - first == 1 ? new[] { first } : new[] { first, first + 1 };
                }
            // 空間の切り方は、ガラスのひびの線と同じにする (頭から、ガラスの破片の3つの角を通る面で切る)
            var conePoints = new List<(Vector3 a, Vector3 b, Vector3 c)>(tris.Count);
            foreach (var (ta, tb, tc) in tris) conePoints.Add((OnShell(ta), OnShell(tb), OnShell(tc)));
            shatter.SetCones(conePoints, cells, rows, cols, shellRadius, shellDown);
            pieceRot = new Quaternion[tris.Count];
            pieceScale = new float[tris.Count];
            pieceVanish = new float[tris.Count];

            // 破片 1枚 = ふちの3点 + 真ん中の1点 (ひびの線のために、ふちを 1、真ん中を 0 にした値を頂点の色の a に入れる)
            int n = tris.Count;
            shards = new Shard[n];
            localVerts = new Vector3[n * 4];
            verts = new Vector3[n * 4];
            var colors = new Color[n * 4];
            var indices = new int[n * 9];
            for (int i = 0; i < n; i++)
            {
                var (a, b, c) = tris[i];
                Vector3 pa = OnShell(a), pb = OnShell(b), pc = OnShell(c);
                Vector3 center = (pa + pb + pc) / 3f;
                int v = i * 4;
                localVerts[v] = Vector3.zero;
                localVerts[v + 1] = pa - center;
                localVerts[v + 2] = pb - center;
                localVerts[v + 3] = pc - center;
                colors[v] = new Color(1f, 1f, 1f, 0f);
                colors[v + 1] = colors[v + 2] = colors[v + 3] = Color.white;
                int k = i * 9;
                indices[k] = v; indices[k + 1] = v + 1; indices[k + 2] = v + 2;
                indices[k + 3] = v; indices[k + 4] = v + 2; indices[k + 5] = v + 3;
                indices[k + 6] = v; indices[k + 7] = v + 3; indices[k + 8] = v + 1;
                shards[i] = new Shard { home = center, axis = center.normalized, first = v };
            }
            mesh = new Mesh { name = "UnknownGlassShards" };
            mesh.MarkDynamic();
            for (int i = 0; i < n; i++)
                for (int j = 0; j < 4; j++) verts[shards[i].first + j] = shards[i].home + localVerts[shards[i].first + j];
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = indices;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f); // 飛び散っても消えないように
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            // 割れた後に、ガラスの破片の所の奥行きを一番奥に戻す膜 (同じ形。立体の破片をガラスの前に見せるため)
            var depthShader = shardDepthMaterial != null ? shardDepthMaterial.shader : Shader.Find("ShootingGallery/Unknown Shard Depth");
            if (solidMat != null && depthShader != null)
            {
                var dgo = new GameObject("UnknownGlassDepth");
                dgo.transform.SetParent(shell, false);
                dgo.AddComponent<MeshFilter>().sharedMesh = mesh;
                depthRenderer = dgo.AddComponent<MeshRenderer>();
                depthRenderer.sharedMaterial = shardDepthMaterial != null ? shardDepthMaterial : new Material(depthShader);
                depthRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                depthRenderer.receiveShadows = false;
                depthRenderer.enabled = false;
            }
            go.SetActive(false);
        }

        // ガラスの見た目: 割れる前・組み上がった後 = 透けるガラス / 割れている間 = 灰色に止まった世界の空の色の破片
        void SetSolid(bool solid)
        {
            if (solidMat == null || depthRenderer == null) return; // (シェーダーがない時は、いつも透けるガラス)
            if (solid) solidMat.SetColor(SkyColorId, graySky);
            shellRenderer.sharedMaterial = solid ? solidMat : mat;
            depthRenderer.enabled = solid;
        }

        // 球の上の点 (下の半分は床にめり込まないように縦に縮める)
        Vector3 OnShell(Vector3 dir) => new Vector3(dir.x * shellRadius, dir.y * (dir.y < 0f ? shellDown : shellRadius), dir.z * shellRadius);

        // 元の形に戻し、割れ方 (剥がれる順番・ずれていく向き・回り方) を決め直す。正面 (撃った的の方) から割れ始め、外側へ広がる
        void ResetShards()
        {
            var front = transform.forward;
            for (int i = 0; i < shards.Length; i++)
            {
                var s = shards[i];
                var a = s.axis;
                s.delay = Vector3.Angle(front, a) / 180f * 0.9f + Random.Range(0f, 0.12f);
                // 目から見て、一片がずれていく向き: 下へ (落ちる) + 正面から外へ (割れ目が広がる)。少しずつばらつかせる
                var away = a - front * Vector3.Dot(a, front);
                var d = Vector3.down + away * 0.8f + Random.insideUnitSphere * 0.25f;
                var tangent = d - a * Vector3.Dot(a, d);
                if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.Cross(a, Vector3.right);
                s.fallAxis = Vector3.Cross(a, tangent).normalized; // (この軸で正に回すと、a が tangent の方へ動く)
                // 回すと a は a·cosθ + t·sinθ の円をたどる。高さ a.y·cosθ + t.y·sinθ が一番低くなる角度 (atan2(t.y, a.y) + 180°) までにとどめる
                var t1 = tangent.normalized;
                s.maxFall = Mathf.Repeat(Mathf.Atan2(t1.y, a.y) * Mathf.Rad2Deg + 180f, 360f);
                s.fallSpeed = Random.Range(fallSpeed.x, fallSpeed.y);
                s.fallAccel = Random.Range(fallAccel.x, fallAccel.y);
                s.spin = Random.Range(pieceSpin.x, pieceSpin.y) * (Random.value < 0.5f ? -1f : 1f);
                shards[i] = s;
                PoseShard(i, 0f);
            }
            mesh.vertices = verts;
        }

        // 割れ始めてから t 秒の形。1つの一片の動きは剥がれ始めてからの時間だけで決まる (帰りは同じ道を逆にたどる)
        void PoseShards(float t)
        {
            for (int i = 0; i < shards.Length; i++) PoseShard(i, Mathf.Clamp(t - shards[i].delay, 0f, glassFlyTime));
            ApplyPieces();
        }

        // 帰りの t 秒: 最後に割れた一片から順に、遠くから元の場所へ戻る (はじめ速く、はまる直前にゆっくり)
        void PoseShardsReturning(float t)
        {
            for (int i = 0; i < shards.Length; i++)
            {
                float start = (1f - shards[i].delay) * returnSpread;
                float u = Mathf.Clamp01((t - start) / Mathf.Max(0.05f, returnTime));
                float e = 1f - (1f - u) * (1f - u) * (1f - u);
                PoseShard(i, glassFlyTime * (1f - e));
            }
            ApplyPieces();
        }

        // 剥がれ始めてから ft 秒の一片 i: 頭を中心に回って (目から見て、割れた景色の一片が下・外へずれて落ちていく)、
        // 自分の向きのまわりにも少し回り、頭から遠ざかる (目から見た形は変わらないまま奥へ離れる)。終わり近くで小さくなって消える。
        // どれも頭を中心にした動きなので、奥にある屋台・地面の破片も、目から見てガラスの一片とぴったりいっしょに動く
        void PoseShard(int i, float ft)
        {
            var s = shards[i];
            float angle = s.fallSpeed * ft + 0.5f * s.fallAccel * ft * ft;
            // 落ちるだけ: 一番低い所 (maxFall) に近づくほどゆっくりになり、そこを越えない (越えると、足元を回って舞い上がって見える)
            if (s.maxFall > 0.01f) angle = s.maxFall * (1f - Mathf.Exp(-angle / s.maxFall));
            else angle = 0f;
            var rot = Quaternion.AngleAxis(angle, s.fallAxis) * Quaternion.AngleAxis(s.spin * ft, s.axis);
            float scale = 1f + recede * ft * ft;
            float k = ft / Mathf.Max(0.01f, glassFlyTime);
            float vanish = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, k));
            pieceRot[i] = rot;
            pieceScale[i] = scale;
            pieceVanish[i] = vanish;
            var center = rot * (s.home * scale);
            for (int j = 0; j < 4; j++) verts[s.first + j] = center + rot * (localVerts[s.first + j] * (scale * vanish));
        }

        // ガラスの形を書き直し、奥の空間の破片も同じ一片の動きで置く
        void ApplyPieces()
        {
            mesh.vertices = verts;
            shatter.ApplyPieces(pieceRot, pieceScale, pieceVanish);
        }
        void SetLook(float crack, float crackGlow, float fade)
        {
            mat.SetFloat(CrackId, crack);
            mat.SetFloat(CrackGlowId, crackGlow);
            mat.SetFloat(FadeId, fade);
            if (solidMat != null)
            {
                solidMat.SetFloat(CrackId, crack);
                solidMat.SetFloat(CrackGlowId, crackGlow);
            }
        }

        // ふしぎな空間を出す / しまう (ゲートは閉じた状態に)。いつもの床は、空間を出す時にはもう消えている (割れる部品に入れ替え済み)
        void SetSpace(bool on)
        {
            if (space != null) space.SetActive(on);
            // 背景を ふしぎな空間 に戻しておく
            if (bigHole != null) bigHole.gameObject.SetActive(false);
            if (cosmos != null) cosmos.SetActive(false);
            SetPortal(Vector3.zero, Vector3.forward, 0f, PortalNone);
            for (int i = 0; i < gates.Length; i++)
            {
                gateOpen[i] = false;
                gates[i].localScale = gateScale[i] * 0.001f;
                gates[i].gameObject.SetActive(false);
            }
            // 帰りに消したゲートの半透明の部品と文字を戻しておく (次に開く時のために)
            foreach (var r in gateSeeThrough) if (r != null) r.enabled = true;
            SetGateTextAlpha(1f);
            if (!on) SetGround(true);
        }

        // ゲートの名前の文字の濃さ (1 = 元の色)
        void SetGateTextAlpha(float a)
        {
            foreach (var (text, color) in gateTexts)
                if (text != null) text.color = new Color(color.r, color.g, color.b, color.a * a);
        }

        // いつもの床の部品 (地面・穴のふち) を出す / 消す
        void SetGround(bool show)
        {
            for (int i = 0; i < groundParts.Length; i++)
            {
                if (groundParts[i] == null) continue;
                if (!show)
                {
                    if (groundParts[i].gameObject.activeSelf) { groundWasActive[i] = true; groundParts[i].gameObject.SetActive(false); }
                }
                else if (groundWasActive[i])
                {
                    groundParts[i].gameObject.SetActive(true);
                    groundWasActive[i] = false;
                }
            }
        }

        static Vector3 HeadPosition()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : new Vector3(0f, 1.6f, 0f);
        }

        static float EaseOutBack(float t)
        {
            const float c = 1.7f;
            t = Mathf.Clamp01(t) - 1f;
            return 1f + (c + 1f) * t * t * t + c * t * t;
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
