using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、音楽モード (四分音符の的)。</summary>
    public partial class GameManager
    {
        // ---- 音楽モード ----

        /// <summary>この的を四分音符の的 (音楽モード) に化けさせてよいか (WeaponDropper が聞く)。条件は幽霊モードと同じ</summary>
        public bool CanOfferMusic(Target target, float lifetime)
        {
            if (concertHall == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + concertHall.duration + concertHall.CloseTime + 2f;
        }

        /// <summary>
        /// 四分音符の的を撃った: 赤いカーテンがぐるりと閉まってプレイヤーを囲み、その間に屋台がしまわれてコンサートホールになる (OnConcertCovered)。
        /// ステージは的があった方に建つ。カーテンが閉まっている間に、プレイヤーは浮き床ごと客席の上へ上がる。カーテンが開くと演奏が始まる。
        /// 武器はトランペット (五線譜をなびかせた音符を吹き出す)
        /// </summary>
        void StartMusic(float yaw)
        {
            if (DebugClearFor(() => StartMusic(yaw))) return;
            if (concertHall == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            stageStalls.Clear();
            SetShownStallTargets(false);
            SetFlatFloor(true); // カーテンが閉まる間に、ハッチが縮んで穴がふさがる
            ResetView(true); // 浮き床で持ち上げる前に、いつもの視点にそろえる (そこからの高さで動かすので)
            concertHall.Begin(yaw, player.transform);
            player.GiveStageWeapon(WeaponType.Trumpet, concertHall.duration);
            Sfx.Play("stageup");
            ShowMessage("MUSIC MODE!!", 2f, new Color(1f, 0.85f, 0.3f));
        }

        // カーテンが閉まりきった: その外で屋台をしまい、薄暗いコンサートホールの明るさにする
        void OnConcertCovered()
        {
            if (!MusicActive) return;
            PutAwayStalls(true); // 集めるだけ
            foreach (var s in stageStalls) s.HideImmediate();
            RenderSettings.fogStartDistance = 40f; // ホールの中は霧がかからない
            RenderSettings.fogEndDistance = 120f;
            FadeAmbience(concertFogColor, concertAmbientDim, concertSkyColor, 0.3f);
            FadeSun(0f, 0.3f); // 屋根の下 (外の月明かりは入らない)
        }

        // カーテンが開いて消えた
        void OnConcertOpened()
        {
            if (State == GameState.Playing && MusicActive)
                ShowMessage("WELCOME TO THE CONCERT!\nSHOOT THE SOLOIST\n& THE FLOWING NOTES!", 3f, new Color(1f, 0.85f, 0.3f));
        }

        // ステージがぱっと明るくなる (照明は ConcertHall が上げ、明るくなりきった瞬間に演奏が始まる)。ホール全体の環境光も同時に明るくする
        void OnConcertMusic()
        {
            FadeAmbience(concertFogColor, concertAmbientPlay, concertSkyColor, concertHall.lightRiseSeconds);
        }

        /// <summary>音楽モードの時間切れ: 演奏が終わり、カーテンが閉まって元の屋台に戻る (ラウンドの終わり・リセットの時は、その場ですぐ戻す)</summary>
        void EndMusic()
        {
            if (!MusicActive) return;
            int hits = concertHall.Hits;
            bool immediate = State != GameState.Playing;
            concertHall.End(immediate);
            ReleaseStageWeapon(WeaponType.Trumpet);
            HoldStallsFor(immediate ? 0.1f : concertHall.CloseTime); // カーテンが消えるまで、屋台の拡張・入れ替えを待たせる
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"MUSIC MODE END\nHIT {hits}", 2.5f, new Color(1f, 0.85f, 0.3f));
            }
        }

        // 帰りにカーテンが閉まりきった (ホールは消えた): 屋台を元の場所に並べ、床・明るさを戻す (的はまだ出さない。カーテンが消えてから)。
        // 待っている間にフィーバーが終わったフィーバー屋台は出さない
        void OnConcertCoveredBack(bool immediate)
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, 0.05f);
            FadeSun(sunIntensity, 0.05f);
            if (State == GameState.Playing) SetFlatFloor(false);
            foreach (var s in stageStalls)
            {
                bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                if (show) s.ShowImmediate(false);
            }
            stageStalls.Clear();
        }

        // カーテンが消えた: 屋台の的を出す
        void OnConcertClosed()
        {
            if (State == GameState.Playing && !ChallengeActive) SetShownStallTargets(true);
        }
    }
}
