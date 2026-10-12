using UnityEngine;

namespace ShootingGallery
{
    /// <summary>的を倒した位置に出る「+10」などの文字。</summary>
    public class ScorePopup : MonoBehaviour
    {
        const float Lifetime = 0.9f;
        TextMesh text;
        Color baseColor;
        float age;

        public static void Spawn(Font font, Vector3 position, string message, Color color, float height = 0.12f)
        {
            if (font == null) return;
            var go = new GameObject("ScorePopup");
            go.transform.position = position;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = message;
            tm.fontSize = 64;
            tm.characterSize = height / 6.4f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            var p = go.AddComponent<ScorePopup>();
            p.text = tm;
            p.baseColor = color;
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (0.5f * Time.deltaTime);
            if (Camera.main != null)
                transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
            var c = baseColor;
            c.a = 1f - age / Lifetime;
            text.color = c;
            if (age >= Lifetime) Destroy(gameObject);
        }
    }
}
