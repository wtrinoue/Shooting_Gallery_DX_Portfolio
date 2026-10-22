using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GameManager のうち、2 MIN の終わりのフィナーレ。
    /// 残り finalePrepareSeconds 秒でメイン屋台のカウンターにクラッカーが出て (FinaleCrackers.Prepare)、
    /// 時間切れと同時に紙吹雪が飛び出し、拍手 (finaleApplause) が流れて、だんだん小さくなって止まる。
    /// </summary>
    public partial class GameManager
    {
        [Header("Finale (2 MIN の終わり: 残り時間が少なくなるとクラッカーが出て、時間切れと同時に紙吹雪と拍手)")]
        public FinaleCrackers finaleCrackers;
        [Tooltip("豪華屋台のメインに置いた同じ配置のクラッカー (豪華タイム中に見える。紙吹雪は普通の屋台の組が出す)")]
        public FinaleCrackers deluxeFinaleCrackers;
        [Tooltip("時間切れと同時に流す拍手")]
        public AudioClip finaleApplause;
        [Range(0f, 1f)] public float finaleApplauseVolume = 0.8f;
        [Tooltip("クラッカーを出す、ラウンドの残り時間 (秒)")]
        public float finalePrepareSeconds = 10f;
        [Tooltip("拍手をいつもの大きさのまま聞かせる秒数 (このあと、だんだん小さくして止める)")]
        public float finaleApplauseHold = 1.5f;
        [Tooltip("拍手が小さくなって消えるまでの秒数")]
        public float finaleApplauseFade = 4f;

        AudioSource applauseSource;
        float applauseStartTime = -1f; // 拍手を流し始めた時刻 (流していなければ -1)

        void UpdateFinale()
        {
            // 2 MIN の残りが少なくなった: クラッカーを出して待つ (拍手の音も先に読んでおく)
            if (State == GameState.Playing && TimeAttack && timeLeft <= finalePrepareSeconds && finaleCrackers != null && !finaleCrackers.Prepared)
            {
                finaleCrackers.Prepare();
                if (deluxeFinaleCrackers != null) deluxeFinaleCrackers.Prepare();
                if (finaleApplause != null && finaleApplause.loadState == AudioDataLoadState.Unloaded) finaleApplause.LoadAudioData();
            }

            // 拍手: しばらくそのまま、そのあと だんだん小さくして止める
            if (applauseStartTime < 0f || applauseSource == null) return;
            float t = Time.time - applauseStartTime - finaleApplauseHold;
            float k = t <= 0f ? 1f : 1f - t / Mathf.Max(0.05f, finaleApplauseFade);
            if (k <= 0f || !applauseSource.isPlaying)
            {
                StopApplause();
                return;
            }
            applauseSource.volume = finaleApplauseVolume * k * k; // (耳で聞いてなめらかに小さくなるように 2乗で)
        }

        /// <summary>時間切れ: 紙吹雪を飛び出させ、拍手を流す</summary>
        void PlayFinale()
        {
            if (finaleCrackers != null) finaleCrackers.Burst(mainHomePos, mainHomeRot);
            if (deluxeFinaleCrackers != null) deluxeFinaleCrackers.Burst(mainHomePos, mainHomeRot); // (金紙が飛んで反動で揺れるだけ。紙吹雪は出さない)
            if (finaleApplause == null) return;
            if (applauseSource == null)
            {
                applauseSource = gameObject.AddComponent<AudioSource>();
                applauseSource.playOnAwake = false;
                applauseSource.loop = false;
                applauseSource.spatialBlend = 0f; // どこにいても同じ大きさ
            }
            if (finaleApplause.loadState == AudioDataLoadState.Unloaded) finaleApplause.LoadAudioData();
            applauseSource.clip = finaleApplause;
            applauseSource.time = 0f;
            applauseSource.volume = finaleApplauseVolume;
            applauseSource.Play();
            applauseStartTime = Time.time;
        }

        void StopApplause()
        {
            applauseStartTime = -1f;
            if (applauseSource != null) applauseSource.Stop();
        }

        /// <summary>フィナーレをその場ですぐ片付ける (ラウンドの始まり・リセット)</summary>
        void ClearFinale()
        {
            StopApplause();
            if (finaleCrackers != null) finaleCrackers.ClearAll();
            if (deluxeFinaleCrackers != null) deluxeFinaleCrackers.ClearAll();
        }

        /// <summary>フィナーレのクラッカーをしまう (タイトルに戻る時。拍手と落ちている紙吹雪はそのまま)</summary>
        void HideFinaleCrackers()
        {
            if (finaleCrackers != null) finaleCrackers.Hide();
            if (deluxeFinaleCrackers != null) deluxeFinaleCrackers.Hide();
        }
    }
}
