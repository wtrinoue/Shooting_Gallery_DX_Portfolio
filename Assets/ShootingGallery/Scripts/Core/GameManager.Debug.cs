using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>GameManager のうち、エンドレスモード (B ボタン長押し) とデバッグモード (X ボタン長押し)。</summary>
    public partial class GameManager
    {
        /// <summary>
        /// エンドレスモードを始める (B ボタンの長押しで呼ばれる)。時間切れがなく、A ボタンの長押しで最初に戻るまで続く。
        /// タイトルからでもラウンドの途中からでも、点数 0 から始め直す。
        /// </summary>
        public void StartEndless()
        {
            StopForNewRound();
            StartRound(true);
        }

        /// <summary>
        /// デバッグモードを始める (X ボタンの長押しで呼ばれる)。時間切れのないエンドレスで、すぐにフィーバーと同じ6軒の輪を出し、
        /// そのまま終わらせない (ステージも上がらない)。輪の屋台はメイン屋台と同じギミックなしの屋台 (debugStalls) で、
        /// 屋台ごとに武器・特別な的・チャレンジ・別の世界へ行くモードの的が種類別に並ぶ (GameManager.DebugKindsByStall)。
        /// </summary>
        public void StartDebug()
        {
            StopForNewRound();
            StartRound(true, true);
            StartFever();
            ShowMessage("DEBUG MODE\nALL TARGETS!  HOLD A : QUIT", 3f, new Color(0.6f, 1f, 0.4f));
        }

        // エンドレス / デバッグを始める前に、今のラウンドやタイトルへ戻る待ちを止める
        void StopForNewRound()
        {
            if (backToTitle != null)
            {
                StopCoroutine(backToTitle);
                backToTitle = null;
            }
            if (State == GameState.Playing)
            {
                State = GameState.Result; // 終わらせている間は Playing 扱いにしない
                StopEverything();
            }
            player.CalibrateHeight(); // START を撃った時と同じく、180cm の人の視点に合わせる
        }

        // ---- デバッグモード ----

        /// <summary>
        /// デバッグモードで海戦・空中・忍者の的を撃った時: 他のモード・チャレンジ・豪華タイム・ボーナスステージの途中なら、
        /// それを終わらせて、片付けが済んでから start を呼ぶ (true を返す。呼んだ側はそのまま戻る)。
        /// 普段のゲームではこれらの的はそもそも出ない時なので何もしない (false)。
        /// </summary>
        bool DebugClearFor(System.Action start)
        {
            if (!DebugMode || State != GameState.Playing) return false;
            float wait = EndAllForNewMode(); // 普段のゲームと同じ片付け (GameManager.ModeQueue.cs)
            if (wait <= 0f) return false;
            StartCoroutine(StartAfter(wait + 0.2f, start));
            return true;
        }

        IEnumerator StartAfter(float delay, System.Action start)
        {
            yield return new WaitForSeconds(delay);
            if (State == GameState.Playing && DebugMode) start();
        }
    }
}
