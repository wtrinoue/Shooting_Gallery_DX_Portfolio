using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ミサイル (宇宙モード)。銃口の向きへまっすぐ飛び (重力なし)、後ろに炎と白い煙の筋を引く。
    /// 何かに当たる (または遠くまで飛びきる) と爆発して、範囲内の的を全部倒す (爆発は Bomb.Blast と同じ)。
    /// </summary>
    public class Missile : MonoBehaviour
    {
        const float CastRadius = 0.08f;
        const float MaxLifetime = 3.5f;

        Vector3 velocity;
        float blastRadius;
        Material explosionMaterial;
        float age;
        Transform flame;

        public static void Spawn(Vector3 position, Vector3 direction, float speed, float blastRadius,
                                 Material bodyMaterial, Material finMaterial, Material flameMaterial, Material trailMaterial, Material explosionMaterial)
        {
            // 撃つたびに部品から作らず、FxPool で使い回す (材質は銃ごとに決まっているので、作る時に付ければよい)
            var go = FxPool.Get(PoolKey, () => Build(bodyMaterial, finMaterial, flameMaterial, trailMaterial));
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            var m = go.GetComponent<Missile>();
            m.velocity = direction.normalized * speed;
            m.blastRadius = blastRadius;
            m.explosionMaterial = explosionMaterial;
            m.age = 0f;
            var t = go.GetComponent<TrailRenderer>();
            if (t != null) { t.Clear(); t.emitting = true; }
        }

        const string PoolKey = "Missile";

        // ミサイル1発分の見た目と部品を作る
        static GameObject Build(Material bodyMaterial, Material finMaterial, Material flameMaterial, Material trailMaterial)
        {
            var go = new GameObject(PoolKey);

            // 白い胴体・赤い先端・4枚の羽根・後ろの炎 (+Z が前)
            Part(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.05f, 0.11f, 0.05f), Quaternion.Euler(90f, 0f, 0f), bodyMaterial);
            Part(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, 0.11f), new Vector3(0.05f, 0.05f, 0.09f), Quaternion.identity, finMaterial);
            for (int i = 0; i < 4; i++)
                Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, -0.09f), new Vector3(0.11f, 0.004f, 0.05f), Quaternion.Euler(0f, 0f, 45f + i * 90f), finMaterial);
            var flame = Part(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, -0.15f), new Vector3(0.045f, 0.045f, 0.1f), Quaternion.identity, flameMaterial);

            if (trailMaterial != null)
            {
                var trail = go.AddComponent<TrailRenderer>();
                trail.sharedMaterial = trailMaterial;
                trail.time = 0.45f;
                trail.startWidth = 0.06f;
                trail.endWidth = 0.22f;
                trail.minVertexDistance = 0.1f;
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.autodestruct = false;
            }

            var m = go.AddComponent<Missile>();
            m.flame = flame;
            return go;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var p = GameObject.CreatePrimitive(type);
            DestroyImmediate(p.GetComponent<Collider>()); // すぐ消す (同じフレームの当たり判定で自分に当たらないように)
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = rot;
            p.transform.localScale = scale;
            var r = p.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p.transform;
        }

        void Update()
        {
            age += Time.deltaTime;
            // 炎がゆらゆら伸び縮みする
            if (flame != null) flame.localScale = new Vector3(0.045f, 0.045f, 0.08f + 0.05f * Mathf.PerlinNoise(age * 30f, 0f));

            Vector3 step = velocity * Time.deltaTime;
            float dist = step.magnitude;
            if (dist > 0f && Physics.SphereCast(transform.position, CastRadius, step / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                Explode(hit.point);
                return;
            }
            transform.position += step;
            if (age >= MaxLifetime) Explode(transform.position);
        }

        void Explode(Vector3 point)
        {
            Bomb.Blast(point, blastRadius, explosionMaterial);
            // 煙の筋はその場に少し残して消す
            var trail = GetComponent<TrailRenderer>();
            if (trail != null)
            {
                var left = FxPool.Get("MissileTrail", () =>
                {
                    var g = new GameObject("MissileTrail");
                    var tr = g.AddComponent<TrailRenderer>();
                    tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    tr.receiveShadows = false;
                    tr.autodestruct = false;
                    return g;
                });
                left.transform.position = transform.position;
                var copy = left.GetComponent<TrailRenderer>();
                copy.emitting = false;
                copy.Clear();
                copy.sharedMaterial = trail.sharedMaterial;
                copy.time = trail.time;
                copy.widthCurve = trail.widthCurve;
                int count = trail.GetPositions(trailPositions);
                if (count < trailPositions.Length) System.Array.Clear(trailPositions, count, trailPositions.Length - count);
                for (int i = 0; i < count; i++) copy.AddPosition(trailPositions[i]);
                FxPool.ReleaseAfter("MissileTrail", left, trail.time + 0.5f);
            }
            FxPool.Release(PoolKey, gameObject);
        }

        static readonly Vector3[] trailPositions = new Vector3[256]; // 煙の筋の点 (使い回し)
    }
}
