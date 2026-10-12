using UnityEngine;

namespace ShootingGallery
{
    /// <summary>爆発の見た目。半透明の球が膨らみながら消える。使い終わったら FxPool にしまって使い回す。</summary>
    public class Explosion : MonoBehaviour
    {
        const float Duration = 0.35f;
        const string PoolKey = "Explosion";

        float radius;
        float age;
        Renderer rend;
        MaterialPropertyBlock block;
        Color baseColor;

        public static void Spawn(Vector3 position, float radius, Material material)
        {
            var go = FxPool.Get(PoolKey, () =>
            {
                var g = FxPool.Primitive(PrimitiveType.Sphere, material);
                var ex = g.AddComponent<Explosion>();
                ex.rend = g.GetComponent<Renderer>();
                ex.block = new MaterialPropertyBlock();
                return g;
            });
            go.transform.position = position;
            var e = go.GetComponent<Explosion>();
            e.rend.sharedMaterial = material;
            e.radius = radius;
            e.age = 0f;
            e.baseColor = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : new Color(1f, 0.6f, 0.2f, 0.8f);
            e.Update();
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Duration);
            float grow = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.one * (radius * 2f * Mathf.Lerp(0.3f, 1f, grow));

            var c = baseColor;
            c.a = baseColor.a * (1f - t);
            block.SetColor("_BaseColor", c);
            rend.SetPropertyBlock(block);

            if (t >= 1f) FxPool.Release(PoolKey, gameObject);
        }
    }
}
