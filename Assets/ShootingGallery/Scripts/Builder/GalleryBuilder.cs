using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace ShootingGallery
{
    /// <summary>
    /// プリミティブだけで射的屋さんのゲーム一式 (屋台・的・銃・プレイヤー・HUD) を生成する道具。
    ///
    /// 【おすすめの使い方 (編集中に生成)】
    ///   インスペクターの「シーンに生成」ボタン (または右クリック → Build Into Scene) を押すと、
    ///   子オブジェクト「ShootingGallery」に一式が作られる。マテリアルはアセットとして保存されるので、
    ///   この「ShootingGallery」をコピーして好きなシーンに貼り付けるだけで、再生と同時にゲームが動く。
    ///   (貼り付けた後はこの GalleryBuilder は不要)
    ///
    /// 【おまけ (再生時に生成)】
    ///   シーンにゲーム一式がまだ無い状態で再生すると、その場で生成する。
    ///
    /// オブジェクトを追加したい時は BuildRoutine() の中に追記する。
    /// 位置は全てこの GameObject からの相対座標 (原点に置いておくのがおすすめ)。
    /// </summary>
    [DisallowMultipleComponent]
    public partial class GalleryBuilder : MonoBehaviour
    {
        public const string WorldName = "ShootingGallery";
        const string GeneratedMaterialDir = "Assets/ShootingGallery/Generated/Materials";

        [Header("Layout")]
        [Tooltip("プレイヤー(中心)から屋台までの距離")]
        public float stallRadius = 3.5f;

        [Header("Rules")]
        public float roundDuration = 120f;
        public float feverDuration = 20f;
        [Tooltip("マシンガン / ショットガン / 爆弾 を使える秒数")]
        public float weaponSeconds = 3f;

        [Header("Shaders (自動設定。ビルドに含めるために参照を保持)")]
        public Shader litShader;
        public Shader unlitShader;

        // 屋台ローカル座標: +Z が奥 (プレイヤーから離れる方向)
        static readonly float[] TierHeights = { 0.85f, 1.3f, 1.75f };
        static readonly float[] TierZ = { 0.0f, 0.25f, 0.5f };
        static readonly float[] SlotX = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };

        Transform world;
        GunController builtGun; // 生成した右手の銃 (武器の見た目を、武器の的のフィギュアに使い回す)
        Font font;
        GameObject playerMark;
        bool editMode; // true = 編集中にシーンへ生成している (マテリアルをアセット保存, 的は見える状態で置く)
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        void Reset()
        {
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        void Prepare()
        {
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mats.Clear();
            meshes.Clear();
        }

        void Awake()
        {
            // すでにゲーム一式がシーンにある (生成済み or 貼り付け済み) なら何もしない
            if (FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) != null)
            {
                enabled = false;
                return;
            }

            Prepare();
            world = new GameObject(WorldName).transform;
            world.SetParent(transform, false);
            StartCoroutine(BuildRoutine());
        }

#if UNITY_EDITOR
        /// <summary>編集中にゲーム一式を子オブジェクトとして生成する (インスペクターのボタンから呼ばれる)。</summary>
        [ContextMenu("Build Into Scene (シーンに生成)")]
        public void BuildIntoScene()
        {
            if (Application.isPlaying)
            {
                UnityEngine.Debug.LogWarning("[GalleryBuilder] 再生中は使えません。再生を止めてから押してください。");
                return;
            }

            var old = transform.Find(WorldName);
            if (old != null) UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);

            editMode = true;
            try
            {
                Prepare();
                world = new GameObject(WorldName).transform;
                world.SetParent(transform, false);

                var routine = BuildRoutine();
                while (routine.MoveNext()) { } // 編集中は1フレームで最後まで作る

                UnityEditor.Undo.RegisterCreatedObjectUndo(world.gameObject, "Build Shooting Gallery");
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                UnityEditor.Selection.activeGameObject = world.gameObject;
            }
            finally
            {
                editMode = false;
            }
        }
