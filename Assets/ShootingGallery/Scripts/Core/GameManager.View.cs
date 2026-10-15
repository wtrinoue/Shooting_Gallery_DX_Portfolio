using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// GameManager のうち、視点を整える処理 (独立した見張り役)。
    /// 発掘・潜水艦・音楽モードはプレイヤーを上下に動かし、怪獣モードは世界を縮めて戻す。不具合や途中で止められたことで
    /// 視点が元に戻りきらなかった時のために、どのモードも動いていない (片付けも終わった) のに視点がずれたままなら、なめらかに元に戻す。
    /// プレイヤーを動かすモードを始める前と、ラウンドの始まり・タイトルに戻る時にも、念のため整える
    /// </summary>
    public partial class GameManager
    {
        [Header("視点を整える")]
        [Tooltip("どのモードでもないのに視点がずれたまま、この秒数たったら元に戻す")]
        public float viewGuardDelay = 1f;

        float viewOffTime;

        /// <summary>視点を整える (いつもの立ち位置・大きさ・視点の高さに戻す)。instant ならその場で、でなければなめらかに</summary>
        public void ResetView(bool instant = false)
        {
            if (player == null) return;
            viewOffTime = 0f;
            if (player.IsViewOff) player.ResetView(instant);
        }

        // 毎フレーム: どのモードも動いていない・始まる予定もない・片付けも終わったのに視点がずれていたら、少し待ってから元に戻す
        void UpdateViewGuard()
        {
            if (player == null) return;
            bool idle = !ChallengeActive && pendingModeStart == null && Time.time > stallsReturnUntil + 0.5f && !player.IsRestoringView;
            if (!idle || !player.IsViewOff)
            {
                viewOffTime = 0f;
                return;
            }
            viewOffTime += Time.deltaTime;
            if (viewOffTime < viewGuardDelay) return;
            Debug.LogWarning("[GameManager] 視点がずれたままだったので、元に戻しました");
            ResetView();
        }
    }
}
