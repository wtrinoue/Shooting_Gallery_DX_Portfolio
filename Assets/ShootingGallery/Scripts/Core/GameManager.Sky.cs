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
            return timeLeft > lifetime + skyBattle.duration + parachuteTime + 2f; // 帰りのパラシュートが降りきるまで
        }

        /// <summary>
        /// プロペラの的を撃った: 床とハッチがその場できれいさっぱり消えて一面の空になり、出ている屋台はばらばらに回りながら下の地上へ落ちていく。
        /// 足元にはプロペラ機がせり上がり、気球や敵機が飛ぶ。yaw = 撃った的の向き (機首をこちらへ)
        /// </summary>
        void StartSky(float yaw)
        {
            if (DebugClearFor(() => StartSky(yaw))) return;
            if (skyBattle == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            HideHatch(true); // 床下のハッチも床といっしょに、その場で消す
            skyStalls.Clear();
            skyStalls.Add(mainStall);
            if (IsFever) skyStalls.AddRange(activeFeverStalls);
            for (int i = 0; i < skyStalls.Count; i++)
                skyStalls[i].FallAway(skyFallTime, i == 0 ? 0.1f : Random.Range(0.05f, 0.5f)); // 屋台ごとに少しずつずらして、ばらばらに

            skyBattle.dropDelay = 0.3f; // 床が消えたら、すぐに足元へプロペラ機がせり上がってくる
            skyBattle.Begin(yaw); // 機首を撃った的の方へ
            player.GiveStageWeapon(WeaponType.AirMachineGun, skyBattle.duration + skyBattle.riseTime); // 空中ステージの間はずっと機関銃 (昔の飛行機の水冷式。性能はマシンガンと同じ)
            FadeAmbience(skyFogColor, skyAmbient, skySkyColor, 0.4f); // 床が消えると同時に、夜から昼の青空へ
            Sfx.Play("stageup");
            ShowMessage("SKY BATTLE!!\nSHOOT DOWN THE BALLOONS & PLANES!", 3f, new Color(0.55f, 0.85f, 1f));
        }

        /// <summary>空中ステージの時間切れ: 足元から床とハッチが戻って翼が沈み、屋台は元の場所の真上からパラシュートでまっすぐ降りてくる</summary>
        void EndSky()
        {
            if (!SkyActive) return;
            int downed = skyBattle.Downed;
            skyBattle.End();
            ReleaseStageWeapon(WeaponType.AirMachineGun);
            bool playing = State == GameState.Playing;
            StartCoroutine(RiseStallsAfter(new List<Stall>(skyStalls), skyBattle.groundGrowTime,
                (s, i) => s.ParachuteIn(parachuteTime, i == 0 ? 0f : Random.Range(0.1f, 0.7f), parachuteHeight, playing),
                parachuteTime + 0.8f + 0.7f));
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
