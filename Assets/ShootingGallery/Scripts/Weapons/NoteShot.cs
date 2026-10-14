using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// トランペットから吹き出す音符 (音楽モード)。銃口の向きへそれなりの速さでまっすぐ飛ぶ (重力なし)。
    /// 音符の形は撃つたびにランダム (4分音符 / 2分音符 / 8分音符)。くるくる揺れながら飛び、
    /// しっぽに5本の線 (五線譜) を、帯のように波打たせてたなびかせる (宇宙モードのミサイルの煙の筋の代わり)。
    /// 的に当たるとその的を倒し (爆発はしない)、光がはじける。五線譜の筋は少し残ってから消える
    /// </summary>
    public class NoteShot : MonoBehaviour
    {
        const float CastRadius = 0.16f; // 見た目より大きめ (当てやすく)
        const float MaxLifetime = 2.8f;
        const float LineGap = 0.032f;   // 五線の間隔
        const float TrailTime = 0.5f;   // 五線譜の筋が残る秒数
        const string PoolKey = "NoteShot";

        Vector3 velocity;
        float age, deadAt, wavePhase;
        bool dead;
        Material sparkMaterial;
        GameObject[] shapes;        // 4分音符・2分音符・8分音符の見た目 (1つだけ出す)
        Transform visual, staff;
        TrailRenderer[] trails;

        /// <summary>
        /// 音符を1つ吹き出す。noteMaterials = 音符の色 (撃つたびにランダム)、lineMaterial = 五線の筋、holeMaterial = 2分音符の玉の中の暗い所、sparkMaterial = 当たった時の光
        /// </summary>
        public static void Spawn(Vector3 position, Vector3 direction, float speed, Material[] noteMaterials, Material lineMaterial, Material holeMaterial, Material sparkMaterial)
        {
            var go = FxPool.Get(PoolKey, () => Build(lineMaterial, holeMaterial));
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            var n = go.GetComponent<NoteShot>();
            n.velocity = direction.normalized * speed;
            n.age = 0f;
            n.dead = false;
            n.sparkMaterial = sparkMaterial;
            n.wavePhase = Random.value * 10f;
            n.visual.gameObject.SetActive(true);
            int kind = Random.Range(0, n.shapes.Length);
            var mat = noteMaterials != null && noteMaterials.Length > 0 ? noteMaterials[Random.Range(0, noteMaterials.Length)] : null;
            for (int i = 0; i < n.shapes.Length; i++)
            {
                n.shapes[i].SetActive(i == kind);
                if (i == kind && mat != null)
                    foreach (var r in n.shapes[i].GetComponentsInChildren<Renderer>())
                        if (r.name != "Hole") r.sharedMaterial = mat;
            }
            n.UpdateStaff();
            foreach (var t in n.trails) { t.Clear(); t.emitting = true; }
        }

        // 音符3種類の見た目と、しっぽの五線譜 (5本の筋) を作る。+Z が進む向き、音符の表は -Z (撃った人の方)
        static GameObject Build(Material lineMaterial, Material holeMaterial)
        {
            var go = new GameObject(PoolKey);
            var n = go.AddComponent<NoteShot>();
            n.visual = new GameObject("Visual").transform;
            n.visual.SetParent(go.transform, false);
            n.shapes = new GameObject[3];
            for (int kind = 0; kind < 3; kind++)
            {
                var s = new GameObject(kind == 0 ? "Quarter" : kind == 1 ? "Half" : "Eighth");
                s.transform.SetParent(n.visual, false);
                Part(PrimitiveType.Sphere, s.transform, Vector3.zero, new Vector3(0.11f, 0.08f, 0.045f), Quaternion.Euler(0f, 0f, 22f));
                if (kind == 1) // 2分音符: 白い玉の真ん中が抜けて見えるように、暗い楕円を重ねる (色は付け替えない)
                {
                    var hole = Part(PrimitiveType.Sphere, s.transform, new Vector3(0f, 0f, -0.01f), new Vector3(0.065f, 0.035f, 0.03f), Quaternion.Euler(0f, 0f, 22f));
                    hole.name = "Hole";
                    hole.GetComponent<Renderer>().sharedMaterial = holeMaterial;
                }
                Part(PrimitiveType.Cube, s.transform, new Vector3(0.048f, 0.11f, 0f), new Vector3(0.013f, 0.22f, 0.013f), Quaternion.identity);
                if (kind == 2) Part(PrimitiveType.Cube, s.transform, new Vector3(0.08f, 0.17f, 0f), new Vector3(0.014f, 0.11f, 0.012f), Quaternion.Euler(0f, 0f, 40f));
                n.shapes[kind] = s;
            }
            // しっぽの五線譜: 音符の少し後ろで上下に波打つ5つの点から、それぞれ筋を引く
            n.staff = new GameObject("StaffTrail").transform;
            n.staff.SetParent(go.transform, false);
            n.trails = new TrailRenderer[5];
            for (int i = 0; i < 5; i++)
            {
                var e = new GameObject($"Line{i}");
                e.transform.SetParent(n.staff, false);
                e.transform.localPosition = new Vector3(0f, (i - 2) * LineGap, 0f);
                var t = e.AddComponent<TrailRenderer>();
                t.sharedMaterial = lineMaterial;
                t.time = TrailTime;
                t.widthMultiplier = 0.009f;
                t.minVertexDistance = 0.04f;
                t.numCapVertices = 0;
                t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t.receiveShadows = false;
                t.autodestruct = false;
                n.trails[i] = t;
            }
            return go;
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            var p = GameObject.CreatePrimitive(type);
            DestroyImmediate(p.GetComponent<Collider>()); // すぐ消す (同じフレームの当たり判定で自分に当たらないように)
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localRotation = rot;
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p.transform;
        }

        // 五線譜の筋の出どころ (音符の少し後ろ) を、上下左右にゆらゆら波打たせる
        void UpdateStaff()
        {
            float t = age * 13f + wavePhase;
            staff.localPosition = new Vector3(0.03f * Mathf.Sin(t * 0.7f), 0.07f * Mathf.Sin(t), -0.08f);
            staff.localRotation = Quaternion.Euler(0f, 0f, 14f * Mathf.Sin(t * 0.5f));
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
            // 音符はくるくると左右に揺れながら飛ぶ
            visual.localRotation = Quaternion.Euler(0f, 0f, 20f * Mathf.Sin(age * 9f + wavePhase));
            UpdateStaff();

            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            if (dist > 0f && Physics.SphereCast(transform.position, CastRadius, step / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                var target = hit.collider.GetComponentInParent<Target>();
                if (target != null && target.IsUp) target.Hit(hit.point); // 点数と音は GameManager
                Die(hit.point);
                return;
            }
            transform.position += step;
            if (age >= MaxLifetime) Die(transform.position);
        }

        // 当たった (または飛びきった): 光がはじけて音符が消え、五線譜の筋はその場で消えていく
        void Die(Vector3 point)
        {
            dead = true;
            deadAt = age;
            visual.gameObject.SetActive(false);
            foreach (var t in trails) t.emitting = false;
            if (sparkMaterial != null)
            {
                Explosion.Spawn(point, 0.3f, sparkMaterial);
                SpaceDebris.Spawn(point, 0.25f, sparkMaterial, 5);
            }
        }
    }
}
