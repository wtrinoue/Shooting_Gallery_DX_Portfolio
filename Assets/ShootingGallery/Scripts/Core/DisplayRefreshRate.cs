#if UNITY_ANDROID
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Features.Meta;

namespace ShootingGallery
{
    /// <summary>
    /// Quest の画面の速さ (リフレッシュレート) を 90Hz にし、視界の端だけ粗く描く (固定フォービエイテッドレンダリング) ようにする。Quest は、アプリが頼まないと 72Hz で動く。
    /// 起動したら自分で作られ、VR が始まるのを待ってから、ヘッドセットが選べる速さの中で wanted (なければそれ以下でいちばん近い速さ) を頼む。
    /// 使うには Project Settings → XR Plug-in Management → OpenXR (Android) で「Meta Quest: Display Utilities」をオンにしておく
    /// (メニュー: ShootingGallery → Quest (Android) の VR 設定をする)。オフだと何もしない (72Hz のまま)。
    /// Android (Quest) のビルドの時だけ入る (PC・WebGL では使わない)
    /// </summary>
    public class DisplayRefreshRate : MonoBehaviour
    {
        const float Wanted = 90f;
        // 視界の端を粗く描く強さ (0 = しない 〜 1 = いちばん強い)。強いほど GPU が軽くなるが、視界の端のぼやけ・ちらつきが目立つ
        const float Foveation = 0.75f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Application.isEditor) return; // (エディタでは Quest Link の PC 側の設定で動くので頼まない)
            var go = new GameObject("DisplayRefreshRate");
            DontDestroyOnLoad(go);
            go.AddComponent<DisplayRefreshRate>();
        }

        IEnumerator Start()
        {
            // VR が始まって、画面の仕組み (XRDisplaySubsystem) が使えるようになるまで待つ (最大 10 秒)
            XRDisplaySubsystem display = null;
            for (float t = 0f; t < 10f && display == null; t += Time.unscaledDeltaTime)
            {
                var loader = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null ? XRGeneralSettings.Instance.Manager.activeLoader : null;
                if (loader != null) display = loader.GetLoadedSubsystem<XRDisplaySubsystem>();
                if (display == null) yield return null;
            }
            if (display == null) { Destroy(gameObject); yield break; }

            // 視界の端だけ粗く描く (固定フォービエイテッドレンダリング。OpenXR の Foveated Rendering がオンの時だけ効く。目の動きは使わない)
            display.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;
            display.foveatedRenderingLevel = Foveation;

            if (display.TryGetSupportedDisplayRefreshRates(Allocator.Temp, out var rates))
            {
                float best = 0f;
                foreach (var r in rates)
                    if (r <= Wanted + 0.5f && r > best) best = r; // 90 があれば 90。なければ 90 以下でいちばん速い速さ
                if (best > 0f)
                {
                    bool ok = display.TryRequestDisplayRefreshRate(best);
                    Debug.Log($"[DisplayRefreshRate] {best}Hz を頼みました ({(ok ? "受け付けられた" : "受け付けられなかった")})");
                }
            }
            else Debug.Log("[DisplayRefreshRate] 選べる速さが分かりません (OpenXR の Meta Quest: Display Utilities がオフかもしれません)");
            Destroy(gameObject);
        }
    }
}
#endif
