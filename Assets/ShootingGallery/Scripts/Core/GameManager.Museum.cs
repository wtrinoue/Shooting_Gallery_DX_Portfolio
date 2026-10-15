using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、美術館モード (絵筆の的)。</summary>
    public partial class GameManager
    {
        // ---- 美術館モード ----

        const float MuseumStallSinkTime = 0.55f; // 始まってすぐに、屋台が下へ一気にしまわれる秒数 (キャンバスはそのあとに出る)

        /// <summary>この的を絵筆の的 (美術館モード) に化けさせてよいか (WeaponDropper が聞く)。条件は音楽モードと同じ</summary>
        public bool CanOfferMuseum(Target target, float lifetime)
        {
            if (artMuseum == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + artMuseum.duration + artMuseum.CloseTime + 2f;
        }

        /// <summary>
        /// 絵筆の的を撃った: まず屋台が下へ一気にしまわれ、床のハッチが真ん中へ縮んで消える (平らな床)。
        /// 何もなくなってから目の前にキャンバスが現れ、筆がひとりでに美術館の中を描き出す。白い閃光に包まれると美術館の中 (OnMuseumArrived)。
        /// 武器はイマジネーション (キャンバスの近くでは筆、彫刻の近くではトンカチになる)
        /// </summary>
        void StartMuseum(float yaw)
        {
            if (DebugClearFor(() => StartMuseum(yaw))) return;
            if (artMuseum == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            PutAwayStalls(true); // 集めるだけ (しまい方はここで決める)
            foreach (var s in stageStalls)
            {
                s.SetTargetsAvailable(false);
                s.Sink(MuseumStallSinkTime);
            }
            SetFlatFloor(true); // ハッチが真ん中へ縮んで消え、穴にふたをする
            artMuseum.Begin(yaw, Mathf.Max(MuseumStallSinkTime, HatchTime) + 0.25f);
            player.GiveWeapon(WeaponType.Imagination, artMuseum.duration); // (始まりの音は鳴らさない。描き終えて光る時に、閃光の音)
            ShowMessage("ART MUSEUM!!", 2f, new Color(0.95f, 0.7f, 1f));
        }

        // 白い光で何も見えない間に美術館へ来た: その外で屋台をしまい、床のハッチを閉じ、美術館の中の明るさにする
        void OnMuseumArrived()
        {
            if (!MuseumActive) return;
            foreach (var s in stageStalls) s.HideImmediate(); // もう沈んでいる (念のため隠しきる)
            RenderSettings.fogStartDistance = 16f; // 展示室の中ははっきり見え、廊下の奥はだんだん霧にかすんで、果てが見えない
            RenderSettings.fogEndDistance = 60f;
            FadeAmbience(museumFogColor, museumAmbient, museumSkyColor, 0.05f);
            if (State == GameState.Playing)
                ShowMessage("CREATE MASTERPIECES!\nSHOOT THE BLANK CANVASES\n& THE STONE BLOCKS!", 3f, new Color(0.95f, 0.7f, 1f));
        }

        /// <summary>美術館モードの時間切れ: 白い閃光に包まれて、元の屋台の前へ戻る (ラウンドの終わり・リセットの時は、その場ですぐ戻す)</summary>
        void EndMuseum()
        {
            if (!MuseumActive) return;
            int hits = artMuseum.Hits, done = artMuseum.Completed;
            bool immediate = State != GameState.Playing;
            artMuseum.End(immediate);
            if (player.Weapon == WeaponType.Imagination) player.ResetWeapon();
            HoldStallsFor(immediate ? 0.1f : artMuseum.CloseTime); // 光が晴れるまで、屋台の拡張・入れ替えを待たせる
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"ART MUSEUM END\nMASTERPIECES {done}", 2.5f, new Color(0.95f, 0.7f, 1f));
            }
        }

        // 帰りに光で何も見えない間 (美術館は消えた): 屋台を元の場所に並べ、床・明るさを戻す (的はまだ出さない。光が晴れてから)。
        // 待っている間にフィーバーが終わったフィーバー屋台は出さない
        void OnMuseumLeft(bool immediate)
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, 0.05f);
            if (State == GameState.Playing) SetFlatFloor(false);
            foreach (var s in stageStalls)
            {
                bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                if (show) s.ShowImmediate(false);
            }
            stageStalls.Clear();
        }

        // 光が晴れた: もう屋台の前にいる。屋台の的を出す
        void OnMuseumClosed()
        {
            if (State == GameState.Playing && !ChallengeActive) SetShownStallTargets(true);
        }
    }
}
