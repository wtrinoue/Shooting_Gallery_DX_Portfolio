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
    public partial class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Header("References")]
        public PlayerRig player;
        public Stall mainStall;
        [Tooltip("フィーバー用の屋台 (全ギミック分)。フィーバーのたびにこの中からランダムに選んで出す")]
        public Stall[] feverStalls;
        [Tooltip("デバッグモードの輪に並べる屋台 (メイン屋台と同じギミックなしの屋台 5軒)。デバッグモードの間はフィーバー屋台の代わりにこれを使う")]
        public Stall[] debugStalls;
        [Tooltip("WORLD GATE の輪の屋台 (1段だけの屋台 6軒。1軒目が正面)。WORLD GATE の間はメイン屋台・フィーバー屋台の代わりにこれを使う")]
        public Stall[] gateStalls;
        [Tooltip("プレイヤー(中心)からメイン屋台までの距離。最初のステージの輪の半径")]
        public float stallRadius = 3.5f;
        public Target startTarget;
        [Tooltip("タイトルの WORLD GATE の的 (START の横)。撃つと、別の世界へ飛ぶ的が並んだ6軒の屋台に囲まれる")]
        public Target worldGateTarget;
        [Tooltip("操作方法の看板 (プレイヤーの背中側)。タイトルの間だけ表示する (ゲーム中はフィーバーの屋台が並ぶ場所なので隠す)")]
        public GameObject controlsBoard;
        [Tooltip("タイトルの間だけ穴をふさぐ平らな床のふたと、穴のふち (ラウンドが始まるとふたが消え、ふちとハッチが出る)")]
        public GameObject pitCover;
        public Transform pitRim;
        public TextMesh hudText;
        public TextMesh messageText;
        public Font font;
        [Tooltip("青い玉を撃つと始まるボーナスステージ (足元の床が開いて地下室の的を撃つ)")]
        public BonusStage bonusStage;
        [Tooltip("スナイパーの的を撃つと始まるスナイパーチャレンジ (遠くの大きな的を1発で狙う)")]
        public SniperChallenge sniper;
        [Tooltip("マシンガンチャレンジの的を撃つと始まるマシンガンチャレンジ (屋台3軒分に並んだ的をマシンガンで撃つ)")]
        public MachineGunChallenge machineGunChallenge;

        [Header("Deluxe (豪華屋台チェンジ: DELUXE の的を撃つと、出ている屋台が全部黒と金の豪華な屋台に入れ替わる)")]
        [Tooltip("豪華版のメイン屋台 (フィーバーの的もある)")]
        public Stall deluxeMainStall;
        [Tooltip("豪華版のフィーバー屋台。豪華タイム中のフィーバーはこの中から選ぶ (輪に並ぶ最大数 - 1 軒以上必要)")]
        public Stall[] deluxeFeverStalls;
        public float deluxeDuration = 20f;
        [Tooltip("豪華タイム中に空に上がる花火の色 (半透明のマテリアル)")]
        public Material[] fireworkMaterials;
        [Tooltip("豪華タイム中の霧の色 (夜空が紫がかる)")]
        public Color deluxeFogColor = new Color(0.14f, 0.04f, 0.2f);
        [Tooltip("豪華タイム中の環境光")]
        public Color deluxeAmbient = new Color(0.55f, 0.45f, 0.42f);

        [Header("Naval (海戦モード: 錨の的を撃つと、屋台が沈んで足元が船の甲板に、まわりが海になる。敵の船を撃沈する)")]
        public NavalBattle navalBattle;
        [Tooltip("海戦モード中の空の色 (昼の青空。カメラの背景色)。普段は夜のまま")]
        public Color navalSkyColor = new Color(0.42f, 0.68f, 0.95f);
        [Tooltip("海戦モード中の霧の色 (水平線がかすむ明るい青)")]
        public Color navalFogColor = new Color(0.62f, 0.8f, 0.96f);
        [Tooltip("海戦モード中の環境光 (昼の明るさ)")]
        public Color navalAmbient = new Color(0.78f, 0.82f, 0.9f);

        [Header("Sky (空中ステージ: プロペラの的を撃つと、屋台が沈んで地面が落ちていき、足元がプロペラ機の胴体の上になる。気球や敵機をマシンガンで撃墜する)")]
        public SkyBattle skyBattle;
        [Tooltip("空中ステージ中の空の色 (高い所の明るい青空。カメラの背景色)")]
        public Color skySkyColor = new Color(0.45f, 0.72f, 1f);
        [Tooltip("空中ステージ中の霧の色 (遠くがかすむ白っぽい青)")]
        public Color skyFogColor = new Color(0.75f, 0.87f, 1f);
        [Tooltip("空中ステージ中の環境光 (昼の明るさ)")]
        public Color skyAmbient = new Color(0.82f, 0.85f, 0.92f);

        [Header("Ninja (忍者モード: 手裏剣の的を撃つと、壁と天井がプレイヤーを囲み、ふすまから出てくる忍者を手裏剣で倒す)")]
        public NinjaDojo ninjaDojo;
        [Tooltip("忍者モード中の環境光 (行灯の明かりの、暖かくて少し暗い部屋)")]
        public Color ninjaAmbient = new Color(0.45f, 0.38f, 0.32f);

        [Header("Kaiju (怪獣モード: 怪獣の的を撃つと、屋台が足元へ縮んで、まわりに街が広がる。手の怪獣の頭から炎を吐いてビルを壊す)")]
        public KaijuCity kaijuCity;
        public Color kaijuSkyColor = new Color(0.4f, 0.65f, 0.98f);
        public Color kaijuFogColor = new Color(0.7f, 0.82f, 0.97f);
        public Color kaijuAmbient = new Color(0.85f, 0.87f, 0.92f);

        [Header("Casino (カジノモード: カジノの的を撃つと、円柱形の大広間に囲まれる。スロットとルーレットにダーツを投げる)")]
        public CasinoHall casinoHall;
        public Color casinoAmbient = new Color(0.55f, 0.42f, 0.55f);

        [Header("Western (西部劇モード: 保安官バッジの的を撃つと、荒野を走る列車の上に立つ。並んで走るカウボーイをリボルバーで撃つ)")]
        public WesternTrain westernTrain;
        public Color westernSkyColor = new Color(0.55f, 0.72f, 0.92f);
        public Color westernFogColor = new Color(0.92f, 0.82f, 0.66f);
        public Color westernAmbient = new Color(0.88f, 0.8f, 0.7f);

        [Header("Space (宇宙モード: 宇宙の的を撃つと、屋台も床も消えて星空の宇宙に浮かぶ。流れる星のパネル・惑星・隕石・ロケットをミサイルで撃つ)")]
        public SpaceStage spaceStage;
        public Color spaceSkyColor = new Color(0.005f, 0.006f, 0.02f);
        public Color spaceAmbient = new Color(0.35f, 0.36f, 0.45f);

        [Header("Ghost (幽霊モード: おばけの的を撃つと、屋台もハッチも消えて月夜の墓場に。まわりのお化け・魔女・ゾンビ・骸骨を撃つ)")]
        public GhostManor ghostManor;
        public Color ghostSkyColor = new Color(0.2f, 0.07f, 0.3f);
        public Color ghostFogColor = new Color(0.24f, 0.11f, 0.32f);
        public Color ghostAmbient = new Color(0.45f, 0.38f, 0.58f);

        [Header("Mine (発掘モード: つるはしの的を撃つと、足元に穴が掘られてゆっくり降りていき、宝石とトロッコの洞窟へ)")]
        public MineCave mineCave;
        public Color mineSkyColor = new Color(0.02f, 0.015f, 0.01f);
        public Color mineFogColor = new Color(0.04f, 0.03f, 0.025f);
        public Color mineAmbient = new Color(0.24f, 0.2f, 0.18f); // 暗め (ランプと結晶の明かりで照らされた所が浮かび上がるように)

        [Header("Submarine (潜水艦モード: 潜水艦の的を撃つと、床が砂になって屋台が斜めに埋もれ、あたりが海に。丸い潜水艦に包まれて海の底へ沈み、魚雷で撃つ)")]
        public SubmarineDive submarineDive;
        [Tooltip("水の中の背景の色 / 霧の色 (遠くが青くかすむ) / 環境光")]
        public Color subSkyColor = new Color(0.03f, 0.2f, 0.32f);
        public Color subFogColor = new Color(0.04f, 0.27f, 0.4f);
        public Color subAmbient = new Color(0.42f, 0.6f, 0.7f);

        [Header("Music (音楽モード: 四分音符の的を撃つと、赤いカーテンに囲まれてコンサートホールへ。ソロの楽器と、五線譜を流れる音符を撃つ)")]
        public ConcertHall concertHall;
        [Tooltip("ホールの背景の色 / 霧の色 / 環境光 (演奏前の薄暗い時 / 演奏中)")]
        public Color concertSkyColor = new Color(0.01f, 0.008f, 0.01f);
        public Color concertFogColor = new Color(0.03f, 0.02f, 0.02f);
        public Color concertAmbientDim = new Color(0.1f, 0.08f, 0.09f);
        public Color concertAmbientBright = new Color(0.62f, 0.56f, 0.5f); // 名前を変えて、シーンに保存された前の値 (concertAmbient 0.3, 0.25, 0.23) を使わない

        [Header("Festival (夏祭りモード: 提灯の的を撃つと、屋台が射的・輪投げ・金魚すくい・食べ物の4軒に入れ替わる。見ている屋台で武器が変わり、スコアがお金になる)")]
        public FestivalStage festivalStage;
        [Tooltip("夏祭りモードの間だけ HUD に出す、お金 (= スコア) の大きな表示 (スコアなどの文字とは別)")]
        public GameObject moneyPanel;
        public TextMesh moneyText;
        [Tooltip("お金の表示の下の、夏祭りモードの残り時間 (秒数と、減っていくバー。左はしが原点)")]
        public TextMesh festivalTimeText;
        public Transform festivalTimeBar;

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

        /// <summary>スナイパーチャレンジ中か</summary>
        public bool SniperActive => sniperStall != null;
        Stall sniperStall;          // チャレンジのために引っ込めた屋台
        /// <summary>マシンガンチャレンジ中か</summary>
        public bool MachineGunChallengeActive { get; private set; }
        readonly List<Stall> machineGunStalls = new List<Stall>(); // マシンガンチャレンジのために引っ込めた屋台 (撃った屋台と両隣)
        bool machineGunGiven;       // マシンガンチャレンジのマシンガンをもう渡したか (屋台がせり上がってから渡す)
        /// <summary>スナイパー / マシンガンのどちらかのチャレンジ中か (この間はフィーバーの残り時間と屋台の拡張を止める)</summary>
        /// <summary>チャレンジ / 別の世界のモード中か、そこから屋台が戻ってくる途中か (この間は屋台の拡張・入れ替えをしない。戻る途中に別の屋台がひょっこり出ないように)</summary>
        bool StallsBusy => ChallengeActive || Time.time < stallsReturnUntil;
        /// <summary>
        /// いつものプレイ中か (フィーバー中も含む)。チャレンジ・別の世界のモード・そこから戻る途中・DX モード・ボーナスステージの間は false。
        /// UFO はこの間だけ飛ぶ (うなり音がモードの演出のじゃまにならないように)
        /// </summary>
        public bool IsNormalPlay => State == GameState.Playing && !StallsBusy && !DeluxeActive && (bonusStage == null || !bonusStage.IsBusy);
        float stallsReturnUntil; // 屋台が元の場所に戻りきる時刻
        /// <summary>屋台が戻りきるまで (今から seconds 秒) 屋台の拡張・入れ替えを待たせる</summary>
        void HoldStallsFor(float seconds) => stallsReturnUntil = Mathf.Max(stallsReturnUntil, Time.time + seconds);
        public bool ChallengeActive => SniperActive || MachineGunChallengeActive || NavalActive || SkyActive || NinjaActive || KaijuActive || CasinoActive || WesternActive || SpaceActive || GhostActive || MineActive || FestivalActive || SubmarineActive || MusicActive;
        /// <summary>海戦モード中か (チャレンジの1つとして扱う: この間はフィーバーの残り時間と屋台の拡張を止め、他のチャレンジや豪華屋台チェンジは出さない)</summary>
        public bool NavalActive => navalBattle != null && navalBattle.IsActive;
        readonly List<Stall> navalStalls = new List<Stall>(); // 海戦モードのために沈めた屋台
        /// <summary>空中ステージ中か (海戦モードと同じくチャレンジの1つとして扱う)</summary>
        public bool SkyActive => skyBattle != null && skyBattle.IsActive;
        readonly List<Stall> skyStalls = new List<Stall>(); // 空中ステージのために沈めた屋台
        /// <summary>忍者モード中か (海戦モードと同じくチャレンジの1つとして扱う)</summary>
        public bool NinjaActive => ninjaDojo != null && ninjaDojo.IsActive;
        readonly List<Stall> ninjaStalls = new List<Stall>(); // 忍者モードのために沈めた屋台
        /// <summary>怪獣 / カジノ / 西部劇モード中か (海戦モードと同じくチャレンジの1つとして扱う)</summary>
        public bool KaijuActive => kaijuCity != null && kaijuCity.IsActive;
        public bool CasinoActive => casinoHall != null && casinoHall.IsActive;
        public bool WesternActive => westernTrain != null && westernTrain.IsActive;
        public bool SpaceActive => spaceStage != null && spaceStage.IsActive;
        public bool GhostActive => ghostManor != null && ghostManor.IsActive;
        public bool MineActive => mineCave != null && mineCave.IsActive;
        public bool SubmarineActive => submarineDive != null && submarineDive.IsActive;
        public bool MusicActive => concertHall != null && concertHall.IsActive;
        public bool FestivalActive => festivalStage != null && festivalStage.IsActive;
        readonly List<Stall> stageStalls = new List<Stall>(); // 怪獣 / カジノ / 西部劇モードのために縮めた・沈めた屋台 (同時には1つしか動かない)

        /// <summary>エンドレスモード中か (B ボタン長押しで始まる。時間切れがなく、A ボタン長押しで終わる)</summary>
        public bool Endless { get; private set; }
        const float EndlessTime = 99999f; // エンドレスモードの残り時間 (減らさない。時間切れの判断に引っかからない大きな値)
        float elapsed; // ラウンドが始まってからの秒数 (ステージの進み具合)
        /// <summary>
        /// デバッグモード中か (X ボタン長押しで始まる)。時間切れのないエンドレスで、フィーバーと同じ6軒の輪が出たまま。
        /// 輪の屋台ごとに決まった種類の特殊な的が並ぶ (WeaponDropper が DebugKinds を見て化けさせる)
        /// </summary>
        public bool DebugMode { get; private set; }

        // デバッグモードで輪の i 軒目 (0 = 正面のメイン屋台。60 度ずつ右回り) に並べる特殊な的の種類。空の屋台は普通の的だけ
        static readonly TargetKind[][] DebugKindsByStall =
        {
            new[] { TargetKind.MachineGun, TargetKind.Shotgun, TargetKind.Bomb },                // 正面: 武器
            new[] { TargetKind.Explosive, TargetKind.BlueOrb, TargetKind.Deluxe },                // 60 度: 爆弾の的・青い玉・豪華タイム
            new[] { TargetKind.Sniper, TargetKind.MachineGunChallenge },                          // 120 度: チャレンジ
            new[]                                                                                 // 180 度 (背中側): 別の世界へ行くモード全部
            {
                TargetKind.Naval, TargetKind.Sky, TargetKind.Ninja, TargetKind.Kaiju, TargetKind.Casino, TargetKind.Western,
                TargetKind.Space, TargetKind.Ghost, TargetKind.Mine, TargetKind.Festival, TargetKind.Submarine, TargetKind.Music,
            },
            new TargetKind[0],                                                                    // 240 度: 普通の的だけ
            new TargetKind[0],                                                                    // 300 度: 普通の的だけ
        };

        /// <summary>デバッグモードで、この屋台に並べる特殊な的の種類 (デバッグモードでない / 輪にない屋台なら null)</summary>
        public TargetKind[] DebugKinds(Stall stall)
        {
            if (!DebugMode || stall == null) return null;
            if (GateMode) return GateKinds(stall); // WORLD GATE: 別の世界へ飛ぶ的だけを、輪の屋台に順に詰めて並べる
            int i = stall == mainStall ? 0 : activeFeverStalls.IndexOf(stall) + 1;
            return i > 0 || stall == mainStall ? DebugKindsByStall[i % DebugKindsByStall.Length] : null;
        }
        /// <summary>豪華タイム中か (この間 mainStall / feverStalls は豪華版の屋台を指す)</summary>
        public bool DeluxeActive { get; private set; }
        float deluxeLeft;
        float nextFireworkTime;
        Stall normalMainStall;      // 普通のメイン屋台 (豪華タイムが終わったら戻す)
        Stall[] normalFeverStalls;  // 普通のフィーバー屋台
        Stall[] gimmickFeverStalls; // ギミックのあるフィーバー屋台 (デバッグモードの後に戻す)
        Color normalFogColor, normalAmbient;
        float normalFogStart, normalFogEnd;
        Light sunLight;       // シーンの平行光源 (カジノの暗転で消す)
        float sunIntensity;   // 普段の明るさ
        Coroutine sunRoutine;
        Color normalSkyColor; // 普段の空 (夜。カメラの背景色)
        Coroutine ambienceRoutine;

        public GameState State { get; private set; }
        /// <summary>今のステージ (0 始まり)</summary>
        public int Stage { get; private set; }
        public bool IsFever { get; private set; }
        /// <summary>このフィーバー中にもう青い玉を撃ったか (ボーナスステージは1回のフィーバーにつき1回)</summary>
        public bool BonusUsedThisFever { get; private set; }
        public int Score { get; private set; }

        float timeLeft;
        /// <summary>ラウンドの残り時間 (秒)</summary>
        public float TimeLeft => timeLeft;
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
            normalMainStall = mainStall;
            if (player.head != null) normalSkyColor = player.head.backgroundColor;
            // 普段の夜の霧と環境光 (SceneEnvironment が Awake で設定したもの)。どのモードが終わってもここへ戻す
            // (モードが始まるたびに今の色を覚えると、前のモードの色が戻りきる前に始まった時にその色のままになるため)
            normalFogColor = RenderSettings.fogColor;
            normalAmbient = RenderSettings.ambientLight;
            normalFogStart = RenderSettings.fogStartDistance;
            normalFogEnd = RenderSettings.fogEndDistance;
            var lightsRoot = transform.parent != null ? transform.parent : transform; // ゲーム一式 (ShootingGallery) の中の平行光源
            foreach (var l in lightsRoot.GetComponentsInChildren<Light>())
                if (l.type == LightType.Directional && l.enabled) { sunLight = l; sunIntensity = l.intensity; break; }
            normalFeverStalls = feverStalls;
            gimmickFeverStalls = feverStalls;
            if (debugStalls != null)
                foreach (var s in debugStalls) s.HideImmediate();
            if (gateStalls != null)
                foreach (var s in gateStalls) if (s != null) s.HideImmediate();
            if (deluxeMainStall != null) deluxeMainStall.HideImmediate();
            if (deluxeFeverStalls != null)
                foreach (var s in deluxeFeverStalls) s.HideImmediate();
            if (navalBattle != null) navalBattle.ShipSunk += OnShipSunk;
            if (skyBattle != null) skyBattle.EnemyDown += OnEnemyDown;
            if (ninjaDojo != null) ninjaDojo.Opened += OnDojoOpened;
            if (casinoHall != null)
            {
                casinoHall.LightUp += OnCasinoOpened;
                casinoHall.Darken += OnCasinoDarken;
                casinoHall.Restore += OnCasinoRestore;
                if (casinoHall.slot != null) casinoHall.slot.Result += OnSlotResult;
            }
            if (westernTrain != null)
            {
                westernTrain.RiderDown += OnRiderDown;
                westernTrain.Arrived += OnWesternArrived;
                westernTrain.Left += OnWesternLeft;
            }
            if (spaceStage != null)
            {
                spaceStage.ObjectDestroyed += OnSpaceObjectDestroyed;
                spaceStage.Darken += OnSpaceDarken;
                spaceStage.Restore += OnSpaceRestore;
            }
            if (ghostManor != null)
            {
                ghostManor.SpookDown += OnSpookDown;
                ghostManor.Arrived += OnGhostArrived;
                ghostManor.Left += OnGhostLeft;
                ghostManor.Closed += OnGhostClosed;
            }
            if (mineCave != null)
            {
                mineCave.WentDeep += OnMineDeep;
                mineCave.EnteredCave += OnMineCave;
                mineCave.Rising += OnMineRising;
                mineCave.NearSurface += OnMineNearSurface;
                mineCave.Closed += OnMineClosed;
                mineCave.Covered += HideHatch; // 土の床が広がりきってから、その下でハッチを消す
                mineCave.GemBroken += OnGemBroken;
            }
            if (submarineDive != null)
            {
                submarineDive.Covered += OnSubCovered;
                submarineDive.Underwater += OnSubUnderwater;
                submarineDive.Surfaced += OnSubSurfaced;
                submarineDive.Drained += OnSubDrained;
                submarineDive.Closed += OnSubClosed;
            }
            if (concertHall != null)
            {
                concertHall.Covered += OnConcertCovered;
                concertHall.Opened += OnConcertOpened;
                concertHall.MusicStarted += OnConcertMusic;
                concertHall.CoveredBack += OnConcertCoveredBack;
                concertHall.Closed += OnConcertClosed;
            }
            player.DebugRequested += StartDebug; // X ボタン長押しでデバッグモード
            player.ResetRequested += ResetToTitle; // A ボタン長押しで最初に戻る
            player.EndlessRequested += StartEndless; // B ボタン長押しでエンドレスモード
            // 生成した時に小さくして隠してあった床下のハッチは、ラウンドが始まるまで隠したまま (タイトルの間は平らな床)
            if (bonusStage != null && bonusStage.transform.localScale.x < 0.5f)
            {
                hatchScale = Vector3.one;
                hatchHidden = true;
                hatchWasActive = bonusStage.gameObject.activeSelf;
            }
            GoTitle();
        }

        void Update()
        {
            if (State == GameState.Playing)
            {
                elapsed += Time.deltaTime;
                if (!Endless) timeLeft -= Time.deltaTime;
                int stage = Mathf.Min((int)(elapsed / Mathf.Max(1f, stageInterval)), stageStallCounts.Length - 1);
                if (stage > Stage && !StallsBusy && !DebugMode) StageUp(stage); // デバッグモードは6軒のまま。チャレンジ中と、別の世界から屋台が戻ってくる途中は拡張を待つ
                if (SniperActive && sniper.TimeLeft <= 0f) EndSniper(false, "TIME OVER...");
                if (MachineGunChallengeActive && !machineGunGiven && machineGunChallenge.IsReady) GiveChallengeMachineGun();
                if (MachineGunChallengeActive && machineGunChallenge.TimeLeft <= 0f) EndMachineGunChallenge();
                if (IsFever)
                {
                    if (!StallsBusy) feverLeft -= Time.deltaTime; // チャレンジ中と、屋台が戻ってくる途中はフィーバーの残り時間を止める
                    if (feverLeft <= 0f) EndFever();
                }
                if (DeluxeActive)
                {
                    if (!StallsBusy) deluxeLeft -= Time.deltaTime; // チャレンジ中と、屋台が戻ってくる途中は止める (屋台を入れ替えない)
                    if (deluxeLeft <= 0f && !StallsBusy) EndDeluxe();
                    else if (Time.time >= nextFireworkTime) LaunchFirework();
                }
                if (NavalActive && navalBattle.TimeLeft <= 0f) EndNaval();
                if (SkyActive && skyBattle.TimeLeft <= 0f) EndSky();
                if (NinjaActive && ninjaDojo.TimeLeft <= 0f) EndNinja();
                if (KaijuActive && kaijuCity.TimeLeft <= 0f) EndKaiju();
                if (CasinoActive && casinoHall.TimeLeft <= 0f) EndCasino();
                if (WesternActive && westernTrain.TimeLeft <= 0f) EndWestern();
                if (SpaceActive && spaceStage.TimeLeft <= 0f) EndSpace();
                if (GhostActive && ghostManor.TimeLeft <= 0f) EndGhost();
                if (MineActive && mineCave.TimeLeft <= 0f) EndMine();
                if (SubmarineActive && submarineDive.TimeLeft <= 0f) EndSubmarine();
                if (MusicActive && concertHall.TimeLeft <= 0f) EndMusic();
                UpdateFestival(); // 見ている屋台の武器に持ち替える / 時間切れ
                if (timeLeft <= 0f) EndRound();
            }

            if (messageTimer > 0f)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0f && messageText != null) messageText.text = "";
            }

            UpdateHud();
            UpdateMoneyPanel();
        }

        // ---- 夏祭りモードのお金の表示 ----
        int shownMoney = -1;
        float moneyPulse;   // お金が増えた / 減った時に、文字がふくらんで色が変わる (1 → 0)
        bool moneyGained;
        static readonly Color MoneyColor = new Color(1f, 0.85f, 0.3f);

        int shownFestivalSeconds = -1;
        int timeBarState = -1; // バーの色 (0 = 緑, 1 = 赤)。変わった時だけ渡す
        Renderer timeBarRenderer;
        MaterialPropertyBlock timeBarBlock;

        // 夏祭りの残り時間: バーが右から減り、秒数が出る。残り 10 秒を切ると赤くなる
        void UpdateFestivalTime()
        {
            float left = festivalStage.TimeLeft, ratio = Mathf.Clamp01(left / Mathf.Max(1f, festivalStage.duration));
            bool hurry = left <= 10f;
            if (festivalTimeBar != null)
            {
                festivalTimeBar.localScale = new Vector3(Mathf.Max(0.001f, ratio), 1f, 1f);
                if (timeBarRenderer == null) timeBarRenderer = festivalTimeBar.GetComponentInChildren<Renderer>();
                int state = hurry ? 1 : 0;
                if (timeBarRenderer != null && state != timeBarState)
                {
                    timeBarState = state;
                    if (timeBarBlock == null) timeBarBlock = new MaterialPropertyBlock();
                    timeBarBlock.SetColor("_BaseColor", hurry ? new Color(1f, 0.3f, 0.25f) : new Color(0.45f, 0.95f, 0.55f));
                    timeBarRenderer.SetPropertyBlock(timeBarBlock);
                }
            }
            int secs = Mathf.CeilToInt(left);
            if (festivalTimeText == null || secs == shownFestivalSeconds) return;
            shownFestivalSeconds = secs;
            festivalTimeText.text = $"{secs}s";
            festivalTimeText.color = hurry ? new Color(1f, 0.4f, 0.35f) : Color.white;
        }

        void UpdateMoneyPanel()
        {
            if (moneyPanel == null || moneyText == null) return;
            bool show = FestivalActive && State == GameState.Playing;
            if (moneyPanel.activeSelf != show) moneyPanel.SetActive(show);
            if (!show)
            {
                shownMoney = shownFestivalSeconds = -1;
                return;
            }
            if (Score != shownMoney)
            {
                if (shownMoney >= 0)
                {
                    moneyPulse = 1f;
                    moneyGained = Score > shownMoney;
                }
                shownMoney = Score;
                moneyText.text = "¥" + Score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            }
            UpdateFestivalTime();
            if (moneyPulse <= 0f) return;
            moneyPulse = Mathf.Max(0f, moneyPulse - Time.deltaTime * 2.5f);
            moneyText.transform.localScale = Vector3.one * (1f + 0.35f * moneyPulse);
            moneyText.color = Color.Lerp(MoneyColor, moneyGained ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f), moneyPulse);
        }

        // HUD に出す数字の組み合わせ (前のフレームと同じなら文字を作り直さない。文字の作り直しと文字列のごみを毎フレーム出さないように)
        int lastHudSignature;
        bool hudWritten;

        static int Mix(int h, int v) => unchecked(h * 31 + v);
        static int Mix(int h, bool on, int v) => Mix(h, on ? v : int.MinValue);

        int HudSignature()
        {
            int h = Mix(17, (int)State);
            h = Mix(h, Score);
            h = Mix(h, highScore);
            if (State != GameState.Playing) return h;
            h = Mix(h, Stage);
            h = Mix(h, GateMode ? 3 : DebugMode ? 2 : Endless ? 1 : 0);
            h = Mix(h, DebugMode || Endless ? (int)elapsed : Mathf.CeilToInt(Mathf.Max(0f, timeLeft)));
            h = Mix(h, IsFever, Mathf.CeilToInt(feverLeft));
            h = Mix(h, SniperActive, SniperShotsLeft);
            h = Mix(h, MachineGunChallengeActive, MachineGunChallengeActive ? machineGunChallenge.Hits : 0);
            h = Mix(h, DeluxeActive, Mathf.CeilToInt(Mathf.Max(0f, deluxeLeft)));
            h = Mix(h, NavalActive, NavalActive ? Mathf.CeilToInt(navalBattle.TimeLeft) * 1000 + navalBattle.Sunk : 0);
            h = Mix(h, SkyActive, SkyActive ? Mathf.CeilToInt(skyBattle.TimeLeft) * 1000 + skyBattle.Downed : 0);
            h = Mix(h, NinjaActive, NinjaActive ? Mathf.CeilToInt(ninjaDojo.TimeLeft) * 1000 + ninjaDojo.Hits : 0);
            h = Mix(h, KaijuActive, KaijuActive ? Mathf.CeilToInt(kaijuCity.TimeLeft) * 1000 + kaijuCity.Destroyed : 0);
            h = Mix(h, CasinoActive, CasinoActive ? Mathf.CeilToInt(casinoHall.TimeLeft) : 0);
            h = Mix(h, WesternActive, WesternActive ? Mathf.CeilToInt(westernTrain.TimeLeft) * 1000 + westernTrain.Downed : 0);
            h = Mix(h, SpaceActive, SpaceActive ? Mathf.CeilToInt(spaceStage.TimeLeft) * 1000 + spaceStage.Hits : 0);
            h = Mix(h, GhostActive, GhostActive ? Mathf.CeilToInt(ghostManor.TimeLeft) * 1000 + ghostManor.Hits : 0);
            h = Mix(h, MineActive, MineActive ? Mathf.CeilToInt(mineCave.TimeLeft) * 1000 + mineCave.Hits : 0);
            h = Mix(h, SubmarineActive, SubmarineActive ? Mathf.CeilToInt(submarineDive.TimeLeft) * 1000 + submarineDive.Hits : 0);
            h = Mix(h, MusicActive, MusicActive ? Mathf.CeilToInt(concertHall.TimeLeft) * 1000 + concertHall.Hits : 0);
            h = Mix(h, FestivalActive, FestivalActive ? Mathf.CeilToInt(festivalStage.TimeLeft) * 1000 + festivalStage.Hits : 0);
            return h;
        }

        void UpdateHud()
        {
            if (hudText == null) return;
            int signature = HudSignature();
            if (hudWritten && signature == lastHudSignature) return; // 出す数字が変わっていない
            lastHudSignature = signature;
            hudWritten = true;
            switch (State)
            {
                case GameState.Title:
                    hudText.text = $"SHOOTING GALLERY DX\nHIGH SCORE {highScore}\nHOLD B : ENDLESS MODE   HOLD X : DEBUG";
                    break;
                case GameState.Playing:
                    string fever = IsFever ? $"   FEVER {Mathf.CeilToInt(feverLeft)}" : "";
                    string sniperShots = SniperActive ? $"   SNIPER {SniperShotsLeft} SHOTS" : "";
                    string mg = MachineGunChallengeActive ? $"   MG {machineGunChallenge.Hits}/{machineGunChallenge.Total}" : "";
                    string deluxe = DeluxeActive ? $"   DELUXE {Mathf.CeilToInt(Mathf.Max(0f, deluxeLeft))}" : "";
                    string naval = NavalActive ? $"   SEA {Mathf.CeilToInt(navalBattle.TimeLeft)}  SUNK {navalBattle.Sunk}" : "";
                    string sky = SkyActive ? $"   SKY {Mathf.CeilToInt(skyBattle.TimeLeft)}  DOWN {skyBattle.Downed}" : "";
                    string ninja = NinjaActive ? $"   NINJA {Mathf.CeilToInt(ninjaDojo.TimeLeft)}  HIT {ninjaDojo.Hits}" : "";
                    string kaiju = KaijuActive ? $"   KAIJU {Mathf.CeilToInt(kaijuCity.TimeLeft)}  BREAK {kaijuCity.Destroyed}" : "";
                    string casino = CasinoActive ? $"   CASINO {Mathf.CeilToInt(casinoHall.TimeLeft)}" : "";
                    string western = WesternActive ? $"   WEST {Mathf.CeilToInt(westernTrain.TimeLeft)}  DOWN {westernTrain.Downed}" : "";
                    string space = SpaceActive ? $"   SPACE {Mathf.CeilToInt(spaceStage.TimeLeft)}  HIT {spaceStage.Hits}" : "";
                    string ghost = GhostActive ? $"   GHOST {Mathf.CeilToInt(ghostManor.TimeLeft)}  BUSTED {ghostManor.Hits}" : "";
                    string mine = MineActive ? $"   MINE {Mathf.CeilToInt(mineCave.TimeLeft)}  DUG {mineCave.Hits}" : "";
                    string sub = SubmarineActive ? $"   SUB {Mathf.CeilToInt(submarineDive.TimeLeft)}  HIT {submarineDive.Hits}" : "";
                    string concert = MusicActive ? $"   CONCERT {Mathf.CeilToInt(concertHall.TimeLeft)}  HIT {concertHall.Hits}" : "";
                    string festival = FestivalActive ? $"   FESTIVAL {Mathf.CeilToInt(festivalStage.TimeLeft)}  GOT {festivalStage.Hits}" : "";
                    ninja += kaiju + casino + western + space + ghost + mine + sub + concert + festival;
                    int secs = (int)elapsed;
                    string time = GateMode ? $"WORLD GATE {secs / 60}:{secs % 60:00}" : DebugMode ? $"DEBUG {secs / 60}:{secs % 60:00}" : Endless ? $"ENDLESS {secs / 60}:{secs % 60:00}" : $"TIME {Mathf.CeilToInt(Mathf.Max(0f, timeLeft))}";
                    string score = FestivalActive ? "" : $"SCORE {Score}   "; // 夏祭りの世界ではスコアがお金 (お金はその上の大きな表示に出す)
                    hudText.text = $"{score}STAGE {Stage + 1}\n{time}{fever}{deluxe}{sniperShots}{mg}{naval}{sky}{ninja}";
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
            Endless = false;
            DebugMode = false;
            GateMode = false;
            mainStall.SetTargetsAvailable(false);
            startTarget.SetAvailable(true);
            if (worldGateTarget != null) worldGateTarget.SetAvailable(true);
            if (controlsBoard != null) controlsBoard.SetActive(true);
            SetFlatFloor(true); // タイトルの間は、穴もふちもない平らな床
            ShowMessage("SHOOT  START !", 9999f, Color.white);
        }

        void StartRound(bool endless = false, bool debug = false)
        {
            State = GameState.Playing;
            Endless = endless;
            DebugMode = debug; // 的を出す前に決めておく (起き上がる時に種類別に化ける)
            // フィーバーの輪に使う屋台: デバッグモードはメイン屋台と同じギミックなしの屋台、普段はギミックのある屋台 (豪華タイムは StopEverything で終わっている)
            feverStalls = debug && debugStalls != null && debugStalls.Length > 0 ? debugStalls : gimmickFeverStalls;
            normalFeverStalls = feverStalls;
            Score = 0;
            Stage = 0;
            stallsReturnUntil = 0f;
            elapsed = 0f;
            timeLeft = endless ? EndlessTime : roundDuration;
            startTarget.SetAvailable(false);
            if (worldGateTarget != null) worldGateTarget.SetAvailable(false);
            if (controlsBoard != null) controlsBoard.SetActive(false);
            SetFlatFloor(false); // ラウンドが始まった: 平らな床のふたが消え、穴のふちとハッチがふくらんで出る
            mainStall.SetTargetsAvailable(true);
            player.ResetWeapon();
            Sfx.Play(endless ? "stageup" : "start");
            if (endless) ShowMessage("ENDLESS MODE!!\nHOLD A : QUIT", 2.5f, new Color(0.4f, 1f, 0.9f));
            else ShowMessage("GO !", 1.5f, Color.yellow);
        }

        void EndRound()
        {
            State = GameState.Result;
            StopEverything();

            bool newRecord = Score > highScore;
            if (newRecord)
            {
                highScore = Score;
                PlayerPrefs.SetInt(HighScoreKey, highScore);
                PlayerPrefs.Save();
            }
            Sfx.Play("end");
            ShowMessage(newRecord ? $"TIME UP!\nNEW RECORD {Score}" : $"TIME UP!\nSCORE {Score}", 4f, new Color(1f, 0.85f, 0.3f));
            backToTitle = StartCoroutine(BackToTitle());
        }

        // ラウンド中のチャレンジ・モードを全部終わらせ、普通のメイン屋台だけ (的は出さない) の状態に戻す。State は先に Playing 以外にしておくこと
        void StopEverything()
        {
            CancelPendingMode(); // 片付け待ちのモードも取り消す
            if (SniperActive) EndSniper(false, null);
            if (MachineGunChallengeActive) EndMachineGunChallenge();
            if (NavalActive) EndNaval();
            if (SkyActive) EndSky();
            if (NinjaActive) EndNinja();
            if (KaijuActive) EndKaiju();
            if (CasinoActive) EndCasino();
            if (WesternActive) EndWestern();
            if (SpaceActive) EndSpace();
            if (GhostActive) EndGhost();
            if (MineActive) EndMine();
            if (SubmarineActive) EndSubmarine();
            if (MusicActive) EndMusic();
            if (FestivalActive) EndFestival();
            if (IsFever) EndFever();
            if (DeluxeActive) EndDeluxe(); // 普通のメイン屋台に戻してからタイトルへ (身長の的は普通のメイン屋台にある)
            EndWorldGate(); // WORLD GATE の屋台を沈め、普通のメイン屋台を戻す (START・WORLD GATE の的はメイン屋台にある)
            if (bonusStage != null) bonusStage.Close();
            // 点数の的はその場ですぐに消す (倒れる動きもなし。終わった後の静けさ)。せり上がり途中の屋台も、着いた時に的を出さない
            mainStall.SetTargetsAvailable(false);
            if (normalMainStall != null) normalMainStall.SetTargetsAvailable(false);
            if (deluxeMainStall != null) deluxeMainStall.SetTargetsAvailable(false);
            player.ResetWeapon();
            player.SetDualWield(false);
        }

        Coroutine backToTitle;

        IEnumerator BackToTitle()
        {
            yield return new WaitForSeconds(4f);
            backToTitle = null;
            GoTitle();
        }

        /// <summary>
        /// 最初 (タイトルの START の的) に戻す。A ボタンの長押しで呼ばれる。
        /// ラウンド中ならスコアを残さずに全部のモードを終わらせ、身長を選んでいる途中なら START からやり直す。
        /// </summary>
        public void ResetToTitle()
        {
            if (backToTitle != null)
            {
                StopCoroutine(backToTitle);
                backToTitle = null;
            }
            if (State == GameState.Playing)
            {
                State = GameState.Result; // 終わらせている間は Playing 扱いにしない (点数やメッセージを出さない)
                StopEverything();
            }
            GoTitle();
            Sfx.Play("door");
        }

        // ---- 的が撃たれた ----

        public void OnTargetHit(Target target, Vector3 point)
        {
            if (target.kind == TargetKind.Start)
            {
                if (State != GameState.Playing)
                {
                    player.CalibrateHeight(); // 視点を身長 180cm の人の高さに合わせる (屋台との位置関係をいつも同じにする)
                    StartRound();
                }
                return;
            }
            if (target.kind == TargetKind.WorldGate)
            {
                if (State != GameState.Playing) StartWorldGate(); // 別の世界へ飛ぶ的だけが並んだ6軒の屋台に囲まれる
                return;
            }
            // 操作方法の看板のクレジット / ライセンスの的 (タイトルの間でも撃てる。点数なし)
            if (target.kind == TargetKind.Credits)
            {
                var viewer = target.GetComponentInParent<CreditsViewer>(true);
                if (viewer != null) viewer.OnButton(target);
                return;
            }
            if (State != GameState.Playing) return;
            if (target.kind == TargetKind.SniperBig) target.points = sniper.PointsAt(point); // 真ん中に近いほど高い

            // ボーナスの的: 的ごとにランダムな点数 (1000 / 2000 / 3000)。当てるたびに音階が上がる (音もここで鳴る)
            if (target.kind == TargetKind.BigBonus)
            {
                if (bonusStage == null) return;
                target.points = bonusStage.RegisterHit(target);
                if (target.points <= 0) return;
            }

            // 空中ステージの敵: 何発か当てて撃墜した1発で、大きく書いてある点数が入る (それまでは当たった音だけ)
            if (target.kind == TargetKind.Aerial)
            {
                var enemy = target.GetComponent<SkyEnemy>();
                int down = enemy != null ? enemy.TakeHit(point) : 0;
                if (down <= 0)
                {
                    Sfx.Play("hit");
                    return;
                }
                target.points = down;
            }

            // カジノのスロット: 撃つとレバーが下がって回る。点数はリールが止まった時に入る (OnSlotResult)
            if (target.kind == TargetKind.SlotMachine)
            {
                if (casinoHall != null && casinoHall.slot.Pull()) ShowMessage("SPIN!!", 1f, new Color(1f, 0.85f, 0.3f));
                return;
            }
            // カジノのルーレット: ダーツが刺さった扇の点数
            if (target.kind == TargetKind.Roulette)
            {
                var wheel = target.GetComponent<RouletteWheel>();
                target.points = wheel != null ? wheel.PointsAt(point) : 10;
            }

            // 夏祭りの食べ物: お金 (スコア) を払って買う (点数は入らない)
            // 音楽モードの楽器: 撃つと跳ねて音が鳴る。点数が入るのはソロを弾いている楽器だけ
            if (target.kind == TargetKind.MusicInstrument)
            {
                target.points = concertHall != null ? concertHall.OnInstrumentHit(target, point) : 0;
                if (target.points <= 0) return;
            }

            if (target.kind == TargetKind.FestivalFood)
            {
                BuyFood(target, point);
                return;
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
                // 武器の的: マシンガンチャレンジ中は武器を持ち替えない (点数だけ入る)
                case TargetKind.Bomb:
                    popupColor = new Color(1f, 0.55f, 0.15f);
                    if (MachineGunChallengeActive) { Sfx.Play("hit"); break; }
                    player.GiveWeapon(WeaponType.Bomb, specialWeaponDuration);
                    Sfx.Play("pickup");
                    ShowMessage("BOMB!", 1.5f, popupColor);
                    break;
                case TargetKind.MachineGun:
                    popupColor = new Color(0.4f, 0.7f, 1f);
                    if (MachineGunChallengeActive) { Sfx.Play("hit"); break; }
                    player.GiveWeapon(WeaponType.MachineGun, specialWeaponDuration);
                    Sfx.Play("pickup");
                    ShowMessage("MACHINE GUN!", 1.5f, popupColor);
                    break;
                case TargetKind.Shotgun:
                    popupColor = new Color(0.4f, 1f, 0.5f);
                    if (MachineGunChallengeActive) { Sfx.Play("hit"); break; }
                    player.GiveWeapon(WeaponType.Shotgun, specialWeaponDuration);
                    Sfx.Play("pickup");
                    ShowMessage("SHOTGUN!", 1.5f, popupColor);
                    break;
                case TargetKind.Fever:
                    popupColor = new Color(1f, 0.3f, 0.3f);
                    StartFever();
                    break;
                case TargetKind.Sniper:
                    popupColor = new Color(1f, 0.25f, 0.3f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.SniperBig:
                    popupColor = new Color(1f, 0.85f, 0.3f);
                    Sfx.Play("complete");
                    Sfx.Play("explosion");
                    ShowMessage(target.points >= sniper.MaxPoints ? $"BULLSEYE!!\n+{target.points}" : $"NICE SHOT!\n+{target.points}", 3f, popupColor);
                    break;
                case TargetKind.MachineGunChallenge:
                    popupColor = new Color(1f, 0.85f, 0.2f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.Naval:
                    popupColor = new Color(0.4f, 0.75f, 1f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.Sky:
                    popupColor = new Color(0.55f, 0.85f, 1f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.Aerial:
                    popupColor = new Color(1f, 0.7f, 0.25f); // 撃墜の演出は OnEnemyDown
                    break;
                case TargetKind.Ninja:
                    popupColor = new Color(0.75f, 0.5f, 1f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.NinjaPanel:
                    popupColor = target.points >= 500 ? new Color(1f, 0.85f, 0.3f) : new Color(0.85f, 0.75f, 1f);
                    Sfx.Play(target.points >= 300 ? "bonus" : "hit");
                    if (ninjaDojo != null) ninjaDojo.RegisterHit();
                    break;
                case TargetKind.Deluxe:
                    popupColor = new Color(1f, 0.8f, 0.3f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.Kaiju:
                    popupColor = new Color(0.4f, 1f, 0.4f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.KaijuBuilding:
                    popupColor = new Color(1f, 0.75f, 0.3f);
                    Sfx.Play("bonus");
                    if (kaijuCity != null) kaijuCity.RegisterHit();
                    break;
                case TargetKind.Casino:
                    popupColor = new Color(1f, 0.4f, 0.6f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.Roulette:
                    popupColor = target.points >= 500 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.5f, 0.7f);
                    Sfx.Play(target.points >= 200 ? "bonus" : "hit");
                    if (target.points >= 1000) ShowMessage($"BULLSEYE!!\n+{target.points}", 1.5f, popupColor);
                    break;
                case TargetKind.Western:
                    popupColor = new Color(0.95f, 0.75f, 0.35f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.Rider:
                    popupColor = new Color(0.95f, 0.75f, 0.35f);
                    Sfx.Play(target.points >= 500 ? "bonus" : "hit");
                    break;
                case TargetKind.Space:
                    popupColor = new Color(0.6f, 0.7f, 1f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.SpaceObject:
                    popupColor = target.points >= 500 ? new Color(1f, 0.85f, 0.35f) : new Color(0.6f, 0.9f, 1f);
                    Sfx.Play(target.points >= 500 ? "bonus" : "hit");
                    break;
                case TargetKind.Ghost:
                    popupColor = new Color(0.75f, 0.5f, 1f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.Mine:
                    popupColor = new Color(1f, 0.75f, 0.35f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.MineObject:
                    popupColor = target.points >= 500 ? new Color(1f, 0.85f, 0.35f) : new Color(0.7f, 0.95f, 1f);
                    Sfx.Play(target.points >= 300 ? "bonus" : "hit");
                    if (mineCave != null && target.GetComponentInParent<MineCart>() != null) mineCave.RegisterCartHit(); // 宝石は MineGem が数える
                    break;
                case TargetKind.Submarine:
                    popupColor = new Color(1f, 0.85f, 0.25f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.SubmarineObject: // 海の生き物・ほかの潜水艦・壺・宝箱
                    popupColor = target.points >= 1000 ? new Color(1f, 0.85f, 0.3f) : target.points >= 400 ? new Color(1f, 0.55f, 0.45f) : new Color(0.55f, 0.95f, 1f);
                    Sfx.Play(target.points >= 300 ? "bonus" : "hit");
                    if (target.points >= 1000) ShowMessage($"TREASURE!!\n+{target.points}", 1.5f, popupColor);
                    if (submarineDive != null) submarineDive.RegisterHit();
                    break;
                case TargetKind.Music:
                    popupColor = new Color(1f, 0.85f, 0.3f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.MusicInstrument: // ソロの楽器 (音は ConcertHall が鳴らした)
                    popupColor = new Color(1f, 0.85f, 0.3f);
                    Sfx.Play("bonus");
                    var inst = target.GetComponentInParent<MusicInstrument>();
                    ShowMessage($"BRAVO!! {(inst != null ? inst.displayName : "SOLO")}\n+{target.points}", 1.5f, popupColor);
                    break;
                case TargetKind.MusicNote: // 五線譜の音符 (その高さの音が鳴る)
                    popupColor = target.points >= 400 ? new Color(1f, 0.85f, 0.3f) : new Color(0.75f, 0.9f, 1f);
                    if (concertHall != null) concertHall.OnNoteHit(target);
                    break;
                case TargetKind.Festival:
                    popupColor = new Color(1f, 0.55f, 0.35f);
                    RequestMode(target); // 順番待ちに入れる (他のモードの途中なら、終わってから始まる)
                    break;
                case TargetKind.FestivalPrize: // 射的の景品が後ろへ倒れて落ちた
                    popupColor = target.points >= 500 ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.7f, 0.45f);
                    Sfx.Play(target.points >= 300 ? "bonus" : "hit");
                    if (target.points >= 500) ShowMessage($"BIG PRIZE!!\n+{target.points}", 1.5f, popupColor);
                    if (festivalStage != null) festivalStage.RegisterHit();
                    break;
                case TargetKind.FestivalRing: // 輪投げの輪が杭にかかった
                    popupColor = target.points >= 500 ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.55f, 0.45f);
                    Sfx.Play("bonus");
                    if (target.points >= 500) ShowMessage($"RINGER!!\n+{target.points}", 1.5f, popupColor);
                    if (festivalStage != null) festivalStage.RegisterHit();
                    break;
                case TargetKind.FestivalFish: // 金魚をすくった
                    popupColor = target.points >= 1000 ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 0.55f, 0.35f);
                    Sfx.Play(target.points >= 300 ? "bonus" : "hit");
                    if (target.points >= 1000) ShowMessage($"GOLDEN FISH!!\n+{target.points}", 2f, popupColor);
                    else if (target.points >= 500) ShowMessage($"BIG FISH!\n+{target.points}", 1.5f, popupColor);
                    if (festivalStage != null) festivalStage.RegisterHit();
                    break;
                case TargetKind.GhostSpook:
                    popupColor = target.points >= 500 ? new Color(1f, 0.85f, 0.35f) : new Color(0.8f, 0.6f, 1f);
                    Sfx.Play(target.points >= 500 ? "bonus" : "hit");
                    break;
                case TargetKind.MachineGunChallengeTarget:
                    popupColor = new Color(1f, 0.85f, 0.2f);
                    if (machineGunChallenge != null && machineGunChallenge.IsBomb(target))
                    {   // 混ざっていた爆弾: 少し後に爆発して周りのカップを落とす (近くの爆弾も誘爆する)
                        Sfx.Play("hit");
                        popupColor = new Color(1f, 0.35f, 0.1f);
                        StartCoroutine(ExplodeAfterDelay(point, machineGunChallenge.bombRadius));
                    }
                    else Sfx.Play("can");
                    machineGunChallenge.OnCupHit(target); // 上に積んであったカップも崩れ落ちる
                    if (MachineGunChallengeActive && machineGunChallenge.RegisterHit() == 0) EndMachineGunChallenge(); // 全部倒した
                    break;
            }

            float popupSize = 0.1f + 0.1f * Mathf.Clamp01(target.points / 500f); // 高得点ほど大きく
            if (Camera.main != null) // 遠く (UFO など) でも読めるように距離に応じて拡大
                popupSize *= Mathf.Max(1f, Vector3.Distance(Camera.main.transform.position, point) / 3f);
            // 夏祭りの世界では、取った点数がそのままお金になったことが分かるように「+¥」で出す
            ScorePopup.Spawn(font, point + Vector3.up * 0.1f, FestivalActive ? $"+¥{target.points}" : $"+{target.points}", popupColor, popupSize);
        }

        /// <summary>的以外から点数を入れる (スロットの出目など)。pos に点数を浮かべる</summary>
        void AwardPoints(int points, Vector3 pos, Color color, float size)
        {
            if (State != GameState.Playing || points <= 0) return;
            Score += points;
            ScorePopup.Spawn(font, pos, $"+{points}", color, size);
        }

        /// <summary>霧の色と環境光 (sky を渡した時は空の色も) を少しずつ変える</summary>
        void FadeAmbience(Color fog, Color ambient, Color? sky = null, float duration = 1.2f)
        {
            if (ambienceRoutine != null) StopCoroutine(ambienceRoutine);
            ambienceRoutine = StartCoroutine(FadeAmbienceRoutine(fog, ambient, sky, duration));
        }

        /// <summary>
        /// 怪獣 / カジノ / 西部劇モードを始める時の共通の準備: 出ている屋台 (メイン + フィーバー中の屋台) を stageStalls に入れる。
        /// shrink なら集めるだけ (怪獣モードが床といっしょに足元へ縮める)、そうでなければ地下へ沈める
        /// </summary>
        void PutAwayStalls(bool shrink)
        {
            stageStalls.Clear();
            stageStalls.Add(mainStall);
            if (IsFever) stageStalls.AddRange(activeFeverStalls);
            foreach (var s in stageStalls)
            {
                if (!shrink) s.Sink(stallMoveDuration); // 怪獣モード (shrink) は集めるだけ。床といっしょに縮める (ScaleWorld)
            }
        }

        // ---- 床下のハッチ (ボーナスステージの絞り羽根) ----
        // 別の世界へ変わるモード (怪獣以外) では、始まってすぐにハッチがその場で小さくなって消え、屋台が戻る時にふくらんで戻る。
        // 怪獣モードだけは、床や屋台といっしょに足元へ縮む (ScaleWorld)
        const float HatchTime = 0.35f;
        Coroutine hatchRoutine;
        Vector3 hatchScale;
        bool hatchHidden, hatchWasActive;

        // タイトルの間は平らな床 (穴のふたを出して、穴のふちとハッチを隠す) / ラウンド中はいつもの穴のふちとハッチ
        Coroutine flatRoutine;
        Vector3 rimScale;

        void SetFlatFloor(bool flat)
        {
            if (flatRoutine != null) StopCoroutine(flatRoutine);
            flatRoutine = StartCoroutine(FlatFloorRoutine(flat));
        }

        IEnumerator FlatFloorRoutine(bool flat)
        {
            if (pitRim != null && rimScale == Vector3.zero) rimScale = pitRim.localScale.x > 0.5f ? pitRim.localScale : Vector3.one;
            if (flat)
            {
                // ハッチが小さくなって消えてから、ふたで穴をふさぎ、ふちを隠す
                bool wasHidden = hatchHidden;
                HideHatch();
                if (!wasHidden) yield return new WaitForSeconds(HatchTime);
                if (pitCover != null) pitCover.SetActive(true);
                if (pitRim != null) { pitRim.gameObject.SetActive(false); pitRim.localScale = rimScale; }
                flatRoutine = null;
                yield break;
            }
            // ふたが消え、穴のふちとハッチがふくらんで出る
            if (pitCover != null) pitCover.SetActive(false);
            ShowHatch();
            if (pitRim != null)
            {
                pitRim.gameObject.SetActive(true);
                for (float k = 0f; k < 1f; k += Time.deltaTime / HatchTime)
                {
                    pitRim.localScale = rimScale * Mathf.Max(0.001f, 1f - (1f - k) * (1f - k));
                    yield return null;
                }
                pitRim.localScale = rimScale;
            }
            flatRoutine = null;
        }

        void HideHatch()
        {
            if (bonusStage == null || hatchHidden) return;
            hatchHidden = true;
            var t = bonusStage.transform;
            if (hatchScale == Vector3.zero) hatchScale = t.localScale;
            hatchWasActive = t.gameObject.activeSelf;
            if (hatchRoutine != null) StopCoroutine(hatchRoutine);
            hatchRoutine = StartCoroutine(ScaleHatch(false));
        }

        void ShowHatch()
        {
            if (bonusStage == null || !hatchHidden || State != GameState.Playing) return; // タイトルの間は平らな床のまま
            hatchHidden = false;
            if (hatchRoutine != null) StopCoroutine(hatchRoutine);
            hatchRoutine = StartCoroutine(ScaleHatch(true));
        }

        IEnumerator ScaleHatch(bool show)
        {
            var t = bonusStage.transform;
            if (show) t.gameObject.SetActive(hatchWasActive);
            float from = t.localScale.x / Mathf.Max(0.0001f, hatchScale.x), to = show ? 1f : 0f;
            for (float k = 0f; k < 1f; k += Time.deltaTime / HatchTime)
            {
                float e = show ? 1f - (1f - k) * (1f - k) : k * k;
                t.localScale = hatchScale * Mathf.Max(0.001f, Mathf.Lerp(from, to, e));
                yield return null;
            }
            t.localScale = hatchScale * Mathf.Max(0.001f, to);
            if (!show)
            {
                t.gameObject.SetActive(false);
                t.localScale = hatchScale; // 隠している間は元の大きさに戻しておく (他の処理が大きさを使っても困らないように)
            }
            hatchRoutine = null;
        }

        /// <summary>
        /// 世界が変わるモード (海戦・空中・忍者・怪獣・カジノ・西部劇・宇宙) で、屋台が沈みきって少し間をおくまでの秒数。
        /// モードの演出 (壁・床・空の色など) はこれが過ぎてから始める (屋台が引っ込む途中で舞台が動き出さないように)
        /// </summary>
        float StallsAwayTime => stallMoveDuration + 0.4f;

        // delay 秒後に、まだそのモードの最中なら action を行う (屋台が引っ込みきってから空の色を変えるなど)
        IEnumerator WhileActiveAfter(float delay, System.Func<bool> stillActive, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            if (stillActive()) action();
        }

        // 的があった方の向き (度。プレイヤーから見た水平の角度)
        float YawToward(Target target)
        {
            Vector3 dir = target.transform.position - player.transform.position;
            return dir.sqrMagnitude > 0.001f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : 0f;
        }

        IEnumerator FadeAmbienceRoutine(Color fog, Color ambient, Color? sky, float duration)
        {
            Color fogFrom = RenderSettings.fogColor, ambientFrom = RenderSettings.ambientLight;
            var cam = player != null ? player.head : null;
            Color skyFrom = cam != null ? cam.backgroundColor : Color.black;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                RenderSettings.fogColor = Color.Lerp(fogFrom, fog, t);
                RenderSettings.ambientLight = Color.Lerp(ambientFrom, ambient, t);
                if (cam != null && sky.HasValue) cam.backgroundColor = Color.Lerp(skyFrom, sky.Value, t);
                yield return null;
            }
            RenderSettings.fogColor = fog;
            RenderSettings.ambientLight = ambient;
            if (cam != null && sky.HasValue) cam.backgroundColor = sky.Value;
            ambienceRoutine = null;
        }

        // 爆弾の的: 少し間をおいて爆発し、周りの的をまとめて倒す (範囲内の爆弾の的も誘爆する)
        IEnumerator ExplodeAfterDelay(Vector3 point, float radius = -1f)
        {
            yield return new WaitForSeconds(explosiveDelay);
            if (radius <= 0f) radius = explosiveRadius;
            var explosionMat = player != null && player.rightGun != null ? player.rightGun.explosionMaterial : null;
            Bomb.Blast(point, radius, explosionMat);
            ScorePopup.Spawn(font, point + Vector3.up * 0.3f, "BOOM!", new Color(1f, 0.45f, 0.1f), 0.2f);
        }
    }
}
