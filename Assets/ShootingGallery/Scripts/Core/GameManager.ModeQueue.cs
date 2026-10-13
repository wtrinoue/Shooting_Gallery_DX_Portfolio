using System.Collections;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GameManager のうち、モードの的 (チャレンジ・別の世界のモード・DX) を撃った時の始め方。
    /// 撃ったらすぐに始める。他のチャレンジやモード・DX・ボーナスステージの途中なら、それを終わらせて、片付けが済みしだい始める
    /// (前は、他のモードの途中で撃ったモードの的は何も起きずに消えていた。マシンガンなどモード中の武器で撃つと反応しないように見えていた)。
    /// マシンガンや散弾で同時にいくつも撃った時は、いちばん先に当たったモードだけを始め、2番目以降は取り消す (リセット)。
    /// </summary>
    public partial class GameManager
    {
        struct PendingMode
        {
            public TargetKind kind;
            public float yaw;   // 的があった向き (カジノ・西部劇・忍者の正面)
            public Stall stall; // 的があった屋台 (スナイパー・マシンガンのチャレンジで引っ込める屋台)
        }

        [Tooltip("モードの的に当ててからこの秒数のうちに当たった別のモードの的は「同時に撃った」として取り消す")]
        public float simultaneousModeWindow = 0.3f;
        float modeLockUntil;          // ここまでは、次のモードの的に当たっても取り消す
        Coroutine pendingModeStart;   // 前のモードの片付けを待ってから始める処理

        /// <summary>この種類の的は、撃つとモードが始まる的か</summary>
        static bool IsModeKind(TargetKind kind) =>
            kind == TargetKind.Sniper || kind == TargetKind.MachineGunChallenge || kind == TargetKind.Deluxe ||
            kind == TargetKind.Naval || kind == TargetKind.Sky || kind == TargetKind.Ninja || kind == TargetKind.Kaiju ||
            kind == TargetKind.Casino || kind == TargetKind.Western || kind == TargetKind.Space || kind == TargetKind.Ghost;

        /// <summary>モードの的を撃った: すぐに始める (同時に撃った2番目以降は取り消す)</summary>
        void RequestMode(Target target)
        {
            if (State != GameState.Playing) return;
            if (Time.time < modeLockUntil) return; // 同時に撃った2番目以降 (と、前のモードの片付け待ちの間) は取り消す
            var p = new PendingMode { kind = target.kind, yaw = YawToward(target), stall = target.GetComponentInParent<Stall>(true) };

            // 途中のチャレンジ・モード・DX・ボーナスステージを終わらせる。屋台が戻ってくる途中なら戻りきるのを待つ。
            // スナイパー・マシンガンのチャレンジは、豪華タイム (DX) はそのまま続ける (豪華版の屋台でも普通の屋台でも、その場所を引っ込めるだけ)
            bool challenge = p.kind == TargetKind.Sniper || p.kind == TargetKind.MachineGunChallenge;
            float wait = EndAllForNewMode(keepDeluxe: challenge);
            if (StallsBusy) wait = Mathf.Max(wait, stallsReturnUntil - Time.time);
            if (wait <= 0f)
            {
                modeLockUntil = Time.time + simultaneousModeWindow;
                StartPendingMode(p);
                return;
            }
            modeLockUntil = Time.time + wait + 0.2f + simultaneousModeWindow;
            if (pendingModeStart != null) StopCoroutine(pendingModeStart);
            pendingModeStart = StartCoroutine(StartModeAfter(wait + 0.2f, p));
        }

        IEnumerator StartModeAfter(float delay, PendingMode p)
        {
            yield return new WaitForSeconds(delay);
            pendingModeStart = null;
            if (State == GameState.Playing) StartPendingMode(p);
        }

        /// <summary>ラウンドの終わりなど: 片付け待ちのモードも取り消す</summary>
        void CancelPendingMode()
        {
            if (pendingModeStart != null) StopCoroutine(pendingModeStart);
            pendingModeStart = null;
            modeLockUntil = 0f;
        }

        /// <summary>
        /// 新しいモードを始めるために、途中のチャレンジ・モード・DX・ボーナスステージを全部終わらせる。
        /// 片付け (屋台が戻る・壁が下がるなど) が済むまでの秒数を返す (何もなければ 0)
        /// </summary>
        float EndAllForNewMode(bool keepDeluxe = false)
        {
            float wait = 0f;
            if (SniperActive) { EndSniper(false, null); wait = Mathf.Max(wait, stallMoveDuration); }
            if (MachineGunChallengeActive) { EndMachineGunChallenge(); wait = Mathf.Max(wait, machineGunChallenge.SwapBackDelay + stallMoveDuration); }
            if (NavalActive) { EndNaval(); wait = Mathf.Max(wait, navalBattle.riseTime * 0.6f + stallMoveDuration); }
            if (SkyActive) { EndSky(); wait = Mathf.Max(wait, skyBattle.riseTime + 0.1f + stallMoveDuration); }
            if (NinjaActive) { EndNinja(); wait = Mathf.Max(wait, ninjaDojo.CloseTime + 0.1f + stallMoveDuration); }
            if (KaijuActive) { EndKaiju(); wait = Mathf.Max(wait, kaijuCity.CloseTime + KaijuGrowTime + 0.3f); }
            if (CasinoActive) { EndCasino(); wait = Mathf.Max(wait, casinoHall.CloseTime + 0.1f + stallMoveDuration); }
            if (WesternActive) { EndWestern(); wait = Mathf.Max(wait, westernTrain.CloseTime + stallMoveDuration); }
            if (SpaceActive) { EndSpace(); wait = Mathf.Max(wait, spaceStage.CloseTime + 0.1f + stallMoveDuration); }
            if (GhostActive) { EndGhost(); wait = Mathf.Max(wait, ghostManor.CloseTime + stallMoveDuration); }
            if (DeluxeActive && !keepDeluxe) { EndDeluxe(); wait = Mathf.Max(wait, swapOutTime + swapInTime); }
            if (bonusStage != null && bonusStage.IsBusy) { bonusStage.Close(); wait = Mathf.Max(wait, bonusStage.slideTime + 0.3f); }
            return wait;
        }

        void StartPendingMode(PendingMode p)
        {
            // チャレンジは的があった場所の屋台を引っ込める。待っている間に豪華版 ⇔ 普通版に入れ替わっていたら、その場所の今の版を使う。
            // その場所がもうない時 (フィーバー終了など) はメイン屋台を使う
            var stall = CurrentStallAt(p.stall) ?? mainStall;
            switch (p.kind)
            {
                case TargetKind.Sniper: StartSniper(stall); break;
                case TargetKind.MachineGunChallenge: StartMachineGunChallenge(stall); break;
                case TargetKind.Deluxe: StartDeluxe(); break;
                case TargetKind.Naval: StartNaval(); break;
                case TargetKind.Sky: StartSky(); break;
                case TargetKind.Ninja: StartNinja(p.yaw); break;
                case TargetKind.Kaiju: StartKaiju(); break;
                case TargetKind.Casino: StartCasino(p.yaw); break;
                case TargetKind.Western: StartWestern(p.yaw); break;
                case TargetKind.Space: StartSpace(); break;
                case TargetKind.Ghost: StartGhost(p.yaw); break;
            }
        }
    }
}
