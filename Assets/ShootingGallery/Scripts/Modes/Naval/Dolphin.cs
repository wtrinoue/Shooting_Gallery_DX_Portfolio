using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 海戦モードの景色: 船のそばで、ときどき海から弧を描いて飛び跳ねるイルカ (飾り。当たり判定なし)。
    /// 水の中にいる間は見せず、待ってから前へ向かって跳ね上がり、頭から海へ戻る。出る時と入る時に水しぶき。
    /// 位置はこのオブジェクトのローカル (+Z が前、y = 0 が海面)。
    /// </summary>
    public class Dolphin : MonoBehaviour
    {
        [Tooltip("跳ねる体 (このオブジェクトの子。+Z が頭)")]
        public Transform body;
        [Tooltip("水しぶき (半透明。なければ出さない)")]
        public Material splashMaterial;
        [Tooltip("1回の跳ねる秒数 / 高さ (m) / 前へ進む長さ (m)")]
        public float jumpTime = 1.2f;
        public float jumpHeight = 1.6f;
        public float jumpLength = 3.2f;
        [Tooltip("水の中で待つ秒数 (この範囲でランダム)")]
        public Vector2 wait = new Vector2(1.2f, 3.5f);

        const float Under = 0.5f; // 跳ね始め / 終わりの深さ (m)
        float t, nextJump;
        bool jumping;

        void OnEnable()
        {
            jumping = false;
            nextJump = Time.time + Random.Range(0f, wait.y); // 群れのイルカがそろって跳ねないように
            if (body != null) body.gameObject.SetActive(false);
        }

        void Update()
        {
            if (body == null) return;
            if (!jumping)
            {
                if (Time.time < nextJump) return;
                jumping = true;
                t = 0f;
                body.gameObject.SetActive(true);
            }
            t += Time.deltaTime / Mathf.Max(0.1f, jumpTime);
            float k = Mathf.Clamp01(t);
            float z = (k - 0.5f) * jumpLength;
            float y = -Under + (jumpHeight + Under) * 4f * k * (1f - k);
            // 体の向き: 弧に沿って (上がる時は頭が上、下りる時は頭から海へ)
            float dy = (jumpHeight + Under) * 4f * (1f - 2f * k);
            float pitch = -Mathf.Atan2(dy, jumpLength) * Mathf.Rad2Deg;
            body.localPosition = new Vector3(0f, y, z);
            body.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            // 海面を出る時と入る時に水しぶき
            float prevY = -Under + (jumpHeight + Under) * 4f * Mathf.Max(0f, k - Time.deltaTime / jumpTime) * (1f - Mathf.Max(0f, k - Time.deltaTime / jumpTime));
            if (splashMaterial != null && (prevY < 0f) != (y < 0f))
                Explosion.Spawn(transform.TransformPoint(new Vector3(0f, 0.05f, z)), 0.45f, splashMaterial);
            if (k >= 1f)
            {
                jumping = false;
                body.gameObject.SetActive(false);
                nextJump = Time.time + Random.Range(wait.x, wait.y);
            }
        }
    }
}
