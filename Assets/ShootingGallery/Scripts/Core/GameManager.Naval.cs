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

        [Tooltip("海戦モード: 屋台が海に沈む秒数")]
        public float navalSinkTime = 2.4f;

        /// <summary>
        /// 錨の的を撃った: 床下のハッチがその場で消え、海面がせり上がって床をきれいに覆い、一面の海になる。出ている屋台は傾きながら海に沈んでいく。
        /// 沈んだら足元に甲板がせり上がり、敵の船が走り出す。yaw = 撃った的の向き (船首をこちらへ)
        /// </summary>
        void StartNaval(float yaw)
        {
            if (DebugClearFor(() => StartNaval(yaw))) return;
            if (navalBattle == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            HideHatch(true); // 床下のハッチは、その場で消す (すぐに海が覆う)
            navalStalls.Clear();
            navalStalls.Add(mainStall);
            if (IsFever) navalStalls.AddRange(activeFeverStalls);
            // 海が床を覆ったころから、屋台ごとに少しずらして、ぐらっと傾いて海に沈む (根元に水しぶき)
            for (int i = 0; i < navalStalls.Count; i++)
                navalStalls[i].Bury(navalSinkTime, navalBattle.seaRiseTime * 0.6f + (i == 0 ? 0f : Random.Range(0.05f, 0.5f)), navalBattle.splashMaterial);

            navalBattle.dropDelay = navalBattle.seaRiseTime * 0.6f + navalSinkTime * 0.75f; // 屋台がだいたい沈んでから甲板がせり上がる
            navalBattle.Begin(yaw); // 船首を撃った的の方へ
            player.GiveWeapon(WeaponType.Bazooka, navalBattle.duration + navalBattle.riseTime); // 海戦の間はずっと大砲 (WeaponType.Bazooka の見た目が大砲)
            FadeAmbience(navalFogColor, navalAmbient, navalSkyColor, 0.8f); // 海が広がるのといっしょに、夜から昼の青空へ
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
