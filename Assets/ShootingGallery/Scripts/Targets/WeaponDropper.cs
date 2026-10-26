using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 普通の点数の的が起き上がる時に、ときどき武器の的 (MG / SG / BOMB) に化けさせる。
    /// 的そのものが化けるので、コンベアーなら流れ、観覧車なら回り、もぐら叩きなら穴から出る…と
    /// 屋台ごとの動きにそのまま従う。倒されるか一定時間たつと元の的に戻る。
    /// 出やすさは マシンガン > ショットガン > 爆弾。
    /// </summary>
    public class WeaponDropper : MonoBehaviour
    {
        [Tooltip("武器の的の見た目 (使い回す)。TargetKind が名前の最後に付いた子 (Look_MachineGun など)")]
        public GameObject[] looks;

        [Header("出やすさ (合計が 1 でなくてもよい)")]
        public float machineGunWeight = 6f;
        public float shotgunWeight = 3f;
        public float bombWeight = 1f;

        [Header("Timing")]
        [Tooltip("武器の的が出てから次に出るまでの間隔 (秒)")]
        public Vector2 interval = new Vector2(4f, 7f);
        public Vector2 feverInterval = new Vector2(2f, 4f);
        [Tooltip("起き上がった的が化ける確率 (間隔が空いている時だけ)")]
        [Range(0f, 1f)] public float chance = 0.35f;
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float lifetime = 6f;
        public int maxActive = 2;
        public int weaponPoints = 100;

        [Header("爆弾の的 (当てると爆発して周りの的を倒す。武器の的とは別枠で出る)")]
        public Vector2 explosiveInterval = new Vector2(5f, 9f);
        public Vector2 explosiveFeverInterval = new Vector2(2.5f, 5f);
        [Range(0f, 1f)] public float explosiveChance = 0.3f;
        public int maxExplosives = 1;
        public int explosivePoints = 50;

        [Header("青い玉 (フィーバー中にメイン以外の屋台に出る。撃つとボーナスステージ)")]
        [Range(0f, 1f)] public float blueOrbChance = 0.12f;
        public int blueOrbPoints = 300;
        public float blueOrbLifetime = 8f;
        float nextBlueOrbTime;

        [Header("スナイパーの的 (撃つとスナイパーチャレンジ。同時に1つまで。屋台の拡張に巻き込まれない時だけ出る)")]
        [Tooltip("スナイパーの的が消えて (撃たれて / 時間切れで) から次に出るまでの間隔 (秒)")]
        public Vector2 sniperGap = new Vector2(6f, 12f);
        [Tooltip("フィーバー中の間隔 (秒)。フィーバー中はメイン以外の屋台に出る")]
        public Vector2 sniperFeverGap = new Vector2(3f, 6f);
        [Tooltip("ゲーム開始から最初のスナイパーの的が出るまでの秒数")]
        public Vector2 sniperStartDelay = new Vector2(4f, 7f);
        [Tooltip("フィーバーが始まってから最初のスナイパーの的が出るまでの秒数 (屋台がせり上がり終わってから)")]
        public Vector2 sniperFeverStartDelay = new Vector2(1.5f, 3f);
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float sniperLifetime = 5f;
        public int sniperTargetPoints = 100;
        [Tooltip("出す屋台を決めてから、その屋台の的が起き上がるのを待つ最長の秒数 (過ぎたら選び直す)")]
        public float sniperReserveTimeout = 4f;
        float nextSniperTime;

        [Header("マシンガンチャレンジの的 (撃つとマシンガンチャレンジ。出し方はスナイパーの的と同じで、チャレンジの的は全部合わせて同時に1つまで)")]
        [Tooltip("マシンガンチャレンジが終わってから、次のマシンガンチャレンジの的が出るまでの間隔 (秒)")]
        public Vector2 mgChallengeGap = new Vector2(10f, 16f);
        [Tooltip("フィーバー中の間隔 (秒)")]
        public Vector2 mgChallengeFeverGap = new Vector2(5f, 9f);
        [Tooltip("ゲーム開始から最初のマシンガンチャレンジの的が出るまでの秒数")]
        public Vector2 mgChallengeStartDelay = new Vector2(12f, 18f);
        [Tooltip("フィーバーが始まってから最初のマシンガンチャレンジの的が出るまでの秒数 (屋台がせり上がり終わってから)")]
        public Vector2 mgChallengeFeverStartDelay = new Vector2(4f, 7f);
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float mgChallengeLifetime = 5f;
        public int mgChallengeTargetPoints = 100;
        [Tooltip("チャレンジが終わってから、もう一方のチャレンジの的が出るまでの最短の秒数 (続けて出すぎないように)")]
        public Vector2 challengeSeparation = new Vector2(3f, 5f);
        [Tooltip("ラウンドの残り時間がこの秒数になってもまだ1回も出ていないチャレンジの的は、すぐに出す (スナイパーもマシンガンも1ラウンドに必ず1回は出るように)")]
        public float guaranteeByTimeLeft = 55f;
        float nextMgChallengeTime;
        float separatedUntil;              // チャレンジが終わってから、次のチャレンジの的を出してよくなる時刻
        bool sniperShown, mgChallengeShown; // このラウンドでもう出たか

        [Header("DELUXE の的 (撃つと出ている屋台が全部豪華な屋台に入れ替わる。同時に1つまで)")]
        [Tooltip("ゲーム開始から最初に出せるようになるまでの秒数")]
        public Vector2 deluxeStartDelay = new Vector2(15f, 25f);
        [Tooltip("豪華タイムが終わってから、次に出せるようになるまでの秒数")]
        public Vector2 deluxeGap = new Vector2(20f, 30f);
        [Tooltip("出せる時に、起き上がった的が化ける確率")]
        [Range(0f, 1f)] public float deluxeChance = 0.25f;
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float deluxeLifetime = 6f;
        public int deluxeTargetPoints = 300;
        float nextDeluxeTime;
        bool wasDeluxe;

        [Header("錨の的 (撃つと海戦モード。同時に1つまで)")]
        [Tooltip("ゲーム開始から最初に出せるようになるまでの秒数")]
        public Vector2 navalStartDelay = new Vector2(25f, 40f);
        [Tooltip("海戦モードが終わってから、次に出せるようになるまでの秒数")]
        public Vector2 navalGap = new Vector2(25f, 35f);
        [Tooltip("出せる時に、起き上がった的が化ける確率")]
        [Range(0f, 1f)] public float navalChance = 0.25f;
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float navalLifetime = 6f;
        public int navalTargetPoints = 300;
        float nextNavalTime;
        bool wasNaval;

        [Header("プロペラの的 (撃つと空中ステージ。同時に1つまで。錨の的とも同時には出ない)")]
        [Tooltip("ゲーム開始から最初に出せるようになるまでの秒数")]
        public Vector2 skyStartDelay = new Vector2(35f, 55f);
        [Tooltip("空中ステージ / 海戦モードが終わってから、次に出せるようになるまでの秒数")]
        public Vector2 skyGap = new Vector2(25f, 35f);
        [Tooltip("出せる時に、起き上がった的が化ける確率")]
        [Range(0f, 1f)] public float skyChance = 0.25f;
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float skyLifetime = 6f;
        public int skyTargetPoints = 300;
        float nextSkyTime;
        bool wasSky;

        [Header("手裏剣の的 (撃つと忍者モード。同時に1つまで。錨・プロペラの的とも同時には出ない)")]
        [Tooltip("ゲーム開始から最初に出せるようになるまでの秒数")]
        public Vector2 ninjaStartDelay = new Vector2(30f, 50f);
        [Tooltip("忍者モード / 海戦モード / 空中ステージが終わってから、次に出せるようになるまでの秒数")]
        public Vector2 ninjaGap = new Vector2(25f, 35f);
        [Tooltip("出せる時に、起き上がった的が化ける確率")]
        [Range(0f, 1f)] public float ninjaChance = 0.25f;
        [Tooltip("撃たれずにこの秒数たったら引っ込む")]
        public float ninjaLifetime = 6f;
        public int ninjaTargetPoints = 300;
        float nextNinjaTime;
        bool wasNinja;

        /// <summary>怪獣 / カジノ / 西部劇モードの的の出し方 (どれも同じ決まりなので1つにまとめた)</summary>
        [System.Serializable]
        public class StageOffer
        {
            public TargetKind kind;
            [Tooltip("ゲーム開始から最初に出せるようになるまでの秒数")]
            public Vector2 startDelay = new Vector2(35f, 55f);
            [Tooltip("どれかのモードが終わってから、次に出せるようになるまでの秒数")]
            public Vector2 gap = new Vector2(25f, 35f);
            [Tooltip("出せる時に、起き上がった的が化ける確率")]
            [Range(0f, 1f)] public float chance = 0.25f;
            [Tooltip("撃たれずにこの秒数たったら引っ込む")]
            public float lifetime = 6f;
            public int points = 300;
            [System.NonSerialized] public float next;
        }

        [Header("怪獣 / カジノ / 西部劇 / 宇宙の的 (撃つとそれぞれのモード。海戦などの的とも同時には出ない)")]
        public StageOffer kaijuOffer = new StageOffer { kind = TargetKind.Kaiju, startDelay = new Vector2(30f, 55f) };
        public StageOffer casinoOffer = new StageOffer { kind = TargetKind.Casino, startDelay = new Vector2(35f, 60f) };
        public StageOffer westernOffer = new StageOffer { kind = TargetKind.Western, startDelay = new Vector2(30f, 60f) };
        public StageOffer spaceOffer = new StageOffer { kind = TargetKind.Space, startDelay = new Vector2(30f, 60f) };
        public StageOffer ghostOffer = new StageOffer { kind = TargetKind.Ghost, startDelay = new Vector2(30f, 60f) };
        public StageOffer mineOffer = new StageOffer { kind = TargetKind.Mine, startDelay = new Vector2(30f, 60f) };
        public StageOffer submarineOffer = new StageOffer { kind = TargetKind.Submarine, startDelay = new Vector2(30f, 60f) };
        public StageOffer festivalOffer = new StageOffer { kind = TargetKind.Festival, startDelay = new Vector2(30f, 60f) };
        public StageOffer musicOffer = new StageOffer { kind = TargetKind.Music, startDelay = new Vector2(30f, 60f) };
        public StageOffer museumOffer = new StageOffer { kind = TargetKind.Museum, startDelay = new Vector2(30f, 60f) };
        public StageOffer unknownOffer = new StageOffer { kind = TargetKind.Unknown, startDelay = new Vector2(30f, 60f) };
        StageOffer[] offers; // (呼ぶたびに配列を作らないように、初めの1回だけ作る)
        StageOffer[] Offers => offers ??= new[] { kaijuOffer, casinoOffer, westernOffer, spaceOffer, ghostOffer, mineOffer, festivalOffer, submarineOffer, musicOffer, museumOffer, unknownOffer };
        bool wasStageMode;

        bool wasPlaying, wasFever, challengeWasBusy;
        TargetKind lastChallengeKind = TargetKind.Sniper;
        // チャレンジの的 (スナイパー / マシンガン) を出す予定の屋台 / 的 (どの屋台にも等しく出るよう、先に屋台をランダムに決めておく)
        TargetKind reservedKind;
        Stall reservedStall;
        Target reservedTarget;
        float reservedAt;

        class Disguise
        {
            public GameObject look;
            public TargetKind kind;
            public int originalPoints;
            public float since;
        }

        readonly Dictionary<Target, Disguise> active = new Dictionary<Target, Disguise>();
        readonly List<Target> toRestore = new List<Target>();
        float nextAllowedTime;
        float nextExplosiveTime;

        void OnEnable()
        {
            Target.Rising += OnTargetRising;
            GameManager.SpecialTargetsCancelled += RestoreAll;
        }

        void OnDisable()
        {
            Target.Rising -= OnTargetRising;
            GameManager.SpecialTargetsCancelled -= RestoreAll;
        }

        // フィーバーが始まった: 出ている特別な的を、全部点数の的に戻す (撃っても不発。予約も取り消す)
        void RestoreAll()
        {
            toRestore.Clear();
            toRestore.AddRange(active.Keys);
            foreach (var t in toRestore) Restore(t);
            toRestore.Clear();
            ClearChallengeReservation();
            foreach (var s in stagePlan) s.target = null;
        }

        void Start()
        {
            foreach (var look in looks) look.SetActive(false);
        }

        void Update()
        {
            // 消えた的は元に戻す / 長く出ている的は引っ込める
            toRestore.Clear();
            foreach (var pair in active)
            {
                if (!pair.Key.IsVisible) toRestore.Add(pair.Key);
                else if (pair.Key.IsUp && Time.time - pair.Value.since > LifetimeOf(pair.Value.kind) && !(GameManager.I != null && GameManager.I.DebugMode))
                    pair.Key.Lower(); // デバッグモードでは撃たれるまで出しっぱなし
            }
            foreach (var t in toRestore) Restore(t);

            // 豪華タイムが終わった所から次の DELUXE の的までの間隔を数える
            var gm = GameManager.I;
            bool deluxe = gm != null && gm.DeluxeActive;
            if (wasDeluxe && !deluxe) nextDeluxeTime = Time.time + Random.Range(deluxeGap.x, deluxeGap.y);
            wasDeluxe = deluxe;
            bool naval = gm != null && gm.NavalActive;
            bool sky = gm != null && gm.SkyActive;
            bool ninja = gm != null && gm.NinjaActive;
            // 海戦・空中ステージ・忍者モードが続けて来すぎないように、どれかが終わったら全部の間隔をあける
            bool stageMode = naval || sky || ninja || (gm != null && (gm.KaijuActive || gm.CasinoActive || gm.WesternActive || gm.SpaceActive || gm.GhostActive || gm.MineActive || gm.FestivalActive || gm.SubmarineActive || gm.MusicActive || gm.MuseumActive || gm.UnknownActive));
            if (wasStageMode && !stageMode)
            {
                foreach (var o in Offers) o.next = Mathf.Max(o.next, Time.time + Random.Range(o.gap.x, o.gap.y));
                nextNavalTime = Mathf.Max(nextNavalTime, Time.time + Random.Range(navalGap.x, navalGap.y));
                nextSkyTime = Mathf.Max(nextSkyTime, Time.time + Random.Range(skyGap.x, skyGap.y));
                nextNinjaTime = Mathf.Max(nextNinjaTime, Time.time + Random.Range(ninjaGap.x, ninjaGap.y));
            }
            wasNaval = naval;
            wasSky = sky;
            wasNinja = ninja;
            wasStageMode = stageMode;
            UpdateStagePlan(gm, stageMode, gm != null && gm.State == GameState.Playing);
            UpdateStageReservation(gm);

            UpdateChallengeReservation();
        }

        /// <summary>
        /// チャレンジの的 (スナイパー / マシンガン) を出す時が来たら、今出ている屋台からランダムに1軒選び、その屋台の立っている的を1つ
        /// いったん倒して起き上がらせる (起き上がる時に OnTargetRising で化ける)。
        /// フィーバー外はメイン屋台、フィーバー中はメイン以外の屋台 (フィーバー屋台) から選ぶ。
        /// 倒せる的がない屋台 (もぐら叩きなど) は、その屋台の的が次に起き上がるのを待つ。
        /// チャレンジの的は種類を合わせて同時に1つまで。両方の時間が来ていたら、先に時間が来た方を出す。
        /// </summary>
        void UpdateChallengeReservation()
        {
            var gm = GameManager.I;
            if (gm != null && gm.DebugMode)
            {   // デバッグモードはチャレンジの的も屋台ごとに決まった所に出すので、予約はしない
                ClearChallengeReservation();
                return;
            }
            bool playing = gm != null && gm.State == GameState.Playing;
            bool fever = playing && gm.IsFever;
            bool sniperBusy = playing && (gm.SniperActive || Count(TargetKind.Sniper) > 0);
            bool mgBusy = playing && (gm.MachineGunChallengeActive || Count(TargetKind.MachineGunChallenge) > 0);
            bool busy = sniperBusy || mgBusy;
            if (sniperBusy) lastChallengeKind = TargetKind.Sniper;
            else if (mgBusy) lastChallengeKind = TargetKind.MachineGunChallenge;

            if (playing && !wasPlaying) // ゲーム開始直後には出さない
            {
                stagePlan.Clear();
                stageClock = 0f;
                stageResumeAt = 0f;
                stageNextWindow = 0f;
                stageBag.Clear();
                PlanNextStage();
                nextSniperTime = Time.time + Random.Range(sniperStartDelay.x, sniperStartDelay.y);
                nextMgChallengeTime = Time.time + Random.Range(mgChallengeStartDelay.x, mgChallengeStartDelay.y);
                sniperShown = mgChallengeShown = false;
                separatedUntil = 0f;
                nextDeluxeTime = Time.time + Random.Range(deluxeStartDelay.x, deluxeStartDelay.y);
                nextNavalTime = Time.time + Random.Range(navalStartDelay.x, navalStartDelay.y);
                nextSkyTime = Time.time + Random.Range(skyStartDelay.x, skyStartDelay.y);
                nextNinjaTime = Time.time + Random.Range(ninjaStartDelay.x, ninjaStartDelay.y);
                foreach (var o in Offers) o.next = Time.time + Random.Range(o.startDelay.x, o.startDelay.y);
            }
            else if (fever && !wasFever) // フィーバーが始まったら早めに出す (屋台がせり上がる 1.2 秒も待つ)
            {
                nextSniperTime = Mathf.Min(nextSniperTime, Time.time + gm.stallMoveDuration + Random.Range(sniperFeverStartDelay.x, sniperFeverStartDelay.y));
                nextMgChallengeTime = Mathf.Min(nextMgChallengeTime, Time.time + gm.stallMoveDuration + Random.Range(mgChallengeFeverStartDelay.x, mgChallengeFeverStartDelay.y));
            }
            if (challengeWasBusy && !busy) // 前のチャレンジの的 / チャレンジが終わった所から間隔を数える
            {
                float separated = Time.time + Random.Range(challengeSeparation.x, challengeSeparation.y);
                separatedUntil = separated;
                if (lastChallengeKind == TargetKind.Sniper)
                {
                    var gap = fever ? sniperFeverGap : sniperGap;
                    nextSniperTime = Time.time + Random.Range(gap.x, gap.y);
                    nextMgChallengeTime = Mathf.Max(nextMgChallengeTime, separated);
                }
                else
                {
                    var gap = fever ? mgChallengeFeverGap : mgChallengeGap;
                    nextMgChallengeTime = Time.time + Random.Range(gap.x, gap.y);
                    nextSniperTime = Mathf.Max(nextSniperTime, separated);
                }
            }
            wasPlaying = playing;
            wasFever = fever;
            challengeWasBusy = busy;

            if (!playing || busy || gm.FeverAssembling)
            {
                ClearChallengeReservation();
                return;
            }
            if (reservedStall != null)
            {
                if (Time.time - reservedAt > sniperReserveTimeout || !reservedStall.IsSettled) ClearChallengeReservation();
                return;
            }
            // 1回も出ていないチャレンジは、ラウンドの残りが少なくなったら (前のチャレンジから少し間をあけて) すぐに出す
            if (gm.TimeLeft <= guaranteeByTimeLeft || gm.GoalProgress >= 0.6f) // (普通のラウンドは時間切れがないので、目標の点数の 6 割を超えたら)
            {
                float soon = Mathf.Max(Time.time, separatedUntil);
                if (!sniperShown) nextSniperTime = Mathf.Min(nextSniperTime, soon);
                if (!mgChallengeShown) nextMgChallengeTime = Mathf.Min(nextMgChallengeTime, soon);
            }
            bool sniperDue = Time.time >= nextSniperTime;
            bool mgDue = Time.time >= nextMgChallengeTime;
            if (!sniperDue && !mgDue) return;
            // 両方の時間が来ていたら、まだ出ていない方 → 先に時間が来た方 の順で選ぶ
            TargetKind kind;
            if (sniperDue && mgDue && sniperShown != mgChallengeShown)
                kind = sniperShown ? TargetKind.MachineGunChallenge : TargetKind.Sniper;
            else
                kind = sniperDue && (!mgDue || nextSniperTime <= nextMgChallengeTime) ? TargetKind.Sniper : TargetKind.MachineGunChallenge;

            var stalls = gm.ShownStalls(stallBuffer);
            if (fever) stalls.Remove(gm.mainStall); // フィーバー中はまわりの屋台に出す
            if (stalls.Count == 0) return;
            var stall = stalls[Random.Range(0, stalls.Count)];

            var candidates = targetBuffer;
            candidates.Clear();
            foreach (var t in stall.targets)
                if (t != null && t.kind == TargetKind.Normal && t.face != null && t.IsUp && !t.manualControl && !active.ContainsKey(t))
                    candidates.Add(t);
            Target pick = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
            if (!CanOfferChallenge(kind, pick != null ? pick : stall.targets.Length > 0 ? stall.targets[0] : null, LifetimeOf(kind) + 1f))
            {
                // 今は出せない (拡張が近い等)。少ししてから選び直す
                if (kind == TargetKind.Sniper) nextSniperTime = Time.time + 1f;
                else nextMgChallengeTime = Time.time + 1f;
                return;
            }

            reservedKind = kind;
            reservedStall = stall;
            reservedAt = Time.time;
            if (pick != null && pick.Reappear(0.4f)) reservedTarget = pick;
        }

        void ClearChallengeReservation()
        {
            reservedStall = null;
            reservedTarget = null;
        }

        bool CanOfferChallenge(TargetKind kind, Target target, float lifetime)
        {
            var gm = GameManager.I;
            return kind == TargetKind.Sniper ? gm.CanOfferSniper(target, lifetime) : gm.CanOfferMachineGunChallenge(target, lifetime);
        }

        [Header("別の世界へ飛ぶモードの的の出方 (5秒ごとに、その5秒のどこかのランダムな時刻に、3か所同時に出す)")]
        [Tooltip("区切りの秒数。区切りごとに、その中の 0〜この秒数 のランダムな時刻を抽選し、その時刻にまとめて出す")]
        public float stageBatchWindow = 5f;   // 名前を変えて、シーンに保存された前の値 (10秒ごとに1つ) を使わないようにしている
        [Tooltip("1回に同時に出す数 (それぞれ別の種類・ランダムな別の的)")]
        public int stageBatchCount = 3;
        [Tooltip("同時に出ていてよい数 (前の回の的がまだ残っている時の上限)")]
        public int stageMaxOutTotal = 6;
        [Tooltip("モードが終わってから、次を出し始めるまでの秒数")]
        public float stageResumeDelay = 3f;
        [Tooltip("予定の時間からこの秒数たっても出せなかった (時間切れが近いなど) ものはとばす")]
        public float stageGiveUp = 6f;
        [Tooltip("1つの屋台に同時に出ていてよい数 (最初の屋台だけの時も、これより多くは出さない)")]
        public int stageMaxPerStall = 1;
        [Tooltip("フィーバーで他の屋台も出ている時の、最初の屋台 (メイン屋台) の選ばれやすさ (他の屋台は 1)")]
        [Range(0f, 1f)] public float stageMainStallWeight = 0.3f;

        static readonly TargetKind[] StageKinds =
            { TargetKind.Naval, TargetKind.Sky, TargetKind.Ninja, TargetKind.Kaiju, TargetKind.Casino, TargetKind.Western, TargetKind.Space, TargetKind.Ghost, TargetKind.Mine, TargetKind.Festival, TargetKind.Submarine, TargetKind.Music, TargetKind.Museum, TargetKind.Unknown };

        /// <summary>出す予定の、別の世界へ飛ぶモードの的 1つ分</summary>
        class StageSpawn
        {
            public TargetKind kind;
            public float at;           // 出す時刻 (stageClock)
            public Target target;      // 化けさせるために選んで、起き上がるのを待っている的 (まだなら null)
            public float reservedAt;
        }
        readonly List<StageSpawn> stagePlan = new List<StageSpawn>();
        float stageClock;       // モードやチャレンジの最中は止まる時計 (予定はこの時計で数える)
        float stageResumeAt;    // モードが終わってから、ここまでは時計を止めておく
        float stageNextWindow;  // 次の区切りの始まり (stageClock の時刻)
        readonly List<TargetKind> stageBag = new List<TargetKind>(); // 出す種類の袋 (7種類を一通り出してから、また詰め直す)

        // 次の区切りの予定を作る: 区切りの中の 0〜stageBatchWindow 秒のランダムな時刻を1つ抽選し、その時刻に stageBatchCount 個をまとめて出す。
        // 種類は袋から順に出す (7種類がまんべんなく出る。同じ回の中では同じ種類にしない)
        void PlanNextStage()
        {
            float window = Mathf.Max(0.5f, stageBatchWindow);
            // 出せずに遅れていた分 (時計がもう先に進んでいる区切り) は飛ばして、今の区切りから数える
            if (stageClock >= stageNextWindow + window) stageNextWindow = Mathf.Floor(stageClock / window) * window;
            float at = stageNextWindow + Random.Range(0f, window);
            stageNextWindow += window;

            var used = new List<TargetKind>();
            int count = Mathf.Clamp(stageBatchCount, 1, StageKinds.Length);
            for (int n = 0; n < count; n++)
            {
                if (stageBag.Count == 0)
                {
                    stageBag.AddRange(StageKinds);
                    for (int i = stageBag.Count - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        (stageBag[i], stageBag[j]) = (stageBag[j], stageBag[i]);
                    }
                }
                int k = stageBag.FindIndex(x => !used.Contains(x));
                if (k < 0) { stageBag.Clear(); n--; continue; } // 袋の残りが全部この回で使った種類: 詰め直す
                used.Add(stageBag[k]);
                stagePlan.Add(new StageSpawn { kind = stageBag[k], at = at });
                stageBag.RemoveAt(k);
            }
        }

        // 予定の時計を進める。モード・チャレンジ・豪華タイム・ボーナスステージの最中と、モードが終わってすぐは止める
        void UpdateStagePlan(GameManager gm, bool stageMode, bool playing)
        {
            if (!playing) { stagePlan.Clear(); return; }
            bool paused = stageMode || gm.ChallengeActive || gm.DeluxeActive || (gm.bonusStage != null && gm.bonusStage.IsBusy);
            if (stageMode) stageResumeAt = Time.time + stageResumeDelay;
            if (!paused && Time.time >= stageResumeAt) stageClock += Time.deltaTime;
            if (stagePlan.Count == 0) PlanNextStage(); // 1回分を出しきったら (またはとばしたら)、次の区切りの時刻を抽選
            // 予定から長く遅れたもの (出せる状態にならなかった) はとばす
            for (int i = stagePlan.Count - 1; i >= 0; i--) // (毎フレーム呼ぶので、ラムダを作らないループで)
                if (stagePlan[i].target == null && stageClock - stagePlan[i].at > stageGiveUp) stagePlan.RemoveAt(i);
        }

        // ---- 別の世界へ飛ぶモードの的 (海戦・空中・忍者・怪獣・カジノ・西部劇・宇宙) を、ランダムな普通の的から化けさせる ----
        // 予定の時刻が来たら、今出ている屋台全部の、立っている普通の的の中からランダムに選び (1回分の数だけ。なるべく別の屋台から)、
        // いっせいに倒して起き上がらせる (起き上がる瞬間に OnTargetRising で化ける)。起き上がるのが早い屋台にばかり出ないように、的そのものをランダムに選ぶ
        void UpdateStageReservation(GameManager gm)
        {
            if (gm == null || gm.State != GameState.Playing || gm.DebugMode)
            {
                foreach (var s in stagePlan) s.target = null;
                return;
            }
            foreach (var s in stagePlan)
                if (s.target != null && Time.time - s.reservedAt > 2f) s.target = null; // 起き上がらなかった: 選び直す

            bool paused = gm.ChallengeActive || gm.DeluxeActive || (gm.bonusStage != null && gm.bonusStage.IsBusy) || Time.time < stageResumeAt || gm.FeverAssembling;
            if (paused) return;

            int outCount = 0;
            foreach (var k in StageKinds) outCount += Count(k);
            foreach (var s in stagePlan) if (s.target != null) outCount++;

            List<Target> candidates = null;
            var usedStalls = usedStallBuffer;
            usedStalls.Clear();
            foreach (var s in stagePlan)
            {
                if (s.target != null || s.at > stageClock) continue;
                if (outCount >= stageMaxOutTotal) break;
                if (Count(s.kind) > 0) continue; // 同じ種類がまだ出ている
                if (candidates == null)
                {
                    candidates = targetBuffer;
                    candidates.Clear();
                    foreach (var stall in gm.ShownStalls(stallBuffer))
                        foreach (var t in stall.targets)
                            if (t != null && t.kind == TargetKind.Normal && t.face != null && t.IsUp && !t.manualControl && !active.ContainsKey(t)
                                && t != reservedTarget && !IsStageReserved(t))
                                candidates.Add(t);
                }
                if (candidates.Count == 0) break;
                var pick = PickStageTarget(gm, candidates, usedStalls);
                if (pick == null) break; // どの屋台ももう上限まで出ている: 空くまで待つ
                if (!CanOfferStage(s.kind, pick, LifetimeOf(s.kind) + 1f)) continue; // 時間切れが近いなど (遅れすぎたら UpdateStagePlan がとばす)
                if (!pick.Reappear(0.4f)) { candidates.Remove(pick); continue; }
                s.target = pick;
                s.reservedAt = Time.time;
                candidates.Remove(pick);
                usedStalls.Add(pick.GetComponentInParent<Stall>());
                outCount++;
            }
        }

        // 毎フレーム使うリスト (使い回して、ごみを出さないように)
        readonly List<Stall> stallBuffer = new List<Stall>();
        readonly List<Stall> usedStallBuffer = new List<Stall>();
        readonly List<Target> targetBuffer = new List<Target>();
        readonly List<Target> poolBuffer = new List<Target>();

        // 化けさせる的を選ぶ: まず屋台を選び (最初の屋台は選ばれにくく)、その屋台の的からランダムに1つ。
        // 別の世界へ飛ぶ的がもう stageMaxPerStall 個出ている屋台、この回でもう選んだ屋台は選ばない (最初の屋台にばかり出ないように)
        Target PickStageTarget(GameManager gm, List<Target> candidates, List<Stall> usedStalls)
        {
            var stalls = stallBuffer; // candidates を作った後なので使い回してよい
            stalls.Clear();
            foreach (var t in candidates)
            {
                var st = t.GetComponentInParent<Stall>();
                if (st != null && !stalls.Contains(st) && !usedStalls.Contains(st) && StageCountOn(st) < Mathf.Max(1, stageMaxPerStall)) stalls.Add(st);
            }
            if (stalls.Count == 0) return null;

            float total = 0f;
            foreach (var st in stalls) total += StallWeight(gm, st, stalls.Count);
            float r = Random.value * total;
            Stall chosen = stalls[stalls.Count - 1];
            foreach (var st in stalls)
            {
                r -= StallWeight(gm, st, stalls.Count);
                if (r <= 0f) { chosen = st; break; }
            }

            var pool = poolBuffer;
            pool.Clear();
            foreach (var t in candidates) if (t.GetComponentInParent<Stall>() == chosen) pool.Add(t);
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        float StallWeight(GameManager gm, Stall st, int stallCount) =>
            st == gm.mainStall && stallCount > 1 ? Mathf.Max(0.01f, stageMainStallWeight) : 1f;

        // この屋台に今出ている (化けるのを待っている的も含む) 別の世界へ飛ぶ的の数
        int StageCountOn(Stall stall)
        {
            int n = 0;
            foreach (var kv in active)
                if (System.Array.IndexOf(StageKinds, kv.Value.kind) >= 0 && kv.Key != null && kv.Key.GetComponentInParent<Stall>() == stall) n++;
            foreach (var s in stagePlan)
                if (s.target != null && s.target.GetComponentInParent<Stall>() == stall) n++;
            return n;
        }

        bool IsStageReserved(Target t)
        {
            foreach (var s in stagePlan) if (s.target == t) return true;
            return false;
        }

        // 選んだ的が起き上がった: 予定の種類に化けさせる
        bool TryOfferReservedStage(Target target)
        {
            var s = stagePlan.Find(x => x.target == target);
            if (s == null) return false;
            stagePlan.Remove(s);
            return Count(s.kind) == 0 && CanOfferStage(s.kind, target, LifetimeOf(s.kind)) && ApplyDisguise(target, s.kind, PointsOf(s.kind));
        }

        void OnTargetRising(Target target)
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            if (target.kind != TargetKind.Normal || target.face == null || active.ContainsKey(target)) return;
            if (gm.FeverAssembling) return; // フィーバーの屋台がそろうまでは、特別な的を出さない

            // デバッグモード: 輪の屋台ごとに決まった種類の特殊な的を、まだ出ていない種類から順に化けさせる (どの種類も1つずつ出たまま)
            if (gm.DebugMode)
            {
                var kinds = gm.DebugKinds(target.GetComponentInParent<Stall>());
                if (kinds == null) return;
                foreach (var kind in kinds)
                    if (Count(kind) == 0 && ApplyDisguise(target, kind, PointsOf(kind))) return;
                return;
            }

            // チャレンジの的 (スナイパー / マシンガン): 予約した的 (または予約した屋台で起き上がった的) だけが化ける。
            // 拡張・時間切れ・フィーバー終了に巻き込まれないかは GameManager が判断
            if (reservedStall != null && (target == reservedTarget || (reservedTarget == null && target.GetComponentInParent<Stall>() == reservedStall)))
            {
                var kind = reservedKind;
                ClearChallengeReservation();
                if (Count(TargetKind.Sniper) == 0 && Count(TargetKind.MachineGunChallenge) == 0 && CanOfferChallenge(kind, target, LifetimeOf(kind))
                    && ApplyDisguise(target, kind, kind == TargetKind.Sniper ? sniperTargetPoints : mgChallengeTargetPoints))
                {
                    if (kind == TargetKind.Sniper) sniperShown = true;
                    else mgChallengeShown = true;
                    return;
                }
            }

            // 別の世界へ飛ぶモードの的 (錨・プロペラ・手裏剣・怪獣・カジノ・保安官バッジ・宇宙): 予定の時刻にランダムに選んだ普通の的だけが化ける (UpdateStageReservation)
            if (TryOfferReservedStage(target)) return;

            // DELUXE の的: どの屋台にも出る。撃たれずに消えたら少ししてまた出る
            if (Time.time >= nextDeluxeTime && Count(TargetKind.Deluxe) == 0 && Random.value < deluxeChance
                && gm.CanOfferDeluxe(target, deluxeLifetime))
            {
                if (ApplyDisguise(target, TargetKind.Deluxe, deluxeTargetPoints)) nextDeluxeTime = Time.time + deluxeLifetime + 4f;
                return;
            }

            // 青い玉: フィーバー中だけ、メイン以外の屋台に1つまで (撃たれずに消えたらまた出る)
            if (gm.IsFever && !gm.NavalActive && !gm.SkyActive && !gm.NinjaActive && !gm.KaijuActive && !gm.CasinoActive && !gm.WesternActive && !gm.SpaceActive && !gm.GhostActive && !gm.MineActive && !gm.FestivalActive && !gm.SubmarineActive && !gm.MusicActive && !gm.MuseumActive && !gm.UnknownActive && !gm.BonusUsedThisFever && (gm.bonusStage == null || !gm.bonusStage.IsBusy)
                && Count(TargetKind.BlueOrb) == 0 && Time.time >= nextBlueOrbTime
                && target.GetComponentInParent<Stall>() != gm.mainStall && Random.value < blueOrbChance)
            {
                if (ApplyDisguise(target, TargetKind.BlueOrb, blueOrbPoints)) nextBlueOrbTime = Time.time + 3f;
                return;
            }

            // 爆弾の的
            if (Time.time >= nextExplosiveTime && Count(TargetKind.Explosive) < maxExplosives && Random.value < explosiveChance)
            {
                if (ApplyDisguise(target, TargetKind.Explosive, explosivePoints))
                {
                    var r = gm.IsFever ? explosiveFeverInterval : explosiveInterval;
                    nextExplosiveTime = Time.time + Random.Range(r.x, r.y);
                }
                return;
            }

            // 武器の的 (マシンガンチャレンジ中は持ち替えないので出さない)
            if (gm.MachineGunChallengeActive) return;
            if (Time.time < nextAllowedTime || active.Count - Count(TargetKind.Explosive) >= maxActive) return;
            if (Random.value > chance) return;
            if (ApplyDisguise(target, PickKind(), weaponPoints))
            {
                var range = gm.IsFever ? feverInterval : interval;
                nextAllowedTime = Time.time + Random.Range(range.x, range.y);
            }
        }

        // 的の「顔」を武器の箱 (や爆弾) に付け替える
        bool ApplyDisguise(Target target, TargetKind kind, int points)
        {
            var look = TakeLook(kind);
            if (look == null) return false;

            target.face.SetActive(false);
            look.transform.SetParent(target.visual, false);
            look.transform.localPosition = target.face.transform.localPosition;
            look.transform.localRotation = Quaternion.identity;
            look.SetActive(true);
            active[target] = new Disguise { look = look, kind = kind, originalPoints = target.points, since = Time.time };
            target.kind = kind;
            target.points = points;
            return true;
        }

        float LifetimeOf(TargetKind kind) => kind switch
        {
            TargetKind.BlueOrb => blueOrbLifetime,
            TargetKind.Sniper => sniperLifetime,
            TargetKind.MachineGunChallenge => mgChallengeLifetime,
            TargetKind.Deluxe => deluxeLifetime,
            TargetKind.Naval => navalLifetime,
            TargetKind.Sky => skyLifetime,
            TargetKind.Ninja => ninjaLifetime,
            TargetKind.Kaiju => kaijuOffer.lifetime,
            TargetKind.Casino => casinoOffer.lifetime,
            TargetKind.Western => westernOffer.lifetime,
            TargetKind.Space => spaceOffer.lifetime,
            TargetKind.Ghost => ghostOffer.lifetime,
            TargetKind.Mine => mineOffer.lifetime,
            TargetKind.Submarine => submarineOffer.lifetime,
            TargetKind.Music => musicOffer.lifetime,
            TargetKind.Museum => museumOffer.lifetime,
            TargetKind.Unknown => unknownOffer.lifetime,
            TargetKind.Festival => festivalOffer.lifetime,
            _ => lifetime
        };

        bool CanOfferStage(TargetKind kind, Target target, float lifetime)
        {
            var gm = GameManager.I;
            return kind switch
            {
                TargetKind.Naval => gm.CanOfferNaval(target, lifetime),
                TargetKind.Sky => gm.CanOfferSky(target, lifetime),
                TargetKind.Ninja => gm.CanOfferNinja(target, lifetime),
                TargetKind.Kaiju => gm.CanOfferKaiju(target, lifetime),
                TargetKind.Casino => gm.CanOfferCasino(target, lifetime),
                TargetKind.Western => gm.CanOfferWestern(target, lifetime),
                TargetKind.Space => gm.CanOfferSpace(target, lifetime),
                TargetKind.Ghost => gm.CanOfferGhost(target, lifetime),
                TargetKind.Mine => gm.CanOfferMine(target, lifetime),
                TargetKind.Submarine => gm.CanOfferSubmarine(target, lifetime),
                TargetKind.Music => gm.CanOfferMusic(target, lifetime),
                TargetKind.Museum => gm.CanOfferMuseum(target, lifetime),
                TargetKind.Unknown => gm.CanOfferUnknown(target, lifetime),
                TargetKind.Festival => gm.CanOfferFestival(target, lifetime),
                _ => false
            };
        }

        int PointsOf(TargetKind kind) => kind switch
        {
            TargetKind.Explosive => explosivePoints,
            TargetKind.BlueOrb => blueOrbPoints,
            TargetKind.Sniper => sniperTargetPoints,
            TargetKind.MachineGunChallenge => mgChallengeTargetPoints,
            TargetKind.Deluxe => deluxeTargetPoints,
            TargetKind.Naval => navalTargetPoints,
            TargetKind.Sky => skyTargetPoints,
            TargetKind.Ninja => ninjaTargetPoints,
            TargetKind.Kaiju => kaijuOffer.points,
            TargetKind.Casino => casinoOffer.points,
            TargetKind.Western => westernOffer.points,
            TargetKind.Space => spaceOffer.points,
            TargetKind.Ghost => ghostOffer.points,
            TargetKind.Mine => mineOffer.points,
            TargetKind.Submarine => submarineOffer.points,
            TargetKind.Music => musicOffer.points,
            TargetKind.Museum => museumOffer.points,
            TargetKind.Unknown => unknownOffer.points,
            TargetKind.GateBack => 0, // WORLD GATE の BACK (点数なし)
            TargetKind.Festival => festivalOffer.points,
            _ => weaponPoints
        };

        int Count(TargetKind kind)
        {
            int n = 0;
            foreach (var d in active.Values) if (d.kind == kind) n++;
            return n;
        }

        void Restore(Target target)
        {
            var d = active[target];
            active.Remove(target);
            target.kind = TargetKind.Normal;
            target.points = d.originalPoints;
            if (target.face != null) target.face.SetActive(true);
            d.look.SetActive(false);
            d.look.transform.SetParent(transform, false);
        }

        GameObject TakeLook(TargetKind kind)
        {
            string suffix = "_" + kind;
            foreach (var look in looks)
                if (!look.activeSelf && look.transform.parent == transform && look.name.EndsWith(suffix)) return look;
            return null;
        }

        TargetKind PickKind()
        {
            float r = Random.value * (machineGunWeight + shotgunWeight + bombWeight);
            if (r < machineGunWeight) return TargetKind.MachineGun;
            if (r < machineGunWeight + shotgunWeight) return TargetKind.Shotgun;
            return TargetKind.Bomb;
        }
    }
}
