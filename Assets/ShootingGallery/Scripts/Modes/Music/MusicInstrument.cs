using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 音楽モードの楽器 (演奏者はいない。楽器だけが曲の拍に合わせてリズミカルに揺れる)。
    ///   pivot  : 拍ごとに弾み、2拍で左右 (swayAxis のまわり) に揺れる
    ///   bow    : 弓が弦の上を行ったり来たりする (バイオリン・チェロなど)
    ///   slider : トロンボーンのスライドが伸び縮みする
    ///   beaters: マレット・スティックが拍に合わせて振り下ろされる (ティンパニ・木琴・太鼓・ピアノのふた)
    ///   clappers: シンバルが小節の頭で打ち合わされる (2枚)
    /// ソロの楽器 (ConcertHall が選ぶ) は、少し浮き上がって大きく揺れ、まわりに光の粒が回り、点数の札が出る。
    /// 撃たれるとぴょんと飛び跳ねる (ソロの時は高く、くるっと回る)
    /// </summary>
    public class MusicInstrument : MonoBehaviour
    {
        public string displayName = "VIOLIN";
        [Tooltip("当たり判定 (kind = MusicInstrument, stayUpOnHit)。点数は ConcertHall が当たった時に決める")]
        public Target hitTarget;
        [Tooltip("ソロの時に撃つと入る点数")]
        public int points = 300;
        [Tooltip("音色と、撃った時に鳴らす高さの目安 (MIDI の番号。大きい楽器ほど低い)")]
        public ConcertMusic.Voice voice = ConcertMusic.Voice.Strings;
        public int midi = 60;

        [Header("揺れ")]
        public Transform pivot;
        public Vector3 swayAxis = Vector3.forward;
        public float swayDeg = 5f;
        [Tooltip("拍ごとに弾む高さ (m)")]
        public float bob = 0.025f;
        [Tooltip("左右に1往復する拍の数")]
        public float swayBeats = 2f;
        [Tooltip("揺れのずれ (拍)。隣の楽器と少しずつずらす")]
        public float phaseBeats;
        [Tooltip("スポットライトを当てる高さ (根元から, m)")]
        public float aimHeight = 1.2f;

        [Header("動く部品")]
        public Transform bow;
        public Vector3 bowStroke;
        public Transform slider;
        public Vector3 slideOffset;
        public float slideBeats = 2f;
        public Transform[] beaters;
        public Vector3 beaterAxis = Vector3.right;
        public float beaterDeg = 30f;
        [Tooltip("何拍ごとに打つか (0.5 なら8分音符ごと)。beaters の2本目以降は半分ずらす")]
        public float beaterEvery = 1f;
        public Transform[] clappers;
        public Vector3 clapOffset;

        [Header("ソロ")]
        [Tooltip("ソロの間だけ出す物 (光の粒・点数の札)")]
        public GameObject soloMark;
        public TextMesh soloLabel;
        public float soloLift = 0.3f;

        public bool IsSolo { get; private set; }
        public Vector3 AimPoint => (pivot != null ? pivot.position : transform.position) + Vector3.up * aimHeight * 0.5f;

        ConcertHall hall;
        Vector3 pivotPos, bowPos, slidePos;
        Quaternion pivotRot;
        Quaternion[] beaterRot;
        Vector3[] clapPos;
        float lift, jumpStart = -10f, jumpHeight, jumpSpin;

        public void Init(ConcertHall owner)
        {
            hall = owner;
            if (pivot != null) { pivotPos = pivot.localPosition; pivotRot = pivot.localRotation; }
            if (bow != null) bowPos = bow.localPosition;
            if (slider != null) slidePos = slider.localPosition;
            beaterRot = new Quaternion[beaters != null ? beaters.Length : 0];
            for (int i = 0; i < beaterRot.Length; i++) beaterRot[i] = beaters[i].localRotation;
            clapPos = new Vector3[clappers != null ? clappers.Length : 0];
            for (int i = 0; i < clapPos.Length; i++) clapPos[i] = clappers[i].localPosition;
            SetSolo(false);
        }

        public void SetSolo(bool on)
        {
            IsSolo = on;
            if (soloMark != null) soloMark.SetActive(on);
            if (soloLabel != null) soloLabel.text = points.ToString();
        }

        /// <summary>撃たれた: ぴょんと跳ねる (big = ソロの時。高く跳んで、くるっと1回転)</summary>
        public void Jump(bool big)
        {
            jumpStart = Time.time;
            jumpHeight = big ? 0.55f : 0.18f;
            jumpSpin = big ? 360f : 0f;
        }

        void Update()
        {
            if (hall == null || pivot == null) return;
            float beat = hall.Beat;
            float groove = hall.Groove; // 演奏中は 1、始まる前・終わった後は 0
            float b = beat + phaseBeats;
            float amp = Mathf.Lerp(0.15f, IsSolo ? 2.4f : 1f, groove);
            float pulse = Mathf.Exp(-Frac(b) * 6f) * groove; // 拍の頭で 1、すぐに減る

            lift = Mathf.MoveTowards(lift, IsSolo ? soloLift : 0f, Time.deltaTime * 0.8f);
            float jumpY = 0f, spin = 0f;
            float kj = (Time.time - jumpStart) / 0.55f;
            if (kj < 1f)
            {
                jumpY = jumpHeight * 4f * kj * (1f - kj);
                spin = jumpSpin * kj * kj * (3f - 2f * kj);
            }

            pivot.localPosition = pivotPos + Vector3.up * (bob * pulse * (IsSolo ? 2f : 1f) + lift + jumpY);
            float sway = Mathf.Sin(b / Mathf.Max(0.25f, swayBeats) * Mathf.PI * 2f) * swayDeg * amp;
            pivot.localRotation = Quaternion.Euler(0f, spin, 0f) * pivotRot * Quaternion.AngleAxis(sway, swayAxis);

            if (bow != null) bow.localPosition = bowPos + bowStroke * Mathf.Sin(b * Mathf.PI) * Mathf.Max(groove, 0.1f);
            if (slider != null) slider.localPosition = slidePos + slideOffset * (0.5f - 0.5f * Mathf.Cos(b / Mathf.Max(0.25f, slideBeats) * Mathf.PI * 2f)) * groove;
            for (int i = 0; i < beaterRot.Length; i++)
            {
                // 打った瞬間 (拍の頭) は 0 度、すぐに振り上がっていく。2本目以降は半分ずらして交互に打つ
                float every = Mathf.Max(0.25f, beaterEvery);
                float f = Frac((b + (i % 2) * every * 0.5f) / every);
                float up = (1f - Mathf.Exp(-f * 7f)) * groove;
                beaters[i].localRotation = beaterRot[i] * Quaternion.AngleAxis(beaterDeg * up, beaterAxis);
            }
            if (clapPos.Length >= 2)
            {
                // 小節の頭で打ち合わせ、すぐに離れる
                float open = (1f - Mathf.Exp(-Frac(b / 4f) * 4f * 5f)) * groove;
                clappers[0].localPosition = clapPos[0] + clapOffset * open;
                clappers[1].localPosition = clapPos[1] - clapOffset * open;
            }
        }

        static float Frac(float x) => x - Mathf.Floor(x);
    }
}
