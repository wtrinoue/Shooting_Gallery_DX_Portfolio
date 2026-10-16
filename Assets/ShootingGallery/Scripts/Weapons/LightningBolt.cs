using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 紫の電撃 (Unknown ステージの武器のまっすぐな太いレーザーと、撃たれた物が別の世界へ転送される時のびりびり) の稲妻 1本。
    /// 2点の間を折れ線で結び、短い間隔で形を作り直してびりびり震わせる (jaggedness = 0 ならまっすぐ)。life 秒で消える (FxPool で使い回す)。
    /// follow を渡すと、始まりの点がその物について動く (縮みながら消える物の中心から出るびりびり)。
    /// </summary>
    public class LightningBolt : MonoBehaviour
    {
        const string PoolKey = "LightningBolt";
        const int Points = 10;
        const float Rejitter = 0.035f; // 形を作り直す間隔 (秒)

        LineRenderer line;
        Vector3 from, to;
        Transform follow;
        Vector3 followOffset;
        float age, life, jaggedness, width, taper, nextJitter;

        /// <summary>from から to へ稲妻を出す。jaggedness = 折れ曲がりの大きさ (長さに対する割合)、taper = 先の太さ (始まりの太さに対する割合)</summary>
        public static LightningBolt Spawn(Vector3 from, Vector3 to, Material mat, float width, float life, float jaggedness = 0.12f, Transform follow = null, float taper = 0.35f)
        {
            var go = FxPool.Get(PoolKey, () =>
            {
                var g = new GameObject("LightningBolt");
                var lr = g.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = Points;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                g.AddComponent<LightningBolt>().line = lr;
                return g;
            });
            var bolt = go.GetComponent<LightningBolt>();
            bolt.line.sharedMaterial = mat;
            bolt.from = from;
            bolt.to = to;
            bolt.follow = follow;
            bolt.followOffset = follow != null ? from - follow.position : Vector3.zero;
            bolt.age = 0f;
            bolt.life = life;
            bolt.width = width;
            bolt.taper = taper;
            bolt.jaggedness = jaggedness;
            bolt.nextJitter = 0f;
            bolt.Shape();
            return bolt;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life)
            {
                follow = null;
                FxPool.Release(PoolKey, gameObject);
                return;
            }
            if (follow != null)
            {
                var shift = follow.position + followOffset - from;
                from += shift;
                to += shift;
            }
            if (age >= nextJitter) Shape();
            // 終わりに向けて細くなる
            float k = 1f - age / life;
            line.startWidth = width * k;
            line.endWidth = width * taper * k;
        }

        // 折れ線の形を作り直す (両端は動かさず、間の点を横にずらす。真ん中ほど大きく)
        void Shape()
        {
            nextJitter = age + Rejitter;
            var dir = to - from;
            float len = dir.magnitude;
            var side = Vector3.Cross(dir, Random.onUnitSphere).normalized;
            var up = Vector3.Cross(dir, side).normalized;
            for (int i = 0; i < Points; i++)
            {
                float u = i / (float)(Points - 1);
                var p = from + dir * u;
                if (i > 0 && i < Points - 1)
                {
                    float amp = len * jaggedness * Mathf.Sin(u * Mathf.PI);
                    p += side * Random.Range(-amp, amp) + up * Random.Range(-amp, amp);
                }
                line.SetPosition(i, p);
            }
            line.startWidth = width;
            line.endWidth = width * taper;
        }
    }
}
