using UnityEngine;

namespace ShootingGallery
{
    /// <summary>一定速度で回り続ける (観覧車の輪, 回転木馬の台)。</summary>
    public class Rotator : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float speed = 30f; // 度/秒

        void Update() => transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
    }
}
