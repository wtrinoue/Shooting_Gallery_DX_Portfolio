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
            HideHatch(); // 床下のハッチはすぐに小さくなって消える
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
            RenderSettings.ambientLight = new Color(1.5f, 1.15f, 0.85f);
            FadeAmbience(normalFogColor, casinoAmbient, null, 1.6f);
            if (State != GameState.Playing) return;
            player.GiveStageWeapon(WeaponType.Dart, casinoHall.TimeLeft);
            switchClickQuietUntil = Time.time + SwitchClickQuiet; // (持ったばかりの持ち替えは、音を鳴らさない)
            ShowMessage("WELCOME TO THE CASINO!\nSHOOT THE SLOTS, ROULETTES & DARTS!", 3f, new Color(1f, 0.85f, 0.3f));
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
            // ゲーム一式の外の平行光源も、太陽の普段の明るさに対する同じ割合へ
            float k = sunIntensity > 0.0001f ? to / sunIntensity : 0f;
            var othersFrom = new float[otherSuns.Count];
            for (int i = 0; i < otherSuns.Count; i++) othersFrom[i] = otherSuns[i] != null ? otherSuns[i].intensity : 0f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                sunLight.intensity = Mathf.Lerp(from, to, t);
                for (int i = 0; i < otherSuns.Count; i++)
                    if (otherSuns[i] != null) otherSuns[i].intensity = Mathf.Lerp(othersFrom[i], otherSunIntensity[i] * k, t);
                yield return null;
            }
            sunLight.intensity = to;
            for (int i = 0; i < otherSuns.Count; i++)
                if (otherSuns[i] != null) otherSuns[i].intensity = otherSunIntensity[i] * k;
            sunRoutine = null;
        }

        /// <summary>カジノモードの時間切れ: 暗くして飾りを消し、壁を下げ、沈めた屋台を戻す (壁が下がりきってから)</summary>
        void EndCasino()
        {
            if (!CasinoActive) return;
            casinoHall.End();
            // (ダーツ・コインのどちらを持っていても、元に戻す)
            if (player.Weapon == WeaponType.Dart || player.Weapon == WeaponType.Coin || player.Weapon == WeaponType.Ball) ReleaseStageWeapon(player.Weapon);
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
        void OnSlotResult(SlotMachine machine, int points, SlotMachine.Tier tier, string face)
        {
            if (State != GameState.Playing || !CasinoActive || machine == null) return;
            float size = machine.transform.lossyScale.y;
            var slotPos = machine.transform.position + Vector3.up * 3.6f * size;
            // 当たると、スロットのコイン受けから金色のコインが飛び散る (当たりが大きいほどたくさん)
            int coins = tier switch { SlotMachine.Tier.Jackpot => 60, SlotMachine.Tier.BigWin => 35, SlotMachine.Tier.Win => 20, SlotMachine.Tier.Pair => 6, _ => 0 };
            if (coins > 0)
                CoinBurst.Spawn(machine.transform.position + Vector3.up * 0.4f * size - machine.transform.forward * 0.9f * size, -machine.transform.forward, coins,
                                casinoHall.coinMaterial, casinoHall.transform.position.y, tier >= SlotMachine.Tier.BigWin ? 6f : 4.5f);
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
                    StartCoroutine(CoinShower(machine.transform.position + Vector3.up * 1.5f * size, tier == SlotMachine.Tier.BigWin ? 16 : 8));
                    break;
                case SlotMachine.Tier.Jackpot:
                    Sfx.Play("deluxe");
                    Sfx.Play("complete");
                    AwardPoints(points, slotPos, new Color(1f, 0.3f, 0.6f), 1.1f);
                    ShowMessage($"7 7 7\nJACKPOT!!!  +{points}", 4f, new Color(1f, 0.3f, 0.6f));
                    StartCoroutine(CoinShower(machine.transform.position + Vector3.up * 1.5f * size, 30));
                    for (int i = 0; i < 8; i++) LaunchFireworkAt(machine.transform.position + Random.insideUnitSphere * 3f + Vector3.up * 5f * size);
                    break;
            }
        }

        /// <summary>ボールのルーレットのボールがポケットに入った: そのポケットの点数を入れる。緑の 0 (1000) は大当たりで、コインが飛び散る</summary>
        void OnRouletteResult(BallRoulette wheel, int points, int pocket)
        {
            if (State != GameState.Playing || !CasinoActive || wheel == null) return;
            var pos = wheel.ball != null ? wheel.ball.position + Vector3.up * 0.4f : wheel.transform.position + Vector3.up * 2f;
            bool big = points >= 1000;
            Sfx.Play(big ? "complete" : points >= 300 ? "bonus" : "coin");
            AwardPoints(points, pos, big ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.85f, 0.4f), big ? 0.8f : 0.45f);
            ShowMessage(big ? $"ZERO!! JACKPOT!\n+{points}" : $"ROULETTE\n+{points}", big ? 2.5f : 1.3f, big ? new Color(0.4f, 1f, 0.55f) : new Color(1f, 0.85f, 0.4f));
            int coins = big ? 40 : points >= 500 ? 18 : points >= 300 ? 10 : 4;
            CoinBurst.Spawn(pos, -wheel.transform.forward, coins, casinoHall.coinMaterial, casinoHall.transform.position.y, big ? 5.5f : 4f);
            if (big) for (int i = 0; i < 5; i++) LaunchFireworkAt(pos + Random.insideUnitSphere * 2f + Vector3.up * 3f);
        }

        // ---- 狙った先で、武器を持ち替える ----

        /// <summary>
        /// カジノで遊んでいる間、銃の向きの先にあるゲームの武器に持ち替える (スロット = コイン、ボールのルーレット = 青い球、ダーツボード = ダーツ)。
        /// 何もない方向を向いている時は、持っている武器のまま
        /// </summary>
        void UpdateCasinoAim()
        {
            if (!CasinoActive || !casinoHall.IsOpen || State != GameState.Playing || player == null || player.rightGun == null) return;
            CasinoAim(player.rightGun, player.Weapon, false);
            if (player.DualWield && player.leftGun != null) CasinoAim(player.leftGun, player.LeftWeapon, true); // (二丁拳銃の時は左右別々に)
        }

        // ---- 狙った先で持ち替える時の音 (カジノ・夏祭り) ----
        // 左右の手が同時に持ち替えたり、境目で何度も持ち替えたりしても、カチッは間をあけて1回だけ。モードが始まって武器を持ったばかりの間は鳴らさない
        const float SwitchClickQuiet = 0.8f;    // 武器を持ってから、持ち替えの音を鳴らさない秒数 (手元へ飛んでくる間に、左右が狙った先に合わせて持ち替わるので)
        const float SwitchClickInterval = 0.3f; // 次の持ち替えの音まで、最低これだけあける
        float switchClickQuietUntil;

        void PlaySwitchClick()
        {
            if (Time.time < switchClickQuietUntil) return;
            switchClickQuietUntil = Time.time + SwitchClickInterval;
            Sfx.Play("click");
        }

        // gun の向きの先のゲームの武器に、その手の銃だけ持ち替える
        void CasinoAim(GunController gun, WeaponType current, bool left)
        {
            if (current != WeaponType.Dart && current != WeaponType.Coin && current != WeaponType.Ball) return; // (ほかの武器の的を取った時などは、そのまま)
            var t = gun.transform;
            if (!Physics.Raycast(t.position, t.forward, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore)) return;
            WeaponType? want = null;
            if (hit.collider.GetComponentInParent<SlotMachine>() != null) want = WeaponType.Coin;
            else if (hit.collider.GetComponentInParent<BallRoulette>() != null) want = WeaponType.Ball; // (ボールのルーレットは青い球)
            else if (hit.collider.GetComponentInParent<RouletteWheel>() != null) want = WeaponType.Dart;
            if (!want.HasValue || current == want.Value) return;
            player.SwitchStageWeapon(want.Value, left);
            PlaySwitchClick();
        }

        // スロットから金色のコインがあふれ出る (光の玉が次々にはじける)
        IEnumerator CoinShower(Vector3 center, int count)
        {
            var mat = fireworkMaterials != null && fireworkMaterials.Length > 0 ? fireworkMaterials[0] : null;
            for (int i = 0; i < count; i++)
            {
                Explosion.Spawn(center + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.3f, 1.5f), Random.Range(-0.6f, 0.2f)), Random.Range(0.12f, 0.25f), mat);
                if (i % 3 == 0) Sfx.Play("coin");
                yield return new WaitForSeconds(0.025f); // (30 個でも 0.75 秒で出しきる)
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
