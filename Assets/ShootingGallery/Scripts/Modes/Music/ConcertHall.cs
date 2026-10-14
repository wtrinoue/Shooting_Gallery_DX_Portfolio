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
    ///   4. 薄暗いホールで演奏が始まると (MusicStarted)、ステージがぱっと明るく照らされ、楽器が曲に合わせて揺れる。
    ///   的: ソロを弾いている楽器 (スポットライトが当たり、点数の札が出ている)。撃つと跳ねて点数が入り、空中に波打つ五線譜が流れてくる。
    ///       五線譜を流れてくる音符も的 (撃つとその高さの音が鳴る)。ソロでない楽器は撃つと跳ねて音が鳴るだけ。
    ///   終わる時は行きと同じ: 演奏が止まって暗くなり、カーテンが閉まって (CoveredBack) ホールが消え、開くと元の屋台の前。
    /// </summary>
    public class ConcertHall : MonoBehaviour
    {
        [Header("ホール (始まるまで隠しておく)")]
        public GameObject hall;
        public StageCurtain curtain;
        public float appearTime = 0.9f, closeTime = 2f, holdTime = 0.3f, openTime = 2f, vanishTime = 0.9f;

        [Header("浮き床 (カーテンが閉まっている間に、プレイヤーを床ごと客席の上へ持ち上げる。ステージ全体が見えるように)")]
        [Tooltip("持ち上げる物 (カーテンと浮き床) / 浮き床 (ホールが出ている間だけ出す)")]
        public Transform lift;
        public GameObject platform;
        public float liftHeight = 2.6f, liftTime = 2.2f;

        [Header("照明")]
        [Tooltip("ステージを照らす明かり (演奏前は dimLevel の明るさ、演奏が始まると 100%)")]
        public Light[] stageLights;
        [Range(0f, 1f)] public float dimLevel = 0.12f;
        [Tooltip("客席を照らす明かり (演奏が始まると houseDim まで暗くする)")]
        public Light houseLight;
        [Range(0f, 1f)] public float houseDim = 0.45f;
        [Tooltip("ソロの楽器を追いかけるスポットライトと、その光の筋 (光と同じ向きに伸ばす。長さ 1 の円すい)")]
        public Light[] followSpots;
        public Transform[] followBeams;
        [Tooltip("演奏中だけ点ける物 (ステージの縁の明かり・光の筋など)")]
        public GameObject[] showWhilePlaying;
        public float lightUpTime = 1.5f;

        [Header("楽器")]
        public MusicInstrument[] instruments;
        [Tooltip("同時にソロを弾く楽器の数 / 1つの楽器がソロを続ける拍の数")]
        public int soloCount = 2;
        public float soloBeats = 16f;
        [Tooltip("指揮者の譜面台の上で拍子をとる指揮棒")]
        public Transform baton;

        [Header("五線譜")]
        public MusicStaff[] staffs;
        [Tooltip("五線譜を次々に出す間隔 (秒。空いている五線譜がある時)")]
        public float staffInterval = 3.2f;
        [Tooltip("五線譜が飛び出すステージ側の位置 (z。プレイヤーからの距離)")]
        public float staffStartZ = 10.5f;

        [Header("音")]
        [Range(0f, 1f)] public float musicVolume = 0.55f;

        [Header("時間")]
        [Tooltip("音楽モードの長さ (秒。カーテンが閉まり始めてから)")]
        public float duration = 52f;

        public bool IsActive { get; private set; }
        public float TimeLeft => IsActive ? Mathf.Max(0f, duration - (Time.time - startTime)) : 0f;
        public int Hits { get; private set; }
        /// <summary>時間切れから、カーテンが消えて元の屋台が見えるまでの秒数</summary>
        public float CloseTime => 1.2f + appearTime + closeTime + liftTime + 0.4f + openTime + vanishTime;

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
            ConcertMusic.Build(this); // 曲と楽器の音を作っておく (タイトルの間に)
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
            PlayClip(ConcertMusic.Curtain, 0.7f);
            yield return curtain.Draw(0f, 1f, closeTime);
            // 2. 閉まっている間に、外をコンサートホールにする (演奏前の薄暗いステージ)
            SetTargets(true); // 隠れたまま的を立てておく (出す時に起き上がる動きを見せない)
            if (hall != null) hall.SetActive(true);
            SetLights(0f);
            if (platform != null) platform.SetActive(true);
            Covered?.Invoke();
            yield return MoveLift(0f, liftHeight); // 浮き床ごと、客席の上へ上がる
            yield return new WaitForSeconds(holdTime);
            // 3. カーテンが逆向きに開いて、天井ごと消える。拍手
            PlayClip(ConcertMusic.Curtain, 0.7f);
            Sfx.Play("cheer", 0.6f);
            yield return curtain.Draw(1f, 0f, openTime);
            yield return curtain.Vanish(vanishTime);
            Opened?.Invoke();
            yield return new WaitForSeconds(0.8f);
            // 4. 演奏が始まり、ステージが明るくなる
            StartPlaying();
            MusicStarted?.Invoke();
            for (float t = 0f; t < 1f; t += Time.deltaTime / lightUpTime)
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
            Sfx.Play("cheer", 0.7f);
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
            PlayClip(ConcertMusic.Curtain, 0.7f);
            yield return curtain.Draw(0f, 1f, closeTime);
            SetTargets(false);
            if (hall != null) hall.SetActive(false);
            yield return MoveLift(liftHeight, 0f); // 浮き床ごと、元の地面へ下りる
            if (platform != null) platform.SetActive(false);
            CoveredBack?.Invoke(false);
            yield return new WaitForSeconds(0.4f);
            // 3. カーテンが開いて消えると、元の屋台の前
            PlayClip(ConcertMusic.Curtain, 0.7f);
            yield return curtain.Draw(1f, 0f, openTime);
            yield return curtain.Vanish(vanishTime);
            Closed?.Invoke();
            anim = null;
        }

        void StartPlaying()
        {
            playing = true;
            Groove = 1f;
            loops = 0;
            lastRawBeat = 0f;
            Beat = 0f;
            idleBeatClock = 0f;
            if (music != null && ConcertMusic.Loop != null)
            {
                music.clip = ConcertMusic.Loop;
                music.volume = musicVolume;
                music.timeSamples = 0;
                music.Play();
            }
            soloists.Clear();
            soloUntil.Clear();
            for (int i = 0; i < soloCount; i++) PickSoloist();
            SpawnStaff();
        }

        void StopPlaying(bool instant)
        {
            playing = false;
            foreach (var s in soloists) if (s != null) s.SetSolo(false);
            soloists.Clear();
            soloUntil.Clear();
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
                if (stageLights[i] != null) stageLights[i].intensity = stageBase[i] * Mathf.Lerp(dimLevel, 1f, k);
            for (int i = 0; i < followSpots.Length; i++)
                if (followSpots[i] != null)
                {
                    followSpots[i].intensity = followBase[i] * k;
                    followSpots[i].enabled = k > 0.01f;
                }
            if (houseLight != null) houseLight.intensity = houseBase * Mathf.Lerp(1f, houseDim, k);
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
        }

        void EndSolo(MusicInstrument inst)
        {
            if (inst == null) return;
            inst.SetSolo(false);
            soloists.Remove(inst);
            soloUntil.Remove(inst);
            if (playing) PickSoloist();
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

        IEnumerator MoveLift(float from, float to)
        {
            Sfx.Play("door", 0.5f); // ゴゴゴ…とせり上がる / 下りる
            for (float t = 0f; t < 1f; t += Time.deltaTime / liftTime)
            {
                SetLift(Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
                yield return null;
            }
            SetLift(to);
        }

        // ---- トランペット (音楽モードの武器) ----

        /// <summary>トランペットで音符を撃った時の音: 今の和音に合う高さで、パッと鳴らす (GunController / Sfx から)</summary>
        public static void PlayTrumpetShot(Vector3 pos)
        {
            if (Instance == null) return;
            int m = ConcertMusic.ChordToneNear(Instance.Beat, Random.Range(67, 80));
            Instance.PlayVoice(ConcertMusic.Voice.Brass, m, pos, 0.45f);
        }

        public static ConcertHall Instance { get; private set; }

        // ---- 当たった (GameManager から) ----

        /// <summary>楽器に当たった: 跳ねて音が鳴る。ソロの楽器なら点数を返し、五線譜を出す (ソロでなければ 0)</summary>
        public int OnInstrumentHit(Target target, Vector3 point)
        {
            if (!IsActive || !playing) return 0;
            var inst = target.GetComponentInParent<MusicInstrument>();
            if (inst == null) return 0;
            int m = ConcertMusic.ChordToneNear(Beat, inst.midi);
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
            PlayVoice(ConcertMusic.Voice.Bell, SlotMidi[Mathf.Clamp(note.slot, 0, SlotMidi.Length - 1)] + 12, note.transform.position, 0.9f);
        }

        // ---- 五線譜 ----

NEWSPAWN

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
            if (music != null && music.isPlaying)
            {
                float raw = music.timeSamples / (float)ConcertMusic.Rate / ConcertMusic.BeatSeconds;
                if (raw < lastRawBeat - 1f) loops++; // 曲の頭に戻った
                lastRawBeat = raw;
                Beat = loops * ConcertMusic.LoopBeats + raw;
            }
            else
            {
                idleBeatClock += Time.deltaTime * 0.6f;
                Beat = idleBeatClock;
            }

            if (!playing) return;
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
            if (Time.time - lastStaffTime > staffInterval) SpawnStaff();
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
