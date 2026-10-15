using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GameManager のうち、WORLD GATE (タイトルの START の横の的)。
    /// 撃つと、メイン屋台が沈み、1段だけの屋台 6軒 (gateStalls) がプレイヤーを取り囲む。屋台の段には、別の世界へ飛ぶ的が横一列に並ぶ
    /// (輪の正面の屋台から順に詰め、段に入りきらない分は次の屋台へ)。好きな世界の的を撃つとその世界へ行き、終わるとまた6軒の輪に戻ってくる。
    /// 時間切れはなく、A ボタンの長押しでタイトルに戻る。
    /// 仕組みはデバッグモードと同じ (DebugMode も立てる: 輪を出したまま・的は屋台ごとに決まった種類に化ける・別の世界の片付けは DebugClearFor)。
    /// その間 mainStall / feverStalls は WORLD GATE の屋台を指すので、別の世界へ行く時に沈め、戻る時にせり上げるのも普段と同じ処理で動く。
    /// </summary>
    public partial class GameManager
    {
        /// <summary>WORLD GATE の間か</summary>
        public bool GateMode { get; private set; }

        // 並べる別の世界の的 (この順に、輪の正面の屋台から右回りに詰める)
        static readonly TargetKind[] GateWorlds =
        {
            TargetKind.Naval, TargetKind.Sky, TargetKind.Ninja, TargetKind.Kaiju, TargetKind.Casino, TargetKind.Western,
            TargetKind.Space, TargetKind.Ghost, TargetKind.Mine, TargetKind.Festival, TargetKind.Submarine, TargetKind.Music,
        };

        /// <summary>WORLD GATE を始める (タイトルの WORLD GATE の的を撃った時)</summary>
        void StartWorldGate()
        {
            if (gateStalls == null || gateStalls.Length < 2) return;
            StopForNewRound(); // 視点を身長 180cm の人の高さに合わせるのも、ここでする (START と同じ)
            StartRound(true, true); // 時間切れのないデバッグモードの仕組みで動かす
            GateMode = true;

            // メイン屋台 (START の的がある) は沈め、WORLD GATE の1軒目を正面に、残りの5軒を輪に使う
            var oldMain = mainStall;
            oldMain.SetTargetsAvailable(false);
            oldMain.Sink(stallMoveDuration);
            mainStall = gateStalls[0];
            var pool = new Stall[gateStalls.Length - 1];
            System.Array.Copy(gateStalls, 1, pool, 0, pool.Length);
            feverStalls = pool;
            normalFeverStalls = pool;
            mainStall.PlaceAt(mainHomePos, mainHomeRot);

            StartFever(); // 6軒の輪 (正面の屋台は隠れたまま輪の場所へ。メイン屋台が沈みきってからせり上がる)
            StartCoroutine(RiseGateMainAfter(stallMoveDuration));
            ShowMessage("WORLD GATE\nCHOOSE YOUR WORLD!\nHOLD A : QUIT", 3.5f, new Color(0.7f, 0.6f, 1f));
        }

        IEnumerator RiseGateMainAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (GateMode && State == GameState.Playing && !ChallengeActive) mainStall.Rise(stallMoveDuration);
        }

        /// <summary>WORLD GATE を終える (タイトルへ戻る時。StopEverything から)。WORLD GATE の屋台を沈め、普通のメイン屋台を元の場所にせり上げる</summary>
        void EndWorldGate()
        {
            if (!GateMode) return;
            GateMode = false;
            if (gateStalls != null)
                foreach (var s in gateStalls)
                    if (s != null) { s.SetTargetsAvailable(false); s.Sink(stallMoveDuration); }
            mainStall = normalMainStall;
            feverStalls = gimmickFeverStalls;
            normalFeverStalls = gimmickFeverStalls;
            mainStall.PlaceAt(mainHomePos, mainHomeRot);
            StartCoroutine(RiseNormalMainAfter(stallMoveDuration));
        }

        IEnumerator RiseNormalMainAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            // タイトルなら的は出さない (START の的は GoTitle が出す)。エンドレス / デバッグを続けて始めた時は的も出す
            if (!GateMode) mainStall.Rise(stallMoveDuration, State == GameState.Playing);
        }

        /// <summary>
        /// WORLD GATE で、この屋台に並べる別の世界の的。輪の正面の屋台から右回りに、各屋台の段の的の数だけ順に詰める
        /// (段に入りきらない分は次の屋台へ。余った屋台は普通の的のまま)
        /// </summary>
        TargetKind[] GateKinds(Stall stall)
        {
            var ring = new List<Stall> { mainStall };
            if (IsFever) ring.AddRange(activeFeverStalls);
            int index = ring.IndexOf(stall);
            if (index < 0) return null;
            int start = 0;
            for (int i = 0; i < index; i++) start += ring[i].targets != null ? ring[i].targets.Length : 0;
            int count = Mathf.Clamp(GateWorlds.Length - start, 0, stall.targets != null ? stall.targets.Length : 0);
            var kinds = new TargetKind[count];
            System.Array.Copy(GateWorlds, start, kinds, 0, count);
            return kinds;
        }
    }
}
