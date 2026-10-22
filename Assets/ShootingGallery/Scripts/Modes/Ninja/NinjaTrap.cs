using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 忍者の隠れ場所の仕掛け (畳 / ふすま / 天井板)。忍者のパネル (target) が出ている間だけ仕掛けが開き、
    /// 忍者は隠れていた所 (畳の下・ふすまの奥・天井裏) から hideOffset の分だけ動いて出てくる。
    /// 倒されたり引っ込んだりすると、忍者は隠れ場所へ戻り、仕掛けが閉じる。
    /// 出てくる時と消える時は、ぼわっと煙が出る (どろろん)。出てくるたびにポーズが変わる (NinjaPose)
    /// </summary>
    public class NinjaTrap : MonoBehaviour
    {
        public Target target;
        [Tooltip("開く板 (畳・ふすま・天井板)。閉じた状態で置いておく")]
        public Transform flap;
        [Tooltip("開いた時に板が動く量 (板の親のローカル座標)。ふすまは横へ滑る")]
        public Vector3 flapOpenOffset;
        [Tooltip("開いた時に板が回る角度 (板のローカル)。畳は起き上がり、天井板は下へ開く")]
        public Vector3 flapOpenEuler;
        [Tooltip("隠れている時の忍者の位置のずれ (忍者の親のローカル座標)。出てくる時はここから元の位置へ動く")]
        public Vector3 hideOffset;
        [Tooltip("開く / 閉じるのにかかる秒数")]
        public float openTime = 0.2f;
        [Tooltip("出てくる時と消える時の煙 (半透明の材質。なければ出さない) / 出てくるたびに変えるポーズ")]
        public Material smoke;
        public NinjaPose pose;

        Vector3 flapPos, targetPos;
        Quaternion flapRot;
        float k;
        bool wasVisible;

        void Awake()
        {
            if (flap != null)
            {
                flapPos = flap.localPosition;
                flapRot = flap.localRotation;
            }
            targetPos = target.transform.localPosition;
            Apply(0f);
        }

        void Update()
        {
            bool visible = target.IsVisible;
            if (visible != wasVisible)
            {
                wasVisible = visible;
                if (visible && pose != null) pose.Randomize(); // (まだ隠れ場所の中なので、ポーズが変わる瞬間は見えない)
                Poof(visible);
            }
            float to = visible ? 1f : 0f;
            if (Mathf.Approximately(k, to)) return;
            k = Mathf.MoveTowards(k, to, Time.deltaTime / Mathf.Max(0.01f, openTime));
            Apply(k);
        }

        // どろろん: 忍者の立つ所のまわりに、煙がぼわっと広がる
        void Poof(bool appear)
        {
            if (smoke == null) return;
            var t = target.transform;
            Vector3 stand = t.parent != null ? t.parent.TransformPoint(targetPos) : targetPos;
            Vector3 up = t.rotation * Vector3.up; // (天井からぶら下がる忍者は下向き)
            for (int i = 0; i < 5; i++)
                Explosion.Spawn(stand + up * Random.Range(0.3f, 1.3f) + Random.insideUnitSphere * 0.3f, Random.Range(0.55f, 0.9f), smoke);
            Sfx.Play("ghostpop", appear ? 0.45f : 0.3f);
        }

        void Apply(float x)
        {
            float e = x * x * (3f - 2f * x);
            if (flap != null)
            {
                flap.localPosition = flapPos + flapOpenOffset * e;
                flap.localRotation = flapRot * Quaternion.Euler(flapOpenEuler * e);
            }
            target.transform.localPosition = targetPos + hideOffset * (1f - e);
        }
    }
}
