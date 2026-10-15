using System.Collections.Generic;
using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 作品のまわりの「イマジネーションが形を変える範囲」(当たり判定とは別)。イマジネーションの玉がこの中に入ると、
    /// キャンバスの近くでは筆に、彫刻の近くではトンカチに変わる (ImaginationShot が飛びながら調べる)。
    /// 物理の当たり判定は使わず、出ている作品の球の範囲を一覧から調べるだけ (作品の数は少ないので軽い。ほかの武器の当たり判定にもじゃまをしない)
    /// </summary>
    public class ArtField : MonoBehaviour
    {
        public Artwork.Kind kind = Artwork.Kind.Canvas;
        [Tooltip("範囲の真ん中 (この物のローカル)")]
        public Vector3 center;
        [Tooltip("範囲の半径 (m。この物の大きさの倍率がかかる)")]
        public float radius = 1.2f;

        static readonly List<ArtField> active = new List<ArtField>();

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        /// <summary>point がどれかの作品の範囲の中なら true (kind にその作品の種類。いくつも重なる時は、真ん中に一番近い作品)</summary>
        public static bool TryFind(Vector3 point, out Artwork.Kind kind)
        {
            kind = Artwork.Kind.Canvas;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < active.Count; i++)
            {
                var f = active[i];
                var t = f.transform;
                float r = f.radius * t.lossyScale.x;
                float d = (t.TransformPoint(f.center) - point).sqrMagnitude;
                if (d < r * r && d < best)
                {
                    best = d;
                    kind = f.kind;
                    found = true;
                }
            }
            return found;
        }
    }
}
