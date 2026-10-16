using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、発掘モード (つるはしの的)。</summary>
    public partial class GameManager
    {
        // ---- 発掘モード ----

        bool mineStallsAway; // 降りていって、屋台を止めて沈めてあるか

        /// <summary>この的をつるはしの的 (発掘モード) に化けさせてよいか (WeaponDropper が聞く)。条件は海戦モードと同じ</summary>
        public bool CanOfferMine(Target target, float lifetime)
        {
            if (mineCave == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + mineCave.duration + mineCave.CloseTime + 2f;
        }

        /// <summary>
        /// つるはしの的を撃った: 足元の土の床に穴が掘られ (ハッチは隠れる)、プレイヤーがゆっくり真下へ降りていく。
        /// 屋台はそのまま動いている (降りていく演出のため)。縦穴を抜けて見えなくなったら、その場で地下へ隠す (OnMineDeep)。
        /// 屋台の的は始まった時点でしまう (降りていく途中で機能付きの的を撃って、演出が途中で切られないように)。
        /// 上がりきって穴が埋まったら出し直す (OnMineClosed)
        /// </summary>
        void StartMine()
        {
            if (DebugClearFor(StartMine)) return;
            if (mineCave == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            mineStallsAway = false;
            stageStalls.Clear();
            SetShownStallTargets(false);
            ResetView(true); // 下へ降りる前に、いつもの視点にそろえる (そこからの深さで動かすので)
            mineCave.Begin(player.transform);
            player.GiveStageWeapon(WeaponType.Pickaxe, mineCave.duration); // つるはしを回しながら投げる
            // 縦穴を降りる間に、だんだん暗くなる
            FadeAmbience(mineFogColor, mineAmbient * 0.6f, mineSkyColor, mineCave.descendTime * 0.7f);
            FadeSun(sunIntensity * 0.12f, mineCave.descendTime * 0.6f);
            Sfx.Play("stageup");
            ShowMessage("MINING MODE!!", 2f, new Color(1f, 0.75f, 0.35f));
        }

        // 降りていって縦穴の口からも地上の屋台が見えなくなった: 屋台をその場で地下へ隠す
        // (沈めると、縦穴の口から看板が地面へしまわれていくのが見えてしまう)
        void OnMineDeep()
        {
            if (!MineActive || mineStallsAway) return;
            mineStallsAway = true;
            PutAwayStalls(true); // 集めるだけ
            foreach (var s in stageStalls) s.HideImmediate();
        }

        // 縦穴を抜けて洞窟に入った: 洞窟の明るさと霧 (遠くの壁が暗がりに溶ける)
        void OnMineCave()
        {
            RenderSettings.fogStartDistance = 8f;  // 奥の壁が暗がりに沈むように
            RenderSettings.fogEndDistance = 55f;
            FadeAmbience(mineFogColor, mineAmbient, mineSkyColor, 1.5f);
            if (State == GameState.Playing) ShowMessage("WELCOME TO THE MINE!\nSHOOT THE GEMS & MINE CARTS!", 3f, new Color(1f, 0.75f, 0.35f));
        }

        // 上がり始めた: だんだんいつもの夜の明るさに戻す
        void OnMineRising()
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, mineCave.ascendTime * 0.8f);
            FadeSun(sunIntensity, mineCave.ascendTime * 0.8f);
        }

        // 上がってきて屋台が見えるくらいまで来た: 屋台はもう動いている (沈めておいた屋台をその場に並べる)。
        // 的はまだ出さない (上がりきって穴が埋まってから OnMineClosed で出す)。
        // 待っている間にフィーバーが終わったフィーバー屋台は出さない
        void OnMineNearSurface()
        {
            if (!mineStallsAway) return;
            mineStallsAway = false;
            foreach (var s in stageStalls)
            {
                bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                if (show) s.ShowImmediate(false);
            }
            stageStalls.Clear();
        }

        /// <summary>発掘モードの時間切れ: ゆっくり上がって元の穴へ戻る (ラウンドの終わり・リセットの時は、その場ですぐ地上へ戻す)</summary>
        void EndMine()
        {
            if (!MineActive) return;
            int hits = mineCave.Hits;
            bool immediate = State != GameState.Playing;
            if (immediate)
            {
                RenderSettings.fogStartDistance = normalFogStart;
                RenderSettings.fogEndDistance = normalFogEnd;
                FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, 0.05f);
                FadeSun(sunIntensity, 0.05f);
            }
            mineCave.End(immediate);
            ReleaseStageWeapon(WeaponType.Pickaxe);
            HoldStallsFor(immediate ? 0.1f : mineCave.CloseTime); // 上がりきって穴が埋まるまで、屋台の拡張・入れ替えを待たせる
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"MINING MODE END\nDUG UP {hits}", 2.5f, new Color(1f, 0.75f, 0.35f));
            }
        }

        // 上がりきって穴が土で埋まり、土の床が消えた: ハッチを戻し、しまっておいた屋台の的を出す
        void OnMineClosed()
        {
            ShowHatch();
            if (State == GameState.Playing && !ChallengeActive) SetShownStallTargets(true);
        }

        // 出ている屋台 (メイン + フィーバー中の屋台) の的を出す / しまう
        void SetShownStallTargets(bool value)
        {
            mainStall.SetTargetsAvailable(value);
            if (!IsFever) return;
            foreach (var s in activeFeverStalls) s.SetTargetsAvailable(value);
        }

        // 宝石を割った (点数はもう入っている): 高い宝石の時は名前を大きく出す
        void OnGemBroken(MineGem g)
        {
            if (State != GameState.Playing || g.points < 500) return;
            ShowMessage($"{g.displayName}!!\n+{g.points}", 1.5f, g.points >= 1000 ? new Color(0.85f, 0.95f, 1f) : new Color(1f, 0.4f, 0.45f));
        }
    }
}
