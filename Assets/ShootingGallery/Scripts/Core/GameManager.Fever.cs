using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、フィーバー (360度に屋台が並ぶ) と、時間で屋台の輪が広がるステージ。</summary>
    public partial class GameManager
    {
        void StartFever()
        {
            feverLeft = DebugMode ? EndlessTime : feverDuration; // デバッグモードはフィーバーを終わらせない
            if (IsFever) return; // フィーバー中に再度当てたら延長だけ
            IsFever = true;
            BonusUsedThisFever = false;
            activeFeverStalls.Clear();
            ArrangeFeverStalls();
            player.SetDualWield(true);
            Sfx.Play("fever");
            ShowMessage("FEVER TIME!!\nLOOK AROUND!", 3f, new Color(1f, 0.3f, 0.3f));
        }

        /// <summary>
        /// 時間がたってステージが上がった。フィーバー中ならその場で輪を広げて屋台を増やす
        /// (フィーバー外ならメイン屋台はそのままで、次のフィーバーから広い輪になる)。
        /// </summary>
        void StageUp(int stage)
        {
            Stage = stage;
            int count = StallCountForStage();
            Sfx.Play("stageup");
            ShowMessage($"STAGE {Stage + 1}!\nFEVER STALLS x{count}", 2.5f, new Color(0.5f, 1f, 0.6f));
            if (IsFever) ArrangeFeverStalls();
        }

        int StallCountForStage()
        {
            if (stageStallCounts == null || stageStallCounts.Length == 0) return 6;
            return Mathf.Clamp(stageStallCounts[Mathf.Clamp(Stage, 0, stageStallCounts.Length - 1)], 2, feverStalls.Length + 1);
        }

        /// <summary>
        /// n 軒を輪に並べるときの半径。最初のステージの屋台どうしの間隔 (弦の長さ) を保つように広げる。
        /// </summary>
        float RingRadius(int n)
        {
            int baseCount = stageStallCounts != null && stageStallCounts.Length > 0 ? Mathf.Max(2, stageStallCounts[0]) : 6;
            float r = stallRadius * Mathf.Sin(Mathf.PI / baseCount) / Mathf.Sin(Mathf.PI / n);
            return Mathf.Max(stallRadius, r);
        }

        /// <summary>
        /// 今のステージの数だけ屋台を輪に並べる。メイン屋台は正面 (0度) のまま輪の半径まで下がり、
        /// フィーバー屋台は全ギミックからランダムに選んでランダムな順で並べる。
        /// すでに出ている屋台は地上のまま新しい位置へ滑り、足りない分が新しくせり上がる。
        /// </summary>
        void ArrangeFeverStalls()
        {
            int n = StallCountForStage();
            float radius = RingRadius(n);

            // 足りない分を、まだ出ていない屋台からランダムに選んでランダムな位置に差し込む
            var pool = new List<Stall>();
            foreach (var s in feverStalls)
                if (!activeFeverStalls.Contains(s)) pool.Add(s);
            var added = new List<Stall>();
            while (activeFeverStalls.Count < n - 1 && pool.Count > 0)
            {
                int j = Random.Range(0, pool.Count);
                activeFeverStalls.Insert(Random.Range(0, activeFeverStalls.Count + 1), pool[j]);
                added.Add(pool[j]);
                pool.RemoveAt(j);
            }

            float baseYaw = mainHomeRot.eulerAngles.y; // メイン屋台の向き (正面)
            var mainRot = Quaternion.Euler(0f, baseYaw, 0f);
            mainStall.Slide(mainRot * new Vector3(0f, 0f, radius) + Vector3.up * mainHomePos.y, mainHomeRot, stallMoveDuration);
            n = activeFeverStalls.Count + 1;
            for (int i = 0; i < activeFeverStalls.Count; i++)
            {
                // メイン屋台と合わせて 360 度に等間隔
                var stall = activeFeverStalls[i];
                var rot = Quaternion.Euler(0f, baseYaw + 360f / n * (i + 1), 0f);
                var pos = rot * new Vector3(0f, 0f, radius);
                if (!added.Contains(stall))
                    stall.Slide(pos, rot, stallMoveDuration); // このフィーバーで出ている屋台は後ろへ下がって詰め直す
                else
                {   // 新しく出す屋台は地下からせり上がる (前のフィーバーで沈む途中でも下から出し直す)
                    stall.PlaceAt(pos, rot);
                    stall.Rise(stallMoveDuration);
                }
            }
        }

        void EndFever()
        {
            IsFever = false;
            if (bonusStage != null) bonusStage.Close();
            foreach (var s in feverStalls) s.Sink(stallMoveDuration);
            activeFeverStalls.Clear();
            mainStall.Slide(mainHomePos, mainHomeRot, stallMoveDuration); // メイン屋台は最初の位置へ戻る
            player.SetDualWield(false);
            if (State == GameState.Playing) ShowMessage("FEVER END", 1.5f, Color.white);
        }
    }
}
