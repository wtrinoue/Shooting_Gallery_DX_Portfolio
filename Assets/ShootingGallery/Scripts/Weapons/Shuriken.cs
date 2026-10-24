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
            // 投げるたびに部品から作らず、FxPool で使い回す
            var go = FxPool.Get("Shuriken", () =>
            {
                var g = new GameObject("Shuriken");
                var sh = g.AddComponent<Shuriken>();
                sh.poolKey = "Shuriken";
                // 見た目: 紺色の丸から四方へ灰色の三角形の刃が伸びる4方手裏剣 (ShurikenShape。飛ぶ向きに対して水平に寝かせる)
                sh.spin = new GameObject("Spin").transform;
                sh.spin.SetParent(g.transform, false);
                ShurikenShape.Create(sh.spin, "Shuriken", 0.075f, mat, centerMat, true);
                return g;
            });
            Launch(go, origin, dir, speed, range, mask);
        }

        /// <summary>
        /// ダーツを投げる (カジノモード)。飛び方と刺さり方は手裏剣と同じで、見た目だけが違う:
        /// 細い軸の先に針、後ろに十字の羽根。回らずに、先を前に向けて飛ぶ。
        /// </summary>
        public static void SpawnDart(Vector3 origin, Vector3 dir, float speed, float range, Material shaftMat, Material flightMat, LayerMask mask)
        {
            var go = FxPool.Get("Dart", () =>
            {
                var g = new GameObject("Dart");
                var s = g.AddComponent<Shuriken>();
                s.poolKey = "Dart";
                s.spinSpeed = 0f;
                s.spin = new GameObject("Body").transform;
                s.spin.SetParent(g.transform, false);
                s.spin.localPosition = Vector3.back * 0.06f; // 針の先が当たった所に来るように、体は少し後ろ
                Part(PrimitiveType.Cylinder, s.spin, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.012f, 0.06f, 0.012f), shaftMat, Vector3.zero);
                Part(PrimitiveType.Cylinder, s.spin, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.004f, 0.03f, 0.004f), shaftMat, new Vector3(0f, 0f, 0.085f)); // 針
                Part(PrimitiveType.Cube, s.spin, Quaternion.identity, new Vector3(0.05f, 0.002f, 0.04f), flightMat, new Vector3(0f, 0f, -0.07f));  // 羽根
                Part(PrimitiveType.Cube, s.spin, Quaternion.identity, new Vector3(0.002f, 0.05f, 0.04f), flightMat, new Vector3(0f, 0f, -0.07f));
                return g;
            });
            Launch(go, origin, dir, speed, range, mask);
        }

        /// <summary>
        /// つるはしを投げる (発掘モード)。飛び方と刺さり方は手裏剣と同じで、見た目と回り方だけが違う:
        /// 木の柄の先に、両側へ反った鉄の頭。縦にくるくる回りながら飛ぶ (少し大きめ)。
        /// </summary>
        public static void SpawnPickaxe(Vector3 origin, Vector3 dir, float speed, float range, Material handleMat, Material headMat, LayerMask mask)
        {
            var go = FxPool.Get("Pickaxe", () =>
            {
                var g = new GameObject("Pickaxe");
                var s = g.AddComponent<Shuriken>();
                s.poolKey = "Pickaxe";
                s.spinSpeed = 900f;
                s.spinAxis = Vector3.right; // 縦回り
                s.spin = new GameObject("Spin").transform;
                s.spin.SetParent(g.transform, false);
                Part(PrimitiveType.Cylinder, s.spin, Quaternion.identity, new Vector3(0.025f, 0.14f, 0.025f), handleMat, new Vector3(0f, -0.04f, 0f)); // 柄
                foreach (float side in new[] { -1f, 1f }) // 頭 (前後に反ったとがり)
                {
                    Part(PrimitiveType.Cube, s.spin, Quaternion.Euler(side * 12f, 0f, 0f), new Vector3(0.03f, 0.03f, 0.13f), headMat, new Vector3(0f, 0.09f, side * 0.06f));
                    Part(PrimitiveType.Cube, s.spin, Quaternion.Euler(side * 35f, 0f, 0f), new Vector3(0.02f, 0.02f, 0.06f), headMat, new Vector3(0f, 0.065f, side * 0.135f));
                }
                return g;
            });
            Launch(go, origin, dir, speed, range, mask);
        }

        string poolKey = "Shuriken"; // FxPool にしまう時の種類 (Shuriken / Dart / Pickaxe)
        Vector3 spinAxis = Vector3.up;

        // 使い回しの1本を、origin から dir へ飛ばし始める
        static void Launch(GameObject go, Vector3 origin, Vector3 dir, float speed, float range, LayerMask mask)
        {
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(dir));
            go.transform.localScale = Vector3.one; // 刺さっていた物の大きさの影響を戻す
            var s = go.GetComponent<Shuriken>();
            s.velocity = dir.normalized * speed;
            s.maxDistance = range;
            s.travelled = 0f;
            s.mask = mask;
            s.stuck = false;
        }

        static void Part(PrimitiveType type, Transform parent, Quaternion rot, Vector3 scale, Material mat, Vector3 pos = default)
        {
            var p = GameObject.CreatePrimitive(type);
            DestroyImmediate(p.GetComponent<Collider>()); // すぐ消す (飛んでいる自分に当たらないように)
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
            if (spinSpeed != 0f) spin.Rotate(spinAxis * (spinSpeed * dt), Space.Self);

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
            if (travelled > maxDistance) FxPool.Release(poolKey, gameObject);
        }

        // 当たった所に、刃の先が少し食い込むように刺さる。当たった物 (の親) について動く (倒れる的と一緒に倒れる)
        void Stick(RaycastHit hit, Vector3 dir)
        {
            stuck = true;
            transform.position = hit.point + dir * 0.03f;
            // 当たった物そのものは大きさを変えた箱のことが多いので、その親につける (子が歪まないように)
            var parent = hit.collider.transform.parent;
            if (parent != null) transform.SetParent(parent, true);
            FxPool.ReleaseAfter(poolKey, gameObject, StuckTime); // 的と一緒に非表示になっても、時間どおりにしまう
        }
    }
}
