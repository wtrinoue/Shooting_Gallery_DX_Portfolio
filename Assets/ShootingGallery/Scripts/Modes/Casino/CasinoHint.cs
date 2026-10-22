using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// カジノのゲームの上に浮かぶ「SHOOT!」の目印 (光る文字と、下を指してぴょこぴょこ弾む矢印)。どこを撃てばいいか分かるように。
    /// 撃てる時だけ出す (スロットのリールやルーレットのボールが回っている間は隠す)
    /// </summary>
    public class CasinoHint : MonoBehaviour
    {
        public Target target;
        public SlotMachine slot;
        public BallRoulette roulette;
        [Tooltip("出し入れする見た目 (文字と矢印) / 弾む矢印")]
        public GameObject visual;
        public Transform arrow;

        Vector3 arrowHome;
        float phase;

        void Awake()
        {
            if (arrow != null) arrowHome = arrow.localPosition;
            phase = Random.value * 6f;
        }

        void Update()
        {
            bool ready = target != null && target.IsUp && (slot == null || !slot.IsSpinning) && (roulette == null || roulette.IsReady);
            if (visual != null && visual.activeSelf != ready) visual.SetActive(ready);
            if (!ready || arrow == null) return;
            float b = Mathf.Abs(Mathf.Sin((Time.time + phase) * 4f)); // ぴょこぴょこ
            arrow.localPosition = arrowHome + Vector3.down * (0.12f * b);
        }
    }
}
