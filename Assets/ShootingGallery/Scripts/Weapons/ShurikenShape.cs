using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 手裏剣の形 (MonoBehaviour ではない、形を作るだけの道具)。真ん中に紺色の丸、そこから四方へ灰色の三角形の刃が伸びる4方手裏剣。
    /// 手に持つ武器 (GalleryBuilder.Player)、投げて飛ぶ手裏剣 (Shuriken.Spawn)、忍者の的が持つ手裏剣と、忍者モードへ行く的の絵 (GalleryBuilder.Ninja) が使う。
    /// (前は平たい棒を4本、45度ずつずらして重ねた8方手裏剣で、歯車のように見えていた)
    /// </summary>
    public static class ShurikenShape
    {
        public static readonly Color BladeColor = new Color(0.55f, 0.57f, 0.6f);  // 刃の灰色
        public static readonly Color CenterColor = new Color(0.08f, 0.12f, 0.36f); // 真ん中の紺色

        // 形の割合 (刃の先までの半径を 1 とする)
        const float BaseDistance = 0.12f; // 刃の付け根 (三角形の底辺) の、真ん中からの距離
        const float BaseHalfWidth = 0.3f; // 刃の付け根の幅の半分
        const float HalfThickness = 0.04f; // 刃の厚みの半分
        const float CenterRadius = 0.36f;  // 紺色の丸の半径
        const float CenterHalfThickness = 0.07f;

        static Mesh blades;

        /// <summary>4枚の刃をまとめたメッシュ (XY 平面。表は -Z、刃の先までの半径 1)</summary>
        public static Mesh Blades
        {
            get
            {
                if (blades == null) blades = BuildBlades();
                return blades;
            }
        }

        /// <summary>
        /// parent の下に手裏剣を1枚作る。radius = 刃の先までの半径 (m)。flat = 地面と平行に寝かせる (XZ 平面。投げる・手に持つ時)、
        /// false なら XY 平面に立てて表を -Z (プレイヤーの方) へ向ける (的の絵・忍者が持つ手裏剣)。当たり判定は付けない
        /// </summary>
        public static Transform Create(Transform parent, string name, float radius, Material bladeMat, Material centerMat, bool flat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            if (flat) root.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var b = new GameObject("Blades");
            b.transform.SetParent(root, false);
            b.transform.localScale = Vector3.one * radius;
            b.AddComponent<MeshFilter>().sharedMesh = Blades;
            var br = b.AddComponent<MeshRenderer>();
            br.sharedMaterial = bladeMat;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = "Center";
            var col = c.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
            c.transform.SetParent(root, false);
            c.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 円柱の軸を Z (面に垂直) に
            c.transform.localScale = new Vector3(CenterRadius * 2f * radius, CenterHalfThickness * radius, CenterRadius * 2f * radius);
            var cr = c.GetComponent<Renderer>();
            cr.sharedMaterial = centerMat;
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        // 4枚の三角形の刃 (厚みのある三角柱)。面ごとに頂点を分けて、角ばった陰影にする
        static Mesh BuildBlades()
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                int o = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(o); tris.Add(o + 1); tris.Add(o + 2);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

            var front = new Vector3(0f, 0f, -HalfThickness);
            var back = new Vector3(0f, 0f, HalfThickness);
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0f, 0f, i * 90f);
                Vector3 tip = rot * new Vector3(0f, 1f, 0f);
                Vector3 left = rot * new Vector3(-BaseHalfWidth, BaseDistance, 0f);
                Vector3 right = rot * new Vector3(BaseHalfWidth, BaseDistance, 0f);
                // 表 (-Z から見て時計回り) と裏
                Tri(tip + front, right + front, left + front);
                Tri(tip + back, left + back, right + back);
                // 2本の斜めの辺と、付け根の面 (外向き)
                Quad(tip + front, tip + back, right + back, right + front);
                Quad(left + front, left + back, tip + back, tip + front);
                Quad(right + front, right + back, left + back, left + front);
            }
            var mesh = new Mesh { name = "Shape_ShurikenBlades" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => blades = null; // (再生を始めるたびに作り直す)
    }
}
