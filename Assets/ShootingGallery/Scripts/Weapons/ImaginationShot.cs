using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// イマジネーションの玉 (美術館モード)。銃口の向きへまっすぐ飛ぶ (重力なし)、光る玉。まわりを小さな光の粒が回り、後ろに虹色の光の筋を引く。
    /// 形が変わる: キャンバスへ向かう時は筆 (筆先を前にまっすぐ飛ぶ)、彫刻へ向かう時はトンカチ (縦にくるくる回る)、天井から吊るした作品へ向かう時は粘土 (ぐにゃぐにゃ転がる)。
    ///   放った時に飛ぶ先を1回だけ調べ、作品に当たると決まっていれば、当たる MorphLead m 手前 (近ければすぐ) で変わる。
    ///   それ以外でも、作品のまわりの範囲 (ArtField) に入ると変わる (かすめて飛ぶ時)。
    /// 的に当たるとその的を撃ち (作品なら ArtMuseum が筆やトンカチにして作品を作る)、光がはじける。光の筋は少し残ってから消える
    /// </summary>
    public class ImaginationShot : MonoBehaviour
    {
        const float CastRadius = 0.15f; // 見た目より大きめ (当てやすく)
        const float MaxLifetime = 3f;
        const float TrailTime = 0.35f;
        const string PoolKey = "ImaginationShot";
        const float MorphLead = 9f; // 作品に当たると決まっている時、当たる何 m 手前で筆 / トンカチに変わるか

        Vector3 velocity;
        float age, deadAt, phase;
        bool dead;
        Material sparkMaterial;
        Transform visual, orbit, ball, brush, hammer, clay;
        Renderer core;
        Renderer[] sparkles;
        Renderer[] brushParts, hammerParts, clayParts;
        TrailRenderer trail;
        int form; // 0 = 光の玉 / 1 = 筆 / 2 = トンカチ / 3 = 粘土
        float formAge;
        int lockedForm;      // 当たると決まっている作品の形 (0 = 決まっていない)
        float lockedAt;      // その形に変わる、飛んだ距離 (m)
        float travelled;

        /// <summary>玉を1つ放つ。materials = 玉の色 (撃つたびにランダム。まわりの粒は別の色)、trailMaterial = 虹色の筋、sparkMaterial = 当たった時の光</summary>
        public static void Spawn(Vector3 position, Vector3 direction, float speed, Material[] materials, Material trailMaterial, Material sparkMaterial)
        {
            var go = FxPool.Get(PoolKey, () => Build(trailMaterial));
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            var s = go.GetComponent<ImaginationShot>();
            s.velocity = direction.normalized * speed;
            s.age = 0f;
            s.dead = false;
            s.sparkMaterial = sparkMaterial;
            s.phase = Random.value * 10f;
            s.visual.gameObject.SetActive(true);
            if (materials != null && materials.Length > 0)
            {
                int c = Random.Range(0, materials.Length);
                s.core.sharedMaterial = materials[c];
                for (int i = 0; i < s.sparkles.Length; i++) s.sparkles[i].sharedMaterial = materials[(c + 1 + i) % materials.Length];
                // 筆とトンカチも、玉と同じ光る色で (筆先・金具・柄 / 頭・柄)
                for (int i = 0; i < s.brushParts.Length; i++) s.brushParts[i].sharedMaterial = materials[(c + i) % materials.Length];
                for (int i = 0; i < s.hammerParts.Length; i++) s.hammerParts[i].sharedMaterial = materials[(c + i * 2) % materials.Length];
                for (int i = 0; i < s.clayParts.Length; i++) s.clayParts[i].sharedMaterial = materials[(c + i) % materials.Length];
            }
            s.SetForm(0);
            s.travelled = 0f;
            s.lockedForm = 0;
            // 飛ぶ先を1回だけ調べる (まっすぐ飛ぶので、作品に当たるかは放った時に分かる。毎フレーム遠くまで調べない)
            float range = speed * MaxLifetime;
            if (Physics.SphereCast(position, CastRadius, direction.normalized, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                var art = hit.collider.GetComponentInParent<Artwork>();
                if (art != null && art.target != null && art.target.IsUp)
                {
                    s.lockedForm = FormFor(art.kind);
                    s.lockedAt = Mathf.Max(0f, hit.distance - MorphLead);
                }
            }
            s.trail.Clear();
            s.trail.emitting = true;
        }

        static GameObject Build(Material trailMaterial)
        {
            var go = new GameObject(PoolKey);
            var s = go.AddComponent<ImaginationShot>();
            s.visual = new GameObject("Visual").transform;
            s.visual.SetParent(go.transform, false);
            s.ball = new GameObject("Ball").transform;
            s.ball.SetParent(s.visual, false);
            s.core = Part(PrimitiveType.Sphere, s.ball, Vector3.zero, Vector3.one * 0.09f).GetComponent<Renderer>();
            s.orbit = new GameObject("Orbit").transform;
            s.orbit.SetParent(s.ball, false);
            s.sparkles = new Renderer[3];
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                s.sparkles[i] = Part(PrimitiveType.Sphere, s.orbit, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.085f, Vector3.one * 0.03f).GetComponent<Renderer>();
            }
            // 筆 (筆先が前 +Z。柄は後ろへ)
            s.brush = new GameObject("Brush").transform;
            s.brush.SetParent(s.visual, false);
            s.brushParts = new[]
            {
                Part(PrimitiveType.Capsule, s.brush, new Vector3(0f, 0f, 0.05f), new Vector3(0.05f, 0.05f, 0.05f), 90f).GetComponent<Renderer>(),  // 筆先
                Part(PrimitiveType.Cylinder, s.brush, new Vector3(0f, 0f, -0.02f), new Vector3(0.04f, 0.03f, 0.04f), 90f).GetComponent<Renderer>(), // 金具
                Part(PrimitiveType.Cylinder, s.brush, new Vector3(0f, 0f, -0.16f), new Vector3(0.028f, 0.11f, 0.028f), 90f).GetComponent<Renderer>(), // 柄
            };
            // トンカチ (頭が先。縦に回る)
            s.hammer = new GameObject("Hammer").transform;
            s.hammer.SetParent(s.visual, false);
            s.hammerParts = new[]
            {
                Part(PrimitiveType.Cube, s.hammer, new Vector3(0f, 0.09f, 0f), new Vector3(0.16f, 0.07f, 0.07f)).GetComponent<Renderer>(),           // 頭
                Part(PrimitiveType.Cylinder, s.hammer, new Vector3(0f, -0.02f, 0f), new Vector3(0.03f, 0.1f, 0.03f)).GetComponent<Renderer>(),    // 柄
            };
            // 粘土のかたまり (ぐにゃっとした玉に、小さなこぶ。ころころ転がりながら飛ぶ)
            s.clay = new GameObject("Clay").transform;
            s.clay.SetParent(s.visual, false);
            s.clayParts = new[]
            {
                Part(PrimitiveType.Sphere, s.clay, Vector3.zero, new Vector3(0.15f, 0.12f, 0.13f)).GetComponent<Renderer>(),
                Part(PrimitiveType.Sphere, s.clay, new Vector3(0.05f, 0.04f, 0.02f), new Vector3(0.08f, 0.07f, 0.08f)).GetComponent<Renderer>(),
                Part(PrimitiveType.Sphere, s.clay, new Vector3(-0.05f, -0.03f, -0.03f), new Vector3(0.07f, 0.06f, 0.07f)).GetComponent<Renderer>(),
            };
            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = trailMaterial;
            t.time = TrailTime;
            t.widthMultiplier = 0.06f;
            t.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            var grad = new Gradient(); // 虹色 (材質は白の半透明で、頂点の色で虹にする)
            grad.SetKeys(new[]
            {
                new GradientColorKey(new Color(1f, 0.4f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0.25f),
                new GradientColorKey(new Color(0.4f, 1f, 0.5f), 0.5f), new GradientColorKey(new Color(0.35f, 0.7f, 1f), 0.75f),
                new GradientColorKey(new Color(0.8f, 0.45f, 1f), 1f),
            }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            t.colorGradient = grad;
            t.minVertexDistance = 0.04f;
            t.numCapVertices = 2;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.autodestruct = false;
            s.trail = t;
            return go;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, float pitch)
        {
            var p = Part(type, parent, pos, scale);
            p.localRotation = Quaternion.Euler(pitch, 0f, 0f); // 縦長の部品を前後 (Z) に寝かせる
            return p;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(type);
            DestroyImmediate(p.GetComponent<Collider>()); // すぐ消す (同じフレームの当たり判定で自分に当たらないように)
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (dead)
            {
                if (age >= deadAt + TrailTime + 0.1f) FxPool.Release(PoolKey, gameObject); // 筋が消えきってからしまう
                return;
            }
            // 作品のまわりの範囲に入ったら、筆 (キャンバス) / トンカチ (彫刻) に変わる。範囲を出たら玉に戻る
            int want = lockedForm != 0 && travelled >= lockedAt ? lockedForm
                     : ArtField.TryFind(transform.position, out var kind) ? FormFor(kind) : 0;
            if (want != form)
            {
                SetForm(want);
                if (sparkMaterial != null) Explosion.Spawn(transform.position, 0.22f, sparkMaterial); // ぽんっと変身
            }
            formAge += dt;
            float pop = Mathf.Min(1f, 0.4f + formAge * 6f); // 変わった瞬間は小さく、すぐ元の大きさへ
            switch (form)
            {
                case 1: // 筆: 筆先を前に、くるくるとひねりながらまっすぐ飛ぶ
                    visual.localScale = Vector3.one * pop;
                    brush.localRotation = Quaternion.Euler(0f, 0f, age * 360f);
                    break;
                case 2: // トンカチ: 縦にくるくる回りながら飛ぶ
                    visual.localScale = Vector3.one * pop;
                    hammer.localRotation = Quaternion.Euler(age * 900f, 0f, 0f);
                    break;
                case 3: // 粘土: ぐにゃぐにゃ形を変えながら、ころころ転がって飛ぶ
                    visual.localScale = new Vector3(1f + 0.12f * Mathf.Sin(age * 17f), 1f - 0.12f * Mathf.Sin(age * 17f), 1f) * pop;
                    clay.localRotation = Quaternion.Euler(age * 300f, age * 170f, 0f);
                    break;
                default: // 玉はふわふわ脈打ち、まわりの光の粒がくるくる回る
                    visual.localScale = Vector3.one * pop * (1f + 0.15f * Mathf.Sin(age * 14f + phase));
                    orbit.localRotation = Quaternion.Euler(0f, 0f, age * 540f);
                    break;
            }

            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist > 0f && Physics.SphereCast(transform.position, CastRadius, step / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                var target = hit.collider.GetComponentInParent<Target>();
                if (target != null && target.IsUp) target.Hit(hit.point); // 点数と作品づくりは GameManager → ArtMuseum
                Die(hit.point);
                return;
            }
            transform.position += step;
            travelled += dist;
            if (age >= MaxLifetime) Die(transform.position);
        }

        void SetForm(int f)
        {
            form = f;
            formAge = 0f;
            ball.gameObject.SetActive(f == 0);
            brush.gameObject.SetActive(f == 1);
            hammer.gameObject.SetActive(f == 2);
            clay.gameObject.SetActive(f == 3);
        }

        // 作品の種類 → 変わる形 (キャンバス: 筆 / 彫刻: トンカチ / 吊るした作品: 粘土)
        static int FormFor(Artwork.Kind kind) => kind == Artwork.Kind.Canvas ? 1 : kind == Artwork.Kind.Sculpture ? 2 : 3;

        // 当たった (または飛びきった): 光がはじけて玉が消え、光の筋はその場で消えていく
        void Die(Vector3 point)
        {
            dead = true;
            deadAt = age;
            visual.gameObject.SetActive(false);
            trail.emitting = false;
            if (sparkMaterial != null)
            {
                Explosion.Spawn(point, 0.3f, sparkMaterial);
                SpaceDebris.Spawn(point, 0.25f, sparkMaterial, 6);
            }
        }
    }
}
