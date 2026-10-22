using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 当たった時に飛び散るコイン (カジノのスロット・ルーレット)。金色のコインが勢いよく飛び出し、くるくる回りながら落ちて、
    /// 床で一度はねてから、少しして小さくなって消える。コインは FxPool で使い回す
    /// </summary>
    public class CoinBurst : MonoBehaviour
    {
        const string PoolKey = "CasinoCoin";
        const float Gravity = 9f, Life = 2.6f;

        struct Coin
        {
            public Transform t;
            public Vector3 v, spinAxis;
            public float spin, age, floorY;
            public bool landed;
        }

        static CoinBurst instance;
        readonly List<Coin> coins = new List<Coin>();

        static CoinBurst Instance
        {
            get
            {
                if (instance == null) instance = new GameObject("CoinBurst").AddComponent<CoinBurst>();
                return instance;
            }
        }

        /// <summary>
        /// from から、forward の向きを中心に count 枚のコインを飛び散らせる。floorY = 床の高さ (ワールド)。
        /// spread = 飛び散る広がり (0 = まっすぐ 〜 1 = 半球)
        /// </summary>
        public static void Spawn(Vector3 from, Vector3 forward, int count, Material mat, float floorY, float speed = 4.5f, float spread = 0.7f)
        {
            if (mat == null) return;
            var self = Instance;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.up;
            for (int i = 0; i < count; i++)
            {
                var go = FxPool.Get(PoolKey, () => FxPool.Primitive(PrimitiveType.Cylinder, mat));
                go.GetComponent<Renderer>().sharedMaterial = mat;
                var t = go.transform;
                t.position = from + Random.insideUnitSphere * 0.15f;
                t.rotation = Random.rotation;
                t.localScale = new Vector3(0.11f, 0.007f, 0.11f);
                var dir = (forward + Random.insideUnitSphere * spread + Vector3.up * 0.6f).normalized;
                self.coins.Add(new Coin
                {
                    t = t, v = dir * speed * Random.Range(0.7f, 1.3f), spinAxis = Random.onUnitSphere,
                    spin = Random.Range(500f, 1100f), floorY = floorY + 0.01f
                });
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = coins.Count - 1; i >= 0; i--)
            {
                var c = coins[i];
                if (c.t == null) { coins.RemoveAt(i); continue; }
                c.age += dt;
                if (!c.landed)
                {
                    c.v += Vector3.down * Gravity * dt;
                    c.t.position += c.v * dt;
                    c.t.Rotate(c.spinAxis, c.spin * dt, Space.World);
                    if (c.t.position.y <= c.floorY && c.v.y < 0f)
                    {
                        // 床で一度はねて、二度目は寝かせて止める
                        if (c.v.y < -2.5f) { c.v = new Vector3(c.v.x * 0.5f, -c.v.y * 0.35f, c.v.z * 0.5f); c.spin *= 0.5f; Sfx.Play("coin", 0.12f); }
                        else
                        {
                            c.landed = true;
                            var p = c.t.position;
                            c.t.SetPositionAndRotation(new Vector3(p.x, c.floorY, p.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                        }
                    }
                }
                float fade = Mathf.Clamp01((Life - c.age) / 0.4f); // 終わりぎわに小さくなって消える
                c.t.localScale = new Vector3(0.11f, 0.007f, 0.11f) * fade;
                if (c.age >= Life)
                {
                    FxPool.Release(PoolKey, c.t.gameObject);
                    coins.RemoveAt(i);
                    continue;
                }
                coins[i] = c;
            }
        }
    }
}
