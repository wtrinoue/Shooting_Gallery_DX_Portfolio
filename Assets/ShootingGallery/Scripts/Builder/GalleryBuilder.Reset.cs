using UnityEngine;

namespace ShootingGallery
{
    /// <summary>ボタン長押し (A = リセット / B = エンドレスモード) のインジケーターの生成。</summary>
    public partial class GalleryBuilder
    {
        /// <summary>
        /// ボタン長押しのインジケーター。カメラの子で、視界の少し下に浮かぶ輪 (24 個の目盛り) と文字。
        /// 光の影響を受けない色にして、暗い場所でも見えるようにする。
        /// </summary>
        ResetIndicator BuildResetIndicator(Transform head, string name = "ResetIndicator", string holdText = "HOLD A : RESET", string doneText = "RESET!", Color? onColor = null)
        {
            var root = Group(head, name);
            root.localPosition = new Vector3(0f, -0.12f, 0.7f);
            var visual = Group(root, "Visual");
            var on = Mat(name == "ResetIndicator" ? "Reset_On" : $"{name}_On",onColor ?? new Color(1f, 0.45f, 0.15f), null, true);
            var off = Mat("Reset_Off", new Color(0.18f, 0.18f, 0.22f), null, true);

            const int Segments = 24;
            const float Radius = 0.06f;
            var segments = new Renderer[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float a = Mathf.PI / 2f - i * Mathf.PI * 2f / Segments; // 上から時計回り
                var seg = Prim(PrimitiveType.Cube, "Segment", visual, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * Radius,
                               new Vector3(0.012f, 0.006f, 0.002f), off, false);
                seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg); // 長い方を中心に向ける
                seg.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                segments[i] = seg.GetComponent<Renderer>();
            }
            var label = MakeText(visual, "Label", holdText, new Vector3(0f, -0.09f, 0f), 0.022f, Color.white, overlay: true);

            var indicator = root.gameObject.AddComponent<ResetIndicator>();
            indicator.visual = visual.gameObject;
            indicator.segments = segments;
            indicator.onMaterial = on;
            indicator.offMaterial = off;
            indicator.label = label;
            indicator.holdText = holdText;
            indicator.doneText = doneText;
            visual.gameObject.SetActive(false); // 押している間だけ出る
            return indicator;
        }
    }
}
