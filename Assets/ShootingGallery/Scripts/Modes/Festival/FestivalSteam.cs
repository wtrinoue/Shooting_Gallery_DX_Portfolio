using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 鉄板や鍋から立ちのぼる湯気 (夏祭りの食べ物の屋台の調理場)。子の半透明の玉が、ゆらゆら揺れながら上へのぼり、
    /// ふくらみながら小さく消えていくのをくり返す (玉ごとに時間をずらす)。
    /// </summary>
    public class FestivalSteam : MonoBehaviour
    {
        [Tooltip("のぼる高さ (m)")]
        public float height = 0.6f;
        [Tooltip("1つの玉がのぼって消えるまでの秒数")]
        public float period = 2.2f;
        [Tooltip("玉の大きさ (m)")]
        public float size = 0.14f;

        Transform[] puffs;

        void Awake()
        {
            puffs = new Transform[transform.childCount];
            for (int i = 0; i < puffs.Length; i++) puffs[i] = transform.GetChild(i);
        }

        void Update()
        {
            float now = Time.time;
            for (int i = 0; i < puffs.Length; i++)
            {
                float t = Mathf.Repeat(now / period + i / (float)puffs.Length, 1f);
                puffs[i].localPosition = new Vector3(Mathf.Sin(t * 5f + i * 1.7f) * 0.05f, t * height, Mathf.Cos(t * 4f + i) * 0.03f);
                puffs[i].localScale = Vector3.one * (size * (0.5f + 1.3f * t) * (1f - t * t));
            }
        }
    }
}
