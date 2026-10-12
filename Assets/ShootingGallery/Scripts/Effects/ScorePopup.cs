using UnityEngine;

namespace ShootingGallery
{
    /// <summary>的を倒した位置に出る「+10」などの文字。使い終わったら FxPool にしまって使い回す。</summary>
    public class ScorePopup : MonoBehaviour
    {
        const float Lifetime = 0.9f;
        const string PoolKey = "ScorePopup";
        TextMesh text;
        Color baseColor;
        float age;

        public static void Spawn(Font font, Vector3 position, string message, Color color, float height = 0.12f)
        {
            if (font == null) return;
            var go = FxPool.Get(PoolKey, () =>
            {
                var g = new GameObject(PoolKey);
                var tm = g.AddComponent<TextMesh>();
                tm.fontSize = 64;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                g.AddComponent<ScorePopup>().text = tm;
                return g;
            });
            go.transform.position = position;
            var p = go.GetComponent<ScorePopup>();
            var t = p.text;
            if (t.font != font)
            {
                t.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            t.characterSize = height / 6.4f;
            t.text = message;
            t.color = color;
            p.baseColor = color;
            p.age = 0f;
            p.FaceCamera();
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (0.5f * Time.deltaTime);
            FaceCamera();
            var c = baseColor;
            c.a = 1f - age / Lifetime;
            text.color = c;
            if (age >= Lifetime) FxPool.Release(PoolKey, gameObject);
        }

        void FaceCamera()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
