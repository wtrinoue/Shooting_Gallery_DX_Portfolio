using UnityEngine;

namespace ShootingGallery
{
    /// <summary>ローカルZ軸まわりに左右へ揺れる (振り子)。</summary>
    public class Pendulum : MonoBehaviour
    {
        public float amplitude = 25f; // 度
        public float speed = 2f;
        public float phase;

        Quaternion baseRotation;

        void Awake() => baseRotation = transform.localRotation;

        void Update()
        {
            float angle = amplitude * Mathf.Sin(Time.time * speed + phase);
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
        }
    }
}
