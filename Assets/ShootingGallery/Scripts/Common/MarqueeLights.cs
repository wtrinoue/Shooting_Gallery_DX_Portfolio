using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 看板のまわりの電球を、順番に点けたり消したりして光が流れて見えるようにする (豪華屋台の電飾)。
    /// bulbs を spacing 個おきに点け、step 秒ごとに1つずつずらす。
    /// </summary>
    public class MarqueeLights : MonoBehaviour
    {
        public Renderer[] bulbs;
        public Material onMaterial;
        public Material offMaterial;
        [Tooltip("何個おきに点けるか")]
        public int spacing = 3;
        [Tooltip("光が1つ進む秒数")]
        public float step = 0.12f;
        [Tooltip("ONなら逆向きに流れる")]
        public bool reverse;

        int offset;
        float timer;

        void OnEnable() => Apply();

        void Update()
        {
            timer += Time.deltaTime;
            if (timer < step) return;
            timer -= step;
            offset = (offset + (reverse ? spacing - 1 : 1)) % Mathf.Max(1, spacing);
            Apply();
        }

        void Apply()
        {
            if (bulbs == null) return;
            int n = Mathf.Max(1, spacing);
            for (int i = 0; i < bulbs.Length; i++)
                if (bulbs[i] != null) bulbs[i].sharedMaterial = (i + offset) % n == 0 ? onMaterial : offMaterial;
        }
    }
}
