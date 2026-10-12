using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// ボタン長押し (A = リセット / B = エンドレスモード) のたまり具合を、視界の少し下に輪で表示する。カメラの子に置く。
    /// 押している間だけ表示され、輪の目盛りが時計回りに点いていき、全部点くと「RESET!」になる。
    /// </summary>
    public class ResetIndicator : MonoBehaviour
    {
        [Tooltip("表示 / 非表示を切り替える見た目のルート")]
        public GameObject visual;
        [Tooltip("輪の目盛り (上から時計回りの順)")]
        public Renderer[] segments;
        public Material onMaterial;
        public Material offMaterial;
        public TextMesh label;
        [Tooltip("押している間の文字")]
        public string holdText = "HOLD A : RESET";
        [Tooltip("たまりきった時の文字")]
        public string doneText = "RESET!";

        int lit = -1;

        void Awake() => SetProgress(0f, false);

        /// <summary>progress = 0〜1。holding = 押しているか (押していない時は隠す)</summary>
        public void SetProgress(float progress, bool holding)
        {
            bool show = holding && progress > 0.04f; // ちょっと触っただけでは出さない
            if (visual != null && visual.activeSelf != show) visual.SetActive(show);
            if (!show) return;

            int n = Mathf.Clamp(Mathf.FloorToInt(progress * segments.Length + 0.0001f), 0, segments.Length);
            if (n == lit) return;
            lit = n;
            for (int i = 0; i < segments.Length; i++)
                if (segments[i] != null) segments[i].sharedMaterial = i < n ? onMaterial : offMaterial;
            if (label != null) label.text = progress >= 1f ? doneText : holdText;
        }
    }
}
