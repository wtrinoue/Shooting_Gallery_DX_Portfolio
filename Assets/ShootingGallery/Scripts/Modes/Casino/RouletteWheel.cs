using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// カジノの壁に飾られたダーツの的。dartboard = true なら本物と同じ数え方のダーツボード
    /// (20 の数字・ダブル (2倍)・トリプル (3倍)・ブル)。false なら扇形に区切られた円盤がゆっくり回るルーレット (扇ごとの点数)。
    /// ダーツが当たった所の点数が入る (ダーツは円盤に刺さる)。何回当ててもよい。
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

        [Header("ダーツボード")]
        [Tooltip("ON なら本物のダーツボードの数え方 (数字 × 1 / 2 / 3 倍、ブル)")]
        public bool dartboard;
        [Tooltip("ダブルの輪の外側の半径 (m)。ここより外 (数字の輪) は点数なし")]
        public float boardRadius = 0.85f;
        [Tooltip("輪の境目 (boardRadius に対する割合): 真ん中のブル / 外のブル / トリプルの内・外 / ダブルの内・外")]
        public float[] rings = { 0.045f, 0.1f, 0.53f, 0.6f, 0.9f, 1f };
        [Tooltip("数字の並び (12 時から時計回り)")]
        public int[] numbers = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };
        [Tooltip("数字 1 あたりの点数 (シングル。ダブルは 2倍、トリプルは 3倍)")]
        public int pointsPerNumber = 10;
        public int outerBullPoints = 500;

        /// <summary>最後に当たった所の呼び名 (例: "T20" / "D16" / "BULL" / "OUT")。演出のメッセージに使う</summary>
        public string LastHit { get; private set; } = "";

        void Awake() => target.SetAvailable(false);

        public void Activate()
        {
            target.SetAvailable(true);
            target.Raise(9999f);
        }

        public void Deactivate() => target.SetAvailable(false);

        void Update() => spinner.Rotate(0f, 0f, speed * Time.deltaTime, Space.Self);

        // ダーツボードの点数: 真ん中のブル / 外のブル / 数字 × (シングル 1・トリプル 3・ダブル 2)。数字の輪より外は 0
        int DartPoints(Vector3 local, float r)
        {
            if (r <= rings[0]) { LastHit = "BULL"; return bullPoints; }
            if (r <= rings[1]) { LastHit = "BULL"; return outerBullPoints; }
            if (r > rings[5]) { LastHit = "OUT"; return 0; }
            int n = Mathf.Max(1, numbers.Length);
            float step = 360f / n;
            float a = Mathf.Repeat(Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg + step / 2f, 360f); // 12 時の数字が真上の真ん中
            int number = numbers[Mathf.Clamp(Mathf.FloorToInt(a / step), 0, n - 1)];
            int mult = r >= rings[2] && r <= rings[3] ? 3 : r >= rings[4] ? 2 : 1;
            LastHit = (mult == 3 ? "T" : mult == 2 ? "D" : "") + number;
            return number * mult * pointsPerNumber;
        }

        /// <summary>当たった所の点数 (ワールド座標)。ダーツボードなら数字と輪で、ルーレットなら真ん中は bullPoints、それ以外はその扇の点数</summary>
        public int PointsAt(Vector3 worldPoint)
        {
            Vector3 p = spinner.InverseTransformPoint(worldPoint);
            Vector2 flat = new Vector2(p.x * spinner.lossyScale.x, p.y * spinner.lossyScale.y);
            if (dartboard) return DartPoints(p, flat.magnitude / Mathf.Max(0.01f, boardRadius));
            if (flat.magnitude <= bullRadius) return bullPoints;
            // 12 時 (+Y) から時計回りの角度。円盤の表は -Z (プレイヤー側) で、プレイヤーからはローカル +X が右に見える
            float a = Mathf.Repeat(Mathf.Atan2(p.x, p.y) * Mathf.Rad2Deg, 360f);
            int n = Mathf.Max(1, sectorPoints.Length);
            return sectorPoints[Mathf.Clamp(Mathf.FloorToInt(a / (360f / n)), 0, n - 1)];
        }
    }
}
