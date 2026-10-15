using UnityEngine;
using UnityEngine.XR;
#if SG_WEBXR && UNITY_WEBGL && !UNITY_EDITOR
using WebXR;
#endif

namespace ShootingGallery
{
    /// <summary>
    /// OpenXR (PC / Quest) と WebXR (ブラウザ / unityroom) の違いを吸収する。
    /// WebXR の時は WebXR Export (com.de-panther.webxr) を使う。ブラウザでは「Enter VR」を押すまで VR にならないので、
    /// VR 中かどうかは XRSettings ではなく WebXR のセッションの状態で見る。
    /// </summary>
    public static class XRBridge
    {
#if SG_WEBXR && UNITY_WEBGL && !UNITY_EDITOR
        static WebXRManager Manager => WebXRManager.Instance;

        /// <summary>VR で遊んでいるか (ブラウザでは VR のセッションに入っている間だけ)</summary>
        public static bool IsActive => Manager != null && Manager.XRState == WebXRState.VR;

        /// <summary>
        /// 頭の高さが床から測られているか。WebXR は local-floor の参照空間で始める (WebXR Export の既定) ので、いつも床が基準。
        /// </summary>
        public static bool TrySetFloorOrigin() => true;

        /// <summary>この端末 / ブラウザで VR に入れて、まだ入っていないか</summary>
        public static bool CanEnterVR => Manager != null && Manager.isSupportedVR && Manager.XRState == WebXRState.NORMAL;

        /// <summary>VR に入る。ブラウザの決まりで、クリックやタップなどの直後にしか呼べない</summary>
        public static void EnterVR() => Manager?.ToggleVR();

        public static void Haptic(XRNode node, float amplitude, float seconds)
        {
            if (Manager == null || Manager.XRState != WebXRState.VR) return;
            var hand = node == XRNode.LeftHand ? WebXRControllerHand.LEFT : WebXRControllerHand.RIGHT;
            Manager.HapticPulse(hand, amplitude, seconds * 1000f); // WebXR はミリ秒
        }

        /// <summary>
        /// WebXR (Unity 6 + URP) では HDR がオンだったり Render Scale が 1 でなかったりすると、左目が真っ暗になる (WebXR Export の既知の問題)。
        /// Mobile_RPAsset は Android (Quest 単体) でも使うのでアセットは変えずに、WebGL で動く時だけここで直す (実行中の変更なので保存はされない)。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void FixRenderSettings()
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
            {
                urp.supportsHDR = false;
                urp.renderScale = 1f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SetupScene()
        {
            foreach (var cam in Camera.allCameras) cam.allowHDR = false;

            var go = new GameObject("WebXREnterButton");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<WebXREnterButton>();
        }
#else
        public static bool IsActive => XRSettings.isDeviceActive;
        public static bool CanEnterVR => false;
        public static void EnterVR() { }

        public static bool TrySetFloorOrigin()
        {
            var subsystems = new System.Collections.Generic.List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            bool ok = false;
            foreach (var s in subsystems)
            {
                if ((s.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0)
                    ok |= s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
            return ok;
        }

        public static void Haptic(XRNode node, float amplitude, float seconds)
        {
            InputDevices.GetDeviceAtXRNode(node).SendHapticImpulse(0, amplitude, seconds);
        }
#endif
    }

    /// <summary>
    /// ブラウザ (unityroom など) で、VR に入れる時だけ画面の右下に出る「Enter VR」ボタン。
    /// unityroom はゲーム画面の外にボタンを置けないので、ゲームの中に出す。
    /// </summary>
    public class WebXREnterButton : MonoBehaviour
    {
        GUIStyle style;

        void OnGUI()
        {
            if (!XRBridge.CanEnterVR) return;

            float s = Screen.height / 540f;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            }
            style.fontSize = Mathf.RoundToInt(22 * s);

            float w = 180f * s, h = 56f * s, m = 16f * s;
            if (GUI.Button(new Rect(Screen.width - w - m, Screen.height - h - m, w, h), "Enter VR", style))
                XRBridge.EnterVR();
        }
    }
}
