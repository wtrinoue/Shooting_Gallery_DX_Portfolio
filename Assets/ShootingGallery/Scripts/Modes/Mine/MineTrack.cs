using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 発掘モードの線路の道すじ (点を順につないだ折れ線。points は親 = 洞窟のローカル座標)。
    /// loop なら最後の点から最初の点へつながって、ぐるぐる回る線路。そうでなければ、トンネルの穴から出てきて別の穴へ入る線路。
    /// </summary>
    public class MineTrack : MonoBehaviour
    {
        public Vector3[] points = new Vector3[0];
        public bool loop;

        float[] cumulative; // 各点までの道のりの長さ

        /// <summary>線路の長さ (m)</summary>
        public float Length { get { Prepare(); return cumulative[cumulative.Length - 1]; } }

        void Prepare()
        {
            int n = points.Length + (loop ? 1 : 0);
            if (cumulative != null && cumulative.Length == n) return;
            cumulative = new float[Mathf.Max(1, n)];
            for (int i = 1; i < n; i++)
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[(i - 1) % points.Length], points[i % points.Length]);
        }

        /// <summary>はじめから dist (m) 進んだ所の位置と進む向き (ループの線路は一周で元に戻る)</summary>
        public void Evaluate(float dist, out Vector3 pos, out Vector3 forward)
        {
            Prepare();
            pos = points.Length > 0 ? points[0] : Vector3.zero;
            forward = Vector3.forward;
            if (points.Length < 2) return;
            float len = cumulative[cumulative.Length - 1];
            dist = loop ? Mathf.Repeat(dist, len) : Mathf.Clamp(dist, 0f, len);
            int i = 1;
            while (i < cumulative.Length - 1 && cumulative[i] < dist) i++;
            Vector3 a = points[(i - 1) % points.Length], b = points[i % points.Length];
            float seg = cumulative[i] - cumulative[i - 1];
            float t = seg > 0f ? (dist - cumulative[i - 1]) / seg : 0f;
            pos = Vector3.Lerp(a, b, t);
            if ((b - a).sqrMagnitude > 0.0001f) forward = (b - a).normalized;
        }
    }
}
