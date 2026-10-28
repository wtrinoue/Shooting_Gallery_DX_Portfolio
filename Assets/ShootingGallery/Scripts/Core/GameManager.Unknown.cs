using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、Unknown ステージ (ワープゲートの的)。計画は new_docs/develop.txt</summary>
    public partial class GameManager
    {
        // ---- Unknown ステージ ----

        const float UnknownFreezeDelay = 0.35f; // 的を撃ってから世界が灰色に止まるまでの秒数 (屋台の的がしまわれるのを待つ)
        readonly List<Transform> unknownBreakables = new List<Transform>(); // 破片に切り分ける物 (使い回す)

        /// <summary>この的を Unknown ステージの的に化けさせてよいか (WeaponDropper が聞く)。条件は美術館モードと同じ</summary>
        public bool CanOfferUnknown(Target target, float lifetime)
        {
            if (unknownStage == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + unknownStage.duration + unknownStage.CloseTime + 2f;
        }

        /// <summary>
        /// Unknown ステージの的を撃った: 屋台の的だけしまい (屋台は見えたまま)、その場で世界が灰色に止まる。
        /// 割れる瞬間に、屋台・ハッチ・地面をまとめた空間が、同じ形の灰色の破片に入れ替わり (OnUnknownSwapped で本物を隠す)、立体のまま散らばる。
        /// ふしぎな空間に着くと、武器は紫の電撃 (WeaponType.Lightning) になる (当たった物は別の世界へ転送される)
        /// </summary>
        void StartUnknown(float yaw)
        {
            if (DebugClearFor(() => StartUnknown(yaw))) return;
            if (unknownStage == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            PutAwayStalls(true); // 集めるだけ (屋台はまだ動かさない。灰色に止まった世界に残すように)
            foreach (var s in stageStalls) s.SetTargetsAvailable(false);
            unknownBreakables.Clear();
            foreach (var s in stageStalls) unknownBreakables.Add(s.transform);
            if (bonusStage != null) unknownBreakables.Add(bonusStage.transform);
            unknownStage.Begin(yaw, UnknownFreezeDelay, unknownBreakables);
            PauseRoundBgmNow(); // 的を撃った瞬間に、それまでの曲を消す (アンノウンの曲は、割れて別の世界が見えた時から。OnUnknownShattered)
            ShowMessage("UNKNOWN STAGE...", 2f, new Color(0.75f, 0.85f, 1f));
        }

        // 割れる瞬間、同じ形の灰色の破片に入れ替わった: 本物の屋台・ハッチはその場で隠す (見た目は同じなので気づかない)
        void OnUnknownSwapped()
        {
            if (!UnknownActive) return;
            foreach (var s in stageStalls) s.HideImmediate();
            HideHatch(true);
        }

        // 灰色の世界が割れた (外はもう ふしぎな空間): 空間の明るさ・霧にする
        void OnUnknownShattered()
        {
            if (!UnknownActive) return;
            RenderSettings.fogStartDistance = 50f; // ゲートの奥 (45m) まで広がる世界や、大きなホールの奥が見えるように、霧はずっと遠くから
            RenderSettings.fogEndDistance = 140f;
            FadeAmbience(unknownFogColor, unknownAmbient, unknownSkyColor, 0.3f);
            PlayStageBgm(unknownBgm); // 別の世界が見えた: アンノウンの曲を流し始める
        }

        // 割れ終わった: 遊び始める (ゲートが順に開く)
        void OnUnknownArrived()
        {
            if (State != GameState.Playing || !UnknownActive) return;
            player.GiveStageWeapon(WeaponType.Lightning, unknownStage.TimeLeft); // 紫の電撃 (終わるまで)
            ShowMessage("WELCOME TO THE UNKNOWN!\nSEND THE VISITORS BACK\nWITH THE WARP BOLT!", 3f, new Color(0.75f, 0.85f, 1f));
        }

        // 紛れ込んできた物を撃った: 大きな点数の時は名前を出す (点数はもう入っている)
        void OnUnknownObjectDown(UnknownObject o)
        {
            if (State != GameState.Playing || o.Points < 500) return;
            ShowMessage($"{o.displayName}!!\n+{o.Points}", 1.5f, new Color(1f, 0.85f, 0.35f));
        }

        /// <summary>Unknown ステージの時間切れ: 部品が戻ってきて組み上がり、色が戻ると元の屋台の前 (ラウンドの終わり・リセットの時は、その場ですぐ戻す)</summary>
        void EndUnknown()
        {
            if (!UnknownActive) return;
            if (State != GameState.Playing) StopStageBgm(unknownBgm, true); // (ラウンドの終わり・リセットの時はすぐ止める。ふだんは戻りきってから OnUnknownClosed で)
            int hits = unknownStage.Hits;
            bool immediate = State != GameState.Playing;
            unknownStage.End(immediate);
            ReleaseStageWeapon(WeaponType.Lightning);
            HoldStallsFor(immediate ? 0.1f : unknownStage.CloseTime); // 色が戻るまで、屋台の拡張・入れ替えを待たせる
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"UNKNOWN STAGE END\nHIT {hits}", 2.5f, new Color(0.75f, 0.85f, 1f));
            }
        }

        // 帰りに灰色の空間が組み上がった: 破片と入れ替えるため、本物の屋台・ハッチをその場ですぐに出し、明るさもすぐ戻す
        // (的はまだ出さない。色が戻ってから)。待っている間にフィーバーが終わったフィーバー屋台は出さない
        void OnUnknownLeft(bool immediate)
        {
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            if (ambienceRoutine != null) { StopCoroutine(ambienceRoutine); ambienceRoutine = null; }
            RenderSettings.fogColor = normalFogColor;
            SceneEnvironment.SetAmbient(normalAmbient);
            if (player != null && player.head != null) player.head.backgroundColor = normalSkyColor;
            if (State == GameState.Playing) ShowHatch(true);
            foreach (var s in stageStalls)
            {
                bool show = s == mainStall || (IsFever && activeFeverStalls.Contains(s));
                if (show) s.ShowImmediate(false);
            }
            stageStalls.Clear();
        }

        // 色が戻った: もう屋台の前にいる。屋台の的を出す
        void OnUnknownClosed()
        {
            StopStageBgm(unknownBgm); // 元の世界に戻りきった: BGM をふわっと止める
            if (State == GameState.Playing && !ChallengeActive) SetShownStallTargets(true);
        }
    }
}
