using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.Utilities; // (InputSystem.onAnyButtonPress.Call)

namespace ShootingGallery
{
    /// <summary>
    /// プレイヤー。中心から移動せず、見回して撃つだけ。
    /// 右手トリガーで右の銃を撃つ。フィーバー中の左手の銃は左手トリガーで撃つ。
    /// 右手の A ボタンを長押しすると (インジケーターがたまりきると) 最初に戻る。B ボタンの長押しでエンドレスモード、左手の X ボタンの長押しでデバッグモードが始まる。
    /// VRデバイスが無い時はマウスでテストできる (右ドラッグか WASD で視点, 左クリック/Spaceで撃つ (二丁拳銃の時は左右の銃をいっしょに。左の銃はカーソルの少し左を狙う), 1 キー長押しでデバッグ, 2 キー長押しでエンドレス, 3 キー長押しでリセット。VR のボタンと同じく長押しで発動)。
    /// 長押しのボタンは1つずつ (最初に押し始めたボタンを離すまで、ほかはたまらない)。
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public Transform cameraOffset;
        public Camera head;
        public Transform rightHand;
        public Transform leftHand;
        public GunController rightGun;
        public GunController leftGun;
        [Tooltip("手に持つコントローラーの模型 (タイトルで操作方法の看板を見ている間だけ、銃の代わりに出す)")]
        public GameObject rightControllerModel, leftControllerModel;
        [Tooltip("コントローラーの間に引き金を引くと出る白い玉の材質")]
        public Material pointerBallMaterial;

        public float standingEyeHeight = 1.6f;
        // (以前は 0.85 で、START を撃つまでずっと 0.85m 高く、撃った瞬間に身長の高さへ下がっていた。今は起動してすぐ身長に合わせる: AutoCalibrate)
        [Tooltip("VRの時に視点をこの分 (m) だけ実際より高くする (身長に合わせる前の、ほんの一瞬だけ。0 = 実際の高さのまま)")]
        public float xrHeightBoost = 0f;
        [Tooltip("身長に対する目の高さの割合 (目の高さ = 身長 × この値)")]
        public float eyeHeightRatio = 0.935f;
        [Tooltip("視点を合わせる身長 (cm)。誰が遊んでも、この身長の人と同じ高さから屋台が見える")]
        public int referenceHeightCm = 170;
        public float mouseSensitivity = 0.15f;
        [Tooltip("PC: WASD キーで視点を回す速さ (度/秒)。A / D で左右、W / S で上下")]
        public float keyLookSpeed = 90f;
        [Tooltip("PC: 左の銃 (二丁拳銃) がカーソルより左を狙う量 (狙う点までの距離に対する割合。0.035 で約 2°)")]
        public float desktopLeftAimOffset = 0.035f;

        [Header("リセット (右手の A ボタン長押し)")]
        [Tooltip("最初に戻るまで押し続ける秒数")]
        public float resetHoldTime = 2f;
        [Tooltip("たまり具合を見せる輪 (カメラの子)")]
        public ResetIndicator resetIndicator;
        /// <summary>A ボタンを resetHoldTime 秒押し続けた (GameManager が最初に戻す)</summary>
        public event System.Action ResetRequested;

        [Header("エンドレスモード (右手の B ボタン長押し)")]
        [Tooltip("エンドレスモードが始まるまで押し続ける秒数")]
        public float endlessHoldTime = 2f;
        [Tooltip("たまり具合を見せる輪 (カメラの子)")]
        public ResetIndicator endlessIndicator;
        /// <summary>B ボタンを endlessHoldTime 秒押し続けた (GameManager がエンドレスモードを始める)</summary>
        public event System.Action EndlessRequested;

        InputAction resetButton;
        float resetHeld;
        bool resetFired; // 1回押し続けて発動したら、離すまで次は数えない
        InputAction endlessButton;
        float endlessHeld;
        bool endlessFired;

        [Header("デバッグモード (左手の X ボタン長押し)")]
        [Tooltip("デバッグモードが始まるまで押し続ける秒数")]
        public float debugHoldTime = 2f;
        [Tooltip("たまり具合を見せる輪 (カメラの子)")]
        public ResetIndicator debugIndicator;
        /// <summary>X ボタンを debugHoldTime 秒押し続けた (GameManager がデバッグモードを始める)</summary>
        public event System.Action DebugRequested;

        InputAction debugButton;
        float debugHeld;
        bool debugFired;

        float? calibratedOffsetY; // 身長に合わせた CameraOffset の高さ (選ぶまでは null)
        float xrStartedAt = -1f;  // VR になった時刻 (起動してすぐ身長に合わせる時に、頭の追跡が始まるのを少し待つ)

        InputAction rightTrigger;
        InputAction leftTrigger;

