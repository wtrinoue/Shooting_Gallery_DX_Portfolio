using UnityEngine;

namespace ShootingGallery
{
    /// <summary>親が回っても常に reference と同じ向きを保つ (観覧車のゴンドラ)。</summary>
    public class KeepUpright : MonoBehaviour
    {
        public Transform reference;

        void LateUpdate() => transform.rotation = reference != null ? reference.rotation : Quaternion.identity;
    }
}
