using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace ShootingGallery
{
    /// <summary>
    /// Meta Quest (Android の APK) で VR として起動するための設定を、メニューから一度にする。
    ///   メニュー: ShootingGallery → Quest (Android) の VR 設定をする
    /// してくれること:
    ///   1. XR Plug-in Management: Android で OpenXR を使い、起動時に VR を始める (これがないと、ただの平面のアプリとして起動する)
    ///   2. OpenXR (Android): Meta Quest のサポートと、Quest のコントローラー (Touch / Touch Plus / Touch Pro) の入力をオンにする
    ///      (オフだと、トリガーや A / B ボタンが読めない)
    ///   3. Player: 最小 API レベル 32・IL2CPP・ARM64・テクスチャ圧縮 ASTC (Quest で動かすのに必要 / 推奨の値)
    ///   4. OpenXR (Android): Meta Quest: Display Utilities (90Hz を頼むのに使う。com.unity.xr.meta-openxr が入っている時)
    ///   5. OpenXR (PC): Quest のコントローラーの入力 (エディタで Quest Link を使って試す時)
    /// WebGL の設定は変えない
    /// </summary>
    static class QuestSetup
    {
        const BuildTargetGroup Group = BuildTargetGroup.Android;

        [MenuItem("ShootingGallery/Quest (Android) の VR 設定をする")]
        static void Setup()
        {
            var log = new System.Text.StringBuilder();

            // 1. XR Plug-in Management
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                EditorUtility.DisplayDialog("Quest の VR 設定", "XR Plug-in Management の設定が見つかりません。\nProject Settings → XR Plug-in Management を一度開いてから、もう一度実行してください。", "OK");
                return;
            }
            if (!perTarget.HasSettingsForBuildTarget(Group)) perTarget.CreateDefaultSettingsForBuildTarget(Group);
            if (!perTarget.HasManagerSettingsForBuildTarget(Group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(Group);
            var general = perTarget.SettingsForBuildTarget(Group);
            general.InitManagerOnStart = true;
            var manager = perTarget.ManagerSettingsForBuildTarget(Group);
            bool hasOpenXR = false;
            foreach (var l in manager.activeLoaders) if (l != null && l.GetType().FullName == "UnityEngine.XR.OpenXR.OpenXRLoader") hasOpenXR = true;
            if (!hasOpenXR && !XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", Group))
                log.AppendLine("× OpenXR を Android に割り当てられませんでした (Project Settings → XR Plug-in Management → Android タブで OpenXR にチェックしてください)");
            else
                log.AppendLine("○ Android で OpenXR を使う (起動時に VR を始める)");
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(perTarget);

            // 2. OpenXR の機能 (Android)
            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(Group);
            if (openxr == null)
                log.AppendLine("× OpenXR の Android の設定が見つかりません (もう一度実行してみてください)");
            else
            {
                Enable<MetaQuestFeature>(openxr, "Meta Quest Support", log);
                Enable<OculusTouchControllerProfile>(openxr, "Oculus Touch Controller Profile", log);
                Enable<MetaQuestTouchPlusControllerProfile>(openxr, "Meta Quest Touch Plus Controller Profile", log);
                Enable<MetaQuestTouchProControllerProfile>(openxr, "Meta Quest Touch Pro Controller Profile", log);
                EnableByName(openxr, "DisplayUtilitiesFeature", "Meta Quest: Display Utilities (90Hz にする)", log); // (com.unity.xr.meta-openxr。なければ飛ばす)
                EnableFoveation(openxr, log); // 視界の端だけ粗く描く (GPU を軽くする。目の動きは使わない)
                EditorUtility.SetDirty(openxr);
            }

            // 2b. OpenXR (PC。エディタの再生ボタンで Quest Link を使って試す時): コントローラーの入力をオンにする
            var pc = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if (pc != null)
            {
                Enable<OculusTouchControllerProfile>(pc, "PC: Oculus Touch Controller Profile", log);
                Enable<MetaQuestTouchPlusControllerProfile>(pc, "PC: Meta Quest Touch Plus Controller Profile", log);
                EditorUtility.SetDirty(pc);
            }

            // 3. Player (Android)
            if ((int)PlayerSettings.Android.minSdkVersion < 32) PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            log.AppendLine("○ 最小 API レベル 32 / IL2CPP / ARM64 / ASTC");

            AssetDatabase.SaveAssets();
            Debug.Log("[QuestSetup]\n" + log);
            EditorUtility.DisplayDialog("Quest の VR 設定", log + "\nAPK を作り直して、Quest に入れ直してください。", "OK");
        }

        // 固定フォービエイテッドレンダリング (視界の端だけ粗く描く) をオンにする。URP 用のやり方 (SRP Foveation) を使い、
        // 目の動き (アイトラッキング) は使わない (使うと権限の確認が出る。Quest 3 には目の動きを読む仕組みもない)
        static void EnableFoveation(OpenXRSettings settings, System.Text.StringBuilder log)
        {
            foreach (var f in settings.GetFeatures())
            {
                if (f == null || f.GetType().Name != "FoveatedRenderingFeature") continue;
                f.enabled = true;
                var so = new SerializedObject(f);
                var eye = so.FindProperty("useEyeTracking");
                if (eye != null) { eye.boolValue = false; so.ApplyModifiedProperties(); }
                EditorUtility.SetDirty(f);
                settings.foveatedRenderingApi = OpenXRSettings.BackendFovationApi.SRPFoveation;
                log.AppendLine("○ Foveated Rendering (視界の端だけ粗く描く。目の動きは使わない)");
                return;
            }
            log.AppendLine("× Foveated Rendering が見つかりません");
        }

        // 型を直接使わずに、名前で探してオンにする (入っていないパッケージの機能でも、ビルドが通るように)
        static void EnableByName(OpenXRSettings settings, string typeName, string label, System.Text.StringBuilder log)
        {
            foreach (var f in settings.GetFeatures())
            {
                if (f == null || f.GetType().Name != typeName) continue;
                f.enabled = true;
                EditorUtility.SetDirty(f);
                log.AppendLine($"○ {label}");
                return;
            }
            log.AppendLine($"× {label} が見つかりません (Package Manager で com.unity.xr.meta-openxr を入れてください)");
        }

        static void Enable<T>(OpenXRSettings settings, string label, System.Text.StringBuilder log) where T : OpenXRFeature
        {
            var f = settings.GetFeature<T>();
            if (f == null) { log.AppendLine($"× {label} が見つかりません"); return; }
            f.enabled = true;
            EditorUtility.SetDirty(f);
            log.AppendLine($"○ {label}");
        }
    }
}
