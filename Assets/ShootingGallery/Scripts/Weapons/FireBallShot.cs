using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 怪獣モードの炎の玉。銃口の向きへまっすぐ飛び (重力なし)、当たった所で燃え上がって範囲内の的を全部倒す (爆発は Bomb.Blast と同じ)。
    /// 見た目は、しずくの後ろがギザギザの炎の舌に割れた形 (半透明の橙の外側と、白っぽく光る芯)。くるくる回りながらゆらめき、
    /// 後ろに黄色 → 橙 → 赤 → 黒い煙へ変わる炎の尾と、ちぎれた火の粉を引く。
    /// </summary>
    public class FireBallShot : MonoBehaviour
    {
        const float CastRadius = 0.1f;
        const float MaxLifetime = 3f;
        const float EmberInterval = 0.07f;
        const string PoolKey = "FireBallShot";

        Vector3 velocity;
        float blastRadius;
        Material explosionMaterial;
        float age, emberTimer, spin;
        Transform look;
        TrailRenderer trail;

        static Mesh outerMesh, coreMesh;

        public static void Spawn(Vector3 position, Vector3 direction, float speed, float blastRadius,
                                 Material outerMaterial, Material coreMaterial, Material trailMaterial, Material explosionMaterial)
        {
            var go = FxPool.Get(PoolKey, () => Build(outerMaterial, coreMaterial, trailMaterial));
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            var f = go.GetComponent<FireBallShot>();
            f.velocity = direction.normalized * speed;
            f.blastRadius = blastRadius;
            f.explosionMaterial = explosionMaterial;
            f.age = 0f;
            f.emberTimer = 0f;
            f.spin = Random.Range(0f, 360f);
            if (f.trail != null) { f.trail.Clear(); f.trail.emitting = true; }
        }

        static GameObject Build(Material outerMaterial, Material coreMaterial, Material trailMaterial)
        {
            if (outerMesh == null) outerMesh = FlameMesh(0.17f, 0.55f, 5, 0.5f, 0);
            if (coreMesh == null) coreMesh = FlameMesh(0.1f, 0.3f, 4, 0.35f, 1);
            var go = new GameObject(PoolKey);
            var look = new GameObject("Flame").transform;
            look.SetParent(go.transform, false);
            Part(look, "Outer", outerMesh, outerMaterial);
            Part(look, "Core", coreMesh, coreMaterial).localPosition = new Vector3(0f, 0f, 0.03f);

            if (trailMaterial != null)
            {
                var t = go.AddComponent<TrailRenderer>();
                t.sharedMaterial = trailMaterial;
                t.time = 0.4f;
                t.minVertexDistance = 0.08f;
                t.widthCurve = new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.3f, 0.24f), new Keyframe(1f, 0.04f));
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.08f), 0.25f),
                            new GradientColorKey(new Color(0.75f, 0.12f, 0.03f), 0.6f), new GradientColorKey(new Color(0.15f, 0.12f, 0.12f), 1f) },
                    new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.8f, 0.35f), new GradientAlphaKey(0.35f, 0.75f), new GradientAlphaKey(0f, 1f) });
                t.colorGradient = g;
                t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t.receiveShadows = false;
                t.autodestruct = false;
            }

            var f = go.AddComponent<FireBallShot>();
            f.look = look;
            f.trail = go.GetComponent<TrailRenderer>();
            return go;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material mat)
        {
            var p = new GameObject(name);
            p.transform.SetParent(parent, false);
            p.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = p.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return p.transform;
        }

        /// <summary>
        /// しずくの後ろがギザギザに割れた炎の形 (+Z が前)。前は丸く、後ろ (-Z) は tongues 本の炎の舌に分かれて、舌ごとに長さがちがう。
        /// radius = 前の丸みの半径、length = 後ろへ伸びる長さ、jag = 舌の長さのばらつき
        /// </summary>
        static Mesh FlameMesh(float radius, float length, int tongues, float jag, int seed)
        {
            const int Rings = 14, Segs = 30;
            var rng = new System.Random(17 + seed);
            var tongueLen = new float[tongues];
            for (int i = 0; i < tongues; i++) tongueLen[i] = 0.75f + (float)rng.NextDouble() * 0.5f;
            var verts = new Vector3[(Rings + 1) * (Segs + 1) + 2];
            int v = 0;
            for (int r = 0; r <= Rings; r++)
            {
                float u = r / (float)Rings; // 0 = 前の先、1 = 後ろのしっぽ
                for (int s = 0; s <= Segs; s++)
                {
                    float a = s / (float)Segs * Mathf.PI * 2f;
                    // 舌: 角度ごとに長さがちがう (尖った舌の先と、その間の切れ込み)
                    float x = a / (Mathf.PI * 2f) * tongues;
                    int k = Mathf.FloorToInt(x) % tongues;
                    float frac = x - Mathf.Floor(x);
                    float peak = 1f - Mathf.Abs(frac - 0.5f) * 2f; // 舌の真ん中で 1、切れ込みで 0
                    float tail = length * Mathf.Lerp(1f - jag, 1f, Mathf.Pow(peak, 1.5f)) * tongueLen[k];
                    float z, rad;
                    if (u < 0.35f)
                    {   // 前の丸い所
                        float q = u / 0.35f; // 0 = 先、1 = いちばん太い所
                        float ang = q * Mathf.PI * 0.5f;
                        z = Mathf.Cos(ang) * radius;
                        rad = Mathf.Sin(ang) * radius;
                    }
                    else
                    {   // 後ろへ細くなって、舌の先へ
                        float q = (u - 0.35f) / 0.65f;
                        z = -q * tail;
                        rad = radius * Mathf.Pow(1f - q, 0.8f) * (1f + 0.25f * Mathf.Sin(q * Mathf.PI) * (peak - 0.4f));
                    }
                    verts[v++] = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, z);
                }
            }
            var tris = new int[Rings * Segs * 6];
            int ti = 0;
            for (int r = 0; r < Rings; r++)
                for (int s = 0; s < Segs; s++)
                {
                    int a0 = r * (Segs + 1) + s, a1 = a0 + 1, b0 = a0 + Segs + 1, b1 = b0 + 1;
                    tris[ti++] = a0; tris[ti++] = b0; tris[ti++] = a1; // (表が外向き)
                    tris[ti++] = a1; tris[ti++] = b0; tris[ti++] = b1;
                }
            var mesh = new Mesh { name = "FlameDrop" };
            System.Array.Resize(ref verts, v);
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            // ゆらめき: くるくる回りながら、太さと長さが小刻みに変わる
            spin += 540f * dt;
            float w = 1f + 0.12f * Mathf.Sin(age * 47f) + 0.06f * Mathf.Sin(age * 83f);
            float l = 1f + 0.18f * Mathf.Sin(age * 39f + 1f);
            look.localRotation = Quaternion.Euler(0f, 0f, spin);
            look.localScale = new Vector3(w, w * (1f + 0.08f * Mathf.Sin(age * 61f)), l);

            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist > 0f && Physics.SphereCast(transform.position, CastRadius, step / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                Explode(hit.point);
                return;
            }
            transform.position += step;

            // ちぎれた火の粉 (炎の尾から、ぱっと膨らんで消える)
            emberTimer += dt;
            if (emberTimer >= EmberInterval && explosionMaterial != null)
            {
                emberTimer = 0f;
                Explosion.Spawn(transform.position - velocity.normalized * 0.25f + Random.insideUnitSphere * 0.08f, Random.Range(0.1f, 0.18f), explosionMaterial);
            }
            if (age >= MaxLifetime) Explode(transform.position);
        }

        void Explode(Vector3 point)
        {
            Bomb.Blast(point, blastRadius, explosionMaterial);
            if (trail != null) trail.emitting = false;
            FxPool.Release(PoolKey, gameObject);
        }
    }
}
