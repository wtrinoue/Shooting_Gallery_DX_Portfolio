using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// 撃った時に銃から飛び出す薬きょう (見た目だけ。当たり判定なし)。
    /// 少し飛んでくるくる回りながら落ち、しばらくすると小さくなって消える。見た目は FxPool で使い回す。
    /// 形は先が開いた筒 (中が空洞で、底はふさがっている)。ショットガンの薬きょうは、赤い筒の根元に金色の雷管の台が付く
    /// </summary>
    public class ShellCasing : MonoBehaviour
    {
        const float Lifetime = 1.1f, ShrinkTime = 0.2f, Gravity = 9.8f;

        Vector3 velocity, spinAxis;
        float spinSpeed, age;
        Vector3 size;
        string poolKey;

        static Mesh tubeMesh;

        /// <summary>
        /// pos から velocity で飛ばす。rot = 薬きょうの向き (ローカルの +Y が口の向き)。diameter / length = 太さと長さ (m)。
        /// baseMat を渡すと、根元にその色の台 (ショットガンの雷管の台) を付ける
        /// </summary>
        public static void Spawn(Vector3 pos, Quaternion rot, Vector3 velocity, float diameter, float length, Material mat, Material baseMat = null)
        {
            if (mat == null) return;
            string key = "Shell_" + mat.name + (baseMat != null ? "_" + baseMat.name : "");
            var go = FxPool.Get(key, () => Create(key, mat, baseMat));
            var s = go.GetComponent<ShellCasing>();
            go.transform.SetPositionAndRotation(pos, rot);
            s.size = new Vector3(diameter, length, diameter);
            go.transform.localScale = s.size;
            s.velocity = velocity;
            s.spinAxis = Random.onUnitSphere;
            s.spinSpeed = Random.Range(600f, 1100f);
            s.age = 0f;
        }

        static GameObject Create(string key, Material mat, Material baseMat)
        {
            var g = new GameObject(key);
            var s = g.AddComponent<ShellCasing>();
            s.poolKey = key;
            var body = new GameObject("Body");
            body.transform.SetParent(g.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = TubeMesh();
            var r = body.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (baseMat != null)
            {
                // 根元の金色の台 (筒より少し太く、長さの 1/4)
                var cap = FxPool.Primitive(PrimitiveType.Cylinder, baseMat);
                cap.transform.SetParent(g.transform, false);
                cap.transform.localPosition = new Vector3(0f, 0.125f, 0f);
                cap.transform.localScale = new Vector3(1.08f, 0.125f, 1.08f);
            }
            return g;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            velocity += Vector3.down * (Gravity * dt);
            transform.position += velocity * dt;
            transform.Rotate(spinAxis, spinSpeed * dt, Space.World);
            float left = Lifetime - age;
            if (left < ShrinkTime) transform.localScale = size * Mathf.Max(0.01f, left / ShrinkTime);
            if (age >= Lifetime) FxPool.Release(poolKey, gameObject);
        }

        /// <summary>
        /// 先が開いた筒 (直径 1、底 y = 0 〜 口 y = 1)。外側の壁・内側の壁・口のふち・外の底・中の底があり、口から中の空洞が見える
        /// </summary>
        static Mesh TubeMesh()
        {
            if (tubeMesh != null) return tubeMesh;
            const int Sides = 12;
            const float Outer = 0.5f, Inner = 0.36f, InnerBottom = 0.18f; // 中の底は、根元から少し上 (厚い底)
            var verts = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                normals.Add(na); normals.Add(nb); normals.Add(nb); normals.Add(na);
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            }
            for (int s = 0; s < Sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / Sides, a1 = (s + 1) * Mathf.PI * 2f / Sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                // 外側の壁 (外向き)
                Quad(d0 * Outer, d0 * Outer + Vector3.up, d1 * Outer + Vector3.up, d1 * Outer, d0, d1);
                // 内側の壁 (中向き)
                Quad(d1 * Inner + Vector3.up * InnerBottom, d1 * Inner + Vector3.up, d0 * Inner + Vector3.up, d0 * Inner + Vector3.up * InnerBottom, -d1, -d0);
                // 口のふち (上向き)
                Quad(d0 * Inner + Vector3.up, d1 * Inner + Vector3.up, d1 * Outer + Vector3.up, d0 * Outer + Vector3.up, Vector3.up, Vector3.up);
                // 外の底 (下向き) と、中の底 (上向き)
                int b = verts.Count;
                verts.Add(Vector3.zero); verts.Add(d0 * Outer); verts.Add(d1 * Outer);
                normals.Add(Vector3.down); normals.Add(Vector3.down); normals.Add(Vector3.down);
                tris.AddRange(new[] { b, b + 1, b + 2 });
                b = verts.Count;
                verts.Add(Vector3.up * InnerBottom); verts.Add(d1 * Inner + Vector3.up * InnerBottom); verts.Add(d0 * Inner + Vector3.up * InnerBottom);
                normals.Add(Vector3.up); normals.Add(Vector3.up); normals.Add(Vector3.up);
                tris.AddRange(new[] { b, b + 1, b + 2 });
            }
            tubeMesh = new Mesh { name = "ShellCasingTube" };
            tubeMesh.SetVertices(verts);
            tubeMesh.SetNormals(normals);
            tubeMesh.SetTriangles(tris, 0);
            tubeMesh.RecalculateBounds();
            return tubeMesh;
        }
    }
}
