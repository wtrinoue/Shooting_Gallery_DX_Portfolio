using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GameManager のうち、モードの BGM (夏祭り・幽霊・怪獣・鉱山・海戦・西部劇・宇宙・潜水艦・忍者・アンノウン)。
    /// モードが始まると曲を頭から、ふわっと大きくしながら流し、終わるとふわっと小さくして止める。
    /// (カジノ・美術館・音楽・空中ステージは、それぞれのモードが自分で BGM を鳴らす)
    /// </summary>
    public partial class GameManager
    {
        [Header("モードの BGM")]
        public AudioClip festivalBgm;
        public AudioClip ghostBgm;
        public AudioClip kaijuBgm;
        public AudioClip mineBgm;
        public AudioClip navalBgm;
        public AudioClip westernBgm;
        public AudioClip spaceBgm;
        public AudioClip submarineBgm;
        public AudioClip ninjaBgm;
        public AudioClip unknownBgm;
        [Range(0f, 1f)] public float stageBgmVolume = 0.45f;
        [Tooltip("曲が大きくなりきるまで / 消えるまでの秒数")]
        public float stageBgmFadeIn = 1.5f;
        public float stageBgmFadeOut = 1.5f;

        AudioSource stageBgm;
        AudioClip stageBgmClip;
        float stageBgmTarget;

        /// <summary>モードの BGM を頭から流す (ふわっと大きく)</summary>
        void PlayStageBgm(AudioClip clip)
        {
            if (clip == null) return;
            if (stageBgm == null)
            {
                stageBgm = gameObject.AddComponent<AudioSource>();
                stageBgm.playOnAwake = false;
                stageBgm.loop = true;
                stageBgm.spatialBlend = 0f; // どこにいても同じ大きさ
            }
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            stageBgmClip = clip;
            stageBgm.clip = clip;
            stageBgm.time = 0f;
            stageBgm.volume = 0f;
            stageBgm.Play();
            stageBgmTarget = stageBgmVolume;
            stageBgmStopAfterStalls = null; // (前のモードの「屋台が戻ったら止める」は取り消す)
        }

        AudioClip stageBgmStopAfterStalls; // 屋台が戻りきったら止める曲 (StopStageBgmAfterStalls)

        /// <summary>
        /// モードが終わった: 曲は屋台が戻ってくるまで流し続け、戻りきったら (stallsReturnUntil) ふわっと止める (ユーザーの指定)。
        /// immediate (ラウンドの終わり・リセット) ならすぐ止める
        /// </summary>
        void StopStageBgmAfterStalls(AudioClip clip, bool immediate)
        {
            if (immediate) { StopStageBgm(clip, true); return; }
            if (stageBgm == null || stageBgmClip != clip) return;
            stageBgmStopAfterStalls = clip;
        }

        // 屋台が戻りきって、次のモードも始まっていなければ、流し続けていた曲を止める (毎フレーム)
        void UpdateStageBgmStopAfterStalls()
        {
            if (stageBgmStopAfterStalls == null) return;
            if (State != GameState.Playing) { StopStageBgm(stageBgmStopAfterStalls, true); stageBgmStopAfterStalls = null; return; }
            if (AnyWorldMode || Time.time < stallsReturnUntil) return;
            StopStageBgm(stageBgmStopAfterStalls);
            stageBgmStopAfterStalls = null;
        }

        /// <summary>モードの BGM を止める (clip を渡した時は、その曲が流れている時だけ)。immediate ならすぐ止める</summary>
        void StopStageBgm(AudioClip clip = null, bool immediate = false)
        {
            if (stageBgm == null || (clip != null && stageBgmClip != clip)) return;
            stageBgmTarget = 0f;
            if (immediate)
            {
                stageBgm.volume = 0f;
                stageBgm.Stop();
            }
        }

        // ---- ラウンドの BGM (2 MIN / 100,000 POINTS / エンドレス。WORLD GATE とデバッグは無音) ----

        [Tooltip("ラウンドの BGM (タイトルの的を撃った時から流れる。別の世界のモードの間は小さくして一時停止し、戻ったら続きから)")]
        public AudioClip roundBgm;
        [Range(0f, 1f)] public float roundBgmVolume = 0.4f;
        /// <summary>2 MIN のタイムアタックで、別の世界から戻った時に曲の位置を残り時間に合わせる境目 (秒。1分42秒)</summary>
        const float TimeAttackBgmSync = 102f;

        AudioSource roundBgmSource;
        bool roundBgmOn;

        /// <summary>ラウンドの BGM を頭から流す (看板が 3, 2, 1 と数え始めた時。エンドレスは始まった時)</summary>
        void PlayRoundBgm()
        {
            if (roundBgm == null) return;
            if (roundBgmSource == null)
            {
                roundBgmSource = gameObject.AddComponent<AudioSource>();
                roundBgmSource.playOnAwake = false;
                roundBgmSource.loop = true;
                roundBgmSource.spatialBlend = 0f;
            }
            if (roundBgm.loadState == AudioDataLoadState.Unloaded) roundBgm.LoadAudioData();
            roundBgmSource.clip = roundBgm;
            roundBgmSource.time = 0f;
            roundBgmSource.volume = roundBgmVolume; // (頭から、すぐにいつもの大きさで。カウントダウンの 3 とそろうように)
            roundBgmSource.Play();
            roundBgmOn = true;
        }

        /// <summary>ラウンドの BGM を、その場ですぐ一時停止する (別の世界から戻ったら、いつもどおり続きから流れる)</summary>
        void PauseRoundBgmNow()
        {
            if (roundBgmSource == null || !roundBgmSource.isPlaying) return;
            roundBgmSource.volume = 0f;
            roundBgmSource.Pause();
        }

        /// <summary>ラウンドの BGM を止める (immediate でなければ、ふわっと小さくしてから)</summary>
        void StopRoundBgm(bool immediate = false)
        {
            roundBgmOn = false;
            if (roundBgmSource != null && immediate) roundBgmSource.Stop();
        }

        // 別の世界のモード (とその BGM) の間は、ラウンドの BGM を小さくして一時停止し、戻ったら続きから流す
        bool AnyWorldMode => NavalActive || SkyActive || NinjaActive || KaijuActive || CasinoActive || WesternActive || SpaceActive || GhostActive
                             || MineActive || FestivalActive || SubmarineActive || MusicActive || MuseumActive || UnknownActive;
        bool WorldModeActive => AnyWorldMode || (stageBgm != null && stageBgm.isPlaying);

        void UpdateRoundBgm()
        {
            if (roundBgmSource == null || roundBgmSource.clip == null) return;
            bool want = roundBgmOn && !WorldModeActive;
            float target = want ? roundBgmVolume : 0f;
            if (want && !roundBgmSource.isPlaying)
            {
                // 別の世界から戻ってきた: 一時停止していた続きから。
                // 2 MIN のタイムアタックで残りが TimeAttackBgmSync 秒 (1分42秒) 以下の時は、曲の「1分42秒 − 残り時間」の所から
                if (TimeAttack && timeLeft <= TimeAttackBgmSync)
                    roundBgmSource.time = Mathf.Clamp(TimeAttackBgmSync - timeLeft, 0f, Mathf.Max(0f, roundBgmSource.clip.length - 0.05f));
                roundBgmSource.UnPause();
            }
            roundBgmSource.volume = Mathf.MoveTowards(roundBgmSource.volume, target, Time.deltaTime * roundBgmVolume / 1.2f);
            if (!want && roundBgmSource.isPlaying && roundBgmSource.volume <= 0f)
            {
                if (roundBgmOn) roundBgmSource.Pause(); // 別の世界の間は一時停止
                else roundBgmSource.Stop();
            }
        }

        // ---- タイトルの操作方法の看板を見ている間は、手元をコントローラーの模型に ----

        Transform controlsBoardFace; // 操作方法の看板 (TitleBoards の中の ControlsBoard)
        const float ControllerViewAngle = 35f; // 看板の方からこの角度 (度) 以内を向いていたら、見ていることにする

        GameObject controlsVR, controlsPC; // 操作方法の看板の2通りの説明 (VR のコントローラー / マウスとキーボード)

        void UpdateControllerView()
        {
            if (player == null) return;
            UpdateControlsBoardMode();
            bool on = false;
            // (手元のコントローラーの模型は VR の時だけ。PC では銃のまま、CREDITS の的も銃で撃てる)
            if (player.UsingVRControls && State == GameState.Title && controlsBoard != null && controlsBoard.activeInHierarchy && (goalSign == null || !goalSign.IsPlaying))
            {
                if (controlsBoardFace == null) controlsBoardFace = controlsBoard.transform.Find("ControlsBoard");
                if (controlsBoardFace != null && player.head != null)
                {
                    Vector3 to = controlsBoardFace.position - player.head.transform.position;
                    Vector3 fwd = player.head.transform.forward;
                    to.y = 0f;
                    fwd.y = 0f;
                    on = to.sqrMagnitude > 0.01f && fwd.sqrMagnitude > 0.01f && Vector3.Angle(to, fwd) < ControllerViewAngle;
                }
            }
            player.SetControllerView(on);
        }

        bool controlsShowVR;   // 操作方法の看板に、VR の説明を出しているか (false = マウス・キーボード)
        bool controlsManual;   // 看板の「VR ⇔ PC」のボタンで一度でも切り替えたか (切り替えた後は、入力では変えない)

        /// <summary>看板の「VR ⇔ PC」のボタンを撃った: 説明を入れ替える。以後は入力に合わせて自動では変えない</summary>
        void ToggleControlsDisplay()
        {
            controlsManual = true;
            controlsShowVR = !controlsShowVR;
            Sfx.Play("click");
            UpdateControlsBoardMode();
        }

        // 操作方法の看板: VR かマウス・キーボードの説明の片方だけを出す。
        // ボタンで切り替えるまでは、今どちらの操作で遊んでいるか (PlayerRig.UsingVRControls) に合わせる (最初に見せる説明が、今の操作の方になるように)
        void UpdateControlsBoardMode()
        {
            if (!controlsManual) controlsShowVR = player.UsingVRControls;
            if (controlsBoard == null) return;
            if (controlsVR == null || controlsPC == null)
            {
                var vr = controlsBoard.transform.Find("ControlsBoard/VRControls");
                var pc = controlsBoard.transform.Find("ControlsBoard/PCControls");
                if (vr == null || pc == null) return;
                controlsVR = vr.gameObject;
                controlsPC = pc.gameObject;
            }
            bool useVR = controlsShowVR;
            if (controlsVR.activeSelf != useVR) controlsVR.SetActive(useVR);
            if (controlsPC.activeSelf == useVR) controlsPC.SetActive(!useVR);
        }

        // 曲の大きさを目標へ近づける (止めると決めた曲は、消えきったら止める)
        void UpdateStageBgm()
        {
            UpdateStageBgmStopAfterStalls();
            UpdateRoundBgm();
            if (stageBgm == null || !stageBgm.isPlaying) return;
            float fade = stageBgmTarget > stageBgm.volume ? stageBgmFadeIn : stageBgmFadeOut;
            stageBgm.volume = Mathf.MoveTowards(stageBgm.volume, stageBgmTarget, Time.deltaTime * stageBgmVolume / Mathf.Max(0.05f, fade));
            if (stageBgmTarget <= 0f && stageBgm.volume <= 0f) stageBgm.Stop();
        }
    }
}
