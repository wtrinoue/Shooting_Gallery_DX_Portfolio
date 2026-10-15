using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 手前の物に隠れる 3D の文字 (Shaders/Text3DDepth.shader) に、フォントの文字の画像を渡す。
    /// フォント (Resources/Fonts/MPLUS1p-Medium) の文字の画像は実行中に作られ、使う文字が増えると作り直されるので、
    /// シーンを読み込んだ時と作り直された時 (Font.textureRebuilt) に、そのシェーダーを使っている文字のマテリアルへ付け直す。
    /// エディターでシーンを開いた時も付け直す (付いていないと文字が四角く塗りつぶされて見えるため)
    /// </summary>
    public static class DepthText
    {
        public const string ShaderName = "ShootingGallery/Text 3D (Depth Tested)";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Font.textureRebuilt -= Refresh;
            Font.textureRebuilt += Refresh;
            Refresh(null);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void EditorInit()
        {
            Font.textureRebuilt -= Refresh;
            Font.textureRebuilt += Refresh;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (scene, mode) => Refresh(null);
            UnityEditor.EditorApplication.delayCall += () => Refresh(null);
        }
#endif

        /// <summary>font の文字の画像を、そのフォントで手前の物に隠れる文字のマテリアルへ付け直す (null なら全部のフォント)</summary>
        public static void Refresh(Font font)
        {
            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tm.font == null || (font != null && tm.font != font)) continue;
                var r = tm.GetComponent<MeshRenderer>();
                var mat = r != null ? r.sharedMaterial : null;
                if (mat == null || mat.shader == null || mat.shader.name != ShaderName) continue;
                var tex = tm.font.material != null ? tm.font.material.mainTexture : null;
                if (mat.mainTexture != tex) mat.mainTexture = tex;
            }
        }
    }
}