#endif

        // ==================================================================
        // ここに生成処理を書く
        // 1フレーム目で「見た目だけの準備中の状態」を出し、残りは1フレームに1軒ずつ組み立てる。
        // (VR で長く固まると酔いやすいので、重い処理を分けている)
        // ==================================================================

        IEnumerator BuildRoutine()
        {
            var total = Stopwatch.StartNew();
            var step = Stopwatch.StartNew();

            // ---- 1. すぐ見せるもの: 地面・プレイヤー・HUD・メイン屋台の外観 ----
            BuildEnvironment();
            var player = BuildPlayer();
            var (hudText, msgText) = BuildHud(player);
            hudText.text = "SHOOTING GALLERY DX";

            var mainCloth = Mat("Cloth_Red", new Color(0.75f, 0.1f, 0.12f));
            var mainShell = BuildStallShell("Stall_Main", 0f, Gimmick.Classic, mainCloth, true);

            // フィーバー用の屋台は全ギミック分を作っておき、フィーバーのたびに GameManager がランダムに選んで並べる
            var feverGimmicks = new[]
            {
                Gimmick.Conveyor, Gimmick.FerrisWheel, Gimmick.PopUp, Gimmick.Pendulum, Gimmick.Carousel,
                Gimmick.Elevator, Gimmick.DuckParade, Gimmick.Roulette, Gimmick.Turnstile
            };
            var feverColors = new[]
            {
                new Color(0.15f, 0.35f, 0.75f), new Color(0.1f, 0.55f, 0.3f), new Color(0.55f, 0.2f, 0.65f),
                new Color(0.85f, 0.45f, 0.1f), new Color(0.1f, 0.55f, 0.6f), new Color(0.75f, 0.15f, 0.45f),
                new Color(0.2f, 0.45f, 0.85f), new Color(0.15f, 0.15f, 0.2f), new Color(0.6f, 0.5f, 0.1f)
            };
            int stepsTotal = 3 + feverGimmicks.Length + DeluxeFeverGimmicks.Length;
            int stepsDone = 1;
            msgText.text = LoadingText(stepsDone, stepsTotal);
            LogStep("準備中の表示", step);
            yield return null;

            // ---- 2. メイン屋台の的 ----
            var mainStall = BuildStallContent(mainShell, Gimmick.Classic, mainCloth);
            var startTarget = BuildStartTarget(mainShell);
            var controlsBoard = BuildControlsBoard(); // 背中側の操作方法の看板 (シーンに生成した時も見えるまま置く)
            // START の的はシーンに生成した時も見えるまま置く (タイトルで最初に出る的。再生時は GameManager が出し入れする)
            msgText.text = LoadingText(++stepsDone, stepsTotal);
            LogStep("メイン屋台", step);
            yield return null;

            // ---- 3. フィーバー屋台 (全ギミック分。シーンに生成した時も再生時も、隠した状態で作る) ----
            // 置き場所は仮 (メイン屋台のまわりに等間隔)。再生時は GameManager がフィーバーのたびに並べ直す。
            // 見た目を確認したい時は、Hierarchy で Stall_〇〇 を一時的にアクティブにすればよい
            var feverStalls = new Stall[feverGimmicks.Length];
            for (int i = 0; i < feverGimmicks.Length; i++)
            {
                float angle = 360f / (feverGimmicks.Length + 1) * (i + 1);

                var gimmick = feverGimmicks[i];
                var cloth = Mat($"Cloth_{gimmick}", feverColors[i % feverColors.Length]);
                var shell = BuildStallShell($"Stall_{gimmick}", angle, gimmick, cloth, false);
                feverStalls[i] = BuildStallContent(shell, gimmick, cloth);
                msgText.text = LoadingText(++stepsDone, stepsTotal);
                LogStep($"フィーバー屋台 {gimmick}", step);
                yield return null;
            }

            // ---- 3b. 豪華屋台 (DELUXE の的を撃つと、出ている屋台がこれに入れ替わる。隠した状態で作る) ----
            var deluxeMain = BuildDeluxeStall("Stall_Deluxe_Grand", DeluxeGimmick.Grand, 0, 0f);
            msgText.text = LoadingText(++stepsDone, stepsTotal);
            LogStep("豪華屋台 Grand", step);
            yield return null;
            var deluxeFever = new Stall[DeluxeFeverGimmicks.Length];
            for (int i = 0; i < DeluxeFeverGimmicks.Length; i++)
            {
                var gimmick = DeluxeFeverGimmicks[i];
                float angle = 360f / (DeluxeFeverGimmicks.Length + 1) * (i + 1);
                deluxeFever[i] = BuildDeluxeStall($"Stall_Deluxe_{gimmick}", gimmick, i + 1, angle);
                msgText.text = LoadingText(++stepsDone, stepsTotal);
                LogStep($"豪華屋台 {gimmick}", step);
                yield return null;
            }

            // ---- 4. 武器の的 (全屋台の普通の的がときどき化ける) と UFO ----
            BuildWeaponDropper();
            BuildUfo();
            var bonusStage = BuildBonusStage();
            var sniper = BuildSniperChallenge();
            var machineGunChallenge = BuildMachineGunChallenge();
            var navalBattle = BuildNavalBattle();
            var skyBattle = BuildSkyBattle(bonusStage);
            var ninjaDojo = BuildNinjaDojo();
            var kaijuCity = BuildKaijuCity();
            var casinoHall = BuildCasino();
            var westernTrain = BuildWestern();
            var spaceStage = BuildSpace(bonusStage);
            var ghostManor = BuildGhost();
            var mineCave = BuildMine();
            LogStep("武器の的 / UFO", step);

            // ---- 5. ゲーム開始 ----
            var gmGo = new GameObject("GameManager");
            gmGo.SetActive(false);
            gmGo.transform.SetParent(world, false);
            var gm = gmGo.AddComponent<GameManager>();
            gm.player = player;
            gm.mainStall = mainStall;
            gm.feverStalls = feverStalls;
            gm.bonusStage = bonusStage;
            gm.sniper = sniper;
            gm.machineGunChallenge = machineGunChallenge;
            gm.navalBattle = navalBattle;
            gm.skyBattle = skyBattle;
            gm.ninjaDojo = ninjaDojo;
            gm.kaijuCity = kaijuCity;
            gm.casinoHall = casinoHall;
            gm.westernTrain = westernTrain;
            gm.spaceStage = spaceStage;
            gm.ghostManor = ghostManor;
            gm.mineCave = mineCave;
            gm.deluxeMainStall = deluxeMain;
            gm.deluxeFeverStalls = deluxeFever;
            gm.fireworkMaterials = FireworkMaterials();
            gm.stallRadius = stallRadius;
            gm.startTarget = startTarget;
            gm.controlsBoard = controlsBoard;
            gm.hudText = hudText;
            gm.messageText = msgText;
            gm.font = font;
            gm.roundDuration = roundDuration;
            gm.feverDuration = feverDuration;
            gm.specialWeaponDuration = weaponSeconds;
            // 床下のハッチは、生成した時は小さくして隠しておく (ゲームが始まるとふくらんで出てくる。GameManager.Start)
            if (bonusStage != null) bonusStage.transform.localScale = Vector3.one * 0.001f;
            // タイトルの間は平らな床: 穴を地面と同じ色のふたでふさぎ、穴のふちも隠しておく (ラウンドが始まるとふたが消え、ふちとハッチが出る)
            var pitRim = world.Find("PitRim");
            var pitCover = Prim(PrimitiveType.Cylinder, "PitCover", world, new Vector3(0f, 0.0015f, 0f), new Vector3(2f * (PitRadius + 0.15f), 0.0015f, 2f * (PitRadius + 0.15f)),
                                Mat("Ground", new Color(0.22f, 0.18f, 0.14f)), false);
            if (pitRim != null) pitRim.gameObject.SetActive(false);
            gm.pitCover = pitCover;
            gm.pitRim = pitRim;
            gmGo.SetActive(true);
            if (editMode) msgText.text = ""; // 「NOW LOADING」をシーンに残さない

            // ↓ 追加のオブジェクトはこの下に書く
            // 例: Prim(PrimitiveType.Cube, "MyBox", world, new Vector3(2f, 0.5f, 1f), Vector3.one * 0.5f, Mat("MyBox", Color.cyan));

            UnityEngine.Debug.Log($"[GalleryBuilder] 生成完了: 合計 {total.ElapsedMilliseconds} ms");
        }

        static string LoadingText(int done, int total) => $"NOW LOADING...\n{done} / {total}";

        static void LogStep(string label, Stopwatch sw)
        {
            UnityEngine.Debug.Log($"[GalleryBuilder] {label}: {sw.ElapsedMilliseconds} ms");
            sw.Restart();
        }

        (TextMesh hud, TextMesh message) BuildHud(PlayerRig player)
        {
            var hud = new GameObject("HUD");
            hud.transform.SetParent(world, false);
            hud.transform.localPosition = new Vector3(0f, 2.05f, 2.2f);
            var follow = hud.AddComponent<HudFollow>();
            follow.head = player.head.transform;
            var hudText = MakeText(hud.transform, "HudText", "", new Vector3(0f, 0.12f, 0f), 0.11f, Color.white);
            var msgText = MakeText(hud.transform, "MessageText", "", new Vector3(0f, -0.35f, 0f), 0.16f, Color.white);
            return (hudText, msgText);
        }

        // ------------------------------------------------------------------
        // 環境
        // ------------------------------------------------------------------

        void BuildEnvironment()
        {
            // 環境光・霧・元からあるカメラの停止は、再生時に SceneEnvironment が行う (コピペ先のシーンでも効くように)
            world.gameObject.AddComponent<SceneEnvironment>();

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(world, false);
            lightGo.transform.localRotation = Quaternion.Euler(55f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.88f, 0.72f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;

            BuildGround();
            // 足元の目印 (黄色い丸) は不要になったので作らない。playerMark は null のまま (使う側はすべて null チェック済み)
        }
    }
}
