using UnityEngine;

namespace ShootingGallery
{
    /// <summary>視線の水平方向にゆっくり追従するHUD。360度モードでも見失わないようにする。</summary>
    public class HudFollow : MonoBehaviour
    {
        public Transform head;
        public float distance = 2.2f;
        public float heightOffset = 0.88f; // (いちばん下の文字が、屋台ののれんの下のふちより上に見える高さ)
        public float followSpeed = 2.5f;

        void LateUpdate()
        {
            if (head == null) return;
            Vector3 fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) return;
            fwd.Normalize();

            Vector3 targetPos = head.position + fwd * distance + Vector3.up * heightOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            Vector3 look = transform.position - head.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(look);
        }
    }
}
