using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 夏祭りモードの打ち上げ花火 1発。光の玉が光の尾を引きながらひゅるる…と空高く上がり、てっぺんで「ドーン」とぱっと光って、
    /// 火の粉が光の尾を引きながら広がり、垂れて、最後はきらきら瞬きながら消えていく。
    /// 開き方は3種類: 丸く全方向に開き、内側に別の色の芯がある (菊・芯入り) / 輪になって開く (輪) / 金色の火の粉が長い尾を引いて垂れる (しだれ柳)。
    /// 使い終わったら FxPool にしまって使い回す (火の粉の球も尾も作り直さない)。
    /// </summary>
    public class FestivalFirework : MonoBehaviour
    {
        public enum Style { Peony, Ring, Willow }

        const int Outer = 48;      // 外側の火の粉
        const int Core = 18;       // 内側の芯の火の粉 (菊だけ)
        const int Sparks = Outer + Core;
        const float FlashTime = 0.2f; // 開いた瞬間の光
        const string PoolKey = "FestivalFirework";

        Transform shell;
        TrailRenderer shellTrail;
        Transform[] sparks;
        Renderer[] sparkRenderers;
        TrailRenderer[] sparkTrails;
        readonly Vector3[] dirs = new Vector3[Sparks];
        readonly float[] reach = new float[Sparks]; // 開く大きさの倍率 (芯は小さく)
        readonly float[] twinkle = new float[Sparks];
        Vector3 from, burst;
        float riseTime, age, radius, gravity, life, sparkSize, trailTime;
        int used; // 使う火の粉の数 (芯がない時は外側だけ)
        bool burstDone;

        /// <summary>from から burst (開く所) へ打ち上げる。radius = 開いた大きさ (m)。coreColor = 芯の色 (菊の時だけ。null なら芯なし)</summary>
        public static void Launch(Vector3 from, Vector3 burst, float radius, Style style, Material color, Material shellMaterial, Material coreColor = null)
        {
            var go = FxPool.Get(PoolKey, () => Create(shellMaterial));
            var f = go.GetComponent<FestivalFirework>();
            f.from = from;
            f.burst = burst;
            f.radius = radius;
            f.age = 0f;
            f.burstDone = false;
            f.riseTime = Random.Range(1.5f, 2.1f);
            f.gravity = style == Style.Willow ? 3.4f : 1.4f;
            f.life = style == Style.Willow ? 3.6f : 2.4f;
            f.sparkSize = radius * (style == Style.Willow ? 0.05f : 0.06f);
            f.trailTime = style == Style.Willow ? 1.3f : style == Style.Ring ? 0.3f : 0.45f;
            bool core = style == Style.Peony && coreColor != null;
            f.used = core ? Sparks : Outer;

            // 打ち上がる光の玉と、その光の尾 (上がっていく軌跡が見える)
            f.shell.gameObject.SetActive(true);
            f.shell.localScale = Vector3.one * 0.35f;
            f.shell.position = from;
            f.shellTrail.Clear();
            f.shellTrail.emitting = true;

            for (int i = 0; i < Sparks; i++)
            {
                f.sparks[i].gameObject.SetActive(false);
                var mat = i < Outer ? color : coreColor;
                f.sparkRenderers[i].sharedMaterial = mat;
                f.sparkTrails[i].sharedMaterial = mat;
                f.sparkTrails[i].time = f.trailTime;
                f.reach[i] = i < Outer ? Random.Range(0.92f, 1.05f) : Random.Range(0.42f, 0.5f);
                f.twinkle[i] = Random.value * 10f;
            }
            // 開く向き
            if (style == Style.Ring)
            {
                // 少し傾いた縦の輪 (プレイヤーの方から見て丸く見えるように、輪の面を中心へ向ける)
                Vector3 toCenter = Vector3.ProjectOnPlane(from - burst, Vector3.up).normalized;
                var rot = Quaternion.LookRotation(toCenter) * Quaternion.Euler(Random.Range(-25f, 25f), 0f, Random.Range(-30f, 30f));
                for (int i = 0; i < Outer; i++)
                {
                    float a = i * Mathf.PI * 2f / Outer;
                    f.dirs[i] = rot * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                }
            }
            else
            {
                Sphere(f.dirs, 0, Outer);
                if (core) Sphere(f.dirs, Outer, Core);
            }
        }

        // 球の表面に均等に (フィボナッチ球)
        static void Sphere(Vector3[] dirs, int start, int count)
        {
            float off = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float y = 1f - (i + 0.5f) * 2f / count;
                float r = Mathf.Sqrt(1f - y * y);
                float a = i * 2.39996f + off;
                dirs[start + i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
        }

        static GameObject Create(Material shellMaterial)
        {
            var g = new GameObject(PoolKey);
            var f = g.AddComponent<FestivalFirework>();
            f.shell = FxPool.Primitive(PrimitiveType.Sphere, shellMaterial).transform;
            f.shell.SetParent(g.transform, false);
            f.shellTrail = AddTrail(f.shell.gameObject, shellMaterial, 0.9f, 0.3f);
            f.sparks = new Transform[Sparks];
            f.sparkRenderers = new Renderer[Sparks];
            f.sparkTrails = new TrailRenderer[Sparks];
            for (int i = 0; i < Sparks; i++)
            {
                var s = FxPool.Primitive(PrimitiveType.Sphere, shellMaterial);
                s.transform.SetParent(g.transform, false);
                s.SetActive(false);
                f.sparks[i] = s.transform;
                f.sparkRenderers[i] = s.GetComponent<Renderer>();
                f.sparkTrails[i] = AddTrail(s, shellMaterial, 0.45f, 1f);
            }
            return g;
        }

        // 光の尾 (先が太く、後ろへ細くなって消える)
        static TrailRenderer AddTrail(GameObject go, Material mat, float time, float width)
        {
            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = mat;
            t.time = time;
            t.widthMultiplier = width;
            t.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            t.minVertexDistance = 0.25f;
            t.numCapVertices = 0;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.emitting = false;
            return t;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age < riseTime)
            {
                // ひゅるる…と上がる (だんだん遅くなる。少しゆらゆら)。後ろに光の尾が伸びる
                float k = age / riseTime;
                float e = 1f - (1f - k) * (1f - k);
                shell.position = Vector3.Lerp(from, burst, e) + new Vector3(Mathf.Sin(age * 9f) * 0.15f, 0f, 0f);
                return;
            }
            float t = age - riseTime;
            if (!burstDone)
            {
                // ドーン: 打ち上げの尾はそこで止め (残りはすっと消えていく)、火の粉を開く所に並べてから尾を引かせる
                burstDone = true;
                shell.position = burst;
                shellTrail.emitting = false;
                for (int i = 0; i < used; i++)
                {
                    sparks[i].gameObject.SetActive(true);
                    sparks[i].position = burst;
                    sparkTrails[i].Clear();
                    sparkTrails[i].emitting = true;
                }
            }

            // 開いた瞬間、大きくぱっと光ってすぐ消える
            if (t < FlashTime) shell.localScale = Vector3.one * radius * 0.45f * (1f - t / FlashTime);
            else if (shell.localScale.x > 0.002f) shell.localScale = Vector3.one * 0.001f;

            float spread = radius * (1f - Mathf.Exp(-t * 3.2f)); // ぱっと広がって、だんだん止まる
            float fall = 0.5f * gravity * t * t;
            float fade = Mathf.Clamp01(1f - t / life);
            bool glitter = t > life * 0.55f; // 終わりぎわは、きらきら瞬く
            for (int i = 0; i < used; i++)
            {
                float flicker = glitter ? (Mathf.Sin((t + twinkle[i]) * 38f) > -0.2f ? 1f : 0.15f) : 0.85f + 0.15f * Mathf.Sin(t * 30f + twinkle[i]);
                float size = sparkSize * fade * flicker;
                sparks[i].position = burst + dirs[i] * (spread * reach[i]) + Vector3.down * fall;
                sparks[i].localScale = Vector3.one * size;
                sparkTrails[i].widthMultiplier = sparkSize * 0.7f * fade;
                if (t > life - trailTime) sparkTrails[i].emitting = false; // 消える前に尾を引き終える
            }
            if (t >= life) FxPool.Release(PoolKey, gameObject);
        }
    }
}
