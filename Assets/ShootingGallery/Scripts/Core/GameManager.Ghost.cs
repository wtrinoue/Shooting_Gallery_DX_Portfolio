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
            SetFlatFloor(true); // ハッチが小さくなって消え、穴がふさがって平らな床になる
            PutAwayStalls(false);
            ghostManor.dropDelay = StallsAwayTime; // 屋台が沈みきって平らな床になってから、土に変わり始める
            ghostManor.Begin(yaw);
            player.GiveWeapon(WeaponType.Flashlight, ghostManor.duration);
            Sfx.Play("stageup");
            ShowMessage("GHOST MODE!!", 1.5f, new Color(0.75f, 0.5f, 1f));
        }

        /// <summary>
        /// 幽霊モードの時間切れ: 上の部品から崩れて消え、土が足元へ縮んで平らな床に戻る。そのあと穴のふちとハッチが出て、屋台がせり上がる (OnGhostClosed)。
        /// ラウンドの終わり・リセットの時は、その場ですぐ全部消す
        /// </summary>
        void EndGhost()
        {
            if (!GhostActive) return;
            int hits = ghostManor.Hits;
            ghostReturnStalls.Clear();
            ghostReturnStalls.AddRange(stageStalls);
            stageStalls.Clear();
            HoldStallsFor(ghostManor.CloseTime + stallMoveDuration + 0.2f); // 屋台がせり上がりきるまで、屋台の拡張・入れ替えを待たせる
            ghostManor.End(State != GameState.Playing);
            if (player.Weapon == WeaponType.Flashlight) player.ResetWeapon();
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"GHOST MODE END\nBUSTED {hits}", 2.5f, new Color(0.75f, 0.5f, 1f));
            }
        }

        // 床が土に変わり始めた: 空・霧をだんだん紫に、明るさを月明かりにしていく (土が広がり、木やお化け屋敷が組み上がる間に)
        void OnGhostArrived()
        {
            RenderSettings.fogStartDistance = 25f;  // お化け屋敷はくっきり、遠くの月は紫にかすむ
            RenderSettings.fogEndDistance = 140f;
            FadeAmbience(ghostFogColor, ghostAmbient, ghostSkyColor, 2.5f);
            FadeSun(sunIntensity * 0.55f, 2.5f); // 月明かり
            if (State == GameState.Playing) ShowMessage("GHOST MODE!!\nSHOOT THE SPOOKS!", 3f, new Color(0.75f, 0.5f, 1f));
        }

        // 崩れ始めた: 夜祭りの空と霧へだんだん戻していく
        void OnGhostLeft()
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            float t = State == GameState.Playing ? ghostManor.CloseTime : 0.05f;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, t);
            FadeSun(sunIntensity, t);
        }

        // 平らな床に戻った: 穴のふちとハッチが出て、沈めておいた屋台がせり上がる (待っている間にフィーバーが終わった屋台は出さない)。
        // タイトルへ戻る途中なら床は平らなまま (屋台だけ戻す)
        void OnGhostClosed()
        {
            if (State == GameState.Playing) SetFlatFloor(false);
            StartCoroutine(RiseStallsAfter(new List<Stall>(ghostReturnStalls), 0f));
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
