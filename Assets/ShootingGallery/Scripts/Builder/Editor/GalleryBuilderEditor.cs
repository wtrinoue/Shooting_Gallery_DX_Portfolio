using UnityEditor;
using UnityEngine;

namespace ShootingGallery.EditorTools
{
    /// <summary>GalleryBuilder のインスペクターに「シーンに生成」ボタンを出す。</summary>
    [CustomEditor(typeof(GalleryBuilder))]
    public class GalleryBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var builder = (GalleryBuilder)target;

            EditorGUILayout.HelpBox(
                "「シーンに生成」を押すと、子オブジェクト「" + GalleryBuilder.WorldName + "」にゲーム一式が作られます。\n" +
                "それをコピーして好きなシーンに貼り付けるだけで、再生と同時にゲームが動きます。\n" +
                "(貼り付けた後、この GalleryBuilder は削除して構いません)",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                bool exists = builder.transform.Find(GalleryBuilder.WorldName) != null;
                if (GUILayout.Button(exists ? "作り直す (今の生成物は置き換わります)" : "シーンに生成", GUILayout.Height(32)))
                    builder.BuildIntoScene();

                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    "「アセットだけ生成」は、材質・メッシュなどのアセットだけを作って保存し、シーンにはゲーム一式を残しません。\n" +
                    "シーンには GalleryBuilder だけを置いておき、再生した時に組み立てる使い方の時に押します (ゲーム一式が大きく、シーンに入れると GitHub に送れないため)。\n" +
                    "最後に、実機用のアセット一覧 (Resources/GalleryRuntimeAssets) もそろえます。",
                    MessageType.None);
                if (GUILayout.Button("アセットだけ生成 (シーンには残さない)", GUILayout.Height(28)))
                    builder.GenerateAssetsOnly();
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
