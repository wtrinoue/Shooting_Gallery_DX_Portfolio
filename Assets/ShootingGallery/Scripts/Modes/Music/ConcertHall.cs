using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 音楽モード (四分音符の的を撃つと始まる)。
    ///   1. 頭の少し上に濃い灰色の円い天井とレールが現れ、赤いカーテンがぐるりと 0 度 → 360 度と引かれてプレイヤーを囲む。
    ///   2. 閉まりきったら (Covered)、その外で屋台をしまってコンサートホールを出す (ステージ・合奏団の楽器・客席)。
    ///   3. カーテンが逆向きに開いていき、天井・レールごと縮んで消える (Opened)。プレイヤーは客席の真ん中の少しステージ寄りに座っている。
    ///   4. 薄暗いホールがぱっと明るくなり (MusicStarted)、明るくなりきった瞬間に演奏が始まって、楽器が曲に合わせて揺れる。
    ///   的: ソロを弾いている楽器 (スポットライトが当たり、点数の札が出ている)。撃つと跳ねて点数が入り、空中に波打つ五線譜が流れてくる。
    ///       五線譜を流れてくる音符も的 (撃つとその高さの音が鳴る)。ソロでない楽器は撃つと跳ねて音が鳴るだけ。
    ///   終わる時は行きと同じ: 演奏が止まって暗くなり、カーテンが閉まって (CoveredBack) ホールが消え、開くと元の屋台の前。
    /// </summary>
    public class ConcertHall : MonoBehaviour
    {
        [Header("ホール (始まるまで隠しておく)")]
        public GameObject hall;
        public StageCurtain curtain;
        [Tooltip("カーテンの秒数: 天井とレールが現れる / カーテンが閉まる / 閉まったまま待つ (始まる時) / 開く / 天井ごと消える")]
        public float appearTime = 0.9f;
        public float curtainCloseSeconds = 1.8f; // 名前を変えて、シーンに保存された前の値 (closeTime 2 / closeSeconds 1.2) を使わない
        public float holdSeconds = 1.5f;  // 同じく (holdTime 0.3)
        public float curtainOpenSeconds = 2.8f; // 同じく (openTime 2)
        public float vanishTime = 0.9f;
        [Tooltip("カーテンが消えてから、ステージが明るくなって演奏が始まるまでの暗い時間 (秒)")]
        public float darkSeconds = 3f; // 前は 0.8 秒 (コードの中に直接)
        [Range(0f, 1f), Tooltip("カーテンが動く音の大きさ (拍手が聞こえるように小さめ)")]
        public float curtainVolume = 0.3f; // 前は 0.7

        [Header("浮き床 (カーテンが閉まっている間に、その場でプレイヤーを床ごと客席の上の高さに置き直す。動く様子は見せない。ステージ全体が見えるように)")]
        [Tooltip("持ち上げる物 (カーテンと浮き床) / 浮き床 (的を撃った時から、帰りにカーテンが開ききるまで出す)")]
        public Transform lift;
        public GameObject platform;
        public float liftHeight = 4.2f;
        [Tooltip("いつもの地面の高さにある間 (行きにカーテンが閉まるまで・帰りにカーテンが開ききるまで)、浮き床を持ち上げておく高さ (m。地面に埋もれて見えなくならないように)")]
        public float platformGroundRaise = 0.08f;

        [Header("照明")]
        [Tooltip("ステージを照らす明かり (演奏前は dimLevel の明るさ、演奏中はビルダーで決めた明るさの playBright 倍)")]
        public Light[] stageLights;
        [Range(0f, 1f)] public float dimLevel = 0.12f;
        public float playBright = 2.6f; // (客席を暗くするためにホールの環境光を下げた分、ステージは明るく。前は 1.8)
        [Tooltip("客席を照らす明かり (演奏中はビルダーで決めた明るさの houseDuringPlay 倍。1 より小さいと、演奏中は客席が暗くなってステージが目立つ)")]
        public Light houseLight;
        public float houseDuringPlay = 0.35f; // 名前を変えて、シーンに保存された前の値 (housePlay 1.6) を使わない
        [Tooltip("ソロの楽器を照らすスポットライトと、その光の筋 (光と同じ向きに伸ばす。長さ 1 の円すい)。ソロ1つに1台ずつ、その楽器の斜め上前に固定して照らす。ソロの数より少なければ、始める時に1台目を複製して足す")]
        public Light[] followSpots;
        public Transform[] followBeams;
        [Tooltip("スポットライトを置く所: ソロの楽器から客席の方へ何 m 手前か (高さは天井近く = ビルダーで置いた高さ)")]
        public float spotFront = 2.5f;
        [Tooltip("演奏中だけ点ける物 (ステージの縁の明かり・光の筋など)")]
        public GameObject[] showWhilePlaying;
        [Tooltip("演奏が始まると同時に、ステージがゆっくり明るくなっていく秒数")]
        public float lightRiseSeconds = 4f; // 名前を変えて、シーンに保存された前の値 (lightUpTime 1.5 / lightUpSeconds 0.25) を使わない
        [Tooltip("楽器の動きの大きさ: 演奏が始まった時 (0〜1) / 最高潮 (1) になるまでの秒数。あとはずっと最高潮")]
        [Range(0f, 1f)] public float grooveStart = 0.2f;
        public float grooveRiseSeconds = 8f;

        [Header("楽器")]
        public MusicInstrument[] instruments;
        [Tooltip("同時にソロを弾く楽器の数 / 1つの楽器がソロを続ける拍の数")]
        public int soloSlots = 5; // 名前を変えて、シーンに保存された前の値 (soloCount 2) を使わない
        public float soloBeats = 16f;
        [Tooltip("ソロの楽器を撃ちやすいように当たり判定を消す範囲: ソロの楽器からの距離 (m) / プレイヤーからソロの楽器への視線からの距離 (m)")]
        public float blockRadius = 3f;
        public float sightClearance = 1.2f;
        [Tooltip("指揮台の上で4拍子を振る指揮棒 (指揮者はいない)。先が4拍子の図形 (1 真下 → 2 左 → 3 右 → 4 上) を描き、光の尾を引く")]
        public Transform baton;
        [Tooltip("指揮棒の大きさ (ビルダーで作った大きさの倍率) / 図形の幅と高さ (m)")]
        public float batonScale = 1.7f;
        public Vector2 batonPatternSize = new Vector2(0.62f, 0.72f);
        [Tooltip("演奏が始まる前に指揮棒を振り上げる秒数 (振り上げきった所から振り下ろすと演奏が始まる)")]
        public float batonRaiseSeconds = 1.1f;
        [Tooltip("振り上げたあと、振り下ろして演奏を始めるまで、いったん静かに止まっている秒数 (大事な間)")]
        public float batonHoldSeconds = 1.5f;
        [Tooltip("振り下ろす直前に、ポイっと上へ跳ね上げる高さ (m) / 秒数")]
        public float batonFlickHeight = 0.2f;
        public float batonFlickSeconds = 0.16f;
        [Tooltip("カーテンが消えてから、指揮台がスポットライトで照らされるまでの秒数 / 明るくなる秒数")]
        public float podiumSpotDelay = 0.5f;
        public float podiumSpotFade = 0.6f;

        [Header("五線譜")]
        public MusicStaff[] staffs;
        [Tooltip("五線譜を次々に出す間隔 (秒。空いている五線譜がある時)")]
        public float staffEvery = 2.4f; // 名前を変えて、シーンに保存された前の値 (staffInterval 3.2) を使わない
        [Tooltip("五線譜が出てくる所: ステージの左右の端の上空。いちばん奥の z / いちばん手前の z (プレイヤーからの距離) / 左右の x")]
        public float staffStartZ = 16f;
        public float staffNearZ = 10f;
        public float staffEdgeX = 8.4f;
        [Tooltip("五線譜が出てくる高さの範囲 (m。下: ト音記号がステージの床に埋まらない高さ / 上: ト音記号がステージの天井に入らない高さ)")]
        public float staffLowY = 2.3f;
        public float staffHighY = 7.3f;
        [Tooltip("同じ側の五線譜どうしの段 (0〜1) の最小の間隔。これより必ず離れ、それより先はランダム (帯が重ならないように)")]
        public float minStaffLevelGap = 0.3f;

        [Header("音")]
        [Tooltip("演奏中に曲 (BGM) を流すか。流さない時も、楽器の揺れは同じ速さ (曲のテンポ) の拍で動く")]
        public bool bgm = true; // (名前を変えた: 前の playBackgroundMusic = false がシーンに残っていて、BGM が鳴らなかったので)
        [Range(0f, 1f)] public float musicVolume = 0.55f;
        [Range(0f, 1f), Tooltip("演奏の始まりの BGM の音量。introFadeSeconds かけて musicVolume (いつもの音量) へ下げていく")]
        public float introVolume = 0.9f;
        public float introFadeSeconds = 10f;
        [Tooltip("BGM の曲 (空ならコードで作った曲を流す)。今は 魔王魂 の曲 (Assets/Source/music_stage/music_stage.mp3。クレジットは Resources/THIRD_PARTY_NOTICES.txt)")]
        public AudioClip bgmClip;
        [Tooltip("BGM の曲のテンポ (BPM) / 最初の拍の時刻 (秒)。楽器の揺れ・指揮棒をこの拍に合わせる")]
        public float bgmTempo = 133.975f;    // 曲の打点を1拍ずつ測って求めた値 (前は bgmBpm 135 で、70秒で 0.4秒ずれていた)。名前を変えて、シーンに保存された前の値を使わない
        public float bgmBeatOffset = 0.0067f; // 同じく (前は bgmFirstBeat 0.14)。1.798秒 (bgmSkipSeconds の所) が拍の頭になる
        [Tooltip("BGM の曲を、頭から何秒の所から流し始めるか (曲の初めの音のない所を飛ばす)。拍の数え方もこれに合わせるので、楽器の揺れはずれない。曲が終わって頭に戻る時は 0 秒から")]
        public float bgmSkipSeconds = 1.8f; // 魔王魂の曲は頭から 1.8 秒の所から (ユーザー指定)。名前を変えて、シーンに保存された前の値 (bgmStartAt 1.7 / bgmStartSeconds 1) を使わない
        [Tooltip("BGM の曲の調: 主音 (0 = ド, 2 = レ, 5 = ファ …) と短調かどうか。楽器や音符を撃った時の音を、この調の音階に合わせる")]
        public int bgmKeyRoot = 5;
        public bool bgmMinor = true;

        bool UseBgmClip => bgmClip != null;
        AudioClip CurrentBgm => UseBgmClip ? bgmClip : ConcertMusic.Loop;
        float BeatSec => UseBgmClip ? 60f / Mathf.Max(1f, bgmTempo) : ConcertMusic.BeatSeconds;
        float FirstBeat => UseBgmClip ? bgmBeatOffset : 0f;
        // 曲を流し始める拍 (指揮棒の「1」をたたく瞬間に音が出て、そのあとも拍とずれないように)。その拍の曲の時刻から流す。
        // 拍の表があれば bgmSkipSeconds にいちばん近い小節の頭 (表の 1拍目)、なければいちばん近い拍の頭
        float StartBeat
        {
            get
            {
                if (!UseBgmClip) return 0f;
                if (HasBeatMap) return 4f * Mathf.Round(TimeToBeat(bgmSkipSeconds) / 4f);
                return bgmSkipSeconds > 0f ? Mathf.Max(0f, Mathf.Round((bgmSkipSeconds - FirstBeat) / BeatSec)) : 0f;
            }
        }
        float StartAt => UseBgmClip && (HasBeatMap || StartBeat > 0f) ? Mathf.Max(0f, BeatToTime(StartBeat)) : 0f;

        // ---- 拍の表 (人が曲に合わせて打った拍。time,bar,beat の CSV) ----
        // 表があれば、曲の時刻 ⇔ 拍を表から求める (テンポが途中で変わっても、指揮棒・楽器が曲についていく)。なければ bgmTempo / bgmBeatOffset の一定のテンポ。
        // 拍の番号 = (小節 - 1) × 4 + (拍 - 1)。0 が 1小節目の1拍目。指揮棒の「たたく」瞬間は表の時刻そのもの (ならさない。
        // 前は前後3拍の直線でならしていたが、テンポが急に変わる所 (17小節の頭・33小節からのゆっくりになる所) で拍が前後にずれた)。
        // 表の外は、表全体のふつうの1拍の長さ (間隔のまん中の値) で延ばす (最後のゆっくりになる所のテンポで延ばさない)
        [Tooltip("曲に合わせて人が打った拍の表 (time,bar,beat の CSV。Assets/Source/music_stage/music_stage.csv)。空なら bgmTempo の一定のテンポ")]
        public TextAsset beatMap;
        [Tooltip("拍の表の、手の揺れをならす幅 (前後何拍ずつ。0 = 表の時刻そのまま)")]
        public int beatMapSmoothBeats = 0; // 名前を変えて、シーンに保存された前の値 (beatMapSmooth 3) を使わない
        [Tooltip("指揮棒・楽器の拍を、音よりこの秒数だけ遅らせる (+) / 早める (-)。ヘッドセットや Bluetooth の音の遅れを合わせる時に")]
        public float beatOffsetSeconds = 0f;
        [Tooltip("確認用: 演奏中、拍の表の拍ごとにカチッと鳴らす (拍の表と曲が合っているか、指揮棒と音が合っているかを耳で確かめる)")]
        public bool clickOnBeats = false;
        float[] mapBeats, mapTimes;
        float mapHeadSlope, mapTailSlope; // 表の外を延ばすテンポ (秒 / 拍)
        bool HasBeatMap => UseBgmClip && mapTimes != null && mapTimes.Length >= 2;

        void LoadBeatMap()
        {
            mapBeats = mapTimes = null;
            if (beatMap == null) return;
            var beats = new List<float>();
            var times = new List<float>();
            foreach (var raw in beatMap.text.Split('\n'))
            {
                var p = raw.Trim().Split(',');
                if (p.Length < 3) continue;
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                if (!float.TryParse(p[0], System.Globalization.NumberStyles.Float, inv, out float t)) continue; // 見出しの行など
                if (!int.TryParse(p[1], out int bar) || !int.TryParse(p[2], out int beat)) continue;
                float b = (bar - 1) * 4 + (beat - 1);
                if (beats.Count > 0 && (b <= beats[beats.Count - 1] || t <= times[times.Count - 1])) continue; // 戻っている行は使わない
                beats.Add(b);
                times.Add(t);
            }
            if (beats.Count < 2) return;
            int n = beats.Count;
            mapBeats = beats.ToArray();
            mapTimes = new float[n];
            // 表の時刻をそのまま使う (beatMapSmoothBeats > 0 の時だけ、前後その拍数の直線に当てはめてならす)
            for (int k = 0; k < n; k++)
            {
                if (beatMapSmoothBeats <= 0) { mapTimes[k] = times[k]; continue; }
                FitLine(beats, times, Mathf.Max(0, k - beatMapSmoothBeats), Mathf.Min(n - 1, k + beatMapSmoothBeats), out float a, out float s);
                mapTimes[k] = a + s * mapBeats[k];
            }
            for (int k = 1; k < n; k++) mapTimes[k] = Mathf.Max(mapTimes[k], mapTimes[k - 1] + 0.05f); // 念のため、時刻は必ず進む
            // 表の外を延ばすテンポ: 1拍あたりの間隔のまん中の値 (ゆっくりになる所や、打ちそこねた所に引っぱられない)
            var gaps = new List<float>(n - 1);
            for (int k = 1; k < n; k++) gaps.Add((mapTimes[k] - mapTimes[k - 1]) / Mathf.Max(1f, mapBeats[k] - mapBeats[k - 1]));
            gaps.Sort();
            mapHeadSlope = mapTailSlope = Mathf.Max(0.1f, gaps[gaps.Count / 2]);
        }

        // i0〜i1 の点に直線 time = a + s × beat を当てはめる (最小二乗)
        static void FitLine(List<float> beats, List<float> times, int i0, int i1, out float a, out float s)
        {
            double n = 0, sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = i0; i <= i1; i++) { n++; sx += beats[i]; sy += times[i]; sxx += beats[i] * beats[i]; sxy += beats[i] * times[i]; }
            double d = n * sxx - sx * sx;
            s = d > 1e-6 ? (float)((n * sxy - sx * sy) / d) : 0.45f;
            a = (float)((sy - s * sx) / n);
        }

        // 曲の時刻 (秒) → 拍
        float TimeToBeat(float sec)
        {
            if (!HasBeatMap) return (sec - FirstBeat) / BeatSec;
            int n = mapTimes.Length;
            if (sec <= mapTimes[0]) return mapBeats[0] + (sec - mapTimes[0]) / mapHeadSlope;
            if (sec >= mapTimes[n - 1]) return mapBeats[n - 1] + (sec - mapTimes[n - 1]) / mapTailSlope;
            int i = System.Array.BinarySearch(mapTimes, sec);
            if (i >= 0) return mapBeats[i];
            i = ~i; // mapTimes[i - 1] < sec < mapTimes[i]
            return Mathf.Lerp(mapBeats[i - 1], mapBeats[i], (sec - mapTimes[i - 1]) / (mapTimes[i] - mapTimes[i - 1]));
        }

        // 拍 → 曲の時刻 (秒)
        float BeatToTime(float beat)
        {
            if (!HasBeatMap) return FirstBeat + beat * BeatSec;
            int n = mapBeats.Length;
            if (beat <= mapBeats[0]) return mapTimes[0] + (beat - mapBeats[0]) * mapHeadSlope;
            if (beat >= mapBeats[n - 1]) return mapTimes[n - 1] + (beat - mapBeats[n - 1]) * mapTailSlope;
            int i = System.Array.BinarySearch(mapBeats, beat);
            if (i >= 0) return mapTimes[i];
            i = ~i;
            return Mathf.Lerp(mapTimes[i - 1], mapTimes[i], (beat - mapBeats[i - 1]) / (mapBeats[i] - mapBeats[i - 1]));
        }

        // その拍の1拍の長さ (秒)
        float BeatSecAt(float beat) => Mathf.Max(0.05f, BeatToTime(beat + 0.5f) - BeatToTime(beat - 0.5f));

        [Header("軽量化 (WebGL の VR は両目で2回描くので、リアルタイムの明かりと影を減らす)")]
        [Tooltip("ソロ・指揮台のスポットライトを光の筋だけにする (明かりは使わない。指揮台は演奏前の暗い間だけ照らす)。ステージを照らす2灯 (StageWash) も消して、残りの明かりを washMakeUp 倍にして補う")]
        public bool lightweight = true;
        public float washMakeUp = 1.35f;
        [Tooltip("ホールが出ている間は、外の明かりの影を切る")]
        public bool noShadowsInHall = true;
        [Tooltip("同時に出す五線譜の数の上限")]
        public int maxStaffs = 3;

        [Header("時間")]
        [Tooltip("音楽モードの長さ (秒。カーテンが閉まり始めてから)")]
        public float duration = 52f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        public int Hits { get; private set; }
        /// <summary>時間切れから、カーテンが消えて元の屋台が見えるまでの秒数</summary>
        public float CloseTime => 1.2f + appearTime + curtainCloseSeconds + 0.4f + curtainOpenSeconds + vanishTime;

        /// <summary>今の拍 (曲の頭から。演奏していない時もゆっくり進む)</summary>
        public float Beat { get; private set; }
        /// <summary>演奏の盛り上がり (演奏中は 1、始まる前・終わった後は 0)。楽器の揺れの大きさ</summary>
        public float Groove { get; private set; }

        /// <summary>行き: カーテンが閉まりきった (この後ホールが出る。GameManager が屋台をしまう)</summary>
        public event System.Action Covered;
        /// <summary>行き: カーテンが開いて消えた</summary>
        public event System.Action Opened;
        /// <summary>演奏が始まった (ステージが明るくなる)</summary>
        public event System.Action MusicStarted;
        /// <summary>帰り: カーテンが閉まりきってホールが消えた (GameManager が屋台を戻す)。true ならラウンドの終わりなどで、すぐに戻した</summary>
        public event System.Action<bool> CoveredBack;
        /// <summary>帰り: カーテンが開いて消えた (屋台の的を出してよい)</summary>
        public event System.Action Closed;

        AudioSource music;
        readonly List<AudioSource> voicePool = new List<AudioSource>();
        int nextVoice;
        float startTime, idleBeatClock, lastStaffTime, nextSoloCheck, lastRawBeat;
        float playStartTime; // 演奏が始まった時刻 (楽器の動きを少しずつ活発にする)
        float staffSide = 1f;
        Transform player;
        float playerStartY;
        int loops; // 曲が頭に戻った回数 (拍を曲のループをまたいで数え続けるため)
        float[] stageBase, followBase;
        float houseBase;
        Coroutine anim;
        bool playing;
        readonly List<MusicInstrument> soloists = new List<MusicInstrument>();
        readonly Dictionary<MusicInstrument, float> soloUntil = new Dictionary<MusicInstrument, float>();

        // 五線の線と間 (下から 0〜8) の音の高さ: ミ ファ ソ ラ シ ド レ ミ ファ (ト音記号の五線と同じ)
        static readonly int[] SlotMidi = { 64, 65, 67, 69, 71, 72, 74, 76, 77 };

        void Awake()
        {
            Instance = this;
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject($"Voice{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.6f; // どの楽器が鳴ったか、少し方向が分かるように
                src.minDistance = 4f;
                src.maxDistance = 40f;
                voicePool.Add(src);
            }
            stageBase = new float[stageLights.Length];
            for (int i = 0; i < stageLights.Length; i++) stageBase[i] = stageLights[i] != null ? stageLights[i].intensity : 0f;
            if (lightweight)
                for (int i = 0; i < stageLights.Length; i++)
                {
                    if (stageLights[i] == null) continue;
                    if (stageLights[i].type == LightType.Spot) { stageLights[i].enabled = false; stageBase[i] = 0f; } // StageWash
                    else stageBase[i] *= washMakeUp;
                }
            CollectShadowLights();
            PrepareSpots();
            LoadBeatMap();
            houseBase = houseLight != null ? houseLight.intensity : 0f;
            PrepareBaton();
            int mergedParts = 0;
            foreach (var inst in instruments) if (inst != null) { inst.Init(this); mergedParts += inst.MergedParts; }
            Debug.Log($"[ConcertHall] 楽器の部品 {mergedParts} 個の見た目をまとめました (軽量化)");
            foreach (var s in staffs) if (s != null) s.Init();
            if (curtain != null) { curtain.Init(); curtain.HideInstant(); }
            SetTargets(false);
            SetLights(0f);
            if (hall != null) hall.SetActive(false);
            ConcertMusic.Build(this, !UseBgmClip, curtainCloseSeconds, curtainOpenSeconds); // 楽器の音 (と、BGM の曲がなければコードで作る曲) を作っておく (タイトルの間に)
        }

        /// <summary>音楽モードを始める。yaw = ステージを置く向き (度)</summary>
        public void Begin(float yaw, Transform playerRoot)
        {
            if (IsActive) return;
            IsActive = true;
            player = playerRoot;
            playerStartY = player != null ? player.localPosition.y : 0f;
            startTime = Time.time;
            Hits = 0;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // 曲を今のうちに読み込んでおく (演奏が始まる時には、すぐ正確な位置から流せるように)
            if (bgmClip != null && bgmClip.loadState == AudioDataLoadState.Unloaded) bgmClip.LoadAudioData();
            Play(Enter());
        }

        /// <summary>時間切れ: 演奏が止まり、カーテンが閉まって元に戻る。immediate なら、その場ですぐに全部隠す (ラウンドの終わり・リセット)</summary>
        public void End(bool immediate = false)
        {
            if (!IsActive && !immediate) return;
            IsActive = false;
            if (immediate)
            {
                if (anim != null) StopCoroutine(anim);
                anim = null;
                StopPlaying(true);
                if (music != null) music.Stop();
                Groove = 0f;
                SetLights(0f);
                SetPodiumSpot(0f);
                LowerBatonNow();
                SetTargets(false);
                if (hall != null) hall.SetActive(false);
                SetHallShadows(false);
                if (curtain != null) curtain.HideInstant();
                SetLift(0f);
                if (platform != null) platform.SetActive(false);
                CoveredBack?.Invoke(true);
                Closed?.Invoke();
                return;
            }
            Play(Leave());
        }

        IEnumerator Enter()
        {
            // 1. 足元の浮き床が出て (的を撃った時点から)、天井とレールが現れ、カーテンがぐるりと閉まる
            PlacePlatform(0f);
            if (platform != null) platform.SetActive(true);
            yield return curtain.Appear(appearTime);
            PlayClip(ConcertMusic.Curtain, curtainVolume);
            yield return curtain.Draw(0f, 1f, curtainCloseSeconds);
            // 2. 閉まっている間に、外をコンサートホールにする (演奏前の薄暗いステージ)
            SetTargets(true); // 隠れたまま的を立てておく (出す時に起き上がる動きを見せない)
            if (hall != null) hall.SetActive(true);
            SetHallShadows(true);
            SetLights(0f);
            Covered?.Invoke();
            SetLift(liftHeight); // カーテンが閉まっている間に、その場で浮き床ごと客席の上の高さへ置き直す (エレベーターのようには動かさない)
            yield return new WaitForSeconds(holdSeconds);
            // 3. カーテンが逆向きに開いて、天井ごと消える。拍手
            PlayClip(ConcertMusic.CurtainOpen, curtainVolume);
            Sfx.Play("applause", 0.8f); // パチパチ (大勢の拍手)
            yield return curtain.Draw(1f, 0f, curtainOpenSeconds);
            yield return curtain.Vanish(vanishTime);
            Opened?.Invoke();
            // 暗いホールで拍手を聞かせる間に、まず指揮台だけがスポットライトで照らされ、最後に指揮棒を振り上げる
            float raise = batonRaiseSeconds + batonHoldSeconds;
            float dark = Mathf.Max(darkSeconds, podiumSpotDelay + podiumSpotFade + raise);
            yield return new WaitForSeconds(podiumSpotDelay);
            yield return FadePodiumSpot(1f, podiumSpotFade);
            yield return new WaitForSeconds(Mathf.Max(0f, dark - podiumSpotDelay - podiumSpotFade - raise));
            batonHoldBeat = 3.5f; // 4拍目の「と」= 図形のいちばん上まで振り上げる
            batonTargetW = 1f;
            yield return new WaitForSeconds(batonRaiseSeconds);
            yield return new WaitForSeconds(batonHoldSeconds); // 振り上げたまま、いったん静かに落ち着く (大事な間)
            // ポイっと少し上へ跳ね上げて (息を吸うように)、そこから曲のテンポで真下へ振り下ろす (半拍)
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.02f, batonFlickSeconds))
            {
                batonFlick = batonFlickHeight * (1f - (1f - t) * (1f - t)); // すっと上がって、上で止まる
                yield return null;
            }
            batonFlick = batonFlickHeight;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, BeatSecAt(StartBeat - 0.25f) * 0.5f))
            {
                batonHoldBeat = 3.5f + 0.5f * t;
                batonFlick = batonFlickHeight * (1f - t) * (1f - t); // 跳ね上げた分は振り下ろしの勢いの中で消える
                yield return null;
            }
            batonFlick = 0f;
            batonHoldBeat = 4f;
            // 4. 振り下ろした底 (1拍目をたたく瞬間) に演奏が始まり、ステージ全体が lightRiseSeconds かけてゆっくり明るくなる (ホールの環境光も GameManager が同時に上げる)。
            //    楽器の動きも少しずつ活発になり (Update の Groove)、あとはずっと最高潮
            StartPlaying();
            MusicStarted?.Invoke();
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, lightRiseSeconds))
            {
                SetLights(t * t * (3f - 2f * t));
                yield return null;
            }
            SetLights(1f);
            anim = null;
        }

        IEnumerator Leave()
        {
            // 1. 演奏が止まって拍手。ステージが暗くなる
            batonHoldBeat = Beat - StartBeat; // 指揮棒は今の所で止めて、ゆっくり下ろす
            StopPlaying(false);
            batonTargetW = 0f;
            Sfx.Play("applause", 0.85f);
            float vol = music != null ? music.volume : 0f;
            float podium = podiumLevel;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
            {
                if (music != null) music.volume = vol * (1f - t);
                Groove = 1f - t;
                SetLights(1f - t);
                SetPodiumSpot(podium * (1f - t));
                yield return null;
            }
            if (music != null) music.Stop();
            Groove = 0f;
            SetLights(0f);
            SetPodiumSpot(0f);
            // 2. カーテンが閉まる。閉まっている間にホールを消して、屋台を戻す
            yield return curtain.Appear(appearTime);
            PlayClip(ConcertMusic.Curtain, curtainVolume);
            yield return curtain.Draw(0f, 1f, curtainCloseSeconds);
            SetTargets(false);
            if (hall != null) hall.SetActive(false);
            SetHallShadows(false);
            SetLift(0f); // 閉まっている間に、その場で元の地面の高さへ戻す (浮き床は、カーテンが開ききるまで足元に残す)
            CoveredBack?.Invoke(false);
            yield return new WaitForSeconds(0.4f);
            // 3. カーテンが開いて消えると、元の屋台の前
            PlayClip(ConcertMusic.CurtainOpen, curtainVolume);
            yield return curtain.Draw(1f, 0f, curtainOpenSeconds);
            if (platform != null) platform.SetActive(false); // カーテンが開ききった
            yield return curtain.Vanish(vanishTime);
            Closed?.Invoke();
            anim = null;
        }

        void StartPlaying()
        {
            playing = true;
            Groove = grooveStart; // ここから少しずつ活発に (Update)
            playStartTime = Time.time;
            loops = 0;
            lastRawBeat = StartBeat; // 曲の途中 (bgmSkipSeconds) から流す時は、その所の拍から数える
            Beat = StartBeat;
            idleBeatClock = StartBeat;
            TryStartMusic();
            soloists.Clear();
            if (spotOwner != null) System.Array.Clear(spotOwner, 0, spotOwner.Length);
            soloUntil.Clear();
            passAt.Clear();
            for (int i = 0; i < soloSlots; i++) PickSoloist();
            SpawnStaff();
        }

        // BGM を流し始める (曲ができていれば)。演奏の途中から始める時は、今の拍の位置から鳴らして、楽器の揺れとずれないようにする
        // 今の BGM の音量: 演奏の始まりは introVolume、introFadeSeconds かけてなめらかに musicVolume へ下げる
        float CurrentMusicVolume() =>
            Mathf.Lerp(introVolume, musicVolume, Mathf.SmoothStep(0f, 1f, (Time.time - playStartTime) / Mathf.Max(0.01f, introFadeSeconds)));

        // 曲 1周の拍の数 (頭に戻った時に拍を数え続けるため)
        float LoopBeats(AudioClip clip) => Mathf.Max(1f, TimeToBeat(clip.length) - TimeToBeat(0f));

        void TryStartMusic()
        {
            var clip = CurrentBgm;
            if (!bgm || music == null || clip == null || music.isPlaying) return;
            if (music.clip == clip && Time.realtimeSinceStartup - songPlayCalled < 2f) return; // WebGL では Play() の直後しばらく isPlaying が false のことがある。毎フレーム頭から流し直さない
            // 音の時計で予約する時は、曲が読み込み終わってから (読み込み中に予約すると、時計だけ進んで音が遅れて出る)。それまでは拍をテンポどおりに進めて待つ
            if (UseDspClock && clip.loadState != AudioDataLoadState.Loaded)
            {
                if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
                if (clip.loadState != AudioDataLoadState.Failed) return;
            }
            music.clip = clip;
            music.volume = CurrentMusicVolume();
            float loopBeats = LoopBeats(clip);
            loops = Mathf.FloorToInt((idleBeatClock - TimeToBeat(0f)) / loopBeats);
            float raw = idleBeatClock - loops * loopBeats;
            lastRawBeat = raw;
            float time = idleBeatClock <= StartBeat ? StartAt : BeatToTime(raw); // 演奏の始まりからなら、流し始める所 (bgmSkipSeconds) から
            // WebGL (unityroom) では曲の読み込みが終わるまで frequency / samples が 0 のことがあるので、その時は秒で指定する
            if (clip.frequency > 0 && clip.samples > 0) music.timeSamples = Mathf.Clamp(Mathf.RoundToInt(time * clip.frequency), 0, clip.samples - 1);
            else music.time = Mathf.Max(0f, time);
            if (UseDspClock)
            {
                // 少し先の、音の時計の決まった時刻に鳴り始めるよう予約する (その時刻から、曲の time 秒目が鳴る)
                songDspStart = AudioSettings.dspTime + DspLead;
                music.PlayScheduled(songDspStart);
            }
            else music.Play();
            songStartPos = time;
            songLastPos = -1f;
            songHeard = false;
            songPlayCalled = Time.realtimeSinceStartup;
        }

        // 曲の今の再生位置 (秒)。
        // WebGL (unityroom) では Play() を呼んでから本当に音が出始めるまで遅れがあり、再生位置や frequency が 0 / NaN のこともある
        // (そのまま使うと拍が NaN になって楽器が全部消えた。Play() の時から自分の時計で進めると、音より先に楽器が動いてずれた)。
        // そこで、再生位置が進み始める (= 本当に音が出始める) までは流し始めた位置で止めて待ち、進み始めたらそれに合わせる。
        // 再生位置が取れない間 (フレームの合間など) は、最後に合わせた所から実時間で進める。いつまでも進まない時 (1.5秒) は実時間だけで進める
        float songStartPos, songLastPos, songSyncPos, songSyncAt, songPlayCalled = -10f;
        bool songHeard;
        double songDspStart;
        const double DspLead = 0.05; // 予約してから鳴り始めるまで (読み込み済みの曲をほどき始める時間。指揮棒の「1」とほとんど同時)

        // パソコン・Quest (エディターも): 音の時計 (AudioSettings.dspTime) で曲の位置を数える。
        // 実際に鳴っている音と同じ時計なので、フレームの揺れや、再生位置の読み取りのずれがない。WebGL は音の時計が当てにならないので、前のやり方
