using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、宇宙モード (宇宙の的)。</summary>
    public partial class GameManager
    {
        // ---- 宇宙モード ----

        /// <summary>この的を宇宙の的 (宇宙モード) に化けさせてよいか (WeaponDropper が聞く)。条件は海戦モードと同じ</summary>
        public bool CanOfferSpace(Target target, float lifetime)
        {
            if (spaceStage == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + spaceStage.duration + spaceStage.CloseTime + 2f;
        }

        /// <summary>
        /// 宇宙の的を撃った: 出ている屋台を全部沈めてから、まわりを真っ暗にして床をはるか下へ離し、星空の宇宙に浮かべる。
        /// 武器はミサイル (手元のゲージが宇宙モードの残り時間)。
        /// </summary>
        void StartSpace()
        {
            if (DebugClearFor(StartSpace)) return;
            if (spaceStage == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            HideHatch(); // 床下のハッチはすぐに小さくなって消える
            PutAwayStalls(false);
            spaceStage.dropDelay = StallsAwayTime; // 屋台が沈みきってから暗くなる
            spaceStage.Begin();
            player.GiveWeapon(WeaponType.Missile, spaceStage.duration);
            Sfx.Play("stageup");
            ShowMessage("SPACE MODE!!", 2f, new Color(0.6f, 0.75f, 1f));
        }

        // 暗くなる: 夜空よりも暗い宇宙の色にする。太陽は少し弱めて残す (惑星に光の当たる側と影の側ができる)。遠くの星が霧に隠れないように、霧をずっと遠くからにする
        void OnSpaceDarken()
        {
            FadeAmbience(spaceSkyColor, spaceAmbient, spaceSkyColor, 2.5f);
            FadeSun(sunIntensity * 0.7f, 2.5f);
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 500f;
            ShowMessage("WELCOME TO SPACE!\nSHOOT THE STARS & PLANETS WITH MISSILES!", 3.5f, new Color(0.6f, 0.75f, 1f));
        }

        // 星が消えて床が戻り始めた: いつもの夜の明るさに戻す
        void OnSpaceRestore()
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, 2f);
            FadeSun(sunIntensity, 2f);
        }

        /// <summary>宇宙モードの時間切れ: 的を消し、星を暗くして床を戻し、沈めた屋台を戻す (床が戻ってから)</summary>
        void EndSpace()
        {
            if (!SpaceActive) return;
            int hits = spaceStage.Hits;
            spaceStage.End();
            if (player.Weapon == WeaponType.Missile) player.ResetWeapon();
            StartCoroutine(RiseStallsAfter(new List<Stall>(stageStalls), spaceStage.CloseTime));
            stageStalls.Clear();
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"SPACE MODE END\nHIT {hits}", 2.5f, new Color(0.6f, 0.75f, 1f));
            }
        }

        // 的を壊した: 大きな的 (惑星・ロケット・金の星) の時は名前を大きく出す (点数はもう入っている)
        void OnSpaceObjectDestroyed(SpaceObject o)
        {
            if (State != GameState.Playing || o.Points < 500) return;
            var color = o.Points >= 1000 ? new Color(1f, 0.85f, 0.3f) : new Color(0.6f, 0.85f, 1f);
            ShowMessage($"{o.displayName}!!\n+{o.Points}", 1.5f, color);
        }
    }
}
