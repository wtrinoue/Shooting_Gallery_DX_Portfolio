using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GameManager のうち、2 MIN の終わりのフィナーレ。
    /// 残り finalePrepareSeconds 秒で、すべての屋台のカウンターにクラッカーが出て (FinaleCrackers.Prepare。見えるのは今出ている屋台の組)、
    /// 時間切れと同時に、出ている屋台のクラッカーから紙吹雪が飛び出し、拍手 (finaleApplause) が流れて、だんだん小さくなって止まる。
    /// </summary>
    public partial class GameManager
    {
        [Header("Finale (2 MIN の終わり: 残り時間が少なくなるとクラッカーが出て、時間切れと同時に紙吹雪と拍手)")]
        [Tooltip("普通のメイン屋台のクラッカー (屋台が1軒も出ていない時は、この組がメイン屋台の最初の位置から紙吹雪を出す)")]
        public FinaleCrackers mainFinaleCrackers;
        [Tooltip("すべての屋台 (メイン・フィーバー・豪華屋台) のクラッカー。mainFinaleCrackers も含む")]
        public FinaleCrackers[] finaleCrackerSets;
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
            if (State == GameState.Playing && TimeAttack && timeLeft <= finalePrepareSeconds && mainFinaleCrackers != null && !mainFinaleCrackers.Prepared)
            {
                foreach (var f in FinaleSets()) f.Prepare(); // (このあと せり上がる屋台にも、出ている状態で付いてくる)
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

        /// <summary>
        /// 時間切れ: 出ている屋台のクラッカーから紙吹雪を飛び出させ、拍手を流す。
        /// 屋台の位置で決めるので、StopEverything (フィーバーの屋台が沈む・豪華屋台が戻る) より前に呼ぶこと
        /// </summary>
        void PlayFinale()
        {
            bool any = false;
            foreach (var f in FinaleSets())
            {
                bool shown = FinaleStallShown(f);
                f.Burst(shown); // (出ていない屋台の組も金紙だけ飛ばす。あとで出てきた時に、ふたが残っていないように)
                any |= shown;
            }
            // 1軒も出ていない (別の世界のモードの途中など): メイン屋台が戻ってくる最初の位置から出す
            if (!any && mainFinaleCrackers != null) mainFinaleCrackers.BurstAt(mainHomePos, mainHomeRot);

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

        // その組の屋台が、今プレイヤーのまわりに出ているか (メイン屋台か、フィーバー中に輪に並んでいる屋台で、見えている)
        bool FinaleStallShown(FinaleCrackers f)
        {
            if (f.stall == null || !f.stall.gameObject.activeInHierarchy) return false;
            var s = f.stall.GetComponent<Stall>();
            return s != null && (s == mainStall || (IsFever && activeFeverStalls.Contains(s)));
        }

        System.Collections.Generic.IEnumerable<FinaleCrackers> FinaleSets()
        {
            if (finaleCrackerSets == null) yield break;
            foreach (var f in finaleCrackerSets) if (f != null) yield return f;
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
            foreach (var f in FinaleSets()) f.ClearAll();
        }

        /// <summary>フィナーレのクラッカーをしまう (タイトルに戻る時。拍手と落ちている紙吹雪はそのまま)</summary>
        void HideFinaleCrackers()
        {
            foreach (var f in FinaleSets()) f.Hide();
        }
    }
}
