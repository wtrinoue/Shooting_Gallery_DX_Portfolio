using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 手元がコントローラーの模型の間 (タイトルで操作方法の看板を見ている間) に、引き金を引くと出る白い玉。
    /// まっすぐ飛び、看板のボタン (TargetKind.Credits) に当たった時だけそのボタンを押す。ほかの物に当たるか、飛びきったら消える (音も爆発もなし)
    /// </summary>
    public class PointerBall : MonoBehaviour
    {
        const string PoolKey = "PointerBall";
        const float Speed = 14f;
        const float MaxLifetime = 1.5f;
        const float Radius = 0.03f;

        Vector3 velocity;
        float age;

        public static void Spawn(Vector3 position, Vector3 direction, Material material)
        {
            var go = FxPool.Get(PoolKey, () =>
            {
                var g = FxPool.Primitive(PrimitiveType.Sphere, material);
                g.AddComponent<PointerBall>();
                return g;
            });
            go.transform.position = position;
            go.transform.localScale = Vector3.one * Radius * 2f;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var b = go.GetComponent<PointerBall>();
            b.velocity = direction.normalized * Speed;
            b.age = 0f;
        }

        void Update()
        {
            age += Time.deltaTime;
            Vector3 step = velocity * Time.deltaTime;
            float dist = step.magnitude;
            if (dist > 0f && Physics.SphereCast(transform.position, Radius, step / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = hit.collider.GetComponentInParent<Target>();
                if (t != null && t.kind == TargetKind.Credits) t.Hit(hit.point); // 看板のボタンだけを押す
                FxPool.Release(PoolKey, gameObject);
                return;
            }
            transform.position += step;
            if (age >= MaxLifetime) FxPool.Release(PoolKey, gameObject);
        }
    }
}
