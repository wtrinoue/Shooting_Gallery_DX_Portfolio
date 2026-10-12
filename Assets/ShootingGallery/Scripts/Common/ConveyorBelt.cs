using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// items をローカルX方向へ流し、端まで行ったら反対側へ戻す (ベルトコンベアー)。
    /// waveHeight を入れると、流れながら上下に波打つ (アヒルの行進)。
    /// </summary>
    public class ConveyorBelt : MonoBehaviour
    {
        public Transform[] items;
        public float length = 3.4f;
        public float speed = 0.5f; // m/秒 (マイナスで逆向き)

        [Header("波打ち (0 なら平ら)")]
        public float waveHeight;
        [Tooltip("ベルトの長さの中に入る波の数")]
        public float waves = 2f;
        [Tooltip("ONの item だけ波打たせる (OFF なら全部)。板などの飾りを平らなままにしたい時用")]
        public bool waveTargetsOnly = true;

        float[] baseY;
        bool[] waving;

        void Awake()
        {
            baseY = new float[items.Length];
            waving = new bool[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                baseY[i] = items[i].localPosition.y;
                waving[i] = !waveTargetsOnly || items[i].GetComponent<Target>() != null;
            }
        }

        void Update()
        {
            float half = length * 0.5f;
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var p = item.localPosition;
                p.x += speed * Time.deltaTime;
                if (p.x > half) p.x -= length;
                else if (p.x < -half) p.x += length;

                if (waveHeight > 0f && waving[i])
                    p.y = baseY[i] + waveHeight * Mathf.Sin((p.x / length) * waves * 2f * Mathf.PI);
                item.localPosition = p;
            }
        }
    }
}
