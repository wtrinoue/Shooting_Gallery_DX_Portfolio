using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、潜水艦モード (潜水艦の的)。</summary>
    public partial class GameManager
    {
        // ---- 潜水艦モード ----

        /// <summary>この的を潜水艦の的 (潜水艦モード) に化けさせてよいか (WeaponDropper が聞く)。条件は発掘モードと同じ</summary>
        public bool CanOfferSubmarine(Target target, float lifetime)
        {
            if (submarineDive == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + submarineDive.duration + submarineDive.CloseTime + 2f;
        }

        /// <summary>
        /// 潜水艦の的を撃った: 足元から砂の床が広がり、屋台が斜めに傾いて砂に埋もれていく (OnSubCovered)。同時にあたり一面が海になり、
        /// それを横目に丸い潜水艦がプレイヤーを包んで、海の底へ沈んでいく。武器は魚雷。
        /// 屋台の的は始まった時点でしまう。浮かび上がって水が引いたら屋台が砂からせり上がり (OnSubDrained)、砂の床が消えたら的を出し直す (OnSubClosed)
        /// </summary>
        void StartSubmarine()
        {
            if (DebugClearFor(StartSubmarine)) return;
            if (submarineDive == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            stageStalls.Clear();
            SetShownStallTargets(false);
            ResetView(true); // もぐる前に、いつもの視点にそろえる (そこからの深さで動かすので)
            submarineDive.Begin(player.transform, player.head.transform);
            player.GiveStageWeapon(WeaponType.Torpedo, submarineDive.duration);
            Sfx.Play("stageup");
            ShowMessage("SUBMARINE MODE!!", 2f, new Color(1f, 0.85f, 0.25f));
        }

        // 砂の床が広がりきった: その下でハッチを消し、出ている屋台を斜めに傾けて砂に埋もれさせる (屋台ごとに少しずらす)
        void OnSubCovered()
        {
            HideHatch();
            if (!SubmarineActive) return;
            PutAwayStalls(true); // 集めるだけ
            for (int i = 0; i < stageStalls.Count; i++)
                stageStalls[i].Bury(submarineDive.stallBuryTime, i == 0 ? 0f : Random.Range(0.1f, 0.6f), submarineDive.sandPuffMaterial);
        }

        // 頭が水にもぐった: 水の中の青い色と濃い霧 (遠くが青くかすむ)
        void OnSubUnderwater()
        {
            RenderSettings.fogStartDistance = 3f;  // 近くはすっきり見え、遠くへ行くほどゆっくり青くかすむ (海がずっと広がって見えるように)
            RenderSettings.fogEndDistance = 85f;
            FadeAmbience(subFogColor, subAmbient, subSkyColor, 1.2f);
            FadeSun(sunIntensity * 0.45f, 1.2f);
            if (State == GameState.Playing && SubmarineActive)
                ShowMessage("DIVE!!\nTORPEDO THE SEA CREATURES\n& OPEN TREASURE CHESTS!", 3f, new Color(0.5f, 0.9f, 1f));
        }

        // 浮かんで頭が水から出た: いつもの夜の明るさに戻す
        void OnSubSurfaced()
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            FadeAmbience(normalFogColor, normalAmbient, normalSkyColor, 1f);
            FadeSun(sunIntensity, 1f);
        }

        // 海の水が引いた: 埋もれていた屋台が、砂の中から斜めにせり上がって立ち直る (的はまだ出さない。砂の床が消えてから OnSubClosed で出す)。
        // 待っている間にフィーバーが終わったフィーバー屋台は出さない。immediate (ラウンドの終わりなど) なら、その場ですぐ並べる
        void OnSubDrained(bool immediate)
        {
            foreach (var s in stageStalls)
            {
                bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                if (!show) continue;
                if (immediate) s.ShowImmediate(false);
                else s.Unbury(submarineDive.stallRiseTime, false, submarineDive.sandPuffMaterial);
            }
            stageStalls.Clear();
        }

        /// <summary>潜水艦モードの時間切れ: ゆっくり浮かんで水面へ戻る (ラウンドの終わり・リセットの時は、その場ですぐ地上へ戻す)</summary>
        void EndSubmarine()
        {
            if (!SubmarineActive) return;
            int hits = submarineDive.Hits;
            bool immediate = State != GameState.Playing;
            submarineDive.End(immediate);
            ReleaseStageWeapon(WeaponType.Torpedo);
            HoldStallsFor(immediate ? 0.1f : submarineDive.CloseTime); // 浮かび上がって屋台が戻り、砂の床が消えるまで、屋台の拡張・入れ替えを待たせる
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"SUBMARINE MODE END\nHIT {hits}", 2.5f, new Color(1f, 0.85f, 0.25f));
            }
        }

        // 砂の床が消えた: ハッチを戻し、屋台の的を出す
        void OnSubClosed()
        {
            ShowHatch();
            if (State == GameState.Playing && !ChallengeActive) SetShownStallTargets(true);
        }
    }
}