        WeaponType weapon = WeaponType.Handgun;
        float weaponTimeLeft;
        float weaponDuration;
        bool stageWeapon; // モード専用の武器 (時間が来ても自動では戻らない。GameManager が屋台が見えてから戻す)
        bool stageGaugeFresh; // モード専用の武器を渡したばかり (次にモードの残り時間に合わせる時、その時間をゲージの満タンにする)
        bool dualWield;

        bool? xrModeApplied;
        float yaw, pitch;
        TrackedPoseDriver rightPose, leftPose;
        float handCheckAt = -1f; // VR になってから手の追跡を確かめる時刻 (ログ用)

        public bool DualWield => dualWield;
        public WeaponType Weapon => weapon;

        // ---- 視点を整える (どのモードでもない時の、いつもの立ち位置・大きさ・視点の高さに戻す) ----
        // 発掘・潜水艦・音楽モードはプレイヤーを上下に動かす。不具合や途中で止められたことで元に戻りきらなくても、
        // GameManager がここを呼んで、いつもの視点に戻す (ResetView)

        Vector3 homePos, homeScale = Vector3.one;
        Quaternion homeRot = Quaternion.identity;
        Coroutine viewRoutine;
        bool floorOrigin; // VR の床の高さを、床を基準に取れているか (ApplyMode で決まる)

        /// <summary>視点を元に戻している途中か</summary>
        public bool IsRestoringView => viewRoutine != null;

        // ---- 今どちらの操作で遊んでいるか (タイトルの操作方法の看板で、最初にどちらの説明を見せるかに使う) ----

        /// <summary>
        /// VR のコントローラーで遊んでいるか (false = マウスとキーボード)。最後に押されたボタンの種類で決める。
        /// VR が動いていない時はいつも false。VR に入った時は true から始める。
        /// </summary>
        public bool UsingVRControls { get; private set; }
        System.IDisposable anyButtonListener; // InputSystem.onAnyButtonPress の登録 (ボタンが押された時だけ呼ばれる。毎フレーム調べない)

        // 何かのボタンが押された: 今と違う種類 (VR のコントローラー ⇔ マウス・キーボード) の時だけ切り替える。
        // 同じ種類なら何もしない (早期リターン)。ヘッドセットの位置・向きはボタンではないので数えない (置いてあるだけで切り替わらないように)
        void OnAnyButton(InputControl control)
        {
            bool vr = control.device is XRController;
            if (vr == UsingVRControls) return;
            if (vr && !XRBridge.IsActive) return; // (VR が動いていない時は、マウスとキーボードのまま)
            UsingVRControls = vr;
        }

        // 今の視点の高さの元 (CameraOffset)。VR は身長に合わせた値 (まだなら床の取り方に応じた値)、VR でなければ 0
        Vector3 HomeCameraOffset()
        {
            if (xrModeApplied != true) return Vector3.zero;
            return Vector3.up * (calibratedOffsetY ?? (floorOrigin ? 0f : standingEyeHeight) + xrHeightBoost);
        }

        /// <summary>いつもの立ち位置・大きさ・視点の高さからずれているか (1cm より大きくずれていたら true)</summary>
        public bool IsViewOff
        {
            get
            {
                if ((transform.localPosition - homePos).sqrMagnitude > 0.0001f) return true;
                if ((transform.localScale - homeScale).sqrMagnitude > 0.0001f) return true;
                if (Quaternion.Angle(transform.localRotation, homeRot) > 0.5f) return true;
                if (cameraOffset != null && xrModeApplied == true && calibratedOffsetY.HasValue
                    && Mathf.Abs(cameraOffset.localPosition.y - calibratedOffsetY.Value) > 0.01f) return true;
                return false;
            }
        }

        /// <summary>
        /// 視点を整える: いつもの立ち位置・大きさ・向きと、視点の高さ (CameraOffset) に戻す。
        /// instant でなければ 0.4 秒かけてなめらかに戻す (急に動くと VR で酔うので)
        /// </summary>
        public void ResetView(bool instant)
        {
            if (viewRoutine != null) StopCoroutine(viewRoutine);
            viewRoutine = null;
            if (instant || !isActiveAndEnabled)
            {
                transform.localPosition = homePos;
                transform.localScale = homeScale;
                transform.localRotation = homeRot;
                if (cameraOffset != null) cameraOffset.localPosition = HomeCameraOffset();
                return;
            }
            viewRoutine = StartCoroutine(ResetViewSmooth(0.4f));
        }