#if UNITY_WEBGL && !UNITY_EDITOR
        static bool UseDspClock => false;
#else
        static bool UseDspClock => true;
#endif

        // 曲の今の位置 (秒。拍の表と同じ、曲の頭からの時刻)。beatOffsetSeconds の分だけ、拍を音より遅らせる / 早める
        float SongTime(AudioClip clip) => SongPosition(clip) - beatOffsetSeconds;

        float SongPosition(AudioClip clip)
        {
            if (UseDspClock)
            {
                double since = AudioSettings.dspTime - songDspStart;
                if (since < 0.0) return songStartPos; // まだ鳴り始めていない: 流し始める所で待つ
                float pos = songStartPos + (float)since;
                float length = clip.length;
                return length > 0.1f && IsFinite(length) ? Mathf.Repeat(pos, length) : pos;
            }
            float now = Time.realtimeSinceStartup;
            float a = clip.frequency > 0 ? music.timeSamples / (float)clip.frequency : music.time;
            bool valid = IsFinite(a) && a >= 0f;
            if (valid && songLastPos >= 0f && a != songLastPos)
            {
                // 再生位置が動いた: 本当に鳴っている。その位置に合わせる
                songHeard = true;
                songSyncPos = a;
                songSyncAt = now;
            }
            if (valid) songLastPos = a;
            if (!songHeard)
            {
                if (now - songPlayCalled < 1.5f) return songStartPos; // まだ音が出ていない: 流し始める所で待つ
                songHeard = true; // 再生位置が取れない環境: ここから実時間で進める
                songSyncPos = songStartPos;
                songSyncAt = now;
            }
            float t = songSyncPos + (now - songSyncAt);
            float len = clip.length;
            if (len > 0.1f && IsFinite(len)) t = Mathf.Repeat(t, len);
            return t;
        }

        static bool IsFinite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

        void StopPlaying(bool instant)
        {
            playing = false;
            foreach (var s in soloists) if (s != null) s.SetSolo(false);
            soloists.Clear();
            if (spotOwner != null) System.Array.Clear(spotOwner, 0, spotOwner.Length); // スポットライトも全部消す
            ApplySpots();
            soloUntil.Clear();
            passAt.Clear();
            UpdateBlockers(); // 演奏が止まったら、当たり判定を全部戻す
            foreach (var s in staffs)
            {
                if (s == null) continue;
                if (instant) s.HideInstant();
                else s.Stop();
            }
        }

        // 楽器と音符の当たり判定を出す / しまう (しまうと楽器は見えなくなるので、ホールが隠れている間だけ)
        void SetTargets(bool on)
        {
            foreach (var inst in instruments)
            {
                if (inst == null || inst.hitTarget == null) continue;
                inst.hitTarget.SetAvailable(on);
                inst.SetSolo(false);
            }
        }

        // 照明の明るさ (0 = 演奏前の薄暗いステージ 〜 1 = 演奏中)
        void SetLights(float k)
        {
            for (int i = 0; i < stageLights.Length; i++)
                if (stageLights[i] != null) stageLights[i].intensity = stageBase[i] * Mathf.Lerp(dimLevel, playBright, k);
            if (houseLight != null) houseLight.intensity = houseBase * Mathf.Lerp(1f, houseDuringPlay, k);
            if (showWhilePlaying != null)
                foreach (var go in showWhilePlaying) if (go != null) go.SetActive(k > 0.3f);
            lightLevel = k;
            ApplySpots(); // 光の筋も showWhilePlaying に入っているので、その後でソロのある物だけにする
            if (podiumSpot != null) podiumSpot.enabled = podiumLevel > 0.01f && !(lightweight && k > 0.5f);
        }

        // ---- ソロのスポットライト (ソロ1つに1台。その楽器の斜め上前に固定して照らす) ----

        float lightLevel;            // 今の照明の明るさ (SetLights の k)
        MusicInstrument[] spotOwner; // スポットライトごとの、照らしているソロの楽器 (なければ null)

        // ソロの数だけスポットライトと光の筋をそろえる (足りなければ1台目を複製)
        void PrepareSpots()
        {
            var spots = new List<Light>();
            var beams = new List<Transform>();
            for (int i = 0; i < followSpots.Length; i++)
            {
                if (followSpots[i] == null) continue;
                spots.Add(followSpots[i]);
                beams.Add(i < followBeams.Length ? followBeams[i] : null);
            }
            if (spots.Count > 0)
                while (spots.Count < soloSlots)
                {
                    var spot = Instantiate(spots[0], spots[0].transform.parent);
                    spot.name = spots[0].name + "_Copy";
                    spots.Add(spot);
                    Transform beam = null;
                    if (beams[0] != null)
                    {
                        beam = Instantiate(beams[0], beams[0].parent);
                        beam.name = beams[0].name + "_Copy";
                    }
                    beams.Add(beam);
                }
            followSpots = spots.ToArray();
            followBeams = beams.ToArray();
            followBase = new float[followSpots.Length];
            for (int i = 0; i < followSpots.Length; i++) followBase[i] = followSpots[i].intensity;
            spotOwner = new MusicInstrument[followSpots.Length];
        }

        // 空いているスポットライトをソロの楽器に向けて置く (ソロの間は動かさない)
        void AssignSpot(MusicInstrument inst)
        {
            if (spotOwner == null) return;
            for (int i = 0; i < spotOwner.Length; i++)
            {
                if (spotOwner[i] != null) continue;
                spotOwner[i] = inst;
                var spot = followSpots[i].transform;
                var hall = spot.parent;
                // 楽器から客席の方へ spotFront 手前の、天井近く (ビルダーで置いた高さ) から照らす
                Vector3 target = inst.AimPoint;
                Vector3 back = hall != null ? -hall.forward : Vector3.back;
                Vector3 pos = target + back * spotFront;
                pos.y = spot.position.y;
                spot.position = pos;
                spot.rotation = Quaternion.LookRotation((target - pos).normalized);
                var beam = followBeams[i];
                if (beam != null)
                {
                    beam.position = pos;
                    beam.rotation = spot.rotation;
                    beam.localScale = new Vector3(1f, 1f, Mathf.Max(0.1f, (target - pos).magnitude));
                }
                break;
            }
            ApplySpots();
        }

        void ReleaseSpot(MusicInstrument inst)
        {
            if (spotOwner == null) return;
            for (int i = 0; i < spotOwner.Length; i++) if (spotOwner[i] == inst) spotOwner[i] = null;
            ApplySpots();
        }

        // ソロを照らしているスポットライトだけ点ける (明るさは照明に合わせる)
        void ApplySpots()
        {
            if (spotOwner == null) return;
            for (int i = 0; i < followSpots.Length; i++)
            {
                bool on = spotOwner[i] != null && lightLevel > 0.01f;
                followSpots[i].intensity = followBase[i] * lightLevel;
                followSpots[i].enabled = on && !lightweight; // 軽量化の時は光の筋だけ (楽器はソロの蛍光色で目立つ)
                if (followBeams[i] != null) followBeams[i].gameObject.SetActive(on && lightLevel > 0.3f);
            }
        }

        // ---- ソロ ----

        readonly List<MusicInstrument> candidates = new List<MusicInstrument>(); // ソロの候補 (使い回す。演奏中にメモリを確保しない = GC で止まらないように)

        void PickSoloist()
        {
            candidates.Clear();
            foreach (var inst in instruments)
                if (inst != null && !inst.IsSolo && inst.hitTarget != null) candidates.Add(inst);
            if (candidates.Count == 0) return;
            // 前のソロと同じ所に固まらないように、今のソロから離れた楽器を選びやすくする
            MusicInstrument pick = candidates[Random.Range(0, candidates.Count)];
            for (int tries = 0; tries < 6 && soloists.Count > 0; tries++)
            {
                bool near = false;
                foreach (var s in soloists) if (s != null && (s.transform.position - pick.transform.position).sqrMagnitude < 9f) near = true;
                if (!near) break;
                pick = candidates[Random.Range(0, candidates.Count)];
            }
            pick.SetSolo(true);
            AssignSpot(pick); // 空いているスポットライトをこの楽器に固定して当てる
            soloists.Add(pick);
            soloUntil[pick] = Beat + soloBeats * Random.Range(0.8f, 1.2f);
            UpdateBlockers();
        }

        void EndSolo(MusicInstrument inst)
        {
            if (inst == null) return;
            inst.SetSolo(false);
            ReleaseSpot(inst);
            soloists.Remove(inst);
            soloUntil.Remove(inst);
            passAt.Remove(inst);
            UpdateBlockers();
            if (playing) PickSoloist();
        }

        // ソロの楽器を撃ちやすいように、ソロでない楽器のうち、ソロの楽器のまわり (blockRadius 以内) にある物と、
        // プレイヤーの目からソロの楽器への視線の近く (sightClearance 以内) でソロより手前にある物の当たり判定を消す。演奏していない時は全部戻す
        void UpdateBlockers()
        {
            var cam = Camera.main;
            Vector3 eyePos = cam != null ? cam.transform.position : transform.position + Vector3.up * (1.6f + liftHeight);
            foreach (var inst in instruments)
            {
                if (inst == null) continue;
                bool block = false;
                if (playing && !inst.IsSolo)
                    foreach (var s in soloists)
                    {
                        if (s == null) continue;
                        Vector3 p = inst.AimPoint, target = s.AimPoint;
                        if ((p - target).sqrMagnitude < blockRadius * blockRadius) { block = true; break; }
                        var sight = target - eyePos;
                        float t = Vector3.Dot(p - eyePos, sight) / Mathf.Max(0.0001f, sight.sqrMagnitude);
                        if (t > 0f && t < 1f && (eyePos + sight * t - p).sqrMagnitude < sightClearance * sightClearance) { block = true; break; }
                    }
                inst.SetHittable(!block);
            }
        }

        // 撃たれたソロの楽器を、delay 秒後に別の楽器へ交代させる (Update で見る。コルーチンを作らない)
        readonly Dictionary<MusicInstrument, float> passAt = new Dictionary<MusicInstrument, float>();
        void PassSoloAfter(MusicInstrument inst, float delay)
        {
            soloUntil[inst] = float.MaxValue; // 交代するまで、時間切れで交代させない
            if (!passAt.ContainsKey(inst)) passAt[inst] = Time.time + delay;
        }

        // ---- 影 (ホールが出ている間は、外の明かりの影を切る。影を描く分が丸ごと減る) ----

        readonly List<Light> shadowLights = new List<Light>();
        readonly List<LightShadows> shadowModes = new List<LightShadows>();

        void CollectShadowLights()
        {
            shadowLights.Clear();
            shadowModes.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.shadows != LightShadows.None && !l.transform.IsChildOf(transform))
                {
                    shadowLights.Add(l);
                    shadowModes.Add(l.shadows);
                }
        }

        void SetHallShadows(bool hallShown)
        {
            if (!noShadowsInHall) return;
            for (int i = 0; i < shadowLights.Count; i++)
                if (shadowLights[i] != null) shadowLights[i].shadows = hallShown ? LightShadows.None : shadowModes[i];
        }

        // ---- 浮き床 ----

        // 浮き床 (とカーテン) とプレイヤーを、地面から h (m) の高さにする
        void SetLift(float h)
        {
            if (lift != null) lift.localPosition = Vector3.up * h;
            PlacePlatform(h);
            if (player != null)
            {
                var p = player.localPosition;
                player.localPosition = new Vector3(p.x, playerStartY + h, p.z);
            }
        }

        Vector3 platformRest;
        bool platformRestCaptured;

        // 浮き床の高さ: 持ち上げた時は足元にぴったり、いつもの地面の高さの時は少し上げて見えるように
        void PlacePlatform(float h)
        {
            if (platform == null) return;
            var t = platform.transform;
            if (!platformRestCaptured) { platformRest = t.localPosition; platformRestCaptured = true; }
            t.localPosition = platformRest + Vector3.up * (h < 0.01f ? platformGroundRaise : 0f);
        }

        public static ConcertHall Instance { get; private set; }

        // ---- 当たった (GameManager から) ----

        /// <summary>楽器に当たった: 跳ねて音が鳴る。ソロの楽器なら点数を返し、五線譜を出す (ソロでなければ 0)</summary>
        public int OnInstrumentHit(Target target, Vector3 point)
        {
            if (!IsActive || !playing) return 0;
            var inst = target.GetComponentInParent<MusicInstrument>();
            if (inst == null) return 0;
            // 撃った楽器の音を曲に合わせる: BGM の曲ならその調の音階に、コードで作った曲なら今の和音に
            int m = UseBgmClip ? ConcertMusic.ScaleToneNear(bgmKeyRoot, bgmMinor, inst.midi) : ConcertMusic.ChordToneNear(Beat, inst.midi);
            PlayVoice(inst.voice, m, inst.transform.position, inst.IsSolo ? 1f : 0.6f);
            if (!inst.IsSolo)
            {
                inst.Jump(false);
                return 0;
            }
            inst.Jump(true);
            Hits++;
            SpawnStaff();
            PassSoloAfter(inst, 0.9f); // 少し見せてから、ソロが別の楽器へ移る
            return inst.points;
        }

        /// <summary>音符に当たった: その線 / 間の高さの音が鳴る (点数は音符の的が持っている)</summary>
        public void OnNoteHit(Target target)
        {
            var note = target.GetComponentInParent<MusicNote>();
            if (note == null) return;
            Hits++;
            int bell = SlotMidi[Mathf.Clamp(note.slot, 0, SlotMidi.Length - 1)] + 12;
            if (UseBgmClip) bell = ConcertMusic.ScaleToneNear(bgmKeyRoot, bgmMinor, bell); // BGM の曲の調の音階に合わせる
            PlayVoice(ConcertMusic.Voice.Bell, bell, note.transform.position, 0.9f);
        }

        // ---- 五線譜 ----

        // 空いている五線譜を1本出す。左右を交互に (ふさがっていれば反対側)。
        // 五線譜ごとに「段」(Level 0〜1) を決め、同じ側に出ている五線譜とは必ず minStaffLevelGap 以上離す (それより先はランダム。音符の間隔と同じ考え方)。
        // 段が大きいほど、ステージの端の奥・高い所から出て、プレイヤーの後ろの高い所へ抜ける (段の順に並ぶので、帯どうしが交わらない)
        void SpawnStaff()
        {
            lastStaffTime = Time.time;
            if (!playing || staffs == null) return;
            MusicStaff staff = null;
            int busy = 0;
            foreach (var s in staffs)
            {
                if (s == null) continue;
                if (s.IsBusy) busy++;
                else if (staff == null) staff = s;
            }
            if (staff == null || busy >= maxStaffs) return;

            float side = 0f, level = -1f;
            for (int k = 0; k < 2; k++)
            {
                float sd = k == 0 ? staffSide : -staffSide;
                level = PickStaffLevel(staff, sd);
                if (level >= 0f) { side = sd; break; }
            }
            if (level < 0f) return; // どちらの側も空きがない
            staffSide = -side; // 次は反対側から

            float eye = 1.6f + liftHeight; // 浮き床の上のプレイヤーの目の高さ
            // 始まり: ステージの左右の端 (楽器の外側、両わきの幕の内側) の、床のすぐ上 (staffLowY) 〜 天井の下 (staffHighY)。段が大きいほど奥で高い
            var start = new Vector3(side * Random.Range(staffEdgeX - 0.3f, staffEdgeX), // 舞台の両わきの幕にかからないように、これより外へは出さない
                                    Mathf.Lerp(staffLowY, staffHighY, level) + Random.Range(-0.1f, 0.1f),
                                    Mathf.Lerp(staffNearZ, staffStartZ, level));
            // 終わり: プレイヤーの横を回って、真後ろの近くまで回り込んだ所 (横 1.2〜2.2m・後ろ 5〜6m。左右の帯が後ろでぶつからない間をあける)。
            // 段が大きいほど高い (目の 2.6m 下〜2.2m 上。客席の椅子より上)
            var end = new Vector3(side * Random.Range(1.2f, 2.2f), eye + Mathf.Lerp(-2.6f, 2.2f, level), -Random.Range(5f, 6f));
            staff.Level = level;
            staff.Begin(start, end, new Vector3(0f, eye, 0f));
        }

        // side の側で、出ている五線譜の段から minStaffLevelGap 以上離れた段をランダムに選ぶ。見つからなければ -1
        float PickStaffLevel(MusicStaff self, float side)
        {
            for (int tries = 0; tries < 24; tries++)
            {
                float level = Random.value;
                bool ok = true;
                foreach (var s in staffs)
                    if (s != null && s != self && s.IsBusy && Mathf.Approximately(s.Side, side) && Mathf.Abs(s.Level - level) < minStaffLevelGap) { ok = false; break; }
                if (ok) return level;
            }
            return -1f;
        }

        // ---- 音 ----

        void PlayVoice(ConcertMusic.Voice voice, int midi, Vector3 pos, float volume)
        {
            var clip = ConcertMusic.VoiceClip(voice);
            if (clip == null || voicePool.Count == 0) return;
            var src = voicePool[nextVoice];
            nextVoice = (nextVoice + 1) % voicePool.Count;
            int baseMidi = ConcertMusic.BaseMidi(voice);
            src.pitch = baseMidi > 0 ? Mathf.Clamp(ConcertMusic.Hz(midi) / ConcertMusic.Hz(baseMidi), 0.1f, 3f) : Random.Range(0.95f, 1.05f);
            src.transform.position = pos;
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }

        void PlayClip(AudioClip clip, float volume)
        {
            if (clip == null || voicePool.Count == 0) return;
            var src = voicePool[nextVoice];
            nextVoice = (nextVoice + 1) % voicePool.Count;
            src.pitch = 1f;
            src.transform.localPosition = Vector3.up * 2f; // プレイヤーのまわり (カーテンの音)
            src.clip = clip;
            src.volume = volume;
            src.Play();
        }

        void Update()
        {
            // 拍: 演奏中は曲の再生位置から (楽器の揺れが音とずれないように)。それ以外はゆっくり進める
            if (music != null && music.isPlaying && music.clip != null)
            {
                float raw = TimeToBeat(SongTime(music.clip)); // 拍の表があれば表から
                if (raw < lastRawBeat - 1f) loops++; // 曲の頭に戻った
                lastRawBeat = raw;
                float beat = loops * LoopBeats(music.clip) + raw;
                if (IsFinite(beat))
                {
                    if (clickOnBeats && playing && Mathf.Floor(beat) > Mathf.Floor(Beat)) Sfx.Play("click", 1.5f); // 確認用: 拍ごとにカチッ
                    Beat = idleBeatClock = beat; // 念のため、おかしな値の時は前の拍のまま
                }
            }
            else
            {
                // 曲を流していない演奏中は、曲のテンポどおりに進める
                // (曲を流し始めたのにまだ鳴っていない間は、拍を進めずに音を待つ)
                bool waitingForSong = playing && bgm && CurrentBgm != null && Time.realtimeSinceStartup - songPlayCalled < 2f;
                if (!waitingForSong) idleBeatClock += playing ? Time.deltaTime / BeatSec : Time.deltaTime * 0.6f;
                Beat = idleBeatClock;
            }

            UpdateBaton(); // 指揮棒は演奏の前 (振り上げ) と後 (下ろす) も動く
            if (!playing) return;
            // 楽器の動き: 演奏の始まりは控えめで、grooveRiseSeconds かけて最高潮へ。あとはずっと最高潮
            Groove = Mathf.Lerp(grooveStart, 1f, Mathf.SmoothStep(0f, 1f, (Time.time - playStartTime) / Mathf.Max(0.01f, grooveRiseSeconds)));
            if (music != null && music.isPlaying) music.volume = CurrentMusicVolume(); // 始まりは大きく、だんだんいつもの音量へ
            TryStartMusic(); // 演奏が始まった時に曲がまだできていなかったら、できしだい流す

            // 撃たれたソロの交代 (EndSolo は soloists から消して後ろに足すので、後ろから見る)
            if (passAt.Count > 0)
                for (int i = soloists.Count - 1; i >= 0; i--)
                {
                    if (i >= soloists.Count) continue;
                    var s = soloists[i];
                    if (passAt.TryGetValue(s, out var at) && Time.time >= at) EndSolo(s);
                }
            // ソロの交代
            if (Time.time >= nextSoloCheck)
            {
                nextSoloCheck = Time.time + 0.25f;
                for (int i = soloists.Count - 1; i >= 0; i--)
                {
                    if (i >= soloists.Count) continue;
                    var s = soloists[i];
                    if (soloUntil.TryGetValue(s, out var until) && Beat >= until && until < float.MaxValue) EndSolo(s);
                }
                while (soloists.Count < soloSlots && playing) { int before = soloists.Count; PickSoloist(); if (soloists.Count == before) break; }
            }
            // 五線譜を次々に出す (何本も出てきては途切れる)
            if (Time.time - lastStaffTime > staffEvery) SpawnStaff();
        }

        // ---- 指揮棒 (指揮台の上で4拍子を振る。指揮者はいない) ----
        //
        // 指揮棒の先が描く4拍子の図形 (指揮者から見た向き = 後ろの客席から見た向きと同じ。x は右、y は上。1 の点が (0, 0)、いちばん上が 1)。
        // 拍の点 (1〜4、たたく所) と、その間の跳ね上がった所 (「と」) を3次ベジェ曲線でつなぐ。半拍ごとに1本、全部で8本 (それぞれ4つの制御点)。
        //   1 (真下へ振り下ろした所) → と (左上へまっすぐ跳ね上がる) → 2 (左下で丸く) → と (左上) → 3 (右へ大きく、下で丸く) → と (右上)
        //   → 4 (少し左へ戻って丸く) → と (真上。いちばん高い所。振り上げ) → 1 (まっすぐ真下へ。いちばん大事な振り下ろし)
        static readonly Vector2[] BatonPath =
        {
            new Vector2(0f, 0f),        new Vector2(-0.127f, 0.197f), new Vector2(-0.253f, 0.393f), new Vector2(-0.38f, 0.59f),  // 1 → と
            new Vector2(-0.38f, 0.59f), new Vector2(-0.40f, 0.35f),   new Vector2(-0.38f, 0.054f),  new Vector2(-0.5f, 0.054f),  // と → 2
            new Vector2(-0.5f, 0.054f), new Vector2(-0.62f, 0.054f),  new Vector2(-0.72f, 0.25f),   new Vector2(-0.735f, 0.446f), // 2 → と
            new Vector2(-0.735f, 0.446f), new Vector2(-0.45f, 0.20f), new Vector2(0.10f, 0.036f),   new Vector2(0.5f, 0.036f),   // と → 3
            new Vector2(0.5f, 0.036f),  new Vector2(0.85f, 0.036f),   new Vector2(1.1f, 0.22f),     new Vector2(1.206f, 0.375f), // 3 → と
            new Vector2(1.206f, 0.375f), new Vector2(0.95f, 0.22f),   new Vector2(0.70f, 0.232f),   new Vector2(0.47f, 0.232f),  // と → 4
            new Vector2(0.47f, 0.232f), new Vector2(0.24f, 0.232f),   new Vector2(0.02f, 0.55f),    new Vector2(0f, 1f),         // 4 → と (振り上げ)
            new Vector2(0f, 1f),        new Vector2(0f, 0.667f),      new Vector2(0f, 0.333f),      new Vector2(0f, 0f),         // と → 1 (振り下ろし)
        };
        const float BatonPathCenterX = 0.235f; // 図形の左右の真ん中 (左端 -0.735 と右端 1.206 の間)
        static readonly Vector3 BatonTipLocal = new Vector3(0f, 0.08f, 0.445f); // ビルダーで作った指揮棒の先 (Baton の中の位置。大きさを掛ける前)

        Vector3 batonRest;        // ビルダーで置いた指揮棒の位置 (Conductor の中)
        Vector3 batonShoulder;    // 指揮棒を持つ「肩」の位置 (見えない指揮者。ここから指揮棒の先の方を向く)
        Vector3 batonCenter;      // 図形の 1 の点 (たたく所の真ん中の下)
        Vector3 batonDown;        // 演奏していない時に指揮棒の先を下ろしておく所
        float batonW;             // 0 = 下ろしている 〜 1 = 振っている
        float batonTargetW;
        float batonHoldBeat;      // 演奏していない間の指揮棒の図形の上の拍 (0 = 1拍目をたたく所。振り上げは 3.5 = 4拍目の「と」のいちばん上)
        float batonFlick;         // 振り下ろす直前にポイっと跳ね上げている高さ (m)
        TrailRenderer batonTrail; // 指揮棒の先の光の尾 (図形が見えるように)
        Light podiumSpot;         // 指揮台を照らすスポットライト (演奏の前に、最初に点く)
        Transform podiumBeam;
        float podiumSpotBase, podiumLevel;

        // 指揮棒を大きくして、先に光の尾を付け、指揮台を照らすスポットライトを用意する (ビルダーの作り直しなしで動くように、ここで作る)
        void PrepareBaton()
        {
            if (baton == null) return;
            batonRest = baton.localPosition;
            baton.localScale = Vector3.one * batonScale;
            // 見えない指揮者: 肩は指揮棒の少し右上・手前 (客席側)。図形はその前 (楽器の方) の縦の面に描く
            batonShoulder = batonRest + new Vector3(0.1f, 0.3f, -0.55f);
            batonCenter = batonRest + new Vector3(-0.05f, -0.1f, 0.55f);
            batonDown = batonRest + new Vector3(0.1f, -0.45f, 0.25f);

            var tip = new GameObject("BatonTip").transform;
            tip.SetParent(baton, false);
            tip.localPosition = BatonTipLocal;
            batonTrail = tip.gameObject.AddComponent<TrailRenderer>();
            batonTrail.time = 0.45f;
            batonTrail.minVertexDistance = 0.01f;
            batonTrail.widthMultiplier = 0.035f;
            batonTrail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.3f), 1f) },
                         new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            batonTrail.colorGradient = grad;
            batonTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            batonTrail.receiveShadows = false;
            batonTrail.emitting = false;
            // 材質は五線譜の線と同じ (半透明・頂点の色が効く)
            if (staffs != null && staffs.Length > 0 && staffs[0] != null && staffs[0].lines != null && staffs[0].lines.Length > 0 && staffs[0].lines[0] != null)
                batonTrail.sharedMaterial = staffs[0].lines[0].sharedMaterial;

            // 指揮台を照らすスポットライト: ソロのスポットライトを複製して、指揮台の上の客席寄りから当てる
            if (followSpots != null && followSpots.Length > 0 && followSpots[0] != null)
            {
                var parent = baton.parent;
                podiumSpot = Instantiate(followSpots[0], followSpots[0].transform.parent);
                podiumSpot.name = "PodiumSpot";
                podiumSpot.spotAngle = 18f;
                podiumSpotBase = followBase != null && followBase.Length > 0 ? followBase[0] : podiumSpot.intensity;
                Vector3 target = parent.TransformPoint(batonRest + new Vector3(-0.2f, -0.7f, -0.1f)); // 指揮台の上の、指揮者の胸くらい
                Vector3 pos = parent.TransformPoint(batonRest + new Vector3(-0.2f, 5.5f, -3.2f));
                podiumSpot.transform.position = pos;
                podiumSpot.transform.rotation = Quaternion.LookRotation((target - pos).normalized);
                if (followBeams != null && followBeams.Length > 0 && followBeams[0] != null)
                {
                    podiumBeam = Instantiate(followBeams[0], followBeams[0].parent);
                    podiumBeam.name = "PodiumBeam";
                    podiumBeam.position = pos;
                    podiumBeam.rotation = podiumSpot.transform.rotation;
                    podiumBeam.localScale = new Vector3(1.3f, 1.3f, (target - pos).magnitude + 1.5f);
                }
                SetPodiumSpot(0f);
            }
            batonW = batonTargetW = 0f;
            PlaceBaton(batonDown);
        }

        // 指揮棒をその場ですぐ下ろす (光の尾も消す)。モードをすぐに終わらせる時
        void LowerBatonNow()
        {
            if (baton == null) return;
            batonW = batonTargetW = 0f;
            batonFlick = 0f;
            PlaceBaton(batonDown);
            if (batonTrail != null) { batonTrail.emitting = false; batonTrail.Clear(); }
        }

        void SetPodiumSpot(float k)
        {
            podiumLevel = k;
            if (podiumSpot != null)
            {
                podiumSpot.intensity = podiumSpotBase * k;
                podiumSpot.enabled = k > 0.01f && !(lightweight && lightLevel > 0.5f); // 軽量化の時は、ステージが明るくなったら光の筋だけ
            }
            if (podiumBeam != null) podiumBeam.gameObject.SetActive(k > 0.2f);
        }

        IEnumerator FadePodiumSpot(float to, float seconds)
        {
            float from = podiumLevel;
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
            {
                SetPodiumSpot(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetPodiumSpot(to);
        }

        // 拍 beat の時の、図形の上の指揮棒の先の位置 (Conductor の中)。拍の点ではいちばん速く (たたく)、「と」ではゆっくり止まる
        Vector3 BatonPatternPoint(float beat)
        {
            float h = Mathf.Repeat(beat, 4f) * 2f; // 半拍ごとの番号 0〜8
            int seg = Mathf.Min(7, Mathf.FloorToInt(h));
            float t = h - seg;
            t = seg % 2 == 0 ? 1f - (1f - t) * (1f - t) : t * t; // 拍の点から「と」へは減速、「と」から拍の点へは加速
            int i = seg * 4;
            float u = 1f - t;
            Vector2 p = u * u * u * BatonPath[i] + 3f * u * u * t * BatonPath[i + 1] + 3f * u * t * t * BatonPath[i + 2] + t * t * t * BatonPath[i + 3];
            return batonCenter + new Vector3((p.x - BatonPathCenterX) * batonPatternSize.x, p.y * batonPatternSize.y, 0f);
        }

        // 指揮棒の先を tipPos (Conductor の中) に置く。根元は肩から先の方へ向けた線の上
        void PlaceBaton(Vector3 tipPos)
        {
            var dir = tipPos - batonShoulder;
            if (dir.sqrMagnitude < 0.0001f) return;
            var rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(BatonTipLocal.normalized, Vector3.up));
            baton.localRotation = rot;
            baton.localPosition = tipPos - rot * (BatonTipLocal * batonScale);
        }

        // 毎フレーム: 演奏中は今の拍の図形の上、演奏前は止めておく拍の所 (振り上げ)。下ろしている所との間をなめらかにつなぐ
        void UpdateBaton()
        {
            if (baton == null) return;
            batonW = Mathf.MoveTowards(batonW, batonTargetW, Time.deltaTime / Mathf.Max(0.05f, batonRaiseSeconds));
            float beat = playing ? Beat - StartBeat : batonHoldBeat; // 曲を流し始めた拍を「1」にする
            float w = Mathf.SmoothStep(0f, 1f, batonW);
            PlaceBaton(Vector3.Lerp(batonDown, BatonPatternPoint(beat), w) + Vector3.up * batonFlick);
            if (batonTrail != null) batonTrail.emitting = batonW > 0.95f;
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
