using UnityEngine;

namespace ShootingGallery
{
    /// <summary>爆発の見た目。半透明の球が膨らみながら消える。</summary>
    public class Explosion : MonoBehaviour
    {
        const float Duration = 0.35f;

        float radius;
        float age;
        Renderer rend;
        MaterialPropertyBlock block;
        Color baseColor;

        public static void Spawn(Vector3 position, float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Explosion";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = position;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var e = go.AddComponent<Explosion>();
            e.radius = radius;
            e.rend = r;
            e.block = new MaterialPropertyBlock();
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

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
