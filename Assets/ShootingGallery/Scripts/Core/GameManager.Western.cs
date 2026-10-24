using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、西部劇モード (保安官バッジの的)。</summary>
    public partial class GameManager
    {
        // ---- 西部劇モード ----

        /// <summary>この的を保安官バッジの的 (西部劇モード) に化けさせてよいか (WeaponDropper が聞く)。条件は海戦モードと同じ</summary>
        public bool CanOfferWestern(Target target, float lifetime)
        {
            if (westernTrain == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + westernTrain.duration + westernTrain.CloseTime + 2f;
        }

        void StartWestern(Target target) => StartWestern(YawToward(target));

        /// <summary>
        /// 保安官バッジの的を撃った: 出ている屋台を全部沈め、目の前を暗くしてから、その的があった方へ走る列車の屋根の上に立たせる。
        /// 武器はリボルバー (6発撃つと弾込め)。
        /// </summary>
        void StartWestern(float yaw)
        {
            if (DebugClearFor(() => StartWestern(yaw))) return;
            if (westernTrain == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            HideHatch(); // 床下のハッチはすぐに小さくなって消える
            PutAwayStalls(false);
            westernTrain.dropDelay = StallsAwayTime; // 屋台が沈みきってから暗くなる
            westernTrain.Begin(yaw);
            PlayStageBgm(westernBgm);
            player.GiveStageWeapon(WeaponType.Revolver, westernTrain.duration);
            Sfx.Play("stageup");
            ShowMessage("WILD WEST!!", 1.2f, new Color(0.95f, 0.75f, 0.35f));
        }

        /// <summary>
        /// 西部劇モードの時間切れ: 砂嵐で覆っていつもの地面へ戻す。屋台と的は砂嵐で何も見えない間に並べておき (OnWesternLeft)、
        /// 晴れた時にはもう普通に遊べるようにする
        /// </summary>
        void EndWestern()
        {
            if (!WesternActive) return;
            StopStageBgmAfterStalls(westernBgm, State != GameState.Playing); // 屋台が戻りきるまで曲は流し続ける (ラウンドの終わり・リセットの時はすぐ止める)
            int downed = westernTrain.Downed;
            westernTrain.End();
            ReleaseStageWeapon(WeaponType.Revolver);
            westernReturnStalls.Clear();
            westernReturnStalls.AddRange(stageStalls);
            HoldStallsFor(westernTrain.CloseTime); // 屋台を並べるまで、屋台の拡張・入れ替えを待たせる
            stageStalls.Clear();
            if (State == GameState.Playing)
            {
                Sfx.Play(downed > 0 ? "complete" : "end");
                ShowMessage($"WILD WEST END\nDOWN {downed} OUTLAWS", 2.5f, new Color(0.95f, 0.75f, 0.35f));
            }
        }

        // 真っ暗な間に列車の上へ来た: 昼の荒野の空と霧にすぐ切り替える (明るくなった時にはもう荒野)
        void OnWesternArrived()
        {
            RenderSettings.fogStartDistance = Mathf.Max(normalFogStart, 30f);
            RenderSettings.fogEndDistance = Mathf.Max(normalFogEnd, 90f);
            FadeAmbience(westernFogColor, westernAmbient, westernSkyColor, 0.05f);
            if (State == GameState.Playing) ShowMessage("WILD WEST!!\nSHOOT THE OUTLAWS!", 3f, new Color(0.95f, 0.75f, 0.35f));
        }

        // 砂嵐で何も見えない間にいつもの地面へ戻った: 夜祭りの空と霧に戻し、沈めておいた屋台と的をその場にすぐ並べる (晴れた時にはもう遊べる)。
        // 待っている間にフィーバーが終わったフィーバー屋台は出さない
        void OnWesternLeft()
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, 0.05f);
            ShowHatch();
            foreach (var s in westernReturnStalls)
            {
                bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                if (show) s.ShowImmediate(State == GameState.Playing);
            }
            westernReturnStalls.Clear();
        }

        readonly List<Stall> westernReturnStalls = new List<Stall>(); // 西部劇モードの帰りに並べ直す屋台

        // カウボーイ / 馬車を倒した: 大きな表示 (点数は当てた時にもう入っている)
        void OnRiderDown(WesternRider rider)
        {
            if (State != GameState.Playing) return;
            var color = new Color(0.95f, 0.75f, 0.35f);
            ScorePopup.Spawn(font, rider.transform.position + Vector3.up * 3f, "BANG!", color, 0.45f);
            ShowMessage($"{rider.displayName} DOWN!!\n+{rider.Points}", 1.5f, color);
        }
    }
}
