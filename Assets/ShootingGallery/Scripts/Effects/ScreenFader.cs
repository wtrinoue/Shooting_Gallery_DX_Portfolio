using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 目の前を暗くする (場面の切り替え用)。カメラのすぐ前に置いた黒い板の透明度を少しずつ変える。
    /// 覆っている間に舞台を入れ替えると、床が動くのを見せずに別の場所へ来たように見える (カジノの暗転、西部劇の砂嵐など)。
    /// 色を渡すと、その色で覆う (西部劇は砂の色)。渡さなければ黒。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ScreenFader : MonoBehaviour
    {
        static ScreenFader instance;
        Renderer rend;
        Material mat;
        Color baseColor;
        float alpha, target, speed = 1f;

        /// <summary>真っ暗になりきっているか</summary>
        public static bool IsBlack => instance != null && instance.alpha >= 0.999f;

        void Awake()
        {
            instance = this;
            rend = GetComponent<Renderer>();
            mat = rend.material; // 実行中だけの複製 (元の材質は変えない)
            baseColor = mat.GetColor("_BaseColor");
            Apply();
        }

        /// <summary>duration 秒かけて、覆い方 a (0 = 見える, 1 = 何も見えない) にする。color を渡すとその色で覆う (省略すると黒)</summary>
        public static void FadeTo(float a, float duration, Color? color = null)
        {
            if (instance == null) return;
            // 色は覆い始める時だけ変える (晴れていく途中で色が変わらないように)
            if (a > instance.alpha || instance.alpha <= 0.001f)
            {
                var c = color ?? Color.black;
                instance.baseColor = new Color(c.r, c.g, c.b, instance.baseColor.a);
                instance.Apply();
            }
            instance.target = Mathf.Clamp01(a);
            instance.speed = Mathf.Abs(instance.target - instance.alpha) / Mathf.Max(0.01f, duration);
        }

        void Update()
        {
            if (Mathf.Approximately(alpha, target)) return;
            alpha = Mathf.MoveTowards(alpha, target, speed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            var c = baseColor;
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
            rend.enabled = alpha > 0.001f;
        }
    }
}
