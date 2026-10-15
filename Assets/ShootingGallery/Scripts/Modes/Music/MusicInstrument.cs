using System.Collections.Generic;
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
        [Tooltip("ソロの間、楽器のまわりを回る輪の五線譜と、その上で拍に合わせて跳ねる音符")]
        public Transform soloStaff;
        public Transform[] soloNotes;
        [Tooltip("ソロの間、楽器の色を拍ごとに入れ替える蛍光色 (ほかの楽器よりずっと目立つように)")]
        public Material[] soloMaterials;
        [Tooltip("当たり判定の箱と、ソロの間に大きくする倍率 (ソロでない時は元の大きさ。隣のソロの楽器をさえぎらないように)")]
        public Transform hitBox;
        public float soloHitScale = 1.6f;

        public bool IsSolo { get; private set; }
        public Vector3 AimPoint => (pivot != null ? pivot.position : transform.position) + Vector3.up * aimHeight * 0.5f * transform.lossyScale.y; // 根元ごと拡大しているぶんも掛ける

        ConcertHall hall;
        Vector3 pivotPos, bowPos, slidePos;
        Quaternion pivotRot;
        Quaternion[] beaterRot;
        Vector3[] clapPos;
        float lift, jumpStart = -10f, jumpHeight, jumpSpin;
        Renderer[] bodyRenderers;   // 楽器の見た目 (pivot の下)
        Material[] bodyMaterials;   // その元の色
        int neonIndex = -1;
        Vector3 hitBase;
        Collider hitCollider;

        /// <summary>当たり判定を出す / 消す (ソロの楽器のまわりや、ソロの楽器への視線をふさぐ楽器は消す。ConcertHall から)</summary>
        public void SetHittable(bool on)
        {
            if (hitCollider != null) hitCollider.enabled = on;
        }

        public void Init(ConcertHall owner)
        {
            hall = owner;
            MergeMeshes();
            if (pivot != null)
            {
                pivotPos = pivot.localPosition;
                pivotRot = pivot.localRotation;
                var all = pivot.GetComponentsInChildren<Renderer>(true);
                var list = new List<Renderer>(all.Length);
                foreach (var r in all) if (r.enabled) list.Add(r); // まとめて消した元の部品は除く (まとめた物が入る)
                bodyRenderers = list.ToArray();
                bodyMaterials = new Material[bodyRenderers.Length];
                for (int i = 0; i < bodyRenderers.Length; i++) bodyMaterials[i] = bodyRenderers[i].sharedMaterial;
            }
            if (hitBox != null)
            {
                hitBase = hitBox.localScale;
                hitCollider = hitBox.GetComponent<Collider>();
            }
            if (bow != null) bowPos = bow.localPosition;
            if (slider != null) slidePos = slider.localPosition;
            beaterRot = new Quaternion[beaters != null ? beaters.Length : 0];
            for (int i = 0; i < beaterRot.Length; i++) beaterRot[i] = beaters[i].localRotation;
            clapPos = new Vector3[clappers != null ? clappers.Length : 0];
            for (int i = 0; i < clapPos.Length; i++) clapPos[i] = clappers[i].localPosition;
            SetSolo(false);
        }

        bool merged;
        /// <summary>見た目を1つにまとめた部品の数 (確かめる用)</summary>
        public int MergedParts { get; private set; }

        // 動かない部品の見た目を材質ごとに1つにまとめる (楽器は40個ほどあり、部品ごとに描くと重いので)。
        // 1. Pivot の下 (楽器の本体。拍に合わせて Pivot ごと揺れる): 別に動く部品 (弓・スライド・マレット・シンバル) とソロの印・当たり判定の箱はそのまま
        // 2. 根元 (椅子・足元の光の輪): 撃たれると揺れる Wobble の下とソロの印はそのまま
        void MergeMeshes()
        {
            if (merged) return;
            merged = true;
            var exclude = new HashSet<Transform>();
            void Add(Transform t) { if (t != null) exclude.Add(t); }
            Add(bow); Add(slider); Add(soloMark != null ? soloMark.transform : null); Add(soloStaff); Add(hitBox);
            if (beaters != null) foreach (var t in beaters) Add(t);
            if (clappers != null) foreach (var t in clappers) Add(t);
            if (soloNotes != null) foreach (var t in soloNotes) Add(t);
            if (pivot != null) MergedParts += MeshMerger.Merge(pivot, exclude);

            var wobble = hitTarget != null && hitTarget.visual != null ? hitTarget.visual : pivot != null ? pivot.parent : null;
            exclude.Clear();
            Add(wobble); Add(pivot); Add(soloMark != null ? soloMark.transform : null);
            MergedParts += MeshMerger.Merge(transform, exclude);
        }

        public void SetSolo(bool on)
        {
            IsSolo = on;
            if (soloMark != null) soloMark.SetActive(on);
            if (soloLabel != null) soloLabel.text = PointText.Of(points);
            if (hitBox != null) hitBox.localScale = on ? hitBase * soloHitScale : hitBase;
            if (!on) SetNeon(-1);
        }

        // 楽器の色を蛍光色 index にする (-1 で元の色に戻す)
        void SetNeon(int index)
        {
            if (index == neonIndex || bodyRenderers == null) return;
            neonIndex = index;
            for (int i = 0; i < bodyRenderers.Length; i++)
                if (bodyRenderers[i] != null)
                    bodyRenderers[i].sharedMaterial = index < 0 || soloMaterials == null || soloMaterials.Length == 0
                        ? bodyMaterials[i]
                        : soloMaterials[index % soloMaterials.Length];
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
            if (float.IsNaN(beat) || float.IsInfinity(beat)) return; // おかしな拍で位置が NaN になると、楽器が消えてしまう
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
            // ソロの間は、拍ごとに蛍光色が入れ替わる (黄 → ピンク → 水色 …)
            if (IsSolo && soloMaterials != null && soloMaterials.Length > 0) SetNeon(Mathf.FloorToInt(beat) % soloMaterials.Length);
            if (IsSolo && soloStaff != null)
            {
                // 輪の五線譜: 拍に合わせて回り (1小節で 1/4 周)、拍の頭でふわっとふくらむ。少し傾けて、立体の輪に見せる
                soloStaff.localRotation = Quaternion.Euler(12f, beat * 22.5f, 0f);
                soloStaff.localScale = Vector3.one * (1f + 0.08f * pulse);
                // 輪の上の音符: 順番に、自分の拍で跳ね上がる (音を出している感じ)
                if (soloNotes != null)
                    for (int i = 0; i < soloNotes.Length; i++)
                    {
                        if (soloNotes[i] == null) continue;
                        float f = Frac(b * 0.5f + i / (float)soloNotes.Length);
                        float hop = Mathf.Sin(Mathf.Clamp01(f * 2f) * Mathf.PI) * 0.22f;
                        soloNotes[i].localPosition = new Vector3(soloNotes[i].localPosition.x, hop, soloNotes[i].localPosition.z);
                    }
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
