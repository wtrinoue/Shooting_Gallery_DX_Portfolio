using UnityEditor;
using UnityEngine;

namespace ShootingGallery.EditorTools
{
    /// <summary>
    /// 音楽モードの BGM (GalleryBuilder.ConcertBgmPath。約4分49秒の長い曲) の読み込み方を決める。
    /// そのままだと読み込み時に全部ほどいてメモリに置く (約 50MB) ので、少しずつ読みながら鳴らす (Streaming)、Vorbis 圧縮にする
    /// </summary>
    public class ConcertBgmImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (assetPath != GalleryBuilder.ConcertBgmPath) return;
            var importer = (AudioImporter)assetImporter;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
