using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、忍者モード (手裏剣の的)。</summary>
    public partial class GameManager
    {
        // ---- 忍者モード ----

        /// <summary>この的を手裏剣の的 (忍者モード) に化けさせてよいか (WeaponDropper が聞く)。条件は海戦モードと同じ</summary>
        public bool CanOfferNinja(Target target, float lifetime)
        {
            if (ninjaDojo == null || target == null || State != GameState.Playing) return false;
            if (ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return false;
            var stall = target.GetComponentInParent<Stall>();
            if (stall == null || !stall.IsSettled) return false;
            return timeLeft > lifetime + ninjaDojo.duration + ninjaDojo.CloseTime + 2f;
        }

        /// <summary>
        /// 手裏剣の的を撃った: 出ている屋台を全部沈め、その的があった方を正面にして壁と天井でプレイヤーを囲む。
        /// 武器は手裏剣 (白いレーザーで狙う)。
        /// </summary>
        void StartNinja(Target target)
        {
            Vector3 dir = target.transform.position - player.transform.position;
            StartNinja(dir.sqrMagnitude > 0.001f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : 0f);
        }

        // yaw = 正面にする向き (度)
        void StartNinja(float yaw)
        {
            if (DebugClearFor(() => StartNinja(yaw))) return;
            if (ninjaDojo == null || ChallengeActive || DeluxeActive || (bonusStage != null && bonusStage.IsBusy)) return;
            HideHatch(); // 床下のハッチはすぐに小さくなって消える
            ninjaStalls.Clear();
            ninjaStalls.Add(mainStall);
            if (IsFever) ninjaStalls.AddRange(activeFeverStalls);
            foreach (var s in ninjaStalls) s.Sink(stallMoveDuration);

            // 奥座敷の一番奥まで見えるように、霧を遠くからにする
            RenderSettings.fogStartDistance = Mathf.Max(normalFogStart, 30f);
            RenderSettings.fogEndDistance = Mathf.Max(normalFogEnd, 55f);
            ninjaDojo.dropDelay = StallsAwayTime; // 屋台が沈みきってから壁が起き上がる
            ninjaDojo.Begin(yaw);
            player.GiveStageWeapon(WeaponType.Shuriken, ninjaDojo.duration + 0.5f); // 忍者モードの間はずっと手裏剣
            StartCoroutine(WhileActiveAfter(StallsAwayTime, () => NinjaActive, () => FadeAmbience(normalFogColor, ninjaAmbient))); // 屋台が沈みきってから
            Sfx.Play("stageup");
            ShowMessage("NINJA MODE!!", 2f, new Color(0.75f, 0.5f, 1f));
        }

        // 正面のふすまが開き始めた: 忍者の BGM を流し始める
        void OnDojoDoorsOpening()
        {
            if (NinjaActive && State == GameState.Playing) PlayStageBgm(ninjaBgm);
        }

        // 正面のふすまが開いた: 忍者が出てくる合図
        void OnDojoOpened()
        {
            if (State != GameState.Playing) return;
            ShowMessage("THROW SHURIKEN!\nDEFEAT THE NINJAS!", 2f, new Color(0.75f, 0.5f, 1f));
        }

        /// <summary>忍者モードの時間切れ: 壁と天井を片付け、沈めた屋台を元の場所に戻す (壁が倒れ終わってから)</summary>
        void EndNinja()
        {
            if (!NinjaActive) return;
            StopStageBgmAfterStalls(ninjaBgm, State != GameState.Playing); // 屋台が戻りきるまで曲は流し続ける (ラウンドの終わり・リセットの時はすぐ止める)
            int hits = ninjaDojo.Hits;
            ninjaDojo.End();
            ReleaseStageWeapon(WeaponType.Shuriken);
            StartCoroutine(RiseStallsAfter(new List<Stall>(ninjaStalls), ninjaDojo.CloseTime + 0.1f));
            ninjaStalls.Clear();
            FadeAmbience(normalFogColor, normalAmbient);
            RenderSettings.fogStartDistance = normalFogStart;
            RenderSettings.fogEndDistance = normalFogEnd;
            if (State == GameState.Playing)
            {
                Sfx.Play(hits > 0 ? "complete" : "end");
                ShowMessage($"NINJA MODE END\nDEFEATED {hits}", 2.5f, new Color(0.75f, 0.5f, 1f));
            }
        }
    }
}
