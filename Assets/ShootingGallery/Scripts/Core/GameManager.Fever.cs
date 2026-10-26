using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、フィーバー (360度に屋台が並ぶ) と、時間で屋台の輪が広がるステージ。</summary>
    public partial class GameManager
    {
        void StartFever(Vector3? from = null) // from = 撃ったフィーバーの的の場所 (左手の銃がそこから飛んでくる)
        {
            feverLeft = DebugMode ? EndlessTime : feverDuration; // デバッグモードはフィーバーを終わらせない
            if (IsFever) return; // フィーバー中に再度当てたら延長だけ
            IsFever = true;
            BonusUsedThisFever = false;
            activeFeverStalls.Clear();
            ArrangeFeverStalls();
            player.SetDualWield(true, from);
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

        // 屋台の外形 (BuildStallFrame / BuildDeluxeFrame の寸法)。輪に並べた時に隣とぶつかりやすいのは、屋根の手前の角と、柱の前に吊るした提灯
        const float StallHalfWidth = 1.86f;  // 屋根の幅の半分 (豪華屋台の屋根の縁 3.72m)
        const float StallFrontDepth = 0.98f; // 屋台の中心から屋根・幕の手前の端まで
        const float LanternX = 1.7f, LanternDepth = 1.08f, LanternRadius = 0.15f; // 提灯の中心 (横・手前) と半径
        const float HatchHalfWidth = 2.0f, HatchFrontDepth = 1.6f; // 足元のハッチ (StallHatch) の枠の外側の、幅の半分と手前の端 (カウンターの前の布 + すき間 + 枠)

        /// <summary>
        /// n 軒を輪に並べるときの半径。隣どうしの屋台の間が stallGap だけ空くように決める
        /// (屋台は扇形の中に収め、扇の境目の線から屋根の角・提灯までを stallGap / 2 以上離す)。メイン屋台だけの時の stallRadius より内側にはしない。
        /// </summary>
        readonly List<Stall> arrowStalls = new List<Stall>();

        // ボーナスステージが開いてしばらくの間: 出ている屋台 (せり上がる・滑る途中も含む) ごとに、目の前の床から「下を見て」の矢印を出す
        void UpdateBonusArrows()
        {
            if (bonusStage == null || !bonusStage.ArrowsActive) return;
            arrowStalls.Clear();
            if (mainStall != null && mainStall.gameObject.activeInHierarchy) arrowStalls.Add(mainStall);
            if (IsFever)
                foreach (var s in activeFeverStalls)
                    if (s != null && s.gameObject.activeInHierarchy) arrowStalls.Add(s);
            bonusStage.UpdateArrows(arrowStalls, HatchFrontDepth);
        }

        // ---- 屋台が増えた時の、左右を指す赤い矢印 (メイン屋台の前の「下を見て」の矢印の場所の、左右の両隣) ----
        const float SideArrowTime = 6f;  // 出しておく秒数
        const float SideArrowGap = 0.15f; // 真ん中 (「下を見て」の矢印のハッチ) とのすき間
        float sideArrowLeft;

        void ShowSideArrows()
        {
            if (sideArrows == null || sideArrows.Length < 2) return;
            sideArrowLeft = SideArrowTime;
        }

        // メイン屋台の前の左右に置く (屋台が滑ると、いっしょに滑る)。時間がたつか、ラウンドが終わったらしまう
        void UpdateSideArrows()
        {
            if (sideArrows == null || sideArrows.Length < 2) return;
            if (sideArrowLeft > 0f) sideArrowLeft -= Time.deltaTime;
            bool on = sideArrowLeft > 0f && State == GameState.Playing && mainStall != null && mainStall.gameObject.activeInHierarchy;
            if (!on)
            {
                foreach (var a in sideArrows) if (a != null) a.Hide();
                return;
            }
            var p = mainStall.transform.localPosition;
            var dir = new Vector3(p.x, 0f, p.z);
            float dist = dir.magnitude;
            if (dist < 0.01f) return;
            dir /= dist;
            // 「下を見て」の矢印と同じ距離 (屋台の足元のハッチのすぐ手前) の、左右の両隣
            float centerHalf = bonusStage != null && bonusStage.arrow != null ? bonusStage.arrow.length / 2f : 0.3f;
            float minRadius = bonusStage != null ? bonusStage.arrowRadius : 2.5f;
            for (int i = 0; i < 2; i++)
            {
                var a = sideArrows[i];
                if (a == null) continue;
                float r = Mathf.Max(minRadius, dist - HatchFrontDepth - a.width / 2f - 0.06f);
                float side = i == 0 ? -1f : 1f; // 0 = 左 (プレイヤーから見て)
                float angle = (centerHalf + SideArrowGap + a.length / 2f) / r * Mathf.Rad2Deg;
                a.Place(Quaternion.Euler(0f, side * angle, 0f) * dir * r);
                a.Show();
            }
        }

        // ---- フィーバーが終わる時の、メイン屋台の方を指す緑の矢印 ----
        // それぞれの屋台の前の、メイン屋台に近い方の端から、円周にそってメイン屋台の方を指す (真後ろの屋台は両端から外向きに)
        const float ReturnArrowTime = 5f;  // 出しておく秒数
        const float ReturnArrowEnd = 1f;   // 屋台の真ん中から、矢印を置く端まで (円周にそった長さ m)
        readonly List<BonusArrow> returnArrowsShown = new List<BonusArrow>();
        readonly List<BonusArrow>[] returnArrowPool = { new List<BonusArrow>(), new List<BonusArrow>() }; // 左向き / 右向き
        float returnArrowLeft;
        Coroutine returnArrowRoutine;
        readonly List<(float yaw, float r, bool left)> returnArrowPlan = new List<(float, float, bool)>();

        // しまわれる前の屋台の場所から矢印の置き場所を決めておき、屋台がしまわれきったら出す
        void ShowReturnArrows()
        {
            if (returnArrows == null || returnArrows.Length < 2 || returnArrows[0] == null || mainStall == null) return;
            if (ChallengeActive) return; // 別の世界・チャレンジの間 (周りの屋台がもうしまわれている / 見えていない) は出さない
            HideReturnArrows();
            returnArrowPlan.Clear();
            var mp = mainStall.transform.localPosition;
            float mainYaw = Mathf.Atan2(mp.x, mp.z) * Mathf.Rad2Deg;
            float sector = 180f / (activeFeverStalls.Count + 1); // 屋台1軒分の扇の半分 (度)
            float minRadius = bonusStage != null ? bonusStage.arrowRadius : 2.5f;
            var a0 = returnArrows[0];
            foreach (var s in activeFeverStalls)
            {
                if (s == null || !s.IsSettled) continue; // 出ている屋台だけ (しまわれた・せり上がる途中の屋台の所には出さない)
                var p = s.transform.localPosition;
                float dist = new Vector2(p.x, p.z).magnitude;
                if (dist < 0.01f) continue;
                float yaw = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
                float rel = Mathf.DeltaAngle(mainYaw, yaw); // + = メイン屋台から右回り (プレイヤーから見て右) の屋台
                // 「下を見て」の矢印と同じ距離 (屋台の足元のハッチのすぐ手前)。端は、隣の屋台の扇にかからない所まで
                float r = Mathf.Max(minRadius, dist - HatchFrontDepth - a0.width / 2f - 0.06f);
                float offset = Mathf.Min(ReturnArrowEnd, sector * Mathf.Deg2Rad * r - a0.length / 2f - 0.1f) / r * Mathf.Rad2Deg;
                if (Mathf.Abs(rel) >= 179f)
                {   // 真後ろ: 両端から、外向きに (どちらから回ってもよい)
                    returnArrowPlan.Add((yaw - offset, r, true));
                    returnArrowPlan.Add((yaw + offset, r, false));
                }
                else if (rel > 0f) returnArrowPlan.Add((yaw - offset, r, true)); // 右側の屋台: メイン屋台側の端 (プレイヤーから見て左) で、左を指す
                else returnArrowPlan.Add((yaw + offset, r, false));             // 左側の屋台: メイン屋台側の端 (右) で、右を指す
            }
            if (returnArrowPlan.Count == 0) return;
            returnArrowRoutine = StartCoroutine(ShowReturnArrowsAfter(stallMoveDuration));
        }

        IEnumerator ShowReturnArrowsAfter(float delay)
        {
            yield return new WaitForSeconds(delay); // 周りの屋台がしまわれきるまで待つ
            returnArrowRoutine = null;
            if (IsFever || ChallengeActive) yield break; // (待っている間に、次のフィーバー・別の世界・チャレンジが始まった)
            foreach (var p in returnArrowPlan) PlaceReturnArrow(p.yaw, p.r, p.left);
            returnArrowLeft = ReturnArrowTime;
        }

        // yaw の向き、穴の真ん中から r の床に矢印を出す (left = プレイヤーから見て左を指す)。見本を複製して使い回す
        void PlaceReturnArrow(float yaw, float r, bool left)
        {
            int k = left ? 0 : 1;
            BonusArrow a = null;
            foreach (var c in returnArrowPool[k])
                if (c != null && !c.Showing && !c.gameObject.activeSelf) { a = c; break; }
            if (a == null)
            {
                var template = returnArrows[k];
                if (template == null) return;
                a = Instantiate(template, template.transform.parent);
                a.name = template.name;
                a.HideImmediate();
                returnArrowPool[k].Add(a);
            }
            a.Place(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * r);
            a.Show();
            returnArrowsShown.Add(a);
        }

        void UpdateReturnArrows()
        {
            if (returnArrowLeft <= 0f) return;
            returnArrowLeft -= Time.deltaTime;
            if (returnArrowLeft <= 0f || IsFever) HideReturnArrows(); // (時間切れ・次のフィーバー)
        }

        /// <summary>床の矢印 (青い「下を見て」・赤い左右・緑のメイン屋台へ) を全部しまう (別の世界・チャレンジへ行く時など、邪魔になりそうな時)</summary>
        void HideAllArrows()
        {
            sideArrowLeft = 0f;
            if (sideArrows != null) foreach (var a in sideArrows) if (a != null) a.Hide();
            if (returnArrowLeft > 0f || returnArrowRoutine != null || returnArrowsShown.Count > 0) HideReturnArrows();
            if (bonusStage != null && bonusStage.ArrowsActive) bonusStage.HideArrows();
        }

        void HideReturnArrows()
        {
            returnArrowLeft = 0f;
            if (returnArrowRoutine != null) { StopCoroutine(returnArrowRoutine); returnArrowRoutine = null; }
            foreach (var a in returnArrowsShown) if (a != null) a.Hide();
            returnArrowsShown.Clear();
        }

        float RingRadius(int n)
        {
            if (n < 2) return stallRadius;
            float half = Mathf.PI / n, sin = Mathf.Sin(half), cos = Mathf.Cos(half);
            float margin = stallGap * 0.5f;
            // 点 (横 x, 中心からの距離 R - depth) と扇の境目の線との距離 = (R - depth)·sin − x·cos
            float roof = (StallHalfWidth * cos + margin) / sin + StallFrontDepth;
            float lantern = (LanternX * cos + LanternRadius + margin) / sin + LanternDepth;
            float hatch = (HatchHalfWidth * cos + margin) / sin + HatchFrontDepth; // 隣どうしのハッチも重ならないように
            return Mathf.Max(stallRadius, Mathf.Max(Mathf.Max(roof, lantern), hatch));
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
                // デバッグモードは決まった順 (屋台ごとの的の種類が、いつも同じ向きに並ぶように)
                int j = DebugMode ? 0 : Random.Range(0, pool.Count);
                activeFeverStalls.Insert(DebugMode ? activeFeverStalls.Count : Random.Range(0, activeFeverStalls.Count + 1), pool[j]);
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
            if (added.Count > 0) ShowSideArrows(); // 屋台が増えた: 左右を見るように、メイン屋台の前に左右の矢印を出す
        }

        void EndFever()
        {
            IsFever = false;
            if (bonusStage != null) bonusStage.Close();
            ShowReturnArrows(); // (時間切れ・点数を取りきったラウンドの終わりでも) 周りの屋台がしまわれたら、それぞれの屋台があった所からメイン屋台の方を指す矢印を出す
            foreach (var s in feverStalls) s.Sink(stallMoveDuration);
            activeFeverStalls.Clear();
            mainStall.Slide(mainHomePos, mainHomeRot, stallMoveDuration); // メイン屋台は最初の位置へ戻る
            player.SetDualWield(false);
            if (State == GameState.Playing) ShowMessage("FEVER END", 1.5f, Color.white);
        }
    }
}
