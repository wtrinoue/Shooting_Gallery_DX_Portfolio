using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、カジノモード (カジノの的)。</summary>
    public partial class GameManager
    {
        // ---- カジノモード ----

        /// <summary>この的をカジノの的 (カジノモード) に化けさせてよいか (WeaponDropper が聞く)。条件は海戦モードと同じ</summary>
        public bool CanOfferCasino(Target target, float lifetime)
        {
            if (casinoHall == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + casinoHall.IntroTime + casinoHall.playTime + casinoHall.CloseTime + 2f;
        }

        void StartCasino(Target target) => StartCasino(YawToward(target));

        /// <summary>
        /// カジノの的を撃った: 出ている屋台を全部沈め、その的があった方を正面にして円柱の大広間で囲む。
        /// 明かりが点くまではいつもの銃のまま (撃つ物がない)。明かりが点いたらダーツを渡す (OnCasinoLightUp)。
        /// </summary>
        void StartCasino(float yaw)
        {
            if (DebugClearFor(() => StartCasino(yaw))) return;
            if (casinoHall == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            PutAwayStalls(false);
            casinoHall.dropDelay = StallsAwayTime; // 屋台が沈みきってから壁が伸びてくる
            casinoHall.Begin(yaw);
            Sfx.Play("stageup");
            ShowMessage("CASINO MODE!!", 2f, new Color(1f, 0.4f, 0.6f));
        }

        // 真っ暗に (囲まれた直後 / 終わりの時)。影の設定によっては太陽の光が壁を通って差し込むので、太陽も消す
        void OnCasinoDarken()
        {
            FadeAmbience(RenderSettings.fogColor, new Color(0.02f, 0.02f, 0.03f), null, 0.25f);
            FadeSun(0f, 0.25f);
        }

        // 全部の明かりが点いた: 一瞬まぶしく光らせてから、カジノの明るさへ落ち着かせる (太陽は消したまま。中はシャンデリアの明かり)。
        // ダーツを渡す (手元のゲージがゲームの残り時間)
        void OnCasinoOpened()
        {
            RenderSettings.ambientLight = new Color(1.6f, 1.35f, 1.05f);
            FadeAmbience(normalFogColor, casinoAmbient, null, 1.6f);
            if (State != GameState.Playing) return;
            player.GiveWeapon(WeaponType.Dart, casinoHall.TimeLeft);
            ShowMessage("WELCOME TO THE CASINO!\nHIT THE SLOT & ROULETTES!", 3f, new Color(1f, 0.85f, 0.3f));
        }

        // 片付けで天井が上がり始めた: いつもの夜の明るさに戻す
        void OnCasinoRestore()
        {
            FadeAmbience(normalFogColor, normalAmbient, null, 1.5f);
            FadeSun(sunIntensity, 1.5f);
        }

        // 太陽 (シーンの平行光源) の明るさを少しずつ変える
        void FadeSun(float to, float duration)
        {
            if (sunLight == null) return;
            if (sunRoutine != null) StopCoroutine(sunRoutine);
            sunRoutine = StartCoroutine(FadeSunRoutine(to, duration));
        }

        IEnumerator FadeSunRoutine(float to, float duration)
        {
            float from = sunLight.intensity;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                sunLight.intensity = Mathf.Lerp(from, to, t);
                yield return null;
            }
            sunLight.intensity = to;
            sunRoutine = null;
        }

        /// <summary>カジノモードの時間切れ: 暗くして飾りを消し、壁を下げ、沈めた屋台を戻す (壁が下がりきってから)</summary>
        void EndCasino()
        {
            if (!CasinoActive) return;
            casinoHall.End();
            if (player.Weapon == WeaponType.Dart) player.ResetWeapon();
            StartCoroutine(RiseStallsAfter(new List<Stall>(stageStalls), casinoHall.CloseTime + 0.1f));
            stageStalls.Clear();
            if (State == GameState.Playing)
            {
                Sfx.Play("end");
                ShowMessage("CASINO CLOSED", 2.5f, new Color(1f, 0.4f, 0.6f));
            }
        }

        /// <summary>
        /// スロットのリールが全部止まった: 出目の点数を入れ、出目の強さに合わせた演出を出す
        /// (そろわない → 小さく / 2つそろう → コイン / そろう → ファンファーレとコインの山 / 777 → 大当たりの花火)
        /// </summary>
        void OnSlotResult(int points, SlotMachine.Tier tier, string face)
        {
            if (State != GameState.Playing || !CasinoActive) return;
            var slotPos = casinoHall.slot.transform.position + Vector3.up * 3.6f;
            switch (tier)
            {
                case SlotMachine.Tier.Miss:
                    Sfx.Play("snipermiss");
                    AwardPoints(points, slotPos, new Color(0.7f, 0.7f, 0.75f), 0.3f);
                    ShowMessage($"{face}\nTOO BAD... +{points}", 1.5f, new Color(0.7f, 0.7f, 0.8f));
                    break;
                case SlotMachine.Tier.Pair:
                    Sfx.Play("coin");
                    AwardPoints(points, slotPos, new Color(1f, 0.85f, 0.4f), 0.4f);
                    ShowMessage($"{face}\nPAIR! +{points}", 1.5f, new Color(1f, 0.85f, 0.4f));
                    break;
                case SlotMachine.Tier.Win:
                case SlotMachine.Tier.BigWin:
                    Sfx.Play("complete");
                    AwardPoints(points, slotPos, new Color(1f, 0.85f, 0.3f), tier == SlotMachine.Tier.BigWin ? 0.8f : 0.6f);
                    ShowMessage($"{face}\n{(tier == SlotMachine.Tier.BigWin ? "BIG WIN!!" : "WIN!")} +{points}", 2.5f, new Color(1f, 0.85f, 0.3f));
                    StartCoroutine(CoinShower(casinoHall.slot.transform.position + Vector3.up * 1.5f, tier == SlotMachine.Tier.BigWin ? 16 : 8));
                    break;
                case SlotMachine.Tier.Jackpot:
                    Sfx.Play("deluxe");
                    Sfx.Play("complete");
                    AwardPoints(points, slotPos, new Color(1f, 0.3f, 0.6f), 1.1f);
                    ShowMessage($"7 7 7\nJACKPOT!!!  +{points}", 4f, new Color(1f, 0.3f, 0.6f));
                    StartCoroutine(CoinShower(casinoHall.slot.transform.position + Vector3.up * 1.5f, 30));
                    for (int i = 0; i < 8; i++) LaunchFireworkAt(casinoHall.slot.transform.position + Random.insideUnitSphere * 3f + Vector3.up * 5f);
                    break;
            }
        }

        // スロットから金色のコインがあふれ出る (光の玉が次々にはじける)
        IEnumerator CoinShower(Vector3 center, int count)
        {
            var mat = fireworkMaterials != null && fireworkMaterials.Length > 0 ? fireworkMaterials[0] : null;
            for (int i = 0; i < count; i++)
            {
                Explosion.Spawn(center + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.3f, 1.5f), Random.Range(-0.6f, 0.2f)), Random.Range(0.12f, 0.25f), mat);
                if (i % 3 == 0) Sfx.Play("coin");
                yield return new WaitForSeconds(0.05f);
            }
        }

        // 決まった場所に花火を1発 (大当たりの時)
        void LaunchFireworkAt(Vector3 center)
        {
            if (fireworkMaterials == null || fireworkMaterials.Length == 0) return;
            var mat = fireworkMaterials[Random.Range(0, fireworkMaterials.Length)];
            Explosion.Spawn(center, Random.Range(0.6f, 0.9f), mat);
            for (int i = 0; i < 6; i++)
                Explosion.Spawn(center + Random.onUnitSphere * Random.Range(0.7f, 1.3f), Random.Range(0.18f, 0.3f), mat);
        }
    }
}
