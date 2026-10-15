using System.Collections.Generic;

namespace ShootingGallery
{
    /// <summary>点数の文字 (点数ごとに一度だけ作って使い回す。的が出るたびに文字列を作ると GC で止まるので)</summary>
    public static class PointText
    {
        static readonly Dictionary<int, string> texts = new Dictionary<int, string>();

        public static string Of(int points)
        {
            if (!texts.TryGetValue(points, out var s)) texts.Add(points, s = points.ToString());
            return s;
        }
    }
}
