using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 実機 (APK など) で、実行時に世界を組み立てる時に使うアセットの一覧 (Assets/Resources/GalleryRuntimeAssets.asset)。
    /// シーンに世界を保存していない時、ビルダーが実行時に組み立てる。その時、エディター専用の読み込み (AssetDatabase) は使えず、
    /// どの材質からも参照されていないシェーダー (と、その描き方の組み合わせ) はビルドから省かれてしまう。
    /// ここから参照しておくと Resources なので必ずビルドに入る:
    ///   BGM・拍の表 (ビルダーが実機ではここから読む) / 専用のシェーダー / ビルダーが作った材質 (アルファで切り抜く絵の材質など、描き方の組み合わせごと入るように)。
    /// 中身はエディターが自動でそろえる (Editor/GalleryRuntimeAssetsSync.cs: エディターを開いた時・ビルドの直前・メニュー)
    /// </summary>
    public class GalleryRuntimeAssets : ScriptableObject
    {
        public const string ResourceName = "GalleryRuntimeAssets";

        [Header("BGM・拍の表 (実機ではビルダーがここから読む)")]
        public AudioClip casinoBgm;
        public AudioClip museumBgm;
        public AudioClip concertBgm;
        public AudioClip skyBgm;
        public TextAsset concertBeatMap;

        [Header("ビルドに入れておくもの (実行時に Shader.Find で探すシェーダーと、ビルダーが作った材質)")]
        public Shader[] shaders;
        public Material[] materials;

        static GalleryRuntimeAssets cached;
        static bool loaded;

        /// <summary>Resources から読む (なければ null)</summary>
        public static GalleryRuntimeAssets Instance
        {
            get
            {
                if (!loaded)
                {
                    cached = Resources.Load<GalleryRuntimeAssets>(ResourceName);
                    loaded = true;
                }
                return cached;
            }
        }
    }
}
