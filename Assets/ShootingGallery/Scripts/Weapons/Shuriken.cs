using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 投げた手裏剣 (忍者モード)。くるくる回りながらまっすぐ飛び、当たった所に刺さる。的に当たれば倒す。
    /// 刺さった手裏剣は少しの間そのまま残ってから消える (的が倒れると一緒に倒れて見えなくなる)。
    /// </summary>
    public class Shuriken : MonoBehaviour
    {
        const float Radius = 0.07f;    // 当たり判定の太さ (少しだけエイム補正になる)
        const float SpinSpeed = 1440f; // 度/秒
        const float StuckTime = 1.5f;  // 刺さってから消えるまで

        Vector3 velocity;
        float maxDistance;
        float travelled;
        LayerMask mask;
        Transform spin;
        float spinSpeed = SpinSpeed;
        bool stuck;

        /// <summary>手裏剣を投げる。dir の向きに speed (m/秒) でまっすぐ飛び、range (m) 飛んだら消える</summary>
        public static void Spawn(Vector3 origin, Vector3 dir, float speed, float range, Material mat, Material centerMat, LayerMask mask)
        {
            var go = new GameObject("Shuriken");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(dir);
            var s = go.AddComponent<Shuriken>();
            s.velocity = dir.normalized * speed;
            s.maxDistance = range;
            s.mask = mask;

            // 見た目: 平たい棒を4本、45度ずつずらして重ねた8方手裏剣と、真ん中の輪 (飛ぶ向きに対して水平に寝かせる)
            s.spin = new GameObject("Spin").transform;
            s.spin.SetParent(go.transform, false);
            for (int i = 0; i < 4; i++)
                Part(PrimitiveType.Cube, s.spin, Quaternion.Euler(0f, i * 45f, 0f), new Vector3(0.026f, 0.006f, 0.14f), mat);
            Part(PrimitiveType.Cylinder, s.spin, Quaternion.identity, new Vector3(0.045f, 0.005f, 0.045f), centerMat);
        }

        /// <summary>
        /// ダーツを投げる (カジノモード)。飛び方と刺さり方は手裏剣と同じで、見た目だけが違う:
        /// 細い軸の先に針、後ろに十字の羽根。回らずに、先を前に向けて飛ぶ。
        /// </summary>
        public static void SpawnDart(Vector3 origin, Vector3 dir, float speed, float range, Material shaftMat, Material flightMat, LayerMask mask)
        {
            var go = new GameObject("Dart");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.LookRotation(dir);
            var s = go.AddComponent<Shuriken>();
            s.velocity = dir.normalized * speed;
            s.maxDistance = range;
            s.mask = mask;
            s.spinSpeed = 0f;
            s.spin = new GameObject("Body").transform;
            s.spin.SetParent(go.transform, false);
            s.spin.localPosition = Vector3.back * 0.06f; // 針の先が当たった所に来るように、体は少し後ろ
            Part(PrimitiveType.Cylinder, s.spin, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.012f, 0.06f, 0.012f), shaftMat, Vector3.zero);
            Part(PrimitiveType.Cylinder, s.spin, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.004f, 0.03f, 0.004f), shaftMat, new Vector3(0f, 0f, 0.085f)); // 針
            Part(PrimitiveType.Cube, s.spin, Quaternion.identity, new Vector3(0.05f, 0.002f, 0.04f), flightMat, new Vector3(0f, 0f, -0.07f));  // 羽根
            Part(PrimitiveType.Cube, s.spin, Quaternion.identity, new Vector3(0.002f, 0.05f, 0.04f), flightMat, new Vector3(0f, 0f, -0.07f));
        }

        static void Part(PrimitiveType type, Transform parent, Quaternion rot, Vector3 scale, Material mat, Vector3 pos = default)
        {
            var p = GameObject.CreatePrimitive(type);
            Destroy(p.GetComponent<Collider>());
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = rot;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (stuck) return;
            float dt = Time.deltaTime;
            float step = velocity.magnitude * dt;
            Vector3 dir = velocity.normalized;
            if (spinSpeed != 0f) spin.Rotate(0f, spinSpeed * dt, 0f, Space.Self);

            if (Physics.SphereCast(transform.position, Radius, dir, out var hit, step, mask, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<Target>();
                if (target != null && target.IsUp) target.Hit(hit.point); // 当たった音は GameManager が鳴らす
                else Sfx.Play("thunk");
                Stick(hit, dir);
                return;
            }
            transform.position += dir * step;
            travelled += step;
            if (travelled > maxDistance) Destroy(gameObject);
        }

        // 当たった所に、刃の先が少し食い込むように刺さる。当たった物 (の親) について動く (倒れる的と一緒に倒れる)
        void Stick(RaycastHit hit, Vector3 dir)
        {
            stuck = true;
            transform.position = hit.point + dir * 0.03f;
            // 当たった物そのものは大きさを変えた箱のことが多いので、その親につける (子が歪まないように)
            var parent = hit.collider.transform.parent;
            if (parent != null) transform.SetParent(parent, true);
            Destroy(gameObject, StuckTime); // 的と一緒に非表示になっても消える
        }
    }
}
