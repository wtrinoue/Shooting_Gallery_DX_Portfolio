using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// カジノの壁に飾られたルーレットの的。扇形に区切られた円盤がゆっくり回り、扇ごとに点数が違う。
    /// ダーツが当たった所の扇の点数が入る (ダーツは円盤に刺さって一緒に回る)。何回当ててもよい。
    /// </summary>
    public class RouletteWheel : MonoBehaviour
    {
        [Tooltip("ルーレットの当たり判定 (kind = Roulette, stayUpOnHit。visual は空のオブジェクト)")]
        public Target target;
        [Tooltip("回る円盤 (ローカル Z まわりに回る。扇は 12 時の位置から時計回りに sectorPoints の順)")]
        public Transform spinner;
        [Tooltip("扇ごとの点数 (12 時の位置から時計回り)")]
        public int[] sectorPoints = { 10, 100, 20, 200, 30, 500, 40, 300 };
        [Tooltip("真ん中の小さな円の半径 (m)。ここに当たると一番高い点数")]
        public float bullRadius = 0.12f;
        public int bullPoints = 1000;
        [Tooltip("回る速さ (度/秒。マイナスで逆回り)")]
        public float speed = 40f;

        void Awake() => target.SetAvailable(false);

        public void Activate()
        {
            target.SetAvailable(true);
            target.Raise(9999f);
        }

        public void Deactivate() => target.SetAvailable(false);

        void Update() => spinner.Rotate(0f, 0f, speed * Time.deltaTime, Space.Self);

        /// <summary>当たった所の点数 (ワールド座標)。真ん中なら bullPoints、それ以外はその扇の点数</summary>
        public int PointsAt(Vector3 worldPoint)
        {
            Vector3 p = spinner.InverseTransformPoint(worldPoint);
            Vector2 flat = new Vector2(p.x * spinner.lossyScale.x, p.y * spinner.lossyScale.y);
            if (flat.magnitude <= bullRadius) return bullPoints;
            // 12 時 (+Y) から時計回りの角度。円盤の表は -Z (プレイヤー側) で、プレイヤーからはローカル +X が右に見える
            float a = Mathf.Repeat(Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg, 360f);
            int n = Mathf.Max(1, sectorPoints.Length);
            return sectorPoints[Mathf.Clamp(Mathf.FloorToInt(a / (360f / n)), 0, n - 1)];
        }
    }
}
