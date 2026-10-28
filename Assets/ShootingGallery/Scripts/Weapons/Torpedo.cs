using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 魚雷 (潜水艦モード)。銃口の向きへまっすぐ進む (重力なし)。撃った直後はゆっくりで、後ろのプロペラが回って少しずつ速くなる。
    /// 進みながら後ろにぶくぶく泡を引く (宇宙モードのミサイルの煙の筋の代わり)。
    /// 何かに当たる (または遠くまで進みきる) と爆発して、範囲内の的を全部倒す (爆発は Bomb.Blast と同じ)。泡もぶわっと湧く。
    /// </summary>
    public class Torpedo : MonoBehaviour
    {
        const float CastRadius = 0.08f;
        const float MaxLifetime = 4.5f;
        const float LaunchSpeedRatio = 0.4f; // 撃った直後の速さ (最高速に対する割合)
        const float SpeedUpTime = 0.7f;      // 最高速になるまでの秒数
        const float BubbleInterval = 0.07f;  // 泡を出す間隔 (秒。前は 0.03。半透明の泡が重なると GPU が重いので減らした)
        const float NoBubbleNear = 1f;       // 撃ってからこの距離 (m) までは、しっぽの泡を出さない (目の前で泡が画面を覆わないように)

        Vector3 direction;
        float speed, blastRadius, age, nextBubble, traveled;
        Material explosionMaterial, bubbleMaterial;
        Transform propeller, tail;

        public static void Spawn(Vector3 position, Vector3 direction, float speed, float blastRadius,
                                 Material bodyMaterial, Material noseMaterial, Material propellerMaterial, Material bubbleMaterial, Material explosionMaterial)
        {
            // 撃つたびに部品から作らず、FxPool で使い回す (材質は銃ごとに決まっているので、作る時に付ければよい)
            var go = FxPool.Get(PoolKey, () => Build(bodyMaterial, noseMaterial, propellerMaterial));
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            var t = go.GetComponent<Torpedo>();
            t.direction = direction.normalized;
            t.speed = speed;
            t.blastRadius = blastRadius;
            t.explosionMaterial = explosionMaterial;
            t.bubbleMaterial = bubbleMaterial;
            t.age = 0f;
            t.nextBubble = 0f;
            t.traveled = 0f;
            // 撃った所に、ボコッと泡が出る (目の前を覆わないように、銃口より少し先に少しだけ)
            Bubble.Burst(position + direction.normalized * 0.6f, 0.25f, bubbleMaterial, 3);
        }

        const string PoolKey = "Torpedo";

        // 魚雷1発分の見た目を作る: 灰色の胴・黄色い先端・後ろの十字の羽根・真鍮のプロペラ (+Z が前)
        static GameObject Build(Material bodyMaterial, Material noseMaterial, Material propellerMaterial)
        {
            var go = new GameObject(PoolKey);
            Part(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(0.07f, 0.15f, 0.07f), Quaternion.Euler(90f, 0f, 0f), bodyMaterial);
            Part(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, 0.15f), new Vector3(0.07f, 0.07f, 0.1f), Quaternion.identity, noseMaterial);
            Part(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0f, 0.07f), new Vector3(0.074f, 0.012f, 0.074f), Quaternion.Euler(90f, 0f, 0f), noseMaterial); // 黄色い帯
            var tail = Part(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0f, -0.15f), new Vector3(0.06f, 0.06f, 0.1f), Quaternion.identity, bodyMaterial);
            for (int i = 0; i < 4; i++)
                Part(PrimitiveType.Cube, go.transform, new Vector3(0f, 0f, -0.16f), new Vector3(0.11f, 0.004f, 0.045f), Quaternion.Euler(0f, 0f, i * 90f), bodyMaterial);
            // プロペラ (後ろの軸のまわりに回る3枚羽根)
            var prop = new GameObject("Propeller").transform;
            prop.SetParent(go.transform, false);
            prop.localPosition = new Vector3(0f, 0f, -0.21f);
            Part(PrimitiveType.Sphere, prop, Vector3.zero, Vector3.one * 0.022f, Quaternion.identity, propellerMaterial);
            for (int i = 0; i < 3; i++)
            {
                var blade = Part(PrimitiveType.Cube, prop, Vector3.zero, new Vector3(0.022f, 0.06f, 0.004f), Quaternion.Euler(0f, 0f, i * 120f), propellerMaterial);
                blade.localPosition = blade.localRotation * new Vector3(0f, 0.03f, 0f);
                blade.localRotation *= Quaternion.Euler(0f, 25f, 0f); // 羽根を少しひねる
            }

            var t = go.AddComponent<Torpedo>();
            t.propeller = prop;
            t.tail = tail;
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
            float dt = Time.deltaTime;
            age += dt;
            // 撃った直後はゆっくり。プロペラが回り始めて、だんだん速くなる
            float k = Mathf.Clamp01(age / SpeedUpTime);
            float v = speed * Mathf.Lerp(LaunchSpeedRatio, 1f, k * (2f - k));
            if (propeller != null) propeller.Rotate(0f, 0f, 1500f * (0.3f + 0.7f * k) * dt, Space.Self);

            // 後ろのプロペラから、ぶくぶく泡が出る
            if (age >= nextBubble && tail != null && traveled >= NoBubbleNear)
            {
                nextBubble = age + BubbleInterval;
                Vector3 back = tail.position - direction * 0.08f + Random.insideUnitSphere * 0.03f;
                Bubble.Spawn(back, Random.Range(0.025f, 0.06f), bubbleMaterial, -direction * Random.Range(0.3f, 0.8f) + Random.insideUnitSphere * 0.3f, Random.Range(0.8f, 1.4f));
            }

            Vector3 step = direction * v * dt;
            float dist = step.magnitude;
            if (dist > 0f && Physics.SphereCast(transform.position, CastRadius, direction, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                Explode(hit.point);
                return;
            }
            transform.position += step;
            traveled += step.magnitude;
            if (age >= MaxLifetime) Explode(transform.position);
        }

        void Explode(Vector3 point)
        {
            Bomb.Blast(point, blastRadius, explosionMaterial);
            Bubble.Burst(point, blastRadius * 0.6f, bubbleMaterial, 14); // 水の中なので、爆発のあとに泡がぶわっと湧く
            FxPool.Release(PoolKey, gameObject);
        }
    }
}
