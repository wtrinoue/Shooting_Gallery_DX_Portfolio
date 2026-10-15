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
        [Tooltip("持ち上げる物 (カーテンと浮き床) / 浮き床 (ホールが出ている間だけ出す)")]
        public Transform lift;
        public GameObject platform;
        public float liftHeight = 4.2f;

        [Header("照明")]
        [Tooltip("ステージを照らす明かり (演奏前は dimLevel の明るさ、演奏中はビルダーで決めた明るさの playBright 倍)")]
        public Light[] stageLights;
        [Range(0f, 1f)] public float dimLevel = 0.12f;
        public float playBright = 1.8f;
        [Tooltip("客席を照らす明かり (演奏中はビルダーで決めた明るさの housePlay 倍。1 より大きいとホール全体も明るくなる)")]
        public Light houseLight;
        public float housePlay = 1.6f; // 前は houseDim 0.45 (演奏中は客席を暗くしていた)
        [Tooltip("ソロの楽器を追いかけるスポットライトと、その光の筋 (光と同じ向きに伸ばす。長さ 1 の円すい)")]
        public Light[] followSpots;
        public Transform[] followBeams;
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
        public int soloCount = 2;
        public float soloBeats = 16f;
        [Tooltip("ソロの楽器を撃ちやすいように当たり判定を消す範囲: ソロの楽器からの距離 (m) / プレイヤーからソロの楽器への視線からの距離 (m)")]
        public float blockRadius = 3f;
        public float sightClearance = 1.2f;
        [Tooltip("指揮者の譜面台の上で拍子をとる指揮棒")]
        public Transform baton;

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
        [Tooltip("BGM の曲 (空ならコードで作った曲を流す)。今は 魔王魂 の曲 (Assets/Source/music_stage.mp3。クレジットは Resources/THIRD_PARTY_NOTICES.txt)")]
        public AudioClip bgmClip;
        [Tooltip("BGM の曲のテンポ (BPM) / 最初の拍の時刻 (秒)。楽器の揺れ・指揮棒をこの拍に合わせる")]
        public float bgmBpm = 135f;
        public float bgmFirstBeat = 0.14f;
        [Tooltip("BGM の曲を、頭から何秒の所から流し始めるか (曲の初めの音のない所を飛ばす)。拍の数え方もこれに合わせるので、楽器の揺れはずれない。曲が終わって頭に戻る時は 0 秒から")]
        public float bgmSkipSeconds = 1.8f; // 魔王魂の曲は頭から 1.8 秒の所から (ユーザー指定)。名前を変えて、シーンに保存された前の値 (bgmStartAt 1.7 / bgmStartSeconds 1) を使わない
        [Tooltip("BGM の曲の調: 主音 (0 = ド, 2 = レ, 5 = ファ …) と短調かどうか。楽器や音符を撃った時の音を、この調の音階に合わせる")]
        public int bgmKeyRoot = 5;
        public bool bgmMinor = true;

        bool UseBgmClip => bgmClip != null;
        AudioClip CurrentBgm => UseBgmClip ? bgmClip : ConcertMusic.Loop;
        float BeatSec => UseBgmClip ? 60f / Mathf.Max(1f, bgmBpm) : ConcertMusic.BeatSeconds;
        float FirstBeat => UseBgmClip ? bgmFirstBeat : 0f;
        // 流し始める所 (曲の頭からの秒) と、その所の拍 (演奏はこの拍から数え始める)
        float StartAt => UseBgmClip ? Mathf.Max(0f, bgmSkipSeconds) : 0f;
        float StartBeat => StartAt > 0f ? (StartAt - FirstBeat) / BeatSec : 0f;

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
        Quaternion batonBase;

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
            followBase = new float[followSpots.Length];
            for (int i = 0; i < followSpots.Length; i++) followBase[i] = followSpots[i] != null ? followSpots[i].intensity : 0f;
            houseBase = houseLight != null ? houseLight.intensity : 0f;
            if (baton != null) batonBase = baton.localRotation;
            foreach (var inst in instruments) if (inst != null) inst.Init(this);
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
                SetTargets(false);
                if (hall != null) hall.SetActive(false);
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
            // 1. 天井とレールが現れ、カーテンがぐるりと閉まる
            yield return curtain.Appear(appearTime);
            PlayClip(ConcertMusic.Curtain, curtainVolume);
            yield return curtain.Draw(0f, 1f, curtainCloseSeconds);
            // 2. 閉まっている間に、外をコンサートホールにする (演奏前の薄暗いステージ)
            SetTargets(true); // 隠れたまま的を立てておく (出す時に起き上がる動きを見せない)
            if (hall != null) hall.SetActive(true);
            SetLights(0f);
            if (platform != null) platform.SetActive(true);
            Covered?.Invoke();
            SetLift(liftHeight); // カーテンが閉まっている間に、その場で浮き床ごと客席の上の高さへ置き直す (エレベーターのようには動かさない)
            yield return new WaitForSeconds(holdSeconds);
            // 3. カーテンが逆向きに開いて、天井ごと消える。拍手
            PlayClip(ConcertMusic.CurtainOpen, curtainVolume);
            Sfx.Play("applause", 0.8f); // パチパチ (大勢の拍手)
            yield return curtain.Draw(1f, 0f, curtainOpenSeconds);
            yield return curtain.Vanish(vanishTime);
            Opened?.Invoke();
            yield return new WaitForSeconds(darkSeconds); // 暗いホールで拍手を聞かせてから
            // 4. 明るくなり始めると同時に演奏が始まり、ステージ全体が lightRiseSeconds かけてゆっくり明るくなる (ホールの環境光も GameManager が同時に上げる)。
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
            StopPlaying(false);
            Sfx.Play("applause", 0.85f);
            float vol = music != null ? music.volume : 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
            {
                if (music != null) music.volume = vol * (1f - t);
                Groove = 1f - t;
                SetLights(1f - t);
                yield return null;
            }
            if (music != null) music.Stop();
            Groove = 0f;
            SetLights(0f);
            // 2. カーテンが閉まる。閉まっている間にホールを消して、屋台を戻す
            yield return curtain.Appear(appearTime);
            PlayClip(ConcertMusic.Curtain, curtainVolume);
            yield return curtain.Draw(0f, 1f, curtainCloseSeconds);
            SetTargets(false);
            if (hall != null) hall.SetActive(false);
            SetLift(0f); // 閉まっている間に、その場で元の地面の高さへ戻す
            if (platform != null) platform.SetActive(false);
            CoveredBack?.Invoke(false);
            yield return new WaitForSeconds(0.4f);
            // 3. カーテンが開いて消えると、元の屋台の前
            PlayClip(ConcertMusic.CurtainOpen, curtainVolume);
            yield return curtain.Draw(1f, 0f, curtainOpenSeconds);
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
            soloUntil.Clear();
            for (int i = 0; i < soloCount; i++) PickSoloist();
            SpawnStaff();
        }

        // BGM を流し始める (曲ができていれば)。演奏の途中から始める時は、今の拍の位置から鳴らして、楽器の揺れとずれないようにする
        void TryStartMusic()
        {
            var clip = CurrentBgm;
            if (!bgm || music == null || clip == null || music.isPlaying) return;
            music.clip = clip;
            music.volume = musicVolume;
            float loopBeats = (clip.length - FirstBeat) / BeatSec;
            loops = Mathf.FloorToInt(idleBeatClock / loopBeats);
            float raw = idleBeatClock - loops * loopBeats;
            lastRawBeat = raw;
            float time = idleBeatClock <= StartBeat ? StartAt : FirstBeat + raw * BeatSec; // 演奏の始まりからなら、流し始める所 (bgmSkipSeconds) から
            music.timeSamples = Mathf.Clamp(Mathf.RoundToInt(time * clip.frequency), 0, clip.samples - 1);
            music.Play();
        }

        void StopPlaying(bool instant)
        {
            playing = false;
            foreach (var s in soloists) if (s != null) s.SetSolo(false);
            soloists.Clear();
            soloUntil.Clear();
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
            for (int i = 0; i < followSpots.Length; i++)
                if (followSpots[i] != null)
                {
                    followSpots[i].intensity = followBase[i] * k;
                    followSpots[i].enabled = k > 0.01f;
                }
            if (houseLight != null) houseLight.intensity = houseBase * Mathf.Lerp(1f, housePlay, k);
            if (showWhilePlaying != null)
                foreach (var go in showWhilePlaying) if (go != null) go.SetActive(k > 0.3f);
        }

        // ---- ソロ ----

        void PickSoloist()
        {
            var candidates = new List<MusicInstrument>();
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
            soloists.Add(pick);
            soloUntil[pick] = Beat + soloBeats * Random.Range(0.8f, 1.2f);
            UpdateBlockers();
        }

        void EndSolo(MusicInstrument inst)
        {
            if (inst == null) return;
            inst.SetSolo(false);
            soloists.Remove(inst);
            soloUntil.Remove(inst);
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

        IEnumerator PassSoloAfter(MusicInstrument inst, float delay)
        {
            soloUntil[inst] = float.MaxValue; // 交代するまで、時間切れで交代させない
            yield return new WaitForSeconds(delay);
            if (soloists.Contains(inst)) EndSolo(inst);
        }

        // ---- 浮き床 ----

        // 浮き床 (とカーテン) とプレイヤーを、地面から h (m) の高さにする
        void SetLift(float h)
        {
            if (lift != null) lift.localPosition = Vector3.up * h;
            if (player != null)
            {
                var p = player.localPosition;
                player.localPosition = new Vector3(p.x, playerStartY + h, p.z);
            }
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
            StartCoroutine(PassSoloAfter(inst, 0.9f)); // 少し見せてから、ソロが別の楽器へ移る
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
            foreach (var s in staffs) if (s != null && !s.IsBusy) { staff = s; break; }
            if (staff == null) return;

            float side = 0f, level = -1f;
            foreach (float sd in new[] { staffSide, -staffSide })
            {
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
                float raw = (music.timeSamples / (float)music.clip.frequency - FirstBeat) / BeatSec;
                if (raw < lastRawBeat - 1f) loops++; // 曲の頭に戻った
                lastRawBeat = raw;
                Beat = loops * (music.clip.length - FirstBeat) / BeatSec + raw;
            }
            else
            {
                // 曲を流していない演奏中は、曲のテンポどおりに進める
                idleBeatClock += playing ? Time.deltaTime / BeatSec : Time.deltaTime * 0.6f;
                Beat = idleBeatClock;
            }

            if (!playing) return;
            // 楽器の動き: 演奏の始まりは控えめで、grooveRiseSeconds かけて最高潮へ。あとはずっと最高潮
            Groove = Mathf.Lerp(grooveStart, 1f, Mathf.SmoothStep(0f, 1f, (Time.time - playStartTime) / Mathf.Max(0.01f, grooveRiseSeconds)));
            TryStartMusic(); // 演奏が始まった時に曲がまだできていなかったら、できしだい流す
            UpdateBaton();
            UpdateFollowSpots();

            // ソロの交代
            if (Time.time >= nextSoloCheck)
            {
                nextSoloCheck = Time.time + 0.25f;
                foreach (var s in new List<MusicInstrument>(soloists))
                    if (soloUntil.TryGetValue(s, out var until) && Beat >= until && until < float.MaxValue) EndSolo(s);
                while (soloists.Count < soloCount && playing) { int before = soloists.Count; PickSoloist(); if (soloists.Count == before) break; }
            }
            // 五線譜を次々に出す (何本も出てきては途切れる)
            if (Time.time - lastStaffTime > staffEvery) SpawnStaff();
        }

        // 指揮棒: 4拍子の図形 (下 → 左 → 右 → 上) を描く
        void UpdateBaton()
        {
            if (baton == null) return;
            float b = Mathf.Repeat(Beat, 4f);
            int i = Mathf.FloorToInt(b);
            float f = b - i;
            float e = f * f * (3f - 2f * f);
            float[] side = { 0f, -35f, 35f, 0f };
            float[] lift = { 25f, 5f, 5f, -20f };
            float z = Mathf.Lerp(side[i], side[(i + 1) % 4], e);
            float x = Mathf.Lerp(lift[i], lift[(i + 1) % 4], e) - 18f * Mathf.Exp(-f * 8f);
            baton.localRotation = batonBase * Quaternion.Euler(x, 0f, z);
        }

        // ソロの楽器を追いかけるスポットライト
        void UpdateFollowSpots()
        {
            for (int i = 0; i < followSpots.Length; i++)
            {
                var spot = followSpots[i];
                if (spot == null) continue;
                var target = i < soloists.Count ? soloists[i] : null;
                if (target == null) continue;
                Vector3 to = target.AimPoint - spot.transform.position;
                var want = Quaternion.LookRotation(to.normalized);
                spot.transform.rotation = Quaternion.Slerp(spot.transform.rotation, want, Time.deltaTime * 4f);
                if (i < followBeams.Length && followBeams[i] != null)
                {
                    var beam = followBeams[i];
                    beam.position = spot.transform.position;
                    beam.rotation = spot.transform.rotation;
                    beam.localScale = new Vector3(1f, 1f, Mathf.Max(0.1f, to.magnitude));
                }
            }
        }

        void Play(IEnumerator routine)
        {
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(routine);
        }
    }
}
