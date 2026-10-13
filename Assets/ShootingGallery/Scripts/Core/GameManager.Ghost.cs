using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、幽霊モード (おばけの的)。</summary>
    public partial class GameManager
    {
        // ---- 幽霊モード ----

        /// <summary>この的をおばけの的 (幽霊モード) に化けさせてよいか (WeaponDropper が聞く)。条件は海戦モードと同じ</summary>
        public bool CanOfferGhost(Target target, float lifetime)
        {
            if (ghostManor == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + ghostManor.duration + ghostManor.CloseTime + 2f;
        }

        void StartGhost(Target target) => StartGhost(YawToward(target));

        /// <summary>
        /// おばけの的を撃った: 出ている屋台を全部沈め (ハッチも消える)、紫の闇に包まれてから、墓場とお化け屋敷の世界にする。
        /// お化け屋敷は的があった方の先に建つ。武器は懐中電灯 (照らした的がやられる。電池の目盛りは銃の上、手元のゲージが幽霊モードの残り時間)
        /// </summary>
        void StartGhost(float yaw)
        {
            if (DebugClearFor(() => StartGhost(yaw))) return;
            if (ghostManor == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            HideHatch(); // 床下のハッチはすぐに小さくなって消える
            PutAwayStalls(false);
            ghostManor.dropDelay = StallsAwayTime; // 屋台が沈みきってから闇に包まれる
            ghostManor.Begin(yaw);
            player.GiveWeapon(WeaponType.Flashlight, ghostManor.duration);
            Sfx.Play("stageup");
            ShowMessage("GHOST MODE!!", 1.5f, new Color(0.75f, 0.5f, 1f));
        }

        /// <summary>
        /// 幽霊モードの時間切れ: また紫の闇に包まれ、いつもの地面へ戻す。屋台と的は闇で何も見えない間に並べておき (OnGhostLeft)、
        /// 晴れた時にはもう普通に遊べるようにする
        /// </summary>
        void EndGhost()
        {
            if (!GhostActive) return;
            int hits = ghostManor.Hits;
            ghostManor.End();
            if (player.Weapon == WeaponType.Flashlight) player.ResetWeapon();
            ghostReturnStalls.Clear();
            ghostReturnStalls.AddRange(stageStalls);
            HoldStallsFor(ghostManor.CloseTime); // 屋台を並べるまで、屋台の拡張・入れ替えを待たせる
            stageStalls.Clear();
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"GHOST MODE END\nBUSTED {hits}", 2.5f, new Color(0.75f, 0.5f, 1f));
            }
        }

        // 闇の中で幽霊の世界へ来た: 紫の空・紫の霧・月明かりの暗さにすぐ切り替える (晴れた時にはもう墓場)
        void OnGhostArrived()
        {
            RenderSettings.fogStartDistance = 25f;  // お化け屋敷はくっきり、遠くの月は紫にかすむ
            RenderSettings.fogEndDistance = 140f;
            FadeAmbience(ghostFogColor, ghostAmbient, ghostSkyColor, 0.05f);
            FadeSun(sunIntensity * 0.55f, 0.05f); // 月明かり
            if (State == GameState.Playing) ShowMessage("GHOST MODE!!\nSHOOT THE SPOOKS!", 3f, new Color(0.75f, 0.5f, 1f));
        }

        // 闇で何も見えない間にいつもの地面へ戻った: 夜祭りの空と霧に戻し、沈めておいた屋台と的をその場にすぐ並べる (晴れた時にはもう遊べる)。
        // 待っている間にフィーバーが終わったフィーバー屋台は出さない
        void OnGhostLeft()
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, 0.05f);
            FadeSun(sunIntensity, 0.05f);
            ShowHatch();
            foreach (var s in ghostReturnStalls)
            {
                bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                if (show) s.ShowImmediate(State == GameState.Playing);
            }
            ghostReturnStalls.Clear();
        }

        readonly List<Stall> ghostReturnStalls = new List<Stall>(); // 幽霊モードの帰りに並べ直す屋台

        // お化け・魔女・ゾンビ・骸骨を倒した (点数はもう入っている): 高い点数の時は名前を大きく出す
        void OnSpookDown(GhostSpook s)
        {
            if (State != GameState.Playing) return;
            var color = new Color(0.75f, 0.5f, 1f);
            ScorePopup.Spawn(font, s.body.position + Vector3.up * 1.2f, "BOO!", color, 0.3f);
            if (s.Points >= 500) ShowMessage($"{s.displayName} BUSTED!!\n+{s.Points}", 1.5f, new Color(1f, 0.85f, 0.35f));
        }
    }
}
