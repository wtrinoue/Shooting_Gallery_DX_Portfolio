using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、空中ステージ (プロペラの的)。</summary>
    public partial class GameManager
    {
        // ---- 空中ステージ ----

        /// <summary>
        /// この的をプロペラの的 (空中ステージ) に化けさせてよいか (WeaponDropper が聞く)。条件は海戦モードと同じ。
        /// </summary>
        public bool CanOfferSky(Target target, float lifetime)
        {
            if (skyBattle == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + skyBattle.duration + skyBattle.riseTime + 2f;
        }

        /// <summary>プロペラの的を撃った: 出ている屋台を全部沈め、地面を落として足元にプロペラ機を出し、気球や敵機を飛ばす</summary>
        void StartSky()
        {
            if (DebugClearFor(StartSky)) return;
            if (skyBattle == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            skyStalls.Clear();
            skyStalls.Add(mainStall);
            if (IsFever) skyStalls.AddRange(activeFeverStalls);
            foreach (var s in skyStalls) s.Sink(stallMoveDuration);

            skyBattle.dropDelay = StallsAwayTime; // 屋台が沈みきってから地面が落ちる
            skyBattle.Begin();
            player.GiveWeapon(WeaponType.MachineGun, skyBattle.duration + skyBattle.riseTime); // 空中ステージの間はずっとマシンガン
            StartCoroutine(WhileActiveAfter(StallsAwayTime, () => SkyActive, () => FadeAmbience(skyFogColor, skyAmbient, skySkyColor))); // 屋台が沈みきってから、夜から昼の青空へ
            Sfx.Play("stageup");
            ShowMessage("SKY BATTLE!!\nSHOOT DOWN THE BALLOONS & PLANES!", 3f, new Color(0.55f, 0.85f, 1f));
        }

        /// <summary>空中ステージの時間切れ: 地面を戻して翼を沈め、沈めた屋台を元の場所に戻す (地面が戻ってから)</summary>
        void EndSky()
        {
            if (!SkyActive) return;
            int downed = skyBattle.Downed;
            skyBattle.End();
            if (player.Weapon == WeaponType.MachineGun) player.ResetWeapon();
            StartCoroutine(RiseStallsAfter(new List<Stall>(skyStalls), skyBattle.riseTime + 0.1f));
            skyStalls.Clear();
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor); // 夜に戻す
            if (State == GameState.Playing)
            {
                Sfx.Play(downed > 0 ? "complete" : "end");
                ShowMessage($"SKY BATTLE END\nSHOT DOWN {downed}", 2.5f, new Color(0.55f, 0.85f, 1f));
            }
        }

        // 敵を撃墜した: 爆発音と大きな表示 (点数は撃墜した1発でもう入っている)
        void OnEnemyDown(SkyEnemy enemy)
        {
            if (State != GameState.Playing) return;
            Sfx.Play("explosion");
            Sfx.Play("bonus");
            var color = new Color(1f, 0.6f, 0.2f);
            ScorePopup.Spawn(font, enemy.transform.position + Vector3.up * 2.5f, "SHOT DOWN!", color, 0.45f);
            ShowMessage($"{enemy.displayName} DOWN!!\n+{enemy.Points}", 1.5f, color);
        }
    }
}
