using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    public enum GameState { Title, Playing, Result }

    /// <summary>
    /// ゲーム進行。
    /// Title : START の的だけが出ている。撃つと開始。
    /// Playing: 制限時間内に的を倒して得点。武器の的で銃が変わり、赤い的でフィーバー(360度モード)。
    /// Result : スコア表示の後、Title に戻る。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Header("References")]
        public PlayerRig player;
        public Stall mainStall;
        [Tooltip("フィーバー用の屋台 (全ギミック分)。フィーバーのたびにこの中からランダムに選んで出す")]
        public Stall[] feverStalls;
        [Tooltip("プレイヤー(中心)からメイン屋台までの距離。最初のステージの輪の半径")]
        public float stallRadius = 3.5f;
        public Target startTarget;
        public TextMesh hudText;
        public TextMesh messageText;
        public Font font;
        [Tooltip("青い玉を撃つと始まるボーナスステージ (足元の床が開いて地下室の的を撃つ)")]
        public BonusStage bonusStage;
        [Tooltip("スナイパーの的を撃つと始まるスナイパーチャレンジ (遠くの大きな的を1発で狙う)")]
        public SniperChallenge sniper;

        [Header("Rules")]
        public float roundDuration = 90f;
        public float feverDuration = 20f;
        public float specialWeaponDuration = 3f;
        public float stallMoveDuration = 1.2f;
        [Tooltip("爆弾の的が爆発した時に、周りの的を倒す半径 (m)")]
        public float explosiveRadius = 1.1f;
        [Tooltip("爆弾の的に当ててから爆発するまでの秒数 (連鎖が順番に起きて見える)")]
        public float explosiveDelay = 0.12f;

        [Header("Stages (時間がたつと屋台の輪が広がる)")]
        [Tooltip("ステージごとの、フィーバーで輪に並ぶ屋台の数 (メイン屋台を含む)。屋台の間隔が変わらないよう、数が増えるほど輪が大きくなる")]
        public int[] stageStallCounts = { 6, 7, 8 };
        [Tooltip("次のステージに進むまでの秒数")]
        public float stageInterval = 30f;

        [Header("Sniper Challenge")]
        public int sniperPoints = 3000;

        /// <summary>スナイパーチャレンジ中か</summary>
        public bool SniperActive => sniperStall != null;
        Stall sniperStall;          // チャレンジのために引っ込めた屋台

        public GameState State { get; private set; }
        /// <summary>今のステージ (0 始まり)</summary>
        public int Stage { get; private set; }
        public bool IsFever { get; private set; }
        /// <summary>このフィーバー中にもう青い玉を撃ったか (ボーナスステージは1回のフィーバーにつき1回)</summary>
        public bool BonusUsedThisFever { get; private set; }
        public int Score { get; private set; }

        float timeLeft;
        float feverLeft;
        float messageTimer;
        int highScore;
        // フィーバーで今出ている屋台 (輪に並ぶ順。メイン屋台は含まない)
        readonly List<Stall> activeFeverStalls = new List<Stall>();
        Vector3 mainHomePos;
        Quaternion mainHomeRot;

        const string HighScoreKey = "ShootingGalleryDX_HighScore";

        void Awake()
        {
            I = this;
            highScore = PlayerPrefs.GetInt(HighScoreKey, 0);
        }

        void Start()
        {
            mainHomePos = mainStall.transform.localPosition;
            mainHomeRot = mainStall.transform.localRotation;
            foreach (var s in feverStalls) s.HideImmediate();
            GoTitle();
        }

        void Update()
        {
            if (State == GameState.Playing)
            {
                timeLeft -= Time.deltaTime;
                int stage = Mathf.Min((int)((roundDuration - timeLeft) / Mathf.Max(1f, stageInterval)), stageStallCounts.Length - 1);
                if (stage > Stage && !SniperActive) StageUp(stage); // チャレンジ中は拡張を待つ (念のため)
                if (SniperActive && sniper.TimeLeft <= 0f) EndSniper(false, "TIME OVER...");
                if (IsFever)
                {
                    if (!SniperActive) feverLeft -= Time.deltaTime; // スナイパーチャレンジ中はフィーバーの残り時間を止める
                    if (feverLeft <= 0f) EndFever();
                }
                if (timeLeft <= 0f) EndRound();
            }

            if (messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f && messageText != null) messageText.text = "";
            }

            UpdateHud();
        }

        void UpdateHud()
        {
            if (hudText == null) return;
            switch (State)
            {
                case GameState.Title:
                    hudText.text = $"SHOOTING GALLERY DX\nHIGH SCORE {highScore}";
                    break;
                case GameState.Playing:
                    string fever = IsFever ? $"   FEVER {Mathf.CeilToInt(feverLeft)}" : "";
                    hudText.text = $"SCORE {Score}   STAGE {Stage + 1}\nTIME {Mathf.CeilToInt(Mathf.Max(0f, timeLeft))}{fever}";
                    break;
                case GameState.Result:
                    hudText.text = $"SCORE {Score}\nHIGH SCORE {highScore}";
                    break;
            }
        }

        void ShowMessage(string text, float seconds, Color color)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.color = color;
            messageTimer = seconds;
        }

        // ---- 状態遷移 ----

        void GoTitle()
        {
            State = GameState.Title;
            mainStall.SetTargetsAvailable(false);
            startTarget.SetAvailable(true);
            ShowMessage("SHOOT  START !", 9999f, Color.white);
        }

        void StartRound()
        {
            State = GameState.Playing;
            Score = 0;
            Stage = 0;
            timeLeft = roundDuration;
            startTarget.SetAvailable(false);
            mainStall.SetTargetsAvailable(true);
            player.ResetWeapon();
            Sfx.Play("start");
            ShowMessage("GO !", 1.5f, Color.yellow);
        }

        void EndRound()
        {
            State = GameState.Result;
            if (SniperActive) EndSniper(false, null);
            if (IsFever) EndFever();
            mainStall.SetTargetsAvailable(false);
            player.ResetWeapon();

            bool newRecord = Score > highScore;
            if (newRecord)
            {
                highScore = Score;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
                PlayerPrefs.Save();
            }
            Sfx.Play("end");
            ShowMessage(newRecord ? $"TIME UP!\nNEW RECORD {Score}" : $"TIME UP!\nSCORE {Score}", 4f, new Color(1f, 0.85f, 0.3f));
            StartCoroutine(BackToTitle());
        }

        IEnumerator BackToTitle()
        {
            yield return new WaitForSeconds(4f);
            GoTitle();
        }

        void StartFever()
        {
            feverLeft = feverDuration;
            if (IsFever) return; // フィーバー中に再度当てたら延長だけ
            IsFever = true;
            BonusUsedThisFever = false;
            activeFeverStalls.Clear();
            ArrangeFeverStalls();
            player.SetDualWield(true);
            Sfx.Play("fever");
            ShowMessage("FEVER TIME!!\nLOOK AROUND!", 3f, new Color(1f, 0.3f, 0.3f));
        }

        /// <summary>
        /// 時間がたってステージが上がった。フィーバー中ならその場で輪を広げて屋台を増やす
        /// (フィーバー外ならメイン屋台はそのままで、次のフィーバーから広い輪になる)。
        /// </summary>
        void StageUp(int stage)
        {
            Stage = stage;
            int count = StallCountForStage();
            Sfx.Play("stageup");
            ShowMessage($"STAGE {Stage + 1}!\nFEVER STALLS x{count}", 2.5f, new Color(0.5f, 1f, 0.6f));
            if (IsFever) ArrangeFeverStalls();
        }

        int StallCountForStage()
        {
            if (stageStallCounts == null || stageStallCounts.Length == 0) return 6;
            return Mathf.Clamp(stageStallCounts[Mathf.Clamp(Stage, 0, stageStallCounts.Length - 1)], 2, feverStalls.Length + 1);
        }

        /// <summary>
        /// n 軒を輪に並べるときの半径。最初のステージの屋台どうしの間隔 (弦の長さ) を保つように広げる。
        /// </summary>
        float RingRadius(int n)
        {
            int baseCount = stageStallCounts != null && stageStallCounts.Length > 0 ? Mathf.Max(2, stageStallCounts[0]) : 6;
            float r = stallRadius * Mathf.Sin(Mathf.PI / baseCount) / Mathf.Sin(Mathf.PI / n);
            return Mathf.Max(stallRadius, r);
        }

        /// <summary>
        /// 今のステージの数だけ屋台を輪に並べる。メイン屋台は正面 (0度) のまま輪の半径まで下がり、
        /// フィーバー屋台は全ギミックからランダムに選んでランダムな順で並べる。
        /// すでに出ている屋台は地上のまま新しい位置へ滑り、足りない分が新しくせり上がる。
        /// </summary>
        void ArrangeFeverStalls()
        {
            int n = StallCountForStage();
            float radius = RingRadius(n);

            // 足りない分を、まだ出ていない屋台からランダムに選んでランダムな位置に差し込む
            var pool = new List<Stall>();
            foreach (var s in feverStalls)
                if (!activeFeverStalls.Contains(s)) pool.Add(s);
            var added = new List<Stall>();
            while (activeFeverStalls.Count < n - 1 && pool.Count > 0)
            {
                int j = Random.Range(0, pool.Count);
                activeFeverStalls.Insert(Random.Range(0, activeFeverStalls.Count + 1), pool[j]);
                added.Add(pool[j]);
                pool.RemoveAt(j);
            }

            float baseYaw = mainHomeRot.eulerAngles.y; // メイン屋台の向き (正面)
            var mainRot = Quaternion.Euler(0f, baseYaw, 0f);
            mainStall.Slide(mainRot * new Vector3(0f, 0f, radius) + Vector3.up * mainHomePos.y, mainHomeRot, stallMoveDuration);
            n = activeFeverStalls.Count + 1;
            for (int i = 0; i < activeFeverStalls.Count; i++)
            {
                // メイン屋台と合わせて 360 度に等間隔
                var stall = activeFeverStalls[i];
                var rot = Quaternion.Euler(0f, baseYaw + 360f / n * (i + 1), 0f);
                var pos = rot * new Vector3(0f, 0f, radius);
                if (!added.Contains(stall))
                    stall.Slide(pos, rot, stallMoveDuration); // このフィーバーで出ている屋台は後ろへ下がって詰め直す
                else
                {   // 新しく出す屋台は地下からせり上がる (前のフィーバーで沈む途中でも下から出し直す)
                    stall.PlaceAt(pos, rot);
                    stall.Rise(stallMoveDuration);
                }
            }
        }

        void EndFever()
        {
            IsFever = false;
            if (bonusStage != null) bonusStage.Close();
            foreach (var s in feverStalls) s.Sink(stallMoveDuration);
            activeFeverStalls.Clear();
            mainStall.Slide(mainHomePos, mainHomeRot, stallMoveDuration); // メイン屋台は最初の位置へ戻る
            player.SetDualWield(false);
            if (State == GameState.Playing) ShowMessage("FEVER END", 1.5f, Color.white);
        }

        // ---- 的が撃たれた ----

        public void OnTargetHit(Target target, Vector3 point)
        {
            if (target.kind == TargetKind.Start)
            {
                if (State != GameState.Playing) StartRound();
                return;
            }
            if (State != GameState.Playing) return;
            if (target.kind == TargetKind.SniperBig) target.points = sniperPoints;

            // ボーナスの大きな的: 当てるたびに 100, 200, 300… と点数が上がる (音階もここで鳴る)
            if (target.kind == TargetKind.BigBonus)
            {
                if (bonusStage == null) return;
                target.points = bonusStage.RegisterHit();
                if (target.points <= 0) return;
            }

            Score += target.points;
            Color popupColor = PointStyle.PopupColor(target.points);

            switch (target.kind)
            {
                case TargetKind.Normal:
                    Sfx.Play(target.points >= 200 ? "bonus" : "hit");
                    break;
                case TargetKind.BlueOrb:
                    popupColor = new Color(0.35f, 0.65f, 1f);
                    if (bonusStage != null && !bonusStage.IsBusy)
                    {
                        BonusUsedThisFever = true;
                        bonusStage.Open();
                        Sfx.Play("bonus");
                        ShowMessage("BONUS CHANCE!\nLOOK DOWN!!", 2.5f, popupColor);
                    }
                    break;
                case TargetKind.BigBonus:
                    popupColor = new Color(0.4f, 0.9f, 1f);
                    if (bonusStage.IsComplete)
                        ShowMessage($"PERFECT!!\nDO RE MI COMPLETE!", 2.5f, new Color(1f, 0.85f, 0.3f));
                    break;
                case TargetKind.Explosive:
                    Sfx.Play("hit");
                    popupColor = new Color(1f, 0.35f, 0.1f);
                    StartCoroutine(ExplodeAfterDelay(point));
                    break;
                case TargetKind.Ufo:
                    Sfx.Play("ufo");
                    Sfx.Play("explosion");
                    popupColor = new Color(0.4f, 1f, 0.9f);
                    ShowMessage($"UFO!!  +{target.points}", 2f, popupColor);
                    break;
                case TargetKind.Bomb:
                    player.GiveWeapon(WeaponType.Bomb, specialWeaponDuration);
                    Sfx.Play("pickup");
                    popupColor = new Color(1f, 0.55f, 0.15f);
                    ShowMessage("BOMB!", 1.5f, popupColor);
                    break;
                case TargetKind.MachineGun:
                    player.GiveWeapon(WeaponType.MachineGun, specialWeaponDuration);
                    Sfx.Play("pickup");
                    popupColor = new Color(0.4f, 0.7f, 1f);
                    ShowMessage("MACHINE GUN!", 1.5f, popupColor);
                    break;
                case TargetKind.Shotgun:
                    player.GiveWeapon(WeaponType.Shotgun, specialWeaponDuration);
                    Sfx.Play("pickup");
                    popupColor = new Color(0.4f, 1f, 0.5f);
                    ShowMessage("SHOTGUN!", 1.5f, popupColor);
                    break;
                case TargetKind.Fever:
                    popupColor = new Color(1f, 0.3f, 0.3f);
                    StartFever();
                    break;
                case TargetKind.Sniper:
                    popupColor = new Color(1f, 0.25f, 0.3f);
                    StartSniper(target);
                    break;
                case TargetKind.SniperBig:
                    popupColor = new Color(1f, 0.85f, 0.3f);
                    Sfx.Play("complete");
                    Sfx.Play("explosion");
                    ShowMessage($"BULLSEYE!!\n+{target.points}", 3f, popupColor);
                    break;
            }

            float popupSize = 0.1f + 0.1f * Mathf.Clamp01(target.points / 500f); // 高得点ほど大きく
            if (Camera.main != null) // 遠く (UFO など) でも読めるように距離に応じて拡大
                popupSize *= Mathf.Max(1f, Vector3.Distance(Camera.main.transform.position, point) / 3f);
            ScorePopup.Spawn(font, point + Vector3.up * 0.1f, $"+{target.points}", popupColor, popupSize);
        }

        // ---- スナイパーチャレンジ ----

        /// <summary>
        /// この的をスナイパーの的に化けさせてよいか (WeaponDropper が聞く)。
        /// 化けてから撃たれるまで (lifetime) + チャレンジの制限時間のあいだに、屋台の拡張・時間切れが来ない時だけ OK。
        /// フィーバー屋台の場合、フィーバーの残り時間はチャレンジ中は止まるので、撃たれるまでの分だけあればよい。
        /// </summary>
        public bool CanOfferSniper(Target target, float lifetime)
        {
            if (target == null || sniper == null || State != GameState.Playing || SniperActive) return false;
            if (bonusStage != null && bonusStage.IsBusy) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;

            // 時間切れまでには、撃たれるまで + チャレンジ全体の時間が必要
            if (timeLeft < lifetime + sniper.timeLimit + sniper.appearTime + 1.5f) return false;
            // 屋台の拡張はチャレンジ中は待たせるので、的が出ている間に拡張が来なければよい
            if (Stage < stageStallCounts.Length - 1)
            {
                float untilNextStage = (Stage + 1) * stageInterval - (roundDuration - timeLeft);
                if (untilNextStage < lifetime + 1.5f) return false;
            }
            if (stall != mainStall && (!IsFever || feverLeft < lifetime + 1.5f)) return false;
            return true;
        }

        /// <summary>今プレイヤーの周りに出ている屋台 (メイン + フィーバー中の屋台)。動いている途中の屋台は除く</summary>
        public List<Stall> ShownStalls()
        {
            var list = new List<Stall>();
            if (mainStall != null && mainStall.IsSettled) list.Add(mainStall);
            if (IsFever)
                foreach (var s in activeFeverStalls)
                    if (s.IsSettled) list.Add(s);
            return list;
        }

        /// <summary>スナイパーの的を撃った: その屋台を引っ込め、屋台があった方の遠くに大きな的を出し、スナイパーライフルを渡す</summary>
        void StartSniper(Target target)
        {
            if (sniper == null || SniperActive) return;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null) return;

            sniperStall = stall;
            stall.Sink(stallMoveDuration);
            Vector3 eye = player.head != null ? player.head.transform.position : player.transform.position + Vector3.up * 1.6f;
            sniper.Show(stall.transform.position - player.transform.position, eye);

            player.SetDualWield(false); // ライフルは右手の1丁だけ
            player.GiveWeapon(WeaponType.Sniper, sniper.timeLimit);
            Sfx.Play("sniperready");
            ShowMessage($"SNIPER CHALLENGE!\nONE SHOT  {sniperPoints} PTS", 3f, new Color(1f, 0.3f, 0.35f));
        }

        /// <summary>スナイパーライフルの1発が撃たれた (GunController から)。hit = 当たった的 (なければ null)</summary>
        public void OnSniperShot(Target hit)
        {
            if (!SniperActive) return;
            bool success = hit != null && hit.kind == TargetKind.SniperBig;
            EndSniper(success, success ? null : "MISS...");
        }

        /// <summary>チャレンジを終える: 大きな的を消し、引っ込めた屋台を戻し、銃を元に戻す</summary>
        void EndSniper(bool success, string failMessage)
        {
            var stall = sniperStall;
            sniperStall = null;
            sniper.Hide(success);

            // 屋台を戻す (チャレンジ中にフィーバーが終わったフィーバー屋台は戻さない)
            bool show = stall == mainStall || (IsFever && activeFeverStalls.Contains(stall));
            if (stall != null && show) stall.Rise(stallMoveDuration, State == GameState.Playing);

            if (player.Weapon == WeaponType.Sniper) player.ResetWeapon(); // 外した1発で別の武器の的を取った時はそのまま
            player.SetDualWield(IsFever);
            if (!success && failMessage != null)
            {
                Sfx.Play("snipermiss");
                ShowMessage(failMessage, 2f, new Color(0.7f, 0.7f, 0.8f));
            }
        }

        // 爆弾の的: 少し間をおいて爆発し、周りの的をまとめて倒す (範囲内の爆弾の的も誘爆する)
        IEnumerator ExplodeAfterDelay(Vector3 point)
        {
            yield return new WaitForSeconds(explosiveDelay);
            var explosionMat = player != null && player.rightGun != null ? player.rightGun.explosionMaterial : null;
            Bomb.Blast(point, explosiveRadius, explosionMat);
            ScorePopup.Spawn(font, point + Vector3.up * 0.3f, "BOOM!", new Color(1f, 0.45f, 0.1f), 0.2f);
        }
    }
}