        System.Collections.IEnumerator ResetViewSmooth(float seconds)
        {
            Vector3 fromPos = transform.localPosition, fromScale = transform.localScale;
            Quaternion fromRot = transform.localRotation;
            Vector3 fromOffset = cameraOffset != null ? cameraOffset.localPosition : Vector3.zero, toOffset = HomeCameraOffset();
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3f - 2f * t);
                transform.localPosition = Vector3.Lerp(fromPos, homePos, e);
                transform.localScale = Vector3.Lerp(fromScale, homeScale, e);
                transform.localRotation = Quaternion.Slerp(fromRot, homeRot, e);
                if (cameraOffset != null) cameraOffset.localPosition = Vector3.Lerp(fromOffset, toOffset, e);
                yield return null;
            }
            viewRoutine = null;
            ResetView(true);
        }

        void Awake()
        {
            homePos = transform.localPosition; // ゲームが始まった時の立ち位置・大きさ・向きを「いつもの視点」として覚える
            homeScale = transform.localScale;
            homeRot = transform.localRotation;
            if (rightHand != null) rightPose = rightHand.GetComponent<TrackedPoseDriver>();
            if (leftHand != null) leftPose = leftHand.GetComponent<TrackedPoseDriver>();

            rightTrigger =new InputAction("RightTrigger", InputActionType.Button);
            rightTrigger.AddBinding("<XRController>{RightHand}/trigger");
            rightTrigger.AddBinding("<Mouse>/leftButton");
            rightTrigger.AddBinding("<Keyboard>/space");

            leftTrigger = new InputAction("LeftTrigger", InputActionType.Button);
            leftTrigger.AddBinding("<XRController>{LeftHand}/trigger"); // (PC には左の銃のボタンはない。二丁拳銃の時は左クリック / Space で両方撃つ)

            resetButton = new InputAction("Reset", InputActionType.Button);
            // ボタンは用途 ({PrimaryButton} など) でも指定する: WebXR のコントローラーは名前が buttonA / buttonB で、primaryButton では見つからない
            resetButton.AddBinding("<XRController>{RightHand}/primaryButton"); // Quest の A ボタン
            resetButton.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            resetButton.AddBinding("<Keyboard>/3");       // PC: 3 キー (テンキーも可)
            resetButton.AddBinding("<Keyboard>/numpad3");

            endlessButton = new InputAction("Endless", InputActionType.Button);
            endlessButton.AddBinding("<XRController>{RightHand}/secondaryButton"); // Quest の B ボタン
            endlessButton.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            endlessButton.AddBinding("<Keyboard>/2");     // PC: 2 キー
            endlessButton.AddBinding("<Keyboard>/numpad2");

            debugButton = new InputAction("Debug", InputActionType.Button);
            debugButton.AddBinding("<XRController>{LeftHand}/primaryButton"); // Quest の X ボタン
            debugButton.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            debugButton.AddBinding("<Keyboard>/1");       // PC: 1 キー
            debugButton.AddBinding("<Keyboard>/numpad1");
        }

        void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
            rightTrigger.Enable();
            leftTrigger.Enable();
            resetButton.Enable();
            endlessButton.Enable();
            debugButton.Enable();
            anyButtonListener = InputSystem.onAnyButtonPress.Call(OnAnyButton);
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
            anyButtonListener?.Dispose();
            anyButtonListener = null;
            rightTrigger.Disable();
            leftTrigger.Disable();
            resetButton.Disable();
            endlessButton.Disable();
            debugButton.Disable();
        }

        void Start()
        {
            SetDualWield(false);
            ResetWeapon();
        }

        // ---- 視点のずれの見張り (原因を調べるためのログ) ----
        // プレイヤー (この物)・CameraOffset が、コードで動かされたらログに出す (どのモードの何で動いたか、スタックで分かる)。
        // 1フレームが長くかかった (VR では画面が引きずられて、視点がずれたように見える) 時もログに出す
        [Tooltip("視点が動いた時・フレームが重かった時に Console へログを出す (調べ終わったら切る)")]
        public bool logViewChanges = true;
        Vector3 watchPos, watchScale, watchOffset;
        Quaternion watchRot;
        bool watchReady;

        void LateUpdate()
        {
            if (!logViewChanges) return;
            var off = cameraOffset != null ? cameraOffset.localPosition : Vector3.zero;
            if (watchReady)
            {
                if ((transform.localPosition - watchPos).sqrMagnitude > 1e-6f || (transform.localScale - watchScale).sqrMagnitude > 1e-6f
                    || Quaternion.Angle(transform.localRotation, watchRot) > 0.05f || (off - watchOffset).sqrMagnitude > 1e-6f)
                    Debug.Log($"[ViewWatch] 視点が動きました: 位置 {watchPos} → {transform.localPosition}, 向き {watchRot.eulerAngles} → {transform.localRotation.eulerAngles}, " +
                              $"大きさ {watchScale} → {transform.localScale}, CameraOffset {watchOffset} → {off}");
                if (Time.unscaledDeltaTime > 0.05f)
                    Debug.Log($"[ViewWatch] フレームが重くなりました: {Time.unscaledDeltaTime * 1000f:0} ms (VR では画面が引きずられて、視点がずれたように見えます)");
            }
            watchPos = transform.localPosition;
            watchScale = transform.localScale;
            watchRot = transform.localRotation;
            watchOffset = off;
            watchReady = true;
        }

        void Update()
        {
            bool xr = XRBridge.IsActive;
            if (xrModeApplied != xr) ApplyMode(xr);
            if (!xr) UpdateDesktop();
            else UpdateHandsFallback();
            if (xr) AutoCalibrate();

            UpdateReset();

            // 射撃
            // (手元がコントローラーの模型の間は、どちらの手からも撃たない)
            bool rPressed = !controllerView && rightTrigger.WasPressedThisFrame();
            bool rHeld = !controllerView && rightTrigger.IsPressed();
            if (RightGunArriving) rPressed = rHeld = false; // (的から右手へ飛んでくる間は、まだ撃てない)
            rightGun.TriggerInput(rPressed, rHeld);

            // コントローラーの間は弾を撃たない代わりに、コントローラーの先から白い玉が出る (看板のボタン (CREDITS・ページ送り) だけを押せる。PointerBall)
            if (controllerView)
            {
                if (rightTrigger.WasPressedThisFrame()) ShootPointerBall(rightHand);
                if (leftTrigger.WasPressedThisFrame()) ShootPointerBall(leftHand);
            }

            if (dualWield && leftGun != null && !LeftGunArriving) // (的から左手へ飛んでくる間は、まだ撃てない)
            {
                // VR は左手のトリガー、PC は右の銃と同じ左クリック / Space で、左右の銃をいっしょに撃つ
                var leftInput = xr ? leftTrigger : rightTrigger;
                leftGun.TriggerInput(!controllerView && leftInput.WasPressedThisFrame(), !controllerView && leftInput.IsPressed());
            }

            // 特殊武器の残り時間
            if (weapon != WeaponType.Handgun)
            {
                weaponTimeLeft -= Time.deltaTime;
                if (weaponTimeLeft <= 0f)
                {
                    if (stageWeapon) weaponTimeLeft = 0f; // (モード専用の武器は、帰りの途中で勝手にハンドガンにならないように持ったまま)
                    else ResetWeapon();
                }
            }
            var def = Weapons.Get(weapon);
            if (controllerView) rightGun.SetInfo(""); // (銃を隠している間は、武器の名前も出さない)
            else rightGun.SetInfo(rightGun.IsCharging ? "CHARGING..." : rightGun.NeedsReload ? $"RELOAD {rightGun.Loaded}/{def.magazine} : PULL TRIGGER" :rightGun.IsReloading ? (weapon == WeaponType.Poi ? "NEXT POI..." : "RELOADING...") : def.displayName); // 残りの弾は銃の上の弾の列で見せる
            if (leftGun != null) leftGun.SetInfo("");

            // 弾込めのゲージで見せる武器 (コルク銃・ポイ): 撃つと空になり、たまっていく。満タンになったら撃てる (武器の色に変わる)
            if (def.reloadGauge)
            {
                SetReloadGauge(rightGun);
                if (leftGun != null) SetReloadGauge(leftGun);
                return;
            }
            // 残り時間はゲージで表示 (ハンドガンの時は非表示)
            float ratio = weapon == WeaponType.Handgun || weaponDuration <= 0f ? 0f : weaponTimeLeft / weaponDuration;
            Color gaugeColor = ratio < 0.3f ? new Color(1f, 0.25f, 0.2f) : GaugeColor(weapon);
            rightGun.SetGauge(ratio, gaugeColor);
            if (leftGun != null) leftGun.SetGauge(ratio, gaugeColor);
        }

        void SetReloadGauge(GunController gun)
        {
            float k = gun.ReloadProgress;
            gun.SetGauge(Mathf.Max(0.02f, k), k >= 1f ? GaugeColor(weapon) : new Color(0.55f, 0.55f, 0.6f));
        }

        static Color GaugeColor(WeaponType type) => type switch
        {
            WeaponType.MachineGun => new Color(0.3f, 0.6f, 1f),
            WeaponType.AirMachineGun => new Color(0.3f, 0.6f, 1f),
            WeaponType.Shotgun => new Color(0.3f, 0.95f, 0.4f),
            WeaponType.Bomb => new Color(1f, 0.6f, 0.15f),
            WeaponType.Sniper => new Color(1f, 0.2f, 0.25f),
            WeaponType.Bazooka => new Color(0.4f, 0.8f, 1f),
            WeaponType.Shuriken => new Color(0.85f, 0.85f, 0.95f),
            WeaponType.FireBall => new Color(1f, 0.5f, 0.1f),
            WeaponType.Dart => new Color(1f, 0.3f, 0.5f),
            WeaponType.Revolver => new Color(0.9f, 0.7f, 0.4f),
            WeaponType.Flashlight => new Color(1f, 0.92f, 0.55f),
            WeaponType.Pickaxe => new Color(0.85f, 0.65f, 0.4f),
            WeaponType.Missile => new Color(0.55f, 0.75f, 1f),
            WeaponType.RingToss => new Color(1f, 0.45f, 0.35f),
            WeaponType.CorkGun => new Color(1f, 0.8f, 0.3f),
            WeaponType.Poi => new Color(0.4f, 0.8f, 1f),
            WeaponType.Coin => new Color(1f, 0.85f, 0.25f),
            WeaponType.Torpedo => new Color(0.35f, 0.9f, 1f),
            WeaponType.Trumpet => new Color(1f, 0.82f, 0.3f),
            WeaponType.Imagination => new Color(0.95f, 0.6f, 1f),
            WeaponType.Lightning => new Color(0.75f, 0.4f, 1f),
            _ => Color.white
        };

        /// <summary>モード専用の武器を持たせる (時間が来ても自動ではハンドガンに戻らない。戻すのは GameManager.ReleaseStageWeapon)</summary>
        public void GiveStageWeapon(WeaponType type, float duration, Vector3? from = null)
        {
            GiveWeapon(type, duration, from);
            stageWeapon = true;
            stageGaugeFresh = true;
        }

        /// <summary>
        /// モード専用の武器を、別のモード専用の武器に持ち替える (狙った先で武器が変わる時)。ゲージ (残り時間と満タンの長さ) はそのまま続ける
        /// </summary>
        public void SwitchStageWeapon(WeaponType type)
        {
            float left = weaponTimeLeft, full = weaponDuration;
            bool changed = type != weapon;
            GiveWeapon(type, left, null, false); // (夏祭り・カジノの、見ている所での持ち替え: たぐり寄せず、手が少し震えるだけ)
            if (changed)
            {
                XRBridge.Haptic(UnityEngine.XR.XRNode.RightHand, 0.3f, 0.06f);
                if (dualWield) XRBridge.Haptic(UnityEngine.XR.XRNode.LeftHand, 0.3f, 0.06f);
            }
            weaponDuration = full;
            stageWeapon = true;
        }

        /// <summary>
        /// モード専用の武器のゲージを、モードの残り時間 (timeLeft) に合わせる (GameManager が毎フレーム呼ぶ)。
        /// モードはこの残り時間が 0 になった瞬間に元の世界へ戻り始めるので、ゲージが空になった瞬間に戻り始める
        /// </summary>
        public void SyncStageWeaponTime(float timeLeft)
        {
            if (!stageWeapon) return;
            timeLeft = Mathf.Max(0f, timeLeft);
            weaponDuration = stageGaugeFresh ? timeLeft : Mathf.Max(weaponDuration, timeLeft);
            stageGaugeFresh = false;
            weaponTimeLeft = timeLeft;
        }

        /// <summary>
        /// 武器を持ち替える。銃が入れ替わる時は、手元へたぐり寄せる (PullGunsFrom): from = 持ち替えのもとになった的の場所。
        /// 渡さない時は、目の前の少し先から (DefaultPullFrom)。いつもの銃 (ハンドガン) に戻る時は、たぐり寄せない
        /// </summary>
        public void GiveWeapon(WeaponType type, float duration, Vector3? from = null, bool pull = true)
        {
            bool changed = type != weapon; // (銃が入れ替わる時だけ、たぐり寄せる)
            stageWeapon = false;
            weapon = type;
            weaponTimeLeft = duration;
            weaponDuration = duration;
            rightGun.SetWeapon(type);
            if (leftGun != null) leftGun.SetWeapon(type);
            if (pull && changed && type != WeaponType.Handgun) PullGunsFrom(from ?? DefaultPullFrom());
        }

        public void ResetWeapon()
        {
            // (いつもの銃に戻る時は、たぐり寄せない。飛んでくる途中だったら、その場で手の中に戻す)
            StopFlight(rightGun, rightFlight);
            StopFlight(leftGun, leftFlight);
            weapon = WeaponType.Handgun;
            weaponTimeLeft = 0f;
            stageWeapon = false;
            rightGun.SetWeapon(weapon);
            if (leftGun != null) leftGun.SetWeapon(weapon);
        }

        bool controllerView;

        // コントローラーの先から白い玉を出す
        void ShootPointerBall(Transform hand)
        {
            if (hand == null || pointerBallMaterial == null) return;
            PointerBall.Spawn(hand.position + hand.forward * 0.06f, hand.forward, pointerBallMaterial);
        }


        /// <summary>手元を銃の代わりにコントローラーの模型にする / 銃に戻す (操作方法の看板を見ている間。撃つことはできる)</summary>
        public void SetControllerView(bool on)
        {
            if (controllerView == on) return;
            controllerView = on;
            if (rightControllerModel != null) rightControllerModel.SetActive(on);
            if (leftControllerModel != null) leftControllerModel.SetActive(on);
            if (rightGun != null) rightGun.SetModelHidden(on);
            if (leftGun != null) leftGun.SetModelHidden(on);
        }

        /// <summary>
        /// 二丁拳銃にする / やめる。flyFrom を渡すと、左手の銃がその場所 (撃ったフィーバーの的) から手元へ飛んでくる (PullGun)
        /// </summary>
        public void SetDualWield(bool value, Vector3? flyFrom = null)
        {
            bool added = value && !dualWield; // (銃が増える時だけ、たぐり寄せる)
            dualWield = value;
            StopFlight(leftGun, leftFlight);
            if (leftGun != null) leftGun.gameObject.SetActive(value);
            if (added) PullGun(leftGun, leftFlight, flyFrom ?? DefaultPullFrom(), UnityEngine.XR.XRNode.LeftHand, 0f);
        }

        /// <summary>
        /// 武器を持ち替えさせた的の場所 (from) から、手元の銃をたぐり寄せる (武器の的・フィーバーの的・スナイパーなど、銃が増える / 変わる時の共通の演出)。
        /// 右手の銃と、二丁拳銃の時は左手の銃も (少し遅れて) 飛んでくる。手元に着くまで、その手では撃てない
        /// </summary>
        public void PullGunsFrom(Vector3 from)
        {
            PullGun(rightGun, rightFlight, from, UnityEngine.XR.XRNode.RightHand, 0f);
            if (dualWield && leftGun != null && leftGun.gameObject.activeInHierarchy)
                PullGun(leftGun, leftFlight, from, UnityEngine.XR.XRNode.LeftHand, 0.08f);
        }

        // 飛んでくる途中の銃の状態 (手ごとに1つ)
        class GunFlight
        {
            public Coroutine routine;
            public bool arriving;   // 飛んでくる途中 (その手ではまだ撃てない)
            public bool captured;   // 手の中での位置・向きを覚えたか
            public Vector3 homePos;
            public Quaternion homeRot;
        }
        readonly GunFlight rightFlight = new GunFlight(), leftFlight = new GunFlight();

        // たぐり寄せる場所を渡されなかった時: 目の前の少し先 (見ている方の 2.5m 先、目より少し下) から
        Vector3 DefaultPullFrom()
        {
            if (head == null) return transform.position + transform.forward * 2.5f + Vector3.up;
            var fwd = head.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = transform.forward;
            return head.transform.position + fwd.normalized * 2.5f + Vector3.down * 0.3f;
        }
        const float GunFlyTime = 0.45f; // (飛んでくる秒数)

        bool RightGunArriving => rightFlight.arriving;
        bool LeftGunArriving => leftFlight.arriving;

        void PullGun(GunController gun, GunFlight f, Vector3 from, UnityEngine.XR.XRNode node, float delay)
        {
            if (gun == null || !isActiveAndEnabled || !gun.gameObject.activeInHierarchy) return;
            StopFlight(gun, f);
            f.routine = StartCoroutine(Fly(gun, f, from, node, delay));
        }

        // 飛んでいる途中ならやめて、手の中の元の位置・向きに戻す
        void StopFlight(GunController gun, GunFlight f)
        {
            if (f.routine != null) StopCoroutine(f.routine);
            f.routine = null;
            f.arriving = false;
            if (gun != null) gun.LaserSuppressed = false;
            if (gun != null && f.captured)
            {
                gun.transform.localPosition = f.homePos;
                gun.transform.localRotation = f.homeRot;
            }
        }

        // 的の場所から手元へ: 少し山なりに飛びながら、くるくる回って (だんだんゆっくり) 手の中に収まる
        System.Collections.IEnumerator Fly(GunController gun, GunFlight f, Vector3 from, UnityEngine.XR.XRNode node, float delay)
        {
            var t = gun.transform;
            if (!f.captured)
            {
                f.homePos = t.localPosition;
                f.homeRot = t.localRotation;
                f.captured = true;
            }
            f.arriving = true;
            gun.LaserSuppressed = true; // (レーザーは手元に着いてから)
            for (float k = -delay / GunFlyTime; k < 1f; k += Time.deltaTime / GunFlyTime)
            {
                float u = Mathf.Clamp01(k);
                float e = 1f - (1f - u) * (1f - u) * (1f - u); // (速く飛び出して、手元でゆっくり)
                var parent = t.parent;
                Vector3 to = parent != null ? parent.TransformPoint(f.homePos) : f.homePos;
                Quaternion toRot = parent != null ? parent.rotation * f.homeRot : f.homeRot;
                t.position = Vector3.Lerp(from, to, e) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.35f);
                t.rotation = toRot * Quaternion.AngleAxis((1f - e) * 720f, Vector3.up) * Quaternion.AngleAxis((1f - e) * 180f, Vector3.forward);
                yield return null;
            }
            t.localPosition = f.homePos;
            t.localRotation = f.homeRot;
            f.arriving = false;
            gun.LaserSuppressed = false; // 手元に着いた: レーザーを出す
            f.routine = null;
            Sfx.Play("click"); // 手元に着いた: これで撃てる
            XRBridge.Haptic(node, 0.5f, 0.08f);
        }

        // A ボタン (リセット) / B ボタン (エンドレスモード) の長押し
        void UpdateReset()
        {
            // 長押しのボタンは1つずつ: 最初に押し始めたボタンを離すまで、ほかのボタンはたまらない (同時に押すと全部のゲージがたまっていたので)。
            // 同じフレームに押し始めた時は リセット → エンドレス → デバッグ の順に優先
            if (holdOwner != null && !holdOwner.IsPressed()) holdOwner = null;
            if (holdOwner == null)
            {
                if (resetButton.IsPressed()) holdOwner = resetButton;
                else if (endlessButton.IsPressed()) holdOwner = endlessButton;
                else if (debugButton.IsPressed()) holdOwner = debugButton;
            }
            if (UpdateHold(resetButton, holdOwner == resetButton, ref resetHeld, ref resetFired, resetHoldTime, resetIndicator)) ResetRequested?.Invoke();
            if (UpdateHold(endlessButton, holdOwner == endlessButton, ref endlessHeld, ref endlessFired, endlessHoldTime, endlessIndicator)) EndlessRequested?.Invoke();
            if (UpdateHold(debugButton, holdOwner == debugButton, ref debugHeld, ref debugFired, debugHoldTime, debugIndicator)) DebugRequested?.Invoke();
        }

        InputAction holdOwner; // 今たまっている長押しのボタン (離すまで、ほかの長押しのボタンは無効)

        // 押している間たまっていき、holdTime 秒で発動する (発動したフレームだけ true)。離すと最初から。active = false の間は押していないのと同じ
        static bool UpdateHold(InputAction button, bool active, ref float held, ref bool fired, float holdTime, ResetIndicator indicator)
        {
            bool holding = active && button.IsPressed();
            bool fire = false;
            if (!holding)
            {
                held = 0f;
                fired = false;
            }
            else if (!fired)
            {
                held += Time.deltaTime;
                if (held >= holdTime)
                {
                    fired = true;
                    fire = true;
                }
            }
            if (indicator != null) indicator.SetProgress(fired ? 1f : held / Mathf.Max(0.01f, holdTime), holding);
            return fire;
        }

        /// <summary>
        /// 視点の高さを、身長 referenceHeightCm の人の目の高さに合わせる (START を撃った時に1回だけ)。誰が遊んでも屋台との位置関係が同じになる。
        /// 今の頭の高さが「referenceHeightCm × eyeHeightRatio」になるように CameraOffset を上下させる (背の低い人は少し高い所から見ることになる)。
        /// 床の高さの取り方 (Floor / Device) に関係なく合う。VR でない時は何もしない。
        /// </summary>
        public void CalibrateHeight()
        {
            if (!XRBridge.IsActive || head == null || cameraOffset == null) return;
            float eye = referenceHeightCm / 100f * eyeHeightRatio;
            calibratedOffsetY = eye - head.transform.localPosition.y; // head の localPosition は CameraOffset から見た、実際に追跡している頭の高さ
            cameraOffset.localPosition = Vector3.up * calibratedOffsetY.Value;
            Debug.Log($"[PlayerRig] 身長 {referenceHeightCm}cm の人の視点に合わせました (目の高さ {eye:0.00}m, CameraOffset {calibratedOffsetY.Value:0.00}m)");
        }

        /// <summary>
        /// 起動してすぐ (VR になって頭の追跡が始まったら1回だけ)、身長 referenceHeightCm の人の視点に合わせる。
        /// これで、タイトルの間から START を撃った後まで、ずっと同じ高さから見える (START を撃った時にも合わせ直すが、同じ人なら高さはほとんど変わらない)
        /// </summary>
        void AutoCalibrate()
        {
            if (calibratedOffsetY.HasValue || xrStartedAt < 0f || Time.unscaledTime < xrStartedAt + 0.3f) return;
            if (head == null || head.transform.localPosition.sqrMagnitude < 0.0001f) return; // (頭の追跡がまだ始まっていない)
            CalibrateHeight();
        }

        // ---- XR / デスクトップの切り替え ----

        void ApplyMode(bool xr)
        {
            xrModeApplied = xr;
            UsingVRControls = xr; // VR に入ったらコントローラー、出たらマウスとキーボードから
            foreach (var tpd in GetComponentsInChildren<TrackedPoseDriver>(true))
                tpd.enabled = xr;

            if (xr)
            {
                handCheckAt = Time.unscaledTime + 3f;
                xrStartedAt = Time.unscaledTime;
                bool floor = XRBridge.TrySetFloorOrigin();
                floorOrigin = floor;
                cameraOffset.localPosition = Vector3.up * (calibratedOffsetY ?? (floor ? 0f : standingEyeHeight) + xrHeightBoost);
            }
            else
            {
                cameraOffset.localPosition = Vector3.zero;
                head.transform.localPosition = Vector3.up * standingEyeHeight;
                head.transform.localRotation = Quaternion.identity;
                yaw = pitch = 0f;
            }
        }

        // ---- 手の追跡の予備 ----
        // 手 (銃) はふつう TrackedPoseDriver が Input System のコントローラー (<XRController>{RightHand}/pointerPosition) から動かす。
        // そのコントローラーが Input System に見つからない時は手が止まったままになるので、XR の標準の API (UnityEngine.XR.InputDevices) から直接読んで動かす

        static readonly UnityEngine.XR.InputFeatureUsage<Vector3> PointerPosition = new UnityEngine.XR.InputFeatureUsage<Vector3>("PointerPosition");
        static readonly UnityEngine.XR.InputFeatureUsage<Quaternion> PointerRotation = new UnityEngine.XR.InputFeatureUsage<Quaternion>("PointerRotation");

        void OnBeforeRender()
        {
            if (xrModeApplied == true) UpdateHandsFallback();
        }

        void UpdateHandsFallback()
        {
            bool rightOk = HandFallback(rightHand, rightPose, UnityEngine.XR.XRNode.RightHand);
            bool leftOk = HandFallback(leftHand, leftPose, UnityEngine.XR.XRNode.LeftHand);

            if (handCheckAt > 0f && Time.unscaledTime >= handCheckAt)
            {
                handCheckAt = -1f;
                LogHandTracking(rightOk, leftOk);
            }
        }

        /// <summary>Input System で手が追えていれば何もしない。追えていなければ XR の標準の API から読んで置く。手が動いていれば true</summary>
        static bool HandFallback(Transform hand, TrackedPoseDriver tpd, UnityEngine.XR.XRNode node)
        {
            if (hand == null) return false;
            var action = tpd != null && tpd.enabled ? tpd.positionInput.action : null;
            if (action != null && action.controls.Count > 0) return true;

            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return false;
            if (!device.TryGetFeatureValue(PointerPosition, out var pos) && !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out pos)) return false;
            if (!device.TryGetFeatureValue(PointerRotation, out var rot) && !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rot)) return false;
            hand.localPosition = pos;
            hand.localRotation = rot;
            return true;
        }

        // VR になって少したってから、手がどの方法で追えているかをコンソールに出す (手が動かない時の原因調べ用)
        void LogHandTracking(bool rightOk, bool leftOk)
        {
            var sb = new System.Text.StringBuilder("[PlayerRig] 手の追跡: ");
            sb.Append($"右 {Describe(rightPose, rightOk)} / 左 {Describe(leftPose, leftOk)}\n");
            sb.Append("Input System の XR 機器: ");
            foreach (var d in InputSystem.devices)
                if (d is TrackedDevice) sb.Append($"[{d.layout} \"{d.displayName}\" {string.Join(",", d.usages)}] ");
            var xrDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevices(xrDevices);
            sb.Append("\nXR の機器: ");
            foreach (var d in xrDevices) sb.Append($"[{d.name} {d.characteristics}] ");
            if (rightOk && leftOk) Debug.Log(sb.ToString());
            else Debug.LogWarning(sb.ToString());
        }

        static string Describe(TrackedPoseDriver tpd, bool ok)
        {
            var action = tpd != null ? tpd.positionInput.action : null;
            if (action != null && action.controls.Count > 0) return $"Input System ({action.controls[0].path})";
            return ok ? "予備 (XR の標準の API)" : "追えていない";
        }

        void UpdateDesktop()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
                head.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // WASD キーでも視点を回す (右ドラッグと同時に使ってもよい)
            var kb = Keyboard.current;
            if (kb != null)
            {
                float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                if (h != 0f || v != 0f)
                {
                    float step = keyLookSpeed * Time.deltaTime;
                    yaw += h * step;
                    pitch = Mathf.Clamp(pitch - v * step, -80f, 80f);
                    head.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                }
            }

            // 銃はマウスカーソルの先を狙う
            Ray ray = head.ScreenPointToRay(mouse.position.ReadValue());
            Vector3 aimPoint = Physics.Raycast(ray, out var hit, 100f) ? hit.point : ray.GetPoint(30f);

            PlaceDesktopHand(rightHand, new Vector3(0.22f, -0.22f, 0.35f), aimPoint);
            // 左の銃はカーソルの少し左を狙う (2丁が同じ点に重ならないように。距離に比べて約 2°)
            Vector3 leftAim = aimPoint - head.transform.right * (Vector3.Distance(head.transform.position, aimPoint) * desktopLeftAimOffset);
            PlaceDesktopHand(leftHand, new Vector3(-0.22f, -0.22f, 0.35f), leftAim);
        }

        void PlaceDesktopHand(Transform hand, Vector3 localOffset, Vector3 aimPoint)
        {
            if (hand == null) return;
            hand.position = head.transform.TransformPoint(localOffset);
            hand.rotation = Quaternion.LookRotation(aimPoint - hand.position, head.transform.up);
        }
    }
}
