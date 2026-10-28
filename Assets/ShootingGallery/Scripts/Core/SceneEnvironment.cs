using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 夜祭りの空気感 (環境光・霧) を再生時に適用し、シーンに元からあるカメラを止める。
    /// 霧や環境光は「シーンの設定」なのでオブジェクトをコピペしても付いてこない。
    /// そのため、このコンポーネントが再生時に毎回設定し直す。
    /// </summary>
    public class SceneEnvironment : MonoBehaviour
    {
        public Color ambientColor = new Color(0.42f, 0.4f, 0.5f);
        public bool fog = true;
        public Color fogColor = new Color(0.05f, 0.05f, 0.14f);
        public float fogStart = 12f;
        public float fogEnd = 30f;
        [Tooltip("このオブジェクトの子以外のカメラ (元からある Main Camera など) を止める")]
        public bool disableOtherCameras = true;

        /// <summary>
        /// 環境光 (まわりの明るさ) を変える。ambientLight を変えるだけだと、エディタでは自動で計算し直されて効くが、
        /// ビルドしたアプリ (Quest) では計算し直されず、元の明るさのまま残る (発掘モードの洞窟が明るく見えた原因)。
        /// そのため、明かりの計算に実際に使われる環境光のデータ (ambientProbe) も、同じ色で作り直す
        /// </summary>
        public static void SetAmbient(Color color)
        {
            RenderSettings.ambientLight = color;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2();
            sh.AddAmbientLight(QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
            RenderSettings.ambientProbe = sh;
        }

        void Awake()
        {
            WarnMissingMaterials();

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            SetAmbient(ambientColor);
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;

            if (!disableOtherCameras) return;
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (c.transform.IsChildOf(transform)) continue;
                c.gameObject.tag = "Untagged";
                c.enabled = false;
                var listener = c.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }

        // 再生中に生成されたものをコピペすると、マテリアルが外れて全部ピンクになる。その時に原因を知らせる。
        // 形 (メッシュ) がまだない物は描かれない (ピンクにならない) ので数えない (Unknown ステージの、使い回しの割れる部品の置き場など)
        void WarnMissingMaterials()
        {
            int missing = 0;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) || r.GetComponent<TextMesh>() != null || r.sharedMaterial != null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                missing++;
            }
            if (missing == 0) return;

            Debug.LogError(
                $"[ShootingGallery] マテリアルが外れているオブジェクトが {missing} 個あります (ピンク色に表示されます)。\n" +
                "再生中に生成されたものをコピーして貼り付けると、再生終了と同時にマテリアルが消えるためこうなります。\n" +
                "直し方: 貼り付けたものを削除し、再生を止めた状態で GalleryBuilder の「シーンに生成」ボタンを押して、" +
                "できた ShootingGallery をコピーしてください。", this);
        }
    }
}
