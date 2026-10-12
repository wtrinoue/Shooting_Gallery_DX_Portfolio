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
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
