using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShootingGallery.EditorTools
{
    /// <summary>
    /// 実機で実行時に世界を組み立てる時に使うアセットの一覧 (Assets/Resources/GalleryRuntimeAssets.asset) の中身をそろえる。
    /// エディターを開いた時・ビルドの直前 (APK など)・メニューから。
    ///   BGM・拍の表 (GalleryBuilder の *BgmPath) / 専用のシェーダー (Assets/ShootingGallery/Shaders と、実行時に Shader.Find で探す URP のシェーダー) /
    ///   ビルダーが作った材質 (Assets/ShootingGallery/Generated/Materials。「シーンに生成」で保存される。描き方の組み合わせごとビルドに入るように)
    /// </summary>
    [InitializeOnLoad]
    public class GalleryRuntimeAssetsSync : IPreprocessBuildWithReport
    {
        const string AssetPath = "Assets/Resources/" + GalleryRuntimeAssets.ResourceName + ".asset";
        const string ShaderDir = "Assets/ShootingGallery/Shaders";
        const string MaterialDir = "Assets/ShootingGallery/Generated/Materials";
        // 実行時に Shader.Find で探す、パッケージのシェーダー (どの材質からも参照されていないと、ビルドから省かれるので)
        static readonly string[] ExtraShaders =
        {
            "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit",
        };

        // エディターを開いた時は、一覧のファイルがまだない時だけ作る (毎回 1000 個以上の材質を読み直さないように)。ビルドの直前には毎回そろえる
        static GalleryRuntimeAssetsSync() => EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GalleryRuntimeAssets>(AssetPath) == null) Sync(false);
        };

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Sync(true);

        [MenuItem("ShootingGallery/実機用のアセット一覧をそろえる (GalleryRuntimeAssets)")]
        static void SyncFromMenu() => Sync(true);

        static void Sync(bool log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var assets = AssetDatabase.LoadAssetAtPath<GalleryRuntimeAssets>(AssetPath);
            bool created = assets == null;
            if (created)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                assets = ScriptableObject.CreateInstance<GalleryRuntimeAssets>();
                AssetDatabase.CreateAsset(assets, AssetPath);
            }

            assets.casinoBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.CasinoBgmPath);
            assets.museumBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.MuseumBgmPath);
            assets.concertBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.ConcertBgmPath);
            assets.skyBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.SkyBgmPath);
            assets.festivalBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.FestivalBgmPath);
            assets.ghostBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.GhostBgmPath);
            assets.kaijuBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.KaijuBgmPath);
            assets.mineBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.MineBgmPath);
            assets.navalBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.NavalBgmPath);
            assets.westernBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.WesternBgmPath);
            assets.spaceBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.SpaceBgmPath);
            assets.submarineBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.SubmarineBgmPath);
            assets.ninjaBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.NinjaBgmPath);
            assets.unknownBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.UnknownBgmPath);
            assets.revolverShot = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.RevolverShotPath);
            assets.revolverReload = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.RevolverReloadPath);
            assets.roundBgm = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.RoundBgmPath);
            assets.finaleApplause = AssetDatabase.LoadAssetAtPath<AudioClip>(GalleryBuilder.FinaleApplausePath);
            assets.concertBeatMap = AssetDatabase.LoadAssetAtPath<TextAsset>(GalleryBuilder.ConcertBeatMapPath);
            assets.pocochiImage = AssetDatabase.LoadAssetAtPath<Texture2D>(GalleryBuilder.PocochiImagePath);

            var shaders = new List<Shader>();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { ShaderDir }))
            {
                var s = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null) shaders.Add(s);
            }
            foreach (var name in ExtraShaders)
            {
                var s = Shader.Find(name);
                if (s != null && !shaders.Contains(s)) shaders.Add(s);
            }
            assets.shaders = shaders.ToArray();

            var materials = new List<Material>();
            if (AssetDatabase.IsValidFolder(MaterialDir))
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialDir }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    if (m != null) materials.Add(m);
                }
            assets.materials = materials.ToArray();

            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            if (log || created)
                Debug.Log($"[GalleryRuntimeAssets] 実機用のアセット一覧をそろえました (BGM {Count(assets.casinoBgm, assets.museumBgm, assets.concertBgm, assets.skyBgm, assets.festivalBgm, assets.ghostBgm, assets.kaijuBgm, assets.mineBgm, assets.navalBgm, assets.westernBgm, assets.spaceBgm, assets.submarineBgm, assets.ninjaBgm, assets.roundBgm, assets.unknownBgm, assets.finaleApplause)}/16, " +
                          $"拍の表 {(assets.concertBeatMap != null ? "あり" : "なし")}, シェーダー {shaders.Count}, 材質 {materials.Count})" +
                          (materials.Count == 0 ? "\n材質がありません。一度「シーンに生成」を押して、材質を作ってからビルドしてください" : ""));
        }

        static int Count(params Object[] objs)
        {
            int n = 0;
            foreach (var o in objs) if (o != null) n++;
            return n;
        }
    }
}
