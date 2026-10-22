using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 懐中電灯で照らされた時間を、的ごとにためておく (幽霊モード)。照らすとすぐ消えるのではなく、照らされた時間が Required 秒たまるとやられる。
    /// 照らすのをやめると、少し間をおいてから、たまった分がゆっくり減っていく (少しずつ照らしても積み重なる)
    /// </summary>
    public static class LightExposure
    {
        /// <summary>やられるまでに照らす秒数</summary>
        public const float Required = 0.5f;
        /// <summary>照らすのをやめてから減り始めるまでの秒数 / 減る速さ (秒/秒)</summary>
        const float Grace = 0.25f, Decay = 0.5f;

        static readonly Dictionary<Target, (float amount, float time)> map = new Dictionary<Target, (float, float)>();

        static float Current(Target t)
        {
            if (t == null || !map.TryGetValue(t, out var e)) return 0f;
            float idle = Time.time - e.time - Grace;
            return idle > 0f ? Mathf.Max(0f, e.amount - idle * Decay) : e.amount;
        }

        /// <summary>dt 秒照らした。たまりきったら true (ためた分は消す)</summary>
        public static bool Add(Target t, float dt)
        {
            if (t == null) return false;
            float a = Current(t) + dt;
            if (a >= Required)
            {
                map.Remove(t);
                return true;
            }
            map[t] = (a, Time.time);
            return false;
        }

        /// <summary>どれだけたまったか (0〜1)。的の揺れなどの見た目に使う</summary>
        public static float Progress(Target t) => Mathf.Clamp01(Current(t) / Required);

        /// <summary>ためた分を消す (的が出直した時)</summary>
        public static void Clear(Target t)
        {
            if (t != null) map.Remove(t);
        }
    }
}
