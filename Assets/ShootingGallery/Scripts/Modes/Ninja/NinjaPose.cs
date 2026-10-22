using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 忍者のパネルのポーズ。腕は肩、脚は腰を軸に回り、体ごと低く構えることもできる。出てくるたびに Randomize でポーズが変わる (NinjaTrap)。
    /// 腕・脚の角度は Z まわり (度)。0 でまっすぐ下。左の腕・脚はマイナスで外へ、右はプラスで外へ開く
    /// </summary>
    public class NinjaPose : MonoBehaviour
    {
        [Tooltip("体全体 (低く構える時に下げる)。腕と脚はこの子")]
        public Transform figure;
        public Transform armL, armR, legL, legR;
        [Tooltip("右手の刀 / 左手の手裏剣 (ポーズによって持っていない)")]
        public GameObject katana, shuriken;

        // (左腕, 右腕, 左脚, 右脚, 体を下げる量, 刀, 手裏剣)
        static readonly (float armL, float armR, float legL, float legR, float drop, bool katana, bool shuriken)[] Poses =
        {
            (-60f, 150f, -6f, 6f, 0f, true, true),        // 構え: 刀を振りかぶり、手裏剣を持つ手を横へ
            (55f, -55f, -4f, 4f, 0f, false, false),       // 印を結ぶ: 両手を胸の前で合わせる
            (-35f, 120f, -18f, 24f, 0f, false, true),     // 手裏剣を投げる: 右手を振り上げ、足を踏み出す
            (-135f, 135f, -28f, 28f, -0.05f, true, false),// 跳ぶ: 両手を上げ、足を大きく開く
            (-25f, 75f, -38f, 38f, 0.11f, true, true),    // 低く構える: 腰を落とし、刀を前へ
        };

        Vector3 figureHome;
        bool captured;

        void Capture()
        {
            if (captured || figure == null) return;
            figureHome = figure.localPosition;
            captured = true;
        }

        /// <summary>ポーズをランダムに選ぶ</summary>
        public void Randomize() => Apply(Random.Range(0, Poses.Length));

        public void Apply(int index)
        {
            Capture();
            var p = Poses[Mathf.Clamp(index, 0, Poses.Length - 1)];
            if (armL != null) armL.localRotation = Quaternion.Euler(0f, 0f, p.armL);
            if (armR != null) armR.localRotation = Quaternion.Euler(0f, 0f, p.armR);
            if (legL != null) legL.localRotation = Quaternion.Euler(0f, 0f, p.legL);
            if (legR != null) legR.localRotation = Quaternion.Euler(0f, 0f, p.legR);
            if (figure != null) figure.localPosition = figureHome + Vector3.down * p.drop;
            if (katana != null) katana.SetActive(p.katana);
            if (shuriken != null) shuriken.SetActive(p.shuriken);
        }
    }
}
