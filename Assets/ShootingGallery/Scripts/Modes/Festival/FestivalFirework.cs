using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 夏祭りモードの打ち上げ花火 1発。光の玉がひゅるる…と空高く上がり、てっぺんで「ドーン」と火の粉が広がって、垂れながら消えていく。
    /// 開き方は3種類: 丸く全方向に開く (菊) / 輪になって開く (輪) / 金色の火の粉が長く垂れる (しだれ柳)。
    /// 使い終わったら FxPool にしまって使い回す (火の粉の球も作り直さない)。
    /// </summary>
    public class FestivalFirework : MonoBehaviour
    {
        public enum Style { Peony, Ring, Willow }

        const int Sparks = 40;
        const string PoolKey = "FestivalFirework";

        Transform shell;
        Transform[] sparks;
        Renderer[] sparkRenderers;
        readonly Vector3[] dirs = new Vector3[Sparks];
        Vector3 from, burst;
        float riseTime, age, radius, gravity, life, sparkSize;
        bool burstDone;

        /// <summary>from から burst (開く所) へ打ち上げる。radius = 開いた大きさ (m)</summary>
        public static void Launch(Vector3 from, Vector3 burst, float radius, Style style, Material color, Material shellMaterial)
        {
            var go = FxPool.Get(PoolKey, () => Create(shellMaterial));
            var f = go.GetComponent<FestivalFirework>();
            f.from = from;
            f.burst = burst;
            f.radius = radius;
            f.age = 0f;
            f.burstDone = false;
            f.riseTime = Random.Range(1.3f, 1.8f);
            f.gravity = style == Style.Willow ? 3.2f : 1.2f;
            f.life = style == Style.Willow ? 3.2f : 2f;
            f.sparkSize = radius * (style == Style.Willow ? 0.06f : 0.075f);
            f.shell.gameObject.SetActive(true);
            f.shell.position = from;
            for (int i = 0; i < Sparks; i++)
            {
                f.sparks[i].gameObject.SetActive(false);
                f.sparkRenderers[i].sharedMaterial = color;
            }
            // 開く向き
            if (style == Style.Ring)
            {
                // 少し傾いた縦の輪 (プレイヤーの方から見て丸く見えるように、輪の面を中心へ向ける)
                Vector3 toCenter = Vector3.ProjectOnPlane(from - burst, Vector3.up).normalized;
                var rot = Quaternion.LookRotation(toCenter) * Quaternion.Euler(Random.Range(-25f, 25f), 0f, Random.Range(-30f, 30f));
                for (int i = 0; i < Sparks; i++)
                {
                    float a = i * Mathf.PI * 2f / Sparks;
                    f.dirs[i] = rot * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                }
            }
            else
            {
                // 球の表面に均等に (フィボナッチ球)
                float off = Random.value * Mathf.PI * 2f;
                for (int i = 0; i < Sparks; i++)
                {
                    float y = 1f - (i + 0.5f) * 2f / Sparks;
                    float r = Mathf.Sqrt(1f - y * y);
                    float a = i * 2.39996f + off;
                    f.dirs[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                }
            }
        }

        static GameObject Create(Material shellMaterial)
        {
            var g = new GameObject(PoolKey);
            var f = g.AddComponent<FestivalFirework>();
            f.shell = FxPool.Primitive(PrimitiveType.Sphere, shellMaterial).transform;
            f.shell.SetParent(g.transform, false);
            f.shell.localScale = Vector3.one * 0.35f;
            f.sparks = new Transform[Sparks];
            f.sparkRenderers = new Renderer[Sparks];
            for (int i = 0; i < Sparks; i++)
            {
                var s = FxPool.Primitive(PrimitiveType.Sphere, shellMaterial);
                s.transform.SetParent(g.transform, false);
                s.SetActive(false);
                f.sparks[i] = s.transform;
                f.sparkRenderers[i] = s.GetComponent<Renderer>();
            }
            return g;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age < riseTime)
            {
                // ひゅるる…と上がる (だんだん遅くなる。少しゆらゆら)
                float k = age / riseTime;
                float e = 1f - (1f - k) * (1f - k);
                shell.position = Vector3.Lerp(from, burst, e) + new Vector3(Mathf.Sin(age * 9f) * 0.15f, 0f, 0f);
                return;
            }
            if (!burstDone)
            {
                burstDone = true;
                shell.gameObject.SetActive(false);
                foreach (var s in sparks) s.gameObject.SetActive(true); // 音は鳴らさない (ユーザーの指定)
            }
            float t = age - riseTime;
            float spread = radius * (1f - Mathf.Exp(-t * 3.2f)); // ぱっと広がって、だんだん止まる
            float fall = 0.5f * gravity * t * t;
            float size = sparkSize * Mathf.Clamp01(1f - t / life) * (0.85f + 0.15f * Mathf.Sin(t * 30f)); // ちかちか瞬きながら小さくなる
            for (int i = 0; i < Sparks; i++)
            {
                sparks[i].position = burst + dirs[i] * spread + Vector3.down * fall;
                sparks[i].localScale = Vector3.one * size;
            }
            if (t >= life) FxPool.Release(PoolKey, gameObject);
        }
    }
}
