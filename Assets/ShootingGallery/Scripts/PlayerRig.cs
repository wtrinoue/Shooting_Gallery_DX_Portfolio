using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace ShootingGallery
{
    /// <summary>
    /// プレイヤー。中心から移動せず、見回して撃つだけ。
    /// 操作は右手トリガーのみ (フィーバー中の左手の銃も右トリガーで同時に撃つ)。
    /// VRデバイスが無い時はマウスでテストできる (右ドラッグで視点, 左クリックで発射)。
    /// </summary>
    public class PlayerRig : MonoBehaviour
    {
        public Transform cameraOffset;
        public Camera head;
        public Transform rightHand;
        public Transform leftHand;
        public GunController rightGun;
        public GunController leftGun;

        [Tooltip("ONなら左手の銃も右トリガーで撃つ。OFFなら左トリガーで撃つ")]
        public bool leftGunUsesRightTrigger = true;
        public float standingEyeHeight = 1.6f;
        public float mouseSensitivity = 0.15f;

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
        }

        void OnEnable()
        {
            rightTrigger.Enable();
            leftTrigger.Enable();
        }

        void OnDisable()
        {
            rightTrigger.Disable();
            leftTrigger.Disable();
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

            // 射撃
            bool rPressed = rightTrigger.WasPressedThisFrame();
            bool rHeld = rightTrigger.IsPressed();
            rightGun.TriggerInput(rPressed, rHeld);

            if (dualWield && leftGun != null)
            {
                if (leftGunUsesRightTrigger) leftGun.TriggerInput(rPressed, rHeld);
                else leftGun.TriggerInput(leftTrigger.WasPressedThisFrame(), leftTrigger.IsPressed());
            }

            // 特殊武器の残り時間
            if (weapon != WeaponType.Handgun)
            {
                weaponTimeLeft -= Time.deltaTime;
                if (weaponTimeLeft <= 0f) ResetWeapon();
            }
            rightGun.SetInfo(Weapons.Get(weapon).displayName);
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

        // ---- XR / デスクトップの切り替え ----

        void ApplyMode(bool xr)
        {
            xrModeApplied = xr;
            foreach (var tpd in GetComponentsInChildren<TrackedPoseDriver>(true))
                tpd.enabled = xr;

            if (xr)
            {
                bool floor = TrySetFloorOrigin();
                cameraOffset.localPosition = floor ? Vector3.zero : Vector3.up * standingEyeHeight;
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
