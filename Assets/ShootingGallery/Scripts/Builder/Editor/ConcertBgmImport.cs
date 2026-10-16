using UnityEditor;
using UnityEngine;

namespace ShootingGallery.EditorTools
{
    /// <summary>
    /// 音楽モードの BGM (GalleryBuilder.ConcertBgmPath。約4分49秒の長い曲) ・美術館モードの BGM (GalleryBuilder.MuseumBgmPath)・空中ステージの BGM (GalleryBuilder.SkyBgmPath)・カジノモードの BGM (GalleryBuilder.CasinoBgmPath) の読み込み方を決める。
    /// そのままだと読み込み時に全部ほどいてメモリに置く (約 50MB) ので、Vorbis 圧縮のままメモリに置いて、鳴らしながらほどく (CompressedInMemory)
    /// </summary>
    public class ConcertBgmImport : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (assetPath != GalleryBuilder.ConcertBgmPath && assetPath != GalleryBuilder.MuseumBgmPath && assetPath != GalleryBuilder.SkyBgmPath && assetPath != GalleryBuilder.CasinoBgmPath) return;
            var importer = (AudioImporter)assetImporter;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            // 圧縮したままメモリに置く (Vorbis で約 5MB)。Streaming は途中 (1小節目の頭の 1.98 秒) から流す時の位置がずれることがあり、
            // 指揮棒・楽器の拍 (拍の表は曲の頭からの時刻) と音がずれたので使わない
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
