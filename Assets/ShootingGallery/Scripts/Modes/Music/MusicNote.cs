using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 五線譜を流れてくる音符の的 (MusicStaff が動かす)。種類で点数が違う: 4分音符 / 8分音符 / 2分音符。金色の音符は高得点。
    /// 当たると縮んで消え (Target の shrinkOnHit)、その線 / 間の高さの音が鳴る (ConcertHall)
    /// </summary>
    public class MusicNote : MonoBehaviour
    {
        [Tooltip("当たり判定 (kind = MusicNote, manualControl, shrinkOnHit)")]
        public Target target;
        public int basePoints = 100;
        [Tooltip("金色の音符の時だけ出す光の輪 / 足す点数")]
        public GameObject goldMark;
        public int goldBonus = 300;
        [Tooltip("点数の文字")]
        public TextMesh label;
        [Tooltip("色を付け替える部品 (玉・棒・旗) と、出すたびにランダムに選ぶ色")]
        public Renderer[] bodyRenderers;
        public Material[] palette;

        // MusicStaff が使う
        [System.NonSerialized] public float u, speed;
        [System.NonSerialized] public int slot;
        [System.NonSerialized] public bool inUse, armed, popped;
        [System.NonSerialized] public float popTime;

        public int Points { get; private set; }

        /// <summary>出す (gold なら金色の高得点の音符)</summary>
        public void Show(bool gold)
        {
            Points = basePoints + (gold ? goldBonus : 0);
            if (goldMark != null) goldMark.SetActive(gold);
            if (label != null) label.text = Points.ToString();
            if (palette != null && palette.Length > 0 && bodyRenderers != null)
            {
                var mat = palette[Random.Range(0, palette.Length)];
                foreach (var r in bodyRenderers) if (r != null) r.sharedMaterial = mat;
            }
            gameObject.SetActive(true);
            inUse = true;
            armed = false;
            popped = false;
            target.points = Points;
            target.SetAvailable(true);
            target.Raise(9999f); // 縮んだ状態からふくらんで出る
        }

        public void Hide()
        {
            inUse = false;
            target.SetAvailable(false);
            gameObject.SetActive(false);
        }
    }
}
