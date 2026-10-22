using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 文字 (TextMesh) の黒いふち取り。同じ文字を黒で8方向に少しずつずらして、元の文字の後ろに描く。
    /// 明るい所でも暗い所でも読めるように (いつも目の前に出る HUD の文字など)。文字や大きさが変わったら、ふちも合わせる
    /// </summary>
    [RequireComponent(typeof(TextMesh))]
    public class TextOutline : MonoBehaviour
    {
        [Tooltip("ふちの太さ (この文字のローカルの m)")]
        public float thickness = 0.006f;
        public Color color = Color.black;

        TextMesh source;
        MeshRenderer sourceRenderer;
        TextMesh[] copies;
        MeshRenderer[] copyRenderers;
        string lastText;
        float lastAlpha = -1f;
        float placedThickness = -1f;

        static readonly Vector2[] Dirs =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.71f, 0.71f), new Vector2(-0.71f, 0.71f), new Vector2(0.71f, -0.71f), new Vector2(-0.71f, -0.71f),
        };

        void Awake()
        {
            source = GetComponent<TextMesh>();
            sourceRenderer = GetComponent<MeshRenderer>();
            copies = new TextMesh[Dirs.Length];
            copyRenderers = new MeshRenderer[Dirs.Length];
            for (int i = 0; i < Dirs.Length; i++)
            {
                var go = new GameObject("Outline");
                go.transform.SetParent(transform, false);
                // (位置は LateUpdate で太さに合わせて置く)
                var tm = go.AddComponent<TextMesh>();
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = sourceRenderer.sharedMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.sortingOrder = sourceRenderer.sortingOrder - 1; // 元の文字より先に描く (後ろに来る)
                copies[i] = tm;
                copyRenderers[i] = r;
            }
            SyncStyle();
        }

        // 文字の形の設定を合わせる (フォント・大きさ・そろえ方)
        void SyncStyle()
        {
            foreach (var tm in copies)
            {
                tm.font = source.font;
                tm.fontSize = source.fontSize;
                tm.fontStyle = source.fontStyle;
                tm.characterSize = source.characterSize;
                tm.lineSpacing = source.lineSpacing;
                tm.anchor = source.anchor;
                tm.alignment = source.alignment;
                tm.richText = source.richText;
                tm.tabSize = source.tabSize;
            }
            lastText = null;
        }

        void LateUpdate()
        {
            if (copies == null) return;
            if (!Mathf.Approximately(placedThickness, thickness))
            {   // ふちの太さに合わせて、8方向へずらして置く (元の文字の少し後ろ)
                placedThickness = thickness;
                for (int i = 0; i < copies.Length; i++)
                    copies[i].transform.localPosition = new Vector3(Dirs[i].x * thickness, Dirs[i].y * thickness, 0.001f);
            }
            if (copies[0].characterSize != source.characterSize || copies[0].anchor != source.anchor || copies[0].fontSize != source.fontSize) SyncStyle();
            bool visible = sourceRenderer.enabled;
            if (copyRenderers[0].enabled != visible)
                foreach (var r in copyRenderers) r.enabled = visible;
            if (source.text != lastText)
            {
                lastText = source.text;
                foreach (var tm in copies) tm.text = lastText;
            }
            float alpha = source.color.a;
            if (!Mathf.Approximately(alpha, lastAlpha))
            {
                lastAlpha = alpha;
                var c = new Color(color.r, color.g, color.b, color.a * alpha);
                foreach (var tm in copies) tm.color = c;
            }
        }
    }
}
