using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、海戦モード (錨の的)。</summary>
    public partial class GameManager
    {
        // ---- 海戦モード ----

        /// <summary>
        /// この的を錨の的 (海戦モード) に化けさせてよいか (WeaponDropper が聞く)。
        /// チャレンジ中・豪華タイム中・ボーナスステージ中は出さない。撃たれるまで + 海戦モードの間に時間切れが来る時も出さない。
        /// </summary>
        public bool CanOfferNaval(Target target, float lifetime)
        {
            if (navalBattle == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + navalBattle.duration + navalBattle.riseTime + 2f;
        }

        /// <summary>錨の的を撃った: 出ている屋台を全部沈め、足元に甲板、まわりに海を出して敵の船を走らせる</summary>
        void StartNaval()
        {
            if (DebugClearFor(StartNaval)) return;
            if (navalBattle == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            HideHatch(); // 床下のハッチはすぐに小さくなって消える
            navalStalls.Clear();
            navalStalls.Add(mainStall);
            if (IsFever) navalStalls.AddRange(activeFeverStalls);
            foreach (var s in navalStalls) s.Sink(stallMoveDuration);

            navalBattle.dropDelay = StallsAwayTime; // 屋台が沈みきってから甲板と海がせり上がる
            navalBattle.Begin();
            player.GiveWeapon(WeaponType.Bazooka, navalBattle.duration + navalBattle.riseTime); // 海戦の間はずっとバズーカ
            StartCoroutine(WhileActiveAfter(StallsAwayTime, () => NavalActive, () => FadeAmbience(navalFogColor, navalAmbient, navalSkyColor))); // 屋台が沈みきってから、夜から昼の青空へ
            Sfx.Play("stageup");
            ShowMessage("SEA BATTLE!!\nSINK THE PIRATE SHIPS!", 3f, new Color(0.4f, 0.8f, 1f));
        }

        /// <summary>海戦モードの時間切れ: 甲板と海を沈め、沈めた屋台を元の場所に戻す</summary>
        void EndNaval()
        {
            if (!NavalActive) return;
            int sunk = navalBattle.Sunk;
            navalBattle.End();
            if (player.Weapon == WeaponType.Bazooka) player.ResetWeapon();
            StartCoroutine(RiseStallsAfter(new List<Stall>(navalStalls), navalBattle.riseTime * 0.6f));
            navalStalls.Clear();
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor); // 夜に戻す
            if (State == GameState.Playing)
            {
                Sfx.Play(sunk > 0 ? "complete" : "end");
                ShowMessage($"SEA BATTLE END\nSUNK {sunk} SHIPS", 2.5f, new Color(0.5f, 0.85f, 1f));
            }
        }

        // 敵の船を撃沈した: ボーナス点と大きな表示
        void OnShipSunk(EnemyShip ship)
        {
            if (State != GameState.Playing) return;
            // 点数は船に当てた時に (帆の数字の分だけ) もう入っている。ここは撃沈の演出だけ
            Sfx.Play("explosion");
            Sfx.Play("bonus");
            var color = new Color(1f, 0.6f, 0.2f);
            ScorePopup.Spawn(font, ship.transform.position + Vector3.up * 3.2f, "SUNK!", color, 0.5f);
            ShowMessage($"{ship.displayName} SUNK!!\n+{ship.Points}", 1.5f, color);
        }
    }
}
