using UnityEngine;

namespace ShootingGallery
{
    /// <summary>爆発で飛び散る、きらきら光る小さな破片 (しばらく飛んで小さくなって消える)</summary>
    public class SpaceDebris : MonoBehaviour
    {
        Vector3 velocity, spinAxis;
        float age, life, size;

        public static void Spawn(Vector3 center, float radius, Material mat, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                go.name = "Debris";
                DestroyImmediate(go.GetComponent<Collider>());
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.position = center + Random.insideUnitSphere * radius * 0.3f;
                go.transform.rotation = Random.rotation;
                var d = go.AddComponent<SpaceDebris>();
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
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
