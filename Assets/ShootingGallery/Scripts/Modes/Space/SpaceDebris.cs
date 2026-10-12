using UnityEngine;

namespace ShootingGallery
{
    /// <summary>爆発で飛び散る、きらきら光る小さな破片 (しばらく飛んで小さくなって消える)。使い終わったら FxPool にしまって使い回す</summary>
    public class SpaceDebris : MonoBehaviour
    {
        Vector3 velocity, spinAxis;
        float age, life, size;
        string poolKey;

        public static void Spawn(Vector3 center, float radius, Material mat, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var type = i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere;
                string key = type == PrimitiveType.Cube ? "Debris_Cube" : "Debris_Sphere";
                var go = FxPool.Get(key, () =>
                {
                    var g = FxPool.Primitive(type, mat);
                    g.AddComponent<SpaceDebris>().poolKey = key;
                    return g;
                });
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.transform.SetPositionAndRotation(center + Random.insideUnitSphere * radius * 0.3f, Random.rotation);
                var d = go.GetComponent<SpaceDebris>();
                d.age = 0f;
                d.velocity = Random.onUnitSphere * Random.Range(2f, 5f) * Mathf.Max(0.6f, radius);
                d.spinAxis = Random.onUnitSphere;
                d.life = Random.Range(0.8f, 1.5f);
                d.size = Random.Range(0.06f, 0.16f) * Mathf.Max(0.7f, radius);
                go.transform.localScale = Vector3.one * d.size;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            transform.position += velocity * dt;
            velocity *= 1f - dt * 0.8f; // 宇宙なので落ちない。少しずつ遅くなる
            transform.Rotate(spinAxis, 400f * dt, Space.Self);
            float k = age / life;
            transform.localScale = Vector3.one * size * Mathf.Max(0f, 1f - k * k);
            if (k >= 1f) FxPool.Release(poolKey, gameObject);
        }
    }
}
