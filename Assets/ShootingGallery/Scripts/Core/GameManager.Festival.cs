using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、夏祭りモード (提灯の的)。</summary>
    public partial class GameManager
    {
        // ---- 夏祭りモード ----

        [Tooltip("夏祭りモードの夜空の色 (星がきれいに見える、深い藍色)")]
        public Color festivalSkyColor = new Color(0.008f, 0.012f, 0.045f);

        /// <summary>この的を提灯の的 (夏祭りモード) に化けさせてよいか (WeaponDropper が聞く)。条件は他の別の世界のモードと同じ</summary>
        public bool CanOfferFestival(Target target, float lifetime)
        {
            if (festivalStage == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + festivalStage.duration + festivalStage.CloseTime + 2f;
        }

        /// <summary>
        /// 提灯の的を撃った: いつもの屋台が沈み、夏祭りの屋台が4軒 (前 = 射的、右 = 輪投げ、後ろ = 金魚すくい、左 = 食べ物) せり上がる。
        /// 地面や空はそのまま。頭の上の提灯が灯り、空高く花火が上がる (遠くの花火が見えるように霧を遠ざける)。
        /// 武器は見ている屋台で変わる (UpdateFestival)。この世界のお金は、夏祭りに入ってから稼いだ分 (スコアとは別。稼いだ分はスコアにも入る。HUD に MONEY ¥)
        /// </summary>
        void StartFestival(float yaw)
        {
            if (DebugClearFor(() => StartFestival(yaw))) return;
            if (festivalStage == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            PutAwayStalls(false);
            festivalStage.riseDelay = StallsAwayTime; // いつもの屋台が沈みきってから、夏祭りの屋台がせり上がる
            festivalStage.moveTime = stallMoveDuration;
            FestivalMoney = 0; // お金は夏祭りに入ってから稼いだ分だけ (スコアとは別)
            festivalStage.Begin(yaw);
            PlayStageBgm(festivalBgm);
            // 空高く上がる遠くの花火と、その奥の星空 (95m くらいまで) が霧でかすまないように、霧はカメラの見える距離 (100m) より遠くから
            RenderSettings.fogStartDistance = Mathf.Max(normalFogStart, 100f);
            RenderSettings.fogEndDistance = Mathf.Max(normalFogEnd, 180f);
            FadeAmbience(festivalSkyColor, normalAmbient, festivalSkyColor, 2.5f); // 星がきれいに見える、深い藍色の夜空へ
            player.GiveStageWeapon(festivalStage.WeaponFor(player.head.transform.forward, WeaponType.Handgun), festivalStage.duration);
            Sfx.Play("stageup");
            ShowMessage("SUMMER FESTIVAL!!\nWIN GAMES = EARN MONEY\nSPEND IT AT THE FOOD STALL!", 3.5f, new Color(1f, 0.55f, 0.35f));
        }

        // 見ている屋台が変わったら、その屋台の武器に持ち替える
        void UpdateFestival()
        {
            if (!FestivalActive) return;
            if (festivalStage.TimeLeft <= 0f)
            {
                EndFestival();
                return;
            }
            var w = festivalStage.WeaponFor(player.head.transform.forward, player.Weapon);
            if (w == player.Weapon) return;
            player.SwitchStageWeapon(w); // (ゲージは続きから)
            Sfx.Play("click");
        }

        /// <summary>夏祭りモードの時間切れ: 提灯が消えて夏祭りの屋台が沈み、いつもの屋台がせり上がる (ラウンドの終わり・リセットの時はすぐ隠す)</summary>
        void EndFestival()
        {
            if (!FestivalActive) return;
            StopStageBgmAfterStalls(festivalBgm, State != GameState.Playing); // 屋台が戻りきるまで曲は流し続ける (ラウンドの終わり・リセットの時はすぐ止める)
            int hits = festivalStage.Hits;
            bool immediate = State != GameState.Playing;
            festivalStage.End(immediate);
            if (festivalStage.IsFestivalWeapon(player.Weapon)) ReleaseStageWeapon(player.Weapon);
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, immediate ? 0.05f : festivalStage.CloseTime); // いつもの夜空に戻す
            StartCoroutine(RiseStallsAfter(new List<Stall>(stageStalls), immediate ? 0f : festivalStage.CloseTime));
            stageStalls.Clear();
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"FESTIVAL END\nGOT {hits}", 2.5f, new Color(1f, 0.55f, 0.35f));
            }
        }

        // 料理を買った: お金を払い (その分スコアが増える)、買った物を屋台の前の机に並べる (買えるかどうかは FestivalStage.TryBuy で確かめ済み)
        void BuyFood(Target food, Vector3 point)
        {
            int price = food.points;
            bool spend = festivalStage == null || festivalStage.spendMoneyOnFood;
            if (spend)
            {   // お金は減り、その分スコアが増える (買った料理の値段がスコアになる)
                FestivalMoney = Mathf.Max(0, FestivalMoney - price);
                Score += price;
            }
            else AddScore(price);
            var color = new Color(1f, 0.8f, 0.3f);
            ScorePopup.Spawn(font, point + Vector3.up * 0.15f, spend ? $"-¥{price}" : $"+{price}", color, 0.16f);
            Sfx.Play("coin");
            string name = food.name.StartsWith("Food_") ? food.name.Substring(5).Replace('_', ' ') : "FOOD";
            ShowMessage(spend ? $"{name}!\n-¥{price}   SCORE +{price}" : $"{name}!\n+{price}", 1.5f, color);
            if (festivalStage != null)
            {
                festivalStage.RegisterHit();
                festivalStage.OnFoodBought(food);
            }
        }
    }
}
