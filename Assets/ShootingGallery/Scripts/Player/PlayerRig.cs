using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace ShootingGallery
{
    /// <summary>
    /// プレイヤー。中心から移動せず、見回して撃つだけ。
    /// 右手トリガーで右の銃を撃つ。フィーバー中の左手の銃は左手トリガーで撃つ。
    /// 右手の A ボタンを長押しすると (インジケーターがたまりきると) 最初に戻る。B ボタンの長押しでエンドレスモード、左手の X ボタンの長押しでデバッグモードが始まる。
    /// VRデバイスが無い時はマウスでテストできる (右ドラッグで視点, 左クリック/Spaceで右の銃, Fキーで左の銃, 1 キー長押しでデバッグ, 2 キー長押しでエンドレス, 3 キー長押しでリセット。VR のボタンと同じく長押しで発動)。
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
        bool dualWield;

        bool? xrModeApplied;
        float yaw, pitch;

        public bool DualWield => dualWield;
        public WeaponType Weapon => weapon;

        void Awake()
        {
            rightTrigger = new InputAction("RightTrigger", InputActionType.Button);
            rightTrigger.AddBinding("<XRController>{RightHand}/trigger");
            rightTrigger.AddBinding("<Mouse>/leftButton");
            rightTrigger.AddBinding("<Keyboard>/space");

            leftTrigger = new InputAction("LeftTrigger", InputActionType.Button);
            leftTrigger.AddBinding("<XRController>{LeftHand}/trigger");
            leftTrigger.AddBinding("<Keyboard>/f");

            resetButton = new InputAction("Reset", InputActionType.Button);
            resetButton.AddBinding("<XRController>{RightHand}/primaryButton"); // Quest の A ボタン
            resetButton.AddBinding("<Keyboard>/3");       // PC: 3 キー (テンキーも可)
            resetButton.AddBinding("<Keyboard>/numpad3");

            endlessButton = new InputAction("Endless", InputActionType.Button);
            endlessButton.AddBinding("<XRController>{RightHand}/secondaryButton"); // Quest の B ボタン
            endlessButton.AddBinding("<Keyboard>/2");     // PC: 2 キー
            endlessButton.AddBinding("<Keyboard>/numpad2");

            debugButton = new InputAction("Debug", InputActionType.Button);
            debugButton.AddBinding("<XRController>{LeftHand}/primaryButton"); // Quest の X ボタン
            debugButton.AddBinding("<Keyboard>/1");       // PC: 1 キー
            debugButton.AddBinding("<Keyboard>/numpad1");
        }

        void OnEnable()
        {
            rightTrigger.Enable();
            leftTrigger.Enable();
            resetButton.Enable();
            endlessButton.Enable();
            debugButton.Enable();
        }

        void OnDisable()
        {
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
            bool xr = XRSettings.isDeviceActive;
            if (xrModeApplied != xr) ApplyMode(xr);
            if (!xr) UpdateDesktop();

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
                if (weaponTimeLeft <= 0f) ResetWeapon();
            }
            var def = Weapons.Get(weapon);
            rightGun.SetInfo(rightGun.IsCharging ? "CHARGING..." : rightGun.IsReloading ? "RELOADING..." : def.displayName); // 残りの弾は銃の上の弾の列で見せる
            if (leftGun != null) leftGun.SetInfo("");

            // 残り時間はゲージで表示 (ハンドガンの時は非表示)
            float ratio = weapon == WeaponType.Handgun || weaponDuration <= 0f ? 0f : weaponTimeLeft / weaponDuration;
            Color gaugeColor = ratio < 0.3f ? new Color(1f, 0.25f, 0.2f) : GaugeColor(weapon);
            rightGun.SetGauge(ratio, gaugeColor);
            if (leftGun != null) leftGun.SetGauge(ratio, gaugeColor);
        }

        static Color GaugeColor(WeaponType type) => type switch
        {
            WeaponType.MachineGun => new Color(0.3f, 0.6f, 1f),
            WeaponType.Shotgun => new Color(0.3f, 0.95f, 0.4f),
            WeaponType.Bomb => new Color(1f, 0.6f, 0.15f),
            WeaponType.Sniper => new Color(1f, 0.2f, 0.25f),
            WeaponType.Bazooka => new Color(0.4f, 0.8f, 1f),
            WeaponType.Shuriken => new Color(0.85f, 0.85f, 0.95f),
            WeaponType.FireBall => new Color(1f, 0.5f, 0.1f),
            WeaponType.Dart => new Color(1f, 0.3f, 0.5f),
            WeaponType.Revolver => new Color(0.9f, 0.7f, 0.4f),
            WeaponType.Flashlight => new Color(1f, 0.92f, 0.55f),
            WeaponType.Missile => new Color(0.55f, 0.75f, 1f),
            _ => Color.white
        };

        public void GiveWeapon(WeaponType type, float duration)
        {
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
            if (!XRSettings.isDeviceActive || head == null || cameraOffset == null) return;
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
                bool floor = TrySetFloorOrigin();
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

        static bool TrySetFloorOrigin()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            bool ok = false;
            foreach (var s in subsystems)
            {
                if ((s.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0)
                    ok |= s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
            return ok;
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
