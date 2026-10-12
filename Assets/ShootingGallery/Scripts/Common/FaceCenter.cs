using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// いつも水平に center (プレイヤーの足元) の方を向く (表の -Z が center を向く)。
    /// 向きが変わりながら走る敵の船に付けた的を、いつもプレイヤーから見えるようにする。
    /// </summary>
    public class FaceCenter : MonoBehaviour
    {
        [Tooltip("向く先 (ワールド座標)。プレイヤーは中心から動かないので原点")]
        public Vector3 center = Vector3.zero;

        void LateUpdate()
        {
            Vector3 d = transform.position - center;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d.normalized);
        }
    }
}
