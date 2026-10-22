using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace ShootingGallery
{
    /// <summary>
    /// プレイヤー。中心から移動せず、見回して撃つだけ。
    /// 右手トリガーで右の銃を撃つ。フィーバー中の左手の銃は左手トリガーで撃つ。
    /// 右手の A ボタンを長押しすると (インジケーターがたまりきると) 最初に戻る。B ボタンの長押しでエンドレスモード、左手の X ボタンの長押しでデバッグモードが始まる。
    /// VRデバイスが無い時はマウスでテストできる (右ドラッグか WASD で視点, 左クリック/Spaceで右の銃, Fキーで左の銃, 1 キー長押しでデバッグ, 2 キー長押しでエンドレス, 3 キー長押しでリセット。VR のボタンと同じく長押しで発動)。
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public Transform cameraOffset;
        public Camera head;
        public Transform rightHand;
        public Transform leftHand;
        public GunController rightGun;
        public GunController leftGun;

        public float standingEyeHeight = 1.6f;
        [Tooltip("VRの時に視点をこの分 (m) だけ実際より高くする (START を撃つまでの間だけ。撃った後は referenceHeightCm の人の視点に合わせる)")]
        public float xrHeightBoost = 0.85f;
        [Tooltip("身長に対する目の高さの割合 (目の高さ = 身長 × この値)")]
        public float eyeHeightRatio = 0.935f;
        [Tooltip("視点を合わせる身長 (cm)。誰が遊んでも、この身長の人と同じ高さから屋台が見える")]
        public int referenceHeightCm = 180;
        public float mouseSensitivity = 0.15f;
        [Tooltip("PC: WASD キーで視点を回す速さ (度/秒)。A / D で左右、W / S で上下")]
        public float keyLookSpeed = 90f;

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
            leftTrigger.AddBinding("<XRController>{LeftHand}/trigger");
            leftTrigger.AddBinding("<Keyboard>/f");

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
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
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

        void Update()
        {
            bool xr = XRBridge.IsActive;
            if (xrModeApplied != xr) ApplyMode(xr);
            if (!xr) UpdateDesktop();
            else UpdateHandsFallback();

            UpdateReset();

            // 射撃
            bool rPressed = rightTrigger.WasPressedThisFrame();
            bool rHeld = rightTrigger.IsPressed();
            rightGun.TriggerInput(rPressed, rHeld);

            if (dualWield && leftGun != null)
            {
                leftGun.TriggerInput(leftTrigger.WasPressedThisFrame(), leftTrigger.IsPressed());
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
            rightGun.SetInfo(rightGun.IsCharging ? "CHARGING..." : rightGun.NeedsReload ? $"RELOAD {rightGun.Loaded}/{def.magazine} : PULL TRIGGER" :rightGun.IsReloading ? (weapon == WeaponType.Poi ? "NEXT POI..." : "RELOADING...") : def.displayName); // 残りの弾は銃の上の弾の列で見せる
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
        public void GiveStageWeapon(WeaponType type, float duration)
        {
            GiveWeapon(type, duration);
            stageWeapon = true;
            stageGaugeFresh = true;
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

        public void GiveWeapon(WeaponType type, float duration)
        {
            stageWeapon = false;
            weapon = type;
            weaponTimeLeft = duration;
            weaponDuration = duration;
            rightGun.SetWeapon(type);
            if (leftGun != null) leftGun.SetWeapon(type);
        }

        public void ResetWeapon()
        {
            weapon = WeaponType.Handgun;
            weaponTimeLeft = 0f;
            stageWeapon = false;
            rightGun.SetWeapon(weapon);
            if (leftGun != null) leftGun.SetWeapon(weapon);
        }

        public void SetDualWield(bool value)
        {
            dualWield = value;
            if (leftGun != null) leftGun.gameObject.SetActive(value);
        }

        // A ボタン (リセット) / B ボタン (エンドレスモード) の長押し
        void UpdateReset()
        {
            if (UpdateHold(resetButton, ref resetHeld, ref resetFired, resetHoldTime, resetIndicator)) ResetRequested?.Invoke();
            if (UpdateHold(endlessButton, ref endlessHeld, ref endlessFired, endlessHoldTime, endlessIndicator)) EndlessRequested?.Invoke();
            if (UpdateHold(debugButton, ref debugHeld, ref debugFired, debugHoldTime, debugIndicator)) DebugRequested?.Invoke();
        }

        // 押している間たまっていき、holdTime 秒で発動する (発動したフレームだけ true)。離すと最初から
        static bool UpdateHold(InputAction button, ref float held, ref bool fired, float holdTime, ResetIndicator indicator)
        {
            bool holding = button.IsPressed();
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

        // ---- XR / デスクトップの切り替え ----

        void ApplyMode(bool xr)
        {
            xrModeApplied = xr;
            foreach (var tpd in GetComponentsInChildren<TrackedPoseDriver>(true))
                tpd.enabled = xr;

            if (xr)
            {
                handCheckAt = Time.unscaledTime + 3f;
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
            PlaceDesktopHand(leftHand, new Vector3(-0.22f, -0.22f, 0.35f), aimPoint);
        }

        void PlaceDesktopHand(Transform hand, Vector3 localOffset, Vector3 aimPoint)
        {
            if (hand == null) return;
            hand.position = head.transform.TransformPoint(localOffset);
            hand.rotation = Quaternion.LookRotation(aimPoint - hand.position, head.transform.up);
        }
    }
}
