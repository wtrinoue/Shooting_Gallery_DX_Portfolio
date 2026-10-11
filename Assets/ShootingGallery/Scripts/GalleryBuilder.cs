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
    public class GalleryBuilder : MonoBehaviour
    {
        public const string WorldName = "ShootingGallery";
        const string GeneratedMaterialDir = "Assets/ShootingGallery/Generated/Materials";

        [Header("Layout")]
        [Tooltip("プレイヤー(中心)から屋台までの距離")]
        public float stallRadius = 3.5f;

        [Header("Rules")]
        public float roundDuration = 90f;
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

        // ボーナスステージ: 足元の床の穴 (半分の大きさ) と地下室の深さ
        const float PitHalf = 1.5f;
        const float PitDepth = 5f;

        Transform world;
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
            int stepsTotal = 2 + feverGimmicks.Length;
            int stepsDone = 1;
            msgText.text = LoadingText(stepsDone, stepsTotal);
            LogStep("準備中の表示", step);
            yield return null;

            // ---- 2. メイン屋台の的 ----
            var mainStall = BuildStallContent(mainShell, Gimmick.Classic, mainCloth);
            var startTarget = BuildStartTarget(mainShell);
            if (!editMode) startTarget.SetAvailable(false); // 編集中は見えるまま置く (再生時に GameManager が出し入れする)
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

            // ---- 4. 武器の的 (全屋台の普通の的がときどき化ける) と UFO ----
            BuildWeaponDropper();
            BuildUfo();
            var bonusStage = BuildBonusStage();
            var sniper = BuildSniperChallenge();
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
            gm.stallRadius = stallRadius;
            gm.startTarget = startTarget;
            gm.hudText = hudText;
            gm.messageText = msgText;
            gm.font = font;
            gm.roundDuration = roundDuration;
            gm.feverDuration = feverDuration;
            gm.specialWeaponDuration = weaponSeconds;
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

            // 地面: 足元に PitHalf*2 m 四方の穴が空いた4枚の床 (穴はボーナスステージの床扉でふさがっている)
            // 厚みがあるので、開いた床扉はこの下に滑り込んで見えなくなる
            const float Far = 30f, Thick = 0.2f;
            var groundMat = Mat("Ground", new Color(0.22f, 0.18f, 0.14f));
            var ground = Group(world, "Ground");
            float span = Far - PitHalf;
            Prim(PrimitiveType.Cube, "Ground_N", ground, new Vector3(0f, -Thick / 2f, PitHalf + span / 2f), new Vector3(Far * 2f, Thick, span), groundMat);
            Prim(PrimitiveType.Cube, "Ground_S", ground, new Vector3(0f, -Thick / 2f, -PitHalf - span / 2f), new Vector3(Far * 2f, Thick, span), groundMat);
            Prim(PrimitiveType.Cube, "Ground_E", ground, new Vector3(PitHalf + span / 2f, -Thick / 2f, 0f), new Vector3(span, Thick, PitHalf * 2f), groundMat);
            Prim(PrimitiveType.Cube, "Ground_W", ground, new Vector3(-PitHalf - span / 2f, -Thick / 2f, 0f), new Vector3(span, Thick, PitHalf * 2f), groundMat);

            // 足元の目印 (ここから動かない)。ボーナスステージで床が開いている間は隠れる
            playerMark = Prim(PrimitiveType.Cylinder, "PlayerMark", world, new Vector3(0f, 0.005f, 0f), new Vector3(0.8f, 0.005f, 0.8f),
                              Mat("PlayerMark", new Color(0.9f, 0.8f, 0.4f), new Color(0.3f, 0.25f, 0.05f)), false);
        }

        // ------------------------------------------------------------------
        // プレイヤー (XRリグ + 銃)
        // ------------------------------------------------------------------

        PlayerRig BuildPlayer()
        {
            // 参照を全部つないでから Awake させるため、組み立て中は非アクティブにしておく
            var rigGo = new GameObject("PlayerRig");
            rigGo.SetActive(false);
            rigGo.transform.SetParent(world, false);
            var offset = Group(rigGo.transform, "CameraOffset");

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(offset, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.14f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<AudioSource>();
            camGo.AddComponent<Sfx>();
            AddPoseDriver(camGo, "<XRHMD>/centerEyePosition", "<XRHMD>/centerEyeRotation");

            var right = Group(offset, "RightHand");
            right.localPosition = new Vector3(0.22f, 1.3f, 0.35f);
            AddPoseDriver(right.gameObject, "<XRController>{RightHand}/pointerPosition", "<XRController>{RightHand}/pointerRotation");

            var left = Group(offset, "LeftHand");
            left.localPosition = new Vector3(-0.22f, 1.3f, 0.35f);
            AddPoseDriver(left.gameObject, "<XRController>{LeftHand}/pointerPosition", "<XRController>{LeftHand}/pointerRotation");

            var rig = rigGo.AddComponent<PlayerRig>();
            rig.cameraOffset = offset;
            rig.head = cam;
            rig.rightHand = right;
            rig.leftHand = left;
            rig.rightGun = BuildGun(right, "RightGun", XRNode.RightHand);
            rig.leftGun = BuildGun(left, "LeftGun", XRNode.LeftHand);
            rigGo.SetActive(true);
            return rig;
        }

        static void AddPoseDriver(GameObject go, string position, string rotation)
        {
            var tpd = go.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tpd.ignoreTrackingState = true;
            tpd.positionInput = new InputActionProperty(new InputAction(go.name + " Position", InputActionType.Value, position, expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction(go.name + " Rotation", InputActionType.Value, rotation, expectedControlType: "Quaternion"));
        }

        GunController BuildGun(Transform hand, string name, XRNode node)
        {
            var gunGo = new GameObject(name);
            gunGo.transform.SetParent(hand, false);
            var gun = gunGo.AddComponent<GunController>();
            gun.hapticNode = node;
            gun.tracerMaterial = Mat("Tracer", new Color(1f, 0.9f, 0.5f), null, true);

            var metal = Mat("Gun_Metal", new Color(0.18f, 0.18f, 0.2f));
            var grip = Mat("Gun_Grip", new Color(0.35f, 0.2f, 0.1f));
            var blue = Mat("Gun_Blue", new Color(0.2f, 0.35f, 0.7f));
            var green = Mat("Gun_Green", new Color(0.2f, 0.5f, 0.25f));
            var flashMat = Mat("MuzzleFlash", new Color(1f, 0.8f, 0.3f), null, true);

            // 0: ハンドガン
            var hg = Group(gunGo.transform, "Model_Handgun");
            GunPart(PrimitiveType.Cube, hg, new Vector3(0f, 0f, 0.05f), new Vector3(0.035f, 0.045f, 0.17f), metal);
            GunPart(PrimitiveType.Cube, hg, new Vector3(0f, -0.055f, -0.015f), new Vector3(0.03f, 0.09f, 0.04f), grip, 15f);
            var hgMuzzle = Muzzle(hg, new Vector3(0f, 0.005f, 0.14f), flashMat);

            // 1: マシンガン
            var mg = Group(gunGo.transform, "Model_MachineGun");
            GunPart(PrimitiveType.Cube, mg, new Vector3(0f, 0f, 0.08f), new Vector3(0.05f, 0.065f, 0.28f), blue);
            GunPart(PrimitiveType.Cylinder, mg, new Vector3(0f, 0.01f, 0.28f), new Vector3(0.022f, 0.07f, 0.022f), metal, 90f);
            GunPart(PrimitiveType.Cube, mg, new Vector3(0f, -0.08f, 0.1f), new Vector3(0.03f, 0.11f, 0.045f), metal, -10f);
            GunPart(PrimitiveType.Cube, mg, new Vector3(0f, -0.06f, -0.02f), new Vector3(0.03f, 0.09f, 0.04f), grip, 15f);
            var mgMuzzle = Muzzle(mg, new Vector3(0f, 0.01f, 0.36f), flashMat);

            // 2: ショットガン (二連)
            var sg = Group(gunGo.transform, "Model_Shotgun");
            GunPart(PrimitiveType.Cube, sg, new Vector3(0f, 0f, 0.04f), new Vector3(0.05f, 0.055f, 0.2f), green);
            GunPart(PrimitiveType.Cylinder, sg, new Vector3(-0.013f, 0.01f, 0.28f), new Vector3(0.024f, 0.15f, 0.024f), metal, 90f);
            GunPart(PrimitiveType.Cylinder, sg, new Vector3(0.013f, 0.01f, 0.28f), new Vector3(0.024f, 0.15f, 0.024f), metal, 90f);
            GunPart(PrimitiveType.Cube, sg, new Vector3(0f, -0.06f, -0.04f), new Vector3(0.035f, 0.1f, 0.05f), grip, 20f);
            var sgMuzzle = Muzzle(sg, new Vector3(0f, 0.01f, 0.44f), flashMat);

            // 3: 爆弾ランチャー (太い筒)
            var bm = Group(gunGo.transform, "Model_Bomb");
            var orange = Mat("Gun_Orange", new Color(0.9f, 0.45f, 0.1f));
            GunPart(PrimitiveType.Cylinder, bm, new Vector3(0f, 0.01f, 0.12f), new Vector3(0.075f, 0.16f, 0.075f), orange, 90f);
            GunPart(PrimitiveType.Cylinder, bm, new Vector3(0f, 0.01f, 0.285f), new Vector3(0.085f, 0.012f, 0.085f), metal, 90f);
            GunPart(PrimitiveType.Cube, bm, new Vector3(0f, -0.06f, -0.01f), new Vector3(0.035f, 0.09f, 0.045f), grip, 15f);
            var bmMuzzle = Muzzle(bm, new Vector3(0f, 0.01f, 0.3f), flashMat);

            // 4: スナイパーライフル (長い銃身 + スコープ + ストック)。レーザーポインター付き
            var sr = Group(gunGo.transform, "Model_Sniper");
            var sniperBody = Mat("Gun_Sniper", new Color(0.25f, 0.27f, 0.22f));
            GunPart(PrimitiveType.Cube, sr, new Vector3(0f, 0f, 0.06f), new Vector3(0.04f, 0.05f, 0.26f), sniperBody);
            GunPart(PrimitiveType.Cylinder, sr, new Vector3(0f, 0.01f, 0.36f), new Vector3(0.018f, 0.17f, 0.018f), metal, 90f);
            GunPart(PrimitiveType.Cylinder, sr, new Vector3(0f, 0.055f, 0.08f), new Vector3(0.032f, 0.08f, 0.032f), metal, 90f);   // スコープ
            GunPart(PrimitiveType.Cylinder, sr, new Vector3(0f, 0.055f, 0.165f), new Vector3(0.042f, 0.012f, 0.042f), metal, 90f); // スコープの前玉
            GunPart(PrimitiveType.Cube, sr, new Vector3(0f, -0.015f, -0.12f), new Vector3(0.035f, 0.06f, 0.12f), grip);           // ストック
            GunPart(PrimitiveType.Cube, sr, new Vector3(0f, -0.06f, -0.02f), new Vector3(0.03f, 0.09f, 0.04f), grip, 15f);
            var srMuzzle = Muzzle(sr, new Vector3(0f, 0.01f, 0.53f), flashMat);

            var laserMat = Mat("Laser", new Color(1f, 0.1f, 0.1f), null, true);
            var laserGo = new GameObject("Laser");
            laserGo.transform.SetParent(gunGo.transform, false);
            var laser = laserGo.AddComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.startWidth = 0.004f;
            laser.endWidth = 0.004f;
            laser.sharedMaterial = laserMat;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;
            var dot = RemoveCollider(Prim(PrimitiveType.Sphere, "LaserDot", gunGo.transform, Vector3.zero, Vector3.one * 0.03f, Mat("LaserDot", new Color(1f, 0.25f, 0.2f), null, true)));
            dot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gun.laser = laser;
            gun.laserDot = dot.transform;
            laserGo.SetActive(false);
            dot.SetActive(false);

            gun.bombMaterial = Mat("Bomb", new Color(0.12f, 0.12f, 0.14f), new Color(0.5f, 0.15f, 0f));
            gun.explosionMaterial = TransparentMat("Explosion", new Color(1f, 0.55f, 0.15f, 0.8f));
            gun.models = new[] { hg.gameObject, mg.gameObject, sg.gameObject, bm.gameObject, sr.gameObject };
            gun.muzzles = new[] { hgMuzzle, mgMuzzle, sgMuzzle, bmMuzzle, srMuzzle };
            gun.infoText = MakeText(gunGo.transform, "InfoText", "HANDGUN", new Vector3(0f, 0.11f, 0f), 0.025f, Color.white);

            // 特殊武器の残り時間ゲージ (銃の上に浮かぶ横長バー)
            const float GaugeWidth = 0.14f;
            var gauge = Group(gunGo.transform, "TimeGauge");
            gauge.localPosition = new Vector3(0f, 0.08f, 0f);
            RemoveCollider(Prim(PrimitiveType.Cube, "Frame", gauge, Vector3.zero, new Vector3(GaugeWidth + 0.008f, 0.02f, 0.002f), Mat("Gauge_Frame", new Color(0.05f, 0.05f, 0.06f), null, true)));
            var fill = RemoveCollider(Prim(PrimitiveType.Cube, "Fill", gauge, new Vector3(0f, 0f, -0.002f), new Vector3(GaugeWidth, 0.012f, 0.002f), Mat("Gauge_Fill", Color.white, null, true)));
            fill.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gun.gaugeRoot = gauge;
            gun.gaugeFill = fill.transform;
            gun.gaugeWidth = GaugeWidth;
            return gun;
        }

        void GunPart(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, float xRot = 0f)
        {
            var go = Prim(type, type.ToString(), parent, pos, scale, mat);
            go.transform.localRotation = Quaternion.Euler(xRot, 0f, 0f);
            RemoveCollider(go);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Transform Muzzle(Transform parent, Vector3 pos, Material flashMat)
        {
            var muzzle = Group(parent, "Muzzle");
            muzzle.localPosition = pos;
            var flash = Prim(PrimitiveType.Sphere, "Flash", muzzle, Vector3.forward * 0.03f, new Vector3(0.07f, 0.07f, 0.11f), flashMat);
            RemoveCollider(flash);
            flash.SetActive(false);
            return muzzle;
        }

        // ------------------------------------------------------------------
        // 屋台 (共通の骨組み + ギミックごとの中身)
        // ------------------------------------------------------------------

        enum Gimmick { Classic, Conveyor, FerrisWheel, PopUp, Pendulum, Carousel, Elevator, DuckParade, Roulette, Turnstile }

        /// <summary>
        /// 屋台の外観 (見た目だけ・機能なし)。準備中の表示にも使う。
        /// active = false なら非表示のまま作る (フィーバー屋台)。radius を省略すると stallRadius。
        /// </summary>
        Transform BuildStallShell(string name, float angleDeg, Gimmick gimmick, Material cloth, bool active, float radius = -1f)
        {
            var t = Group(world, name);
            t.gameObject.SetActive(active);
            var rot = Quaternion.Euler(0f, angleDeg, 0f);
            t.localPosition = rot * new Vector3(0f, 0f, radius > 0f ? radius : stallRadius);
            t.localRotation = rot;

            string sign = gimmick switch
            {
                Gimmick.Classic => "SHOOTING GALLERY DX",
                Gimmick.Conveyor => "CONVEYOR",
                Gimmick.FerrisWheel => "FERRIS WHEEL",
                Gimmick.PopUp => "POP-UP PANIC",
                Gimmick.Pendulum => "PENDULUM",
                Gimmick.Carousel => "CAROUSEL",
                Gimmick.Elevator => "ELEVATOR",
                Gimmick.DuckParade => "DUCK PARADE",
                Gimmick.Roulette => "ROULETTE",
                _ => "TURNSTILE"
            };
            BuildStallFrame(t, cloth, sign);

            if (gimmick == Gimmick.Classic)
            {
                // ひな壇とレールも外観に含めておくと、準備中でも完成形に近く見える
                for (int i = 0; i < TierHeights.Length; i++)
                    Prim(PrimitiveType.Cube, $"Tier_{i + 1}", t, new Vector3(0f, TierHeights[i] / 2f, TierZ[i]), new Vector3(3.0f, TierHeights[i], 0.25f), cloth);
                Prim(PrimitiveType.Cube, "TopRail", t, new Vector3(0f, 2.43f, 0.65f), new Vector3(3.2f, 0.03f, 0.08f), Mat("Metal", new Color(0.5f, 0.5f, 0.55f)));
            }
            return t;
        }

        /// <summary>屋台の中身 (的とギミック)。的は全部しまった状態で作る。</summary>
        Stall BuildStallContent(Transform t, Gimmick gimmick, Material cloth)
        {
            // 的は組み立て中は非アクティブにしておき、しまった状態にしてから有効化する
            var targetsRoot = Group(t, "Targets");
            targetsRoot.gameObject.SetActive(false);
            var targets = new List<Target>();
            switch (gimmick)
            {
                case Gimmick.Classic: BuildClassic(targetsRoot, targets); break;
                case Gimmick.Conveyor: BuildConveyor(t, targets); break;
                case Gimmick.FerrisWheel: BuildFerrisWheel(t, targets, cloth); break;
                case Gimmick.PopUp: BuildPopUp(t, targetsRoot, targets, cloth); break;
                case Gimmick.Pendulum: BuildPendulum(t, targets); break;
                case Gimmick.Carousel: BuildCarousel(t, targets, cloth); break;
                case Gimmick.Elevator: BuildElevator(t, targets, cloth); break;
                case Gimmick.DuckParade: BuildDuckParade(t, targets); break;
                case Gimmick.Roulette: BuildRoulette(t, targets, cloth); break;
                case Gimmick.Turnstile: BuildTurnstile(t, targets, cloth); break;
            }

            var stall = t.gameObject.AddComponent<Stall>();
            stall.targets = targets.ToArray();
            if (!editMode) stall.SetTargetsAvailable(false); // 編集中は的が見えるまま置く (再生時に GameManager が出し入れする)
            targetsRoot.gameObject.SetActive(true);
            return stall;
        }

        /// <summary>カウンター・奥の壁・柱・屋根・幕・提灯・看板。どの屋台も共通。</summary>
        void BuildStallFrame(Transform t, Material cloth, string sign)
        {
            var wood = Mat("Wood", new Color(0.55f, 0.36f, 0.2f));
            var woodDark = Mat("Wood_Dark", new Color(0.25f, 0.16f, 0.1f));
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));

            Prim(PrimitiveType.Cube, "Counter", t, new Vector3(0f, 0.5f, -1.2f), new Vector3(2.4f, 1.0f, 0.4f), wood);
            Prim(PrimitiveType.Cube, "CounterTop", t, new Vector3(0f, 1.02f, -1.2f), new Vector3(2.5f, 0.05f, 0.5f), woodDark);

            Prim(PrimitiveType.Cube, "BackWall", t, new Vector3(0f, 1.4f, 0.8f), new Vector3(3.6f, 2.8f, 0.08f), white);
            foreach (var p in new[] { new Vector3(-1.7f, 0f, -0.9f), new Vector3(1.7f, 0f, -0.9f), new Vector3(-1.7f, 0f, 0.75f), new Vector3(1.7f, 0f, 0.75f) })
                Prim(PrimitiveType.Cylinder, "Pillar", t, p + Vector3.up * 1.4f, new Vector3(0.1f, 1.4f, 0.1f), woodDark);
            // 屋根と看板は当たり判定なし (空の UFO を狙う弾をさえぎらないように)
            Prim(PrimitiveType.Cube, "Roof", t, new Vector3(0f, 2.8f, -0.05f), new Vector3(3.7f, 0.08f, 1.8f), woodDark, false);

            for (int i = 0; i < 8; i++)
                RemoveCollider(Prim(PrimitiveType.Cube, $"Awning_{i}", t, new Vector3(-1.575f + i * 0.45f, 2.58f, -0.95f), new Vector3(0.45f, 0.38f, 0.04f), i % 2 == 0 ? cloth : white));

            foreach (float x in new[] { -1.7f, 1.7f })
                RemoveCollider(Prim(PrimitiveType.Sphere, "Lantern", t, new Vector3(x, 2.2f, -1.0f), new Vector3(0.28f, 0.36f, 0.28f), LanternMat()));

            Prim(PrimitiveType.Cube, "SignBoard", t, new Vector3(0f, 3.15f, -0.9f), new Vector3(2.6f, 0.55f, 0.06f), Mat("Sign", new Color(0.1f, 0.08f, 0.06f)), false);
            MakeText(t, "SignText", sign, new Vector3(0f, 3.15f, -0.94f), 0.22f, new Color(1f, 0.85f, 0.35f));
        }

        // ---- メイン: ひな壇 (特殊な的はここにだけある) ----
        // (ひな壇とレールは BuildStallShell で作成済み)
        void BuildClassic(Transform root, List<Target> targets)
        {
            Vector3 Slot(int tier, int s) => new Vector3(SlotX[s], TierHeights[tier], TierZ[tier]);

            // 下段: 10点
            for (int s = 0; s < 5; s++) targets.Add(PointTarget(root, 10, Slot(0, s)));

            // 中段: 10, 50, (左右に動く300), 50, 10
            targets.Add(PointTarget(root, 10, Slot(1, 0)));
            targets.Add(PointTarget(root, 50, Slot(1, 1)));
            var slider = PointTarget(root, 300, Slot(1, 2));
            slider.moving = true;
            slider.moveAmplitude = 0.28f;
            slider.moveSpeed = 2.5f;
            targets.Add(slider);
            targets.Add(PointTarget(root, 50, Slot(1, 3)));
            targets.Add(PointTarget(root, 10, Slot(1, 4)));

            // 上段: 100, 200, フィーバー(赤), 200, 100
            // (武器の的は WeaponDropper が全屋台にランダムで出す)
            targets.Add(PointTarget(root, 100, Slot(2, 0)));
            targets.Add(PointTarget(root, 200, Slot(2, 1)));
            targets.Add(FeverTarget(root, Slot(2, 2)));
            targets.Add(PointTarget(root, 200, Slot(2, 3)));
            targets.Add(PointTarget(root, 100, Slot(2, 4)));

            // 一番上のレールを高速で往復する500点
            var runner = PointTarget(root, 500, new Vector3(0f, 2.45f, 0.65f), 5f);
            runner.moving = true;
            runner.moveAmplitude = 1.3f;
            runner.moveSpeed = 1.6f;
            targets.Add(runner);
        }

        // ---- ベルトコンベアー: 上下2本のベルトが逆向きに流れる ----
        void BuildConveyor(Transform t, List<Target> targets)
        {
            var beltMat = Mat("Belt", new Color(0.12f, 0.12f, 0.13f));
            var slatMat = Mat("BeltSlat", new Color(0.35f, 0.35f, 0.38f));
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            var tunnelMat = Mat("Tunnel", new Color(0.85f, 0.75f, 0.2f));

            BuildBelt(t, targets, "Belt_Lower", 0.95f, 0.05f, 0.5f, new[] { 10, 50, 10, 100, 10, 50 }, beltMat, slatMat, metal, tunnelMat);
            BuildBelt(t, targets, "Belt_Upper", 1.65f, 0.4f, -0.85f, new[] { 100, 200, 50, 300, 100, 500 }, beltMat, slatMat, metal, tunnelMat);
        }

        void BuildBelt(Transform t, List<Target> targets, string name, float y, float z, float speed, int[] values,
                       Material beltMat, Material slatMat, Material metal, Material tunnelMat)
        {
            const float BeltWidth = 3.0f;
            const float LoopLength = 3.4f; // 両端のトンネルの中で折り返す

            Prim(PrimitiveType.Cube, name + "_Body", t, new Vector3(0f, y - 0.04f, z), new Vector3(BeltWidth, 0.08f, 0.32f), beltMat);
            foreach (float x in new[] { -BeltWidth / 2f, BeltWidth / 2f })
            {
                var roller = Prim(PrimitiveType.Cylinder, name + "_Roller", t, new Vector3(x, y - 0.04f, z), new Vector3(0.1f, 0.17f, 0.1f), metal);
                roller.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cube, name + "_Leg", t, new Vector3(x * 0.9f, (y - 0.08f) / 2f, z), new Vector3(0.06f, y - 0.08f, 0.06f), metal);
                Prim(PrimitiveType.Cube, name + "_Tunnel", t, new Vector3(x, y + 0.27f, z), new Vector3(0.45f, 0.55f, 0.42f), tunnelMat);
            }

            var belt = Group(t, name);
            belt.localPosition = new Vector3(0f, y, z);
            var items = new List<Transform>();

            // 流れて見えるようにベルト表面の板も一緒に動かす
            for (int i = 0; i < 16; i++)
            {
                var slat = Prim(PrimitiveType.Cube, "Slat", belt, new Vector3(-LoopLength / 2f + i * LoopLength / 16f, 0.005f, 0f), new Vector3(0.03f, 0.01f, 0.3f), slatMat);
                RemoveCollider(slat);
                items.Add(slat.transform);
            }

            for (int i = 0; i < values.Length; i++)
            {
                var target = PointTarget(belt, values[i], new Vector3(-LoopLength / 2f + (i + 0.5f) * LoopLength / values.Length, 0.01f, 0f));
                targets.Add(target);
                items.Add(target.transform);
            }

            var conveyor = belt.gameObject.AddComponent<ConveyorBelt>();
            conveyor.items = items.ToArray();
            conveyor.length = LoopLength;
            conveyor.speed = speed;
        }

        // ---- 観覧車: 回る輪にぶら下がったゴンドラの上に的 ----
        void BuildFerrisWheel(Transform t, List<Target> targets, Material cloth)
        {
            const float Radius = 0.75f;
            var center = new Vector3(0f, 1.7f, 0.35f);
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));

            // 支柱 (ハの字)
            foreach (float side in new[] { -1f, 1f })
            {
                var leg = Prim(PrimitiveType.Cube, "WheelLeg", t, new Vector3(side * 0.35f, center.y / 2f, center.z + 0.12f), new Vector3(0.06f, center.y + 0.1f, 0.06f), metal);
                leg.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
            }

            var wheel = Group(t, "Wheel");
            wheel.localPosition = center;
            var rotator = wheel.gameObject.AddComponent<Rotator>();
            rotator.axis = Vector3.forward;
            rotator.speed = 22f;

            RemoveCollider(Prim(PrimitiveType.Cylinder, "Hub", wheel, Vector3.zero, new Vector3(0.16f, 0.06f, 0.16f), cloth)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                var spoke = Prim(PrimitiveType.Cube, "Spoke", wheel, Vector3.zero, new Vector3(Radius * 2f, 0.025f, 0.025f), white);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                RemoveCollider(spoke);
            }
            const int RimSegments = 24;
            for (int i = 0; i < RimSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RimSegments;
                var seg = Prim(PrimitiveType.Cube, "Rim", wheel, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius,
                               new Vector3(0.03f, 2f * Mathf.PI * Radius / RimSegments + 0.01f, 0.03f), i % 2 == 0 ? cloth : white);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                RemoveCollider(seg);
            }

            int[] values = { 50, 100, 200, 50, 300, 100 };
            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var gondola = Group(wheel, $"Gondola_{i}");
                gondola.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius;
                gondola.gameObject.AddComponent<KeepUpright>().reference = t;

                RemoveCollider(Prim(PrimitiveType.Cube, "Hanger", gondola, new Vector3(0f, -0.1f, 0f), new Vector3(0.015f, 0.2f, 0.015f), metal));
                Prim(PrimitiveType.Cube, "Floor", gondola, new Vector3(0f, -0.2f, 0f), new Vector3(0.24f, 0.03f, 0.14f), cloth);
                targets.Add(PointTarget(gondola, values[i], new Vector3(0f, -0.185f, 0f)));
            }
        }

        // ---- もぐら叩き: 穴からランダムに一瞬だけ顔を出す ----
        void BuildPopUp(Transform t, Transform root, List<Target> targets, Material cloth)
        {
            float[] rowHeights = { 0.95f, 1.15f, 1.35f };
            float[] rowZ = { -0.15f, 0.2f, 0.55f };
            float[] colX = { -1.05f, -0.35f, 0.35f, 1.05f };
            int[][] values =
            {
                new[] { 50, 100, 50, 100 },
                new[] { 100, 200, 100, 200 },
                new[] { 200, 300, 500, 300 },
            };
            var hole = Mat("Hole", new Color(0.05f, 0.04f, 0.04f));

            var popTargets = new List<Target>();
            for (int r = 0; r < rowHeights.Length; r++)
            {
                Prim(PrimitiveType.Cube, $"Step_{r + 1}", t, new Vector3(0f, rowHeights[r] / 2f, rowZ[r]), new Vector3(3.0f, rowHeights[r], 0.35f), cloth);
                for (int c = 0; c < colX.Length; c++)
                {
                    var pos = new Vector3(colX[c], rowHeights[r], rowZ[r]);
                    RemoveCollider(Prim(PrimitiveType.Cylinder, "Hole", t, pos + Vector3.up * 0.003f, new Vector3(0.3f, 0.003f, 0.3f), hole));
                    var target = PointTarget(root, values[r][c], pos);
                    target.manualControl = true;
                    popTargets.Add(target);
                }
            }
            targets.AddRange(popTargets);

            var popUp = root.gameObject.AddComponent<PopUpGimmick>();
            popUp.targets = popTargets.ToArray();
            popUp.interval = 0.4f;
            popUp.stayTime = 1.4f;
            popUp.maxUp = 5;
        }

        // ---- 振り子: 梁から吊るされた的が左右に揺れる ----
        void BuildPendulum(Transform t, List<Target> targets)
        {
            const float BeamY = 2.6f;
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            var rope = Mat("Rope", new Color(0.85f, 0.8f, 0.65f));
            Prim(PrimitiveType.Cube, "Beam", t, new Vector3(0f, BeamY, 0.2f), new Vector3(3.3f, 0.07f, 0.07f), metal);

            float[] xs = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };
            float[] lengths = { 0.85f, 1.1f, 0.7f, 1.1f, 0.85f };
            int[] values = { 50, 100, 500, 200, 50 };
            float[] speeds = { 1.8f, 1.5f, 2.6f, 1.6f, 2.0f };

            for (int i = 0; i < xs.Length; i++)
            {
                var pivot = Group(t, $"Pendulum_{i}");
                pivot.localPosition = new Vector3(xs[i], BeamY, 0.2f);
                var swing = pivot.gameObject.AddComponent<Pendulum>();
                swing.amplitude = 22f;
                swing.speed = speeds[i];
                swing.phase = i * 1.3f;

                RemoveCollider(Prim(PrimitiveType.Cube, "Rope", pivot, new Vector3(0f, -lengths[i] / 2f, 0f), new Vector3(0.012f, lengths[i], 0.012f), rope));
                // 的の円の上端がロープの先に来るように吊るす
                float d = PointStyle.Diameter(values[i]);
                targets.Add(PointTarget(pivot, values[i], new Vector3(0f, -lengths[i] - d / 2f - TargetDiscY, 0f)));
            }
        }

        // ---- 回転木馬: 2段のターンテーブルが逆向きに回る ----
        void BuildCarousel(Transform t, List<Target> targets, Material cloth)
        {
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var center = new Vector3(0f, 0f, 0.0f);

            Prim(PrimitiveType.Cylinder, "Base", t, center + Vector3.up * 0.45f, new Vector3(0.5f, 0.45f, 0.5f), white);
            Prim(PrimitiveType.Cylinder, "Pole", t, center + Vector3.up * 1.65f, new Vector3(0.06f, 0.75f, 0.06f), gold);
            RemoveCollider(Prim(PrimitiveType.Sphere, "Top", t, center + Vector3.up * 2.45f, Vector3.one * 0.18f, gold));

            BuildTurntable(t, targets, "Turntable_Lower", center + Vector3.up * 0.92f, 0.75f, 0.62f, 35f, new[] { 10, 50, 100, 50, 10, 200, 50, 100 }, cloth);
            BuildTurntable(t, targets, "Turntable_Upper", center + Vector3.up * 1.55f, 0.5f, 0.38f, -50f, new[] { 100, 300, 200, 500, 200 }, white);
        }

        void BuildTurntable(Transform t, List<Target> targets, string name, Vector3 pos, float discRadius, float targetRadius, float speed, int[] values, Material discMat)
        {
            var table = Group(t, name);
            table.localPosition = pos;
            var rotator = table.gameObject.AddComponent<Rotator>();
            rotator.axis = Vector3.up;
            rotator.speed = speed;

            Prim(PrimitiveType.Cylinder, "Disc", table, new Vector3(0f, -0.02f, 0f), new Vector3(discRadius * 2f, 0.02f, discRadius * 2f), discMat);
            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var target = PointTarget(table, values[i], dir * targetRadius);
                target.transform.localRotation = Quaternion.LookRotation(-dir); // 外側を向かせる
                targets.Add(target);
            }
        }

        // ---- エレベーター: 縦のレールに沿って、台に乗った的が上下する ----
        void BuildElevator(Transform t, List<Target> targets, Material cloth)
        {
            const float CenterY = 1.45f, Range = 0.5f, Z = 0.25f;
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            float[] xs = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };
            int[] values = { 50, 100, 500, 200, 50 };
            float[] speeds = { 1.2f, 1.7f, 2.6f, 1.5f, 1.1f };

            for (int i = 0; i < xs.Length; i++)
            {
                Prim(PrimitiveType.Cylinder, "Rail", t, new Vector3(xs[i], 1.6f, Z + 0.1f), new Vector3(0.03f, 0.85f, 0.03f), metal);

                var car = Group(t, $"Car_{i}");
                car.localPosition = new Vector3(xs[i], CenterY, Z);
                var osc = car.gameObject.AddComponent<Oscillator>();
                osc.offset = new Vector3(0f, Range, 0f);
                osc.speed = speeds[i];
                osc.phase = i * 1.1f;

                Prim(PrimitiveType.Cube, "Platform", car, new Vector3(0f, -0.02f, 0f), new Vector3(0.3f, 0.03f, 0.18f), cloth);
                targets.Add(PointTarget(car, values[i], Vector3.zero));
            }
        }

        // ---- アヒルの行進: 水路を、アヒルに乗った的が波打ちながら流れる ----
        void BuildDuckParade(Transform t, List<Target> targets)
        {
            var water = Mat("Water", new Color(0.15f, 0.45f, 0.85f), new Color(0.02f, 0.08f, 0.2f));
            var wood = Mat("Wood", new Color(0.55f, 0.36f, 0.2f));
            var tunnelMat = Mat("Tunnel", new Color(0.85f, 0.75f, 0.2f));

            BuildChannel(t, targets, "Channel_Lower", 0.9f, 0.0f, 0.45f, new[] { 10, 50, 10, 100, 10, 50, 10 }, water, wood, tunnelMat);
            BuildChannel(t, targets, "Channel_Upper", 1.5f, 0.4f, -0.7f, new[] { 50, 200, 100, 300, 50, 200 }, water, wood, tunnelMat);
        }

        void BuildChannel(Transform t, List<Target> targets, string name, float y, float z, float speed, int[] values,
                          Material water, Material wood, Material tunnelMat)
        {
            const float Width = 3.0f;
            const float LoopLength = 3.4f; // 両端のトンネルの中で折り返す
            var yellow = Mat("Duck", new Color(1f, 0.85f, 0.15f));
            var orange = Mat("DuckBeak", new Color(1f, 0.45f, 0.1f));

            Prim(PrimitiveType.Cube, name + "_Trough", t, new Vector3(0f, y / 2f, z), new Vector3(Width, y, 0.35f), wood);
            Prim(PrimitiveType.Cube, name + "_Water", t, new Vector3(0f, y + 0.005f, z), new Vector3(Width - 0.04f, 0.01f, 0.3f), water, false);
            foreach (float x in new[] { -Width / 2f, Width / 2f })
                Prim(PrimitiveType.Cube, name + "_Tunnel", t, new Vector3(x, y + 0.25f, z), new Vector3(0.45f, 0.5f, 0.42f), tunnelMat);

            var lane = Group(t, name);
            lane.localPosition = new Vector3(0f, y, z);
            var items = new List<Transform>();
            float facing = Mathf.Sign(speed); // アヒルのくちばしを進む向きへ
            for (int i = 0; i < values.Length; i++)
            {
                var target = PointTarget(lane, values[i], new Vector3(-LoopLength / 2f + (i + 0.5f) * LoopLength / values.Length, 0f, 0f));
                Prim(PrimitiveType.Sphere, "DuckBody", target.visual, new Vector3(0f, 0.05f, 0f), new Vector3(0.22f, 0.11f, 0.14f), yellow, false);
                Prim(PrimitiveType.Sphere, "DuckHead", target.visual, new Vector3(0.07f * facing, 0.13f, 0f), Vector3.one * 0.08f, yellow, false);
                Prim(PrimitiveType.Cube, "DuckBeak", target.visual, new Vector3(0.115f * facing, 0.125f, 0f), new Vector3(0.04f, 0.015f, 0.03f), orange, false);
                targets.Add(target);
                items.Add(target.transform);
            }

            var conveyor = lane.gameObject.AddComponent<ConveyorBelt>();
            conveyor.items = items.ToArray();
            conveyor.length = LoopLength;
            conveyor.speed = speed;
            conveyor.waveHeight = 0.06f;
            conveyor.waves = 3f;
        }

        // ---- ルーレット: 大きな円盤ごと的が回る (観覧車と違って的も一緒に傾く) ----
        void BuildRoulette(Transform t, List<Target> targets, Material cloth)
        {
            const float Radius = 0.9f;
            var center = new Vector3(0f, 1.7f, 0.5f);
            var white = Mat("Cloth_White", new Color(0.92f, 0.9f, 0.85f));
            var gold = Mat("Gold", new Color(1f, 0.78f, 0.25f), new Color(0.3f, 0.2f, 0f));
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));

            Prim(PrimitiveType.Cube, "Post", t, new Vector3(0f, center.y / 2f, center.z + 0.1f), new Vector3(0.12f, center.y, 0.08f), metal);
            // 上の矢印 (回らない)
            var pointer = Prim(PrimitiveType.Cube, "Pointer", t, new Vector3(0f, center.y + Radius + 0.04f, center.z - 0.06f), new Vector3(0.1f, 0.1f, 0.03f),
                               Mat("Explosive_Band", new Color(0.9f, 0.1f, 0.05f), new Color(0.8f, 0.05f, 0f)), false);
            pointer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var wheel = Group(t, "Wheel");
            wheel.localPosition = center;
            var rotator = wheel.gameObject.AddComponent<Rotator>();
            rotator.axis = Vector3.forward;
            rotator.speed = 40f;

            var disc = Prim(PrimitiveType.Cylinder, "Disc", wheel, new Vector3(0f, 0f, 0.03f), new Vector3(Radius * 2f, 0.02f, Radius * 2f), white);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int k = 0; k < 8; k++) // 放射状の縞模様
            {
                var stripe = Prim(PrimitiveType.Cube, "Stripe", wheel, new Vector3(0f, 0f, 0.005f), new Vector3(Radius * 2f - 0.05f, 0.12f, 0.01f), k % 2 == 0 ? cloth : gold, false);
                stripe.transform.localRotation = Quaternion.Euler(0f, 0f, k * 22.5f);
            }
            const int RimSegments = 24;
            for (int i = 0; i < RimSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RimSegments;
                var seg = Prim(PrimitiveType.Cube, "Rim", wheel, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius,
                               new Vector3(0.04f, 2f * Mathf.PI * Radius / RimSegments + 0.01f, 0.06f), gold, false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }

            // 的: 円の中心が円盤上のその位置に来るように、根元を TargetDiscY だけ下げて置く
            int[] values = { 50, 100, 200, 50, 300, 100 };
            for (int i = 0; i < values.Length; i++)
            {
                float a = i * Mathf.PI * 2f / values.Length;
                var pos = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.62f;
                targets.Add(PointTarget(wheel, values[i], pos + new Vector3(0f, -TargetDiscY, -0.03f)));
            }
            targets.Add(PointTarget(wheel, 500, new Vector3(0f, -TargetDiscY, -0.03f))); // 真ん中は500点
        }

        // ---- 回転扉: くるくる回る板の表と裏に的。板が横を向いた瞬間は当てにくい ----
        void BuildTurnstile(Transform t, List<Target> targets, Material cloth)
        {
            const float Z = 0.2f;
            var metal = Mat("Metal", new Color(0.5f, 0.5f, 0.55f));
            float[] xs = { -1.1f, -0.37f, 0.37f, 1.1f };
            float[] ys = { 1.25f, 1.75f, 1.25f, 1.75f };
            float[] speeds = { 70f, -90f, 110f, -60f };
            int[][] values = { new[] { 10, 100 }, new[] { 50, 200 }, new[] { 100, 300 }, new[] { 50, 500 } };

            for (int i = 0; i < xs.Length; i++)
            {
                float axleHeight = ys[i] - 0.25f;
                Prim(PrimitiveType.Cylinder, "Axle", t, new Vector3(xs[i], axleHeight / 2f, Z), new Vector3(0.03f, axleHeight / 2f, 0.03f), metal);

                var panel = Group(t, $"Panel_{i}");
                panel.localPosition = new Vector3(xs[i], ys[i], Z);
                var rotator = panel.gameObject.AddComponent<Rotator>();
                rotator.axis = Vector3.up;
                rotator.speed = speeds[i];

                Prim(PrimitiveType.Cube, "Board", panel, Vector3.zero, new Vector3(0.5f, 0.5f, 0.03f), cloth);
                targets.Add(PointTarget(panel, values[i][0], new Vector3(0f, -TargetDiscY, -0.02f)));     // 表
                var back = PointTarget(panel, values[i][1], new Vector3(0f, -TargetDiscY, 0.02f));       // 裏
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                targets.Add(back);
            }
        }

        // ------------------------------------------------------------------
        // 的
        // ------------------------------------------------------------------

        const float TargetDiscY = 0.3f; // 的の根元から円の中心までの高さ

        /// <summary>点数付きの的。点数で色と大きさが変わり、中央に点数が書いてある。</summary>
        Target PointTarget(Transform parent, int points, Vector3 localPos, float respawnDelay = 3f)
        {
            var root = Group(parent, $"Target_{points}");
            root.localPosition = localPos;
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Normal;
            target.points = points;
            target.respawnDelay = respawnDelay;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;

            float d = PointStyle.Diameter(points);
            Prim(PrimitiveType.Cube, "Stand", pivot, new Vector3(0f, (TargetDiscY - d / 2f) / 2f, 0f), new Vector3(0.035f, TargetDiscY - d / 2f, 0.035f), Mat("Target_Stand", new Color(0.3f, 0.3f, 0.32f)));

            // 「顔」(円盤と数字)。武器の的に化ける時はここが武器の箱に付け替わる
            var face = Group(pivot, "Face");
            target.face = face.gameObject;
            var color = Mat($"Points_{points}", PointStyle.TargetColor(points), PointStyle.Emission(points));
            Bullseye(face, TargetDiscY, d, color, Mat("Target_White", Color.white), null);
            MakeText(face, "Points", points.ToString(), new Vector3(0f, TargetDiscY, -0.012f), d * 0.32f, new Color(0.1f, 0.1f, 0.1f));
            return target;
        }

        // ------------------------------------------------------------------
        // 武器の的のランダム出現 / UFO
        // ------------------------------------------------------------------

        void BuildWeaponDropper()
        {
            var go = new GameObject("WeaponDropper");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            // 武器の的の見た目 (箱 + 文字)。普通の的の「顔」と付け替えて使い回す。同時に出るのは最大2個
            var looks = new List<GameObject>();
            foreach (var kind in new[] { TargetKind.MachineGun, TargetKind.Shotgun, TargetKind.Bomb, TargetKind.Explosive, TargetKind.BlueOrb, TargetKind.Sniper })
                for (int i = 0; i < 2; i++)
                    looks.Add(WeaponLook(go.transform, kind));

            var dropper = go.AddComponent<WeaponDropper>();
            dropper.looks = looks.ToArray();
            foreach (var look in looks) look.SetActive(false);
            go.SetActive(true);
        }

        /// <summary>武器の的の見た目。的の根元を原点に、円盤と同じ高さに箱が来るように作る。</summary>
        GameObject WeaponLook(Transform parent, TargetKind kind)
        {
            var look = Group(parent, $"Look_{kind}");

            if (kind == TargetKind.BlueOrb)
            {
                // 青い玉: フィーバーの赤い玉の青い版。輪が回っている
                Prim(PrimitiveType.Sphere, "BlueOrb", look, new Vector3(0f, TargetDiscY, 0f), Vector3.one * 0.24f,
                     Mat("Target_BlueOrb", new Color(0.15f, 0.45f, 1f), new Color(0.2f, 0.6f, 1.8f)));
                var spin = Group(look, "Spinner");
                spin.localPosition = new Vector3(0f, TargetDiscY, 0f);
                var rotator = spin.gameObject.AddComponent<Rotator>();
                rotator.axis = Vector3.up;
                rotator.speed = 150f;
                var ring = Prim(PrimitiveType.Cylinder, "Ring", spin, Vector3.zero, new Vector3(0.36f, 0.008f, 0.36f),
                                Mat("Target_BlueRing", new Color(0.7f, 0.9f, 1f), new Color(0.3f, 0.5f, 0.9f)), false);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                return look.gameObject;
            }

            if (kind == TargetKind.Explosive)
            {
                // 爆弾の的: 黒くて丸い爆弾 + 導火線 + 火花 + 赤い帯
                Prim(PrimitiveType.Sphere, "BombBody", look, new Vector3(0f, TargetDiscY, 0f), Vector3.one * 0.22f,
                     Mat("Explosive_Body", new Color(0.08f, 0.08f, 0.1f)));
                var band = Prim(PrimitiveType.Cylinder, "Band", look, new Vector3(0f, TargetDiscY, 0f), new Vector3(0.226f, 0.02f, 0.226f),
                     Mat("Explosive_Band", new Color(0.9f, 0.1f, 0.05f), new Color(0.8f, 0.05f, 0f)), false);
                band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cylinder, "Cap", look, new Vector3(0f, TargetDiscY + 0.115f, 0f), new Vector3(0.06f, 0.02f, 0.06f),
                     Mat("Gun_Metal", new Color(0.18f, 0.18f, 0.2f)), false);
                var fuse = Prim(PrimitiveType.Cylinder, "Fuse", look, new Vector3(0.02f, TargetDiscY + 0.16f, 0f), new Vector3(0.012f, 0.04f, 0.012f),
                     Mat("Rope", new Color(0.85f, 0.8f, 0.65f)), false);
                fuse.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
                Prim(PrimitiveType.Sphere, "Spark", look, new Vector3(0.04f, TargetDiscY + 0.2f, 0f), Vector3.one * 0.04f,
                     Mat("Explosive_Spark", new Color(1f, 0.85f, 0.3f), new Color(2f, 1.4f, 0.3f)), false);
                return look.gameObject;
            }

            if (kind == TargetKind.Sniper)
            {
                // スナイパーの的: 黒い板に赤い照準 (丸 + 十字) と「SNIPER」の文字
                var c = new Vector3(0f, TargetDiscY, 0f);
                Prim(PrimitiveType.Cube, "Plate", look, c, new Vector3(0.28f, 0.28f, 0.04f), Mat("Sniper_Plate", new Color(0.08f, 0.08f, 0.1f)));
                var red = Mat("Sniper_Red", new Color(1f, 0.15f, 0.15f), new Color(1.6f, 0.1f, 0.1f));
                var ringOuter = Prim(PrimitiveType.Cylinder, "Scope", look, c + new Vector3(0f, 0.02f, -0.022f), new Vector3(0.17f, 0.002f, 0.17f), red, false);
                ringOuter.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var ringInner = Prim(PrimitiveType.Cylinder, "ScopeHole", look, c + new Vector3(0f, 0.02f, -0.024f), new Vector3(0.13f, 0.002f, 0.13f),
                                     Mat("Sniper_Plate", new Color(0.08f, 0.08f, 0.1f)), false);
                ringInner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Prim(PrimitiveType.Cube, "CrossH", look, c + new Vector3(0f, 0.02f, -0.026f), new Vector3(0.2f, 0.012f, 0.002f), red, false);
                Prim(PrimitiveType.Cube, "CrossV", look, c + new Vector3(0f, 0.02f, -0.026f), new Vector3(0.012f, 0.2f, 0.002f), red, false);
                MakeText(look, "Label", "SNIPER", c + new Vector3(0f, -0.105f, -0.025f), 0.05f, Color.white);
                return look.gameObject;
            }

            (string label, Color col) = kind switch
            {
                TargetKind.MachineGun => ("MG", new Color(0.25f, 0.5f, 1f)),
                TargetKind.Shotgun => ("SG", new Color(0.25f, 0.85f, 0.35f)),
                _ => ("BOMB", new Color(1f, 0.5f, 0.1f))
            };
            Prim(PrimitiveType.Cube, "Box", look, new Vector3(0f, TargetDiscY, 0f), new Vector3(0.26f, 0.19f, 0.08f), Mat($"Target_{kind}", col, col * 0.4f));
            MakeText(look, "Label", label, new Vector3(0f, TargetDiscY, -0.045f), label.Length > 2 ? 0.07f : 0.11f, Color.white);
            return look.gameObject;
        }

        /// <summary>
        /// ボーナスステージ: プレイヤーの足元の床扉 (左右にスライドして開く) と、その下の地下室。
        /// 地下室の底には上向きの大きな的と、当てるたびに点く8個のランプがある。
        /// </summary>
        BonusStage BuildBonusStage()
        {
            const float TargetDiameter = 1.6f;
            var go = new GameObject("BonusStage");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            // ---- 床扉 (左右2枚。周りの床より少しだけ低く、開くと床の下へ滑り込む) ----
            var doorMat = Mat("Door_Metal", new Color(0.33f, 0.34f, 0.38f));
            var stripeMat = Mat("Tunnel", new Color(0.85f, 0.75f, 0.2f));
            Transform Door(string name, float side)
            {
                var door = Group(go.transform, name);
                door.localPosition = new Vector3(side * PitHalf / 2f, -0.04f, 0f);
                Prim(PrimitiveType.Cube, "Panel", door, Vector3.zero, new Vector3(PitHalf - 0.005f, 0.06f, PitHalf * 2f), doorMat);
                // 合わせ目の黄色い線と、横向きの溝
                Prim(PrimitiveType.Cube, "Stripe", door, new Vector3(-side * (PitHalf / 2f - 0.06f), 0.031f, 0f), new Vector3(0.08f, 0.004f, PitHalf * 2f), stripeMat, false);
                for (int i = -2; i <= 2; i++)
                    Prim(PrimitiveType.Cube, "Groove", door, new Vector3(0f, 0.031f, i * 0.55f), new Vector3(PitHalf - 0.2f, 0.004f, 0.03f),
                         Mat("Door_Groove", new Color(0.2f, 0.2f, 0.23f)), false);
                return door;
            }
            var doorLeft = Door("DoorLeft", -1f);
            var doorRight = Door("DoorRight", 1f);

            // ---- 地下室 (穴の真下の箱。壁は床の下から底まで) ----
            var room = Group(go.transform, "Basement");
            var wallMat = Mat("Basement_Wall", new Color(0.2f, 0.18f, 0.26f));
            var floorMat = Mat("Basement_Floor", new Color(0.08f, 0.08f, 0.12f));
            var lightMat = Mat("Basement_Light", new Color(0.5f, 0.8f, 1f), new Color(0.6f, 1.2f, 1.8f));
            float wallH = PitDepth - 0.21f;          // 周りの床 (厚さ 0.2) の下から底まで
            float wallY = -0.21f - wallH / 2f;
            float w = PitHalf + 0.1f;
            Prim(PrimitiveType.Cube, "Wall_N", room, new Vector3(0f, wallY, w), new Vector3(PitHalf * 2f + 0.4f, wallH, 0.2f), wallMat);
            Prim(PrimitiveType.Cube, "Wall_S", room, new Vector3(0f, wallY, -w), new Vector3(PitHalf * 2f + 0.4f, wallH, 0.2f), wallMat);
            Prim(PrimitiveType.Cube, "Wall_E", room, new Vector3(w, wallY, 0f), new Vector3(0.2f, wallH, PitHalf * 2f), wallMat);
            Prim(PrimitiveType.Cube, "Wall_W", room, new Vector3(-w, wallY, 0f), new Vector3(0.2f, wallH, PitHalf * 2f), wallMat);
            Prim(PrimitiveType.Cube, "Floor", room, new Vector3(0f, -PitDepth - 0.1f, 0f), new Vector3(PitHalf * 2f + 0.4f, 0.2f, PitHalf * 2f + 0.4f), floorMat);
            // 四隅の光る柱と、壁の横線 (深さが分かるように)
            foreach (var c in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
                Prim(PrimitiveType.Cube, "LightStrip", room, new Vector3(c.x * (PitHalf - 0.04f), -PitDepth / 2f - 0.1f, c.y * (PitHalf - 0.04f)),
                     new Vector3(0.06f, PitDepth - 0.4f, 0.06f), lightMat, false);
            for (int k = 1; k <= 3; k++)
            {
                float y = -PitDepth * k / 4f;
                Prim(PrimitiveType.Cube, "Ring_N", room, new Vector3(0f, y, PitHalf - 0.01f), new Vector3(PitHalf * 2f, 0.03f, 0.02f), lightMat, false);
                Prim(PrimitiveType.Cube, "Ring_S", room, new Vector3(0f, y, -PitHalf + 0.01f), new Vector3(PitHalf * 2f, 0.03f, 0.02f), lightMat, false);
                Prim(PrimitiveType.Cube, "Ring_E", room, new Vector3(PitHalf - 0.01f, y, 0f), new Vector3(0.02f, 0.03f, PitHalf * 2f), lightMat, false);
                Prim(PrimitiveType.Cube, "Ring_W", room, new Vector3(-PitHalf + 0.01f, y, 0f), new Vector3(0.02f, 0.03f, PitHalf * 2f), lightMat, false);
            }

            // ---- 底の大きな的 (上を向いて寝かせてある。当てても倒れず揺れるだけ) ----
            var root = Group(room, "BigTarget");
            root.localPosition = new Vector3(0f, -PitDepth + 0.02f, 0f);
            root.localRotation = Quaternion.Euler(90f, 0f, 0f); // 的の表 (-Z) を真上に向ける。文字の上はメイン屋台側
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.BigBonus;
            target.points = 100;
            target.manualControl = true;
            target.stayUpOnHit = true;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            var blue = Mat("Target_BigBlue", new Color(0.15f, 0.4f, 1f), new Color(0.05f, 0.15f, 0.5f));
            Bullseye(pivot, 0f, TargetDiameter, blue, Mat("Target_White", Color.white), blue);
            MakeText(pivot, "Label", "DO RE MI", new Vector3(0f, TargetDiameter * 0.34f, -0.02f), 0.12f, Color.white);

            // ---- 当てるたびに点くランプ (的のまわりに8個、ド〜ドの虹色) ----
            Color[] rainbow =
            {
                new Color(1f, 0.2f, 0.2f), new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.1f), new Color(0.3f, 1f, 0.3f),
                new Color(0.2f, 0.9f, 1f), new Color(0.25f, 0.4f, 1f), new Color(0.7f, 0.3f, 1f), new Color(1f, 0.4f, 0.8f)
            };
            var lampOff = Mat("Lamp_Off", new Color(0.25f, 0.25f, 0.3f));
            var lampsOn = new GameObject[rainbow.Length];
            for (int i = 0; i < rainbow.Length; i++)
            {
                // メイン屋台側 (+Z) から時計回りに並べる
                float a = i * Mathf.PI * 2f / rainbow.Length;
                var pos = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (TargetDiameter / 2f + 0.3f) + Vector3.up * (-PitDepth + 0.08f);
                Prim(PrimitiveType.Sphere, $"Lamp_{i}_Off", room, pos, Vector3.one * 0.14f, lampOff, false);
                lampsOn[i] = Prim(PrimitiveType.Sphere, $"Lamp_{i}_On", room, pos, Vector3.one * 0.17f, Mat($"Lamp_On_{i}", rainbow[i], rainbow[i] * 2.2f), false);
            }

            var stage = go.AddComponent<BonusStage>();
            stage.doorLeft = doorLeft;
            stage.doorRight = doorRight;
            stage.slideDistance = PitHalf;
            stage.bigTarget = target;
            stage.lampsOn = lampsOn;
            stage.hideWhileOpen = playerMark != null ? new[] { playerMark } : new GameObject[0];
            foreach (var lamp in lampsOn) lamp.SetActive(false);
            go.SetActive(true);
            return stage;
        }

        /// <summary>
        /// スナイパーチャレンジの遠くの大きな的。宙に浮かぶ金と赤の的 + 光る輪 + 「3000」。
        /// 編集中はメイン屋台の奥の遠くに置いておく (再生時は隠れていて、チャレンジのたびに SniperChallenge が動かす)。
        /// </summary>
        SniperChallenge BuildSniperChallenge()
        {
            const float Diameter = 2.2f;
            var go = new GameObject("SniperChallenge");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            var root = Group(go.transform, "FarTarget");
            root.localPosition = new Vector3(0f, 3.5f, 22f);

            // 的本体 (根元が下端。撃たれると後ろに倒れる)
            var targetRoot = Group(root, "BigTarget");
            targetRoot.localPosition = new Vector3(0f, -Diameter / 2f, 0f);
            var target = targetRoot.gameObject.AddComponent<Target>();
            target.kind = TargetKind.SniperBig;
            target.points = 3000;
            target.manualControl = true;
            var pivot = Group(targetRoot, "Pivot");
            target.visual = pivot;
            var gold = Mat("Sniper_Gold", new Color(1f, 0.75f, 0.15f), new Color(0.9f, 0.55f, 0.05f));
            var red = Mat("Sniper_BigRed", new Color(1f, 0.12f, 0.1f), new Color(1.1f, 0.08f, 0.05f));
            Bullseye(pivot, Diameter / 2f, Diameter, red, Mat("Target_White", Color.white), gold);
            MakeText(pivot, "Label", "3000", new Vector3(0f, Diameter / 2f, -0.03f), 0.28f, new Color(0.15f, 0.05f, 0.05f));

            // 飾り: 的のまわりの光る輪と、照準の目盛り (当たり判定なし)
            var glow = Mat("Sniper_Glow", new Color(1f, 0.3f, 0.3f), new Color(2f, 0.3f, 0.25f), true);
            var halo = Prim(PrimitiveType.Cylinder, "Halo", root, new Vector3(0f, 0f, 0.05f), new Vector3(Diameter + 0.35f, 0.005f, Diameter + 0.35f), glow, false);
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (var d in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right })
            {
                Prim(PrimitiveType.Cube, "Tick", root, d * (Diameter / 2f + 0.35f), new Vector3(0.06f, 0.06f, 0.02f) + new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), 0f) * 0.3f, glow, false);
            }
            MakeText(root, "Title", "SNIPER CHALLENGE", new Vector3(0f, Diameter / 2f + 0.75f, 0f), 0.3f, new Color(1f, 0.85f, 0.3f));

            var challenge = go.AddComponent<SniperChallenge>();
            challenge.root = root;
            challenge.bigTarget = target;
            root.gameObject.SetActive(false); // チャレンジの時だけ出る
            go.SetActive(true);
            return challenge;
        }

        void BuildUfo()
        {
            var go = new GameObject("UfoFlyer");
            go.SetActive(false);
            go.transform.SetParent(world, false);

            var root = Group(go.transform, "UFO");
            root.localPosition = new Vector3(0f, 6.5f, 2f); // 編集中に見た目を確認できる位置 (再生時は UfoFlyer が動かす)
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Ufo;
            target.points = 1000;
            target.manualControl = true;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;

            // 円盤 (当たり判定はこれ) + ドーム + 回るライト
            var saucer = Prim(PrimitiveType.Sphere, "Saucer", pivot, Vector3.zero, new Vector3(1.3f, 0.28f, 1.3f), Mat("Ufo_Body", new Color(0.7f, 0.72f, 0.8f)));
            saucer.GetComponent<SphereCollider>().radius = 0.7f; // 見た目より少し大きめにして遠くでも当てやすく
            Prim(PrimitiveType.Sphere, "Dome", pivot, new Vector3(0f, 0.12f, 0f), new Vector3(0.55f, 0.4f, 0.55f),
                 Mat("Ufo_Dome", new Color(0.4f, 0.9f, 1f), new Color(0.2f, 0.6f, 0.8f)), false);
            var lights = Group(pivot, "Lights");
            var yellow = Mat("Ufo_LightA", new Color(1f, 0.9f, 0.3f), new Color(1.5f, 1.2f, 0.2f));
            var magenta = Mat("Ufo_LightB", new Color(1f, 0.3f, 0.9f), new Color(1.4f, 0.2f, 1.2f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Prim(PrimitiveType.Sphere, "Light", lights, new Vector3(Mathf.Cos(a), -0.03f, Mathf.Sin(a)) * 0.6f, Vector3.one * 0.1f, i % 2 == 0 ? yellow : magenta, false);
            }
            target.spinner = lights;
            target.spinSpeed = 120f;

            // うなり音: UFO の位置から聞こえる立体音響
            var hum = root.gameObject.AddComponent<AudioSource>();
            hum.loop = true;
            hum.playOnAwake = false;
            hum.spatialBlend = 1f;
            hum.rolloffMode = AudioRolloffMode.Linear;
            hum.minDistance = 4f;
            hum.maxDistance = 45f;
            hum.volume = 0.7f;
            hum.dopplerLevel = 0.5f;

            var flyer = go.AddComponent<UfoFlyer>();
            flyer.ufo = target;
            target.SetAvailable(false); // 飛ぶ時だけ見える (シーンに生成した時も隠しておく)
            flyer.hum = hum;
            go.SetActive(true);
        }

        /// <summary>フィーバーの赤い的 (メイン屋台の最上段)。当てると周りに屋台が現れる。</summary>
        Target FeverTarget(Transform parent, Vector3 localPos)
        {
            var root = Group(parent, "Target_Fever");
            root.localPosition = localPos;
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Fever;
            target.points = 300;
            target.respawnDelay = 25f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;

            Prim(PrimitiveType.Cube, "Stand", pivot, new Vector3(0f, 0.08f, 0f), new Vector3(0.04f, 0.16f, 0.04f), Mat("Target_Stand", new Color(0.3f, 0.3f, 0.32f)));
            Prim(PrimitiveType.Sphere, "RedOrb", pivot, new Vector3(0f, 0.32f, 0f), Vector3.one * 0.26f, Mat("Target_Fever", new Color(1f, 0.1f, 0.1f), new Color(1.5f, 0.05f, 0.05f)));
            var spinner = Group(pivot, "Spinner");
            spinner.localPosition = new Vector3(0f, 0.32f, 0f);
            var ring = Prim(PrimitiveType.Cylinder, "Ring", spinner, Vector3.zero, new Vector3(0.38f, 0.008f, 0.38f), Mat("Target_FeverRing", new Color(1f, 0.8f, 0.2f), new Color(0.6f, 0.4f, 0f)), false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            target.spinner = spinner;
            target.spinSpeed = 150f;
            return target;
        }

        Target BuildStartTarget(Transform stall)
        {
            var root = Group(stall, "Target_Start");
            root.localPosition = new Vector3(0f, 1.1f, -0.6f);
            var target = root.gameObject.AddComponent<Target>();
            target.kind = TargetKind.Start;
            target.points = 0;
            target.respawnDelay = 1f;
            var pivot = Group(root, "Pivot");
            target.visual = pivot;
            Prim(PrimitiveType.Cube, "Stand", pivot, new Vector3(0f, 0.15f, 0f), new Vector3(0.05f, 0.3f, 0.05f), Mat("Target_Stand", new Color(0.3f, 0.3f, 0.32f)));
            var red = Mat("Target_StartRed", new Color(0.85f, 0.1f, 0.1f), new Color(0.3f, 0f, 0f));
            Bullseye(pivot, 0.62f, 0.55f, red, Mat("Target_White", Color.white), red);
            MakeText(pivot, "Label", "START", new Vector3(0f, 0.62f, -0.03f), 0.13f, new Color(0.1f, 0.1f, 0.1f));
            return target;
        }

        /// <summary>同心円の的。外側の円にだけ BoxCollider を付ける。center が null なら2色。</summary>
        void Bullseye(Transform pivot, float centerY, float diameter, Material outer, Material middle, Material center)
        {
            var o = Disc(pivot, "Outer", centerY, diameter, 0f, outer);
            o.AddComponent<BoxCollider>();
            Disc(pivot, "Middle", centerY, diameter * 0.66f, -0.006f, middle);
            if (center != null)
                Disc(pivot, "Center", centerY, diameter * 0.3f, -0.012f, center);
        }

        // 当たり判定なしの円盤 (的の模様用)
        GameObject Disc(Transform parent, string name, float y, float diameter, float z, Material mat)
        {
            var go = Prim(PrimitiveType.Cylinder, name, parent, new Vector3(0f, y, z), new Vector3(diameter, 0.01f, diameter), mat, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return go;
        }

        // ------------------------------------------------------------------
        // ユーティリティ
        // ------------------------------------------------------------------

        /// <summary>プリミティブを作る。position / scale は parent のローカル。collider = false なら当たり判定なし。</summary>
        protected GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : world, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            if (!collider) RemoveCollider(go);
            else if (type == PrimitiveType.Cylinder && scale.y * 2f < Mathf.Max(scale.x, scale.z))
            {
                // 円柱の標準の当たり判定はカプセル型なので、薄い円盤にすると直径と同じ大きさの「球」になってしまい
                // 周りの的まで覆って弾をさえぎる。形どおりの凸メッシュの当たり判定に置き換える。
                DestroyImmediate(go.GetComponent<Collider>());
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                mc.convex = true;
            }
            return go;
        }

        protected static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>当たり判定を消す (飾りの部品用)。渡した GameObject をそのまま返す。</summary>
        protected static GameObject RemoveCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) DestroyImmediate(c);
            return go;
        }

        protected TextMesh MakeText(Transform parent, string name, string text, Vector3 localPos, float height, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = height / 6.4f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        Material LanternMat() => Mat("Lantern", new Color(1f, 0.55f, 0.2f), new Color(1.6f, 0.6f, 0.15f));

        /// <summary>URP マテリアルを生成 (同じ名前なら使い回す)。</summary>
        protected Material Mat(string name, Color color, Color? emission = null, bool unlit = false)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;

            var mat = new Material(unlit ? unlitShader : litShader) { name = name };
            mat.SetColor("_BaseColor", color);
            if (!unlit) mat.SetFloat("_Smoothness", 0.25f);
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        /// <summary>半透明の Unlit マテリアル (爆発エフェクト用)。</summary>
        protected Material TransparentMat(string name, Color color)
        {
            if (mats.TryGetValue(name, out var cached)) return cached;

            var mat = new Material(unlitShader) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat = SaveAsAssetIfEditing(mat, name);
            mats[name] = mat;
            return mat;
        }

        /// <summary>
        /// 編集中の生成なら、マテリアルを Assets/ShootingGallery/Generated/Materials に .mat として保存する。
        /// (アセットにしておかないと、シーンに保存・コピペした時にマテリアルが外れてしまう)
        /// 同じ名前のファイルがあれば中身を上書きして使い回すので、何度生成してもファイルは増えない。
        /// </summary>
        Material SaveAsAssetIfEditing(Material mat, string name)
        {
#if UNITY_EDITOR
            if (!editMode) return mat;

            EnsureFolder(GeneratedMaterialDir);
            string path = $"{GeneratedMaterialDir}/{name}.mat";
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(mat, path);
                return mat;
            }

            existing.shader = mat.shader;
            existing.CopyPropertiesFromMaterial(mat);
            existing.shaderKeywords = mat.shaderKeywords;
            existing.SetOverrideTag("RenderType", mat.GetTag("RenderType", false));
            existing.renderQueue = mat.renderQueue;
            UnityEditor.EditorUtility.SetDirty(existing);
            DestroyImmediate(mat);
            return existing;
#else
            return mat;
#endif
        }

#if UNITY_EDITOR
        static void EnsureFolder(string path)
        {
            if (UnityEditor.AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            UnityEditor.AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
#endif
    }
}
