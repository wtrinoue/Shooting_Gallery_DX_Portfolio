using UnityEngine;

namespace ShootingGallery
{
    /// <summary>点数ごとの見た目 (色・大きさ)。点数が高いほど小さく、派手になる。</summary>
    public static class PointStyle
    {
        public static readonly int[] Values = { 10, 50, 100, 200, 300, 500 };

        public static Color TargetColor(int points) => points switch
        {
            >= 1000 => new Color(1f, 0.25f, 0.75f),  // 桃 (豪華屋台だけに出る)
            >= 500 => new Color(1f, 0.75f, 0.1f),    // 金
            >= 300 => new Color(0.78f, 0.8f, 0.88f), // 銀
            >= 200 => new Color(0.65f, 0.3f, 0.9f),  // 紫
            >= 100 => new Color(0.2f, 0.45f, 1f),    // 青
            >= 50 => new Color(0.2f, 0.75f, 0.3f),   // 緑
            _ => new Color(0.1f, 0.15f, 0.4f)        // 紺
        };

        public static Color PopupColor(int points) => points < 50 ? Color.white : TargetColor(points);

        public static float Diameter(int points) => points switch
        {
            >= 1000 => 0.14f,
            >= 500 => 0.15f,
            >= 300 => 0.18f,
            >= 200 => 0.2f,
            >= 100 => 0.22f,
            >= 50 => 0.24f,
            _ => 0.26f
        };

        /// <summary>高得点の的は光らせる</summary>
        public static Color? Emission(int points) => points >= 300 ? TargetColor(points) * 0.45f : (Color?)null;
    }
}
