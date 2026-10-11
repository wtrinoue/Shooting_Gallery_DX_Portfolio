using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>爆弾。放物線を描いて飛び、何かに当たると爆発して範囲内の的を全部倒す。</summary>
    public class Bomb : MonoBehaviour
    {
        const float Gravity = 5f;     // 本物より弱めにして狙いやすくしている
        const float CastRadius = 0.05f;
        const float MaxLifetime = 4f;

        Vector3 velocity;
        float blastRadius;
        Material explosionMaterial;
        float age;

        public static void Spawn(Vector3 position, Vector3 velocity, float blastRadius, Material bombMaterial, Material explosionMaterial)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Bomb";
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.09f;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = bombMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var bomb = go.AddComponent<Bomb>();
            bomb.velocity = velocity;
            bomb.blastRadius = blastRadius;
            bomb.explosionMaterial = explosionMaterial;
        }

        void Update()
        {
            age += Time.deltaTime;
            velocity += Vector3.down * (Gravity * Time.deltaTime);
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
            Blast(point, blastRadius, explosionMaterial);
            Destroy(gameObject);
        }

        /// <summary>爆発: 半径内の的を全部倒し、爆発の見た目と音を出す (爆弾ランチャーと爆弾の的で共通)。</summary>
        public static void Blast(Vector3 point, float radius, Material explosionMaterial)
        {
            var hitTargets = new HashSet<Target>();
            foreach (var col in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = col.GetComponentInParent<Target>();
                if (target != null && hitTargets.Add(target))
                    target.Hit(col.bounds.center);
            }

            Explosion.Spawn(point, radius, explosionMaterial);
            Sfx.Play("explosion");
        }
    }
}
