using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 水の中の泡 (潜水艦モード)。ゆらゆら揺れながら上がっていき、少しずつふくらんで、最後にぱちんと小さくなって消える。
    /// 魚雷の後ろに引く泡・的に当たった時にぶわっと出る泡・海底から立ちのぼる泡に使う。使い終わったら FxPool にしまって使い回す
    /// </summary>
    public class Bubble : MonoBehaviour
    {
        const string PoolKey = "Bubble";

        Vector3 velocity;
        float age, life, size, phase, wobble;

        /// <summary>泡を1つ出す。velocity = 最初の速さ (だんだん弱まって、上へ浮かぶ速さに変わる)</summary>
        public static void Spawn(Vector3 position, float size, Material mat, Vector3 velocity, float life = 1.2f)
        {
            if (mat == null) return;
            var go = FxPool.Get(PoolKey, () =>
            {
                var g = FxPool.Primitive(PrimitiveType.Sphere, mat);
                g.AddComponent<Bubble>();
                return g;
            });
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.position = position;
            var b = go.GetComponent<Bubble>();
            b.velocity = velocity;
            b.age = 0f;
            b.life = life * Random.Range(0.8f, 1.2f);
            b.size = size;
            b.phase = Random.value * 10f;
            b.wobble = Random.Range(0.15f, 0.35f);
            go.transform.localScale = Vector3.one * (size * 0.4f);
        }

        /// <summary>ぶわっと泡が湧き出す (的に当たった時・魚雷が爆発した時)</summary>
        public static void Burst(Vector3 center, float radius, Material mat, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                Spawn(center + dir * radius * Random.Range(0f, 0.5f), Random.Range(0.04f, 0.12f) * Mathf.Max(0.6f, radius),
                      mat, dir * Random.Range(1f, 2.5f) * Mathf.Max(0.6f, radius), Random.Range(0.9f, 1.8f));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            // 最初の勢いはすぐに弱まり、浮く力で上へ (ゆらゆら左右に揺れながら)
            velocity = Vector3.Lerp(velocity, Vector3.up * (0.6f + size * 4f), 1f - Mathf.Exp(-3f * dt));
            Vector3 sway = new Vector3(Mathf.Sin(age * 7f + phase), 0f, Mathf.Cos(age * 6f + phase * 1.3f)) * wobble;
            transform.position += (velocity + sway) * dt;
            float k = age / life;
            // ふくらみながら上がり、最後の 15% でぱちんと消える
            float grow = Mathf.Lerp(0.4f, 1.15f, Mathf.Clamp01(k * 3f));
            float pop = k > 0.85f ? 1f - (k - 0.85f) / 0.15f : 1f;
            transform.localScale = Vector3.one * size * grow * Mathf.Max(0f, pop);
            if (k >= 1f) FxPool.Release(PoolKey, gameObject);
        }
    }
}
