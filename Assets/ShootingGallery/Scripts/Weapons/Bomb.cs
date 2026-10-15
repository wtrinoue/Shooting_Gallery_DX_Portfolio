using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>爆弾。放物線を描いて飛び、何かに当たると爆発して範囲内の的を全部倒す。</summary>
    public class Bomb : MonoBehaviour
    {
        // 重力は武器ごと (WeaponDef.projectileGravity)。本物より弱めにして狙いやすくしている
        const float CastRadius = 0.05f;
        const float MaxLifetime = 4f;
        const string PoolKey = "Bomb";
        static readonly Collider[] blastBuffer = new Collider[256];
        static readonly HashSet<Target> blastTargets = new HashSet<Target>();
        static int blastDepth;

        Vector3 velocity;
        float gravity = 5f;
        float blastRadius;
        Material explosionMaterial;
        float age;

        public static void Spawn(Vector3 position, Vector3 velocity, float blastRadius, Material bombMaterial, Material explosionMaterial, float gravity = 5f, float size = 0.09f)
        {
            // 撃つたびに作らず、FxPool で使い回す
            var go = FxPool.Get(PoolKey, () => { var g = FxPool.Primitive(PrimitiveType.Sphere, bombMaterial); g.AddComponent<Bomb>(); return g; });
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = bombMaterial;

            var bomb = go.GetComponent<Bomb>();
            bomb.age = 0f;
            bomb.velocity = velocity;
            bomb.gravity = gravity;
            bomb.blastRadius = blastRadius;
            bomb.explosionMaterial = explosionMaterial;
        }

        void Update()
        {
            age += Time.deltaTime;
            velocity += Vector3.down * (gravity * Time.deltaTime);
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
            FxPool.Release(PoolKey, gameObject);
        }

        /// <summary>爆発: 半径内の的を全部倒し、爆発の見た目と音を出す (爆弾ランチャーと爆弾の的で共通)。</summary>
        public static void Blast(Vector3 point, float radius, Material explosionMaterial)
        {
            // 範囲内の当たり判定は使い回しの配列に入れる (爆発ごとに配列を作らない)。
            // 的を倒した先でもう1つ爆発が起きることがあるので、その時 (入れ子) だけは配列を作る
            blastDepth++;
            try
            {
                var cols = blastDepth == 1 ? blastBuffer : new Collider[blastBuffer.Length];
                var hitTargets = blastDepth == 1 ? blastTargets : new HashSet<Target>();
                hitTargets.Clear();
                int n = Physics.OverlapSphereNonAlloc(point, radius, cols, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var col = cols[i];
                    if (col == null) continue;
                    var target = col.GetComponentInParent<Target>();
                    if (target != null && hitTargets.Add(target))
                        target.Hit(col.bounds.center);
                }
                hitTargets.Clear();
            }
            finally { blastDepth--; }

            Explosion.Spawn(point, radius, explosionMaterial);
            Sfx.Play("explosion");
        }
    }
}
