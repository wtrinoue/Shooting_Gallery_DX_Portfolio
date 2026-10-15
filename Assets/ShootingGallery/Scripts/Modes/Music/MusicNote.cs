using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 五線譜を流れてくる記号の的 (MusicStaff が動かす)。音符6種・休符4種・フェルマータ・タイや連桁でつないだ音符で、種類で点数が違う (GalleryBuilder.NoteKindPoints)。金色の物は高得点。
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
        [Tooltip("五線の決まった高さに置く記号 (休符・フェルマータ) か。その時の高さ = 真ん中の線から上へ (m)。音符はランダムな線・間に乗る")]
        public bool hasFixedHeight;
        public float fixedHeight;
        [Tooltip("決まった高さの記号を撃った時に鳴らす音の高さ (五線の線・間の番号。下の線 = 0)")]
        public int fixedSlot = 4;
        [Tooltip("ふつうの音符より横に長いぶんの半分 (m)。タイでつないだ音符など。前後の音符との間をそのぶん余計にあける")]
        public float extraHalfWidth;
        [Tooltip("記号の種類 (GalleryBuilder の NoteQuarter 〜。同じ種類の複製を探すのに使う)")]
        public int kind;
        [Tooltip("長さ = 32分音符が何個分か (4分音符 = 8)。小節の抽選に使う (MeasureStock)。0 なら長さの抽選には入れない")]
        public int length32 = 8;
        /// <summary>小節の中での扱い: 長さのある音符・休符 / 全休符 (拍子ぶんの長さ。1小節にそれだけ) / フェルマータ (拍を持たない。小節の最後に付く)</summary>
        public enum MeasureRole { Timed, WholeRest, Fermata }
        [Tooltip("小節の中での扱い (Timed = 長さで抽選 / WholeRest = 小節の初めに確率で、出たらその小節はそれだけ / Fermata = 小節の最後に確率で付く)")]
        public MeasureRole role;

        // MusicStaff が使う
        [System.NonSerialized] public float u, speed;
        [System.NonSerialized] public int slot;
        [System.NonSerialized] public bool inUse, armed, popped, queued; // queued = 小節に並べる予約済み (まだ流していない)
        [System.NonSerialized] public float popTime;

        public int Points { get; private set; }

        /// <summary>出す (gold なら金色の高得点の音符)</summary>
        public void Show(bool gold)
        {
            Points = basePoints + (gold ? goldBonus : 0);
            if (goldMark != null) goldMark.SetActive(gold);
            if (label != null) label.text = PointText.Of(Points);
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
            queued = false;
            target.SetAvailable(false);
            gameObject.SetActive(false);
        }
    }
}
