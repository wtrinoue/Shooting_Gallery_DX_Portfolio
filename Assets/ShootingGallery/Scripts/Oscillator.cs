using UnityEngine;

namespace ShootingGallery
{
    /// <summary>決まった向きに行ったり来たりする (エレベーターの台など)。</summary>
    public class Oscillator : MonoBehaviour
    {
        [Tooltip("動く向き (ローカル)。長さが振れ幅 (m) になる")]
        public Vector3 offset = new Vector3(0f, 0.45f, 0f);
        public float speed = 1.5f;
        public float phase;

        Vector3 basePos;

        void Awake() => basePos = transform.localPosition;

        void Update() => transform.localPosition = basePos + offset * Mathf.Sin(Time.time * speed + phase);
    }
}
